using Godot;
using KBEngine;

public static class SkillProtocolLogger
{
	public static bool Enabled { get; set; }

	public static void LogResult(string receiverType, int receiverEntityId, SKILL_RESULT protocolResult)
	{
		// if (!Enabled)
		// {
		// 	return;
		// }

		if (protocolResult == null)
		{
			GD.Print($"[SkillResultRaw] receiver={receiverType}({receiverEntityId}) result=null");
			return;
		}

		GD.Print(
			$"[SkillResultRaw] receiver={receiverType}({receiverEntityId}) "
			+ $"skill_id={protocolResult.skill_id} "
			+ $"caster_id={protocolResult.caster_id} "
			+ $"target_id={protocolResult.target_id} "
			+ $"effect_type={protocolResult.effect_type} "
			+ $"value={protocolResult.value} "
			+ $"is_kill={protocolResult.is_kill} "
			+ $"result_time={protocolResult.cast_time}"
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
