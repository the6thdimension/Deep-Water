using System;

namespace DeepWater.Metoc
{
    /// <summary>
    /// The weather at a place and time (Theatre E19): the time, the sun, the moon, the reckoned light (E27, E41), the
    /// cloud and visibility with their ceiling and labels (E30, E51), and the rain with the visibility through it (E53,
    /// E54). Wind joins in a later slice. Readers take it as it is; nothing drawn feeds back into it.
    /// </summary>
    public readonly struct MetocReport
    {
        public readonly long Tick;
        public readonly double UtSeconds, DaySpeed;
        public readonly MetocPlace Place;
        public readonly SunState Sun;
        public readonly MoonState Moon;
        public readonly IlluminationState Light;
        public readonly MetocWeather Weather;
        /// <summary>The rain's extinction of visible light, per metre (E54); 0 when dry.</summary>
        public readonly double RainExtinctionPerM;
        /// <summary>
        /// The visibility through the air and the rain together, metres (E54): what an eye at the surface sees. The
        /// weather's <see cref="MetocWeather.VisibilityM"/> is the air's alone, as set.
        /// </summary>
        public readonly double EffectiveVisibilityM;

        public MetocReport(long tick, double utSeconds, double daySpeed, in MetocPlace place, in SunState sun, in MoonState moon,
            in IlluminationState light, in MetocWeather weather, double rainExtinctionPerM, double effectiveVisibilityM)
        {
            Tick = tick;
            UtSeconds = utSeconds;
            DaySpeed = daySpeed;
            Place = place;
            Sun = sun;
            Moon = moon;
            Light = light;
            Weather = weather;
            RainExtinctionPerM = rainExtinctionPerM;
            EffectiveVisibilityM = effectiveVisibilityM;
        }

        public string TimeText => FormatUt(UtSeconds);

        public static string FormatUt(double utSeconds)
        {
            Calendar.ToCalendar(utSeconds, out int y, out int mo, out int d, out int h, out int mi, out double s);
            return $"{y:0000}-{mo:00}-{d:00} {h:00}:{mi:00}:{Math.Floor(s):00} UT";
        }

        /// <summary>
        /// FNV-1a over every number in the report, bit for bit. Two runs agree exactly when their fingerprints do;
        /// a later Fix (Reckoning R34) takes it as METOC's per-tick fingerprint.
        /// </summary>
        public ulong Fingerprint()
        {
            ulong h = 14695981039346656037UL;
            void Mix(double v)
            {
                ulong bits = (ulong)BitConverter.DoubleToInt64Bits(v);
                for (int i = 0; i < 8; i++)
                {
                    h ^= (bits >> (8 * i)) & 0xFF;
                    h *= 1099511628211UL;
                }
            }
            Mix(Tick);
            Mix(UtSeconds); Mix(DaySpeed); Mix(Place.LatitudeDeg); Mix(Place.LongitudeDeg);
            Mix(Sun.Sky.ElevationDeg); Mix(Sun.Sky.AzimuthDeg); Mix(Sun.DeclinationDeg); Mix(Sun.DistanceAu);
            Mix(Moon.Sky.ElevationDeg); Mix(Moon.Sky.AzimuthDeg); Mix(Moon.PhaseAngleDeg); Mix(Moon.DistanceKm);
            Mix(Light.SunLux); Mix(Light.MoonLux); Mix(Light.NightSkyLux); Mix(Light.CloudTransmission);
            Mix(Weather.CoverOktas); Mix(Weather.CloudBaseM); Mix(Weather.CloudThicknessM); Mix(Weather.HighCloudOktas);
            Mix(Weather.VisibilityM); Mix(Weather.ObscurationTopM); Mix(Weather.RainMmPerHour);
            Mix(RainExtinctionPerM); Mix(EffectiveVisibilityM);
            return h;
        }
    }
}
