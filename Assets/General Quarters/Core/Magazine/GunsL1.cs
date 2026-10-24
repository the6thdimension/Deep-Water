using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Ballistics;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Magazine
{
    /// <summary>How a round decides it has reached its target (G39).</summary>
    public enum FuzeKind : byte
    {
        /// <summary>Kills only by hitting: the target's presented area.</summary>
        Impact = 0,
        /// <summary>Bursts within a lethal radius of an air target; impact against surface targets (Mk 45 HE-CVT).</summary>
        ProximityVsAir = 1,
    }

    /// <summary>Guns L1's data for one mount (G35–G41). Every number's source is in the loadout's provenance.</summary>
    [System.Serializable]
    public struct GunBallisticsData
    {
        public ProjectileData Projectile;
        public float ElevationMinDeg, ElevationMaxDeg;
        public float TrainRateDps, ElevateRateDps;
        /// <summary>The mount fires only once train and elevation are both within this of the solution, degrees.</summary>
        public float LayToleranceDeg;
        /// <summary>Round-to-round dispersion: σ of the circular normal pattern, milliradians.</summary>
        public float DispersionMrad;
        /// <summary>Age of the fire-control solution when the rounds leave; the ship's angular motion over it is aim error (G37), s.</summary>
        public float SolutionLatencyS;
        public FuzeKind Fuze;
        /// <summary>Proximity fuze: the distance inside which a burst kills, m, and the chance the fuze functions.</summary>
        public float LethalRadiusM, FuzeReliability;
        /// <summary>Chance that one hit kills, against air and surface targets.</summary>
        public float PkPerHitAir, PkPerHitSurface;
        /// <summary>Closed-loop spotting (Phalanx, G40): the aim bias a new target starts with, mrad, and the loop's time constant, s.</summary>
        public bool ClosedLoopSpotting;
        public float SpotBiasMrad, SpotTimeConstantS;
    }

    /// <summary>
    /// Guns L1 (G35–G41): ballistic fire control, a mount that slews, and a shot-group hit model.
    /// - Fire control looks the aim point up in the projectile's firing table (3-DOF point mass, G7 drag, ISA),
    ///   leading the track's constant-velocity prediction by the ballistic time of flight.
    /// - The mount trains and elevates toward the solution at its rates and fires only once laid.
    /// - At impact, the miss distance is the aim error (where the target really is against where it was predicted),
    ///   the ship's angular motion over the solution latency, and any spotting bias. With the dispersion it gives a
    ///   per-round hit probability against the target's presented area, or the lethal radius of a proximity fuze.
    ///   One seeded draw per burst decides it, and the log prints the sum.
    /// Not modelled (L2): fire-control motion prediction, track noise, wind, the shell inheriting the ship's velocity.
    /// </summary>
    public sealed class GunMountL1 : GunMountBase, IGunMount
    {
        const string Rule = "Guns L1 (ballistic shot group)";
        const int LeadIterations = 4;

        struct Burst
        {
            public int TrackId, TargetTruthId, FiredEvent, Mount, Rounds;
            public long ImpactTick;
            public Vector3 AimPoint, LineOfFire;
            public float RangeM, SigmaM, MotionM, BiasM;
        }

        readonly FiringTable _table;
        readonly List<Burst> _bursts = new List<Burst>();
        long _readyTick;
        float _trainDeg, _elevationDeg;   // where the mount points: train from its rest facing, elevation above the horizontal
        int _biasTrack = -1;
        float _biasMrad;

        // The solution for the last track CanEngage accepted.
        float _solTrainDeg, _solElevationDeg, _solTofS;
        Vector3 _solAim;

        public GunMountL1(in GunMountProfileData profile) : base(profile)
        {
            GunBallisticsData b = profile.Ballistics;
            _table = FiringTable.For(b.Projectile, b.ElevationMinDeg, b.ElevationMaxDeg);
        }

        public FidelityLevel Level => FidelityLevel.L1;
        public float TrainDeg => _trainDeg;
        public float ElevationDeg => _elevationDeg;
        public FiringTable Table => _table;

        public void Step(EngagementContext ctx, List<DirectedEngagement> directed)
        {
            ResolveArrivals(ctx);
            if (!Online) return;
            GunBallisticsData b = _profile.Ballistics;
            float dt = ctx.TickSeconds;

            if (!TryPickTarget(ctx, directed, out Track track, out int cause, out int directedIndex, out float range))
            {
                Slew(0f, 0f, b, dt);   // nothing to shoot: back to rest
                return;
            }
            Slew(_solTrainDeg, _solElevationDeg, b, dt);
            bool laid = Mathf.Abs(_trainDeg - _solTrainDeg) <= b.LayToleranceDeg && Mathf.Abs(_elevationDeg - _solElevationDeg) <= b.LayToleranceDeg;
            if (!laid || ctx.Tick < _readyTick || Rounds < _profile.BurstRounds) return;

            int n = _profile.BurstRounds;
            float burstS = n / Mathf.Max(1e-3f, _profile.RoundsPerMinute) * 60f;
            _readyTick = ctx.Tick + Mathf.Max(1, Mathf.RoundToInt(burstS / dt));
            Rounds -= n;

            // Ship motion over the solution latency (G37), and Phalanx's spotting bias (G40), both as miss at the target.
            float omegaRad = Mathf.Sqrt(ctx.Own.RollRateDps * ctx.Own.RollRateDps + ctx.Own.PitchRateDps * ctx.Own.PitchRateDps) * Mathf.Deg2Rad;
            float motionM = omegaRad * b.SolutionLatencyS * range;
            float biasM = 0f, biasMrad = 0f;
            if (b.ClosedLoopSpotting)
            {
                if (_biasTrack != track.Id) { _biasTrack = track.Id; _biasMrad = b.SpotBiasMrad; }
                ShotGroup.SpottingBias(_biasMrad, burstS, b.SpotTimeConstantS, out float meanMrad, out float leftMrad);
                biasMrad = meanMrad;
                biasM = meanMrad * 1e-3f * range;
                _biasMrad = leftMrad;
            }
            float sigmaM = b.DispersionMrad * 1e-3f * range;
            long impact = ctx.Tick + Mathf.Max(1, Mathf.RoundToInt(_solTofS / dt));
            Vector3 mount = ctx.Own.ToWorld(_profile.OffsetShip);
            Vector3 lineOfFire = (_solAim - mount).normalized;

            int fired = ctx.Log.Add(ctx.Tick, EventKind.BurstFired, Rule,
                System.FormattableString.Invariant($"{_profile.Name} fired {n} round{(n == 1 ? "" : "s")} at track {track.Id}: {range:0} m, elevation {_solElevationDeg:0.0}°, time of flight {_solTofS:0.00} s; σ {sigmaM:0.0} m, ship motion {motionM:0.0} m, spotting bias {biasM:0.0} m ({biasMrad:0.000} mrad); {Rounds} left"),
                track.Id, track.CreatedEvent, cause, causeC: ctx.Doctrine.RoeEvent);
            ctx.Log.Locate(fired, _solAim, _profile.Weapon, ctx.Mount);
            _bursts.Add(new Burst
            {
                TrackId = track.Id, TargetTruthId = track.TruthId, FiredEvent = fired, Mount = ctx.Mount, Rounds = n, ImpactTick = impact,
                AimPoint = _solAim, LineOfFire = lineOfFire, RangeM = range, SigmaM = sigmaM, MotionM = motionM, BiasM = biasM,
            });
            SpendDirected(directed, directedIndex);
        }

        void Slew(float trainDeg, float elevationDeg, in GunBallisticsData b, float dt)
        {
            _trainDeg = Mathf.MoveTowards(_trainDeg, trainDeg, b.TrainRateDps * dt);
            _elevationDeg = Mathf.MoveTowards(_elevationDeg, Mathf.Clamp(elevationDeg, b.ElevationMinDeg, b.ElevationMaxDeg), b.ElevateRateDps * dt);
        }

        protected override bool CanEngage(EngagementContext ctx, in Track track, out float range)
        {
            range = 0f;
            if (!KindOk(track)) return false;
            Vector3 mount = ctx.Own.ToWorld(_profile.OffsetShip);
            Vector3 now = track.PredictedPosition(ctx.Tick, ctx.TickSeconds);
            // Cheap rejection before the lead iteration: nothing beyond 1.5 times the mount's range closes inside it
            // within one time of flight (a 300 m/s threat covers 750 m in a CIWS shell's 2.5 s to 2 km).
            if (Vector3.Distance(mount, now) > 1.5f * _profile.MaxRangeM) return false;

            // Ballistic lead (G38): aim where the track will be after the shell's time of flight, and iterate.
            float tof = Vector3.Distance(mount, now) / Mathf.Max(1f, _profile.Ballistics.Projectile.MuzzleVelocityMps);
            Vector3 aim = now;
            float elevation = 0f;
            for (int i = 0; i < LeadIterations; i++)
            {
                aim = now + track.Velocity * tof;
                Vector3 rel = aim - mount;
                if (!_table.TrySolve(new Vector2(rel.x, rel.z).magnitude, rel.y, out elevation, out tof)) return false;
            }
            // Rounds arrive on a tick: aim where the track will be on that tick, so the rounding is not counted as aim error.
            tof = Mathf.Max(1, Mathf.RoundToInt(tof / ctx.TickSeconds)) * ctx.TickSeconds;
            aim = now + track.Velocity * tof;
            range = Vector3.Distance(mount, aim);
            if (range < _profile.MinRangeM || range > _profile.MaxRangeM) return false;
            float train = Mathf.DeltaAngle(_profile.FacingDeg, ctx.Own.RelativeBearingDeg(aim));
            if (Mathf.Abs(train) > _profile.ArcHalfDeg) return false;

            _solTrainDeg = train;
            _solElevationDeg = elevation;
            _solTofS = tof;
            _solAim = aim;
            return true;
        }

        void ResolveArrivals(EngagementContext ctx)
        {
            GunBallisticsData b = _profile.Ballistics;
            for (int i = 0; i < _bursts.Count; i++)
            {
                Burst s = _bursts[i];
                if (s.ImpactTick > ctx.Tick) continue;
                _bursts.RemoveAt(i--);
                if (!ctx.TargetAlive(s.TargetTruthId) || !ctx.TryTruth(s.TargetTruthId, out TruthTarget target))
                {
                    int wasted = ctx.Log.Add(ctx.Tick, EventKind.Note, Rule, $"{_profile.Name} rounds at track {s.TrackId} wasted: target already destroyed", s.TrackId, s.FiredEvent);
                    ctx.Log.Locate(wasted, s.AimPoint, _profile.Weapon, s.Mount);
                    continue;
                }

                // Where the target really is, against where fire control predicted it, across the line of fire.
                Vector3 err = target.Position - s.AimPoint;
                float aimM = (err - Vector3.Dot(err, s.LineOfFire) * s.LineOfFire).magnitude;
                float missM = Mathf.Sqrt(aimM * aimM + s.MotionM * s.MotionM + s.BiasM * s.BiasM);

                bool air = target.Kind != TargetKind.SurfaceShip;
                float radiusM, pkPerRound;
                string what;
                if (air && b.Fuze == FuzeKind.ProximityVsAir)
                {
                    radiusM = b.LethalRadiusM;
                    pkPerRound = b.FuzeReliability;
                    what = System.FormattableString.Invariant($"lethal radius {radiusM:0.0} m, fuze {pkPerRound:0.00}");
                }
                else
                {
                    TargetSize.Of(target, out float len, out float dia);
                    radiusM = ShotGroup.PresentedRadiusM(len, dia, target.Velocity, s.LineOfFire);
                    pkPerRound = air ? b.PkPerHitAir : b.PkPerHitSurface;
                    what = System.FormattableString.Invariant($"presented radius {radiusM:0.00} m, Pk per hit {pkPerRound:0.00}");
                }
                float pRound = ShotGroup.RoundHitProbability(radiusM, s.SigmaM, missM);
                float pk = ShotGroup.BurstKillProbability(pRound, pkPerRound, s.Rounds);

                bool kill = ctx.Rng.Chance(pk, out ChanceRoot root);
                int ev = ctx.Log.Add(ctx.Tick, kill ? EventKind.Kill : EventKind.Miss, Rule,
                    System.FormattableString.Invariant($"{_profile.Name} rounds arrived at track {s.TrackId} ({s.RangeM:0} m): miss {missM:0.0} m (aim {aimM:0.0}, motion {s.MotionM:0.0}, bias {s.BiasM:0.0}), σ {s.SigmaM:0.0} m, {what} → {pRound:0.000} a round × {s.Rounds} → Pk {pk:0.00} → {(kill ? "KILL" : "survived")}"),
                    s.TrackId, s.FiredEvent, root: RootKind.Chance, chance: root);
                ctx.Log.Locate(ev, target.Position, _profile.Weapon, s.Mount);
                if (kill) ctx.RecordKill(s.TargetTruthId, s.TrackId, _profile.Weapon, ev);
            }
        }

        public ulong Fingerprint(ulong h)
        {
            h = CombatFix.Mix(CombatFix.Mix(CombatFix.Mix(h, Rounds), _readyTick), Online);
            h = CombatFix.Mix(CombatFix.Mix(CombatFix.Mix(h, _trainDeg), _elevationDeg), _biasMrad);
            for (int i = 0; i < _bursts.Count; i++)
                h = CombatFix.Mix(CombatFix.Mix(h, _bursts[i].TrackId), _bursts[i].ImpactTick);
            return h;
        }
    }
}
