using System.Collections.Generic;
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
    /// <b>Mirage Bay</b> - a Gulf city of 500 m climbing from the sea.
    /// <list type="bullet">
    /// <item><b>Old Souk</b> (zone 0): a hill town on the west, tight alleys stepping down to the
    /// harbour, flat roofs linked by planks, the Wind Clock tower at the top, the covered market.</item>
    /// <item><b>Smugglers' Docks</b> (zone 1): the quay at sea level - container lanes, warehouses,
    /// gantry cranes, the cargo ship - under the highway viaduct.</item>
    /// <item><b>The Zenith</b> (zone 2): the hotel tower on the north-east promontory, its podium
    /// (lobby ballroom, bar, kitchen), the pool deck party and the rooftop helipad.</item>
    /// </list>
    /// </summary>
    public class MapGulf : HillMap
    {
        protected override string MapFile => "Map_Gulf";

        [MenuItem("Goobyr/Build/Map Gulf")]
        public static void BuildMenu() => new MapGulf().Build();

        private const float SEA = 0f, QUAY = 2.2f, ZENITH_Y = 40f;
        private static readonly Vector2 ZENITH = new Vector2(150, 160), CLOCK = new Vector2(-150, 40);
        private static readonly float[] LANES_X = { -200f, -160f, -120f, -80f };
        private static readonly float[] ALLEYS_Z = { -120f, -80f, -40f, 0f, 40f };
        private static readonly Color[] STUCCO = { new Color(0.96f, 0.9f, 0.78f), new Color(0.93f, 0.84f, 0.7f), new Color(0.98f, 0.94f, 0.86f), new Color(0.9f, 0.8f, 0.68f), new Color(0.95f, 0.87f, 0.74f) };
        private static readonly Color FLAT = new Color(0.82f, 0.76f, 0.66f);

        private readonly List<LotInfo> _souk = new List<LotInfo>();
        private readonly List<LotInfo> _warehouses = new List<LotInfo>();
        private LotInfo _clock, _market, _tower, _podium, _customs;

        protected override float WaterLevel => SEA;

        private static float Coast(float x) => -168f + 14f * Mathf.Sin(x / 60f);

        protected override float Relief(float x, float z)
        {
            float lCoast = Coast(x);
            if (z < lCoast - 12f) return -6f;
            float lInland = Mathf.Clamp01((z - lCoast + 12f) / 24f);
            float h = Mathf.Lerp(-6f, QUAY, lInland) + Mathf.Max(0f, z - lCoast - 12f) * 0.11f;
            h += LandShape.Fbm(x, z, 80f, 4, 4.4f) * 4f * Mathf.Clamp01((z - lCoast) / 60f);
            float lHill = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), new Vector2(-150f, -20f)) / 140f);
            h += 26f * lHill * lHill * (3f - 2f * lHill);
            float lPromontory = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), ZENITH) / 120f);
            h += 14f * lPromontory * lPromontory;
            float lEdge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(170f, 245f, Mathf.Max(Mathf.Abs(x), z)));
            return h + (LandShape.Ridged(x, z, 90f, 3, 3.3f) * 18f + 6f) * lEdge;
        }

        protected override float Woods(float pX, float pZ, float pHeight) => Mathf.Clamp01(0.3f + LandShape.Fbm(pX, pZ, 50f, 3, 6.1f) * 0.6f);

        protected override string[] TreeSet(float pHeight) => pHeight < 8f ? new[] { "PalmA", "PalmB", "PalmA" } : new[] { "PalmA", "PalmB", "CypressA", "BroadleafB" };

        protected override void NaturalGround(float pX, float pZ, float pHeight, float pSlope, float[] pWeights)
        {
            float n = LandShape.Fbm(pX, pZ, 30f, 3, 1.1f);
            pWeights[(int)Ground.Sand] = 1.2f + n + Mathf.Clamp01((8f - pHeight) / 6f) * 3f;
            pWeights[(int)Ground.DryGrass] = Mathf.Clamp01(0.5f - n) * 1.2f;
            pWeights[(int)Ground.Dirt] = Mathf.Clamp01(n) * 0.6f;
        }

        // ================================================================ plan

        protected override void Plan()
        {
            Shape.Disc(ZENITH, 58f, ZENITH_Y, 16f);
            Plaza(R(20, -165, 235, -95), Ground.Asphalt, QUAY, 10f);
            Road(10f, Ground.Asphalt, new Vector2(-245, -150), new Vector2(-150, -150), new Vector2(-60, -148), new Vector2(15, -150), new Vector2(240, -150));
            Road(9f, Ground.Asphalt, new Vector2(20, -148), new Vector2(40, -80), new Vector2(80, -20), new Vector2(120, 30), new Vector2(110, 80), new Vector2(125, 110));
            Road(8f, Ground.Asphalt, new Vector2(-60, -148), new Vector2(-50, -60), new Vector2(-45, 30), new Vector2(0, 90), new Vector2(60, 110), new Vector2(105, 112));
            Road(4f, Ground.Dirt, new Vector2(-244, 100), new Vector2(-150, 80), new Vector2(-80, 60));
            Road(4f, Ground.Dirt, new Vector2(244, 20), new Vector2(180, 0), new Vector2(120, 30));
            Road(4f, Ground.Dirt, new Vector2(30, 244), new Vector2(10, 160), new Vector2(0, 90));

            // The souk: north-south lanes stepping down the hill, east-west alleys crossing them.
            foreach (float x in LANES_X) Road(4.5f, Ground.Cobble, new Vector2(x, -140), new Vector2(x + 3f, -60), new Vector2(x - 2f, 20), new Vector2(x, 60));
            foreach (float z in ALLEYS_Z) Road(3.5f, Ground.Cobble, new Vector2(-222, z), new Vector2(-60, z + 2f));
            PlanSouk();
            PlanDocks();
            PlanZenith();
        }

        private (string, int, int, int, Roof, Color, Color, Room[]) SoukHouse(int n) =>
            ("Stucco", 3, 3 + n % 2, 2 + (n % 3 == 0 ? 1 : 0), Roof.Flat, STUCCO[n % STUCCO.Length], FLAT,
                new[] { n % 5 == 0 ? Room.Bar : n % 5 == 1 ? Room.Shop : n % 5 == 2 ? Room.Dining : n % 5 == 3 ? Room.Storage : Room.Lounge, n % 4 == 0 ? Room.Bathroom : Room.None, Room.Bedroom, Room.Office });

        private void PlanSouk()
        {
            int k = 0;
            for (int l = 0; l < LANES_X.Length; l++)
            {
                Street lLane = Streets[Streets.Count - LANES_X.Length - ALLEYS_Z.Length + l];
                float lLength = Length(lLane.path);
                // Segments between the alleys (the lane runs from z = -140).
                for (int a = -1; a < ALLEYS_Z.Length; a++)
                {
                    float z0 = a < 0 ? -140f : ALLEYS_Z[a] + 4f, z1 = a + 1 < ALLEYS_Z.Length ? ALLEYS_Z[a + 1] - 4f : 60f;
                    float d0 = z0 + 140f, d1 = Mathf.Min(lLength, z1 + 140f);
                    if (d1 - d0 < 10f) continue;
                    int lBase = k;
                    _souk.AddRange(Frontage("Souk " + l + "." + a + "W", lLane, d0, d1, 1, 1f, 0, n => SoukHouse(lBase + n)));
                    if (l < LANES_X.Length - 1) _souk.AddRange(Frontage("Souk " + l + "." + a + "E", lLane, d0, d1, -1, 1f, 0, n => SoukHouse(lBase + n + 3)));
                    k += 7;
                }
            }
            _clock = Lot("Wind Clock", "Stucco", CLOCK + new Vector2(20, 6), 0, 2, 2, 8, Roof.Flat, new Color(0.9f, 0.82f, 0.68f), FLAT, 0, 0.2f);
            Doors(_clock.spec, 0, 0);
            _market = Lot("Covered Market", "Stucco", new Vector2(-100, -100), 0, 7, 5, 1, Roof.Flat, new Color(0.94f, 0.88f, 0.76f), FLAT, 0, 0.2f);
            for (int m = 0; m < 7; m++) _market.spec.arches.Add(new Vector2Int(0, m));
            Doors(_market.spec, 2, 3, 1, 2, 3, 2);
            Rooms(_market.spec, Room.Shop, Room.Bar);
            _customs = Lot("Harbour Customs", "Stucco", new Vector2(-30, -125), 0, 5, 4, 3, Roof.Flat, new Color(0.88f, 0.86f, 0.8f), FLAT, 0, 0.2f);
            Doors(_customs.spec, 0, 2, 2, 1);
            Rooms(_customs.spec, Room.Office, Room.Security, Room.Office, Room.Bathroom, Room.Lounge, Room.Storage);
        }

        private void PlanDocks()
        {
            for (int i = 0; i < 3; i++)
            {
                LotInfo w = Lot("Warehouse " + i, "Suburb", new Vector2(70 + i * 55, -75), 180, 8, 5, 1, Roof.Flat, new Color(0.62f, 0.6f, 0.55f), new Color(0.4f, 0.4f, 0.42f), 1, 0f, Shape.Height(70 + i * 55, -95) + 0.1f);
                w.spec.brick = i == 1;
                w.spec.arches.Add(new Vector2Int(0, 3));
                w.spec.arches.Add(new Vector2Int(0, 4));
                Doors(w.spec, 2, 1, 1, 2);
                Rooms(w.spec, i == 1 ? Room.Office : Room.Storage, i == 0 ? Room.Bathroom : Room.Utility);
                _warehouses.Add(w);
            }
        }

        private void PlanZenith()
        {
            _podium = Lot("Zenith Podium", "Classic", ZENITH + new Vector2(0, -22), 180, 10, 6, 2, Roof.Flat, new Color(0.95f, 0.93f, 0.88f), new Color(0.6f, 0.58f, 0.55f), 2, 0f, ZENITH_Y + 0.1f);
            _podium.spec.tallWindows = true;
            _podium.spec.basement = true;
            Doors(_podium.spec, 0, 4, 0, 5, 2, 2, 1, 2, 3, 2);
            Rooms(_podium.spec, Room.Ballroom, Room.Bar, Room.Dining, Room.Kitchen, Room.Gym, Room.Bathroom);
            _tower = Lot("Zenith Tower", "Classic", ZENITH + new Vector2(0, 14), 180, 6, 6, 10, Roof.Flat, new Color(0.88f, 0.9f, 0.94f), new Color(0.5f, 0.52f, 0.56f), 2, 0f, ZENITH_Y + 0.1f);
            _tower.spec.tallWindows = true;
            _tower.spec.roofAccess = true;
            _tower.spec.balconyChance = 0.3f;
            Doors(_tower.spec, 0, 2, 0, 3, 2, 2);
            Rooms(_tower.spec, Room.Lounge, Room.Security, Room.Bedroom, Room.Bathroom, Room.Bedroom, Room.Lounge, Room.Bar, Room.Lounge, Room.Bedroom, Room.Office,
                Room.Bedroom, Room.Bathroom, Room.Bedroom, Room.Lounge, Room.Office, Room.Bedroom, Room.Lounge, Room.Bar, Room.Office, Room.Bedroom);
            Plaza(R(ZENITH.x - 45, ZENITH.y - 52, ZENITH.x + 45, ZENITH.y - 34), Ground.Cobble, ZENITH_Y + 0.1f, 4f);
        }

        // ================================================================ dress

        protected override void Dress()
        {
            K.Water(new Vector3(0, SEA, -215f), new Vector2(520f, 110f));
            DressSouk();
            DressDocks();
            DressZenith();
            DressBetween();
        }

        private void DressSouk()
        {
            const int Z = 0;
            for (int i = 0; i < _souk.Count; i++)
                if (i % 2 == 0) Staff(_souk[i], Z, 1, i % 4 == 0 ? ItemKind.Smg : ItemKind.Pistol, i % 6 == 0, i % 3 == 0 ? ItemKind.Shotgun : ItemKind.Pistol);
            // The rooftop web: planks across the lanes from roof to roof.
            for (int i = 0; i + 1 < _souk.Count; i++)
            {
                LotInfo a = _souk[i], b = _souk[i + 1];
                if (Vector2.Distance(a.center, b.center) > 26f || i % 3 != 0) continue;
                Vector3 pa = new Vector3(a.center.x, a.height + a.spec.floors * MapKit.STOREY + 0.1f, a.center.y), pb = new Vector3(b.center.x, b.height + b.spec.floors * MapKit.STOREY + 0.1f, b.center.y);
                Vector3 d = pb - pa;
                K.Block(K.Env, "RoofPlank", (pa + pb) * 0.5f, new Vector3(1.2f, 0.12f, d.magnitude), new Color(0.55f, 0.42f, 0.3f), Core.GameLayers.DEFAULT, true, Quaternion.LookRotation(d));
            }
            // Awnings, lanterns and stalls along the lanes.
            foreach (Street lLane in Streets)
            {
                if (lLane.ground != Ground.Cobble || lLane.width > 4.6f || lLane.width < 4.4f) continue;
                Dress(lLane, new Color(1f, 0.7f, 0.4f), false, 16f);
                for (float d = 12f; d < Length(lLane.path) - 12f; d += 34f)
                {
                    Vector2 p = PointAt(lLane.path, d, out Vector2 dir);
                    if (!Built(p.x + 3f, p.y)) continue;
                    K.Prop("MarketStall", G(p + Vector2.Perpendicular(dir) * 1.2f, 0f), Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f, null, MapKit.Collide.Box, true);
                }
            }
            Staff(_clock, Z, 2, ItemKind.Pistol, true);
            K.Npc(NpcRole.Lookout, In(_clock, 0, 7, 0), 180f, Z, ItemKind.Sniper);
            K.UniqueWeapon(ItemKind.RocketLauncher, In(_clock, 0.8f, 8, 0.8f, 0.3f), "Wind Clock roof");
            Staff(_market, Z, 4, ItemKind.Smg, true, ItemKind.Shotgun);
            K.Crowd(In(_market, 0, 0, -2f, 0f), 2f, 8f, 16, Z);
            K.Activity(ActivityType.Speech, In(_market, 0, 0, 3f), 180f, Z, 16f);
            Staff(_customs, Z, 4, ItemKind.Smg, true, ItemKind.Sniper);
            K.UniqueWeapon(ItemKind.Flamethrower, In(_customs, -5f, 0, 3f, 0.2f), "Customs evidence room");
            Routines(Z, new Vector2(-100, -128), 10f);
            Squad(Z, 4, ItemKind.Smg, 4f, new Vector2(-200, -130), new Vector2(-198, -40), new Vector2(-200, 50));
            Squad(Z, 3, ItemKind.Pistol, 3f, new Vector2(-120, -130), new Vector2(-118, -40), new Vector2(-120, 50));
            Squad(Z, 3, ItemKind.Smg, 4f, new Vector2(-215, -80), new Vector2(-65, -78));
            Squad(Z, 2, ItemKind.Shotgun, 3f, new Vector2(-215, 0), new Vector2(-65, 2));
            Squad(Z, 3, ItemKind.Pistol, 4f, new Vector2(-230, -150), new Vector2(-60, -148), new Vector2(10, -150));
            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-140, 25, -45), new Vector3(190, 90, 220), G(-230, -150), new Color(0.55f, 0.4f, 0.26f), HatStyle.Turban, In(_customs, 0, 2, 0));
        }

        private void DressDocks()
        {
            const int Z = 1;
            foreach (LotInfo w in _warehouses) Staff(w, Z, 4, ItemKind.Smg, true, ItemKind.Shotgun);
            // The deal in the middle warehouse.
            K.Activity(ActivityType.Inspect, In(_warehouses[1], 0, 0, 2f), 0, Z);
            K.Crowd(In(_warehouses[1], 0, 0, 0, 0f), 1.5f, 4f, 6, Z);
            // Container lanes on the quay, stacked two high, with ladders.
            Color[] lColors = { new Color(0.7f, 0.2f, 0.16f), new Color(0.15f, 0.35f, 0.55f), new Color(0.2f, 0.5f, 0.35f), new Color(0.85f, 0.6f, 0.2f), new Color(0.5f, 0.5f, 0.52f) };
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 8; c++)
                {
                    if ((r + c) % 5 == 2) continue;
                    Vector3 p = new Vector3(45f + c * 22f, QUAY, -118f - r * 12f);
                    K.Prop("ShippingContainer", p, 0f, lColors[(r * 3 + c) % lColors.Length], MapKit.Collide.Box, true);
                    if ((r + c) % 3 == 0) K.Prop("ShippingContainer", p + Vector3.up * 2.6f, 0f, lColors[(r + c * 2) % lColors.Length], MapKit.Collide.Box, true);
                    if ((r + c) % 3 == 0 && c % 2 == 0) K.Climbable(K.Env, p + new Vector3(3.2f, 2.6f, 0), new Vector3(0.2f, 5.2f, 0.8f), Quaternion.Euler(0, 90, 0), new Color(0.4f, 0.4f, 0.42f), true);
                }
            // Gantry cranes on the quay edge, a lookout in each cab.
            for (int i = 0; i < 2; i++)
            {
                Vector3 c = new Vector3(80f + i * 90f, QUAY, -160f);
                for (int l = 0; l < 4; l++) K.Block(K.Env, "CraneLeg", c + new Vector3(l % 2 == 0 ? -6f : 6f, 9f, l < 2 ? -4f : 4f), new Vector3(0.8f, 18f, 0.8f), new Color(0.9f, 0.62f, 0.16f));
                K.Block(K.Env, "CraneBeam", c + new Vector3(0, 18.5f, -8f), new Vector3(14f, 1.4f, 30f), new Color(0.9f, 0.62f, 0.16f));
                K.Block(K.Env, "CraneCab", c + new Vector3(0, 16.5f, 0), new Vector3(3f, 2.6f, 3f), new Color(0.3f, 0.3f, 0.32f));
                K.Climbable(K.Env, c + new Vector3(-6.5f, 9f, -4f), new Vector3(0.2f, 18f, 0.8f), Quaternion.identity, new Color(0.4f, 0.4f, 0.42f), true);
                K.Npc(NpcRole.Lookout, c + new Vector3(0, 19.4f, 3f), 0, Z, ItemKind.Sniper);
            }
            K.UniqueWeapon(ItemKind.RocketLauncher, new Vector3(170f, QUAY + 19.4f, -164f), "Crane walkway");
            // The cargo ship at the quay, its deck reached by the gangway.
            Vector3 lShip = new Vector3(150f, SEA, -190f);
            K.Block(K.Env, "Hull", lShip + new Vector3(0, 2f, 0), new Vector3(70f, 6f, 14f), new Color(0.2f, 0.25f, 0.32f), Core.GameLayers.DEFAULT, true);
            K.Block(K.Env, "Deck", lShip + new Vector3(0, 5.1f, 0), new Vector3(68f, 0.3f, 13f), new Color(0.5f, 0.45f, 0.4f));
            K.Block(K.Env, "Bridge", lShip + new Vector3(28f, 8.5f, 0), new Vector3(9f, 6.5f, 11f), new Color(0.92f, 0.92f, 0.9f), Core.GameLayers.DEFAULT, true, null, true);
            K.Block(K.Env, "Gangway", new Vector3(150f, (QUAY + SEA + 5.2f) * 0.5f + 0.3f, -174f), new Vector3(2f, 0.2f, 10f), new Color(0.5f, 0.5f, 0.52f), Core.GameLayers.DEFAULT, true, Quaternion.Euler(-14f, 0, 0));
            for (int i = 0; i < 3; i++) K.Npc(NpcRole.Guard, lShip + new Vector3(-20f + i * 15f, 5.3f, 0), 90f * i, Z, ItemKind.Smg);
            K.Point(MatchPoint.Kind.Extraction, lShip + new Vector3(-30f, 5.4f, 0), 270, "Cargo ship");
            // The highway viaduct over the docks.
            for (float x = -10f; x < 240f; x += 24f) K.Block(K.Env, "Pillar", new Vector3(x, QUAY + 6f, -52f), new Vector3(2.4f, 12f, 2.4f), new Color(0.7f, 0.68f, 0.64f), Core.GameLayers.DEFAULT, true, null, true);
            K.Block(K.Env, "Viaduct", new Vector3(115f, QUAY + 12.6f, -52f), new Vector3(260f, 1.2f, 16f), new Color(0.6f, 0.6f, 0.62f));
            K.Alarm(new Vector3(60f, QUAY, -100f), 0);
            // The dockers' canteen under the viaduct: tables, drinks, the shift break.
            Vector3 lCanteen = new Vector3(20f, QUAY, -66f);
            K.Prop("TentMilitary", lCanteen, 0, new Color(0.6f, 0.55f, 0.45f), MapKit.Collide.Box);
            K.Prop("Table", lCanteen + new Vector3(0, 0, 0.5f), 0);
            K.DrinksOn(lCanteen + new Vector3(0, 0.85f, 0.5f), 0, Z, 4);
            K.Activity(ActivityType.Dining, lCanteen + new Vector3(0, 0.1f, -1f), 0, Z);
            K.Crowd(lCanteen, 2f, 5f, 6, Z);
            Squad(Z, 5, ItemKind.Smg, 3f, new Vector2(40, -105), new Vector2(220, -105), new Vector2(220, -150), new Vector2(40, -150));
            Squad(Z, 3, ItemKind.Shotgun, 4f, new Vector2(50, -95), new Vector2(210, -95));
            Squad(Z, 2, ItemKind.Smg, 5f, new Vector2(40, -80), new Vector2(80, -20), new Vector2(120, 30));
            Routines(Z, new Vector2(120, -105), 9f);
            K.Zone(Z, K.Map.zoneNames[1], new Vector3(130, 10, -110), new Vector3(220, 60, 140), G(235, -150), new Color(0.3f, 0.3f, 0.32f), HatStyle.Beanie, In(_warehouses[1], 4f, 0, 2f));
        }

        private void DressZenith()
        {
            const int Z = 2;
            Staff(_podium, Z, 8, ItemKind.Smg, true, ItemKind.Shotgun);
            for (int f = 0; f < 2; f++) K.Sprinklers(In(_podium, 0, f, 0, 0f), new Vector3(_podium.built.W, MapKit.STOREY, _podium.built.D), _podium.yaw, MapKit.STOREY - 0.05f);
            K.VentKit(_podium.built, In(_podium, 0, -1, -5f, 0f), _podium.yaw);
            Staff(_tower, Z, 14, ItemKind.Smg, true, ItemKind.Sniper);
            for (int f = 0; f < 10; f++) K.Sprinklers(In(_tower, 0, f, 0, 0f), new Vector3(_tower.built.W, MapKit.STOREY, _tower.built.D), _tower.yaw, MapKit.STOREY - 0.05f);
            K.UniqueWeapon(ItemKind.Flamethrower, In(_podium, 12f, 0, 6f, 0.2f), "Zenith kitchen");
            K.UniqueWeapon(ItemKind.RocketLauncher, In(_tower, 4f, 9, 4f, 0.2f), "Penthouse office");
            // The helipad on the tower roof: the helicopter extraction, guarded.
            Vector3 lRoof = In(_tower, 0, 10, 0, 0.05f);
            K.Block(K.Props, "Helipad", lRoof, new Vector3(12f, 0.1f, 12f), new Color(0.3f, 0.3f, 0.32f));
            K.Point(MatchPoint.Kind.Extraction, lRoof + Vector3.up * 0.2f, 0, "Zenith helipad");
            K.Npc(NpcRole.Guard, lRoof + new Vector3(5f, 0.1f, 5f), 45f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Lookout, lRoof + new Vector3(-5f, 0.1f, -5f), 225f, Z, ItemKind.Sniper);
            // The pool deck party: the pool, loungers, the toast, the crowd.
            Vector3 lDeck = G(ZENITH.x, ZENITH.y - 43f, 0f);
            K.Block(K.Props, "Pool", lDeck + new Vector3(-20f, -0.2f, 0), new Vector3(18f, 0.4f, 8f), new Color(0.3f, 0.65f, 0.85f));
            for (int i = 0; i < 4; i++) K.Prop("UmbrellaTable", lDeck + new Vector3(4f + i * 8f, 0, 0), 0, new Color(0.95f, 0.95f, 0.92f), MapKit.Collide.Box, true);
            K.Crowd(lDeck + new Vector3(10f, 0, 0), 3f, 13f, 20, Z);
            K.Activity(ActivityType.Speech, lDeck + new Vector3(10f, 0.1f, 6f), 180f, Z, 18f);
            K.DrinksOn(lDeck + new Vector3(30f, 1.1f, 4f), 0, Z, 4);
            K.Prop("BarCounter", lDeck + new Vector3(30f, 0, 4f), 0, null, MapKit.Collide.Box, true);
            Routines(Z, new Vector2(ZENITH.x + 30f, ZENITH.y + 30f), 9f);
            Squad(Z, 4, ItemKind.Smg, 3f, ZENITH + new Vector2(-50, -55), ZENITH + new Vector2(50, -55), ZENITH + new Vector2(50, 45), ZENITH + new Vector2(-50, 45));
            Squad(Z, 2, ItemKind.Pistol, 4f, new Vector2(120, 30), new Vector2(110, 80), new Vector2(125, 110));
            Guard(Z, ZENITH.x - 4f, ZENITH.y - 58f, 180f, ItemKind.Smg);
            Guard(Z, ZENITH.x + 4f, ZENITH.y - 58f, 180f, ItemKind.Smg);
            K.Zone(Z, K.Map.zoneNames[2], new Vector3(ZENITH.x, 60, ZENITH.y), new Vector3(140, 120, 140), G(105, 112), new Color(0.2f, 0.22f, 0.3f), HatStyle.None, In(_tower, 2f, 9, 2f));
        }

        private void DressBetween()
        {
            Watchtower(1, 230f, -60f, 7f, 225f, new Color(0.55f, 0.5f, 0.42f));
            Watchtower(0, -230f, 80f, 6f, 135f, new Color(0.6f, 0.52f, 0.42f));
            Watchtower(2, 60f, 200f, 6f, 180f, new Color(0.6f, 0.55f, 0.45f));
            Squad(1, 2, ItemKind.Smg, 4f, new Vector2(-50, -60), new Vector2(-45, 30), new Vector2(0, 90), new Vector2(60, 110));
            Vector2[] lStashes = { new Vector2(-30, 60), new Vector2(40, 40), new Vector2(200, 60), new Vector2(-220, 140), new Vector2(0, 200), new Vector2(230, 200), new Vector2(-100, 150), new Vector2(160, -20) };
            for (int i = 0; i < lStashes.Length; i++) K.Weapon(i % 3 == 0 ? ItemKind.Sniper : i % 3 == 1 ? ItemKind.Smg : ItemKind.Shotgun, G(lStashes[i], 0.2f));
            K.Gadget(ItemKind.C4, G(30f, -140f, 0.2f));
            K.Gadget(ItemKind.Poison, G(-100f, -95f, 0.2f));
            K.Gadget(ItemKind.SmokeGrenade, G(-160f, -30f, 0.2f));
            K.Gadget(ItemKind.EmeticPoison, G(ZENITH.x - 10f, ZENITH.y - 48f, 0.2f));
            K.UniqueWeapon(ItemKind.Flamethrower, G(200f, 62f, 0.2f), "Desert road shack");
            K.Point(MatchPoint.Kind.Start, G(-234f, 180f, 0.2f), 128, "Caravan trail");
            K.Point(MatchPoint.Kind.Start, G(236f, 18f, 0.2f), 260, "Desert road");
            K.Point(MatchPoint.Kind.Start, G(-20f, 234f, 0.2f), 175, "North track");
            K.Point(MatchPoint.Kind.Start, G(-150f, -234f, 0.2f), 33, "South dunes");
            K.Point(MatchPoint.Kind.Extraction, G(232f, -150f, 0.2f), 90, "Corniche car");
            K.Point(MatchPoint.Kind.Extraction, G(-238f, 92f, 0.2f), 270, "Caravan trail truck");
        }
    }
}
