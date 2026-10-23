using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using DeepWater.Sea.Core;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.Sea.Modules.Buoyancy
{
    /// <summary>
    /// Floats a Rigidbody on the simulated sea: each FixedUpdate it batch-queries the sea at
    /// every hull probe and applies buoyancy + drag at the probe positions. Runs after the
    /// <see cref="SeaStateDirector"/> (which advances the sea clock first).
    ///
    /// The sea reference is explicit (no scene search): wire it in the Inspector or via
    /// <see cref="Configure"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-100)]
    public sealed class BuoyantBody : MonoBehaviour
    {
        [SerializeField] SeaStateDirector sea;
        [SerializeField] BuoyancyHullSO hull;
        [Tooltip("Set the Rigidbody's mass from the hull asset on enable.")]
        [SerializeField] bool applyHullMass = true;

        Rigidbody _body;
        NativeArray<float3> _probeWorld;
        NativeArray<SeaSample> _samples;
        BuoyancyProbe[] _probes;
        BuoyancyDamping _damping;

        /// <summary>Fraction of the hull's probe volume currently under water, 0–1.</summary>
        public float SubmergedFraction { get; private set; }

        /// <summary>Runtime wiring (e.g. for spawned hulls). Re-initialises if already running.</summary>
        public void Configure(SeaStateDirector seaDirector, BuoyancyHullSO hullAsset)
        {
            sea = seaDirector;
            hull = hullAsset;
            if (Application.isPlaying && isActiveAndEnabled) { OnDisable(); enabled = true; OnEnable(); }
        }

        void OnEnable()
        {
            _body = GetComponent<Rigidbody>();
            if (sea == null || hull == null || hull.probes == null || hull.probes.Length == 0)
            {
                Debug.LogError($"[BuoyantBody] '{name}' needs a SeaStateDirector and a BuoyancyHull with probes; buoyancy disabled.", this);
                enabled = false;
                return;
            }

            int n = hull.probes.Length;
            _probes = new BuoyancyProbe[n];
            for (int i = 0; i < n; i++) _probes[i] = hull.BakeProbe(i);
            _probeWorld = new NativeArray<float3>(n, Allocator.Persistent);
            _samples = new NativeArray<SeaSample>(n, Allocator.Persistent);
            if (applyHullMass) _body.mass = hull.massKg;
        }

        void OnDisable()
        {
            if (_probeWorld.IsCreated) _probeWorld.Dispose();
            if (_samples.IsCreated) _samples.Dispose();
        }

        void FixedUpdate()
        {
            if (!sea.IsReady) return;

            // Damping depends on gravity, which the director captures at enable.
            _damping = hull.BakeDamping(sea.Gravity);

            // Rigid pose only — probe offsets are metres in the hull's frame and must not pick up
            // the transform's scale (AP10).
            float3 origin = _body.position;
            quaternion rotation = _body.rotation;
            for (int i = 0; i < _probes.Length; i++)
                _probeWorld[i] = origin + math.mul(rotation, _probes[i].LocalPosition);

            sea.Query(_probeWorld, _samples);

            float density = sea.WaterDensityKgM3;
            float gravity = sea.Gravity;
            float submergedVolume = 0f, totalVolume = 0f;

            for (int i = 0; i < _probes.Length; i++)
            {
                float3 p = _probeWorld[i];
                SeaSample s = _samples[i];
                float3 pointVelocity = _body.GetPointVelocity(p);
                float3 force = BuoyancyModel.ProbeForce(_probes[i], _damping, density, gravity,
                    s.SurfaceHeight, p, pointVelocity, s.Velocity);

                if (math.any(force != float3.zero))
                    _body.AddForceAtPosition(force, p, ForceMode.Force);

                submergedVolume += _probes[i].AreaM2 * BuoyancyModel.SubmergedHeight(s.SurfaceHeight, p.y, _probes[i].HeightM);
                totalVolume += _probes[i].AreaM2 * _probes[i].HeightM;
            }

            SubmergedFraction = totalVolume > 0f ? submergedVolume / totalVolume : 0f;
        }
    }
}
