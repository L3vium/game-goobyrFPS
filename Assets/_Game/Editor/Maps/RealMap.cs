using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The recipe of the realistic 500 m maps: a relief shaped by <see cref="LandShape"/> stamps
    /// (graded roads, building pads, terraces, gorges), PBR ground layers per biome, generated
    /// buildings (<see cref="ArchBuilder"/>) on their pads with cellars cut into the land, cave
    /// tunnels, the biome's trees, bushes, rocks and instanced grass from the <c>Veg_*</c> models, tall
    /// grass fields to hide in, the map edges and the time of day. A map writes <see cref="Relief"/>,
    /// <see cref="Plan"/> (pads, roads, before the land exists) and <see cref="Dress"/> (buildings,
    /// props, NPCs, markers).
    /// </summary>
    public abstract class RealMap : MapBuilderBase
    {
        protected const float HALF = 250f, SIZE = 500f;
        protected virtual float MinY => -30f;
        protected virtual float MaxY => 110f;

        public class Street
        {
            public Vector2[] path;
            public float width;
            public Ground ground;
        }

        protected LandShape Shape;
        protected ArchKit A;
        protected readonly List<Street> Streets = new List<Street>();
        private readonly List<(Rect rect, Ground ground)> _pads = new List<(Rect, Ground)>();
        private readonly List<(Vector2 center, Vector2 size, float yaw, Ground ground)> _turnedPads = new List<(Vector2, Vector2, float, Ground)>();
        private readonly List<(Matrix4x4 toLocal, Rect rect)> _cellarHoles = new List<(Matrix4x4, Rect)>();
        private readonly List<(Vector3 a, Vector3 b, float radius)> _caveHoles = new List<(Vector3, Vector3, float)>();
        private bool[,] _built;
        private const float MASK_CELL = 2f;

        protected abstract TimeOfDay.Biome Biome { get; }
        protected virtual float SunHeading => 0f;
        protected virtual SecurityTier OutsideTier => SecurityTier.Restricted;
        protected abstract float Relief(float pX, float pZ);
        protected abstract void Plan();
        protected abstract void Dress();
        /// <summary>The PBR ground layers in <see cref="Ground"/> order.</summary>
        protected abstract TerrainLayer[] Layers();
        /// <summary>The instanced details (grass, ferns) and the tall grass of the hiding fields.</summary>
        protected abstract void Details();
        /// <summary>0..1 how wooded a point left wild is.</summary>
        protected virtual float Woods(float pX, float pZ, float pHeight) => Mathf.Clamp01(0.55f + LandShape.Fbm(pX, pZ, 70f, 3, 2.2f));
        /// <summary>A tree (or bush, or rock) for a wild point, by roll 0..1 under the woods density.</summary>
        protected abstract string TreeAt(float pX, float pZ, float pHeight, float pRoll, System.Random pRandom, out float pScale);
        protected virtual float WaterLevel => -100f;
        /// <summary>Slope (degrees) where bare rock takes over the ground (snow maps keep snow higher).</summary>
        protected virtual float RockSlope => 32f;
        /// <summary>The boulders that dress cliff faces (embedded, 4 to 10 m) to break the stretched walls.</summary>
        protected virtual string CliffRock(System.Random pRandom) => pRandom.Next(3) switch { 0 => "Veg_Rock_A", 1 => "Veg_Rock_B", _ => "Veg_Rock_C" };
        protected virtual void NaturalGround(float pX, float pZ, float pHeight, float pSlope, float[] pWeights)
        {
            float n = LandShape.Fbm(pX, pZ, 30f, 3, 1.7f);
            pWeights[(int)Ground.Grass] = 1f + n;
            pWeights[(int)Ground.DryGrass] = Mathf.Clamp01(0.4f - n);
            pWeights[(int)Ground.ForestFloor] = Mathf.Clamp01(Woods(pX, pZ, pHeight) - 0.5f) * 3f;
        }

        protected override void Compose()
        {
            ArchLib.Reset();
            ArchBuilder.MeshPrefix = K.Map.sceneName;
            // The sky material and the volume profile of the map come from the art pass.
            if (ArtBuilder.Sky(K.Map) == null) ArtBuilder.Build();
            Shape = new LandShape(Relief);
            Plan();
            K.LandLayers = Untinted(Layers());
            K.PlanStorey = 3.6f;
            K.AfterLight = (pSun, pSky) =>
            {
                GameObject lTime = new GameObject("TimeOfDay");
                lTime.transform.SetParent(K.Lights, false);
                lTime.AddComponent<TimeOfDay>().Setup(pSun, pSky, TimeOfDay.PresetsFor(Biome, SunHeading));
            };
            K.BuildLand(SIZE, MinY, MaxY, Shape.Height);
            BuildMask();
            K.PaintLand(PaintWeights);
            A = new ArchKit(K);
            GameObject lOutside = new GameObject("SecurityOutside");
            lOutside.transform.SetParent(K.Markers, false);
            lOutside.AddComponent<SecurityOutside>().Setup(OutsideTier);
            Dress();
            K.LandHoles(HoleAt);
            PlantTallGrass();
            Details();
            Vegetate();
            Edges();
            K.CoverSpacing = 1.8f;
            K.DistanceCulling();
        }

        // ---------------------------------------------------------------- planning

        protected Street Road(float pWidth, Ground pGround, params Vector2[] pPath) => Road(pWidth, pGround, 26f, pPath);

        protected Street Road(float pWidth, Ground pGround, float pWindow, params Vector2[] pPath)
        {
            Shape.Road(pPath, pWidth, 4f, pWindow);
            Street lStreet = new Street { path = pPath, width = pWidth, ground = pGround };
            Streets.Add(lStreet);
            return lStreet;
        }

        /// <summary>A flat paved area at a height (null keeps the land's), kept free of trees.</summary>
        protected void Plaza(Rect pRect, Ground pGround, float? pHeight = null, float pFeather = 5f)
        {
            if (pHeight.HasValue) Shape.Flat(pRect, pHeight.Value, pFeather);
            _pads.Add((pRect, pGround));
            Clear.Add(pRect);
        }

        /// <summary>
        /// The pad of a building: flat at the spec's origin height over its (rotated) footprint grown
        /// by <paramref name="pMargin"/>, painted, kept clear of trees.
        /// </summary>
        protected void Lot(ArchSpec pSpec, float pMargin = 3f, Ground pGround = Ground.Cobble, float pFeather = 5f)
        {
            Vector2 lCenter = Center(pSpec);
            Vector2 lSize = pSpec.size + Vector2.one * pMargin * 2f;
            Shape.Flat(lCenter, lSize, pSpec.yaw, pSpec.origin.y - 0.02f, pFeather);
            _turnedPads.Add((lCenter, lSize, pSpec.yaw, pGround));
            float lReach = lSize.magnitude * 0.5f;
            Clear.Add(new Rect(lCenter.x - lReach * 0.8f, lCenter.y - lReach * 0.8f, lReach * 1.6f, lReach * 1.6f));
        }

        /// <summary>
        /// A spec placed by its front: the middle of its front edge (local z = 0) at a world point,
        /// facing 0 north, 1 east, 2 south or 3 west; the building extends behind it.
        /// </summary>
        protected static ArchSpec Facing(string pName, Vector2 pFront, float pY, int pFacing, float pWidth, float pDepth, ArchStyle pStyle, int pZone)
        {
            float lYaw = pFacing switch { 0 => 180f, 1 => -90f, 2 => 0f, _ => 90f };
            Vector3 lOrigin = new Vector3(pFront.x, pY, pFront.y) - Quaternion.Euler(0, lYaw, 0) * new Vector3(pWidth * 0.5f, 0, 0);
            return new ArchSpec { name = pName, origin = lOrigin, yaw = lYaw, size = new Vector2(pWidth, pDepth), style = pStyle, zone = pZone };
        }

        /// <summary>World centre (xz) of a spec's footprint.</summary>
        protected static Vector2 Center(ArchSpec pSpec)
        {
            Vector3 c = pSpec.origin + Quaternion.Euler(0, pSpec.yaw, 0) * new Vector3(pSpec.size.x * 0.5f, 0, pSpec.size.y * 0.5f);
            return new Vector2(c.x, c.z);
        }

        /// <summary>Generates a planned building, furnishes it, and cuts the land over its cellar.</summary>
        protected ArchResult Build(ArchSpec pSpec, int pZone)
        {
            ArchResult r = ArchBuilder.Build(pSpec, K.Env);
            ArchFurnish.Furnish(A, r, pZone);
            Matrix4x4 lToLocal = r.root.worldToLocalMatrix;
            if (pSpec.cutLand) _cellarHoles.Add((lToLocal, new Rect(0.3f, 0.3f, pSpec.size.x - 0.6f, pSpec.size.y - 0.6f)));
            foreach (ArchRoom lRoom in pSpec.rooms)
                if (lRoom.floor < 0) _cellarHoles.Add((lToLocal, new Rect(lRoom.rect.xMin + 0.1f, lRoom.rect.yMin + 0.1f, lRoom.rect.width - 0.2f, lRoom.rect.height - 0.2f)));
            return r;
        }

        /// <summary>A cut in the land along a cave tunnel's mouth (where its tube breaks the surface).</summary>
        protected void CaveHole(Vector3 pA, Vector3 pB, float pRadius) => _caveHoles.Add((pA, pB, pRadius));

        private bool HoleAt(float pX, float pZ)
        {
            foreach ((Matrix4x4 lToLocal, Rect lRect) in _cellarHoles)
            {
                Vector3 l = lToLocal.MultiplyPoint3x4(new Vector3(pX, 0f, pZ));
                if (lRect.Contains(new Vector2(l.x, l.z))) return true;
            }
            foreach ((Vector3 a, Vector3 b, float lRadius) in _caveHoles)
            {
                Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), p2 = new Vector2(pX, pZ);
                Vector2 ab = b2 - a2;
                float t = Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                if (Vector2.Distance(p2, a2 + ab * t) > lRadius) continue;
                float lFloor = Mathf.Lerp(a.y, b.y, t);
                float lGround = K.GroundAt(pX, pZ);
                // Only where the land lies inside the tube's height: the mouth, not the hill above it.
                if (lGround > lFloor - 0.6f && lGround < lFloor + 3.6f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- the land

        private void BuildMask()
        {
            int n = Mathf.CeilToInt(SIZE / MASK_CELL);
            _built = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = -HALF + (i + 0.5f) * MASK_CELL, z = -HALF + (j + 0.5f) * MASK_CELL;
                    _built[i, j] = !IsClear(x, z) || OnStreet(x, z, 3f) >= 0 || OnPad(x, z, 2f) != null;
                }
        }

        /// <summary>Marks an area built after the land exists (no trees, no grass there).</summary>
        protected void MarkBuilt(Rect pRect)
        {
            Clear.Add(pRect);
            if (_built == null) return;
            int n = _built.GetLength(0);
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = -HALF + (i + 0.5f) * MASK_CELL, z = -HALF + (j + 0.5f) * MASK_CELL;
                    if (pRect.Contains(new Vector2(x, z))) _built[i, j] = true;
                }
        }

        protected bool Built(float pX, float pZ)
        {
            int i = Mathf.FloorToInt((pX + HALF) / MASK_CELL), j = Mathf.FloorToInt((pZ + HALF) / MASK_CELL);
            return i < 0 || j < 0 || i >= _built.GetLength(0) || j >= _built.GetLength(1) || _built[i, j];
        }

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

        private Ground? OnPad(float pX, float pZ, float pMargin)
        {
            Vector2 p = new Vector2(pX, pZ);
            foreach ((Rect lRect, Ground lGround) in _pads)
                if (p.x > lRect.xMin - pMargin && p.x < lRect.xMax + pMargin && p.y > lRect.yMin - pMargin && p.y < lRect.yMax + pMargin) return lGround;
            foreach ((Vector2 c, Vector2 lSize, float lYaw, Ground lGround) in _turnedPads)
            {
                Vector3 l = Quaternion.Euler(0, -lYaw, 0) * new Vector3(pX - c.x, 0, pZ - c.y);
                if (Mathf.Abs(l.x) < lSize.x * 0.5f + pMargin && Mathf.Abs(l.z) < lSize.y * 0.5f + pMargin) return lGround;
            }
            return null;
        }

        private float[] PaintWeights(float pX, float pZ, float pHeight, float pSlope)
        {
            float[] w = new float[8];
            NaturalGround(pX, pZ, pHeight, pSlope, w);
            w[(int)Ground.Dirt] += Mathf.Clamp01((pSlope - 22f) / 12f) * 2f;
            w[(int)Ground.Rock] += Mathf.Clamp01((pSlope - RockSlope) / 8f) * 10f;
            int s = OnStreet(pX, pZ, 0f);
            if (s >= 0) { w[(int)Streets[s].ground] += 40f; return w; }
            if (OnStreet(pX, pZ, 1.2f) >= 0) w[(int)Ground.Dirt] += 5f;
            Ground? lPad = OnPad(pX, pZ, 0f);
            if (lPad.HasValue) { w[(int)lPad.Value] += 40f; return w; }
            if (OnPad(pX, pZ, 1.5f).HasValue) w[(int)Ground.Dirt] += 4f;
            return w;
        }

        private void PlantTallGrass()
        {
            System.Random lRandom = new System.Random(K.Map.sceneName.GetHashCode() + 77);
            List<(Vector2 point, float score)> lCandidates = new List<(Vector2, float)>();
            for (float x = -HALF + 30f; x < HALF - 30f; x += 9f)
                for (float z = -HALF + 30f; z < HALF - 30f; z += 9f)
                {
                    Vector2 p = new Vector2(x + (float)lRandom.NextDouble() * 5f, z + (float)lRandom.NextDouble() * 5f);
                    if (Built(p.x, p.y) || K.GroundAt(p.x, p.y) < WaterLevel + 0.8f || K.LandSlope(p.x, p.y) > 20f) continue;
                    float lToBuilt = DistanceToBuilt(p, 40f);
                    if (lToBuilt < 5f || lToBuilt > 28f) continue;
                    lCandidates.Add((p, 1f - Mathf.Abs(lToBuilt - 12f) / 20f + (float)lRandom.NextDouble() * 0.35f));
                }
            lCandidates.Sort((a, b) => b.score.CompareTo(a.score));
            List<Vector2> lPicked = new List<Vector2>();
            foreach ((Vector2 lPoint, float _) in lCandidates)
            {
                if (lPicked.Count >= 12) break;
                bool lFar = true;
                foreach (Vector2 lOther in lPicked) if ((lOther - lPoint).sqrMagnitude < 50f * 50f) { lFar = false; break; }
                if (!lFar) continue;
                float lRadius = Mathf.Min(8f + (float)lRandom.NextDouble() * 5f, DistanceToBuilt(lPoint, 20f) - 2f);
                if (lRadius < 5.5f) continue;
                lPicked.Add(lPoint);
                K.TallGrassField(lPoint, lRadius);
            }
        }

        private float DistanceToBuilt(Vector2 pPoint, float pMax)
        {
            for (float r = 2f; r <= pMax; r += 2f)
                for (int a = 0; a < 16; a++)
                {
                    float lAngle = a * Mathf.PI / 8f;
                    if (Built(pPoint.x + Mathf.Cos(lAngle) * r, pPoint.y + Mathf.Sin(lAngle) * r)) return r;
                }
            return pMax;
        }

        /// <summary>Trees, bushes and rocks on everything left wild (no grass under them is the terrain's job).</summary>
        private void Vegetate()
        {
            System.Random lRandom = new System.Random(K.Map.sceneName.GetHashCode());
            for (float x = -HALF + 2f; x < HALF - 2f; x += 3.2f)
                for (float z = -HALF + 2f; z < HALF - 2f; z += 3.2f)
                {
                    float px = x + (float)lRandom.NextDouble() * 2.8f, pz = z + (float)lRandom.NextDouble() * 2.8f;
                    if (Built(px, pz) || K.InTallGrass(px, pz, 1.5f)) continue;
                    float h = K.GroundAt(px, pz);
                    if (h < WaterLevel + 0.5f) continue;
                    float lSlope = K.LandSlope(px, pz);
                    if (lSlope > 38f)
                    {
                        // Scree slopes: boulders break up the rock faces.
                        if (lSlope < 52f && lRandom.NextDouble() < 0.05) K.LandTree(lRandom.NextDouble() < 0.5 ? "Veg_Rock_B" : "Veg_Rock_A", px, pz, 1.5f + (float)lRandom.NextDouble() * 2f);
                        // Cliff walls: big boulders half sunk into the face give a broken, layered silhouette
                        // and hide the terrain texture stretched down the steep slope.
                        else if (lSlope >= 52f && lRandom.NextDouble() < 0.2) K.LandTree(CliffRock(lRandom), px, pz, 4f + (float)lRandom.NextDouble() * 6f);
                        continue;
                    }
                    float lWoods = Woods(px, pz, h);
                    float lRoll = (float)lRandom.NextDouble();
                    if (lRoll > lWoods) continue;
                    string lPiece = TreeAt(px, pz, h, lRoll / Mathf.Max(0.01f, lWoods), lRandom, out float lScale);
                    if (!string.IsNullOrEmpty(lPiece)) K.LandTree(lPiece, px, pz, lScale);
                }
        }

        private void Edges()
        {
            for (int s = 0; s < 4; s++)
            {
                Vector3 lPos = s switch { 0 => new Vector3(0, 40, -HALF + 1f), 1 => new Vector3(HALF - 1f, 40, 0), 2 => new Vector3(0, 40, HALF - 1f), _ => new Vector3(-HALF + 1f, 40, 0) };
                Vector3 lSize = s % 2 == 0 ? new Vector3(SIZE, 200, 2) : new Vector3(2, 200, SIZE);
                GameObject lEdge = new GameObject("MapEdge");
                lEdge.transform.SetParent(K.Markers, false);
                lEdge.transform.position = lPos;
                lEdge.AddComponent<BoxCollider>().size = lSize;
            }
        }

        // ---------------------------------------------------------------- helpers

        protected Vector3 G(float pX, float pZ, float pUp = 0.05f) => K.At(pX, pZ, pUp);
        protected Vector3 G(Vector2 p, float pUp = 0.05f) => K.At(p.x, p.y, pUp);

        /// <summary>A world point at a yaw-rotated offset from an origin.</summary>
        protected static Vector3 Off(Vector3 pOrigin, float pYaw, float pX, float pY, float pZ) => pOrigin + Quaternion.Euler(0, pYaw, 0) * new Vector3(pX, pY, pZ);

        /// <summary>A crawl or walk tunnel through rock: a noisy tube along floor points, lanterns, holes at the mouths.</summary>
        protected void Cave(string pName, float pWidth, float pHeight, params Vector3[] pPath)
        {
            ArchMat lRock = ArchLib.Surface("rock_face", 4f);
            Transform lRoot = K.Group("Cave_" + pName, K.Env);
            List<Vector3> lPoints = new List<Vector3>();
            for (int s = 0; s < pPath.Length - 1; s++)
            {
                int lSteps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(pPath[s], pPath[s + 1]) / 1.6f));
                for (int k = 0; k < lSteps; k++) lPoints.Add(Vector3.Lerp(pPath[s], pPath[s + 1], (float)k / lSteps));
            }
            lPoints.Add(pPath[pPath.Length - 1]);
            const int N = 14;
            Vector3[][] lRings = new Vector3[lPoints.Count][];
            for (int i = 0; i < lPoints.Count; i++)
            {
                Vector3 lPrev = lPoints[Mathf.Max(0, i - 1)], lNext = lPoints[Mathf.Min(lPoints.Count - 1, i + 1)];
                Vector3 lForward = lNext - lPrev;
                lForward.y = 0f;
                if (lForward.sqrMagnitude < 1e-4f) lForward = Vector3.forward;
                lForward.Normalize();
                Vector3 lRight = Vector3.Cross(Vector3.up, lForward);
                lRings[i] = new Vector3[N];
                // Ends stay regular so they meet doors and mouths cleanly.
                float lNoise = (i == 0 || i == lPoints.Count - 1) ? 0f : 1f;
                float lWiden = 1f + 0.35f * Mathf.PerlinNoise(i * 0.13f, 3.7f) * lNoise;
                for (int k = 0; k < N; k++)
                {
                    float a = k * Mathf.PI * 2f / N;
                    float n = 1f + (Mathf.PerlinNoise(i * 0.31f + k * 0.17f, k * 0.53f) - 0.5f) * 0.45f * lNoise;
                    Vector3 lOffset = lRight * Mathf.Cos(a) * pWidth * 0.5f * n * lWiden + Vector3.up * (Mathf.Sin(a) * pHeight * 0.5f * n + pHeight * 0.5f);
                    Vector3 p = lPoints[i] + lOffset;
                    if (p.y < lPoints[i].y) p.y = lPoints[i].y;
                    lRings[i][k] = p;
                }
            }
            ArchMesh lMesh = new ArchMesh();
            for (int i = 0; i < lPoints.Count - 1; i++)
            {
                Vector3 lMid = (lPoints[i] + lPoints[i + 1]) * 0.5f + Vector3.up * pHeight * 0.5f;
                for (int k = 0; k < N; k++)
                {
                    Vector3 a = lRings[i][k], b = lRings[i][(k + 1) % N], c = lRings[i + 1][(k + 1) % N], d = lRings[i + 1][k];
                    Vector3 lNormal = Vector3.Cross(c - a, b - a);
                    if (Vector3.Dot(lNormal, lMid - (a + c) * 0.5f) > 0f) lMesh.Quad(lRock, a, b, c, d); else lMesh.Quad(lRock, d, c, b, a);
                }
            }
            lMesh.Flush(lRoot, K.Map.sceneName + "_Cave_" + pName);
            // Lanterns along the way.
            float lRun = 0f;
            for (int i = 1; i < lPoints.Count; i++)
            {
                lRun += Vector3.Distance(lPoints[i - 1], lPoints[i]);
                if (lRun < 14f) continue;
                lRun = 0f;
                Vector3 lWall = lRings[i][0] + (lRings[i][N / 2] - lRings[i][0]).normalized * 0.25f;
                Vector3 lLamp = new Vector3(lWall.x, lPoints[i].y + pHeight * 0.7f, lWall.z);
                Light lLight = new GameObject("CaveLantern").AddComponent<Light>();
                lLight.transform.SetParent(lRoot, false);
                lLight.transform.position = lLamp;
                lLight.type = LightType.Point;
                lLight.range = 9f;
                lLight.intensity = 1.6f;
                lLight.color = new Color(1f, 0.62f, 0.32f);
                lLight.shadows = LightShadows.None;
                ArchMesh lBulb = new ArchMesh();
                lBulb.Box(ArchLib.LampGlow, Vector3.zero, new Vector3(0.18f, 0.26f, 0.18f), false);
                lBulb.Box(ArchLib.Iron, Vector3.up * 0.16f, new Vector3(0.22f, 0.05f, 0.22f), false);
                GameObject lHolder = new GameObject("Lantern");
                lHolder.transform.SetParent(lRoot, false);
                lHolder.transform.position = lLamp;
                lBulb.Flush(lHolder.transform, "Arch_CaveLantern");
            }
            CaveHole(pPath[0], pPath[Mathf.Min(1, pPath.Length - 1)], pWidth * 0.6f);
            CaveHole(pPath[pPath.Length - 1], pPath[Mathf.Max(0, pPath.Length - 2)], pWidth * 0.6f);
            Mouth(lRoot, pName + "_A", pPath[0], pPath[Mathf.Min(1, pPath.Length - 1)], pWidth, pHeight);
            Mouth(lRoot, pName + "_B", pPath[pPath.Length - 1], pPath[Mathf.Max(0, pPath.Length - 2)], pWidth, pHeight);
        }

        /// <summary>
        /// A visible cave mouth where a tunnel meets the open ground: a timber frame (posts, lintel,
        /// struts), boulders heaped either side and over it, a lantern hung at the entrance.
        /// </summary>
        private void Mouth(Transform pRoot, string pName, Vector3 pEnd, Vector3 pInward, float pWidth, float pHeight)
        {
            if (Mathf.Abs(K.GroundAt(pEnd.x, pEnd.z) - pEnd.y) > 2.5f) return;
            Vector3 lIn = pInward - pEnd;
            lIn.y = 0f;
            if (lIn.sqrMagnitude < 0.01f) return;
            lIn.Normalize();
            Vector3 lSide = Vector3.Cross(Vector3.up, lIn);
            ArchMesh lMesh = new ArchMesh();
            ArchMat lTimber = ArchLib.Surface("rough_wood", 1.2f);
            Vector3 lFrame = pEnd + lIn * 1.2f;
            float w = pWidth * 0.5f - 0.15f, h = pHeight - 0.25f;
            foreach (float s in new[] { -1f, 1f }) lMesh.Tube(lTimber, lFrame + lSide * s * w - Vector3.up * 0.3f, lFrame + lSide * s * w + Vector3.up * h, 0.13f, 8);
            lMesh.Tube(lTimber, lFrame - lSide * (w + 0.3f) + Vector3.up * h, lFrame + lSide * (w + 0.3f) + Vector3.up * h, 0.15f, 8);
            foreach (float s in new[] { -1f, 1f }) lMesh.Tube(lTimber, lFrame + lSide * s * w + Vector3.up * (h - 0.8f), lFrame + lSide * s * (w - 0.6f) + Vector3.up * h, 0.07f, 6, false);
            lMesh.Box(ArchLib.LampGlow, lFrame + Vector3.up * (h - 0.35f) - lIn * 0.15f, new Vector3(0.16f, 0.24f, 0.16f), false);
            GameObject lObject = new GameObject("CaveMouth_" + pName);
            lObject.transform.SetParent(pRoot, false);
            lMesh.Flush(lObject.transform, K.Map.sceneName + "_CaveMouth_" + pName);
            Light lLight = new GameObject("Lamp").AddComponent<Light>();
            lLight.transform.SetParent(K.Lights, false);
            lLight.transform.position = lFrame + Vector3.up * (h - 0.6f) - lIn * 0.4f;
            lLight.type = LightType.Point;
            lLight.range = 8f;
            lLight.intensity = 1.8f;
            lLight.color = new Color(1f, 0.62f, 0.32f);
            lLight.shadows = LightShadows.None;
            // Boulders framing the opening.
            System.Random r = new System.Random(pName.GetHashCode());
            foreach (float s in new[] { -1f, 1f })
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = pEnd + lSide * s * (pWidth * 0.5f + 1.6f + k * 1.8f) + lIn * (0.5f + k * 1.2f);
                    K.LandTree(CliffRock(r), p.x, p.z, 2.2f + (float)r.NextDouble() * 1.6f);
                }
        }

        /// <summary>Running water: the lake shader with faster, choppier ripples, a swell and wide foam.</summary>
        protected static Material RiverMaterial()
        {
            Material lBase = ArtBuilder.Water;
            if (lBase == null) return null;
            Material lRiver = EditorAssets.Material(EditorAssets.ART_RESOURCES + "/M_WaterRiver.mat", lBase.shader);
            lRiver.CopyPropertiesFromMaterial(lBase);
            lRiver.SetFloat("_RippleStrength", 1.15f);
            lRiver.SetFloat("_RippleSpeed", 1.7f);
            lRiver.SetFloat("_RippleScale", 0.55f);
            lRiver.SetFloat("_Swell", 0.14f);
            lRiver.SetFloat("_FoamWidth", 1.3f);
            UnityEditor.EditorUtility.SetDirty(lRiver);
            return lRiver;
        }

        /// <summary>A flat water strip along a polyline (a river), each leg a rotated swimmable plane.</summary>
        protected void River(float pLevel, float pWidth, Vector3 pCurrent, params Vector2[] pPath)
        {
            for (int s = 0; s < pPath.Length - 1; s++)
            {
                Vector2 a = pPath[s], b = pPath[s + 1];
                Vector2 lMid = (a + b) * 0.5f;
                float lLength = Vector2.Distance(a, b) + pWidth;
                GameObject lWater = K.Water(new Vector3(lMid.x, pLevel, lMid.y), new Vector2(pWidth, lLength), pCurrent);
                lWater.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg, 0);
                Material lRiver = RiverMaterial();
                if (lRiver != null) lWater.GetComponent<MeshRenderer>().sharedMaterial = lRiver;
            }
        }

        /// <summary>A copy of the layer with its albedo scaled (greener grass, paler sand), saved next to it as
        /// <c>TL_&lt;id&gt;_&lt;hex&gt;</c> so the shared layer other maps use stays untinted.</summary>
        protected static TerrainLayer Tint(TerrainLayer pLayer, Color pTint)
        {
            if (pLayer == null) return null;
            string lPath = UnityEditor.AssetDatabase.GetAssetPath(pLayer);
            if (string.IsNullOrEmpty(lPath)) return pLayer;
            string lTintPath = lPath.Replace(".terrainlayer", "_" + ColorUtility.ToHtmlStringRGB(pTint) + ".terrainlayer");
            TerrainLayer lTinted = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(lTintPath);
            if (lTinted == null)
            {
                lTinted = new TerrainLayer();
                UnityEditor.AssetDatabase.CreateAsset(lTinted, lTintPath);
            }
            UnityEditor.EditorUtility.CopySerialized(pLayer, lTinted);
            lTinted.name = System.IO.Path.GetFileNameWithoutExtension(lTintPath);
            lTinted.diffuseRemapMin = Vector4.zero;
            lTinted.diffuseRemapMax = new Vector4(pTint.r, pTint.g, pTint.b, 1f);
            UnityEditor.EditorUtility.SetDirty(lTinted);
            _tinted.Add(lTinted);
            return lTinted;
        }

        private static readonly System.Collections.Generic.HashSet<TerrainLayer> _tinted = new System.Collections.Generic.HashSet<TerrainLayer>();

        /// <summary>Clears any albedo remap left on the shared layers (older builds tinted them in place).</summary>
        private static TerrainLayer[] Untinted(TerrainLayer[] pLayers)
        {
            if (pLayers == null) return null;
            foreach (TerrainLayer lLayer in pLayers)
            {
                if (lLayer == null || _tinted.Contains(lLayer)) continue;
                if (lLayer.diffuseRemapMax == Vector4.one && lLayer.diffuseRemapMin == Vector4.zero) continue;
                lLayer.diffuseRemapMin = Vector4.zero;
                lLayer.diffuseRemapMax = Vector4.one;
                UnityEditor.EditorUtility.SetDirty(lLayer);
            }
            return pLayers;
        }

        /// <summary>A POI on the pause map.</summary>
        protected void Poi(Vector3 pPosition, string pLabel, MapPoi.Kind pKind) => A.Poi(pPosition, pLabel, pKind);
    }
}
