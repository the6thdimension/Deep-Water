using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.DamageControl
{
    /// <summary>Whether she is lost, counted from the flooding (G60).</summary>
    public enum FloodVerdict : byte
    {
        Afloat = 0,
        /// <summary>The water aboard leaves less buoyant volume than her weight.</summary>
        Sinking = 1,
        /// <summary>Heeled past the capsize angle for the capsize time.</summary>
        Capsized = 2,
    }

    /// <summary>The ship's spaces and the water in them, for the hull, the HUD and the debrief.</summary>
    public interface IFlooding
    {
        FloodPlan Plan { get; }
        FloodModel Water { get; }
        FloodVerdict Verdict { get; }
        /// <summary>True once a space has a hole.</summary>
        bool IsBreached(int space);
        int Breaches { get; }
        /// <summary>Spaces with water in them, and spaces full.</summary>
        int WetSpaces { get; }
        int FullSpaces { get; }

        /// <summary>As measured from the pose: list (starboard down +), deg; trim (by the head +) over the waterline, m; draft amidships, m.</summary>
        float ListDeg { get; }
        float TrimM { get; }
        float DraftM { get; }
        /// <summary>The intact ship's GM at the drawn waterline, and the damaged ship's now (refreshed once a second).</summary>
        float IntactGmM { get; }
        DamagedStability Stability { get; }
        /// <summary>(ρ·hull volume − ship − water) / (ρ·hull volume − ship): 1 intact, 0 when she can no longer float.</summary>
        float ReserveBuoyancy { get; }

        float PeakListDeg { get; }
        float PeakTrimM { get; }
        float MinGmM { get; }
    }

    /// <summary>
    /// Damage Control L1: compartments and flooding (G53–G60). The hull is cut into spaces from its own probe columns
    /// (<see cref="FloodPlan"/>); holes from Breach Orders and from hits let the sea in, and water moves through hatches,
    /// doors and weather openings by orifice flow (<see cref="FloodModel"/>). The water's mass and centroid ride on the
    /// hull's Rigidbody (the host applies them, G55). She is lost when the flooding says so, never by hit points (G60).
    /// Systems still take hit-point damage as at L0. Crew action (pumps, plugging, counter-flooding) is L2 (G58).
    /// </summary>
    public sealed class DamageControlL1 : IDamageControl, IFlooding
    {
        const string Rule = "Damage Control L1 (compartments and flooding)";
        const int StabilityPeriodTicks = 50;
        static readonly float[] ListMarksDeg = { 5f, 10f, 15f, 20f, 30f, 45f };

        readonly DamageProfileData _damage;
        readonly FloodingProfileData _p;
        readonly SystemDamage _systems;
        readonly FloodPlan _plan;
        readonly FloodModel _water;
        readonly bool[] _breached, _openToSea;
        readonly int[] _wetEvent, _fullEvent;
        readonly List<int> _openingCause = new List<int>();
        int _lastFloodEvent = -1, _listMark;
        float _capsizeS;
        long _nextStabilityTick;

        public DamageControlL1(in DamageProfileData damage, in FloodingProfileData flooding)
        {
            _damage = damage;
            _p = flooding;
            HullHitPoints = damage.HullHitPoints;
            _systems = new SystemDamage(damage);
            _plan = new FloodPlan(flooding);
            _water = new FloodModel(_plan);
            int n = _plan.Spaces.Length;
            _breached = new bool[n];
            _openToSea = new bool[n];
            _wetEvent = new int[n];
            _fullEvent = new int[n];
            for (int i = 0; i < n; i++) { _wetEvent[i] = -1; _fullEvent[i] = -1; }
            for (int i = 0; i < _water.OpeningCount; i++) _openingCause.Add(-1);
            IntactGmM = FloodHydrostatics.Evaluate(_plan, _water, _openToSea, 0f, 0f).GmM;
            Stability = FloodHydrostatics.Evaluate(_plan, _water, _openToSea, 0f, 0f);
            MinGmM = IntactGmM;
            ReserveBuoyancy = 1f;
            DraftM = -_p.KeelY;
        }

        public FidelityLevel Level => FidelityLevel.L1;
        public float HullHitPoints { get; private set; }
        public bool OutOfAction => Verdict != FloodVerdict.Afloat;
        public IFlooding Flooding => this;
        public bool IsOnline(ShipSystem system) => _systems.IsOnline(system);

        public FloodPlan Plan => _plan;
        public FloodModel Water => _water;
        public FloodVerdict Verdict { get; private set; }
        public bool IsBreached(int space) => _breached[space];
        public int Breaches { get; private set; }
        public int WetSpaces { get; private set; }
        public int FullSpaces { get; private set; }
        public float ListDeg { get; private set; }
        public float TrimM { get; private set; }
        public float DraftM { get; private set; }
        public float IntactGmM { get; }
        public DamagedStability Stability { get; private set; }
        public float ReserveBuoyancy { get; private set; }
        public float PeakListDeg { get; private set; }
        public float PeakTrimM { get; private set; }
        public float MinGmM { get; private set; }

        /// <summary>A hit's hole (G57): area ∝ warhead^(2/3), cube-root blast scaling, through the profile's one calibration point.</summary>
        public float HoleAreaM2(float warheadKg) =>
            warheadKg > 0f && _p.HoleRefWarheadKg > 0f ? _p.HoleAreaAtRefM2 * Mathf.Pow(warheadKg / _p.HoleRefWarheadKg, 2f / 3f) : 0f;

        public void ApplyHit(long tick, in ShipHit hit, in OwnShipState own, int causeEvent, EngagementLog log, List<ShipSystem> newlyOffline)
        {
            Vector3 local = own.ToShipFrame(hit.Point);
            HullHitPoints = Mathf.Max(0f, HullHitPoints - hit.WarheadKg * _damage.HullDamagePerWarheadKg);
            int nearest = _systems.Nearest(local, out float distance);
            int space = _plan.SpaceAt(local);
            float area = HoleAreaM2(hit.WarheadKg);
            string into = space >= 0
                ? System.FormattableString.Invariant($", a {area:0.0} m² hole into {_plan.Spaces[space].Name}")
                : ", above or beyond the watertight hull: nothing floods";
            int hitEvent = log.Add(tick, EventKind.ShipHit, Rule,
                System.FormattableString.Invariant($"{hit.Kind} hit at {local.z:+0;-0} m from amidships ({hit.WarheadKg:0} kg warhead)") + into + _systems.Where(nearest, distance),
                -1, causeEvent, root: RootKind.Scenario);
            log.Locate(hitEvent, hit.Point);
            _systems.Damage(tick, nearest, hit.WarheadKg, hitEvent, own, Rule, log, newlyOffline);
            if (space >= 0 && area > 0f) OpenHole(tick, local, area, space, hitEvent, own, log);
        }

        public void Breach(long tick, in Order order, in OwnShipState own, int orderEvent, EngagementLog log)
        {
            int space = _plan.SpaceAt(order.PointShip);
            if (space < 0 || order.AreaM2 <= 0f)
            {
                log.Add(tick, EventKind.Note, Rule, space < 0
                    ? "breach outside the subdivided hull (a peak or the superstructure): nothing floods at L1"
                    : "breach with no area: nothing floods", -1, orderEvent);
                return;
            }
            OpenHole(tick, order.PointShip, order.AreaM2, space, orderEvent, own, log);
        }

        void OpenHole(long tick, Vector3 pointShip, float areaM2, int space, int cause, in OwnShipState own, EngagementLog log)
        {
            _water.AddOpening(new FloodOpening { Kind = OpeningKind.Hole, SpaceA = space, SpaceB = -1, PointShip = pointShip, AreaM2 = areaM2 });
            float depth = -(own.Position.y + Vector3.Dot(own.VerticalAxes, pointShip));
            int ev = log.Add(tick, EventKind.Breach, Rule, System.FormattableString.Invariant(
                $"hole {areaM2:0.00} m² in {_plan.Spaces[space].Name} at ({pointShip.x:+0.0;-0.0}, {pointShip.y:+0.0;-0.0}, {pointShip.z:+0.0;-0.0}) m, ") +
                (depth > 0f ? System.FormattableString.Invariant($"{depth:0.0} m below the sea") : System.FormattableString.Invariant($"{-depth:0.0} m above the sea")),
                -1, cause);
            _openingCause.Add(ev);
            if (!_breached[space]) { _breached[space] = true; Breaches++; }
        }

        public void Step(long tick, float dt, in OwnShipState own, EngagementLog log)
        {
            _water.Step(dt, own);
            LogFlooding(tick, log);

            ListDeg = own.RollDeg;
            TrimM = -_p.LengthWlM * Mathf.Tan(own.PitchDeg * Mathf.Deg2Rad);
            DraftM = -(own.Position.y + own.VerticalAxes.y * _p.KeelY);
            if (Mathf.Abs(ListDeg) > Mathf.Abs(PeakListDeg)) PeakListDeg = ListDeg;
            if (Mathf.Abs(TrimM) > Mathf.Abs(PeakTrimM)) PeakTrimM = TrimM;
            LogList(tick, log);

            float hull = _p.SeawaterDensityKgM3 * _plan.HullVolumeM3;
            ReserveBuoyancy = (hull - _p.ShipMassKg - _water.WaterMassKg) / Mathf.Max(1f, hull - _p.ShipMassKg);

            if (tick >= _nextStabilityTick)
            {
                _nextStabilityTick = tick + StabilityPeriodTicks;
                for (int s = 0; s < _openToSea.Length; s++) _openToSea[s] = OpenToSea(s);
                Stability = FloodHydrostatics.Evaluate(_plan, _water, _openToSea, own.Position.y, own.PitchDeg);
                if (!float.IsNaN(Stability.GmM) && Stability.GmM < MinGmM) MinGmM = Stability.GmM;
            }

            Judge(tick, dt, own, log);
        }

        /// <summary>A space with a hole under the sea is in free communication with it: lost buoyancy, not weight.</summary>
        bool OpenToSea(int space)
        {
            if (!_breached[space]) return false;
            for (int i = 0; i < _water.OpeningCount; i++)
            {
                FloodOpening o = _water.Opening(i);
                if (o.Kind == OpeningKind.Hole && o.SpaceA == space && _water.WorldY(o.PointShip) < 0f) return true;
            }
            return false;
        }

        /// <summary>The notable moments only: a space starts taking water (and from where), a space is full.</summary>
        void LogFlooding(long tick, EngagementLog log)
        {
            int wet = 0, full = 0;
            for (int s = 0; s < _plan.Spaces.Length; s++)
            {
                if (_water.Volume(s) <= 0f) continue;
                wet++;
                if (_water.IsFull(s)) full++;
                if (_wetEvent[s] < 0)
                {
                    int via = StrongestInflow(s, out float q);
                    if (via < 0 || q <= 0f)
                    {
                        // Water put there by the scenario or a test, not by an opening.
                        _wetEvent[s] = log.Add(tick, EventKind.Flooding, Rule,
                            System.FormattableString.Invariant($"{_plan.Spaces[s].Name} holds {_water.Volume(s):0} m³ of water"), root: RootKind.Scenario);
                        _lastFloodEvent = _wetEvent[s];
                        continue;
                    }
                    FloodOpening o = _water.Opening(via);
                    int other = o.SpaceA == s ? o.SpaceB : o.SpaceA;
                    string from = o.Kind switch
                    {
                        OpeningKind.Hole => "through the hole",
                        OpeningKind.Hatch => $"through the hatch from {_plan.Spaces[other].Name}",
                        OpeningKind.Door => $"through the door from {_plan.Spaces[other].Name}",
                        _ => "over the deck edge (weather openings)",
                    };
                    int cause = o.Kind == OpeningKind.Hole ? _openingCause[via]
                        : o.Kind == OpeningKind.Weather ? _lastFloodEvent
                        : other >= 0 ? _wetEvent[other] : _lastFloodEvent;
                    _wetEvent[s] = log.Add(tick, EventKind.Flooding, Rule,
                        System.FormattableString.Invariant($"{_plan.Spaces[s].Name} taking water {from}: {q:0.00} m³/s"), -1, cause);
                    _lastFloodEvent = _wetEvent[s];
                }
                if (_water.IsFull(s) && _fullEvent[s] < 0)
                {
                    _fullEvent[s] = log.Add(tick, EventKind.Flooding, Rule,
                        System.FormattableString.Invariant($"{_plan.Spaces[s].Name} flooded: {_water.Volume(s):0} m³"), -1, _wetEvent[s]);
                    _lastFloodEvent = _fullEvent[s];
                }
            }
            WetSpaces = wet;
            FullSpaces = full;
        }

        int StrongestInflow(int space, out float flow)
        {
            int best = -1;
            flow = float.MinValue;
            for (int i = 0; i < _water.OpeningCount; i++)
            {
                FloodOpening o = _water.Opening(i);
                float into = o.SpaceA == space ? _water.Flow(i) : o.SpaceB == space ? -_water.Flow(i) : float.MinValue;
                if (into > flow) { flow = into; best = i; }
            }
            return best;
        }

        void LogList(long tick, EngagementLog log)
        {
            if (_lastFloodEvent < 0 || _listMark >= ListMarksDeg.Length || Mathf.Abs(ListDeg) < ListMarksDeg[_listMark]) return;
            log.Add(tick, EventKind.Flooding, Rule, System.FormattableString.Invariant(
                $"listing {Mathf.Abs(ListDeg):0} deg to {(ListDeg > 0f ? "starboard" : "port")}; GM {Stability.GmM:0.00} m (intact {IntactGmM:0.00})"), -1, _lastFloodEvent);
            _listMark++;
        }

        /// <summary>G60: lost only by what the flooding did, measured.</summary>
        void Judge(long tick, float dt, in OwnShipState own, EngagementLog log)
        {
            if (Verdict != FloodVerdict.Afloat) return;
            _capsizeS = Mathf.Abs(own.RollDeg) > _p.CapsizeHeelDeg ? _capsizeS + dt : 0f;
            string why;
            if (ReserveBuoyancy <= 0f)
            {
                Verdict = FloodVerdict.Sinking;
                why = System.FormattableString.Invariant(
                    $"SINKING: {_water.WaterMassKg / 1000f:0} t of water aboard; the hull's {_plan.HullVolumeM3:0} m³ floats {_p.SeawaterDensityKgM3 * _plan.HullVolumeM3 / 1000f:0} t, she weighs {(_p.ShipMassKg + _water.WaterMassKg) / 1000f:0} t: ship OUT OF ACTION");
            }
            else if (_capsizeS >= _p.CapsizeHoldS)
            {
                Verdict = FloodVerdict.Capsized;
                why = System.FormattableString.Invariant(
                    $"CAPSIZED: heeled past {_p.CapsizeHeelDeg:0} deg for {_p.CapsizeHoldS:0} s ({own.RollDeg:+0;-0} deg, GM {Stability.GmM:0.00} m): ship OUT OF ACTION");
            }
            else return;
            int ev = log.Add(tick, EventKind.SystemOffline, Rule, why, -1, _lastFloodEvent);
            log.Locate(ev, own.Position, WeaponClass.None, (int)ShipSystem.Hull);
        }

        public ulong Fingerprint(ulong h)
        {
            h = CombatFix.Mix(CombatFix.Mix(h, HullHitPoints), OutOfAction);
            h = _systems.Fingerprint(h);
            h = CombatFix.Mix(CombatFix.Mix(h, (int)Verdict), _water.OpeningCount);
            for (int s = 0; s < _plan.Spaces.Length; s++) h = CombatFix.Mix(h, _water.Volume(s));
            return h;
        }
    }
}
