# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Godot 4.4.1 mono (C#) client for KBEngine (branch `dev-2.6.x`). This is a multiplayer game client that connects to a KBEngine game server, handles entity lifecycle, and renders a 3D world with player/monster/npc entities.

## Build & Run

- **Editor**: `D:/Godot_v4.4.1-stable_mono_win64/Godot_v4.4.1-stable_mono_win64.exe` — open this project folder.
- **Build**: Use Godot editor's Build button (MSBuild), or `dotnet build demo1.sln` from the project root.
- **Render mode**: `gl_compatibility` (set in `project.godot`).
- **Entry point**: `App.tscn` → autoloads `App` → scene loads `Start.tscn` (`Scripts/start.gd`) which instantiates `MainUI.tscn`.

## Architecture: Strict Layer Separation

```
┌─────────────────────────────────────────────────┐
│ Presentation: Prefab/, UI/, World.cs             │
│ (Godot scenes, controllers, UI, .gd scripts)     │
├─────────────────────────────────────────────────┤
│ Handwritten KBE Integration: Scripts/KBE/        │
│   ├── Entity wrappers (Player, Monster, Npc)     │
│   ├── Protocol adapters (*KbeProtocolState*)      │
│   ├── Render bindings (*WorldEntityRenderBinding*)│
│   └── Contracts (*IWorldEntityView*, etc.)        │
├─────────────────────────────────────────────────┤
│ Generated SDK: kbe_csharp_plugins/               │
│ (DO NOT EDIT — regenerated from server protocol)  │
└─────────────────────────────────────────────────┘
```

**Critical rule**: Only the handwritten KBE integration layer (`Scripts/KBE/`) may reference `KBEngine` namespace types. Presentation code (`UI/`, `Prefab/`, `.gd` scripts) must only talk to handwritten wrappers like `App.Client`, `Player`, or methods exposed from `Scripts/KBE/`.

## Key Files

### Entry & Lifecycle
- `App.cs` — Autoload singleton. Initializes KBEngine connection, `KbeClient`, config warmup. Handles graceful shutdown and disconnect recovery (switches back to `Start.tscn`).
- `World.cs` — Scene root (`World.tscn`). Fires `OnWorldReady` event that entities listen to for deferred render binding. Manages world UI overlay.
- `Scripts/KBE/KbeClient.cs` — Thin wrapper around `KBEngineApp` events: `ConnectionStateChanged`, `LoginFailed`, `BaseappLoginStarted`, `LocalPlayerEnteredWorld`, `Disconnected`.

### Entity Model
Each entity type follows the same pattern:
1. Inherits from a generated `*Base` class (e.g., `PlayerBase`)
2. Implements `IWorldEntityView` + either `ILocallyControlledWorldEntity` (Player) or `IServerDrivenWorldEntity` (Monster, Npc)
3. Owns a `WorldEntityRenderBinding<TEntity, TController>` for render lifecycle
4. Owns a `Kbe*ProtocolState` that reads from the generated KBEngine entity fields
5. Responds to `onXxxChanged` callbacks to call `RefreshRenderInfo()`/`RefreshRenderTransform()`

### Render Binding System
`WorldEntityRenderBinding<TEntity, TController>` is the bridge between server entities and Godot scene nodes:
- On `__init__()` → subscribes to `World.OnWorldReady`
- On `onEnterWorld()` → creates or binds a scene node (if World scene isn't loaded yet, defers until `OnWorldReady`)
- Uses `WorldEntitySceneRegistry` to map entity types to `.tscn` paths
- On `onLeaveWorld()` → `QueueFree()` the node
- On `onDestroy()` → unsubscribe and cleanup

### Controller Hierarchy
- `WorldEntityControllerBase<TEntity>` (abstract, extends `Node3D`) — Common: interpolation, snap-on-initial-transform, head info labels, movement facing, animation state hooks.
- `PlayerController` extends it — adds camera binding, per-player animation runtime, model appearance loading from FBX files with runtime animation library assembly.
- `MonsterController` / `NpcController` — minimal extensions with specific node paths.

### Protocol Adapters
`Scripts/KBE/Protocol/` isolates generated KBEngine types:
- `KbeVector2Value`, `KbeVector3Value`, `KbeVector4Value` — immutable wrappers with `FromProtocol()`/`ToProtocol()`/`FromGodot()`/`ToGodot()`.
- `KbeEntityProtocolState<TEntity>` — base for reading position, direction from the KBEngine `Entity`.
- `KbePlayerProtocolState`, `KbeMonsterProtocolState`, `KbeNpcProtocolState` — typed protocol state readers.

### Rotation Mapping
`WorldEntityRotationMapping` handles the KBEngine ↔ Godot coordinate conversion with a 180° yaw offset: KBEngine direction vector (pitch, roll, yaw) is remapped to Godot Euler (pitch=X, yaw=Z, roll=Y with offset).

## Config & Data Tables
- `common/globalConfig.cs` — `GameConfig` (host, port, heartbeat tick).
- `common/clientSyncConfig.cs` — `ClientNetworkConfig` (sync interval), `ClientUiConfig`, `RemoteEntitySyncConfig`, `RemotePlayerSyncConfig`.
- `common/DataTables/` — JSON-driven config repositories with `Warmup()` pattern. `RoleConfigRepository`, `SexConfigRepository`, `PlayerAppearanceConfigRepository`. Each loads from `common/Data/*.json`, caches in a static field, and provides lookup methods.
- `common/DataTables/AvatarInitConfigTable.cs.uid` — exists without a `.cs` file (uid only), not yet implemented.

## Entity Components
- `Scripts/KBE/Components/Combat.cs` — `namespace KBEngine`, extends `CombatBase`. On `onHpChanged`/`onMpChanged`/`onEnterworld`, calls `RefreshRenderInfo()` on the owner if it implements `IWorldEntityRenderHooks`.
- `Scripts/KBE/Components/Motion.cs` — same pattern for `onMoveSpeedChanged`.

## UI Flow
`MainUi` (C#) handles login + character creation: account/password entry, role/sex selection, nickname generation, "remember login" persistence via `ConfigFile` → `user://login.cfg`. It waits for the local `Player` entity to enter world through `KbeClient.LocalPlayerEnteredWorld`, with a short polling fallback, then transitions to `World.tscn`.

## Client Flow Summary

```
autoload App → MainUi (login/character create) → local Player entity created by server
  → KbeClient emits LocalPlayerEnteredWorld → MainUi changes scene to World.tscn
  → World._Ready fires OnWorldReady → all waiting entities bind their render nodes
  → Server-created entities (Monster, Npc) spawn directly, no login dependency
```

## Protocol Update Workflow
When the server protocol changes:
1. Regenerate `kbe_csharp_plugins/` via `kbcmd --clientsdk=csharp --outpath=<path>`
2. Review diffs in generated `*Base.cs` and `EntityDef.cs`
3. Update adapters in `Scripts/KBE/` and `Scripts/KBE/Protocol/`
4. Update UI/Prefab only through wrapper APIs
5. Build and test manually

## GDScript Notes
- `Scripts/start.gd` — Instantiates `MainUI.tscn` as child of Start scene.
- `Scripts/camera_pivot.gd` — Third-person camera with mouse rotation, scroll zoom, auto focus-point from mesh AABB bounds.
- `Scripts/player_character_body_3d.gd` — WASD movement, jump, camera-relative direction. Reads `moveSpeed` and `status` from parent `PlayerController` node.

## Runtime State Reset
`ClientRuntimeState.ResetForSceneTransition()` clears all static state (singletons, event handlers) — called on disconnect recovery and graceful shutdown. Every class with static state implements `ResetStaticState()`.
