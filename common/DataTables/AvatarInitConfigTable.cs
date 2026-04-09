using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CommonData
{
	public static class ClientDataPaths
	{
		public const string AvatarInitConfigPath = "res://common/Data/d_avatar_init.json";
	}

	public static class AvatarInitConfigRepository
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
		};

		private static AvatarInitConfigRoot _current = new();

		public static AvatarInitConfigRoot Current => _current;
		public static IReadOnlyDictionary<string, AvatarInitEntry> Datas => _current.AllDatas.Datas;

		public static void Warmup()
		{
			_current = LoadInternal();
		}

		public static void Reload()
		{
			_current = LoadInternal();
		}

		public static bool TryGetById(int id, out AvatarInitEntry entry)
		{
			return TryGetById(id.ToString(), out entry);
		}

		public static bool TryGetById(string id, out AvatarInitEntry entry)
		{
			return Datas.TryGetValue(id, out entry);
		}

		private static AvatarInitConfigRoot LoadInternal()
		{
			if (!FileAccess.FileExists(ClientDataPaths.AvatarInitConfigPath))
			{
				GD.PushWarning($"Avatar init config not found: {ClientDataPaths.AvatarInitConfigPath}");
				return new AvatarInitConfigRoot();
			}

			var file = FileAccess.Open(ClientDataPaths.AvatarInitConfigPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushWarning($"Failed to open avatar init config: {ClientDataPaths.AvatarInitConfigPath}");
				return new AvatarInitConfigRoot();
			}

			var json = file.GetAsText();
			try
			{
				var config = JsonSerializer.Deserialize<AvatarInitConfigRoot>(json, JsonOptions) ?? new AvatarInitConfigRoot();
				config.Normalize();
				return config;
			}
			catch (JsonException exception)
			{
				GD.PushWarning($"Failed to parse avatar init config: {exception.Message}");
				return new AvatarInitConfigRoot();
			}
		}
	}

	public sealed class AvatarInitConfigRoot
	{
		[JsonPropertyName("allDatas")]
		public AvatarInitDataCollection AllDatas { get; set; } = new();

		public void Normalize()
		{
			AllDatas ??= new AvatarInitDataCollection();
			AllDatas.Normalize();
		}
	}

	public sealed class AvatarInitDataCollection
	{
		[JsonPropertyName("datas")]
		public Dictionary<string, AvatarInitEntry> Datas { get; set; } = new();

		public void Normalize()
		{
			Datas ??= new Dictionary<string, AvatarInitEntry>();
		}
	}

	public sealed class AvatarInitEntry
	{
		[JsonPropertyName("role")]
		public int Role { get; set; }

		[JsonPropertyName("race")]
		public int Race { get; set; }

		[JsonPropertyName("sex")]
		public int Sex { get; set; }

		[JsonPropertyName("modelID")]
		public uint ModelId { get; set; }

		[JsonPropertyName("modelScale")]
		public float ModelScale { get; set; }

		[JsonPropertyName("spaceUType")]
		public uint SpaceUType { get; set; }

		[JsonPropertyName("spawnPos")]
		public float[] SpawnPos { get; set; } = Array.Empty<float>();

		[JsonPropertyName("spawnYaw")]
		public float SpawnYaw { get; set; }

		[JsonPropertyName("money")]
		public int Money { get; set; }

		[JsonPropertyName("level")]
		public int Level { get; set; }

		[JsonPropertyName("moveSpeed")]
		public int MoveSpeed { get; set; }

		[JsonPropertyName("hp_max")]
		public int HpMax { get; set; }

		[JsonPropertyName("hp")]
		public int Hp { get; set; }

		[JsonPropertyName("mp_max")]
		public int MpMax { get; set; }

		[JsonPropertyName("mp")]
		public int Mp { get; set; }

		[JsonPropertyName("anger")]
		public int Anger { get; set; }

		[JsonPropertyName("anger_max")]
		public int AngerMax { get; set; }

		[JsonPropertyName("energy")]
		public int Energy { get; set; }

		[JsonPropertyName("energy_max")]
		public int EnergyMax { get; set; }

		[JsonPropertyName("constitution")]
		public int Constitution { get; set; }

		[JsonPropertyName("intellect")]
		public int Intellect { get; set; }

		[JsonPropertyName("strength")]
		public int Strength { get; set; }

		[JsonPropertyName("stamina")]
		public int Stamina { get; set; }

		[JsonPropertyName("dexterity")]
		public int Dexterity { get; set; }

		[JsonPropertyName("damage")]
		public int Damage { get; set; }

		[JsonPropertyName("magic_damage")]
		public int MagicDamage { get; set; }

		[JsonPropertyName("magic_defense")]
		public int MagicDefense { get; set; }

		[JsonPropertyName("hitval")]
		public int HitValue { get; set; }

		[JsonPropertyName("defense")]
		public int Defense { get; set; }

		[JsonPropertyName("speed")]
		public int Speed { get; set; }

		[JsonPropertyName("dodge")]
		public int Dodge { get; set; }

		[JsonPropertyName("potential")]
		public int Potential { get; set; }

		[JsonPropertyName("exp")]
		public int Experience { get; set; }

		[JsonIgnore]
		public Vector3 SpawnPosition => SpawnPos.Length >= 3
			? new Vector3(SpawnPos[0], SpawnPos[1], SpawnPos[2])
			: Vector3.Zero;
	}
}
