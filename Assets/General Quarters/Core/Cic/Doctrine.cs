namespace DeepWater.GeneralQuarters.Core.Cic
{
    /// <summary>
    /// Doctrine and ROE as they stand right now (BLUEPRINT §3.4). Changed only by Orders. Each setting
    /// remembers the engagement-log event that set it, so an engagement can cite "doctrine Auto, ROE free"
    /// as a cause that leads back to the Order (or the scenario default).
    /// </summary>
    public sealed class DoctrineState
    {
        const int Classes = 8;   // WeaponClass values 0..7
        readonly DoctrineMode[] _mode = new DoctrineMode[Classes];
        readonly int[] _modeEvent = new int[Classes];

        public Roe Roe { get; private set; }
        public int RoeEvent { get; private set; } = -1;

        public DoctrineState()
        {
            for (int i = 0; i < Classes; i++) _modeEvent[i] = -1;
        }

        public DoctrineMode Mode(WeaponClass weapon) => _mode[(int)weapon];
        public int ModeEvent(WeaponClass weapon) => _modeEvent[(int)weapon];

        public void SetMode(WeaponClass weapon, DoctrineMode mode, int causeEvent)
        {
            _mode[(int)weapon] = mode;
            _modeEvent[(int)weapon] = causeEvent;
        }

        public void SetRoe(Roe roe, int causeEvent)
        {
            Roe = roe;
            RoeEvent = causeEvent;
        }

        /// <summary>
        /// Whether the ROE allows engaging a contact of this identity. Hold allows only self-defence:
        /// an inbound hostile inside a weapon's self-defence envelope.
        /// </summary>
        public bool RoePermits(Identity identity, bool selfDefence)
        {
            switch (Roe)
            {
                case Roe.WeaponsFree: return identity != Identity.Friend;
                case Roe.WeaponsTight: return identity == Identity.Hostile;
                default: return selfDefence && identity == Identity.Hostile;
            }
        }
    }
}
