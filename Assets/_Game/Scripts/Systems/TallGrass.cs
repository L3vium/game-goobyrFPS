using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>Tall grass: crouched inside it, a player is almost invisible.</summary>
    public class TallGrass : MonoBehaviour
    {
        private void OnTriggerEnter(Collider pOther)
        {
            PlayerAvatar lAvatar = pOther.GetComponentInParent<PlayerAvatar>();
            if (lAvatar != null && lAvatar.IsOwner && pOther == lAvatar.Controller) lAvatar.Motor.EnterGrass();
        }

        private void OnTriggerExit(Collider pOther)
        {
            PlayerAvatar lAvatar = pOther.GetComponentInParent<PlayerAvatar>();
            if (lAvatar != null && lAvatar.IsOwner && pOther == lAvatar.Controller) lAvatar.Motor.ExitGrass();
        }

        /// <summary>Host side: is this point inside grass (NPC perception)?</summary>
        public static bool Covers(Vector3 pPoint)
        {
            Collider[] lHits = Physics.OverlapSphere(pPoint, 0.2f, 1 << GameLayers.GRASS, QueryTriggerInteraction.Collide);
            return lHits.Length > 0;
        }
    }
}
