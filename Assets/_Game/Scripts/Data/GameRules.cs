using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Data
{
    /// <summary>
    /// The tuning a designer changes without code: loadout, health, damage, AI senses, match
    /// timings. One asset (<c>Assets/_Game/Data/Resources/GameDatabase</c> points at it), edited in
    /// the GD Tool.
    /// </summary>
    [CreateAssetMenu(menuName = "Goobyr/Game Rules")]
    public class GameRules : ScriptableObject
    {
        [Header("Players")]
        [Tooltip("What every player spawns with. The silenced pistol by default.")]
        public List<ItemDefinition> startingLoadout = new List<ItemDefinition>();
        public float playerHealth = 100f;
        [Tooltip("Multiplies the damage players deal.")]
        public float playerDamageMultiplier = 1f;
        public int maxPlayers = 8;
        [Tooltip("F1 to F10 cheats on a living agent, listed bottom right. Always on in the Hitmen Dev build.")]
        public bool cheatsEnabled;

        [Header("Match")]
        public float timeLimitMinutes = 20f;
        public bool proximityChatByDefault = true;
        public float proximityChatRange = 30f;
        [Tooltip("Seconds between two reveals of the proof carrier to everyone, when they have just taken it.")]
        public float proofRevealInterval = 15f;
        [Tooltip("The same, once they have held it for the whole ramp.")]
        public float proofRevealFastest = 3f;
        [Tooltip("Seconds of carrying over which the reveals go from the slowest to the fastest.")]
        public float proofRevealRamp = 150f;
        [Tooltip("Seconds a carrier stays inside an extraction point to win.")]
        public float extractionTime = 3f;
        public int activeExtractions = 2;

        [Header("NPCs")]
        public float npcHealth = 100f;
        [Tooltip("Multiplies the damage NPCs deal.")]
        public float npcDamageMultiplier = 0.7f;
        public float npcVisionRange = 14f;
        public float npcVisionAngle = 90f;
        [Tooltip("Seconds of full exposure before an NPC is sure it saw a player.")]
        public float npcDetectTime = 2f;
        public float npcReactionTime = 0.7f;
        [Tooltip("Seconds of radio an NPC needs to call reinforcements. Kill it before.")]
        public float callDuration = 3f;
        public int reinforcementsPerCall = 4;
        public int maxCallsPerZone = 2;
        [Tooltip("Seconds an NPC searches around the last known position before giving up.")]
        public float searchDuration = 30f;
        [Tooltip("Hit chance of an NPC at 10 m against a standing, moving target.")]
        [Range(0f, 1f)] public float npcAccuracy = 0.32f;

        [Header("Systems")]
        public float ventGasDelay = 6f;
        public float ventGasDuration = 10f;
        public float fireSpreadInterval = 1.2f;
    }
}
