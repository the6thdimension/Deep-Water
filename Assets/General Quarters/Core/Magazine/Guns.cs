using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Magazine
{
    /// <summary>One gun mount's tuning (P1). Pk numbers are AUTHORED unless the loadout cites a source.</summary>
    [System.Serializable]
    public struct GunMountProfileData
    {
        public string Name;
        public WeaponClass Weapon;
        public ShipSystem System;
        /// <summary>Mount position in the ship frame, m.</summary>
        public Vector3 OffsetShip;
        /// <summary>The direction the mount faces at rest, relative to the bow, degrees (180 = aft).</summary>
        public float FacingDeg;
        /// <summary>Half-width of the training arc around <see cref="FacingDeg"/>, degrees.</summary>
        public float ArcHalfDeg;
        public float MinRangeM;
        public float MaxRangeM;
        public bool AntiAir;
        public bool AntiSurface;
        /// <summary>True for a last-ditch self-defence weapon (CIWS): it may fire under WeaponsHold.</summary>
        public bool SelfDefence;
        /// <summary>A target counts as a threat to this mount only if its closest approach is nearer than this, m.</summary>
        public float ThreatCpaM;
        public float RoundsPerMinute;
        public int BurstRounds;
        public float MuzzleVelocityMps;
        /// <summary>Mean projectile speed over the flight as a fraction of muzzle velocity (drag). L0 time-of-flight only.</summary>
        public float MeanVelocityFraction;
        public int MagazineRounds;
        public float PkPerBurstAtMinRange;
        public float PkPerBurstAtMaxRange;
    }

    /// <summary>A TAO-directed engagement for guns (an EngageOrder), held until rounds run out or cease fire.</summary>
    public struct DirectedEngagement
    {
        public int TrackId;
        public WeaponClass Weapon;
        /// <summary>Bursts still to fire; -1 = until cease fire.</summary>
        public int BurstsRemaining;
        public int CauseEvent;
    }

    public interface IGunMount
    {
        FidelityLevel Level { get; }
        GunMountProfileData Profile { get; }
        bool Online { get; set; }
        int Rounds { get; }
        void Step(EngagementContext ctx, List<DirectedEngagement> directed);
        ulong Fingerprint(ulong h);
    }

    /// <summary>
    /// Guns L0: a burst model. A mount that is ready, online, in arc and in range fires a burst; the burst
    /// arrives after the time of flight for the firing range and kills with a seeded Pk that falls linearly
    /// from minimum to maximum range. Doctrine Auto lets the mount pick the most urgent threat itself
    /// (local fire control); otherwise it fires only on directed engagements. What it does NOT model (L1+):
    /// ballistics, drag tables, dispersion, aiming lead, mount slew, the ship's roll.
    /// </summary>
    public sealed class GunMountL0 : IGunMount
    {
        const string Rule = "Guns L0 (burst Pk by range)";

        struct Burst
        {
            public int TrackId;
            public int TargetTruthId;
            public long ImpactTick;
            public float Pk;
            public float RangeM;
            public int FiredEvent;
            /// <summary>Where the burst was aimed: the track's predicted position at impact.</summary>
            public Vector3 AimPoint;
            public int Mount;
        }

        readonly GunMountProfileData _profile;
        readonly List<RankedThreat> _ranked = new List<RankedThreat>();
        readonly List<Burst> _bursts = new List<Burst>();
        long _readyTick;

        public GunMountL0(in GunMountProfileData profile)
        {
            _profile = profile;
            Rounds = profile.MagazineRounds;
            Online = true;
        }

        public FidelityLevel Level => FidelityLevel.L0;
        public GunMountProfileData Profile => _profile;
        public bool Online { get; set; }
        public int Rounds { get; private set; }

        public void Step(EngagementContext ctx, List<DirectedEngagement> directed)
        {
            ResolveArrivals(ctx);
            if (!Online || ctx.Tick < _readyTick || Rounds < _profile.BurstRounds) return;

            if (!TryPickTarget(ctx, directed, out Track track, out int cause, out int directedIndex, out float range)) return;

            float span = Mathf.Max(1f, _profile.MaxRangeM - _profile.MinRangeM);
            float pk = Mathf.Lerp(_profile.PkPerBurstAtMinRange, _profile.PkPerBurstAtMaxRange, Mathf.Clamp01((range - _profile.MinRangeM) / span));
            float tof = range / Mathf.Max(1f, _profile.MuzzleVelocityMps * _profile.MeanVelocityFraction);
            long impact = ctx.Tick + Mathf.Max(1, Mathf.RoundToInt(tof / ctx.TickSeconds));
            float cycle = _profile.BurstRounds / Mathf.Max(1e-3f, _profile.RoundsPerMinute) * 60f;
            _readyTick = ctx.Tick + Mathf.Max(1, Mathf.RoundToInt(cycle / ctx.TickSeconds));
            Rounds -= _profile.BurstRounds;

            Vector3 aimPoint = track.PredictedPosition(impact, ctx.TickSeconds);
            int fired = ctx.Log.Add(ctx.Tick, EventKind.BurstFired, Rule,
                System.FormattableString.Invariant($"{_profile.Name} fired {_profile.BurstRounds} rounds at track {track.Id} ({range:0} m, time of flight {tof:0.0} s, Pk {pk:0.00}); {Rounds} left"),
                track.Id, track.CreatedEvent, cause, causeC: ctx.Doctrine.RoeEvent);
            ctx.Log.Locate(fired, aimPoint, _profile.Weapon, ctx.Mount);
            _bursts.Add(new Burst
            {
                TrackId = track.Id, TargetTruthId = track.TruthId, ImpactTick = impact, Pk = pk, RangeM = range, FiredEvent = fired,
                AimPoint = aimPoint, Mount = ctx.Mount,
            });

            if (directedIndex >= 0)
            {
                DirectedEngagement d = directed[directedIndex];
                if (d.BurstsRemaining > 0) d.BurstsRemaining--;
                if (d.BurstsRemaining == 0) directed.RemoveAt(directedIndex);
                else directed[directedIndex] = d;
            }
        }

        void ResolveArrivals(EngagementContext ctx)
        {
            for (int i = 0; i < _bursts.Count; i++)
            {
                Burst b = _bursts[i];
                if (b.ImpactTick > ctx.Tick) continue;
                if (!ctx.TargetAlive(b.TargetTruthId))
                {
                    int wasted = ctx.Log.Add(ctx.Tick, EventKind.Note, Rule, $"{_profile.Name} burst at track {b.TrackId} wasted: target already destroyed", b.TrackId, b.FiredEvent);
                    ctx.Log.Locate(wasted, b.AimPoint, _profile.Weapon, b.Mount);
                }
                else
                {
                    bool kill = ctx.Rng.Chance(b.Pk, out ChanceRoot root);
                    int ev = ctx.Log.Add(ctx.Tick, kill ? EventKind.Kill : EventKind.Miss, Rule,
                        System.FormattableString.Invariant($"{_profile.Name} burst arrived at track {b.TrackId} ({b.RangeM:0} m); Pk {b.Pk:0.00} → {(kill ? "KILL" : "survived")}"),
                        b.TrackId, b.FiredEvent, root: RootKind.Chance, chance: root);
                    ctx.Log.Locate(ev, ctx.TryTruth(b.TargetTruthId, out TruthTarget target) ? target.Position : b.AimPoint, _profile.Weapon, b.Mount);
                    if (kill) ctx.RecordKill(b.TargetTruthId, b.TrackId, _profile.Weapon, ev);
                }
                _bursts.RemoveAt(i--);
            }
        }

        bool TryPickTarget(EngagementContext ctx, List<DirectedEngagement> directed, out Track track, out int cause, out int directedIndex, out float range)
        {
            // A directed engagement (TAO order) comes first, in issue order.
            for (int i = 0; i < directed.Count; i++)
            {
                DirectedEngagement d = directed[i];
                if (d.Weapon != _profile.Weapon) continue;
                if (ctx.Tracks.TryGet(d.TrackId, out track) && track.Active && !track.Destroyed && CanEngage(ctx, track, out range))
                {
                    cause = d.CauseEvent;
                    directedIndex = i;
                    return true;
                }
            }

            directedIndex = -1;
            if (ctx.Doctrine.Mode(_profile.Weapon) == DoctrineMode.Auto)
            {
                ThreatRanking.Rank(ctx.Tick, ctx.TickSeconds, ctx.Own, ctx.Tracks, ctx.Doctrine, _profile.ThreatCpaM, _profile.SelfDefence, _ranked);
                for (int i = 0; i < _ranked.Count; i++)
                {
                    track = _ranked[i].Track;
                    if (CanEngage(ctx, track, out range))
                    {
                        cause = ctx.Doctrine.ModeEvent(_profile.Weapon);
                        return true;
                    }
                }
            }
            track = default;
            cause = -1;
            range = 0f;
            return false;
        }

        bool CanEngage(EngagementContext ctx, in Track track, out float range)
        {
            Vector3 aim = track.PredictedPosition(ctx.Tick, ctx.TickSeconds);
            Vector3 mount = ctx.Own.ToWorld(_profile.OffsetShip);
            range = Vector3.Distance(mount, aim);
            if (range < _profile.MinRangeM || range > _profile.MaxRangeM) return false;
            bool kindOk = track.Kind == TargetKind.SurfaceShip ? _profile.AntiSurface : _profile.AntiAir;
            if (!kindOk) return false;
            float bearing = ctx.Own.RelativeBearingDeg(aim);
            return Mathf.Abs(Mathf.DeltaAngle(_profile.FacingDeg, bearing)) <= _profile.ArcHalfDeg;
        }

        public ulong Fingerprint(ulong h)
        {
            h = CombatFix.Mix(CombatFix.Mix(CombatFix.Mix(h, Rounds), _readyTick), Online);
            for (int i = 0; i < _bursts.Count; i++)
                h = CombatFix.Mix(CombatFix.Mix(h, _bursts[i].TrackId), _bursts[i].ImpactTick);
            return h;
        }
    }

    public static class GunFactory
    {
        public static IGunMount Create(FidelityLevel requested, in GunMountProfileData profile, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.Guns, requested);
            switch (resolution.Resolved)
            {
                default: return new GunMountL0(profile);
            }
        }
    }
}
