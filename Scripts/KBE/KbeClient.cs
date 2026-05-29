using System;
using KBEngine;

public sealed class KbeClient : IDisposable
{
	private bool _isBound;

	public event Action<bool> ConnectionStateChanged;
	public event Action<string> LoginFailed;
	public event Action BaseappLoginStarted;
	public event Action<Player> LocalPlayerEnteredWorld;
	public event Action Disconnected;

	public bool IsInitialized => KBEngineApp.app != null;

	public void Bind()
	{
		if (_isBound)
		{
			return;
		}

		KBEngine.Event.registerOut(EventOutTypes.onConnectionState, this, nameof(HandleConnectionState));
		KBEngine.Event.registerOut(EventOutTypes.onLoginFailed, this, nameof(HandleLoginFailed));
		KBEngine.Event.registerOut(EventOutTypes.onLoginBaseapp, this, nameof(HandleLoginBaseapp));
		KBEngine.Event.registerOut(EventOutTypes.onLoginBaseappFailed, this, nameof(HandleLoginBaseappFailed));
		KBEngine.Event.registerOut(EventOutTypes.onEnterWorld, this, nameof(HandleEnterWorld));
		KBEngine.Event.registerOut(EventOutTypes.onDisconnected, this, nameof(HandleDisconnected));
		_isBound = true;
	}

	public void Unbind()
	{
		if (!_isBound)
		{
			return;
		}

		KBEngine.Event.deregisterOut(EventOutTypes.onConnectionState, this, nameof(HandleConnectionState));
		KBEngine.Event.deregisterOut(EventOutTypes.onLoginFailed, this, nameof(HandleLoginFailed));
		KBEngine.Event.deregisterOut(EventOutTypes.onLoginBaseapp, this, nameof(HandleLoginBaseapp));
		KBEngine.Event.deregisterOut(EventOutTypes.onLoginBaseappFailed, this, nameof(HandleLoginBaseappFailed));
		KBEngine.Event.deregisterOut(EventOutTypes.onEnterWorld, this, nameof(HandleEnterWorld));
		KBEngine.Event.deregisterOut(EventOutTypes.onDisconnected, this, nameof(HandleDisconnected));
		_isBound = false;
	}

	public void Login(string account, string password, byte[] payload)
	{
		if (KBEngineApp.app == null)
		{
			throw new InvalidOperationException("KBEngine 尚未初始化。");
		}

		KBEngineApp.app.login(account, password, payload);
	}

	public string DescribeServerError(ushort retCode)
	{
		return KBEngineApp.app != null ? KBEngineApp.app.serverErr(retCode) : $"retCode={retCode}";
	}

	public void Dispose()
	{
		Unbind();
	}

	public bool TryGetLocalPlayer(out Player player)
	{
		var app = KBEngineApp.app;
		player = Player.LocalPlayer;
		if (player != null && player.inWorld && app != null && player.id == app.entity_id)
		{
			return true;
		}

		var entity = app?.player();
		player = entity as Player;
		return player != null && entity.inWorld && app != null && entity.id == app.entity_id;
	}

	public void NotifyIfLocalPlayerReady()
	{
		if (TryGetLocalPlayer(out var player))
		{
			LocalPlayerEnteredWorld?.Invoke(player);
		}
	}

	public void HandleConnectionState(bool success)
	{
		ConnectionStateChanged?.Invoke(success);
	}

	public void HandleLoginFailed(ushort retCode, byte[] _serverData)
	{
		LoginFailed?.Invoke(DescribeServerError(retCode));
	}

	public void HandleLoginBaseappFailed(ushort retCode)
	{
		LoginFailed?.Invoke(DescribeServerError(retCode));
	}

	public void HandleLoginBaseapp()
	{
		BaseappLoginStarted?.Invoke();
		NotifyIfLocalPlayerReady();
	}

	public void HandleEnterWorld(KBEngine.Entity entity)
	{
		if (entity is not Player player)
		{
			return;
		}

		if (KBEngineApp.app == null || entity.id != KBEngineApp.app.entity_id)
		{
			return;
		}

		LocalPlayerEnteredWorld?.Invoke(player);
	}

	public void HandleDisconnected()
	{
		Disconnected?.Invoke();
	}
}
