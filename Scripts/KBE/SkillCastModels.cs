using System;

public enum SkillEffectType : byte
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
	private const ulong MillisecondsThreshold = 1_000_000_000_000UL;

	public int SkillId { get; private set; }
	public int CasterId { get; private set; }
	public int TargetId { get; private set; }
	public SkillEffectType EffectType { get; private set; }
	public int Value { get; private set; }
	public bool IsKill { get; private set; }
	public ulong CastTime { get; private set; }
	public bool HasCastTime => CastTime > 0UL;

	public static SkillCastResult FromValues(
		uint skillId,
		int casterId,
		int targetId,
		byte effectType,
		uint value,
		byte isKill,
		ulong castTime)
	{
		return new SkillCastResult
		{
			SkillId = (int)Math.Min(skillId, int.MaxValue),
			CasterId = casterId,
			TargetId = targetId,
			EffectType = ResolveEffectType(effectType),
			Value = (int)Math.Min(value, int.MaxValue),
			IsKill = isKill != 0,
			CastTime = castTime,
		};
	}

	private static SkillEffectType ResolveEffectType(byte effectType)
	{
		return effectType switch
		{
			(byte)SkillEffectType.Damage => SkillEffectType.Damage,
			(byte)SkillEffectType.Heal => SkillEffectType.Heal,
			_ => SkillEffectType.Unknown,
		};
	}

	public double ResolveElapsedCastSeconds(ulong currentServerTime)
	{
		if (CastTime == 0UL || currentServerTime == 0UL)
		{
			return 0.0d;
		}

		var elapsedSeconds = NormalizeServerTimeSeconds(currentServerTime) - NormalizeServerTimeSeconds(CastTime);
		return elapsedSeconds > 0.0d ? elapsedSeconds : 0.0d;
	}

	private static double NormalizeServerTimeSeconds(ulong serverTime)
	{
		return serverTime >= MillisecondsThreshold
			? serverTime / 1000.0d
			: serverTime;
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
		return new SkillCastError((int)Math.Min(skillId, int.MaxValue), ResolveErrorCode(errorCode));
	}

	private static SkillErrorCode ResolveErrorCode(byte errorCode)
	{
		return errorCode switch
		{
			(byte)SkillErrorCode.CooldownNotReady => SkillErrorCode.CooldownNotReady,
			(byte)SkillErrorCode.NotEnoughMp => SkillErrorCode.NotEnoughMp,
			(byte)SkillErrorCode.OutOfRange => SkillErrorCode.OutOfRange,
			(byte)SkillErrorCode.InvalidTarget => SkillErrorCode.InvalidTarget,
			(byte)SkillErrorCode.CasterDead => SkillErrorCode.CasterDead,
			(byte)SkillErrorCode.Casting => SkillErrorCode.Casting,
			_ => SkillErrorCode.SkillNotFound,
		};
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
