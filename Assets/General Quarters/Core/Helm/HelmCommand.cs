using DeepWater.GeneralQuarters.Core.Orders;

namespace DeepWater.GeneralQuarters.Core.Helm
{
    /// <summary>How the Helm steers: hold an ordered course, hold a rudder angle by hand, or follow a route.</summary>
    public enum CourseMode : byte
    {
        Hold = 0,
        Manual = 1,
        Route = 2,
    }

    /// <summary>How the Helm makes speed: hold ordered knots, or answer a telegraph bell.</summary>
    public enum SpeedMode : byte
    {
        Hold = 0,
        Bell = 1,
    }

    /// <summary>
    /// What the conn has ordered, as standing state. Orders change it (<see cref="Apply"/>); the Helm element
    /// reads it every step. Course and speed are independent: a bell changes how speed is made and leaves the
    /// course alone; a rudder order takes the wheel and leaves the engines alone.
    /// </summary>
    public struct HelmCommand
    {
        public CourseMode Course;
        /// <summary>Ordered course, compass degrees (0 = +Z, 90 = +X).</summary>
        public float CourseDeg;
        /// <summary>Ordered rudder angle in <see cref="CourseMode.Manual"/>, degrees; positive turns to starboard.</summary>
        public float RudderDeg;
        public int RouteId;

        public SpeedMode Speed;
        public float SpeedKn;
        public EngineBell Bell;

        /// <summary>Order sequence of the last change, so a log line can name what the Helm is answering.</summary>
        public int OrderSequence;

        /// <summary>Lying stopped: all stop, rudder amidships, on <paramref name="courseDeg"/>.</summary>
        public static HelmCommand Stopped(float courseDeg) => new HelmCommand
        {
            Course = CourseMode.Hold, CourseDeg = courseDeg, Speed = SpeedMode.Bell, Bell = EngineBell.Stop, OrderSequence = -1,
        };

        /// <summary>The command after <paramref name="order"/>. Orders that are not helm orders leave it unchanged.</summary>
        public static HelmCommand Apply(HelmCommand c, in Order order)
        {
            switch (order.Kind)
            {
                case OrderKind.Helm:
                    c.Course = CourseMode.Hold;
                    c.CourseDeg = CompassMath.Normalize(order.CourseDeg);
                    c.Speed = SpeedMode.Hold;
                    c.SpeedKn = order.SpeedKn;
                    break;
                case OrderKind.Bell:
                    c.Speed = SpeedMode.Bell;
                    c.Bell = order.Bell;
                    break;
                case OrderKind.Rudder:
                    c.Course = CourseMode.Manual;
                    c.RudderDeg = order.RudderDeg;
                    break;
                case OrderKind.Route:
                    c.Course = CourseMode.Route;
                    c.RouteId = order.RouteId;
                    c.Speed = SpeedMode.Hold;
                    c.SpeedKn = order.SpeedKn;
                    break;
                default:
                    return c;
            }
            c.OrderSequence = order.Sequence;
            return c;
        }
    }

    /// <summary>Compass arithmetic: bearings in [0, 360), signed differences in (−180, 180].</summary>
    public static class CompassMath
    {
        public static float Normalize(float deg)
        {
            float d = deg % 360f;
            return d < 0f ? d + 360f : d;
        }

        /// <summary>The turn from <paramref name="fromDeg"/> to <paramref name="toDeg"/>, degrees; positive is to starboard.</summary>
        public static float Difference(float fromDeg, float toDeg)
        {
            float d = Normalize(toDeg - fromDeg);
            return d > 180f ? d - 360f : d;
        }
    }
}
