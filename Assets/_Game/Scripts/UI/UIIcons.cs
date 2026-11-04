using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// Flat HUD icons drawn once from signed distance functions (crisp, anti-aliased, white so the
    /// HUD tints them): the NPC state diamond, its outline, a ring for countdowns, the detection arc,
    /// the phone, the target chevron, a soft dot.
    /// </summary>
    public static class UIIcons
    {
        private const int SIZE = 128;
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite Diamond => Get("diamond", (x, y) => Mathf.Abs(x) + Mathf.Abs(y) - 0.86f);
        public static Sprite DiamondOutline => Get("diamond_line", (x, y) => Mathf.Abs(Mathf.Abs(x) + Mathf.Abs(y) - 0.8f) - 0.07f);
        public static Sprite Ring => Get("ring", (x, y) => Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.82f) - 0.1f);
        public static Sprite ThinRing => Get("ring_thin", (x, y) => Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.94f) - 0.025f);
        public static Sprite Disc => Get("disc", (x, y) => Mathf.Sqrt(x * x + y * y) - 0.98f);
        public static Sprite Dot => Get("dot", (x, y) => Mathf.Sqrt(x * x + y * y) - 0.7f);
        /// <summary>A 64° slice of a thin ring, centred on "up": rotated around the crosshair toward a watcher.</summary>
        public static Sprite Arc => Get("arc", (x, y) =>
        {
            float lRing = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.9f) - 0.07f;
            float lAngle = Mathf.Abs(Mathf.Atan2(x, y)) * Mathf.Rad2Deg;
            return Mathf.Max(lRing, (lAngle - 32f) * 0.02f);
        });
        /// <summary>A downward-pointing triangle (the target marker): flat top at 0.55, tip at -0.75.</summary>
        public static Sprite Chevron => Get("chevron", (x, y) => Mathf.Max(y - 0.55f, (Mathf.Abs(x) - (y + 0.75f) * 0.62f) * 0.85f));
        /// <summary>A mobile phone: rounded body, the screen cut out, a button dot.</summary>
        public static Sprite Phone => Get("phone", (x, y) =>
        {
            float lBody = RoundBox(x, y, 0.42f, 0.8f, 0.14f);
            float lScreen = RoundBox(x, y - 0.08f, 0.3f, 0.55f, 0.04f);
            float lButton = Mathf.Sqrt(x * x + (y + 0.66f) * (y + 0.66f)) - 0.07f;
            // Inside the body, outside the screen and the button.
            return Mathf.Max(lBody, Mathf.Max(-lScreen, -lButton));
        });

        /// <summary>A navigation arrow pointing up, notched at the back (you, on the maps).</summary>
        public static Sprite Arrow => Get("arrow", (x, y) =>
        {
            // Inside the wedge under the tip (0, 0.85), above the notch line through (0, -0.75).
            float lBody = (Mathf.Abs(x) * 1.6f + y - 0.85f) * 0.53f;
            float lNotch = (Mathf.Abs(x) * 1.2f - 0.75f - y) * 0.64f;
            return Mathf.Max(lBody, lNotch);
        });
        /// <summary>A plus in a rounded square outline (create a room).</summary>
        public static Sprite Create => Get("create", (x, y) =>
        {
            float lFrame = Mathf.Abs(RoundBox(x, y, 0.82f, 0.82f, 0.2f)) - 0.06f;
            float lPlus = Mathf.Min(RoundBox(x, y, 0.42f, 0.07f, 0.03f), RoundBox(x, y, 0.07f, 0.42f, 0.03f));
            return Mathf.Min(lFrame, lPlus);
        });
        /// <summary>A code field: a rounded frame with three dots (join with a code).</summary>
        public static Sprite Code => Get("code", (x, y) =>
        {
            float lFrame = Mathf.Abs(RoundBox(x, y, 0.85f, 0.45f, 0.14f)) - 0.06f;
            float lDots = Mathf.Min(Mathf.Sqrt((x + 0.42f) * (x + 0.42f) + y * y), Mathf.Min(Mathf.Sqrt(x * x + y * y), Mathf.Sqrt((x - 0.42f) * (x - 0.42f) + y * y))) - 0.11f;
            return Mathf.Min(lFrame, lDots);
        });
        /// <summary>One person: a head over shoulders (solo).</summary>
        public static Sprite Person => Get("person", (x, y) =>
        {
            float lHead = Mathf.Sqrt(x * x + (y - 0.38f) * (y - 0.38f)) - 0.3f;
            float lBody = Mathf.Max(RoundBox(x, y + 0.55f, 0.62f, 0.38f, 0.32f), -(y + 0.82f));
            return Mathf.Min(lHead, lBody);
        });
        /// <summary>A gear-like dial: a ring with a centre dot (options).</summary>
        public static Sprite Options => Get("options", (x, y) =>
        {
            float lRadius = Mathf.Sqrt(x * x + y * y);
            float lTeeth = 0.62f + 0.12f * Mathf.Clamp(Mathf.Cos(Mathf.Atan2(y, x) * 8f) * 3f, -1f, 1f);
            return Mathf.Max(lRadius - lTeeth, 0.3f - lRadius);
        });
        /// <summary>An exit: a door frame with an arrow out (quit).</summary>
        public static Sprite Exit => Get("exit", (x, y) =>
        {
            float lFrame = Mathf.Max(Mathf.Abs(RoundBox(x + 0.25f, y, 0.5f, 0.8f, 0.08f)) - 0.06f, x - 0.1f);
            float lShaft = RoundBox(x - 0.2f, y, 0.45f, 0.07f, 0.03f);
            float lHead = Mathf.Max(Mathf.Abs(y) * 1.2f + (x - 0.85f), -(x - 0.45f));
            return Mathf.Min(lFrame, Mathf.Min(lShaft, lHead));
        });

        // ---------------------------------------------------------------- agents' icons

        public const int AGENT_COUNT = 16;
        /// <summary>The shield: an agent's icon, and the mark of a team protecting the target.</summary>
        public const int SHIELD = 8;

        /// <summary>The icon an agent chose for themselves.</summary>
        public static Sprite Agent(int pIndex)
        {
            int lIndex = Mathf.Abs(pIndex) % AGENT_COUNT;
            return Get("agent" + lIndex, (x, y) => AgentShape(lIndex, x, y));
        }

        private static float C(float pX, float pY, float pCx, float pCy, float pR) => Mathf.Sqrt((pX - pCx) * (pX - pCx) + (pY - pCy) * (pY - pCy)) - pR;

        /// <summary>Signed distance to a polygon given as x, y pairs.</summary>
        private static float Poly(float pX, float pY, params float[] pPoints)
        {
            int lCount = pPoints.Length / 2;
            float lDistance = float.MaxValue;
            bool lInside = false;
            for (int i = 0, j = lCount - 1; i < lCount; j = i++)
            {
                float lAx = pPoints[j * 2], lAy = pPoints[j * 2 + 1], lBx = pPoints[i * 2], lBy = pPoints[i * 2 + 1];
                float lEx = lBx - lAx, lEy = lBy - lAy, lWx = pX - lAx, lWy = pY - lAy;
                float t = Mathf.Clamp01((lWx * lEx + lWy * lEy) / (lEx * lEx + lEy * lEy));
                float lDx = lWx - lEx * t, lDy = lWy - lEy * t;
                lDistance = Mathf.Min(lDistance, lDx * lDx + lDy * lDy);
                if ((lAy > pY) != (lBy > pY) && pX < lAx + (pY - lAy) / (lBy - lAy) * (lBx - lAx)) lInside = !lInside;
            }
            return (lInside ? -1f : 1f) * Mathf.Sqrt(lDistance);
        }

        private static float StarShape(float pX, float pY)
        {
            float[] lPoints = new float[20];
            for (int i = 0; i < 10; i++)
            {
                float lAngle = Mathf.PI * 0.5f + i * Mathf.PI / 5f, lRadius = i % 2 == 0 ? 0.92f : 0.38f;
                lPoints[i * 2] = Mathf.Cos(lAngle) * lRadius;
                lPoints[i * 2 + 1] = Mathf.Sin(lAngle) * lRadius;
            }
            return Poly(pX, pY, lPoints);
        }

        private static float AgentShape(int pIndex, float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            switch (pIndex)
            {
                case 0: return StarShape(x, y);
                case 1: return Mathf.Min(Mathf.Min(C(x, y, -0.36f, 0.3f, 0.42f), C(x, y, 0.36f, 0.3f, 0.42f)), Poly(x, y, -0.74f, 0.12f, 0.74f, 0.12f, 0f, -0.86f));
                case 2: return Poly(x, y, 0.15f, 0.92f, -0.55f, -0.05f, -0.05f, -0.05f, -0.2f, -0.92f, 0.55f, 0.15f, 0.05f, 0.15f);
                case 3: return Mathf.Max(C(x, y, 0f, 0f, 0.82f), -C(x, y, 0.36f, 0.16f, 0.64f));
                case 4: return Mathf.Min(r - 0.42f, Mathf.Max(Mathf.Abs(r - 0.74f) - 0.16f, (0.5f - Mathf.Cos(Mathf.Atan2(y, x) * 8f)) * 0.3f));
                case 5: return Mathf.Min(C(x, y, 0f, -0.24f, 0.6f), Poly(x, y, -0.52f, 0.06f, 0.52f, 0.06f, 0f, 0.94f));
                case 6: return Poly(x, y, -0.8f, -0.62f, 0.8f, -0.62f, 0.92f, 0.5f, 0.42f, 0f, 0f, 0.72f, -0.42f, 0f, -0.92f, 0.5f);
                case 7: return Mathf.Abs(x) + Mathf.Abs(y) * 0.8f - 0.84f;
                case SHIELD: return Poly(x, y, -0.72f, 0.78f, 0.72f, 0.78f, 0.72f, 0f, 0f, -0.92f, -0.72f, 0f);
                case 9: return Mathf.Min(Mathf.Max(Mathf.Max(C(x, y, 0f, 0.62f, 1.08f), C(x, y, 0f, -0.62f, 1.08f)), 0.42f - r), r - 0.24f);
                case 10: return Mathf.Min(Mathf.Min(Mathf.Abs(r - 0.76f) - 0.11f, Mathf.Abs(r - 0.42f) - 0.09f), r - 0.13f);
                case 11: return Mathf.Min(RoundBox(x, y, 0.24f, 0.82f, 0.04f), RoundBox(x, y, 0.82f, 0.24f, 0.04f));
                case 12: return Mathf.Max(Mathf.Max(Mathf.Min(C(x, y, 0f, 0.16f, 0.74f), RoundBox(x, y + 0.62f, 0.38f, 0.3f, 0.06f)), -C(x, y, -0.28f, 0.14f, 0.19f)), -C(x, y, 0.28f, 0.14f, 0.19f));
                case 13: return Mathf.Min(C(x, y, -0.1f, -0.2f, 0.66f), Poly(x, y, 0.22f, 0.3f, 0.42f, 0.5f, 0.78f, 0.84f, 0.66f, 0.94f, 0.3f, 0.6f, 0.1f, 0.42f));
                case 14: return Mathf.Min(Mathf.Min(Mathf.Min(C(x, y, -0.36f, -0.2f, 0.4f), C(x, y, 0.36f, -0.2f, 0.4f)), Poly(x, y, -0.74f, -0.02f, 0.74f, -0.02f, 0f, 0.92f)), Poly(x, y, -0.1f, -0.3f, 0.1f, -0.3f, 0.26f, -0.88f, -0.26f, -0.88f));
                default: return Mathf.Min(Mathf.Min(Mathf.Min(C(x, y, 0f, 0.42f, 0.38f), C(x, y, -0.4f, -0.08f, 0.38f)), C(x, y, 0.4f, -0.08f, 0.38f)), Poly(x, y, -0.08f, 0f, 0.08f, 0f, 0.22f, -0.9f, -0.22f, -0.9f));
            }
        }

        private static float RoundBox(float pX, float pY, float pHalfW, float pHalfH, float pRadius)
        {
            float lDx = Mathf.Abs(pX) - pHalfW + pRadius, lDy = Mathf.Abs(pY) - pHalfH + pRadius;
            return Mathf.Sqrt(Mathf.Max(lDx, 0f) * Mathf.Max(lDx, 0f) + Mathf.Max(lDy, 0f) * Mathf.Max(lDy, 0f)) + Mathf.Min(Mathf.Max(lDx, lDy), 0f) - pRadius;
        }

        /// <summary>Rasterises a distance function over [-1, 1]² (negative inside) into a white sprite.</summary>
        private static Sprite Get(string pKey, System.Func<float, float, float> pDistance)
        {
            if (_cache.TryGetValue(pKey, out Sprite lSprite) && lSprite != null) return lSprite;
            Texture2D lTexture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = "Icon_" + pKey, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color32[] lPixels = new Color32[SIZE * SIZE];
            float lPixel = 2f / SIZE;
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float lX = (x + 0.5f) / SIZE * 2f - 1f, lY = (y + 0.5f) / SIZE * 2f - 1f;
                    float lAlpha = Mathf.Clamp01(0.5f - pDistance(lX, lY) / lPixel);
                    lPixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)(lAlpha * 255f));
                }
            lTexture.SetPixels32(lPixels);
            lTexture.Apply(false, true);
            lSprite = Sprite.Create(lTexture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
            _cache[pKey] = lSprite;
            return lSprite;
        }
    }
}
