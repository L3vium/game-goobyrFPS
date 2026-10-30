using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <b>Mirage Bay</b> - a sun-bleached coastal city, built vertically. The sea closes the west
    /// and the south.
    /// <list type="bullet">
    /// <item><b>Old Souk</b> (north-west): a maze of two- and three-storey houses with alleys, a
    /// market courtyard, rooftops joined by planks (cross the whole district without touching the
    /// ground), a clock tower with a ladder, a sniper nest and a zipline down to the docks.</item>
    /// <item><b>Smugglers' Docks</b> (south-west): stacked containers with ladders, warehouses, a
    /// raised highway on pillars with its ramps, a moored cargo ship with a gangway, a crane.</item>
    /// <item><b>The Zenith</b> (east): a podium (lobby with chandelier, bar, toilets, basement with
    /// breaker and air handler) topped with a pool deck and a six-floor hotel tower with a helipad;
    /// jump pads from the plaza to the podium, scaffolding up the tower, a zipline from the
    /// construction crane next door, sprinklers everywhere.</item>
    /// </list>
    /// </summary>
    public class MapGulf : MapBuilderBase
    {
        protected override string MapFile => "Map_Gulf";

        [MenuItem("Goobyr/Build/Map Gulf")]
        public static void BuildMenu() => new MapGulf().Build();

        private static readonly Color SAND = new Color(0.93f, 0.82f, 0.62f);
        private static readonly Color PAVE = new Color(0.86f, 0.8f, 0.72f);
        private static readonly Color ROAD = new Color(0.45f, 0.45f, 0.48f);
        private static readonly Color STONE = new Color(0.9f, 0.84f, 0.74f);

        protected override void Compose()
        {
            K.Grid(68, 68, 5f, new Vector2(-170, -170), SAND);

            // Sea: west and south.
            K.Height(R(-170, -170, -135, 170), -3f, new Color(0.75f, 0.7f, 0.55f));
            K.Height(R(-170, -170, 170, -130), -3f, new Color(0.75f, 0.7f, 0.55f));
            K.Water(new Vector3(-155, -1.2f, 0), new Vector2(40, 340));
            K.Water(new Vector3(0, -1.2f, -152), new Vector2(340, 45));
            Clear.Add(R(-170, -170, -130, 170));
            Clear.Add(R(-170, -170, 170, -125));

            // Roads.
            K.Paint(R(-130, -30, 170, -20), ROAD);
            K.Paint(R(-15, -125, -5, 170), ROAD);
            K.Paint(R(30, -20, 40, 120), ROAD);
            K.Paint(R(30, 110, 130, 120), ROAD);
            Clear.Add(R(-130, -32, 170, -18));
            Clear.Add(R(-17, -125, -3, 170));
            Clear.Add(R(28, -20, 42, 122));
            Clear.Add(R(28, 108, 132, 122));

            Souk();
            Docks();
            Zenith();
            Outskirts();
        }

        // ---------------------------------------------------------------- Old Souk (zone 0)

        private void Souk()
        {
            const int Z = 0;
            Color[] lWalls = { new Color(0.95f, 0.88f, 0.75f), new Color(0.92f, 0.78f, 0.6f), new Color(0.98f, 0.93f, 0.85f), new Color(0.85f, 0.72f, 0.58f), new Color(0.9f, 0.82f, 0.78f) };
            Color lRoof = new Color(0.88f, 0.8f, 0.68f);
            K.Paint(R(-128, -5, -25, 115), PAVE);

            // Market courtyard and clock tower stay open.
            Clear.Add(R(-85, 20, -55, 45));
            Clear.Add(R(-78, 48, -62, 62));

            // House grid: 11 m cells, alleys between; random heights; planks between roofs.
            float[,] lHeights = new float[9, 10];
            for (int i = 0; i < 9; i++)
                for (int j = 0; j < 10; j++)
                {
                    float x = -122 + i * 11.5f, z = 2 + j * 11.5f;
                    if (!IsClear(x, z) || K.Rng.NextDouble() < 0.12) continue;
                    int lFloors = K.Rng.NextDouble() < 0.35 ? 3 : 2;
                    Vector2 lSize = new Vector2(8 + (float)K.Rng.NextDouble() * 1.5f, 8 + (float)K.Rng.NextDouble() * 1.5f);
                    int lDoor = K.Rng.Next(4);
                    bool lGuard = K.Rng.NextDouble() < 0.35;
                    ItemKind lLoot = K.Rng.NextDouble() < 0.5 ? ItemKind.Pistol : K.Rng.NextDouble() < 0.5 ? ItemKind.Smg : ItemKind.Coin;
                    MapKit.BuildingResult lHouse = House("SoukHouse_" + i + "_" + j, x, z, lSize, 0, lFloors, lWalls[K.Rng.Next(lWalls.Length)], lRoof, Z, lDoor, lGuard, lLoot, false, K.Rng.NextDouble() < 0.25 ? K.Rng.Next(4) : -1);
                    lHeights[i, j] = lFloors * 3.4f;
                    // Awnings over the alleys (low cover, climb onto them from crates).
                    if (K.Rng.NextDouble() < 0.4)
                        K.Block(K.Env, "Awning", lHouse.Local(0, 2.6f, -lSize.y * 0.5f - 1f), new Vector3(lSize.x * 0.8f, 0.1f, 2f), new Color(0.85f, 0.35f + (float)K.Rng.NextDouble() * 0.4f, 0.3f), Core.GameLayers.DEFAULT, true, Quaternion.Euler(-12, 0, 0));
                }
            for (int i = 0; i < 9; i++)
                for (int j = 0; j < 10; j++)
                {
                    if (lHeights[i, j] <= 0f) continue;
                    float x = -122 + i * 11.5f, z = 2 + j * 11.5f;
                    if (i + 1 < 9 && Mathf.Approximately(lHeights[i + 1, j], lHeights[i, j]) && K.Rng.NextDouble() < 0.6)
                        K.Bridge(new Vector3(x + 4.4f, lHeights[i, j] + 0.05f, z), new Vector3(x + 7.1f, lHeights[i, j] + 0.05f, z), 1.2f, new Color(0.6f, 0.45f, 0.3f), 0.01f);
                    if (j + 1 < 10 && Mathf.Approximately(lHeights[i, j + 1], lHeights[i, j]) && K.Rng.NextDouble() < 0.6)
                        K.Bridge(new Vector3(x, lHeights[i, j] + 0.05f, z + 4.4f), new Vector3(x, lHeights[i, j] + 0.05f, z + 7.1f), 1.2f, new Color(0.6f, 0.45f, 0.3f), 0.01f);
                }

            // Market: stalls (cover, drinks, a radio), fountain, crates to climb onto the awnings.
            for (int i = 0; i < 6; i++)
            {
                float x = -82 + (i % 3) * 9, z = 25 + (i / 3) * 12;
                K.Block(K.Env, "Stall", K.At(x, z, 0.5f), new Vector3(3, 1f, 1.6f), new Color(0.6f, 0.42f, 0.28f), Core.GameLayers.DEFAULT, true, null, true);
                K.Block(K.Env, "StallRoof", K.At(x, z, 2.6f), new Vector3(3.6f, 0.1f, 2.4f), new Color(0.9f, 0.3f + i * 0.1f, 0.35f));
                for (int p = 0; p < 2; p++) K.Block(K.Env, "Pole", K.At(x + (p == 0 ? -1.6f : 1.6f), z, 1.3f), new Vector3(0.1f, 2.6f, 0.1f), new Color(0.5f, 0.4f, 0.3f));
            }
            K.BarTable(K.At(-73, 22), 180, Z, 3);
            K.BarTable(K.At(-64, 42), 0, Z, 2);
            K.Mesh(K.Env, "Fountain", EditorAssets.SaveMesh(FX.LowPolyMesh.Cylinder(12), "Kit_Cyl12"), K.At(-70, 33, 0.4f), new Vector3(5, 0.8f, 5), STONE, null, true);
            K.Distraction(K.At(-70, 33, 0.8f), 0, "radio");
            K.Activities(K.At(-70, 28, 0.1f), 0, Z, ActivityType.Inspect, ActivityType.Phone, ActivityType.Smoke);
            K.Activity(ActivityType.Dining, K.At(-64, 40, 0.1f), 0, Z);
            K.Activity(ActivityType.Speech, K.At(-76, 33, 0.1f), 90, Z);
            K.Toilet(K.At(-84, 44), 180, Z);
            K.CrateStack(K.At(-60, 21), 3);
            K.HidingSpot(K.At(-58, 44), 0, "dumpster");

            // Clock tower: ladder, sniper nest, zipline to the docks.
            Vector3 lTower = K.At(-70, 55);
            K.Block(K.Env, "ClockTower", lTower + Vector3.up * 11, new Vector3(6, 22, 6), new Color(0.88f, 0.75f, 0.55f));
            K.Block(K.Env, "Nest", lTower + Vector3.up * 22.2f, new Vector3(8, 0.4f, 8), new Color(0.7f, 0.55f, 0.4f));
            for (int s = 0; s < 4; s++)
            {
                Quaternion lRot = Quaternion.Euler(0, s * 90f, 0);
                K.Block(K.Env, "NestRail", lTower + Vector3.up * 22.9f + lRot * new Vector3(0, 0, 3.9f), new Vector3(8, 1f, 0.2f), new Color(0.7f, 0.55f, 0.4f), Core.GameLayers.DEFAULT, true, lRot);
            }
            K.Block(K.Env, "Clock", lTower + new Vector3(0, 18, -3.05f), new Vector3(3, 3, 0.1f), Color.white);
            K.Climbable(K.Env, lTower + new Vector3(0, 11.2f, 3.2f), new Vector3(0.8f, 22.4f, 0.15f), Quaternion.Euler(0, 180, 0), new Color(0.5f, 0.4f, 0.3f), true);
            K.Npc(NpcRole.Lookout, lTower + new Vector3(2, 22.5f, 2), 180, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, lTower + new Vector3(-2.5f, 22.5f, -2.5f));
            K.Zipline(lTower + new Vector3(-2.5f, 22.4f, 2.5f), K.At(-60, -45, 0f));

            K.Patrol(Z, ItemKind.Smg, 3f, K.At(-120, -6), K.At(-30, -6), K.At(-30, 112), K.At(-120, 112));
            K.Patrol(Z, ItemKind.Pistol, 3f, K.At(-90, 18), K.At(-55, 18), K.At(-55, 48), K.At(-90, 48));
            K.Patrol(Z, ItemKind.Shotgun, 2f, K.At(-110, 60), K.At(-40, 60), K.At(-40, 90));
            Post(Z, -122, 80, 270, ItemKind.Smg);
            Post(Z, -25, 50, 90, ItemKind.Smg);
            K.Point(MatchPoint.Kind.Extraction, K.At(-127, 85, 0.1f), 270, "Souk west gate");
            K.UniqueWeapon(ItemKind.Flamethrower, K.At(-84, 44, 0.1f) + new Vector3(2, 0, 0), "Souk market toilets");
            BaseActivities(Z, new Vector3(-70, 0, 33), 13f);
            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-75, 0, 55), new Vector3(110, 60, 120), K.At(-25, 115, 0.1f), new Color(0.9f, 0.75f, 0.45f), HatStyle.Turban, K.At(-70, 55, 0.1f));
        }

        // ---------------------------------------------------------------- Smugglers' Docks (zone 1)

        private void Docks()
        {
            const int Z = 1;
            K.Paint(R(-130, -125, -20, -35), new Color(0.62f, 0.62f, 0.64f));
            K.Block(K.Env, "QuayEdge", new Vector3(-75, -0.2f, -125.5f), new Vector3(110, 1.2f, 1f), new Color(0.5f, 0.5f, 0.52f));

            // Container yard: rows of stacks, ladders, gaps to slip through.
            Color[] lColors = { new Color(0.85f, 0.3f, 0.25f), new Color(0.25f, 0.5f, 0.8f), new Color(0.3f, 0.65f, 0.4f), new Color(0.95f, 0.7f, 0.25f), new Color(0.6f, 0.4f, 0.7f) };
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 7; col++)
                {
                    if (K.Rng.NextDouble() < 0.2) continue;
                    float x = -120 + col * 7f, z = -110 + row * 16f;
                    int lStack = 1 + K.Rng.Next(3);
                    for (int s = 0; s < lStack; s++) ShippingContainer(new Vector3(x, 2.6f * s, z), 0, lColors[K.Rng.Next(lColors.Length)], s == 0 && K.Rng.NextDouble() < 0.4);
                }
            K.Npc(NpcRole.Lookout, new Vector3(-99, 7.9f, -94), 90, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Shotgun, new Vector3(-106, 0.1f, -102));
            K.Weapon(ItemKind.Smg, new Vector3(-85, 0.1f, -70));
            K.Gadget(ItemKind.C4, new Vector3(-113, 0.1f, -86));

            // Warehouses.
            MapKit.BuildingSpec lWarehouse = new MapKit.BuildingSpec { name = "Warehouse A", origin = K.At(-55, -95), size = new Vector2(24, 18), floors = 1, floorHeight = 6f, wall = new Color(0.7f, 0.72f, 0.75f), trim = new Color(0.5f, 0.52f, 0.55f), roof = new Color(0.55f, 0.58f, 0.6f), windowSpacing = 6f, climbSide = 1 };
            lWarehouse.openings.Add(new MapKit.Opening(0, 0.5f, 0, 6f, 0, 5f));
            lWarehouse.openings.Add(new MapKit.Opening(2, 0.3f));
            MapKit.BuildingResult lA = K.Building(lWarehouse);
            Clear.Add(R(-70, -107, -40, -83));
            K.CrateStack(lA.Local(-6, 0, 3), 8);
            K.CrateStack(lA.Local(6, 0, -3), 6);
            K.Barrel(lA.Local(9, 0, 6), true);
            K.Activities(lA.Local(0, 0.1f, 5), 0, Z, ActivityType.Inspect, ActivityType.Office, ActivityType.Phone);
            K.BarTable(lA.Local(-8, 0, -6), 0, Z, 2);
            K.Toilet(lA.Local(10.5f, 0, 7.5f), 180, Z);
            K.Npc(NpcRole.Guard, lA.Local(0, 0.1f, -4), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, lA.Local(-9, 0.1f, 6), 90, Z, ItemKind.Shotgun);
            K.UniqueWeapon(ItemKind.RocketLauncher, lA.Local(9, 6.1f, -7), "Warehouse roof");
            MapKit.BuildingResult lB = House("Warehouse B", -30, -65, new Vector2(16, 14), 90, 1, new Color(0.75f, 0.68f, 0.6f), new Color(0.55f, 0.58f, 0.6f), Z, 0, true, ItemKind.Smg, false, 2);
            K.Distraction(lB.Local(5, 0, 5), 0, "radio");

            // The raised highway, its pillars and ramps.
            const float DECK = 9f;
            K.Block(K.Env, "Highway", new Vector3(-50, DECK, -40), new Vector3(150, 0.8f, 12), ROAD);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Barrier", new Vector3(-50, DECK + 0.8f, -40 + s * 5.8f), new Vector3(150, 0.8f, 0.4f), STONE);
            for (float x = -120; x <= 20; x += 18) K.Block(K.Env, "Pillar", new Vector3(x, DECK * 0.5f - 0.3f, -40), new Vector3(2f, DECK, 3f), STONE, Core.GameLayers.DEFAULT, true, null, true);
            K.Ramp(K.Env, new Vector3(60, 0, -40), new Vector3(25, DECK + 0.4f, -40), 10f, ROAD);
            K.Climbable(K.Env, new Vector3(-84, DECK * 0.5f, -38.4f), new Vector3(1.5f, DECK, 0.3f), Quaternion.identity, new Color(0.4f, 0.4f, 0.45f), true);
            K.Car(new Vector3(-80, DECK + 0.4f, -42), 90, new Color(0.9f, 0.9f, 0.9f));
            K.Car(new Vector3(-20, DECK + 0.4f, -38), 270, new Color(0.3f, 0.3f, 0.35f));
            K.Weapon(ItemKind.Sniper, new Vector3(-50, DECK + 0.5f, -40));
            K.Npc(NpcRole.Guard, new Vector3(-60, DECK + 0.5f, -40), 180, Z, ItemKind.Smg);
            Clear.Add(R(-125, -48, 60, -32));

            // The cargo ship and its gangway; the crane with a zipline to the ship.
            Vector3 lShip = new Vector3(-80, 0, -142);
            K.Block(K.Env, "Hull", lShip + new Vector3(0, 1.5f, 0), new Vector3(60, 9, 14), new Color(0.25f, 0.3f, 0.4f));
            K.Block(K.Env, "Deck", lShip + new Vector3(0, 6.1f, 0), new Vector3(60, 0.3f, 14), new Color(0.6f, 0.4f, 0.3f));
            MapKit.BuildingResult lBridge = House("Ship Bridge", lShip.x + 22, lShip.z, new Vector2(10, 10), 0, 2, Color.white, new Color(0.25f, 0.3f, 0.4f), Z, 3, true, ItemKind.Smg);
            lBridge.root.position = lShip + new Vector3(22, 6.25f, 0);
            for (int i = 0; i < 4; i++) ShippingContainer(lShip + new Vector3(-20 + i * 7f, 6.25f, 0), 0, lColors[i]);
            K.Ramp(K.Env, new Vector3(-90, 0.2f, -124), new Vector3(-90, 6.3f, -135.5f), 2.5f, new Color(0.6f, 0.45f, 0.3f));
            K.Activity(ActivityType.Lookout, lShip + new Vector3(-26, 6.4f, 0), 270, Z);
            K.Activity(ActivityType.Smoke, lShip + new Vector3(10, 6.4f, 5), 0, Z);
            K.Npc(NpcRole.Guard, lShip + new Vector3(0, 6.4f, 5), 180, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, lShip + new Vector3(-15, 6.4f, -5), 0, Z, ItemKind.Smg);
            Vector3 lCrane = new Vector3(-40, 0, -118);
            K.Block(K.Env, "CraneTower", lCrane + Vector3.up * 13, new Vector3(2.5f, 26, 2.5f), new Color(0.95f, 0.75f, 0.2f));
            K.Block(K.Env, "CraneJib", lCrane + new Vector3(-12, 26, -5), new Vector3(30, 1.2f, 1.2f), new Color(0.95f, 0.75f, 0.2f), Core.GameLayers.DEFAULT, true, Quaternion.Euler(0, -20, 0));
            K.Block(K.Env, "CraneCab", lCrane + new Vector3(0, 26.5f, 0), new Vector3(3.5f, 1f, 3.5f), new Color(0.95f, 0.75f, 0.2f));
            K.Climbable(K.Env, lCrane + new Vector3(0, 13.2f, 1.35f), new Vector3(0.8f, 26.4f, 0.15f), Quaternion.Euler(0, 180, 0), new Color(0.4f, 0.4f, 0.45f), true);
            K.Zipline(lCrane + new Vector3(1, 27f, 1), lShip + new Vector3(15, 6.25f, 4));

            // Speedboat extraction on the quay, patrols.
            K.Point(MatchPoint.Kind.Extraction, new Vector3(-122, 0.1f, -120), 0, "Marina speedboat");
            K.Patrol(Z, ItemKind.Smg, 3f, new Vector3(-125, 0.1f, -118), new Vector3(-25, 0.1f, -118), new Vector3(-25, 0.1f, -55));
            K.Patrol(Z, ItemKind.Shotgun, 2f, new Vector3(-118, 0.1f, -60), new Vector3(-70, 0.1f, -60), new Vector3(-70, 0.1f, -80));
            Post(Z, -20, -110, 90, ItemKind.Smg);
            Post(Z, -120, -50, 0, ItemKind.Shotgun);
            K.Alarm(new Vector3(-24, 0, -100), 270);
            for (float x = -120; x <= -30; x += 22) K.Lamp(new Vector3(x, 0, -122), new Color(1f, 0.8f, 0.55f));
            BaseActivities(Z, new Vector3(-80, 0, -80), 8f);
            K.Zone(Z, K.Map.zoneNames[1], new Vector3(-70, 0, -90), new Vector3(120, 60, 110), new Vector3(-10, 0.1f, -125), new Color(0.3f, 0.35f, 0.45f), HatStyle.Beanie, lA.Local(0, 0.1f, 0));
        }

        // ---------------------------------------------------------------- The Zenith (zone 2)

        private void Zenith()
        {
            const int Z = 2;
            Vector2 c = new Vector2(85, 45);
            const float PW = 50, PD = 40, PH = 4f;
            K.Paint(R(c.x - 45, c.y - 40, c.x + 45, c.y + 40), PAVE);
            Clear.Add(R(c.x - 45, c.y - 40, c.x + 45, c.y + 40));
            K.Hole(R(c.x - PW * 0.5f, c.y - PD * 0.5f, c.x + PW * 0.5f, c.y + PD * 0.5f));
            // A sunken service ramp down to the basement (west side).
            K.Height(R(c.x - PW * 0.5f - 12, c.y - 5, c.x - PW * 0.5f, c.y + 5), -PH, ROAD);
            K.Ramp(K.Env, new Vector3(c.x - PW * 0.5f - 25, 0, c.y), new Vector3(c.x - PW * 0.5f - 12, -PH, c.y), 9f, ROAD);

            MapKit.BuildingSpec lPodium = new MapKit.BuildingSpec
            {
                name = "Zenith Podium", origin = new Vector3(c.x, 0, c.y), size = new Vector2(PW, PD), floors = 2, floorHeight = PH,
                wall = new Color(0.95f, 0.95f, 0.98f), trim = new Color(0.8f, 0.7f, 0.45f), floor = new Color(0.88f, 0.84f, 0.78f), roof = new Color(0.85f, 0.85f, 0.88f),
                basement = true, windowSpacing = 4f, climbSide = 0, climbAt = 0.85f
            };
            lPodium.openings.Add(new MapKit.Opening(0, 0.5f, 0, 5f, 0, 3.6f));
            lPodium.openings.Add(new MapKit.Opening(2, 0.5f, 0, 3f, 0, 3f));
            lPodium.openings.Add(new MapKit.Opening(1, 0.3f));
            lPodium.openings.Add(new MapKit.Opening(3, 0.5f, -1, 6f, 0, 3.6f));
            MapKit.BuildingResult P = K.Building(lPodium);

            // Lobby: chandelier, reception (cover), bar, lounges, toilets.
            K.Chandelier(P.Local(0, PH - 0.1f, -4), 3.8f);
            K.Block(P.interior, "Reception", P.Local(0, 0.55f, 8), new Vector3(8, 1.1f, 1.2f), new Color(0.8f, 0.7f, 0.45f), Core.GameLayers.DEFAULT, true, null, true);
            K.BarTable(P.Local(-16, 0, -10), 90, Z, 3);
            K.BarTable(P.Local(-16, 0, 6), 90, Z, 3);
            K.BarTable(P.Local(14, 0, -12), 0, Z, 2);
            K.Activity(ActivityType.Speech, P.Local(0, 0.1f, -10), 0, Z);
            K.Activities(P.Local(14, 0.1f, 8), 270, Z, ActivityType.Phone, ActivityType.Idle);
            K.Partition(P, 0, new Vector3(18, 0, -PD * 0.5f), new Vector3(18, 0, PD * 0.5f), 0.5f, lPodium.wall * 0.95f);
            K.Toilet(P.Local(22, 0, -12), 270, Z);
            K.Toilet(P.Local(22, 0, -9), 270, Z);
            K.HidingSpot(P.Local(22.5f, 0, 14), 270, "closet");
            K.Npc(NpcRole.Guard, P.Local(-5, 0.1f, 12), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, P.Local(5, 0.1f, 12), 180, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, P.Local(0, 0.1f, -17), 0, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, P.Local(20, 0.1f, 0), 270, Z, ItemKind.Shotgun);

            // First floor: spa / gym / conference.
            K.Partition(P, 1, new Vector3(-8, 0, -PD * 0.5f), new Vector3(-8, 0, PD * 0.5f), 0.5f, lPodium.wall);
            K.Partition(P, 1, new Vector3(8, 0, -PD * 0.5f), new Vector3(8, 0, PD * 0.5f), 0.5f, lPodium.wall);
            K.Activity(ActivityType.Workout, P.Local(-16, PH + 0.1f, 0), 90, Z);
            K.Activity(ActivityType.Office, P.Local(16, PH + 0.1f, 0), 270, Z);
            K.Distraction(P.Local(16, PH, -12), 0, "tv");
            K.Npc(NpcRole.Guard, P.Local(0, PH + 0.1f, 10), 180, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Shotgun, P.Local(-20, PH + 0.1f, 15));

            // Basement: parking, utility room, air handler.
            BuildingVolume lVolume = K.BuildingVolume("The Zenith", P.Local(0, -PH, 0), new Vector3(PW, PH * 9, PD), 0);
            K.Breaker(P.Local(-22, -PH, 19.6f), 0, lVolume);
            for (int i = 0; i < 4; i++) K.Car(P.Local(-12 + i * 7, -PH, -12), 0, new Color(0.3f + i * 0.15f, 0.3f, 0.4f));
            K.Npc(NpcRole.Guard, P.Local(0, -PH + 0.1f, 4), 90, Z, ItemKind.Smg);
            K.Gadget(ItemKind.Poison, P.Local(18, -PH + 0.1f, 15));

            // Pool deck on the podium roof: pool, bar, loungers; tower above.
            float lRoof = PH * 2;
            K.Block(K.Env, "Pool", P.Local(-12, lRoof + 0.05f, -10), new Vector3(16, 0.1f, 10), new Color(0.3f, 0.75f, 0.95f));
            K.BarTable(P.Local(-12, lRoof, 2), 180, Z, 3);
            K.Activity(ActivityType.Garden, P.Local(-20, lRoof + 0.1f, -16), 0, Z);
            K.Activity(ActivityType.Workout, P.Local(-4, lRoof + 0.1f, -16), 0, Z);
            K.Npc(NpcRole.Guard, P.Local(-20, lRoof + 0.1f, 15), 180, Z, ItemKind.Smg);
            for (int i = 0; i < 6; i++) K.Tree(P.Local(-22 + i * 4, lRoof, 17), MapKit.TreeKind.Palm, 0.6f);

            const float TH = 4f;
            MapKit.BuildingSpec lTower = new MapKit.BuildingSpec
            {
                name = "Zenith Tower", origin = P.Local(12, lRoof, 0), size = new Vector2(22, 22), floors = 6, floorHeight = TH,
                wall = new Color(0.55f, 0.75f, 0.9f), trim = new Color(0.8f, 0.7f, 0.45f), floor = new Color(0.88f, 0.84f, 0.78f), roof = new Color(0.75f, 0.75f, 0.8f),
                windowSpacing = 3f, climbSide = 1, climbAt = 0.5f
            };
            lTower.openings.Add(new MapKit.Opening(3, 0.5f, 0, 3f, 0, 3f));
            lTower.openings.Add(new MapKit.Opening(0, 0.3f));
            for (int f = 1; f < 6; f++) lTower.openings.Add(new MapKit.Opening(3, 0.3f, f, 2.2f, 0, 2.6f));
            MapKit.BuildingResult T = K.Building(lTower);
            for (int f = 0; f < 6; f++)
            {
                float y = f * TH;
                K.Partition(T, f, new Vector3(-11, 0, 2), new Vector3(6, 0, 2), 0.3f, lTower.wall * 1.05f);
                K.BarTable(T.Local(-6, y, -6), 0, Z, f == 5 ? 3 : 1, f == 5 || f == 2);
                if (f % 2 == 1) K.Npc(NpcRole.Guard, T.Local(0, y + 0.1f, -2), 0, Z, ItemKind.Smg);
                if (f == 2) K.Toilet(T.Local(-9, y, 8), 180, Z);
                if (f == 4) K.Distraction(T.Local(-8, y, -9), 0, "phone");
                K.Lamp(T.Local(-3, y + TH - 0.5f, -3), new Color(1f, 0.9f, 0.75f), 10f, 1.5f, false);
                K.Weapon(f % 2 == 0 ? ItemKind.Pistol : ItemKind.Smg, T.Local(-9, y + 0.1f, -9));
            }
            // Penthouse (top floor) for the targets who live up there.
            K.Activities(T.Local(-4, 5 * TH + 0.1f, 6), 180, Z, ActivityType.Office, ActivityType.Phone, ActivityType.Dining);
            K.Toilet(T.Local(-9, 5 * TH, 8), 180, Z);
            K.Chandelier(T.Local(-2, 6 * TH - 0.1f, -4), 3.8f);
            K.HidingSpot(T.Local(9, 5 * TH, 9), 270, "closet");
            K.Npc(NpcRole.Guard, T.Local(3, 5 * TH + 0.1f, 6), 180, Z, ItemKind.Shotgun);
            // Helipad roof.
            float lTop = lRoof + 6 * TH;
            K.Block(K.Env, "Helipad", T.Local(0, 6 * TH + 0.1f, 0), new Vector3(12, 0.1f, 12), new Color(0.35f, 0.35f, 0.4f));
            K.Point(MatchPoint.Kind.Extraction, T.Local(0, 6 * TH + 0.2f, 0), 0, "Zenith helipad");
            K.Npc(NpcRole.Lookout, T.Local(8, 6 * TH + 0.2f, 8), 45, Z, ItemKind.Sniper);
            _ = lTop;

            // Vents: every floor of the podium and the tower.
            System.Collections.Generic.List<Transform> lVents = new System.Collections.Generic.List<Transform>
            {
                K.VentGrill(P.Local(-10, PH - 0.15f, 0)), K.VentGrill(P.Local(10, PH - 0.15f, -8)), K.VentGrill(P.Local(0, 2 * PH - 0.15f, 0))
            };
            for (int f = 0; f < 6; f++) lVents.Add(K.VentGrill(T.Local(0, (f + 1) * TH - 0.15f, -6)));
            K.Vent(P.Local(10, -PH, 14), 180, lVolume, lVents.ToArray());
            K.Sprinklers(P.Local(0, 0, 0), new Vector3(PW, PH * 2, PD), 0, PH - 0.1f);
            K.Sprinklers(T.Local(0, 0, 0), new Vector3(22, TH * 6, 22), 0, TH - 0.1f);

            // Ways in: jump pads to the pool deck, scaffolding on the tower, the crane zipline.
            K.JumpPad(K.At(c.x - 30, c.y - 28), 45, new Vector3(0, 17, 9));
            K.JumpPad(K.At(c.x + 32, c.y + 24), 225, new Vector3(0, 17, 9));
            Vector3 lSite = K.At(c.x + 45, c.y - 30);
            K.Block(K.Env, "CraneTower", lSite + Vector3.up * 19, new Vector3(2.5f, 38, 2.5f), new Color(0.95f, 0.7f, 0.2f));
            K.Block(K.Env, "CraneJib", lSite + new Vector3(-10, 38, 8), new Vector3(28, 1.2f, 1.2f), new Color(0.95f, 0.7f, 0.2f), Core.GameLayers.DEFAULT, true, Quaternion.Euler(0, 40, 0));
            K.Block(K.Env, "CraneCab", lSite + new Vector3(0, 38.5f, 0), new Vector3(3.5f, 1f, 3.5f), new Color(0.95f, 0.7f, 0.2f));
            K.Climbable(K.Env, lSite + new Vector3(0, 19.2f, -1.35f), new Vector3(0.8f, 38.4f, 0.15f), Quaternion.identity, new Color(0.4f, 0.4f, 0.45f), true);
            K.Zipline(lSite + new Vector3(-1, 39f, 1), T.Local(5, 6 * TH, 5));
            K.CrateStack(lSite + new Vector3(-8, 0, 4), 6);
            K.Barrel(lSite + new Vector3(-5, 0, -4), true);
            K.Weapon(ItemKind.Sniper, lSite + new Vector3(1.2f, 38.6f, 1.2f));
            K.UniqueWeapon(ItemKind.RocketLauncher, lSite + new Vector3(-8, 2.3f, 4), "Construction site crates");
            K.UniqueWeapon(ItemKind.Flamethrower, P.Local(-20, -PH + 0.1f, 15), "Zenith basement");
            Clear.Add(R(lSite.x - 15, lSite.z - 15, lSite.x + 15, lSite.z + 15));

            K.Patrol(Z, ItemKind.Smg, 2.5f, K.At(c.x - 35, c.y - 32), K.At(c.x + 35, c.y - 32), K.At(c.x + 35, c.y + 32), K.At(c.x - 35, c.y + 32));
            K.Patrol(Z, ItemKind.Pistol, 3f, P.Local(-20, 0.1f, -15), P.Local(10, 0.1f, -15), P.Local(10, 0.1f, 15), P.Local(-20, 0.1f, 15));
            Post(Z, c.x, c.y - PD * 0.5f - 4, 180, ItemKind.Smg);
            Post(Z, c.x - PW * 0.5f - 20, c.y, 270, ItemKind.Shotgun);
            for (int i = 0; i < 8; i++) K.Tree(K.At(c.x - 40 + i * 11, c.y - 36), MapKit.TreeKind.Palm, 1.1f);
            for (int i = 0; i < 8; i++) K.Lamp(K.At(c.x - 38 + i * 11, c.y - 34), new Color(1f, 0.9f, 0.7f));
            BaseActivities(Z, new Vector3(c.x, 0, c.y - 30), 6f);
            K.Zone(Z, K.Map.zoneNames[2], new Vector3(c.x, 0, c.y), new Vector3(110, 70, 100), K.At(c.x + 60, c.y + 60, 0.1f), new Color(0.25f, 0.25f, 0.3f), HatStyle.None, T.Local(0, 5 * TH + 0.1f, 6));
        }

        // ---------------------------------------------------------------- outskirts

        private void Outskirts()
        {
            Forest(R(-10, -10, 25, 170), 26, MapKit.TreeKind.Palm, 0.9f, 1.3f);
            Forest(R(130, -120, 170, 170), 30, MapKit.TreeKind.Palm, 0.9f, 1.3f);
            Rocks(R(130, -120, 170, 170), 25, new Color(0.85f, 0.7f, 0.5f), 1f, 3f);
            K.PatrolCar(9f, 0f, new Color(0.95f, 0.95f, 0.95f), K.At(-120, -25, 0.05f), K.At(160, -25, 0.05f), K.At(160, -22, 0.05f), K.At(-120, -22, 0.05f));
            K.PatrolCar(7f, 40f, new Color(0.2f, 0.25f, 0.3f), K.At(35, -15, 0.05f), K.At(35, 115, 0.05f), K.At(125, 115, 0.05f), K.At(125, 112, 0.05f), K.At(38, 112, 0.05f), K.At(38, -15, 0.05f));
            K.Point(MatchPoint.Kind.Extraction, K.At(160, -40, 0.1f), 90, "Desert road");
            K.Point(MatchPoint.Kind.Start, K.At(-120, 150, 0.2f), 135, "North beach");
            K.Point(MatchPoint.Kind.Start, K.At(150, 150, 0.2f), 225, "Desert dunes");
            K.Point(MatchPoint.Kind.Start, K.At(150, -110, 0.2f), 315, "Coast road");
            K.Point(MatchPoint.Kind.Start, K.At(0, 10, 0.2f), 0, "Central boulevard");

            float[,] lSpots = { { 0, 60 }, { 10, -60 }, { 150, 60 }, { 120, -90 }, { -20, 140 }, { 60, 140 }, { 100, -60 }, { 0, -100 }, { 140, 20 } };
            ItemKind[] lKinds = { ItemKind.Smg, ItemKind.Shotgun, ItemKind.Sniper, ItemKind.Smg, ItemKind.Pistol, ItemKind.Sniper, ItemKind.Shotgun, ItemKind.Smg, ItemKind.Sniper };
            for (int i = 0; i < lSpots.GetLength(0); i++) K.Weapon(lKinds[i], K.At(lSpots[i, 0], lSpots[i, 1], 0.1f));
            K.Gadget(ItemKind.SmokeGrenade, K.At(0, 30, 0.1f));
            K.Gadget(ItemKind.SmokeGrenade, K.At(60, -60, 0.1f));
            K.Gadget(ItemKind.EmeticPoison, K.At(-40, 0, 0.1f));
            K.Gadget(ItemKind.Brick, K.At(20, 90, 0.1f));
            K.Gadget(ItemKind.Coin, K.At(-10, -90, 0.1f));
            K.Grass(new Vector3(150, 0, 100), new Vector2(20, 30), new Color(0.85f, 0.78f, 0.45f));
        }
    }
}
