using UnityEngine;

namespace SimplyFly.GroundOps
{
    public sealed class UIController : MonoBehaviour
    {
        [SerializeField] private EngineController engine;
        [SerializeField] private ThrottleController throttle;
        [SerializeField] private GroundMovementController movement;
        [SerializeField] private BrakeController brakes;
        [SerializeField] private SteeringController steering;
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private AircraftResetController resetController;
        [SerializeField] private Vector2 panelPosition = new Vector2(18f, 18f);
        [SerializeField] private int fontSize = 16;

        private GUIStyle style;
        private GUIStyle diagnosticStyle;

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = fontSize,
                    padding = new RectOffset(12, 12, 10, 10),
                    normal = { textColor = Color.white }
                };
            }

            string text =
                $"SIMPLY FLY - FLIGHT TEST\n" +
                $"Aircraft State {(flightPhysics == null ? "--" : flightPhysics.State.ToString())}\n" +
                $"Engine State   {(engine == null ? "--" : engine.State.ToString())}\n" +
                $"Engine RPM     {(engine == null ? 0f : engine.CurrentRPM):0}\n" +
                $"Throttle       {(throttle == null ? 0f : throttle.Throttle * 100f):0}%\n" +
                $"Drive Throttle {(movement == null ? 0f : movement.SmoothedThrottle * 100f):0}%\n" +
                $"Ground Thrust  {(movement == null ? 0f : movement.AppliedThrust):0} N\n" +
                $"Ground Speed   {(movement == null ? 0f : movement.GroundSpeedKnots):0.0} kt\n" +
                $"Airspeed       {(flightPhysics == null ? 0f : flightPhysics.AirspeedKnots):0.0} kt\n" +
                $"Lift           {(flightPhysics == null ? 0f : flightPhysics.LiftForce):0} N " +
                $"({(flightPhysics == null ? 0f : flightPhysics.LiftPercentOfWeight):0}%)\n" +
                $"Pitch          {(flightPhysics == null ? 0f : flightPhysics.PitchAngle):+0.0;-0.0;0.0} deg\n" +
                $"Angle of Attack {(flightPhysics == null ? 0f : flightPhysics.AngleOfAttack):+0.0;-0.0;0.0} deg\n" +
                $"Flight Path     {(flightPhysics == null ? 0f : flightPhysics.FlightPathAngle):+0.0;-0.0;0.0} deg\n" +
                $"Neutral AoA     {(flightPhysics == null ? 0f : flightPhysics.DynamicNeutralAoA):0.0} deg\n" +
                $"Elevator Input {(flightPhysics == null ? 0f : flightPhysics.ElevatorInput):+0.00;-0.00;0.00}\n" +
                $"Elevator Defl. {(flightPhysics == null ? 0f : flightPhysics.ElevatorDeflection):+0.00;-0.00;0.00}\n" +
                $"Pitch Moment   {(flightPhysics == null ? 0f : flightPhysics.PitchMoment):+0.00;-0.00;0.00}\n" +
                $"Tail Force     {(flightPhysics == null ? 0f : flightPhysics.TailForce):+0;-0;0} N\n" +
                $"Grounded       {(flightPhysics != null && flightPhysics.IsPhysicallyGrounded ? "Yes" : "No")}\n" +
                $"Vertical Speed {(flightPhysics == null ? 0f : flightPhysics.VerticalSpeed):+0.00;-0.00;0.00} m/s\n" +
                $"Drag           {(flightPhysics == null ? 0f : flightPhysics.DragForce):0} N\n" +
                $"Brakes         {(brakes != null && brakes.IsBraking ? $"{brakes.BrakeAmount * 100f:0}%" : "Released")}\n" +
                $"Steering       {(steering == null ? 0f : steering.SteeringAngle):+0.0;-0.0;0.0} deg\n\n" +
                "E Start | Shift/Ctrl Throttle | A/D Steer\n" +
                "S/Down Nose Up | W/Up Nose Down | Space Brake\n" +
                "RMB Orbit | R Reset Camera";

            GUI.Box(new Rect(panelPosition.x, panelPosition.y, 410f, 575f), text, style);
            if (GUI.Button(
                    new Rect(panelPosition.x, panelPosition.y + 585f, 200f, 42f),
                    "RESET AIRCRAFT (Backspace)"))
            {
                resetController?.ResetAircraft();
            }

            if (movement != null)
            {
                if (diagnosticStyle == null)
                {
                    diagnosticStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.UpperLeft,
                        fontSize = 13,
                        padding = new RectOffset(12, 12, 10, 10),
                        normal = { textColor = new Color(0.85f, 1f, 0.85f) }
                    };
                }

                GUI.Box(
                    new Rect(panelPosition.x + 425f, panelPosition.y, 475f, 535f),
                    movement.GetDiagnostics(),
                    diagnosticStyle);
            }
        }
    }
}
