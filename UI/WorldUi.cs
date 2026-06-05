using System.Collections.Generic;
using CommonData;
using Godot;

public partial class WorldUi : Control
{
	private Label _infoLabel;
	private Panel _targetInfoPanel;
	private Label _targetNameLabel;
	private Label _targetHpLabel;
	private ProgressBar _targetHpBar;
	private HBoxContainer _skillButtonContainer;
	private readonly Dictionary<Button, SkillConfigEntry> _skillButtons = new();
	private double _refreshAccumulator;
	private string _lastInfoText = string.Empty;

	private ISelectableWorldEntityController _cachedTarget;
	private string _lastTargetName = string.Empty;
	private string _lastTargetHpText = string.Empty;

	public override void _Ready()
	{
		_infoLabel = GetNode<Label>("MarginContainer/InfoLabel");
		_targetInfoPanel = GetNode<Panel>("TargetInfoPanel");
		_targetNameLabel = GetNode<Label>("TargetInfoPanel/TargetNameLabel");
		_targetHpLabel = GetNode<Label>("TargetInfoPanel/TargetHPLabel");
		_targetHpBar = EnsureTargetHpBar();
		_skillButtonContainer = GetNode<HBoxContainer>("SkillBarPanel/SkillButtonContainer");
		PopulateSkillButtons();
		RefreshHud(force: true);
		RefreshTargetInfo(force: true);
		RefreshSkillButtons();
	}

	public override void _Process(double delta)
	{
		_refreshAccumulator += delta;
		if (_refreshAccumulator < ClientUiConfig.WorldHudRefreshIntervalSeconds)
		{
			return;
		}

		_refreshAccumulator = 0.0d;
		RefreshHud();
		RefreshTargetInfo();
		RefreshSkillButtons();
	}

	private void PopulateSkillButtons()
	{
		foreach (Node child in _skillButtonContainer.GetChildren())
		{
			_skillButtonContainer.RemoveChild(child);
			child.QueueFree();
		}

		_skillButtons.Clear();

		var skills = new List<SkillConfigEntry>(SkillConfigRepository.Datas.Values);
		skills.Sort((left, right) => left.Id.CompareTo(right.Id));
		foreach (var skill in skills)
		{
			var skillId = skill.Id;
			var button = new Button
			{
				Text = $"{skill.Id}. {skill.DisplayName}",
				TooltipText = BuildSkillTooltip(skill),
				CustomMinimumSize = new Vector2(112.0f, 42.0f),
				FocusMode = FocusModeEnum.None,
			};

			button.Pressed += () => PlayerController.LocalInstance?.TryCastSelectedTargetSkill(skillId);
			_skillButtonContainer.AddChild(button);
			_skillButtons[button] = skill;
		}
	}

	private static string BuildSkillTooltip(SkillConfigEntry skill)
	{
		var effectText = skill.IsHealSkill ? "治疗" : "伤害";
		return $"MP {skill.CostMp} | CD {skill.CooldownSeconds:0.#}s | Delay {skill.CastDelaySeconds:0.##}s | Range {skill.RangeMax:0.#} | {effectText} x{skill.EffectValue:0.#}";
	}

	private void RefreshSkillButtons()
	{
		var playerController = PlayerController.LocalInstance;
		var hasLocalPlayer = playerController?.Player != null;
		foreach (var pair in _skillButtons)
		{
			var button = pair.Key;
			var skill = pair.Value;
			var cooldownRemaining = playerController?.GetDisplayCooldownRemaining(skill) ?? 0.0f;
			button.Text = cooldownRemaining > 0.0f
				? $"{skill.Id}. {skill.DisplayName} ({cooldownRemaining:0.0})"
				: $"{skill.Id}. {skill.DisplayName}";
			button.Disabled = !hasLocalPlayer || cooldownRemaining > 0.0f || playerController?.IsSkillCastLocked == true;
		}
	}

	private void RefreshHud(bool force = false)
	{
		var player = PlayerController.LocalInstance?.Player;
		var playerController = PlayerController.LocalInstance;
		var entityId = player != null ? player.EntityId.ToString() : "-";
		var dbid = player != null ? player.DatabaseId.ToString() : "-";
		var serverId = player != null ? player.ServerId.ToString() : "-";
		var serverTime = player != null ? player.ServerTimeText : "-";
		var spaceLine = player != null ? player.SpaceLine.ToString() : "-";
		var spaceUtype = player != null ? player.SpaceUtype.ToString() : "-";
		var moveSpeed = player != null ? player.RawMoveSpeed.ToString() : "-";
		var hp = player != null ? player.HitPoints.ToString() : "-";
		var mp = player != null ? player.ManaPoints.ToString() : "-";
		var position = player?.WorldPosition ?? Vector3.Zero;
		var positionText = player != null
			? $"({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00})"
			: "-";
		var animationState = playerController != null ? playerController.CurrentAnimationStateName : "-";
		var animationKey = playerController != null ? playerController.CurrentAnimationKey : "-";
		var skillCast = playerController != null ? playerController.LastSkillCastSummary : "-";

		var nextInfoText = $"WASD move\nSpace jump\nHold RMB to rotate camera\nEntity: {entityId}\nDBID: {dbid}\nServer: {serverId}\nServerTime: {serverTime}\nSpaceUType: {spaceUtype}\nSpaceLine: {spaceLine}\nPosition: {positionText}\nMoveSpeed: {moveSpeed}\nHP: {hp}\nMP: {mp}\nAnimState: {animationState}\nAnimKey: {animationKey}\nSkillCast: {skillCast}";
		if (!force && string.Equals(_lastInfoText, nextInfoText, System.StringComparison.Ordinal))
		{
			return;
		}

		_lastInfoText = nextInfoText;
		_infoLabel.Text = nextInfoText;
	}

	private void RefreshTargetInfo(bool force = false)
	{
		var target = PlayerController.LocalInstance?.SelectedTarget;
		if (target == null || target is not GodotObject targetObject || !IsInstanceValid(targetObject))
		{
			HideTargetInfo();
			return;
		}

		if (!ReferenceEquals(target, _cachedTarget))
		{
			_cachedTarget = target;
			_lastTargetName = string.Empty;
			_lastTargetHpText = string.Empty;
			force = true;
		}

		var entity = target.SelectedEntityView;
		if (entity == null)
		{
			HideTargetInfo();
			return;
		}

		var name = entity.DisplayName ?? "???";
		var hpText = entity.MaxHitPoints == 0UL
			? (string.IsNullOrWhiteSpace(entity.SecondaryInfoText) ? "NPC" : entity.SecondaryInfoText)
			: $"HP {entity.HitPoints}/{entity.MaxHitPoints} | MP {entity.ManaPoints}";

		_targetInfoPanel.Visible = true;
		RefreshTargetHpBar(entity);

		if (force || _lastTargetName != name)
		{
			_lastTargetName = name;
			_targetNameLabel.Text = name;
		}

		if (force || _lastTargetHpText != hpText)
		{
			_lastTargetHpText = hpText;
			_targetHpLabel.Text = hpText;
		}
	}

	private void HideTargetInfo()
	{
		if (!_targetInfoPanel.Visible)
		{
			return;
		}

		_targetInfoPanel.Visible = false;
		_lastTargetName = string.Empty;
		_lastTargetHpText = string.Empty;
		_cachedTarget = null;
		if (_targetHpBar != null && IsInstanceValid(_targetHpBar))
		{
			_targetHpBar.Value = 0.0d;
			_targetHpBar.Visible = false;
		}
	}

	private ProgressBar EnsureTargetHpBar()
	{
		var existingBar = _targetInfoPanel.GetNodeOrNull<ProgressBar>("RuntimeTargetHPBar");
		if (existingBar != null)
		{
			return existingBar;
		}

		var bar = new ProgressBar
		{
			Name = "RuntimeTargetHPBar",
			ShowPercentage = false,
			MinValue = 0.0d,
			MaxValue = 1.0d,
			Value = 0.0d,
			CustomMinimumSize = new Vector2(170.0f, 8.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		bar.SetAnchorsPreset(LayoutPreset.TopLeft);
		bar.Position = new Vector2(_targetHpLabel.Position.X, _targetHpLabel.Position.Y + _targetHpLabel.Size.Y + 4.0f);
		bar.Visible = false;
		_targetInfoPanel.AddChild(bar);
		return bar;
	}

	private void RefreshTargetHpBar(IWorldEntityView entity)
	{
		if (_targetHpBar == null || !IsInstanceValid(_targetHpBar))
		{
			_targetHpBar = EnsureTargetHpBar();
		}

		if (entity == null || entity.MaxHitPoints == 0UL)
		{
			_targetHpBar.Visible = false;
			_targetHpBar.Value = 0.0d;
			return;
		}

		_targetHpBar.Visible = true;
		_targetHpBar.MaxValue = entity.MaxHitPoints;
		_targetHpBar.Value = Mathf.Clamp((double)entity.HitPoints, 0.0d, entity.MaxHitPoints);
	}
}
