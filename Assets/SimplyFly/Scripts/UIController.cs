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
        [SerializeField] private Vector2 panelPosition = new Vector2(18f, 18f);
        [SerializeField] private int fontSize = 16;

        private GUIStyle style;

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
                $"SIMPLY FLY — GROUND OPS\n" +
                $"Engine State   {(engine == null ? "--" : engine.State.ToString())}\n" +
                $"Engine RPM     {(engine == null ? 0f : engine.CurrentRPM):0}\n" +
                $"Throttle       {(throttle == null ? 0f : throttle.Throttle * 100f):0}%\n" +
                $"Ground Speed   {(movement == null ? 0f : movement.GroundSpeedKnots):0.0} kt\n" +
                $"Brakes         {(brakes != null && brakes.IsBraking ? $"{brakes.BrakeAmount * 100f:0}%" : "Released")}\n" +
                $"Steering       {(steering == null ? 0f : steering.SteeringAngle):+0.0;-0.0;0.0}°\n\n" +
                "E Start  |  W/S Throttle  |  A/D Steer\nSpace Brake  |  RMB Orbit  |  R Reset Camera";

            GUI.Box(new Rect(panelPosition.x, panelPosition.y, 370f, 205f), text, style);
        }
    }
}
