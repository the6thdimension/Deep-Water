using DeepWater.LeanBuild.Editor;
using NUnit.Framework;
using UnityEditor;

namespace DeepWater.LeanBuild.Tests
{
    public class BuildPlanTests
    {
        static bool CodeOnly(params string[] paths) => ChangeSet.IsCodeOnly(paths, out _);

        [Test]
        public void ScriptsAsmdefsAndTheirMetas_AreCode()
        {
            Assert.IsTrue(CodeOnly("Assets/General Quarters/Examples/Scripts/Range/RangeDirector.cs"));
            Assert.IsTrue(CodeOnly("Assets/Obi/Scripts/Obi.asmdef", "Assets/Obi/Scripts/Obi.asmdef.meta", "Assets/A/B.cs.meta", "Assets/X/Y.asmref"));
            Assert.IsTrue(CodeOnly(), "nothing changed: a scripts-only build gives the same player");
        }

        [Test]
        public void AnythingElse_IsNotCode_AndIsNamed()
        {
            Assert.IsFalse(ChangeSet.IsCodeOnly(new[] { "Assets/A.cs", "Assets/_SCENES/Dogfight.unity", "Assets/B.cs" }, out string first));
            Assert.AreEqual("Assets/_SCENES/Dogfight.unity", first);
            foreach (string p in new[] { "Assets/T.png.meta", "ProjectSettings/ProjectSettings.asset", "Packages/manifest.json", "Assets/New Folder.meta", "Assets/Shader.hlsl" })
                Assert.IsFalse(CodeOnly(p), p);
        }

        [Test]
        public void ScriptsOnly_NeedsEveryCondition()
        {
            var ok = BuildPlan.For(true, true, previousBuildExists: true, changesKnown: true, onlyCodeChanged: true, null);
            Assert.AreEqual(BuildOptions.Development | BuildOptions.BuildScriptsOnly, ok.Options);
            Assert.IsTrue(ok.ScriptsOnly);

            Assert.AreEqual(BuildOptions.None, BuildPlan.For(false, false, true, true, true, null).Options, "Release: a full, non-development build");
            Assert.AreEqual(BuildOptions.Development, BuildPlan.For(true, false, true, true, true, null).Options);
            StringAssert.Contains("development", BuildPlan.For(false, true, true, true, true, null).Why);
            StringAssert.Contains("no earlier build", BuildPlan.For(true, true, false, true, true, null).Why);
            StringAssert.Contains("unknown", BuildPlan.For(true, true, true, false, true, null).Why);
            var texture = BuildPlan.For(true, true, true, true, false, "Assets/T.png");
            Assert.IsFalse(texture.ScriptsOnly);
            StringAssert.Contains("Assets/T.png", texture.Why);
        }
    }
}
