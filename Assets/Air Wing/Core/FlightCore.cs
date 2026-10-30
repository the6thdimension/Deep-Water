using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Radar;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.Gunnery.Guns;
using RH.Shared.Contracts;
using UnityEngine;

namespace DeepWater.AirWing.Core
{
    /// <summary>One aircraft shot down (Gunnery K5): when, by whom, with what.</summary>
    public readonly struct KillRecord
    {
        public readonly long Tick;
        public readonly int VictimId, ShooterId;
        public readonly string Weapon;

        public KillRecord(long tick, int victimId, int shooterId, string weapon)
        {
            Tick = tick;
            VictimId = victimId;
            ShooterId = shooterId;
            Weapon = weapon;
        }

        public override string ToString() => $"tick {Tick} · {ShooterId} shot down {VictimId} ({Weapon})";
    }

    /// <summary>
    /// The flight core's tick (BLUEPRINT §3.2): for each aircraft, in ascending id order, its seat issues this tick's
    /// Pilot Inputs, they go into the input log, and the aircraft steps its substeps; then the guns (Gunnery K4): the
    /// rounds already in flight fly the tick and are tested against every aircraft's path over it, hits take hit
    /// points and may kill, and each aircraft whose trigger is held fires this tick's rounds; then the tick's Fix. With an
    /// <see cref="Core.Arena"/> (W59–W61) the tick opens with its respawns and closes, before the Fix, with its
    /// collisions and its box.
    /// A Unity adapter calls <see cref="Step"/> from FixedUpdate (P2). Same aircraft, same seats issuing the same
    /// inputs, same Fix sequence (W7, W28).
    /// </summary>
    public sealed class FlightCore
    {
        readonly List<Aircraft> _aircraft = new List<Aircraft>();
        readonly List<ISeat> _seats = new List<ISeat>();
        readonly List<AircraftRecord> _records = new List<AircraftRecord>();
        readonly List<AircraftRecord> _picture = new List<AircraftRecord>();
        readonly ISurfaceTruth _surface;
        readonly IContactProbe _probe;
        readonly List<PilotInputs> _issued = new List<PilotInputs>();
        readonly List<GunTarget> _targets = new List<GunTarget>();
        readonly List<HitReport> _tickHits = new List<HitReport>();
        readonly List<HitReport> _hits = new List<HitReport>();
        readonly List<KillRecord> _kills = new List<KillRecord>();
        readonly DeterministicRng _gunDraws;

        /// <summary>Every round in flight (Gunnery). Presentation draws the tracers from it.</summary>
        public readonly RoundsInFlight Rounds = new RoundsInFlight();

        /// <summary>Every game missile in flight, and what each did (W65). Presentation draws them from it.</summary>
        public readonly MissilesInFlight Missiles = new MissilesInFlight();
        readonly DeterministicRng _missileDraws;

        /// <summary>Every hit so far, in tick order.</summary>
        public IReadOnlyList<HitReport> Hits => _hits;

        /// <summary>Every aircraft shot down so far, in tick order.</summary>
        public IReadOnlyList<KillRecord> Kills => _kills;

        public readonly SubstepClock Clock;
        public readonly InputLog Inputs = new InputLog();
        public readonly FixLog Fixes = new FixLog();

        /// <summary>The next tick to run. Ticks count from 0.</summary>
        public long Tick { get; private set; }

        public IReadOnlyList<Aircraft> Aircraft => _aircraft;

        /// <summary>
        /// Every aircraft as the previous tick ended, the same for every seat this tick (RH Aviator A1). Seats are asked
        /// in id order, so reading <see cref="Aircraft"/> mid-tick would show some aircraft a tick ahead of others; a
        /// pilot reads this instead. Empty before the first tick.
        /// </summary>
        public IReadOnlyList<AircraftRecord> Picture => _picture;

        /// <summary>The truth feed's surface (sea or ground height) under any point.</summary>
        public ISurfaceTruth Surface => _surface;

        /// <summary>
        /// The flight core over <paramref name="surface"/>. The landing gear meets the ground through
        /// <paramref name="probe"/> (the scenes pass PhysX's); without one it uses the truth feed's level ground.
        /// </summary>
        public FlightCore(in SubstepClock clock, ISurfaceTruth surface, IContactProbe probe = null, ulong gunnerySeed = 1)
        {
            Clock = clock;
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _probe = probe ?? new SurfaceProbe(surface);
            _gunDraws = new DeterministicRng(gunnerySeed);
            // The missiles draw from their own sequence, so a missile never shifts the guns' luck (or the other way).
            _missileDraws = new DeterministicRng(gunnerySeed ^ 0x6D697373696C6573UL);
            // And the radars from theirs ("radars"), so turning a radar on never shifts a gun's or a missile's luck.
            _radarDraws = new DeterministicRng(gunnerySeed ^ 0x7261646172730000UL);
        }

        readonly List<JetRadar> _radars = new List<JetRadar>();
        readonly DeterministicRng _radarDraws;

        /// <summary>The world the radars look through (SENSORS-CONCEPT J16); default: no sea, no clutter.</summary>
        public RadarEnvironment RadarWorld { get; private set; }

        /// <summary>Set the sea the radars look down on (J16), from the next tick.</summary>
        public void UseRadarWorld(in RadarEnvironment world)
        {
            if (world.Sea && !(world.SeaBeaufort >= 0.0 && world.SeaBeaufort <= 12.0))
                throw new ArgumentOutOfRangeException(nameof(world), world.SeaBeaufort, "Beaufort 0 … 12");
            RadarWorld = world;
        }

        /// <summary>Aircraft <paramref name="id"/>'s radar, or null for a type without one (J1).</summary>
        public JetRadar RadarOf(int id) => _radars[IndexOf(id)];

        /// <summary>Add an aircraft and the seat that flies it. Ids must rise, so the tick order and the Fix are stable.</summary>
        public void Add(Aircraft aircraft, ISeat seat)
        {
            if (aircraft == null) throw new ArgumentNullException(nameof(aircraft));
            if (_aircraft.Count > 0 && aircraft.Id <= _aircraft[_aircraft.Count - 1].Id)
                throw new ArgumentException($"Aircraft ids must rise: {aircraft.Id} after {_aircraft[_aircraft.Count - 1].Id}.", nameof(aircraft));
            _aircraft.Add(aircraft);
            _seats.Add(seat ?? throw new ArgumentNullException(nameof(seat)));
            _radars.Add(aircraft.Profile.Radar.IsSet ? new JetRadar(aircraft.Id, aircraft.Profile.Radar) : null);
        }

        /// <summary>Hand aircraft <paramref name="id"/> to another seat. Takes effect on the next tick (W18).</summary>
        public void SetSeat(int id, ISeat seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            _seats[IndexOf(id)] = seat;
        }

        public ISeat SeatOf(int id) => _seats[IndexOf(id)];

        public Aircraft Find(int id) => _aircraft[IndexOf(id)];

        int IndexOf(int id)
        {
            for (int i = 0; i < _aircraft.Count; i++)
                if (_aircraft[i].Id == id) return i;
            throw new KeyNotFoundException($"No aircraft {id}.");
        }

        /// <summary>The arena's rules (W59–W61), or null: no respawns, no collisions, no box.</summary>
        public Arena Arena { get; private set; }

        /// <summary>Run <paramref name="arena"/>'s rules inside every tick from the next one on (W59–W61). Once only.</summary>
        public void UseArena(Arena arena)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            if (Arena != null && Arena != arena) throw new InvalidOperationException("the flight core already has an arena");
            Arena = arena;
        }

        bool Protected(int id, long tick) => Arena != null && Arena.Protected(id, tick);

        public void Step()
        {
            long tick = Tick;
            // The arena's respawns first, so the picture shows a respawned jet where it now is (W59).
            Arena?.BeforeTick(tick, _aircraft);
            // The picture for this tick: everyone where the last tick left them (A1).
            _picture.Clear();
            foreach (Aircraft a in _aircraft) _picture.Add(new AircraftRecord(a.Id, a.State));
            _issued.Clear();
            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                ISeat seat = _seats[i];
                InputEntry entry = Inputs.Append(tick, a.Id, seat.Kind, seat.Issue(tick, a.Id, a.State));
                float surface = _surface.HeightAt(a.State.Position.x, a.State.Position.z);
                a.Step(entry.Inputs, surface, _probe, Clock);
                _issued.Add(entry.Inputs);
            }
            Guns(tick);
            MissileStage(tick);
            Arena?.AfterTick(tick, _aircraft, _picture);
            RadarStage(tick);
            _records.Clear();
            foreach (Aircraft a in _aircraft) _records.Add(new AircraftRecord(a.Id, a.State));
            Fixes.Append(FlightFix.Of(tick, _records));
            Tick = tick + 1;
        }

        /// <summary>
        /// The guns' part of the tick. Rounds already in flight fly first, against every flying aircraft's path from the
        /// picture to now; then this tick's rounds leave the muzzles, to fly from the next tick. A round fired this tick
        /// therefore never hits on the tick it was fired, which keeps the order of firing out of who gets hit.
        /// </summary>
        void Guns(long tick)
        {
            float dt = (float)Clock.TickSeconds;
            _targets.Clear();
            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                // A protected jet (just respawned, W59) is not a target: rounds pass through it.
                if (a.State.Status != FlightStatus.Flying || !a.Profile.Combat.IsSet || Protected(a.Id, tick)) continue;
                _targets.Add(new GunTarget { Id = a.Id, From = _picture[i].State.Position, To = a.State.Position, RadiusM = a.Profile.Combat.HitRadiusM });
            }
            _tickHits.Clear();
            if (Rounds.Live.Count > 0) Rounds.Step(tick, dt, _targets, _gunDraws, _tickHits);
            foreach (HitReport h in _tickHits)
            {
                _hits.Add(h);
                Aircraft target = Find(h.TargetId);
                if (target.State.Status != FlightStatus.Flying) continue;
                target.State.HitPoints -= h.Rounds;
                if (target.State.HitPoints <= 0f)
                {
                    target.State.HitPoints = 0f;
                    target.Kill(h.ShooterId, $"shot down by {h.ShooterId} ({h.Weapon})");
                    _kills.Add(new KillRecord(tick, h.TargetId, h.ShooterId, h.Weapon));
                }
            }

            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                ref AirframeState s = ref a.State;
                if (!a.Profile.Gun.IsSet || s.Status != FlightStatus.Flying || !_issued[i].Trigger || s.Rounds <= 0 || Protected(a.Id, tick))
                {
                    s.GunCarry = 0f;   // a burst starts from a whole round
                    continue;
                }
                GunMount m = a.Profile.Gun;
                s.GunCarry += m.Gun.RoundsPerMinute / 60f * dt;
                int n = Mathf.Min((int)s.GunCarry, s.Rounds);
                s.GunCarry -= (int)s.GunCarry;
                if (n <= 0) continue;
                s.Rounds -= n;
                Rounds.Fire(a.Id, tick, m.Gun, s.Position + s.Attitude * m.MuzzleM, s.Velocity, s.Attitude * m.BoreBody, n);
            }
        }

        /// <summary>
        /// The radars' part of the tick (J1, J2, J6), after the arena: each jet's radar obeys this tick's command and takes
        /// the looks due on this tick, at where everyone now is, in id order (so the draws repeat).
        /// </summary>
        void RadarStage(long tick)
        {
            double tickS = Math.Round(Clock.TickSeconds, 6);
            for (int i = 0; i < _aircraft.Count; i++)
                _radars[i]?.Step(tick, tickS, _aircraft[i], _issued[i], _aircraft, _surface, RadarWorld, _radarDraws);
        }

        int IndexOrNone(int id)
        {
            for (int i = 0; i < _aircraft.Count; i++) if (_aircraft[i].Id == id) return i;
            return -1;
        }

        /// <summary>
        /// The game missiles' part of the tick (W65), after the guns. First the missiles already in flight fly their
        /// substeps after their target's path over the tick (from the picture to now), and each one armed that passed
        /// within its lethal radius of its target bursts: one seeded draw, a kill or the damage. A missile whose target
        /// left its seeker cone, went down or respawned is lost; one past its life, or at the surface, expires. Then each
        /// jet's lock: the aircraft nearest its nose inside the lock cone and range, held for the lock time. Last, each
        /// jet whose pickle is pressed, with a lock, a missile and its interval run out, launches from its next pylon; that
        /// missile flies from the next tick. Protected jets (W59) can't be locked, hit or launch.
        /// </summary>
        void MissileStage(long tick)
        {
            float dt = (float)Clock.TickSeconds;
            int steps = Clock.Substeps;
            float sdt = dt / steps;
            List<MissileInFlight> live = Missiles.Mutable;
            int kept = 0;
            for (int i = 0; i < live.Count; i++)
            {
                MissileInFlight m = live[i];
                MissileProfile p = Find(m.ShooterId).Profile.Missiles.Missile;
                int ti = IndexOrNone(m.TargetId);
                Aircraft target = ti >= 0 ? _aircraft[ti] : null;
                Vector3 tFrom = ti >= 0 ? _picture[ti].State.Position : m.Position, tTo = target != null ? target.State.Position : m.Position;
                bool trackable = target != null && target.State.Status == FlightStatus.Flying && target.Resets == m.TargetResets;
                Vector3 from = m.Position;
                for (int k = 0; k < steps; k++)
                {
                    Vector3 accel = Vector3.zero;
                    if (!m.Lost)
                    {
                        Vector3 tPos = Vector3.Lerp(tFrom, tTo, (k + 1f) / steps);
                        if (!trackable || !MissileGuidance.InCone(m.Position, m.Velocity, tPos, p.SeekerConeDeg))
                        {
                            m.Lost = true;
                            Missiles.Log(new MissileEvent(tick, m.Id, m.ShooterId, m.TargetId, MissileEventKind.Lost, m.Position, false));
                        }
                        else accel = MissileGuidance.Command(m.Position, m.Velocity, tPos, target.State.Velocity, p.NavGain, p.MaxG);
                    }
                    m.Velocity = MissileGuidance.Step(m.Velocity, accel, m.AgeS, p, sdt);
                    m.Position += m.Velocity * sdt;
                    m.AgeS += sdt;
                }

                if (!m.Lost && trackable && m.AgeS >= p.ArmS && !Protected(target.Id, tick)
                    && Arena.ClosestApproachSq(from, m.Position, tFrom, tTo) <= p.LethalRadiusM * p.LethalRadiusM)
                {
                    bool killed = _missileDraws.NextFloat01() < p.KillChance;
                    ref AirframeState ts = ref target.State;
                    if (!killed) ts.HitPoints -= p.DamageHp;
                    if (killed || ts.HitPoints <= 0f)
                    {
                        killed = true;
                        ts.HitPoints = 0f;
                        target.Kill(m.ShooterId, $"shot down by {m.ShooterId} ({p.Name})");
                        _kills.Add(new KillRecord(tick, target.Id, m.ShooterId, p.Name));
                    }
                    _hits.Add(new HitReport(tick, m.ShooterId, target.Id, p.Name, 1, m.Position, (m.Velocity - ts.Velocity).magnitude));
                    Missiles.Log(new MissileEvent(tick, m.Id, m.ShooterId, target.Id, MissileEventKind.Burst, m.Position, killed));
                    continue;
                }
                if (m.AgeS >= p.LifeS || m.Position.y <= _surface.HeightAt(m.Position.x, m.Position.z))
                {
                    Missiles.Log(new MissileEvent(tick, m.Id, m.ShooterId, m.TargetId, MissileEventKind.Expired, m.Position, false));
                    continue;
                }
                live[kept++] = m;
            }
            live.RemoveRange(kept, live.Count - kept);

            // Locks, then launches.
            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                ref AirframeState s = ref a.State;
                MissileMount mount = a.Profile.Missiles;
                if (s.MissileCooldownTicks > 0) s.MissileCooldownTicks--;
                if (!mount.IsSet || s.Status != FlightStatus.Flying || s.Missiles <= 0)
                {
                    s.LockTargetId = 0;
                    s.LockTicks = 0;
                    continue;
                }
                MissileProfile p = mount.Missile;
                // The seeker holds the jet it is on while that one stays inside the lock cone and range; only then does it
                // take the one nearest the nose (2026-10-29: taking the nearest every tick, in an 8 v 8 it hopped between
                // jets ahead and seldom held one for the lock's second).
                if (s.LockTargetId != 0 && Lockable(i, IndexOrNone(s.LockTargetId), p, tick)) s.LockTicks = Mathf.Min(s.LockTicks + 1, LockTicksFor(p));
                else
                {
                    int best = LockCandidate(i, p, tick);
                    s.LockTargetId = best;
                    s.LockTicks = best != 0 ? 1 : 0;
                }

                if (!_issued[i].Pickle || s.MissileCooldownTicks > 0 || s.LockTargetId == 0 || s.LockTicks < LockTicksFor(p) || Protected(a.Id, tick)) continue;
                int pylon = mount.Carried - s.Missiles;
                Aircraft locked = Find(s.LockTargetId);
                Missiles.Launch(tick, a.Id, locked.Id, locked.Resets, p.Name, s.Position + s.Attitude * mount.PylonsM[pylon], s.Velocity);
                s.Missiles--;
                s.MissileCooldownTicks = Mathf.Max(1, Mathf.RoundToInt(p.LaunchIntervalS / dt));
            }
        }

        /// <summary>
        /// <paramref name="a"/>'s missile seeker has held its target for the lock time: a launch would go now (given a
        /// missile and the interval). The one test the HUD, the AI pilots and the learned pilot's senses all ask.
        /// </summary>
        public bool IsLocked(Aircraft a) =>
            a.Profile.Missiles.IsSet && a.State.LockTargetId != 0 && a.State.LockTicks >= LockTicksFor(a.Profile.Missiles.Missile);

        /// <summary>Ticks a lock must be held: the lock time on the tick count (AP26).</summary>
        int LockTicksFor(in MissileProfile p) => Math.Max(1, (int)Math.Round(p.LockTimeS / Math.Round(Clock.TickSeconds, 6)));

        /// <summary>Aircraft <paramref name="j"/> (an index) is inside aircraft <paramref name="i"/>'s lock cone and range, flying and unprotected.</summary>
        bool Lockable(int i, int j, in MissileProfile p, long tick)
        {
            if (j < 0 || j == i) return false;
            Aircraft o = _aircraft[j];
            if (o.State.Status != FlightStatus.Flying || Protected(o.Id, tick)) return false;
            AirframeState me = _aircraft[i].State;
            Vector3 r = o.State.Position - me.Position;
            float d = r.magnitude;
            return d >= p.LockMinM && d <= p.LockMaxM && Vector3.Dot(me.Attitude * Vector3.forward, r / d) >= Mathf.Cos(p.LockConeDeg * Mathf.Deg2Rad);
        }

        /// <summary>The flying aircraft nearest aircraft <paramref name="i"/>'s nose, inside its lock cone and range (0: none).</summary>
        int LockCandidate(int i, in MissileProfile p, long tick)
        {
            AirframeState me = _aircraft[i].State;
            Vector3 nose = me.Attitude * Vector3.forward;
            float bestCos = Mathf.Cos(p.LockConeDeg * Mathf.Deg2Rad);
            int best = 0;
            for (int j = 0; j < _aircraft.Count; j++)
            {
                if (j == i) continue;
                Aircraft o = _aircraft[j];
                if (o.State.Status != FlightStatus.Flying || Protected(o.Id, tick)) continue;
                Vector3 r = o.State.Position - me.Position;
                float d = r.magnitude;
                if (d < p.LockMinM || d > p.LockMaxM) continue;
                float c = Vector3.Dot(nose, r / d);
                if (c >= bestCos) { bestCos = c; best = o.Id; }
            }
            return best;
        }
    }
}
