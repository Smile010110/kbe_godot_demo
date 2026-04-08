using Godot;

public partial class WorldUi : Control
{
	private Label _infoLabel;

	public override void _Ready()
	{
		_infoLabel = GetNode<Label>("MarginContainer/InfoLabel");
	}

	public override void _Process(double delta)
	{
		var player = PlayerController.LocalInstance?.Player;
		var entityId = player != null ? player.EntityId.ToString() : "-";
		var dbid = player != null ? player.DatabaseId.ToString() : "-";
		var serverId = player != null ? player.ServerId.ToString() : "-";
		var spaceLine = player != null ? player.SpaceLine.ToString() : "-";
		var spaceUtype = player != null ? player.SpaceUtype.ToString() : "-";
		var moveSpeed = player != null ? player.RawMoveSpeed.ToString() : "-";
		var hp = player != null ? player.HitPoints.ToString() : "-";
		var mp = player != null ? player.ManaPoints.ToString() : "-";
		var position = player?.WorldPosition ?? Vector3.Zero;
		var positionText = player != null
			? $"({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00})"
			: "-";

		_infoLabel.Text = $"WASD move\nSpace jump\nHold RMB to rotate camera\nEntity: {entityId}\nDBID: {dbid}\nServer: {serverId}\nSpaceUType: {spaceUtype}\nSpaceLine: {spaceLine}\nPosition: {positionText}\nMoveSpeed: {moveSpeed}\nHP: {hp}\nMP: {mp}";
	}
}
