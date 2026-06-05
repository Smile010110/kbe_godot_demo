public static class WorldEntityNameplateText
{
	public static string BuildCombatMotionLine(ulong hp, ulong mp, byte moveSpeed)
	{
		return $"HP {hp} | MP {mp} | SPD {moveSpeed}";
	}

	public static string BuildPlayerLine(ulong hp, ulong mp, byte moveSpeed)
	{
		return $"HP {hp} | MP {mp} | SPD {moveSpeed}";
	}

	public static string BuildSpeedOnlyLine(byte moveSpeed)
	{
		return $"SPD {moveSpeed}";
	}
}
