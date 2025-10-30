using Godot;
using System;
using KBEngine;

public partial class World : Node3D
{
	public static World Instance;

	// 移除构造函数，使用Godot的生命周期方法
	public World()
	{
		KBELog.DEBUG_MSG("World单例已初始化 11");
	    World.Instance = this;
	}

	public override void _Ready()
	{
		base._Ready();

		// 在这里设置单例实例
		Instance = this;
		KBELog.DEBUG_MSG("World单例已初始化 22");

		var worldUITscn = GD.Load<PackedScene>("res://UI/WorldUI.tscn");
		Control worldUI = worldUITscn.Instantiate<Control>();
		worldUI.MouseFilter = Control.MouseFilterEnum.Ignore;
		GetParent().CallDeferred("add_child", worldUI);
	}
}
