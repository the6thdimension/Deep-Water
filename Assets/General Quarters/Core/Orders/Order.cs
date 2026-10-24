namespace DeepWater.GeneralQuarters.Core.Orders
{
    /// <summary>Who gave an Order. The ship never knows whether a seat is human (BLUEPRINT §3.3).</summary>
    public enum OrderIssuer : byte
    {
        Scenario = 0,
        Captain = 1,
        Tao = 2,
        AiCaptain = 3,
    }

    public enum OrderKind : byte
    {
        Helm = 0,
        Engage = 1,
        CeaseFire = 2,
        Doctrine = 3,
        Roe = 4,
    }

    /// <summary>
    /// The only way anything changes the ship. A flat value type so the log stays allocation-free and
    /// every field can go into the Fix. <see cref="OrderLog.Issue"/> stamps <see cref="Tick"/> and
    /// <see cref="Sequence"/>; build Orders with the factory methods.
    /// </summary>
    public struct Order
    {
        public OrderKind Kind;
        public OrderIssuer Issuer;
        /// <summary>The tick on which the Order takes effect (stamped by the log).</summary>
        public long Tick;
        /// <summary>Position in the order log (stamped by the log); the root id Inquiry links to.</summary>
        public int Sequence;

        // Helm
        public float CourseDeg;
        public float SpeedKn;

        // Engage / CeaseFire
        /// <summary>The track to engage or cease firing on; -1 means all tracks (cease fire only).</summary>
        public int TrackId;
        public WeaponClass Weapon;
        /// <summary>Missiles or bursts to fire; 0 means the doctrine default.</summary>
        public int Rounds;

        // Doctrine / ROE
        public DoctrineMode Mode;
        public Roe Roe;

        public static Order Helm(OrderIssuer issuer, float courseDeg, float speedKn) =>
            new Order { Kind = OrderKind.Helm, Issuer = issuer, CourseDeg = courseDeg, SpeedKn = speedKn, TrackId = -1 };

        public static Order Engage(OrderIssuer issuer, int trackId, WeaponClass weapon, int rounds = 0) =>
            new Order { Kind = OrderKind.Engage, Issuer = issuer, TrackId = trackId, Weapon = weapon, Rounds = rounds };

        public static Order CeaseFire(OrderIssuer issuer, int trackId = -1) =>
            new Order { Kind = OrderKind.CeaseFire, Issuer = issuer, TrackId = trackId };

        public static Order SetDoctrine(OrderIssuer issuer, WeaponClass weapon, DoctrineMode mode) =>
            new Order { Kind = OrderKind.Doctrine, Issuer = issuer, Weapon = weapon, Mode = mode, TrackId = -1 };

        public static Order SetRoe(OrderIssuer issuer, Roe roe) =>
            new Order { Kind = OrderKind.Roe, Issuer = issuer, Roe = roe, TrackId = -1 };

        public override string ToString() => Kind switch
        {
            OrderKind.Helm => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: helm course {CourseDeg:0}°, {SpeedKn:0} kn"),
            OrderKind.Engage => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: engage track {TrackId} with {Weapon}"),
            OrderKind.CeaseFire => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: cease fire {(TrackId < 0 ? "all" : "track " + TrackId)}"),
            OrderKind.Doctrine => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: {Weapon} doctrine {Mode}"),
            _ => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: ROE {Roe}"),
        };
    }
}
