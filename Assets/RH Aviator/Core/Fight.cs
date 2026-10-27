using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>A side in a fight (W52): blue is the player's, red hunts the player.</summary>
    public enum Side : byte { None = 0, Blue = 1, Red = 2 }

    /// <summary>One moment a jet got into, or left, an enemy's rear cone (W46): the fight log.</summary>
    public readonly struct ConeEvent
    {
        public readonly long Tick;
        public readonly int Attacker, Target;
        public readonly bool Entered;

        public ConeEvent(long tick, int attacker, int target, bool entered)
        {
            Tick = tick;
            Attacker = attacker;
            Target = target;
            Entered = entered;
        }

        public override string ToString() => $"tick {Tick} · {Attacker} {(Entered ? "in" : "out of")} {Target}'s rear cone";
    }

    /// <summary>
    /// The rear cone (W46): the attacker inside <c>coneDeg</c> of the target's tail and inside <c>rangeM</c>, and
    /// pointing at the target within the same angle (A9, proposed in slice 3d: by position alone, two jets just past a
    /// head-on pass each scored 2.7 s in the other's cone while flying apart). No weapons and no gun solution
    /// (BLUEPRINT §6.5): scored in seconds.
    /// </summary>
    public static class RearCone
    {
        public static bool Inside(in AirframeState attacker, in AirframeState target, float coneDeg, float rangeM)
        {
            if (attacker.Status == FlightStatus.Crashed || target.Status == FlightStatus.Crashed) return false;
            Vector3 fromTarget = attacker.Position - target.Position;
            float d = fromTarget.magnitude;
            if (d > rangeM || d < 1f) return false;
            Vector3 tail = -(target.Velocity.sqrMagnitude > 1f ? target.Velocity.normalized : target.Attitude * Vector3.forward);
            if (Vector3.Angle(fromTarget, tail) > coneDeg) return false;
            Vector3 heading = attacker.Velocity.sqrMagnitude > 1f ? attacker.Velocity : attacker.Attitude * Vector3.forward;
            return Vector3.Angle(heading, -fromTarget) <= coneDeg;
        }
    }

    /// <summary>
    /// One fight (W43 up to eight, W46, W52, W53): who is on which side, which aircraft the player flies (red hunts it),
    /// and the score: seconds each aircraft spent in an enemy's rear cone, with the fight log of every entry and exit.
    /// <see cref="Score"/> runs once per tick after the flight core steps. Pure and deterministic.
    /// </summary>
    public sealed class Engagement
    {
        readonly Dictionary<int, Side> _sides = new Dictionary<int, Side>();
        readonly Dictionary<int, Side> _sideEver = new Dictionary<int, Side>();
        readonly Dictionary<int, double> _seconds = new Dictionary<int, double>();
        readonly HashSet<long> _inCone = new HashSet<long>();
        readonly List<ConeEvent> _log = new List<ConeEvent>();
        readonly List<int> _ids = new List<int>();

        public readonly float ConeDeg, RangeM;

        /// <summary>The aircraft red hunts (W52): the player's, or 0 for none (then red fights the nearest blue).</summary>
        public Func<int> Hunted = () => 0;

        public Engagement(float coneDeg = 30f, float rangeM = 1000f)
        {
            ConeDeg = coneDeg;
            RangeM = rangeM;
        }

        public void Join(int id, Side side)
        {
            if (side == Side.None) throw new ArgumentException("a fighter needs a side", nameof(side));
            if (!_sides.ContainsKey(id)) { _ids.Add(id); _ids.Sort(); }
            _sides[id] = side;
            _sideEver[id] = side;
            if (!_seconds.ContainsKey(id)) _seconds[id] = 0.0;
        }

        /// <summary>Takes a fighter out (it keeps its score): it is no one's enemy from now on.</summary>
        public void Leave(int id)
        {
            _sides.Remove(id);
            _ids.Remove(id);
        }

        public Side SideOf(int id) => _sides.TryGetValue(id, out Side s) ? s : Side.None;
        public bool Enemies(int a, int b) { Side sa = SideOf(a), sb = SideOf(b); return sa != Side.None && sb != Side.None && sa != sb; }
        public IReadOnlyList<int> Fighters => _ids;
        public IReadOnlyList<ConeEvent> Log => _log;

        /// <summary>Seconds <paramref name="id"/> has spent in an enemy's rear cone (W46).</summary>
        public double SecondsInCone(int id) => _seconds.TryGetValue(id, out double s) ? s : 0.0;

        /// <summary>A side's total.</summary>
        public double SideSeconds(Side side)
        {
            double t = 0;
            foreach (KeyValuePair<int, double> kv in _seconds) if (_sideEver.TryGetValue(kv.Key, out Side s) && s == side) t += kv.Value;
            return t;
        }

        /// <summary>After each tick: who sat in whose rear cone, the truth (scoring is the referee, not a pilot).</summary>
        public void Score(FlightCore core)
        {
            double dt = Math.Round(core.Clock.TickSeconds, 6);
            long tick = core.Tick - 1;
            IReadOnlyList<Aircraft> all = core.Aircraft;
            for (int i = 0; i < all.Count; i++)
            for (int j = 0; j < all.Count; j++)
            {
                if (i == j) continue;
                int a = all[i].Id, t = all[j].Id;
                if (!Enemies(a, t)) continue;
                long key = ((long)a << 32) | (uint)t;
                bool inside = RearCone.Inside(all[i].State, all[j].State, ConeDeg, RangeM);
                bool was = _inCone.Contains(key);
                if (inside) _seconds[a] += dt;
                if (inside != was)
                {
                    if (inside) _inCone.Add(key); else _inCone.Remove(key);
                    _log.Add(new ConeEvent(tick, a, t, inside));
                }
            }
        }
    }

    /// <summary>
    /// The fight's plan for one tick (W38, W45): toward the target the pilot believes in, by lead, pure or lag pursuit
    /// from the geometry and the closure, at its fighting speed; or, with an enemy seen close in its own rear quarter,
    /// a break turn across the threat's line (keeping its own tail clear). The height follows the target's, above the
    /// pilot's floor. A rookie's profile has no lead and no lag: pure pursuit only.
    /// </summary>
    public static class Fighting
    {
        public enum Pursuit : byte { Pure = 0, Lead = 1, Lag = 2, Break = 3 }

        public static FlightPathRequest Plan(in AirframeState me, in Contact target, Contact? threat, long now, double tickSeconds, float surfaceM, in PilotProfile p, out Pursuit pursuit)
        {
            const float Kt = (float)Units.KnotToMetrePerSecond;
            float floor = surfaceM + p.FightFloorM;
            if (threat.HasValue)
            {
                // A break turn: hard across the threat's line of sight, toward the side it is on.
                Vector3 toThreat = threat.Value.GuessPosition(now, tickSeconds) - me.Position;
                Vector3 track = new Vector3(me.Velocity.x, 0f, me.Velocity.z);
                float side = Mathf.Sign(Vector3.SignedAngle(track, new Vector3(toThreat.x, 0f, toThreat.z), Vector3.up));
                pursuit = Pursuit.Break;
                return new FlightPathRequest
                {
                    AimWorld = me.Position + Quaternion.Euler(0f, side * 100f, 0f) * (track.sqrMagnitude > 1f ? track.normalized : Vector3.forward) * 5000f,
                    AltitudeM = Mathf.Max(me.Position.y, floor),
                    SpeedMps = p.CornerKt * Kt,
                };
            }

            Vector3 tPos = target.GuessPosition(now, tickSeconds);
            Vector3 tVel = target.Seen.Velocity;
            Vector3 rel = tPos - me.Position;
            float range = rel.magnitude;
            float closure = -Vector3.Dot(tVel - me.Velocity, rel / Mathf.Max(range, 1f));   // positive: closing
            // Behind the target (inside its rear hemisphere)?
            bool behind = Vector3.Dot(-rel, tVel) < 0f;
            pursuit = Pursuit.Pure;
            Vector3 aim = tPos;
            if (p.LagS > 0f && range < p.LagRangeM && closure > p.OvershootClosureKt * Kt)
            {
                pursuit = Pursuit.Lag;
                aim = tPos - tVel * p.LagS;
            }
            else if (p.LeadS > 0f && behind && range > p.LagRangeM)
            {
                pursuit = Pursuit.Lead;
                aim = tPos + tVel * p.LeadS;
            }
            float speedKt = pursuit == Pursuit.Lag ? p.CornerKt - p.LagSlowKt : p.CornerKt;
            // Range control (W45): close with speed, fight at the fighting speed. The target's own speed plus a term on
            // the range to the attack distance, held between the slow speed and the fighting speed plus the chase margin
            // (slice 3d: at its 320 kt fighting speed the ace fell 4–5 km behind a rookie circling at 380 kt, and red
            // could never catch a hands-off player at 400 kt). A rookie has none: it flies one speed.
            if (p.RangeKtPerM > 0f && pursuit != Pursuit.Lag)
            {
                float targetKt = tVel.magnitude / Kt;
                speedKt = Mathf.Clamp(targetKt + p.RangeKtPerM * (range - p.AttackRangeM), Mathf.Max(p.SlowKt, 150f), p.CornerKt + p.ChaseOverKt);
            }
            var q = new FlightPathRequest
            {
                AimWorld = aim,
                AltitudeM = Mathf.Max(tPos.y, floor),
                SpeedMps = speedKt * Kt,
            };
            // Turn matching (W45, AP32): bank for the target's own turn rate at the pilot's speed, so the L1 law only
            // takes out the angle off (slice 3d: on the law alone the ace settled 33° off a turning rookie's tail).
            if (p.TurnMatch > 0f)
            {
                Vector3 spin = target.Seen.Attitude * target.Seen.BodyRates;
                float turnRate = Vector3.Dot(spin, Vector3.up);   // +: turning right
                float v = new Vector2(me.Velocity.x, me.Velocity.z).magnitude;
                q.BankDeg = p.TurnMatch * Mathf.Atan2(v * turnRate, Units.StandardGravity) * Mathf.Rad2Deg;
            }
            Energy(ref q, me, p);
            return q;
        }

        /// <summary>
        /// Energy sense (W45): below the fighting speed a pilot with it eases its turn, from its full G at the fighting
        /// speed down to 2 G (and the bank that holds level at it) at the slow speed, so it gets the speed back instead
        /// of bleeding to the edge of the stall (slice 3d: an ace at 75° and 7 G sank from 10,800 to 6,800 ft at 200 kt).
        /// A rookie has none.
        /// </summary>
        static void Energy(ref FlightPathRequest q, in AirframeState me, in PilotProfile p)
        {
            if (p.SlowKt <= 0f) return;
            const float Kt = (float)Units.KnotToMetrePerSecond;
            float kt = me.Velocity.magnitude / Kt;
            float share = Mathf.InverseLerp(p.SlowKt, p.CornerKt, kt);
            if (share >= 1f) return;
            float g = Mathf.Lerp(2f, p.Hands.MaxG, share);
            q.GLimit = g;
            q.BankLimitDeg = Mathf.Acos(Mathf.Clamp(1f / g, 0f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
