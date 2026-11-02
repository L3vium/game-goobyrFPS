using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using NUnit.Framework;
using UnityEngine;

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
        public void Aiming_TheAgentNeverSteersTheBody()
        {
            foreach (NpcState lState in new[] { NpcState.Engaged, NpcState.Combat, NpcState.Calling, NpcState.Suspicious })
                Assert.IsTrue(NpcRules.AimsManually(lState), lState + " turns to its target");
            foreach (NpcState lState in new[] { NpcState.Idle, NpcState.Patrol, NpcState.Routine, NpcState.Search, NpcState.Investigate, NpcState.Flee })
                Assert.IsFalse(NpcRules.AimsManually(lState), lState + " faces where it walks");
        }

        [Test]
        public void Gunfire_BringsEveryArmedGuardInEarshotButCapsTheCrowd()
        {
            Assert.IsTrue(NpcRules.AlwaysResponds(NoiseKind.Alarming, true, false));
            Assert.IsFalse(NpcRules.AlwaysResponds(NoiseKind.Alarming, false, false), "the unarmed keep the cap");
            Assert.IsFalse(NpcRules.AlwaysResponds(NoiseKind.Alarming, true, true), "a far siren stays capped");
            Assert.IsFalse(NpcRules.AlwaysResponds(NoiseKind.Suspicious, true, false), "a silenced shot stays quiet");
            Assert.IsFalse(NpcRules.AlwaysResponds(NoiseKind.Distraction, true, false));
            Assert.LessOrEqual(DetectionModel.Responders(NoiseKind.Alarming, 1f), 3);
            Assert.IsTrue(NpcRules.ShotNoiseDue(-100f, 0f), "an NPC's first shot is heard");
            Assert.IsFalse(NpcRules.ShotNoiseDue(10f, 10f + NpcRules.SHOT_NOISE_INTERVAL * 0.5f), "rate-limited");
            Assert.IsTrue(NpcRules.ShotNoiseDue(10f, 10f + NpcRules.SHOT_NOISE_INTERVAL));
        }

        [Test]
        public void Walls_OnlyStructuralOnesMuffleAndGlassIsSeenThrough()
        {
            Assert.IsTrue(NpcRules.IsStructural(false, new Vector3(0.3f, 3f, 6f), false), "a wall");
            Assert.IsTrue(NpcRules.IsStructural(false, new Vector3(1f, 2.1f, 0.06f), false), "a door");
            Assert.IsTrue(NpcRules.IsStructural(true, Vector3.one, false), "the ground between them (a hill)");
            Assert.IsFalse(NpcRules.IsStructural(false, new Vector3(1f, 1f, 1f), false), "a crate");
            Assert.IsFalse(NpcRules.IsStructural(false, new Vector3(0.5f, 8f, 0.5f), false), "a tree trunk");
            Assert.IsFalse(NpcRules.IsStructural(false, new Vector3(2f, 1.5f, 4.5f), false), "a car");
            Assert.IsFalse(NpcRules.IsStructural(false, new Vector3(3f, 1.2f, 0.1f), false), "a fence");
            Assert.IsFalse(NpcRules.IsStructural(false, new Vector3(20f, 9f, 14f), true), "a building's window panes");

            Assert.IsTrue(NpcRules.IsSeeThrough(NpcRules.GLASS_OBJECT, null, 0, -1), "the generator's glass batch");
            Assert.IsTrue(NpcRules.IsSeeThrough("Pane", NpcRules.GLASS_MATERIAL + " (Instance)", 0, -1));
            Assert.IsTrue(NpcRules.IsSeeThrough("Pane", null, 18, 18), "a Glass layer, if the project gets one");
            Assert.IsFalse(NpcRules.IsSeeThrough("plaster", "M_Plaster", 0, -1));

            // A pistol heard 45 m away in the open still carries through one wall, not through three.
            Assert.Greater(DetectionModel.Loudness(12f, 45f, 1), 0f);
            Assert.AreEqual(0f, DetectionModel.Loudness(12f, 45f, 3));
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
