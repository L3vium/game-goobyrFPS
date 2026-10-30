using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <b>Pinecrest Hollow</b> - a cold mountain valley of 1 km², walled by forested ridges and
    /// cliffs. A river comes down a gorge from the north, crosses an upper terrace, drops over a
    /// waterfall onto the valley floor, runs along Millbrook's quays and ends in a lake in the
    /// south-east corner.
    /// <list type="bullet">
    /// <item><b>Millbrook</b> (zone 0, south-west): main street with boardwalks, the square and its
    /// timber festival, the town hall bell tower, the Split Rail inn, the sheriff, back streets of
    /// houses, the water tower (zipline to the sawmill), the sawmill and log yard across the river,
    /// the rail depot.</item>
    /// <item><b>The Grove & Outpost</b> (zone 1, north terrace): a palisaded commune on an island of
    /// a marsh crossed by channels and stilt walkways, the longhouse ceremony, greenhouse and
    /// orchard; next to it a fenced military outpost on a hill: watchtowers, barracks, HQ, bunker,
    /// motor pool, comms mast with the alarm flare, helipad.</item>
    /// <item><b>Lakeside Lodge</b> (zone 2, south-east): a three-floor lodge on a stone terrace above
    /// the lake - evening reception in the ballroom, offices, suites, basement breaker and air
    /// handler down a service ramp, guest house, gardens, gazebo, boathouse and pier.</item>
    /// </list>
    /// Between them: logging roads with patrol cars, the ranger station, the campsite, the quarry
    /// and its old mine, clearings of tall grass. The map is split per district into partial files;
    /// this one holds the terrain (a height function, carved river and lake, graded roads, pads)
    /// and the shared dressing helpers.
    /// </summary>
    public partial class MapForest : MapBuilderBase
    {
        protected override string MapFile => "Map_Forest";

        [MenuItem("Goobyr/Build/Map Forest")]
        public static void BuildMenu() => new MapForest().Build();

        // ---------------------------------------------------------------- palette
        private static readonly Color MEADOW = new Color(0.5f, 0.58f, 0.36f);
        private static readonly Color FOREST_FLOOR = new Color(0.33f, 0.42f, 0.27f);
        private static readonly Color ALPINE = new Color(0.55f, 0.58f, 0.42f);
        private static readonly Color ROCK = new Color(0.5f, 0.5f, 0.49f);
        private static readonly Color SNOW = new Color(0.9f, 0.93f, 0.96f);
        private static readonly Color DIRT = new Color(0.55f, 0.45f, 0.33f);
        private static readonly Color STREET = new Color(0.6f, 0.52f, 0.4f);
        private static readonly Color TRAIL = new Color(0.48f, 0.42f, 0.31f);
        private static readonly Color GRAVEL = new Color(0.6f, 0.58f, 0.53f);
        private static readonly Color RIVERBED = new Color(0.38f, 0.4f, 0.36f);
        private static readonly Color MUD = new Color(0.36f, 0.37f, 0.25f);
        private static readonly Color SAND = new Color(0.74f, 0.69f, 0.56f);
        private static readonly Color COBBLE = new Color(0.62f, 0.6f, 0.56f);
        private static readonly Color STONE = new Color(0.66f, 0.64f, 0.6f);
        private static readonly Color PLANK = new Color(0.58f, 0.43f, 0.3f);
        private static readonly Color LOG = new Color(0.55f, 0.4f, 0.27f);
        private static readonly Color WARM = new Color(1f, 0.82f, 0.58f);

        protected override void Compose()
        {
            K.Grid(N, N, CELL, new Vector2(ORIGIN, ORIGIN), MEADOW);
            PlanTown();
            PlanGrove();
            PlanOutpost();
            PlanLodge();
            PlanWilds();
            Terrain();

            Waters();
            Town();
            Sawmill();
            Grove();
            Outpost();
            Lodge();
            Wilds();
            Vegetation();
            MatchPoints();
            MapEdges();
            // Trees at ~250 m, a chair at ~40 m, a building at ~300 m (the fog hides the rest).
            K.DistanceCulling();
        }

        // ================================================================ terrain

        private const int N = 250;
        private const float CELL = 4f, ORIGIN = -500f;
        /// <summary>Level of the upper terrace (north), on which the Grove and the campsite sit.</summary>
        private const float UPPER = 10f;
        private const float RIVER_WATER = -3.4f, RIVER_BANK = -3f, RIVER_BED = -4.6f;

        private const byte WET = 1, ROAD = 2, BUILT = 4, BARE = 8, BANK = 16, CLIFF = 32;

        private readonly float[,] _h = new float[N, N];
        private readonly Color[,] _c = new Color[N, N];
        private readonly Color?[,] _paint = new Color?[N, N];
        private readonly byte[,] _f = new byte[N, N];
        private float[,] _corner;

        private readonly List<System.Action> _zoneStage = new List<System.Action>();
        private readonly List<System.Action> _lotStage = new List<System.Action>();
        private readonly List<System.Action> _carveStage = new List<System.Action>();
        private readonly List<System.Action> _paintStage = new List<System.Action>();
        private readonly List<RoadDef> _roads = new List<RoadDef>();

        private class RoadDef
        {
            public Vector2[] points;
            public float width;
            public Color color;
            public int window;
        }

        /// <summary>The river from the north gorge to the lake; the waterfall is at <see cref="FALL"/>.</summary>
        private static readonly Vector2[] RIVER =
        {
            new Vector2(-60, 520), new Vector2(-72, 430), new Vector2(-42, 340), new Vector2(-58, 250), new Vector2(-46, 152),
            new Vector2(-60, 100), new Vector2(-82, 20), new Vector2(-80, -50), new Vector2(-76, -110), new Vector2(-62, -185),
            new Vector2(-10, -245), new Vector2(80, -282), new Vector2(200, -305)
        };
        private const int FALL = 4;
        private const float FALL_Z = 152f;

        private static float Cx(int pI) => ORIGIN + (pI + 0.5f) * CELL;
        private static int Ci(float pX) => Mathf.Clamp(Mathf.FloorToInt((pX - ORIGIN) / CELL), 0, N - 1);

        /// <summary>Every cell whose centre lies in the rectangle.</summary>
        private static void Cells(Rect pRect, System.Action<int, int> pAction)
        {
            int i0 = Mathf.Max(0, Mathf.CeilToInt((pRect.xMin - ORIGIN) / CELL - 0.5f)), i1 = Mathf.Min(N - 1, Mathf.FloorToInt((pRect.xMax - ORIGIN) / CELL - 0.5f));
            int j0 = Mathf.Max(0, Mathf.CeilToInt((pRect.yMin - ORIGIN) / CELL - 0.5f)), j1 = Mathf.Min(N - 1, Mathf.FloorToInt((pRect.yMax - ORIGIN) / CELL - 0.5f));
            for (int i = i0; i <= i1; i++) for (int j = j0; j <= j1; j++) pAction(i, j);
        }

        private static Rect Expand(Rect pRect, float pBy) => new Rect(pRect.xMin - pBy, pRect.yMin - pBy, pRect.width + pBy * 2f, pRect.height + pBy * 2f);

        private static float RectDistance(Rect pRect, float pX, float pZ)
        {
            float dx = Mathf.Max(pRect.xMin - pX, 0f, pX - pRect.xMax), dz = Mathf.Max(pRect.yMin - pZ, 0f, pZ - pRect.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float Border(float pX, float pZ) => Mathf.Min(Mathf.Min(pX - ORIGIN, -ORIGIN - pX), Mathf.Min(pZ - ORIGIN, -ORIGIN - pZ));

        /// <summary>Where the valley floor climbs to the upper terrace (a cliff band, cut by roads).</summary>
        private static float TerraceLine(float pX) => 150f + 30f * Mathf.Sin((pX + 50f) / 140f) + (Mathf.PerlinNoise(pX / 45f + 2.7f, 0.5f) - 0.5f) * 26f * Mathf.SmoothStep(0f, 1f, (Mathf.Abs(pX - RIVER[FALL].x) - 12f) / 40f);

        /// <summary>1 below the edge, 0 above it, with a short smooth band (a cliff when steps are tall).</summary>
        private static float Below(float pValue, float pEdge, float pBand = 5f) => 1f - Mathf.SmoothStep(0f, 1f, (pValue - pEdge + pBand) / (pBand * 2f));

        private static float BaseHeight(float x, float z)
        {
            float h = UPPER * (1f - Below(z, TerraceLine(x), 5f));
            // The quarry shelf (north-west), ten metres above the terrace.
            float lShelfX = -250f + 25f * Mathf.Sin(z / 70f);
            h += 10f * Below(x, lShelfX, 6f) * (1f - Below(z, 265f, 6f));
            // Ridges on every edge: a forested foothill shelf, then two cliff steps.
            float d = Border(x, z) + (Mathf.PerlinNoise(x * 0.011f + 3.1f, z * 0.011f + 7.7f) - 0.5f) * 34f;
            float n = Mathf.PerlinNoise(x * 0.021f + 11.3f, z * 0.021f + 5.2f);
            h += 7f * Below(d, 82f, 6f) + 14f * (0.8f + n * 0.4f) * Below(d, 58f, 3f) + 17f * (0.7f + n * 0.6f) * Below(d, 32f, 3f);
            // A rolling forest floor.
            h += (Mathf.PerlinNoise(x / 55f + 1.3f, z / 55f + 8.1f) - 0.5f) * 3.6f + (Mathf.PerlinNoise(x / 17f + 4.4f, z / 17f) - 0.5f) * 1.1f;
            return h;
        }

        /// <summary>Flattens a rectangle to a height, blending into the terrain over a feather.</summary>
        private void Flatten(Rect pRect, float pHeight, float pFeather, byte pFlag = 0)
        {
            Cells(Expand(pRect, pFeather), (i, j) =>
            {
                float d = RectDistance(pRect, Cx(i), Cx(j));
                if (d <= 0.01f) { _h[i, j] = pHeight; _f[i, j] |= pFlag; return; }
                if (pFeather <= 0f) return;
                _h[i, j] = Mathf.Lerp(pHeight, _h[i, j], Mathf.SmoothStep(0f, 1f, d / pFeather));
            });
        }

        private void FlattenDisc(Vector2 pCenter, float pRadius, float pHeight, float pFeather, byte pFlag = 0)
        {
            Cells(new Rect(pCenter.x - pRadius - pFeather, pCenter.y - pRadius - pFeather, (pRadius + pFeather) * 2f, (pRadius + pFeather) * 2f), (i, j) =>
            {
                float d = Vector2.Distance(pCenter, new Vector2(Cx(i), Cx(j))) - pRadius;
                if (d <= 0f) { _h[i, j] = pHeight; _f[i, j] |= pFlag; return; }
                if (pFeather <= 0f) return;
                _h[i, j] = Mathf.Lerp(pHeight, _h[i, j], Mathf.SmoothStep(0f, 1f, d / pFeather));
            });
        }

        private void PaintRect(Rect pRect, Color pColor, byte pFlag = BARE) => _paintStage.Add(() => Cells(pRect, (i, j) => { _paint[i, j] = pColor; _f[i, j] |= pFlag; }));

        private void PaintDisc(Vector2 pCenter, float pRadius, Color pColor, byte pFlag = BARE) => _paintStage.Add(() =>
            Cells(new Rect(pCenter.x - pRadius, pCenter.y - pRadius, pRadius * 2f, pRadius * 2f), (i, j) =>
            {
                if (Vector2.Distance(pCenter, new Vector2(Cx(i), Cx(j))) > pRadius) return;
                _paint[i, j] = pColor;
                _f[i, j] |= pFlag;
            }));

        /// <summary>A graded road: its heights are smoothed along its length, so it climbs terraces in ramps.</summary>
        private void Road(float pWidth, Color pColor, params Vector2[] pPoints) => _roads.Add(new RoadDef { points = pPoints, width = pWidth, color = pColor, window = 9 });

        private void Trail(params Vector2[] pPoints) => _roads.Add(new RoadDef { points = pPoints, width = 3f, color = TRAIL, window = 5 });

        private static (float distance, int segment, float t) Nearest(Vector2[] pLine, Vector2 pPoint)
        {
            float lBest = float.MaxValue; int lSeg = 0; float lT = 0f;
            for (int s = 0; s < pLine.Length - 1; s++)
            {
                Vector2 a = pLine[s], b = pLine[s + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(pPoint - a, ab) / ab.sqrMagnitude);
                float d = Vector2.Distance(pPoint, a + ab * t);
                if (d < lBest) { lBest = d; lSeg = s; lT = t; }
            }
            return (lBest, lSeg, lT);
        }

        private float RawHeight(float pX, float pZ) => _h[Ci(pX), Ci(pZ)];

        private void Terrain()
        {
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) _h[i, j] = BaseHeight(Cx(i), Cx(j));
            foreach (System.Action lStage in _zoneStage) lStage();
            CarveRiver();
            CarveLake();
            foreach (RoadDef lRoad in _roads) GradeRoad(lRoad);
            foreach (System.Action lStage in _lotStage) lStage();
            foreach (System.Action lStage in _carveStage) lStage();
            Colours();
            foreach (System.Action lStage in _paintStage) lStage();

            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    K.Height(new Rect(ORIGIN + i * CELL, ORIGIN + j * CELL, CELL, CELL), _h[i, j], _paint[i, j] ?? _c[i, j]);

            // Corner heights as the ground mesh builds them, for placing things on slopes.
            _corner = new float[N + 1, N + 1];
            for (int x = 0; x <= N; x++)
                for (int z = 0; z <= N; z++)
                {
                    float lSum = 0f; int lCount = 0;
                    for (int dx = -1; dx <= 0; dx++)
                        for (int dz = -1; dz <= 0; dz++)
                        {
                            int cx = x + dx, cz = z + dz;
                            if (cx < 0 || cz < 0 || cx >= N || cz >= N) continue;
                            lSum += _h[cx, cz];
                            lCount++;
                        }
                    _corner[x, z] = lSum / lCount;
                }
        }

        private void CarveRiver()
        {
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    float x = Cx(i), z = Cx(j);
                    (float d, int lSeg, float t) = Nearest(RIVER, new Vector2(x, z));
                    if (d > 22f) continue;
                    bool lUpper = Mathf.Abs(z - FALL_Z) < 14f ? z > FALL_Z : lSeg < FALL;
                    float L = lUpper ? UPPER : 0f;
                    float w = 7f;
                    if (!lUpper && z > FALL_Z - 26f) w = 10f; // the plunge pool under the fall
                    if (d < w) { _h[i, j] = L + RIVER_BED; _f[i, j] |= WET; }
                    else if (d < w + 5f) { _h[i, j] = L + RIVER_BANK; _f[i, j] |= BANK; }
                    else if (d < w + 11f) _h[i, j] = Mathf.Lerp(L + RIVER_BANK, _h[i, j], Mathf.SmoothStep(0f, 1f, (d - w - 5f) / 6f));
                }
        }

        private static float LakeShape(float x, float z) => Sq((x - 330f) / 185f) + Sq((z + 335f) / 165f) + (Mathf.PerlinNoise(x * 0.02f + 2f, z * 0.02f + 9f) - 0.5f) * 0.14f;
        private static float Sq(float v) => v * v;

        private void CarveLake()
        {
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    float x = Cx(i), z = Cx(j);
                    float d = Border(x, z) + (Mathf.PerlinNoise(x * 0.03f, z * 0.03f + 4f) - 0.5f) * 14f;
                    if (d < 40f) continue;
                    float e = LakeShape(x, z);
                    if (e < 1f) { _h[i, j] = Mathf.Lerp(-3.9f, -7.5f, Mathf.SmoothStep(0f, 1f, (1f - e) * 5f)); _f[i, j] |= WET; }
                    else if (e < 1.12f) _h[i, j] = Mathf.Min(_h[i, j], Mathf.Lerp(-2.4f, _h[i, j], (e - 1f) / 0.12f));
                }
        }

        private void GradeRoad(RoadDef pRoad)
        {
            // Samples every 2 m along the centre line, their heights smoothed along the road.
            List<Vector2> lSamples = new List<Vector2>();
            for (int s = 0; s < pRoad.points.Length - 1; s++)
            {
                Vector2 a = pRoad.points[s], b = pRoad.points[s + 1];
                int lSteps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2f));
                for (int k = 0; k < lSteps; k++) lSamples.Add(Vector2.Lerp(a, b, (float)k / lSteps));
            }
            lSamples.Add(pRoad.points[pRoad.points.Length - 1]);
            float[] lHeights = new float[lSamples.Count];
            for (int k = 0; k < lSamples.Count; k++)
            {
                int ci = Ci(lSamples[k].x), cj = Ci(lSamples[k].y);
                lHeights[k] = (_f[ci, cj] & (WET | BANK)) != 0 ? float.NaN : _h[ci, cj];
            }
            // Bridges: the deck is level with the road on both banks.
            for (int k = 0; k < lHeights.Length; k++)
            {
                if (!float.IsNaN(lHeights[k])) continue;
                int a = k; while (a > 0 && float.IsNaN(lHeights[a])) a--;
                int b = k; while (b < lHeights.Length - 1 && float.IsNaN(lHeights[b])) b++;
                float ha = float.IsNaN(lHeights[a]) ? 0f : lHeights[a], hb = float.IsNaN(lHeights[b]) ? ha : lHeights[b];
                for (int m = a + 1; m < b; m++) lHeights[m] = Mathf.Lerp(ha, hb, (float)(m - a) / (b - a));
            }
            for (int it = 0; it < 3; it++)
            {
                float[] lCopy = (float[])lHeights.Clone();
                for (int k = 0; k < lHeights.Length; k++)
                {
                    float lSum = 0f; int lCount = 0;
                    for (int m = Mathf.Max(0, k - pRoad.window); m <= Mathf.Min(lHeights.Length - 1, k + pRoad.window); m++) { lSum += lCopy[m]; lCount++; }
                    lHeights[k] = lSum / lCount;
                }
            }

            float lHalf = pRoad.width * 0.5f, lShoulder = 5f;
            Rect lBox = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            foreach (Vector2 p in lSamples) lBox = Rect.MinMaxRect(Mathf.Min(lBox.xMin, p.x), Mathf.Min(lBox.yMin, p.y), Mathf.Max(lBox.xMax, p.x), Mathf.Max(lBox.yMax, p.y));
            Cells(Expand(lBox, lHalf + lShoulder + 2f), (i, j) =>
            {
                if ((_f[i, j] & (WET | BANK)) != 0) return;
                Vector2 c = new Vector2(Cx(i), Cx(j));
                float lBest = float.MaxValue; int lIndex = 0;
                for (int k = 0; k < lSamples.Count; k++)
                {
                    float d = (lSamples[k] - c).sqrMagnitude;
                    if (d < lBest) { lBest = d; lIndex = k; }
                }
                float lDist = Mathf.Sqrt(lBest);
                if (lDist > lHalf + lShoulder + 1.5f) return;
                float lTarget = lHeights[lIndex];
                if (lDist <= lHalf + 1.5f)
                {
                    _h[i, j] = lTarget;
                    _f[i, j] |= ROAD;
                    _paint[i, j] = pRoad.color;
                }
                else _h[i, j] = Mathf.Lerp(lTarget, _h[i, j], Mathf.SmoothStep(0f, 1f, (lDist - lHalf - 1.5f) / lShoulder));
            });
        }

        private float Slope(int i, int j)
        {
            float lMax = 0f;
            if (i > 0) lMax = Mathf.Max(lMax, Mathf.Abs(_h[i, j] - _h[i - 1, j]));
            if (i < N - 1) lMax = Mathf.Max(lMax, Mathf.Abs(_h[i, j] - _h[i + 1, j]));
            if (j > 0) lMax = Mathf.Max(lMax, Mathf.Abs(_h[i, j] - _h[i, j - 1]));
            if (j < N - 1) lMax = Mathf.Max(lMax, Mathf.Abs(_h[i, j] - _h[i, j + 1]));
            return lMax;
        }

        private void Colours()
        {
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    float x = Cx(i), z = Cx(j), h = _h[i, j], s = Slope(i, j);
                    float n = Mathf.PerlinNoise(x / 40f + 7f, z / 40f + 3f);
                    Color c = Color.Lerp(MEADOW, FOREST_FLOOR, Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x / 90f + 1f, z / 90f + 2f) - 0.35f) * 2.2f));
                    if (h > 24f) c = Color.Lerp(c, ALPINE, Mathf.Clamp01((h - 24f) / 8f));
                    if (s > 1.8f) c = Color.Lerp(c, Color.Lerp(DIRT, ROCK, 0.6f), Mathf.Clamp01((s - 1.8f) / 1.4f));
                    if (s > 3.2f) { c = Color.Lerp(ROCK, ROCK * 0.85f, n); _f[i, j] |= CLIFF; }
                    if (h > 40f && s < 7f) c = Color.Lerp(c, SNOW, Mathf.Clamp01((h - 40f) / 3f));
                    if ((_f[i, j] & BANK) != 0) c = Color.Lerp(GRAVEL, SAND, n * 0.5f);
                    if ((_f[i, j] & WET) != 0) c = RIVERBED;
                    _c[i, j] = c;
                }
        }

        // ================================================================ placement helpers

        /// <summary>The ground surface (as meshed, corners averaged) under a point.</summary>
        private float Surf(float pX, float pZ)
        {
            float u = (pX - ORIGIN) / CELL, v = (pZ - ORIGIN) / CELL;
            int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, N - 1), j = Mathf.Clamp(Mathf.FloorToInt(v), 0, N - 1);
            float fu = Mathf.Clamp01(u - i), fv = Mathf.Clamp01(v - j);
            float a = Mathf.Lerp(_corner[i, j], _corner[i + 1, j], fu), b = Mathf.Lerp(_corner[i, j + 1], _corner[i + 1, j + 1], fu);
            return Mathf.Lerp(a, b, fv);
        }

        private Vector3 G(float pX, float pZ, float pUp = 0f) => new Vector3(pX, Surf(pX, pZ) + pUp, pZ);
        private Vector3 G(Vector2 pPoint, float pUp = 0f) => G(pPoint.x, pPoint.y, pUp);

        private bool Flag(float pX, float pZ, byte pFlag) => (_f[Ci(pX), Ci(pZ)] & pFlag) != 0;

        private float Rand(float pMin, float pMax) => Mathf.Lerp(pMin, pMax, (float)K.Rng.NextDouble());

        private GameObject P(string pName, float pX, float pZ, float pYaw, Color? pTint = null, bool pCover = false, MapKit.Collide pCollide = MapKit.Collide.Box, float pUp = 0f)
            => K.Prop(pName, G(pX, pZ, pUp), pYaw, pTint, pCollide, pCover);

        private GameObject P(string pName, Vector3 pAt, float pYaw, Color? pTint = null, bool pCover = false, MapKit.Collide pCollide = MapKit.Collide.Box)
            => K.Prop(pName, pAt, pYaw, pTint, pCollide, pCover);

        private GameObject Nat(string pName, float pX, float pZ, float pScale, float pYaw = -1f, Color? pTint = null, MapKit.Collide pCollide = MapKit.Collide.Box, float pSink = 0f)
        {
            Color lTint = GameArt.Vary(pTint ?? MapKit.DefaultTint("Nature", pName), 0.1f, K.Rng);
            return K.NaturePiece(pName, G(pX, pZ, -pSink), pYaw < 0f ? Rand(0f, 360f) : pYaw, pScale, lTint, pCollide);
        }

        /// <summary>A building lot: the kit spec, with its ground pad queued for the terrain stage.</summary>
        private MapKit.KitSpec Lot(string pName, string pStyle, float pX, float pZ, float pYaw, int pW, int pD, int pFloors, MapKit.Roof pRoof, Color pWall, Color pRoof2, int pZone, float pGround, float pFeather = 8f)
        {
            MapKit.KitSpec lSpec = new MapKit.KitSpec
            {
                name = pName, style = pStyle, origin = new Vector3(pX, pGround + 0.05f, pZ), yaw = pYaw, width = pW, depth = pD, floors = pFloors,
                roof = pRoof, wall = pWall, roofTint = pRoof2, zone = pZone, seed = Mathf.Abs(pName.GetHashCode() % 100000) + 1, navigable = true
            };
            Rect lFoot = Footprint(pX, pZ, pYaw, pW, pD);
            _lotStage.Add(() => Flatten(Expand(lFoot, 3f), pGround, pFeather, BUILT));
            return lSpec;
        }

        private static Rect Footprint(float pX, float pZ, float pYaw, int pW, int pD)
        {
            bool lSwap = Mathf.Abs(Mathf.DeltaAngle(pYaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(pYaw, 270f)) < 1f;
            float hw = (lSwap ? pD : pW) * MapKit.MODULE * 0.5f, hd = (lSwap ? pW : pD) * MapKit.MODULE * 0.5f;
            return Rect.MinMaxRect(pX - hw, pZ - hd, pX + hw, pZ + hd);
        }

        private static void Doors(MapKit.KitSpec pSpec, params int[] pSideModule)
        {
            for (int i = 0; i + 1 < pSideModule.Length; i += 2) pSpec.doors.Add(new Vector2Int(pSideModule[i], pSideModule[i + 1]));
        }

        private static void Floors(MapKit.KitSpec pSpec, params MapKit.Room[] pRoomsPerFloor)
        {
            // Pairs per floor: front room, back room (None = single room).
            for (int i = 0; i + 1 < pRoomsPerFloor.Length; i += 2)
                pSpec.rooms.Add(pRoomsPerFloor[i + 1] == MapKit.Room.None ? new[] { pRoomsPerFloor[i] } : new[] { pRoomsPerFloor[i], pRoomsPerFloor[i + 1] });
        }

        private static float SideYaw(int pSide) => pSide switch { 0 => 180f, 1 => 90f, 2 => 0f, _ => 270f };
        private static Vector3 SideNormal(int pSide) => pSide switch { 0 => Vector3.back, 1 => Vector3.right, 2 => Vector3.forward, _ => Vector3.left };
        private static Vector3 SideTangent(int pSide) => pSide switch { 0 => Vector3.right, 1 => Vector3.forward, 2 => Vector3.left, _ => Vector3.back };

        /// <summary>A world point on a facade: side, module (centre of the module), height, distance out of the wall.</summary>
        private static Vector3 Facade(MapKit.KitBuilding pB, int pSide, float pModule, float pY, float pOut)
        {
            int lCount = pSide % 2 == 0 ? pB.spec.width : pB.spec.depth;
            float lHalfOther = (pSide % 2 == 0 ? pB.D : pB.W) * 0.5f;
            float lAlong = -lCount * MapKit.MODULE * 0.5f + MapKit.MODULE * 0.5f + pModule * MapKit.MODULE;
            return pB.root.TransformPoint(SideNormal(pSide) * (lHalfOther + pOut) + SideTangent(pSide) * lAlong + Vector3.up * pY);
        }

        private float FacadeYaw(MapKit.KitBuilding pB, int pSide) => pB.Yaw + SideYaw(pSide);

        /// <summary>Striped awnings over ground-floor modules of a facade (shop fronts).</summary>
        private void Awnings(MapKit.KitBuilding pB, int pSide, Color pTint, params int[] pModules)
        {
            foreach (int m in pModules) K.Piece(pB.spec.style, "Awning", Facade(pB, pSide, m, 2.15f, 0f), FacadeYaw(pB, pSide), pTint, pB.root, MapKit.Collide.None);
        }

        /// <summary>A ladder (kit pieces stacked) with its climbable volume; it faces <paramref name="pYaw"/>.</summary>
        private void Ladder(Vector3 pBottom, float pHeight, float pYaw, string pStyle = "Timber")
        {
            Vector3 lFront = Quaternion.Euler(0, pYaw, 0) * Vector3.forward;
            for (float y = 0f; y < pHeight - 0.5f; y += 4f) K.Piece(pStyle, "Ladder", pBottom + Vector3.up * y, pYaw, null, K.Env, MapKit.Collide.None);
            GameObject lClimb = K.Climbable(K.Env, pBottom + Vector3.up * (pHeight * 0.5f + 0.3f) - lFront * 0.05f, new Vector3(0.8f, pHeight + 0.6f, 0.3f), Quaternion.LookRotation(-lFront), Color.white, false);
            lClimb.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>Climbable ivy on a wall or a rock face: a green sheet with leafy bushes.</summary>
        private void Ivy(Vector3 pBottom, float pHeight, float pWidth, float pYaw)
        {
            Vector3 lFront = Quaternion.Euler(0, pYaw, 0) * Vector3.forward;
            K.Climbable(K.Env, pBottom + Vector3.up * pHeight * 0.5f, new Vector3(pWidth, pHeight, 0.3f), Quaternion.LookRotation(-lFront), new Color(0.27f, 0.45f, 0.22f), false);
            for (float y = 0.5f; y < pHeight; y += 1.6f)
                for (float x = -pWidth * 0.4f; x <= pWidth * 0.4f; x += 1.4f)
                {
                    GameObject lLeaf = K.NaturePiece(K.Rng.NextDouble() < 0.5 ? "BushA" : "BushC", pBottom + Quaternion.Euler(0, pYaw, 0) * new Vector3(x, y - 0.4f, 0.1f), Rand(0, 360), Rand(0.5f, 0.8f), GameArt.Vary(new Color(0.3f, 0.5f, 0.25f), 0.1f, K.Rng), MapKit.Collide.None);
                    if (lLeaf != null) lLeaf.transform.localScale = new Vector3(lLeaf.transform.localScale.x, lLeaf.transform.localScale.y, 0.35f);
                }
        }

        /// <summary>A row of kit pieces along a line, one every <paramref name="pStep"/> metres, facing the line's side.</summary>
        private void Row(string pStyle, string pPiece, Vector3 pFrom, Vector3 pTo, float pStep, float pYawOffset, Color? pTint = null, MapKit.Collide pCollide = MapKit.Collide.Mesh, Transform pParent = null)
        {
            Vector3 lDir = pTo - pFrom;
            float lLength = new Vector2(lDir.x, lDir.z).magnitude;
            int lCount = Mathf.Max(1, Mathf.RoundToInt(lLength / pStep));
            float lYaw = Quaternion.LookRotation(new Vector3(lDir.x, 0, lDir.z)).eulerAngles.y + pYawOffset;
            for (int i = 0; i < lCount; i++) K.Piece(pStyle, pPiece, Vector3.Lerp(pFrom, pTo, (i + 0.5f) / lCount), lYaw, pTint, pParent ?? K.Env, pCollide);
        }

        /// <summary>A wooden deck (boardwalk, platform, pier top) with plank stripes.</summary>
        private void Deck(Vector3 pCenter, Vector2 pSize, float pYaw, float pThickness = 0.3f, Color? pColor = null)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Color lColor = pColor ?? PLANK;
            K.Block(K.Env, "Deck", pCenter - Vector3.up * pThickness * 0.5f, new Vector3(pSize.x, pThickness, pSize.y), lColor, GameLayers.DEFAULT, true, lRot);
            for (float z = -pSize.y * 0.5f + 0.6f; z < pSize.y * 0.5f; z += 1.2f)
                K.Block(K.Env, "Plank", pCenter + lRot * new Vector3(0, 0.005f, z), new Vector3(pSize.x - 0.1f, 0.02f, 0.06f), lColor * 0.8f, GameLayers.DEFAULT, false, lRot);
        }

        /// <summary>A bridge: deck, kit railings (or balustrades) on both sides, posts or piers below.</summary>
        private void Span(Vector3 pFrom, Vector3 pTo, float pWidth, bool pStone, float pPierDepth = 4.5f)
        {
            Vector3 lDir = pTo - pFrom;
            Vector3 lFlat = new Vector3(lDir.x, 0, lDir.z);
            Quaternion lRot = Quaternion.LookRotation(lFlat);
            Color lColor = pStone ? STONE : PLANK;
            K.Block(K.Env, "BridgeDeck", (pFrom + pTo) * 0.5f - Vector3.up * 0.3f, new Vector3(pWidth, 0.6f, lFlat.magnitude + 0.4f), lColor, GameLayers.DEFAULT, true, lRot);
            if (!pStone)
                for (float z = -lFlat.magnitude * 0.5f + 0.5f; z < lFlat.magnitude * 0.5f; z += 1f)
                    K.Block(K.Env, "Plank", (pFrom + pTo) * 0.5f + lRot * new Vector3(0, 0.01f, z), new Vector3(pWidth - 0.1f, 0.02f, 0.06f), lColor * 0.8f, GameLayers.DEFAULT, false, lRot);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 lSide = lRot * Vector3.right * (s * (pWidth * 0.5f - 0.2f));
                Row(pStone ? "Classic" : "Timber", pStone ? "Balustrade" : "Railing", pFrom + lSide, pTo + lSide, 3f, 90f * s);
            }
            int lPiers = Mathf.Max(1, Mathf.RoundToInt(lFlat.magnitude / 9f));
            for (int i = 1; i < lPiers; i++)
            {
                Vector3 p = Vector3.Lerp(pFrom, pTo, (float)i / lPiers);
                if (pStone) K.Block(K.Env, "Pier", p - Vector3.up * (0.6f + pPierDepth * 0.5f), new Vector3(pWidth * 0.8f, pPierDepth, 1.6f), STONE * 0.9f, GameLayers.DEFAULT, true, lRot);
                else for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Pile", p + lRot * Vector3.right * (s * pWidth * 0.4f) - Vector3.up * (0.6f + pPierDepth * 0.5f), new Vector3(0.35f, pPierDepth, 0.35f), LOG, GameLayers.DEFAULT, true, lRot);
            }
        }

        /// <summary>A kit watchtower (platform at 7 m, ladder on its front) with a lookout and a rifle.</summary>
        private void Tower(int pZone, float pX, float pZ, float pYaw, Color? pTint = null, bool pLookout = true)
        {
            Vector3 lBase = G(pX, pZ, -0.05f);
            K.Prop("Watchtower", lBase, pYaw, pTint, MapKit.Collide.Mesh);
            Vector3 lFront = Quaternion.Euler(0, pYaw, 0) * Vector3.forward;
            GameObject lClimb = K.Climbable(K.Env, lBase + lFront * 2.05f + Vector3.up * 4.2f, new Vector3(0.8f, 8.4f, 0.3f), Quaternion.LookRotation(-lFront), Color.white, false);
            lClimb.GetComponent<MeshRenderer>().enabled = false;
            if (pLookout) K.Npc(NpcRole.Lookout, lBase + Vector3.up * 7.15f - lFront * 0.6f, pYaw + 180f, pZone, ItemKind.Sniper);
            K.Weapon(K.Rng.NextDouble() < 0.5 ? ItemKind.Sniper : ItemKind.Smg, lBase + Vector3.up * 7.2f + Quaternion.Euler(0, pYaw, 0) * new Vector3(1f, 0, 0.6f));
        }

        /// <summary>A guard on a post, with a sandbag wall in front when asked.</summary>
        private void Guard(int pZone, Vector3 pAt, float pYaw, ItemKind pWeapon, bool pSandbags = false)
        {
            K.Npc(NpcRole.Guard, pAt + Vector3.up * 0.1f, pYaw, pZone, pWeapon);
            if (pSandbags) K.Prop("Sandbags", pAt + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0, 1.3f), pYaw, null, MapKit.Collide.Box, true);
        }

        private void Guard(int pZone, float pX, float pZ, float pYaw, ItemKind pWeapon, bool pSandbags = false) => Guard(pZone, G(pX, pZ), pYaw, pWeapon, pSandbags);

        /// <summary>A patrol on ground points (y from the surface).</summary>
        private void Walk(int pZone, ItemKind pWeapon, float pPause, params Vector2[] pPoints)
        {
            Vector3[] lPoints = new Vector3[pPoints.Length];
            for (int i = 0; i < pPoints.Length; i++) lPoints[i] = G(pPoints[i], 0.1f);
            K.Patrol(pZone, pWeapon, pPause, lPoints);
        }

        /// <summary>A patrol car on a road: dense waypoints that follow the ground, driving on the right.</summary>
        private void Cruiser(float pSpeed, float pOffset, Color pColor, params Vector2[] pPoints)
        {
            List<Vector3> lRoute = new List<Vector3>();
            for (int s = 0; s < pPoints.Length; s++)
            {
                Vector2 a = pPoints[s], b = pPoints[(s + 1) % pPoints.Length];
                Vector2 lRight = new Vector2(b.y - a.y, -(b.x - a.x)).normalized * 2f;
                int lSteps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 8f));
                for (int k = 0; k < lSteps; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)k / lSteps) + lRight;
                    lRoute.Add(G(p, 0.05f));
                }
            }
            K.PatrolCar(pSpeed, pOffset, pColor, lRoute.ToArray());
        }

        private void Clutter(Vector3 pAt, float pYaw, int pSeed)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            switch (pSeed % 4)
            {
                case 0: K.Crate(pAt); K.Crate(pAt + r * new Vector3(1.15f, 0, 0.1f)); K.Crate(pAt + r * new Vector3(0.5f, 1.05f, 0)); break;
                case 1: K.Prop("Pallet", pAt, pYaw); K.Crate(pAt + Vector3.up * 0.15f, 1.6f); K.Barrel(pAt + r * new Vector3(1.6f, 0, 0), false); break;
                case 2: K.Barrel(pAt, false); K.Barrel(pAt + r * new Vector3(0.7f, 0, 0.2f), false); K.Barrel(pAt + r * new Vector3(0.3f, 0, 0.8f), false); break;
                default: K.Prop("TrashBin", pAt, pYaw, null, MapKit.Collide.Box); K.Prop("Pallet", pAt + r * new Vector3(1.4f, 0, 0), pYaw + 90f); break;
            }
        }

        // ================================================================ water, edges

        private void Waters()
        {
            // The river as rotated planes along its course; the waterfall is a steep plane with foam.
            for (int s = 0; s < RIVER.Length - 1; s++)
            {
                Vector2 a = RIVER[s], b = RIVER[s + 1];
                bool lUpper = s < FALL;
                float y = (lUpper ? UPPER : 0f) + RIVER_WATER;
                Vector2 lDir = (b - a).normalized;
                Vector2 a2 = s == FALL ? a + lDir * 3f : a - lDir * 6f;
                Vector2 b2 = s == FALL - 1 ? b : b + lDir * 6f;
                GameObject lPlane = K.Water(new Vector3((a2.x + b2.x) * 0.5f, y, (a2.y + b2.y) * 0.5f), new Vector2(s >= FALL && s <= FALL ? 22f : 17f, Vector2.Distance(a2, b2)));
                lPlane.transform.rotation = Quaternion.LookRotation(new Vector3(lDir.x, 0, lDir.y));
            }
            Vector2 lLip = RIVER[FALL];
            Vector3 lTop = new Vector3(lLip.x, UPPER + RIVER_WATER, lLip.y + 0.5f), lBottom = new Vector3(lLip.x - 0.6f, RIVER_WATER, lLip.y - 4.5f);
            GameObject lFall = K.Water((lTop + lBottom) * 0.5f, new Vector2(13f, Vector3.Distance(lTop, lBottom)));
            lFall.transform.rotation = Quaternion.LookRotation(lBottom - lTop);
            for (int i = 0; i < 14; i++)
            {
                Vector3 lFoam = new Vector3(lLip.x + Rand(-6f, 6f), RIVER_WATER + Rand(-0.2f, 0.3f), lLip.y - Rand(4f, 9f));
                K.Mesh(K.Env, "Foam", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0.2f, 7), "Forest_Foam"), lFoam, new Vector3(Rand(1.4f, 2.6f), Rand(0.5f, 0.9f), Rand(1.4f, 2.4f)), new Color(0.88f, 0.94f, 0.98f));
            }
            for (int i = 0; i < 6; i++)
                K.Mesh(K.Env, "Spray", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0.2f, 7), "Forest_Foam"), new Vector3(lLip.x + Rand(-5f, 5f), Rand(0f, 7f), lLip.y - 1.5f), Vector3.one * Rand(0.6f, 1.2f), new Color(0.92f, 0.96f, 1f));

            // The lake.
            K.Water(new Vector3(318, RIVER_WATER, -330), new Vector2(380, 340));
        }

        /// <summary>Invisible walls on the map edges, behind the ridges.</summary>
        private void MapEdges()
        {
            for (int s = 0; s < 4; s++)
            {
                Vector3 lPos = s switch { 0 => new Vector3(0, 40, -498), 1 => new Vector3(498, 40, 0), 2 => new Vector3(0, 40, 498), _ => new Vector3(-498, 40, 0) };
                Vector3 lSize = s % 2 == 0 ? new Vector3(1000, 120, 2) : new Vector3(2, 120, 1000);
                K.Block(K.Env, "MapEdge", lPos, lSize, Color.white).GetComponent<MeshRenderer>().enabled = false;
            }
        }
    }
}
