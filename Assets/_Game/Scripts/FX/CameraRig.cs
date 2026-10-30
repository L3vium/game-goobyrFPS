using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// The local camera: a base camera for the world, an overlay camera for the first-person
    /// viewmodel (so guns never clip into walls), and the global post-processing volume the juice
    /// pulses (bloom, chromatic aberration, vignette, saturation).
    /// <para>
    /// Whoever drives it (the local avatar, the spectator, the title flyover) calls
    /// <see cref="SetPose"/> every LateUpdate; the rig adds shake, FOV kicks and pulses on top.
    /// </para>
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        public Camera WorldCamera { get; private set; }
        public Camera ViewCamera { get; private set; }
        public Transform ViewmodelRoot { get; private set; }
        public float BaseFov { get; set; } = 78f;

        private Volume _volume;
        private Bloom _bloom;
        private ChromaticAberration _aberration;
        private Vignette _vignette;
        private ColorAdjustments _color;

        private float _trauma;
        private float _fovKick, _fovTarget;
        private float _aberrationPulse;
        private float _bloomPulse;
        private float _vignettePulse;
        private float _vignetteBase = 0.22f;
        private Color _vignetteColor = Color.black;
        private float _desaturate;
        private float _exposurePulse;
        private float _seed;

        public static CameraRig Ensure()
        {
            if (Instance != null) return Instance;
            GameObject lRig = new GameObject("CameraRig");
            return lRig.AddComponent<CameraRig>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _seed = Random.value * 100f;

            foreach (Camera lExisting in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (lExisting.GetComponentInParent<CameraRig>() == null) lExisting.gameObject.SetActive(false);

            GameObject lWorld = new GameObject("WorldCamera");
            lWorld.transform.SetParent(transform, false);
            lWorld.tag = "MainCamera";
            WorldCamera = lWorld.AddComponent<Camera>();
            WorldCamera.nearClipPlane = 0.05f;
            WorldCamera.farClipPlane = 900f;
            WorldCamera.fieldOfView = BaseFov;
            WorldCamera.cullingMask = ~(1 << GameLayers.LOCAL_VIEW);
            lWorld.AddComponent<AudioListener>();
            UniversalAdditionalCameraData lWorldData = WorldCamera.GetUniversalAdditionalCameraData();
            lWorldData.renderPostProcessing = true;
            lWorldData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            GameObject lView = new GameObject("ViewCamera");
            lView.transform.SetParent(lWorld.transform, false);
            ViewCamera = lView.AddComponent<Camera>();
            ViewCamera.nearClipPlane = 0.01f;
            ViewCamera.farClipPlane = 10f;
            ViewCamera.fieldOfView = 60f;
            ViewCamera.cullingMask = 1 << GameLayers.LOCAL_VIEW;
            ViewCamera.clearFlags = CameraClearFlags.Depth;
            UniversalAdditionalCameraData lViewData = ViewCamera.GetUniversalAdditionalCameraData();
            lViewData.renderType = CameraRenderType.Overlay;
            lWorldData.cameraStack.Add(ViewCamera);

            ViewmodelRoot = new GameObject("ViewmodelRoot").transform;
            ViewmodelRoot.SetParent(lWorld.transform, false);

            BuildVolume();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildVolume()
        {
            GameObject lVolumeObject = new GameObject("JuiceVolume");
            lVolumeObject.transform.SetParent(transform, false);
            _volume = lVolumeObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50;
            VolumeProfile lProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.sharedProfile = lProfile;

            _bloom = lProfile.Add<Bloom>(true);
            _bloom.threshold.Override(0.95f);
            _bloom.intensity.Override(0.9f);
            _bloom.scatter.Override(0.7f);
            _aberration = lProfile.Add<ChromaticAberration>(true);
            _aberration.intensity.Override(0.04f);
            _vignette = lProfile.Add<Vignette>(true);
            _vignette.intensity.Override(_vignetteBase);
            _vignette.smoothness.Override(0.45f);
            _color = lProfile.Add<ColorAdjustments>(true);
            _color.saturation.Override(12f);
            _color.contrast.Override(8f);
            _color.postExposure.Override(0f);
            Tonemapping lTone = lProfile.Add<Tonemapping>(true);
            lTone.mode.Override(TonemappingMode.Neutral);
        }

        /// <summary>The pose of the eye this frame, before juice.</summary>
        public void SetPose(Vector3 pPosition, Quaternion pRotation)
        {
            float lShake = _trauma * _trauma;
            float t = Time.time * 22f;
            Vector3 lOffset = new Vector3(Mathf.PerlinNoise(_seed, t) - 0.5f, Mathf.PerlinNoise(_seed + 3f, t) - 0.5f, 0f) * lShake * 0.35f;
            Vector3 lAngles = new Vector3(Mathf.PerlinNoise(_seed + 7f, t) - 0.5f, Mathf.PerlinNoise(_seed + 11f, t) - 0.5f, Mathf.PerlinNoise(_seed + 13f, t) - 0.5f) * lShake * 9f;
            WorldCamera.transform.SetPositionAndRotation(pPosition + pRotation * lOffset, pRotation * Quaternion.Euler(lAngles));
        }

        public void SetTargetFov(float pFov) => _fovTarget = pFov;

        public void SetVignette(float pBase, Color pColor)
        {
            _vignetteBase = pBase;
            _vignetteColor = pColor;
        }

        public void SetDesaturate(float pAmount) => _desaturate = pAmount;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _trauma = Mathf.MoveTowards(_trauma, 0f, dt * 1.6f);
            _fovKick = Mathf.Lerp(_fovKick, 0f, 1f - Mathf.Exp(-9f * dt));
            float lFov = _fovTarget > 1f ? _fovTarget : BaseFov;
            WorldCamera.fieldOfView = Mathf.Lerp(WorldCamera.fieldOfView, lFov + _fovKick, 1f - Mathf.Exp(-14f * dt));

            _aberrationPulse = Mathf.MoveTowards(_aberrationPulse, 0f, dt * 2.2f);
            _bloomPulse = Mathf.MoveTowards(_bloomPulse, 0f, dt * 3f);
            _vignettePulse = Mathf.MoveTowards(_vignettePulse, 0f, dt * 1.5f);
            _exposurePulse = Mathf.MoveTowards(_exposurePulse, 0f, dt * 4f);

            _aberration.intensity.value = Mathf.Clamp01(0.04f + _aberrationPulse);
            _bloom.intensity.value = 0.9f + _bloomPulse;
            _vignette.intensity.value = Mathf.Clamp01(_vignetteBase + _vignettePulse);
            _vignette.color.value = Color.Lerp(_vignetteColor, new Color(0.7f, 0f, 0.05f), Mathf.Clamp01(_vignettePulse * 2f));
            _color.saturation.value = Mathf.Lerp(12f, -80f, _desaturate);
            _color.postExposure.value = _exposurePulse;
        }

        // ---------------------------------------------------------------- juice entry points

        public void AddTrauma(float pAmount) => _trauma = Mathf.Clamp01(_trauma + pAmount);
        public void KickFov(float pAmount) => _fovKick += pAmount;
        public void PulseAberration(float pAmount) => _aberrationPulse = Mathf.Max(_aberrationPulse, pAmount);
        public void PulseBloom(float pAmount) => _bloomPulse = Mathf.Max(_bloomPulse, pAmount);
        public void PulseHurt(float pAmount) => _vignettePulse = Mathf.Max(_vignettePulse, pAmount);
        public void PulseExposure(float pAmount) => _exposurePulse = Mathf.Max(_exposurePulse, pAmount);
    }
}
