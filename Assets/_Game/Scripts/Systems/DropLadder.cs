using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// A one-way shortcut: a ladder folded up on a roof or a tower, lowered from the top. Once down
    /// it stays down and anyone can climb it from below (its climb volume switches on).
    /// </summary>
    public class DropLadder : Interactable
    {
        [SerializeField] private GameObject _ladder;

        public void Setup(GameObject pLadder)
        {
            _ladder = pLadder;
            _prompt = "LOWER LADDER";
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0 ? _prompt : "";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            if (State != 0) return;
            SetStateServer(1);
            PlayEffectServer(0);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (_ladder != null) _ladder.SetActive(pNew == 1);
        }

        public override void OnEffect(int pEffect) => Audio.Sfx.Play("ladder_drop", transform.position, 0.9f, 18f);
    }
}
