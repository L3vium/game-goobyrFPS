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
    /// The air-handling unit in a building's basement. Pour poison in, and after a delay green gas
    /// pours from every vent of the building: everyone inside - guards, target, players - dies.
    /// Once per match. Loud, final, and visible from outside.
    /// </summary>
    public class VentPoison : Interactable
    {
        [SerializeField] private BuildingVolume _building;
        [SerializeField] private Transform[] _vents;

        private float _timer;
        private ulong _by;
        private int _team;
        private readonly List<GameObject> _gas = new List<GameObject>();

        public void Setup(BuildingVolume pBuilding, Transform[] pVents)
        {
            _building = pBuilding;
            _vents = pVents;
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => pHeld != null && pHeld.kind == ItemKind.Poison ? "POISON THE VENTILATION" : "VENTILATION (NEEDS LETHAL POISON)";

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            ItemDefinition lHeld = GameDatabase.Item(pHeldItem);
            if (lHeld == null || lHeld.kind != ItemKind.Poison || !pWho.Weapons.ConsumeItemServer(pHeldItem)) return;
            _by = pWho.OwnerClientId;
            _team = pWho.Team.Value;
            _timer = GameDatabase.Rules.ventGasDelay;
            SetStateServer(1);
            if (MapEffects.Instance != null) MapEffects.Instance.ToastRpc("SOMETHING IS WRONG WITH THE AIR IN " + _building.BuildingName.ToUpperInvariant());
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew == 1) Audio.Sfx.Play("vent_hiss", transform.position, 1f, 30f);
            if (pNew == 2 && _vents != null)
            {
                foreach (Transform lVent in _vents) if (lVent != null) _gas.Add(Effects.Gas(lVent, Vector3.zero));
                if (_building != null) _building.Gassed = true;
                Audio.Sfx.Play("gas_alarm", transform.position + Vector3.up * 6f, 1f, 120f);
            }
            if (pNew == 3)
            {
                foreach (GameObject lGas in _gas) if (lGas != null) Destroy(lGas, 4f);
                _gas.Clear();
                if (_building != null) _building.Gassed = false;
            }
        }

        private void Update()
        {
            if (State == 0 || State == 3 || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            _timer -= Time.deltaTime;
            if (State == 1 && _timer <= 0f)
            {
                SetStateServer(2);
                _timer = GameDatabase.Rules.ventGasDuration;
                NoiseSystem.Emit(_building.transform.position, 60f, NoiseKind.Alarming, _by);
            }
            else if (State == 2)
            {
                // Gas kills over the duration: everyone inside, a few each tick.
                foreach (Damageable lVictim in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                {
                    if (!lVictim.IsAlive || _building == null || !_building.Contains(lVictim.transform.position + Vector3.up)) continue;
                    lVictim.ApplyDamage(new DamageInfo { amount = 30f * Time.deltaTime, type = DamageType.Gas, point = lVictim.transform.position, direction = Vector3.up, attacker = _by, attackerTeam = Match.MatchRulesLogic.NO_TEAM });
                }
                if (_timer <= 0f) SetStateServer(3);
            }
        }
    }
}
