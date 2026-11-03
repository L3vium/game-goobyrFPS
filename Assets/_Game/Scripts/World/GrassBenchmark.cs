using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.World
{
    /// <summary>
    /// Measures the grass in play mode: a 1920x1080 camera (TAA, post-processing, SSAO, as in game) at
    /// each viewpoint, the grass drawn for that camera only, in four set-ups — no grass, far cards
    /// outside the depth prepass, every blade in it, and that without the far cards' motion vectors. Every view and set-up is warmed up first (shader
    /// variants compile on first use), then the set-ups alternate in blocks (a few frames to settle:
    /// GPU timings arrive several frames late) so a background load on the machine hits them alike. Recorded per frame: the GPU frame time
    /// (<see cref="FrameTimingManager"/>), the GPU time of the passes the grass draws in, the grass's own
    /// CPU time, and how many tufts and cards it drew. Medians and 10th percentiles in
    /// <c>Logs/grass_bench.json</c>, then play mode ends. Added by <c>Goobyr/Debug/Grass Benchmark</c>.
    /// </summary>
    public class GrassBenchmark : MonoBehaviour
    {
        public string[] Names;
        public Vector3[] From, To;
        public int Warmup = 30, Cycles = 3, Settle = 15, Frames = 40, Width = 1920, Height = 1080;
        public string Output = "Logs/grass_bench.json";

        private static readonly string[] MARKERS = { "GrassCull", "DrawOpaqueObjects", "DrawDepthNormalPrepass", "DrawMotionVectors" };
        private static readonly string[] CONFIGS = { "off", "nearPrepass", "allPrepass", "allPrepassNearMotion" };

        private Camera _camera;
        private Recorder[] _recorders;
        private readonly FrameTiming[] _timings = new FrameTiming[1];

        private void Set(string pConfig)
        {
            GrassField.Enabled = pConfig != "off";
            GrassField.FarPrepass = pConfig.StartsWith("allPrepass");
            GrassField.FarMotion = pConfig != "allPrepassNearMotion";
        }

        private IEnumerator Start()
        {
            yield return null;
            _camera = new GameObject("BenchCamera").AddComponent<Camera>();
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c != _camera) c.enabled = false;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 900f;
            _camera.fieldOfView = 60f;
            _camera.tag = "MainCamera";
            UniversalAdditionalCameraData lData = _camera.GetUniversalAdditionalCameraData();
            lData.renderPostProcessing = true;
            lData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            RenderTexture lTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = lTarget;
            GrassField.CountInstances = true;
            GrassField.OnlyCamera = _camera;
            _recorders = new Recorder[MARKERS.Length];
            for (int i = 0; i < MARKERS.Length; i++)
            {
                _recorders[i] = Recorder.Get(MARKERS[i]);
                _recorders[i].enabled = true;
            }

            // Warm up: every view in every set-up.
            for (int v = 0; v < From.Length; v++)
            {
                Look(v);
                foreach (string lConfig in CONFIGS)
                {
                    Set(lConfig);
                    for (int w = 0; w < Warmup; w++) yield return null;
                }
            }

            StringBuilder lJson = new StringBuilder("{\n  \"gpu\": \"" + SystemInfo.graphicsDeviceName + "\",\n  \"resolution\": \"" + Width + "x" + Height + "\",\n  \"frameTimingSupported\": " + (FrameTimingManager.IsFeatureEnabled() ? "true" : "false") + ",\n  \"views\": [\n");
            for (int v = 0; v < From.Length; v++)
            {
                Look(v);
                Dictionary<string, List<float>[]> lSamples = new Dictionary<string, List<float>[]>();
                foreach (string lConfig in CONFIGS)
                {
                    List<float>[] lLists = new List<float>[MARKERS.Length + 5];
                    for (int i = 0; i < lLists.Length; i++) lLists[i] = new List<float>();
                    lSamples[lConfig] = lLists;
                }
                for (int lCycle = 0; lCycle < Cycles; lCycle++)
                    foreach (string lConfig in CONFIGS)
                    {
                        Set(lConfig);
                        for (int s = 0; s < Settle; s++) yield return null;
                        List<float>[] l = lSamples[lConfig];
                        for (int f = 0; f < Frames; f++)
                        {
                            yield return null;
                            FrameTimingManager.CaptureFrameTimings();
                            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
                            {
                                l[0].Add((float)_timings[0].gpuFrameTime);
                                l[1].Add((float)_timings[0].cpuMainThreadFrameTime);
                            }
                            for (int i = 0; i < MARKERS.Length; i++) l[2 + i].Add(_recorders[i].gpuElapsedNanoseconds / 1e6f);
                            bool lOn = GrassField.Enabled;
                            l[2 + MARKERS.Length].Add(lOn ? GrassField.FrameCpuMilliseconds : 0f);
                            l[3 + MARKERS.Length].Add(lOn ? GrassField.LastNear : 0);
                            l[4 + MARKERS.Length].Add(lOn ? GrassField.LastFar : 0);
                        }
                    }
                foreach (string lConfig in CONFIGS)
                {
                    List<float>[] l = lSamples[lConfig];
                    lJson.Append("    {\"view\": \"").Append(Names[v]).Append("\", \"config\": \"").Append(lConfig).Append("\"");
                    lJson.Append(", \"gpuFrameMs\": ").Append(F(Percentile(l[0], 0.5f))).Append(", \"gpuFrameP10Ms\": ").Append(F(Percentile(l[0], 0.1f)));
                    lJson.Append(", \"cpuMainMs\": ").Append(F(Percentile(l[1], 0.5f)));
                    for (int i = 0; i < MARKERS.Length; i++) lJson.Append(", \"").Append(MARKERS[i]).Append("Ms\": ").Append(F(Percentile(l[2 + i], 0.5f)));
                    lJson.Append(", \"grassCpuMs\": ").Append(F(Percentile(l[2 + MARKERS.Length], 0.5f)));
                    lJson.Append(", \"tufts\": ").Append(Percentile(l[3 + MARKERS.Length], 0.5f).ToString("F0")).Append(", \"cards\": ").Append(Percentile(l[4 + MARKERS.Length], 0.5f).ToString("F0"));
                    lJson.Append(v == From.Length - 1 && lConfig == CONFIGS[CONFIGS.Length - 1] ? "}\n" : "},\n");
                }
            }
            lJson.Append("  ]\n}\n");

            GrassField.OnlyCamera = null;
            GrassField.FarPrepass = false;
            GrassField.FarMotion = true;
            GrassField.Enabled = true;
            GrassField.CountInstances = false;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Output)));
            System.IO.File.WriteAllText(Output, lJson.ToString());
            Debug.Log("[Grass] benchmark written to " + Output);
            _camera.targetTexture = null;
            lTarget.Release();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Look(int pView) => _camera.transform.SetPositionAndRotation(From[pView], Quaternion.LookRotation(To[pView] - From[pView]));

        private static string F(float pValue) => pValue.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        private static float Percentile(List<float> pValues, float pAt)
        {
            if (pValues.Count == 0) return 0f;
            pValues.Sort();
            return pValues[Mathf.Clamp(Mathf.FloorToInt(pValues.Count * pAt), 0, pValues.Count - 1)];
        }
    }
}
