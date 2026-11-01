using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapQasrAlRimal
    {
        /// <summary>
        /// The keep (docs/maps/QasrAlRimal.md section 4): 34 x 34 m round a screened light well, local x
        /// west to east, z south to north, in three bays (0-11, 11-23, 23-34). Cellar, ground, F1, F2,
        /// the glass penthouse on F3 and the roof (helipad, wind tower).
        /// </summary>
        public static ArchSpec KeepSpec(Vector3 pOrigin)
        {
            ArchSpec k = new ArchSpec { name = "Keep", origin = pOrigin, size = new Vector2(34, 34), style = ArchStyles.Fortress(), zone = ZONE_CITADEL };
            const SecurityTier R = SecurityTier.Restricted, H = SecurityTier.Hostile;
            const AccessLevel Gu = AccessLevel.Guest, St = AccessLevel.Staff, Se = AccessLevel.Security, Pr = AccessLevel.Private, No = AccessLevel.None;

            // ---- cellar (-1)
            k.Room("CisternLanding", -1, 0, 23, 11, 34, RoomType.Cellar, R, No);
            k.Room("CoolerPlant", -1, 11, 23, 23, 34, RoomType.Plant, R, St);
            k.Room("Armoury", -1, 23, 23, 34, 34, RoomType.Armoury, H, Se);
            k.Room("OldCells", -1, 0, 11, 11, 23, RoomType.Store, R, No);
            k.Room("Cistern", -1, 11, 11, 23, 23, RoomType.Cellar, R, No).noFurniture = true;
            k.Room("Vault", -1, 23, 11, 34, 23, RoomType.Vault, H, Pr);
            k.Room("Monitoring", -1, 0, 0, 34, 11, RoomType.Security, H, Se);
            k.Door(-1, 11, 28); k.Secure(-1, 23, 28, SecureDoor.Lock.Keycard, true);
            k.Door(-1, 5, 23); k.Secure(-1, 28, 23, SecureDoor.Lock.Keypad, true, "Vault keypad");
            k.Secure(-1, 5, 11, SecureDoor.Lock.Keycard, true);
            k.Door(-1, 28, 11, DoorKind.Door, 1.4f);
            k.Door(-1, 11, 17); k.Door(-1, 17, 23);
            // The well shaft from the qanat comes up through the landing's floor.
            k.holes.Add((-1, new Rect(4.4f, 27.9f, 2.2f, 2.2f)));

            // ---- ground (0)
            k.Room("Kitchen", 0, 0, 23, 11, 34, RoomType.Kitchen, R, St);
            k.Room("MessHall", 0, 11, 23, 23, 34, RoomType.Mess, R, St);
            k.Room("Granary", 0, 23, 23, 34, 34, RoomType.Store, R, St);
            k.Room("PetitionersHall", 0, 0, 0, 11, 23, RoomType.Hall, R, Gu);
            ArchRoom lCourt = k.Room("InnerCourt", 0, 11, 11, 23, 23, RoomType.Terrace, R, Gu); lCourt.open = true;
            k.Room("GuardRoom", 0, 23, 11, 34, 23, RoomType.GuardRoom, H, Se);
            k.Room("GateHall", 0, 11, 0, 23, 11, RoomType.Hall, R, Gu);
            k.Room("Linen", 0, 23, 0, 34, 11, RoomType.Linen, R, No);
            k.Door(0, 11, 28); k.Door(0, 23, 28); k.Door(0, 5, 23); k.Door(0, 28, 23);
            k.Door(0, 11, 17, DoorKind.Double, 1.8f); k.Door(0, 23, 17); k.Door(0, 17, 11, DoorKind.Double, 2.2f);
            k.Door(0, 23, 5); k.Door(0, 11, 5);
            k.Door(0, 19, 0, DoorKind.Double, 2.4f).poi = "Gate hall";
            k.Door(0, 0, 28, DoorKind.Door, 1.6f).poi = "Kitchen delivery door";
            k.Secure(0, 34, 5, SecureDoor.Lock.Padlock, false, "Linen yard door");
            k.Door(0, 34, 28, DoorKind.Door, 1.4f).poi = "Grain chute";

            // ---- F1
            k.Room("ScribesOffice", 1, 0, 19, 11, 34, RoomType.Office, R, St);
            k.Room("Niche", 1, 0, 11, 2.5f, 19, RoomType.Stair, R, Se).noFurniture = true;
            k.Room("ScribesHall", 1, 2.5f, 11, 11, 19, RoomType.Archive, R, St);
            k.Room("Servery", 1, 11, 23, 23, 34, RoomType.Pantry, R, St);
            k.Room("Archive", 1, 23, 23, 34, 34, RoomType.Archive, R, St);
            k.Room("CountingRoom", 1, 23, 11, 34, 23, RoomType.Office, R, Se);
            k.Room("WaitingGallery", 1, 0, 0, 11, 11, RoomType.Gallery, R, Gu);
            k.Room("AudienceHall", 1, 11, 0, 34, 11, RoomType.Ballroom, R, Gu);
            foreach (Rect r in WellRing(false)) { ArchRoom g = k.Room("WellGalleryS", 1, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, Gu); g.arcade = true; }
            foreach (Rect r in WellRing(true)) { ArchRoom g = k.Room("WellGalleryN", 1, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, St); g.arcade = true; }
            k.Door(1, 11, 28); k.Door(1, 23, 28); k.Door(1, 5, 19); k.Door(1, 11, 15);
            k.Door(1, 17, 23); k.Door(1, 28, 23); k.Secure(1, 23, 17, SecureDoor.Lock.Keycard, true);
            k.Door(1, 11, 5); k.Door(1, 17, 11, DoorKind.Double, 2.0f); k.Door(1, 11.75f, 17); k.Door(1, 22.25f, 17);
            k.Door(1, 2.5f, 11.7f, DoorKind.Door, 1.0f).poi = "Calligraphy niche";
            k.Door(1, 1.25f, 11, DoorKind.Doorway, 1.0f);
            k.Window(1, 0, 28, WindowKind.Open).poi = "Scribes' window";

            // ---- F2
            k.Room("BunkRoom", 2, 0, 28.5f, 11, 34, RoomType.Dormitory, R, No);
            k.Room("OfficersQuarters", 2, 0, 23, 11, 28.5f, RoomType.Bedroom, R, Se);
            k.Room("RadioRoom", 2, 11, 23, 23, 34, RoomType.Radio, H, Se);
            k.Room("WarRoom", 2, 23, 11, 34, 34, RoomType.Office, H, Pr);
            k.Room("Anteroom", 2, 0, 11, 11, 23, RoomType.Office, R, Se);
            foreach (Rect r in WellRing(false)) { ArchRoom g = k.Room("Gallery2", 2, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, Se); g.arcade = true; }
            foreach (Rect r in WellRing(true)) { ArchRoom g = k.Room("Gallery2", 2, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, Se); g.arcade = true; }
            k.Room("StairHead", 2, 0, 0, 11, 11, RoomType.Stair, R, Se);
            k.Room("OfficersMess", 2, 11, 0, 34, 11, RoomType.Mess, R, Se);
            k.Door(2, 11, 26); k.Door(2, 5.5f, 28.5f); k.Secure(2, 23, 28, SecureDoor.Lock.Keycard, true);
            k.Door(2, 5, 23); k.Secure(2, 11, 17, SecureDoor.Lock.Keycard, true);
            k.Door(2, 23, 17); k.Door(2, 17, 23);
            k.Door(2, 5, 11); k.Door(2, 11, 5); k.Door(2, 17, 11);
            k.Door(2, 28, 11);
            foreach (float x in new[] { 15f, 21f, 27f }) k.balconies.Add(new ArchBalcony { floor = 2, at = new Vector2(x, 0), width = 2.4f, depth = 1.0f });
            k.balconies.Add(new ArchBalcony { floor = 2, at = new Vector2(34, 5.5f), width = 2.6f, depth = 1.2f });

            // ---- F3 penthouse: a glass ring round the light well.
            ArchMat lMarble = ArchLib.Tinted("marble_01", new Color(0.98f, 0.97f, 0.95f), "penthouse", 2.5f);
            ArchRoom Glass(ArchRoom r) { r.wallMat = lMarble; r.windowWidth = 2.4f; return r; }
            Glass(k.Room("Dining", 3, 0, 23, 11, 34, RoomType.Dining, H, Se));
            Glass(k.Room("Pantry", 3, 11, 23, 23, 34, RoomType.Pantry, H, St));
            Glass(k.Room("Study", 3, 23, 23, 34, 34, RoomType.Study, H, Pr));
            Glass(k.Room("Vestibule", 3, 0, 0, 11, 23, RoomType.Hall, H, Se));
            Glass(k.Room("Bedroom", 3, 23, 11, 34, 23, RoomType.Suite, H, Pr));
            Glass(k.Room("BedroomWC", 3, 30, 19, 34, 23, RoomType.Bath, H, Pr));
            Glass(k.Room("Majlis", 3, 11, 0, 23, 11, RoomType.Salon, H, Se));
            ArchRoom lPool = k.Room("PoolTerrace", 3, 23, 0, 34, 11, RoomType.Terrace, H, Se); lPool.open = true;
            foreach (Rect r in WellRing(false)) { ArchRoom g = k.Room("GlassGallery", 3, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, H, Se); g.arcade = true; }
            foreach (Rect r in WellRing(true)) { ArchRoom g = k.Room("GlassGallery", 3, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, H, Se); g.arcade = true; }
            k.Door(3, 11, 28); k.Door(3, 23, 28); k.Door(3, 5, 23); k.Door(3, 28, 23);
            k.Door(3, 11, 17); k.Door(3, 23, 15); k.Door(3, 32, 19, DoorKind.Door, 1.0f);
            k.Door(3, 17, 11, DoorKind.Double, 2.0f); k.Door(3, 11, 5); k.Door(3, 23, 5, DoorKind.Double, 2.4f);
            k.Door(3, 28, 11);

            // ---- roof (F4): helipad terrace, the wind tower.
            ArchRoom lRoof = k.Room("Helipad", 4, 0, 0, 34, 34, RoomType.Terrace, H, Se); lRoof.open = true;
            lRoof.noZone = false;
            k.holes.Add((4, new Rect(13.5f, 13.5f, 7f, 7f)));

            // Stairs: grand (gate hall -> F1, waiting gallery -> F2), private (F2 -> F3), service core (cellar -> roof), the scribes' secret stair.
            k.Stair(0, 11.6f, 3f, 16.4f, 9.6f, 0, true, true);
            k.Stair(1, 5.6f, 1.6f, 10.4f, 8.2f, 0, true, false);
            k.Stair(2, 5.6f, 1.6f, 10.4f, 8.2f, 0, true, false);
            for (int f = -1; f <= 3; f++) k.Stair(f, 17.6f, 25f, 22.4f, 30.6f, 2, true, true);
            k.Stair(1, 0.85f, 11.6f, 2.05f, 17.6f, 0, false);

            // Wind-tower duct: roof -> F3 pantry -> F2 radio room -> mess hall -> cooler plant.
            ArchDuct lWind = new ArchDuct();
            lWind.nodes.Add(new Vector3(12.5f, -3.6f, 32.5f));
            lWind.nodes.Add(new Vector3(12.5f, 0.4f, 32.5f));
            lWind.nodes.Add(new Vector3(12.5f, 7.4f, 32.5f));
            lWind.nodes.Add(new Vector3(12.5f, 11.4f, 32.5f));
            lWind.nodes.Add(new Vector3(12.5f, 14.4f, 32.5f));
            lWind.grilles.Add((0, new Vector3Int(1, 0, 0), "Cooler plant vent"));
            lWind.grilles.Add((1, new Vector3Int(1, 0, 0), "Mess hall vent"));
            lWind.grilles.Add((2, new Vector3Int(1, 0, 0), "Radio room vent"));
            lWind.grilles.Add((3, new Vector3Int(1, 0, 0), "Pantry vent"));
            lWind.grilles.Add((4, new Vector3Int(0, 1, 0), "Wind tower"));
            k.ducts.Add(lWind);

            k.cornices.Add(1); k.cornices.Add(2); k.cornices.Add(3);
            k.drains.Add(new ArchDrain { at = new Vector2(0, 25f), topFloor = 1, poi = "West drainpipe" });
            k.drains.Add(new ArchDrain { at = new Vector2(34, 30f), topFloor = 3 });
            k.drains.Add(new ArchDrain { at = new Vector2(8f, 34f), topFloor = 3 });
            return k;
        }

        /// <summary>The light-well gallery ring (south half or north half) round the void.</summary>
        private static Rect[] WellRing(bool pNorth) => pNorth
            ? new[] { Rect.MinMaxRect(11, 20.5f, 23, 23), Rect.MinMaxRect(11, 17, 13.5f, 20.5f), Rect.MinMaxRect(20.5f, 17, 23, 20.5f) }
            : new[] { Rect.MinMaxRect(11, 11, 23, 13.5f), Rect.MinMaxRect(11, 13.5f, 13.5f, 17), Rect.MinMaxRect(20.5f, 13.5f, 23, 17) };
    }
}
