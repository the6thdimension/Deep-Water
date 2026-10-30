using System;

namespace DeepWater.Metoc
{
    /// <summary>
    /// The weather METOC decides (slice 2, E30, E51, E52): one main cloud layer (cover, base, thickness), high cloud,
    /// and the visibility at the surface with the top of the layer that limits it. Heights are metres above sea level.
    /// A blend can hold a fraction of an okta; the METAR code rounds it.
    /// </summary>
    public readonly struct MetocWeather
    {
        /// <summary>The main layer's cover, oktas (0 clear to 8 overcast).</summary>
        public readonly double CoverOktas;
        /// <summary>The main layer's base and thickness, metres.</summary>
        public readonly double CloudBaseM, CloudThicknessM;
        /// <summary>High cloud (cirrus), oktas. It sets neither the ceiling nor the dimming (E52).</summary>
        public readonly double HighCloudOktas;
        /// <summary>Meteorological optical range at the surface, metres (E51).</summary>
        public readonly double VisibilityM;
        /// <summary>The top of the fog bank or haze that limits the visibility, metres (E51).</summary>
        public readonly double ObscurationTopM;

        public MetocWeather(double coverOktas, double cloudBaseM, double cloudThicknessM, double highCloudOktas, double visibilityM,
            double obscurationTopM)
        {
            CoverOktas = coverOktas;
            CloudBaseM = cloudBaseM;
            CloudThicknessM = cloudThicknessM;
            HighCloudOktas = highCloudOktas;
            VisibilityM = visibilityM;
            ObscurationTopM = obscurationTopM;
        }

        /// <summary>Null when valid, else why not. A boundary check: METOC refuses a bad weather, it never clamps one.</summary>
        public string Problem()
        {
            if (!InRange(CoverOktas, 0, 8)) return $"cover {CoverOktas} oktas is outside 0–8";
            if (!InRange(HighCloudOktas, 0, 8)) return $"high cloud {HighCloudOktas} oktas is outside 0–8";
            if (!InRange(CloudBaseM, 0, 20000)) return $"cloud base {CloudBaseM} m is outside 0–20 000 m";
            if (!InRange(CloudThicknessM, 1, 20000)) return $"cloud thickness {CloudThicknessM} m is outside 1–20 000 m";
            if (!InRange(VisibilityM, 1, 1e6)) return $"visibility {VisibilityM} m is outside 1 m–1 000 km";
            if (!InRange(ObscurationTopM, 1, 20000)) return $"obscuration top {ObscurationTopM} m is outside 1–20 000 m";
            return null;
        }

        static bool InRange(double v, double lo, double hi) => v >= lo && v <= hi;   // false for NaN

        /// <summary>The main layer's cover rounded to whole oktas, as a METAR report gives it.</summary>
        public int CoverOktasRounded => (int)Math.Round(CoverOktas, MidpointRounding.AwayFromZero);

        /// <summary>A ceiling exists when the main layer is broken or overcast (five oktas or more).</summary>
        public bool HasCeiling => CoverOktasRounded >= 5;

        /// <summary>The ceiling, metres: the main layer's base when broken or overcast, else infinity (unlimited).</summary>
        public double CeilingM => HasCeiling ? CloudBaseM : double.PositiveInfinity;

        /// <summary>METAR cover: SKC (0), FEW (1–2), SCT (3–4), BKN (5–7), OVC (8).</summary>
        public string CoverCode => CoverOktasRounded switch
        {
            0 => "SKC",
            <= 2 => "FEW",
            <= 4 => "SCT",
            <= 7 => "BKN",
            _ => "OVC",
        };

        /// <summary>WMO: fog below 1 km, mist or haze to 5 km, nothing to report above (E51).</summary>
        public string ObscurationLabel => VisibilityM < 1000 ? "fog" : VisibilityM <= 5000 ? "mist or haze" : "";

        /// <summary>
        /// The weather a fraction <paramref name="t"/> of the way from <paramref name="a"/> to <paramref name="b"/> (E33):
        /// cover and heights linearly, visibility and the obscuration top in log space, since they span decades.
        /// </summary>
        public static MetocWeather Blend(in MetocWeather a, in MetocWeather b, double t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;
            return new MetocWeather(
                Lerp(a.CoverOktas, b.CoverOktas, t),
                Lerp(a.CloudBaseM, b.CloudBaseM, t),
                Lerp(a.CloudThicknessM, b.CloudThicknessM, t),
                Lerp(a.HighCloudOktas, b.HighCloudOktas, t),
                LogLerp(a.VisibilityM, b.VisibilityM, t),
                LogLerp(a.ObscurationTopM, b.ObscurationTopM, t));
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static double LogLerp(double a, double b, double t) => Math.Exp(Lerp(Math.Log(a), Math.Log(b), t));

        public override string ToString() =>
            $"{CoverCode} {CoverOktas:0.#}/8 at {CloudBaseM:0} m ({CloudThicknessM:0} m thick), high {HighCloudOktas:0.#}/8, " +
            $"visibility {VisibilityM:0} m to {ObscurationTopM:0} m";
    }
}
