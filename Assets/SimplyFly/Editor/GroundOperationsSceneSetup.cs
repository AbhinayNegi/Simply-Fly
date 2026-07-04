#if UNITY_EDITOR
using System.IO;
using System.Linq;
using SimplyFly.GroundOps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SimplyFly.Editor
{
    [InitializeOnLoad]
    public static class GroundOperationsSceneSetup
    {
        private const string SetupMarkerName = "Banked Flight Stability v1";
        private const string FlightSchoolPanelSettingsPath =
            "Assets/SimplyFly/UI/FlightSchoolPanelSettings.asset";
        private const string SideViewAircraftAssetPath =
            "Assets/SimplyFly/UI/AircraftSideView.png";
        private static readonly Vector3 AircraftSpawnPosition = new Vector3(-1020f, 1.73f, 0f);
        private const string EngineLoopAssetPath =
            "Assets/SimplyFly/Audio/Engine/single_prop_engine_loop.ogg";
        private const string WindSoftAssetPath =
            "Assets/SimplyFly/Audio/Wind/wind_soft_loop.ogg";
        private const string WindStrongAssetPath =
            "Assets/SimplyFly/Audio/Wind/wind_strong_loop.ogg";
        private const string WindDescentAssetPath =
            "Assets/SimplyFly/Audio/Wind/wind_rush_loop.ogg";
        private const string RunwayRumbleAssetPath =
            "Assets/SimplyFly/Audio/Ground/runway_rumble_loop.ogg";
        private const string StallWarningAssetPath =
            "Assets/SimplyFly/Audio/Warnings/stall_warning_beep.ogg";
        private const string StallBuffetAssetPath =
            "Assets/SimplyFly/Audio/Warnings/stall_buffet_loop.ogg";
        private const string FlapMotorAssetPath =
            "Assets/SimplyFly/Audio/Controls/flap_motor_loop.wav";
        private const string TouchdownSoftAssetPath =
            "Assets/SimplyFly/Audio/Landing/touchdown_soft.wav";
        private const string TouchdownFirmAssetPath =
            "Assets/SimplyFly/Audio/Landing/touchdown_firm.wav";
        private const string UIClickAssetPath =
            "Assets/SimplyFly/Audio/UI/ui_click.ogg";

        static GroundOperationsSceneSetup()
        {
            EditorApplication.delayCall += TryInitialSetup;
        }

        [MenuItem("Simply Fly/Build Aircraft Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject aircraftVisual = FindAircraft();
            if (aircraftVisual == null)
            {
                Debug.LogError("Simply Fly: Piper M350 aircraft was not found in the open scene.");
                return;
            }

            Undo.SetCurrentGroupName("Build Simply Fly Aircraft");
            CreateAirport();
            GameObject aircraftRoot = EnsureStandardAircraftRoot(aircraftVisual);
            ConfigureAircraft(aircraftRoot, aircraftVisual);
            ConfigureCamera(aircraftRoot);
            ConfigureAirportLocations(aircraftRoot);
            ConfigureHUD(aircraftRoot);
            ConfigureFlightSchool(aircraftRoot);
            ConfigureFlightTesting(aircraftRoot);
            ConfigureAirportSelector();
            ConfigureGameStructure(aircraftRoot);
            CreateMarker();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Simply Fly: standard +Z-forward aircraft root is ready.");
        }

        private static void TryInitialSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().name != "Main" ||
                GameObject.Find(SetupMarkerName) != null)
                return;

            BuildScene();
        }

        private static GameObject FindAircraft()
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                .FirstOrDefault(go => go.name.StartsWith("Piper M350"));
        }

        private static GameObject EnsureStandardAircraftRoot(GameObject visual)
        {
            GameObject root = GameObject.Find("AircraftRoot");
            if (root == null)
            {
                root = new GameObject("AircraftRoot");
                Undo.RegisterCreatedObjectUndo(root, "Create standard aircraft root");
            }

            root.transform.SetPositionAndRotation(
                AircraftSpawnPosition,
                Quaternion.Euler(0f, 90f, 0f));
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            visual.transform.localScale = Vector3.one;

            RemoveLegacySystemsFromVisual(visual);
            return root;
        }

        private static void RemoveLegacySystemsFromVisual(GameObject visual)
        {
            DestroyComponent<UIController>(visual);
            DestroyComponent<AircraftResetController>(visual);
            DestroyComponent<FlightPhysicsController>(visual);
            DestroyComponent<StallController>(visual);
            DestroyComponent<FlapController>(visual);
            DestroyComponent<AssistedTrimController>(visual);
            DestroyComponent<PilotAwarenessAssist>(visual);
            DestroyComponent<WheelVisualController>(visual);
            DestroyComponent<EngineAudioController>(visual);
            DestroyComponent<AircraftAudioController>(visual);
            DestroyComponent<RudderController>(visual);
            DestroyComponent<PropellerController>(visual);
            DestroyComponent<GroundMovementController>(visual);
            DestroyComponent<SteeringController>(visual);
            DestroyComponent<BrakeController>(visual);
            DestroyComponent<EngineController>(visual);
            DestroyComponent<ThrottleController>(visual);
            DestroyComponent<AircraftInput>(visual);
            DestroyComponent<AudioSource>(visual);
            DestroyComponent<BoxCollider>(visual);
            DestroyComponent<Rigidbody>(visual);

            MeshCollider meshCollider = visual.GetComponent<MeshCollider>();
            if (meshCollider != null) meshCollider.enabled = false;

            string[] legacyChildren =
            {
                "Left Main Wheel Collider",
                "Right Main Wheel Collider",
                "Nose Wheel Collider"
            };
            foreach (string childName in legacyChildren)
            {
                Transform child = visual.transform.Find(childName);
                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void DestroyComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null) Undo.DestroyObjectImmediate(component);
        }

        private static void RemoveEditorAwarenessAudioChildren(Transform aircraft)
        {
            string[] runtimeOnlyChildren =
            {
                "Awareness Wind",
                "Awareness Ground Rumble",
                "Awareness Stall Tone",
                "Awareness Touchdown"
            };

            foreach (string childName in runtimeOnlyChildren)
            {
                Transform child = aircraft.Find(childName);
                if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void CreateAirport()
        {
            GameObject airport =
                FindSceneObject("Flight Test Field") ??
                FindSceneObject("Airport") ??
                new GameObject("Flight Test Field");
            airport.name = "Flight Test Field";

            CreateSurface("Airport Ground", airport.transform, new Vector3(0f, -0.3f, 0f),
                new Vector3(10000f, 0.4f, 10000f), new Color(0.16f, 0.25f, 0.13f));
            CreateSurface("Runway 09-27", airport.transform, new Vector3(0f, -0.08f, 0f),
                new Vector3(2200f, 0.16f, 40f), new Color(0.105f, 0.11f, 0.12f));

            GameObject grid = FindOrCreateChild(airport.transform, "Ground Reference Grid");
            for (int offset = -5000; offset <= 5000; offset += 100)
            {
                bool major = offset % 500 == 0;
                Color gridColor = major
                    ? new Color(0.34f, 0.43f, 0.29f)
                    : new Color(0.22f, 0.31f, 0.19f);
                float width = major ? 1.25f : 0.38f;
                CreateSurface(
                    $"Grid X {offset}",
                    grid.transform,
                    new Vector3(offset, -0.088f, 0f),
                    new Vector3(width, 0.015f, 10000f),
                    gridColor,
                    false);
                CreateSurface(
                    $"Grid Z {offset}",
                    grid.transform,
                    new Vector3(0f, -0.087f, offset),
                    new Vector3(10000f, 0.015f, width),
                    gridColor,
                    false);
            }

            GameObject markings = FindOrCreateChild(airport.transform, "Runway Markings");
            DestroyChildIfExists(markings.transform, "Left Edge");
            DestroyChildIfExists(markings.transform, "Right Edge");
            for (int x = -1000; x <= 1000; x += 30)
            {
                CreateSurface($"Centerline {x}", markings.transform, new Vector3(x, 0.015f, 0f),
                    new Vector3(15f, 0.025f, 0.35f), new Color(0.92f, 0.92f, 0.88f), false);
            }

            CreateSurface("North Edge", markings.transform, new Vector3(0f, 0.012f, 19f),
                new Vector3(2160f, 0.02f, 0.3f), Color.white, false);
            CreateSurface("South Edge", markings.transform, new Vector3(0f, 0.012f, -19f),
                new Vector3(2160f, 0.02f, 0.3f), Color.white, false);

            GameObject thresholds = FindOrCreateChild(markings.transform, "Threshold Markings");
            for (int stripe = 0; stripe < 8; stripe++)
            {
                float z = -14f + stripe * 4f;
                CreateSurface(
                    $"West Threshold {stripe}",
                    thresholds.transform,
                    new Vector3(-1070f, 0.017f, z),
                    new Vector3(22f, 0.03f, 1.8f),
                    Color.white,
                    false);
                CreateSurface(
                    $"East Threshold {stripe}",
                    thresholds.transform,
                    new Vector3(1070f, 0.017f, z),
                    new Vector3(22f, 0.03f, 1.8f),
                    Color.white,
                    false);
            }

            GameObject approachTarget =
                FindOrCreateChild(airport.transform, "Runway 09 Approach Target");
            approachTarget.transform.localPosition = new Vector3(-1070f, 0f, 0f);
            approachTarget.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Color approachColor = new Color(0.1f, 0.9f, 1f);
            CreateSurface("Left Post", approachTarget.transform,
                new Vector3(-18f, 10f, 0f), new Vector3(1.2f, 20f, 1.2f),
                approachColor, false);
            CreateSurface("Right Post", approachTarget.transform,
                new Vector3(18f, 10f, 0f), new Vector3(1.2f, 20f, 1.2f),
                approachColor, false);
            CreateSurface("Top Bar", approachTarget.transform,
                new Vector3(0f, 20f, 0f), new Vector3(37.2f, 1.2f, 1.2f),
                approachColor, false);
            CreateSurface("Aim Point", approachTarget.transform,
                new Vector3(0f, 7f, 0f), new Vector3(4f, 4f, 1f),
                Color.white, false);

            GameObject landmarks = FindOrCreateChild(airport.transform, "Orientation Landmarks");
            CreateLandmark("North Red Tower", landmarks.transform,
                new Vector3(0f, 0f, 1500f), new Color(0.85f, 0.08f, 0.06f));
            CreateLandmark("East Blue Tower", landmarks.transform,
                new Vector3(1800f, 0f, 0f), new Color(0.05f, 0.22f, 0.9f));
            CreateLandmark("South Yellow Tower", landmarks.transform,
                new Vector3(0f, 0f, -1500f), new Color(0.95f, 0.72f, 0.04f));
            CreateLandmark("West Green Tower", landmarks.transform,
                new Vector3(-1800f, 0f, -1200f), new Color(0.05f, 0.7f, 0.18f));

            GameObject trainingBuildings =
                FindOrCreateChild(airport.transform, "Training Buildings");
            CreateSurface("Hangar A", trainingBuildings.transform,
                new Vector3(-350f, 12f, 120f), new Vector3(70f, 24f, 45f),
                new Color(0.34f, 0.38f, 0.4f), false);
            CreateSurface("Hangar B", trainingBuildings.transform,
                new Vector3(-250f, 10f, 120f), new Vector3(60f, 20f, 40f),
                new Color(0.3f, 0.34f, 0.36f), false);
            GameObject windsock =
                FindOrCreateChild(airport.transform, "Training Windsock");
            CreateSurface("Pole", windsock.transform,
                new Vector3(-900f, 8f, 70f), new Vector3(0.5f, 16f, 0.5f),
                Color.white, false);
            CreateSurface("Sock", windsock.transform,
                new Vector3(-895f, 15f, 70f), new Vector3(10f, 1.8f, 1.8f),
                new Color(1f, 0.28f, 0.06f), false);

            CreateTrainingFieldPolish(airport);

            GameObject boundaries = FindOrCreateChild(airport.transform, "Distant Boundaries");
            Color boundaryColor = new Color(0.20f, 0.24f, 0.19f);
            CreateSurface("North Boundary", boundaries.transform,
                new Vector3(0f, 35f, 4900f), new Vector3(10000f, 70f, 35f),
                boundaryColor, false);
            CreateSurface("South Boundary", boundaries.transform,
                new Vector3(0f, 35f, -4900f), new Vector3(10000f, 70f, 35f),
                boundaryColor, false);
            CreateSurface("East Boundary", boundaries.transform,
                new Vector3(4900f, 35f, 0f), new Vector3(35f, 70f, 10000f),
                boundaryColor, false);
            CreateSurface("West Boundary", boundaries.transform,
                new Vector3(-4900f, 35f, 0f), new Vector3(35f, 70f, 10000f),
                boundaryColor, false);

            CreateValleyAirstripEnvironment();
            CreateCoastalAirportEnvironment();
        }

        private static void CreateTrainingFieldPolish(GameObject airport)
        {
            DestroyChildIfExists(airport.transform, "Training Field Polish");
            GameObject polish = FindOrCreateChild(airport.transform, "Training Field Polish");
            Color white = new Color(0.94f, 0.94f, 0.9f);
            Color asphalt = new Color(0.12f, 0.13f, 0.14f);
            Color taxiYellow = new Color(0.95f, 0.72f, 0.08f);

            GameObject runwayDetails = FindOrCreateChild(polish.transform, "Runway Details");
            CreateRunwayNumber(runwayDetails.transform, "09", new Vector3(-930f, 0.025f, 0f), 90f, white);
            CreateRunwayNumber(runwayDetails.transform, "27", new Vector3(930f, 0.025f, 0f), 270f, white);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int x = -1020; x <= 1020; x += 120)
                {
                    CreateSurface($"Edge Light {side} {x}", runwayDetails.transform,
                        new Vector3(x, 0.32f, side * 20.8f),
                        new Vector3(0.45f, 0.65f, 0.45f),
                        new Color(0.78f, 0.92f, 1f), false);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 3; row++)
                {
                    CreateSurface($"West Touchdown {side} {row}", runwayDetails.transform,
                        new Vector3(-760f + row * 60f, 0.022f, side * 7f),
                        new Vector3(24f, 0.025f, 1.2f), white, false);
                    CreateSurface($"East Touchdown {side} {row}", runwayDetails.transform,
                        new Vector3(760f - row * 60f, 0.022f, side * 7f),
                        new Vector3(24f, 0.025f, 1.2f), white, false);
                }
            }

            GameObject movementArea = FindOrCreateChild(polish.transform, "Taxiway and Apron");
            CreateSurface("Taxiway Connector", movementArea.transform,
                new Vector3(-650f, -0.065f, 78f), new Vector3(18f, 0.13f, 116f), asphalt);
            CreateSurface("Training Apron", movementArea.transform,
                new Vector3(-650f, -0.06f, 158f), new Vector3(190f, 0.14f, 78f), asphalt);
            CreateSurface("Taxi Center Line", movementArea.transform,
                new Vector3(-650f, 0.012f, 78f), new Vector3(0.3f, 0.02f, 116f),
                taxiYellow, false);
            CreateSurface("Apron Lead Line", movementArea.transform,
                new Vector3(-650f, 0.014f, 151f), new Vector3(0.3f, 0.02f, 55f),
                taxiYellow, false);
            CreateSurface("Parking Tee", movementArea.transform,
                new Vector3(-650f, 0.015f, 175f), new Vector3(18f, 0.02f, 0.35f),
                taxiYellow, false);

            GameObject buildings = FindOrCreateChild(polish.transform, "Airport Objects");
            CreateSimpleHangar(buildings.transform, new Vector3(-735f, 0f, 245f));
            CreateSurface("Control Hut", buildings.transform,
                new Vector3(-560f, 5f, 225f), new Vector3(18f, 10f, 16f),
                new Color(0.62f, 0.58f, 0.48f), false);
            CreateSurface("Control Cab", buildings.transform,
                new Vector3(-560f, 12f, 225f), new Vector3(13f, 5f, 13f),
                new Color(0.25f, 0.43f, 0.48f), false);
            CreateSurface("Fuel Pad", buildings.transform,
                new Vector3(-515f, 0f, 172f), new Vector3(25f, 0.12f, 20f),
                new Color(0.32f, 0.33f, 0.34f), false);
            CreateSurface("Fuel Tank", buildings.transform,
                new Vector3(-515f, 3f, 172f), new Vector3(12f, 6f, 8f),
                new Color(0.78f, 0.78f, 0.72f), false);
            CreateSurface("Fuel Sign", buildings.transform,
                new Vector3(-515f, 7f, 172f), new Vector3(5f, 2f, 0.4f),
                new Color(0.85f, 0.12f, 0.08f), false);
            CreateStaticTrainer(buildings.transform, "Parked Trainer A",
                new Vector3(-610f, 1.2f, 172f), 90f);
            CreateStaticTrainer(buildings.transform, "Parked Trainer B",
                new Vector3(-690f, 1.2f, 172f), 90f);
            CreateFence(buildings.transform);

            GameObject references = FindOrCreateChild(polish.transform, "Airport Reference Markers");
            for (int x = -900; x <= 900; x += 300)
            {
                if (x == 0) continue;
                CreateDistanceBoard(references.transform, x);
            }
            for (int x = -1050; x <= 1050; x += 150)
            {
                CreateSurface($"Grass Motion Strip {x}", references.transform,
                    new Vector3(x, -0.075f, 48f), new Vector3(55f, 0.018f, 1.2f),
                    new Color(0.3f, 0.4f, 0.2f), false);
                CreateSurface($"Grass Motion Strip South {x}", references.transform,
                    new Vector3(x, -0.075f, -48f), new Vector3(55f, 0.018f, 1.2f),
                    new Color(0.3f, 0.4f, 0.2f), false);
            }

            GameObject approachAids = FindOrCreateChild(polish.transform, "Approach Aids");
            CreateApproachLights(approachAids.transform, -1);
            CreateApproachLights(approachAids.transform, 1);
            CreatePapi(approachAids.transform, new Vector3(-970f, 0.45f, -27f), "PAPI 09");
            CreatePapi(approachAids.transform, new Vector3(970f, 0.45f, 27f), "PAPI 27");

            GameObject scenery = FindOrCreateChild(polish.transform, "Local Scenery");
            CreateSurface("Airport Road", scenery.transform,
                new Vector3(0f, -0.075f, 360f), new Vector3(3000f, 0.025f, 14f),
                new Color(0.16f, 0.15f, 0.14f), false);
            for (int x = -1800; x <= 1800; x += 180)
            {
                if (x > -850 && x < -450) continue;
                CreateSimpleTree(scenery.transform, new Vector3(x, 0f, 470f), $"North Tree {x}");
                if (x % 360 == 0)
                    CreateSimpleTree(scenery.transform, new Vector3(x, 0f, -430f), $"South Tree {x}");
            }
            CreateSurface("North Distant Hill", scenery.transform,
                new Vector3(800f, 45f, 1800f), new Vector3(850f, 90f, 250f),
                new Color(0.18f, 0.23f, 0.15f), false);
            CreateSurface("South Distant Hill", scenery.transform,
                new Vector3(-1100f, 35f, -1900f), new Vector3(1000f, 70f, 260f),
                new Color(0.19f, 0.24f, 0.16f), false);

            GameObject centerlineDebug =
                FindOrCreateChild(polish.transform, "Runway Centerline Debug");
            CreateSurface("Centerline Guide", centerlineDebug.transform,
                new Vector3(0f, 0.08f, 0f), new Vector3(2200f, 0.04f, 0.12f),
                Color.cyan, false);
            centerlineDebug.SetActive(false);

            GameObject pathDebug = FindOrCreateChild(polish.transform, "Approach Path Debug");
            for (int distance = 250; distance <= 1500; distance += 250)
            {
                float height = Mathf.Tan(3f * Mathf.Deg2Rad) * distance;
                CreateSurface($"Glide Gate {distance}", pathDebug.transform,
                    new Vector3(-1070f - distance, height, 0f),
                    new Vector3(0.6f, 1f, 38f), new Color(0.1f, 0.85f, 0.95f), false);
            }
            pathDebug.SetActive(false);

            AirportReferenceToggle toggle = GetOrAdd<AirportReferenceToggle>(airport);
            SetObject(toggle, "runwayCenterlineDebug", centerlineDebug);
            SetObject(toggle, "approachPathDebug", pathDebug);
            SetObject(toggle, "airportReferenceMarkers", references);

            RenderSettings.fog = false;
            RenderSettings.ambientLight = new Color(0.55f, 0.59f, 0.63f);
            Light sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                .FirstOrDefault(light => light.type == LightType.Directional);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
                sun.intensity = 1.15f;
                sun.color = new Color(1f, 0.96f, 0.88f);
            }
        }

        private static void CreateValleyAirstripEnvironment()
        {
            GameObject root =
                FindSceneObject("Valley Airstrip Environment") ??
                new GameObject("Valley Airstrip Environment");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            CreateSurface("Valley Ground", root.transform,
                new Vector3(0f, -0.3f, 0f), new Vector3(6000f, 0.4f, 6000f),
                new Color(0.16f, 0.22f, 0.11f));
            CreatePrototypeRunway(
                root.transform,
                "Valley Runway",
                Vector3.zero,
                15f,
                1500f,
                32f,
                "Valley Threshold Target");

            GameObject hills = FindOrCreateChild(root.transform, "Valley Hills");
            Color nearHill = new Color(0.19f, 0.24f, 0.13f);
            Color farHill = new Color(0.15f, 0.18f, 0.12f);
            for (int z = -2200; z <= 2200; z += 550)
            {
                float stagger = (z / 550) % 2 == 0 ? 0f : 140f;
                CreateSurface($"West Hill {z}", hills.transform,
                    new Vector3(-720f - stagger, 120f, z),
                    new Vector3(650f, 240f, 420f), nearHill, false);
                CreateSurface($"East Hill {z}", hills.transform,
                    new Vector3(720f + stagger, 150f, z),
                    new Vector3(700f, 300f, 450f), farHill, false);
            }

            GameObject guides = FindOrCreateChild(root.transform, "Valley Visual Guides");
            CreateSurface("River", guides.transform,
                new Vector3(-430f, -0.07f, 0f), new Vector3(30f, 0.03f, 5600f),
                new Color(0.08f, 0.3f, 0.48f), false);
            CreateSurface("Valley Road", guides.transform,
                new Vector3(420f, -0.06f, 0f), new Vector3(12f, 0.035f, 5600f),
                new Color(0.25f, 0.23f, 0.2f), false);
            CreateSurface("Valley Hangar", guides.transform,
                new Vector3(140f, 9f, 180f), new Vector3(45f, 18f, 35f),
                new Color(0.38f, 0.36f, 0.32f), false);
            root.SetActive(false);
        }

        private static void CreateCoastalAirportEnvironment()
        {
            GameObject root =
                FindSceneObject("Coastal Airport Environment") ??
                new GameObject("Coastal Airport Environment");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            CreateSurface("Ocean", root.transform,
                new Vector3(0f, -0.42f, 0f), new Vector3(7000f, 0.35f, 7000f),
                new Color(0.035f, 0.22f, 0.36f));
            CreateSurface("Coastal Land", root.transform,
                new Vector3(0f, -0.22f, 1500f), new Vector3(7000f, 0.4f, 3600f),
                new Color(0.22f, 0.3f, 0.14f));
            CreateSurface("Beach", root.transform,
                new Vector3(0f, 0.005f, -290f), new Vector3(7000f, 0.035f, 90f),
                new Color(0.72f, 0.63f, 0.4f), false);
            CreatePrototypeRunway(
                root.transform,
                "Coastal Runway",
                new Vector3(500f, 0f, 700f),
                60f,
                1800f,
                38f,
                "Coastal Threshold Target");

            GameObject landmarks =
                FindOrCreateChild(root.transform, "Coastal Landmarks");
            CreateSurface("Lighthouse Tower", landmarks.transform,
                new Vector3(-900f, 25f, -180f), new Vector3(18f, 50f, 18f),
                Color.white, false);
            CreateSurface("Lighthouse Top", landmarks.transform,
                new Vector3(-900f, 55f, -180f), new Vector3(28f, 10f, 28f),
                new Color(0.85f, 0.08f, 0.05f), false);
            CreateSurface("Harbor Pier", landmarks.transform,
                new Vector3(-520f, 1f, -460f), new Vector3(320f, 2f, 25f),
                new Color(0.28f, 0.25f, 0.21f), false);
            for (int x = -500; x <= 1100; x += 200)
            {
                int index = (x + 500) / 200;
                CreateSurface($"Town Block {index}", landmarks.transform,
                    new Vector3(x, 12f, 1450f + (index % 2) * 180f),
                    new Vector3(90f, 24f + (index % 3) * 8f, 90f),
                    new Color(0.42f, 0.43f, 0.4f), false);
            }
            root.SetActive(false);
        }

        private static void CreatePrototypeRunway(
            Transform environment,
            string runwayName,
            Vector3 center,
            float heading,
            float length,
            float width,
            string thresholdName)
        {
            GameObject runway = FindOrCreateChild(environment, runwayName);
            runway.transform.localPosition = center;
            runway.transform.localRotation = Quaternion.Euler(0f, heading, 0f);
            CreateSurface("Surface", runway.transform,
                new Vector3(0f, -0.08f, 0f), new Vector3(width, 0.16f, length),
                new Color(0.105f, 0.11f, 0.12f));
            CreateSurface("Left Edge", runway.transform,
                new Vector3(-width * 0.5f + 1f, 0.012f, 0f),
                new Vector3(0.3f, 0.02f, length - 30f), Color.white, false);
            CreateSurface("Right Edge", runway.transform,
                new Vector3(width * 0.5f - 1f, 0.012f, 0f),
                new Vector3(0.3f, 0.02f, length - 30f), Color.white, false);
            GameObject markings = FindOrCreateChild(runway.transform, "Centerline");
            for (float z = -length * 0.5f + 60f;
                 z <= length * 0.5f - 60f;
                 z += 45f)
            {
                CreateSurface($"Dash {z:0}", markings.transform,
                    new Vector3(0f, 0.015f, z),
                    new Vector3(0.35f, 0.025f, 18f), Color.white, false);
            }

            GameObject threshold = FindOrCreateChild(runway.transform, thresholdName);
            threshold.transform.localPosition = new Vector3(0f, 0f, -length * 0.5f);
            threshold.transform.localRotation = Quaternion.identity;
            Color targetColor = new Color(0.1f, 0.9f, 1f);
            CreateSurface("Left Post", threshold.transform,
                new Vector3(-width * 0.5f, 9f, 0f),
                new Vector3(1f, 18f, 1f), targetColor, false);
            CreateSurface("Right Post", threshold.transform,
                new Vector3(width * 0.5f, 9f, 0f),
                new Vector3(1f, 18f, 1f), targetColor, false);
            CreateSurface("Top Bar", threshold.transform,
                new Vector3(0f, 18f, 0f),
                new Vector3(width + 1f, 1f, 1f), targetColor, false);
        }

        private static void ConfigureAircraft(GameObject aircraft, GameObject visual)
        {
            aircraft.transform.SetPositionAndRotation(
                AircraftSpawnPosition,
                Quaternion.Euler(0f, 90f, 0f));

            Rigidbody body = GetOrAdd<Rigidbody>(aircraft);
            body.mass = 1200f;
            body.linearDamping = 0.02f;
            body.angularDamping = 3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0f, -0.45f, -0.2f);

            MeshCollider oldCollider = visual.GetComponent<MeshCollider>();
            if (oldCollider != null) oldCollider.enabled = false;
            BoxCollider fuselage = GetOrAdd<BoxCollider>(aircraft);
            fuselage.center = new Vector3(0f, 0.05f, -0.25f);
            fuselage.size = new Vector3(1.65f, 1.75f, 6.6f);

            Transform leftVisual = FindChild(visual.transform, "Wheel1.002");
            Transform rightVisual = FindChild(visual.transform, "Wheel2.002");
            Transform noseVisual = FindChild(visual.transform, "Wheel3.002");
            Transform propeller = FindChild(visual.transform, "propeller");

            WheelCollider left = CreateWheelCollider(aircraft.transform, "Left Main Wheel Collider",
                ModelToRootPosition(leftVisual != null
                    ? leftVisual.localPosition
                    : new Vector3(-0.2f, -1.53f, -2.88f)), false);
            WheelCollider right = CreateWheelCollider(aircraft.transform, "Right Main Wheel Collider",
                ModelToRootPosition(rightVisual != null
                    ? rightVisual.localPosition
                    : new Vector3(-0.2f, -1.53f, 2.88f)), false);
            WheelCollider nose = CreateWheelCollider(aircraft.transform, "Nose Wheel Collider",
                ModelToRootPosition(noseVisual != null
                    ? noseVisual.localPosition
                    : new Vector3(2.58f, -1.48f, 0f)), true);
            WheelCollider[] wheels = { left, right, nose };

            AircraftInput input = GetOrAdd<AircraftInput>(aircraft);
            SetInt(input, "resetCameraKey",
                (int)UnityEngine.InputSystem.Key.C);
            ThrottleController throttle = GetOrAdd<ThrottleController>(aircraft);
            EngineController engine = GetOrAdd<EngineController>(aircraft);
            BrakeController brakes = GetOrAdd<BrakeController>(aircraft);
            SteeringController steering = GetOrAdd<SteeringController>(aircraft);
            GroundMovementController movement = GetOrAdd<GroundMovementController>(aircraft);
            FlightPhysicsController flightPhysics = GetOrAdd<FlightPhysicsController>(aircraft);
            RudderController rudder = GetOrAdd<RudderController>(aircraft);
            FlapController flaps = GetOrAdd<FlapController>(aircraft);
            AssistedTrimController trim = GetOrAdd<AssistedTrimController>(aircraft);
            StallController stall = GetOrAdd<StallController>(aircraft);
            ApproachGuidanceController approach =
                GetOrAdd<ApproachGuidanceController>(aircraft);
            LandingController landing = GetOrAdd<LandingController>(aircraft);
            RemoveEditorAwarenessAudioChildren(aircraft.transform);
            PilotAwarenessAssist awareness =
                GetOrAdd<PilotAwarenessAssist>(aircraft);
            AircraftResetController resetController = GetOrAdd<AircraftResetController>(aircraft);
            PropellerController propellerController = GetOrAdd<PropellerController>(aircraft);
            DestroyComponent<EngineAudioController>(aircraft);
            DestroyComponent<AudioSource>(aircraft);
            AircraftAudioController audio =
                GetOrAdd<AircraftAudioController>(aircraft);
            WheelVisualController visuals = GetOrAdd<WheelVisualController>(aircraft);
            UIController ui = GetOrAdd<UIController>(aircraft);

            SetObject(throttle, "input", input);
            SetObject(engine, "input", input);
            SetObject(engine, "throttle", throttle);
            SetObject(awareness, "flightPhysics", flightPhysics);
            SetObject(awareness, "approach", approach);
            SetObject(awareness, "stall", stall);
            SetObject(awareness, "trim", trim);
            SetObject(awareness, "landing", landing);
            SetObject(awareness, "engine", engine);
            SetObject(awareness, "input", input);
            SetBool(awareness, "audioCues", false);
            SetFloat(engine, "offRPM", 0f);
            SetObject(brakes, "input", input);
            SetFloat(brakes, "brakeStrength", 2800f);
            SetFloat(brakes, "brakeResponseSpeed", 1.5f);
            SetObject(steering, "input", input);
            SetObject(steering, "noseWheel", nose);
            SetObject(steering, "aircraftBody", body);
            SetObject(steering, "movement", movement);
            SetObject(steering, "flightPhysics", flightPhysics);
            SetVector3(steering, "localForwardAxis", Vector3.forward);
            SetCurve(steering, "steeringSensitivityCurve",
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.45f, 0.62f),
                    new Keyframe(1f, 0.18f)));
            SetFloat(steering, "highSpeedSteeringMultiplier", 0.18f);
            SetFloat(steering, "rudderNosewheelInfluence", 0.18f);
            SetFloat(steering, "wheelbase", 2.8f);
            SetFloat(steering, "yawResponse", 5f);
            SetFloat(steering, "maximumYawAcceleration", 1.6f);
            SetFloat(steering, "neutralYawRateDeadband", 0.015f);
            SetObject(movement, "engine", engine);
            SetObject(movement, "throttle", throttle);
            SetObject(movement, "brakes", brakes);
            SetArray(movement, "wheels", wheels);
            SetVector3(movement, "localForwardAxis", Vector3.forward);
            SetFloat(movement, "idleThrust", 650f);
            SetFloat(movement, "maxGroundThrust", 3000f);
            SetFloat(movement, "throttleResponseSpeed", 0.4f);
            SetFloat(movement, "maximumTaxiSpeed", 34f);
            SetCurve(movement, "speedLimitThrottleCurve",
                new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.25f, 0.13f),
                    new Keyframe(0.5f, 0.38f),
                    new Keyframe(0.75f, 0.7f),
                    new Keyframe(1f, 1f)));
            SetFloat(movement, "speedFadeStartRatio", 0.9f);
            SetFloat(movement, "rollingResistance", 70f);
            SetFloat(movement, "rollingResistanceBuildSpeed", 0.75f);
            SetFloat(movement, "idleCreepSpeed", 2f);
            SetFloat(movement, "breakawayThrust", 0.18f);
            SetFloat(movement, "staticFrictionSoftness", 2.5f);
            SetObject(flightPhysics, "input", input);
            SetObject(flightPhysics, "engine", engine);
            SetObject(flightPhysics, "throttle", throttle);
            SetObject(flightPhysics, "groundMovement", movement);
            SetObject(flightPhysics, "stall", stall);
            SetObject(flightPhysics, "flaps", flaps);
            SetObject(flightPhysics, "trim", trim);
            SetArray(flightPhysics, "groundCheckPoints",
                new Object[] { left.transform, right.transform, nose.transform });
            SetVector3(flightPhysics, "localForwardAxis", Vector3.forward);
            SetVector3(flightPhysics, "localPitchAxis", Vector3.left);
            SetFloat(flightPhysics, "takeoffSpeed", 30f);
            SetFloat(flightPhysics, "liftMultiplier", 1.04f);
            SetFloat(flightPhysics, "maxLiftForce", 16500f);
            SetFloat(flightPhysics, "pitchLiftInfluence", 0.38f);
            SetFloat(flightPhysics, "levelLiftFactor", 0.68f);
            SetFloat(flightPhysics, "referenceAngleOfAttack", 8f);
            SetVector3(flightPhysics, "liftApplicationOffset", Vector3.zero);
            SetFloat(flightPhysics, "rotationSpeed", 27f);
            SetFloat(flightPhysics, "elevatorAuthority", 2.2f);
            SetFloat(flightPhysics, "pitchResponse", 2.4f);
            SetFloat(flightPhysics, "elevatorReturnSpeed", 3f);
            SetFloat(flightPhysics, "pitchDamping", 3.2f);
            SetFloat(flightPhysics, "maxRotationPitchAngle", 9f);
            SetFloat(flightPhysics, "neutralAoA", 0.75f);
            SetFloat(flightPhysics, "cruiseNeutralAoA", 1.2f);
            SetFloat(flightPhysics, "climbNeutralAoA", 2f);
            SetFloat(flightPhysics, "aoADeadband", 0.5f);
            SetFloat(flightPhysics, "aerodynamicPitchStability", 0.085f);
            SetFloat(flightPhysics, "maxStabilityMoment", 0.45f);
            SetFloat(flightPhysics, "pitchStabilityBankReduction", 0.72f);
            SetFloat(flightPhysics, "pitchStabilitySlipReduction", 0.85f);
            SetFloat(flightPhysics, "pitchDampingInHighBank", 2.2f);
            SetFloat(flightPhysics, "lowForwardSpeedPitchDamping", 2.4f);
            SetFloat(flightPhysics, "highAoAPitchDamping", 2f);
            SetCurve(flightPhysics, "elevatorAuthorityByForwardSpeed",
                new AnimationCurve(
                    new Keyframe(0f, 0.05f),
                    new Keyframe(25f, 0.3f),
                    new Keyframe(50f, 0.8f),
                    new Keyframe(80f, 1f),
                    new Keyframe(120f, 0.9f)));
            SetFloat(flightPhysics, "elevatorAuthoritySlipReduction", 0.8f);
            SetFloat(flightPhysics, "virtualTailArm", 2.7f);
            SetFloat(flightPhysics, "tailStability", 0.42f);
            SetFloat(flightPhysics, "tailPitchRateInfluence", 0.32f);
            SetFloat(flightPhysics, "maxTailForce", 1700f);
            SetFloat(flightPhysics, "verticalSpeedStability", 0.38f);
            SetFloat(flightPhysics, "targetNeutralVerticalSpeed", 1.2f);
            SetFloat(flightPhysics, "idleTargetVerticalSpeed", -2.5f);
            SetFloat(flightPhysics, "levelFlightThrottle", 0.52f);
            SetFloat(flightPhysics, "maxVerticalSpeedAoACorrection", 3.5f);
            SetFloat(flightPhysics, "runwayPitchLimitStrength", 0.65f);
            SetFloat(flightPhysics, "maxAirbornePitchAngle", 45f);
            SetFloat(flightPhysics, "airborneSafetyStrength", 0.18f);
            SetFloat(flightPhysics, "liftoffConfirmationTime", 0.22f);
            SetFloat(flightPhysics, "groundedCheckDistance", 0.14f);
            SetFloat(flightPhysics, "noseSettledPitchAngle", 1.25f);
            SetFloat(flightPhysics, "groundNoseSettlingStrength", 0.2f);
            SetFloat(flightPhysics, "maxGroundNoseSettlingMoment", 1.4f);
            SetFloat(flightPhysics, "aerodynamicDrag", 2.2f);
            SetFloat(flightPhysics, "dragMultiplier", 1f);
            SetFloat(flightPhysics, "airborneThrustMultiplier", 1.53f);
            SetFloat(flightPhysics, "sideSlipDrag", 35f);
            SetCurve(flightPhysics, "sideSlipDragByAirspeed",
                new AnimationCurve(
                    new Keyframe(0f, 0.15f),
                    new Keyframe(35f, 0.55f),
                    new Keyframe(70f, 1f),
                    new Keyframe(120f, 1.15f)));
            SetFloat(flightPhysics, "maxSideSlipForce", 6500f);
            SetFloat(flightPhysics, "verticalStabilizerStrength", 0.018f);
            SetFloat(flightPhysics, "weatherVaneDamping", 1.1f);
            SetFloat(flightPhysics, "maxWeatherVaneTorque", 0.55f);
            SetFloat(flightPhysics, "aileronAuthority", 2.2f);
            SetFloat(flightPhysics, "rollResponse", 3f);
            SetFloat(flightPhysics, "aileronReturnSpeed", 4f);
            SetCurve(flightPhysics, "rollAuthorityCurve",
                new AnimationCurve(
                    new Keyframe(0f, 0.02f),
                    new Keyframe(0.6f, 0.22f),
                    new Keyframe(0.9f, 0.75f),
                    new Keyframe(1f, 1f),
                    new Keyframe(1.6f, 1.15f)));
            SetFloat(flightPhysics, "rollDamping", 2.4f);
            SetFloat(flightPhysics, "bankStability", 0f);
            SetFloat(flightPhysics, "maximumBankAngle", 70f);
            SetFloat(flightPhysics, "bankLimitStrength", 0.12f);
            SetFloat(flightPhysics, "lateralSlipDamping", 0f);
            SetFloat(flightPhysics, "coordinatedTurnStrength", 0f);
            SetFloat(flightPhysics, "bankTurnStrength", 0f);
            SetFloat(flightPhysics, "maxAssistedYawRate", 18f);
            SetObject(stall, "flightPhysics", flightPhysics);
            SetObject(stall, "input", input);
            SetObject(stall, "flaps", flaps);
            SetVector3(stall, "localPitchAxis", Vector3.left);
            SetFloat(stall, "stallSpeed", 42f);
            SetFloat(stall, "stallAoA", 14f);
            SetFloat(stall, "stallWarningSpeed", 49f);
            SetFloat(stall, "stallWarningAoA", 8f);
            SetFloat(stall, "stallRecoverySpeed", 52f);
            SetFloat(stall, "stallRecoveryAoA", 6f);
            SetFloat(stall, "stallExitSpeedMargin", 3f);
            SetFloat(stall, "stallExitAoAMargin", 1.5f);
            SetFloat(stall, "stallRecoverySmoothing", 1.2f);
            SetFloat(stall, "bankStallRiskMultiplier", 0.22f);
            SetFloat(stall, "stalledLiftMultiplier", 0.48f);
            SetFloat(stall, "stallLiftLossRate", 1.4f);
            SetFloat(stall, "stallRecoveryRate", 0.75f);
            SetFloat(stall, "lowSpeedControlReduction", 0.42f);
            SetFloat(stall, "stallNoseDropStrength", 0.18f);
            SetFloat(stall, "maxStallNoseDropMoment", 1.2f);
            SetFloat(stall, "stallPitchDamping", 1.8f);
            SetFloat(stall, "buffetStrength", 0.06f);
            SetObject(rudder, "input", input);
            SetObject(rudder, "flightPhysics", flightPhysics);
            SetObject(rudder, "groundMovement", movement);
            SetObject(rudder, "stall", stall);
            SetVector3(rudder, "localYawAxis", Vector3.up);
            SetFloat(rudder, "rudderSmoothing", 2.5f);
            SetFloat(rudder, "rudderReturnSpeed", 4f);
            SetFloat(rudder, "rudderAuthority", 0.5f);
            SetCurve(rudder, "rudderAuthorityByAirspeedCurve",
                new AnimationCurve(
                    new Keyframe(0f, 0.05f),
                    new Keyframe(20f, 0.25f),
                    new Keyframe(50f, 0.70f),
                    new Keyframe(80f, 1f),
                    new Keyframe(120f, 0.85f)));
            SetFloat(rudder, "groundRudderAuthority", 0.2f);
            SetFloat(rudder, "yawDamping", 1.5f);
            SetFloat(rudder, "maxYawRate", 9f);
            SetFloat(rudder, "yawLimitStrength", 1f);
            SetObject(flaps, "input", input);
            SetObject(flaps, "flightPhysics", flightPhysics);
            SetVector3(flaps, "localPitchAxis", Vector3.left);
            SetFloat(flaps, "flapDeploySpeed", 10f);
            SetFloat(flaps, "flapRetractSpeed", 14f);
            SetFloat(flaps, "firstFlapStepLiftBonus", 0.10f);
            SetFloat(flaps, "flapLiftBonusPerStep", 0.07f);
            SetFloat(flaps, "maxFlapLiftBonus", 0.24f);
            SetFloat(flaps, "firstFlapStepDrag", 0.25f);
            SetFloat(flaps, "flapDragPerStep", 0.70f);
            SetFloat(flaps, "maxFlapDrag", 1.65f);
            SetFloat(flaps, "flapStallSpeedReduction", 7f);
            SetFloat(flaps, "flapPitchMoment", -0.06f);
            SetFloat(flaps, "maxFlapExtensionSpeed", 100f);
            SetObject(trim, "input", input);
            SetObject(trim, "flightPhysics", flightPhysics);
            SetObject(trim, "stall", stall);
            SetObject(trim, "flaps", flaps);
            SetObject(trim, "throttle", throttle);
            SetFloat(trim, "trimCaptureSpeed", 0.22f);
            SetFloat(trim, "trimResponseSpeed", 0.16f);
            SetFloat(trim, "maximumNoseUpTrim", 0.38f);
            SetFloat(trim, "maximumNoseDownTrim", 0.32f);
            SetFloat(trim, "trimRecommendationDelay", 1f);
            SetFloat(trim, "trimDeadband", 0.12f);
            SetFloat(trim, "trimStrength", 1f);
            SetFloat(trim, "airspeedTrimInfluence", 0.18f);
            SetFloat(trim, "flapTrimInfluence", 0.2f);
            SetFloat(trim, "trimCaptureMinSpeed", 50f);
            SetFloat(trim, "trimCaptureMaxBank", 30f);
            SetFloat(trim, "trimCaptureMaxPitchRate", 8f);
            SetFloat(trim, "trimCaptureMaxVerticalSpeed", 8f);
            SetObject(propellerController, "engine", engine);
            SetObject(propellerController, "propeller", propeller);
            SetFloat(propellerController, "blurStartRPM", 600f);
            SetInt(propellerController, "blurCopyCount", 12);
            SetFloat(propellerController, "blurCopyOpacity", 0.055f);
            SetFloat(propellerController, "minimumBlurVisualSpeed", 180f);
            SetFloat(propellerController, "maximumBlurVisualSpeed", 520f);
            SetObject(audio, "engine", engine);
            SetObject(audio, "throttle", throttle);
            SetObject(audio, "flightPhysics", flightPhysics);
            SetObject(audio, "groundMovement", movement);
            SetObject(audio, "brakes", brakes);
            SetObject(audio, "stall", stall);
            SetObject(audio, "landing", landing);
            SetObject(audio, "flaps", flaps);
            SetObject(audio, "rudder", rudder);
            AudioClip engineLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(EngineLoopAssetPath);
            SetObject(audio, "engineIdleLoop", engineLoop);
            SetObject(audio, "enginePowerLoop", engineLoop);
            SetObject(audio, "propellerLoop", engineLoop);
            SetObject(audio, "windLoop",
                AssetDatabase.LoadAssetAtPath<AudioClip>(WindSoftAssetPath));
            SetObject(audio, "strongWindLoop",
                AssetDatabase.LoadAssetAtPath<AudioClip>(WindStrongAssetPath));
            SetObject(audio, "descentWindLoop",
                AssetDatabase.LoadAssetAtPath<AudioClip>(WindDescentAssetPath));
            SetObject(audio, "runwayRumbleLoop",
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    RunwayRumbleAssetPath));
            SetObject(audio, "stallWarningLoop",
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    StallWarningAssetPath));
            SetObject(audio, "stallBuffetLoop",
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    StallBuffetAssetPath));
            SetObject(audio, "flapMotorLoop",
                AssetDatabase.LoadAssetAtPath<AudioClip>(FlapMotorAssetPath));
            AudioClip softTouchdown =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    TouchdownSoftAssetPath);
            AudioClip firmTouchdown =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    TouchdownFirmAssetPath);
            SetObject(audio, "touchdownSoftClip", softTouchdown);
            SetObject(audio, "touchdownFirmClip", firmTouchdown);
            SetObject(audio, "touchdownHardClip", firmTouchdown);
            SetObject(ui, "engine", engine);
            SetObject(ui, "throttle", throttle);
            SetObject(ui, "movement", movement);
            SetObject(ui, "brakes", brakes);
            SetObject(ui, "steering", steering);
            SetObject(ui, "flightPhysics", flightPhysics);
            SetObject(ui, "approachGuidance", approach);
            SetObject(ui, "landing", landing);
            SetObject(ui, "stall", stall);
            SetObject(ui, "flaps", flaps);
            SetObject(ui, "trim", trim);
            SetObject(ui, "aircraftAudio", audio);
            SetObject(ui, "rudder", rudder);
            SetObject(ui, "resetController", resetController);
            SetObject(approach, "flightPhysics", flightPhysics);
            GameObject runwayApproachTarget =
                GameObject.Find("Runway 09 Approach Target");
            SetObject(
                approach,
                "runwayThreshold",
                runwayApproachTarget == null ? null : runwayApproachTarget.transform);
            SetVector3(approach, "localForwardAxis", Vector3.forward);
            SetObject(landing, "input", input);
            SetObject(landing, "flightPhysics", flightPhysics);
            SetObject(landing, "approachGuidance", approach);
            SetObject(landing, "leftMainWheel", left);
            SetObject(landing, "rightMainWheel", right);
            SetObject(landing, "noseWheel", nose);
            SetVector3(landing, "localPitchAxis", Vector3.left);
            SetFloat(landing, "flareAssistStrength", 0.15f);
            SetFloat(landing, "flareHeight", 8f);
            SetFloat(landing, "flareLiftMultiplier", 1.08f);
            SetFloat(landing, "maxFlarePitchAngle", 11f);
            SetFloat(landing, "smoothThreshold", 1.5f);
            SetFloat(landing, "firmThreshold", 3f);
            SetFloat(landing, "hardThreshold", 5f);
            SetFloat(landing, "bounceStrength", 0.28f);
            SetFloat(landing, "touchdownDamping", 3f);
            SetFloat(landing, "maxBounceVelocity", 2.2f);
            SetFloat(landing, "noseLoweringDamping", 0.11f);
            SetFloat(landing, "noseWheelTouchDelay", 0.35f);
            SetFloat(landing, "postTouchdownPitchDamping", 3.5f);
            SetFloat(landing, "noseLoweringPitchTarget", -1f);
            SetFloat(landing, "wheelContactTolerance", 0.12f);
            SetObject(resetController, "engine", engine);
            SetObject(resetController, "throttle", throttle);
            SetObject(resetController, "brakes", brakes);
            SetObject(resetController, "steering", steering);
            SetObject(resetController, "groundMovement", movement);
            SetObject(resetController, "flightPhysics", flightPhysics);
            SetObject(resetController, "landing", landing);
            SetObject(resetController, "stall", stall);
            SetObject(resetController, "flaps", flaps);
            SetObject(resetController, "trim", trim);
            SetObject(resetController, "rudder", rudder);
            SetObject(resetController, "propeller", propellerController);
            SetVector3(resetController, "airborneTestPosition",
                new Vector3(-400f, 300f, -900f));
            SetVector3(resetController, "airborneTestEuler",
                new Vector3(0f, 45f, 0f));
            SetFloat(resetController, "airborneTestSpeed", 42f);
            SetFloat(resetController, "airborneTestThrottle", 0.72f);
            SetVector3(resetController, "finalApproachPosition",
                new Vector3(-2600f, 95f, 0f));
            SetVector3(resetController, "finalApproachEuler",
                new Vector3(0f, 90f, 0f));
            SetFloat(resetController, "finalApproachSpeed", 33f);
            SetFloat(resetController, "finalApproachVerticalSpeed", -1.5f);
            SetFloat(resetController, "finalApproachThrottle", 0.45f);
            ConfigureWheelVisuals(visuals, wheels, new[] { leftVisual, rightVisual, noseVisual });
            SetObject(visuals, "movement", movement);
        }

        private static void ConfigureAirportLocations(GameObject aircraft)
        {
            GameObject system =
                FindSceneObject("Airport Location System") ??
                new GameObject("Airport Location System");
            AirportLocationManager manager = GetOrAdd<AirportLocationManager>(system);
            AircraftResetController reset =
                aircraft.GetComponent<AircraftResetController>();
            ApproachGuidanceController approach =
                aircraft.GetComponent<ApproachGuidanceController>();
            SetObject(manager, "resetController", reset);
            SetObject(manager, "approachGuidance", approach);
            SetInt(manager, "selectedAirportIndex", 0);

            GameObject trainingRoot = FindSceneObject("Flight Test Field");
            GameObject valleyRoot = FindSceneObject("Valley Airstrip Environment");
            GameObject coastalRoot = FindSceneObject("Coastal Airport Environment");
            Transform trainingThreshold =
                FindSceneObject("Runway 09 Approach Target")?.transform;
            Transform valleyThreshold =
                FindSceneObject("Valley Threshold Target")?.transform;
            Transform coastalThreshold =
                FindSceneObject("Coastal Threshold Target")?.transform;

            Vector3 valleyForward = Quaternion.Euler(0f, 15f, 0f) * Vector3.forward;
            Vector3 valleyThresholdPosition = -valleyForward * 750f;
            Vector3 coastalCenter = new Vector3(500f, 0f, 700f);
            Vector3 coastalForward = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward;
            Vector3 coastalThresholdPosition = coastalCenter - coastalForward * 900f;

            SerializedObject serialized = new SerializedObject(manager);
            SerializedProperty airports = serialized.FindProperty("airports");
            airports.arraySize = 3;
            ConfigureAirportDefinition(
                airports.GetArrayElementAtIndex(0),
                "Training Field",
                90f,
                2200f,
                40f,
                AircraftSpawnPosition,
                new Vector3(-650f, 1.73f, 175f),
                new Vector3(0f, 90f, 0f),
                new Vector3(-2600f, 0f, 0f),
                95f,
                new Vector3(-1070f, 0f, 0f),
                trainingThreshold,
                FindSceneObject("Runway 09-27")?.transform,
                300f,
                60f,
                trainingRoot);
            ConfigureAirportDefinition(
                airports.GetArrayElementAtIndex(1),
                "Valley Airstrip",
                15f,
                1500f,
                32f,
                valleyThresholdPosition + valleyForward * 55f + Vector3.up * 1.73f,
                valleyThresholdPosition + valleyForward * 55f + Vector3.up * 1.73f,
                new Vector3(0f, 15f, 0f),
                valleyThresholdPosition - valleyForward * 1400f,
                120f,
                valleyThresholdPosition,
                valleyThreshold,
                FindSceneObject("Valley Runway")?.transform,
                350f,
                60f,
                valleyRoot);
            ConfigureAirportDefinition(
                airports.GetArrayElementAtIndex(2),
                "Coastal Airport",
                60f,
                1800f,
                38f,
                coastalThresholdPosition + coastalForward * 60f + Vector3.up * 1.73f,
                coastalThresholdPosition + coastalForward * 60f + Vector3.up * 1.73f,
                new Vector3(0f, 60f, 0f),
                coastalThresholdPosition - coastalForward * 1500f,
                105f,
                coastalThresholdPosition,
                coastalThreshold,
                FindSceneObject("Coastal Runway")?.transform,
                300f,
                58f,
                coastalRoot);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (trainingRoot != null) trainingRoot.SetActive(true);
            if (valleyRoot != null) valleyRoot.SetActive(false);
            if (coastalRoot != null) coastalRoot.SetActive(false);
            SetObject(reset, "locationManager", manager);
            SetObject(aircraft.GetComponent<UIController>(), "locationManager", manager);
        }

        private static void ConfigureAirportDefinition(
            SerializedProperty property,
            string airportName,
            float heading,
            float length,
            float width,
            Vector3 runwaySpawn,
            Vector3 parkingSpawn,
            Vector3 parkingEuler,
            Vector3 finalSpawn,
            float finalAltitude,
            Vector3 thresholdPosition,
            Transform threshold,
            Transform centerline,
            float patternAltitude,
            float approachSpeed,
            GameObject environmentRoot)
        {
            property.FindPropertyRelative("airportName").stringValue = airportName;
            property.FindPropertyRelative("runwayHeading").floatValue = heading;
            property.FindPropertyRelative("runwayLength").floatValue = length;
            property.FindPropertyRelative("runwayWidth").floatValue = width;
            property.FindPropertyRelative("aircraftSpawnPosition").vector3Value = runwaySpawn;
            property.FindPropertyRelative("parkingSpawnPosition").vector3Value = parkingSpawn;
            property.FindPropertyRelative("parkingSpawnEuler").vector3Value = parkingEuler;
            property.FindPropertyRelative("finalApproachSpawnPosition").vector3Value = finalSpawn;
            property.FindPropertyRelative("finalApproachAltitude").floatValue = finalAltitude;
            property.FindPropertyRelative("finalApproachSpeed").floatValue = 32f;
            property.FindPropertyRelative("finalApproachVerticalSpeed").floatValue = -1.5f;
            property.FindPropertyRelative("finalApproachThrottle").floatValue = 0.45f;
            property.FindPropertyRelative("runwayThresholdPosition").vector3Value =
                thresholdPosition;
            property.FindPropertyRelative("runwayThreshold").objectReferenceValue = threshold;
            property.FindPropertyRelative("runwayCenterline").objectReferenceValue = centerline;
            property.FindPropertyRelative("patternAltitude").floatValue = patternAltitude;
            property.FindPropertyRelative("recommendedApproachSpeed").floatValue =
                approachSpeed;
            property.FindPropertyRelative("environmentRoot").objectReferenceValue =
                environmentRoot;
        }

        private static void ConfigureHUD(GameObject aircraft)
        {
            GameObject hud = GameObject.Find("Flight HUD");
            if (hud == null)
            {
                hud = new GameObject("Flight HUD", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(hud, "Create flight HUD");
            }

            Canvas canvas = GetOrAdd<Canvas>(hud);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = GetOrAdd<CanvasScaler>(hud);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup hudGroup = GetOrAdd<CanvasGroup>(hud);
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;

            FlightHUDInstrument airspeed = CreateHUDInstrument(
                hud.transform, "Airspeed", new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(28f, 0f),
                new Vector2(235f, 145f), "AIRSPEED");
            FlightHUDInstrument altitude = CreateHUDInstrument(
                hud.transform, "Altitude", new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-28f, 0f),
                new Vector2(235f, 145f), "ALTITUDE");
            FlightHUDInstrument verticalSpeed = CreateHUDInstrument(
                hud.transform, "Vertical Speed", new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-28f, 28f),
                new Vector2(235f, 132f), "VERTICAL SPEED");
            FlightHUDInstrument heading = CreateHUDInstrument(
                hud.transform, "Heading", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                new Vector2(330f, 125f), "HEADING");
            FlightHUDInstrument power = CreateHUDInstrument(
                hud.transform, "Power", new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(28f, 28f),
                new Vector2(235f, 132f), "POWER");
            FlightHUDInstrument attitude = CreateHUDInstrument(
                hud.transform, "Attitude", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 28f),
                new Vector2(285f, 110f), "ATTITUDE");

            GameObject slipPanel =
                FindOrCreateUIChild(hud.transform, "Slip Skid Indicator");
            RectTransform slipRect = slipPanel.GetComponent<RectTransform>();
            SetHUDRect(
                slipRect,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 315f),
                new Vector2(250f, 82f));
            Image slipBackground = GetOrAdd<Image>(slipPanel);
            slipBackground.color =
                new Color(0.015f, 0.05f, 0.065f, 0.78f);
            slipBackground.raycastTarget = false;
            TextMeshProUGUI slipTitle = CreateHUDText(
                slipPanel.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -7f), new Vector2(230f, 20f),
                13f, TextAlignmentOptions.Center);
            slipTitle.text = "SLIP / SKID";
            slipTitle.fontStyle = FontStyles.Bold;
            slipTitle.color = new Color(0.55f, 0.86f, 0.9f);
            TextMeshProUGUI slipIndicator = CreateHUDText(
                slipPanel.transform, "Ball",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 2f), new Vector2(230f, 30f),
                24f, TextAlignmentOptions.Center);
            slipIndicator.text = "[     o     ]";
            slipIndicator.fontStyle = FontStyles.Bold;
            TextMeshProUGUI slipDetail = CreateHUDText(
                slipPanel.transform, "Detail",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 5f), new Vector2(230f, 18f),
                11f, TextAlignmentOptions.Center);
            slipDetail.text = "COORDINATED";
            slipDetail.color = new Color(0.65f, 0.84f, 0.88f);
            SlipSkidHUDView slipView =
                GetOrAdd<SlipSkidHUDView>(slipPanel);
            SetObject(slipView, "indicatorLabel", slipIndicator);
            SetObject(slipView, "detailLabel", slipDetail);

            TextMeshProUGUI warning = CreateHUDText(
                hud.transform, "Flight Warning", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -165f),
                new Vector2(520f, 70f), 40f, TextAlignmentOptions.Center);
            warning.fontStyle = FontStyles.Bold;
            warning.raycastTarget = false;

            TextMeshProUGUI awarenessLabel = CreateHUDText(
                hud.transform, "Awareness Cue", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -215f),
                new Vector2(520f, 32f), 20f, TextAlignmentOptions.Center);
            awarenessLabel.fontStyle = FontStyles.Bold;
            awarenessLabel.raycastTarget = false;

            ConfigureSideViewTrainingAid(hud.transform, aircraft);
            ConfigureSpeedSensationHUD(hud.transform, aircraft);

            GameObject feedbackPanel = FindOrCreateUIChild(hud.transform, "Landing Feedback");
            RectTransform feedbackRect = feedbackPanel.GetComponent<RectTransform>();
            SetHUDRect(feedbackRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -245f), new Vector2(360f, 125f));
            Image feedbackBackground = GetOrAdd<Image>(feedbackPanel);
            feedbackBackground.color = new Color(0.015f, 0.04f, 0.055f, 0.88f);
            feedbackBackground.raycastTarget = false;
            CanvasGroup feedbackGroup = GetOrAdd<CanvasGroup>(feedbackPanel);
            feedbackGroup.alpha = 0f;
            feedbackGroup.interactable = false;
            feedbackGroup.blocksRaycasts = false;
            TextMeshProUGUI feedback = CreateHUDText(
                feedbackPanel.transform, "Message", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(340f, 110f), 24f, TextAlignmentOptions.Center);
            feedback.fontStyle = FontStyles.Bold;

            GameObject trimPanel = FindOrCreateUIChild(hud.transform, "Assisted Trim");
            RectTransform trimPanelRect = trimPanel.GetComponent<RectTransform>();
            SetHUDRect(trimPanelRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 155f), new Vector2(390f, 140f));
            Image trimBackground = GetOrAdd<Image>(trimPanel);
            trimBackground.color = new Color(0.015f, 0.05f, 0.065f, 0.78f);
            trimBackground.raycastTarget = false;
            TextMeshProUGUI trimStatus = CreateHUDText(
                trimPanel.transform, "Status", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -10f),
                new Vector2(370f, 25f), 17f, TextAlignmentOptions.Center);
            trimStatus.fontStyle = FontStyles.Bold;
            TextMeshProUGUI trimRecommendation = CreateHUDText(
                trimPanel.transform, "Recommendation", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -36f),
                new Vector2(370f, 28f), 16f, TextAlignmentOptions.Center);
            trimRecommendation.fontStyle = FontStyles.Bold;
            GameObject trimBar = FindOrCreateUIChild(trimPanel.transform, "Trim Bar");
            RectTransform trimBarRect = trimBar.GetComponent<RectTransform>();
            SetHUDRect(trimBarRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -76f), new Vector2(260f, 6f));
            Image trimBarImage = GetOrAdd<Image>(trimBar);
            trimBarImage.color = new Color(0.35f, 0.62f, 0.68f, 0.8f);
            trimBarImage.raycastTarget = false;
            GameObject trimMarker = FindOrCreateUIChild(trimBar.transform, "Marker");
            RectTransform trimMarkerRect = trimMarker.GetComponent<RectTransform>();
            SetHUDRect(trimMarkerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(7f, 24f));
            Image trimMarkerImage = GetOrAdd<Image>(trimMarker);
            trimMarkerImage.color = new Color(0.25f, 0.95f, 1f);
            trimMarkerImage.raycastTarget = false;
            TextMeshProUGUI trimScale = CreateHUDText(
                trimPanel.transform, "Scale", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -84f),
                new Vector2(300f, 22f), 12f, TextAlignmentOptions.Center);
            trimScale.text = "NOSE DOWN          NEUTRAL          NOSE UP";
            trimScale.color = new Color(0.65f, 0.84f, 0.88f);
            TextMeshProUGUI trimMessage = CreateHUDText(
                trimPanel.transform, "Message", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 7f),
                new Vector2(370f, 24f), 15f, TextAlignmentOptions.Center);
            trimMessage.fontStyle = FontStyles.Bold;
            FlightTrimHUDView trimView = GetOrAdd<FlightTrimHUDView>(trimPanel);
            SetObject(trimView, "statusLabel", trimStatus);
            SetObject(trimView, "recommendationLabel", trimRecommendation);
            SetObject(trimView, "messageLabel", trimMessage);
            SetObject(trimView, "trimMarker", trimMarkerRect);
            SetObject(trimView, "trimMarkerImage", trimMarkerImage);
            SetFloat(trimView, "trimBarWidth", 260f);

            FlightHUDController controller = GetOrAdd<FlightHUDController>(hud);
            SetObject(controller, "engine", aircraft.GetComponent<EngineController>());
            SetObject(controller, "throttle", aircraft.GetComponent<ThrottleController>());
            SetObject(controller, "flightPhysics", aircraft.GetComponent<FlightPhysicsController>());
            SetObject(controller, "approach", aircraft.GetComponent<ApproachGuidanceController>());
            SetObject(controller, "stall", aircraft.GetComponent<StallController>());
            SetObject(controller, "flaps", aircraft.GetComponent<FlapController>());
            SetObject(controller, "trim", aircraft.GetComponent<AssistedTrimController>());
            SetObject(controller, "locationManager",
                FindSceneObject("Airport Location System")?
                    .GetComponent<AirportLocationManager>());
            SetObject(controller, "landing", aircraft.GetComponent<LandingController>());
            SetObject(controller, "awarenessAssist",
                aircraft.GetComponent<PilotAwarenessAssist>());
            SetObject(controller, "debugUI", aircraft.GetComponent<UIController>());
            SetObject(controller, "hudGroup", hudGroup);
            SetObject(controller, "airspeedInstrument", airspeed);
            SetObject(controller, "altitudeInstrument", altitude);
            SetObject(controller, "verticalSpeedInstrument", verticalSpeed);
            SetObject(controller, "headingInstrument", heading);
            SetObject(controller, "powerInstrument", power);
            SetObject(controller, "attitudeInstrument", attitude);
            SetObject(controller, "warningLabel", warning);
            SetObject(controller, "awarenessLabel", awarenessLabel);
            SetObject(controller, "landingFeedbackLabel", feedback);
            SetObject(controller, "landingFeedbackGroup", feedbackGroup);
            SetObject(controller, "trimView", trimView);
            SetObject(controller, "rudder",
                aircraft.GetComponent<RudderController>());
            SetObject(controller, "slipSkidView", slipView);
            SetFloat(controller, "safeApproachSpeedMin", 65f);
            SetFloat(controller, "safeApproachSpeedMax", 75f);
            SetFloat(controller, "touchdownBankWarningAngle", 5f);
        }

        private static void ConfigureSpeedSensationHUD(
            Transform hud,
            GameObject aircraft)
        {
            GameObject root =
                FindOrCreateUIChild(hud, "Speed Sensation HUD");
            RectTransform rootRect = root.GetComponent<RectTransform>();
            SetHUDRect(
                rootRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1920f, 1080f));
            CanvasGroup cueGroup = GetOrAdd<CanvasGroup>(root);
            cueGroup.interactable = false;
            cueGroup.blocksRaycasts = false;

            TextMeshProUGUI energy = CreateHUDText(
                root.transform,
                "Energy Trend",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, -104f),
                new Vector2(300f, 28f),
                16f,
                TextAlignmentOptions.Left);
            energy.fontStyle = FontStyles.Bold;
            TextMeshProUGUI airflow = CreateHUDText(
                root.transform,
                "Airflow",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(28f, -132f),
                new Vector2(300f, 22f),
                12f,
                TextAlignmentOptions.Left);
            airflow.color = new Color(0.55f, 0.76f, 0.8f);
            TextMeshProUGUI pressure = CreateHUDText(
                root.transform,
                "Control Pressure",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 315f),
                new Vector2(520f, 28f),
                16f,
                TextAlignmentOptions.Center);
            pressure.fontStyle = FontStyles.Bold;
            pressure.color = new Color(1f, 0.78f, 0.25f);

            GameObject streakRoot =
                FindOrCreateUIChild(root.transform, "Peripheral Airflow");
            RectTransform streakRootRect =
                streakRoot.GetComponent<RectTransform>();
            SetHUDRect(
                streakRootRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1920f, 1080f));
            CanvasGroup streakGroup = GetOrAdd<CanvasGroup>(streakRoot);
            streakGroup.alpha = 0f;
            streakGroup.interactable = false;
            streakGroup.blocksRaycasts = false;

            RectTransform[] streaks = new RectTransform[14];
            for (int i = 0; i < streaks.Length; i++)
            {
                bool left = i < streaks.Length / 2;
                int row = i % (streaks.Length / 2);
                float y = -330f + row * 110f;
                float x = left ? -790f : 790f;
                streaks[i] = CreateUIBlock(
                    streakRoot.transform,
                    $"Airflow Streak {i + 1:00}",
                    new Vector2(x, y),
                    new Vector2(58f + row * 4f, 2f),
                    new Color(0.7f, 0.92f, 1f, 0f),
                    new Vector2(0.5f, 0.5f),
                    left ? -18f : 18f);
            }

            SpeedSensationHUD speedHUD =
                GetOrAdd<SpeedSensationHUD>(root);
            SetObject(speedHUD, "awareness",
                aircraft.GetComponent<PilotAwarenessAssist>());
            SetObject(speedHUD, "cueGroup", cueGroup);
            SetObject(speedHUD, "streakGroup", streakGroup);
            SetArray(speedHUD, "streaks",
                streaks.Cast<Object>().ToArray());
            SetObject(speedHUD, "energyLabel", energy);
            SetObject(speedHUD, "controlPressureLabel", pressure);
            SetObject(speedHUD, "airflowLabel", airflow);
        }

        private static void ConfigureFlightSchool(GameObject aircraft)
        {
            FlightLessonSystem lessonSystem =
                GetOrAdd<FlightLessonSystem>(aircraft);
            SetObject(lessonSystem, "input",
                aircraft.GetComponent<AircraftInput>());
            SetObject(lessonSystem, "engine",
                aircraft.GetComponent<EngineController>());
            SetObject(lessonSystem, "throttle",
                aircraft.GetComponent<ThrottleController>());
            SetObject(lessonSystem, "groundMovement",
                aircraft.GetComponent<GroundMovementController>());
            SetObject(lessonSystem, "flightPhysics",
                aircraft.GetComponent<FlightPhysicsController>());
            SetObject(lessonSystem, "approach",
                aircraft.GetComponent<ApproachGuidanceController>());
            SetObject(lessonSystem, "landing",
                aircraft.GetComponent<LandingController>());
            SetObject(lessonSystem, "stall",
                aircraft.GetComponent<StallController>());
            SetObject(lessonSystem, "flaps",
                aircraft.GetComponent<FlapController>());
            SetObject(lessonSystem, "trim",
                aircraft.GetComponent<AssistedTrimController>());
            SetObject(lessonSystem, "resetController",
                aircraft.GetComponent<AircraftResetController>());
            SetObject(lessonSystem, "locationManager",
                FindSceneObject("Airport Location System")?
                    .GetComponent<AirportLocationManager>());

            UnityEngine.UIElements.PanelSettings panelSettings =
                AssetDatabase.LoadAssetAtPath<
                    UnityEngine.UIElements.PanelSettings>(
                    FlightSchoolPanelSettingsPath);
            if (panelSettings == null)
            {
                string directory = Path.GetDirectoryName(
                    FlightSchoolPanelSettingsPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                panelSettings = ScriptableObject.CreateInstance<
                    UnityEngine.UIElements.PanelSettings>();
                panelSettings.name = "Flight School Panel Settings";
                AssetDatabase.CreateAsset(
                    panelSettings, FlightSchoolPanelSettingsPath);
            }
            panelSettings.scaleMode =
                UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode =
                UnityEngine.UIElements.PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            EditorUtility.SetDirty(panelSettings);

            GameObject uiObject =
                FindSceneObject("Flight School UI") ??
                new GameObject("Flight School UI");
            UnityEngine.UIElements.UIDocument document =
                GetOrAdd<UnityEngine.UIElements.UIDocument>(uiObject);
            document.panelSettings = panelSettings;
            document.sortingOrder = 140;
            FlightSchoolUI schoolUI = GetOrAdd<FlightSchoolUI>(uiObject);
            SetObject(schoolUI, "lessonSystem", lessonSystem);
            SetObject(schoolUI, "pitchCoach",
                FindSceneObject("Side View Training Aid")?
                    .GetComponent<SideViewTrainingAid>());
            SetInt(schoolUI, "toggleKey",
                (int)UnityEngine.InputSystem.Key.G);
        }

        private static void ConfigureGameStructure(GameObject aircraft)
        {
            GameObject structureObject =
                FindSceneObject("Simply Fly Game Structure") ??
                new GameObject("Simply Fly Game Structure");
            GameSettingsManager settings =
                GetOrAdd<GameSettingsManager>(structureObject);
            SetObject(settings, "aircraftAudio",
                aircraft.GetComponent<AircraftAudioController>());
            SetObject(settings, "awarenessAssist",
                aircraft.GetComponent<PilotAwarenessAssist>());
            SetObject(settings, "flightHUD",
                FindSceneObject("Flight HUD")?
                    .GetComponent<FlightHUDController>());
            SetObject(settings, "pitchCoach",
                FindSceneObject("Side View Training Aid")?
                    .GetComponent<SideViewTrainingAid>());
            SetObject(settings, "cameraController",
                Camera.main == null
                    ? null
                    : Camera.main.GetComponent<CameraController>());
            SetObject(settings, "debugUI",
                aircraft.GetComponent<UIController>());

            GameFlowController flow =
                GetOrAdd<GameFlowController>(structureObject);
            SetObject(flow, "airportManager",
                FindSceneObject("Airport Location System")?
                    .GetComponent<AirportLocationManager>());
            SetObject(flow, "resetController",
                aircraft.GetComponent<AircraftResetController>());
            SetObject(flow, "lessonSystem",
                aircraft.GetComponent<FlightLessonSystem>());
            SetObject(flow, "lessonUI",
                FindSceneObject("Flight School UI")?
                    .GetComponent<FlightSchoolUI>());
            SetObject(flow, "flightHUD",
                FindSceneObject("Flight HUD")?
                    .GetComponent<FlightHUDController>());
            SetObject(flow, "settings", settings);

            UnityEngine.UIElements.PanelSettings panelSettings =
                AssetDatabase.LoadAssetAtPath<
                    UnityEngine.UIElements.PanelSettings>(
                    FlightSchoolPanelSettingsPath);
            GameObject menuObject =
                FindSceneObject("Game Menu UI") ??
                new GameObject("Game Menu UI");
            UnityEngine.UIElements.UIDocument document =
                GetOrAdd<UnityEngine.UIElements.UIDocument>(menuObject);
            document.panelSettings = panelSettings;
            document.sortingOrder = 500;
            GameMenuUI menu = GetOrAdd<GameMenuUI>(menuObject);
            SetObject(menu, "flow", flow);
            SetObject(menu, "airports",
                FindSceneObject("Airport Location System")?
                    .GetComponent<AirportLocationManager>());
            SetObject(menu, "lessons",
                aircraft.GetComponent<FlightLessonSystem>());
            SetObject(menu, "settings", settings);
            SetObject(menu, "uiClickClip",
                AssetDatabase.LoadAssetAtPath<AudioClip>(UIClickAssetPath));
        }

        private static void ConfigureFlightTesting(GameObject aircraft)
        {
            FlightTelemetryRecorder recorder =
                GetOrAdd<FlightTelemetryRecorder>(aircraft);
            FlightTestRunner runner = GetOrAdd<FlightTestRunner>(aircraft);
            SetObject(recorder, "input",
                aircraft.GetComponent<AircraftInput>());
            SetObject(recorder, "engine",
                aircraft.GetComponent<EngineController>());
            SetObject(recorder, "throttle",
                aircraft.GetComponent<ThrottleController>());
            SetObject(recorder, "groundMovement",
                aircraft.GetComponent<GroundMovementController>());
            SetObject(recorder, "brakes",
                aircraft.GetComponent<BrakeController>());
            SetObject(recorder, "flightPhysics",
                aircraft.GetComponent<FlightPhysicsController>());
            SetObject(recorder, "trim",
                aircraft.GetComponent<AssistedTrimController>());
            SetObject(recorder, "flaps",
                aircraft.GetComponent<FlapController>());
            SetObject(recorder, "stall",
                aircraft.GetComponent<StallController>());
            SetObject(recorder, "approach",
                aircraft.GetComponent<ApproachGuidanceController>());
            SetObject(recorder, "landing",
                aircraft.GetComponent<LandingController>());
            SetObject(recorder, "rudder",
                aircraft.GetComponent<RudderController>());
            SetObject(recorder, "locationManager",
                FindSceneObject("Airport Location System")?
                    .GetComponent<AirportLocationManager>());

            SetObject(runner, "input",
                aircraft.GetComponent<AircraftInput>());
            SetObject(runner, "engine",
                aircraft.GetComponent<EngineController>());
            SetObject(runner, "throttle",
                aircraft.GetComponent<ThrottleController>());
            SetObject(runner, "flaps",
                aircraft.GetComponent<FlapController>());
            SetObject(runner, "flightPhysics",
                aircraft.GetComponent<FlightPhysicsController>());
            SetObject(runner, "approach",
                aircraft.GetComponent<ApproachGuidanceController>());
            SetObject(runner, "landing",
                aircraft.GetComponent<LandingController>());
            SetObject(runner, "stall",
                aircraft.GetComponent<StallController>());
            SetObject(runner, "resetController",
                aircraft.GetComponent<AircraftResetController>());
            SetObject(runner, "locationManager",
                FindSceneObject("Airport Location System")?
                    .GetComponent<AirportLocationManager>());
            SetObject(runner, "recorder", recorder);

            UnityEngine.UIElements.PanelSettings panelSettings =
                AssetDatabase.LoadAssetAtPath<
                    UnityEngine.UIElements.PanelSettings>(
                    FlightSchoolPanelSettingsPath);
            GameObject uiObject =
                FindSceneObject("Flight Test Debug UI") ??
                new GameObject("Flight Test Debug UI");
            UnityEngine.UIElements.UIDocument document =
                GetOrAdd<UnityEngine.UIElements.UIDocument>(uiObject);
            document.panelSettings = panelSettings;
            document.sortingOrder = 160;
            FlightTestDebugUI debugUI =
                GetOrAdd<FlightTestDebugUI>(uiObject);
            SetObject(debugUI, "runner", runner);
            SetObject(debugUI, "recorder", recorder);
        }

        private static void ConfigureSideViewTrainingAid(
            Transform hud,
            GameObject aircraft)
        {
            GameObject panelObject =
                FindOrCreateUIChild(hud, "Side View Training Aid");
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetHUDRect(
                panelRect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -165f),
                new Vector2(500f, 300f));
            Image panelImage = GetOrAdd<Image>(panelObject);
            panelImage.color = new Color(0.015f, 0.045f, 0.06f, 0.88f);
            panelImage.raycastTarget = false;
            CanvasGroup panelGroup = GetOrAdd<CanvasGroup>(panelObject);

            TextMeshProUGUI title = CreateHUDText(
                panelObject.transform,
                "Title",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -8f),
                new Vector2(470f, 24f),
                17f,
                TextAlignmentOptions.Center);
            title.text = "SIDE VIEW — TRAINING AID";
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.35f, 0.92f, 1f);

            GameObject diagram =
                FindOrCreateUIChild(panelObject.transform, "Diagram");
            RectTransform diagramRect = diagram.GetComponent<RectTransform>();
            SetHUDRect(
                diagramRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -38f),
                new Vector2(460f, 118f));

            CreateUIBlock(
                diagram.transform,
                "Horizon",
                Vector2.zero,
                new Vector2(430f, 2f),
                new Color(0.5f, 0.7f, 0.72f, 0.65f));

            RectTransform flightPathArrow = CreateUIBlock(
                diagram.transform,
                "Flight Path",
                new Vector2(5f, 0f),
                new Vector2(155f, 3f),
                new Color(0.25f, 1f, 0.45f, 0.95f),
                new Vector2(0f, 0.5f));
            CreateUIBlock(
                flightPathArrow,
                "Arrow Head Upper",
                new Vector2(151f, 4f),
                new Vector2(16f, 3f),
                new Color(0.25f, 1f, 0.45f, 0.95f),
                new Vector2(1f, 0.5f),
                28f);
            CreateUIBlock(
                flightPathArrow,
                "Arrow Head Lower",
                new Vector2(151f, -4f),
                new Vector2(16f, 3f),
                new Color(0.25f, 1f, 0.45f, 0.95f),
                new Vector2(1f, 0.5f),
                -28f);

            GameObject aircraftRoot =
                FindOrCreateUIChild(diagram.transform, "Aircraft Silhouette");
            RectTransform aircraftRect =
                aircraftRoot.GetComponent<RectTransform>();
            SetHUDRect(
                aircraftRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(176f, 78f));
            RemoveOldSideViewGeometry(aircraftRect);
            Image aircraftImage = GetOrAdd<Image>(aircraftRoot);
            aircraftImage.sprite = null;
            aircraftImage.color = Color.clear;
            aircraftImage.enabled = false;
            aircraftImage.raycastTarget = false;
            CreateAircraftSideOutline(aircraftRect);

            CreateUIBlock(
                diagram.transform, "Elevator Track",
                new Vector2(196f, 0f), new Vector2(3f, 70f),
                new Color(0.55f, 0.7f, 0.72f, 0.55f));
            RectTransform elevatorMarker = CreateUIBlock(
                diagram.transform, "Elevator Marker",
                new Vector2(196f, 0f), new Vector2(16f, 5f),
                new Color(1f, 0.72f, 0.18f));
            CreateUIBlock(
                diagram.transform, "Trim Track",
                new Vector2(218f, 0f), new Vector2(3f, 70f),
                new Color(0.55f, 0.7f, 0.72f, 0.55f));
            RectTransform trimMarker = CreateUIBlock(
                diagram.transform, "Trim Marker",
                new Vector2(218f, 0f), new Vector2(16f, 5f),
                new Color(0.85f, 0.35f, 1f));

            GameObject advanced =
                FindOrCreateUIChild(diagram.transform, "Force Vectors");
            RectTransform advancedRect = advanced.GetComponent<RectTransform>();
            SetHUDRect(
                advancedRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1f, 1f));
            CanvasGroup advancedGroup = GetOrAdd<CanvasGroup>(advanced);
            RectTransform liftArrow = CreateForceArrow(
                advanced.transform, "Lift", new Vector2(-10f, 10f),
                90f, new Color(0.2f, 1f, 0.45f));
            RectTransform weightArrow = CreateForceArrow(
                advanced.transform, "Weight", new Vector2(-10f, -10f),
                -90f, new Color(1f, 0.55f, 0.2f));
            RectTransform thrustArrow = CreateForceArrow(
                advanced.transform, "Thrust", new Vector2(35f, -18f),
                0f, new Color(0.25f, 0.8f, 1f));
            RectTransform dragArrow = CreateForceArrow(
                advanced.transform, "Drag", new Vector2(-35f, -18f),
                180f, new Color(1f, 0.3f, 0.3f));

            TextMeshProUGUI attitude = CreateHUDText(
                panelObject.transform, "Attitude Data",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -158f), new Vector2(470f, 24f),
                15f, TextAlignmentOptions.Center);
            TextMeshProUGUI motion = CreateHUDText(
                panelObject.transform, "Motion Data",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -184f), new Vector2(230f, 46f),
                14f, TextAlignmentOptions.Left);
            TextMeshProUGUI configuration = CreateHUDText(
                panelObject.transform, "Configuration Data",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-14f, -184f), new Vector2(230f, 46f),
                14f, TextAlignmentOptions.Right);
            TextMeshProUGUI guidance = CreateHUDText(
                panelObject.transform, "Guidance",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(470f, 27f),
                15f, TextAlignmentOptions.Center);
            guidance.fontStyle = FontStyles.Bold;
            guidance.color = new Color(1f, 0.78f, 0.25f);
            TextMeshProUGUI mode = CreateHUDText(
                panelObject.transform, "Mode",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(470f, 20f),
                12f, TextAlignmentOptions.Center);
            mode.color = new Color(0.55f, 0.75f, 0.78f);

            SideViewTrainingAid aid =
                GetOrAdd<SideViewTrainingAid>(panelObject);
            SetObject(aid, "flightPhysics",
                aircraft.GetComponent<FlightPhysicsController>());
            SetObject(aid, "input", aircraft.GetComponent<AircraftInput>());
            SetObject(aid, "trim",
                aircraft.GetComponent<AssistedTrimController>());
            SetObject(aid, "flaps", aircraft.GetComponent<FlapController>());
            SetObject(aid, "landing",
                aircraft.GetComponent<LandingController>());
            SetObject(aid, "stall", aircraft.GetComponent<StallController>());
            SetObject(aid, "throttle",
                aircraft.GetComponent<ThrottleController>());
            SetObject(aid, "panelGroup", panelGroup);
            SetObject(aid, "advancedGroup", advancedGroup);
            SetObject(aid, "aircraftSilhouette", aircraftRect);
            SetObject(aid, "flightPathArrow", flightPathArrow);
            SetObject(aid, "elevatorMarker", elevatorMarker);
            SetObject(aid, "trimMarker", trimMarker);
            SetObject(aid, "liftArrow", liftArrow);
            SetObject(aid, "weightArrow", weightArrow);
            SetObject(aid, "thrustArrow", thrustArrow);
            SetObject(aid, "dragArrow", dragArrow);
            SetObject(aid, "attitudeLabel", attitude);
            SetObject(aid, "motionLabel", motion);
            SetObject(aid, "configurationLabel", configuration);
            SetObject(aid, "guidanceLabel", guidance);
            SetObject(aid, "modeLabel", mode);
            SetInt(aid, "toggleKey",
                (int)UnityEngine.InputSystem.Key.I);
        }

        private static void CreateAircraftSideOutline(RectTransform root)
        {
            Color outline = new Color(0.28f, 0.94f, 1f, 1f);
            Color detail = new Color(0.55f, 0.82f, 0.87f, 0.95f);
            const float width = 3f;

            // Fuselage shell: tail at left, propeller/nose at right.
            CreateOutlineSegment(root, "Body Top",
                new Vector2(-60f, 6f), new Vector2(55f, 6f), width, outline);
            CreateOutlineSegment(root, "Upper Nose",
                new Vector2(55f, 6f), new Vector2(77f, 1f), width, outline);
            CreateOutlineSegment(root, "Lower Nose",
                new Vector2(77f, 1f), new Vector2(55f, -7f), width, outline);
            CreateOutlineSegment(root, "Body Bottom",
                new Vector2(55f, -7f), new Vector2(-58f, -7f), width, outline);
            CreateOutlineSegment(root, "Tail Join",
                new Vector2(-58f, -7f), new Vector2(-70f, 2f), width, outline);
            CreateOutlineSegment(root, "Tail Top Join",
                new Vector2(-70f, 2f), new Vector2(-60f, 6f), width, outline);

            // Vertical tail and horizontal stabilizer.
            CreateOutlineSegment(root, "Fin Front",
                new Vector2(-57f, 6f), new Vector2(-72f, 29f), width, outline);
            CreateOutlineSegment(root, "Fin Top",
                new Vector2(-72f, 29f), new Vector2(-82f, 28f), width, outline);
            CreateOutlineSegment(root, "Fin Rear",
                new Vector2(-82f, 28f), new Vector2(-70f, 2f), width, outline);
            CreateOutlineSegment(root, "Tailplane Upper",
                new Vector2(-61f, 4f), new Vector2(-91f, 0f), width, outline);
            CreateOutlineSegment(root, "Tailplane Lower",
                new Vector2(-91f, 0f), new Vector2(-68f, -3f), width, outline);

            // Slight isometric wing shape keeps the tiny icon recognizable.
            CreateOutlineSegment(root, "Wing Leading",
                new Vector2(24f, 3f), new Vector2(-9f, -21f), width, outline);
            CreateOutlineSegment(root, "Wing Tip",
                new Vector2(-9f, -21f), new Vector2(34f, -17f), width, outline);
            CreateOutlineSegment(root, "Wing Trailing",
                new Vector2(34f, -17f), new Vector2(45f, 1f), width, outline);

            // Windscreen/cabin detail and propeller.
            CreateOutlineSegment(root, "Windscreen",
                new Vector2(35f, 6f), new Vector2(27f, -1f), 2f, detail);
            CreateOutlineSegment(root, "Cabin Roof",
                new Vector2(27f, -1f), new Vector2(-7f, -1f), 2f, detail);
            CreateOutlineSegment(root, "Propeller",
                new Vector2(80f, -15f), new Vector2(80f, 17f), 2f, detail);
            CreateOutlineSegment(root, "Propeller Hub",
                new Vector2(76f, 1f), new Vector2(84f, 1f), 4f, outline);

            // Simple landing gear gives a clear upright reference near the ground.
            CreateOutlineSegment(root, "Main Gear",
                new Vector2(18f, -7f), new Vector2(13f, -18f), 2f, detail);
            CreateOutlineSegment(root, "Main Wheel",
                new Vector2(7f, -19f), new Vector2(19f, -19f), 4f, detail);
            CreateOutlineSegment(root, "Nose Gear",
                new Vector2(59f, -6f), new Vector2(61f, -15f), 2f, detail);
            CreateOutlineSegment(root, "Nose Wheel",
                new Vector2(57f, -16f), new Vector2(65f, -16f), 3f, detail);
        }

        private static RectTransform CreateOutlineSegment(
            Transform parent,
            string name,
            Vector2 from,
            Vector2 to,
            float width,
            Color color)
        {
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            return CreateUIBlock(
                parent,
                name,
                (from + to) * 0.5f,
                new Vector2(delta.magnitude, width),
                color,
                new Vector2(0.5f, 0.5f),
                angle);
        }

        private static void GenerateSideViewAircraftSprite(
            GameObject aircraftVisual)
        {
            if (aircraftVisual == null) return;

            const int width = 768;
            const int height = 384;
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject previewAircraft = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture renderTexture = null;
            Texture2D capturedTexture = null;

            try
            {
                previewAircraft =
                    Object.Instantiate(aircraftVisual);
                previewAircraft.name = "Side View Aircraft Preview";
                previewAircraft.transform.SetParent(null);
                previewAircraft.transform.SetPositionAndRotation(
                    Vector3.zero, Quaternion.identity);
                previewAircraft.transform.localScale = Vector3.one;
                SceneManager.MoveGameObjectToScene(
                    previewAircraft, previewScene);

                Renderer[] renderers =
                    previewAircraft.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) return;
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                cameraObject = new GameObject("Side View Capture Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
                Camera captureCamera = cameraObject.AddComponent<Camera>();
                captureCamera.orthographic = true;
                captureCamera.orthographicSize =
                    Mathf.Max(bounds.extents.y * 1.75f,
                        bounds.extents.x * 0.55f);
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                captureCamera.allowHDR = false;
                captureCamera.allowMSAA = true;
                captureCamera.nearClipPlane = 0.01f;
                captureCamera.farClipPlane =
                    Mathf.Max(100f, bounds.size.magnitude * 8f);

                Vector3 viewDirection =
                    new Vector3(-0.18f, 0.18f, -1f).normalized;
                float cameraDistance =
                    Mathf.Max(10f, bounds.size.magnitude * 2.5f);
                cameraObject.transform.position =
                    bounds.center + viewDirection * cameraDistance;
                cameraObject.transform.rotation = Quaternion.LookRotation(
                    bounds.center - cameraObject.transform.position,
                    Vector3.up);

                lightObject = new GameObject("Side View Capture Light");
                SceneManager.MoveGameObjectToScene(lightObject, previewScene);
                Light keyLight = lightObject.AddComponent<Light>();
                keyLight.type = LightType.Directional;
                keyLight.intensity = 1.25f;
                keyLight.color = new Color(1f, 0.96f, 0.9f);
                lightObject.transform.rotation =
                    Quaternion.Euler(35f, -35f, 0f);

                renderTexture = new RenderTexture(
                    width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.antiAliasing = 4;
                renderTexture.Create();
                captureCamera.targetTexture = renderTexture;
                captureCamera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                capturedTexture = new Texture2D(
                    width, height, TextureFormat.RGBA32, false);
                capturedTexture.ReadPixels(
                    new Rect(0f, 0f, width, height), 0, 0);
                capturedTexture.Apply();
                RenderTexture.active = previous;

                string absolutePath = Path.GetFullPath(
                    SideViewAircraftAssetPath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(absolutePath) ?? string.Empty);
                File.WriteAllBytes(
                    absolutePath, capturedTexture.EncodeToPNG());
                AssetDatabase.ImportAsset(
                    SideViewAircraftAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);

                TextureImporter importer =
                    AssetImporter.GetAtPath(SideViewAircraftAssetPath)
                        as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                if (capturedTexture != null)
                    Object.DestroyImmediate(capturedTexture);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void RemoveOldSideViewGeometry(
            RectTransform aircraftRoot)
        {
            string[] oldGeometry =
            {
                "Fuselage",
                "Nose",
                "Main Wing",
                "Tail Plane",
                "Tail"
            };
            foreach (string childName in oldGeometry)
            {
                Transform child = aircraftRoot.Find(childName);
                if (child != null)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static RectTransform CreateForceArrow(
            Transform parent,
            string name,
            Vector2 position,
            float rotation,
            Color color)
        {
            RectTransform arrow = CreateUIBlock(
                parent, name, position, new Vector2(54f, 3f),
                color, new Vector2(0f, 0.5f), rotation);
            CreateUIBlock(
                arrow, "Head Upper", new Vector2(52f, 4f),
                new Vector2(14f, 3f), color,
                new Vector2(1f, 0.5f), 28f);
            CreateUIBlock(
                arrow, "Head Lower", new Vector2(52f, -4f),
                new Vector2(14f, 3f), color,
                new Vector2(1f, 0.5f), -28f);
            return arrow;
        }

        private static RectTransform CreateUIBlock(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            Vector2? pivot = null,
            float rotation = 0f)
        {
            GameObject block = FindOrCreateUIChild(parent, name);
            RectTransform rect = block.GetComponent<RectTransform>();
            Vector2 actualPivot = pivot ?? new Vector2(0.5f, 0.5f);
            SetHUDRect(
                rect,
                new Vector2(0.5f, 0.5f),
                actualPivot,
                position,
                size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = GetOrAdd<Image>(block);
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static FlightHUDInstrument CreateHUDInstrument(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            string title)
        {
            GameObject panelObject = FindOrCreateUIChild(parent, name);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetHUDRect(panelRect, anchor, pivot, position, size);
            Image panel = GetOrAdd<Image>(panelObject);
            panel.color = new Color(0.015f, 0.05f, 0.065f, 0.78f);
            panel.raycastTarget = false;

            TextMeshProUGUI titleLabel = CreateHUDText(
                panelObject.transform, "Title", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -13f),
                new Vector2(size.x - 18f, 25f), 16f, TextAlignmentOptions.Center);
            titleLabel.text = title;
            titleLabel.color = new Color(0.65f, 0.84f, 0.88f);
            titleLabel.fontStyle = FontStyles.Bold;

            TextMeshProUGUI valueLabel = CreateHUDText(
                panelObject.transform, "Value", new Vector2(0.5f, 0.55f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                new Vector2(size.x - 16f, 52f), 35f, TextAlignmentOptions.Center);
            valueLabel.fontStyle = FontStyles.Bold;

            TextMeshProUGUI detailLabel = CreateHUDText(
                panelObject.transform, "Detail", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                new Vector2(size.x - 16f, 42f), 13f, TextAlignmentOptions.Center);
            detailLabel.color = new Color(0.72f, 0.86f, 0.88f);

            FlightHUDInstrument instrument = GetOrAdd<FlightHUDInstrument>(panelObject);
            SetObject(instrument, "titleLabel", titleLabel);
            SetObject(instrument, "valueLabel", valueLabel);
            SetObject(instrument, "detailLabel", detailLabel);
            SetObject(instrument, "panel", panel);
            return instrument;
        }

        private static void ConfigureAirportSelector()
        {
            GameObject canvasObject = FindSceneObject("Location Selector Canvas");
            if (canvasObject == null)
            {
                canvasObject =
                    new GameObject("Location Selector Canvas", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(
                    canvasObject, "Create location selector canvas");
            }
            Canvas canvas = GetOrAdd<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvasObject);

            GameObject panelObject =
                FindOrCreateUIChild(canvasObject.transform, "Location Selector");
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            SetHUDRect(panelRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -20f), new Vector2(410f, 225f));
            Image panelImage = GetOrAdd<Image>(panelObject);
            panelImage.color = new Color(0.015f, 0.05f, 0.065f, 0.88f);
            CanvasGroup selectorGroup = GetOrAdd<CanvasGroup>(panelObject);

            TextMeshProUGUI title = CreateHUDText(
                panelObject.transform, "Title", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -10f),
                new Vector2(390f, 26f), 18f, TextAlignmentOptions.Center);
            title.text = "AIRPORT LOCATION  [O]";
            title.fontStyle = FontStyles.Bold;
            TextMeshProUGUI info = CreateHUDText(
                panelObject.transform, "Airport Info", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -38f),
                new Vector2(390f, 62f), 14f, TextAlignmentOptions.Center);

            Button training = CreateUIButton(
                panelObject.transform, "Training Field", new Vector2(10f, -108f),
                new Vector2(123f, 36f), "TRAINING");
            Button valley = CreateUIButton(
                panelObject.transform, "Valley Airstrip", new Vector2(143f, -108f),
                new Vector2(123f, 36f), "VALLEY");
            Button coastal = CreateUIButton(
                panelObject.transform, "Coastal Airport", new Vector2(276f, -108f),
                new Vector2(123f, 36f), "COASTAL");
            Button runway = CreateUIButton(
                panelObject.transform, "Spawn Runway", new Vector2(45f, -158f),
                new Vector2(150f, 42f), "SPAWN RUNWAY");
            Button final = CreateUIButton(
                panelObject.transform, "Spawn Final", new Vector2(215f, -158f),
                new Vector2(150f, 42f), "SPAWN FINAL");

            AirportSelectorUI selector = GetOrAdd<AirportSelectorUI>(panelObject);
            SetInt(selector, "toggleKey",
                (int)UnityEngine.InputSystem.Key.O);
            SetObject(selector, "locationManager",
                FindSceneObject("Airport Location System")?
                    .GetComponent<AirportLocationManager>());
            SetArray(selector, "airportButtons",
                new Object[] { training, valley, coastal });
            SetObject(selector, "airportInfo", info);
            SetObject(selector, "runwayButton", runway);
            SetObject(selector, "finalButton", final);
            SetObject(selector, "selectorGroup", selectorGroup);

            GameObject eventSystemObject =
                FindSceneObject("EventSystem") ?? new GameObject("EventSystem");
            GetOrAdd<EventSystem>(eventSystemObject);
            InputSystemUIInputModule inputModule =
                GetOrAdd<InputSystemUIInputModule>(eventSystemObject);
            if (inputModule.actionsAsset == null)
                inputModule.AssignDefaultActions();
        }

        private static Button CreateUIButton(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            string label)
        {
            GameObject buttonObject = FindOrCreateUIChild(parent, name);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetHUDRect(rect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                position, size);
            Image image = GetOrAdd<Image>(buttonObject);
            image.color = new Color(0.08f, 0.22f, 0.26f, 0.95f);
            Button button = GetOrAdd<Button>(buttonObject);
            button.targetGraphic = image;
            TextMeshProUGUI text = CreateHUDText(
                buttonObject.transform, "Label", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero,
                size - new Vector2(8f, 5f), 13f, TextAlignmentOptions.Center);
            text.text = label;
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static TextMeshProUGUI CreateHUDText(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = FindOrCreateUIChild(parent, name);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            SetHUDRect(rect, anchor, pivot, position, size);
            TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(textObject);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        private static GameObject FindOrCreateUIChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject child = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(child, "Create " + name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void SetHUDRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void ConfigureCamera(GameObject aircraft)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            CameraController controller = GetOrAdd<CameraController>(camera.gameObject);
            SetObject(controller, "target", aircraft.transform);
            SetObject(controller, "input", aircraft.GetComponent<AircraftInput>());
            SetObject(controller, "awarenessAssist",
                aircraft.GetComponent<PilotAwarenessAssist>());
            SetFloat(controller, "defaultYaw", 0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 15000f;
            camera.transform.SetPositionAndRotation(aircraft.transform.position + new Vector3(-13f, 6f, 0f),
                Quaternion.LookRotation(new Vector3(1f, -0.25f, 0f)));
        }

        private static WheelCollider CreateWheelCollider(Transform parent, string name, Vector3 localPosition, bool nose)
        {
            GameObject go = FindOrCreateChild(parent, name);
            go.transform.localPosition = localPosition + Vector3.up * 0.18f;
            go.transform.localRotation = Quaternion.identity;
            WheelCollider wheel = GetOrAdd<WheelCollider>(go);
            wheel.radius = nose ? 0.18f : 0.22f;
            wheel.mass = nose ? 25f : 40f;
            wheel.suspensionDistance = nose ? 0.1f : 0.14f;
            wheel.forceAppPointDistance = 0.1f;

            JointSpring spring = wheel.suspensionSpring;
            spring.spring = nose ? 26000f : 36000f;
            spring.damper = nose ? 5200f : 6800f;
            spring.targetPosition = 0.45f;
            wheel.suspensionSpring = spring;

            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.stiffness = 0f;
            wheel.forwardFriction = forward;
            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.stiffness = 0f;
            wheel.sidewaysFriction = sideways;
            return wheel;
        }

        private static Vector3 ModelToRootPosition(Vector3 modelPosition)
        {
            // Piper visual: +X forward, +Z wing axis.
            // Standard root: +Z forward, +X right.
            return new Vector3(-modelPosition.z, modelPosition.y, modelPosition.x);
        }

        private static void ConfigureWheelVisuals(WheelVisualController controller, WheelCollider[] colliders,
            Transform[] transforms)
        {
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty array = serialized.FindProperty("wheels");
            array.arraySize = colliders.Length;
            for (int i = 0; i < colliders.Length; i++)
            {
                SerializedProperty item = array.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("collider").objectReferenceValue = colliders[i];
                item.FindPropertyRelative("visual").objectReferenceValue = transforms[i];
                item.FindPropertyRelative("visualAxle").vector3Value = Vector3.forward;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateMarker()
        {
            GameObject marker = GameObject.Find(SetupMarkerName) ?? new GameObject(SetupMarkerName);
            marker.transform.SetAsFirstSibling();
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create " + name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static GameObject FindSceneObject(string name)
        {
            return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                .FirstOrDefault(go =>
                    go.scene.IsValid() &&
                    go.name == name);
        }

        private static void DestroyChildIfExists(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }

        private static void CreateLandmark(
            string name,
            Transform parent,
            Vector3 groundPosition,
            Color color)
        {
            GameObject landmark = FindOrCreateChild(parent, name);
            CreateSurface(
                "Tower",
                landmark.transform,
                groundPosition + Vector3.up * 40f,
                new Vector3(24f, 80f, 24f),
                color,
                false);
            CreateSurface(
                "Beacon",
                landmark.transform,
                groundPosition + Vector3.up * 88f,
                new Vector3(42f, 16f, 42f),
                Color.Lerp(color, Color.white, 0.2f),
                false);
        }

        private static void CreateRunwayNumber(
            Transform parent,
            string number,
            Vector3 position,
            float heading,
            Color color)
        {
            GameObject root = FindOrCreateChild(parent, $"Runway Number {number}");
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, heading, 0f);
            bool[][] digits =
            {
                new[] { true, true, true, true, true, true, false },  // 0
                new[] { false, true, true, false, false, false, false },
                new[] { true, true, false, true, true, false, true }, // 2
                new[] { true, true, true, true, false, false, true },
                new[] { false, true, true, false, false, true, true },
                new[] { true, false, true, true, false, true, true },
                new[] { true, false, true, true, true, true, true },
                new[] { true, true, true, false, false, false, false }, // 7
                new[] { true, true, true, true, true, true, true },
                new[] { true, true, true, true, false, true, true } // 9
            };
            Vector3[] positions =
            {
                new Vector3(0f, 0f, 4f),
                new Vector3(2f, 0f, 2f),
                new Vector3(2f, 0f, -2f),
                new Vector3(0f, 0f, -4f),
                new Vector3(-2f, 0f, -2f),
                new Vector3(-2f, 0f, 2f),
                Vector3.zero
            };
            Vector3[] sizes =
            {
                new Vector3(3.7f, 0.03f, 0.65f),
                new Vector3(0.65f, 0.03f, 3.7f),
                new Vector3(0.65f, 0.03f, 3.7f),
                new Vector3(3.7f, 0.03f, 0.65f),
                new Vector3(0.65f, 0.03f, 3.7f),
                new Vector3(0.65f, 0.03f, 3.7f),
                new Vector3(3.7f, 0.03f, 0.65f)
            };

            for (int digitIndex = 0; digitIndex < number.Length; digitIndex++)
            {
                int digit = number[digitIndex] - '0';
                float offset = (digitIndex - (number.Length - 1) * 0.5f) * 5.5f;
                for (int segment = 0; segment < 7; segment++)
                {
                    if (!digits[digit][segment]) continue;
                    CreateSurface($"Digit {digitIndex} Segment {segment}", root.transform,
                        positions[segment] + Vector3.right * offset,
                        sizes[segment], color, false);
                }
            }
        }

        private static void CreateSimpleHangar(Transform parent, Vector3 origin)
        {
            GameObject hangar = FindOrCreateChild(parent, "Training Hangar");
            CreateSurface("Building", hangar.transform, origin + Vector3.up * 11f,
                new Vector3(75f, 22f, 48f), new Color(0.36f, 0.39f, 0.4f), false);
            CreateSurface("Roof", hangar.transform, origin + Vector3.up * 23f,
                new Vector3(82f, 3f, 53f), new Color(0.25f, 0.28f, 0.29f), false);
            CreateSurface("Open Door", hangar.transform,
                origin + new Vector3(0f, 9f, -24.2f),
                new Vector3(42f, 17f, 0.5f), new Color(0.045f, 0.055f, 0.06f), false);
        }

        private static void CreateStaticTrainer(
            Transform parent, string name, Vector3 position, float heading)
        {
            GameObject aircraft = FindOrCreateChild(parent, name);
            aircraft.transform.localPosition = position;
            aircraft.transform.localRotation = Quaternion.Euler(0f, heading, 0f);
            CreateSurface("Fuselage", aircraft.transform, Vector3.zero,
                new Vector3(1.6f, 1.4f, 9f), new Color(0.72f, 0.76f, 0.78f), false);
            CreateSurface("Wing", aircraft.transform, new Vector3(0f, 0.25f, 0f),
                new Vector3(12f, 0.3f, 2f), new Color(0.78f, 0.8f, 0.81f), false);
            CreateSurface("Tail", aircraft.transform, new Vector3(0f, 0.5f, -3.6f),
                new Vector3(5f, 0.25f, 1.2f), new Color(0.2f, 0.55f, 0.7f), false);
        }

        private static void CreateFence(Transform parent)
        {
            GameObject fence = FindOrCreateChild(parent, "Airport Fence");
            Color fenceColor = new Color(0.38f, 0.4f, 0.4f);
            for (int x = -820; x <= -470; x += 25)
            {
                CreateSurface($"Fence Post {x}", fence.transform,
                    new Vector3(x, 1.2f, 292f), new Vector3(0.25f, 2.4f, 0.25f),
                    fenceColor, false);
            }
            CreateSurface("Fence Rail Upper", fence.transform,
                new Vector3(-645f, 1.7f, 292f), new Vector3(350f, 0.12f, 0.12f),
                fenceColor, false);
            CreateSurface("Fence Rail Lower", fence.transform,
                new Vector3(-645f, 0.7f, 292f), new Vector3(350f, 0.12f, 0.12f),
                fenceColor, false);
        }

        private static void CreateDistanceBoard(Transform parent, int x)
        {
            float distanceHundreds = (1100f - Mathf.Abs(x)) / 100f;
            Color boardColor = distanceHundreds <= 3f
                ? new Color(0.82f, 0.16f, 0.08f)
                : new Color(0.12f, 0.15f, 0.12f);
            for (int side = -1; side <= 1; side += 2)
            {
                CreateSurface($"Distance Board {x} {side}", parent,
                    new Vector3(x, 1.3f, side * 27f), new Vector3(0.5f, 2.6f, 4f),
                    boardColor, false);
            }
        }

        private static void CreateApproachLights(Transform parent, int side)
        {
            float thresholdX = side < 0 ? -1100f : 1100f;
            for (int step = 1; step <= 8; step++)
            {
                float x = thresholdX + side * step * 25f;
                CreateSurface($"Approach Light {side} {step}", parent,
                    new Vector3(x, 0.28f, 0f), new Vector3(0.5f, 0.55f, 0.5f),
                    Color.white, false);
                if (step % 2 == 0)
                {
                    CreateSurface($"Approach Crossbar {side} {step}", parent,
                        new Vector3(x, 0.22f, 0f), new Vector3(0.35f, 0.4f, 18f),
                        new Color(0.88f, 0.88f, 0.82f), false);
                }
            }
        }

        private static void CreatePapi(Transform parent, Vector3 origin, string name)
        {
            GameObject papi = FindOrCreateChild(parent, name);
            for (int index = 0; index < 4; index++)
            {
                CreateSurface($"Lamp {index}", papi.transform,
                    origin + Vector3.forward * (index - 1.5f) * 1.8f,
                    new Vector3(0.9f, 0.9f, 0.9f),
                    index < 2 ? Color.white : new Color(0.95f, 0.08f, 0.04f), false);
            }
        }

        private static void CreateSimpleTree(Transform parent, Vector3 origin, string name)
        {
            GameObject tree = FindOrCreateChild(parent, name);
            CreateSurface("Trunk", tree.transform, origin + Vector3.up * 3f,
                new Vector3(1.2f, 6f, 1.2f), new Color(0.27f, 0.17f, 0.08f), false);
            CreateSurface("Crown", tree.transform, origin + Vector3.up * 9f,
                new Vector3(7f, 10f, 7f), new Color(0.12f, 0.3f, 0.1f), false);
        }

        private static void CreateSurface(string name, Transform parent, Vector3 position, Vector3 scale,
            Color color, bool collider = true)
        {
            Transform existing = parent.Find(name);
            GameObject surface = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = name;
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = position;
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = scale;
            Collider surfaceCollider = surface.GetComponent<Collider>();
            if (surfaceCollider != null) surfaceCollider.enabled = collider;

            Renderer renderer = surface.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            renderer.sharedMaterial = material;
        }

        private static Transform FindChild(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void SetObject(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector3(Object target, string field, Vector3 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string field, Vector2 value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCurve(Object target, string field, AnimationCurve value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).animationCurveValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
#endif
