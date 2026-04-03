using System;
using Godot;

public partial class PlayerController : WorldEntityControllerBase<Player>
{
	public static PlayerController LocalInstance { get; private set; }

	public Player Player => EntityView;

	private Node3D _cameraPivot;
	private Camera3D _camera;

	protected override string CharacterBodyPath => "PlayerCharacterBody3D";
	protected override string NameLabelPath => "PlayerCharacterBody3D/HeadInfo/NameLabel";
	protected override string InfoLabelPath => "PlayerCharacterBody3D/HeadInfo/HPLabel";

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
		BindEntity(player);
	}

	protected override void OnCommonNodesReady()
	{
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/SpringArm3D/Camera3D");
	}

	protected override void UpdateControllerState()
	{
		if (Player == null || CharacterBody == null || _cameraPivot == null || _camera == null)
		{
			return;
		}

		var isLocalPlayer = Player.IsLocalPlayer;
		CharacterBody.SetPhysicsProcess(isLocalPlayer);
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
