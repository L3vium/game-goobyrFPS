using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// The control PC of one or more <see cref="AutoTurret"/>s, tucked away where the cables lead (a
    /// maintenance closet, a roof hut, the cellar). Hacking it (a faint keyboard rattle) turns its
    /// turrets on the guards for the rest of the match. State 0 locked, 1 hacked.
    /// </summary>
    public class TurretTerminal : Interactable
    {
        [SerializeField] private AutoTurret[] _turrets = new AutoTurret[0];
        [SerializeField] private Renderer _screen;

        private MaterialPropertyBlock _block;

        public AutoTurret[] Turrets => _turrets;

        public void Setup(AutoTurret[] pTurrets, Renderer pScreen)
        {
            _turrets = pTurrets;
            _screen = pScreen;
            _prompt = "HACK THE TURRET CONTROL";
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0 ? _prompt : "";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            if (State != 0) return;
            ulong lBy = pWho != null ? pWho.OwnerClientId : ulong.MaxValue;
            SetStateServer(1);
            PlayEffectServer(0);
            NoiseSystem.Emit(transform.position, 3f, NoiseKind.Suspicious, lBy);
            foreach (AutoTurret lTurret in _turrets) if (lTurret != null) lTurret.TurnServer(lBy);
            if (pWho != null) pWho.NotifyRpc("TURRETS HACKED", "They now fire on the guards");
        }

        private void OnEnable() => Tint();

        protected override void OnStateChanged(int pOld, int pNew) => Tint();

        public override void OnEffect(int pEffect) => Audio.Sfx.Play("keyboard", transform.position, 0.7f, 6f);

        /// <summary>Screen: red lock screen, green once hacked.</summary>
        private void Tint()
        {
            if (_screen == null) return;
            _block ??= new MaterialPropertyBlock();
            Color lColor = State == 0 ? new Color(2.2f, 0.25f, 0.2f) : new Color(0.25f, 2.4f, 0.5f);
            _screen.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", lColor / 3f);
            _block.SetColor("_EmissionColor", lColor);
            _screen.SetPropertyBlock(_block);
        }
    }
}
