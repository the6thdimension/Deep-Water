using System;
using UnityEngine;

namespace DeepWater.AirWing.ScriptableObjects
{
    /// <summary>Which control drives a moving part of the model.</summary>
    public enum SurfaceDriver
    {
        /// <summary>Stick roll (ailerons).</summary>
        Roll,
        /// <summary>Stick pitch, pull positive (stabilators).</summary>
        Pitch,
        /// <summary>Pedals (rudders).</summary>
        Pedals,
        /// <summary>The flaps switch, 0 or 1.</summary>
        Flaps,
        /// <summary>The speed-brake switch, 0 or 1.</summary>
        SpeedBrake,
    }

    /// <summary>
    /// How a model's moving parts follow the Pilot Inputs: presentation only, never read by the flight core.
    /// Each surface turns its transform from its rest pose by <c>axis × driver × maxAngleDeg</c> in Euler degrees,
    /// eased at <c>smoothing</c> per second. Values for the F/A-18E were copied from the FDS F-18E prefab's
    /// AerialArcade surface settings (names, axes, angles) on 2026-10-25; the rudder travel is AUTHORED at 30°.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAircraftVisuals", menuName = "Deep Water/Air Wing/Aircraft Visuals", order = 40)]
    public class AircraftVisualsSO : ScriptableObject
    {
        [Serializable]
        public struct Surface
        {
            [Tooltip("Name of the transform in the model (searched under the aircraft).")]
            public string transformName;
            public SurfaceDriver driver;
            [Tooltip("Euler axis, in the transform's local rest pose.")]
            public Vector3 axis;
            public float maxAngleDeg;
            [Tooltip("Easing toward the commanded angle, per second.")]
            public float smoothing;
        }

        public Surface[] surfaces = Array.Empty<Surface>();

        /// <summary>One part of the landing gear: a hinge, how far it turns to stow, and when in the retraction.</summary>
        [Serializable]
        public struct GearPart
        {
            [Tooltip("Name of the transform in the model (searched under the aircraft).")]
            public string transformName;
            [Tooltip("Hinge axis in the model's frame (x right, y up, z forward), at rest.")]
            public Vector3 axis;
            [Tooltip("Angle about the axis with the gear up, degrees (0 with it down).")]
            public float stowedAngleDeg;
            [Tooltip("The part moves while the retraction (0 down … 1 up) runs from start to end.")]
            [Range(0f, 1f)] public float startFraction;
            [Range(0f, 1f)] public float endFraction;
            [Tooltip("Hidden once the gear is fully up (it sits inside a closed bay).")]
            public bool hideWhenStowed;
        }

        [Tooltip("The landing gear's moving parts, driven by the core's GearDeployed (Landing Gear L1).")]
        public GearPart[] gearParts = Array.Empty<GearPart>();
        [Tooltip("Where the model's origin sits relative to the centre of mass the core flies, metres (body frame).")]
        public Vector3 modelOffsetM;

        [Header("Explosions (presentation: ExplosionView)")]
        [Tooltip("Played where the aircraft is shot down or collides, and where it meets the sea or the ground. Empty: none.")]
        public GameObject explosion;
        [Tooltip("The explosion's scale in the air: a kill or a collision.")]
        [Min(0.01f)] public float airExplosionScale = 1f;
        [Tooltip("Its scale where the aircraft meets the sea or the ground.")]
        [Min(0.01f)] public float impactExplosionScale = 1f;
        [Tooltip("Seconds before a played explosion is removed.")]
        [Min(0.1f)] public float explosionLifeS = 6f;
        [Tooltip("The explosion's scale where a missile bursts without a kill (a kill plays the air explosion at the victim).")]
        [Min(0.01f)] public float missileBurstScale = 0.8f;

        [Header("Game missiles (presentation: MissileView, W65)")]
        [Tooltip("The missile's shape: on each loaded pylon, and in flight. Its +Z is the nose. Empty: none drawn.")]
        public GameObject missileModel;
        [Tooltip("The smoke trail a missile in flight draws (its exhaust and a ribbon). Empty: none.")]
        public GameObject missileTrail;
        [Tooltip("Seconds a trail lingers after its missile is gone.")]
        [Min(0f)] public float missileTrailLingerS = 3f;

        [TextArea(2, 8)] public string provenance = "";
    }
}
