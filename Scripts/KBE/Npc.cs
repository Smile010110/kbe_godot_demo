using System;
using System.Collections.Generic;
using Godot;
using KBEngine;

public class Npc : NpcBase, IServerDrivenWorldEntity, IWorldEntityRenderHooks
{
	private readonly WorldEntityRenderBinding<Npc, NpcController> _renderBinding;
	private readonly KbeNpcProtocolState _protocolState;

	public Npc()
	{
		_protocolState = new KbeNpcProtocolState(this);
		_renderBinding = new WorldEntityRenderBinding<Npc, NpcController>(this, this);
	}

	public KbeNpcProtocolState Protocol => _protocolState;
	public bool IsLocallyControlled => false;
	public int EntityId => _protocolState.EntityId;
	public ulong DatabaseId => _protocolState.DatabaseId;
	public WorldEntityKind EntityKind => WorldEntityKind.Npc;
	public bool IsTeammate => false;
	public string DisplayName => _protocolState.DisplayName;
	public string SecondaryInfoText => RawMoveSpeed > 0 ? WorldEntityNameplateText.BuildSpeedOnlyLine(RawMoveSpeed) : string.Empty;
	public bool ShowSecondaryInfo => !string.IsNullOrWhiteSpace(SecondaryInfoText);
	public ulong HitPoints => 0UL;
	public ulong MaxHitPoints => 0UL;
	public ulong ManaPoints => 0UL;
	public IReadOnlyList<KbeBuffInfo> Buffs => Array.Empty<KbeBuffInfo>();
	public int ActiveBuffCount => 0;
	public string BuffSummaryText => string.Empty;
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
