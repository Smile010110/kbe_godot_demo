using Godot;
using System;
using System.Text.Json;
using CommonData;

public partial class MainUi : Control
{
	private const string LoginConfigPath = "user://login.cfg";
	private const string LoginConfigSection = "login";
	private static readonly string[] NicknamePrefixes = { "Swift", "Quiet", "Iron", "Sunny", "Frost", "Wild", "Bright", "Amber" };
	private static readonly string[] NicknameSuffixes = { "Fox", "Wolf", "Leaf", "River", "Stone", "Falcon", "Star", "Pine" };

	public static MainUi Instance;

	private LineEdit _userNameEdit;
	private LineEdit _passwordEdit;
	private LineEdit _nameEdit;
	private CheckBox _rememberLoginCheckBox;
	private Button _loginButton;
	private Label _statusLabel;
	private KbeClient _client;

	public override void _Ready()
	{
		Instance = this;
		_userNameEdit = GetNode<LineEdit>("CenterContainer/Panel/VBox/UserNameEdit");
		_passwordEdit = GetNode<LineEdit>("CenterContainer/Panel/VBox/PasswordEdit");
		_nameEdit = GetNode<LineEdit>("CenterContainer/Panel/VBox/NameRow/NameEdit");
		_rememberLoginCheckBox = GetNode<CheckBox>("CenterContainer/Panel/VBox/RememberLoginCheckBox");
		_loginButton = GetNode<Button>("CenterContainer/Panel/VBox/LoginBtn");
		_statusLabel = GetNode<Label>("CenterContainer/Panel/VBox/StatusLabel");
		_client = App.Instance?.Client;

		_statusLabel.Text = $"Ready to connect to {GameConfig.KbEngineHost}:{GameConfig.KbEnginePort}";
		LoadRememberedLogin();

		Player.OnLocalPlayerEnterWorldRequested += OnPlayerEnterWorldRequested;

		if (_client != null)
		{
			_client.ConnectionStateChanged += OnConnectionState;
			_client.LoginFailed += OnLoginFailed;
			_client.BaseappLoginSucceeded += OnLoginBaseapp;
			_client.Disconnected += OnDisconnected;
		}
	}

	public override void _ExitTree()
	{
		Player.OnLocalPlayerEnterWorldRequested -= OnPlayerEnterWorldRequested;

		if (_client != null)
		{
			_client.ConnectionStateChanged -= OnConnectionState;
			_client.LoginFailed -= OnLoginFailed;
			_client.BaseappLoginSucceeded -= OnLoginBaseapp;
			_client.Disconnected -= OnDisconnected;
		}

		base._ExitTree();
	}

	public void OnConnectionState(bool success)
	{
		_statusLabel.Text = success ? "Connected to loginapp, waiting for baseapp..." : "Connection failed.";
		_loginButton.Disabled = false;
	}

	public void OnLoginFailed(string errorMessage)
	{
		_statusLabel.Text = $"Login failed: {errorMessage}";
		_loginButton.Disabled = false;
	}

	public void OnLoginBaseapp()
	{
		_statusLabel.Text = "Baseapp login succeeded, waiting for Player entity...";
	}

	public void OnDisconnected()
	{
		_statusLabel.Text = "Disconnected from server.";
		_loginButton.Disabled = false;
	}

	private void OnPlayerEnterWorldRequested()
	{
		_statusLabel.Text = "Player is ready, loading world...";
		GetTree().ChangeSceneToFile("res://World.tscn");
	}

	private void _on_remember_login_check_box_toggled(bool toggledOn)
	{
		if (!toggledOn)
		{
			ClearRememberedLogin();
		}
	}

	private void _on_login_btn_button_up()
	{
		if (_client == null || !_client.IsInitialized)
		{
			_statusLabel.Text = "KBEngine is not initialized yet.";
			return;
		}

		var account = _userNameEdit.Text.Trim();
		var password = _passwordEdit.Text.Trim();
		var displayName = _nameEdit.Text.Trim();

		if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
		{
			_statusLabel.Text = "Username and password are required.";
			return;
		}

		if (string.IsNullOrWhiteSpace(displayName))
		{
			displayName = GenerateRandomNickname();
			_nameEdit.Text = displayName;
		}

		PersistLoginPreference(account, password, displayName);

		var loginData = new LoginData
		{
			ServerId = GameConfig.DefaultServerId,
			ClientInfo = GameConfig.ClientInfo,
			Name = displayName,
		};

		var payload = JsonSerializer.SerializeToUtf8Bytes(loginData);
		_statusLabel.Text = $"Logging in to {GameConfig.KbEngineHost}:{GameConfig.KbEnginePort}...";
		_loginButton.Disabled = true;
		_client.Login(account, password, payload);
	}

	private void _on_random_name_btn_button_up()
	{
		_nameEdit.Text = GenerateRandomNickname();
	}

	private void LoadRememberedLogin()
	{
		var config = new ConfigFile();
		if (config.Load(LoginConfigPath) != Error.Ok)
		{
			_rememberLoginCheckBox.ButtonPressed = false;
			return;
		}

		var rememberLogin = (bool)config.GetValue(LoginConfigSection, "remember", false);
		_rememberLoginCheckBox.ButtonPressed = rememberLogin;

		if (!rememberLogin)
		{
			return;
		}

		_userNameEdit.Text = (string)config.GetValue(LoginConfigSection, "username", string.Empty);
		_passwordEdit.Text = (string)config.GetValue(LoginConfigSection, "password", string.Empty);
		_nameEdit.Text = (string)config.GetValue(LoginConfigSection, "display_name", string.Empty);
	}

	private void PersistLoginPreference(string account, string password, string displayName)
	{
		if (!_rememberLoginCheckBox.ButtonPressed)
		{
			ClearRememberedLogin();
			return;
		}

		var config = new ConfigFile();
		config.SetValue(LoginConfigSection, "remember", true);
		config.SetValue(LoginConfigSection, "username", account);
		config.SetValue(LoginConfigSection, "password", password);
		config.SetValue(LoginConfigSection, "display_name", displayName);
		config.Save(LoginConfigPath);
	}

	private void ClearRememberedLogin()
	{
		if (!FileAccess.FileExists(LoginConfigPath))
		{
			return;
		}

		DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(LoginConfigPath));
	}

	private static string GenerateRandomNickname()
	{
		var prefix = NicknamePrefixes[Random.Shared.Next(NicknamePrefixes.Length)];
		var suffix = NicknameSuffixes[Random.Shared.Next(NicknameSuffixes.Length)];
		var number = Random.Shared.Next(100, 999);
		return $"{prefix}{suffix}{number}";
	}
}
