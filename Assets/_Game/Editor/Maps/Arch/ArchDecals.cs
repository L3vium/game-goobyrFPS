using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Weathering decals for the generated buildings (URP Decal projectors, "Shader Graphs/Decal"): rising
    /// damp at the foot of the walls, rain streaks under the window sills, hairline cracks. The textures
    /// are painted here procedurally (noise, gradients, random walks) into Art/Generated/Decals, once per
    /// editor session; the file is rewritten only when the painting changes.
    /// </summary>
    public static class ArchDecals
    {
        private const string DIR = "Assets/_Game/Art/Generated/Decals";
        private const int SIZE = 256;
        private static Material _grime, _streak, _crack;
        private static readonly Material[] _posters = new Material[POSTERS];

        /// <summary>How many poster designs there are.</summary>
        public const int POSTERS = 3;

        public static Material Grime => _grime != null ? _grime : _grime = Make("Grime", PaintGrime);
        public static Material Streak => _streak != null ? _streak : _streak = Make("Streak", PaintStreak);
        public static Material Crack => _crack != null ? _crack : _crack = Make("Crack", PaintCrack);

        /// <summary>A pasted street poster: a fiesta bill, an election face, a printed notice.</summary>
        public static Material Poster(int pIndex)
        {
            pIndex = Mathf.Clamp(pIndex, 0, POSTERS - 1);
            if (_posters[pIndex] == null) _posters[pIndex] = Make("Poster" + pIndex, () => PaintPoster(pIndex));
            return _posters[pIndex];
        }

        private static Material Make(string pName, System.Func<Texture2D> pPaint)
        {
            Shader lShader = Shader.Find("Shader Graphs/Decal");
            if (lShader == null)
            {
                Debug.LogWarning("[Build] ArchDecals: no \"Shader Graphs/Decal\" shader, the weathering decals are skipped.");
                return null;
            }
            EditorAssets.EnsureFolder(DIR);
            string lTexturePath = $"{DIR}/T_Decal_{pName}.png";
            Texture2D lPainted = pPaint();
            byte[] lBytes = lPainted.EncodeToPNG();
            Object.DestroyImmediate(lPainted);
            if (!File.Exists(lTexturePath) || !lBytes.SequenceEqual(File.ReadAllBytes(lTexturePath)))
            {
                File.WriteAllBytes(lTexturePath, lBytes);
                AssetDatabase.ImportAsset(lTexturePath, ImportAssetOptions.ForceUpdate);
            }
            TextureImporter lImporter = AssetImporter.GetAtPath(lTexturePath) as TextureImporter;
            if (lImporter != null && (!lImporter.alphaIsTransparency || lImporter.wrapMode != TextureWrapMode.Clamp))
            {
                lImporter.alphaIsTransparency = true;
                lImporter.wrapMode = TextureWrapMode.Clamp;
                lImporter.sRGBTexture = true;
                lImporter.mipmapEnabled = true;
                lImporter.SaveAndReimport();
            }
            string lMaterialPath = $"{DIR}/M_Decal_{pName}.mat";
            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lMaterialPath);
            if (lMaterial == null)
            {
                lMaterial = new Material(lShader) { name = "M_Decal_" + pName };
                AssetDatabase.CreateAsset(lMaterial, lMaterialPath);
            }
            lMaterial.shader = lShader;
            lMaterial.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(lTexturePath));
            lMaterial.SetFloat("Normal_Blend", 0f);
            lMaterial.enableInstancing = true;
            EditorUtility.SetDirty(lMaterial);
            return lMaterial;
        }

        private static float Noise(float x, float y, int pOctaves)
        {
            float lSum = 0f, lAmp = 0.5f, lFreq = 1f;
            for (int o = 0; o < pOctaves; o++) { lSum += Mathf.PerlinNoise(x * lFreq + o * 17.3f, y * lFreq + o * 9.1f) * lAmp; lAmp *= 0.5f; lFreq *= 2.1f; }
            return lSum;
        }

        /// <summary>Rising damp: dark and blotchy at the bottom edge, fading upward in tongues, soft at the ends.</summary>
        private static Texture2D PaintGrime()
        {
            Texture2D t = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, true);
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float u = (float)x / SIZE, v = (float)y / SIZE;
                    float n = Noise(u * 8f, v * 3f, 5);
                    float lTongue = Noise(u * 14f, 3.7f, 3) * 0.55f;
                    float lHeight = Mathf.Clamp01(1f - v / Mathf.Max(0.05f, 0.25f + lTongue));
                    float lEdge = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) * 8f);
                    float a = Mathf.Clamp01(lHeight * (0.45f + n * 0.75f)) * lEdge * 0.8f;
                    float c = 0.12f + n * 0.08f;
                    t.SetPixel(x, y, new Color(c * 1.1f, c, c * 0.85f, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Rain streaks: a few thin dark runs dripping down from the top edge.</summary>
        private static Texture2D PaintStreak()
        {
            Texture2D t = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, true);
            System.Random r = new System.Random(7);
            float[] lStrength = new float[SIZE], lReach = new float[SIZE];
            for (int k = 0; k < 9; k++)
            {
                int lCentre = 30 + r.Next(SIZE - 60), lWidth = 3 + r.Next(8);
                float lLength = 0.35f + (float)r.NextDouble() * 0.6f, lPower = 0.4f + (float)r.NextDouble() * 0.5f;
                for (int x = lCentre - lWidth; x <= lCentre + lWidth; x++)
                {
                    if (x < 0 || x >= SIZE) continue;
                    float lHere = lPower * (1f - Mathf.Abs(x - lCentre) / (float)(lWidth + 1));
                    if (lHere > lStrength[x]) { lStrength[x] = lHere; lReach[x] = lLength; }
                }
            }
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float v = (float)y / SIZE;
                    // Strong at the top edge (under the sill), fading down to the streak's length.
                    float lFade = lReach[x] > 0f ? Mathf.Clamp01((v - (1f - lReach[x])) / lReach[x]) : 0f;
                    float n = Noise(x * 0.05f, y * 0.01f, 3);
                    float a = Mathf.Clamp01(lStrength[x] * lFade * (0.45f + n * 0.6f)) * 0.8f;
                    t.SetPixel(x, y, new Color(0.13f, 0.12f, 0.1f, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Hairline cracks: two branching random walks down from the top.</summary>
        private static Texture2D PaintCrack()
        {
            Texture2D t = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, true);
            Color[] lClear = new Color[SIZE * SIZE];
            for (int i = 0; i < lClear.Length; i++) lClear[i] = new Color(0.1f, 0.09f, 0.08f, 0f);
            t.SetPixels(lClear);
            System.Random r = new System.Random(11);
            void Walk(Vector2 p, Vector2 d, int pSteps, int pDepth)
            {
                for (int k = 0; k < pSteps; k++)
                {
                    d = (Quaternion.Euler(0, 0, ((float)r.NextDouble() - 0.5f) * 50f) * d).normalized;
                    p += d * 1.6f;
                    int x = (int)p.x, y = (int)p.y;
                    if (x < 1 || y < 1 || x >= SIZE - 1 || y >= SIZE - 1) return;
                    float lFade = 1f - (float)k / pSteps;
                    for (int ox = -1; ox <= 0; ox++) t.SetPixel(x + ox, y, new Color(0.08f, 0.07f, 0.06f, 0.85f * lFade));
                    if (pDepth < 2 && r.NextDouble() < 0.04) Walk(p, Quaternion.Euler(0, 0, r.NextDouble() < 0.5 ? 35f : -35f) * d, pSteps / 2, pDepth + 1);
                }
            }
            for (int k = 0; k < 2; k++) Walk(new Vector2(SIZE * 0.5f + (k - 0.5f) * 40f, SIZE - 4), new Vector2(((float)r.NextDouble() - 0.5f) * 0.6f, -1f), 140, 0);
            t.Apply();
            return t;
        }

        /// <summary>
        /// A poster in poster space (u 0..1 across, v 0..1 up): paper with torn, faded edges; a design
        /// of flat colour blocks and lines of "print" (bars too small to read), sun-faded and stained.
        /// </summary>
        private static Texture2D PaintPoster(int pDesign)
        {
            Texture2D t = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, true);
            Color lPaper = pDesign == 0 ? new Color(0.95f, 0.8f, 0.3f) : pDesign == 1 ? new Color(0.93f, 0.92f, 0.88f) : new Color(0.9f, 0.88f, 0.8f);
            Color lBand = pDesign == 0 ? new Color(0.72f, 0.12f, 0.1f) : pDesign == 1 ? new Color(0.12f, 0.25f, 0.55f) : new Color(0.15f, 0.15f, 0.15f);
            Color lInk = new Color(0.1f, 0.09f, 0.08f);
            // Text lines: (v, length) rows of short word bars.
            bool Print(float u, float v, float v0, float v1, float u0, float u1, int pSeed)
            {
                if (v < v0 || v > v1 || u < u0 || u > u1) return false;
                float lRow = (v - v0) / (v1 - v0) * ((v1 - v0) / 0.035f);
                if (lRow - Mathf.Floor(lRow) > 0.55f) return false;
                int lLine = Mathf.FloorToInt(lRow);
                float lWord = (u - u0) * 26f + Mathf.Sin(lLine * 12.9f + pSeed) * 3f;
                return lWord - Mathf.Floor(lWord) < 0.78f && u < u1 - ((lLine * 7 + pSeed) % 5) * 0.03f;
            }
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float u = (x + 0.5f) / SIZE, v = (y + 0.5f) / SIZE;
                    Color c = lPaper;
                    switch (pDesign)
                    {
                        case 0: // fiesta bill: red title band, a dancer silhouette in a sun disc, the programme
                            if (v > 0.78f && v < 0.94f) c = lBand;
                            if (v > 0.81f && v < 0.91f && Print(u, v, 0.82f, 0.9f, 0.12f, 0.88f, 3) && (int)(v * 40f) % 2 == 0) c = Color.white;
                            float lDisc = Vector2.Distance(new Vector2(u, v * 1.4f), new Vector2(0.5f, 0.56f * 1.4f));
                            if (lDisc < 0.2f) c = Color.Lerp(new Color(0.95f, 0.55f, 0.15f), lPaper, lDisc / 0.2f * 0.3f);
                            // A dancer: head, bodice, a flared skirt, arms raised.
                            if (Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.7f)) < 0.034f) c = lInk;
                            if (v > 0.57f && v < 0.67f && Mathf.Abs(u - 0.5f) < 0.03f) c = lInk;
                            if (v > 0.4f && v <= 0.57f && Mathf.Abs(u - 0.5f) < 0.03f + (0.57f - v) * 0.95f) c = lInk;
                            float lArm = Mathf.Abs(u - 0.5f) - 0.03f, lRise = v - 0.64f;
                            if (lArm > 0f && lArm < 0.12f && Mathf.Abs(lRise - lArm * 0.9f) < 0.013f) c = lInk;
                            if (Print(u, v, 0.08f, 0.33f, 0.1f, 0.9f, 5)) c = lInk;
                            break;
                        case 1: // election poster: a face in a blue frame, the name band, the slogan
                            if (v > 0.32f && v < 0.92f && u > 0.08f && u < 0.92f) c = lBand;
                            float lFace = Vector2.Distance(new Vector2(u, v * 1.3f), new Vector2(0.5f, 0.66f * 1.3f));
                            if (v > 0.36f && v < 0.88f && u > 0.14f && u < 0.86f) c = new Color(0.8f, 0.82f, 0.85f);
                            if (lFace < 0.17f) c = new Color(0.78f, 0.58f, 0.45f);
                            if (lFace < 0.18f && v > 0.72f) c = new Color(0.15f, 0.1f, 0.08f);
                            if (v > 0.36f && v < 0.5f && Mathf.Abs(u - 0.5f) < 0.28f - (v - 0.36f)) c = new Color(0.12f, 0.13f, 0.18f);
                            if (v > 0.18f && v < 0.29f) c = new Color(0.8f, 0.1f, 0.1f);
                            if (v > 0.2f && v < 0.27f && Print(u, v, 0.205f, 0.265f, 0.15f, 0.85f, 9)) c = Color.white;
                            if (Print(u, v, 0.05f, 0.14f, 0.2f, 0.8f, 2)) c = lInk;
                            break;
                        default: // printed notice: a heading, a seal, dense small print
                            if (Print(u, v, 0.8f, 0.9f, 0.18f, 0.82f, 1) && (int)(v * 50f) % 2 == 0) c = lBand;
                            if (Print(u, v, 0.14f, 0.74f, 0.1f, 0.9f, 4)) c = Color.Lerp(lInk, lPaper, 0.25f);
                            if (Vector2.Distance(new Vector2(u, v), new Vector2(0.78f, 0.1f)) < 0.06f) c = new Color(0.55f, 0.1f, 0.12f);
                            break;
                    }
                    // Sun fading, damp stains, grime; torn ragged edges and a corner gone.
                    float n = Noise(u * 6f + pDesign, v * 6f, 4);
                    c = Color.Lerp(c, new Color(0.92f, 0.9f, 0.84f), 0.18f + n * 0.12f);
                    c *= 0.82f + n * 0.25f;
                    float lEdge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float lRag = 0.02f + Noise(u * 40f + v * 40f, pDesign * 3.1f, 2) * 0.03f;
                    float a = lEdge > lRag ? 0.96f : 0f;
                    if (pDesign != 2 && u + (1f - v) < 0.18f + Noise(u * 30f, v * 30f, 2) * 0.06f) a = 0f;
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>
        /// A decal projector on a wall: world centre on the face, the face's outward normal, width, height,
        /// how far the box reaches out from the face and into it (kept shallow so passers-by aren't
        /// painted), and an optional mirror for variety.
        /// </summary>
        public static void Place(Transform pParent, Material pMaterial, Vector3 pFaceCentre, Vector3 pOutward, float pWidth, float pHeight, float pOut = 0.25f, float pIn = 0.1f, bool pMirror = false)
        {
            if (pMaterial == null) return;
            GameObject lObject = new GameObject("Decal_" + pMaterial.name.Replace("M_Decal_", ""));
            lObject.transform.SetParent(pParent, false);
            lObject.transform.position = pFaceCentre + pOutward * ((pOut - pIn) * 0.5f);
            lObject.transform.rotation = Quaternion.LookRotation(-pOutward, Vector3.up);
            DecalProjector lProjector = lObject.AddComponent<DecalProjector>();
            lProjector.material = pMaterial;
            lProjector.size = new Vector3(pWidth, pHeight, pOut + pIn);
            lProjector.pivot = Vector3.zero;
            if (pMirror) { lProjector.uvScale = new Vector2(-1f, 1f); lProjector.uvBias = new Vector2(1f, 0f); }
            lProjector.drawDistance = 70f;
            lProjector.fadeScale = 0.9f;
            lProjector.startAngleFade = 50f;
            lProjector.endAngleFade = 75f;
            lObject.isStatic = true;
        }
    }
}
