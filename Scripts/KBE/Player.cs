using System;
using Godot;
using KBEngine;

public class Player : PlayerBase
{
	public static Player Instance;

	// 定义场景切换事件
	public static event Action OnEnterWorldRequested;

	// 标记玩家是否等待创建
    private bool _waitingForWorld = false;
	
	public Player()
	{
		Instance = this;
	}

	public override void __init__()
	{
		base.__init__();

		KBELog.DEBUG_MSG("Player::__init__()");

        // 订阅世界就绪事件
        World.OnWorldReady += OnWorldReady;
	}

	public override void onServer_idChanged(UInt16 oldValue)
	{
		KBELog.DEBUG_MSG($"Player::onServer_idChanged() {oldValue}");
	}

	public override void onEnterWorld()
	{
		base.onEnterWorld();

		// 触发进入世界场景事件
		OnEnterWorldRequested?.Invoke();

		// 设置等待创建标记
		_waitingForWorld = true;
	}

	// 世界就绪时的回调
	private void OnWorldReady()
	{
		if (_waitingForWorld)
		{
			KBELog.DEBUG_MSG($"世界已就绪，开始创建玩家 {this.id}");
			CreatePlayerInWorld();
			_waitingForWorld = false;
		}
	}

	// 在世界中创建玩家
	private void CreatePlayerInWorld()
	{
		if (this.isPlayer())
		{
			KBELog.DEBUG_MSG($"创建玩家实体 {this.id}");
			var player = GD.Load<PackedScene>("res://Prefab/Player.tscn");
			PlayerController playerNode = player.Instantiate<PlayerController>();
			this.renderObj = playerNode;

			// 现在World.Instance肯定不为null
			World.Instance.AddChild(playerNode);
			playerNode.Player = this;
			playerNode.GlobalPosition = new Vector3(position.x, position.y, position.z);
			playerNode.SetHeadInfo();

			KBELog.DEBUG_MSG($"玩家 {this.id} 在世界中创建完成");
		}
		else
		{
			KBELog.DEBUG_MSG($"创建非玩家实体 {this.id}");
			var player = GD.Load<PackedScene>("res://Prefab/Player.tscn");
			PlayerController playerNode = player.Instantiate<PlayerController>();
			this.renderObj = playerNode;
			World.Instance.AddChild(playerNode);
			playerNode.Player = this;
			playerNode.GlobalPosition = new Vector3(position.x, position.y, position.z);
			playerNode.SetHeadInfo();
		}
	}

	// 清理事件订阅
    ~Player()
    {
		World.OnWorldReady -= OnWorldReady;
		
        KBELog.DEBUG_MSG("Player事件订阅已清理");
    }
}
