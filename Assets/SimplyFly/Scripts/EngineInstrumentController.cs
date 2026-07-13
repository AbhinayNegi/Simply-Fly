using UnityEngine;

namespace SimplyFly.GroundOps
{
    /// <summary>Adapts the current engine model to the reusable HUD presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class EngineInstrumentController : MonoBehaviour
    {
        [SerializeField] private EngineController engine;
        [SerializeField] private EngineInstrumentHUD view;

        public void Bind(EngineController source) => engine = source;

        private void Awake()
        {
            if (view == null) view = GetComponent<EngineInstrumentHUD>();
            if (view == null) view = gameObject.AddComponent<EngineInstrumentHUD>();
            if (engine == null) engine = FindAnyObjectByType<EngineController>();
        }

        private void Update()
        {
            float output = engine == null ? 0f : engine.NormalizedRPM;
            float rpm = engine == null ? 0f : engine.CurrentRPM;
            EngineInstrumentState state = engine == null
                ? EngineInstrumentState.Off
                : engine.State switch
                {
                    EngineController.EngineState.Starting =>
                        EngineInstrumentState.Starting,
                    EngineController.EngineState.Running =>
                        EngineInstrumentState.Running,
                    _ => EngineInstrumentState.Off
                };
            view?.SetData(output, rpm, state);
        }
    }
}
