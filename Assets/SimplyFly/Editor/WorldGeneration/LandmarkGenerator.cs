using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class LandmarkGenerator : IWorldGenerator
    {
        public string Name => "Landmarks";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableLandmarks || context.settings.landmarkCount <= 0) return;
            GameObject root = new("Landmarks"); Undo.RegisterCreatedObjectUndo(root, "Generate landmarks");
            root.transform.SetParent(context.root.transform, false);
            System.Random random = new(context.settings.seed + context.settings.landmarkSeedOffset);
            List<LandmarkTypes> enabled = EnabledTypes(context.settings.enabledLandmarkTypes);
            for (int i = 0; i < context.settings.landmarkCount && enabled.Count > 0; i++)
            {
                LandmarkTypes type = enabled[random.Next(enabled.Count)];
                if (!TryFindLocation(context, type, random, out Vector3 point)) continue;
                string id = $"LMK-{context.landmarks.Count + 1:00}";
                string display = type.ToString();
                LandmarkData data = new() { navigationId = id, displayName = display,
                    type = type, position = point,
                    visibilityRadius = Mathf.Lerp(1800f, 5000f,
                        context.settings.landmarkVisibilityPriority) };
                context.landmarks.Add(data);
                GameObject prefab = PrefabFor(context.settings, type);
                Vector3 size = PlaceholderSize(type);
                GameObject instance = Phase3GenerationUtility.PlacePrefabOrPlaceholder(
                    id + "_" + display, prefab == null ? null : new[] { prefab }, root.transform,
                    point + Vector3.up * size.y * 0.5f, Quaternion.identity, size, random,
                    LandmarkColor(type), PrimitiveFor(type));
                instance.name = id + "_" + display;
            }
        }

        private static bool TryFindLocation(WorldGenerationContext context, LandmarkTypes type,
            System.Random random, out Vector3 best)
        {
            best = default; float bestScore = float.NegativeInfinity;
            for (int i = 0; i < 220; i++)
            {
                Vector3 point = context.NormalizedToWorld(
                    Mathf.Lerp(0.04f, 0.96f, (float)random.NextDouble()),
                    Mathf.Lerp(0.04f, 0.96f, (float)random.NextDouble()));
                if (WorldGenerationMath.InAirportOrApproach(context.settings,
                    point.x, point.z, context.settings.townAirportExclusionDistance * 0.6f)) continue;
                if (context.IsInsidePhase2Exclusion(point, 25f)) continue;
                bool insideTown = false;
                foreach (SettlementData town in context.settlements)
                    if (Vector3.Distance(point, town.center) < town.radius)
                    { insideTown = true; break; }
                bool townAppropriate = type == LandmarkTypes.ChurchTower ||
                    type == LandmarkTypes.FactoryChimney || type == LandmarkTypes.FuelStation;
                if (insideTown && !townAppropriate) continue;
                bool spaced = true; foreach (LandmarkData landmark in context.landmarks)
                    if (Vector3.Distance(point, landmark.position) < context.settings.minimumLandmarkSpacing)
                    { spaced = false; break; }
                if (!spaced) continue;
                float elevation = point.y;
                float roadDistance = Phase3GenerationUtility.DistanceToRoad(context, point);
                float waterDistance = Mathf.Min(
                    WorldGenerationMath.DistanceToPathXZ(point, context.riverPath),
                    DistanceToBoundary(point, context.lakeBoundary));
                float townDistance = float.PositiveInfinity;
                foreach (SettlementData town in context.settlements)
                    townDistance = Mathf.Min(townDistance, Vector3.Distance(point, town.center));
                float score = type switch
                {
                    LandmarkTypes.Lighthouse => -waterDistance * 4f + elevation,
                    LandmarkTypes.FuelStation => -roadDistance * 5f,
                    LandmarkTypes.ChurchTower => -townDistance * 3f,
                    LandmarkTypes.FactoryChimney => -townDistance * 2f - roadDistance,
                    LandmarkTypes.IsolatedBarn => townDistance - roadDistance * 0.4f,
                    LandmarkTypes.Quarry => elevation * 2f + roadDistance * 0.2f,
                    _ => elevation * 4f + roadDistance * 0.1f
                };
                if (score > bestScore) { best = point; bestScore = score; }
            }
            return bestScore > float.NegativeInfinity;
        }

        private static List<LandmarkTypes> EnabledTypes(LandmarkTypes mask)
        {
            List<LandmarkTypes> result = new();
            foreach (LandmarkTypes type in System.Enum.GetValues(typeof(LandmarkTypes)))
                if (type != LandmarkTypes.All && type != 0 && mask.HasFlag(type)) result.Add(type);
            return result;
        }

        private static float DistanceToBoundary(Vector3 point, List<Vector3> boundary)
        { if (boundary.Count < 2) return float.PositiveInfinity;
          List<Vector3> loop = new(boundary) { boundary[0] };
          return WorldGenerationMath.DistanceToPathXZ(point, loop); }
        private static GameObject PrefabFor(WorldGenerationSettings s, LandmarkTypes type) => type switch
        { LandmarkTypes.WaterTower => s.waterTowerPrefab, LandmarkTypes.RadioMast => s.radioMastPrefab,
          LandmarkTypes.WindTurbine => s.windTurbinePrefab, LandmarkTypes.ChurchTower => s.churchTowerPrefab,
          LandmarkTypes.FactoryChimney => s.factoryChimneyPrefab, LandmarkTypes.Quarry => s.quarryPrefab,
          LandmarkTypes.Lighthouse => s.lighthousePrefab, LandmarkTypes.HilltopAntenna => s.hilltopAntennaPrefab,
          LandmarkTypes.IsolatedBarn => s.isolatedBarnPrefab, _ => s.fuelStationPrefab };
        private static Vector3 PlaceholderSize(LandmarkTypes type) => type switch
        { LandmarkTypes.RadioMast or LandmarkTypes.HilltopAntenna => new Vector3(5f, 85f, 5f),
          LandmarkTypes.WindTurbine => new Vector3(8f, 65f, 8f),
          LandmarkTypes.FactoryChimney => new Vector3(12f, 55f, 12f),
          LandmarkTypes.Lighthouse or LandmarkTypes.ChurchTower => new Vector3(14f, 42f, 14f),
          LandmarkTypes.WaterTower => new Vector3(18f, 34f, 18f),
          LandmarkTypes.Quarry => new Vector3(90f, 4f, 90f), _ => new Vector3(28f, 12f, 34f) };
        private static PrimitiveType PrimitiveFor(LandmarkTypes type) =>
            type is LandmarkTypes.RadioMast or LandmarkTypes.HilltopAntenna or
                LandmarkTypes.FactoryChimney or LandmarkTypes.Lighthouse or
                LandmarkTypes.WaterTower ? PrimitiveType.Cylinder : PrimitiveType.Cube;
        private static Color LandmarkColor(LandmarkTypes type) =>
            type == LandmarkTypes.Lighthouse ? Color.white :
            type == LandmarkTypes.RadioMast ? new Color(0.75f, 0.15f, 0.12f) :
            new Color(0.48f, 0.48f, 0.45f);
    }
}
