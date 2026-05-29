using Godot;
using KBEngine;

public readonly struct KbeVector2Value
{
	public KbeVector2Value(float x, float y)
	{
		X = x;
		Y = y;
	}

	public float X { get; }
	public float Y { get; }

	public static KbeVector2Value FromProtocol(KBVector2 value)
	{
		return new KbeVector2Value(value.x, value.y);
	}

	public static KbeVector2Value FromGodot(Vector2 value)
	{
		return new KbeVector2Value(value.X, value.Y);
	}

	public KBVector2 ToProtocol()
	{
		return new KBVector2(X, Y);
	}

	public Vector2 ToGodot()
	{
		return new Vector2(X, Y);
	}
}

public readonly struct KbeVector3Value
{
	public KbeVector3Value(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public float X { get; }
	public float Y { get; }
	public float Z { get; }

	public static KbeVector3Value FromProtocol(KBVector3 value)
	{
		return new KbeVector3Value(value.x, value.y, value.z);
	}

	public static KbeVector3Value FromGodot(Vector3 value)
	{
		return new KbeVector3Value(value.X, value.Y, value.Z);
	}

	public KBVector3 ToProtocol()
	{
		return new KBVector3(X, Y, Z);
	}

	public Vector3 ToGodot()
	{
		return new Vector3(X, Y, Z);
	}
}

public readonly struct KbeVector4Value
{
	public KbeVector4Value(float x, float y, float z, float w)
	{
		X = x;
		Y = y;
		Z = z;
		W = w;
	}

	public float X { get; }
	public float Y { get; }
	public float Z { get; }
	public float W { get; }

	public static KbeVector4Value FromProtocol(KBVector4 value)
	{
		return new KbeVector4Value(value.x, value.y, value.z, value.w);
	}

	public static KbeVector4Value FromGodot(Vector4 value)
	{
		return new KbeVector4Value(value.X, value.Y, value.Z, value.W);
	}

	public KBVector4 ToProtocol()
	{
		return new KBVector4(X, Y, Z, W);
	}

	public Vector4 ToGodot()
	{
		return new Vector4(X, Y, Z, W);
	}
}
