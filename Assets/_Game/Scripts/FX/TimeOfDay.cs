using System;
using Com.IsartDigital.GoobyrFPS.FX.Rendering;
using Com.IsartDigital.GoobyrFPS.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>One lighting state of a map: sun, sky, fog, exposure, colour grade and wind.</summary>
    [Serializable]
    public struct TimePreset
    {
        /// <summary>Sun (or moon) elevation and heading in degrees.</summary>
        public Vector2 sun;
        public Color sunColor;
        public float sunIntensity;
        public Color zenith, horizon, haze, cloudLit, cloudShade;
        [Range(0f, 1f)] public float cloudCoverage;
        public float fogDensity;
        public Color fogAmbient;
        public float fogSun;
        public float exposure;
        /// <summary>0 = street lamps off, 1 = full (night).</summary>
        [Range(0f, 1f)] public float lamps;
        /// <summary>How much of the sky lights the world (ambient).</summary>
        public float ambient;

        /// <summary>White balance: temperature (negative cooler, positive warmer) and tint (negative greener, positive more magenta).</summary>
        [Header("Grade (HDR, before the ACES tonemapper)")]
        public float temperature, tint;
        public float saturation, contrast;
        /// <summary>Split toning: colour multipliers of the shadows and of the highlights (white = neutral).</summary>
        public Color shadows, highlights;

        /// <summary>0 calm, 0.5 a breeze, 1 a strong wind.</summary>
        [Header("Wind")]
        public float wind;
    }

    /// <summary>
    /// The contract's time of day, picked by the host in the briefing
    /// (<see cref="LobbyState.TimeOfDay"/>) and applied on every machine: the sun's angle and colour
    /// (a cold moon at night), the sky's gradient and clouds, ambient light and reflections taken
    /// from that sky (and local reflection probes, <see cref="World.RoomProbes"/>), the volumetric fog's
    /// density and glow, the exposure, the colour grade, the wind and the street lamps.
    /// <para>
    /// The look of each biome lives in <see cref="PresetsFor"/>: the presets are rebuilt from it when
    /// the map loads, so a change to a look reaches every map without rebuilding them. Maps built
    /// before the presets carried a grade only stored the presets; their biome is recognised from the
    /// noon sun.
    /// </para>
    /// </summary>
    public class TimeOfDay : MonoBehaviour
    {
        public static readonly string[] Names = { "DAWN", "NOON", "DUSK", "NIGHT" };

        [SerializeField] private TimePreset[] _presets = new TimePreset[4];
        [SerializeField] private Light _sun;
        [SerializeField] private Material _sky;
        [SerializeField] private bool _hasBiome;
        [SerializeField] private Biome _biome;
        [SerializeField] private float _heading;

        public static TimeOfDay Instance { get; private set; }
        public int Current { get; private set; } = -1;
        public Biome MapBiome => _biome;
        /// <summary>The camera has a roof over it and walls around it.</summary>
        public bool Indoors { get; private set; }

        private Material _skyInstance;
        private Volume _volume;
        private GoobyrAtmosphere _atmosphere;
        private ColorAdjustments _adjust;
        private WhiteBalance _balance;
        private ShadowsMidtonesHighlights _split;
        private bool _fresh;

        /// <summary>Sets the map's sun, sky and biome look (presets rebuilt from <see cref="PresetsFor"/>).</summary>
        public void Setup(Light pSun, Material pSky, Biome pBiome, float pHeading)
        {
            _sun = pSun;
            _sky = pSky;
            _biome = pBiome;
            _heading = pHeading;
            _hasBiome = true;
            _presets = PresetsFor(pBiome, pHeading);
            _fresh = true;
        }

        /// <summary>Sets explicit presets; when they are a biome's (<see cref="PresetsFor"/>), the biome is remembered so later looks reach this map.</summary>
        public void Setup(Light pSun, Material pSky, TimePreset[] pPresets)
        {
            _sun = pSun;
            _sky = pSky;
            _presets = pPresets;
            _hasBiome = Recognise(pPresets, out _biome, out _heading);
            _fresh = true;
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_skyInstance != null) Destroy(_skyInstance);
            if (_volume != null && _volume.profile != null) Destroy(_volume.profile);
            if (LobbyState.Instance != null) LobbyState.Instance.TimeOfDay.OnValueChanged -= HandleChanged;
        }

        private void Start()
        {
            if (LobbyState.Instance != null) LobbyState.Instance.TimeOfDay.OnValueChanged += HandleChanged;
            // Local reflections: probes per building floor and round the streets, captured at load.
            if (GetComponent<World.RoomProbes>() == null) gameObject.AddComponent<World.RoomProbes>();
            Apply(LobbyState.Instance != null ? LobbyState.Instance.TimeOfDay.Value : 1);
        }

        private void HandleChanged(int pOld, int pNew) => Apply(pNew);

        /// <summary>Rebuilds the presets from the biome's current look (once per load).</summary>
        private void Refresh()
        {
            if (_fresh) return;
            _fresh = true;
            if (!_hasBiome) _hasBiome = Recognise(_presets, out _biome, out _heading);
            if (_hasBiome) _presets = PresetsFor(_biome, _heading);
        }

        public void Apply(int pIndex)
        {
            Refresh();
            if (_presets == null || _presets.Length == 0) return;
            pIndex = Mathf.Clamp(pIndex, 0, _presets.Length - 1);
            Current = pIndex;
            TimePreset p = _presets[pIndex];

            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(p.sun.x, p.sun.y, 0f);
                _sun.color = p.sunColor;
                _sun.intensity = p.sunIntensity;
                _sun.shadowStrength = pIndex == 3 ? 0.7f : 0.9f;
                RenderSettings.sun = _sun;
            }

            if (_sky != null)
            {
                if (_skyInstance == null) _skyInstance = new Material(_sky);
                _skyInstance.SetColor("_ZenithColor", p.zenith);
                _skyInstance.SetColor("_HorizonColor", p.horizon);
                _skyInstance.SetColor("_HazeColor", p.haze);
                _skyInstance.SetColor("_SunColor", p.sunColor);
                _skyInstance.SetColor("_CloudColor", p.cloudLit);
                _skyInstance.SetColor("_CloudShade", p.cloudShade);
                _skyInstance.SetFloat("_CloudCoverage", p.cloudCoverage);
                _skyInstance.SetFloat("_SunSize", pIndex == 3 ? 0.012f : 0.006f);
                RenderSettings.skybox = _skyInstance;
            }

            // Ambient light and reflections come from that sky.
            RenderSettings.ambientMode = AmbientMode.Skybox;
            _ambient = p.ambient;
            RenderSettings.ambientIntensity = p.ambient * _indoor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(p.ambient) * _indoor;
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();

            EnsureVolume();
            _atmosphere.density.Override(p.fogDensity);
            _atmosphere.ambientColor.Override(p.fogAmbient);
            _atmosphere.sunScattering.Override(p.fogSun);
            _adjust.postExposure.Override(p.exposure);
            _adjust.contrast.Override(p.contrast);
            _adjust.saturation.Override(p.saturation);
            _balance.temperature.Override(p.temperature);
            _balance.tint.Override(p.tint);
            _split.shadows.Override(new Vector4(p.shadows.r, p.shadows.g, p.shadows.b, 0f));
            _split.highlights.Override(new Vector4(p.highlights.r, p.highlights.g, p.highlights.b, 0f));

            // The reflections are captured again under the new light.
            if (World.RoomProbes.Instance != null) World.RoomProbes.Instance.Refresh();

            // The prevailing wind of the biome, from the map's sun heading.
            Vector3 lWind = Quaternion.Euler(0f, _heading + 35f, 0f) * Vector3.forward;
            World.Wind.Set(new Vector2(lWind.x, lWind.z), p.wind, _biome == Biome.Jungle ? 0.55f : 0.8f);

            foreach (Light lLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (lLight == _sun || lLight.type == LightType.Directional) continue;
                if (!lLight.name.StartsWith("Lamp")) continue;
                if (!_lampIntensity.TryGetValue(lLight, out float lAuthored)) _lampIntensity[lLight] = lAuthored = lLight.intensity;
                lLight.enabled = p.lamps > 0.01f;
                lLight.intensity = lAuthored * p.lamps;
            }
        }

        private float _ambient = 1f, _indoor = 1f, _appliedIndoor = 1f;

        /// <summary>
        /// Indoors (a roof right over the camera) the sky's ambient light is mostly shut out: without
        /// it, generated interiors would glow sky-blue. Smoothed, and the environment refreshed only
        /// when it changed noticeably.
        /// </summary>
        private void LateUpdate()
        {
            Camera lCamera = Camera.main;
            if (lCamera == null || Current < 0) return;
            Vector3 p = lCamera.transform.position;
            bool lRoof = Physics.Raycast(p, Vector3.up, 14f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            int lWalls = 0;
            if (lRoof)
                foreach (Vector3 d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    if (Physics.Raycast(p, d, 18f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) lWalls++;
            Indoors = lRoof && lWalls >= 3;
            // Barely less than outdoors: walking into a building is never walking into the dark.
            _indoor = Mathf.MoveTowards(_indoor, lRoof && lWalls >= 3 ? 0.9f : 1f, Time.deltaTime * 1.5f);
            if (Mathf.Abs(_indoor - _appliedIndoor) < 0.05f && !(_indoor == 1f && _appliedIndoor != 1f)) return;
            _appliedIndoor = _indoor;
            RenderSettings.ambientIntensity = _ambient * _indoor;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(_ambient) * _indoor;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Each street lamp's authored intensity, remembered the first time it is dimmed.</summary>
        private readonly System.Collections.Generic.Dictionary<Light, float> _lampIntensity = new System.Collections.Generic.Dictionary<Light, float>();

        private void EnsureVolume()
        {
            if (_volume != null) return;
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 5f;
            VolumeProfile lProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _atmosphere = lProfile.Add<GoobyrAtmosphere>(false);
            _adjust = lProfile.Add<ColorAdjustments>(false);
            _balance = lProfile.Add<WhiteBalance>(false);
            _split = lProfile.Add<ShadowsMidtonesHighlights>(false);
            _volume.profile = lProfile;
        }

        // ---------------------------------------------------------------- biome presets

        public enum Biome { Jungle, Desert, Snow }

        /// <summary>Elevation and heading offset of each biome's noon sun (how older maps are recognised).</summary>
        private static readonly Vector2[] NOON = { new Vector2(62f, 20f), new Vector2(68f, 15f), new Vector2(32f, 10f) };

        /// <summary>The biome (and the map's sun heading) whose presets these are, from their noon sun.</summary>
        private static bool Recognise(TimePreset[] pPresets, out Biome pBiome, out float pHeading)
        {
            pBiome = Biome.Jungle;
            pHeading = 0f;
            if (pPresets == null || pPresets.Length < 2) return false;
            for (int b = 0; b < NOON.Length; b++)
            {
                if (Mathf.Abs(pPresets[1].sun.x - NOON[b].x) > 0.5f) continue;
                pBiome = (Biome)b;
                pHeading = pPresets[1].sun.y - NOON[b].y;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Dawn, noon, dusk and night for a biome: jungle warm and humid (soft haze, green-gold
        /// light), desert golden (dry glare, cool shadows), snow cool and crisp (clean whites, blue
        /// shadows). The grade keeps each biome's identity at every hour; dawn and dusk stay bright
        /// enough to play, the night is moonlit and readable.
        /// </summary>
        public static TimePreset[] PresetsFor(Biome pBiome, float pHeading)
        {
            Vector2 lNoon = NOON[(int)pBiome];
            TimePreset Make(float pElevation, float pYaw, Color pSun, float pIntensity, Color pZenith, Color pHorizon, Color pHaze, float pCover, float pFog, Color pFogAmbient, float pFogSun, float pExposure, float pLamps, float pAmbient) => new TimePreset
            {
                sun = new Vector2(pElevation, pHeading + pYaw), sunColor = pSun, sunIntensity = pIntensity,
                zenith = pZenith, horizon = pHorizon, haze = pHaze,
                cloudLit = Color.Lerp(Color.white, pSun, 0.5f), cloudShade = Color.Lerp(pZenith, pHorizon, 0.5f) * 0.75f,
                cloudCoverage = pCover, fogDensity = pFog, fogAmbient = pFogAmbient, fogSun = pFogSun,
                exposure = pExposure, lamps = pLamps, ambient = pAmbient
            };
            TimePreset[] lPresets;
            switch (pBiome)
            {
                case Biome.Jungle:
                    lPresets = new[]
                    {
                        Make(9f, -80f, new Color(1f, 0.76f, 0.56f), 2.4f, new Color(0.36f, 0.5f, 0.72f), new Color(0.98f, 0.82f, 0.66f), new Color(1f, 0.74f, 0.5f), 0.45f, 0.018f, new Color(0.62f, 0.64f, 0.6f), 2.4f, 0.75f, 0.6f, 1.05f),
                        Make(lNoon.x, lNoon.y, new Color(1f, 0.96f, 0.88f), 2.8f, new Color(0.24f, 0.47f, 0.82f), new Color(0.8f, 0.87f, 0.9f), new Color(1f, 0.93f, 0.8f), 0.55f, 0.007f, new Color(0.64f, 0.72f, 0.74f), 1.4f, 0.25f, 0f, 1.05f),
                        Make(8f, 95f, new Color(1f, 0.62f, 0.38f), 2.5f, new Color(0.32f, 0.36f, 0.6f), new Color(1f, 0.66f, 0.46f), new Color(1f, 0.56f, 0.34f), 0.5f, 0.014f, new Color(0.62f, 0.54f, 0.48f), 2.8f, 0.7f, 0.8f, 1f),
                        Make(38f, 160f, new Color(0.6f, 0.7f, 1f), 0.45f, new Color(0.03f, 0.05f, 0.1f), new Color(0.1f, 0.13f, 0.2f), new Color(0.32f, 0.38f, 0.52f), 0.35f, 0.01f, new Color(0.09f, 0.12f, 0.17f), 1.2f, 1.25f, 1f, 0.8f)
                    };
                    break;
                case Biome.Desert:
                    lPresets = new[]
                    {
                        Make(7f, -85f, new Color(1f, 0.74f, 0.52f), 2.6f, new Color(0.38f, 0.54f, 0.78f), new Color(1f, 0.82f, 0.66f), new Color(1f, 0.74f, 0.5f), 0.15f, 0.009f, new Color(0.76f, 0.68f, 0.6f), 2.2f, 0.6f, 0.5f, 1.05f),
                        Make(lNoon.x, lNoon.y, new Color(1f, 0.95f, 0.85f), 3.1f, new Color(0.2f, 0.44f, 0.85f), new Color(0.9f, 0.88f, 0.82f), new Color(1f, 0.95f, 0.85f), 0.08f, 0.004f, new Color(0.85f, 0.8f, 0.72f), 1f, -0.1f, 0f, 1.05f),
                        Make(7f, 92f, new Color(1f, 0.58f, 0.34f), 2.6f, new Color(0.34f, 0.38f, 0.62f), new Color(1f, 0.64f, 0.44f), new Color(1f, 0.54f, 0.3f), 0.2f, 0.008f, new Color(0.72f, 0.56f, 0.46f), 2.6f, 0.6f, 0.8f, 1f),
                        Make(42f, 170f, new Color(0.62f, 0.7f, 1f), 0.5f, new Color(0.03f, 0.05f, 0.1f), new Color(0.11f, 0.13f, 0.2f), new Color(0.32f, 0.36f, 0.52f), 0.05f, 0.004f, new Color(0.1f, 0.11f, 0.17f), 1f, 1.2f, 1f, 0.8f)
                    };
                    break;
                default:
                    lPresets = new[]
                    {
                        Make(6f, -75f, new Color(1f, 0.8f, 0.66f), 2.2f, new Color(0.38f, 0.5f, 0.74f), new Color(1f, 0.86f, 0.78f), new Color(1f, 0.8f, 0.66f), 0.4f, 0.012f, new Color(0.76f, 0.78f, 0.84f), 2.2f, 0.45f, 0.6f, 1.05f),
                        Make(lNoon.x, lNoon.y, new Color(1f, 0.97f, 0.94f), 2.4f, new Color(0.3f, 0.5f, 0.8f), new Color(0.86f, 0.9f, 0.95f), new Color(1f, 0.97f, 0.92f), 0.5f, 0.006f, new Color(0.8f, 0.84f, 0.9f), 1.2f, -0.25f, 0f, 1.1f),
                        Make(6f, 100f, new Color(1f, 0.66f, 0.52f), 2.2f, new Color(0.32f, 0.38f, 0.62f), new Color(1f, 0.72f, 0.64f), new Color(1f, 0.64f, 0.52f), 0.45f, 0.011f, new Color(0.74f, 0.68f, 0.7f), 2.4f, 0.45f, 0.8f, 1f),
                        Make(35f, 165f, new Color(0.62f, 0.72f, 1f), 0.55f, new Color(0.03f, 0.05f, 0.1f), new Color(0.12f, 0.15f, 0.22f), new Color(0.36f, 0.42f, 0.56f), 0.3f, 0.009f, new Color(0.12f, 0.14f, 0.2f), 1.1f, 1.1f, 1f, 0.85f)
                    };
                    break;
            }
            Grade(pBiome, lPresets);
            Stylise(pBiome, lPresets);
            return lPresets;
        }

        /// <summary>The flat look's skies and grade: pastel, airy, soft contrast; the sun's strength no longer sets the exposure.</summary>
        private static void Stylise(Biome pBiome, TimePreset[] pPresets)
        {
            for (int i = 0; i < pPresets.Length; i++)
            {
                TimePreset p = pPresets[i];
                p.saturation = i == 3 ? 6f : 8f;
                p.contrast = -4f;
                p.temperature *= 0.4f;
                p.tint = 0f;
                p.shadows = Color.white;
                p.highlights = Color.white;
                p.exposure = i == 3 ? 0.35f : 0f;
                // Barely any haze, and a sun that lights the terrain and the grass as gently as the flat shader.
                p.fogDensity *= 0.1f;
                p.fogAmbient = Color.Lerp(p.fogAmbient, Color.white, 0.5f);
                p.sunIntensity = i == 3 ? 0.65f : 1.1f;
                p.cloudCoverage = pBiome == Biome.Jungle ? 0.72f : Mathf.Min(p.cloudCoverage, 0.38f);
                p.ambient = i == 3 ? 1.5f : 1.15f;
                if (i == 1)
                {
                    p.zenith = pBiome == Biome.Desert ? new Color(0.44f, 0.72f, 0.86f) : pBiome == Biome.Jungle ? new Color(0.4f, 0.76f, 0.8f) : new Color(0.5f, 0.76f, 0.88f);
                    p.horizon = pBiome == Biome.Desert ? new Color(0.95f, 0.9f, 0.76f) : new Color(0.86f, 0.95f, 0.93f);
                    p.haze = Color.Lerp(p.horizon, Color.white, 0.4f);
                    if (pBiome == Biome.Jungle)
                    {
                        // Rain clouds: a grey-green sky, the sun behind them.
                        p.zenith = new Color(0.52f, 0.63f, 0.64f);
                        p.horizon = new Color(0.76f, 0.83f, 0.8f);
                        p.haze = new Color(0.8f, 0.85f, 0.82f);
                        p.sunIntensity = 0.82f;
                        p.fogDensity *= 2.5f;
                    }
                }
                else if (i == 2)
                {
                    p.zenith = new Color(0.56f, 0.56f, 0.72f);
                    p.horizon = new Color(1f, 0.78f, 0.56f);
                    p.haze = new Color(1f, 0.82f, 0.6f);
                    p.sunColor = new Color(1f, 0.74f, 0.5f);
                }
                else if (i == 3)
                {
                    p.zenith = new Color(0.08f, 0.13f, 0.3f);
                    p.horizon = new Color(0.2f, 0.3f, 0.5f);
                    p.haze = new Color(0.3f, 0.4f, 0.62f);
                    p.sunColor = new Color(0.7f, 0.8f, 1f);
                }
                p.cloudLit = Color.Lerp(Color.white, p.sunColor, 0.3f);
                p.cloudShade = Color.Lerp(p.zenith, p.horizon, 0.6f);
                pPresets[i] = p;
            }
        }

        /// <summary>The colour grade and the wind of each preset: the biome's identity, nudged per hour.</summary>
        private static void Grade(Biome pBiome, TimePreset[] pPresets)
        {
            // Biome identity: white balance, tint, saturation, contrast, shadow and highlight tones, wind.
            float lTemperature, lTint, lSaturation, lContrast, lWind;
            Color lShadows, lHighlights;
            switch (pBiome)
            {
                case Biome.Jungle:
                    lTemperature = 4f; lTint = 0f; lSaturation = 13f; lContrast = 14f; lWind = 0.35f;
                    lShadows = new Color(0.95f, 1f, 1f); lHighlights = new Color(1.03f, 1.01f, 0.95f);
                    break;
                case Biome.Desert:
                    // Calm: ochre stone and sand are saturated already; a neutral-cool shadow keeps them from banding orange.
                    lTemperature = 3f; lTint = 0f; lSaturation = -8f; lContrast = 6f; lWind = 0.5f;
                    lShadows = new Color(0.97f, 0.99f, 1.05f); lHighlights = new Color(1.02f, 1.01f, 0.98f);
                    break;
                default:
                    lTemperature = -4f; lTint = 1f; lSaturation = 6f; lContrast = 15f; lWind = 0.6f;
                    lShadows = new Color(0.95f, 0.98f, 1.06f); lHighlights = new Color(1f, 1f, 1.01f);
                    break;
            }
            // Per hour: dawn and dusk warmer and less magenta, the night cooler, desaturated, with lifted blue shadows.
            float[] lWarm = { 3f, 0f, 4f, -8f }, lGreen = { -3f, 0f, -5f, 0f }, lSat = { 0f, 0f, 2f, -18f }, lCon = { -2f, 0f, -1f, -6f }, lGust = { 0.7f, 1f, 0.8f, 0.6f };
            for (int i = 0; i < pPresets.Length && i < 4; i++)
            {
                // Dawn and dusk are back-lit half the time: more sky light in the shade, a little more
                // exposure, warm lifted shadows, so the ground under a tree against the sun still reads.
                if (i == 0 || i == 2)
                {
                    pPresets[i].ambient *= 1.3f;
                    pPresets[i].exposure += 0.12f;
                }
                pPresets[i].temperature = lTemperature + lWarm[i];
                pPresets[i].tint = lTint + lGreen[i];
                pPresets[i].saturation = lSaturation + lSat[i];
                pPresets[i].contrast = lContrast + lCon[i];
                pPresets[i].shadows = i == 3 ? new Color(0.92f, 0.98f, 1.12f) : i == 1 ? lShadows : lShadows * new Color(1.1f, 1.06f, 1.02f);
                pPresets[i].highlights = i == 3 ? new Color(0.98f, 1f, 1.04f) : lHighlights;
                pPresets[i].wind = lWind * lGust[i];
            }
        }
    }
}
