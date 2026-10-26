using System.Collections.Generic;

namespace DeepWater.FullSpeed.Editor
{
    /// <summary>One row of a process snapshot: its id, its parent's id and its executable name.</summary>
    public readonly struct ProcessEntry
    {
        public readonly int Id;
        public readonly int ParentId;
        public readonly string Name;

        public ProcessEntry(int id, int parentId, string name)
        {
            Id = id;
            ParentId = parentId;
            Name = name;
        }
    }

    /// <summary>Pure tree logic over a process snapshot, kept apart from Win32 so tests can reach it.</summary>
    public static class ProcessTree
    {
        /// <summary>
        /// The root and every process descended from it. Windows reuses process ids, so a parent id can point at
        /// an unrelated newer process. Each id is visited once, so a cycle or a reused id cannot loop.
        /// </summary>
        public static HashSet<int> WithDescendants(int root, IReadOnlyList<ProcessEntry> snapshot)
        {
            var children = new Dictionary<int, List<int>>();
            foreach (ProcessEntry e in snapshot)
            {
                if (e.Id == e.ParentId) continue;   // the idle process names itself as its parent
                if (!children.TryGetValue(e.ParentId, out List<int> list)) children[e.ParentId] = list = new List<int>();
                list.Add(e.Id);
            }

            var tree = new HashSet<int> { root };
            var open = new Stack<int>();
            open.Push(root);
            while (open.Count > 0)
            {
                if (!children.TryGetValue(open.Pop(), out List<int> kids)) continue;
                foreach (int k in kids)
                    if (tree.Add(k)) open.Push(k);
            }
            return tree;
        }
    }
}
