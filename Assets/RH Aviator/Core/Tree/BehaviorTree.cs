using System;
using System.Collections.Generic;

namespace DeepWater.Aviator.Core.Tree
{
    /// <summary>What a node says when it has run: it did its job, it did not, or it is still at it.</summary>
    public enum NodeStatus : byte { Failure = 0, Success = 1, Running = 2 }

    /// <summary>
    /// One node of a pilot's behavior tree (RH Aviator A27). A tree is built once, when its seat is made, and ticked on
    /// the pilot's watch (A34); ticking allocates nothing. Every node keeps what it said last and on which tick, so the
    /// Fight Tree window can light the branch the pilot is running (A28). Pure and deterministic, like the rest of the core.
    /// </summary>
    public abstract class Node
    {
        static readonly Node[] None = new Node[0];

        /// <summary>The node's name, as the window and the rule log say it.</summary>
        public readonly string Name;
        readonly Node[] _children;

        /// <summary>What it said the last time it ran.</summary>
        public NodeStatus Status { get; private set; }
        /// <summary>The tick it last ran on (<see cref="long.MinValue"/>: never). A node not run on its tree's last tick was skipped.</summary>
        public long LastTick { get; private set; } = long.MinValue;
        public IReadOnlyList<Node> Children => _children;

        protected Node(string name, Node[] children)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("a node needs a name", nameof(name));
            Name = name;
            _children = children ?? None;
            for (int i = 0; i < _children.Length; i++)
                if (_children[i] == null) throw new ArgumentNullException(nameof(children), $"{name}: child {i} is null");
        }

        public NodeStatus Tick(long tick)
        {
            Status = Run(tick);
            LastTick = tick;
            return Status;
        }

        protected abstract NodeStatus Run(long tick);
        protected Node Child(int i) => _children[i];
        protected int Count => _children.Length;
    }

    /// <summary>Tries its children in order and stops at the first that does not fail: "the first of these that applies".</summary>
    public sealed class Selector : Node
    {
        public Selector(string name, params Node[] children) : base(name, children) { }

        protected override NodeStatus Run(long tick)
        {
            for (int i = 0; i < Count; i++)
            {
                NodeStatus s = Child(i).Tick(tick);
                if (s != NodeStatus.Failure) return s;
            }
            return NodeStatus.Failure;
        }
    }

    /// <summary>Runs its children in order and stops at the first that does not succeed: "all of these, in turn".</summary>
    public sealed class Sequence : Node
    {
        public Sequence(string name, params Node[] children) : base(name, children) { }

        protected override NodeStatus Run(long tick)
        {
            for (int i = 0; i < Count; i++)
            {
                NodeStatus s = Child(i).Tick(tick);
                if (s != NodeStatus.Success) return s;
            }
            return NodeStatus.Success;
        }
    }

    /// <summary>A question: succeeds when it holds. It changes nothing.</summary>
    public sealed class Condition : Node
    {
        readonly Func<bool> _holds;
        public Condition(string name, Func<bool> holds) : base(name, null) => _holds = holds ?? throw new ArgumentNullException(nameof(holds));
        protected override NodeStatus Run(long tick) => _holds() ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <summary>A deed that always gets done: it acts and succeeds.</summary>
    public sealed class Act : Node
    {
        readonly Action _act;
        public Act(string name, Action act) : base(name, null) => _act = act ?? throw new ArgumentNullException(nameof(act));
        protected override NodeStatus Run(long tick) { _act(); return NodeStatus.Success; }
    }

    /// <summary>
    /// A deed that can end its branch: it acts and says whether the branch goes on (success) or stops here (failure),
    /// as a rule that returns early does.
    /// </summary>
    public sealed class Step : Node
    {
        readonly Func<bool> _act;
        public Step(string name, Func<bool> act) : base(name, null) => _act = act ?? throw new ArgumentNullException(nameof(act));
        protected override NodeStatus Run(long tick) => _act() ? NodeStatus.Success : NodeStatus.Failure;
    }
}
