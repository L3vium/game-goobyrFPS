using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// The flash a character (or a prop) gets when hit: Feel's flicker of the base colour, then a short
    /// white glow (emission, so the textures stay) fading out in a few frames.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        /// <summary>An over-bright tint: on textured materials a plain white would not show.</summary>
        private static readonly Color FLICKER = new Color(1.9f, 1.55f, 1.5f);

        private HumanBody _body;
        private Renderer[] _renderers;
        private float _value;

        public static void Play(HumanBody pBody, float pStrength = 1f)
        {
            if (pBody == null || !pBody.Built || pBody.Model == null) return;
            FeelFx.Flash(pBody.Model.gameObject, FLICKER, 0.05f + 0.05f * pStrength);
            HitFlash lFlash = pBody.GetComponent<HitFlash>();
            if (lFlash == null) lFlash = pBody.gameObject.AddComponent<HitFlash>();
            lFlash._body = pBody;
            lFlash._value = Mathf.Max(lFlash._value, pStrength);
            lFlash.enabled = true;
        }

        public static void Play(GameObject pObject, float pStrength = 1f)
        {
            if (pObject == null) return;
            FeelFx.Flash(pObject, FLICKER, 0.05f + 0.05f * pStrength);
            HitFlash lFlash = pObject.GetComponent<HitFlash>();
            if (lFlash == null) lFlash = pObject.AddComponent<HitFlash>();
            lFlash._renderers ??= pObject.GetComponentsInChildren<Renderer>();
            lFlash._value = Mathf.Max(lFlash._value, pStrength);
            lFlash.enabled = true;
        }

        private void Update()
        {
            _value = Mathf.MoveTowards(_value, 0f, Time.deltaTime * 7f);
            if (_body != null) _body.Flash(_value);
            else if (_renderers != null)
                foreach (Renderer lRenderer in _renderers)
                {
                    if (lRenderer == null) continue;
                    GameArt.SetFlash(lRenderer, _value);
                    Material lMaterial = lRenderer.sharedMaterial;
                    if (lMaterial != null && lMaterial.IsKeywordEnabled("_EMISSION") && lRenderer is SkinnedMeshRenderer) GameArt.SetEmission(lRenderer, Color.white * (_value * 0.5f));
                }
            if (_value <= 0f) enabled = false;
        }
    }
}
