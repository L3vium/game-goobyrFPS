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
    /// <b>Elmridge</b> - an American hill suburb of 500 m at dusk.
    /// <list type="bullet">
    /// <item><b>Downtown Strip</b> (zone 0): Main Street and Ash Avenue on the south-west slope -
    /// brick shops with flats above, the police station, the Starlight cinema, the town meeting in
    /// the square, the water tower.</item>
    /// <item><b>Junkyard Bowl</b> (zone 1): the bikers' lair in the hollow by the creek - stacked
    /// wrecks, the magnet crane, the Lucky Ten bowling alley, the chop shop, the tall grass.</item>
    /// <item><b>Hilltop Mansion</b> (zone 2): the Whitlock estate on its walled hilltop, reached by
    /// the winding residential lanes: gatehouse, mansion, pool house, greenhouse, garden party,
    /// helipad.</item>
    /// </list>
    /// </summary>
    public class MapSuburb : HillMap
    {
        protected override string MapFile => "Map_Suburb";

        [MenuItem("Goobyr/Build/Map Suburb")]
        public static void BuildMenu() => new MapSuburb().Build();

        private static readonly Vector2 MANSION = new Vector2(-95, 180), YARD = new Vector2(145, -150);
        private static readonly Vector2[] CREEK = { new Vector2(210, 250), new Vector2(150, 160), new Vector2(110, 60), new Vector2(150, -40), new Vector2(200, -120), new Vector2(230, -250) };
        private const float HILLTOP = 36f, HOLLOW = -2f, CREEK_WATER = -1.2f;
        private static readonly Color[] HOUSES = { new Color(0.96f, 0.94f, 0.88f), new Color(0.72f, 0.82f, 0.9f), new Color(0.95f, 0.85f, 0.68f), new Color(0.8f, 0.88f, 0.76f), new Color(0.92f, 0.78f, 0.72f), new Color(0.76f, 0.74f, 0.84f) };
        private static readonly Color[] ROOFS = { new Color(0.32f, 0.33f, 0.38f), new Color(0.48f, 0.26f, 0.22f), new Color(0.28f, 0.36f, 0.32f), new Color(0.22f, 0.24f, 0.3f) };
        private static readonly Color BRICK = new Color(0.72f, 0.42f, 0.32f), FLAT = new Color(0.42f, 0.42f, 0.44f);

        private Street _main, _ash, _maple, _oak;
        private readonly List<LotInfo> _downtown = new List<LotInfo>();
        private readonly List<LotInfo> _homes = new List<LotInfo>();
        private LotInfo _police, _cinema, _bowling, _chop, _mansion, _gatehouse, _poolhouse, _greenhouse;

        protected override float WaterLevel => CREEK_WATER;

        protected override float Relief(float x, float z)
        {
            float h = 6f - x * 0.04f + LandShape.Fbm(x, z, 110f, 4, 2.6f) * 9f;
            float lHill = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, z), MANSION) / 150f);
            h += 22f * lHill * lHill * (3f - 2f * lHill);
            float lEdge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(165f, 245f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))));
            return h + (LandShape.Ridged(x, z, 100f, 3, 5.5f) * 16f + 8f) * lEdge;
        }

        protected override float Woods(float pX, float pZ, float pHeight) => Mathf.Clamp01(0.5f + LandShape.Fbm(pX, pZ, 55f, 3, 8.4f) * 0.7f);

        protected override string[] TreeSet(float pHeight) => new[] { "BroadleafA", "BroadleafC", "BirchA", "BroadleafB", "PineA", "BroadleafA" };

        // ================================================================ plan

        protected override void Plan()
        {
            Shape.Disc(MANSION, 52f, HILLTOP, 14f);
            Shape.Disc(YARD, 62f, HOLLOW, 24f);
            Shape.Carve(CREEK, 7f, 2.4f, 6f);

            _main = Road(11f, Ground.Asphalt, new Vector2(-245, -150), new Vector2(-140, -150), new Vector2(-40, -145), new Vector2(50, -142), new Vector2(95, -150));
            _ash = Road(10f, Ground.Asphalt, new Vector2(-130, -240), new Vector2(-130, -150), new Vector2(-126, -70));
            _maple = Road(8f, Ground.Asphalt, new Vector2(-126, -70), new Vector2(-50, -45), new Vector2(-125, 0), new Vector2(-50, 40), new Vector2(-118, 82), new Vector2(-95, 118));
            _oak = Road(8f, Ground.Asphalt, new Vector2(-50, -45), new Vector2(40, -30), new Vector2(80, 30), new Vector2(30, 90), new Vector2(-50, 40));
            Road(6f, Ground.Dirt, new Vector2(95, -150), new Vector2(120, -135));
            Road(4f, Ground.Dirt, new Vector2(-244, 60), new Vector2(-170, 45), new Vector2(-125, 0));
            Road(4f, Ground.Dirt, new Vector2(244, -40), new Vector2(190, -60), new Vector2(130, -100));
            Road(4f, Ground.Dirt, new Vector2(60, 244), new Vector2(40, 160), new Vector2(30, 90));
            Plaza(R(-160, -128, -132, -104), Ground.Cobble, null);

            PlanDowntown();
            PlanResidential();
            PlanJunkyard();
            PlanMansion();
        }

        private (string, int, int, int, Roof, Color, Color, Room[]) Shop(int n) =>
            ("Suburb", 3 + n % 2, 3, 2 + (n % 3 == 0 ? 1 : 0), Roof.Flat, n % 3 == 1 ? new Color(0.9f, 0.86f, 0.78f) : BRICK * (0.9f + (n % 4) * 0.05f), FLAT,
                new[] { n % 5 == 0 ? Room.Dining : n % 5 == 1 ? Room.Shop : n % 5 == 2 ? Room.Bar : n % 5 == 3 ? Room.Office : Room.Shop, n % 3 == 0 ? Room.Bathroom : Room.Storage, Room.Bedroom, Room.Lounge });

        private (string, int, int, int, Roof, Color, Color, Room[]) Home(int n) =>
            ("Suburb", 3 + n % 2, 3, 2, n % 3 == 0 ? Roof.Hip : Roof.Gable, HOUSES[n % HOUSES.Length], ROOFS[n % ROOFS.Length],
                new[] { Room.Lounge, Room.Kitchen, Room.Bedroom, n % 2 == 0 ? Room.Bathroom : Room.Office });

        private void PlanDowntown()
        {
            int lBase = 0;
            foreach ((float from, float to) in new[] { (8f, 95f), (125f, 230f) })
            {
                int b = lBase;
                _downtown.AddRange(Frontage("Main N" + from, _main, from, to, 1, 2f, 0, n => Shop(b + n)));
                _downtown.AddRange(Frontage("Main S" + from, _main, from, to, -1, 2f, 0, n => Shop(b + n + 3)));
                lBase += 8;
            }
            _downtown.AddRange(Frontage("Ash W", _ash, 6f, 76f, -1, 2f, 0, n => Shop(n + 20)));
            _downtown.AddRange(Frontage("Ash E", _ash, 6f, 76f, 1, 2f, 0, n => Shop(n + 25)));
            _police = Lot("Police Station", "Suburb", new Vector2(-30, -112), 180, 6, 4, 2, Roof.Flat, new Color(0.82f, 0.82f, 0.8f), FLAT, 0, 0f, Shape.Height(-30f, -145f) + 0.2f);
            _police.spec.brick = true;
            Doors(_police.spec, 0, 2, 0, 3, 2, 1);
            Rooms(_police.spec, Room.Security, Room.Office, Room.Office, Room.Storage);
            _cinema = Lot("Starlight Cinema", "Classic", new Vector2(-80, -184), 0, 7, 6, 2, Roof.Flat, new Color(0.62f, 0.2f, 0.22f), FLAT, 0, 0f, Shape.Height(-80f, -150f) + 0.2f);
            _cinema.spec.roofAccess = true;
            Doors(_cinema.spec, 0, 3, 2, 1, 1, 2);
            Rooms(_cinema.spec, Room.Ballroom, Room.Bar, Room.Office, Room.Bathroom);
        }

        private void PlanResidential()
        {
            int k = 0;
            for (int s = 0; s < _maple.path.Length - 1; s++)
            {
                Street lLeg = new Street { path = new[] { _maple.path[s], _maple.path[s + 1] }, width = _maple.width, ground = _maple.ground };
                float lLength = Length(lLeg.path);
                int b = k;
                _homes.AddRange(Frontage("Maple " + s + "L", lLeg, 14f, lLength - 14f, 1, 8f, s < 3 ? 0 : 2, n => Home(b + n)));
                _homes.AddRange(Frontage("Maple " + s + "R", lLeg, 14f, lLength - 14f, -1, 8f, s < 3 ? 0 : 2, n => Home(b + n + 2)));
                k += 5;
            }
            for (int s = 0; s < _oak.path.Length - 1; s++)
            {
                Street lLeg = new Street { path = new[] { _oak.path[s], _oak.path[s + 1] }, width = _oak.width, ground = _oak.ground };
                int b = k;
                _homes.AddRange(Frontage("Oak " + s, lLeg, 14f, Length(lLeg.path) - 14f, s % 2 == 0 ? 1 : -1, 9f, 1, n => Home(b + n)));
                k += 4;
            }
        }

        private void PlanJunkyard()
        {
            Plaza(R(YARD.x - 55, YARD.y - 50, YARD.x + 40, YARD.y + 45), Ground.Dirt, HOLLOW, 10f);
            _bowling = Lot("Lucky Ten Lanes", "Suburb", YARD + new Vector2(-30, 30), 180, 10, 6, 1, Roof.Flat, new Color(0.88f, 0.62f, 0.4f), FLAT, 1, 0f, HOLLOW + 0.1f);
            _bowling.spec.brick = true;
            Doors(_bowling.spec, 0, 4, 0, 5, 2, 2, 1, 2);
            _bowling.spec.climb = new Vector2Int(3, 3);
            Rooms(_bowling.spec, Room.Bar, Room.Gym);
            _chop = Lot("Hart's Chop Shop", "Suburb", YARD + new Vector2(22, 32), 180, 5, 4, 1, Roof.Flat, new Color(0.6f, 0.62f, 0.66f), FLAT, 1, 0f, HOLLOW + 0.1f);
            _chop.spec.arches.Add(new Vector2Int(0, 1));
            _chop.spec.arches.Add(new Vector2Int(0, 3));
            Doors(_chop.spec, 2, 2);
            Rooms(_chop.spec, Room.Storage, Room.Office);
        }

        private void PlanMansion()
        {
            Plaza(R(MANSION.x - 46, MANSION.y - 50, MANSION.x + 46, MANSION.y - 26), Ground.Cobble, HILLTOP + 0.1f, 4f);
            _mansion = Lot("Whitlock Mansion", "Classic", MANSION + new Vector2(0, 6), 180, 9, 6, 3, Roof.Hip, new Color(0.97f, 0.96f, 0.92f), new Color(0.24f, 0.26f, 0.3f), 2, 0f, HILLTOP + 0.1f);
            _mansion.spec.tallWindows = true;
            _mansion.spec.basement = true;
            _mansion.spec.balconyChance = 0.5f;
            Doors(_mansion.spec, 0, 4, 0, 5, 2, 2, 1, 2, 3, 3);
            _mansion.spec.climb = new Vector2Int(1, 1);
            Rooms(_mansion.spec, Room.Ballroom, Room.Bar, Room.Dining, Room.Kitchen, Room.Office, Room.Bedroom);
            _gatehouse = Lot("Gatehouse", "Classic", MANSION + new Vector2(10, -62), 180, 2, 2, 1, Roof.Hip, new Color(0.92f, 0.9f, 0.86f), new Color(0.24f, 0.26f, 0.3f), 2);
            Doors(_gatehouse.spec, 0, 0, 2, 1);
            Rooms(_gatehouse.spec, Room.Security, Room.None);
            _poolhouse = Lot("Pool House", "Suburb", MANSION + new Vector2(38, 18), 270, 4, 3, 1, Roof.Flat, new Color(0.9f, 0.92f, 0.94f), FLAT, 2, 0f, HILLTOP + 0.1f);
            Doors(_poolhouse.spec, 0, 1, 2, 2);
            Rooms(_poolhouse.spec, Room.Bar, Room.Bathroom);
            _greenhouse = Lot("Greenhouse", "Classic", MANSION + new Vector2(-38, 22), 90, 4, 3, 1, Roof.Gable, new Color(0.8f, 0.92f, 0.86f), new Color(0.7f, 0.8f, 0.76f), 2, 0f, HILLTOP + 0.1f);
            Doors(_greenhouse.spec, 0, 1);
            Rooms(_greenhouse.spec, Room.Storage, Room.None);
        }

        // ================================================================ dress

        protected override void Dress()
        {
            for (int s = 0; s < CREEK.Length - 1; s++)
            {
                Vector2 a = CREEK[s], b = CREEK[s + 1], d = (b - a).normalized;
                float y = Mathf.Min(K.GroundAt(a.x, a.y), K.GroundAt(b.x, b.y)) + 0.6f;
                GameObject lPlane = K.Water(new Vector3((a.x + b.x) * 0.5f, Mathf.Max(CREEK_WATER, y), (a.y + b.y) * 0.5f), new Vector2(8f, Vector2.Distance(a, b) + 6f));
                lPlane.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
            }
            DressDowntown();
            DressResidential();
            DressJunkyard();
            DressMansion();
            Points();
        }

        private void DressDowntown()
        {
            const int Z = 0;
            Dress(_main, new Color(1f, 0.68f, 0.38f), true, 18f);
            Dress(_ash, new Color(1f, 0.68f, 0.38f), true, 18f);
            for (int i = 0; i < _downtown.Count; i++) Staff(_downtown[i], Z, i % 3 == 0 ? 2 : 1, i % 2 == 0 ? ItemKind.Pistol : ItemKind.Smg, i % 4 == 0, i % 4 == 1 ? ItemKind.Shotgun : ItemKind.Pistol);
            Staff(_police, Z, 5, ItemKind.Smg, true, ItemKind.Shotgun);
            K.UniqueWeapon(ItemKind.RocketLauncher, In(_police, 6f, 0, 3f, 0.2f), "Police armory");
            Staff(_cinema, Z, 3, ItemKind.Pistol, true);
            K.Crowd(In(_cinema, 0, 0, 2f, 0f), 2f, 6f, 14, Z);
            K.Npc(NpcRole.Lookout, In(_cinema, 3f, 2, 3f), 0, Z, ItemKind.Sniper);
            Watchtower(Z, -175f, -110f, 14f, 90f, new Color(0.6f, 0.62f, 0.66f));
            K.UniqueWeapon(ItemKind.Flamethrower, G(-60f, -128f, 0.2f), "Gas station stock room");
            K.Prop("PoliceCar", G(-38f, -142f, 0f), 90f, null, MapKit.Collide.Box, true);
            // The town meeting in the square by Ash Avenue.
            Vector3 lSquare = G(-146f, -116f, 0f);
            K.Crowd(lSquare, 3f, 10f, 18, Z);
            K.Activity(ActivityType.Speech, lSquare + new Vector3(-10f, 0.1f, 0), 90f, Z, 18f);
            Routines(Z, new Vector2(-146f, -116f), 11f);
            Squad(Z, 4, ItemKind.Smg, 3f, new Vector2(-235, -150), new Vector2(-140, -150), new Vector2(-40, -145), new Vector2(50, -142));
            Squad(Z, 3, ItemKind.Pistol, 4f, new Vector2(-130, -230), new Vector2(-130, -150), new Vector2(-126, -75));
            Squad(Z, 2, ItemKind.Shotgun, 3f, new Vector2(-100, -128), new Vector2(-20, -125));
            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-110, 20, -150), new Vector3(240, 70, 170), G(-235, -150), new Color(0.22f, 0.32f, 0.55f), HatStyle.Cap, In(_police, 0, 1, 0));
        }

        private void DressResidential()
        {
            Dress(_maple, new Color(1f, 0.75f, 0.45f), true, 24f);
            Dress(_oak, new Color(1f, 0.75f, 0.45f), true, 24f);
            for (int i = 0; i < _homes.Count; i++)
            {
                LotInfo h = _homes[i];
                Staff(h, h.spec.zone, i % 3 == 0 ? 1 : 0, ItemKind.Pistol, i % 5 == 0, i % 4 == 0 ? ItemKind.Shotgun : ItemKind.Pistol);
                // Front yards: a mailbox, flowers; a backyard table in some.
                Vector3 lFront = In(h, 0, 0, -h.built.D * 0.5f - 3.2f, 0f);
                K.Prop("Mailbox", lFront + Quaternion.Euler(0, h.yaw, 0) * new Vector3(3f, 0, 0), h.yaw + 180f, ROOFS[i % ROOFS.Length]);
                if (i % 2 == 0) K.NaturePiece("FlowerBed", lFront + Quaternion.Euler(0, h.yaw, 0) * new Vector3(-3f, 0, 0), h.yaw, 1f, new Color(0.9f, 0.5f, 0.6f), MapKit.Collide.None);
                if (i % 4 == 1) { K.Prop("Table", In(h, 0, 0, h.built.D * 0.5f + 3f, 0f), h.yaw); K.Activity(ActivityType.Dining, In(h, 1.2f, 0, h.built.D * 0.5f + 3f), h.yaw, h.spec.zone); }
            }
            Squad(0, 3, ItemKind.Pistol, 4f, new Vector2(-120, -60), new Vector2(-55, -42), new Vector2(-120, 0), new Vector2(-55, 40));
            Squad(1, 3, ItemKind.Smg, 4f, new Vector2(-45, -40), new Vector2(40, -30), new Vector2(80, 30), new Vector2(30, 88));
            Squad(2, 2, ItemKind.Smg, 3f, new Vector2(-55, 42), new Vector2(-115, 82), new Vector2(-95, 116));
        }

        private void DressJunkyard()
        {
            const int Z = 1;
            Vector3 y = G(YARD, 0f);
            Color[] lWrecks = { new Color(0.55f, 0.33f, 0.22f), new Color(0.45f, 0.5f, 0.55f), new Color(0.7f, 0.6f, 0.3f), new Color(0.4f, 0.45f, 0.35f), new Color(0.62f, 0.28f, 0.25f) };
            // Lanes of stacked wrecks.
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 6; c++)
                {
                    if ((r * 6 + c) % 7 == 3) continue;
                    Vector3 p = y + new Vector3(-50f + c * 9f, 0, -40f + r * 11f);
                    for (int s = 0; s < 1 + (r + c) % 3; s++) K.Car(p + Vector3.up * (s * 1.5f), (r * 40 + c * 25 + s * 70) % 360, lWrecks[(r + c + s) % lWrecks.Length]);
                }
            // The magnet crane, the crusher, the lookout.
            K.Block(K.Env, "CraneMast", y + new Vector3(20f, 8f, -30f), new Vector3(1.2f, 16f, 1.2f), new Color(0.85f, 0.65f, 0.15f));
            K.Block(K.Env, "CraneBoom", y + new Vector3(10f, 16f, -30f), new Vector3(22f, 1f, 1f), new Color(0.85f, 0.65f, 0.15f));
            K.Climbable(K.Env, y + new Vector3(20.8f, 8f, -30f), new Vector3(0.2f, 16f, 0.8f), Quaternion.Euler(0, 90, 0), new Color(0.4f, 0.4f, 0.42f), true);
            K.Npc(NpcRole.Lookout, y + new Vector3(20f, 16.6f, -30f), 270f, Z, ItemKind.Sniper);
            K.UniqueWeapon(ItemKind.RocketLauncher, y + new Vector3(19f, 16.6f, -29f), "Crane top");
            K.Block(K.Env, "Crusher", y + new Vector3(30f, 1.5f, 0f), new Vector3(6f, 3f, 4f), new Color(0.4f, 0.4f, 0.42f), Core.GameLayers.DEFAULT, true, null, true);
            K.Grass(G(YARD + new Vector2(-30, -48), 0f), new Vector2(40f, 10f), new Color(0.62f, 0.62f, 0.34f));
            Staff(_bowling, Z, 6, ItemKind.Smg, true, ItemKind.Shotgun);
            K.Crowd(In(_bowling, -6f, 0, 0, 0f), 2f, 6f, 12, Z);
            K.Activity(ActivityType.Inspect, In(_bowling, 10f, 0, 3f), 0, Z);
            K.ToiletKit(In(_bowling, 13f, 0, 7f, 0f), _bowling.yaw + 180f, Z);
            Staff(_chop, Z, 3, ItemKind.Shotgun, true, ItemKind.Smg);
            K.UniqueWeapon(ItemKind.Flamethrower, In(_chop, -5f, 0, 3f, 0.2f), "Chop shop");
            K.Alarm(y + new Vector3(-10f, 0, 40f), 0);
            Squad(Z, 5, ItemKind.Smg, 3f, YARD + new Vector2(-52, -45), YARD + new Vector2(36, -45), YARD + new Vector2(36, 18), YARD + new Vector2(-52, 18));
            Squad(Z, 3, ItemKind.Shotgun, 4f, new Vector2(60, -140), new Vector2(95, -150), new Vector2(120, -135));
            Squad(Z, 2, ItemKind.Smg, 4f, new Vector2(190, -60), new Vector2(130, -100));
            Routines(Z, YARD + new Vector2(-15, -10), 8f);
            K.Zone(Z, K.Map.zoneNames[1], new Vector3(YARD.x, 10, YARD.y), new Vector3(150, 50, 140), G(244, -40), new Color(0.3f, 0.28f, 0.26f), HatStyle.Beanie, In(_bowling, 0, 0, 5f));
            K.Point(MatchPoint.Kind.Extraction, G(232f, -230f, 0.2f), 180, "Creek culvert");
        }

        private void DressMansion()
        {
            const int Z = 2;
            Vector3 c = G(MANSION, 0f);
            // The estate wall around the hilltop, the gate on the south.
            for (int s = 0; s < 4; s++)
            {
                Quaternion r = Quaternion.Euler(0, s * 90f, 0);
                if (s == 2)
                {
                    K.Block(K.Env, "EstateWall", c + r * new Vector3(-30f, 1.2f, 52f), new Vector3(44f, 2.4f, 0.6f), STONE, Core.GameLayers.DEFAULT, true, r, true);
                    K.Block(K.Env, "EstateWall", c + r * new Vector3(32f, 1.2f, 52f), new Vector3(40f, 2.4f, 0.6f), STONE, Core.GameLayers.DEFAULT, true, r, true);
                }
                else K.Block(K.Env, "EstateWall", c + r * new Vector3(0, 1.2f, 52f), new Vector3(104f, 2.4f, 0.6f), STONE, Core.GameLayers.DEFAULT, true, r, true);
            }
            Staff(_mansion, Z, 9, ItemKind.Smg, true, ItemKind.Shotgun);
            for (int f = 0; f < 3; f++) K.Sprinklers(In(_mansion, 0, f, 0, 0f), new Vector3(_mansion.built.W, MapKit.STOREY, _mansion.built.D), _mansion.yaw, MapKit.STOREY - 0.05f);
            K.VentKit(_mansion.built, In(_mansion, 0, -1, -6f, 0f), _mansion.yaw);
            K.UniqueWeapon(ItemKind.RocketLauncher, In(_mansion, -9f, 2, 5f, 0.2f), "Mansion study");
            Staff(_gatehouse, Z, 1, ItemKind.Smg, true);
            Staff(_poolhouse, Z, 2, ItemKind.Pistol, false);
            Staff(_greenhouse, Z, 1, ItemKind.Pistol, false);
            K.UniqueWeapon(ItemKind.Flamethrower, In(_greenhouse, 2f, 0, 1f, 0.2f), "Greenhouse");
            // The garden party on the lawn, the pool, the helipad.
            Vector3 lLawn = G(MANSION.x, MANSION.y - 38f, 0f);
            for (int i = 0; i < 4; i++) K.Prop("UmbrellaTable", lLawn + new Vector3(-18f + i * 12f, 0, 0), 0, new Color(0.95f, 0.95f, 0.92f), MapKit.Collide.Box, true);
            K.Crowd(lLawn, 3f, 13f, 20, Z);
            K.Activity(ActivityType.Speech, lLawn + new Vector3(0, 0.1f, 8f), 180f, Z, 18f);
            K.DrinksOn(lLawn + new Vector3(20f, 1.1f, 6f), 0, Z, 4);
            K.Prop("BarCounter", lLawn + new Vector3(20f, 0, 6f), 0, null, MapKit.Collide.Box, true);
            K.Block(K.Props, "Pool", c + new Vector3(38f, -0.15f, -6f), new Vector3(9f, 0.3f, 16f), new Color(0.3f, 0.65f, 0.85f));
            K.Block(K.Props, "Helipad", c + new Vector3(-30f, 0.05f, 40f), new Vector3(12f, 0.1f, 12f), new Color(0.3f, 0.3f, 0.32f));
            K.Point(MatchPoint.Kind.Extraction, c + new Vector3(-30f, 0.2f, 40f), 0, "Whitlock helipad");
            Routines(Z, MANSION + new Vector2(30, -34), 8f);
            Squad(Z, 4, ItemKind.Smg, 3f, MANSION + new Vector2(-48, -48), MANSION + new Vector2(48, -48), MANSION + new Vector2(48, 48), MANSION + new Vector2(-48, 48));
            Squad(Z, 3, ItemKind.Smg, 4f, MANSION + new Vector2(-40, -20), MANSION + new Vector2(40, -20));
            Guard(Z, MANSION.x + 4f, MANSION.y - 54f, 180f, ItemKind.Smg);
            Guard(Z, MANSION.x - 4f, MANSION.y - 54f, 180f, ItemKind.Shotgun);
            K.Zone(Z, K.Map.zoneNames[2], new Vector3(MANSION.x, 40, MANSION.y), new Vector3(120, 70, 120), G(-95, 118), new Color(0.2f, 0.22f, 0.32f), HatStyle.TopHat, In(_mansion, 6f, 2, 3f));
        }

        private void Points()
        {
            Vector2[] lStashes = { new Vector2(-200, 120), new Vector2(0, 160), new Vector2(120, 120), new Vector2(200, 60), new Vector2(-20, 0), new Vector2(60, -80), new Vector2(-210, -40), new Vector2(160, 200) };
            for (int i = 0; i < lStashes.Length; i++) K.Weapon(i % 3 == 0 ? ItemKind.Sniper : i % 3 == 1 ? ItemKind.Smg : ItemKind.Shotgun, G(lStashes[i], 0.2f));
            K.Gadget(ItemKind.C4, G(110f, -140f, 0.2f));
            K.Gadget(ItemKind.Poison, G(-110f, -158f, 0.2f));
            K.Gadget(ItemKind.SmokeGrenade, G(-60f, 20f, 0.2f));
            K.Gadget(ItemKind.EmeticPoison, G(MANSION.x + 15f, MANSION.y - 40f, 0.2f));
            Watchtower(1, 190f, 40f, 6f, 270f, new Color(0.5f, 0.42f, 0.32f));
            Watchtower(2, -200f, 150f, 6f, 90f, new Color(0.5f, 0.42f, 0.32f));
            K.Point(MatchPoint.Kind.Start, G(-236f, 58f, 0.2f), 100, "West woods trail");
            K.Point(MatchPoint.Kind.Start, G(236f, -42f, 0.2f), 260, "East trail");
            K.Point(MatchPoint.Kind.Start, G(58f, 236f, 0.2f), 190, "North trail");
            K.Point(MatchPoint.Kind.Start, G(-130f, -234f, 0.2f), 0, "Ash Avenue south");
            K.Point(MatchPoint.Kind.Extraction, G(-238f, -150f, 0.2f), 270, "Main Street car");
            K.Point(MatchPoint.Kind.Extraction, G(56f, 230f, 0.2f), 0, "North trail truck");
        }
    }
}
