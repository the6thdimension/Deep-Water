using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Cic
{
    /// <summary>Where a weapon class can engage: range band and the kinds of target it is for.</summary>
    public struct WeaponBand
    {
        public WeaponClass Weapon;
        public float MinRangeM;
        public float MaxRangeM;
        public bool AntiAir;
        public bool AntiSurface;
        /// <summary>Missiles of this class allowed in flight at one track at once (shoot-look-shoot = 1).</summary>
        public int MaxInFlightPerTrack;

        public bool Covers(TargetKind kind) =>
            kind == TargetKind.SurfaceShip ? AntiSurface : (kind == TargetKind.Aircraft || kind == TargetKind.AntiShipMissile) && AntiAir;

        public bool InRange(float rangeM) => rangeM >= MinRangeM && rangeM <= MaxRangeM;
    }

    public struct TewaProfileData
    {
        /// <summary>Ticks between threat evaluations (TEWA's Watch).</summary>
        public int EvaluatePeriodTicks;
        /// <summary>A track counts as a threat only if its closest approach is nearer than this, m.</summary>
        public float ThreatCpaM;
        /// <summary>Missile weapon bands, in preference order (outer layer first).</summary>
        public WeaponBand[] MissileBands;
    }

    /// <summary>A threat's geometry relative to the own ship.</summary>
    public struct ThreatGeometry
    {
        public float RangeM;
        /// <summary>Seconds to closest approach; negative or infinite when opening.</summary>
        public float TimeToCpaS;
        public float CpaDistanceM;

        public bool Inbound => TimeToCpaS > 0f && !float.IsInfinity(TimeToCpaS);

        public static ThreatGeometry Of(in Track track, in OwnShipState own, long tick, float tickSeconds)
        {
            Vector3 r = track.PredictedPosition(tick, tickSeconds) - own.Position;
            Vector3 v = track.Velocity - own.Velocity;
            float vv = v.sqrMagnitude;
            float t = vv > 1e-6f ? -Vector3.Dot(r, v) / vv : float.PositiveInfinity;
            float cpa = float.IsInfinity(t) || t < 0f ? r.magnitude : (r + v * t).magnitude;
            return new ThreatGeometry { RangeM = r.magnitude, TimeToCpaS = t, CpaDistanceM = cpa };
        }
    }

    /// <summary>A threat in priority order, with the numbers that ranked it.</summary>
    public struct RankedThreat
    {
        public Track Track;
        public ThreatGeometry Geometry;
    }

    /// <summary>A weapon assigned to a track, waiting for fire control.</summary>
    public struct Assignment
    {
        public int TrackId;
        public WeaponClass Weapon;
        public int Rounds;
        /// <summary>The engagement-log event that made the assignment (an Assigned event or an Order).</summary>
        public int CauseEvent;
    }

    /// <summary>The questions TEWA asks the magazine, so it never assigns what can't be fired.</summary>
    public interface IMissileAvailability
    {
        bool CanEngage(WeaponClass weapon);
        int InFlightAt(int trackId, WeaponClass weapon);
    }

    public interface ITewa
    {
        FidelityLevel Level { get; }
        int EvaluatePeriodTicks { get; }

        /// <summary>Rank threats and assign missiles where doctrine and ROE allow; log every decision.</summary>
        void Evaluate(long tick, float tickSeconds, in OwnShipState own, ITrackManager tracks, DoctrineState doctrine,
            IMissileAvailability magazine, EngagementLog log, List<Assignment> assignments);
    }

    /// <summary>
    /// Shared threat ranking: inbound tracks the ROE lets us engage, nearest closest-approach time first,
    /// track id as the deterministic tie-break.
    /// </summary>
    public static class ThreatRanking
    {
        public static void Rank(long tick, float tickSeconds, in OwnShipState own, ITrackManager tracks, DoctrineState doctrine,
            float threatCpaM, bool selfDefence, List<RankedThreat> output)
        {
            output.Clear();
            IReadOnlyList<Track> all = tracks.Tracks;
            for (int i = 0; i < all.Count; i++)
            {
                Track t = all[i];
                if (!t.Active || t.Destroyed) continue;
                if (!doctrine.RoePermits(t.Identity, selfDefence)) continue;
                ThreatGeometry g = ThreatGeometry.Of(t, own, tick, tickSeconds);
                if (!g.Inbound || g.CpaDistanceM > threatCpaM) continue;
                output.Add(new RankedThreat { Track = t, Geometry = g });
            }
            output.Sort((a, b) =>
            {
                int c = a.Geometry.TimeToCpaS.CompareTo(b.Geometry.TimeToCpaS);
                return c != 0 ? c : a.Track.Id.CompareTo(b.Track.Id);
            });
        }
    }

    /// <summary>
    /// TEWA L0: rank inbound threats by time to closest approach; give each the outermost missile layer
    /// whose band contains it, one missile at a time (shoot-look-shoot), as doctrine allows. Guns choose
    /// their own targets under doctrine (gun-mount local fire control). What it does NOT model (L1+):
    /// kill-probability-weighted pairing, salvo doctrine, inventory optimisation.
    /// </summary>
    public sealed class TewaL0 : ITewa
    {
        readonly TewaProfileData _profile;
        readonly List<RankedThreat> _ranked = new List<RankedThreat>();
        readonly HashSet<long> _proposed = new HashSet<long>();

        public TewaL0(in TewaProfileData profile) => _profile = profile;

        public FidelityLevel Level => FidelityLevel.L0;
        public int EvaluatePeriodTicks => Mathf.Max(1, _profile.EvaluatePeriodTicks);

        public void Evaluate(long tick, float tickSeconds, in OwnShipState own, ITrackManager tracks, DoctrineState doctrine,
            IMissileAvailability magazine, EngagementLog log, List<Assignment> assignments)
        {
            ThreatRanking.Rank(tick, tickSeconds, own, tracks, doctrine, _profile.ThreatCpaM, false, _ranked);
            for (int i = 0; i < _ranked.Count; i++)
            {
                RankedThreat threat = _ranked[i];
                for (int b = 0; b < _profile.MissileBands.Length; b++)
                {
                    WeaponBand band = _profile.MissileBands[b];
                    if (!band.Covers(threat.Track.Kind) || !band.InRange(threat.Geometry.RangeM)) continue;
                    if (magazine.InFlightAt(threat.Track.Id, band.Weapon) >= band.MaxInFlightPerTrack) break; // covered by this layer
                    if (!magazine.CanEngage(band.Weapon)) continue;                                            // next layer

                    DoctrineMode mode = doctrine.Mode(band.Weapon);
                    if (mode == DoctrineMode.Manual) break;

                    string text = System.FormattableString.Invariant(
                        $"track {threat.Track.Id} ({threat.Track.Identity} {threat.Track.Kind}, CPA in {threat.Geometry.TimeToCpaS:0.0} s at {threat.Geometry.CpaDistanceM:0} m, range {threat.Geometry.RangeM / 1000f:0.0} km) → {band.Weapon} (band {band.MinRangeM / 1000f:0.0}–{band.MaxRangeM / 1000f:0.0} km); doctrine {mode}, ROE {doctrine.Roe}");

                    if (mode == DoctrineMode.SemiAuto)
                    {
                        long key = ((long)threat.Track.Id << 8) | (byte)band.Weapon;
                        if (_proposed.Add(key))
                            log.Add(tick, EventKind.Proposed, "TEWA L0 (CPA rank, range bands)", "proposed: " + text,
                                threat.Track.Id, threat.Track.CreatedEvent, doctrine.ModeEvent(band.Weapon), causeC: doctrine.RoeEvent);
                        break;
                    }

                    int ev = log.Add(tick, EventKind.Assigned, "TEWA L0 (CPA rank, range bands)", text,
                        threat.Track.Id, threat.Track.CreatedEvent, doctrine.ModeEvent(band.Weapon), causeC: doctrine.RoeEvent);
                    assignments.Add(new Assignment { TrackId = threat.Track.Id, Weapon = band.Weapon, Rounds = 1, CauseEvent = ev });
                    break;
                }
            }
        }
    }

    public static class TewaFactory
    {
        public static ITewa Create(FidelityLevel requested, in TewaProfileData profile, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.Tewa, requested);
            switch (resolution.Resolved)
            {
                default: return new TewaL0(profile);
            }
        }
    }
}
