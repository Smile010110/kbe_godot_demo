public static class WorldEntityNameplateText
{
	public static string BuildCombatMotionLine(ulong hp, ulong maxHp, ulong mp, byte moveSpeed, int activeBuffCount = 0)
	{
		return AppendBuffCount($"HP {hp}/{maxHp} | MP {mp} | SPD {moveSpeed}", activeBuffCount);
	}

	public static string BuildPlayerLine(ulong hp, ulong maxHp, ulong mp, byte moveSpeed, int activeBuffCount = 0)
	{
		return AppendBuffCount($"HP {hp}/{maxHp} | MP {mp} | SPD {moveSpeed}", activeBuffCount);
	}

	public static string BuildSpeedOnlyLine(byte moveSpeed)
	{
		return $"SPD {moveSpeed}";
	}

	private static string AppendBuffCount(string baseText, int activeBuffCount)
	{
		return activeBuffCount > 0 ? $"{baseText}\nBuff {activeBuffCount}" : baseText;
	}
}
