using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CommonData
{
	public static class BuffConfigRepository
	{
		private const string ConfigPath = "res://common/Data/d_buff.json";
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
		};

		private static BuffConfigRoot _current = new();

		public static IReadOnlyDictionary<string, BuffConfigEntry> Datas => _current.AllDatas.Datas;

		public static void Warmup()
		{
			_current = LoadInternal();
		}

		public static void Reload()
		{
			_current = LoadInternal();
		}

		public static bool TryGetByBuffId(int buffId, out BuffConfigEntry entry)
		{
			if (Datas.TryGetValue(buffId.ToString(), out entry))
			{
				entry.Normalize();
				return true;
			}

			foreach (var item in Datas.Values)
			{
				if (item.Id != buffId)
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

		public static string ResolveDisplayName(int buffId)
		{
			return TryGetByBuffId(buffId, out var entry)
				? entry.DisplayName
				: $"Buff {buffId}";
		}

		private static BuffConfigRoot LoadInternal()
		{
			if (!FileAccess.FileExists(ConfigPath))
			{
				GD.PushWarning($"Buff config not found: {ConfigPath}");
				return new BuffConfigRoot();
			}

			using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushWarning($"Failed to open buff config: {ConfigPath}");
				return new BuffConfigRoot();
			}

			try
			{
				var config = JsonSerializer.Deserialize<BuffConfigRoot>(file.GetAsText(), JsonOptions) ?? new BuffConfigRoot();
				config.Normalize();
				return config;
			}
			catch (JsonException exception)
			{
				GD.PushWarning($"Failed to parse buff config: {exception.Message}");
				return new BuffConfigRoot();
			}
		}
	}

	public sealed class BuffConfigRoot
	{
		[JsonPropertyName("allDatas")]
		public BuffConfigCollection AllDatas { get; set; } = new();

		public void Normalize()
		{
			AllDatas ??= new BuffConfigCollection();
			AllDatas.Normalize();
		}
	}

	public sealed class BuffConfigCollection
	{
		[JsonPropertyName("datas")]
		public Dictionary<string, BuffConfigEntry> Datas { get; set; } = new();

		public void Normalize()
		{
			Datas ??= new Dictionary<string, BuffConfigEntry>();
			foreach (var entry in Datas.Values)
			{
				entry.Normalize();
			}
		}
	}

	public sealed class BuffConfigEntry
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("duration")]
		public int DurationMs { get; set; }

		[JsonPropertyName("max_stack")]
		public int MaxStack { get; set; }

		[JsonPropertyName("stack_rule")]
		public int StackRule { get; set; }

		[JsonPropertyName("attrs")]
		public Dictionary<string, int> Attrs { get; set; } = new();

		[JsonPropertyName("desc")]
		public string Desc { get; set; } = string.Empty;

		[JsonIgnore]
		public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Buff {Id}" : Name;

		[JsonIgnore]
		public bool IsPermanent => DurationMs == 0;

		public string BuildAttrSummary()
		{
			if (Attrs == null || Attrs.Count == 0)
			{
				return string.Empty;
			}

			var builder = new StringBuilder();
			foreach (var pair in Attrs)
			{
				if (!int.TryParse(pair.Key, out var attrId))
				{
					continue;
				}

				if (AttrConfigRepository.TryGetByAttrId(attrId, out var attrConfig)
					&& !attrConfig.ShouldClientShow)
				{
					continue;
				}

				if (builder.Length > 0)
				{
					builder.Append(", ");
				}

				var attrName = AttrConfigRepository.ResolveDisplayName(attrId);
				var sign = pair.Value > 0 ? "+" : string.Empty;
				builder.Append(attrName);
				builder.Append(' ');
				builder.Append(sign);
				builder.Append(pair.Value);
			}

			return builder.ToString();
		}

		public void Normalize()
		{
			Name ??= string.Empty;
			Attrs ??= new Dictionary<string, int>();
			Desc ??= string.Empty;
			DurationMs = Mathf.Max(0, DurationMs);
			MaxStack = Mathf.Max(0, MaxStack);
			StackRule = Mathf.Max(0, StackRule);
		}
	}
}
