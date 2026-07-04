using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SimplyFly.GroundOps
{
    public sealed class AirportSelectorUI : MonoBehaviour
    {
        [SerializeField] private AirportLocationManager locationManager;
        [SerializeField] private TMP_Dropdown airportDropdown;
        [SerializeField] private Button[] airportButtons;
        [SerializeField] private TMP_Text airportInfo;
        [SerializeField] private Button runwayButton;
        [SerializeField] private Button finalButton;
        [SerializeField] private CanvasGroup selectorGroup;
        [SerializeField] private Key toggleKey = Key.O;

        private bool visible;

        private void Start()
        {
            if (locationManager == null)
                locationManager = FindAnyObjectByType<AirportLocationManager>();
            PopulateDropdown();
            if (airportDropdown != null)
                airportDropdown.onValueChanged.AddListener(SelectAirport);
            if (airportButtons != null)
            {
                for (int i = 0; i < airportButtons.Length; i++)
                {
                    int index = i;
                    if (airportButtons[i] != null)
                        airportButtons[i].onClick.AddListener(
                            () => SelectAirport(index));
                }
            }
            if (runwayButton != null)
                runwayButton.onClick.AddListener(SpawnAtRunway);
            if (finalButton != null)
                finalButton.onClick.AddListener(SpawnOnFinal);
            if (locationManager != null)
                locationManager.AirportChanged += RefreshInfo;
            RefreshInfo();
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            if (locationManager != null)
                locationManager.AirportChanged -= RefreshInfo;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                visible = !visible;
                ApplyVisibility();
            }
        }

        private void PopulateDropdown()
        {
            if (airportDropdown == null || locationManager == null) return;
            airportDropdown.ClearOptions();
            List<string> names = new List<string>();
            AirportDefinition[] definitions = locationManager.Airports;
            if (definitions != null)
            {
                foreach (AirportDefinition airport in definitions)
                    names.Add(airport?.airportName ?? "Unnamed Airport");
            }
            airportDropdown.AddOptions(names);
            airportDropdown.SetValueWithoutNotify(
                locationManager.SelectedAirportIndex);
        }

        private void SelectAirport(int index)
        {
            locationManager?.SelectAirport(index, true);
            RefreshInfo();
        }

        private void SpawnAtRunway()
        {
            locationManager?.SpawnAtRunway();
        }

        private void SpawnOnFinal()
        {
            locationManager?.SpawnOnFinalApproach();
        }

        private void RefreshInfo()
        {
            if (airportInfo == null || locationManager == null) return;
            AirportDefinition airport = locationManager.CurrentAirport;
            airportInfo.text = airport == null
                ? "NO AIRPORT"
                : $"{airport.airportName}\n" +
                  $"RWY {airport.runwayHeading:000}°   " +
                  $"{airport.runwayLength:0} × {airport.runwayWidth:0} m\n" +
                  $"PATTERN {airport.patternAltitude:0} m   " +
                  $"APP {airport.recommendedApproachSpeed:0} kt";
        }

        private void ApplyVisibility()
        {
            if (selectorGroup == null) return;
            selectorGroup.alpha = visible ? 1f : 0f;
            selectorGroup.interactable = visible;
            selectorGroup.blocksRaycasts = visible;
        }
    }
}
