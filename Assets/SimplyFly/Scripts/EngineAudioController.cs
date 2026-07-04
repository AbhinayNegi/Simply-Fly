using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineAudioController : MonoBehaviour
    {
        [SerializeField] private EngineController engine;
        [SerializeField] private AudioClip engineLoop;
        [Header("Response")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.75f, 1.55f);
        [SerializeField] private Vector2 volumeRange = new Vector2(0.28f, 0.72f);
        [Min(0.01f)] [SerializeField] private float audioSmoothing = 4f;
        [Range(0f, 1f)] [SerializeField] private float volumeMultiplier = 1f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.72f;
            source.dopplerLevel = 0.1f;
            source.minDistance = 8f;
            source.maxDistance = 140f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.clip = engineLoop != null ? engineLoop : CreatePlaceholderLoop();
            source.Play();
        }

        private void Update()
        {
            if (engine == null || source == null) return;
            float rpm = engine.NormalizedRPM;
            float targetPitch = Mathf.Lerp(pitchRange.x, pitchRange.y, rpm);
            float targetVolume = Mathf.Lerp(0f, Mathf.Lerp(volumeRange.x, volumeRange.y, rpm),
                Mathf.InverseLerp(10f, 250f, engine.CurrentRPM)) *
                volumeMultiplier;
            float blend = 1f - Mathf.Exp(-audioSmoothing * Time.deltaTime);
            source.pitch = Mathf.Lerp(source.pitch, targetPitch, blend);
            source.volume = Mathf.Lerp(source.volume, targetVolume, blend);
        }

        public void SetVolumeMultiplier(float value)
        {
            volumeMultiplier = Mathf.Clamp01(value);
        }

        private static AudioClip CreatePlaceholderLoop()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float pulse = Mathf.Sin(t * Mathf.PI * 2f * 47f) * 0.72f;
                float second = Mathf.Sin(t * Mathf.PI * 2f * 94f) * 0.42f;
                float third = Mathf.Sin(t * Mathf.PI * 2f * 141f) * 0.24f;
                float mechanical = Mathf.Sin(t * Mathf.PI * 2f * 188f) * 0.13f;
                float texture = Mathf.Sin(t * Mathf.PI * 2f * 282f) * 0.06f;
                float combustionPulse =
                    0.84f + Mathf.Sin(t * Mathf.PI * 2f * 12f) * 0.16f;
                samples[i] =
                    (pulse + second + third + mechanical + texture) *
                    combustionPulse * 0.26f;
            }

            AudioClip clip = AudioClip.Create("Procedural Engine Placeholder", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
