using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The 500 m map recipe the three maps share: a relief (height function) shaped by graded
    /// streets that climb it and by building pads cut into it at their street's height (stone
    /// retaining walls where the land falls away), painted ground, dense forests on everything left
    /// wild, and the gameplay dressing helpers (guards in rooms, patrol groups, crowds, loot).
    /// A map writes <see cref="Relief"/>, <see cref="Plan"/> (streets, lots, plazas: before the land
    /// exists) and <see cref="Dress"/> (everything placed on it).
    /// </summary>
    public abstract class HillMap : MapBuilderBase
    {
        protected const float HALF = 250f, SIZE = 500f, MIN_Y = -30f, MAX_Y = 90f;
        protected static readonly Color STONE = new Color(0.62f, 0.6f, 0.56f);

        protected LandShape Shape;
        protected readonly List<Street> Streets = new List<Street>();
        protected readonly List<LotInfo> Lots = new List<LotInfo>();
        private readonly List<(Rect rect, Ground ground)> _pads = new List<(Rect, Ground)>();
        private bool[,] _built;
        private const float MASK_CELL = 2f;

        public class Street
        {
            public Vector2[] path;
            public float width;
            public Ground ground;
        }

        public class LotInfo
        {
            public MapKit.KitSpec spec;
            public Vector2 center, size;
            public float yaw, height;
            public MapKit.KitBuilding built;
        }

        /// <summary>The land before any stamp: hills, valleys, ridges (metres).</summary>
        protected abstract float Relief(float pX, float pZ);
        /// <summary>Streets, lots and plazas, laid before the land is built.</summary>
        protected abstract void Plan();
        /// <summary>Everything on the land: interiors, scenes, NPCs, loot, zones, match points.</summary>
        protected abstract void Dress();
        /// <summary>0..1 how wooded a point left wild is.</summary>
        protected virtual float Woods(float pX, float pZ, float pHeight) => Mathf.Clamp01(0.55f + LandShape.Fbm(pX, pZ, 70f, 3, 2.2f));
        /// <summary>The trees that grow at a height.</summary>
        protected virtual string[] TreeSet(float pHeight) => new[] { "PineA", "PineB", "PineC", "BroadleafA", "BroadleafC", "BirchA" };
        /// <summary>Land under this height is water (no trees, no grass).</summary>
        protected virtual float WaterLevel => -100f;
        /// <summary>The natural ground: weights per layer before streets and pads.</summary>
        protected virtual void NaturalGround(float pX, float pZ, float pHeight, float pSlope, float[] pWeights)
        {
            float n = LandShape.Fbm(pX, pZ, 30f, 3, 1.7f);
            pWeights[(int)Ground.Grass] = 1f + n;
            pWeights[(int)Ground.DryGrass] = Mathf.Clamp01(0.4f - n);
            pWeights[(int)Ground.ForestFloor] = Mathf.Clamp01(Woods(pX, pZ, pHeight) - 0.5f) * 3f;
        }

        protected override void Compose()
        {
            Shape = new LandShape(Relief);
            Plan();
            K.BuildLand(SIZE, MIN_Y, MAX_Y, Shape.Height);
            BuildMask();
            K.PaintLand(PaintWeights);
            foreach (LotInfo lLot in Lots)
            {
                lLot.built = K.Building(lLot.spec);
                RetainingWalls(lLot);
            }
            Dress();
            Vegetate();
            Edges();
            K.CoverSpacing = 1.6f;
            K.DistanceCulling();
        }

        // ---------------------------------------------------------------- planning

        /// <summary>A graded street (it climbs the land as a ramp, its shoulders blend in).</summary>
        protected Street Road(float pWidth, Ground pGround, params Vector2[] pPath)
        {
            Shape.Road(pPath, pWidth, 5f, 26f);
            Street lStreet = new Street { path = pPath, width = pWidth, ground = pGround };
            Streets.Add(lStreet);
            return lStreet;
        }

        /// <summary>
        /// A building lot: the pad is cut at the land's height there (as shaped so far, so after the
        /// streets) plus <paramref name="pRaise"/>, or at <paramref name="pHeight"/> when given.
        /// </summary>
        protected LotInfo Lot(string pName, string pStyle, Vector2 pCenter, float pYaw, int pWidth, int pDepth, int pFloors, MapKit.Roof pRoof, Color pWall, Color pRoofTint, int pZone, float pRaise = 0f, float? pHeight = null)
        {
            float h = pHeight ?? Shape.Height(pCenter.x, pCenter.y) + pRaise;
            Vector2 lSize = new Vector2(pWidth * MapKit.MODULE, pDepth * MapKit.MODULE);
            bool lSwap = Mathf.Abs(Mathf.DeltaAngle(pYaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(pYaw, 270f)) < 1f;
            Shape.Flat(pCenter, lSize + Vector2.one * 5f, pYaw, h, 4f);
            MapKit.KitSpec lSpec = new MapKit.KitSpec
            {
                name = pName, style = pStyle, origin = new Vector3(pCenter.x, h + 0.05f, pCenter.y), yaw = pYaw, width = pWidth, depth = pDepth, floors = pFloors,
                roof = pRoof, wall = pWall, roofTint = pRoofTint, zone = pZone, seed = Mathf.Abs(pName.GetHashCode() % 100000) + 1, navigable = true
            };
            LotInfo lLot = new LotInfo { spec = lSpec, center = pCenter, size = lSwap ? new Vector2(lSize.y, lSize.x) : lSize, yaw = pYaw, height = h };
            Lots.Add(lLot);
            Clear.Add(R(pCenter.x - lLot.size.x * 0.5f - 4f, pCenter.y - lLot.size.y * 0.5f - 4f, pCenter.x + lLot.size.x * 0.5f + 4f, pCenter.y + lLot.size.y * 0.5f + 4f));
            return lLot;
        }

        /// <summary>
        /// A row of buildings facing a street on one side (+1 left of its direction, -1 right), from
        /// <paramref name="pFrom"/> to <paramref name="pTo"/> metres along it, each at the street's
        /// height in front of it (so they step down the slopes). <paramref name="pPick"/> describes
        /// the n-th building (style, width, depth, floors, roof, wall, roof tint, rooms).
        /// </summary>
        protected List<LotInfo> Frontage(string pPrefix, Street pStreet, float pFrom, float pTo, int pSide, float pGap, int pZone, System.Func<int, (string style, int w, int d, int floors, MapKit.Roof roof, Color wall, Color roofTint, Room[] rooms)> pPick)
        {
            List<LotInfo> lRow = new List<LotInfo>();
            float lAlong = pFrom;
            for (int n = 0; lAlong < pTo; n++)
            {
                var lDef = pPick(n);
                float lFront = lDef.w * MapKit.MODULE;
                Vector2 c = PointAt(pStreet.path, lAlong + lFront * 0.5f, out Vector2 d);
                if (lAlong + lFront > pTo) break;
                Vector2 lNormal = Vector2.Perpendicular(d) * pSide;
                Vector2 lCenter = c + lNormal * (pStreet.width * 0.5f + 2.2f + lDef.d * MapKit.MODULE * 0.5f);
                float lYaw = Mathf.Round(Mathf.Atan2(lNormal.x, lNormal.y) * Mathf.Rad2Deg);
                LotInfo lLot = Lot(pPrefix + n, lDef.style, lCenter, lYaw, lDef.w, lDef.d, lDef.floors, lDef.roof, lDef.wall, lDef.roofTint, pZone, 0f, Shape.Height(c.x, c.y) + 0.15f);
                Doors(lLot.spec, 0, lDef.w / 2, 2, 0);
                if (lDef.rooms != null) Rooms(lLot.spec, lDef.rooms);
                lLot.spec.climb = new Vector2Int(1, 1);
                lRow.Add(lLot);
                lAlong += lFront + pGap;
            }
            return lRow;
        }

        /// <summary>The point at a distance along a polyline, and the direction there.</summary>
        protected static Vector2 PointAt(Vector2[] pPath, float pDistance, out Vector2 pDirection)
        {
            for (int s = 0; s < pPath.Length - 1; s++)
            {
                float l = Vector2.Distance(pPath[s], pPath[s + 1]);
                pDirection = (pPath[s + 1] - pPath[s]).normalized;
                if (pDistance <= l || s == pPath.Length - 2) return pPath[s] + pDirection * Mathf.Min(pDistance, l);
                pDistance -= l;
            }
            pDirection = Vector2.up;
            return pPath[0];
        }

        protected static float Length(Vector2[] pPath)
        {
            float l = 0f;
            for (int s = 0; s < pPath.Length - 1; s++) l += Vector2.Distance(pPath[s], pPath[s + 1]);
            return l;
        }

        /// <summary>A paved square (flattened at a height when given), kept free of trees.</summary>
        protected void Plaza(Rect pRect, Ground pGround, float? pHeight = null, float pFeather = 5f)
        {
            if (pHeight.HasValue) Shape.Flat(pRect, pHeight.Value, pFeather);
            _pads.Add((pRect, pGround));
            Clear.Add(pRect);
        }

        protected static void Doors(MapKit.KitSpec pSpec, params int[] pSideModule)
        {
            for (int i = 0; i + 1 < pSideModule.Length; i += 2) pSpec.doors.Add(new Vector2Int(pSideModule[i], pSideModule[i + 1]));
        }

        /// <summary>Rooms per floor, two per floor (None for one room).</summary>
        protected static void Rooms(MapKit.KitSpec pSpec, params Room[] pPairs)
        {
            for (int i = 0; i + 1 < pPairs.Length; i += 2)
                pSpec.rooms.Add(pPairs[i + 1] == Room.None ? new[] { pPairs[i] } : new[] { pPairs[i], pPairs[i + 1] });
        }

        // ---------------------------------------------------------------- the land

        /// <summary>A coarse mask of what is built (streets, lots, plazas): no trees, no grass there.</summary>
        private void BuildMask()
        {
            int n = Mathf.CeilToInt(SIZE / MASK_CELL);
            _built = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = -HALF + (i + 0.5f) * MASK_CELL, z = -HALF + (j + 0.5f) * MASK_CELL;
                    _built[i, j] = !IsClear(x, z) || OnStreet(x, z, 3.5f) >= 0;
                }
        }

        protected bool Built(float pX, float pZ)
        {
            int i = Mathf.FloorToInt((pX + HALF) / MASK_CELL), j = Mathf.FloorToInt((pZ + HALF) / MASK_CELL);
            return i < 0 || j < 0 || i >= _built.GetLength(0) || j >= _built.GetLength(1) || _built[i, j];
        }

        /// <summary>The street a point is on (within its half width plus a margin), or -1.</summary>
        protected int OnStreet(float pX, float pZ, float pMargin)
        {
            Vector2 p = new Vector2(pX, pZ);
            for (int s = 0; s < Streets.Count; s++)
            {
                if (!LandShape.PathBounds(Streets[s].path, Streets[s].width * 0.5f + pMargin).Contains(p)) continue;
                if (LandShape.PathDistance(Streets[s].path, p, out _, out _) <= Streets[s].width * 0.5f + pMargin) return s;
            }
            return -1;
        }

        private float[] PaintWeights(float pX, float pZ, float pHeight, float pSlope)
        {
            float[] w = new float[8];
            NaturalGround(pX, pZ, pHeight, pSlope, w);
            w[(int)Ground.Dirt] += Mathf.Clamp01((pSlope - 20f) / 12f) * 2f;
            w[(int)Ground.Rock] += Mathf.Clamp01((pSlope - 30f) / 8f) * 8f;
            int s = OnStreet(pX, pZ, 0f);
            if (s >= 0) { w[(int)Streets[s].ground] += 40f; return w; }
            if (OnStreet(pX, pZ, 1.5f) >= 0) w[(int)Ground.Dirt] += 6f;
            Vector2 p = new Vector2(pX, pZ);
            foreach ((Rect lRect, Ground lGround) in _pads) if (lRect.Contains(p)) { w[(int)lGround] += 40f; return w; }
            foreach (LotInfo lLot in Lots)
                if (Mathf.Abs(pX - lLot.center.x) < lLot.size.x * 0.5f + 2.5f && Mathf.Abs(pZ - lLot.center.y) < lLot.size.y * 0.5f + 2.5f) { w[(int)Ground.Cobble] += 20f; return w; }
            return w;
        }

        /// <summary>Stone walls along the pad edges that stand above the land (terraces read as built).</summary>
        private void RetainingWalls(LotInfo pLot)
        {
            Transform lGroup = K.Group("Retaining_" + pLot.spec.name, K.Env);
            Vector2 lHalf = pLot.size * 0.5f + Vector2.one * 2.5f;
            Vector2[] lCorners = { new Vector2(-lHalf.x, -lHalf.y), new Vector2(lHalf.x, -lHalf.y), new Vector2(lHalf.x, lHalf.y), new Vector2(-lHalf.x, lHalf.y) };
            for (int e = 0; e < 4; e++)
            {
                Vector2 a = pLot.center + lCorners[e], b = pLot.center + lCorners[(e + 1) % 4];
                Vector2 lOut = (Vector2.Perpendicular(b - a).normalized) * -1f;
                int lSteps = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / 3f));
                for (int k = 0; k < lSteps; k++)
                {
                    Vector2 c = Vector2.Lerp(a, b, (k + 0.5f) / lSteps);
                    float lBelow = K.GroundAt(c.x + lOut.x * 2.5f, c.y + lOut.y * 2.5f);
                    float lDrop = pLot.height - lBelow;
                    if (Mathf.Abs(lDrop) < 1.2f) continue;
                    // A drop: the wall holds the pad up (with a parapet); a rise: it holds the hill back.
                    float lTop = lDrop > 0f ? pLot.height + 0.9f : lBelow + 0.3f;
                    float lBottom = lDrop > 0f ? lBelow - 0.5f : pLot.height - 0.3f;
                    K.Block(lGroup, "Retaining", new Vector3(c.x, (lTop + lBottom) * 0.5f, c.y), new Vector3(Vector2.Distance(a, b) / lSteps + 0.05f, lTop - lBottom, 0.7f), GameArt_Vary(STONE, k + e * 7), Core.GameLayers.DEFAULT, true, Quaternion.LookRotation(new Vector3(b.x - a.x, 0, b.y - a.y)) * Quaternion.Euler(0, 90, 0));
                }
            }
        }

        private static Color GameArt_Vary(Color pColor, int pSeed)
        {
            float v = 0.92f + ((pSeed * 7919) % 17) / 100f;
            return new Color(pColor.r * v, pColor.g * v, pColor.b * v);
        }

        /// <summary>Forests on everything left wild (dense where <see cref="Woods"/> says so), bushes, grass.</summary>
        private void Vegetate()
        {
            System.Random lRandom = new System.Random(K.Map.sceneName.GetHashCode());
            for (float x = -HALF + 2f; x < HALF - 2f; x += 3.6f)
                for (float z = -HALF + 2f; z < HALF - 2f; z += 3.6f)
                {
                    float px = x + (float)lRandom.NextDouble() * 3f, pz = z + (float)lRandom.NextDouble() * 3f;
                    if (Built(px, pz)) continue;
                    float h = K.GroundAt(px, pz);
                    if (h < WaterLevel + 0.6f || K.LandSlope(px, pz) > 40f) continue;
                    float lWoods = Woods(px, pz, h);
                    double lRoll = lRandom.NextDouble();
                    if (lRoll < lWoods * 0.8f)
                    {
                        string[] lSet = TreeSet(h);
                        K.LandTree(lSet[lRandom.Next(lSet.Length)], px, pz, 0.75f + (float)lRandom.NextDouble() * 0.55f);
                    }
                    else if (lRoll < lWoods * 0.8f + 0.12f) K.LandTree(lRandom.NextDouble() < 0.5 ? "BushA" : "BushB", px, pz, 0.7f + (float)lRandom.NextDouble() * 0.7f);
                    else if (lRoll > 0.985) K.LandTree(lRandom.NextDouble() < 0.5 ? "RockB" : "RockC", px, pz, 0.6f + (float)lRandom.NextDouble() * 0.8f);
                }
            K.LandGrass((x, z) => Built(x, z) || K.GroundAt(x, z) < WaterLevel + 0.4f || K.LandSlope(x, z) > 32f ? 0f : Mathf.Clamp01(0.45f + LandShape.Fbm(x, z, 25f, 2, 9.1f)));
        }

        private void Edges()
        {
            for (int s = 0; s < 4; s++)
            {
                Vector3 lPos = s switch { 0 => new Vector3(0, 40, -HALF + 1f), 1 => new Vector3(HALF - 1f, 40, 0), 2 => new Vector3(0, 40, HALF - 1f), _ => new Vector3(-HALF + 1f, 40, 0) };
                Vector3 lSize = s % 2 == 0 ? new Vector3(SIZE, 160, 2) : new Vector3(2, 160, SIZE);
                K.Block(K.Env, "MapEdge", lPos, lSize, Color.white).GetComponent<MeshRenderer>().enabled = false;
            }
        }

        // ---------------------------------------------------------------- dressing helpers

        protected Vector3 G(float pX, float pZ, float pUp = 0.1f) => K.At(pX, pZ, pUp);
        protected Vector3 G(Vector2 p, float pUp = 0.1f) => K.At(p.x, p.y, pUp);

        /// <summary>A world point inside a building: local x/z, a floor, height above that floor.</summary>
        protected static Vector3 In(LotInfo pLot, float pX, int pFloor, float pZ, float pUp = 0.1f) => pLot.built.Local(pX, pFloor * MapKit.STOREY + pUp, pZ);

        /// <summary>
        /// Guards in a building: one at the front door, then one per room spot on each floor (up to
        /// <paramref name="pInside"/>), plus a weapon inside.
        /// </summary>
        protected void Staff(LotInfo pLot, int pZone, int pInside, ItemKind pWeapon, bool pDoorGuard = true, ItemKind pLoot = ItemKind.Pistol)
        {
            float hw = pLot.built.W * 0.5f - 1.6f, hd = pLot.built.D * 0.5f - 1.6f;
            if (pDoorGuard) K.Npc(NpcRole.Guard, In(pLot, 1.5f, 0, -pLot.built.D * 0.5f - 1.6f), pLot.yaw + 180f, pZone, pWeapon);
            for (int i = 0; i < pInside; i++)
            {
                int lFloor = i % Mathf.Max(1, pLot.spec.floors);
                float x = (i / Mathf.Max(1, pLot.spec.floors)) % 2 == 0 ? -hw * 0.6f : hw * 0.6f, z = (i % 3 - 1) * hd * 0.5f;
                K.Npc(NpcRole.Guard, In(pLot, x, lFloor, z), pLot.yaw + 90f * (i % 4), pZone, i % 3 == 2 ? ItemKind.Shotgun : pWeapon);
            }
            K.Weapon(pLoot, In(pLot, -hw * 0.3f, 0, hd * 0.6f, 0.2f));
        }

        /// <summary>A patrol group of up to five on ground points.</summary>
        protected void Squad(int pZone, int pSize, ItemKind pWeapon, float pPause, params Vector2[] pPoints)
        {
            Vector3[] lPoints = new Vector3[pPoints.Length];
            for (int i = 0; i < pPoints.Length; i++) lPoints[i] = G(pPoints[i]);
            K.Patrol(pZone, pWeapon, pPause, pSize, lPoints);
        }

        protected void Guard(int pZone, float pX, float pZ, float pYaw, ItemKind pWeapon) => K.Npc(NpcRole.Guard, G(pX, pZ), pYaw, pZone, pWeapon);

        /// <summary>Street dressing along a street: lamps, benches, bins, parked cars every so often.</summary>
        protected void Dress(Street pStreet, Color pLamp, bool pCars, float pEvery = 22f)
        {
            float lRun = 0f;
            int k = 0;
            for (int s = 0; s < pStreet.path.Length - 1; s++)
            {
                Vector2 a = pStreet.path[s], b = pStreet.path[s + 1], d = (b - a).normalized, n = Vector2.Perpendicular(d);
                float lLength = Vector2.Distance(a, b);
                for (float t = pEvery - lRun; t < lLength; t += pEvery, k++)
                {
                    Vector2 c = a + d * t;
                    int lSide = k % 2 == 0 ? 1 : -1;
                    Vector2 p = c + n * lSide * (pStreet.width * 0.5f + 0.9f);
                    if (!IsClear(p.x, p.y)) continue;
                    float lYaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + (lSide > 0 ? 90f : -90f);
                    if (k % 3 == 0) K.Lamp(G(p, 0f), pLamp, 14f, 1.5f, true);
                    else if (k % 3 == 1) K.Prop("Bench", G(p, 0f), lYaw + 180f, null, MapKit.Collide.Box, true);
                    else K.Prop("TrashBin", G(p, 0f), lYaw, null, MapKit.Collide.Box);
                    if (pCars && k % 4 == 2)
                    {
                        Vector2 lCar = c - n * lSide * (pStreet.width * 0.5f - 1.4f);
                        K.Car(G(lCar, 0f), Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, new[] { new Color(0.7f, 0.15f, 0.12f), new Color(0.2f, 0.3f, 0.5f), new Color(0.85f, 0.85f, 0.82f), new Color(0.15f, 0.15f, 0.17f) }[k % 4]);
                    }
                }
                lRun = (lRun + lLength) % pEvery;
            }
        }

        /// <summary>Activity nodes of every kind around a point (the zone's routine places).</summary>
        protected void Routines(int pZone, Vector2 pCenter, float pRadius)
        {
            ActivityType[] lAll = { ActivityType.Idle, ActivityType.Phone, ActivityType.Smoke, ActivityType.Inspect, ActivityType.Garden, ActivityType.Lookout, ActivityType.Speech, ActivityType.Dining, ActivityType.Office, ActivityType.Workout };
            for (int i = 0; i < lAll.Length; i++)
            {
                float a = i * Mathf.PI * 2f / lAll.Length;
                Vector2 p = pCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * pRadius;
                K.Activity(lAll[i], G(p), a * Mathf.Rad2Deg, pZone);
            }
        }

        /// <summary>Stairs on the land from one point up (or down) to another, stone.</summary>
        protected void LandStairs(Vector2 pFrom, Vector2 pTo, float pWidth = 2.4f)
        {
            Vector3 a = G(pFrom, 0f), b = G(pTo, 0f);
            if (a.y > b.y) (a, b) = (b, a);
            K.Stairs(K.Env, a, b, pWidth, STONE);
        }
    }
}
