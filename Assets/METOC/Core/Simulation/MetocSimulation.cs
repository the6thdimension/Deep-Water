using System;
using System.Collections.Generic;

namespace DeepWater.Metoc
{
    /// <summary>
    /// METOC's reckoned core (P6, E29). Each fixed step applies the orders issued since the last one, in the order
    /// they were issued, advances the clock by the step times the day speed, and computes the report. Pure: the
    /// same scenario, steps and orders give the same reports, bit for bit.
    /// </summary>
    public sealed class MetocSimulation
    {
        readonly IlluminationProfile _light;
        readonly List<MetocOrder> _pending = new List<MetocOrder>();
        readonly List<MetocOrderRecord> _log = new List<MetocOrderRecord>();
        MetocClock _clock;
        MetocPlace _place;

        public long Tick { get; private set; }
        public MetocReport Report { get; private set; }
        public IReadOnlyList<MetocOrderRecord> Log => _log;
        public double DaySpeed => _clock.DaySpeed;

        public MetocSimulation(in MetocScenario scenario, IlluminationProfile light)
        {
            scenario.Validate();
            _light = light ?? throw new ArgumentNullException(nameof(light));
            _clock = new MetocClock(scenario.StartUtSeconds, scenario.DaySpeed);
            _place = scenario.Place.Wrapped();
            Report = Compute();
        }

        /// <summary>Queue an order for the start of the next step.</summary>
        public void Issue(in MetocOrder order) => _pending.Add(order);

        public void Step(double stepSeconds)
        {
            if (!(stepSeconds > 0) || double.IsInfinity(stepSeconds)) throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds, "a step must be finite and positive");
            for (int i = 0; i < _pending.Count; i++) Apply(_pending[i]);
            _pending.Clear();
            _clock.Advance(stepSeconds);
            Tick++;
            Report = Compute();
        }

        void Apply(in MetocOrder o)
        {
            string refused = null;
            switch (o.Kind)
            {
                case MetocOrderKind.JumpTo:
                    if (double.IsNaN(o.A) || double.IsInfinity(o.A)) refused = $"time {o.A} is not finite";
                    else _clock.JumpTo(o.A);
                    break;
                case MetocOrderKind.SetDaySpeed:
                    refused = MetocScenario.DaySpeedProblem(o.A);
                    if (refused == null) _clock.SetDaySpeed(o.A);
                    break;
                case MetocOrderKind.SetPlace:
                    var place = new MetocPlace(o.A, o.B);
                    refused = place.Problem();
                    if (refused == null) _place = place.Wrapped();
                    break;
                default:
                    refused = $"unknown order kind {o.Kind}";
                    break;
            }
            _log.Add(new MetocOrderRecord(Tick, o, refused));
        }

        MetocReport Compute()
        {
            var sun = Almanac.Sun(_clock.UtSeconds, _place.LatitudeDeg, _place.LongitudeDeg);
            var moon = Almanac.Moon(_clock.UtSeconds, _place.LatitudeDeg, _place.LongitudeDeg, sun);
            var light = Illumination.Compute(sun, moon, _light);
            return new MetocReport(Tick, _clock.UtSeconds, _clock.DaySpeed, _place, sun, moon, light);
        }
    }
}
