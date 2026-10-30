using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Between the districts: the Gulf Highway (elevated over the docks and the wadi, it curves
    /// down onto the Palm Interchange roundabout), the Gulf Road and the corniche (two patrol-car
    /// loops with the souk ring), the dry wadi and its bridges, the Sandgate apartments and the
    /// domed civic hall between the souk and the docks, the old caravanserai, the embassy villas
    /// north of the Zenith, the oasis camp in the dunes and the desert road petrol station (car
    /// extraction).
    /// </summary>
    public partial class MapGulf
    {
        private static readonly Vector2 ROUNDABOUT = new Vector2(170, -120);
        private static readonly Vector2 CARAVANSERAI = new Vector2(40, 120);
        private static readonly Vector2 OASIS = new Vector2(300, 300);
        private static readonly Vector2 PETROL = new Vector2(196, 344);

        /// <summary>The highway centre line and deck top, from the west edge over the docks to the roundabout.</summary>
        private static readonly Vector3[] HIGHWAY =
        {
            new Vector3(-500, HWY_TOP, HWY_Z), new Vector3(-60, HWY_TOP, HWY_Z), new Vector3(-10, HWY_TOP, -255), new Vector3(40, HWY_TOP, -238), new Vector3(85, 10f, -210),
            new Vector3(118, 8f, -178), new Vector3(140, 5.5f, -152), new Vector3(155, YARD + 0.05f, -137)
        };

        private readonly List<(Vector2 a, Vector2 b)> _bridges = new List<(Vector2, Vector2)>();

        private void PlanBetween()
        {
            Road(12f, ASPHALT, new Vector2(-440, -120), new Vector2(-300, -120), new Vector2(-128, -120), new Vector2(60, -120), ROUNDABOUT, new Vector2(330, -120), new Vector2(495, -118));
            Road(14f, ASPHALT, new Vector2(175, -324), new Vector2(172, -200), new Vector2(170, -142));
            Road(12f, ASPHALT, new Vector2(170, -98), new Vector2(170, 60), new Vector2(170, 200), new Vector2(168, 360), new Vector2(170, 500));
            Road(9f, ASPHALT, new Vector2(-178, 172), new Vector2(-110, 174), new Vector2(-40, 176), new Vector2(60, 180), new Vector2(170, 182));
            Road(8f, ASPHALT, new Vector2(-440, -40), new Vector2(-150, -40));
            Lane(6f, COBBLE, new Vector2(38.5f, 137), new Vector2(40, 180));
            Lane(5f, COBBLE, new Vector2(38.5f, 103), new Vector2(30, 60), new Vector2(10, 20));
            Lane(6f, SCRUB * 0.9f, new Vector2(170, 300), new Vector2(240, 300), OASIS);
            _zoneStage.Add(() => FlattenDisc(ROUNDABOUT, 30f, YARD, 20f));
            _zoneStage.Add(() => Flatten(R(-445, -150, -150, 30), 2.5f, 25f));
            _zoneStage.Add(() => Flatten(R(-130, -345, 176, -310), YARD, 14f));
            _zoneStage.Add(() => FlattenDisc(CARAVANSERAI, 30f, Surf0(CARAVANSERAI), 20f));
            _zoneStage.Add(() => FlattenDisc(OASIS, 26f, 7f, 18f));
            _zoneStage.Add(() => FlattenDisc(PETROL, 26f, 6.5f, 18f));
            _zoneStage.Add(() => Flatten(R(-60, -80, 70, 60), 2.5f, 20f));
            _zoneStage.Add(() => Flatten(R(240, -84, 430, -36), 2f, 16f));
            _zoneStage.Add(() => Flatten(R(-6, -110, 136, -74), YARD, 12f));
            _zoneStage.Add(() => Flatten(R(356, 56, 476, 206), 8f, 20f));
            PaintRect(R(-6, -110, 136, -74), CONCRETE);
            _tame.Add(R(-8, -112, 138, -72));
            _tame.Add(R(354, 54, 478, 208));
            _zoneStage.Add(() => FlattenDisc(new Vector2(302, 178), 18f, 9f, 16f));
            _tame.Add(R(284, 160, 322, 196));
            _tame.Add(R(-62, -82, 72, 62));
            PaintDisc(ROUNDABOUT, 24f, ASPHALT, 0);
            PaintDisc(ROUNDABOUT, 12f, LAWN, BUILT);
            PaintDisc(OASIS, 9f, new Color(0.62f, 0.6f, 0.4f));
            PaintRect(R(-218, -20, -170, 12), new Color(0.6f, 0.62f, 0.4f));
            PaintRect(R(-125, -344, 176, -330), PAVE);
            PaintRect(R(PETROL.x - 14, PETROL.y - 12, PETROL.x + 26, PETROL.y + 14), CONCRETE);
            _tame.Add(R(-450, -160, -140, 40));
            _tame.Add(new Rect(CARAVANSERAI.x - 32, CARAVANSERAI.y - 32, 64, 64));
            _tame.Add(new Rect(PETROL.x - 18, PETROL.y - 16, 48, 34));
            _tame.Add(new Rect(ROUNDABOUT.x - 30, ROUNDABOUT.y - 30, 60, 60));

            // The Sandgate apartments and the domed civic hall.
            (float x, float z, float yaw, int floors)[] lBlocks = { (-400, -72, 0, 4), (-355, -72, 0, 5), (-252, -72, 0, 4), (-400, -6, 180, 5), (-252, -6, 180, 4) };
            for (int i = 0; i < lBlocks.Length; i++)
            {
                MapKit.KitSpec a = Lot("Sandgate Block " + i, "Stucco", lBlocks[i].x, lBlocks[i].z, lBlocks[i].yaw, 6, 4, lBlocks[i].floors, Roof.Flat, SOUK_WALLS[(i * 3 + 1) % SOUK_WALLS.Length] * 0.97f, SOUK_ROOF, 0, 2.5f, 5f);
                Doors(a, 0, 2, 2, 1);
                a.windowChance = 0.55f;
                a.balconyChance = 0.3f;
                a.climb = new Vector2Int(3, 1);
                Floors(a, Room.Lounge, Room.Kitchen, Room.Bedroom, Room.Bathroom);
                for (int f = 2; f < lBlocks[i].floors; f++) a.rooms.Add(new Room[0]);
            }
            MapKit.KitSpec h = Lot("Sandgate Civic Hall", "Stucco", -340f, -4f, 180, 6, 6, 2, Roof.Flat, new Color(0.97f, 0.93f, 0.84f), SOUK_ROOF, 0, 2.5f, 5f);
            Doors(h, 0, 2, 0, 3, 2, 1, 1, 2, 3, 2);
            h.arches.Add(new Vector2Int(0, 1));
            h.arches.Add(new Vector2Int(0, 4));
            h.tallWindows = true;
            Floors(h, Room.Ballroom, Room.Office, Room.Office, Room.Lounge);

            // The caravanserai: four wings around a courtyard, arches on the court, corner passages.
            float cx = CARAVANSERAI.x, cz = CARAVANSERAI.y;
            MapKit.KitSpec n = Lot("Caravanserai North", "Stucco", cx, cz + 10.5f, 0, 10, 3, 2, Roof.Flat, new Color(0.9f, 0.78f, 0.6f), SOUK_ROOF, 0, float.NaN, 6f);
            MapKit.KitSpec s = Lot("Caravanserai South", "Stucco", cx, cz - 10.5f, 180, 10, 3, 1, Roof.Flat, new Color(0.9f, 0.78f, 0.6f), SOUK_ROOF, 0, float.NaN, 6f);
            MapKit.KitSpec w = Lot("Caravanserai West", "Stucco", cx - 10.5f, cz, 270, 3, 3, 1, Roof.Flat, new Color(0.88f, 0.76f, 0.58f), SOUK_ROOF, 0, float.NaN, 6f);
            MapKit.KitSpec e = Lot("Caravanserai East", "Stucco", cx + 10.5f, cz, 90, 3, 3, 1, Roof.Flat, new Color(0.88f, 0.76f, 0.58f), SOUK_ROOF, 0, float.NaN, 6f);
            foreach (MapKit.KitSpec c in new[] { n, s, w, e })
            {
                c.windowChance = 0.25f;
                c.balconyChance = 0f;
                for (int m = 0; m < c.width; m++) if (m != c.width / 2) c.arches.Add(new Vector2Int(0, m));
                Doors(c, 0, c.width / 2);
            }
            Doors(s, 2, 4);
            Doors(n, 2, 5);
            n.climb = new Vector2Int(2, 2);
            Floors(n, Room.Dining, Room.None, Room.Bedroom, Room.None);
            Floors(s, Room.Storage, Room.None);
            Floors(w, Room.Storage, Room.None);
            Floors(e, Room.Storage, Room.None);

            // The embassy villas north of the Zenith, the petrol station shop.
            for (int i = 0; i < 3; i++)
            {
                MapKit.KitSpec v = Lot("Embassy Villa " + i, "Classic", 262f + i * 72f, -62f, 0, 5, 4, 2, Roof.Flat, new[] { new Color(0.96f, 0.93f, 0.86f), new Color(0.92f, 0.86f, 0.8f), new Color(0.97f, 0.95f, 0.9f) }[i], new Color(0.6f, 0.6f, 0.62f), 2, 2f, 6f);
                Doors(v, 0, 2, 2, 1);
                v.tallWindows = true;
                v.balconyChance = 0.35f;
                Floors(v, Room.Lounge, Room.Dining, Room.Office, Room.Bedroom);
            }
            for (int i = 0; i < 3; i++)
            {
                MapKit.KitSpec d = Lot("Gulf Road Depot " + i, "Stucco", 14f + i * 42f, -92f, 180, 8, 5, 1, Roof.Flat, new[] { new Color(0.82f, 0.8f, 0.76f), new Color(0.78f, 0.72f, 0.62f), new Color(0.75f, 0.78f, 0.8f) }[i], new Color(0.55f, 0.56f, 0.56f), 1, YARD, 5f);
                Doors(d, 0, 3, 0, 4, 2, 2);
                d.windowChance = 0.12f;
                d.balconyChance = 0f;
                d.rooms.Add(new[] { Room.Storage });
            }
            MapKit.KitSpec lShop = Lot("Desert Stop", "Stucco", PETROL.x + 16f, PETROL.y + 6f, 180, 4, 3, 1, Roof.Flat, new Color(0.95f, 0.9f, 0.8f), new Color(0.6f, 0.6f, 0.62f), 2, 6.5f, 5f);
            Doors(lShop, 0, 1, 0, 2);
            Floors(lShop, Room.Shop, Room.Storage);
        }

        /// <summary>The base height of the terrain at a point (before any stage), for pads that follow the land.</summary>
        private static float Surf0(Vector2 p) => BaseHeight(p.x, p.y);

        private void Between()
        {
            Bridges();
            Corniche();
            Interchange();
            Sandgate();
            Caravanserai();
            Villas();
            PalmPark();
            Oasis();
            PetrolStation();
            Wadi();
            Avenues();
            Depots();
            WaterTower();
            SolarFarm();
            FortRuin();
            RelayStation();
            BusTerminal();
            DryGrass();

            Cruiser(10f, 0f, new Color(0.9f, 0.9f, 0.92f), new Vector2(-128, -322), new Vector2(172, -324), new Vector2(172, -146), new Vector2(152, -124), new Vector2(-128, -120));
            Cruiser(8f, 140f, new Color(0.12f, 0.14f, 0.2f), new Vector2(-430, 70), new Vector2(-178, 70), new Vector2(-178, 318), new Vector2(-430, 318), new Vector2(-449, 300), new Vector2(-449, 88));
        }

        /// <summary>Stone bridges where the roads cross the wadi (the road's bridged stretch, plus a little).</summary>
        private void Bridges()
        {
            foreach (RoadDef lRoad in _roads)
            {
                List<Vector2> lSamples = new List<Vector2>();
                for (int s = 0; s < lRoad.points.Length - 1; s++)
                {
                    int lSteps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(lRoad.points[s], lRoad.points[s + 1])));
                    for (int k = 0; k < lSteps; k++) lSamples.Add(Vector2.Lerp(lRoad.points[s], lRoad.points[s + 1], (float)k / lSteps));
                }
                int lFirst = -1;
                for (int k = 0; k < lSamples.Count; k++)
                {
                    bool lIn = Nearest(WADI, lSamples[k]).distance < 11.5f;
                    if (lIn && lFirst < 0) lFirst = k;
                    if ((!lIn || k == lSamples.Count - 1) && lFirst >= 0)
                    {
                        Vector2 a = lSamples[Mathf.Max(0, lFirst - 3)], b = lSamples[Mathf.Min(lSamples.Count - 1, k + 2)];
                        _bridges.Add((a, b));
                        Span(G(a, 0.02f), G(b, 0.02f), lRoad.width + 1.5f, true, 5f, "Stucco");
                        lFirst = -1;
                    }
                }
            }
            // A plank footbridge by the palm park.
            Span(G(-136, 40, 0.3f), G(-96, 40, 0.3f), 2.6f, false, 4f, "Timber");
            Debug.Log($"[Build] Map_Gulf: {_bridges.Count} wadi road bridges");
        }

        /// <summary>The elevated Gulf Highway: deck segments with joint pads, barriers, pillars and cap beams, lamps, signs, a maintenance stair.</summary>
        private void Highway()
        {
            const int Z = 1;
            Color lDeck = new Color(0.5f, 0.5f, 0.5f), lPier = new Color(0.72f, 0.7f, 0.66f), lBarrier = new Color(0.82f, 0.8f, 0.76f);
            const float T = 1.2f, HALF = HWY_WIDTH * 0.5f;
            Vector2 lStairGap = new Vector2(-208f, HWY_Z + HALF);
            for (int i = 0; i < HIGHWAY.Length - 1; i++)
            {
                Vector3 a = HIGHWAY[i], b = HIGHWAY[i + 1];
                GameObject lSeg = K.Ramp(K.Env, a, b, HWY_WIDTH, lDeck, T);
                lSeg.name = "HighwayDeck";
                Quaternion lRot = Quaternion.LookRotation(b - a);
                Vector3 lFlat = new Vector3(b.x - a.x, 0, b.z - a.z);
                Vector3 lAcross = Vector3.Cross(Vector3.up, lFlat.normalized);
                // Barriers on both sides, split around the stair gap.
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 ea = a + lAcross * (s * (HALF - 0.2f)), eb = b + lAcross * (s * (HALF - 0.2f));
                    float lLen = Vector3.Distance(ea, eb);
                    List<Vector2> lCuts = new List<Vector2>();
                    Vector2 lFrom2 = new Vector2(ea.x, ea.z), lTo2 = new Vector2(eb.x, eb.z);
                    float lT = Vector2.Dot(lStairGap - lFrom2, (lTo2 - lFrom2).normalized);
                    if (Vector2.Distance(lFrom2 + (lTo2 - lFrom2).normalized * lT, lStairGap) < 0.8f && lT > 0f && lT < lLen) lCuts.Add(new Vector2(lT - 1.8f, lT + 1.8f));
                    float lStart = 0f;
                    lCuts.Add(new Vector2(lLen + 0.5f, lLen + 1f));
                    foreach (Vector2 lCut in lCuts)
                    {
                        float lEnd = Mathf.Min(lCut.x, lLen + 0.3f);
                        if (lEnd - lStart > 0.5f)
                        {
                            Vector3 p0 = Vector3.Lerp(ea, eb, lStart / lLen), p1 = Vector3.Lerp(ea, eb, lEnd / lLen);
                            K.Block(K.Env, "Barrier", (p0 + p1) * 0.5f + Vector3.up * 0.55f, new Vector3(0.4f, 1.1f, Vector3.Distance(p0, p1)), lBarrier, GameLayers.DEFAULT, true, Quaternion.LookRotation(p1 - p0), true);
                        }
                        lStart = lCut.y;
                    }
                }
                // Joint pad at the inner points (covers the wedge between two segments).
                if (i > 0) K.Mesh(K.Env, "DeckJoint", CylMesh, a - Vector3.up * T * 0.5f, new Vector3(HWY_WIDTH + 0.4f, T, HWY_WIDTH + 0.4f), lDeck, null, true).isStatic = true;
                // Pillars every ~24 m where the deck is high enough.
                float lLength = lFlat.magnitude;
                for (float d = (i == 0 ? 12f : 0f); d < lLength; d += 24f)
                {
                    Vector3 p = Vector3.Lerp(a, b, d / lLength);
                    float g = Surf(p.x, p.z);
                    float lUnder = p.y - T - 1f;
                    if (lUnder - g < 2.4f) continue;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 c = p + lAcross * (s * 5f);
                        float gc = Mathf.Min(Surf(c.x, c.z), g) - 0.5f;
                        K.Mesh(K.Env, "Pillar", CylMesh, new Vector3(c.x, (gc + lUnder) * 0.5f, c.z), new Vector3(1.7f, lUnder - gc, 1.7f), lPier, null, true).isStatic = true;
                    }
                    K.Block(K.Env, "CapBeam", p - Vector3.up * (T + 0.5f), new Vector3(13f, 1f, 2f), lPier, GameLayers.DEFAULT, true, Quaternion.LookRotation(lFlat));
                }
                // Lamps every 40 m, alternating sides.
                for (float d = 20f; d < lLength; d += 40f)
                {
                    Vector3 p = Vector3.Lerp(a, b, d / lLength) + lAcross * (((int)(d / 40f) % 2 == 0 ? 1 : -1) * (HALF - 0.6f));
                    K.Prop("StreetLamp", p, Quaternion.LookRotation(-lAcross).eulerAngles.y, null, MapKit.Collide.Box);
                    K.Lamp(p + Vector3.up * 4.3f, WARM, 14f, 1.2f, false);
                }
                // Lane markings.
                K.Block(K.Env, "LaneLine", (a + b) * 0.5f + Vector3.up * 0.01f, new Vector3(0.2f, 0.02f, lLength), Color.white, GameLayers.DEFAULT, false, lRot);
            }
            // The sign gantry over the deck at the docks end.
            Vector3 lSign = new Vector3(-140f, HWY_TOP, HWY_Z);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "SignPost", lSign + new Vector3(0, 3.2f, s * (HALF - 0.3f)), new Vector3(0.4f, 6.4f, 0.4f), new Color(0.5f, 0.5f, 0.52f));
            K.Block(K.Env, "SignBeam", lSign + new Vector3(0, 6.2f, 0), new Vector3(0.4f, 0.4f, HWY_WIDTH), new Color(0.5f, 0.5f, 0.52f));
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Sign", lSign + new Vector3(0.3f, 5.4f, s * 3.6f), new Vector3(0.1f, 1.8f, 5.6f), new Color(0.1f, 0.42f, 0.25f));
            // The maintenance stair from the yard, abandoned cars and a broken-down truck on the deck.
            K.Stairs(K.Env, new Vector3(-208f, YARD, -232f), new Vector3(-208f, HWY_TOP, HWY_Z + HALF - 0.1f), 2.4f, new Color(0.55f, 0.55f, 0.55f));
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "StairRail", new Vector3(-208f + s * 1.25f, (YARD + HWY_TOP) * 0.5f + 1f, (-232f + HWY_Z + HALF) * 0.5f), new Vector3(0.08f, 0.08f, Vector3.Distance(new Vector3(0, YARD, -232f), new Vector3(0, HWY_TOP, HWY_Z + HALF))), new Color(0.9f, 0.75f, 0.2f), GameLayers.DEFAULT, false, Quaternion.LookRotation(new Vector3(0, HWY_TOP - YARD, HWY_Z + HALF + 232f)));
            K.Car(new Vector3(-300f, HWY_TOP, HWY_Z - 3.5f), 92f, new Color(0.7f, 0.15f, 0.12f));
            K.Car(new Vector3(-420f, HWY_TOP, HWY_Z + 3.5f), 265f, new Color(0.85f, 0.85f, 0.82f));
            K.Car(new Vector3(-180f, HWY_TOP, HWY_Z + 3f), 270f, new Color(0.3f, 0.35f, 0.4f));
            K.Prop("TrafficCone", new Vector3(-176f, HWY_TOP, HWY_Z + 6f), 0);
            K.Prop("TrafficCone", new Vector3(-173f, HWY_TOP, HWY_Z + 5f), 0);
            K.Patrol(Z, ItemKind.Smg, 5f, new Vector3(-470f, HWY_TOP + 0.1f, HWY_Z), new Vector3(-150f, HWY_TOP + 0.1f, HWY_Z), new Vector3(-150f, HWY_TOP + 0.1f, HWY_Z - 3f), new Vector3(-470f, HWY_TOP + 0.1f, HWY_Z - 3f));
            K.Npc(NpcRole.Lookout, new Vector3(-330f, HWY_TOP + 0.1f, HWY_Z - 6f), 180f, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, new Vector3(-322f, HWY_TOP + 0.15f, HWY_Z - 6.3f));
            K.Gadget(ItemKind.Brick, new Vector3(-176f, HWY_TOP + 0.1f, HWY_Z + 4f));
        }

        /// <summary>The corniche: a seaside promenade along the beach with palms, lamps, benches and kiosks.</summary>
        private void Corniche()
        {
            for (int i = 0; i < 20; i++)
            {
                float x = -118f + i * 15f;
                if (Mathf.Abs(x + 50f) < 16f) continue;
                Palm(x, -336f, 1.1f);
                Palm(x + 7.5f, -314f, 0.9f);
                if (i % 2 == 0) { P("StreetLamp", x + 7.5f, -333f, 0); K.Lamp(G(x + 7.5f, -332f, 4.3f), WARM, 13f, 1.3f, false); }
                else { P("Bench", x + 7.5f, -338f, 180f); P("TrashBin", x + 9.5f, -338f, 180f); }
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 k = G(-10f + i * 120f, -339f);
                K.Block(K.Env, "Kiosk", k + Vector3.up * 1.3f, new Vector3(3f, 2.6f, 3f), new Color(0.95f, 0.9f, 0.8f), GameLayers.DEFAULT, true, null, true);
                K.Block(K.Env, "KioskRoof", k + Vector3.up * 2.75f, new Vector3(4.2f, 0.3f, 4.2f), FABRIC[i * 2 + 1]);
                for (int t = 0; t < 2; t++) Shade(k + new Vector3(-4f + t * 8f, 0, -2f), 0, FABRIC[(i + t) % FABRIC.Length], true);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = -100f + i * 42f;
                K.Prop("Boat", new Vector3(x, SEA - 0.2f, BeachLine(x) - 3f), Rand(150f, 210f), FABRIC[i % FABRIC.Length], MapKit.Collide.Box);
            }
            K.Weapon(ItemKind.Pistol, G(-10f, -341.2f, 1.1f));
            K.Gadget(ItemKind.Coin, G(110f, -339f, 0.1f));
        }

        /// <summary>The Palm Interchange: the roundabout where the highway ramp lands, its island monument, signs and lamps.</summary>
        private void Interchange()
        {
            Vector3 c = G(ROUNDABOUT);
            K.Mesh(K.Env, "IslandRim", CylMesh, c + Vector3.up * 0.2f, new Vector3(24.6f, 0.4f, 24.6f), STONE, null, true);
            for (int i = 0; i < 6; i++) Palm(ROUNDABOUT.x + Mathf.Cos(i * Mathf.PI / 3f) * 8f, ROUNDABOUT.y + Mathf.Sin(i * Mathf.PI / 3f) * 8f, 1.15f);
            // The monument: a tall bronze sail on a stone plinth (the city's emblem).
            K.Block(K.Env, "Plinth", c + Vector3.up * 1.2f, new Vector3(4f, 2.4f, 4f), STONE * 0.9f);
            K.Mesh(K.Env, "Sail", ConeMesh, c + Vector3.up * 2.4f, new Vector3(3.2f, 16f, 1.2f), new Color(0.75f, 0.55f, 0.3f), Quaternion.Euler(0, 30f, 6f), true);
            for (int i = 0; i < 8; i++)
            {
                Vector2 p = ROUNDABOUT + new Vector2(Mathf.Cos(i * Mathf.PI / 4f + 0.2f), Mathf.Sin(i * Mathf.PI / 4f + 0.2f)) * 27f;
                if (Flag(p.x, p.y, ROAD) && Vector2.Distance(p, ROUNDABOUT) < 26f) continue;
                P("StreetLamp", p.x, p.y, 0);
                K.Lamp(G(p.x, p.y, 4.3f), WARM, 14f, 1.3f, false);
            }
            for (int i = 0; i < 2; i++) P("StreetSign", ROUNDABOUT.x + (i == 0 ? -30f : 30f), ROUNDABOUT.y + 9f, i == 0 ? 90f : 270f);
            Guard(2, ROUNDABOUT.x + 28f, ROUNDABOUT.y - 18f, 225, ItemKind.Smg);
            K.Car(G(ROUNDABOUT.x + 32f, ROUNDABOUT.y - 22f), 30f, new Color(0.12f, 0.14f, 0.2f));
        }

        /// <summary>The Sandgate apartments: blocks with balconies, the domed civic hall, a dusty football pitch, parked cars.</summary>
        private void Sandgate()
        {
            const int Z = 0;
            for (int i = 0; i < 5; i++)
            {
                MapKit.KitBuilding b = K.Building(Spec("Sandgate Block " + i));
                RoofDressing(b, i + 2);
                K.Car(G(b.root.position.x + 13f, b.root.position.z + (b.Yaw < 90f ? -9f : 9f)), 90f, FABRIC[(i + 3) % FABRIC.Length] * 0.8f);
                if (i % 2 == 0) K.HidingKit(G(b.root.position.x - 12f, b.root.position.z), 90, "Dumpster");
                Palm(b.root.position.x - 13f, b.root.position.z + (b.Yaw < 90f ? -10f : 10f), 0.95f);
            }
            MapKit.KitBuilding h = K.Building(Spec("Sandgate Civic Hall"));
            float lRoof = RoofY(h.spec);
            K.Mesh(K.Env, "HallDrum", CylMesh, new Vector3(h.root.position.x, lRoof + 1.2f, h.root.position.z), new Vector3(11f, 2.4f, 11f), new Color(0.95f, 0.9f, 0.8f), null, true).isStatic = true;
            K.Mesh(K.Env, "HallDome", DomeMesh, new Vector3(h.root.position.x, lRoof + 2.3f, h.root.position.z), new Vector3(11.4f, 9f, 11.4f), new Color(0.3f, 0.55f, 0.6f), null, true).isStatic = true;
            K.Mesh(K.Env, "HallFinial", DomeMesh, new Vector3(h.root.position.x, lRoof + 7f, h.root.position.z), Vector3.one * 0.7f, ZENITH_GOLD);
            for (int i = 0; i < 4; i++) Nat("CypressA", h.root.position.x + (i % 2 == 0 ? -12f : 12f), h.root.position.z + (i < 2 ? -6f : 6f), 0.75f);
            // The pitch: lines and goals.
            Vector3 lPitch = G(-194, -4);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(K.Env, "Goal", lPitch + new Vector3(s * 22f, 1.1f, 0), new Vector3(0.12f, 2.2f, 7f), Color.white, GameLayers.DEFAULT, false);
                K.Block(K.Env, "GoalBar", lPitch + new Vector3(s * 22f, 2.2f, 0), new Vector3(0.15f, 0.15f, 7.2f), Color.white, GameLayers.DEFAULT, false);
            }
            K.Block(K.Env, "Halfway", lPitch + Vector3.up * 0.02f, new Vector3(0.15f, 0.02f, 30f), Color.white, GameLayers.DEFAULT, false);
            for (int i = 0; i < 5; i++) { P("StreetLamp", -430f + i * 70f, -46.5f, 180); K.Lamp(G(-430f + i * 70f, -47.5f, 4.3f), WARM, 12f, 1.3f, false); }
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(-430, -36), new Vector2(-160, -36), new Vector2(-160, -44), new Vector2(-430, -44));
            K.Weapon(ItemKind.Smg, G(-196, 14, 0.15f));
            K.Weapon(ItemKind.Pistol, h.Local(6f, MapKit.STOREY + 0.15f, -5f));
            K.Gadget(ItemKind.SmokeGrenade, G(-300, -40, 0.1f));
        }

        private void Caravanserai()
        {
            const int Z = 0;
            foreach (string lWing in new[] { "North", "South", "West", "East" }) K.Building(Spec("Caravanserai " + lWing));
            MapKit.KitSpec n = Spec("Caravanserai North");
            float lRoof = RoofY(n);
            for (int i = 0; i < 2; i++)
                K.Mesh(K.Env, "Dome", DomeMesh, new Vector3(CARAVANSERAI.x - 7f + i * 14f, lRoof - 0.3f, n.origin.z + 0.5f), new Vector3(6f, 5f, 6f), new Color(0.92f, 0.85f, 0.72f), null, true).isStatic = true;
            // The court: a well, a trader's tent, bales, sacks, pots, palms; the guards; the hidden rocket launcher.
            Vector3 c = G(CARAVANSERAI);
            K.Mesh(K.Env, "Well", CylMesh, c + Vector3.up * 0.5f, new Vector3(2.4f, 1f, 2.4f), STONE * 0.85f, null, true);
            Shade(G(CARAVANSERAI.x + 3.5f, CARAVANSERAI.y + 3f), 0, new Color(0.85f, 0.75f, 0.55f), false);
            K.Prop("Rug", G(CARAVANSERAI.x + 3.5f, CARAVANSERAI.y + 3f, 0.02f), 0, FABRIC[3], MapKit.Collide.None);
            Sacks(G(CARAVANSERAI.x - 4f, CARAVANSERAI.y - 3.5f), 0, 6);
            Pots(G(CARAVANSERAI.x - 4.5f, CARAVANSERAI.y + 4f), 3);
            K.HidingKit(G(CARAVANSERAI.x + 4f, CARAVANSERAI.y - 4f), 0, "Haystack");
            for (int i = 0; i < 4; i++) Palm(CARAVANSERAI.x + (i % 2 == 0 ? -22f : 22f), CARAVANSERAI.y + (i < 2 ? -20f : 20f), 1.1f);
            K.Npc(NpcRole.Guard, c + new Vector3(-2f, 0.1f, -2f), 180, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Lookout, new Vector3(CARAVANSERAI.x + 10f, lRoof + 0.1f, n.origin.z), 0, Z, ItemKind.Sniper);
            K.UniqueWeapon(ItemKind.RocketLauncher, Spec("Caravanserai East").origin + new Vector3(0, 0.15f, 2.5f), "Caravanserai storeroom");
            K.Weapon(ItemKind.Shotgun, Spec("Caravanserai West").origin + new Vector3(0, 0.15f, -2.5f));
            K.Gadget(ItemKind.Poison, G(CARAVANSERAI.x - 4f, CARAVANSERAI.y - 1.5f, 0.6f));
        }

        private void Villas()
        {
            const int Z = 2;
            for (int i = 0; i < 3; i++)
            {
                MapKit.KitBuilding v = K.Building(Spec("Embassy Villa " + i));
                Vector3 o = v.root.position;
                // A walled garden with a gate on the road side, a pool behind, palms, a guard at the gate.
                Color lWall = new Color(0.95f, 0.9f, 0.82f);
                float y = o.y - 0.3f;
                K.Fence(new Vector3(o.x - 16f, y, o.z - 18f), new Vector3(o.x - 3f, y, o.z - 18f), 2f, lWall, true);
                K.Fence(new Vector3(o.x + 3f, y, o.z - 18f), new Vector3(o.x + 16f, y, o.z - 18f), 2f, lWall, true);
                K.Fence(new Vector3(o.x - 16f, y, o.z - 18f), new Vector3(o.x - 16f, y, o.z + 22f), 2f, lWall, true);
                K.Fence(new Vector3(o.x + 16f, y, o.z - 18f), new Vector3(o.x + 16f, y, o.z + 22f), 2f, lWall, true);
                K.Fence(new Vector3(o.x - 16f, y, o.z + 22f), new Vector3(o.x + 16f, y, o.z + 22f), 2f, lWall, true);
                Vector3 lPool = new Vector3(o.x, o.y, o.z + 14f);
                K.Block(K.Env, "VillaPoolRim", lPool + Vector3.up * 0.25f, new Vector3(9f, 0.5f, 5f), STONE, GameLayers.DEFAULT, true, null, true);
                K.Water(lPool + Vector3.up * 0.48f, new Vector2(8.4f, 4.4f));
                for (int k = 0; k < 4; k++) Palm(o.x + (k % 2 == 0 ? -13f : 13f), o.z + (k < 2 ? -14f : 18f), 0.9f);
                Shade(new Vector3(o.x - 9f, o.y, o.z + 14f), 0, FABRIC[i + 2], true);
                if (i != 1) Guard(Z, o.x - 1.5f, o.z - 19.5f, 180, ItemKind.Smg);
                K.Weapon(i == 1 ? ItemKind.Shotgun : ItemKind.Pistol, v.Local(5f, MapKit.STOREY + 0.15f, -4f));
            }
            K.Activity(ActivityType.Garden, new Vector3(Spec("Embassy Villa 1").origin.x + 9f, Spec("Embassy Villa 1").origin.y + 0.1f, Spec("Embassy Villa 1").origin.z + 16f), 0, Z);
        }

        /// <summary>Palm avenues along the main roads, power lines along the desert highway, billboards by the interchange.</summary>
        private void Avenues()
        {
            void Line(Vector2 a, Vector2 b, float pStep, float pOffset, bool pBoth)
            {
                Vector2 lDir = (b - a).normalized, lSide = new Vector2(-lDir.y, lDir.x);
                float lLength = Vector2.Distance(a, b);
                for (float d = pStep * 0.5f; d < lLength; d += pStep)
                    for (int s = pBoth ? -1 : 1; s <= 1; s += 2)
                    {
                        Vector2 p = a + lDir * d + lSide * (s * pOffset);
                        if (Flag(p.x, p.y, ROAD | BUILT | WET | BANK)) continue;
                        Palm(p.x, p.y, 1.05f);
                    }
            }
            Line(new Vector2(-120, -120), new Vector2(140, -120), 18f, 10f, true);
            Line(new Vector2(200, -119), new Vector2(250, -119), 18f, 10f, true);
            Line(new Vector2(360, -119), new Vector2(495, -118), 18f, 10f, true);
            Line(new Vector2(175, -318), new Vector2(172, -150), 12f, 9.5f, true);
            Line(new Vector2(170, -90), new Vector2(170, 470), 22f, 9f, true);
            Line(new Vector2(-170, 172), new Vector2(160, 182), 20f, 7.5f, false);
            for (float z = -80f; z < 480f; z += 42f) P("PowerPole", 182f, z, 0);
            for (int i = 0; i < 3; i++)
            {
                Vector3 b = G(120f + i * 70f, -138f - (i % 2) * 6f);
                for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "BillboardLeg", b + new Vector3(s * 3f, 3.5f, 0), new Vector3(0.35f, 7f, 0.35f), new Color(0.4f, 0.4f, 0.42f));
                K.Block(K.Env, "Billboard", b + new Vector3(0, 8.5f, 0), new Vector3(9f, 4f, 0.3f), new Color(0.95f, 0.93f, 0.88f));
                K.Block(K.Env, "BillboardArt", b + new Vector3(0, 8.5f, -0.17f), new Vector3(8.4f, 3.4f, 0.05f), FABRIC[(i * 3 + 1) % FABRIC.Length], GameLayers.DEFAULT, false);
            }
        }

        /// <summary>The Gulf Road depots: low sheds with yards, trucks, pallets and a forklift's worth of crates.</summary>
        private void Depots()
        {
            for (int i = 0; i < 3; i++)
            {
                MapKit.KitBuilding d = K.Building(Spec("Gulf Road Depot " + i));
                StripBands(d);
                Vector3 o = d.root.position;
                K.Car(G(o.x - 8f, o.z - 13f), 90f, new Color(0.9f, 0.9f, 0.88f));
                for (int k = 0; k < 3; k++) Clutter(G(o.x + 4f + k * 3.5f, o.z - 12f), 0, k + i);
                if (i == 1) K.HidingKit(G(o.x + 14f, o.z), 90, "Dumpster");
            }
            K.Weapon(ItemKind.Shotgun, Spec("Gulf Road Depot 1").origin + new Vector3(4f, 0.15f, 2f));
        }

        /// <summary>The water tower on the plain: a tank on lattice legs, a ladder and a sniper's perch.</summary>
        private void WaterTower()
        {
            Vector3 b = G(92, 34);
            const float H = 22f;
            for (int i = 0; i < 4; i++) K.Block(K.Env, "TowerLeg", b + new Vector3(i % 2 == 0 ? -3f : 3f, H * 0.5f, i < 2 ? -3f : 3f), new Vector3(0.5f, H, 0.5f), new Color(0.75f, 0.73f, 0.7f));
            for (int k = 1; k < 4; k++)
                for (int s = 0; s < 4; s++)
                {
                    Quaternion q = Quaternion.Euler(0, s * 90f, 0);
                    K.Block(K.Env, "TowerBrace", b + Vector3.up * (k * 5.5f) + q * new Vector3(0, 0, 3f), new Vector3(6.4f, 0.2f, 0.2f), new Color(0.75f, 0.73f, 0.7f), GameLayers.DEFAULT, false, q);
                }
            K.Block(K.Env, "TowerDeck", b + Vector3.up * (H + 0.15f), new Vector3(9f, 0.3f, 9f), new Color(0.6f, 0.6f, 0.62f));
            K.Mesh(K.Env, "TowerTank", CylMesh, b + Vector3.up * (H + 3.8f), new Vector3(7.5f, 7f, 7.5f), new Color(0.92f, 0.92f, 0.9f), null, true).isStatic = true;
            K.Mesh(K.Env, "TowerCap", ConeMesh, b + Vector3.up * (H + 7.3f), new Vector3(8f, 2f, 8f), new Color(0.2f, 0.45f, 0.55f));
            K.Mesh(K.Env, "TowerBand", CylMesh, b + Vector3.up * (H + 4.5f), new Vector3(7.6f, 1f, 7.6f), new Color(0.2f, 0.45f, 0.55f));
            Ladder(b + new Vector3(0, 0, -4.6f), H + 0.5f, 180f, "Classic");
            K.Block(K.Env, "DeckRail", b + new Vector3(0, H + 0.85f, 4.45f), new Vector3(9f, 1.1f, 0.1f), Color.white);
            K.Weapon(ItemKind.Sniper, b + new Vector3(3.5f, H + 0.35f, -3.5f));
        }

        /// <summary>A solar farm in the eastern dunes: rows of tilted panels on frames, an inverter shed.</summary>
        private void SolarFarm()
        {
            Color lPanel = new Color(0.15f, 0.22f, 0.38f), lFrame = new Color(0.7f, 0.7f, 0.72f);
            for (int r = 0; r < 7; r++)
                for (int k = 0; k < 6; k++)
                {
                    Vector3 p = G(372f + k * 17f, 68f + r * 18f);
                    K.Block(K.Env, "SolarPanel", p + Vector3.up * 1.6f, new Vector3(15f, 0.12f, 3.4f), lPanel, GameLayers.DEFAULT, true, Quaternion.Euler(-25f, 0, 0), true);
                    K.Block(K.Env, "SolarFrame", p + Vector3.up * 0.75f, new Vector3(14f, 1.5f, 0.2f), lFrame, GameLayers.DEFAULT, false);
                }
            Vector3 s = G(470, 48);
            K.Block(K.Env, "Inverter", s + Vector3.up * 1.3f, new Vector3(6f, 2.6f, 3f), new Color(0.85f, 0.85f, 0.82f), GameLayers.DEFAULT, true, null, true);
            K.DistractionKit(s + new Vector3(-3.8f, 0, 0), 270, "generator");
            K.Fence(G(360, 56, -0.2f), G(476, 56, -0.2f), 2f, new Color(0.6f, 0.62f, 0.62f));
            K.Gadget(ItemKind.C4, s + new Vector3(0, 0.1f, -2.2f));
        }

        /// <summary>The coast bus terminal between the corniche and the highway: a long shade roof on columns, buses, benches, palm groves around it.</summary>
        private void BusTerminal()
        {
            Vector3 c = G(80, -268);
            Color lRoof = new Color(0.95f, 0.93f, 0.88f);
            for (int i = 0; i < 6; i++)
                for (int s = -1; s <= 1; s += 2) K.Piece("Classic", "Column", c + new Vector3(-20f + i * 8f, 0, s * 5f), 0, null, K.Env, MapKit.Collide.Box);
            K.Block(K.Env, "TerminalRoof", c + Vector3.up * 3.55f, new Vector3(46f, 0.35f, 12f), lRoof);
            K.Block(K.Env, "TerminalFascia", c + new Vector3(0, 3.95f, -6.05f), new Vector3(46f, 0.6f, 0.12f), new Color(0.15f, 0.45f, 0.5f), GameLayers.DEFAULT, false);
            for (int i = 0; i < 4; i++)
            {
                GameObject lBus = K.Prop("Van", c + new Vector3(-15f + i * 10f, 0, -10f), 90f, new[] { new Color(0.15f, 0.45f, 0.5f), new Color(0.9f, 0.9f, 0.88f) }[i % 2], MapKit.Collide.Box, true);
                if (lBus != null) lBus.transform.localScale = new Vector3(1.25f, 1.45f, 2.3f);
            }
            for (int i = 0; i < 5; i++) K.Prop("Bench", c + new Vector3(-16f + i * 8f, 0, 2.5f), 180, null);
            K.Prop("TrashBin", c + new Vector3(18f, 0, 2.5f), 0);
            K.Prop("StreetSign", c + new Vector3(-24f, 0, -6f), 90);
            PaintRect(R(52, -288, 108, -256), ASPHALT, 0);
            for (int i = 0; i < 22; i++)
            {
                float a = i * 2.39996f, r = 18f + (i % 5) * 9f;
                float x = 80f + Mathf.Cos(a) * r * 1.6f, z = -268f + Mathf.Sin(a) * r;
                if (Flag(x, z, ROAD | WET | BANK) || x < 0f || x > 160f || z > -232f || z < -306f) continue;
                Palm(x, z, Rand(0.9f, 1.15f));
            }
            K.Grass(G(130, -295), new Vector2(18, 12), new Color(0.82f, 0.72f, 0.45f));
            K.Weapon(ItemKind.Pistol, c + new Vector3(10f, 0.6f, 2.5f));
        }

        /// <summary>A radio relay on a dune ridge: a lattice mast with red lights, a hut, a fence, a lone lookout.</summary>
        private void RelayStation()
        {
            Vector3 c = G(300, 180);
            Color lSteel = new Color(0.8f, 0.8f, 0.82f), lRed = new Color(0.85f, 0.2f, 0.18f);
            const float H = 46f;
            for (int i = 0; i < 4; i++) K.Block(K.Env, "MastLeg", c + new Vector3(i % 2 == 0 ? -1.2f : 1.2f, H * 0.5f, i < 2 ? -1.2f : 1.2f), new Vector3(0.22f, H, 0.22f), (i % 2 == 0) ? lSteel : lRed);
            for (float y = 3f; y < H; y += 6f)
                for (int s = 0; s < 4; s++)
                {
                    Quaternion q = Quaternion.Euler(0, s * 90f, 0);
                    K.Block(K.Env, "MastBrace", c + Vector3.up * y + q * new Vector3(0, 0, 1.2f), new Vector3(3.2f, 0.1f, 0.1f), lSteel, GameLayers.DEFAULT, false, q * Quaternion.Euler(0, 0, 40f));
                }
            GameObject lMast = K.Block(K.Env, "MastCollider", c + Vector3.up * H * 0.5f, new Vector3(2.6f, H, 2.6f), lSteel);
            lMast.GetComponent<MeshRenderer>().enabled = false;
            for (int k = 1; k <= 3; k++) K.Lamp(c + Vector3.up * (H * k / 3f + 0.3f), new Color(1f, 0.15f, 0.1f), 6f, 1.5f, false);
            K.Block(K.Env, "Dish", c + new Vector3(1.6f, H * 0.7f, 0), new Vector3(0.3f, 2.4f, 2.4f), lSteel, GameLayers.DEFAULT, false);
            Vector3 hut = c + new Vector3(8f, 0, -6f);
            K.Block(K.Env, "RelayHut", hut + Vector3.up * 1.4f, new Vector3(5f, 2.8f, 4f), new Color(0.9f, 0.88f, 0.82f), GameLayers.DEFAULT, true, null, true);
            K.Block(K.Env, "RelayHutRoof", hut + Vector3.up * 2.9f, new Vector3(5.6f, 0.2f, 4.6f), new Color(0.5f, 0.5f, 0.52f));
            K.DistractionKit(hut + new Vector3(0, 0, -2.4f), 180, "generator");
            K.Fence(c + new Vector3(-12f, -0.2f, -14f), c + new Vector3(16f, -0.2f, -14f), 2.2f, new Color(0.6f, 0.62f, 0.62f));
            K.Fence(c + new Vector3(-12f, -0.2f, -14f), c + new Vector3(-12f, -0.2f, 10f), 2.2f, new Color(0.6f, 0.62f, 0.62f));
            K.Npc(NpcRole.Lookout, hut + new Vector3(-3.5f, 0.1f, -3f), 225f, 2, ItemKind.Sniper);
            K.Weapon(ItemKind.Smg, hut + new Vector3(3f, 0.1f, -2.6f));
        }

        /// <summary>The ruin of an old fort on the northern ridge: broken walls, a round tower stump, rubble.</summary>
        private void FortRuin()
        {
            Vector3 c = G(-150, 400);
            Color lStone = new Color(0.72f, 0.6f, 0.46f);
            (Vector3 at, Vector3 size, float yaw)[] lWalls =
            {
                (new Vector3(0, 0, -14), new Vector3(26f, 4.5f, 1.6f), 0f), (new Vector3(-13, 0, 0), new Vector3(1.6f, 3.2f, 20f), 0f),
                (new Vector3(13, 0, 3), new Vector3(1.6f, 5.5f, 14f), 0f), (new Vector3(-4, 0, 14), new Vector3(14f, 2.4f, 1.6f), 0f), (new Vector3(8, 0, 12), new Vector3(6f, 1.4f, 1.6f), 15f)
            };
            foreach ((Vector3 lAt, Vector3 lSize, float lYaw) in lWalls)
                K.Block(K.Env, "FortWall", c + lAt + Vector3.up * (lSize.y * 0.5f - 0.6f), lSize, GameArt.Vary(lStone, 0.06f, K.Rng), GameLayers.DEFAULT, true, Quaternion.Euler(0, lYaw, 0), true);
            K.Mesh(K.Env, "FortTower", CylMesh, c + new Vector3(13f, 3.5f, -14f), new Vector3(7f, 9f, 7f), lStone * 0.95f, null, true).isStatic = true;
            K.Stairs(K.Env, c + new Vector3(13f, -0.6f, -5f), c + new Vector3(13f, 8f, -10.6f), 1.8f, lStone * 0.9f);
            for (int i = 0; i < 10; i++) Nat(i % 2 == 0 ? "RockC" : "RockB", c.x + Rand(-16f, 16f), c.z + Rand(-16f, 16f), Rand(0.6f, 1.1f), -1f, lStone, MapKit.Collide.Box, 0.3f);
            K.Weapon(ItemKind.Sniper, c + new Vector3(13f, 8.2f, -14f));
            K.Gadget(ItemKind.SmokeGrenade, c + new Vector3(-8f, 0.1f, 6f));
        }

        /// <summary>Patches of dry tall grass to crawl through: along the wadi banks, on the plain, at the desert's edge.</summary>
        private void DryGrass()
        {
            Color lDry = new Color(0.82f, 0.72f, 0.45f);
            Vector3[] lPatches =
            {
                new Vector3(-130, 60, 18), new Vector3(-80, 250, 20), new Vector3(-20, 330, 24), new Vector3(110, 80, 22), new Vector3(240, 150, 26), new Vector3(-60, -150, 16),
                new Vector3(60, -200, 18), new Vector3(-200, 380, 22), new Vector3(330, 380, 24), new Vector3(420, -10, 20), new Vector3(-200, -90, 14), new Vector3(120, 300, 20)
            };
            foreach (Vector3 p in lPatches)
            {
                if (Flag(p.x, p.y, ROAD | BUILT)) continue;
                K.Grass(G(p.x, p.y), new Vector2(p.z, p.z * 0.7f), lDry);
            }
        }

        /// <summary>The Date Palm Park on the east bank of the wadi: a plantation in rows, paths, a domed pavilion, benches.</summary>
        private void PalmPark()
        {
            for (int i = 0; i < 8; i++)
                for (int k = 0; k < 8; k++)
                {
                    float x = -48f + i * 15f, z = -68f + k * 15f;
                    if (Mathf.Abs(x - 4.5f) < 6f || Mathf.Abs(z + 8f) < 6f) continue;
                    Palm(x + Rand(-1.5f, 1.5f), z + Rand(-1.5f, 1.5f), Rand(0.95f, 1.2f));
                }
            PaintRect(R(-0.5f, -80, 9.5f, 60), PAVE, 0);
            PaintRect(R(-60, -13, 70, -3), PAVE, 0);
            Vector3 c = G(4.5f, -8f);
            K.Mesh(K.Env, "PavilionBase", CylMesh, c + Vector3.up * 0.25f, new Vector3(9f, 0.5f, 9f), STONE, null, true);
            for (int i = 0; i < 8; i++) K.Piece("Stucco", "Column", c + Vector3.up * 0.5f + Quaternion.Euler(0, i * 45f, 0) * Vector3.forward * 3.8f, i * 45f, null, K.Env, MapKit.Collide.Box);
            K.Mesh(K.Env, "PavilionRing", CylMesh, c + Vector3.up * 4.1f, new Vector3(8.8f, 0.4f, 8.8f), new Color(0.95f, 0.9f, 0.8f));
            K.Mesh(K.Env, "PavilionDome", DomeMesh, c + Vector3.up * 4.2f, new Vector3(8.4f, 6f, 8.4f), new Color(0.3f, 0.55f, 0.6f));
            for (int i = 0; i < 4; i++) P("Bench", 4.5f + (i % 2 == 0 ? -9f : 9f), -8f + (i < 2 ? -4f : 4f), i % 2 == 0 ? 90f : 270f);
            for (int i = 0; i < 4; i++) { P("LampPostOrnate", 4.5f + (i % 2 == 0 ? -6f : 6f), -26f + (i / 2) * 36f, 0); K.Lamp(G(4.5f + (i % 2 == 0 ? -6f : 6f), -26f + (i / 2) * 36f, 4.1f), WARM, 12f, 1.3f, false); }
            K.Weapon(ItemKind.Smg, c + Vector3.up * 0.65f);
        }

        /// <summary>An oasis camp in the dunes: a spring pool under palms, tents, a fire, a lookout.</summary>
        private void Oasis()
        {
            Vector3 c = G(OASIS);
            K.Water(c + Vector3.up * 0.15f, new Vector2(14f, 10f));
            K.Block(K.Env, "SpringBed", c + Vector3.up * -0.2f, new Vector3(14.4f, 0.5f, 10.4f), new Color(0.35f, 0.45f, 0.35f), GameLayers.DEFAULT, false);
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                Palm(OASIS.x + Mathf.Cos(a) * Rand(10f, 16f), OASIS.y + Mathf.Sin(a) * Rand(9f, 14f), Rand(0.9f, 1.25f));
            }
            for (int i = 0; i < 3; i++) P("TentMilitary", OASIS.x - 14f + i * 9f, OASIS.y - 18f, 0, new Color(0.88f, 0.8f, 0.62f), true, MapKit.Collide.Mesh);
            K.Lamp(G(OASIS.x + 4f, OASIS.y - 12f, 0.3f), new Color(1f, 0.55f, 0.25f), 9f, 2f, false);
            for (int i = 0; i < 3; i++) K.NaturePiece("Log", G(OASIS.x + 4f, OASIS.y - 12f) + Quaternion.Euler(0, i * 120f, 0) * Vector3.forward * 2.5f, i * 120f + 90f, 0.7f, PLANK, MapKit.Collide.Box);
            K.Npc(NpcRole.Lookout, G(OASIS.x + 20f, OASIS.y + 8f, 0.1f), 45f, 2, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, G(OASIS.x - 5f, OASIS.y - 15f, 0.15f));
            K.Weapon(ItemKind.Smg, G(OASIS.x + 10f, OASIS.y - 16f, 0.15f));
            K.Gadget(ItemKind.EmeticPoison, G(OASIS.x + 6f, OASIS.y - 14f, 0.1f));
        }

        /// <summary>The desert road petrol station: canopy, pumps, the shop, the getaway pickup (car extraction).</summary>
        private void PetrolStation()
        {
            Vector3 c = G(PETROL);
            for (int i = 0; i < 4; i++) K.Block(K.Env, "CanopyPost", c + new Vector3(i % 2 == 0 ? -6f : 6f, 2.6f, i < 2 ? -4f : 4f), new Vector3(0.5f, 5.2f, 0.5f), Color.white);
            K.Block(K.Env, "Canopy", c + Vector3.up * 5.4f, new Vector3(16f, 0.8f, 11f), new Color(0.85f, 0.25f, 0.2f));
            K.Lamp(c + Vector3.up * 4.8f, new Color(1f, 0.95f, 0.85f), 16f, 2f, false);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(K.Env, "Pump", c + new Vector3(s * 2f, 0.8f, 0), new Vector3(0.8f, 1.6f, 0.6f), new Color(0.92f, 0.92f, 0.9f), GameLayers.DEFAULT, true, null, true);
                K.Barrel(c + new Vector3(s * 2f, 0, 1.5f), true);
            }
            K.Building(Spec("Desert Stop"));
            K.Car(c + new Vector3(-4f, 0, 0), 0, new Color(0.8f, 0.7f, 0.3f));
            K.Car(G(PETROL.x - 8f, PETROL.y - 10f), 0, new Color(0.25f, 0.3f, 0.35f));
            K.Point(MatchPoint.Kind.Extraction, G(PETROL.x - 8f, PETROL.y - 15f, 0.1f), 180, "Desert road pickup");
            K.UniqueWeapon(ItemKind.Flamethrower, Spec("Desert Stop").origin + new Vector3(3f, 0.15f, -2f), "Desert Stop storeroom");
            Guard(2, PETROL.x + 6f, PETROL.y - 6f, 180, ItemKind.Shotgun);
            K.Weapon(ItemKind.Shotgun, Spec("Desert Stop").origin + new Vector3(-3f, 1.0f, 2f));
            P("PowerPole", PETROL.x - 16f, PETROL.y + 12f, 0);
            K.Prop("StreetSign", c + new Vector3(-12f, 0, -12f), 90);
        }

        /// <summary>Life in the dry wadi: an abandoned car in the bed, culvert pipes, a goatherd's shelter, the patrol under the bridges.</summary>
        private void Wadi()
        {
            Vector2 p = WADI[6] + (WADI[7] - WADI[6]) * 0.5f;
            K.Car(G(p, -0.2f), 20f, new Color(0.45f, 0.35f, 0.25f));
            for (int i = 0; i < 3; i++) K.Mesh(K.Env, "Culvert", CylMesh, G(WADI[5].x + 6f, WADI[5].y - 10f + i * 2.4f, 0.5f), new Vector3(1.8f, 4f, 1.8f), CONCRETE, Quaternion.Euler(0, 0, 90), true);
            Vector2 lShelter = WADI[3] + new Vector2(14f, -6f);
            K.Block(K.Env, "ShelterRoof", G(lShelter, 2.4f), new Vector3(5f, 0.15f, 4f), PLANK);
            for (int i = 0; i < 4; i++) K.Block(K.Env, "ShelterPost", G(lShelter + new Vector2(i % 2 == 0 ? -2.2f : 2.2f, i < 2 ? -1.8f : 1.8f), 1.2f), new Vector3(0.15f, 2.4f, 0.15f), PLANK);
            Walk(1, ItemKind.Smg, 4f, new Vector2(-96, -110), new Vector2(-72, -200), new Vector2(-57, -262), new Vector2(-52, -320));
            K.Weapon(ItemKind.Pistol, G(p, 0.4f));
            K.Gadget(ItemKind.C4, G(WADI[4], 0.1f));
        }
    }
}
