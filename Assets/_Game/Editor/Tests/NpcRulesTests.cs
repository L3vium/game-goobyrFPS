using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using NUnit.Framework;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>EditMode tests of the NPC brain's pure rules (<see cref="NpcRules"/>): the attack chain, civilians, combat, patrols.</summary>
    public class NpcRulesTests
    {
        [Test]
        public void Glimpse_NeverCalmsASearcherOrAResponder()
        {
            Assert.IsFalse(NpcRules.CanBecomeSuspicious(NpcState.Search, false), "a searcher who glimpses you hunts you");
            Assert.IsFalse(NpcRules.CanBecomeSuspicious(NpcState.Investigate, true), "a responder running to a shot");
            Assert.IsTrue(NpcRules.CanBecomeSuspicious(NpcState.Investigate, false), "a calm look at a noise can still turn into a stare");
            Assert.IsTrue(NpcRules.CanBecomeSuspicious(NpcState.Patrol, false));
            Assert.IsTrue(NpcRules.CanBecomeSuspicious(NpcState.Idle, false));
            foreach (NpcState lState in new[] { NpcState.Combat, NpcState.Calling, NpcState.Engaged, NpcState.Flee, NpcState.Sick, NpcState.KnockedOut, NpcState.Dead })
                Assert.IsFalse(NpcRules.CanBecomeSuspicious(lState, false), lState.ToString());
            Assert.IsTrue(NpcRules.OnEdge(NpcState.Search, false));
            Assert.IsTrue(NpcRules.OnEdge(NpcState.Investigate, true));
            Assert.IsFalse(NpcRules.OnEdge(NpcState.Investigate, false));
            Assert.IsFalse(NpcRules.OnEdge(NpcState.Patrol, false));
        }

        [Test]
        public void HostileMemory_RemembersThePlayerForAboutTwoMinutes()
        {
            HostileMemory lMemory = new HostileMemory();
            Assert.IsFalse(lMemory.IsHostile(1, 0f));
            lMemory.Mark(1, 10f);
            Assert.IsTrue(lMemory.IsHostile(1, 10f + HostileMemory.DURATION - 1f));
            Assert.IsFalse(lMemory.IsHostile(1, 10f + HostileMemory.DURATION + 1f), "forgotten after a while");
            Assert.IsFalse(lMemory.IsHostile(2, 20f), "only the player who did it");
            lMemory.Mark(1, 100f);
            Assert.IsTrue(lMemory.IsHostile(1, 100f + HostileMemory.DURATION - 1f), "a new act starts the clock again");
            lMemory.Mark(HostileMemory.NO_PLAYER, 0f);
            Assert.IsFalse(lMemory.IsHostile(HostileMemory.NO_PLAYER, 1f));
            Assert.That(HostileMemory.DURATION, Is.InRange(90f, 150f));
        }

        [Test]
        public void Spotted_AKnownHostileIsFoughtOnSightEvenInPublic()
        {
            Assert.AreEqual(NpcRules.SpotReaction.Fight, NpcRules.OnSpotted(false, false, true, SecurityTier.Public), "seen = combat");
            Assert.AreEqual(NpcRules.SpotReaction.Confront, NpcRules.OnSpotted(false, false, false, SecurityTier.Public));
            Assert.AreEqual(NpcRules.SpotReaction.Confront, NpcRules.OnSpotted(false, true, false, SecurityTier.Public), "a searcher does not shoot a passer-by");
            Assert.AreEqual(NpcRules.SpotReaction.Fight, NpcRules.OnSpotted(false, true, false, SecurityTier.Restricted), "a searcher fights a trespasser");
            Assert.AreEqual(NpcRules.SpotReaction.Flee, NpcRules.OnSpotted(true, false, true, SecurityTier.Hostile), "the unarmed run");
        }

        [Test]
        public void GunInHand_SuspiciousAnywhereACrimeOutsidePublicPlaces()
        {
            foreach (ItemKind lGun in new[] { ItemKind.Pistol, ItemKind.SilencedPistol, ItemKind.Smg, ItemKind.Shotgun, ItemKind.Sniper, ItemKind.RocketLauncher, ItemKind.Flamethrower })
                Assert.IsTrue(NpcRules.IsVisibleWeapon(lGun), lGun.ToString());
            foreach (ItemKind lOther in new[] { ItemKind.Fists, ItemKind.Coin, ItemKind.Keycard, ItemKind.Poison, ItemKind.Brick })
                Assert.IsFalse(NpcRules.IsVisibleWeapon(lOther), lOther.ToString());

            Assert.Greater(DetectionModel.TierFactor(SecurityTier.Public, NpcRules.ActsSuspicious(false, true)), 0f, "a gun out on the plaza is noticed");
            Assert.AreEqual(0f, DetectionModel.TierFactor(SecurityTier.Public, NpcRules.ActsSuspicious(false, false)), "a calm unarmed passer-by is not");
            Assert.IsTrue(NpcRules.IsCrime(SecurityTier.Restricted, true));
            Assert.IsTrue(NpcRules.IsCrime(SecurityTier.Hostile, true));
            Assert.IsFalse(NpcRules.IsCrime(SecurityTier.Public, true), "in public it is suspicious, not a crime: the confrontation is the chance to put it away");
            Assert.IsFalse(NpcRules.IsCrime(SecurityTier.Hostile, false));
            Assert.AreEqual(NpcRules.SpotReaction.Fight, NpcRules.OnSpotted(false, false, false, SecurityTier.Restricted, true), "an armed trespasser is fought, not escorted");
        }
    }
}
