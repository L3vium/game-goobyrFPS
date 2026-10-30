using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>The white flash a Goob (or a prop) gets when hit, fading out in a few frames.</summary>
    public class HitFlash : MonoBehaviour
    {
        private GoobBody _body;
        private Renderer[] _renderers;
        private float _value;

        public static void Play(GoobBody pBody, float pStrength = 1f)
        {
            if (pBody == null) return;
            HitFlash lFlash = pBody.GetComponent<HitFlash>();
            if (lFlash == null) lFlash = pBody.gameObject.AddComponent<HitFlash>();
            lFlash._body = pBody;
            lFlash._value = Mathf.Max(lFlash._value, pStrength);
            lFlash.enabled = true;
        }

        public static void Play(GameObject pObject, float pStrength = 1f)
        {
            if (pObject == null) return;
            HitFlash lFlash = (pObject.GetComponent<HitFlash>() is HitFlash lFoundHitFlash && lFoundHitFlash != null ? lFoundHitFlash : pObject.AddComponent<HitFlash>());
            lFlash._renderers ??= pObject.GetComponentsInChildren<Renderer>();
            lFlash._value = Mathf.Max(lFlash._value, pStrength);
            lFlash.enabled = true;
        }

        private void Update()
        {
            _value = Mathf.MoveTowards(_value, 0f, Time.deltaTime * 7f);
            if (_body != null) _body.Flash(_value);
            else if (_renderers != null) foreach (Renderer lRenderer in _renderers) if (lRenderer != null) GameArt.SetFlash(lRenderer, _value);
            if (_value <= 0f) enabled = false;
        }
    }
}
