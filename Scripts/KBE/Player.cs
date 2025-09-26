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
		KBELog.DEBUG_MSG("Player::__init__()");
	}

	public override void onServer_idChanged(UInt16 oldValue)
	{
		KBELog.DEBUG_MSG($"Player::onServer_idChanged() {oldValue}");
	}

}
