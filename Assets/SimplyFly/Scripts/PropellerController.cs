using UnityEngine;
using UnityEngine.Rendering;

namespace SimplyFly.GroundOps
{
    public sealed class PropellerController : MonoBehaviour
    {
        [SerializeField] private EngineController engine;
        [SerializeField] private Transform propeller;
        [SerializeField] private Vector3 localRotationAxis = Vector3.right;
        [Tooltip("Visual rotation multiplier. Lower this if the propeller aliases on screen.")]
        [Min(0.01f)] [SerializeField] private float rotationScale = 1f;
        [Header("High RPM blur")]
        [Min(0f)] [SerializeField] private float blurStartRPM = 1200f;
        [Range(2, 12)] [SerializeField] private int blurCopyCount = 6;
        [Range(0.01f, 0.5f)] [SerializeField] private float blurCopyOpacity = 0.1f;

        private Quaternion authoredRotation;
        private double accumulatedAngle;
        private MeshRenderer solidRenderer;
        private MeshRenderer[] blurRenderers;

        private void Awake()
        {
            if (propeller == null) return;
            authoredRotation = propeller.localRotation;
            solidRenderer = propeller.GetComponent<MeshRenderer>();
            CreateBlurCopies();
        }

        private void LateUpdate()
        {
            if (engine == null || propeller == null) return;

            // RPM * 360 / 60 * deltaTime. The accumulator only ever advances:
            // throttle changes alter its rate, never its direction or current angle.
            accumulatedAngle += engine.CurrentRPM * 360.0 / 60.0 * rotationScale * Time.deltaTime;
            float displayedAngle = (float)(accumulatedAngle % 360.0);
            propeller.localRotation =
                authoredRotation * Quaternion.AngleAxis(displayedAngle, localRotationAxis.normalized);

            bool showBlur = engine.CurrentRPM >= blurStartRPM;
            if (solidRenderer != null) solidRenderer.enabled = !showBlur;
            if (blurRenderers == null) return;
            foreach (MeshRenderer blurRenderer in blurRenderers)
                if (blurRenderer != null) blurRenderer.enabled = showBlur;
        }

        private void CreateBlurCopies()
        {
            MeshFilter sourceFilter = propeller.GetComponent<MeshFilter>();
            if (sourceFilter == null || solidRenderer == null || sourceFilter.sharedMesh == null) return;

            blurRenderers = new MeshRenderer[blurCopyCount];
            for (int i = 0; i < blurCopyCount; i++)
            {
                GameObject copy = new GameObject($"Propeller Blur {i + 1}");
                copy.transform.SetParent(propeller, false);
                copy.transform.localRotation =
                    Quaternion.AngleAxis(360f * i / blurCopyCount, localRotationAxis.normalized);

                MeshFilter filter = copy.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterials = CreateTransparentMaterials(solidRenderer.sharedMaterials);
                renderer.enabled = false;
                blurRenderers[i] = renderer;
            }
        }

        private Material[] CreateTransparentMaterials(Material[] sourceMaterials)
        {
            Material[] materials = new Material[sourceMaterials.Length];
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material material = new Material(sourceMaterials[i])
                {
                    name = sourceMaterials[i].name + " (Prop Blur)",
                    renderQueue = (int)RenderQueue.Transparent
                };
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    color.a = blurCopyOpacity;
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = blurCopyOpacity;
                    material.color = color;
                }
                materials[i] = material;
            }
            return materials;
        }
    }
}
