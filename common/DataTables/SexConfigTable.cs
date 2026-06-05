using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CommonData
{
	public static class SexConfigRepository
	{
		private const string ConfigPath = "res://common/Data/d_sex.json";
		private static readonly SexConfigEntry DefaultMale = new() { Sex = 1, Name = "男", ModelId = 900001U };
		private static readonly SexConfigEntry DefaultFemale = new() { Sex = 2, Name = "女", ModelId = 900002U };
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
		};

		private static SexConfigRoot _current = new();

		public static IReadOnlyDictionary<string, SexConfigEntry> Datas => _current.AllDatas.Datas;

		public static void Warmup()
		{
			_current = LoadInternal();
		}

		public static SexConfigEntry GetDefault()
		{
			foreach (var entry in Datas.Values)
			{
				return entry;
			}

			return DefaultMale;
		}

		public static bool TryGetBySex(int sex, out SexConfigEntry entry)
		{
			foreach (var item in Datas.Values)
			{
				if (item.Sex == sex)
				{
					entry = item;
					return true;
				}
			}

			entry = null;
			return false;
		}

		public static string ResolveDisplayName(int sex)
		{
			if (TryGetBySex(sex, out var entry))
			{
				return entry.DisplayName;
			}

			return $"Sex {sex}";
		}

		private static SexConfigRoot LoadInternal()
		{
			if (!FileAccess.FileExists(ConfigPath))
			{
				GD.PushWarning($"Sex config not found: {ConfigPath}");
				return CreateFallbackRoot();
			}

			using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushWarning($"Failed to open sex config: {ConfigPath}");
				return CreateFallbackRoot();
			}

			try
			{
				var config = JsonSerializer.Deserialize<SexConfigRoot>(file.GetAsText(), JsonOptions) ?? new SexConfigRoot();
				config.Normalize();
				return config;
			}
			catch (JsonException exception)
			{
				GD.PushWarning($"Failed to parse sex config: {exception.Message}");
				return CreateFallbackRoot();
			}
		}

		private static SexConfigRoot CreateFallbackRoot()
		{
			return new SexConfigRoot
			{
				AllDatas = new SexConfigCollection
				{
					Datas = new Dictionary<string, SexConfigEntry>
					{
						{ DefaultMale.Sex.ToString(), DefaultMale },
						{ DefaultFemale.Sex.ToString(), DefaultFemale },
					},
				},
			};
		}
	}

	public sealed class SexConfigRoot
	{
		[JsonPropertyName("allDatas")]
		public SexConfigCollection AllDatas { get; set; } = new();

		public void Normalize()
		{
			AllDatas ??= new SexConfigCollection();
			AllDatas.Normalize();
		}
	}

	public sealed class SexConfigCollection
	{
		[JsonPropertyName("datas")]
		public Dictionary<string, SexConfigEntry> Datas { get; set; } = new();

		public void Normalize()
		{
			Datas ??= new Dictionary<string, SexConfigEntry>();
			foreach (var entry in Datas.Values)
			{
				entry.Normalize();
			}
		}
	}

	public sealed class SexConfigEntry
	{
		[JsonPropertyName("sex")]
		public int Sex { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("modelID")]
		public uint ModelId { get; set; }

		[JsonIgnore]
		public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Sex {Sex}" : Name;

		public void Normalize()
		{
			if (Sex == 1 && (string.IsNullOrWhiteSpace(Name) || Name.StartsWith("鐢", StringComparison.Ordinal)))
			{
				Name = "男";
			}
			else if (Sex == 2 && (string.IsNullOrWhiteSpace(Name) || Name.StartsWith("濂", StringComparison.Ordinal)))
			{
				Name = "女";
			}
		}
	}
}
