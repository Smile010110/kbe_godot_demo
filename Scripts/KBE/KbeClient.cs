using System;
using KBEngine;

public sealed class KbeClient : IDisposable
{
	private bool _isBound;

	public event Action<bool> ConnectionStateChanged;
	public event Action<string> LoginFailed;
	public event Action BaseappLoginSucceeded;
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
		KBEngine.Event.deregisterOut(EventOutTypes.onDisconnected, this, nameof(HandleDisconnected));
		_isBound = false;
	}

	public void Login(string account, string password, byte[] payload)
	{
		if (KBEngineApp.app == null)
		{
			throw new InvalidOperationException("KBEngine is not initialized.");
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

	public void HandleConnectionState(bool success)
	{
		ConnectionStateChanged?.Invoke(success);
	}

	public void HandleLoginFailed(ushort retCode, byte[] _serverData)
	{
		LoginFailed?.Invoke(DescribeServerError(retCode));
	}

	public void HandleLoginBaseapp()
	{
		BaseappLoginSucceeded?.Invoke();
	}

	public void HandleDisconnected()
	{
		Disconnected?.Invoke();
	}
}
