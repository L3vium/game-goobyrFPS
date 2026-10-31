using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using NUnit.Framework;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>EditMode tests of the pure match rules: teams per mode, friendly fire, outcomes, draws.</summary>
    public class MatchRulesTests
    {
        [Test]
        public void FreeForAll_EveryoneOnTheirOwnTeam()
        {
            HashSet<int> lTeams = new HashSet<int>();
            for (int lSeat = 0; lSeat < 8; lSeat++) lTeams.Add(MatchRulesLogic.ResolveTeam(MatchMode.FreeForAll, lSeat, 0, 2));
            Assert.AreEqual(8, lTeams.Count);
        }

        [Test]
        public void Coop_EveryoneOnTeamZero()
        {
            for (int lSeat = 0; lSeat < 8; lSeat++) Assert.AreEqual(0, MatchRulesLogic.ResolveTeam(MatchMode.Coop, lSeat, 3, 4));
        }

        [Test]
        public void Teams_ChoiceKeptAndClampedToTeamCount()
        {
            Assert.AreEqual(2, MatchRulesLogic.ResolveTeam(MatchMode.Teams, 0, 2, 3));
            Assert.AreEqual(0, MatchRulesLogic.ResolveTeam(MatchMode.Teams, 0, 3, 3));
            Assert.AreEqual(1, MatchRulesLogic.ResolveTeam(MatchMode.Teams, 5, -1, 2));
            Assert.AreEqual(3, MatchRulesLogic.ResolveTeam(MatchMode.Teams, 0, 3, 9), "team count is clamped to 4");
        }

        [Test]
        public void AutoBalance_SpreadsPlayersEvenly()
        {
            List<int> lTeams = MatchRulesLogic.AutoBalance(7, 3);
            int[] lSizes = new int[3];
            foreach (int lTeam in lTeams) lSizes[lTeam]++;
            CollectionAssert.AreEqual(new[] { 3, 2, 2 }, lSizes);
        }

        [Test]
        public void FriendlyFire_OnlyBetweenDifferentTeams()
        {
            Assert.IsFalse(MatchRulesLogic.CanDamage(1, 1));
            Assert.IsTrue(MatchRulesLogic.CanDamage(1, 2));
            Assert.IsTrue(MatchRulesLogic.CanDamage(MatchRulesLogic.NO_TEAM, 1), "NPCs and the world hurt everyone");
        }

        [Test]
        public void Outcome_ExtractionWinsEvenAtTimeUp()
        {
            List<RulesPlayer> lPlayers = new List<RulesPlayer> { new RulesPlayer { clientId = 0, team = 0, alive = true } };
            Assert.AreEqual(MatchOutcome.Extracted, MatchRulesLogic.Evaluate(lPlayers, 0, 0f));
        }

        [Test]
        public void Outcome_AllEliminatedAndTimeUp()
        {
            List<RulesPlayer> lDead = new List<RulesPlayer> { new RulesPlayer { alive = false }, new RulesPlayer { alive = false } };
            Assert.AreEqual(MatchOutcome.AllEliminated, MatchRulesLogic.Evaluate(lDead, MatchRulesLogic.NO_TEAM, 100f));
            List<RulesPlayer> lAlive = new List<RulesPlayer> { new RulesPlayer { alive = true } };
            Assert.AreEqual(MatchOutcome.TimeUp, MatchRulesLogic.Evaluate(lAlive, MatchRulesLogic.NO_TEAM, 0f));
            Assert.AreEqual(MatchOutcome.None, MatchRulesLogic.Evaluate(lAlive, MatchRulesLogic.NO_TEAM, 10f));
        }

        [Test]
        public void TargetDraw_OnlyEnabledAndDeterministic()
        {
            List<bool> lEnabled = new List<bool>();
            for (int i = 0; i < 15; i++) lEnabled.Add(i == 4 || i == 9);
            for (int lSeed = 0; lSeed < 50; lSeed++)
            {
                int lDrawn = MatchRulesLogic.DrawTarget(lEnabled, lSeed);
                Assert.IsTrue(lDrawn == 4 || lDrawn == 9);
                Assert.AreEqual(lDrawn, MatchRulesLogic.DrawTarget(lEnabled, lSeed));
            }
            Assert.AreEqual(-1, MatchRulesLogic.DrawTarget(new List<bool> { false, false }, 1));
        }

        [Test]
        public void Spectate_TeammatesOnlyExceptFreeForAll()
        {
            Assert.IsTrue(MatchRulesLogic.CanSpectate(MatchMode.FreeForAll, 0, 3));
            Assert.IsTrue(MatchRulesLogic.CanSpectate(MatchMode.Teams, 1, 1));
            Assert.IsFalse(MatchRulesLogic.CanSpectate(MatchMode.Teams, 1, 2));
        }

        [Test]
        public void RoomCode_SixLettersFormatted()
        {
            string lCode = RoomCode.New();
            Assert.AreEqual(7, lCode.Length);
            Assert.AreEqual('-', lCode[3]);
            Assert.AreEqual(RoomCode.Clean(lCode), RoomCode.Clean("  " + lCode.ToLowerInvariant() + " "));
        }

        [Test]
        public void SafeStart_NeverNextToAGuard()
        {
            List<UnityEngine.Vector3> lStarts = new List<UnityEngine.Vector3> { new UnityEngine.Vector3(0, 0, 0), new UnityEngine.Vector3(200, 0, 0), new UnityEngine.Vector3(0, 0, 200) };
            List<UnityEngine.Vector3> lThreats = new List<UnityEngine.Vector3> { new UnityEngine.Vector3(10, 5, 0), new UnityEngine.Vector3(0, 0, 230) };
            for (int lSeed = 0; lSeed < 20; lSeed++) Assert.AreEqual(1, MatchRulesLogic.PickSafeStart(lStarts, lThreats, new System.Random(lSeed)));
            // Nothing safe: the roomiest one (30 m clear against 20 m and 10 m).
            lThreats.Add(new UnityEngine.Vector3(220, 0, 0));
            Assert.AreEqual(2, MatchRulesLogic.PickSafeStart(lStarts, lThreats, new System.Random(1)));
        }
    }
}
