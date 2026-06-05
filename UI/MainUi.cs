using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommonData;

public partial class MainUi : Control
{
	private const string LoginConfigPath = "user://login.json";
	private const string LegacyLoginConfigPath = "user://login.cfg";
	private const string LoginConfigSection = "login";
	private const double LocalPlayerReadyPollIntervalSeconds = 0.1d;
	private const double LoginSaveDebounceSeconds = 0.35d;
	private static readonly string[] NicknamePrefixes = { "清风", "流云", "长歌", "星河", "青岚", "暮雪", "惊鸿", "晓月" };
	private static readonly string[] NicknameSuffixes = { "剑", "影", "歌", "川", "羽", "辰", "霜", "岚" };

	public static MainUi Instance { get; private set; }

	private LineEdit _userNameEdit;
	private LineEdit _passwordEdit;
	private LineEdit _nameEdit;
	private CheckBox _rememberLoginCheckBox;
	private Button _randomNameButton;
	private Button _createRoleButton;
	private Label _createRoleSummaryLabel;
	private Button _loginButton;
	private Label _statusLabel;
	private Control _createRoleOverlay;
	private LineEdit _createNameEdit;
	private Button _createRandomNameButton;
	private OptionButton _roleOptionButton;
	private OptionButton _sexOptionButton;
	private Label _modelIdLabel;
	private Button _createRoleConfirmButton;
	private Button _createRoleCancelButton;
	private KbeClient _client;
	private bool _isLoginInFlight;
	private bool _isWorldLoadRequested;
	private bool _isLoadingRememberedLogin;
	private bool _hasPendingRememberedLoginSave;
	private double _rememberedLoginSaveRemainingSeconds;
	private double _localPlayerReadyPollAccumulator;

	public override void _Ready()
	{
		Instance = this;
		_userNameEdit = GetNode<LineEdit>("CenterContainer/Panel/VBox/UserNameEdit");
		_passwordEdit = GetNode<LineEdit>("CenterContainer/Panel/VBox/PasswordEdit");
		_nameEdit = GetNode<LineEdit>("CenterContainer/Panel/VBox/NameRow/NameEdit");
		_rememberLoginCheckBox = GetNode<CheckBox>("CenterContainer/Panel/VBox/RememberLoginCheckBox");
		_randomNameButton = GetNode<Button>("CenterContainer/Panel/VBox/NameRow/RandomNameBtn");
		_createRoleButton = GetNode<Button>("CenterContainer/Panel/VBox/CreateRoleBtn");
		_createRoleSummaryLabel = GetNode<Label>("CenterContainer/Panel/VBox/CreateRoleSummaryLabel");
		_loginButton = GetNode<Button>("CenterContainer/Panel/VBox/LoginBtn");
		_statusLabel = GetNode<Label>("CenterContainer/Panel/VBox/StatusLabel");
		_createRoleOverlay = GetNode<Control>("CreateRoleOverlay");
		_createNameEdit = GetNode<LineEdit>("CreateRoleOverlay/CenterContainer/Panel/VBox/CreateNameRow/CreateNameEdit");
		_createRandomNameButton = GetNode<Button>("CreateRoleOverlay/CenterContainer/Panel/VBox/CreateNameRow/CreateRandomNameBtn");
		_roleOptionButton = GetNode<OptionButton>("CreateRoleOverlay/CenterContainer/Panel/VBox/RoleOptionButton");
		_sexOptionButton = GetNode<OptionButton>("CreateRoleOverlay/CenterContainer/Panel/VBox/SexOptionButton");
		_modelIdLabel = GetNode<Label>("CreateRoleOverlay/CenterContainer/Panel/VBox/ModelIdLabel");
		_createRoleConfirmButton = GetNode<Button>("CreateRoleOverlay/CenterContainer/Panel/VBox/ActionRow/ConfirmBtn");
		_createRoleCancelButton = GetNode<Button>("CreateRoleOverlay/CenterContainer/Panel/VBox/ActionRow/CancelBtn");
		_client = App.Instance?.Client;

		_userNameEdit.TextChanged += _ => ScheduleRememberedLoginSave();
		_passwordEdit.TextChanged += _ => ScheduleRememberedLoginSave();
		_nameEdit.TextChanged += _ => ScheduleRememberedLoginSave();

		PopulateCreateRoleOptions();
		LoadRememberedLogin();
		ApplyDraftToUi(CharacterCreationState.Current);
		UpdateCreateRoleSummary();

		var pendingStatusMessage = App.Instance?.ConsumePendingStatusMessage();
		_statusLabel.Text = string.IsNullOrWhiteSpace(pendingStatusMessage)
			? $"准备连接服务器：{GameConfig.KbEngineHost}:{GameConfig.KbEnginePort}"
			: pendingStatusMessage;
		SetLoginUiBusy(false);

		if (_client != null)
		{
			_client.ConnectionStateChanged += OnConnectionState;
			_client.LoginFailed += OnLoginFailed;
			_client.BaseappLoginStarted += OnBaseappLoginStarted;
			_client.LocalPlayerEnteredWorld += OnLocalPlayerEnteredWorld;
			_client.Disconnected += OnDisconnected;
			_client.NotifyIfLocalPlayerReady();
		}
	}

	public override void _ExitTree()
	{
		FlushPendingRememberedLoginSave();

		if (_client != null)
		{
			_client.ConnectionStateChanged -= OnConnectionState;
			_client.LoginFailed -= OnLoginFailed;
			_client.BaseappLoginStarted -= OnBaseappLoginStarted;
			_client.LocalPlayerEnteredWorld -= OnLocalPlayerEnteredWorld;
			_client.Disconnected -= OnDisconnected;
		}

		if (ReferenceEquals(Instance, this))
		{
			Instance = null;
		}

		base._ExitTree();
	}

	public static void ResetStaticState()
	{
		Instance = null;
	}

	public override void _Process(double delta)
	{
		TickRememberedLoginSave(delta);

		if (!_isLoginInFlight || _isWorldLoadRequested || _client == null)
		{
			return;
		}

		_localPlayerReadyPollAccumulator += delta;
		if (_localPlayerReadyPollAccumulator < LocalPlayerReadyPollIntervalSeconds)
		{
			return;
		}

		_localPlayerReadyPollAccumulator = 0.0d;
		if (_client.TryGetLocalPlayer(out _))
		{
			RequestWorldLoad();
		}
	}

	public void OnConnectionState(bool success)
	{
		_statusLabel.Text = success ? "已连接 loginapp，正在等待进入 baseapp..." : "连接失败。";
		if (!success)
		{
			SetLoginUiBusy(false);
		}
	}

	public void OnLoginFailed(string errorMessage)
	{
		_statusLabel.Text = $"登录失败：{errorMessage}";
		SetLoginUiBusy(false);
	}

	public void OnBaseappLoginStarted()
	{
		_statusLabel.Text = "loginapp 已认证，正在连接 baseapp 并等待 Player 实体...";
		_client?.NotifyIfLocalPlayerReady();
	}

	public void OnDisconnected()
	{
		_statusLabel.Text = "已与服务器断开连接。";
		SetLoginUiBusy(false);
	}

	private void OnLocalPlayerEnteredWorld(Player _player)
	{
		RequestWorldLoad();
	}

	private void RequestWorldLoad()
	{
		if (_isWorldLoadRequested)
		{
			return;
		}

		_isWorldLoadRequested = true;
		_statusLabel.Text = "角色已就绪，正在加载场景...";
		var error = GetTree().ChangeSceneToFile("res://World.tscn");
		if (error == Error.Ok)
		{
			return;
		}

		_isWorldLoadRequested = false;
		_statusLabel.Text = $"加载世界场景失败：{error}";
		SetLoginUiBusy(false);
	}

	private void _on_remember_login_check_box_toggled(bool toggledOn)
	{
		if (!toggledOn)
		{
			ClearRememberedLogin();
			return;
		}

		PersistRememberedLoginIfEnabled();
	}

	private void _on_login_btn_button_up()
	{
		if (_isLoginInFlight)
		{
			return;
		}

		if (_client == null || !_client.IsInitialized)
		{
			_statusLabel.Text = "KBEngine 尚未初始化完成。";
			return;
		}

		var account = _userNameEdit.Text.Trim();
		var password = _passwordEdit.Text.Trim();
		if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
		{
			_statusLabel.Text = "账号和密码不能为空。";
			return;
		}

		if (!CharacterCreationState.Current.IsConfirmed)
		{
			_statusLabel.Text = "请先创建角色再进行登录。";
			OpenCreateRoleOverlay();
			return;
		}

		var draft = CharacterCreationState.Current.Clone();
		var displayName = _nameEdit.Text.Trim();
		if (string.IsNullOrWhiteSpace(displayName))
		{
			displayName = GenerateRandomNickname();
			_nameEdit.Text = displayName;
		}

		draft.Name = displayName;
		draft.IsConfirmed = true;
		CharacterCreationState.EnsureModelResolved(draft);
		CharacterCreationState.Set(draft);
		UpdateCreateRoleSummary();
		PersistLoginPreference(account, password, draft);

		var loginData = new LoginData
		{
			ServerId = GameConfig.DefaultServerId,
			ClientInfo = GameConfig.ClientInfo,
			Name = draft.Name,
			Role = draft.Role,
			Sex = draft.Sex,
			ModelId = draft.ModelId,
		};

		var payload = JsonSerializer.SerializeToUtf8Bytes(loginData);
		_statusLabel.Text = $"正在登录服务器：{GameConfig.KbEngineHost}:{GameConfig.KbEnginePort}...";
		_isWorldLoadRequested = false;
		_localPlayerReadyPollAccumulator = 0.0d;
		SetLoginUiBusy(true);
		try
		{
			_client.Login(account, password, payload);
		}
		catch (Exception e)
		{
			SetLoginUiBusy(false);
			_statusLabel.Text = $"发起登录失败：{e.Message}";
		}
	}

	private void _on_random_name_btn_button_up()
	{
		var name = GenerateRandomNickname();
		_nameEdit.Text = name;
		_createNameEdit.Text = name;
	}

	private void _on_create_role_btn_button_up()
	{
		OpenCreateRoleOverlay();
	}

	private void _on_create_random_name_btn_button_up()
	{
		_createNameEdit.Text = GenerateRandomNickname();
	}

	private void _on_create_sex_option_button_item_selected(long index)
	{
		UpdateModelIdLabel();
	}

	private void _on_create_role_cancel_btn_button_up()
	{
		_createRoleOverlay.Visible = false;
	}

	private void _on_create_role_confirm_btn_button_up()
	{
		var draft = BuildDraftFromCreateRoleUi();
		if (string.IsNullOrWhiteSpace(draft.Name))
		{
			draft.Name = GenerateRandomNickname();
		}

		draft.IsConfirmed = true;
		CharacterCreationState.EnsureModelResolved(draft);
		CharacterCreationState.Set(draft);
		_nameEdit.Text = draft.Name;
		_createNameEdit.Text = draft.Name;
		UpdateCreateRoleSummary();
		FlushPendingRememberedLoginSave();
		PersistRememberedLoginIfEnabled();
		_createRoleOverlay.Visible = false;
	}

	private void PopulateCreateRoleOptions()
	{
		_roleOptionButton.Clear();
		var roleEntries = new List<RoleConfigEntry>(RoleConfigRepository.Datas.Values);
		roleEntries.Sort((left, right) =>
		{
			var leftValue = left.Role != 0 ? left.Role : left.Id;
			var rightValue = right.Role != 0 ? right.Role : right.Id;
			return leftValue.CompareTo(rightValue);
		});
		foreach (var role in roleEntries)
		{
			var roleValue = role.Role != 0 ? role.Role : role.Id;
			_roleOptionButton.AddItem(role.DisplayName, roleValue);
		}

		_sexOptionButton.Clear();
		var sexEntries = new List<SexConfigEntry>(SexConfigRepository.Datas.Values);
		sexEntries.Sort((left, right) => left.Sex.CompareTo(right.Sex));
		foreach (var sex in sexEntries)
		{
			_sexOptionButton.AddItem(sex.DisplayName, sex.Sex);
		}

		if (_roleOptionButton.ItemCount == 0)
		{
			_roleOptionButton.AddItem(RoleConfigRepository.ResolveDisplayName(1), 1);
		}

		if (_sexOptionButton.ItemCount == 0)
		{
			_sexOptionButton.AddItem(SexConfigRepository.ResolveDisplayName(1), 1);
		}
	}

	private void LoadRememberedLogin()
	{
		_isLoadingRememberedLogin = true;
		if (!TryLoadRememberedLoginJson(out var preference))
		{
			if (!TryLoadLegacyRememberedLogin(out preference))
			{
				_rememberLoginCheckBox.ButtonPressed = false;
				_isLoadingRememberedLogin = false;
				return;
			}
		}

		try
		{
			_rememberLoginCheckBox.ButtonPressed = preference.Remember;
			if (!preference.Remember)
			{
				return;
			}

			_userNameEdit.Text = preference.Username ?? string.Empty;
			_passwordEdit.Text = preference.ResolvePassword();
			_nameEdit.Text = preference.DisplayName ?? string.Empty;

			var draft = CharacterCreationState.BuildDefaultDraft();
			draft.Name = _nameEdit.Text;
			draft.Role = preference.Role != 0 ? preference.Role : draft.Role;
			draft.Sex = preference.Sex != 0 ? preference.Sex : draft.Sex;
			draft.ModelId = preference.ModelId != 0U ? preference.ModelId : draft.ModelId;
			draft.IsConfirmed = preference.CharacterConfirmed;
			CharacterCreationState.EnsureModelResolved(draft);
			CharacterCreationState.Set(draft);
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Ignored invalid login config: {exception.Message}");
			_rememberLoginCheckBox.ButtonPressed = false;
			CharacterCreationState.Reset();
		}
		finally
		{
			_isLoadingRememberedLogin = false;
		}
	}

	private void PersistRememberedLoginIfEnabled()
	{
		_hasPendingRememberedLoginSave = false;
		_rememberedLoginSaveRemainingSeconds = 0.0d;

		if (_isLoadingRememberedLogin || !_rememberLoginCheckBox.ButtonPressed)
		{
			return;
		}

		var draft = CharacterCreationState.Current.Clone();
		var displayName = _nameEdit.Text.Trim();
		if (!string.IsNullOrWhiteSpace(displayName))
		{
			draft.Name = displayName;
		}

		CharacterCreationState.EnsureModelResolved(draft);
		PersistLoginPreference(_userNameEdit.Text.Trim(), _passwordEdit.Text.Trim(), draft);
	}

	private void PersistLoginPreference(string account, string password, CharacterCreationDraft draft)
	{
		if (!_rememberLoginCheckBox.ButtonPressed)
		{
			ClearRememberedLogin();
			return;
		}

		var preference = new RememberedLoginPreference
		{
			Remember = true,
			Username = account,
			DisplayName = draft.Name,
			Role = draft.Role,
			Sex = draft.Sex,
			ModelId = draft.ModelId,
			CharacterConfirmed = draft.IsConfirmed,
			SavedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
		};
		preference.SetPassword(password);

		try
		{
			using var file = FileAccess.Open(LoginConfigPath, FileAccess.ModeFlags.Write);
			file.StoreString(JsonSerializer.Serialize(preference, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Failed to save login json: {exception.Message}");
		}
	}

	private void ClearRememberedLogin()
	{
		_hasPendingRememberedLoginSave = false;
		_rememberedLoginSaveRemainingSeconds = 0.0d;
		RemoveUserFile(LoginConfigPath);
		RemoveUserFile(LegacyLoginConfigPath);
	}

	private void ScheduleRememberedLoginSave()
	{
		if (_isLoadingRememberedLogin || !_rememberLoginCheckBox.ButtonPressed)
		{
			return;
		}

		_hasPendingRememberedLoginSave = true;
		_rememberedLoginSaveRemainingSeconds = LoginSaveDebounceSeconds;
	}

	private void TickRememberedLoginSave(double delta)
	{
		if (!_hasPendingRememberedLoginSave)
		{
			return;
		}

		_rememberedLoginSaveRemainingSeconds -= delta;
		if (_rememberedLoginSaveRemainingSeconds > 0.0d)
		{
			return;
		}

		PersistRememberedLoginIfEnabled();
	}

	private void FlushPendingRememberedLoginSave()
	{
		if (!_hasPendingRememberedLoginSave)
		{
			return;
		}

		PersistRememberedLoginIfEnabled();
	}

	private static bool TryLoadRememberedLoginJson(out RememberedLoginPreference preference)
	{
		preference = null;
		if (!FileAccess.FileExists(LoginConfigPath))
		{
			return false;
		}

		try
		{
			using var file = FileAccess.Open(LoginConfigPath, FileAccess.ModeFlags.Read);
			preference = JsonSerializer.Deserialize<RememberedLoginPreference>(file.GetAsText());
			return preference != null;
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Ignored invalid login json: {exception.Message}");
			return false;
		}
	}

	private static bool TryLoadLegacyRememberedLogin(out RememberedLoginPreference preference)
	{
		preference = null;
		var config = new ConfigFile();
		if (config.Load(LegacyLoginConfigPath) != Error.Ok)
		{
			return false;
		}

		preference = new RememberedLoginPreference
		{
			Remember = (bool)config.GetValue(LoginConfigSection, "remember", false),
			Username = (string)config.GetValue(LoginConfigSection, "username", string.Empty),
			LegacyPassword = (string)config.GetValue(LoginConfigSection, "password", string.Empty),
			DisplayName = (string)config.GetValue(LoginConfigSection, "display_name", string.Empty),
			Role = (int)config.GetValue(LoginConfigSection, "role", 0),
			Sex = (int)config.GetValue(LoginConfigSection, "sex", 0),
			ModelId = Convert.ToUInt32(config.GetValue(LoginConfigSection, "model_id", 0L)),
			CharacterConfirmed = (bool)config.GetValue(LoginConfigSection, "character_confirmed", false),
		};
		return true;
	}

	private static void RemoveUserFile(string path)
	{
		if (FileAccess.FileExists(path))
		{
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
		}
	}

	private void OpenCreateRoleOverlay()
	{
		var draft = CharacterCreationState.Current.Clone();
		if (string.IsNullOrWhiteSpace(draft.Name))
		{
			draft.Name = _nameEdit.Text.Trim();
		}

		ApplyDraftToUi(draft);
		_createRoleOverlay.Visible = true;
	}

	private void ApplyDraftToUi(CharacterCreationDraft draft)
	{
		CharacterCreationState.EnsureModelResolved(draft);
		_createNameEdit.Text = draft.Name;

		SelectOptionById(_roleOptionButton, draft.Role);
		SelectOptionById(_sexOptionButton, draft.Sex);
		UpdateModelIdLabel();
	}

	private CharacterCreationDraft BuildDraftFromCreateRoleUi()
	{
		var draft = CharacterCreationState.Current.Clone();
		draft.Name = _createNameEdit.Text.Trim();
		draft.Role = GetSelectedOptionId(_roleOptionButton, draft.Role);
		draft.Sex = GetSelectedOptionId(_sexOptionButton, draft.Sex);
		CharacterCreationState.EnsureModelResolved(draft);
		return draft;
	}

	private void UpdateCreateRoleSummary()
	{
		var draft = CharacterCreationState.Current;
		if (!draft.IsConfirmed)
		{
			_createRoleSummaryLabel.Text = "尚未创建角色。请先点击“创建角色”配置职业、性别和模型。";
			return;
		}

		var displayName = string.IsNullOrWhiteSpace(draft.Name) ? "-" : draft.Name;
		_createRoleSummaryLabel.Text = $"角色：{displayName} | 职业：{RoleConfigRepository.ResolveDisplayName(draft.Role)} | 性别：{SexConfigRepository.ResolveDisplayName(draft.Sex)} | 模型ID：{draft.ModelId}";
	}

	private void UpdateModelIdLabel()
	{
		var sex = GetSelectedOptionId(_sexOptionButton, CharacterCreationState.Current.Sex);
		if (SexConfigRepository.TryGetBySex(sex, out var sexConfig))
		{
			_modelIdLabel.Text = $"模型ID：{sexConfig.ModelId}";
			return;
		}

		_modelIdLabel.Text = "模型ID：-";
	}

	private static int GetSelectedOptionId(OptionButton optionButton, int fallback)
	{
		var selectedIndex = optionButton.Selected;
		return selectedIndex >= 0 ? optionButton.GetItemId(selectedIndex) : fallback;
	}

	private static void SelectOptionById(OptionButton optionButton, int value)
	{
		for (var index = 0; index < optionButton.ItemCount; index++)
		{
			if (optionButton.GetItemId(index) != value)
			{
				continue;
			}

			optionButton.Select(index);
			return;
		}

		if (optionButton.ItemCount > 0)
		{
			optionButton.Select(0);
		}
	}

	private static string GenerateRandomNickname()
	{
		var prefix = NicknamePrefixes[Random.Shared.Next(NicknamePrefixes.Length)];
		var suffix = NicknameSuffixes[Random.Shared.Next(NicknameSuffixes.Length)];
		var number = Random.Shared.Next(100, 999);
		return $"{prefix}{suffix}{number}";
	}

	private void SetLoginUiBusy(bool isBusy)
	{
		_isLoginInFlight = isBusy;
		_userNameEdit.Editable = !isBusy;
		_passwordEdit.Editable = !isBusy;
		_nameEdit.Editable = !isBusy;
		_rememberLoginCheckBox.Disabled = isBusy;
		_randomNameButton.Disabled = isBusy;
		_createRoleButton.Disabled = isBusy;
		_loginButton.Disabled = isBusy;
		_createNameEdit.Editable = !isBusy;
		_roleOptionButton.Disabled = isBusy;
		_sexOptionButton.Disabled = isBusy;
		_createRandomNameButton.Disabled = isBusy;
		_createRoleConfirmButton.Disabled = isBusy;
		_createRoleCancelButton.Disabled = isBusy;
	}

	private sealed class RememberedLoginPreference
	{
		private const string PasswordEncoding = "device-xor-v1";

		public bool Remember { get; set; }
		public string Username { get; set; } = string.Empty;

		[JsonPropertyName("password")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string LegacyPassword { get; set; }

		[JsonPropertyName("password_obfuscated")]
		public string ObfuscatedPassword { get; set; } = string.Empty;

		[JsonPropertyName("password_encoding")]
		public string ObfuscatedPasswordEncoding { get; set; } = PasswordEncoding;

		public string DisplayName { get; set; } = string.Empty;
		public int Role { get; set; }
		public int Sex { get; set; }
		public uint ModelId { get; set; }
		public bool CharacterConfirmed { get; set; }
		public long SavedAtUnixMs { get; set; }

		public string ResolvePassword()
		{
			if (string.Equals(ObfuscatedPasswordEncoding, PasswordEncoding, StringComparison.Ordinal)
				&& !string.IsNullOrWhiteSpace(ObfuscatedPassword))
			{
				return DecodeDeviceBoundValue(ObfuscatedPassword);
			}

			return LegacyPassword ?? string.Empty;
		}

		public void SetPassword(string password)
		{
			LegacyPassword = null;
			ObfuscatedPasswordEncoding = PasswordEncoding;
			ObfuscatedPassword = EncodeDeviceBoundValue(password ?? string.Empty);
		}

		private static string EncodeDeviceBoundValue(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			var bytes = Encoding.UTF8.GetBytes(value);
			ApplyDeviceKey(bytes);
			return Convert.ToBase64String(bytes);
		}

		private static string DecodeDeviceBoundValue(string encodedValue)
		{
			try
			{
				var bytes = Convert.FromBase64String(encodedValue);
				ApplyDeviceKey(bytes);
				return Encoding.UTF8.GetString(bytes);
			}
			catch (Exception exception)
			{
				GD.PushWarning($"Ignored invalid saved password: {exception.Message}");
				return string.Empty;
			}
		}

		private static void ApplyDeviceKey(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return;
			}

			var deviceId = OS.GetUniqueId();
			var keyText = string.IsNullOrWhiteSpace(deviceId)
				? "kbe_godot_demo_login"
				: $"kbe_godot_demo_login:{deviceId}";
			var keyBytes = Encoding.UTF8.GetBytes(keyText);
			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] ^= keyBytes[i % keyBytes.Length];
			}
		}
	}
}
