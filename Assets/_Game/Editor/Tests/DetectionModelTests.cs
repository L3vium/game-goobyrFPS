using Com.IsartDigital.GoobyrFPS.AI;
using NUnit.Framework;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>EditMode tests of the stealth rules: the gauge, the confrontation, containment, the phone call.</summary>
    public class DetectionModelTests
    {
        private static DetectionModel.Sight InFront(float pDistance) => new DetectionModel.Sight
        {
            distance = pDistance, range = 20f, angle = 0f, halfCone = 45f, chestVisible = true, headVisible = true
        };

        [Test]
        public void Exposure_FasterUpCloseAndZeroOutOfRange()
        {
            Assert.Greater(DetectionModel.Exposure(InFront(3f)), DetectionModel.Exposure(InFront(15f)));
            Assert.AreEqual(0f, DetectionModel.Exposure(InFront(25f)));
        }

        [Test]
        public void Exposure_EdgeOfVisionSlowerAndBehindUnseen()
        {
            DetectionModel.Sight lSide = InFront(10f);
            lSide.angle = 60f;
            DetectionModel.Sight lBehind = InFront(10f);
            lBehind.angle = 150f;
            Assert.Less(DetectionModel.Exposure(lSide), DetectionModel.Exposure(InFront(10f)) * 0.5f);
            Assert.Greater(DetectionModel.Exposure(lSide), 0f);
            Assert.AreEqual(0f, DetectionModel.Exposure(lBehind));
            lBehind.distance = 1.5f;
            Assert.Greater(DetectionModel.Exposure(lBehind), 0f, "felt at arm's length whatever it faces");
        }

        [Test]
        public void Exposure_CrouchedInGrassOrBehindAWallIsUnseen()
        {
            DetectionModel.Sight lGrass = InFront(8f);
            lGrass.crouched = true;
            lGrass.inGrass = true;
            Assert.AreEqual(0f, DetectionModel.Exposure(lGrass));
            DetectionModel.Sight lWall = InFront(8f);
            lWall.chestVisible = lWall.headVisible = false;
            Assert.AreEqual(0f, DetectionModel.Exposure(lWall));
        }

        [Test]
        public void Gauge_FillsInViewAndDrainsOutOfView()
        {
            float lGauge = 0f;
            for (int i = 0; i < 10; i++) lGauge = DetectionModel.Fill(lGauge, 1f, 2.2f, 0.2f, false);
            Assert.That(lGauge, Is.InRange(0.8f, 1f));
            for (int i = 0; i < 30; i++) lGauge = DetectionModel.Fill(lGauge, 0f, 2.2f, 0.2f, false);
            Assert.AreEqual(0f, lGauge, 1e-4f);
            Assert.AreEqual(DetectionModel.COMBAT_FLOOR, DetectionModel.Fill(0.9f, 0f, 2.2f, 10f, true), 1e-4f, "a fight is not forgotten");
        }

        [Test]
        public void Confrontation_ResolvesToCombatOrSearch()
        {
            Assert.AreEqual(DetectionModel.Confrontation.Hold, DetectionModel.ResolveConfront(1f, 0f, false));
            Assert.AreEqual(DetectionModel.Confrontation.Combat, DetectionModel.ResolveConfront(DetectionModel.CONFRONT_TIME, 0f, false));
            Assert.AreEqual(DetectionModel.Confrontation.Combat, DetectionModel.ResolveConfront(0.2f, 0f, true), "aiming at it starts the fight");
            Assert.AreEqual(DetectionModel.Confrontation.LostSight, DetectionModel.ResolveConfront(1.5f, DetectionModel.CONFRONT_LOST, false));
        }

        [Test]
        public void Containment_BystanderOutOfSightNeverJoins()
        {
            Assert.IsTrue(DetectionModel.Witnesses(10f, 20f, true));
            Assert.IsFalse(DetectionModel.Witnesses(10f, 20f, false), "a wall in between");
            Assert.IsFalse(DetectionModel.Witnesses(DetectionModel.WITNESS_RANGE + 1f, 0f, true), "too far");
            Assert.IsFalse(DetectionModel.Witnesses(5f, 170f, true), "looking the other way");
        }

        [Test]
        public void PhoneCall_OnlyTheCallersZoneAroundTheCaller()
        {
            Assert.IsTrue(DetectionModel.HearsCall(1, 1, 30f));
            Assert.IsFalse(DetectionModel.HearsCall(1, 1, DetectionModel.CALL_RADIUS + 5f));
            Assert.IsFalse(DetectionModel.HearsCall(2, 1, 10f));
        }

        [Test]
        public void Loudness_FallsWithDistanceAndWalls()
        {
            Assert.AreEqual(1f, DetectionModel.Loudness(0f, 30f, 0), 1e-4f);
            Assert.Greater(DetectionModel.Loudness(10f, 30f, 0), DetectionModel.Loudness(20f, 30f, 0));
            Assert.AreEqual(0f, DetectionModel.Loudness(30f, 30f, 0));
            // A pistol heard at 20 m in the open is not heard through one wall at that distance.
            Assert.Greater(DetectionModel.Loudness(20f, 30f, 0), 0f);
            Assert.AreEqual(0f, DetectionModel.Loudness(20f, 30f, 1));
            Assert.Greater(DetectionModel.Loudness(5f, 30f, 1), DetectionModel.Loudness(5f, 30f, 2));
        }

        [Test]
        public void Hearing_OnlyTheFewNearestRespond()
        {
            Assert.AreEqual(1, DetectionModel.Responders(NoiseKind.Distraction, 0.4f));
            Assert.LessOrEqual(DetectionModel.Responders(NoiseKind.Distraction, 0.9f), 2);
            Assert.LessOrEqual(DetectionModel.Responders(NoiseKind.Alarming, 1f), 3);
            Assert.AreEqual(DetectionModel.Heard.Glance, DetectionModel.React(NoiseKind.Alarming, 0.9f, false));
            Assert.AreEqual(DetectionModel.Heard.Alarmed, DetectionModel.React(NoiseKind.Alarming, 0.9f, true));
            Assert.AreEqual(DetectionModel.Heard.Investigate, DetectionModel.React(NoiseKind.Alarming, 0.1f, true));
            Assert.AreEqual(DetectionModel.Heard.Investigate, DetectionModel.React(NoiseKind.Distraction, 0.1f, true));
            Assert.AreEqual(DetectionModel.Heard.None, DetectionModel.React(NoiseKind.Alarming, 0f, true));
        }

        [Test]
        public void Hearing_GivesAGuessNotAPosition()
        {
            Assert.GreaterOrEqual(DetectionModel.GuessError(1f), 1.5f);
            Assert.Greater(DetectionModel.GuessError(30f), DetectionModel.GuessError(8f));
            Assert.LessOrEqual(DetectionModel.GuessError(500f), 10f);
        }

        [Test]
        public void Footsteps_SilentStillQuietCrouchedFarSprinting()
        {
            Assert.AreEqual(0f, DetectionModel.FootstepRadius(0f, false, false));
            Assert.LessOrEqual(DetectionModel.FootstepRadius(2.3f, true, false), DetectionModel.STEP_CROUCH);
            Assert.Greater(DetectionModel.FootstepRadius(4.4f, false, false), DetectionModel.FootstepRadius(2.3f, true, false));
            Assert.AreEqual(DetectionModel.STEP_SPRINT, DetectionModel.FootstepRadius(7.2f, false, true));
            Assert.AreEqual(DetectionModel.STEP_SLIDE, DetectionModel.FootstepRadius(10f, true, false));
            Assert.AreEqual(0f, DetectionModel.LandingRadius(0.8f, false));
            Assert.Greater(DetectionModel.LandingRadius(4f, false), DetectionModel.LandingRadius(4f, true));
        }

        [Test]
        public void Takedown_OnlyFromBehindAndUnnoticed()
        {
            Assert.IsTrue(DetectionModel.CanTakedown(20f, 0f, false));
            Assert.IsFalse(DetectionModel.CanTakedown(120f, 0f, false));
            Assert.IsFalse(DetectionModel.CanTakedown(20f, 0.8f, false));
            Assert.IsFalse(DetectionModel.CanTakedown(20f, 0f, true));
        }

        [Test]
        public void Trespassing_PublicIsSafeUnlessSuspicious()
        {
            Assert.AreEqual(0f, DetectionModel.TierFactor(SecurityTier.Public, false));
            Assert.Greater(DetectionModel.TierFactor(SecurityTier.Public, true), 0f);
            Assert.Less(DetectionModel.TierFactor(SecurityTier.Restricted, false), DetectionModel.TierFactor(SecurityTier.Hostile, false));
            Assert.AreEqual(1f, DetectionModel.TierFactor(SecurityTier.Hostile, false));
        }

        [Test]
        public void Passes_MakeTheirZonePublicExceptForEnforcers()
        {
            Assert.AreEqual(SecurityTier.Public, SecurityZone.ForPlayer(SecurityTier.Hostile, AccessLevel.Guest, AccessLevel.Guest, false));
            Assert.AreEqual(SecurityTier.Public, SecurityZone.ForPlayer(SecurityTier.Restricted, AccessLevel.Staff, AccessLevel.Security, false));
            Assert.AreEqual(SecurityTier.Hostile, SecurityZone.ForPlayer(SecurityTier.Hostile, AccessLevel.Security, AccessLevel.Staff, false));
            Assert.AreEqual(SecurityTier.Hostile, SecurityZone.ForPlayer(SecurityTier.Hostile, AccessLevel.Guest, AccessLevel.Security, true));
            Assert.AreEqual(SecurityTier.Restricted, SecurityZone.ForPlayer(SecurityTier.Restricted, AccessLevel.None, AccessLevel.Security, false));
            // Personal rooms are never legal, whatever the pass.
            Assert.AreEqual(SecurityTier.Hostile, SecurityZone.ForPlayer(SecurityTier.Hostile, AccessLevel.Private, AccessLevel.Security, false));
        }

        [Test]
        public void Escort_LastsLongerThanAConfrontation()
        {
            Assert.AreEqual(DetectionModel.Confrontation.Hold, DetectionModel.ResolveConfront(3f, 0f, false, DetectionModel.ESCORT_TIME));
            Assert.AreEqual(DetectionModel.Confrontation.Combat, DetectionModel.ResolveConfront(3f, 0f, false));
        }

        [Test]
        public void Gauge_FillsFasterOnEdge()
        {
            float lCalm = DetectionModel.Fill(0f, 1f, 2f, 0.1f, false);
            float lEdge = DetectionModel.Fill(0f, 1f, 2f, 0.1f, false, true);
            Assert.Greater(lEdge, lCalm);
        }
    }
}
