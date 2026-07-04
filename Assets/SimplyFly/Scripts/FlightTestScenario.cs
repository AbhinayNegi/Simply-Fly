using System;

namespace SimplyFly.GroundOps
{
    public enum FlightTestScenarioType
    {
        IdleCreep,
        LowThrottleTaxi,
        CleanTakeoff,
        Flaps10Takeoff,
        Flaps30Stall,
        BankedTurn,
        ApproachStability,
        Landing,
        BrakeStopping
    }

    [Serializable]
    public sealed class FlightTestScenario
    {
        public string name;
        public string description;
        public FlightTestScenarioType type;
        public float maximumDuration;
    }
}
