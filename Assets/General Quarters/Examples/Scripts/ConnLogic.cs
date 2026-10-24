using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Orders;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>One frame of conn input, whatever device it came from (see ConnInput and the "Conn" action map).</summary>
    public struct ConnInputFrame
    {
        /// <summary>Rudder demand, −1 (left) … +1 (right), as a fraction of maximum rudder.</summary>
        public float Rudder;
        /// <summary>Course change demand, −1 (left) … +1 (right); held, it turns the ordered course.</summary>
        public float CourseChange;
        public bool RingUp, RingDown, Steady, Midships, AllStop;
    }

    /// <summary>
    /// What the conn's controls mean as Orders. Pure: no Unity input or scene, so it is tested on its own.
    ///   - Rudder off centre takes the wheel (a Rudder order); back to centre, she steadies on her heading.
    ///   - Ring up / down moves one telegraph bell; from speed hold it starts at the bell nearest the ordered speed.
    ///   - Course change turns the ordered course while held, as course orders, and leaves the engines alone.
    /// </summary>
    public sealed class ConnLogic
    {
        /// <summary>Degrees per second the ordered course turns while a course key or d-pad is held.</summary>
        public const float CourseRateDegPerS = 12f;
        const float RudderDeadband = 0.05f;
        const float RudderResendDeg = 1f;

        readonly float _maxRudderDeg;
        readonly float[] _aheadBellKn;
        readonly List<Order> _orders = new List<Order>();
        bool _onTheWheel, _turningCourse;
        float _lastRudderDeg, _courseOrderDeg;
        int _sentCourse = -1;

        public ConnLogic(float maxRudderDeg, float[] aheadBellKn)
        {
            _maxRudderDeg = maxRudderDeg;
            _aheadBellKn = aheadBellKn;
        }

        /// <summary>The Orders this frame's input gives, in the order to issue them. The list is reused.</summary>
        public IReadOnlyList<Order> Step(in ConnInputFrame input, in HelmCommand current, float headingDeg, float dt)
        {
            _orders.Clear();

            if (input.AllStop) Add(Order.EngineOrder(OrderIssuer.Captain, EngineBell.Stop));
            else if (input.RingUp) Add(Order.EngineOrder(OrderIssuer.Captain, Next(current, +1)));
            else if (input.RingDown) Add(Order.EngineOrder(OrderIssuer.Captain, Next(current, -1)));

            if (input.Midships)
            {
                _onTheWheel = false;
                Add(Order.Rudder(OrderIssuer.Captain, 0f));
            }
            else if (Mathf.Abs(input.Rudder) > RudderDeadband)
            {
                float deg = Mathf.Clamp(input.Rudder, -1f, 1f) * _maxRudderDeg;
                if (!_onTheWheel || Mathf.Abs(deg - _lastRudderDeg) >= RudderResendDeg)
                {
                    Add(Order.Rudder(OrderIssuer.Captain, deg));
                    _lastRudderDeg = deg;
                }
                _onTheWheel = true;
            }
            else if (_onTheWheel)
            {
                // Wheel let go: steady as she goes, on the heading she has now.
                _onTheWheel = false;
                Steady(headingDeg);
            }

            if (input.Steady) Steady(headingDeg);

            if (Mathf.Abs(input.CourseChange) > 0.1f)
            {
                if (!_turningCourse)
                {
                    _courseOrderDeg = current.Course == CourseMode.Hold ? current.CourseDeg : Mathf.Round(headingDeg);
                    _sentCourse = Mathf.RoundToInt(_courseOrderDeg);
                    _turningCourse = true;
                }
                _courseOrderDeg = CompassMath.Normalize(_courseOrderDeg + Mathf.Clamp(input.CourseChange, -1f, 1f) * CourseRateDegPerS * dt);
                int whole = Mathf.RoundToInt(_courseOrderDeg) % 360;
                if (whole != _sentCourse)
                {
                    Add(Order.SteerCourse(OrderIssuer.Captain, whole));
                    _sentCourse = whole;
                }
            }
            else _turningCourse = false;

            return _orders;
        }

        void Steady(float headingDeg)
        {
            int course = Mathf.RoundToInt(CompassMath.Normalize(headingDeg)) % 360;
            _courseOrderDeg = course;
            _sentCourse = course;
            Add(Order.SteerCourse(OrderIssuer.Captain, course));
        }

        void Add(Order o) => _orders.Add(o);

        /// <summary>The bell one step up or down. From speed hold, the step starts at the ordered speed.</summary>
        public EngineBell Next(in HelmCommand current, int step)
        {
            int bell;
            if (current.Speed == SpeedMode.Bell) bell = (int)current.Bell + step;
            else if (step > 0)
            {
                bell = _aheadBellKn.Length - 1;   // the first ahead bell faster than the ordered speed
                for (int i = 0; i < _aheadBellKn.Length; i++)
                    if (_aheadBellKn[i] > current.SpeedKn + 0.5f) { bell = i; break; }
            }
            else
            {
                bell = (int)EngineBell.BackOneThird;   // the first ahead bell slower than the ordered speed, or back ⅓
                for (int i = _aheadBellKn.Length - 1; i >= 0; i--)
                    if (_aheadBellKn[i] < current.SpeedKn - 0.5f) { bell = i; break; }
            }
            return (EngineBell)Mathf.Clamp(bell, (int)EngineBell.BackEmergency, (int)EngineBell.AheadFlank);
        }
    }
}
