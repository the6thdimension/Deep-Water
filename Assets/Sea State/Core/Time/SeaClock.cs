namespace DeepWater.Sea.Core
{
    /// <summary>
    /// Fixed-step simulation clock for the sea. Time is <c>base + steps × Δt</c> in double
    /// precision — never <c>Time.time</c>, never an accumulated float — so the sea seen by
    /// step n is the same no matter how many render frames happened in between (P2).
    ///
    /// If Δt changes mid-run (someone edits <c>Time.fixedDeltaTime</c>), the clock re-bases:
    /// the current time becomes the new base and steps restart from zero, so time stays
    /// continuous and each segment is still an exact multiple of its own Δt.
    /// </summary>
    public struct SeaClock
    {
        double _baseTimeS;
        long _stepsSinceBase;
        double _stepS;

        /// <summary>Total fixed steps taken since the clock started.</summary>
        public long TotalSteps { get; private set; }

        /// <summary>Current simulation time in seconds.</summary>
        public double TimeS => _baseTimeS + _stepsSinceBase * _stepS;

        /// <summary>Fixed step of the current segment, seconds (0 before the first step).</summary>
        public double StepS => _stepS;

        public static SeaClock StartingAt(double timeS) => new SeaClock { _baseTimeS = timeS };

        /// <summary>Advance one fixed step of <paramref name="stepS"/> seconds.</summary>
        public void Step(double stepS)
        {
            if (stepS != _stepS)
            {
                _baseTimeS = TimeS;
                _stepsSinceBase = 0;
                _stepS = stepS;
            }
            _stepsSinceBase++;
            TotalSteps++;
        }
    }
}
