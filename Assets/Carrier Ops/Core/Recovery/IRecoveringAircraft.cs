using UnityEngine;

namespace CarrierOps.Core.Recovery
{
    /// <summary>
    /// Contract for any aircraft that's actively trying to land on the carrier. Implemented
    /// by a thin MonoBehaviour companion (<c>TailhookHook</c>) that wraps the AerialArcade
    /// F-18 (or any other aircraft) without modifying it.
    ///
    /// The carrier polls all registered IRecoveringAircraft each FixedUpdate to:
    /// - feed FLOLS geometry (where is the aircraft, are they on glideslope)
    /// - check wire engagement (is the hook down, is the hook position crossing a wire,
    ///   is the speed in the catchable envelope)
    /// - apply deceleration during a trap (via <see cref="ApplyDeceleration"/>)
    ///
    /// **Why an int ID is also needed:** the WireState struct is unmanaged and can't hold a
    /// managed reference. The entity maintains an int→IRecoveringAircraft map so wires can
    /// remember "I'm holding aircraft #7" without leaking managed refs into structs.
    /// </summary>
    public interface IRecoveringAircraft
    {
        /// <summary>Stable identifier assigned by the carrier on registration. Positive.</summary>
        int RegistrationId { get; set; }

        /// <summary>World position of the aircraft (typically the airframe's center of mass).</summary>
        Vector3 Position { get; }

        /// <summary>World position of the tailhook tip. Used for wire engagement checks.</summary>
        Vector3 HookTipPosition { get; }

        /// <summary>True if the pilot has lowered the tailhook. Wires can only engage when this is true.</summary>
        bool HookDown { get; }

        /// <summary>World velocity. Used by FLOLS (for closure rate display) and engagement (for speed envelope check).</summary>
        Vector3 Velocity { get; }

        /// <summary>
        /// Apply a longitudinal deceleration during a trap. The wire calls this each
        /// FixedUpdate while the aircraft is engaged. Magnitude is in m/s²; sign is positive
        /// (the wire pulls against the aircraft's velocity). The companion is responsible
        /// for translating this into a velocity change on its rigidbody.
        /// </summary>
        /// <param name="decelMagnitudeMps2">Deceleration magnitude in m/s² along the velocity vector.</param>
        /// <param name="dt">Fixed step duration in seconds.</param>
        void ApplyDeceleration(float decelMagnitudeMps2, float dt);
    }
}
