using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// Slice 1e: the rebuilt Dogfight (W12–W14) runs on Air Wing and the Sea State only, with nothing missing and
    /// nothing from the old jet. Read from the scene file, so no scene is opened.
    /// </summary>
    public class DogfightSceneTests
    {
        const string ScenePath = "Assets/_SCENES/Dogfight.unity";
        const string SceneGuid = "6981d1a2f44fd5f4e8766d22cb99db41";   // Dogfight's GUID before the rebuild (C24)
        const string JetPrefabPath = "Assets/Air Wing/Prefabs/FA18E_AirWing.prefab";

        static string SceneText() => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), ScenePath));

        [Test]
        public void Dogfight_KeptItsGuid()
        {
            Assert.AreEqual(SceneGuid, AssetDatabase.AssetPathToGUID(ScenePath), "the builder overwrites the scene in place");
        }

        [Test]
        public void Dogfight_HasOnlyAirWingSeaStateAndUnityScripts_NoneMissing()
        {
            var scripts = Regex.Matches(SceneText(), @"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})")
                .Cast<Match>().Select(m => m.Groups[1].Value)
                .Select(g => (guid: g, path: AssetDatabase.GUIDToAssetPath(g))).ToList();
            Assert.Greater(scripts.Count, 0);

            var missing = scripts.Where(s => string.IsNullOrEmpty(s.path) || AssetDatabase.LoadAssetAtPath<MonoScript>(s.path) == null).Select(s => s.guid).Distinct().ToArray();
            Assert.IsEmpty(missing, "missing scripts: " + string.Join(", ", missing));

            var foreign = scripts.Select(s => s.path).Where(p => p.StartsWith("Assets/") && !p.StartsWith("Assets/Air Wing/") && !p.StartsWith("Assets/Sea State/")).Distinct().ToArray();
            Assert.IsEmpty(foreign, "scripts from outside Air Wing and the Sea State: " + string.Join(", ", foreign));

            string[] Classes(string typeName) => scripts.Select(s => AssetDatabase.LoadAssetAtPath<MonoScript>(s.path)?.GetClass()?.Name).Where(n => n == typeName).ToArray();
            Assert.AreEqual(1, Classes("AirWingRunner").Length, "one runner");
            Assert.AreEqual(1, Classes("PlayerSeatInput").Length, "one player seat");
            Assert.AreEqual(1, Classes("GateCourseView").Length, "one gate course");
            Assert.AreEqual(1, Classes("SeaStateDirector").Length, "the Sea State sea");
            Assert.AreEqual(1, Classes("FlightHud").Length);
            Assert.AreEqual(1, Classes("ChaseCamera").Length);
            Assert.AreEqual(2, Classes("FlatGround").Length, "the land and the runway");
        }

        [Test]
        public void Dogfight_FliesTheAirWingJet()
        {
            string jetGuid = AssetDatabase.AssetPathToGUID(JetPrefabPath);
            Assert.IsNotEmpty(jetGuid, JetPrefabPath);
            int jets = Regex.Matches(SceneText(), @"m_SourcePrefab: \{fileID: 100100000, guid: " + jetGuid).Count;
            Assert.AreEqual(1, jets, "one Air Wing F/A-18E");
        }
    }
}
