using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapWeisshorn
    {
        private Vector3 Hp(float x, int f, float z, float up = 0.05f) => _hotelR.At(x, f, z, up);
        private Vector3 _skiJumpTop, _bullwheel, _tiller, _domeTop;

        /// <summary>A chalet: dark timber over a stone ground floor, steep snowy roof; shut (curtains drawn) or lit and open.</summary>
        private ArchSpec Chalet(Vector2 pFront, int pFacing, float pWidth, float pDepth, bool pEnterable, string pRole)
        {
            int k = _chaletCount++;
            ArchStyle lStyle = ArchStyles.Alpine(k % 3 != 0);
            ArchSpec h = Facing("Chalet" + k + (pEnterable ? "_" + pRole : ""), pFront, VILLAGE_Y, pFacing, pWidth, pDepth, lStyle, ZONE_VILLAGE);
            h.shut = !pEnterable;
            h.plinth = 1.2f;
            ArchMat lStone = ArchLib.Surface("stone_wall", 2.5f);
            if (!pEnterable)
            {
                ArchRoom lShut0 = h.Room("Shut0", 0, 0, 0, pWidth, pDepth, RoomType.Bedroom); lShut0.wallMat = lStone; lShut0.noFurniture = true;
                h.Room("Shut1", 1, 0, 0, pWidth, pDepth, RoomType.Bedroom).noFurniture = true;
                h.Door(0, pWidth * 0.5f, 0, DoorKind.Door, 1.2f);
            }
            else
            {
                float lSplit = Mathf.Round(pDepth * 0.55f * 2f) / 2f;
                h.Room("Front", 0, 0, 0, pWidth, lSplit, pRole == "shop" ? RoomType.Shop : pRole == "bar" ? RoomType.Bar : RoomType.Living, SecurityTier.Public).wallMat = lStone;
                h.Room("Back", 0, 0, lSplit, pWidth, pDepth, RoomType.Kitchen, SecurityTier.Restricted).wallMat = lStone;
                h.Door(0, pWidth * 0.5f, 0, DoorKind.Door, 1.4f);
                h.Door(0, pWidth * 0.3f, lSplit);
                h.Door(0, pWidth * 0.75f, pDepth, DoorKind.Door, 1.1f);
                h.Stair(0, 0.9f, pDepth - 1.5f, pWidth - 1.1f, pDepth - 0.45f, 3, false);
                h.Room("Bedroom", 1, 0, 0, pWidth, lSplit, RoomType.Bedroom, SecurityTier.Restricted);
                h.Room("Landing", 1, 0, lSplit, pWidth, pDepth, RoomType.Corridor, SecurityTier.Restricted);
                h.Door(1, pWidth * 0.75f, lSplit);
            }
            h.balconies.Add(new ArchBalcony { floor = 1, at = new Vector2(pWidth * 0.5f, 0), width = pWidth - 2f, depth = 1.2f });
            h.Roof(1, -0.01f, -0.01f, pWidth + 0.01f, pDepth + 0.01f, RoofKind.Gable, false, 36f).overhang = 1.0f;
            if (k % 3 == 0) h.drains.Add(new ArchDrain { at = new Vector2(0.4f, 0), topFloor = 1 });
            _village.Add((h, ZONE_VILLAGE));
            return h;
        }

        private void PlanVillage()
        {
            int lOpen = 0;
            string[] lRoles = { "shop", "home", "bar", "home", "shop" };
            for (float x = -104f; x < 104f; x += 13f)
            {
                if (Mathf.Abs(x + 20f) < 8f || Mathf.Abs(x - 30f) < 14f) continue;
                bool o = ((int)x / 13) % 2 == 0 && lOpen < 10;
                if (o) lOpen++;
                Chalet(new Vector2(x + 5.5f, -144.5f), 2, 11f, 9f, o, lRoles[lOpen % lRoles.Length]);
                Chalet(new Vector2(x + 5.5f, -155.5f), 0, 11f, 9f, false, "");
            }
            // Church and spire, Gasthaus and cellar, ski rental, valley station, boathouse.
            _church = Facing("Kirche", new Vector2(-20f, -118f), VILLAGE_Y, 2, 14f, 22f, ArchStyles.Alpine(false), ZONE_VILLAGE);
            _church.style.facade = ArchLib.Tinted("painted_plaster_wall", new Color(0.97f, 0.96f, 0.93f), "kirche", 3.5f);
            _church.Tall("Nave", 0, 3, 0, 5, 14, 22, RoomType.Church, SecurityTier.Public);
            for (int f = 0; f <= 4; f++) _church.Room("Spire" + f, f, 4, 0, 10, 5, RoomType.Stair, SecurityTier.Restricted).noFurniture = true;
            ArchRoom lBelfry = _church.Room("SpireBelfry", 5, 4, 0, 10, 5, RoomType.Belvedere, SecurityTier.Restricted); lBelfry.arcade = true;
            for (int f = 0; f <= 4; f++) _church.Stair(f, 4.5f, 0.5f, 9.5f, 4.6f, 3, true, true);
            _church.Door(0, 7, 0, DoorKind.Double, 2.2f).poi = "Church";
            _church.Door(0, 7, 5); _church.Door(0, 14, 15, DoorKind.Door, 1.1f).poi = "Church side door";
            _church.drains.Add(new ArchDrain { at = new Vector2(0f, 14f), topFloor = 1, poi = "Church drainpipe" });
            _church.Roof(2, 0, 5, 14, 22, RoofKind.Gable, false, 40f);
            _church.Roof(5, 4, 0, 10, 5, RoofKind.Hip, true, 62f).overhang = 0.3f;
            _village.Add((_church, ZONE_VILLAGE));
            _gasthaus = Facing("Gasthaus", new Vector2(30f, -157f), VILLAGE_Y, 0, 20f, 14f, ArchStyles.Alpine(true), ZONE_VILLAGE);
            ArchSpec g = _gasthaus;
            g.Room("Stube", 0, 0, 0, 14, 14, RoomType.Bar, SecurityTier.Public);
            g.Room("GastKitchen", 0, 14, 0, 20, 8, RoomType.Kitchen, SecurityTier.Restricted, AccessLevel.Staff);
            g.Room("GastStair", 0, 14, 8, 20, 14, RoomType.Store, SecurityTier.Restricted, AccessLevel.Staff).noFurniture = true;
            g.Room("GastCellar", -1, 0, 0, 20, 14, RoomType.WineCellar, SecurityTier.Restricted, AccessLevel.Staff);
            g.Room("GastRooms", 1, 0, 0, 10, 14, RoomType.Bedroom, SecurityTier.Restricted);
            g.Room("Landlord", 1, 10, 0, 20, 14, RoomType.Office, SecurityTier.Restricted, AccessLevel.Staff);
            g.Door(0, 7, 0, DoorKind.Double, 2.2f).poi = "Gasthaus";
            g.Door(0, 14, 4); g.Door(0, 14, 11); g.Door(0, 17, 14, DoorKind.Door, 1.1f);
            g.Door(1, 10, 4);
            g.Door(-1, 20, 7, DoorKind.Door, 1.3f).poi = "Lake culvert";
            g.Stair(-1, 14.5f, 8.5f, 19.5f, 13.6f, 3, true, true);
            g.Stair(0, 14.5f, 8.5f, 19.5f, 13.6f, 3, true, true);
            g.balconies.Add(new ArchBalcony { floor = 1, at = new Vector2(5, 0), width = 7f, depth = 1.4f });
            g.balconies.Add(new ArchBalcony { floor = 1, at = new Vector2(15, 0), width = 7f, depth = 1.4f });
            g.Roof(1, -0.01f, -0.01f, 20.01f, 14.01f, RoofKind.Gable, true, 34f).overhang = 1.2f;
            _village.Add((g, ZONE_VILLAGE));
            _rental = Chalet(new Vector2(-62f, -144.5f), 2, 12f, 9f, true, "shop");
            _valley = Facing("ValleyStation", new Vector2(-2f, -130f), VILLAGE_Y, 2, 12f, 10f, ArchStyles.Alpine(false), ZONE_VILLAGE);
            _valley.Room("Waiting", 0, 0, 0, 12, 10, RoomType.Lobby, SecurityTier.Public);
            _valley.Door(0, 6, 0, DoorKind.Double, 2.4f).poi = "Funicular valley station";
            _valley.Door(0, 6, 10, DoorKind.Doorway, 3f);
            _valley.Roof(0, -0.01f, -0.01f, 12.01f, 10.01f, RoofKind.Gable, true, 30f).overhang = 1f;
            _village.Add((_valley, ZONE_VILLAGE));
            _boathouse = Facing("Boathouse", new Vector2(-150f, -160f), VILLAGE_Y, 2, 10f, 7f, ArchStyles.Timber("grey_roof_tiles"), ZONE_VILLAGE);
            _boathouse.style.snow = true;
            _boathouse.Room("Boats", 0, 0, 0, 10, 7, RoomType.Workshop, SecurityTier.Restricted);
            _boathouse.Door(0, 5, 0, DoorKind.Doorway, 3.4f).poi = "Boathouse";
            _boathouse.Door(0, 10, 4, DoorKind.Door, 1.1f);
            _boathouse.Roof(0, -0.01f, -0.01f, 10.01f, 7.01f, RoofKind.Gable, false, 32f);
            _village.Add((_boathouse, ZONE_VILLAGE));
        }

        private void PlanSki()
        {
            ArchSpec Hut(string pName, Vector2 pFront, int pFacing, float w, float d, RoomType pType, int pFloors, SecurityTier pTier, AccessLevel pAccess)
            {
                ArchSpec s = Facing(pName, pFront, Shape.Height(pFront.x, pFront.y), pFacing, w, d, ArchStyles.Alpine(true), ZONE_SKI);
                s.Room(pName + "Room", 0, 0, 0, w, d, pType, pTier, pAccess);
                if (pFloors > 1) s.Room(pName + "Upper", 1, 0, 0, w, d, RoomType.Store, pTier, pAccess);
                s.Door(0, w * 0.5f, 0, DoorKind.Double, 2f);
                s.Door(0, w, d * 0.5f, DoorKind.Door, 1.1f);
                s.Roof(pFloors - 1, -0.01f, -0.01f, w + 0.01f, d + 0.01f, RoofKind.Gable, w > d, 34f).overhang = 0.9f;
                Lot(s, 2.5f, Ground.Dirt, 3f);
                return s;
            }
            _midStation = Hut("MidStation", MIDSTATION, 2, 14f, 9f, RoomType.Bar, 1, SecurityTier.Public, AccessLevel.None);
            _midStation.Door(0, 4f, 0, DoorKind.Door, 1.2f);
            _liftBottom = Hut("LiftBottom", LIFT_BOTTOM, 2, 10f, 8f, RoomType.Workshop, 1, SecurityTier.Restricted, AccessLevel.Staff);
            _liftTop = Hut("LiftTop", LIFT_TOP, 2, 9f, 7f, RoomType.Workshop, 1, SecurityTier.Restricted, AccessLevel.Staff);
            _garage = Hut("GroomerGarage", GARAGE, 3, 16f, 12f, RoomType.Garage, 1, SecurityTier.Restricted, AccessLevel.Staff);
            _garage.doors[0].kind = DoorKind.Doorway; _garage.doors[0].width = 5f; _garage.doors[0].height = 3.3f;
            _garage.Room("GarageMine", -1, 2, 2, 14, 10, RoomType.Cellar, SecurityTier.Restricted, AccessLevel.Staff);
            _garage.Stair(-1, 9f, 2.5f, 13.6f, 9.5f, 0, true, true);
            _garage.Door(-1, 2, 6, DoorKind.Door, 1.4f).poi = "Mine gallery";
            _mineHut = Hut("MineHut", new Vector2(120f, 30f), 3, 6f, 5f, RoomType.Bedroom, 1, SecurityTier.Restricted, AccessLevel.None);
            _observatory = Facing("Observatory", OBSERVATORY + new Vector2(0, -8f), SUMMIT_Y, 2, 18f, 14f, ArchStyles.Industrial("concrete_wall_008", "metal_plate"), ZONE_SKI);
            ArchSpec o = _observatory;
            o.style.snow = true;
            o.Room("DomeHall", 0, 0, 0, 18, 9, RoomType.Hall, SecurityTier.Hostile, AccessLevel.Security);
            o.Room("ObsLab", 0, 0, 9, 9, 14, RoomType.Office, SecurityTier.Hostile, AccessLevel.Security);
            o.Room("ObsBunks", 0, 9, 9, 18, 14, RoomType.Barracks, SecurityTier.Hostile, AccessLevel.Security);
            o.Room("Bunker1", -1, 0, 0, 18, 14, RoomType.Security, SecurityTier.Hostile, AccessLevel.Security);
            o.Room("Command", -2, 0, 0, 18, 14, RoomType.Office, SecurityTier.Hostile, AccessLevel.Private);
            o.Door(0, 9, 0, DoorKind.Door, 1.6f).poi = "Observatory";
            o.Door(0, 4.5f, 9); o.Door(0, 13.5f, 9);
            o.Stair(-1, 13.5f, 0.6f, 17.4f, 5.6f, 0, true, true);
            o.Stair(-2, 2f, 2f, 4.4f, 8f, 0, false);
            o.Secure(-1, 9, 7, SecureDoor.Lock.Keycard, true);
            o.Door(-2, 0, 11, DoorKind.Door, 1.3f).poi = "Escape tunnel";
            Lot(o, 4f, Ground.Asphalt);
            _antenna = Facing("AntennaHut", OBSERVATORY + new Vector2(-24f, 6f), SUMMIT_Y, 1, 6f, 5f, ArchStyles.Timber("corrugated_iron_02"), ZONE_SKI);
            _antenna.style.snow = true;
            _antenna.Room("AntennaRoom", 0, 0, 0, 6, 5, RoomType.Radio, SecurityTier.Hostile, AccessLevel.Security);
            _antenna.Door(0, 3, 0, DoorKind.Door, 1.1f);
            _antenna.Roof(0, -0.01f, -0.01f, 6.01f, 5.01f, RoofKind.Shed, true, 14f);
            Lot(_antenna, 2f, Ground.Dirt);
            _cableTop = Facing("CableCarTop", OBSERVATORY + new Vector2(22f, -18f), SUMMIT_Y, 2, 8f, 6f, ArchStyles.Industrial(), ZONE_SKI);
            _cableTop.style.snow = true;
            _cableTop.Room("CableTopRoom", 0, 0, 0, 8, 6, RoomType.Workshop, SecurityTier.Hostile, AccessLevel.Security);
            _cableTop.Door(0, 4, 0, DoorKind.Doorway, 2.4f).poi = "Cable car top";
            _cableTop.Door(0, 8, 3, DoorKind.Door, 1.1f);
            Lot(_cableTop, 2f, Ground.Asphalt);
        }
    }
}
