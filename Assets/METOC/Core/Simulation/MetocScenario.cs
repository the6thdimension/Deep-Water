using System;

namespace DeepWater.Metoc
{
    /// <summary>The place the sky is computed for: latitude north positive, longitude east positive (E28).</summary>
    public readonly struct MetocPlace
    {
        public readonly double LatitudeDeg, LongitudeDeg;

        public MetocPlace(double latitudeDeg, double longitudeDeg)
        {
            LatitudeDeg = latitudeDeg;
            LongitudeDeg = longitudeDeg;
        }

        /// <summary>Null when valid, else why not: latitude within ±90°, longitude finite.</summary>
        public string Problem()
        {
            if (double.IsNaN(LatitudeDeg) || LatitudeDeg < -90 || LatitudeDeg > 90) return $"latitude {LatitudeDeg} is outside ±90°";
            if (double.IsNaN(LongitudeDeg) || double.IsInfinity(LongitudeDeg)) return $"longitude {LongitudeDeg} is not finite";
            return null;
        }

        /// <summary>The same place with its longitude wrapped into [−180°, 180°).</summary>
        public MetocPlace Wrapped() => new MetocPlace(LatitudeDeg, Almanac.Normalize360(LongitudeDeg + 180.0) - 180.0);
    }

    /// <summary>A METOC scenario's starting state (E29): when, where, how fast the day runs, and the weather.</summary>
    public readonly struct MetocScenario
    {
        public readonly double StartUtSeconds;
        public readonly MetocPlace Place;
        public readonly double DaySpeed;
        public readonly MetocWeather StartWeather;

        public MetocScenario(double startUtSeconds, in MetocPlace place, double daySpeed, in MetocWeather startWeather)
        {
            StartUtSeconds = startUtSeconds;
            Place = place;
            DaySpeed = daySpeed;
            StartWeather = startWeather;
        }

        public static string DaySpeedProblem(double daySpeed) =>
            double.IsNaN(daySpeed) || double.IsInfinity(daySpeed) || daySpeed < 0 ? $"day speed {daySpeed} must be finite and ≥ 0" : null;

        /// <summary>Throws on a scenario the almanac cannot run (a boundary check, not a silent clamp).</summary>
        public void Validate()
        {
            if (double.IsNaN(StartUtSeconds) || double.IsInfinity(StartUtSeconds)) throw new ArgumentException($"start time {StartUtSeconds} is not finite");
            string p = Place.Problem() ?? DaySpeedProblem(DaySpeed) ?? StartWeather.Problem();
            if (p != null) throw new ArgumentException(p);
        }
    }
}
