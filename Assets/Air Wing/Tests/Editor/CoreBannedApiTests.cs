using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// The flight core stays deterministic (W7, BLUEPRINT §6 constraint 1). This scans the SOURCE of
    /// <c>Assets/Air Wing/Core</c> for the APIs that break determinism, with comments and string literals stripped
    /// first. A source scan, not an IL check: cheap, readable, and enough until Reckoning's analyzer guard (R36)
    /// exists. Copied from General Quarters' scan, not referenced (W31).
    /// </summary>
    public class CoreBannedApiTests
    {
        static readonly (string pattern, string why)[] Banned =
        {
            (@"\bUnityEngine\.Time\b|(?<![\w.])Time\.(time|deltaTime|fixedTime|fixedDeltaTime|timeAsDouble|realtimeSinceStartup|frameCount|unscaledTime)\b", "Unity's clock is not the flight tick"),
            (@"\bUnityEngine\.Random\b|(?<![\w.])Random\.(value|Range|insideUnitSphere|onUnitSphere|InitState)\b", "global RNG; use DeterministicRng"),
            (@"new\s+(System\.)?Random\s*\(", "System.Random's algorithm is not guaranteed across runtimes; use DeterministicRng"),
            (@"(?<![\w.])Physics\.", "PhysX answers from scene state; contacts reach the core through the truth feed"),
            (@"\bFind(First|Any)?Object(s)?(Of|By)Type\b|\bGameObject\.Find\b", "scene search order is not deterministic"),
            (@"\bGameObject\b|\bMonoBehaviour\b|\bTransform\b|\bRigidbody\b", "the core holds no scene objects (P6)"),
            (@"\bDateTime\.(Now|UtcNow)\b|\bEnvironment\.TickCount\b|\bStopwatch\b", "wall-clock time"),
            (@"\bParallel\.|\bTask\.Run\b|\bThreadPool\b", "thread-timing-dependent results"),
        };

        [Test]
        public void FlightCore_UsesNoBannedApis()
        {
            string core = Path.Combine(Application.dataPath, "Air Wing", "Core");
            Assert.IsTrue(Directory.Exists(core), core);
            var report = new StringBuilder();
            int files = 0;
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories).OrderBy(f => f))
            {
                files++;
                string code = StripCommentsAndStrings(File.ReadAllText(file));
                foreach (var (pattern, why) in Banned)
                    foreach (Match m in Regex.Matches(code, pattern))
                    {
                        int line = code.Take(m.Index).Count(c => c == '\n') + 1;
                        report.AppendLine($"{Path.GetFileName(file)}:{line}: '{m.Value}' ({why})");
                    }
            }
            Assert.Greater(files, 0, "no core files found");
            Assert.IsEmpty(report.ToString(), "banned APIs in the flight core:\n" + report);
        }

        [Test]
        public void Scanner_CatchesWhatItShould_AndIgnoresComments()
        {
            string code = StripCommentsAndStrings("float t = Time.time; // Random.value\n/* Physics.Raycast */ var s = \"GameObject\";");
            Assert.IsTrue(Regex.IsMatch(code, Banned[0].pattern), "Time.time must be caught");
            Assert.IsFalse(Regex.IsMatch(code, Banned[1].pattern), "a comment must not trip the scan");
            Assert.IsFalse(Regex.IsMatch(code, Banned[3].pattern), "a block comment must not trip the scan");
            Assert.IsFalse(Regex.IsMatch(code, Banned[5].pattern), "a string literal must not trip the scan");
        }

        /// <summary>W31: Air Wing copies General Quarters' patterns; it never references the module.</summary>
        [Test]
        public void AirWing_DoesNotReferenceGeneralQuarters()
        {
            string asmdef = File.ReadAllText(Path.Combine(Application.dataPath, "Air Wing", "DeepWater.AirWing.asmdef"));
            StringAssert.DoesNotContain("GeneralQuarters", asmdef);
        }

        /// <summary>Blank out comments and string/char literals, keeping newlines so line numbers survive.</summary>
        static string StripCommentsAndStrings(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    while (i < s.Length && s[i] != '\n') i++;
                    if (i < s.Length) sb.Append('\n');
                }
                else if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) { if (s[i] == '\n') sb.Append('\n'); i++; }
                    i++;
                }
                else if (c == '"' || c == '\'')
                {
                    char q = c;
                    i++;
                    while (i < s.Length && s[i] != q) { if (s[i] == '\\') i++; else if (s[i] == '\n') sb.Append('\n'); i++; }
                    sb.Append(' ');
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
