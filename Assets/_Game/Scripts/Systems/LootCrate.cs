using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// A crate with a coloured band, here and there on the maps: opened with the interact key (a little
    /// noise), it throws out a weapon or a gadget, the good ones rarely. Opened once.
    /// </summary>
    public class LootCrate : Interactable
    {
        private static readonly ItemKind[] COMMON = { ItemKind.Pistol, ItemKind.SilencedPistol, ItemKind.Revolver, ItemKind.Knife, ItemKind.Bat, ItemKind.FryingPan, ItemKind.Machete, ItemKind.Brick, ItemKind.SmokeGrenade, ItemKind.RadioDecoy, ItemKind.Firecrackers, ItemKind.RubberDuck };
        private static readonly ItemKind[] GOOD = { ItemKind.Smg, ItemKind.Shotgun, ItemKind.SawedOff, ItemKind.AssaultRifle, ItemKind.Crossbow, ItemKind.Katana, ItemKind.C4 };
        private static readonly ItemKind[] RARE = { ItemKind.Sniper, ItemKind.GrenadeLauncher, ItemKind.Grapple, ItemKind.JetPack, ItemKind.SpringBoots };
        private const float CHANCE_RARE = 0.12f, CHANCE_GOOD = 0.43f, NOISE = 6f;

        [SerializeField] private Transform _lid;
        private float _open;

        public void Setup(Transform pLid)
        {
            _lid = pLid;
            _range = 2.4f;
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => "OPEN THE CRATE";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            if (State != 0) return;
            SetStateServer(1);
            float lRoll = Random.value;
            ItemKind[] lTable = lRoll < CHANCE_RARE ? RARE : lRoll < CHANCE_GOOD ? GOOD : COMMON;
            ItemDefinition lItem = GameDatabase.Find(lTable[Random.Range(0, lTable.Length)]);
            if (lItem != null) ItemPickup.SpawnServer(lItem, transform.position + Vector3.up * 1.1f, -1, -1, Vector3.up * 3.5f + Random.insideUnitSphere);
            AI.NoiseSystem.Emit(transform.position, NOISE, AI.NoiseKind.Footstep, pWho != null ? pWho.OwnerClientId : ulong.MaxValue);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew != 1) return;
            Audio.Sfx.Play("pickup", transform.position, 1f, 15f);
            Effects.Dust(transform.position + Vector3.up, 0.5f);
            enabled = true;
        }

        private void Update()
        {
            if (State == 0 || _lid == null) { enabled = State != 0; return; }
            _open = Mathf.MoveTowards(_open, 1f, Time.deltaTime * 4f);
            // A lid thrown back with a little bounce.
            float lEase = 1f - Mathf.Pow(1f - _open, 3f);
            _lid.localRotation = Quaternion.Euler(-105f * lEase * (1f + Mathf.Sin(_open * Mathf.PI) * 0.2f), 0f, 0f);
            if (_open >= 1f) enabled = false;
        }
    }
}
