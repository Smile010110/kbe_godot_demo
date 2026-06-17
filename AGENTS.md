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

Use this path only to inspect current protocol docs, generated-source inputs, config schema, and server behavior needed for client adaptation. Treat it as read-only from this client workspace.

## Standard Development Workflow

Use this flow for every non-trivial code change:

1. Read the relevant local code, config tables, scenes, and generated protocol signatures before editing.
2. Identify the owning layer first. Prefer protocol adapters and domain wrappers before UI or prefab changes.
3. Keep changes scoped to the requested behavior. Do not refactor unrelated code just because it is nearby.
4. Preserve user work in a dirty tree. Never revert unrelated changes unless explicitly requested.
5. Match the current server protocol and current config schema. This project is in development, so do not preserve old schemas or compatibility paths unless requested.
6. Run `dotnet build demo1.sln --no-restore` after code changes. Treat handwritten-code warnings as issues to fix; generated SDK warnings are expected.
7. Summarize changed behavior, files touched, and verification result when handing work back.

## Hard Boundaries

- Scope work to this client repository and client business logic only.
- Treat non-client repositories as read-only references. Do not patch, disable, or hotfix server scripts/assets/protocol sources from this workspace.
- `kbe_csharp_plugins/` is generated from the server protocol. Do not edit it by hand.
- When protocol changes, inspect generated types only to adapt handwritten code.
- Handwritten client code is C# only. Do not add GDScript.
- Prefer adapting wrappers in `Scripts/KBE/` before touching UI or prefabs.
- Do not add third-party packages or assets unless the feature clearly requires them.
- Do not commit, reset, discard, or revert changes unless the user explicitly asks.

## Main Layers

- `App.cs`: autoload singleton, config warmup, KBEngine initialization, disconnect recovery.
- `Scripts/KBE/`: handwritten KBEngine integration and protocol-facing wrappers.
- `Scripts/KBE/Protocol/`: adapters around generated protocol values and custom datatypes.
- `Scripts/KBE/Components/`: handwritten component shells and generated component callbacks.
- `Prefab/`: world entity controllers and entity scenes.
- `UI/`: login UI and world HUD.
- `common/Data/`: JSON config tables.
- `common/DataTables/`: C# config repositories.
- `kbe_csharp_plugins/`: generated C# SDK. Inspect only.

Only the handwritten KBEngine integration layer should depend on generated KBEngine concepts. Presentation code should use contracts such as `IWorldEntityView`, `ISelectableWorldEntityController`, and controller APIs.

## Current Entity Model

Current world entities:

- `Player`: local or remote player entity. Owns server time, role/sex metadata, combat state, motion state, buff state, and render binding.
- `Monster`: server-driven hostile entity. Owns combat/motion/buff state and render binding.
- `Npc`: server-driven non-combat entity. Owns motion state and render binding.

Shared contracts live in `Scripts/KBE/WorldEntityContracts.cs`:

- `IWorldEntityView`
- `ILocallyControlledWorldEntity`
- `IServerDrivenWorldEntity`
- `IWorldEntityRenderHooks`
- `ISelectableWorldEntityController`
- `ISkillCastPresentationController`

Render lifecycle is owned by `WorldEntityRenderBinding<TEntity, TController>`:

1. Entity `__init__()` subscribes to `World.OnWorldReady`.
2. Entity `onEnterWorld()` creates or binds its presentation node, or waits for world bootstrap.
3. Entity property callbacks call `RefreshRenderInfo()` or `RefreshRenderTransform()`.
4. Entity `onLeaveWorld()` / `onDestroy()` frees render nodes and unsubscribes.

## Protocol Adapter Rules

- Convert generated protocol values into handwritten models before presenting them.
- Keep generated structs/classes out of UI and prefab logic when possible.
- Put vector conversions in `Scripts/KBE/Protocol/KbeProtocolVectors.cs`.
- Put entity state reads in `Scripts/KBE/Protocol/KbeProtocolState.cs`.
- Put generated custom datatype conversion in focused adapter files such as `KbeProtocolBuffs.cs`.
- Do not show stale or fake protocol fields. If the server no longer syncs a value, remove it from presentation or use an explicit fallback.

## Config Tables

Config repositories follow a `Warmup()` pattern and load JSON from `common/Data/`.

Current relevant tables:

- `d_attr.json` -> `AttrConfigRepository`
- `d_buff.json` -> `BuffConfigRepository`
- `d_role.json` -> `RoleConfigRepository`
- `d_sex.json` -> `SexConfigRepository`
- `d_skill.json` -> `SkillConfigRepository`

Current appearance behavior:

- `PlayerAppearanceConfigRepository` still exists for model/profile lookup.
- `player_model_profiles.json` is no longer required in the current client data set.
- Missing appearance JSON should silently use the fallback profile instead of producing normal startup warnings.

Development-stage rules:

- Do not add backward compatibility for old config field names unless explicitly requested.
- Match the latest uploaded table schema directly.
- If a JSON schema changes, update the matching repository under `common/DataTables/` and warm it in `App.cs` if it is runtime-critical.
- Use `using var` for `FileAccess`.

## Skill State

The current client has skill UI/runtime scaffolding, cooldown handling, target checks, animation locks, floating text dispatch, and generated result/error adapters.

Current protocol caveat:

- `Player.TryCastSkill` is gated by `Player.CanCastSkills`.
- If the current generated `PlayerBase` does not expose a usable skill cell call, skill buttons must remain disabled and `TryCastSkill` should fail with an actionable warning.
- Do not fake skill success locally.

Current `d_skill.json` schema:

- `id`
- `name`
- `skill_type`
- `cast_type`
- `cost_mp`
- `cooldown`
- `gcd_group`
- `range_max`
- `target_type`
- `effect_type`
- `effect_value`
- `cast_delay_ms`
- `cast_without_target`
- `aoe_type`
- `aoe_radius`
- `aoe_angle`
- `aoe_width`
- `aoe_length`

Skill client rules:

- Local cooldown starts only after a request is sent successfully.
- Button disabled states should reflect local cooldown, global cooldown, pending cast lock, and skill protocol availability.
- Invalid target, out-of-range, cooldown, or MP failures should show a local message but must not clear the selected target.
- `cast_without_target != 0` skills are allowed to send target id `0`.
- Skill animation selection currently falls back to action type/state, because the current uploaded skill table no longer contains `animation_key`.
- HP/MP changes are displayed from server-synchronized combat fields, not local damage prediction.

## Buff State

Server buff sync is entity-level `buff_list` on `Player` and `Monster`.

Generated protocol types:

- `BUFF_INFO`: `buff_key`, `buff_id`, `level`, `duration`, `remain_time`, `stack`
- `BUFF_LIST`: `values`

Client adapter:

- `Scripts/KBE/Protocol/KbeProtocolBuffs.cs`
- `KbeBuffInfo` keeps protocol time values in milliseconds.
- UI summary displays remaining time in seconds using rounded-up seconds.
- Buff display names come from `BuffConfigRepository`.
- Buff attributes are described through `BuffConfigRepository` and `AttrConfigRepository`.

Presentation rules:

- Entity head info shows buff count only.
- HUD/target panel may show compact buff summaries.
- Do not predict buff application/removal locally. Use server `buff_list` changes.

## UI and UX Rules

- Clicking UI controls must not clear or invalidate selected world targets.
- World selection should be based on shared selectable/entity contracts, not monster-only assumptions.
- Target panel should tolerate missing targets, scene exit, and invalid Godot instances.
- Login persistence should only save when remember-login is enabled. Avoid writing the file on every keystroke; debounce and flush before exit.
- Avoid `GD.Print` spam during normal gameplay. Use warnings for unexpected states and remove temporary debug logs before handoff.

## Generated Protocol Update Workflow

1. Regenerate `kbe_csharp_plugins/` from the server.
2. Inspect generated method signatures, entity properties, component lists, and custom datatypes.
3. Update handwritten models/wrappers under `Scripts/KBE/`.
4. Update config repositories under `common/DataTables/` if JSON schemas changed.
5. Update UI/prefabs only through handwritten wrapper contracts.
6. Build with `dotnet build demo1.sln --no-restore`.

## Verification Checklist

Before final handoff for code changes:

- Build: `dotnet build demo1.sln --no-restore`.
- Search for temporary debug logs, stale protocol fields, and accidental generated-SDK edits.
- Confirm changed config keys match the current JSON schema.
- Confirm `kbe_csharp_plugins/` was not hand-edited unless the user explicitly requested generated SDK replacement.
- If scene/UI behavior changed, reason through local player, remote player, missing target, invalid target, disconnect, and scene-exit paths.
