using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepWater.AirWing.Core.Missiles
{
    /// <summary>How a game missile finds its target (W65, W68).</summary>
    public enum MissileSeeker : byte
    {
        /// <summary>Its own heat seeker from launch (W65): the jet's lock is its seeker's.</summary>
        Heat = 0,
        /// <summary>
        /// Active radar (SENSORS-CONCEPT J7): launched on the shooter's single-target track, it flies on that track's
        /// estimate until it is <see cref="MissileProfile.ActiveRangeM"/> from it, then its own radar takes over.
        /// </summary>
        ActiveRadar = 1,
    }

    /// <summary>
    /// A game missile (Air Wing W65, SIX 2026-10-29: "make game missile sufficient for video game air to air"): a generic
    /// archetype with arcade numbers, every one AUTHORED: the short-range heat-seeker (W65) or the medium-range active radar
    /// missile (W68). Not any real missile; no real data.
    /// </summary>
    [Serializable]
    public struct MissileProfile
    {
        public string Name;
        /// <summary>How it finds its target.</summary>
        public MissileSeeker Seeker;
        /// <summary>An active radar missile: its own radar takes over this far from the target's estimated position, m.</summary>
        public float ActiveRangeM;
        /// <summary>The motor: this acceleration along the missile's path for <see cref="BurnS"/> seconds, m/s².</summary>
        public float BoostAccelMps2, BurnS;
        /// <summary>Drag as a deceleration k·v², 1/m: after the burn the missile coasts and slows.</summary>
        public float DragPerM;
        /// <summary>The most sideways acceleration it can pull, g.</summary>
        public float MaxG;
        /// <summary>Proportional navigation's gain N (a = N·Vc·λ̇; 3–5 is the textbook range).</summary>
        public float NavGain;
        /// <summary>Its seeker's cone, half-angle from its own path, degrees: a target outside it is lost for good.</summary>
        public float SeekerConeDeg;
        /// <summary>
        /// The lock (before launch): a target inside this half-angle of the jet's nose, between these ranges, held for this
        /// long, metres and seconds.
        /// </summary>
        public float LockConeDeg, LockMinM, LockMaxM, LockTimeS;
        /// <summary>Seconds of flight before the fuse arms, and before it self-destructs.</summary>
        public float ArmS, LifeS;
        /// <summary>The proximity fuse's radius, m; the chance a burst kills outright, 0 … 1; the hit points it takes otherwise.</summary>
        public float LethalRadiusM, KillChance, DamageHp;
        /// <summary>The least time between two launches from one jet, s.</summary>
        public float LaunchIntervalS;

        public bool IsSet => !string.IsNullOrEmpty(Name);

        public void Validate()
        {
            if (!IsSet) throw new ArgumentException("missile: no name");
            Positive(BoostAccelMps2, nameof(BoostAccelMps2)); Positive(BurnS, nameof(BurnS)); Positive(MaxG, nameof(MaxG));
            Positive(NavGain, nameof(NavGain)); Positive(LifeS, nameof(LifeS)); Positive(LethalRadiusM, nameof(LethalRadiusM));
            Positive(LockMaxM, nameof(LockMaxM)); Positive(LaunchIntervalS, nameof(LaunchIntervalS));
            if (!(DragPerM >= 0f)) throw new ArgumentException("missile: drag must be zero or more");
            if (!(SeekerConeDeg > 0f && SeekerConeDeg < 90f && LockConeDeg > 0f && LockConeDeg <= SeekerConeDeg))
                throw new ArgumentException("missile: cones must be 0 … 90°, the lock cone inside the seeker's");
            if (!(LockMinM >= 0f && LockMinM < LockMaxM)) throw new ArgumentException("missile: lock ranges need 0 ≤ min < max");
            if (!(LockTimeS >= 0f && ArmS >= 0f && ArmS < LifeS)) throw new ArgumentException("missile: the lock time and arming are zero or more, arming inside the life");
            if (!(KillChance >= 0f && KillChance <= 1f && DamageHp >= 0f)) throw new ArgumentException("missile: kill chance 0 … 1, damage zero or more");
            if (Seeker == MissileSeeker.ActiveRadar && !(ActiveRangeM > 0f)) throw new ArgumentException("missile: an active radar missile needs its active range");
        }

        static void Positive(float v, string name)
        {
            if (!(v > 0f) || float.IsInfinity(v)) throw new ArgumentException($"missile: {name} must be positive, not {v}");
        }
    }

    /// <summary>
    /// The missiles an aircraft carries (W65, W68): the heat-seeker and its pylons, and the radar missile and its pylons, in
    /// the body frame, one missile each. Either may be absent.
    /// </summary>
    public struct MissileMount
    {
        public MissileProfile Missile;
        /// <summary>Pylon positions about the centre of mass, body frame (Unity axes), m. Fired in this order.</summary>
        public Vector3[] PylonsM;
        /// <summary>The active radar missile (W68, SENSORS-CONCEPT J7, J18); unset for a jet that carries none.</summary>
        public MissileProfile RadarMissile;
        public Vector3[] RadarPylonsM;

        public bool IsSet => Missile.IsSet && PylonsM != null && PylonsM.Length > 0;
        public int Carried => IsSet ? PylonsM.Length : 0;
        public bool HasRadarMissiles => RadarMissile.IsSet && RadarPylonsM != null && RadarPylonsM.Length > 0;
        public int RadarCarried => HasRadarMissiles ? RadarPylonsM.Length : 0;

        public void Validate()
        {
            Missile.Validate();
            if (PylonsM == null || PylonsM.Length == 0) throw new ArgumentException("missile mount: no pylons");
            if (Missile.Seeker != MissileSeeker.Heat) throw new ArgumentException("missile mount: the pickle's missile is the heat-seeker");
            if (RadarMissile.IsSet)
            {
                RadarMissile.Validate();
                if (RadarMissile.Seeker != MissileSeeker.ActiveRadar) throw new ArgumentException("missile mount: the radar missile must be active radar");
                if (RadarPylonsM == null || RadarPylonsM.Length == 0) throw new ArgumentException("missile mount: a radar missile with no pylons");
            }
        }
    }

    /// <summary>One missile in flight.</summary>
    public struct MissileInFlight
    {
        public int Id, ShooterId, TargetId;
        /// <summary>The target's reset count at launch: a target that respawns is a different jet to the seeker.</summary>
        public int TargetResets;
        public long LaunchTick;
        public Vector3 Position, Velocity;
        public float AgeS;
        /// <summary>Its seeker has lost the target: it flies on, unguided and unfused, until its life ends.</summary>
        public bool Lost;
        public string Name;
        /// <summary>An active radar missile (W68), and whether its own radar has taken over.</summary>
        public bool Radar, Active;
        /// <summary>The shooter's reset count at launch: a radar missile flying on the shooter's track dies with it.</summary>
        public int ShooterResets;
    }

    public enum MissileEventKind : byte { Launched = 0, Burst = 1, Lost = 2, Expired = 3, Active = 4 }

    /// <summary>Something a missile did, on a tick.</summary>
    public readonly struct MissileEvent
    {
        public readonly long Tick;
        public readonly int MissileId, ShooterId, TargetId;
        public readonly MissileEventKind Kind;
        public readonly Vector3 Point;
        /// <summary>A burst that killed its target outright (else it took <see cref="MissileProfile.DamageHp"/>).</summary>
        public readonly bool Killed;

        public MissileEvent(long tick, int missileId, int shooterId, int targetId, MissileEventKind kind, Vector3 point, bool killed)
        {
            Tick = tick;
            MissileId = missileId;
            ShooterId = shooterId;
            TargetId = targetId;
            Kind = kind;
            Point = point;
            Killed = killed;
        }

        public override string ToString() => Kind switch
        {
            MissileEventKind.Launched => $"tick {Tick} · {ShooterId} launched missile {MissileId} at {TargetId}",
            MissileEventKind.Burst => $"tick {Tick} · missile {MissileId} ({ShooterId}) burst at {TargetId}{(Killed ? ", a kill" : "")}",
            MissileEventKind.Lost => $"tick {Tick} · missile {MissileId} ({ShooterId}) lost {TargetId}",
            MissileEventKind.Active => $"tick {Tick} · missile {MissileId} ({ShooterId}) went active on {TargetId}",
            _ => $"tick {Tick} · missile {MissileId} ({ShooterId}) expired",
        };
    }

    /// <summary>
    /// The game missile's law (W65), pure: proportional navigation, a = N·Vc·(Ω × r̂) with Ω the line of sight's turn rate,
    /// taken sideways to the missile's path and capped at its G. Opening on its target (Vc ≤ 0) it turns straight for it
    /// at the cap. No gravity: an arcade missile. A textbook law with game numbers.
    /// </summary>
    public static class MissileGuidance
    {
        const float G0 = 9.80665f;

        /// <summary>The sideways acceleration commanded, world frame, m/s².</summary>
        public static Vector3 Command(Vector3 missilePos, Vector3 missileVel, Vector3 targetPos, Vector3 targetVel, float navGain, float maxG)
        {
            Vector3 r = targetPos - missilePos;
            float range = r.magnitude;
            float speed = missileVel.magnitude;
            if (range < 1e-3f || speed < 1e-3f) return Vector3.zero;
            Vector3 los = r / range, along = missileVel / speed;
            Vector3 v = targetVel - missileVel;
            float closing = -Vector3.Dot(v, los);
            Vector3 a;
            if (closing > 0f)
            {
                Vector3 omega = Vector3.Cross(r, v) / (range * range);
                a = navGain * closing * Vector3.Cross(omega, los);
            }
            else a = (los - along * Vector3.Dot(los, along)) * (maxG * G0 * 10f);   // opening: hard for it (capped below)
            a -= along * Vector3.Dot(a, along);   // sideways only: the motor and drag set the speed
            float cap = maxG * G0;
            return a.sqrMagnitude > cap * cap ? a.normalized * cap : a;
        }

        /// <summary>
        /// One step of flight: the commanded acceleration turns the path, the motor (during the burn) and drag (k·v²) set
        /// the speed. Returns the new velocity.
        /// </summary>
        public static Vector3 Step(Vector3 velocity, Vector3 lateralAccel, float ageS, in MissileProfile p, float dt)
        {
            float speed = velocity.magnitude;
            float accel = (ageS < p.BurnS ? p.BoostAccelMps2 : 0f) - p.DragPerM * speed * speed;
            Vector3 turned = velocity + lateralAccel * dt;
            float newSpeed = Mathf.Max(1f, speed + accel * dt);
            return turned.sqrMagnitude > 1e-6f ? turned.normalized * newSpeed : velocity;
        }

        /// <summary>Is <paramref name="targetPos"/> inside <paramref name="coneDeg"/> of the direction <paramref name="axis"/> from <paramref name="from"/>?</summary>
        public static bool InCone(Vector3 from, Vector3 axis, Vector3 targetPos, float coneDeg)
        {
            Vector3 r = targetPos - from;
            float d = r.magnitude, a = axis.magnitude;
            if (d < 1e-3f || a < 1e-6f) return false;
            return Vector3.Dot(axis / a, r / d) >= Mathf.Cos(coneDeg * Mathf.Deg2Rad);
        }
    }
}
