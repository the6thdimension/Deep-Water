using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.Sea.Core;
using DeepWater.Sea.Modules.Buoyancy;

namespace DeepWater.GeneralQuarters.Modules.Hull
{
    /// <summary>
    /// Carries Damage Control L1's flood water into PhysX (G55) and the sea into Damage Control. Each fixed step, before
    /// Sea State's buoyancy and the combat core run:
    /// 1. the Rigidbody takes the water the core holds (its mass and centroid, through <see cref="ShipHull"/>);
    /// 2. the sea surface is sampled at every opening to the sea (holes and weather openings) for the core's heads.
    /// The core steps after this, so the hull carries last step's water: one 20 ms step behind, against an 11 s roll.
    /// Below Damage Control L1 it does nothing.
    ///
    /// Once she is lost (SINKING, G60) and wholly under water, it stops her physics where she is. Nothing below the hull
    /// is modelled (no seabed, no wreck), and a sunk hull plunging by the head has no damping along its length (the probes
    /// damp heave only, G21), so it falls without limit. On 2026-10-24 that ran the hull to positions the renderer could
    /// not draw, and the Editor lost its GPU device. A fresh run raises her again.
    /// </summary>
    [DefaultExecutionOrder(-110)]   // before BuoyancySystem (−100) and ShipCombat (−100)
    [RequireComponent(typeof(ShipCombat), typeof(ShipHull), typeof(BuoyantBody))]
    [DisallowMultipleComponent]
    public sealed class ShipFlooding : MonoBehaviour
    {
        /// <summary>Wholly under: the share of the probes' volume submerged at which a sinking ship is gone.</summary>
        const float GoneFraction = 0.999f;

        ShipCombat _combat;
        ShipHull _hull;
        BuoyantBody _buoyant;
        CombatSystem _run;
        bool _gone;
        NativeArray<float3> _points;
        NativeArray<SeaSample> _samples;
        int[] _seaOpening = new int[0];
        bool _densityChecked;

        void Awake()
        {
            _combat = GetComponent<ShipCombat>();
            _hull = GetComponent<ShipHull>();
            _buoyant = GetComponent<BuoyantBody>();
        }

        void OnDisable()
        {
            if (_points.IsCreated) _points.Dispose();
            if (_samples.IsCreated) _samples.Dispose();
        }

        void FixedUpdate()
        {
            if (_combat.Combat != _run)
            {
                _run = _combat.Combat;
                if (_gone) RaiseHer();
            }
            IFlooding flooding = _combat.Combat?.DamageControl.Flooding;
            if (flooding == null || _gone) return;
            FloodModel water = flooding.Water;
            _hull.ApplyFloodWater(water.WaterMassKg, water.WaterCentroidShip);
            SampleSea(flooding, water);
            if (flooding.Verdict == FloodVerdict.Sinking && _buoyant.SubmergedFraction >= GoneFraction) StopHer(flooding);
        }

        void StopHer(IFlooding flooding)
        {
            Rigidbody body = _hull.Body;
            Vector3 at = body.position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            _gone = true;
            CombatSystem c = _combat.Combat;
            c.Log.Add(c.Tick, EventKind.Note, "Range (Damage Control L1 → hull)", System.FormattableString.Invariant(
                $"she has gone down, {-at.y:0} m under, with {flooding.Water.WaterMassKg / 1000f:0} t of water in her: the hull's physics stopped (no seabed is modelled)"),
                root: RootKind.Scenario);
            Debug.Log($"[ShipFlooding] '{name}' sank and was stopped {-at.y:0} m under (no seabed is modelled).", this);
        }

        /// <summary>A fresh run after she went down: back on the surface, upright on her heading, dry, still.</summary>
        void RaiseHer()
        {
            Rigidbody body = _hull.Body;
            Vector3 p = body.position;
            Vector3 forward = body.rotation * Vector3.forward;
            forward.y = 0f;
            Quaternion upright = forward.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
            body.position = new Vector3(p.x, 0f, p.z);
            body.rotation = upright;
            transform.SetPositionAndRotation(body.position, upright);
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            _hull.ApplyFloodWater(0f, Vector3.zero);
            _gone = false;
        }

        void SampleSea(IFlooding flooding, FloodModel water)
        {
            BuoyancySystem system = _buoyant.System;
            if (system == null || system.Sea == null || !system.Sea.IsReady) return;
            if (!_densityChecked)
            {
                _densityChecked = true;
                float sea = system.Sea.WaterDensityKgM3, core = flooding.Plan.Profile.SeawaterDensityKgM3;
                if (Mathf.Abs(sea - core) > 0.5f)
                    Debug.LogWarning($"[ShipFlooding] the sea is {sea:0} kg/m³ but Damage Control floods at {core:0} kg/m³: " +
                                     "water and buoyancy will not cancel in a space open to the sea.", this);
            }

            int count = 0;
            for (int i = 0; i < water.OpeningCount; i++) if (water.Opening(i).ToSea) count++;
            if (count == 0) return;
            if (!_points.IsCreated || _points.Length != count)
            {
                OnDisable();
                _points = new NativeArray<float3>(count, Allocator.Persistent);
                _samples = new NativeArray<SeaSample>(count, Allocator.Persistent);
                _seaOpening = new int[count];
            }
            for (int i = 0, k = 0; i < water.OpeningCount; i++)
            {
                FloodOpening o = water.Opening(i);
                if (!o.ToSea) continue;
                _seaOpening[k] = i;
                _points[k++] = transform.TransformPoint(o.PointShip);
            }
            system.Sea.Query(_points, _samples);
            for (int k = 0; k < count; k++) water.SetSeaSurface(_seaOpening[k], _samples[k].SurfaceHeight);
        }
    }
}
