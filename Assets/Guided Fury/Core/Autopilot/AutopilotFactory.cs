namespace GuidedFury.Core.Autopilot
{
    /// <summary>
    /// AutopilotKind → instance. Same pattern as <see cref="GuidedFury.Core.Guidance.GuidanceFactory"/>.
    /// All autopilots are currently stateless singletons.
    /// </summary>
    public static class AutopilotFactory
    {
        public static IAutopilot Create(AutopilotKind kind)
        {
            switch (kind)
            {
                case AutopilotKind.SimpleRate:        return SimpleRateAutopilot.Instance;
                case AutopilotKind.SurfaceDeflection: return SurfaceDeflectionAutopilot.Instance;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown AutopilotKind");
            }
        }
    }
}
