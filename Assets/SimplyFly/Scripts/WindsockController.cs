using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class WindsockController : MonoBehaviour
    {
        [SerializeField] private WindController wind;
        [SerializeField] private Transform sock;
        [Range(0.2f, 0.8f)] [SerializeField] private float calmLength = 0.35f;
        [Range(0.4f, 1f)] [SerializeField] private float calmWidth = 0.65f;
        [SerializeField] private float strongWindKnots = 18f;
        [SerializeField] private float responseSpeed = 4f;
        [SerializeField] private float calmDroopDegrees = 32f;

        private Quaternion visualRotation;
        private Vector3 fixedLocalPosition;
        private Vector3 fullWindScale;
        private Transform fabricPivot;
        private bool captured;

        private void Awake()
        {
            if (wind == null)
                wind = FindAnyObjectByType<WindController>();
            if (sock == null)
            {
                Transform found = transform.Find("Sock") ??
                                  transform.Find("Windsock");
                sock = found;
            }
            CaptureFabricState();
        }

        public void Configure(WindController windController, Transform sockTransform)
        {
            wind = windController;
            sock = sockTransform;
            CaptureFabricState();
        }

        private void CaptureFabricState()
        {
            if (sock == null) return;
            EnsureFabricPivot();
            fixedLocalPosition = sock.localPosition;
            fullWindScale = sock.localScale;
            visualRotation = fabricPivot.rotation;
            captured = true;
        }

        private void EnsureFabricPivot()
        {
            if (sock.parent != null &&
                sock.parent.name == "Windsock Fabric Pivot")
            {
                fabricPivot = sock.parent;
                return;
            }

            Transform originalParent = sock.parent;
            Vector3 attachmentPoint =
                sock.position - sock.right * (Mathf.Abs(sock.lossyScale.x) * 0.5f);
            GameObject pivotObject = new("Windsock Fabric Pivot");
            fabricPivot = pivotObject.transform;
            fabricPivot.SetParent(originalParent, true);
            fabricPivot.SetPositionAndRotation(attachmentPoint, sock.rotation);
            sock.SetParent(fabricPivot, true);
        }

        private void LateUpdate()
        {
            if (wind == null || sock == null) return;
            if (!captured) CaptureFabricState();
            Vector3 windVelocity = wind.CurrentWindVelocity;
            Vector3 horizontal = Vector3.ProjectOnPlane(windVelocity, Vector3.up);
            float strength = Mathf.Clamp01(
                wind.WindSpeedKnots / Mathf.Max(1f, strongWindKnots));
            float droop = Mathf.Lerp(calmDroopDegrees, 0f, strength);
            if (horizontal.sqrMagnitude > 0.01f)
            {
                // The generated sock mesh is long on local X. Rotate only the
                // fabric so the pole and windsock root remain completely fixed.
                Quaternion target =
                    Quaternion.LookRotation(horizontal.normalized, Vector3.up) *
                    Quaternion.Euler(droop, 90f, 0f);
                visualRotation = Quaternion.Slerp(
                    visualRotation,
                    target,
                    1f - Mathf.Exp(-responseSpeed * Time.deltaTime));
                fabricPivot.rotation = visualRotation;
            }

            sock.localPosition = fixedLocalPosition;
            Vector3 calmScale = new(
                fullWindScale.x * calmLength,
                fullWindScale.y * calmWidth,
                fullWindScale.z * calmWidth);
            sock.localScale = Vector3.Lerp(calmScale, fullWindScale, strength);
        }
    }
}
