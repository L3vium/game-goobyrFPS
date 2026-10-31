using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// A body of water you can swim in: the water plane it sits on (a unit square scaled to the
    /// surface), a depth below it, and a current (rivers push swimmers downstream). Queried, not
    /// triggered: the motor asks <see cref="At"/> where it stands, bullets ask <see cref="Raycast"/>
    /// where they meet the surface.
    /// </summary>
    public class WaterVolume : MonoBehaviour
    {
        public static readonly List<WaterVolume> All = new List<WaterVolume>();

        [SerializeField] private Vector3 _current;
        [SerializeField] private float _depth = 30f;

        /// <summary>Drift of the water in m/s (zero for lakes and the sea).</summary>
        public Vector3 Current => _current;

        public void Setup(Vector3 pCurrent, float pDepth)
        {
            _current = pCurrent;
            _depth = pDepth;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Is the point under this surface (and above its depth)? The surface height there.</summary>
        public bool Contains(Vector3 pPoint, out float pSurface)
        {
            Vector3 lLocal = transform.InverseTransformPoint(pPoint);
            pSurface = transform.TransformPoint(new Vector3(lLocal.x, 0f, lLocal.z)).y;
            return Mathf.Abs(lLocal.x) <= 0.5f && Mathf.Abs(lLocal.z) <= 0.5f && pPoint.y <= pSurface + 0.02f && pPoint.y >= pSurface - _depth;
        }

        /// <summary>The water the point is in, if any, and its surface height there.</summary>
        public static WaterVolume At(Vector3 pPoint, out float pSurface)
        {
            foreach (WaterVolume lWater in All)
                if (lWater != null && lWater.Contains(pPoint, out pSurface)) return lWater;
            pSurface = float.MinValue;
            return null;
        }

        /// <summary>Where a ray first meets a water surface within <paramref name="pMax"/> (bullets, thrown things).</summary>
        public static bool Raycast(Vector3 pOrigin, Vector3 pDirection, float pMax, out Vector3 pHit)
        {
            pHit = Vector3.zero;
            float lBest = pMax;
            bool lFound = false;
            foreach (WaterVolume lWater in All)
            {
                if (lWater == null) continue;
                Plane lPlane = new Plane(lWater.transform.up, lWater.transform.position);
                if (!lPlane.Raycast(new Ray(pOrigin, pDirection), out float lDistance) || lDistance > lBest || lDistance <= 0f) continue;
                Vector3 lPoint = pOrigin + pDirection * lDistance;
                Vector3 lLocal = lWater.transform.InverseTransformPoint(lPoint);
                if (Mathf.Abs(lLocal.x) > 0.5f || Mathf.Abs(lLocal.z) > 0.5f) continue;
                lBest = lDistance;
                pHit = lPoint;
                lFound = true;
            }
            return lFound;
        }
    }
}
