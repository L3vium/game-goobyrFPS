using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// An item lying on the map: a weapon, ammo or a gadget, at rest on the ground with no aura; it
    /// only swells a little while the player looks at it in reach. Spawned by the host from the
    /// map's item spawn points and from the dead.
    /// </summary>
    public class ItemPickup : NetworkBehaviour
    {
        public readonly NetworkVariable<int> ItemIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Magazine = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Reserve = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> Resting = new NetworkVariable<bool>(true);

        public ItemDefinition Item => GameDatabase.Item(ItemIndex.Value);

        /// <summary>Every spawned pickup (instinct vision highlights the nearby ones).</summary>
        public static readonly List<ItemPickup> All = new List<ItemPickup>();

        private Transform _visual;
        /// <summary>The pickup the local player looks at, in reach.</summary>
        public static ItemPickup Focused;
        private Rigidbody _body;

        public static ItemPickup SpawnServer(ItemDefinition pItem, Vector3 pPosition, int pMagazine, int pReserve, Vector3 pVelocity)
        {
            if (pItem == null) return null;
            NetworkObject lObject = Instantiate(NetPrefabs.Instance.itemPickup, pPosition, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            ItemPickup lPickup = lObject.GetComponent<ItemPickup>();
            lObject.Spawn(true);
            lPickup.ItemIndex.Value = pItem.Index;
            lPickup.Magazine.Value = pMagazine;
            lPickup.Reserve.Value = pReserve;
            lPickup.Resting.Value = pVelocity == Vector3.zero;
            if (pVelocity != Vector3.zero)
            {
                Rigidbody lBody = lObject.GetComponent<Rigidbody>();
                lBody.isKinematic = false;
                lBody.linearVelocity = pVelocity;
            }
            return lPickup;
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            _body = GetComponent<Rigidbody>();
            if (!IsServer && _body != null) _body.isKinematic = true;
            ItemIndex.OnValueChanged += (_, __) => BuildVisual();
            BuildVisual();
        }

        private void BuildVisual()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            ItemDefinition lItem = Item;
            if (lItem == null) return;
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            GameObject lModel = WeaponModels.Build(lItem, _visual, GameLayers.PICKUP);
            lModel.transform.localScale = Vector3.one * (lItem.slot == ItemSlot.Gadget ? 1.5f : 1.2f);
        }

        private void Update()
        {
            if (_visual == null) return;
            bool lResting = _body == null || _body.isKinematic || _body.IsSleeping();
            _visual.localPosition = lResting ? new Vector3(0, 0.08f, 0) : Vector3.zero;
            // A weapon lies on its side; small things sit as they are.
            _visual.localRotation = lResting && Item != null && Item.slot == ItemSlot.Weapon ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            _visual.localScale = Vector3.one * Mathf.MoveTowards(_visual.localScale.x, Focused == this ? 1.2f : 1f, Time.deltaTime * 3f);
            if (IsServer && _body != null && !_body.isKinematic && _body.IsSleeping())
            {
                _body.isKinematic = true;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            }
        }
    }
}
