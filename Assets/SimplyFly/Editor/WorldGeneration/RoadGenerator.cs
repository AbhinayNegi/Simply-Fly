using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class RoadGenerator : IWorldGenerator
    {
        public string Name => "Roads and bridges";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableRoads) return;
            WorldGenerationSettings s = context.settings;
            System.Random random = new(s.seed + s.roadSeedOffset);
            GameObject roadsRoot = new("Roads"); Undo.RegisterCreatedObjectUndo(roadsRoot, "Generate roads");
            roadsRoot.transform.SetParent(context.root.transform, false);
            Vector3 start = context.airportPosition + context.airportRotation * Vector3.right *
                (s.airportWidth * 0.5f + s.roadShoulderWidth + 90f);
            start.y = context.terrain.SampleHeight(start) + context.terrain.transform.position.y;
            context.settlementTarget = FindSettlementTarget(context, random, start);
            List<Vector3> main = BuildCurvedPath(context, start, context.settlementTarget,
                random, s.roadCurveSmoothness);
            GenerateRoad(context, roadsRoot.transform, main, s.mainRoadWidth, "Main_Road");

            int branches = Mathf.Clamp(s.branchCount, 1, 3);
            for (int i = 0; i < branches; i++)
            {
                int index = Mathf.Clamp(Mathf.RoundToInt((i + 1f) / (branches + 1f) *
                    (main.Count - 1)), 1, main.Count - 2);
                Vector3 branchStart = main[index];
                Vector3 tangent = (main[Mathf.Min(main.Count - 1, index + 1)] -
                    main[Mathf.Max(0, index - 1)]).normalized;
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 branchEnd = branchStart + Vector3.Cross(Vector3.up, tangent) * side *
                    Mathf.Lerp(550f, 1100f, (float)random.NextDouble());
                branchEnd.x = Mathf.Clamp(branchEnd.x, 80f, s.terrainSize - 80f);
                branchEnd.z = Mathf.Clamp(branchEnd.z, 80f, s.terrainSize - 80f);
                branchEnd.y = context.terrain.SampleHeight(branchEnd);
                List<Vector3> branch = BuildCurvedPath(context, branchStart, branchEnd,
                    random, Mathf.Max(3, s.roadCurveSmoothness - 2));
                GenerateRoad(context, roadsRoot.transform, branch,
                    s.secondaryRoadWidth, $"Secondary_Road_{i + 1}");
            }
            PromoteFarmlandNearRoads(context);
            GenerateBridges(context);
        }

        public static List<Vector3> BuildCurvedPath(WorldGenerationContext context,
            Vector3 start, Vector3 end, System.Random random, int smoothness)
        {
            Vector3 direction = Vector3.ProjectOnPlane(end - start, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, direction.normalized);
            float offset = direction.magnitude * Mathf.Lerp(-0.16f, 0.16f,
                (float)random.NextDouble());
            Vector3 controlA = Vector3.Lerp(start, end, 0.32f) + side * offset;
            Vector3 controlB = Vector3.Lerp(start, end, 0.68f) - side * offset * 0.65f;
            int samples = Mathf.Clamp(smoothness * 12, 32, 160);
            List<Vector3> path = new(samples);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                float u = 1f - t;
                Vector3 point = u * u * u * start + 3f * u * u * t * controlA +
                    3f * u * t * t * controlB + t * t * t * end;
                point.x = Mathf.Clamp(point.x, 10f, context.settings.terrainSize - 10f);
                point.z = Mathf.Clamp(point.z, 10f, context.settings.terrainSize - 10f);
                path.Add(point);
            }
            Phase2GenerationUtility.ConformToTerrain(context, path);
            return path;
        }

        private static void GenerateRoad(WorldGenerationContext context, Transform parent,
            List<Vector3> path, float width, string name)
        {
            ValidateSlope(context, path);
            context.roadPaths.Add(path);
            Phase2GenerationUtility.ShapeTerrainAlongPath(context, path, width * 0.5f,
                context.settings.roadShoulderWidth + 12f, 0f);
            Phase2GenerationUtility.ConformToTerrain(context, path, 0.12f);
            Material shoulder = Phase2GenerationUtility.ResolveMaterial(
                context.settings.shoulderMaterial, "RoadShoulder", new Color(0.28f, 0.24f, 0.18f));
            Material road = Phase2GenerationUtility.ResolveMaterial(
                context.settings.roadMaterial, "Road", new Color(0.12f, 0.12f, 0.12f));
            Phase2GenerationUtility.CreateStrip(name + "_Shoulder", parent, path,
                width + context.settings.roadShoulderWidth * 2f, shoulder, 0.03f);
            Phase2GenerationUtility.CreateStrip(name, parent, path, width, road, 0.08f);
        }

        private static void ValidateSlope(WorldGenerationContext context, List<Vector3> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 delta = path[i] - path[i - 1];
                float slope = Mathf.Abs(Mathf.Atan2(delta.y,
                    Vector3.ProjectOnPlane(delta, Vector3.up).magnitude) * Mathf.Rad2Deg);
                if (slope > context.settings.maximumRoadSlope)
                    context.invalidRoadSegments.Add(Vector3.Lerp(path[i - 1], path[i], 0.5f));
            }
        }

        private static Vector3 FindSettlementTarget(WorldGenerationContext context,
            System.Random random, Vector3 start)
        {
            Vector3 best = start; float bestScore = float.NegativeInfinity;
            WorldGenerationSettings s = context.settings;
            for (int i = 0; i < 180; i++)
            {
                float nx = Mathf.Lerp(0.08f, 0.92f, (float)random.NextDouble());
                float nz = Mathf.Lerp(0.08f, 0.92f, (float)random.NextDouble());
                Vector3 point = context.NormalizedToWorld(nx, nz);
                if (WorldGenerationMath.InAirportOrApproach(s, point.x, point.z, 80f)) continue;
                Vector3 airportRight = context.airportRotation * Vector3.right;
                if (Vector3.Dot(point - context.airportPosition, airportRight) < 0f) continue;
                float distance = Vector3.Distance(start, point);
                float slope = context.terrainData.GetSteepness(nx, nz);
                int bx = Mathf.RoundToInt(nx * (context.biomes.GetLength(1) - 1));
                int bz = Mathf.RoundToInt(nz * (context.biomes.GetLength(0) - 1));
                float biomePenalty = context.biomes[bz, bx] == WorldBiome.RockyHills ? 900f :
                    context.biomes[bz, bx] == WorldBiome.Forest ? 350f : 0f;
                float score = Mathf.Min(distance, s.terrainSize * 0.65f) - slope * 45f - biomePenalty;
                if (distance > s.terrainSize * 0.25f && score > bestScore)
                { best = point; bestScore = score; }
            }
            return best;
        }

        private static void PromoteFarmlandNearRoads(WorldGenerationContext context)
        {
            for (int z = 0; z < context.biomes.GetLength(0); z++)
            for (int x = 0; x < context.biomes.GetLength(1); x++)
            {
                float nx = x / (float)(context.biomes.GetLength(1) - 1);
                float nz = z / (float)(context.biomes.GetLength(0) - 1);
                Vector3 point = context.NormalizedToWorld(nx, nz);
                float distance = float.PositiveInfinity;
                foreach (List<Vector3> path in context.roadPaths)
                    distance = Mathf.Min(distance, WorldGenerationMath.DistanceToPathXZ(point, path));
                if (distance > 45f || distance < context.settings.mainRoadWidth) continue;
                if (context.terrainData.GetSteepness(nx, nz) < 8f &&
                    context.biomes[z, x] == WorldBiome.Grassland)
                    context.biomes[z, x] = WorldBiome.Farmland;
            }
        }

        private static void GenerateBridges(WorldGenerationContext context)
        {
            if (context.riverPath.Count < 2) return;
            GameObject root = new("Bridges"); Undo.RegisterCreatedObjectUndo(root, "Generate bridges");
            root.transform.SetParent(context.root.transform, false);
            foreach (List<Vector3> road in context.roadPaths)
            for (int r = 1; r < road.Count; r++)
            for (int w = 1; w < context.riverPath.Count; w++)
            {
                if (!Phase2GenerationUtility.SegmentIntersectionXZ(road[r - 1], road[r],
                    context.riverPath[w - 1], context.riverPath[w], out Vector3 crossing)) continue;
                if (context.bridgeCrossings.Exists(p => Vector3.Distance(p, crossing) < 40f)) continue;
                crossing.y = Mathf.Max(road[r].y, context.riverPath[w].y + 1.5f);
                context.bridgeCrossings.Add(crossing);
                CreateBridge(context, root.transform, crossing, road[r] - road[r - 1]);
            }
        }

        private static void CreateBridge(WorldGenerationContext context, Transform parent,
            Vector3 crossing, Vector3 roadDirection)
        {
            GameObject bridge;
            bool validPrefab = context.settings.bridgePrefab != null &&
                PrefabUtility.GetPrefabAssetType(context.settings.bridgePrefab) != PrefabAssetType.NotAPrefab;
            if (validPrefab)
                bridge = PrefabUtility.InstantiatePrefab(context.settings.bridgePrefab, parent) as GameObject;
            else
            {
                bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bridge.name = "Bridge_Placeholder";
                bridge.transform.SetParent(parent, false);
                bridge.GetComponent<MeshRenderer>().sharedMaterial = Phase2GenerationUtility.ResolveMaterial(
                    context.settings.roadMaterial, "Bridge", new Color(0.32f, 0.3f, 0.28f));
            }
            if (bridge == null) return;
            Undo.RegisterCreatedObjectUndo(bridge, "Generate bridge");
            bridge.transform.SetPositionAndRotation(crossing,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(roadDirection, Vector3.up)));
            if (!validPrefab)
                bridge.transform.localScale = new Vector3(context.settings.mainRoadWidth + 2f,
                    0.8f, context.settings.riverWidth + context.settings.riverBankWidth * 1.4f);
        }
    }
}
