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
    }
}
