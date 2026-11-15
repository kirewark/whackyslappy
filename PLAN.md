# WhackySlappy — Follow-up Plan

Status at 2026-11-14: Step 1 (arena, player) works in play test. Steps 2–5 exist in code but feel wrong.
Reference: `Design.md` (sections cited below). Each task = 1–2 files, done by a Qwen worker, reviewed + committed by the orchestrator.

## Phase A — Fix NPCs that "fall dead" (Design.md 4.1–4.3)

Likely causes (from code review, not yet confirmed in play):
1. **Center of mass is too high.** The head sphere is part of the compound collider at +1 m, so Unity puts the auto center of mass high. Gravity tips the NPC over more strongly than the righting torque (`uprightStrength` 8) can push back.
2. **A fallen NPC stays stuck in Wander.** NPCs go to Tumbling only from a slap or a speed above 4 m/s. An NPC that tips over slowly stays in Wander, where the righting torque is weak, and it never reaches GettingUp.
3. **The wander push makes NPCs tip.** `AddForce` pushes at the center of mass of a tall, narrow body.
4. **The body shape does not match Design 4.1.** The physics material (friction 0.4, bounce 0.2) and the eyes are missing.

Tasks:
- [ ] A1 `NpcController.cs`: set `rb.centerOfMass` low (about 0.3 m above the feet), like an inflatable punching clown. Add a [SerializeField] for it.
- [ ] A2 `NpcController.cs`: add a "fallen" check in Wander. If the NPC tilts more than 45°, go to Tumbling, so the normal recovery steps (settle, then GettingUp) run.
- [ ] A3 `NpcController.cs`: raise the righting strength. Add a tilt-proportional term, test at 25–40, and keep the damping.
- [ ] A4 `NpcController.cs`: add the physics material and two eye spheres (Design 4.1). Make sure the feet are at y=0 when the NPC spawns.
- [ ] A5 Add an on-screen debug overlay, toggled with F1 (Design 8). Show each NPC state count, the last hand speed and the last impulse. Do this first if A1–A3 do not fix the problem, because it shows the real state.

Done when: NPCs stand still and wander without falling over, and they get back up within 2 s after a slap.

## Phase B — Make the slap stronger and longer (Design 3.2–3.6)

Likely causes:
1. **No forward push.** `forwardBias` (0.6 × camera forward) is in the design but not in `SlapHand`, so NPCs get pushed sideways and do not fly away from the player.
2. **Short reach and small hit area.** `m_reach` is 0.8 m and `m_sweepRadius` is 0.2 m.
3. **Low impulse.** The impulse comes from the hand's speed in the swing animation, and the min/max limits (6–30 N·s) may never be reached.

Tasks:
- [ ] B1 `SlapHand.cs`: use the full Design 3.5 formula: `direction = normalize(normalize(handVel) + camFwd*forwardBias + up*upBias)`, `magnitude = clamp(|handVel|*slapStrength, min, max)`. Add `forwardBias` and `minImpulse` fields.
- [ ] B2 `SlapHand.cs`: increase the reach to about 1.3 m and the sweep radius to about 0.35 m. Make the hand bigger and cartoonish.
- [ ] B3 `SlapHand.cs`: log the hand speed and the impulse on each hit, to tune with real numbers. Keep it until the Phase D tuning is done.
- [ ] B4 Draw the impulse vector with `Debug.DrawRay` for 1 s on each hit (Design 8).

Done when: a standing slap knocks an NPC about 3–5 m, and a running slap clearly sends it farther.

## Phase C — Feedback / feel (Design 3.7, build step 6)
- [ ] C1 Play a placeholder slap sound on each hit (a generated tone is fine).
- [ ] C2 Add a camera kick of 1–2°, which recovers in 0.1 s.
- [ ] C3 Add an optional hit-stop (timeScale 0.05 for 0.04 s), toggled with a key.

## Phase D — Tuning pass (build step 7)
- [ ] D1 Test against the success criteria in Design 11. Change the tuning values in Design 3.6 and 4.2, and write the final values into Design.md.
- [ ] D2 Decide the open questions in Design 12 (stay down for comedy? wobbly head joint? backhand swing?).

## Phase E — Ship pipeline (project goal)
- [ ] E1 Use one scene: make `SampleScene` the only scene in Build Settings and rename it to `Arena`. (The play-test problem came from an Untitled scene.)
- [ ] E2 Add a GitHub Actions workflow: GameCI `unity-builder` for the WebGL build, then deploy to GitHub Pages. This needs `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` in the repo secrets (the user must add them).
- [ ] E3 Embed the playable build on the portfolio site.
- [ ] E4 Check 60 fps in the WebGL build (Design 11).

## Order
A (A5 first if blind) → B → user play test → C → D → E. Phase E can run in parallel any time after A.
