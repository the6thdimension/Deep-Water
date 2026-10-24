using System.Collections.Generic;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The TAO's picking logic, pure so it is tested on its own: which tracks can be selected, stepping through
    /// them, and which track is most urgent.
    /// </summary>
    public static class TaoLogic
    {
        /// <summary>A track the TAO can select: live and not destroyed.</summary>
        public static bool Selectable(in Track t) => t.Active && !t.Destroyed;

        /// <summary>
        /// The track <paramref name="step"/> places from <paramref name="currentId"/> among selectable tracks in id order,
        /// wrapping around; the first (or last) one when nothing valid is selected. −1 when there are none.
        /// </summary>
        public static int Cycle(IReadOnlyList<Track> tracks, int currentId, int step)
        {
            int count = 0, at = -1;
            for (int i = 0; i < tracks.Count; i++)
            {
                if (!Selectable(tracks[i])) continue;
                if (tracks[i].Id == currentId) at = count;
                count++;
            }
            if (count == 0) return -1;
            int index = at < 0 ? (step >= 0 ? 0 : count - 1) : ((at + step) % count + count) % count;
            for (int i = 0, n = 0; i < tracks.Count; i++)
            {
                if (!Selectable(tracks[i])) continue;
                if (n++ == index) return tracks[i].Id;
            }
            return -1;
        }

        /// <summary>The inbound hostile track with the soonest closest approach, or −1.</summary>
        public static int MostUrgent(IReadOnlyList<Track> tracks, in OwnShipState own, long tick, float tickSeconds)
        {
            int best = -1;
            float bestTime = float.PositiveInfinity;
            for (int i = 0; i < tracks.Count; i++)
            {
                Track t = tracks[i];
                if (!Selectable(t) || t.Identity != Identity.Hostile) continue;
                ThreatGeometry g = ThreatGeometry.Of(t, own, tick, tickSeconds);
                if (!g.Inbound || g.TimeToCpaS >= bestTime) continue;
                bestTime = g.TimeToCpaS;
                best = t.Id;
            }
            return best;
        }
    }
}
