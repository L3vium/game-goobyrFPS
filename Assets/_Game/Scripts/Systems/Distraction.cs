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
    /// A radio, a TV, a car alarm, a phone, a generator, a fan, a garden hose, an office PC: switch it
    /// on (or hack it) and it makes noise - and, for the fan, the hose and the PC, a show (spinning
    /// blades, a spray of water, a flashing alarm screen) - until someone comes to switch it off:
    /// that someone is the nearest NPC.
    /// </summary>
    public class Distraction : Interactable
    {
        [SerializeField] private string _sound = "radio";
        [SerializeField] private float _radius = 18f;
        [SerializeField] private float _duration = 25f;

        [SerializeField] private Transform _spinner;
        [SerializeField] private Transform _nozzle;
        [SerializeField] private Renderer _screen;

        private float _timer;
        private float _pulse;
        private float _spin;
        private GameObject _spray;
        private MaterialPropertyBlock _block;

        public void Setup(string pPrompt, string pSound, float pRadius)
        {
            _prompt = pPrompt;
            _sound = pSound;
            _radius = pRadius;
        }

        /// <summary>Optional moving parts: fan blades (spin on local Z), a hose nozzle (sprays along +Z), a screen (flashes).</summary>
        public void SetupFx(Transform pSpinner, Transform pNozzle, Renderer pScreen)
        {
            _spinner = pSpinner;
            _nozzle = pNozzle;
            _screen = pScreen;
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
            if (_nozzle != null)
            {
                if (pNew == 1 && _spray == null)
                {
                    _spray = Effects.Spray(_nozzle, Vector3.zero);
                    _spray.transform.localRotation = Quaternion.identity;
                }
                else if (pNew == 0 && _spray != null) { Destroy(_spray); _spray = null; }
            }
            if (pNew == 0) Screen(new Color(0.15f, 0.35f, 0.9f));
        }

        private void OnEnable() => Screen(new Color(0.15f, 0.35f, 0.9f));

        private void Screen(Color pColor)
        {
            if (_screen == null) return;
            _block ??= new MaterialPropertyBlock();
            _screen.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", pColor / 3f);
            _block.SetColor("_EmissionColor", pColor);
            _screen.SetPropertyBlock(_block);
        }

        private void Update()
        {
            if (_spinner != null)
            {
                _spin = Mathf.MoveTowards(_spin, State == 1 ? 1100f : 0f, Time.deltaTime * (State == 1 ? 900f : 350f));
                if (_spin > 0f) _spinner.Rotate(0f, 0f, _spin * Time.deltaTime, Space.Self);
            }
            if (State != 1) return;
            if (_screen != null) Screen(Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? new Color(3f, 0.2f, 0.15f) : new Color(2.5f, 2.5f, 2.5f));
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
