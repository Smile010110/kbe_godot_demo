using System;
using System.Collections.Generic;
using System.Text;
using CommonData;
using Godot;
using KBEngine;

public readonly struct KbeBuffInfo
{
	public KbeBuffInfo(
		string buffKey,
		uint buffId,
		byte level,
		uint durationMs,
		uint syncedRemainTimeMs,
		ushort stack,
		ulong syncedAtClientMs)
	{
		BuffKey = buffKey ?? string.Empty;
		BuffId = buffId;
		Level = level;
		DurationMs = durationMs;
		SyncedRemainTimeMs = syncedRemainTimeMs;
		Stack = stack == 0 ? (ushort)1 : stack;
		SyncedAtClientMs = syncedAtClientMs;
	}

	public string BuffKey { get; }
	public uint BuffId { get; }
	public byte Level { get; }
	public uint DurationMs { get; }
	public uint SyncedRemainTimeMs { get; }
	public ushort Stack { get; }
	public ulong SyncedAtClientMs { get; }
	public bool IsPermanent => DurationMs == 0U;

	public uint RemainingTimeMs
	{
		get
		{
			if (IsPermanent || SyncedRemainTimeMs == 0U || SyncedAtClientMs == 0UL)
			{
				return SyncedRemainTimeMs;
			}

			var nowMs = Time.GetTicksMsec();
			var elapsedMs = nowMs > SyncedAtClientMs ? nowMs - SyncedAtClientMs : 0UL;
			return elapsedMs >= SyncedRemainTimeMs ? 0U : (uint)(SyncedRemainTimeMs - elapsedMs);
		}
	}

	public uint RemainingTimeSeconds => (RemainingTimeMs + 999U) / 1000U;

	public string SummaryText
	{
		get
		{
			var displayName = BuffConfigRepository.ResolveDisplayName((int)BuffId);
			var stackText = Stack > 1 ? $"x{Stack}" : string.Empty;
			return IsPermanent ? $"{displayName}{stackText}" : $"{displayName}{stackText} {RemainingTimeSeconds}s";
		}
	}

	public static KbeBuffInfo FromProtocol(BUFF_INFO protocolBuff, ulong syncedAtClientMs)
	{
		if (protocolBuff == null)
		{
			return default;
		}

		return new KbeBuffInfo(
			protocolBuff.buff_key,
			protocolBuff.buff_id,
			protocolBuff.level,
			protocolBuff.duration,
			protocolBuff.remain_time,
			protocolBuff.stack,
			syncedAtClientMs);
	}
}

public readonly struct KbeBuffState
{
	public static KbeBuffState Empty { get; } = new(Array.Empty<KbeBuffInfo>());

	public KbeBuffState(IReadOnlyList<KbeBuffInfo> buffs)
	{
		Buffs = buffs ?? Array.Empty<KbeBuffInfo>();
	}

	public IReadOnlyList<KbeBuffInfo> Buffs { get; }
	public int Count => Buffs.Count;
	public bool HasBuffs => Count > 0;

	public string SummaryText => BuildSummary(maxItems: 3);

	public string BuildSummary(int maxItems)
	{
		if (!HasBuffs)
		{
			return string.Empty;
		}

		var builder = new StringBuilder();
		var visibleCount = Math.Clamp(maxItems, 1, Count);
		for (var i = 0; i < visibleCount; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}

			builder.Append(Buffs[i].SummaryText);
		}

		if (Count > visibleCount)
		{
			builder.Append(" +");
			builder.Append(Count - visibleCount);
		}

		return builder.ToString();
	}

	public static KbeBuffState FromProtocol(BUFF_LIST protocolBuffs, ulong syncedAtClientMs)
	{
		if (protocolBuffs?.values == null || protocolBuffs.values.Count == 0)
		{
			return Empty;
		}

		var buffs = new List<KbeBuffInfo>(protocolBuffs.values.Count);
		foreach (var protocolBuff in protocolBuffs.values)
		{
			if (protocolBuff == null)
			{
				continue;
			}

			buffs.Add(KbeBuffInfo.FromProtocol(protocolBuff, syncedAtClientMs));
		}

		buffs.Sort((left, right) =>
		{
			var idComparison = left.BuffId.CompareTo(right.BuffId);
			return idComparison != 0 ? idComparison : string.CompareOrdinal(left.BuffKey, right.BuffKey);
		});

		return buffs.Count == 0 ? Empty : new KbeBuffState(buffs);
	}
}
