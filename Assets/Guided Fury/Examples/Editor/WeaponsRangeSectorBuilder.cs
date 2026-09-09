#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using GuidedFury.Core.Integrators;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Examples.Editor
{
    /// <summary>
    /// Additive sector builder that grows the MissileRange into a full multi-weapon test
    /// range (2026-09-08 range expansion, user-selected options: Unity Terrain, both air
    /// platforms, naval sector, extend-in-place).
    ///
    /// Sectors (all rebuilt idempotently — each root object is deleted and recreated):
    /// - RangeTerrain        — programmatic Unity Terrain: flat launch/impact corridor,
    ///                         western ridgeline (terrain masking), 460 m mesa (elevated /
    ///                         A2G launches), rolling far hills, eastern naval basin.
    /// - GroundImpactSector  — bunkers, rigidbody vehicle column (blast physics), radar
    ///                         site, building cluster, scoring rings around the aimpoint.
    /// - AerialCorridor      — waypoint racetrack drones at 300 m and 800 m.
    /// - VlsLauncher         — vertical-launch cell pad next to the TEL.
    /// - DemolitionPad       — emplaced charges ([B] detonates) + blast props.
    /// - MesaGantry          — fixed elevated launcher on the mesa aimed at the ground
    ///                         sector (deterministic air-to-ground geometry).
    /// - AirborneTestbed     — aircraft flying a racetrack at 1200 m with a wing pylon
    ///                         runner (air-to-air / air-to-ground shots).
    /// - NavalSector         — water basin + two anchored ship hulk targets (future
    ///                         Harpoon / sea-skimmer work).
    ///
    /// Runs additively on the OPEN scene ("Build Weapons Range Sectors" menu) so manual
    /// scene tuning is preserved; also invoked from MissileRangeSceneBuilder.PopulateScene
    /// so a full from-scratch rebuild produces the complete range.
    /// </summary>
    public static class WeaponsRangeSectorBuilder
    {
        private const string TerrainFolder    = "Assets/Guided Fury/Examples/Terrain";
        private const string TerrainDataPath  = TerrainFolder + "/MissileRangeTerrain.asset";
        private const string TerrainLayerPath = TerrainFolder + "/MissileRangeTerrainLayer.terrainlayer";
        private const string TerrainTexPath   = TerrainFolder + "/MissileRangeTerrainTex.asset";
        private const string EssmProfilePath  = "Assets/Guided Fury/Examples/Profiles/RIM-162_ESSM.asset";
        private const string EssmPrefabPath   = "Assets/rim-162essm/ESSM Shell.prefab";

        // Terrain frame: 20 km x 20 km, land surface at world y = 0 (base height 40 over a
        // -40 origin so the naval basin can dip below the waterline).
        private const float TerrainSize   = 20000f;
        private const float TerrainHeight = 600f;
        private static readonly Vector3 TerrainOrigin = new Vector3(-8000f, -40f, -2000f);
        private const float LandH = 40f;
        private const int HeightmapRes = 1025;

        [MenuItem("Guided Fury/Build Weapons Range Sectors (additive)")]
        public static void BuildAdditive()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(EssmProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[GuidedFury] RIM-162 profile not found at {EssmProfilePath} — " +
                               "run Guided Fury > Authored Missiles > Build RIM-162 ESSM Profile first.");
                return;
            }
            BuildSectors(profile);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[GuidedFury] Weapons-range sectors built. [B] demo charge; VLS / Mesa / Testbed fire buttons are on the control panel.");
        }

        /// <summary>Build (or rebuild) every sector. Idempotent per root object.</summary>
        public static void BuildSectors(MissileProfileSO profile)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EssmPrefabPath);

            BuildTerrain();
            BuildGroundImpactSector();
            BuildAerialCorridor();
            BuildNavalSector();
            BuildVlsPad(profile, prefab);
            BuildDemolitionPad(profile);
            BuildMesaGantry(profile, prefab);
            BuildAirborneTestbed(profile, prefab);
            BuildExtendedMarkers();
        }

        // ===================================================================
        // Terrain
        // ===================================================================

        private static void BuildTerrain()
        {
            Directory.CreateDirectory(TerrainFolder);

            // Reuse the existing TerrainData asset when present (stable GUID, scene
            // reference survives rebuilds); create it once otherwise.
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = HeightmapRes;
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

            var heights = new float[HeightmapRes, HeightmapRes];
            for (int iz = 0; iz < HeightmapRes; iz++)
            {
                float wz = TerrainOrigin.z + (iz / (float)(HeightmapRes - 1)) * TerrainSize;
                for (int ix = 0; ix < HeightmapRes; ix++)
                {
                    float wx = TerrainOrigin.x + (ix / (float)(HeightmapRes - 1)) * TerrainSize;
                    heights[iz, ix] = Mathf.Clamp01(SampleHeightM(wx, wz) / TerrainHeight);
                }
            }
            data.SetHeights(0, 0, heights);

            // Single grass-green layer so HDRP has something to render.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexPath);
            if (tex == null)
            {
                tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var c = new Color(0.34f, 0.44f, 0.29f);
                var px = new Color[64];
                for (int i = 0; i < 64; i++) px[i] = c;
                tex.SetPixels(px); tex.Apply();
                AssetDatabase.CreateAsset(tex, TerrainTexPath);
            }
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainLayerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, TerrainLayerPath);
            }
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(60f, 60f);
            data.terrainLayers = new[] { layer };

            var old = GameObject.Find("RangeTerrain");
            if (old != null) Object.DestroyImmediate(old);
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "RangeTerrain";
            terrainGo.transform.position = TerrainOrigin;
            var terrain = terrainGo.GetComponent<Terrain>();
            var hdrpTerrainShader = Shader.Find("HDRP/TerrainLit");
            if (hdrpTerrainShader != null)
                terrain.materialTemplate = new Material(hdrpTerrainShader);

            // The terrain replaces the old flat Ground plane (which would z-fight at y=0).
            var ground = GameObject.Find("Ground");
            if (ground != null) Object.DestroyImmediate(ground);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Deterministic world-space terrain height in meters above the terrain origin
        /// (land surface = LandH = world y 0). Pure math + Perlin (fixed offsets), so the
        /// terrain regenerates identically on every rebuild.
        /// </summary>
        private static float SampleHeightM(float wx, float wz)
        {
            float h = LandH;

            // Western ridgeline: terrain-masking wall along x = -2000, z 500..5000.
            float ridge = Mathf.Exp(-Sq((wx + 2000f) / 320f));
            float ridgeZ = SmoothWindow(wz, 500f, 5000f, 600f);
            h += 170f * ridge * ridgeZ;

            // Mesa at (3000, 2500): flat 460 m top for the elevated gantry.
            float mesaDist = Mathf.Sqrt(Sq(wx - 3000f) + Sq(wz - 2500f));
            float mesa = 1f - Mathf.Clamp01((mesaDist - 450f) / 300f); // 1 inside top, 0 past base
            mesa = mesa * mesa * (3f - 2f * mesa);                     // smoothstep rim
            h += 460f * mesa;

            // Rolling far hills beyond z = 7000.
            float hillRamp = Mathf.Clamp01((wz - 7000f) / 1500f);
            if (hillRamp > 0f)
                h += hillRamp * 240f * Mathf.PerlinNoise(wx * 0.00042f + 11.7f, wz * 0.00042f + 3.9f);

            // Eastern naval basin: drop to the sea floor (height 0 = world -40) past x = 6200.
            float basin = Mathf.Clamp01((wx - 6200f) / 800f);
            basin = basin * basin * (3f - 2f * basin);
            h = Mathf.Lerp(h, 0f, basin);

            // Launch + impact corridor stays flat: blend back to LandH near the centerline
            // (mesa and basin excluded by geometry; ridge sits outside the corridor).
            float corridor = (1f - Mathf.Clamp01((Mathf.Abs(wx) - 1000f) / 400f))
                           * SmoothWindow(wz, -1500f, 6800f, 400f);
            if (corridor > 0f && wx < 6200f)
                h = Mathf.Lerp(h, LandH, corridor);

            return h;
        }

        private static float Sq(float v) => v * v;

        /// <summary>1 inside [a,b], smooth falloff of width w outside.</summary>
        private static float SmoothWindow(float v, float a, float b, float w)
        {
            float rise = Mathf.Clamp01((v - (a - w)) / w);
            float fall = Mathf.Clamp01(((b + w) - v) / w);
            return Mathf.Min(rise, fall);
        }

        // ===================================================================
        // Ground impact sector (z 2000..3400)
        // ===================================================================

        private static void BuildGroundImpactSector()
        {
            var root = Rebuild("GroundImpactSector");

            // Scoring rings around the primary aimpoint (no colliders — they must not
            // trip proximity fuzes; they are paint, not structure).
            Vector3 aim = new Vector3(0f, 0f, 2500f);
            BuildScoringRing(root, "Ring_50m", aim, 50f, 0.06f, new Color(0.9f, 0.85f, 0.2f));
            BuildScoringRing(root, "Ring_25m", aim, 25f, 0.09f, new Color(0.9f, 0.5f, 0.15f));
            BuildScoringRing(root, "Ring_10m", aim, 10f, 0.12f, new Color(0.85f, 0.2f, 0.15f));

            // Hardened bunkers (kinematic — they absorb hits, they don't fly).
            for (int i = 0; i < 3; i++)
                BuildStaticTarget(root, $"Bunker_{i}", new Vector3(-160f + 160f * i, 2f, 2000f),
                                  new Vector3(9f, 4f, 9f), new Color(0.45f, 0.45f, 0.42f));

            // Vehicle column: dynamic rigidbodies — the blast-physics showcase.
            for (int i = 0; i < 5; i++)
                BuildTruck(root, $"ColumnTruck_{i}", new Vector3(120f, 1.3f, 2600f + i * 45f));

            // Mock radar site.
            var radar = new GameObject("RadarSite");
            radar.transform.SetParent(root.transform, false);
            radar.transform.position = new Vector3(-220f, 0f, 3000f);
            BuildPrimitive(radar.transform, PrimitiveType.Cylinder, "Mast", new Vector3(0f, 4f, 0f),
                           new Vector3(1.2f, 4f, 1.2f), new Color(0.5f, 0.52f, 0.5f));
            var dish = BuildPrimitive(radar.transform, PrimitiveType.Sphere, "Dish", new Vector3(0f, 8.6f, 0f),
                           new Vector3(6f, 6f, 1.2f), new Color(0.75f, 0.75f, 0.78f));
            dish.transform.localRotation = Quaternion.Euler(-35f, 180f, 0f);
            AddHittable(radar, kinematic: true);

            // Building cluster.
            var sizes = new[] {
                new Vector3(10f, 12f, 10f), new Vector3(8f, 7f, 14f), new Vector3(12f, 5f, 8f),
                new Vector3(6f, 16f, 6f),  new Vector3(9f, 9f, 9f),  new Vector3(14f, 4f, 10f) };
            for (int i = 0; i < sizes.Length; i++)
            {
                Vector3 pos = new Vector3(240f + (i % 3) * 22f, sizes[i].y * 0.5f, 3150f + (i / 3) * 26f);
                BuildStaticTarget(root, $"Building_{i}", pos, sizes[i], new Color(0.55f, 0.5f, 0.45f));
            }
        }

        private static void BuildScoringRing(GameObject root, string name, Vector3 center, float radius, float height, Color color)
        {
            var ring = BuildPrimitive(root.transform, PrimitiveType.Cylinder, name,
                center + new Vector3(0f, height, 0f), new Vector3(radius * 2f, height, radius * 2f), color);
            var col = ring.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void BuildStaticTarget(GameObject root, string name, Vector3 pos, Vector3 size, Color color)
        {
            var go = BuildPrimitive(root.transform, PrimitiveType.Cube, name, pos, size, color);
            AddHittable(go, kinematic: true);
        }

        private static void BuildTruck(GameObject root, string name, Vector3 pos)
        {
            var truck = new GameObject(name);
            truck.transform.SetParent(root.transform, false);
            truck.transform.position = pos;
            BuildPrimitive(truck.transform, PrimitiveType.Cube, "Bed", new Vector3(0f, 0f, -0.8f),
                           new Vector3(2.6f, 1.6f, 5.4f), new Color(0.27f, 0.32f, 0.21f));
            BuildPrimitive(truck.transform, PrimitiveType.Cube, "Cab", new Vector3(0f, 0.7f, 2.6f),
                           new Vector3(2.4f, 2.2f, 1.8f), new Color(0.23f, 0.28f, 0.18f));
            var rb = truck.AddComponent<Rigidbody>();
            rb.mass = 7000f;
            truck.AddComponent<HittableBox>();
        }

        // ===================================================================
        // Aerial corridor
        // ===================================================================

        private static void BuildAerialCorridor()
        {
            var root = Rebuild("AerialCorridor");
            BuildDrone(root, "Drone_Low300m",  new Vector3(0f, 300f, 3000f), 800f, 90f,
                       new Color(0.8f, 0.3f, 0.3f));
            BuildDrone(root, "Drone_High800m", new Vector3(-500f, 800f, 4500f), 1200f, 140f,
                       new Color(0.8f, 0.55f, 0.2f));
        }

        private static void BuildDrone(GameObject root, string name, Vector3 center, float radius, float speed, Color color)
        {
            var drone = new GameObject(name);
            drone.transform.SetParent(root.transform, false);
            BuildPrimitive(drone.transform, PrimitiveType.Cube, "Fuselage", Vector3.zero,
                           new Vector3(1.2f, 1.2f, 6f), color);
            BuildPrimitive(drone.transform, PrimitiveType.Cube, "Wing", new Vector3(0f, 0f, 0.5f),
                           new Vector3(8f, 0.15f, 1.6f), color);

            const int Points = 12;
            var waypoints = new Vector3[Points];
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                waypoints[i] = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            drone.transform.position = waypoints[0];

            var mover = drone.AddComponent<WaypointMover>();
            mover.SetPath(waypoints, speed, looped: true);

            var rb = drone.AddComponent<Rigidbody>();
            rb.isKinematic = true;         // WaypointMover owns motion; body exists for fuze/queries
            rb.useGravity = false;
            drone.AddComponent<HittableBox>();
        }

        // ===================================================================
        // Naval sector (x > 6200 basin)
        // ===================================================================

        private static void BuildNavalSector()
        {
            var root = Rebuild("NavalSector");

            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            water.transform.SetParent(root.transform, false);
            water.transform.position = new Vector3(9200f, -2f, 8000f);
            water.transform.localScale = new Vector3(600f, 1f, 2000f); // 6 km x 20 km
            var rend = water.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(new Color(0.10f, 0.22f, 0.35f));

            BuildShipHulk(root, "ShipHulk_A", new Vector3(7800f, 0f, 2500f), headingDeg: 15f);
            BuildShipHulk(root, "ShipHulk_B", new Vector3(8400f, 0f, 4200f), headingDeg: -30f);
        }

        private static void BuildShipHulk(GameObject root, string name, Vector3 pos, float headingDeg)
        {
            var ship = new GameObject(name);
            ship.transform.SetParent(root.transform, false);
            ship.transform.position = pos;
            ship.transform.rotation = Quaternion.Euler(0f, headingDeg, 0f);
            var gray = new Color(0.45f, 0.47f, 0.5f);
            BuildPrimitive(ship.transform, PrimitiveType.Cube, "Hull", new Vector3(0f, 3f, 0f),
                           new Vector3(16f, 8f, 120f), gray);
            BuildPrimitive(ship.transform, PrimitiveType.Cube, "Superstructure", new Vector3(0f, 11f, -10f),
                           new Vector3(10f, 8f, 28f), gray * 1.15f);
            BuildPrimitive(ship.transform, PrimitiveType.Cube, "Mast", new Vector3(0f, 19f, -14f),
                           new Vector3(1.5f, 8f, 1.5f), gray * 0.8f);
            AddHittable(ship, kinematic: true);
        }

        // ===================================================================
        // Launchers
        // ===================================================================

        private static void BuildVlsPad(MissileProfileSO profile, GameObject missilePrefab)
        {
            var root = Rebuild("VlsLauncher");
            root.transform.position = new Vector3(-60f, 0f, -20f);

            BuildPrimitive(root.transform, PrimitiveType.Cube, "Pad", new Vector3(0f, 0.25f, 0f),
                           new Vector3(14f, 0.5f, 10f), new Color(0.4f, 0.4f, 0.4f));
            BuildPrimitive(root.transform, PrimitiveType.Cube, "CellBlock", new Vector3(0f, 1.6f, 0f),
                           new Vector3(6f, 2.6f, 8f), new Color(0.32f, 0.36f, 0.34f));
            for (int i = 0; i < 8; i++)
                BuildPrimitive(root.transform, PrimitiveType.Cube, $"CellLid_{i}",
                               new Vector3(-1.5f + (i % 2) * 3f, 2.95f, -3f + (i / 2) * 2f),
                               new Vector3(2.4f, 0.12f, 1.7f), new Color(0.2f, 0.22f, 0.2f));

            // Muzzle: straight up out of the forward-left cell.
            var muzzle = new GameObject("VlsMuzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(-1.5f, 3.4f, -3f);
            muzzle.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

            AddRunner(root, profile, missilePrefab, muzzle.transform,
                      initialTargetName: "Drone_Low300m");
        }

        private static void BuildDemolitionPad(MissileProfileSO profile)
        {
            var root = Rebuild("DemolitionPad");
            root.transform.position = new Vector3(60f, 0f, -20f);

            BuildPrimitive(root.transform, PrimitiveType.Cube, "Pad", new Vector3(0f, 0.25f, 0f),
                           new Vector3(14f, 0.5f, 14f), new Color(0.5f, 0.45f, 0.4f));

            for (int i = 0; i < 3; i++)
            {
                var charge = BuildPrimitive(root.transform, PrimitiveType.Cylinder, $"DemoCharge_{i}",
                    new Vector3(-4f + i * 4f, 0.9f, 0f), new Vector3(0.8f, 0.4f, 0.8f),
                    new Color(0.55f, 0.15f, 0.12f));
                var comp = charge.AddComponent<DemolitionCharge>();
                var so = new SerializedObject(comp);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Blast props: rigidbody crates ringing the pad so a detonation reads visually.
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                var crate = BuildPrimitive(root.transform, PrimitiveType.Cube, $"DemoProp_{i}",
                    new Vector3(Mathf.Cos(a) * 9f, 1f, Mathf.Sin(a) * 9f),
                    new Vector3(1.6f, 1.6f, 1.6f), new Color(0.7f, 0.55f, 0.3f));
                var rb = crate.AddComponent<Rigidbody>();
                rb.mass = 60f;
            }

            root.AddComponent<DemolitionPad>();
        }

        private static void BuildMesaGantry(MissileProfileSO profile, GameObject missilePrefab)
        {
            var root = Rebuild("MesaGantry");
            // Mesa top: SampleHeightM(3000, 2500) = LandH + 460 → world y = 460.
            Vector3 basePos = new Vector3(3000f, 460f, 2500f);
            root.transform.position = basePos;

            BuildPrimitive(root.transform, PrimitiveType.Cube, "Plinth", new Vector3(0f, 0.75f, 0f),
                           new Vector3(6f, 1.5f, 6f), new Color(0.42f, 0.42f, 0.4f));
            var rail = BuildPrimitive(root.transform, PrimitiveType.Cube, "Rail", new Vector3(0f, 2.4f, 1.2f),
                           new Vector3(0.7f, 0.7f, 6f), new Color(0.2f, 0.2f, 0.2f));

            // Aim down-slope at the vehicle column (deterministic A2G geometry).
            Vector3 muzzlePos = basePos + new Vector3(0f, 3f, 3f);
            Vector3 aim = new Vector3(120f, 1.5f, 2700f);
            Vector3 dir = (aim - muzzlePos).normalized;
            rail.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            AddRunner(root, profile, missilePrefab, muzzleTransform: null,
                      initialTargetName: "ColumnTruck_0",
                      launchPosition: muzzlePos, launchDirection: dir);
        }

        private static void BuildAirborneTestbed(MissileProfileSO profile, GameObject missilePrefab)
        {
            var root = Rebuild("AirborneTestbed");

            var body = new Color(0.35f, 0.4f, 0.48f);
            BuildPrimitive(root.transform, PrimitiveType.Cube, "Fuselage", Vector3.zero,
                           new Vector3(1.4f, 1.4f, 12f), body);
            BuildPrimitive(root.transform, PrimitiveType.Cube, "Wing", new Vector3(0f, 0f, 0.5f),
                           new Vector3(13f, 0.2f, 2.6f), body);
            BuildPrimitive(root.transform, PrimitiveType.Cube, "Tail", new Vector3(0f, 1.1f, -5.4f),
                           new Vector3(0.2f, 2.2f, 1.6f), body);

            // Racetrack orbit at 1200 m.
            const int Points = 12;
            Vector3 center = new Vector3(0f, 1200f, 4000f);
            var waypoints = new Vector3[Points];
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                waypoints[i] = center + new Vector3(Mathf.Cos(a) * 1500f, 0f, Mathf.Sin(a) * 1500f);
            }
            root.transform.position = waypoints[0];
            var mover = root.AddComponent<WaypointMover>();
            mover.SetPath(waypoints, 160f, looped: true);

            // Wing pylon muzzle: slightly below and outboard, firing along the aircraft nose.
            var pylon = new GameObject("WingPylonMuzzle");
            pylon.transform.SetParent(root.transform, false);
            pylon.transform.localPosition = new Vector3(2.6f, -0.9f, 1.2f);
            pylon.transform.localRotation = Quaternion.identity;

            AddRunner(root, profile, missilePrefab, pylon.transform,
                      initialTargetName: "ShipHulk_A");
        }

        // ===================================================================
        // Shared helpers
        // ===================================================================

        private static void BuildExtendedMarkers()
        {
            foreach (float d in new[] { 7500f, 10000f })
            {
                string name = $"Marker_{d:0}m";
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = name;
                pillar.transform.position = new Vector3(0f, 15f, d);
                pillar.transform.localScale = new Vector3(3f, 30f, 3f);
                var rend = pillar.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = RangeMaterials.MakeColored(new Color(0.9f, 0.9f, 0.95f));
            }
        }

        /// <summary>Delete-and-recreate a sector root (idempotent rebuilds).</summary>
        private static GameObject Rebuild(string rootName)
        {
            var old = GameObject.Find(rootName);
            if (old != null) Object.DestroyImmediate(old);
            return new GameObject(rootName);
        }

        private static GameObject BuildPrimitive(Transform parent, PrimitiveType type, string name,
                                                 Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(color);
            return go;
        }

        private static void AddHittable(GameObject go, bool kinematic)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = kinematic;
            rb.useGravity = !kinematic;
            go.AddComponent<HittableBox>();
        }

        private static void AddRunner(GameObject host, MissileProfileSO profile, GameObject missilePrefab,
                                      Transform muzzleTransform, string initialTargetName,
                                      Vector3 launchPosition = default, Vector3 launchDirection = default)
        {
            var runner = host.AddComponent<GuidedFury_TestRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("missilePrefab").objectReferenceValue = missilePrefab;
            so.FindProperty("lod").enumValueIndex = (int)MissileLod.L4_FullAero6Dof;
            so.FindProperty("autoLaunchOnStart").boolValue = false;
            if (muzzleTransform != null)
            {
                so.FindProperty("muzzleTransform").objectReferenceValue = muzzleTransform;
            }
            else
            {
                so.FindProperty("launchPosition").vector3Value = launchPosition;
                so.FindProperty("launchDirection").vector3Value = launchDirection;
            }
            var targetGo = string.IsNullOrEmpty(initialTargetName) ? null : GameObject.Find(initialTargetName);
            if (targetGo != null)
                so.FindProperty("target").objectReferenceValue = targetGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
