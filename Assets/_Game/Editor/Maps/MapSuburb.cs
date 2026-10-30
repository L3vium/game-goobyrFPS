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
    /// <b>Elmridge</b> - an American suburb that climbs to a hill.
    /// <list type="bullet">
    /// <item><b>Downtown Strip</b> (south-west): a main street with the diner, the mall, the police
    /// station, a cinema with a ladder to its roof sign, a gas station and parking lots; police
    /// cars patrol the loop.</item>
    /// <item><b>Junkyard Bowl</b> (south-east): car wrecks stacked into walls, a scrap fence, the
    /// abandoned bowling alley, a drive-in screen, a field of tall grass and a drainage canal with
    /// culverts running under the road.</item>
    /// <item><b>Hilltop Mansion</b> (north): a colonial mansion on a plateau reached by a winding
    /// drive - gate, hedge maze, pool and pool house, greenhouse, ballroom chandelier, dining,
    /// bar, study, a basement (breaker, air handler, vault) and sprinklers; an ivy cliff at the
    /// back and a jump pad from the cul-de-sac gardens.</item>
    /// </list>
    /// Between them, cul-de-sacs of houses with fenced backyards, sheds and pools.
    /// </summary>
    public class MapSuburb : MapBuilderBase
    {
        protected override string MapFile => "Map_Suburb";

        [MenuItem("Goobyr/Build/Map Suburb")]
        public static void BuildMenu() => new MapSuburb().Build();

        private static readonly Color LAWN = new Color(0.5f, 0.78f, 0.42f);
        private static readonly Color ROAD = new Color(0.4f, 0.4f, 0.44f);
        private static readonly Color WALK = new Color(0.82f, 0.8f, 0.76f);
        private static readonly Color HEDGE = new Color(0.3f, 0.55f, 0.3f);
        private const float HILL = 8f;

        protected override void Compose()
        {
            K.Grid(68, 68, 5f, new Vector2(-170, -170), LAWN);

            // Roads: main street (east-west), avenue (north-south), canal.
            K.Paint(R(-170, -75, 170, -65), ROAD);
            K.Paint(R(-15, -170, -5, 30), ROAD);
            K.Paint(R(-170, -20, 120, -12), ROAD);
            Clear.Add(R(-170, -78, 170, -62));
            Clear.Add(R(-18, -170, -2, 30));
            Clear.Add(R(-170, -22, 120, -10));
            K.Height(R(100, -170, 110, 30), -2.5f, new Color(0.55f, 0.55f, 0.5f));
            K.Water(new Vector3(105, -1.6f, -70), new Vector2(10, 200));
            Clear.Add(R(98, -170, 112, 30));
            // Culverts: the road crosses the canal on a slab with the channel open below.
            K.Bridge(new Vector3(98, 0.1f, -70), new Vector3(112, 0.1f, -70), 10f, ROAD, 0.6f);
            K.Bridge(new Vector3(98, 0.1f, -16), new Vector3(112, 0.1f, -16), 8f, ROAD, 0.6f);

            // The hill: plateau with a winding drive and an ivy cliff.
            K.Height(R(-60, 40, 130, 170), HILL, new Color(0.45f, 0.72f, 0.38f));
            K.Ramp(K.Env, new Vector3(-40, 0, 20), new Vector3(-40, HILL, 40), 8f, ROAD);
            K.Ramp(K.Env, new Vector3(110, 0, 20), new Vector3(110, HILL, 40), 6f, WALK);
            K.Climbable(K.Env, new Vector3(30, HILL * 0.5f, 39.6f), new Vector3(14, HILL, 0.4f), Quaternion.identity, new Color(0.35f, 0.55f, 0.3f), false);
            K.Climbable(K.Env, new Vector3(-60.4f, HILL * 0.5f, 100), new Vector3(0.4f, HILL, 14), Quaternion.Euler(0, 90, 0), new Color(0.35f, 0.55f, 0.3f), false);

            Downtown();
            Junkyard();
            Mansion();
            Neighbourhood();
            Points();
        }

        // ---------------------------------------------------------------- Downtown (zone 0)

        private void Downtown()
        {
            const int Z = 0;
            K.Paint(R(-160, -62, -20, -55), WALK);
            K.Paint(R(-160, -85, -20, -78), WALK);

            MapKit.BuildingResult lDiner = House("Diner", -140, -50, new Vector2(16, 10), 180, 1, new Color(0.95f, 0.45f, 0.45f), new Color(0.9f, 0.9f, 0.9f), Z, 2, true, ItemKind.Pistol);
            K.BarTable(lDiner.Local(-4, 0, 2), 0, Z, 3);
            K.BarTable(lDiner.Local(4, 0, 2), 0, Z, 2);
            K.Activity(ActivityType.Dining, lDiner.Local(0, 0.1f, -2), 0, Z);
            K.Toilet(lDiner.Local(7, 0, -4), 0, Z);
            K.Distraction(lDiner.Local(-7, 0, -4.5f), 0, "radio");

            // Mall: two floors, a big atrium, a roof with a skylight hole.
            MapKit.BuildingSpec lMallSpec = new MapKit.BuildingSpec { name = "Elmridge Mall", origin = K.At(-100, -40), size = new Vector2(36, 24), floors = 2, floorHeight = 4.2f, wall = new Color(0.9f, 0.85f, 0.78f), trim = new Color(0.4f, 0.55f, 0.75f), roof = new Color(0.6f, 0.62f, 0.65f), windowSpacing = 5f, climbSide = 3 };
            lMallSpec.openings.Add(new MapKit.Opening(0, 0.5f, 0, 5f, 0, 3.6f));
            lMallSpec.openings.Add(new MapKit.Opening(2, 0.3f));
            lMallSpec.openings.Add(new MapKit.Opening(1, 0.5f));
            MapKit.BuildingResult lMall = K.Building(lMallSpec);
            Clear.Add(R(-120, -54, -80, -26));
            for (int i = 0; i < 4; i++)
            {
                K.Partition(lMall, 0, new Vector3(-18 + i * 9, 0, 4), new Vector3(-18 + i * 9, 0, 12), -1f, lMallSpec.trim);
                K.CrateStack(lMall.Local(-14 + i * 9, 0, 8), 2);
            }
            K.Activities(lMall.Local(0, 0.1f, -4), 0, Z, ActivityType.Phone, ActivityType.Idle, ActivityType.Inspect);
            K.BarTable(lMall.Local(10, 4.2f, -6), 0, Z, 2);
            K.Toilet(lMall.Local(16, 4.2f, 10), 270, Z);
            K.Npc(NpcRole.Guard, lMall.Local(0, 0.1f, -9), 180, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, lMall.Local(-10, 4.3f, 0), 90, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Shotgun, lMall.Local(-16, 4.3f, -10));
            K.Gadget(ItemKind.SmokeGrenade, lMall.Local(14, 0.1f, -10));

            MapKit.BuildingResult lPolice = House("Police", -55, -45, new Vector2(18, 12), 180, 2, new Color(0.75f, 0.8f, 0.9f), new Color(0.3f, 0.35f, 0.5f), Z, 2, true, ItemKind.Shotgun, false, 1);
            K.Activity(ActivityType.Office, lPolice.Local(4, 0.1f, 2), 0, Z);
            K.Activity(ActivityType.Lookout, lPolice.Local(0, 6.9f, 0), 180, Z);
            K.Npc(NpcRole.Guard, lPolice.Local(-4, 3.5f, 2), 180, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Smg, lPolice.Local(6, 3.5f, -3));
            K.Gadget(ItemKind.SmokeGrenade, lPolice.Local(-6, 0.1f, -3));

            // Cinema (south side): ladder to the roof sign, sniper perch over the street.
            MapKit.BuildingResult lCinema = House("Cinema", -120, -98, new Vector2(20, 18), 0, 2, new Color(0.55f, 0.3f, 0.45f), new Color(0.3f, 0.2f, 0.3f), Z, 0, true, ItemKind.Smg, false, 2);
            K.Block(K.Env, "Marquee", lCinema.Local(0, 5f, -10.5f), new Vector3(14, 2.2f, 1.2f), new Color(1.5f, 1.2f, 0.4f));
            K.Distraction(lCinema.Local(0, 0, 6), 180, "tv");
            K.Npc(NpcRole.Lookout, lCinema.Local(6, 6.9f, -6), 0, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, lCinema.Local(-6, 6.9f, -6));

            // Gas station with red pumps.
            Vector3 lGas = K.At(-70, -95);
            K.Block(K.Env, "Canopy", lGas + new Vector3(0, 4.5f, 0), new Vector3(14, 0.4f, 9), new Color(0.2f, 0.5f, 0.85f));
            for (int i = 0; i < 4; i++) K.Block(K.Env, "Pillar", lGas + new Vector3(i % 2 == 0 ? -6 : 6, 2.25f, i < 2 ? -3.5f : 3.5f), new Vector3(0.4f, 4.5f, 0.4f), Color.white);
            K.Barrel(lGas + new Vector3(-2, 0, 0), true);
            K.Barrel(lGas + new Vector3(2, 0, 0), true);
            House("Gas Shop", -70, -106, new Vector2(10, 7), 0, 1, Color.white, new Color(0.2f, 0.5f, 0.85f), Z, 2, true, ItemKind.Pistol);
            K.UniqueWeapon(ItemKind.Flamethrower, K.At(-67, -107, 0.1f), "Gas shop");
            Clear.Add(R(-80, -112, -60, -88));

            for (int i = 0; i < 6; i++) K.Car(K.At(-150 + i * 9, -59), 90 + (i % 2) * 180, new Color(0.3f + i * 0.1f, 0.4f, 0.6f - i * 0.05f));
            for (int x = -160; x <= -20; x += 18) { K.Lamp(K.At(x, -63), new Color(1f, 0.85f, 0.6f)); K.Lamp(K.At(x + 9, -77), new Color(1f, 0.85f, 0.6f)); }
            K.HidingSpot(K.At(-90, -58), 180, "dumpster");
            K.HidingSpot(K.At(-130, -82), 0, "dumpster");
            K.Distraction(K.At(-104, -59, 0.9f), 0, "caralarm");
            K.Patrol(Z, ItemKind.Pistol, 3f, K.At(-160, -60), K.At(-25, -60), K.At(-25, -80), K.At(-160, -80));
            K.Patrol(Z, ItemKind.Smg, 3f, K.At(-150, -30), K.At(-30, -30), K.At(-30, -50));
            K.Patrol(Z, ItemKind.Shotgun, 2f, K.At(-110, -88), K.At(-50, -88), K.At(-50, -110));
            Post(Z, -160, -70, 90, ItemKind.Smg);
            Post(Z, -30, -95, 0, ItemKind.Pistol);
            K.PatrolCar(8f, 0f, new Color(0.15f, 0.15f, 0.2f), K.At(-160, -72, 0.05f), K.At(160, -72, 0.05f), K.At(160, -68, 0.05f), K.At(-160, -68, 0.05f));
            K.PatrolCar(7f, 90f, Color.white, K.At(-12, -160, 0.05f), K.At(-12, 25, 0.05f), K.At(-8, 25, 0.05f), K.At(-8, -160, 0.05f));
            BaseActivities(Z, new Vector3(-90, 0, -70), 9f);
            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-90, 0, -70), new Vector3(150, 60, 90), K.At(-165, -70, 0.1f), new Color(0.3f, 0.4f, 0.6f), HatStyle.Cap, lPolice.Local(4, 0.1f, 2));
        }

        // ---------------------------------------------------------------- Junkyard Bowl (zone 1)

        private void Junkyard()
        {
            const int Z = 1;
            Vector2 c = new Vector2(55, -110);
            K.Paint(R(15, -160, 95, -82), new Color(0.6f, 0.55f, 0.45f));
            Clear.Add(R(15, -160, 95, -82));
            Color lScrap = new Color(0.55f, 0.5f, 0.48f);
            K.Fence(new Vector3(15, 0, -82), new Vector3(45, 0, -82), 3f, lScrap, true);
            K.Fence(new Vector3(55, 0, -82), new Vector3(95, 0, -82), 3f, lScrap, true);
            K.Fence(new Vector3(15, 0, -160), new Vector3(15, 0, -82), 3f, lScrap, true);
            K.Fence(new Vector3(15, 0, -160), new Vector3(95, 0, -160), 3f, lScrap, true);
            K.Climbable(K.Env, new Vector3(15.3f, 1.5f, -120), new Vector3(0.3f, 3, 1.4f), Quaternion.Euler(0, 90, 0), new Color(0.4f, 0.4f, 0.45f), true);

            // Car piles: walls of wrecks with gaps; climbable stacks.
            Color[] lRust = { new Color(0.6f, 0.35f, 0.25f), new Color(0.45f, 0.5f, 0.55f), new Color(0.7f, 0.6f, 0.3f), new Color(0.4f, 0.45f, 0.35f) };
            for (int i = 0; i < 18; i++)
            {
                float x = 22 + (i % 6) * 11f, z = -150 + (i / 6) * 14f;
                K.Car(K.At(x, z), K.Rng.Next(360), lRust[K.Rng.Next(lRust.Length)]);
                if (K.Rng.NextDouble() < 0.5) K.Car(K.At(x, z, 1.8f), K.Rng.Next(360), lRust[K.Rng.Next(lRust.Length)]);
            }
            K.Barrel(K.At(40, -100), true);
            K.Barrel(K.At(41, -101), true);
            K.Barrel(K.At(70, -140), false);
            K.HidingSpot(K.At(30, -95), 0, "dumpster");
            K.Distraction(K.At(80, -95), 0, "generator");

            // The bowling alley.
            MapKit.BuildingSpec lBowl = new MapKit.BuildingSpec { name = "Bowl-O-Rama", origin = K.At(55, -45), size = new Vector2(34, 18), floors = 1, floorHeight = 5f, wall = new Color(0.85f, 0.55f, 0.35f), trim = new Color(0.4f, 0.3f, 0.5f), roof = new Color(0.5f, 0.5f, 0.55f), windowSpacing = 6f, climbSide = 2 };
            lBowl.openings.Add(new MapKit.Opening(0, 0.5f, 0, 4f, 0, 3.2f));
            lBowl.openings.Add(new MapKit.Opening(1, 0.5f));
            lBowl.openings.Add(new MapKit.Opening(3, 0.5f));
            MapKit.BuildingResult B = K.Building(lBowl);
            Clear.Add(R(35, -57, 75, -33));
            for (int i = 0; i < 6; i++) K.Block(B.interior, "Lane", B.Local(-12 + i * 4.5f, 0.05f, 3), new Vector3(2.6f, 0.1f, 11), new Color(0.85f, 0.7f, 0.45f));
            K.Block(B.interior, "Counter", B.Local(0, 0.55f, -6), new Vector3(12, 1.1f, 1f), new Color(0.4f, 0.3f, 0.5f), Core.GameLayers.DEFAULT, true, null, true);
            K.BarTable(B.Local(-12, 0, -6), 0, Z, 3);
            K.BarTable(B.Local(12, 0, -6), 0, Z, 2);
            K.Activities(B.Local(0, 0.1f, -3), 0, Z, ActivityType.Workout, ActivityType.Speech, ActivityType.Smoke);
            K.Toilet(B.Local(16, 0, -8), 270, Z);
            K.Distraction(B.Local(-16, 0, -8), 90, "radio");
            K.Npc(NpcRole.Guard, B.Local(-6, 0.1f, 6), 180, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, B.Local(6, 0.1f, 6), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, B.Local(0, 0.1f, -8), 0, Z, ItemKind.Smg);
            K.UniqueWeapon(ItemKind.RocketLauncher, B.Local(14, 5.1f, 6), "Bowling alley roof");
            K.Gadget(ItemKind.C4, B.Local(-14, 0.1f, 7));

            // Drive-in screen and the tall-grass field (approach unseen from the canal).
            K.Block(K.Env, "Screen", K.At(130, -120, 7), new Vector3(24, 10, 0.6f), Color.white);
            for (int i = 0; i < 2; i++) K.Block(K.Env, "ScreenLeg", K.At(122 + i * 16, -120, 3), new Vector3(0.6f, 6, 0.6f), new Color(0.4f, 0.4f, 0.45f));
            K.Climbable(K.Env, K.At(130, -119.6f, 6), new Vector3(1f, 12, 0.2f), Quaternion.Euler(0, 180, 0), new Color(0.4f, 0.4f, 0.45f), true);
            K.Weapon(ItemKind.Sniper, K.At(130, -120, 12.3f) + new Vector3(0, 0, 0.6f));
            K.Block(K.Env, "ScreenTop", K.At(130, -119.4f, 12.1f), new Vector3(24, 0.3f, 1.4f), new Color(0.5f, 0.5f, 0.55f));
            K.Grass(new Vector3(135, 0, -90), new Vector2(40, 30), new Color(0.78f, 0.75f, 0.42f));
            K.Grass(new Vector3(90, 0, -20), new Vector2(14, 30), new Color(0.78f, 0.75f, 0.42f));
            for (int i = 0; i < 5; i++) K.Car(K.At(118 + i * 6, -140), 0, lRust[i % 4]);

            K.Patrol(Z, ItemKind.Smg, 3f, K.At(20, -88), K.At(90, -88), K.At(90, -155), K.At(20, -155));
            K.Patrol(Z, ItemKind.Shotgun, 2f, K.At(30, -30), K.At(80, -30), K.At(80, -60));
            K.Patrol(Z, ItemKind.Pistol, 4f, K.At(115, -100), K.At(150, -100), K.At(150, -150));
            Post(Z, 50, -85, 0, ItemKind.Smg);
            Post(Z, 100, -40, 270, ItemKind.Shotgun);
            Watchtower(Z, 25, -150, 6f, 45, new Color(0.5f, 0.45f, 0.4f));
            K.Alarm(K.At(88, -150), 0);
            BaseActivities(Z, new Vector3(55, 0, -120), 10f);
            K.Zone(Z, K.Map.zoneNames[1], new Vector3(75, 0, -95), new Vector3(150, 60, 140), K.At(160, -160, 0.1f), new Color(0.35f, 0.3f, 0.3f), HatStyle.Beanie, B.Local(0, 0.1f, -3));
        }

        // ---------------------------------------------------------------- Hilltop Mansion (zone 2)

        private void Mansion()
        {
            const int Z = 2;
            Vector2 c = new Vector2(35, 105);
            const float W = 40, D = 26, H = 3.6f;
            K.Paint(R(-30, 50, 120, 165), new Color(0.4f, 0.7f, 0.38f));

            // Garden wall with a gate (south) and gaps; the gate road from the drive.
            Color lWall = new Color(0.85f, 0.82f, 0.75f);
            K.Fence(new Vector3(-25, HILL, 55), new Vector3(28, HILL, 55), 2.6f, lWall, true);
            K.Fence(new Vector3(42, HILL, 55), new Vector3(115, HILL, 55), 2.6f, lWall, true);
            K.Fence(new Vector3(-25, HILL, 55), new Vector3(-25, HILL, 160), 2.6f, lWall, true);
            K.Fence(new Vector3(115, HILL, 55), new Vector3(115, HILL, 160), 2.6f, lWall, true);
            K.Fence(new Vector3(-25, HILL, 160), new Vector3(115, HILL, 160), 2.6f, lWall, true);
            K.Climbable(K.Env, new Vector3(80, HILL + 1.3f, 54.7f), new Vector3(1.6f, 2.6f, 0.3f), Quaternion.Euler(0, 180, 0), new Color(0.35f, 0.55f, 0.3f), false);
            K.Paint(R(-45, 20, -35, 60), ROAD);
            K.Paint(R(-35, 50, 40, 60), new Color(0.85f, 0.8f, 0.7f));
            Post(Z, 30, 52, 180, ItemKind.Smg);
            Post(Z, 40, 52, 180, ItemKind.Shotgun);
            Clear.Add(R(-30, 50, 120, 165));

            // Basement under the mansion: the plateau has a hole, a service ramp goes down at the east.
            K.Hole(R(c.x - W * 0.5f, c.y - D * 0.5f, c.x + W * 0.5f, c.y + D * 0.5f));
            K.Height(R(c.x + W * 0.5f, c.y - 4, c.x + W * 0.5f + 14, c.y + 4), HILL - H, new Color(0.5f, 0.5f, 0.52f));
            K.Ramp(K.Env, new Vector3(c.x + W * 0.5f + 26, HILL, c.y), new Vector3(c.x + W * 0.5f + 14, HILL - H, c.y), 7f, ROAD);

            MapKit.BuildingSpec lSpec = new MapKit.BuildingSpec
            {
                name = "Whitlock Mansion", origin = new Vector3(c.x, HILL, c.y), size = new Vector2(W, D), floors = 3, floorHeight = H,
                wall = new Color(0.96f, 0.94f, 0.9f), trim = new Color(0.35f, 0.45f, 0.6f), floor = new Color(0.62f, 0.45f, 0.35f), roof = new Color(0.3f, 0.32f, 0.38f),
                basement = true, pitchedRoof = false, windowSpacing = 4f, climbSide = 2, climbAt = 0.15f
            };
            lSpec.openings.Add(new MapKit.Opening(0, 0.5f, 0, 3f, 0, 3f));
            lSpec.openings.Add(new MapKit.Opening(2, 0.5f, 0, 3f, 0, 3f));
            lSpec.openings.Add(new MapKit.Opening(1, 0.5f, -1, 3f, 0, 2.8f));
            lSpec.openings.Add(new MapKit.Opening(3, 0.7f));
            lSpec.openings.Add(new MapKit.Opening(0, 0.2f, 1, 2f, 0, 2.6f));
            MapKit.BuildingResult M = K.Building(lSpec);

            // Ground floor: ballroom (chandelier), dining, bar, toilets.
            K.Partition(M, 0, new Vector3(-8, 0, -D * 0.5f), new Vector3(-8, 0, D * 0.5f), 0.5f, lSpec.wall * 0.95f);
            K.Partition(M, 0, new Vector3(10, 0, -D * 0.5f), new Vector3(10, 0, D * 0.5f), 0.4f, lSpec.wall * 0.95f);
            K.Chandelier(M.Local(1, H - 0.1f, -2), 3.4f);
            K.Activity(ActivityType.Speech, M.Local(1, 0.1f, 6), 180, Z, 18f);
            K.BarTable(M.Local(-14, 0, -6), 0, Z, 3);
            K.BarTable(M.Local(-14, 0, 4), 180, Z, 3);
            K.Activity(ActivityType.Dining, M.Local(-15, 0.1f, 0), 90, Z);
            K.BarTable(M.Local(14, 0, 6), 180, Z, 3);
            K.Toilet(M.Local(17, 0, -10), 0, Z);
            K.Toilet(M.Local(19, 0, -10), 0, Z);
            K.Distraction(M.Local(14, 0, -11.5f), 0, "radio");
            K.Npc(NpcRole.Guard, M.Local(-3, 0.1f, -10), 0, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, M.Local(6, 0.1f, 10), 180, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, M.Local(15, 0.1f, 0), 270, Z, ItemKind.Pistol);

            // First floor: study, bedrooms; second floor: suites, the attic stash.
            K.Partition(M, 1, new Vector3(-6, 0, -D * 0.5f), new Vector3(-6, 0, D * 0.5f), 0.5f, lSpec.wall);
            K.Partition(M, 1, new Vector3(6, 0, -D * 0.5f), new Vector3(6, 0, D * 0.5f), 0.5f, lSpec.wall);
            K.Activities(M.Local(12, H + 0.1f, 4), 0, Z, ActivityType.Office, ActivityType.Phone);
            K.Distraction(M.Local(16, H, -10), 0, "phone");
            K.HidingSpot(M.Local(-17, H, 11), 180, "closet");
            K.Npc(NpcRole.Guard, M.Local(0, H + 0.1f, 6), 180, Z, ItemKind.Smg);
            K.BarTable(M.Local(-12, 2 * H, 0), 90, Z, 2);
            K.Toilet(M.Local(-17, 2 * H, -10), 0, Z);
            K.HidingSpot(M.Local(16, 2 * H, 11), 180, "closet");
            K.Npc(NpcRole.Guard, M.Local(4, 2 * H + 0.1f, -6), 0, Z, ItemKind.Shotgun);
            K.Weapon(ItemKind.Smg, M.Local(17, 2 * H + 0.1f, -10));
            K.Npc(NpcRole.Lookout, M.Local(0, 3 * H + 0.2f, -10), 180, Z, ItemKind.Sniper);

            // Basement: utility room, air handler, vault.
            BuildingVolume lVolume = K.BuildingVolume("Whitlock Mansion", M.Local(0, 0, 0), new Vector3(W, H * 4.2f, D), 0);
            K.Breaker(M.Local(-18, -H, -12.6f), 180, lVolume);
            K.Vent(M.Local(-12, -H, 8), 0, lVolume, new[]
            {
                K.VentGrill(M.Local(-14, H - 0.15f, 0)), K.VentGrill(M.Local(1, H - 0.15f, 8)), K.VentGrill(M.Local(15, H - 0.15f, -4)),
                K.VentGrill(M.Local(-12, 2 * H - 0.15f, 0)), K.VentGrill(M.Local(12, 2 * H - 0.15f, 0)),
                K.VentGrill(M.Local(-12, 3 * H - 0.15f, 0)), K.VentGrill(M.Local(12, 3 * H - 0.15f, 0))
            });
            K.Partition(M, -1, new Vector3(6, 0, -D * 0.5f), new Vector3(6, 0, 0), 0.6f, lSpec.trim);
            K.Partition(M, -1, new Vector3(6, 0, 0), new Vector3(W * 0.5f, 0, 0), 0.4f, lSpec.trim);
            K.UniqueWeapon(ItemKind.RocketLauncher, M.Local(16, -H + 0.1f, -8), "Mansion vault");
            K.Gadget(ItemKind.Poison, M.Local(10, -H + 0.1f, -10));
            K.Npc(NpcRole.Guard, M.Local(0, -H + 0.1f, 0), 90, Z, ItemKind.Smg);
            K.Lamp(M.Local(0, -0.4f, 0), new Color(1f, 0.9f, 0.7f), 14f, 1.6f, false);
            K.Lamp(M.Local(-14, H - 0.4f, 0), new Color(1f, 0.85f, 0.6f), 12f, 1.5f, false);
            K.Lamp(M.Local(12, 2 * H - 0.4f, 0), new Color(1f, 0.85f, 0.6f), 12f, 1.5f, false);
            K.Lamp(M.Local(0, 3 * H - 0.4f, 0), new Color(1f, 0.85f, 0.6f), 12f, 1.5f, false);
            K.Sprinklers(M.Local(0, 0, 0), new Vector3(W, H * 3, D), 0, H - 0.1f);

            // Gardens: hedge maze, pool and pool house, greenhouse.
            float lMazeX = -10, lMazeZ = 140;
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 4; j++)
                {
                    if (K.Rng.NextDouble() < 0.35) continue;
                    bool lHorizontal = K.Rng.NextDouble() < 0.5;
                    K.Block(K.Env, "Hedge", new Vector3(lMazeX + i * 5, HILL + 1.1f, lMazeZ + j * 5), lHorizontal ? new Vector3(5.2f, 2.2f, 0.9f) : new Vector3(0.9f, 2.2f, 5.2f), HEDGE, Core.GameLayers.DEFAULT, true, null, true);
                }
            K.Activity(ActivityType.Garden, new Vector3(lMazeX + 12, HILL + 0.1f, lMazeZ + 8), 0, Z);
            K.Npc(NpcRole.Guard, new Vector3(lMazeX + 6, HILL + 0.1f, lMazeZ - 4), 0, Z, ItemKind.Shotgun);
            K.Block(K.Env, "Pool", new Vector3(85, HILL + 0.05f, 100), new Vector3(16, 0.1f, 9), new Color(0.3f, 0.75f, 0.95f));
            MapKit.BuildingResult lPoolHouse = House("Pool House", 85, 125, new Vector2(10, 7), 180, 1, Color.white, new Color(0.3f, 0.32f, 0.38f), Z, 2, true, ItemKind.Pistol);
            K.BarTable(lPoolHouse.Local(-2, 0, 1), 0, Z, 3);
            K.Toilet(lPoolHouse.Local(3.5f, 0, 2), 180, Z);
            K.Activities(new Vector3(85, HILL + 0.1f, 90), 0, Z, ActivityType.Workout, ActivityType.Smoke);
            MapKit.BuildingSpec lGreen = new MapKit.BuildingSpec { name = "Greenhouse", origin = new Vector3(95, HILL, 145), size = new Vector2(10, 8), floors = 1, wall = new Color(0.8f, 0.95f, 0.9f), trim = Color.white, roof = new Color(0.85f, 0.95f, 0.95f), windowSpacing = 2f, stairs = false, roofAccess = false };
            lGreen.openings.Add(new MapKit.Opening(0, 0.5f));
            MapKit.BuildingResult lGreenhouse = K.Building(lGreen);
            K.Activity(ActivityType.Garden, lGreenhouse.Local(0, 0.1f, 1), 0, Z);
            K.Gadget(ItemKind.EmeticPoison, lGreenhouse.Local(3, 0.1f, 2));
            for (int i = 0; i < 8; i++) K.Tree(new Vector3(-20 + i * 8, HILL, 60), MapKit.TreeKind.Round, 0.8f);

            K.Patrol(Z, ItemKind.Smg, 3f, new Vector3(-20, HILL + 0.1f, 62), new Vector3(110, HILL + 0.1f, 62), new Vector3(110, HILL + 0.1f, 155), new Vector3(-20, HILL + 0.1f, 155));
            K.Patrol(Z, ItemKind.Pistol, 3f, M.Local(-18, 0.1f, -12), M.Local(18, 0.1f, -12), M.Local(18, 0.1f, 12), M.Local(-18, 0.1f, 12));
            K.Patrol(Z, ItemKind.Shotgun, 4f, new Vector3(70, HILL + 0.1f, 85), new Vector3(100, HILL + 0.1f, 85), new Vector3(100, HILL + 0.1f, 135));
            K.Block(K.Env, "Helipad", new Vector3(60, HILL + 0.05f, 150), new Vector3(10, 0.1f, 10), new Color(0.35f, 0.35f, 0.4f));
            K.Point(MatchPoint.Kind.Extraction, new Vector3(60, HILL + 0.1f, 150), 0, "Mansion lawn helipad");
            BaseActivities(Z, new Vector3(35, HILL, 80), 7f);
            K.Zone(Z, K.Map.zoneNames[2], new Vector3(45, HILL, 105), new Vector3(150, 60, 115), new Vector3(-40, 0.1f, 30), new Color(0.2f, 0.22f, 0.3f), HatStyle.TopHat, M.Local(12, H + 0.1f, 4));
        }

        // ---------------------------------------------------------------- cul-de-sacs

        private void Neighbourhood()
        {
            Color[] lWalls = { new Color(0.95f, 0.9f, 0.8f), new Color(0.75f, 0.85f, 0.95f), new Color(0.95f, 0.8f, 0.75f), new Color(0.85f, 0.95f, 0.8f), new Color(1f, 0.95f, 0.7f) };
            Color[] lRoofs = { new Color(0.55f, 0.3f, 0.3f), new Color(0.3f, 0.35f, 0.45f), new Color(0.4f, 0.3f, 0.25f) };
            int lIndex = 0;
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 7; col++)
                {
                    float x = -150 + col * 20f, z = row == 0 ? 0f : 22f;
                    if (!IsClear(x, z)) continue;
                    float lYaw = row == 0 ? 180 : 0;
                    House("House" + lIndex, x, z + (row == 0 ? 0 : 0), new Vector2(10, 9), lYaw, 2, lWalls[lIndex % lWalls.Length], lRoofs[lIndex % lRoofs.Length], 0, row == 0 ? 2 : 0, lIndex % 3 == 0, lIndex % 2 == 0 ? ItemKind.Pistol : ItemKind.Coin, true);
                    // Backyard fence, shed or pool.
                    float lBack = row == 0 ? 10f : 34f;
                    K.Fence(new Vector3(x - 9, 0, lBack), new Vector3(x + 9, 0, lBack), 1.4f, new Color(0.95f, 0.95f, 0.92f));
                    if (lIndex % 3 == 1) K.HidingSpot(new Vector3(x + 5, 0, row == 0 ? 8 : 30), 0, "closet");
                    if (lIndex % 4 == 2) K.Block(K.Env, "BackyardPool", new Vector3(x - 3, 0.05f, row == 0 ? 7 : 31), new Vector3(6, 0.1f, 3.5f), new Color(0.3f, 0.75f, 0.95f));
                    if (lIndex % 5 == 0) K.Distraction(new Vector3(x - 6, 0, row == 0 ? 7 : 31), 0, "radio");
                    K.Tree(new Vector3(x + 7, 0, row == 0 ? -7 : 15), MapKit.TreeKind.Round, 0.9f + (float)K.Rng.NextDouble() * 0.3f);
                    lIndex++;
                }
            K.JumpPad(new Vector3(20, 0, 30), 0, new Vector3(0, 18, 10));
            K.JumpPad(new Vector3(-55, 0, 30), 0, new Vector3(0, 16, 8));
            K.Patrol(0, ItemKind.Smg, 4f, new Vector3(-150, 0.1f, 11), new Vector3(-30, 0.1f, 11), new Vector3(-30, 0.1f, -8));
            Forest(R(-170, 40, -65, 170), 50, MapKit.TreeKind.Pine, 1f, 1.5f, new Color(0.28f, 0.5f, 0.35f));
            Forest(R(115, 40, 170, 170), 30, MapKit.TreeKind.Round, 0.9f, 1.3f, new Color(0.95f, 0.6f, 0.3f));
            Forest(R(120, -60, 170, 30), 30, MapKit.TreeKind.Round, 0.9f, 1.3f);
            Rocks(R(-170, -170, 170, 170), 25, new Color(0.65f, 0.62f, 0.6f));
        }

        private void Points()
        {
            K.Point(MatchPoint.Kind.Start, K.At(-160, -160, 0.2f), 45, "Highway exit");
            K.Point(MatchPoint.Kind.Start, K.At(-160, 30, 0.2f), 90, "Cul-de-sac");
            K.Point(MatchPoint.Kind.Start, K.At(160, -10, 0.2f), 270, "Canal park");
            K.Point(MatchPoint.Kind.Start, K.At(0, -150, 0.2f), 0, "South avenue");
            K.Point(MatchPoint.Kind.Extraction, K.At(-165, -150, 0.1f), 45, "Highway on-ramp");
            K.Point(MatchPoint.Kind.Extraction, K.At(-150, -16, 0.1f), 90, "Bus stop");
            K.Point(MatchPoint.Kind.Extraction, new Vector3(105, -2.4f, 20), 0, "Canal culvert");

            float[,] lSpots = { { -120, -16 }, { -40, 0 }, { 0, -40 }, { 60, 0 }, { 140, -60 }, { 150, 0 }, { -150, 120 }, { 140, 120 }, { -90, 100 }, { 0, -120 }, { 60, -160 } };
            ItemKind[] lKinds = { ItemKind.Smg, ItemKind.Shotgun, ItemKind.Smg, ItemKind.Sniper, ItemKind.Smg, ItemKind.Shotgun, ItemKind.Sniper, ItemKind.Sniper, ItemKind.Smg, ItemKind.Pistol, ItemKind.Shotgun };
            for (int i = 0; i < lSpots.GetLength(0); i++) K.Weapon(lKinds[i], K.At(lSpots[i, 0], lSpots[i, 1], 0.1f));
            K.Gadget(ItemKind.SmokeGrenade, K.At(-20, -40, 0.1f));
            K.Gadget(ItemKind.Brick, K.At(-100, 10, 0.1f));
            K.Gadget(ItemKind.C4, K.At(120, 0, 0.1f));
            K.Gadget(ItemKind.Coin, K.At(-60, 15, 0.1f));
        }
    }
}
