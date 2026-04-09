using Godot;
using System;

public partial class World : Node3D
{
	private const string WorldUiScenePath = "res://UI/WorldUI.tscn";

	public static World Instance { get; private set; }
	public static event Action OnWorldReady;

	private Control _worldUi;

	public override void _Ready()
	{
		base._Ready();
		Instance = this;
		CallDeferred(nameof(AttachWorldUi));

		OnWorldReady?.Invoke();
	}

	public override void _ExitTree()
	{
		DestroyWorldUi();

		if (ReferenceEquals(Instance, this))
		{
			Instance = null;
		}

		base._ExitTree();
	}

	public static void ResetStaticState()
	{
		Instance = null;
		OnWorldReady = null;
	}

	private void AttachWorldUi()
	{
		if (_worldUi != null && IsInstanceValid(_worldUi) && _worldUi.GetParent() != null)
		{
			return;
		}

		var uiParent = GetTree().CurrentScene ?? GetParent();
		if (uiParent == null)
		{
			return;
		}

		var worldUiScene = GD.Load<PackedScene>(WorldUiScenePath);
		_worldUi ??= worldUiScene.Instantiate<Control>();
		_worldUi.MouseFilter = Control.MouseFilterEnum.Ignore;
		if (_worldUi.GetParent() == null)
		{
			uiParent.AddChild(_worldUi);
		}
	}

	private void DestroyWorldUi()
	{
		if (_worldUi != null && IsInstanceValid(_worldUi))
		{
			_worldUi.QueueFree();
		}

		_worldUi = null;
	}
}
