using System;
using Godot;
using KBEngine;

public class Player : PlayerBase
{
	public static Player Instance;
	public Player()
	{
		Instance = this;
	}

	public override void __init__()
	{
		base.__init__();

		KBELog.DEBUG_MSG("Player::__init__()");
	}

	public override void onServer_idChanged(UInt16 oldValue)
	{
		KBELog.DEBUG_MSG($"Player::onServer_idChanged() {oldValue}");
	}

	public override void onEnterWorld()
	{
		base.onEnterWorld();
		
		if (this.isPlayer())
		{
			KBELog.DEBUG_MSG($"Player::onEnterWorld() player {this.id}");
			// var player = GD.Load<PackedScene>("res://Prefab/Player.tscn");
			// PlayerController playerNode = player.Instantiate<PlayerController>();
			// // monsterNode.trs
			// this.renderObj = playerNode;
			// World.Instance.GetTree().CurrentScene.GetNode("World").AddChild(playerNode);
			// playerNode.Player = this;
			// playerNode.GlobalPosition = new Vector3(position.x, position.y, position.z);
			
			// playerNode.SetHeadInfo();
		   
		}
		else
		{
			KBELog.DEBUG_MSG($"Player::onEnterWorld() not player {this.id}");
			// var monster = GD.Load<PackedScene>("res://Prefab/Avatar.tscn");
			// AvatarController monsterNode = monster.Instantiate<AvatarController>();
			// this.renderObj = monsterNode;
			// monsterNode.Avatar = this;
			// World.Instance.GetTree().CurrentScene.GetNode("World").AddChild(monsterNode);
			// monsterNode.GlobalPosition = new Vector3(position.x, 0, position.z);
			// monsterNode.SetHeadInfo();
		}
	}
}
