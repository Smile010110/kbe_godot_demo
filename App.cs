using Godot;
using System;
using System.Threading;
using CommonData;
using KBEngine;

public partial class App : GodotKBEMain
{
	private const string StartScenePath = "res://Start.tscn";

	public static App Instance { get; private set; }

	private bool _isShuttingDown;
	private bool _isRecoveringFromDisconnect;
	// 仅从主线程 / CallDeferred 回调中访问；Godot 单线程模式下无需同步。
	private string _pendingStatusMessage = string.Empty;

	public KbeClient Client { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		RoleConfigRepository.Warmup();
		SexConfigRepository.Warmup();
		PlayerAppearanceConfigRepository.Warmup();
		KBELog.Init(new GodotLogProvider());
		ip = GameConfig.KbEngineHost;
		port = GameConfig.KbEnginePort;
		syncPlayerMS = ClientNetworkConfig.PlayerSyncIntervalMs;
		serverHeartbeatTick = GameConfig.ServerHeartbeatTick;
		GetTree().AutoAcceptQuit = false;
		base._Ready();
		Client = new KbeClient();
		Client.Bind();
		Client.Disconnected += OnClientDisconnected;
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
		if (Client != null)
		{
			Client.Disconnected -= OnClientDisconnected;
		}

		Client?.Dispose();
		Client = null;
		ClientRuntimeState.ResetForSceneTransition();

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
	}

	public string ConsumePendingStatusMessage()
	{
		var message = _pendingStatusMessage;
		_pendingStatusMessage = string.Empty;
		return message;
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

	private void OnClientDisconnected()
	{
		if (_isShuttingDown || _isRecoveringFromDisconnect)
		{
			return;
		}

		_isRecoveringFromDisconnect = true;
		_pendingStatusMessage = "已与服务器断开连接，服务器可能已经重启，请重新登录。";
		CallDeferred(nameof(RecoverFromUnexpectedDisconnect));
	}

	private void RecoverFromUnexpectedDisconnect()
	{
		if (_isShuttingDown)
		{
			return;
		}

		ClientRuntimeState.ResetForSceneTransition();
		gameapp?.reset();
		var error = GetTree().ChangeSceneToFile(StartScenePath);
		if (error != Error.Ok)
		{
			_pendingStatusMessage = $"返回登录场景失败：{error}";
		}

		_isRecoveringFromDisconnect = false;
	}
}
