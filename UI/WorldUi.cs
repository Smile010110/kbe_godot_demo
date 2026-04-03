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
		var entityId = player != null ? player.id.ToString() : "-";
		var dbid = player != null ? player.dbid.ToString() : "-";
		var serverId = player != null ? player.server_id.ToString() : "-";
		var spaceUtype = player != null ? player.space_utype.ToString() : "-";
		var moveSpeed = player?.motion != null ? player.motion.moveSpeed.ToString() : "-";
		var hp = player?.combat != null ? player.combat.hp.ToString() : "-";
		var mp = player?.combat != null ? player.combat.mp.ToString() : "-";
		var positionText = player != null
			? $"({player.position.x:0.00}, {player.position.y:0.00}, {player.position.z:0.00})"
			: "-";

		_infoLabel.Text = $"WASD move\nSpace jump\nHold RMB to rotate camera\nEntity: {entityId}\nDBID: {dbid}\nServer: {serverId}\nSpaceUType: {spaceUtype}\nPosition: {positionText}\nMoveSpeed: {moveSpeed}\nHP: {hp}\nMP: {mp}";
	}
}
