#if UNITY_EDITOR
using System.Linq;
using SimplyFly.GroundOps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SimplyFly.Editor
{
    [InitializeOnLoad]
    public static class GroundOperationsSceneSetup
    {
        private const string SetupMarkerName = "Sprint 9 - Assisted Trim v1";
        private static readonly Vector3 AircraftSpawnPosition = new Vector3(-1020f, 1.73f, 0f);
        private const string EngineLoopAssetPath =
            "Assets/SimplyFly/Audio/airplane_prop_loop.ogg";

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
            ConfigureHUD(aircraftRoot);
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
            DestroyComponent<WheelVisualController>(visual);
            DestroyComponent<EngineAudioController>(visual);
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

        private static void CreateAirport()
        {
            GameObject airport =
                GameObject.Find("Flight Test Field") ??
                GameObject.Find("Airport") ??
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
            ThrottleController throttle = GetOrAdd<ThrottleController>(aircraft);
            EngineController engine = GetOrAdd<EngineController>(aircraft);
            BrakeController brakes = GetOrAdd<BrakeController>(aircraft);
            SteeringController steering = GetOrAdd<SteeringController>(aircraft);
            GroundMovementController movement = GetOrAdd<GroundMovementController>(aircraft);
            FlightPhysicsController flightPhysics = GetOrAdd<FlightPhysicsController>(aircraft);
            FlapController flaps = GetOrAdd<FlapController>(aircraft);
            AssistedTrimController trim = GetOrAdd<AssistedTrimController>(aircraft);
            StallController stall = GetOrAdd<StallController>(aircraft);
            ApproachGuidanceController approach =
                GetOrAdd<ApproachGuidanceController>(aircraft);
            LandingController landing = GetOrAdd<LandingController>(aircraft);
            AircraftResetController resetController = GetOrAdd<AircraftResetController>(aircraft);
            PropellerController propellerController = GetOrAdd<PropellerController>(aircraft);
            EngineAudioController audio = GetOrAdd<EngineAudioController>(aircraft);
            WheelVisualController visuals = GetOrAdd<WheelVisualController>(aircraft);
            UIController ui = GetOrAdd<UIController>(aircraft);

            SetObject(throttle, "input", input);
            SetObject(engine, "input", input);
            SetObject(engine, "throttle", throttle);
            SetFloat(engine, "offRPM", 0f);
            SetObject(brakes, "input", input);
            SetFloat(brakes, "brakeStrength", 8500f);
            SetFloat(brakes, "brakeResponseSpeed", 3.5f);
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
            SetFloat(steering, "wheelbase", 2.8f);
            SetFloat(steering, "yawResponse", 5f);
            SetFloat(steering, "maximumYawAcceleration", 1.6f);
            SetFloat(steering, "neutralYawRateDeadband", 0.015f);
            SetObject(movement, "engine", engine);
            SetObject(movement, "throttle", throttle);
            SetObject(movement, "brakes", brakes);
            SetArray(movement, "wheels", wheels);
            SetVector3(movement, "localForwardAxis", Vector3.forward);
            SetFloat(movement, "idleThrust", 420f);
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
            SetFloat(movement, "idleCreepSpeed", 1.8f);
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
            SetFloat(flightPhysics, "lateralSlipDamping", 0.7f);
            SetFloat(flightPhysics, "coordinatedTurnStrength", 3f);
            SetFloat(flightPhysics, "bankTurnStrength", 1f);
            SetFloat(flightPhysics, "maxAssistedYawRate", 18f);
            SetFloat(flightPhysics, "yawDamping", 0.7f);
            SetFloat(flightPhysics, "maxLateralSlipAcceleration", 14f);
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
            SetObject(flaps, "input", input);
            SetObject(flaps, "flightPhysics", flightPhysics);
            SetVector3(flaps, "localPitchAxis", Vector3.left);
            SetFloat(flaps, "flapDeploySpeed", 10f);
            SetFloat(flaps, "flapRetractSpeed", 14f);
            SetFloat(flaps, "flapLiftBonusPerStep", 0.08f);
            SetFloat(flaps, "maxFlapLiftBonus", 0.24f);
            SetFloat(flaps, "flapDragPerStep", 0.55f);
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
            AudioClip engineLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(EngineLoopAssetPath);
            SetObject(audio, "engineLoop", engineLoop);
            SetVector2(audio, "pitchRange", new Vector2(0.75f, 1.55f));
            SetVector2(audio, "volumeRange", new Vector2(0.28f, 0.72f));
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

            TextMeshProUGUI warning = CreateHUDText(
                hud.transform, "Flight Warning", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -165f),
                new Vector2(520f, 70f), 40f, TextAlignmentOptions.Center);
            warning.fontStyle = FontStyles.Bold;
            warning.raycastTarget = false;

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
            SetObject(controller, "landing", aircraft.GetComponent<LandingController>());
            SetObject(controller, "debugUI", aircraft.GetComponent<UIController>());
            SetObject(controller, "hudGroup", hudGroup);
            SetObject(controller, "airspeedInstrument", airspeed);
            SetObject(controller, "altitudeInstrument", altitude);
            SetObject(controller, "verticalSpeedInstrument", verticalSpeed);
            SetObject(controller, "headingInstrument", heading);
            SetObject(controller, "powerInstrument", power);
            SetObject(controller, "attitudeInstrument", attitude);
            SetObject(controller, "warningLabel", warning);
            SetObject(controller, "landingFeedbackLabel", feedback);
            SetObject(controller, "landingFeedbackGroup", feedbackGroup);
            SetObject(controller, "trimView", trimView);
            SetFloat(controller, "safeApproachSpeedMin", 65f);
            SetFloat(controller, "safeApproachSpeedMax", 75f);
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

        private static void SetInt(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
