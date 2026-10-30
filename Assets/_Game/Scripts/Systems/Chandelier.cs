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
    /// <summary>
    /// A chandelier hanging by a chain. Shoot the chain (or pull the release lever) and it falls:
    /// whoever stands under it is crushed.
    /// </summary>
    public class Chandelier : Interactable, IShootable
    {
        [SerializeField] private Transform _crystal;
        [SerializeField] private float _dropHeight = 6f;
        [SerializeField] private float _killRadius = 2.2f;

        private float _fall;
        private Vector3 _restLocal;
        private bool _falling;

        public void Setup(Transform pCrystal, float pDropHeight)
        {
            _crystal = pCrystal;
            _dropHeight = pDropHeight;
            _prompt = "RELEASE THE CHANDELIER";
            _range = 2.5f;
        }

        private void Awake()
        {
            if (_crystal != null) _restLocal = _crystal.localPosition;
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => DropServer(pWho != null ? pWho.OwnerClientId : DamageInfo.NO_PLAYER, pWho != null ? pWho.Team.Value : Match.MatchRulesLogic.NO_TEAM);

        public void OnShotServer(DamageInfo pInfo) => DropServer(pInfo.attacker, pInfo.attackerTeam);

        private void DropServer(ulong pBy, int pTeam)
        {
            if (State != 0) return;
            SetStateServer(1);
            Vector3 lFloor = _crystal.position + Vector3.down * _dropHeight;
            foreach (Collider lCollider in Physics.OverlapSphere(lFloor + Vector3.up, _killRadius, GameLayers.CharacterMask, QueryTriggerInteraction.Ignore))
            {
                Damageable lVictim = lCollider.GetComponentInParent<Damageable>();
                if (lVictim != null) lVictim.Kill(new DamageInfo { type = DamageType.Crush, point = lFloor, direction = Vector3.down, attacker = pBy, attackerTeam = pTeam });
            }
            NoiseSystem.Emit(lFloor, 40f, NoiseKind.Alarming, pBy);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew == 1) { _falling = true; _fall = 0f; Audio.Sfx.Play("chain_snap", _crystal.position, 1f, 40f); }
            else if (_crystal != null) _crystal.localPosition = _restLocal;
        }

        private void Update()
        {
            if (!_falling || _crystal == null) return;
            _fall += Time.deltaTime;
            float lDrop = Mathf.Min(_dropHeight, 0.5f * 22f * _fall * _fall);
            _crystal.localPosition = _restLocal + Vector3.down * lDrop / Mathf.Max(0.01f, transform.lossyScale.y);
            if (lDrop >= _dropHeight)
            {
                _falling = false;
                Effects.Sparks(_crystal.position, 30);
                Effects.Dust(_crystal.position, 2f);
                Juice.ShakeAt(_crystal.position, 0.6f, 25f);
                Juice.Squash(_crystal, 0.4f);
                Audio.Sfx.Play("crash", _crystal.position, 1f, 60f);
            }
        }
    }
}
