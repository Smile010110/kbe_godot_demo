using Godot;
using System;
using System.Threading;
using KBEngine;

public partial class App : GodotKBEMain
{
	public static App Instance { get; private set; }

	private bool _isShuttingDown;

	public KbeClient Client { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		KBELog.Init(new GodotLogProvider());
		ip = GameConfig.KbEngineHost;
		port = GameConfig.KbEnginePort;
		serverHeartbeatTick = GameConfig.ServerHeartbeatTick;
		GetTree().AutoAcceptQuit = false;
		base._Ready();
		Client = new KbeClient();
		Client.Bind();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
		{
			ShutdownKbEngineGracefully();
			GetTree().Quit();
			return;
		}

		base._Notification(what);
	}

	public override void _ExitTree()
	{
		ShutdownKbEngineGracefully();

		if (ReferenceEquals(Instance, this))
		{
			Instance = null;
		}

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

		Client?.Dispose();
		Client = null;

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

		var deadline = DateTime.UtcNow.AddMilliseconds(500);
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
