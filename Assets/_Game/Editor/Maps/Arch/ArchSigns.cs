using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Painted shop signs for the generated buildings (a bakery, a bar, a chemist's): the words are set in
    /// the built-in font, glyph by glyph from its atlas, on a weathered board with a painted border; the
    /// texture goes to Art/Generated/Signs (rewritten only when the painting changes) under a lit
    /// material. <see cref="ArchBuilder"/> hangs the board over the street door that names it.
    /// </summary>
    public static class ArchSigns
    {
        private const string DIR = "Assets/_Game/Art/Generated/Signs";
        private const int W = 512, H = 128, GLYPH = 64;
        private static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private static Texture2D _atlas;

        /// <summary>Board and ink colours, picked by the text so a street gets a mix.</summary>
        private static readonly (Color board, Color ink)[] PALETTE =
        {
            (new Color(0.12f, 0.24f, 0.17f), new Color(0.93f, 0.86f, 0.62f)),
            (new Color(0.42f, 0.09f, 0.07f), new Color(0.95f, 0.8f, 0.42f)),
            (new Color(0.1f, 0.15f, 0.3f), new Color(0.95f, 0.94f, 0.9f)),
            (new Color(0.9f, 0.86f, 0.74f), new Color(0.14f, 0.12f, 0.1f)),
            (new Color(0.3f, 0.2f, 0.12f), new Color(0.96f, 0.92f, 0.82f))
        };

        /// <summary>The lit material of a sign reading <paramref name="pText"/> (null when the font is missing).</summary>
        public static Material For(string pText)
        {
            if (string.IsNullOrEmpty(pText)) return null;
            string lKey = Slug(pText);
            if (_materials.TryGetValue(lKey, out Material lCached) && lCached != null) return lCached;
            int lHash = 0;
            foreach (char ch in pText) lHash = lHash * 31 + ch;
            (Color lBoard, Color lInk) = PALETTE[(lHash & 0x7fffffff) % PALETTE.Length];
            Texture2D lPainted = Paint(pText, lBoard, lInk, lHash);
            if (lPainted == null) return null;
            byte[] lBytes = lPainted.EncodeToPNG();
            Object.DestroyImmediate(lPainted);
            EditorAssets.EnsureFolder(DIR);
            string lTexturePath = $"{DIR}/T_Sign_{lKey}.png";
            if (!File.Exists(lTexturePath) || !lBytes.SequenceEqual(File.ReadAllBytes(lTexturePath)))
            {
                File.WriteAllBytes(lTexturePath, lBytes);
                AssetDatabase.ImportAsset(lTexturePath, ImportAssetOptions.ForceUpdate);
            }
            TextureImporter lImporter = AssetImporter.GetAtPath(lTexturePath) as TextureImporter;
            if (lImporter != null && (lImporter.wrapMode != TextureWrapMode.Clamp || lImporter.textureCompression != TextureImporterCompression.CompressedHQ))
            {
                lImporter.wrapMode = TextureWrapMode.Clamp;
                lImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                lImporter.anisoLevel = 4;
                lImporter.SaveAndReimport();
            }
            string lMaterialPath = $"{DIR}/M_Sign_{lKey}.mat";
            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lMaterialPath);
            if (lMaterial == null)
            {
                lMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Sign_" + lKey };
                AssetDatabase.CreateAsset(lMaterial, lMaterialPath);
            }
            lMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(lTexturePath));
            lMaterial.SetColor("_BaseColor", Color.white);
            lMaterial.SetFloat("_Smoothness", 0.22f);
            lMaterial.enableInstancing = true;
            EditorUtility.SetDirty(lMaterial);
            _materials[lKey] = lMaterial;
            return lMaterial;
        }

        private static string Slug(string pText)
        {
            System.Text.StringBuilder lSlug = new System.Text.StringBuilder();
            foreach (char ch in pText.Normalize(System.Text.NormalizationForm.FormD))
            {
                if (char.IsLetterOrDigit(ch) && ch < 128) lSlug.Append(char.ToLowerInvariant(ch));
                else if (ch == ' ' || ch == '-') lSlug.Append('_');
            }
            return lSlug.ToString();
        }

        /// <summary>A readable copy of the built-in font's atlas, with every glyph of the text in it.</summary>
        private static Texture2D Atlas(Font pFont, string pText)
        {
            pFont.RequestCharactersInTexture(pText, GLYPH, FontStyle.Bold);
            Texture lSource = pFont.material.mainTexture;
            if (_atlas != null) Object.DestroyImmediate(_atlas);
            RenderTexture lCopy = RenderTexture.GetTemporary(lSource.width, lSource.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(lSource, lCopy);
            RenderTexture lActive = RenderTexture.active;
            RenderTexture.active = lCopy;
            _atlas = new Texture2D(lSource.width, lSource.height, TextureFormat.RGBA32, false);
            _atlas.ReadPixels(new Rect(0, 0, lSource.width, lSource.height), 0, 0);
            _atlas.Apply();
            RenderTexture.active = lActive;
            RenderTexture.ReleaseTemporary(lCopy);
            return _atlas;
        }

        private static float Noise(float x, float y, int pOctaves)
        {
            float lSum = 0f, lAmp = 0.5f, lFreq = 1f;
            for (int o = 0; o < pOctaves; o++) { lSum += Mathf.PerlinNoise(x * lFreq + o * 17.3f, y * lFreq + o * 9.1f) * lAmp; lAmp *= 0.5f; lFreq *= 2.1f; }
            return lSum;
        }

        private static Texture2D Paint(string pText, Color pBoard, Color pInk, int pSeed)
        {
            Font lFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (lFont == null) return null;
            Texture2D lAtlas = Atlas(lFont, pText);
            // Measure the line, then scale it into the board (a margin round the border).
            float lWidth = 0f, lTop = 0f, lBottom = 0f;
            foreach (char ch in pText)
            {
                if (!lFont.GetCharacterInfo(ch, out CharacterInfo lInfo, GLYPH, FontStyle.Bold)) continue;
                lWidth += lInfo.advance;
                lTop = Mathf.Max(lTop, lInfo.maxY);
                lBottom = Mathf.Min(lBottom, lInfo.minY);
            }
            if (lWidth <= 0f) return null;
            float lScale = Mathf.Min((W - 70f) / lWidth, (H - 44f) / Mathf.Max(1f, lTop - lBottom));
            float lPen = (W - lWidth * lScale) * 0.5f, lBase = (H - (lTop - lBottom) * lScale) * 0.5f - lBottom * lScale;

            float[] lAlpha = new float[W * H];
            foreach (char ch in pText)
            {
                if (!lFont.GetCharacterInfo(ch, out CharacterInfo lInfo, GLYPH, FontStyle.Bold)) continue;
                float x0 = lPen + lInfo.minX * lScale, x1 = lPen + lInfo.maxX * lScale;
                float y0 = lBase + lInfo.minY * lScale, y1 = lBase + lInfo.maxY * lScale;
                for (int y = Mathf.Max(0, Mathf.FloorToInt(y0)); y < Mathf.Min(H, Mathf.CeilToInt(y1)); y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(x0)); x < Mathf.Min(W, Mathf.CeilToInt(x1)); x++)
                    {
                        float u = Mathf.InverseLerp(x0, x1, x + 0.5f), v = Mathf.InverseLerp(y0, y1, y + 0.5f);
                        // Bilinear across the glyph's four atlas corners (handles flipped glyphs).
                        Vector2 lBottomEdge = Vector2.Lerp(lInfo.uvBottomLeft, lInfo.uvBottomRight, u), lTopEdge = Vector2.Lerp(lInfo.uvTopLeft, lInfo.uvTopRight, u);
                        Vector2 lUv = Vector2.Lerp(lBottomEdge, lTopEdge, v);
                        Color lTexel = lAtlas.GetPixelBilinear(lUv.x, lUv.y);
                        lAlpha[y * W + x] = Mathf.Max(lAlpha[y * W + x], Mathf.Max(lTexel.a, lTexel.r));
                    }
                lPen += lInfo.advance * lScale;
            }

            Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, true);
            float lShift = (pSeed & 255) * 0.37f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W, v = (float)y / H;
                    // Painted planks: grain along the board, plank seams, a painted border line.
                    float lGrain = Noise(u * 3f + lShift, v * 40f, 4);
                    Color c = pBoard * (0.82f + lGrain * 0.3f);
                    if (Mathf.Abs(v - 0.5f) < 0.006f) c *= 0.7f;
                    int lInset = Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                    if (lInset < 7) c = pBoard * 0.55f;
                    else if (lInset >= 12 && lInset < 16) c = Color.Lerp(c, pInk, 0.85f);
                    // The lettering, with a thin shadow down and right.
                    int lShadow = Mathf.Max(0, y + 3) * W + Mathf.Min(W - 1, Mathf.Max(0, x - 3));
                    float lShade = y + 3 < H ? lAlpha[Mathf.Min(lAlpha.Length - 1, lShadow)] : 0f;
                    c = Color.Lerp(c, pBoard * 0.35f, lShade * 0.55f);
                    c = Color.Lerp(c, pInk, Mathf.Clamp01(lAlpha[y * W + x] * 1.3f));
                    // Sun, rain and grime: faded patches, flaking paint showing the wood.
                    float n = Noise(u * 7f + lShift, v * 2.5f, 4);
                    c = Color.Lerp(c, new Color(0.62f, 0.58f, 0.5f), Mathf.Clamp01((n - 0.55f) * 1.4f));
                    if (Noise(u * 40f + lShift, v * 12f, 2) > 0.66f) c = Color.Lerp(c, new Color(0.35f, 0.27f, 0.2f), 0.6f);
                    c *= 0.85f + Noise(u * 2f, v * 2f + lShift, 2) * 0.25f;
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }
    }
}
