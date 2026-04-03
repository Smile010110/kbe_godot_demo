using Godot;
using KBEngine;

public class Monster : MonsterBase, IServerDrivenWorldEntity, IWorldEntityRenderHooks
{
	private readonly WorldEntityRenderBinding<Monster, MonsterController> _renderBinding;

	public Monster()
	{
		_renderBinding = new WorldEntityRenderBinding<Monster, MonsterController>(this, this, "res://Prefab/Monster.tscn");
	}

	public bool IsLocallyControlled => false;
	public int EntityId => id;
	public ulong DatabaseId => dbid;
	public string DisplayName => string.IsNullOrWhiteSpace(name) ? $"Monster {id}" : name;
	public ulong HitPoints => combat != null ? combat.hp : 0UL;
	public ulong ManaPoints => combat != null ? combat.mp : 0UL;
	public byte RawMoveSpeed => motion != null ? motion.moveSpeed : (byte)0;
	public float MoveSpeedUnits => Mathf.Max(0.1f, RawMoveSpeed / 10.0f);
	public Vector3 WorldPosition => new Vector3(position.x, position.y, position.z);
	public Vector3 WorldRotationDegrees => new Vector3(direction.x, direction.y - 180.0f, direction.z);
	public NameplateStyle NameplateStyle => NameplateStyle.Monster;
	public Color NameplateColor => NameplatePalette.Resolve(NameplateStyle);

	public override void __init__()
	{
		base.__init__();
		World.OnWorldReady -= OnWorldReady;
		World.OnWorldReady += OnWorldReady;
	}

	public override void onEnterWorld()
	{
		base.onEnterWorld();

		if (World.Instance == null)
		{
			_renderBinding.WaitForWorld();
			return;
		}

		_renderBinding.CreateOrBindRenderObject();
	}

	public override void onLeaveWorld()
	{
		base.onLeaveWorld();
		_renderBinding.Cleanup();
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

	public override void onNameChanged(string oldValue)
	{
		RefreshRenderInfo();
	}

	public override void onPositionChanged(KBVector3 oldValue)
	{
		base.onPositionChanged(oldValue);
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

	private void OnWorldReady()
	{
		_renderBinding.HandleWorldReady();
	}
}
