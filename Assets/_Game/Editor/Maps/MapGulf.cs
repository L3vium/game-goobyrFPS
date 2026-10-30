using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <b>Mirage Bay</b> - a sun-bleached Gulf city of 1 km² on a late golden afternoon. The sea
    /// closes the south and the west; dunes and a sandstone ridge close the north and the east.
    /// <list type="bullet">
    /// <item><b>Old Souk</b> (zone 0, north-west): a walled plateau above an old harbour, a maze of
    /// alleys between flat-roofed houses joined by planks, two covered market streets crossing at
    /// the Spice Crossing, the Wind Clock tower (sniper nest), the Seven Lamps tea house, the
    /// hammam, the spice warehouse and the customs house on the harbour wall.</item>
    /// <item><b>Smugglers' Docks</b> (zone 1, south-west): the container port under the elevated
    /// Gulf Highway, stacked container lanes, gantry cranes (one ziplines down), the moored cargo
    /// ship with its hold, the warehouses and their deal, the fuel depot, the drainage culvert
    /// under the quay, the harbour office with the alarm flare.</item>
    /// <item><b>The Zenith</b> (zone 2, south-east): a podium and an eighteen-floor hotel tower
    /// (lobby with chandelier, pool deck, sky lounge, penthouse, helipad), basement parking with
    /// the breaker and the air handler, a scaffold up its east face, the construction site and
    /// its crane, the gardens, the marina and the beach club.</item>
    /// </list>
    /// Between them: the corniche along the sea, the dry wadi with bridges and walkable banks, the
    /// Palm Interchange roundabout under the highway ramp, the Sandgate apartments, the
    /// caravanserai, the embassy villas, the desert road and its petrol station. This file holds
    /// the terrain (a height function, the carved sea and wadi, graded roads, pads) and the
    /// shared dressing helpers; each district has its own partial.
    /// </summary>
    public partial class MapGulf : MapBuilderBase
    {
        protected override string MapFile => "Map_Gulf";

        [MenuItem("Goobyr/Build/Map Gulf")]
        public static void BuildMenu() => new MapGulf().Build();

        // ---------------------------------------------------------------- palette
        private static readonly Color SAND = new Color(0.86f, 0.75f, 0.56f);
        private static readonly Color DUNE = new Color(0.9f, 0.72f, 0.48f);
        private static readonly Color SCRUB = new Color(0.74f, 0.68f, 0.5f);
        private static readonly Color ROCK = new Color(0.68f, 0.54f, 0.42f);
        private static readonly Color PAVE = new Color(0.86f, 0.8f, 0.7f);
        private static readonly Color COBBLE = new Color(0.74f, 0.67f, 0.56f);
        private static readonly Color ASPHALT = new Color(0.36f, 0.36f, 0.38f);
        private static readonly Color CONCRETE = new Color(0.66f, 0.65f, 0.62f);
        private static readonly Color BED = new Color(0.72f, 0.65f, 0.52f);
        private static readonly Color BEACH = new Color(0.95f, 0.88f, 0.7f);
        private static readonly Color SEABED = new Color(0.55f, 0.6f, 0.52f);
        private static readonly Color LAWN = new Color(0.45f, 0.6f, 0.32f);
        private static readonly Color STONE = new Color(0.9f, 0.84f, 0.72f);
        private static readonly Color PLANK = new Color(0.6f, 0.44f, 0.3f);
        private static readonly Color WARM = new Color(1f, 0.8f, 0.55f);
        private static readonly Color LANTERN = new Color(1f, 0.62f, 0.3f);

        private static readonly Color[] FABRIC =
        {
            new Color(0.78f, 0.22f, 0.18f), new Color(0.15f, 0.42f, 0.5f), new Color(0.88f, 0.62f, 0.2f), new Color(0.48f, 0.22f, 0.45f),
            new Color(0.25f, 0.5f, 0.3f), new Color(0.92f, 0.85f, 0.7f), new Color(0.2f, 0.28f, 0.55f)
        };

        protected override void Compose()
        {
            K.Grid(N, N, CELL, new Vector2(ORIGIN, ORIGIN), SAND);
            PlanSouk();
            PlanPort();
            PlanZenith();
            PlanBetween();
            Terrain();

            Seas();
            Souk();
            Port();
            Zenith();
            Between();
            Highway();
            Vegetation();
            MatchPoints();
            MapEdges();
            NudgeActivities();
            // 1.4x the forest's thresholds: the souk is dense and seen whole from the north road; past
            // ~300 m the haze hides a building anyway, a palm goes at ~180 m, a stall at ~30 m.
            K.DistanceCulling(0.049f, 0.07f, 0.042f, 0.035f);
            Debug.Log($"[Build] Map_Gulf: {K.Root.GetComponentsInChildren<Transform>(true).Length} objects");
        }

        // ================================================================ terrain

        private const int N = 250;
        private const float CELL = 4f, ORIGIN = -500f;
        /// <summary>Water surface, sea floor, the coastal plain (port, corniche, Zenith), the souk plateau and its harbour quay.</summary>
        private const float SEA = -1.6f, SEA_FLOOR = -6f, YARD = 1.5f, SOUK = 4f, HARBOUR = 0.8f;

        private const byte WET = 1, ROAD = 2, BUILT = 4, BARE = 8, BANK = 16, CLIFF = 32, SEAF = 64;

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
        private readonly Dictionary<string, MapKit.KitSpec> _specs = new Dictionary<string, MapKit.KitSpec>();
        /// <summary>Rectangles kept free of scrub and palms (districts, yards, plazas).</summary>
        private readonly List<Rect> _tame = new List<Rect>();

        private class RoadDef
        {
            public Vector2[] points;
            public float width;
            public Color color;
            public int window;
        }

        /// <summary>The dry wadi, from the northern dunes to its mouth on the beach east of the docks.</summary>
        private static readonly Vector2[] WADI =
        {
            new Vector2(60, 520), new Vector2(22, 400), new Vector2(-40, 300), new Vector2(-98, 205), new Vector2(-116, 100),
            new Vector2(-108, 0), new Vector2(-92, -120), new Vector2(-70, -200), new Vector2(-56, -262), new Vector2(-50, -345), new Vector2(-48, -400)
        };

        private static float Cx(int pI) => ORIGIN + (pI + 0.5f) * CELL;
        private static int Ci(float pX) => Mathf.Clamp(Mathf.FloorToInt((pX - ORIGIN) / CELL), 0, N - 1);

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

        /// <summary>1 below the edge, 0 above it, with a smooth band.</summary>
        private static float Below(float pValue, float pEdge, float pBand = 5f) => 1f - Mathf.SmoothStep(0f, 1f, (pValue - pEdge + pBand) / (pBand * 2f));

        /// <summary>How much of a dune desert a point is: the north and the east (land edges).</summary>
        private static float Desert(float x, float z) => Mathf.Clamp01(Mathf.Max(Mathf.SmoothStep(0f, 1f, (z - 110f) / 130f), Mathf.SmoothStep(0f, 1f, (x - 330f) / 90f) * Mathf.SmoothStep(0f, 1f, (z + 60f) / 80f)));

        /// <summary>Distance to the land edges (north, east above the coast, the north-west headland).</summary>
        private static float LandBorder(float x, float z)
        {
            float d = 500f - z;
            if (z > -80f) d = Mathf.Min(d, 500f - x);
            if (z > 350f) d = Mathf.Min(d, x + 500f);
            return d;
        }

        private static float BaseHeight(float x, float z)
        {
            // A coastal plain rising gently inland, dunes in the desert, a sandstone ridge on the land edges.
            float h = YARD + Mathf.SmoothStep(0f, 1f, (z + 140f) / 560f) * 5f;
            float lCrest = 1f - Mathf.Abs(Mathf.PerlinNoise(x / 85f + 3.3f, z / 50f + 1.7f) * 2f - 1f);
            h += Desert(x, z) * (lCrest * lCrest * 7f + Mathf.PerlinNoise(x / 28f + 5f, z / 28f + 2f) * 1.5f);
            float d = LandBorder(x, z) + (Mathf.PerlinNoise(x * 0.012f + 3.1f, z * 0.012f + 7.7f) - 0.5f) * 30f;
            float n = Mathf.PerlinNoise(x * 0.025f + 11.3f, z * 0.025f + 5.2f);
            h += 5f * Below(d, 75f, 10f) + 10f * (0.7f + n * 0.6f) * Below(d, 45f, 4f) + 9f * (0.6f + n * 0.8f) * Below(d, 22f, 3f);
            h += (Mathf.PerlinNoise(x / 60f + 1.3f, z / 60f + 8.1f) - 0.5f) * 1.6f;
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

        /// <summary>A graded road: its heights are smoothed along its length; it bridges the wadi at bank level.</summary>
        private void Road(float pWidth, Color pColor, params Vector2[] pPoints) => _roads.Add(new RoadDef { points = pPoints, width = pWidth, color = pColor, window = 9 });

        private void Lane(float pWidth, Color pColor, params Vector2[] pPoints) => _roads.Add(new RoadDef { points = pPoints, width = pWidth, color = pColor, window = 4 });

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

        private void Terrain()
        {
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++) _h[i, j] = BaseHeight(Cx(i), Cx(j));
            foreach (System.Action lStage in _zoneStage) lStage();
            CarveWadi();
            CarveSea();
            foreach (RoadDef lRoad in _roads) GradeRoad(lRoad);
            foreach (System.Action lStage in _lotStage) lStage();
            foreach (System.Action lStage in _carveStage) lStage();
            Colours();
            foreach (System.Action lStage in _paintStage) lStage();

            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    K.Height(new Rect(ORIGIN + i * CELL, ORIGIN + j * CELL, CELL, CELL), _h[i, j], _paint[i, j] ?? _c[i, j]);

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

        private float _wadiLength;
        private float[] _wadiCumul;

        /// <summary>Where along the wadi (0 = north end, 1 = mouth) a point is.</summary>
        private float WadiProgress(int pSegment, float pT)
        {
            if (_wadiCumul == null)
            {
                _wadiCumul = new float[WADI.Length];
                for (int s = 1; s < WADI.Length; s++) _wadiCumul[s] = _wadiCumul[s - 1] + Vector2.Distance(WADI[s - 1], WADI[s]);
                _wadiLength = _wadiCumul[WADI.Length - 1];
            }
            return (_wadiCumul[pSegment] + pT * Vector2.Distance(WADI[pSegment], WADI[pSegment + 1])) / _wadiLength;
        }

        /// <summary>Wadi bed height along its course (it falls from the dunes to just above the sea).</summary>
        private float WadiBed(float pProgress) => Mathf.Lerp(5f, -1.2f, pProgress);

        private void CarveWadi()
        {
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    float x = Cx(i), z = Cx(j);
                    (float d, int lSeg, float t) = Nearest(WADI, new Vector2(x, z));
                    if (d > 26f) continue;
                    float lBed = Mathf.Min(WadiBed(WadiProgress(lSeg, t)), _h[i, j] - 3.2f);
                    if (d < 7f) { _h[i, j] = lBed; _f[i, j] |= WET; }
                    else if (d < 11f) { _h[i, j] = lBed + (d - 7f) * 0.12f; _f[i, j] |= BANK; }
                    else _h[i, j] = Mathf.Lerp(lBed + 0.5f, _h[i, j], Mathf.SmoothStep(0f, 1f, (d - 11f) / 15f));
                }
        }

        /// <summary>The south coast line (z) of the natural beaches, per x.</summary>
        private static float BeachLine(float x) => x < 175f ? -350f + 6f * Mathf.Sin(x / 40f) : -356f + 5f * Mathf.Sin(x / 35f);

        /// <summary>The west coast line (x) of the rocky shore north and south of the old harbour.</summary>
        private static float WestLine(float z) => -452f + 5f * Mathf.Sin(z / 30f);

        private void CarveSea()
        {
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    float x = Cx(i), z = Cx(j);
                    float h = _h[i, j];
                    bool lSea = false;
                    // Vertical quays: the docks (south and the north-west corner), the marina, the old harbour.
                    if (x < -124f && z < -362f) { h = SEA_FLOOR; lSea = true; }
                    else if (x >= 176f && x < 352f && z < -342f) { h = SEA_FLOOR; lSea = true; }
                    else if (x < -455f && z > -170f && z < 345f && z > 36f && z < 314f) { h = SEA_FLOOR; lSea = true; }
                    else if (x < -455f && z > -170f && z < 345f)
                    {
                        float t = (WestLine(z) + 8f - x) / 22f;
                        if (t > 0f) { h = Mathf.Lerp(Mathf.Min(h, YARD), SEA_FLOOR, Mathf.SmoothStep(0f, 1f, t)); lSea = t > 0.35f; }
                    }
                    else if (x >= -124f && (x < 176f || x >= 352f))
                    {
                        // Natural beaches: a sand slope into the sea.
                        float t = (BeachLine(x) + 16f - z) / 40f;
                        if (t > 0f) { h = Mathf.Min(h, Mathf.Lerp(YARD, SEA_FLOOR, Mathf.SmoothStep(0f, 1f, t))); lSea = h < SEA - 0.2f; _f[i, j] |= BARE; }
                    }
                    _h[i, j] = h;
                    if (lSea) _f[i, j] |= SEAF | WET;
                }
        }

        private void GradeRoad(RoadDef pRoad)
        {
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
                if (float.IsNaN(lHeights[a])) ha = hb;
                for (int m = a + 1; m < b; m++) lHeights[m] = Mathf.Lerp(ha, hb, (float)(m - a) / Mathf.Max(1, b - a));
                if (float.IsNaN(lHeights[a])) lHeights[a] = ha;
                if (float.IsNaN(lHeights[b])) lHeights[b] = hb;
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
                    _paint[i, j] = lDist > lHalf - 0.5f && pRoad.color == ASPHALT ? PAVE : pRoad.color;
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
                    Color c = Color.Lerp(SCRUB, SAND, Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x / 90f + 1f, z / 90f + 2f) - 0.3f) * 2f));
                    c = Color.Lerp(c, Color.Lerp(DUNE, SAND, n * 0.6f), Desert(x, z));
                    if (s > 1.5f) c = Color.Lerp(c, ROCK, Mathf.Clamp01((s - 1.5f) / 1.5f));
                    if (s > 3f) { c = Color.Lerp(ROCK, ROCK * 0.82f, n); _f[i, j] |= CLIFF; }
                    if (h > 16f && s > 1f) c = Color.Lerp(c, ROCK * 0.92f, 0.6f);
                    if ((_f[i, j] & BANK) != 0) c = Color.Lerp(BED, SAND, n * 0.6f);
                    if ((_f[i, j] & WET) != 0 && (_f[i, j] & SEAF) == 0) c = Color.Lerp(BED, BED * 0.88f, n);
                    if (h < 1.3f && h > SEA - 0.8f && (_f[i, j] & BARE) != 0) c = Color.Lerp(BEACH, BEACH * 0.9f, n);
                    if (h <= SEA - 0.8f) c = Color.Lerp(SEABED, BEACH * 0.8f, Mathf.Clamp01((h - SEA_FLOOR) / 4f));
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

        private T Pick<T>(T[] pArray) => pArray[K.Rng.Next(pArray.Length)];

        private GameObject P(string pName, float pX, float pZ, float pYaw, Color? pTint = null, bool pCover = false, MapKit.Collide pCollide = MapKit.Collide.Box, float pUp = 0f)
            => K.Prop(pName, G(pX, pZ, pUp), pYaw, pTint, pCollide, pCover);

        private GameObject P(string pName, Vector3 pAt, float pYaw, Color? pTint = null, bool pCover = false, MapKit.Collide pCollide = MapKit.Collide.Box)
            => K.Prop(pName, pAt, pYaw, pTint, pCollide, pCover);

        private GameObject Nat(string pName, float pX, float pZ, float pScale, float pYaw = -1f, Color? pTint = null, MapKit.Collide pCollide = MapKit.Collide.Box, float pSink = 0f)
        {
            Color lTint = GameArt.Vary(pTint ?? MapKit.DefaultTint("Nature", pName), 0.1f, K.Rng);
            return K.NaturePiece(pName, G(pX, pZ, -pSink), pYaw < 0f ? Rand(0f, 360f) : pYaw, pScale, lTint, pCollide);
        }

        private GameObject Nat(string pName, Vector3 pAt, float pScale, float pYaw = -1f, Color? pTint = null, MapKit.Collide pCollide = MapKit.Collide.Box)
        {
            Color lTint = GameArt.Vary(pTint ?? MapKit.DefaultTint("Nature", pName), 0.1f, K.Rng);
            return K.NaturePiece(pName, pAt, pYaw < 0f ? Rand(0f, 360f) : pYaw, pScale, lTint, pCollide);
        }

        /// <summary>A date palm (one of the two kit palms) on the ground.</summary>
        private void Palm(float pX, float pZ, float pScale = 1f) => Nat(K.Rng.NextDouble() < 0.5 ? "PalmA" : "PalmB", pX, pZ, pScale * Rand(0.85f, 1.15f), -1f, null, MapKit.Collide.Box, 0.1f);

        private MapKit.KitSpec Spec(string pName) => _specs[pName];

        private MapKit.KitSpec Register(MapKit.KitSpec pSpec)
        {
            _specs[pSpec.name] = pSpec;
            return pSpec;
        }

        /// <summary>A building lot: the kit spec (navigable), with its ground pad queued for the terrain stage (NaN ground: follow the land).</summary>
        private MapKit.KitSpec Lot(string pName, string pStyle, float pX, float pZ, float pYaw, int pW, int pD, int pFloors, MapKit.Roof pRoof, Color pWall, Color pRoof2, int pZone, float pGround, float pFeather = 6f)
        {
            MapKit.KitSpec lSpec = new MapKit.KitSpec
            {
                name = pName, style = pStyle, origin = new Vector3(pX, pGround + 0.05f, pZ), yaw = pYaw, width = pW, depth = pD, floors = pFloors,
                roof = pRoof, wall = pWall, roofTint = pRoof2, zone = pZone, seed = Mathf.Abs(pName.GetHashCode() % 100000) + 1, navigable = true
            };
            Rect lFoot = Footprint(pX, pZ, pYaw, pW, pD);
            // A NaN ground follows the land: the pad takes the graded height at the lot's centre.
            _lotStage.Add(() =>
            {
                float lGround = float.IsNaN(pGround) ? _h[Ci(pX), Ci(pZ)] : pGround;
                lSpec.origin.y = lGround + 0.05f;
                Flatten(Expand(lFoot, 2f), lGround, pFeather, BUILT);
            });
            _tame.Add(Expand(lFoot, 3f));
            return Register(lSpec);
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

        private static float FacadeYaw(MapKit.KitBuilding pB, int pSide) => pB.Yaw + SideYaw(pSide);

        private static float RoofY(MapKit.KitSpec pSpec) => pSpec.origin.y + pSpec.floors * MapKit.STOREY;

        /// <summary>Striped awnings over ground-floor modules of a facade (shop fronts).</summary>
        private void Awnings(MapKit.KitBuilding pB, int pSide, Color pTint, params int[] pModules)
        {
            foreach (int m in pModules) K.Piece(pB.spec.style, "Awning", Facade(pB, pSide, m, 2.15f, 0f), FacadeYaw(pB, pSide), pTint, pB.root, MapKit.Collide.None);
        }

        /// <summary>Removes the roof parapet module nearest a world point (a plank, a bridge or a stair lands there).</summary>
        private static void OpenParapet(MapKit.KitBuilding pB, Vector3 pAt)
        {
            Transform lBest = null; float lBestDistance = 1.7f;
            foreach (Transform lChild in pB.root)
            {
                if (lChild.name != "Parapet" || Mathf.Abs(lChild.position.y - pAt.y) > 1.5f) continue;
                float d = Vector2.Distance(new Vector2(lChild.position.x, lChild.position.z), new Vector2(pAt.x, pAt.z));
                if (d < lBestDistance) { lBestDistance = d; lBest = lChild; }
            }
            if (lBest != null) Object.DestroyImmediate(lBest.gameObject);
        }

        /// <summary>A ladder (kit pieces stacked) with its climbable volume; it faces <paramref name="pYaw"/>.</summary>
        private void Ladder(Vector3 pBottom, float pHeight, float pYaw, string pStyle = "Stucco")
        {
            Vector3 lFront = Quaternion.Euler(0, pYaw, 0) * Vector3.forward;
            for (float y = 0f; y < pHeight - 0.5f; y += 3.9f) K.Piece(pStyle, "Ladder", pBottom + Vector3.up * y, pYaw, null, K.Env, MapKit.Collide.None);
            GameObject lClimb = K.Climbable(K.Env, pBottom + Vector3.up * (pHeight * 0.5f + 0.3f) - lFront * 0.05f, new Vector3(0.8f, pHeight + 0.6f, 0.3f), Quaternion.LookRotation(-lFront), Color.white, false);
            lClimb.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>A row of kit pieces along a line, one every <paramref name="pStep"/> metres.</summary>
        private void Row(string pStyle, string pPiece, Vector3 pFrom, Vector3 pTo, float pStep, float pYawOffset, Color? pTint = null, MapKit.Collide pCollide = MapKit.Collide.Mesh, Transform pParent = null)
        {
            Vector3 lDir = pTo - pFrom;
            float lLength = new Vector2(lDir.x, lDir.z).magnitude;
            int lCount = Mathf.Max(1, Mathf.RoundToInt(lLength / pStep));
            float lYaw = Quaternion.LookRotation(new Vector3(lDir.x, 0, lDir.z)).eulerAngles.y + pYawOffset;
            for (int i = 0; i < lCount; i++) K.Piece(pStyle, pPiece, Vector3.Lerp(pFrom, pTo, (i + 0.5f) / lCount), lYaw, pTint, pParent ?? K.Env, pCollide);
        }

        /// <summary>A plank deck (pier, platform, roof bridge) with plank stripes; its top is at <paramref name="pTop"/>.</summary>
        private void Deck(Vector3 pTop, Vector2 pSize, float pYaw, float pThickness = 0.3f, Color? pColor = null, float pStripe = 1.2f)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Color lColor = pColor ?? PLANK;
            K.Block(K.Env, "Deck", pTop - Vector3.up * pThickness * 0.5f, new Vector3(pSize.x, pThickness, pSize.y), lColor, GameLayers.DEFAULT, true, lRot);
            if (pStripe <= 0f) return;
            for (float z = -pSize.y * 0.5f + pStripe * 0.5f; z < pSize.y * 0.5f; z += pStripe)
                K.Block(K.Env, "Plank", pTop + lRot * new Vector3(0, 0.005f, z), new Vector3(pSize.x - 0.1f, 0.02f, 0.06f), lColor * 0.8f, GameLayers.DEFAULT, false, lRot);
        }

        /// <summary>A bridge: deck, kit balustrades (or railings) on both sides, piers below.</summary>
        private void Span(Vector3 pFrom, Vector3 pTo, float pWidth, bool pStone, float pPierDepth = 4.5f, string pStyle = "Classic")
        {
            Vector3 lDir = pTo - pFrom;
            Vector3 lFlat = new Vector3(lDir.x, 0, lDir.z);
            Quaternion lRot = Quaternion.LookRotation(lFlat);
            Color lColor = pStone ? STONE : PLANK;
            K.Block(K.Env, "BridgeDeck", (pFrom + pTo) * 0.5f - Vector3.up * 0.3f, new Vector3(pWidth, 0.6f, lFlat.magnitude + 0.4f), lColor, GameLayers.DEFAULT, true, lRot);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 lSide = lRot * Vector3.right * (s * (pWidth * 0.5f - 0.2f));
                Row(pStyle, pStone ? "Balustrade" : "Railing", pFrom + lSide, pTo + lSide, 3f, 90f * s, pStone ? STONE : (Color?)null);
            }
            int lPiers = Mathf.Max(1, Mathf.RoundToInt(lFlat.magnitude / 10f));
            for (int i = 1; i < lPiers; i++)
            {
                Vector3 p = Vector3.Lerp(pFrom, pTo, (float)i / lPiers);
                K.Block(K.Env, "Pier", p - Vector3.up * (0.6f + pPierDepth * 0.5f), new Vector3(pWidth * 0.8f, pPierDepth, 1.8f), STONE * 0.88f, GameLayers.DEFAULT, true, lRot);
            }
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

        /// <summary>A patrol car on a road loop: dense waypoints that follow the ground, driving on the right.</summary>
        private void Cruiser(float pSpeed, float pOffset, Color pColor, params Vector2[] pPoints)
        {
            Physics.SyncTransforms();
            List<Vector3> lRoute = new List<Vector3>();
            for (int s = 0; s < pPoints.Length; s++)
            {
                Vector2 a = pPoints[s], b = pPoints[(s + 1) % pPoints.Length];
                Vector2 lRight = new Vector2(b.y - a.y, -(b.x - a.x)).normalized * 2.4f;
                int lSteps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 8f));
                for (int k = 0; k < lSteps; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)k / lSteps) + lRight;
                    lRoute.Add(new Vector3(p.x, RoadTop(p.x, p.y) + 0.05f, p.y));
                }
            }
            K.PatrolCar(pSpeed, pOffset, pColor, lRoute.ToArray());
        }

        /// <summary>The top of whatever a car drives on there: a bridge deck if one spans the point, else the ground.</summary>
        private float RoadTop(float pX, float pZ)
        {
            float lTop = Surf(pX, pZ);
            foreach (RaycastHit lHit in Physics.RaycastAll(new Vector3(pX, lTop + 3f, pZ), Vector3.down, 12f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                if (lHit.collider.name == "BridgeDeck") lTop = Mathf.Max(lTop, lHit.point.y);
            return lTop;
        }

        private void Clutter(Vector3 pAt, float pYaw, int pSeed)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            switch (pSeed % 5)
            {
                case 0: K.Crate(pAt); K.Crate(pAt + r * new Vector3(1.15f, 0, 0.1f)); K.Crate(pAt + r * new Vector3(0.5f, 1.05f, 0)); break;
                case 1: K.Prop("Pallet", pAt, pYaw); K.Crate(pAt + Vector3.up * 0.15f, 1.6f); K.Barrel(pAt + r * new Vector3(1.6f, 0, 0), false); break;
                case 2: K.Barrel(pAt, false); K.Barrel(pAt + r * new Vector3(0.7f, 0, 0.2f), false); K.Barrel(pAt + r * new Vector3(0.3f, 0, 0.8f), false); break;
                case 3: Sacks(pAt, pYaw, 4); K.Crate(pAt + r * new Vector3(-1.3f, 0, 0)); break;
                default: K.Prop("TrashBin", pAt, pYaw, null, MapKit.Collide.Box); K.Prop("Pallet", pAt + r * new Vector3(1.4f, 0, 0), pYaw + 90f); break;
            }
        }

        private Mesh _sack, _pot, _dome, _cylinder, _cone;
        private Mesh SackMesh => _sack != null ? _sack : _sack = EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0.12f, 5), "Gulf_Sack");
        /// <summary>A clay jar: a round belly, a narrow neck and a lip (unit height).</summary>
        private Mesh PotMesh => _pot != null ? _pot : _pot = EditorAssets.SaveMesh(LowPolyMesh.Combine(new List<(Mesh, Matrix4x4, Color)>
        {
            (LowPolyMesh.Icosphere(1), Matrix4x4.TRS(new Vector3(0, -0.12f, 0), Quaternion.identity, new Vector3(0.9f, 0.72f, 0.9f)), Color.white),
            (LowPolyMesh.Cylinder(8, 0.8f), Matrix4x4.TRS(new Vector3(0, 0.32f, 0), Quaternion.identity, new Vector3(0.34f, 0.3f, 0.34f)), Color.white),
            (LowPolyMesh.Cylinder(8), Matrix4x4.TRS(new Vector3(0, 0.47f, 0), Quaternion.identity, new Vector3(0.44f, 0.06f, 0.44f)), Color.white)
        }), "Gulf_Jar");
        private Mesh DomeMesh => _dome != null ? _dome : _dome = EditorAssets.SaveMesh(LowPolyMesh.Icosphere(2), "Gulf_Dome");
        private Mesh CylMesh => _cylinder != null ? _cylinder : _cylinder = EditorAssets.SaveMesh(LowPolyMesh.Cylinder(16), "Gulf_Cyl16");
        private Mesh ConeMesh => _cone != null ? _cone : _cone = EditorAssets.SaveMesh(LowPolyMesh.Cone(12), "Gulf_Cone12");

        /// <summary>Burlap sacks of spice and grain, slumped on the ground.</summary>
        private void Sacks(Vector3 pAt, float pYaw, int pCount)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            Color[] lTints = { new Color(0.72f, 0.6f, 0.42f), new Color(0.66f, 0.55f, 0.38f), new Color(0.78f, 0.68f, 0.5f) };
            for (int i = 0; i < pCount; i++)
            {
                Vector3 p = pAt + r * new Vector3((i % 3) * 0.75f, (i / 3) * 0.45f, (i % 2) * 0.2f);
                GameObject lSack = K.Mesh(K.Props, "Sack", SackMesh, p + Vector3.up * 0.28f, new Vector3(0.75f, 0.55f, 0.6f), lTints[i % 3], r);
                lSack.isStatic = true;
            }
        }

        /// <summary>Clay jars and pots.</summary>
        private void Pots(Vector3 pAt, int pCount, float pYaw = 0f)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            for (int i = 0; i < pCount; i++)
            {
                float lSize = Rand(0.45f, 0.8f);
                GameObject lPot = K.Mesh(K.Props, "Pot", PotMesh, pAt + r * new Vector3(i * 0.7f, lSize * 0.5f, (i % 2) * 0.4f), new Vector3(lSize * 0.8f, lSize, lSize * 0.8f), GameArt.Vary(new Color(0.72f, 0.42f, 0.28f), 0.12f, K.Rng));
                lPot.isStatic = true;
            }
        }

        /// <summary>A parasol (or an umbrella table): no box over the canopy, only the pole and the table collide.</summary>
        private void Shade(Vector3 pAt, float pYaw, Color pTint, bool pTable)
        {
            GameObject lShade = K.Prop(pTable ? "UmbrellaTable" : "Parasol", pAt, pYaw, pTint, MapKit.Collide.None);
            if (lShade == null) return;
            BoxCollider lBox = lShade.AddComponent<BoxCollider>();
            lBox.center = new Vector3(0, pTable ? 0.4f : 1.1f, 0);
            lBox.size = pTable ? new Vector3(1.1f, 0.8f, 1.1f) : new Vector3(0.12f, 2.2f, 0.12f);
        }

        /// <summary>
        /// Kit room presets sometimes drop an activity node between stools or chairs: every node whose
        /// standing capsule is blocked moves to the nearest free floor spot within 1.6 m.
        /// </summary>
        private void NudgeActivities()
        {
            Physics.SyncTransforms();
            int lMoved = 0;
            bool Free(Vector3 p) => !Physics.CheckCapsule(p + Vector3.up * 0.55f, p + Vector3.up * 1.5f, 0.32f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            foreach (ActivityNode lNode in K.Markers.GetComponentsInChildren<ActivityNode>())
            {
                Vector3 p = lNode.transform.position;
                if (Free(p)) continue;
                bool lDone = false;
                for (float d = 0.6f; d <= 1.61f && !lDone; d += 0.5f)
                    for (int a = 0; a < 8 && !lDone; a++)
                    {
                        Vector3 c = p + Quaternion.Euler(0, a * 45f, 0) * Vector3.forward * d;
                        float lFloor = Surf(c.x, c.z);
                        if (Physics.Raycast(c + Vector3.up * 1.2f, Vector3.down, out RaycastHit lHit, 2.2f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) lFloor = lHit.point.y;
                        else if (Mathf.Abs(lFloor - p.y) > 0.5f) continue;
                        c.y = lFloor + 0.1f;
                        if (Mathf.Abs(c.y - p.y) > 0.5f || !Free(c)) continue;
                        lNode.transform.position = c;
                        lDone = true;
                        lMoved++;
                    }
            }
            Debug.Log($"[Build] Map_Gulf: {lMoved} activity nodes moved off furniture");
        }

        /// <summary>A hanging lantern: a glowing bulb with a short warm light (no shadows).</summary>
        private void Lantern(Vector3 pAt, float pRange = 7f) => K.Lamp(pAt, LANTERN, pRange, 1.3f, false);

        /// <summary>A shipping container lying along <paramref name="pYaw"/> (cover), with a ladder on its end when asked.</summary>
        private void Container(Vector3 pAt, float pYaw, Color pTint, bool pLadder = false, bool pCover = true)
        {
            K.Prop("ShippingContainer", pAt, pYaw + 90f, pTint, MapKit.Collide.Box, pCover);
            if (pLadder) Ladder(pAt + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0, -3.25f), 2.7f, pYaw + 180f);
        }

        // ================================================================ sea, edges, vegetation, match points

        private void Seas()
        {
            // Two big planes reaching the horizon: the Gulf to the south and to the west (hidden under the land).
            K.Water(new Vector3(0, SEA, -1160), new Vector2(4000, 1660));
            K.Water(new Vector3(-1220, SEA, 835), new Vector2(1560, 2330));
            // Desert beyond the land edges (no collider): the plateau behind the ridge, the coastal plain east.
            K.Block(K.Env, "DesertSkirt", new Vector3(0, 13f, 1260f), new Vector3(3000f, 4f, 1500f), DUNE * 0.95f, GameLayers.DEFAULT, false);
            K.Block(K.Env, "DesertSkirt", new Vector3(1260f, 13f, 700f), new Vector3(1500f, 4f, 1600f), DUNE * 0.95f, GameLayers.DEFAULT, false);
            K.Block(K.Env, "PlainSkirt", new Vector3(1260f, YARD - 2.2f, -210f), new Vector3(1500f, 4f, 240f), SAND * 0.95f, GameLayers.DEFAULT, false);
        }

        /// <summary>Invisible walls on the map edges.</summary>
        private void MapEdges()
        {
            for (int s = 0; s < 4; s++)
            {
                Vector3 lPos = s switch { 0 => new Vector3(0, 40, -498), 1 => new Vector3(498, 40, 0), 2 => new Vector3(0, 40, 498), _ => new Vector3(-498, 40, 0) };
                Vector3 lSize = s % 2 == 0 ? new Vector3(1000, 140, 2) : new Vector3(2, 140, 1000);
                K.Block(K.Env, "MapEdge", lPos, lSize, Color.white).GetComponent<MeshRenderer>().enabled = false;
            }
        }

        private bool Tame(float pX, float pZ)
        {
            foreach (Rect r in _tame) if (r.Contains(new Vector2(pX, pZ))) return true;
            return false;
        }

        private bool Blocked(int i, int j)
        {
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    byte f = _f[Mathf.Clamp(i + di, 0, N - 1), Mathf.Clamp(j + dj, 0, N - 1)];
                    if ((f & (ROAD | BUILT)) != 0) return true;
                    if (di == 0 && dj == 0 && (f & (WET | BARE)) != 0) return true;
                }
            return false;
        }

        /// <summary>Desert scrub, rocks and lone palms in clusters; cliff chunks on the ridge faces; dry growth in the wadi.</summary>
        private void Vegetation()
        {
            Color lDry = new Color(0.55f, 0.56f, 0.32f), lOlive = new Color(0.45f, 0.52f, 0.3f);
            int lBushes = 0, lRocks = 0, lPalms = 0, lCliffs = 0;
            const float STEP = 7f;
            for (float gx = -494f; gx < 494f; gx += STEP)
                for (float gz = -494f; gz < 494f; gz += STEP)
                {
                    float x = gx + Rand(-4f, 4f), z = gz + Rand(-4f, 4f);
                    int i = Ci(x), j = Ci(z);
                    if (Blocked(i, j) || Tame(x, z)) continue;
                    float s = Slope(i, j);
                    if (s > 3.2f) continue;
                    float lCluster = Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x / 45f + 4f, z / 45f + 1f) - 0.45f) * 3f);
                    float lWild = Mathf.Lerp(0.25f, 1f, Mathf.Max(Desert(x, z), Mathf.Clamp01(1f - LandBorder(x, z) / 120f)));
                    float lRoll = (float)K.Rng.NextDouble();
                    if (lRoll < lCluster * lWild * 0.62f)
                    {
                        string lPiece = Pick(new[] { "BushA", "BushC", "BushB", "BushC" });
                        Nat(lPiece, x, z, Rand(0.7f, 1.3f), -1f, Color.Lerp(lDry, lOlive, (float)K.Rng.NextDouble()), MapKit.Collide.None, 0.15f);
                        lBushes++;
                        if (K.Rng.NextDouble() < 0.3) { Nat("BushC", x + Rand(-2f, 2f), z + Rand(-2f, 2f), Rand(0.5f, 0.9f), -1f, lDry, MapKit.Collide.None, 0.1f); lBushes++; }
                    }
                    else if (lRoll > 0.955f)
                    {
                        Nat(Pick(new[] { "RockB", "RockC", "RockD", "RockB" }), x, z, Rand(0.6f, 1.4f), -1f, GameArt.Vary(ROCK, 0.08f, K.Rng), MapKit.Collide.Box, 0.3f);
                        lRocks++;
                    }
                    else if (lRoll > 0.94f && Desert(x, z) < 0.6f)
                    {
                        if (K.Rng.NextDouble() < 0.5) { Palm(x, z); lPalms++; }
                        else { Nat("DeadTree", x, z, Rand(0.35f, 0.55f), -1f, new Color(0.55f, 0.45f, 0.35f)); }
                    }
                }
            // Ridge faces dressed with sandstone chunks; tamarisk scrub and palms along the wadi.
            for (int i = 1; i < N - 1; i++)
                for (int j = 1; j < N - 1; j++)
                {
                    float x = Cx(i), z = Cx(j);
                    if ((_f[i, j] & (WET | ROAD | BUILT)) != 0) continue;
                    float s = Slope(i, j);
                    if (s > 3.2f && LandBorder(x, z) < 90f && K.Rng.NextDouble() < 0.12)
                    {
                        float lLow = Mathf.Min(Mathf.Min(_h[i - 1, j], _h[i + 1, j]), Mathf.Min(_h[i, j - 1], _h[i, j + 1]));
                        K.NaturePiece("CliffChunk", new Vector3(x + Rand(-1f, 1f), Mathf.Lerp(lLow, _h[i, j], 0.35f), z + Rand(-1f, 1f)), Rand(0, 360), Rand(0.7f, 1.3f), GameArt.Vary(ROCK, 0.07f, K.Rng), MapKit.Collide.None);
                        lCliffs++;
                    }
                    if ((_f[i, j] & BANK) != 0 && !Tame(x, z) && K.Rng.NextDouble() < 0.16)
                    {
                        if (K.Rng.NextDouble() < 0.3) { Palm(x + Rand(-1f, 1f), z + Rand(-1f, 1f), 0.9f); lPalms++; }
                        else Nat(K.Rng.NextDouble() < 0.5 ? "ReedsClump" : "BushB", x + Rand(-1.5f, 1.5f), z + Rand(-1.5f, 1.5f), Rand(0.8f, 1.3f), -1f, lDry, MapKit.Collide.None);
                    }
                    if ((_f[i, j] & WET) != 0 && (_f[i, j] & SEAF) == 0 && K.Rng.NextDouble() < 0.06)
                        Nat(Pick(new[] { "RockA", "RockB", "RockC" }), x, z, Rand(0.6f, 1.2f), -1f, GameArt.Vary(ROCK, 0.1f, K.Rng), MapKit.Collide.Box, 0.2f);
                }
            Debug.Log($"[Build] Map_Gulf vegetation: {lBushes} bushes, {lRocks} rocks, {lPalms} palms, {lCliffs} cliff chunks");
        }

        private void MatchPoints()
        {
            K.Point(MatchPoint.Kind.Start, G(-300, 470, 0.2f), 180, "North caravan road");
            K.Point(MatchPoint.Kind.Start, G(170, 472, 0.2f), 180, "Desert highway");
            K.Point(MatchPoint.Kind.Start, G(476, -118, 0.2f), 270, "Gulf road east");
            K.Point(MatchPoint.Kind.Start, G(-486, -300, 0.2f), 90, "Docks west gate");
        }
    }
}
