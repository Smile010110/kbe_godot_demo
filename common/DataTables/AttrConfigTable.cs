using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CommonData
{
	public static class AttrConfigRepository
	{
		private const string ConfigPath = "res://common/Data/d_attr.json";
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
		};

		private static AttrConfigRoot _current = new();

		public static IReadOnlyDictionary<string, AttrConfigEntry> Datas => _current.AllDatas.Datas;

		public static void Warmup()
		{
			_current = LoadInternal();
		}

		public static void Reload()
		{
			_current = LoadInternal();
		}

		public static bool TryGetByAttrId(int attrId, out AttrConfigEntry entry)
		{
			if (Datas.TryGetValue(attrId.ToString(), out entry))
			{
				entry.Normalize();
				return true;
			}

			foreach (var item in Datas.Values)
			{
				if (item.Id != attrId)
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

		public static string ResolveDisplayName(int attrId)
		{
			return TryGetByAttrId(attrId, out var entry)
				? entry.DisplayName
				: $"Attr {attrId}";
		}

		private static AttrConfigRoot LoadInternal()
		{
			if (!FileAccess.FileExists(ConfigPath))
			{
				GD.PushWarning($"Attr config not found: {ConfigPath}");
				return new AttrConfigRoot();
			}

			using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushWarning($"Failed to open attr config: {ConfigPath}");
				return new AttrConfigRoot();
			}

			try
			{
				var config = JsonSerializer.Deserialize<AttrConfigRoot>(file.GetAsText(), JsonOptions) ?? new AttrConfigRoot();
				config.Normalize();
				return config;
			}
			catch (JsonException exception)
			{
				GD.PushWarning($"Failed to parse attr config: {exception.Message}");
				return new AttrConfigRoot();
			}
		}
	}

	public sealed class AttrConfigRoot
	{
		[JsonPropertyName("allDatas")]
		public AttrConfigCollection AllDatas { get; set; } = new();

		public void Normalize()
		{
			AllDatas ??= new AttrConfigCollection();
			AllDatas.Normalize();
		}
	}

	public sealed class AttrConfigCollection
	{
		[JsonPropertyName("datas")]
		public Dictionary<string, AttrConfigEntry> Datas { get; set; } = new();

		public void Normalize()
		{
			Datas ??= new Dictionary<string, AttrConfigEntry>();
			foreach (var entry in Datas.Values)
			{
				entry.Normalize();
			}
		}
	}

	public sealed class AttrConfigEntry
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("key")]
		public string Key { get; set; } = string.Empty;

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("client_show")]
		public int ClientShow { get; set; }

		[JsonPropertyName("desc")]
		public string Desc { get; set; } = string.Empty;

		[JsonIgnore]
		public bool ShouldClientShow => ClientShow != 0;

		[JsonIgnore]
		public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Attr {Id}" : Name;

		public void Normalize()
		{
			Key ??= string.Empty;
			Name ??= string.Empty;
			Desc ??= string.Empty;
			ClientShow = Mathf.Max(0, ClientShow);
		}
	}
}
