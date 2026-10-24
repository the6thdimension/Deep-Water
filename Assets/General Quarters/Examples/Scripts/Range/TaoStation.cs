using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Modules.Combat;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The TAO's station: the selected track and the TAO's orders (engage, cease fire, doctrine, ROE). Keys, pad
    /// buttons and the TAO screen all come through here, so they give the same Orders, issued as the TAO.
    /// </summary>
    public sealed class TaoStation
    {
        readonly ShipCombat _combat;

        public TaoStation(ShipCombat combat) => _combat = combat;

        /// <summary>The selected track, or −1.</summary>
        public int SelectedTrackId { get; private set; } = -1;

        public bool TryGetSelected(out Track track)
        {
            track = default;
            CombatSystem c = _combat.Combat;
            return c != null && SelectedTrackId >= 0 && c.Tracks.TryGet(SelectedTrackId, out track) && TaoLogic.Selectable(track);
        }

        public void Select(int trackId) => SelectedTrackId = trackId;

        public void Cycle(int step)
        {
            if (_combat.Combat != null) SelectedTrackId = TaoLogic.Cycle(_combat.Combat.Tracks.Tracks, SelectedTrackId, step);
        }

        /// <summary>Keep a valid selection: when the selected track is gone, take the most urgent threat.</summary>
        public void Refresh()
        {
            CombatSystem c = _combat.Combat;
            if (c == null) { SelectedTrackId = -1; return; }
            if (TryGetSelected(out _)) return;
            SelectedTrackId = TaoLogic.MostUrgent(c.Tracks.Tracks, _combat.Own, c.Tick, c.TickSeconds);
        }

        /// <summary>Engage the selected track: one missile per order, or guns until cease fire.</summary>
        public bool Engage(WeaponClass weapon)
        {
            if (!TryGetSelected(out Track t)) return false;
            bool missile = weapon == WeaponClass.Essm || weapon == WeaponClass.Sm2;
            _combat.Issue(Order.Engage(OrderIssuer.Tao, t.Id, weapon, missile ? 1 : 0));
            return true;
        }

        public bool CeaseFire()
        {
            if (!TryGetSelected(out Track t)) return false;
            _combat.Issue(Order.CeaseFire(OrderIssuer.Tao, t.Id));
            return true;
        }

        public void CeaseFireAll() => _combat.Issue(Order.CeaseFire(OrderIssuer.Tao));

        public void SetDoctrine(WeaponClass weapon, DoctrineMode mode) => _combat.Issue(Order.SetDoctrine(OrderIssuer.Tao, weapon, mode));

        /// <summary>ROE is the Captain's to set, and the TAO's (BLUEPRINT §3.3).</summary>
        public void SetRoe(Roe roe, OrderIssuer issuer) => _combat.Issue(Order.SetRoe(issuer, roe));

        /// <summary>Free → Tight → Hold → Free.</summary>
        public static Roe Next(Roe roe) => roe == Roe.WeaponsFree ? Roe.WeaponsTight : roe == Roe.WeaponsTight ? Roe.WeaponsHold : Roe.WeaponsFree;
    }
}
