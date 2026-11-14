# AGENTS.md — Rules for AI agents in this repo

Read this file before you change the repo. Read `Design.md` for the game design.

## Project

- **WhackySlappy** is a silly first-person game. The player slaps goofy cartoon NPCs, and the NPCs fly away with physics.
- This repo is **public** and is a portfolio piece. Keep commits, code, and docs clean.
- Current phase: **core gameplay prototype** (FPS movement, slap melee weapon, NPC physics). Do not add progression, scoring depth, menus, or art polish until the user asks.

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
- Commit author: `Kire Wark <kirewark@users.noreply.github.com>`.
- Use Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `ci:`.
- Never force-push or rewrite history without explicit approval from the user.
- Never put tokens or secrets in files, remote URLs, or commit messages.

## Hand-off notes for delegated agents

- You do not see the orchestrator's conversation. This file and `Design.md` are your context.
- Report the exact files you changed. Do not claim code works unless you compiled or ran it. Without an Editor, say "untested".
