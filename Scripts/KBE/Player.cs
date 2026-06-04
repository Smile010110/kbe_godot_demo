using System;
using Godot;
using KBEngine;
using CommonData;

public class Player : PlayerBase, ILocallyControlledWorldEntity, IWorldEntityRenderHooks
{
	public static Player LocalPlayer { get; private set; }

	private const float PositionSyncEpsilonSquared = 0.0001f;
	private const float RotationSyncEpsilonDegrees = 0.1f;
	private const ulong MillisecondsThreshold = 1_000_000_000_000UL;

	private readonly WorldEntityRenderBinding<Player, PlayerController> _renderBinding;
	private readonly KbePlayerProtocolState _protocolState;
	private Vector3 _lastSyncedWorldPosition;
	private Vector3 _lastSyncedWorldRotationDegrees;
	private bool _hasLastSyncedTransform;
	private ulong _serverTimeAnchorValue;
	private long _serverTimeAnchorClientTickMs;
	private bool _serverTimeUsesMilliseconds;
	private bool _hasServerTimeAnchor;

	public Player()
	{
		_protocolState = new KbePlayerProtocolState(this);
		_renderBinding = new WorldEntityRenderBinding<Player, PlayerController>(this, this);
	}

	public static void ResetStaticState()
	{
		LocalPlayer = null;
	}

	public KbePlayerProtocolState Protocol => _protocolState;
	public bool IsLocalPlayer => _protocolState.IsLocalPlayer;
	public bool IsLocallyControlled => IsLocalPlayer;
	public int EntityId => _protocolState.EntityId;
	public ulong DatabaseId => _protocolState.DatabaseId;
	public ushort ServerId => _protocolState.ServerId;
	public ulong ServerTime => GetCurrentServerTime();
	public string ServerTimeText => FormatServerTime(ServerTime);
	public ushort Level => _protocolState.Level;
	public byte Role => _protocolState.Role;
	public byte Sex => _protocolState.Sex;
	public uint Exp => _protocolState.Exp;
	public string RoleName => RoleConfigRepository.ResolveDisplayName(Role);
	public uint AppearanceModelId => ResolveAppearanceModelId();
	public byte SpaceLine => _protocolState.SpaceLine;
	public uint SpaceUtype => _protocolState.SpaceUtype;
	public WorldEntityKind EntityKind => WorldEntityKind.Player;
	public bool IsTeammate => false;
	public string DisplayName => _protocolState.DisplayName;
	public string SecondaryInfoText => WorldEntityNameplateText.BuildPlayerLine(HitPoints, ManaPoints, Attack, Defense, RawMoveSpeed, Exp);
	public bool ShowSecondaryInfo => true;
	public ulong HitPoints => _protocolState.Combat.HitPoints;
	public ulong ManaPoints => _protocolState.Combat.ManaPoints;
	public uint Attack => _protocolState.Combat.Attack;
	public uint Defense => _protocolState.Combat.Defense;
	public byte RawMoveSpeed => _protocolState.Motion.RawMoveSpeed;

	public event Action<SkillCastResult> SkillResultReceived;
	public event Action<SkillCastError> SkillErrorReceived;

	public bool TryCastSkill(int skillId, ulong targetEntityId, string extData = "")
	{
		if (skillId < 0)
		{
			GD.PushWarning($"Invalid skill id: {skillId}");
			return false;
		}

		if (cellEntityCall?.skill == null)
		{
			GD.PushWarning($"Cannot cast skill without SkillComponent cell call. skill={skillId}");
			return false;
		}

		cellEntityCall.skill.cast_skill((uint)skillId, targetEntityId, extData ?? string.Empty);
		return true;
	}

	public float MoveSpeedUnits => _protocolState.Motion.MoveSpeedUnits;
	public Vector3 WorldPosition => _protocolState.WorldPosition;
	public Vector3 WorldRotationDegrees => _protocolState.WorldRotationDegrees;
	public bool UsePlanarRotation => true;

	public override void __init__()
	{
		base.__init__();
		SyncServerTimeAnchor(_protocolState.RawServerTime);
		_renderBinding.Initialize();
	}

	public override void onEnterWorld()
	{
		base.onEnterWorld();

		if (IsLocalPlayer)
		{
			LocalPlayer = this;
		}

		_renderBinding.EnterWorld();
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
		if (ReferenceEquals(LocalPlayer, this))
		{
			LocalPlayer = null;
		}

		_renderBinding.Destroy();
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

	public override void onServer_timeChanged(ulong oldValue)
	{
		SyncServerTimeAnchor(_protocolState.RawServerTime);
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

	public override void onLevelChanged(ushort oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onRoleChanged(byte oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onSexChanged(byte oldValue)
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

	public void HandleSkillResult(SkillCastResult skillCast)
	{
		SkillResultReceived?.Invoke(skillCast);
		RefreshRenderInfo();
	}

	public void HandleSkillError(SkillCastError error)
	{
		SkillErrorReceived?.Invoke(error);
		RefreshRenderInfo();
	}

	public void ApplyLocalTransform(Vector3 worldPosition, Vector3 worldRotationDegrees)
	{
		if (!HasLocalTransformChanged(worldPosition, worldRotationDegrees))
		{
			return;
		}

		_protocolState.SetWorldTransform(worldPosition, worldRotationDegrees);

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

	private uint ResolveAppearanceModelId()
	{
		if (SexConfigRepository.TryGetBySex(Sex, out var sexEntry))
		{
			return sexEntry.ModelId;
		}

		if (IsLocalPlayer && CharacterCreationState.Current.ModelId != 0U)
		{
			return CharacterCreationState.Current.ModelId;
		}

		return PlayerAppearanceConfigRepository.DefaultModelId;
	}

	private ulong GetCurrentServerTime()
	{
		SyncServerTimeAnchor(_protocolState.RawServerTime);
		if (!_hasServerTimeAnchor)
		{
			return 0UL;
		}

		var elapsedClientMs = Math.Max(0L, System.Environment.TickCount64 - _serverTimeAnchorClientTickMs);
		var elapsedSeconds = (ulong)(elapsedClientMs / 1000L);
		if (elapsedSeconds == 0UL)
		{
			return _serverTimeAnchorValue;
		}

		var tickStep = _serverTimeUsesMilliseconds ? 1000UL : 1UL;
		return _serverTimeAnchorValue + elapsedSeconds * tickStep;
	}

	private void SyncServerTimeAnchor(ulong rawServerTime)
	{
		if (rawServerTime == 0UL)
		{
			return;
		}

		if (_hasServerTimeAnchor && _serverTimeAnchorValue == rawServerTime)
		{
			return;
		}

		_serverTimeAnchorValue = rawServerTime;
		_serverTimeAnchorClientTickMs = System.Environment.TickCount64;
		_serverTimeUsesMilliseconds = rawServerTime >= MillisecondsThreshold;
		_hasServerTimeAnchor = true;
	}

	private static string FormatServerTime(ulong rawServerTime)
	{
		if (rawServerTime == 0)
		{
			return "-";
		}

		if (rawServerTime > long.MaxValue)
		{
			return rawServerTime.ToString();
		}

		try
		{
			var serverTime = rawServerTime >= MillisecondsThreshold
				? DateTimeOffset.FromUnixTimeMilliseconds((long)rawServerTime)
				: DateTimeOffset.FromUnixTimeSeconds((long)rawServerTime);
			return serverTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
		}
		catch (ArgumentOutOfRangeException)
		{
			return rawServerTime.ToString();
		}
	}
}
