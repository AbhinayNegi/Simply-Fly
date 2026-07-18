using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class LakeGenerator : IWorldGenerator
    {
        public string Name => "Lake";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableLake) return;
            WorldGenerationSettings s = context.settings;
            System.Random random = new(s.seed + s.lakeSeedOffset);
            Vector3 center = FindLowRegion(context, random);
            float radius = Mathf.Lerp(s.lakeMinimumRadius, s.lakeMaximumRadius,
                (float)random.NextDouble());
            float surfaceY = center.y - s.lakeDepth * 0.15f;
            const int points = 40;
            for (int i = 0; i < points; i++)
            {
                float angle = i / (float)points * Mathf.PI * 2f;
                float irregular = 0.82f + 0.18f * Mathf.PerlinNoise(
                    i * 0.31f, (s.seed + s.lakeSeedOffset) * 0.001f);
                float r = radius * irregular;
                context.lakeBoundary.Add(new Vector3(center.x + Mathf.Cos(angle) * r,
                    surfaceY, center.z + Mathf.Sin(angle) * r));
            }
            CarveLake(context, center, radius, surfaceY);
            Transform waterRoot = EnsureWaterRoot(context).transform;
            CreateLakeMesh(context, waterRoot, center, surfaceY);
        }

        private static Vector3 FindLowRegion(WorldGenerationContext context, System.Random random)
        {
            WorldGenerationSettings s = context.settings;
            Vector3 best = context.NormalizedToWorld(0.2f, 0.2f);
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < 180; i++)
            {
                float nx = Mathf.Lerp(0.08f, 0.92f, (float)random.NextDouble());
                float nz = Mathf.Lerp(0.08f, 0.92f, (float)random.NextDouble());
                Vector3 point = context.NormalizedToWorld(nx, nz);
                if (WorldGenerationMath.InAirportOrApproach(s, point.x, point.z,
                    s.lakeMaximumRadius + s.lakeShoreBlendDistance)) continue;
                float slope = context.terrainData.GetSteepness(nx, nz);
                float score = point.y + slope * 8f;
                if (score < bestScore) { best = point; bestScore = score; }
            }
            return best;
        }

        private static void CarveLake(WorldGenerationContext context, Vector3 center,
            float radius, float surfaceY)
        {
            TerrainData data = context.terrainData; WorldGenerationSettings s = context.settings;
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            float cell = s.terrainSize / (resolution - 1f);
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 delta = new(x * cell - center.x, z * cell - center.z);
                float angle = Mathf.Atan2(delta.y, delta.x);
                float irregular = 0.82f + 0.18f * Mathf.PerlinNoise(
                    angle * 1.7f + 10f, (s.seed + s.lakeSeedOffset) * 0.001f);
                float localRadius = radius * irregular;
                float distance = delta.magnitude;
                if (distance > localRadius + s.lakeShoreBlendDistance) continue;
                float bed = (surfaceY - s.lakeDepth) / s.terrainHeight;
                float blend = distance <= localRadius ? 1f : 1f - Mathf.SmoothStep(0f, 1f,
                    (distance - localRadius) / Mathf.Max(1f, s.lakeShoreBlendDistance));
                heights[z, x] = Mathf.Lerp(heights[z, x], bed, blend);
            }
            data.SetHeights(0, 0, heights); context.heights = heights;
            EditorUtility.SetDirty(data);
        }

        private static void CreateLakeMesh(WorldGenerationContext context, Transform parent,
            Vector3 center, float surfaceY)
        {
            GameObject lake = new("Lake", typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(lake, "Generate lake"); lake.transform.SetParent(parent, false);
            int count = context.lakeBoundary.Count;
            Vector3[] vertices = new Vector3[count + 1]; Vector2[] uv = new Vector2[count + 1];
            int[] triangles = new int[count * 3]; vertices[0] = center + Vector3.up * (surfaceY - center.y);
            for (int i = 0; i < count; i++)
            {
                vertices[i + 1] = context.lakeBoundary[i];
                uv[i + 1] = new Vector2(vertices[i + 1].x, vertices[i + 1].z) * 0.01f;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % count + 1;
            }
            Mesh mesh = new() { name = "Lake Water Mesh", vertices = vertices,
                triangles = triangles, uv = uv }; mesh.RecalculateNormals();
            lake.GetComponent<MeshFilter>().sharedMesh = mesh;
            lake.GetComponent<MeshRenderer>().sharedMaterial = Phase2GenerationUtility.ResolveMaterial(
                context.settings.waterMaterial, "Water", new Color(0.08f, 0.38f, 0.58f, 0.75f));
        }

        public static GameObject EnsureWaterRoot(WorldGenerationContext context)
        {
            Transform existing = context.root.transform.Find("Water");
            if (existing != null) return existing.gameObject;
            GameObject water = new("Water"); Undo.RegisterCreatedObjectUndo(water, "Create water root");
            water.transform.SetParent(context.root.transform, false); return water;
        }
    }
}
