using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class FarmGenerator : IWorldGenerator
    {
        public string Name => "Farmland";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableFarms) return;
            GameObject root = new("Farmland"); Undo.RegisterCreatedObjectUndo(root, "Generate farmland");
            root.transform.SetParent(context.root.transform, false);
            System.Random random = new(context.settings.seed + context.settings.farmlandSeedOffset);
            int target = Mathf.Clamp(Mathf.RoundToInt(context.settings.farmlandCoverage * 36f), 1, 28);
            for (int attempt = 0; attempt < target * 25 && context.fieldBoundaries.Count < target; attempt++)
            {
                Vector3 center = FindCandidate(context, random);
                float size = Mathf.Lerp(context.settings.minimumFieldSize,
                    context.settings.maximumFieldSize, (float)random.NextDouble());
                List<Vector3> boundary = BuildFieldBoundary(context, center, size, random);
                if (!IsFieldClear(context, boundary, center, size)) continue;
                context.fieldBoundaries.Add(boundary);
                CreateField(context, root.transform, boundary,
                    $"Field_{context.fieldBoundaries.Count:00}");
                MarkFarmlandBiome(context, boundary);
                if (random.NextDouble() < context.settings.farmBuildingDensity)
                    PlaceFarmCluster(context, root.transform, center, random);
                if (random.NextDouble() < 0.45) CreateAccessTrack(context, root.transform, center);
            }
        }

        public static List<Vector3> BuildFieldBoundary(WorldGenerationContext context,
            Vector3 center, float size, System.Random random)
        {
            int corners = random.Next(5, 9); float rotation = (float)random.NextDouble() * Mathf.PI;
            List<Vector3> boundary = new(corners);
            for (int i = 0; i < corners; i++)
            {
                float angle = rotation + i / (float)corners * Mathf.PI * 2f;
                float variation = Mathf.Lerp(1f - context.settings.fieldIrregularity,
                    1f + context.settings.fieldIrregularity, (float)random.NextDouble());
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) *
                    size * 0.5f * variation;
                point.y = context.terrain.SampleHeight(point) + 0.08f;
                boundary.Add(point);
            }
            return boundary;
        }

        private static Vector3 FindCandidate(WorldGenerationContext context, System.Random random)
        {
            Vector3 best = default; float bestScore = float.NegativeInfinity;
            for (int i = 0; i < 80; i++)
            {
                Vector3 point = context.NormalizedToWorld(
                    Mathf.Lerp(0.06f, 0.94f, (float)random.NextDouble()),
                    Mathf.Lerp(0.06f, 0.94f, (float)random.NextDouble()));
                float nx = point.x / context.settings.terrainSize;
                float nz = point.z / context.settings.terrainSize;
                float slope = context.terrainData.GetSteepness(nx, nz);
                float roadDistance = Phase3GenerationUtility.DistanceToRoad(context, point);
                int bx = Mathf.Clamp(Mathf.RoundToInt(nx * (context.biomes.GetLength(1) - 1)), 0,
                    context.biomes.GetLength(1) - 1);
                int bz = Mathf.Clamp(Mathf.RoundToInt(nz * (context.biomes.GetLength(0) - 1)), 0,
                    context.biomes.GetLength(0) - 1);
                float biome = context.biomes[bz, bx] == WorldBiome.Forest ? -800f :
                    context.biomes[bz, bx] == WorldBiome.RockyHills ? -1200f : 100f;
                float score = biome - slope * 80f - Mathf.Abs(roadDistance - 100f) * 1.4f;
                if (score > bestScore) { best = point; bestScore = score; }
            }
            return best;
        }

        private static bool IsFieldClear(WorldGenerationContext context,
            List<Vector3> boundary, Vector3 center, float size)
        {
            if (WorldGenerationMath.InAirportOrApproach(context.settings,
                center.x, center.z, size)) return false;
            float roadDistance = Phase3GenerationUtility.DistanceToRoad(context, center);
            if (roadDistance < size * 0.32f) return false;
            if (WorldGenerationMath.DistanceToPathXZ(center, context.riverPath) <
                size * 0.55f + context.settings.riverBankWidth) return false;
            if (context.lakeBoundary.Count > 2 &&
                WorldGenerationMath.PointInPolygonXZ(center, context.lakeBoundary)) return false;
            foreach (WorldFootprint footprint in context.buildingFootprints)
                if (footprint.ContainsXZ(center, size * 0.5f)) return false;
            foreach (SettlementData settlement in context.settlements)
                if (Vector3.Distance(center, settlement.center) < settlement.radius + size) return false;
            foreach (List<Vector3> existing in context.fieldBoundaries)
                if (WorldGenerationMath.PointInPolygonXZ(center, existing)) return false;
            float nx = center.x / context.settings.terrainSize;
            float nz = center.z / context.settings.terrainSize;
            return context.terrainData.GetSteepness(nx, nz) <=
                context.settings.maximumBuildingSlope * 1.35f;
        }

        private static void CreateField(WorldGenerationContext context, Transform parent,
            List<Vector3> boundary, string name)
        {
            Vector3 center = Vector3.zero; foreach (Vector3 point in boundary) center += point;
            center /= boundary.Count;
            Vector3[] vertices = new Vector3[boundary.Count + 1]; vertices[0] = center;
            int[] triangles = new int[boundary.Count * 3];
            for (int i = 0; i < boundary.Count; i++)
            {
                vertices[i + 1] = boundary[i]; triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % boundary.Count + 1;
            }
            GameObject field = new(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(field, "Generate field"); field.transform.SetParent(parent, false);
            Mesh mesh = new() { name = "Shared Field Layout Mesh", vertices = vertices,
                triangles = triangles }; mesh.RecalculateNormals();
            field.GetComponent<MeshFilter>().sharedMesh = mesh;
            field.GetComponent<MeshRenderer>().sharedMaterial = Phase2GenerationUtility.ResolveMaterial(
                null, "Farmland", new Color(0.48f, 0.42f, 0.16f));
            GameObjectUtility.SetStaticEditorFlags(field, StaticEditorFlags.BatchingStatic);
        }

        private static void PlaceFarmCluster(WorldGenerationContext context, Transform parent,
            Vector3 center, System.Random random)
        {
            Vector3 point = center; point.y = context.terrain.SampleHeight(point);
            WorldFootprint footprint = new() { center = point, size = new Vector2(24f, 32f),
                zone = SettlementZone.Rural };
            if (!Phase3GenerationUtility.IsFootprintClear(context, footprint)) return;
            Phase3GenerationUtility.FlattenFootprint(context, footprint);
            Phase3GenerationUtility.PlacePrefabOrPlaceholder("Farm_Barn",
                context.settings.barnPrefabs, parent, point + Vector3.up * 6f,
                Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f),
                new Vector3(24f, 12f, 32f), random, new Color(0.48f, 0.22f, 0.12f));
            context.buildingFootprints.Add(footprint);
            if (random.NextDouble() < 0.55)
                Phase3GenerationUtility.PlacePrefabOrPlaceholder("Farm_Silo",
                    context.settings.siloPrefabs, parent, point + new Vector3(22f, 8f, 4f),
                    Quaternion.identity, new Vector3(8f, 16f, 8f), random,
                    new Color(0.55f, 0.55f, 0.52f), PrimitiveType.Cylinder);
            if (random.NextDouble() < 0.35)
                Phase3GenerationUtility.PlacePrefabOrPlaceholder("Hay_Bale",
                    context.settings.hayBalePrefabs, parent, point + new Vector3(-18f, 1.2f, 8f),
                    Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 90f),
                    new Vector3(2f, 3f, 2f), random,
                    new Color(0.7f, 0.55f, 0.16f), PrimitiveType.Cylinder);
            if (random.NextDouble() < 0.22)
                Phase3GenerationUtility.PlacePrefabOrPlaceholder("Tractor",
                    context.settings.tractorPrefabs, parent, point + new Vector3(10f, 1.2f, -16f),
                    Quaternion.Euler(0f, (float)random.NextDouble() * 180f, 0f),
                    new Vector3(3f, 2.4f, 5f), random,
                    new Color(0.12f, 0.38f, 0.12f));
        }

        private static void CreateAccessTrack(WorldGenerationContext context, Transform parent,
            Vector3 center)
        {
            Vector3 road = Phase3GenerationUtility.NearestPointOnRoad(context, center, out _);
            List<Vector3> path = new() { road, Vector3.Lerp(road, center, 0.5f), center };
            path = Phase2GenerationUtility.SmoothPath(path, 2);
            Phase2GenerationUtility.ConformToTerrain(context, path, 0.1f);
            context.settlementRoads.Add(path); context.roadPaths.Add(path);
            Phase2GenerationUtility.CreateStrip("Farm_Access_Track", parent, path, 4f,
                Phase2GenerationUtility.ResolveMaterial(context.settings.shoulderMaterial,
                    "FarmTrack", new Color(0.3f, 0.22f, 0.12f)), 0.03f);
        }

        private static void MarkFarmlandBiome(WorldGenerationContext context, List<Vector3> boundary)
        {
            for (int z = 0; z < context.biomes.GetLength(0); z++)
            for (int x = 0; x < context.biomes.GetLength(1); x++)
            {
                Vector3 point = context.NormalizedToWorld(x / (float)(context.biomes.GetLength(1) - 1),
                    z / (float)(context.biomes.GetLength(0) - 1));
                if (WorldGenerationMath.PointInPolygonXZ(point, boundary))
                    context.biomes[z, x] = WorldBiome.Farmland;
            }
        }
    }
}
