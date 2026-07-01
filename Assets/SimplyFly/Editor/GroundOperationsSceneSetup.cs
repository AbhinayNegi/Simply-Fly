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
        private const string SetupMarkerName = "Sprint 1 - Ground Operations v2";

        static GroundOperationsSceneSetup()
        {
            EditorApplication.delayCall += TryInitialSetup;
        }

        [MenuItem("Simply Fly/Build Sprint 1 Ground Operations Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject aircraft = FindAircraft();
            if (aircraft == null)
            {
                Debug.LogError("Simply Fly: Piper M350 aircraft was not found in the open scene.");
                return;
            }

            Undo.SetCurrentGroupName("Build Simply Fly Sprint 1");
            CreateAirport();
            ConfigureAircraft(aircraft);
            ConfigureCamera(aircraft);
            CreateMarker();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Simply Fly: Sprint 1 ground operations scene is ready. Press Play, then E to start.");
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

        private static void ConfigureAircraft(GameObject aircraft)
        {
            // The original wheel pivots sit 1.5 m below the root. At 1.55 m the
            // WheelCollider ray origins were inside the runway, so isGrounded stayed false.
            aircraft.transform.SetPositionAndRotation(new Vector3(-520f, 2.05f, 0f), Quaternion.identity);

            Rigidbody body = GetOrAdd<Rigidbody>(aircraft);
            body.mass = 1200f;
            body.linearDamping = 0.02f;
            body.angularDamping = 3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(-0.2f, -0.45f, 0f);

            MeshCollider oldCollider = aircraft.GetComponent<MeshCollider>();
            if (oldCollider != null) oldCollider.enabled = false;
            BoxCollider fuselage = GetOrAdd<BoxCollider>(aircraft);
            fuselage.center = new Vector3(-0.25f, 0.05f, 0f);
            fuselage.size = new Vector3(6.6f, 1.75f, 1.65f);

            Transform leftVisual = FindChild(aircraft.transform, "Wheel1.002");
            Transform rightVisual = FindChild(aircraft.transform, "Wheel2.002");
            Transform noseVisual = FindChild(aircraft.transform, "Wheel3.002");
            Transform propeller = FindChild(aircraft.transform, "propeller");

            WheelCollider left = CreateWheelCollider(aircraft.transform, "Left Main Wheel Collider",
                leftVisual != null ? leftVisual.localPosition : new Vector3(-0.2f, -1.53f, -2.88f), false);
            WheelCollider right = CreateWheelCollider(aircraft.transform, "Right Main Wheel Collider",
                rightVisual != null ? rightVisual.localPosition : new Vector3(-0.2f, -1.53f, 2.88f), false);
            WheelCollider nose = CreateWheelCollider(aircraft.transform, "Nose Wheel Collider",
                noseVisual != null ? noseVisual.localPosition : new Vector3(2.58f, -1.48f, 0f), true);
            WheelCollider[] wheels = { left, right, nose };

            AircraftInput input = GetOrAdd<AircraftInput>(aircraft);
            ThrottleController throttle = GetOrAdd<ThrottleController>(aircraft);
            EngineController engine = GetOrAdd<EngineController>(aircraft);
            BrakeController brakes = GetOrAdd<BrakeController>(aircraft);
            SteeringController steering = GetOrAdd<SteeringController>(aircraft);
            GroundMovementController movement = GetOrAdd<GroundMovementController>(aircraft);
            PropellerController propellerController = GetOrAdd<PropellerController>(aircraft);
            EngineAudioController audio = GetOrAdd<EngineAudioController>(aircraft);
            WheelVisualController visuals = GetOrAdd<WheelVisualController>(aircraft);
            UIController ui = GetOrAdd<UIController>(aircraft);

            SetObject(throttle, "input", input);
            SetObject(engine, "input", input);
            SetObject(engine, "throttle", throttle);
            SetObject(brakes, "input", input);
            SetObject(steering, "input", input);
            SetObject(steering, "noseWheel", nose);
            SetObject(steering, "aircraftBody", body);
            SetObject(movement, "engine", engine);
            SetObject(movement, "throttle", throttle);
            SetObject(movement, "brakes", brakes);
            SetArray(movement, "wheels", wheels);
            SetObject(propellerController, "engine", engine);
            SetObject(propellerController, "propeller", propeller);
            SetObject(audio, "engine", engine);
            SetObject(ui, "engine", engine);
            SetObject(ui, "throttle", throttle);
            SetObject(ui, "movement", movement);
            SetObject(ui, "brakes", brakes);
            SetObject(ui, "steering", steering);
            ConfigureWheelVisuals(visuals, wheels, new[] { leftVisual, rightVisual, noseVisual });
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
            SetFloat(controller, "defaultYaw", 90f);
            camera.transform.SetPositionAndRotation(aircraft.transform.position + new Vector3(-13f, 6f, 0f),
                Quaternion.LookRotation(new Vector3(1f, -0.25f, 0f)));
        }

        private static WheelCollider CreateWheelCollider(Transform parent, string name, Vector3 localPosition, bool nose)
        {
            GameObject go = FindOrCreateChild(parent, name);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            WheelCollider wheel = GetOrAdd<WheelCollider>(go);
            wheel.radius = nose ? 0.34f : 0.42f;
            wheel.mass = nose ? 25f : 40f;
            wheel.suspensionDistance = nose ? 0.18f : 0.24f;
            wheel.forceAppPointDistance = 0.1f;

            JointSpring spring = wheel.suspensionSpring;
            spring.spring = nose ? 26000f : 36000f;
            spring.damper = nose ? 5200f : 6800f;
            spring.targetPosition = 0.45f;
            wheel.suspensionSpring = spring;

            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.stiffness = 1.35f;
            wheel.forwardFriction = forward;
            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.stiffness = nose ? 1.15f : 1.55f;
            wheel.sidewaysFriction = sideways;
            return wheel;
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
    }
}
#endif
