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
    /// The flare launcher / siren of a lair: an NPC (or a player) fires it and the whole map is
    /// alerted toward this position - reinforcements of every zone converge.
    /// </summary>
    public class AlarmTrigger : Interactable
    {
        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => "FIRE THE ALARM FLARE";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => FireServer(transform.position);

        public void FireServer(Vector3 pAlertPoint)
        {
            if (State != 0) return;
            SetStateServer(1);
            AlertDirector.RaiseGlobal(pAlertPoint);
            Invoke(nameof(Rearm), 60f);
        }

        private void Rearm() => SetStateServer(0);

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew != 1) return;
            Effects.Muzzle(transform.position + Vector3.up, Vector3.up, 3f);
            FlareVisual.Launch(transform.position + Vector3.up);
            Audio.Sfx.Play("siren", transform.position, 1f, 400f, 0f);
            Juice.Flash(new Color(1f, 0.3f, 0.2f), 0.15f);
            UI.Hud.Toast("ALARM! THE WHOLE AREA IS ON ALERT");
        }
    }
}
