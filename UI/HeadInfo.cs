using Godot;

public partial class HeadInfo : Node3D
{
	private Camera3D _cachedCamera;

	public override void _Process(double delta)
	{
		if (_cachedCamera == null || !IsInstanceValid(_cachedCamera))
		{
			_cachedCamera = GetViewport().GetCamera3D();
		}

		if (_cachedCamera == null)
		{
			return;
		}

		var camPos = _cachedCamera.GlobalTransform.Origin;
		var myPos = GlobalTransform.Origin;
		camPos.Y = myPos.Y;
		LookAt(camPos, Vector3.Up);
		RotateY(Mathf.Pi);
	}
}
