using Godot;

public partial class PlayerCharacterBody3D : CharacterBody3D
{
	private const float JumpVelocity = 4.5f;
	private const float DefaultGravity = 9.8f;

	private Node3D _cameraPivot;

	public override void _Ready()
	{
		_cameraPivot = GetNode<Node3D>("../CameraPivot");
	}

	public override void _PhysicsProcess(double delta)
	{
		var playerController = GetParent<PlayerController>();
		if (playerController == null || playerController.GetStatus() != 0)
		{
			return;
		}

		var moveSpeed = playerController.GetMoveSpeed();
		var inputDir = Vector2.Zero;

		if (Input.IsActionPressed("move_forward"))
		{
			inputDir.Y -= 1;
		}
		if (Input.IsActionPressed("move_backward"))
		{
			inputDir.Y += 1;
		}
		if (Input.IsActionPressed("move_left"))
		{
			inputDir.X -= 1;
		}
		if (Input.IsActionPressed("move_right"))
		{
			inputDir.X += 1;
		}

		inputDir = inputDir.Normalized();

		if (inputDir != Vector2.Zero)
		{
			var cameraYawBasis = new Basis(Vector3.Up, _cameraPivot.Rotation.Y);
			var moveDir = (cameraYawBasis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

			Velocity = new Vector3(moveDir.X * moveSpeed, Velocity.Y, moveDir.Z * moveSpeed);
			LookAt(GlobalPosition + new Vector3(moveDir.X, 0, moveDir.Z), Vector3.Up);
		}
		else
		{
			Velocity = new Vector3(0.0f, Velocity.Y, 0.0f);
		}

		var gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", DefaultGravity);
		if (!IsOnFloor())
		{
			Velocity = new Vector3(Velocity.X, Velocity.Y - gravity * (float)delta, Velocity.Z);
		}
		else
		{
			if (Input.IsActionJustPressed("jump"))
			{
				Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
			}
			else
			{
				Velocity = new Vector3(Velocity.X, 0.0f, Velocity.Z);
			}
		}

		if (Input.IsActionJustPressed("attack"))
		{
			playerController.TryAttack();
		}

		MoveAndSlide();
	}
}
