using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.World
{
    /// <summary>A biome's grass: its colours, its size and how much it moves.</summary>
    [System.Serializable]
    public class GrassStyle
    {
        public Color root = new Color(0.07f, 0.11f, 0.035f);
        public Color tip = new Color(0.3f, 0.47f, 0.13f);
        public Color dry = new Color(0.6f, 0.54f, 0.28f);
        public Color translucency = new Color(0.5f, 0.8f, 0.2f);
        [Tooltip("Blade height (m) where the map's height is 0.5.")] public float height = 0.55f;
        [Range(0f, 1f)] public float heightVariation = 0.45f;
        [Tooltip("Blade height (m) in the tall grass you hide in.")] public float tallHeight = 1.9f;
        public float bladeWidth = 0.04f;
        public float tuftRadius = 0.2f;
        [Tooltip("Multiplies the map's density.")] public float density = 1f;
        public float wind = 0.9f;
        [Range(0f, 2f)] public float translucencyAmount = 1f;
        [Range(0f, 1f)] public float gloss = 0.25f;

        /// <summary>
        /// The colour the meadow has from afar (linear albedo): blades seen whole and lit, roots in their
        /// shade, a little less saturated than a tip. The terrain takes it where the grass grows
        /// (<c>Goobyr/TerrainLit</c>, through <see cref="GrassField"/>'s published map), so the land does not
        /// change colour where the blades end (calibrated in the grass test scene).
        /// </summary>
        public Color FarColor
        {
            get
            {
                Color t = tip.linear;
                return new Color(t.r * 0.9f, t.g * 0.6f, t.b * 1.3f, 1f);
            }
        }

        /// <summary>The same for dry grass (where the map says dry), linear.</summary>
        public Color FarDryColor
        {
            get
            {
                Color d = dry.linear;
                return new Color(d.r * 0.55f, d.g * 0.52f, d.b * 0.6f, 1f);
            }
        }

        /// <summary>San Lázaro: lush humid meadow, broad-bladed, green to yellow-green.</summary>
        public static GrassStyle Jungle() => new GrassStyle();

        /// <summary>Weisshorn's meadows: short, fine and dense, a cooler green, straw where dry.</summary>
        public static GrassStyle Alpine() => new GrassStyle
        {
            root = new Color(0.06f, 0.1f, 0.045f), tip = new Color(0.27f, 0.42f, 0.17f), dry = new Color(0.6f, 0.56f, 0.38f),
            translucency = new Color(0.45f, 0.75f, 0.3f), height = 0.36f, heightVariation = 0.4f, tallHeight = 1.4f,
            bladeWidth = 0.03f, tuftRadius = 0.17f, density = 1.1f, wind = 1f
        };

        /// <summary>Qasr's oasis: sparse sun-bleached tufts, yellow-green, dry at the edges.</summary>
        public static GrassStyle Oasis() => new GrassStyle
        {
            root = new Color(0.08f, 0.1f, 0.04f), tip = new Color(0.36f, 0.45f, 0.14f), dry = new Color(0.7f, 0.6f, 0.33f),
            translucency = new Color(0.6f, 0.8f, 0.25f), height = 0.48f, heightVariation = 0.5f, tallHeight = 1.7f,
            bladeWidth = 0.035f, tuftRadius = 0.18f, density = 0.85f, wind = 0.9f
        };
    }

    /// <summary>
    /// Real grass on a terrain, stateless and GPU-procedural. The builder bakes a map over the terrain
    /// (R density, G height, B dryness, A tall grass) and the height range of every 16 m chunk. For every
    /// camera (game, scene view, map shots, from <c>beginCameraRendering</c>) the chunks within
    /// <see cref="FarDistance"/> are culled on the CPU, then <c>Art/Shaders/GrassCull.compute</c> visits
    /// a ~0.35 m slot grid in them, places grass from the map and the terrain's heightmap, thins it out
    /// with distance, and fills the instance buffers of two indirect draws: tufts of curved blades near
    /// the camera, 4-blade cards beyond <see cref="CardDistance"/>. <c>Goobyr/Grass</c> builds and shades
    /// the blades (wind from <see cref="Wind"/>, trampling from <see cref="GrassTrample"/>). Nothing is
    /// stored per blade: the same hash gives the same meadow every frame and on every machine. The field
    /// also publishes its map and far colours to the terrain shader, which paints the ground under and
    /// beyond the blades in the meadow's colour.
    /// </summary>
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        /// <summary>Every live field (the trample system and the benchmark look here).</summary>
        public static readonly List<GrassField> All = new List<GrassField>();
        /// <summary>Global switch (benchmarks, debug views).</summary>
        public static bool Enabled = true;
        /// <summary>CPU time of the last camera's culling and submission (ms), smoothed.</summary>
        public static float CpuMilliseconds { get; private set; }
        /// <summary>Total CPU time this frame over every camera (ms), smoothed.</summary>
        public static float FrameCpuMilliseconds { get; private set; }
        /// <summary>Reads back how many tufts and cards the cameras draw (benchmarks; one small GPU readback per camera).</summary>
        public static bool CountInstances;
        /// <summary>When set, only this camera gets grass (benchmarks: the scene view must not add its own).</summary>
        public static Camera OnlyCamera;
        /// <summary>Far cards in the depth-normals prepass too (off: only the near tufts, which SSAO needs; measured cheaper).</summary>
        public static bool FarPrepass = false;
        /// <summary>Far cards in the motion-vector pass (TAA keeps their sway sharp; off saves a vertex pass).</summary>
        public static bool FarMotion = true;
        private bool _farPrepass, _farMotion = true;
        /// <summary>
        /// A position or size the culling can take: finite and within 100 km of the origin. Every
        /// procedural draw (grass, snow glints) and every trampling source is checked, so one NaN or a
        /// body flung to infinity cannot hand the culling an invalid box.
        /// </summary>
        public static bool IsSane(Vector3 v) => IsSane(v.x) && IsSane(v.y) && IsSane(v.z);
        private static bool IsSane(float f) => !float.IsNaN(f) && !float.IsInfinity(f) && f > -1e5f && f < 1e5f;

        public static int LastNear { get; private set; }
        public static int LastFar { get; private set; }
        private static UnityEngine.Profiling.CustomSampler _sampler;

        private const int NEAR_CAPACITY = 64 * 1024, FAR_CAPACITY = 128 * 1024, STRIDE = 36;
        private const float MAX_HEIGHT = 32766f / 65535f;

        [SerializeField] private Terrain _terrain;
        [SerializeField] private Texture2D _map;
        [SerializeField] private GrassStyle _style = new GrassStyle();
        [SerializeField] private float _chunkSize = 16f;
        [SerializeField] private int _chunks;
        [Tooltip("Lowest and highest ground of each chunk; x > y when the chunk has no grass.")]
        [SerializeField] private Vector2[] _chunkHeights;
        [SerializeField] private Shader _shader;
        [SerializeField] private ComputeShader _cull;
        [SerializeField] private ComputeShader _trample;

        [Header("Distances")]
        [Tooltip("No grass past this distance (m): the ground, in the meadow's colour, takes over.")] public float FarDistance = 90f;
        [Tooltip("Tufts become 4-blade cards around this distance (m).")] public float CardDistance = 22f;
        [Tooltip("Every slot is kept up to this distance (m), then fewer and wider.")] public float ThinStart = 16f;
        public float ThinExponent = 1.25f;
        [Range(0.02f, 1f)] public float FewestKept = 0.15f;
        [Tooltip("Spacing of the slot grid (m): one tuft per slot at most.")] public float Spacing = 0.28f;

        public GrassStyle Style => _style;
        public Terrain Land => _terrain;
        public ComputeShader TrampleCompute => _trample;

        private class View
        {
            public GraphicsBuffer near, far, args, chunks;
            public MaterialPropertyBlock nearProps, farProps;
            public CommandBuffer commands;
            public int frame;
        }

        private readonly Dictionary<Camera, View> _views = new Dictionary<Camera, View>();
        private readonly List<Camera> _stale = new List<Camera>();
        private readonly Plane[] _planes = new Plane[6];
        private readonly Vector4[] _planeVectors = new Vector4[6];
        private uint[] _visible;
        private readonly uint[] _argsReset = new uint[10];
        private Material _nearMaterial, _farMaterial;
        private int _cullKernel = -1, _clampKernel = -1, _slots;
        private static Mesh _tuft, _card;
        private static int _cpuFrame = -1;
        private static float _cpuThisFrame;
        private readonly System.Diagnostics.Stopwatch _watch = new System.Diagnostics.Stopwatch();

        private static readonly int ID_NEAR = Shader.PropertyToID("_Near"), ID_FAR = Shader.PropertyToID("_Far"), ID_ARGS = Shader.PropertyToID("_Args"), ID_CHUNKS = Shader.PropertyToID("_Chunks");
        private static readonly int ID_MAP = Shader.PropertyToID("_GrassMap"), ID_HEIGHTMAP = Shader.PropertyToID("_Heightmap");
        private static readonly int ID_ORIGIN = Shader.PropertyToID("_TerrainOrigin"), ID_SIZE = Shader.PropertyToID("_TerrainSize"), ID_GRID = Shader.PropertyToID("_Grid");
        private static readonly int ID_CAMERA = Shader.PropertyToID("_CameraPosition"), ID_LOD = Shader.PropertyToID("_Lod"), ID_STYLE = Shader.PropertyToID("_Style");
        private static readonly int ID_PLANES = Shader.PropertyToID("_FrustumPlanes"), ID_CAPACITY = Shader.PropertyToID("_Capacity"), ID_CAPACITY_FAR = Shader.PropertyToID("_CapacityFar");
        private static readonly int ID_INSTANCES = Shader.PropertyToID("_GrassInstances");
        private static readonly int ID_WIND = Shader.PropertyToID("_GoobyrWind"), ID_WIND_TIME = Shader.PropertyToID("_GoobyrWindTime");
        private static readonly int ID_TRAMPLE = Shader.PropertyToID("_GrassTrample"), ID_TRAMPLE_AREA = Shader.PropertyToID("_GrassTrampleArea");
        private static readonly int ID_GROUND_MAP = Shader.PropertyToID("_GoobyrGrassMap"), ID_GROUND_ON = Shader.PropertyToID("_GoobyrGrassOn");
        private static readonly int ID_GROUND_FAR = Shader.PropertyToID("_GoobyrGrassFar"), ID_GROUND_DRY = Shader.PropertyToID("_GoobyrGrassFarDry");

        /// <summary>Sets the field up (the builder): the terrain, the baked map and chunk heights, the shaders.</summary>
        public void Setup(Terrain pTerrain, Texture2D pMap, GrassStyle pStyle, float pChunkSize, int pChunks, Vector2[] pChunkHeights, Shader pShader, ComputeShader pCull, ComputeShader pTrample)
        {
            _terrain = pTerrain;
            _map = pMap;
            _style = pStyle;
            _chunkSize = pChunkSize;
            _chunks = pChunks;
            _chunkHeights = pChunkHeights;
            _shader = pShader;
            _cull = pCull;
            _trample = pTrample;
            Release();
            if (isActiveAndEnabled)
            {
                Prepare();
                PublishGround();
            }
        }

        private void OnEnable()
        {
            All.Add(this);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            Prepare();
            PublishGround();
        }

        private void OnDisable()
        {
            All.Remove(this);
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Release();
            if (All.Count > 0) All[All.Count - 1].PublishGround();
            else Shader.SetGlobalFloat(ID_GROUND_ON, 0f);
        }

        private void OnValidate()
        {
            if (_nearMaterial != null) ApplyStyle();
            if (isActiveAndEnabled) PublishGround();
        }

        /// <summary>
        /// Tells <c>Goobyr/TerrainLit</c> where this field grows (its map covers the terrain's UV square) and
        /// the meadow's colours from afar: the ground under and beyond the blades takes them, so nothing
        /// changes colour where the blades end, while lots, paths and rock keep their own. One field per
        /// scene: the last one enabled is the one published.
        /// </summary>
        private void PublishGround()
        {
            if (_map == null || _style == null) return;
            Shader.SetGlobalTexture(ID_GROUND_MAP, _map);
            Shader.SetGlobalVector(ID_GROUND_FAR, _style.FarColor);
            Shader.SetGlobalVector(ID_GROUND_DRY, _style.FarDryColor);
            Shader.SetGlobalFloat(ID_GROUND_ON, 1f);
        }

        private bool Ready => _terrain != null && _terrain.terrainData != null && _map != null && _shader != null && _cull != null && _chunkHeights != null && _chunkHeights.Length == _chunks * _chunks && _chunks > 0;

        private void Prepare()
        {
            if (!Ready || _nearMaterial != null) return;
            _cullKernel = _cull.FindKernel("Cull");
            _clampKernel = _cull.FindKernel("Clamp");
            _slots = Mathf.Max(1, Mathf.RoundToInt(_chunkSize / Spacing));
            _visible = new uint[_chunks * _chunks];
            _nearMaterial = new Material(_shader) { name = "Grass Near", hideFlags = HideFlags.HideAndDontSave };
            _farMaterial = new Material(_shader) { name = "Grass Far", hideFlags = HideFlags.HideAndDontSave };
            _farPrepass = !FarPrepass;
            _farMotion = !FarMotion;
            ApplyStyle();
            if (_tuft == null) _tuft = GrassMeshes.Tuft();
            if (_card == null) _card = GrassMeshes.Card();
            _argsReset[0] = _tuft.GetIndexCount(0);
            _argsReset[5] = _card.GetIndexCount(0);
        }

        private void ApplyStyle()
        {
            foreach (Material m in new[] { _nearMaterial, _farMaterial })
            {
                if (m == null) continue;
                // The flat look: fat leaves in clean colours, roots barely darker, no shine.
                Color lTip = Color.Lerp(_style.tip, Color.white, 0.22f);
                m.SetColor("_RootColor", Color.Lerp(lTip, Color.black, 0.28f));
                m.SetColor("_TipColor", lTip);
                m.SetColor("_DryColor", Color.Lerp(_style.dry, Color.white, 0.2f));
                m.SetColor("_TranslucencyColor", _style.translucency);
                m.SetFloat("_BladeWidth", _style.bladeWidth * 3.4f);
                m.SetFloat("_TuftRadius", _style.tuftRadius * 1.5f);
                m.SetFloat("_WindScale", _style.wind);
                m.SetFloat("_Translucency", 0.15f);
                m.SetFloat("_Gloss", 0f);
            }
        }

        private void Release()
        {
            foreach (View v in _views.Values) ReleaseView(v);
            _views.Clear();
            if (_nearMaterial != null) DestroyImmediate(_nearMaterial);
            if (_farMaterial != null) DestroyImmediate(_farMaterial);
            _nearMaterial = _farMaterial = null;
        }

        private static void ReleaseView(View v)
        {
            v.near?.Release();
            v.far?.Release();
            v.args?.Release();
            v.chunks?.Release();
            v.commands?.Release();
        }

        private View ViewFor(Camera pCamera)
        {
            if (_views.TryGetValue(pCamera, out View v)) return v;
            v = new View
            {
                near = new GraphicsBuffer(GraphicsBuffer.Target.Structured, NEAR_CAPACITY, STRIDE),
                far = new GraphicsBuffer(GraphicsBuffer.Target.Structured, FAR_CAPACITY, STRIDE),
                args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, 10, sizeof(uint)),
                chunks = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _chunks * _chunks, sizeof(uint)),
                nearProps = new MaterialPropertyBlock(),
                farProps = new MaterialPropertyBlock(),
                commands = new CommandBuffer { name = "Grass" }
            };
            v.nearProps.SetBuffer(ID_INSTANCES, v.near);
            v.farProps.SetBuffer(ID_INSTANCES, v.far);
            _views[pCamera] = v;
            return v;
        }

        /// <summary>Drops the buffers of cameras that no longer exist (shot cameras come and go).</summary>
        private void Sweep()
        {
            _stale.Clear();
            foreach (KeyValuePair<Camera, View> lPair in _views)
                if (lPair.Key == null || Time.frameCount - lPair.Value.frame > 600) _stale.Add(lPair.Key);
            foreach (Camera c in _stale)
            {
                ReleaseView(_views[c]);
                _views.Remove(c);
            }
        }

        private void OnBeginCamera(ScriptableRenderContext pContext, Camera pCamera)
        {
            if (!Enabled || !Ready) return;
            if (pCamera.cameraType == CameraType.Preview || pCamera.cameraType == CameraType.Reflection) return;
            if ((pCamera.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (pCamera.TryGetComponent(out UniversalAdditionalCameraData lData) && lData.renderType == CameraRenderType.Overlay) return;
            if (OnlyCamera != null && pCamera != OnlyCamera) return;
            // A camera thrown somewhere absurd (NaN, a ragdoll flung away) draws no grass: its bounds would
            // be invalid for the culling.
            if (!IsSane(pCamera.transform.position) || !(pCamera.farClipPlane > 0f)) return;
            if (_nearMaterial == null) Prepare();
            _watch.Restart();

            int lCount = CullChunks(pCamera, out Bounds lBounds);
            if (lCount > 0 && IsSane(lBounds.center) && IsSane(lBounds.size)) Submit(pContext, pCamera, lCount, lBounds);

            _watch.Stop();
            float lMs = (float)_watch.Elapsed.TotalMilliseconds;
            CpuMilliseconds = Mathf.Lerp(CpuMilliseconds, lMs, 0.1f);
            if (_cpuFrame != Time.frameCount)
            {
                FrameCpuMilliseconds = Mathf.Lerp(FrameCpuMilliseconds, _cpuThisFrame, 0.1f);
                _cpuFrame = Time.frameCount;
                _cpuThisFrame = 0f;
                if (Time.frameCount % 300 == 0) Sweep();
            }
            _cpuThisFrame += lMs;
        }

        /// <summary>The chunks with grass that touch the camera's frustum within the grass distance.</summary>
        private int CullChunks(Camera pCamera, out Bounds pBounds)
        {
            pBounds = default;
            Vector3 c = pCamera.transform.position;
            float lFar = Mathf.Min(FarDistance, pCamera.farClipPlane);
            GeometryUtility.CalculateFrustumPlanes(pCamera, _planes);
            Vector3 o = _terrain.transform.position;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - lFar - o.x) / _chunkSize)), x1 = Mathf.Min(_chunks - 1, Mathf.FloorToInt((c.x + lFar - o.x) / _chunkSize));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.z - lFar - o.z) / _chunkSize)), z1 = Mathf.Min(_chunks - 1, Mathf.FloorToInt((c.z + lFar - o.z) / _chunkSize));
            int n = 0;
            float lTop = Mathf.Max(_style.height * 1.6f, _style.tallHeight * 1.25f) + 0.5f;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 h = _chunkHeights[z * _chunks + x];
                    if (h.x > h.y) continue;
                    Vector3 lMin = new Vector3(o.x + x * _chunkSize - 1f, h.x - 0.3f, o.z + z * _chunkSize - 1f);
                    Vector3 lMax = new Vector3(lMin.x + _chunkSize + 2f, h.y + lTop, lMin.z + _chunkSize + 2f);
                    Bounds b = new Bounds((lMin + lMax) * 0.5f, lMax - lMin);
                    if (b.SqrDistance(c) > lFar * lFar) continue;
                    if (!GeometryUtility.TestPlanesAABB(_planes, b)) continue;
                    _visible[n++] = (uint)x | ((uint)z << 16);
                    if (n == 1) pBounds = b; else pBounds.Encapsulate(b);
                }
            return n;
        }

        private void Submit(ScriptableRenderContext pContext, Camera pCamera, int pCount, Bounds pBounds)
        {
            View v = ViewFor(pCamera);
            v.frame = Time.frameCount;
            if (_farPrepass != FarPrepass)
            {
                // The far cards in the depth(-normals) prepass or not (then they are shaded with overdraw).
                _farPrepass = FarPrepass;
                _farMaterial.SetShaderPassEnabled("DepthNormals", _farPrepass);
                _farMaterial.SetShaderPassEnabled("DepthOnly", _farPrepass);
            }
            if (_farMotion != FarMotion)
            {
                _farMotion = FarMotion;
                _farMaterial.SetShaderPassEnabled("MotionVectors", _farMotion);
            }
            TerrainData lData = _terrain.terrainData;
            Vector3 o = _terrain.transform.position;
            for (int i = 0; i < 6; i++) _planeVectors[i] = new Vector4(_planes[i].normal.x, _planes[i].normal.y, _planes[i].normal.z, _planes[i].distance);

            CommandBuffer cmd = v.commands;
            cmd.Clear();
            if (_sampler == null) _sampler = UnityEngine.Profiling.CustomSampler.Create("GrassCull", true);
            cmd.BeginSample(_sampler);
            cmd.SetBufferData(v.args, _argsReset);
            cmd.SetBufferData(v.chunks, _visible, 0, 0, pCount);
            cmd.SetComputeVectorParam(_cull, ID_ORIGIN, new Vector4(o.x, o.y, o.z, lData.size.y / MAX_HEIGHT));
            cmd.SetComputeVectorParam(_cull, ID_SIZE, new Vector4(lData.size.x, lData.size.z, lData.heightmapResolution, 0f));
            cmd.SetComputeVectorParam(_cull, ID_GRID, new Vector4(_chunkSize, _slots, _chunkSize / _slots, _style.density));
            Vector3 c = pCamera.transform.position;
            cmd.SetComputeVectorParam(_cull, ID_CAMERA, new Vector4(c.x, c.y, c.z, Mathf.Min(FarDistance, pCamera.farClipPlane)));
            cmd.SetComputeVectorParam(_cull, ID_LOD, new Vector4(CardDistance, ThinStart, ThinExponent, FewestKept));
            cmd.SetComputeVectorParam(_cull, ID_STYLE, new Vector4(_style.height, _style.heightVariation, _style.tallHeight, _style.wind));
            cmd.SetComputeVectorParam(_cull, ID_WIND, new Vector4(Wind.Direction.x, Wind.Direction.y, Wind.Strength, Wind.Gustiness));
            cmd.SetComputeVectorParam(_cull, ID_WIND_TIME, new Vector4(Wind.Time, Wind.PreviousTime, Wind.GustSpeed, Wind.GustSize));
            Texture lTrample = GrassTrample.Current;
            cmd.SetComputeTextureParam(_cull, _cullKernel, ID_TRAMPLE, lTrample != null ? lTrample : Texture2D.blackTexture);
            cmd.SetComputeVectorParam(_cull, ID_TRAMPLE_AREA, lTrample != null ? GrassTrample.Area : Vector4.zero);
            cmd.SetComputeVectorArrayParam(_cull, ID_PLANES, _planeVectors);
            cmd.SetComputeIntParam(_cull, ID_CAPACITY, NEAR_CAPACITY);
            cmd.SetComputeIntParam(_cull, ID_CAPACITY_FAR, FAR_CAPACITY);
            cmd.SetComputeBufferParam(_cull, _cullKernel, ID_NEAR, v.near);
            cmd.SetComputeBufferParam(_cull, _cullKernel, ID_FAR, v.far);
            cmd.SetComputeBufferParam(_cull, _cullKernel, ID_ARGS, v.args);
            cmd.SetComputeBufferParam(_cull, _cullKernel, ID_CHUNKS, v.chunks);
            cmd.SetComputeTextureParam(_cull, _cullKernel, ID_MAP, _map);
            cmd.SetComputeTextureParam(_cull, _cullKernel, ID_HEIGHTMAP, lData.heightmapTexture);
            int lGroups = (_slots + 7) / 8;
            cmd.DispatchCompute(_cull, _cullKernel, lGroups, lGroups, pCount);
            cmd.SetComputeBufferParam(_cull, _clampKernel, ID_ARGS, v.args);
            cmd.DispatchCompute(_cull, _clampKernel, 1, 1, 1);
            cmd.EndSample(_sampler);
            if (CountInstances)
                cmd.RequestAsyncReadback(v.args, r =>
                {
                    if (r.hasError) return;
                    Unity.Collections.NativeArray<uint> lArgs = r.GetData<uint>();
                    LastNear = (int)lArgs[1];
                    LastFar = (int)lArgs[6];
                });
            pContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            RenderParams lParams = new RenderParams(_nearMaterial)
            {
                camera = pCamera,
                worldBounds = pBounds,
                matProps = v.nearProps,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                motionVectorMode = MotionVectorGenerationMode.Object
            };
            Graphics.RenderMeshIndirect(lParams, _tuft, v.args, 1, 0);
            lParams.material = _farMaterial;
            lParams.matProps = v.farProps;
            Graphics.RenderMeshIndirect(lParams, _card, v.args, 1, 1);
        }
    }

    /// <summary>The two blade meshes of the grass, generated once (attributes read by <c>Goobyr/Grass</c>).</summary>
    public static class GrassMeshes
    {
        /// <summary>A near tuft: 10 curved blades fanning out; the first four (lowest rank) cover the tuft on their own.</summary>
        public static Mesh Tuft() => Build("Grass Tuft", 10, new[] { 0f, 0.38f, 0.72f }, 1f, 11);

        /// <summary>A far card: 4 wider blades, one tapered triangle each (their curve is lost past 20 m anyway).</summary>
        public static Mesh Card() => Build("Grass Card", 4, new[] { 0f }, 1.55f, 23);

        private static Mesh Build(string pName, int pBlades, float[] pRows, float pWidth, int pSeed)
        {
            System.Random r = new System.Random(pSeed);
            float R() => (float)r.NextDouble();
            List<Vector3> lPositions = new List<Vector3>();
            List<Vector4> lBlade = new List<Vector4>(), lPlace = new List<Vector4>(), lForm = new List<Vector4>();
            List<int> lIndices = new List<int>();
            for (int b = 0; b < pBlades; b++)
            {
                // Golden-angle spread: any first k blades already surround the tuft.
                float lAngle = b * 2.39996f + R() * 0.4f;
                float lRadius = b == 0 ? 0.15f : 0.35f + 0.65f * R();
                Vector2 lOffset = new Vector2(Mathf.Cos(lAngle), Mathf.Sin(lAngle)) * lRadius;
                // Each blade faces round the tuft and leans outward, a little at random.
                float lFacing = (lAngle + (R() - 0.5f) * 1.2f) / (Mathf.PI * 2f);
                float lRank = (b + 0.5f) / pBlades;
                float lHeight = b < pBlades / 2 ? 0.8f + 0.2f * R() : 0.6f + 0.35f * R();
                float lWidth = (0.8f + 0.4f * R()) * pWidth;
                Vector4 lPlaceValue = new Vector4(lOffset.x, lOffset.y, lHeight, lWidth);
                Vector4 lFormValue = new Vector4(0.12f + 0.35f * lRadius + 0.1f * R(), 0.1f + 0.5f * R(), R(), 0f);
                int lStart = lPositions.Count;
                foreach (float t in pRows)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        lPositions.Add(new Vector3(lOffset.x * 0.15f, t, lOffset.y * 0.15f));
                        lBlade.Add(new Vector4(s, t, lRank, lFacing));
                        lPlace.Add(lPlaceValue);
                        lForm.Add(lFormValue);
                    }
                lPositions.Add(new Vector3(lOffset.x * 0.15f, 1f, lOffset.y * 0.15f));
                lBlade.Add(new Vector4(0f, 1f, lRank, lFacing));
                lPlace.Add(lPlaceValue);
                lForm.Add(lFormValue);
                for (int k = 0; k < pRows.Length - 1; k++)
                {
                    int a = lStart + k * 2;
                    lIndices.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
                int lLast = lStart + (pRows.Length - 1) * 2;
                lIndices.AddRange(new[] { lLast, lLast + 2, lLast + 1 });
            }
            Mesh lMesh = new Mesh { name = pName, hideFlags = HideFlags.HideAndDontSave };
            lMesh.SetVertices(lPositions);
            lMesh.SetUVs(0, lBlade);
            lMesh.SetUVs(1, lPlace);
            lMesh.SetUVs(2, lForm);
            lMesh.SetIndices(lIndices, MeshTopology.Triangles, 0);
            lMesh.bounds = new Bounds(Vector3.up, Vector3.one * 4f);
            return lMesh;
        }
    }
}
