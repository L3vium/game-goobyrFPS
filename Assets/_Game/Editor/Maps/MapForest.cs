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
    /// <b>Pinecrest Hollow</b> - a cold river splits the valley north-south.
    /// <list type="bullet">
    /// <item><b>Millbrook Town</b> (south-west): a main street of shops, the sawmill, the saloon,
    /// the sheriff's office, a water tower (ladder, sniper nest, zipline across the river), two
    /// bridges with dry banks to hide under.</item>
    /// <item><b>The Grove & Outpost</b> (north-east): a palisaded commune in the pines, wrapped in
    /// a marsh crossed by stilt walkways, tall grass all around; next door a military outpost with
    /// watchtowers, barracks, a motor pool, a bunker and the alarm flare.</item>
    /// <item><b>Lakeside Lodge</b> (south-east): a timber lodge on a stone plinth above the lake -
    /// grand hall chandelier, bar, toilets, offices, a basement (utility room, air handler) with an
    /// outside service door, sprinklers, a balcony roof and a boathouse.</item>
    /// </list>
    /// A cliff plateau in the north-west overlooks the town (climbable rock face, sniper spot).
    /// </summary>
    public class MapForest : MapBuilderBase
    {
        protected override string MapFile => "Map_Forest";

        [MenuItem("Goobyr/Build/Map Forest")]
        public static void BuildMenu() => new MapForest().Build();

        private static readonly Color GRASS = new Color(0.55f, 0.72f, 0.4f);
        private static readonly Color DIRT = new Color(0.72f, 0.6f, 0.42f);
        private static readonly Color STONE = new Color(0.62f, 0.62f, 0.6f);
        private static readonly Color WOOD = new Color(0.6f, 0.42f, 0.28f);
        private static readonly Color PINE = new Color(0.25f, 0.48f, 0.36f);

        protected override void Compose()
        {
            K.Grid(68, 68, 5f, new Vector2(-170, -170), GRASS);

            // River (north-south) and lake (south-east), roads.
            K.Height(R(-12, -170, 3, 170), -2.5f, new Color(0.5f, 0.55f, 0.45f));
            K.Height(R(-17, -170, -12, 170), -1.2f, DIRT);
            K.Height(R(3, -170, 8, 170), -1.2f, DIRT);
            K.Water(new Vector3(-4.5f, -1.4f, 0), new Vector2(15, 340));
            K.Height(R(40, -170, 170, -125), -2.5f, new Color(0.5f, 0.55f, 0.45f));
            K.Water(new Vector3(105, -1.4f, -150), new Vector2(130, 50));
            K.Paint(R(-160, -75, -17, -63), DIRT);
            K.Paint(R(8, -75, 75, -63), DIRT);
            K.Paint(R(30, -63, 42, 60), DIRT);
            Clear.Add(R(-170, -78, 170, -60));
            Clear.Add(R(27, -63, 45, 60));
            Clear.Add(R(-20, -170, 10, 170));
            Clear.Add(R(40, -170, 170, -125));

            // North-west cliff plateau with a climbable face and a ramp from the west.
            K.Height(R(-170, 60, -60, 170), 7f, new Color(0.5f, 0.66f, 0.38f));
            K.Climbable(K.Env, new Vector3(-80, 3.5f, 59.6f), new Vector3(10, 7, 0.4f), Quaternion.identity, new Color(0.35f, 0.55f, 0.3f), false);
            K.Ramp(K.Env, new Vector3(-150, 0, 40), new Vector3(-150, 7, 60), 6f, STONE);
            K.Point(MatchPoint.Kind.WeaponSpawn, new Vector3(-95, 7.2f, 66), 0, "Sniper", ItemKind.Sniper);
            K.Rock(new Vector3(-95, 7f, 64), 1.8f, STONE);

            Town();
            Grove();
            Outpost();
            Lodge();
            Wilderness();
            MatchPoints();
        }

        // ---------------------------------------------------------------- Millbrook Town (zone 0)

        private void Town()
        {
            const int Z = 0;
            Color[] lWalls = { new Color(0.92f, 0.82f, 0.62f), new Color(0.75f, 0.5f, 0.38f), new Color(0.62f, 0.72f, 0.78f), new Color(0.85f, 0.65f, 0.55f), new Color(0.8f, 0.8f, 0.7f) };
            Color lRoof = new Color(0.55f, 0.3f, 0.25f);

            // North side of main street.
            House("General Store", -140, -52, new Vector2(12, 10), 180, 2, lWalls[0], lRoof, Z, 2, true, ItemKind.Shotgun, false, 1);
            MapKit.BuildingResult lSaloon = House("Saloon", -115, -52, new Vector2(16, 12), 180, 2, lWalls[1], lRoof, Z, 2, true, ItemKind.Smg);
            K.BarTable(lSaloon.Local(3, 0, 2), 180, Z, 3);
            K.BarTable(lSaloon.Local(-4, 0, -2), 0, Z, 2);
            K.Distraction(lSaloon.Local(5, 0, -4.5f), 180, "radio");
            K.Toilet(lSaloon.Local(-6.5f, 3.4f, 4.5f), 180, Z);
            K.Activities(lSaloon.Local(0, 3.5f, 0), 0, Z, ActivityType.Office, ActivityType.Phone);
            House("Sheriff", -90, -52, new Vector2(12, 10), 180, 1, lWalls[2], lRoof, Z, 2, true, ItemKind.Pistol);
            K.Activity(ActivityType.Office, K.At(-90, -50, 0.1f), 0, Z);
            House("Diner", -65, -52, new Vector2(14, 10), 180, 1, lWalls[3], lRoof, Z, 2, true, ItemKind.Pistol);
            K.BarTable(K.At(-62, -50), 0, Z, 2);
            K.Activity(ActivityType.Dining, K.At(-68, -50, 0.1f), 0, Z);
            K.Toilet(K.At(-71, -55.5f), 0, Z);
            House("Barber", -42, -52, new Vector2(10, 9), 180, 2, lWalls[4], lRoof, Z, 2, false, ItemKind.Coin);

            // South side.
            MapKit.BuildingResult lSawmill = SawMill(-128, -95);
            House("Hotel Pine", -95, -88, new Vector2(16, 12), 0, 3, lWalls[0], lRoof, Z, 0, true, ItemKind.Smg, false, 3);
            House("Post Office", -68, -86, new Vector2(12, 10), 0, 1, lWalls[2], lRoof, Z, 0, true, ItemKind.Pistol);
            GasStation(-40, -92);
            _ = lSawmill;

            // Water tower: ladder, sniper nest, zipline over the river toward the woods.
            Vector3 lTower = K.At(-30, -30);
            for (int i = 0; i < 4; i++) K.Block(K.Env, "TowerLeg", lTower + new Vector3(i % 2 == 0 ? -2.5f : 2.5f, 7, i < 2 ? -2.5f : 2.5f), new Vector3(0.4f, 14, 0.4f), WOOD);
            K.Block(K.Env, "TowerDeck", lTower + Vector3.up * 14, new Vector3(7, 0.4f, 7), WOOD);
            K.Mesh(K.Env, "Tank", EditorAssets.SaveMesh(FX.LowPolyMesh.Cylinder(10), "Kit_Cyl10"), lTower + Vector3.up * 17, new Vector3(5.5f, 5, 5.5f), new Color(0.5f, 0.6f, 0.65f), null, true);
            K.Climbable(K.Env, lTower + new Vector3(0, 7.2f, -3.6f), new Vector3(0.8f, 14.4f, 0.15f), Quaternion.identity, new Color(0.5f, 0.4f, 0.3f), true);
            K.Weapon(ItemKind.Sniper, lTower + new Vector3(2.5f, 14.3f, 2.5f));
            K.Npc(NpcRole.Lookout, lTower + new Vector3(-2, 14.3f, 2), 90, Z, ItemKind.Sniper);
            K.Zipline(lTower + new Vector3(3, 14.2f, 0), K.At(25, -15));
            Clear.Add(R(-36, -36, -24, -24));

            // Bridges over the river: road bridge and footbridge; dry banks under them.
            K.Bridge(new Vector3(-20, 0.1f, -69), new Vector3(10, 0.1f, -69), 9f, STONE);
            K.Bridge(new Vector3(-20, 0.1f, 40), new Vector3(10, 0.1f, 40), 3f, WOOD);
            K.Bridge(new Vector3(-20, 0.1f, 115), new Vector3(10, 0.1f, 115), 3f, WOOD);
            K.Crate(new Vector3(-14, -1.2f, -66));
            K.Crate(new Vector3(4, -1.2f, -72));
            K.Gadget(ItemKind.SmokeGrenade, new Vector3(-14.5f, -1.1f, -70));
            K.Gadget(ItemKind.EmeticPoison, new Vector3(5, -1.1f, 37));
            K.Npc(NpcRole.Guard, K.At(-24, -69, 0.1f), 90, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, K.At(14, -69, 0.1f), 270, Z, ItemKind.Smg);

            // Street life: patrols, cars, lamps, cover.
            K.Patrol(Z, ItemKind.Smg, 3f, K.At(-150, -69), K.At(-30, -69), K.At(-30, -62), K.At(-150, -62));
            K.Patrol(Z, ItemKind.Pistol, 4f, K.At(-110, -78), K.At(-50, -78), K.At(-50, -100), K.At(-110, -100));
            K.Patrol(Z, ItemKind.Shotgun, 2f, K.At(-60, -35), K.At(-100, -35), K.At(-130, -30));
            K.Car(K.At(-100, -64), 90, new Color(0.85f, 0.3f, 0.3f));
            K.Car(K.At(-75, -74), 270, new Color(0.3f, 0.5f, 0.85f));
            K.Car(K.At(-48, -64), 85, new Color(0.9f, 0.8f, 0.4f));
            K.Distraction(K.At(-48, -66, 0.9f), 0, "caralarm");
            for (int x = -150; x <= -30; x += 20) K.Lamp(K.At(x, -61), new Color(1f, 0.85f, 0.6f));
            Post(Z, -150, -69, 90, ItemKind.Smg);
            Post(Z, -80, -40, 180, ItemKind.Pistol);
            K.Barrel(K.At(-36, -82), true);
            K.Barrel(K.At(-38, -83), false);
            K.HidingSpot(K.At(-58, -80), 0, "dumpster");
            K.HidingSpot(K.At(-120, -78), 90, "dumpster");
            K.Npc(NpcRole.Guard, K.At(-120, -40, 0.1f), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, K.At(-70, -100, 0.1f), 0, Z, ItemKind.Pistol);
            BaseActivities(Z, new Vector3(-85, 0, -69), 10f);
            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-90, 0, -70), new Vector3(150, 60, 110), K.At(-160, -69, 0.1f), new Color(0.55f, 0.45f, 0.35f), HatStyle.Cowboy, K.At(-115, -52, 3.5f));
            Forest(R(-170, -170, -20, -110), 60, MapKit.TreeKind.Pine, 0.9f, 1.4f, PINE);
            Forest(R(-170, -45, -20, 55), 70, MapKit.TreeKind.Round, 0.8f, 1.2f);
        }

        private MapKit.BuildingResult SawMill(float pX, float pZ)
        {
            MapKit.BuildingSpec lSpec = new MapKit.BuildingSpec
            {
                name = "Sawmill", origin = K.At(pX, pZ), size = new Vector2(26, 18), floors = 2, floorHeight = 4.5f,
                wall = new Color(0.62f, 0.42f, 0.3f), trim = new Color(0.4f, 0.28f, 0.2f), roof = new Color(0.45f, 0.45f, 0.5f), climbSide = 3, windowSpacing = 6f
            };
            lSpec.openings.Add(new MapKit.Opening(2, 0.5f, 0, 5f, 0, 4f));
            lSpec.openings.Add(new MapKit.Opening(0, 0.3f));
            lSpec.openings.Add(new MapKit.Opening(1, 0.5f, 1, 3f, 0, 3f));
            MapKit.BuildingResult lMill = K.Building(lSpec);
            Clear.Add(R(pX - 16, pZ - 12, pX + 16, pZ + 12));
            K.CrateStack(lMill.Local(-8, 0, -4), 6);
            K.CrateStack(lMill.Local(6, 0, 4), 4);
            for (int i = 0; i < 4; i++) K.Block(lMill.interior, "Log", lMill.Local(-2 + i * 1.1f, 0.5f, -4), new Vector3(1f, 1f, 7f), new Color(0.55f, 0.38f, 0.22f), Core.GameLayers.DEFAULT, true, lMill.Rotation, true).AddComponent<Flammable>().Setup(25f, 3f, false, 1.4f);
            K.Distraction(lMill.Local(8, 0, -6), 0, "generator");
            K.Npc(NpcRole.Guard, lMill.Local(0, 0.1f, 2), 0, 0, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, lMill.Local(4, 4.6f, 0), 180, 0, ItemKind.Smg);
            K.Activity(ActivityType.Inspect, lMill.Local(-4, 0.1f, 2), 90, 0);
            K.Activity(ActivityType.Workout, lMill.Local(4, 0.1f, -2), 90, 0);
            K.UniqueWeapon(ItemKind.RocketLauncher, lMill.Local(-10, 4.6f, 6), "Sawmill loft");
            K.Weapon(ItemKind.Smg, lMill.Local(10, 0.1f, 6));
            K.Gadget(ItemKind.C4, lMill.Local(-10, 0.1f, 6));
            K.Lamp(lMill.Local(0, 4.2f, 0), new Color(1f, 0.85f, 0.6f), 14f, 2f, false);
            return lMill;
        }

        private void GasStation(float pX, float pZ)
        {
            Vector3 lBase = K.At(pX, pZ);
            K.Block(K.Env, "Canopy", lBase + new Vector3(0, 4.5f, 6), new Vector3(12, 0.4f, 8), new Color(0.9f, 0.3f, 0.25f));
            for (int i = 0; i < 4; i++) K.Block(K.Env, "Pillar", lBase + new Vector3(i % 2 == 0 ? -5 : 5, 2.25f, i < 2 ? 3 : 9), new Vector3(0.4f, 4.5f, 0.4f), Color.white);
            K.Barrel(lBase + new Vector3(-2, 0, 6), true);
            K.Barrel(lBase + new Vector3(2, 0, 6), true);
            House("Gas Shop", pX, pZ - 6, new Vector2(10, 7), 0, 1, new Color(0.95f, 0.95f, 0.9f), new Color(0.9f, 0.3f, 0.25f), 0, 0, true, ItemKind.Shotgun);
            K.UniqueWeapon(ItemKind.Flamethrower, K.At(pX + 3, pZ - 7, 0.1f), "Gas station back room");
            Clear.Add(R(pX - 8, pZ - 12, pX + 8, pZ + 12));
        }

        // ---------------------------------------------------------------- The Grove (zone 1)

        private void Grove()
        {
            const int Z = 1;
            Vector2 c = new Vector2(70, 85);
            // Marsh ring (moat) with stilt walkways.
            K.Height(R(c.x - 40, c.y - 35, c.x + 40, c.y - 28), -1.5f, new Color(0.45f, 0.55f, 0.35f));
            K.Height(R(c.x - 40, c.y + 28, c.x + 40, c.y + 35), -1.5f, new Color(0.45f, 0.55f, 0.35f));
            K.Height(R(c.x - 40, c.y - 35, c.x - 33, c.y + 35), -1.5f, new Color(0.45f, 0.55f, 0.35f));
            K.Water(new Vector3(c.x, -0.7f, c.y - 31.5f), new Vector2(80, 7));
            K.Water(new Vector3(c.x, -0.7f, c.y + 31.5f), new Vector2(80, 7));
            K.Water(new Vector3(c.x - 36.5f, -0.7f, c.y), new Vector2(7, 70));
            K.Bridge(new Vector3(c.x, 0.2f, c.y - 37), new Vector3(c.x, 0.2f, c.y - 26), 4f, WOOD);
            K.Bridge(new Vector3(c.x - 42, 0.2f, c.y + 10), new Vector3(c.x - 31, 0.2f, c.y + 10), 2f, WOOD);
            K.Bridge(new Vector3(c.x - 20, 0.2f, c.y + 37), new Vector3(c.x - 20, 0.2f, c.y + 26), 2f, WOOD);
            K.Grass(new Vector3(c.x - 50, 0, c.y - 20), new Vector2(18, 30), new Color(0.75f, 0.78f, 0.4f));
            K.Grass(new Vector3(c.x + 5, 0, c.y - 48), new Vector2(40, 14), new Color(0.75f, 0.78f, 0.4f));
            K.Grass(new Vector3(c.x - 10, 0, c.y + 48), new Vector2(40, 12), new Color(0.72f, 0.75f, 0.38f));

            // Palisade with a gate (south) and gaps you can squeeze through.
            Color lLog = new Color(0.5f, 0.35f, 0.22f);
            K.Fence(new Vector3(c.x - 26, 0, c.y - 24), new Vector3(c.x - 3, 0, c.y - 24), 3.5f, lLog, true);
            K.Fence(new Vector3(c.x + 3, 0, c.y - 24), new Vector3(c.x + 26, 0, c.y - 24), 3.5f, lLog, true);
            K.Fence(new Vector3(c.x - 26, 0, c.y + 24), new Vector3(c.x + 26, 0, c.y + 24), 3.5f, lLog, true);
            K.Fence(new Vector3(c.x - 26, 0, c.y - 24), new Vector3(c.x - 26, 0, c.y + 6), 3.5f, lLog, true);
            K.Fence(new Vector3(c.x - 26, 0, c.y + 12), new Vector3(c.x - 26, 0, c.y + 24), 3.5f, lLog, true);
            K.Fence(new Vector3(c.x + 26, 0, c.y - 24), new Vector3(c.x + 26, 0, c.y + 24), 3.5f, lLog, true);
            K.Climbable(K.Env, new Vector3(c.x + 15, 1.75f, c.y + 24.3f), new Vector3(1.2f, 3.5f, 0.3f), Quaternion.identity, new Color(0.35f, 0.55f, 0.3f), false);
            K.Npc(NpcRole.Guard, new Vector3(c.x - 3, 0.1f, c.y - 27), 180, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, new Vector3(c.x + 3, 0.1f, c.y - 27), 180, Z, ItemKind.Smg);
            Clear.Add(R(c.x - 45, c.y - 40, c.x + 45, c.y + 40));

            // Longhouse (speeches), cabins, greenhouse, kitchen.
            MapKit.BuildingSpec lLong = new MapKit.BuildingSpec { name = "Longhouse", origin = new Vector3(c.x, 0, c.y + 10), size = new Vector2(24, 10), floors = 1, floorHeight = 4.2f, wall = WOOD, trim = WOOD * 0.7f, roof = new Color(0.45f, 0.35f, 0.25f), pitchedRoof = true, stairs = false };
            lLong.openings.Add(new MapKit.Opening(0, 0.5f, 0, 3f, 0, 3.2f));
            lLong.openings.Add(new MapKit.Opening(2, 0.2f));
            MapKit.BuildingResult lHall = K.Building(lLong);
            K.Activity(ActivityType.Speech, lHall.Local(0, 0.1f, 3), 180, Z, 18f);
            K.BarTable(lHall.Local(-6, 0, -1), 0, Z, 3);
            K.BarTable(lHall.Local(6, 0, -1), 0, Z, 2);
            K.Activity(ActivityType.Dining, lHall.Local(-3, 0.1f, -2), 0, Z);
            K.Npc(NpcRole.Guard, lHall.Local(-9, 0.1f, 2), 90, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, lHall.Local(9, 0.1f, 2), 270, Z, ItemKind.Shotgun);
            for (int i = 0; i < 4; i++)
            {
                float x = c.x - 18 + i * 12f;
                MapKit.BuildingResult lCabin = House("Cabin" + i, x, c.y - 10, new Vector2(7, 6), 0, 1, WOOD * (0.9f + i * 0.04f), new Color(0.45f, 0.35f, 0.25f), Z, 0, i % 2 == 0, i == 1 ? ItemKind.Shotgun : ItemKind.Pistol, true);
                if (i == 2) K.Toilet(lCabin.Local(2, 0, 1.5f), 0, Z);
                if (i == 3) K.Distraction(lCabin.Local(-2, 0, 1.8f), 180, "radio");
            }
            MapKit.BuildingSpec lGreen = new MapKit.BuildingSpec { name = "Greenhouse", origin = new Vector3(c.x + 18, 0, c.y + 14), size = new Vector2(8, 12), floors = 1, wall = new Color(0.75f, 0.9f, 0.85f), trim = Color.white, roof = new Color(0.8f, 0.95f, 0.9f), windowSpacing = 2f, stairs = false, roofAccess = false };
            lGreen.openings.Add(new MapKit.Opening(0, 0.5f));
            MapKit.BuildingResult lGreenhouse = K.Building(lGreen);
            for (int i = 0; i < 3; i++) K.HidingSpot(lGreenhouse.Local(-2, 0, -3 + i * 3), 90, "hay");
            K.Activity(ActivityType.Garden, lGreenhouse.Local(2, 0.1f, 0), 270, Z);
            K.Gadget(ItemKind.Poison, lGreenhouse.Local(2.5f, 0.1f, 4));
            K.UniqueWeapon(ItemKind.Flamethrower, lGreenhouse.Local(-2.5f, 0.1f, 4.5f), "Grove greenhouse");
            K.Toilet(new Vector3(c.x - 20, 0, c.y + 20), 180, Z);
            K.Distraction(new Vector3(c.x - 5, 0, c.y - 2), 0, "radio");
            for (int i = 0; i < 4; i++) K.HidingSpot(new Vector3(c.x - 22 + i * 3, 0, c.y + 1), 0, "hay");

            K.Patrol(Z, ItemKind.Smg, 3f, new Vector3(c.x - 22, 0.1f, c.y - 20), new Vector3(c.x + 22, 0.1f, c.y - 20), new Vector3(c.x + 22, 0.1f, c.y + 20), new Vector3(c.x - 22, 0.1f, c.y + 20));
            K.Patrol(Z, ItemKind.Shotgun, 4f, new Vector3(c.x - 45, 0.1f, c.y - 40), new Vector3(c.x + 45, 0.1f, c.y - 40), new Vector3(c.x + 45, 0.1f, c.y + 40));
            K.Npc(NpcRole.Guard, new Vector3(c.x, 0.1f, c.y), 0, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, new Vector3(c.x + 10, 0.1f, c.y - 2));
            K.Gadget(ItemKind.Brick, new Vector3(c.x - 30, 0.1f, c.y - 45));
            BaseActivities(Z, new Vector3(c.x, 0, c.y + 1), 8f);
            K.Lamp(new Vector3(c.x - 10, 0, c.y - 18), new Color(1f, 0.7f, 0.4f));
            K.Lamp(new Vector3(c.x + 10, 0, c.y + 18), new Color(1f, 0.7f, 0.4f));
            Forest(R(10, 30, 170, 170), 140, MapKit.TreeKind.Pine, 1f, 1.6f, PINE);
        }

        // ---------------------------------------------------------------- Military outpost (zone 1)

        private void Outpost()
        {
            const int Z = 1;
            Vector2 c = new Vector2(138, 85);
            Clear.Add(R(c.x - 25, c.y - 30, c.x + 32, c.y + 30));
            K.Paint(R(c.x - 25, c.y - 30, c.x + 32, c.y + 30), DIRT);
            Color lFence = new Color(0.55f, 0.55f, 0.5f);
            K.Fence(new Vector3(c.x - 25, 0, c.y - 30), new Vector3(c.x - 25, 0, c.y + 30), 2.4f, lFence);
            K.Fence(new Vector3(c.x - 25, 0, c.y + 30), new Vector3(c.x + 30, 0, c.y + 30), 2.4f, lFence);
            K.Fence(new Vector3(c.x - 25, 0, c.y - 30), new Vector3(c.x - 5, 0, c.y - 30), 2.4f, lFence);
            K.Fence(new Vector3(c.x + 5, 0, c.y - 30), new Vector3(c.x + 30, 0, c.y - 30), 2.4f, lFence);

            Watchtower(Z, c.x - 22, c.y - 27, 7f, 200, WOOD);
            Watchtower(Z, c.x + 27, c.y + 27, 7f, 20, WOOD);
            Watchtower(Z, c.x - 22, c.y + 27, 7f, 300, WOOD);

            Color lArmy = new Color(0.45f, 0.52f, 0.35f);
            MapKit.BuildingResult lBarracks = House("Barracks", c.x + 8, c.y + 14, new Vector2(18, 8), 0, 1, lArmy, lArmy * 0.7f, Z, 0, true, ItemKind.Smg, true);
            K.Activity(ActivityType.Workout, lBarracks.Local(4, 0.1f, 0), 0, Z);
            House("HQ", c.x + 12, c.y - 12, new Vector2(12, 10), 0, 2, lArmy, lArmy * 0.6f, Z, 2, true, ItemKind.Pistol, false, 1);
            K.Activity(ActivityType.Office, K.At(c.x + 12, c.y - 10, 3.5f), 0, Z);
            K.Distraction(K.At(c.x + 15, c.y - 15), 0, "radio");

            // Motor pool.
            for (int i = 0; i < 3; i++) K.Car(K.At(c.x - 12 + i * 5, c.y - 18), 0, lArmy);
            K.Barrel(K.At(c.x - 16, c.y - 12), true);
            K.Barrel(K.At(c.x - 15, c.y - 11), true);
            K.CrateStack(K.At(c.x - 8, c.y + 2), 5);
            K.Gadget(ItemKind.C4, K.At(c.x - 8, c.y + 4.5f, 2.2f));

            // Bunker with a basement, the comms mast and the alarm flare.
            MapKit.BuildingSpec lBunker = new MapKit.BuildingSpec { name = "Bunker", origin = K.At(c.x - 10, c.y + 15), size = new Vector2(10, 8), floors = 1, floorHeight = 3f, wall = STONE, trim = STONE * 0.8f, roof = STONE * 0.9f, basement = true, windowSpacing = 0f };
            lBunker.openings.Add(new MapKit.Opening(0, 0.5f));
            K.Hole(R(c.x - 15, c.y + 11, c.x - 5, c.y + 19));
            MapKit.BuildingResult lB = K.Building(lBunker);
            K.UniqueWeapon(ItemKind.RocketLauncher, lB.Local(-3, -2.9f, -2), "Outpost bunker basement");
            K.Weapon(ItemKind.Shotgun, lB.Local(3, -2.9f, -2));
            K.Npc(NpcRole.Guard, lB.Local(0, -2.9f, 0), 0, Z, ItemKind.Shotgun);
            Vector3 lMast = K.At(c.x + 22, c.y);
            K.Block(K.Env, "Mast", lMast + Vector3.up * 10, new Vector3(0.6f, 20, 0.6f), new Color(0.8f, 0.2f, 0.2f));
            K.Alarm(K.At(c.x + 20, c.y + 3), 270);
            K.Npc(NpcRole.Guard, K.At(c.x + 18, c.y + 3, 0.1f), 270, Z, ItemKind.Smg);

            K.Patrol(Z, ItemKind.Smg, 2.5f, K.At(c.x - 20, c.y - 25), K.At(c.x + 25, c.y - 25), K.At(c.x + 25, c.y + 25), K.At(c.x - 20, c.y + 25));
            Post(Z, c.x, c.y - 33, 180, ItemKind.Smg);
            Post(Z, c.x + 5, c.y, 270, ItemKind.Shotgun);
            K.Weapon(ItemKind.Smg, K.At(c.x + 3, c.y - 25, 0.1f));
            K.Weapon(ItemKind.Sniper, K.At(c.x + 28, c.y - 5, 0.1f));
            K.Point(MatchPoint.Kind.Extraction, K.At(c.x + 20, c.y - 22, 0.1f), 0, "Outpost helipad");
            K.Block(K.Env, "Helipad", K.At(c.x + 20, c.y - 22, 0.05f), new Vector3(9, 0.1f, 9), new Color(0.35f, 0.35f, 0.4f));
            K.Zone(Z, K.Map.zoneNames[1], new Vector3(100, 0, 85), new Vector3(150, 60, 100), K.At(160, 125, 0.1f), new Color(0.42f, 0.52f, 0.32f), HatStyle.Beret, new Vector3(70, 0.2f, 95));
        }

        // ---------------------------------------------------------------- Lakeside Lodge (zone 2)

        private void Lodge()
        {
            const int Z = 2;
            const float PLINTH = 3.4f;
            Vector2 c = new Vector2(100, -85);
            float W = 36, D = 24;

            // Terrace on three sides, the service yard (west) stays at ground level by the basement door.
            K.Height(R(c.x - W * 0.5f - 6, c.y - D * 0.5f - 8, c.x + W * 0.5f + 8, c.y + D * 0.5f + 8), PLINTH, STONE * 1.05f);
            K.Height(R(c.x - W * 0.5f - 12, c.y - 6, c.x - W * 0.5f - 1, c.y + 6), 0f, DIRT);
            K.Hole(R(c.x - W * 0.5f, c.y - D * 0.5f, c.x + W * 0.5f, c.y + D * 0.5f));
            K.Stairs(K.Env, new Vector3(c.x, 0, c.y + D * 0.5f + 15), new Vector3(c.x, PLINTH, c.y + D * 0.5f + 8), 6f, STONE);
            K.Ramp(K.Env, new Vector3(c.x + W * 0.5f + 16, 0, c.y), new Vector3(c.x + W * 0.5f + 8, PLINTH, c.y), 5f, STONE);
            Clear.Add(R(c.x - W * 0.5f - 15, c.y - D * 0.5f - 12, c.x + W * 0.5f + 20, c.y + D * 0.5f + 18));

            MapKit.BuildingSpec lSpec = new MapKit.BuildingSpec
            {
                name = "Lakeside Lodge", origin = new Vector3(c.x, PLINTH, c.y), size = new Vector2(W, D), floors = 2, floorHeight = 3.4f,
                wall = new Color(0.62f, 0.44f, 0.3f), trim = new Color(0.45f, 0.32f, 0.22f), floor = new Color(0.55f, 0.4f, 0.3f), roof = new Color(0.35f, 0.38f, 0.42f),
                basement = true, climbSide = 2, climbAt = 0.85f, windowSpacing = 4.5f
            };
            lSpec.openings.Add(new MapKit.Opening(2, 0.5f, 0, 3f, 0, 3f));
            lSpec.openings.Add(new MapKit.Opening(0, 0.5f, 0, 3f, 0, 3f));
            lSpec.openings.Add(new MapKit.Opening(1, 0.5f));
            lSpec.openings.Add(new MapKit.Opening(3, 0.5f, -1, 2.4f, 0, 2.6f));
            lSpec.openings.Add(new MapKit.Opening(0, 0.2f, 1, 2f, 0, 2.6f));
            MapKit.BuildingResult L = K.Building(lSpec);

            // Ground floor: grand hall (chandelier), bar, dining, toilets.
            K.Partition(L, 0, new Vector3(-6, 0, -D * 0.5f), new Vector3(-6, 0, D * 0.5f), 0.5f, lSpec.wall * 0.9f);
            K.Partition(L, 0, new Vector3(8, 0, -D * 0.5f), new Vector3(8, 0, D * 0.5f), 0.3f, lSpec.wall * 0.9f);
            K.Chandelier(L.Local(1, 3.3f, 0), 3.2f);
            K.BarTable(L.Local(-11, 0, -6), 0, Z, 3);
            K.BarTable(L.Local(-11, 0, 4), 180, Z, 3);
            K.Activity(ActivityType.Dining, L.Local(-13, 0.1f, 0), 90, Z);
            K.BarTable(L.Local(3, 0, 6), 180, Z, 2);
            K.Distraction(L.Local(4, 0, -10.5f), 0, "tv");
            K.Toilet(L.Local(14, 0, -9), 0, Z);
            K.Toilet(L.Local(16, 0, -9), 0, Z);
            K.Activity(ActivityType.Speech, L.Local(1, 0.1f, -4), 0, Z, 16f);
            K.HidingSpot(L.Local(12, 0, 10.5f), 180, "closet");
            K.Npc(NpcRole.Guard, L.Local(-2, 0.1f, 9), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, L.Local(-2, 0.1f, -9), 0, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, L.Local(12, 0.1f, 2), 270, Z, ItemKind.Pistol);

            // Upstairs: offices, the master suite, a phone.
            K.Partition(L, 1, new Vector3(-4, 0, -D * 0.5f), new Vector3(-4, 0, D * 0.5f), 0.5f, lSpec.wall);
            K.Partition(L, 1, new Vector3(-4, 0, 0), new Vector3(W * 0.5f - 3, 0, 0), 0.6f, lSpec.wall);
            K.Activity(ActivityType.Office, L.Local(6, 3.5f, 6), 0, Z);
            K.Activity(ActivityType.Phone, L.Local(6, 3.5f, -6), 0, Z);
            K.Distraction(L.Local(10, 3.4f, -10), 0, "phone");
            K.BarTable(L.Local(-10, 3.4f, 0), 90, Z, 2);
            K.Toilet(L.Local(-15, 3.4f, 10), 180, Z);
            K.Npc(NpcRole.Guard, L.Local(0, 3.5f, 9), 180, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Shotgun, L.Local(14, 3.5f, 8));
            K.UniqueWeapon(ItemKind.RocketLauncher, L.Local(14, 6.9f, -9), "Lodge roof");

            // Basement: utility room (breaker), air handler (vents to every room), cellar.
            BuildingVolume lVolume = K.BuildingVolume("Lakeside Lodge", L.Local(0, 0, 0), new Vector3(W, 7.5f, D), 0);
            K.Breaker(L.Local(-14, -3.4f, -11.3f), 180, lVolume);
            K.Vent(L.Local(10, -3.4f, -8), 0, lVolume, new[]
            {
                K.VentGrill(L.Local(-11, 3.25f, 0)), K.VentGrill(L.Local(1, 3.25f, 7)), K.VentGrill(L.Local(13, 3.25f, -3)),
                K.VentGrill(L.Local(-10, 6.65f, 6)), K.VentGrill(L.Local(8, 6.65f, -6)), K.VentGrill(L.Local(8, 6.65f, 6))
            });
            K.CrateStack(L.Local(0, -3.4f, 6), 6);
            K.Barrel(L.Local(-6, -3.4f, -8), false);
            K.Barrel(L.Local(-7.5f, -3.4f, -8), false);
            K.Npc(NpcRole.Guard, L.Local(4, -3.3f, 0), 270, Z, ItemKind.Smg);
            K.Gadget(ItemKind.Poison, L.Local(-12, -3.3f, 8));
            K.Lamp(L.Local(0, -0.4f, 0), new Color(1f, 0.9f, 0.7f), 14f, 1.6f, false);
            K.Lamp(L.Local(-10, 3f, 0), new Color(1f, 0.85f, 0.6f), 12f, 1.6f, false);
            K.Lamp(L.Local(10, 6.4f, 0), new Color(1f, 0.85f, 0.6f), 12f, 1.6f, false);
            K.Sprinklers(L.Local(0, 0, 0), new Vector3(W, 7f, D), 0, 3.3f);

            // Terrace, garden, boathouse.
            K.BarTable(K.At(c.x + 12, c.y + D * 0.5f + 4), 0, Z, 2);
            K.Activity(ActivityType.Garden, K.At(c.x - 10, c.y + D * 0.5f + 5, 0.1f), 0, Z);
            K.Activity(ActivityType.Smoke, K.At(c.x + W * 0.5f + 4, c.y - 6, 0.1f), 90, Z);
            K.Activity(ActivityType.Lookout, K.At(c.x, c.y - D * 0.5f - 5, 0.1f), 180, Z);
            Post(Z, c.x, c.y + D * 0.5f + 16, 0, ItemKind.Smg);
            Post(Z, c.x - W * 0.5f - 8, c.y, 270, ItemKind.Shotgun);
            K.Patrol(Z, ItemKind.Smg, 3f, K.At(c.x - 20, c.y + 18), K.At(c.x + 24, c.y + 18), K.At(c.x + 24, c.y - 18), K.At(c.x - 20, c.y - 18));
            K.Patrol(Z, ItemKind.Pistol, 3f, K.At(c.x - 30, c.y - 30), K.At(c.x + 40, c.y - 32));
            K.Bridge(new Vector3(c.x + 8, 0.2f, c.y - 32), new Vector3(c.x + 8, 0.2f, c.y - 50), 3f, WOOD);
            House("Boathouse", c.x + 8, c.y - 56, new Vector2(8, 8), 0, 1, WOOD, new Color(0.35f, 0.38f, 0.42f), Z, 2, true, ItemKind.Smg, true);
            K.Point(MatchPoint.Kind.Extraction, K.At(c.x + 20, c.y - 45, 0.1f), 0, "Boathouse dock");
            K.Distraction(K.At(c.x + 4, c.y - 40), 0, "generator");
            BaseActivities(Z, new Vector3(c.x + 30, 0, c.y + 22), 6f);
            K.Zone(Z, K.Map.zoneNames[2], new Vector3(c.x, 0, c.y), new Vector3(110, 60, 90), K.At(c.x + 55, c.y + 40, 0.1f), new Color(0.25f, 0.28f, 0.35f), HatStyle.None, L.Local(14, 3.5f, 8));
            Forest(R(20, -125, 170, -40), 50, MapKit.TreeKind.Round, 0.9f, 1.3f, new Color(0.85f, 0.6f, 0.3f));
        }

        // ---------------------------------------------------------------- the rest

        private void Wilderness()
        {
            Forest(R(10, -60, 170, 30), 90, MapKit.TreeKind.Pine, 0.9f, 1.5f, PINE);
            Forest(R(-170, 60, -20, 170), 60, MapKit.TreeKind.Pine, 1f, 1.5f, PINE);
            Rocks(R(-170, -170, 170, 170), 70, STONE);
            K.Grass(new Vector3(-60, 0, 20), new Vector2(30, 20), new Color(0.7f, 0.75f, 0.35f));
            K.Grass(new Vector3(30, 0, -20), new Vector2(16, 30), new Color(0.7f, 0.75f, 0.35f));
            K.Grass(new Vector3(-120, 0, 30), new Vector2(24, 16), new Color(0.7f, 0.75f, 0.35f));
            K.JumpPad(K.At(22, -55), 0, new Vector3(0, 15, 11));
            K.PatrolCar(8f, 0f, new Color(0.35f, 0.45f, 0.3f), K.At(-160, -71, 0.05f), K.At(70, -71, 0.05f), K.At(70, -67, 0.05f), K.At(-160, -67, 0.05f));
            K.PatrolCar(6f, 60f, new Color(0.55f, 0.35f, 0.25f), K.At(36, -60, 0.05f), K.At(36, 55, 0.05f), K.At(40, 55, 0.05f), K.At(40, -60, 0.05f));
            K.JumpPad(K.At(-40, 10), 180, new Vector3(0, 13, 9));

            // Old mine entrance (north-west, on the plateau): an extraction.
            K.Block(K.Env, "MineFrame", new Vector3(-140, 9.5f, 150), new Vector3(6, 5, 1), WOOD);
            K.Point(MatchPoint.Kind.Extraction, new Vector3(-140, 7.1f, 140), 0, "Old mine");

            // Weapons scattered in the wild (many guns, everywhere).
            float[,] lSpots = { { -120, 10 }, { -60, 0 }, { 30, -40 }, { 60, 20 }, { 20, 120 }, { -50, 150 }, { 150, -20 }, { 160, 150 }, { -160, -120 }, { -20, -140 }, { 60, -110 } };
            ItemKind[] lKinds = { ItemKind.Smg, ItemKind.Shotgun, ItemKind.Sniper, ItemKind.Pistol, ItemKind.Smg, ItemKind.Sniper, ItemKind.Shotgun, ItemKind.Smg, ItemKind.Pistol, ItemKind.Smg, ItemKind.Sniper };
            for (int i = 0; i < lSpots.GetLength(0); i++) K.Weapon(lKinds[i], K.At(lSpots[i, 0], lSpots[i, 1], 0.1f));
            K.Gadget(ItemKind.SmokeGrenade, K.At(-100, 0, 0.1f));
            K.Gadget(ItemKind.Coin, K.At(40, 0, 0.1f));
            K.Gadget(ItemKind.C4, K.At(130, 20, 0.1f));
            K.Gadget(ItemKind.EmeticPoison, K.At(-130, -80, 0.1f));
            K.Gadget(ItemKind.Poison, K.At(-75, -95, 0.1f));
            K.UniqueWeapon(ItemKind.Flamethrower, K.At(-140, 140, 7.1f), "Old mine");
        }

        private void MatchPoints()
        {
            K.Point(MatchPoint.Kind.Start, K.At(-150, -150, 0.2f), 45, "South woods");
            K.Point(MatchPoint.Kind.Start, K.At(-40, 100, 0.2f), 120, "River north");
            K.Point(MatchPoint.Kind.Start, K.At(30, 0, 0.2f), 0, "Clearing");
            K.Point(MatchPoint.Kind.Start, K.At(160, -30, 0.2f), 270, "East road");
            K.Point(MatchPoint.Kind.Extraction, K.At(-160, -69, 0.1f), 90, "Logging truck");
        }
    }
}
