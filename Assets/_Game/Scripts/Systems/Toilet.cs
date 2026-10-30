using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>A toilet: where sick NPCs go, and where they are alone and easy to finish.</summary>
    public class Toilet : MonoBehaviour
    {
        private static readonly List<Toilet> _all = new List<Toilet>();
        public Vector3 StandPoint => transform.position + transform.forward * 0.6f;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static Toilet Nearest(Vector3 pPoint)
        {
            Toilet lBest = null;
            float lBestDistance = float.MaxValue;
            foreach (Toilet lToilet in _all)
            {
                float lDistance = (lToilet.transform.position - pPoint).sqrMagnitude;
                if (lDistance < lBestDistance) { lBestDistance = lDistance; lBest = lToilet; }
            }
            return lBest;
        }
    }
}
