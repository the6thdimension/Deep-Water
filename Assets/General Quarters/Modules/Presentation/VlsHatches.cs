using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Presentation
{
    /// <summary>
    /// VLS cell hatches (G33). The core launches from a VLS group (L0 has no cells or hatch timing; that is VLS L1);
    /// here each launch takes the group's next cell in turn and its hatch swings open, holds, and closes. The hatch is a
    /// thin lid hinged on one edge, shown only while it moves, so the deck's own cell pattern shows when shut.
    /// </summary>
    public sealed class VlsHatches
    {
        sealed class Hatch
        {
            public Transform Hinge;
            public float Start;
        }

        readonly CombatPresentationSO _p;
        readonly List<Transform>[] _cells;
        readonly int[] _next;
        readonly List<Hatch> _moving = new List<Hatch>();
        readonly Stack<Transform> _free = new Stack<Transform>();
        readonly Transform _root;

        public VlsHatches(Transform ship, CombatPresentationSO p, Transform root)
        {
            _p = p;
            _root = root;
            _cells = new List<Transform>[p.vlsCellPrefixes.Length];
            _next = new int[_cells.Length];
            var all = ship.GetComponentsInChildren<Transform>(true);
            for (int g = 0; g < _cells.Length; g++)
            {
                _cells[g] = new List<Transform>();
                foreach (Transform t in all) if (t.name.StartsWith(p.vlsCellPrefixes[g])) _cells[g].Add(t);
                _cells[g].Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                if (_cells[g].Count == 0) Debug.LogWarning($"[VlsHatches] no cells named '{p.vlsCellPrefixes[g]}*' in {ship.name}; launches from group {g} show no hatch.");
            }
        }

        /// <summary>Open the next cell of <paramref name="group"/>; returns the cell (where the launch plume goes) or null.</summary>
        public Transform Launch(int group, float now)
        {
            if (group < 0 || group >= _cells.Length || _cells[group].Count == 0) return null;
            Transform cell = _cells[group][_next[group]];
            _next[group] = (_next[group] + 1) % _cells[group].Count;

            Transform hinge = _free.Count > 0 ? _free.Pop() : NewHatch();
            // Hinged on the cell's outboard edge, so the lid swings away from the centreline.
            float side = cell.localPosition.x >= 0f ? 1f : -1f;
            hinge.SetParent(cell, false);
            hinge.localPosition = new Vector3(side * 0.5f * _p.hatchSize.x, _p.hatchSize.y * 0.5f, 0f);
            hinge.localRotation = Quaternion.identity;
            hinge.GetChild(0).localPosition = new Vector3(-side * 0.5f * _p.hatchSize.x, 0f, 0f);
            hinge.gameObject.SetActive(true);
            _moving.Add(new Hatch { Hinge = hinge, Start = now });
            return cell;
        }

        public void Update(float now)
        {
            float open = _p.hatchOpenS, hold = _p.hatchHoldS, close = _p.hatchCloseS;
            for (int i = _moving.Count - 1; i >= 0; i--)
            {
                Hatch h = _moving[i];
                float t = now - h.Start;
                float k = t < open ? t / open : t < open + hold ? 1f : 1f - (t - open - hold) / Mathf.Max(close, 1e-3f);
                if (t >= open + hold + close)
                {
                    h.Hinge.gameObject.SetActive(false);
                    h.Hinge.SetParent(_root, false);
                    _free.Push(h.Hinge);
                    _moving.RemoveAt(i);
                    continue;
                }
                float side = h.Hinge.localPosition.x >= 0f ? 1f : -1f;
                // The lid lies inboard of its hinge; turning about the ship's fore-and-aft axis by −side lifts it up and outboard.
                h.Hinge.localRotation = Quaternion.Euler(0f, 0f, -side * _p.hatchOpenDeg * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k)));
            }
        }

        public void Clear()
        {
            foreach (Hatch h in _moving) { h.Hinge.gameObject.SetActive(false); h.Hinge.SetParent(_root, false); _free.Push(h.Hinge); }
            _moving.Clear();
            for (int g = 0; g < _next.Length; g++) _next[g] = 0;
        }

        Transform NewHatch()
        {
            var hinge = new GameObject("VLS hatch").transform;
            hinge.SetParent(_root, false);
            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(lid.GetComponent<Collider>());
            lid.name = "Lid";
            lid.transform.SetParent(hinge, false);
            lid.transform.localScale = _p.hatchSize;
            if (_p.hatchMaterial != null) lid.GetComponent<MeshRenderer>().sharedMaterial = _p.hatchMaterial;
            hinge.gameObject.SetActive(false);
            return hinge;
        }
    }
}
