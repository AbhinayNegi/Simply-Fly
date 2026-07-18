using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public static class Phase3GenerationUtility
    {
        public static GameObject PlacePrefabOrPlaceholder(string category,
            GameObject[] prefabs, Transform parent, Vector3 position,
            Quaternion rotation, Vector3 placeholderSize, System.Random random,
            Color color, PrimitiveType primitive = PrimitiveType.Cube)
        {
            GameObject instance = null;
            if (prefabs != null && prefabs.Length > 0)
            {
                List<GameObject> valid = new();
                foreach (GameObject prefab in prefabs)
                    if (prefab != null && PrefabUtility.GetPrefabAssetType(prefab) !=
                        PrefabAssetType.NotAPrefab) valid.Add(prefab);
                if (valid.Count > 0)
                    instance = PrefabUtility.InstantiatePrefab(valid[random.Next(valid.Count)], parent) as GameObject;
            }
            if (instance == null)
            {
                instance = GameObject.CreatePrimitive(primitive);
                instance.name = category + "_Placeholder";
                instance.transform.SetParent(parent, false);
                instance.transform.localScale = placeholderSize;
                instance.GetComponent<Renderer>().sharedMaterial =
                    Phase2GenerationUtility.ResolveMaterial(null, category, color);
            }
            Undo.RegisterCreatedObjectUndo(instance, "Generate " + category);
            instance.transform.SetPositionAndRotation(position, rotation);
            GameObjectUtility.SetStaticEditorFlags(instance,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
            return instance;
        }

        public static void FlattenFootprint(WorldGenerationContext context,
            WorldFootprint footprint, float blend = 5f)
        {
            TerrainData data = context.terrainData; int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            float cell = context.settings.terrainSize / (resolution - 1f);
            float target = footprint.center.y / context.settings.terrainHeight;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                Vector3 point = new(x * cell, 0f, z * cell);
                float distance = Vector2.Distance(new Vector2(point.x, point.z),
                    new Vector2(footprint.center.x, footprint.center.z));
                if (distance > footprint.Radius + blend) continue;
                float weight = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(footprint.Radius, footprint.Radius + blend, distance));
                if (distance <= footprint.Radius) weight = 1f;
                heights[z, x] = Mathf.Lerp(heights[z, x], target, weight);
            }
            data.SetHeights(0, 0, heights); context.heights = heights;
            EditorUtility.SetDirty(data);
        }

        public static float DistanceToRoad(WorldGenerationContext context, Vector3 point)
        {
            float distance = float.PositiveInfinity;
            foreach (List<Vector3> path in context.roadPaths)
                distance = Mathf.Min(distance, WorldGenerationMath.DistanceToPathXZ(point, path));
            return distance;
        }

        public static Vector3 NearestPointOnRoad(WorldGenerationContext context,
            Vector3 point, out Vector3 tangent)
        {
            Vector3 best = point; tangent = Vector3.forward; float bestDistance = float.PositiveInfinity;
            foreach (List<Vector3> path in context.roadPaths)
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1]; Vector3 b = path[i];
                Vector2 av = new(a.x, a.z); Vector2 bv = new(b.x, b.z);
                Vector2 pv = new(point.x, point.z); Vector2 ab = bv - av;
                float t = ab.sqrMagnitude < 0.001f ? 0f :
                    Mathf.Clamp01(Vector2.Dot(pv - av, ab) / ab.sqrMagnitude);
                Vector3 candidate = Vector3.Lerp(a, b, t);
                float distance = Vector2.Distance(pv, Vector2.Lerp(av, bv, t));
                if (distance < bestDistance)
                { best = candidate; bestDistance = distance; tangent = (b - a).normalized; }
            }
            return best;
        }

        public static bool IsFootprintClear(WorldGenerationContext context,
            WorldFootprint footprint)
        {
            if (WorldGenerationMath.InAirportOrApproach(context.settings,
                footprint.center.x, footprint.center.z,
                context.settings.townAirportExclusionDistance)) return false;
            if (WorldGenerationMath.DistanceToPathXZ(footprint.center, context.riverPath) <
                footprint.Radius + context.settings.riverBankWidth) return false;
            if (context.lakeBoundary.Count > 2 &&
                WorldGenerationMath.PointInPolygonXZ(footprint.center, context.lakeBoundary)) return false;
            if (DistanceToRoad(context, footprint.center) < footprint.Radius +
                context.settings.roadShoulderWidth + 2f) return false;
            foreach (WorldFootprint existing in context.buildingFootprints)
                if (footprint.Overlaps(existing, context.settings.minimumBuildingSpacing)) return false;
            return true;
        }

        public static void AddOptionalLodGroupHook(GameObject instance)
        {
            // Prefab authors can add child renderers and call this hook from a
            // future postprocessor without adding scripts to every building.
            if (instance == null || instance.GetComponent<LODGroup>() != null) return;
        }
    }
}
