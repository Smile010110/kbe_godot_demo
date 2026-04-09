using System;
using Godot;
using KBEngine;

public sealed class WorldEntityRenderBinding<TEntity, TController>
	where TEntity : class
	where TController : Node, IWorldEntityController<TEntity>
{
	private readonly Entity _owner;
	private readonly TEntity _entity;
	private bool _waitingForWorld;

	public WorldEntityRenderBinding(Entity owner, TEntity entity)
	{
		_owner = owner;
		_entity = entity;
	}

	public void Initialize()
	{
		World.OnWorldReady -= HandleWorldReady;
		World.OnWorldReady += HandleWorldReady;
	}

	public void EnterWorld(Action onWaitingForWorld = null)
	{
		if (World.Instance == null)
		{
			WaitForWorld();
			onWaitingForWorld?.Invoke();
			return;
		}

		CreateOrBindRenderObject();
	}

	public void WaitForWorld()
	{
		_waitingForWorld = true;
	}

	public void HandleWorldReady()
	{
		if (!_waitingForWorld)
		{
			return;
		}

		_waitingForWorld = false;
		CreateOrBindRenderObject();
	}

	public void CreateOrBindRenderObject()
	{
		if (World.Instance == null)
		{
			_waitingForWorld = true;
			return;
		}

		_waitingForWorld = false;

		if (_owner.renderObj is TController existingController)
		{
			existingController.BindEntity(_entity);
			return;
		}

		var entityScene = WorldEntitySceneRegistry.GetPackedScene<TEntity>();
		var entityNode = entityScene.Instantiate<TController>();
		_owner.renderObj = entityNode;
		World.Instance.AddChild(entityNode);
		entityNode.BindEntity(_entity);
	}

	public void RefreshInfo()
	{
		if (_owner.renderObj is TController entityController)
		{
			entityController.SetHeadInfo();
		}
	}

	public void RefreshTransform()
	{
		if (_owner.renderObj is TController entityController)
		{
			entityController.UpdateFromEntity();
		}
	}

	public void Cleanup()
	{
		if (_owner.renderObj is Node node)
		{
			node.QueueFree();
		}

		_owner.renderObj = null;
		_waitingForWorld = false;
	}

	public void Destroy()
	{
		World.OnWorldReady -= HandleWorldReady;
		Cleanup();
	}
}
