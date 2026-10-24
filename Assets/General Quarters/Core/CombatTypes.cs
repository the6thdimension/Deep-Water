namespace DeepWater.GeneralQuarters.Core
{
    /// <summary>The weapon classes the CIC can assign. Values are stable: they go into the Fix.</summary>
    public enum WeaponClass : byte
    {
        None = 0,
        Sm2 = 1,
        Essm = 2,
        Mk45 = 3,
        Ciws = 4,
        Mk38 = 5,
        Harpoon = 6,
        Tomahawk = 7,
    }

    /// <summary>
    /// How much a weapon class may do on its own (BLUEPRINT §3.4).
    /// Auto: TEWA fires. SemiAuto: TEWA proposes, the TAO confirms with an EngageOrder. Manual: only Orders fire.
    /// </summary>
    public enum DoctrineMode : byte
    {
        Manual = 0,
        SemiAuto = 1,
        Auto = 2,
    }

    /// <summary>
    /// Rules of engagement. Free: anything not identified friendly. Tight: only tracks identified hostile.
    /// Hold: only self-defence (inbound threats inside a weapon's self-defence envelope) or a direct Order.
    /// </summary>
    public enum Roe : byte
    {
        WeaponsHold = 0,
        WeaponsTight = 1,
        WeaponsFree = 2,
    }

    /// <summary>What a contact physically is. Comes from the truth feed; L0 tracks see it perfectly.</summary>
    public enum TargetKind : byte
    {
        Unknown = 0,
        Aircraft = 1,
        AntiShipMissile = 2,
        SurfaceShip = 3,
    }

    /// <summary>Identification. L0 tracks inherit it from truth; identification errors are a higher Tracks level.</summary>
    public enum Identity : byte
    {
        Unknown = 0,
        Hostile = 1,
        Friend = 2,
        Neutral = 3,
    }
}
