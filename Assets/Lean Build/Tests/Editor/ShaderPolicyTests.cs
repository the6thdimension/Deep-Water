using DeepWater.LeanBuild.Editor;
using NUnit.Framework;

namespace DeepWater.LeanBuild.Tests
{
    public class ShaderPolicyTests
    {
        static readonly string[] Names = { "Standard", "Standard (Specular setup)" };
        static readonly string[] Prefixes = { "Legacy Shaders/", "Mobile/" };
        const string Taa = "Hidden/HDRP/TemporalAA";

        static StripReason Decide(string shader, bool taaUsed, bool stripTaa = true) =>
            ShaderPolicy.Decide(shader, stripTaa, Taa, taaUsed, Names, Prefixes);

        [Test]
        public void TemporalAA_GoesOnlyWhenNoCameraUsesIt()
        {
            Assert.AreEqual(StripReason.TemporalAAUnused, Decide(Taa, taaUsed: false));
            Assert.AreEqual(StripReason.Keep, Decide(Taa, taaUsed: true), "a camera uses TAA: keep it");
            Assert.AreEqual(StripReason.Keep, Decide(Taa, taaUsed: false, stripTaa: false), "switched off in the settings: keep it");
        }

        [Test]
        public void BuiltInShaders_GoByExactNameOrPrefix()
        {
            Assert.AreEqual(StripReason.ByName, Decide("Standard", false));
            Assert.AreEqual(StripReason.ByName, Decide("Standard (Specular setup)", false));
            Assert.AreEqual(StripReason.ByPrefix, Decide("Legacy Shaders/Diffuse", false));
            Assert.AreEqual(StripReason.ByPrefix, Decide("Mobile/Unlit (Supports Lightmap)", false));
        }

        [Test]
        public void HdrpAndProjectShaders_AreKept()
        {
            foreach (string s in new[] { "HDRP/Lit", "HDRP/Water/Water", "Hidden/HDRP/FinalPass", "Shader Graphs/SolidColor", "StandardHDRP/Custom", "UI/Default", "" })
                Assert.AreEqual(StripReason.Keep, Decide(s, false), s);
        }
    }
}
