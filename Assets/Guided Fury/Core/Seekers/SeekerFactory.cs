namespace GuidedFury.Core.Seekers
{
    /// <summary>
    /// Maps <see cref="SeekerKind"/> values to seeker instances. Each missile gets its
    /// own seeker (seekers are stateful — lock state, dwell timer, etc.), so this is
    /// `new` not singleton.
    ///
    /// **Adding a new seeker:** implement <see cref="ISeeker"/>, add a value to
    /// <see cref="SeekerKind"/>, then add a case here. The compiler will flag the
    /// missing case if you forget.
    /// </summary>
    public static class SeekerFactory
    {
        /// <summary>
        /// Build a seeker matching the kind. Returns null for <see cref="SeekerKind.None"/>
        /// — that signals "no seeker, use raw truth target source."
        /// </summary>
        public static ISeeker Create(SeekerKind kind, SeekerProfile profile)
        {
            switch (kind)
            {
                case SeekerKind.None:       return null;
                case SeekerKind.ConeSeeker: return new SimpleConeSeeker(profile);
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(kind), kind, "Unknown SeekerKind");
            }
        }
    }
}
