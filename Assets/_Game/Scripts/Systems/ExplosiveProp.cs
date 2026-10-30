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
    /// <summary>A red barrel, a gas bottle: shoot it and it goes up.</summary>
    public class ExplosiveProp : Interactable, IShootable
    {
        [SerializeField] private float _radius = 6f;
        [SerializeField] private float _damage = 150f;
        private float _health = 30f;

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => false;

        public void OnShotServer(DamageInfo pInfo)
        {
            if (State != 0) return;
            _health -= pInfo.type == DamageType.Fire ? 12f : pInfo.amount;
            if (_health > 0f && pInfo.type != DamageType.Explosion) return;
            SetStateServer(1);
            Ballistics.ExplodeServer(transform.position + Vector3.up * 0.5f, _radius, _damage, pInfo.attacker, pInfo.attackerTeam);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            foreach (Renderer lRenderer in GetComponentsInChildren<Renderer>()) lRenderer.enabled = pNew == 0;
            foreach (Collider lCollider in GetComponentsInChildren<Collider>()) lCollider.enabled = pNew == 0;
        }
    }
}
