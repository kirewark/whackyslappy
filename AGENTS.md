# AGENTS.md — Rules for AI agents in this repo

Read this file before you change the repo. Read `Design.md` for the game design.

## Project

- **WhackySlappy** is a silly first-person game. The player slaps goofy cartoon NPCs, and the NPCs fly away with physics.
- This repo is **public** and is a portfolio piece. Keep commits, code, and docs clean.
- Current phase: **core gameplay prototype** (FPS movement, slap melee weapon, NPC physics). Do not add scoring, progression, menus, or art polish until the user asks. `Design.md` section 1 lists what is out of scope.

## Facts

| Item | Value |
|------|-------|
| Unity version | `6000.1.0f1` (see `Whackyslappy/ProjectSettings/ProjectVersion.txt`) |
| Unity project root | `Whackyslappy/` (not the repo root) |
| Render pipeline | Built-in |
| Input | Input System 1.14 **only**. Do not use `UnityEngine.Input` (legacy is disabled). |
| Input actions | `Whackyslappy/Assets/InputSystem_Actions.inputactions` (Player map: Move, Look, Attack, Jump, Sprint, ...) |
| Target platform | WebGL (GitHub Pages) |
| Unity Editor on agent host | **No.** Code is written without compiling. The user tests in the Editor. |

## Folder layout

Put all new game content under `Whackyslappy/Assets/_Project/`:

```
_Project/
  Scripts/
    Player/      FPS controller, camera, slap weapon
    NPC/         NPC behavior and physics reactions
    Core/        Game manager, spawner, shared code
    Editor/      Editor-only tools (put an asmdef or use the Editor folder rule)
  Prefabs/
  Materials/
  Audio/
  Scenes/
```

## Code rules

1. Namespace: `WhackySlappy.<Area>` (example: `WhackySlappy.Player`).
2. One `MonoBehaviour` per file. The file name must match the class name.
3. Use `[SerializeField] private` fields for tuning values. Do not hard-code tuning numbers in logic.
4. Put physics code in `FixedUpdate`. Put input reading and camera code in `Update` / `LateUpdate`.
5. Use `Rigidbody.AddForce` / `AddForceAtPosition` for slap hits. Do not set `transform.position` on physics objects.
6. Add a short XML `<summary>` comment to each public class. Write comments in simple, clear English.
7. Because no Editor is available, keep code simple and compile-safe. Use only APIs that exist in Unity 6. Do not invent APIs.

## Scenes and assets

- Do **not** hand-edit `.unity` or `.prefab` YAML. It breaks easily.
- To build a test level, write a runtime bootstrap script or an Editor menu script (`Tools > WhackySlappy > Build Test Arena`) that creates objects in code.
- Unity creates `.meta` files on import. Commit `.meta` files when they exist. Never delete a `.meta` file for an asset that still exists.

## Git rules

- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `Build/`, or `Builds/`. The `.gitignore` covers them.
- `git-lfs` is **not** installed on the agent host. Do not add binary files that match `.gitattributes` LFS patterns until LFS is set up.
- Commit author: `Kire Wark <200969737+kirewark@users.noreply.github.com>`.
- Use Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `ci:`.
- Never force-push or rewrite history without explicit approval from the user.
- Never put tokens or secrets in files, remote URLs, or commit messages.

## CI and pipeline

`.github/workflows/ci.yml` runs on every push, every branch:
`lint` (GitHub-hosted) → `test` → `build-webgl` → `staging` → `screenshot`
(the last four on the self-hosted Unity runner, a Proxmox VM).

- `lint`: blocks committed Unity junk dirs, requires a `.meta` next to every asset and
  folder under `Assets/`, and fails non-`kirewark` authors who touch `.github/`.
- `test`: game-ci `unity-test-runner@v4.3.2`, `testMode: all` (EditMode + PlayMode;
  roughly 38 EditMode and 3 PlayMode tests).
- `build-webgl`: game-ci `unity-builder@v5.0.1`, target WebGL.
- `staging`: rsyncs the build to the LAN web host; each branch plays at
  `http://staging.lan/whacky/<branch>/` (the screenshot job reaches the same host at
  `http://10.10.10.11/whacky/<branch>/`).
- `screenshot`: headless Playwright boots the staged build (`screenshot.png`), then
  takes photo-mode shots `?shot=overview|slap|getup` (4 frames each at 0.25/0.75/1.5/3 s).
  Artifact `screenshots-<sha>` holds the frames, `smoke.json` telemetry and `summary.md`.

## Agent workflow rules

- Never edit anything under `.github/` (the `lint` job enforces this).
- `ProjectSettings/` changes require the owner's explicit approval.
- One branch + one PR per small task; merge only once `lint`, `test` and `build-webgl`
  are green (branch protection on `main`).
- Every new asset, script or folder needs a hand-written `.meta`: unique GUID from
  `python3 -c "import uuid; print(uuid.uuid4().hex)"`, `fileFormatVersion: 2`. There is
  no Editor to generate them, and lint fails without them.
- New folders that have an asmdef also need an Editor-only asmdef for their `Editor/` folder.
- Runtime materials only via `DefaultLitMaterial.Create()` (WebGL strips unused shaders).
- URL query options the player reads: `?q=low|high|off` (graphics), `?fps=1` (fps
  counter + telemetry), `?shot=overview|slap|getup` (photo mode).
- Visual changes: before merging, review the screenshot frames AND the `smoke.json`
  telemetry in the `screenshots-<sha>` artifact.

## Gameplay facts (implemented, 2026-11)

- Slap: hold left mouse to charge aim (drag steers the swing); mouse-look slows to 35%
  while held. Right mouse = leg sweep.
- NPC AI: wander → aggro inside 8 m (drops only past 13 m) → chase → telegraphed
  no-damage strike (camera bump + whoosh sound).
- Face expressions: Idle, Angry, Recover, Hurt, Hit, Dizzy, Dead.
- Spawning: max 4 alive, one replacement every 3 s, ≥12 m from the player and ≥2.5 m
  from corpses; max 6 corpses.
- Spatial audio: logarithmic rolloff 2–45 m; NPC voice sources follow their NPC.
- Arena props are combined into a few meshes to keep draw calls low.

## Hand-off notes for delegated agents

- You do not see the orchestrator's conversation. This file and `Design.md` are your context.
- Report the exact files you changed. Do not claim code works unless you compiled or ran it. Without an Editor, say "untested".
