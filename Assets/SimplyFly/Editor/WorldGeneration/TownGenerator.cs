using UnityEditor;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class TownGenerator : IWorldGenerator
    {
        public string Name => "Town buildings";
        public void Generate(WorldGenerationContext context)
        {
            if (!context.settings.enableTowns) return;
            GameObject settlementsRoot = new("Settlements");
            Undo.RegisterCreatedObjectUndo(settlementsRoot, "Generate settlements");
            settlementsRoot.transform.SetParent(context.root.transform, false);
            System.Random random = new(context.settings.seed + context.settings.townSeedOffset + 1709);
            foreach (SettlementData settlement in context.settlements)
                GenerateSettlement(context, settlement, settlementsRoot.transform, random);
        }

        private static void GenerateSettlement(WorldGenerationContext context,
            SettlementData settlement, Transform parent, System.Random random)
        {
            GameObject root = new(settlement.name.Replace(' ', '_'));
            Undo.RegisterCreatedObjectUndo(root, "Generate " + settlement.name);
            root.transform.SetParent(parent, false);
            GameObject centre = CreateZone("Town_Centre", root.transform);
            GameObject residential = CreateZone("Residential", root.transform);
            GameObject industrial = CreateZone("Industrial_Edge", root.transform);
            GameObject rural = CreateZone("Rural_Edge", root.transform);
            float density = context.settings.buildingDensity * (settlement.isVillage ? 0.56f : 1f);
            int target = Mathf.Clamp(Mathf.RoundToInt(settlement.radius * 0.16f * density),
                settlement.isVillage ? 8 : 18, settlement.isVillage ? 28 : 90);
            int accepted = 0;
            for (int attempt = 0; attempt < target * 14 && accepted < target; attempt++)
            {
                float normalizedRadius = Mathf.Sqrt((float)random.NextDouble());
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = normalizedRadius * settlement.radius * 0.88f;
                Vector3 point = settlement.center + new Vector3(Mathf.Cos(angle), 0f,
                    Mathf.Sin(angle)) * radius;
                point.y = context.terrain.SampleHeight(point) + context.terrain.transform.position.y;
                float nx = point.x / context.settings.terrainSize;
                float nz = point.z / context.settings.terrainSize;
                float slope = context.terrainData.GetSteepness(nx, nz);
                if (slope > context.settings.maximumBuildingSlope)
                { context.slopeViolations.Add(point); continue; }
                SettlementZone zone = ZoneFor(normalizedRadius, angle);
                Vector2 size = SizeFor(zone, random);
                WorldFootprint footprint = new() { center = point, size = size,
                    zone = zone, heading = 0f };
                if (!Phase3GenerationUtility.IsFootprintClear(context, footprint)) continue;
                Vector3 nearestRoad = Phase3GenerationUtility.NearestPointOnRoad(context,
                    point, out Vector3 tangent);
                Vector3 facing = Vector3.ProjectOnPlane(nearestRoad - point, Vector3.up);
                if (facing.sqrMagnitude < 0.1f) facing = Vector3.Cross(Vector3.up, tangent);
                footprint.heading = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                Phase3GenerationUtility.FlattenFootprint(context, footprint);
                point.y = context.terrain.SampleHeight(point) + context.terrain.transform.position.y;
                GameObject[] prefabs = PrefabsFor(context.settings, zone, random);
                Vector3 dimensions = new(size.x, HeightFor(zone, random), size.y);
                GameObject building = Phase3GenerationUtility.PlacePrefabOrPlaceholder(
                    CategoryName(zone), prefabs, ZoneRoot(zone, centre, residential, industrial, rural),
                    point + Vector3.up * dimensions.y * 0.5f,
                    Quaternion.Euler(0f, footprint.heading, 0f), dimensions, random,
                    ColorFor(zone));
                Phase3GenerationUtility.AddOptionalLodGroupHook(building);
                context.buildingFootprints.Add(footprint); accepted++;
                if (context.settings.decorativePropPrefabs != null &&
                    context.settings.decorativePropPrefabs.Length > 0 && random.NextDouble() < 0.10)
                {
                    Vector3 propPoint = point + Quaternion.Euler(0f,
                        (float)random.NextDouble() * 360f, 0f) * Vector3.forward *
                        (footprint.Radius + 3f);
                    propPoint.y = context.terrain.SampleHeight(propPoint) + 0.75f;
                    Phase3GenerationUtility.PlacePrefabOrPlaceholder("Decorative_Prop",
                        context.settings.decorativePropPrefabs, root.transform, propPoint,
                        Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f),
                        new Vector3(1.5f, 1.5f, 1.5f), random,
                        new Color(0.25f, 0.45f, 0.16f), PrimitiveType.Sphere);
                }
            }
        }

        private static SettlementZone ZoneFor(float radius, float angle)
        {
            if (radius < 0.28f) return SettlementZone.Centre;
            if (radius < 0.68f) return SettlementZone.Residential;
            return Mathf.Sin(angle) > 0.25f ? SettlementZone.Industrial : SettlementZone.Rural;
        }

        private static Vector2 SizeFor(SettlementZone zone, System.Random random)
        {
            float variation = Mathf.Lerp(0.85f, 1.18f, (float)random.NextDouble());
            return zone switch
            {
                SettlementZone.Centre => new Vector2(18f, 25f) * variation,
                SettlementZone.Industrial => new Vector2(34f, 45f) * variation,
                SettlementZone.Rural => new Vector2(24f, 32f) * variation,
                _ => new Vector2(16f, 22f) * variation
            };
        }

        private static float HeightFor(SettlementZone zone, System.Random random) =>
            (zone == SettlementZone.Centre ? 12f : zone == SettlementZone.Industrial ? 10f : 8f) *
            Mathf.Lerp(0.8f, 1.25f, (float)random.NextDouble());

        private static GameObject[] PrefabsFor(WorldGenerationSettings s,
            SettlementZone zone, System.Random random) => zone switch
        {
            SettlementZone.Centre => random.NextDouble() < 0.7 ? s.shopPrefabs : s.publicBuildingPrefabs,
            SettlementZone.Industrial => s.warehousePrefabs,
            SettlementZone.Rural => random.NextDouble() < 0.65 ? s.barnPrefabs : s.smallHousePrefabs,
            _ => random.NextDouble() < 0.7 ? s.smallHousePrefabs : s.mediumHousePrefabs
        };

        private static string CategoryName(SettlementZone zone) => zone switch
        {
            SettlementZone.Centre => "Shop_PublicBuilding",
            SettlementZone.Industrial => "Warehouse_Factory",
            SettlementZone.Rural => "Barn_Farmhouse", _ => "House"
        };
        private static Color ColorFor(SettlementZone zone) => zone switch
        {
            SettlementZone.Centre => new Color(0.62f, 0.48f, 0.34f),
            SettlementZone.Industrial => new Color(0.34f, 0.37f, 0.4f),
            SettlementZone.Rural => new Color(0.52f, 0.32f, 0.18f),
            _ => new Color(0.72f, 0.68f, 0.58f)
        };
        private static Transform ZoneRoot(SettlementZone zone, GameObject centre,
            GameObject residential, GameObject industrial, GameObject rural) => zone switch
        { SettlementZone.Centre => centre.transform, SettlementZone.Industrial => industrial.transform,
          SettlementZone.Rural => rural.transform, _ => residential.transform };
        private static GameObject CreateZone(string name, Transform parent)
        { GameObject zone = new(name); Undo.RegisterCreatedObjectUndo(zone, "Create settlement zone");
          zone.transform.SetParent(parent, false); return zone; }
    }
}
