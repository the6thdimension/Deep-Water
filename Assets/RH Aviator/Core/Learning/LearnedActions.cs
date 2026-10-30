using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
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
    /// and six choices: the trigger, the missile pickle, and (version 3, SENSORS-CONCEPT J20) the radar, IFF, decoys and
    /// the radar-missile pickle. Decoded into a <see cref="FlightPathRequest"/> the hands fly, so the policy works the same
    /// bounded hands as every rule pilot: their bank cap and their ground guard; and into the same radar commands and
    /// switches a rule pilot's <see cref="SensorHabits"/> give.
    /// </summary>
    public static class LearnedActions
    {
        public const int ContinuousCount = 4;
        public const int Turn = 0, Climb = 1, Speed = 2, GCap = 3;
        /// <summary>The discrete branches: the trigger (0 off, 1 on), then the missile pickle (0 off, 1 on; W65).</summary>
        public const int TriggerChoices = 2, PickleChoices = 2;
        public const int TriggerBranch = 0, PickleBranch = 1;
        /// <summary>
        /// Version 3 (J20): the radar (0 leave it, 1 track-while-scan, 2 unlock, 3 + k lock the track in senses slot k),
        /// IFF (0 off, 1 ask), decoys (0 none, 1 chaff, 2 flares, 3 both) and the radar-missile pickle (0 off, 1 on).
        /// A radar command is momentary: given once, on the tick after the decision.
        /// </summary>
        public const int RadarBranch = 2, IffBranch = 3, DecoyBranch = 4, RadarPickleBranch = 5;
        public const int RadarLeave = 0, RadarSearch = 1, RadarUnlock = 2, RadarLockSlot0 = 3;
        public const int RadarChoices = RadarLockSlot0 + LearnedSenses.TrackSlots, IffChoices = 2, DecoyChoices = 4, RadarPickleChoices = 2;
        /// <summary>Every branch's size, in order: the action spec's discrete branches.</summary>
        public static int[] Branches() => new[] { TriggerChoices, PickleChoices, RadarChoices, IffChoices, DecoyChoices, RadarPickleChoices };
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

        /// <summary>
        /// The sensor choices <paramref name="d"/> (every branch, as <see cref="Branches"/>) added to <paramref name="command"/>:
        /// a lock names the radar track in that senses slot (<paramref name="trackSlots"/>); a lock on an empty slot is nothing.
        /// A short vector (an older policy's two branches) leaves the sensors alone.
        /// </summary>
        public static void DecodeSensors(ReadOnlySpan<int> d, IReadOnlyList<int> trackSlots, ref LearnedCommand command)
        {
            command.Radar = RadarCommand.None;
            command.RadarTrack = 0;
            if (d.Length <= RadarPickleBranch) return;
            int radar = d[RadarBranch];
            if (radar == RadarSearch) command.Radar = RadarCommand.TrackWhileScan;
            else if (radar == RadarUnlock) command.Radar = RadarCommand.Unlock;
            else if (radar >= RadarLockSlot0 && radar < RadarChoices)
            {
                int track = trackSlots != null && radar - RadarLockSlot0 < trackSlots.Count ? trackSlots[radar - RadarLockSlot0] : 0;
                if (track != 0) { command.Radar = RadarCommand.Lock; command.RadarTrack = track; }
            }
            command.Interrogate = d[IffBranch] == 1;
            command.Chaff = (d[DecoyBranch] & 1) != 0;
            command.Flare = (d[DecoyBranch] & 2) != 0;
            command.RadarPickle = d[RadarPickleBranch] == 1;
        }

        /// <summary>
        /// The continuous actions that ask for <paramref name="r"/> (Decode's inverse, clamped to the ranges; a rule pilot's
        /// flight-path angle is not expressible and is dropped). Used to turn a teacher's requests into actions (J13).
        /// </summary>
        public static void FromRequest(in FlightPathRequest r, in AirframeState s, in LearnedPilotSettings set, in PilotProfile pilot, Span<float> actions)
        {
            if (actions.Length != ContinuousCount) throw new ArgumentException($"{ContinuousCount} actions");
            Vector3 to = r.AimWorld - s.Position;
            float bearing = to.x * to.x + to.z * to.z > 1f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : FlightPathHands.TrackDeg(s);
            actions[Turn] = Mathf.Clamp(Mathf.DeltaAngle(FlightPathHands.TrackDeg(s), bearing) / set.TurnRangeDeg, -1f, 1f);
            // A flight-path angle (gun tracking) is the height the hands would climb by for the same path.
            float climbM = (float.IsNaN(r.AltitudeM) ? 0f : r.AltitudeM - s.Position.y) + (pilot.Hands.ClimbDegPerM > 0f ? r.PathDeg / pilot.Hands.ClimbDegPerM : 0f);
            actions[Climb] = Mathf.Clamp(climbM / set.ClimbRangeM, -1f, 1f);
            float kt = r.SpeedMps / (float)Units.KnotToMetrePerSecond;
            actions[Speed] = Mathf.Clamp(2f * Mathf.InverseLerp(set.MinKt, set.MaxKt, kt) - 1f, -1f, 1f);
            float g = r.GLimit > 0f ? r.GLimit : pilot.Hands.MaxG;
            actions[GCap] = Mathf.Clamp(2f * Mathf.InverseLerp(set.MinG, pilot.Hands.MaxG, g) - 1f, -1f, 1f);
        }

        /// <summary>
        /// The discrete choices that give a teacher's inputs (J13): its trigger and pickles, its held switches, and its last
        /// radar command since the previous decision (<paramref name="radar"/>, <paramref name="radarTrack"/>). A lock on a
        /// track not in a senses slot cannot be named and is left out.
        /// </summary>
        public static void FromInputs(in PilotInputs held, RadarCommand radar, int radarTrack, IReadOnlyList<int> trackSlots, Span<int> d)
        {
            if (d.Length != 6) throw new ArgumentException("six branches");
            d[TriggerBranch] = held.Trigger ? 1 : 0;
            d[PickleBranch] = held.Pickle ? 1 : 0;
            d[RadarBranch] = RadarLeave;
            if (radar == RadarCommand.TrackWhileScan) d[RadarBranch] = RadarSearch;
            else if (radar == RadarCommand.Unlock) d[RadarBranch] = RadarUnlock;
            else if (radar == RadarCommand.Lock && trackSlots != null)
                for (int k = 0; k < trackSlots.Count && k < LearnedSenses.TrackSlots; k++)
                    if (trackSlots[k] == radarTrack) { d[RadarBranch] = RadarLockSlot0 + k; break; }
            d[IffBranch] = (held.Switches & PilotSwitches.Interrogate) != 0 ? 1 : 0;
            d[DecoyBranch] = ((held.Switches & PilotSwitches.Chaff) != 0 ? 1 : 0) | ((held.Switches & PilotSwitches.Flare) != 0 ? 2 : 0);
            d[RadarPickleBranch] = held.RadarPickle ? 1 : 0;
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
