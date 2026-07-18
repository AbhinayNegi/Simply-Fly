using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace SimplyFly.WorldGeneration.Editor.Tests
{
    public sealed class WorldGenerationTests
    {
        private static WorldGenerationSettings TestSettings(int seed)
        {
            WorldGenerationSettings settings = new()
            {
                seed = seed, terrainSize = 1200f, terrainHeight = 240f,
                heightmapResolution = 65, alphamapResolution = 32,
                airportLength = 420f, airportWidth = 260f,
                approachClearLength = 500f, airportHeading = 27f,
                townAirportExclusionDistance = 220f, mainTownSize = 180f,
                townRoadProximityRange = 260f, minimumFieldSize = 70f,
                maximumFieldSize = 140f
            };
            settings.Sanitize();
            return settings;
        }

        [Test]
        public void SameSeed_ReproducesIdenticalHeightmap()
        {
            float[,] first = TerrainGenerator.BuildHeights(TestSettings(4381));
            float[,] second = TerrainGenerator.BuildHeights(TestSettings(4381));
            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentHeightmaps()
        {
            float[,] first = TerrainGenerator.BuildHeights(TestSettings(4381));
            float[,] second = TerrainGenerator.BuildHeights(TestSettings(4382));
            bool difference = false;
            for (int z = 0; z < first.GetLength(0) && !difference; z++)
            for (int x = 0; x < first.GetLength(1); x++)
                if (!Mathf.Approximately(first[z, x], second[z, x])) { difference = true; break; }
            Assert.That(difference, Is.True);
        }

        [Test]
        public void AirportInterior_IsFlat_AndApproachesAreExcluded()
        {
            WorldGenerationSettings settings = TestSettings(782);
            float[,] heights = TerrainGenerator.BuildHeights(settings);
            bool[,] exclusions = new bool[heights.GetLength(0), heights.GetLength(1)];
            AirportZoneGenerator.FlattenAndMask(settings, heights, exclusions);
            int center = (heights.GetLength(0) - 1) / 2;
            float expected = heights[center, center];
            float cellSize = settings.terrainSize / (heights.GetLength(0) - 1);
            int halfWidthCells = Mathf.FloorToInt(settings.airportWidth * 0.35f / cellSize);
            for (int z = center - halfWidthCells; z <= center + halfWidthCells; z++)
            for (int x = center - halfWidthCells; x <= center + halfWidthCells; x++)
                Assert.That(heights[z, x], Is.EqualTo(expected).Within(0.000001f));
            Assert.That(exclusions[center, center], Is.True);
            Assert.That(WorldGenerationMath.InAirportOrApproach(settings,
                settings.terrainSize * 0.5f, settings.terrainSize * 0.5f +
                settings.airportLength * 0.5f + 200f), Is.True);
            Assert.That(VegetationGenerator.IsPlacementAllowed(settings,
                settings.terrainSize * 0.5f, settings.terrainSize * 0.5f), Is.False);
        }

        [Test]
        public void RegenerationRootCleanup_RemovesAllDuplicates()
        {
            const string testRoot = "World_Generated_TestOnly";
            new GameObject(testRoot);
            new GameObject(testRoot);
            Assert.That(GeneratedWorldUtility.CountRoots(testRoot), Is.EqualTo(2));
            GeneratedWorldUtility.ClearRoots(testRoot, false);
            Assert.That(GeneratedWorldUtility.CountRoots(testRoot), Is.Zero);
        }

        [Test]
        public void TextureWeights_AreNormalizedAndUseThreeLayers()
        {
            WorldGenerationSettings settings = TestSettings(95);
            TerrainData data = new()
            {
                heightmapResolution = settings.heightmapResolution,
                alphamapResolution = settings.alphamapResolution,
                size = new Vector3(settings.terrainSize, settings.terrainHeight, settings.terrainSize)
            };
            data.SetHeights(0, 0, TerrainGenerator.BuildHeights(settings));
            float[,,] weights = TextureGenerator.BuildWeights(data, settings);
            Assert.That(weights.GetLength(2), Is.EqualTo(3));
            for (int z = 0; z < weights.GetLength(0); z += 5)
            for (int x = 0; x < weights.GetLength(1); x += 5)
                Assert.That(weights[z, x, 0] + weights[z, x, 1] + weights[z, x, 2],
                    Is.EqualTo(1f).Within(0.0001f));
            Object.DestroyImmediate(data);
        }

        [Test]
        public void RoadPath_IsDeterministicSmoothAndTerrainConforming()
        {
            WorldGenerationContext context = CreateContext(7712);
            Vector3 start = context.NormalizedToWorld(0.25f, 0.3f);
            Vector3 end = context.NormalizedToWorld(0.8f, 0.74f);
            List<Vector3> first = RoadGenerator.BuildCurvedPath(context, start, end,
                new System.Random(context.settings.seed + context.settings.roadSeedOffset), 7);
            List<Vector3> second = RoadGenerator.BuildCurvedPath(context, start, end,
                new System.Random(context.settings.seed + context.settings.roadSeedOffset), 7);
            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(first[i], Is.EqualTo(second[i]));
                Assert.That(first[i].y, Is.EqualTo(context.terrain.SampleHeight(first[i]) + 0.15f)
                    .Within(0.02f));
                if (i < 2) continue;
                float turn = Vector3.Angle(first[i - 1] - first[i - 2],
                    first[i] - first[i - 1]);
                Assert.That(turn, Is.LessThan(45f));
            }
            DestroyContext(context);
        }

        [Test]
        public void RoadSeedOffset_ChangesCurvedPath()
        {
            WorldGenerationContext context = CreateContext(7712);
            Vector3 start = context.NormalizedToWorld(0.2f, 0.3f);
            Vector3 end = context.NormalizedToWorld(0.82f, 0.7f);
            List<Vector3> first = RoadGenerator.BuildCurvedPath(context, start, end,
                new System.Random(10), 7);
            List<Vector3> second = RoadGenerator.BuildCurvedPath(context, start, end,
                new System.Random(11), 7);
            Assert.That(Vector3.Distance(first[first.Count / 2], second[second.Count / 2]),
                Is.GreaterThan(0.1f));
            DestroyContext(context);
        }

        [Test]
        public void RiverEnforcement_IsStrictlyDownhill()
        {
            List<Vector3> path = new()
            {
                new Vector3(0f, 30f, 0f), new Vector3(10f, 32f, 0f),
                new Vector3(20f, 29f, 0f), new Vector3(30f, 29.5f, 0f)
            };
            RiverGenerator.EnforceDownhill(path);
            for (int i = 1; i < path.Count; i++)
                Assert.That(path[i].y, Is.LessThan(path[i - 1].y));
        }

        [Test]
        public void RoadRiverCrossing_IsDetectedForBridgePlacement()
        {
            bool crossed = Phase2GenerationUtility.SegmentIntersectionXZ(
                new Vector3(-10f, 0f, 0f), new Vector3(10f, 0f, 0f),
                new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, 10f),
                out Vector3 crossing);
            Assert.That(crossed, Is.True);
            Assert.That(crossing.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(crossing.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Phase2Exclusions_BlockAirportAndWaterPlacement()
        {
            WorldGenerationContext context = CreateContext(341);
            context.riverPath.Add(new Vector3(100f, 0f, 100f));
            context.riverPath.Add(new Vector3(500f, 0f, 100f));
            context.lakeBoundary.AddRange(new[]
            {
                new Vector3(700f, 0f, 700f), new Vector3(850f, 0f, 700f),
                new Vector3(850f, 0f, 850f), new Vector3(700f, 0f, 850f)
            });
            Assert.That(context.IsInsidePhase2Exclusion(new Vector3(250f, 0f, 100f)), Is.True);
            Assert.That(context.IsInsidePhase2Exclusion(new Vector3(775f, 0f, 775f)), Is.True);
            Assert.That(VegetationGenerator.IsPlacementAllowed(context.settings,
                context.settings.terrainSize * 0.5f, context.settings.terrainSize * 0.5f), Is.False);
            DestroyContext(context);
        }

        private static WorldGenerationContext CreateContext(int seed)
        {
            WorldGenerationSettings settings = TestSettings(seed);
            TerrainData data = new()
            {
                heightmapResolution = settings.heightmapResolution,
                alphamapResolution = settings.alphamapResolution,
                size = new Vector3(settings.terrainSize, settings.terrainHeight, settings.terrainSize)
            };
            data.SetHeights(0, 0, TerrainGenerator.BuildHeights(settings));
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            GameObject root = new("WorldGeneration_Test_Context");
            terrainObject.transform.SetParent(root.transform);
            WorldGenerationContext context = new(settings)
            {
                root = root, terrain = terrainObject.GetComponent<Terrain>(),
                terrainData = data, heights = data.GetHeights(0, 0,
                    settings.heightmapResolution, settings.heightmapResolution)
            };
            return context;
        }

        private static void DestroyContext(WorldGenerationContext context)
        {
            Object.DestroyImmediate(context.root);
            Object.DestroyImmediate(context.terrainData);
        }

        [Test]
        public void TownCandidate_IsDeterministicFlatNearRoadAndOutsideAirport()
        {
            WorldGenerationContext firstContext = CreateContext(8081);
            AddTestRoad(firstContext);
            Vector3 preferred = firstContext.NormalizedToWorld(0.78f, 0.72f);
            Vector3 first = TownLocationGenerator.FindCandidate(firstContext,
                new System.Random(101), preferred, 90f);
            WorldGenerationContext secondContext = CreateContext(8081);
            AddTestRoad(secondContext);
            Vector3 second = TownLocationGenerator.FindCandidate(secondContext,
                new System.Random(101), preferred, 90f);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(Phase3GenerationUtility.DistanceToRoad(firstContext, first),
                Is.LessThanOrEqualTo(firstContext.settings.townRoadProximityRange));
            Assert.That(WorldGenerationMath.InAirportOrApproach(firstContext.settings,
                first.x, first.z, firstContext.settings.townAirportExclusionDistance), Is.False);
            float slope = firstContext.terrainData.GetSteepness(
                first.x / firstContext.settings.terrainSize,
                first.z / firstContext.settings.terrainSize);
            Assert.That(slope, Is.LessThanOrEqualTo(firstContext.settings.maximumBuildingSlope));
            DestroyContext(firstContext); DestroyContext(secondContext);
        }

        [Test]
        public void BuildingFootprints_RejectOverlap()
        {
            WorldFootprint first = new() { center = new Vector3(100f, 0f, 100f),
                size = new Vector2(20f, 30f) };
            WorldFootprint overlapping = new() { center = new Vector3(115f, 0f, 100f),
                size = new Vector2(18f, 22f) };
            WorldFootprint separate = new() { center = new Vector3(180f, 0f, 100f),
                size = new Vector2(18f, 22f) };
            Assert.That(first.Overlaps(overlapping, 5f), Is.True);
            Assert.That(first.Overlaps(separate, 5f), Is.False);
        }

        [Test]
        public void FieldLayouts_AreIrregularDeterministicAndSeedSensitive()
        {
            WorldGenerationContext context = CreateContext(931);
            Vector3 center = context.NormalizedToWorld(0.72f, 0.72f);
            List<Vector3> first = FarmGenerator.BuildFieldBoundary(context, center, 120f,
                new System.Random(55));
            List<Vector3> repeat = FarmGenerator.BuildFieldBoundary(context, center, 120f,
                new System.Random(55));
            List<Vector3> different = FarmGenerator.BuildFieldBoundary(context, center, 120f,
                new System.Random(56));
            CollectionAssert.AreEqual(first, repeat);
            Assert.That(first.Count, Is.InRange(5, 8));
            Assert.That(Vector3.Distance(first[0], different[0]), Is.GreaterThan(0.1f));
            float firstRadius = Vector2.Distance(new Vector2(first[0].x, first[0].z),
                new Vector2(center.x, center.z));
            bool varied = false;
            for (int i = 1; i < first.Count; i++)
            {
                float radius = Vector2.Distance(new Vector2(first[i].x, first[i].z),
                    new Vector2(center.x, center.z));
                if (Mathf.Abs(radius - firstRadius) > 1f) { varied = true; break; }
            }
            Assert.That(varied, Is.True);
            DestroyContext(context);
        }

        private static void AddTestRoad(WorldGenerationContext context)
        {
            List<Vector3> road = new();
            for (int i = 0; i < 20; i++)
                road.Add(context.NormalizedToWorld(0.1f + i / 19f * 0.8f,
                    0.72f));
            context.roadPaths.Add(road);
        }
    }
}
