using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// The NPC brain's decisions, kept pure so they can be tested (how fast the senses work lives in
    /// <see cref="DetectionModel"/>). <see cref="Npc"/> asks these rules; it never re-implements them.
    /// </summary>
    public static class NpcRules
    {
        // ---------------------------------------------------------------- states

        /// <summary>
        /// Can a glimpse of a player make this NPC stop and stare (and walk over past half)? Only
        /// while it goes about its business or calmly checks something out. Someone already hunting -
        /// searching, or running to a shot - is past that: a glimpse sends it straight at the player,
        /// weapon up, and a full gauge means a fight. It never cools down into a calm investigator.
        /// </summary>
        public static bool CanBecomeSuspicious(NpcState pState, bool pInvestigateAlarmed) => pState switch
        {
            NpcState.Idle or NpcState.Patrol or NpcState.Routine or NpcState.Escort or NpcState.Suspicious => true,
            NpcState.Investigate => !pInvestigateAlarmed,
            _ => false
        };

        /// <summary>Weapon up and expecting trouble (searching, or checking out a shot): sees faster and hunts what it glimpses.</summary>
        public static bool OnEdge(NpcState pState, bool pInvestigateAlarmed) => pState == NpcState.Search || (pState == NpcState.Investigate && pInvestigateAlarmed);

        /// <summary>
        /// States in which the NPC turns itself toward something (its aim, the player it stares at or
        /// confronts, the caller's threat) while it may still be walking: the NavMeshAgent must not
        /// steer its rotation then, or it swings the body back along the path and the aim with it.
        /// </summary>
        public static bool AimsManually(NpcState pState) => pState is NpcState.Engaged or NpcState.Combat or NpcState.Calling or NpcState.Suspicious;

        // ---------------------------------------------------------------- spotting

        public enum SpotReaction : byte { Confront, Fight, Flee }

        /// <summary>
        /// What an NPC does the moment its gauge fills on a player. The unarmed run. A player its zone
        /// knows to be hostile (<see cref="HostileMemory"/>) is fought on sight, wherever they stand.
        /// Someone already hunting fights a trespasser at once. Anyone else is confronted first - and
        /// in a public place a confrontation of a player who behaves ends without a shot.
        /// </summary>
        public static SpotReaction OnSpotted(bool pFlees, bool pOnEdge, bool pKnownHostile, SecurityTier pTier, bool pCrime = false)
        {
            if (pFlees) return SpotReaction.Flee;
            if (pKnownHostile || pCrime) return SpotReaction.Fight;
            if (pOnEdge && pTier != SecurityTier.Public) return SpotReaction.Fight;
            return SpotReaction.Confront;
        }

        // ---------------------------------------------------------------- weapons in hand

        /// <summary>A weapon anyone recognises from across a room: every gun, the rocket launcher, the flamethrower.</summary>
        public static bool IsVisibleWeapon(Data.ItemKind pKind) => pKind is Data.ItemKind.Pistol or Data.ItemKind.SilencedPistol or Data.ItemKind.Smg
            or Data.ItemKind.Shotgun or Data.ItemKind.Sniper or Data.ItemKind.RocketLauncher or Data.ItemKind.Flamethrower;

        /// <summary>Behaviour that gives a player away even in a public place: what the player does (aiming, sprinting, climbing, the proof) or a weapon in hand.</summary>
        public static bool ActsSuspicious(bool pBehaviour, bool pWeaponDrawn) => pBehaviour || pWeaponDrawn;

        /// <summary>
        /// A weapon out anywhere but a public place is a crime for an armed guard who sees it: it
        /// fights, no confrontation. In a public place it is only suspicious - the confrontation is
        /// the chance to put it away.
        /// </summary>
        public static bool IsCrime(SecurityTier pTier, bool pWeaponDrawn) => pWeaponDrawn && pTier != SecurityTier.Public;
    }
}
