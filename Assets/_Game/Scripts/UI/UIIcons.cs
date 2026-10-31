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
