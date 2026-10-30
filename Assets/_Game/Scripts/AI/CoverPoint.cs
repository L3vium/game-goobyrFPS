using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// A spot an NPC can fight from: low (crouch behind) or high (stand behind, peek around).
    /// Placed by the map builders along walls, crates, cars, counters.
    /// </summary>
    public class CoverPoint : MonoBehaviour
    {
        [SerializeField] private bool _low = true;

        private static readonly List<CoverPoint> _all = new List<CoverPoint>();
        public static IReadOnlyList<CoverPoint> All => _all;

        public bool Low => _low;
        /// <summary>Host side: the NPC holding it.</summary>
        public Npc Occupant { get; set; }

        public void Setup(bool pLow) => _low = pLow;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        /// <summary>Is a threat at <paramref name="pThreat"/> blocked from someone crouched/standing here?</summary>
        public bool CoversFrom(Vector3 pThreat)
        {
            Vector3 lEye = transform.position + Vector3.up * (_low ? 0.9f : 1.5f);
            return Physics.Linecast(lEye, pThreat + Vector3.up * 1.2f, Core.GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Can someone here see the threat by standing up or leaning out?</summary>
        public bool CanPeek(Vector3 pThreat)
        {
            Vector3 lUp = transform.position + Vector3.up * 1.65f;
            Vector3 lSide = transform.position + Vector3.up * 1.4f + transform.right * 0.7f;
            Vector3 lOther = transform.position + Vector3.up * 1.4f - transform.right * 0.7f;
            Vector3 lTo = pThreat + Vector3.up * 1.2f;
            int lMask = Core.GameLayers.SightBlockMask;
            return !Physics.Linecast(lUp, lTo, lMask, QueryTriggerInteraction.Ignore)
                || !Physics.Linecast(lSide, lTo, lMask, QueryTriggerInteraction.Ignore)
                || !Physics.Linecast(lOther, lTo, lMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _low ? new Color(0.2f, 0.8f, 1f) : new Color(0.2f, 0.4f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * (_low ? 0.45f : 0.9f), new Vector3(0.5f, _low ? 0.9f : 1.8f, 0.5f));
        }
    }
}
