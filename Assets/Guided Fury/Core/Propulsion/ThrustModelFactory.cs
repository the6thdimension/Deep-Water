namespace GuidedFury.Core.Propulsion
{
    public static class ThrustModelFactory
    {
        public static IThrustModel Create(ThrustModelKind kind)
        {
            switch (kind)
            {
                case ThrustModelKind.ConstantBoost: return ConstantBoostThrustModel.Instance;
                case ThrustModelKind.BoostSustain:  return BoostSustainThrustModel.Instance;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown ThrustModelKind");
            }
        }
    }
}
