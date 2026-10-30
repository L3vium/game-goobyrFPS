using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// A closet, a dumpster, a haystack, a crate: step in to vanish. NPCs only find you inside if
    /// they saw you get in.
    /// </summary>
    public class HidingSpot : Interactable
    {
        [SerializeField] private Transform _inside;
        [SerializeField] private Transform _exit;

        public Vector3 InsidePosition => _inside != null ? _inside.position : transform.position;
        public Vector3 ExitPosition => _exit != null ? _exit.position : transform.position + transform.forward * 1.2f;

        public void Setup(Transform pInside, Transform pExit, string pPrompt)
        {
            _inside = pInside;
            _exit = pExit;
            _prompt = pPrompt;
        }

        public override bool InteractLocal(PlayerAvatar pWho)
        {
            if (pWho.Motor.Mode == MotorMode.Hidden) { pWho.Motor.ExitHidingSpot(); return true; }
            if (pWho.Motor.EnterHidingSpot(this)) Juice.Squash(transform, 0.2f);
            return true;
        }
    }
}
