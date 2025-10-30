using Godot;
using System;

public partial class WorldUi : Control
{
	
	private Button _button;
	
	public override void _Ready()
	{
		_button = GetNode<Button>("Button");
	}
	
	
	public void _on_button_button_up()
	{
		// PlayerController.Instance?.Player?.cellEntityCall.relive(1); // 调用复活方法
	}


	public override void _Process(double delta)
	{
		// _button.Visible = PlayerController.Instance?.Player?.state == 1;
	}
}
