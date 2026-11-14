# WhackySlappy — Prototype Design Document

**Genre:** First-person physics slap comedy
**Platform:** Web (WebGL, GitHub Pages)
**Engine:** Unity 6000.1.0f1, Built-in render pipeline
**Status:** Prototype v0 — core gameplay loop only

---

## 1. Purpose of this prototype

Find out if slapping a goofy NPC **feels good**. That is the only question this prototype must answer.

The prototype has three systems:

1. **FPS controller** — move, look, jump, sprint.
2. **Slap** — an equipped hand (melee weapon) that hits NPCs with a **physics hit**.
3. **NPC physics reactions** — NPCs take the hit, tumble, fly, and recover or fall off.

### Out of scope (do not build)

- Scoring, points, combos, waves, high scores.
- Progression, upgrades, unlocks, more weapons.
- Menus, HUD (except a crosshair), intro text.
- NPC attacks, player health, win or lose state.
- Final art, animation rigs, music.
- CI/CD and WebGL deploy (next phase, not this document).

---

## 2. Core gameplay loop

```
Walk up to NPC → Swing hand → Hand physically hits NPC → NPC tumbles / flies → NPC gets up or falls off → New NPC appears → Repeat
```

1. The player stands on a flat platform with NPCs on it.
2. NPCs wander slowly toward the player.
3. The player swings the hand. The hand moves through the world on a real path.
4. If the hand touches an NPC, physics applies an impulse at the **contact point**. A faster hand hits harder. A hit off-center makes the NPC spin.
5. The NPC loses balance, tumbles, and slides or flies.
6. If the NPC stays on the platform, it stands back up after a short time.
7. If the NPC falls off the platform, it despawns. A new NPC spawns after 2–4 s.

---

## 3. The physics hit (main feature)

### 3.1 Rule

The slap is **not** a raycast that "launches" whatever is in front of the camera. The hand is a physical object that moves along a swing path. The NPC reacts only if the hand actually touches it. The force comes from **how fast the hand moves at the moment of contact**.

### 3.2 Hand object

| Part | Setup |
|------|-------|
| Visual | Primitive (flattened sphere or cube "palm"), child of the camera rig. |
| Body | `Rigidbody`, `isKinematic = true`, interpolation on. |
| Collider | `SphereCollider`, radius ≈ 0.2 m. Layer `PlayerHand`. |
| Motion | Code drives the hand in `FixedUpdate` with `MovePosition` / `MoveRotation`, along a curve in camera space. No Animator. |

### 3.3 Swing (one slap)

A forehand slap: from the right, across the front of the player, to the left.

| Phase | Time | Hand position (camera space, m) | Can hit? |
|-------|------|----------------------------------|----------|
| Rest | — | (0.35, −0.30, 0.50) | No |
| Wind-up | 0.08 s | → (0.70, −0.10, 0.45) | No |
| Strike | 0.12 s | → arc through (0.0, −0.05, 1.10) → (−0.50, −0.10, 0.70) | **Yes** |
| Recover | 0.20 s | → back to Rest | No |

- Total swing ≈ 0.40 s. A new swing cannot start until the current one ends. (This is the cooldown.)
- The input action is `Player/Attack` (left mouse button).

### 3.4 Hit detection

A fast hand can pass through an NPC between two physics steps. To prevent that:

1. During the **Strike** phase, in each `FixedUpdate`, do a `SphereCastAll` from the hand's previous position to its new position (hand radius, `NPC` layer mask).
2. For each NPC that the cast touches, use the first contact point.
3. Hit each NPC **one time per swing** at most. Keep a set of NPCs hit during this swing.

### 3.5 Impulse

```
handVelocity  = (handPosNow − handPosPrev) / Time.fixedDeltaTime   // world space; includes player movement
direction     = normalize(normalize(handVelocity) + cameraForward * forwardBias + Vector3.up * upBias)
magnitude     = clamp(|handVelocity| * slapStrength, minImpulse, maxImpulse)
npcBody.AddForceAtPosition(direction * magnitude, contactPoint, ForceMode.Impulse)
```

- Because `handVelocity` is in world space, running into a slap hits harder automatically. Do not add a separate player-speed bonus.
- Because the force is applied at the contact point, hits on the head or shoulder make the NPC spin. Do not add fake spin for v0.
- After the hit, the NPC switches to the **Tumbling** state (see 4.3).

### 3.6 Starting tuning values

Put all of these on the component as `[SerializeField]` fields. Expect to change them during play tests.

| Value | Start | Notes |
|-------|-------|-------|
| `slapStrength` | 1.5 | Impulse per (m/s) of hand speed |
| `minImpulse` | 6 N·s | A weak touch still does something |
| `maxImpulse` | 30 N·s | Stops absurd launches |
| `forwardBias` | 0.6 | Pushes NPCs away from the player, not only sideways |
| `upBias` | 0.4 | Lift, so NPCs leave the ground |
| Hand radius | 0.2 m | Bigger is more forgiving |

Rough check: the strike moves about 1 m in 0.12 s ≈ 8 m/s → 12 N·s on a 1.5 kg NPC → about 8 m/s launch speed.

### 3.7 Feedback (minimal, only for feel)

- One placeholder slap sound on hit.
- Small camera kick on hit (≈ 1–2°, recovers in 0.1 s).
- Optional hit-stop: freeze `Time.timeScale` at 0.05 for ≈ 0.04 s. Make this a toggle so we can compare.

---

## 4. NPCs

### 4.1 Body (primitives)

| Part | Setup |
|------|-------|
| Body | Capsule, height 1.6 m, radius 0.35 m. `Rigidbody` mass 1.5 kg, linear damping 0.1, angular damping 0.5. Collision detection: Continuous Dynamic. |
| Head | Sphere, radius 0.3 m, on top of the body. Same rigidbody (compound collider) for v0. |
| Face | Two eye spheres + a colored material. Goofy is enough. |
| Physics material | Friction 0.4, bounciness 0.2. |
| Layer | `NPC` |

A wobbly head on a joint, or a full ragdoll, is a **later** option. Use one rigidbody first, and only add joints if the single body does not feel funny enough.

### 4.2 Balance

An upright NPC stays standing with a **self-righting torque** (like an inflatable punching clown):

```
torque = Vector3.Cross(transform.up, Vector3.up) * uprightStrength − angularVelocity * uprightDamping
```

Apply it in `FixedUpdate`, only in the Idle and Wander states. Freeze nothing. The NPC must be free to fall over when hit.

### 4.3 States

```
Wander → (hit) → Tumbling → (still, on ground) → GettingUp → Wander
                    └→ (below kill height) → Despawn → respawn after 2–4 s
```

| State | Behavior |
|-------|----------|
| **Wander** | Walk slowly toward the player (≈ 1.0 m/s) with `AddForce`. Self-righting on. Stop pushing when closer than 1.2 m. |
| **Tumbling** | No self-righting, no walking. Pure physics. Ignore new hits for 0.1 s to stop double hits. |
| **GettingUp** | Starts when speed < 0.3 m/s for 1.0 s and the NPC is on the ground. Self-righting on with extra strength until upright, then Wander. |
| **Despawn** | When the NPC's Y < −10 m. Destroy (or return to a pool). Ask the spawner for a replacement. |

A hit during Wander or GettingUp sends the NPC to Tumbling.

### 4.4 Spawner

- Keep **5** NPCs alive at the same time (tunable).
- Spawn at random points on the platform, at least 6 m from the player.
- Respawn delay: random 2–4 s.

---

## 5. Player controller

| Item | Value |
|------|-------|
| Body | `CharacterController`, height 1.8 m, radius 0.35 m. Layer `Player`. |
| Walk speed | 5 m/s |
| Sprint | × 1.6 (hold `Player/Sprint`) |
| Jump | ≈ 1.0 m high (`Player/Jump`) |
| Gravity | −20 m/s² (snappier than real gravity) |
| Look | `Player/Look`, mouse sensitivity setting, pitch clamped to ±85° |
| Cursor | Locked during play. **Esc** unlocks. Click relocks. (WebGL needs a click before it can lock the pointer.) |

The player does not push NPCs by walking into them in v0. The slap is the only interaction.

---

## 6. Test arena

| Item | Setup |
|------|-------|
| Platform | Flat floor, 20 m × 20 m, over empty space. |
| Edges | No walls for NPCs. Slapping NPCs off the edge is the payoff. |
| Player fence | Invisible walls on layer `PlayerBlocker` around the edge. They block the player only. |
| Kill height | Y = −10 m (NPCs below this despawn). |
| Lighting | One directional light + ambient. |
| Look | Grid or checker floor so speed and distance are easy to read. |

The arena is created **in code** by a bootstrap script (no hand-edited scene YAML). See `AGENTS.md`.

### Layers and collisions

| Layer | Collides with |
|-------|---------------|
| `Player` | Environment, PlayerBlocker, NPC |
| `PlayerHand` | Nothing (the hand uses casts, not collisions) |
| `NPC` | Environment, NPC, Player |
| `Environment` | Everything except PlayerBlocker |
| `PlayerBlocker` | Player only |

---

## 7. Controls

All input comes from `Assets/InputSystem_Actions.inputactions`, map `Player`.

| Action | Input action | Default binding |
|--------|--------------|-----------------|
| Move | `Move` | WASD |
| Look | `Look` | Mouse |
| Jump | `Jump` | Space |
| Sprint | `Sprint` | Left Shift |
| Slap | `Attack` | Left mouse button |

---

## 8. Debug tools (for tuning, not for players)

- Gizmos: draw the hand's swing path and the hit sphere in the Scene view.
- On each hit, draw the impulse vector for 1 s (`Debug.DrawRay`).
- Optional on-screen text (toggle with **F1**): last hand speed, last impulse, NPC state counts.

These tools help us tune the feel. They are not a score.

---

## 9. Technical specs

- **Unity:** 6000.1.0f1. **Render pipeline:** Built-in.
- **Input:** Input System 1.14 only. Do not use the legacy `Input` class.
- **Physics:** PhysX, fixed timestep 0.02 s (default). Try 0.01667 s if fast hits feel inconsistent.
- **Target:** WebGL 2.0, Chrome / Firefox / Edge, 60 fps on a mid-range laptop.

---

## 10. Build order

Each step must be play-testable before the next step starts.

| # | Step | Test (in the Editor) |
|---|------|----------------------|
| 1 | Bootstrap arena + layers + player controller | You can walk, sprint, jump, and look. You cannot leave the platform. |
| 2 | Hand + swing path (no hits yet) | Click: the hand swings right-to-left smoothly. You cannot spam it. |
| 3 | Hit detection + impulse | A box with a `Rigidbody` placed on the floor gets knocked away. A faster approach knocks it farther. |
| 4 | NPC body + self-righting + states | NPCs stand, tumble when slapped, and get back up. |
| 5 | NPC wander + spawner + despawn | NPCs walk to you. NPCs knocked off the edge are replaced. |
| 6 | Feedback + debug overlay | Sound, camera kick, and hit-stop toggle work. F1 shows values. |
| 7 | Tuning pass | Change values in section 3.6 until the success criteria pass. |

---

## 11. Success criteria

The prototype is done when all of these are true:

- [ ] Slapping an NPC feels good: tactile, with a clear response.
- [ ] The NPC reaction depends on **where** and **how fast** you hit it.
- [ ] A running slap is clearly stronger than a standing slap.
- [ ] The hand never passes through an NPC without a hit.
- [ ] You can slap 20 NPCs in a row with no stuck, frozen, or jittering NPCs.
- [ ] NPCs knocked off the edge are replaced. NPCs left on the platform get up.
- [ ] 60 fps in a WebGL build.

---

## 12. Open questions (decide after first play test)

1. Should NPCs always get back up, or stay down for a few seconds for comedy?
2. Single rigidbody or wobbly head on a joint?
3. Is right-to-left the only swing, or do we alternate forehand and backhand?
