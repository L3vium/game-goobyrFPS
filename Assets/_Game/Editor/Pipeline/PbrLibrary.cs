using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Turns the downloaded PBR sources (Poly Haven CC0 sets, the Blender exports) into URP Lit
    /// materials, terrain layers and model material remaps.
    /// <para>
    /// Sources follow one naming rule: <c>&lt;name&gt;_diff</c> (base colour), <c>&lt;name&gt;_nor_gl</c>
    /// (OpenGL normal), <c>&lt;name&gt;_arm</c> (R ambient occlusion, G roughness, B metallic), optional
    /// <c>&lt;name&gt;_alpha</c>, each with an optional resolution suffix (<c>_2k</c>). The ARM map is
    /// repacked into URP's metallic map (R metal, G occlusion, A smoothness) once, as
    /// <c>&lt;name&gt;_ms.png</c>, and shared with the terrain layer's mask map.
    /// </para>
    /// <list type="bullet">
    /// <item><c>Art/PBR/Textures/&lt;id&gt;/</c> → <c>Art/PBR/Materials/M_&lt;id&gt;.mat</c> + <c>TL_&lt;id&gt;.terrainlayer</c>.</item>
    /// <item><c>Art/PBR/Models/&lt;asset&gt;/&lt;asset&gt;.fbx</c> → one material per slot (textures prefixed by
    /// the slot name), remapped on the model; leaves and cards are alpha-tested and two-sided, glass
    /// is transparent.</item>
    /// </list>
    /// </summary>
    public static class PbrLibrary
    {
        public const string ROOT = EditorAssets.ROOT + "/Art/PBR";
        public const string MATERIALS = ROOT + "/Materials";
        private static readonly string[] MAPS = { "diff", "nor_gl", "arm", "alpha" };

        [MenuItem("Goobyr/Build/PBR Materials")]
        public static void Build()
        {
            EditorAssets.EnsureFolder(MATERIALS);
            int lTextures = 0, lModels = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string lDir in SubFolders(ROOT + "/Textures")) PrepareTextures(lDir);
                foreach (string lDir in SubFolders(ROOT + "/Models")) PrepareTextures(lDir);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();

            foreach (string lDir in SubFolders(ROOT + "/Textures"))
            {
                string lId = Path.GetFileName(lDir);
                if (SurfaceMaterial(lDir, lId) != null) lTextures++;
            }
            foreach (string lDir in SubFolders(ROOT + "/Models"))
                if (RemapModel(lDir)) lModels++;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] PBR: {lTextures} surface sets, {lModels} models");
        }

        // ---------------------------------------------------------------- lookups for the builders

        /// <summary>The URP Lit material of a surface set (<c>Art/PBR/Textures/&lt;id&gt;</c>), built if needed. Null if the set is missing.</summary>
        public static Material Surface(string pId)
        {
            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIALS}/M_{pId}.mat");
            if (lMaterial != null) return lMaterial;
            string lDir = $"{ROOT}/Textures/{pId}";
            if (!AssetDatabase.IsValidFolder(lDir)) { Debug.LogWarning($"[PBR] missing surface set {pId}"); return null; }
            PrepareTextures(lDir);
            return SurfaceMaterial(lDir, pId);
        }

        /// <summary>A tinted copy of a surface (painted plaster in another colour), cached as an asset.</summary>
        public static Material Tinted(string pId, Color pTint, string pSuffix)
        {
            string lPath = $"{MATERIALS}/M_{pId}_{pSuffix}.mat";
            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lPath);
            Material lBase = Surface(pId);
            if (lBase == null) return null;
            if (lMaterial == null)
            {
                lMaterial = new Material(lBase);
                AssetDatabase.CreateAsset(lMaterial, lPath);
            }
            else lMaterial.CopyPropertiesFromMaterial(lBase);
            lMaterial.SetColor("_BaseColor", pTint);
            EditorUtility.SetDirty(lMaterial);
            return lMaterial;
        }

        /// <summary>Terrain layer of a surface set, tiled every <paramref name="pTile"/> metres.</summary>
        public static TerrainLayer Layer(string pId, float pTile)
        {
            string lPath = $"{MATERIALS}/TL_{pId}.terrainlayer";
            Material lSurface = Surface(pId);
            if (lSurface == null) return null;
            TerrainLayer lLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lPath);
            if (lLayer == null)
            {
                lLayer = new TerrainLayer();
                AssetDatabase.CreateAsset(lLayer, lPath);
            }
            lLayer.diffuseTexture = lSurface.GetTexture("_BaseMap") as Texture2D;
            lLayer.normalMapTexture = lSurface.GetTexture("_BumpMap") as Texture2D;
            lLayer.maskMapTexture = lSurface.GetTexture("_MetallicGlossMap") as Texture2D;
            lLayer.normalScale = 1f;
            lLayer.tileSize = new Vector2(pTile, pTile);
            lLayer.smoothness = 1f;
            lLayer.metallic = 0f;
            // Mask map packs metal, occlusion and smoothness; no height (B = 0).
            lLayer.maskMapRemapMin = Vector4.zero;
            lLayer.maskMapRemapMax = Vector4.one;
            EditorUtility.SetDirty(lLayer);
            return lLayer;
        }

        /// <summary>A model prefab-ready asset (<c>Art/PBR/Models/&lt;asset&gt;/&lt;asset&gt;.fbx</c>), remapped to PBR materials. Null if missing.</summary>
        public static GameObject Model(string pAsset)
        {
            string lPath = $"{ROOT}/Models/{pAsset}/{pAsset}.fbx";
            GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>(lPath);
            if (lModel == null) Debug.LogWarning($"[PBR] missing model {pAsset}");
            return lModel;
        }

        public static bool HasModel(string pAsset) => File.Exists($"{ROOT}/Models/{pAsset}/{pAsset}.fbx");
        public static bool HasSurface(string pId) => AssetDatabase.IsValidFolder($"{ROOT}/Textures/{pId}");

        // ---------------------------------------------------------------- textures

        /// <summary>Importer settings and packed metallic maps of one folder (the character builder's entry point).</summary>
        public static void PrepareFolder(string pDir) => PrepareTextures(pDir);

        private static IEnumerable<string> SubFolders(string pPath)
        {
            if (!AssetDatabase.IsValidFolder(pPath)) return Enumerable.Empty<string>();
            return AssetDatabase.GetSubFolders(pPath);
        }

        /// <summary>Texture of a map for a prefix in a folder (<c>prefix_map</c> or <c>prefix_map_2k</c>), or null.</summary>
        private static string Find(string pDir, string pPrefix, string pMap)
        {
            foreach (string lFile in Directory.GetFiles(pDir))
            {
                string lName = Path.GetFileNameWithoutExtension(lFile);
                string lExt = Path.GetExtension(lFile).ToLowerInvariant();
                if (lExt != ".png" && lExt != ".jpg" && lExt != ".jpeg" && lExt != ".tga") continue;
                if (!lName.StartsWith(pPrefix + "_", System.StringComparison.OrdinalIgnoreCase)) continue;
                string lRest = lName.Substring(pPrefix.Length + 1);
                if (lRest.Equals(pMap, System.StringComparison.OrdinalIgnoreCase)) return Unix(lFile);
                if (lRest.StartsWith(pMap + "_", System.StringComparison.OrdinalIgnoreCase) && char.IsDigit(lRest[pMap.Length + 1])) return Unix(lFile);
            }
            return null;
        }

        private static string Unix(string pPath) => pPath.Replace('\\', '/');

        /// <summary>Importer settings per map, and the packed metallic map (and combined colour + alpha) for every prefix found.</summary>
        private static void PrepareTextures(string pDir)
        {
            foreach (string lFile in Directory.GetFiles(pDir))
            {
                string lPath = Unix(lFile);
                TextureImporter lImporter = AssetImporter.GetAtPath(lPath) as TextureImporter;
                if (lImporter == null) continue;
                string lName = Path.GetFileNameWithoutExtension(lPath).ToLowerInvariant();
                bool lChanged = false;
                bool lNormal = lName.Contains("_nor_gl");
                bool lLinear = lNormal || lName.Contains("_arm") || lName.EndsWith("_ms") || lName.Contains("_alpha");
                if (lNormal && lImporter.textureType != TextureImporterType.NormalMap) { lImporter.textureType = TextureImporterType.NormalMap; lChanged = true; }
                if (lImporter.sRGBTexture == lLinear) { lImporter.sRGBTexture = !lLinear; lChanged = true; }
                if (lImporter.maxTextureSize != 2048) { lImporter.maxTextureSize = 2048; lChanged = true; }
                if (lImporter.anisoLevel != 8) { lImporter.anisoLevel = 8; lChanged = true; }
                if (lName.Contains("_diffa") && !lImporter.alphaIsTransparency) { lImporter.alphaIsTransparency = true; lImporter.mipMapsPreserveCoverage = true; lImporter.alphaTestReferenceValue = 0.45f; lChanged = true; }
                if (lChanged) lImporter.SaveAndReimport();
            }

            // Every prefix that has an ARM map gets its packed metallic map.
            foreach (string lFile in Directory.GetFiles(pDir))
            {
                string lName = Path.GetFileNameWithoutExtension(lFile);
                int lIndex = lName.IndexOf("_arm", System.StringComparison.OrdinalIgnoreCase);
                if (lIndex <= 0) continue;
                string lPrefix = lName.Substring(0, lIndex);
                string lOut = $"{Unix(pDir)}/{lPrefix}_ms.png";
                if (File.Exists(lOut)) continue;
                PackMetallic(Unix(lFile), lOut);
            }

            // Separate alpha masks (leaf cards) merge into the colour: <prefix>_diffa.png.
            foreach (string lFile in Directory.GetFiles(pDir))
            {
                string lName = Path.GetFileNameWithoutExtension(lFile);
                int lIndex = lName.IndexOf("_alpha", System.StringComparison.OrdinalIgnoreCase);
                if (lIndex <= 0 || Path.GetExtension(lFile).ToLowerInvariant() == ".meta") continue;
                string lPrefix = lName.Substring(0, lIndex);
                string lDiff = Find(pDir, lPrefix, "diff");
                string lOut = $"{Unix(pDir)}/{lPrefix}_diffa.png";
                if (lDiff == null || File.Exists(lOut)) continue;
                MergeAlpha(lDiff, Unix(lFile), lOut);
            }
        }

        private static Texture2D Read(string pPath)
        {
            Texture2D lTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            lTexture.LoadImage(File.ReadAllBytes(pPath));
            return lTexture;
        }

        private static void PackMetallic(string pArm, string pOut)
        {
            Texture2D lArm = Read(pArm);
            Color32[] lPixels = lArm.GetPixels32();
            for (int i = 0; i < lPixels.Length; i++)
            {
                Color32 c = lPixels[i];
                lPixels[i] = new Color32(c.b, c.r, 0, (byte)(255 - c.g));
            }
            Texture2D lOut = new Texture2D(lArm.width, lArm.height, TextureFormat.RGBA32, false, true);
            lOut.SetPixels32(lPixels);
            File.WriteAllBytes(pOut, lOut.EncodeToPNG());
            Object.DestroyImmediate(lArm);
            Object.DestroyImmediate(lOut);
            AssetDatabase.ImportAsset(pOut);
            TextureImporter lImporter = AssetImporter.GetAtPath(pOut) as TextureImporter;
            if (lImporter != null) { lImporter.sRGBTexture = false; lImporter.maxTextureSize = 2048; lImporter.SaveAndReimport(); }
        }

        private static void MergeAlpha(string pDiff, string pAlpha, string pOut)
        {
            Texture2D lDiff = Read(pDiff);
            Texture2D lAlpha = Read(pAlpha);
            if (lAlpha.width != lDiff.width || lAlpha.height != lDiff.height)
            {
                RenderTexture lRt = RenderTexture.GetTemporary(lDiff.width, lDiff.height);
                Graphics.Blit(lAlpha, lRt);
                RenderTexture.active = lRt;
                lAlpha.Reinitialize(lDiff.width, lDiff.height);
                lAlpha.ReadPixels(new Rect(0, 0, lDiff.width, lDiff.height), 0, 0);
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(lRt);
            }
            Color32[] lColors = lDiff.GetPixels32();
            Color32[] lMask = lAlpha.GetPixels32();
            for (int i = 0; i < lColors.Length; i++) lColors[i].a = lMask[i].r;
            lDiff.SetPixels32(lColors);
            File.WriteAllBytes(pOut, lDiff.EncodeToPNG());
            Object.DestroyImmediate(lDiff);
            Object.DestroyImmediate(lAlpha);
            AssetDatabase.ImportAsset(pOut);
            TextureImporter lImporter = AssetImporter.GetAtPath(pOut) as TextureImporter;
            if (lImporter != null) { lImporter.alphaIsTransparency = true; lImporter.mipMapsPreserveCoverage = true; lImporter.alphaTestReferenceValue = 0.45f; lImporter.maxTextureSize = 2048; lImporter.SaveAndReimport(); }
        }

        // ---------------------------------------------------------------- materials

        public enum Kind { Opaque, Cutout, Glass }

        private static Material SurfaceMaterial(string pDir, string pId) => MakeMaterial(pDir, pId, $"{MATERIALS}/M_{pId}.mat", Kind.Opaque);

        /// <summary>Builds (or refreshes) a URP Lit material from the maps of <paramref name="pPrefix"/> in a folder.</summary>
        public static Material MakeMaterial(string pDir, string pPrefix, string pPath, Kind pKind)
        {
            string lDiffa = Find(pDir, pPrefix, "diffa");
            string lDiff = lDiffa ?? Find(pDir, pPrefix, "diff");
            string lNormal = Find(pDir, pPrefix, "nor_gl");
            string lMetal = Find(pDir, pPrefix, "ms");
            if (lDiff == null && lNormal == null && pKind != Kind.Glass) return null;

            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(pPath);
            Shader lLit = Shader.Find("Universal Render Pipeline/Lit");
            if (lMaterial == null)
            {
                lMaterial = new Material(lLit);
                AssetDatabase.CreateAsset(lMaterial, pPath);
            }
            else lMaterial.shader = lLit;

            if (pKind == Kind.Opaque && lDiffa != null) pKind = Kind.Cutout;
            Texture2D lBase = lDiff != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(lDiff) : null;
            if (pKind == Kind.Opaque && lBase != null && lDiff.EndsWith(".png") && AssetImporter.GetAtPath(lDiff) is TextureImporter lPng && lPng.DoesSourceTextureHaveAlpha() && LooksLikeCard(pPrefix)) pKind = Kind.Cutout;

            lMaterial.SetTexture("_BaseMap", lBase);
            lMaterial.SetColor("_BaseColor", Color.white);
            if (lNormal != null)
            {
                lMaterial.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(lNormal));
                lMaterial.SetFloat("_BumpScale", 1f);
                lMaterial.EnableKeyword("_NORMALMAP");
            }
            if (lMetal != null)
            {
                Texture2D lMs = AssetDatabase.LoadAssetAtPath<Texture2D>(lMetal);
                lMaterial.SetTexture("_MetallicGlossMap", lMs);
                lMaterial.SetTexture("_OcclusionMap", lMs);
                lMaterial.SetFloat("_OcclusionStrength", 1f);
                lMaterial.SetFloat("_Smoothness", 1f);
                lMaterial.SetFloat("_SmoothnessTextureChannel", 0f);
                lMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
                lMaterial.EnableKeyword("_OCCLUSIONMAP");
            }
            else
            {
                lMaterial.SetFloat("_Metallic", 0f);
                lMaterial.SetFloat("_Smoothness", pKind == Kind.Glass ? 0.95f : 0.25f);
            }
            ApplyKind(lMaterial, pKind);
            lMaterial.enableInstancing = true;
            EditorUtility.SetDirty(lMaterial);
            return lMaterial;
        }

        private static bool LooksLikeCard(string pName)
        {
            string l = pName.ToLowerInvariant();
            return l.Contains("leaf") || l.Contains("leaves") || l.Contains("twig") || l.Contains("frond") || l.Contains("grass") || l.Contains("card") || l.Contains("fern") || l.Contains("foliage") || l.Contains("needle");
        }

        private static void ApplyKind(Material pMaterial, Kind pKind)
        {
            pMaterial.SetFloat("_Surface", pKind == Kind.Glass ? 1f : 0f);
            pMaterial.SetFloat("_AlphaClip", pKind == Kind.Cutout ? 1f : 0f);
            pMaterial.SetFloat("_Cutoff", 0.45f);
            pMaterial.SetFloat("_Cull", pKind == Kind.Opaque ? 2f : 0f);
            pMaterial.SetFloat("_Blend", 0f);
            if (pKind == Kind.Cutout) pMaterial.EnableKeyword("_ALPHATEST_ON"); else pMaterial.DisableKeyword("_ALPHATEST_ON");
            if (pKind == Kind.Glass)
            {
                pMaterial.SetOverrideTag("RenderType", "Transparent");
                pMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                pMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                pMaterial.SetFloat("_ZWrite", 0f);
                pMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                pMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                Color lColor = pMaterial.GetColor("_BaseColor");
                pMaterial.SetColor("_BaseColor", new Color(lColor.r * 0.8f, lColor.g * 0.9f, lColor.b * 0.95f, 0.28f));
            }
            else
            {
                pMaterial.SetOverrideTag("RenderType", pKind == Kind.Cutout ? "TransparentCutout" : "Opaque");
                pMaterial.SetFloat("_SrcBlend", 1f);
                pMaterial.SetFloat("_DstBlend", 0f);
                pMaterial.SetFloat("_ZWrite", 1f);
                pMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                pMaterial.renderQueue = pKind == Kind.Cutout ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            }
            pMaterial.doubleSidedGI = pKind != Kind.Opaque;
        }

        /// <summary>Materials for every slot of a model, remapped on its importer. True if the model exists.</summary>
        private static bool RemapModel(string pDir)
        {
            string lAsset = Path.GetFileName(pDir);
            string lFbx = $"{Unix(pDir)}/{lAsset}.fbx";
            if (!File.Exists(lFbx)) lFbx = Directory.GetFiles(pDir, "*.fbx").Select(Unix).FirstOrDefault();
            if (lFbx == null) return false;
            ModelImporter lImporter = AssetImporter.GetAtPath(lFbx) as ModelImporter;
            if (lImporter == null) return false;

            HashSet<string> lSlots = new HashSet<string>();
            foreach (Object lSub in AssetDatabase.LoadAllAssetsAtPath(lFbx))
                if (lSub is Renderer lRenderer) foreach (Material lMat in lRenderer.sharedMaterials) if (lMat != null) lSlots.Add(lMat.name);
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> lPair in lImporter.GetExternalObjectMap())
                if (lPair.Key.type == typeof(Material)) lSlots.Add(lPair.Key.name);

            bool lChanged = false;
            foreach (string lSlot in lSlots)
            {
                string lPrefix = BestPrefix(pDir, lSlot);
                if (lPrefix == null) continue;
                string lLower = lSlot.ToLowerInvariant();
                Kind lKind = lLower.Contains("glass") || lLower.Contains("window") ? Kind.Glass : LooksLikeCard(lSlot) ? Kind.Cutout : Kind.Opaque;
                Material lMaterial = MakeMaterial(pDir, lPrefix, $"{Unix(pDir)}/M_{lSlot}.mat", lKind);
                if (lMaterial == null) continue;
                AssetImporter.SourceAssetIdentifier lId = new AssetImporter.SourceAssetIdentifier(typeof(Material), lSlot);
                if (lImporter.GetExternalObjectMap().TryGetValue(lId, out Object lOld) && lOld == lMaterial) continue;
                lImporter.AddRemap(lId, lMaterial);
                lChanged = true;
            }
            if (lImporter.importAnimation && !lFbx.Contains("/Characters/")) { lImporter.importAnimation = false; lChanged = true; }
            if (lChanged) lImporter.SaveAndReimport();
            return true;
        }

        /// <summary>The texture prefix that belongs to a material slot: the slot name itself, or the longest prefix it starts with.</summary>
        private static string BestPrefix(string pDir, string pSlot)
        {
            if (Find(pDir, pSlot, "diff") != null || Find(pDir, pSlot, "diffa") != null || Find(pDir, pSlot, "nor_gl") != null) return pSlot;
            string lBest = null;
            foreach (string lFile in Directory.GetFiles(pDir))
            {
                string lName = Path.GetFileNameWithoutExtension(lFile);
                int lIndex = lName.IndexOf("_diff", System.StringComparison.OrdinalIgnoreCase);
                if (lIndex <= 0) continue;
                string lPrefix = lName.Substring(0, lIndex);
                if (pSlot.StartsWith(lPrefix, System.StringComparison.OrdinalIgnoreCase) || lPrefix.StartsWith(pSlot, System.StringComparison.OrdinalIgnoreCase))
                    if (lBest == null || lPrefix.Length > lBest.Length) lBest = lPrefix;
            }
            // A single-material model whose textures use another prefix (Poly Haven ids).
            if (lBest == null)
            {
                string[] lDiffs = Directory.GetFiles(pDir).Where(f => Path.GetFileNameWithoutExtension(f).Contains("_diff") && !f.EndsWith(".meta")).ToArray();
                if (lDiffs.Length == 1) { string n = Path.GetFileNameWithoutExtension(lDiffs[0]); lBest = n.Substring(0, n.IndexOf("_diff", System.StringComparison.OrdinalIgnoreCase)); }
            }
            return lBest;
        }
    }
}
