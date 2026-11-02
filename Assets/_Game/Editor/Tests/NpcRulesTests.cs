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
        public void PhoneCall_NeverTheLastFighterAndNotTwiceInARow()
        {
            Assert.IsTrue(NpcRules.ShouldCall(false, 0, 2, 3f, 2, 100f, 0f));
            Assert.IsFalse(NpcRules.ShouldCall(false, 0, 2, 3f, 1, 100f, 0f), "a lone fighter keeps shooting");
            Assert.IsFalse(NpcRules.ShouldCall(true, 0, 2, 3f, 3, 100f, 0f), "one call at a time");
            Assert.IsFalse(NpcRules.ShouldCall(false, 0, 2, 3f, 3, 100f, 100f + NpcRules.CALL_COOLDOWN), "a cooldown after a call");
            Assert.IsFalse(NpcRules.ShouldCall(false, 2, 2, 3f, 3, 100f, 0f), "the zone's calls are used up");
            Assert.IsFalse(NpcRules.ShouldCall(false, 0, 2, 0.5f, 3, 100f, 0f), "not in the first instant of a fight");
            Assert.IsFalse(NpcRules.KeepsCalling(0), "the last one standing drops the phone");
            Assert.IsTrue(NpcRules.KeepsCalling(1));
            Assert.Greater(NpcRules.CALL_COOLDOWN, NpcRules.CALL_RETRY);
        }

        [Test]
        public void Investigation_LooksAroundOnItsOwnClock()
        {
            Assert.IsFalse(NpcRules.LookedAround(-1f, 500f, NpcRules.LookAroundTime(false)), "not there yet");
            Assert.IsFalse(NpcRules.LookedAround(100f, 102f, NpcRules.LookAroundTime(false)), "a patrol that just arrived stays and looks");
            Assert.IsTrue(NpcRules.LookedAround(100f, 100f + NpcRules.LookAroundTime(false), NpcRules.LookAroundTime(false)));
            Assert.Greater(NpcRules.LookAroundTime(true), NpcRules.LookAroundTime(false), "longer after a shot");
            Assert.GreaterOrEqual(NpcRules.LookAroundTime(false), 3f);
        }

        [Test]
        public void Flee_RunsAwayFromTheThreatNotFromTheMapOrigin()
        {
            // A crowd member at (100, 0, 100), a shot at (110, 0, 100): it runs toward -x, whatever the origin.
            Vector3 lWish = NpcRules.FleeWish(new Vector3(100f, 0f, 100f), new Vector3(110f, 0f, 100f), Vector3.forward, 22f);
            Assert.Less(lWish.x, 100f);
            Assert.AreEqual(100f, lWish.z, 0.01f);
            Assert.AreEqual(22f, Vector3.Distance(lWish, new Vector3(100f, 0f, 100f)), 0.01f);
            // The threat right on it: it bolts the way it faces.
            Vector3 lBolt = NpcRules.FleeWish(new Vector3(5f, 2f, 5f), new Vector3(5f, 0f, 5f), Vector3.right, 10f);
            Assert.AreEqual(15f, lBolt.x, 0.01f);
            Assert.AreEqual(2f, lBolt.y, 0.01f, "stays on its floor");
        }

        [Test]
        public void Health_TheTargetTakesAFifthMore()
        {
            Assert.AreEqual(120f, NpcRules.MaxHealth(NpcRole.Target, 100f), 1e-3f);
            Assert.AreEqual(100f, NpcRules.MaxHealth(NpcRole.Guard, 100f), 1e-3f);
            Assert.AreEqual(100f, NpcRules.MaxHealth(NpcRole.Bodyguard, 100f), 1e-3f);
        }

        [Test]
        public void Ghost_SentWhenItChangesNotOverAndOver()
        {
            Assert.IsTrue(NpcRules.GhostSendDue(true, false, 0.1f), "the player slipped out of sight: show the shadow");
            Assert.IsTrue(NpcRules.GhostSendDue(false, true, 0.1f), "seen again: hide it");
            Assert.IsFalse(NpcRules.GhostSendDue(false, false, 5f), "in plain sight: nothing to send");
            Assert.IsFalse(NpcRules.GhostSendDue(true, true, 0.5f));
            Assert.IsTrue(NpcRules.GhostSendDue(true, true, 1.2f), "the shadow follows the squad's belief");
        }

        [Test]
        public void Lookout_FlatConeSeesTheGroundBelowItsTower()
        {
            // A lookout 12 m up, a player 8 m out in front: 56 degrees down - outside a 45-degree half cone in 3-D.
            Vector3 lTo = new Vector3(0f, -12f, 8f);
            Assert.Greater(NpcRules.ConeAngle(Vector3.forward, lTo, false), 45f);
            Assert.Less(NpcRules.ConeAngle(Vector3.forward, lTo, true), 1f, "the lookout scans the ground");
            Assert.AreEqual(90f, NpcRules.ConeAngle(Vector3.forward, new Vector3(8f, -12f, 0f), true), 0.5f, "the bearing still counts");
            Assert.AreEqual(180f, NpcRules.ConeAngle(Vector3.forward, new Vector3(0f, -12f, 1f), true), "right under its feet it sees nothing");
            Assert.AreEqual(180f, NpcRules.ConeAngle(Vector3.forward, new Vector3(0f, 20f, 5f), true), "nor straight up");
        }

        /// <summary>Seconds for a guard's gauge to fill on a player standing in front of it at this distance, in this tier.</summary>
        private static float TimeToSpot(float pDistance, SecurityTier pTier, bool pSuspicious)
        {
            DetectionModel.Sight lSight = new DetectionModel.Sight { distance = pDistance, range = 20f, angle = 0f, halfCone = 45f, chestVisible = true, headVisible = true };
            float lExposure = DetectionModel.Exposure(lSight) * DetectionModel.TierFactor(pTier, pSuspicious);
            float lGauge = 0f, lTime = 0f;
            const float TICK = 0.18f;
            while (lGauge < 1f && lTime < 30f)
            {
                lGauge = DetectionModel.Fill(lGauge, lExposure, 2.2f, TICK, false);
                lTime += TICK;
            }
            return lTime;
        }

        [Test]
        public void Timeline_RestrictedShootsWithinFourSecondsHostileOnSightPublicNever()
        {
            // Restricted: the gauge fills, a short warning, then the first shot - 3 to 4 s at 8-10 m.
            foreach (float lDistance in new[] { 8f, 10f })
            {
                float lFirstShot = TimeToSpot(lDistance, SecurityTier.Restricted, false) + DetectionModel.ESCORT_TIME + NpcRules.REACTION_CONFRONTED;
                Assert.That(lFirstShot, Is.InRange(2.5f, 4.2f), "restricted at " + lDistance + " m");
            }
            Assert.AreEqual(NpcRules.SpotReaction.Confront, NpcRules.OnSpotted(false, false, false, SecurityTier.Restricted));
            // Hostile: no warning, a short reaction.
            Assert.AreEqual(NpcRules.SpotReaction.Fight, NpcRules.OnSpotted(false, false, false, SecurityTier.Hostile));
            Assert.Less(TimeToSpot(10f, SecurityTier.Hostile, false) + NpcRules.REACTION_ON_SIGHT, 2.5f);
            // An armed trespasser is in a fight within 3 s.
            Assert.Less(TimeToSpot(10f, SecurityTier.Restricted, true) + NpcRules.REACTION_ON_SIGHT, 3f);
            // Public, calm and unarmed: never noticed at all.
            Assert.GreaterOrEqual(TimeToSpot(3f, SecurityTier.Public, false), 30f);
        }

        [Test]
        public void Patrol_PairsWalkSideBySideAndErrandsSetTheStop()
        {
            Assert.AreEqual(Vector3.zero, NpcRules.FormationSlot(0), "the leader");
            Vector3 lPartner = NpcRules.FormationSlot(1);
            Assert.AreEqual(0f, lPartner.z, 1e-4f, "the second walks at the leader's shoulder");
            Assert.Greater(Mathf.Abs(lPartner.x), 0.5f);
            Assert.Less(NpcRules.FormationSlot(2).z, -1f, "the third behind");
            Assert.AreNotEqual(NpcRules.FormationSlot(2), NpcRules.FormationSlot(3));

            Assert.AreEqual(0f, NpcRules.StopSeconds(true, 0f, 3f, 0.5f), "walked straight through");
            Assert.That(NpcRules.StopSeconds(true, 10f, 3f, 0f), Is.InRange(8f, 12f), "the errand's own time");
            Assert.That(NpcRules.StopSeconds(true, 10f, 3f, 1f), Is.InRange(8f, 12f));
            Assert.That(NpcRules.StopSeconds(false, 0f, 3f, 0.5f), Is.InRange(1.5f, 4.5f), "a plain stop: the route's pause");
        }

        [Test]
        public void Crowd_RunsForAnExitAwayFromTheThreatAndCowersWhenCornered()
        {
            Vector3 lSelf = Vector3.zero, lThreat = new Vector3(10f, 0f, 0f);
            Assert.AreEqual(float.MaxValue, NpcRules.ExitScore(lSelf, lThreat, new Vector3(30f, 0f, 0f)), "never past the gunman");
            float lBehind = NpcRules.ExitScore(lSelf, lThreat, new Vector3(-30f, 0f, 0f));
            float lSide = NpcRules.ExitScore(lSelf, lThreat, new Vector3(5f, 0f, 25f));
            Assert.AreEqual(30f, lBehind, 0.01f, "straight away from it: just the distance");
            Assert.Less(lBehind, NpcRules.ExitScore(lSelf, lThreat, new Vector3(12f, 0f, 6f)), "an exit right by the threat is worse than a farther one away from it");
            Assert.Less(lSide, float.MaxValue);

            Assert.IsTrue(NpcRules.ShouldCower(3f, true), "too close to run past");
            Assert.IsTrue(NpcRules.ShouldCower(10f, false), "nowhere to go");
            Assert.IsFalse(NpcRules.ShouldCower(10f, true));
            Assert.IsFalse(NpcRules.ShouldCower(30f, false));

            Assert.IsTrue(NpcRules.Reports(true, 20f), "a witness with a guard close by tells it");
            Assert.IsFalse(NpcRules.Reports(true, NpcRules.REPORT_RANGE + 10f), "no guard near: it runs");
            Assert.IsFalse(NpcRules.Reports(false, 5f), "a shot only heard is not something to report");
            Assert.IsTrue(NpcRules.ReporterNeeded(0));
            Assert.IsFalse(NpcRules.ReporterNeeded(NpcRules.MAX_REPORTERS), "one or two go and tell, the rest run");
        }

        [Test]
        public void Panic_SpreadsOncePerIncident()
        {
            IncidentLog lLog = new IncidentLog();
            int lFirst = lLog.At(new Vector3(0f, 0f, 0f), 10f);
            Assert.AreEqual(lFirst, lLog.At(new Vector3(6f, 0f, 3f), 12f), "the next shot of the same fight");
            Assert.AreNotEqual(lFirst, lLog.At(new Vector3(200f, 0f, 0f), 12f), "another place");
            Assert.AreNotEqual(lFirst, lLog.At(new Vector3(0f, 0f, 0f), 12f + IncidentLog.WINDOW + 1f), "much later");
            Assert.LessOrEqual(NpcRules.PANIC_RADIUS, 12f);
        }

        [Test]
        public void Barks_RateLimitedWithAVoicePerNpc()
        {
            Assert.IsTrue(NpcRules.BarkAllowed(0));
            Assert.IsFalse(NpcRules.BarkAllowed(NpcRules.MAX_BARKS_PER_SECOND), "a stampede is a few screams, not fifty");
            Assert.AreEqual(NpcRules.VoiceOf(42, true), NpcRules.VoiceOf(42, true), "the same voice all match");
            for (ulong lId = 0; lId < 50; lId++)
            {
                string lGuard = NpcRules.VoiceOf(lId, false);
                Assert.IsTrue(lGuard == "low" || lGuard == "mid", "guards speak low or mid");
            }
            Assert.Greater(NpcRules.BarkGap(Barks.Chatter), NpcRules.BarkGap(Barks.Hey));
        }

        [Test]
        public void Combat_AccuracyRampsUpCoverCyclesAndSearchFansOut()
        {
            float lSettled = NpcRules.HitChance(0.32f, 10f, false, false, true, 10f, 5f, false);
            Assert.Less(NpcRules.HitChance(0.32f, 10f, false, false, true, 0.2f, 5f, false), lSettled * 0.4f, "the first shots of a fight are wild");
            Assert.Less(NpcRules.HitChance(0.32f, 10f, false, false, true, 10f, 0.1f, false), lSettled * 0.6f, "a target just found again is hard to hit");
            Assert.Less(NpcRules.HitChance(0.32f, 30f, false, false, true, 10f, 5f, false), lSettled, "range");
            Assert.Less(NpcRules.HitChance(0.32f, 10f, true, false, true, 10f, 5f, false), lSettled, "crouched");
            Assert.Less(NpcRules.HitChance(0.32f, 10f, false, false, false, 10f, 5f, false), lSettled * 0.3f, "firing blind");
            Assert.LessOrEqual(NpcRules.HitChance(1f, 1f, false, false, true, 10f, 5f, true), 1f);

            Assert.IsFalse(NpcRules.Peeking(0.2f), "ducked first");
            Assert.IsTrue(NpcRules.Peeking(NpcRules.DUCK + 0.1f), "then up to shoot");
            Assert.IsFalse(NpcRules.Peeking(NpcRules.DUCK + NpcRules.PEEK + 0.1f), "and down again");

            float lPrevious = 0f;
            for (int i = 0; i <= 8; i++)
            {
                float lRadius = NpcRules.SearchOffset(i, 30f).magnitude;
                Assert.That(lRadius, Is.InRange(2.9f, 14.1f));
                Assert.GreaterOrEqual(lRadius, lPrevious - 1e-3f, "the spiral widens");
                lPrevious = lRadius;
            }
            Assert.Greater(Vector3.Distance(NpcRules.SearchOffset(0, 0f), NpcRules.SearchOffset(0, 180f)), 5f, "two searchers start on opposite sides");
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
