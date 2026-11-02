using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// The monitoring room's recorder: switch it off and every camera of its zone goes dark for the
    /// rest of the match (the guard watching the screens comes to check after a while).
    /// </summary>
    public class CameraRecorder : Interactable
    {
        [SerializeField] private int _zone;

        public void Setup(int pZone)
        {
            _zone = pZone;
            _prompt = "WIPE AND SHUT DOWN CAMERAS";
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0 ? _prompt : "";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            if (State != 0) return;
            SetStateServer(1);
            PlayEffectServer(0);
            // The screens go black: someone will wander in to look, in a minute.
            Invoke(nameof(CheckServer), 60f);
        }

        private void CheckServer() => NoiseSystem.Emit(transform.position, 25f, NoiseKind.Distraction);

        protected override void OnStateChanged(int pOld, int pNew) => SecurityCamera.SetZone(_zone, pNew == 1);

        public override void OnEffect(int pEffect) => Audio.Sfx.Play("switch", transform.position, 0.8f, 8f);
    }
}
