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
        [SerializeField] private ApproachGuidanceController approachGuidance;
        [SerializeField] private LandingController landing;
        [SerializeField] private StallController stall;
        [SerializeField] private FlapController flaps;
        [SerializeField] private AssistedTrimController trim;
        [SerializeField] private AircraftAudioController aircraftAudio;
        [SerializeField] private RudderController rudder;
        [SerializeField] private AirportLocationManager locationManager;
        [SerializeField] private AircraftResetController resetController;
        [SerializeField] private Vector2 panelPosition = new Vector2(18f, 18f);
        [SerializeField] private int fontSize = 16;
        [Header("Debug visibility")]
        [SerializeField] private bool showMainDebug = true;
        [SerializeField] private bool showAirborneDebug = true;
        [SerializeField] private bool showApproachDebug = true;
        [SerializeField] private bool showLandingDebug = true;
        [SerializeField] private bool showStallDebug;
        [SerializeField] private bool showGroundDiagnostics = true;
        [SerializeField] private bool showAudioDebug;
        [SerializeField] private bool debugVisible;

        private GUIStyle style;
        private GUIStyle diagnosticStyle;
        private GUIStyle warningStyle;

        private void OnGUI()
        {
            if (!debugVisible) return;

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

            float panelTop = panelPosition.y + 44f;
            if (GUI.Button(
                    new Rect(panelPosition.x, panelPosition.y, 130f, 34f),
                    showMainDebug ? "Hide Main" : "Show Main"))
                showMainDebug = !showMainDebug;
            if (GUI.Button(
                    new Rect(panelPosition.x + 138f, panelPosition.y, 150f, 34f),
                    showAirborneDebug ? "Hide Air Debug" : "Show Air Debug"))
                showAirborneDebug = !showAirborneDebug;
            if (GUI.Button(
                    new Rect(panelPosition.x + 296f, panelPosition.y, 165f, 34f),
                    showGroundDiagnostics ? "Hide Ground Debug" : "Show Ground Debug"))
                showGroundDiagnostics = !showGroundDiagnostics;
            if (GUI.Button(
                    new Rect(panelPosition.x + 469f, panelPosition.y, 165f, 34f),
                    showApproachDebug ? "Hide Approach" : "Show Approach"))
                showApproachDebug = !showApproachDebug;
            if (GUI.Button(
                    new Rect(panelPosition.x + 642f, panelPosition.y, 155f, 34f),
                    showLandingDebug ? "Hide Landing" : "Show Landing"))
                showLandingDebug = !showLandingDebug;
            if (GUI.Button(
                    new Rect(panelPosition.x + 805f, panelPosition.y, 145f, 34f),
                    showStallDebug ? "Hide Stall" : "Show Stall"))
                showStallDebug = !showStallDebug;
            if (GUI.Button(
                    new Rect(panelPosition.x + 958f, panelPosition.y, 145f, 34f),
                    showAudioDebug ? "Hide Audio" : "Show Audio"))
                showAudioDebug = !showAudioDebug;

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
                $"Lift Multiplier {(flightPhysics == null ? 1f : flightPhysics.CurrentLiftMultiplier):0.00}\n" +
                $"Drag Multiplier {(flightPhysics == null ? 1f : flightPhysics.CurrentDragMultiplier):0.00}\n" +
                $"Pitch          {(flightPhysics == null ? 0f : flightPhysics.PitchAngle):+0.0;-0.0;0.0} deg\n" +
                $"Angle of Attack {(flightPhysics == null ? 0f : flightPhysics.AngleOfAttack):+0.0;-0.0;0.0} deg\n" +
                $"Flight Path     {(flightPhysics == null ? 0f : flightPhysics.FlightPathAngle):+0.0;-0.0;0.0} deg\n" +
                $"Neutral AoA     {(flightPhysics == null ? 0f : flightPhysics.DynamicNeutralAoA):0.0} deg\n" +
                $"Elevator Input {(flightPhysics == null ? 0f : flightPhysics.ElevatorInput):+0.00;-0.00;0.00}\n" +
                $"Elevator Defl. {(flightPhysics == null ? 0f : flightPhysics.ElevatorDeflection):+0.00;-0.00;0.00}\n" +
                $"Pitch Moment   {(flightPhysics == null ? 0f : flightPhysics.PitchMoment):+0.00;-0.00;0.00}\n" +
                $"Tail Force     {(flightPhysics == null ? 0f : flightPhysics.TailForce):+0;-0;0} N\n" +
                $"Bank           {(flightPhysics == null ? 0f : flightPhysics.BankAngle):+0.0;-0.0;0.0} deg\n" +
                $"Aileron Input  {(flightPhysics == null ? 0f : flightPhysics.AileronInput):+0.00;-0.00;0.00}\n" +
                $"Aileron Defl.  {(flightPhysics == null ? 0f : flightPhysics.AileronDeflection):+0.00;-0.00;0.00}\n" +
                $"Roll Rate      {(flightPhysics == null ? 0f : flightPhysics.RollRate * Mathf.Rad2Deg):+0.0;-0.0;0.0} deg/s\n" +
                $"Roll Moment    {(flightPhysics == null ? 0f : flightPhysics.RollMoment):+0.00;-0.00;0.00}\n" +
                $"Yaw Assist     {(flightPhysics == null ? 0f : flightPhysics.CoordinatedYawMoment):+0.00;-0.00;0.00}\n" +
                $"Rudder Input   {(rudder == null ? 0f : rudder.RudderInput):+0.00;-0.00;0.00}\n" +
                $"Slip / Skid    {(rudder == null ? 0f : rudder.SlipAmount):+0.00;-0.00;0.00}\n" +
                $"Flaps Selected {(flaps == null ? 0f : flaps.SelectedAngle):0} deg\n" +
                $"Flaps Actual   {(flaps == null ? 0f : flaps.ActualAngle):0.0} deg\n" +
                $"Flap Lift      {(flaps == null ? 0f : flaps.LiftBonus):0.00}\n" +
                $"Flap Drag      {(flaps == null ? 0f : flaps.DragBonus):0.00}\n" +
                $"Stall Reduction {(flaps == null ? 0f : flaps.StallSpeedReduction):0.0} kt\n" +
                $"Flap Overspeed {(flaps != null && flaps.SpeedWarning ? "YES" : "No")}\n" +
                $"Grounded       {(flightPhysics != null && flightPhysics.IsPhysicallyGrounded ? "Yes" : "No")}\n" +
                $"Vertical Speed {(flightPhysics == null ? 0f : flightPhysics.VerticalSpeed):+0.00;-0.00;0.00} m/s\n" +
                $"Drag           {(flightPhysics == null ? 0f : flightPhysics.DragForce):0} N\n" +
                $"Brakes         {(brakes != null && brakes.IsBraking ? $"{brakes.BrakeAmount * 100f:0}%" : "Released")}\n" +
                $"Steering       {(steering == null ? 0f : steering.SteeringAngle):+0.0;-0.0;0.0} deg\n\n" +
                "E Start | Shift/Ctrl Throttle | A/D Steer/Roll\n" +
                "S/Down Nose Up | W/Up Nose Down | Space Brake\n" +
                "F Extend Flaps | V Retract Flaps\n" +
                "T Capture Trim | Shift+T Reset Trim\n" +
                "RMB Orbit | Z/X Quick Look | C Recenter\n" +
                "J/L Rudder | I Side View Aid | K Force Vectors\n" +
                "B Speed Sensation HUD\n" +
                "G Flight School | O Airport Selector\n" +
                "F5 Test UI | F6/F7 Scenario | F8 Record | F9 Run\n" +
                "F1 Runway | F2 Airborne | F3 Final Approach";

            if (showMainDebug)
                GUI.Box(new Rect(panelPosition.x, panelTop, 410f, 825f), text, style);

            string airborneText =
                "AIRBORNE ROLL / TURN DEBUG\n\n" +
                "FLIGHT\n" +
                $"State              {(flightPhysics == null ? "--" : flightPhysics.DebugFlightState)}\n" +
                $"Airspeed           {(flightPhysics == null ? 0f : flightPhysics.AirspeedKnots):0.0} kt\n" +
                $"Altitude           {(flightPhysics == null ? 0f : flightPhysics.Altitude):0.0} m\n" +
                $"Vertical speed     {(flightPhysics == null ? 0f : flightPhysics.VerticalSpeed):+0.00;-0.00;0.00} m/s\n\n" +
                "ATTITUDE\n" +
                $"Pitch              {(flightPhysics == null ? 0f : flightPhysics.PitchAngle):+0.0;-0.0;0.0} deg\n" +
                $"Bank               {(flightPhysics == null ? 0f : flightPhysics.BankAngle):+0.0;-0.0;0.0} deg\n" +
                $"Pitch rate         {(flightPhysics == null ? 0f : flightPhysics.PitchRateDegrees):+0.0;-0.0;0.0} deg/s\n" +
                $"Pitch moment       {(flightPhysics == null ? 0f : flightPhysics.PitchMoment):+0.00;-0.00;0.00}\n" +
                $"Pitch stability    {(flightPhysics == null ? 0f : flightPhysics.PitchStabilityTorque):+0;-0;0} Nm\n" +
                $"Heading            {(flightPhysics == null ? 0f : flightPhysics.Heading):000.0} deg\n" +
                $"Turn rate          {(flightPhysics == null ? 0f : flightPhysics.TurnRate):+0.00;-0.00;0.00} deg/s\n" +
                $"Turn direction     {(flightPhysics == null ? "None" : flightPhysics.TurnDirection)}\n\n" +
                "ROLL CONTROL\n" +
                $"Roll input         {(flightPhysics == null ? 0f : flightPhysics.AileronInput):+0.00;-0.00;0.00}\n" +
                $"Aileron deflection {(flightPhysics == null ? 0f : flightPhysics.AileronDeflection):+0.00;-0.00;0.00}\n" +
                $"Roll rate          {(flightPhysics == null ? 0f : flightPhysics.RollRate * Mathf.Rad2Deg):+0.0;-0.0;0.0} deg/s\n" +
                $"Roll moment        {(flightPhysics == null ? 0f : flightPhysics.RollMoment):+0.00;-0.00;0.00}\n" +
                $"Maximum bank       {(flightPhysics == null ? 0f : flightPhysics.MaximumBankAngle):0.0} deg\n\n" +
                "TURN / YAW\n" +
                $"Yaw rate           {(flightPhysics == null ? 0f : flightPhysics.YawRate * Mathf.Rad2Deg):+0.0;-0.0;0.0} deg/s\n" +
                $"Yaw assist moment  {(flightPhysics == null ? 0f : flightPhysics.YawAssistMoment):+0.00;-0.00;0.00}\n" +
                $"Yaw damping moment {(flightPhysics == null ? 0f : flightPhysics.YawDampingMoment):+0.00;-0.00;0.00}\n" +
                $"Yaw response       {(flightPhysics == null ? 0f : flightPhysics.CoordinatedTurnStrength):0.00}\n" +
                $"Bank turn setting  {(flightPhysics == null ? 0f : flightPhysics.BankTurnStrengthSetting):0.00}\n" +
                $"Max assisted yaw   {(flightPhysics == null ? 0f : flightPhysics.MaxAssistedYawRate):0.0} deg/s\n" +
                $"Slip damping       {(flightPhysics == null ? 0f : flightPhysics.LateralSlipDamping):0.00}\n" +
                $"Slip damping force {(flightPhysics == null ? 0f : flightPhysics.LateralSlipDampingForce):+0;-0;0} N\n\n" +
                "AIRFLOW STABILITY\n" +
                $"Raw / clamped AoA  {(flightPhysics == null ? 0f : flightPhysics.RawAngleOfAttack):+0.0;-0.0;0.0} / " +
                $"{(flightPhysics == null ? 0f : flightPhysics.AngleOfAttack):+0.0;-0.0;0.0} deg\n" +
                $"Airflow direction  {(flightPhysics == null ? Vector3.zero : flightPhysics.EffectiveAirflowDirection)}\n" +
                $"Forward airflow    {(flightPhysics == null ? 0f : flightPhysics.ForwardAirflowSpeed):+0.00;-0.00;0.00} m/s\n" +
                $"Lateral airflow    {(flightPhysics == null ? 0f : flightPhysics.LateralAirflowSpeed):+0.00;-0.00;0.00} m/s\n" +
                $"Side-slip drag     {(flightPhysics == null ? 0f : flightPhysics.SideSlipDragForce):+0;-0;0} N\n" +
                $"Weather-vane torque {(flightPhysics == null ? 0f : flightPhysics.WeatherVaneTorque):+0.00;-0.00;0.00}\n" +
                $"Elevator authority {(flightPhysics == null ? 0f : flightPhysics.ElevatorAuthorityMultiplier):0.00}\n" +
                $"Lift point         {(flightPhysics == null ? Vector3.zero : flightPhysics.LiftApplicationPoint)}\n\n" +
                "MANUAL RUDDER\n" +
                $"Rudder input       {(rudder == null ? 0f : rudder.RudderInput):+0.00;-0.00;0.00}\n" +
                $"Authority multiplier {(rudder == null ? 0f : rudder.AuthorityMultiplier):0.00}\n" +
                $"Command yaw torque {(rudder == null ? 0f : rudder.CommandYawMoment):+0.00;-0.00;0.00}\n" +
                $"Yaw rate           {(rudder == null ? 0f : rudder.YawRateDegrees):+0.0;-0.0;0.0} deg/s\n" +
                $"Yaw damping torque {(rudder == null ? 0f : rudder.YawDampingMoment):+0.00;-0.00;0.00}\n" +
                $"Yaw limit torque   {(rudder == null ? 0f : rudder.YawLimitMoment):+0.00;-0.00;0.00}\n" +
                $"Net yaw torque     {(rudder == null ? 0f : rudder.AppliedYawMoment):+0.00;-0.00;0.00}\n" +
                $"Slip amount        {(rudder == null ? 0f : rudder.SlipAmount):+0.00;-0.00;0.00}\n" +
                $"Slip direction     {(rudder == null ? "--" : rudder.SlipDirection)}\n" +
                $"Slip ball          {(rudder == null ? 0f : rudder.SlipBallPosition):+0.00;-0.00;0.00}\n\n" +
                "LIFT FORCES\n" +
                $"Total lift         {(flightPhysics == null ? 0f : flightPhysics.LiftForce):0} N\n" +
                $"Lift / weight      {(flightPhysics == null ? 0f : flightPhysics.LiftPercentOfWeight):0.0}%\n" +
                $"Vertical lift      {(flightPhysics == null ? 0f : flightPhysics.VerticalLiftForce):0} N\n" +
                $"Vertical / weight  {(flightPhysics == null ? 0f : flightPhysics.VerticalLiftPercentOfWeight):0.0}%\n" +
                $"Side lift          {(flightPhysics == null ? 0f : flightPhysics.SideLiftForce):+0;-0;0} N\n" +
                $"Bank turn force    {(flightPhysics == null ? 0f : flightPhysics.BankTurnForcePercent):0.0}%\n\n" +
                "VELOCITY\n" +
                $"Lateral velocity   {(flightPhysics == null ? 0f : flightPhysics.LateralVelocity):+0.00;-0.00;0.00} m/s\n" +
                $"Forward velocity   {(flightPhysics == null ? 0f : flightPhysics.ForwardVelocity):+0.00;-0.00;0.00} m/s\n" +
                $"Ground speed       {(flightPhysics == null ? 0f : flightPhysics.HorizontalGroundSpeed * 1.943844f):0.0} kt";

            if (showAirborneDebug)
                GUI.Box(
                    new Rect(panelPosition.x + 425f, panelTop, 455f, 1010f),
                    airborneText,
                    style);

            string approachText =
                "APPROACH DEBUG\n\n" +
                $"Airport           {(locationManager?.CurrentAirport?.airportName ?? "--")}\n" +
                $"Target available  {(approachGuidance != null && approachGuidance.HasRunwayTarget ? "Yes" : "No")}\n" +
                $"Threshold distance {(approachGuidance == null ? 0f : approachGuidance.DistanceToThreshold):0} m\n" +
                $"Runway altitude    {(approachGuidance == null ? 0f : approachGuidance.AltitudeAboveRunway):+0.0;-0.0;0.0} m\n" +
                $"Vertical speed     {(approachGuidance == null ? 0f : approachGuidance.VerticalSpeed):+0.00;-0.00;0.00} m/s\n" +
                $"Airspeed           {(approachGuidance == null ? 0f : approachGuidance.AirspeedKnots):0.0} kt\n" +
                $"Ground speed       {(approachGuidance == null ? 0f : approachGuidance.GroundSpeedKnots):0.0} kt\n" +
                $"Heading            {(approachGuidance == null ? 0f : approachGuidance.Heading):000.0} deg\n" +
                $"Runway heading     {(approachGuidance == null ? 0f : approachGuidance.RunwayHeading):000.0} deg\n" +
                $"Recommended speed  {(locationManager?.CurrentAirport == null ? 0f : locationManager.CurrentAirport.recommendedApproachSpeed):0.0} kt\n" +
                $"Alignment error    {(approachGuidance == null ? 0f : approachGuidance.AlignmentError):+0.0;-0.0;0.0} deg\n" +
                $"Lateral offset     {(approachGuidance == null ? 0f : approachGuidance.LateralOffset):+0;-0;0} m\n" +
                $"Glide slope        {(approachGuidance == null ? 0f : approachGuidance.GlideSlopeAngle):+0.0;-0.0;0.0} deg\n\n" +
                "F1  Runway threshold\n" +
                "F2  Airborne test\n" +
                "F3  Final approach";

            if (showApproachDebug)
            {
                float approachPanelX = Mathf.Max(
                    panelPosition.x,
                    Screen.width - 420f);
                GUI.Box(
                    new Rect(approachPanelX, panelTop, 400f, 390f),
                    approachText,
                    style);
            }

            string landingText =
                "LANDING DEBUG\n\n" +
                $"Landing state      {(landing == null ? "--" : landing.Phase.ToString())}\n" +
                $"Flare active       {(landing != null && landing.FlareActive ? "Yes" : "No")}\n" +
                $"Height above runway {(landing == null ? 0f : landing.HeightAboveRunway):+0.0;-0.0;0.0} m\n" +
                $"Left main touching {(landing != null && landing.LeftMainTouching ? "Yes" : "No")}\n" +
                $"Right main touching {(landing != null && landing.RightMainTouching ? "Yes" : "No")}\n" +
                $"Main wheels        {(landing != null && landing.MainWheelsTouching ? "Yes" : "No")}\n" +
                $"Nose wheel         {(landing != null && landing.NoseWheelTouching ? "Yes" : "No")}\n" +
                $"All wheels         {(landing != null && landing.AllWheelsTouching ? "Yes" : "No")}\n" +
                $"Touchdown V/S      {(landing == null ? 0f : landing.TouchdownVerticalSpeed):+0.00;-0.00;0.00} m/s\n" +
                $"Landing quality    {(landing == null ? "--" : landing.Quality.ToString())}\n" +
                $"Touchdown airspeed {(landing == null ? 0f : landing.TouchdownAirspeed * 1.943844f):0.0} kt\n" +
                $"Touchdown pitch    {(landing == null ? 0f : landing.TouchdownPitch):+0.0;-0.0;0.0} deg\n" +
                $"Touchdown bank     {(landing == null ? 0f : landing.TouchdownBank):+0.0;-0.0;0.0} deg\n" +
                $"Centerline offset  {(landing == null ? 0f : landing.TouchdownCenterlineOffset):+0;-0;0} m\n" +
                $"Main wheels first  {(landing != null && landing.MainWheelsTouchedFirst ? "Yes" : "No")}\n" +
                $"Nose-first warning {(landing != null && landing.NoseWheelFirstWarning ? "YES" : "No")}\n" +
                $"Bounce velocity    {(landing == null ? 0f : landing.BounceVelocity):0.00} m/s\n" +
                $"Landing roll speed {(landing == null ? 0f : landing.LandingRollSpeed * 1.943844f):0.0} kt";

            if (showLandingDebug)
            {
                float landingPanelX = Mathf.Max(
                    panelPosition.x,
                    Screen.width - 420f);
                GUI.Box(
                    new Rect(landingPanelX, panelTop + 405f, 400f, 465f),
                    landingText,
                    style);
            }

            string stallText =
                "STALL DEBUG\n\n" +
                $"Stall state       {(stall == null ? "--" : stall.State.ToString())}\n" +
                $"Stall warning     {(stall != null && stall.StallWarning ? "YES" : "No")}\n" +
                $"Airspeed          {(flightPhysics == null ? 0f : flightPhysics.AirspeedKnots):0.0} kt\n" +
                $"Stall speed       {(stall == null ? 0f : stall.EffectiveStallSpeed):0.0} kt\n" +
                $"Clean stall speed {(stall == null ? 0f : stall.CleanStallSpeed):0.0} kt\n" +
                $"Angle of attack   {(flightPhysics == null ? 0f : flightPhysics.AngleOfAttack):+0.0;-0.0;0.0} deg\n" +
                $"Stall AoA         {(stall == null ? 0f : stall.EffectiveStallAoA):0.0} deg\n" +
                $"Lift multiplier   {(stall == null ? 1f : stall.LiftMultiplier):0.00}\n" +
                $"Control authority {(stall == null ? 1f : stall.ControlAuthorityMultiplier):0.00}\n" +
                $"Nose-drop moment  {(stall == null ? 0f : stall.NoseDropMoment):+0.00;-0.00;0.00}\n" +
                $"Recovery progress {(stall == null ? 1f : stall.RecoveryProgress) * 100f:0}%\n" +
                $"Bank risk factor  {(stall == null ? 1f : stall.BankRiskFactor):0.00}\n\n" +
                "TRIM DEBUG\n" +
                $"Trim active        {(trim != null && trim.TrimActive ? "Yes" : "No")}\n" +
                $"Current offset     {(trim == null ? 0f : trim.CurrentTrimOffset):+0.000;-0.000;0.000}\n" +
                $"Target offset      {(trim == null ? 0f : trim.TargetTrimOffset):+0.000;-0.000;0.000}\n" +
                $"Elevator deflection {(flightPhysics == null ? 0f : flightPhysics.ElevatorDeflection):+0.00;-0.00;0.00}\n" +
                $"Sustained pressure {(trim == null ? 0f : trim.SustainedElevatorPressure):+0.00;-0.00;0.00}\n" +
                $"Recommendation     {(trim == null ? "--" : trim.Recommendation)}\n" +
                $"Rejection          {(trim == null || string.IsNullOrEmpty(trim.RejectionReason) ? "--" : trim.RejectionReason)}\n" +
                $"Captured airspeed  {(trim == null ? 0f : trim.CapturedAirspeed):0.0} kt\n" +
                $"Captured V/S       {(trim == null ? 0f : trim.CapturedVerticalSpeed):+0.00;-0.00;0.00} m/s\n" +
                $"Captured flaps     {(trim == null ? 0f : trim.CapturedFlapSetting):0} deg";

            if (showStallDebug)
            {
                float stallPanelX = Mathf.Max(
                    panelPosition.x,
                    Screen.width - 800f);
                GUI.Box(
                    new Rect(stallPanelX, panelTop, 430f, 585f),
                    stallText,
                    style);
            }

            if (stall != null && stall.StallWarning)
            {
                if (warningStyle == null)
                {
                    warningStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 24,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(1f, 0.25f, 0.18f) }
                    };
                }
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 120f, panelPosition.y, 240f, 42f),
                    "STALL WARNING",
                    warningStyle);
            }

            if (GUI.Button(
                    new Rect(panelPosition.x, panelTop + 835f, 200f, 42f),
                    "RESET AIRCRAFT (Backspace)"))
            {
                resetController?.ResetAircraft();
            }

            if (movement != null && showGroundDiagnostics)
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
                    new Rect(panelPosition.x + 1285f, panelTop, 475f, 535f),
                    movement.GetDiagnostics(),
                    diagnosticStyle);
            }

            if (aircraftAudio != null && showAudioDebug)
            {
                if (diagnosticStyle == null)
                {
                    diagnosticStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.UpperLeft,
                        fontSize = 13,
                        padding = new RectOffset(12, 12, 10, 10),
                        normal = { textColor = new Color(0.85f, 0.95f, 1f) }
                    };
                }
                GUI.Box(
                    new Rect(panelPosition.x + 865f, panelTop, 390f, 245f),
                    aircraftAudio.GetDiagnostics(),
                    diagnosticStyle);
            }
        }

        public void ToggleDebugVisibility()
        {
            debugVisible = !debugVisible;
        }

        public void SetDebugVisibility(bool visible)
        {
            debugVisible = visible;
        }
    }
}
