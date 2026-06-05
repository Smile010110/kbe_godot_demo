using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CommonData
{
	public static class PlayerAppearanceConfigRepository
	{
		private const string ConfigPath = "res://common/Data/player_model_profiles.json";
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
		};

		private static readonly PlayerAppearanceProfile FallbackProfile = new()
		{
			ModelId = 900001,
			ResourceFolder = "res://Res/Player/90001",
			ModelSceneFile = "model.fbx",
			ModelPosition = new[] { 0.0f, -0.85f, 0.0f },
			ModelRotationDegrees = new[] { 0.0f, 180.0f, 0.0f },
			ModelScale = new[] { 1.0f, 1.0f, 1.0f },
		};

		private static PlayerAppearanceConfigRoot _current = CreateFallbackRoot();

		public static uint DefaultModelId => _current.DefaultModelId != 0U ? _current.DefaultModelId : FallbackProfile.ModelId;
		public static IReadOnlyDictionary<string, PlayerAppearanceProfile> Datas => _current.Datas;

		public static void Warmup()
		{
			_current = LoadInternal();
		}

		public static void Reload()
		{
			_current = LoadInternal();
		}

		public static PlayerAppearanceProfile GetDefaultProfile()
		{
			if (TryGetByModelId(DefaultModelId, out var profile))
			{
				return profile;
			}

			return CloneFallbackProfile();
		}

		public static bool TryGetByModelId(uint modelId, out PlayerAppearanceProfile profile)
		{
			if (Datas.TryGetValue(modelId.ToString(), out profile))
			{
				profile.Normalize();
				return true;
			}

			profile = null;
			return false;
		}

		private static PlayerAppearanceConfigRoot LoadInternal()
		{
			if (!FileAccess.FileExists(ConfigPath))
			{
				GD.PushWarning($"Player appearance config not found: {ConfigPath}");
				return CreateFallbackRoot();
			}

			using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Read);
			if (file == null)
			{
				GD.PushWarning($"Failed to open player appearance config: {ConfigPath}");
				return CreateFallbackRoot();
			}

			var json = file.GetAsText();
			try
			{
				var config = JsonSerializer.Deserialize<PlayerAppearanceConfigRoot>(json, JsonOptions) ?? CreateFallbackRoot();
				config.Normalize();
				EnsureFallbackDefault(config);
				return config;
			}
			catch (JsonException exception)
			{
				GD.PushWarning($"Failed to parse player appearance config: {exception.Message}");
				return CreateFallbackRoot();
			}
		}

		private static PlayerAppearanceConfigRoot CreateFallbackRoot()
		{
			var config = new PlayerAppearanceConfigRoot
			{
				DefaultModelId = FallbackProfile.ModelId,
				Datas = new Dictionary<string, PlayerAppearanceProfile>
				{
					{ FallbackProfile.ModelId.ToString(), CloneFallbackProfile() },
				},
			};

			config.Normalize();
			return config;
		}

		private static void EnsureFallbackDefault(PlayerAppearanceConfigRoot config)
		{
			if (!config.Datas.ContainsKey(FallbackProfile.ModelId.ToString()))
			{
				config.Datas[FallbackProfile.ModelId.ToString()] = CloneFallbackProfile();
			}

			if (config.DefaultModelId == 0U)
			{
				config.DefaultModelId = FallbackProfile.ModelId;
			}
		}

		private static PlayerAppearanceProfile CloneFallbackProfile()
		{
			return new PlayerAppearanceProfile
			{
				ModelId = FallbackProfile.ModelId,
				ResourceFolder = FallbackProfile.ResourceFolder,
				ModelSceneFile = FallbackProfile.ModelSceneFile,
				ModelPosition = (float[])FallbackProfile.ModelPosition.Clone(),
				ModelRotationDegrees = (float[])FallbackProfile.ModelRotationDegrees.Clone(),
				ModelScale = (float[])FallbackProfile.ModelScale.Clone(),
			};
		}
	}

	public sealed class PlayerAppearanceConfigRoot
	{
		[JsonPropertyName("defaultModelId")]
		public uint DefaultModelId { get; set; }

		[JsonPropertyName("datas")]
		public Dictionary<string, PlayerAppearanceProfile> Datas { get; set; } = new();

		public void Normalize()
		{
			Datas ??= new Dictionary<string, PlayerAppearanceProfile>();
			foreach (var profile in Datas.Values)
			{
				profile.Normalize();
			}
		}
	}

	public sealed class PlayerAppearanceProfile
	{
		[JsonPropertyName("modelId")]
		public uint ModelId { get; set; }

		[JsonPropertyName("resourceFolder")]
		public string ResourceFolder { get; set; } = string.Empty;

		[JsonPropertyName("modelSceneFile")]
		public string ModelSceneFile { get; set; } = "model.fbx";

		[JsonPropertyName("modelPosition")]
		public float[] ModelPosition { get; set; } = Array.Empty<float>();

		[JsonPropertyName("modelRotationDegrees")]
		public float[] ModelRotationDegrees { get; set; } = Array.Empty<float>();

		[JsonPropertyName("modelScale")]
		public float[] ModelScale { get; set; } = Array.Empty<float>();

		[JsonIgnore]
		public string ModelScenePath => $"{ResourceFolder.TrimEnd('/')}/{ModelSceneFile}";

		[JsonIgnore]
		public Vector3 ModelPositionVector => ToVector3(ModelPosition, Vector3.Zero);

		[JsonIgnore]
		public Vector3 ModelRotationDegreesVector => ToVector3(ModelRotationDegrees, Vector3.Zero);

		[JsonIgnore]
		public Vector3 ModelScaleVector => ToVector3(ModelScale, Vector3.One);

		public void Normalize()
		{
			ResourceFolder ??= string.Empty;
			ModelSceneFile = string.IsNullOrWhiteSpace(ModelSceneFile) ? "model.fbx" : ModelSceneFile;
			ModelPosition ??= Array.Empty<float>();
			ModelRotationDegrees ??= Array.Empty<float>();
			ModelScale ??= Array.Empty<float>();
		}

		private static Vector3 ToVector3(float[] values, Vector3 fallback)
		{
			return values.Length >= 3
				? new Vector3(values[0], values[1], values[2])
				: fallback;
		}
	}
}
