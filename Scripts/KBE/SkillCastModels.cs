using System;
using KBEngine;

public enum SkillEffectType
{
	Unknown = 0,
	Damage = 1,
	Heal = 2,
}

public enum SkillErrorCode : byte
{
	SkillNotFound = 0,
	CooldownNotReady = 1,
	NotEnoughMp = 2,
	OutOfRange = 3,
	InvalidTarget = 4,
	CasterDead = 5,
	Casting = 6,
}

public sealed class SkillCastResult
{
	public int SkillId { get; private set; }
	public ulong CasterId { get; private set; }
	public ulong TargetId { get; private set; }
	public SkillEffectType EffectType { get; private set; }
	public int Value { get; private set; }
	public bool IsKill { get; private set; }

	public static SkillCastResult FromProtocol(SKILL_RESULT protocol)
	{
		if (protocol == null)
		{
			return null;
		}

		return new SkillCastResult
		{
			SkillId = (int)Math.Min(protocol.skill_id, int.MaxValue),
			CasterId = protocol.caster_id,
			TargetId = protocol.target_id,
			EffectType = Enum.IsDefined(typeof(SkillEffectType), protocol.effect_type)
				? (SkillEffectType)protocol.effect_type
				: SkillEffectType.Unknown,
			Value = (int)Math.Min(protocol.value, int.MaxValue),
			IsKill = protocol.is_kill != 0,
		};
	}
}

public sealed class SkillCastError
{
	public SkillCastError(int skillId, SkillErrorCode errorCode)
	{
		SkillId = skillId;
		ErrorCode = errorCode;
	}

	public int SkillId { get; }
	public SkillErrorCode ErrorCode { get; }
	public string Message => ResolveMessage(ErrorCode);

	public static SkillCastError FromProtocol(uint skillId, byte errorCode)
	{
		var typedCode = Enum.IsDefined(typeof(SkillErrorCode), errorCode)
			? (SkillErrorCode)errorCode
			: SkillErrorCode.SkillNotFound;
		return new SkillCastError((int)Math.Min(skillId, int.MaxValue), typedCode);
	}

	public static string ResolveMessage(SkillErrorCode errorCode)
	{
		return errorCode switch
		{
			SkillErrorCode.SkillNotFound => "技能不存在",
			SkillErrorCode.CooldownNotReady => "技能冷却中",
			SkillErrorCode.NotEnoughMp => "内力不足",
			SkillErrorCode.OutOfRange => "距离太远",
			SkillErrorCode.InvalidTarget => "无效目标",
			SkillErrorCode.CasterDead => "角色已死亡",
			SkillErrorCode.Casting => "正在施法中",
			_ => $"技能错误 {(byte)errorCode}",
		};
	}
}
