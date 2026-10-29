using System;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// How a learned pilot decides and what its actions mean (M1, M23), all AUTHORED and baked from
    /// <c>LearnedPilotSO</c>. Changing a range changes what a trained policy's numbers mean: a policy is kept with the
    /// settings it was trained under (M19).
    /// </summary>
    public struct LearnedPilotSettings
    {
        /// <summary>Ticks between decisions: 5 at the 50 Hz tick is ten a second, the rule pilots' trigger rate (A12).</summary>
        public int DecisionPeriodTicks;
        /// <summary>The longest one life runs before the episode is cut (interrupted, not lost), seconds.</summary>
        public float MaxLifeS;
        /// <summary>A turn action of ±1 asks for a track this many degrees either side of the present one.</summary>
        public float TurnRangeDeg;
        /// <summary>A climb action of ±1 asks for a height this many metres above or below the present one.</summary>
        public float ClimbRangeM;
        /// <summary>The speed action's ends, knots true: −1 asks for the first, +1 the second.</summary>
        public float MinKt, MaxKt;
        /// <summary>The G-cap action's low end, g; its high end is the pilot's own most (the ace's 7 G, M17).</summary>
        public float MinG;

        public void Validate()
        {
            if (DecisionPeriodTicks < 1) throw new ArgumentException("learned pilot: decide at least every tick");
            if (!(MaxLifeS > 0f)) throw new ArgumentException("learned pilot: a life needs a length");
            if (!(TurnRangeDeg > 0f && TurnRangeDeg <= 180f)) throw new ArgumentException("learned pilot: the turn range is 0 … 180°");
            if (!(ClimbRangeM > 0f)) throw new ArgumentException("learned pilot: the climb range must be positive");
            if (!(MinKt > 0f && MaxKt > MinKt)) throw new ArgumentException("learned pilot: speeds need 0 < min < max");
            if (!(MinG >= 1f)) throw new ArgumentException("learned pilot: the G cap's low end is at least 1 g");
        }
    }

    /// <summary>
    /// The policy's actions and what they ask for (M1): four continuous numbers in −1 … +1 (turn, climb, speed, G cap)
    /// and one choice (trigger off or on). Decoded into a <see cref="FlightPathRequest"/> the hands fly, so the policy
    /// works the same bounded hands as every rule pilot: their bank cap and their ground guard.
    /// </summary>
    public static class LearnedActions
    {
        public const int ContinuousCount = 4;
        public const int Turn = 0, Climb = 1, Speed = 2, GCap = 3;
        /// <summary>The discrete branches: the trigger (0 off, 1 on), then the missile pickle (0 off, 1 on; W65).</summary>
        public const int TriggerChoices = 2, PickleChoices = 2;
        public const int TriggerBranch = 0, PickleBranch = 1;
        /// <summary>The aim point sits this far along the asked-for track: far enough to be a heading (the hands cap L at a turn radius).</summary>
        const float AimDistanceM = 10000f;

        /// <summary>
        /// The command for actions <paramref name="a"/> (length <see cref="ContinuousCount"/>) from state <paramref name="s"/>.
        /// Values beyond ±1 are clamped (the trainer samples a Gaussian); a NaN is a broken policy and throws.
        /// </summary>
        public static LearnedCommand Decode(ReadOnlySpan<float> a, bool trigger, bool pickle, in AirframeState s, in LearnedPilotSettings set, in PilotProfile pilot)
        {
            if (a.Length != ContinuousCount) throw new ArgumentException($"a learned pilot takes {ContinuousCount} continuous actions, not {a.Length}");
            float turn = Unit(a[Turn], "turn"), climb = Unit(a[Climb], "climb"), speed = Unit(a[Speed], "speed"), g = Unit(a[GCap], "G cap");
            float bearing = (FlightPathHands.TrackDeg(s) + turn * set.TurnRangeDeg) * Mathf.Deg2Rad;
            return new LearnedCommand
            {
                Request = new FlightPathRequest
                {
                    AimWorld = s.Position + new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing)) * AimDistanceM,
                    AltitudeM = s.Position.y + climb * set.ClimbRangeM,
                    SpeedMps = Mathf.Lerp(set.MinKt, set.MaxKt, 0.5f * (speed + 1f)) * (float)Units.KnotToMetrePerSecond,
                    GLimit = Mathf.Lerp(set.MinG, pilot.Hands.MaxG, 0.5f * (g + 1f)),
                },
                Trigger = trigger,
                Pickle = pickle,
            };
        }

        static float Unit(float v, string name)
        {
            if (float.IsNaN(v)) throw new ArgumentException($"the policy's {name} action is NaN");
            return Mathf.Clamp(v, -1f, 1f);
        }

        /// <summary>
        /// A scripted stand-in for the policy (the agent's heuristic, for smokes without a trainer): lead pursuit on the
        /// target it guesses at (the pilot's own lead time, as the rule pilots' <see cref="Fighting"/>), climbing to the lead
        /// point, at the pilot's fighting speed with the G cap open, holding the trigger when the pipper's miss is inside the
        /// pilot's own discipline. Fills <paramref name="actions"/>. (Pure pursuit, tried first on 2026-10-29, sat in slow
        /// circles 50° off the target's nose for two minutes and never fired.)
        /// </summary>
        public static bool Scripted(in AirframeState me, bool hasTarget, Vector3 targetGuess, Vector3 targetVelocity, float missMils,
                                    bool lockedOnEnemy, in PilotProfile pilot, in LearnedPilotSettings set, Span<float> actions, out bool pickle)
        {
            // The pickle as the rule pilots' discipline has it: locked on an enemy inside the pilot's missile range (A16).
            pickle = lockedOnEnemy && hasTarget && (targetGuess - me.Position).magnitude <= pilot.MissileRangeM;
            if (actions.Length != ContinuousCount) throw new ArgumentException($"{ContinuousCount} actions");
            actions[Turn] = actions[Climb] = 0f;
            actions[Speed] = Mathf.Clamp(2f * Mathf.InverseLerp(set.MinKt, set.MaxKt, pilot.CornerKt) - 1f, -1f, 1f);
            actions[GCap] = 1f;
            if (!hasTarget) return false;
            Vector3 to = targetGuess + targetVelocity * pilot.LeadS - me.Position;
            float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            actions[Turn] = Mathf.Clamp(Mathf.DeltaAngle(FlightPathHands.TrackDeg(me), bearing) / set.TurnRangeDeg, -1f, 1f);
            actions[Climb] = Mathf.Clamp(to.y / set.ClimbRangeM, -1f, 1f);
            return missMils <= pilot.TriggerMils;
        }
    }
}
