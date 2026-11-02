using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// A way out the crowd runs for when trouble starts: a town gate, the road out, a building's
    /// back door to the street, the jetty. Placed by the map builders around every crowd and public
    /// area (<c>AiPlacement.Exit</c>); a fleeing civilian picks the nearest reachable one that does
    /// not lead toward the threat, and cowers when there is none.
    /// </summary>
    public class ExitPoint : MonoBehaviour
    {
        [SerializeField] private int _zone = -1;

        private static readonly List<ExitPoint> _all = new List<ExitPoint>();
        public static IReadOnlyList<ExitPoint> All => _all;

        /// <summary>The zone whose crowd uses it (-1: anyone's).</summary>
        public int Zone => _zone;

        public void Setup(int pZone) => _zone = pZone;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.45f);
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(1.2f, 2f, 1.2f));
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 1.5f);
        }
    }
}
