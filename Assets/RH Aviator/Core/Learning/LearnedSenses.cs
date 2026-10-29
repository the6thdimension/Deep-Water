using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.State;
using DeepWater.Gunnery.Guns;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// What a learned pilot sees (M7, M23): a fixed vector of <see cref="Size"/> numbers, written every decision. Its own
    /// state (its own instruments: the truth about itself, as a rule pilot has), then the nearest
    /// <see cref="EnemySlots"/> enemies and <see cref="FriendSlots"/> friends <b>as its eyes deliver them</b> (field of view,
    /// reaction delay, memory: <see cref="Perception"/>), then the pipper on the nearest enemy in view, as the player's
    /// pipper shows it. Never the truth about anyone else (TRAINING-CONCEPT standing constraint 2).
    /// <para>The layout and its scales are the observation contract: a trained policy only reads vectors of the
    /// <see cref="Version"/> it was trained on, so a change here bumps the version (M19 keeps it with each policy).
    /// They are structure, not tuning, and live here rather than in an SO.</para>
    /// <para>Layout, in order (vectors in the pilot's body frame: +x right, +y up, +z nose):
    /// own 12: speed, height above the surface, climb rate, nose-up sine, bank sine and cosine, G, angle of attack,
    /// rounds left, hit points left, engine, protected;
    /// each enemy 12: present, in view now, age of the sighting, relative position (3), range, relative velocity (3),
    /// "on its tail" (cosine from its tail to me), "pointing at me" (cosine from its nose to me);
    /// each friend 8: present, in view now, relative position (3), relative velocity (3);
    /// pipper 4: a solution, the miss in mils, the miss's direction across the bore (x, y).
    /// Absent contacts are zeros, so "present" tells the network a slot is empty.</para>
    /// </summary>
    public sealed class LearnedSenses
    {
        public const int Version = 1;
        public const int EnemySlots = 3, FriendSlots = 2;
        public const int OwnSize = 12, EnemySize = 12, FriendSize = 8, PipperSize = 4;
        public const int Size = OwnSize + EnemySlots * EnemySize + FriendSlots * FriendSize + PipperSize;   // 68

        // Scales (Version 1): each brings its number to about ±1 in a fight.
        const float SpeedScaleMps = 300f, HeightScaleM = 3000f, ClimbScaleMps = 100f, GScale = 7.5f, AoaScaleDeg = 20f;
        const float PositionScaleM = 3000f, RangeScaleM = 10000f, VelocityScaleMps = 300f, MissScaleMils = 30f, Clip = 5f;
        /// <summary>The pipper is worked on an enemy in view inside this range, metres (past it the gun is no use).</summary>
        public const float PipperRangeM = 2000f;

        readonly float[] _values = new float[Size];
        readonly List<(float range, int id)> _enemies = new List<(float, int)>(8), _friends = new List<(float, int)>(8);

        /// <summary>The vector written last.</summary>
        public float[] Values => _values;
        /// <summary>The nearest enemy in view inside <see cref="PipperRangeM"/> at the last write (0: none), its guessed position and the miss, mils.</summary>
        public int PipperTarget { get; private set; }
        public Vector3 PipperTargetGuess { get; private set; }
        public float PipperMissMils { get; private set; } = float.PositiveInfinity;
        /// <summary>The nearest enemy it knows of at the last write (0: none), and where it guesses it is.</summary>
        public int NearestEnemy { get; private set; }
        public Vector3 NearestEnemyGuess { get; private set; }
        /// <summary>The nearest enemy's velocity as last seen.</summary>
        public Vector3 NearestEnemyVelocity { get; private set; }

        /// <summary>
        /// Writes what aircraft <paramref name="selfId"/> sees now into <see cref="Values"/>. <paramref name="eyes"/> are
        /// its seat's; <paramref name="fight"/> says who is a friend (a side kept after a jet goes down, so a remembered
        /// wreck stays an enemy until forgotten). Allocates nothing after the first calls.
        /// </summary>
        public float[] Write(FlightCore core, int selfId, Perception eyes, Engagement fight, in PilotProfile pilot, bool protectedNow)
        {
            Array.Clear(_values, 0, Size);
            Aircraft self = core.Find(selfId);
            AirframeState me = self.State;
            AircraftProfile profile = self.Profile;
            long tick = core.Tick;
            double dt = Math.Round(core.Clock.TickSeconds, 6);
            Quaternion toBody = Quaternion.Inverse(me.Attitude);
            Vector3 up = me.Attitude * Vector3.up, right = me.Attitude * Vector3.right, nose = me.Attitude * Vector3.forward;

            int i = 0;
            _values[i++] = me.Velocity.magnitude / SpeedScaleMps;
            _values[i++] = Bound((me.Position.y - core.Surface.HeightAt(me.Position.x, me.Position.z)) / HeightScaleM);
            _values[i++] = Bound(me.Velocity.y / ClimbScaleMps);
            _values[i++] = nose.y;
            float bank = Mathf.Atan2(-right.y, up.y);
            _values[i++] = Mathf.Sin(bank);
            _values[i++] = Mathf.Cos(bank);
            _values[i++] = Bound(me.LoadFactor / GScale);
            _values[i++] = Bound(me.AngleOfAttack * Mathf.Rad2Deg / AoaScaleDeg);
            _values[i++] = profile.Gun.IsSet ? me.Rounds / (float)Mathf.Max(1, profile.Gun.Gun.RoundsCarried) : 0f;
            _values[i++] = profile.Combat.IsSet ? me.HitPoints / profile.Combat.HitPoints : 0f;
            _values[i++] = me.EngineSpool + me.AfterburnerSpool;
            _values[i++] = protectedNow ? 1f : 0f;

            // Sort what the eyes know into enemies and friends, nearest first (ties by id, so the order is stable).
            _enemies.Clear();
            _friends.Clear();
            NearestEnemy = 0;
            Side mine = fight.SideEver(selfId);
            IReadOnlyList<int> known = eyes.Known;
            for (int k = 0; k < known.Count; k++)
            {
                if (!eyes.TryGet(known[k], out Contact c)) continue;
                Side theirs = fight.SideEver(c.Id);
                if (theirs == Side.None || mine == Side.None) continue;
                float range = (c.GuessPosition(tick, dt) - me.Position).magnitude;
                (theirs == mine ? _friends : _enemies).Add((range, c.Id));
            }
            _enemies.Sort();
            _friends.Sort();

            PipperTarget = 0;
            PipperMissMils = float.PositiveInfinity;
            for (int e = 0; e < EnemySlots; e++, i += EnemySize)
            {
                if (e >= _enemies.Count) continue;
                eyes.TryGet(_enemies[e].id, out Contact c);
                Vector3 guess = c.GuessPosition(tick, dt);
                Vector3 rel = guess - me.Position;
                if (e == 0) { NearestEnemy = c.Id; NearestEnemyGuess = guess; NearestEnemyVelocity = c.Seen.Velocity; }
                if (c.InView && PipperTarget == 0 && rel.magnitude <= PipperRangeM) { PipperTarget = c.Id; PipperTargetGuess = guess; }
                Vector3 fromThem = rel.sqrMagnitude > 1f ? -rel.normalized : Vector3.zero;
                Vector3 theirTail = c.Seen.Velocity.sqrMagnitude > 1f ? -c.Seen.Velocity.normalized : c.Seen.Attitude * Vector3.back;
                int j = i;
                _values[j++] = 1f;
                _values[j++] = c.InView ? 1f : 0f;
                _values[j++] = Mathf.Clamp01((float)((tick - c.SeenTick) * dt) / Mathf.Max(pilot.MemoryS, 0.1f));
                Body(toBody * rel, PositionScaleM, ref j);
                _values[j++] = Mathf.Min(rel.magnitude / RangeScaleM, 2f);
                Body(toBody * (c.Seen.Velocity - me.Velocity), VelocityScaleMps, ref j);
                _values[j++] = Vector3.Dot(theirTail, fromThem);
                _values[j++] = Vector3.Dot(c.Seen.Attitude * Vector3.forward, fromThem);
            }
            for (int f = 0; f < FriendSlots; f++, i += FriendSize)
            {
                if (f >= _friends.Count) continue;
                eyes.TryGet(_friends[f].id, out Contact c);
                int j = i;
                _values[j++] = 1f;
                _values[j++] = c.InView ? 1f : 0f;
                Body(toBody * (c.GuessPosition(tick, dt) - me.Position), PositionScaleM, ref j);
                Body(toBody * (c.Seen.Velocity - me.Velocity), VelocityScaleMps, ref j);
            }

            // The pipper, as the player's shows it: where this burst would pass the nearest enemy in view.
            if (PipperTarget != 0 && profile.Gun.IsSet)
            {
                eyes.TryGet(PipperTarget, out Contact t);
                GunMount m = profile.Gun;
                Vector3 muzzle = me.Position + me.Attitude * m.MuzzleM;
                float miss = LeadSight.MissDistance(m.Gun, muzzle, me.Velocity, me.Attitude * m.BoreBody, PipperTargetGuess, t.Seen.Velocity, out Vector3 off, out _);
                float range = Mathf.Max((PipperTargetGuess - muzzle).magnitude, 1f);
                if (!float.IsInfinity(miss))
                {
                    PipperMissMils = miss / range * 1000f;
                    Vector3 across = toBody * off;
                    float n = Mathf.Max(new Vector2(across.x, across.y).magnitude, 1e-3f);
                    _values[i] = 1f;
                    _values[i + 1] = Mathf.Min(PipperMissMils / MissScaleMils, 3f);
                    _values[i + 2] = across.x / n;
                    _values[i + 3] = across.y / n;
                }
            }
            i += PipperSize;
            if (i != Size) throw new InvalidOperationException($"learned senses wrote {i} numbers, not {Size}");
            return _values;
        }

        void Body(Vector3 v, float scale, ref int j)
        {
            _values[j++] = Bound(v.x / scale);
            _values[j++] = Bound(v.y / scale);
            _values[j++] = Bound(v.z / scale);
        }

        static float Bound(float v) => Mathf.Clamp(v, -Clip, Clip);
    }
}
