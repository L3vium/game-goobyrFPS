using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The grass review scene (<c>Scenes/World/GrassTest.unity</c>): a 256 m rolling meadow under San
    /// Lázaro's sky and time of day, a dirt path through it, a patch of tall grass to hide in, a few
    /// jungle trees, bushes and rocks, a dropped crate that keeps the grass pressed, and the real
    /// grass baked over it. <see cref="Benchmark"/> plays it and measures the grass's GPU and CPU cost
    /// at 1080p from fixed viewpoints, grass on against grass off.
    /// </summary>
    public static class GrassTestScene
    {
        public const string FOLDER = EditorAssets.SCENES + "/World";
        public const string SCENE = FOLDER + "/GrassTest.unity";
        private const float SIZE = 256f, HEIGHT = 48f;

        /// <summary>The dirt path (no grass on it).</summary>
        public static readonly Vector2[] PATH = { new Vector2(-110f, -128f), new Vector2(-50f, -70f), new Vector2(-8f, -20f), new Vector2(22f, 30f), new Vector2(8f, 80f), new Vector2(-20f, 128f) };
        /// <summary>The tall grass patch you hide in.</summary>
        public static readonly Vector2 TALL = new Vector2(34f, -34f);

        /// <summary>The review viewpoints: name, eye (x, z, height over the ground), target (x, z, height).</summary>
        public static readonly (string name, Vector3 eye, Vector3 target)[] VIEWS =
        {
            ("close", new Vector3(-14f, -46f, 1.6f), new Vector3(-10f, -36f, 0.2f)),
            ("mid", new Vector3(-30f, -78f, 1.6f), new Vector3(10f, -20f, 1.5f)),
            ("far", new Vector3(0f, 70f, 1.6f), new Vector3(-10f, -60f, 0f)),
            ("low", new Vector3(-12f, -42f, 0.45f), new Vector3(-4f, -30f, 0.4f)),
            ("tall", new Vector3(24f, -50f, 1.6f), new Vector3(34f, -34f, 0.8f)),
            ("pickup", new Vector3(-8.5f, -34f, 1.6f), new Vector3(-5f, -30f, 0f)),
            ("trample", new Vector3(-17f, -14f, 6f), new Vector3(-6f, -14f, 0f))
        };

        public static float Ground(float pX, float pZ)
        {
            float h = 8f + 3.5f * LandShape.Fbm(pX, pZ, 70f, 3, 4.1f) + 1.2f * LandShape.Fbm(pX, pZ, 18f, 2, 8.3f) + LandShape.DetailNoise(pX, pZ);
            // A hill to the north, for a view down over the whole meadow.
            float lNorth = Mathf.Clamp01((pZ - 10f) / 110f);
            h += lNorth * lNorth * 20f;
            // The path is cut a little into the slope.
            float d = LandShape.PathDistance(PATH, new Vector2(pX, pZ), out _, out _);
            if (d < 4f) h -= 0.25f * (1f - d / 4f);
            return h;
        }

        private static float PathDistance(float pX, float pZ) => LandShape.PathDistance(PATH, new Vector2(pX, pZ), out _, out _);

        [MenuItem("Goobyr/Debug/Grass Test Scene")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorAssets.EnsureFolder(FOLDER);

            // Land.
            string lDataPath = FOLDER + "/GrassTest_Terrain.asset";
            TerrainData lData = AssetDatabase.LoadAssetAtPath<TerrainData>(lDataPath);
            if (lData == null)
            {
                lData = new TerrainData();
                AssetDatabase.CreateAsset(lData, lDataPath);
            }
            const int RES = 513;
            lData.heightmapResolution = RES;
            lData.size = new Vector3(SIZE, HEIGHT, SIZE);
            Vector3 o = new Vector3(-SIZE * 0.5f, 0f, -SIZE * 0.5f);
            float[,] lHeights = new float[RES, RES];
            for (int z = 0; z < RES; z++)
                for (int x = 0; x < RES; x++)
                    lHeights[z, x] = Mathf.Clamp01(Ground(o.x + x * SIZE / (RES - 1), o.z + z * SIZE / (RES - 1)) / HEIGHT);
            lData.SetHeights(0, 0, lHeights);
            // The grass layer is tinted to the grass's far colour exactly as the maps' RealGrass does.
            GrassStyle lStyle = GrassStyle.Jungle();
            TerrainLayer lGrass = GrassBaker.Turf(Own("leafy_grass", 4f, Vector4.one), lStyle.FarColor, "GrassTest_Turf_Grass"), lDirt = Own("dirt", 4f, Vector4.one), lLeaves = Own("forest_leaves_02", 4f, Vector4.one);
            lData.terrainLayers = new[] { lGrass, lDirt, lLeaves };
            lData.alphamapResolution = 512;
            float[,,] lAlpha = new float[512, 512, 3];
            for (int z = 0; z < 512; z++)
                for (int x = 0; x < 512; x++)
                {
                    float wx = o.x + (x + 0.5f) / 512f * SIZE, wz = o.z + (z + 0.5f) / 512f * SIZE;
                    float lPath = Mathf.Clamp01(1f - (PathDistance(wx, wz) - 1.3f) / 1.2f);
                    float lLitter = Mathf.Clamp01(LandShape.Fbm(wx, wz, 20f, 2, 3.3f) * 2f - 0.6f) * (1f - lPath);
                    lAlpha[z, x, 0] = Mathf.Max(0f, 1f - lPath - lLitter);
                    lAlpha[z, x, 1] = lPath;
                    lAlpha[z, x, 2] = lLitter;
                }
            lData.SetAlphamaps(0, 0, lAlpha);

            // Trees, bushes and rocks round the meadow (never on the path or in the tall grass).
            System.Random r = new System.Random(7);
            string[] lPieces = { "Veg_JungleTree_A", "Veg_JungleTree_B", "Veg_JungleBush_A", "Veg_JungleBush_B", "Veg_Rock_A", "Veg_Rock_B", "Veg_TreeFern", "Veg_Banana" };
            TreePrototype[] lPrototypes = new TreePrototype[lPieces.Length];
            for (int i = 0; i < lPieces.Length; i++) lPrototypes[i] = new TreePrototype { prefab = VegLibrary.TreePrefab(lPieces[i]) };
            lData.treePrototypes = lPrototypes;
            System.Collections.Generic.List<TreeInstance> lTrees = new System.Collections.Generic.List<TreeInstance>();
            for (int k = 0; k < 420; k++)
            {
                float x = (float)r.NextDouble() * (SIZE - 16f) - SIZE * 0.5f + 8f, z = (float)r.NextDouble() * (SIZE - 16f) - SIZE * 0.5f + 8f;
                if (PathDistance(x, z) < 5f || Vector2.Distance(new Vector2(x, z), TALL) < 16f) continue;
                // Woods on the edges and in clumps, the middle of the meadow open.
                float lEdge = Mathf.Clamp01((Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - 70f) / 40f);
                float lClump = Mathf.Clamp01(LandShape.Fbm(x, z, 40f, 2, 5.5f) * 2.5f);
                if (r.NextDouble() > lEdge * 0.9f + lClump * 0.35f) continue;
                int p = r.Next(lPieces.Length);
                float s = lPieces[p].Contains("Rock") ? 1f + (float)r.NextDouble() * 1.5f : 0.85f + (float)r.NextDouble() * 0.35f;
                lTrees.Add(new TreeInstance
                {
                    position = new Vector3((x - o.x) / SIZE, Mathf.Clamp01((Ground(x, z) - (lPieces[p].Contains("Rock") ? 0.3f * s : 0.1f)) / HEIGHT), (z - o.z) / SIZE),
                    prototypeIndex = p,
                    widthScale = s,
                    heightScale = s,
                    rotation = (float)r.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }
            lData.SetTreeInstances(lTrees.ToArray(), true);
            EditorUtility.SetDirty(lData);

            GameObject lLand = Terrain.CreateTerrainGameObject(lData);
            lLand.name = "Land";
            lLand.transform.position = o;
            lLand.isStatic = true;
            Terrain lTerrain = lLand.GetComponent<Terrain>();
            lTerrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>(PbrLibrary.MATERIALS + "/M_LandPbr.mat");
            lTerrain.heightmapPixelError = 1f;
            lTerrain.basemapDistance = 220f;
            lTerrain.drawInstanced = true;
            lTerrain.treeDistance = 600f;
            lTerrain.treeBillboardDistance = 600f;
            lTerrain.shadowCastingMode = ShadowCastingMode.TwoSided;

            // The grass: dense meadow, thinner and drier on the hill, none on the path, tall in the patch.
            GrassBaker.Bake(lTerrain, "GrassTest", lStyle,
                (x, z) =>
                {
                    float d = PathDistance(x, z);
                    float lEdgeOfPath = Mathf.Clamp01((d - 1.6f) / 2.2f);
                    return lEdgeOfPath * Mathf.Clamp01(0.8f + 0.5f * LandShape.Fbm(x, z, 16f, 2, 2.2f));
                },
                (x, z) => Mathf.Clamp01(0.55f + 0.4f * LandShape.Fbm(x, z, 11f, 2, 6.1f) - Mathf.Clamp01((z - 30f) / 90f) * 0.2f),
                (x, z) => Mathf.Clamp01(0.15f + 0.5f * LandShape.Fbm(x, z, 26f, 2, 1.9f) + Mathf.Clamp01((z - 20f) / 100f) * 0.3f),
                (x, z) => Mathf.Clamp01((10f + 2f * LandShape.Fbm(x, z, 5f, 2, 9.9f) - Vector2.Distance(new Vector2(x, z), TALL)) / 2.5f));

            // Light: San Lázaro's sun, sky, grade and time of day.
            MapDefinition lMap = null;
            foreach (MapDefinition m in GameDatabase.Instance.maps) if (m.sceneName == "Map_SanLazaro") lMap = m;
            GameObject lSun = new GameObject("Sun");
            Light lLight = lSun.AddComponent<Light>();
            lLight.type = LightType.Directional;
            lLight.shadows = LightShadows.Soft;
            TimePreset lNoon = TimeOfDay.PresetsFor(TimeOfDay.Biome.Jungle, 30f)[1];
            lSun.transform.rotation = Quaternion.Euler(lNoon.sun.x, lNoon.sun.y, 0f);
            lLight.color = lNoon.sunColor;
            lLight.intensity = lNoon.sunIntensity;
            RenderSettings.sun = lLight;
            Material lSky = lMap != null ? ArtBuilder.Sky(lMap) : null;
            if (lSky != null) RenderSettings.skybox = lSky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = false;
            GameObject lTime = new GameObject("TimeOfDay");
            lTime.AddComponent<TimeOfDay>().Setup(lLight, lSky, TimeOfDay.Biome.Jungle, 30f);
            if (lMap != null)
            {
                Volume lVolume = new GameObject("MapVolume").AddComponent<Volume>();
                lVolume.isGlobal = true;
                lVolume.priority = 1;
                lVolume.sharedProfile = ArtBuilder.Volume(lMap) ?? ArtBuilder.VolumeProfileFor(lMap);
            }

            // A dropped crate by the path keeps the grass round it pressed (items stay visible).
            GameObject lCrate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lCrate.name = "DroppedCrate";
            lCrate.transform.localScale = new Vector3(0.5f, 0.35f, 0.4f);
            lCrate.transform.position = new Vector3(-5f, Ground(-5f, -30f) + 0.17f, -30f);
            lCrate.AddComponent<GrassBender>().Radius = 1.1f;

            // Review camera at the first viewpoint.
            GameObject lCamera = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera c = lCamera.AddComponent<Camera>();
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 900f;
            UnityEngine.Rendering.Universal.UniversalAdditionalCameraData lCameraData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(c);
            lCameraData.renderPostProcessing = true;
            lCameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.TemporalAntiAliasing;
            Place(lCamera.transform, VIEWS[1].eye, VIEWS[1].target);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SCENE);
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] grass test scene " + SCENE);
        }

        /// <summary>
        /// The scene's own copy of a PBR ground layer (the shared layers the maps use stay untouched),
        /// tiled every <paramref name="pTile"/> metres, its albedo scaled: the grass layer becomes an
        /// olive turf, the colour the grass has from afar, so the meadow does not change colour where
        /// the blades end (90 m).
        /// </summary>
        private static TerrainLayer Own(string pId, float pTile, Vector4 pAlbedo)
        {
            TerrainLayer lShared = AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{PbrLibrary.MATERIALS}/TL_{pId}.terrainlayer");
            if (lShared == null) lShared = PbrLibrary.Layer(pId, pTile);
            string lPath = $"{FOLDER}/GrassTest_{pId}.terrainlayer";
            TerrainLayer lOwn = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lPath);
            if (lOwn == null)
            {
                lOwn = new TerrainLayer();
                AssetDatabase.CreateAsset(lOwn, lPath);
            }
            EditorUtility.CopySerialized(lShared, lOwn);
            lOwn.name = "GrassTest_" + pId;
            lOwn.tileSize = new Vector2(pTile, pTile);
            lOwn.diffuseRemapMin = Vector4.zero;
            lOwn.diffuseRemapMax = pAlbedo;
            EditorUtility.SetDirty(lOwn);
            return lOwn;
        }

        /// <summary>A point given as (x, z, height over the ground) on the meadow.</summary>
        public static Vector3 OnGround(Vector3 p) => new Vector3(p.x, Ground(p.x, p.y) + p.z, p.y);

        public static void Place(Transform pCamera, Vector3 pEye, Vector3 pTarget)
        {
            Vector3 a = OnGround(pEye), b = OnGround(pTarget);
            pCamera.SetPositionAndRotation(a, Quaternion.LookRotation(b - a));
        }

        /// <summary>A dirt-track of footsteps (for the trample review): presses the grass along a line now.</summary>
        public static void Trail(Vector3 pFrom, Vector3 pTo, float pStep = 0.4f)
        {
            int n = Mathf.CeilToInt(Vector3.Distance(pFrom, pTo) / pStep);
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(pFrom, pTo, (float)i / n);
                GrassTrample.Stamp(new Vector3(p.x, Ground(p.x, p.z), p.z), 0.55f, 1f);
            }
        }

        /// <summary>Plays the scene and measures the grass at 1080p (writes <c>Logs/grass_bench.json</c>).</summary>
        [MenuItem("Goobyr/Debug/Grass Benchmark")]
        public static void Benchmark()
        {
            if (EditorSceneManager.GetActiveScene().path != SCENE) EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            // One benchmark at a time (an earlier one stays in the scene after play mode).
            foreach (GrassBenchmark lOld in Object.FindObjectsByType<GrassBenchmark>(FindObjectsSortMode.None)) Object.DestroyImmediate(lOld.gameObject);
            GameObject lBench = new GameObject("GrassBenchmark");
            GrassBenchmark b = lBench.AddComponent<GrassBenchmark>();
            int n = 3;
            b.From = new Vector3[n];
            b.To = new Vector3[n];
            b.Names = new string[n];
            int k = 0;
            foreach ((string lName, Vector3 lEye, Vector3 lTarget) in VIEWS)
            {
                if (lName != "close" && lName != "mid" && lName != "far") continue;
                b.Names[k] = lName;
                b.From[k] = OnGround(lEye);
                b.To[k] = OnGround(lTarget);
                k++;
            }
            EditorApplication.isPlaying = true;
        }
    }
}
