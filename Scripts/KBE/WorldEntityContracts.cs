using Godot;

public interface IWorldEntityView
{
	WorldEntityKind EntityKind { get; }
	bool IsTeammate { get; }
	string DisplayName { get; }
	string SecondaryInfoText { get; }
	bool ShowSecondaryInfo { get; }
	ulong HitPoints { get; }
	ulong ManaPoints { get; }
	uint Attack { get; }
	uint Defense { get; }
	byte RawMoveSpeed { get; }
	float MoveSpeedUnits { get; }
	Vector3 WorldPosition { get; }
	Vector3 WorldRotationDegrees { get; }
	bool UsePlanarRotation { get; }
	bool IsLocallyControlled { get; }
}

public interface ILocallyControlledWorldEntity : IWorldEntityView
{
	void ApplyLocalTransform(Vector3 worldPosition, Vector3 worldRotationDegrees);
}

public interface IServerDrivenWorldEntity : IWorldEntityView
{
}

public interface IWorldEntityRenderHooks
{
	void RefreshRenderInfo();
	void RefreshRenderTransform();
}

public interface IWorldEntityController<TEntity> where TEntity : class
{
	void BindEntity(TEntity entity);
	void SetHeadInfo();
	void UpdateFromEntity();
}

public interface ISelectableWorldEntityController
{
	IWorldEntityView SelectedEntityView { get; }
	CharacterBody3D SelectionBody { get; }
}
