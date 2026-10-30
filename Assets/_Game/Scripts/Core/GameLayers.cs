using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Core
{
    /// <summary>
    /// The physics layers set up in the TagManager, and the masks every system queries with.
    /// One place so a raycast never hard-codes an index.
    /// </summary>
    public static class GameLayers
    {
        public const int DEFAULT = 0;
        public const int WATER = 4;
        public const int PLAYER = 8;
        public const int NPC = 9;
        public const int RAGDOLL = 10;
        public const int INTERACTABLE = 11;
        public const int PICKUP = 12;
        public const int SMOKE = 13;
        public const int GRASS = 14;
        public const int CLIMBABLE = 15;
        public const int LOCAL_VIEW = 16;
        public const int TRIGGER = 17;

        /// <summary>What a bullet stops on: world, characters, props you can hit.</summary>
        public static readonly int ShotMask = (1 << DEFAULT) | (1 << PLAYER) | (1 << NPC) | (1 << INTERACTABLE) | (1 << CLIMBABLE) | (1 << RAGDOLL);

        /// <summary>What blocks a line of sight (smoke and grass are handled by the perception, not by raycasts).</summary>
        public static readonly int SightBlockMask = (1 << DEFAULT) | (1 << INTERACTABLE) | (1 << CLIMBABLE);

        /// <summary>What the character controller walks on and bumps into.</summary>
        public static readonly int WorldMask = (1 << DEFAULT) | (1 << INTERACTABLE) | (1 << CLIMBABLE);

        /// <summary>Living characters (players and NPCs).</summary>
        public static readonly int CharacterMask = (1 << PLAYER) | (1 << NPC);

        public static bool IsIn(int pLayer, int pMask) => (pMask & (1 << pLayer)) != 0;

        public static void SetRecursive(GameObject pObject, int pLayer)
        {
            pObject.layer = pLayer;
            foreach (Transform lChild in pObject.transform) SetRecursive(lChild.gameObject, pLayer);
        }
    }
}
