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
    /// A radio, a TV, a car alarm, a phone, a generator: switch it on and it makes noise until
    /// someone comes to switch it off - that someone is the nearest NPC.
    /// </summary>
    public class Distraction : Interactable
    {
        [SerializeField] private string _sound = "radio";
        [SerializeField] private float _radius = 18f;
        [SerializeField] private float _duration = 25f;

        private float _timer;
        private float _pulse;

        public void Setup(string pPrompt, string pSound, float pRadius)
        {
            _prompt = pPrompt;
            _sound = pSound;
            _radius = pRadius;
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => ActivateServer(pWho != null ? pWho.OwnerClientId : ulong.MaxValue);

        public void ActivateServer(ulong pBy)
        {
            if (State != 0) return;
            _timer = _duration;
            _pulse = 0f;
            SetStateServer(1);
            NoiseSystem.Emit(transform.position, _radius, NoiseKind.Distraction, pBy);
        }

        /// <summary>Host: an NPC reached it and turned it off.</summary>
        public void TurnOffServer()
        {
            if (State == 1) SetStateServer(0);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew == 1) Juice.Squash(transform, 0.25f);
        }

        private void Update()
        {
            if (State != 1) return;
            _pulse -= Time.deltaTime;
            if (_pulse <= 0f)
            {
                _pulse = 1.2f;
                Audio.Sfx.Play(_sound, transform.position, 0.8f, _radius * 1.5f);
                Juice.Squash(transform, 0.08f, 0.2f);
            }
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) SetStateServer(0);
        }
    }
}
