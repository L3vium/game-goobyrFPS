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

        /// <summary>Seconds an NPC looks around the spot it went to check: longer when it came weapon up.</summary>
        public static float LookAroundTime(bool pAlarmed) => pAlarmed ? 7f : 4f;

        /// <summary>
        /// Has it looked around long enough? Its own clock, started when it reached the spot
        /// (<paramref name="pArrivedAt"/> &lt; 0: not there yet) - never a timer left over from a patrol pause.
        /// </summary>
        public static bool LookedAround(float pArrivedAt, float pNow, float pFor) => pArrivedAt >= 0f && pNow - pArrivedAt >= pFor;

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

        // ---------------------------------------------------------------- the phone call

        /// <summary>Seconds of real fighting before anyone thinks of the radio.</summary>
        public const float CALL_DELAY = 1.5f;
        /// <summary>Seconds after a completed call before the zone's next one; after an interrupted one, before someone tries again.</summary>
        public const float CALL_COOLDOWN = 30f, CALL_RETRY = 8f;

        /// <summary>
        /// Does the zone hand someone the radio now? Once a real fight has gone on a moment, never while
        /// a call is under way or right after one, never past the zone's calls - and never to the last
        /// one fighting: someone has to keep shooting, so it takes two fighters.
        /// </summary>
        public static bool ShouldCall(bool pCallInProgress, int pCallsMade, int pMaxCalls, float pFightingFor, int pFighters, float pNow, float pNextCallAt)
            => !pCallInProgress && pCallsMade < pMaxCalls && pFightingFor > CALL_DELAY && pFighters >= 2 && pNow >= pNextCallAt;

        /// <summary>A caller drops the phone and fights as soon as nobody else is left fighting.</summary>
        public static bool KeepsCalling(int pOtherFighters) => pOtherFighters > 0;

        // ---------------------------------------------------------------- hearing

        /// <summary>An NPC's gunfire is a sound like any other, at most once every this many seconds per shooter.</summary>
        public const float SHOT_NOISE_INTERVAL = 1.5f;

        /// <summary>Is it time for this shooter's gunfire to be heard again?</summary>
        public static bool ShotNoiseDue(float pLastNoise, float pNow) => pNow - pLastNoise >= SHOT_NOISE_INTERVAL || pNow < pLastNoise;

        /// <summary>
        /// Gunfire and explosions bring every armed guard who hears them (walls and distance already
        /// decide who does); the unarmed, and every other kind of sound, keep the cap of the few who
        /// hear it best (<see cref="DetectionModel.Responders"/>). A capped sound (a far siren) is
        /// capped for everyone.
        /// </summary>
        public static bool AlwaysResponds(NoiseKind pKind, bool pArmedGuard, bool pCapped) => pKind == NoiseKind.Alarming && pArmedGuard && !pCapped;

        /// <summary>
        /// Does a collider muffle a sound passing through it? Terrain does, and anything big enough to
        /// be a wall - at least 1.9 m tall and 0.9 m across: building shells, doors, big rocks,
        /// containers. Glass, crates, furniture, fences, cars and tree trunks don't.
        /// </summary>
        public static bool IsStructural(bool pTerrain, Vector3 pBoundsSize, bool pSeeThrough)
        {
            if (pSeeThrough) return false;
            if (pTerrain) return true;
            return pBoundsSize.y >= 1.9f && Mathf.Max(pBoundsSize.x, pBoundsSize.z) >= 0.9f;
        }

        /// <summary>Structural hits closer than this along a ray are one wall (the core, plaster and panelling of one partition).</summary>
        public const float WALL_MERGE = 0.6f;

        /// <summary>The generated window panes' object and material names (one "glass" batch per building).</summary>
        public const string GLASS_OBJECT = "glass", GLASS_MATERIAL = "M_WindowGlass";

        /// <summary>
        /// Can an NPC see through this collider? Window glass: on the Glass layer if the project has
        /// one (<paramref name="pGlassLayer"/> &gt;= 0), or the building generator's glass batch.
        /// </summary>
        public static bool IsSeeThrough(string pObjectName, string pMaterialName, int pLayer, int pGlassLayer)
        {
            if (pGlassLayer >= 0 && pLayer == pGlassLayer) return true;
            if (pObjectName == GLASS_OBJECT) return true;
            return pMaterialName != null && pMaterialName.StartsWith(GLASS_MATERIAL, System.StringComparison.Ordinal);
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
