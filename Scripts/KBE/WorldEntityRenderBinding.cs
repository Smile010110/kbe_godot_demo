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
	private bool _isDestroyed;

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
		World.OnWorldReady -= HandleWorldReady;
		World.OnWorldReady += HandleWorldReady;
		_waitingForWorld = true;
	}

	public void HandleWorldReady()
	{
		if (_isDestroyed || !_waitingForWorld)
		{
			return;
		}

		_waitingForWorld = false;
		CreateOrBindRenderObject();
	}

	public void CreateOrBindRenderObject()
	{
		if (_isDestroyed || World.Instance == null)
		{
			_waitingForWorld = !_isDestroyed;
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
		if (_isDestroyed)
		{
			return;
		}

		if (_owner.renderObj is TController entityController)
		{
			entityController.SetHeadInfo();
		}
	}

	public void RefreshTransform()
	{
		if (_isDestroyed)
		{
			return;
		}

		if (_owner.renderObj is TController entityController)
		{
			entityController.UpdateFromEntity();
		}
	}

	public void Cleanup()
	{
		World.OnWorldReady -= HandleWorldReady;
		if (_owner.renderObj is Node node)
		{
			node.QueueFree();
		}

		_owner.renderObj = null;
		_waitingForWorld = false;
	}

	public void Destroy()
	{
		_isDestroyed = true;
		World.OnWorldReady -= HandleWorldReady;
		Cleanup();
	}
}
