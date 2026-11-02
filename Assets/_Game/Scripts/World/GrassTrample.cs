using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.World
{
    /// <summary>
    /// Trampled grass. A 64 m window round the camera (one texel per 25 cm, a half-float texture) holds
    /// how the grass is pushed and flattened; <c>Art/Shaders/GrassTrample.compute</c> updates it once a
    /// frame: the window follows the camera, pressed grass stands back up over a few seconds, and every
    /// source presses again. Sources: <see cref="GrassBender"/> components, and in play mode the
    /// players, the NPCs and the dropped items (found every half second), so a body walking through a
    /// meadow leaves a trail and an item on the ground stays visible in tall grass. The grass shader
    /// reads the texture as <c>_GrassTrample</c> / <c>_GrassTrampleArea</c>.
    /// </summary>
    public static class GrassTrample
    {
        public const int SIZE = 256;
        public const float AREA = 64f;
        private const int MAX_SOURCES = 128;
        /// <summary>Seconds for pressed grass to mostly stand back up.</summary>
        public static float Recovery = 2.5f;

        private static readonly int ID_TRAMPLE = Shader.PropertyToID("_GrassTrample"), ID_AREA = Shader.PropertyToID("_GrassTrampleArea");
        private static readonly int ID_PREVIOUS = Shader.PropertyToID("_Previous"), ID_CURRENT = Shader.PropertyToID("_Current"), ID_SOURCES = Shader.PropertyToID("_Sources");
        private static readonly int ID_COUNT = Shader.PropertyToID("_SourceCount"), ID_SIZE = Shader.PropertyToID("_Size"), ID_WINDOW = Shader.PropertyToID("_Window"), ID_SHIFT = Shader.PropertyToID("_Shift");

        private static readonly List<GrassBender> _benders = new List<GrassBender>();
        private static readonly List<(Transform t, float radius, float strength)> _found = new List<(Transform, float, float)>();
        private static readonly List<(Vector3 position, float radius, float strength)> _stamps = new List<(Vector3, float, float)>();
        private static readonly Dictionary<Transform, Vector3> _last = new Dictionary<Transform, Vector3>();
        private static readonly Vector4[] _sources = new Vector4[MAX_SOURCES * 2];
        private static RenderTexture[] _textures;
        private static GraphicsBuffer _buffer;
        private static CommandBuffer _commands;
        private static int _current, _frame = -1;
        private static float _lastTime, _nextScan;
        private static Vector2Int _corner;
        private static bool _hooked, _fresh = true;

        /// <summary>This frame's trample texture (null before the first update) and its window (x, z, 1 / size, 1).</summary>
        public static RenderTexture Current => _textures != null ? _textures[_current] : null;
        public static Vector4 Area { get; private set; }

        public static void Add(GrassBender pBender)
        {
            Hook();
            if (!_benders.Contains(pBender)) _benders.Add(pBender);
        }

        public static void Remove(GrassBender pBender) => _benders.Remove(pBender);

        /// <summary>A one-off press (a body falling, a blast): applied on the next frame, then it recovers.</summary>
        public static void Stamp(Vector3 pPosition, float pRadius, float pStrength = 1f)
        {
            Hook();
            _stamps.Add((pPosition, pRadius, pStrength));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            RenderPipelineManager.beginContextRendering += BeginFrame;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookEditor()
        {
            Hook();
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
        }
#endif

        private static void Release()
        {
            if (_textures != null) foreach (RenderTexture t in _textures) if (t != null) { t.Release(); Object.DestroyImmediate(t); }
            _textures = null;
            _buffer?.Release();
            _buffer = null;
            _commands?.Release();
            _commands = null;
            _fresh = true;
        }

        private static void BeginFrame(ScriptableRenderContext pContext, List<Camera> pCameras)
        {
            if (GrassField.All.Count == 0 || GrassField.All[0] == null) return;
            ComputeShader lCompute = GrassField.All[0].TrampleCompute;
            if (lCompute == null) return;
            float lNow = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            int lFrame = Application.isPlaying ? Time.frameCount : Mathf.RoundToInt(lNow * 240f);
            if (lFrame == _frame && _stamps.Count == 0) return;
            _frame = lFrame;
            float lDelta = Mathf.Clamp(lNow - _lastTime, 0f, 0.1f);
            _lastTime = lNow;

            Camera lCenter = Camera.main;
            if (lCenter == null)
                foreach (Camera c in pCameras) if (c != null && c.cameraType != CameraType.Preview && c.cameraType != CameraType.Reflection) { lCenter = c; break; }
            if (lCenter == null) return;
            Ensure();

            // The window snaps to whole texels so the pressed grass does not swim as it follows.
            float lTexel = AREA / SIZE;
            Vector3 p = lCenter.transform.position;
            Vector2Int lCorner = new Vector2Int(Mathf.FloorToInt((p.x - AREA * 0.5f) / lTexel), Mathf.FloorToInt((p.z - AREA * 0.5f) / lTexel));
            Vector2Int lShift = _fresh ? new Vector2Int(SIZE * 4, SIZE * 4) : lCorner - _corner;
            _corner = lCorner;
            _fresh = false;
            Vector2 lOrigin = new Vector2(lCorner.x * lTexel, lCorner.y * lTexel);

            if (Application.isPlaying && lNow >= _nextScan) Scan(lNow);
            int lCount = Gather(lOrigin, lDelta);

            int lPrevious = _current;
            _current = 1 - _current;
            int lKernel = 0;
            _commands.Clear();
            _commands.SetBufferData(_buffer, _sources, 0, 0, Mathf.Max(1, lCount) * 2);
            _commands.SetComputeTextureParam(lCompute, lKernel, ID_PREVIOUS, _textures[lPrevious]);
            _commands.SetComputeTextureParam(lCompute, lKernel, ID_CURRENT, _textures[_current]);
            _commands.SetComputeBufferParam(lCompute, lKernel, ID_SOURCES, _buffer);
            _commands.SetComputeIntParam(lCompute, ID_COUNT, lCount);
            _commands.SetComputeIntParam(lCompute, ID_SIZE, SIZE);
            _commands.SetComputeVectorParam(lCompute, ID_WINDOW, new Vector4(lOrigin.x, lOrigin.y, lTexel, Mathf.Exp(-lDelta / Mathf.Max(0.1f, Recovery))));
            _commands.SetComputeIntParams(lCompute, ID_SHIFT, lShift.x, lShift.y, 0, 0);
            _commands.DispatchCompute(lCompute, lKernel, SIZE / 8, SIZE / 8, 1);
            pContext.ExecuteCommandBuffer(_commands);
            _commands.Clear();
            Area = new Vector4(lOrigin.x, lOrigin.y, 1f / AREA, 1f);
            Shader.SetGlobalTexture(ID_TRAMPLE, _textures[_current]);
            Shader.SetGlobalVector(ID_AREA, Area);
        }

        private static void Ensure()
        {
            if (_textures != null && _textures[0] != null && _textures[0].IsCreated()) return;
            Release();
            _textures = new RenderTexture[2];
            for (int i = 0; i < 2; i++)
            {
                _textures[i] = new RenderTexture(SIZE, SIZE, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = "GrassTrample" + i,
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _textures[i].Create();
            }
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAX_SOURCES * 2, 16);
            _commands = new CommandBuffer { name = "GrassTrample" };
            _fresh = true;
        }

        /// <summary>Players, NPCs and dropped items (they carry no component of ours).</summary>
        private static void Scan(float pNow)
        {
            _nextScan = pNow + 0.5f;
            _found.Clear();
            foreach (Player.PlayerAvatar a in Object.FindObjectsByType<Player.PlayerAvatar>(FindObjectsSortMode.None)) _found.Add((a.transform, 0.55f, 1f));
            foreach (AI.Npc n in Object.FindObjectsByType<AI.Npc>(FindObjectsSortMode.None)) _found.Add((n.transform, 0.55f, 1f));
            foreach (Weapons.ItemPickup i in Object.FindObjectsByType<Weapons.ItemPickup>(FindObjectsSortMode.None)) _found.Add((i.transform, 0.45f, 0.85f));
        }

        /// <summary>Packs the sources inside the window (with their stride, from their last position).</summary>
        private static int Gather(Vector2 pOrigin, float pDelta)
        {
            int n = 0;
            Rect lWindow = new Rect(pOrigin.x - 2f, pOrigin.y - 2f, AREA + 4f, AREA + 4f);
            void Put(Transform pTransform, Vector3 pPosition, float pRadius, float pStrength)
            {
                if (n >= MAX_SOURCES || !lWindow.Contains(new Vector2(pPosition.x, pPosition.z))) return;
                Vector3 lStride = Vector3.zero;
                if (pTransform != null)
                {
                    if (_last.TryGetValue(pTransform, out Vector3 lBefore) && pDelta > 0f) lStride = Vector3.ClampMagnitude((pPosition - lBefore) / pDelta * 0.25f, 1f);
                    _last[pTransform] = pPosition;
                }
                _sources[n * 2] = new Vector4(pPosition.x, pPosition.y, pPosition.z, pRadius);
                _sources[n * 2 + 1] = new Vector4(lStride.x, lStride.z, pStrength, 0f);
                n++;
            }
            foreach (GrassBender b in _benders) if (b != null && b.isActiveAndEnabled) Put(b.transform, b.transform.position, b.Radius * Mathf.Max(b.transform.lossyScale.x, b.transform.lossyScale.z), b.Strength);
            foreach ((Transform t, float lRadius, float lStrength) in _found) if (t != null && t.gameObject.activeInHierarchy) Put(t, t.position, lRadius, lStrength);
            foreach ((Vector3 lPosition, float lRadius, float lStrength) in _stamps) Put(null, lPosition, lRadius, lStrength);
            _stamps.Clear();
            if (_last.Count > 512) _last.Clear();
            return n;
        }
    }
}
