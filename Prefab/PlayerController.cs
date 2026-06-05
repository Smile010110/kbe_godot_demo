using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using CommonData;

public partial class PlayerController : WorldEntityControllerBase<Player>
{
	private sealed class PlayerAnimationRuntimeSet
	{
		public AnimationLibrary AnimationLibrary { get; } = new();
		public Dictionary<PlayerAnimationState, string> StateAnimations { get; } = new();
		public Dictionary<string, string> AnimationKeysByStem { get; } = new(StringComparer.OrdinalIgnoreCase);
	}

	private enum PlayerAnimationState
	{
		Idle,
		MoveForward,
		MoveBackward,
		StrafeLeft,
		StrafeRight,
		JumpIdle,
		JumpMove,
		Attack,
		HeavyAttack,
		Cast,
	}

	private const string PlayerModelPath = "PlayerCharacterBody3D/PlayerModel";
	private const string RuntimeAnimationLibraryName = "player_runtime";
	private const float AnimationMoveEpsilon = 0.05f;
	private const float DirectionSelectionThreshold = 0.35f;
	private const float SelectionRayLength = 1000.0f;
	private const float SelectionRingYOffset = 0.08f;
	private const float SelectionRingDefaultInnerRadius = 0.62f;
	private const float SelectionRingThickness = 0.10f;
	private const float SelectionRingGroundRayStartHeight = 2.0f;
	private const float SelectionRingGroundRayLength = 8.0f;
	private const int SelectionRingSegments = 96;
	private const float GlobalCooldownSeconds = 1.5f;
	private const float FloatingSkillTextLifetimeSeconds = 0.85f;
	private const float RemoteSkillResultAnimationStaleSeconds = 0.5f;
	private const int MaxPendingRemoteSkillAnimations = 32;

	private static readonly Dictionary<uint, PlayerAnimationRuntimeSet> s_sharedAnimationRuntimeSets = new();

	[Export]
	public float RemotePlayerInterpolationSeconds = 0.06f;
	[Export]
	public float RemotePlayerMinInterpolationSeconds = 0.02f;
	[Export]
	public float RemotePlayerMaxInterpolationSeconds = 0.12f;
	[Export]
	public float RemotePlayerSnapDistance = 0.9f;
	[Export]
	public float AttackCooldownSeconds = 0.6f;
	[Export]
	public float AttackRange = 4.0f;
	[Export]
	public int DefaultTargetSkillId = 1001;
	[Export]
	public float SkillCastAckTimeoutSeconds = 1.5f;

	public static PlayerController LocalInstance { get; private set; }

	public Player Player => EntityView;
	public string CurrentAnimationKey => _currentAnimationKey;
	public string CurrentAnimationStateName => _currentAnimationStateName;
	public string LastSkillCastSummary => _lastSkillCastSummary;
	public bool IsSkillCastLocked => _isSkillCastPending || _skillAnimationLockRemaining > 0.0f;
	public ISelectableWorldEntityController SelectedTarget { get; private set; }

	private Node3D _cameraPivot;
	private Camera3D _camera;
	private Node3D _playerModelRoot;
	private AnimationPlayer _modelAnimationPlayer;
	private Vector3 _lastAnimationPosition;
	private bool _hasLastAnimationPosition;
	private string _currentAnimationKey = string.Empty;
	private string _currentAnimationStateName = PlayerAnimationState.Idle.ToString();
	private bool _wasOnFloorLastFrame = true;
	private bool _hasLatchedAirborneState;
	private PlayerAnimationState _latchedAirborneState = PlayerAnimationState.JumpIdle;
	private PlayerAppearanceProfile _currentAppearanceProfile;
	private PlayerAnimationRuntimeSet _currentAnimationRuntimeSet;
	private float _attackCooldownRemaining;
	private float _globalSkillCooldownRemaining;
	private readonly Dictionary<int, float> _skillCooldowns = new();
	private bool _isSkillCastPending;
	private float _skillCastAckTimeoutRemaining;
	private int _pendingSkillId;
	private int _pendingSkillTargetId;
	private float _skillAnimationLockRemaining;
	private string _activeTimedAnimationKey = string.Empty;
	private readonly List<SkillCastResult> _pendingRemoteSkillAnimations = new();
	private string _lastSkillCastSummary = "-";
	private MeshInstance3D _selectionRing;

	protected override string CharacterBodyPath => "PlayerCharacterBody3D";
	protected override string NameLabelPath => "PlayerCharacterBody3D/HeadInfo/NameLabel";
	protected override string InfoLabelPath => "PlayerCharacterBody3D/HeadInfo/HPLabel";

	public override void _Ready()
	{
		base._Ready();
	}

	public override void BindEntity(Player entity)
	{
		base.BindEntity(entity);

		EnsureAppearanceProfileApplied(force: true);
	}

	public override void _ExitTree()
	{
		if (ReferenceEquals(LocalInstance, this))
		{
			LocalInstance = null;
		}

		base._ExitTree();
	}

	public override void _Input(InputEvent @event)
	{

		if (Player == null || !Player.IsLocalPlayer)
		{
			return;
		}

		if (_camera == null)
		{
			return;
		}

		if (@event is InputEventMouseButton mouseButton
			&& mouseButton.ButtonIndex == MouseButton.Left
			&& mouseButton.Pressed)
		{
			if (IsPointerOverUi(mouseButton.Position))
			{
				return;
			}

			var targetController = FindSelectableTargetAtScreenPosition(mouseButton.Position);
			if (targetController == null)
			{
				ClearSelection();
				return;
			}

			if (ReferenceEquals(targetController, SelectedTarget))
			{
				return;
			}

			ClearSelection();
			SelectTarget(targetController);
		}
	}

	private bool IsPointerOverUi(Vector2 screenPosition)
	{
		var viewport = GetViewport();
		if (viewport?.GuiGetHoveredControl() != null)
		{
			return true;
		}

		return IsScreenPositionInsideControlTree(viewport?.GetTree()?.Root, screenPosition);
	}

	private static bool IsScreenPositionInsideControlTree(Node node, Vector2 screenPosition)
	{
		if (node == null)
		{
			return false;
		}

		foreach (Node child in node.GetChildren())
		{
			if (child is Control control
				&& control.IsVisibleInTree()
				&& control.MouseFilter == Control.MouseFilterEnum.Stop
				&& control.GetGlobalRect().HasPoint(screenPosition))
			{
				return true;
			}

			if (IsScreenPositionInsideControlTree(child, screenPosition))
			{
				return true;
			}
		}

		return false;
	}

	private void SelectTarget(ISelectableWorldEntityController target)
	{
		var body = GetTargetBody(target);
		if (body == null)
		{
			return;
		}

		SelectedTarget = target;
		_selectionRing = CreateSelectionRing(ResolveSelectionRingInnerRadius(body));
		_selectionRing.Name = "SelectionRing";

		body.AddChild(_selectionRing);
		UpdateSelectionRingPosition();

	}

	private void ClearSelection()
	{
		if (_selectionRing != null)
		{
			if (IsInstanceValid(_selectionRing))
			{
				_selectionRing.QueueFree();
			}
			_selectionRing = null;
		}

		SelectedTarget = null;
	}

	private bool IsSelectedTargetValid()
	{
		return SelectedTarget is GodotObject targetObject && IsInstanceValid(targetObject);
	}

	private void UpdateSelectionRingPosition()
	{
		if (_selectionRing == null || !IsInstanceValid(_selectionRing) || SelectedTarget == null)
		{
			return;
		}

		var body = GetTargetBody(SelectedTarget);
		if (body == null)
		{
			return;
		}

		_selectionRing.GlobalPosition = ResolveSelectionRingPosition(body);
		_selectionRing.GlobalRotationDegrees = Vector3.Zero;
	}

	public void TryAttack()
	{
		TryCastSelectedTargetSkill(DefaultTargetSkillId);
	}

	public void TryCastSelectedTargetSkill(int skillId)
	{
		if (Player == null || !Player.IsLocalPlayer)
		{
			return;
		}

		if (_isSkillCastPending)
		{
			return;
		}

		if (skillId < 0)
		{
			GD.PushWarning($"Invalid skill id: {skillId}");
			return;
		}

		var skillConfig = ResolveSkillConfig(skillId);
		if (skillConfig == null)
		{
			SetLocalSkillMessage($"技能不存在: {skillId}");
			return;
		}

		if (!IsSkillCooldownReady(skillConfig))
		{
			SetLocalSkillMessage(SkillCastError.ResolveMessage(SkillErrorCode.CooldownNotReady));
			return;
		}

		if (Player.ManaPoints < (ulong)skillConfig.CostMp)
		{
			SetLocalSkillMessage(SkillCastError.ResolveMessage(SkillErrorCode.NotEnoughMp));
			return;
		}

		var targetEntityId = ResolveSkillCastTargetEntityId(skillConfig);
		if (!IsValidSkillCastTarget(skillConfig, targetEntityId))
		{
			SetLocalSkillMessage(SkillCastError.ResolveMessage(SkillErrorCode.InvalidTarget));
			return;
		}

		if (skillConfig != null && !IsSkillTargetInRange(skillConfig, targetEntityId))
		{
			SetLocalSkillMessage(SkillCastError.ResolveMessage(SkillErrorCode.OutOfRange));
			return;
		}

		_pendingSkillId = skillId;
		_pendingSkillTargetId = targetEntityId;
		if (!Player.TryCastSkill(_pendingSkillId, targetEntityId))
		{
			_pendingSkillId = 0;
			_pendingSkillTargetId = 0;
			SetLocalSkillMessage("技能请求发送失败");
			return;
		}

		StartLocalSkillCooldown(skillConfig);
		SetLocalSkillMessage($"释放 {skillConfig.DisplayName}");
		StartLocalSkillCast(skillConfig);
	}

	public float GetSkillCooldownRemaining(int skillId)
	{
		if (!_skillCooldowns.TryGetValue(skillId, out var skillCooldown))
		{
			return 0.0f;
		}

		return Mathf.Max(0.0f, skillCooldown);
	}

	public float GetDisplayCooldownRemaining(SkillConfigEntry skillConfig)
	{
		if (skillConfig == null)
		{
			return 0.0f;
		}

		var skillCooldown = GetSkillCooldownRemaining(skillConfig.Id);
		var globalCooldown = skillConfig.UsesGlobalCooldown ? _globalSkillCooldownRemaining : 0.0f;
		return Mathf.Max(skillCooldown, globalCooldown);
	}

	public bool HandleServerSkillResult(SkillCastResult skillCast)
	{
		if (Player == null || !Player.IsLocalPlayer || skillCast == null)
		{
			return false;
		}

		var isCaster = skillCast.CasterId == Player.EntityId;
		if (isCaster)
		{
			if (_isSkillCastPending && skillCast.SkillId == _pendingSkillId)
			{
				CompleteLocalSkillCast();
			}
		}
		else
		{
			PlayCasterAnimation(skillCast);
		}

		ShowSkillResultText(skillCast);
		_lastSkillCastSummary = BuildSkillCastSummary(skillCast);
		return true;
	}

	private static string BuildSkillCastSummary(SkillCastResult skillCast)
	{
		if (skillCast == null)
		{
			return "-";
		}

		var effectText = skillCast.EffectType == SkillEffectType.Heal ? "Heal" : "Damage";
		var killText = skillCast.IsKill ? " Kill" : string.Empty;
		var resultTimeText = skillCast.HasResultTime ? $" result_time={skillCast.ResultTime}" : string.Empty;
		return $"Skill {skillCast.SkillId} {effectText} {skillCast.Value} caster={skillCast.CasterId} target={skillCast.TargetId}{killText}{resultTimeText}";
	}

	public bool HandleServerSkillError(SkillCastError error)
	{
		if (Player == null || !Player.IsLocalPlayer || error == null)
		{
			return false;
		}

		if (error.ErrorCode == SkillErrorCode.Casting)
		{
			if (!_isSkillCastPending)
			{
				SetLocalSkillMessage(error.Message);
			}
			return true;
		}

		var isPendingSkillError = _isSkillCastPending && error.SkillId == _pendingSkillId;
		if (isPendingSkillError)
		{
			CancelLocalSkillCast();
		}

		if (error.ErrorCode == SkillErrorCode.CooldownNotReady)
		{
			if (ResolveSkillConfig(error.SkillId) is SkillConfigEntry skillConfig)
			{
				StartLocalSkillCooldown(skillConfig);
			}
		}
		else if (isPendingSkillError)
		{
			ClearLocalSkillCooldown(error.SkillId);
		}

		SetLocalSkillMessage(error.Message);
		GD.PushWarning($"[SkillError] skill={error.SkillId} code={(byte)error.ErrorCode} message={error.Message}");
		return true;
	}

	private static PlayerAnimationState ResolveSkillAnimationState(SkillConfigEntry skillConfig)
	{
		if (skillConfig?.IsHealSkill == true)
		{
			return PlayerAnimationState.Cast;
		}

		return PlayerAnimationState.Attack;
	}

	private void StartLocalSkillCast(SkillConfigEntry skillConfig)
	{
		if (skillConfig == null)
		{
			return;
		}

		var castDelaySeconds = Mathf.Max(skillConfig.CastDelaySeconds, 0.0f);
		_isSkillCastPending = true;
		_skillCastAckTimeoutRemaining = Mathf.Max(SkillCastAckTimeoutSeconds, castDelaySeconds + 1.0f);
		_skillAnimationLockRemaining = castDelaySeconds;
		PlaySkillAnimation(skillConfig);
	}

	private void CompleteLocalSkillCast()
	{
		_isSkillCastPending = false;
		_skillCastAckTimeoutRemaining = 0.0f;
		_pendingSkillId = 0;
		_pendingSkillTargetId = 0;
	}

	private void CancelLocalSkillCast()
	{
		_isSkillCastPending = false;
		_skillCastAckTimeoutRemaining = 0.0f;
		_skillAnimationLockRemaining = 0.0f;
		_pendingSkillId = 0;
		_pendingSkillTargetId = 0;
		ResetAnimationSpeed();
	}

	private void PlaySkillAnimation(SkillConfigEntry skillConfig, double elapsedSeconds = 0.0d)
	{
		if (TryPlaySkillAnimationKey(skillConfig, elapsedSeconds))
		{
			return;
		}

		var state = ResolveSkillAnimationState(skillConfig);
		var desiredDuration = Mathf.Max(skillConfig.CastDelaySeconds, 0.05f);
		PlayAnimationForState(state, force: true, desiredDurationSeconds: desiredDuration, elapsedSeconds: elapsedSeconds);
	}

	private void PlaySkillResultAnimation(int skillId, double elapsedSeconds)
	{
		var skillConfig = ResolveSkillConfig(skillId);
		if (skillConfig == null)
		{
			PlayAnimationForState(PlayerAnimationState.Attack, force: true);
			return;
		}

		var castDelaySeconds = Mathf.Max(skillConfig.CastDelaySeconds, 0.05f);
		if (elapsedSeconds > RemoteSkillResultAnimationStaleSeconds)
		{
			return;
		}

		_skillAnimationLockRemaining = castDelaySeconds;
		PlaySkillAnimation(skillConfig);
	}

	private void PlayCasterAnimation(SkillCastResult skillCast)
	{
		if (!TryPlayCasterAnimation(skillCast))
		{
			QueuePendingRemoteSkillAnimation(skillCast);
		}
	}

	private bool TryPlayCasterAnimation(SkillCastResult skillCast)
	{
		if (skillCast == null || skillCast.CasterId <= 0)
		{
			return true;
		}

		var elapsedSeconds = skillCast.ResolveElapsedResultSeconds(Player?.ServerTime ?? 0UL);
		if (IsSkillResultAnimationTooLate(skillCast, elapsedSeconds))
		{
			return true;
		}

		var entity = KBEngine.KBEngineApp.app?.findEntity(skillCast.CasterId);
		if (entity?.renderObj is PlayerController playerController)
		{
			playerController.PlaySkillResultAnimation(skillCast.SkillId, elapsedSeconds);
			return true;
		}

		return entity?.renderObj != null;
	}

	private void QueuePendingRemoteSkillAnimation(SkillCastResult skillCast)
	{
		if (skillCast == null || !skillCast.HasResultTime)
		{
			return;
		}

		foreach (var pendingSkillCast in _pendingRemoteSkillAnimations)
		{
			if (pendingSkillCast.SkillId == skillCast.SkillId
				&& pendingSkillCast.CasterId == skillCast.CasterId
				&& pendingSkillCast.ResultTime == skillCast.ResultTime)
			{
				return;
			}
		}

		if (_pendingRemoteSkillAnimations.Count >= MaxPendingRemoteSkillAnimations)
		{
			_pendingRemoteSkillAnimations.RemoveAt(0);
		}

		_pendingRemoteSkillAnimations.Add(skillCast);
	}

	private void FlushPendingRemoteSkillAnimations()
	{
		if (_pendingRemoteSkillAnimations.Count == 0)
		{
			return;
		}

		for (var index = _pendingRemoteSkillAnimations.Count - 1; index >= 0; index--)
		{
			if (TryPlayCasterAnimation(_pendingRemoteSkillAnimations[index]))
			{
				_pendingRemoteSkillAnimations.RemoveAt(index);
			}
		}
	}

	private static bool IsSkillResultAnimationTooLate(SkillCastResult skillCast, double elapsedSeconds)
	{
		if (skillCast == null)
		{
			return true;
		}

		if (!skillCast.HasResultTime)
		{
			return false;
		}

		return elapsedSeconds > RemoteSkillResultAnimationStaleSeconds;
	}

	private bool TryPlaySkillAnimationKey(SkillConfigEntry skillConfig, double elapsedSeconds = 0.0d)
	{
		if (skillConfig == null
			|| string.IsNullOrWhiteSpace(skillConfig.AnimationKey)
			|| _currentAnimationRuntimeSet == null)
		{
			return false;
		}

		if (!_currentAnimationRuntimeSet.AnimationKeysByStem.TryGetValue(skillConfig.AnimationKey, out var animationKey))
		{
			return false;
		}

		var desiredDuration = Mathf.Max(skillConfig.CastDelaySeconds, 0.05f);
		PlayAnimationByKey(
			animationKey,
			force: true,
			desiredDurationSeconds: desiredDuration,
			stateName: $"Skill:{skillConfig.AnimationKey}",
			elapsedSeconds: elapsedSeconds);
		return true;
	}

	private void ShowSkillResultText(SkillCastResult skillCast)
	{
		if (!TryResolveSkillResultTextAnchor(skillCast, out var targetBody))
		{
			GD.PushWarning($"Skill result target has no render anchor. skill={skillCast.SkillId} target={skillCast.TargetId}");
			return;
		}

		SkillFloatingTextPresenter.Show(targetBody, skillCast, FloatingSkillTextLifetimeSeconds);
	}

	private bool TryResolveSkillResultTextAnchor(SkillCastResult skillCast, out Node3D anchor)
	{
		anchor = null;
		if (skillCast == null)
		{
			return false;
		}

		if (TryResolveSelectableController(skillCast.TargetId, out var targetController) && targetController.SelectionBody != null)
		{
			anchor = targetController.SelectionBody;
			return true;
		}

		if (SelectedTarget != null
			&& SelectedTarget.SelectedEntityId > 0
			&& SelectedTarget.SelectedEntityId == skillCast.TargetId
			&& SelectedTarget.SelectionBody != null)
		{
			anchor = SelectedTarget.SelectionBody;
			return true;
		}

		if (Player != null
			&& CharacterBody != null
			&& (skillCast.TargetId == Player.EntityId || skillCast.CasterId == Player.EntityId))
		{
			anchor = CharacterBody;
			return true;
		}

		if (CharacterBody != null)
		{
			anchor = CharacterBody;
			return true;
		}

		return false;
	}

	private static bool TryResolveSelectableController(int entityId, out ISelectableWorldEntityController controller)
	{
		controller = null;
		if (entityId <= 0 || KBEngine.KBEngineApp.app == null)
		{
			return false;
		}

		var entity = KBEngine.KBEngineApp.app.findEntity(entityId);
		controller = entity?.renderObj as ISelectableWorldEntityController;
		return controller != null;
	}

	private static SkillConfigEntry ResolveSkillConfig(int skillId)
	{
		return SkillConfigRepository.TryGetBySkillId(skillId, out var skillConfig)
			? skillConfig
			: null;
	}

	private bool IsSkillCooldownReady(SkillConfigEntry skillConfig)
	{
		return GetDisplayCooldownRemaining(skillConfig) <= 0.0f;
	}

	private void StartLocalSkillCooldown(SkillConfigEntry skillConfig)
	{
		if (skillConfig == null)
		{
			return;
		}

		_skillCooldowns[skillConfig.Id] = skillConfig.CooldownSeconds;
		if (skillConfig.UsesGlobalCooldown)
		{
			_globalSkillCooldownRemaining = GlobalCooldownSeconds;
		}
	}

	private void ClearLocalSkillCooldown(int skillId)
	{
		_skillCooldowns.Remove(skillId);
		if (ResolveSkillConfig(skillId)?.UsesGlobalCooldown == true)
		{
			_globalSkillCooldownRemaining = 0.0f;
		}
	}

	private void SetLocalSkillMessage(string message)
	{
		_lastSkillCastSummary = message;
	}

	private int ResolveSkillCastTargetEntityId(SkillConfigEntry skillConfig)
	{
		if (skillConfig == null || skillConfig.IsSelfTargetSkill || skillConfig.IsFriendlyTargetSkill)
		{
			return 0;
		}

		if (SelectedTarget == null || !IsSelectedTargetValid())
		{
			return 0;
		}

		var targetEntityId = SelectedTarget.SelectedEntityId;
		return targetEntityId > 0 ? targetEntityId : 0;
	}

	private bool IsValidSkillCastTarget(SkillConfigEntry skillConfig, int targetEntityId)
	{
		if (skillConfig == null)
		{
			return false;
		}

		if (skillConfig.IsSelfTargetSkill || skillConfig.IsFriendlyTargetSkill)
		{
			return true;
		}

		return targetEntityId > 0;
	}

	private bool IsSkillTargetInRange(SkillConfigEntry skillConfig, int targetEntityId)
	{
		if (Player != null && (targetEntityId == 0 || targetEntityId == Player.EntityId))
		{
			return true;
		}

		if (CharacterBody == null || SelectedTarget == null)
		{
			return false;
		}

		var targetBody = GetTargetBody(SelectedTarget);
		if (targetBody == null)
		{
			return false;
		}

		var range = skillConfig?.RangeMax > 0.0f ? skillConfig.RangeMax : AttackRange;
		if (range <= 0.0f)
		{
			return true;
		}

		var planarDelta = targetBody.GlobalPosition - CharacterBody.GlobalPosition;
		planarDelta.Y = 0.0f;
		return planarDelta.LengthSquared() <= range * range;
	}

	private ISelectableWorldEntityController FindSelectableTargetAtScreenPosition(Vector2 screenPosition)
	{
		var from = _camera.ProjectRayOrigin(screenPosition);
		var to = from + _camera.ProjectRayNormal(screenPosition) * SelectionRayLength;

		var spaceState = GetWorld3D().DirectSpaceState;
		var query = new PhysicsRayQueryParameters3D
		{
			From = from,
			To = to,
			CollisionMask = uint.MaxValue,
			CollideWithBodies = true,
			CollideWithAreas = false,
			HitFromInside = true,
		};

		if (CharacterBody != null)
		{
			query.Exclude.Add(CharacterBody.GetRid());
		}

		var result = spaceState.IntersectRay(query);
		if (!result.TryGetValue("collider", out var colliderObj) || colliderObj.As<Node>() == null)
		{
			return null;
		}


		var hitNode = colliderObj.As<Node>();
		return FindControllerInHierarchy<ISelectableWorldEntityController>(hitNode);
	}

	private static MeshInstance3D CreateSelectionRing(float innerRadius)
	{
		var ringMesh = CreateFlatSelectionRingMesh(innerRadius, innerRadius + SelectionRingThickness);

		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.15f, 0.15f, 1.0f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnergyMultiplier = 2.0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			DisableReceiveShadows = true,
		};

		var ring = new MeshInstance3D
		{
			Mesh = ringMesh,
			MaterialOverride = material,
			TopLevel = true,
		};

		return ring;
	}

	private static ArrayMesh CreateFlatSelectionRingMesh(float innerRadius, float outerRadius)
	{
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		for (var segment = 0; segment < SelectionRingSegments; segment++)
		{
			var currentAngle = Mathf.Tau * segment / SelectionRingSegments;
			var nextAngle = Mathf.Tau * (segment + 1) / SelectionRingSegments;
			var innerCurrent = new Vector3(Mathf.Cos(currentAngle) * innerRadius, 0.0f, Mathf.Sin(currentAngle) * innerRadius);
			var outerCurrent = new Vector3(Mathf.Cos(currentAngle) * outerRadius, 0.0f, Mathf.Sin(currentAngle) * outerRadius);
			var innerNext = new Vector3(Mathf.Cos(nextAngle) * innerRadius, 0.0f, Mathf.Sin(nextAngle) * innerRadius);
			var outerNext = new Vector3(Mathf.Cos(nextAngle) * outerRadius, 0.0f, Mathf.Sin(nextAngle) * outerRadius);

			AddSelectionRingTriangle(surfaceTool, innerCurrent, outerCurrent, outerNext);
			AddSelectionRingTriangle(surfaceTool, innerCurrent, outerNext, innerNext);
		}

		return surfaceTool.Commit();
	}

	private static void AddSelectionRingTriangle(SurfaceTool surfaceTool, Vector3 first, Vector3 second, Vector3 third)
	{
		surfaceTool.SetNormal(Vector3.Up);
		surfaceTool.AddVertex(first);
		surfaceTool.SetNormal(Vector3.Up);
		surfaceTool.AddVertex(second);
		surfaceTool.SetNormal(Vector3.Up);
		surfaceTool.AddVertex(third);
	}

	private static CharacterBody3D GetTargetBody(ISelectableWorldEntityController target)
	{
		return target?.SelectionBody;
	}

	private Vector3 ResolveSelectionRingPosition(CharacterBody3D body)
	{
		var fallbackPosition = ResolveSelectionRingFallbackPosition(body);
		var from = body.GlobalPosition + Vector3.Up * SelectionRingGroundRayStartHeight;
		var to = body.GlobalPosition - Vector3.Up * SelectionRingGroundRayLength;
		var query = new PhysicsRayQueryParameters3D
		{
			From = from,
			To = to,
			CollideWithBodies = true,
			CollideWithAreas = false,
		};

		query.Exclude.Add(body.GetRid());

		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (result.TryGetValue("position", out var hitPosition))
		{
			var groundPosition = hitPosition.AsVector3() + Vector3.Up * SelectionRingYOffset;
			if (groundPosition.Y <= body.GlobalPosition.Y + 0.1f)
			{
				return new Vector3(body.GlobalPosition.X, groundPosition.Y, body.GlobalPosition.Z);
			}
		}

		return fallbackPosition;
	}

	private static Vector3 ResolveSelectionRingFallbackPosition(CharacterBody3D body)
	{
		var collisionShape = body.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		if (collisionShape == null)
		{
			return body.GlobalPosition + Vector3.Up * SelectionRingYOffset;
		}

		return collisionShape.GlobalPosition - Vector3.Up * ResolveCollisionShapeHalfHeight(collisionShape.Shape) + Vector3.Up * SelectionRingYOffset;
	}

	private static float ResolveSelectionRingInnerRadius(CharacterBody3D body)
	{
		var collisionShape = body.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
		return Mathf.Max(SelectionRingDefaultInnerRadius, ResolveCollisionShapeRadius(collisionShape?.Shape) + 0.12f);
	}

	private static float ResolveCollisionShapeHalfHeight(Shape3D shape)
	{
		return shape switch
		{
			CapsuleShape3D capsule => capsule.Height * 0.5f,
			BoxShape3D box => box.Size.Y * 0.5f,
			CylinderShape3D cylinder => cylinder.Height * 0.5f,
			SphereShape3D sphere => sphere.Radius,
			_ => 0.0f,
		};
	}

	private static float ResolveCollisionShapeRadius(Shape3D shape)
	{
		return shape switch
		{
			CapsuleShape3D capsule => capsule.Radius,
			CylinderShape3D cylinder => cylinder.Radius,
			SphereShape3D sphere => sphere.Radius,
			BoxShape3D box => Mathf.Max(box.Size.X, box.Size.Z) * 0.5f,
			_ => SelectionRingDefaultInnerRadius,
		};
	}

	private static T FindControllerInHierarchy<T>(Node node) where T : class
	{
		var current = node;
		while (current != null)
		{
			if (current is T typedNode)
			{
				return typedNode;
			}

			current = current.GetParentOrNull<Node>();
		}

		return null;
	}

	public static void ResetStaticState()
	{
		LocalInstance = null;
	}

	protected override void OnCommonNodesReady()
	{
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/SpringArm3D/Camera3D");
		_playerModelRoot = GetNode<Node3D>(PlayerModelPath);
		EnsureAppearanceProfileApplied(force: true);
	}

	protected override void ApplyControllerConfigDefaults()
	{
		base.ApplyControllerConfigDefaults();
		RemotePlayerInterpolationSeconds = RemotePlayerSyncConfig.DefaultInterpolationSeconds;
		RemotePlayerMinInterpolationSeconds = RemotePlayerSyncConfig.MinInterpolationSeconds;
		RemotePlayerMaxInterpolationSeconds = RemotePlayerSyncConfig.MaxInterpolationSeconds;
		RemotePlayerSnapDistance = RemotePlayerSyncConfig.SnapDistance;
	}

	protected override void UpdateControllerState()
	{
		if (CharacterBody == null || _cameraPivot == null || _camera == null)
		{
			return;
		}

		EnsureAppearanceProfileApplied();
		if (Player == null)
		{
			return;
		}

		var isLocalPlayer = Player.IsLocalPlayer;
		CharacterBody.SetPhysicsProcess(isLocalPlayer);
		_cameraPivot.SetProcess(isLocalPlayer);
		_cameraPivot.SetProcessInput(isLocalPlayer);
		_cameraPivot.SetProcessUnhandledInput(isLocalPlayer);
		_camera.Current = isLocalPlayer;

		if (isLocalPlayer)
		{
			LocalInstance = this;
			SkillClientRuntime.Flush(this);
		}
		else if (ReferenceEquals(LocalInstance, this))
		{
			LocalInstance = null;
		}
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (_attackCooldownRemaining > 0f)
		{
			_attackCooldownRemaining -= (float)delta;
		}

		TickSkillCooldowns((float)delta);
		TickSkillCastState((float)delta);
		FlushPendingRemoteSkillAnimations();

		if (_isSkillCastPending)
		{
			_skillCastAckTimeoutRemaining -= (float)delta;
			if (_skillCastAckTimeoutRemaining <= 0.0f)
			{
				GD.PushWarning($"Skill cast ack timed out. skill={_pendingSkillId}, target={_pendingSkillTargetId}");
				CancelLocalSkillCast();
			}
		}

		if (SelectedTarget != null)
		{
			if (!IsSelectedTargetValid())
			{
				ClearSelection();
			}
			else
			{
				UpdateSelectionRingPosition();
			}
		}

		EnsureAppearanceProfileApplied();
		UpdateAnimationState((float)delta);
	}

	private void TickSkillCastState(float delta)
	{
		if (_skillAnimationLockRemaining > 0.0f)
		{
			_skillAnimationLockRemaining = Mathf.Max(0.0f, _skillAnimationLockRemaining - delta);
			if (_skillAnimationLockRemaining <= 0.0f)
			{
				ResetAnimationSpeed();
			}
		}

	}

	private void TickSkillCooldowns(float delta)
	{
		if (_globalSkillCooldownRemaining > 0.0f)
		{
			_globalSkillCooldownRemaining = Mathf.Max(0.0f, _globalSkillCooldownRemaining - delta);
		}

		if (_skillCooldowns.Count == 0)
		{
			return;
		}

		var skillIds = new List<int>(_skillCooldowns.Keys);
		foreach (var skillId in skillIds)
		{
			var remaining = _skillCooldowns[skillId] - delta;
			if (remaining <= 0.0f)
			{
				_skillCooldowns.Remove(skillId);
			}
			else
			{
				_skillCooldowns[skillId] = remaining;
			}
		}
	}

	protected override bool ShouldUseMovementFacing()
	{
		return Player != null && !Player.IsLocalPlayer && Player.UsePlanarRotation;
	}

	protected override float GetRemoteSnapDistance()
	{
		return Player != null && !Player.IsLocalPlayer ? RemotePlayerSnapDistance : base.GetRemoteSnapDistance();
	}

	protected override float GetDefaultRemoteInterpolationSeconds()
	{
		return Player != null && !Player.IsLocalPlayer ? RemotePlayerInterpolationSeconds : base.GetDefaultRemoteInterpolationSeconds();
	}

	protected override float GetMinRemoteInterpolationSeconds()
	{
		return Player != null && !Player.IsLocalPlayer ? RemotePlayerMinInterpolationSeconds : base.GetMinRemoteInterpolationSeconds();
	}

	protected override float GetMaxRemoteInterpolationSeconds()
	{
		return Player != null && !Player.IsLocalPlayer ? RemotePlayerMaxInterpolationSeconds : base.GetMaxRemoteInterpolationSeconds();
	}

	private void UpdateAnimationState(float delta)
	{
		if (_modelAnimationPlayer == null || CharacterBody == null || Player == null || _currentAnimationRuntimeSet == null)
		{
			return;
		}

		if (_attackCooldownRemaining > 0f || _skillAnimationLockRemaining > 0.0f)
		{
			return;
		}

		var nextState = ResolveAnimationState(delta);
		PlayAnimationForState(nextState);
	}

	private PlayerAnimationState ResolveAnimationState(float delta)
	{
		if (Player.IsLocalPlayer)
		{
			return ResolveLocalAnimationState();
		}

		return ResolveRemoteAnimationState(delta);
	}

	private PlayerAnimationState ResolveLocalAnimationState()
	{
		var movementVector = ResolveLocalMovementVector();
		if (!CharacterBody.IsOnFloor())
		{
			return ResolveAirborneState(
				movementVector == Vector2.Zero
					? PlayerAnimationState.JumpIdle
					: PlayerAnimationState.JumpMove
			);
		}

		ClearLatchedAirborneState();
		return ResolveDirectionalAnimationState(movementVector, PlayerAnimationState.Idle);
	}

	private PlayerAnimationState ResolveRemoteAnimationState(float delta)
	{
		var planarDelta = ResolveRemotePlanarDelta(delta);

		ClearLatchedAirborneState();
		var planarSpeed = planarDelta.Length() / Mathf.Max(delta, 0.0001f);
		if (planarSpeed <= AnimationMoveEpsilon)
		{
			return PlayerAnimationState.Idle;
		}

		var localPlanarDelta = CharacterBody.GlobalBasis.Inverse() * planarDelta;
		var movementVector = new Vector2(localPlanarDelta.X, localPlanarDelta.Z);
		return ResolveDirectionalAnimationState(movementVector, PlayerAnimationState.MoveForward);
	}

	private PlayerAnimationState ResolveDirectionalAnimationState(Vector2 movementVector, PlayerAnimationState idleFallback)
	{
		if (movementVector.LengthSquared() <= AnimationMoveEpsilon * AnimationMoveEpsilon)
		{
			return idleFallback;
		}

		var normalizedVector = movementVector.Normalized();
		if (normalizedVector.Y <= -DirectionSelectionThreshold)
		{
			return PlayerAnimationState.MoveForward;
		}

		if (normalizedVector.Y >= DirectionSelectionThreshold)
		{
			return PlayerAnimationState.MoveBackward;
		}

		return normalizedVector.X < 0.0f
			? PlayerAnimationState.StrafeLeft
			: PlayerAnimationState.StrafeRight;
	}

	private Vector2 ResolveLocalMovementVector()
	{
		var planarVelocity = CharacterBody.Velocity;
		planarVelocity.Y = 0.0f;
		if (planarVelocity.LengthSquared() <= AnimationMoveEpsilon * AnimationMoveEpsilon)
		{
			return Vector2.Zero;
		}

		var localPlanarVelocity = CharacterBody.GlobalBasis.Inverse() * planarVelocity;
		return new Vector2(localPlanarVelocity.X, localPlanarVelocity.Z);
	}

	private PlayerAnimationState ResolveAirborneState(PlayerAnimationState suggestedAirborneState)
	{
		if (_wasOnFloorLastFrame || !_hasLatchedAirborneState)
		{
			_latchedAirborneState = suggestedAirborneState;
			_hasLatchedAirborneState = true;
		}

		_wasOnFloorLastFrame = false;
		return _latchedAirborneState;
	}

	private void ClearLatchedAirborneState()
	{
		_wasOnFloorLastFrame = true;
		_hasLatchedAirborneState = false;
	}

	private Vector3 ResolveRemotePlanarDelta(float delta)
	{
		var currentPosition = CharacterBody.GlobalPosition;
		if (!_hasLastAnimationPosition || delta <= 0.0f)
		{
			_lastAnimationPosition = currentPosition;
			_hasLastAnimationPosition = true;
			return Vector3.Zero;
		}

		var displacement = currentPosition - _lastAnimationPosition;
		displacement.Y = 0.0f;
		_lastAnimationPosition = currentPosition;
		return displacement;
	}

	private void PlayAnimationForState(
		PlayerAnimationState state,
		bool force = false,
		float desiredDurationSeconds = 0.0f,
		double elapsedSeconds = 0.0d)
	{
		if (_modelAnimationPlayer == null || _currentAnimationRuntimeSet == null || !_currentAnimationRuntimeSet.StateAnimations.TryGetValue(state, out var animationKey))
		{
			return;
		}

		PlayAnimationByKey(animationKey, force, desiredDurationSeconds, state.ToString(), elapsedSeconds);
	}

	private void PlayAnimationByKey(
		string animationKey,
		bool force,
		float desiredDurationSeconds,
		string stateName,
		double elapsedSeconds = 0.0d)
	{
		if (_modelAnimationPlayer == null || string.IsNullOrWhiteSpace(animationKey))
		{
			return;
		}

		if (!force && string.Equals(_currentAnimationKey, animationKey, StringComparison.Ordinal))
		{
			return;
		}

		_currentAnimationStateName = string.IsNullOrWhiteSpace(stateName) ? animationKey : stateName;
		_currentAnimationKey = animationKey;
		ApplyAnimationSpeed(animationKey, desiredDurationSeconds);
		_modelAnimationPlayer.Play(animationKey);
		var animationStartOffset = ResolveAnimationStartOffset(animationKey, desiredDurationSeconds, elapsedSeconds);
		if (animationStartOffset > 0.0d)
		{
			_modelAnimationPlayer.Seek(animationStartOffset, update: true);
		}
	}

	private double ResolveAnimationStartOffset(string animationKey, float desiredDurationSeconds, double elapsedSeconds)
	{
		if (_modelAnimationPlayer == null
			|| string.IsNullOrWhiteSpace(animationKey)
			|| desiredDurationSeconds <= 0.0f
			|| elapsedSeconds <= 0.0d
			|| !_modelAnimationPlayer.HasAnimation(animationKey))
		{
			return 0.0d;
		}

		var animation = _modelAnimationPlayer.GetAnimation(animationKey);
		if (animation == null || animation.Length <= 0.0d)
		{
			return 0.0d;
		}

		var progress = ClampDouble(elapsedSeconds / desiredDurationSeconds, 0.0d, 0.98d);
		return animation.Length * progress;
	}

	private static double ClampDouble(double value, double min, double max)
	{
		if (value < min)
		{
			return min;
		}

		return value > max ? max : value;
	}

	private void ApplyAnimationSpeed(string animationKey, float desiredDurationSeconds)
	{
		if (_modelAnimationPlayer == null || desiredDurationSeconds <= 0.0f || !_modelAnimationPlayer.HasAnimation(animationKey))
		{
			ResetAnimationSpeed();
			return;
		}

		var animation = _modelAnimationPlayer.GetAnimation(animationKey);
		if (animation == null || animation.Length <= 0.0)
		{
			ResetAnimationSpeed();
			return;
		}

		_modelAnimationPlayer.SpeedScale = Mathf.Clamp((float)(animation.Length / desiredDurationSeconds), 0.2f, 4.0f);
		_activeTimedAnimationKey = animationKey;
	}

	private void ResetAnimationSpeed()
	{
		if (_modelAnimationPlayer != null)
		{
			_modelAnimationPlayer.SpeedScale = 1.0f;
		}

		_activeTimedAnimationKey = string.Empty;
	}

	private void EnsureAppearanceProfileApplied(bool force = false)
	{
		if (_playerModelRoot == null)
		{
			return;
		}

		var profile = ResolveAppearanceProfile();
		if (!force && _currentAppearanceProfile != null && _currentAppearanceProfile.ModelId == profile.ModelId)
		{
			return;
		}

		_currentAppearanceProfile = profile;
		_currentAnimationKey = string.Empty;
		_currentAnimationStateName = PlayerAnimationState.Idle.ToString();
		LoadModelInstance(profile);
		_currentAnimationRuntimeSet = EnsureAnimationRuntimeSetLoaded(profile);
		AttachAnimationLibrary(_currentAnimationRuntimeSet);
		PlayAnimationForState(PlayerAnimationState.Idle, force: true);
	}

	private PlayerAppearanceProfile ResolveAppearanceProfile()
	{
		if (Player != null && PlayerAppearanceConfigRepository.TryGetByModelId(Player.AppearanceModelId, out var profile))
		{
			return profile;
		}

		return PlayerAppearanceConfigRepository.GetDefaultProfile();
	}

	private void LoadModelInstance(PlayerAppearanceProfile profile)
	{
		foreach (Node child in _playerModelRoot.GetChildren())
		{
			_playerModelRoot.RemoveChild(child);
			child.QueueFree();
		}

		var modelScene = GD.Load<PackedScene>(profile.ModelScenePath);
		if (modelScene == null)
		{
			GD.PushWarning($"Player model scene not found: {profile.ModelScenePath}");
			_modelAnimationPlayer = null;
			return;
		}

		var instantiatedNode = modelScene.Instantiate<Node>();
		Node3D modelNode;
		if (instantiatedNode is Node3D instantiatedNode3D)
		{
			modelNode = instantiatedNode3D;
		}
		else
		{
			modelNode = new Node3D();
			modelNode.AddChild(instantiatedNode);
		}

		modelNode.Name = "ModelInstance";
		modelNode.Position = profile.ModelPositionVector;
		modelNode.RotationDegrees = profile.ModelRotationDegreesVector;
		modelNode.Scale = profile.ModelScaleVector;
		_playerModelRoot.AddChild(modelNode);
		_modelAnimationPlayer = FindAnimationPlayer(_playerModelRoot);
	}

	private void AttachAnimationLibrary(PlayerAnimationRuntimeSet runtimeSet)
	{
		if (_modelAnimationPlayer == null || runtimeSet == null)
		{
			return;
		}

		if (_modelAnimationPlayer.HasAnimationLibrary(RuntimeAnimationLibraryName))
		{
			_modelAnimationPlayer.RemoveAnimationLibrary(RuntimeAnimationLibraryName);
		}

		_modelAnimationPlayer.AddAnimationLibrary(
			RuntimeAnimationLibraryName,
			(AnimationLibrary)runtimeSet.AnimationLibrary.Duplicate(true)
		);
	}

	private static PlayerAnimationRuntimeSet EnsureAnimationRuntimeSetLoaded(PlayerAppearanceProfile profile)
	{
		if (s_sharedAnimationRuntimeSets.TryGetValue(profile.ModelId, out var runtimeSet))
		{
			return runtimeSet;
		}

		runtimeSet = new PlayerAnimationRuntimeSet();

		var dir = DirAccess.Open(profile.ResourceFolder);
		if (dir == null)
		{
			GD.PushWarning($"Player animation folder not found: {profile.ResourceFolder}");
			s_sharedAnimationRuntimeSets[profile.ModelId] = runtimeSet;
			return runtimeSet;
		}

		dir.ListDirBegin();
		while (true)
		{
			var fileName = dir.GetNext();
			if (string.IsNullOrEmpty(fileName))
			{
				break;
			}

			if (dir.CurrentIsDir() || !fileName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (string.Equals(fileName, profile.ModelSceneFile, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			LoadAnimationClip(profile, runtimeSet, fileName);
		}

		dir.ListDirEnd();
		ResolveStateAnimationMap(runtimeSet);
		s_sharedAnimationRuntimeSets[profile.ModelId] = runtimeSet;
		return runtimeSet;
	}

	private static void LoadAnimationClip(PlayerAppearanceProfile profile, PlayerAnimationRuntimeSet runtimeSet, string fileName)
	{
		var scenePath = $"{profile.ResourceFolder.TrimEnd('/')}/{fileName}";
		var animationScene = GD.Load<PackedScene>(scenePath);
		if (animationScene == null)
		{
			return;
		}

		var sceneRoot = animationScene.Instantiate<Node>();
		var sourceAnimationPlayer = FindAnimationPlayer(sceneRoot);
		if (sourceAnimationPlayer == null)
		{
			sceneRoot.QueueFree();
			return;
		}

		var animation = ExtractFirstAnimation(sourceAnimationPlayer);
		if (animation == null)
		{
			sceneRoot.QueueFree();
			return;
		}

		var fileStem = Path.GetFileNameWithoutExtension(fileName);
		var animationName = SanitizeAnimationName(fileStem);
		if (runtimeSet.AnimationLibrary.HasAnimation(animationName))
		{
			sceneRoot.QueueFree();
			return;
		}

		var duplicatedAnimation = (Animation)animation.Duplicate(true);
		ConfigureAnimationClip(duplicatedAnimation, fileStem);
		runtimeSet.AnimationLibrary.AddAnimation(animationName, duplicatedAnimation);
		runtimeSet.AnimationKeysByStem[fileStem] = $"{RuntimeAnimationLibraryName}/{animationName}";
		sceneRoot.QueueFree();
	}

	private static Animation ExtractFirstAnimation(AnimationPlayer animationPlayer)
	{
		foreach (var libraryName in animationPlayer.GetAnimationLibraryList())
		{
			var library = animationPlayer.GetAnimationLibrary(libraryName);
			if (library == null)
			{
				continue;
			}

			foreach (var animationName in library.GetAnimationList())
			{
				var animation = library.GetAnimation(animationName);
				if (animation != null)
				{
					return animation;
				}
			}
		}

		return null;
	}

	private static void ResolveStateAnimationMap(PlayerAnimationRuntimeSet runtimeSet)
	{
		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.Idle,
			"idle",
			"idle_2",
			"idle_3",
			"idle_4",
			"idle_5");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.MoveForward,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.MoveBackward,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.StrafeLeft,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.StrafeRight,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.JumpIdle,
			"jump_2",
			"jump_attack",
			"jump");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.JumpMove,
			"jump",
			"jump_attack",
			"jump_2");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.Attack,
			"attack",
			"slash",
			"slash_2",
			"slash_3",
			"kick",
			"kick_2",
			"slide_attack",
			"high_spin_attack",
			"spell_cast");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.HeavyAttack,
			"high_spin_attack",
			"slash_4",
			"slash_5",
			"slide_attack",
			"jump_attack",
			"attack");

		TryAssignStateAnimation(runtimeSet, PlayerAnimationState.Cast,
			"spell_cast",
			"cast",
			"power_up",
			"attack");
	}

	private static void TryAssignStateAnimation(PlayerAnimationRuntimeSet runtimeSet, PlayerAnimationState state, params string[] preferredFileStems)
	{
		if (runtimeSet == null)
		{
			return;
		}

		foreach (var preferredFileStem in preferredFileStems)
		{
			if (!runtimeSet.AnimationKeysByStem.TryGetValue(preferredFileStem, out var animationKey))
			{
				continue;
			}

			runtimeSet.StateAnimations[state] = animationKey;
			return;
		}
	}

	private static void ConfigureAnimationClip(Animation animation, string fileStem)
	{
		var lowerStem = fileStem.ToLowerInvariant();
		if (lowerStem.Contains("idle", StringComparison.Ordinal)
			|| lowerStem.Contains("walk", StringComparison.Ordinal)
			|| lowerStem.Contains("run", StringComparison.Ordinal)
			|| lowerStem.Contains("strafe", StringComparison.Ordinal)
			|| lowerStem.Contains("turn", StringComparison.Ordinal))
		{
			animation.LoopMode = Animation.LoopModeEnum.Linear;
		}

		if (lowerStem.Contains("walk", StringComparison.Ordinal)
			|| lowerStem.Contains("run", StringComparison.Ordinal)
			|| lowerStem.Contains("strafe", StringComparison.Ordinal)
			|| lowerStem.Contains("jump", StringComparison.Ordinal))
		{
			StripRootMotion(animation);
		}
	}

	private static string SanitizeAnimationName(string value)
	{
		return value
			.ToLowerInvariant()
			.Replace("(", string.Empty)
			.Replace(")", string.Empty)
			.Replace(" ", "_")
			.Replace("-", "_");
	}

	private static AnimationPlayer FindAnimationPlayer(Node node)
	{
		if (node is AnimationPlayer animationPlayer)
		{
			return animationPlayer;
		}

		foreach (Node child in node.GetChildren())
		{
			var nestedAnimationPlayer = FindAnimationPlayer(child);
			if (nestedAnimationPlayer != null)
			{
				return nestedAnimationPlayer;
			}
		}

		return null;
	}

	private static void StripRootMotion(Animation animation)
	{
		for (var trackIndex = animation.GetTrackCount() - 1; trackIndex >= 0; trackIndex--)
		{
			var trackType = animation.TrackGetType(trackIndex);
			if (trackType != Animation.TrackType.Position3D
				&& trackType != Animation.TrackType.Rotation3D)
			{
				continue;
			}

			var trackPath = animation.TrackGetPath(trackIndex).ToString();
			if (!ShouldStripRootMotionTrack(trackPath))
			{
				continue;
			}

			animation.RemoveTrack(trackIndex);
		}
	}

	private static bool ShouldStripRootMotionTrack(string trackPath)
	{
		return trackPath.Contains("Hips", StringComparison.OrdinalIgnoreCase)
			|| trackPath.Contains("Root", StringComparison.OrdinalIgnoreCase);
	}

}
