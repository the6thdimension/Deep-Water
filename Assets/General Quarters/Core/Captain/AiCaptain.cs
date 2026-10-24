using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Captain
{
    /// <summary>The two seats a player can hold (G3). The AI captain fills whichever one is empty.</summary>
    public enum Seat : byte
    {
        Captain = 0,
        Tao = 1,
    }

    /// <summary>The AI captain's tuning (AUTHORED: these are its habits, not ship data).</summary>
    [Serializable]
    public struct AiCaptainProfileData
    {
        /// <summary>Ticks between the AI's looks at the picture (its Watch).</summary>
        public int EvaluatePeriodTicks;

        // Captain's rules
        /// <summary>C1: the route it follows when nothing threatens (the range registers it on the Helm under this id).</summary>
        public int RouteId;
        /// <summary>C1: speed on the route, knots.</summary>
        public float RouteSpeedKn;
        /// <summary>C2: the bell it rings when the first hostile missile is tracked.</summary>
        public EngineBell ThreatBell;
        /// <summary>C3: candidate courses are the current course ± this step, degrees.</summary>
        public float ArcHeadingStepDeg;
        /// <summary>C3: how many steps each way it looks (so the widest turn is steps × step).</summary>
        public int ArcHeadingSteps;
        /// <summary>C3: a turn must bring at least this many more (threat, mount) pairs into arc.</summary>
        public int ArcMinGain;
        /// <summary>C3: seconds between arc turns, so it doesn't chase every track.</summary>
        public float ArcTurnIntervalS;
        /// <summary>C4: seconds with no inbound hostile before it secures and goes back to the route.</summary>
        public float SecureAfterS;

        // TAO's rules
        /// <summary>T1: seconds a SemiAuto proposal stands before the AI confirms it.</summary>
        public float ConfirmDelayS;
    }

    /// <summary>
    /// The AI captain (G19): it holds the seat the player left, and issues that seat's Orders, the same Orders a
    /// player gives. Every decision is a named rule written to the engagement log with its numbers.
    /// </summary>
    public interface IAiCaptain
    {
        FidelityLevel Level { get; }
        /// <summary>Look at the picture and give orders for <paramref name="seat"/>. Called once per tick, before the combat step.</summary>
        void Step(CombatSystem combat, in OwnShipState own, Seat seat);
    }

    /// <summary>
    /// AI captain L0: named, recorded rules.
    /// <list type="bullet">
    /// <item><b>C1 Route:</b> with nothing inbound, follow the range route at the route speed.</item>
    /// <item><b>C2 Flank:</b> when the first inbound hostile missile is tracked, ring up the threat bell.</item>
    /// <item><b>C3 Open arcs:</b> while threats are inbound, steer the course that puts the most (threat, gun mount)
    /// pairs inside the mounts' training arcs, if it gains enough and the last arc turn was long enough ago.</item>
    /// <item><b>C4 Secure:</b> when nothing has been inbound for a while, go back to the route (C1).</item>
    /// <item><b>T1 Confirm:</b> as TAO, confirm a SemiAuto proposal that has stood for the confirm delay.</item>
    /// </list>
    /// What it does NOT do (L1, ML-Agents): weigh kill probabilities, manage the magazine, decoys, or learn.
    /// </summary>
    public sealed class AiCaptainL0 : IAiCaptain
    {
        enum Posture : byte { Unknown = 0, Patrol = 1, Threat = 2 }

        const string RouteRule = "AI captain C1 (route)";
        const string FlankRule = "AI captain C2 (flank on first threat)";
        const string ArcRule = "AI captain C3 (open arcs)";
        const string SecureRule = "AI captain C4 (secure)";
        const string ConfirmRule = "AI captain T1 (confirm proposals)";

        readonly AiCaptainProfileData _p;
        readonly List<RankedThreat> _threats = new List<RankedThreat>();
        // T1: since when each standing proposal (track, weapon) has waited; reset when the AI confirms it.
        readonly Dictionary<long, long> _proposalSince = new Dictionary<long, long>();
        readonly HashSet<long> _standing = new HashSet<long>();
        readonly List<long> _stale = new List<long>();

        Seat _seat = (Seat)255;
        Posture _posture;
        long _lastThreatTick = long.MinValue, _lastArcTurnTick = long.MinValue;

        public AiCaptainL0(in AiCaptainProfileData profile) => _p = profile;

        public FidelityLevel Level => FidelityLevel.L0;

        public void Step(CombatSystem combat, in OwnShipState own, Seat seat)
        {
            long tick = combat.Tick;
            if (seat != _seat)
            {
                // Taking a new seat: look at the picture fresh.
                _seat = seat;
                _posture = Posture.Unknown;
                _proposalSince.Clear();
            }
            if (tick % Mathf.Max(1, _p.EvaluatePeriodTicks) != 0) return;
            if (seat == Seat.Captain) StepCaptain(combat, own, tick);
            else StepTao(combat, tick);
        }

        void StepCaptain(CombatSystem combat, in OwnShipState own, long tick)
        {
            float dt = combat.TickSeconds;
            InboundHostiles(combat, own, _threats);
            if (_threats.Count > 0) _lastThreatTick = tick;

            if (_threats.Count > 0 && _posture != Posture.Threat)
            {
                _posture = Posture.Threat;
                RankedThreat first = _threats[0];
                Decide(combat, tick, FlankRule,
                    System.FormattableString.Invariant($"{_threats.Count} inbound hostile (first: track {first.Track.Id}, {first.Track.Kind}, {first.Geometry.RangeM / 1000f:0.0} km, CPA in {first.Geometry.TimeToCpaS:0} s) → ring {_p.ThreatBell}"),
                    first.Track.Id, first.Track.CreatedEvent, Order.EngineOrder(OrderIssuer.AiCaptain, _p.ThreatBell));
            }

            if (_posture == Posture.Threat)
            {
                bool quiet = _threats.Count == 0 && (tick - _lastThreatTick) * dt >= _p.SecureAfterS;
                if (quiet)
                {
                    _posture = Posture.Patrol;
                    Decide(combat, tick, SecureRule,
                        System.FormattableString.Invariant($"nothing inbound for {_p.SecureAfterS:0} s → follow route {_p.RouteId} at {_p.RouteSpeedKn:0} kn"),
                        -1, -1, Order.FollowRoute(OrderIssuer.AiCaptain, _p.RouteId, _p.RouteSpeedKn));
                    return;
                }
                if (_threats.Count > 0 && (_lastArcTurnTick == long.MinValue || (tick - _lastArcTurnTick) * dt >= _p.ArcTurnIntervalS))
                    OpenArcs(combat, own, tick);
                return;
            }

            // Patrol (or a fresh look with nothing inbound): be on the route.
            _posture = Posture.Patrol;
            HelmCommand helm = combat.Helm;
            if (helm.Course != CourseMode.Route || helm.RouteId != _p.RouteId)
                Decide(combat, tick, RouteRule,
                    System.FormattableString.Invariant($"nothing inbound → follow route {_p.RouteId} at {_p.RouteSpeedKn:0} kn"),
                    -1, -1, Order.FollowRoute(OrderIssuer.AiCaptain, _p.RouteId, _p.RouteSpeedKn));
        }

        void OpenArcs(CombatSystem combat, in OwnShipState own, long tick)
        {
            HelmCommand helm = combat.Helm;
            float current = helm.Course == CourseMode.Hold ? helm.CourseDeg : own.HeadingDeg;
            int now = ArcMath.PairsInArc(_threats, own, current, combat.Guns);
            float best = current;
            int bestScore = now;
            // Smallest turns first, starboard before port, so ties keep the gentlest turn.
            for (int k = 1; k <= _p.ArcHeadingSteps; k++)
                for (int side = 1; side >= -1; side -= 2)
                {
                    float course = CompassMath.Normalize(current + side * k * _p.ArcHeadingStepDeg);
                    int score = ArcMath.PairsInArc(_threats, own, course, combat.Guns);
                    if (score > bestScore) { bestScore = score; best = course; }
                }
            if (bestScore - now < Mathf.Max(1, _p.ArcMinGain)) return;
            _lastArcTurnTick = tick;
            RankedThreat first = _threats[0];
            Decide(combat, tick, ArcRule,
                System.FormattableString.Invariant($"{_threats.Count} inbound; course {current:000} has {now} (threat, mount) pairs in arc, {best:000} has {bestScore} → steer {best:000}"),
                first.Track.Id, first.Track.CreatedEvent, Order.SteerCourse(OrderIssuer.AiCaptain, best));
        }

        void StepTao(CombatSystem combat, long tick)
        {
            // A proposal disappears once a missile is in flight at the track; one that comes back (after a miss)
            // waits the full delay again. One that keeps standing after a confirm (the launch couldn't happen
            // yet) is confirmed again after another delay.
            IReadOnlyList<Assignment> proposals = combat.Tewa.Proposals;
            _standing.Clear();
            for (int i = 0; i < proposals.Count; i++)
            {
                Assignment a = proposals[i];
                long key = ((long)a.TrackId << 8) | (byte)a.Weapon;
                _standing.Add(key);
                if (!_proposalSince.TryGetValue(key, out long since)) { _proposalSince[key] = tick; since = tick; }
                if ((tick - since) * combat.TickSeconds < _p.ConfirmDelayS) continue;
                _proposalSince[key] = tick;
                Decide(combat, tick, ConfirmRule,
                    System.FormattableString.Invariant($"proposal stood {(tick - since) * combat.TickSeconds:0.0} s: engage track {a.TrackId} with {a.Weapon}"),
                    a.TrackId, a.CauseEvent, Order.Engage(OrderIssuer.AiCaptain, a.TrackId, a.Weapon, 1));
            }
            _stale.Clear();
            foreach (long key in _proposalSince.Keys) if (!_standing.Contains(key)) _stale.Add(key);
            for (int i = 0; i < _stale.Count; i++) _proposalSince.Remove(_stale[i]);
        }

        static void Decide(CombatSystem combat, long tick, string rule, string text, int trackId, int cause, in Order order)
        {
            combat.Log.Add(tick, EventKind.Decision, rule, text, trackId, cause, root: cause < 0 ? RootKind.Scenario : RootKind.None);
            combat.Issue(order);
        }

        /// <summary>Inbound hostile tracks (missiles and aircraft), most urgent first.</summary>
        static void InboundHostiles(CombatSystem combat, in OwnShipState own, List<RankedThreat> output)
        {
            output.Clear();
            IReadOnlyList<Track> all = combat.Tracks.Tracks;
            for (int i = 0; i < all.Count; i++)
            {
                Track t = all[i];
                if (!t.Active || t.Destroyed || t.Identity != Identity.Hostile) continue;
                if (t.Kind != TargetKind.AntiShipMissile && t.Kind != TargetKind.Aircraft) continue;
                ThreatGeometry g = ThreatGeometry.Of(t, own, combat.Tick, combat.TickSeconds);
                if (!g.Inbound) continue;
                output.Add(new RankedThreat { Track = t, Geometry = g });
            }
            output.Sort((a, b) =>
            {
                int c = a.Geometry.TimeToCpaS.CompareTo(b.Geometry.TimeToCpaS);
                return c != 0 ? c : a.Track.Id.CompareTo(b.Track.Id);
            });
        }
    }

    /// <summary>Gun-arc arithmetic for C3, pure so it can be tested on its own.</summary>
    public static class ArcMath
    {
        /// <summary>
        /// How many (threat, mount) pairs would be inside the mounts' training arcs if the ship steered
        /// <paramref name="courseDeg"/>: anti-air mounts that are online only, by bearing from the ship's position.
        /// </summary>
        public static int PairsInArc(List<RankedThreat> threats, in OwnShipState own, float courseDeg, IReadOnlyList<IGunMount> mounts)
        {
            int pairs = 0;
            for (int i = 0; i < threats.Count; i++)
            {
                Vector3 d = threats[i].Track.Position - own.Position;
                float trueBearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float relative = Mathf.DeltaAngle(courseDeg, trueBearing);
                for (int m = 0; m < mounts.Count; m++)
                {
                    GunMountProfileData g = mounts[m].Profile;
                    if (!mounts[m].Online || !g.AntiAir) continue;
                    if (Mathf.Abs(Mathf.DeltaAngle(g.FacingDeg, relative)) <= g.ArcHalfDeg) pairs++;
                }
            }
            return pairs;
        }
    }

    public static class AiCaptainFactory
    {
        public static IAiCaptain Create(FidelityLevel requested, in AiCaptainProfileData profile, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.AiCaptain, requested);
            switch (resolution.Resolved)
            {
                default: return new AiCaptainL0(profile);
            }
        }
    }
}
