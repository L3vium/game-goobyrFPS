using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapSanLazaro
    {
        /// <summary>
        /// Casa La Cumbre, from the design's plans (docs/maps/SanLazaro.md section 4): a 40 x 32 m
        /// courtyard house, local x west to east, z south to north. Vault (-2), cellar (-1), ground,
        /// F1, F2 and the mirador tower (F2 to F5, belvedere on F6). Portico on the ground and the
        /// gallery ring on F1 round the patio, loggia and open south gallery, grand, service, crypt,
        /// priest's and mirador stairs, the kitchen-hood vent, the laundry chute, the purge duct,
        /// cornice ledges on F1 and F2, the buyer's balcony, the suite balcony, drainpipes.
        /// </summary>
        public static ArchSpec CasaSpec(Vector3 pOrigin, float pYaw)
        {
            ArchSpec c = new ArchSpec { name = "CasaLaCumbre", origin = pOrigin, yaw = pYaw, size = new Vector2(40, 32), style = ArchStyles.Grand(ArchStyles.Colonial(new Color(0.96f, 0.95f, 0.91f), "casa"), new Color(0.95f, 0.88f, 0.72f), "casa"), zone = ZONE_HACIENDA };
            const SecurityTier P = SecurityTier.Public, R = SecurityTier.Restricted, H = SecurityTier.Hostile;
            const AccessLevel Gu = AccessLevel.Guest, St = AccessLevel.Staff, Se = AccessLevel.Security, Pr = AccessLevel.Private, No = AccessLevel.None;
            _ = P;

            // ---- vault (-2) and cellar (-1)
            c.Room("Vault", -2, 13, 10, 27, 17, RoomType.Vault, H, Pr);
            c.Room("WineCellar", -1, 0, 24, 10, 32, RoomType.WineCellar, R, St);
            c.Room("ColdRoom", -1, 10, 24, 20, 32, RoomType.ColdRoom, R, St);
            c.Room("CoalStore", -1, 20, 24, 30, 32, RoomType.CoalStore, R, St);
            c.Room("Laundry", -1, 30, 14, 40, 32, RoomType.Laundry, R, St);
            c.Room("Crypt", -1, 0, 14, 10, 24, RoomType.Crypt, R, No);
            c.Room("CellarCorridor", -1, 10, 18, 30, 24, RoomType.Corridor, R, St);
            c.Room("SecurityRoom", -1, 12, 10, 28, 18, RoomType.Security, H, Se);
            c.Room("VaultStair", -1, 14, 10, 17.5f, 17, RoomType.Stair, H, Pr).noFurniture = true;
            c.Door(-1, 10, 28); c.Door(-1, 20, 28); c.Door(-1, 30, 28);
            c.Door(-1, 5, 24); c.Door(-1, 30, 21); c.Door(-1, 10, 21);
            c.Secure(-1, 14.5f, 18, SecureDoor.Lock.Keycard, true);
            c.Secure(-1, 26, 18, SecureDoor.Lock.Keycard, true);
            c.Secure(-1, 17.5f, 16.2f, SecureDoor.Lock.Keypad, true, "Vault keypad");
            c.Door(-1, 0, 28, DoorKind.Door, 1.4f).poi = "Cave gate";
            c.Stair(-2, 14.4f, 10.4f, 16f, 16.4f, 0, false);
            // Purge duct: vault -> security room (a trap pocket with two ways, both into security).
            ArchDuct lPurge = new ArchDuct();
            lPurge.nodes.Add(new Vector3(25.5f, -7.2f, 15.5f));
            lPurge.nodes.Add(new Vector3(25.5f, -3.2f, 15.5f));
            lPurge.grilles.Add((0, new Vector3Int(-1, 0, 0), "Purge duct"));
            lPurge.grilles.Add((1, new Vector3Int(-1, 0, 0), "Purge duct"));
            c.ducts.Add(lPurge);
            // Coal chute: a padlocked hatch outside the north wall, a shaft down to the coal store.
            ArchDuct lCoal = new ArchDuct();
            lCoal.nodes.Add(new Vector3(25.5f, -3.6f, 31f));
            lCoal.nodes.Add(new Vector3(25.5f, -3.6f, 33f));
            lCoal.nodes.Add(new Vector3(25.5f, -0.6f, 33f));
            lCoal.grilles.Add((0, new Vector3Int(0, 0, -1), "Coal chute"));
            lCoal.grilles.Add((2, new Vector3Int(0, 1, 0), "Coal chute"));
            c.ducts.Add(lCoal);

            // ---- ground (0)
            c.Room("Sacristy", 0, 0, 24, 10, 32, RoomType.Sacristy, R, No);
            c.Room("Salon", 0, 10, 24, 20, 32, RoomType.Salon, R, Gu);
            c.Room("Dining", 0, 20, 24, 30, 32, RoomType.Dining, R, Gu);
            c.Room("Kitchen", 0, 30, 24, 40, 32, RoomType.Kitchen, R, St);
            c.Room("Chapel", 0, 0, 14, 10, 24, RoomType.Chapel, R, Gu);
            c.Room("PriestStair", 0, 0, 14, 2, 22, RoomType.Stair, R, Se).noFurniture = true;
            c.Room("Pantry", 0, 30, 14, 40, 24, RoomType.Pantry, R, St);
            c.Room("Chute", 0, 30.5f, 14.5f, 31.5f, 15.5f, RoomType.Store, R, St).noFurniture = true;
            c.Room("Hall", 0, 0, 0, 10, 14, RoomType.Hall, R, Gu);
            c.Room("Office", 0, 30, 0, 40, 14, RoomType.Office, R, Se);
            ArchRoom lLoggia = c.Room("Loggia", 0, 10, 0, 30, 6, RoomType.Loggia, R, Gu); lLoggia.arcade = true;
            ArchRoom lPatio = c.Room("Patio", 0, 12.5f, 8.5f, 27.5f, 21.5f, RoomType.Terrace, R, Gu); lPatio.open = true;
            foreach (Rect r in PatioRing()) { ArchRoom lPortico = c.Room("Portico", 0, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, Gu); lPortico.arcade = true; }
            c.Door(0, 10, 28); c.Door(0, 20, 28, DoorKind.Double, 1.8f); c.Door(0, 30, 28);
            c.Door(0, 5, 24); c.Door(0, 15, 24, DoorKind.Double, 1.8f); c.Door(0, 25, 24, DoorKind.Double, 1.8f); c.Door(0, 35, 24);
            c.Door(0, 10, 19); c.Door(0, 30, 19); c.Door(0, 5, 14); c.Door(0, 35, 14);
            c.Door(0, 10, 10, DoorKind.Double, 1.8f);
            c.Secure(0, 30, 10, SecureDoor.Lock.Keycard, true);
            c.Door(0, 10, 3); c.Door(0, 30, 3);
            c.Door(0, 3, 0, DoorKind.Double, 2.0f).poi = "Main door";
            c.Door(0, 36, 32, DoorKind.Door, 1.4f).poi = "Kitchen yard door";
            c.Door(0, 2, 15.2f, DoorKind.Door, 1.0f).poi = "Confessional";
            c.Secure(0, 0, 29, SecureDoor.Lock.Crowbar, false, "Boarded sacristy window").sill = 0.9f;
            c.doors[c.doors.Count - 1].height = 1.5f;
            // The grand stair: one straight marble flight from the hall up to the first-floor landing.
            c.Stair(0, 4.5f, 3.5f, 8.5f, 10.5f, 0, false).grand = true;
            c.Stair(1, 0.5f, 8f, 3f, 13.5f, 0, true, true);
            c.Stair(-1, 36, 14.5f, 39.6f, 19.5f, 2, true, true);
            c.Stair(0, 36, 14.5f, 39.6f, 19.5f, 2, true, true);
            c.Stair(1, 36, 14.5f, 39.6f, 19.5f, 2, true, true);
            c.Stair(-1, 6, 15, 9.5f, 20, 0, true, false);
            c.Stair(0, 0.5f, 15.2f, 1.7f, 21.2f, 0, false);
            c.holes.Add((0, new Rect(30.5f, 14.5f, 1f, 1f)));
            c.holes.Add((1, new Rect(30.5f, 14.5f, 1f, 1f)));

            // ---- F1
            c.Room("GuestB", 1, 0, 24, 10, 32, RoomType.Bedroom, R, St);
            c.Room("MasterSuite", 1, 10, 24, 20, 32, RoomType.Suite, H, Pr);
            c.Room("SuiteWC", 1, 17, 28.5f, 20, 32, RoomType.Bath, H, Pr);
            c.Room("Study", 1, 20, 24, 30, 32, RoomType.Study, H, Pr);
            c.Room("GuardRoom", 1, 30, 24, 40, 32, RoomType.GuardRoom, H, Se);
            c.Room("Anteroom", 1, 0, 14, 10, 24, RoomType.Office, R, Se);
            c.Room("Linen", 1, 30, 14, 40, 24, RoomType.Linen, R, No);
            c.Room("StairLanding", 1, 0, 0, 10, 14, RoomType.Stair, R, St);
            c.Room("GuestA", 1, 30, 0, 40, 14, RoomType.Bedroom, R, Se);
            foreach (Rect r in PatioRing()) { ArchRoom lGallery = c.Room("Gallery", 1, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, St); lGallery.arcade = true; }
            ArchRoom lSouth = c.Room("Gallery", 1, 10, 0, 30, 6, RoomType.Gallery, R, St); lSouth.arcade = true;
            c.Door(1, 10, 28); c.Door(1, 5, 24); c.Door(1, 20, 27); c.Door(1, 30, 28);
            c.Door(1, 15, 24); c.Door(1, 18.5f, 28.5f, DoorKind.Door, 1.0f);
            c.Secure(1, 25, 24, SecureDoor.Lock.Keycard, true);
            c.Door(1, 35, 24); c.Door(1, 30, 19); c.Door(1, 35, 14); c.Door(1, 30, 10);
            c.Secure(1, 10, 19, SecureDoor.Lock.Keycard, true);
            c.Door(1, 5, 14); c.Door(1, 10, 10, DoorKind.Double, 1.8f);
            c.Window(1, 25, 32, WindowKind.Open).poi = "Study window";
            c.Window(1, 35, 32, WindowKind.Open);
            c.Window(1, 5, 32, WindowKind.Open);
            c.balconies.Add(new ArchBalcony { floor = 1, at = new Vector2(14, 32), width = 2.6f });
            c.balconies.Add(new ArchBalcony { floor = 1, at = new Vector2(35, 0), width = 3.0f });
            c.Stair(1, 31, 25.5f, 34.5f, 30.5f, 0, true, true);

            // ---- F2
            c.Room("AtticStore", 2, 0, 24, 10, 32, RoomType.Attic, R, No);
            c.Room("Dormitory", 2, 10, 24, 20, 32, RoomType.Dormitory, R, St);
            c.Room("Archive", 2, 20, 24, 30, 32, RoomType.Archive, R, Se);
            c.Room("MiradorBase", 2, 30, 24, 40, 32, RoomType.GuardRoom, H, Se);
            c.Room("StairHall", 2, 30, 14, 40, 24, RoomType.Stair, R, St);
            // The third floor runs round all four wings (servants in the west, nursery and sewing room
            // in the south, the steward's office in the east), with the patio gallery a storey higher.
            c.Room("StairTop", 2, 0, 0, 10, 14, RoomType.Stair, R, St);
            c.Room("Servants", 2, 0, 14, 10, 24, RoomType.Dormitory, R, St);
            c.Room("Nursery", 2, 10, 0, 20, 6, RoomType.Bedroom, R, St);
            c.Room("SewingRoom", 2, 20, 0, 30, 6, RoomType.Linen, R, St);
            c.Room("Steward", 2, 30, 0, 40, 14, RoomType.Office, R, Se);
            foreach (Rect r in PatioRing()) { ArchRoom lUpper = c.Room("UpperGallery", 2, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, R, St); lUpper.arcade = true; }
            c.Door(2, 10, 28); c.Door(2, 20, 28); c.Door(2, 30, 28); c.Door(2, 35, 24);
            c.Door(2, 3, 24); c.Door(2, 35, 14); c.Door(2, 5, 14);
            c.Door(2, 10, 19); c.Door(2, 30, 19); c.Door(2, 15, 6); c.Door(2, 25, 6); c.Door(2, 15, 24);
            c.Door(2, 10, 3); c.Door(2, 30, 3);
            c.balconies.Add(new ArchBalcony { floor = 2, at = new Vector2(15, 0), width = 2.4f });
            c.balconies.Add(new ArchBalcony { floor = 2, at = new Vector2(25, 0), width = 2.4f });
            c.balconies.Add(new ArchBalcony { floor = 2, at = new Vector2(0, 19), width = 2.4f });
            // Attic vent: through the attic's south wall from the roof walk.
            ArchDuct lAttic = new ArchDuct();
            lAttic.nodes.Add(new Vector3(6.5f, 7.25f, 23.5f));
            lAttic.nodes.Add(new Vector3(6.5f, 7.25f, 25.5f));
            lAttic.grilles.Add((0, new Vector3Int(0, 0, -1), "Attic vent"));
            lAttic.grilles.Add((1, new Vector3Int(0, 0, 1), "Attic vent"));
            c.ducts.Add(lAttic);

            // ---- mirador tower (F3-F5) and belvedere (F6)
            c.Room("Mirador3", 3, 30, 24, 40, 32, RoomType.Store, H, Se);
            c.Room("Mirador4", 4, 30, 24, 40, 32, RoomType.Store, H, Se);
            c.Room("Mirador5", 5, 30, 24, 40, 32, RoomType.GuardRoom, H, Se);
            ArchRoom lBelvedere = c.Room("Belvedere", 6, 30, 24, 40, 32, RoomType.Belvedere, H, Se); lBelvedere.arcade = true;
            for (int f = 2; f <= 5; f++) c.Stair(f, 31, 25.5f, 34.5f, 30.5f, 0, true, true);
            c.Roof(6, 30, 24, 40, 32, RoofKind.Hip, true, 30f).overhang = 0.8f;
            c.Roof(2, 0, 24, 30, 32, RoofKind.Hip, true, 26f);

            // Kitchen-hood vent: pantry -> linen -> study ceiling -> roof.
            ArchDuct lHood = new ArchDuct();
            lHood.nodes.Add(new Vector3(31.5f, 0f, 22.5f));
            lHood.nodes.Add(new Vector3(31.5f, 4f, 22.5f));
            lHood.nodes.Add(new Vector3(31.5f, 6f, 22.5f));
            lHood.nodes.Add(new Vector3(31.5f, 6f, 28.5f));
            lHood.nodes.Add(new Vector3(24.5f, 6f, 28.5f));
            lHood.nodes.Add(new Vector3(24.5f, 14f, 28.5f));
            lHood.grilles.Add((0, new Vector3Int(1, 0, 0), "Kitchen-hood vent"));
            lHood.grilles.Add((1, new Vector3Int(1, 0, 0), "Linen vent"));
            lHood.grilles.Add((4, new Vector3Int(0, -1, 0), "Study vent"));
            lHood.grilles.Add((5, new Vector3Int(0, 1, 0), "Roof vent"));
            c.ducts.Add(lHood);

            c.cornices.Add(1);
            c.cornices.Add(2);
            c.drains.Add(new ArchDrain { at = new Vector2(0, 20.5f), topFloor = 1, poi = "North drainpipe" });
            c.drains.Add(new ArchDrain { at = new Vector2(32.5f, 0), topFloor = 1, poi = "Espalier", kind = ClimbKind.Trellis });
            c.Climb(0, 9f, ClimbKind.Ivy, "West ivy", 2);
            c.Climb(40, 6.5f, ClimbKind.Ivy, "East ivy", 2);
            c.drains.Add(new ArchDrain { at = new Vector2(40, 22.5f), topFloor = 2 });
            c.drains.Add(new ArchDrain { at = new Vector2(8.5f, 32), topFloor = 2 });
            return c;
        }

        /// <summary>The portico (ground) and gallery (F1) ring round the patio void.</summary>
        private static Rect[] PatioRing() => new[]
        {
            Rect.MinMaxRect(10, 21.5f, 30, 24), Rect.MinMaxRect(10, 6, 30, 8.5f),
            Rect.MinMaxRect(10, 8.5f, 12.5f, 21.5f), Rect.MinMaxRect(27.5f, 8.5f, 30, 21.5f)
        };
    }
}
