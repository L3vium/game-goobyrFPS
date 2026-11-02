using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.World
{
    /// <summary>
    /// The world's one wind: a direction, a strength and gusts that drift downwind as patches. The
    /// grass and the foliage shader read the same shader globals (<c>Art/Shaders/GoobyrWind.hlsl</c>), so
    /// a gust that rolls over a meadow also shakes the trees it reaches. The globals are written once
    /// per frame before any camera renders (edit mode included, for map shots), with last frame's time
    /// kept so the shaders can output motion vectors for TAA. <see cref="FX.TimeOfDay"/> sets the
    /// strength per biome and time of day.
    /// </summary>
    public static class Wind
    {
        private static readonly int WIND = Shader.PropertyToID("_GoobyrWind");
        private static readonly int WIND_TIME = Shader.PropertyToID("_GoobyrWindTime");

        /// <summary>Direction the wind blows toward (world xz, normalised).</summary>
        public static Vector2 Direction { get; private set; } = new Vector2(0.8f, 0.6f);
        /// <summary>0 calm, 0.5 a breeze, 1 a strong wind.</summary>
        public static float Strength { get; private set; } = 0.45f;
        /// <summary>0 steady, 1 all gusts.</summary>
        public static float Gustiness { get; private set; } = 0.7f;
        /// <summary>How fast the gust patches drift downwind (m/s) and how big they are (m).</summary>
        public static float GustSpeed = 7f, GustSize = 28f;

        /// <summary>Shader time of this frame and of the last one.</summary>
        public static float Time { get; private set; }
        public static float PreviousTime { get; private set; }

        /// <summary>Holds the wind at a given time (review shots: the same gust in every render); null runs it live.</summary>
        public static float? Freeze;

        private static bool _hooked;

        public static void Set(Vector2 pDirection, float pStrength, float pGustiness)
        {
            if (pDirection.sqrMagnitude > 1e-4f) Direction = pDirection.normalized;
            Strength = Mathf.Max(0f, pStrength);
            Gustiness = Mathf.Clamp01(pGustiness);
            Push();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            RenderPipelineManager.beginContextRendering += BeginFrame;
            Push();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditor() => Hook();
#endif

        private static void BeginFrame(ScriptableRenderContext pContext, List<Camera> pCameras)
        {
            if (Freeze.HasValue)
            {
                Time = Freeze.Value;
                PreviousTime = Freeze.Value - 1f / 60f;
                Push();
                return;
            }
            float lNow = Application.isPlaying ? UnityEngine.Time.time : UnityEngine.Time.realtimeSinceStartup;
            if (!Mathf.Approximately(lNow, Time))
            {
                PreviousTime = Time;
                Time = lNow;
                if (PreviousTime <= 0f || Time - PreviousTime > 0.25f) PreviousTime = Time;
            }
            Push();
        }

        private static void Push()
        {
            Shader.SetGlobalVector(WIND, new Vector4(Direction.x, Direction.y, Strength, Gustiness));
            Shader.SetGlobalVector(WIND_TIME, new Vector4(Time, PreviousTime, GustSpeed, GustSize));
        }
    }
}
