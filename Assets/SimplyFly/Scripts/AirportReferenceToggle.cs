using UnityEngine;
using UnityEngine.InputSystem;

namespace SimplyFly.GroundOps
{
    /// <summary>
    /// Optional development-only visibility controls for airport reference aids.
    /// The actual airport scenery is unaffected.
    /// </summary>
    public sealed class AirportReferenceToggle : MonoBehaviour
    {
        [SerializeField] private GameObject runwayCenterlineDebug;
        [SerializeField] private GameObject approachPathDebug;
        [SerializeField] private GameObject airportReferenceMarkers;
        [SerializeField] private bool showRunwayCenterlineDebug;
        [SerializeField] private bool showApproachPathDebug;
        [SerializeField] private bool showAirportReferenceMarkers = true;

        private void Start()
        {
            ApplyVisibility();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.numpad7Key.wasPressedThisFrame) ToggleRunwayCenterlineDebug();
            if (keyboard.numpad8Key.wasPressedThisFrame) ToggleApproachPathDebug();
            if (keyboard.numpad9Key.wasPressedThisFrame) ToggleAirportReferenceMarkers();
        }

        public void ToggleRunwayCenterlineDebug()
        {
            showRunwayCenterlineDebug = !showRunwayCenterlineDebug;
            ApplyVisibility();
        }

        public void ToggleApproachPathDebug()
        {
            showApproachPathDebug = !showApproachPathDebug;
            ApplyVisibility();
        }

        public void ToggleAirportReferenceMarkers()
        {
            showAirportReferenceMarkers = !showAirportReferenceMarkers;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (runwayCenterlineDebug != null)
                runwayCenterlineDebug.SetActive(showRunwayCenterlineDebug);
            if (approachPathDebug != null)
                approachPathDebug.SetActive(showApproachPathDebug);
            if (airportReferenceMarkers != null)
                airportReferenceMarkers.SetActive(showAirportReferenceMarkers);
        }
    }
}
