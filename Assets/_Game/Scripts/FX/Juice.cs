using System;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// One-line access to the game feel: shake, flashes, pulses, squash. Everything is local
    /// (each machine juices what it sees), so gameplay code just calls it where things happen.
    /// </summary>
    public static class Juice
    {
        /// <summary>Raised for the HUD's full-screen flash: colour, strength.</summary>
        public static event Action<Color, float> OnScreenFlash;

        public static void Shake(float pTrauma)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.AddTrauma(pTrauma);
        }

        /// <summary>Shake scaled by the distance to the camera (explosions).</summary>
        public static void ShakeAt(Vector3 pPosition, float pTrauma, float pRadius)
        {
            if (CameraRig.Instance == null) return;
            float lDistance = Vector3.Distance(CameraRig.Instance.WorldCamera.transform.position, pPosition);
            float lFactor = 1f - Mathf.Clamp01(lDistance / pRadius);
            if (lFactor <= 0f) return;
            CameraRig.Instance.AddTrauma(pTrauma * lFactor);
            CameraRig.Instance.PulseAberration(0.6f * lFactor);
            CameraRig.Instance.PulseBloom(2.5f * lFactor);
            CameraRig.Instance.PulseExposure(0.6f * lFactor);
            if (lFactor > 0.5f) OnScreenFlash?.Invoke(new Color(1f, 0.9f, 0.7f), 0.5f * lFactor);
        }

        public static void Flash(Color pColor, float pStrength) => OnScreenFlash?.Invoke(pColor, pStrength);

        public static void Aberration(float pAmount)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.PulseAberration(pAmount);
        }

        public static void FovKick(float pAmount)
        {
            if (CameraRig.Instance != null) CameraRig.Instance.KickFov(pAmount);
        }

        public static void Hurt(float pAmount)
        {
            if (CameraRig.Instance == null) return;
            CameraRig.Instance.PulseHurt(pAmount);
            CameraRig.Instance.PulseAberration(pAmount * 0.8f);
            CameraRig.Instance.AddTrauma(pAmount * 0.5f);
        }

        /// <summary>Squash and stretch on any transform (pickups, jump pads, props being hit).</summary>
        public static void Squash(Transform pTarget, float pAmount = 0.3f, float pDuration = 0.35f)
        {
            if (pTarget == null) return;
            SquashStretch lSquash = pTarget.GetComponent<SquashStretch>();
            if (lSquash == null) lSquash = pTarget.gameObject.AddComponent<SquashStretch>();
            lSquash.Play(pAmount, pDuration);
        }
    }

    /// <summary>Spring-based squash and stretch around the object's rest scale.</summary>
    public class SquashStretch : MonoBehaviour
    {
        private Vector3 _rest;
        private float _value, _velocity;
        private bool _initialised;

        public void Play(float pAmount, float pDuration)
        {
            if (!_initialised) { _rest = transform.localScale; _initialised = true; }
            _velocity -= pAmount * 14f / Mathf.Max(0.1f, pDuration);
            enabled = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _velocity += (-_value * 260f - _velocity * 11f) * dt;
            _value += _velocity * dt;
            float lY = 1f + _value;
            float lXZ = 1f / Mathf.Sqrt(Mathf.Max(0.2f, lY));
            transform.localScale = new Vector3(_rest.x * lXZ, _rest.y * lY, _rest.z * lXZ);
            if (Mathf.Abs(_value) < 0.001f && Mathf.Abs(_velocity) < 0.01f)
            {
                transform.localScale = _rest;
                _value = _velocity = 0f;
                enabled = false;
            }
        }
    }
}
