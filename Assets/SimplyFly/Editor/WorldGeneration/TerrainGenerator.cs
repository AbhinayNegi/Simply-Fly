using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class TerrainGenerator : IWorldGenerator
    {
        public const string GeneratedFolder = "Assets/SimplyFly/Generated/World";
        public const string TerrainDataPath = GeneratedFolder + "/WorldTerrainData.asset";
        public string Name => "Terrain";

        public void Generate(WorldGenerationContext context)
        {
            EnsureFolders();
            TerrainData oldData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (oldData != null) AssetDatabase.DeleteAsset(TerrainDataPath);

            WorldGenerationSettings settings = context.settings;
            TerrainData data = new()
            {
                heightmapResolution = settings.heightmapResolution,
                alphamapResolution = settings.alphamapResolution,
                baseMapResolution = Mathf.Min(1024, settings.alphamapResolution * 2),
                size = new Vector3(settings.terrainSize, settings.terrainHeight, settings.terrainSize)
            };
            AssetDatabase.CreateAsset(data, TerrainDataPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain_Generated";
            terrainObject.transform.SetParent(context.root.transform, false);
            Undo.RegisterCreatedObjectUndo(terrainObject, "Generate Simply Fly terrain");
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 1800f;
            terrain.detailObjectDistance = 400f;
            context.terrain = terrain;
            context.terrainData = data;

            float[,] heights = BuildHeights(settings);
            data.SetHeights(0, 0, heights);
            context.heights = heights;
            EditorUtility.SetDirty(data);
        }

        public static float[,] BuildHeights(WorldGenerationSettings settings)
        {
            int resolution = settings.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float worldX = x / (float)(resolution - 1) * settings.terrainSize;
                float worldZ = z / (float)(resolution - 1) * settings.terrainSize;
                float broad = WorldGenerationMath.FractalNoise(worldX, worldZ,
                    settings.seed, settings.noiseScale, settings.noiseOctaves);
                float ridges = WorldGenerationMath.FractalNoise(worldX + 817f,
                    worldZ - 193f, settings.seed ^ 0x45d9f3b,
                    settings.noiseScale * 0.42f, 3);
                float centered = Mathf.Clamp01((broad - 0.28f) / 0.58f);
                // Compress low values into broad, flatter valleys while retaining
                // rounded hill tops. There are no mesh or transform shortcuts here.
                float valley = Mathf.SmoothStep(0f, 1f, centered);
                valley = Mathf.Lerp(valley * valley, valley,
                    1f - settings.valleyFlattening);
                float detail = (ridges - 0.5f) * 0.08f * valley;
                heights[z, x] = Mathf.Clamp01(0.035f +
                    (valley * 0.82f + detail) * settings.hillStrength);
            }
            return heights;
        }

        public static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SimplyFly/Generated"))
                AssetDatabase.CreateFolder("Assets/SimplyFly", "Generated");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/SimplyFly/Generated", "World");
        }
    }
}
