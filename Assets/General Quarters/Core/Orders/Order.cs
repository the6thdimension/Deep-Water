using UnityEngine;

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
        /// <summary>An engine order telegraph bell: overrides speed hold until the next Helm order.</summary>
        Bell = 5,
        /// <summary>A rudder angle by hand: overrides course hold until the next Helm or Route order.</summary>
        Rudder = 6,
        /// <summary>Follow a route the Helm holds, at the order's speed.</summary>
        Route = 7,
        /// <summary>Steer a course and leave the engines as they are ("come to 090").</summary>
        Course = 8,
        /// <summary>Open a hole in the hull: a scripted breach (Damage Control L1, G57). Issued by the scenario or a test.</summary>
        Breach = 9,
    }

    /// <summary>
    /// Engine order telegraph bells, USN style. Ahead bells are fractions of standard speed (⅓, ⅔, standard,
    /// full = 4/3 standard, flank = maximum); the Helm profile says what each one means in knots.
    /// </summary>
    public enum EngineBell : sbyte
    {
        BackEmergency = -4,
        BackFull = -3,
        BackTwoThirds = -2,
        BackOneThird = -1,
        Stop = 0,
        AheadOneThird = 1,
        AheadTwoThirds = 2,
        AheadStandard = 3,
        AheadFull = 4,
        AheadFlank = 5,
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

        // Helm / Bell / Rudder / Route
        public float CourseDeg;
        public float SpeedKn;
        public EngineBell Bell;
        /// <summary>Rudder angle, degrees; positive turns the bow to starboard.</summary>
        public float RudderDeg;
        public int RouteId;

        // Engage / CeaseFire
        /// <summary>The track to engage or cease firing on; -1 means all tracks (cease fire only).</summary>
        public int TrackId;
        public WeaponClass Weapon;
        /// <summary>Missiles or bursts to fire; 0 means the doctrine default.</summary>
        public int Rounds;

        // Doctrine / ROE
        public DoctrineMode Mode;
        public Roe Roe;

        // Breach
        /// <summary>Where the hole is, in the ship frame (x starboard, y up, z forward; origin at the waterline amidships), m.</summary>
        public Vector3 PointShip;
        /// <summary>The hole's area, m².</summary>
        public float AreaM2;

        public static Order Helm(OrderIssuer issuer, float courseDeg, float speedKn) =>
            new Order { Kind = OrderKind.Helm, Issuer = issuer, CourseDeg = courseDeg, SpeedKn = speedKn, TrackId = -1 };

        public static Order EngineOrder(OrderIssuer issuer, EngineBell bell) =>
            new Order { Kind = OrderKind.Bell, Issuer = issuer, Bell = bell, TrackId = -1 };

        public static Order Rudder(OrderIssuer issuer, float rudderDeg) =>
            new Order { Kind = OrderKind.Rudder, Issuer = issuer, RudderDeg = rudderDeg, TrackId = -1 };

        public static Order SteerCourse(OrderIssuer issuer, float courseDeg) =>
            new Order { Kind = OrderKind.Course, Issuer = issuer, CourseDeg = courseDeg, TrackId = -1 };

        public static Order FollowRoute(OrderIssuer issuer, int routeId, float speedKn) =>
            new Order { Kind = OrderKind.Route, Issuer = issuer, RouteId = routeId, SpeedKn = speedKn, TrackId = -1 };

        public static Order Engage(OrderIssuer issuer, int trackId, WeaponClass weapon, int rounds = 0) =>
            new Order { Kind = OrderKind.Engage, Issuer = issuer, TrackId = trackId, Weapon = weapon, Rounds = rounds };

        public static Order CeaseFire(OrderIssuer issuer, int trackId = -1) =>
            new Order { Kind = OrderKind.CeaseFire, Issuer = issuer, TrackId = trackId };

        public static Order SetDoctrine(OrderIssuer issuer, WeaponClass weapon, DoctrineMode mode) =>
            new Order { Kind = OrderKind.Doctrine, Issuer = issuer, Weapon = weapon, Mode = mode, TrackId = -1 };

        public static Order SetRoe(OrderIssuer issuer, Roe roe) =>
            new Order { Kind = OrderKind.Roe, Issuer = issuer, Roe = roe, TrackId = -1 };

        public static Order Breach(OrderIssuer issuer, Vector3 pointShip, float areaM2) =>
            new Order { Kind = OrderKind.Breach, Issuer = issuer, PointShip = pointShip, AreaM2 = areaM2, TrackId = -1 };

        public override string ToString() => Kind switch
        {
            OrderKind.Helm => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: helm course {CourseDeg:0}°, {SpeedKn:0} kn"),
            OrderKind.Engage => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: engage track {TrackId} with {Weapon}"),
            OrderKind.CeaseFire => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: cease fire {(TrackId < 0 ? "all" : "track " + TrackId)}"),
            OrderKind.Doctrine => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: {Weapon} doctrine {Mode}"),
            OrderKind.Bell => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: all engines {Bell}"),
            OrderKind.Rudder => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: rudder {RudderDeg:0}°"),
            OrderKind.Route => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: follow route {RouteId} at {SpeedKn:0} kn"),
            OrderKind.Course => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: steer {CourseDeg:000}"),
            OrderKind.Breach => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: breach {AreaM2:0.00} m² at ({PointShip.x:+0.0;-0.0}, {PointShip.y:+0.0;-0.0}, {PointShip.z:+0.0;-0.0}) m"),
            _ => System.FormattableString.Invariant($"#{Sequence} t{Tick} {Issuer}: ROE {Roe}"),
        };
    }
}
