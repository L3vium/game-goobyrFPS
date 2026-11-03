using System.IO;
using Com.IsartDigital.GoobyrFPS.World;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Bakes a terrain's real grass (<see cref="GrassField"/>): a 1024² map over the terrain, saved as
    /// <c>Art/Generated/Grass/&lt;name&gt;_Grass.png</c> (R density, G height, B dryness, A tall grass), the
    /// lowest and highest ground of every 16 m chunk (empty chunks marked), and the field component on
    /// the terrain with its shaders. The density function says where grass grows (0 on roads, building
    /// lots, water and bare rock: the same masks the terrain details used).
    /// </summary>
    public static class GrassBaker
    {
        public const string FOLDER = EditorAssets.ROOT + "/Art/Generated/Grass";
        public const int RESOLUTION = 1024;
        public const float CHUNK = 16f;

        /// <summary>
        /// Bakes and installs the grass of a terrain. Functions take a world point (x, z) and return 0..1:
        /// density; height (0.5 = the style's height, null = a gentle noise); dryness (null = noise); tall
        /// grass to hide in (null = none).
        /// </summary>
        public static GrassField Bake(Terrain pTerrain, string pName, GrassStyle pStyle, System.Func<float, float, float> pDensity, System.Func<float, float, float> pHeight = null, System.Func<float, float, float> pDry = null, System.Func<float, float, float> pTall = null)
        {
            TerrainData lData = pTerrain.terrainData;
            Vector3 o = pTerrain.transform.position, lSize = lData.size;
            pHeight ??= (x, z) => Mathf.Clamp01(0.5f + 0.35f * LandShape.Fbm(x, z, 9f, 2, 31.7f));
            pDry ??= (x, z) => Mathf.Clamp01(0.25f + 0.6f * LandShape.Fbm(x, z, 24f, 2, 12.9f));

            Color32[] lPixels = new Color32[RESOLUTION * RESOLUTION];
            for (int j = 0; j < RESOLUTION; j++)
                for (int i = 0; i < RESOLUTION; i++)
                {
                    float x = o.x + (i + 0.5f) / RESOLUTION * lSize.x, z = o.z + (j + 0.5f) / RESOLUTION * lSize.z;
                    float d = Mathf.Clamp01(pDensity(x, z));
                    lPixels[j * RESOLUTION + i] = d <= 0f
                        ? new Color32(0, 0, 0, 0)
                        : new Color32(Byte(d), Byte(pHeight(x, z)), Byte(pDry(x, z)), Byte(pTall != null ? pTall(x, z) : 0f));
                }
            EditorAssets.EnsureFolder(FOLDER);
            string lPath = $"{FOLDER}/{pName}_Grass.png";
            Texture2D lImage = new Texture2D(RESOLUTION, RESOLUTION, TextureFormat.RGBA32, false, true);
            lImage.SetPixels32(lPixels);
            File.WriteAllBytes(lPath, lImage.EncodeToPNG());
            Object.DestroyImmediate(lImage);
            AssetDatabase.ImportAsset(lPath, ImportAssetOptions.ForceUpdate);
            TextureImporter lImporter = (TextureImporter)AssetImporter.GetAtPath(lPath);
            lImporter.textureType = TextureImporterType.Default;
            lImporter.sRGBTexture = false;
            lImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            lImporter.alphaIsTransparency = false;
            // Mipmaps for the terrain shader, which reads the map from afar; the cull compute reads level 0.
            lImporter.mipmapEnabled = true;
            lImporter.wrapMode = TextureWrapMode.Clamp;
            lImporter.filterMode = FilterMode.Bilinear;
            lImporter.maxTextureSize = RESOLUTION;
            lImporter.textureCompression = TextureImporterCompression.Uncompressed;
            lImporter.isReadable = false;
            lImporter.SaveAndReimport();
            Texture2D lMap = AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);

            // Chunks: the ground's height range where anything grows (x > y marks an empty chunk).
            int lChunks = Mathf.Max(1, Mathf.CeilToInt(lSize.x / CHUNK - 0.01f));
            float lChunk = lSize.x / lChunks;
            int lRes = lData.heightmapResolution;
            float[,] lHeights = lData.GetHeights(0, 0, lRes, lRes);
            Vector2[] lRanges = new Vector2[lChunks * lChunks];
            int lFull = 0;
            for (int cz = 0; cz < lChunks; cz++)
                for (int cx = 0; cx < lChunks; cx++)
                {
                    bool lAny = false;
                    int i0 = Mathf.FloorToInt(cx * lChunk / lSize.x * RESOLUTION), i1 = Mathf.Min(RESOLUTION - 1, Mathf.CeilToInt((cx + 1) * lChunk / lSize.x * RESOLUTION));
                    int j0 = Mathf.FloorToInt(cz * lChunk / lSize.z * RESOLUTION), j1 = Mathf.Min(RESOLUTION - 1, Mathf.CeilToInt((cz + 1) * lChunk / lSize.z * RESOLUTION));
                    for (int j = j0; j <= j1 && !lAny; j++)
                        for (int i = i0; i <= i1; i++)
                            if (lPixels[j * RESOLUTION + i].r > 2) { lAny = true; break; }
                    if (!lAny) { lRanges[cz * lChunks + cx] = new Vector2(1f, -1f); continue; }
                    float lMin = float.MaxValue, lMax = float.MinValue;
                    int h0 = Mathf.FloorToInt(cx * lChunk / lSize.x * (lRes - 1)), h1 = Mathf.Min(lRes - 1, Mathf.CeilToInt((cx + 1) * lChunk / lSize.x * (lRes - 1)));
                    int v0 = Mathf.FloorToInt(cz * lChunk / lSize.z * (lRes - 1)), v1 = Mathf.Min(lRes - 1, Mathf.CeilToInt((cz + 1) * lChunk / lSize.z * (lRes - 1)));
                    for (int v = v0; v <= v1; v++)
                        for (int h = h0; h <= h1; h++)
                        {
                            float y = o.y + lHeights[v, h] * lSize.y;
                            lMin = Mathf.Min(lMin, y);
                            lMax = Mathf.Max(lMax, y);
                        }
                    lRanges[cz * lChunks + cx] = new Vector2(lMin, lMax);
                    lFull++;
                }

            GrassField lField = pTerrain.GetComponent<GrassField>();
            if (lField == null) lField = pTerrain.gameObject.AddComponent<GrassField>();
            lField.Setup(pTerrain, lMap, pStyle, lChunk, lChunks, lRanges,
                AssetDatabase.LoadAssetAtPath<Shader>(EditorAssets.ROOT + "/Art/Shaders/Grass.shader"),
                AssetDatabase.LoadAssetAtPath<ComputeShader>(EditorAssets.ROOT + "/Art/Shaders/GrassCull.compute"),
                AssetDatabase.LoadAssetAtPath<ComputeShader>(EditorAssets.ROOT + "/Art/Shaders/GrassTrample.compute"));
            EditorUtility.SetDirty(lField);
            Debug.Log($"[Build] {pName} grass: {lFull}/{lRanges.Length} chunks of {lChunk:F1} m with grass");
            return lField;
        }

        private static byte Byte(float pValue) => (byte)Mathf.RoundToInt(Mathf.Clamp01(pValue) * 255f);

        /// <summary>
        /// Migration for maps baked while the grass's far colour was baked into tinted copies of their turf
        /// layers (<c>&lt;map&gt;_Turf_&lt;Ground&gt;.terrainlayer</c>, before the terrain shader painted the
        /// meadow itself): each copy gets its source layer's colour back (the map's own copy of the layer,
        /// else the shared one), and every grass map gets mipmaps. Idempotent; map builds no longer make
        /// turf copies, so the next rebuild drops them.
        /// </summary>
        public static string Untint()
        {
            System.Text.StringBuilder lReport = new System.Text.StringBuilder();
            string[] lAll = AssetDatabase.FindAssets("t:TerrainLayer");
            foreach (string lGuid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { FOLDER }))
            {
                TerrainLayer lTurf = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(lGuid));
                int lCut = lTurf != null ? lTurf.name.IndexOf("_Turf_", System.StringComparison.Ordinal) : -1;
                if (lCut <= 0) continue;
                string lMap = lTurf.name.Substring(0, lCut);
                TerrainLayer lSource = null;
                foreach (string g in lAll)
                {
                    string lPath = AssetDatabase.GUIDToAssetPath(g);
                    if (lPath.StartsWith(FOLDER)) continue;
                    TerrainLayer l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lPath);
                    if (l == null || l.diffuseTexture != lTurf.diffuseTexture || l.tileSize != lTurf.tileSize) continue;
                    bool lOwn = l.name.EndsWith("_" + lMap) || l.name.StartsWith(lMap + "_");
                    if (lSource == null || lOwn) lSource = l;
                    if (lOwn) break;
                }
                Vector4 lMin = lSource != null ? lSource.diffuseRemapMin : Vector4.zero, lMax = lSource != null ? lSource.diffuseRemapMax : Vector4.one;
                if (lTurf.diffuseRemapMin != lMin || lTurf.diffuseRemapMax != lMax)
                {
                    lTurf.diffuseRemapMin = lMin;
                    lTurf.diffuseRemapMax = lMax;
                    EditorUtility.SetDirty(lTurf);
                    AssetDatabase.SaveAssetIfDirty(lTurf);
                }
                lReport.Append(lTurf.name + " <- " + (lSource != null ? lSource.name : "white") + " " + lMax + "\n");
            }
            foreach (string lGuid in AssetDatabase.FindAssets("t:Texture2D", new[] { FOLDER }))
            {
                string lPath = AssetDatabase.GUIDToAssetPath(lGuid);
                if (!Path.GetFileNameWithoutExtension(lPath).EndsWith("_Grass")) continue;
                TextureImporter lImporter = AssetImporter.GetAtPath(lPath) as TextureImporter;
                if (lImporter == null || lImporter.mipmapEnabled) continue;
                lImporter.mipmapEnabled = true;
                lImporter.SaveAndReimport();
                lReport.Append(lPath + ": mipmaps\n");
            }
            return lReport.ToString();
        }
    }
}
