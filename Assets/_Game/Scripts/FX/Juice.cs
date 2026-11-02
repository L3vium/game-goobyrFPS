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

        /// <summary>A silent takedown: a dip of the view, a cold flash and a beat of slow motion (solo only).</summary>
        public static void Takedown()
        {
            Shake(0.22f);
            Aberration(0.45f);
            FovKick(-7f);
            Flash(new Color(0.85f, 0.9f, 1f), 0.18f);
            HitStop(0.16f, 0.2f);
        }

        /// <summary>A kill landed: a short freeze frame (solo only), a punch of aberration.</summary>
        public static void Kill(bool pHeadshot)
        {
            Aberration(pHeadshot ? 0.5f : 0.3f);
            Shake(pHeadshot ? 0.18f : 0.1f);
            HitStop(pHeadshot ? 0.08f : 0.05f, 0.05f);
        }

        /// <summary>
        /// Freeze frame. It slows the whole game clock, so it only runs when nobody else shares it
        /// (a solo match): in multiplayer the host's clock is everyone's simulation.
        /// </summary>
        public static void HitStop(float pSeconds, float pScale)
        {
            Unity.Netcode.NetworkManager lNet = Unity.Netcode.NetworkManager.Singleton;
            if (lNet == null || !lNet.IsServer || lNet.ConnectedClientsIds.Count > 1) return;
            if (CameraRig.Instance != null) CameraRig.Instance.HitStop(pSeconds, pScale);
        }

        /// <summary>
        /// Squash and stretch on any transform (pickups, jump pads, props being hit, devices switched on),
        /// played by Feel (<see cref="FeelFx.Squash"/>). Amounts are the old cartoon scale: a realistic
        /// object flexes about a third of that.
        /// </summary>
        public static void Squash(Transform pTarget, float pAmount = 0.3f, float pDuration = 0.35f)
        {
            if (pTarget == null) return;
            FeelFx.Squash(pTarget, pAmount * 0.35f, pDuration);
        }
    }
}
