using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Game feel on world objects through Feel (MoreMountains MMF_Player), built at runtime and
    /// cached per object: the white flash of a character or a prop that is hit (MMF_Flicker on the
    /// URP Lit <c>_BaseColor</c>, through property blocks so materials stay shared), a squash and
    /// stretch on impact, a pop on pickups. Camera-side juice (shake, aberration, FOV, hit-stop)
    /// stays in <see cref="Juice"/> and <see cref="CameraRig"/>.
    /// </summary>
    public static class FeelFx
    {
        private static readonly Dictionary<int, MMF_Player> _flash = new Dictionary<int, MMF_Player>();
        private static readonly Dictionary<int, MMF_Player> _squash = new Dictionary<int, MMF_Player>();

        /// <summary>A short white (or coloured) flash over every renderer under <paramref name="pRoot"/>.</summary>
        public static void Flash(GameObject pRoot, Color pColor, float pDuration = 0.1f)
        {
            if (pRoot == null) return;
            if (!_flash.TryGetValue(pRoot.GetInstanceID(), out MMF_Player lPlayer) || lPlayer == null)
            {
                List<Renderer> lRenderers = new List<Renderer>();
                foreach (Renderer lRenderer in pRoot.GetComponentsInChildren<Renderer>())
                    if (lRenderer is MeshRenderer || lRenderer is SkinnedMeshRenderer) lRenderers.Add(lRenderer);
                if (lRenderers.Count == 0) return;
                lPlayer = NewPlayer(pRoot, "FeelFlash");
                MMF_Flicker lFlicker = new MMF_Flicker
                {
                    BoundRenderer = lRenderers[0],
                    ExtraBoundRenderers = lRenderers.GetRange(1, lRenderers.Count - 1),
                    Mode = MMF_Flicker.Modes.PropertyName,
                    PropertyName = "_BaseColor",
                    UseMaterialPropertyBlocks = true,
                    FlickerPeriod = 0.05f,
                    Label = "Hit flash"
                };
                lPlayer.AddFeedback(lFlicker);
                lPlayer.Initialization();
                _flash[pRoot.GetInstanceID()] = lPlayer;
            }
            MMF_Flicker lFeedback = lPlayer.GetFeedbackOfType<MMF_Flicker>();
            if (lFeedback != null)
            {
                lFeedback.FlickerColor = pColor;
                lFeedback.FlickerDuration = pDuration;
            }
            lPlayer.PlayFeedbacks();
        }

        /// <summary>Squash and stretch on a transform (a body taking a hit, a prop knocked): subtle for realistic objects.</summary>
        public static void Squash(Transform pTarget, float pAmount = 0.08f, float pDuration = 0.22f)
        {
            if (pTarget == null) return;
            if (!_squash.TryGetValue(pTarget.GetInstanceID(), out MMF_Player lPlayer) || lPlayer == null)
            {
                lPlayer = NewPlayer(pTarget.gameObject, "FeelSquash");
                lPlayer.AddFeedback(new MMF_SquashAndStretch { SquashAndStretchTarget = pTarget, Label = "Squash" });
                lPlayer.Initialization();
                _squash[pTarget.GetInstanceID()] = lPlayer;
            }
            MMF_SquashAndStretch lFeedback = lPlayer.GetFeedbackOfType<MMF_SquashAndStretch>();
            if (lFeedback != null)
            {
                lFeedback.RemapCurveZero = 1f;
                lFeedback.RemapCurveOne = 1f + pAmount;
                lFeedback.AnimateScaleDuration = pDuration;
            }
            lPlayer.PlayFeedbacks();
        }

        private static MMF_Player NewPlayer(GameObject pHost, string pName)
        {
            GameObject lObject = new GameObject(pName);
            lObject.transform.SetParent(pHost.transform, false);
            MMF_Player lPlayer = lObject.AddComponent<MMF_Player>();
            lPlayer.InitializationMode = MMFeedbacks.InitializationModes.Script;
            lPlayer.CanPlayWhileAlreadyPlaying = true;
            return lPlayer;
        }
    }
}
