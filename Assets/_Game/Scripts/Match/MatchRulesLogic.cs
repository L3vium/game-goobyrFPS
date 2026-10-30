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
        TimeUp
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
        public const int NO_TEAM = -1;
        public const int MAX_TEAMS = 4;

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
    }
}
