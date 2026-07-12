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
    // One-shot rollback for the abandoned Cessna visual integration.
    [InitializeOnLoad]
    public static class CessnaModelIntegration
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PiperPath =
            "Assets/EmbersStorm - Free Piper M350 Plane/Prefabs/Piper M350.Blue.prefab";
        private const string MarkerName = "Cessna Integration Reverted v2";

        static CessnaModelIntegration()
        {
            EditorApplication.update += TryRevert;
            EditorSceneManager.sceneOpened += (_, __) => TryRevert();
        }

        [MenuItem("Simply Fly/Revert Cessna Model Integration")]
        public static void RevertNow() => TryRevert();

        private static void TryRevert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != ScenePath ||
                GameObject.Find(MarkerName) != null)
                return;

            GameObject root = GameObject.Find("AircraftRoot");
            if (root == null) return;

            Transform visualRoot = root.transform.Find("VisualRoot");
            if (visualRoot != null) UnityEngine.Object.DestroyImmediate(visualRoot.gameObject);
            foreach (GameObject existingPiper in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                         .Where(go => go.name.StartsWith("Piper M350", StringComparison.OrdinalIgnoreCase)).ToArray())
                UnityEngine.Object.DestroyImmediate(existingPiper);
            foreach (GameObject oldRollbackMarker in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                         .Where(go => go.name.StartsWith("Cessna Integration Reverted", StringComparison.OrdinalIgnoreCase)).ToArray())
                UnityEngine.Object.DestroyImmediate(oldRollbackMarker);
            foreach (GameObject oldMarker in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                         .Where(go => go.name.StartsWith("Cessna Visual Integration")).ToArray())
                UnityEngine.Object.DestroyImmediate(oldMarker);

            GameObject piperAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PiperPath);
            if (piperAsset == null) throw new InvalidOperationException("Original Piper visual prefab is missing.");
            GameObject piper = (GameObject)PrefabUtility.InstantiatePrefab(
                piperAsset, SceneManager.GetActiveScene());
            piper.name = "Piper M350.Blue";
            piper.transform.SetParent(root.transform, false);
            piper.transform.localPosition = Vector3.zero;
            piper.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            piper.transform.localScale = Vector3.one;
            RemoveFlightSystemsFromVisual(piper);

            // Restore the runway-aligned spawn orientation used before the Cessna work.
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            root.transform.localScale = Vector3.one;

            Transform propeller = FindChild(piper.transform, "propeller");
            PropellerController propellerController = root.GetComponent<PropellerController>();
            if (propellerController != null && propeller != null)
            {
                SerializedObject serialized = new SerializedObject(propellerController);
                serialized.FindProperty("propeller").objectReferenceValue = propeller;
                SerializedProperty meshProperty = serialized.FindProperty("propellerMesh");
                if (meshProperty != null) meshProperty.objectReferenceValue = null;
                serialized.FindProperty("localRotationAxis").vector3Value = Vector3.right;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            RestoreWheelVisuals(root, piper.transform);
            new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorApplication.update -= TryRevert;
            Debug.Log("CESSNA_INTEGRATION_REVERTED: restored original Piper M350 visual.");
        }

        private static void RemoveFlightSystemsFromVisual(GameObject visual)
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
            foreach (string childName in new[]
                     { "Left Main Wheel Collider", "Right Main Wheel Collider", "Nose Wheel Collider" })
            {
                Transform child = visual.transform.Find(childName);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            // Removing a Rigidbody preserves the world pose. Reassert the intended
            // visual-only local pose so it follows AircraftRoot exactly.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            visual.transform.localScale = Vector3.one;
        }

        private static void DestroyComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        private static void RestoreWheelVisuals(GameObject root, Transform piper)
        {
            WheelVisualController controller = root.GetComponent<WheelVisualController>();
            if (controller == null) return;
            Transform[] wheelMeshes = piper.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Wheel", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.name).ToArray();
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty wheels = serialized.FindProperty("wheels");
            for (int i = 0; i < wheels.arraySize && i < wheelMeshes.Length; i++)
                wheels.GetArrayElementAtIndex(i).FindPropertyRelative("visual").objectReferenceValue = wheelMeshes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindChild(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase));
    }
}
#endif
