using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// The armoured cable from an <see cref="AutoTurret"/> to its <see cref="TurretTerminal"/>, clipped
    /// along walls and ceilings: following it is how you find the terminal. Instinct draws it in the
    /// turret's colour.
    /// </summary>
    public class TurretCable : MonoBehaviour
    {
        [SerializeField] private AutoTurret _turret;

        private static readonly List<TurretCable> _all = new List<TurretCable>();
        public static IReadOnlyList<TurretCable> All => _all;

        private Renderer[] _renderers;

        public AutoTurret Turret => _turret;
        public Renderer[] Renderers => _renderers ??= GetComponentsInChildren<Renderer>();

        public void Setup(AutoTurret pTurret) => _turret = pTurret;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);
    }
}
