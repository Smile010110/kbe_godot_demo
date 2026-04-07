using Godot;

public abstract partial class WorldEntityControllerBase<TEntity> : Node3D, IWorldEntityController<TEntity>
	where TEntity : class, IWorldEntityView
{
	[Export]
	public float MoveSpeed = 5.0f;

	protected TEntity EntityView { get; private set; }
	protected CharacterBody3D CharacterBody { get; private set; }
	protected Label3D NameLabel { get; private set; }
	protected Label3D InfoLabel { get; private set; }

	private Vector3 _targetPosition;
	private bool _isReady;
	private bool _hasInitialTransform;

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
		_targetPosition = entityPosition;
		CharacterBody.GlobalRotationDegrees = EntityView.WorldRotationDegrees;

		if (EntityView.IsLocallyControlled || !_hasInitialTransform)
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
		var nextPosition = currentPosition.MoveToward(_targetPosition, (float)(MoveSpeed * delta));
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
