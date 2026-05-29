using Godot;

public static class WorldEntityRotationMapping
{
	private const float YawOffsetDegrees = 180.0f;

	public static Vector3 ToGodotRotationDegrees(KbeVector3Value protocolDirection)
	{
		return new Vector3(
			protocolDirection.Y,
			protocolDirection.Z - YawOffsetDegrees,
			protocolDirection.X
		);
	}

	public static KbeVector3Value ToProtocolDirection(Vector3 godotRotationDegrees)
	{
		return new KbeVector3Value(
			godotRotationDegrees.Z,
			godotRotationDegrees.X,
			godotRotationDegrees.Y + YawOffsetDegrees
		);
	}
}
