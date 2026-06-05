# AGENTS.md

Guidance for Codex agents working in this repository.

## Project

Godot `4.4.1 mono` C# client demo for KBEngine.

Build from the repository root:

```powershell
dotnet build demo1.sln --no-restore
```

Generated SDK warnings from `kbe_csharp_plugins/` are expected during local builds.

## Server Reference

Server project reference path: `D:\UGit\KBEngine\base_assets`.

Use this path only to inspect current protocol docs, generated-source inputs, and server behavior needed for client adaptation. Treat it as read-only from this client workspace.

## Standard Development Workflow

Use this flow for every non-trivial code change:

1. Read the relevant local code, config tables, scenes, and generated protocol signatures before editing.
2. Identify the owning layer first. Prefer protocol adapters and domain wrappers before UI or prefab changes.
3. Keep changes scoped to the requested behavior. Do not refactor unrelated code just because it is nearby.
4. Preserve user work in a dirty tree. Never revert unrelated changes unless explicitly requested.
5. Make the implementation match the current server protocol and current config schema. This project is in development, so do not preserve old schemas or compatibility paths unless requested.
6. Run `dotnet build demo1.sln --no-restore` after code changes. Treat handwritten-code warnings as issues to fix; generated SDK warnings are expected.
7. Summarize changed behavior, files touched, and verification result when handing work back.

## Hard Boundaries

- Scope work to this client repository and client business logic only. Do not modify other projects such as server assets or protocol source repositories; inspect them only when needed for client adaptation.
- Treat all non-client repositories as read-only references. Do not patch, disable, or hotfix server scripts/assets/protocol sources from this workspace; when a server-side issue is suspected, report the finding and the exact file/behavior for the user to handle.
- `kbe_csharp_plugins/` is generated from the server protocol. Do not edit it by hand.
- When protocol changes, inspect generated types only to adapt handwritten code.
- Handwritten client code is C# only. Do not add GDScript.
- Prefer adapting wrappers in `Scripts/KBE/` before touching UI or prefabs.
- Do not add new third-party packages or assets unless the feature clearly requires them.
- Do not commit, reset, or discard changes unless the user explicitly asks.

## Main Layers

- `App.cs`: autoload, config warmup, KBEngine initialization, disconnect recovery.
- `Scripts/KBE/`: handwritten KBEngine integration and protocol-facing wrappers.
- `Scripts/KBE/Protocol/`: adapters around generated protocol values.
- `Prefab/`: world entity controllers and entity scenes.
- `UI/`: login UI and world HUD.
- `common/Data/`: JSON config tables.
- `common/DataTables/`: C# config repositories.

## Code Quality Rules

- Keep protocol-facing code strongly typed. Convert generated protocol values into handwritten models under `Scripts/KBE/`.
- Prefer small, named methods over long inline branches when logic is reused or crosses responsibilities.
- Avoid god classes. If a controller grows behavior in multiple domains, move presentation, dispatch, parsing, or state helpers into focused classes under the owning layer.
- Keep runtime state explicit and reset it on cancellation, scene exit, or ownership changes.
- Avoid silent failure for server-authoritative events. Log actionable warnings when a result cannot be presented or mapped.
- Do not spam `GD.Print` during normal gameplay. Use warnings for unexpected states and remove temporary debug logs before handoff.
- Use `using var` for `FileAccess` and other disposable resources.
- Do not show stale or fake protocol fields in UI. If the server no longer syncs a value, remove it from presentation rather than displaying `0`.

## Config Tables

Config repositories follow a `Warmup()` pattern and load JSON from `common/Data/`.

Current relevant tables:

- `d_role.json` -> `RoleConfigRepository`
- `d_sex.json` -> `SexConfigRepository`
- `d_skill.json` -> `SkillConfigRepository`
- `player_model_profiles.json` -> `PlayerAppearanceConfigRepository`

Development-stage rule: do not add backward compatibility for old config field names unless explicitly requested. Match the latest uploaded table schema directly.

## Skills

Skill casting uses the server-authoritative flow:

1. Client calls generated `cast_skill(skillId, targetId, extData)` through handwritten `Player.TryCastSkill`.
2. Client starts local cooldown only after the request is sent successfully.
3. Client plays the configured skill animation immediately using `cast_delay_ms` so the impact frame lines up with delayed server settlement.
4. Server broadcasts entity-level `on_skill_result(SKILL_RESULT)` to AOI players or monsters.
5. `Player` / `Monster` converts generated `SKILL_RESULT` into `SkillCastResult` and publishes it through handwritten dispatch/presentation code.
6. Local-player results may be queued until the configured impact time. Remote caster results should play the matching animation without duplicating local floating text.
7. HP/MP changes are displayed from Combat component synchronization, not local damage prediction.

Skill config drives local test buttons, range checks, and local animation lock duration.
Skill animation should be data-driven by config such as `animation_key`; avoid hardcoding behavior by skill id.

## Generated Protocol Update Workflow

1. Regenerate `kbe_csharp_plugins/` from the server.
2. Inspect generated method signatures and custom datatypes.
3. Update handwritten models/wrappers under `Scripts/KBE/`.
4. Update config repositories under `common/DataTables/` if JSON schemas changed.
5. Build with `dotnet build demo1.sln --no-restore`.

## Git Safety

The tree may contain user changes. Do not revert unrelated edits.

## UI and UX Rules

- Clicking UI controls must not clear or invalidate selected world targets.
- Invalid skill target, out-of-range, cooldown, or MP failures should show a local message but must not clear the selected target.
- World selection should be based on shared selectable/entity contracts, not monster-only assumptions.
- Button disabled states should reflect local cooldown, global cooldown, pending cast lock, and local player availability.
- Login persistence should only save when the user enabled remember-login. Avoid writing the file on every keystroke; debounce and flush before exit.

## Verification Checklist

Before final handoff for code changes:

- Build: `dotnet build demo1.sln --no-restore`.
- Search for temporary debug logs, stale protocol fields, and accidental generated-SDK edits.
- Confirm changed config keys match the current JSON schema.
- If scene/UI behavior changed, reason through local player, remote player, missing target, invalid target, and disconnect/scene-exit paths.
