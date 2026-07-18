using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class WorldGeneratorWindow : EditorWindow
    {
        [SerializeField] private WorldGenerationSettings settings = new();
        [SerializeField] private bool showAdvanced;
        [SerializeField] private bool debugAirport = true;
        [SerializeField] private bool debugBiomes;
        [SerializeField] private bool debugForestRegions;
        [SerializeField] private bool debugHeightHeatmap;
        [SerializeField] private bool debugRoadPaths = true;
        [SerializeField] private bool debugRiverPath = true;
        [SerializeField] private bool debugLakeBoundary = true;
        [SerializeField] private bool debugRoadExclusions;
        [SerializeField] private bool debugBridgeCrossings = true;
        [SerializeField] private bool debugInvalidSlopes = true;
        [SerializeField] private bool debugTownCandidates;
        [SerializeField] private bool debugRejectedTowns;
        [SerializeField] private bool debugSettlementBoundaries = true;
        [SerializeField] private bool debugFieldBoundaries = true;
        [SerializeField] private bool debugLandmarkVisibility = true;
        [SerializeField] private bool debugBuildingOverlaps;
        [SerializeField] private bool debugPhase3SlopeViolations = true;
        private Vector2 scroll;
        private static WorldGenerationContext lastContext;

        [MenuItem("Tools/Simply Fly/World Generator")]
        public static void Open() => GetWindow<WorldGeneratorWindow>(
            "Simply Fly World Generator");

        private void OnEnable() => SceneView.duringSceneGui += DrawSceneDebug;
        private void OnDisable() => SceneView.duringSceneGui -= DrawSceneDebug;

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Procedural World Generation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates a deterministic built-in Unity Terrain and keeps every scene object under World_Generated.",
                MessageType.Info);
            if (!IsDedicatedWorldScene())
            {
                EditorGUILayout.HelpBox("Generation is locked to the dedicated WorldGenerationTest scene.", MessageType.Warning);
                if (GUILayout.Button("Create / Open World Test Scene")) OpenWorldTestScene();
            }
            settings.seed = EditorGUILayout.IntField("Seed", settings.seed);
            settings.terrainSize = EditorGUILayout.FloatField("Terrain Size", settings.terrainSize);
            settings.terrainHeight = EditorGUILayout.FloatField("Terrain Height", settings.terrainHeight);
            settings.noiseScale = EditorGUILayout.FloatField("Noise Scale", settings.noiseScale);
            settings.hillStrength = EditorGUILayout.Slider("Hill Strength", settings.hillStrength, 0f, 1f);
            settings.airportLength = EditorGUILayout.FloatField("Airport Length", settings.airportLength);
            settings.airportWidth = EditorGUILayout.FloatField("Airport Width", settings.airportWidth);
            settings.forestDensity = EditorGUILayout.Slider("Forest Density", settings.forestDensity, 0f, 1f);
            settings.rockDensity = EditorGUILayout.Slider("Rock Density", settings.rockDensity, 0f, 1f);

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Configuration", true);
            if (showAdvanced) DrawAdvancedSettings();
            DrawPhase2Settings();
            DrawPhase3Settings();
            DrawDebugSettings();
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate World", GUILayout.Height(32f))) GenerateWorld();
                if (GUILayout.Button("Regenerate", GUILayout.Height(32f))) GenerateWorld();
                GUI.backgroundColor = new Color(1f, 0.65f, 0.55f);
                if (GUILayout.Button("Clear World", GUILayout.Height(32f))) ClearWorld();
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Roads")) GenerateWorld();
                if (GUILayout.Button("Generate Water")) GenerateWorld();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate Roads and Water")) GenerateWorld();
                if (GUILayout.Button("Clear Roads and Water")) ClearPhase2();
            }
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Settlements")) GenerateWorld();
                if (GUILayout.Button("Generate Farmland")) GenerateWorld();
                if (GUILayout.Button("Generate Landmarks")) GenerateWorld();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate Phase 3")) GenerateWorld();
                if (GUILayout.Button("Clear Phase 3")) ClearPhase3();
            }
            if (lastContext != null)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Generated Airport", EditorStyles.boldLabel);
                EditorGUILayout.Vector3Field("Position", lastContext.airportPosition);
                EditorGUILayout.Vector3Field("Rotation", lastContext.airportRotation.eulerAngles);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAdvancedSettings()
        {
            settings.heightmapResolution = EditorGUILayout.IntPopup("Heightmap Resolution",
                settings.heightmapResolution,
                new[] { "129", "257", "513", "1025", "2049" },
                new[] { 129, 257, 513, 1025, 2049 });
            settings.alphamapResolution = EditorGUILayout.IntPopup("Texture Resolution",
                settings.alphamapResolution,
                new[] { "128", "256", "512", "1024" },
                new[] { 128, 256, 512, 1024 });
            settings.noiseOctaves = EditorGUILayout.IntSlider("Noise Octaves", settings.noiseOctaves, 2, 8);
            settings.valleyFlattening = EditorGUILayout.Slider("Valley Flattening", settings.valleyFlattening, 0.1f, 0.9f);
            settings.airportHeading = EditorGUILayout.Slider("Airport Heading", settings.airportHeading, 0f, 360f);
            settings.airportNormalizedPosition = EditorGUILayout.Vector2Field("Airport Position (0-1)", settings.airportNormalizedPosition);
            settings.airportBlendMargin = EditorGUILayout.FloatField("Airport Blend Margin", settings.airportBlendMargin);
            settings.approachClearLength = EditorGUILayout.FloatField("Approach Clear Length", settings.approachClearLength);
            settings.approachClearWidth = EditorGUILayout.FloatField("Approach Clear Width", settings.approachClearWidth);
            settings.dirtSlopeStart = EditorGUILayout.Slider("Dirt Slope Start", settings.dirtSlopeStart, 0f, 60f);
            settings.rockSlopeStart = EditorGUILayout.Slider("Rock Slope Start", settings.rockSlopeStart, 1f, 80f);
            settings.rockElevationStart = EditorGUILayout.Slider("Rock Elevation Start", settings.rockElevationStart, 0f, 1f);
            settings.textureBlend = EditorGUILayout.Slider("Texture Blend", settings.textureBlend, 0.001f, 0.25f);
            settings.grassLayer = ObjectField("Grass Terrain Layer", settings.grassLayer);
            settings.dirtLayer = ObjectField("Dirt Terrain Layer", settings.dirtLayer);
            settings.rockLayer = ObjectField("Rock Terrain Layer", settings.rockLayer);
            settings.biomeScale = EditorGUILayout.FloatField("Biome Region Scale", settings.biomeScale);
            settings.forestThreshold = EditorGUILayout.Slider("Forest Threshold", settings.forestThreshold, 0f, 1f);
            settings.farmlandThreshold = EditorGUILayout.Slider("Farmland Threshold", settings.farmlandThreshold, 0f, 1f);
            settings.rockyThreshold = EditorGUILayout.Slider("Rocky Threshold", settings.rockyThreshold, 0f, 1f);
            settings.placementSpacing = EditorGUILayout.FloatField("Placement Spacing", settings.placementSpacing);
            DrawPrefabArray("Tree Prefabs", ref settings.treePrefabs);
            DrawPrefabArray("Rock Prefabs", ref settings.rockPrefabs);
        }

        private void DrawDebugSettings()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Debug Visualization", EditorStyles.boldLabel);
            debugAirport = EditorGUILayout.Toggle("Airport Boundary", debugAirport);
            debugBiomes = EditorGUILayout.Toggle("Biome Colors", debugBiomes);
            debugForestRegions = EditorGUILayout.Toggle("Forest Regions", debugForestRegions);
            debugHeightHeatmap = EditorGUILayout.Toggle("Terrain Height Heatmap", debugHeightHeatmap);
            debugRoadPaths = EditorGUILayout.Toggle("Road Spline Paths", debugRoadPaths);
            debugRiverPath = EditorGUILayout.Toggle("River Spline Path", debugRiverPath);
            debugLakeBoundary = EditorGUILayout.Toggle("Lake Boundary", debugLakeBoundary);
            debugRoadExclusions = EditorGUILayout.Toggle("Road Exclusion Zones", debugRoadExclusions);
            debugBridgeCrossings = EditorGUILayout.Toggle("Bridge Crossings", debugBridgeCrossings);
            debugInvalidSlopes = EditorGUILayout.Toggle("Invalid Road Slopes", debugInvalidSlopes);
            debugTownCandidates = EditorGUILayout.Toggle("Town Candidates", debugTownCandidates);
            debugRejectedTowns = EditorGUILayout.Toggle("Rejected Town Areas", debugRejectedTowns);
            debugSettlementBoundaries = EditorGUILayout.Toggle("Settlement Boundaries", debugSettlementBoundaries);
            debugFieldBoundaries = EditorGUILayout.Toggle("Farm Field Boundaries", debugFieldBoundaries);
            debugLandmarkVisibility = EditorGUILayout.Toggle("Landmark Visibility", debugLandmarkVisibility);
            debugBuildingOverlaps = EditorGUILayout.Toggle("Building Overlap Checks", debugBuildingOverlaps);
            debugPhase3SlopeViolations = EditorGUILayout.Toggle("Building Slope Violations", debugPhase3SlopeViolations);
            if (GUI.changed) SceneView.RepaintAll();
        }

        private void DrawPhase2Settings()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Phase 2 - Roads", EditorStyles.boldLabel);
            settings.enableRoads = EditorGUILayout.Toggle("Enable Roads", settings.enableRoads);
            settings.mainRoadWidth = EditorGUILayout.FloatField("Main Road Width", settings.mainRoadWidth);
            settings.secondaryRoadWidth = EditorGUILayout.FloatField("Secondary Road Width", settings.secondaryRoadWidth);
            settings.roadShoulderWidth = EditorGUILayout.FloatField("Shoulder Width", settings.roadShoulderWidth);
            settings.branchCount = EditorGUILayout.IntSlider("Branch Count", settings.branchCount, 1, 3);
            settings.maximumRoadSlope = EditorGUILayout.Slider("Maximum Road Slope", settings.maximumRoadSlope, 1f, 30f);
            settings.roadCurveSmoothness = EditorGUILayout.IntSlider("Curve Smoothness", settings.roadCurveSmoothness, 2, 12);
            settings.roadMaterial = MaterialField("Road Material", settings.roadMaterial);
            settings.shoulderMaterial = MaterialField("Shoulder Material", settings.shoulderMaterial);
            settings.bridgePrefab = (GameObject)EditorGUILayout.ObjectField("Bridge Prefab", settings.bridgePrefab, typeof(GameObject), false);
            settings.roadSeedOffset = EditorGUILayout.IntField("Road Seed Offset", settings.roadSeedOffset);

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Phase 2 - River", EditorStyles.boldLabel);
            settings.enableRiver = EditorGUILayout.Toggle("Enable River", settings.enableRiver);
            settings.riverWidth = EditorGUILayout.FloatField("River Width", settings.riverWidth);
            settings.riverDepth = EditorGUILayout.FloatField("River Depth", settings.riverDepth);
            settings.riverBankWidth = EditorGUILayout.FloatField("Bank Width", settings.riverBankWidth);
            settings.riverCurveSmoothness = EditorGUILayout.IntSlider("Curve Smoothness", settings.riverCurveSmoothness, 2, 12);
            settings.minimumRiverPathLength = EditorGUILayout.FloatField("Minimum Path Length", settings.minimumRiverPathLength);
            settings.waterMaterial = MaterialField("Water Material", settings.waterMaterial);
            settings.riverSeedOffset = EditorGUILayout.IntField("River Seed Offset", settings.riverSeedOffset);

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Phase 2 - Lake", EditorStyles.boldLabel);
            settings.enableLake = EditorGUILayout.Toggle("Enable Lake", settings.enableLake);
            settings.lakeMinimumRadius = EditorGUILayout.FloatField("Minimum Radius", settings.lakeMinimumRadius);
            settings.lakeMaximumRadius = EditorGUILayout.FloatField("Maximum Radius", settings.lakeMaximumRadius);
            settings.lakeDepth = EditorGUILayout.FloatField("Lake Depth", settings.lakeDepth);
            settings.lakeShoreBlendDistance = EditorGUILayout.FloatField("Shore Blend Distance", settings.lakeShoreBlendDistance);
            settings.lakeSeedOffset = EditorGUILayout.IntField("Lake Seed Offset", settings.lakeSeedOffset);
        }

        private void DrawPhase3Settings()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Phase 3 - Towns", EditorStyles.boldLabel);
            settings.enableTowns = EditorGUILayout.Toggle("Enable Towns", settings.enableTowns);
            settings.mainTownSize = EditorGUILayout.FloatField("Main Town Size", settings.mainTownSize);
            settings.villageCount = EditorGUILayout.IntSlider("Village Count", settings.villageCount, 0, 3);
            settings.buildingDensity = EditorGUILayout.Slider("Building Density", settings.buildingDensity, 0.05f, 1f);
            settings.minimumBuildingSpacing = EditorGUILayout.FloatField("Minimum Spacing", settings.minimumBuildingSpacing);
            settings.maximumBuildingSlope = EditorGUILayout.Slider("Maximum Building Slope", settings.maximumBuildingSlope, 1f, 25f);
            settings.townRoadProximityRange = EditorGUILayout.FloatField("Road Proximity Range", settings.townRoadProximityRange);
            settings.townAirportExclusionDistance = EditorGUILayout.FloatField("Airport Exclusion", settings.townAirportExclusionDistance);
            settings.townSeedOffset = EditorGUILayout.IntField("Town Seed Offset", settings.townSeedOffset);
            DrawPrefabArray("Small Houses", ref settings.smallHousePrefabs);
            DrawPrefabArray("Medium Houses", ref settings.mediumHousePrefabs);
            DrawPrefabArray("Shops", ref settings.shopPrefabs);
            DrawPrefabArray("Warehouses", ref settings.warehousePrefabs);
            DrawPrefabArray("Barns", ref settings.barnPrefabs);
            DrawPrefabArray("Public Buildings", ref settings.publicBuildingPrefabs);
            DrawPrefabArray("Decorative Props", ref settings.decorativePropPrefabs);

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Phase 3 - Farms", EditorStyles.boldLabel);
            settings.enableFarms = EditorGUILayout.Toggle("Enable Farms", settings.enableFarms);
            settings.farmlandCoverage = EditorGUILayout.Slider("Farmland Coverage", settings.farmlandCoverage, 0f, 1f);
            settings.minimumFieldSize = EditorGUILayout.FloatField("Minimum Field Size", settings.minimumFieldSize);
            settings.maximumFieldSize = EditorGUILayout.FloatField("Maximum Field Size", settings.maximumFieldSize);
            settings.fieldIrregularity = EditorGUILayout.Slider("Field Irregularity", settings.fieldIrregularity, 0f, 1f);
            settings.farmBuildingDensity = EditorGUILayout.Slider("Farm Prop Density", settings.farmBuildingDensity, 0f, 1f);
            settings.fenceDensity = EditorGUILayout.Slider("Fence Density", settings.fenceDensity, 0f, 1f);
            settings.farmlandSeedOffset = EditorGUILayout.IntField("Farmland Seed Offset", settings.farmlandSeedOffset);
            DrawPrefabArray("Silos", ref settings.siloPrefabs);
            DrawPrefabArray("Hay Bales", ref settings.hayBalePrefabs);
            DrawPrefabArray("Tractors", ref settings.tractorPrefabs);

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Phase 3 - Landmarks", EditorStyles.boldLabel);
            settings.enableLandmarks = EditorGUILayout.Toggle("Enable Landmarks", settings.enableLandmarks);
            settings.landmarkCount = EditorGUILayout.IntSlider("Landmark Count", settings.landmarkCount, 0, 24);
            settings.minimumLandmarkSpacing = EditorGUILayout.FloatField("Minimum Spacing", settings.minimumLandmarkSpacing);
            settings.landmarkVisibilityPriority = EditorGUILayout.Slider("Visibility Priority", settings.landmarkVisibilityPriority, 0f, 1f);
            settings.enabledLandmarkTypes = (LandmarkTypes)EditorGUILayout.EnumFlagsField(
                "Enabled Types", settings.enabledLandmarkTypes);
            settings.landmarkSeedOffset = EditorGUILayout.IntField("Landmark Seed Offset", settings.landmarkSeedOffset);
            settings.waterTowerPrefab = PrefabField("Water Tower", settings.waterTowerPrefab);
            settings.radioMastPrefab = PrefabField("Radio Mast", settings.radioMastPrefab);
            settings.windTurbinePrefab = PrefabField("Wind Turbine", settings.windTurbinePrefab);
            settings.churchTowerPrefab = PrefabField("Church Tower", settings.churchTowerPrefab);
            settings.factoryChimneyPrefab = PrefabField("Factory Chimney", settings.factoryChimneyPrefab);
            settings.quarryPrefab = PrefabField("Quarry", settings.quarryPrefab);
            settings.lighthousePrefab = PrefabField("Lighthouse", settings.lighthousePrefab);
            settings.hilltopAntennaPrefab = PrefabField("Hilltop Antenna", settings.hilltopAntennaPrefab);
            settings.isolatedBarnPrefab = PrefabField("Isolated Barn", settings.isolatedBarnPrefab);
            settings.fuelStationPrefab = PrefabField("Fuel Station", settings.fuelStationPrefab);
        }

        private static GameObject PrefabField(string label, GameObject value) =>
            (GameObject)EditorGUILayout.ObjectField(label, value, typeof(GameObject), false);

        private static Material MaterialField(string label, Material value) =>
            (Material)EditorGUILayout.ObjectField(label, value, typeof(Material), false);

        private static TerrainLayer ObjectField(string label, TerrainLayer value) =>
            (TerrainLayer)EditorGUILayout.ObjectField(label, value, typeof(TerrainLayer), false);

        private static void DrawPrefabArray(string label, ref GameObject[] values)
        {
            values ??= new GameObject[0];
            int count = Mathf.Max(0, EditorGUILayout.IntField(label + " Count", values.Length));
            if (count != values.Length) System.Array.Resize(ref values, count);
            EditorGUI.indentLevel++;
            for (int i = 0; i < values.Length; i++)
                values[i] = (GameObject)EditorGUILayout.ObjectField($"Element {i}", values[i], typeof(GameObject), false);
            EditorGUI.indentLevel--;
        }

        private void GenerateWorld()
        {
            if (!IsDedicatedWorldScene())
            {
                EditorUtility.DisplayDialog("Simply Fly World Generator",
                    "Open the dedicated WorldGenerationTest scene before generating.", "OK");
                return;
            }
            settings.Sanitize();
            ClearWorld();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generate Simply Fly world");
            GameObject root = new(WorldGenerationContext.RootName);
            Undo.RegisterCreatedObjectUndo(root, "Generate Simply Fly world");
            WorldGenerationContext context = new(settings) { root = root };
            IWorldGenerator[] generators =
            {
                new TerrainGenerator(), new AirportZoneGenerator(),
                new BiomeGenerator(), new LakeGenerator(), new RiverGenerator(),
                new RoadGenerator(), new TownLocationGenerator(),
                new SettlementRoadGenerator(), new TownGenerator(),
                new FarmGenerator(), new LandmarkGenerator(),
                new TextureGenerator(), new VegetationGenerator(),
                new FinalCleanupGenerator()
            };
            try
            {
                for (int i = 0; i < generators.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Simply Fly World Generator",
                        generators[i].Name, (i + 1f) / generators.Length);
                    generators[i].Generate(context);
                    if (generators[i] is BiomeGenerator)
                        context.phase1Heights = (float[,])context.heights.Clone();
                    if (generators[i] is RoadGenerator)
                    {
                        context.phase2Heights = (float[,])context.heights.Clone();
                        context.phase2Biomes = (WorldBiome[,])context.biomes.Clone();
                        context.phase2RoadPathCount = context.roadPaths.Count;
                    }
                }
                lastContext = context;
                Selection.activeGameObject = root;
                Undo.CollapseUndoOperations(undoGroup);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                SceneView.RepaintAll();
                Debug.Log($"Simply Fly world generated. Seed {settings.seed}, " +
                    $"airport {context.airportPosition}, {generators.Length} generator stages.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                Undo.RevertAllDownToGroup(undoGroup);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void ClearWorld()
        {
            GeneratedWorldUtility.ClearRoots();
            lastContext = null;
            SceneView.RepaintAll();
        }

        private void ClearPhase2()
        {
            if (lastContext?.root == null) return;
            foreach (string childName in new[] { "Roads", "Water", "Bridges",
                         "Settlements", "Farmland", "Landmarks" })
            {
                Transform child = lastContext.root.transform.Find(childName);
                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
            }
            Transform placement = lastContext.root.transform.Find("Environment_Placement");
            if (placement != null) Undo.DestroyObjectImmediate(placement.gameObject);
            if (lastContext.phase1Heights != null)
            {
                lastContext.terrainData.SetHeights(0, 0, lastContext.phase1Heights);
                lastContext.heights = (float[,])lastContext.phase1Heights.Clone();
            }
            lastContext.roadPaths.Clear(); lastContext.riverPath.Clear();
            lastContext.lakeBoundary.Clear(); lastContext.bridgeCrossings.Clear();
            lastContext.invalidRoadSegments.Clear();
            new TextureGenerator().Generate(lastContext);
            new VegetationGenerator().Generate(lastContext);
            EditorUtility.SetDirty(lastContext.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private void ClearPhase3()
        {
            if (lastContext?.root == null) return;
            foreach (string childName in new[] { "Settlements", "Farmland", "Landmarks" })
            {
                Transform child = lastContext.root.transform.Find(childName);
                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
            }
            Transform settlementRoads = lastContext.root.transform.Find("Roads/Settlement_Roads");
            if (settlementRoads != null) Undo.DestroyObjectImmediate(settlementRoads.gameObject);
            Transform placement = lastContext.root.transform.Find("Environment_Placement");
            if (placement != null) Undo.DestroyObjectImmediate(placement.gameObject);
            if (lastContext.phase2Heights != null)
            {
                lastContext.terrainData.SetHeights(0, 0, lastContext.phase2Heights);
                lastContext.heights = (float[,])lastContext.phase2Heights.Clone();
            }
            if (lastContext.phase2Biomes != null)
                lastContext.biomes = (WorldBiome[,])lastContext.phase2Biomes.Clone();
            if (lastContext.roadPaths.Count > lastContext.phase2RoadPathCount)
                lastContext.roadPaths.RemoveRange(lastContext.phase2RoadPathCount,
                    lastContext.roadPaths.Count - lastContext.phase2RoadPathCount);
            lastContext.settlementRoads.Clear(); lastContext.settlements.Clear();
            lastContext.buildingFootprints.Clear(); lastContext.fieldBoundaries.Clear();
            lastContext.landmarks.Clear(); lastContext.townCandidates.Clear();
            lastContext.rejectedTownCandidates.Clear(); lastContext.slopeViolations.Clear();
            new TextureGenerator().Generate(lastContext);
            new VegetationGenerator().Generate(lastContext);
            EditorUtility.SetDirty(lastContext.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private void DrawSceneDebug(SceneView sceneView)
        {
            if (lastContext?.terrainData == null || lastContext.terrain == null) return;
            WorldGenerationSettings s = lastContext.settings;
            if (debugAirport) DrawAirport(s, lastContext.airportPosition);
            if (debugBiomes || debugForestRegions || debugHeightHeatmap) DrawSampleGrid();
            DrawPhase2Debug();
            DrawPhase3Debug();
        }

        private void DrawPhase3Debug()
        {
            if (debugTownCandidates)
            { Handles.color = Color.green; foreach (Vector3 p in lastContext.townCandidates)
                Handles.DrawWireDisc(p, Vector3.up, 12f); }
            if (debugRejectedTowns)
            { Handles.color = new Color(1f, 0.2f, 0.2f, 0.45f);
              foreach (Vector3 p in lastContext.rejectedTownCandidates)
                Handles.DrawWireDisc(p, Vector3.up, 10f); }
            if (debugSettlementBoundaries)
            {
                Handles.color = Color.yellow;
                foreach (SettlementData settlement in lastContext.settlements)
                { List<Vector3> loop = new(settlement.boundary) { settlement.boundary[0] };
                  Handles.DrawAAPolyLine(3f, loop.ToArray()); }
            }
            if (debugFieldBoundaries)
            {
                Handles.color = new Color(0.9f, 0.7f, 0.15f);
                foreach (List<Vector3> field in lastContext.fieldBoundaries)
                { List<Vector3> loop = new(field) { field[0] };
                  Handles.DrawAAPolyLine(2f, loop.ToArray()); }
            }
            if (debugLandmarkVisibility)
            {
                Handles.color = new Color(0.75f, 0.2f, 1f, 0.35f);
                foreach (LandmarkData landmark in lastContext.landmarks)
                    Handles.DrawWireDisc(landmark.position, Vector3.up, landmark.visibilityRadius);
            }
            if (debugBuildingOverlaps)
            {
                Handles.color = new Color(1f, 0.4f, 0f, 0.55f);
                foreach (WorldFootprint footprint in lastContext.buildingFootprints)
                    Handles.DrawWireDisc(footprint.center, Vector3.up, footprint.Radius);
            }
            if (debugPhase3SlopeViolations)
            { Handles.color = Color.red; foreach (Vector3 p in lastContext.slopeViolations)
                Handles.DrawWireCube(p, Vector3.one * 14f); }
        }

        private void DrawPhase2Debug()
        {
            if (debugRoadPaths)
            {
                Handles.color = Color.white;
                foreach (List<Vector3> path in lastContext.roadPaths)
                    Handles.DrawAAPolyLine(3f, path.ToArray());
            }
            if (debugRoadExclusions)
            {
                Handles.color = new Color(1f, 0.5f, 0f, 0.35f);
                foreach (List<Vector3> path in lastContext.roadPaths)
                    for (int i = 0; i < path.Count; i += 8)
                        Handles.DrawWireDisc(path[i], Vector3.up,
                            lastContext.settings.mainRoadWidth * 0.5f + lastContext.settings.roadShoulderWidth);
            }
            if (debugRiverPath && lastContext.riverPath.Count > 1)
            { Handles.color = Color.cyan; Handles.DrawAAPolyLine(4f, lastContext.riverPath.ToArray()); }
            if (debugLakeBoundary && lastContext.lakeBoundary.Count > 2)
            {
                Handles.color = new Color(0.1f, 0.65f, 1f);
                List<Vector3> loop = new(lastContext.lakeBoundary) { lastContext.lakeBoundary[0] };
                Handles.DrawAAPolyLine(3f, loop.ToArray());
            }
            if (debugBridgeCrossings)
            {
                Handles.color = Color.magenta;
                foreach (Vector3 point in lastContext.bridgeCrossings)
                    Handles.DrawWireDisc(point, Vector3.up, 18f);
            }
            if (debugInvalidSlopes)
            {
                Handles.color = Color.red;
                foreach (Vector3 point in lastContext.invalidRoadSegments)
                    Handles.DrawWireCube(point, Vector3.one * 18f);
            }
        }

        private static bool IsDedicatedWorldScene() =>
            SceneManager.GetActiveScene().name == "WorldGenerationTest";

        private static void OpenWorldTestScene()
        {
            const string path = "Assets/Scenes/WorldGenerationTest.unity";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (existing != null) EditorSceneManager.OpenScene(path);
            else
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
            }
        }

        private static void DrawAirport(WorldGenerationSettings settings, Vector3 center)
        {
            Vector3 right = Quaternion.Euler(0f, settings.airportHeading, 0f) * Vector3.right;
            Vector3 forward = Quaternion.Euler(0f, settings.airportHeading, 0f) * Vector3.forward;
            Vector3 r = right * settings.airportWidth * 0.5f;
            Vector3 f = forward * settings.airportLength * 0.5f;
            Handles.color = Color.yellow;
            Handles.DrawAAPolyLine(4f, center - r - f, center + r - f,
                center + r + f, center - r + f, center - r - f);
            Handles.color = new Color(1f, 0.55f, 0.1f, 0.8f);
            float approach = settings.approachClearLength;
            Handles.DrawAAPolyLine(2f, center - f, center - f - forward * approach);
            Handles.DrawAAPolyLine(2f, center + f, center + f + forward * approach);
        }

        private void DrawSampleGrid()
        {
            const int samples = 22;
            float radius = lastContext.settings.terrainSize / samples * 0.22f;
            for (int z = 0; z < samples; z++)
            for (int x = 0; x < samples; x++)
            {
                float nx = (x + 0.5f) / samples;
                float nz = (z + 0.5f) / samples;
                int bx = Mathf.Clamp(Mathf.RoundToInt(nx * (lastContext.biomes.GetLength(1) - 1)), 0, lastContext.biomes.GetLength(1) - 1);
                int bz = Mathf.Clamp(Mathf.RoundToInt(nz * (lastContext.biomes.GetLength(0) - 1)), 0, lastContext.biomes.GetLength(0) - 1);
                WorldBiome biome = lastContext.biomes[bz, bx];
                if (debugForestRegions && !debugBiomes && biome != WorldBiome.Forest) continue;
                Vector3 point = lastContext.NormalizedToWorld(nx, nz) + Vector3.up * 3f;
                if (debugHeightHeatmap)
                {
                    float height = lastContext.terrainData.GetInterpolatedHeight(nx, nz) /
                        lastContext.settings.terrainHeight;
                    Handles.color = Color.Lerp(Color.blue, Color.red, height);
                }
                else Handles.color = BiomeColor(biome);
                Handles.DrawSolidDisc(point, Vector3.up, radius);
            }
        }

        private static Color BiomeColor(WorldBiome biome) => biome switch
        {
            WorldBiome.Forest => new Color(0.05f, 0.35f, 0.08f, 0.55f),
            WorldBiome.Farmland => new Color(0.8f, 0.65f, 0.18f, 0.55f),
            WorldBiome.RockyHills => new Color(0.45f, 0.45f, 0.45f, 0.55f),
            _ => new Color(0.25f, 0.75f, 0.2f, 0.45f)
        };
    }
}
