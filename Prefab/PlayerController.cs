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
	}

	private const string PlayerModelPath = "PlayerCharacterBody3D/PlayerModel";
	private const string RuntimeAnimationLibraryName = "player_runtime";
	private const float AnimationMoveEpsilon = 0.05f;
	private const float DirectionSelectionThreshold = 0.35f;
	private const float SelectionRayLength = 1000.0f;
	private const float SelectionRingYOffset = 0.08f;

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

	public static PlayerController LocalInstance { get; private set; }

	public Player Player => EntityView;
	public string CurrentAnimationKey => _currentAnimationKey;
	public string CurrentAnimationStateName => _currentAnimationStateName;
	public MonsterController SelectedTarget { get; private set; }

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
	private MeshInstance3D _selectionRing;

	protected override string CharacterBodyPath => "PlayerCharacterBody3D";
	protected override string NameLabelPath => "PlayerCharacterBody3D/HeadInfo/NameLabel";
	protected override string InfoLabelPath => "PlayerCharacterBody3D/HeadInfo/HPLabel";

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
		if (@event is InputEventMouseButton mouseBtn
			&& mouseBtn.ButtonIndex == MouseButton.Left
			&& mouseBtn.Pressed)
		{
			GD.Print($"[TargetSelect] RAW LEFT CLICK — Player={Player != null} IsLocal={Player?.IsLocalPlayer} Camera={_camera != null} InTree={IsInsideTree()}");
		}

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
			GD.Print($"[TargetSelect] Guards passed, searching at {mouseButton.Position}...");
			var monsterController = FindMonsterAtScreenPosition(mouseButton.Position);
			if (monsterController == null)
			{
				GD.Print("[TargetSelect] No monster found, clearing selection.");
				ClearSelection();
				return;
			}

			if (ReferenceEquals(monsterController, SelectedTarget))
			{
				GD.Print("[TargetSelect] Same target already selected.");
				return;
			}

			ClearSelection();
			SelectTarget(monsterController);
		}
	}

	private void SelectTarget(MonsterController target)
	{
		SelectedTarget = target;
		_selectionRing = CreateSelectionRing();
		_selectionRing.Name = "SelectionRing";

		World.Instance.AddChild(_selectionRing);
		UpdateSelectionRingPosition();

		GD.Print($"[TargetSelect] Selected monster: {target.Name}");
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

	private void UpdateSelectionRingPosition()
	{
		if (_selectionRing == null || !IsInstanceValid(_selectionRing) || SelectedTarget == null)
		{
			return;
		}

		var body = SelectedTarget.GetNodeOrNull<CharacterBody3D>("MonsterCharacterBody3D");
		if (body == null)
		{
			return;
		}

		var bodyPos = body.GlobalPosition;
		_selectionRing.GlobalPosition = new Vector3(bodyPos.X, bodyPos.Y - 0.98f, bodyPos.Z);
		_selectionRing.GlobalRotationDegrees = new Vector3(90f, 0f, 0f);
	}

	public void TryAttack()
	{
		if (Player == null || !Player.IsLocalPlayer)
		{
			return;
		}

		if (SelectedTarget == null || !IsInstanceValid(SelectedTarget))
		{
			ClearSelection();
			return;
		}

		if (_attackCooldownRemaining > 0f)
		{
			return;
		}

		var targetEntityId = SelectedTarget.Monster?.EntityId ?? -1;
		if (targetEntityId <= 0)
		{
			return;
		}

		Player.AttackTarget(targetEntityId);
		_attackCooldownRemaining = AttackCooldownSeconds;
		PlayAnimationForState(PlayerAnimationState.Attack, force: true);
	}

	private MonsterController FindMonsterAtScreenPosition(Vector2 screenPosition)
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

		if (result.TryGetValue("position", out var hitPos))
		{
			GD.Print($"[TargetSelect] Ray hit: {colliderObj.As<Node>().Name} at {hitPos.AsVector3()}");
		}

		var hitNode = colliderObj.As<Node>();
		var controller = FindControllerInHierarchy<MonsterController>(hitNode);
		if (controller == null)
		{
			GD.Print($"[TargetSelect] No MonsterController found in hierarchy of {hitNode.Name}");
		}

		return controller;
	}

	private static MeshInstance3D CreateSelectionRing()
	{
		var torusMesh = new TorusMesh
		{
			InnerRadius = 0.35f,
			OuterRadius = 0.45f,
		};

		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.15f, 0.15f, 1.0f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnergyMultiplier = 2.0f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			DisableReceiveShadows = true,
		};

		var ring = new MeshInstance3D
		{
			Mesh = torusMesh,
			MaterialOverride = material,
			RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
		};

		return ring;
	}

	private static T FindControllerInHierarchy<T>(Node node) where T : Node
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
		GD.Print($"[TargetSelect] OnCommonNodesReady: _camera={_camera != null} _cameraPivot={_cameraPivot != null}");
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
			GD.Print($"[TargetSelect] LocalInstance set. Camera active: {_camera.Current}");
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

		if (SelectedTarget != null)
		{
			if (!IsInstanceValid(SelectedTarget))
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

		if (_attackCooldownRemaining > 0f)
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
		if (!CharacterBody.IsOnFloor())
		{
			return ResolveAirborneState(
				planarDelta.LengthSquared() <= AnimationMoveEpsilon * AnimationMoveEpsilon
					? PlayerAnimationState.JumpIdle
					: PlayerAnimationState.JumpMove
			);
		}

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

	private void PlayAnimationForState(PlayerAnimationState state, bool force = false)
	{
		if (_modelAnimationPlayer == null || _currentAnimationRuntimeSet == null || !_currentAnimationRuntimeSet.StateAnimations.TryGetValue(state, out var animationKey))
		{
			return;
		}

		if (!force && string.Equals(_currentAnimationKey, animationKey, StringComparison.Ordinal))
		{
			return;
		}

		_currentAnimationStateName = state.ToString();
		_currentAnimationKey = animationKey;
		_modelAnimationPlayer.Play(animationKey);
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
