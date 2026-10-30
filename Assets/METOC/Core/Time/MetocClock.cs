namespace DeepWater.Metoc
{
    /// <summary>
    /// Reckoned time (E29, E44): Universal Time in seconds from J2000.0, advanced by the simulation's fixed step
    /// times the day speed. Day speed is a labelled factor: 1 is real time, 60 shows an hour of sky per minute.
    /// <para>
    /// The clock counts steps from an anchor instead of adding each step to the time. UT is near 8.5 × 10⁸ s, where
    /// a double resolves only about 0.1 µs, so adding 1.2 s steps lost 24 µs in 500 steps and kept losing. Counting
    /// rounds once, however long the run: the anchor moves only on a jump, a new day speed or a new step length.
    /// </para>
    /// </summary>
    public struct MetocClock
    {
        double _anchorUt;
        long _steps;
        double _stepSeconds;

        public double DaySpeed { get; private set; }

        public MetocClock(double utSeconds, double daySpeed)
        {
            _anchorUt = utSeconds;
            _steps = 0;
            _stepSeconds = 0;
            DaySpeed = daySpeed;
        }

        public double UtSeconds => _anchorUt + _steps * _stepSeconds * DaySpeed;

        public double JulianDate => Calendar.JulianDateFromUt(UtSeconds);

        public void Advance(double simulationSeconds)
        {
            if (simulationSeconds != _stepSeconds)
            {
                Reanchor();
                _stepSeconds = simulationSeconds;
            }
            _steps++;
        }

        public void JumpTo(double utSeconds)
        {
            _anchorUt = utSeconds;
            _steps = 0;
        }

        public void SetDaySpeed(double daySpeed)
        {
            Reanchor();
            DaySpeed = daySpeed;
        }

        void Reanchor()
        {
            _anchorUt = UtSeconds;
            _steps = 0;
        }
    }
}
