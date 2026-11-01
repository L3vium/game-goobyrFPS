using System;
using Com.IsartDigital.GoobyrFPS.FX.Rendering;
using Com.IsartDigital.GoobyrFPS.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>One lighting state of a map: sun, sky, fog and exposure.</summary>
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
    }

    /// <summary>
    /// The contract's time of day, picked by the host in the briefing
    /// (<see cref="LobbyState.TimeOfDay"/>) and applied on every machine: the sun's angle and colour
    /// (a cold moon at night), the sky's gradient and clouds, ambient light and reflections taken
    /// from that sky, the volumetric fog's density and glow, the exposure, and the street lamps.
    /// The map builder writes one preset per time for the map's biome.
    /// </summary>
    public class TimeOfDay : MonoBehaviour
    {
        public static readonly string[] Names = { "DAWN", "NOON", "DUSK", "NIGHT" };

        [SerializeField] private TimePreset[] _presets = new TimePreset[4];
        [SerializeField] private Light _sun;
        [SerializeField] private Material _sky;

        public static TimeOfDay Instance { get; private set; }
        public int Current { get; private set; } = -1;

        private Material _skyInstance;
        private Volume _volume;
        private GoobyrAtmosphere _atmosphere;
        private ColorAdjustments _adjust;

        public void Setup(Light pSun, Material pSky, TimePreset[] pPresets)
        {
            _sun = pSun;
            _sky = pSky;
            _presets = pPresets;
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
            Apply(LobbyState.Instance != null ? LobbyState.Instance.TimeOfDay.Value : 1);
        }

        private void HandleChanged(int pOld, int pNew) => Apply(pNew);

        public void Apply(int pIndex)
        {
            if (_presets == null || _presets.Length == 0) return;
            pIndex = Mathf.Clamp(pIndex, 0, _presets.Length - 1);
            Current = pIndex;
            TimePreset p = _presets[pIndex];

            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(p.sun.x, p.sun.y, 0f);
                _sun.color = p.sunColor;
                _sun.intensity = p.sunIntensity;
                _sun.shadowStrength = pIndex == 3 ? 0.6f : 0.85f;
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
            RenderSettings.ambientIntensity = p.ambient;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(p.ambient);
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();

            EnsureVolume();
            _atmosphere.density.Override(p.fogDensity);
            _atmosphere.ambientColor.Override(p.fogAmbient);
            _atmosphere.sunScattering.Override(p.fogSun);
            _adjust.postExposure.Override(p.exposure);

            foreach (Light lLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (lLight == _sun || lLight.type == LightType.Directional) continue;
                if (!lLight.name.StartsWith("Lamp")) continue;
                if (!_lampIntensity.TryGetValue(lLight, out float lAuthored)) _lampIntensity[lLight] = lAuthored = lLight.intensity;
                lLight.enabled = p.lamps > 0.01f;
                lLight.intensity = lAuthored * p.lamps;
            }
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
            _volume.profile = lProfile;
        }

        // ---------------------------------------------------------------- biome presets

        public enum Biome { Jungle, Desert, Snow }

        /// <summary>Dawn, noon, dusk and night for a biome (warm humid haze, dry glare, cold blue light).</summary>
        public static TimePreset[] PresetsFor(Biome pBiome, float pHeading)
        {
            TimePreset Make(float pElevation, float pYaw, Color pSun, float pIntensity, Color pZenith, Color pHorizon, Color pHaze, float pCover, float pFog, Color pFogAmbient, float pFogSun, float pExposure, float pLamps, float pAmbient) => new TimePreset
            {
                sun = new Vector2(pElevation, pHeading + pYaw), sunColor = pSun, sunIntensity = pIntensity,
                zenith = pZenith, horizon = pHorizon, haze = pHaze,
                cloudLit = Color.Lerp(Color.white, pSun, 0.5f), cloudShade = Color.Lerp(pZenith, pHorizon, 0.5f) * 0.75f,
                cloudCoverage = pCover, fogDensity = pFog, fogAmbient = pFogAmbient, fogSun = pFogSun,
                exposure = pExposure, lamps = pLamps, ambient = pAmbient
            };

            switch (pBiome)
            {
                case Biome.Jungle:
                    return new[]
                    {
                        Make(8f, -80f, new Color(1f, 0.72f, 0.5f), 1.6f, new Color(0.32f, 0.45f, 0.66f), new Color(0.98f, 0.78f, 0.62f), new Color(1f, 0.7f, 0.45f), 0.45f, 0.022f, new Color(0.6f, 0.62f, 0.62f), 2.6f, 0.3f, 0.6f, 0.9f),
                        Make(62f, 20f, new Color(1f, 0.96f, 0.88f), 2.6f, new Color(0.22f, 0.45f, 0.82f), new Color(0.78f, 0.86f, 0.9f), new Color(1f, 0.93f, 0.8f), 0.55f, 0.008f, new Color(0.62f, 0.7f, 0.74f), 1.4f, 0f, 0f, 1f),
                        Make(7f, 95f, new Color(1f, 0.55f, 0.32f), 1.7f, new Color(0.28f, 0.3f, 0.55f), new Color(1f, 0.6f, 0.42f), new Color(1f, 0.5f, 0.3f), 0.5f, 0.016f, new Color(0.6f, 0.48f, 0.45f), 3f, 0.25f, 0.8f, 0.8f),
                        Make(38f, 160f, new Color(0.55f, 0.65f, 0.95f), 0.25f, new Color(0.02f, 0.03f, 0.07f), new Color(0.08f, 0.1f, 0.16f), new Color(0.3f, 0.35f, 0.5f), 0.35f, 0.012f, new Color(0.07f, 0.09f, 0.14f), 1.2f, 0.6f, 1f, 0.55f)
                    };
                case Biome.Desert:
                    return new[]
                    {
                        Make(6f, -85f, new Color(1f, 0.7f, 0.48f), 1.8f, new Color(0.35f, 0.5f, 0.75f), new Color(1f, 0.8f, 0.62f), new Color(1f, 0.72f, 0.48f), 0.15f, 0.010f, new Color(0.75f, 0.66f, 0.58f), 2.4f, 0.2f, 0.5f, 0.9f),
                        Make(68f, 15f, new Color(1f, 0.95f, 0.85f), 3.1f, new Color(0.18f, 0.42f, 0.85f), new Color(0.9f, 0.88f, 0.82f), new Color(1f, 0.95f, 0.85f), 0.08f, 0.004f, new Color(0.85f, 0.8f, 0.72f), 1f, -0.2f, 0f, 1.05f),
                        Make(6f, 92f, new Color(1f, 0.5f, 0.28f), 1.9f, new Color(0.3f, 0.32f, 0.6f), new Color(1f, 0.58f, 0.38f), new Color(1f, 0.48f, 0.25f), 0.2f, 0.009f, new Color(0.7f, 0.5f, 0.42f), 3f, 0.2f, 0.8f, 0.8f),
                        Make(42f, 170f, new Color(0.6f, 0.68f, 0.95f), 0.3f, new Color(0.02f, 0.03f, 0.08f), new Color(0.09f, 0.1f, 0.16f), new Color(0.3f, 0.34f, 0.5f), 0.05f, 0.004f, new Color(0.08f, 0.09f, 0.14f), 1f, 0.6f, 1f, 0.55f)
                    };
                default:
                    return new[]
                    {
                        Make(5f, -75f, new Color(1f, 0.75f, 0.62f), 1.5f, new Color(0.35f, 0.45f, 0.7f), new Color(1f, 0.82f, 0.75f), new Color(1f, 0.75f, 0.6f), 0.4f, 0.014f, new Color(0.75f, 0.75f, 0.82f), 2.4f, 0.1f, 0.6f, 1f),
                        Make(32f, 10f, new Color(1f, 0.97f, 0.94f), 2.2f, new Color(0.3f, 0.48f, 0.78f), new Color(0.86f, 0.9f, 0.95f), new Color(1f, 0.97f, 0.92f), 0.5f, 0.007f, new Color(0.78f, 0.82f, 0.9f), 1.2f, -0.35f, 0f, 1.1f),
                        Make(5f, 100f, new Color(1f, 0.6f, 0.5f), 1.5f, new Color(0.3f, 0.32f, 0.58f), new Color(1f, 0.66f, 0.62f), new Color(1f, 0.58f, 0.5f), 0.45f, 0.012f, new Color(0.72f, 0.6f, 0.65f), 2.6f, 0.05f, 0.8f, 0.9f),
                        Make(35f, 165f, new Color(0.6f, 0.7f, 1f), 0.35f, new Color(0.02f, 0.03f, 0.08f), new Color(0.1f, 0.12f, 0.18f), new Color(0.35f, 0.4f, 0.55f), 0.3f, 0.01f, new Color(0.1f, 0.12f, 0.18f), 1.1f, 0.7f, 1f, 0.7f)
                    };
            }
        }
    }
}
