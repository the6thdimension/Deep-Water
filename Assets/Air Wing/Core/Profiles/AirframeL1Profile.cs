using System;

namespace DeepWater.AirWing.Core.Profiles
{
    /// <summary>
    /// The L1 aerodynamic model (W32): polynomials in angle of attack (radians), coefficient i multiplying α^i, in the
    /// form of the published F/A-18 HARV fits (Chakraborty, Seiler and Balas, J. Guidance 2011, Appendix; NASA HARV
    /// flight data). Non-dimensional, so the same numbers serve any reference geometry. Names follow the paper:
    /// Cma is the basic pitching moment (not a slope), Cmds per radian of stabilator, Cmq per unit q·c̄/2V, and so on.
    /// </summary>
    public struct AeroPolynomialsL1
    {
        public float[] Cma, Cmds, Cmq;
        /// <summary>Basic lift, times cos(2β/3); and per radian of stabilator.</summary>
        public float[] CL, CLds;
        /// <summary>Basic drag, times cos β, plus <see cref="CD0"/>; and per radian of stabilator.</summary>
        public float[] CD, Cdds;
        public float CD0;
        public float[] Cyb, Cyda, Cydr;
        public float[] Clb, Clda, Cldr, Clp, Clr;
        public float[] Cnb, Cnda, Cndr, Cnp, Cnr;

        /// <summary>Throws, naming the first polynomial with the wrong number of terms.</summary>
        public void Validate()
        {
            Terms(Cma, 3, nameof(Cma)); Terms(Cmds, 3, nameof(Cmds)); Terms(Cmq, 4, nameof(Cmq));
            Terms(CL, 4, nameof(CL)); Terms(CLds, 4, nameof(CLds)); Terms(CD, 5, nameof(CD)); Terms(Cdds, 4, nameof(Cdds));
            Terms(Cyb, 3, nameof(Cyb)); Terms(Cyda, 4, nameof(Cyda)); Terms(Cydr, 4, nameof(Cydr));
            Terms(Clb, 5, nameof(Clb)); Terms(Clda, 4, nameof(Clda)); Terms(Cldr, 4, nameof(Cldr)); Terms(Clp, 2, nameof(Clp)); Terms(Clr, 3, nameof(Clr));
            Terms(Cnb, 3, nameof(Cnb)); Terms(Cnda, 4, nameof(Cnda)); Terms(Cndr, 5, nameof(Cndr)); Terms(Cnp, 2, nameof(Cnp)); Terms(Cnr, 2, nameof(Cnr));
        }

        static void Terms(float[] c, int n, string name)
        {
            if (c == null || c.Length != n) throw new ArgumentException($"{name}: {n} terms expected, got {(c == null ? "none" : c.Length.ToString())}");
            foreach (float v in c)
                if (float.IsNaN(v) || float.IsInfinity(v)) throw new ArgumentException($"{name}: every term must be finite");
        }
    }

    /// <summary>The L1 airframe's numbers, SI (W22): the aero model, its reference geometry and the rigid-body inertia.</summary>
    public struct AirframeL1Profile
    {
        public AeroPolynomialsL1 Aero;
        public float ReferenceAreaM2;
        public float SpanM;
        public float ChordM;
        public float IxxKgM2;
        public float IyyKgM2;
        public float IzzKgM2;
        /// <summary>The product of inertia as NASA TM-107601 and the HARV model sign it (−2,971 slug·ft² for the A/B).</summary>
        public float IxzKgM2;
        /// <summary>
        /// The polynomials are evaluated with α clamped to this range and held flat outside it. The fits hold from 0 to
        /// 60°; the range is AUTHORED (the profile says how far past the data it reaches).
        /// </summary>
        public float AlphaMinRad;
        public float AlphaMaxRad;

        public bool IsSet => Aero.Cma != null;

        public void Validate()
        {
            Aero.Validate();
            AirframeProfile.Positive(ReferenceAreaM2, nameof(ReferenceAreaM2));
            AirframeProfile.Positive(SpanM, nameof(SpanM));
            AirframeProfile.Positive(ChordM, nameof(ChordM));
            AirframeProfile.Positive(IxxKgM2, nameof(IxxKgM2));
            AirframeProfile.Positive(IyyKgM2, nameof(IyyKgM2));
            AirframeProfile.Positive(IzzKgM2, nameof(IzzKgM2));
            if (float.IsNaN(IxzKgM2) || float.IsInfinity(IxzKgM2)) throw new ArgumentOutOfRangeException(nameof(IxzKgM2), IxzKgM2, "must be finite");
            if (!(IxxKgM2 * IzzKgM2 > IxzKgM2 * IxzKgM2)) throw new ArgumentException("inertia: Ixx·Izz must exceed Ixz²");
            if (!(AlphaMinRad < AlphaMaxRad)) throw new ArgumentException($"alpha range {AlphaMinRad} … {AlphaMaxRad} rad is empty");
        }
    }
}
