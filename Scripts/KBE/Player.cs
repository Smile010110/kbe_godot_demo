using System;
using Godot;
using KBEngine;

public class Player : PlayerBase, ILocallyControlledWorldEntity, IWorldEntityRenderHooks
{
	public static event Action OnLocalPlayerEnterWorldRequested;
	public static Player LocalPlayer { get; private set; }

	private const float PositionSyncEpsilonSquared = 0.0001f;
	private const float RotationSyncEpsilonDegrees = 0.1f;

	private readonly WorldEntityRenderBinding<Player, PlayerController> _renderBinding;
	private Vector3 _lastSyncedWorldPosition;
	private Vector3 _lastSyncedWorldRotationDegrees;
	private bool _hasLastSyncedTransform;

	public Player()
	{
		_renderBinding = new WorldEntityRenderBinding<Player, PlayerController>(this, this);
	}

	public bool IsLocalPlayer => isPlayer();
	public bool IsLocallyControlled => IsLocalPlayer;
	public int EntityId => id;
	public ulong DatabaseId => dbid;
	public ushort ServerId => server_id;
	public byte SpaceLine => space_line;
	public uint SpaceUtype => space_utype;
	public WorldEntityKind EntityKind => WorldEntityKind.Player;
	public bool IsTeammate => false;
	public string DisplayName => string.IsNullOrWhiteSpace(name) ? $"Player {id}" : name;
	public string SecondaryInfoText => WorldEntityNameplateText.BuildCombatMotionLine(HitPoints, ManaPoints, RawMoveSpeed);
	public bool ShowSecondaryInfo => true;
	public ulong HitPoints => combat != null ? combat.hp : 0UL;
	public ulong ManaPoints => combat != null ? combat.mp : 0UL;
	public byte RawMoveSpeed => motion != null ? motion.moveSpeed : (byte)0;
	public float MoveSpeedUnits => Mathf.Max(0.1f, RawMoveSpeed / 10.0f);
	public Vector3 WorldPosition => new Vector3(position.x, position.y, position.z);
	public Vector3 WorldRotationDegrees => WorldEntityRotationMapping.ToGodotRotationDegrees(direction);
	public bool UsePlanarRotation => true;

	public override void __init__()
	{
		base.__init__();
		World.OnWorldReady -= OnWorldReady;
		World.OnWorldReady += OnWorldReady;
	}

	public override void onEnterWorld()
	{
		base.onEnterWorld();

		if (IsLocalPlayer)
		{
			LocalPlayer = this;
		}

		if (World.Instance == null)
		{
			_renderBinding.WaitForWorld();

			if (IsLocalPlayer)
			{
				OnLocalPlayerEnterWorldRequested?.Invoke();
			}

			return;
		}

		_renderBinding.CreateOrBindRenderObject();
	}

	public override void onLeaveWorld()
	{
		base.onLeaveWorld();
		_renderBinding.Cleanup();

		if (ReferenceEquals(LocalPlayer, this))
		{
			LocalPlayer = null;
		}
	}

	public override void onDestroy()
	{
		World.OnWorldReady -= OnWorldReady;
		_renderBinding.Cleanup();
		base.onDestroy();
	}

	public override void onDbidChanged(ulong oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onServer_idChanged(ushort oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onSpace_utypeChanged(uint oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onSpace_lineChanged(byte oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onNameChanged(string oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onPositionChanged(KBVector3 oldValue)
	{
		base.onPositionChanged(oldValue);

		if (!IsLocalPlayer)
		{
			RefreshRenderTransform();
		}
	}

	public override void onDirectionChanged(KBVector3 oldValue)
	{
		base.onDirectionChanged(oldValue);

		if (!IsLocalPlayer)
		{
			RefreshRenderTransform();
		}
	}

	public override void onSmoothPositionChanged(KBVector3 oldValue)
	{
		base.onSmoothPositionChanged(oldValue);

		if (!IsLocalPlayer)
		{
			RefreshRenderTransform();
		}
	}

	public void ApplyLocalTransform(Vector3 worldPosition, Vector3 worldRotationDegrees)
	{
		if (!HasLocalTransformChanged(worldPosition, worldRotationDegrees))
		{
			return;
		}

		position = worldPosition;
		direction = WorldEntityRotationMapping.ToKbeDirection(worldRotationDegrees);

		_lastSyncedWorldPosition = worldPosition;
		_lastSyncedWorldRotationDegrees = worldRotationDegrees;
		_hasLastSyncedTransform = true;
	}

	public void RefreshRenderInfo()
	{
		_renderBinding.RefreshInfo();
	}

	public void RefreshRenderTransform()
	{
		_renderBinding.RefreshTransform();
	}

	private void OnWorldReady()
	{
		_renderBinding.HandleWorldReady();
	}

	private bool HasLocalTransformChanged(Vector3 worldPosition, Vector3 worldRotationDegrees)
	{
		if (!_hasLastSyncedTransform)
		{
			return true;
		}

		if (_lastSyncedWorldPosition.DistanceSquaredTo(worldPosition) > PositionSyncEpsilonSquared)
		{
			return true;
		}

		return HasRotationChanged(_lastSyncedWorldRotationDegrees, worldRotationDegrees);
	}

	private static bool HasRotationChanged(Vector3 previousRotation, Vector3 currentRotation)
	{
		return Mathf.Abs(Mathf.AngleDifference(previousRotation.X, currentRotation.X)) > RotationSyncEpsilonDegrees
			|| Mathf.Abs(Mathf.AngleDifference(previousRotation.Y, currentRotation.Y)) > RotationSyncEpsilonDegrees
			|| Mathf.Abs(Mathf.AngleDifference(previousRotation.Z, currentRotation.Z)) > RotationSyncEpsilonDegrees;
	}
}
