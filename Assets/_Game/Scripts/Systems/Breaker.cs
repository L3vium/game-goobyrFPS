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
    /// The breaker in a building's utility room. Off: every light of the building dies, NPCs see
    /// far less inside, and the nearest one walks over to switch it back on (a distraction with a
    /// known destination).
    /// </summary>
    public class Breaker : Interactable
    {
        [SerializeField] private BuildingVolume _building;
        [SerializeField] private Transform _lever;

        private readonly List<Light> _lights = new List<Light>();
        private readonly List<Renderer> _bulbs = new List<Renderer>();

        public BuildingVolume Building => _building;
        public bool PowerOn => State == 0;

        public void Setup(BuildingVolume pBuilding, Transform pLever)
        {
            _building = pBuilding;
            _lever = pLever;
        }

        private void Start()
        {
            if (_building == null) return;
            foreach (Light lLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (lLight.type != LightType.Directional && _building.Contains(lLight.transform.position)) _lights.Add(lLight);
            foreach (PoweredLight lBulb in FindObjectsByType<PoweredLight>(FindObjectsSortMode.None))
                if (_building.Contains(lBulb.transform.position)) _bulbs.AddRange(lBulb.GetComponentsInChildren<Renderer>());
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0 ? "CUT THE POWER" : "RESTORE POWER";

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => ToggleServer(pWho != null ? pWho.OwnerClientId : ulong.MaxValue);

        public void ToggleServer(ulong pBy)
        {
            int lNext = State == 0 ? 1 : 0;
            SetStateServer(lNext);
            if (lNext == 1) NoiseSystem.Emit(transform.position, 0f, NoiseKind.Distraction, pBy);
            BreakerAlert.Raise(this);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            bool lOn = pNew == 0;
            if (_building != null) _building.PowerOn = lOn;
            foreach (Light lLight in _lights) if (lLight != null) lLight.enabled = lOn;
            foreach (Renderer lBulb in _bulbs) if (lBulb != null) GameArt.SetEmission(lBulb, lOn ? new Color(1.5f, 1.3f, 0.9f) : Color.black);
            if (_lever != null) _lever.localRotation = Quaternion.Euler(lOn ? -35f : 35f, 0, 0);
            Effects.Sparks(transform.position + transform.forward * 0.2f, lOn ? 4 : 18);
            Audio.Sfx.Play(lOn ? "power_on" : "power_off", transform.position, 1f, 40f);
        }
    }
}
