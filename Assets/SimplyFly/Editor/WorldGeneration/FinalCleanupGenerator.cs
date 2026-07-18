using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimplyFly.WorldGeneration.Editor
{
    public sealed class FinalCleanupGenerator : IWorldGenerator
    {
        public string Name => "Final cleanup";
        public void Generate(WorldGenerationContext context)
        {
            foreach (Transform child in context.root.GetComponentsInChildren<Transform>(true))
            {
                if (child == context.root.transform) continue;
                if (child.childCount == 0 && child.GetComponent<Renderer>() == null &&
                    child.name.EndsWith("_Edge")) Undo.DestroyObjectImmediate(child.gameObject);
            }
            EditorUtility.SetDirty(context.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
