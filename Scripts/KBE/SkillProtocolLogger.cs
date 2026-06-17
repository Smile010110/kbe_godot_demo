using Godot;

public static class SkillProtocolLogger
{
	public static bool Enabled { get; set; }

	public static void LogResult(string receiverType, int receiverEntityId, SkillCastResult result)
	{
		if (!Enabled)
		{
			return;
		}

		if (result == null)
		{
			GD.Print($"[SkillResultRaw] receiver={receiverType}({receiverEntityId}) result=null");
			return;
		}

		GD.Print(
			$"[SkillResultRaw] receiver={receiverType}({receiverEntityId}) "
			+ $"skill_id={result.SkillId} "
			+ $"caster_id={result.CasterId} "
			+ $"target_id={result.TargetId} "
			+ $"effect_type={result.EffectType} "
			+ $"value={result.Value} "
			+ $"is_kill={result.IsKill} "
			+ $"cast_time={result.CastTime}"
		);
	}

	public static void LogError(string receiverType, int receiverEntityId, SkillCastError error)
	{
		if (!Enabled)
		{
			return;
		}

		if (error == null)
		{
			GD.Print($"[SkillErrorRaw] receiver={receiverType}({receiverEntityId}) error=null");
			return;
		}

		GD.Print(
			$"[SkillErrorRaw] receiver={receiverType}({receiverEntityId}) "
			+ $"skill_id={error.SkillId} "
			+ $"error_code={(byte)error.ErrorCode} "
			+ $"message={error.Message}"
		);
	}
}
