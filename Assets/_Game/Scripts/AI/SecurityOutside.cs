using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// Placed once in a map by its builder: the tier of the ground outside every
    /// <see cref="SecurityZone"/> (the jungle around San Lázaro is restricted, not hostile). Put back
    /// to hostile when the map unloads.
    /// </summary>
    public class SecurityOutside : MonoBehaviour
    {
        [SerializeField] private SecurityTier _tier = SecurityTier.Restricted;

        public void Setup(SecurityTier pTier) => _tier = pTier;

        private void OnEnable() => SecurityZone.Outside = _tier;

        private void OnDisable() => SecurityZone.Outside = SecurityTier.Hostile;
    }
}
