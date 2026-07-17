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
        [Range(0.01f, 0.5f)] [SerializeField] private float blurCopyOpacity = 0.11f;
        [Tooltip("Brightness multiplier for the solid and blurred propeller blades.")]
        [Range(0.05f, 1f)] [SerializeField] private float propellerBrightness = 0.2f;
        [Range(0.02f, 0.5f)] [SerializeField] private float propellerDiscOpacity = 0.18f;
        [Range(1f, 1.25f)] [SerializeField] private float propellerDiscRadiusScale = 1.06f;
        [Tooltip("Visible blur rotation at the blur threshold, in degrees per second.")]
        [Min(1f)] [SerializeField] private float minimumBlurVisualSpeed = 180f;
        [Tooltip("Visible blur rotation at maximum RPM, in degrees per second.")]
        [Min(1f)] [SerializeField] private float maximumBlurVisualSpeed = 520f;

        private Quaternion authoredRotation;
        private double accumulatedAngle;
        private MeshRenderer solidRenderer;
        private MeshRenderer[] blurRenderers;
        private Transform[] blurTransforms;
        private MeshRenderer blurDiscRenderer;
        private Quaternion blurMeshAuthoredRotation;
        private float blurVisualAngle;

        private void Awake()
        {
            if (propeller == null) return;
            if (blurCopyCount < 4) blurCopyCount = 12;
            if (blurCopyOpacity <= 0f) blurCopyOpacity = 0.11f;
            if (propellerBrightness <= 0f) propellerBrightness = 0.2f;
            if (propellerDiscOpacity <= 0f) propellerDiscOpacity = 0.18f;
            if (propellerDiscRadiusScale < 1f) propellerDiscRadiusScale = 1.06f;
            if (minimumBlurVisualSpeed <= 0f) minimumBlurVisualSpeed = 180f;
            if (maximumBlurVisualSpeed <= minimumBlurVisualSpeed)
                maximumBlurVisualSpeed = 520f;
            authoredRotation = propeller.localRotation;
            // Imported aircraft commonly use an empty pivot with the visible blade mesh
            // beneath it. Source the renderer from that child while rotating the pivot.
            solidRenderer = propeller.GetComponentInChildren<MeshRenderer>(true);
            DarkenSolidPropeller();
            CreateBlurCopies();
            CreateBlurDisc();
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
            if (blurDiscRenderer != null) blurDiscRenderer.enabled = showBlur;
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
                    blurTransforms[i].localRotation =
                        Quaternion.AngleAxis(offset + blurVisualAngle - displayedAngle,
                            localRotationAxis.normalized) * blurMeshAuthoredRotation;
                }
            }
        }

        private void CreateBlurCopies()
        {
            MeshFilter sourceFilter = solidRenderer == null
                ? null
                : solidRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || solidRenderer == null || sourceFilter.sharedMesh == null) return;

            Transform sourceTransform = solidRenderer.transform;
            blurMeshAuthoredRotation = sourceTransform.localRotation;

            blurRenderers = new MeshRenderer[blurCopyCount];
            blurTransforms = new Transform[blurCopyCount];
            for (int i = 0; i < blurCopyCount; i++)
            {
                GameObject copy = new GameObject($"Propeller Blur {i + 1}");
                copy.transform.SetParent(propeller, false);
                copy.transform.localPosition = sourceTransform.localPosition;
                copy.transform.localScale = sourceTransform.localScale;
                copy.transform.localRotation =
                    Quaternion.AngleAxis(360f * i / blurCopyCount,
                        localRotationAxis.normalized) * blurMeshAuthoredRotation;

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

        private void DarkenSolidPropeller()
        {
            if (solidRenderer == null) return;
            Material[] source = solidRenderer.sharedMaterials;
            Material[] darkened = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) continue;
                Material material = new Material(source[i])
                {
                    name = source[i].name + " (Dark Propeller)"
                };
                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    color.r *= propellerBrightness;
                    color.g *= propellerBrightness;
                    color.b *= propellerBrightness;
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.r *= propellerBrightness;
                    color.g *= propellerBrightness;
                    color.b *= propellerBrightness;
                    material.color = color;
                }
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
                darkened[i] = material;
            }
            solidRenderer.materials = darkened;
        }

        private void CreateBlurDisc()
        {
            if (solidRenderer == null) return;
            MeshFilter sourceFilter = solidRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

            Bounds bounds = sourceFilter.sharedMesh.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) *
                           propellerDiscRadiusScale;
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Propeller Visibility Disc";
            disc.transform.SetParent(propeller, false);
            disc.transform.localPosition = solidRenderer.transform.localPosition;
            disc.transform.localRotation = solidRenderer.transform.localRotation;
            disc.transform.localScale = new Vector3(
                radius * 2f, 0.006f, radius * 2f);
            Collider generatedCollider = disc.GetComponent<Collider>();
            if (generatedCollider != null) Destroy(generatedCollider);

            blurDiscRenderer = disc.GetComponent<MeshRenderer>();
            Material source = solidRenderer.sharedMaterial;
            if (source == null) return;
            Material material = new Material(source)
            {
                name = source.name + " (Propeller Disc)",
                renderQueue = (int)RenderQueue.Transparent
            };
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            Color discColor = new Color(0.035f, 0.045f, 0.055f, propellerDiscOpacity);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", discColor);
            else if (material.HasProperty("_Color")) material.color = discColor;
            blurDiscRenderer.sharedMaterial = material;
            blurDiscRenderer.shadowCastingMode = ShadowCastingMode.Off;
            blurDiscRenderer.receiveShadows = false;
            blurDiscRenderer.enabled = false;
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
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
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
            if (blurDiscRenderer != null) blurDiscRenderer.enabled = false;
            if (blurRenderers == null) return;
            foreach (MeshRenderer renderer in blurRenderers)
                if (renderer != null) renderer.enabled = false;
        }
    }
}
