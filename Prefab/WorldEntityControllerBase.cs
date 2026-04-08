using Godot;

public abstract partial class WorldEntityControllerBase<TEntity> : Node3D, IWorldEntityController<TEntity>
	where TEntity : class, IWorldEntityView
{
	[Export]
	public float MoveSpeed = 5.0f;
	[Export]
	public float RemoteInterpolationSeconds = 0.1f;
	[Export]
	public float RemoteSnapDistance = 1.5f;

	protected TEntity EntityView { get; private set; }
	protected CharacterBody3D CharacterBody { get; private set; }
	protected Label3D NameLabel { get; private set; }
	protected Label3D InfoLabel { get; private set; }

	private Vector3 _targetPosition;
	private bool _isReady;
	private bool _hasInitialTransform;
	private ulong _lastTargetUpdateTimeMs;
	private float _targetInterpolationSeconds = 0.1f;
	private const float MovementFacingEpsilonSquared = 0.0001f;

	protected abstract string CharacterBodyPath { get; }
	protected abstract string NameLabelPath { get; }
	protected abstract string InfoLabelPath { get; }

	public override void _Ready()
	{
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

		if (EntityView.IsLocallyControlled || !_hasInitialTransform || distanceToTarget >= RemoteSnapDistance)
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
			_targetInterpolationSeconds = RemoteInterpolationSeconds;
		}
		else
		{
			var elapsedSeconds = (nowMs - _lastTargetUpdateTimeMs) / 1000.0f;
			_targetInterpolationSeconds = Mathf.Clamp(elapsedSeconds, 0.03f, 0.2f);
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

	protected virtual void UpdateControllerState()
	{
	}
}
