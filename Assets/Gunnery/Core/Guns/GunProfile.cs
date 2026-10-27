using System;
using DeepWater.Gunnery.Ballistics;

namespace DeepWater.Gunnery.Guns
{
    /// <summary>
    /// One gun as Gunnery sees it (K1, K4): its round, how fast it fires, how many rounds it carries and how widely they
    /// spread. Where it sits and where it points belong to the vehicle that carries it. SI units.
    /// </summary>
    public struct GunProfile
    {
        public string Name;
        /// <summary>The round, as the ballistics model flies it (mass, diameter, muzzle velocity, form factor on G7).</summary>
        public ProjectileData Round;
        public float RoundsPerMinute;
        public int RoundsCarried;
        /// <summary>Dispersion, 1σ, milliradians: the spread of rounds about the bore line.</summary>
        public float DispersionMils;
        /// <summary>Rounds are dropped from the model after this long in flight, s.</summary>
        public float MaxTimeOfFlightS;

        public bool IsSet => RoundsCarried > 0 && RoundsPerMinute > 0f;

        public void Validate()
        {
            if (!IsSet) throw new ArgumentException($"gun {Name}: needs rounds and a rate of fire");
            if (Round.MuzzleVelocityMps <= 0f) throw new ArgumentException($"gun {Name}: muzzle velocity must be positive");
            if (Round.MassKg <= 0f || Round.DiameterM <= 0f) throw new ArgumentException($"gun {Name}: the round needs a mass and a diameter");
            if (DispersionMils < 0f) throw new ArgumentException($"gun {Name}: dispersion cannot be negative");
            if (MaxTimeOfFlightS <= 0f) throw new ArgumentException($"gun {Name}: rounds need a time of flight");
        }
    }

    /// <summary>
    /// A source of uniform draws in [0, 1), injected by the caller so that Gunnery has no randomness of its own: the
    /// caller's seeded generator decides every hit, and a replay of the caller replays the hits.
    /// </summary>
    public interface IUniformSource
    {
        float NextFloat01();
    }
}
