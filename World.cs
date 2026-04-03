using Godot;
using System;

public partial class World : Node3D
{
	public static World Instance { get; private set; }
	public static event Action OnWorldReady;

	public override void _Ready()
	{
		base._Ready();
		Instance = this;

		var worldUiScene = GD.Load<PackedScene>("res://UI/WorldUI.tscn");
		var worldUi = worldUiScene.Instantiate<Control>();
		worldUi.MouseFilter = Control.MouseFilterEnum.Ignore;
		GetParent().CallDeferred("add_child", worldUi);

		OnWorldReady?.Invoke();
	}

	public override void _ExitTree()
	{
		if (ReferenceEquals(Instance, this))
		{
			Instance = null;
		}

		base._ExitTree();
	}
}
