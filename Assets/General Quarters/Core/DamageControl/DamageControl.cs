using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.DamageControl
{
    /// <summary>One damageable system and where it sits in the ship frame.</summary>
    public struct SystemProfile
    {
        public ShipSystem System;
        public Vector3 PositionShip;
        public float HitPoints;
    }

    /// <summary>Damage tuning. All AUTHORED at L0: there is no public damage table to cite.</summary>
    public struct DamageProfileData
    {
        public float HullHitPoints;
        /// <summary>Hull hit points removed per kg of warhead.</summary>
        public float HullDamagePerWarheadKg;
        /// <summary>Hit points removed from the nearest system per kg of warhead.</summary>
        public float SystemDamagePerWarheadKg;
        /// <summary>A system takes damage only if the hit lands within this distance of it, m.</summary>
        public float SystemReachM;
        public SystemProfile[] Systems;
    }

    public interface IDamageControl
    {
        FidelityLevel Level { get; }
        float HullHitPoints { get; }
        bool OutOfAction { get; }
        bool IsOnline(ShipSystem system);

        /// <summary>Apply a hit; append any system that went offline to <paramref name="newlyOffline"/>.</summary>
        void ApplyHit(long tick, in ShipHit hit, in OwnShipState own, int causeEvent, EngagementLog log, List<ShipSystem> newlyOffline);
        ulong Fingerprint(ulong h);
    }

    /// <summary>
    /// Damage Control L0: hit points. A hit removes hull points by warhead mass and damages the nearest
    /// system within reach; a system at zero goes offline; the hull at zero puts the ship out of action.
    /// What it does NOT model (L1+): compartments, flooding into the buoyancy probes, list and sinking
    /// (L1); fire spread and damage-control parties (L2).
    /// </summary>
    public sealed class DamageControlL0 : IDamageControl
    {
        const string Rule = "Damage Control L0 (hit points)";

        readonly DamageProfileData _profile;
        readonly float[] _hp;
        readonly bool[] _online;

        public DamageControlL0(in DamageProfileData profile)
        {
            _profile = profile;
            HullHitPoints = profile.HullHitPoints;
            _hp = new float[profile.Systems.Length];
            _online = new bool[profile.Systems.Length];
            for (int i = 0; i < _hp.Length; i++) { _hp[i] = profile.Systems[i].HitPoints; _online[i] = true; }
        }

        public FidelityLevel Level => FidelityLevel.L0;
        public float HullHitPoints { get; private set; }
        public bool OutOfAction { get; private set; }

        public bool IsOnline(ShipSystem system)
        {
            for (int i = 0; i < _profile.Systems.Length; i++)
                if (_profile.Systems[i].System == system) return _online[i];
            return true;
        }

        public void ApplyHit(long tick, in ShipHit hit, in OwnShipState own, int causeEvent, EngagementLog log, List<ShipSystem> newlyOffline)
        {
            Vector3 local = own.ToShipFrame(hit.Point);
            float hullDamage = hit.WarheadKg * _profile.HullDamagePerWarheadKg;
            HullHitPoints = Mathf.Max(0f, HullHitPoints - hullDamage);

            int nearest = -1;
            float best = _profile.SystemReachM;
            for (int i = 0; i < _profile.Systems.Length; i++)
            {
                float d = Vector3.Distance(local, _profile.Systems[i].PositionShip);
                if (d <= best) { best = d; nearest = i; }
            }

            string where = nearest >= 0
                ? System.FormattableString.Invariant($", {_profile.Systems[nearest].System} hit ({best:0} m)")
                : ", no system within reach";
            int hitEvent = log.Add(tick, EventKind.ShipHit, Rule,
                System.FormattableString.Invariant($"{hit.Kind} hit at {local.z:+0;-0} m from amidships ({hit.WarheadKg:0} kg warhead): hull {HullHitPoints:0}/{_profile.HullHitPoints:0}") + where,
                -1, causeEvent, root: RootKind.Scenario);

            if (nearest >= 0 && _online[nearest])
            {
                _hp[nearest] = Mathf.Max(0f, _hp[nearest] - hit.WarheadKg * _profile.SystemDamagePerWarheadKg);
                if (_hp[nearest] <= 0f)
                {
                    _online[nearest] = false;
                    newlyOffline.Add(_profile.Systems[nearest].System);
                    log.Add(tick, EventKind.SystemOffline, Rule, $"{_profile.Systems[nearest].System} OFFLINE", -1, hitEvent);
                }
            }

            if (!OutOfAction && HullHitPoints <= 0f)
            {
                OutOfAction = true;
                log.Add(tick, EventKind.SystemOffline, Rule, "hull at zero: ship OUT OF ACTION (flooding and sinking are Damage Control L1)", -1, hitEvent);
            }
        }

        public ulong Fingerprint(ulong h)
        {
            h = CombatFix.Mix(CombatFix.Mix(h, HullHitPoints), OutOfAction);
            for (int i = 0; i < _hp.Length; i++) h = CombatFix.Mix(CombatFix.Mix(h, _hp[i]), _online[i]);
            return h;
        }
    }

    public static class DamageControlFactory
    {
        public static IDamageControl Create(FidelityLevel requested, in DamageProfileData profile, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.DamageControl, requested);
            switch (resolution.Resolved)
            {
                default: return new DamageControlL0(profile);
            }
        }
    }
}
