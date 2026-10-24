using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Presentation
{
    /// <summary>Train and elevation that lay a barrel on a point. Pure, so it is tested on its own.</summary>
    public static class MountAim
    {
        /// <summary>
        /// Train (degrees from the mount's rest heading, positive to starboard) and elevation (degrees above the
        /// horizontal) that point a barrel at <paramref name="targetShip"/>, both in the ship frame (+Z bow, +X starboard).
        /// <paramref name="restYawDeg"/> is the bearing the barrel points at rest (the loadout's facing: 0 bow, 180 stern).
        /// </summary>
        public static void Solve(Vector3 pivotShip, float restYawDeg, Vector3 targetShip, out float trainDeg, out float elevationDeg)
        {
            Vector3 d = targetShip - pivotShip;
            float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            trainDeg = Mathf.DeltaAngle(restYawDeg, bearing);
            elevationDeg = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Move <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxStep"/> degrees, the short way,
        /// and keep the result in (−180, 180] so a mount that keeps turning never winds up.
        /// </summary>
        public static float Slew(float current, float target, float maxStep) =>
            Mathf.DeltaAngle(0f, current + Mathf.Clamp(Mathf.DeltaAngle(current, target), -maxStep, maxStep));
    }

    /// <summary>
    /// Lays the ship model's gun mounts on what they are firing at (G33): each burst the core logs names its mount and its
    /// aim point; the mount trains and elevates there at its rig's rates, holds while it keeps firing, then returns to
    /// rest. Presentation only: the core's guns have no slew at L0 (Guns L1), so a mount can fire before it is on.
    /// </summary>
    public sealed class MountAnimator
    {
        sealed class Mount
        {
            public MountRig Rig;
            public Transform Train, Elevate;
            public Quaternion TrainRest, ElevateRest;
            public Vector3 ElevateAxis;   // local axis that raises the barrel
            public float RestYawDeg;
            public float Train0, Elevation0;   // current, degrees
            public Vector3 Aim;                // world
            public float HoldUntil = float.NegativeInfinity;
        }

        readonly Transform _ship;
        readonly List<Mount> _byLoadoutIndex = new List<Mount>();

        /// <summary>Find each loadout mount's rig in <paramref name="ship"/>. Mounts with no rig (or missing parts) stay still.</summary>
        public MountAnimator(Transform ship, IReadOnlyList<IGunMount> guns, MountRig[] rigs)
        {
            _ship = ship;
            for (int i = 0; i < guns.Count; i++)
            {
                GunMountProfileData g = guns[i].Profile;
                Mount m = null;
                foreach (MountRig rig in rigs)
                {
                    if (rig.mountName != g.Name) continue;
                    Transform train = Find(ship, rig.trainTransform);
                    if (train == null) { Debug.LogWarning($"[MountAnimator] '{rig.trainTransform}' not found in {ship.name}; {g.Name} stays still."); break; }
                    Transform elevate = string.IsNullOrEmpty(rig.elevateTransform) ? null : Find(ship, rig.elevateTransform);
                    // At rest the barrel points along the mount's facing: raising it turns about the axis across that direction.
                    Vector3 restDir = Quaternion.Euler(0f, g.FacingDeg, 0f) * Vector3.forward;
                    m = new Mount
                    {
                        Rig = rig, Train = train, Elevate = elevate, TrainRest = train.localRotation,
                        ElevateRest = elevate != null ? elevate.localRotation : Quaternion.identity,
                        ElevateAxis = Vector3.Cross(restDir, Vector3.up).normalized, RestYawDeg = g.FacingDeg,
                    };
                    break;
                }
                _byLoadoutIndex.Add(m);
            }
        }

        /// <summary>A burst from loadout mount <paramref name="mountIndex"/> at <paramref name="aimWorld"/>.</summary>
        public void Engage(int mountIndex, Vector3 aimWorld, float now)
        {
            if (mountIndex < 0 || mountIndex >= _byLoadoutIndex.Count || _byLoadoutIndex[mountIndex] == null) return;
            Mount m = _byLoadoutIndex[mountIndex];
            m.Aim = aimWorld;
            m.HoldUntil = now + m.Rig.holdS;
        }

        /// <summary>The muzzle of loadout mount <paramref name="mountIndex"/> and the barrel's direction, world; false when it has no rig.</summary>
        public bool TryMuzzle(int mountIndex, out Vector3 muzzle, out Vector3 direction)
        {
            muzzle = direction = Vector3.zero;
            if (mountIndex < 0 || mountIndex >= _byLoadoutIndex.Count || _byLoadoutIndex[mountIndex] == null) return false;
            Mount m = _byLoadoutIndex[mountIndex];
            Transform barrel = m.Elevate != null ? m.Elevate : m.Train;
            direction = (_ship.rotation * BarrelShip(m)).normalized;
            muzzle = barrel.position + direction * m.Rig.barrelM;
            return true;
        }

        public void Update(float now, float dt)
        {
            for (int i = 0; i < _byLoadoutIndex.Count; i++)
            {
                Mount m = _byLoadoutIndex[i];
                if (m == null) continue;
                float train = 0f, elevation = 0f;
                if (now < m.HoldUntil)
                    MountAim.Solve(_ship.InverseTransformPoint(m.Train.position), m.RestYawDeg, _ship.InverseTransformPoint(m.Aim), out train, out elevation);
                m.Train0 = MountAim.Slew(m.Train0, train, m.Rig.trainRateDegS * dt);
                m.Elevation0 = Mathf.MoveTowards(m.Elevation0, Mathf.Clamp(elevation, -5f, 85f), m.Rig.elevateRateDegS * dt);
                m.Train.localRotation = m.TrainRest * Quaternion.Euler(0f, m.Train0, 0f);
                if (m.Elevate != null) m.Elevate.localRotation = m.ElevateRest * Quaternion.AngleAxis(m.Elevation0, m.ElevateAxis);
            }
        }

        static Vector3 BarrelShip(Mount m)
        {
            Vector3 rest = Quaternion.Euler(0f, m.RestYawDeg, 0f) * Vector3.forward;
            return Quaternion.Euler(0f, m.Train0, 0f) * (Quaternion.AngleAxis(m.Elevate != null ? m.Elevation0 : 0f, m.ElevateAxis) * rest);
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = Find(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
