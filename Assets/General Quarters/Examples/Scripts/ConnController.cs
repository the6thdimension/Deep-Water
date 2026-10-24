using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.GeneralQuarters.Modules.Propulsion;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The conn: turns ConnInput (keyboard, gamepad, joystick) and panel clicks into Orders for one ShipDrive, and
    /// keeps a short log of what it ordered. Everything goes through <see cref="Issue"/>, so a key, a stick and
    /// a button give the ship the same orders. On the range the orders go into the combat system's order log
    /// (<see cref="ShipCombat"/>), and the conn reads its controls only while the player holds the Captain seat.
    /// </summary>
    [RequireComponent(typeof(ConnInput))]
    public sealed class ConnController : MonoBehaviour
    {
        [SerializeField] ShipDrive ship;
        [SerializeField] HelmProfileSO helmProfile;
        [Tooltip("On the range: orders go through the combat system, so its order log holds them. Empty: straight to the drive.")]
        [SerializeField] ShipCombat combat;

        ConnInput _input;
        ConnLogic _logic;
        readonly List<string> _log = new List<string>();
        OrderKind _lastKind;
        float _lastLogTime = float.NegativeInfinity;

        public ShipDrive Ship => ship;
        public ConnInput Controls => _input;
        public HelmProfileSO HelmProfile => helmProfile;
        /// <summary>The orders given, newest last (at most 8).</summary>
        public IReadOnlyList<string> RecentOrders => _log;
        /// <summary>The conn's standing orders as the ship is answering them.</summary>
        public HelmCommand Command => combat != null && combat.Combat != null ? combat.Combat.Helm : ship.Command;
        /// <summary>Whether the player has the conn (the Captain seat). Without it the controls are not read.</summary>
        public bool HasTheConn
        {
            get => _hasTheConn;
            set { _hasTheConn = value; _logic = null; }   // a fresh start when the conn comes back: no stale rudder
        }

        bool _hasTheConn = true;

        /// <summary>Point the conn at a ship (the drive menu does this for a spawned ship; the range adds its combat system).</summary>
        public void Bind(ShipDrive drive, HelmProfileSO helm, ShipCombat combatHost = null)
        {
            ship = drive;
            helmProfile = helm;
            combat = combatHost;
            _logic = null;
        }

        void Awake() => _input = GetComponent<ConnInput>();

        void Update()
        {
            if (!_hasTheConn || ship == null || !ship.isActiveAndEnabled || ship.EngineRoom == null || !_input.Ready) return;
            if (_logic == null)
            {
                if (helmProfile == null) { Debug.LogError("[ConnController] needs the ship's Helm profile (for the bells).", this); enabled = false; return; }
                _logic = new ConnLogic(ship.EngineRoom.MaxRudderDeg, helmProfile.profile.AheadBellKn);
            }
            IReadOnlyList<Order> orders = _logic.Step(_input.Frame(), Command, ship.Motion.HeadingDeg, Time.unscaledDeltaTime);
            for (int i = 0; i < orders.Count; i++) Issue(orders[i]);
        }

        /// <summary>Give the ship an order now and log it.</summary>
        public void Issue(Order order)
        {
            if (ship == null) return;
            if (combat != null) combat.Issue(order); else ship.Issue(order);
            string line = $"{Time.time,6:0}s  {Describe(order)}";
            // Holding a course key or the rudder stick sends an order per degree; the log keeps only the latest.
            bool sameRun = _log.Count > 0 && order.Kind == _lastKind && (order.Kind == OrderKind.Course || order.Kind == OrderKind.Rudder)
                           && Time.time - _lastLogTime < 1.5f;
            if (sameRun) _log[_log.Count - 1] = line; else _log.Add(line);
            if (_log.Count > 8) _log.RemoveAt(0);
            _lastKind = order.Kind;
            _lastLogTime = Time.time;
        }

        public void RingBell(EngineBell bell) => Issue(Order.EngineOrder(OrderIssuer.Captain, bell));
        public void Rudder(float degrees) => Issue(Order.Rudder(OrderIssuer.Captain, degrees));
        public void SteerCourse(float degrees) => Issue(Order.SteerCourse(OrderIssuer.Captain, degrees));
        public void HoldSpeed(float knots) =>
            Issue(Order.Helm(OrderIssuer.Captain, Command.Course == CourseMode.Hold ? Command.CourseDeg : Mathf.Round(ship.Motion.HeadingDeg), knots));
        public void Steady() => SteerCourse(Mathf.Round(ship.Motion.HeadingDeg) % 360f);

        /// <summary>The order in the conn's words.</summary>
        public static string Describe(in Order o)
        {
            switch (o.Kind)
            {
                case OrderKind.Bell: return BellName(o.Bell);
                case OrderKind.Rudder:
                    if (Mathf.Abs(o.RudderDeg) < 0.5f) return "Rudder amidships";
                    return $"{(o.RudderDeg > 0f ? "Right" : "Left")} {Mathf.Abs(o.RudderDeg):0}° rudder";
                case OrderKind.Course: return $"Steer {Mathf.RoundToInt(o.CourseDeg) % 360:000}";
                case OrderKind.Helm: return $"Steer {Mathf.RoundToInt(o.CourseDeg) % 360:000}, make {o.SpeedKn:0} knots";
                case OrderKind.Route: return $"Follow route {o.RouteId} at {o.SpeedKn:0} knots";
                default: return o.ToString();
            }
        }

        public static string BellName(EngineBell bell)
        {
            switch (bell)
            {
                case EngineBell.BackEmergency: return "Back emergency";
                case EngineBell.BackFull: return "All back full";
                case EngineBell.BackTwoThirds: return "All back two thirds";
                case EngineBell.BackOneThird: return "All back one third";
                case EngineBell.Stop: return "All stop";
                case EngineBell.AheadOneThird: return "All ahead one third";
                case EngineBell.AheadTwoThirds: return "All ahead two thirds";
                case EngineBell.AheadStandard: return "All ahead standard";
                case EngineBell.AheadFull: return "All ahead full";
                default: return "All ahead flank";
            }
        }
    }
}
