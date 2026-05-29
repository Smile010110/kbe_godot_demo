using System.Collections.Generic;
using Godot;

public partial class CameraPivot : Node3D
{
	private CharacterBody3D _player;
	private Node3D _playerModel;
	private SpringArm3D _springArm;
	private Camera3D _camera;

	private Vector3 _modelFocusOffset = Vector3.Zero;
	private int _lastModelChildCount = -1;
	private bool _isRotating;

	[Export(PropertyHint.Range, "0.0,1.0")]
	public float MouseSensitivity { get; set; } = 0.01f;

	[Export]
	public float TiltLimit { get; set; } = Mathf.DegToRad(75.0f);

	[Export]
	public float FocusHeight { get; set; } = 1.35f;

	[Export]
	public float ZoomStep { get; set; } = 1.0f;

	[Export]
	public float MinZoom { get; set; } = 1.5f;

	[Export]
	public float MaxZoom { get; set; } = 14.0f;

	public override void _Ready()
	{
		_player = GetNode<CharacterBody3D>("../PlayerCharacterBody3D");
		_playerModel = GetNode<Node3D>("../PlayerCharacterBody3D/PlayerModel");
		_springArm = GetNode<SpringArm3D>("SpringArm3D");
		_camera = GetNode<Camera3D>("SpringArm3D/Camera3D");
		RefreshModelFocusOffset();
	}

	public override void _Process(double delta)
	{
		EnsureModelFocusOffset();
		var focusPoint = _player.ToGlobal(_modelFocusOffset);
		GlobalPosition = focusPoint;
		_camera.LookAt(focusPoint, Vector3.Up);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				_isRotating = mouseButton.Pressed;
			}

			if (mouseButton.Pressed)
			{
				if (mouseButton.ButtonIndex == MouseButton.WheelUp)
				{
					_springArm.SpringLength = Mathf.Clamp(
						_springArm.SpringLength - ZoomStep, MinZoom, MaxZoom);
				}
				else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
				{
					_springArm.SpringLength = Mathf.Clamp(
						_springArm.SpringLength + ZoomStep, MinZoom, MaxZoom);
				}
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && _isRotating)
		{
			Rotation = new Vector3(
				Mathf.Clamp(Rotation.X - mouseMotion.Relative.Y * MouseSensitivity, -TiltLimit, TiltLimit),
				Rotation.Y - mouseMotion.Relative.X * MouseSensitivity,
				0.0f
			);
		}
	}

	private void EnsureModelFocusOffset()
	{
		var childCount = _playerModel.GetChildCount();
		if (childCount == _lastModelChildCount)
		{
			return;
		}

		RefreshModelFocusOffset();
	}

	private void RefreshModelFocusOffset()
	{
		_lastModelChildCount = _playerModel.GetChildCount();
		_modelFocusOffset = ComputeModelFocusOffset();
	}

	private Vector3 ComputeModelFocusOffset()
	{
		var points = new List<Vector3>();
		CollectMeshBoundPoints(_playerModel, points);

		if (points.Count == 0)
		{
			return Vector3.Up * FocusHeight;
		}

		var minPoint = points[0];
		var maxPoint = points[0];

		foreach (var point in points)
		{
			minPoint = new Vector3(
				Mathf.Min(minPoint.X, point.X),
				Mathf.Min(minPoint.Y, point.Y),
				Mathf.Min(minPoint.Z, point.Z));
			maxPoint = new Vector3(
				Mathf.Max(maxPoint.X, point.X),
				Mathf.Max(maxPoint.Y, point.Y),
				Mathf.Max(maxPoint.Z, point.Z));
		}

		return (minPoint + maxPoint) * 0.5f;
	}

	private void CollectMeshBoundPoints(Node node, List<Vector3> points)
	{
		if (node is MeshInstance3D meshInstance)
		{
			var meshBounds = meshInstance.GetAabb();

			foreach (var corner in GetAabbCorners(meshBounds))
			{
				points.Add(_player.ToLocal(meshInstance.ToGlobal(corner)));
			}
		}

		foreach (Node child in node.GetChildren())
		{
			CollectMeshBoundPoints(child, points);
		}
	}

	private static Vector3[] GetAabbCorners(Aabb bounds)
	{
		var position = bounds.Position;
		var size = bounds.Size;

		return new[]
		{
			position,
			position + new Vector3(size.X, 0.0f, 0.0f),
			position + new Vector3(0.0f, size.Y, 0.0f),
			position + new Vector3(0.0f, 0.0f, size.Z),
			position + new Vector3(size.X, size.Y, 0.0f),
			position + new Vector3(size.X, 0.0f, size.Z),
			position + new Vector3(0.0f, size.Y, size.Z),
			position + size,
		};
	}
}
