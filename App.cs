using Godot;
using CommonData;
using KBEngine;

public partial class App : GodotKBEMain
{
	private const string StartScenePath = "res://Start.tscn";

	public static App Instance { get; private set; }

	private bool _isShuttingDown;
	private bool _isRecoveringFromDisconnect;
	private bool _isKbEngineRuntimeActive;
	private string _pendingStatusMessage = string.Empty;

	public KbeClient Client { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		RoleConfigRepository.Warmup();
		SexConfigRepository.Warmup();
		AttrConfigRepository.Warmup();
		BuffConfigRepository.Warmup();
		SkillConfigRepository.Warmup();
		PlayerAppearanceConfigRepository.Warmup();
		KBELog.Init(new ClientKbeLogProvider());
		ip = GameConfig.KbEngineHost;
		port = GameConfig.KbEnginePort;
		syncPlayerMS = ClientNetworkConfig.PlayerSyncIntervalMs;
		// GodotKBEMain halves this value before passing it into KBEngineArgs.
		serverHeartbeatTick = GameConfig.ServerHeartbeatTick * 2;
		GetTree().AutoAcceptQuit = false;
		base._Ready();
		_isKbEngineRuntimeActive = true;
		BindClientFacade();
	}

	public override void KBEUpdate()
	{
		if (_isShuttingDown || !_isKbEngineRuntimeActive || gameapp == null || KBEngineApp.app == null)
		{
			KBEngine.Event.processOutEvents();
			return;
		}

		base.KBEUpdate();
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

	public string ConsumePendingStatusMessage()
	{
		var message = _pendingStatusMessage;
		_pendingStatusMessage = string.Empty;
		return message;
	}

	private void BindClientFacade()
	{
		Client = new KbeClient();
		Client.Bind();
		Client.Disconnected += OnClientDisconnected;
	}

	private void UnbindClientFacade()
	{
		if (Client == null)
		{
			return;
		}

		Client.Disconnected -= OnClientDisconnected;
		Client.Dispose();
		Client = null;
	}

	private void ShutdownKbEngineGracefully()
	{
		if (_isShuttingDown)
		{
			return;
		}

		_isShuttingDown = true;
		UnbindClientFacade();
		ClientRuntimeState.ResetForSceneTransition();
		DestroyKbEngineSession(sendLogout: true, clearEvents: true);
	}

	private void FlushPendingNetwork()
	{
		if (KBEngineApp.app?.networkInterface() == null || !KBEngineApp.app.networkInterface().valid())
		{
			return;
		}

		for (var i = 0; i < 3; i++)
		{
			if (!isMultiThreads)
			{
				gameapp?.process();
			}

			KBEngine.Event.processOutEvents();
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
		RestartKbEngineSession();
		var error = GetTree().ChangeSceneToFile(StartScenePath);
		if (error != Error.Ok)
		{
			_pendingStatusMessage = $"返回登录场景失败：{error}";
		}

		_isRecoveringFromDisconnect = false;
	}

	private void RestartKbEngineSession()
	{
		_isKbEngineRuntimeActive = false;
		UnbindClientFacade();
		DestroyKbEngineSession(sendLogout: false, clearEvents: false);
		KBEngine.Event.clearFiredEvents();
		initKBEngine();
		_isKbEngineRuntimeActive = true;
		BindClientFacade();
	}

	private void DestroyKbEngineSession(bool sendLogout, bool clearEvents)
	{
		_isKbEngineRuntimeActive = false;

		if (KBEngineApp.app != null)
		{
			if (sendLogout && KBEngineApp.app.currserver == "baseapp")
			{
				KBEngineApp.app.logout();
				FlushPendingNetwork();
			}

			// Avoid a second generated logout when the socket is already gone.
			KBEngineApp.app.currserver = string.Empty;
			gameapp?.destroy();
			KBEngineApp.app = null;
		}

		gameapp = null;
		if (clearEvents)
		{
			KBEngine.Event.clear();
		}
	}
}
