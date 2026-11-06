using System.Collections.Generic;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Match
{
    public enum MatchMode : byte
    {
        /// <summary>1v1v1v1...: everyone for themselves.</summary>
        FreeForAll,
        /// <summary>2 to 4 teams of any size.</summary>
        Teams,
        /// <summary>One team, everyone against the AI.</summary>
        Coop
    }

    public enum MatchPhase : byte
    {
        Lobby,
        Loading,
        Playing,
        Ended
    }

    public enum MatchOutcome : byte
    {
        None,
        /// <summary>A team extracted with the proof.</summary>
        Extracted,
        /// <summary>Every player is eliminated.</summary>
        AllEliminated,
        /// <summary>Time ran out.</summary>
        TimeUp,
        /// <summary>A team protects the target: it died, the attackers win.</summary>
        TargetKilled,
        /// <summary>A team protects the target: every attacker is down, the protectors win.</summary>
        AttackersDown
    }

    /// <summary>A player as the rules see it: no Unity, so the rules are unit-tested.</summary>
    public struct RulesPlayer
    {
        public ulong clientId;
        public int team;
        public bool alive;
    }

    /// <summary>
    /// The pure part of the match rules: who is on which team, who may hurt whom, and when a
    /// match is over. Everything here is deterministic and tested in EditMode.
    /// </summary>
    public static class MatchRulesLogic
    {
        /// <summary>Seconds until the next reveal of whoever carries the proof: from the slowest to the fastest over the ramp.</summary>
        public static float RevealInterval(float pSlowest, float pFastest, float pRamp, float pCarriedFor)
        {
            float t = System.Math.Min(1f, System.Math.Max(0f, pCarriedFor / System.Math.Max(1f, pRamp)));
            return pSlowest + (pFastest - pSlowest) * t;
        }

        public const int NO_TEAM = -1;
        /// <summary>Team ids: the room's teams first, then one for each agent on their own.</summary>
        public const int MAX_TEAMS = 12;
        /// <summary>How many teams a room can make.</summary>
        public const int MAX_BOXES = 4;

        /// <summary>
        /// The team a player actually plays on. FFA gives everyone their own team (their seat),
        /// co-op puts everyone on team 0, teams keeps the chosen team clamped to the team count.
        /// </summary>
        public static int ResolveTeam(MatchMode pMode, int pSeat, int pChosenTeam, int pTeamCount)
        {
            switch (pMode)
            {
                case MatchMode.FreeForAll: return pSeat;
                case MatchMode.Coop: return 0;
                default:
                    int lCount = pTeamCount < 2 ? 2 : pTeamCount > MAX_TEAMS ? MAX_TEAMS : pTeamCount;
                    if (pChosenTeam < 0) return pSeat % lCount;
                    return pChosenTeam % lCount;
            }
        }

        /// <summary>Teammates never hurt each other; everyone else (and every NPC) can.</summary>
        public static bool CanDamage(int pAttackerTeam, int pVictimTeam)
        {
            if (pAttackerTeam == NO_TEAM || pVictimTeam == NO_TEAM) return true;
            return pAttackerTeam != pVictimTeam;
        }

        /// <summary>
        /// Balances teams for "shuffle": seats go round-robin over the team count.
        /// </summary>
        public static List<int> AutoBalance(int pPlayerCount, int pTeamCount)
        {
            List<int> lTeams = new List<int>(pPlayerCount);
            int lCount = pTeamCount < 2 ? 2 : pTeamCount > MAX_TEAMS ? MAX_TEAMS : pTeamCount;
            for (int i = 0; i < pPlayerCount; i++) lTeams.Add(i % lCount);
            return lTeams;
        }

        /// <summary>True while at least one player is alive.</summary>
        public static bool AnyAlive(IReadOnlyList<RulesPlayer> pPlayers)
        {
            foreach (RulesPlayer lPlayer in pPlayers) if (lPlayer.alive) return true;
            return false;
        }

        /// <summary>
        /// The outcome once a check happens. An extraction always wins; otherwise the match ends
        /// when nobody is alive or the clock is out.
        /// </summary>
        public static MatchOutcome Evaluate(IReadOnlyList<RulesPlayer> pPlayers, int pExtractedTeam, float pTimeLeft)
        {
            if (pExtractedTeam != NO_TEAM) return MatchOutcome.Extracted;
            if (pPlayers.Count > 0 && !AnyAlive(pPlayers)) return MatchOutcome.AllEliminated;
            if (pTimeLeft <= 0f) return MatchOutcome.TimeUp;
            return MatchOutcome.None;
        }

        /// <summary>
        /// The outcome when a team protects the target: no proof to carry. The attackers win the moment
        /// it dies; the protectors when every attacker is down, or when the clock runs out.
        /// </summary>
        public static MatchOutcome EvaluateProtect(IReadOnlyList<RulesPlayer> pPlayers, int pProtectorMask, bool pTargetDead, float pTimeLeft)
        {
            if (pTargetDead) return MatchOutcome.TargetKilled;
            bool lAttackerAlive = false;
            foreach (RulesPlayer lPlayer in pPlayers)
                if (lPlayer.alive && !IsProtector(pProtectorMask, lPlayer.team)) lAttackerAlive = true;
            if (!lAttackerAlive) return MatchOutcome.AttackersDown;
            return pTimeLeft <= 0f ? MatchOutcome.TimeUp : MatchOutcome.None;
        }

        public static bool IsProtector(int pMask, int pTeam) => pTeam >= 0 && pTeam < 31 && (pMask & (1 << pTeam)) != 0;

        /// <summary>
        /// The mode is what the teams say (an agent with no team counts as a team of one): everyone on
        /// one team is co-op, nobody sharing a team is free-for-all, anything else is teams.
        /// </summary>
        public static MatchMode ModeFromBoxes(IReadOnlyList<int> pAgentsPerBox)
        {
            int lUsed = 0;
            bool lShared = false;
            foreach (int lCount in pAgentsPerBox)
            {
                if (lCount > 0) lUsed++;
                if (lCount > 1) lShared = true;
            }
            if (lUsed <= 1) return MatchMode.Coop;
            return lShared ? MatchMode.Teams : MatchMode.FreeForAll;
        }

        /// <summary>Draws the target index among the enabled ones, with the host's seed.</summary>
        public static int DrawTarget(IReadOnlyList<bool> pEnabled, int pSeed)
        {
            List<int> lPool = new List<int>();
            for (int i = 0; i < pEnabled.Count; i++) if (pEnabled[i]) lPool.Add(i);
            if (lPool.Count == 0) return -1;
            System.Random lRandom = new System.Random(pSeed);
            return lPool[lRandom.Next(lPool.Count)];
        }

        /// <summary>The team a spectator may watch: teammates first, anybody in FFA.</summary>
        public static bool CanSpectate(MatchMode pMode, int pViewerTeam, int pWatchedTeam)
        {
            if (pMode == MatchMode.FreeForAll) return true;
            return pViewerTeam == pWatchedTeam;
        }

        /// <summary>
        /// The starts in the order they are handed out, one per team: the safe ones in random order
        /// first, then the others, those with the most room first.
        /// </summary>
        public static List<int> OrderStarts(IReadOnlyList<UnityEngine.Vector3> pStarts, IReadOnlyList<UnityEngine.Vector3> pThreats, System.Random pRandom)
        {
            List<int> lSafe = new List<int>(), lRest = new List<int>();
            for (int i = 0; i < pStarts.Count; i++) (StartClearance(pStarts[i], pThreats) >= SAFE_START_DISTANCE ? lSafe : lRest).Add(i);
            for (int i = lSafe.Count - 1; i > 0; i--)
            {
                int j = pRandom.Next(i + 1);
                (lSafe[i], lSafe[j]) = (lSafe[j], lSafe[i]);
            }
            lRest.Sort((a, b) => StartClearance(pStarts[b], pThreats).CompareTo(StartClearance(pStarts[a], pThreats)));
            lSafe.AddRange(lRest);
            return lSafe;
        }

        /// <summary>Starts must be this far from any guard post or patrol point.</summary>
        public const float SAFE_START_DISTANCE = 50f;

        /// <summary>Distance from <paramref name="pStart"/> to the closest threat (NPC post, patrol point), on the ground plane.</summary>
        public static float StartClearance(UnityEngine.Vector3 pStart, IReadOnlyList<UnityEngine.Vector3> pThreats)
        {
            float lBest = float.MaxValue;
            foreach (UnityEngine.Vector3 lThreat in pThreats)
            {
                float lDx = lThreat.x - pStart.x, lDz = lThreat.z - pStart.z;
                lBest = System.Math.Min(lBest, (float)System.Math.Sqrt(lDx * lDx + lDz * lDz));
            }
            return lBest;
        }

        /// <summary>
        /// Draws a start point among the safe ones (no threat within <see cref="SAFE_START_DISTANCE"/>);
        /// when none is safe, the one with the most room. -1 without starts.
        /// </summary>
        public static int PickSafeStart(IReadOnlyList<UnityEngine.Vector3> pStarts, IReadOnlyList<UnityEngine.Vector3> pThreats, System.Random pRandom)
        {
            if (pStarts.Count == 0) return -1;
            List<int> lSafe = new List<int>();
            int lRoomiest = 0;
            float lMost = -1f;
            for (int i = 0; i < pStarts.Count; i++)
            {
                float lClearance = StartClearance(pStarts[i], pThreats);
                if (lClearance >= SAFE_START_DISTANCE) lSafe.Add(i);
                if (lClearance > lMost) { lMost = lClearance; lRoomiest = i; }
            }
            return lSafe.Count > 0 ? lSafe[pRandom.Next(lSafe.Count)] : lRoomiest;
        }
    }
}
