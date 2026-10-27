using System;

namespace RHRadarSuite.Tracking
{
    /// <summary>
    /// The two gains of an α-β filter (ROADMAP Phase 3, Brookner: "α-β first — it's easy"). α weights the position
    /// residual, β the velocity residual (per update period). Every value a particular radar tracks with comes from
    /// its profile (P1); the factories here are the textbook ways to pick a pair.
    /// </summary>
    public readonly struct AlphaBetaGains
    {
        public readonly double Alpha;
        public readonly double Beta;

        public AlphaBetaGains(double alpha, double beta)
        {
            // Jury's stability triangle for z² − (2 − α − β)z + (1 − α): 0 < α < 2, 0 < β < 4 − 2α.
            if (!(alpha > 0.0 && alpha < 2.0)) throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "0 < α < 2");
            if (!(beta > 0.0 && beta < 4.0 - 2.0 * alpha)) throw new ArgumentOutOfRangeException(nameof(beta), beta, "0 < β < 4 − 2α (stable)");
            Alpha = alpha;
            Beta = beta;
        }

        /// <summary>Benedict–Bordner: β = α²/(2 − α), the pair that best trades noise against the transient.</summary>
        public static AlphaBetaGains BenedictBordner(double alpha) => new AlphaBetaGains(alpha, alpha * alpha / (2.0 - alpha));

        /// <summary>
        /// Critically damped (fading memory, Brookner): α = 1 − θ², β = (1 − θ)², both poles at θ (0 &lt; θ &lt; 1;
        /// a larger θ remembers longer and smooths more).
        /// </summary>
        public static AlphaBetaGains CriticallyDamped(double theta)
        {
            if (!(theta > 0.0 && theta < 1.0)) throw new ArgumentOutOfRangeException(nameof(theta), theta, "0 < θ < 1");
            return new AlphaBetaGains(1.0 - theta * theta, (1.0 - theta) * (1.0 - theta));
        }

        /// <summary>
        /// Kalata's steady-state Kalman gains for the tracking index λ = σ_a T² / σ_m (white acceleration noise σ_a,
        /// update period T, measurement noise σ_m): r = (4 + λ − √(8λ + λ²)) / 4, α = 1 − r², β = 2(2 − α) − 4√(1 − α).
        /// </summary>
        public static AlphaBetaGains Kalata(double trackingIndex)
        {
            if (!(trackingIndex > 0.0)) throw new ArgumentOutOfRangeException(nameof(trackingIndex), trackingIndex, "λ > 0");
            double l = trackingIndex;
            double r = (4.0 + l - Math.Sqrt(8.0 * l + l * l)) / 4.0;
            double alpha = 1.0 - r * r;
            return new AlphaBetaGains(alpha, 2.0 * (2.0 - alpha) - 4.0 * Math.Sqrt(1.0 - alpha));
        }

        /// <summary>
        /// The steady-state ratio of the filtered position's noise variance to the measurement's (white noise in,
        /// constant gains): (2α² + β(2 − 3α)) / (α(4 − 2α − β)).
        /// </summary>
        public double PositionVarianceRatio => (2.0 * Alpha * Alpha + Beta * (2.0 - 3.0 * Alpha)) / (Alpha * (4.0 - 2.0 * Alpha - Beta));
    }

    /// <summary>
    /// One axis of an α-β tracker, pure C# (P6), double precision: a position and a velocity that follow the
    /// measurements. Each update predicts on constant velocity, then corrects by the residual: x = x̂ + α r,
    /// v = v + (β / T) r. A missed look is a <see cref="Coast"/>: the prediction alone.
    /// </summary>
    public struct AlphaBetaFilter
    {
        public double Position;
        public double Velocity;
        /// <summary>The last update's residual (measurement minus prediction); 0 before the first update and after a coast.</summary>
        public double Residual;

        public AlphaBetaFilter(double position, double velocity)
        {
            Position = position;
            Velocity = velocity;
            Residual = 0.0;
        }

        /// <summary>Where the filter expects the target <paramref name="dt"/> seconds on.</summary>
        public double Predict(double dt) => Position + Velocity * dt;

        /// <summary>Advance <paramref name="dt"/> seconds and correct to <paramref name="measurement"/>.</summary>
        public void Update(in AlphaBetaGains gains, double measurement, double dt)
        {
            if (!(dt > 0.0)) throw new ArgumentOutOfRangeException(nameof(dt), dt, "the update period must be positive");
            double predicted = Predict(dt);
            Residual = measurement - predicted;
            Position = predicted + gains.Alpha * Residual;
            Velocity += gains.Beta / dt * Residual;
        }

        /// <summary>Advance <paramref name="dt"/> seconds with no measurement (a missed look).</summary>
        public void Coast(double dt)
        {
            if (!(dt > 0.0)) throw new ArgumentOutOfRangeException(nameof(dt), dt, "the coast period must be positive");
            Position = Predict(dt);
            Residual = 0.0;
        }
    }
}
