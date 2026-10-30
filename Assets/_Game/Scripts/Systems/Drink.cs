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
    /// A glass on a table along the target's routine. Poison it: lethal poison kills whoever drinks,
    /// emetic poison sends them running to the nearest toilet - alone.
    /// </summary>
    public class Drink : Interactable
    {
        public const int CLEAN = 0, LETHAL = 1, EMETIC = 2, EMPTY = 3;

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld)
        {
            if (pHeld != null && pHeld.kind == ItemKind.Poison) return "POISON THE DRINK (LETHAL)";
            if (pHeld != null && pHeld.kind == ItemKind.EmeticPoison) return "POISON THE DRINK (EMETIC)";
            return "A DRINK (HOLD A POISON)";
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == CLEAN;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            ItemDefinition lHeld = GameDatabase.Item(pHeldItem);
            if (lHeld == null || !lHeld.IsPoison || !pWho.Weapons.ConsumeItemServer(pHeldItem)) return;
            Poisoner = pWho.OwnerClientId;
            PoisonerTeam = pWho.Team.Value;
            SetStateServer(lHeld.kind == ItemKind.Poison ? LETHAL : EMETIC);
        }

        public ulong Poisoner { get; private set; } = DamageInfo.NO_PLAYER;
        public int PoisonerTeam { get; private set; } = Match.MatchRulesLogic.NO_TEAM;

        /// <summary>Host: an NPC drinks it. Returns the poison it held.</summary>
        public int DrinkServer()
        {
            int lWas = State;
            SetStateServer(EMPTY);
            return lWas;
        }

        /// <summary>Host: a fresh glass after a while.</summary>
        public void RefillServer() => SetStateServer(CLEAN);

        protected override void OnStateChanged(int pOld, int pNew)
        {
            Renderer lLiquid = transform.Find("Liquid")?.GetComponent<Renderer>();
            if (lLiquid != null)
            {
                lLiquid.enabled = pNew != EMPTY;
                // Poison is only visible up close: a faint green fizz.
                GameArt.SetEmission(lLiquid, pNew == LETHAL || pNew == EMETIC ? new Color(0.05f, 0.25f, 0.02f) : Color.black);
            }
            if (pNew == LETHAL || pNew == EMETIC) Audio.Sfx.Play("pour", transform.position, 0.6f, 6f);
        }
    }
}
