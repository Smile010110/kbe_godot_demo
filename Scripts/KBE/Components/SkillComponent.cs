using Godot;

namespace KBEngine
{
	public class SkillComponent : SkillComponentBase
	{
		public override void on_skill_result(SKILL_RESULT protocolResult)
		{
			var result = SkillCastResult.FromProtocol(protocolResult);
			if (result == null)
			{
				return;
			}

			if (owner is global::Player player)
			{
				player.HandleSkillResult(result);
			}
			else
			{
				GD.Print($"[SkillResult] owner={owner?.id} skill={result.SkillId} caster={result.CasterId} target={result.TargetId} effect={result.EffectType} value={result.Value} kill={result.IsKill}");
			}
		}

		public override void on_skill_error(uint skillId, byte errorCode)
		{
			var error = SkillCastError.FromProtocol(skillId, errorCode);
			if (owner is global::Player player)
			{
				player.HandleSkillError(error);
				return;
			}

			GD.PushWarning($"[SkillError] owner={owner?.id} skill={error.SkillId} code={(byte)error.ErrorCode} message={error.Message}");
		}
	}
}
