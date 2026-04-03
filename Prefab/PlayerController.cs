using Godot;
using KBEngine;

public partial class PlayerController : Node3D
{
	[Export]
	public float MoveSpeed = 5.0f;

	public static PlayerController LocalInstance { get; private set; }

	public Player Player { get; private set; }

	private Vector3 _targetPosition;
	private CharacterBody3D _characterBody;
	private Node3D _cameraPivot;
	private Camera3D _camera;
	private Label3D _nameLabel;
	private Label3D _infoLabel;
	private bool _isReady;

	public override void _Ready()
	{
		_characterBody = GetNode<CharacterBody3D>("PlayerCharacterBody3D");
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/SpringArm3D/Camera3D");
		_nameLabel = GetNode<Label3D>("PlayerCharacterBody3D/HeadInfo/NameLabel");
		_infoLabel = GetNode<Label3D>("PlayerCharacterBody3D/HeadInfo/HPLabel");
		_targetPosition = _characterBody.GlobalPosition;
		_isReady = true;

		if (Player != null)
		{
			RefreshPresentation();
		}
	}

	public override void _ExitTree()
	{
		if (ReferenceEquals(LocalInstance, this))
		{
			LocalInstance = null;
		}

		base._ExitTree();
	}

	public void BindPlayer(Player player)
	{
		Player = player;

		if (_isReady)
		{
			RefreshPresentation();
		}
	}

	public int GetStatus()
	{
		return Player != null && Player.isPlayer() ? 0 : -1;
	}

	public float GetMoveSpeed()
	{
		if (Player?.motion == null)
		{
			return MoveSpeed;
		}

		return Mathf.Max(0.1f, Player.motion.moveSpeed / 10.0f);
	}

	public void SetHeadInfo()
	{
		if (Player == null || _nameLabel == null || _infoLabel == null)
		{
			return;
		}

		MoveSpeed = GetMoveSpeed();

		var hp = Player.combat != null ? Player.combat.hp : 0UL;
		var mp = Player.combat != null ? Player.combat.mp : 0UL;
		var moveSpeed = Player.motion != null ? Player.motion.moveSpeed : 0;
		var displayName = string.IsNullOrWhiteSpace(Player.name) ? $"Player {Player.id}" : Player.name;

		_nameLabel.Text = Player.isPlayer() ? $"{displayName} (You)" : displayName;
		_infoLabel.Text = $"HP {hp} | MP {mp} | SPD {moveSpeed}";
	}

	public void UpdateFromEntity()
	{
		if (Player == null || _characterBody == null)
		{
			return;
		}

		var entityPosition = new Vector3(Player.position.x, Player.position.y, Player.position.z);
		var entityRotation = new Vector3(Player.direction.x, Player.direction.y - 180.0f, Player.direction.z);

		if (Player.isPlayer())
		{
			_characterBody.GlobalPosition = entityPosition;
		}
		else
		{
			_targetPosition = entityPosition;
			_characterBody.GlobalRotationDegrees = entityRotation;
		}
	}

	public override void _Process(double delta)
	{
		if (Player == null || _characterBody == null)
		{
			return;
		}

		if (Player.isPlayer())
		{
			Player.position = _characterBody.GlobalPosition;
			Player.direction = new KBVector3(
				_characterBody.GlobalRotationDegrees.X,
				_characterBody.GlobalRotationDegrees.Y + 180.0f,
				_characterBody.GlobalRotationDegrees.Z
			);
			return;
		}

		var currentPosition = _characterBody.GlobalTransform.Origin;
		var nextPosition = currentPosition.MoveToward(_targetPosition, (float)(MoveSpeed * delta));
		_characterBody.GlobalTransform = new Transform3D(_characterBody.GlobalTransform.Basis, nextPosition);
	}

	private void RefreshPresentation()
	{
		UpdateOwnershipState();
		UpdateFromEntity();
		SetHeadInfo();
	}

	private void UpdateOwnershipState()
	{
		if (Player == null || _characterBody == null || _cameraPivot == null || _camera == null)
		{
			return;
		}

		var isLocalPlayer = Player.isPlayer();
		_characterBody.SetPhysicsProcess(isLocalPlayer);
		_cameraPivot.SetProcess(isLocalPlayer);
		_cameraPivot.SetProcessInput(isLocalPlayer);
		_cameraPivot.SetProcessUnhandledInput(isLocalPlayer);
		_camera.Current = isLocalPlayer;

		if (isLocalPlayer)
		{
			LocalInstance = this;
		}
		else if (ReferenceEquals(LocalInstance, this))
		{
			LocalInstance = null;
		}
	}
}
