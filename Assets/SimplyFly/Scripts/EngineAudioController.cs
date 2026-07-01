using UnityEngine;

namespace SimplyFly.GroundOps
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class EngineAudioController : MonoBehaviour
    {
        [SerializeField] private EngineController engine;
        [SerializeField] private AudioClip engineLoop;
        [Header("Response")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.65f, 1.45f);
        [SerializeField] private Vector2 volumeRange = new Vector2(0.18f, 0.55f);
        [Min(0.01f)] [SerializeField] private float audioSmoothing = 4f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0.1f;
            source.clip = engineLoop != null ? engineLoop : CreatePlaceholderLoop();
            source.Play();
        }

        private void Update()
        {
            if (engine == null || source == null) return;
            float rpm = engine.NormalizedRPM;
            float targetPitch = Mathf.Lerp(pitchRange.x, pitchRange.y, rpm);
            float targetVolume = Mathf.Lerp(0f, Mathf.Lerp(volumeRange.x, volumeRange.y, rpm),
                Mathf.InverseLerp(10f, 250f, engine.CurrentRPM));
            float blend = 1f - Mathf.Exp(-audioSmoothing * Time.deltaTime);
            source.pitch = Mathf.Lerp(source.pitch, targetPitch, blend);
            source.volume = Mathf.Lerp(source.volume, targetVolume, blend);
        }

        private static AudioClip CreatePlaceholderLoop()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float pulse = Mathf.Sin(t * Mathf.PI * 2f * 38f);
                float harmonic = Mathf.Sin(t * Mathf.PI * 2f * 76f) * 0.35f;
                float texture = Mathf.Sin(t * Mathf.PI * 2f * 114f) * 0.12f;
                samples[i] = (pulse + harmonic + texture) * 0.18f;
            }

            AudioClip clip = AudioClip.Create("Procedural Engine Placeholder", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
