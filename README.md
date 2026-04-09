# Godot KBEngine Demo

Godot version: `4.4.1 mono`  
KBEngine branch used by this client: `dev-2.6.x`

This repository is a Godot client demo wired to a KBEngine-generated C# SDK.

## Source Of Truth

`kbe_csharp_plugins/` is generated from the server protocol.

Rules:

- Do not write business logic into `kbe_csharp_plugins/`.
- Do not patch generated `*Base.cs`, `EntityDef.cs`, or engine helper files by hand.
- When the server protocol changes, regenerate `kbe_csharp_plugins/` first, then adapt the handwritten layer.

## Layer Boundary

Generated layer:

- `kbe_csharp_plugins/`

Handwritten KBEngine integration layer:

- [App.cs](/d:/UGit/kbe_godot_demo/App.cs)
- `Scripts/KBE/`

Core handwritten world-entity primitives:

- `IWorldEntityView`
- `ILocallyControlledWorldEntity`
- `IServerDrivenWorldEntity`
- `IWorldEntityRenderHooks`
- `WorldEntityKind`
- `WorldEntityNameplateStyleResolver`
- `WorldEntitySceneRegistry`
- `WorldEntityRenderBinding<TEntity, TController>`
- `WorldEntityControllerBase<TEntity>`

`WorldEntityRenderBinding<TEntity, TController>` owns the handwritten world-facing entity lifecycle:

- subscribe to `World.OnWorldReady`
- wait for world bootstrap when scenes switch
- create or bind the presentation node
- clean up render nodes on leave/destroy

Presentation and game-facing layer:

- `UI/`
- `Prefab/`
- [World.cs](/d:/UGit/kbe_godot_demo/World.cs)
- `Scripts/*.gd`
- `common/`

Client data tables:

- `common/Data/`
- `common/DataTables/`

Runtime tuning that should stay in the handwritten layer:

- `GameConfig`
- `ClientNetworkConfig`
- `RemoteEntitySyncConfig`
- `RemotePlayerSyncConfig`

Only the handwritten KBEngine integration layer should reference:

- `using KBEngine`
- `KBEngineApp`
- `KBEngine.Event`
- generated entity/component base classes such as `PlayerBase`, `CombatBase`, `MotionBase`
- generated protocol structs such as `KBVector3`

`UI/`, `Prefab/`, and other presentation code should only talk to handwritten wrappers such as:

- `App.Client`
- `Player`
- handwritten adapter methods and properties exposed from `Scripts/KBE/`

## Current Client Flow

`autoload App -> MainUi -> local Player -> World`

Only the local controlled `Player` is allowed to trigger world bootstrap.
Server-created entities such as `Monster` are spawned directly from world sync and do not participate in login flow.
`Npc` follows the same server-driven path.

Current generated entities/modules in use:

- `Player`
- `Monster`
- `Npc`
- `GameMgr`
- `Server`
- `Space`
- `WebServer`

## Protocol Update Workflow

1. Regenerate the client SDK into `kbe_csharp_plugins/`.
2. Review generated changes in `PlayerBase`, component bases, and entity defs.
3. Update handwritten adapters in `Scripts/KBE/`.
4. Update UI or prefabs only through handwritten wrapper APIs.
5. Build the main project and verify the client manually.

## SDK Generation

Example:

```bat
start "" "%KBE_BIN_PATH%/kbcmd.exe" --clientsdk=csharp --outpath="%~dp0/kbe_csharp_plugins"
```

After generation, replace the local `kbe_csharp_plugins/` directory contents with the newly generated files.
