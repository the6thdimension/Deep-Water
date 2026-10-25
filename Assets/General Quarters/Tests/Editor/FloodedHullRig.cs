using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Editor;
using DeepWater.GeneralQuarters.ScriptableObjects;
using DeepWater.Sea.Core;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// The DDG on Sea State's own probe jobs in a calm sea, carrying Damage Control L1's water (G55): heave, roll and pitch
    /// integrated, with extra damping so she comes to rest quickly (the rest pose is the equilibrium, whatever the damping).
    /// The combat core steps every tick; the hull every <see cref="HullEvery"/> ticks. Pure EditMode, no PhysX, so the run
    /// repeats exactly. Public so a RunCommand can drive it for exploration.
    /// </summary>
    public sealed class FloodedHullRig : IDisposable
    {
        public const int HullEvery = 5;
        public const float G = 9.81f;

        public readonly CombatSystem Combat;
        public readonly IFlooding Flooding;
        public readonly ShipHullProfileSO Hull;

        /// <summary>The origin's height (the drawn waterline amidships), and rotations about z (+ starboard up) and x (+ bow down).</summary>
        public float HeaveM, RollRad, PitchRad;
        float _vy, _wRoll, _wPitch;
        readonly float _cHeave, _cRoll, _cPitch;

        readonly SeaSimulation _sea;
        NativeArray<BuoyancyProbe> _probes;
        NativeArray<int> _probeBody;
        NativeArray<BuoyancyBodyPose> _poses;
        NativeArray<BuoyancyDamping> _damping;
        NativeArray<BuoyancyProbeOutput> _probeOut;
        NativeArray<int2> _ranges;
        NativeArray<BuoyancyBodyOutput> _bodyOut;
        readonly List<TruthTarget> _world = new List<TruthTarget>();
        readonly List<ShipHit> _hits = new List<ShipHit>();
        readonly List<KillReport> _kills = new List<KillReport>();
        readonly Stopwatch _clock = new Stopwatch();
        long _coreSteps;

        /// <summary>A DDG at Damage Control L1: the test loadout, with the range loadout's flooding data (the hull's spaces).</summary>
        public static CombatLoadout Loadout(ulong seed = 1442)
        {
            CombatLoadout l = TestLoadouts.Ddg51L0(seed);
            l.DamageLevel = FidelityLevel.L1;
            l.Flooding = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset).Bake().Flooding;
            return l;
        }

        public FloodedHullRig(CombatLoadout loadout, float extraDampingRatio = 0.6f)
        {
            Combat = new CombatSystem(loadout);
            Flooding = Combat.DamageControl.Flooding ?? throw new ArgumentException("the rig needs Damage Control L1");
            Hull = AssetDatabase.LoadAssetAtPath<ShipHullProfileSO>(Ddg51HullBuilder.HullAsset);

            _sea = new SeaSimulation(0f, 4);
            _sea.Apply(new SeaBakeInput { MinWavelengthM = 10f, Gravity = G, FoamCalibrationGrid = 32 }, default, 0f);
            BuoyancyHullSOProbes(Hull, out _probes);
            int n = _probes.Length;
            _probeBody = new NativeArray<int>(n, Allocator.Persistent);
            _probeOut = new NativeArray<BuoyancyProbeOutput>(n, Allocator.Persistent);
            _poses = new NativeArray<BuoyancyBodyPose>(1, Allocator.Persistent);
            _damping = new NativeArray<BuoyancyDamping>(1, Allocator.Persistent);
            _ranges = new NativeArray<int2>(1, Allocator.Persistent);
            _bodyOut = new NativeArray<BuoyancyBodyOutput>(1, Allocator.Persistent);
            _damping[0] = Hull.buoyancy.BakeDamping(G);
            _ranges[0] = new int2(0, n);

            // Extra damping, as a fraction of each mode's critical damping (intact numbers are close enough for a brake).
            float m = Hull.massKg;
            Vector3 inertia = Hull.InertiaTensor;
            float awp = 2004f, gm = 1.56f, gmL = 289f;   // the probe hull's own (flood_predict.py, intact)
            _cHeave = 2f * extraDampingRatio * Mathf.Sqrt(Combat.DamageControl.Flooding.Plan.Profile.SeawaterDensityKgM3 * G * awp / m);
            _cRoll = 2f * extraDampingRatio * Mathf.Sqrt(m * G * gm / inertia.z);
            _cPitch = 2f * extraDampingRatio * Mathf.Sqrt(m * G * gmL / inertia.x);
        }

        static void BuoyancyHullSOProbes(ShipHullProfileSO hull, out NativeArray<BuoyancyProbe> probes)
        {
            var src = hull.buoyancy.probes;
            probes = new NativeArray<BuoyancyProbe>(src.Length, Allocator.Persistent);
            for (int i = 0; i < src.Length; i++)
                probes[i] = new BuoyancyProbe { LocalPosition = src[i].localPosition, AreaM2 = src[i].areaM2, HeightM = src[i].heightM };
        }

        public float ListDeg => -RollRad * Mathf.Rad2Deg;
        public float TrimM => Flooding.Plan.Profile.LengthWlM * Mathf.Tan(PitchRad);
        public float SinkageM => -HeaveM;
        public float Seconds => Combat.Tick * Combat.TickSeconds;
        /// <summary>Mean wall-clock time of the combat core's step, ms (Editor).</summary>
        public double CoreMsPerTick => _coreSteps > 0 ? _clock.Elapsed.TotalMilliseconds / _coreSteps : 0.0;

        public OwnShipState Own() => new OwnShipState
        {
            Position = new Vector3(0f, HeaveM, 0f),
            HeadingDeg = 0f,
            RollDeg = -RollRad * Mathf.Rad2Deg,
            PitchDeg = -PitchRad * Mathf.Rad2Deg,
        };

        public Quaternion Rotation => Quaternion.Euler(PitchRad * Mathf.Rad2Deg, 0f, RollRad * Mathf.Rad2Deg);

        /// <summary>One hull step: <see cref="HullEvery"/> core ticks, then the hull moves under buoyancy and its (wet) weight.</summary>
        public void Step()
        {
            for (int k = 0; k < HullEvery; k++)
            {
                _hits.Clear();
                _kills.Clear();
                _clock.Start();
                Combat.Step(Own(), _world, _hits, _kills);
                _clock.Stop();
                _coreSteps++;
            }
            float dt = HullEvery * Combat.TickSeconds;
            Load(out float mass, out Vector3 inertia, out Vector3 force, out Vector3 torqueShip, Flooding.Water.WaterCentroidShip);
            float ay = force.y / mass - G - _cHeave * _vy;
            float aRoll = torqueShip.z / inertia.z - _cRoll * _wRoll;
            float aPitch = torqueShip.x / inertia.x - _cPitch * _wPitch;
            _vy += ay * dt; HeaveM += _vy * dt;
            _wRoll += aRoll * dt; RollRad += _wRoll * dt;
            _wPitch += aPitch * dt; PitchRad += _wPitch * dt;
        }

        /// <summary>Step until she has been still for <paramref name="stillS"/> (or <paramref name="maxS"/> pass). True if she settled.</summary>
        public bool Settle(float maxS, float stillS = 5f)
        {
            float still = 0f, dt = HullEvery * Combat.TickSeconds;
            for (float t = 0f; t < maxS; t += dt)
            {
                Step();
                bool quiet = Mathf.Abs(_vy) < 2e-4f && Mathf.Abs(_wRoll) < 2e-5f && Mathf.Abs(_wPitch) < 2e-5f
                             && Mathf.Abs(Flooding.Water.InflowM3s) < 0.05f;
                still = quiet ? still + dt : 0f;
                if (still >= stillS) return true;
            }
            return false;
        }

        /// <summary>
        /// The ship plus her water as one body at the current pose: mass, inertia, Sea State's force on her and the torque
        /// about the combined centre of mass, in the ship frame. <paramref name="waterCentroidShip"/> lets a test freeze the
        /// water where it was (a solid load) to compare with water that runs (a free surface).
        /// </summary>
        public void Load(out float mass, out Vector3 inertia, out Vector3 force, out Vector3 torqueShip, Vector3 waterCentroidShip)
        {
            FloodMass.Combine(Hull.massKg, Hull.centerOfMassM, Hull.InertiaTensor, Flooding.Water.WaterMassKg, waterCentroidShip,
                out mass, out Vector3 centre, out inertia);
            Quaternion q = Rotation;
            var origin = new Vector3(0f, HeaveM, 0f);
            _poses[0] = new BuoyancyBodyPose
            {
                Position = origin,
                Rotation = q,
                LinearVelocity = new float3(0f, _vy, 0f),
                AngularVelocity = new float3(_wPitch, 0f, _wRoll),
                WorldCenterOfMass = origin + q * centre,
            };
            new BuoyancyProbeJob
            {
                Groups = _sea.PhysicsGroups, GroupCount = _sea.PhysicsGroupCount, SeaLevel = _sea.SeaLevel,
                InverseIterations = _sea.InverseIterations, WaterDensity = Flooding.Plan.Profile.SeawaterDensityKgM3, Gravity = G,
                Probes = _probes, ProbeBody = _probeBody, Poses = _poses, Damping = _damping, Output = _probeOut,
            }.Run(_probes.Length);
            new BuoyancySumJob { BodyProbeRange = _ranges, Probes = _probeOut, Output = _bodyOut }.Run(1);
            force = _bodyOut[0].Force;
            torqueShip = Quaternion.Inverse(q) * (Vector3)_bodyOut[0].Torque;
        }

        /// <summary>Each probe's buoyancy force this pose (after <see cref="Load"/>), for the lost-buoyancy identity.</summary>
        public NativeArray<BuoyancyProbeOutput> ProbeOutputs => _probeOut;
        public NativeArray<BuoyancyProbe> Probes => _probes;

        /// <summary>Heel her to <paramref name="rollRad"/> about the waterline centreline, keeping heave and pitch, stopped.</summary>
        public void Pose(float rollRad)
        {
            RollRad = rollRad;
            _vy = _wRoll = _wPitch = 0f;
        }

        public void Dispose()
        {
            _sea.Dispose();
            _probes.Dispose(); _probeBody.Dispose(); _probeOut.Dispose(); _poses.Dispose();
            _damping.Dispose(); _ranges.Dispose(); _bodyOut.Dispose();
        }
    }
}
