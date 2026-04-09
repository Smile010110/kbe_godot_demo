using Godot;
using KBEngine;

public class Npc : NpcBase, IServerDrivenWorldEntity, IWorldEntityRenderHooks
{
	private readonly WorldEntityRenderBinding<Npc, NpcController> _renderBinding;

	public Npc()
	{
		_renderBinding = new WorldEntityRenderBinding<Npc, NpcController>(this, this);
	}

	public bool IsLocallyControlled => false;
	public int EntityId => id;
	public ulong DatabaseId => dbid;
	public WorldEntityKind EntityKind => WorldEntityKind.Npc;
	public bool IsTeammate => false;
	public string DisplayName => string.IsNullOrWhiteSpace(name) ? $"Npc {id}" : name;
	public string SecondaryInfoText => RawMoveSpeed > 0 ? WorldEntityNameplateText.BuildSpeedOnlyLine(RawMoveSpeed) : string.Empty;
	public bool ShowSecondaryInfo => !string.IsNullOrWhiteSpace(SecondaryInfoText);
	public ulong HitPoints => 0UL;
	public ulong ManaPoints => 0UL;
	public byte RawMoveSpeed => motion != null ? motion.moveSpeed : (byte)0;
	public float MoveSpeedUnits => Mathf.Max(0.1f, RawMoveSpeed / 10.0f);
	public Vector3 WorldPosition => new Vector3(position.x, position.y, position.z);
	public Vector3 WorldRotationDegrees => WorldEntityRotationMapping.ToGodotRotationDegrees(direction);
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
