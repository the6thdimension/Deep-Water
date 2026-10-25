using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;

namespace DeepWater.AirWing.Tests
{
    /// <summary>Slice 1d: the Air Wing jet carries none of the vendor kit (AP8, W30) and its moving parts resolve.</summary>
    public class PrefabTests
    {
        const string PrefabPath = "Assets/Air Wing/Prefabs/FA18E_AirWing.prefab";

        [Test]
        public void TheJet_HasOnlyAirWingScripts_AndNoRigidbody()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath + " (Deep Water → Air Wing → Build F-A-18E Prefab)");
            var foreign = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m => m == null || !m.GetType().Namespace.StartsWith("DeepWater.AirWing"))
                .Select(m => m == null ? "missing script" : m.GetType().FullName).ToArray();
            Assert.IsEmpty(foreign, "vendor or missing scripts on the Air Wing jet: " + string.Join(", ", foreign));
            Assert.IsEmpty(prefab.GetComponentsInChildren<Rigidbody>(true), "the core writes the motion; no Rigidbody (AP8)");
            Assert.AreEqual(1, prefab.GetComponentsInChildren<AircraftView>(true).Length);
        }

        [Test]
        public void EveryMovingPart_NamesATransformInTheModel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var view = prefab.GetComponent<AircraftView>();
            var visuals = (ScriptableObjects.AircraftVisualsSO)new SerializedObject(view).FindProperty("visuals").objectReferenceValue;
            Assert.IsNotNull(visuals);
            var names = prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToHashSet();
            var missing = visuals.surfaces.Where(s => !names.Contains(s.transformName)).Select(s => s.transformName).ToArray();
            Assert.IsEmpty(missing, "surfaces with no transform: " + string.Join(", ", missing));
            Assert.Greater(visuals.surfaces.Length, 0);
        }
    }
}
