using Godot;

public abstract partial class WorldEntityControllerBase<TEntity> : Node3D, IWorldEntityController<TEntity>
	, ISelectableWorldEntityController
	, ISkillCastPresentationController
	where TEntity : class, IWorldEntityView
{
	[Export]
	public float MoveSpeed = 5.0f;
	[Export]
	public float RemoteInterpolationSeconds = 0.1f;
	[Export]
	public float RemoteMinInterpolationSeconds = 0.03f;
	[Export]
	public float RemoteMaxInterpolationSeconds = 0.2f;
	[Export]
	public float RemoteSnapDistance = 1.5f;

	private const string HealthBarRootName = "RuntimeHealthBar";
	private const float HealthBarWidth = 0.92f;
	private const float HealthBarHeight = 0.085f;
	private const float HealthBarBorder = 0.018f;
	private const float HealthBarYOffset = 1.40f;
	private const float HealthBarFillZOffset = 0.01f;
	private static readonly Color LocalHealthBarColor = new(0.16f, 0.95f, 0.22f, 1.0f);
	private static readonly Color OtherHealthBarColor = new(1.0f, 0.12f, 0.10f, 1.0f);
	private static readonly Color HealthBarBackgroundColor = new(0.03f, 0.03f, 0.03f, 1.0f);
	private static readonly Color EmptyHealthBarColor = new(0.18f, 0.18f, 0.18f, 0.85f);

	protected TEntity EntityView { get; private set; }
	protected CharacterBody3D CharacterBody { get; private set; }
	protected Label3D NameLabel { get; private set; }
	protected Label3D InfoLabel { get; private set; }
	public int SelectedEntityId => EntityView?.EntityId ?? -1;
	public IWorldEntityView SelectedEntityView => EntityView;
	public CharacterBody3D SelectionBody => CharacterBody;

	private Node3D _healthBarRoot;
	private MeshInstance3D _healthBarFill;
	private StandardMaterial3D _healthBarFillMaterial;
	private Vector3 _targetPosition;
	private bool _isReady;
	private bool _hasInitialTransform;
	private ulong _lastTargetUpdateTimeMs;
	private float _targetInterpolationSeconds = 0.1f;
	private bool _hasPresentedHeadInfo;
	private ulong _presentedHitPoints;
	private ulong _presentedMaxHitPoints;
	private ulong _presentedManaPoints;
	private int _presentedActiveBuffCount;
	private byte _presentedRawMoveSpeed;
	private const float MovementFacingEpsilonSquared = 0.0001f;

	protected abstract string CharacterBodyPath { get; }
	protected abstract string NameLabelPath { get; }
	protected abstract string InfoLabelPath { get; }

	public override void _Ready()
	{
		ApplyControllerConfigDefaults();
		CharacterBody = GetNode<CharacterBody3D>(CharacterBodyPath);
		NameLabel = GetNode<Label3D>(NameLabelPath);
		InfoLabel = GetNode<Label3D>(InfoLabelPath);
		_targetPosition = CharacterBody.GlobalPosition;
		_isReady = true;

		OnCommonNodesReady();

		if (EntityView != null)
		{
			RefreshPresentation();
		}
	}

	public virtual void BindEntity(TEntity entity)
	{
		EntityView = entity;

		if (_isReady)
		{
			RefreshPresentation();
		}
	}

	public virtual int GetStatus()
	{
		return EntityView != null && EntityView.IsLocallyControlled ? 0 : -1;
	}

	public virtual bool TryPlaySkillCastAnimation(SkillCastResult skillCast, double elapsedSeconds)
	{
		return CharacterBody != null;
	}

	public virtual float GetMoveSpeed()
	{
		if (EntityView == null)
		{
			return MoveSpeed;
		}

		return EntityView.MoveSpeedUnits;
	}

	public virtual void SetHeadInfo()
	{
		if (EntityView == null || NameLabel == null || InfoLabel == null)
		{
			return;
		}

		MoveSpeed = GetMoveSpeed();
		NameLabel.Text = ResolveNameLabelText();
		NameLabel.Modulate = WorldEntityNameplateStyleResolver.ResolveColor(EntityView);
		InfoLabel.Text = EntityView.SecondaryInfoText;
		InfoLabel.Visible = EntityView.ShowSecondaryInfo;
		UpdateHealthBar();
		RecordPresentedHeadInfo();
	}

	public virtual void UpdateFromEntity()
	{
		if (EntityView == null || CharacterBody == null)
		{
			return;
		}

		var entityPosition = EntityView.WorldPosition;
		var distanceToTarget = CharacterBody.GlobalPosition.DistanceTo(entityPosition);
		_targetPosition = entityPosition;
		CharacterBody.GlobalRotationDegrees = ResolveAppliedRotationDegrees();
		RecordTargetUpdate();

		if (EntityView.IsLocallyControlled || !_hasInitialTransform || distanceToTarget >= GetRemoteSnapDistance())
		{
			CharacterBody.GlobalPosition = entityPosition;
			_hasInitialTransform = true;
		}
	}

	public override void _Process(double delta)
	{
		if (EntityView == null || CharacterBody == null)
		{
			return;
		}

		RefreshHeadInfoIfStatsChanged();

		if (EntityView.IsLocallyControlled)
		{
			return;
		}

		var currentPosition = CharacterBody.GlobalTransform.Origin;
		ApplyMovementFacing(currentPosition);
		var interpolationSeconds = Mathf.Max(0.01f, _targetInterpolationSeconds);
		var stepDistance = currentPosition.DistanceTo(_targetPosition) * ((float)delta / interpolationSeconds);
		var nextPosition = currentPosition.MoveToward(_targetPosition, stepDistance);
		CharacterBody.GlobalTransform = new Transform3D(CharacterBody.GlobalTransform.Basis, nextPosition);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (EntityView == null || CharacterBody == null)
		{
			return;
		}

		if (EntityView is ILocallyControlledWorldEntity localEntity)
		{
			localEntity.ApplyLocalTransform(CharacterBody.GlobalPosition, CharacterBody.GlobalRotationDegrees);
		}
	}

	protected virtual string ResolveNameLabelText()
	{
		return EntityView?.DisplayName ?? string.Empty;
	}

	protected virtual Vector3 ResolveAppliedRotationDegrees()
	{
		if (EntityView == null)
		{
			return Vector3.Zero;
		}

		var rotationDegrees = EntityView.WorldRotationDegrees;
		if (!EntityView.UsePlanarRotation)
		{
			return rotationDegrees;
		}

		return new Vector3(0.0f, rotationDegrees.Y, 0.0f);
	}

	protected virtual bool ShouldUseMovementFacing()
	{
		return EntityView is IServerDrivenWorldEntity && EntityView.UsePlanarRotation;
	}

	protected virtual float GetRemoteSnapDistance()
	{
		return RemoteSnapDistance;
	}

	protected virtual float GetDefaultRemoteInterpolationSeconds()
	{
		return RemoteInterpolationSeconds;
	}

	protected virtual float GetMinRemoteInterpolationSeconds()
	{
		return RemoteMinInterpolationSeconds;
	}

	protected virtual float GetMaxRemoteInterpolationSeconds()
	{
		return RemoteMaxInterpolationSeconds;
	}

	protected virtual void ApplyMovementFacing(Vector3 currentPosition)
	{
		if (!ShouldUseMovementFacing() || CharacterBody == null)
		{
			return;
		}

		var planarDelta = _targetPosition - currentPosition;
		planarDelta.Y = 0.0f;
		if (planarDelta.LengthSquared() <= MovementFacingEpsilonSquared)
		{
			return;
		}

		var yawDegrees = Mathf.RadToDeg(Mathf.Atan2(-planarDelta.X, -planarDelta.Z));
		CharacterBody.GlobalRotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f);
	}

	protected virtual void RecordTargetUpdate()
	{
		var nowMs = Time.GetTicksMsec();
		if (_lastTargetUpdateTimeMs == 0)
		{
			_targetInterpolationSeconds = GetDefaultRemoteInterpolationSeconds();
		}
		else
		{
			var elapsedSeconds = (nowMs - _lastTargetUpdateTimeMs) / 1000.0f;
			_targetInterpolationSeconds = Mathf.Clamp(
				elapsedSeconds,
				GetMinRemoteInterpolationSeconds(),
				GetMaxRemoteInterpolationSeconds()
			);
		}

		_lastTargetUpdateTimeMs = nowMs;
	}

	protected virtual void RefreshPresentation()
	{
		UpdateControllerState();
		UpdateFromEntity();
		SetHeadInfo();
	}

	protected virtual void OnCommonNodesReady()
	{
	}

	protected virtual void ApplyControllerConfigDefaults()
	{
		RemoteInterpolationSeconds = RemoteEntitySyncConfig.DefaultInterpolationSeconds;
		RemoteMinInterpolationSeconds = RemoteEntitySyncConfig.MinInterpolationSeconds;
		RemoteMaxInterpolationSeconds = RemoteEntitySyncConfig.MaxInterpolationSeconds;
		RemoteSnapDistance = RemoteEntitySyncConfig.SnapDistance;
	}

	protected virtual void UpdateControllerState()
	{
	}

	private void RefreshHeadInfoIfStatsChanged()
	{
		if (EntityView == null)
		{
			return;
		}

		if (!_hasPresentedHeadInfo
			|| _presentedHitPoints != EntityView.HitPoints
			|| _presentedMaxHitPoints != EntityView.MaxHitPoints
			|| _presentedManaPoints != EntityView.ManaPoints
			|| _presentedActiveBuffCount != EntityView.ActiveBuffCount
			|| _presentedRawMoveSpeed != EntityView.RawMoveSpeed)
		{
			SetHeadInfo();
		}
	}

	private void RecordPresentedHeadInfo()
	{
		if (EntityView == null)
		{
			_hasPresentedHeadInfo = false;
			return;
		}

		_presentedHitPoints = EntityView.HitPoints;
		_presentedMaxHitPoints = EntityView.MaxHitPoints;
		_presentedManaPoints = EntityView.ManaPoints;
		_presentedActiveBuffCount = EntityView.ActiveBuffCount;
		_presentedRawMoveSpeed = EntityView.RawMoveSpeed;
		_hasPresentedHeadInfo = true;
	}

	private void UpdateHealthBar()
	{
		if (EntityView == null || NameLabel == null)
		{
			SetHealthBarVisible(false);
			return;
		}

		var maxHitPoints = EntityView.MaxHitPoints;
		if (maxHitPoints == 0UL)
		{
			SetHealthBarVisible(false);
			return;
		}

		EnsureHealthBar();
		if (_healthBarRoot == null || _healthBarFill == null)
		{
			return;
		}

		_healthBarRoot.Visible = true;
		var ratio = Mathf.Clamp((float)((double)EntityView.HitPoints / maxHitPoints), 0.0f, 1.0f);
		var displayRatio = Mathf.Max(ratio, 0.01f);
		_healthBarFill.Visible = true;
		_healthBarFill.Scale = new Vector3(displayRatio, 1.0f, 1.0f);
		_healthBarFill.Position = new Vector3((displayRatio - 1.0f) * HealthBarWidth * 0.5f, 0.0f, HealthBarFillZOffset);
		_healthBarFillMaterial.AlbedoColor = ratio <= 0.0f
			? EmptyHealthBarColor
			: EntityView.IsLocallyControlled
			? LocalHealthBarColor
			: OtherHealthBarColor;
		_healthBarFillMaterial.Emission = _healthBarFillMaterial.AlbedoColor;
	}

	private void SetHealthBarVisible(bool visible)
	{
		if (_healthBarRoot != null && IsInstanceValid(_healthBarRoot))
		{
			_healthBarRoot.Visible = visible;
		}
	}

	private void EnsureHealthBar()
	{
		if (_healthBarRoot != null && IsInstanceValid(_healthBarRoot))
		{
			return;
		}

		var headInfo = NameLabel?.GetParentOrNull<Node3D>();
		if (headInfo == null)
		{
			return;
		}

		_healthBarRoot = new Node3D
		{
			Name = HealthBarRootName,
			Position = new Vector3(0.0f, HealthBarYOffset, 0.0f),
		};

		var background = CreateHealthBarQuad(
			"Background",
			HealthBarWidth + HealthBarBorder * 2.0f,
			HealthBarHeight + HealthBarBorder * 2.0f,
			HealthBarBackgroundColor,
			renderPriority: 0);
		_healthBarFillMaterial = CreateHealthBarMaterial(LocalHealthBarColor, renderPriority: 1);
		_healthBarFill = CreateHealthBarQuad("Fill", HealthBarWidth, HealthBarHeight, LocalHealthBarColor, _healthBarFillMaterial);
		_healthBarFill.Position = new Vector3(0.0f, 0.0f, HealthBarFillZOffset);

		_healthBarRoot.AddChild(background);
		_healthBarRoot.AddChild(_healthBarFill);
		headInfo.AddChild(_healthBarRoot);
	}

	private static MeshInstance3D CreateHealthBarQuad(string name, float width, float height, Color color, StandardMaterial3D material = null, int renderPriority = 0)
	{
		return new MeshInstance3D
		{
			Name = name,
			Mesh = new QuadMesh { Size = new Vector2(width, height) },
			MaterialOverride = material ?? CreateHealthBarMaterial(color, renderPriority),
		};
	}

	private static StandardMaterial3D CreateHealthBarMaterial(Color color, int renderPriority)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = 0.8f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			DisableReceiveShadows = true,
			RenderPriority = renderPriority,
		};
	}
}
