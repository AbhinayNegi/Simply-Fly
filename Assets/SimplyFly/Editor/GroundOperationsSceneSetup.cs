#if UNITY_EDITOR
using System.Linq;
using SimplyFly.GroundOps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimplyFly.Editor
{
    [InitializeOnLoad]
    public static class GroundOperationsSceneSetup
    {
        private const string SetupMarkerName = "Sprint 2 - Camera Follow v1";

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
            return Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
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
                new Vector3(-520f, 1.73f, 0f),
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
            GameObject airport = GameObject.Find("Airport") ?? new GameObject("Airport");
            CreateSurface("Airport Ground", airport.transform, new Vector3(0f, -0.3f, 0f),
                new Vector3(1600f, 0.4f, 1600f), new Color(0.17f, 0.25f, 0.13f));
            CreateSurface("Runway 09-27", airport.transform, new Vector3(0f, -0.08f, 0f),
                new Vector3(1200f, 0.16f, 45f), new Color(0.12f, 0.12f, 0.13f));

            GameObject markings = FindOrCreateChild(airport.transform, "Runway Markings");
            for (int x = -540; x <= 540; x += 30)
            {
                CreateSurface($"Centerline {x}", markings.transform, new Vector3(x, 0.015f, 0f),
                    new Vector3(15f, 0.025f, 0.35f), new Color(0.92f, 0.92f, 0.88f), false);
            }

            CreateSurface("Left Edge", markings.transform, new Vector3(0f, 0.012f, -21f),
                new Vector3(1160f, 0.02f, 0.28f), Color.white, false);
            CreateSurface("Right Edge", markings.transform, new Vector3(0f, 0.012f, 21f),
                new Vector3(1160f, 0.02f, 0.28f), Color.white, false);
        }

        private static void ConfigureAircraft(GameObject aircraft, GameObject visual)
        {
            aircraft.transform.SetPositionAndRotation(
                new Vector3(-520f, 1.73f, 0f),
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
            AircraftResetController resetController = GetOrAdd<AircraftResetController>(aircraft);
            PropellerController propellerController = GetOrAdd<PropellerController>(aircraft);
            EngineAudioController audio = GetOrAdd<EngineAudioController>(aircraft);
            WheelVisualController visuals = GetOrAdd<WheelVisualController>(aircraft);
            UIController ui = GetOrAdd<UIController>(aircraft);

            SetObject(throttle, "input", input);
            SetObject(engine, "input", input);
            SetObject(engine, "throttle", throttle);
            SetObject(brakes, "input", input);
            SetFloat(brakes, "brakeStrength", 8500f);
            SetFloat(brakes, "brakeResponseSpeed", 3.5f);
            SetObject(steering, "input", input);
            SetObject(steering, "noseWheel", nose);
            SetObject(steering, "aircraftBody", body);
            SetObject(steering, "movement", movement);
            SetObject(steering, "flightPhysics", flightPhysics);
            SetVector3(steering, "localForwardAxis", Vector3.forward);
            SetObject(movement, "engine", engine);
            SetObject(movement, "throttle", throttle);
            SetObject(movement, "brakes", brakes);
            SetArray(movement, "wheels", wheels);
            SetVector3(movement, "localForwardAxis", Vector3.forward);
            SetFloat(movement, "idleThrust", 420f);
            SetFloat(movement, "maxGroundThrust", 4800f);
            SetFloat(movement, "throttleResponseSpeed", 0.55f);
            SetFloat(movement, "maximumTaxiSpeed", 21f);
            SetFloat(movement, "rollingResistance", 75f);
            SetFloat(movement, "idleCreepSpeed", 1.35f);
            SetFloat(movement, "breakawayThrust", 0.28f);
            SetFloat(movement, "staticFrictionSoftness", 1.25f);
            SetObject(flightPhysics, "input", input);
            SetObject(flightPhysics, "engine", engine);
            SetObject(flightPhysics, "throttle", throttle);
            SetObject(flightPhysics, "groundMovement", movement);
            SetArray(flightPhysics, "groundCheckPoints",
                new Object[] { left.transform, right.transform, nose.transform });
            SetVector3(flightPhysics, "localForwardAxis", Vector3.forward);
            SetVector3(flightPhysics, "localPitchAxis", Vector3.left);
            SetFloat(flightPhysics, "takeoffSpeed", 19.5f);
            SetFloat(flightPhysics, "maxLiftForce", 16500f);
            SetFloat(flightPhysics, "pitchLiftInfluence", 0.38f);
            SetFloat(flightPhysics, "levelLiftFactor", 0.68f);
            SetFloat(flightPhysics, "referenceAngleOfAttack", 8f);
            SetFloat(flightPhysics, "rotationSpeed", 17.5f);
            SetFloat(flightPhysics, "elevatorAuthority", 2.2f);
            SetFloat(flightPhysics, "pitchResponse", 2.4f);
            SetFloat(flightPhysics, "elevatorReturnSpeed", 3f);
            SetFloat(flightPhysics, "pitchDamping", 3.2f);
            SetFloat(flightPhysics, "maxRotationPitchAngle", 9f);
            SetFloat(flightPhysics, "neutralAoA", 0.75f);
            SetFloat(flightPhysics, "aoADeadband", 0.5f);
            SetFloat(flightPhysics, "aerodynamicPitchStability", 0.085f);
            SetFloat(flightPhysics, "maxStabilityMoment", 0.45f);
            SetFloat(flightPhysics, "virtualTailArm", 2.7f);
            SetFloat(flightPhysics, "tailStability", 0.42f);
            SetFloat(flightPhysics, "tailPitchRateInfluence", 0.32f);
            SetFloat(flightPhysics, "maxTailForce", 1700f);
            SetFloat(flightPhysics, "descentRecoveryAoAInfluence", 0.32f);
            SetFloat(flightPhysics, "maxDescentRecoveryAoA", 6f);
            SetFloat(flightPhysics, "runwayPitchLimitStrength", 0.65f);
            SetFloat(flightPhysics, "maxAirbornePitchAngle", 45f);
            SetFloat(flightPhysics, "airborneSafetyStrength", 0.18f);
            SetFloat(flightPhysics, "liftoffConfirmationTime", 0.22f);
            SetFloat(flightPhysics, "groundedCheckDistance", 0.14f);
            SetFloat(flightPhysics, "aerodynamicDrag", 2.2f);
            SetObject(propellerController, "engine", engine);
            SetObject(propellerController, "propeller", propeller);
            SetFloat(propellerController, "blurStartRPM", 600f);
            SetInt(propellerController, "blurCopyCount", 12);
            SetFloat(propellerController, "blurCopyOpacity", 0.055f);
            SetFloat(propellerController, "minimumBlurVisualSpeed", 180f);
            SetFloat(propellerController, "maximumBlurVisualSpeed", 520f);
            SetObject(audio, "engine", engine);
            SetObject(ui, "engine", engine);
            SetObject(ui, "throttle", throttle);
            SetObject(ui, "movement", movement);
            SetObject(ui, "brakes", brakes);
            SetObject(ui, "steering", steering);
            SetObject(ui, "flightPhysics", flightPhysics);
            SetObject(ui, "resetController", resetController);
            SetObject(resetController, "engine", engine);
            SetObject(resetController, "throttle", throttle);
            SetObject(resetController, "brakes", brakes);
            SetObject(resetController, "steering", steering);
            SetObject(resetController, "groundMovement", movement);
            SetObject(resetController, "flightPhysics", flightPhysics);
            SetObject(resetController, "propeller", propellerController);
            ConfigureWheelVisuals(visuals, wheels, new[] { leftVisual, rightVisual, noseVisual });
            SetObject(visuals, "movement", movement);
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

        private static void SetInt(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
