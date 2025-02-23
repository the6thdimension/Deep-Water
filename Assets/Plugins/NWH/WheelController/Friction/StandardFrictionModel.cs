using System;
using UnityEngine;

namespace NWH.WheelController3D
{
    [Serializable]
    public class StandardFrictionModel : IFrictionModel
    {
        private Vector2 _combinedSlip;
        private Vector2 _slipDir;

        public void Step(float Tm, float Tb, float Vx, float Vy, ref float W, float L, float Lmax, float dt, float R,
            float I, AnimationCurve frictionCurve, float BCDEz, float kFx, float kFy, float kSx, float kSy, 
            ref float Sx, ref float Sy, ref float Fx, ref float Fy, ref float Tcnt, float Spk, float SCircleCoeff, float SCircleShape)
        {
            if (dt < 0.00001f) dt = 0.00001f;
            if (I < 0.0001f) I = 0.0001f;

            float LcX = Lmax * 2.6f * (1f - Mathf.Exp(-0.00012f * L));
            float LcY = Lmax * 3.8f * (1f - Mathf.Exp(-0.0001f * L));

            // *******************************
            // ******** LONGITUDINAL ********* 
            // *******************************
            float Winit = W;
            float VxAbs = Vx < 0 ? -Vx : Vx;
            float WAbs  = W < 0 ? -W : W;

            if (VxAbs >= 0.1f)
                Sx = (Vx - W * R) / VxAbs;
            else
                Sx = (Vx - W * R) * 2.2f;

            Sx *= kSx;
            Sx =  Sx < -1f ? -1f : Sx > 1f ? 1f : Sx;

            W += Tm / I * dt;

            Tb = Tb * (W > 0 ? -1f : 1f);
            float TbCap = (W < 0 ? -W : W) * I / dt;
            float Tbr   = (Tb < 0 ? -Tb : Tb) - (TbCap < 0 ? -TbCap : TbCap);
            Tbr = Tbr < 0 ? 0 : Tbr;
            Tb = Tb > TbCap  ? TbCap :
                 Tb < -TbCap ? -TbCap : Tb;
            W += Tb / I * dt;

            float maxTorque   = frictionCurve.Evaluate(Sx < 0f ? -Sx : Sx) * LcX * R;
            float errorTorque = (W - Vx / R) * I / dt;
            float surfaceTorque = errorTorque < -maxTorque ? -maxTorque :
                                  errorTorque > maxTorque  ? maxTorque : errorTorque;

            W  -= surfaceTorque / I * dt;
            Fx =  surfaceTorque / R;

            Tbr = Tbr * (W > 0 ? -1f : 1f);
            float TbCap2 = (W < 0 ? -W : W) * I / dt;
            float Tb2 = Tbr > TbCap2  ? TbCap2 :
                        Tbr < -TbCap2 ? -TbCap2 : Tbr;
            W += Tb2 / I * dt;

            float deltaOmegaTorque = (W - Winit) * I / dt;
            Tcnt = -surfaceTorque + Tb + Tb2 - deltaOmegaTorque;

            // Force 0 slip when in air.
            if (LcX < 0.001f) Sx = 0;



            // *******************************
            // ******** LATERAL ********* 
            // *******************************
            if (VxAbs > 0.3f)
            {
                Sy = Mathf.Atan(Vy / VxAbs) * Mathf.Rad2Deg;
                Sy /= 120f;
            }
            else
            {
                Sy = Vy * (0.001f / dt);
            }

            Sy *= kSy;
            Sy = Sy < -1f ? -1f : Sy > 1f ? 1f : Sy;
            float slipSign = Sy < 0 ? -1f : 1f;
            Fy = -slipSign * frictionCurve.Evaluate(Sy < 0 ? -Sy : Sy) * LcY;

            // Force 0 slip when in air.
            if (LcY < 0.001f) Sy = 0;



            // *******************************
            // ********* SLIP CIRCLE ********* 
            // *******************************

            if (SCircleCoeff > 0)
            {
                float Vsrq = Vx * Vx + Vy * Vy;
                if (Vsrq > 0.2f)
                {
                    float s = Sx / Spk;
                    float a = Sy / Spk;
                    float rho = Mathf.Sqrt(s * s + a * a);

                    if (rho > 1)
                    {
                        float beta = Mathf.Atan2(Sy, Sx * SCircleShape);
                        float sinBeta = Mathf.Sin(beta);
                        float cosBeta = Mathf.Cos(beta);
                        float f = LcX * cosBeta * cosBeta + LcY * sinBeta * sinBeta;

                        float invSlipCircleCoeff = 1f - SCircleCoeff;
                        Fx = invSlipCircleCoeff * Fx + SCircleCoeff * (-f * cosBeta);
                        Fy = invSlipCircleCoeff * Fy + SCircleCoeff * (-f * sinBeta);
                    }
                }
            }


            Fx *= kFx;
            Fy *= kFy;
        }
    }
}