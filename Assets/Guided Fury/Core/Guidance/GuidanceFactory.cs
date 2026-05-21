namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// Maps <see cref="GuidanceLawKind"/> values to their stateless singleton implementations.
    ///
    /// **Why a static factory and not a `new`:** guidance laws are stateless and stored as
    /// shared singletons (one instance per process). Spawning a new instance per missile
    /// would be wasteful and would clutter profiling — this centralizes the lookup.
    ///
    /// **Adding a new law:** implement <see cref="IGuidanceLaw"/>, add a value to
    /// <see cref="GuidanceLawKind"/>, then add a case here. Three explicit edits — the
    /// compiler will flag the missing case if you forget.
    /// </summary>
    public static class GuidanceFactory
    {
        public static IGuidanceLaw Create(GuidanceLawKind kind)
        {
            switch (kind)
            {
                case GuidanceLawKind.None:                   return NullGuidanceLaw.Instance;
                case GuidanceLawKind.Pursuit:                return PursuitGuidance.Instance;
                case GuidanceLawKind.ProportionalNavigation: return ProportionalNavigation.Instance;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(kind), kind, "Unknown GuidanceLawKind");
            }
        }
    }
}
