using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>The ground layers painted on the terrain, in the order of <see cref="TextureBuilder.NAMES"/>.</summary>
    public enum Ground { Grass, DryGrass, ForestFloor, Dirt, Rock, Sand, Asphalt, Cobble }

    /// <summary>
    /// The land: a Unity <see cref="Terrain"/> shaped by a height function (see
    /// <see cref="LandShape"/>), painted with the stylized ground layers, covered with forests (tree
    /// instances: tens of thousands cost almost nothing in the scene file) and grass (details).
    /// A map calls <see cref="BuildLand"/>, then <see cref="PaintLand"/>, adds trees and grass, and
    /// <see cref="Finish"/> flushes them.
    /// </summary>
    public partial class MapKit
    {
        public Terrain Land { get; private set; }
        private TerrainData _landData;
        private float _landSize;
        private Vector3 _landOrigin;
        private readonly List<TreeInstance> _treeInstances = new List<TreeInstance>();
        private readonly List<TreePrototype> _treePrototypes = new List<TreePrototype>();
        private readonly Dictionary<string, int> _treeIndex = new Dictionary<string, int>();
        private System.Func<float, float, float> _grassDensity;

        public const int LAND_RESOLUTION = 1025;
        private string LandPath => EditorAssets.SCENES + "/" + Map.sceneName + "_Terrain.asset";
        private const int ALPHA_RESOLUTION = 1024, DETAIL_RESOLUTION = 1024;

        /// <summary>Builds the terrain, centred on the origin, from a world height function (metres).</summary>
        public void BuildLand(float pSize, float pMinY, float pMaxY, System.Func<float, float, float> pHeight)
        {
            _landSize = pSize;
            _landOrigin = new Vector3(-pSize * 0.5f, pMinY, -pSize * 0.5f);
            float lRange = pMaxY - pMinY;
            // Rebuilt in place: the asset keeps its GUID, and what is painted on it later is saved with it.
            _landData = AssetDatabase.LoadAssetAtPath<TerrainData>(LandPath);
            if (_landData == null)
            {
                _landData = new TerrainData();
                AssetDatabase.CreateAsset(_landData, LandPath);
            }
            _landData.heightmapResolution = LAND_RESOLUTION;
            _landData.size = new Vector3(pSize, lRange, pSize);
            float[,] lHeights = new float[LAND_RESOLUTION, LAND_RESOLUTION];
            float lStep = pSize / (LAND_RESOLUTION - 1);
            for (int z = 0; z < LAND_RESOLUTION; z++)
                for (int x = 0; x < LAND_RESOLUTION; x++)
                    lHeights[z, x] = Mathf.Clamp01((pHeight(_landOrigin.x + x * lStep, _landOrigin.z + z * lStep) - pMinY) / lRange);
            _landData.SetHeights(0, 0, lHeights);
            _landData.terrainLayers = TextureBuilder.Layers();
            _landData.alphamapResolution = ALPHA_RESOLUTION;
            _landData.SetTreeInstances(new TreeInstance[0], false);
            _landData.detailPrototypes = new DetailPrototype[0];

            GameObject lObject = Terrain.CreateTerrainGameObject(_landData);
            lObject.name = "Land";
            lObject.layer = GameLayers.DEFAULT;
            lObject.transform.SetParent(Env, false);
            lObject.transform.position = _landOrigin;
            lObject.isStatic = true;
            Land = lObject.GetComponent<Terrain>();
            Land.materialTemplate = LandMaterial();
            Land.heightmapPixelError = 3f;
            Land.basemapDistance = 220f;
            Land.drawInstanced = true;
            Land.treeDistance = 700f;
            Land.treeBillboardDistance = 700f;
            Land.treeMaximumFullLODCount = 20000;
            Land.detailObjectDistance = 90f;
            Land.detailObjectDensity = 1f;
            Land.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        }

        private static Material LandMaterial()
        {
            string lPath = TextureBuilder.FOLDER + "/M_Land.mat";
            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lPath);
            if (lMaterial != null) return lMaterial;
            EditorAssets.EnsureFolder(TextureBuilder.FOLDER);
            lMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            lMaterial.EnableKeyword("_NORMALMAP");
            AssetDatabase.CreateAsset(lMaterial, lPath);
            return lMaterial;
        }

        /// <summary>World point of a heightmap/alphamap sample.</summary>
        private Vector2 LandPoint(int pX, int pZ, int pResolution) => new Vector2(_landOrigin.x + (pX + 0.5f) / pResolution * _landSize, _landOrigin.z + (pZ + 0.5f) / pResolution * _landSize);

        /// <summary>Slope in degrees at a world point.</summary>
        public float LandSlope(float pX, float pZ) => _landData.GetSteepness((pX - _landOrigin.x) / _landSize, (pZ - _landOrigin.z) / _landSize);

        /// <summary>
        /// Paints the ground layers: the function gets (x, z, height, slope in degrees) and returns a
        /// weight per <see cref="Ground"/> layer (normalised here).
        /// </summary>
        public void PaintLand(System.Func<float, float, float, float, float[]> pWeights)
        {
            int lLayers = _landData.terrainLayers.Length;
            float[,,] lMap = new float[ALPHA_RESOLUTION, ALPHA_RESOLUTION, lLayers];
            for (int z = 0; z < ALPHA_RESOLUTION; z++)
                for (int x = 0; x < ALPHA_RESOLUTION; x++)
                {
                    Vector2 p = LandPoint(x, z, ALPHA_RESOLUTION);
                    float[] w = pWeights(p.x, p.y, GroundAt(p.x, p.y), LandSlope(p.x, p.y));
                    float lSum = 0f;
                    for (int l = 0; l < lLayers && l < w.Length; l++) lSum += Mathf.Max(0f, w[l]);
                    if (lSum <= 0f) { lMap[z, x, 0] = 1f; continue; }
                    for (int l = 0; l < lLayers && l < w.Length; l++) lMap[z, x, l] = Mathf.Max(0f, w[l]) / lSum;
                }
            _landData.SetAlphamaps(0, 0, lMap);
        }

        /// <summary>A tree (or a bush) of the kit's nature set planted on the terrain.</summary>
        public void LandTree(string pPiece, float pX, float pZ, float pScale, float pYaw = -1f)
        {
            if (!_treeIndex.TryGetValue(pPiece, out int lIndex))
            {
                GameObject lPrefab = TreePrefab(pPiece);
                if (lPrefab == null) return;
                lIndex = _treePrototypes.Count;
                _treePrototypes.Add(new TreePrototype { prefab = lPrefab, bendFactor = 0.2f });
                _treeIndex[pPiece] = lIndex;
            }
            // Seat on the lowest ground under the footprint so no side floats on a slope; rocks bed in deeper.
            bool lRock = pPiece.StartsWith("Rock");
            float lHeight = LowestGround(new Vector3(pX, 0f, pZ), (lRock ? 0.9f : 0.45f) * pScale) - (lRock ? 0.3f * pScale : 0.15f);
            _treeInstances.Add(new TreeInstance
            {
                position = new Vector3((pX - _landOrigin.x) / _landSize, Mathf.Clamp01((lHeight - _landOrigin.y) / _landData.size.y), (pZ - _landOrigin.z) / _landSize),
                prototypeIndex = lIndex,
                widthScale = pScale * (0.9f + (float)Rng.NextDouble() * 0.2f),
                heightScale = pScale,
                rotation = (pYaw < 0f ? (float)Rng.NextDouble() * 360f : pYaw) * Mathf.Deg2Rad,
                color = Color.white,
                lightmapColor = Color.white
            });
        }

        /// <summary>
        /// The prefab a terrain tree uses: the kit mesh with its default tint, a trunk collider
        /// (bushes and grass have none) and a LOD group that culls it when tiny on screen.
        /// </summary>
        private GameObject TreePrefab(string pPiece)
        {
            string lFolder = EditorAssets.ROOT + "/Art/Generated/Trees", lPath = lFolder + "/" + pPiece + ".prefab";
            GameObject lExisting = AssetDatabase.LoadAssetAtPath<GameObject>(lPath);
            if (lExisting != null) return lExisting;
            Mesh lMesh = KitMesh("Nature", pPiece);
            if (lMesh == null) return null;
            EditorAssets.EnsureFolder(lFolder);
            GameObject lTree = new GameObject(pPiece);
            GameObject lModel = new GameObject("Model");
            lModel.transform.SetParent(lTree.transform, false);
            lModel.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            lModel.AddComponent<MeshFilter>().sharedMesh = lMesh;
            MeshRenderer lRenderer = lModel.AddComponent<MeshRenderer>();
            lRenderer.sharedMaterial = _toon;
            SetColor(lRenderer, DefaultTint("Nature", pPiece));
            bool lSmall = pPiece.StartsWith("Bush") || pPiece.StartsWith("Grass") || pPiece.StartsWith("Reeds") || pPiece.StartsWith("Flower") || pPiece.StartsWith("Rock");
            if (!lSmall)
            {
                CapsuleCollider lTrunk = lTree.AddComponent<CapsuleCollider>();
                lTrunk.radius = 0.35f;
                lTrunk.height = Mathf.Min(6f, lMesh.bounds.size.y);
                lTrunk.center = new Vector3(0, lTrunk.height * 0.5f, 0);
            }
            else if (pPiece.StartsWith("Rock")) lTree.AddComponent<BoxCollider>().size = lMesh.bounds.size * 0.85f;
            LODGroup lLod = lTree.AddComponent<LODGroup>();
            lLod.SetLODs(new[] { new LOD(lSmall ? 0.025f : 0.008f, new Renderer[] { lRenderer }) });
            lLod.RecalculateBounds();
            GameObject lPrefab = PrefabUtility.SaveAsPrefabAsset(lTree, lPath);
            Object.DestroyImmediate(lTree);
            return lPrefab;
        }

        /// <summary>Grass tufts over the terrain: density 0..1 per world point (0 on roads, floors, water).</summary>
        public void LandGrass(System.Func<float, float, float> pDensity) => _grassDensity = pDensity;

        /// <summary>Flushes trees and grass into the terrain data (called by <see cref="Finish"/>).</summary>
        private void FinishLand()
        {
            if (Land == null) return;
            _landData.treePrototypes = _treePrototypes.ToArray();
            _landData.SetTreeInstances(_treeInstances.ToArray(), true);
            if (_grassDensity != null)
            {
                _landData.SetDetailResolution(DETAIL_RESOLUTION, 32);
                _landData.detailPrototypes = new[]
                {
                    new DetailPrototype
                    {
                        prototypeTexture = TextureBuilder.GrassBlades(),
                        renderMode = DetailRenderMode.GrassBillboard,
                        minWidth = 0.5f, maxWidth = 0.9f, minHeight = 0.35f, maxHeight = 0.7f,
                        healthyColor = new Color(0.5f, 0.68f, 0.3f), dryColor = new Color(0.7f, 0.66f, 0.38f),
                        noiseSpread = 0.3f
                    }
                };
                int[,] lLayer = new int[DETAIL_RESOLUTION, DETAIL_RESOLUTION];
                for (int z = 0; z < DETAIL_RESOLUTION; z++)
                    for (int x = 0; x < DETAIL_RESOLUTION; x++)
                    {
                        Vector2 p = LandPoint(x, z, DETAIL_RESOLUTION);
                        lLayer[z, x] = Mathf.RoundToInt(Mathf.Clamp01(_grassDensity(p.x, p.y)) * 6f);
                    }
                _landData.SetDetailLayer(0, 0, 0, lLayer);
            }
            Land.terrainData = _landData;
            Land.GetComponent<TerrainCollider>().terrainData = _landData;
            Land.Flush();
            EditorUtility.SetDirty(_landData);
            AssetDatabase.SaveAssetIfDirty(_landData);
            Debug.Log($"[Build] {Map.sceneName} land: {_treeInstances.Count} trees");
        }
    }

    /// <summary>
    /// A height function built from stamps applied in order over a base relief: plateaus,
    /// terraces, discs, carved channels and graded roads. Each stamp sees the height left by the
    /// previous ones, so a road laid after the hills climbs them smoothly.
    /// </summary>
    public class LandShape
    {
        private readonly System.Func<float, float, float> _base;
        private readonly List<(Rect bounds, System.Func<float, float, float, float> op)> _stamps = new List<(Rect, System.Func<float, float, float, float>)>();

        public LandShape(System.Func<float, float, float> pBase) => _base = pBase;

        public float Height(float pX, float pZ)
        {
            float h = _base(pX, pZ);
            Vector2 p = new Vector2(pX, pZ);
            foreach ((Rect lBounds, System.Func<float, float, float, float> lOp) in _stamps)
                if (lBounds.Contains(p)) h = lOp(pX, pZ, h);
            return h;
        }

        private LandShape Add(Rect pBounds, System.Func<float, float, float, float> pOp)
        {
            _stamps.Add((pBounds, pOp));
            return this;
        }

        public static float RectDistance(Rect pRect, float pX, float pZ)
        {
            float dx = Mathf.Max(pRect.xMin - pX, 0f, pX - pRect.xMax), dz = Mathf.Max(pRect.yMin - pZ, 0f, pZ - pRect.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Rect Grow(Rect pRect, float pBy) => new Rect(pRect.xMin - pBy, pRect.yMin - pBy, pRect.width + pBy * 2f, pRect.height + pBy * 2f);

        /// <summary>A flat pad at a height, blending into the land over a feather (a building lot, a square).</summary>
        public LandShape Flat(Rect pRect, float pHeight, float pFeather) => Add(Grow(pRect, pFeather), (x, z, h) =>
        {
            float d = RectDistance(pRect, x, z);
            return d <= 0f ? pHeight : Mathf.Lerp(pHeight, h, Mathf.SmoothStep(0f, 1f, d / pFeather));
        });

        /// <summary>A rotated flat pad (a building that does not follow the axes).</summary>
        public LandShape Flat(Vector2 pCenter, Vector2 pSize, float pYaw, float pHeight, float pFeather)
        {
            float lReach = pSize.magnitude * 0.5f + pFeather;
            Quaternion lInverse = Quaternion.Euler(0, -pYaw, 0);
            Rect lLocal = new Rect(-pSize.x * 0.5f, -pSize.y * 0.5f, pSize.x, pSize.y);
            return Add(new Rect(pCenter.x - lReach, pCenter.y - lReach, lReach * 2f, lReach * 2f), (x, z, h) =>
            {
                Vector3 l = lInverse * new Vector3(x - pCenter.x, 0, z - pCenter.y);
                float d = RectDistance(lLocal, l.x, l.z);
                return d <= 0f ? pHeight : Mathf.Lerp(pHeight, h, Mathf.SmoothStep(0f, 1f, d / pFeather));
            });
        }

        /// <summary>A round plateau (or basin, below the land) with a soft rim.</summary>
        public LandShape Disc(Vector2 pCenter, float pRadius, float pHeight, float pFeather) => Add(Grow(new Rect(pCenter.x - pRadius, pCenter.y - pRadius, pRadius * 2f, pRadius * 2f), pFeather), (x, z, h) =>
        {
            float d = Vector2.Distance(pCenter, new Vector2(x, z)) - pRadius;
            return d <= 0f ? pHeight : Mathf.Lerp(pHeight, h, Mathf.SmoothStep(0f, 1f, d / pFeather));
        });

        /// <summary>Adds a smooth hill (positive) or hollow (negative) of the given radius.</summary>
        public LandShape Bump(Vector2 pCenter, float pRadius, float pAmount) => Add(new Rect(pCenter.x - pRadius, pCenter.y - pRadius, pRadius * 2f, pRadius * 2f), (x, z, h) =>
        {
            float t = 1f - Mathf.Clamp01(Vector2.Distance(pCenter, new Vector2(x, z)) / pRadius);
            return h + pAmount * t * t * (3f - 2f * t);
        });

        /// <summary>Quantises the land into terraces (stepped hillsides with steep risers) inside an area.</summary>
        public LandShape Terraces(Rect pArea, float pStep, float pRiser, float pFeather) => Add(Grow(pArea, pFeather), (x, z, h) =>
        {
            float lLevel = Mathf.Floor(h / pStep) * pStep, f = (h - lLevel) / pStep;
            float lStepped = lLevel + pStep * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - (1f - pRiser)) / pRiser));
            float d = RectDistance(pArea, x, z);
            return d <= 0f ? lStepped : Mathf.Lerp(lStepped, h, Mathf.SmoothStep(0f, 1f, d / pFeather));
        });

        /// <summary>Carves a channel (river, ravine, ditch) along a path: flat bed of the width, banks over the feather.</summary>
        public LandShape Carve(Vector2[] pPath, float pWidth, float pDepth, float pFeather) => Add(PathBounds(pPath, pWidth * 0.5f + pFeather), (x, z, h) =>
        {
            float d = PathDistance(pPath, new Vector2(x, z), out _, out _) - pWidth * 0.5f;
            if (d <= 0f) return h - pDepth;
            return d < pFeather ? h - pDepth * (1f - Mathf.SmoothStep(0f, 1f, d / pFeather)) : h;
        });

        /// <summary>
        /// A graded road: the height along its course is the land's (as shaped so far), smoothed
        /// over <paramref name="pWindow"/> metres, so it climbs hills as a ramp; its shoulders
        /// blend into the land.
        /// </summary>
        public LandShape Road(Vector2[] pPath, float pWidth, float pShoulder, float pWindow = 30f, float pLift = 0f)
        {
            List<Vector2> lSamples = new List<Vector2>();
            List<float> lLengths = new List<float>();
            int[] lSegStart = new int[pPath.Length], lSegSteps = new int[pPath.Length];
            float lTotal = 0f;
            for (int s = 0; s < pPath.Length - 1; s++)
            {
                int lSteps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(pPath[s], pPath[s + 1]) / 2f));
                lSegStart[s] = lSamples.Count;
                lSegSteps[s] = lSteps;
                for (int k = 0; k < lSteps; k++)
                {
                    Vector2 p = Vector2.Lerp(pPath[s], pPath[s + 1], (float)k / lSteps);
                    if (lSamples.Count > 0) lTotal += Vector2.Distance(lSamples[lSamples.Count - 1], p);
                    lSamples.Add(p);
                    lLengths.Add(lTotal);
                }
            }
            lTotal += Vector2.Distance(lSamples[lSamples.Count - 1], pPath[pPath.Length - 1]);
            lSamples.Add(pPath[pPath.Length - 1]);
            lLengths.Add(lTotal);
            float[] lRaw = new float[lSamples.Count];
            for (int k = 0; k < lRaw.Length; k++) lRaw[k] = Height(lSamples[k].x, lSamples[k].y);
            float[] lGraded = new float[lRaw.Length];
            for (int k = 0; k < lRaw.Length; k++)
            {
                float lSum = 0f; int lCount = 0;
                for (int m = 0; m < lRaw.Length; m++)
                    if (Mathf.Abs(lLengths[m] - lLengths[k]) <= pWindow * 0.5f) { lSum += lRaw[m]; lCount++; }
                lGraded[k] = lSum / lCount + pLift;
            }
            float lHalf = pWidth * 0.5f;
            return Add(PathBounds(pPath, lHalf + pShoulder), (x, z, h) =>
            {
                float d = PathDistance(pPath, new Vector2(x, z), out int lSeg, out float lT);
                if (d >= lHalf + pShoulder) return h;
                float lF = lT * lSegSteps[lSeg];
                int i0 = Mathf.Min(lSegStart[lSeg] + Mathf.FloorToInt(lF), lGraded.Length - 1), i1 = Mathf.Min(i0 + 1, lGraded.Length - 1);
                float lTarget = Mathf.Lerp(lGraded[i0], lGraded[i1], lF - Mathf.Floor(lF));
                if (d <= lHalf) return lTarget;
                return Mathf.Lerp(lTarget, h, Mathf.SmoothStep(0f, 1f, (d - lHalf) / pShoulder));
            });
        }

        public static Rect PathBounds(Vector2[] pPath, float pMargin)
        {
            Vector2 lMin = pPath[0], lMax = pPath[0];
            foreach (Vector2 p in pPath) { lMin = Vector2.Min(lMin, p); lMax = Vector2.Max(lMax, p); }
            return Rect.MinMaxRect(lMin.x - pMargin, lMin.y - pMargin, lMax.x + pMargin, lMax.y + pMargin);
        }

        /// <summary>Distance from a point to a polyline, with the segment and its parameter.</summary>
        public static float PathDistance(Vector2[] pPath, Vector2 pPoint, out int pSegment, out float pT)
        {
            float lBest = float.MaxValue; pSegment = 0; pT = 0f;
            for (int s = 0; s < pPath.Length - 1; s++)
            {
                Vector2 a = pPath[s], ab = pPath[s + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(pPoint - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                float d = Vector2.Distance(pPoint, a + ab * t);
                if (d < lBest) { lBest = d; pSegment = s; pT = t; }
            }
            return lBest;
        }

        // ---------------------------------------------------------------- noise

        /// <summary>Fractal Perlin noise in [-1, 1].</summary>
        public static float Fbm(float pX, float pZ, float pScale, int pOctaves, float pSeed)
        {
            float lSum = 0f, lAmp = 1f, lNorm = 0f, f = 1f / pScale;
            for (int o = 0; o < pOctaves; o++)
            {
                lSum += (Mathf.PerlinNoise(pX * f + pSeed + o * 17.1f, pZ * f + pSeed * 0.7f + o * 9.3f) * 2f - 1f) * lAmp;
                lNorm += lAmp;
                lAmp *= 0.5f;
                f *= 2.03f;
            }
            return lSum / lNorm;
        }

        /// <summary>Ridged noise in [0, 1]: sharp crests (mountain ridges, rocky spines).</summary>
        public static float Ridged(float pX, float pZ, float pScale, int pOctaves, float pSeed)
        {
            float lSum = 0f, lAmp = 1f, lNorm = 0f, f = 1f / pScale;
            for (int o = 0; o < pOctaves; o++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(pX * f + pSeed + o * 11.7f, pZ * f + pSeed * 1.3f + o * 5.1f) * 2f - 1f);
                lSum += n * n * lAmp;
                lNorm += lAmp;
                lAmp *= 0.5f;
                f *= 2.1f;
            }
            return lSum / lNorm;
        }
    }
}
