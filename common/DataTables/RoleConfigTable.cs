using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CommonData
{
	public static class RoleConfigRepository
	{
		private const string ConfigPath = "res://common/Data/d_role.json";
		private static readonly RoleConfigEntry DefaultRole = new() { Id = 1, Role = 1, Name = "战士" };
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
		};

		private static RoleConfigRoot _current = new();

		public static IReadOnlyDictionary<string, RoleConfigEntry> Datas => _current.AllDatas.Datas;

		public static void Warmup()
		{
			_current = LoadInternal();
		}

		public static RoleConfigEntry GetDefault()
		{
			foreach (var entry in Datas.Values)
			{
				return entry;
			}

			return DefaultRole;
		}

		public static bool TryGetByRole(int role, out RoleConfigEntry entry)
		{
			foreach (var item in Datas.Values)
			{
				var roleValue = item.Role != 0 ? item.Role : item.Id;
				if (roleValue != role)
				{
					continue;
				}

				entry = item;
				return true;
			}

			entry = null;
			return false;
		}

		public static string ResolveDisplayName(int role)
		{
			if (TryGetByRole(role, out var entry))
			{
				return entry.DisplayName;
			}

			return $"Role {role}";
		}

		private static RoleConfigRoot LoadInternal()
		{
			if (!FileAccess.FileExists(ConfigPath))
			{
				GD.PushWarning($"Role config not found: {ConfigPath}");
				return CreateFallbackRoot();
			}

			var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushWarning($"Failed to open role config: {ConfigPath}");
				return CreateFallbackRoot();
			}

			try
			{
				var config = JsonSerializer.Deserialize<RoleConfigRoot>(file.GetAsText(), JsonOptions) ?? new RoleConfigRoot();
				config.Normalize();
				return config;
			}
			catch (JsonException exception)
			{
				GD.PushWarning($"Failed to parse role config: {exception.Message}");
				return CreateFallbackRoot();
			}
		}

		private static RoleConfigRoot CreateFallbackRoot()
		{
			return new RoleConfigRoot
			{
				AllDatas = new RoleConfigCollection
				{
					Datas = new Dictionary<string, RoleConfigEntry>
					{
						{ DefaultRole.Role.ToString(), DefaultRole },
					},
				},
			};
		}
	}

	public sealed class RoleConfigRoot
	{
		[JsonPropertyName("allDatas")]
		public RoleConfigCollection AllDatas { get; set; } = new();

		public void Normalize()
		{
			AllDatas ??= new RoleConfigCollection();
			AllDatas.Normalize();
		}
	}

	public sealed class RoleConfigCollection
	{
		[JsonPropertyName("datas")]
		public Dictionary<string, RoleConfigEntry> Datas { get; set; } = new();

		public void Normalize()
		{
			Datas ??= new Dictionary<string, RoleConfigEntry>();
			foreach (var entry in Datas.Values)
			{
				entry.Normalize();
			}
		}
	}

	public sealed class RoleConfigEntry
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("role")]
		public int Role { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonIgnore]
		public string DisplayName
		{
			get
			{
				var roleValue = Role != 0 ? Role : Id;
				return string.IsNullOrWhiteSpace(Name) ? $"Role {roleValue}" : Name;
			}
		}

		public void Normalize()
		{
			var roleValue = Role != 0 ? Role : Id;
			if (roleValue == 1 && (string.IsNullOrWhiteSpace(Name) || Name.StartsWith("鎴", StringComparison.Ordinal)))
			{
				Name = "战士";
			}
		}
	}
}
