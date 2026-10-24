namespace DeepWater.GeneralQuarters.Core.Fidelity
{
    /// <summary>Fidelity levels. Each element defines what its levels mean (BLUEPRINT §4).</summary>
    public enum FidelityLevel : byte
    {
        L0 = 0,
        L1 = 1,
        L2 = 2,
        L3 = 3,
        L4 = 4,
        L5 = 5,
    }

    /// <summary>The ship's elements, each with its own ladder.</summary>
    public enum ElementKind : byte
    {
        Hull = 0,
        EngineRoom = 1,
        Helm = 2,
        Radar = 3,
        Illuminators = 4,
        Tracks = 5,
        Tewa = 6,
        Vls = 7,
        Guns = 8,
        AntiShip = 9,
        DamageControl = 10,
        AiCaptain = 11,
    }

    /// <summary>What a loadout asked for, what it got, and why.</summary>
    public readonly struct FidelityResolution
    {
        public readonly ElementKind Element;
        public readonly FidelityLevel Requested;
        public readonly FidelityLevel Resolved;

        public FidelityResolution(ElementKind element, FidelityLevel requested, FidelityLevel resolved)
        {
            Element = element;
            Requested = requested;
            Resolved = resolved;
        }

        public bool FellBack => Resolved != Requested;

        public override string ToString() => FellBack
            ? $"{Element}: {Requested} requested, {Resolved} used (highest implemented)"
            : $"{Element}: {Resolved}";
    }

    /// <summary>
    /// The one table of which levels exist. Factories resolve through it, so a loadout asking for a level
    /// that isn't built yet runs at the highest built level and says so, instead of failing or pretending.
    /// Raise an element's entry in the same commit that adds its level and that level's tests.
    /// </summary>
    public static class FidelityLadder
    {
        public static FidelityLevel HighestImplemented(ElementKind element)
        {
            switch (element)
            {
                // Phase 1: every element starts at L0.
                default: return FidelityLevel.L0;
            }
        }

        public static FidelityResolution Resolve(ElementKind element, FidelityLevel requested)
        {
            FidelityLevel highest = HighestImplemented(element);
            return new FidelityResolution(element, requested, requested <= highest ? requested : highest);
        }
    }
}
