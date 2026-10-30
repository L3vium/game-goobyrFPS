using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Systems;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// Something thrown, simulated by the host: a coin (distraction where it lands), a brick (knocks
    /// out the NPC it hits), a smoke grenade (a cloud that blinds the AI), or C4 (sticks where it
    /// lands, waits for the remote).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrownItem : NetworkBehaviour
    {
        public readonly NetworkVariable<int> ItemIndex = new NetworkVariable<int>(-1);

        private ulong _thrower;
        private int _team;
        private bool _landed;
        private float _fuse = -1f;
        private Rigidbody _body;
        private GameObject _model;

        public ItemDefinition Item => GameDatabase.Item(ItemIndex.Value);

        public static NetworkObject Spawn(ItemDefinition pItem, Vector3 pOrigin, Vector3 pDirection, ulong pThrower, int pTeam)
        {
            NetworkObject lObject = Instantiate(NetPrefabs.Instance.thrown, pOrigin, Quaternion.LookRotation(pDirection));
            ThrownItem lThrown = lObject.GetComponent<ThrownItem>();
            lThrown._thrower = pThrower;
            lThrown._team = pTeam;
            lObject.Spawn(true);
            lThrown.ItemIndex.Value = pItem.Index;
            Rigidbody lBody = lObject.GetComponent<Rigidbody>();
            lBody.linearVelocity = pDirection.normalized * pItem.throwForce + Vector3.up * 2.5f;
            lBody.angularVelocity = Random.insideUnitSphere * 10f;
            if (pItem.kind == ItemKind.SmokeGrenade) lThrown._fuse = 1.4f;
            return lObject;
        }

        public override void OnNetworkSpawn()
        {
            _body = GetComponent<Rigidbody>();
            if (!IsServer) _body.isKinematic = true;
            ItemIndex.OnValueChanged += (_, __) => BuildModel();
            BuildModel();
        }

        private void BuildModel()
        {
            if (_model != null) Destroy(_model);
            if (Item == null) return;
            _model = WeaponModels.Build(Item, transform, GameLayers.PICKUP);
            _model.transform.localScale = Vector3.one * 1.6f;
        }

        private void Update()
        {
            if (!IsServer || Item == null) return;
            if (_fuse > 0f)
            {
                _fuse -= Time.deltaTime;
                if (_fuse <= 0f) PopSmoke();
            }
        }

        private void OnCollisionEnter(Collision pCollision)
        {
            if (!IsServer || Item == null || _landed) return;
            ItemKind lKind = Item.kind;

            if (lKind == ItemKind.C4)
            {
                // Sticks to the first surface, and to whoever it hits.
                _landed = true;
                _body.isKinematic = true;
                ContactPoint lContact = pCollision.GetContact(0);
                transform.position = lContact.point + lContact.normal * 0.03f;
                transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, lContact.normal), lContact.normal);
                NetworkObject lCarrier = pCollision.collider.GetComponentInParent<NetworkObject>();
                if (lCarrier != null && lCarrier != NetworkObject) NetworkObject.TrySetParent(lCarrier, true);
                FeedbackRpc(0);
                return;
            }

            if (lKind == ItemKind.Brick)
            {
                Npc lNpc = pCollision.collider.GetComponentInParent<Npc>();
                Damageable lTarget = pCollision.collider.GetComponentInParent<Damageable>();
                if (lNpc != null && lNpc.Health.IsAlive) lNpc.KnockOut(_body.linearVelocity.normalized, _thrower);
                else if (lTarget != null) lTarget.ApplyDamage(new DamageInfo { amount = 30f, type = DamageType.Melee, point = transform.position, direction = _body.linearVelocity.normalized, attacker = _thrower, attackerTeam = _team });
                IShootable lShootable = pCollision.collider.GetComponentInParent<IShootable>();
                lShootable?.OnShotServer(new DamageInfo { amount = 20f, type = DamageType.Melee, point = transform.position, attacker = _thrower, attackerTeam = _team });
            }

            if (lKind == ItemKind.Coin || lKind == ItemKind.Brick)
            {
                _landed = true;
                NoiseSystem.Emit(transform.position, lKind == ItemKind.Coin ? 14f : 10f, NoiseKind.Distraction, _thrower);
                FeedbackRpc(lKind == ItemKind.Coin ? 1 : 2);
                Invoke(nameof(Remove), 8f);
            }
        }

        private void PopSmoke()
        {
            SmokeVolume.AddServer(transform.position, 6.5f, 22f);
            if (MapEffects.Instance != null) MapEffects.Instance.SmokeRpc(transform.position, 6.5f, 22f);
            NoiseSystem.Emit(transform.position, 18f, NoiseKind.Suspicious, _thrower);
            Remove();
        }

        /// <summary>Host: the remote was pressed.</summary>
        public void DetonateServer()
        {
            if (!IsSpawned) return;
            Ballistics.ExplodeServer(transform.position, Mathf.Max(5f, Item.explosionRadius), Item.damage, _thrower, _team);
            Remove();
        }

        private void Remove()
        {
            if (IsSpawned) NetworkObject.Despawn();
        }

        [Rpc(SendTo.Everyone)]
        private void FeedbackRpc(int pKind)
        {
            switch (pKind)
            {
                case 0: Audio.Sfx.Play("c4_stick", transform.position, 0.8f, 15f); break;
                case 1: Audio.Sfx.Play("coin", transform.position, 1f, 25f); break;
                case 2: Audio.Sfx.Play("brick", transform.position, 1f, 25f); Effects.Dust(transform.position, 0.5f); break;
            }
            Juice.Squash(transform, 0.3f);
        }
    }
}
