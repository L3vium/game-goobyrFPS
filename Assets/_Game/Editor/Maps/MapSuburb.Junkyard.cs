using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEditor;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Junkyard Bowl (zone 1): the bikers' lair at the east end of Main Street. Public: Main
    /// Street, the bowling lot, the canal quays. Restricted: the junkyard behind its fence, the
    /// club house, the tall-grass field, the farmhouse yard. VIP: the deal in the bowling alley's
    /// back room, the crane cab, the chop shop. Landmarks: the magnet crane and the giant pin on the
    /// bowling roof. Ways into the club house: the guarded front doors, the back door from the
    /// alley (the deal room), the side door by the lanes, the drainpipe and the roof hatch. Ways
    /// into the junkyard: the gate, the culvert under the lane, the tall grass and the rail
    /// wagons along the south fence, the crawl gap behind the crusher. The canal runs in a concrete
    /// channel: walkable shelves under the bridges, two crawl-through culverts, a storm drain tunnel
    /// into the south woods (the tunnel extraction).
    /// </summary>
    public partial class MapSuburb
    {
        private static readonly Color RUST = new Color(0.55f, 0.33f, 0.22f);
        private static readonly Color[] WRECKS =
        {
            new Color(0.55f, 0.33f, 0.22f), new Color(0.45f, 0.5f, 0.55f), new Color(0.7f, 0.6f, 0.3f), new Color(0.4f, 0.45f, 0.35f),
            new Color(0.62f, 0.28f, 0.25f), new Color(0.3f, 0.36f, 0.45f), new Color(0.72f, 0.7f, 0.65f)
        };
        private static readonly Color SCRAP = new Color(0.5f, 0.47f, 0.44f);
        /// <summary>The canal crossings (z) and their kind: bridges carry roads and the railway, culverts carry lanes over crawl pipes.</summary>
        private static readonly float[] CANAL_BRIDGES = { MAIN_Z, MAPLE_Z, RAIL_Z };
        private static readonly float[] CANAL_CULVERTS = { -300f, -142f };
        private const float CANAL_TUNNEL = -450f, CANAL_END = -476f;

        private void PlanJunkyard()
        {
            _zoneStage.Add(() => Flatten(R(120, -400, 440, -238), 0f, 20f));
            _zoneStage.Add(() => Flatten(R(276, -440, 324, CANAL_HEAD + 4f), 0f, 16f));
            Lane(6f, GRAVEL, new Vector2(236, -300), new Vector2(330, -300));
            Lane(5f, GRAVEL, new Vector2(150, MAIN_Z), new Vector2(148, -300), new Vector2(152, -318));
            Lane(4f, GRAVEL, new Vector2(CEDAR_X, -142), new Vector2(330, -142), new Vector2(380, -110));
            PaintRect(R(320, -362, 436, -244), new Color(0.5f, 0.45f, 0.36f));
            PaintRect(R(184, -262, 226, -238), ASPHALT);
            PaintRect(R(232, -292, 260, -262), CONCRETE);
            _tame.Add(R(116, -404, 444, -236));

            MapKit.KitSpec b = Lot("Lucky Ten Lanes", "Suburb", 205f, -276f, 180, 12, 8, 1, Roof.Flat, new Color(0.88f, 0.62f, 0.4f), FLAT_ROOF, 1, 0f, 3f);
            Doors(b, 0, 5, 0, 6, 2, 2, 3, 2);
            b.brick = true;
            b.windowChance = 0.3f;
            b.balconyChance = 0f;
            b.climb = new Vector2Int(1, 6);
            MapKit.KitSpec o = Lot("Junkyard Office", "Suburb", 345f, -256f, 180, 3, 3, 1, Roof.Flat, new Color(0.85f, 0.85f, 0.8f), FLAT_ROOF, 1, 0f, 3f);
            Doors(o, 0, 1);
            o.roofAccess = false;
            o.stairs = false;
            Floors(o, Room.Office, Room.None);
            MapKit.KitSpec c = Lot("Hart's Chop Shop", "Suburb", 412f, -260f, 180, 5, 4, 1, Roof.Flat, new Color(0.6f, 0.62f, 0.66f), FLAT_ROOF, 1, 0f, 3f);
            c.arches.Add(new Vector2Int(0, 1));
            c.arches.Add(new Vector2Int(0, 3));
            Doors(c, 2, 2, 3, 1);
            c.windowChance = 0.3f;
            MapKit.KitSpec g = Lot("Voss Farmhouse", "Timber", 152f, -330f, 0, 4, 3, 2, Roof.Gable, new Color(0.86f, 0.8f, 0.66f), new Color(0.45f, 0.22f, 0.18f), 1, 0f, 5f);
            Doors(g, 0, 1, 2, 2);
            g.balconyChance = 0.2f;
            Floors(g, Room.Dining, Room.Kitchen, Room.Bedroom, Room.Bathroom);
        }

        private void Junkyard()
        {
            const int Z = 1;
            Canal();
            Bowling();
            Yard();
            Crane();
            Crusher();
            Farmhouse();
            Field();

            K.Alarm(G(352, -248), 180);
            Walk(Z, ItemKind.Smg, 4f, new Vector2(325, -250), new Vector2(430, -250), new Vector2(430, -356), new Vector2(325, -356));
            Walk(Z, ItemKind.Shotgun, 3f, new Vector2(342, -270), new Vector2(342, -345), new Vector2(395, -345), new Vector2(395, -270));
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(180, -236), new Vector2(280, -236), new Vector2(280, -292), new Vector2(180, -292));
            Walk(Z, ItemKind.Smg, 5f, new Vector2(240, -300), new Vector2(240, -360), new Vector2(170, -360));
            K.Zone(Z, K.Map.zoneNames[1], new Vector3(300, 10, -305), new Vector3(330, 80, 180), G(436, MAIN_Z, 0.1f), new Color(0.3f, 0.28f, 0.26f), HatStyle.Beanie,
                Spec("Lucky Ten Lanes").origin + new Vector3(-11f, 0.1f, -6f));
        }

        // ---------------------------------------------------------------- the canal

        /// <summary>Quays, shelves, curbs, the head wall, bridges, culverts, stairs and ladders, the storm drain tunnel.</summary>
        private void Canal()
        {
            const int Z = 1;
            float lLength = CANAL_HEAD - CANAL_END, lMid = (CANAL_HEAD + CANAL_END) * 0.5f;
            Transform lCanal = K.Group("Canal", K.Env);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(lCanal, "Quay", new Vector3(CANAL_X + s * 12f, -2.1f, lMid), new Vector3(8f, 4.2f, lLength), CONCRETE);
                K.Block(lCanal, "Shelf", new Vector3(CANAL_X + s * 6f, -3.6f, lMid), new Vector3(4f, 1.2f, lLength), CONCRETE * 0.9f);
                // Curbs along the quay edges, broken at the crossings.
                float z0 = CANAL_HEAD;
                List<float> lCuts = new List<float>(CANAL_BRIDGES);
                lCuts.AddRange(CANAL_CULVERTS);
                lCuts.Add(CANAL_TUNNEL);
                lCuts.Sort((a, b) => b.CompareTo(a));
                foreach (float lCut in lCuts)
                {
                    float z1 = lCut + 9f;
                    if (z0 - z1 > 1f) K.Block(lCanal, "Curb", new Vector3(CANAL_X + s * 8.2f, 0.18f, (z0 + z1) * 0.5f), new Vector3(0.4f, 0.36f, z0 - z1), CONCRETE * 1.1f);
                    z0 = lCut - 9f;
                    if (lCut == CANAL_TUNNEL) break;
                }
            }
            // The head wall where the creek pours in, with its outlet and a little fall.
            K.Block(lCanal, "HeadWall", new Vector3(CANAL_X, -1.8f, CANAL_HEAD + 1f), new Vector3(32f, 5f, 2f), CONCRETE * 0.85f);
            K.Mesh(lCanal, "Outlet", CylMesh, new Vector3(CANAL_X, -2.2f, CANAL_HEAD - 0.02f), new Vector3(2.2f, 0.2f, 2.2f), new Color(0.08f, 0.08f, 0.09f), Quaternion.Euler(90, 0, 0)).isStatic = true;
            GameObject lFall = K.Water(new Vector3(CANAL_X, -2.9f, CANAL_HEAD - 0.6f), new Vector2(2f, 1.6f));
            lFall.transform.rotation = Quaternion.Euler(70f, 0, 0);
            // Bridges: roads on a deck with a single pier in the water (the shelves stay clear underneath).
            foreach (float z in CANAL_BRIDGES)
            {
                float w = z == RAIL_Z ? 6f : z == MAIN_Z ? 15f : 13f;
                Span(new Vector3(CANAL_X - 16f, 0.04f, z), new Vector3(CANAL_X + 16f, 0.04f, z), w, z != RAIL_Z, 4f, "Classic", false);
                K.Block(lCanal, "Pier", new Vector3(CANAL_X, -2.3f, z), new Vector3(1.4f, 3.6f, w * 0.8f), CONCRETE * 0.8f);
                for (int s = -1; s <= 1; s += 2)
                {
                    // Stairs down to the shelves next to every bridge, and a lamp under it.
                    float sz = z + s * (w * 0.5f + 5f);
                    K.Stairs(lCanal, new Vector3(CANAL_X - 6.8f, CANAL_SHELF, sz + s * 6.6f), new Vector3(CANAL_X - 6.8f, 0.02f, sz - s * 0.2f), 2.4f, CONCRETE * 0.95f);
                    K.Lamp(new Vector3(CANAL_X + s * 7.5f, -1.2f, z), SODIUM, 8f, 1f, false);
                }
                if (z == RAIL_Z)
                    for (int s = -1; s <= 1; s += 2) K.Block(lCanal, "Girder", new Vector3(CANAL_X, 0.7f, z + s * 3.1f), new Vector3(32f, 1.4f, 0.3f), new Color(0.35f, 0.3f, 0.28f));
            }
            foreach (float z in CANAL_CULVERTS) Culvert(lCanal, z);
            for (int i = 0; i < 6; i++)
            {
                float z = 30f - i * 90f;
                Ladder(new Vector3(CANAL_X + 7.8f, CANAL_SHELF, z), 3.4f, 270f, "Suburb");
            }
            // The storm drain tunnel into the south woods: a roof under the rise, a portal, the dark end, the extraction.
            float lHill = Mathf.Max(Surf(CANAL_X - 18f, -463f), Surf(CANAL_X + 18f, -463f));
            K.Block(lCanal, "TunnelRoof", new Vector3(CANAL_X, (lHill - 0.2f) * 0.5f, (CANAL_TUNNEL + CANAL_END) * 0.5f), new Vector3(34f, lHill + 0.2f, CANAL_TUNNEL - CANAL_END), WOODS * 0.9f);
            K.Block(lCanal, "Portal", new Vector3(CANAL_X, -0.2f + 0.6f, CANAL_TUNNEL + 0.4f), new Vector3(18f, 1.2f, 0.8f), CONCRETE * 0.75f);
            for (int s = -1; s <= 1; s += 2) K.Block(lCanal, "PortalWing", new Vector3(CANAL_X + s * 9.5f, lHill * 0.5f, CANAL_TUNNEL + 1f), new Vector3(3f, lHill + 4f, 2f), CONCRETE * 0.75f);
            K.Block(lCanal, "TunnelDark", new Vector3(CANAL_X, -2.1f, CANAL_END + 2f), new Vector3(16f, 3.8f, 4f), new Color(0.04f, 0.04f, 0.05f));
            for (int i = 0; i < 3; i++) K.Lamp(new Vector3(CANAL_X, -0.8f, CANAL_TUNNEL - 5f - i * 7f), new Color(0.8f, 0.9f, 0.75f), 8f, 1f, false);
            K.Point(MatchPoint.Kind.Extraction, new Vector3(CANAL_X - 6f, CANAL_SHELF + 0.1f, -466f), 180, "Storm drain outfall");
            K.Npc(NpcRole.Guard, new Vector3(CANAL_X + 6f, CANAL_SHELF + 0.1f, -440f), 0, Z, ItemKind.Shotgun);
            K.Gadget(ItemKind.SmokeGrenade, new Vector3(CANAL_X - 6f, CANAL_SHELF + 0.1f, -230f));
            K.Weapon(ItemKind.Smg, new Vector3(CANAL_X + 6f, CANAL_SHELF + 0.15f, -62f));
            K.Activity(ActivityType.Smoke, new Vector3(CANAL_X - 6f, CANAL_SHELF + 0.1f, MAIN_Z + 2f), 90, Z);
        }

        /// <summary>A lane carried over the canal on a berm with two crawl pipes (1.3 m) through it.</summary>
        private void Culvert(Transform pParent, float pZ)
        {
            Color c = CONCRETE * 0.85f;
            const float TOP = -2.7f;
            float[,] lWalls = { { 292f, 296.6f }, { 299.2f, 300.8f }, { 303.4f, 308f } };
            for (int i = 0; i < 3; i++) K.Block(pParent, "CulvertWall", new Vector3((lWalls[i, 0] + lWalls[i, 1]) * 0.5f, (CANAL_BED - 0.2f + TOP) * 0.5f, pZ), new Vector3(lWalls[i, 1] - lWalls[i, 0], TOP - CANAL_BED + 0.2f, 8f), c);
            K.Block(pParent, "CulvertTop", new Vector3(CANAL_X, TOP * 0.5f, pZ), new Vector3(16f, -TOP, 8f), GRAVEL);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(pParent, "Headwall", new Vector3(CANAL_X, 0.25f, pZ + s * 3.8f), new Vector3(16f, 0.5f, 0.4f), c);
                for (int p = 0; p < 2; p++) K.Block(pParent, "PipeLip", new Vector3(p == 0 ? 297.9f : 302.1f, TOP + 0.1f, pZ + s * 4.05f), new Vector3(2.8f, 0.2f, 0.12f), c * 0.9f, GameLayers.DEFAULT, false);
            }
            K.Gadget(ItemKind.Coin, new Vector3(297.9f, CANAL_BED + 0.15f, pZ + 5f));
        }

        // ---------------------------------------------------------------- the club house

        /// <summary>The Lucky Ten Lanes: lobby bar, pool tables, the card game, Big Ronnie's court, six lanes, the deal in the back room; the pin on the roof.</summary>
        private void Bowling()
        {
            const int Z = 1;
            MapKit.KitBuilding b = K.Building(Spec("Lucky Ten Lanes"));
            Quaternion r = Quaternion.Euler(0, b.Yaw, 0);
            Color lWood = new Color(0.82f, 0.62f, 0.38f), lFelt = new Color(0.12f, 0.45f, 0.25f), lRed = new Color(0.72f, 0.12f, 0.14f);
            // Lanes: maple boards, gutters, ball returns, pins under the masking unit.
            for (int i = 0; i < 6; i++)
            {
                float x = -14.5f + i * 3.2f;
                K.Block(b.root, "Lane", b.Local(x, 0.04f, 4.5f), new Vector3(2.4f, 0.08f, 14f), lWood, GameLayers.DEFAULT, false, r);
                K.Block(b.root, "Gutter", b.Local(x + 1.4f, 0.03f, 4.5f), new Vector3(0.35f, 0.06f, 14f), new Color(0.25f, 0.25f, 0.27f), GameLayers.DEFAULT, false, r);
                List<(Mesh, Matrix4x4, Color)> lPins = new List<(Mesh, Matrix4x4, Color)>();
                for (int row = 0, n = 0; row < 4; row++)
                    for (int k = 0; k <= row; k++, n++)
                        lPins.Add((LowPolyMesh.Cylinder(6, 0.6f), Matrix4x4.TRS(new Vector3((k - row * 0.5f) * 0.3f, 0.2f, row * 0.26f), Quaternion.identity, new Vector3(0.12f, 0.4f, 0.12f)), Color.white));
                K.Mesh(b.root, "Pins", EditorAssets.SaveMesh(LowPolyMesh.Combine(lPins), "Suburb_Pins"), b.Local(x, 0.08f, 10.2f), Vector3.one, Color.white, r).isStatic = true;
                if (i % 2 == 0) K.Block(b.root, "BallReturn", b.Local(x + 1.6f, 0.4f, -1f), new Vector3(0.6f, 0.8f, 1.6f), new Color(0.2f, 0.25f, 0.4f), GameLayers.DEFAULT, true, r);
            }
            K.Block(b.root, "Masking", b.Local(-6.5f, 2.6f, 9.8f), new Vector3(19.6f, 1.4f, 0.4f), new Color(0.12f, 0.12f, 0.2f), GameLayers.DEFAULT, false, r);
            Neon(b.root, b.Local(-6.5f, 2.6f, 9.55f), new Vector3(19f, 0.12f, 0.04f), new Color(0.5f, 1.2f, 2.8f), b.Yaw + 180f);
            Neon(b.root, b.Local(-6.5f, 2.2f, 9.55f), new Vector3(19f, 0.12f, 0.04f), new Color(2.8f, 0.5f, 1.6f), b.Yaw + 180f);
            K.Block(b.root, "Foul", b.Local(-6.5f, 0.09f, -2.4f), new Vector3(19.6f, 0.02f, 0.1f), lRed, GameLayers.DEFAULT, false, r);
            K.Activity(ActivityType.Workout, b.Local(-11.3f, 0.1f, -3.2f), b.Yaw + 180f, Z);
            K.Activity(ActivityType.Workout, b.Local(-1.7f, 0.1f, -3.2f), b.Yaw + 180f, Z);
            for (int i = 0; i < 3; i++) K.Prop("Bench", b.Local(-13f + i * 6.4f, 0, -5.4f), b.Yaw, new Color(0.3f, 0.3f, 0.45f));
            // The lobby: the bar (left), the pool tables, the card game, the shoe counter, Ronnie's court (right).
            K.Prop("BarCounter", b.Local(-14f, 0, -9.2f), b.Yaw, lRed, MapKit.Collide.Box, true);
            K.Prop("BarCounter", b.Local(-10.9f, 0, -9.2f), b.Yaw, lRed, MapKit.Collide.Box);
            K.Prop("BarShelf", b.Local(-12.5f, 0, -11.5f), b.Yaw, null);
            for (int i = 0; i < 4; i++) K.Prop("Stool", b.Local(-15f + i * 1.4f, 0, -8f), b.Yaw + 180f, lRed);
            K.DrinksOn(b.Local(-12.5f, 1.15f, -9.2f), b.Yaw + 180f, Z, 3);
            K.DistractionKit(b.Local(-16.8f, 0, -6f), b.Yaw + 90f, "radio");
            for (int i = 0; i < 2; i++)
            {
                Vector3 t = b.Local(-5f + i * 4.5f, 0, -8.5f);
                K.Block(b.root, "PoolTable", t + Vector3.up * 0.4f, new Vector3(1.4f, 0.8f, 2.6f), new Color(0.35f, 0.22f, 0.15f), GameLayers.DEFAULT, true, r, true);
                K.Block(b.root, "Felt", t + Vector3.up * 0.81f, new Vector3(1.2f, 0.02f, 2.4f), lFelt, GameLayers.DEFAULT, false, r);
                K.Lamp(t + Vector3.up * 2.6f, new Color(1f, 0.9f, 0.7f), 5f, 1f, false);
            }
            // The card game: a round table, four chairs, cards and chips, an ashtray.
            Vector3 lCards = b.Local(6f, 0, -8f);
            K.Prop("TableRound", lCards, b.Yaw, lFelt, MapKit.Collide.Box, true);
            for (int i = 0; i < 4; i++) K.Prop("Chair", lCards + Quaternion.Euler(0, i * 90f, 0) * Vector3.forward * 1.1f, i * 90f + 180f, lWood);
            for (int i = 0; i < 5; i++) K.Block(b.root, "Card", lCards + new Vector3(Rand(-0.35f, 0.35f), 0.81f, Rand(-0.35f, 0.35f)), new Vector3(0.08f, 0.01f, 0.12f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(0, Rand(0, 360), 0));
            for (int i = 0; i < 3; i++) K.Mesh(b.root, "Chips", CylMesh, lCards + new Vector3(-0.3f + i * 0.3f, 0.86f, 0.25f), new Vector3(0.06f, 0.1f, 0.06f), AWNINGS[i]).isStatic = true;
            K.DrinksOn(lCards + Vector3.up * 0.8f, b.Yaw, Z, 2);
            K.Activities(lCards + Quaternion.Euler(0, b.Yaw, 0) * new Vector3(1.8f, 0.1f, 1.6f), b.Yaw, Z, ActivityType.Smoke, ActivityType.Idle);
            K.Prop("Sofa", b.Local(12f, 0, -10.8f), b.Yaw, new Color(0.45f, 0.12f, 0.12f), MapKit.Collide.Box, true);
            K.Prop("Armchair", b.Local(9.6f, 0, -9f), b.Yaw + 90f, new Color(0.45f, 0.12f, 0.12f));
            K.Prop("TableRound", b.Local(12f, 0, -9f), b.Yaw, lWood, MapKit.Collide.Box);
            K.DistractionKit(b.Local(12f, 0, -6.2f), b.Yaw + 180f, "tv");
            K.Activity(ActivityType.Phone, b.Local(9.6f, 0.1f, -7f), b.Yaw, Z);
            K.ToiletKit(b.Local(16.5f, 0, -3f), b.Yaw + 270f, Z);
            K.ToiletKit(b.Local(16.5f, 0, -1.5f), b.Yaw + 270f, Z);
            // The back room: partitions, the deal (a briefcase on the table, crates of stolen goods), the safe.
            K.PartitionKit(b, 0, new Vector3(6, 0, 0), new Vector3(15, 0, 0), 1);
            K.PartitionKit(b, 0, new Vector3(6, 0, 0), new Vector3(6, 0, 12), 1);
            K.Prop("Table", b.Local(10f, 0, 6f), b.Yaw, new Color(0.4f, 0.3f, 0.22f), MapKit.Collide.Box, true);
            K.Block(b.root, "Briefcase", b.Local(10f, 0.92f, 6f), new Vector3(0.5f, 0.12f, 0.35f), new Color(0.15f, 0.12f, 0.1f), GameLayers.DEFAULT, false, r);
            K.Block(b.root, "Cash", b.Local(10.6f, 0.86f, 5.8f), new Vector3(0.3f, 0.1f, 0.2f), new Color(0.4f, 0.65f, 0.35f), GameLayers.DEFAULT, false, r);
            for (int i = 0; i < 3; i++) K.Crate(b.Local(7.4f, 0, 10.6f - i * 1.3f));
            K.Prop("CrateLarge", b.Local(13f, 0, 10.6f), b.Yaw, null, MapKit.Collide.Box, true);
            K.Block(b.root, "Safe", b.Local(13.8f, 0.6f, 2f), new Vector3(0.9f, 1.2f, 0.8f), new Color(0.3f, 0.32f, 0.35f), GameLayers.DEFAULT, true, r);
            K.Activities(b.Local(10f, 0.1f, 4.6f), b.Yaw, Z, ActivityType.Inspect, ActivityType.Office);
            K.UniqueWeapon(ItemKind.Flamethrower, b.Local(8f, 0.15f, 2f), "Bowling alley back room");
            K.Lamp(b.Local(10f, MapKit.STOREY - 0.45f, 6f), WARM, 8f, 1.2f, false);
            for (int i = 0; i < 3; i++) K.Lamp(b.Local(-12f + i * 9f, MapKit.STOREY - 0.45f, -7f), new Color(1f, 0.75f, 0.5f), 11f, 1.3f, false);
            K.Lamp(b.Local(-6f, MapKit.STOREY - 0.45f, 3f), new Color(0.75f, 0.8f, 1f), 12f, 1.1f, false);
            K.Sprinklers(b.Local(0, 0, 0), new Vector3(b.W, MapKit.STOREY, b.D), b.Yaw, MapKit.STOREY - 0.05f);
            K.Npc(NpcRole.Guard, b.Local(-3f, 0.1f, -10.5f), b.Yaw + 180f, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, b.Local(3f, 0.1f, -10.5f), b.Yaw + 180f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, b.Local(-9f, 0.1f, -7f), b.Yaw + 90f, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, b.Local(7.6f, 0.1f, 6f), b.Yaw + 90f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, b.Local(12.5f, 0.1f, 8f), b.Yaw + 270f, Z, ItemKind.Shotgun);
            K.Weapon(ItemKind.Shotgun, b.Local(-16f, 0.15f, 10f));
            K.Weapon(ItemKind.Pistol, b.Local(14f, 1.0f, -10.6f));
            K.Gadget(ItemKind.C4, b.Local(11f, 0.95f, 6.2f));

            // The roof: the giant pin and the sign, a lookout; the parking lot full of bikes; the back alley.
            float lRoof = RoofY(b.spec);
            Vector3 lPin = b.Local(-12f, MapKit.STOREY, -6f);
            K.Mesh(b.root, "PinBody", BallMesh, lPin + Vector3.up * 2.6f, new Vector3(2.6f, 4.4f, 2.6f), Color.white, null, true).isStatic = true;
            K.Mesh(b.root, "PinNeck", CylMesh, lPin + Vector3.up * 5.4f, new Vector3(1.1f, 1.6f, 1.1f), Color.white).isStatic = true;
            K.Mesh(b.root, "PinHead", BallMesh, lPin + Vector3.up * 6.8f, new Vector3(1.7f, 1.9f, 1.7f), Color.white).isStatic = true;
            K.Mesh(b.root, "PinStripe", CylMesh, lPin + Vector3.up * 5.6f, new Vector3(1.18f, 0.35f, 1.18f), lRed).isStatic = true;
            K.Block(b.root, "SignBoard", b.Local(2f, MapKit.STOREY + 1.4f, -11.8f), new Vector3(16f, 2.2f, 0.3f), new Color(0.15f, 0.12f, 0.25f), GameLayers.DEFAULT, true, r);
            Neon(b.root, b.Local(2f, MapKit.STOREY + 1.4f, -12f), new Vector3(14.5f, 1.2f, 0.04f), new Color(2.8f, 0.6f, 1.8f), b.Yaw + 180f);
            Neon(b.root, b.Local(2f, MapKit.STOREY + 0.35f, -12f), new Vector3(15.5f, 0.1f, 0.04f), new Color(0.6f, 2.6f, 2.8f), b.Yaw + 180f);
            K.Npc(NpcRole.Lookout, b.Local(6f, MapKit.STOREY + 0.1f, -9f), b.Yaw + 180f, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, b.Local(-4f, MapKit.STOREY + 0.15f, 9f));
            for (int i = 0; i < 9; i++) Motorbike(G(190f + i * 3.4f, -258f), 180f + Rand(-12f, 12f), WRECKS[(i * 2) % WRECKS.Length] * 0.7f + new Color(0.1f, 0.1f, 0.1f));
            K.Car(G(218, -248), 90, new Color(0.15f, 0.15f, 0.17f));
            K.Car(G(188, -246), 270, new Color(0.45f, 0.1f, 0.1f));
            K.Prop("StreetLamp", G(205, -246), 0);
            K.Lamp(G(205, -246, 4.3f), SODIUM, 15f, 1.6f, false);
            Guard(Z, 199f, -261f, 0, ItemKind.Smg);
            Guard(Z, 211f, -261f, 0, ItemKind.Pistol);
            // The bikers' gym outside the side door, smokers in the alley behind.
            Vector3 lGym = G(246, -282);
            K.Prop("Bench", lGym, 90, new Color(0.2f, 0.2f, 0.22f), MapKit.Collide.Box);
            K.Block(K.Props, "WeightRack", lGym + new Vector3(0, 0.6f, 3f), new Vector3(2f, 1.2f, 0.5f), new Color(0.25f, 0.25f, 0.28f), GameLayers.DEFAULT, true, null, true);
            K.Block(K.Props, "BagFrame", lGym + new Vector3(3f, 1.4f, -2f), new Vector3(0.15f, 2.8f, 0.15f), new Color(0.3f, 0.3f, 0.32f));
            K.Mesh(K.Props, "PunchBag", CylMesh, lGym + new Vector3(3.6f, 1.6f, -2f), new Vector3(0.6f, 1.3f, 0.6f), lRed).isStatic = true;
            for (int i = 0; i < 3; i++) K.Mesh(K.Props, "Tire", CylMesh, lGym + new Vector3(-2.5f, 0.2f + i * 0.4f, -2f), new Vector3(1.1f, 0.38f, 1.1f), new Color(0.12f, 0.12f, 0.13f), null, i == 0).isStatic = true;
            K.Activity(ActivityType.Workout, lGym + new Vector3(1.2f, 0.1f, 0), 270, Z);
            K.Activity(ActivityType.Smoke, G(214, -292, 0.1f), 0, Z);
            K.HidingKit(G(222, -292), 0, "Dumpster");
            for (int i = 0; i < 2; i++) Clutter(G(238f + i * 14f, -266f), 90, i + 2);
            GasBottle(G(245f, -266f));
            GasBottle(G(245.5f, -266.4f));
        }

        /// <summary>A parked chopper: two wheels, a frame, a tank and a seat; one box collider.</summary>
        private void Motorbike(Vector3 pAt, float pYaw, Color pTint)
        {
            Transform lBike = K.Group("Motorbike", K.Props);
            lBike.SetPositionAndRotation(pAt, Quaternion.Euler(0, pYaw, 0));
            Quaternion r = lBike.rotation;
            for (int w = -1; w <= 1; w += 2) K.Mesh(lBike, "Wheel", CylMesh, pAt + r * new Vector3(0, 0.36f, w * 0.75f), new Vector3(0.72f, 0.14f, 0.72f), new Color(0.1f, 0.1f, 0.11f), r * Quaternion.Euler(0, 0, 90)).isStatic = true;
            K.Block(lBike, "Frame", pAt + r * new Vector3(0, 0.62f, 0), new Vector3(0.22f, 0.3f, 1.5f), new Color(0.75f, 0.76f, 0.8f), GameLayers.DEFAULT, false, r);
            K.Block(lBike, "Tank", pAt + r * new Vector3(0, 0.88f, 0.25f), new Vector3(0.34f, 0.26f, 0.55f), pTint, GameLayers.DEFAULT, false, r);
            K.Block(lBike, "Seat", pAt + r * new Vector3(0, 0.84f, -0.3f), new Vector3(0.3f, 0.12f, 0.6f), new Color(0.1f, 0.08f, 0.07f), GameLayers.DEFAULT, false, r);
            K.Block(lBike, "Bars", pAt + r * new Vector3(0, 1.08f, 0.62f), new Vector3(0.8f, 0.05f, 0.05f), new Color(0.75f, 0.76f, 0.8f), GameLayers.DEFAULT, false, r);
            BoxCollider lBox = lBike.gameObject.AddComponent<BoxCollider>();
            lBox.center = new Vector3(0, 0.55f, 0);
            lBox.size = new Vector3(0.7f, 1.1f, 2.1f);
        }

        // ---------------------------------------------------------------- the junkyard

        /// <summary>A wrecked car (kit model, rusty tint) with a box collider; tilted when asked.</summary>
        private void Wreck(Vector3 pAt, float pYaw, string pModel, float pRoll, bool pCover, Transform pParent)
        {
            GameObject lCar = K.Prop(pModel, pAt, pYaw, GameArt.Vary(Pick(WRECKS), 0.08f, K.Rng), MapKit.Collide.Box, pCover, pParent);
            if (lCar == null) return;
            if (pRoll != 0f) lCar.transform.rotation = Quaternion.Euler(Rand(-4f, 4f), pYaw + 180f, pRoll);
        }

        /// <summary>The yard: fence and gate, lanes between stacked wrecks, a vantage pile, bales, tires, the office, the chop shop, the lookout tower, the freight wagons.</summary>
        private void Yard()
        {
            const int Z = 1;
            Transform lYard = K.Group("Junkyard", K.Env);
            Color lFence = new Color(0.52f, 0.5f, 0.46f);
            // Corrugated fence (solid, 3 m) with the gate on Main Street and the culvert lane gate on the west.
            K.Fence(G(320, -244), G(364, -244), 3f, lFence, true);
            K.Fence(G(376, -244), G(436, -244), 3f, lFence, true);
            K.Fence(G(436, -244), G(436, -362), 3f, lFence, true);
            K.Fence(G(320, -362), G(436, -362), 3f, lFence, true);
            K.Fence(G(320, -244), G(320, -296), 3f, lFence, true);
            K.Fence(G(320, -304), G(320, -362), 3f, lFence, true);
            K.Fence(G(426, -362), G(426, -358), 3f, lFence, true);
            K.Prop("Barrier", G(370, -244), 0, null, MapKit.Collide.Box, true);
            Guard(Z, 366f, -241f, 0, ItemKind.Shotgun);
            Guard(Z, 374f, -241f, 0, ItemKind.Smg);
            for (int s = -1; s <= 1; s += 2) { K.Block(K.Env, "GatePost", G(370 + s * 6.4f, -244, 2f), new Vector3(0.6f, 4f, 0.6f), RUST); }
            K.Block(K.Env, "GateSign", G(370, -244, 4.3f), new Vector3(13f, 1.2f, 0.2f), new Color(0.75f, 0.6f, 0.2f));
            // Wreck walls: four rows of stacks along the yard, gaps for the cross lanes.
            float[] lRows = { 334f, 350f, 366f, 382f };
            for (int row = 0; row < lRows.Length; row++)
                for (float z = -270f; z > -352f; z -= 4.7f)
                {
                    if (Mathf.Abs(z + 305f) < 6f || (row == 3 && z < -322f)) continue;
                    float x = lRows[row] + Rand(-0.4f, 0.4f);
                    float lYaw = 90f + Rand(-8f, 8f);
                    Wreck(new Vector3(x, Surf(x, z), z), lYaw, Pick(new[] { "Sedan", "Pickup", "Van", "Sedan" }), 0f, true, lYard);
                    int lHigh = 1 + (int)(Mathf.PerlinNoise(x * 0.1f, z * 0.1f) * 2.6f);
                    for (int k = 1; k < lHigh; k++) Wreck(new Vector3(x + Rand(-0.3f, 0.3f), Surf(x, z) + 1.85f + (k - 1) * 1.6f, z), lYaw + Rand(-10f, 10f), "Sedan", Rand(-3f, 3f), false, lYard);
                }
            // The vantage pile: a pyramid of wrecks with a plank ramp up to a lookout deck.
            Vector3 lPile = G(404, -338);
            for (int i = 0; i < 6; i++) Wreck(lPile + new Vector3(-3f + (i % 3) * 3f, 0, -2.2f + (i / 3) * 4.4f), 90f, "Van", 0f, i % 2 == 0, lYard);
            for (int i = 0; i < 3; i++) Wreck(lPile + new Vector3(-2f + i * 2.2f, 2.15f, 0), Rand(0f, 30f), "Sedan", Rand(-6f, 6f), false, lYard);
            Deck(lPile + Vector3.up * 3.9f, new Vector2(7f, 4f), 0f, 0.2f, PLANK, 0.8f, lYard);
            K.Ramp(lYard, lPile + new Vector3(-14f, 0.02f, 0), lPile + new Vector3(-3.4f, 3.9f, 0), 1.6f, PLANK, 0.2f);
            K.Npc(NpcRole.Lookout, lPile + new Vector3(1.5f, 4f, 0.5f), 0, Z, ItemKind.Sniper);
            K.Activity(ActivityType.Lookout, lPile + new Vector3(-1.5f, 4f, -0.5f), 270, Z);
            // Crushed bales, tire piles, scrap heaps, barrels and gas bottles near the patrols.
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = G(424f - (i % 3) * 2f, -300f - (i / 3) * 3.6f, 0.5f + (i % 2) * 1.0f);
                K.Block(lYard, "Bale", p, new Vector3(1.8f, 1f, 3.2f), GameArt.Vary(SCRAP, 0.15f, K.Rng), GameLayers.DEFAULT, true, Quaternion.Euler(0, Rand(-6f, 6f), 0), i < 3);
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 t = G(350f + i * 18f, -357f);
                for (int k = 0; k < 4; k++) K.Mesh(lYard, "Tire", CylMesh, t + new Vector3(Rand(-0.2f, 0.2f), 0.2f + k * 0.38f, Rand(-0.2f, 0.2f)), new Vector3(1.1f, 0.36f, 1.1f), new Color(0.12f, 0.12f, 0.13f), null, k == 0).isStatic = true;
            }
            for (int i = 0; i < 4; i++)
            {
                GameObject lHeap = K.Mesh(lYard, "ScrapHeap", DomeMesh, G(392f + (i % 2) * 22f, -284f - (i / 2) * 30f), new Vector3(7f, 3.2f, 6f), GameArt.Vary(SCRAP, 0.12f, K.Rng), Quaternion.Euler(0, Rand(0, 360), 0), true);
                lHeap.isStatic = true;
            }
            for (int i = 0; i < 3; i++) GasBottle(G(398f + i * 0.5f, -268f));
            K.Barrel(G(358, -305), true);
            K.Barrel(G(358.8f, -305.6f), true);
            K.HidingKit(G(328, -250), 90, "Dumpster");
            K.HidingKit(G(428, -352), 270, "Dumpster");
            // The office trailer (Tobias's books), the chop shop (Jolene under a car), the tower.
            MapKit.KitBuilding o = K.Building(Spec("Junkyard Office"));
            K.ToiletKit(o.Local(3f, 0, 3f), o.Yaw + 180f, Z);
            K.Npc(NpcRole.Guard, o.Local(-2f, 0.1f, -2f), o.Yaw + 180f, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, o.Local(2.5f, 0.95f, -3f));
            K.Activity(ActivityType.Smoke, G(338, -248, 0.1f), 180, Z);
            MapKit.KitBuilding c = K.Building(Spec("Hart's Chop Shop"));
            for (int i = 0; i < 2; i++)
            {
                Vector3 lLift = c.Local(-3f + i * 6f, 0, -1f);
                K.Block(c.root, "LiftPost", lLift + new Vector3(1.4f, 0.9f, 0), new Vector3(0.3f, 1.8f, 0.3f), new Color(0.85f, 0.65f, 0.15f));
                K.Block(c.root, "LiftPost", lLift + new Vector3(-1.4f, 0.9f, 0), new Vector3(0.3f, 1.8f, 0.3f), new Color(0.85f, 0.65f, 0.15f));
                Wreck(lLift + Vector3.up * 1.4f, c.Yaw, "Sedan", 0f, false, c.root);
            }
            K.Prop("Cabinet", c.Local(-6.5f, 0, 4.6f), c.Yaw, new Color(0.7f, 0.15f, 0.15f), MapKit.Collide.Box, true);
            K.Prop("Cabinet", c.Local(-5.2f, 0, 4.6f), c.Yaw, new Color(0.7f, 0.15f, 0.15f), MapKit.Collide.Box);
            K.Activity(ActivityType.Inspect, c.Local(3f, 0.1f, 1.4f), c.Yaw + 180f, Z);
            K.Activity(ActivityType.Phone, c.Local(-6f, 0.1f, 3f), c.Yaw, Z);
            K.DistractionKit(c.Local(6.5f, 0, 4.6f), c.Yaw + 180f, "generator");
            K.Npc(NpcRole.Guard, c.Local(0f, 0.1f, 3f), c.Yaw + 180f, Z, ItemKind.Smg);
            K.Lamp(c.Local(0, MapKit.STOREY - 0.45f, 0), new Color(0.85f, 0.9f, 1f), 10f, 1.4f, false);
            K.Weapon(ItemKind.Shotgun, c.Local(-6f, 0.95f, 4.6f));
            Watchtower(Z, 328f, -354f, 9f, 45f, RUST);
            K.Activity(ActivityType.Lookout, G(331, -351, 9.2f), 45, Z);
            // Freight wagons at the railway's buffer, along the south fence (a way over it).
            for (int w = 0; w < 2; w++)
            {
                float x = 404f + w * 17f;
                K.Block(lYard, "Gondola", new Vector3(x, 1.2f, RAIL_Z), new Vector3(15f, 0.3f, 3f), RUST * 0.8f);
                for (int s = -1; s <= 1; s += 2)
                {
                    K.Block(lYard, "GondolaSide", new Vector3(x, 2.1f, RAIL_Z + s * 1.45f), new Vector3(15f, 1.8f, 0.12f), RUST, GameLayers.DEFAULT, true, null, s < 0);
                    K.Block(lYard, "GondolaEnd", new Vector3(x + s * 7.45f, 2.1f, RAIL_Z), new Vector3(0.12f, 1.8f, 3f), RUST);
                }
                for (int k = 0; k < 3; k++) K.Block(lYard, "Bale", new Vector3(x - 4f + k * 4f, 1.85f, RAIL_Z), new Vector3(1.8f, 1f, 2.4f), GameArt.Vary(SCRAP, 0.15f, K.Rng), GameLayers.DEFAULT, true, Quaternion.Euler(0, Rand(-15f, 15f), 0));
                K.Block(lYard, "Bogie", new Vector3(x, 0.5f, RAIL_Z), new Vector3(12f, 0.8f, 2.4f), new Color(0.15f, 0.15f, 0.16f), GameLayers.DEFAULT, false);
            }
            Ladder(new Vector3(396.2f, 0f, RAIL_Z), 3f, 270f, "Suburb");
            K.Weapon(ItemKind.Shotgun, G(352, -322, 0.15f));
            K.Weapon(ItemKind.Smg, G(410, -352, 0.15f));
            K.Gadget(ItemKind.Brick, G(374, -300, 0.15f));
            for (int i = 0; i < 4; i++) { P("PowerPole", 330f + i * 32f, -247f, 0); }
            for (int i = 0; i < 4; i++) K.Lamp(G(340f + i * 28f, -305f, 6f), SODIUM, 18f, 1.4f, false);
        }

        /// <summary>The magnet crane: a lattice mast with a ladder, the cab and its platform (rocket launcher), the boom, the cable and the magnet over a wreck.</summary>
        private void Crane()
        {
            const int Z = 1;
            Vector3 c = G(398, -305);
            Transform lCrane = K.Group("Crane", K.Env);
            Color lYellow = new Color(0.92f, 0.7f, 0.15f);
            const float H = 16f;
            K.Block(lCrane, "CraneBase", c + Vector3.up * 0.5f, new Vector3(5f, 1f, 5f), new Color(0.3f, 0.3f, 0.32f));
            for (int k = 0; k < 4; k++) K.Block(lCrane, "MastPost", c + new Vector3(k % 2 == 0 ? -0.9f : 0.9f, H * 0.5f + 1f, k < 2 ? -0.9f : 0.9f), new Vector3(0.25f, H, 0.25f), lYellow);
            for (int k = 1; k < 8; k++)
            {
                K.Block(lCrane, "MastBrace", c + Vector3.up * (1f + k * 2f), new Vector3(2f, 0.15f, 0.15f), lYellow * 0.9f, GameLayers.DEFAULT, false, Quaternion.Euler(0, 0, k % 2 == 0 ? 35f : -35f));
                K.Block(lCrane, "MastBrace", c + Vector3.up * (1f + k * 2f), new Vector3(0.15f, 0.15f, 2f), lYellow * 0.9f, GameLayers.DEFAULT, false, Quaternion.Euler(k % 2 == 0 ? 35f : -35f, 0, 0));
            }
            Deck(c + Vector3.up * (H + 1f), new Vector2(5.5f, 5.5f), 0f, 0.25f, new Color(0.35f, 0.35f, 0.37f), 0.6f, lCrane);
            for (int s = 0; s < 4; s++)
            {
                Quaternion r = Quaternion.Euler(0, s * 90f, 0);
                if (s != 2) K.Block(lCrane, "PlatformRail", c + Vector3.up * (H + 1.55f) + r * new Vector3(0, 0, 2.7f), new Vector3(5.5f, 1.1f, 0.08f), lYellow, GameLayers.DEFAULT, true, r);
            }
            K.Block(lCrane, "Cab", c + new Vector3(1.2f, H + 2.3f, 1.2f), new Vector3(2.4f, 2.4f, 2.6f), lYellow, GameLayers.DEFAULT, true, null, true);
            Neon(lCrane, c + new Vector3(1.2f, H + 2.6f, -0.12f), new Vector3(1.8f, 0.9f, 0.04f), new Color(1.6f, 1.4f, 1f));
            Vector3 lTip = c + new Vector3(-24f, H + 6f, 0);
            Vector3 lFoot = c + Vector3.up * (H + 2.5f);
            K.Block(lCrane, "Boom", (lTip + lFoot) * 0.5f, new Vector3(0.9f, 0.9f, Vector3.Distance(lTip, lFoot)), lYellow, GameLayers.DEFAULT, false, Quaternion.LookRotation(lTip - lFoot));
            K.Block(lCrane, "CounterJib", c + new Vector3(5f, H + 3f, 0), new Vector3(8f, 0.6f, 0.8f), lYellow, GameLayers.DEFAULT, false);
            K.Block(lCrane, "Counterweight", c + new Vector3(8.4f, H + 2.2f, 0), new Vector3(1.6f, 1.6f, 1.6f), new Color(0.3f, 0.3f, 0.32f));
            float lMagnetY = Surf(lTip.x, lTip.z) + 4.4f;
            K.Block(lCrane, "Cable", new Vector3(lTip.x, (lTip.y + lMagnetY) * 0.5f, lTip.z), new Vector3(0.06f, lTip.y - lMagnetY, 0.06f), new Color(0.15f, 0.15f, 0.16f), GameLayers.DEFAULT, false);
            K.Mesh(lCrane, "Magnet", CylMesh, new Vector3(lTip.x, lMagnetY - 0.3f, lTip.z), new Vector3(2.4f, 0.6f, 2.4f), new Color(0.25f, 0.25f, 0.28f), null, true).isStatic = true;
            Wreck(new Vector3(lTip.x, lMagnetY - 2.3f, lTip.z), 30f, "Sedan", 12f, false, lCrane);
            Ladder(c + new Vector3(0, 1f, -2.95f), H + 0.4f, 180f, "Suburb");
            for (int k = 0; k < 3; k++) K.Block(lCrane, "LadderStay", c + new Vector3(0, 4f + k * 5f, -1.95f), new Vector3(0.6f, 0.1f, 2f), lYellow * 0.8f, GameLayers.DEFAULT, false);
            K.Npc(NpcRole.Lookout, c + new Vector3(-1.5f, H + 1.1f, -1.5f), 225, Z, ItemKind.Sniper);
            K.UniqueWeapon(ItemKind.RocketLauncher, c + new Vector3(-1.8f, H + 1.15f, 1.6f), "Junkyard crane cab");
            K.Lamp(lTip + Vector3.up * 0.6f, new Color(1f, 0.15f, 0.1f), 6f, 1.6f, false);
        }

        /// <summary>The crusher: a press on its frame with a car half crushed, the control booth (generator), a crawl gap behind it through the fence.</summary>
        private void Crusher()
        {
            const int Z = 1;
            Vector3 c = G(420, -330);
            Transform lCrusher = K.Group("Crusher", K.Env);
            Color lBlue = new Color(0.2f, 0.32f, 0.5f);
            K.Block(lCrusher, "CrusherBed", c + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 7f), new Color(0.3f, 0.3f, 0.32f), GameLayers.DEFAULT, true, null, true);
            for (int s = -1; s <= 1; s += 2) K.Block(lCrusher, "CrusherColumn", c + new Vector3(s * 2.2f, 3.2f, 0), new Vector3(0.6f, 6.4f, 1.2f), lBlue);
            K.Block(lCrusher, "CrusherHead", c + Vector3.up * 5f, new Vector3(5f, 1.6f, 6f), lBlue);
            K.Block(lCrusher, "Ram", c + Vector3.up * 3.6f, new Vector3(3.8f, 1.2f, 6.6f), new Color(0.55f, 0.55f, 0.58f));
            GameObject lFlat = K.Prop("Sedan", c + Vector3.up * 1.2f, 0, Pick(WRECKS), MapKit.Collide.None, false, lCrusher);
            if (lFlat != null) lFlat.transform.localScale = new Vector3(1.05f, 0.45f, 1f);
            K.Block(lCrusher, "Booth", c + new Vector3(-4f, 1.3f, 4f), new Vector3(2f, 2.6f, 2f), new Color(0.85f, 0.85f, 0.8f), GameLayers.DEFAULT, true, null, true);
            K.DistractionKit(c + new Vector3(-4f, 0, 2.3f), 180, "generator");
            K.UniqueWeapon(ItemKind.Flamethrower, c + new Vector3(-4f, 0.15f, 6f), "Junkyard crusher booth");
            K.Activity(ActivityType.Inspect, c + new Vector3(-3f, 0.1f, -4.5f), 90, Z);
            K.Npc(NpcRole.Guard, c + new Vector3(-6f, 0.1f, -3f), 90, Z, ItemKind.Shotgun);
            // A crawl gap under the east fence behind the crusher.
            K.Block(lCrusher, "FenceGap", G(436.2f, -330, 0.55f), new Vector3(0.4f, 1.1f, 2f), new Color(0.1f, 0.1f, 0.1f), GameLayers.DEFAULT, false);
        }

        /// <summary>Granny Voss's farmhouse: pies cooling on the porch, the vegetable garden, the coop, the grandsons on the porch.</summary>
        private void Farmhouse()
        {
            const int Z = 1;
            MapKit.KitBuilding g = K.Building(Spec("Voss Farmhouse"));
            Vector3 lPorch = Facade(g, 0, 1.5f, 0.35f, 1.6f);
            Deck(lPorch, new Vector2(12f, 3f), g.Yaw, 0.35f, PLANK, 1f);
            for (int i = 0; i < 4; i++) K.Piece("Timber", "Column", Facade(g, 0, i, 0.35f, 2.9f), FacadeYaw(g, 0), null, g.root, MapKit.Collide.Box);
            K.Block(g.root, "PorchRoof", Facade(g, 0, 1.5f, 3.4f, 1.6f), new Vector3(12.4f, 0.2f, 3.4f), new Color(0.45f, 0.22f, 0.18f), GameLayers.DEFAULT, true, Quaternion.Euler(0, g.Yaw, 0));
            K.Prop("Table", Facade(g, 0, 2.5f, 0.35f, 1.4f), g.Yaw, Color.white, MapKit.Collide.Box, true);
            for (int i = 0; i < 3; i++) K.Mesh(g.root, "Pie", CylMesh, Facade(g, 0, 2.5f, 1.2f, 1.4f) + new Vector3(-0.5f + i * 0.5f, 0, 0), new Vector3(0.34f, 0.08f, 0.34f), new Color(0.85f, 0.55f, 0.28f)).isStatic = true;
            K.DrinksOn(Facade(g, 0, 0.5f, 1.15f, 1.4f), g.Yaw + 180f, Z, 2);
            K.Prop("Table", Facade(g, 0, 0.5f, 0.35f, 1.4f), g.Yaw, Color.white, MapKit.Collide.Box);
            K.Prop("Armchair", Facade(g, 0, 3.4f, 0.35f, 1.2f), g.Yaw + 180f, new Color(0.5f, 0.35f, 0.25f));
            K.Npc(NpcRole.Guard, Facade(g, 0, 3.2f, 0.45f, 2.4f), g.Yaw + 180f, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, G(140, -350, 0.1f), 180, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Shotgun, g.Local(-4f, MapKit.STOREY + 0.15f, 3f));
            K.Gadget(ItemKind.Poison, g.Local(3f, 0.95f, 3.5f));
            // The vegetable garden (rows, a scarecrow, a shed), the coop, the clothesline.
            Vector3 lGarden = G(152, -352);
            for (int row = 0; row < 6; row++)
            {
                K.Block(K.Props, "GardenRow", lGarden + new Vector3(-8f + row * 3f, 0.15f, 0), new Vector3(1.4f, 0.3f, 12f), MUD, GameLayers.DEFAULT, false);
                for (int k = 0; k < 5; k++) Nat(row % 2 == 0 ? "BushC" : "GrassTuft", lGarden.x - 8f + row * 3f, lGarden.z - 5f + k * 2.5f, row % 2 == 0 ? 0.5f : 1.6f, -1f, row % 3 == 0 ? new Color(0.35f, 0.6f, 0.25f) : new Color(0.55f, 0.65f, 0.3f), MapKit.Collide.None);
            }
            K.Block(K.Props, "ScarecrowPost", lGarden + new Vector3(0, 1.2f, 7f), new Vector3(0.12f, 2.4f, 0.12f), PLANK);
            K.Block(K.Props, "ScarecrowArms", lGarden + new Vector3(0, 1.8f, 7f), new Vector3(1.6f, 0.1f, 0.1f), PLANK);
            K.Mesh(K.Props, "ScarecrowHead", BallMesh, lGarden + new Vector3(0, 2.4f, 7f), Vector3.one * 0.45f, new Color(0.9f, 0.8f, 0.5f)).isStatic = true;
            K.Activity(ActivityType.Garden, lGarden + new Vector3(-6.5f, 0.1f, 2f), 90, Z);
            K.Activity(ActivityType.Garden, lGarden + new Vector3(4f, 0.1f, -2f), 270, Z);
            K.Block(K.Env, "Coop", G(170, -344, 0.9f), new Vector3(3f, 1.8f, 2f), new Color(0.75f, 0.35f, 0.25f), GameLayers.DEFAULT, true, null, true);
            K.HidingKit(G(170, -350), 0, "Haystack");
            for (int s = -1; s <= 1; s += 2) K.Block(K.Props, "LinePost", G(134 + s * 5f, -336, 1.1f), new Vector3(0.1f, 2.2f, 0.1f), PLANK);
            for (int i = 0; i < 4; i++) K.Block(K.Props, "Laundry", G(130.5f + i * 2.2f, -336, 1.6f), new Vector3(1.2f, 0.9f, 0.03f), AWNINGS[i % AWNINGS.Length] * 1.2f, GameLayers.DEFAULT, false);
            K.Fence(G(132, -364), G(172, -364), 1.1f, new Color(0.9f, 0.88f, 0.82f));
            K.Fence(G(132, -364), G(132, -318), 1.1f, new Color(0.9f, 0.88f, 0.82f));
            K.Car(G(168, -322), 0, new Color(0.45f, 0.5f, 0.42f));
        }

        /// <summary>The field of tall grass between the bowling alley, the farmhouse, the canal and the railway; dead trees and a rusting tractor.</summary>
        private void Field()
        {
            Color lStraw = new Color(0.8f, 0.74f, 0.42f);
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x = 196f + i * 22f, z = -318f - j * 24f;
                    if (x > 270f) continue;
                    K.Grass(G(x, z), new Vector2(20f, 22f), lStraw);
                }
            K.Grass(G(260, -312), new Vector2(16f, 18f), lStraw);
            K.Grass(G(274, -200), new Vector2(14f, 40f), lStraw);
            K.Grass(G(330, -172), new Vector2(20f, 24f), lStraw);
            for (int i = 0; i < 5; i++) Nat("DeadTree", 190f + i * 18f + Rand(-4f, 4f), -360f + Rand(-3f, 3f), Rand(0.6f, 0.9f));
            Vector3 t = G(232, -352);
            K.Block(K.Env, "Tractor", t + Vector3.up * 1.1f, new Vector3(1.8f, 1.4f, 3f), new Color(0.6f, 0.2f, 0.15f), GameLayers.DEFAULT, true, Quaternion.Euler(0, 25f, 0), true);
            K.Mesh(K.Env, "TractorWheel", CylMesh, t + new Vector3(0.9f, 0.8f, -0.6f), new Vector3(1.6f, 0.5f, 1.6f), new Color(0.12f, 0.12f, 0.13f), Quaternion.Euler(0, 25f, 90)).isStatic = true;
            K.Mesh(K.Env, "TractorWheel", CylMesh, t + new Vector3(-0.9f, 0.8f, -0.2f), new Vector3(1.6f, 0.5f, 1.6f), new Color(0.12f, 0.12f, 0.13f), Quaternion.Euler(0, 25f, 90)).isStatic = true;
            K.Weapon(ItemKind.Sniper, t + new Vector3(0, 1.9f, 0));
        }
    }
}
