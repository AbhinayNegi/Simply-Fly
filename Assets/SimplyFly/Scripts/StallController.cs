using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class StallController : MonoBehaviour
    {
        public enum StallState
        {
            Normal,
            Warning,
            Stalled,
            Recovering
        }

        [Header("References")]
        [SerializeField] private FlightPhysicsController flightPhysics;
        [SerializeField] private AircraftInput input;
        [SerializeField] private Vector3 localPitchAxis = Vector3.left;

        [Header("Detection")]
        [Tooltip("Knots.")]
        [Min(1f)] [SerializeField] private float stallSpeed = 42f;
        [Tooltip("Degrees.")]
        [Min(1f)] [SerializeField] private float stallAoA = 14f;
        [Tooltip("Knots.")]
        [Min(1f)] [SerializeField] private float stallWarningSpeed = 49f;
        [Tooltip("Degrees.")]
        [Min(1f)] [SerializeField] private float stallWarningAoA = 8f;
        [Tooltip("Knots.")]
        [Min(1f)] [SerializeField] private float stallRecoverySpeed = 52f;
        [Tooltip("Degrees.")]
        [Min(0f)] [SerializeField] private float stallRecoveryAoA = 6f;
        [Min(0f)] [SerializeField] private float stallExitSpeedMargin = 3f;
        [Min(0f)] [SerializeField] private float stallExitAoAMargin = 1.5f;
        [Min(0.01f)] [SerializeField] private float stallRecoverySmoothing = 1.2f;
        [Range(0f, 1f)] [SerializeField] private float bankStallRiskMultiplier = 0.22f;

        [Header("Lift and control")]
        [Range(0.1f, 1f)] [SerializeField] private float stalledLiftMultiplier = 0.48f;
        [Min(0.01f)] [SerializeField] private float stallLiftLossRate = 1.4f;
        [Min(0.01f)] [SerializeField] private float stallRecoveryRate = 0.75f;
        [Range(0.1f, 1f)] [SerializeField] private float lowSpeedControlReduction = 0.42f;

        [Header("Nose drop")]
        [Min(0f)] [SerializeField] private float stallNoseDropStrength = 0.18f;
        [Min(0f)] [SerializeField] private float maxStallNoseDropMoment = 1.2f;
        [Min(0f)] [SerializeField] private float stallPitchDamping = 1.8f;

        [Header("Warning")]
        [SerializeField] private bool stallWarningEnabled = true;
        [Min(0f)] [SerializeField] private float buffetStrength = 0.06f;
        [SerializeField] private bool warningToneEnabled;
        [SerializeField] private AudioClip warningTone;

        public StallState State { get; private set; } = StallState.Normal;
        public bool StallWarning =>
            stallWarningEnabled &&
            (State == StallState.Warning ||
             State == StallState.Stalled ||
             State == StallState.Recovering);
        public float LiftMultiplier { get; private set; } = 1f;
        public float ControlAuthorityMultiplier { get; private set; } = 1f;
        public float NoseDropMoment { get; private set; }
        public float RecoveryProgress { get; private set; } = 1f;
        public float BankRiskFactor { get; private set; } = 1f;
        public float EffectiveStallSpeed { get; private set; }
        public float EffectiveStallAoA { get; private set; }

        private Rigidbody body;
        private AudioSource warningSource;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (flightPhysics == null)
                flightPhysics = GetComponent<FlightPhysicsController>();
            if (input == null) input = GetComponent<AircraftInput>();
            ConfigureWarningAudio();
            EffectiveStallSpeed = stallSpeed;
            EffectiveStallAoA = stallAoA;
        }

        private void FixedUpdate()
        {
            if (flightPhysics == null ||
                flightPhysics.State != FlightPhysicsController.AircraftState.Airborne)
            {
                State = StallState.Normal;
                RecoveryProgress = 1f;
                UpdateMultipliers(1f, 1f);
                NoseDropMoment = 0f;
                UpdateWarningAudio();
                return;
            }

            float bankRatio = Mathf.Clamp01(Mathf.Abs(flightPhysics.BankAngle) / 70f);
            BankRiskFactor = 1f + bankRatio * bankStallRiskMultiplier;
            EffectiveStallSpeed = stallSpeed * BankRiskFactor;
            EffectiveStallAoA = stallAoA / BankRiskFactor;
            float effectiveWarningSpeed = stallWarningSpeed * BankRiskFactor;
            float effectiveWarningAoA = stallWarningAoA / BankRiskFactor;

            float speed = flightPhysics.AirspeedKnots;
            float aoa = Mathf.Max(0f, flightPhysics.AngleOfAttack);
            bool warningRisk =
                speed <= effectiveWarningSpeed &&
                aoa >= effectiveWarningAoA;
            bool stallRisk =
                (speed <= EffectiveStallSpeed && aoa >= effectiveWarningAoA) ||
                (speed <= effectiveWarningSpeed && aoa >= EffectiveStallAoA);
            bool recoveryCondition =
                speed >= stallRecoverySpeed * BankRiskFactor ||
                aoa <= stallRecoveryAoA / BankRiskFactor;
            bool safelyClear =
                speed >= effectiveWarningSpeed + stallExitSpeedMargin ||
                aoa <= effectiveWarningAoA - stallExitAoAMargin;

            UpdateState(warningRisk, stallRisk, recoveryCondition, safelyClear);
            UpdateStallEffects(speed, aoa, effectiveWarningSpeed, effectiveWarningAoA);
            ApplyNoseDropAndBuffet();
            UpdateWarningAudio();
        }

        private void UpdateState(
            bool warningRisk,
            bool stallRisk,
            bool recoveryCondition,
            bool safelyClear)
        {
            switch (State)
            {
                case StallState.Normal:
                    if (stallRisk)
                    {
                        State = StallState.Stalled;
                        RecoveryProgress = 0f;
                    }
                    else if (warningRisk)
                    {
                        State = StallState.Warning;
                    }
                    break;

                case StallState.Warning:
                    if (stallRisk)
                    {
                        State = StallState.Stalled;
                        RecoveryProgress = 0f;
                    }
                    else if (safelyClear)
                    {
                        State = StallState.Normal;
                    }
                    break;

                case StallState.Stalled:
                    if (recoveryCondition)
                        State = StallState.Recovering;
                    break;

                case StallState.Recovering:
                    if (stallRisk)
                    {
                        State = StallState.Stalled;
                        RecoveryProgress = 0f;
                    }
                    else if (RecoveryProgress >= 0.99f && safelyClear)
                    {
                        State = StallState.Normal;
                    }
                    break;
            }
        }

        private void UpdateStallEffects(
            float speed,
            float aoa,
            float effectiveWarningSpeed,
            float effectiveWarningAoA)
        {
            float speedRisk = 1f - Mathf.InverseLerp(
                EffectiveStallSpeed,
                effectiveWarningSpeed,
                speed);
            float aoaRisk = Mathf.InverseLerp(
                effectiveWarningAoA,
                EffectiveStallAoA,
                aoa);
            float risk = Mathf.Clamp01(Mathf.Max(speedRisk, aoaRisk));

            float targetLift = 1f;
            float targetControl = 1f;
            if (State == StallState.Warning)
            {
                targetLift = Mathf.Lerp(1f, 0.88f, risk);
                targetControl = Mathf.Lerp(1f, 0.82f, risk);
            }
            else if (State == StallState.Stalled)
            {
                targetLift = stalledLiftMultiplier;
                targetControl = lowSpeedControlReduction;
            }
            else if (State == StallState.Recovering)
            {
                RecoveryProgress = Mathf.MoveTowards(
                    RecoveryProgress,
                    1f,
                    stallRecoverySmoothing * Time.fixedDeltaTime);
                targetLift = Mathf.Lerp(
                    stalledLiftMultiplier,
                    1f,
                    RecoveryProgress);
                targetControl = Mathf.Lerp(
                    lowSpeedControlReduction,
                    1f,
                    RecoveryProgress);
            }
            else
            {
                RecoveryProgress = 1f;
            }

            UpdateMultipliers(targetLift, targetControl);
        }

        private void UpdateMultipliers(float targetLift, float targetControl)
        {
            float rate = targetLift < LiftMultiplier
                ? stallLiftLossRate
                : stallRecoveryRate;
            LiftMultiplier = Mathf.MoveTowards(
                LiftMultiplier,
                targetLift,
                rate * Time.fixedDeltaTime);
            ControlAuthorityMultiplier = Mathf.MoveTowards(
                ControlAuthorityMultiplier,
                targetControl,
                rate * Time.fixedDeltaTime);
        }

        private void ApplyNoseDropAndBuffet()
        {
            bool stalled =
                State == StallState.Stalled || State == StallState.Recovering;
            Vector3 pitchAxis =
                transform.TransformDirection(localPitchAxis.normalized);
            if (stalled)
            {
                float pitchRate = Vector3.Dot(body.angularVelocity, pitchAxis);
                float elevatorRelief = input == null
                    ? 1f
                    : Mathf.Lerp(1f, 0.65f, Mathf.Max(0f, -input.ElevatorInput));
                float drop =
                    -stallNoseDropStrength *
                    Mathf.Max(0f, flightPhysics.AngleOfAttack - stallRecoveryAoA) *
                    elevatorRelief;
                NoseDropMoment = Mathf.Clamp(
                    drop - pitchRate * stallPitchDamping,
                    -maxStallNoseDropMoment,
                    maxStallNoseDropMoment);
                body.AddTorque(
                    pitchAxis * NoseDropMoment,
                    ForceMode.Acceleration);
            }
            else
            {
                NoseDropMoment = 0f;
            }

            if (buffetStrength <= 0f ||
                (State != StallState.Warning && State != StallState.Stalled))
                return;

            float noiseA = Mathf.PerlinNoise(Time.time * 13f, 0.17f) * 2f - 1f;
            float noiseB = Mathf.PerlinNoise(0.43f, Time.time * 16f) * 2f - 1f;
            body.AddTorque(
                transform.forward * noiseA * buffetStrength +
                transform.up * noiseB * buffetStrength * 0.35f,
                ForceMode.Acceleration);
        }

        private void ConfigureWarningAudio()
        {
            if (!warningToneEnabled || warningTone == null) return;
            GameObject audioObject = new GameObject("Stall Warning Audio");
            audioObject.transform.SetParent(transform, false);
            warningSource = audioObject.AddComponent<AudioSource>();
            warningSource.clip = warningTone;
            warningSource.loop = true;
            warningSource.playOnAwake = false;
            warningSource.spatialBlend = 0f;
            warningSource.volume = 0.55f;
        }

        private void UpdateWarningAudio()
        {
            if (warningSource == null) return;
            if (StallWarning)
            {
                if (!warningSource.isPlaying) warningSource.Play();
            }
            else if (warningSource.isPlaying)
            {
                warningSource.Stop();
            }
        }

        public void ResetStall()
        {
            State = StallState.Normal;
            LiftMultiplier = 1f;
            ControlAuthorityMultiplier = 1f;
            NoseDropMoment = 0f;
            RecoveryProgress = 1f;
            BankRiskFactor = 1f;
            UpdateWarningAudio();
        }
    }
}
