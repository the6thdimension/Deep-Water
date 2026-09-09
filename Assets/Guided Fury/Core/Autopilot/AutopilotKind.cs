namespace GuidedFury.Core.Autopilot
{
    /// <summary>
    /// Identifies an autopilot implementation. Profile picks one of these; the missile
    /// instantiates the matching <see cref="IAutopilot"/> at launch.
    /// </summary>
    public enum AutopilotKind : byte
    {
        /// <summary>L3-style direct rate controller — applies torque proportional to (commanded_rate - current_rate). Suitable for L3 / simple L4. Stateless.</summary>
        SimpleRate          = 0,

        /// <summary>Two-loop autopilot: outer accel→rate, inner rate→control surface deflection. Required for proper L4 with control surfaces.</summary>
        SurfaceDeflection   = 1,
    }
}
