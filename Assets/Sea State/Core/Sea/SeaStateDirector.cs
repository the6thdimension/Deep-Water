using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.Sea.Core
{
    /// <summary>How the render clock relates to the fixed-step simulation clock.</summary>
    public enum SeaRenderClock
    {
        /// <summary>
        /// Waves are drawn at the same instant interpolated rigidbodies are drawn
        /// (between the last two fixed steps). Use with <c>RigidbodyInterpolation.Interpolate</c> (Pat5).
        /// </summary>
        Interpolated,
        /// <summary>Waves are drawn at the latest fixed step (for non-interpolated bodies).</summary>
        LatestStep,
    }

    /// <summary>
    /// MonoBehaviour adapter for <see cref="SeaSimulation"/>: picks the Douglas sea and swell,
    /// pumps the simulation each FixedUpdate, and exposes the sea to the scene through
    /// <see cref="ISeaSurface"/>. Runs before anything that queries it.
    ///
    /// **Timing contract.** At FixedUpdate n every body still holds its state from the end
    /// of physics step n−1, i.e. time (n−1)·Δt. The director therefore advances its clock at
    /// the *start* of each FixedUpdate except the first, so queries made during FixedUpdate n
    /// see the sea at exactly the time the bodies' state belongs to.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class SeaStateDirector : MonoBehaviour, ISeaSurface
    {
        [SerializeField] SeaStateLibrarySO library;

        [Header("Sea state")]
        [SerializeField, Range(0, 9)] int douglasSea = 4;
        [SerializeField, Range(0, 9)] int douglasSwell = 0;

        [Header("Scenario")]
        [Tooltip("Direction the wind blows FROM, compass degrees (0 = from +Z, 90 = from +X).")]
        [SerializeField, Range(0f, 360f)] float windFromDeg = 0f;
        [Tooltip("Direction the swell comes FROM, compass degrees.")]
        [SerializeField, Range(0f, 360f)] float swellFromDeg = 270f;
        [Tooltip("XOR-ed into every profile seed. 0 = profile seeds as authored. Same seed → same sea (P7).")]
        [SerializeField] uint scenarioSeed = 0u;

        [Header("Rendering")]
        [SerializeField] SeaRenderClock renderClock = SeaRenderClock.Interpolated;

        SeaSimulation _simulation;
        bool _steppedOnce;
        float _gravity;

        /// <summary>Raised after every sea-state change with the new conditions.</summary>
        public event Action<SeaConditions> ConditionsChanged;

        public SeaSimulation Simulation => _simulation;
        public SeaStateLibrarySO Library => library;
        public bool IsReady => _simulation != null;
        public int DouglasSea => douglasSea;
        public int DouglasSwell => douglasSwell;
        public float WindFromDeg => windFromDeg;
        public float SwellFromDeg => swellFromDeg;
        public float Gravity => _gravity;
        public float WaterDensityKgM3 => library != null ? library.waterDensityKgM3 : 0f;

        public SeaConditions Conditions => _simulation != null ? _simulation.Conditions : default;
        public double SimulationTimeS => _simulation != null ? _simulation.SimulationTimeS : 0.0;

        /// <summary>
        /// Simulation time at which the current render frame should draw the waves, so the
        /// waves on screen match the hull poses on screen (see <see cref="SeaRenderClock"/>).
        /// </summary>
        public double RenderTimeS
        {
            get
            {
                if (_simulation == null) return 0.0;
                double dt = Time.fixedDeltaTime;
                if (renderClock == SeaRenderClock.LatestStep) return _simulation.SimulationTimeS + dt;
                double alpha = dt > 0.0 ? math.saturate((Time.timeAsDouble - Time.fixedTimeAsDouble) / dt) : 0.0;
                return _simulation.SimulationTimeS + alpha * dt;
            }
        }

        void OnEnable()
        {
            if (library == null)
            {
                Debug.LogError($"[SeaStateDirector] '{name}' has no SeaStateLibrary assigned; the sea is disabled.", this);
                enabled = false;
                return;
            }

            _gravity = Physics.gravity.magnitude;
            _simulation = new SeaSimulation(transform.position.y, library.inverseIterations);
            _steppedOnce = false;
            ApplyCurrent(0f);
        }

        void OnDisable()
        {
            _simulation?.Dispose();
            _simulation = null;
        }

        void FixedUpdate()
        {
            if (_steppedOnce) _simulation.Step(Time.fixedDeltaTime);
            _steppedOnce = true;
        }

        /// <summary>Change the Douglas wind-sea degree (cross-fades over the library's transition time).</summary>
        public void SetSeaState(int degree) => Set(ref douglasSea, Mathf.Clamp(degree, 0, 9));

        /// <summary>Change the Douglas swell degree.</summary>
        public void SetSwell(int degree) => Set(ref douglasSwell, Mathf.Clamp(degree, 0, 9));

        /// <summary>Change the wind direction (compass degrees the wind blows from).</summary>
        public void SetWindFrom(float compassDeg) => Set(ref windFromDeg, Mathf.Repeat(compassDeg, 360f));

        /// <summary>Change the swell direction (compass degrees the swell comes from).</summary>
        public void SetSwellFrom(float compassDeg) => Set(ref swellFromDeg, Mathf.Repeat(compassDeg, 360f));

        public void Query(NativeArray<float3> worldPositions, NativeArray<SeaSample> results)
            => _simulation.Query(worldPositions, results);

        public SeaSample Sample(float3 worldPosition) => _simulation.Sample(worldPosition);

        void Set<T>(ref T field, T value) where T : IEquatable<T>
        {
            if (field.Equals(value)) return;
            field = value;
            if (_simulation != null) ApplyCurrent(library.transitionSeconds);
        }

        void OnValidate()
        {
            // Inspector edits during play take effect like a selector change.
            if (Application.isPlaying && _simulation != null && library != null)
                ApplyCurrent(library.transitionSeconds);
        }

        void ApplyCurrent(float transitionSeconds)
        {
            SeaStateProfileSO sea = library.GetSea(douglasSea);
            SwellProfileSO swell = library.GetSwell(douglasSwell);
            if (sea == null)
                Debug.LogError($"[SeaStateDirector] Library has no profile for Douglas sea {douglasSea}; using a flat sea.", this);
            if (swell == null && douglasSwell != 0)
                Debug.LogError($"[SeaStateDirector] Library has no profile for Douglas swell {douglasSwell}; swell omitted.", this);

            SeaBakeInput input = library.BuildBakeInput(douglasSea, douglasSwell, windFromDeg, swellFromDeg,
                scenarioSeed, _gravity, out SeaConditions conditions);
            SeaBakeReport report = _simulation.Apply(input, conditions, transitionSeconds);

            Debug.Log($"[SeaStateDirector] Douglas sea {douglasSea} ({(sea != null ? sea.douglasTerm : "missing")}) + swell {douglasSwell}: " +
                      $"Hs {report.TargetHsM:0.00} m (resolved {report.ResolvedHsM:0.00} m, {report.ResolvedVarianceFraction:P0} of variance), " +
                      $"Tp {conditions.WindSeaTpS:0.0} s, wind {conditions.WindSpeedMs / SeaUnits.MetersPerSecondPerKnot:0.#} kt from {windFromDeg:0}°, " +
                      $"whitecaps {conditions.WhitecapFraction:P1}, {report.WindSeaCount}+{report.SwellCount} components, " +
                      $"folded {report.FoldedFraction:P2}, fade {transitionSeconds:0.#} s, t={_simulation.SimulationTimeS:0.00} s.", this);

            ConditionsChanged?.Invoke(_simulation.Conditions);
        }
    }
}
