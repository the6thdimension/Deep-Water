using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.DamageControl
{
    /// <summary>One damageable system and where it sits in the ship frame.</summary>
    [System.Serializable]
    public struct SystemProfile
    {
        public ShipSystem System;
        public Vector3 PositionShip;
        public float HitPoints;
    }

    /// <summary>Damage tuning. All AUTHORED at L0: there is no public damage table to cite.</summary>
    [System.Serializable]
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
        /// <summary>The ship's spaces and the water in them; null below L1.</summary>
        IFlooding Flooding { get; }

        /// <summary>Apply a hit; append any system that went offline to <paramref name="newlyOffline"/>.</summary>
        void ApplyHit(long tick, in ShipHit hit, in OwnShipState own, int causeEvent, EngagementLog log, List<ShipSystem> newlyOffline);
        /// <summary>A Breach Order (G57): open a hole. <paramref name="orderEvent"/> is the log event of the Order.</summary>
        void Breach(long tick, in Order order, in OwnShipState own, int orderEvent, EngagementLog log);
        /// <summary>Advance the element one tick, after this tick's hits (BLUEPRINT §3.2, step 7).</summary>
        void Step(long tick, float dt, in OwnShipState own, EngagementLog log);
        ulong Fingerprint(ulong h);
    }

    /// <summary>
    /// The systems' hit points, shared by every Damage Control level: a hit damages the nearest system within reach,
    /// and a system at zero goes offline.
    /// </summary>
    public sealed class SystemDamage
    {
        readonly DamageProfileData _profile;
        readonly float[] _hp;
        readonly bool[] _online;

        public SystemDamage(in DamageProfileData profile)
        {
            _profile = profile;
            _hp = new float[profile.Systems.Length];
            _online = new bool[profile.Systems.Length];
            for (int i = 0; i < _hp.Length; i++) { _hp[i] = profile.Systems[i].HitPoints; _online[i] = true; }
        }

        public bool IsOnline(ShipSystem system)
        {
            for (int i = 0; i < _profile.Systems.Length; i++)
                if (_profile.Systems[i].System == system) return _online[i];
            return true;
        }

        /// <summary>The nearest system within reach of a ship-frame point, or −1.</summary>
        public int Nearest(Vector3 local, out float distance)
        {
            int nearest = -1;
            distance = _profile.SystemReachM;
            for (int i = 0; i < _profile.Systems.Length; i++)
            {
                float d = Vector3.Distance(local, _profile.Systems[i].PositionShip);
                if (d <= distance) { distance = d; nearest = i; }
            }
            return nearest;
        }

        public string Where(int nearest, float distance) => nearest >= 0
            ? System.FormattableString.Invariant($", {_profile.Systems[nearest].System} hit ({distance:0} m)")
            : ", no system within reach";

        public void Damage(long tick, int nearest, float warheadKg, int hitEvent, in OwnShipState own, string rule, EngagementLog log, List<ShipSystem> newlyOffline)
        {
            if (nearest < 0 || !_online[nearest]) return;
            _hp[nearest] = Mathf.Max(0f, _hp[nearest] - warheadKg * _profile.SystemDamagePerWarheadKg);
            if (_hp[nearest] > 0f) return;
            _online[nearest] = false;
            newlyOffline.Add(_profile.Systems[nearest].System);
            int off = log.Add(tick, EventKind.SystemOffline, rule, $"{_profile.Systems[nearest].System} OFFLINE", -1, hitEvent);
            log.Locate(off, own.ToWorld(_profile.Systems[nearest].PositionShip), WeaponClass.None, (int)_profile.Systems[nearest].System);
        }

        public ulong Fingerprint(ulong h)
        {
            for (int i = 0; i < _hp.Length; i++) h = CombatFix.Mix(CombatFix.Mix(h, _hp[i]), _online[i]);
            return h;
        }
    }

    /// <summary>
    /// Damage Control L0: hit points. A hit removes hull points by warhead mass and damages the nearest
    /// system within reach; a system at zero goes offline; the hull at zero puts the ship out of action.
    /// What it does NOT model: compartments, flooding, list and sinking (L1, <see cref="DamageControlL1"/>);
    /// fire spread and damage-control parties (L2).
    /// </summary>
    public sealed class DamageControlL0 : IDamageControl
    {
        const string Rule = "Damage Control L0 (hit points)";

        readonly DamageProfileData _profile;
        readonly SystemDamage _systems;

        public DamageControlL0(in DamageProfileData profile)
        {
            _profile = profile;
            HullHitPoints = profile.HullHitPoints;
            _systems = new SystemDamage(profile);
        }

        public FidelityLevel Level => FidelityLevel.L0;
        public float HullHitPoints { get; private set; }
        public bool OutOfAction { get; private set; }
        public IFlooding Flooding => null;

        public bool IsOnline(ShipSystem system) => _systems.IsOnline(system);

        public void ApplyHit(long tick, in ShipHit hit, in OwnShipState own, int causeEvent, EngagementLog log, List<ShipSystem> newlyOffline)
        {
            Vector3 local = own.ToShipFrame(hit.Point);
            float hullDamage = hit.WarheadKg * _profile.HullDamagePerWarheadKg;
            HullHitPoints = Mathf.Max(0f, HullHitPoints - hullDamage);

            int nearest = _systems.Nearest(local, out float best);
            int hitEvent = log.Add(tick, EventKind.ShipHit, Rule,
                System.FormattableString.Invariant($"{hit.Kind} hit at {local.z:+0;-0} m from amidships ({hit.WarheadKg:0} kg warhead): hull {HullHitPoints:0}/{_profile.HullHitPoints:0}") + _systems.Where(nearest, best),
                -1, causeEvent, root: RootKind.Scenario);
            log.Locate(hitEvent, hit.Point);

            _systems.Damage(tick, nearest, hit.WarheadKg, hitEvent, own, Rule, log, newlyOffline);

            if (!OutOfAction && HullHitPoints <= 0f)
            {
                OutOfAction = true;
                int sunk = log.Add(tick, EventKind.SystemOffline, Rule, "hull at zero: ship OUT OF ACTION (flooding and sinking are Damage Control L1)", -1, hitEvent);
                log.Locate(sunk, own.Position, WeaponClass.None, (int)ShipSystem.Hull);
            }
        }

        public void Breach(long tick, in Order order, in OwnShipState own, int orderEvent, EngagementLog log) =>
            log.Add(tick, EventKind.Note, Rule, "breach not modelled: Damage Control L0 has no compartments (flooding is L1)", -1, orderEvent);

        public void Step(long tick, float dt, in OwnShipState own, EngagementLog log) { }

        public ulong Fingerprint(ulong h)
        {
            h = CombatFix.Mix(CombatFix.Mix(h, HullHitPoints), OutOfAction);
            return _systems.Fingerprint(h);
        }
    }

    public static class DamageControlFactory
    {
        /// <summary>
        /// Build the element at the requested level. L1 needs the hull's spaces (<paramref name="flooding"/>); asking for
        /// it without them is a loadout error and throws, rather than quietly running L0.
        /// </summary>
        public static IDamageControl Create(FidelityLevel requested, in DamageProfileData profile, in FloodingProfileData flooding,
            out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.DamageControl, requested);
            switch (resolution.Resolved)
            {
                case FidelityLevel.L1:
                    if (!flooding.IsValid)
                        throw new System.ArgumentException("Damage Control L1 needs the hull's spaces: give the loadout its hull profile.");
                    return new DamageControlL1(profile, flooding);
                default: return new DamageControlL0(profile);
            }
        }
    }
}
