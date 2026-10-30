namespace DeepWater.Metoc
{
    public enum MetocOrderKind
    {
        /// <summary>Set the clock to a UT instant (a god jump, E29).</summary>
        JumpTo,
        /// <summary>Set the day speed (E29).</summary>
        SetDaySpeed,
        /// <summary>Move the place the sky is computed for.</summary>
        SetPlace,
    }

    /// <summary>
    /// A change to METOC's state from the scenario or the god role (standing constraint 8). Issued at any time,
    /// applied at the start of the next tick, and kept in the simulation's log with its issuer.
    /// </summary>
    public readonly struct MetocOrder
    {
        public readonly MetocOrderKind Kind;
        /// <summary>JumpTo: UT seconds. SetDaySpeed: the factor. SetPlace: latitude.</summary>
        public readonly double A;
        /// <summary>SetPlace: longitude.</summary>
        public readonly double B;
        public readonly string Issuer;

        MetocOrder(MetocOrderKind kind, double a, double b, string issuer)
        {
            Kind = kind;
            A = a;
            B = b;
            Issuer = issuer;
        }

        public static MetocOrder JumpTo(double utSeconds, string issuer) => new MetocOrder(MetocOrderKind.JumpTo, utSeconds, 0, issuer);
        public static MetocOrder DaySpeed(double daySpeed, string issuer) => new MetocOrder(MetocOrderKind.SetDaySpeed, daySpeed, 0, issuer);
        public static MetocOrder Place(double latitudeDeg, double longitudeDeg, string issuer) => new MetocOrder(MetocOrderKind.SetPlace, latitudeDeg, longitudeDeg, issuer);

        public override string ToString() => Kind switch
        {
            MetocOrderKind.JumpTo => $"jump to {MetocReport.FormatUt(A)} ({Issuer})",
            MetocOrderKind.SetDaySpeed => $"day speed {A:0.###}× ({Issuer})",
            _ => $"place {A:0.####}°, {B:0.####}° ({Issuer})",
        };
    }

    /// <summary>One line of the order log: when it was applied, and whether it was refused and why.</summary>
    public readonly struct MetocOrderRecord
    {
        public readonly long Tick;
        public readonly MetocOrder Order;
        /// <summary>Null when applied; otherwise the reason it was refused.</summary>
        public readonly string Refused;

        public MetocOrderRecord(long tick, in MetocOrder order, string refused)
        {
            Tick = tick;
            Order = order;
            Refused = refused;
        }

        public override string ToString() => $"tick {Tick}: {Order}{(Refused == null ? "" : " REFUSED: " + Refused)}";
    }
}
