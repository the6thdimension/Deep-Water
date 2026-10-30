using System;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.Core.Radar;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.Aviator.Core.Tree;
using RHRadarSuite.Tracking;
using UnityEngine;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// A rule pilot's hands on the jet's sensors (SENSORS-CONCEPT J9; RH Aviator A21), judged on each watch and held
    /// between. Pure and deterministic: its chances draw from a stream of its own, derived from the fight's seed.
    /// <list type="bullet">
    /// <item><b>Search:</b> the radar in track-while-scan whenever it is off.</item>
    /// <item><b>Pick and identify:</b> the nearest firm track ahead inside its radar-missile range (a third more), not a
    /// friend. Seen (a contact it sees within <see cref="SightGateM"/> of the track, an enemy, and no friend there): hostile.
    /// Otherwise it asks (IFF, held): a friend's answer drops it; <see cref="PilotProfile.IffChallenges"/> challenges in the
    /// beam with no answer make it hostile, but only when its own jet carries a code. A new contact is forgotten, not asked
    /// about, at <see cref="PilotProfile.IffForgetChance"/>: then only sight makes it hostile.</item>
    /// <item><b>Shoot:</b> a hostile track is locked; inside <see cref="PilotProfile.RadarMissileRangeM"/> and 40° of the
    /// nose, one radar missile; the lock is held until it goes active, then the radar goes back to searching.</item>
    /// <item><b>Defend:</b> on a warning (the warning receiver's "missile" for chaff; a heat-seeker seen coming inside
    /// <see cref="PilotProfile.MissileWarnM"/> for flares) it drops them, one a second, at <see cref="PilotProfile.DecoyChance"/>
    /// drawn once per warning.</item>
    /// </list>
    /// Nothing here reads which side a track is on: only what the pilot sees, its radar's tracks and IFF, and its own
    /// missiles. Every decision is a rule firing in the pilot's log.
    /// </summary>
    public sealed class SensorHabits
    {
        /// <summary>A seen contact this near a track is taken to be it, metres.</summary>
        public const float SightGateM = 500f;
        /// <summary>The cone about the nose a radar missile is launched in, degrees (the missile's own is 45°).</summary>
        public const float LaunchConeDeg = 40f;

        readonly FlightCore _core;
        readonly PilotProfile _pilot;
        readonly DeterministicRng _draws;
        readonly Action<long, int, string, string> _note;

        int _candidate;          // the track it is identifying or shooting (0: none)
        bool _asks, _hostile;
        string _how, _pickled;   // how the candidate was found hostile; how the last pickle's was (counted once its missile flies)
        bool _warnChaff, _warnFlare, _dropChaff, _dropFlare;
        PilotSwitches _held;

        /// <summary>Radar missiles it launched (counted once in the air), how many on a track it had asked about, and how many on one it had seen.</summary>
        public int RadarLaunches { get; private set; }
        public int LaunchesAfterAsking { get; private set; }
        public int LaunchesOnSight { get; private set; }
        /// <summary>Missile warnings it had, and how many it answered with chaff or flares.</summary>
        public int Warnings { get; private set; }
        public int WarningsAnswered { get; private set; }

        /// <param name="note">Logs a rule firing: tick, aircraft, rule, reason.</param>
        public SensorHabits(FlightCore core, in PilotProfile pilot, int selfId, Action<long, int, string, string> note)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _pilot = pilot;
            _note = note ?? throw new ArgumentNullException(nameof(note));
            _draws = new DeterministicRng(core.Seed ^ 0x6861626974730000UL ^ ((ulong)(uint)selfId * 0xD6E8FEB86659FD93UL));
            Tree = BuildTree();
        }

        /// <summary>
        /// The habits as a behavior tree (RH Aviator A27; slice 4a): the port of what were two methods, <c>Defend</c> then
        /// <c>Shoot</c>, branch for branch, proven by <c>FightTreePortTests</c> to fly as they did (A33). Each early return of
        /// the old <c>Shoot</c> is a <see cref="Step"/> that fails or a branch of a <see cref="Selector"/> that ends; the
        /// chances draw in the same order. For the Fight Tree window (A28).
        /// </summary>
        public Node Tree { get; }

        // The watch the tree is running on: what the branches read, and the inputs they write (a ref cannot be captured).
        long _t;
        int _id;
        AirframeState _me;
        JetRadar _radar;
        Perception _eyes;
        Engagement _fight;
        PilotInputs _inputs;
        bool _guiding, _inAir;
        RadarTrack _track;

        /// <summary>
        /// Adds this tick's sensor inputs to <paramref name="inputs"/>: the held switches every tick; on a watch, the
        /// judgement (a radar command, a lock, a launch). <paramref name="eyes"/> and <paramref name="fight"/> are the
        /// pilot's: what it sees, and the sides of what it sees.
        /// </summary>
        public void Apply(long tick, int id, in AirframeState me, bool watch, Perception eyes, Engagement fight, ref PilotInputs inputs)
        {
            _radar = _core.RadarOf(id);
            if (watch && me.Status == FlightStatus.Flying)
            {
                _t = tick; _id = id; _me = me; _eyes = eyes; _fight = fight;
                _inputs = inputs;
                Tree.Tick(tick);
                inputs = _inputs;
            }
            if (me.Status != FlightStatus.Flying) { _held = PilotSwitches.None; _candidate = 0; }
            inputs.Switches |= _held;
        }

        Node BuildTree() => new Sequence("sensors",
            new Act("defend: warnings", Defend),
            new Condition("has a radar", () => _radar != null),
            new Act("stop asking", () => _held &= ~PilotSwitches.Interrogate),
            new Selector("radar",
                new Sequence("search",
                    new Condition("radar off", () => _radar.Mode == RadarMode.Off),
                    new Act("radar: search", Search)),
                new Sequence("radar missiles",
                    new Condition("carries radar missiles", () => _pilot.RadarMissileRangeM > 0f),
                    new Act("count launches", CountLaunches),
                    new Selector("lock or pick",
                        new Sequence("locked",
                            new Condition("radar locked", () => _radar.Mode == RadarMode.SingleTarget),
                            new Selector("shoot or let go",
                                new Condition("guiding its missile", () => _guiding),
                                new Sequence("let go",
                                    new Condition("nothing to shoot", NothingToShoot),
                                    new Act("radar: unlock", Unlock)),
                                new Sequence("shoot",
                                    new Condition("in shot", () => _radar.TryTrack(_candidate, out _track) && InShot(_me, _track, _pilot.RadarMissileRangeM, LaunchConeDeg)),
                                    new Act("radar missile", Pickle)),
                                new Act("hold the lock", () => { }))),
                        new Sequence("nothing to pick",
                            new Condition("one in the air, or none left", () => _inAir || _me.RadarMissiles <= 0),
                            new Act("forget the candidate", () => _candidate = 0)),
                        new Sequence("pick and identify",
                            new Act("pick the nearest", PickCandidate),
                            new Condition("a candidate", () => _candidate != 0),
                            new Step("its track held", () => { if (_radar.TryTrack(_candidate, out _track)) return true; _candidate = 0; return false; }),
                            new Step("identify by sight", IdentifyBySight),
                            new Step("identify by IFF", IdentifyByIff),
                            new Condition("hostile, in shot", () => _hostile && InShot(_me, _track, _pilot.RadarMissileRangeM, LaunchConeDeg)),
                            new Act("radar: lock", Lock))))));

        void Defend()
        {
            long tick = _t;
            int id = _id;
            ref readonly AirframeState me = ref _me;
            WarningReceiver rwr = _core.WarningReceiverOf(id);
            bool chaff = rwr != null && rwr.Strongest == PaintKind.Missile;
            bool flare = false;
            var live = _core.Missiles.Live;
            float warn2 = _pilot.MissileWarnM * _pilot.MissileWarnM;
            for (int i = 0; i < live.Count && !flare; i++)
            {
                MissileInFlight m = live[i];
                if (m.ShooterId == id || m.Radar || m.Lost) continue;
                Vector3 to = me.Position - m.Position;
                flare = to.sqrMagnitude <= warn2 && Vector3.Dot(m.Velocity - me.Velocity, to) > 0f;   // near and closing
            }
            if (chaff && !_warnChaff) _dropChaff = Warned(tick, id, "chaff", "the warning receiver says missile");
            if (flare && !_warnFlare) _dropFlare = Warned(tick, id, "flares", $"a heat-seeker coming inside {_pilot.MissileWarnM:F0} m");
            _warnChaff = chaff;
            _warnFlare = flare;
            _held &= ~(PilotSwitches.Chaff | PilotSwitches.Flare);
            if (chaff && _dropChaff) _held |= PilotSwitches.Chaff;
            if (flare && _dropFlare) _held |= PilotSwitches.Flare;
        }

        bool Warned(long tick, int id, string rule, string reason)
        {
            Warnings++;
            bool answer = _draws.NextFloat01() < _pilot.DecoyChance;
            if (answer) WarningsAnswered++;
            _note(tick, id, answer ? rule : rule + ": missed", reason);
            return answer;
        }

        void Search()
        {
            _inputs.Radar = RadarCommand.TrackWhileScan;
            _note(_t, _id, "radar: search", "track-while-scan");
        }

        /// <summary>One radar missile of its own in the air at a time; the lock is held for it until it goes active.</summary>
        void CountLaunches()
        {
            _guiding = false; _inAir = false;
            var live = _core.Missiles.Live;
            for (int i = 0; i < live.Count; i++)
                if (live[i].ShooterId == _id && live[i].Radar && !live[i].Lost) { _inAir = true; _guiding |= !live[i].Active; }
            if (_pickled != null && _inAir)
            {
                RadarLaunches++;
                if (_pickled == "asked") LaunchesAfterAsking++;
                else LaunchesOnSight++;
            }
            _pickled = null;
        }

        bool NothingToShoot() =>
            _candidate == 0 || _radar.LockedTrackId != _candidate || _inAir || _me.RadarMissiles <= 0 || !_hostile;

        void Unlock()
        {
            _inputs.Radar = RadarCommand.Unlock;
            _note(_t, _id, "radar: unlock", _inAir ? "its missile is active" : "nothing to shoot");
            _candidate = 0;
        }

        void Pickle()
        {
            _inputs.RadarPickle = true;
            _pickled = _how;
            _note(_t, _id, "radar missile", $"on track {_candidate}, hostile by {(_how == "asked" ? "IFF: asked, no answer" : "sight")}, {Range(_me, _track):F0} m");
        }

        /// <summary>A new pick is a new candidate: not yet hostile, and asked about unless forgotten (one draw).</summary>
        void PickCandidate()
        {
            int pick = Pick(_me, _radar);
            if (pick == _candidate) return;
            _candidate = pick;
            _hostile = false;
            _how = null;
            if (pick == 0) return;
            _asks = !(_draws.NextFloat01() < _pilot.IffForgetChance);
            _note(_t, _id, _asks ? "iff: ask" : "iff: forgot", $"track {pick}");
        }

        /// <summary>Seen as a friend: dropped, and the branch ends. Seen as an enemy: hostile, by sight.</summary>
        bool IdentifyBySight()
        {
            if (_hostile) return true;
            int seen = SeenAs(_track, _eyes, _fight, _id, _t);
            if (seen < 0) { Drop(_t, _id, _radar, "seen: a friend"); return false; }
            if (seen > 0) { _hostile = true; _how = "sight"; _note(_t, _id, "identified", $"track {_candidate}: seen, an enemy"); }
            return true;
        }

        /// <summary>
        /// A friend's answer: dropped, and the branch ends. Enough challenges unanswered, its own jet carrying a code:
        /// hostile, asked. Otherwise it keeps asking.
        /// </summary>
        bool IdentifyByIff()
        {
            if (_hostile || !_asks) return true;
            if (_radar.IdentityOf(_candidate) == TrackIdentity.Friend) { Drop(_t, _id, _radar, "iff: a friend answered"); return false; }
            if (_core.Find(_id).IffCode != 0 && _radar.ChallengesOf(_candidate) >= _pilot.IffChallenges)
            {
                _hostile = true;
                _how = "asked";
                _note(_t, _id, "identified", $"track {_candidate}: {_radar.ChallengesOf(_candidate)} challenges, no answer");
            }
            else _held |= PilotSwitches.Interrogate;
            return true;
        }

        void Lock()
        {
            _inputs.Radar = RadarCommand.Lock;
            _inputs.RadarTrack = _candidate;
            _note(_t, _id, "radar: lock", $"track {_candidate}, {Range(_me, _track):F0} m");
        }

        void Drop(long tick, int id, JetRadar radar, string why)
        {
            _note(tick, id, "iff: friend", $"track {_candidate}: {why}");
            _candidate = 0;
            _hostile = false;
        }

        /// <summary>The nearest firm track ahead, inside a third more than its radar-missile range, not a known friend (0: none).</summary>
        int Pick(in AirframeState me, JetRadar radar)
        {
            int best = 0;
            float bestD = _pilot.RadarMissileRangeM * 4f / 3f;
            foreach (RadarTrack t in radar.Tracks)
            {
                if (t.State != TrackState.Confirmed || radar.IdentityOf(t.Id) == TrackIdentity.Friend) continue;
                if (!InShot(me, t, bestD, 60f)) continue;
                bestD = Range(me, t);
                best = t.Id;
            }
            return best;
        }

        /// <summary>
        /// What sight says about the track: +1 an enemy seen there and no friend, −1 a friend seen there, 0 nothing seen.
        /// A seen contact's side is the pilot's (it knows what it sees, W38); the track's maker is never read.
        /// </summary>
        int SeenAs(in RadarTrack t, Perception eyes, Engagement fight, int id, long tick)
        {
            if (eyes == null || fight == null) return 0;
            Vector3 at = At(t);
            Side mine = fight.SideOf(id);
            bool enemy = false;
            foreach (int k in eyes.Known)
            {
                if (!eyes.TryGet(k, out Contact c) || !c.InView) continue;
                if ((c.GuessPosition(tick, _core.Clock.TickSeconds) - at).sqrMagnitude > SightGateM * SightGateM) continue;
                Side s = fight.SideOf(k);
                if (s == mine) return -1;
                if (s != Side.None) enemy = true;
            }
            return enemy ? 1 : 0;
        }

        static Vector3 At(in RadarTrack t) => new Vector3((float)t.Position.X, (float)t.Position.Y, (float)t.Position.Z);
        static float Range(in AirframeState me, in RadarTrack t) => (At(t) - me.Position).magnitude;

        static bool InShot(in AirframeState me, in RadarTrack t, float rangeM, float coneDeg)
        {
            Vector3 to = At(t) - me.Position;
            float d = to.magnitude;
            return d <= rangeM && d > 1f && Vector3.Dot(me.Attitude * Vector3.forward, to / d) >= Mathf.Cos(coneDeg * Mathf.Deg2Rad);
        }
    }
}
