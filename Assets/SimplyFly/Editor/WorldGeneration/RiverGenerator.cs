using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class RiverGenerator : IWorldGenerator
    {
        public string Name => "River";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableRiver) return;
            List<Vector3> controls = BuildDownhillControls(context);
            List<Vector3> smooth = Phase2GenerationUtility.SmoothPath(controls,
                Mathf.Clamp(context.settings.riverCurveSmoothness / 2, 1, 5));
            Phase2GenerationUtility.ConformToTerrain(context, smooth, -0.25f);
            EnforceDownhill(smooth);
            context.riverPath.AddRange(smooth);
            Phase2GenerationUtility.ShapeTerrainAlongPath(context, context.riverPath,
                context.settings.riverWidth * 0.5f,
                context.settings.riverBankWidth, -context.settings.riverDepth, true);
            EnforceDownhill(context.riverPath);

            GameObject waterRoot = LakeGenerator.EnsureWaterRoot(context);
            GameObject riverRoot = new("River");
            Undo.RegisterCreatedObjectUndo(riverRoot, "Generate river");
            riverRoot.transform.SetParent(waterRoot.transform, false);
            Phase2GenerationUtility.CreateStrip("River_Water", riverRoot.transform,
                context.riverPath, context.settings.riverWidth,
                Phase2GenerationUtility.ResolveMaterial(context.settings.waterMaterial,
                    "Water", new Color(0.08f, 0.38f, 0.58f, 0.75f)), 0.12f);
        }

        public static List<Vector3> BuildDownhillControls(WorldGenerationContext context)
        {
            WorldGenerationSettings s = context.settings;
            System.Random random = new(s.seed + s.riverSeedOffset);
            Vector3 current = FindHighStart(context, random);
            List<Vector3> path = new() { current };
            Vector2 direction = NearestEdgeDirection(current, s.terrainSize);
            float step = Mathf.Max(70f, s.terrainSize / 45f);
            for (int iteration = 0; iteration < 90; iteration++)
            {
                Vector3 best = current; float bestScore = float.PositiveInfinity;
                Vector2 bestDirection = direction;
                for (int candidate = -3; candidate <= 3; candidate++)
                {
                    float angle = candidate * 18f + ((float)random.NextDouble() - 0.5f) * 9f;
                    Vector2 candidateDirection = WorldGenerationMath.Rotate(direction, angle).normalized;
                    Vector2 xz = new(current.x, current.z); xz += candidateDirection * step;
                    if (xz.x < 5f || xz.y < 5f || xz.x > s.terrainSize - 5f || xz.y > s.terrainSize - 5f)
                        continue;
                    if (WorldGenerationMath.InAirportOrApproach(s, xz.x, xz.y,
                        s.riverWidth + s.riverBankWidth)) continue;
                    Vector3 point = context.NormalizedToWorld(xz.x / s.terrainSize, xz.y / s.terrainSize);
                    float uphillPenalty = Mathf.Max(0f, point.y - current.y) * 20f;
                    float edgeDistance = Mathf.Min(xz.x, xz.y, s.terrainSize - xz.x, s.terrainSize - xz.y);
                    float score = point.y + uphillPenalty + edgeDistance * 0.012f;
                    if (context.lakeBoundary.Count > 2 &&
                        WorldGenerationMath.PointInPolygonXZ(point, context.lakeBoundary)) score -= 10000f;
                    if (score < bestScore) { best = point; bestScore = score; bestDirection = candidateDirection; }
                }
                if (best == current) break;
                current = best; direction = Vector2.Lerp(direction, bestDirection, 0.65f).normalized;
                path.Add(current);
                if (DistanceToEdge(current, s.terrainSize) < step * 1.2f ||
                    (context.lakeBoundary.Count > 2 &&
                     WorldGenerationMath.PointInPolygonXZ(current, context.lakeBoundary))) break;
            }
            return path;
        }

        private static Vector3 FindHighStart(WorldGenerationContext context, System.Random random)
        {
            Vector3 best = default; float bestScore = float.NegativeInfinity;
            WorldGenerationSettings s = context.settings;
            for (int i = 0; i < 220; i++)
            {
                float nx = Mathf.Lerp(0.12f, 0.88f, (float)random.NextDouble());
                float nz = Mathf.Lerp(0.12f, 0.88f, (float)random.NextDouble());
                Vector3 point = context.NormalizedToWorld(nx, nz);
                if (WorldGenerationMath.InAirportOrApproach(s, point.x, point.z, 400f)) continue;
                if (DistanceToEdge(point, s.terrainSize) < s.minimumRiverPathLength) continue;
                float score = point.y - context.terrainData.GetSteepness(nx, nz) * 0.7f;
                if (score > bestScore) { best = point; bestScore = score; }
            }
            return best;
        }

        public static void EnforceDownhill(List<Vector3> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 point = path[i];
                point.y = Mathf.Min(point.y, path[i - 1].y - 0.01f);
                path[i] = point;
            }
        }

        private static Vector2 NearestEdgeDirection(Vector3 point, float size)
        {
            float[] distances = { point.x, size - point.x, point.z, size - point.z };
            int best = 0; for (int i = 1; i < 4; i++) if (distances[i] < distances[best]) best = i;
            return best switch { 0 => Vector2.left, 1 => Vector2.right,
                2 => Vector2.down, _ => Vector2.up };
        }
        public static float DistanceToEdge(Vector3 point, float size) =>
            Mathf.Min(point.x, point.z, size - point.x, size - point.z);
    }
}
