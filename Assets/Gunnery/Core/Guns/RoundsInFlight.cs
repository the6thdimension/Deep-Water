using System.Collections.Generic;
using DeepWater.Gunnery.Ballistics;
using RH.Shared.Contracts;
using UnityEngine;

namespace DeepWater.Gunnery.Guns
{
    /// <summary>
    /// One tick's worth of rounds from one gun, flown together (K4): a shot group. Its rounds share a mean path; they
    /// spread about it by the gun's dispersion, which grows with the distance flown.
    /// </summary>
    public struct Volley
    {
        public int ShooterId;
        public long FiredTick;
        public string Weapon;
        public Vector3 Position, Velocity;
        public int Rounds;
        public float AgeS;
        /// <summary>Distance flown along the mean path, m.</summary>
        public float FlownM;
        public float DispersionRad;
        public float DragK;
        public float MaxTimeOfFlightS;

        /// <summary>The spread of the rounds about the mean path here, 1σ, m.</summary>
        public float SigmaM => DispersionRad * FlownM;
    }

    /// <summary>Something rounds can hit this tick: a sphere moving in a straight line from <see cref="From"/> to <see cref="To"/>.</summary>
    public struct GunTarget
    {
        public int Id;
        public Vector3 From, To;
        public float RadiusM;
    }

    /// <summary>
    /// Every round in flight (K4). Each tick it flies the volleys, then tests each against every target over the tick
    /// (closest approach of the two straight-line motions), and turns a close pass into hits: each round in the volley
    /// hits with <see cref="ShotGroup.RoundHitProbability"/> at the miss distance, drawn from the caller's seeded source.
    /// A volley that hit loses those rounds and flies on. Deterministic: volleys in firing order, targets in the caller's
    /// order, every draw from the caller. No allocation after warm-up.
    /// </summary>
    public sealed class RoundsInFlight
    {
        readonly List<Volley> _live = new List<Volley>(256);

        /// <summary>A volley is tested only while the pass is within this many σ of a target's edge.</summary>
        public const float SigmaReach = 3f;
        /// <summary>Volleys below this height are dropped (the caller's world has no surface here; rounds that low hit it).</summary>
        public float FloorM = -100f;

        public IReadOnlyList<Volley> Live => _live;

        /// <summary>Rounds fired since the model was made, and rounds that hit something.</summary>
        public long RoundsFired { get; private set; }
        public long RoundsHit { get; private set; }

        /// <summary>
        /// Fire <paramref name="rounds"/> from <paramref name="muzzle"/>: the volley leaves at the shooter's velocity plus
        /// the muzzle velocity along <paramref name="boreWorld"/>.
        /// </summary>
        public void Fire(int shooterId, long tick, in GunProfile gun, Vector3 muzzle, Vector3 shooterVelocity, Vector3 boreWorld, int rounds)
        {
            if (rounds <= 0) return;
            _live.Add(new Volley
            {
                ShooterId = shooterId,
                FiredTick = tick,
                Weapon = gun.Name,
                Position = muzzle,
                Velocity = shooterVelocity + boreWorld.normalized * gun.Round.MuzzleVelocityMps,
                Rounds = rounds,
                DispersionRad = gun.DispersionMils * 1e-3f,
                DragK = RoundFlight.DragConstant(gun.Round),
                MaxTimeOfFlightS = gun.MaxTimeOfFlightS,
            });
            RoundsFired += rounds;
        }

        /// <summary>
        /// Fly every volley <paramref name="dt"/> seconds and test it against <paramref name="targets"/> (moving over the
        /// same <paramref name="dt"/>). Hits are appended to <paramref name="hits"/>, in volley then target order.
        /// </summary>
        public void Step(long tick, float dt, IReadOnlyList<GunTarget> targets, IUniformSource draws, List<HitReport> hits)
        {
            int keep = 0;
            for (int i = 0; i < _live.Count; i++)
            {
                Volley v = _live[i];
                Vector3 from = v.Position;
                RoundFlight.Step(ref v.Position, ref v.Velocity, v.DragK, dt);
                v.AgeS += dt;
                v.FlownM += (v.Position - from).magnitude;

                for (int t = 0; t < targets.Count && v.Rounds > 0; t++)
                {
                    GunTarget g = targets[t];
                    if (g.Id == v.ShooterId) continue;
                    // Closest approach of the volley's segment and the target's, both straight over the tick.
                    Vector3 r0 = from - g.From, r1 = v.Position - g.To, dr = r1 - r0;
                    float s = dr.sqrMagnitude > 1e-9f ? Mathf.Clamp01(-Vector3.Dot(r0, dr) / dr.sqrMagnitude) : 0f;
                    float miss = (r0 + dr * s).magnitude;
                    float sigma = v.SigmaM;
                    if (miss > g.RadiusM + SigmaReach * sigma) continue;

                    float p = ShotGroup.RoundHitProbability(g.RadiusM, sigma, miss);
                    int hit = 0;
                    for (int k = 0; k < v.Rounds; k++)
                        if (draws.NextFloat01() < p) hit++;
                    if (hit == 0) continue;
                    v.Rounds -= hit;
                    RoundsHit += hit;
                    Vector3 point = Vector3.Lerp(from, v.Position, s);
                    Vector3 targetVelocity = dt > 0f ? (g.To - g.From) / dt : Vector3.zero;
                    hits.Add(new HitReport(tick, v.ShooterId, g.Id, v.Weapon, hit, point, (v.Velocity - targetVelocity).magnitude));
                }

                if (v.Rounds > 0 && v.AgeS < v.MaxTimeOfFlightS && v.Position.y > FloorM) _live[keep++] = v;
            }
            _live.RemoveRange(keep, _live.Count - keep);
        }

        public void Clear() => _live.Clear();
    }
}
