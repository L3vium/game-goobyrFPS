using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapWeisshorn
    {
        /// <summary>
        /// The Grand Hotel Weisshorn (docs/maps/Weisshorn.md section 4): 70 x 30 m, storeys of 4 m,
        /// local x west to east, z south (front) to north (slope). L0 underbelly, L1 staff tier, L2
        /// guest tier (lobby, spa, bar, cloakroom; the ballroom, double height, east), L3 suites and
        /// the mezzanine, L4 VIP floor and the ballroom roof, L5 attic, the clock tower above.
        /// </summary>
        public static ArchSpec HotelSpec(Vector3 pOrigin)
        {
            ArchStyle lStyle = ArchStyles.Alpine(true);
            lStyle.facade = ArchLib.Tinted("painted_plaster_wall", new Color(0.93f, 0.9f, 0.84f), "hotel", 3.5f);
            lStyle.plinth = ArchLib.Surface("stone_wall", 2.5f);
            lStyle.windowSpacing = 3.2f;
            lStyle.trim = ArchLib.Tinted("stone_wall", new Color(0.92f, 0.9f, 0.86f), "alp_trim", 2f);
            lStyle = ArchStyles.Grand(lStyle, new Color(0.86f, 0.8f, 0.68f), "hotel");
            ArchSpec h = new ArchSpec { name = "GrandHotel", origin = pOrigin, size = new Vector2(70, 30), storey = 4f, style = lStyle, zone = ZONE_HOTEL, plinth = 1.2f, cutLand = true };
            const SecurityTier R = SecurityTier.Restricted, H = SecurityTier.Hostile;
            const AccessLevel Gu = AccessLevel.Guest, St = AccessLevel.Staff, Se = AccessLevel.Security, Pr = AccessLevel.Private, No = AccessLevel.None;
            ArchMat lStone = ArchLib.Surface("stone_wall", 2.5f);

            // ---- L0 underbelly (opens south on the lower terrace)
            ArchRoom Stone(ArchRoom r) { r.wallMat = lStone; return r; }
            Stone(h.Room("Lockers", 0, 0, 14, 14, 30, RoomType.Workshop, R, St));
            Stone(h.Room("Boiler", 0, 14, 14, 28, 30, RoomType.Plant, R, St));
            Stone(h.Room("ColdStore", 0, 28, 14, 42, 30, RoomType.ColdRoom, R, St));
            Stone(h.Room("WaxStore", 0, 42, 14, 56, 30, RoomType.Store, R, No));
            Stone(h.Room("Laundry", 0, 0, 0, 20, 14, RoomType.Laundry, R, St));
            Stone(h.Room("ServiceCorridor", 0, 20, 0, 56, 14, RoomType.Corridor, R, St));
            Stone(h.Room("SnowcatBay", 0, 56, 0, 70, 30, RoomType.Garage, R, St));
            h.Door(0, 14, 22); h.Door(0, 28, 22); h.Door(0, 42, 22); h.Door(0, 7, 14); h.Door(0, 35, 14); h.Door(0, 49, 14);
            h.Door(0, 20, 7); h.Door(0, 56, 7); h.Door(0, 56, 22);
            h.Door(0, 10, 0, DoorKind.Door, 1.4f).poi = "Laundry door";
            h.Door(0, 30, 0, DoorKind.Double, 2.2f);
            h.Door(0, 63, 0, DoorKind.Doorway, 4.2f).height = 3.2f;
            h.Secure(0, 35, 30, SecureDoor.Lock.Crowbar, false, "Mine door");

            // ---- L1 staff tier (opens south on the ski-room terrace)
            // The two lower storeys are the hotel's rusticated stone base.
            Stone(h.Room("SkiRoom", 1, 0, 0, 14, 14, RoomType.Workshop, R, St));
            Stone(h.Room("StaffCorridor", 1, 14, 0, 42, 14, RoomType.Corridor, R, St));
            Stone(h.Room("Canteen", 1, 42, 0, 56, 14, RoomType.Mess, R, St));
            Stone(h.Room("Linen", 1, 0, 14, 14, 30, RoomType.Linen, R, No));
            Stone(h.Room("SecurityOffice", 1, 14, 14, 28, 30, RoomType.Security, H, Se));
            Stone(h.Room("Kitchens", 1, 28, 14, 42, 30, RoomType.Kitchen, R, St));
            Stone(h.Room("WineCellar", 1, 42, 14, 56, 30, RoomType.WineCellar, R, St));
            Stone(h.Room("Storage", 1, 56, 0, 70, 30, RoomType.Store, R, St));
            h.Door(1, 14, 7); h.Door(1, 7, 14); h.Door(1, 42, 7); h.Door(1, 49, 14); h.Door(1, 35, 14); h.Door(1, 42, 22); h.Door(1, 56, 7);
            h.Secure(1, 14, 22, SecureDoor.Lock.Keycard, true); h.Secure(1, 28, 22, SecureDoor.Lock.Keycard, true);
            h.Door(1, 7, 0, DoorKind.Double, 2f).poi = "Ski-room door";
            h.Door(1, 35, 30, DoorKind.Double, 2.4f).poi = "Kitchen delivery bay";
            h.Room("ChuteL1", 1, 14, 1, 15, 2, RoomType.Store, R, St).noFurniture = true;

            // ---- L2 guest tier
            h.Room("Spa", 2, 0, 16, 13, 30, RoomType.Spa, R, Gu);
            h.Room("Sauna", 2, 0, 26, 5, 30, RoomType.Spa, R, Gu).noFurniture = true;
            h.Room("Bar", 2, 13, 16, 26, 30, RoomType.Bar, R, Gu);
            h.Room("Cloakroom", 2, 26, 16, 40, 30, RoomType.Store, R, St);
            h.Room("Lobby", 2, 0, 0, 40, 16, RoomType.Lobby, R, Gu);
            h.Room("ServiceStairL2", 2, 29, 8, 40, 16, RoomType.Stair, R, St).noFurniture = true;
            h.Room("ChuteL2", 2, 14, 1, 15, 2, RoomType.Store, R, St).noFurniture = true;
            // The ballroom is dressed by hand round its stage (tables, chandelier), not by the furnisher.
            h.Room("Ballroom", 2, 40, 0, 70, 30, RoomType.Ballroom, R, Gu).noFurniture = true;
            ArchRoom lBallVoid = h.Room("Ballroom", 3, 40, 0, 70, 30, RoomType.Ballroom, R, Gu);
            lBallVoid.isVoid = lBallVoid.noFloor = lBallVoid.noFurniture = lBallVoid.noLight = lBallVoid.noZone = true;
            h.Door(2, 13, 23); h.Door(2, 26, 23); h.Door(2, 6, 16); h.Door(2, 24.5f, 16, DoorKind.Double, 2f); h.Door(2, 27.5f, 16); h.Door(2, 35, 16);
            h.Door(2, 40, 23, DoorKind.Door, 1.4f); h.Door(2, 40, 4, DoorKind.Double, 2.4f); h.Door(2, 29, 12);
            h.Door(2, 2.5f, 26);
            h.Door(2, 14.5f, 0, DoorKind.Double, 2.6f).poi = "Front doors";
            h.Door(2, 55, 0, DoorKind.Double, 2.6f).poi = "Ballroom terrace";
            h.Door(2, 70, 15, DoorKind.Double, 2.2f);
            h.Secure(2, 0, 20, SecureDoor.Lock.Padlock, false, "Spa pool-plant door");

            // ---- L3 suites round the lobby atrium, the mezzanine over the ballroom
            h.Room("SuiteA1", 3, 0, 20, 13, 30, RoomType.Suite, R, Gu);
            h.Room("SuiteA2", 3, 13, 20, 26, 30, RoomType.Bedroom, R, No);
            h.Room("SuiteA3", 3, 26, 20, 40, 30, RoomType.Bedroom, R, St);
            h.Room("SuiteA4", 3, 0, 0, 13, 8, RoomType.Bedroom, R, St);
            h.Room("VipLanding", 3, 13, 0, 26, 8, RoomType.Stair, R, St);
            h.Room("SuiteA5", 3, 26, 0, 40, 8, RoomType.Clinic, R, Gu);
            foreach (Rect r in AtriumRing()) { ArchRoom c = h.Room("CorridorA", 3, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Corridor, R, St); c.overlook = true; }
            ArchRoom lAtrium3 = h.Room("Atrium", 3, 12, 10, 28, 17, RoomType.Lobby, R, Gu);
            lAtrium3.isVoid = lAtrium3.noFloor = lAtrium3.noFurniture = lAtrium3.noLight = lAtrium3.noZone = true;
            foreach (Rect r in new[] { Rect.MinMaxRect(40, 26, 70, 30), Rect.MinMaxRect(40, 0, 43, 26), Rect.MinMaxRect(67, 0, 70, 26) })
            { ArchRoom m = h.Room("Mezzanine", 3, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, Gu); m.overlook = true; }
            h.Door(3, 6.5f, 20); h.Door(3, 19.5f, 20); h.Door(3, 33, 20); h.Door(3, 6.5f, 8); h.Door(3, 19.5f, 8); h.Door(3, 33, 8);
            h.Door(3, 13, 25, DoorKind.Door, 1.0f);
            h.Door(3, 40, 18.5f, DoorKind.Double, 1.8f).poi = "Mezzanine door";
            h.Door(3, 40, 4);
            foreach (float x in new[] { 6.5f, 19.5f, 33f }) h.balconies.Add(new ArchBalcony { floor = 3, at = new Vector2(x, 0), width = 3.2f, depth = 1.1f });
            foreach (float x in new[] { 6.5f, 19.5f }) h.balconies.Add(new ArchBalcony { floor = 4, at = new Vector2(x, 0), width = 3.2f, depth = 1.1f });
            foreach (float z in new[] { 12f, 24f }) h.balconies.Add(new ArchBalcony { floor = 3, at = new Vector2(0, z), width = 2.8f, depth = 1.0f });
            h.Room("ChuteL3", 3, 14, 1, 15, 2, RoomType.Store, R, St).noFurniture = true;

            // ---- L4 VIP floor, the ballroom roof
            h.Room("GuardPostB1", 4, 0, 20, 13, 30, RoomType.GuardRoom, H, Se);
            h.Room("DealerB2", 4, 13, 20, 26, 30, RoomType.Suite, H, Se);
            h.Room("BankerB3", 4, 26, 20, 40, 30, RoomType.Suite, H, Pr);
            h.Room("Booth", 4, 0, 0, 10, 8, RoomType.Office, R, St);
            h.Room("VipLobby", 4, 10, 0, 24, 8, RoomType.Hall, H, Se);
            h.Room("Presidential", 4, 24, 0, 33, 8, RoomType.Suite, H, Pr);
            h.Room("Cinema", 4, 33, 0, 40, 8, RoomType.Living, H, Pr);
            foreach (Rect r in AtriumRing()) { ArchRoom c = h.Room("CorridorB", 4, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Corridor, H, Se); c.overlook = true; }
            ArchRoom lAtrium4 = h.Room("Atrium", 4, 12, 10, 28, 17, RoomType.Lobby, R, Gu);
            lAtrium4.isVoid = lAtrium4.noFloor = lAtrium4.noFurniture = lAtrium4.noLight = lAtrium4.noZone = true;
            ArchRoom lRoof = h.Room("BallroomRoof", 4, 40, 0, 70, 30, RoomType.Terrace, H, Se); lRoof.open = true;
            h.Door(4, 13, 25); h.Door(4, 26, 25); h.Door(4, 6.5f, 20); h.Door(4, 19.5f, 20); h.Door(4, 33, 20);
            h.Door(4, 17, 8); h.Secure(4, 28.5f, 8, SecureDoor.Lock.Keypad, true, "Presidential keypad"); h.Secure(4, 36.5f, 8, SecureDoor.Lock.Keycard, true);
            h.Door(4, 33, 4);
            h.Secure(4, 5, 8, SecureDoor.Lock.Padlock, false, "Booth service landing");
            h.Door(4, 40, 18.5f, DoorKind.Door, 1.4f).poi = "Roof door";
            h.balconies.Add(new ArchBalcony { floor = 4, at = new Vector2(33, 30), width = 3.2f, depth = 1.1f });
            h.balconies.Add(new ArchBalcony { floor = 4, at = new Vector2(28.5f, 0), width = 3.6f, depth = 1.2f });
            h.Window(4, 6.5f, 30, WindowKind.Open).poi = "Guard post window";

            // ---- L5 attic, the clock room, the roof walk; the clock tower above.
            h.Room("AtticStore", 5, 0, 0, 20, 30, RoomType.Attic, R, No);
            h.Room("ClockRoom", 5, 20, 12, 28, 20, RoomType.GuardRoom, H, Se);
            foreach (Rect r in new[] { Rect.MinMaxRect(20, 0, 40, 12), Rect.MinMaxRect(20, 20, 40, 30), Rect.MinMaxRect(28, 12, 40, 20) })
            { ArchRoom w = h.Room("RoofWalk", 5, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Terrace, R, Se); w.open = true; }
            h.Door(5, 20, 16); h.Door(5, 28, 16); h.Door(5, 10, 0, DoorKind.Door, 1.2f);
            h.Window(5, 10, 30, WindowKind.Open).poi = "Crevasse window";
            h.Window(5, 0, 4, WindowKind.Open);
            for (int f = 6; f <= 8; f++) h.Room("ClockTower" + f, f, 20, 12, 28, 20, RoomType.Stair, H, Se).noFurniture = true;
            ArchRoom lBelfry = h.Room("Belfry", 9, 20, 12, 28, 20, RoomType.Belvedere, H, Se); lBelfry.arcade = true;
            // Deep timber eaves on the steep alpine roofs.
            h.Roof(5, 0, 0, 20, 30, RoofKind.Gable, false, 38f).overhang = 1.3f;
            h.Roof(9, 20, 12, 28, 20, RoofKind.Hip, true, 45f).overhang = 0.9f;

            // Stairs: the feature stair up the atrium (lobby -> L3 gallery), the west lobby stair, VIP (L3 -> L4),
            // service core (L0 -> L5), clock tower, ballroom side stairs.
            h.Stair(2, 18f, 9.5f, 22f, 16f, 2, false).grand = true;
            h.Stair(2, 2.4f, 4f, 7.2f, 13f, 0, true, true);
            h.Stair(3, 18.4f, 0.6f, 23.6f, 6.2f, 3, true, true);
            for (int f = 0; f <= 4; f++) h.Stair(f, 32f, 9f, 39.6f, 13.4f, 3, true, true);
            for (int f = 5; f <= 8; f++) h.Stair(f, 20.6f, 13.6f, 24.4f, 19.4f, 0, true, true);
            h.Stair(2, 40.6f, 18f, 42.4f, 24.4f, 0, false);
            h.Stair(2, 67.6f, 18f, 69.4f, 24.4f, 0, false);
            // Laundry chute L3 -> L0.
            for (int f = 1; f <= 3; f++) h.holes.Add((f, new Rect(14, 1, 1, 1)));
            // Skylight duct: ballroom roof -> mezzanine -> cloakroom -> L1 linen.
            ArchDuct lSky = new ArchDuct();
            lSky.nodes.Add(new Vector3(45f, 16.4f, 28.5f));
            lSky.nodes.Add(new Vector3(45f, 14.4f, 28.5f));
            lSky.nodes.Add(new Vector3(38.5f, 14.4f, 28.5f));
            lSky.nodes.Add(new Vector3(38.5f, 8.4f, 28.5f));
            lSky.nodes.Add(new Vector3(38.5f, 6.4f, 28.5f));
            lSky.nodes.Add(new Vector3(7.5f, 6.4f, 28.5f));
            lSky.grilles.Add((0, new Vector3Int(0, 1, 0), "Skylight vent"));
            lSky.grilles.Add((1, new Vector3Int(0, -1, 0), "Mezzanine vent"));
            lSky.grilles.Add((3, new Vector3Int(-1, 0, 0), "Cloakroom vent"));
            lSky.grilles.Add((5, new Vector3Int(0, -1, 0), "Linen vent"));
            h.ducts.Add(lSky);
            // Dumbwaiter: wine cellar -> behind the bar -> projection booth (a rung shaft, hatches).
            ArchDuct lDumb = new ArchDuct();
            lDumb.nodes.Add(new Vector3(42.5f, 4.4f, 15.5f));
            lDumb.nodes.Add(new Vector3(41.5f, 4.4f, 15.5f));
            lDumb.grilles.Add((0, new Vector3Int(1, 0, 0), "Dumbwaiter"));
            h.ducts.Add(lDumb);

            h.cornices.Add(3); h.cornices.Add(4); h.cornices.Add(5);
            h.drains.Add(new ArchDrain { at = new Vector2(1.5f, 0), topFloor = 3, poi = "South drainpipe" });
            h.drains.Add(new ArchDrain { at = new Vector2(0, 8f), topFloor = 4 });
            h.drains.Add(new ArchDrain { at = new Vector2(70f, 26f), topFloor = 3 });
            h.drains.Add(new ArchDrain { at = new Vector2(39.5f, 0), topFloor = 4, poi = "Ballroom drainpipe" });
            h.drains.Add(new ArchDrain { at = new Vector2(68.5f, 0), topFloor = 3 });
            h.drains.Add(new ArchDrain { at = new Vector2(70f, 4f), topFloor = 3 });
            return h;
        }

        private static Rect[] AtriumRing() => new[]
        {
            Rect.MinMaxRect(0, 17, 40, 20), Rect.MinMaxRect(0, 8, 12, 17), Rect.MinMaxRect(28, 8, 40, 17), Rect.MinMaxRect(12, 8, 28, 10)
        };
    }
}
