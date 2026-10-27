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

        /// <summary>Runs <paramref name="check"/> on a copy of the jet in a preview scene (the open scene stays clean).</summary>
        static void WithJet(System.Action<GameObject, ScriptableObjects.AircraftVisualsSO> check)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var visuals = (ScriptableObjects.AircraftVisualsSO)new SerializedObject(prefab.GetComponent<AircraftView>()).FindProperty("visuals").objectReferenceValue;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var jet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            try { check(jet, visuals); }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheGearRig_FindsEveryPart_AndGearDownIsTheRestPose()
        {
            WithJet((jet, visuals) =>
            {
                Transform model = jet.transform.Find("Model");
                Assert.Greater(visuals.gearParts.Length, 0, "Deep Water → Air Wing → Apply HARV L1 Data to F-A-18E");
                var all = model.GetComponentsInChildren<Transform>(true);
                var rest = all.ToDictionary(t => t, t => t.localRotation);
                var rig = new GearRig(model, model, visuals.gearParts);
                Assert.AreEqual(visuals.gearParts.Length, rig.PartsFound, "every gear part names a transform in the model");
                rig.Pose(0f);
                rig.Pose(1f);
                foreach (Transform t in all)
                    Assert.Less(Quaternion.Angle(rest[t], t.localRotation), 1e-3f, t.name + ": gear down is the model's rest pose");
            });
        }

        [Test]
        public void GearUp_LeavesNothingHangingBelowTheClosedDoors()
        {
            WithJet((jet, visuals) =>
            {
                Transform model = jet.transform.Find("Model");
                var rig = new GearRig(model, model, visuals.gearParts);
                var gearRoots = visuals.gearParts.Select(g => model.GetComponentsInChildren<Transform>(true).First(t => t.name == g.transformName)).ToArray();
                float LowestGear() => gearRoots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).Where(r => r.enabled).Min(r => r.bounds.min.y);
                float down = LowestGear();
                rig.Pose(0.5f);
                float half = LowestGear();
                rig.Pose(0f);
                float up = LowestGear();
                Debug.Log($"[AIRWING GEAR] model gear, lowest visible point: down {down:F2} m, half way {half:F2} m, up {up:F2} m (model frame)");
                Assert.AreEqual(-1.71f, down, 0.02f, "down, the tyres reach the gear's contact height");
                // The lowest closed door is the right inner one, whose thicker mesh reaches 0.86 m below the model's origin.
                Assert.Greater(up, -0.9f, "up, only the closed doors show, at the belly");
                Assert.That(half, Is.GreaterThan(down + 0.1f).And.LessThan(up), "half way, the legs are on their way up, below the stowed doors");
            });
        }
    }
}
