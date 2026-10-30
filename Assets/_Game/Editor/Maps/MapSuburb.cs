using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <b>Elmridge</b> - an American suburb of 1 km² at dusk. Wooded rises close the west, the
    /// north and the south; the Ridgeway expressway on its embankment closes the east.
    /// <list type="bullet">
    /// <item><b>Downtown Strip</b> (zone 0, south-west): Main Street and Ash Avenue with the diner,
    /// the Ridgeview Mall (atrium, rooftop car park), the police station, the Starlight cinema and
    /// its roof, the gas station, the bank, shops with flats above, back alleys with fire escapes,
    /// Malloy Motors, the water tower, the railway, its level crossing and the depot.</item>
    /// <item><b>Junkyard Bowl</b> (zone 1, south-east): the junkyard (lanes of stacked wrecks,
    /// vantage piles, the magnet crane, the crusher, the lookout tower), the Lucky Ten bowling
    /// alley taken over as the bikers' club house, the tall-grass field, Granny Voss's farmhouse,
    /// the drainage canal with its culverts and the walkable banks under its bridges.</item>
    /// <item><b>Hilltop Mansion</b> (zone 2, north-west): the Whitlock estate on a walled plateau
    /// with its terrace wall, gatehouse, formal gardens, fountain, hedge maze, pool, greenhouse,
    /// tennis court, garage, guest cottage, staff quarters and helipad around a three-floor
    /// colonial mansion with a basement.</item>
    /// </list>
    /// Between them: streets of houses with yards, Juniper Park and its pond, the high school,
    /// the meeting hall, the Briarwood country club on the golf course, the creek, the woods.
    /// This file holds the terrain (a height function, the carved canal, creek and pond, graded
    /// roads) and the shared dressing helpers; each district has its own partial.
    /// </summary>
    public partial class MapSuburb : MapBuilderBase
    {
        protected override string MapFile => "Map_Suburb";

        [MenuItem("Goobyr/Build/Map Suburb")]
        public static void BuildMenu() => new MapSuburb().Build();

        // ---------------------------------------------------------------- palette
        private static readonly Color GRASS = new Color(0.46f, 0.66f, 0.34f);
        private static readonly Color LAWN = new Color(0.42f, 0.7f, 0.32f);
        private static readonly Color MEADOW = new Color(0.58f, 0.66f, 0.36f);
        private static readonly Color WOODS = new Color(0.34f, 0.5f, 0.28f);
        private static readonly Color DIRT = new Color(0.55f, 0.46f, 0.34f);
        private static readonly Color GRAVEL = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color ASPHALT = new Color(0.32f, 0.32f, 0.35f);
        private static readonly Color PAVE = new Color(0.74f, 0.72f, 0.68f);
        private static readonly Color CONCRETE = new Color(0.66f, 0.65f, 0.62f);
        private static readonly Color STONE = new Color(0.86f, 0.82f, 0.74f);
        private static readonly Color PLANK = new Color(0.58f, 0.43f, 0.3f);
        private static readonly Color MUD = new Color(0.42f, 0.38f, 0.3f);
        private static readonly Color WARM = new Color(1f, 0.8f, 0.55f);
        private static readonly Color SODIUM = new Color(1f, 0.68f, 0.38f);

        private static readonly Color[] HOUSE_WALLS =
        {
            new Color(0.96f, 0.94f, 0.88f), new Color(0.72f, 0.82f, 0.9f), new Color(0.95f, 0.85f, 0.68f), new Color(0.8f, 0.88f, 0.76f),
            new Color(0.92f, 0.78f, 0.72f), new Color(0.88f, 0.86f, 0.8f), new Color(0.98f, 0.92f, 0.78f), new Color(0.76f, 0.74f, 0.84f)
        };
        private static readonly Color[] HOUSE_ROOFS =
        {
            new Color(0.32f, 0.33f, 0.38f), new Color(0.48f, 0.26f, 0.22f), new Color(0.28f, 0.36f, 0.32f), new Color(0.4f, 0.32f, 0.26f), new Color(0.22f, 0.24f, 0.3f)
        };
        private static readonly Color[] CAR_TINTS =
        {
            new Color(0.75f, 0.12f, 0.12f), new Color(0.15f, 0.3f, 0.6f), new Color(0.9f, 0.9f, 0.88f), new Color(0.12f, 0.12f, 0.14f), new Color(0.55f, 0.6f, 0.62f),
            new Color(0.2f, 0.45f, 0.3f), new Color(0.85f, 0.65f, 0.2f), new Color(0.45f, 0.25f, 0.4f)
        };
        private static readonly Color[] AWNINGS =
        {
            new Color(0.75f, 0.18f, 0.16f), new Color(0.15f, 0.42f, 0.38f), new Color(0.2f, 0.3f, 0.55f), new Color(0.85f, 0.6f, 0.18f), new Color(0.45f, 0.22f, 0.4f)
        };

        protected override void Compose()
        {
            K.Grid(N, N, CELL, new Vector2(ORIGIN, ORIGIN), GRASS);
            _glowMaterial = AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ART_RESOURCES + "/M_Glow.mat");
            PlanDowntown();
            PlanJunkyard();
            PlanMansion();
            PlanBetween();
            Terrain();

            Waters();
            Downtown();
            Junkyard();
            Mansion();
            Between();
            Vegetation();
            MatchPoints();
            MapEdges();
            NudgeActivities();
            // Furniture rooms, wreck stacks and hedges make clusters of cover candidates: keep one per 1.6 m.
            K.CoverSpacing = 1.6f;
            // The forest's thresholds, a little more generous for the dense downtown seen from the hill.
            K.DistanceCulling(0.04f, 0.055f, 0.035f, 0.03f);
            Debug.Log($"[Build] Map_Suburb: {K.Root.GetComponentsInChildren<Transform>(true).Length} objects");
        }

        // ================================================================ terrain

        private const int N = 250;
        private const float CELL = 4f, ORIGIN = -500f;
        /// <summary>The estate plateau, the foot of its south terrace wall, the expressway embankment.</summary>
        private const float HILL = 18f, TERRACE_FOOT = 12f, HWY = 5.5f;
        private static readonly Rect PLATEAU = Rect.MinMaxRect(-372, 248, -140, 432);

        /// <summary>Street lines: Main Street, Ash Avenue, Birch Street, Back Street, Mill Road, Maple Street, Willow Road, Oak Lane, Cedar Street, the railway.</summary>
        private const float MAIN_Z = -230f, ASH_X = -230f, BIRCH_X = -80f, BACK_Z = -130f, MILL_X = -420f, MAPLE_Z = -60f, WILLOW_Z = 45f, OAK_Z = 150f, CEDAR_X = 170f, RAIL_Z = -372f;
        /// <summary>The drainage canal: a concrete channel along x = 300 from its head (z = 60) to the south edge.</summary>
        private const float CANAL_X = 300f, CANAL_HEAD = 60f, CANAL_BED = -4f, CANAL_SHELF = -3f, CANAL_WATER = -3.55f;
        private static readonly Vector2 POND = new Vector2(52, 82);
        private const float POND_R = 20f, POND_WATER = -1.1f;

        private const byte WET = 1, ROAD = 2, BUILT = 4, BARE = 8, BANK = 16, CLIFF = 32;

        private readonly float[,] _h = new float[N, N];
        private readonly Color[,] _c = new Color[N, N];
        private readonly Color?[,] _paint = new Color?[N, N];
        private readonly byte[,] _f = new byte[N, N];
        private float[,] _corner;
        private Material _glowMaterial;

        private readonly List<System.Action> _zoneStage = new List<System.Action>();
        private readonly List<System.Action> _lotStage = new List<System.Action>();
        private readonly List<System.Action> _carveStage = new List<System.Action>();
        private readonly List<System.Action> _paintStage = new List<System.Action>();
        private readonly List<RoadDef> _roads = new List<RoadDef>();
        private readonly Dictionary<string, MapKit.KitSpec> _specs = new Dictionary<string, MapKit.KitSpec>();
        /// <summary>Rectangles kept free of wild trees and bushes (lots, yards, plazas, fields).</summary>
        private readonly List<Rect> _tame = new List<Rect>();

        private class RoadDef
        {
            public Vector2[] points;
            public float width;
            public Color color;
            public int window;
        }

        /// <summary>The creek from the north woods down the golf course into the park pond, then from the pond to the canal head.</summary>
        private static readonly Vector2[] CREEK_UP = { new Vector2(96, 510), new Vector2(84, 440), new Vector2(62, 372), new Vector2(50, 300), new Vector2(40, 226), new Vector2(38, 160), new Vector2(46, 106) };
        private static readonly Vector2[] CREEK_DOWN = { new Vector2(70, 70), new Vector2(112, 52), new Vector2(170, 46), new Vector2(232, 52), new Vector2(286, 58), new Vector2(300, 62) };

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

        /// <summary>Distance to the wooded edges (west, north, south; the east is the expressway).</summary>
        private static float Border(float x, float z) => Mathf.Min(x + 500f, Mathf.Min(500f - z, z + 500f));

        private static float BaseHeight(float x, float z)
        {
            // Gently rolling lawns.
            float h = (Mathf.PerlinNoise(x / 120f + 3.1f, z / 120f + 7.3f) - 0.5f) * 2.4f + (Mathf.PerlinNoise(x / 37f + 9.2f, z / 37f + 1.4f) - 0.5f) * 0.6f;
            // The estate hill: a plateau whose south face is a terrace wall (the slope below it starts lower).
            float d = RectDistance(PLATEAU, x, z) + (Mathf.PerlinNoise(x * 0.018f + 5.2f, z * 0.018f + 1.7f) - 0.5f) * 18f;
            float lHigh = HILL * (1f - Mathf.SmoothStep(0f, 1f, d / 90f));
            float lLow = TERRACE_FOOT * (1f - Mathf.SmoothStep(0f, 1f, d / 75f));
            float w = z < PLATEAU.yMin ? Mathf.Clamp01(Mathf.Max((x + 205f) / 25f, (-372f - x) / 20f)) : 1f;
            h += Mathf.Lerp(lLow, lHigh, w);
            // Wooded rises on the west, north and south edges.
            float b = Border(x, z) + (Mathf.PerlinNoise(x * 0.015f + 2.2f, z * 0.015f + 8.8f) - 0.5f) * 30f;
            h += 8f * (1f - Mathf.SmoothStep(0f, 1f, (b - 8f) / 70f)) * (0.7f + Mathf.PerlinNoise(x * 0.03f, z * 0.03f + 4f) * 0.6f);
            // The golf course rolls a little more.
            if (x > -130f && x < 250f && z > 200f) h += (Mathf.PerlinNoise(x / 45f + 1.1f, z / 45f + 6.6f) - 0.5f) * 3f;
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

        private void PaintRect(Rect pRect, Color pColor, byte pFlag = BARE) => _paintStage.Add(() => Cells(pRect, (i, j) => { _paint[i, j] = pColor; _f[i, j] |= pFlag; }));

        private void PaintDisc(Vector2 pCenter, float pRadius, Color pColor, byte pFlag = BARE) => _paintStage.Add(() =>
            Cells(new Rect(pCenter.x - pRadius, pCenter.y - pRadius, pRadius * 2f, pRadius * 2f), (i, j) =>
            {
                if (Vector2.Distance(pCenter, new Vector2(Cx(i), Cx(j))) > pRadius) return;
                _paint[i, j] = pColor;
                _f[i, j] |= pFlag;
            }));

        /// <summary>A graded road: its heights are smoothed along its length; it bridges the canal and the creek at bank level.</summary>
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
            CarveCanal();
            CarveCreek();
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

        /// <summary>
        /// The canal: a flat concrete bed 16 m wide and 4 m deep (x 292..308) under quays; its sloped
        /// edges are covered by the quay blocks built later. Under the south rise it runs in a tunnel.
        /// </summary>
        private void CarveCanal()
        {
            Cells(R(CANAL_X - 12f, -476f, CANAL_X + 12f, CANAL_HEAD), (i, j) =>
            {
                if (Mathf.Abs(Cx(i) - CANAL_X) < 12f) { _h[i, j] = CANAL_BED; _f[i, j] |= WET; }
            });
        }

        private void CarveCreek()
        {
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    float x = Cx(i), z = Cx(j);
                    if ((_f[i, j] & WET) != 0) continue;
                    float d = Mathf.Min(Nearest(CREEK_UP, new Vector2(x, z)).distance, Nearest(CREEK_DOWN, new Vector2(x, z)).distance);
                    float lPond = Vector2.Distance(POND, new Vector2(x, z));
                    if (lPond < POND_R + 10f)
                    {
                        float lPondBed = lPond < POND_R - 4f ? -2.4f : Mathf.Lerp(-2.4f, 0.2f, (lPond - POND_R + 4f) / 14f);
                        if (lPond < POND_R - 2f) _f[i, j] |= WET;
                        else if (lPond < POND_R + 2f) _f[i, j] |= BANK;
                        _h[i, j] = Mathf.Min(_h[i, j], lPondBed);
                        continue;
                    }
                    if (d > 12f) continue;
                    // The creek follows the land: its bed is 1.6 m under the local ground (smoothed over its banks).
                    float lGround = _h[i, j];
                    if (d < 3.5f) { _h[i, j] = Mathf.Min(lGround - 1.6f, 0.2f + lGround - 1.6f); _f[i, j] |= WET; }
                    else if (d < 6f) { _h[i, j] = lGround - 1.0f; _f[i, j] |= BANK; }
                    else _h[i, j] = Mathf.Lerp(lGround - 0.8f, lGround, Mathf.SmoothStep(0f, 1f, (d - 6f) / 6f));
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
                else if ((_f[i, j] & ROAD) == 0) _h[i, j] = Mathf.Lerp(lTarget, _h[i, j], Mathf.SmoothStep(0f, 1f, (lDist - lHalf - 1.5f) / lShoulder));
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
                    float x = Cx(i), z = Cx(j), s = Slope(i, j);
                    float n = Mathf.PerlinNoise(x / 40f + 7f, z / 40f + 3f);
                    Color c = Color.Lerp(GRASS, MEADOW, Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x / 90f + 1f, z / 90f + 2f) - 0.35f) * 2f));
                    c = Color.Lerp(c, WOODS, Mathf.Clamp01(1f - (Border(x, z) - 20f) / 70f));
                    if (s > 1.4f) c = Color.Lerp(c, DIRT, Mathf.Clamp01((s - 1.4f) / 1.6f));
                    if (s > 3f) _f[i, j] |= CLIFF;
                    if ((_f[i, j] & BANK) != 0) c = Color.Lerp(MUD, GRASS, n * 0.5f);
                    if ((_f[i, j] & WET) != 0) c = Mathf.Abs(x - CANAL_X) < 12f && z < CANAL_HEAD + 1f ? CONCRETE * 0.82f : Color.Lerp(MUD, MUD * 0.85f, n);
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

        /// <summary>A broadleaf street or garden tree (one of the three kit broadleaves, or a birch).</summary>
        private void Shade(float pX, float pZ, float pScale = 1f)
        {
            string lPiece = Pick(new[] { "BroadleafA", "BroadleafC", "BroadleafA", "BirchA", "BroadleafB" });
            float s = pScale * Rand(0.75f, 1.05f) * (lPiece == "BroadleafB" ? 0.75f : 1f);
            Nat(lPiece, pX, pZ, s, -1f, null, MapKit.Collide.Box, 0.1f);
        }

        /// <summary>A kit piece in a parent group, stretched (hedges, planters): no cover point is registered.</summary>
        private GameObject Stretch(string pStyle, string pName, Vector3 pAt, float pYaw, Vector3 pScale, Color? pTint, Transform pParent, MapKit.Collide pCollide = MapKit.Collide.Box)
        {
            GameObject lPiece = K.Piece(pStyle, pName, pAt, pYaw, pTint, pParent, pCollide);
            if (lPiece != null) lPiece.transform.localScale = pScale;
            return lPiece;
        }

        /// <summary>A glowing panel (neon, lit sign, marquee bulbs): no collider, no shadow.</summary>
        private GameObject Neon(Transform pParent, Vector3 pCenter, Vector3 pSize, Color pColor, float pYaw = 0f)
        {
            GameObject lPanel = K.Mesh(pParent, "Neon", MapKit.SharedBox, pCenter, pSize, pColor, Quaternion.Euler(0, pYaw, 0), false, _glowMaterial);
            lPanel.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lPanel.isStatic = true;
            return lPanel;
        }

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

        /// <summary>Removes the roof parapet module nearest a world point (a ramp, a plank or a bridge lands there).</summary>
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

        /// <summary>Removes the kit's roof clutter (air handler, crate) inside a world rectangle of a roof.</summary>
        private static void ClearRoof(MapKit.KitBuilding pB, Rect pArea)
        {
            List<GameObject> lDoomed = new List<GameObject>();
            float y = RoofY(pB.spec);
            foreach (Transform lChild in pB.root)
                if ((lChild.name == "AirHandler" || lChild.name == "Crate") && Mathf.Abs(lChild.position.y - y) < 0.5f && pArea.Contains(new Vector2(lChild.position.x, lChild.position.z))) lDoomed.Add(lChild.gameObject);
            foreach (GameObject lObject in lDoomed) Object.DestroyImmediate(lObject);
        }

        /// <summary>
        /// Opens a hole in a building's slab at a floor (a double-height hall, a grand stair): the
        /// slab blocks crossing the local rectangle are replaced by copies trimmed around it.
        /// </summary>
        private static void CutSlab(MapKit.KitBuilding pB, int pFloor, Rect pHole)
        {
            float y = pFloor * MapKit.STOREY - 0.12f;
            List<Transform> lSlabs = new List<Transform>();
            foreach (Transform lChild in pB.root) if (lChild.name == "Slab" && Mathf.Abs(lChild.localPosition.y - y) < 0.05f) lSlabs.Add(lChild);
            foreach (Transform lSlab in lSlabs)
            {
                Vector3 c = lSlab.localPosition, s = lSlab.localScale;
                Rect r = Rect.MinMaxRect(c.x - s.x * 0.5f, c.z - s.z * 0.5f, c.x + s.x * 0.5f, c.z + s.z * 0.5f);
                if (!r.Overlaps(pHole)) continue;
                Rect h = Rect.MinMaxRect(Mathf.Max(r.xMin, pHole.xMin), Mathf.Max(r.yMin, pHole.yMin), Mathf.Min(r.xMax, pHole.xMax), Mathf.Min(r.yMax, pHole.yMax));
                Rect[] lParts =
                {
                    Rect.MinMaxRect(r.xMin, r.yMin, h.xMin, r.yMax), Rect.MinMaxRect(h.xMax, r.yMin, r.xMax, r.yMax),
                    Rect.MinMaxRect(h.xMin, r.yMin, h.xMax, h.yMin), Rect.MinMaxRect(h.xMin, h.yMax, h.xMax, r.yMax)
                };
                foreach (Rect p in lParts)
                {
                    if (p.width < 0.05f || p.height < 0.05f) continue;
                    GameObject lCopy = Object.Instantiate(lSlab.gameObject, pB.root);
                    lCopy.name = "Slab";
                    lCopy.transform.localRotation = Quaternion.identity;
                    lCopy.transform.localPosition = new Vector3(p.center.x, c.y, p.center.y);
                    lCopy.transform.localScale = new Vector3(p.width, s.y, p.height);
                }
                Object.DestroyImmediate(lSlab.gameObject);
            }
        }

        /// <summary>A ladder (kit pieces stacked) with its climbable volume; it faces <paramref name="pYaw"/>.</summary>
        private void Ladder(Vector3 pBottom, float pHeight, float pYaw, string pStyle = "Suburb")
        {
            Vector3 lFront = Quaternion.Euler(0, pYaw, 0) * Vector3.forward;
            for (float y = 0f; y < pHeight - 0.5f; y += 3.9f) K.Piece(pStyle, "Ladder", pBottom + Vector3.up * y, pYaw, null, K.Env, MapKit.Collide.None);
            GameObject lClimb = K.Climbable(K.Env, pBottom + Vector3.up * (pHeight * 0.5f + 0.3f) - lFront * 0.05f, new Vector3(0.8f, pHeight + 0.6f, 0.3f), Quaternion.LookRotation(-lFront), Color.white, false);
            lClimb.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>A climbable trellis of ivy against a wall (the wall is behind it, it faces <paramref name="pYaw"/>).</summary>
        private void Trellis(Vector3 pBottom, float pHeight, float pWidth, float pYaw)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            Vector3 lFront = r * Vector3.forward;
            K.Climbable(K.Env, pBottom + Vector3.up * pHeight * 0.5f + lFront * 0.12f, new Vector3(pWidth, pHeight, 0.15f), Quaternion.LookRotation(-lFront), new Color(0.3f, 0.5f, 0.26f), false);
            for (int i = 0; i < Mathf.RoundToInt(pHeight / 1.2f); i++)
                K.Mesh(K.Env, "IvyLeaves", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0.25f, 31), "Suburb_Ivy"), pBottom + Vector3.up * (0.6f + i * 1.2f) + lFront * 0.3f + r * Vector3.right * Rand(-pWidth * 0.3f, pWidth * 0.3f), new Vector3(pWidth * 0.7f, 1.3f, 0.5f), GameArt.Vary(new Color(0.3f, 0.52f, 0.26f), 0.1f, K.Rng));
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

        /// <summary>A plank deck (porch, platform, catwalk) with plank stripes; its top is at <paramref name="pTop"/>.</summary>
        private void Deck(Vector3 pTop, Vector2 pSize, float pYaw, float pThickness = 0.3f, Color? pColor = null, float pStripe = 1.2f, Transform pParent = null)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Color lColor = pColor ?? PLANK;
            Transform lParent = pParent ?? K.Env;
            K.Block(lParent, "Deck", pTop - Vector3.up * pThickness * 0.5f, new Vector3(pSize.x, pThickness, pSize.y), lColor, GameLayers.DEFAULT, true, lRot);
            if (pStripe <= 0f) return;
            for (float z = -pSize.y * 0.5f + pStripe * 0.5f; z < pSize.y * 0.5f; z += pStripe)
                K.Block(lParent, "Plank", pTop + lRot * new Vector3(0, 0.005f, z), new Vector3(pSize.x - 0.1f, 0.02f, 0.06f), lColor * 0.8f, GameLayers.DEFAULT, false, lRot);
        }

        /// <summary>A bridge: deck, balustrades (or railings) on both sides, piers below.</summary>
        private void Span(Vector3 pFrom, Vector3 pTo, float pWidth, bool pStone, float pPierDepth = 4.5f, string pStyle = "Classic", bool pPiers = true)
        {
            Vector3 lDir = pTo - pFrom;
            Vector3 lFlat = new Vector3(lDir.x, 0, lDir.z);
            Quaternion lRot = Quaternion.LookRotation(lFlat);
            Color lColor = pStone ? CONCRETE : PLANK;
            K.Block(K.Env, "BridgeDeck", (pFrom + pTo) * 0.5f - Vector3.up * 0.3f, new Vector3(pWidth, 0.6f, lFlat.magnitude + 0.4f), lColor, GameLayers.DEFAULT, true, Quaternion.LookRotation(lDir));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 lSide = lRot * Vector3.right * (s * (pWidth * 0.5f - 0.2f));
                Row(pStyle, pStone ? "Balustrade" : "Railing", pFrom + lSide, pTo + lSide, 3f, 90f * s, pStone ? CONCRETE * 1.1f : (Color?)null);
            }
            if (!pPiers) return;
            int lPiers = Mathf.Max(1, Mathf.RoundToInt(lFlat.magnitude / 10f));
            for (int i = 1; i < lPiers; i++)
            {
                Vector3 p = Vector3.Lerp(pFrom, pTo, (float)i / lPiers);
                K.Block(K.Env, "Pier", p - Vector3.up * (0.6f + pPierDepth * 0.5f), new Vector3(pWidth * 0.8f, pPierDepth, 1.2f), CONCRETE * 0.88f, GameLayers.DEFAULT, true, lRot);
            }
        }

        /// <summary>A guard on a post, with a sandbag wall (or a police barrier) in front when asked.</summary>
        private void Guard(int pZone, Vector3 pAt, float pYaw, ItemKind pWeapon, string pCover = null)
        {
            K.Npc(NpcRole.Guard, pAt + Vector3.up * 0.1f, pYaw, pZone, pWeapon);
            if (pCover != null) K.Prop(pCover, pAt + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0, 1.4f), pYaw, null, MapKit.Collide.Box, true);
        }

        private void Guard(int pZone, float pX, float pZ, float pYaw, ItemKind pWeapon, string pCover = null) => Guard(pZone, G(pX, pZ), pYaw, pWeapon, pCover);

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
                case 3: K.Prop("TrashBin", pAt, pYaw, null, MapKit.Collide.Box); K.Prop("TrashBin", pAt + r * new Vector3(0.8f, 0, 0), pYaw, null, MapKit.Collide.Box); K.Crate(pAt + r * new Vector3(-1.3f, 0, 0)); break;
                default: K.Prop("TrashBin", pAt, pYaw, null, MapKit.Collide.Box); K.Prop("Pallet", pAt + r * new Vector3(1.4f, 0, 0), pYaw); break;
            }
        }

        /// <summary>A red gas bottle that explodes when shot (next to a patrol, a grill, a workshop).</summary>
        private void GasBottle(Vector3 pAt)
        {
            GameObject lBottle = K.Prop("GasBottle", pAt, Rand(0f, 360f), new Color(0.85f, 0.15f, 0.12f), MapKit.Collide.Box);
            if (lBottle == null) { K.Barrel(pAt, true); return; }
            lBottle.isStatic = false;
            lBottle.layer = GameLayers.INTERACTABLE;
            lBottle.AddComponent<ExplosiveProp>();
        }

        /// <summary>A string of pennant flags between two points, in one group.</summary>
        private void Bunting(Vector3 pFrom, Vector3 pTo, int pSeed)
        {
            Transform lGroup = K.Group("Bunting", K.Props);
            int lCount = Mathf.RoundToInt(Vector3.Distance(pFrom, pTo) / 1.4f);
            Quaternion lRot = Quaternion.LookRotation(new Vector3(pTo.x - pFrom.x, 0, pTo.z - pFrom.z));
            K.Block(lGroup, "Rope", (pFrom + pTo) * 0.5f, new Vector3(0.03f, 0.03f, Vector3.Distance(pFrom, pTo)), new Color(0.3f, 0.3f, 0.3f), GameLayers.DEFAULT, false, Quaternion.LookRotation(pTo - pFrom));
            for (int i = 1; i < lCount; i++)
            {
                Vector3 p = Vector3.Lerp(pFrom, pTo, (float)i / lCount) + Vector3.down * 0.25f;
                K.Block(lGroup, "Flag", p, new Vector3(0.02f, 0.45f, 0.5f), AWNINGS[(i + pSeed) % AWNINGS.Length], GameLayers.DEFAULT, false, lRot * Quaternion.Euler(0, 90, 0));
            }
        }

        /// <summary>String lights between two points: a cable and warm bulbs (one real light in the middle).</summary>
        private void StringLights(Vector3 pFrom, Vector3 pTo)
        {
            Transform lGroup = K.Group("StringLights", K.Props);
            K.Block(lGroup, "Cable", (pFrom + pTo) * 0.5f, new Vector3(0.03f, 0.03f, Vector3.Distance(pFrom, pTo)), new Color(0.2f, 0.2f, 0.2f), GameLayers.DEFAULT, false, Quaternion.LookRotation(pTo - pFrom));
            int lCount = Mathf.RoundToInt(Vector3.Distance(pFrom, pTo) / 1.5f);
            for (int i = 1; i < lCount; i++) Neon(lGroup, Vector3.Lerp(pFrom, pTo, (float)i / lCount) - Vector3.up * 0.12f, Vector3.one * 0.14f, new Color(2.4f, 1.9f, 1.2f));
            K.Lamp(Vector3.Lerp(pFrom, pTo, 0.5f) - Vector3.up * 0.4f, WARM, 9f, 1.1f, false);
        }

        /// <summary>A parasol (or an umbrella table): no box over the canopy, only the pole and the table collide.</summary>
        private void Parasol(Vector3 pAt, float pYaw, Color pTint, bool pTable)
        {
            GameObject lShade = K.Prop(pTable ? "UmbrellaTable" : "Parasol", pAt, pYaw, pTint, MapKit.Collide.None);
            if (lShade == null) return;
            BoxCollider lBox = lShade.AddComponent<BoxCollider>();
            lBox.center = new Vector3(0, pTable ? 0.4f : 1.1f, 0);
            lBox.size = pTable ? new Vector3(1.1f, 0.8f, 1.1f) : new Vector3(0.12f, 2.2f, 0.12f);
        }

        private Mesh _cylinder, _cone, _dome, _sphere;
        private Mesh CylMesh => _cylinder != null ? _cylinder : _cylinder = EditorAssets.SaveMesh(LowPolyMesh.Cylinder(16), "Suburb_Cyl16");
        private Mesh ConeMesh => _cone != null ? _cone : _cone = EditorAssets.SaveMesh(LowPolyMesh.Cone(16), "Suburb_Cone16");
        private Mesh DomeMesh => _dome != null ? _dome : _dome = EditorAssets.SaveMesh(LowPolyMesh.Icosphere(2), "Suburb_Dome");
        private Mesh BallMesh => _sphere != null ? _sphere : _sphere = EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1), "Suburb_Ball");

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
            Debug.Log($"[Build] Map_Suburb: {lMoved} activity nodes moved off furniture");
        }

        // ================================================================ water, edges, vegetation, match points

        private void Waters()
        {
            // The canal: one long strip between its shelves, the pond, and the creek as planes along its course.
            K.Water(new Vector3(CANAL_X, CANAL_WATER, (CANAL_HEAD - 476f) * 0.5f), new Vector2(9.5f, CANAL_HEAD + 476f));
            K.Water(new Vector3(POND.x, POND_WATER, POND.y), new Vector2(POND_R * 2f + 4f, POND_R * 2f + 4f));
            foreach (Vector2[] lCreek in new[] { CREEK_UP, CREEK_DOWN })
                for (int s = 0; s < lCreek.Length - 1; s++)
                {
                    Vector2 a = lCreek[s], b = lCreek[s + 1];
                    if (lCreek == CREEK_DOWN && s == lCreek.Length - 2) continue;
                    Vector2 lDir = (b - a).normalized;
                    float y = Mathf.Min(_h[Ci(a.x), Ci(a.y)], _h[Ci(b.x), Ci(b.y)]) + 0.55f;
                    GameObject lPlane = K.Water(new Vector3((a.x + b.x) * 0.5f, y, (a.y + b.y) * 0.5f), new Vector2(7f, Vector2.Distance(a, b) + 4f));
                    lPlane.transform.rotation = Quaternion.LookRotation(new Vector3(lDir.x, (_h[Ci(b.x), Ci(b.y)] - _h[Ci(a.x), Ci(a.y)]) / Mathf.Max(1f, Vector2.Distance(a, b)), lDir.y));
                }
        }

        /// <summary>Invisible walls on the map edges, and the land beyond them (woods and plain, no collider).</summary>
        private void MapEdges()
        {
            for (int s = 0; s < 4; s++)
            {
                Vector3 lPos = s switch { 0 => new Vector3(0, 40, -498), 1 => new Vector3(498, 40, 0), 2 => new Vector3(0, 40, 498), _ => new Vector3(-498, 40, 0) };
                Vector3 lSize = s % 2 == 0 ? new Vector3(1000, 140, 2) : new Vector3(2, 140, 1000);
                K.Block(K.Env, "MapEdge", lPos, lSize, Color.white).GetComponent<MeshRenderer>().enabled = false;
            }
            K.Block(K.Env, "Skirt", new Vector3(0, 4f, 1260f), new Vector3(3000f, 4f, 1520f), WOODS * 0.9f, GameLayers.DEFAULT, false);
            K.Block(K.Env, "Skirt", new Vector3(0, 4f, -1260f), new Vector3(3000f, 4f, 1520f), WOODS * 0.9f, GameLayers.DEFAULT, false);
            K.Block(K.Env, "Skirt", new Vector3(-1260f, 4f, 0), new Vector3(1520f, 4f, 1000f), WOODS * 0.9f, GameLayers.DEFAULT, false);
            K.Block(K.Env, "Skirt", new Vector3(1260f, HWY - 2.2f, 0), new Vector3(1520f, 4f, 1000f), MEADOW * 0.9f, GameLayers.DEFAULT, false);
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

        /// <summary>Woods on the edges and the hill, clusters of trees and bushes in the open, reeds and willows by the water.</summary>
        private void Vegetation()
        {
            int lTrees = 0, lBushes = 0, lRocks = 0;
            const float STEP = 7f;
            for (float gx = -494f; gx < 494f; gx += STEP)
                for (float gz = -494f; gz < 494f; gz += STEP)
                {
                    float x = gx + Rand(-3.5f, 3.5f), z = gz + Rand(-3.5f, 3.5f);
                    int i = Ci(x), j = Ci(z);
                    if (x > 440f || Blocked(i, j) || Tame(x, z)) continue;
                    float lWild = Mathf.Clamp01(1f - (Border(x, z) - 30f) / 60f);
                    float lHillside = z > 160f && x < -120f && !PLATEAU.Contains(new Vector2(x, z)) ? 0.75f : 0f;
                    float lCluster = Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x / 50f + 4f, z / 50f + 1f) - 0.4f) * 2.5f);
                    float lWoods = Mathf.Max(lWild, lHillside);
                    float lRoll = (float)K.Rng.NextDouble();
                    if (lRoll < lWoods * 0.55f + lCluster * 0.12f)
                    {
                        string lPiece = lWild > 0.5f ? Pick(new[] { "PineA", "PineB", "BroadleafA", "PineC", "BroadleafC" }) : Pick(new[] { "BroadleafA", "BroadleafC", "BirchA", "BroadleafA", "BroadleafB" });
                        float s = Rand(0.75f, 1.15f) * (lPiece == "BroadleafB" || lPiece == "PineC" ? 0.75f : 1f);
                        Nat(lPiece, x, z, s, -1f, null, MapKit.Collide.Box, 0.15f);
                        lTrees++;
                    }
                    else if (lRoll < lWoods * 0.55f + lCluster * 0.12f + 0.08f + lWoods * 0.2f)
                    {
                        Nat(Pick(new[] { "BushA", "BushB", "BushC" }), x, z, Rand(0.7f, 1.3f), -1f, null, MapKit.Collide.None, 0.1f);
                        lBushes++;
                    }
                    else if (lRoll > 0.985f)
                    {
                        Nat(Pick(new[] { "RockB", "RockC", "RockA" }), x, z, Rand(0.6f, 1.2f), -1f, new Color(0.6f, 0.58f, 0.55f), MapKit.Collide.Box, 0.3f);
                        lRocks++;
                    }
                }
            // Reeds and willows along the creek and the pond.
            for (int i = 1; i < N - 1; i++)
                for (int j = 1; j < N - 1; j++)
                {
                    if ((_f[i, j] & BANK) == 0 || Tame(Cx(i), Cx(j)) || Flag(Cx(i), Cx(j), ROAD)) continue;
                    if (K.Rng.NextDouble() < 0.35) { Nat("ReedsClump", Cx(i) + Rand(-1.5f, 1.5f), Cx(j) + Rand(-1.5f, 1.5f), Rand(0.8f, 1.3f), -1f, null, MapKit.Collide.None); lBushes++; }
                    else if (K.Rng.NextDouble() < 0.08) { Nat("BroadleafC", Cx(i), Cx(j), Rand(0.7f, 0.95f), -1f, new Color(0.5f, 0.66f, 0.36f), MapKit.Collide.Box, 0.1f); lTrees++; }
                }
            Debug.Log($"[Build] Map_Suburb vegetation: {lTrees} trees, {lBushes} bushes, {lRocks} rocks");
        }

        private void MatchPoints()
        {
            K.Point(MatchPoint.Kind.Start, G(-486, MAPLE_Z, 0.2f), 90, "West woods road");
            K.Point(MatchPoint.Kind.Start, G(150, 486, 0.2f), 180, "North trail");
            K.Point(MatchPoint.Kind.Start, new Vector3(470, HWY + 0.2f, -486), 0, "Expressway south");
            K.Point(MatchPoint.Kind.Start, G(-60, -486, 0.2f), 0, "South field path");
        }
    }
}
