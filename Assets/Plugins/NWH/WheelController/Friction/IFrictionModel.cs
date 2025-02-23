using UnityEngine;

namespace NWH.WheelController3D
{
    /// <summary>
    ///     Interface for friction calculation.
    ///     Note that x denotes longitudinal/forward and y lateral/sideways direction, contrary to Unity convention.
    /// </summary>
    public interface IFrictionModel
    {
        /// <summary>
        ///     Longitudinal friction calculation.
        /// </summary>
        /// <param name="Tm">Motor torque.</param>
        /// <param name="Tb">Brake torque.</param>
        /// <param name="Vx">Lng. speed.</param>
        /// <param name="Vy">Lat. speed.</param>
        /// <param name="W">Angular velocity.</param>
        /// <param name="L">Load coefficient (maximum force that can be put down for the current tire load).</param>
        /// <param name="Lmax">Maximum rated tire load.</param>
        /// <param name="dt">Physics step dt.</param>
        /// <param name="R">Tire radius.</param>
        /// <param name="I">Wheel inertia.</param>
        /// <param name="frictionCurve">Friction/slip curve.</param>
        /// <param name="BCDEz">Pacejka's simplified formula E value. Also peak value of friction/slip curve.</param>
        /// <param name="kFx">Longitudinal force coefficient.</param>
        /// <param name="kSx">Longitudinal slip coefficient.</param>
        /// <param name="Sx">Lng. slip.</param>
        /// <param name="Fx">Lng. force.</param>
        /// <param name="Tcnt">Torque that is 'returned' to the powertrain.</param>
        void Step(float Tm, float Tb, float Vx, float Vy, ref float W, float L, float Lmax, float dt, float R,
            float I, AnimationCurve frictionCurve, float BCDEz, float kFx, float kFy, float kSx, float kSy,
            ref float Sx, ref float Sy, ref float Fx, ref float Fy, ref float Tcnt, float Spk, float SCircleCoeff, float SCircleShape);
    }
}