using Godot;

public partial class WorldUi : Control
{
	private Label _infoLabel;
	private double _refreshAccumulator;
	private string _lastInfoText = string.Empty;

	public override void _Ready()
	{
		_infoLabel = GetNode<Label>("MarginContainer/InfoLabel");
		RefreshHud(force: true);
	}

	public override void _Process(double delta)
	{
		_refreshAccumulator += delta;
		if (_refreshAccumulator < ClientUiConfig.WorldHudRefreshIntervalSeconds)
		{
			return;
		}

		_refreshAccumulator = 0.0d;
		RefreshHud();
	}

	private void RefreshHud(bool force = false)
	{
		var player = PlayerController.LocalInstance?.Player;
		var playerController = PlayerController.LocalInstance;
		var entityId = player != null ? player.EntityId.ToString() : "-";
		var dbid = player != null ? player.DatabaseId.ToString() : "-";
		var serverId = player != null ? player.ServerId.ToString() : "-";
		var serverTime = player != null ? player.ServerTimeText : "-";
		var spaceLine = player != null ? player.SpaceLine.ToString() : "-";
		var spaceUtype = player != null ? player.SpaceUtype.ToString() : "-";
		var moveSpeed = player != null ? player.RawMoveSpeed.ToString() : "-";
		var hp = player != null ? player.HitPoints.ToString() : "-";
		var mp = player != null ? player.ManaPoints.ToString() : "-";
		var position = player?.WorldPosition ?? Vector3.Zero;
		var positionText = player != null
			? $"({position.X:0.00}, {position.Y:0.00}, {position.Z:0.00})"
			: "-";
		var animationState = playerController != null ? playerController.CurrentAnimationStateName : "-";
		var animationKey = playerController != null ? playerController.CurrentAnimationKey : "-";

		var nextInfoText = $"WASD move\nSpace jump\nHold RMB to rotate camera\nEntity: {entityId}\nDBID: {dbid}\nServer: {serverId}\nServerTime: {serverTime}\nSpaceUType: {spaceUtype}\nSpaceLine: {spaceLine}\nPosition: {positionText}\nMoveSpeed: {moveSpeed}\nHP: {hp}\nMP: {mp}\nAnimState: {animationState}\nAnimKey: {animationKey}";
		if (!force && string.Equals(_lastInfoText, nextInfoText, System.StringComparison.Ordinal))
		{
			return;
		}

		_lastInfoText = nextInfoText;
		_infoLabel.Text = nextInfoText;
	}
}
