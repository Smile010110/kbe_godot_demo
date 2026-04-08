using Godot;
using KBEngine;

public static class WorldEntityRotationMapping
{
	private const float YawOffsetDegrees = 180.0f;

	public static Vector3 ToGodotRotationDegrees(KBVector3 kbeDirection)
	{
		return new Vector3(
			kbeDirection.y,
			kbeDirection.z - YawOffsetDegrees,
			kbeDirection.x
		);
	}

	public static KBVector3 ToKbeDirection(Vector3 godotRotationDegrees)
	{
		return new KBVector3(
			godotRotationDegrees.Z,
			godotRotationDegrees.X,
			godotRotationDegrees.Y + YawOffsetDegrees
		);
	}
}
