using System;
using System.Collections.Generic;
using Godot;

public static class WorldEntitySceneRegistry
{
	private static readonly Dictionary<Type, string> ScenePaths = new()
	{
		{ typeof(Player), "res://Prefab/Player.tscn" },
		{ typeof(Monster), "res://Prefab/Monster.tscn" },
		{ typeof(Npc), "res://Prefab/Npc.tscn" },
	};

	private static readonly Dictionary<Type, PackedScene> LoadedScenes = new();

	public static string GetScenePath<TEntity>() where TEntity : class
	{
		return GetScenePath(typeof(TEntity));
	}

	public static PackedScene GetPackedScene<TEntity>() where TEntity : class
	{
		return GetPackedScene(typeof(TEntity));
	}

	private static string GetScenePath(Type entityType)
	{
		if (ScenePaths.TryGetValue(entityType, out var scenePath))
		{
			return scenePath;
		}

		throw new InvalidOperationException($"No world-entity scene registered for {entityType.Name}.");
	}

	private static PackedScene GetPackedScene(Type entityType)
	{
		if (LoadedScenes.TryGetValue(entityType, out var cachedScene))
		{
			return cachedScene;
		}

		var scenePath = GetScenePath(entityType);
		var scene = GD.Load<PackedScene>(scenePath);
		LoadedScenes[entityType] = scene;
		return scene;
	}
}
