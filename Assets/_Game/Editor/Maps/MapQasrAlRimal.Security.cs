using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapQasrAlRimal
    {
        private ArchSpec _roofHut;
        private ArchResult _roofHutR;

        /// <summary>
        /// The keep's sentry guns, their hidden terminals and cables, the extra cameras and the court hoses
        /// (offices get their PCs and kitchens, messes and guard rooms their fans from the furnisher).
        /// <list type="bullet">
        /// <item>Cellar: one gun in the vault (over the keypad door, the monitoring door and the vault desk),
        /// one in the armoury (over its keycard door). The cable runs up the vault's east wall, through the
        /// armoury and along the cooler plant's ceiling to the terminal by the plant's west wall, reached
        /// through the wind-tower vent or the cistern landing without passing a gun.</item>
        /// <item>The governor's floors: one gun in the war room, one in the penthouse study above it. The
        /// cable climbs to the roof and runs along the north parapet, round the wind tower, to the terminal
        /// in the roof hut (scaffold, wind-tower rungs or the service stair). Hacked, the war-room gun covers
        /// the map table where the governor's Office routine stops: an accident.</item>
        /// </list>
        /// </summary>
        private void Security()
        {
            ArchResult b = _keepR;
            float H = b.Storey - b.spec.slab - 0.06f;

            // ---- the roof hut on the keep (a small masonry blockhouse west of the light well)
            ArchStyle lHutStyle = ArchStyles.Fortress();
            lHutStyle.outerThickness = 0.3f;
            _roofHut = new ArchSpec { name = "KeepRoofHut", origin = Kp(2f, 4, 17f, 0f), yaw = _keep.yaw, size = new Vector2(4, 3), style = lHutStyle, zone = ZONE_CITADEL, plinth = 0.05f, furnish = false, stringCourses = false };
            _roofHut.Room("RoofHutRoom", 0, 0, 0, 4, 3, RoomType.Plant, SecurityTier.Restricted, AccessLevel.Staff);
            _roofHut.Door(0, 4f, 1.5f, DoorKind.Door, 1.0f);
            _roofHutR = Build(_roofHut, ZONE_CITADEL);

            // ---- cellar: the vault and the armoury
            AutoTurret lVault = A.Turret(Kp(32.6f, -1, 12.2f, 0f), Kp(26f, -1, 20f, 1f));
            AutoTurret lArmoury = A.Turret(Kp(32.6f, -1, 32.6f, 0f), Kp(24f, -1, 27f, 1f));
            A.Cable(lVault, Route(b,
                new Vector4(32.6f, -1, 12.2f, 0.06f), new Vector4(33.14f, -1, 12.2f, 0.06f), new Vector4(33.14f, -1, 12.2f, H),
                new Vector4(33.14f, -1, 22.86f, H), new Vector4(33.14f, -1, 23.14f, H), new Vector4(33.14f, -1, 32.0f, H),
                new Vector4(33.14f, -1, 32.0f, 0.06f), new Vector4(32.9f, -1, 32.3f, 0.06f)));
            A.Cable(lArmoury, Route(b,
                new Vector4(32.6f, -1, 32.6f, 0.06f), new Vector4(32.6f, -1, 33.14f, 0.06f), new Vector4(32.6f, -1, 33.14f, H - 0.09f),
                new Vector4(23.14f, -1, 33.14f, H - 0.09f), new Vector4(22.86f, -1, 33.14f, H - 0.09f), new Vector4(11.14f, -1, 33.14f, H - 0.09f),
                new Vector4(11.14f, -1, 25.6f, H - 0.09f), new Vector4(11.14f, -1, 25.6f, 0.95f), new Vector4(11.45f, -1, 25.3f, 0.85f)));
            TerminalDesk(b, 11.6f, -1, 25f, 90f, lVault, lArmoury);

            // ---- the war room (F2) and the penthouse study (F3)
            AutoTurret lWar = A.Turret(Kp(32.6f, 2, 32.6f, 0f), Kp(25f, 2, 20f, 1f));
            AutoTurret lStudy = A.Turret(Kp(32.6f, 3, 32.6f, 0f), Kp(26f, 3, 25f, 1f));
            A.Cable(lStudy, Route(b,
                new Vector4(32.6f, 3, 32.6f, 0.06f), new Vector4(33.14f, 3, 32.6f, 0.06f), new Vector4(33.14f, 2, 32.6f, H),
                new Vector4(33.14f, 2, 32.6f, 0.06f), new Vector4(32.9f, 2, 32.6f, 0.06f)));
            A.Cable(lWar, Join(
                Route(b,
                    new Vector4(32.6f, 2, 32.6f, 0.06f), new Vector4(32.6f, 2, 33.14f, 0.06f), new Vector4(32.6f, 2, 33.14f, H),
                    new Vector4(32.6f, 3, 33.14f, 0.06f), new Vector4(32.6f, 3, 33.14f, H), new Vector4(32.6f, 4, 33.64f, 0.06f),
                    new Vector4(14.6f, 4, 33.64f, 0.06f), new Vector4(14.6f, 4, 30.6f, 0.06f), new Vector4(6.3f, 4, 30.6f, 0.06f),
                    new Vector4(6.3f, 4, 19.5f, 0.06f)),
                Route(_roofHutR,
                    new Vector4(4.06f, 0, 2.5f, 0.06f), new Vector4(3.64f, 0, 2.5f, 0.06f), new Vector4(3.64f, 0, 2.64f, 0.06f),
                    new Vector4(2.95f, 0, 2.64f, 0.06f), new Vector4(2.95f, 0, 2.64f, 0.95f), new Vector4(2.6f, 0, 2.45f, 0.85f))));
            TerminalDesk(_roofHutR, 1.9f, 0, 2.2f, 180f, lWar, lStudy);

            // ---- cameras: the citadel gate, the vault corridor, the war-room door, the second-court gate
            A.Camera(G(-7.0f, CITADEL.yMin + 0.8f, 4.6f), G(-4f, CITADEL.yMin + 10f, 0.5f), ZONE_CITADEL);
            A.Camera(Kp(0.6f, -1, 10.4f, 3.1f), Kp(28f, -1, 8f, 0.5f), ZONE_CITADEL);
            A.Camera(Kp(11.6f, 2, 33.0f, 3.1f), Kp(22.5f, 2, 28f, 1f), ZONE_CITADEL);
            A.Camera(G(37.5f, KEEP.z - 0.6f, 4.4f), G(34f, KEEP.z + 8f, 0.5f), ZONE_CITADEL);

            // ---- hoses at the foot of the walls: the third and second courts, the date press, the caravanserai
            A.Distraction("hose", Kp(-0.02f, 0, 20f, 0f), b.Yaw(-90f));
            A.Distraction("hose", Kp(34.02f, 0, 15f, 0f), b.Yaw(90f));
            A.Distraction("hose", _pressR.At(-0.02f, 0, 2.5f, 0f), _pressR.Yaw(-90f));
            A.Distraction("hose", _seraiR.At(-0.02f, 0, 3f, 0f), _seraiR.Yaw(-90f));
        }
    }
}
