using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Lakeside Lodge (zone 2): a three-floor stone lodge on a terrace ten metres above the lake.
    /// Public: the beach, the boathouse and the lakeside path. Restricted: the gardens, the
    /// forecourt, the lake terrace where the reception spills out, the service yard. VIP: the
    /// second floor (the owner's office, the accountant's office, the master suite).
    /// Ways in: the guarded front door (forecourt), the terrace doors from the party, the kitchen
    /// service door, the basement door at the bottom of the service ramp (utility room: breaker,
    /// air handler), the bridge from the guest house on the first floor, the west drainpipe and the
    /// roof, the ivy on the terrace wall from the beach.
    /// </summary>
    public partial class MapForest
    {
        private const float TERRACE = 8f, BEACH = -2.2f;
        private static readonly Vector3 LODGE = new Vector3(298, TERRACE + 0.05f, -120);
        private static readonly Color LODGE_STONE = new Color(0.93f, 0.9f, 0.83f);

        private void PlanLodge()
        {
            _zoneStage.Add(() => Flatten(R(205, -150, 400, -42), TERRACE, 30f));
            _zoneStage.Add(() => Flatten(R(196, -180, 412, -152), BEACH, 0f));
            // The service ramp: a sunken lane from the service yard down to the basement door.
            _carveStage.Add(() => Cells(R(252, -132, 280, -124), (i, j) =>
            {
                float x = Cx(i);
                _h[i, j] = x > 272f ? TERRACE - MapKit.STOREY : Mathf.Lerp(TERRACE, TERRACE - MapKit.STOREY, (x - 252f) / 20f);
                _f[i, j] |= BUILT;
                _paint[i, j] = new Color(0.5f, 0.5f, 0.48f);
            }));
            PaintRect(R(268, -104, 328, -58), GRAVEL);
            PaintRect(R(210, -150, 400, -132), COBBLE);
            PaintRect(R(232, -124, 278, -96), new Color(0.5f, 0.5f, 0.48f));
            PaintRect(R(196, -180, 412, -152), SAND, BARE);
            for (int k = 0; k < 4; k++) PaintRect(R(342.5f + k * 15f, -129, 346.5f + k * 15f, -71), new Color(0.66f, 0.63f, 0.56f), 0);
            for (int r = 0; r < 4; r++) PaintRect(R(342.5f, -129 + r * 18f, 391.5f, -125 + r * 18f), new Color(0.66f, 0.63f, 0.56f), 0);

            MapKit.KitSpec lLodge = Register(Lot("Lakeside Lodge", "Classic", LODGE.x, LODGE.z, 0, 12, 8, 3, Roof.Flat, LODGE_STONE, new Color(0.4f, 0.42f, 0.45f), 2, TERRACE, 4f));
            Doors(lLodge, 0, 5, 0, 6, 2, 5, 2, 6, 3, 4, 1, 5);
            lLodge.basement = true;
            lLodge.basementDoors.Add(new Vector2Int(3, 6));
            lLodge.upperDoors.Add(new Vector3Int(1, 3, 1));
            lLodge.tallWindows = true;
            lLodge.balconyChance = 0.3f;
            lLodge.climb = new Vector2Int(3, 2);
            lLodge.interiorWall = new Color(0.92f, 0.88f, 0.8f);
            lLodge.floorColor = new Color(0.55f, 0.4f, 0.3f);
            // The belvedere on the roof: a timber pavilion under a hip roof, the lodge's silhouette (and a VIP bar).
            MapKit.KitSpec lBelvedere = Register(new MapKit.KitSpec
            {
                name = "Lodge Belvedere", style = "Timber", origin = new Vector3(LODGE.x - 3f, LODGE.y + 3 * MapKit.STOREY, LODGE.z + 4.5f), width = 4, depth = 4, floors = 1, roof = Roof.Hip,
                wall = TIMBER_TINTS[1], roofTint = new Color(0.32f, 0.35f, 0.4f), zone = 2, stairs = false, windowChance = 0.9f, seed = 77, navigable = true
            });
            Doors(lBelvedere, 0, 1, 0, 2, 2, 1, 1, 2, 3, 1);
            Floors(lBelvedere, Room.Bar, Room.None);
            MapKit.KitSpec lGuest = Register(Lot("Guest House", "Timber", 328, -120, 0, 4, 6, 2, Roof.Gable, TIMBER_TINTS[1], new Color(0.38f, 0.4f, 0.44f), 2, TERRACE, 4f));
            Doors(lGuest, 0, 2, 2, 1);
            lGuest.upperDoors.Add(new Vector3Int(3, 3, 1));
            lGuest.balconyChance = 0.3f;
            Floors(lGuest, Room.Lounge, Room.Bathroom, Room.Bedroom, Room.Bedroom);
            MapKit.KitSpec lStaff = Register(Lot("Staff Quarters", "Timber", 244, -88, 90, 4, 3, 2, Roof.Hip, TIMBER_TINTS[2], new Color(0.38f, 0.4f, 0.44f), 2, TERRACE, 6f));
            Doors(lStaff, 0, 2, 2, 1);
            Floors(lStaff, Room.Kitchen, Room.Lounge, Room.Bedroom, Room.Bedroom);
            MapKit.KitSpec lGarage = Register(Lot("Lodge Garage", "Classic", 250, -62, 0, 4, 3, 1, Roof.Hip, LODGE_STONE, new Color(0.4f, 0.42f, 0.45f), 2, TERRACE, 6f));
            Doors(lGarage, 0, 1, 0, 2, 2, 1);
            Floors(lGarage, Room.Storage, Room.None);
            MapKit.KitSpec lGate = Register(Lot("Lodge Gatehouse", "Classic", 309, -48, 270, 2, 2, 1, Roof.Hip, LODGE_STONE, new Color(0.4f, 0.42f, 0.45f), 2, TERRACE, 6f));
            Doors(lGate, 0, 0);
            Floors(lGate, Room.Security, Room.None);
            MapKit.KitSpec lBoat = Register(Lot("Boathouse", "Timber", 250, -165.5f, 180, 4, 3, 1, Roof.Gable, TIMBER_TINTS[2], new Color(0.38f, 0.4f, 0.44f), 2, BEACH, 0f));
            Doors(lBoat, 0, 1, 2, 2, 1, 1);
            Floors(lBoat, Room.Storage, Room.None);
            Trail(new Vector2(200, -164), new Vector2(150, -150), new Vector2(110, -125), new Vector2(90, -102));
            Trail(new Vector2(410, -164), new Vector2(445, -120), new Vector2(420, -60));
        }

        /// <summary>A formal garden bed: low hedges on its outline (a gap on each side), flowers inside, a lawn.</summary>
        private void Parterre(Rect pBed, int pSeed)
        {
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
                    GameObject lHedge = Nat("HedgeBlock", x, z, 1f, lAlongX ? 0f : 90f, new Color(0.3f, 0.48f, 0.26f), MapKit.Collide.Box);
                    if (lHedge != null) lHedge.transform.localScale = new Vector3(1f, 0.55f, 0.7f);
                }
            }
            for (int i = 0; i < 3; i++) Nat("FlowerBed", pBed.center.x + (i - 1) * 2.6f, pBed.center.y, 1.2f, 0f, new[] { new Color(0.9f, 0.4f, 0.45f), new Color(0.95f, 0.85f, 0.4f), new Color(0.6f, 0.45f, 0.85f) }[(i + pSeed) % 3], MapKit.Collide.None);
        }

        private void Lodge()
        {
            const int Z = 2;
            K.Hole(R(280.05f, -131.95f, 315.95f, -108.05f));
            MapKit.KitBuilding L = K.Building(Spec("Lakeside Lodge"));
            Vector3 Loc(float x, int f, float z) => L.Local(x, f * MapKit.STOREY, z);
            System.Random lRandom = new System.Random(7);

            // --- partitions: the same frame on every floor (south rooms, north hall), kit walls.
            for (int f = -1; f < 3; f++)
            {
                K.PartitionKit(L, f, new Vector3(-18, 0, 0), new Vector3(12, 0, 0));
                if (f == 0)
                {
                    K.PartitionKit(L, f, new Vector3(-9, 0, -12), new Vector3(-9, 0, 0));
                    K.PartitionKit(L, f, new Vector3(12, 0, -12), new Vector3(12, 0, 0));
                    K.PartitionKit(L, f, new Vector3(-6, 0, 0), new Vector3(-6, 0, 12));
                }
                else
                {
                    K.PartitionKit(L, f, new Vector3(-6, 0, -12), new Vector3(-6, 0, 0));
                    K.PartitionKit(L, f, new Vector3(6, 0, -12), new Vector3(6, 0, 0));
                }
                K.Lamp(Loc(-12, f, -6) + Vector3.up * (MapKit.STOREY - 0.45f), WARM, 10f, 1.3f, false);
                K.Lamp(Loc(12, f, -6) + Vector3.up * (MapKit.STOREY - 0.45f), WARM, 10f, 1.3f, false);
                K.Lamp(Loc(0, f, 6) + Vector3.up * (MapKit.STOREY - 0.45f), WARM, 12f, 1.3f, false);
                K.Sprinklers(Loc(0, f, 0), new Vector3(L.W, MapKit.STOREY, L.D), 0, MapKit.STOREY - 0.05f);
            }

            // --- basement: utility room (breaker, generator) by the ramp door, air handler, wine cellar, panic room.
            K.Furnish(L, -1, Room.Utility, new Rect(-17.5f, -11.5f, 11f, 11f), lRandom);
            K.VentKit(L, Loc(0, -1, -8), 180);
            K.Prop("Cabinet", Loc(4, -1, -11.2f), 180, null, MapKit.Collide.Box, true);
            K.Furnish(L, -1, Room.Storage, new Rect(6.5f, -11.5f, 11f, 11f), lRandom);
            for (int i = 0; i < 4; i++) K.Prop("Bookshelf", Loc(8 + i * 2.4f, -1, -0.8f), 180, new Color(0.45f, 0.3f, 0.2f));
            K.Furnish(L, -1, Room.Security, new Rect(-17.5f, 0.5f, 10f, 11f), lRandom);
            K.Npc(NpcRole.Guard, Loc(0, -1, -4), 270, Z, ItemKind.Smg);
            K.Gadget(ItemKind.Poison, Loc(-16, -1, -1.5f) + Vector3.up * 0.95f);
            K.Weapon(ItemKind.Shotgun, Loc(-12, -1, 10) + Vector3.up * 0.15f);
            K.Activity(ActivityType.Inspect, Loc(12, -1, -6) + Vector3.up * 0.1f, 180, Z);

            // --- ground floor: the reception. Ballroom (chandelier, stage, bar), kitchen, toilets, security, foyer.
            K.Furnish(L, 0, Room.Ballroom, new Rect(-8.5f, -11.5f, 20f, 11f), lRandom);
            K.Prop("BarCounter", Loc(-5.6f, 0, -9.6f), 90, new Color(0.4f, 0.24f, 0.16f), MapKit.Collide.Box, true);
            K.Prop("BarShelf", Loc(-8.4f, 0, -9.6f), 90, null);
            K.DrinksOn(Loc(-5.6f, 0, -9.6f) + Vector3.up * 1.15f, 270, Z, 3);
            for (int i = 0; i < 4; i++)
            {
                Vector3 t = Loc(-3f + (i % 2) * 6f, 0, -9f + (i / 2) * 5f);
                K.Prop("TableRound", t, 0, new Color(0.9f, 0.88f, 0.82f), MapKit.Collide.Box, true);
                for (int c = 0; c < 3; c++) K.Prop("Chair", t + Quaternion.Euler(0, c * 120f, 0) * Vector3.forward * 1.1f, c * 120f + 180f, new Color(0.6f, 0.2f, 0.2f));
                if (i % 2 == 0) K.DrinksOn(t + Vector3.up * 0.78f, 0, Z, 2);
            }
            K.Block(K.Env, "Stage", Loc(9, 0, -9.5f) + Vector3.up * 0.25f, new Vector3(5.5f, 0.5f, 4.5f), new Color(0.45f, 0.3f, 0.22f));
            K.Activity(ActivityType.Speech, Loc(9, 0, -9.5f) + Vector3.up * 0.55f, 0, Z, 18f);
            K.Furnish(L, 0, Room.Kitchen, new Rect(-17.5f, -11.5f, 8f, 8.5f), lRandom);
            K.Activity(ActivityType.Dining, Loc(-10.4f, 0, -6.2f) + Vector3.up * 0.1f, 90, Z);
            K.Furnish(L, 0, Room.Bathroom, new Rect(12.5f, -11.5f, 5f, 11f), lRandom);
            K.Furnish(L, 0, Room.Security, new Rect(-17.5f, 0.5f, 11f, 11f), lRandom);
            K.Furnish(L, 0, Room.Lounge, new Rect(-5.5f, 0.5f, 16f, 11f), lRandom);
            K.Prop("Desk", Loc(4, 0, 9), 180, new Color(0.45f, 0.3f, 0.2f), MapKit.Collide.Box, true);
            for (int s = -1; s <= 1; s += 2) K.Prop("PlantBig", Loc(s * 4f, 0, 11), 180, null);
            K.Npc(NpcRole.Guard, Loc(-4, 0, -2), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(10, 0, -2), 180, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, Loc(0, 0, 9), 0, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, Loc(-15, 0, -4), 90, Z, ItemKind.Pistol);
            K.Gadget(ItemKind.EmeticPoison, Loc(-16, 0, -11) + Vector3.up * 1.0f);

            // --- first floor: guest suites, smoking lounge, bathroom; the bridge to the guest house.
            K.Furnish(L, 1, Room.Bedroom, new Rect(-17.5f, -11.5f, 11f, 11f), lRandom);
            K.Furnish(L, 1, Room.Bedroom, new Rect(-5.5f, -11.5f, 11f, 11f), lRandom);
            K.Furnish(L, 1, Room.Bedroom, new Rect(6.5f, -11.5f, 11f, 11f), lRandom);
            K.Furnish(L, 1, Room.Lounge, new Rect(-17.5f, 0.5f, 14f, 11f), lRandom);
            K.Furnish(L, 1, Room.Bathroom, new Rect(0.5f, 6f, 9f, 5.5f), lRandom);
            K.Activity(ActivityType.Smoke, Loc(-10, 1, 4) + Vector3.up * 0.1f, 0, Z);
            K.Npc(NpcRole.Guard, Loc(-2, 1, 3), 270, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Pistol, Loc(-16, 1, -2) + Vector3.up * 0.15f);

            // --- second floor (VIP): the accountant, the owner's office, the master suite, the library bar.
            K.Furnish(L, 2, Room.Office, new Rect(-17.5f, -11.5f, 11f, 11f), lRandom);
            K.Furnish(L, 2, Room.Office, new Rect(-5.5f, -11.5f, 11f, 11f), lRandom);
            K.Furnish(L, 2, Room.Bedroom, new Rect(6.5f, -11.5f, 11f, 11f), lRandom);
            K.Furnish(L, 2, Room.Lounge, new Rect(-17.5f, 0.5f, 12f, 11f), lRandom);
            K.Furnish(L, 2, Room.Bathroom, new Rect(-4.5f, 6f, 8f, 5.5f), lRandom);
            K.Prop("Table", Loc(6, 2, 3), 0, new Color(0.45f, 0.3f, 0.2f), MapKit.Collide.Box, true);
            K.DrinksOn(Loc(6, 2, 3) + Vector3.up * 0.8f, 0, Z, 3);
            K.Npc(NpcRole.Guard, Loc(-6.5f, 2, 2), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Loc(5, 2, 1.5f), 180, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Smg, Loc(-16, 2, 10) + Vector3.up * 0.15f);
            K.Gadget(ItemKind.C4, Loc(14, 2, -1.5f) + Vector3.up * 0.15f);

            // --- roof: the belvedere bar, lookouts, a rocket launcher by the plant room.
            K.Building(Spec("Lodge Belvedere"));
            float lRoof = LODGE.y + 3 * MapKit.STOREY;
            K.Npc(NpcRole.Lookout, new Vector3(LODGE.x - 12, lRoof + 0.1f, LODGE.z - 9), 180, Z, ItemKind.Sniper);
            K.Npc(NpcRole.Lookout, new Vector3(LODGE.x + 10, lRoof + 0.1f, LODGE.z + 9), 0, Z, ItemKind.Smg);
            K.UniqueWeapon(ItemKind.RocketLauncher, new Vector3(LODGE.x + 4, lRoof + 0.15f, LODGE.z + 8), "Lodge roof");
            K.Activity(ActivityType.Lookout, new Vector3(LODGE.x - 4, lRoof + 0.1f, LODGE.z - 10), 180, Z);

            // --- the service ramp: retaining walls, the basement door, deliveries.
            K.Block(K.Env, "RampWallN", new Vector3(266, TERRACE - 1.5f, -123.7f), new Vector3(28f, 3.4f + 0.6f, 0.6f), STONE * 0.9f);
            K.Block(K.Env, "RampWallS", new Vector3(266, TERRACE - 1.5f, -132.3f), new Vector3(28f, 3.4f + 0.6f, 0.6f), STONE * 0.9f);
            Row("Classic", "Railing", new Vector3(252, TERRACE + 0.3f, -123.5f), new Vector3(280, TERRACE + 0.3f, -123.5f), 3f, 0f, STONE);
            Row("Classic", "Railing", new Vector3(252, TERRACE + 0.3f, -132.5f), new Vector3(280, TERRACE + 0.3f, -132.5f), 3f, 180f, STONE);
            K.Lamp(new Vector3(278.5f, TERRACE - 0.6f, -127.5f), WARM, 8f, 1.2f, false);
            K.Crate(new Vector3(276, TERRACE - MapKit.STOREY, -130.5f));
            K.Barrel(new Vector3(277.5f, TERRACE - MapKit.STOREY, -125.5f), false);
            Guard(Z, 272, -128, 270, ItemKind.Shotgun);

            // --- the guest house and the first-floor bridge.
            MapKit.KitBuilding lGuest = K.Building(Spec("Guest House"));
            Span(new Vector3(316f, TERRACE + MapKit.STOREY + 0.35f, -121.5f), new Vector3(322f, TERRACE + MapKit.STOREY + 0.35f, -121.5f), 2.2f, false, 0.1f);
            K.Block(K.Env, "Pergola", new Vector3(319, TERRACE + MapKit.STOREY - 0.1f, -116f), new Vector3(6.5f, 0.2f, 3f), PLANK);
            K.Npc(NpcRole.Guard, lGuest.root.position + new Vector3(-2, 0.1f, -3), 0, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Shotgun, lGuest.root.position + new Vector3(3, MapKit.STOREY + 0.15f, 6));

            // --- the forecourt: drive, fountain, hedges, cars; the gatehouse.
            P("Fountain", 298, -84, 0, null, true);
            for (int i = 0; i < 6; i++)
            {
                for (int s = -1; s <= 1; s += 2) Nat("HedgeBlock", 298 + s * 7f, -64 - i * 6f, 1.2f, 0f, null, MapKit.Collide.Box);
                if (i % 2 == 0) for (int s = -1; s <= 1; s += 2) Nat("CypressA", 298 + s * 9.5f, -64 - i * 6f, 0.8f);
            }
            for (int i = 0; i < 4; i++) { P("LampPostOrnate", 288 + (i % 2) * 20f, -96 + (i / 2) * 22f, 0); K.Lamp(G(288 + (i % 2) * 20f, -96 + (i / 2) * 22f, 4.1f), WARM, 13f, 1.6f, false); }
            K.Car(G(278, -76), 0, new Color(0.15f, 0.15f, 0.18f));
            K.Car(G(278, -86), 0, new Color(0.55f, 0.1f, 0.12f));
            K.Car(G(318, -76), 180, new Color(0.85f, 0.85f, 0.88f));
            K.Distraction(G(318, -76, 0.9f), 180, "caralarm");
            K.Building(Spec("Lodge Gatehouse"));
            for (int s = -1; s <= 1; s += 2) P("Barrier", 298 + s * 2.2f, -52, 90, null, true);
            Guard(Z, 294, -54, 0, ItemKind.Smg);
            Guard(Z, 302, -54, 0, ItemKind.Shotgun);
            Guard(Z, 294, -106, 0, ItemKind.Smg);
            Guard(Z, 302, -106, 0, ItemKind.Pistol);

            // --- the lake terrace: the reception spills out (tables, bar, string lights), balustrade, grand stairs.
            for (int i = 0; i < 6; i++) P("UmbrellaTable", 262 + i * 8f + (i > 2 ? 16f : 0f), -142, 0, new[] { new Color(0.85f, 0.85f, 0.8f), new Color(0.3f, 0.4f, 0.55f) }[i % 2]);
            P("BarCounter", 340, -138, 180, new Color(0.4f, 0.24f, 0.16f), true);
            K.DrinksOn(G(340, -138, 1.15f), 180, Z, 3);
            K.Activity(ActivityType.Drink, G(272, -145, 0.1f), 180, Z);
            // The owner's dinner table on the terrace, set for four (dining, poisonable wine).
            P("Table", 330, -143, 0, new Color(0.9f, 0.88f, 0.82f), true);
            P("Table", 331.7f, -143, 0, new Color(0.9f, 0.88f, 0.82f), true);
            for (int c = 0; c < 4; c++) P("Chair", 329.6f + (c % 2) * 2.9f, -143f + (c < 2 ? -0.9f : 0.9f), c < 2 ? 0f : 180f, new Color(0.6f, 0.2f, 0.2f));
            K.DrinksOn(G(330.85f, -143, 0.8f), 0, Z, 3);
            K.Activity(ActivityType.Dining, G(330.85f, -144.6f, 0.1f), 0, Z);
            K.Activity(ActivityType.Phone, G(232, -142, 0.1f), 180, Z);
            for (int i = 0; i < 4; i++) Bunting(new Vector3(255 + i * 22f, TERRACE + 4.5f, -134), new Vector3(266 + i * 22f, TERRACE + 4.5f, -148), i + 1);
            // The retaining wall is a solid platform over the whole ground slope between the two levels.
            K.Block(K.Env, "TerraceWall", new Vector3(303, (TERRACE + 0.05f + BEACH) * 0.5f, -152.1f), new Vector3(214, TERRACE + 0.05f - BEACH, 8.2f), STONE * 0.92f);
            for (float x = 199.5f; x < 406f; x += 3f)
                if (Mathf.Abs(x - 298f) > 4f) K.Piece("Classic", "Balustrade", new Vector3(x, TERRACE + 0.05f, -155.9f), 180, STONE, K.Env);
            K.Stairs(K.Env, new Vector3(298, BEACH, -176f), new Vector3(298, TERRACE + 0.05f, -156.1f), 6f, STONE);
            // Buttresses, lanterns and planting along the terrace wall; a beach with trees, rocks and boats.
            for (float x = 202f; x < 404f; x += 12f)
            {
                if (Mathf.Abs(x - 298f) < 8f) continue;
                K.Block(K.Env, "Buttress", new Vector3(x, (TERRACE + BEACH) * 0.5f - 0.2f, -156.6f), new Vector3(1.6f, TERRACE - BEACH + 0.2f, 1.4f), STONE * 0.82f);
                K.Block(K.Env, "ButtressCap", new Vector3(x, TERRACE + 0.05f, -156.6f), new Vector3(1.9f, 0.35f, 1.7f), STONE);
                K.Lamp(new Vector3(x, TERRACE - 2.5f, -157.5f), WARM, 9f, 1.2f, false);
            }
            for (int i = 0; i < 14; i++)
            {
                float x = 205f + i * 14.5f + Rand(-3f, 3f);
                if (Mathf.Abs(x - 298f) < 9f || Mathf.Abs(x - 252f) < 9f) continue;
                Nat(i % 3 == 0 ? "CypressA" : (i % 3 == 1 ? "BirchA" : "PineA"), x, -160f + Rand(-1f, 1f), Rand(0.6f, 0.85f));
                Nat(i % 2 == 0 ? "RockB" : "BushB", x + 4f, -173f + Rand(0f, 3f), Rand(0.7f, 1.1f), -1f, null, MapKit.Collide.Box, 0.3f);
            }
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "StairCheek", new Vector3(298 + s * 3.3f, (TERRACE + BEACH) * 0.5f - 0.6f, -166f), new Vector3(0.6f, 1.2f, 22.4f), STONE * 0.85f, Core.GameLayers.DEFAULT, true, Quaternion.Euler(-27f, 0, 0));
            Guard(Z, 294, -147, 180, ItemKind.Smg);
            Guard(Z, 302, -147, 180, ItemKind.Smg);
            Ivy(new Vector3(226, BEACH, -156.4f), TERRACE - BEACH + 0.6f, 3f, 180f);
            Ivy(new Vector3(388, BEACH, -156.4f), TERRACE - BEACH + 0.6f, 3f, 180f);
            Walk(Z, ItemKind.Smg, 3f, new Vector2(220, -141), new Vector2(390, -141), new Vector2(390, -136), new Vector2(220, -136));

            // --- gardens east: parterres of hedges, flower beds, cypresses, the gazebo (garden + drinks).
            for (int r = 0; r < 3; r++)
                for (int k = 0; k < 3; k++)
                {
                    float x = 352 + k * 15f, z = -120 + r * 18f;
                    Parterre(new Rect(x - 5.5f, z - 5f, 11f, 10f), k + r);
                }
            for (int i = 0; i < 8; i++) Nat("CypressA", 344 + (i % 2) * 52f, -128 + (i / 2) * 18f, Rand(0.7f, 0.9f));
            P("Statue", 372, -92, 180, null, true);
            Vector3 lGazebo = G(372, -138);
            for (int i = 0; i < 6; i++) K.Piece("Classic", "Column", lGazebo + Quaternion.Euler(0, i * 60f, 0) * Vector3.forward * 3f, i * 60f, null, K.Env, MapKit.Collide.Box);
            GameObject lDome = K.Piece("Classic", "RoofHip", lGazebo + Vector3.up * MapKit.STOREY, 0, new Color(0.4f, 0.5f, 0.45f), K.Env, MapKit.Collide.None);
            if (lDome != null) lDome.transform.localScale = new Vector3(7.2f, 2.6f, 7.2f);
            K.Prop("TableRound", lGazebo, 0, null, MapKit.Collide.Box, true);
            K.DrinksOn(lGazebo + Vector3.up * 0.78f, 0, Z, 2);
            K.Activity(ActivityType.Garden, G(360, -110, 0.1f), 90, Z);
            K.Activity(ActivityType.Garden, G(384, -76, 0.1f), 180, Z);
            K.Activity(ActivityType.Smoke, G(392, -146, 0.1f), 180, Z);
            for (int i = 0; i < 4; i++) P("Bench", 346 + i * 14f, -66, 180);
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(346, -128), new Vector2(392, -128), new Vector2(392, -66), new Vector2(346, -66));
            Walk(Z, ItemKind.Smg, 4f, new Vector2(266, -100), new Vector2(330, -100), new Vector2(330, -60), new Vector2(266, -60));

            // --- the service yard: staff quarters, garage, deliveries, dumpsters.
            K.Building(Spec("Staff Quarters"));
            K.Building(Spec("Lodge Garage"));
            K.Car(G(262, -104), 90, new Color(0.9f, 0.9f, 0.88f));
            K.HidingKit(G(270, -114), 90, "Dumpster");
            K.HidingKit(G(270, -118), 90, "Dumpster");
            for (int i = 0; i < 3; i++) Clutter(G(240 + i * 4f, -118), 0, i);
            K.Activity(ActivityType.Smoke, G(262, -112, 0.1f), 0, Z);
            K.Activity(ActivityType.Idle, G(256, -108, 0.1f), 0, Z);
            Guard(Z, 266, -108, 90, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, G(250, -62, 0.15f));

            // --- the beach: boathouse, pier, the boat (extraction), the lakeside path.
            MapKit.KitBuilding lBoathouse = K.Building(Spec("Boathouse"));
            K.UniqueWeapon(ItemKind.Flamethrower, lBoathouse.root.position + new Vector3(3.5f, 0.15f, -2f), "Boathouse");
            for (int i = 0; i < 10; i++) K.Prop("PierSegment", new Vector3(262, RIVER_WATER - 0.15f, -176f - i * 3f), 90, null, MapKit.Collide.Mesh);
            K.Prop("PierSegment", new Vector3(258, RIVER_WATER - 0.15f, -204f), 0, null, MapKit.Collide.Mesh);
            K.Prop("PierSegment", new Vector3(266, RIVER_WATER - 0.15f, -204f), 0, null, MapKit.Collide.Mesh);
            GameObject lBoatProp = K.Prop("Boat", new Vector3(258.5f, RIVER_WATER - 0.2f, -196f), 180, new Color(0.92f, 0.92f, 0.9f), MapKit.Collide.Box);
            K.Prop("Boat", new Vector3(243, RIVER_WATER - 0.25f, -182f), 200, new Color(0.35f, 0.45f, 0.6f), MapKit.Collide.Box);
            K.Point(MatchPoint.Kind.Extraction, new Vector3(262, BEACH + 0.2f, -202f), 180, "Lodge pier boat");
            K.Npc(NpcRole.Guard, new Vector3(262, BEACH + 0.15f, -190f), 0, Z, ItemKind.Smg);
            Guard(Z, 244, -159, 0, ItemKind.Pistol);
            for (int i = 0; i < 3; i++) { P("LampPostOrnate", 225 + i * 40f, -160, 0); K.Lamp(G(225 + i * 40f, -160, 4.1f), WARM, 12f, 1.4f, false); }
            for (int i = 0; i < 5; i++) P("Bench", 280 + i * 22f + (i == 1 ? 6f : 0f), -159, 180);
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(206, -166), new Vector2(404, -166), new Vector2(404, -161), new Vector2(206, -161));
            K.Activity(ActivityType.Lookout, G(330, -166, 0.1f), 180, Z);
            K.Activity(ActivityType.Inspect, lBoathouse.root.position + new Vector3(0, 0.1f, 2f), 0, Z);
            K.Activity(ActivityType.Office, Loc(-12, 2, -6) + Vector3.up * 0.1f, 180, Z);

            K.Zone(Z, K.Map.zoneNames[2], new Vector3(305, TERRACE, -110), new Vector3(240, 90, 170), G(420, -40, 0.1f), new Color(0.25f, 0.28f, 0.35f), HatStyle.None,
                Loc(-12, -1, 6) + Vector3.up * 0.1f);
        }
    }
}
