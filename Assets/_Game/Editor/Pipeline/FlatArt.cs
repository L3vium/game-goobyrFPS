using System.IO;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Turns the project's textured materials into the game's flat look (<c>Goobyr/Flat</c>): each one
    /// keeps its GUID, so every map, prefab and model that uses it changes at once, and takes the
    /// average colour of its texture, a little cleaner and brighter. Textures are dropped from the
    /// materials and terrain layers, so they leave the build. Glass, alpha-cut leaves and the special
    /// shaders are left alone.
    /// </summary>
    public static class FlatArt
    {
        public const string SHADER = "Goobyr/Flat";
        private const string LIT = "Universal Render Pipeline/Lit";
        private const string ROCK = "Goobyr/Rock";
        private const string LAYER_DIR = EditorAssets.ROOT + "/Art/Generated/FlatLayers";

        [MenuItem("Goobyr/Build/Flat Materials")]
        public static void Build()
        {
            int lCount = 0;
            foreach (string lGuid in AssetDatabase.FindAssets("t:Material", new[] { EditorAssets.ROOT + "/Art" }))
            {
                Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(lGuid));
                if (lMaterial != null && Apply(lMaterial)) lCount++;
            }
            int lLayers = 0;
            foreach (string lGuid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { EditorAssets.ROOT + "/Art" }))
            {
                TerrainLayer lLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(lGuid));
                if (lLayer != null && Apply(lLayer)) lLayers++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] flat materials: {lCount} materials, {lLayers} terrain layers");
        }

        /// <summary>A material to flat colour; false when it stays as it is (already flat, glass, cut-out leaves, special shaders).</summary>
        public static bool Apply(Material pMaterial)
        {
            string lShader = pMaterial.shader != null ? pMaterial.shader.name : "";
            if (lShader != LIT && lShader != ROCK) return false;
            if (lShader == LIT && (pMaterial.GetFloat("_Surface") > 0.5f || pMaterial.GetFloat("_AlphaClip") > 0.5f)) return false;

            Color lTint = pMaterial.HasProperty("_BaseColor") ? pMaterial.GetColor("_BaseColor") : Color.white;
            if (lShader == ROCK && pMaterial.HasProperty("_ToneA")) lTint = pMaterial.GetColor("_ToneA");
            Texture lTexture = null;
            foreach (string lName in pMaterial.GetTexturePropertyNames())
            {
                if (lName.Contains("Normal") || lName.Contains("Bump") || lName.Contains("Metal") || lName.Contains("Occlusion")) continue;
                lTexture = pMaterial.GetTexture(lName);
                if (lTexture != null) break;
            }
            Color lColor = Clean(lTexture != null ? Average(lTexture) * lTint : lTint);
            Color lEmission = pMaterial.HasProperty("_EmissionColor") && pMaterial.IsKeywordEnabled("_EMISSION") ? pMaterial.GetColor("_EmissionColor") : Color.black;

            foreach (string lProperty in pMaterial.GetTexturePropertyNames()) pMaterial.SetTexture(lProperty, null);
            pMaterial.shader = Shader.Find(SHADER);
            pMaterial.shaderKeywords = new string[0];
            pMaterial.SetColor("_BaseColor", lColor);
            pMaterial.SetColor("_EmissionColor", lEmission);
            pMaterial.SetFloat("_Cull", 2f);
            pMaterial.enableInstancing = true;
            pMaterial.renderQueue = -1;
            EditorUtility.SetDirty(pMaterial);
            return true;
        }

        /// <summary>
        /// A model made for the flat look (<c>Tools/Blender/characters/wobbly</c>, <c>Tools/Blender/lowpoly</c>):
        /// its <c>&lt;Name&gt;_flat.json</c> gives each material slot its colour. False when it has none.
        /// </summary>
        public static bool RemapFlat(string pFbx)
        {
            string lJson = Path.ChangeExtension(pFbx, null) + "_flat.json";
            if (!File.Exists(lJson)) return false;
            ModelImporter lImporter = AssetImporter.GetAtPath(pFbx) as ModelImporter;
            if (lImporter == null) return false;
            string lName = Path.GetFileNameWithoutExtension(pFbx), lDir = Path.GetDirectoryName(pFbx).Replace('\\', '/');
            bool lChanged = false;
            string lText = File.ReadAllText(lJson);
            int lAt = lText.IndexOf("\"slots\"", System.StringComparison.Ordinal);
            lAt = lText.IndexOf('{', lAt) + 1;
            while (true)
            {
                int lOpen = lText.IndexOf('"', lAt);
                if (lOpen < 0) break;
                int lClose = lText.IndexOf('"', lOpen + 1);
                string lSlot = lText.Substring(lOpen + 1, lClose - lOpen - 1);
                int a = lText.IndexOf('[', lClose), b = lText.IndexOf(']', a);
                string[] lParts = lText.Substring(a + 1, b - a - 1).Split(',');
                System.Globalization.CultureInfo lInv = System.Globalization.CultureInfo.InvariantCulture;
                Color lColor = new Color(float.Parse(lParts[0].Trim(), lInv), float.Parse(lParts[1].Trim(), lInv), float.Parse(lParts[2].Trim(), lInv), 1f);
                lAt = b + 1;

                string lPath = $"{lDir}/M_{lName}_{lSlot}.mat";
                Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lPath);
                if (lMaterial == null)
                {
                    lMaterial = new Material(Shader.Find(SHADER));
                    AssetDatabase.CreateAsset(lMaterial, lPath);
                }
                foreach (string lProperty in lMaterial.GetTexturePropertyNames()) lMaterial.SetTexture(lProperty, null);
                lMaterial.shader = Shader.Find(SHADER);
                lMaterial.shaderKeywords = new string[0];
                lMaterial.SetColor("_BaseColor", lColor);
                lMaterial.SetColor("_EmissionColor", Color.black);
                lMaterial.enableInstancing = true;
                EditorUtility.SetDirty(lMaterial);
                AssetImporter.SourceAssetIdentifier lId = new AssetImporter.SourceAssetIdentifier(typeof(Material), lName + "_" + lSlot);
                if (lImporter.GetExternalObjectMap().TryGetValue(lId, out Object lOld) && lOld == lMaterial) continue;
                lImporter.AddRemap(lId, lMaterial);
                lChanged = true;
            }
            if (lChanged) lImporter.SaveAndReimport();
            return true;
        }

        /// <summary>A terrain layer to one flat colour: a tiny solid texture, no normal or mask map.</summary>
        public static bool Apply(TerrainLayer pLayer)
        {
            if (pLayer.diffuseTexture == null || pLayer.diffuseTexture.name.StartsWith("Flat_")) return false;
            if (!AssetDatabase.IsValidFolder(LAYER_DIR)) AssetDatabase.CreateFolder(EditorAssets.ROOT + "/Art/Generated", "FlatLayers");
            // The ground carries the picture: richer and deeper than the walls standing on it.
            Color.RGBToHSV(Clean(Average(pLayer.diffuseTexture)), out float lHue, out float lSaturation, out float lValue);
            Color lColor = Color.HSVToRGB(lHue, Mathf.Clamp01(lSaturation * 1.5f + 0.06f), lValue * 0.72f);
            string lPath = $"{LAYER_DIR}/Flat_{pLayer.name}.png";
            Texture2D lSolid = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] lPixels = new Color[16];
            for (int i = 0; i < 16; i++) lPixels[i] = new Color(lColor.r, lColor.g, lColor.b, 0f);
            lSolid.SetPixels(lPixels);
            lSolid.Apply();
            File.WriteAllBytes(lPath, lSolid.EncodeToPNG());
            Object.DestroyImmediate(lSolid);
            AssetDatabase.ImportAsset(lPath);
            pLayer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);
            pLayer.normalMapTexture = null;
            pLayer.maskMapTexture = null;
            pLayer.smoothness = 0f;
            pLayer.metallic = 0f;
            EditorUtility.SetDirty(pLayer);
            return true;
        }

        /// <summary>A photograph's average is grey and dull: a touch more colour and light, never pure white or black.</summary>
        private static Color Clean(Color pColor)
        {
            Color.RGBToHSV(pColor, out float lHue, out float lSaturation, out float lValue);
            lSaturation = Mathf.Clamp01(lSaturation * 1.1f + 0.02f);
            lValue = Mathf.Clamp(Mathf.Pow(lValue, 0.85f) * 1.02f, 0.1f, 0.9f);
            Color lClean = Color.HSVToRGB(lHue, lSaturation, lValue);
            lClean.a = 1f;
            return lClean;
        }

        /// <summary>A texture's average colour (gamma space, as material colours are given).</summary>
        private static Color Average(Texture pTexture)
        {
            RenderTexture lTarget = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture lBig = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(pTexture, lBig);
            Graphics.Blit(lBig, lTarget);
            RenderTexture lActive = RenderTexture.active;
            RenderTexture.active = lTarget;
            Texture2D lRead = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            lRead.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
            lRead.Apply();
            RenderTexture.active = lActive;
            Color lSum = Color.clear;
            foreach (Color lPixel in lRead.GetPixels()) lSum += lPixel;
            Object.DestroyImmediate(lRead);
            RenderTexture.ReleaseTemporary(lTarget);
            RenderTexture.ReleaseTemporary(lBig);
            lSum /= 64f;
            lSum.a = 1f;
            return lSum;
        }
    }
}
