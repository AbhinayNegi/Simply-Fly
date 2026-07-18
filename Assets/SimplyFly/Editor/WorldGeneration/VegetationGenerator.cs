using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class VegetationGenerator : IWorldGenerator
    {
        public string Name => "Vegetation and rocks";

        public void Generate(WorldGenerationContext context)
        {
            GameObject vegetation = new("Environment_Placement");
            Undo.RegisterCreatedObjectUndo(vegetation, "Generate world environment");
            vegetation.transform.SetParent(context.root.transform, false);
            GameObject trees = CreateGroup("Forest", vegetation.transform);
            GameObject rocks = CreateGroup("Rocks", vegetation.transform);
            WorldGenerationSettings settings = context.settings;
            float spacing = Mathf.Max(4f, settings.placementSpacing);
            int cells = Mathf.CeilToInt(settings.terrainSize / spacing);
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                float worldX = Mathf.Clamp((x + NextFloat(context.random)) * spacing,
                    0f, settings.terrainSize);
                float worldZ = Mathf.Clamp((z + NextFloat(context.random)) * spacing,
                    0f, settings.terrainSize);
                if (!IsPlacementAllowed(settings, worldX, worldZ))
                    continue;
                Vector3 candidateWorld = context.NormalizedToWorld(
                    worldX / settings.terrainSize, worldZ / settings.terrainSize);
                if (context.IsInsidePhase2Exclusion(candidateWorld, 4f)) continue;
                int bx = Mathf.Clamp(Mathf.RoundToInt(worldX / settings.terrainSize *
                    (context.biomes.GetLength(1) - 1)), 0, context.biomes.GetLength(1) - 1);
                int bz = Mathf.Clamp(Mathf.RoundToInt(worldZ / settings.terrainSize *
                    (context.biomes.GetLength(0) - 1)), 0, context.biomes.GetLength(0) - 1);
                WorldBiome biome = context.biomes[bz, bx];
                if (biome == WorldBiome.Forest &&
                    NextFloat(context.random) < settings.forestDensity)
                    Place(context, settings.treePrefabs, trees.transform, worldX, worldZ, 0.8f, 1.25f);
                else if (biome == WorldBiome.RockyHills &&
                         NextFloat(context.random) < settings.rockDensity)
                    Place(context, settings.rockPrefabs, rocks.transform, worldX, worldZ, 0.65f, 1.7f);
            }
        }

        private static GameObject CreateGroup(string name, Transform parent)
        {
            GameObject group = new(name);
            Undo.RegisterCreatedObjectUndo(group, "Create generated placement group");
            group.transform.SetParent(parent, false);
            return group;
        }

        private static void Place(WorldGenerationContext context, GameObject[] prefabs,
            Transform parent, float worldX, float worldZ, float minScale, float maxScale)
        {
            if (prefabs == null || prefabs.Length == 0) return;
            GameObject prefab = prefabs[context.random.Next(0, prefabs.Length)];
            if (prefab == null) return;
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null) return;
            Undo.RegisterCreatedObjectUndo(instance, "Place generated environment object");
            Vector3 origin = context.terrain.transform.position;
            Vector3 world = origin + new Vector3(worldX, 0f, worldZ);
            world.y = context.terrain.SampleHeight(world) + origin.y;
            float scale = Mathf.Lerp(minScale, maxScale, NextFloat(context.random));
            instance.transform.SetPositionAndRotation(world,
                Quaternion.Euler(0f, NextFloat(context.random) * 360f, 0f));
            instance.transform.localScale = prefab.transform.localScale * scale;
        }

        private static float NextFloat(System.Random random) => (float)random.NextDouble();

        public static bool IsPlacementAllowed(WorldGenerationSettings settings,
            float worldX, float worldZ) => !WorldGenerationMath.InAirportOrApproach(
                settings, worldX, worldZ, 45f);
    }
}
