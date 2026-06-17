using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CommonData
{
	public static class SkillConfigRepository
	{
		private const string ConfigPath = "res://common/Data/d_skill.json";
		private static readonly SkillConfigEntry DefaultSkill = new()
		{
			Id = 1001,
			Name = "普通攻击",
			SkillType = 1,
			CastType = 1,
			CostMp = 0,
			CooldownMs = 1000,
			GcdGroup = 1,
			RangeMax = 2.0f,
			TargetType = 1,
			EffectType = 1,
			EffectValue = 1.0f,
			CastDelayMs = 350,
			CastWithoutTarget = 0,
			AoeType = 0,
			AoeRadius = 0.0f,
			AoeAngle = 0,
			AoeWidth = 0.0f,
			AoeLength = 0.0f,
		};

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
		};

		private static SkillConfigRoot _current = CreateFallbackRoot();

		public static IReadOnlyDictionary<string, SkillConfigEntry> Datas => _current.AllDatas.SkillData;

		public static void Warmup()
		{
			_current = LoadInternal();
		}

		public static void Reload()
		{
			_current = LoadInternal();
		}

		public static SkillConfigEntry GetDefault()
		{
			if (TryGetBySkillId(DefaultSkill.Id, out var skill))
			{
				return skill;
			}

			foreach (var entry in Datas.Values)
			{
				return entry;
			}

			return DefaultSkill;
		}

		public static bool TryGetBySkillId(int skillId, out SkillConfigEntry entry)
		{
			if (Datas.TryGetValue(skillId.ToString(), out entry))
			{
				entry.Normalize();
				return true;
			}

			foreach (var item in Datas.Values)
			{
				if (item.Id != skillId)
				{
					continue;
				}

				item.Normalize();
				entry = item;
				return true;
			}

			entry = null;
			return false;
		}

		public static string ResolveDisplayName(int skillId)
		{
			return TryGetBySkillId(skillId, out var entry)
				? entry.DisplayName
				: $"Skill {skillId}";
		}

		private static SkillConfigRoot LoadInternal()
		{
			if (!FileAccess.FileExists(ConfigPath))
			{
				GD.PushWarning($"Skill config not found: {ConfigPath}");
				return CreateFallbackRoot();
			}

			using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushWarning($"Failed to open skill config: {ConfigPath}");
				return CreateFallbackRoot();
			}

			try
			{
				var config = JsonSerializer.Deserialize<SkillConfigRoot>(file.GetAsText(), JsonOptions) ?? CreateFallbackRoot();
				config.Normalize();
				return config;
			}
			catch (JsonException exception)
			{
				GD.PushWarning($"Failed to parse skill config: {exception.Message}");
				return CreateFallbackRoot();
			}
		}

		private static SkillConfigRoot CreateFallbackRoot()
		{
			return new SkillConfigRoot
			{
				AllDatas = new SkillConfigCollection
				{
					SkillData = new Dictionary<string, SkillConfigEntry>
					{
						{ DefaultSkill.Id.ToString(), DefaultSkill },
					},
				},
			};
		}
	}

	public sealed class SkillConfigRoot
	{
		[JsonPropertyName("allDatas")]
		public SkillConfigCollection AllDatas { get; set; } = new();

		public void Normalize()
		{
			AllDatas ??= new SkillConfigCollection();
			AllDatas.Normalize();
		}
	}

	public sealed class SkillConfigCollection
	{
		[JsonPropertyName("skillData")]
		public Dictionary<string, SkillConfigEntry> SkillData { get; set; } = new();

		public void Normalize()
		{
			SkillData ??= new Dictionary<string, SkillConfigEntry>();
			foreach (var entry in SkillData.Values)
			{
				entry.Normalize();
			}
		}
	}

	public sealed class SkillConfigEntry
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("skill_type")]
		public int SkillType { get; set; }

		[JsonPropertyName("cast_type")]
		public int CastType { get; set; }

		[JsonPropertyName("cost_mp")]
		public int CostMp { get; set; }

		[JsonPropertyName("cooldown")]
		public int CooldownMs { get; set; }

		[JsonPropertyName("gcd_group")]
		public int GcdGroup { get; set; }

		[JsonPropertyName("range_max")]
		public float RangeMax { get; set; }

		[JsonPropertyName("target_type")]
		public int TargetType { get; set; }

		[JsonPropertyName("effect_type")]
		public int EffectType { get; set; }

		[JsonPropertyName("effect_value")]
		public float EffectValue { get; set; }

		[JsonPropertyName("cast_delay_ms")]
		public int CastDelayMs { get; set; }

		[JsonPropertyName("cast_without_target")]
		public int CastWithoutTarget { get; set; }

		[JsonPropertyName("aoe_type")]
		public int AoeType { get; set; }

		[JsonPropertyName("aoe_radius")]
		public float AoeRadius { get; set; }

		[JsonPropertyName("aoe_angle")]
		public int AoeAngle { get; set; }

		[JsonPropertyName("aoe_width")]
		public float AoeWidth { get; set; }

		[JsonPropertyName("aoe_length")]
		public float AoeLength { get; set; }

		[JsonIgnore]
		public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Skill {Id}" : Name;

		[JsonIgnore]
		public bool IsDamageSkill => EffectType == 1;

		[JsonIgnore]
		public bool IsHealSkill => EffectType == 2;

		[JsonIgnore]
		public bool IsSelfTargetSkill => TargetType == 3;

		[JsonIgnore]
		public bool IsFriendlyTargetSkill => TargetType == 2;

		[JsonIgnore]
		public bool CanCastWithoutTarget => CastWithoutTarget != 0;

		[JsonIgnore]
		public bool IsAoeSkill => AoeType != 0;

		[JsonIgnore]
		public float CooldownSeconds => CooldownMs / 1000.0f;

		[JsonIgnore]
		public bool UsesGlobalCooldown => GcdGroup != 0;

		[JsonIgnore]
		public float CastDelaySeconds => CastDelayMs / 1000.0f;

		public void Normalize()
		{
			Name ??= string.Empty;
			SkillType = Mathf.Max(0, SkillType);
			CastType = Mathf.Max(0, CastType);
			CostMp = Mathf.Max(0, CostMp);
			CooldownMs = Mathf.Max(0, CooldownMs);
			GcdGroup = Mathf.Max(0, GcdGroup);
			RangeMax = Mathf.Max(0.0f, RangeMax);
			TargetType = Mathf.Max(0, TargetType);
			EffectType = Mathf.Max(0, EffectType);
			EffectValue = Mathf.Max(0.0f, EffectValue);
			CastDelayMs = Mathf.Max(0, CastDelayMs);
			CastWithoutTarget = Mathf.Max(0, CastWithoutTarget);
			AoeType = Mathf.Max(0, AoeType);
			AoeRadius = Mathf.Max(0.0f, AoeRadius);
			AoeAngle = Mathf.Max(0, AoeAngle);
			AoeWidth = Mathf.Max(0.0f, AoeWidth);
			AoeLength = Mathf.Max(0.0f, AoeLength);
		}
	}
}
