using System;

namespace DeepWater.AirWing.Core.Fidelity
{
    /// <summary>Fidelity levels. Each part defines what its levels mean (BLUEPRINT §4).</summary>
    public enum FidelityLevel : byte
    {
        L0 = 0,
        L1 = 1,
        L2 = 2,
        L3 = 3,
    }

    /// <summary>An aircraft's parts, each with its own ladder. The cockpit is presentation and has none here.</summary>
    public enum PartKind : byte
    {
        Airframe = 0,
        Powerplant = 1,
        FlightControls = 2,
        LandingGear = 3,
        Atmosphere = 4,
    }

    /// <summary>What a fidelity sheet asked for, what it got, and why.</summary>
    public readonly struct FidelityResolution
    {
        public readonly PartKind Part;
        public readonly FidelityLevel Requested;
        public readonly FidelityLevel Resolved;

        public FidelityResolution(PartKind part, FidelityLevel requested, FidelityLevel resolved)
        {
            Part = part;
            Requested = requested;
            Resolved = resolved;
        }

        public bool FellBack => Resolved != Requested;

        public override string ToString() => FellBack
            ? $"{Part}: {Requested} requested, {Resolved} used (highest built)"
            : $"{Part}: {Resolved}";
    }

    /// <summary>
    /// One part's ladder (C14): one builder per built level, from (level, profile) to the part. A request for a
    /// level that isn't built yet runs at the highest built level below it and says so in the resolution, which
    /// the log and the HUD show; it never runs above what was asked. Register a level in the same commit that
    /// adds it and its tests.
    /// </summary>
    public sealed class PartLadder<TProfile, TPart>
    {
        readonly Func<TProfile, TPart>[] _builders = new Func<TProfile, TPart>[LevelCount];

        static readonly int LevelCount = Enum.GetValues(typeof(FidelityLevel)).Length;

        public PartKind Part { get; }

        public PartLadder(PartKind part) => Part = part;

        public PartLadder<TProfile, TPart> Register(FidelityLevel level, Func<TProfile, TPart> builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (_builders[(int)level] != null) throw new InvalidOperationException($"{Part} {level} is already registered.");
            _builders[(int)level] = builder;
            return this;
        }

        public bool IsBuilt(FidelityLevel level) => _builders[(int)level] != null;

        /// <summary>The level a request gets: the highest built level at or below it.</summary>
        public FidelityResolution Resolve(FidelityLevel requested)
        {
            for (int l = (int)requested; l >= 0; l--)
                if (_builders[l] != null)
                    return new FidelityResolution(Part, requested, (FidelityLevel)l);
            throw new InvalidOperationException($"{Part}: no level at or below {requested} is built.");
        }

        public TPart Build(FidelityLevel requested, TProfile profile, out FidelityResolution resolution)
        {
            resolution = Resolve(requested);
            return _builders[(int)resolution.Resolved](profile);
        }
    }
}
