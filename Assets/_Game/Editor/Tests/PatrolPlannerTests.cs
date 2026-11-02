using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.EditorTools;
using NUnit.Framework;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>EditMode tests of the patrol API the map builders use: generated loops, errands, the audit.</summary>
    public class PatrolPlannerTests
    {
        private static PatrolAudit.Report GoodReport() => new PatrolAudit.Report
        {
            guards = 20, moving = 7, routes = 4, stops = 6, townRoutes = 1, exits = 3,
            keyBuildings = new List<string> { "Hacienda" }, keyWithoutLoop = new List<string>(), crowdZonesWithoutExit = new List<string>()
        };

        [Test]
        public void Audit_ThirtyFivePercentMovingALoopPerKeyBuildingATownPatrolAndExits()
        {
            PatrolAudit.Report lReport = GoodReport();
            Assert.IsEmpty(PatrolAudit.Fails(lReport), "7 of 20 guards move: 35 %");
            lReport.moving = 6;
            Assert.IsNotEmpty(PatrolAudit.Fails(lReport), "30 % is too static");
            lReport = GoodReport();
            lReport.keyWithoutLoop.Add("Hacienda");
            Assert.IsNotEmpty(PatrolAudit.Fails(lReport), "a key building without an interior loop");
            lReport = GoodReport();
            lReport.townRoutes = 0;
            Assert.IsNotEmpty(PatrolAudit.Fails(lReport), "nobody walks the town");
            lReport = GoodReport();
            lReport.crowdZonesWithoutExit.Add("zone 0");
            Assert.IsNotEmpty(PatrolAudit.Fails(lReport), "a crowd with nowhere to run");
        }

        [Test]
        public void Errands_FitTheRoom()
        {
            Assert.AreEqual(PatrolAction.Chat, PatrolPlanner.ErrandFor(RoomType.Kitchen));
            Assert.AreEqual(PatrolAction.Smoke, PatrolPlanner.ErrandFor(RoomType.GuardRoom));
            Assert.AreEqual(PatrolAction.Watch, PatrolPlanner.ErrandFor(RoomType.Vault));
            Assert.AreEqual(PatrolAction.Pause, PatrolPlanner.ErrandFor(RoomType.Corridor), "corridors are walked through");
            Assert.AreEqual(PatrolAction.LookAround, PatrolPlanner.ErrandFor(RoomType.Office));
        }

        [Test]
        public void TownLoop_ChatsFirstThenLooksAroundEveryOtherCorner()
        {
            PatrolPoint[] lLoop = PatrolPlanner.TownLoop(Vector3.zero, Vector3.right * 20f, new Vector3(20f, 0f, 20f), Vector3.forward * 20f);
            Assert.AreEqual(4, lLoop.Length);
            Assert.IsTrue(lLoop[0].errand);
            Assert.AreEqual(PatrolAction.Chat, lLoop[0].action);
            Assert.IsFalse(lLoop[1].errand);
            Assert.AreEqual(PatrolAction.LookAround, lLoop[2].action);
        }

        [Test]
        public void InteriorLoop_VisitsTheRoomsByTheirDoorsAndTriesTheLockedOne()
        {
            GameObject lRoot = new GameObject("TestBuilding");
            try
            {
                ArchSpec lSpec = new ArchSpec();
                lSpec.rooms.Add(new ArchRoom { name = "Hall", floor = 0, rect = new Rect(0f, 0f, 6f, 4f), type = RoomType.Hall });
                lSpec.rooms.Add(new ArchRoom { name = "Office", floor = 0, rect = new Rect(6f, 0f, 4f, 4f), type = RoomType.Office });
                lSpec.rooms.Add(new ArchRoom { name = "Kitchen", floor = 0, rect = new Rect(0f, 4f, 6f, 4f), type = RoomType.Kitchen });
                lSpec.rooms.Add(new ArchRoom { name = "Vault", floor = 0, rect = new Rect(6f, 4f, 4f, 4f), type = RoomType.Vault });
                lSpec.doors.Add(new ArchDoor { floor = 0, at = new Vector2(6f, 2f) });                              // Hall - Office
                lSpec.doors.Add(new ArchDoor { floor = 0, at = new Vector2(3f, 4f) });                              // Hall - Kitchen
                lSpec.doors.Add(new ArchDoor { floor = 0, at = new Vector2(8f, 4f), kind = DoorKind.Secure });      // Office - Vault, locked
                ArchResult lBuilding = new ArchResult { spec = lSpec, root = lRoot.transform };

                PatrolPoint[] lLoop = PatrolPlanner.InteriorLoop(lBuilding, 0, 6);
                Assert.IsNotNull(lLoop);
                bool lOffice = false, lKitchen = false, lVault = false, lCheck = false;
                foreach (PatrolPoint lPoint in lLoop)
                {
                    Vector2 p = new Vector2(lPoint.position.x, lPoint.position.z);
                    if (new Rect(6f, 0f, 4f, 4f).Contains(p)) lOffice = true;
                    if (new Rect(0f, 4f, 6f, 4f).Contains(p)) lKitchen = true;
                    if (new Rect(6f, 4f, 4f, 4f).Contains(p)) lVault = true;
                    if (lPoint.errand && lPoint.action == PatrolAction.CheckDoor) lCheck = true;
                }
                Assert.IsTrue(lOffice && lKitchen, "every room a door leads to");
                Assert.IsFalse(lVault, "never through a locked door");
                Assert.IsTrue(lCheck, "it tries the locked door");
                Vector2 lStart = new Vector2(lLoop[0].position.x, lLoop[0].position.z);
                Assert.IsTrue(new Rect(0f, 0f, 6f, 4f).Contains(lStart), "starts in the hall");
            }
            finally
            {
                Object.DestroyImmediate(lRoot);
            }
        }
    }
}
