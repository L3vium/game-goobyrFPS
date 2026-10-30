using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Data
{
    public enum ItemKind
    {
        Fists,
        Pistol,
        SilencedPistol,
        Smg,
        Shotgun,
        Sniper,
        RocketLauncher,
        Flamethrower,
        Coin,
        Brick,
        SmokeGrenade,
        C4,
        Poison,
        EmeticPoison
    }

    public enum ItemSlot
    {
        /// <summary>Takes one of the two weapon slots.</summary>
        Weapon,
        /// <summary>Stacks in the gadget slots (throwables, C4, poison).</summary>
        Gadget
    }

    /// <summary>
    /// Everything the player can hold: guns, the flamethrower, throwables, C4, poison. Edited in the
    /// GD Tool; the host and every client look items up by <see cref="Index"/> in
    /// <see cref="GameDatabase.Items"/>, so an item crosses the network as one byte.
    /// </summary>
    [CreateAssetMenu(menuName = "Goobyr/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Item";
        public ItemKind kind;
        public ItemSlot slot = ItemSlot.Weapon;
        public Color color = Color.gray;
        [Tooltip("Only one of these exists on a map (rocket launcher, flamethrower).")]
        public bool unique;

        [Header("Firing")]
        public float damage = 50f;
        public float headMultiplier = 3f;
        [Tooltip("Shots per second.")]
        public float fireRate = 3f;
        public bool automatic;
        public int pellets = 1;
        [Tooltip("Spread half-angle in degrees, hip fire.")]
        public float spread = 1.5f;
        [Tooltip("Spread multiplier when aiming down sights.")]
        public float adsSpreadFactor = 0.3f;
        public float range = 120f;
        public int magazine = 12;
        public int reserveAmmo = 48;
        public float reloadTime = 1.4f;

        [Header("Feel")]
        public float recoil = 2f;
        public float adsFov = 55f;
        public float shake = 0.3f;
        [Tooltip("How far the shot is heard by NPCs, in metres. Silenced weapons stay small.")]
        public float noiseRadius = 40f;

        [Header("Projectiles and areas")]
        [Tooltip("0 = hitscan.")]
        public float projectileSpeed;
        public float explosionRadius;
        public float throwForce = 18f;
        public int stack = 1;

        /// <summary>Position in <see cref="GameDatabase.Items"/>; set by the database on load.</summary>
        [System.NonSerialized] public int Index = -1;

        public bool IsGun => kind is ItemKind.Pistol or ItemKind.SilencedPistol or ItemKind.Smg or ItemKind.Shotgun or ItemKind.Sniper;
        public bool IsThrowable => kind is ItemKind.Coin or ItemKind.Brick or ItemKind.SmokeGrenade or ItemKind.C4;
        public bool IsPoison => kind is ItemKind.Poison or ItemKind.EmeticPoison;
        public bool UsesAmmo => IsGun || kind is ItemKind.RocketLauncher or ItemKind.Flamethrower;
    }
}
