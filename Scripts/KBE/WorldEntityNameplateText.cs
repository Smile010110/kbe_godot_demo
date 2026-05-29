public static class WorldEntityNameplateText
{
	public static string BuildCombatMotionLine(ulong hp, ulong mp, uint atk, uint def, byte moveSpeed)
	{
		return $"HP {hp} | MP {mp} | ATK {atk} | DEF {def} | SPD {moveSpeed}";
	}

	public static string BuildPlayerLine(ulong hp, ulong mp, uint atk, uint def, byte moveSpeed, uint exp)
	{
		return $"HP {hp} | MP {mp} | ATK {atk} | DEF {def} | SPD {moveSpeed} | EXP {exp}";
	}

	public static string BuildSpeedOnlyLine(byte moveSpeed)
	{
		return $"SPD {moveSpeed}";
	}
}
