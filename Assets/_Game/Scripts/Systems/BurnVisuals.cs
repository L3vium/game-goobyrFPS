using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>Fire drawn on burning characters, on every machine.</summary>
    public static class BurnVisuals
    {
        private static readonly Dictionary<ulong, GameObject> _fires = new Dictionary<ulong, GameObject>();

        public static void Broadcast(Damageable pTarget, bool pOn)
        {
            if (MapEffects.Instance != null) MapEffects.Instance.BurnRpc(pTarget.NetworkObject, pOn);
        }

        public static void Apply(Unity.Netcode.NetworkObject pTarget, bool pOn)
        {
            if (pTarget == null) return;
            ulong lId = pTarget.NetworkObjectId;
            if (_fires.TryGetValue(lId, out GameObject lFire) && lFire != null) Destroy(lFire);
            _fires.Remove(lId);
            if (pOn) _fires[lId] = Effects.Fire(pTarget.transform, new Vector3(0, 0.9f, 0), 0.9f);
        }

        private static void Destroy(Object pObject) => Object.Destroy(pObject);
    }
}
