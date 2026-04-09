using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class PlayerController : WorldEntityControllerBase<Player>
{
	private enum PlayerAnimationState
	{
		Idle,
		MoveForward,
		MoveBackward,
		StrafeLeft,
		StrafeRight,
		JumpIdle,
		JumpMove,
	}

	private const string PlayerModelPath = "PlayerCharacterBody3D/PlayerModel";
	private const string PlayerAnimationFolderPath = "res://Res/Player/player_1";
	private const string PlayerBaseModelFileName = "model.fbx";
	private const string RuntimeAnimationLibraryName = "player_runtime";
	private const float AnimationMoveEpsilon = 0.05f;
	private const float DirectionSelectionThreshold = 0.35f;

	private static AnimationLibrary s_sharedAnimationLibrary;
	private static Dictionary<PlayerAnimationState, string> s_sharedStateAnimations;
	private static Dictionary<string, string> s_sharedAnimationKeysByStem;

	[Export]
	public float RemotePlayerInterpolationSeconds = 0.06f;
	[Export]
	public float RemotePlayerMinInterpolationSeconds = 0.02f;
	[Export]
	public float RemotePlayerMaxInterpolationSeconds = 0.12f;
	[Export]
	public float RemotePlayerSnapDistance = 0.9f;

	public static PlayerController LocalInstance { get; private set; }

	public Player Player => EntityView;
	public string CurrentAnimationKey => _currentAnimationKey;
	public string CurrentAnimationStateName => _currentAnimationStateName;

	private Node3D _cameraPivot;
	private Camera3D _camera;
	private Node3D _playerModel;
	private AnimationPlayer _modelAnimationPlayer;
	private Vector3 _lastAnimationPosition;
	private bool _hasLastAnimationPosition;
	private string _currentAnimationKey = string.Empty;
	private string _currentAnimationStateName = PlayerAnimationState.Idle.ToString();
	private bool _wasOnFloorLastFrame = true;
	private bool _hasLatchedAirborneState;
	private PlayerAnimationState _latchedAirborneState = PlayerAnimationState.JumpIdle;

	protected override string CharacterBodyPath => "PlayerCharacterBody3D";
	protected override string NameLabelPath => "PlayerCharacterBody3D/HeadInfo/NameLabel";
	protected override string InfoLabelPath => "PlayerCharacterBody3D/HeadInfo/HPLabel";

	public override void _ExitTree()
	{
		if (ReferenceEquals(LocalInstance, this))
		{
			LocalInstance = null;
		}

		base._ExitTree();
	}

	public void BindPlayer(Player player)
	{
		BindEntity(player);
	}

	public static void ResetStaticState()
	{
		LocalInstance = null;
	}

	protected override void OnCommonNodesReady()
	{
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/SpringArm3D/Camera3D");
		_playerModel = GetNode<Node3D>(PlayerModelPath);
		_modelAnimationPlayer = FindAnimationPlayer(_playerModel);
		EnsureAnimationLibraryLoaded();
		AttachAnimationLibrary();
		PlayAnimationForState(PlayerAnimationState.Idle, force: true);
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
		if (Player == null || CharacterBody == null || _cameraPivot == null || _camera == null)
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
		}
		else if (ReferenceEquals(LocalInstance, this))
		{
			LocalInstance = null;
		}
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
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
		if (_modelAnimationPlayer == null || CharacterBody == null || Player == null)
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
		if (_modelAnimationPlayer == null || s_sharedStateAnimations == null || !s_sharedStateAnimations.TryGetValue(state, out var animationKey))
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

	private static bool HasAnimationForState(PlayerAnimationState state)
	{
		return s_sharedStateAnimations != null && s_sharedStateAnimations.ContainsKey(state);
	}

	private void AttachAnimationLibrary()
	{
		if (_modelAnimationPlayer == null || s_sharedAnimationLibrary == null)
		{
			return;
		}

		if (_modelAnimationPlayer.HasAnimationLibrary(RuntimeAnimationLibraryName))
		{
			return;
		}

		_modelAnimationPlayer.AddAnimationLibrary(
			RuntimeAnimationLibraryName,
			(AnimationLibrary)s_sharedAnimationLibrary.Duplicate(true)
		);
	}

	private static void EnsureAnimationLibraryLoaded()
	{
		if (s_sharedAnimationLibrary != null && s_sharedStateAnimations != null)
		{
			return;
		}

		s_sharedAnimationLibrary = new AnimationLibrary();
		s_sharedStateAnimations = new Dictionary<PlayerAnimationState, string>();
		s_sharedAnimationKeysByStem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		var dir = DirAccess.Open(PlayerAnimationFolderPath);
		if (dir == null)
		{
			GD.PushWarning($"Player animation folder not found: {PlayerAnimationFolderPath}");
			return;
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

			if (string.Equals(fileName, PlayerBaseModelFileName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			LoadAnimationClip(fileName);
		}

		dir.ListDirEnd();
		ResolveStateAnimationMap();
	}

	private static void LoadAnimationClip(string fileName)
	{
		var scenePath = $"{PlayerAnimationFolderPath}/{fileName}";
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
		if (s_sharedAnimationLibrary.HasAnimation(animationName))
		{
			sceneRoot.QueueFree();
			return;
		}

		var duplicatedAnimation = (Animation)animation.Duplicate(true);
		ConfigureAnimationClip(duplicatedAnimation, fileStem);
		s_sharedAnimationLibrary.AddAnimation(animationName, duplicatedAnimation);
		s_sharedAnimationKeysByStem[fileStem] = $"{RuntimeAnimationLibraryName}/{animationName}";
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

	private static void ResolveStateAnimationMap()
	{
		TryAssignStateAnimation(PlayerAnimationState.Idle,
			"idle",
			"idle_2",
			"idle_3",
			"idle_4",
			"idle_5");

		TryAssignStateAnimation(PlayerAnimationState.MoveForward,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(PlayerAnimationState.MoveBackward,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(PlayerAnimationState.StrafeLeft,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(PlayerAnimationState.StrafeRight,
			"walk",
			"walk_2",
			"run",
			"run_2");

		TryAssignStateAnimation(PlayerAnimationState.JumpIdle,
			"jump_2",
			"jump_attack",
			"jump");

		TryAssignStateAnimation(PlayerAnimationState.JumpMove,
			"jump",
			"jump_attack",
			"jump_2");
	}

	private static void TryAssignStateAnimation(PlayerAnimationState state, params string[] preferredFileStems)
	{
		if (s_sharedAnimationKeysByStem == null)
		{
			return;
		}

		foreach (var preferredFileStem in preferredFileStems)
		{
			if (!s_sharedAnimationKeysByStem.TryGetValue(preferredFileStem, out var animationKey))
			{
				continue;
			}

			s_sharedStateAnimations[state] = animationKey;
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
