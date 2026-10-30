using System.IO;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Paints the stylized ground textures (tileable, hand-painted look: broad colour variation,
    /// strokes, pebbles, cracks, cells) with their normal maps, and the terrain layers that use
    /// them. Everything is generated from noise, so the textures are original and reproducible.
    /// <para>Menu <c>Goobyr/Build/Ground Textures</c>; <see cref="Layers"/> builds them on demand.</para>
    /// </summary>
    public static class TextureBuilder
    {
        public const string FOLDER = EditorAssets.ROOT + "/Art/Generated/Terrain";
        private const int SIZE = 512;

        /// <summary>The terrain layers, in the order of <see cref="MapKit.Ground"/>.</summary>
        public static readonly string[] NAMES = { "Grass", "DryGrass", "ForestFloor", "Dirt", "Rock", "Sand", "Asphalt", "Cobble" };
        /// <summary>World size of one texture tile per layer (metres).</summary>
        private static readonly float[] TILE = { 7f, 7f, 6f, 6f, 9f, 6f, 5f, 3.5f };

        [MenuItem("Goobyr/Build/Ground Textures")]
        public static void BuildMenu()
        {
            Build(true);
            Debug.Log("[Build] ground textures");
        }

        /// <summary>The terrain layers, built once (or rebuilt when asked).</summary>
        public static TerrainLayer[] Layers(bool pRebuild = false)
        {
            TerrainLayer[] lLayers = new TerrainLayer[NAMES.Length];
            bool lMissing = false;
            for (int i = 0; i < NAMES.Length; i++)
            {
                lLayers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(FOLDER + "/TL_" + NAMES[i] + ".terrainlayer");
                lMissing |= lLayers[i] == null;
            }
            return lMissing || pRebuild ? Build(pRebuild) : lLayers;
        }

        private static TerrainLayer[] Build(bool pForce)
        {
            EditorAssets.EnsureFolder(FOLDER);
            TerrainLayer[] lLayers = new TerrainLayer[NAMES.Length];
            for (int i = 0; i < NAMES.Length; i++)
            {
                string lAlbedoPath = FOLDER + "/T_" + NAMES[i] + ".png", lNormalPath = FOLDER + "/T_" + NAMES[i] + "_N.png";
                if (pForce || !File.Exists(lAlbedoPath))
                {
                    Paint(i, out Color[] lAlbedo, out float[] lHeight);
                    WritePng(lAlbedoPath, lAlbedo, false);
                    WritePng(lNormalPath, NormalsFrom(lHeight, i == 4 || i == 7 ? 6f : 3f), true);
                }
                Texture2D lAlbedoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(lAlbedoPath);
                Texture2D lNormalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(lNormalPath);
                string lLayerPath = FOLDER + "/TL_" + NAMES[i] + ".terrainlayer";
                TerrainLayer lLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lLayerPath);
                if (lLayer == null)
                {
                    lLayer = new TerrainLayer();
                    AssetDatabase.CreateAsset(lLayer, lLayerPath);
                }
                lLayer.diffuseTexture = lAlbedoTexture;
                lLayer.normalMapTexture = lNormalTexture;
                lLayer.normalScale = 1f;
                lLayer.tileSize = new Vector2(TILE[i], TILE[i]);
                lLayer.metallic = 0f;
                lLayer.smoothness = i == 6 ? 0.25f : i == 4 ? 0.15f : 0.05f;
                EditorUtility.SetDirty(lLayer);
                lLayers[i] = lLayer;
            }
            AssetDatabase.SaveAssets();
            return lLayers;
        }

        /// <summary>A tuft of grass blades with alpha, for the terrain's grass details.</summary>
        public static Texture2D GrassBlades()
        {
            string lPath = FOLDER + "/T_GrassBlades.png";
            Texture2D lExisting = AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);
            if (lExisting != null) return lExisting;
            EditorAssets.EnsureFolder(FOLDER);
            const int W = 128, H = 128;
            Color[] lPixels = new Color[W * H];
            System.Random lRandom = new System.Random(7);
            for (int b = 0; b < 26; b++)
            {
                float lBase = 10f + (float)lRandom.NextDouble() * (W - 20f), lLean = ((float)lRandom.NextDouble() - 0.5f) * 40f;
                float lTall = H * (0.55f + (float)lRandom.NextDouble() * 0.43f), lWidth = 2.2f + (float)lRandom.NextDouble() * 2.5f;
                Color lColor = Color.Lerp(new Color(0.75f, 0.85f, 0.6f), Color.white, (float)lRandom.NextDouble() * 0.5f);
                for (int y = 0; y < (int)lTall; y++)
                {
                    float t = y / lTall, cx = lBase + lLean * t * t, w = lWidth * (1f - t);
                    for (int x = Mathf.FloorToInt(cx - w); x <= Mathf.CeilToInt(cx + w); x++)
                    {
                        if (x < 0 || x >= W) continue;
                        float a = Mathf.Clamp01(w - Mathf.Abs(x - cx) + 0.5f);
                        Color c = lColor * Mathf.Lerp(0.55f, 1f, t);
                        int i = y * W + x;
                        lPixels[i] = new Color(c.r, c.g, c.b, Mathf.Max(lPixels[i].a, a));
                    }
                }
            }
            Texture2D lTexture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            lTexture.SetPixels(lPixels);
            lTexture.Apply();
            File.WriteAllBytes(lPath, lTexture.EncodeToPNG());
            Object.DestroyImmediate(lTexture);
            AssetDatabase.ImportAsset(lPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter lImporter = (TextureImporter)AssetImporter.GetAtPath(lPath);
            lImporter.alphaIsTransparency = true;
            lImporter.wrapMode = TextureWrapMode.Clamp;
            lImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);
        }

        private static void WritePng(string pPath, Color[] pPixels, bool pNormal)
        {
            Texture2D lTexture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false, pNormal);
            lTexture.SetPixels(pPixels);
            lTexture.Apply();
            File.WriteAllBytes(pPath, lTexture.EncodeToPNG());
            Object.DestroyImmediate(lTexture);
            AssetDatabase.ImportAsset(pPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter lImporter = (TextureImporter)AssetImporter.GetAtPath(pPath);
            lImporter.textureType = pNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            lImporter.sRGBTexture = !pNormal;
            lImporter.wrapMode = TextureWrapMode.Repeat;
            lImporter.anisoLevel = 4;
            lImporter.maxTextureSize = SIZE;
            lImporter.SaveAndReimport();
        }

        // ---------------------------------------------------------------- painting

        private static void Paint(int pLayer, out Color[] pAlbedo, out float[] pHeight)
        {
            pAlbedo = new Color[SIZE * SIZE];
            pHeight = new float[SIZE * SIZE];
            int lSeed = 101 + pLayer * 37;
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float u = (float)x / SIZE, v = (float)y / SIZE;
                    float lBroad = Fbm(u, v, 3, 4, lSeed), lFine = Fbm(u, v, 24, 3, lSeed + 7), lMid = Fbm(u, v, 8, 3, lSeed + 3);
                    Color c; float h;
                    switch (pLayer)
                    {
                        case 0: // Grass: broad hue shifts, darker clumps, light blade strokes.
                        {
                            float lBlade = Strokes(u, v, 48, lSeed);
                            c = Color.Lerp(new Color(0.24f, 0.42f, 0.16f), new Color(0.42f, 0.6f, 0.22f), lBroad);
                            c = Color.Lerp(c, new Color(0.16f, 0.3f, 0.12f), Mathf.Clamp01((0.45f - lMid) * 2.5f));
                            c += new Color(0.08f, 0.1f, 0.03f) * lBlade + new Color(0.03f, 0.03f, 0.01f) * (lFine - 0.5f);
                            h = lMid * 0.4f + lBlade * 0.6f;
                            break;
                        }
                        case 1: // Dry grass: straw and olive.
                        {
                            float lBlade = Strokes(u, v, 40, lSeed);
                            c = Color.Lerp(new Color(0.55f, 0.5f, 0.28f), new Color(0.72f, 0.64f, 0.36f), lBroad);
                            c = Color.Lerp(c, new Color(0.4f, 0.42f, 0.22f), Mathf.Clamp01((0.42f - lMid) * 2f));
                            c += new Color(0.1f, 0.09f, 0.05f) * lBlade;
                            h = lMid * 0.4f + lBlade * 0.6f;
                            break;
                        }
                        case 2: // Forest floor: needles, leaves, moss patches.
                        {
                            float lLeaves = Cells(u, v, 22, lSeed, out float lEdge);
                            c = Color.Lerp(new Color(0.28f, 0.22f, 0.14f), new Color(0.42f, 0.32f, 0.18f), lBroad);
                            c = Color.Lerp(c, new Color(0.25f, 0.36f, 0.16f), Mathf.Clamp01((lMid - 0.55f) * 3f));
                            c = Color.Lerp(c, new Color(0.55f, 0.36f, 0.18f), Mathf.Clamp01(lLeaves - 0.7f) * 2f);
                            c *= 0.85f + 0.3f * lFine;
                            h = lMid * 0.5f + (1f - lEdge) * 0.3f + lFine * 0.2f;
                            break;
                        }
                        case 3: // Dirt: earth with pebbles.
                        {
                            float lPebble = Cells(u, v, 30, lSeed, out float lEdge);
                            c = Color.Lerp(new Color(0.38f, 0.28f, 0.19f), new Color(0.52f, 0.4f, 0.27f), lBroad);
                            float lStone = Mathf.Clamp01((lPebble - 0.82f) * 6f) * Mathf.Clamp01(lEdge * 4f);
                            c = Color.Lerp(c, new Color(0.6f, 0.56f, 0.5f), lStone);
                            c *= 0.9f + 0.2f * lFine;
                            h = lMid * 0.5f + lStone * 0.5f;
                            break;
                        }
                        case 4: // Rock: strata bands and cracks.
                        {
                            Cells(u, v, 6, lSeed, out float lCrack);
                            float lStrata = Mathf.Sin((v * 9f + lBroad * 2.4f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                            c = Color.Lerp(new Color(0.42f, 0.41f, 0.4f), new Color(0.62f, 0.6f, 0.56f), lBroad * 0.6f + lStrata * 0.4f);
                            float lDark = 1f - Mathf.Clamp01(lCrack * 9f);
                            c = Color.Lerp(c, new Color(0.22f, 0.21f, 0.21f), lDark * 0.85f);
                            c *= 0.88f + 0.24f * lFine;
                            h = lStrata * 0.3f + lMid * 0.4f + (1f - lDark) * 0.3f;
                            break;
                        }
                        case 5: // Sand: ripples.
                        {
                            float lRipple = Mathf.Sin((v * 26f + lBroad * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                            c = Color.Lerp(new Color(0.78f, 0.68f, 0.48f), new Color(0.9f, 0.82f, 0.6f), lBroad * 0.7f + lRipple * 0.3f);
                            c *= 0.94f + 0.12f * lFine;
                            h = lRipple * 0.5f + lFine * 0.5f;
                            break;
                        }
                        case 6: // Asphalt: speckled, patched.
                        {
                            c = Color.Lerp(new Color(0.18f, 0.18f, 0.2f), new Color(0.27f, 0.27f, 0.29f), lBroad);
                            float lSpeck = Hash(x, y, lSeed) > 0.93f ? 1f : 0f;
                            c += new Color(0.1f, 0.1f, 0.1f) * lSpeck + new Color(0.03f, 0.03f, 0.03f) * (lFine - 0.5f);
                            c = Color.Lerp(c, new Color(0.13f, 0.13f, 0.15f), Mathf.Clamp01((lMid - 0.68f) * 5f));
                            h = lFine * 0.6f + lSpeck * 0.4f;
                            break;
                        }
                        default: // Cobble: rounded stones in mortar.
                        {
                            float lId = Cells(u, v, 10, lSeed, out float lEdge);
                            float lStone = Mathf.Clamp01(lEdge * 8f);
                            c = Color.Lerp(new Color(0.48f, 0.45f, 0.42f), new Color(0.66f, 0.62f, 0.56f), lId * 0.7f + lBroad * 0.3f);
                            c = Color.Lerp(new Color(0.24f, 0.23f, 0.22f), c, lStone);
                            c *= 0.9f + 0.2f * lFine;
                            h = Mathf.Sqrt(lStone) * 0.8f + lFine * 0.2f;
                            break;
                        }
                    }
                    c.a = 1f;
                    pAlbedo[y * SIZE + x] = c;
                    pHeight[y * SIZE + x] = h;
                }
        }

        private static Color[] NormalsFrom(float[] pHeight, float pStrength)
        {
            Color[] lNormals = new Color[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float l = pHeight[y * SIZE + (x + SIZE - 1) % SIZE], r = pHeight[y * SIZE + (x + 1) % SIZE];
                    float d = pHeight[((y + SIZE - 1) % SIZE) * SIZE + x], u = pHeight[((y + 1) % SIZE) * SIZE + x];
                    Vector3 n = new Vector3((l - r) * pStrength, (d - u) * pStrength, 1f).normalized;
                    lNormals[y * SIZE + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            return lNormals;
        }

        // ---------------------------------------------------------------- tileable noise

        private static float Hash(int pX, int pY, int pSeed)
        {
            unchecked
            {
                uint h = (uint)(pX * 374761393 + pY * 668265263 + pSeed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        /// <summary>Value noise on a grid of <paramref name="pPeriod"/> cells that wraps at 1.</summary>
        private static float Value(float pU, float pV, int pPeriod, int pSeed)
        {
            float x = pU * pPeriod, y = pV * pPeriod;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wrap(int i) => ((i % pPeriod) + pPeriod) % pPeriod;
            float a = Hash(Wrap(x0), Wrap(y0), pSeed), b = Hash(Wrap(x0 + 1), Wrap(y0), pSeed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), pSeed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), pSeed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float pU, float pV, int pBase, int pOctaves, int pSeed)
        {
            float lSum = 0f, lAmp = 0.5f, lNorm = 0f;
            for (int o = 0; o < pOctaves; o++)
            {
                lSum += Value(pU, pV, pBase << o, pSeed + o * 13) * lAmp;
                lNorm += lAmp;
                lAmp *= 0.5f;
            }
            return lSum / lNorm;
        }

        /// <summary>Tileable Voronoi: returns the cell's random id, and the F2 - F1 edge distance.</summary>
        private static float Cells(float pU, float pV, int pPeriod, int pSeed, out float pEdge)
        {
            float x = pU * pPeriod, y = pV * pPeriod;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 9f, f2 = 9f, lId = 0f;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    int wx = ((gx % pPeriod) + pPeriod) % pPeriod, wy = ((gy % pPeriod) + pPeriod) % pPeriod;
                    float px = gx + Hash(wx, wy, pSeed), py = gy + Hash(wx, wy, pSeed + 1);
                    float d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < f1) { f2 = f1; f1 = d; lId = Hash(wx, wy, pSeed + 2); }
                    else if (d < f2) f2 = d;
                }
            pEdge = Mathf.Sqrt(f2) - Mathf.Sqrt(f1);
            return lId;
        }

        /// <summary>Short vertical strokes (blades) scattered on a wrapping grid.</summary>
        private static float Strokes(float pU, float pV, int pPeriod, int pSeed)
        {
            float x = pU * pPeriod, y = pV * pPeriod * 0.5f;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float lBest = 0f;
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, wx = ((gx % pPeriod) + pPeriod) % pPeriod, wy = ((cy % (pPeriod / 2)) + pPeriod / 2) % (pPeriod / 2);
                float sx = gx + Hash(wx, wy, pSeed), sy = cy + Hash(wx, wy, pSeed + 5);
                float lAcross = Mathf.Abs(x - sx), lAlong = (y - sy) * 2f;
                if (lAcross < 0.12f && lAlong > -0.9f && lAlong < 0.9f) lBest = Mathf.Max(lBest, (1f - lAcross / 0.12f) * (1f - Mathf.Abs(lAlong) / 0.9f));
            }
            return lBest;
        }
    }
}
