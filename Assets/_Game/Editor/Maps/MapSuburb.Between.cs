using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Between the three zones: the residential streets (Maple Street, Willow Road, Oak Lane,
    /// Cedar Street) with their houses and yards, Juniper Park around the pond, Elmridge High and
    /// its field, the grange hall, the Briarwood country club on the golf course, the creek and its
    /// bridges, and the Ridgeway expressway on its embankment along the east edge (the rest stop is
    /// the car extraction). Staged: a garage sale, backyard barbecues, the band at the park
    /// pavilion, the town meeting in the grange hall, the gym class, the club's terrace toast.
    /// NPCs belong to the nearest zone: the west and the park to the downtown, the east and the
    /// expressway to the junkyard, Oak Lane's north side and the club to the mansion.
    /// </summary>
    public partial class MapSuburb
    {
        private const float CLUB_X = 150f, CLUB_Z = 238f, HWY_X = 470f, REST_Z = 340f;
        private static readonly Color SAND = new Color(0.9f, 0.84f, 0.64f);
        private static readonly Color GREEN = new Color(0.36f, 0.72f, 0.3f);
        private static readonly Color PICKET = new Color(0.95f, 0.95f, 0.92f);

        private struct HouseLot
        {
            public string name;
            public float x, z;
            public bool north;
            public int zone;
        }

        private readonly List<HouseLot> _houses = new List<HouseLot>();

        private void PlanBetween()
        {
            // Streets.
            Road(10f, ASPHALT, new Vector2(ASH_X, MAPLE_Z), new Vector2(BIRCH_X, MAPLE_Z), new Vector2(CEDAR_X, MAPLE_Z), new Vector2(CANAL_X, MAPLE_Z), new Vector2(420, MAPLE_Z));
            Road(9f, ASPHALT, new Vector2(ASH_X, WILLOW_Z), new Vector2(BIRCH_X, WILLOW_Z), new Vector2(-10, WILLOW_Z));
            Road(10f, ASPHALT, new Vector2(ASH_X, OAK_Z), new Vector2(BIRCH_X, OAK_Z), new Vector2(CEDAR_X, OAK_Z), new Vector2(420, OAK_Z));
            Road(10f, ASPHALT, new Vector2(CEDAR_X, MAIN_Z), new Vector2(CEDAR_X, MAPLE_Z), new Vector2(CEDAR_X, WILLOW_Z), new Vector2(CEDAR_X, OAK_Z));
            Lane(6f, ASPHALT, new Vector2(CLUB_X, OAK_Z), new Vector2(CLUB_X, 222f));

            // The expressway: an embankment along the east edge, two ramps down to Main and Maple.
            _zoneStage.Add(() => Flatten(R(452, -500, 490, 500), HWY, 12f));
            Road(30f, ASPHALT, new Vector2(HWY_X, -500), new Vector2(HWY_X, 500));
            Road(9f, ASPHALT, new Vector2(420, MAIN_Z), new Vector2(444, MAIN_Z + 12f), new Vector2(454, MAIN_Z + 46f));
            Road(9f, ASPHALT, new Vector2(420, MAPLE_Z), new Vector2(444, MAPLE_Z + 12f), new Vector2(454, MAPLE_Z + 46f));
            PaintRect(R(452, REST_Z - 30f, 458, REST_Z + 30f), ASPHALT, ROAD);

            // Lots: the high school and its field, the grange hall, the club and its car park, the park.
            PaintRect(R(-215, -6, -95, 36), GREEN);
            PaintRect(R(122, 172, 178, 222), ASPHALT);
            _tame.Add(R(-220, -12, -90, 40));
            _tame.Add(R(118, 168, 182, 262));
            _tame.Add(R(-4, 40, 118, 140));
            MapKit.KitSpec s = Lot("Elmridge High", "Suburb", -155f, -28f, 0, 12, 5, 2, Roof.Flat, new Color(0.82f, 0.56f, 0.42f), FLAT_ROOF, 0, float.NaN, 6f);
            s.brick = true;
            s.tallWindows = true;
            Doors(s, 0, 5, 0, 6, 2, 2, 2, 9, 3, 2);
            s.climb = new Vector2Int(1, 2);
            Floors(s, Room.Dining, Room.Gym, Room.Office, Room.Storage);
            MapKit.KitSpec h = Lot("Grange Hall", "Classic", -112f, 100f, 270, 6, 4, 1, Roof.Gable, new Color(0.94f, 0.9f, 0.82f), new Color(0.35f, 0.2f, 0.18f), 0, float.NaN, 5f);
            h.tallWindows = true;
            Doors(h, 0, 2, 0, 3, 2, 1);
            Floors(h, Room.Ballroom, Room.None);
            MapKit.KitSpec c = Lot("Briarwood Country Club", "Classic", CLUB_X, CLUB_Z, 0, 8, 5, 2, Roof.Hip, new Color(0.97f, 0.95f, 0.9f), new Color(0.24f, 0.3f, 0.28f), 2, float.NaN, 6f);
            c.tallWindows = true;
            c.balconyChance = 0.4f;
            Doors(c, 0, 3, 0, 4, 2, 2, 2, 5, 1, 2);
            c.climb = new Vector2Int(3, 3);
            Floors(c, Room.Bar, Room.Dining, Room.Lounge, Room.Office);
            MapKit.KitSpec p = Lot("Juniper Pavilion", "Timber", 10f, 120f, 0, 4, 3, 1, Roof.Hip, new Color(0.7f, 0.5f, 0.34f), new Color(0.3f, 0.24f, 0.2f), 0, float.NaN, 4f);
            for (int m = 0; m < 4; m++) { p.arches.Add(new Vector2Int(0, m)); p.arches.Add(new Vector2Int(2, m)); }
            p.arches.Add(new Vector2Int(1, 1));
            p.arches.Add(new Vector2Int(3, 1));
            p.stairs = false;
            p.roofAccess = false;

            // Houses: Maple Street both sides, Willow Road's north side, Oak Lane both sides.
            foreach (float x in new[] { -50f, -16f, 18f, 52f, 86f, 120f, 212f, 246f })
            {
                House("Maple N" + x, x, MAPLE_Z + 21f, true, x < CEDAR_X ? 0 : 1);
                House("Maple S" + x, x, MAPLE_Z - 22f, false, x < CEDAR_X ? 0 : 1);
            }
            foreach (float x in new[] { -205f, -171f, -137f }) House("Willow N" + x, x, WILLOW_Z + 22f, true, 0);
            foreach (float x in new[] { -205f, -171f, -137f, -50f, -16f, 100f, 134f, 205f, 240f, 275f, 330f }) House("Oak S" + x, x, OAK_Z - 22f, false, x < CEDAR_X ? 0 : 1);
            foreach (float x in new[] { -56f, -22f, 96f, 205f, 240f, 275f, 330f }) House("Oak N" + x, x, OAK_Z + 22f, true, 2);
        }

        /// <summary>A family house: 3 to 5 modules wide, one or two floors, front door on the street.</summary>
        private void House(string pName, float pX, float pZ, bool pNorth, int pZone)
        {
            System.Random lRng = new System.Random(pName.GetHashCode());
            int w = 3 + lRng.Next(3), floors = 1 + (lRng.NextDouble() < 0.7 ? 1 : 0);
            MapKit.KitSpec lSpec = Lot(pName, lRng.NextDouble() < 0.2 ? "Timber" : "Suburb", pX, pZ, pNorth ? 0 : 180, w, 3, floors, lRng.NextDouble() < 0.6 ? Roof.Gable : Roof.Hip,
                HOUSE_WALLS[lRng.Next(HOUSE_WALLS.Length)], HOUSE_ROOFS[lRng.Next(HOUSE_ROOFS.Length)], pZone, float.NaN, 5f);
            lSpec.brick = lSpec.style == "Suburb" && lRng.NextDouble() < 0.25;
            lSpec.balconyChance = 0.15f;
            Doors(lSpec, 0, w / 2, 2, w - 1);
            if (lRng.NextDouble() < 0.5) lSpec.climb = new Vector2Int(1, 1);
            if (floors == 2) Floors(lSpec, Room.Lounge, Room.Kitchen, Room.Bedroom, Room.Bathroom);
            else Floors(lSpec, Room.Lounge, Room.Kitchen);
            _houses.Add(new HouseLot { name = pName, x = pX, z = pZ, north = pNorth, zone = pZone });
        }

        private void Between()
        {
            Houses();
            School();
            GrangeHall();
            Park();
            CountryClub();
            Bridges();
            Expressway();
            StreetDressing();

            // Patrols on the residential streets (their barrels come with them), and the second patrol car.
            Walk(0, ItemKind.Pistol, 3f, new Vector2(BIRCH_X + 8f, MAPLE_Z + 7f), new Vector2(135f, MAPLE_Z + 7f), new Vector2(135f, MAPLE_Z - 7f), new Vector2(BIRCH_X + 8f, MAPLE_Z - 7f));
            Walk(1, ItemKind.Smg, 4f, new Vector2(CEDAR_X + 7f, MAPLE_Z + 7f), new Vector2(270f, MAPLE_Z + 7f), new Vector2(270f, OAK_Z - 7f), new Vector2(CEDAR_X + 7f, OAK_Z - 7f));
            Walk(0, ItemKind.Pistol, 4f, new Vector2(ASH_X + 8f, WILLOW_Z + 7f), new Vector2(BIRCH_X - 7f, WILLOW_Z + 7f), new Vector2(BIRCH_X - 7f, OAK_Z - 7f), new Vector2(ASH_X + 8f, OAK_Z - 7f));
            Walk(2, ItemKind.Smg, 3f, new Vector2(-60f, OAK_Z + 7f), new Vector2(120f, OAK_Z + 7f), new Vector2(300f, OAK_Z + 7f));
            Cruiser(8f, 0.5f, new Color(0.9f, 0.9f, 0.88f), new Vector2(BIRCH_X, MAPLE_Z), new Vector2(CEDAR_X, MAPLE_Z), new Vector2(CEDAR_X, OAK_Z), new Vector2(BIRCH_X, OAK_Z));
        }

        // ---------------------------------------------------------------- houses and yards

        private void Houses()
        {
            int i = 0;
            foreach (HouseLot lLot in _houses)
            {
                MapKit.KitBuilding b = K.Building(Spec(lLot.name));
                float lSide = lLot.north ? -1f : 1f;
                float lStreetZ = lLot.z + lSide * (b.D * 0.5f + 7.5f);
                float lYaw = lLot.north ? 0f : 180f;
                // Front yard: a picket fence along the sidewalk with a gap at the path, the mailbox, a tree.
                float lFenceZ = lLot.z + lSide * (b.D * 0.5f + 5.2f);
                Row("Suburb", "Railing", G(lLot.x - 12f, lFenceZ), G(lLot.x - 2f, lFenceZ), 3f, 90f, PICKET);
                Row("Suburb", "Railing", G(lLot.x + 2f, lFenceZ), G(lLot.x + 12f, lFenceZ), 3f, 90f, PICKET);
                P("Mailbox", lLot.x + 2.6f, lFenceZ + lSide * 0.6f, lYaw + 180f, HOUSE_ROOFS[i % HOUSE_ROOFS.Length]);
                Shade(lLot.x - 8f, lLot.z + lSide * (b.D * 0.5f + 2.5f), 0.9f);
                // Driveway and car on one side.
                PaintDriveway(lLot.x + 9.5f, lLot.z, lStreetZ);
                if (i % 3 != 1) K.Car(G(lLot.x + 9.5f, lLot.z + lSide * (b.D * 0.5f + 1f)), lYaw, CAR_TINTS[i % CAR_TINTS.Length]);
                // Back yard: one staged scene in a few, plain lawn in the others.
                Vector3 lBack = G(lLot.x, lLot.z - lSide * (b.D * 0.5f + 6f));
                switch (i % 6)
                {
                    case 0:
                        // A barbecue: grill (gas bottle), table, folding chairs.
                        P("Table", lBack, lYaw);
                        GasBottle(lBack + new Vector3(2.4f, 0, 0));
                        K.Activity(ActivityType.Dining, lBack + new Vector3(0, 0.1f, -1.2f), lYaw, lLot.zone);
                        K.Activity(ActivityType.Drink, lBack + new Vector3(-1.5f, 0.1f, 0.8f), lYaw, lLot.zone);
                        break;
                    case 2:
                        // A kiddie pool and a parasol.
                        K.Block(K.Props, "KiddiePool", lBack + Vector3.up * 0.2f, new Vector3(3f, 0.4f, 3f), new Color(0.35f, 0.6f, 0.9f), GameLayers.DEFAULT, true);
                        Parasol(lBack + new Vector3(3f, 0, 0), 0f, AWNINGS[i % AWNINGS.Length], false);
                        break;
                    case 3:
                        // A shed (hiding) and a workbench clutter.
                        K.HidingKit(lBack + new Vector3(-3f, 0, 0), lYaw, "Wardrobe");
                        Clutter(lBack + new Vector3(2f, 0, 0), lYaw, i);
                        K.Activity(ActivityType.Garden, lBack + new Vector3(0, 0.1f, 2f), lYaw, lLot.zone);
                        break;
                    case 5:
                        Parasol(lBack, 0f, AWNINGS[(i + 2) % AWNINGS.Length], true);
                        K.Activity(ActivityType.Phone, lBack + new Vector3(1.5f, 0.1f, 0), lYaw, lLot.zone);
                        break;
                }
                // Inside: a weapon in some houses, a TV to switch on in others.
                if (i % 4 == 0) K.Weapon(i % 8 == 0 ? ItemKind.Shotgun : ItemKind.Pistol, b.Local(-2f, 0.2f, 1.5f));
                if (i % 5 == 1) K.DistractionKit(b.Local(1.5f, 0.05f, 2.6f), b.Yaw + 180f, "tv");
                if (i % 7 == 4) K.Gadget(i % 2 == 0 ? ItemKind.Poison : ItemKind.Coin, b.Local(0.5f, 0.95f, -1f));
                i++;
            }

            // The garage sale on Maple Street: tables of junk on the lawn, the neighbours browsing.
            Vector3 g = G(52f, MAPLE_Z + 12f);
            for (int t = 0; t < 3; t++)
            {
                P("Table", g + new Vector3(-4f + t * 3.4f, 0, 0), 0);
                K.Crate(g + new Vector3(-4f + t * 3.4f, 0.8f, 0), 0.5f);
            }
            P("Bookshelf", g + new Vector3(6f, 0, 0.5f), 0);
            K.Activity(ActivityType.Inspect, g + new Vector3(-2f, 0.1f, -1.4f), 0, 0);
            K.Activity(ActivityType.Inspect, g + new Vector3(2f, 0.1f, -1.4f), 0, 0);
            K.Weapon(ItemKind.Sniper, g + new Vector3(0, 0.95f, 0));
        }

        private void PaintDriveway(float pX, float pZ0, float pZ1) => PaintNow(R(pX - 2f, Mathf.Min(pZ0, pZ1), pX + 2f, Mathf.Max(pZ0, pZ1)), CONCRETE);

        /// <summary>Paint after the terrain was meshed is not possible: a thin slab instead.</summary>
        private void PaintNow(Rect pRect, Color pColor)
        {
            Vector3 c = G(pRect.center.x, pRect.center.y);
            K.Block(K.Env, "Drive", c + Vector3.up * 0.03f, new Vector3(pRect.width, 0.06f, pRect.height), pColor, GameLayers.DEFAULT, false);
        }

        // ---------------------------------------------------------------- the high school

        private void School()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Elmridge High"));
            // The gym class and the cafeteria rush.
            K.Activity(ActivityType.Workout, b.Local(8f, 0.1f, 0f), b.Yaw, Z);
            K.Activity(ActivityType.Workout, b.Local(11f, 0.1f, 2f), b.Yaw, Z);
            K.Activity(ActivityType.Dining, b.Local(-9f, 0.1f, 0f), b.Yaw, Z);
            K.Activity(ActivityType.Office, b.Local(-9f, MapKit.STOREY + 0.1f, 1f), b.Yaw, Z);
            K.ToiletKit(b.Local(-16f, MapKit.STOREY + 0.05f, 5.5f), b.Yaw + 180f, Z);
            K.DistractionKit(b.Local(-12f, MapKit.STOREY + 0.8f, -5.5f), b.Yaw, "radio");
            K.Weapon(ItemKind.Smg, b.Local(14f, MapKit.STOREY + 0.2f, 4f));
            K.Gadget(ItemKind.SmokeGrenade, b.Local(-14f, 0.2f, 5f));
            Guard(Z, b.Local(0f, 0f, -10f), b.Yaw + 180f, ItemKind.Pistol);
            K.Npc(NpcRole.Lookout, b.Local(10f, MapKit.STOREY * 2f + 0.1f, -5f), b.Yaw + 180f, Z, ItemKind.Sniper);

            // The field: running track lines, goals, bleachers, floodlights.
            Vector3 f = G(-155f, 15f);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(K.Env, "Line", f + new Vector3(0, 0.04f, s * 18f), new Vector3(116f, 0.04f, 0.3f), Color.white, GameLayers.DEFAULT, false);
                K.Block(K.Env, "Line", f + new Vector3(s * 58f, 0.04f, 0), new Vector3(0.3f, 0.04f, 36f), Color.white, GameLayers.DEFAULT, false);
                Vector3 lGoal = f + new Vector3(s * 55f, 0, 0);
                K.Block(K.Env, "GoalBar", lGoal + Vector3.up * 2.4f, new Vector3(0.2f, 0.2f, 7.3f), Color.white);
                for (int k = -1; k <= 1; k += 2) K.Block(K.Env, "GoalPost", lGoal + new Vector3(0, 1.2f, k * 3.6f), new Vector3(0.2f, 2.4f, 0.2f), Color.white);
                K.Lamp(f + new Vector3(s * 40f, 9f, 20f), new Color(1f, 0.95f, 0.85f), 30f, 2f, false);
                K.Block(K.Env, "FloodPole", f + new Vector3(s * 40f, 4.5f, 20f), new Vector3(0.4f, 9f, 0.4f), new Color(0.4f, 0.4f, 0.42f));
            }
            // Bleachers along the north side: three steps you can climb, cover underneath.
            for (int r = 0; r < 3; r++)
                K.Block(K.Env, "Bleacher", f + new Vector3(0, 0.3f + r * 0.6f, 21.5f + r * 1.1f), new Vector3(40f, 0.6f + r * 1.2f, 1.1f), new Color(0.55f, 0.58f, 0.62f), GameLayers.DEFAULT, true, null, true);
            K.Activity(ActivityType.Speech, f + new Vector3(0, 0.1f, 16f), 180f, Z);
            K.Activity(ActivityType.Lookout, f + new Vector3(-10f, 2.1f, 23.7f), 180f, Z);
            Walk(Z, ItemKind.Pistol, 5f, new Vector2(-210f, -2f), new Vector2(-100f, -2f), new Vector2(-100f, 34f), new Vector2(-210f, 34f));
        }

        // ---------------------------------------------------------------- the grange hall

        private void GrangeHall()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Grange Hall"));
            // The town meeting: rows of chairs facing a lectern, the chandelier over the aisle.
            for (int r = 0; r < 4; r++)
                for (int c = -3; c <= 3; c++)
                    if (c != 0) K.Prop("Chair", b.Local(c * 0.9f, 0f, -3f + r * 1.3f), b.Yaw + 180f, null, MapKit.Collide.None);
            K.Prop("Desk", b.Local(0, 0, -6.5f), b.Yaw, null, MapKit.Collide.Box);
            K.Activity(ActivityType.Speech, b.Local(0, 0.1f, -7.2f), b.Yaw + 180f, Z, 18f);
            K.ChandelierKit(b.Local(0, MapKit.STOREY - 0.1f, 1f), 2.2f);
            K.DrinksOn(b.Local(5.5f, 0.85f, 4f), b.Yaw, Z, 4);
            K.Prop("Table", b.Local(5.5f, 0, 4f), b.Yaw, null, MapKit.Collide.Box);
            K.ToiletKit(b.Local(-7.5f, 0.05f, 4.5f), b.Yaw, Z);
            // The bake sale outside.
            Vector3 o = b.Local(0, 0, -11f);
            P("Table", o, b.Yaw);
            P("Table", o + Quaternion.Euler(0, b.Yaw, 0) * new Vector3(2.2f, 0, 0), b.Yaw);
            Parasol(o + Quaternion.Euler(0, b.Yaw, 0) * new Vector3(-2.4f, 0, 0), 0, AWNINGS[1], false);
            K.Activity(ActivityType.Dining, o + Vector3.up * 0.1f + Quaternion.Euler(0, b.Yaw, 0) * new Vector3(0, 0, -1.2f), b.Yaw, Z);
            Guard(Z, b.Local(0, 0, -9f), b.Yaw + 180f, ItemKind.Pistol);
            K.Weapon(ItemKind.Shotgun, b.Local(-6f, 0.2f, -5f));
        }

        // ---------------------------------------------------------------- Juniper Park

        private void Park()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Juniper Pavilion"));
            // The band: a stage under the pavilion, the crowd's benches, string lights.
            Deck(b.Local(0, 0.45f, 2f), new Vector2(9f, 3.5f), b.Yaw);
            K.Activity(ActivityType.Speech, b.Local(0, 0.55f, 2f), b.Yaw + 180f, Z, 16f);
            for (int r = 0; r < 3; r++) K.Prop("Bench", b.Local(-3f + r * 3f, 0, -9f), b.Yaw + 180f, null, MapKit.Collide.Box);
            StringLights(b.Local(-6f, 3.2f, -5f), b.Local(6f, 3.2f, -5f));
            K.DistractionKit(b.Local(3.5f, 0.55f, 2.5f), b.Yaw + 180f, "radio");

            // Paths and benches around the pond, lamps, picnic tables, the ice cream van.
            for (int a = 0; a < 12; a++)
            {
                float lAngle = a * 30f * Mathf.Deg2Rad;
                Vector2 p = POND + new Vector2(Mathf.Cos(lAngle), Mathf.Sin(lAngle)) * (POND_R + 7f);
                if (Flag(p.x, p.y, WET | BANK)) continue;
                if (a % 3 == 0) K.Lamp(G(p), WARM, 12f, 1.3f, true);
                else if (a % 3 == 1) P("Bench", p.x, p.y, -a * 30f + 90f);
                else P("TrashBin", p.x, p.y, 0, null, false, MapKit.Collide.Box);
            }
            foreach (Vector2 t in new[] { new Vector2(-2f, 70f), new Vector2(98f, 98f), new Vector2(104f, 124f) })
            {
                P("Table", t.x, t.y, 20f);
                K.Activity(ActivityType.Dining, G(t.x, t.y - 1.3f, 0.1f), 0, Z);
            }
            K.Prop("Van", G(-4f, WILLOW_Z + 7f), 90f, new Color(0.98f, 0.72f, 0.8f), MapKit.Collide.Box, true);
            K.Activity(ActivityType.Drink, G(-4f, WILLOW_Z + 10.5f, 0.1f), 180f, Z);
            K.Activity(ActivityType.Garden, G(30f, 60f, 0.1f), 45f, Z);
            K.Activity(ActivityType.Smoke, G(110f, 132f, 0.1f), 0f, Z);
            K.ToiletKit(G(-1f, 104f), 90f, Z);
            // The playground: a frame, a slide, a sandbox.
            Vector3 pg = G(84f, 112f);
            K.Block(K.Env, "Sandbox", pg + Vector3.up * 0.1f, new Vector3(6f, 0.2f, 6f), SAND, GameLayers.DEFAULT, false);
            for (int k = -1; k <= 1; k += 2) K.Block(K.Env, "SwingPost", pg + new Vector3(k * 2.5f, 1.3f, 5f), new Vector3(0.15f, 2.6f, 0.15f), new Color(0.85f, 0.3f, 0.2f));
            K.Block(K.Env, "SwingBar", pg + new Vector3(0, 2.6f, 5f), new Vector3(5.2f, 0.15f, 0.15f), new Color(0.85f, 0.3f, 0.2f));
            K.Block(K.Env, "Slide", pg + new Vector3(-5f, 0.9f, 0), new Vector3(0.8f, 0.15f, 4f), new Color(0.95f, 0.75f, 0.2f), GameLayers.DEFAULT, true, Quaternion.Euler(-25f, 0, 0));
            // A wild corner of tall grass by the creek, with a stash.
            K.Grass(G(105f, 70f), new Vector2(18f, 14f), new Color(0.62f, 0.66f, 0.36f));
            K.Weapon(ItemKind.Smg, G(106f, 70f, 0.15f));
            K.HidingKit(G(116f, 52f), 0, "Dumpster");
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(0f, 60f), new Vector2(26f, 112f), new Vector2(80f, 120f), new Vector2(100f, 84f));
            K.Npc(NpcRole.Guard, b.Local(5f, 0.1f, -3f), b.Yaw + 180f, Z, ItemKind.Smg);
        }

        // ---------------------------------------------------------------- the country club and the golf course

        private void CountryClub()
        {
            const int Z = 2;
            MapKit.KitBuilding b = K.Building(Spec("Briarwood Country Club"));
            // The bar and the members' dining room; the office upstairs; the terrace toast on the golf side.
            K.DrinksOn(b.Local(-6f, 1.1f, 2f), b.Yaw, Z, 5);
            K.Activity(ActivityType.Drink, b.Local(-6f, 0.1f, 0f), b.Yaw, Z);
            K.Activity(ActivityType.Dining, b.Local(6f, 0.1f, 1f), b.Yaw, Z);
            K.Activity(ActivityType.Office, b.Local(6f, MapKit.STOREY + 0.1f, 2f), b.Yaw, Z);
            K.Activity(ActivityType.Phone, b.Local(-6f, MapKit.STOREY + 0.1f, -2f), b.Yaw, Z);
            K.ChandelierKit(b.Local(6f, MapKit.STOREY - 0.1f, 1f), 2.2f);
            K.ToiletKit(b.Local(10.5f, 0.05f, 6f), b.Yaw + 180f, Z);
            for (int f = 0; f < 2; f++) K.Sprinklers(b.Local(0, f * MapKit.STOREY, 0), new Vector3(b.W, MapKit.STOREY, b.D), b.Yaw, MapKit.STOREY - 0.05f);

            Vector3 t = b.Local(0, 0, b.D * 0.5f + 5f);
            Deck(t + Vector3.up * 0.35f, new Vector2(b.W, 9f), b.Yaw);
            for (int k = -2; k <= 2; k++)
                if (k != 0) Parasol(t + new Vector3(k * 4.5f, 0.35f, 0), 0, new Color(0.2f, 0.42f, 0.32f), true);
            StringLights(t + new Vector3(-11f, 3.4f, 4f), t + new Vector3(11f, 3.4f, 4f));
            K.Activity(ActivityType.Speech, t + new Vector3(0, 0.45f, 0), b.Yaw, Z, 16f);
            K.Activity(ActivityType.Drink, t + new Vector3(3f, 0.45f, 2f), b.Yaw, Z);
            K.Activity(ActivityType.Smoke, b.Local(b.W * 0.5f + 2.5f, 0.1f, -3f), b.Yaw + 90f, Z);
            GasBottle(b.Local(b.W * 0.5f + 1.5f, 0, -5f));
            K.HidingKit(b.Local(b.W * 0.5f + 2.5f, 0, -6.5f), b.Yaw + 270f, "Dumpster");
            K.Weapon(ItemKind.Sniper, b.Local(-10f, MapKit.STOREY * 2f + 0.2f, 4f));
            K.Weapon(ItemKind.Pistol, b.Local(8f, MapKit.STOREY + 0.2f, -4f));

            Guard(Z, b.Local(-3f, 0f, -b.D * 0.5f - 3f), b.Yaw + 180f, ItemKind.Smg);
            Guard(Z, b.Local(3f, 0f, -b.D * 0.5f - 3f), b.Yaw + 180f, ItemKind.Pistol);
            K.Npc(NpcRole.Lookout, b.Local(9f, MapKit.STOREY * 2f + 0.1f, 5f), b.Yaw, Z, ItemKind.Sniper);
            K.Npc(NpcRole.Guard, t + new Vector3(-8f, 0.45f, 3f), b.Yaw, Z, ItemKind.Smg);
            Walk(Z, ItemKind.Smg, 3f, new Vector2(CLUB_X - 16f, CLUB_Z - 12f), new Vector2(CLUB_X + 16f, CLUB_Z - 12f), new Vector2(CLUB_X + 16f, CLUB_Z + 20f), new Vector2(CLUB_X - 16f, CLUB_Z + 20f));

            // The car park.
            for (int k = 0; k < 7; k++) K.Car(G(126f + k * 7.5f, 196f), k % 2 == 0 ? 0f : 180f, CAR_TINTS[(k * 5 + 1) % CAR_TINTS.Length]);
            K.Distraction(G(141f, 196f, 0.9f), 0, "caralarm");

            // The golf course: greens with flags, sand bunkers, carts, a groundskeeper's shed.
            Vector2[] lGreens = { new Vector2(-40, 300), new Vector2(40, 360), new Vector2(150, 330), new Vector2(220, 420), new Vector2(110, 450), new Vector2(-70, 420) };
            foreach (Vector2 lGreen in lGreens)
            {
                if (Flag(lGreen.x, lGreen.y, WET | BANK)) continue;
                Vector3 lHole = G(lGreen);
                K.Block(K.Env, "Green", lHole + Vector3.up * 0.03f, new Vector3(16f, 0.06f, 14f), new Color(0.42f, 0.8f, 0.36f), GameLayers.DEFAULT, false, Quaternion.Euler(0, lGreen.x * 7f, 0));
                K.Block(K.Env, "FlagPole", lHole + Vector3.up * 1.2f, new Vector3(0.05f, 2.4f, 0.05f), Color.white, GameLayers.DEFAULT, false);
                K.Block(K.Env, "Flag", lHole + new Vector3(0.3f, 2.15f, 0), new Vector3(0.55f, 0.35f, 0.02f), new Color(0.9f, 0.15f, 0.15f), GameLayers.DEFAULT, false);
                K.Block(K.Env, "Bunker", G(lGreen.x + 11f, lGreen.y - 6f) + Vector3.up * 0.02f, new Vector3(7f, 0.05f, 4f), SAND, GameLayers.DEFAULT, false, Quaternion.Euler(0, 25f, 0));
                K.Activity(ActivityType.Garden, lHole + new Vector3(2f, 0.1f, -2f), 0f, Z);
            }
            for (int k = 0; k < 4; k++) K.Prop("Jeep", G(CLUB_X - 20f + k * 4f, CLUB_Z + 18f), 0, new Color(0.95f, 0.95f, 0.9f), MapKit.Collide.Box, true);
            K.HidingKit(G(-20f, 330f), 90f, "Wardrobe");
            K.Weapon(ItemKind.Shotgun, G(-18f, 333f, 0.15f));
            Walk(Z, ItemKind.Pistol, 6f, new Vector2(-40f, 290f), new Vector2(40f, 350f), new Vector2(150f, 320f), new Vector2(210f, 410f));
            K.Npc(NpcRole.Guard, G(110f, 446f), 180f, Z, ItemKind.Sniper);
        }

        // ---------------------------------------------------------------- creek bridges

        private void Bridges()
        {
            // Cedar Street and Oak Lane over the creek (road decks), two park footbridges.
            CreekSpan(new Vector2(CEDAR_X, 32f), new Vector2(CEDAR_X, 60f), 12f, true);
            CreekSpan(new Vector2(26f, OAK_Z), new Vector2(53f, OAK_Z), 12f, true);
            CreekSpan(new Vector2(86f, 48f), new Vector2(94f, 75f), 2.6f, false);
            CreekSpan(new Vector2(30f, 128f), new Vector2(58f, 128f), 2.6f, false);
            // Under the Cedar bridge: a dry bank, a crate, a smoker.
            K.Crate(G(CEDAR_X + 6f, 40f));
            K.Activity(ActivityType.Smoke, G(CEDAR_X - 6f, 41f, 0.1f), 90f, 1);
        }

        private void CreekSpan(Vector2 pA, Vector2 pB, float pWidth, bool pRoad)
        {
            Vector3 a = G(pA, 0.05f), b = G(pB, 0.05f);
            Span(a, b, pWidth, pRoad, 3f, pRoad ? "Classic" : "Timber");
        }

        // ---------------------------------------------------------------- the expressway

        private void Expressway()
        {
            const int Z = 1;
            Transform lHwy = K.Group("Expressway", K.Env);
            // Guard rails on both edges and a median barrier, dashed lane lines, lamps.
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(lHwy, "GuardRail", new Vector3(HWY_X + s * 15.5f, HWY + 0.55f, 0), new Vector3(0.3f, 0.5f, 1000f), new Color(0.72f, 0.72f, 0.74f), GameLayers.DEFAULT, true);
                for (float z = -490f; z < 490f; z += 12f) K.Block(lHwy, "Dash", new Vector3(HWY_X + s * 7f, HWY + 0.03f, z), new Vector3(0.18f, 0.03f, 4f), new Color(0.95f, 0.95f, 0.9f), GameLayers.DEFAULT, false);
            }
            K.Block(lHwy, "Median", new Vector3(HWY_X, HWY + 0.45f, 0), new Vector3(0.6f, 0.9f, 1000f), CONCRETE, GameLayers.DEFAULT, true, null, true);
            for (float z = -460f; z < 480f; z += 80f) K.Lamp(new Vector3(HWY_X + 14.5f, HWY, z), SODIUM, 18f, 1.6f, true);
            // Breakdowns on the shoulder, a highway patrol car.
            K.Car(new Vector3(HWY_X - 12f, HWY, -150f), 0, CAR_TINTS[3]);
            K.Car(new Vector3(HWY_X + 12f, HWY, 120f), 180, CAR_TINTS[6]);
            K.Prop("TrafficCone", new Vector3(HWY_X - 12f, HWY, -144f), 0);
            K.Prop("PoliceCar", new Vector3(HWY_X - 12f, HWY, 60f), 0, null, MapKit.Collide.Box, true);

            // The rest stop: a lay-by, picnic tables, a vending kiosk, the pickup van (car extraction).
            Vector3 r = new Vector3(HWY_X - 13f, HWY, REST_Z);
            K.Block(lHwy, "Kiosk", r + new Vector3(-6f, 1.4f, 14f), new Vector3(3f, 2.8f, 2.4f), new Color(0.85f, 0.82f, 0.75f), GameLayers.DEFAULT, true, null, true);
            Neon(lHwy, r + new Vector3(-4.45f, 2.2f, 14f), new Vector3(0.05f, 0.6f, 1.6f), new Color(0.5f, 1.6f, 2.6f));
            P("Table", r + new Vector3(-7f, 0, 4f), 0);
            P("Table", r + new Vector3(-7f, 0, -4f), 0);
            K.Prop("Van", r + new Vector3(1f, 0, 0), 0, new Color(0.25f, 0.27f, 0.3f), MapKit.Collide.Box, true);
            K.Point(MatchPoint.Kind.Extraction, r + new Vector3(-3f, 0.1f, -6f), 180, "Ridgeway rest stop van");
            K.Activity(ActivityType.Phone, r + new Vector3(-6f, 0.1f, 11f), 0, Z);
            K.Activity(ActivityType.Toilet, r + new Vector3(-8f, 0.1f, 14f), 90, Z);
            Guard(Z, r + new Vector3(2f, 0, -8f), 180f, ItemKind.Smg);
            Guard(Z, r + new Vector3(-4f, 0, 18f), 0f, ItemKind.Shotgun);
            K.Weapon(ItemKind.Smg, r + new Vector3(-7f, 0.95f, 4f));
            Vector3[] lPatrol = { new Vector3(HWY_X - 12f, HWY + 0.1f, -120f), new Vector3(HWY_X - 12f, HWY + 0.1f, 60f), new Vector3(HWY_X - 12f, HWY + 0.1f, REST_Z - 20f) };
            K.Patrol(Z, ItemKind.Smg, 5f, lPatrol);
        }

        // ---------------------------------------------------------------- street dressing

        /// <summary>Lamps, hydrants, bins and signs along Maple, Oak and Cedar; power poles behind the houses.</summary>
        private void StreetDressing()
        {
            int k = 0;
            foreach ((Vector2 from, Vector2 to) in new[]
            {
                (new Vector2(BIRCH_X + 10f, MAPLE_Z), new Vector2(285f, MAPLE_Z)),
                (new Vector2(BIRCH_X + 10f, OAK_Z), new Vector2(400f, OAK_Z)),
                (new Vector2(ASH_X + 10f, OAK_Z), new Vector2(BIRCH_X - 10f, OAK_Z)),
                (new Vector2(ASH_X + 10f, WILLOW_Z), new Vector2(BIRCH_X - 10f, WILLOW_Z)),
            })
            {
                for (float x = from.x; x < to.x; x += 26f, k++)
                {
                    if (Mathf.Abs(x - CEDAR_X) < 10f || Mathf.Abs(x - 40f) < 16f) continue;
                    foreach (int s in new[] { -1, 1 })
                    {
                        float z = from.y + s * 6.5f;
                        if (Flag(x, z, WET | BANK)) continue;
                        if ((k + (s > 0 ? 1 : 0)) % 2 == 0) { P("StreetLamp", x, z, s > 0 ? 180f : 0f); K.Lamp(G(x, z, 4.3f), SODIUM, 15f, 1.4f, false); }
                        else if (k % 3 == 0) P("Hydrant", x, z, 0, new Color(0.85f, 0.15f, 0.12f));
                        else P("TrashBin", x, z, 0, null, false, MapKit.Collide.Box);
                    }
                }
            }
            for (float z = MAIN_Z + 15f; z < OAK_Z - 10f; z += 30f)
            {
                if (Mathf.Abs(z - MAPLE_Z) < 10f || Mathf.Abs(z - 46f) < 16f) continue;
                P("StreetLamp", CEDAR_X + 6.5f, z, 270f);
                K.Lamp(G(CEDAR_X + 6.5f, z, 4.3f), SODIUM, 15f, 1.4f, false);
            }
            foreach (Vector2 p in new[] { new Vector2(BIRCH_X + 7f, MAPLE_Z + 7f), new Vector2(CEDAR_X - 7f, MAPLE_Z - 7f), new Vector2(CEDAR_X + 7f, OAK_Z - 7f), new Vector2(BIRCH_X - 7f, OAK_Z + 7f), new Vector2(BIRCH_X + 7f, WILLOW_Z - 7f) })
                P("StreetSign", p.x, p.y, 45f);
            for (float x = -40f; x < 280f; x += 40f) P("PowerPole", x, MAPLE_Z + 36f, 90f);
            for (float x = 190f; x < 400f; x += 40f) P("PowerPole", x, OAK_Z - 36f, 90f);
        }
    }
}
