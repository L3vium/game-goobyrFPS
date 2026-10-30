using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The Hilltop Mansion (zone 2): the Whitlock estate on a walled plateau eighteen metres above
    /// the town, its south face a terrace wall. Public: the golf course and the orchard below.
    /// Restricted: the drive past the gatehouse, the forecourt, the formal gardens, the hedge maze,
    /// the pool, the tennis court, the service yard. VIP: the mansion's second floor (the
    /// senator's office, the master suite, the security room) and the basement vault. Ways in:
    /// the guarded front doors under the portico, the kitchen service door from the service yard,
    /// the basement door at the bottom of the service ramp, the east door from the pool terrace,
    /// the portico balcony and the back balcony (east drainpipe, trellis), the roof hatch (and the
    /// zipline off it), the terrace wall's garden stair and its ivy, the service gate and the gap in
    /// the north wall. Staging: the senator's evening reception in the ballroom (a speech, the bar,
    /// the chandelier), the garden party on the back terrace, Preston's pool party, a tennis game.
    /// </summary>
    public partial class MapSuburb
    {
        private static readonly Vector3 MANSION = new Vector3(-250f, HILL + 0.05f, 332f);
        private static readonly Color MANSION_WALL = new Color(0.97f, 0.96f, 0.93f);
        private static readonly Color MANSION_ROOF = new Color(0.26f, 0.28f, 0.33f);
        private static readonly Color ESTATE_STONE = new Color(0.84f, 0.8f, 0.72f);
        private static readonly Color BOXWOOD = new Color(0.27f, 0.45f, 0.24f);

        private void PlanMansion()
        {
            _zoneStage.Add(() => Flatten(PLATEAU, HILL, 0f));
            // The basement ramp: a sunken lane from the service yard down to the basement doors (west wall).
            _carveStage.Add(() => Cells(R(-300, 324, -268, 340), (i, j) =>
            {
                float x = Cx(i);
                _h[i, j] = x > -276f ? HILL - MapKit.STOREY : Mathf.Lerp(HILL, HILL - MapKit.STOREY, (x + 300f) / 24f);
                _f[i, j] |= BUILT;
                _paint[i, j] = CONCRETE;
            }));
            Road(7f, ASPHALT, new Vector2(ASH_X, OAK_Z), new Vector2(-214, 176), new Vector2(-188, 204), new Vector2(-172, 232), new Vector2(-178, 254), new Vector2(-182, 276), new Vector2(-200, 290), new Vector2(-232, 296));
            Lane(5f, GRAVEL, new Vector2(-366, 300), new Vector2(-392, 272), new Vector2(-410, 200), new Vector2(-404, 120), new Vector2(-392, 56));
            PaintDisc(new Vector2(-250, 302), 16f, GRAVEL);
            PaintDisc(new Vector2(-250, 302), 7f, LAWN, 0);
            PaintRect(R(-256, 316, -244, 320), STONE);
            PaintRect(R(-232, 344, -268, 356), STONE);
            PaintRect(R(-300, 296, -268, 352), GRAVEL);
            PaintRect(R(-212, 324, -180, 346), STONE);
            PaintRect(R(-184, 392, -156, 408), new Color(0.3f, 0.5f, 0.55f));
            PaintDisc(new Vector2(-255, 402), 9f, CONCRETE);
            _tame.Add(Expand(PLATEAU, 2f));
            _tame.Add(R(-372, 170, -205, 244));

            MapKit.KitSpec m = Lot("Whitlock Mansion", "Classic", MANSION.x, MANSION.z, 0, 12, 8, 3, Roof.Flat, MANSION_WALL, MANSION_ROOF, 2, HILL, 4f);
            Doors(m, 0, 5, 0, 6, 2, 5, 2, 6, 3, 2, 1, 4);
            m.basement = true;
            m.basementDoors.Add(new Vector2Int(3, 3));
            m.basementDoors.Add(new Vector2Int(3, 4));
            m.upperDoors.Add(new Vector3Int(0, 5, 1));
            m.upperDoors.Add(new Vector3Int(0, 6, 1));
            m.upperDoors.Add(new Vector3Int(2, 3, 2));
            m.tallWindows = true;
            m.windowChance = 0.85f;
            m.balconyChance = 0.25f;
            m.climb = new Vector2Int(1, 2);
            m.interiorWall = new Color(0.95f, 0.92f, 0.85f);
            m.floorColor = new Color(0.5f, 0.36f, 0.26f);

            MapKit.KitSpec gate = Lot("Whitlock Gatehouse", "Classic", -190f, 258f, 0, 2, 2, 1, Roof.Hip, ESTATE_STONE, MANSION_ROOF, 2, HILL, 3f);
            Doors(gate, 1, 0);
            gate.stairs = false;
            gate.rooms.Add(new[] { Room.Security });
            MapKit.KitSpec pool = Lot("Whitlock Pool House", "Suburb", -196f, 356f, 180, 4, 3, 1, Roof.Flat, Color.white, FLAT_ROOF, 2, HILL, 3f);
            Doors(pool, 0, 1, 0, 2, 2, 1);
            pool.windowChance = 1f;
            pool.tallWindows = true;
            Floors(pool, Room.Bar, Room.Bathroom);
            MapKit.KitSpec green = Lot("Whitlock Greenhouse", "Classic", -330f, 330f, 90, 4, 3, 1, Roof.Gable, new Color(0.88f, 0.96f, 0.92f), new Color(0.75f, 0.9f, 0.9f), 2, HILL, 3f);
            Doors(green, 0, 1, 2, 2);
            green.windowChance = 1f;
            green.tallWindows = true;
            green.balconyChance = 0f;
            MapKit.KitSpec garage = Lot("Whitlock Garage", "Classic", -352f, 352f, 270, 4, 3, 1, Roof.Hip, ESTATE_STONE, MANSION_ROOF, 2, HILL, 3f);
            garage.arches.Add(new Vector2Int(0, 1));
            garage.arches.Add(new Vector2Int(0, 2));
            Doors(garage, 2, 2);
            garage.windowChance = 0.3f;
            MapKit.KitSpec staff = Lot("Whitlock Staff Quarters", "Suburb", -345f, 296f, 90, 5, 3, 2, Roof.Gable, new Color(0.92f, 0.88f, 0.8f), MANSION_ROOF, 2, HILL, 3f);
            Doors(staff, 0, 2, 2, 2);
            Floors(staff, Room.Kitchen, Room.Lounge, Room.Bedroom, Room.Bathroom);
            MapKit.KitSpec cottage = Lot("Whitlock Guest Cottage", "Suburb", -215f, 404f, 180, 4, 3, 2, Roof.Gable, new Color(0.86f, 0.9f, 0.95f), MANSION_ROOF, 2, HILL, 3f);
            Doors(cottage, 0, 1, 2, 2);
            cottage.balconyChance = 0.3f;
            Floors(cottage, Room.Lounge, Room.Bathroom, Room.Bedroom, Room.Office);
        }

        private void Mansion()
        {
            const int Z = 2;
            K.Hole(R(-267.95f, 320.05f, -232.05f, 343.95f));
            MapKit.KitBuilding M = K.Building(Spec("Whitlock Mansion"));
            MansionInterior(M);
            MansionOutside(M);
            Estate();
            Gardens();
            Maze();
            PoolAndCourt();
            Outbuildings();

            Walk(Z, ItemKind.Smg, 4f, new Vector2(-360, 262), new Vector2(-150, 262), new Vector2(-150, 422), new Vector2(-360, 422));
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(-330, 268), new Vector2(-270, 268), new Vector2(-270, 312), new Vector2(-330, 312));
            Walk(Z, ItemKind.Shotgun, 4f, new Vector2(-225, 268), new Vector2(-165, 268), new Vector2(-165, 312), new Vector2(-225, 312));
            Walk(Z, ItemKind.Pistol, 5f, new Vector2(-214, 180), new Vector2(-186, 208), new Vector2(-174, 236), new Vector2(-180, 252));
            K.Zone(Z, K.Map.zoneNames[2], new Vector3(-256, 25, 340), new Vector3(240, 80, 200), G(ASH_X, OAK_Z, 0.1f), new Color(0.2f, 0.22f, 0.32f), HatStyle.TopHat,
                M.Local(12f, -MapKit.STOREY + 0.1f, -6f));
        }

        // ---------------------------------------------------------------- the house

        private void MansionInterior(MapKit.KitBuilding M)
        {
            const int Z = 2;
            Vector3 Loc(float x, int f, float z) => M.Local(x, f * MapKit.STOREY, z);
            System.Random r = new System.Random(2024);
            Color lWood = new Color(0.42f, 0.28f, 0.2f), lGold = new Color(0.85f, 0.68f, 0.35f), lVelvet = new Color(0.45f, 0.12f, 0.16f);

            // The same frame on every floor: three bays across, front and back rooms.
            for (int f = -1; f < 3; f++)
            {
                K.Sprinklers(Loc(0, f, 0), new Vector3(M.W, MapKit.STOREY, M.D), M.Yaw, MapKit.STOREY - 0.05f);
                if (f < 0) continue;
                K.PartitionKit(M, f, new Vector3(-18, 0, 0), new Vector3(-6, 0, 0), 1);
                K.PartitionKit(M, f, new Vector3(-6, 0, 0), new Vector3(0, 0, 0), 0);
                K.PartitionKit(M, f, new Vector3(0, 0, 0), new Vector3(6, 0, 0), 1);
                K.PartitionKit(M, f, new Vector3(6, 0, 0), new Vector3(15, 0, 0), 1);
                K.PartitionKit(M, f, new Vector3(-6, 0, -12), new Vector3(-6, 0, 0), 2);
                K.PartitionKit(M, f, new Vector3(6, 0, -12), new Vector3(6, 0, -6), 1);
                K.PartitionKit(M, f, new Vector3(6, 0, -6), new Vector3(6, 0, 0), 0);
                K.PartitionKit(M, f, new Vector3(-6, 0, 0), new Vector3(-6, 0, 12), 1);
                K.PartitionKit(M, f, new Vector3(6, 0, 0), new Vector3(6, 0, 12), 1);
                for (int i = 0; i < 3; i++)
                {
                    K.Lamp(Loc(-12f + i * 12f, f, -6f) + Vector3.up * (MapKit.STOREY - 0.45f), WARM, 10f, 1.2f, false);
                    K.Lamp(Loc(-12f + i * 12f, f, 6f) + Vector3.up * (MapKit.STOREY - 0.45f), WARM, 10f, 1.2f, false);
                }
            }

            // --- ground floor: the foyer and its grand stair, the ballroom and the reception, the dining room, the kitchen, the lounge, the bar.
            CutSlab(M, 1, Rect.MinMaxRect(-1.8f, -9.8f, 1.8f, -2f));
            K.Stairs(M.root, Loc(0, 0, -9.6f), Loc(0, 1, -2.1f), 3.2f, new Color(0.9f, 0.88f, 0.84f));
            K.Prop("Rug", Loc(0, 0, -11f), M.Yaw, lVelvet, MapKit.Collide.None);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Piece("Classic", "Column", Loc(s * 2.4f, 0, -10.4f), M.Yaw, MANSION_WALL, M.root, MapKit.Collide.Box);
                K.Prop("PlantBig", Loc(s * 4.8f, 0, -11f), M.Yaw, null);
                K.Prop("Statue", Loc(s * 3.8f, 0, -6f), M.Yaw + 90f * -s, null, MapKit.Collide.Box);
                // The gallery balustrade around the stair hole upstairs.
                foreach (float z in new[] { -8.3f, -5.3f, -3.5f }) K.Piece("Classic", "Balustrade", Loc(s * 1.95f, 1, z), M.Yaw + 90f * s, MANSION_WALL, M.root);
            }
            K.Piece("Classic", "Balustrade", Loc(0, 1, -9.95f), M.Yaw + 180f, MANSION_WALL, M.root);
            K.ChandelierKit(Loc(0, 2, -6f) - Vector3.up * 0.15f, MapKit.STOREY * 2f - 0.6f);
            K.Npc(NpcRole.Guard, Loc(-3.5f, 0, -10f) + Vector3.up * 0.1f, M.Yaw, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, Loc(3.5f, 0, -10f) + Vector3.up * 0.1f, M.Yaw, Z, ItemKind.Smg);

            K.Furnish(M, 0, Room.Ballroom, new Rect(6.5f, -11.5f, 11f, 11f), r);
            K.Block(M.root, "Stage", Loc(12f, 0, -10.6f) + Vector3.up * 0.2f, new Vector3(5f, 0.4f, 2.4f), lWood, GameLayers.DEFAULT, true, Quaternion.Euler(0, M.Yaw, 0));
            K.Activity(ActivityType.Speech, Loc(12f, 0, -10.6f) + Vector3.up * 0.45f, M.Yaw, Z, 18f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 t = Loc(8.5f + i * 3.5f, 0, -4.2f);
                K.Prop("TableRound", t, M.Yaw, Color.white, MapKit.Collide.Box, true);
                if (i != 1) K.DrinksOn(t + Vector3.up * 0.78f, M.Yaw, Z, 2);
            }
            K.Npc(NpcRole.Guard, Loc(9f, 0, -8f) + Vector3.up * 0.1f, M.Yaw + 90f, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, Loc(16.5f, 0, -7f) + Vector3.up * 0.1f, M.Yaw + 270f, Z, ItemKind.Smg);
            K.Furnish(M, 0, Room.Dining, new Rect(-17.5f, -11.5f, 11f, 11f), r);
            K.Furnish(M, 0, Room.Kitchen, new Rect(-17.5f, 0.5f, 11f, 11f), r);
            K.Activity(ActivityType.Inspect, Loc(-12f, 0, 9.4f) + Vector3.up * 0.1f, M.Yaw, Z);
            K.Activity(ActivityType.Dining, Loc(-9f, 0, 3f) + Vector3.up * 0.1f, M.Yaw, Z);
            K.Gadget(ItemKind.EmeticPoison, Loc(-14.5f, 0, 11.3f) + Vector3.up * 1.0f);
            K.Npc(NpcRole.Guard, Loc(-15f, 0, 3.5f) + Vector3.up * 0.1f, M.Yaw + 90f, Z, ItemKind.Shotgun);
            // The back hall: armchairs, plants, the way out to the terrace.
            for (int s = -1; s <= 1; s += 2)
            {
                K.Prop("Armchair", Loc(s * 4.4f, 0, 6f), M.Yaw + 90f * -s, lVelvet, MapKit.Collide.Box, true);
                K.Prop("PlantBig", Loc(s * 5f, 0, 11f), M.Yaw, null);
                K.Prop("Bookshelf", Loc(s * 5.6f, 0, 2.5f), M.Yaw - 90f * s, lWood);
            }
            K.Activity(ActivityType.Phone, Loc(-2.5f, 0, 6f) + Vector3.up * 0.1f, M.Yaw + 90f, Z);
            // The bar room and the toilets.
            K.Prop("BarCounter", Loc(11.5f, 0, 9f), M.Yaw + 180f, lWood, MapKit.Collide.Box, true);
            K.Prop("BarShelf", Loc(11.5f, 0, 11.6f), M.Yaw + 180f, null);
            for (int i = 0; i < 3; i++) K.Prop("Stool", Loc(10.5f + i * 1f, 0, 7.9f), M.Yaw, lVelvet);
            K.DrinksOn(Loc(11.5f, 0, 9f) + Vector3.up * 1.15f, M.Yaw, Z, 3);
            K.ToiletKit(Loc(7f, 0, 11.3f), M.Yaw + 180f, Z);
            K.ToiletKit(Loc(8.6f, 0, 11.3f), M.Yaw + 180f, Z);
            K.DistractionKit(Loc(14f, 0, 1f), M.Yaw, "radio");
            K.Npc(NpcRole.Guard, Loc(12f, 0, 5f) + Vector3.up * 0.1f, M.Yaw + 180f, Z, ItemKind.Pistol);

            // --- first floor: the gallery, guest bedrooms, the library, Dr. Hale's study, a bathroom.
            K.Furnish(M, 1, Room.Bedroom, new Rect(-17.5f, -11.5f, 11f, 11f), r);
            K.Furnish(M, 1, Room.Bedroom, new Rect(-17.5f, 0.5f, 11f, 11f), r);
            K.Furnish(M, 1, Room.Office, new Rect(6.5f, -11.5f, 11f, 11f), r);
            K.Furnish(M, 1, Room.Bathroom, new Rect(6.5f, 0.5f, 8f, 11f), r);
            for (int i = 0; i < 4; i++) K.Prop("Bookshelf", Loc(-4.5f + i * 3f, 1, 11.6f), M.Yaw + 180f, lWood);
            K.Prop("Sofa", Loc(0, 1, 5f), M.Yaw + 180f, lVelvet, MapKit.Collide.Box, true);
            K.Prop("Armchair", Loc(-3.5f, 1, 7f), M.Yaw + 90f, lVelvet);
            K.Prop("TableRound", Loc(0, 1, 7f), M.Yaw, lWood, MapKit.Collide.Box);
            K.DrinksOn(Loc(0, 1, 7f) + Vector3.up * 0.78f, M.Yaw + 180f, Z, 2);
            K.Activity(ActivityType.Smoke, Loc(3.5f, 1, 9f) + Vector3.up * 0.1f, M.Yaw, Z);
            K.Npc(NpcRole.Guard, Loc(4f, 1, -7f) + Vector3.up * 0.1f, M.Yaw + 270f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(-3f, 1, 2.5f) + Vector3.up * 0.1f, M.Yaw, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Pistol, Loc(-16.5f, 1, -1.5f) + Vector3.up * 0.15f);
            K.HidingKit(Loc(-7f, 1, 10.8f), M.Yaw + 180f, "Wardrobe");

            // --- second floor (VIP): the senator's office, Vivian's sitting room, the private bar, the master suite, the security room.
            K.Furnish(M, 2, Room.Office, new Rect(-17.5f, -11.5f, 11f, 11f), r);
            K.Furnish(M, 2, Room.Lounge, new Rect(-17.5f, 0.5f, 11f, 11f), r);
            K.Furnish(M, 2, Room.Security, new Rect(6.5f, -11.5f, 11f, 11f), r);
            K.Furnish(M, 2, Room.Bedroom, new Rect(6.5f, 0.5f, 8f, 11f), r);
            K.Prop("BarCounter", Loc(0, 2, -9.5f), M.Yaw, lWood, MapKit.Collide.Box, true);
            K.Prop("BarShelf", Loc(0, 2, -11.6f), M.Yaw, null);
            K.DrinksOn(Loc(0, 2, -9.5f) + Vector3.up * 1.15f, M.Yaw + 180f, Z, 3);
            K.Prop("Sofa", Loc(-2.5f, 2, -4f), M.Yaw + 180f, lVelvet, MapKit.Collide.Box, true);
            K.Prop("Piano", Loc(3.5f, 2, -3.5f), M.Yaw + 200f, null, MapKit.Collide.Box, true);
            K.ToiletKit(Loc(-4.5f, 2, 11.3f), M.Yaw + 180f, Z);
            K.ToiletKit(Loc(-3f, 2, 11.3f), M.Yaw + 180f, Z);
            K.Prop("Sink", Loc(4.5f, 2, 11.5f), M.Yaw + 180f, null);
            K.Prop("Rug", Loc(0, 2, 6f), M.Yaw, lVelvet, MapKit.Collide.None);
            K.Npc(NpcRole.Guard, Loc(-3f, 2, -1.5f) + Vector3.up * 0.1f, M.Yaw, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(13.5f, 2, 1.2f) + Vector3.up * 0.1f, M.Yaw + 180f, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, Loc(-9f, 2, -1.8f) + Vector3.up * 0.1f, M.Yaw + 180f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(16f, 2, -2f) + Vector3.up * 0.1f, M.Yaw + 270f, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, Loc(-16.5f, 2, 1.5f) + Vector3.up * 0.15f);
            K.Gadget(ItemKind.Poison, Loc(1.5f, 2, -11.5f) + Vector3.up * 1.15f);

            // --- basement: the loading corridor by the ramp doors, the utility room (breaker, generator, air handler), the wine cellar, the vault.
            K.PartitionKit(M, -1, new Vector3(-12, 0, -12), new Vector3(-12, 0, 12), 4);
            K.PartitionKit(M, -1, new Vector3(-12, 0, 0), new Vector3(6, 0, 0), 3);
            K.PartitionKit(M, -1, new Vector3(6, 0, -12), new Vector3(6, 0, 0), 2);
            K.PartitionKit(M, -1, new Vector3(6, 0, 0), new Vector3(6, 0, 12), 1);
            K.PartitionKit(M, -1, new Vector3(6, 0, 0), new Vector3(15, 0, 0), 1);
            K.Furnish(M, -1, Room.Utility, new Rect(-11.5f, -11.5f, 17f, 11f), r);
            K.VentKit(M, Loc(-1f, -1, -5f), M.Yaw);
            for (int i = 0; i < 5; i++) K.Prop("Bookshelf", Loc(-10f + i * 3f, -1, 11.6f), M.Yaw + 180f, new Color(0.35f, 0.22f, 0.15f));
            for (int i = 0; i < 3; i++) K.Barrel(Loc(-10f + i * 1.2f, -1, 2.2f), false);
            K.Prop("Table", Loc(-2f, -1, 6f), M.Yaw, lWood, MapKit.Collide.Box, true);
            K.DrinksOn(Loc(-2f, -1, 6f) + Vector3.up * 0.8f, M.Yaw + 180f, Z, 2);
            for (int i = 0; i < 4; i++) K.Crate(Loc(-16.5f, -1, -10f + i * 2.2f));
            K.Prop("Pallet", Loc(-15f, -1, 9f), M.Yaw);
            K.Furnish(M, -1, Room.Security, new Rect(6.5f, -11.5f, 11f, 11f), r);
            K.Mesh(M.root, "VaultDoor", CylMesh, Loc(6.3f, -1, -1.6f) + Vector3.up * 1.3f, new Vector3(2.4f, 0.3f, 2.4f), new Color(0.55f, 0.58f, 0.62f), Quaternion.Euler(0, M.Yaw, 90), false).isStatic = true;
            for (int i = 0; i < 3; i++) K.Block(M.root, "Strongbox", Loc(17f, -1, -10f + i * 1.2f) + Vector3.up * 0.6f, new Vector3(0.8f, 1.2f, 1f), new Color(0.45f, 0.48f, 0.5f), GameLayers.DEFAULT, true, Quaternion.Euler(0, M.Yaw, 0));
            K.UniqueWeapon(ItemKind.RocketLauncher, Loc(16f, -1, -6f) + Vector3.up * 0.2f, "Mansion vault");
            K.Gadget(ItemKind.C4, Loc(10f, -1, 4f) + Vector3.up * 0.15f);
            K.Npc(NpcRole.Guard, Loc(9f, -1, -2f) + Vector3.up * 0.1f, M.Yaw + 180f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(-15f, -1, 0f) + Vector3.up * 0.1f, M.Yaw + 90f, Z, ItemKind.Shotgun);
            K.Weapon(ItemKind.Shotgun, Loc(-16f, -1, 11f) + Vector3.up * 0.15f);
            for (int i = 0; i < 3; i++) K.Lamp(Loc(-9f + i * 9f, -1, 0) + Vector3.up * (MapKit.STOREY - 0.45f), new Color(0.9f, 0.85f, 0.7f), 10f, 1.2f, false);
        }

        /// <summary>The portico and its balcony, the back balcony and the trellis, the back terrace party, the roof (cupola, lookouts, zipline).</summary>
        private void MansionOutside(MapKit.KitBuilding M)
        {
            const int Z = 2;
            Quaternion r = Quaternion.Euler(0, M.Yaw, 0);
            // Portico: two-storey columns, the balcony deck in front of the first-floor doors, the pediment.
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 2; k++)
                    K.Piece("Classic", "Column", M.Local(-4.5f + i * 3f, k * MapKit.STOREY, -16f), M.Yaw, MANSION_WALL, M.root, MapKit.Collide.Box);
            Deck(M.Local(0, MapKit.STOREY + 0.06f, -14.1f), new Vector2(12.6f, 4.2f), M.Yaw, 0.35f, MANSION_WALL, 0f, M.root);
            for (int i = 0; i < 3; i++) K.Piece("Classic", "Balustrade", M.Local(-3f + i * 3f, MapKit.STOREY + 0.06f, -16.1f), M.Yaw + 180f, MANSION_WALL, M.root);
            for (int s = -1; s <= 1; s += 2) K.Piece("Classic", "Balustrade", M.Local(s * 6.2f, MapKit.STOREY + 0.06f, -14.4f), M.Yaw + 90f * s, MANSION_WALL, M.root);
            K.Block(M.root, "Entablature", M.Local(0, MapKit.STOREY * 2f + 0.25f, -14.6f), new Vector3(13.2f, 0.5f, 5f), MANSION_WALL, GameLayers.DEFAULT, true, r);
            GameObject lPediment = K.Piece("Classic", "RoofGable", M.Local(0, MapKit.STOREY * 2f + 0.5f, -14.6f), M.Yaw + 90f, MANSION_ROOF, M.root, MapKit.Collide.None);
            if (lPediment != null) lPediment.transform.localScale = new Vector3(5.2f, 2.4f, 13.4f);
            K.Lamp(M.Local(0, MapKit.STOREY - 0.5f, -14f), WARM, 10f, 1.5f, false);
            K.Activity(ActivityType.Phone, M.Local(-3.5f, MapKit.STOREY + 0.15f, -14.5f), M.Yaw + 180f, Z);
            // The back balcony of the master suite, the trellis up to it, the drainpipe on the east (spec).
            Vector3 lBack = Facade(M, 2, 3, MapKit.STOREY * 2f + 0.06f, 1.1f);
            Deck(lBack, new Vector2(5f, 2.2f), M.Yaw, 0.25f, MANSION_WALL, 0f, M.root);
            K.Piece("Classic", "Balustrade", Facade(M, 2, 3, MapKit.STOREY * 2f + 0.06f, 2.1f), M.Yaw, MANSION_WALL, M.root);
            Trellis(Facade(M, 2, 1.8f, 0f, 0f) + Vector3.up * 0.05f, MapKit.STOREY * 2f + 1.2f, 1.6f, M.Yaw);
            // The back terrace: the garden party (tables, string lights, a bar), smokers by the kitchen.
            Color[] lCloth = { Color.white, new Color(0.85f, 0.82f, 0.95f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 t = G(-262f + i * 8f, 351f);
                Parasol(t, 0, lCloth[i % 2], true);
                if (i % 2 == 0) K.DrinksOn(t + Vector3.up * 0.78f, 0, Z, 2);
            }
            K.Prop("BarCounter", G(-236f, 354f), 180, new Color(0.35f, 0.24f, 0.18f), MapKit.Collide.Box, true);
            K.DrinksOn(G(-236f, 354f, 1.15f), 0, Z, 3);
            K.Activity(ActivityType.Garden, G(-246f, 354f, 0.1f), 0, Z);
            for (int i = 0; i < 3; i++) StringLights(G(-266f + i * 12f, 345f, 3.2f), G(-266f + i * 12f + 10f, 356f, 3.2f));
            K.Npc(NpcRole.Guard, G(-250f, 348f, 0f), 0, Z, ItemKind.Smg);
            K.Activity(ActivityType.Smoke, G(-272f, 340f, 0.1f), 90, Z);
            // The front: the steps and two guards under the portico.
            Guard(Z, -254f, 316f, 180, ItemKind.Smg);
            Guard(Z, -246f, 316f, 180, ItemKind.Shotgun);
            // The roof: the cupola (lookout), two lookouts, the zipline down to the golf course.
            float lRoof = RoofY(M.spec);
            Vector3 lCupola = new Vector3(MANSION.x - 6f, lRoof, MANSION.z - 2f);
            for (int i = 0; i < 4; i++) K.Piece("Classic", "Column", lCupola + new Vector3(i % 2 == 0 ? -2.4f : 2.4f, 0, i < 2 ? -2.4f : 2.4f), 0, MANSION_WALL, M.root, MapKit.Collide.Box);
            GameObject lDome = K.Piece("Classic", "RoofHip", lCupola + Vector3.up * MapKit.STOREY, 0, new Color(0.35f, 0.5f, 0.45f), M.root, MapKit.Collide.None);
            if (lDome != null) lDome.transform.localScale = new Vector3(6.2f, 3.4f, 6.2f);
            K.Block(M.root, "Weathervane", lCupola + Vector3.up * (MapKit.STOREY + 4.2f), new Vector3(0.08f, 1.6f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
            K.Lamp(lCupola + Vector3.up * 2.8f, WARM, 8f, 1.2f, false);
            K.Npc(NpcRole.Lookout, lCupola + new Vector3(0, 0.1f, 0), 180, Z, ItemKind.Sniper);
            K.Npc(NpcRole.Lookout, M.Local(-14f, lRoof - M.root.position.y + 0.1f, 9f), 0, Z, ItemKind.Smg);
            K.Activity(ActivityType.Lookout, M.Local(-14f, lRoof - M.root.position.y + 0.1f, -10f), 180, Z);
            K.Weapon(ItemKind.Sniper, M.Local(-10f, lRoof - M.root.position.y + 0.15f, -10.5f));
            ClearRoof(M, new Rect(MANSION.x - 11f, MANSION.z - 7f, 10f, 10f));
        }

        // ---------------------------------------------------------------- the estate

        /// <summary>The terrace wall (balustrade, garden stair, ivy), the estate walls with the gate, the service gate and the north gap; the gatehouse; the orchard below.</summary>
        private void Estate()
        {
            const int Z = 2;
            Transform lWalls = K.Group("EstateWalls", K.Env);
            // The terrace wall along the south edge, split by the garden stair at x = -290.
            foreach ((float x0, float x1) in new[] { (-372f, -293.5f), (-286.5f, -205f) })
            {
                K.Block(lWalls, "TerraceWall", new Vector3((x0 + x1) * 0.5f, (10.2f + HILL + 0.05f) * 0.5f, 248.1f), new Vector3(x1 - x0, HILL + 0.05f - 10.2f, 7.8f), ESTATE_STONE * 0.92f);
                for (float x = x0 + 1.5f; x < x1 - 1f; x += 3f) K.Piece("Classic", "Balustrade", new Vector3(x, HILL + 0.05f, 244.6f), 180, ESTATE_STONE, lWalls);
                for (float x = x0 + 6f; x < x1 - 3f; x += 14f)
                {
                    K.Block(lWalls, "Buttress", new Vector3(x, (10.2f + HILL) * 0.5f, 243.7f), new Vector3(1.6f, HILL - 10.2f, 1.4f), ESTATE_STONE * 0.82f);
                    K.Lamp(new Vector3(x, HILL - 2.6f, 242.8f), WARM, 9f, 1.2f, false);
                }
            }
            float lFoot = Surf(-290f, 226f);
            K.Stairs(lWalls, new Vector3(-290f, lFoot, 226f), new Vector3(-290f, HILL + 0.05f, 252.4f), 6f, ESTATE_STONE);
            for (int s = -1; s <= 1; s += 2)
            {
                float lLen = Vector3.Distance(new Vector3(0, lFoot, 226f), new Vector3(0, HILL, 252.4f));
                K.Block(lWalls, "StairCheek", new Vector3(-290f + s * 3.3f, (lFoot + HILL) * 0.5f + 0.4f, 239.2f), new Vector3(0.6f, 1.2f, lLen), ESTATE_STONE * 0.85f, GameLayers.DEFAULT, true, Quaternion.LookRotation(new Vector3(0, HILL - lFoot, 26.4f)));
            }
            Guard(Z, -294f, 256f, 180, ItemKind.Smg);
            Guard(Z, -286f, 256f, 180, ItemKind.Pistol);
            Trellis(new Vector3(-336f, Surf(-336f, 243f), 244.1f), HILL + 1.4f - Surf(-336f, 243f), 2f, 180f);
            Trellis(new Vector3(-232f, Surf(-232f, 243f), 244.1f), HILL + 1.4f - Surf(-232f, 243f), 2f, 180f);
            // Estate walls: the gate on the south, the service gate on the west, a collapsed stretch on the north.
            Color lStone = ESTATE_STONE * 0.9f;
            void W(float ax, float az, float bx, float bz) => K.Fence(new Vector3(ax, HILL, az), new Vector3(bx, HILL, bz), 2.6f, lStone, true);
            W(-205f, 250f, -184f, 250f);
            W(-172f, 250f, -141f, 250f);
            W(-141f, 250f, -141f, 431f);
            W(-141f, 431f, -292f, 431f);
            W(-304f, 431f, -371f, 431f);
            W(-371f, 431f, -371f, 306f);
            W(-371f, 294f, -371f, 250f);
            for (int i = 0; i < 5; i++) K.Block(lWalls, "Rubble", new Vector3(-298f + Rand(-4f, 4f), HILL + 0.3f, 431f + Rand(-1.5f, 1.5f)), new Vector3(Rand(0.6f, 1.4f), Rand(0.4f, 0.9f), Rand(0.6f, 1.2f)), lStone, GameLayers.DEFAULT, true, Quaternion.Euler(Rand(0, 30), Rand(0, 360), 0));
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(lWalls, "GatePier", new Vector3(-178f + s * 6.4f, HILL + 1.7f, 250f), new Vector3(1.2f, 3.4f, 1.2f), ESTATE_STONE);
                K.Mesh(lWalls, "GateLamp", BallMesh, new Vector3(-178f + s * 6.4f, HILL + 3.7f, 250f), Vector3.one * 0.5f, new Color(2.4f, 2f, 1.4f), null, false, _glowMaterial).isStatic = true;
                K.Block(lWalls, "GatePier", new Vector3(-371f, HILL + 1.7f, 300f + s * 6.4f), new Vector3(1.2f, 3.4f, 1.2f), ESTATE_STONE);
            }
            K.Lamp(new Vector3(-178f, HILL + 3.4f, 248f), WARM, 12f, 1.6f, false);
            K.Prop("Barrier", new Vector3(-178f, HILL, 251f), 0, null, MapKit.Collide.Box, true);
            K.Building(Spec("Whitlock Gatehouse"));
            Guard(Z, -182f, 246f, 180, ItemKind.Smg);
            Guard(Z, -174f, 246f, 180, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, new Vector3(-190f, HILL + 0.15f, 258f), 90, Z, ItemKind.Pistol);
            Guard(Z, -366f, 300f, 270, ItemKind.Pistol);
            // Lamp posts down the drive; the orchard on the south slope below the terrace.
            foreach (Vector2 p in new[] { new Vector2(-218, 172), new Vector2(-192, 198), new Vector2(-178, 226), new Vector2(-196, 266), new Vector2(-226, 284) })
            {
                P("LampPostOrnate", p.x + 4f, p.y, 0);
                K.Lamp(G(p.x + 4f, p.y, 4.1f), WARM, 13f, 1.4f, false);
            }
            for (int row = 0; row < 4; row++)
                for (int k = 0; k < 11; k++)
                {
                    float x = -362f + k * 14f + (row % 2) * 7f, z = 186f + row * 13f;
                    if (Mathf.Abs(x + 290f) < 8f || x > -210f) continue;
                    Nat("BroadleafC", x, z, Rand(0.45f, 0.6f), -1f, row % 2 == 0 ? new Color(0.5f, 0.66f, 0.32f) : new Color(0.62f, 0.7f, 0.36f), MapKit.Collide.Box, 0.1f);
                }
            K.Activity(ActivityType.Garden, G(-320f, 212f, 0.1f), 0, Z);
        }

        /// <summary>The forecourt (fountain, guests' cars, lamps), the formal parterres either side, statues and cypresses.</summary>
        private void Gardens()
        {
            const int Z = 2;
            GameObject lFountain = P("Fountain", -250f, 302f, 0, null, true);
            if (lFountain != null) lFountain.transform.localScale = Vector3.one * 1.7f;
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 50f + 210f) * Mathf.Deg2Rad;
                K.Car(G(-250f + Mathf.Cos(a) * 12.5f, 302f + Mathf.Sin(a) * 12.5f), -i * 50f - 210f + 180f, new[] { new Color(0.08f, 0.08f, 0.1f), new Color(0.85f, 0.85f, 0.88f), new Color(0.35f, 0.08f, 0.1f) }[i % 3]);
            }
            K.Distraction(G(-262.5f, 302f, 0.9f), 0, "caralarm");
            for (int i = 0; i < 4; i++) { float x = i < 2 ? -268f : -232f, z = i % 2 == 0 ? 296f : 312f; P("LampPostOrnate", x, z, 0); K.Lamp(G(x, z, 4.1f), WARM, 13f, 1.5f, false); }
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 3; k++) Parterre(new Rect(-330f + k * 20f, 262f + row * 22f, 15f, 16f), k + row);
            for (int i = 0; i < 6; i++) Nat("CypressA", -334f + i * 12f, 312f, 0.7f);
            // The east lawn: a gazebo with drinks, a rose walk.
            Vector3 lGazebo = G(-208f, 272f);
            for (int i = 0; i < 6; i++) K.Piece("Classic", "Column", lGazebo + Quaternion.Euler(0, i * 60f, 0) * Vector3.forward * 3f, i * 60f, null, K.Env, MapKit.Collide.Box);
            GameObject lDome = K.Piece("Classic", "RoofHip", lGazebo + Vector3.up * MapKit.STOREY, 0, new Color(0.35f, 0.5f, 0.45f), K.Env, MapKit.Collide.None);
            if (lDome != null) lDome.transform.localScale = new Vector3(7.2f, 2.6f, 7.2f);
            K.Prop("TableRound", lGazebo, 0, Color.white, MapKit.Collide.Box, true);
            K.DrinksOn(lGazebo + Vector3.up * 0.78f, 0, Z, 2);
            for (int i = 0; i < 8; i++) K.Piece("Nature", "FlowerBed", G(-160f, 262f + i * 5f), 90f, new Color(0.85f, 0.25f, 0.3f), K.Env, MapKit.Collide.None, 1.2f);
            P("Statue", -268f, 286f, 45, null, true);
            P("Statue", -232f, 286f, 315, null, true);
            K.Activity(ActivityType.Garden, G(-302f, 281f, 0.1f), 0, Z);
            K.Activity(ActivityType.Garden, G(-196f, 300f, 0.1f), 180, Z);
            K.Activity(ActivityType.Phone, G(-232f, 292f, 0.1f), 90, Z);
            P("Bench", -300f, 305f, 180);
            P("Bench", -205f, 300f, 180);
            K.Weapon(ItemKind.Smg, G(-312f, 281f, 0.15f));
            K.Gadget(ItemKind.Coin, G(-186f, 286f, 0.15f));
        }

        /// <summary>A formal garden bed: low box hedges on its outline (a gap on each side), flowers inside.</summary>
        private void Parterre(Rect pBed, int pSeed)
        {
            Transform lGroup = K.Group("Parterre", K.Env);
            for (int side = 0; side < 4; side++)
            {
                bool lAlongX = side % 2 == 0;
                float lLength = lAlongX ? pBed.width : pBed.height;
                int lCount = Mathf.RoundToInt(lLength / 2.2f);
                for (int i = 0; i < lCount; i++)
                {
                    if (i == lCount / 2) continue;
                    float t = (i + 0.5f) / lCount;
                    float x = lAlongX ? Mathf.Lerp(pBed.xMin, pBed.xMax, t) : (side == 1 ? pBed.xMax : pBed.xMin);
                    float z = lAlongX ? (side == 0 ? pBed.yMin : pBed.yMax) : Mathf.Lerp(pBed.yMin, pBed.yMax, t);
                    Stretch("Nature", "HedgeBlock", G(x, z), lAlongX ? 0f : 90f, new Vector3(1f, 0.5f, 0.6f), BOXWOOD, lGroup);
                }
            }
            for (int i = 0; i < 3; i++) K.Piece("Nature", "FlowerBed", G(pBed.center.x + (i - 1) * 2.8f, pBed.center.y), 0f, new[] { new Color(0.92f, 0.4f, 0.45f), new Color(0.95f, 0.85f, 0.4f), new Color(0.65f, 0.45f, 0.88f) }[(i + pSeed) % 3], lGroup, MapKit.Collide.None, 1.2f);
        }

        /// <summary>The hedge maze north-west of the house: a perfect maze of tall hedges (10 x 8 cells), a statue and a bench in its heart.</summary>
        private void Maze()
        {
            const int Z = 2;
            const int CW = 10, CH = 8;
            const float S = 6f, X0 = -364f, Z0 = 370f;
            Transform lMaze = K.Group("HedgeMaze", K.Env);
            bool[,] lVisited = new bool[CW, CH];
            bool[,] lOpenE = new bool[CW, CH], lOpenN = new bool[CW, CH];
            System.Random r = new System.Random(808);
            Stack<Vector2Int> lStack = new Stack<Vector2Int>();
            lStack.Push(new Vector2Int(0, 0));
            lVisited[0, 0] = true;
            while (lStack.Count > 0)
            {
                Vector2Int c = lStack.Peek();
                List<Vector2Int> lNext = new List<Vector2Int>();
                foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    Vector2Int n = c + d;
                    if (n.x >= 0 && n.y >= 0 && n.x < CW && n.y < CH && !lVisited[n.x, n.y]) lNext.Add(n);
                }
                if (lNext.Count == 0) { lStack.Pop(); continue; }
                Vector2Int lPick = lNext[r.Next(lNext.Count)];
                if (lPick.x > c.x) lOpenE[c.x, c.y] = true;
                else if (lPick.x < c.x) lOpenE[lPick.x, lPick.y] = true;
                else if (lPick.y > c.y) lOpenN[c.x, c.y] = true;
                else lOpenN[lPick.x, lPick.y] = true;
                lVisited[lPick.x, lPick.y] = true;
                lStack.Push(lPick);
            }
            // A few loops so a chase can continue.
            for (int i = 0; i < 8; i++) { lOpenE[r.Next(CW - 1), r.Next(CH)] = true; lOpenN[r.Next(CW), r.Next(CH - 1)] = true; }
            void Hedge(Vector3 a, Vector3 b)
            {
                Vector3 m = (a + b) * 0.5f;
                bool lAlongX = Mathf.Abs(b.x - a.x) > 0.1f;
                Stretch("Nature", "HedgeBlock", G(m.x, m.z), lAlongX ? 0f : 90f, new Vector3((S + 0.6f) / 2.3f, 1.1f, 0.9f), BOXWOOD, lMaze);
            }
            for (int x = 0; x < CW; x++)
                for (int y = 0; y < CH; y++)
                {
                    float x0 = X0 + x * S, z0 = Z0 + y * S;
                    if (x < CW - 1 && !lOpenE[x, y]) Hedge(new Vector3(x0 + S, 0, z0), new Vector3(x0 + S, 0, z0 + S));
                    if (y < CH - 1 && !lOpenN[x, y]) Hedge(new Vector3(x0, 0, z0 + S), new Vector3(x0 + S, 0, z0 + S));
                }
            for (int x = 0; x < CW; x++)
            {
                if (x != 4) Hedge(new Vector3(X0 + x * S, 0, Z0), new Vector3(X0 + (x + 1) * S, 0, Z0));
                if (x != 7) Hedge(new Vector3(X0 + x * S, 0, Z0 + CH * S), new Vector3(X0 + (x + 1) * S, 0, Z0 + CH * S));
            }
            for (int y = 0; y < CH; y++)
            {
                if (y != 2) Hedge(new Vector3(X0, 0, Z0 + y * S), new Vector3(X0, 0, Z0 + (y + 1) * S));
                Hedge(new Vector3(X0 + CW * S, 0, Z0 + y * S), new Vector3(X0 + CW * S, 0, Z0 + (y + 1) * S));
            }
            Vector3 lHeart = G(X0 + 4.5f * S, Z0 + 3.5f * S);
            P("Statue", lHeart, 0, null, true);
            P("Bench", lHeart + new Vector3(0, 0, -2.2f), 180);
            K.Activity(ActivityType.Garden, lHeart + new Vector3(1.6f, 0.1f, -1.4f), 0, Z);
            K.Weapon(ItemKind.Shotgun, lHeart + new Vector3(-1.5f, 0.15f, 1.5f));
            K.Npc(NpcRole.Guard, G(X0 + 4.5f * S, Z0 - 2f, 0.1f), 180, Z, ItemKind.Pistol);
            K.Lamp(lHeart + Vector3.up * 3f, WARM, 10f, 1.1f, false);
        }

        /// <summary>The pool (a sunken basin), loungers, the pool bar and the DJ; the pool house; the tennis court.</summary>
        private void PoolAndCourt()
        {
            const int Z = 2;
            // The basin: four 4 m cells cut out of the plateau, tiled walls and floor, the water, the coping.
            K.Hole(R(-203.95f, 328.05f, -188.05f, 335.95f));
            Vector3 c = new Vector3(-196f, HILL, 332f);
            Color lTile = new Color(0.45f, 0.75f, 0.85f);
            K.Block(K.Env, "PoolFloor", c + Vector3.up * -1.8f, new Vector3(16f, 0.3f, 8f), lTile);
            for (int s = 0; s < 4; s++)
            {
                bool lAlong = s % 2 == 0;
                Vector3 o = lAlong ? new Vector3(0, 0, (s == 0 ? -1 : 1) * 4.15f) : new Vector3((s == 1 ? 1 : -1) * 8.15f, 0, 0);
                K.Block(K.Env, "PoolWall", c + o + Vector3.up * -0.85f, lAlong ? new Vector3(16.6f, 1.9f, 0.3f) : new Vector3(0.3f, 1.9f, 8f), lTile * 0.9f);
                K.Block(K.Env, "Coping", c + o * 1.04f + Vector3.up * 0.06f, lAlong ? new Vector3(17.4f, 0.12f, 0.9f) : new Vector3(0.9f, 0.12f, 8.6f), STONE);
            }
            K.Water(c + Vector3.up * -0.3f, new Vector2(16f, 8f));
            K.Stairs(K.Env, c + new Vector3(-7.9f, -1.65f, 0), c + new Vector3(-5.6f, 0.02f, 0), 2f, lTile);
            for (int i = 0; i < 6; i++)
            {
                Vector3 l = G(-206f + i * 3.2f, 325.5f);
                K.Block(K.Props, "Lounger", l + Vector3.up * 0.2f, new Vector3(0.8f, 0.4f, 2f), Color.white, GameLayers.DEFAULT, true);
                K.Block(K.Props, "LoungerBack", l + new Vector3(0, 0.55f, -0.8f), new Vector3(0.8f, 0.08f, 0.7f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(-40f, 0, 0));
                if (i % 2 == 0) Parasol(l + new Vector3(1.6f, 0, 0.3f), 0, AWNINGS[(i / 2) % AWNINGS.Length], false);
            }
            K.Prop("BarCounter", G(-183f, 338f), 270, new Color(0.35f, 0.24f, 0.18f), MapKit.Collide.Box, true);
            K.DrinksOn(G(-183f, 338f, 1.15f), 90, Z, 3);
            K.Prop("Table", G(-184f, 328f), 90, new Color(0.2f, 0.2f, 0.22f), MapKit.Collide.Box);
            K.DistractionKit(G(-184f, 328f, 0.8f), 270, "radio");
            for (int i = 0; i < 2; i++) StringLights(G(-206f, 324f + i * 14f, 3.4f), G(-186f, 324f + i * 14f, 3.4f));
            K.Activity(ActivityType.Drink, G(-190f, 341f, 0.1f), 180, Z);
            K.Npc(NpcRole.Guard, G(-208f, 340f, 0f), 90, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, G(-186f, 322f, 0f), 0, Z, ItemKind.Pistol);
            MapKit.KitBuilding h = K.Building(Spec("Whitlock Pool House"));
            K.Activity(ActivityType.Smoke, Facade(h, 2, 2, 0.1f, 1.6f), FacadeYaw(h, 2), Z);
            K.Weapon(ItemKind.Smg, h.Local(-4f, 0.15f, 2.5f));
            // The tennis court: lines, the net, the fence, a bench and the ball machine.
            Vector3 t = G(-170f, 400f);
            Transform lCourt = K.Group("TennisCourt", K.Env);
            K.Block(lCourt, "Court", t + Vector3.up * 0.02f, new Vector3(24f, 0.04f, 11f), new Color(0.25f, 0.48f, 0.38f), GameLayers.DEFAULT, false);
            foreach (float z in new[] { -5.5f, 5.5f, -4.1f, 4.1f }) K.Block(lCourt, "Line", t + new Vector3(0, 0.05f, z), new Vector3(24f, 0.02f, 0.08f), Color.white, GameLayers.DEFAULT, false);
            foreach (float x in new[] { -12f, 12f, -6.4f, 6.4f }) K.Block(lCourt, "Line", t + new Vector3(x, 0.05f, 0), new Vector3(0.08f, 0.02f, x * x > 100f ? 11f : 8.2f), Color.white, GameLayers.DEFAULT, false);
            K.Block(lCourt, "Net", t + new Vector3(0, 0.5f, 0), new Vector3(0.05f, 1f, 12f), new Color(0.9f, 0.9f, 0.9f));
            Color lMesh = new Color(0.25f, 0.35f, 0.3f);
            K.Fence(t + new Vector3(-14f, 0, -7f), t + new Vector3(14f, 0, -7f), 3f, lMesh);
            K.Fence(t + new Vector3(-14f, 0, -7f), t + new Vector3(-14f, 0, 7f), 3f, lMesh);
            K.Fence(t + new Vector3(14f, 0, -7f), t + new Vector3(14f, 0, 7f), 3f, lMesh);
            K.Fence(t + new Vector3(-14f, 0, 7f), t + new Vector3(-2f, 0, 7f), 3f, lMesh);
            K.Fence(t + new Vector3(2f, 0, 7f), t + new Vector3(14f, 0, 7f), 3f, lMesh);
            P("Bench", t + new Vector3(0, 0, 8.6f), 180);
            K.Block(lCourt, "BallMachine", t + new Vector3(10f, 0.45f, -3f), new Vector3(0.8f, 0.9f, 0.8f), new Color(0.2f, 0.3f, 0.5f), GameLayers.DEFAULT, true);
            K.Activity(ActivityType.Workout, t + new Vector3(-8f, 0.1f, 2f), 90, Z);
            K.Activity(ActivityType.Workout, t + new Vector3(8f, 0.1f, -2f), 270, Z);
        }

        /// <summary>The greenhouse, the garage and the service yard, the staff quarters, the guest cottage, the helipad.</summary>
        private void Outbuildings()
        {
            const int Z = 2;
            MapKit.KitBuilding g = K.Building(Spec("Whitlock Greenhouse"));
            for (int i = 0; i < 3; i++)
            {
                Vector3 b = g.Local(-3f + i * 3f, 0, 0.5f);
                K.Block(g.root, "PottingBench", b + Vector3.up * 0.45f, new Vector3(2.2f, 0.9f, 1f), PLANK, GameLayers.DEFAULT, true, Quaternion.Euler(0, g.Yaw, 0));
                for (int k = 0; k < 3; k++) K.Prop("PlantPot", b + Quaternion.Euler(0, g.Yaw, 0) * new Vector3(-0.7f + k * 0.7f, 0.9f, 0), g.Yaw, null, MapKit.Collide.None);
            }
            K.Activity(ActivityType.Garden, g.Local(0, 0.1f, -2f), g.Yaw, Z);
            K.Activity(ActivityType.Garden, g.Local(-4f, 0.1f, 2.4f), g.Yaw + 180f, Z);
            K.UniqueWeapon(ItemKind.Flamethrower, g.Local(4.5f, 0.15f, 3f), "Greenhouse potting corner");
            K.Gadget(ItemKind.Poison, g.Local(3f, 0.95f, 0.5f));
            K.Lamp(g.Local(0, 2.8f, 0), new Color(0.8f, 1f, 0.8f), 10f, 1f, false);
            MapKit.KitBuilding gar = K.Building(Spec("Whitlock Garage"));
            K.Car(gar.Local(-1.5f, 0, -0.5f), gar.Yaw, new Color(0.08f, 0.08f, 0.1f));
            K.Car(gar.Local(1.5f, 0, 0.5f) + Vector3.up * 0.01f, gar.Yaw + 180f, new Color(0.55f, 0.08f, 0.1f));
            K.Car(G(-340f, 352f), 90, new Color(0.85f, 0.85f, 0.88f));
            K.Activity(ActivityType.Inspect, gar.Local(4.4f, 0.1f, -3f), gar.Yaw, Z);
            // The service yard by the kitchen door and the ramp: deliveries, dumpsters, the ramp walls.
            K.Car(G(-290f, 346f), 90, new Color(0.9f, 0.9f, 0.88f));
            for (int i = 0; i < 3; i++) Clutter(G(-296f + i * 4f, 316f), 0, i);
            K.HidingKit(G(-282f, 318f), 0, "Dumpster");
            K.Activity(ActivityType.Smoke, G(-276f, 346f, 0.1f), 270, Z);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(K.Env, "RampWall", new Vector3(-284f, HILL - 1.5f, 332f + s * 6.3f), new Vector3(32f, MapKit.STOREY + 0.6f, 0.6f), ESTATE_STONE * 0.85f);
                Row("Classic", "Railing", new Vector3(-298f, HILL + 0.3f, 332f + s * 6.6f), new Vector3(-270f, HILL + 0.3f, 332f + s * 6.6f), 3f, s < 0 ? 180f : 0f, ESTATE_STONE);
            }
            K.Lamp(new Vector3(-270f, HILL - 0.8f, 332f), WARM, 9f, 1.2f, false);
            Guard(Z, -274f, 332f, 270, ItemKind.Shotgun);
            MapKit.KitBuilding st = K.Building(Spec("Whitlock Staff Quarters"));
            K.Npc(NpcRole.Guard, st.Local(2f, 0.1f, -2f), st.Yaw + 180f, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Pistol, st.Local(-5f, MapKit.STOREY + 0.15f, 2f));
            MapKit.KitBuilding gc = K.Building(Spec("Whitlock Guest Cottage"));
            Deck(Facade(gc, 0, 1.5f, 0.3f, 1.6f), new Vector2(12f, 3f), gc.Yaw, 0.3f, PLANK, 1f);
            K.Weapon(ItemKind.Smg, gc.Local(3f, MapKit.STOREY + 0.15f, -2f));
            // The helipad on the north lawn (extraction), its helicopter.
            Vector3 hp = G(-255f, 402f);
            K.Mesh(K.Env, "Helipad", CylMesh, hp + Vector3.up * 0.04f, new Vector3(15f, 0.08f, 15f), new Color(0.3f, 0.32f, 0.34f));
            K.Mesh(K.Env, "HelipadRing", CylMesh, hp + Vector3.up * 0.03f, new Vector3(16f, 0.06f, 16f), new Color(0.95f, 0.8f, 0.2f));
            K.Block(K.Env, "H", hp + new Vector3(-1.6f, 0.09f, 0), new Vector3(0.6f, 0.02f, 4.4f), Color.white, GameLayers.DEFAULT, false);
            K.Block(K.Env, "H", hp + new Vector3(1.6f, 0.09f, 0), new Vector3(0.6f, 0.02f, 4.4f), Color.white, GameLayers.DEFAULT, false);
            K.Block(K.Env, "H", hp + new Vector3(0, 0.09f, 0), new Vector3(2.6f, 0.02f, 0.6f), Color.white, GameLayers.DEFAULT, false);
            Vector3 lHeli = hp + new Vector3(0, 0, 3f);
            Transform lCopter = K.Group("Helicopter", K.Env);
            K.Mesh(lCopter, "HeliBody", DomeMesh, lHeli + Vector3.up * 1.5f, new Vector3(2.6f, 2.4f, 5f), new Color(0.12f, 0.14f, 0.2f), null, true).isStatic = true;
            K.Block(lCopter, "HeliTail", lHeli + new Vector3(0, 1.9f, -4.8f), new Vector3(0.5f, 0.5f, 5.5f), new Color(0.12f, 0.14f, 0.2f));
            K.Block(lCopter, "HeliFin", lHeli + new Vector3(0, 2.6f, -7.3f), new Vector3(0.12f, 1.4f, 0.8f), new Color(0.85f, 0.68f, 0.35f));
            K.Block(lCopter, "Rotor", lHeli + new Vector3(0, 2.95f, 0), new Vector3(0.3f, 0.06f, 11f), new Color(0.1f, 0.1f, 0.1f), GameLayers.DEFAULT, false, Quaternion.Euler(0, 30f, 0));
            K.Block(lCopter, "Rotor", lHeli + new Vector3(0, 2.95f, 0), new Vector3(0.3f, 0.06f, 11f), new Color(0.1f, 0.1f, 0.1f), GameLayers.DEFAULT, false, Quaternion.Euler(0, 120f, 0));
            for (int s = -1; s <= 1; s += 2) K.Block(lCopter, "Skid", lHeli + new Vector3(s * 1.1f, 0.12f, 0), new Vector3(0.1f, 0.1f, 4f), new Color(0.2f, 0.2f, 0.2f), GameLayers.DEFAULT, false);
            for (int i = 0; i < 6; i++) K.Lamp(hp + Quaternion.Euler(0, i * 60f, 0) * Vector3.forward * 8f + Vector3.up * 0.2f, new Color(0.4f, 1f, 0.5f), 4f, 1f, false);
            K.Point(MatchPoint.Kind.Extraction, hp + new Vector3(4.5f, 0.1f, -3.5f), 0, "Whitlock helipad");
            Guard(Z, -244f, 402f, 270, ItemKind.Smg);
        }
    }
}
