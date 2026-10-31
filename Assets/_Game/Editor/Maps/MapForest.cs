using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEditor;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <b>Pinecrest Hollow</b> - a steep forested valley of 500 m. A river runs down the valley floor
    /// into a lake under a cliff; ridges close every side.
    /// <list type="bullet">
    /// <item><b>Millbrook</b> (zone 0): a timber town on the west slope, three streets stacked on
    /// terraces (Riverside, Main, Upper) linked by switchbacks and stairs; the festival square on
    /// Main, the town hall and its bell tower on the upper terrace, the sawmill by the river.</item>
    /// <item><b>The Outpost and the Grove</b> (zone 1): a walled military outpost on the high east
    /// spur (towers, barracks, HQ, armory, mess, helipad) and the commune's cabins on the shelf below.</item>
    /// <item><b>Lakeside Lodge</b> (zone 2): the lodge on its plateau over the lake, the garden party,
    /// the guest house, the boathouse and the pier down the cliff.</item>
    /// </list>
    /// </summary>
    public class MapForest : HillMap
    {
        protected override string MapFile => "Map_Forest";

        [MenuItem("Goobyr/Build/Map Forest")]
        public static void BuildMenu() => new MapForest().Build();

        private static readonly Vector2[] RIVER =
        {
            new Vector2(10, -250), new Vector2(25, -180), new Vector2(5, -110), new Vector2(-10, -40), new Vector2(12, 30),
            new Vector2(20, 90), new Vector2(-20, 140), new Vector2(-90, 172), new Vector2(-130, 190)
        };
        private static readonly Vector2 LAKE = new Vector2(-160, 200), LODGE = new Vector2(-30, 200), OUTPOST = new Vector2(160, 45), GROVE = new Vector2(145, -150);
        private const float WATER = 4.8f, LODGE_Y = 22f;

        private static readonly Color[] WALLS = { new Color(0.95f, 0.9f, 0.8f), new Color(0.78f, 0.84f, 0.9f), new Color(0.92f, 0.8f, 0.66f), new Color(0.8f, 0.86f, 0.74f), new Color(0.9f, 0.76f, 0.72f) };
        private static readonly Color[] ROOFS = { new Color(0.36f, 0.22f, 0.18f), new Color(0.26f, 0.3f, 0.28f), new Color(0.3f, 0.3f, 0.34f), new Color(0.45f, 0.28f, 0.2f) };

        private Street _main, _riverside, _upper, _valley, _outpostRoad, _drive;
        private LotInfo _hall, _tower, _mill, _hq, _mess, _armory, _lodge, _guest, _boathouse, _longhouse;
        private readonly System.Collections.Generic.List<LotInfo> _barracks = new System.Collections.Generic.List<LotInfo>();
        private readonly System.Collections.Generic.List<LotInfo> _cabins = new System.Collections.Generic.List<LotInfo>();
        private System.Collections.Generic.List<LotInfo> _town;

        protected override float WaterLevel => WATER;

        protected override float Relief(float x, float z)
        {
            float lRiver = LandShape.PathDistance(RIVER, new Vector2(x, z), out _, out _);
            float h = 6f + lRiver * 0.17f + LandShape.Fbm(x, z, 90f, 4, 1.3f) * 7f * Mathf.Clamp01(lRiver / 40f);
            float lEdge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(150f, 245f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))));
            h += (LandShape.Ridged(x, z, 110f, 4, 7.7f) * 26f + 10f) * lEdge;
            float lSpur = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), new Vector2(150f, 20f)) / 110f);
            return h + 18f * lSpur * lSpur * (3f - 2f * lSpur);
        }

        protected override float Woods(float pX, float pZ, float pHeight) => Mathf.Clamp01(0.7f + LandShape.Fbm(pX, pZ, 60f, 3, 2.2f) * 0.8f);

        protected override string[] TreeSet(float pHeight) => pHeight > 30f ? new[] { "PineA", "PineB", "PineC", "PineB" } : new[] { "PineA", "PineB", "PineC", "BroadleafA", "BirchA", "BroadleafC" };

        // ================================================================ plan

        protected override void Plan()
        {
            Shape.Disc(LAKE, 42f, 1.2f, 26f);
            Shape.Disc(LODGE, 50f, LODGE_Y, 10f);
            Shape.Carve(RIVER, 9f, 2.6f, 6f);

            _valley = Road(7f, Ground.Dirt, new Vector2(40, -250), new Vector2(45, -150), new Vector2(35, -60), new Vector2(45, 20), new Vector2(40, 100), new Vector2(15, 150));
            _riverside = Road(8f, Ground.Asphalt, new Vector2(-35, -200), new Vector2(-45, -120), new Vector2(-50, -40), new Vector2(-40, 30));
            _main = Road(9f, Ground.Asphalt, new Vector2(-95, -195), new Vector2(-105, -110), new Vector2(-110, -30), new Vector2(-100, 45));
            _upper = Road(7f, Ground.Cobble, new Vector2(-150, -150), new Vector2(-160, -80), new Vector2(-165, -10), new Vector2(-150, 55));
            Road(7f, Ground.Asphalt, new Vector2(-45, -120), new Vector2(-105, -110));
            Road(7f, Ground.Asphalt, new Vector2(-50, -40), new Vector2(-110, -30));
            Road(6f, Ground.Cobble, new Vector2(-110, -30), new Vector2(-165, -10));
            Road(6f, Ground.Cobble, new Vector2(-105, -110), new Vector2(-160, -80));
            Road(7f, Ground.Asphalt, new Vector2(-40, 30), new Vector2(-100, 45), new Vector2(-150, 55));
            Road(8f, Ground.Asphalt, new Vector2(-50, -40), new Vector2(-12, -48), new Vector2(35, -60));
            _outpostRoad = Road(7f, Ground.Dirt, new Vector2(45, 20), new Vector2(85, 30), new Vector2(110, 18), new Vector2(118, 40));
            _drive = Road(7f, Ground.Cobble, new Vector2(15, 150), new Vector2(-5, 165), new Vector2(-25, 172));
            Road(4f, Ground.Dirt, new Vector2(45, -150), new Vector2(100, -150), new Vector2(125, -150));
            // Trails to the edges (the starts).
            Road(4f, Ground.Dirt, new Vector2(-244, -100), new Vector2(-200, -120), new Vector2(-150, -150));
            Road(4f, Ground.Dirt, new Vector2(244, -60), new Vector2(200, -100), new Vector2(170, -140));
            Road(4f, Ground.Dirt, new Vector2(100, 244), new Vector2(60, 190), new Vector2(15, 150));

            PlanTown();
            PlanOutpost();
            PlanLodge();
        }

        private (string, int, int, int, Roof, Color, Color, Room[]) Shopfront(int n) =>
            (n % 3 == 1 ? "Suburb" : "Timber", 3 + n % 3, 3, 2 + (n % 4 == 0 ? 1 : 0), n % 2 == 0 ? Roof.Gable : Roof.Hip, WALLS[n % WALLS.Length], ROOFS[n % ROOFS.Length],
                new[] { n % 4 == 0 ? Room.Bar : n % 4 == 1 ? Room.Shop : n % 4 == 2 ? Room.Dining : Room.Office, n % 3 == 0 ? Room.Bathroom : Room.Storage, Room.Bedroom, n % 2 == 0 ? Room.Office : Room.Lounge });

        private (string, int, int, int, Roof, Color, Color, Room[]) Home(int n) =>
            ("Timber", 3 + n % 2, 3, 2, n % 2 == 0 ? Roof.Gable : Roof.Hip, WALLS[(n + 2) % WALLS.Length], ROOFS[(n + 1) % ROOFS.Length],
                new[] { Room.Lounge, Room.Kitchen, Room.Bedroom, n % 3 == 0 ? Room.Bathroom : Room.Office });

        private void PlanTown()
        {
            Plaza(R(-150, -82, -120, -48), Ground.Cobble, Shape.Height(-112f, -65f) + 0.1f, 4f);
            _town = new System.Collections.Generic.List<LotInfo>();
            float lMain = Length(_main.path);
            _town.AddRange(Frontage("Main W", _main, 6f, 100f, 1, 4f, 0, Shopfront));
            _town.AddRange(Frontage("Main W2", _main, 150f, lMain - 6f, 1, 4f, 0, n => Shopfront(n + 5)));
            _town.AddRange(Frontage("Main E", _main, 6f, lMain - 6f, -1, 5f, 0, n => Shopfront(n + 2)));
            _town.AddRange(Frontage("Riverside", _riverside, 8f, Length(_riverside.path) - 8f, 1, 6f, 0, Home));
            _town.AddRange(Frontage("Upper", _upper, 8f, Length(_upper.path) - 8f, 1, 6f, 0, n => Home(n + 3)));

            _hall = Lot("Town Hall", "Classic", new Vector2(-196, -40), 90, 6, 5, 2, Roof.Hip, new Color(0.94f, 0.92f, 0.86f), new Color(0.3f, 0.32f, 0.36f), 0, 0f, Shape.Height(-172f, -30f) + 0.2f);
            _hall.spec.tallWindows = true;
            Doors(_hall.spec, 0, 2, 0, 3, 2, 1, 1, 1);
            Rooms(_hall.spec, Room.Ballroom, Room.Bathroom, Room.Office, Room.Security);
            _tower = Lot("Bell Tower", "Classic", new Vector2(-196, -12), 90, 2, 2, 6, Roof.Hip, new Color(0.9f, 0.88f, 0.82f), new Color(0.3f, 0.32f, 0.36f), 0, 0f, _hall.height);
            Doors(_tower.spec, 0, 0);
            _tower.spec.roofAccess = true;
            _mill = Lot("Sawmill", "Timber", new Vector2(-62, -205), 0, 8, 5, 1, Roof.Gable, new Color(0.62f, 0.45f, 0.32f), new Color(0.32f, 0.3f, 0.3f), 0, 0f, Shape.Height(-40f, -200f) + 0.2f);
            _mill.spec.arches.Add(new Vector2Int(1, 2));
            Doors(_mill.spec, 1, 1, 3, 2, 0, 4);
            Rooms(_mill.spec, Room.Storage, Room.Utility);
        }

        private void PlanOutpost()
        {
            float lTop = Shape.Height(OUTPOST.x, OUTPOST.y);
            Plaza(R(OUTPOST.x - 48, OUTPOST.y - 48, OUTPOST.x + 48, OUTPOST.y + 48), Ground.Dirt, lTop, 14f);
            _hq = Lot("Outpost HQ", "Suburb", OUTPOST + new Vector2(22, 24), 180, 6, 4, 2, Roof.Flat, new Color(0.62f, 0.66f, 0.58f), new Color(0.32f, 0.34f, 0.3f), 1, 0f, lTop + 0.1f);
            _hq.spec.brick = true;
            _hq.spec.basement = true;
            Doors(_hq.spec, 0, 2, 0, 3, 2, 1);
            Rooms(_hq.spec, Room.Security, Room.Office, Room.Office, Room.Bathroom);
            _mess = Lot("Mess Hall", "Timber", OUTPOST + new Vector2(-18, 28), 180, 5, 4, 1, Roof.Gable, new Color(0.6f, 0.62f, 0.52f), new Color(0.3f, 0.32f, 0.3f), 1, 0f, lTop + 0.1f);
            Doors(_mess.spec, 0, 2, 2, 1, 1, 1);
            Rooms(_mess.spec, Room.Dining, Room.Kitchen);
            for (int i = 0; i < 2; i++)
            {
                LotInfo b = Lot("Barracks " + i, "Timber", OUTPOST + new Vector2(-24 + i * 30, -22), 0, 7, 3, 1, Roof.Gable, new Color(0.56f, 0.6f, 0.5f), new Color(0.28f, 0.3f, 0.28f), 1, 0f, lTop + 0.1f);
                Doors(b.spec, 0, 1, 0, 5, 2, 3);
                Rooms(b.spec, Room.Bedroom, Room.Bathroom);
                _barracks.Add(b);
            }
            _armory = Lot("Armory", "Suburb", OUTPOST + new Vector2(32, -20), 270, 3, 3, 1, Roof.Flat, new Color(0.55f, 0.58f, 0.5f), new Color(0.3f, 0.3f, 0.3f), 1, 0f, lTop + 0.1f);
            Doors(_armory.spec, 0, 1);
            Rooms(_armory.spec, Room.Storage, Room.None);

            float lShelf = Shape.Height(GROVE.x, GROVE.y);
            Plaza(R(GROVE.x - 30, GROVE.y - 30, GROVE.x + 30, GROVE.y + 30), Ground.ForestFloor, lShelf, 12f);
            _longhouse = Lot("Longhouse", "Timber", GROVE, 90, 7, 3, 1, Roof.Gable, new Color(0.58f, 0.42f, 0.3f), new Color(0.3f, 0.26f, 0.22f), 1, 0f, lShelf + 0.1f);
            Doors(_longhouse.spec, 0, 3, 2, 3);
            Rooms(_longhouse.spec, Room.Ballroom, Room.None);
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + 20f) * Mathf.Deg2Rad;
                Vector2 c = GROVE + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 24f;
                LotInfo lCabin = Lot("Cabin " + i, "Timber", c, Mathf.Round(Mathf.Atan2(c.x - GROVE.x, c.y - GROVE.y) * Mathf.Rad2Deg / 90f) * 90f, 3, 3, 1, Roof.Gable, new Color(0.6f, 0.46f, 0.32f), new Color(0.28f, 0.26f, 0.22f), 1, 0f, lShelf + 0.1f);
                Doors(lCabin.spec, 2, 1);
                Rooms(lCabin.spec, i % 2 == 0 ? Room.Bedroom : Room.Kitchen, Room.None);
                _cabins.Add(lCabin);
            }
        }

        private void PlanLodge()
        {
            Plaza(R(LODGE.x - 40, LODGE.y - 42, LODGE.x + 40, LODGE.y - 18), Ground.Cobble, LODGE_Y + 0.1f, 6f);
            _lodge = Lot("Lakeside Lodge", "Classic", LODGE + new Vector2(0, 4), 180, 9, 6, 3, Roof.Hip, new Color(0.95f, 0.92f, 0.84f), new Color(0.28f, 0.3f, 0.34f), 2, 0f, LODGE_Y + 0.1f);
            _lodge.spec.tallWindows = true;
            _lodge.spec.basement = true;
            _lodge.spec.balconyChance = 0.5f;
            Doors(_lodge.spec, 0, 4, 0, 5, 2, 2, 1, 2, 3, 3);
            _lodge.spec.climb = new Vector2Int(3, 1);
            Rooms(_lodge.spec, Room.Ballroom, Room.Bar, Room.Lounge, Room.Bathroom, Room.Office, Room.Bedroom);
            _guest = Lot("Guest House", "Suburb", LODGE + new Vector2(38, 14), 270, 4, 3, 2, Roof.Gable, new Color(0.86f, 0.8f, 0.7f), new Color(0.3f, 0.3f, 0.34f), 2, 0f, LODGE_Y + 0.1f);
            Doors(_guest.spec, 0, 1, 2, 2);
            Rooms(_guest.spec, Room.Lounge, Room.Kitchen, Room.Bedroom, Room.Bathroom);
            _boathouse = Lot("Boathouse", "Timber", new Vector2(-104, 196), 270, 4, 3, 1, Roof.Gable, new Color(0.55f, 0.4f, 0.28f), new Color(0.26f, 0.24f, 0.22f), 2, 0f, WATER + 0.9f);
            Doors(_boathouse.spec, 0, 1, 2, 1);
            Rooms(_boathouse.spec, Room.Storage, Room.None);
        }

        // ================================================================ dress

        protected override void Dress()
        {
            Waters();
            DressTown();
            DressOutpost();
            DressLodge();
            DressWilds();
            Points();
        }

        private void Waters()
        {
            K.Water(new Vector3(LAKE.x, WATER, LAKE.y), new Vector2(110f, 90f));
            for (int s = 0; s < RIVER.Length - 1; s++)
            {
                Vector2 a = RIVER[s], b = RIVER[s + 1], d = (b - a).normalized;
                GameObject lPlane = K.Water(new Vector3((a.x + b.x) * 0.5f, WATER, (a.y + b.y) * 0.5f), new Vector2(11f, Vector2.Distance(a, b) + 6f));
                lPlane.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
            }
            // The stone bridge over the river (the causeway's parapets and arches).
            Vector3 lBridge = G(-12f, -48f, 0f);
            for (int s = -1; s <= 1; s += 2)
                K.Block(K.Env, "BridgeParapet", lBridge + new Vector3(s * 4.6f, 0.55f, 0), new Vector3(0.6f, 1.1f, 22f), STONE, Core.GameLayers.DEFAULT, true, Quaternion.Euler(0, 75f, 0), true);
        }

        private void DressTown()
        {
            const int Z = 0;
            foreach (Street s in new[] { _main, _riverside, _upper }) Dress(s, new Color(1f, 0.78f, 0.5f), s == _main);
            for (int i = 0; i < _town.Count; i++)
            {
                LotInfo l = _town[i];
                Staff(l, Z, i % 3 == 0 ? 2 : 1, i % 2 == 0 ? ItemKind.Pistol : ItemKind.Smg, i % 4 == 0, i % 5 == 0 ? ItemKind.Shotgun : ItemKind.Pistol);
            }
            // The festival square: stage, stalls, the crowd, the speech.
            Vector3 lSquare = G(-135f, -65f, 0f);
            K.Block(K.Props, "Stage", lSquare + new Vector3(-10f, 0.5f, 0), new Vector3(5f, 1f, 9f), new Color(0.55f, 0.4f, 0.28f), Core.GameLayers.DEFAULT, true, null, true);
            K.Activity(ActivityType.Speech, lSquare + new Vector3(-10f, 1.1f, 0), 90f, Z, 20f);
            for (int i = 0; i < 4; i++) K.Prop("MarketStall", lSquare + new Vector3(-2f + (i % 2) * 9f, 0, -12f + (i / 2) * 24f), i < 2 ? 0f : 180f, null, MapKit.Collide.Box, true);
            K.Crowd(lSquare + new Vector3(-10f, 0, 0), 4f, 11f, 22, Z);
            K.DrinksOn(lSquare + new Vector3(6f, 0.85f, 10f), 0, Z, 3);
            K.Prop("Table", lSquare + new Vector3(6f, 0, 10f), 0);
            Routines(Z, new Vector2(-135f, -65f), 14f);

            // Town hall, the bell tower (sniper nest, rocket launcher on top), the sawmill.
            Staff(_hall, Z, 3, ItemKind.Smg, true, ItemKind.Shotgun);
            K.Npc(NpcRole.Lookout, In(_tower, 0, 5, 0), 90f, Z, ItemKind.Sniper);
            K.UniqueWeapon(ItemKind.RocketLauncher, In(_tower, 0.8f, 6, 0.8f, 0.3f), "Bell tower roof");
            Staff(_mill, Z, 3, ItemKind.Shotgun, true, ItemKind.Smg);
            K.UniqueWeapon(ItemKind.Flamethrower, In(_mill, -8f, 0, 4f, 0.2f), "Sawmill store room");
            for (int i = 0; i < 6; i++) K.Prop("Barrel", In(_mill, -10f + i * 1.1f, 0, -9.5f, 0f), 0);
            K.Prop("Log", G(-40f, -214f, 0.3f), 0, null, MapKit.Collide.Box, true);

            Squad(Z, 4, ItemKind.Smg, 4f, new Vector2(-104, -180), new Vector2(-108, -100), new Vector2(-110, -30), new Vector2(-102, 30));
            Squad(Z, 3, ItemKind.Pistol, 3f, new Vector2(-45, -190), new Vector2(-48, -100), new Vector2(-47, 20));
            Squad(Z, 2, ItemKind.Shotgun, 5f, new Vector2(-152, -140), new Vector2(-163, -40), new Vector2(-152, 45));
            Squad(Z, 3, ItemKind.Smg, 3f, new Vector2(-60, -42), new Vector2(-12, -48), new Vector2(30, -58));
            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-115, 25, -75), new Vector3(190, 90, 280), G(-95, -190), new Color(0.26f, 0.32f, 0.46f), HatStyle.Cap, In(_hall, 3f, 1, 0));
        }

        private void DressOutpost()
        {
            const int Z = 1;
            Vector3 c = G(OUTPOST, 0f);
            Color lWall = new Color(0.5f, 0.48f, 0.42f);
            // The perimeter: a 3 m wall with a gate on the west, towers at the corners.
            for (int s = 0; s < 4; s++)
            {
                Quaternion r = Quaternion.Euler(0, s * 90f, 0);
                Vector3 lMid = c + r * new Vector3(0, 1.5f, 46f);
                if (s == 3)
                {
                    K.Block(K.Env, "Wall", c + r * new Vector3(-27f, 1.5f, 46f), new Vector3(38f, 3f, 0.6f), lWall, Core.GameLayers.DEFAULT, true, r, true);
                    K.Block(K.Env, "Wall", c + r * new Vector3(27f, 1.5f, 46f), new Vector3(38f, 3f, 0.6f), lWall, Core.GameLayers.DEFAULT, true, r, true);
                }
                else K.Block(K.Env, "Wall", lMid, new Vector3(92f, 3f, 0.6f), lWall, Core.GameLayers.DEFAULT, true, r, true);
                Vector3 lCorner = c + r * new Vector3(43f, 0, 43f);
                Watchtower(Z, lCorner.x, lCorner.z, 7f, s * 90f + 45f, new Color(0.45f, 0.4f, 0.32f));
            }
            Staff(_hq, Z, 4, ItemKind.Smg, true, ItemKind.Sniper);
            Staff(_mess, Z, 3, ItemKind.Pistol, false, ItemKind.Smg);
            foreach (LotInfo b in _barracks) Staff(b, Z, 3, ItemKind.Smg, true, ItemKind.Shotgun);
            Staff(_armory, Z, 1, ItemKind.Shotgun, true, ItemKind.Smg);
            K.UniqueWeapon(ItemKind.RocketLauncher, In(_armory, 0, 0, 2f, 0.2f), "Outpost armory");
            K.Alarm(c + new Vector3(-8f, 0, 8f), 0);
            K.Prop("Generator", c + new Vector3(4f, 0, -36f), 0, null, MapKit.Collide.Box, true);
            for (int i = 0; i < 4; i++) K.Prop("ShippingContainer", c + new Vector3(-36f + i * 7f, 0, 8f), 90f, new Color(0.3f, 0.4f, 0.3f), MapKit.Collide.Box, true);
            for (int i = 0; i < 3; i++) K.Prop("Jeep", c + new Vector3(10f + i * 5f, 0, 2f), 0, new Color(0.35f, 0.4f, 0.3f), MapKit.Collide.Box, true);
            K.Prop("Sandbags", c + new Vector3(-46f, 0, 5f), 90, null, MapKit.Collide.Box, true);
            Guard(Z, OUTPOST.x - 49f, OUTPOST.y + 4f, 270f, ItemKind.Smg);
            Guard(Z, OUTPOST.x - 49f, OUTPOST.y - 4f, 270f, ItemKind.Shotgun);
            K.Block(K.Props, "Helipad", c + new Vector3(30f, 0.05f, 2f), new Vector3(14f, 0.1f, 14f), new Color(0.3f, 0.3f, 0.32f));
            K.Point(MatchPoint.Kind.Extraction, c + new Vector3(30f, 0.2f, 2f), 0, "Outpost helipad");
            Squad(Z, 5, ItemKind.Smg, 3f, OUTPOST + new Vector2(-38, -38), OUTPOST + new Vector2(38, -38), OUTPOST + new Vector2(38, 38), OUTPOST + new Vector2(-38, 38));
            Squad(Z, 3, ItemKind.Smg, 4f, new Vector2(85, 30), new Vector2(110, 18), new Vector2(60, 25));
            K.Crowd(c + new Vector3(-18f, 0, 14f), 2.5f, 6f, 8, Z);
            K.Activity(ActivityType.Speech, c + new Vector3(-18f, 0.1f, 14f), 0f, Z, 16f);

            // The Grove: the ceremony in the longhouse, cabins, the greenhouse corner, the fire circle.
            Staff(_longhouse, Z, 3, ItemKind.Pistol, true, ItemKind.Shotgun);
            K.Crowd(In(_longhouse, 0, 0, 0, 0f), 2f, 5f, 10, Z);
            foreach (LotInfo lCabin in _cabins) Staff(lCabin, Z, 1, ItemKind.Pistol, false);
            K.UniqueWeapon(ItemKind.Flamethrower, In(_cabins[2], 0, 0, 2.5f, 0.2f), "Grove cabin");
            K.Lamp(G(GROVE + new Vector2(0, -14), 0.3f), new Color(1f, 0.6f, 0.3f), 10f, 2f, false);
            Squad(Z, 3, ItemKind.Pistol, 4f, GROVE + new Vector2(-28, -28), GROVE + new Vector2(28, -28), GROVE + new Vector2(28, 28), GROVE + new Vector2(-28, 28));
            Routines(Z, GROVE + new Vector2(0, -16), 9f);
            Routines(Z, OUTPOST + new Vector2(-10, -5), 10f);
            K.Zone(Z, K.Map.zoneNames[1], new Vector3(150, 45, -50), new Vector3(170, 100, 290), G(45, 20), new Color(0.32f, 0.38f, 0.26f), HatStyle.Beanie, In(_hq, 0, 1, 0));
        }

        private void DressLodge()
        {
            const int Z = 2;
            Staff(_lodge, Z, 8, ItemKind.Smg, true, ItemKind.Shotgun);
            for (int f = 0; f < 3; f++) K.Sprinklers(In(_lodge, 0, f, 0, 0f), new Vector3(_lodge.built.W, MapKit.STOREY, _lodge.built.D), _lodge.yaw, MapKit.STOREY - 0.05f);
            K.VentKit(_lodge.built, In(_lodge, 0, -1, -6f, 0f), _lodge.yaw);
            K.UniqueWeapon(ItemKind.RocketLauncher, In(_lodge, 9f, 2, 5f, 0.2f), "Lodge attic office");
            Staff(_guest, Z, 2, ItemKind.Pistol, true);
            Staff(_boathouse, Z, 1, ItemKind.Shotgun, true);
            K.UniqueWeapon(ItemKind.Flamethrower, In(_boathouse, 2f, 0, 1f, 0.2f), "Boathouse");

            // The garden party on the terrace: tables, the toast, the crowd.
            Vector3 lParty = G(LODGE.x, LODGE.y - 30f, 0f);
            for (int i = 0; i < 4; i++) K.Prop("UmbrellaTable", lParty + new Vector3(-15f + i * 10f, 0, -4f), 0, new Color(0.9f, 0.9f, 0.86f), MapKit.Collide.Box, true);
            K.Crowd(lParty, 3f, 12f, 18, Z);
            K.Activity(ActivityType.Speech, lParty + new Vector3(0, 0.1f, 6f), 180f, Z, 18f);
            K.DrinksOn(lParty + new Vector3(12f, 1.1f, 6f), 0, Z, 4);
            K.Prop("BarCounter", lParty + new Vector3(12f, 0, 6f), 0, null, MapKit.Collide.Box, true);
            Routines(Z, new Vector2(LODGE.x + 25f, LODGE.y - 25f), 8f);
            Squad(Z, 3, ItemKind.Smg, 4f, LODGE + new Vector2(-40, -42), LODGE + new Vector2(40, -42), LODGE + new Vector2(40, 30), LODGE + new Vector2(-40, 30));
            Squad(Z, 2, ItemKind.Pistol, 3f, new Vector2(15, 150), new Vector2(-5, 165), new Vector2(-25, 172));
            Guard(Z, 12f, 152f, 200f, ItemKind.Smg);
            Guard(Z, 18f, 150f, 200f, ItemKind.Shotgun);

            // The pier into the lake: the boat extraction.
            Vector3 lPier = new Vector3(-112f, WATER + 0.6f, 196f);
            for (int i = 0; i < 6; i++) K.Prop("PierSegment", lPier + new Vector3(-i * 3f, 0, 0), 90f, null, MapKit.Collide.Box);
            K.Prop("Boat", lPier + new Vector3(-17f, -0.6f, 3f), 90f, new Color(0.9f, 0.9f, 0.88f), MapKit.Collide.Box);
            K.Point(MatchPoint.Kind.Extraction, lPier + new Vector3(-15f, 0.3f, 0), 270, "Lake boat");
            LandStairs(new Vector2(-70f, 195f), new Vector2(-96f, 196f));
            K.Zone(Z, K.Map.zoneNames[2], new Vector3(LODGE.x, 30, LODGE.y), new Vector3(170, 80, 120), G(15, 150), new Color(0.22f, 0.24f, 0.32f), HatStyle.TopHat, In(_lodge, 6f, 2, 3f));
        }

        private void DressWilds()
        {
            // Patrols on the valley road, a ranger post, hunters' stands, stashes in the woods.
            Squad(1, 3, ItemKind.Smg, 4f, new Vector2(42, -200), new Vector2(40, -100), new Vector2(40, 0), new Vector2(42, 90));
            Squad(2, 2, ItemKind.Pistol, 5f, new Vector2(100, 240), new Vector2(60, 190), new Vector2(20, 155));
            Watchtower(0, -200f, -120f, 6f, 45f, new Color(0.5f, 0.4f, 0.3f));
            Watchtower(1, 90f, -60f, 6f, 270f, new Color(0.5f, 0.4f, 0.3f));
            Watchtower(2, 40f, 220f, 6f, 180f, new Color(0.5f, 0.4f, 0.3f));
            Vector2[] lStashes = { new Vector2(-210, 40), new Vector2(-60, 90), new Vector2(80, 140), new Vector2(210, 120), new Vector2(90, -220), new Vector2(-200, -200), new Vector2(200, -200), new Vector2(-80, 120) };
            for (int i = 0; i < lStashes.Length; i++) K.Weapon(i % 3 == 0 ? ItemKind.Sniper : i % 3 == 1 ? ItemKind.Smg : ItemKind.Shotgun, G(lStashes[i], 0.2f));
            K.Gadget(ItemKind.C4, G(-30f, -195f, 0.2f));
            K.Gadget(ItemKind.Poison, G(-118f, -60f, 0.2f));
            K.Gadget(ItemKind.SmokeGrenade, G(70f, 28f, 0.2f));
            K.Gadget(ItemKind.EmeticPoison, G(LODGE.x + 10f, LODGE.y - 40f, 0.2f));
            K.UniqueWeapon(ItemKind.RocketLauncher, G(-205f, -125f, 6.3f), "Hunting tower");
            K.UniqueWeapon(ItemKind.Flamethrower, G(90f, -150f, 0.2f), "Grove trail woodshed");
        }

        private void Points()
        {
            K.Point(MatchPoint.Kind.Start, G(-234f, 70f, 0.2f), 107, "West trail");
            K.Point(MatchPoint.Kind.Start, G(236f, -66f, 0.2f), 240, "East trail");
            K.Point(MatchPoint.Kind.Start, G(190f, 234f, 0.2f), 219, "North trail");
            K.Point(MatchPoint.Kind.Start, G(-180f, -234f, 0.2f), 38, "South woods");
            K.Point(MatchPoint.Kind.Extraction, G(-95f, -192f, 0.2f), 180, "Logging truck");
            K.Point(MatchPoint.Kind.Extraction, G(-238f, -98f, 0.2f), 270, "Old mine trail");
        }
    }
}
