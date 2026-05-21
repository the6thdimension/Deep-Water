namespace CarrierOps.Core.Movement
{
    /// <summary>
    /// Helm command — what the bridge tells the ship to do this step.
    ///
    /// Both fields are normalized:
    /// - <see cref="ThrottleNormalized"/> [0..1]: 0 = all stop, 1 = full speed. The kinematics
    ///   model interpolates current speed toward the commanded fraction of max speed.
    /// - <see cref="RudderNormalized"/> [-1..+1]: -1 = full left, 0 = amidships, +1 = full right.
    ///   The kinematics model interpolates current turn rate toward the commanded fraction of
    ///   max turn rate.
    ///
    /// Defaults (all zero) = "all stop, rudder amidships" — a sensible neutral command.
    /// </summary>
    public struct ShipCommand
    {
        public float ThrottleNormalized;   // [0..1]
        public float RudderNormalized;     // [-1..+1]
    }
}
