# Prototype Feature Pass 2: Directional Slap, Leg Sweep, Silliness

Goal: add directional slapping, a leg-sweep secondary action, and goofier physics and sound.
Rules for every phase:
- One worker at a time (Strata serves one request).
- A worker edits at most 2-3 files. Each phase lists the files it owns.
- Run the review gate (Phase 6) before the user playtests.
- No Unity Editor on host: every phase ends "untested". Commit after each phase.
- All tunables are [SerializeField] or constants in one place. All audio stays procedural (WebGL-safe, no asset files).
- Input System only. Unity 6000.1. `Rigidbody.linearVelocity`, not `velocity`.

## Design decisions (made)
- **Left mouse = slap. Right mouse = leg sweep.**
- **Directional slap:** hold LMB and drag the mouse. The drag vector (accumulated mouse delta, clamped) sets the swing angle. Release to swing. A plain click with no drag does the current default swing.
- While LMB is held, mouse delta goes to the aim vector, not the camera look. Look resumes on release.
- Swing direction in camera space: drag right gives a backhand to the right, drag up gives an uppercut, and so on. The impulse direction gets a lateral and vertical part from the drag angle.
- **Leg sweep:** RMB press gives a low horizontal arc at ankle height. It has a cooldown (0.6 s), a short reach, and a distinct hit zone. It applies an impulse at the feet plus torque and forces the NPC into Tumbling. It should make the NPC trip, not fly far.
- **Silliness:** a single `SillyPhysics` config holds the multipliers (impulse x1.6, NPC spin torque, floaty hang-time, bouncy physics material, squash-and-stretch on hit). Cap speeds so NPCs do not leave the arena.
- **Sounds:** procedural cartoon kit: boing, squeak, slide-whistle sweep, honk, whoosh. The NPC "yelp" pitch follows launch strength.

## Phase 1: Input and hit data (foundation)
Files: `Player/PlayerInputReader.cs`, `Slap/ISlappable.cs` (+ new `Slap/SlapHitInfo.cs` if needed).
- Add reader outputs: slap held/pressed/released, sweep pressed, accumulated aim delta while held (`Vector2`, with Consume/Reset).
- Add a hit-info struct (point, impulse, kind: Slap or Sweep, strength 0-1) and extend `ISlappable` to accept it. Keep the old method as a thin wrapper so nothing breaks.
- Acceptance: compiles by inspection, no behaviour change yet.

## Phase 2: Directional slap
Files: `Slap/SlapHand.cs`, `Player/FirstPersonController.cs` (look-suppression flag only).
- LMB press starts aim, drag accumulates, release swings along the drag angle. The hand start pose and arc rotate to the angle.
- Impulse direction uses the drag angle. Clamp the minimum drag so a tiny jitter counts as a plain click.
- Look suppression during the hold, with a `bool` on the controller.
- Acceptance: a click without drag matches the old behaviour. Drag in 4 directions gives 4 distinct swing arcs.

## Phase 3: Leg sweep
Files: new `Slap/LegSweep.cs`, `NPC/NpcController.cs` (receive-sweep handler only), `Core/GameBootstrap.cs` or `Slap/SlapBootstrap.cs` (add component).
- RMB triggers a low arc (SphereCast near y=0.2-0.5 in front of the player), swept visual (reuse the hand mesh lowered, or a simple capsule).
- NPC handler: impulse at the feet, torque about the forward axis, force Tumbling, reduced upward launch.
- Cooldown and a simple debug ray, as the slap has.
- Acceptance: a standing NPC trips and tumbles. A swept NPC still self-rights using the existing logic.

## Phase 4: Silly physics
Files: new `Core/SillyPhysics.cs` (static config and helpers), `NPC/NpcController.cs`, `Slap/SlapHand.cs` (use multipliers only).
- Impulse multiplier, spin torque on launch, a brief low-gravity "hang time" window after a launch, bouncier physic material on the NPC body, max speed clamp.
- Squash and stretch on hit (scale pulse on the visual, decays over about 0.25 s).
- One master "silliness" slider (0-1) for tuning.
- Acceptance: all values live in `SillyPhysics`. Setting silliness to 0 gives the current behaviour.

## Phase 5: Goofy sounds
Files: `Core/SlapAudio.cs` (extend), new `Core/NpcVoice.cs`.
- A `SoundKind` enum (Slap, Sweep, Boing, Squeak, Honk, SlideWhistle, Whoosh). Each is a procedural clip generated once, cached, WebGL-safe.
- Slap picks a random silly layer on top of the base hit. The sweep gets whoosh plus a boing on contact. An NPC launched hard gets a slide-whistle or yelp.
- `NpcVoice`: plays a yelp from the NPC position with pitch tied to strength. Rate-limit so many NPCs do not spam audio.
- Acceptance: the `SlapAudio.Play(Vector3, float)` public API still works. Add a new `Play(SoundKind, Vector3, float)`.

## Phase 6: Review gate and fixes
- Run the qwen3.8 reviewer read-only across the pass-2 diff. It has to check compile errors, Input System use, hit-stop and timeScale safety, allocations, and WebGL safety.
- Fix findings in a separate worker call, then commit.
- User opens Unity, pastes any compile errors, and playtests.

## Phase 7: Tuning (user-driven)
- User playtests and reports feel. Workers adjust constants only (impulse, cooldown, silliness defaults, volume).
- Out of scope: CI/CD and WebGL deployment (separate pass).
