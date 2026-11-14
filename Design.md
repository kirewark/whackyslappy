# WhackySlappy — Game Design Document

**Genre:** First-person physics slap comedy  
**Platform:** Web (WebGL build, GitHub Pages)  
**Engine:** Unity (URP or Built-in, WebGL target)  
**Status:** Prototype v0  

---

## 1. Concept

A fast-paced first-person game where your **only weapon is your hand**. You walk through a space filled with goofy-looking NPCs. You slap them. They fly away with hilarious physics-based reactions.

The core loop is simple, tactile, and funny. Every slap should feel satisfying.

---

## 2. Core Gameplay Loop

```
Spawn in arena → NPCs appear → Slap them → They fly off with physics → Score points → Repeat
```

1. Player spawns into a simple arena (room, courtyard, or platform).
2. NPCs spawn at various positions in the arena.
3. Player approaches an NPC and **slaps** it with their right hand (melee weapon).
4. The slap applies a physics force to the NPC based on:
   - Player movement speed at time of contact
   - Distance from the NPC (closer = harder slap)
   - A base slap force multiplier
5. NPC reacts with a physics-based animation (flinch, spin, ragdoll).
6. After being launched, the NPC flies off the platform/arena and is removed.
7. Player earns points for each slap. New NPCs spawn after a delay.
8. Wave counter increases. More NPCs per wave, slightly faster spawns.

### Victory / End Condition

No explicit win condition for the prototype. The game runs as an **endless slap arcade** — how many can you slap before getting overwhelmed? High score is tracked per session.

---

## 3. Player Mechanics

### Movement
- **WASD** — Move forward/left/back/right
- **Mouse** — Look around (pointer lock, standard FPS)
- **Space** — Jump (small jump, ~3m)
- **Sprint:** Hold **Shift** — 1.6x movement speed
- Grounded check: raycast down 0.1m to confirm standing on floor

### Slap (Melee Weapon)
- **Left Mouse Button** — Slap
- Equipped by default (always ready)
- **Animation:** Quick right-hand swing motion (0.25s forward, 0.15s recover)
- **Hit detection:** Short-range raycast (1.5m reach, 0.5m wide arc)
- **Cooldown:** 0.4s between slaps (prevents rapid-fire)
- **Slap force:**
  - Base: 15 units forward + upward
  - Modified by player velocity at impact (adds player's movement vector * 0.5)
  - Randomized slightly (±10%) for variety

### Audio
- Slap sound plays on hit (cartoon "smack" or "punch")
- Footsteps on floor
- Ambient room tone

---

## 4. NPC Design

### Appearance (Prototype)
- Simple capsule or cube bodies with goofy face textures (or Unity primitives with colored materials)
- Each NPC has:
  - **State machine:** Idle → Flinch → Fly → Offscreen → Despawn
  - **Health:** 1 hit = KO (no health bar)
  - **Physics body:** Rigidbody with moderate mass (1.0-2.0)
  - **Bounciness:** Low friction on floor, moderate bounce (0.2-0.3)

### Behavior
- **Idle:** Slowly bob up and down, rotate slightly (loafing around)
- **On slap:** Apply rigidbody velocity in slap direction, enable ragdoll or spin animation
- **After slap:** Spin through air, flail limbs, land with a thud
- **Offscreen:** When player is >20m away, despawn and remove from scene
- **Respawn:** New NPC spawns at random edge position after 2-4 seconds

### Variety (Future, not prototype)
- Different sizes (small = flies farther, big = more force needed)
- Different weights (light ones fly far, heavy ones stumble)
- Different speeds (slow wanderers vs fast runners)

---

## 5. Arena

### Prototype Layout
- Single flat room: 20m x 20m floor
- 4 walls (3m tall) — NPCs can't walk through walls but CAN fly over them
- Ceiling optional (open or closed)
- Simple lighting: one directional light + ambient
- Floor: grid pattern or concrete material
- Walls: plain color with maybe some posters/graffiti (simple textures)

### Future Expansion
- Multiple arenas (room, courtyard, rooftop)
- Hazards (trampolines, fans that blow NPCs)
- Obstacles to navigate around

---

## 6. UI / HUD

### Minimal Prototype UI
- **Score counter** — top-left corner, large readable text
- **Wave counter** — top-center
- **Slap cooldown indicator** — subtle ring or bar around crosshair (optional)
- **Crosshair** — simple dot or small circle in center

### Text on Screen
- "SLAP THE NPCs" — intro text for 3 seconds
- "WAVE X" — appears briefly when wave changes
- "SCORE: XXX" — always visible

---

## 7. Controls Summary

| Action | Input |
|--------|-------|
| Move | WASD |
| Look | Mouse (pointer lock) |
| Jump | Space |
| Sprint | Left Shift |
| Slap | Left Mouse Button |

---

## 8. Technical Specs

- **Unity version:** 6000.1.0f1 (Unity 6)
- **Render pipeline:** Built-in (no URP package installed; simpler for WebGL)
- **Target:** WebGL, Chrome/Firefox/Edge
- **Build target:** WebGL 2.0
- **Max build size target:** <50MB for initial load
- **Physics:** Unity PhysX, standard settings
- **Input:** Unity Input System 1.14 only (`activeInputHandler: 1`). The legacy `Input` class is disabled and will throw errors.

---

## 9. Asset Requirements (Prototype)

| Asset | Source | Notes |
|-------|--------|-------|
| NPC model | Unity primitives | Capsule + cube head, colored materials |
| NPC faces | Simple textures | Drawn in paint or generated |
| Floor/wall materials | Unity built-in | Concrete, brick, or flat colors |
| Slap sound | Free SFX library | Cartoon slap/punch sound |
| Footstep sound | Free SFX library | Simple footstep loop |
| Background music | Optional | None for prototype |

---

## 10. Build Order (Prototype Phase)

1. **FPS camera + movement** — WASD, mouse look, jump, sprint
2. **Slap mechanic** — Raycast, animation, physics force application
3. **NPC spawn system** — Wave manager, spawn points, respawn logic
4. **NPC behavior** — Idle animation, flinch on hit, physics response
5. **Scoring** — Points per slap, wave counter
6. **UI** — HUD overlay, intro text
7. **Polish** — Sounds, particle effects on slap, screen shake

---

## 11. Success Criteria for Prototype

- Slapping an NPC feels good (tactile, satisfying force)
- NPC flies away in a believable (but funny) way
- You can slap at least 10 NPCs in a row without issues
- No crashes, no stuck NPCs, clean respawns
- Runs at 60fps in WebGL build on a mid-range laptop
