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
    /// An item lying on the map: a weapon, ammo or a gadget, bobbing and glowing a little so it
    /// reads from afar. Spawned by the host from the map's item spawn points and from the dead.
    /// </summary>
    public class ItemPickup : NetworkBehaviour
    {
        public readonly NetworkVariable<int> ItemIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Magazine = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Reserve = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> Resting = new NetworkVariable<bool>(true);

        public ItemDefinition Item => GameDatabase.Item(ItemIndex.Value);

        private Transform _visual;
        private float _phase;
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

        public override void OnNetworkSpawn()
        {
            _body = GetComponent<Rigidbody>();
            if (!IsServer && _body != null) _body.isKinematic = true;
            ItemIndex.OnValueChanged += (_, __) => BuildVisual();
            BuildVisual();
            _phase = Random.value * 6f;
        }

        private void BuildVisual()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            ItemDefinition lItem = Item;
            if (lItem == null) return;
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            GameObject lModel = WeaponModels.Build(lItem, _visual, GameLayers.PICKUP);
            lModel.transform.localScale = Vector3.one * (lItem.slot == ItemSlot.Gadget ? 2.2f : 1.5f);
            // A glow ring under it: rarity colour (gold for the unique ones).
            GameObject lRing = GameArt.Part(_visual, "Ring", LowPolyMesh.Cached("fx.ring", () => LowPolyMesh.Cylinder(24)), new Vector3(0, -0.28f, 0), new Vector3(0.8f, 0.01f, 0.8f), lItem.unique ? new Color(2f, 1.4f, 0.2f) : lItem.color * 1.2f, GameArt.Glow);
            lRing.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (_visual == null) return;
            _phase += Time.deltaTime;
            bool lResting = _body == null || _body.isKinematic || _body.IsSleeping();
            _visual.localPosition = lResting ? new Vector3(0, 0.35f + Mathf.Sin(_phase * 2f) * 0.06f, 0) : Vector3.zero;
            _visual.localRotation = lResting ? Quaternion.Euler(0, _phase * 50f, 0) : Quaternion.identity;
            if (IsServer && _body != null && !_body.isKinematic && _body.IsSleeping())
            {
                _body.isKinematic = true;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            }
        }
    }
}
