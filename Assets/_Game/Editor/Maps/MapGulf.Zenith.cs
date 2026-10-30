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
    /// The Zenith (zone 2): a podium (lobby, lobby bar and kitchen, spa and offices, a basement
    /// with the parking, the breaker and the air handler) carrying an eighteen-floor hotel tower,
    /// the map's landmark: pool deck on the podium roof, rooms, the Sky Lounge on the eighth floor,
    /// the penthouse and its helipad. Public: the gardens, the marina promenade, the beach club.
    /// Restricted: the forecourt past the gatehouse, the pool deck, the service yard, the basement.
    /// VIP: the Sky Lounge, the suites, the penthouse. Ways in: the guarded lobby doors, the
    /// kitchen door in the service yard, the parking ramp into the basement, the pool deck from
    /// the podium stairs or the crane zipline, the scaffold and the window cleaners' gondola up
    /// the west face, the helipad. Around it: the construction site and its tower crane, the
    /// marina with its yachts, the Azure beach club with jump pads between its roofs.
    /// </summary>
    public partial class MapGulf
    {
        /// <summary>The podium (centre on the 4 m ground grid, so its basement hole fits the cells).</summary>
        private static readonly Vector3 PODIUM = new Vector3(304f, YARD + 0.05f, -214f);
        private const int TOWER_FLOORS = 18;
        private static readonly Color ZENITH_STONE = new Color(0.95f, 0.9f, 0.8f);
        private static readonly Color ZENITH_GOLD = new Color(0.85f, 0.68f, 0.35f);

        private float DeckY => PODIUM.y + 2 * MapKit.STOREY;

        private void PlanZenith()
        {
            _zoneStage.Add(() => Flatten(R(176, -346, 496, -102), YARD, 24f));
            _tame.Add(R(176, -420, 500, -100));
            Road(12f, ASPHALT, new Vector2(-128, -322), new Vector2(0, -326), new Vector2(175, -324), new Vector2(330, -322), new Vector2(495, -318));
            // The basement ramp: a sunken lane from the service yard down to the parking doors.
            _carveStage.Add(() => Cells(R(328, -206, 354, -198), (i, j) =>
            {
                float x = Cx(i);
                _h[i, j] = x < 332f ? YARD - MapKit.STOREY : Mathf.Lerp(YARD - MapKit.STOREY, YARD, (x - 332f) / 22f);
                _f[i, j] |= BUILT;
                _paint[i, j] = ASPHALT;
            }));
            PaintRect(R(296, -192, 312, -124), ASPHALT);
            PaintDisc(new Vector2(304, -160), 17f, ASPHALT);
            PaintDisc(new Vector2(304, -160), 9f, LAWN, 0);
            PaintRect(R(250, -194, 296, -128), LAWN, 0);
            PaintRect(R(312, -194, 358, -128), LAWN, 0);
            PaintRect(R(280, -196, 328, -192), PAVE);
            PaintRect(R(328, -248, 366, -188), CONCRETE);
            PaintRect(R(176, -342, 352, -328), PAVE);
            PaintRect(R(200, -162, 248, -136), ASPHALT);
            PaintRect(R(196, -252, 268, -168), new Color(0.7f, 0.64f, 0.54f));
            PaintRect(R(176, -316, 352, -234), PAVE);
            PaintRect(R(352, -316, 496, -310), PAVE);

            MapKit.KitSpec p = Lot("Zenith Podium", "Classic", PODIUM.x, PODIUM.z, 180, 16, 12, 2, Roof.Flat, ZENITH_STONE, new Color(0.78f, 0.74f, 0.68f), 2, YARD, 4f);
            Doors(p, 0, 7, 0, 8, 2, 4, 2, 11, 3, 2, 1, 6);
            p.basement = true;
            p.basementDoors.Add(new Vector2Int(3, 9));
            p.basementDoors.Add(new Vector2Int(3, 10));
            p.tallWindows = true;
            p.windowChance = 0.85f;
            p.balconyChance = 0f;
            p.interiorWall = new Color(0.95f, 0.92f, 0.86f);
            p.floorColor = new Color(0.82f, 0.78f, 0.72f);
            for (int f = 0; f < 3; f++) p.rooms.Add(new Room[0]);

            MapKit.KitSpec t = Register(new MapKit.KitSpec
            {
                name = "Zenith Tower", style = "Classic", origin = new Vector3(PODIUM.x, PODIUM.y + 2 * MapKit.STOREY + 0.02f, -205f), yaw = 180, width = 8, depth = 6, floors = TOWER_FLOORS,
                roof = Roof.Flat, wall = ZENITH_STONE, roofTint = new Color(0.6f, 0.6f, 0.62f), zone = 2, seed = 5150, navigable = true, tallWindows = true, windowChance = 0.9f, balconyChance = 0.12f,
                interiorWall = new Color(0.95f, 0.92f, 0.86f), floorColor = new Color(0.55f, 0.42f, 0.34f)
            });
            Doors(t, 2, 2, 2, 5, 3, 1);
            foreach (int f in new[] { 2, 5, 8, 13 }) t.upperDoors.Add(new Vector3Int(1, f == 13 ? 1 : 2, f));
            for (int f = 0; f < TOWER_FLOORS; f++)
            {
                if (f == 0) t.rooms.Add(new[] { Room.Lounge, Room.Bar });
                else if (f == 7) t.rooms.Add(new[] { Room.Dining, Room.Bar });
                else if (f == 8) t.rooms.Add(new[] { Room.Office, Room.Lounge });
                else if (f == 16) t.rooms.Add(new[] { Room.Office, Room.Bedroom });
                else if (f == 17) t.rooms.Add(new[] { Room.Ballroom, Room.Bedroom });
                else t.rooms.Add(f % 3 == 0 ? new[] { Room.Bedroom, Room.Bathroom } : f % 2 == 0 ? new[] { Room.Bedroom, Room.Lounge } : new[] { Room.Bedroom, Room.Bedroom });
            }

            MapKit.KitSpec lGate = Lot("Zenith Gatehouse", "Classic", 288f, -131f, 180, 2, 2, 1, Roof.Flat, ZENITH_STONE, new Color(0.6f, 0.6f, 0.62f), 2, YARD, 3f);
            Doors(lGate, 0, 0);
            lGate.rooms.Add(new[] { Room.Security });
            MapKit.KitSpec lStaff = Lot("Zenith Staff House", "Stucco", 350f, -232f, 0, 5, 4, 2, Roof.Flat, new Color(0.92f, 0.86f, 0.76f), new Color(0.6f, 0.6f, 0.62f), 2, YARD, 3f);
            Doors(lStaff, 0, 2, 2, 1);
            Floors(lStaff, Room.Kitchen, Room.Lounge, Room.Bedroom, Room.Bathroom);
            MapKit.KitSpec lClub = Lot("Marina Yacht Club", "Stucco", 214f, -298f, 0, 6, 4, 2, Roof.Flat, new Color(0.98f, 0.97f, 0.95f), new Color(0.6f, 0.6f, 0.62f), 2, YARD, 3f);
            Doors(lClub, 0, 2, 0, 3, 2, 1);
            lClub.balconyChance = 0.3f;
            lClub.climb = new Vector2Int(3, 1);
            Floors(lClub, Room.Bar, Room.Lounge, Room.Office, Room.Bathroom);
            MapKit.KitSpec lBeach = Lot("Azure Beach Club", "Stucco", 420f, -334f, 0, 8, 4, 1, Roof.Flat, new Color(0.97f, 0.95f, 0.9f), new Color(0.75f, 0.62f, 0.45f), 2, YARD, 5f);
            Doors(lBeach, 0, 3, 0, 4, 2, 1, 3, 1);
            lBeach.arches.Add(new Vector2Int(0, 1));
            lBeach.arches.Add(new Vector2Int(0, 6));
            Floors(lBeach, Room.Bar, Room.Bathroom);
            for (int i = 0; i < 2; i++)
            {
                MapKit.KitSpec c = Lot("Beach Cabana " + i, "Stucco", 443f + i * 22f, -335f, 0, 2, 2, 1, Roof.Flat, new Color(0.97f, 0.95f, 0.9f), new Color(0.75f, 0.62f, 0.45f), 2, YARD, 4f);
                Doors(c, 0, 0);
                c.rooms.Add(new[] { Room.Storage });
            }
        }

        /// <summary>Removes the kit's roof clutter (air handler, crate) inside a rectangle of a roof.</summary>
        private static void ClearRoof(MapKit.KitBuilding pB, Rect pArea)
        {
            System.Collections.Generic.List<GameObject> lDoomed = new System.Collections.Generic.List<GameObject>();
            float y = RoofY(pB.spec);
            foreach (Transform lChild in pB.root)
                if ((lChild.name == "AirHandler" || lChild.name == "Crate") && Mathf.Abs(lChild.position.y - y) < 0.5f && pArea.Contains(new Vector2(lChild.position.x, lChild.position.z))) lDoomed.Add(lChild.gameObject);
            foreach (GameObject lObject in lDoomed) Object.DestroyImmediate(lObject);
        }

        private void Zenith()
        {
            const int Z = 2;
            MapKit.KitBuilding p = K.Building(Spec("Zenith Podium"));
            MapKit.KitBuilding t = K.Building(Spec("Zenith Tower"));
            Rect lTowerFoot = Rect.MinMaxRect(292f, -214f, 316f, -196f);
            ClearRoof(p, Expand(lTowerFoot, 1f));
            ClearRoof(t, Rect.MinMaxRect(292f, -214f, 316f, -196f));
            for (float x = 293.5f; x < 316f; x += 3f) OpenParapet(p, new Vector3(x, DeckY, -196f));
            K.Hole(R(280.05f, -231.95f, 327.95f, -196.05f));
            Podium(p);
            PoolDeck();
            Tower(t);
            Scaffold(t);
            Forecourt();
            ServiceYard();
            ConstructionSite();
            CarPark();
            Marina();
            BeachClub();

            K.Zone(Z, K.Map.zoneNames[2], new Vector3(330, 40, -255), new Vector3(330, 170, 310), G(476, -118, 0.1f), new Color(0.75f, 0.6f, 0.3f), HatStyle.Fedora,
                p.Local(-14f, -MapKit.STOREY + 0.1f, 8f));
        }

        /// <summary>The podium inside: lobby (chandelier, reception), lobby bar, restaurant and kitchen; the spa and offices; the basement.</summary>
        private void Podium(MapKit.KitBuilding p)
        {
            const int Z = 2;
            Vector3 Loc(float x, int f, float z) => p.Local(x, f * MapKit.STOREY, z);
            System.Random r = new System.Random(77);
            for (int f = -1; f < 2; f++)
            {
                K.Sprinklers(Loc(0, f, 0), new Vector3(p.W, MapKit.STOREY, p.D), p.Yaw, MapKit.STOREY - 0.05f);
                for (int i = 0; i < 4; i++) K.Lamp(Loc(-18f + i * 12f, f, -9f) + Vector3.up * (MapKit.STOREY - 0.45f), WARM, 12f, 1.2f, false);
                for (int i = 0; i < 3; i++) K.Lamp(Loc(-14f + i * 14f, f, 9f) + Vector3.up * (MapKit.STOREY - 0.45f), WARM, 12f, 1.2f, false);
            }
            // --- ground floor: the lobby (front), the kitchen (east), the lobby bar and the restaurant (south).
            K.PartitionKit(p, 0, new Vector3(-24, 0, 0), new Vector3(-6, 0, 0), 3);
            K.PartitionKit(p, 0, new Vector3(-6, 0, 0), new Vector3(18, 0, 0), 4);
            K.PartitionKit(p, 0, new Vector3(-6, 0, 0), new Vector3(-6, 0, 18), 2);
            K.Furnish(p, 0, Room.Ballroom, new Rect(-15f, -17f, 30f, 15.5f), r);
            K.Prop("Desk", Loc(-9f, 0, -3.5f), p.Yaw, ZENITH_GOLD, MapKit.Collide.Box, true);
            K.Prop("Desk", Loc(-11f, 0, -3.5f), p.Yaw, ZENITH_GOLD, MapKit.Collide.Box, true);
            K.Prop("OfficeChair", Loc(-10f, 0, -2.3f), p.Yaw, null);
            for (int i = 0; i < 2; i++)
            {
                K.Prop("Sofa", Loc(-19f, 0, -12f + i * 6f), p.Yaw + 90f, new Color(0.25f, 0.3f, 0.45f), MapKit.Collide.Box, true);
                K.Prop("Sofa", Loc(19f, 0, -12f + i * 6f), p.Yaw - 90f, new Color(0.25f, 0.3f, 0.45f), MapKit.Collide.Box, true);
                K.Prop("PlantBig", Loc(i == 0 ? -6f : 6f, 0, -16.5f), p.Yaw, null);
            }
            K.Activity(ActivityType.Phone, Loc(14f, 0, -9f), p.Yaw, Z);
            // Lobby dressing: two rows of columns, a planter island under the chandelier's light, seating groups, the concierge counter.
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++) K.Piece("Classic", "Column", Loc(s * 7.5f, 0, -15f + k * 5.5f), p.Yaw, null, p.root, MapKit.Collide.Box);
            K.Mesh(K.Env, "LobbyPlanter", CylMesh, Loc(0, 0, -14f) + Vector3.up * 0.35f, new Vector3(3.2f, 0.7f, 3.2f), ZENITH_GOLD, null, true);
            K.Prop("PlantBig", Loc(0, 0, -14f) + Vector3.up * 0.7f, 0, null);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 g = Loc(s * 13f, 0, -12.5f);
                K.Prop("Rug", g, p.Yaw, new Color(0.55f, 0.15f, 0.2f), MapKit.Collide.None);
                K.Prop("TableRound", g, p.Yaw, ZENITH_GOLD, MapKit.Collide.Box, true);
                K.Prop("Armchair", g + Quaternion.Euler(0, p.Yaw, 0) * new Vector3(1.3f, 0, 0), p.Yaw - 90f, new Color(0.25f, 0.3f, 0.45f));
                K.Prop("Armchair", g + Quaternion.Euler(0, p.Yaw, 0) * new Vector3(-1.3f, 0, 0), p.Yaw + 90f, new Color(0.25f, 0.3f, 0.45f));
            }
            K.Prop("BarCounter", Loc(-10f, 0, -5f), p.Yaw, ZENITH_GOLD, MapKit.Collide.Box, true);
            // The lobby's back wall: framed paintings between potted palms, wall lights.
            Color[] lArt = { new Color(0.75f, 0.35f, 0.25f), new Color(0.2f, 0.4f, 0.55f), new Color(0.85f, 0.7f, 0.35f), new Color(0.3f, 0.5f, 0.4f) };
            for (int i = 0; i < 6; i++)
            {
                float x = -20f + i * 8f;
                if (Mathf.Abs(x + 13.5f) < 2f || Mathf.Abs(x - 7.5f) < 2f) x += 3f;
                K.Block(K.Props, "Frame", Loc(x, 0, -0.32f) + Vector3.up * 2f, new Vector3(2.2f, 1.5f, 0.08f), ZENITH_GOLD, GameLayers.DEFAULT, false, Quaternion.Euler(0, p.Yaw, 0));
                K.Block(K.Props, "Painting", Loc(x, 0, -0.37f) + Vector3.up * 2f, new Vector3(1.9f, 1.2f, 0.04f), lArt[i % lArt.Length], GameLayers.DEFAULT, false, Quaternion.Euler(0, p.Yaw, 0));
                float lPlant = Mathf.Abs(x + 3f - 7.5f) < 2.5f || Mathf.Abs(x + 3f + 13.5f) < 2.5f ? x - 3f : x + 3f;
                K.Prop("PlantBig", Loc(lPlant, 0, -0.9f), p.Yaw, null);
            }
            K.Furnish(p, 0, Room.Kitchen, new Rect(-23.5f, 1f, 17f, 16.5f), r);
            K.Activity(ActivityType.Inspect, Loc(-15f, 0, 5f) + Vector3.up * 0.1f, p.Yaw + 180f, Z);
            K.Furnish(p, 0, Room.Bar, new Rect(-5.5f, 1f, 11f, 9f), r);
            K.Furnish(p, 0, Room.Dining, new Rect(6f, 1f, 11f, 7.5f), r);
            K.PartitionKit(p, 0, new Vector3(-6, 0, 11), new Vector3(6, 0, 11), 2);
            K.Furnish(p, 0, Room.Bathroom, new Rect(-5.5f, 11.8f, 10f, 5.7f), r);
            K.Npc(NpcRole.Guard, Loc(-3f, 0, -15f) + Vector3.up * 0.1f, p.Yaw, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, Loc(3f, 0, -15f) + Vector3.up * 0.1f, p.Yaw, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, Loc(16f, 0, -6f) + Vector3.up * 0.1f, p.Yaw + 90f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(-12f, 0, 12f) + Vector3.up * 0.1f, p.Yaw + 270f, Z, ItemKind.Shotgun);
            K.UniqueWeapon(ItemKind.Flamethrower, Loc(-22f, 0, 16f) + Vector3.up * 0.15f, "Zenith kitchen");
            K.Gadget(ItemKind.EmeticPoison, Loc(-20f, 0, 2f) + Vector3.up * 1.0f);

            // --- first floor: the spa and gym (front), the offices and the security room (back).
            K.PartitionKit(p, 1, new Vector3(-24, 0, 0), new Vector3(18, 0, 0), 11);
            K.PartitionKit(p, 1, new Vector3(-6, 0, 0), new Vector3(-6, 0, 18), 2);
            K.PartitionKit(p, 1, new Vector3(6, 0, 0), new Vector3(6, 0, 18), 2);
            K.PartitionKit(p, 1, new Vector3(-4, 0, -18), new Vector3(-4, 0, 0), 3);
            K.Furnish(p, 1, Room.Gym, new Rect(-23f, -17f, 18f, 15f), r);
            K.Activity(ActivityType.Workout, Loc(-18f, 1, -6f) + Vector3.up * 0.1f, p.Yaw, Z);
            K.Furnish(p, 1, Room.Lounge, new Rect(-3f, -17f, 20f, 15f), r);
            K.Furnish(p, 1, Room.Office, new Rect(-23.5f, 1f, 8f, 16f), r);
            K.Furnish(p, 1, Room.Office, new Rect(-15f, 1f, 8.5f, 16f), r);
            K.Furnish(p, 1, Room.Security, new Rect(-5.5f, 1f, 11f, 16f), r);
            K.Furnish(p, 1, Room.Bathroom, new Rect(6.5f, 1f, 11f, 8f), r);
            K.Npc(NpcRole.Guard, Loc(0f, 1, -2f) + Vector3.up * 0.1f, p.Yaw, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(-10f, 1, 2f) + Vector3.up * 0.1f, p.Yaw + 180f, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, Loc(-20f, 1, 16f) + Vector3.up * 0.15f);
            K.Gadget(ItemKind.C4, Loc(4f, 1, 16f) + Vector3.up * 0.15f);

            // --- basement: the parking (ramp doors on the east wall), the utility room (breaker, generator, air handler).
            K.PartitionKit(p, -1, new Vector3(6, 0, -18), new Vector3(6, 0, 0), 3);
            K.PartitionKit(p, -1, new Vector3(6, 0, 0), new Vector3(18, 0, 0), 2);
            K.Furnish(p, -1, Room.Utility, new Rect(7f, -17f, 16f, 16f), r);
            K.VentKit(p, Loc(15f, -1, -6f), p.Yaw);
            for (int i = 0; i < 6; i++)
            {
                Vector3 lCar = Loc(-14f + (i % 3) * 8f, -1, i < 3 ? -12f : 4f);
                K.Car(lCar, p.Yaw + (i < 3 ? 0f : 180f), new[] { new Color(0.1f, 0.1f, 0.12f), new Color(0.85f, 0.85f, 0.88f), new Color(0.6f, 0.1f, 0.12f) }[i % 3]);
                K.Block(K.Env, "Bay", lCar + Vector3.up * 0.01f + Quaternion.Euler(0, p.Yaw, 0) * new Vector3(1.6f, 0, 0), new Vector3(0.12f, 0.02f, 5f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(0, p.Yaw, 0));
            }
            K.Distraction(Loc(-6f, -1, -14.8f), p.Yaw, "caralarm");
            K.Npc(NpcRole.Guard, Loc(0f, -1, -4f) + Vector3.up * 0.1f, p.Yaw + 90f, Z, ItemKind.Smg);
            K.Activity(ActivityType.Smoke, Loc(-21f, -1, -4f) + Vector3.up * 0.1f, p.Yaw + 270f, Z);
            K.Weapon(ItemKind.Shotgun, Loc(10f, -1, 12f) + Vector3.up * 0.15f);
            K.Gadget(ItemKind.Poison, Loc(20f, -1, -15f) + Vector3.up * 0.95f);

            // The front: the portico over the main doors, the gold name band.
            Vector3 lDoor = Facade(p, 0, 7.5f, 0, 0);
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 2; k++) K.Piece("Classic", "Column", lDoor + new Vector3(s * (3f + k * 3f), 0, 4.5f), 0, null, K.Env, MapKit.Collide.Box);
            K.Block(K.Env, "Portico", lDoor + new Vector3(0, 3.55f, 2.6f), new Vector3(14f, 0.4f, 5.6f), ZENITH_STONE);
            K.Block(K.Env, "NameBand", lDoor + new Vector3(0, 4.2f, 5.42f), new Vector3(10f, 0.9f, 0.1f), ZENITH_GOLD);
        }

        /// <summary>The pool deck on the podium roof: the pool, loungers, the pool bar, yoga mats, planters; guards and a patrol.</summary>
        private void PoolDeck()
        {
            const int Z = 2;
            float y = DeckY;
            // The pool, raised in a stone rim on the south terrace.
            Vector3 c = new Vector3(307f, y, -223.5f);
            const float PW = 22f, PD = 7f;
            for (int s = 0; s < 4; s++)
            {
                bool lAlong = s % 2 == 0;
                Vector3 o = lAlong ? new Vector3(0, 0, (s == 0 ? -1 : 1) * (PD * 0.5f + 0.25f)) : new Vector3((s == 1 ? 1 : -1) * (PW * 0.5f + 0.25f), 0, 0);
                K.Block(K.Env, "PoolRim", c + o + Vector3.up * 0.35f, lAlong ? new Vector3(PW + 1f, 0.7f, 0.5f) : new Vector3(0.5f, 0.7f, PD), STONE, GameLayers.DEFAULT, true, null, true);
            }
            K.Block(K.Env, "PoolFloor", c + Vector3.up * 0.06f, new Vector3(PW, 0.12f, PD), new Color(0.35f, 0.7f, 0.8f));
            K.Water(c + Vector3.up * 0.55f, new Vector2(PW, PD));
            for (int i = 0; i < 8; i++)
            {
                Vector3 l = new Vector3(298f + i * 3f, y, -229.2f);
                K.Block(K.Props, "Lounger", l + Vector3.up * 0.2f, new Vector3(0.8f, 0.4f, 2f), Color.white, GameLayers.DEFAULT, true);
                K.Block(K.Props, "LoungerBack", l + new Vector3(0, 0.55f, -0.8f), new Vector3(0.8f, 0.08f, 0.7f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(-40f, 0, 0));
                if (i % 2 == 0) Shade(l + new Vector3(1.5f, 0, 0.5f), 0, FABRIC[(i / 2) % FABRIC.Length], false);
            }
            // The pool bar on the east terrace, a lounge corner, yoga on the west terrace.
            K.Prop("BarCounter", new Vector3(322f, y, -205f), 270, new Color(0.35f, 0.24f, 0.18f), MapKit.Collide.Box, true);
            K.Prop("BarShelf", new Vector3(325.8f, y, -205f), 270, null);
            for (int i = 0; i < 3; i++) K.Prop("Stool", new Vector3(320.6f, y, -206.2f + i * 1.2f), 90, null);
            K.DrinksOn(new Vector3(322f, y + 1.15f, -205f), 90, Z, 3);
            for (int i = 0; i < 3; i++) Shade(new Vector3(318.5f + i * 3.5f, y, -199f), 0, FABRIC[i + 1], true);
            for (int i = 0; i < 3; i++)
            {
                Vector3 m = new Vector3(285f, y, -215f - i * 3f);
                K.Prop("Rug", m + Vector3.up * 0.02f, 90, new Color(0.3f, 0.55f, 0.6f), MapKit.Collide.None);
            }
            K.Activity(ActivityType.Workout, new Vector3(285f, y + 0.1f, -218f), 90, Z);
            for (int i = 0; i < 6; i++)
            {
                Vector3 pl = new Vector3(i < 3 ? 282f : 326f, y, -199f - (i % 3) * 5f);
                K.Prop("PlantBig", pl, 0, null);
            }
            for (int i = 0; i < 5; i++) Nat("PalmA", new Vector3(290f + i * 8f, y, -231f), 0.45f, -1f, null, MapKit.Collide.Box);
            K.Activity(ActivityType.Garden, new Vector3(296f, y + 0.1f, -230f), 180, Z);
            K.Activity(ActivityType.Phone, new Vector3(326f, y + 0.1f, -226f), 90, Z);
            for (int i = 0; i < 4; i++) { Vector3 lPost = new Vector3(i < 2 ? 285f : 325f, y, i % 2 == 0 ? -211.5f : -230.8f); K.Prop("LampPostOrnate", lPost, 0, null); K.Lamp(lPost + Vector3.up * 4.1f, WARM, 13f, 1.4f, false); }
            K.Npc(NpcRole.Guard, new Vector3(290f, y + 0.1f, -228f), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, new Vector3(320f, y + 0.1f, -212f), 90, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, new Vector3(288f, y + 0.1f, -200f), 270, Z, ItemKind.Shotgun);
            K.Patrol(Z, ItemKind.Smg, 4f, new Vector3(286f, y + 0.1f, -208f), new Vector3(286f, y + 0.1f, -226f), new Vector3(322f, y + 0.1f, -226f), new Vector3(322f, y + 0.1f, -210f));
            K.Weapon(ItemKind.Pistol, new Vector3(325f, y + 0.15f, -230f));
        }

        /// <summary>Tower dressing: sprinklers on every floor, the Sky Lounge and the penthouse staging, the helipad and the crown.</summary>
        private void Tower(MapKit.KitBuilding t)
        {
            const int Z = 2;
            for (int f = 0; f < TOWER_FLOORS; f++) K.Sprinklers(t.Local(0, f * MapKit.STOREY, 0), new Vector3(t.W, MapKit.STOREY, t.D), t.Yaw, MapKit.STOREY - 0.05f);
            // Tower lobby by the pool: a doorman, the pool lounge bar inside.
            K.Npc(NpcRole.Guard, t.Local(-6f, 0.1f, 6f), t.Yaw + 180f, Z, ItemKind.Pistol);
            // Floors of rooms: a guard here and there, a weapon in a suite, a hidden spot in a wardrobe (from the presets).
            K.Weapon(ItemKind.Pistol, t.Local(-10f, 3 * MapKit.STOREY + 0.15f, -7f));
            K.Weapon(ItemKind.Smg, t.Local(-10f, 11 * MapKit.STOREY + 0.15f, 7f));
            // The Sky Lounge (floor 7): its maître d', the VIP table by the window.
            K.Npc(NpcRole.Guard, t.Local(-4f, 7 * MapKit.STOREY + 0.1f, -1.2f), t.Yaw + 90f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, t.Local(6f, 7 * MapKit.STOREY + 0.1f, 1.2f), t.Yaw + 270f, Z, ItemKind.Pistol);
            K.Prop("Table", t.Local(-9f, 7 * MapKit.STOREY, 7f), t.Yaw, new Color(0.9f, 0.88f, 0.82f), MapKit.Collide.Box, true);
            K.DrinksOn(t.Local(-9f, 7 * MapKit.STOREY + 0.8f, 7f), t.Yaw, Z, 2);
            K.Activity(ActivityType.Smoke, t.Local(-10.5f, 8 * MapKit.STOREY + 0.1f, 3f), t.Yaw + 90f, Z);
            K.Gadget(ItemKind.Poison, t.Local(-2f, 7 * MapKit.STOREY + 1.0f, 6.5f));
            // The penthouse (top floor): her throne room and its chandelier, the office, the bodyguards.
            float lTop = 17 * MapKit.STOREY;
            K.Prop("Desk", t.Local(-8f, lTop, 6f), t.Yaw + 180f, ZENITH_GOLD, MapKit.Collide.Box, true);
            K.Activity(ActivityType.Office, t.Local(-8f, lTop + 0.1f, 4.8f), t.Yaw, Z);
            K.ToiletKit(t.Local(-11f, lTop, 1.8f), t.Yaw + 90f, Z);
            K.Npc(NpcRole.Guard, t.Local(-3f, lTop + 0.1f, -1.2f), t.Yaw, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, t.Local(5f, lTop + 0.1f, -1.2f), t.Yaw, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, t.Local(-6f, 16 * MapKit.STOREY + 0.1f, -1.2f), t.Yaw, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Shotgun, t.Local(-10f, lTop + 0.15f, -7f));

            // The roof: the helipad (extraction), its helicopter, the crown frame and the mast; a lookout.
            float y = RoofY(t.spec);
            Vector3 h = new Vector3(305f, y, -204f);
            K.Mesh(K.Env, "Helipad", CylMesh, h + Vector3.up * 0.02f, new Vector3(13f, 0.04f, 13f), new Color(0.3f, 0.32f, 0.34f));
            K.Mesh(K.Env, "HelipadRing", CylMesh, h + Vector3.up * 0.01f, new Vector3(14f, 0.04f, 14f), new Color(0.95f, 0.8f, 0.2f));
            K.Block(K.Env, "H", h + new Vector3(-1.6f, 0.05f, 0), new Vector3(0.6f, 0.02f, 4.4f), Color.white, GameLayers.DEFAULT, false);
            K.Block(K.Env, "H", h + new Vector3(1.6f, 0.05f, 0), new Vector3(0.6f, 0.02f, 4.4f), Color.white, GameLayers.DEFAULT, false);
            K.Block(K.Env, "H", h + new Vector3(0, 0.05f, 0), new Vector3(2.6f, 0.02f, 0.6f), Color.white, GameLayers.DEFAULT, false);
            Vector3 lHeli = h + new Vector3(0, 0, 2.5f);
            K.Mesh(K.Env, "HeliBody", DomeMesh, lHeli + Vector3.up * 1.5f, new Vector3(2.6f, 2.4f, 5f), new Color(0.15f, 0.18f, 0.24f), null, true);
            K.Block(K.Env, "HeliTail", lHeli + new Vector3(0, 1.9f, -4.8f), new Vector3(0.5f, 0.5f, 5.5f), new Color(0.15f, 0.18f, 0.24f));
            K.Block(K.Env, "HeliFin", lHeli + new Vector3(0, 2.6f, -7.3f), new Vector3(0.12f, 1.4f, 0.8f), ZENITH_GOLD);
            K.Block(K.Env, "Rotor", lHeli + new Vector3(0, 2.95f, 0), new Vector3(0.3f, 0.06f, 11f), new Color(0.1f, 0.1f, 0.1f), GameLayers.DEFAULT, false, Quaternion.Euler(0, 30f, 0));
            K.Block(K.Env, "Rotor", lHeli + new Vector3(0, 2.95f, 0), new Vector3(0.3f, 0.06f, 11f), new Color(0.1f, 0.1f, 0.1f), GameLayers.DEFAULT, false, Quaternion.Euler(0, 120f, 0));
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Skid", lHeli + new Vector3(s * 1.1f, 0.12f, 0), new Vector3(0.1f, 0.1f, 4f), new Color(0.2f, 0.2f, 0.2f), GameLayers.DEFAULT, false);
            K.Point(MatchPoint.Kind.Extraction, h + new Vector3(4f, 0.1f, -3f), 0, "Zenith helipad");
            for (int i = 0; i < 6; i++) K.Lamp(h + Quaternion.Euler(0, i * 60f, 0) * Vector3.forward * 7f + Vector3.up * 0.2f, new Color(0.4f, 1f, 0.5f), 4f, 1f, false);
            for (int c = 0; c < 4; c++)
            {
                Vector3 lCorner = new Vector3(c % 2 == 0 ? 292.4f : 315.6f, y, c < 2 ? -213.6f : -196.4f);
                K.Block(K.Env, "Crown", lCorner + Vector3.up * 5.5f, new Vector3(0.9f, 11f, 0.9f), ZENITH_STONE);
            }
            for (int s = 0; s < 2; s++)
            {
                K.Block(K.Env, "CrownBeam", new Vector3(304f, y + 11.2f, s == 0 ? -213.6f : -196.4f), new Vector3(24.1f, 0.7f, 0.9f), ZENITH_GOLD);
                K.Block(K.Env, "CrownBeam", new Vector3(s == 0 ? 292.4f : 315.6f, y + 11.2f, -205f), new Vector3(0.9f, 0.7f, 18.1f), ZENITH_GOLD);
            }
            K.Block(K.Env, "Spire", new Vector3(314f, y + 18f, -198f), new Vector3(0.4f, 36f, 0.4f), new Color(0.85f, 0.85f, 0.88f));
            K.Lamp(new Vector3(314f, y + 36.3f, -198f), new Color(1f, 0.15f, 0.1f), 10f, 2f, false);
            K.Npc(NpcRole.Lookout, new Vector3(296f, y + 0.1f, -199f), 0, Z, ItemKind.Sniper);
            K.Activity(ActivityType.Lookout, new Vector3(313f, y + 0.1f, -211f), 180, Z);
            K.Weapon(ItemKind.Sniper, new Vector3(296f, y + 0.15f, -212f));
            K.UniqueWeapon(ItemKind.RocketLauncher, new Vector3(314f, y + 0.15f, -212.5f), "Zenith roof");
        }

        /// <summary>The west face under renovation: a scaffold from the pool deck to the ninth floor (doors onto it), the gondola above.</summary>
        private void Scaffold(MapKit.KitBuilding t)
        {
            float y0 = t.root.position.y;
            const float X = 291.2f;
            // Three columns of scaffold over modules 1-3 of the west face, nine lifts high.
            for (int m = 0; m < 3; m++)
                for (int k = 0; k < 8; k++)
                    K.Piece("Classic", "Scaffold", new Vector3(X, y0 + k * 3.956f, -199f - m * 3f), 90, null, K.Env, MapKit.Collide.None);
            GameObject lClimb = K.Climbable(K.Env, new Vector3(X - 1f, y0 + 8 * 3.956f * 0.5f, -202f), new Vector3(9f, 8 * 3.956f, 0.4f), Quaternion.Euler(0, 90, 0), Color.white, false);
            lClimb.GetComponent<MeshRenderer>().enabled = false;
            foreach (int f in new[] { 2, 5, 8 })
                Deck(new Vector3(X, y0 + f * MapKit.STOREY + 0.05f, -202f), new Vector2(1.6f, 9f), 0f, 0.12f, new Color(0.55f, 0.45f, 0.32f), 0f);
            K.Block(K.Env, "Tarp", new Vector3(X - 0.7f, y0 + 4f * 3.956f, -202f), new Vector3(0.05f, 6f, 9.2f), new Color(0.2f, 0.45f, 0.65f), GameLayers.DEFAULT, false);
            // The gondola at the thirteenth floor, hung from a davit on the roof; a rope climbs to the roof.
            float lG = y0 + 13 * MapKit.STOREY;
            K.Block(K.Env, "Gondola", new Vector3(X - 0.2f, lG - 0.1f, -201f), new Vector3(1.4f, 0.2f, 5f), new Color(0.75f, 0.75f, 0.78f));
            K.Block(K.Env, "GondolaRail", new Vector3(X - 0.85f, lG + 0.55f, -201f), new Vector3(0.08f, 1.1f, 5f), new Color(0.9f, 0.7f, 0.15f));
            float lRoof = RoofY(t.spec);
            K.Block(K.Env, "Davit", new Vector3(X + 0.6f, lRoof + 1.5f, -201f), new Vector3(2.6f, 0.3f, 0.3f), new Color(0.6f, 0.6f, 0.62f));
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "GondolaCable", new Vector3(X - 0.2f, (lG + lRoof + 1.5f) * 0.5f, -201f + s * 2.2f), new Vector3(0.04f, lRoof + 1.5f - lG, 0.04f), new Color(0.2f, 0.2f, 0.2f), GameLayers.DEFAULT, false);
            GameObject lRope = K.Climbable(K.Env, new Vector3(X - 0.3f, (lG + lRoof + 1f) * 0.5f, -197.8f), new Vector3(0.8f, lRoof + 1f - lG, 0.4f), Quaternion.Euler(0, 90, 0), Color.white, false);
            lRope.GetComponent<MeshRenderer>().enabled = false;
            GameObject lRope2 = K.Climbable(K.Env, new Vector3(X - 0.3f, (y0 + 8 * 3.956f + lG) * 0.5f, -197.8f), new Vector3(0.8f, lG - y0 - 8 * 3.956f + 0.5f, 0.4f), Quaternion.Euler(0, 90, 0), Color.white, false);
            lRope2.GetComponent<MeshRenderer>().enabled = false;
            K.Block(K.Env, "WorkRope", new Vector3(X - 0.3f, (y0 + 8 * 3.956f + lRoof) * 0.5f, -197.8f), new Vector3(0.05f, lRoof - y0 - 8 * 3.956f, 0.05f), new Color(0.85f, 0.55f, 0.15f), GameLayers.DEFAULT, false);
            K.Gadget(ItemKind.SmokeGrenade, new Vector3(X - 0.2f, lG + 0.1f, -202.5f));
        }

        /// <summary>The forecourt: the gatehouse on the Gulf road, the drive and its fountain loop, formal gardens and palms.</summary>
        private void Forecourt()
        {
            const int Z = 2;
            K.Building(Spec("Zenith Gatehouse"));
            for (int s = -1; s <= 1; s += 2) P("Barrier", 304f + s * 3f, -126f, 0, null, true);
            Guard(Z, 299, -128, 0, ItemKind.Smg);
            Guard(Z, 309, -128, 0, ItemKind.Shotgun);
            // The fountain loop, the drop-off, the cars.
            GameObject lFountain = P("Fountain", 304, -160, 0, null, true);
            if (lFountain != null) lFountain.transform.localScale = Vector3.one * 1.8f;
            K.Car(G(300, -190), 90, new Color(0.08f, 0.08f, 0.1f));
            K.Car(G(310, -190), 90, new Color(0.08f, 0.08f, 0.1f));
            K.Car(G(318, -170), 0, new Color(0.85f, 0.85f, 0.88f));
            K.Distraction(G(318, -170, 0.9f), 0, "caralarm");
            Guard(Z, 300, -193, 0, ItemKind.Pistol);
            // Palms down the drive, parterres of hedges and flowers, cypress walls, benches, the gazebo.
            for (int i = 0; i < 6; i++) for (int s = -1; s <= 1; s += 2) Palm(304f + s * 10f, -132f - i * 9f, 1.1f);
            foreach (float x0 in new[] { 256f, 322f })
            {
                for (int r = 0; r < 3; r++)
                    for (int k = 0; k < 2; k++)
                    {
                        Rect lBed = new Rect(x0 + k * 16f, -186f + r * 17f, 12f, 11f);
                        for (int e = 0; e < 4; e++)
                        {
                            bool lAlongX = e % 2 == 0;
                            int lCount = Mathf.RoundToInt((lAlongX ? lBed.width : lBed.height) / 2.3f);
                            for (int i = 0; i < lCount; i++)
                            {
                                if (i == lCount / 2) continue;
                                float u = (i + 0.5f) / lCount;
                                float x = lAlongX ? Mathf.Lerp(lBed.xMin, lBed.xMax, u) : (e == 1 ? lBed.xMax : lBed.xMin);
                                float z = lAlongX ? (e == 0 ? lBed.yMin : lBed.yMax) : Mathf.Lerp(lBed.yMin, lBed.yMax, u);
                                GameObject lHedge = Nat("HedgeBlock", x, z, 1f, lAlongX ? 0f : 90f, new Color(0.3f, 0.5f, 0.26f));
                                if (lHedge != null) lHedge.transform.localScale = new Vector3(1f, 0.5f, 0.7f);
                            }
                        }
                        for (int i = 0; i < 3; i++) Nat("FlowerBed", lBed.center.x + (i - 1) * 2.8f, lBed.center.y, 1.2f, 0f, new[] { new Color(0.95f, 0.45f, 0.55f), new Color(0.98f, 0.85f, 0.35f), new Color(0.75f, 0.45f, 0.9f) }[(i + r + k) % 3], MapKit.Collide.None);
                    }
                for (int i = 0; i < 6; i++) Nat("CypressA", x0 + (x0 < 300f ? -4f : 32f), -186f + i * 10f, 0.75f);
            }
            for (int i = 0; i < 6; i++) P("Bench", i < 3 ? 286f : 322f, -140f - (i % 3) * 17f, i < 3 ? 90f : 270f);
            Vector3 lGazebo = G(268, -132);
            for (int i = 0; i < 6; i++) K.Piece("Classic", "Column", lGazebo + Quaternion.Euler(0, i * 60f, 0) * Vector3.forward * 3f, i * 60f, null, K.Env, MapKit.Collide.Box);
            GameObject lDome = K.Piece("Classic", "RoofHip", lGazebo + Vector3.up * MapKit.STOREY, 0, new Color(0.85f, 0.7f, 0.4f), K.Env, MapKit.Collide.None);
            if (lDome != null) lDome.transform.localScale = new Vector3(7.2f, 2.6f, 7.2f);
            K.Prop("TableRound", lGazebo, 0, Color.white, MapKit.Collide.Box, true);
            K.DrinksOn(lGazebo + Vector3.up * 0.78f, 0, Z, 2);
            K.Activity(ActivityType.Garden, G(270, -150, 0.1f), 90, Z);
            K.Activity(ActivityType.Garden, G(352, -175, 0.1f), 270, Z);
            K.Activity(ActivityType.Phone, G(286, -186, 0.1f), 90, Z);
            for (int i = 0; i < 6; i++) { float x = i < 3 ? 296f : 312f, z = -140f - (i % 3) * 18f; P("LampPostOrnate", x, z, 0); K.Lamp(G(x, z, 4.1f), WARM, 13f, 1.5f, false); }
            Walk(Z, ItemKind.Smg, 4f, new Vector2(260, -130), new Vector2(350, -130), new Vector2(355, -192), new Vector2(258, -192));
            K.Weapon(ItemKind.Smg, G(268, -132, 0.9f));
            K.Gadget(ItemKind.Coin, G(266, -132, 0.1f));
        }

        /// <summary>The service yard: the kitchen door, deliveries, the staff house, dumpsters, the smokers; the basement ramp.</summary>
        private void ServiceYard()
        {
            const int Z = 2;
            K.Building(Spec("Zenith Staff House"));
            K.Car(G(340, -232), 0, new Color(0.9f, 0.9f, 0.88f));
            for (int i = 0; i < 3; i++) Clutter(G(332f, -238f + i * 4f), 90, i);
            K.HidingKit(G(334, -216), 90, "Dumpster");
            K.HidingKit(G(334, -212), 90, "Dumpster");
            K.Activity(ActivityType.Inspect, G(336, -226, 0.1f), 270, Z);
            K.Activity(ActivityType.Smoke, G(342, -190, 0.1f), 0, Z);
            K.Activity(ActivityType.Idle, G(346, -194, 0.1f), 0, Z);
            Guard(Z, 333, -224.5f, 90, ItemKind.Pistol);
            // Ramp walls and the barrier at its top.
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? -206.3f : -197.7f;
                K.Block(K.Env, "RampWall", new Vector3(341f, YARD - 1.5f, z), new Vector3(26f, MapKit.STOREY + 0.6f, 0.6f), CONCRETE);
                Row("Classic", "Railing", new Vector3(330f, YARD + 0.3f, z + (s == 0 ? -0.3f : 0.3f)), new Vector3(352f, YARD + 0.3f, z + (s == 0 ? -0.3f : 0.3f)), 3f, s == 0 ? 180f : 0f, STONE);
            }
            P("Barrier", 354f, -202f, 90, null, true);
            K.Lamp(new Vector3(330.5f, YARD - 0.8f, -202f), WARM, 9f, 1.2f, false);
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(332, -244), new Vector2(360, -244), new Vector2(360, -190), new Vector2(332, -190));
            K.Weapon(ItemKind.Pistol, G(350, -238, 0.15f));
        }

        /// <summary>The construction site west of the tower: a five-storey concrete frame, materials, the site office, the tower crane and its zipline.</summary>
        private void ConstructionSite()
        {
            const int Z = 2;
            Color lConcrete = new Color(0.72f, 0.71f, 0.68f), lYellow = new Color(0.92f, 0.72f, 0.15f);
            const float X0 = 214f, X1 = 241f, Z0 = -232f, Z1 = -205f;
            // Hoarding around the site, a gate on the north.
            K.Fence(G(200, -252, -0.2f), G(268, -252, -0.2f), 2.4f, new Color(0.35f, 0.5f, 0.6f), true);
            K.Fence(G(200, -252, -0.2f), G(200, -170, -0.2f), 2.4f, new Color(0.35f, 0.5f, 0.6f), true);
            K.Fence(G(200, -170, -0.2f), G(226, -170, -0.2f), 2.4f, new Color(0.35f, 0.5f, 0.6f), true);
            K.Fence(G(238, -170, -0.2f), G(268, -170, -0.2f), 2.4f, new Color(0.35f, 0.5f, 0.6f), true);
            K.Fence(G(268, -252, -0.2f), G(268, -224, -0.2f), 2.4f, new Color(0.35f, 0.5f, 0.6f), true);
            K.Fence(G(268, -212, -0.2f), G(268, -170, -0.2f), 2.4f, new Color(0.35f, 0.5f, 0.6f), true);
            // The frame: columns on a 9 m grid, slabs with a stair opening, straight flights.
            float g0 = Surf(227, -218);
            for (int f = 0; f < 5; f++)
            {
                float y = g0 + f * MapKit.STOREY;
                for (int i = 0; i < 4; i++)
                    for (int k = 0; k < 4; k++)
                        K.Block(K.Env, "Column", new Vector3(X0 + i * 9f, y + MapKit.STOREY * 0.5f, Z0 + k * 9f), new Vector3(0.6f, MapKit.STOREY, 0.6f), lConcrete);
                float ys = y + MapKit.STOREY;
                // Slab with the opening over x 233-239, z -229..-219.
                K.Block(K.Env, "Slab", new Vector3((X0 - 0.5f + 233f) * 0.5f, ys - 0.15f, (Z0 + Z1) * 0.5f), new Vector3(233f - X0 + 0.5f, 0.3f, Z1 - Z0 + 1f), lConcrete);
                K.Block(K.Env, "Slab", new Vector3((239f + X1 + 0.5f) * 0.5f, ys - 0.15f, (Z0 + Z1) * 0.5f), new Vector3(X1 + 0.5f - 239f, 0.3f, Z1 - Z0 + 1f), lConcrete);
                K.Block(K.Env, "Slab", new Vector3(236f, ys - 0.15f, (Z0 - 0.5f + -229f) * 0.5f), new Vector3(6f, 0.3f, -229f - Z0 + 0.5f), lConcrete);
                K.Block(K.Env, "Slab", new Vector3(236f, ys - 0.15f, (-219f + Z1 + 0.5f) * 0.5f), new Vector3(6f, 0.3f, Z1 + 0.5f - -219f), lConcrete);
                K.Stairs(K.Env, new Vector3(236f, y, -228.8f), new Vector3(236f, ys, -219.1f), 2.4f, lConcrete * 0.92f);
                if (f % 2 == 1) K.Block(K.Env, "EdgeRail", new Vector3((X0 + X1) * 0.5f, ys + 0.5f, Z1 + 0.4f), new Vector3(X1 - X0, 0.12f, 0.12f), lYellow, GameLayers.DEFAULT, false);
                if (f == 2) K.Block(K.Env, "EdgeRail", new Vector3(X0 - 0.4f, ys + 0.5f, (Z0 + Z1) * 0.5f), new Vector3(0.12f, 0.12f, Z1 - Z0), lYellow, GameLayers.DEFAULT, false);
            }
            K.Block(K.Env, "GroundSlab", new Vector3((X0 + X1) * 0.5f, g0 - 0.1f, (Z0 + Z1) * 0.5f), new Vector3(X1 - X0 + 1f, 0.3f, Z1 - Z0 + 1f), lConcrete * 0.95f);
            float lTopSlab = g0 + 5 * MapKit.STOREY;
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 4; k++)
                    for (int b = 0; b < 3; b++) K.Block(K.Props, "Rebar", new Vector3(X0 + i * 9f + (b - 1) * 0.15f, lTopSlab + 0.7f, Z0 + k * 9f), new Vector3(0.04f, 1.4f, 0.04f), new Color(0.45f, 0.3f, 0.22f), GameLayers.DEFAULT, false);
            for (int k = 0; k < 4; k++) K.Piece("Classic", "Scaffold", new Vector3(X0 + 13.5f, g0 + k * 3.956f, Z0 - 0.9f), 180, null, K.Env, MapKit.Collide.Mesh);
            K.Activity(ActivityType.Inspect, new Vector3(222f, lTopSlab + 0.1f, -214f), 0, Z);
            K.Npc(NpcRole.Lookout, new Vector3(220f, lTopSlab + 0.1f, -210f), 0, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, new Vector3(238f, lTopSlab + 0.15f, -210f));
            // Materials: cement sacks, brick pallets, pipes, the mixer, a skip.
            for (int i = 0; i < 4; i++) { K.Prop("Pallet", G(248f + i * 2f, -240f), 0); Sacks(G(248f + i * 2f, -240f, 0.15f), 0, 3); }
            for (int i = 0; i < 3; i++) K.Block(K.Props, "Bricks", G(246f + i * 2.2f, -184f, 0.45f), new Vector3(1.6f, 0.9f, 1f), new Color(0.72f, 0.38f, 0.28f), GameLayers.DEFAULT, true, null, true);
            for (int i = 0; i < 5; i++) K.Mesh(K.Props, "Pipe", CylMesh, G(210f, -190f + i * 0.7f, 0.35f), new Vector3(0.6f, 9f, 0.6f), new Color(0.5f, 0.52f, 0.55f), Quaternion.Euler(0, 0, 90));
            K.Mesh(K.Props, "Mixer", CylMesh, G(252, -196, 1.4f), new Vector3(1.6f, 2.2f, 1.6f), new Color(0.85f, 0.4f, 0.15f), Quaternion.Euler(0, 0, 60), true);
            K.Block(K.Props, "MixerBase", G(252, -196, 0.4f), new Vector3(1.8f, 0.8f, 2.6f), new Color(0.3f, 0.3f, 0.32f), GameLayers.DEFAULT, true, null, true);
            K.HidingKit(G(258, -228), 0, "Dumpster");
            for (int i = 0; i < 4; i++) K.Barrel(G(206f + (i % 2) * 0.8f, -246f + (i / 2) * 0.8f), true);
            // The site office: an open container with plans, a portable toilet, a water cooler.
            Vector3 o = G(254, -180);
            Color lBox = new Color(0.95f, 0.95f, 0.92f);
            K.Block(K.Env, "OfficeFloor", o + Vector3.up * 0.1f, new Vector3(6.2f, 0.2f, 2.5f), lBox * 0.8f);
            K.Block(K.Env, "OfficeRoof", o + Vector3.up * 2.55f, new Vector3(6.2f, 0.12f, 2.5f), lBox);
            K.Block(K.Env, "OfficeBack", o + new Vector3(0, 1.3f, 1.2f), new Vector3(6.2f, 2.6f, 0.1f), lBox);
            for (int k = -1; k <= 1; k += 2) K.Block(K.Env, "OfficeSide", o + new Vector3(k * 3.05f, 1.3f, 0), new Vector3(0.1f, 2.6f, 2.5f), lBox);
            K.Prop("Desk", o + new Vector3(-1f, 0.2f, 0.5f), 180, null, MapKit.Collide.Box, true);
            K.Block(K.Props, "Plans", o + new Vector3(-1f, 1.04f, 0.5f), new Vector3(1.2f, 0.02f, 0.7f), new Color(0.75f, 0.85f, 0.95f), GameLayers.DEFAULT, false);
            K.Activity(ActivityType.Office, o + new Vector3(-1f, 0.3f, -0.6f), 0, Z);
            K.DrinksOn(o + new Vector3(1.8f, 1.05f, 0.6f), 180, Z, 2);
            K.Block(K.Props, "Cooler", o + new Vector3(1.8f, 0.6f, 0.6f), new Vector3(0.6f, 0.8f, 0.6f), new Color(0.3f, 0.5f, 0.8f), GameLayers.DEFAULT, true);
            K.Block(K.Env, "PortaLoo", G(262, -178, 1.2f), new Vector3(1.3f, 2.4f, 1.3f), new Color(0.2f, 0.4f, 0.7f));
            K.ToiletKit(G(262, -180.2f), 180, Z);
            Guard(Z, 232, -172, 0, ItemKind.Shotgun);
            K.Gadget(ItemKind.Brick, G(246, -186, 0.95f));
            K.Gadget(ItemKind.C4, G(250, -241, 1.0f));

            // The tower crane: lattice mast with a ladder, the cab and its platform, the jib towards the Zenith.
            Vector3 m = G(226, -244);
            const float MAST = 52f;
            for (int c = 0; c < 4; c++) K.Block(K.Env, "MastLeg", m + new Vector3(c % 2 == 0 ? -1f : 1f, MAST * 0.5f, c < 2 ? -1f : 1f), new Vector3(0.25f, MAST, 0.25f), lYellow);
            for (float y = 2f; y < MAST; y += 4f)
                for (int s = 0; s < 4; s++)
                {
                    Quaternion q = Quaternion.Euler(0, s * 90f, 0);
                    K.Block(K.Env, "MastBrace", m + Vector3.up * y + q * new Vector3(0, 0, 1f), new Vector3(2.8f, 0.12f, 0.12f), lYellow, GameLayers.DEFAULT, false, q * Quaternion.Euler(0, 0, 45f));
                }
            GameObject lMastCollider = K.Block(K.Env, "MastCollider", m + Vector3.up * MAST * 0.5f, new Vector3(2.2f, MAST, 2.2f), lYellow);
            lMastCollider.GetComponent<MeshRenderer>().enabled = false;
            Ladder(m + new Vector3(0, 0, -1.25f), MAST + 0.4f, 180f, "Classic");
            Vector3 lPlat = m + Vector3.up * (MAST + 0.25f);
            K.Block(K.Env, "CranePlatform", lPlat - Vector3.up * 0.15f, new Vector3(5f, 0.3f, 5f), new Color(0.4f, 0.42f, 0.44f));
            for (int s = 0; s < 4; s++)
            {
                Quaternion q = Quaternion.Euler(0, s * 90f, 0);
                if (s == 2) continue;
                K.Block(K.Env, "PlatformRail", lPlat + Vector3.up * 0.55f + q * new Vector3(0, 0, 2.45f), new Vector3(5f, 1.1f, 0.08f), lYellow, GameLayers.DEFAULT, true, q);
            }
            K.Block(K.Env, "Jib", lPlat + new Vector3(24f, 2.2f, 0), new Vector3(46f, 1.4f, 1.4f), lYellow);
            K.Block(K.Env, "CounterJib", lPlat + new Vector3(-9f, 2.2f, 0), new Vector3(14f, 1.2f, 1.6f), lYellow);
            K.Block(K.Env, "Counterweight", lPlat + new Vector3(-14f, 0.6f, 0), new Vector3(3f, 2.4f, 2.4f), CONCRETE);
            K.Block(K.Env, "CraneCab", lPlat + new Vector3(1.6f, 1.2f, -2.1f), new Vector3(2f, 2.2f, 1.8f), new Color(0.92f, 0.92f, 0.9f));
            K.Block(K.Env, "Apex", lPlat + new Vector3(0, 6f, 0), new Vector3(0.8f, 8f, 0.8f), lYellow);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Pendant", lPlat + new Vector3(s * 10f, 6f, 0), new Vector3(0.05f, 0.05f, 22f), new Color(0.2f, 0.2f, 0.2f), GameLayers.DEFAULT, false, Quaternion.LookRotation(new Vector3(s * 20f, -7.4f, 0)));
            Vector3 lHook = lPlat + new Vector3(30f, 0, 0);
            K.Block(K.Env, "HookCable", new Vector3(lHook.x, (lHook.y + 1.5f + G(lHook.x, lHook.z).y + 14f) * 0.5f, lHook.z), new Vector3(0.05f, lHook.y + 1.5f - G(lHook.x, lHook.z).y - 14f, 0.05f), new Color(0.2f, 0.2f, 0.2f), GameLayers.DEFAULT, false);
            K.Block(K.Env, "HookLoad", new Vector3(lHook.x, G(lHook.x, lHook.z).y + 13.5f, lHook.z), new Vector3(2.4f, 1f, 2.4f), new Color(0.72f, 0.38f, 0.28f));
            K.Lamp(lPlat + new Vector3(0, 10.2f, 0), new Color(1f, 0.2f, 0.15f), 8f, 1.6f, false);
            K.Npc(NpcRole.Lookout, lPlat + new Vector3(-1.2f, 0.1f, 1.2f), 90f, Z, ItemKind.Sniper);
            K.Zipline(lPlat + new Vector3(1.5f, 0.05f, 1.2f), new Vector3(284f, DeckY + 0.05f, -199.5f));
        }

        /// <summary>The visitors' car park west of the forecourt: bays, parked cars, palms, a valet booth.</summary>
        private void CarPark()
        {
            Color[] lCars = { new Color(0.08f, 0.08f, 0.1f), new Color(0.85f, 0.85f, 0.88f), new Color(0.55f, 0.1f, 0.12f), new Color(0.2f, 0.3f, 0.45f), new Color(0.75f, 0.7f, 0.6f) };
            for (int i = 0; i < 9; i++)
            {
                float x = 206f + i * 4.2f;
                if (i % 4 != 3) K.Car(G(x, -142f), 0f, lCars[i % lCars.Length]);
                K.Block(K.Env, "Bay", G(x + 2.1f, -142f, 0.01f), new Vector3(0.12f, 0.02f, 5f), Color.white, GameLayers.DEFAULT, false);
                if (i % 3 != 1) K.Car(G(x, -156f), 180f, lCars[(i + 2) % lCars.Length]);
            }
            for (int i = 0; i < 6; i++) Palm(204f + i * 8f, -149f, 0.9f);
            K.Block(K.Env, "ValetBooth", G(246, -133, 1.2f), new Vector3(2f, 2.4f, 2f), Color.white, GameLayers.DEFAULT, true, null, true);
            K.Block(K.Env, "ValetRoof", G(246, -133, 2.5f), new Vector3(2.6f, 0.2f, 2.6f), ZENITH_GOLD);
            K.Distraction(G(222, -142, 0.9f), 0, "caralarm");
        }

        /// <summary>A motor yacht: a scaled hull, a deck, a white cabin with a window band, a flybridge, a mast.</summary>
        private void Yacht(Vector3 pAt, float pYaw, float pScale, Color pAccent)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            GameObject lHull = K.Prop("Boat", pAt, pYaw, Color.white, MapKit.Collide.Box);
            if (lHull != null) lHull.transform.localScale = new Vector3(2.6f, 1.8f, 2.6f) * pScale;
            float lLen = 4.76f * 2.6f * pScale, lBeam = 2.06f * 2.6f * pScale, lTop = pAt.y + 1.24f * 1.8f * pScale;
            K.Block(K.Env, "YachtDeck", pAt + Vector3.up * (lTop - pAt.y - 0.1f), new Vector3(lBeam * 0.82f, 0.2f, lLen * 0.8f), new Color(0.8f, 0.68f, 0.52f), GameLayers.DEFAULT, true, r);
            K.Block(K.Env, "YachtCabin", pAt + r * new Vector3(0, lTop - pAt.y + 0.9f, -lLen * 0.06f), new Vector3(lBeam * 0.6f, 1.8f, lLen * 0.38f), Color.white, GameLayers.DEFAULT, true, r);
            K.Block(K.Env, "YachtGlass", pAt + r * new Vector3(0, lTop - pAt.y + 1.15f, -lLen * 0.06f), new Vector3(lBeam * 0.61f, 0.6f, lLen * 0.36f), new Color(0.12f, 0.16f, 0.22f), GameLayers.DEFAULT, false, r);
            K.Block(K.Env, "Flybridge", pAt + r * new Vector3(0, lTop - pAt.y + 1.9f, -lLen * 0.1f), new Vector3(lBeam * 0.66f, 0.15f, lLen * 0.3f), pAccent, GameLayers.DEFAULT, false, r);
            K.Block(K.Env, "YachtMast", pAt + r * new Vector3(0, lTop - pAt.y + 3f, -lLen * 0.1f), new Vector3(0.12f, 2.2f, 0.12f), Color.white, GameLayers.DEFAULT, false, r);
            K.Block(K.Env, "HullStripe", pAt + r * new Vector3(0, lTop - pAt.y - 0.5f, 0), new Vector3(lBeam * 0.985f, 0.18f, lLen * 0.7f), pAccent, GameLayers.DEFAULT, false, r);
        }

        /// <summary>The marina: the quay promenade, four pontoons with yachts, the breakwater and its light, the speedboat extraction, the yacht club.</summary>
        private void Marina()
        {
            const int Z = 2;
            float lPontoon = SEA + 0.55f;
            K.Block(K.Env, "QuayEdge", new Vector3(264f, (SEA_FLOOR + YARD) * 0.5f + 0.01f, -342.1f), new Vector3(176f, YARD - SEA_FLOOR + 0.02f, 5f), STONE * 0.85f);
            for (float x = 180f; x < 350f; x += 10f) if (Mathf.Abs(Mathf.Repeat(x - 200f + 17.5f, 35f) - 17.5f) > 3f) K.Prop("Bollard", new Vector3(x, YARD + 0.02f, -344.2f), 0);
            Color[] lAccents = { new Color(0.15f, 0.25f, 0.45f), new Color(0.75f, 0.6f, 0.3f), new Color(0.2f, 0.45f, 0.5f), new Color(0.5f, 0.15f, 0.2f) };
            for (int i = 0; i < 4; i++)
            {
                float x = 200f + i * 35f;
                // The ramp starts at the quay block's edge (the block covers the last sloping ground cells).
                K.Ramp(K.Env, new Vector3(x, YARD + 0.02f, -344.6f), new Vector3(x, lPontoon, -354f), 2.4f, PLANK * 0.9f, 0.25f);
                Deck(new Vector3(x, lPontoon, -375f), new Vector2(3f, 42f), 0f, 0.4f, PLANK, 1.5f);
                for (int k = 0; k < 3; k++) Deck(new Vector3(x + (k % 2 == 0 ? 6.5f : -6.5f), lPontoon, -360f - k * 13f), new Vector2(10f, 1.6f), 0f, 0.3f, PLANK, 0f);
                // Yachts between the fingers, bows to the sea.
                Yacht(new Vector3(x + 6.2f, SEA - 0.35f, -373f), 180f, 0.95f + (i % 2) * 0.15f, lAccents[i % 4]);
                Yacht(new Vector3(x - 6.2f, SEA - 0.35f, -386f), 180f, 0.85f + ((i + 1) % 3) * 0.12f, lAccents[(i + 2) % 4]);
                for (int k = 0; k < 2; k++) K.Lamp(new Vector3(x + 1.3f, lPontoon, -360f - k * 22f), WARM, 9f, 1.2f, true);
            }
            K.Prop("Boat", new Vector3(305f, SEA - 0.2f, -399f), 90f, new Color(0.85f, 0.2f, 0.15f), MapKit.Collide.Box);
            K.Point(MatchPoint.Kind.Extraction, new Vector3(305f, lPontoon + 0.1f, -394f), 180f, "Marina speedboat");
            // The breakwater and its light.
            K.Block(K.Env, "Breakwater", new Vector3(252f, (SEA_FLOOR + 0.6f) * 0.5f, -405f), new Vector3(152f, 0.6f - SEA_FLOOR, 5f), STONE * 0.8f);
            for (int i = 0; i < 16; i++) Nat("RockD", 180f + i * 9.5f + Rand(-1f, 1f), -408.5f, Rand(0.8f, 1.2f), -1f, new Color(0.62f, 0.58f, 0.52f), MapKit.Collide.Box, 2.6f);
            Vector3 lLight = new Vector3(326f, 0.6f, -405f);
            K.Mesh(K.Env, "MarinaLight", CylMesh, lLight + Vector3.up * 3f, new Vector3(1.8f, 6f, 1.8f), Color.white, null, true).isStatic = true;
            K.Mesh(K.Env, "LightBand", CylMesh, lLight + Vector3.up * 4.4f, new Vector3(1.9f, 0.6f, 1.9f), new Color(0.2f, 0.6f, 0.3f));
            K.Lamp(lLight + Vector3.up * 6.4f, new Color(0.6f, 1f, 0.6f), 12f, 2f, false);
            // The promenade: palms, lamps, benches; the yacht club.
            for (int i = 0; i < 12; i++)
            {
                float x = 182f + i * 15f;
                Palm(x, -331f, 1.05f);
                if (i % 2 == 0) { P("LampPostOrnate", x + 7.5f, -332f, 0); K.Lamp(G(x + 7.5f, -332f, 4.1f), WARM, 12f, 1.4f, false); }
                else P("Bench", x + 7.5f, -337f, 180f);
            }
            MapKit.KitBuilding c = K.Building(Spec("Marina Yacht Club"));
            Awnings(c, 0, new Color(0.15f, 0.3f, 0.5f), 0, 1, 4, 5);
            for (int i = 0; i < 4; i++) Shade(G(206f + i * 5.5f, -309.5f), 0, new Color(0.15f, 0.3f, 0.5f), true);
            K.Activity(ActivityType.Dining, G(208f, -311f, 0.1f), 0, Z);
            Guard(Z, 226f, -306f, 180f, ItemKind.Pistol);
            Guard(Z, 309f, -337f, 180, ItemKind.Smg);
            Walk(Z, ItemKind.Smg, 4f, new Vector2(184, -336), new Vector2(346, -336), new Vector2(346, -333), new Vector2(184, -333));
            K.Weapon(ItemKind.Smg, new Vector3(237f, lPontoon + 0.15f, -394f));
            K.Gadget(ItemKind.SmokeGrenade, G(200, -320, 0.1f));
        }

        /// <summary>The Azure beach club: the pavilion with its roof terrace, cabanas, sunbeds and parasols, jump pads between the roofs, the lifeguard tower.</summary>
        private void BeachClub()
        {
            const int Z = 2;
            MapKit.KitBuilding b = K.Building(Spec("Azure Beach Club"));
            float lRoof = RoofY(b.spec);
            Awnings(b, 0, new Color(0.1f, 0.55f, 0.65f), 2, 5);
            // The roof terrace: the DJ booth, loungers, parasols.
            K.Prop("Table", new Vector3(426f, lRoof, -331f), 0, new Color(0.2f, 0.2f, 0.22f), MapKit.Collide.Box, true);
            K.DistractionKit(new Vector3(426f, lRoof + 0.78f, -331f), 180, "radio");
            for (int i = 0; i < 3; i++) Shade(new Vector3(412f + i * 5f, lRoof, -338f), 0, FABRIC[i], false);
            K.Activity(ActivityType.Drink, new Vector3(416f, lRoof + 0.1f, -334f), 180, Z);
            K.Prop("Table", new Vector3(416f, lRoof, -332.6f), 0, Color.white, MapKit.Collide.Box, true);
            K.DrinksOn(new Vector3(416f, lRoof + 0.78f, -332.6f), 180, Z, 2);
            for (int i = 0; i < 2; i++) K.Building(Spec("Beach Cabana " + i));
            // Jump pads: from the sand onto the pavilion roof, from the pavilion roof onto the first cabana.
            K.JumpPad(G(397f, -334f), 90f, new Vector3(0, 17f, 11f));
            K.JumpPad(new Vector3(427f, lRoof, -337f), 90f, new Vector3(0, 14f, 13f));
            // Sunbeds and parasols on the sand, the boardwalk, the volleyball net, the lifeguard tower.
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 9; i++)
                {
                    Vector3 s = G(368f + i * 13f, -348f - r * 6f);
                    K.Block(K.Props, "Sunbed", s + Vector3.up * 0.2f, new Vector3(0.8f, 0.3f, 2f), Color.white, GameLayers.DEFAULT, true);
                    K.Block(K.Props, "Sunbed", s + new Vector3(1.2f, 0.2f, 0), new Vector3(0.8f, 0.3f, 2f), Color.white, GameLayers.DEFAULT, true);
                    Shade(s + new Vector3(0.6f, 0, -1.4f), 0, FABRIC[(i + r) % FABRIC.Length], false);
                }
            Vector3 lNet = G(470, -345);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "NetPost", lNet + new Vector3(s * 4.5f, 1.2f, 0), new Vector3(0.12f, 2.4f, 0.12f), Color.white);
            K.Block(K.Env, "Net", lNet + new Vector3(0, 2f, 0), new Vector3(9f, 0.8f, 0.03f), new Color(0.9f, 0.9f, 0.88f), GameLayers.DEFAULT, false);
            Vector3 lTower = G(380f, -357f, -0.05f);
            K.Prop("Watchtower", lTower, 180f, new Color(0.9f, 0.9f, 0.85f), MapKit.Collide.Mesh);
            GameObject lClimb = K.Climbable(K.Env, lTower + Vector3.back * 2.05f + Vector3.up * 4.2f, new Vector3(0.8f, 8.4f, 0.3f), Quaternion.LookRotation(Vector3.forward), Color.white, false);
            lClimb.GetComponent<MeshRenderer>().enabled = false;
            K.Npc(NpcRole.Lookout, lTower + Vector3.up * 7.15f + Vector3.forward * 0.6f, 180f, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, lTower + Vector3.up * 7.2f + new Vector3(1f, 0, -0.6f));
            for (int i = 0; i < 3; i++) K.Prop("Boat", new Vector3(440f + i * 9f, SEA - 0.2f, -372f - (i % 2) * 4f), 180f + i * 20f, FABRIC[(i * 2) % FABRIC.Length], MapKit.Collide.Box);
            // The club life: yoga on the beach, a phone call by the cabanas; guards at the door and on the sand.
            for (int i = 0; i < 3; i++) K.Prop("Rug", G(455f + i * 2.5f, -350f, 0.02f), 0, new Color(0.3f, 0.55f, 0.6f), MapKit.Collide.None);
            K.Activity(ActivityType.Workout, G(457.5f, -350f, 0.1f), 180, Z);
            K.Activity(ActivityType.Phone, G(452f, -330f, 0.1f), 0, Z);
            K.Activity(ActivityType.Smoke, G(402f, -330f, 0.1f), 270, Z);
            Guard(Z, 418, -343, 180, ItemKind.Pistol);
            Guard(Z, 424, -343, 180, ItemKind.Smg);
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(362, -346), new Vector2(480, -346), new Vector2(480, -340), new Vector2(362, -340));
            K.Weapon(ItemKind.Shotgun, b.Local(9f, 0.15f, 3f));
            K.Gadget(ItemKind.EmeticPoison, new Vector3(416f, lRoof + 0.85f, -332.6f));
        }
    }
}
