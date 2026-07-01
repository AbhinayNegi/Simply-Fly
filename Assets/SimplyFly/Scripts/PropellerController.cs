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
        [Min(0f)] [SerializeField] private float blurStartRPM = 600f;
        [Range(4, 24)] [SerializeField] private int blurCopyCount = 12;
        [Range(0.01f, 0.5f)] [SerializeField] private float blurCopyOpacity = 0.055f;
        [Tooltip("Visible blur rotation at the blur threshold, in degrees per second.")]
        [Min(1f)] [SerializeField] private float minimumBlurVisualSpeed = 180f;
        [Tooltip("Visible blur rotation at maximum RPM, in degrees per second.")]
        [Min(1f)] [SerializeField] private float maximumBlurVisualSpeed = 520f;

        private Quaternion authoredRotation;
        private double accumulatedAngle;
        private MeshRenderer solidRenderer;
        private MeshRenderer[] blurRenderers;
        private Transform[] blurTransforms;
        private float blurVisualAngle;

        private void Awake()
        {
            if (propeller == null) return;
            if (blurCopyCount < 4) blurCopyCount = 12;
            if (blurCopyOpacity <= 0f) blurCopyOpacity = 0.055f;
            if (minimumBlurVisualSpeed <= 0f) minimumBlurVisualSpeed = 180f;
            if (maximumBlurVisualSpeed <= minimumBlurVisualSpeed)
                maximumBlurVisualSpeed = 520f;
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
            if (showBlur)
            {
                float highRpmProgress = Mathf.InverseLerp(blurStartRPM, 2700f, engine.CurrentRPM);
                float visualSpeed = Mathf.Lerp(
                    minimumBlurVisualSpeed, maximumBlurVisualSpeed, highRpmProgress);
                blurVisualAngle = Mathf.Repeat(
                    blurVisualAngle + visualSpeed * Time.deltaTime, 360f);
            }
            if (solidRenderer != null) solidRenderer.enabled = !showBlur;
            if (blurRenderers == null) return;
            for (int i = 0; i < blurRenderers.Length; i++)
            {
                if (blurRenderers[i] == null) continue;
                blurRenderers[i].enabled = showBlur;
                if (showBlur && blurTransforms[i] != null)
                {
                    // Cancel physically sampled rotation, then add a controlled positive
                    // visual rotation. Its speed rises monotonically with RPM, preventing
                    // wagon-wheel reversal while keeping full throttle visibly alive.
                    float offset = 360f * i / blurRenderers.Length;
                    blurTransforms[i].localRotation = Quaternion.AngleAxis(
                        offset + blurVisualAngle - displayedAngle, localRotationAxis.normalized);
                }
            }
        }

        private void CreateBlurCopies()
        {
            MeshFilter sourceFilter = propeller.GetComponent<MeshFilter>();
            if (sourceFilter == null || solidRenderer == null || sourceFilter.sharedMesh == null) return;

            blurRenderers = new MeshRenderer[blurCopyCount];
            blurTransforms = new Transform[blurCopyCount];
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
                float opacityVariation = Mathf.Lerp(0.72f, 1.28f, (i % 4) / 3f);
                renderer.sharedMaterials = CreateTransparentMaterials(
                    solidRenderer.sharedMaterials, opacityVariation);
                renderer.enabled = false;
                blurRenderers[i] = renderer;
                blurTransforms[i] = copy.transform;
            }
        }

        private Material[] CreateTransparentMaterials(Material[] sourceMaterials, float opacityMultiplier)
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
                    color.a = blurCopyOpacity * opacityMultiplier;
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = blurCopyOpacity * opacityMultiplier;
                    material.color = color;
                }
                materials[i] = material;
            }
            return materials;
        }

        public void ResetPropeller()
        {
            accumulatedAngle = 0d;
            blurVisualAngle = 0f;
            if (propeller != null) propeller.localRotation = authoredRotation;
            if (solidRenderer != null) solidRenderer.enabled = true;
            if (blurRenderers == null) return;
            foreach (MeshRenderer renderer in blurRenderers)
                if (renderer != null) renderer.enabled = false;
        }
    }
}
