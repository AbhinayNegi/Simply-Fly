using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>Heading data, wrap handling, and smoothing; contains no UI styling.</summary>
    [DisallowMultipleComponent]
    public sealed class HeadingRibbonController : MonoBehaviour
    {
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private HeadingRibbonHUD ribbon;
        [SerializeField] private NavigationManager navigation;
        [Min(0.01f)] [SerializeField] private float smoothingTime = 0.1f;
        [Min(0.01f)] [SerializeField] private float courseSmoothingTime = 0.14f;

        private float previousRawHeading;
        private float targetContinuousHeading;
        private float displayedContinuousHeading;
        private float smoothVelocity;
        private bool initialized;
        private float displayedContinuousCourse;
        private float courseVelocity;
        private bool courseInitialized;

        public void Bind(FlightPhysicsController source) => flightPhysics = source;

        private void Awake()
        {
            if (ribbon == null) ribbon = GetComponent<HeadingRibbonHUD>();
            if (ribbon == null) ribbon = gameObject.AddComponent<HeadingRibbonHUD>();
            if (flightPhysics == null) flightPhysics = FindAnyObjectByType<FlightPhysicsController>();
            if (navigation == null) navigation = FindAnyObjectByType<NavigationManager>();
            NavigationUI navigationUI = FindAnyObjectByType<NavigationUI>();
            navigationUI?.SetLegacyHeadingBugVisible(false);
        }

        private void Update()
        {
            float raw = flightPhysics == null ? 0f : Mathf.Repeat(flightPhysics.Heading, 360f);
            if (!initialized)
            {
                initialized = true;
                previousRawHeading = raw;
                targetContinuousHeading = raw;
                displayedContinuousHeading = raw;
            }
            else
            {
                targetContinuousHeading += Mathf.DeltaAngle(previousRawHeading, raw);
                previousRawHeading = raw;
                displayedContinuousHeading = Mathf.SmoothDamp(
                    displayedContinuousHeading, targetContinuousHeading,
                    ref smoothVelocity, smoothingTime,
                    Mathf.Infinity, Time.unscaledDeltaTime);
            }
            ribbon?.SetHeading(displayedContinuousHeading);
            UpdateNavigationGuidance();
        }

        private void UpdateNavigationGuidance()
        {
            bool active = navigation != null && navigation.IsActive &&
                navigation.CurrentWaypoint != null;
            if (!active)
            {
                courseInitialized = false;
                ribbon?.SetNavigationGuidance(false, 0f, string.Empty, 0f);
                return;
            }

            float desired = Mathf.Repeat(navigation.DesiredHeading, 360f);
            float targetCourse = displayedContinuousHeading +
                Mathf.DeltaAngle(Mathf.Repeat(displayedContinuousHeading, 360f), desired);
            if (!courseInitialized)
            {
                courseInitialized = true;
                displayedContinuousCourse = targetCourse;
            }
            else
            {
                displayedContinuousCourse = Mathf.SmoothDamp(
                    displayedContinuousCourse, targetCourse, ref courseVelocity,
                    courseSmoothingTime, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            const float metersPerNauticalMile = 1852f;
            ribbon?.SetNavigationGuidance(
                true,
                displayedContinuousCourse,
                navigation.CurrentWaypoint.name,
                Mathf.Max(0f, navigation.DistanceToWaypoint / metersPerNauticalMile));
        }
    }
}
