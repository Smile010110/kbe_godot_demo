using System;
using Godot;
using KBEngine;

public class Player : PlayerBase
{
	public static event Action OnEnterWorldRequested;
	public static Player LocalPlayer { get; private set; }

	private bool _waitingForWorld;

	public override void __init__()
	{
		base.__init__();
		World.OnWorldReady -= OnWorldReady;
		World.OnWorldReady += OnWorldReady;
	}

	public override void onEnterWorld()
	{
		base.onEnterWorld();

		if (isPlayer())
		{
			LocalPlayer = this;
		}

		if (World.Instance == null)
		{
			_waitingForWorld = true;

			if (isPlayer())
			{
				OnEnterWorldRequested?.Invoke();
			}

			return;
		}

		CreateRenderObject();
	}

	public override void onLeaveWorld()
	{
		base.onLeaveWorld();
		CleanupRenderObject();
		_waitingForWorld = false;

		if (ReferenceEquals(LocalPlayer, this))
		{
			LocalPlayer = null;
		}
	}

	public override void onDestroy()
	{
		World.OnWorldReady -= OnWorldReady;
		base.onDestroy();
	}

	public override void onDbidChanged(ulong oldValue)
	{
		if (renderObj is PlayerController playerNode)
		{
			playerNode.SetHeadInfo();
		}
	}

	public override void onServer_idChanged(ushort oldValue)
	{
		if (renderObj is PlayerController playerNode)
		{
			playerNode.SetHeadInfo();
		}
	}

	public override void onNameChanged(string oldValue)
	{
		if (renderObj is PlayerController playerNode)
		{
			playerNode.SetHeadInfo();
		}
	}

	public override void onPositionChanged(KBVector3 oldValue)
	{
		base.onPositionChanged(oldValue);

		if (!isPlayer() && renderObj is PlayerController playerNode)
		{
			playerNode.UpdateFromEntity();
		}
	}

	public override void onDirectionChanged(KBVector3 oldValue)
	{
		base.onDirectionChanged(oldValue);

		if (!isPlayer() && renderObj is PlayerController playerNode)
		{
			playerNode.UpdateFromEntity();
		}
	}

	private void OnWorldReady()
	{
		if (!_waitingForWorld)
		{
			return;
		}

		_waitingForWorld = false;
		CreateRenderObject();
	}

	private void CreateRenderObject()
	{
		if (World.Instance == null)
		{
			_waitingForWorld = true;
			return;
		}

		if (renderObj is PlayerController existingPlayerNode)
		{
			existingPlayerNode.BindPlayer(this);
			return;
		}

		var playerScene = GD.Load<PackedScene>("res://Prefab/Player.tscn");
		var playerNode = playerScene.Instantiate<PlayerController>();
		renderObj = playerNode;
		World.Instance.AddChild(playerNode);
		playerNode.BindPlayer(this);
	}

	private void CleanupRenderObject()
	{
		if (renderObj is Node node)
		{
			node.QueueFree();
		}

		renderObj = null;
	}
}
