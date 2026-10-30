using System;

namespace DeepWater.Metoc
{
    /// <summary>
    /// Gregorian dates to Julian Dates and back (Meeus, <i>Astronomical Algorithms</i>, 2nd ed., ch. 7), and METOC's
    /// time scale: Universal Time in seconds from J2000.0 (E44). TT − UT (about 69 s) and leap seconds are ignored.
    /// Valid for Gregorian dates (from 1582-10-15).
    /// </summary>
    public static class Calendar
    {
        /// <summary>Julian Date of J2000.0, 2000-01-01 12:00.</summary>
        public const double J2000 = 2451545.0;
        public const double SecondsPerDay = 86400.0;
        /// <summary>Days in a Julian century, the unit of the almanac's time argument T.</summary>
        public const double DaysPerCentury = 36525.0;

        /// <summary>Julian Date of a Gregorian date; <paramref name="day"/> may carry a fraction (Meeus 7.1).</summary>
        public static double JulianDate(int year, int month, double day)
        {
            if (month <= 2) { year -= 1; month += 12; }
            int a = year / 100;
            int b = 2 - a + a / 4;
            return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
        }

        /// <summary>The Gregorian date of a Julian Date, the day carrying its fraction (Meeus ch. 7).</summary>
        public static void FromJulianDate(double jd, out int year, out int month, out double day)
        {
            double z = Math.Floor(jd + 0.5), f = jd + 0.5 - z;
            double alpha = Math.Floor((z - 1867216.25) / 36524.25);
            double a = z + 1 + alpha - Math.Floor(alpha / 4);
            double b = a + 1524;
            double c = Math.Floor((b - 122.1) / 365.25);
            double d = Math.Floor(365.25 * c);
            double e = Math.Floor((b - d) / 30.6001);
            day = b - d - Math.Floor(30.6001 * e) + f;
            month = (int)(e < 14 ? e - 1 : e - 13);
            year = (int)(month > 2 ? c - 4716 : c - 4715);
        }

        /// <summary>UT seconds from J2000.0 for a UTC calendar date and time of day.</summary>
        public static double UtSeconds(int year, int month, int day, int hour, int minute, double second)
        {
            // Whole days first, then the seconds of the day, so the fraction never passes through a Julian Date.
            double midnight = JulianDate(year, month, day);
            return (midnight - J2000) * SecondsPerDay + hour * 3600.0 + minute * 60.0 + second;
        }

        public static double JulianDateFromUt(double utSeconds) => J2000 + utSeconds / SecondsPerDay;

        /// <summary>Seconds since the UT midnight that began the day, in [0, 86 400).</summary>
        public static double SecondsOfDay(double utSeconds)
        {
            // J2000.0 is noon, so the day starts 43 200 s before each whole day count.
            double s = (utSeconds + 43200.0) % SecondsPerDay;
            return s < 0 ? s + SecondsPerDay : s;
        }

        /// <summary>The UTC calendar fields of a UT instant; seconds keep their fraction.</summary>
        public static void ToCalendar(double utSeconds, out int year, out int month, out int day, out int hour, out int minute, out double second)
        {
            double sod = SecondsOfDay(utSeconds);
            double midnightJd = JulianDateFromUt(utSeconds - sod);
            FromJulianDate(midnightJd, out year, out month, out double d);
            day = (int)Math.Round(d);
            hour = (int)(sod / 3600.0);
            minute = (int)((sod - hour * 3600.0) / 60.0);
            second = sod - hour * 3600.0 - minute * 60.0;
        }

        /// <summary>Julian centuries from J2000.0: the almanac's time argument T.</summary>
        public static double Centuries(double jd) => (jd - J2000) / DaysPerCentury;
    }
}
