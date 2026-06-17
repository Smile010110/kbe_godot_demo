# CLAUDE.md

This file provides guidance to Claude Code and other coding assistants when working in this repository.

## Project Overview

Godot `4.4.1 mono` C# client for KBEngine. The client connects to a KBEngine server, handles login and entity lifecycle, renders Player/Monster/Npc world entities, and presents server-synchronized combat and buff state.

Build from the repository root:

```powershell
dotnet build demo1.sln --no-restore
```

Generated SDK warnings under `kbe_csharp_plugins/` are expected.

## Strict Layer Separation

Generated SDK:

- `kbe_csharp_plugins/`
- Do not edit by hand.
- Regenerate from the server protocol when protocol changes.

Handwritten KBEngine integration:

- `App.cs`
- `Scripts/KBE/`
- `Scripts/KBE/Protocol/`
- `Scripts/KBE/Components/`

Presentation and gameplay-facing code:

- `Prefab/`
- `UI/`
- `World.cs`
- `Scripts/`
- `common/`

Rule: presentation code should not depend directly on generated KBEngine internals. Prefer contracts and wrappers exposed from `Scripts/KBE/`.

## Entry And Lifecycle

Flow:

```text
App autoload
  -> Start.tscn
  -> MainUi
  -> local Player enters world
  -> World.tscn
  -> World.OnWorldReady
  -> entity render nodes bind through WorldEntityRenderBinding
```

Key files:

- `App.cs`: config warmup, KBEngine startup, client facade binding, shutdown, disconnect recovery.
- `World.cs`: world scene root and `OnWorldReady` event.
- `UI/MainUi.cs`: login and character creation.
- `UI/WorldUi.cs`: world HUD, target panel, skill buttons.
- `Scripts/KBE/KbeClient.cs`: thin client event facade around KBEngine events.

## Entity Model

Each entity type inherits a generated base and exposes handwritten contracts:

- `Player : PlayerBase, ILocallyControlledWorldEntity, IWorldEntityRenderHooks`
- `Monster : MonsterBase, IServerDrivenWorldEntity, IWorldEntityRenderHooks`
- `Npc : NpcBase, IServerDrivenWorldEntity, IWorldEntityRenderHooks`

Shared contracts:

- `IWorldEntityView`
- `ILocallyControlledWorldEntity`
- `IServerDrivenWorldEntity`
- `IWorldEntityRenderHooks`
- `ISelectableWorldEntityController`
- `ISkillCastPresentationController`

Render binding:

- `WorldEntityRenderBinding<TEntity, TController>` creates or binds the Godot presentation node.
- `WorldEntityControllerBase<TEntity>` handles common transform interpolation, head info, health bar, selection-facing state, and presentation hooks.
- `PlayerController` adds local movement, camera, animation runtime, selection, skill UI runtime, cooldowns, and floating text handling.

## Protocol Adapters

Generated protocol values should be converted under `Scripts/KBE/Protocol/`:

- `KbeProtocolVectors.cs`: KBEngine vector <-> Godot vector mapping.
- `KbeProtocolState.cs`: typed entity state readers.
- `KbeProtocolBuffs.cs`: `BUFF_LIST` / `BUFF_INFO` conversion.

Do not pass generated custom datatypes deep into UI or prefab code.

## Config Tables

Current JSON tables in `common/Data/`:

- `d_attr.json`
- `d_buff.json`
- `d_role.json`
- `d_sex.json`
- `d_skill.json`

Repositories in `common/DataTables/`:

- `AttrConfigRepository`
- `BuffConfigRepository`
- `RoleConfigRepository`
- `SexConfigRepository`
- `SkillConfigRepository`
- `PlayerAppearanceConfigRepository`

Repositories use a `Warmup()` pattern and are called from `App._Ready()`. If a schema changes, update the matching repository directly to the latest schema; do not add compatibility for old uploaded tables unless asked.

`player_model_profiles.json` is no longer required in the current data set. `PlayerAppearanceConfigRepository` silently falls back to its built-in default profile when the file is absent.

## Skills

The client has skill UI and presentation scaffolding, but skill requests are gated by current generated protocol availability.

Current rules:

- `Player.CanCastSkills` controls whether the UI can request skills.
- `Player.TryCastSkill` must not fake local success when the generated protocol lacks a usable skill cell call.
- Local cooldown starts only after the request is sent successfully.
- `cast_without_target != 0` skills may send target id `0`.
- Invalid target, out-of-range, cooldown, or MP failures should show local messages without clearing target selection.
- Skill animation currently falls back to action state because the current `d_skill.json` schema has no `animation_key`.

Current skill table fields include:

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

## Buffs

The server syncs buffs through entity-level `buff_list`.

Generated datatypes:

- `BUFF_INFO`: `buff_key`, `buff_id`, `level`, `duration`, `remain_time`, `stack`
- `BUFF_LIST`: `values`

Client behavior:

- `Player` and `Monster` refresh buff state in `onBuff_listChanged`.
- `KbeBuffInfo` keeps protocol duration/remain values in milliseconds.
- UI summaries display remaining time in rounded-up seconds.
- Buff display names come from `BuffConfigRepository`.
- Attribute display names come from `AttrConfigRepository`.
- Head labels show buff count; world HUD and target info show compact summaries.

## Runtime State Reset

`ClientRuntimeState.ResetForSceneTransition()` clears static state during disconnect recovery and graceful shutdown. Classes with static singleton/event state should participate in this reset path.

## Update Workflow

When protocol or data changes:

1. Inspect local generated signatures and current JSON schema.
2. Update handwritten adapters under `Scripts/KBE/`.
3. Update repositories under `common/DataTables/`.
4. Update UI/prefabs only through handwritten contracts.
5. Build with `dotnet build demo1.sln --no-restore`.
6. Confirm generated SDK was not hand-edited.
