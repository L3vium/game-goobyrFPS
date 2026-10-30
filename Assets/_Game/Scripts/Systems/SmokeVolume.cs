using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// Host-side list of smoke clouds (grenades, fires). NPC sight lines that cross one are
    /// blocked, so smoke lets a player cross open ground or break contact.
    /// </summary>
    public static class SmokeVolume
    {
        private struct Cloud
        {
            public Vector3 center;
            public float radius;
            public float until;
        }

        private static readonly List<Cloud> _clouds = new List<Cloud>();

        public static void AddServer(Vector3 pCenter, float pRadius, float pDuration)
        {
            _clouds.Add(new Cloud { center = pCenter + Vector3.up, radius = pRadius, until = Time.time + pDuration });
        }

        /// <summary>True if the segment crosses a living cloud.</summary>
        public static bool Blocks(Vector3 pFrom, Vector3 pTo)
        {
            for (int i = _clouds.Count - 1; i >= 0; i--)
            {
                Cloud lCloud = _clouds[i];
                if (Time.time > lCloud.until) { _clouds.RemoveAt(i); continue; }
                float lGrow = Mathf.Clamp01((lCloud.until - Time.time) / 2f);
                if (DistanceToSegment(lCloud.center, pFrom, pTo) < lCloud.radius * lGrow) return true;
            }
            return false;
        }

        public static bool Inside(Vector3 pPoint)
        {
            foreach (Cloud lCloud in _clouds)
                if (Time.time < lCloud.until && Vector3.Distance(lCloud.center, pPoint) < lCloud.radius) return true;
            return false;
        }

        private static float DistanceToSegment(Vector3 pPoint, Vector3 pA, Vector3 pB)
        {
            Vector3 lAb = pB - pA;
            float t = Mathf.Clamp01(Vector3.Dot(pPoint - pA, lAb) / Mathf.Max(0.0001f, lAb.sqrMagnitude));
            return Vector3.Distance(pPoint, pA + lAb * t);
        }

        public static void Clear() => _clouds.Clear();
    }
}
