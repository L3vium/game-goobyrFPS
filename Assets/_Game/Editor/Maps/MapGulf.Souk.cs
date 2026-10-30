using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The Old Souk (zone 0): a walled plateau four metres above the old harbour. Ten rows of
    /// flat-roofed stucco houses are cut by narrow alleys; two covered market streets (the Long
    /// Souk east-west, the Copper Souk north-south) meet at the Spice Crossing. Roofs of the same
    /// height are joined by planks, so a courier can cross the district without touching the
    /// ground. Set pieces: the Wind Clock tower and its plaza (sniper nest, zipline to the customs
    /// house), the Seven Lamps tea house and its storytelling evening, the domed hammam, the spice
    /// warehouse on the harbour wall, the customs house, two riad courtyards, the harbour quay with
    /// its breakwaters and fishing boats.
    /// </summary>
    public partial class MapGulf
    {
        private const float SX0 = -437f, SX1 = -200f;
        /// <summary>House rows (z from, z to). Rows alternate: front on the south edge, front on the north edge.</summary>
        private static readonly Vector2[] BANDS =
        {
            new Vector2(82, 100), new Vector2(104, 122), new Vector2(126, 144), new Vector2(148, 166),
            new Vector2(178, 196), new Vector2(200, 218), new Vector2(222, 240), new Vector2(244, 262), new Vector2(266, 284), new Vector2(288, 306)
        };
        /// <summary>The Long Souk (east-west) and the Copper Souk (north-south): covered market streets.</summary>
        private static readonly Rect LONG_SOUK = Rect.MinMaxRect(-437, 166, -186, 178), COPPER_SOUK = Rect.MinMaxRect(-323, 70, -311, 310);
        private static readonly Rect SPICE_CROSSING = Rect.MinMaxRect(-341, 148, -293, 196);
        private static readonly Rect CLOCK_PLAZA = Rect.MinMaxRect(-266, 222, -206, 286);
        private static readonly Rect TEA_COURT = Rect.MinMaxRect(-296, 102, -245, 146);
        private static readonly Rect SPICE_YARD = Rect.MinMaxRect(-437, 82, -398, 124);
        private static readonly Rect CUSTOMS = Rect.MinMaxRect(-437, 242, -404, 286);
        private static readonly Rect HAMMAM = Rect.MinMaxRect(-389, 198, -350, 220);
        private static readonly Rect RIAD_A = Rect.MinMaxRect(-404, 264, -377, 286);
        private static readonly Rect RIAD_B = Rect.MinMaxRect(-266, 176, -238, 198);
        private static readonly Rect[] SOUK_RESERVED = { SPICE_CROSSING, CLOCK_PLAZA, TEA_COURT, SPICE_YARD, CUSTOMS, HAMMAM, RIAD_A, RIAD_B };

        private static readonly Color[] SOUK_WALLS =
        {
            new Color(0.96f, 0.9f, 0.78f), new Color(0.93f, 0.8f, 0.62f), new Color(0.98f, 0.94f, 0.86f), new Color(0.88f, 0.72f, 0.58f),
            new Color(0.94f, 0.84f, 0.76f), new Color(0.85f, 0.78f, 0.66f), new Color(0.97f, 0.86f, 0.68f)
        };
        private static readonly Color SOUK_ROOF = new Color(0.82f, 0.72f, 0.6f);

        private class SoukLot
        {
            public MapKit.KitSpec spec;
            public MapKit.KitBuilding built;
            public int band;
            public Rect foot;
            public bool onLong, onCopper;
        }

        private readonly List<SoukLot> _soukLots = new List<SoukLot>();

        private static float SnapSouk(float pX) => SX0 + Mathf.Ceil((pX - SX0) / 3f - 0.001f) * 3f;

        private static bool Overlaps(Rect a, Rect b) => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;

        private static Rect? SoukObstacle(Rect pFoot)
        {
            Rect lFoot = Expand(pFoot, 1.4f);
            if (Overlaps(lFoot, COPPER_SOUK)) return COPPER_SOUK;
            foreach (Rect lRect in SOUK_RESERVED) if (Overlaps(lFoot, lRect)) return lRect;
            return null;
        }

        // ================================================================ plan

        private void PlanSouk()
        {
            _zoneStage.Add(() => Flatten(R(-440, 62, -186, 310), SOUK, 34f));
            _zoneStage.Add(() => Flatten(R(-456, 36, -440, 314), HARBOUR, 0f, BUILT));
            Road(9f, ASPHALT, new Vector2(-430, 70), new Vector2(-178, 70), new Vector2(-178, 318), new Vector2(-430, 318));
            Road(8f, COBBLE, new Vector2(-430, 318), new Vector2(-449, 300), new Vector2(-449, 88), new Vector2(-430, 70));
            Road(8f, ASPHALT, new Vector2(-300, 318), new Vector2(-298, 400), new Vector2(-300, 500));
            PaintRect(LONG_SOUK, COBBLE);
            PaintRect(COPPER_SOUK, COBBLE);
            PaintRect(SPICE_CROSSING, new Color(0.8f, 0.7f, 0.56f));
            PaintRect(CLOCK_PLAZA, PAVE);
            PaintRect(TEA_COURT, new Color(0.78f, 0.62f, 0.48f));
            PaintRect(SPICE_YARD, new Color(0.72f, 0.64f, 0.52f));
            PaintRect(R(-437, 74.5f, -186, 82), PAVE);
            PaintRect(R(-456, 36, -440, 314), COBBLE);
            _tame.Add(R(-460, 60, -180, 316));

            System.Random r = new System.Random(4127);
            int lIndex = 0;
            for (int b = 0; b < BANDS.Length; b++)
            {
                float z0 = BANDS[b].x, z1 = BANDS[b].y;
                bool lNorth = b % 2 == 1;
                float x = SX0;
                int lGuard = 0;
                while (x < SX1 - 8f && lGuard++ < 40)
                {
                    int w = 5 + r.Next(4), d = 4 + r.Next(3);
                    if (x + w * 3f > SX1) w = Mathf.FloorToInt((SX1 - x) / 3f);
                    if (w < 3) break;
                    Rect lFoot = FootAt(x, w, d, z0, z1, lNorth);
                    Rect? lHit = SoukObstacle(lFoot);
                    if (lHit.HasValue)
                    {
                        int lFit = Mathf.FloorToInt((lHit.Value.xMin - 1.5f - x) / 3f);
                        if (lHit.Value.xMin > x && lFit >= 3)
                        {
                            w = Mathf.Min(w, lFit);
                            lFoot = FootAt(x, w, d, z0, z1, lNorth);
                            lHit = SoukObstacle(lFoot);
                        }
                        if (lHit.HasValue) { x = SnapSouk(lHit.Value.xMax + 1.6f); continue; }
                    }
                    PlanSoukLot(lFoot, w, d, b, lNorth, lIndex++, r);
                    double g = r.NextDouble();
                    x = lFoot.xMax + (g < 0.35 ? 3f : g < 0.8 ? 6f : 9f);
                }
            }
            PlanSoukSpecials();
            Debug.Log($"[Build] Map_Gulf souk: {_soukLots.Count} houses");
        }

        private static Rect FootAt(float pX, int pW, int pD, float pZ0, float pZ1, bool pNorth)
            => Rect.MinMaxRect(pX, pNorth ? pZ1 - pD * 3f : pZ0, pX + pW * 3f, pNorth ? pZ1 : pZ0 + pD * 3f);

        private void PlanSoukLot(Rect pFoot, int w, int d, int pBand, bool pNorth, int pIndex, System.Random r)
        {
            bool lOnLong = (pNorth && Mathf.Abs(pFoot.yMax - LONG_SOUK.yMin) < 0.1f) || (!pNorth && Mathf.Abs(pFoot.yMin - LONG_SOUK.yMax) < 0.1f);
            bool lWestOfCopper = Mathf.Abs(pFoot.xMax - COPPER_SOUK.xMin) < 0.1f, lEastOfCopper = Mathf.Abs(pFoot.xMin - COPPER_SOUK.xMax) < 0.1f;
            bool lMain = lOnLong || lWestOfCopper || lEastOfCopper;
            double f = r.NextDouble();
            // Mostly two floors, so the roofs line up and the planks make a web; taller houses on the souks.
            int lFloors = lMain ? (f < 0.35 ? 2 : 3) : (f < 0.12 ? 1 : f < 0.86 ? 2 : 3);
            float lYaw = pNorth ? 180f : 0f;
            string lName = $"Souk House {pIndex}";
            MapKit.KitSpec s = Lot(lName, "Stucco", pFoot.center.x, pFoot.center.y, lYaw, w, d, lFloors, Roof.Flat, SOUK_WALLS[r.Next(SOUK_WALLS.Length)], SOUK_ROOF, 0, SOUK, 2f);
            s.windowChance = 0.3f;
            s.balconyChance = lFloors > 1 ? 0.1f : 0f;
            s.tallWindows = r.NextDouble() < 0.5;
            s.interiorWall = new Color(0.95f, 0.89f, 0.78f);
            s.floorColor = new Color(0.66f, 0.46f, 0.34f);
            Doors(s, 0, w / 2, 2, 1);
            if (lOnLong && w >= 5) { s.arches.Add(new Vector2Int(0, 1)); s.arches.Add(new Vector2Int(0, w - 2)); }
            // A side door onto the Copper Souk (front half of the side wall, clear of the stairs).
            if (lWestOfCopper) s.doors.Add(new Vector2Int(pNorth ? 3 : 1, 0));
            if (lEastOfCopper) s.doors.Add(new Vector2Int(pNorth ? 1 : 3, 0));
            if (r.NextDouble() < 0.4) s.climb = new Vector2Int(r.NextDouble() < 0.5 ? 0 : 2, 0);
            Room lFront = lMain ? Room.Shop : (r.NextDouble() < 0.5 ? Room.Lounge : Room.Storage);
            Room lBack = lMain ? Room.Storage : Room.Kitchen;
            // Two rooms on the ground floor, one living room upstairs (the top floor of a tall house is a storeroom left bare).
            Floors(s, lFront, lBack);
            if (lFloors > 1) { double k = r.NextDouble(); s.rooms.Add(new[] { k < 0.25 ? Room.Office : k < 0.6 ? Room.Bedroom : k < 0.8 ? Room.Lounge : Room.Bathroom }); }
            if (lFloors > 2) s.rooms.Add(new Room[0]);
            _soukLots.Add(new SoukLot { spec = s, band = pBand, foot = pFoot, onLong = lOnLong, onCopper = lWestOfCopper || lEastOfCopper });
        }

        private static readonly Vector3 CLOCK = new Vector3(-236f, SOUK + 0.05f, 254f);

        private void PlanSoukSpecials()
        {
            // The Wind Clock: a slender ten-floor tower (stairs to the roof), the souk's landmark.
            MapKit.KitSpec lClock = Lot("Wind Clock Tower", "Stucco", CLOCK.x, CLOCK.z, 0, 3, 3, 10, Roof.Flat, new Color(0.95f, 0.86f, 0.7f), SOUK_ROOF, 0, SOUK, 2f);
            Doors(lClock, 0, 1);
            lClock.windowChance = 0.25f;
            lClock.balconyChance = 0f;
            lClock.tallWindows = true;
            Floors(lClock, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.None, Room.Security, Room.None);
            for (int i = 0; i < 9; i++) lClock.rooms[i] = new Room[0];

            // The Seven Lamps tea house: front on the alley, its garden court behind.
            MapKit.KitSpec lTea = Lot("Seven Lamps Teahouse", "Stucco", -270.5f, 111.5f, 0, 8, 5, 1, Roof.Flat, new Color(0.93f, 0.78f, 0.6f), SOUK_ROOF, 0, SOUK, 2f);
            Doors(lTea, 0, 4, 2, 1, 2, 6);
            lTea.arches.Add(new Vector2Int(0, 1));
            lTea.arches.Add(new Vector2Int(0, 6));
            lTea.tallWindows = true;
            lTea.climb = new Vector2Int(3, 2);
            Floors(lTea, Room.Dining, Room.Kitchen);

            // The hammam: one long vaulted floor under three domes.
            MapKit.KitSpec lHammam = Lot("Old Hammam", "Stucco", -368.5f, 209f, 0, 11, 5, 1, Roof.Flat, new Color(0.98f, 0.94f, 0.86f), SOUK_ROOF, 0, SOUK, 2f);
            Doors(lHammam, 0, 5, 2, 1);
            lHammam.windowChance = 0.15f;
            // Changing rooms and toilets at the front, the warm pool in the empty vaulted back hall.
            lHammam.rooms.Add(new[] { Room.Bathroom, Room.None });

            // The spice warehouse on the harbour wall: its front on the wall promenade, the loading yard behind.
            MapKit.KitSpec lSpice = Lot("Amberline Spice Warehouse", "Stucco", -428f, 103f, 90, 12, 6, 2, Roof.Flat, new Color(0.9f, 0.76f, 0.58f), SOUK_ROOF, 0, SOUK, 2f);
            Doors(lSpice, 0, 6, 2, 3, 2, 9, 1, 1);
            lSpice.upperDoors.Add(new Vector3Int(2, 6, 1));
            lSpice.windowChance = 0.35f;
            lSpice.climb = new Vector2Int(3, 1);
            Floors(lSpice, Room.Storage, Room.Storage, Room.Office, Room.Storage);

            // The customs house on the harbour (front on the wall promenade).
            MapKit.KitSpec lCustoms = Lot("Customs House", "Classic", -421f, 264f, 90, 7, 5, 3, Roof.Flat, new Color(0.96f, 0.92f, 0.82f), SOUK_ROOF, 0, SOUK, 2f);
            Doors(lCustoms, 0, 3, 2, 1, 1, 0);
            lCustoms.tallWindows = true;
            lCustoms.balconyChance = 0.25f;
            lCustoms.climb = new Vector2Int(3, 4);
            Floors(lCustoms, Room.Security, Room.Office, Room.Office, Room.Lounge, Room.Office, Room.Bathroom);
        }

        // ================================================================ build

        private void Souk()
        {
            const int Z = 0;
            int lIndex = 0;
            foreach (SoukLot lLot in _soukLots)
            {
                MapKit.KitBuilding b = K.Building(lLot.spec);
                lLot.built = b;
                StripBands(b);
                if (lIndex % 3 != 0) ClearRoof(b, Expand(lLot.foot, 1f));
                if (lLot.onLong) Awnings(b, 0, FABRIC[lIndex % FABRIC.Length], lLot.spec.width >= 6 ? new[] { 2, lLot.spec.width - 3 } : new[] { 2 });
                RoofDressing(b, lIndex);
                lIndex++;
            }
            RoofPlanks();
            HarbourWall();
            LongSouk();
            CopperSouk();
            Crossing();
            ClockTower();
            Teahouse();
            Hammam();
            SpiceWarehouse();
            CustomsHouse();
            Riads();
            Alleys();

            // Gates and posts.
            Guard(Z, -190, 168, 90, ItemKind.Smg);
            Guard(Z, -190, 176, 90, ItemKind.Shotgun);
            Guard(Z, -321, 78, 180, ItemKind.Smg);
            Guard(Z, -313, 78, 180, ItemKind.Pistol);
            Guard(Z, -300, 312, 0, ItemKind.Shotgun);
            Walk(Z, ItemKind.Pistol, 3f, new Vector2(-396, 102), new Vector2(-294, 102), new Vector2(-294, 124), new Vector2(-396, 124));
            Walk(Z, ItemKind.Smg, 4f, new Vector2(-300, 242), new Vector2(-194, 242), new Vector2(-194, 264), new Vector2(-300, 264));

            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-318, 20, 188), new Vector3(300, 110, 290), G(-176, 172, 0.1f), new Color(0.62f, 0.38f, 0.28f), HatStyle.Turban,
                Spec("Customs House").origin + Vector3.up * 0.1f + Quaternion.Euler(0, 90, 0) * new Vector3(-6, 0, -4));
        }

        /// <summary>Removes the kit's floor bands (a decorative trim per facade module): the stucco walls carry their own frieze.</summary>
        private static void StripBands(MapKit.KitBuilding pB)
        {
            List<GameObject> lDoomed = new List<GameObject>();
            foreach (Transform lChild in pB.root) if (lChild.name == "FloorBand") lDoomed.Add(lChild.gameObject);
            foreach (GameObject lObject in lDoomed) Object.DestroyImmediate(lObject);
        }

        /// <summary>Water tanks, dishes, washing lines, drying rugs and the odd roof terrace.</summary>
        private void RoofDressing(MapKit.KitBuilding b, int pSeed)
        {
            float y = RoofY(b.spec);
            Vector3 Roof(float u, float v) => b.root.TransformPoint(new Vector3(u * b.W * 0.5f, y - b.root.position.y, v * b.D * 0.5f));
            switch (pSeed % 6)
            {
                case 0:
                    K.Mesh(K.Props, "WaterTank", CylMesh, Roof(-0.55f, 0.55f) + Vector3.up * 1.4f, new Vector3(1.4f, 1.6f, 1.4f), new Color(0.85f, 0.85f, 0.82f), null, true).isStatic = true;
                    K.Block(K.Props, "TankStand", Roof(-0.55f, 0.55f) + Vector3.up * 0.3f, new Vector3(1.5f, 0.6f, 1.5f), new Color(0.4f, 0.38f, 0.36f));
                    break;
                case 1:
                    K.Mesh(K.Props, "Dish", DomeMesh, Roof(0.5f, -0.5f) + Vector3.up * 0.9f, new Vector3(1.1f, 0.25f, 1.1f), new Color(0.9f, 0.9f, 0.88f), Quaternion.Euler(-60f, b.Yaw, 0)).isStatic = true;
                    break;
                case 2:
                    for (int i = 0; i < 4; i++) K.Block(K.Props, "Laundry", Roof(-0.5f + i * 0.22f, 0.4f) + Vector3.up * 1.4f, new Vector3(0.7f, 0.8f, 0.03f), FABRIC[(pSeed + i) % FABRIC.Length], GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
                    K.Block(K.Props, "Line", Roof(-0.2f, 0.4f) + Vector3.up * 1.82f, new Vector3(b.W * 0.55f, 0.02f, 0.02f), new Color(0.3f, 0.3f, 0.3f), GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
                    break;
                case 3:
                    Shade(Roof(-0.4f, 0.35f), b.Yaw, FABRIC[pSeed % FABRIC.Length], true);
                    K.Prop("PlantPot", Roof(-0.75f, 0.75f), 0);
                    break;
                case 4:
                    K.Prop("Rug", Roof(-0.3f, 0.5f) + Vector3.up * 0.02f, b.Yaw, FABRIC[(pSeed + 3) % FABRIC.Length], MapKit.Collide.None);
                    Pots(Roof(-0.75f, -0.6f), 3, b.Yaw);
                    break;
            }
        }

        /// <summary>Planks between roofs of the same height, across the alleys; the parapets open where they land.</summary>
        private void RoofPlanks()
        {
            int lCount = 0, lClimbs = 0;
            Dictionary<SoukLot, List<SoukLot>> lLinks = new Dictionary<SoukLot, List<SoukLot>>();
            void Link(SoukLot a, SoukLot b)
            {
                if (!lLinks.ContainsKey(a)) lLinks[a] = new List<SoukLot>();
                if (!lLinks.ContainsKey(b)) lLinks[b] = new List<SoukLot>();
                lLinks[a].Add(b);
                lLinks[b].Add(a);
            }
            for (int i = 0; i < _soukLots.Count; i++)
                for (int j = 0; j < _soukLots.Count; j++)
                {
                    SoukLot a = _soukLots[i], b = _soukLots[j];
                    if (i == j) continue;
                    bool lSame = a.spec.floors == b.spec.floors;
                    float y = RoofY(a.spec) + 0.08f;
                    // Across a north-south alley (same row, b east of a).
                    float z0 = Mathf.Max(a.foot.yMin, b.foot.yMin), z1 = Mathf.Min(a.foot.yMax, b.foot.yMax);
                    if (a.band == b.band && b.foot.xMin > a.foot.xMax && b.foot.xMin - a.foot.xMax <= 9.1f && z1 - z0 >= 3f && lSame)
                    {
                        float z = z0 + 1.5f + (Mathf.Floor((z1 - z0) / 3f) > 2 ? 3f : 0f);
                        Plank(a.built, b.built, new Vector3(a.foot.xMax, y, z), new Vector3(b.foot.xMin, y, z));
                        Link(a, b);
                        lCount++;
                    }
                    // A lower roof next to a taller one: a plank to its wall and a ladder up to its roof (players only).
                    if (a.band == b.band && b.foot.xMin > a.foot.xMax && b.foot.xMin - a.foot.xMax <= 6.1f && z1 - z0 >= 3f && Mathf.Abs(a.spec.floors - b.spec.floors) == 1 && i % 3 != 2)
                    {
                        SoukLot lLow = a.spec.floors < b.spec.floors ? a : b, lHigh = lLow == a ? b : a;
                        float z = z0 + 1.5f;
                        float yl = RoofY(lLow.spec) + 0.08f;
                        float xl = lLow == a ? a.foot.xMax : b.foot.xMin, xh = lLow == a ? b.foot.xMin : a.foot.xMax;
                        Plank(lLow.built, lLow.built, new Vector3(xl, yl, z), new Vector3(xh - (lLow == a ? 0.5f : -0.5f), yl, z));
                        Ladder(new Vector3(xh - (lLow == a ? 0.35f : -0.35f), yl, z), MapKit.STOREY + 0.4f, lLow == a ? 270f : 90f);
                        OpenParapet(lHigh.built, new Vector3(xh, RoofY(lHigh.spec), z));
                        lClimbs++;
                    }
                    // Across an east-west alley (b in the next row, fronts or backs facing).
                    float x0 = Mathf.Max(a.foot.xMin, b.foot.xMin), x1 = Mathf.Min(a.foot.xMax, b.foot.xMax);
                    if (b.band == a.band + 1 && b.foot.yMin > a.foot.yMax && b.foot.yMin - a.foot.yMax <= 10.1f && x1 - x0 >= 3f && lSame)
                    {
                        float x = x0 + 1.5f;
                        Plank(a.built, b.built, new Vector3(x, y, a.foot.yMax), new Vector3(x, y, b.foot.yMin));
                        Link(a, b);
                        lCount++;
                    }
                }
            // The courier's run: a patrol over the largest web of linked roofs.
            List<SoukLot> lBest = new List<SoukLot>();
            HashSet<SoukLot> lSeen = new HashSet<SoukLot>();
            foreach (SoukLot lStart in lLinks.Keys)
            {
                if (lSeen.Contains(lStart)) continue;
                List<SoukLot> lGroup = new List<SoukLot>();
                Queue<SoukLot> lQueue = new Queue<SoukLot>();
                lQueue.Enqueue(lStart);
                lSeen.Add(lStart);
                while (lQueue.Count > 0)
                {
                    SoukLot c = lQueue.Dequeue();
                    lGroup.Add(c);
                    foreach (SoukLot n in lLinks[c]) if (lSeen.Add(n)) lQueue.Enqueue(n);
                }
                if (lGroup.Count > lBest.Count) lBest = lGroup;
            }
            if (lBest.Count >= 3)
            {
                List<Vector3> lPoints = new List<Vector3>();
                for (int i = 0; i < 4; i++)
                {
                    SoukLot l = lBest[Mathf.Min(lBest.Count - 1, i * lBest.Count / 4)];
                    lPoints.Add(l.built.Local(-l.built.W * 0.25f, RoofY(l.spec) - l.built.root.position.y + 0.1f, l.built.D * 0.2f));
                }
                K.Patrol(0, ItemKind.Smg, 5f, lPoints.ToArray());
                SoukLot lNest = lBest[lBest.Count / 2];
                K.Activity(ActivityType.Lookout, lNest.built.Local(-lNest.built.W * 0.3f, RoofY(lNest.spec) - lNest.built.root.position.y + 0.1f, -lNest.built.D * 0.3f), lNest.spec.yaw + 180f, 0);
                K.Activity(ActivityType.Smoke, lBest[1].built.Local(-lBest[1].built.W * 0.3f, RoofY(lBest[1].spec) - lBest[1].built.root.position.y + 0.1f, lBest[1].built.D * 0.3f), 0, 0);
                K.Weapon(ItemKind.Sniper, lBest[lBest.Count - 1].built.Local(-1f, RoofY(lBest[lBest.Count - 1].spec) - lBest[lBest.Count - 1].built.root.position.y + 0.15f, 1f));
            }
            Debug.Log($"[Build] Map_Gulf souk: {lCount} roof planks, courier web of {lBest.Count} roofs, {lClimbs} plank-and-ladder links");
        }

        /// <summary>A plank bridge between two roofs at the same level.</summary>
        private void Plank(MapKit.KitBuilding a, MapKit.KitBuilding b, Vector3 pFrom, Vector3 pTo)
        {
            Vector3 lDir = pTo - pFrom;
            Quaternion lRot = Quaternion.LookRotation(new Vector3(lDir.x, 0, lDir.z));
            float lLength = new Vector2(lDir.x, lDir.z).magnitude + 1.6f;
            K.Block(K.Env, "RoofPlank", (pFrom + pTo) * 0.5f - Vector3.up * 0.1f, new Vector3(1.8f, 0.2f, lLength), PLANK, GameLayers.DEFAULT, true, lRot);
            K.Block(K.Env, "PlankEdge", (pFrom + pTo) * 0.5f + lRot * new Vector3(0.8f, 0.03f, 0), new Vector3(0.16f, 0.1f, lLength), PLANK * 0.75f, GameLayers.DEFAULT, false, lRot);
            K.Block(K.Env, "PlankEdge", (pFrom + pTo) * 0.5f + lRot * new Vector3(-0.8f, 0.03f, 0), new Vector3(0.16f, 0.1f, lLength), PLANK * 0.75f, GameLayers.DEFAULT, false, lRot);
            if (lLength > 7.5f)
                for (int s = -1; s <= 1; s += 2)
                {
                    K.Block(K.Env, "BridgeRope", (pFrom + pTo) * 0.5f + lRot * new Vector3(s * 0.85f, 1f, 0), new Vector3(0.04f, 0.04f, lLength - 1.6f), new Color(0.75f, 0.65f, 0.45f), GameLayers.DEFAULT, false, lRot);
                    for (int k = -1; k <= 1; k += 2) K.Block(K.Env, "BridgePost", (pFrom + pTo) * 0.5f + lRot * new Vector3(s * 0.85f, 0.5f, k * (lLength * 0.5f - 0.8f)), new Vector3(0.08f, 1f, 0.08f), PLANK * 0.7f, GameLayers.DEFAULT, false, lRot);
                }
            OpenParapet(a, new Vector3(pFrom.x, pFrom.y - 0.08f, pFrom.z));
            OpenParapet(b, new Vector3(pTo.x, pTo.y - 0.08f, pTo.z));
        }

        /// <summary>The harbour: the retaining wall of the plateau, stairs, the quay, breakwaters, boats and nets.</summary>
        private void HarbourWall()
        {
            const int Z = 0;
            // The wall covers the slope between the quay and the plateau; its top is the wall promenade.
            K.Block(K.Env, "HarbourWall", new Vector3(-440.2f, (SOUK + HARBOUR) * 0.5f - 0.2f, 194f), new Vector3(8.4f, SOUK - HARBOUR + 0.4f, 212f), STONE * 0.9f);
            for (float z = 92f; z < 300f; z += 6f)
            {
                if (Mathf.Abs(z - 112f) < 4f || Mathf.Abs(z - 172f) < 4f || Mathf.Abs(z - 232f) < 4f || Mathf.Abs(z - 292f) < 4f) continue;
                K.Piece("Stucco", "Balustrade", new Vector3(-444.2f, SOUK + 0.05f, z), 270f, STONE, K.Env);
                K.Piece("Stucco", "Balustrade", new Vector3(-444.2f, SOUK + 0.05f, z + 3f), 270f, STONE, K.Env);
            }
            foreach (float z in new[] { 112f, 172f, 232f, 292f })
                K.Stairs(K.Env, new Vector3(-452.5f, HARBOUR, z), new Vector3(-444.3f, SOUK + 0.05f, z), 3.2f, STONE * 0.95f);
            // The quay edge and its bollards; the breakwaters close the old harbour.
            // The quay block covers the last ground cells, whose corners slope down to the sea floor.
            K.Block(K.Env, "QuayEdge", new Vector3(-454.1f, (SEA_FLOOR + HARBOUR) * 0.5f + 0.01f, 175f), new Vector3(5f, HARBOUR - SEA_FLOOR + 0.02f, 278f), STONE * 0.8f);
            for (float z = 44f; z < 310f; z += 9f) K.Prop("Bollard", new Vector3(-456f, HARBOUR + 0.02f, z), 90);
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? 39f : 311f;
                K.Block(K.Env, "Breakwater", new Vector3(-476f, (SEA_FLOOR + HARBOUR) * 0.5f, z), new Vector3(42f, HARBOUR - SEA_FLOOR, 6f), STONE * 0.85f);
                for (int i = 0; i < 6; i++) Nat("RockD", -462f + i * -6f + Rand(-1f, 1f), z + (s == 0 ? -4.5f : 4.5f), Rand(0.8f, 1.2f), -1f, GameArt.Vary(new Color(0.62f, 0.58f, 0.52f), 0.08f, K.Rng), MapKit.Collide.Box, 2.5f);
            }
            // The harbour light on the south breakwater.
            Vector3 lLight = new Vector3(-494f, HARBOUR, 39f);
            K.Mesh(K.Env, "HarbourLight", CylMesh, lLight + Vector3.up * 4f, new Vector3(2.2f, 8f, 2.2f), new Color(0.95f, 0.93f, 0.88f), null, true).isStatic = true;
            K.Mesh(K.Env, "LightRing", CylMesh, lLight + Vector3.up * 5.5f, new Vector3(2.35f, 0.7f, 2.35f), new Color(0.75f, 0.2f, 0.18f));
            K.Lamp(lLight + Vector3.up * 8.6f, new Color(1f, 0.9f, 0.7f), 14f, 2f, false);
            // Fishing boats, nets, crates, the fish market tables.
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = new Vector3(-462f - (i % 2) * 7f, SEA - 0.2f, 60f + i * 34f + Rand(-3f, 3f));
                K.Prop("Boat", p, 90f + Rand(-12f, 12f), FABRIC[i % FABRIC.Length] * 0.9f + Color.white * 0.1f, MapKit.Collide.Box);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = G(-450f, 66f + i * 40f);
                K.Mesh(K.Props, "Nets", SackMesh, c + Vector3.up * 0.25f, new Vector3(2.2f, 0.5f, 1.6f), new Color(0.35f, 0.42f, 0.4f)).isStatic = true;
                Clutter(G(-447.5f, 80f + i * 40f), 0, i);
                if (i % 2 == 0) { P("Table", -449f, 130f + i * 36f, 90, PLANK, true); Pots(G(-449.5f, 128f + i * 36f, 0.78f), 2); }
            }
            for (int i = 0; i < 5; i++) { P("LampPostOrnate", -446f, 60f + i * 56f, 90); K.Lamp(G(-446f, 60f + i * 56f, 4.1f), WARM, 12f, 1.4f, false); }
            // The quay guard post and patrol; a fisherman's stash.
            Guard(Z, -450, 170, 270, ItemKind.Shotgun);
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(-450, 50), new Vector2(-450, 300), new Vector2(-447, 300), new Vector2(-447, 50));
            K.Weapon(ItemKind.Pistol, G(-449, 66, 0.3f));
            K.Gadget(ItemKind.Coin, G(-451, 196, 0.1f));
            K.Activity(ActivityType.Smoke, G(-452, 250, 0.1f), 270, Z);
            K.Activity(ActivityType.Inspect, G(-449, 214, 0.1f), 270, Z);
        }

        /// <summary>A covered market street: beams and fabric strips overhead, lanterns, stalls along the walls.</summary>
        private void MarketCanopy(Vector3 pFrom, Vector3 pTo, float pWidth, Rect pSkip, int pSeed)
        {
            Vector3 lDir = pTo - pFrom;
            float lLength = lDir.magnitude;
            Quaternion lRot = Quaternion.LookRotation(lDir.normalized);
            int lIndex = 0;
            for (float t = 3f; t < lLength - 2f; t += 6f)
            {
                Vector3 p = pFrom + lDir.normalized * t;
                if (pSkip.Contains(new Vector2(p.x, p.z))) continue;
                Vector3 lTop = p + Vector3.up * 5.6f;
                K.Block(K.Env, "Beam", lTop, new Vector3(pWidth + 0.6f, 0.22f, 0.22f), PLANK * 0.8f, GameLayers.DEFAULT, false, lRot);
                K.Block(K.Env, "Fabric", lTop + lRot * new Vector3(0, 0.15f + (lIndex % 2) * 0.12f, 3f), new Vector3(pWidth + 0.2f, 0.04f, 6.1f), FABRIC[(lIndex + pSeed) % FABRIC.Length], GameLayers.DEFAULT, false, lRot * Quaternion.Euler(lIndex % 2 == 0 ? 2.5f : -2.5f, 0, 0));
                if (lIndex % 3 == 1) Lantern(lTop - Vector3.up * 1.1f);
                lIndex++;
            }
        }

        /// <summary>Market stalls along one side of a street, skipping the doors and arches of the houses behind.</summary>
        private void Stalls(Vector3 pFrom, Vector3 pTo, float pFaceYaw, Rect pSkip, int pSeed)
        {
            Vector3 lDir = pTo - pFrom;
            float lLength = lDir.magnitude;
            int lIndex = 0;
            for (float t = 2f; t < lLength - 2f; t += 5.4f)
            {
                Vector3 p = pFrom + lDir.normalized * t;
                if (pSkip.Contains(new Vector2(p.x, p.z)) || NearOpening(p, 2.6f)) continue;
                Vector3 g = G(p.x, p.z);
                K.Prop("MarketStall", g, pFaceYaw, FABRIC[(lIndex + pSeed) % FABRIC.Length], MapKit.Collide.Box, true);
                Vector3 lFront = Quaternion.Euler(0, pFaceYaw, 0) * Vector3.forward;
                switch ((lIndex + pSeed) % 5)
                {
                    case 0: Sacks(g + lFront * 1.4f - Quaternion.Euler(0, pFaceYaw, 0) * Vector3.right * 1f, pFaceYaw, 3); break;
                    case 1: Pots(g + lFront * 1.3f - Quaternion.Euler(0, pFaceYaw, 0) * Vector3.right * 0.9f, 3, pFaceYaw + 90f); break;
                    case 2: K.Prop("Rug", g + lFront * 1.8f + Vector3.up * 0.02f, pFaceYaw, FABRIC[(lIndex + 2) % FABRIC.Length], MapKit.Collide.None); break;
                    case 3: K.Barrel(g + lFront * 1.2f + Quaternion.Euler(0, pFaceYaw, 0) * Vector3.right * 1.1f, false); break;
                    default: K.Crate(g + lFront * 1.2f, 0.8f); break;
                }
                lIndex++;
            }
        }

        /// <summary>True when a door or an arch of a souk house opens within <paramref name="pRadius"/> of a point.</summary>
        private bool NearOpening(Vector3 p, float pRadius)
        {
            foreach (SoukLot l in _soukLots)
            {
                if (!Expand(l.foot, pRadius + 2f).Contains(new Vector2(p.x, p.z))) continue;
                foreach (Vector2Int d in l.spec.doors) if (Vector3.Distance(Flat(Facade(l.built, d.x, d.y, 0, 0)), Flat(p)) < pRadius + 1.2f) return true;
                foreach (Vector2Int d in l.spec.arches) if (Vector3.Distance(Flat(Facade(l.built, d.x, d.y, 0, 0)), Flat(p)) < pRadius + 1.2f) return true;
            }
            return false;
        }

        private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0, p.z);

        private void LongSouk()
        {
            const int Z = 0;
            float y = SOUK;
            MarketCanopy(new Vector3(-437, y, 172), new Vector3(-192, y, 172), 12f, SPICE_CROSSING, 0);
            Stalls(new Vector3(-436, y, 167.4f), new Vector3(-190, y, 167.4f), 0f, SPICE_CROSSING, 1);
            Stalls(new Vector3(-436, y, 176.6f), new Vector3(-190, y, 176.6f), 180f, SPICE_CROSSING, 3);
            K.Activity(ActivityType.Inspect, G(-380, 170.5f, 0.1f), 180, Z);
            K.Activity(ActivityType.Phone, G(-250, 173.5f, 0.1f), 0, Z);
            K.Distraction(G(-405, 168.5f, 0.2f), 0, "radio");
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(-430, 171), new Vector2(-200, 171), new Vector2(-200, 173), new Vector2(-430, 173));
            K.Weapon(ItemKind.Pistol, G(-356, 167.8f, 1.1f));
            K.Weapon(ItemKind.Smg, G(-226, 176.4f, 1.1f));
            K.Gadget(ItemKind.Poison, G(-392, 176.4f, 1.0f));
        }

        private void CopperSouk()
        {
            const int Z = 0;
            float y = SOUK;
            MarketCanopy(new Vector3(-317, y, 82), new Vector3(-317, y, 306), 12f, SPICE_CROSSING, 4);
            Stalls(new Vector3(-321.6f, y, 84), new Vector3(-321.6f, y, 304), 90f, Expand(SPICE_CROSSING, 2f), 2);
            Stalls(new Vector3(-312.4f, y, 84), new Vector3(-312.4f, y, 304), 270f, Expand(SPICE_CROSSING, 2f), 5);
            // Coppersmiths: pots and trays hung on the walls, a glowing forge.
            for (int i = 0; i < 6; i++) K.Mesh(K.Props, "CopperTray", CylMesh, G(-322.4f, 214 + i * 14f, 2.6f), new Vector3(0.9f, 0.05f, 0.9f), new Color(0.82f, 0.48f, 0.26f), Quaternion.Euler(0, 0, 90)).isStatic = true;
            K.Lamp(G(-313, 250, 0.6f), new Color(1f, 0.5f, 0.2f), 6f, 1.8f, false);
            K.Activity(ActivityType.Inspect, G(-317, 258, 0.1f), 90, Z);
            K.Activity(ActivityType.Drink, G(-315, 120, 0.1f), 270, Z);
            Walk(Z, ItemKind.Smg, 4f, new Vector2(-316, 86), new Vector2(-316, 300), new Vector2(-318, 300), new Vector2(-318, 86));
            K.Weapon(ItemKind.Shotgun, G(-321.4f, 236, 1.1f));
            K.Gadget(ItemKind.Brick, G(-312.6f, 100, 0.1f));
        }

        /// <summary>The Spice Crossing: a square where the two covered souks meet - fountain, palms, a café, the spice pyramids.</summary>
        private void Crossing()
        {
            const int Z = 0;
            Vector3 c = G(-317, 172);
            P("Fountain", c, 0, null, true);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -336f : -298f, z = i < 2 ? 153f : 191f;
                Palm(x, z, 1.05f);
                K.Prop("PlantPot", G(x + 1.2f, z), 0);
            }
            // The café on the south-east corner (tables, the terrace drinks), lanterns between the palms.
            for (int i = 0; i < 4; i++)
            {
                Vector3 t = G(-306f + (i % 2) * 5f, 155f + (i / 2) * 5f);
                K.Prop("TableRound", t, 0, PLANK, MapKit.Collide.Box, true);
                for (int k = 0; k < 2; k++) K.Prop("Chair", t + Quaternion.Euler(0, k * 180f, 0) * Vector3.forward * 0.9f, k * 180f + 180f, PLANK);
            }
            Shade(G(-303.5f, 157.5f), 0, FABRIC[2], false);
            Shade(G(-303.5f, 162.5f), 0, FABRIC[0], false);
            K.DrinksOn(G(-306, 155, 0.78f), 0, Z, 2);
            K.Activity(ActivityType.Dining, G(-301, 160.2f, 0.1f), 180, Z);
            // Spice pyramids: cones of colour in wooden trays.
            Color[] lSpice = { new Color(0.85f, 0.4f, 0.15f), new Color(0.9f, 0.75f, 0.2f), new Color(0.6f, 0.2f, 0.12f), new Color(0.45f, 0.5f, 0.2f), new Color(0.75f, 0.55f, 0.35f) };
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = G(-334f + i * 2.2f, 186f);
                K.Block(K.Props, "Tray", p + Vector3.up * 0.35f, new Vector3(1.6f, 0.7f, 1.6f), PLANK, GameLayers.DEFAULT, true, null, false);
                K.Mesh(K.Props, "Spice", ConeMesh, p + Vector3.up * 0.7f, new Vector3(1.3f, 0.9f, 1.3f), lSpice[i]).isStatic = true;
            }
            for (int i = 0; i < 4; i++) { P("LampPostOrnate", -332f + (i % 2) * 30f, 157f + (i / 2) * 30f, 0); K.Lamp(G(-332f + (i % 2) * 30f, 157f + (i / 2) * 30f, 4.1f), LANTERN, 10f, 1.4f, false); }
            Guard(Z, -330, 160, 45, ItemKind.Smg);
            Guard(Z, -304, 186, 225, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, G(-334, 158, 0.15f));
            K.Gadget(ItemKind.EmeticPoison, G(-305.5f, 160, 0.82f));
        }

        private void ClockTower()
        {
            const int Z = 0;
            MapKit.KitBuilding t = K.Building(Spec("Wind Clock Tower"));
            float lRoof = RoofY(t.spec);
            // The lantern on the roof: four columns, a pointed cap, clock faces on the top floor.
            for (int c = 0; c < 4; c++)
                K.Piece("Stucco", "Column", new Vector3(CLOCK.x + (c % 2 == 0 ? -4f : 4f), lRoof, CLOCK.z + (c < 2 ? -4f : 4f)), 0, null, K.Env, MapKit.Collide.Box);
            K.Block(K.Env, "LanternRoof", new Vector3(CLOCK.x, lRoof + 3.5f, CLOCK.z), new Vector3(9.6f, 0.3f, 9.6f), new Color(0.88f, 0.8f, 0.66f));
            GameObject lCap = K.Piece("Stucco", "RoofHip", new Vector3(CLOCK.x, lRoof + 3.6f, CLOCK.z), 0, new Color(0.25f, 0.5f, 0.45f), K.Env, MapKit.Collide.None);
            if (lCap != null) lCap.transform.localScale = new Vector3(9f, 6.5f, 9f);
            K.Mesh(K.Env, "Finial", CylMesh, new Vector3(CLOCK.x, lRoof + 11f, CLOCK.z), new Vector3(0.25f, 3f, 0.25f), new Color(0.85f, 0.7f, 0.3f));
            K.Mesh(K.Env, "FinialBall", DomeMesh, new Vector3(CLOCK.x, lRoof + 12.6f, CLOCK.z), Vector3.one * 0.8f, new Color(0.9f, 0.75f, 0.3f));
            for (int s = 0; s < 4; s++)
            {
                Quaternion r = Quaternion.Euler(0, s * 90f, 0);
                Vector3 lFace = new Vector3(CLOCK.x, lRoof - 1.9f, CLOCK.z) + r * new Vector3(0, 0, 4.75f);
                K.Mesh(K.Env, "ClockFace", CylMesh, lFace, new Vector3(3.2f, 0.12f, 3.2f), new Color(0.98f, 0.95f, 0.86f), r * Quaternion.Euler(90, 0, 0));
                K.Mesh(K.Env, "ClockRim", CylMesh, lFace - r * Vector3.forward * 0.03f, new Vector3(3.6f, 0.08f, 3.6f), new Color(0.75f, 0.58f, 0.25f), r * Quaternion.Euler(90, 0, 0));
                K.Block(K.Env, "Hand", lFace + r * new Vector3(0.35f, 0.3f, 0.1f), new Vector3(0.08f, 1.2f, 0.04f), new Color(0.15f, 0.15f, 0.18f), GameLayers.DEFAULT, false, r * Quaternion.Euler(0, 0, -35f));
                K.Block(K.Env, "Hand", lFace + r * new Vector3(-0.2f, 0.15f, 0.1f), new Vector3(0.08f, 0.8f, 0.04f), new Color(0.15f, 0.15f, 0.18f), GameLayers.DEFAULT, false, r * Quaternion.Euler(0, 0, 60f));
            }
            K.Lamp(new Vector3(CLOCK.x, lRoof + 3f, CLOCK.z), LANTERN, 12f, 1.6f, false);
            // The sniper nest and the zipline across the souk to the customs house roof.
            K.Npc(NpcRole.Lookout, new Vector3(CLOCK.x - 2.5f, lRoof + 0.1f, CLOCK.z - 2.5f), 225, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, new Vector3(CLOCK.x + 2.5f, lRoof + 0.15f, CLOCK.z - 2.5f));
            K.UniqueWeapon(ItemKind.RocketLauncher, t.Local(-2.5f, 9 * MapKit.STOREY + 0.15f, -2.5f), "Wind Clock tower");
            MapKit.KitSpec lCustoms = Spec("Customs House");
            K.Zipline(new Vector3(CLOCK.x - 3f, lRoof + 0.05f, CLOCK.z + 2.5f), new Vector3(lCustoms.origin.x + 4f, RoofY(lCustoms) + 0.05f, lCustoms.origin.z - 2f));

            // The plaza: paving, palms, benches, the old well, a guard at the door.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f;
                Vector3 p = CLOCK + Quaternion.Euler(0, a, 0) * Vector3.forward * 19f;
                Palm(p.x, p.z);
                P("Bench", p.x + Mathf.Sin(a * Mathf.Deg2Rad) * -3f, p.z + Mathf.Cos(a * Mathf.Deg2Rad) * -3f, a + 180f);
            }
            Vector3 lWell = G(-250, 232);
            K.Mesh(K.Env, "Well", CylMesh, lWell + Vector3.up * 0.5f, new Vector3(2.4f, 1f, 2.4f), STONE * 0.85f, null, true);
            K.Block(K.Env, "WellBeam", lWell + Vector3.up * 2.3f, new Vector3(2.8f, 0.18f, 0.18f), PLANK);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "WellPost", lWell + new Vector3(s * 1.3f, 1.2f, 0), new Vector3(0.2f, 2.4f, 0.2f), PLANK);
            Guard(Z, CLOCK.x, CLOCK.z - 7f, 180, ItemKind.Smg);
            K.Activity(ActivityType.Smoke, G(-215, 270, 0.1f), 270, Z);
            K.Activity(ActivityType.Lookout, new Vector3(CLOCK.x + 2.5f, lRoof + 0.1f, CLOCK.z + 2.5f), 45, Z);
            for (int i = 0; i < 4; i++) { P("LampPostOrnate", -262f + (i % 2) * 52f, 226f + (i / 2) * 56f, 0); K.Lamp(G(-262f + (i % 2) * 52f, 226f + (i / 2) * 56f, 4.1f), WARM, 13f, 1.5f, false); }
        }

        /// <summary>The Seven Lamps tea house: dining room and tea bar inside, the storytelling evening in the garden court.</summary>
        private void Teahouse()
        {
            const int Z = 0;
            MapKit.KitBuilding t = K.Building(Spec("Seven Lamps Teahouse"));
            float lRoof = RoofY(t.spec);
            K.Mesh(K.Env, "Dome", DomeMesh, new Vector3(-277f, lRoof - 0.4f, 112f), new Vector3(7f, 5.5f, 7f), new Color(0.95f, 0.9f, 0.8f), null, true).isStatic = true;
            K.Mesh(K.Env, "Dome", DomeMesh, new Vector3(-268.5f, lRoof - 0.6f, 112f), new Vector3(5f, 4f, 5f), new Color(0.3f, 0.55f, 0.55f), null, true).isStatic = true;
            Awnings(t, 0, FABRIC[1], 2, 5);
            Guard(Z, -274, 101.5f, 180, ItemKind.Pistol);
            Guard(Z, -266, 101.5f, 180, ItemKind.Smg);

            // The garden court: a carpet stage, cushions in rows, low tables with tea, lanterns strung across.
            Vector3 lStage = G(-270, 142);
            K.Block(K.Env, "StoryStage", lStage + Vector3.up * 0.25f, new Vector3(6f, 0.5f, 3.5f), PLANK * 0.85f);
            K.Prop("Rug", lStage + Vector3.up * 0.52f, 180, FABRIC[0], MapKit.Collide.None);
            K.Activity(ActivityType.Speech, lStage + new Vector3(0, 0.55f, 0), 180, Z, 18f);
            K.Prop("Armchair", lStage + new Vector3(0, 0.5f, 0.9f), 180, FABRIC[3]);
            K.DistractionKit(lStage + new Vector3(2.4f, 0.5f, 0.6f), 180, "radio");
            for (int row = 0; row < 3; row++)
                for (int c = 0; c < 4; c++)
                {
                    Vector3 p = G(-279f + c * 6f, 135f - row * 5f);
                    K.Prop("Rug", p + Vector3.up * 0.02f, 0, FABRIC[(row + c) % FABRIC.Length], MapKit.Collide.None);
                    GameObject lTable = K.Prop("TableRound", p, 0, PLANK * 0.9f, MapKit.Collide.Box, true);
                    if (lTable != null) lTable.transform.localScale = new Vector3(1f, 0.55f, 1f);
                    for (int k = 0; k < 2; k++) K.Mesh(K.Props, "Cushion", SackMesh, p + new Vector3(k == 0 ? -1f : 1f, 0.2f, 0), new Vector3(0.8f, 0.35f, 0.8f), FABRIC[(row + c + k + 2) % FABRIC.Length]).isStatic = true;
                    if ((row + c) % 3 == 0) K.DrinksOn(p + Vector3.up * 0.44f, 0, Z, 2);
                }
            K.Activity(ActivityType.Dining, G(-282, 125.5f, 0.1f), 0, Z);
            // Lanterns strung on a rope between two posts across the court.
            for (int s = 0; s < 2; s++) K.Block(K.Env, "LanternPost", G(s == 0 ? -293.5f : -247.5f, 138f, 2.4f), new Vector3(0.25f, 4.8f, 0.25f), PLANK * 0.8f);
            K.Block(K.Props, "LanternRope", G(-270.5f, 138f, 4.7f), new Vector3(46f, 0.03f, 0.03f), new Color(0.3f, 0.3f, 0.3f), GameLayers.DEFAULT, false);
            for (int i = 0; i < 7; i++) Lantern(G(-288f + i * 6f, 138f, 4.45f), 6f);
            for (int s = 0; s < 2; s++) Palm(s == 0 ? -292f : -248f, 140f, 0.95f);
            for (int i = 0; i < 6; i++) Nat("FlowerBed", -292f + (i % 2) * 44f, 124f + (i / 2) * 6f, 1f, 90f, new Color(0.85f, 0.35f, 0.45f), MapKit.Collide.None);
            K.ToiletKit(t.Local(-11.2f, 0, -5.8f), 90, Z);
            K.Activity(ActivityType.Phone, G(-252, 128, 0.1f), 90, Z);
            K.Activity(ActivityType.Smoke, G(-293, 104.5f, 0.1f), 180, Z);
            K.Weapon(ItemKind.Pistol, t.Local(-10f, 0.15f, 5f));
            K.Gadget(ItemKind.Poison, t.Local(8f, 1.1f, 4f));
        }

        private void Hammam()
        {
            const int Z = 0;
            MapKit.KitBuilding h = K.Building(Spec("Old Hammam"));
            float lRoof = RoofY(h.spec);
            for (int i = 0; i < 3; i++)
                K.Mesh(K.Env, "HammamDome", DomeMesh, h.Local(-10.5f + i * 9f, lRoof - h.root.position.y - 0.3f, 1.5f), new Vector3(6.5f, 4.6f, 6.5f), new Color(0.97f, 0.95f, 0.9f), null, true).isStatic = true;
            for (int i = 0; i < 12; i++)
                K.Mesh(K.Env, "Skylight", DomeMesh, h.Local(-12f + (i % 6) * 4.6f, lRoof - h.root.position.y + 0.05f, i < 6 ? -4.5f : 6f), new Vector3(0.5f, 0.3f, 0.5f), new Color(0.6f, 0.85f, 0.9f));
            // The warm pool in the back hall.
            Vector3 lPool = h.Local(-6f, 0, 4.2f);
            K.Block(K.Env, "PoolRim", lPool + Vector3.up * 0.3f, new Vector3(6f, 0.6f, 3.6f), new Color(0.85f, 0.9f, 0.92f), GameLayers.DEFAULT, true, Quaternion.Euler(0, h.Yaw, 0), true);
            K.Water(lPool + Vector3.up * 0.62f, new Vector2(5.4f, 3f));
            K.Activity(ActivityType.Idle, h.Local(3f, 0.1f, 3f), 0, Z);
            K.Weapon(ItemKind.Shotgun, h.Local(12f, 0.15f, 5f));
            K.Gadget(ItemKind.SmokeGrenade, h.Local(-14f, 0.15f, -5f));
        }

        private void SpiceWarehouse()
        {
            const int Z = 0;
            MapKit.KitBuilding w = K.Building(Spec("Amberline Spice Warehouse"));
            // Inside: mountains of sacks, the scales, the merchant's inspection; upstairs his office.
            for (int i = 0; i < 6; i++) Sacks(w.Local(-15f + i * 5f, 0, -6f), w.Yaw, 6);
            K.Prop("Table", w.Local(-3f, 0, 2.5f), w.Yaw, PLANK, MapKit.Collide.Box, true);
            K.Block(K.Props, "Scales", w.Local(-3f, 0.95f, 2.5f), new Vector3(0.5f, 0.35f, 0.3f), new Color(0.75f, 0.6f, 0.3f), GameLayers.DEFAULT, false, Quaternion.Euler(0, w.Yaw, 0));
            K.Activity(ActivityType.Inspect, w.Local(-3f, 0.1f, 1.2f), w.Yaw, Z);
            K.DrinksOn(w.Local(-3f, 1.6f + MapKit.STOREY - 0.8f, 3f), w.Yaw, Z, 2);
            K.UniqueWeapon(ItemKind.Flamethrower, w.Local(14f, 0.15f, 2f), "Spice warehouse");
            K.Npc(NpcRole.Guard, w.Local(6f, 0.1f, 0f), w.Yaw + 180f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, w.Local(-8f, MapKit.STOREY + 0.1f, -3f), w.Yaw, Z, ItemKind.Shotgun);
            K.Weapon(ItemKind.Smg, w.Local(-14f, MapKit.STOREY + 0.15f, 3.5f));
            K.Gadget(ItemKind.C4, w.Local(10f, 0.15f, -6.5f));
            // The hoist door over the yard, the loading yard: a truck, carts, crates; the lookout on the roof.
            Vector3 lHoist = Facade(w, 2, 6, MapKit.STOREY, 0.6f);
            K.Block(K.Env, "HoistBeam", lHoist + Vector3.up * 3.1f, new Vector3(0.25f, 0.25f, 2f), PLANK * 0.7f, GameLayers.DEFAULT, false, Quaternion.Euler(0, w.Yaw, 0));
            Deck(lHoist + Vector3.up * 0.02f + Quaternion.Euler(0, w.Yaw, 0) * Vector3.forward * 0.4f, new Vector2(3f, 1.6f), w.Yaw, 0.2f);
            K.Car(G(-410, 112), 180, new Color(0.6f, 0.55f, 0.45f));
            for (int i = 0; i < 4; i++) Clutter(G(-404f, 88f + i * 8f), 90, i);
            Sacks(G(-414, 92), 90, 6);
            Guard(Z, -408, 120, 0, ItemKind.Smg);
            K.Activity(ActivityType.Phone, G(-412, 86, 0.1f), 180, Z);
            K.Npc(NpcRole.Lookout, new Vector3(w.root.position.x + 3f, RoofY(w.spec) + 0.1f, w.root.position.z + 10f), 270, Z, ItemKind.Smg);
        }

        private void CustomsHouse()
        {
            const int Z = 0;
            MapKit.KitBuilding c = K.Building(Spec("Customs House"));
            Vector3 lFront = Facade(c, 0, 3, 0, 2.5f);
            P("Barrier", lFront.x - 0.5f, lFront.z - 4f, 0, null, true);
            K.Prop("StreetSign", lFront + new Vector3(0, 0, 4f), 270);
            Guard(Z, lFront + new Vector3(0, 0, -2.5f), 270, ItemKind.Shotgun);
            K.Npc(NpcRole.Lookout, new Vector3(c.root.position.x, RoofY(c.spec) + 0.1f, c.root.position.z + 6f), 270, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Shotgun, c.Local(-8f, 0.15f, 5f));
            K.Weapon(ItemKind.Pistol, c.Local(6f, 2 * MapKit.STOREY + 0.15f, -4f));
            K.Activity(ActivityType.Smoke, G(-401, 262, 0.1f), 90, Z);
            K.Gadget(ItemKind.SmokeGrenade, c.Local(-6f, MapKit.STOREY + 0.95f, 5f));
            // Seized goods stacked on the wall promenade in front.
            for (int i = 0; i < 3; i++) Clutter(G(-439f, 248f + i * 12f), 90, i + 2);
        }

        /// <summary>Two riad courtyards: tiled gardens with a fountain, orange trees and palms, shaded benches.</summary>
        private void Riads()
        {
            foreach (Rect r in new[] { RIAD_A, RIAD_B })
            {
                Vector3 c = G(r.center.x, r.center.y);
                PaintRect(r, new Color(0.62f, 0.7f, 0.68f), 0);
                K.Mesh(K.Env, "Basin", CylMesh, c + Vector3.up * 0.3f, new Vector3(3.4f, 0.6f, 3.4f), new Color(0.3f, 0.55f, 0.6f), null, true);
                K.Water(c + Vector3.up * 0.58f, new Vector2(2.6f, 2.6f));
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = c + new Vector3((i % 2 == 0 ? -1f : 1f) * (r.width * 0.32f), 0, (i < 2 ? -1f : 1f) * (r.height * 0.3f));
                    Nat("BroadleafC", p, 0.45f, -1f, new Color(0.35f, 0.55f, 0.3f));
                    for (int k = 0; k < 3; k++) K.Mesh(K.Props, "Orange", DomeMesh, p + new Vector3(Rand(-1.2f, 1.2f), Rand(2f, 3f), Rand(-1.2f, 1.2f)), Vector3.one * 0.18f, new Color(1f, 0.6f, 0.15f));
                }
                Palm(r.xMin + 2f, r.yMax - 2f, 0.8f);
                P("Bench", r.center.x, r.yMin + 1.5f, 0);
                P("Bench", r.center.x, r.yMax - 1.5f, 180);
                P("LampPostOrnate", r.xMax - 2f, r.yMin + 2f, 0);
                K.Lamp(G(r.xMax - 2f, r.yMin + 2f, 4.1f), LANTERN, 9f, 1.3f, false);
            }
            K.Activity(ActivityType.Idle, G(RIAD_A.center.x + 3f, RIAD_A.center.y, 0.1f), 0, 0);
            K.Activity(ActivityType.Phone, G(RIAD_B.center.x + 3f, RIAD_B.center.y, 0.1f), 0, 0);
            K.Weapon(ItemKind.Smg, G(RIAD_A.xMin + 2f, RIAD_A.yMin + 2f, 0.15f));
        }

        /// <summary>Alley life: crates, pots and bins at the corners, washing lines across, cats' corners, the odd stash.</summary>
        private void Alleys()
        {
            const int Z = 0;
            int lIndex = 0;
            foreach (SoukLot l in _soukLots)
            {
                bool lNorth = l.spec.yaw > 90f;
                // Clutter against the back wall, washing line to the next row on some.
                Vector3 lBack = Facade(l.built, 2, l.spec.width - 1, 0, 0.9f);
                if (lIndex % 3 == 0) Clutter(G(lBack.x, lBack.z), l.spec.yaw, lIndex);
                if (lIndex % 4 == 1) K.HidingKit(G(lBack.x, lBack.z), l.spec.yaw, "Dumpster");
                if (lIndex % 5 == 2 && !l.onLong)
                {
                    Vector3 lFront = Facade(l.built, 0, 0, 0, 0.8f);
                    Pots(G(lFront.x, lFront.z), 2, l.spec.yaw + 90f);
                    K.Prop("PlantPot", G(lFront.x + 0.5f, lFront.z), 0);
                }
                if (l.spec.floors >= 2 && lIndex % 3 == 1)
                {
                    Vector3 a = Facade(l.built, 0, 1, MapKit.STOREY + 2.6f, 0.1f);
                    Vector3 b = a + Quaternion.Euler(0, l.spec.yaw + 180f, 0) * Vector3.forward * 4.2f;
                    K.Block(K.Props, "Line", (a + b) * 0.5f, new Vector3(0.02f, 0.02f, 4.2f), new Color(0.3f, 0.3f, 0.3f), GameLayers.DEFAULT, false, Quaternion.LookRotation(b - a));
                    for (int k = 0; k < 3; k++) K.Block(K.Props, "Laundry", Vector3.Lerp(a, b, 0.25f + k * 0.25f) - Vector3.up * 0.4f, new Vector3(0.6f, 0.75f, 0.03f), FABRIC[(lIndex + k) % FABRIC.Length], GameLayers.DEFAULT, false, Quaternion.LookRotation(b - a) * Quaternion.Euler(0, 90, 0));
                }
                if (lIndex % 9 == 4) K.Weapon(lIndex % 2 == 0 ? ItemKind.Pistol : ItemKind.Smg, l.built.Local(-l.built.W * 0.3f, 0.15f, -l.built.D * 0.3f));
                if (lIndex % 23 == 6) K.Npc(NpcRole.Lookout, l.built.Local(-l.built.W * 0.25f, RoofY(l.spec) - l.built.root.position.y + 0.1f, -l.built.D * 0.25f), l.spec.yaw + 180f, Z, ItemKind.Smg);
                if (lIndex % 13 == 3) K.Activity(ActivityType.Smoke, G(lBack.x, lBack.z, 0.1f) + Quaternion.Euler(0, l.spec.yaw + 180f, 0) * Vector3.forward * -1.2f, l.spec.yaw, Z);
                lIndex++;
            }
            // Lamps at the alley mouths on the ring road.
            for (float x = -420f; x < -190f; x += 30f) { P("StreetLamp", x, 76.5f, 180); K.Lamp(G(x, 77.5f, 4.3f), WARM, 12f, 1.3f, false); }
            K.Gadget(ItemKind.Brick, G(-360, 102, 0.1f));
            K.Gadget(ItemKind.Coin, G(-230, 146, 0.1f));
        }
    }
}
