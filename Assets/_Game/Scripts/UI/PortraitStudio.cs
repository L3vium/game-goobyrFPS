using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// A small photo studio far under the map: one character standing in its idle, a warm key, a
    /// cool fill and a rim light, a dark backdrop, a portrait lens. The contract's target picture is
    /// shot here (head and shoulders, three-quarter view), on its own layer so nothing of the map
    /// shows up and the map's lights are not needed.
    /// </summary>
    public class PortraitStudio : MonoBehaviour
    {
        private static readonly Vector3 PLACE = new Vector3(0f, -1500f, 0f);
        private const int LAYER = GameLayers.LOCAL_VIEW;

        private static PortraitStudio _instance;
        private HumanBody _body;
        private Camera _camera;

        private static PortraitStudio Ensure()
        {
            if (_instance != null) return _instance;
            GameObject lRoot = new GameObject("PortraitStudio");
            lRoot.transform.position = PLACE;
            _instance = lRoot.AddComponent<PortraitStudio>();
            _instance.Setup();
            return _instance;
        }

        private void Setup()
        {
            GameObject lSubject = new GameObject("Subject") { layer = LAYER };
            lSubject.transform.SetParent(transform, false);
            lSubject.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
            _body = lSubject.AddComponent<HumanBody>();
            _body.Culling = AnimatorCullingMode.AlwaysAnimate;
            HumanAnimator lAnimator = lSubject.AddComponent<HumanAnimator>();
            lAnimator.Velocity = Vector3.zero;
            lAnimator.UseIK = false;

            Light(new Vector3(1.6f, 2.4f, -1.2f), new Color(1f, 0.88f, 0.74f), 26f);
            Light(new Vector3(-1.8f, 1.6f, -1.4f), new Color(0.55f, 0.65f, 0.9f), 7f);
            Light(new Vector3(-0.9f, 2.2f, 1.6f), new Color(1f, 0.95f, 0.9f), 30f);

            _camera = new GameObject("PortraitCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.cullingMask = 1 << LAYER;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.07f, 0.075f, 0.085f);
            _camera.fieldOfView = 20f;
            _camera.nearClipPlane = 0.2f;
            _camera.farClipPlane = 8f;
            _camera.enabled = false;
            UniversalAdditionalCameraData lData = _camera.GetUniversalAdditionalCameraData();
            lData.renderPostProcessing = false;
            lData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        private void Light(Vector3 pLocal, Color pColor, float pIntensity)
        {
            Light lLight = new GameObject("Light").AddComponent<Light>();
            lLight.transform.SetParent(transform, false);
            lLight.transform.localPosition = pLocal;
            lLight.transform.LookAt(transform.position + Vector3.up * 1.5f);
            lLight.type = LightType.Spot;
            lLight.spotAngle = 50f;
            lLight.range = 6f;
            lLight.color = pColor;
            lLight.intensity = pIntensity;
            lLight.shadows = LightShadows.None;
            lLight.cullingMask = 1 << LAYER;
        }

        /// <summary>Renders <paramref name="pCharacter"/>'s head and shoulders into <paramref name="pTarget"/>.</summary>
        public static void Shoot(string pCharacter, RenderTexture pTarget)
        {
            PortraitStudio lStudio = Ensure();
            lStudio._body.Configure(pCharacter, Color.clear);
            Animator lAnimator = lStudio._body.Animator;
            if (lAnimator != null) lAnimator.Update(0.5f);
            Transform lHead = lStudio._body.Head;
            Vector3 lFace = lHead != null ? lHead.position + Vector3.up * 0.04f : lStudio.transform.position + Vector3.up * 1.6f;
            Transform lSubject = lStudio._body.transform;
            Vector3 lFront = lSubject.forward * 2.6f + lSubject.right * -0.7f;
            lStudio._camera.transform.position = lFace + lFront + Vector3.up * 0.05f;
            lStudio._camera.transform.LookAt(lFace + Vector3.down * 0.2f);
            lStudio._camera.targetTexture = pTarget;
            lStudio._camera.Render();
            lStudio._camera.targetTexture = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
