using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.DamageControl
{
    /// <summary>Where a space sits in its station (G54).</summary>
    public enum SpaceKind : byte
    {
        Hold = 0,
        Upper = 1,
        WingPort = 2,
        WingStarboard = 3,
    }

    /// <summary>What a hold holds. It sets the permeability (SOLAS II-1 Reg. 7-3).</summary>
    public enum SpaceUse : byte
    {
        Stores = 0,
        Machinery = 1,
        Accommodation = 2,
        Void = 3,
    }

    /// <summary>What joins a space to the sea or to another space (G56).</summary>
    public enum OpeningKind : byte
    {
        /// <summary>Damage: a hole to the sea.</summary>
        Hole = 0,
        /// <summary>From a hold up through the damage-control deck into the upper space above it.</summary>
        Hatch = 1,
        /// <summary>Through a transverse bulkhead, between neighbouring upper spaces.</summary>
        Door = 2,
        /// <summary>From an upper space to the weather at the main deck edge (vents, doors, uptakes).</summary>
        Weather = 3,
    }

    /// <summary>
    /// One Sea State probe column in the ship frame (x starboard, y up, z forward; origin at the waterline amidships):
    /// it spans [<see cref="BottomY"/>, <see cref="TopY"/>] at (<see cref="X"/>, <see cref="Z"/>) and stands for
    /// <see cref="AreaM2"/> of waterplane. The hull floods in the same columns it floats on, so water and buoyancy agree.
    /// </summary>
    [Serializable]
    public struct HullColumn
    {
        public float X, Z;
        public float AreaM2;
        public float BottomY, TopY;
    }

    /// <summary>A watertight section between two transverse bulkheads: one of Blender's stations (G53).</summary>
    [Serializable]
    public struct HullStation
    {
        public string Name;
        public float AftZ, ForeZ;
        public SpaceUse HoldUse;
    }

    /// <summary>
    /// Everything Damage Control L1 floods by: the hull's columns and stations, the subdivision rules (G53, G54), the
    /// openings (G56), the ship's dry mass, and the tuning for holes from hits (G57) and for losing her (G60). Plain data,
    /// baked from the hull profile and the loadout.
    /// </summary>
    [Serializable]
    public struct FloodingProfileData
    {
        public HullColumn[] Columns;
        public HullStation[] Stations;
        /// <summary>The damage-control deck, ship-frame y: holds below it, upper spaces above.</summary>
        public float DeckY;
        /// <summary>A wing station's hold columns beyond this |x| are its port and starboard wing spaces.</summary>
        public float WingInboardX;
        /// <summary>The first and last station (indices) that have wing spaces.</summary>
        public int WingFirstStation, WingLastStation;
        public float PermeabilityStores, PermeabilityMachinery, PermeabilityAccommodation, PermeabilityVoid;
        public float HatchAreaM2, DoorAreaM2, WeatherAreaM2;
        /// <summary>A door's sill above the damage-control deck, m.</summary>
        public float DoorSillM;

        public float ShipMassKg;
        /// <summary>The dry ship's centre of mass, ship frame.</summary>
        public Vector3 ShipCentreOfMass;
        /// <summary>The keel's ship-frame y amidships, for the draft.</summary>
        public float KeelY;
        public float LengthWlM;

        public float SeawaterDensityKgM3;
        public float Gravity;
        /// <summary>Orifice discharge coefficient Cd in Q = Cd·A·√(2g·Δh).</summary>
        public float DischargeCoefficient;

        /// <summary>A hit's hole: area = <see cref="HoleAreaAtRefM2"/> × (warhead / <see cref="HoleRefWarheadKg"/>)^(2/3).</summary>
        public float HoleAreaAtRefM2, HoleRefWarheadKg;

        /// <summary>CAPSIZED: heel beyond this, held for <see cref="CapsizeHoldS"/>.</summary>
        public float CapsizeHeelDeg, CapsizeHoldS;

        public bool IsValid => Columns != null && Columns.Length > 0 && Stations != null && Stations.Length > 0;
    }

    /// <summary>
    /// One space's share of one probe column: the part between <see cref="Y0"/> and <see cref="Y1"/>. Its world bottom is
    /// reckoned the way Sea State reckons the probe's (vertical in the world, from the column centre), so a space open to
    /// the sea holds exactly the water its columns' buoyancy loses.
    /// </summary>
    public struct FloodSegment
    {
        public float X, Z, AreaM2;
        public float Y0, Y1;
        public float ColumnCentreY;
    }

    /// <summary>A space: its place, its permeability and its segments in <see cref="FloodPlan.Segments"/>.</summary>
    public struct FloodSpace
    {
        public string Name;
        public SpaceKind Kind;
        public int Station;
        public SpaceUse Use;
        public float Permeability;
        public int FirstSegment, SegmentCount;
        /// <summary>Water it holds when full: permeability × its volume, m³.</summary>
        public float CapacityM3;
        public Vector3 FullCentroidShip;
        public float BottomY, TopY;
    }

    /// <summary>An opening from <see cref="SpaceA"/> to <see cref="SpaceB"/>, or to the sea when SpaceB is −1.</summary>
    public struct FloodOpening
    {
        public OpeningKind Kind;
        public int SpaceA, SpaceB;
        public Vector3 PointShip;
        public float AreaM2;
        public bool ToSea => SpaceB < 0;
    }

    /// <summary>
    /// The ship cut into spaces (G53, G54): each of Blender's stations splits at the damage-control deck into a hold and
    /// an upper space, and the wing stations' holds lose their outboard columns to port and starboard wing spaces. Every
    /// space is made of the hull's own probe columns, so its volume follows the real hull. Built once from the profile.
    /// </summary>
    public sealed class FloodPlan
    {
        public readonly FloodingProfileData Profile;
        public readonly FloodSpace[] Spaces;
        public readonly FloodSegment[] Segments;
        /// <summary>The planned openings: hatches, doors, weather openings. Holes are added as she is damaged.</summary>
        public readonly FloodOpening[] Openings;
        /// <summary>The hull's whole volume up to the deck, m³ (every column, full height).</summary>
        public readonly float HullVolumeM3;

        readonly int[] _stationFirstSpace;

        public FloodPlan(in FloodingProfileData p)
        {
            if (!p.IsValid) throw new ArgumentException("Damage Control L1 needs the hull's columns and stations.");
            Profile = p;
            int stations = p.Stations.Length;
            _stationFirstSpace = new int[stations];

            var spaces = new List<FloodSpace>();
            for (int s = 0; s < stations; s++)
            {
                _stationFirstSpace[s] = spaces.Count;
                HullStation st = p.Stations[s];
                spaces.Add(NewSpace($"{st.Name} hold", SpaceKind.Hold, s, st.HoldUse, p));
                spaces.Add(NewSpace($"{st.Name} upper", SpaceKind.Upper, s, SpaceUse.Accommodation, p));
                if (HasWings(s))
                {
                    spaces.Add(NewSpace($"{st.Name} wing P", SpaceKind.WingPort, s, SpaceUse.Void, p));
                    spaces.Add(NewSpace($"{st.Name} wing S", SpaceKind.WingStarboard, s, SpaceUse.Void, p));
                }
            }

            // Cut every column at the deck into the spaces it crosses. Columns beyond the end stations (the peaks) belong
            // to no space: they stay intact.
            var perSpace = new List<FloodSegment>[spaces.Count];
            for (int i = 0; i < perSpace.Length; i++) perSpace[i] = new List<FloodSegment>();
            float hull = 0f;
            foreach (HullColumn c in p.Columns)
            {
                hull += c.AreaM2 * (c.TopY - c.BottomY);
                int station = StationAt(c.Z);
                if (station < 0) continue;
                float centre = 0.5f * (c.BottomY + c.TopY);
                if (c.BottomY < p.DeckY)
                    perSpace[HoldSpace(station, c.X)].Add(new FloodSegment
                    {
                        X = c.X, Z = c.Z, AreaM2 = c.AreaM2, Y0 = c.BottomY, Y1 = Mathf.Min(c.TopY, p.DeckY), ColumnCentreY = centre,
                    });
                if (c.TopY > p.DeckY)
                    perSpace[_stationFirstSpace[station] + 1].Add(new FloodSegment
                    {
                        X = c.X, Z = c.Z, AreaM2 = c.AreaM2, Y0 = Mathf.Max(c.BottomY, p.DeckY), Y1 = c.TopY, ColumnCentreY = centre,
                    });
            }
            HullVolumeM3 = hull;

            var segments = new List<FloodSegment>();
            for (int i = 0; i < spaces.Count; i++)
            {
                FloodSpace s = spaces[i];
                s.FirstSegment = segments.Count;
                s.SegmentCount = perSpace[i].Count;
                float volume = 0f, bottom = float.MaxValue, top = float.MinValue;
                Vector3 moment = Vector3.zero;
                foreach (FloodSegment g in perSpace[i])
                {
                    float v = g.AreaM2 * (g.Y1 - g.Y0);
                    volume += v;
                    moment += v * new Vector3(g.X, 0.5f * (g.Y0 + g.Y1), g.Z);
                    bottom = Mathf.Min(bottom, g.Y0);
                    top = Mathf.Max(top, g.Y1);
                }
                s.CapacityM3 = s.Permeability * volume;
                s.FullCentroidShip = volume > 0f ? moment / volume : Vector3.zero;
                s.BottomY = s.SegmentCount > 0 ? bottom : 0f;
                s.TopY = s.SegmentCount > 0 ? top : 0f;
                spaces[i] = s;
                segments.AddRange(perSpace[i]);
            }
            Spaces = spaces.ToArray();
            Segments = segments.ToArray();
            Openings = PlanOpenings();
        }

        public int StationCount => Profile.Stations.Length;

        public bool HasWings(int station) => station >= Profile.WingFirstStation && station <= Profile.WingLastStation;

        /// <summary>The station whose bulkheads bound <paramref name="z"/>, or −1 beyond the end stations.</summary>
        public int StationAt(float z)
        {
            for (int s = 0; s < Profile.Stations.Length; s++)
                if (z >= Profile.Stations[s].AftZ && z < Profile.Stations[s].ForeZ) return s;
            return -1;
        }

        /// <summary>The station's hold, or its wing space at <paramref name="x"/>.</summary>
        public int HoldSpace(int station, float x)
        {
            int first = _stationFirstSpace[station];
            if (!HasWings(station) || Mathf.Abs(x) <= Profile.WingInboardX) return first;
            return first + (x > 0f ? 3 : 2);
        }

        public int UpperSpace(int station) => _stationFirstSpace[station] + 1;

        /// <summary>
        /// The space a ship-frame point lies in, or −1: beyond the end stations, or above the hull's deck there (the
        /// superstructure is not subdivided at L1).
        /// </summary>
        public int SpaceAt(Vector3 pointShip)
        {
            int station = StationAt(pointShip.z);
            if (station < 0) return -1;
            if (pointShip.y < Profile.DeckY) return HoldSpace(station, pointShip.x);
            int upper = UpperSpace(station);
            return Spaces[upper].SegmentCount > 0 && pointShip.y <= Spaces[upper].TopY ? upper : -1;
        }

        public int Find(string name)
        {
            for (int i = 0; i < Spaces.Length; i++) if (Spaces[i].Name == name) return i;
            return -1;
        }

        static FloodSpace NewSpace(string name, SpaceKind kind, int station, SpaceUse use, in FloodingProfileData p) => new FloodSpace
        {
            Name = name, Kind = kind, Station = station, Use = use, Permeability = PermeabilityOf(use, p),
        };

        static float PermeabilityOf(SpaceUse use, in FloodingProfileData p) => use switch
        {
            SpaceUse.Machinery => p.PermeabilityMachinery,
            SpaceUse.Accommodation => p.PermeabilityAccommodation,
            SpaceUse.Void => p.PermeabilityVoid,
            _ => p.PermeabilityStores,
        };

        FloodOpening[] PlanOpenings()
        {
            var openings = new List<FloodOpening>();
            for (int s = 0; s < StationCount; s++)
            {
                HullStation st = Profile.Stations[s];
                float mid = 0.5f * (st.AftZ + st.ForeZ);
                int hold = _stationFirstSpace[s], upper = hold + 1;
                bool hasUpper = Spaces[upper].SegmentCount > 0;

                // The hatch through the damage-control deck (the wings are tanks: no hatch).
                if (hasUpper && Spaces[hold].SegmentCount > 0)
                    openings.Add(new FloodOpening
                    {
                        Kind = OpeningKind.Hatch, SpaceA = hold, SpaceB = upper, PointShip = new Vector3(0f, Profile.DeckY, mid), AreaM2 = Profile.HatchAreaM2,
                    });
                if (!hasUpper) continue;

                // The door aft, through the bulkhead into the next station's upper space.
                if (s + 1 < StationCount && Spaces[_stationFirstSpace[s + 1] + 1].SegmentCount > 0)
                    openings.Add(new FloodOpening
                    {
                        Kind = OpeningKind.Door, SpaceA = upper, SpaceB = _stationFirstSpace[s + 1] + 1,
                        PointShip = new Vector3(0f, Profile.DeckY + Profile.DoorSillM, st.AftZ), AreaM2 = Profile.DoorAreaM2,
                    });

                // Weather openings at the deck edge, each side, mid-station.
                for (int side = -1; side <= 1; side += 2)
                    if (DeckEdge(upper, side, mid, out Vector3 edge))
                        openings.Add(new FloodOpening { Kind = OpeningKind.Weather, SpaceA = upper, SpaceB = -1, PointShip = edge, AreaM2 = Profile.WeatherAreaM2 });
            }
            return openings.ToArray();
        }

        /// <summary>The top of the outermost column on one side, nearest mid-station: the main deck edge.</summary>
        bool DeckEdge(int space, int side, float midZ, out Vector3 edge)
        {
            edge = default;
            FloodSpace s = Spaces[space];
            float bestZ = float.MaxValue;
            for (int i = s.FirstSegment; i < s.FirstSegment + s.SegmentCount; i++)
            {
                float dz = Mathf.Abs(Segments[i].Z - midZ);
                if (dz < bestZ - 1e-3f) bestZ = dz;
            }
            bool found = false;
            for (int i = s.FirstSegment; i < s.FirstSegment + s.SegmentCount; i++)
            {
                FloodSegment g = Segments[i];
                if (Mathf.Abs(Mathf.Abs(g.Z - midZ) - bestZ) > 1e-3f || g.X * side <= 0f) continue;
                if (!found || g.X * side > edge.x * side) { edge = new Vector3(g.X, g.Y1, g.Z); found = true; }
            }
            return found;
        }
    }
}
