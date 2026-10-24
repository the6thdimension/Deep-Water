using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Magazine;

namespace DeepWater.GeneralQuarters.Core
{
    /// <summary>A doctrine setting the scenario starts with.</summary>
    [System.Serializable]
    public struct DoctrineDefault
    {
        public WeaponClass Weapon;
        public DoctrineMode Mode;
    }

    /// <summary>
    /// Everything the combat core is built from: the seed, the tick, and for each element the requested
    /// fidelity level and its tuning. Plain data, baked from <c>ShipLoadoutSO</c> (P1) or built in code
    /// by tests. Baked once; live edits to the asset don't reach a running ship (C7).
    /// </summary>
    public sealed class CombatLoadout
    {
        public ulong Seed = 1;
        public float TickSeconds = 0.02f;

        public FidelityLevel RadarLevel;
        public RadarProfileData Radar;

        public FidelityLevel TracksLevel;
        public TrackProfileData Tracks;

        public FidelityLevel TewaLevel;
        public TewaProfileData Tewa;

        public FidelityLevel VlsLevel;
        public VlsProfileData Vls;

        public FidelityLevel GunsLevel;
        public GunMountProfileData[] Guns = new GunMountProfileData[0];

        public FidelityLevel DamageLevel;
        public DamageProfileData Damage;

        public Roe DefaultRoe = Roe.WeaponsTight;
        public DoctrineDefault[] DefaultDoctrine = new DoctrineDefault[0];
    }
}
