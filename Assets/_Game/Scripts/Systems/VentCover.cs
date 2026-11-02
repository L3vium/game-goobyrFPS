using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// A vent grille over a crawlable duct: unscrewed by hand (a few seconds, a faint scrape), it
    /// stays off and the duct is open to everyone. Crouch into it to crawl.
    /// </summary>
    public class VentCover : Interactable
    {
        [SerializeField] private GameObject _grille;

        public void Setup(GameObject pGrille)
        {
            _grille = pGrille;
            _prompt = "REMOVE GRILLE";
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0 ? _prompt : "";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            if (State != 0) return;
            NoiseSystem.Emit(transform.position, 3f, NoiseKind.Suspicious, pWho != null ? pWho.OwnerClientId : ulong.MaxValue);
            SetStateServer(1);
            PlayEffectServer(0);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (_grille != null) _grille.SetActive(pNew == 0);
        }

        public override void OnEffect(int pEffect) => Audio.Sfx.Play("vent_open", transform.position, 0.7f, 8f);
    }
}
