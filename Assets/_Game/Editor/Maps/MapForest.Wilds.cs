using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Between the zones: the logging roads (and their patrol cars), the ranger station and its
    /// fire lookout, the campsite on the terrace, the quarry pit with the old mine, the logging camp
    /// south of the river, clearings of tall grass, the forest itself (clustered by a density field,
    /// walls of pines on the ridges, birches by the water, rocks and cliff faces), the start points.
    /// </summary>
    public partial class MapForest
    {
        private static readonly Vector2 FARM = new Vector2(-250, -345);
        private static readonly Vector2 RANGER = new Vector2(150, 58), CAMP = new Vector2(-190, 205), QUARRY = new Vector2(-350, 368), LOGCAMP = new Vector2(60, -360);
        private const float QUARRY_RIM = 20f, QUARRY_FLOOR = 12f;

        private static readonly Vector3[] CLEARINGS =
        {
            new Vector3(-150, 50, 26), new Vector3(240, 110, 24), new Vector3(250, -5, 22), new Vector3(-300, 225, 24), new Vector3(130, -215, 26),
            new Vector3(-20, 395, 20), new Vector3(-240, 330, 18), new Vector3(330, 180, 22), new Vector3(-30, -330, 22), new Vector3(200, 20, 18)
        };

        private void PlanWilds()
        {
            // Logging roads: north to the Grove and the Outpost, east back down to the Lodge.
            Road(7f, DIRT, new Vector2(25, -112), new Vector2(25, -40), new Vector2(40, 30), new Vector2(100, 70), new Vector2(150, 90), new Vector2(172, 130), new Vector2(186, 186), new Vector2(165, 228), new Vector2(95, 230), new Vector2(30, 238));
            Road(7f, DIRT, new Vector2(165, 228), new Vector2(215, 250), new Vector2(270, 262));
            Road(7f, DIRT, new Vector2(330, 262), new Vector2(400, 250), new Vector2(412, 180), new Vector2(405, 110), new Vector2(385, 40), new Vector2(340, -12), new Vector2(298, -40));
            // Trails.
            Trail(new Vector2(-190, 205), new Vector2(-120, 262), new Vector2(-90, 300), new Vector2(-66, 307));
            Trail(new Vector2(-30, 307), new Vector2(-26, 307));
            Trail(new Vector2(-10, 405), new Vector2(-15, 350), new Vector2(-24, 314));
            Trail(new Vector2(60, -405), new Vector2(60, -360), new Vector2(40, -290));
            Trail(new Vector2(40, -252), new Vector2(62, -180), new Vector2(70, -116));
            Trail(new Vector2(150, 90), RANGER);
            Trail(new Vector2(-418, 0), new Vector2(-470, 40), new Vector2(-440, 120));
            _zoneStage.Add(() => Flatten(R(-72, 300, -60, 314), UPPER, 8f));
            _zoneStage.Add(() => Flatten(R(-36, 300, -24, 314), UPPER, 6f));

            _zoneStage.Add(() => FlattenDisc(RANGER, 22f, 5f, 30f));
            _zoneStage.Add(() => FlattenDisc(CAMP, 28f, UPPER, 20f));
            _zoneStage.Add(() => FlattenDisc(LOGCAMP, 26f, 0.5f, 20f));
            _zoneStage.Add(() => FlattenDisc(QUARRY + new Vector2(0, -12), 48f, QUARRY_RIM, 18f));
            foreach (Vector3 c in CLEARINGS)
            {
                Vector3 lC = c;
                _zoneStage.Add(() => FlattenDisc(new Vector2(lC.x, lC.y), lC.z * 0.55f, BaseHeight(lC.x, lC.y), 12f));
            }
            // The quarry pit: a floor, a bench, a ramp down from the south rim.
            _carveStage.Add(() => Cells(new Rect(QUARRY.x - 30, QUARRY.y - 40, 60, 70), (i, j) =>
            {
                float x = Cx(i), z = Cx(j), r = Vector2.Distance(new Vector2(x, z), QUARRY);
                if (r < 20f) { _h[i, j] = QUARRY_FLOOR; _paint[i, j] = GRAVEL; _f[i, j] |= BARE; }
                else if (r < 26f && z > QUARRY.y - 10f) { _h[i, j] = QUARRY_FLOOR + 4f; _paint[i, j] = Color.Lerp(GRAVEL, ROCK, 0.5f); _f[i, j] |= BARE; }
                if (x > QUARRY.x - 3.5f && x < QUARRY.x + 3.5f && z > QUARRY.y - 38f && z < QUARRY.y - 16f)
                {
                    _h[i, j] = Mathf.Lerp(QUARRY_RIM, QUARRY_FLOOR, Mathf.InverseLerp(QUARRY.y - 38f, QUARRY.y - 18f, z));
                    _paint[i, j] = DIRT;
                    _f[i, j] |= BARE | ROAD;
                }
            }));
            PaintDisc(CAMP, 18f, new Color(0.52f, 0.5f, 0.36f));
            PaintDisc(LOGCAMP, 20f, new Color(0.55f, 0.47f, 0.35f));
            PaintDisc(RANGER, 12f, new Color(0.5f, 0.48f, 0.34f));

            // Hollow Farm, south of the town: barn, farmhouse, silo, wheat fields.
            _zoneStage.Add(() => FlattenDisc(FARM, 52f, 0.5f, 25f));
            Road(5f, TRAIL, new Vector2(-250, LANE_Z), new Vector2(-250, -230), new Vector2(-244, -300), FARM + new Vector2(0, 18));
            PaintRect(R(FARM.x - 20, FARM.y - 6, FARM.x + 20, FARM.y + 22), new Color(0.55f, 0.47f, 0.35f));
            MapKit.KitSpec lBarn = Register(Lot("Hollow Barn", "Timber", FARM.x - 12f, FARM.y + 6f, 0, 6, 4, 1, Roof.Gable, new Color(0.85f, 0.45f, 0.38f), ROOF_TINTS[1], 0, 0.5f, 6f));
            lBarn.arches.Add(new Vector2Int(0, 2));
            lBarn.arches.Add(new Vector2Int(0, 3));
            Doors(lBarn, 2, 2);
            lBarn.windowChance = 0.3f;
            MapKit.KitSpec lFarmhouse = Register(Lot("Farmhouse", "Suburb", FARM.x + 14f, FARM.y + 22f, 180, 4, 3, 2, Roof.Gable, new Color(0.93f, 0.9f, 0.82f), ROOF_TINTS[2], 0, 0.5f, 6f));
            Doors(lFarmhouse, 0, 2, 2, 1);
            lFarmhouse.balconyChance = 0.3f;
            Floors(lFarmhouse, Room.Kitchen, Room.Lounge, Room.Bedroom, Room.Bathroom);

            MapKit.KitSpec lRanger = Register(Lot("Ranger Station", "Timber", RANGER.x, RANGER.y, 180, 3, 3, 1, Roof.Gable, TIMBER_TINTS[3], ROOF_TINTS[3], 0, 5f, 6f));
            Doors(lRanger, 0, 1, 2, 1);
            Floors(lRanger, Room.Office, Room.None);
            MapKit.KitSpec lShed = Register(Lot("Quarry Shed", "Timber", QUARRY.x + 28f, QUARRY.y - 32f, 0, 4, 3, 1, Roof.Gable, TIMBER_TINTS[2], ROOF_TINTS[1], 0, QUARRY_RIM, 6f));
            Doors(lShed, 0, 2, 3, 1);
            Floors(lShed, Room.Storage, Room.None);
            MapKit.KitSpec lCabin = Register(Lot("Logging Camp Cabin", "Timber", LOGCAMP.x + 10f, LOGCAMP.y + 8f, 0, 3, 3, 1, Roof.Gable, TIMBER_TINTS[2] * 0.9f, ROOF_TINTS[0], 2, 0.5f, 6f));
            Doors(lCabin, 0, 1);
            Floors(lCabin, Room.Bedroom, Room.None);
        }

        private void Wilds()
        {
            // Patrol cars: the big logging loop and the town loop.
            Cruiser(9f, 0f, new Color(0.35f, 0.42f, 0.3f), new Vector2(25, -112), new Vector2(110, -98), new Vector2(170, -70), new Vector2(230, -45), new Vector2(298, -40), new Vector2(340, -12), new Vector2(385, 40),
                new Vector2(405, 110), new Vector2(412, 180), new Vector2(400, 250), new Vector2(330, 262), new Vector2(270, 262), new Vector2(215, 250), new Vector2(165, 228), new Vector2(186, 186), new Vector2(172, 130),
                new Vector2(150, 90), new Vector2(100, 70), new Vector2(40, 30), new Vector2(25, -40));
            Cruiser(7f, 120f, new Color(0.12f, 0.14f, 0.2f), new Vector2(-418, MAIN_Z), new Vector2(-156, MAIN_Z), new Vector2(-156, RIDGE_Z), new Vector2(-418, RIDGE_Z));

            Ranger();
            Farm();
            Campsite();
            Quarry();
            LoggingCamp();
            Crossings();

            // Clearings of tall grass.
            foreach (Vector3 c in CLEARINGS) K.Grass(G(c.x, c.y), new Vector2(c.z, c.z * 0.75f), new Color(0.7f, 0.72f, 0.4f));
            K.Grass(G(-130, -10), new Vector2(26, 18), new Color(0.7f, 0.72f, 0.4f));
            K.Grass(G(110, -150), new Vector2(24, 16), new Color(0.72f, 0.72f, 0.42f));
            K.Grass(G(215, -15), new Vector2(18, 26), new Color(0.72f, 0.72f, 0.42f));

            // Lone weapons and gadgets in the wild (stashes at landmarks).
            (float x, float z, ItemKind kind)[] lStash =
            {
                (-150, 50, ItemKind.Smg), (240, 110, ItemKind.Shotgun), (250, -5, ItemKind.Sniper), (-300, 225, ItemKind.Smg), (130, -215, ItemKind.Pistol),
                (-20, 395, ItemKind.Shotgun), (330, 180, ItemKind.Smg), (-30, -330, ItemKind.Sniper), (200, 20, ItemKind.Pistol), (-240, 330, ItemKind.Shotgun)
            };
            foreach ((float x, float z, ItemKind kind) in lStash)
            {
                Vector3 p = G(x + 3f, z + 2f);
                K.Crate(p);
                K.Weapon(kind, p + Vector3.up * 1.15f);
            }
            K.Gadget(ItemKind.SmokeGrenade, G(-150, 52, 0.1f));
            K.Gadget(ItemKind.Coin, G(240, 112, 0.1f));
            K.Gadget(ItemKind.Brick, G(200, 22, 0.1f));
            K.Gadget(ItemKind.EmeticPoison, G(-300, 227, 0.1f));

            // Jump pads: up the terrace cliff by the waterfall, onto the lodge terrace from the beach woods.
            K.JumpPad(G(-20, 128), 0, new Vector3(0, 16f, 9f));
            K.JumpPad(G(-110, 112), 0, new Vector3(0, 16f, 9f));
            // Hunting stand above the lodge road (the huntmaster's lookout).
            Tower(2, 222, -32, 160, null);
            K.Activity(ActivityType.Lookout, G(225, -30, 0.1f), 160, 2);
        }

        private void Ranger()
        {
            K.Building(Spec("Ranger Station"));
            Vector3 c = G(RANGER.x, RANGER.y);
            Tower(0, RANGER.x + 12f, RANGER.y - 6f, 200, null);
            K.Car(G(RANGER.x - 8f, RANGER.y - 10f), 30, new Color(0.3f, 0.42f, 0.3f));
            for (int i = 0; i < 4; i++) Nat("Log", RANGER.x - 6f, RANGER.y + 7f + i * 0.85f, 0.7f, 90f, null, MapKit.Collide.Box, 0.1f);
            P("PowerPole", RANGER.x + 6f, RANGER.y + 10f, 30);
            K.DistractionKit(G(RANGER.x + 5f, RANGER.y + 6f), 0, "radio");
            K.Npc(NpcRole.Patrol, G(RANGER.x, RANGER.y - 12f, 0.1f), 0, 0, ItemKind.Shotgun, K.Route(4f, G(RANGER.x, RANGER.y - 12f, 0.1f), G(RANGER.x + 20f, RANGER.y + 8f, 0.1f), G(RANGER.x - 16f, RANGER.y + 14f, 0.1f)));
            K.Gadget(ItemKind.SmokeGrenade, c + new Vector3(1, 0.95f, 1));
        }

        private void Farm()
        {
            const int Z = 0;
            MapKit.KitBuilding lBarn = K.Building(Spec("Hollow Barn"));
            K.Building(Spec("Farmhouse"));
            Vector3 b = lBarn.root.position;
            for (int i = 0; i < 4; i++) K.HidingKit(b + new Vector3(-6f + (i % 2) * 2.6f, 0, 2f + (i / 2) * 2.6f), 90, "Haystack");
            K.Car(b + new Vector3(4f, 0, 1f), 0, new Color(0.7f, 0.25f, 0.2f));
            K.Weapon(ItemKind.Shotgun, b + new Vector3(7f, 0.2f, 4.5f));
            K.Activity(ActivityType.Inspect, b + new Vector3(0, 0.1f, -1f), 180, Z);
            // Silo.
            Vector3 lSilo = G(FARM.x - 32f, FARM.y + 10f);
            K.Mesh(K.Env, "Silo", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(14), "Forest_Silo"), lSilo + Vector3.up * 7f, new Vector3(5.5f, 14f, 5.5f), new Color(0.72f, 0.74f, 0.75f), null, true).isStatic = true;
            K.Mesh(K.Env, "SiloCap", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0f, 0), "Forest_Dome"), lSilo + Vector3.up * 14f, new Vector3(5.6f, 3f, 5.6f), new Color(0.6f, 0.3f, 0.25f));
            Ladder(lSilo + new Vector3(0, 0, -2.95f), 14f, 180f, "Suburb");
            K.Weapon(ItemKind.Sniper, lSilo + new Vector3(0.4f, 15.3f, 0));
            // Wheat fields (tall grass) fenced with rails, haystacks, a vegetable garden.
            Vector3[] lFields = { new Vector3(FARM.x + 26, FARM.y - 18, 0), new Vector3(FARM.x - 22, FARM.y - 20, 0), new Vector3(FARM.x + 26, FARM.y - 42, 0), new Vector3(FARM.x - 22, FARM.y - 44, 0) };
            foreach (Vector3 f in lFields)
            {
                K.Grass(G(f.x, f.y), new Vector2(30, 18), new Color(0.86f, 0.74f, 0.4f));
                Row("Timber", "Railing", G(f.x - 15.5f, f.y + 9.8f), G(f.x + 15.5f, f.y + 9.8f), 3f, 0f, TIMBER_TINTS[2]);
            }
            for (int i = 0; i < 5; i++) K.HidingKit(G(FARM.x + Rand(-38f, 38f), FARM.y - 58f + Rand(-4f, 4f)), Rand(0, 360), "Haystack");
            for (int i = 0; i < 6; i++) Nat("FlowerBed", FARM.x + 8f + (i % 3) * 3f, FARM.y + 4f + (i / 3) * 2f, 1.3f, 0f, new Color(0.45f, 0.62f, 0.3f), MapKit.Collide.None);
            K.Activity(ActivityType.Garden, G(FARM.x + 11f, FARM.y + 2f, 0.1f), 0, Z);
            K.Activity(ActivityType.Smoke, G(FARM.x - 4f, FARM.y + 16f, 0.1f), 0, Z);
            K.DistractionKit(G(FARM.x - 2f, FARM.y + 2f), 0, "generator");
            Guard(Z, FARM.x + 2f, FARM.y + 16f, 0, ItemKind.Shotgun);
            Walk(Z, ItemKind.Smg, 5f, FARM + new Vector2(-40, -10), FARM + new Vector2(40, -10), FARM + new Vector2(40, -55), FARM + new Vector2(-40, -55));
            K.Gadget(ItemKind.Brick, G(FARM.x + 16f, FARM.y + 12f, 0.1f));
        }

        private void Campsite()
        {
            Vector3 c = G(CAMP);
            Color[] lTents = { new Color(0.85f, 0.45f, 0.2f), new Color(0.25f, 0.45f, 0.65f), new Color(0.4f, 0.55f, 0.3f), new Color(0.75f, 0.7f, 0.4f), new Color(0.65f, 0.3f, 0.3f) };
            for (int i = 0; i < 5; i++)
            {
                float a = 30f + i * 62f;
                Vector2 p = Polar(CAMP, a, 12f);
                P("TentMilitary", p.x, p.y, 270f - a, lTents[i], true, MapKit.Collide.Mesh);
            }
            for (int i = 0; i < 8; i++) K.NaturePiece("RockA", c + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 1.3f, i * 45f, 1f, null, MapKit.Collide.Box);
            K.Lamp(c + Vector3.up * 0.35f, new Color(1f, 0.55f, 0.25f), 9f, 2f, false);
            for (int i = 0; i < 3; i++) K.NaturePiece("Log", c + Quaternion.Euler(0, i * 120f, 0) * Vector3.forward * 3.4f, i * 120f + 90f, 0.8f, LOG, MapKit.Collide.Box);
            P("Table", CAMP.x + 6f, CAMP.y - 4f, 20, PLANK, true);
            P("Bench", CAMP.x + 6f, CAMP.y - 5.2f, 20);
            K.DrinksOn(G(CAMP.x + 6f, CAMP.y - 4f, 0.8f), 20, 0, 2);
            K.Car(G(CAMP.x - 16f, CAMP.y - 8f), 60, new Color(0.9f, 0.9f, 0.85f));
            K.Activity(ActivityType.Smoke, G(CAMP.x - 3f, CAMP.y + 3f, 0.1f), 180, 0);
            K.Activity(ActivityType.Lookout, G(CAMP.x - 20f, CAMP.y + 20f, 0.1f), 45, 0);
            Guard(0, CAMP.x + 3f, CAMP.y + 4f, 200, ItemKind.Shotgun);
            Walk(0, ItemKind.Smg, 4f, CAMP + new Vector2(-18, -14), CAMP + new Vector2(18, -14), CAMP + new Vector2(18, 18), CAMP + new Vector2(-18, 18));
            K.Weapon(ItemKind.Sniper, G(CAMP.x + 8f, CAMP.y + 9f, 0.1f));
            K.Gadget(ItemKind.C4, G(CAMP.x - 9f, CAMP.y - 2f, 0.1f));
        }

        private void Quarry()
        {
            const int Z = 0;
            K.Building(Spec("Quarry Shed"));
            Vector3 lFloor = new Vector3(QUARRY.x, QUARRY_FLOOR, QUARRY.y);
            // Rock faces around the pit.
            for (int a = 0; a < 360; a += 14)
            {
                Vector2 p = Polar(QUARRY, a, 21.5f);
                if (Mathf.Abs(Mathf.DeltaAngle(a, 270)) < 14) continue;
                K.NaturePiece("CliffChunk", new Vector3(p.x, QUARRY_FLOOR - 1f, p.y), a + 90f, Rand(0.8f, 1.2f), null, MapKit.Collide.None);
            }
            // The old mine: a timber portal in the north wall, rails and carts coming out.
            Vector3 lPortal = new Vector3(QUARRY.x, QUARRY_FLOOR, QUARRY.y + 17.5f);
            K.Piece("Timber", "WallArch", lPortal, 180, new Color(0.7f, 0.6f, 0.5f), K.Env);
            for (int s = -1; s <= 1; s += 2) K.Piece("Timber", "Wall", lPortal + new Vector3(s * 3f, 0, 0), 180, new Color(0.7f, 0.6f, 0.5f), K.Env);
            K.Block(K.Env, "MineDark", lPortal + new Vector3(0, 1.7f, 2.2f), new Vector3(3.2f, 3.4f, 4f), new Color(0.04f, 0.04f, 0.05f));
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "MineRail", lPortal + new Vector3(s * 0.55f, 0.06f, -6f), new Vector3(0.08f, 0.1f, 14f), new Color(0.4f, 0.4f, 0.42f), Core.GameLayers.DEFAULT, false);
            for (int i = 0; i < 2; i++)
            {
                Vector3 lCart = lPortal + new Vector3(0, 0, -4f - i * 4f);
                K.Block(K.Env, "MineCart", lCart + Vector3.up * 0.65f, new Vector3(1.3f, 0.9f, 2f), new Color(0.35f, 0.33f, 0.3f), Core.GameLayers.DEFAULT, true, null, true);
                K.Mesh(K.Env, "Ore", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0.25f, 9), "Forest_Ore"), lCart + Vector3.up * 1.15f, new Vector3(1.2f, 0.5f, 1.8f), new Color(0.45f, 0.44f, 0.42f));
            }
            K.Lamp(lPortal + new Vector3(0, 3f, -0.6f), WARM, 9f, 1.4f, false);
            K.Point(MatchPoint.Kind.Extraction, lPortal + new Vector3(0, 0.1f, -1.5f), 180, "Old mine tunnel");
            K.UniqueWeapon(ItemKind.RocketLauncher, lPortal + new Vector3(1.0f, 0.15f, 1.2f), "Quarry mine");
            // Rubble piles, the crane, explosives.
            for (int i = 0; i < 6; i++) Nat(i % 2 == 0 ? "RockD" : "RockC", QUARRY.x + Rand(-14f, 14f), QUARRY.y + Rand(-12f, 8f), Rand(0.8f, 1.3f));
            for (int i = 0; i < 3; i++) K.Mesh(K.Env, "Gravel", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0.2f, 4), "Forest_GravelPile"), lFloor + new Vector3(-12f + i * 5f, 0, -8f), new Vector3(4f, 2.2f, 4f), new Color(0.6f, 0.58f, 0.54f), null, true);
            for (int k = 0; k < 3; k++) K.Piece("Timber", "Scaffold", new Vector3(QUARRY.x + 16f, QUARRY_FLOOR + k * 4f, QUARRY.y - 4f), 90, null, K.Env);
            Deck(new Vector3(QUARRY.x + 16f, QUARRY_FLOOR + 12.05f, QUARRY.y - 4f), new Vector2(3.4f, 3.4f), 90f, 0.25f);
            for (int s = -1; s <= 1; s += 2) K.Piece("Timber", "Railing", new Vector3(QUARRY.x + 16f + s * 1.6f, QUARRY_FLOOR + 12.05f, QUARRY.y - 4f), s * 90f, null, K.Env);
            K.Block(K.Env, "CraneBoom", new Vector3(QUARRY.x + 9f, QUARRY_FLOOR + 15.3f, QUARRY.y - 4f), new Vector3(16f, 0.5f, 0.8f), new Color(0.75f, 0.6f, 0.2f));
            Ladder(new Vector3(QUARRY.x + 16f, QUARRY_FLOOR, QUARRY.y - 5.95f), 12.2f, 180f);
            K.Npc(NpcRole.Lookout, new Vector3(QUARRY.x + 16f, QUARRY_FLOOR + 12.15f, QUARRY.y - 4f), 200, Z, ItemKind.Sniper);
            for (int i = 0; i < 4; i++) K.Barrel(lFloor + new Vector3(8f + (i % 2) * 0.8f, 0, 6f + (i / 2) * 0.8f), true);
            K.Car(G(QUARRY.x + 20f, QUARRY.y - 40f), 90, new Color(0.62f, 0.5f, 0.2f));
            K.Activity(ActivityType.Inspect, lFloor + new Vector3(-4f, 0.1f, 4f), 0, Z);
            Guard(Z, QUARRY.x + 4f, QUARRY.y - 40f, 180, ItemKind.Smg);
            Walk(Z, ItemKind.Shotgun, 4f, new Vector2(QUARRY.x - 10, QUARRY.y - 8), new Vector2(QUARRY.x + 10, QUARRY.y - 8), new Vector2(QUARRY.x + 6, QUARRY.y + 12), new Vector2(QUARRY.x - 8, QUARRY.y + 12));
            K.Weapon(ItemKind.Shotgun, G(QUARRY.x + 28f, QUARRY.y - 30f, 1.0f));
            K.Gadget(ItemKind.C4, lFloor + new Vector3(9f, 0.15f, 8f));
        }

        private void LoggingCamp()
        {
            K.Building(Spec("Logging Camp Cabin"));
            for (int i = 0; i < 3; i++) LogDeck(new Vector3(LOGCAMP.x - 12f + i * 8f, 0, LOGCAMP.y - 6f), 90f, 2 + i % 2);
            for (int i = 0; i < 8; i++) Nat("DeadTree", LOGCAMP.x + Rand(-24f, 24f), LOGCAMP.y + Rand(-24f, 24f), Rand(0.3f, 0.45f), -1f, new Color(0.5f, 0.42f, 0.32f));
            K.Car(G(LOGCAMP.x + 2f, LOGCAMP.y + 10f), 0, new Color(0.62f, 0.25f, 0.18f));
            K.DistractionKit(G(LOGCAMP.x - 4f, LOGCAMP.y + 12f), 0, "generator");
            K.UniqueWeapon(ItemKind.Flamethrower, G(LOGCAMP.x + 10f, LOGCAMP.y + 5f, 0.15f), "Logging camp");
            Guard(2, LOGCAMP.x, LOGCAMP.y + 2f, 180, ItemKind.Smg);
            Walk(2, ItemKind.Pistol, 4f, LOGCAMP + new Vector2(-20, -16), LOGCAMP + new Vector2(20, -16), LOGCAMP + new Vector2(0, 22));
            K.Weapon(ItemKind.Smg, G(LOGCAMP.x - 6f, LOGCAMP.y + 2f, 0.1f));
        }

        /// <summary>River crossings outside the town: the logging camp footbridge, stepping stones, a log over the fall.</summary>
        private void Crossings()
        {
            Span(new Vector3(40, 0.3f, -250), new Vector3(40, 0.3f, -283), 2.6f, false);
            // Stepping stones on the upper river (a ford north of the boardwalk).
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(-70f + i * 3.4f, UPPER + RIVER_WATER - 0.6f, 395f + Mathf.Sin(i) * 1.2f);
                K.NaturePiece("RockC", p, Rand(0, 360), 0.9f, null, MapKit.Collide.Box);
            }
            // A fallen log across the lip of the waterfall.
            GameObject lLog = K.NaturePiece("Log", new Vector3(RIVER[FALL].x, UPPER - 0.25f, RIVER[FALL].y + 3f), 0f, 5.6f, LOG, MapKit.Collide.Box);
            if (lLog != null) lLog.transform.localScale = new Vector3(5.6f, 1.2f, 1.2f);
            K.Activity(ActivityType.Lookout, G(RIVER[FALL].x + 20f, RIVER[FALL].y + 10f, 0.1f), 180, 0);
            // Rocks by the plunge pool.
            for (int i = 0; i < 10; i++) Nat(i % 2 == 0 ? "RockD" : "CliffChunk", RIVER[FALL].x + Rand(-16f, 16f), RIVER[FALL].y + Rand(-6f, 6f), Rand(0.6f, 1.1f), -1f, null, MapKit.Collide.Box, 1f);
        }

        // ================================================================ vegetation

        private float Wildness(float x, float z)
        {
            if (x > -428 && x < -96 && z > -248 && z < -18) return 0.03f;
            if (Vector2.Distance(new Vector2(x, z), FARM + new Vector2(0, -15)) < 62f) return 0.02f;
            if (x > -58 && x < 45 && z > -175 && z < -30) return 0f;
            if (x > 200 && x < 405 && z > -152 && z < -40) return 0.05f;
            if (Vector2.Distance(new Vector2(x, z), OUTPOST) < 84f) return 0f;
            if (Vector2.Distance(new Vector2(x, z), GROVE) < RING_R + 8f) return 0.02f;
            if (x > -25 && x < 205 && z > 205 && z < 395) return 0.25f;
            if (Vector2.Distance(new Vector2(x, z), QUARRY) < 34f) return 0f;
            return 1f;
        }

        private void Vegetation()
        {
            string[] lPines = { "PineA", "PineB", "PineC", "PineB" };
            const float STEP = 6.4f;
            int lTrees = 0, lBushes = 0, lRocks = 0, lCliffs = 0;
            for (float gx = -496f; gx < 496f; gx += STEP)
                for (float gz = -496f; gz < 496f; gz += STEP)
                {
                    float x = gx + Rand(-3.2f, 3.2f), z = gz + Rand(-3.2f, 3.2f);
                    int i = Ci(x), j = Ci(z);
                    if (Blocked(i, j)) continue;
                    float s = Slope(i, j), h = _h[i, j];
                    if (s > 3.4f || h > 45f) continue;
                    float d = Border(x, z);
                    float lDensity = Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x / 120f + 3f, z / 120f + 9f) - 0.24f) * 2.6f);
                    lDensity = Mathf.Max(lDensity, Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x / 37f + 1f, z / 37f + 5f) - 0.55f) * 3f));
                    if (d < 95f) lDensity = Mathf.Max(lDensity, 0.9f);
                    if (h > 38f) lDensity *= 0.35f;
                    lDensity *= Wildness(x, z);
                    float lRoll = (float)K.Rng.NextDouble();
                    if (lRoll < lDensity * 0.8f)
                    {
                        float lRiver = Nearest(RIVER, new Vector2(x, z)).distance;
                        string lPiece;
                        if (lRiver < 45f && K.Rng.NextDouble() < 0.45) lPiece = "BirchA";
                        else if (h > 36f) lPiece = K.Rng.NextDouble() < 0.2 ? "DeadTree" : "PineA";
                        else if (K.Rng.NextDouble() < 0.06) lPiece = "BroadleafC";
                        else if (K.Rng.NextDouble() < 0.02) lPiece = "DeadTree";
                        else lPiece = lPines[K.Rng.Next(lPines.Length)];
                        float lScale = lPiece == "PineC" ? Rand(0.85f, 1.25f) : Rand(1.0f, 1.5f);
                        if (d < 95f) lScale *= 1.15f;
                        Nat(lPiece, x, z, lScale, -1f, null, MapKit.Collide.Box, 0.2f);
                        lTrees++;
                    }
                    else if (lRoll < lDensity * 0.8f + 0.14f * Wildness(x, z))
                    {
                        string lBush = K.Rng.NextDouble() < 0.12 ? "Log" : new[] { "BushA", "BushB", "BushC", "BushB" }[K.Rng.Next(4)];
                        Nat(lBush, x, z, Rand(0.8f, 1.4f), -1f, lBush == "Log" ? LOG : (Color?)null, MapKit.Collide.None, 0.1f);
                        lBushes++;
                    }
                    else if (lRoll > 0.965f && Wildness(x, z) > 0.5f)
                    {
                        Nat(new[] { "RockA", "RockB", "RockC", "RockD" }[K.Rng.Next(4)], x, z, Rand(0.7f, 1.5f), -1f, null, MapKit.Collide.Box, 0.3f);
                        lRocks++;
                    }
                }
            // Cliff faces dressed with rock chunks; reeds on the banks outside town.
            for (int i = 1; i < N - 1; i++)
                for (int j = 1; j < N - 1; j++)
                {
                    float x = Cx(i), z = Cx(j);
                    if ((_f[i, j] & (WET | ROAD | BUILT)) != 0) continue;
                    float s = Slope(i, j);
                    bool lRidge = Border(x, z) < 70f;
                    if (s > 3.6f && K.Rng.NextDouble() < (lRidge ? 0.22 : 0.5))
                    {
                        float lLow = Mathf.Min(Mathf.Min(_h[i - 1, j], _h[i + 1, j]), Mathf.Min(_h[i, j - 1], _h[i, j + 1]));
                        K.NaturePiece("CliffChunk", new Vector3(x + Rand(-1f, 1f), Mathf.Lerp(lLow, _h[i, j], 0.35f), z + Rand(-1f, 1f)), Rand(0, 360), Rand(0.7f, 1.3f), GameArt.Vary(new Color(0.62f, 0.62f, 0.6f), 0.08f, K.Rng), MapKit.Collide.None);
                        lCliffs++;
                    }
                    if ((_f[i, j] & BANK) != 0 && Wildness(x, z) > 0.2f && K.Rng.NextDouble() < 0.3)
                        Nat("ReedsClump", x + Rand(-1.5f, 1.5f), z + Rand(-1.5f, 1.5f), Rand(0.9f, 1.4f), -1f, null, MapKit.Collide.None);
                }
            Debug.Log($"[Build] Map_Forest vegetation: {lTrees} trees, {lBushes} bushes, {lRocks} rocks, {lCliffs} cliff chunks");
        }

        private bool Blocked(int i, int j)
        {
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    int a = Mathf.Clamp(i + di, 0, N - 1), b = Mathf.Clamp(j + dj, 0, N - 1);
                    byte f = _f[a, b];
                    if ((f & (ROAD | BUILT)) != 0) return true;
                    if (di == 0 && dj == 0 && (f & (WET | BANK | BARE)) != 0) return true;
                }
            return false;
        }

        // ================================================================ match points

        private void MatchPoints()
        {
            K.Point(MatchPoint.Kind.Start, G(-418, -200, 0.2f), 90, "West rail cutting");
            K.Point(MatchPoint.Kind.Start, G(-10, 402, 0.2f), 180, "North gorge trail");
            K.Point(MatchPoint.Kind.Start, G(408, 150, 0.2f), 270, "East ridge road");
            K.Point(MatchPoint.Kind.Start, G(60, -402, 0.2f), 0, "South logging trail");
        }
    }
}
