using System.Collections.Generic;

namespace DeepWater.GeneralQuarters.Core.Orders
{
    /// <summary>
    /// The append-only record of every Order, and the queue of Orders waiting to take effect.
    /// Replaying the same Orders at the same ticks with the same seed reproduces the run exactly; that is
    /// what makes save (snapshot + log), replay and multiplayer possible later (Reckoning R19).
    /// </summary>
    public sealed class OrderLog
    {
        readonly List<Order> _all = new List<Order>();
        readonly List<Order> _pending = new List<Order>();

        /// <summary>Every Order ever issued, in issue order.</summary>
        public IReadOnlyList<Order> All => _all;

        public int PendingCount => _pending.Count;

        /// <summary>
        /// Record an Order to take effect on <paramref name="effectiveTick"/> (the next tick the combat
        /// system will step). Returns the stamped Order.
        /// </summary>
        public Order Issue(Order order, long effectiveTick)
        {
            order.Tick = effectiveTick;
            order.Sequence = _all.Count;
            _all.Add(order);
            _pending.Add(order);
            return order;
        }

        /// <summary>
        /// Move every pending Order due on or before <paramref name="tick"/> into <paramref name="into"/>,
        /// in issue order (sequence order is the tie-break that keeps two runs identical).
        /// </summary>
        public void Drain(long tick, List<Order> into)
        {
            int kept = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                Order o = _pending[i];
                if (o.Tick <= tick) into.Add(o);
                else _pending[kept++] = o;
            }
            _pending.RemoveRange(kept, _pending.Count - kept);
        }
    }
}
