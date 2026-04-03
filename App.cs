using Godot;
using System;
using System.Threading;
using KBEngine;

public partial class App : GodotKBEMain
{
	private bool _isShuttingDown;

	public override void _Ready()
	{
		KBELog.Init(new GodotLogProvider());
		ip = GameConfig.KbEngineHost;
		port = GameConfig.KbEnginePort;
		base._Ready();
	}

	public override void _ExitTree()
	{
		ShutdownKbEngineGracefully();
		base._ExitTree();
	}

	private void ShutdownKbEngineGracefully()
	{
		if (_isShuttingDown)
		{
			return;
		}

		_isShuttingDown = true;
		GD.Print("clientapp::OnDestroy(): begin");

		if (KBEngineApp.app != null)
		{
			if (KBEngineApp.app.currserver == "baseapp")
			{
				KBEngineApp.app.logout();
				FlushPendingNetwork();

				// Prevent the generated destroy() from immediately sending a second logout.
				KBEngineApp.app.currserver = string.Empty;
			}

			gameapp?.destroy();
			KBEngineApp.app = null;
		}

		gameapp = null;
		KBEngine.Event.clear();
		GD.Print("clientapp::OnDestroy(): end");
	}

	private void FlushPendingNetwork()
	{
		if (KBEngineApp.app?.networkInterface() == null || !KBEngineApp.app.networkInterface().valid())
		{
			return;
		}

		var deadline = DateTime.UtcNow.AddMilliseconds(250);
		while (DateTime.UtcNow < deadline)
		{
			if (!isMultiThreads)
			{
				gameapp?.process();
			}

			KBEngine.Event.processOutEvents();
			Thread.Sleep(15);
		}
	}
}
