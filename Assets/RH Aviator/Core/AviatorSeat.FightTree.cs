using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.Aviator.Core.Tree;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// The fight's rules as a behavior tree (RH Aviator A25, A27; slice 4a). Until slice 4a these were one method,
    /// <c>FightRules</c>; the tree is a port of it, branch for branch and in the same order, and flies every fight of
    /// <c>FightTreePortTests</c> exactly as it did (A33). Each early return of the old rules is a <see cref="Step"/> that
    /// fails or a branch of the "plan" <see cref="Selector"/> that succeeds. The tree runs on the watch (A34), with what the
    /// pilot sees (W38): a first sight of an enemy is a "tally"; a seen enemy close in the pilot's own rear quarter makes
    /// it "break"; red goes for the jet the player flies, by a "vector" (a controller's steer, from the truth) until it
    /// sees it; blue goes for the nearest red it sees, and with none inside its clear range it is "clear" and rejoins the
    /// player's wing.
    /// </summary>
    public sealed partial class AviatorSeat
    {
        Node _fightTree;

        /// <summary>The fight tree, for the Fight Tree window (A28); each node says what it did on the last watch it ran.</summary>
        public Node FightTree => _fightTree;

        // The watch the tree is running on, and what its first branch found, for the branches after it.
        long _ft;
        int _fid, _nearest, _threatId;
        float _nearestD, _threatD;
        Side _mine;
        int _hunted;
        bool _seen;
        AirframeState _fs;
        Contact _threat;

        void RunFightTree(long tick, int id, in AirframeState s)
        {
            _ft = tick;
            _fid = id;
            _fs = s;
            _fightTree.Tick(tick);
        }

        Node BuildFightTree() => new Sequence("fight",
            new Act("look: tally, nearest, threat", Look),
            new Step("blue wingman: rejoin when clear", BlueWingman),
            new Condition("in the fight", () => Mode == PilotMode.Fight),
            new Act("target gone", TargetGone),
            new Act("pick a target", PickTarget),
            new Act("check six", CheckSix),
            new Act("avoid", AvoidCheck),
            new Selector("plan",
                new Sequence("break",
                    new Condition("threat behind, in sight", () => _breaking && _eyes.TryGet(_threatId, out _threat)),
                    new Act("break turn", BreakTurn)),
                new Sequence("vector",
                    new Act("hunted out of sight?", UpdateVector),
                    new Condition("on a vector", () => _vectored),
                    new Act("steer on the vector", SteerOnVector)),
                new Sequence("search",
                    new Act("note where it was", NoteLastSeen),
                    new Condition("lost it", () => (_targetId == 0 || !_seen) && _hasLastSeen),
                    new Act("turn to where it was seen", Search)),
                new Sequence("look about",
                    new Act("stop searching", () => _searching = false),
                    new Condition("nothing to fight", () => _targetId == 0 || !_seen),
                    new Act("hold, looking", HoldLooking)),
                new Act("pursue (flown every tick)", () => { })));

        /// <summary>Tally: first sight of each enemy (the contact is as old as the delay); the nearest enemy; a threat behind.</summary>
        void Look()
        {
            long tick = _ft;
            int id = _fid;
            ref readonly AirframeState s = ref _fs;
            _mine = _fight.SideOf(id);
            _hunted = _fight.Hunted();
            _nearest = 0; _threatId = 0;
            _nearestD = float.MaxValue; _threatD = float.MaxValue;
            IReadOnlyList<int> known = _eyes.Known;
            for (int i = 0; i < known.Count; i++)
            {
                int other = known[i];
                if (!_fight.Enemies(id, other) || !_eyes.TryGet(other, out Contact c)) continue;
                if (!_tallied.Contains(other))
                {
                    _tallied.Add(other);
                    Fire(tick, id, "tally", Mode, $"aircraft {other} seen at {(c.Seen.Position - s.Position).magnitude:F0} m (as of {(tick - c.SeenTick) * Math.Round(_core.Clock.TickSeconds, 6):F2} s ago)");
                }
                Vector3 guess = c.GuessPosition(tick, _core.Clock.TickSeconds);
                float d = (guess - s.Position).magnitude;
                if (d < _nearestD) { _nearestD = d; _nearest = other; }
                Vector3 tail = -(s.Velocity.sqrMagnitude > 1f ? s.Velocity.normalized : s.Attitude * Vector3.forward);
                // A threat: close, in the pilot's rear quarter, and pointing at it (slice 3d: a jet just past a head-on
                // pass, flying away, made the ace break for nothing).
                bool pointing = Vector3.Angle(c.Seen.Velocity, s.Position - guess) < _pilot.ThreatConeDeg;
                if (c.InView && pointing && d < _pilot.ThreatRangeM && Vector3.Angle(guess - s.Position, tail) < _pilot.ThreatConeDeg && d < _threatD)
                {
                    _threatD = d;
                    _threatId = other;
                }
            }
        }

        /// <summary>Blue, out of the fight: back to the player's wing; and back in when an enemy is seen inside the clear range. False: rejoined, the tree stops.</summary>
        bool BlueWingman()
        {
            if (_mine != Side.Blue || _hunted == 0 || _hunted == _fid) return true;
            if (Mode == PilotMode.Fight && (_nearest == 0 || _nearestD > _pilot.ClearRangeM))
            {
                _leaderId = _hunted;
                _targetId = 0;
                _fight.Claim(_fid, 0);
                Fire(_ft, _fid, "clear", PilotMode.Join, $"no enemy inside {_pilot.ClearRangeM:F0} m; rejoining aircraft {_hunted}");
                return false;
            }
            if ((Mode == PilotMode.Join || Mode == PilotMode.Fingertip) && _nearest != 0 && _nearestD <= _pilot.ClearRangeM)
                Fire(_ft, _fid, "engage", PilotMode.Fight, $"aircraft {_nearest} at {_nearestD:F0} m");
            return true;
        }

        /// <summary>
        /// A target that has left the fight is no target (slice 3d: the player's jet kept chasing a red jet after red
        /// left, and its wingmen could not rejoin a leader still turning hard).
        /// </summary>
        void TargetGone()
        {
            if (_targetId == 0 || _fight.Enemies(_fid, _targetId)) return;
            Fire(_ft, _fid, "target gone", PilotMode.Fight, $"aircraft {_targetId} is out of the fight");
            _targetId = 0;
            _fight.Claim(_fid, 0);
            _hasLastSeen = false;
        }

        /// <summary>
        /// The target: red hunts the player's jet (W52); everyone else the nearest enemy it knows that no teammate is
        /// already after, else the nearest (slice 3d smoke: three blue jets all chose the one nearest red and sat in its
        /// cone for three minutes while the other three hunted the player unopposed).
        /// </summary>
        void PickTarget()
        {
            long tick = _ft;
            int id = _fid;
            IReadOnlyList<int> known = _eyes.Known;
            int want = _mine == Side.Red && _hunted != 0 && _fight.Enemies(id, _hunted) ? _hunted : _nearest;
            if (!(_mine == Side.Red && _hunted != 0))
            {
                int free = 0;
                float freeD = float.MaxValue;
                for (int i = 0; i < known.Count; i++)
                {
                    int other = known[i];
                    if (!_fight.Enemies(id, other) || _fight.ClaimedBy(other, _mine, id) > 0 || !_eyes.TryGet(other, out Contact c)) continue;
                    float d = (c.GuessPosition(tick, _core.Clock.TickSeconds) - _fs.Position).magnitude;
                    if (d < freeD) { freeD = d; free = other; }
                }
                if (free != 0) want = free;
            }
            if (want != _targetId && want != 0)
            {
                _targetId = want;
                _fight.Claim(id, want);
                Fire(tick, id, "engage", PilotMode.Fight, $"after aircraft {want}");
            }
        }

        /// <summary>A threat on its tail first: break.</summary>
        void CheckSix()
        {
            bool breaking = _threatId != 0;
            if (breaking == _breaking) return;
            _breaking = breaking;
            Fire(_ft, _fid, breaking ? "break" : "tail clear", PilotMode.Fight, breaking ? $"aircraft {_threatId} at {_threatD:F0} m in its rear quarter" : "no threat behind");
        }

        /// <summary>Collision avoidance: any jet predicted to pass too close, soon, from the picture (a near miss is seen).</summary>
        void AvoidCheck()
        {
            if (Avoid(_fs, _fid, _pilot.AvoidM, out float avoidDy, out int avoidId))
            {
                if (!_avoiding) { _avoiding = true; Fire(_ft, _fid, "avoid", PilotMode.Fight, $"aircraft {avoidId} predicted inside {_pilot.AvoidM:F0} m; {(avoidDy > 0f ? "climbing" : "diving")} clear"); }
                _avoidHeight = _fs.Position.y + Mathf.Sign(avoidDy) * 3f * _pilot.AvoidM;
            }
            else _avoiding = false;
        }

        void BreakTurn() =>
            _request = Fighting.Plan(_fs, _threat, _threat, _ft, _core.Clock.TickSeconds, _core.Surface.HeightAt(_fs.Position.x, _fs.Position.z), _pilot, out _pursuit);

        /// <summary>Red with no sight of its quarry: a vector from the truth (a controller's steer), labelled in the log.</summary>
        void UpdateVector()
        {
            _seen = _targetId != 0 && _eyes.TryGet(_targetId, out _);
            bool vector = _mine == Side.Red && _hunted != 0 && !_seen;
            if (vector == _vectored) return;
            _vectored = vector;
            if (vector) Fire(_ft, _fid, "vector", PilotMode.Fight, $"no sight of aircraft {_hunted}; steering on a controller's vector (the truth)");
        }

        void SteerOnVector()
        {
            AirframeState truth = _core.Find(_hunted).State;
            _request = new FlightPathRequest
            {
                AimWorld = truth.Position,
                AltitudeM = Mathf.Max(truth.Position.y, _core.Surface.HeightAt(_fs.Position.x, _fs.Position.z) + _pilot.FightFloorM),
                SpeedMps = _pilot.CornerKt * (float)Units.KnotToMetrePerSecond,
            };
        }

        void NoteLastSeen()
        {
            if (_seen && _eyes.TryGet(_targetId, out Contact last)) { _lastSeen = last.GuessPosition(_ft, _core.Clock.TickSeconds); _hasLastSeen = true; }
        }

        /// <summary>
        /// Lost it: turn back to where it was last seen and look there (slice 3d: a rookie that forgot a jet behind it
        /// after the pass flew straight on at 416 kt for the rest of the fight).
        /// </summary>
        void Search()
        {
            if (!_searching) { _searching = true; Fire(_ft, _fid, "search", PilotMode.Fight, $"lost sight; turning to where it was last seen, {(_lastSeen - _fs.Position).magnitude:F0} m"); }
            _request = new FlightPathRequest
            {
                AimWorld = _lastSeen,
                AltitudeM = Mathf.Max(_lastSeen.y, _core.Surface.HeightAt(_fs.Position.x, _fs.Position.z) + _pilot.FightFloorM),
                SpeedMps = _pilot.CornerKt * (float)Units.KnotToMetrePerSecond,
            };
            if ((_lastSeen - _fs.Position).magnitude < 1000f) _hasLastSeen = false;   // nothing there: hold and look
        }

        /// <summary>Nothing to fight yet: hold what it has, looking.</summary>
        void HoldLooking()
        {
            if (float.IsNaN(_holdAltitudeM)) { _holdAltitudeM = _fs.Position.y; _holdMps = _fs.Velocity.magnitude; }
            Vector3 ahead = new Vector3(_fs.Velocity.x, 0f, _fs.Velocity.z);
            _request = new FlightPathRequest
            {
                AimWorld = _fs.Position + (ahead.sqrMagnitude > 1f ? ahead.normalized : Vector3.forward) * 10000f,
                AltitudeM = _holdAltitudeM,
                SpeedMps = _holdMps,
            };
        }
    }
}
