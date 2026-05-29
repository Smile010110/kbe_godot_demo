using Godot;
using KBEngine;

public class Monster : MonsterBase, IServerDrivenWorldEntity, IWorldEntityRenderHooks
{
	private readonly WorldEntityRenderBinding<Monster, MonsterController> _renderBinding;
	private readonly KbeMonsterProtocolState _protocolState;

	public Monster()
	{
		_protocolState = new KbeMonsterProtocolState(this);
		_renderBinding = new WorldEntityRenderBinding<Monster, MonsterController>(this, this);
	}

	public KbeMonsterProtocolState Protocol => _protocolState;
	public bool IsLocallyControlled => false;
	public int EntityId => _protocolState.EntityId;
	public ulong DatabaseId => _protocolState.DatabaseId;
	public WorldEntityKind EntityKind => WorldEntityKind.Monster;
	public bool IsTeammate => false;
	public string DisplayName => _protocolState.DisplayName;
	public string SecondaryInfoText => WorldEntityNameplateText.BuildCombatMotionLine(HitPoints, ManaPoints, Attack, Defense, RawMoveSpeed);
	public bool ShowSecondaryInfo => true;
	public ulong HitPoints => _protocolState.Combat.HitPoints;
	public ulong ManaPoints => _protocolState.Combat.ManaPoints;
	public uint Attack => _protocolState.Combat.Attack;
	public uint Defense => _protocolState.Combat.Defense;
	public byte RawMoveSpeed => _protocolState.Motion.RawMoveSpeed;
	public float MoveSpeedUnits => _protocolState.Motion.MoveSpeedUnits;
	public Vector3 WorldPosition => _protocolState.WorldPosition;
	public Vector3 WorldRotationDegrees => _protocolState.WorldRotationDegrees;
	public bool UsePlanarRotation => true;

	public override void __init__()
	{
		base.__init__();
		_renderBinding.Initialize();
	}

	public override void onEnterWorld()
	{
		base.onEnterWorld();
		_renderBinding.EnterWorld();
	}

	public override void onLeaveWorld()
	{
		base.onLeaveWorld();
		_renderBinding.Cleanup();
	}

	public override void onDestroy()
	{
		_renderBinding.Destroy();
		base.onDestroy();
	}

	public override void onDbidChanged(ulong oldValue)
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
		RefreshRenderTransform();
	}

	public override void onSmoothPositionChanged(KBVector3 oldValue)
	{
		base.onSmoothPositionChanged(oldValue);
		RefreshRenderTransform();
	}

	public override void onDirectionChanged(KBVector3 oldValue)
	{
		base.onDirectionChanged(oldValue);
		RefreshRenderTransform();
	}

	public void RefreshRenderInfo()
	{
		_renderBinding.RefreshInfo();
	}

	public void RefreshRenderTransform()
	{
		_renderBinding.RefreshTransform();
	}
}
