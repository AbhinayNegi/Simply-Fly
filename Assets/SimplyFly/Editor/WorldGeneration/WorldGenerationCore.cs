using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor
{
    public enum WorldBiome : byte { Grassland, Forest, Farmland, RockyHills }
    public enum SettlementZone : byte { Centre, Residential, Industrial, Rural }
    [Flags]
    public enum LandmarkTypes
    {
        WaterTower = 1 << 0, RadioMast = 1 << 1, WindTurbine = 1 << 2,
        ChurchTower = 1 << 3, FactoryChimney = 1 << 4, Quarry = 1 << 5,
        Lighthouse = 1 << 6, HilltopAntenna = 1 << 7,
        IsolatedBarn = 1 << 8, FuelStation = 1 << 9, All = (1 << 10) - 1
    }

    [Serializable]
    public sealed class WorldGenerationSettings
    {
        [Header("World")]
        public int seed = 12345;
        [Min(512f)] public float terrainSize = 6000f;
        [Min(50f)] public float terrainHeight = 600f;
        [Range(33, 2049)] public int heightmapResolution = 513;
        [Range(16, 1024)] public int alphamapResolution = 256;
        [Min(100f)] public float noiseScale = 1500f;
        [Range(0f, 1f)] public float hillStrength = 0.28f;
        [Range(2, 8)] public int noiseOctaves = 5;
        [Range(0.1f, 0.9f)] public float valleyFlattening = 0.42f;

        [Header("Airport")]
        [Min(300f)] public float airportLength = 1400f;
        [Min(200f)] public float airportWidth = 700f;
        [Range(0f, 360f)] public float airportHeading = 25f;
        public Vector2 airportNormalizedPosition = new(0.5f, 0.5f);
        [Min(0f)] public float airportBlendMargin = 180f;
        [Min(500f)] public float approachClearLength = 1800f;
        [Min(50f)] public float approachClearWidth = 260f;

        [Header("Terrain texturing")]
        [Range(0f, 90f)] public float dirtSlopeStart = 16f;
        [Range(0f, 90f)] public float rockSlopeStart = 32f;
        [Range(0f, 1f)] public float rockElevationStart = 0.62f;
        [Range(0.001f, 0.25f)] public float textureBlend = 0.08f;
        public TerrainLayer grassLayer;
        public TerrainLayer dirtLayer;
        public TerrainLayer rockLayer;

        [Header("Biomes and placement")]
        [Min(100f)] public float biomeScale = 1900f;
        [Range(0f, 1f)] public float forestThreshold = 0.57f;
        [Range(0f, 1f)] public float farmlandThreshold = 0.44f;
        [Range(0f, 1f)] public float rockyThreshold = 0.66f;
        [Range(0f, 1f)] public float forestDensity = 0.45f;
        [Range(0f, 1f)] public float rockDensity = 0.24f;
        [Min(4f)] public float placementSpacing = 28f;
        public GameObject[] treePrefabs;
        public GameObject[] rockPrefabs;

        [Header("Phase 2 - Roads")]
        public bool enableRoads = true;
        [Min(3f)] public float mainRoadWidth = 12f;
        [Min(2f)] public float secondaryRoadWidth = 7f;
        [Min(0f)] public float roadShoulderWidth = 3f;
        [Range(1f, 30f)] public float maximumRoadSlope = 10f;
        [Range(2, 12)] public int roadCurveSmoothness = 7;
        [Range(1, 3)] public int branchCount = 2;
        public Material roadMaterial;
        public Material shoulderMaterial;
        public GameObject bridgePrefab;
        public int roadSeedOffset = 27011;

        [Header("Phase 2 - River")]
        public bool enableRiver = true;
        [Min(5f)] public float riverWidth = 24f;
        [Min(0.25f)] public float riverDepth = 3.5f;
        [Min(2f)] public float riverBankWidth = 18f;
        [Range(2, 12)] public int riverCurveSmoothness = 6;
        public Material waterMaterial;
        [Min(200f)] public float minimumRiverPathLength = 1200f;
        public int riverSeedOffset = 48109;

        [Header("Phase 2 - Lake")]
        public bool enableLake = true;
        [Min(20f)] public float lakeMinimumRadius = 130f;
        [Min(25f)] public float lakeMaximumRadius = 260f;
        [Min(0.25f)] public float lakeDepth = 5f;
        [Min(5f)] public float lakeShoreBlendDistance = 55f;
        public int lakeSeedOffset = 71933;

        [Header("Phase 3 - Towns")]
        public bool enableTowns = true;
        [Min(120f)] public float mainTownSize = 520f;
        [Range(0, 3)] public int villageCount = 2;
        [Range(0.05f, 1f)] public float buildingDensity = 0.42f;
        [Min(4f)] public float minimumBuildingSpacing = 9f;
        [Range(1f, 25f)] public float maximumBuildingSlope = 7f;
        [Min(20f)] public float townRoadProximityRange = 180f;
        [Min(100f)] public float townAirportExclusionDistance = 900f;
        public int townSeedOffset = 93169;
        public GameObject[] smallHousePrefabs;
        public GameObject[] mediumHousePrefabs;
        public GameObject[] shopPrefabs;
        public GameObject[] warehousePrefabs;
        public GameObject[] barnPrefabs;
        public GameObject[] publicBuildingPrefabs;
        public GameObject[] decorativePropPrefabs;

        [Header("Phase 3 - Farms")]
        public bool enableFarms = true;
        [Range(0f, 1f)] public float farmlandCoverage = 0.22f;
        [Min(40f)] public float minimumFieldSize = 90f;
        [Min(60f)] public float maximumFieldSize = 240f;
        [Range(0f, 1f)] public float fieldIrregularity = 0.32f;
        [Range(0f, 1f)] public float farmBuildingDensity = 0.16f;
        [Range(0f, 1f)] public float fenceDensity = 0.08f;
        public int farmlandSeedOffset = 112213;
        public GameObject[] siloPrefabs;
        public GameObject[] hayBalePrefabs;
        public GameObject[] tractorPrefabs;

        [Header("Phase 3 - Landmarks")]
        public bool enableLandmarks = true;
        [Range(0, 24)] public int landmarkCount = 8;
        [Min(100f)] public float minimumLandmarkSpacing = 650f;
        [Range(0f, 1f)] public float landmarkVisibilityPriority = 0.75f;
        public LandmarkTypes enabledLandmarkTypes = LandmarkTypes.All;
        public int landmarkSeedOffset = 143053;
        public GameObject waterTowerPrefab;
        public GameObject radioMastPrefab;
        public GameObject windTurbinePrefab;
        public GameObject churchTowerPrefab;
        public GameObject factoryChimneyPrefab;
        public GameObject quarryPrefab;
        public GameObject lighthousePrefab;
        public GameObject hilltopAntennaPrefab;
        public GameObject isolatedBarnPrefab;
        public GameObject fuelStationPrefab;

        public void Sanitize()
        {
            heightmapResolution = ClosestTerrainResolution(heightmapResolution);
            alphamapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(alphamapResolution), 16, 1024);
            terrainSize = Mathf.Max(512f, terrainSize);
            terrainHeight = Mathf.Max(50f, terrainHeight);
            airportLength = Mathf.Min(Mathf.Max(300f, airportLength), terrainSize * 0.75f);
            airportWidth = Mathf.Min(Mathf.Max(200f, airportWidth), terrainSize * 0.5f);
            airportNormalizedPosition.x = Mathf.Clamp(airportNormalizedPosition.x, 0.15f, 0.85f);
            airportNormalizedPosition.y = Mathf.Clamp(airportNormalizedPosition.y, 0.15f, 0.85f);
            rockSlopeStart = Mathf.Max(dirtSlopeStart + 1f, rockSlopeStart);
            mainRoadWidth = Mathf.Max(3f, mainRoadWidth);
            secondaryRoadWidth = Mathf.Clamp(secondaryRoadWidth, 2f, mainRoadWidth);
            lakeMaximumRadius = Mathf.Max(lakeMinimumRadius, lakeMaximumRadius);
            minimumRiverPathLength = Mathf.Clamp(minimumRiverPathLength, 200f,
                terrainSize * 0.45f);
            mainTownSize = Mathf.Clamp(mainTownSize, 120f, terrainSize * 0.2f);
            maximumFieldSize = Mathf.Max(minimumFieldSize, maximumFieldSize);
            townAirportExclusionDistance = Mathf.Max(airportWidth * 0.5f,
                townAirportExclusionDistance);
        }

        private static int ClosestTerrainResolution(int value)
        {
            int[] valid = { 33, 65, 129, 257, 513, 1025, 2049 };
            int result = valid[0];
            foreach (int candidate in valid)
                if (Mathf.Abs(candidate - value) < Mathf.Abs(result - value)) result = candidate;
            return result;
        }
    }

    public interface IWorldGenerator
    {
        string Name { get; }
        void Generate(WorldGenerationContext context);
    }

    public static class GeneratedWorldUtility
    {
        public static int CountRoots(string rootName = WorldGenerationContext.RootName)
        {
            int count = 0;
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
                if (gameObject.name == rootName && gameObject.scene.IsValid() &&
                    gameObject.transform.parent == null) count++;
            return count;
        }

        public static void ClearRoots(string rootName = WorldGenerationContext.RootName,
            bool recordUndo = true)
        {
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.name != rootName || !gameObject.scene.IsValid() ||
                    gameObject.transform.parent != null) continue;
                if (recordUndo) Undo.DestroyObjectImmediate(gameObject);
                else UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }

    public sealed class WorldGenerationContext
    {
        public const string RootName = "World_Generated";
        public readonly WorldGenerationSettings settings;
        public readonly System.Random random;
        public GameObject root;
        public Terrain terrain;
        public TerrainData terrainData;
        public float[,] heights;
        public float[,] phase1Heights;
        public float[,] phase2Heights;
        public WorldBiome[,] phase2Biomes;
        public WorldBiome[,] biomes;
        public bool[,] exclusionMask;
        public Vector3 airportPosition;
        public Quaternion airportRotation;
        public readonly List<List<Vector3>> roadPaths = new();
        public readonly List<Vector3> riverPath = new();
        public readonly List<Vector3> lakeBoundary = new();
        public readonly List<Vector3> bridgeCrossings = new();
        public readonly List<Vector3> invalidRoadSegments = new();
        public Vector3 settlementTarget;
        public readonly List<SettlementData> settlements = new();
        public readonly List<List<Vector3>> settlementRoads = new();
        public readonly List<WorldFootprint> buildingFootprints = new();
        public readonly List<List<Vector3>> fieldBoundaries = new();
        public readonly List<LandmarkData> landmarks = new();
        public readonly List<Vector3> townCandidates = new();
        public readonly List<Vector3> rejectedTownCandidates = new();
        public readonly List<Vector3> slopeViolations = new();
        public int phase2RoadPathCount;

        public WorldGenerationContext(WorldGenerationSettings value)
        {
            settings = value;
            random = new System.Random(value.seed);
        }

        public Vector3 NormalizedToWorld(float x, float z)
        {
            Vector3 origin = terrain == null ? Vector3.zero : terrain.transform.position;
            float y = terrain == null ? 0f : terrain.SampleHeight(
                origin + new Vector3(x * settings.terrainSize, 0f, z * settings.terrainSize)) + origin.y;
            return origin + new Vector3(x * settings.terrainSize, y, z * settings.terrainSize);
        }

        public bool IsInsidePhase2Exclusion(Vector3 world, float margin = 0f)
        {
            foreach (List<Vector3> path in roadPaths)
                if (WorldGenerationMath.DistanceToPathXZ(world, path) <=
                    settings.mainRoadWidth * 0.5f + settings.roadShoulderWidth + margin) return true;
            if (WorldGenerationMath.DistanceToPathXZ(world, riverPath) <=
                settings.riverWidth * 0.5f + settings.riverBankWidth + margin) return true;
            if (lakeBoundary.Count > 2 && WorldGenerationMath.PointInPolygonXZ(world, lakeBoundary)) return true;
            foreach (WorldFootprint footprint in buildingFootprints)
                if (footprint.ContainsXZ(world, margin)) return true;
            foreach (List<Vector3> field in fieldBoundaries)
                if (WorldGenerationMath.PointInPolygonXZ(world, field)) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class SettlementData
    {
        public string name;
        public Vector3 center;
        public float radius;
        public bool isVillage;
        public readonly List<Vector3> boundary = new();
    }

    [Serializable]
    public struct WorldFootprint
    {
        public Vector3 center;
        public Vector2 size;
        public float heading;
        public SettlementZone zone;
        public float Radius => size.magnitude * 0.5f;
        public bool ContainsXZ(Vector3 point, float margin = 0f) =>
            Vector2.Distance(new Vector2(center.x, center.z),
                new Vector2(point.x, point.z)) <= Radius + margin;
        public bool Overlaps(WorldFootprint other, float spacing) =>
            Vector2.Distance(new Vector2(center.x, center.z),
                new Vector2(other.center.x, other.center.z)) < Radius + other.Radius + spacing;
    }

    [Serializable]
    public sealed class LandmarkData
    {
        public string navigationId;
        public string displayName;
        public LandmarkTypes type;
        public Vector3 position;
        public float visibilityRadius;
    }

    public static class WorldGenerationMath
    {
        public static float FractalNoise(float x, float y, int seed, float scale, int octaves)
        {
            System.Random seeded = new(seed);
            float offsetX = seeded.Next(-100000, 100000);
            float offsetY = seeded.Next(-100000, 100000);
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float weight = 0f;
            for (int octave = 0; octave < octaves; octave++)
            {
                total += Mathf.PerlinNoise(
                    (x + offsetX) / Mathf.Max(1f, scale) * frequency,
                    (y + offsetY) / Mathf.Max(1f, scale) * frequency) * amplitude;
                weight += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return weight <= 0f ? 0f : total / weight;
        }

        public static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }

        public static bool InAirportOrApproach(WorldGenerationSettings settings,
            float worldX, float worldZ, float extraMargin = 0f)
        {
            Vector2 center = settings.airportNormalizedPosition * settings.terrainSize;
            Vector2 local = Rotate(new Vector2(worldX, worldZ) - center, -settings.airportHeading);
            float halfLength = settings.airportLength * 0.5f + extraMargin;
            float halfWidth = settings.airportWidth * 0.5f + extraMargin;
            bool airport = Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.y) <= halfLength;
            bool approach = Mathf.Abs(local.x) <= settings.approachClearWidth * 0.5f + extraMargin &&
                Mathf.Abs(local.y) <= halfLength + settings.approachClearLength + extraMargin;
            return airport || approach;
        }

        public static float SmoothBand(float value, float start, float width) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start - width, start + width, value));

        public static float DistanceToPathXZ(Vector3 point, IReadOnlyList<Vector3> path)
        {
            if (path == null || path.Count < 2) return float.PositiveInfinity;
            float best = float.PositiveInfinity;
            Vector2 p = new(point.x, point.z);
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = new(path[i - 1].x, path[i - 1].z);
                Vector2 b = new(path[i].x, path[i].z);
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude < 0.001f ? 0f :
                    Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        public static bool PointInPolygonXZ(Vector3 point, IReadOnlyList<Vector3> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector3 a = polygon[i]; Vector3 b = polygon[j];
                if (((a.z > point.z) != (b.z > point.z)) &&
                    point.x < (b.x - a.x) * (point.z - a.z) /
                    Mathf.Max(0.00001f, b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
