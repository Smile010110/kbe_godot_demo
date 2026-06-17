# Godot KBEngine Demo

Godot version: `4.4.1 mono`  
KBEngine client SDK: generated C# SDK under `kbe_csharp_plugins/`

This repository is a Godot C# client demo wired to KBEngine. It handles login, entity lifecycle, server-driven world entities, local movement presentation, target selection, skill UI scaffolding, and server-synchronized combat/buff display.

## Build

From the repository root:

```powershell
dotnet build demo1.sln --no-restore
```

Warnings from generated SDK files under `kbe_csharp_plugins/` are expected.

## Source Of Truth

`kbe_csharp_plugins/` is generated from the server protocol.

Rules:

- Do not write business logic into `kbe_csharp_plugins/`.
- Do not patch generated `*Base.cs`, `EntityDef.cs`, or engine helper files by hand.
- When the server protocol changes, regenerate `kbe_csharp_plugins/` first, then adapt the handwritten layer.

Server reference path used during client adaptation:

```text
D:\UGit\KBEngine\base_assets
```

Treat the server reference as read-only from this workspace.

## Layer Boundary

Generated layer:

- `kbe_csharp_plugins/`

Handwritten KBEngine integration layer:

- `App.cs`
- `Scripts/KBE/`
- `Scripts/KBE/Protocol/`
- `Scripts/KBE/Components/`

Presentation and game-facing layer:

- `Prefab/`
- `UI/`
- `World.cs`
- `Scripts/`
- `common/`

Client data tables:

- `common/Data/`
- `common/DataTables/`

Only the handwritten KBEngine integration layer should reference generated KBEngine details such as `KBEngineApp`, generated entity base classes, generated component base classes, and generated protocol structs. UI and prefab code should talk through handwritten contracts and wrappers.

## Current Client Flow

```text
autoload App
  -> Start.tscn
  -> MainUi login / character creation
  -> local Player entity enters world
  -> MainUi switches to World.tscn
  -> World.OnWorldReady binds waiting render nodes
  -> server-created Player / Monster / Npc entities render through Prefab controllers
```

`App.cs` owns:

- config warmup
- KBEngine startup
- `KbeClient` event binding
- graceful shutdown
- disconnect recovery back to `Start.tscn`

`WorldEntityRenderBinding<TEntity, TController>` owns the bridge from server entity lifecycle to Godot scene nodes.

## Entity Model

Current world entities:

- `Player`: local or remote player entity. Reads combat, motion, role, sex, server time, and buff state from generated fields.
- `Monster`: server-driven hostile entity. Reads combat, motion, and buff state.
- `Npc`: server-driven non-combat entity. Reads motion state.

Shared contracts live in `Scripts/KBE/WorldEntityContracts.cs`:

- `IWorldEntityView`
- `ILocallyControlledWorldEntity`
- `IServerDrivenWorldEntity`
- `IWorldEntityRenderHooks`
- `ISelectableWorldEntityController`
- `ISkillCastPresentationController`

Protocol adapters live under `Scripts/KBE/Protocol/`:

- vectors: `KbeProtocolVectors.cs`
- entity state: `KbeProtocolState.cs`
- buff datatypes: `KbeProtocolBuffs.cs`

## Config Tables

Current JSON tables:

- `common/Data/d_attr.json`
- `common/Data/d_buff.json`
- `common/Data/d_role.json`
- `common/Data/d_sex.json`
- `common/Data/d_skill.json`

Current repositories:

- `AttrConfigRepository`
- `BuffConfigRepository`
- `RoleConfigRepository`
- `SexConfigRepository`
- `SkillConfigRepository`
- `PlayerAppearanceConfigRepository`

`PlayerAppearanceConfigRepository` now uses a built-in fallback when `player_model_profiles.json` is absent.

## Skills

The client has skill UI and presentation scaffolding:

- dynamic skill buttons from `d_skill.json`
- local cooldown and global cooldown display
- MP, range, target, and pending-cast checks
- support for `cast_without_target`
- AOE fields loaded for display and future behavior
- floating text dispatch for server results

Current protocol caveat:

- Skill requests are gated by `Player.CanCastSkills`.
- If the generated `PlayerBase` does not expose a usable skill cell call, skill buttons remain disabled and no fake local request is sent.

Current `d_skill.json` fields include:

- `cast_without_target`
- `aoe_type`
- `aoe_radius`
- `aoe_angle`
- `aoe_width`
- `aoe_length`

The latest uploaded skill table does not include `animation_key`, so skill animation currently falls back to action state, such as attack or cast.

## Buffs

The server syncs buffs through entity-level `buff_list` on `Player` and `Monster`.

Generated protocol:

- `BUFF_INFO`: `buff_key`, `buff_id`, `level`, `duration`, `remain_time`, `stack`
- `BUFF_LIST`: `values`

Client behavior:

- `KbeProtocolBuffs.cs` converts generated values into `KbeBuffState`.
- Protocol `duration` and `remain_time` are milliseconds.
- UI summaries display remaining time in rounded-up seconds.
- Buff names come from `d_buff.json`.
- Buff attribute names come from `d_attr.json`.
- Head info shows buff count; HUD and target panels show compact summaries.

## Protocol Update Workflow

1. Regenerate `kbe_csharp_plugins/` from the server.
2. Inspect generated `*Base.cs`, `EntityDef.cs`, component lists, method signatures, and custom datatypes.
3. Update adapters and wrappers in `Scripts/KBE/`.
4. Update config repositories in `common/DataTables/` if JSON schema changed.
5. Update UI/prefabs only through handwritten wrapper APIs.
6. Build with `dotnet build demo1.sln --no-restore`.

## Notes

- The repository may have a dirty worktree during development. Do not discard unrelated changes.
- Generated SDK warnings are expected; handwritten warnings should be treated as issues.
- `common/DataTables/*.cs.uid` files may be generated by Godot for new scripts when the editor imports them.
