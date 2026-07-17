#if UNITY_EDITOR
using System;
using System.Linq;
using SimplyFly.GroundOps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimplyFly.Editor
{
    /// <summary>One-time scene migration from the placeholder Piper to the imported Cessna.</summary>
    [InitializeOnLoad]
    public static class CessnaModelIntegration
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ModelPath = "Assets/Models/Cessna172_Unity.fbx";
        private const string VisualName = "Cessna172_Unity";
        private const string MarkerName = "Cessna Visual Integration v8";
        private static bool queued;

        static CessnaModelIntegration()
        {
            QueueIntegration();
            EditorSceneManager.sceneOpened += (_, __) => QueueIntegration();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) QueueIntegration();
            };
        }

        [MenuItem("Simply Fly/Integrate Imported Cessna 172")]
        public static void IntegrateNow() => Integrate();

        private static void QueueIntegration()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () =>
            {
                queued = false;
                Integrate();
            };
        }

        private static void Integrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != ScenePath ||
                GameObject.Find(MarkerName) != null)
                return;

            GameObject aircraft = GameObject.Find("AircraftRoot");
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (aircraft == null || modelAsset == null) return;

            Undo.SetCurrentGroupName("Integrate imported Cessna 172");
            RemoveOldVisuals(aircraft.transform);

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(
                modelAsset, SceneManager.GetActiveScene());
            visual.name = VisualName;
            visual.transform.SetParent(aircraft.transform, false);
            visual.transform.localPosition = Vector3.zero;
            // This FBX arrives nose-down. Pitching it -90 degrees makes the fuselage
            // horizontal, then a parent-space 180-degree roll puts the wheels underneath.
            // Quaternion multiplication is explicit so Unity's Euler order cannot swap
            // the pitch/roll operations.
            visual.transform.localRotation =
                Quaternion.AngleAxis(180f, Vector3.forward) *
                Quaternion.AngleAxis(-90f, Vector3.right);
            visual.transform.localScale = Vector3.one;
            RemoveImportedPhysics(visual);
            AlignVisualToPhysics(aircraft, visual);
            BindAnimatedParts(aircraft, visual.transform);
            UpdateCameraFocus(aircraft, visual);

            new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("CESSNA_INTEGRATION_COMPLETE: imported Cessna replaced the Piper and was corrected to +Z nose-forward.");
        }

        private static void RemoveOldVisuals(Transform aircraft)
        {
            foreach (Transform child in aircraft.Cast<Transform>().ToArray())
            {
                bool oldVisual = child.name.StartsWith("Piper M350", StringComparison.OrdinalIgnoreCase) ||
                                 child.name == VisualName || child.name == "VisualRoot";
                if (oldVisual) Undo.DestroyObjectImmediate(child.gameObject);
            }
            foreach (GameObject marker in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include)
                     .Where(go => go.name.StartsWith("Cessna Integration Reverted", StringComparison.OrdinalIgnoreCase) ||
                                  go.name.StartsWith("Cessna Visual Integration", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
                Undo.DestroyObjectImmediate(marker);
        }

        private static void RemoveImportedPhysics(GameObject visual)
        {
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Undo.DestroyObjectImmediate(collider);
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
                Undo.DestroyObjectImmediate(body);
            foreach (Camera camera in visual.GetComponentsInChildren<Camera>(true))
                Undo.DestroyObjectImmediate(camera);
            foreach (AudioListener listener in visual.GetComponentsInChildren<AudioListener>(true))
                Undo.DestroyObjectImmediate(listener);
        }

        private static void AlignVisualToPhysics(GameObject aircraft, GameObject visual)
        {
            // Calibrated against the Main scene runway and the existing wheel-support
            // colliders. Renderer bounds are unreliable for this FBX because its baked
            // axis conversion changes which bounds face is treated as the bottom.
            visual.transform.localPosition = new Vector3(0f, -1.6f, 3.78827f);
        }

        private static bool TryAlignWheelsToGroundSupports(
            GameObject aircraft, GameObject visual)
        {
            Transform[] visualWheels =
            {
                FindChild(visual.transform, "Wheel_L"),
                FindChild(visual.transform, "Wheel_R"),
                FindChild(visual.transform, "Wheel_Nose")
            };
            string[] supportNames =
            {
                "Left Main Wheel Collider",
                "Right Main Wheel Collider",
                "Nose Wheel Collider"
            };

            Vector3 visualCenterSum = Vector3.zero;
            Vector3 supportCenterSum = Vector3.zero;
            float visualBottomSum = 0f;
            float supportBottomSum = 0f;
            int count = 0;
            for (int i = 0; i < visualWheels.Length; i++)
            {
                Transform wheel = visualWheels[i];
                Transform support = aircraft.transform.Find(supportNames[i]);
                Renderer renderer = wheel == null
                    ? null
                    : wheel.GetComponentInChildren<Renderer>(true);
                SphereCollider sphere = support == null
                    ? null
                    : support.GetComponent<SphereCollider>();
                if (renderer == null || sphere == null) continue;

                Bounds wheelBounds = renderer.bounds;
                Vector3 wheelCenter = aircraft.transform.InverseTransformPoint(wheelBounds.center);
                Vector3 sphereCenter = aircraft.transform.InverseTransformPoint(
                    sphere.transform.TransformPoint(sphere.center));
                float wheelBottom = aircraft.transform.InverseTransformPoint(
                    new Vector3(wheelBounds.center.x, wheelBounds.min.y,
                        wheelBounds.center.z)).y;
                float supportBottom = sphereCenter.y - sphere.radius;

                visualCenterSum += wheelCenter;
                supportCenterSum += sphereCenter;
                visualBottomSum += wheelBottom;
                supportBottomSum += supportBottom;
                count++;
            }
            if (count == 0) return false;

            Vector3 visualCenter = visualCenterSum / count;
            Vector3 supportCenter = supportCenterSum / count;
            visual.transform.localPosition += new Vector3(
                supportCenter.x - visualCenter.x,
                supportBottomSum / count - visualBottomSum / count,
                supportCenter.z - visualCenter.z);
            return true;
        }

        private static bool TryGetLocalRendererBounds(
            Transform aircraft, GameObject visual, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Vector3[] corners = GetBoundsCorners(renderer.bounds);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = aircraft.InverseTransformPoint(corner);
                    if (!found) { bounds = new Bounds(local, Vector3.zero); found = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return found;
        }

        private static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
            };
        }

        private static void BindAnimatedParts(GameObject aircraft, Transform visual)
        {
            // Rotate the authored pivot. PropellerController can source the blade mesh
            // from its child while retaining this correct shaft pivot.
            Transform propeller = FindChild(visual, "Propeller_Root") ??
                                  FindChild(visual, "Propeller");
            PropellerController propellerController = aircraft.GetComponent<PropellerController>();
            if (propellerController != null && propeller != null)
            {
                SerializedObject serialized = new(propellerController);
                serialized.FindProperty("propeller").objectReferenceValue = propeller;
                serialized.FindProperty("localRotationAxis").vector3Value = Vector3.up;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            WheelVisualController wheelController = aircraft.GetComponent<WheelVisualController>();
            if (wheelController == null) return;
            Transform[] wheelVisuals =
            {
                FindChild(visual, "Wheel_L"),
                FindChild(visual, "Wheel_R"),
                FindChild(visual, "Wheel_Nose")
            };
            SerializedObject wheelSerialized = new(wheelController);
            SerializedProperty wheels = wheelSerialized.FindProperty("wheels");
            for (int i = 0; i < wheels.arraySize && i < wheelVisuals.Length; i++)
            {
                SerializedProperty item = wheels.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("visual").objectReferenceValue = wheelVisuals[i];
                item.FindPropertyRelative("visualAxle").vector3Value = Vector3.right;
            }
            wheelSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateCameraFocus(GameObject aircraft, GameObject visual)
        {
            Transform focus = aircraft.transform.Find("CameraFocusTarget");
            if (focus == null) return;
            if (!TryGetLocalRendererBounds(aircraft.transform, visual, out Bounds bounds)) return;
            focus.localPosition = new Vector3(
                bounds.center.x,
                bounds.center.y + bounds.extents.y * 0.18f,
                bounds.center.z);
        }

        private static Transform FindChild(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => string.Equals(t.name, name,
                    StringComparison.OrdinalIgnoreCase));
    }
}
#endif
