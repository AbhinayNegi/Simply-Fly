using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class TextureGenerator : IWorldGenerator
    {
        public string Name => "Terrain textures";

        public void Generate(WorldGenerationContext context)
        {
            WorldGenerationSettings settings = context.settings;
            TerrainGenerator.EnsureFolders();
            TerrainLayer grass = ResolveLayer(settings.grassLayer, "Grass", new Color(0.27f, 0.48f, 0.18f));
            TerrainLayer dirt = ResolveLayer(settings.dirtLayer, "Dirt", new Color(0.38f, 0.27f, 0.16f));
            TerrainLayer rock = ResolveLayer(settings.rockLayer, "Rock", new Color(0.34f, 0.35f, 0.34f));
            context.terrainData.terrainLayers = new[] { grass, dirt, rock };

            float[,,] weights = BuildWeights(context.terrainData, settings);
            ApplyPhase3Influence(context, weights);
            context.terrainData.SetAlphamaps(0, 0, weights);
            EditorUtility.SetDirty(context.terrainData);
        }

        private static void ApplyPhase3Influence(WorldGenerationContext context,
            float[,,] weights)
        {
            if (context.fieldBoundaries.Count == 0 && context.buildingFootprints.Count == 0) return;
            int height = weights.GetLength(0); int width = weights.GetLength(1);
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                Vector3 point = new(x / (float)(width - 1) * context.settings.terrainSize,
                    0f, z / (float)(height - 1) * context.settings.terrainSize);
                bool settlement = false;
                foreach (WorldFootprint footprint in context.buildingFootprints)
                    if (footprint.ContainsXZ(point, 5f)) { settlement = true; break; }
                bool field = false;
                if (!settlement) foreach (System.Collections.Generic.List<Vector3> boundary in
                    context.fieldBoundaries)
                    if (WorldGenerationMath.PointInPolygonXZ(point, boundary))
                    { field = true; break; }
                if (settlement) { weights[z, x, 0] = 0.12f; weights[z, x, 1] = 0.84f; weights[z, x, 2] = 0.04f; }
                else if (field) { weights[z, x, 0] = 0.42f; weights[z, x, 1] = 0.56f; weights[z, x, 2] = 0.02f; }
            }
        }

        public static float[,,] BuildWeights(TerrainData data,
            WorldGenerationSettings settings)
        {
            int resolution = data.alphamapResolution;
            float[,,] weights = new float[resolution, resolution, 3];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float nx = x / (float)(resolution - 1);
                float nz = z / (float)(resolution - 1);
                float slope = data.GetSteepness(nx, nz);
                float elevation = data.GetInterpolatedHeight(nx, nz) /
                    Mathf.Max(1f, settings.terrainHeight);
                float dirtWeight = WorldGenerationMath.SmoothBand(slope,
                    settings.dirtSlopeStart, settings.textureBlend * 90f);
                float rockBySlope = WorldGenerationMath.SmoothBand(slope,
                    settings.rockSlopeStart, settings.textureBlend * 90f);
                float rockByHeight = WorldGenerationMath.SmoothBand(elevation,
                    settings.rockElevationStart, settings.textureBlend);
                float rockWeight = Mathf.Max(rockBySlope, rockByHeight);
                dirtWeight *= 1f - rockWeight;
                float grassWeight = Mathf.Max(0f, 1f - dirtWeight - rockWeight);
                float sum = Mathf.Max(0.0001f, grassWeight + dirtWeight + rockWeight);
                weights[z, x, 0] = grassWeight / sum;
                weights[z, x, 1] = dirtWeight / sum;
                weights[z, x, 2] = rockWeight / sum;
            }
            return weights;
        }

        private static TerrainLayer ResolveLayer(TerrainLayer assigned,
            string name, Color color)
        {
            if (assigned != null) return assigned;
            string texturePath = $"{TerrainGenerator.GeneratedFolder}/{name}_Auto.png";
            string layerPath = $"{TerrainGenerator.GeneratedFolder}/{name}_Auto.terrainlayer";
            TerrainLayer existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (existing != null) return existing;

            Texture2D texture = new(4, 4, TextureFormat.RGBA32, true);
            texture.name = name + " Auto Texture";
            Color[] colors = new Color[16];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            texture.SetPixels(colors);
            texture.Apply();
            System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);

            TerrainLayer layer = new()
            {
                name = name + " Auto Layer",
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath),
                tileSize = new Vector2(24f, 24f)
            };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }
    }
}
