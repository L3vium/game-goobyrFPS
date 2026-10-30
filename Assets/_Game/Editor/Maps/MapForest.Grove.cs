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
    /// The Grove & Outpost (zone 1) on the upper terrace. The Grove: a palisaded island in a marsh
    /// fed by the river, ringed by a channel crossed by plank walkways, with a raised boardwalk on
    /// stilts from the river, tall grass and hay fields around; inside, the longhouse ceremony
    /// (fire, benches, the elder's altar), cabins, the greenhouse, the kitchen hall, outhouses; an
    /// orchard outside. The Outpost: a fenced hilltop camp east of it - watchtowers on the corners,
    /// barracks, HQ, the bunker and its basement, motor pool, the comms mast with the alarm flare,
    /// the quartermaster's armory (and his fence gap), the helipad.
    /// </summary>
    public partial class MapForest
    {
        private static readonly Vector2 GROVE = new Vector2(95, 300);
        private const float ISLAND = UPPER + 0.35f, PALISADE_R = 46f, RING_R = 58f;
        private static readonly Vector2 OUTPOST = new Vector2(330, 330);
        private const float HILL = 16f;
        private static readonly Color OLIVE = new Color(0.55f, 0.6f, 0.45f);

        private static Vector2 Polar(Vector2 pCenter, float pAngle, float pRadius) => pCenter + new Vector2(Mathf.Cos(pAngle * Mathf.Deg2Rad), Mathf.Sin(pAngle * Mathf.Deg2Rad)) * pRadius;

        private readonly List<Vector2[]> _channels = new List<Vector2[]>();

        private void Channel(params Vector2[] pPoints)
        {
            _channels.Add(pPoints);
            _carveStage.Add(() =>
            {
                Rect lBox = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
                foreach (Vector2 p in pPoints) lBox = Rect.MinMaxRect(Mathf.Min(lBox.xMin, p.x), Mathf.Min(lBox.yMin, p.y), Mathf.Max(lBox.xMax, p.x), Mathf.Max(lBox.yMax, p.y));
                Cells(Expand(lBox, 7f), (i, j) =>
                {
                    if ((_f[i, j] & (BUILT | ROAD)) != 0) return;
                    float d = Nearest(pPoints, new Vector2(Cx(i), Cx(j))).distance;
                    if (d < 3.2f) { _h[i, j] = UPPER - 1.5f; _f[i, j] |= WET; }
                    else if (d < 6f) { _h[i, j] = Mathf.Min(_h[i, j], UPPER - 0.35f); _f[i, j] |= BARE; _paint[i, j] = MUD; }
                });
            });
        }

        private void PlanGrove()
        {
            _zoneStage.Add(() => Flatten(R(-28, 205, 205, 395), UPPER, 25f));
            _zoneStage.Add(() => FlattenDisc(GROVE, PALISADE_R + 4f, ISLAND, 5f));
            _zoneStage.Add(() => Flatten(R(140, 268, 190, 336), UPPER + 0.3f, 6f));
            // The ring channel around the island, the feed from the river and two drains.
            List<Vector2> lRing = new List<Vector2>();
            for (int a = 0; a <= 360; a += 15) lRing.Add(Polar(GROVE, a, RING_R + Mathf.Sin(a * 0.07f) * 2f));
            Channel(lRing.ToArray());
            Channel(new Vector2(-40, 300), new Vector2(0, 298), new Vector2(37, 300));
            Channel(Polar(GROVE, 315, RING_R), new Vector2(160, 245), new Vector2(196, 222));
            Channel(Polar(GROVE, 100, RING_R), new Vector2(84, 372), new Vector2(60, 392));
            // Marsh mottling.
            _paintStage.Add(() => Cells(R(-20, 210, 200, 392), (i, j) =>
            {
                if ((_f[i, j] & (WET | ROAD | BUILT)) != 0) return;
                float x = Cx(i), z = Cx(j);
                if (Vector2.Distance(new Vector2(x, z), GROVE) < PALISADE_R + 3f) { _paint[i, j] = new Color(0.5f, 0.55f, 0.36f); return; }
                float n = Mathf.PerlinNoise(x / 22f + 5f, z / 22f + 1f);
                if (n > 0.55f) { _paint[i, j] = MUD; _f[i, j] |= BARE; }
                else if (n > 0.42f) _paint[i, j] = new Color(0.47f, 0.52f, 0.32f);
            }));
            PaintDisc(Polar(GROVE, 270, 18), 10f, new Color(0.55f, 0.48f, 0.36f));
            PaintRect(R(GROVE.x - 2.5f, GROVE.y - 50f, GROVE.x + 2.5f, GROVE.y + 12f), new Color(0.55f, 0.48f, 0.36f));
            PaintRect(R(GROVE.x - 40f, GROVE.y - 2.5f, GROVE.x + 40f, GROVE.y + 2.5f), new Color(0.55f, 0.48f, 0.36f));

            MapKit.KitSpec lLong = Register(Lot("Longhouse", "Timber", 95, 318, 0, 9, 4, 1, Roof.Gable, new Color(0.82f, 0.7f, 0.56f), new Color(0.38f, 0.32f, 0.24f), 1, ISLAND, 3f));
            Doors(lLong, 0, 4, 2, 1, 1, 1, 3, 2);
            lLong.windowChance = 0.5f;
            MapKit.KitSpec lKitchen = Register(Lot("Grove Kitchen", "Timber", 66, 300, 270, 5, 3, 1, Roof.Hip, TIMBER_TINTS[2], new Color(0.38f, 0.32f, 0.24f), 1, ISLAND, 3f));
            Doors(lKitchen, 0, 2, 2, 2);
            Floors(lKitchen, Room.Dining, Room.Kitchen);
            MapKit.KitSpec lGreen = Register(Lot("Greenhouse", "Timber", 124, 296, 90, 4, 3, 1, Roof.Gable, new Color(0.85f, 0.97f, 0.92f), new Color(0.78f, 0.92f, 0.88f), 1, ISLAND, 3f));
            Doors(lGreen, 0, 1, 2, 2);
            lGreen.windowChance = 1f;
            lGreen.tallWindows = true;
            Vector3[] lCabins = { new Vector3(72, 272, 180), new Vector3(118, 272, 180), new Vector3(68, 322, 0), new Vector3(122, 322, 0) };
            for (int i = 0; i < lCabins.Length; i++)
            {
                MapKit.KitSpec lCabin = Register(Lot("Cabin " + i, "Timber", lCabins[i].x, lCabins[i].y, lCabins[i].z, 3, 3, 1, Roof.Gable, TIMBER_TINTS[(i + 1) % TIMBER_TINTS.Length] * 0.95f, new Color(0.38f, 0.32f, 0.24f), 1, ISLAND, 3f));
                Doors(lCabin, 0, 1);
                Floors(lCabin, Room.Bedroom, Room.None);
            }
            for (int i = 0; i < 2; i++)
            {
                MapKit.KitSpec lPrivy = Register(Lot("Outhouse " + i, "Timber", 106 + i * 4.5f, 265, 180, 1, 1, 1, Roof.Gable, TIMBER_TINTS[2] * 0.85f, new Color(0.38f, 0.32f, 0.24f), 1, ISLAND, 3f));
                Doors(lPrivy, 0, 0);
                lPrivy.stairs = false;
                lPrivy.windowChance = 0f;
            }
        }

        private void PlanOutpost()
        {
            _zoneStage.Add(() => FlattenDisc(OUTPOST, 64f, HILL, 45f));
            Road(6f, DIRT, new Vector2(330, 282), new Vector2(330, 262), new Vector2(270, 262));
            PaintRect(R(280, 282, 326, 324), new Color(0.5f, 0.5f, 0.48f));
            PaintDisc(new Vector2(350, 297), 8.5f, new Color(0.36f, 0.37f, 0.4f));
            PaintRect(R(326, 279, 334, 372), new Color(0.52f, 0.47f, 0.36f));
            PaintRect(R(280, 327, 380, 334), new Color(0.52f, 0.47f, 0.36f));
            PaintRect(R(282, 336, 312, 352), new Color(0.52f, 0.47f, 0.36f));

            MapKit.KitSpec lBarracks = Register(Lot("Barracks", "Timber", 300, 362, 0, 6, 3, 1, Roof.Gable, OLIVE, new Color(0.3f, 0.34f, 0.28f), 1, HILL, 4f));
            Doors(lBarracks, 0, 1, 0, 4, 2, 3);
            Floors(lBarracks, Room.Gym, Room.Bedroom);
            MapKit.KitSpec lHq = Register(Lot("Outpost HQ", "Suburb", 360, 360, 0, 4, 4, 2, Roof.Flat, new Color(0.72f, 0.75f, 0.66f), new Color(0.35f, 0.38f, 0.32f), 1, HILL, 4f));
            Doors(lHq, 0, 2, 3, 1);
            lHq.brick = true;
            lHq.climb = new Vector2Int(1, 2);
            Floors(lHq, Room.Security, Room.Office, Room.Office, Room.Lounge);
            MapKit.KitSpec lBarracksB = Register(Lot("Barracks B", "Timber", 300, 375, 0, 6, 3, 1, Roof.Gable, OLIVE * 0.95f, new Color(0.3f, 0.34f, 0.28f), 1, HILL, 4f));
            Doors(lBarracksB, 0, 2, 2, 4);
            Floors(lBarracksB, Room.Bedroom, Room.Bathroom);
            MapKit.KitSpec lShed = Register(Lot("Vehicle Shed", "Timber", 295, 288, 0, 5, 2, 1, Roof.Gable, OLIVE * 0.9f, new Color(0.3f, 0.34f, 0.28f), 1, HILL, 4f));
            for (int m = 0; m < 5; m++) lShed.arches.Add(new Vector2Int(0, m));
            lShed.stairs = false;
            MapKit.KitSpec lArmory = Register(Lot("Armory", "Suburb", 366, 306, 270, 3, 3, 1, Roof.Flat, new Color(0.7f, 0.72f, 0.64f), new Color(0.35f, 0.38f, 0.32f), 1, HILL, 4f));
            Doors(lArmory, 0, 1, 2, 1);
            Floors(lArmory, Room.Storage, Room.None);
            MapKit.KitSpec lBunker = Register(Lot("Bunker", "Stucco", 318, 342, 0, 4, 4, 1, Roof.Flat, new Color(0.62f, 0.63f, 0.6f), new Color(0.45f, 0.46f, 0.44f), 1, HILL, 4f));
            Doors(lBunker, 0, 1, 2, 2);
            lBunker.basement = true;
            lBunker.windowChance = 0.15f;
            Floors(lBunker, Room.Security, Room.Storage, Room.Office, Room.None);
        }

        // ================================================================ build

        private void Grove()
        {
            const int Z = 1;
            Vector3 c = new Vector3(GROVE.x, ISLAND, GROVE.y);

            // Channel water.
            foreach (Vector2[] lLine in _channels)
                for (int s = 0; s < lLine.Length - 1; s++)
                {
                    Vector2 a = lLine[s], b = lLine[s + 1];
                    GameObject lPlane = K.Water(new Vector3((a.x + b.x) * 0.5f, UPPER - 0.8f, (a.y + b.y) * 0.5f), new Vector2(7.5f, Vector2.Distance(a, b) + 3f));
                    lPlane.transform.rotation = Quaternion.LookRotation(new Vector3(b.x - a.x, 0, b.y - a.y));
                    for (int k = 0; k < 2; k++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, Rand(0f, 1f)) + new Vector2(Rand(-5f, 5f), Rand(-5f, 5f));
                        if (!Flag(p.x, p.y, WET | BUILT | ROAD)) Nat("ReedsClump", p.x, p.y, Rand(0.9f, 1.4f), -1f, null, MapKit.Collide.None);
                    }
                }

            // The palisade: upright logs on a ring, with the gate (south), two breaches and an ivy section.
            for (float a = 0f; a < 360f; a += 1.15f)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 4.2f || Mathf.Abs(Mathf.DeltaAngle(a, 152f)) < 2.2f || Mathf.Abs(Mathf.DeltaAngle(a, 18f)) < 2f) continue;
                Vector2 p = Polar(GROVE, a, PALISADE_R + Rand(-0.15f, 0.15f));
                float lScale = Rand(0.85f, 1.05f);
                GameObject lLog = K.NaturePiece("Log", new Vector3(p.x, ISLAND + 2.05f * lScale - 0.45f, p.y), 0f, lScale, GameArt.Vary(LOG, 0.08f, K.Rng), MapKit.Collide.Box);
                if (lLog != null) lLog.transform.rotation = Quaternion.Euler(Rand(-3f, 3f), 90f - a, 90f + Rand(-2f, 2f));
            }
            Vector2 lIvy = Polar(GROVE, 88f, PALISADE_R - 0.6f);
            Ivy(new Vector3(lIvy.x, ISLAND, lIvy.y), 3.4f, 3f, 180f);
            Vector2 lIvyOut = Polar(GROVE, 88f, PALISADE_R + 0.6f);
            Ivy(new Vector3(lIvyOut.x, ISLAND, lIvyOut.y), 3.4f, 3f, 0f);
            // Gate: timber arches flanked by two watchtowers.
            Vector2 lGate = Polar(GROVE, 270f, PALISADE_R);
            for (int s = -1; s <= 1; s += 2) K.Piece("Timber", "WallArch", new Vector3(lGate.x + s * 1.5f, ISLAND, lGate.y), 180, null, K.Env);
            Tower(Z, lGate.x - 8f, lGate.y - 3f, 180, OLIVE);
            Tower(Z, lGate.x + 8f, lGate.y - 3f, 180, OLIVE);
            Guard(Z, lGate.x - 2.5f, lGate.y - 4f, 180, ItemKind.Shotgun);
            Guard(Z, lGate.x + 2.5f, lGate.y - 4f, 180, ItemKind.Smg);
            for (int i = 0; i < 4; i++) { Vector2 t = Polar(GROVE, 262f + i * 5.3f, PALISADE_R + 3f); Torch(G(t)); }

            // Walkways across the ring channel (south gate, west breach, east breach) and the stilt boardwalk.
            Walkway(Polar(GROVE, 270f, PALISADE_R + 4f), Polar(GROVE, 270f, RING_R + 9f), 0f);
            Walkway(Polar(GROVE, 152f, PALISADE_R + 4f), Polar(GROVE, 152f, RING_R + 9f), 0f);
            Walkway(Polar(GROVE, 18f, PALISADE_R + 4f), Polar(GROVE, 18f, RING_R + 9f), 0f);
            Walkway(new Vector2(-26, 307), new Vector2(38, 307), 1.6f);
            Span(new Vector3(-62, UPPER + 0.35f, 307), new Vector3(-34, UPPER + 0.35f, 307), 2.4f, false, 3f);

            // Buildings.
            MapKit.KitBuilding lLong = K.Building(Spec("Longhouse"));
            Vector3 h = lLong.root.position;
            K.Activity(ActivityType.Speech, h + new Vector3(0, 0.1f, 3.8f), 180, Z, 18f);
            K.Block(K.Env, "Altar", h + new Vector3(0, 0.45f, 4.8f), new Vector3(3f, 0.9f, 1f), new Color(0.45f, 0.36f, 0.28f), Core.GameLayers.DEFAULT, true, null, true);
            for (int i = 0; i < 3; i++) K.Lamp(h + new Vector3(-1.2f + i * 1.2f, 1.1f, 4.8f), new Color(1f, 0.6f, 0.3f), 6f, 1.2f, false);
            for (int row = 0; row < 3; row++)
                for (int s = -1; s <= 1; s += 2)
                    K.Prop("Bench", h + new Vector3(s * 3.5f, 0, 0.8f - row * 2f), 0, PLANK);
            K.Prop("Table", h + new Vector3(-9.5f, 0, 2f), 90, PLANK, MapKit.Collide.Box, true);
            K.DrinksOn(h + new Vector3(-9.5f, 0.8f, 2f), 90, Z, 3);
            K.Prop("Table", h + new Vector3(9.5f, 0, 2f), 90, PLANK, MapKit.Collide.Box, true);
            K.Activity(ActivityType.Dining, h + new Vector3(8.5f, 0.1f, 2f), 90, Z);
            K.Npc(NpcRole.Guard, h + new Vector3(-10, 0.1f, -3.5f), 90, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, h + new Vector3(10, 0.1f, -3.5f), 270, Z, ItemKind.Shotgun);
            K.Lamp(h + new Vector3(-6, 3.1f, 0), new Color(1f, 0.75f, 0.45f), 12f, 1.6f, false);
            K.Lamp(h + new Vector3(6, 3.1f, 0), new Color(1f, 0.75f, 0.45f), 12f, 1.6f, false);
            K.Weapon(ItemKind.Smg, h + new Vector3(12, 0.15f, 4.5f));

            // The ceremony ground: fire pit, log benches in a horseshoe, torches, the tea table.
            Vector3 lFire = G(GROVE.x, GROVE.y - 4f);
            for (int i = 0; i < 10; i++) K.NaturePiece("RockA", lFire + Quaternion.Euler(0, i * 36f, 0) * Vector3.forward * 1.6f, i * 36f, 1.2f, null, MapKit.Collide.Box);
            for (int i = 0; i < 3; i++) K.Lamp(lFire + new Vector3(Rand(-0.5f, 0.5f), 0.35f, Rand(-0.5f, 0.5f)), new Color(1f, 0.55f, 0.25f), 9f, 2.2f, false);
            for (int i = 0; i < 4; i++) { GameObject lWood = K.NaturePiece("Log", lFire + Vector3.up * 0.2f, i * 45f, 0.35f, LOG * 0.6f, MapKit.Collide.None); }
            for (int i = 0; i < 7; i++)
            {
                float a = 200f + i * 23f;
                Vector3 p = lFire + Quaternion.Euler(0, -a, 0) * Vector3.right * 7f;
                K.NaturePiece("Log", p, a + 90f, 0.9f, LOG, MapKit.Collide.Box);
            }
            for (int i = 0; i < 6; i++) Torch(lFire + Quaternion.Euler(0, 30f + i * 60f, 0) * Vector3.forward * 10.5f);
            K.Prop("Table", lFire + new Vector3(10.5f, 0, -6f), 0, PLANK, MapKit.Collide.Box, true);
            K.DrinksOn(lFire + new Vector3(10.5f, 0.8f, -6f), 0, Z, 3);
            K.Activity(ActivityType.Speech, lFire + new Vector3(0, 0.1f, 3f), 180, Z, 16f);
            K.Activity(ActivityType.Workout, G(GROVE.x - 12f, GROVE.y - 24f, 0.1f), 90, Z);
            for (int i = 0; i < 3; i++) K.NaturePiece("Log", G(GROVE.x - 16f, GROVE.y - 28f + i * 2.5f), 0, 0.8f, LOG, MapKit.Collide.Box);
            K.Prop("Bench", G(GROVE.x - 8f, GROVE.y - 27f), 0, PLANK);

            // Kitchen hall (dining), greenhouse (garden, poison), cabins, outhouses.
            K.Building(Spec("Grove Kitchen"));
            MapKit.KitBuilding lGreen = K.Building(Spec("Greenhouse"));
            Vector3 g = lGreen.root.position;
            for (int i = 0; i < 3; i++)
            {
                K.Prop("Table", g + new Vector3(-2f + i * 2f, 0, 3.5f), 0, PLANK, MapKit.Collide.Box);
                K.Prop("PlantPot", g + new Vector3(-2f + i * 2f, 0.8f, 3.5f), 0, null, MapKit.Collide.None);
                K.Prop("PlantBig", g + new Vector3(-2f + i * 2f, 0, -3.8f), 0, null);
            }
            K.Activity(ActivityType.Garden, g + new Vector3(0, 0.1f, 1.5f), 0, Z);
            K.Gadget(ItemKind.Poison, g + new Vector3(2f, 0.95f, 3.5f));
            K.UniqueWeapon(ItemKind.Flamethrower, g + new Vector3(-3.2f, 0.15f, -1.5f), "Grove greenhouse");
            for (int i = 0; i < 4; i++) K.Building(Spec("Cabin " + i));
            for (int i = 0; i < 2; i++)
            {
                MapKit.KitBuilding lPrivy = K.Building(Spec("Outhouse " + i));
                K.ToiletKit(lPrivy.root.position + new Vector3(0, 0, -0.6f), 0, Z);
            }
            for (int i = 0; i < 5; i++) K.HidingKit(G(GROVE.x + 22f + i * 3.2f, GROVE.y + 30f - i * 1.5f), 200, "Haystack");
            K.DistractionKit(G(GROVE.x - 18, GROVE.y - 2), 90, "radio");
            // Vegetable beds and drying racks between the cabins.
            for (int i = 0; i < 6; i++)
            {
                Nat("FlowerBed", GROVE.x - 30f + (i % 3) * 3f, GROVE.y - 30f + (i / 3) * 2f, 1.2f, 0f, new Color(0.45f, 0.6f, 0.3f), MapKit.Collide.None);
                Nat("FlowerBed", GROVE.x + 24f + (i % 3) * 3f, GROVE.y - 30f + (i / 3) * 2f, 1.2f, 0f, new Color(0.55f, 0.55f, 0.3f), MapKit.Collide.None);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = G(GROVE.x + 14f + i * 4f, GROVE.y + 40f);
                GameObject lRack = K.NaturePiece("Log", p + Vector3.up * 1.8f, 0, 0.9f, LOG, MapKit.Collide.None);
                for (int s = -1; s <= 1; s += 2) { GameObject lPost = K.NaturePiece("Log", p + new Vector3(s * 1.6f, 0.9f, 0), 0, 0.45f, LOG * 0.8f, MapKit.Collide.Box); if (lPost != null) lPost.transform.rotation = Quaternion.Euler(0, 0, 90); }
            }
            K.Activity(ActivityType.Phone, G(GROVE.x + 30, GROVE.y - 18, 0.1f), 0, Z);
            K.Activity(ActivityType.Idle, G(GROVE.x - 20, GROVE.y + 10, 0.1f), 0, Z);
            for (int i = 0; i < 6; i++) Clutter(G(GROVE.x - 32f + (i % 3) * 3f, GROVE.y - 14f + (i / 3) * 3f), 90, i);
            K.Weapon(ItemKind.Shotgun, G(GROVE.x + 32f, GROVE.y + 6f, 0.1f));
            K.Weapon(ItemKind.Pistol, G(GROVE.x - 28f, GROVE.y + 16f, 0.1f));

            // The orchard outside the palisade (garden node), beehives.
            for (int r = 0; r < 6; r++)
                for (int k = 0; k < 7; k++)
                {
                    float x = 146 + r * 7.5f + (k % 2) * 1.5f, z = 272 + k * 9f;
                    Nat("BroadleafC", x, z, Rand(0.45f, 0.6f), -1f, new Color(0.42f, 0.62f, 0.28f));
                }
            for (int i = 0; i < 4; i++) K.Crate(G(146 + i * 4f, 266f), 0.9f, false);
            K.Activity(ActivityType.Garden, G(165, 300, 0.1f), 90, Z);
            K.Activity(ActivityType.Garden, G(180, 320, 0.1f), 180, Z);
            Guard(Z, 160, 262, 180, ItemKind.Smg);

            // Fields of tall grass and hay around the marsh.
            K.Grass(G(18, 252), new Vector2(34, 22), new Color(0.72f, 0.74f, 0.4f));
            K.Grass(G(176, 352), new Vector2(26, 26), new Color(0.74f, 0.72f, 0.42f));
            K.Grass(G(64, 372), new Vector2(34, 16), new Color(0.7f, 0.74f, 0.38f));
            K.Grass(G(150, 236), new Vector2(22, 18), new Color(0.72f, 0.74f, 0.4f));
            K.Grass(G(10, 340), new Vector2(22, 26), new Color(0.7f, 0.72f, 0.38f));
            for (int i = 0; i < 6; i++) K.HidingKit(G(110 + i * 6f, 378 + (i % 2) * 4f), Rand(0, 360), "Haystack");

            // The commune's guard.
            Guard(Z, GROVE.x, GROVE.y + 6f, 180, ItemKind.Pistol);
            Guard(Z, GROVE.x - 30f, GROVE.y + 8f, 270, ItemKind.Smg);
            Guard(Z, 38, 305, 270, ItemKind.Shotgun);
            Walk(Z, ItemKind.Smg, 3f, Polar(GROVE, 200, 36), Polar(GROVE, 250, 36), Polar(GROVE, 330, 36), Polar(GROVE, 20, 32), Polar(GROVE, 150, 36));
            Walk(Z, ItemKind.Shotgun, 4f, Polar(GROVE, 270, RING_R + 12), Polar(GROVE, 340, RING_R + 12), Polar(GROVE, 30, RING_R + 12), Polar(GROVE, 110, RING_R + 12), Polar(GROVE, 160, RING_R + 12), Polar(GROVE, 210, RING_R + 12));
            Walk(Z, ItemKind.Pistol, 5f, new Vector2(100, 368), new Vector2(170, 368), new Vector2(190, 300), new Vector2(170, 250));
            K.Gadget(ItemKind.SmokeGrenade, G(GROVE.x + 4f, GROVE.y - 42f, 0.1f));
            K.Gadget(ItemKind.Coin, G(30, 300, 1.8f));
        }

        /// <summary>A torch: a short stake with a flame.</summary>
        private void Torch(Vector3 pAt)
        {
            GameObject lStake = K.NaturePiece("Log", pAt + Vector3.up * 0.8f, 0, 0.42f, LOG * 0.7f, MapKit.Collide.None);
            if (lStake != null) lStake.transform.rotation = Quaternion.Euler(0, 0, 90);
            K.Lamp(pAt + Vector3.up * 1.75f, new Color(1f, 0.6f, 0.28f), 7f, 1.4f, false);
        }

        /// <summary>A plank walkway of kit pier segments, at ground level or raised on stilts with ramps.</summary>
        private void Walkway(Vector2 pFrom, Vector2 pTo, float pRaise)
        {
            Vector2 lDir = (pTo - pFrom).normalized;
            float lLength = Vector2.Distance(pFrom, pTo);
            int lCount = Mathf.Max(1, Mathf.RoundToInt(lLength / 3f));
            float lYaw = Quaternion.LookRotation(new Vector3(lDir.x, 0, lDir.y)).eulerAngles.y + 90f;
            float lDeck = UPPER + 0.15f + pRaise;
            for (int i = 0; i < lCount; i++)
            {
                Vector2 p = Vector2.Lerp(pFrom, pTo, (i + 0.5f) / lCount);
                K.Prop("PierSegment", new Vector3(p.x, lDeck - 1.5f, p.y), lYaw, null, MapKit.Collide.Mesh);
            }
            if (pRaise > 0.3f)
                for (int e = 0; e < 2; e++)
                {
                    Vector2 lEnd = e == 0 ? pFrom : pTo, lOut = e == 0 ? -lDir : lDir;
                    K.Ramp(K.Env, G(lEnd + lOut * 6f, 0.05f), new Vector3(lEnd.x, lDeck, lEnd.y), 2.2f, PLANK, 0.25f);
                }
        }

        private void Outpost()
        {
            const int Z = 1;
            Vector3 c = new Vector3(OUTPOST.x, HILL, OUTPOST.y);
            K.Hole(R(312.05f, 336.05f, 323.95f, 347.95f));

            // Chain fence (gate south, the quartermaster's gap east, a cut on the north side).
            Color lFence = new Color(0.55f, 0.56f, 0.55f);
            void FenceRun(Vector2 a, Vector2 b) => K.Fence(G(a), G(b), 2.6f, lFence);
            FenceRun(new Vector2(275, 278), new Vector2(325, 278));
            FenceRun(new Vector2(335, 278), new Vector2(385, 278));
            FenceRun(new Vector2(385, 278), new Vector2(385, 300));
            FenceRun(new Vector2(385, 309), new Vector2(385, 382));
            FenceRun(new Vector2(385, 382), new Vector2(338, 382));
            FenceRun(new Vector2(334, 382), new Vector2(275, 382));
            FenceRun(new Vector2(275, 382), new Vector2(275, 278));
            for (int s = -1; s <= 1; s += 2) P("Barrier", 330 + s * 2.2f, 276, 0, null, true);
            Guard(Z, 326, 273, 180, ItemKind.Smg, true);
            Guard(Z, 334, 273, 180, ItemKind.Shotgun, true);
            Tower(Z, 279, 282, 225, OLIVE);
            Tower(Z, 381, 282, 135, OLIVE);
            Tower(Z, 381, 378, 45, OLIVE);
            Tower(Z, 279, 378, 315, OLIVE);
            for (int i = 0; i < 4; i++) { float x = i % 2 == 0 ? 282f : 378f, z = i < 2 ? 286f : 374f; K.Lamp(G(x, z, 3.2f), new Color(1f, 0.92f, 0.75f), 18f, 2f, false); }

            // Barracks (gym), HQ (security, colonel's office), armory, bunker.
            K.Building(Spec("Barracks"));
            K.Building(Spec("Barracks B"));
            K.Building(Spec("Vehicle Shed"));
            // Tent rows, sandbag walls, container stacks along the east fence, floodlit crates.
            for (int i = 0; i < 4; i++) P("TentMilitary", 284, new[] { 316f, 323f, 342f, 349f }[i], 90, OLIVE, true, MapKit.Collide.Mesh);
            for (int i = 0; i < 3; i++)
            {
                P("ShippingContainer", 378, 322 + i * 7f, 90, new[] { OLIVE, new Color(0.55f, 0.35f, 0.25f), new Color(0.4f, 0.45f, 0.5f) }[i], true, MapKit.Collide.Box);
                if (i != 1) P("ShippingContainer", 378, 322 + i * 7f, 90, new Color(0.6f, 0.55f, 0.35f), true, MapKit.Collide.Box, 2.6f);
            }
            for (int i = 0; i < 6; i++) P("Sandbags", 322 + i * 2.1f, 322, 0, null, true);
            for (int i = 0; i < 4; i++) P("Sandbags", 344, 304 + i * 2.1f, 90, null, true);
            for (int i = 0; i < 6; i++) Clutter(G(340 + (i % 3) * 3.2f, 366 + (i / 3) * 3.2f), 0, i);
            K.Activity(ActivityType.Idle, G(326, 318, 0.1f), 180, Z);
            MapKit.KitBuilding lHq = K.Building(Spec("Outpost HQ"));
            MapKit.KitBuilding lArmory = K.Building(Spec("Armory"));
            MapKit.KitBuilding lBunker = K.Building(Spec("Bunker"));
            Vector3 hq = lHq.root.position;
            K.Npc(NpcRole.Guard, hq + new Vector3(-3, 0.1f, -3), 0, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, hq + new Vector3(-3, MapKit.STOREY + 0.1f, 3), 180, Z, ItemKind.Shotgun);
            float lRoof = hq.y + 2 * MapKit.STOREY;
            for (int k = 0; k < 3; k++) K.Piece("Suburb", "Scaffold", new Vector3(hq.x - 3.5f, lRoof + k * 4f, hq.z + 3.5f), 0, null, K.Env);
            K.Lamp(new Vector3(hq.x - 3.5f, lRoof + 12.4f, hq.z + 3.5f), new Color(1f, 0.2f, 0.15f), 6f, 1.5f, false);
            K.Activity(ActivityType.Lookout, new Vector3(hq.x + 3, lRoof + 0.1f, hq.z - 3), 180, Z);
            K.Weapon(ItemKind.Sniper, new Vector3(hq.x + 3.5f, lRoof + 0.15f, hq.z - 4.5f));
            K.Weapon(ItemKind.Smg, hq + new Vector3(4, MapKit.STOREY + 0.95f, -4));
            Vector3 b = lBunker.root.position;
            for (int i = 0; i < 4; i++) K.Prop("Sandbags", new Vector3(b.x - 3f + i * 2f, b.y + MapKit.STOREY, b.z - 5f), 180, null, MapKit.Collide.Box, true);
            K.Npc(NpcRole.Lookout, new Vector3(b.x, b.y + MapKit.STOREY + 0.1f, b.z - 3f), 180, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, b + new Vector3(-3, -MapKit.STOREY + 0.1f, -3), 0, Z, ItemKind.Shotgun);
            K.UniqueWeapon(ItemKind.RocketLauncher, b + new Vector3(-4.2f, -MapKit.STOREY + 0.15f, 4f), "Outpost bunker basement");
            K.Weapon(ItemKind.Shotgun, b + new Vector3(-2.2f, -MapKit.STOREY + 0.95f, -4.5f));
            K.Gadget(ItemKind.C4, b + new Vector3(0, -MapKit.STOREY + 0.15f, -4.8f));

            // The quartermaster's back-door deal: pickup at the fence gap, open crates, a lookout for him.
            Vector3 lDeal = G(392, 304);
            K.Car(lDeal + new Vector3(0, 0, 0), 0, new Color(0.45f, 0.4f, 0.32f));
            K.Crate(G(388, 300), 1.1f);
            K.Crate(G(388, 308), 1.6f);
            K.Activity(ActivityType.Inspect, G(388.5f, 304, 0.1f), 90, Z);
            K.Activity(ActivityType.Smoke, G(381, 296, 0.1f), 90, Z);
            K.Activity(ActivityType.Phone, G(394, 312, 0.1f), 180, Z);
            K.Activity(ActivityType.Drink, G(372, 312, 0.1f), 270, Z);
            P("Table", G(373.2f, 312), 90, PLANK, true);
            K.DrinksOn(G(373.2f, 312, 0.8f), 270, Z, 2);
            K.Weapon(ItemKind.Smg, lArmory.root.position + new Vector3(1, 1.0f, 1));
            K.Weapon(ItemKind.Shotgun, lArmory.root.position + new Vector3(-1, 1.0f, -1));
            K.Weapon(ItemKind.Pistol, G(389, 307, 1.7f));
            Guard(Z, 378, 304, 90, ItemKind.Pistol);

            // Motor pool: jeeps, containers (stacked with a ladder), fuel, the generator.
            for (int i = 0; i < 4; i++) P("Jeep", 289 + i * 4f, 288, 180, OLIVE, true);
            for (int i = 0; i < 3; i++) P("ShippingContainer", 290 + i * 7f, 316, 0, new[] { OLIVE, new Color(0.55f, 0.35f, 0.25f), new Color(0.4f, 0.45f, 0.5f) }[i], true, MapKit.Collide.Box);
            P("ShippingContainer", 297, 316, 0, new Color(0.6f, 0.55f, 0.35f), true, MapKit.Collide.Box, 2.6f);
            Ladder(G(296, 314.4f), 2.7f, 180f);
            for (int i = 0; i < 5; i++) K.Barrel(G(312 + (i % 3) * 0.75f, 298 + (i / 3) * 0.8f), true);
            for (int i = 0; i < 3; i++) P("GasBottle", 316 + i * 0.4f, 300, 0);
            K.DistractionKit(G(318, 292), 0, "generator");
            K.Weapon(ItemKind.Smg, G(306, 292, 0.1f));
            Guard(Z, 300, 304, 90, ItemKind.Smg);

            // Mess tents (dining), the obstacle course (workout), the comms mast with the alarm.
            for (int i = 0; i < 3; i++) P("TentMilitary", 292 + i * 7f, 342, 90, OLIVE, true, MapKit.Collide.Mesh);
            P("Table", 302, 330, 0, PLANK, true);
            P("Table", 306, 330, 0, PLANK, true);
            K.Activity(ActivityType.Dining, G(304, 328.8f, 0.1f), 0, Z);
            K.DrinksOn(G(306, 330, 0.8f), 0, Z, 2);
            for (int i = 0; i < 4; i++) P("Barrier", 338 + i * 5f, 336 + (i % 2) * 3f, 90, null, true);
            for (int i = 0; i < 3; i++) Nat("Log", 344 + i * 4f, 346, 1f, 0f, LOG, MapKit.Collide.Box);
            K.Activity(ActivityType.Workout, G(350, 340, 0.1f), 90, Z);
            K.Activity(ActivityType.Inspect, G(296, 298, 0.1f), 180, Z);
            K.Activity(ActivityType.Office, hq + new Vector3(2, 0.1f, 3), 0, Z);
            Vector3 lMast = G(374, 370);
            for (int k = 0; k < 5; k++) K.Piece("Suburb", "Scaffold", lMast + Vector3.up * (k * 4f), 90, null, K.Env);
            Ladder(lMast + new Vector3(0, 0, -0.9f), 20f, 180f, "Suburb");
            K.Lamp(lMast + Vector3.up * 20.4f, new Color(1f, 0.25f, 0.2f), 8f, 2f, false);
            K.Alarm(G(370, 364), 270);
            Guard(Z, 368, 366, 270, ItemKind.Smg, true);
            K.Weapon(ItemKind.Sniper, lMast + Vector3.up * 16.2f + new Vector3(0.6f, 0, 0));

            // The helipad (extraction).
            Vector3 lPad = G(350, 297);
            K.Block(K.Env, "HelipadH1", lPad + new Vector3(-1.6f, 0.03f, 0), new Vector3(0.6f, 0.06f, 5f), Color.white, Core.GameLayers.DEFAULT, false);
            K.Block(K.Env, "HelipadH2", lPad + new Vector3(1.6f, 0.03f, 0), new Vector3(0.6f, 0.06f, 5f), Color.white, Core.GameLayers.DEFAULT, false);
            K.Block(K.Env, "HelipadH3", lPad + new Vector3(0, 0.03f, 0), new Vector3(2.6f, 0.06f, 0.6f), Color.white, Core.GameLayers.DEFAULT, false);
            for (int i = 0; i < 8; i++) K.Lamp(lPad + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 7.8f + Vector3.up * 0.15f, new Color(1f, 0.85f, 0.3f), 3f, 0.8f, false);
            K.Point(MatchPoint.Kind.Extraction, lPad + Vector3.up * 0.1f, 0, "Outpost helipad");
            Guard(Z, 340, 290, 200, ItemKind.Smg, true);

            // Patrols inside and around the fence.
            Walk(Z, ItemKind.Smg, 3f, new Vector2(284, 286), new Vector2(376, 286), new Vector2(376, 374), new Vector2(284, 374));
            Walk(Z, ItemKind.Shotgun, 3f, new Vector2(330, 300), new Vector2(330, 360), new Vector2(290, 352), new Vector2(345, 320));
            Walk(Z, ItemKind.Smg, 4f, new Vector2(268, 270), new Vector2(392, 270), new Vector2(392, 390), new Vector2(268, 390));
            Walk(Z, ItemKind.Pistol, 5f, new Vector2(270, 262), new Vector2(160, 250), new Vector2(110, 240));
            K.Gadget(ItemKind.SmokeGrenade, G(286, 376, 0.1f));
            K.Gadget(ItemKind.Brick, G(336, 386, 0.1f));
            K.Grass(G(300, 398), new Vector2(40, 14), new Color(0.72f, 0.72f, 0.42f));
            K.Grass(G(400, 330), new Vector2(14, 40), new Color(0.72f, 0.72f, 0.42f));

            K.Zone(Z, K.Map.zoneNames[1], new Vector3(190, UPPER, 310), new Vector3(470, 90, 230), G(405, 250, 0.1f), new Color(0.42f, 0.52f, 0.32f), HatStyle.Beret,
                b + new Vector3(3, -MapKit.STOREY + 0.1f, 3));
        }
    }
}
