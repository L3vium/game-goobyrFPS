using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapQasrAlRimal
    {
        private static readonly Color[] OCHRES = { new Color(0.95f, 0.82f, 0.62f), new Color(0.9f, 0.74f, 0.55f), new Color(0.98f, 0.9f, 0.76f), new Color(0.86f, 0.66f, 0.48f), new Color(0.97f, 0.86f, 0.7f) };

        private Vector3 Kp(float x, int f, float z, float up = 0.05f) => _keepR.At(x, f, z, up);

        /// <summary>A flat-roofed adobe house (shut and chained, or open: shop or home with a roof terrace).</summary>
        private ArchSpec House(Vector2 pFront, int pFacing, float pWidth, float pDepth, int pFloors, bool pEnterable, string pRole)
        {
            int k = _houseCount++;
            ArchStyle lStyle = ArchStyles.Adobe(OCHRES[k % OCHRES.Length], "h" + (k % OCHRES.Length));
            ArchSpec h = Facing("House" + k + (pEnterable ? "_" + pRole : ""), pFront, TOWN_Y, pFacing, pWidth, pDepth, lStyle, ZONE_TOWN);
            h.shut = !pEnterable;
            h.plinth = 1.2f;
            if (!pEnterable)
            {
                for (int f = 0; f < pFloors; f++) h.Room("Shut" + f, f, 0, 0, pWidth, pDepth, RoomType.Bedroom).noFurniture = true;
                h.Door(0, pWidth * 0.5f, 0, DoorKind.Door, 1.2f);
            }
            else
            {
                float lSplit = Mathf.Round(pDepth * 0.55f * 2f) / 2f;
                h.Room("Front", 0, 0, 0, pWidth, lSplit, pRole == "shop" ? RoomType.Shop : pRole == "cafe" ? RoomType.Bar : RoomType.Living, SecurityTier.Public);
                h.Room("Back", 0, 0, lSplit, pWidth, pDepth, RoomType.Kitchen, SecurityTier.Restricted);
                h.Door(0, pWidth * 0.5f, 0, pRole == "shop" ? DoorKind.Double : DoorKind.Door, pRole == "shop" ? 2.0f : 1.2f);
                h.Door(0, pWidth * 0.3f, lSplit);
                h.Door(0, pWidth * 0.75f, pDepth, DoorKind.Door, 1.1f);
                if (pFloors > 1)
                {
                    h.Stair(0, 0.9f, pDepth - 1.5f, pWidth - 1.1f, pDepth - 0.45f, 3, false);
                    h.Room("Upper", 1, 0, 0, pWidth, lSplit, RoomType.Bedroom, SecurityTier.Restricted);
                    h.Room("Landing", 1, 0, lSplit, pWidth, pDepth, RoomType.Corridor, SecurityTier.Restricted);
                    h.Door(1, pWidth * 0.75f, lSplit);
                }
            }
            ArchRoom lRoof = h.Room("Roof", pFloors, 0, 0, pWidth, pDepth, RoomType.Terrace, SecurityTier.Public);
            lRoof.open = true;
            if (pEnterable) h.holes.Add((pFloors, new Rect(0.6f, pDepth * 0.55f + 0.6f, 1f, 1f)));
            if (k % 4 == 0) h.drains.Add(new ArchDrain { at = new Vector2(0.35f, 0), topFloor = pFloors - 1 });
            _town.Add((h, ZONE_TOWN));
            return h;
        }

        private void PlanTown()
        {
            int lOpen = 0;
            bool Open(int i) => i % 3 == 0 && lOpen < 16;
            string[] lRoles = { "shop", "home", "cafe", "shop", "home", "shop" };
            // Souk streets (x = 2 and x = 40), both sides, framing the keep to the north.
            foreach (float lStreet in new[] { 2f, 40f })
                for (int s = 0; s < 2; s++)
                {
                    float z = -133f;
                    for (int i = 0; z < -106f; i++)
                    {
                        float w = 6f + (i % 2) * 1.5f;
                        bool lEnter = Open(i + s + (int)lStreet);
                        if (lEnter) lOpen++;
                        House(new Vector2(lStreet + (s == 0 ? -3.4f : 3.4f), z + w * 0.5f), s == 0 ? 1 : 3, w, 8f, 2, lEnter, lRoles[lOpen % lRoles.Length]);
                        z += w + 0.3f;
                    }
                }
            // Plaza north side between the souk mouths, south side, main street west and east.
            for (int i = 0; i < 2; i++) House(new Vector2(17f + i * 7.5f, -133.3f), 2, 7f, 9f, 2, i == 0, "cafe");
            for (float x = 4f; x < 37f; x += 8.4f) { bool o = Open((int)x); if (o) lOpen++; House(new Vector2(x + 4f, -166.8f), 0, 8f, 9f, 2, o, "home"); }
            for (float x = -58f; x < -26f; x += 8.4f)
            {
                House(new Vector2(x + 4f, -146f), 2, 8f, 8f, 2, false, "");
                bool o = Open((int)-x); if (o) lOpen++;
                House(new Vector2(x + 4f, -154f), 0, 8f, 8f, 2, o, "shop");
            }
            for (float x = 56f; x < 116f; x += 8.6f)
            {
                bool o = Open((int)x); if (o) lOpen++;
                House(new Vector2(x + 4f, -146f), 2, 8f, 8f, 2, o, "shop");
                House(new Vector2(x + 4f, -154f), 0, 8f, 8f, 2, false, "");
            }
            for (int i = 0; i < 4; i++) House(new Vector2(-80f + i * 9.5f, -178f), 2, 9f, 8f, 1, false, "");
            Debug.Log($"[Qasr] {lOpen} enterable houses");

            // Tea house (plaza east), mosque court and minaret (plaza west), caravanserai, bus station.
            _teaHouse = Facing("TeaHouse", new Vector2(PLAZA.x + 18.5f, PLAZA.y), TOWN_Y, 3, 14f, 13f, ArchStyles.Adobe(new Color(0.96f, 0.85f, 0.66f), "tea"), ZONE_TOWN);
            ArchSpec t = _teaHouse;
            t.Room("TeaRoom", 0, 0, 0, 14, 9, RoomType.Bar, SecurityTier.Public);
            t.Room("TeaKitchen", 0, 0, 9, 8, 13, RoomType.Kitchen, SecurityTier.Restricted, AccessLevel.Staff);
            t.Room("TeaBackRoom", 0, 8, 9, 14, 13, RoomType.Bedroom, SecurityTier.Restricted);
            t.Door(0, 7, 0, DoorKind.Double, 2.6f).poi = "Tea house";
            t.Door(0, 4, 9); t.Door(0, 11, 9); t.Door(0, 14, 11, DoorKind.Door, 1.1f);
            ArchRoom lTeaRoof = t.Room("TeaRoof", 1, 0, 0, 14, 13, RoomType.Terrace, SecurityTier.Public); lTeaRoof.open = true;
            _town.Add((t, ZONE_TOWN));
            _mosque = Facing("Mosque", new Vector2(PLAZA.x - 18.5f, PLAZA.y), TOWN_Y, 1, 22f, 18f, ArchStyles.Adobe(new Color(0.99f, 0.95f, 0.88f), "mosque", false), ZONE_TOWN);
            ArchSpec m = _mosque;
            m.Tall("PrayerHall", 0, 2, 0, 6, 22, 18, RoomType.Church, SecurityTier.Public);
            ArchRoom lCourt = m.Room("MosqueCourt", 0, 0, 0, 16, 6, RoomType.Terrace, SecurityTier.Public); lCourt.open = true;
            for (int f = 0; f <= 5; f++) m.Room("Minaret" + f, f, 16, 0, 22, 6, RoomType.Stair, SecurityTier.Restricted).noFurniture = true;
            ArchRoom lBalcony = m.Room("MinaretGallery", 6, 16, 0, 22, 6, RoomType.Belvedere, SecurityTier.Restricted); lBalcony.arcade = true;
            for (int f = 0; f <= 5; f++) m.Stair(f, 16.6f, 1.6f, 21.4f, 5.9f, 0, true, true);
            m.Door(0, 8, 6, DoorKind.Double, 3f).poi = "Prayer hall"; m.Door(0, 16, 3); m.Door(0, 8, 18, DoorKind.Door, 1.2f).poi = "Mosque back door";
            m.drains.Add(new ArchDrain { at = new Vector2(0f, 12f), topFloor = 1, poi = "Mosque drainpipe" });
            m.Roof(6, 16, 0, 22, 6, RoofKind.Hip, true, 40f);
            m.drains.Add(new ArchDrain { at = new Vector2(22f, 3f), topFloor = 5, poi = "Minaret drainpipe" });
            _town.Add((m, ZONE_TOWN));
            _serai = new ArchSpec { name = "Caravanserai", origin = new Vector3(-138f, TOWN_Y, -164f), size = new Vector2(30, 30), style = ArchStyles.Adobe(new Color(0.9f, 0.76f, 0.58f), "serai"), zone = ZONE_TOWN };
            ArchSpec c = _serai;
            ArchRoom lYard = c.Room("SeraiYard", 0, 7, 7, 23, 23, RoomType.Terrace, SecurityTier.Public); lYard.open = true;
            Rect[] lRing = { Rect.MinMaxRect(4, 4, 26, 7), Rect.MinMaxRect(4, 23, 26, 26), Rect.MinMaxRect(4, 7, 7, 23), Rect.MinMaxRect(23, 7, 26, 23) };
            for (int f = 0; f < 2; f++) foreach (Rect r in lRing) { ArchRoom g = c.Room("SeraiGallery" + f, f, r.xMin, r.yMin, r.xMax, r.yMax, RoomType.Gallery, SecurityTier.Public); g.arcade = true; }
            for (int f = 0; f < 2; f++)
            {
                c.Room("SeraiS" + f, f, 0, 0, 30, 4, f == 0 ? RoomType.Store : RoomType.Dormitory, SecurityTier.Public);
                c.Room("SeraiN" + f, f, 0, 26, 30, 30, f == 0 ? RoomType.Garage : RoomType.Dormitory, SecurityTier.Public);
                c.Room("SeraiW" + f, f, 0, 4, 4, 26, f == 0 ? RoomType.Shop : RoomType.Bedroom, SecurityTier.Public);
                c.Room("SeraiE" + f, f, 26, 4, 30, 26, f == 0 ? RoomType.Mess : RoomType.Bedroom, SecurityTier.Public);
                c.Door(f, 15, 4); c.Door(f, 15, 26); c.Door(f, 4, 15); c.Door(f, 26, 15);
            }
            c.Door(0, 30, 15, DoorKind.Doorway, 4f).poi = "Caravanserai gate";
            c.Door(0, 15, 30, DoorKind.Double, 3f).poi = "Stable door";
            c.Stair(0, 23.6f, 23.3f, 25.8f, 25.9f, 3, false);
            c.drains.Add(new ArchDrain { at = new Vector2(30f, 3f), topFloor = 1, poi = "Caravanserai drainpipe" });
            c.drains.Add(new ArchDrain { at = new Vector2(0f, 27f), topFloor = 1, poi = "Caravanserai drainpipe" });
            ArchRoom lSeraiRoof = c.Room("SeraiRoof", 2, 0, 0, 30, 30, RoomType.Terrace, SecurityTier.Public); lSeraiRoof.open = true;
            c.holes.Add((2, new Rect(7, 7, 16, 16)));
            _town.Add((c, ZONE_TOWN));
            _busStation = Facing("BusStation", new Vector2(130f, -186f), TOWN_Y, 3, 10f, 8f, ArchStyles.Adobe(new Color(0.85f, 0.9f, 0.92f), "bus"), ZONE_TOWN);
            _busStation.Room("Ticket", 0, 0, 0, 10, 8, RoomType.Office, SecurityTier.Public);
            _busStation.Door(0, 5, 0, DoorKind.Double, 2.4f).poi = "Bus station";
            _busStation.Door(0, 5, 8, DoorKind.Door, 1.1f);
            ArchRoom lBusRoof = _busStation.Room("BusRoof", 1, 0, 0, 10, 8, RoomType.Terrace, SecurityTier.Public); lBusRoof.open = true;
            _town.Add((_busStation, ZONE_TOWN));
        }

        private void PlanGrove()
        {
            _press = Facing("DatePress", new Vector2(62f, -66f), Shape.Height(62f, -70f), 2, 18f, 12f, ArchStyles.Adobe(new Color(0.88f, 0.72f, 0.54f), "press", false), ZONE_GROVE);
            ArchSpec p = _press;
            p.Tall("PressFloor", 0, 2, 0, 0, 12, 12, RoomType.Workshop, SecurityTier.Restricted, AccessLevel.Staff);
            p.Room("PressOffice", 0, 12, 0, 18, 6, RoomType.Office, SecurityTier.Restricted, AccessLevel.Staff);
            p.Room("PressStore", 0, 12, 6, 18, 12, RoomType.Store, SecurityTier.Restricted, AccessLevel.Staff);
            p.Room("PressLoft", 1, 12, 0, 18, 12, RoomType.Attic, SecurityTier.Restricted, AccessLevel.Staff);
            p.Door(0, 6, 0, DoorKind.Double, 3f).poi = "Date press";
            p.Door(0, 12, 3); p.Door(0, 15, 6); p.Door(0, 18, 9, DoorKind.Double, 2.2f).poi = "Press loading door";
            p.Door(0, 6, 12, DoorKind.Door, 1.2f);
            p.Stair(0, 12.6f, 6.6f, 17.4f, 11.6f, 2, true, true);
            p.Window(1, 18, 3, WindowKind.Open).poi = "Loft hatch";
            p.Roof(1, 0, 0, 18, 12, RoofKind.Flat);
            Lot(p, 3f, Ground.Dirt);
            _kasbah = Facing("Kasbah", new Vector2(KASBAH.x, KASBAH.y - 6f), 18f, 2, 14f, 12f, ArchStyles.Adobe(new Color(0.78f, 0.6f, 0.45f), "kasbah", false), ZONE_GROVE);
            ArchSpec ks = _kasbah;
            ks.Room("KasbahHall", 0, 0, 0, 14, 7, RoomType.Hall, SecurityTier.Restricted);
            ks.Room("KasbahStore", 0, 0, 7, 14, 12, RoomType.Store, SecurityTier.Restricted);
            ks.Room("KasbahUpper", 1, 0, 0, 14, 12, RoomType.Attic, SecurityTier.Restricted);
            ArchRoom lKRoof = ks.Room("KasbahRoof", 2, 0, 0, 14, 12, RoomType.Terrace, SecurityTier.Restricted); lKRoof.open = true;
            ks.Door(0, 7, 0, DoorKind.Doorway, 1.6f).poi = "Kasbah";
            ks.Door(0, 4, 7); ks.Door(0, 14, 9.5f, DoorKind.Doorway, 1.2f);
            ks.Stair(0, 9.6f, 7.6f, 13.4f, 11.6f, 3, true, true);
            ks.Stair(1, 9.6f, 7.6f, 13.4f, 11.6f, 3, true, true);
            _baseStation = Facing("BaseStation", new Vector2(-96f, 104f), Shape.Height(-96f, 108f), 3, 8f, 7f, ArchStyles.Industrial(), ZONE_GROVE);
            _baseStation.Room("LiftHouse", 0, 0, 0, 8, 7, RoomType.Workshop, SecurityTier.Restricted, AccessLevel.Staff);
            _baseStation.Door(0, 4, 0, DoorKind.Door, 1.4f).poi = "Goods lift base";
            _baseStation.Secure(0, 4, 7, SecureDoor.Lock.Keycard, false, "Goods lift");
            Lot(_baseStation, 3f, Ground.Dirt);
            // Radar bunker on the Little Mesa.
            _bunker = Facing("RadarBunker", new Vector2(150f, 96f), LITTLE_Y, 2, 18f, 12f, ArchStyles.Industrial("concrete_wall_008", "concrete_floor_02"), ZONE_GROVE);
            ArchSpec b = _bunker;
            b.Room("BunkerHall", 0, 0, 0, 18, 6, RoomType.Security, SecurityTier.Hostile, AccessLevel.Security);
            b.Room("Consoles", 0, 0, 6, 10, 12, RoomType.Radio, SecurityTier.Hostile, AccessLevel.Security);
            b.Room("BunkerBunks", 0, 10, 6, 18, 12, RoomType.Barracks, SecurityTier.Hostile, AccessLevel.Security);
            b.Room("BunkerCellar", -1, 0, 0, 18, 12, RoomType.Store, SecurityTier.Hostile, AccessLevel.Security);
            b.Door(0, 9, 0, DoorKind.Door, 1.6f).poi = "Bunker";
            b.Door(0, 5, 6); b.Door(0, 14, 6); b.Door(0, 10, 9);
            b.Door(-1, 0, 9, DoorKind.Door, 1.4f).poi = "Smugglers' cleft";
            b.Stair(-1, 13.6f, 0.6f, 17.4f, 5.4f, 0, true, true);
            Lot(b, 3f, Ground.Asphalt);
        }

        /// <summary>A range of rooms behind an arcaded gallery, along a court wall (stables, barracks, waiting rooms).</summary>
        private ArchSpec CourtRange(string pName, Vector2 pFront, int pFacing, float pLength, int pFloors, RoomType pType, SecurityTier pTier, AccessLevel pAccess)
        {
            ArchSpec r = Facing(pName, pFront, MESA_Y, pFacing, pLength, 8f, ArchStyles.Fortress(), ZONE_CITADEL);
            r.style.windowSpacing = 5f;
            int lBays = Mathf.Max(1, Mathf.RoundToInt(pLength / 9f));
            for (int f = 0; f < pFloors; f++)
            {
                ArchRoom g = r.Room(pName + "Gallery", f, 0, 0, pLength, 2.5f, RoomType.Gallery, pTier, pAccess);
                g.arcade = true;
                for (int k = 0; k < lBays; k++)
                {
                    float x0 = Mathf.Round(pLength * k / lBays * 2f) / 2f, x1 = Mathf.Round(pLength * (k + 1) / lBays * 2f) / 2f;
                    r.Room(pName + f + "_" + k, f, x0, 2.5f, x1, 8f, pType, pTier, pAccess);
                    r.Door(f, (x0 + x1) * 0.5f, 2.5f);
                }
            }
            if (pFloors > 1) r.Stair(0, pLength - 6.6f, 0.3f, pLength - 0.8f, 2.2f, 1, false);
            ArchRoom lTop = r.Room(pName + "Roof", pFloors, 0, 2.5f, pLength, 8f, RoomType.Terrace, SecurityTier.Restricted);
            lTop.open = true;
            _ranges.Add(r);
            return r;
        }

        private readonly List<ArchSpec> _ranges = new List<ArchSpec>();

        private void PlanCitadel()
        {
            CourtRange("Stables", new Vector2(-62f, 126f), 1, 26f, 1, RoomType.Garage, SecurityTier.Restricted, AccessLevel.Staff);
            CourtRange("Barracks", new Vector2(48f, 113f), 3, 24f, 2, RoomType.Barracks, SecurityTier.Restricted, AccessLevel.Staff);
            CourtRange("WaitingWest", new Vector2(-36f, 70f), 2, 28f, 1, RoomType.Hall, SecurityTier.Restricted, AccessLevel.Guest);
            CourtRange("WaitingEast", new Vector2(28f, 70f), 2, 28f, 1, RoomType.Mess, SecurityTier.Restricted, AccessLevel.Guest);
            _keep = KeepSpec(KEEP);
            Lot(_keep, 2f, Ground.Cobble);
            Vector2[] lCorners = { new Vector2(CITADEL.xMin, CITADEL.yMin), new Vector2(CITADEL.xMax, CITADEL.yMin), new Vector2(CITADEL.xMax, CITADEL.yMax), new Vector2(CITADEL.xMin, CITADEL.yMax) };
            for (int i = 0; i < 4; i++)
            {
                ArchSpec t = new ArchSpec { name = "Bastion" + i, origin = new Vector3(lCorners[i].x - 5f, MESA_Y, lCorners[i].y - 5f), size = new Vector2(10, 10), style = ArchStyles.Fortress(), zone = ZONE_CITADEL };
                t.Room("BastionGuard", 0, 0, 0, 10, 10, RoomType.GuardRoom, SecurityTier.Restricted);
                t.Room("BastionUpper", 1, 0, 0, 10, 10, RoomType.Store, SecurityTier.Restricted);
                ArchRoom lTop = t.Room("BastionTop", 2, 0, 0, 10, 10, RoomType.Terrace, SecurityTier.Restricted); lTop.open = true;
                float dx = i == 0 || i == 3 ? 10f : 0f, dz = i < 2 ? 10f : 0f;
                t.Door(0, dx > 0 ? 7 : 3, dz > 0 ? 10 : 0, DoorKind.Door, 1.4f);
                t.Stair(0, 1, 1, 5.8f, 6.8f, 0, true, true);
                t.Stair(1, 1, 1, 5.8f, 6.8f, 0, true, true);
                _bastionSpecs.Add(t);
            }
        }
    }
}
