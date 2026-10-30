using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>A loop of waypoints a patrol walks, pausing at each.</summary>
    public class PatrolRoute : MonoBehaviour
    {
        [SerializeField] private float _pause = 2.5f;
        public float Pause => _pause;

        public int Count => transform.childCount;
        public Vector3 Point(int pIndex) => transform.GetChild(((pIndex % Count) + Count) % Count).position;

        public void Setup(float pPause) => _pause = pPause;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            for (int i = 0; i < Count; i++) Gizmos.DrawLine(Point(i) + Vector3.up * 0.2f, Point(i + 1) + Vector3.up * 0.2f);
        }
    }
}
