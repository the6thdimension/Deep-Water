using System;

namespace DeepWater.AirWing.Core.Radar
{
    /// <summary>
    /// A jet's IFF (SENSORS-CONCEPT J5), baked from <c>AircraftProfileSO</c>, every number AUTHORED for a game: its
    /// interrogator (how often a press is honoured) and its transponder (how reliably it answers a friend's challenge).
    /// Which jets are friends is not here: it is each aircraft's transponder code (<see cref="Aircraft.IffCode"/>), set by
    /// whoever runs the fight. A jet answers only a challenge in its own code.
    /// </summary>
    public struct IffProfile
    {
        public bool IsSet;
        /// <summary>The chance this jet's transponder answers a challenge in its code, 0 … 1 (J5: "a small set chance of no answer").</summary>
        public float ReplyChance;
        /// <summary>The least time between two honoured presses, seconds: a held button asks once per interval.</summary>
        public float InterrogateIntervalS;

        public void Validate()
        {
            if (!(ReplyChance >= 0f && ReplyChance <= 1f)) throw new ArgumentException("IFF: reply chance 0 … 1");
            if (!(InterrogateIntervalS > 0f)) throw new ArgumentException("IFF: the interrogation interval must be positive");
        }
    }

    /// <summary>What IFF has said about a track (J5). IFF never says hostile: a track that does not answer stays unknown.</summary>
    public enum TrackIdentity : byte { Unknown = 0, Friend = 1 }

    /// <summary>
    /// A jet's radar warning receiver (J3), baked from <c>AircraftProfileSO</c>, AUTHORED: how long it keeps showing a radar
    /// after that radar last painted the jet. A searching radar paints once per sweep, so its hold must outlast the
    /// longest sweep; a track paints ten times a second, so its hold is short and a broken lock clears quickly.
    /// </summary>
    public struct WarningReceiverProfile
    {
        public bool IsSet;
        public float SearchHoldS, TrackHoldS;

        public void Validate()
        {
            if (!(SearchHoldS > 0f && TrackHoldS > 0f)) throw new ArgumentException("warning receiver: holds must be positive");
        }
    }
}
