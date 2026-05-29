using Godot;

public partial class StartScript : Node
{
	private PackedScene _mainUiScene;

	public override void _Ready()
	{
		_mainUiScene = GD.Load<PackedScene>("res://UI/MainUI.tscn");
		AddChild(_mainUiScene.Instantiate());
	}
}
