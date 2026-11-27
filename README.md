# WhackySlappy

A silly first-person game: you wander a cartoon arena and slap goofy ragdoll NPCs,
sending them flying with exaggerated physics. The NPCs wander, get angry, chase you
and swing back (telegraphed, never damaging), then get up dizzy and wander off. The
whole game — arena, NPC bodies, faces, materials and sounds — is generated
procedurally in C#. The repo is also a portfolio piece for game-dev CI/CD: every
push is linted, tested, built to WebGL, deployed to a LAN staging URL and
photographed by a headless browser, so AI agents can "see" what their code did
without ever opening a Unity Editor.

<!-- TODO: live demo link -->

## Pipeline

    push -> lint -> test (EditMode + PlayMode) -> build-webgl -> staging
           -> screenshot evidence -> PR review -> merge -> GitHub Pages [planned]

Every branch auto-plays on the LAN at `http://staging.lan/whacky/<branch>/`. The
screenshot job boots the build, captures `?shot=overview|slap|getup` frames plus
JSON telemetry (artifact `screenshots-<sha>`); reviewers check frames AND telemetry
before merging visual changes.

## Controls

Bindings from `Whackyslappy/Assets/_Project/Scripts/Player/PlayerInputReader.cs`
(see `Design.md` for feel notes):

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | WASD | left stick |
| Look | mouse (slowed to 35% while charging a slap) | right stick |
| Jump | Space | buttonSouth |
| Sprint | Left Shift | left stick press |
| Slap (hold to charge aim, drag steers the swing) | left mouse button | buttonWest |
| Leg sweep | right mouse button | — |

## Tech

- Unity 6 (`6000.1.0f1`), built-in render pipeline, WebGL target.
- Input System 1.14 only — legacy `UnityEngine.Input` is disabled.
- CI: GitHub Actions (`.github/workflows/ci.yml`) on a self-hosted Unity runner
  (Proxmox VM); game-ci `unity-test-runner@v4.3.2` and `unity-builder@v5.0.1`;
  Playwright/Chromium screenshot gate. Roughly 38 EditMode + 3 PlayMode tests.
- Runtime materials go through `DefaultLitMaterial.Create()` so WebGL shader
  stripping can't blank the scene.

## URL options

- `?q=low|high|off` — graphics quality (default high).
- `?fps=1` — on-screen FPS counter, used for perf telemetry.
- `?shot=overview|slap|getup` — photo mode; stages scripted scenes for CI shots.

## Development notes

AI-assisted workflow: an orchestrator agent plans and reviews, local worker models
implement. No Unity Editor runs on the dev hosts — CI is the compiler, and the
staging + screenshot jobs are the playtest. Repo rules for agents are in
`AGENTS.md`, game design in `Design.md`, current plan in `PLAN.md`.
