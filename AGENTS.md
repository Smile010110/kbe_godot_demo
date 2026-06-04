# AGENTS.md

Guidance for Codex agents working in this repository.

## Project

Godot `4.4.1 mono` C# client demo for KBEngine.

Build from the repository root:

```powershell
dotnet build demo1.sln --no-restore
```

Generated SDK warnings from `kbe_csharp_plugins/` are expected during local builds.

## Hard Boundaries

- `kbe_csharp_plugins/` is generated from the server protocol. Do not edit it by hand.
- When protocol changes, inspect generated types only to adapt handwritten code.
- Handwritten client code is C# only. Do not add GDScript.
- Prefer adapting wrappers in `Scripts/KBE/` before touching UI or prefabs.

## Main Layers

- `App.cs`: autoload, config warmup, KBEngine initialization, disconnect recovery.
- `Scripts/KBE/`: handwritten KBEngine integration and protocol-facing wrappers.
- `Scripts/KBE/Protocol/`: adapters around generated protocol values.
- `Prefab/`: world entity controllers and entity scenes.
- `UI/`: login UI and world HUD.
- `common/Data/`: JSON config tables.
- `common/DataTables/`: C# config repositories.

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

1. Client calls generated `cast_skill(skillId, targetId)` through handwritten `Player.CastSkill`.
2. Client waits for server `on_skill_cast`.
3. `Player` converts generated `SKILL_CAST` into `SkillCastResult`.
4. `PlayerController` plays the animation only after the server callback.
5. HP/MP changes are displayed from Combat component synchronization, not local prediction.

Skill config drives local test buttons, range checks, and local animation lock duration.

## Generated Protocol Update Workflow

1. Regenerate `kbe_csharp_plugins/` from the server.
2. Inspect generated method signatures and custom datatypes.
3. Update handwritten models/wrappers under `Scripts/KBE/`.
4. Update config repositories under `common/DataTables/` if JSON schemas changed.
5. Build with `dotnet build demo1.sln --no-restore`.

## Git Safety

The tree may contain user changes. Do not revert unrelated edits.
