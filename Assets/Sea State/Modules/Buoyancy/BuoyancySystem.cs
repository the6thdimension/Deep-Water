using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Modules.Buoyancy
{
    /// <summary>
    /// Floats every registered <see cref="BuoyantBody"/> on the simulated sea with one batched
    /// Burst job per fixed step (all probes of all bodies), then one AddForce + AddTorque per
    /// body. Runs after the <see cref="SeaStateDirector"/> has advanced the sea clock.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class BuoyancySystem : MonoBehaviour
    {
        [SerializeField] SeaStateDirector sea;

        static readonly ProfilerMarker s_StepMarker = new ProfilerMarker("DeepWater.Sea.Buoyancy");

        readonly List<BuoyantBody> _bodies = new List<BuoyantBody>();
        bool _layoutDirty = true;

        NativeArray<BuoyancyProbe> _probes;
        NativeArray<int> _probeBody;
        NativeArray<int2> _bodyRange;
        NativeArray<BuoyancyDamping> _damping;
        NativeArray<BuoyancyBodyPose> _poses;
        NativeArray<BuoyancyProbeOutput> _probeOutput;
        NativeArray<BuoyancyBodyOutput> _bodyOutput;

        public SeaStateDirector Sea => sea;
        public int BodyCount => _bodies.Count;
        public int ProbeCount => _probes.IsCreated ? _probes.Length : 0;

        public void Register(BuoyantBody body)
        {
            if (!_bodies.Contains(body)) { _bodies.Add(body); _layoutDirty = true; }
        }

        public void Unregister(BuoyantBody body)
        {
            if (_bodies.Remove(body)) _layoutDirty = true;
        }

        void OnEnable()
        {
            if (sea == null)
            {
                Debug.LogError($"[BuoyancySystem] '{name}' has no SeaStateDirector; buoyancy disabled.", this);
                enabled = false;
            }
        }

        void OnDisable() => DisposeLayout();

        void FixedUpdate()
        {
            if (!sea.IsReady || _bodies.Count == 0) return;

            using (s_StepMarker.Auto())
            {
                if (_layoutDirty) RebuildLayout();

                for (int b = 0; b < _bodies.Count; b++)
                {
                    Rigidbody rb = _bodies[b].Body;
                    _poses[b] = new BuoyancyBodyPose
                    {
                        Position = rb.position,
                        Rotation = rb.rotation,
                        LinearVelocity = rb.linearVelocity,
                        AngularVelocity = rb.angularVelocity,
                        WorldCenterOfMass = rb.worldCenterOfMass,
                    };
                }

                SeaSimulation sim = sea.Simulation;
                JobHandle probes = new BuoyancyProbeJob
                {
                    Groups = sim.PhysicsGroups,
                    GroupCount = sim.PhysicsGroupCount,
                    SeaLevel = sim.SeaLevel,
                    InverseIterations = sim.InverseIterations,
                    WaterDensity = sea.WaterDensityKgM3,
                    Gravity = sea.Gravity,
                    Probes = _probes,
                    ProbeBody = _probeBody,
                    Poses = _poses,
                    Damping = _damping,
                    Output = _probeOutput,
                }.Schedule(_probes.Length, 32);

                new BuoyancySumJob
                {
                    BodyProbeRange = _bodyRange,
                    Probes = _probeOutput,
                    Output = _bodyOutput,
                }.Schedule(_bodies.Count, 1, probes).Complete();

                for (int b = 0; b < _bodies.Count; b++)
                {
                    BuoyancyBodyOutput o = _bodyOutput[b];
                    Rigidbody rb = _bodies[b].Body;
                    rb.AddForce(o.Force, ForceMode.Force);
                    rb.AddTorque(o.Torque, ForceMode.Force);
                    _bodies[b].SubmergedFraction = o.SubmergedFraction;
                }
            }
        }

        void RebuildLayout()
        {
            DisposeLayout();
            int probeCount = 0;
            foreach (var b in _bodies) probeCount += b.Hull.probes.Length;

            _probes = new NativeArray<BuoyancyProbe>(probeCount, Allocator.Persistent);
            _probeBody = new NativeArray<int>(probeCount, Allocator.Persistent);
            _probeOutput = new NativeArray<BuoyancyProbeOutput>(probeCount, Allocator.Persistent);
            _bodyRange = new NativeArray<int2>(_bodies.Count, Allocator.Persistent);
            _damping = new NativeArray<BuoyancyDamping>(_bodies.Count, Allocator.Persistent);
            _poses = new NativeArray<BuoyancyBodyPose>(_bodies.Count, Allocator.Persistent);
            _bodyOutput = new NativeArray<BuoyancyBodyOutput>(_bodies.Count, Allocator.Persistent);

            int next = 0;
            for (int b = 0; b < _bodies.Count; b++)
            {
                var hull = _bodies[b].Hull;
                _bodyRange[b] = new int2(next, hull.probes.Length);
                _damping[b] = hull.BakeDamping(sea.Gravity);
                for (int p = 0; p < hull.probes.Length; p++, next++)
                {
                    _probes[next] = hull.BakeProbe(p);
                    _probeBody[next] = b;
                }
            }
            _layoutDirty = false;
        }

        void DisposeLayout()
        {
            if (_probes.IsCreated) _probes.Dispose();
            if (_probeBody.IsCreated) _probeBody.Dispose();
            if (_probeOutput.IsCreated) _probeOutput.Dispose();
            if (_bodyRange.IsCreated) _bodyRange.Dispose();
            if (_damping.IsCreated) _damping.Dispose();
            if (_poses.IsCreated) _poses.Dispose();
            if (_bodyOutput.IsCreated) _bodyOutput.Dispose();
            _layoutDirty = true;
        }
    }
}
