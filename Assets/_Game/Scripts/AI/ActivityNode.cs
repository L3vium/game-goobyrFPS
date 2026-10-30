using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>A place where a target spends time: a bar, an office desk, a garden bench, a toilet.</summary>
    public class ActivityNode : MonoBehaviour
    {
        [SerializeField] private ActivityType _activity;
        [SerializeField] private int _zone;
        [SerializeField] private float _duration = 12f;

        private static readonly List<ActivityNode> _all = new List<ActivityNode>();
        public static IReadOnlyList<ActivityNode> All => _all;

        public ActivityType Activity => _activity;
        public int Zone => _zone;
        public float Duration => _duration;

        public void Setup(ActivityType pActivity, int pZone, float pDuration)
        {
            _activity = pActivity;
            _zone = pZone;
            _duration = pDuration;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static ActivityNode Find(int pZone, ActivityType pType, Vector3 pNear)
        {
            ActivityNode lBest = null;
            float lBestDistance = float.MaxValue;
            foreach (ActivityNode lNode in _all)
            {
                if (lNode._zone != pZone || lNode._activity != pType) continue;
                float lDistance = (lNode.transform.position - pNear).sqrMagnitude + Random.Range(0f, 400f);
                if (lDistance < lBestDistance) { lBestDistance = lDistance; lBest = lNode; }
            }
            return lBest;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        }
    }
}
