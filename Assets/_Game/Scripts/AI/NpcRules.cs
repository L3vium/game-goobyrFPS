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
    }
}
