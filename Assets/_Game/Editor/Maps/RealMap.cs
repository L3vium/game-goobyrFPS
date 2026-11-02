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
        private readonly List<ArchResult> _buildings = new List<ArchResult>();
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
            K.LandLayers = Own(Untinted(Layers()));
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
            KeyLoops();
            Errands();
            K.AfterNavMesh = CrowdExits;
            TopUpThrowables(160);
            K.LandHoles(HoleAt);
            PlantTallGrass();
            Details();
            Vegetate();
            CliffSkin();
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
            _buildings.Add(r);
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

        /// <summary>Where a hiding field of tall grass may grow (a map with snow keeps them to its pastures).</summary>
        protected virtual bool TallGrassAllowed(float pX, float pZ) => true;

        private void PlantTallGrass()
        {
            System.Random lRandom = new System.Random(K.Map.sceneName.GetHashCode() + 77);
            List<(Vector2 point, float score)> lCandidates = new List<(Vector2, float)>();
            for (float x = -HALF + 30f; x < HALF - 30f; x += 9f)
                for (float z = -HALF + 30f; z < HALF - 30f; z += 9f)
                {
                    Vector2 p = new Vector2(x + (float)lRandom.NextDouble() * 5f, z + (float)lRandom.NextDouble() * 5f);
                    if (Built(p.x, p.y) || K.GroundAt(p.x, p.y) < WaterLevel + 0.8f || K.LandSlope(p.x, p.y) > 20f || !TallGrassAllowed(p.x, p.y)) continue;
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
                        else if (lSlope >= 52f && lRandom.NextDouble() < 0.2) CliffBoulder(CliffRock(lRandom), px, pz, 4f + (float)lRandom.NextDouble() * 6f, lRandom);
                        continue;
                    }
                    float lWoods = Woods(px, pz, h);
                    float lRoll = (float)lRandom.NextDouble();
                    if (lRoll > lWoods) continue;
                    string lPiece = TreeAt(px, pz, h, lRoll / Mathf.Max(0.01f, lWoods), lRandom, out float lScale);
                    if (!string.IsNullOrEmpty(lPiece)) K.LandTree(lPiece, px, pz, lScale);
                }
        }

        // ---------------------------------------------------------------- patrols and exits

        /// <summary>
        /// A patrol beat through town (the AI placement API): <paramref name="pSize"/> guards walking a loop
        /// of world points (6-10 along roads and squares), pausing <paramref name="pPause"/> s at plain
        /// stops. The errands follow a street beat: a pair stops to chat at the first corner, one has a
        /// smoke or takes a call half-way, and every other corner is a look around.
        /// </summary>
        protected PatrolRoute Beat(string pName, int pZone, int pSize, Data.ItemKind pWeapon, float pPause, params Vector3[] pPoints)
        {
            if (pPoints == null || pPoints.Length < 2) return null;
            int lHash = 7;
            foreach (char ch in pName) lHash = lHash * 31 + ch;
            int lBreak = pPoints.Length / 2;
            PatrolPoint[] lPoints = new PatrolPoint[pPoints.Length];
            for (int i = 0; i < pPoints.Length; i++)
            {
                if (i == 0) lPoints[i] = AiPlacement.At(pPoints[i], pSize >= 2 ? PatrolAction.Chat : PatrolAction.LookAround, pSize >= 2 ? 10f : 6f);
                else if (i == lBreak) lPoints[i] = AiPlacement.At(pPoints[i], (lHash & 1) == 0 ? PatrolAction.Smoke : PatrolAction.Phone, 10f);
                else if (i % 2 == 0) lPoints[i] = AiPlacement.At(pPoints[i], PatrolAction.LookAround, 5f);
                else lPoints[i] = pPoints[i];
            }
            return Beat(pName, pZone, pSize, pWeapon, pPause, lPoints);
        }

        /// <summary>A patrol on waypoints that already carry their errands; the route is named Route_<paramref name="pName"/>.</summary>
        protected PatrolRoute Beat(string pName, int pZone, int pSize, Data.ItemKind pWeapon, float pPause, params PatrolPoint[] pPoints)
        {
            PatrolRoute lRoute = AiPlacement.Patrol(K.Markers, pZone, pSize, pWeapon, pPause, pPoints);
            lRoute.name = "Route_" + pName;
            return lRoute;
        }

        /// <summary>
        /// A loop through a building: (x, floor, z) stops in building coordinates. Each stop does what the
        /// room it is in calls for (<see cref="PatrolPlanner.ErrandFor"/>: a smoke in the guard room, a
        /// chat in the kitchen, a watch at the vault, a lean on the terrace); corridors are walked with a
        /// look round every third stop, and a stop close to a locked door tries it.
        /// </summary>
        protected PatrolRoute Loop(ArchResult b, string pName, int pZone, int pSize, Data.ItemKind pWeapon, float pPause, params Vector3[] pLocal)
        {
            PatrolPoint[] lPoints = new PatrolPoint[pLocal.Length];
            for (int i = 0; i < pLocal.Length; i++)
            {
                int f = Mathf.RoundToInt(pLocal[i].y);
                Vector2 lAt = new Vector2(pLocal[i].x, pLocal[i].z);
                Vector3 lWorld = b.At(lAt.x, f, lAt.y, 0.05f);
                lPoints[i] = lWorld;
                ArchDoor lLocked = b.spec.doors.Find(d => d.floor == f && d.kind == DoorKind.Secure && !d.npcPass && Vector2.Distance(d.at, lAt) < 3.5f);
                if (lLocked != null)
                {
                    Vector3 lDoor = b.At(lLocked.at.x, f, lLocked.at.y, 0.05f);
                    Vector3 lAway = lWorld - lDoor;
                    lAway.y = 0f;
                    if (lAway.sqrMagnitude > 0.04f)
                    {
                        lAway.Normalize();
                        lPoints[i] = AiPlacement.At(lDoor + lAway * 1.1f, PatrolAction.CheckDoor, 4f, Quaternion.LookRotation(-lAway).eulerAngles.y);
                        continue;
                    }
                }
                ArchRoom lRoom = b.spec.rooms.Find(r => r.floor == f && !r.isVoid && r.rect.Contains(lAt));
                PatrolAction lErrand = lRoom != null ? PatrolPlanner.ErrandFor(lRoom.type) : PatrolAction.LookAround;
                if (lErrand == PatrolAction.Chat && pSize < 2) lErrand = PatrolAction.LookAround;
                if (lErrand == PatrolAction.Pause) { if (i % 3 == 0) lPoints[i] = AiPlacement.At(lWorld, PatrolAction.LookAround, 5f); }
                else lPoints[i] = AiPlacement.At(lWorld, lErrand, lErrand == PatrolAction.Smoke ? 12f : lErrand == PatrolAction.Chat ? 10f : lErrand == PatrolAction.LookAround ? 5f : 8f);
            }
            return Beat(b.spec.name + "_" + pName, pZone, pSize, pWeapon, pPause, lPoints);
        }

        /// <summary>
        /// A way off the map (a gate, a dock, a bus stop, a trail) for every zone's crowd: the AI's
        /// <see cref="ExitPoint"/>, named ExitPoint_label, facing away from the map's middle.
        /// </summary>
        protected ExitPoint ExitAt(Vector3 pPosition, string pLabel)
        {
            Vector3 lOut = new Vector3(pPosition.x, 0f, pPosition.z);
            ExitPoint lExit = AiPlacement.Exit(K.Markers, pPosition, lOut.sqrMagnitude > 1f ? Quaternion.LookRotation(lOut).eulerAngles.y : 0f, -1);
            lExit.name = "ExitPoint_" + pLabel;
            return lExit;
        }

        /// <summary>
        /// Every guarded building without a loop of its own (3+ posts inside, no route mostly inside it)
        /// gets one from <see cref="PatrolPlanner.InteriorLoop"/> on its busiest floor, walked by one more
        /// guard of its zone - no guarded house is a set of statues.
        /// </summary>
        private void KeyLoops()
        {
            NpcSpawnPoint[] lSpawns = Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None);
            PatrolRoute[] lRoutes = Object.FindObjectsByType<PatrolRoute>(FindObjectsSortMode.None);
            int lAdded = 0;
            foreach (ArchResult b in _buildings)
            {
                if (b.volume == null || b.spec.shut) continue;
                Dictionary<int, int> lPerFloor = new Dictionary<int, int>();
                int lPosts = 0;
                foreach (NpcSpawnPoint lSpawn in lSpawns)
                {
                    if (lSpawn.Role == NpcRole.Worker || !b.volume.Contains(lSpawn.transform.position + Vector3.up)) continue;
                    lPosts++;
                    int f = Mathf.RoundToInt((b.root.InverseTransformPoint(lSpawn.transform.position).y) / b.spec.storey);
                    lPerFloor[f] = lPerFloor.TryGetValue(f, out int n) ? n + 1 : 1;
                }
                if (lPosts < PatrolAudit.KEY_POSTS) continue;
                bool lHasLoop = false;
                foreach (PatrolRoute lRoute in lRoutes)
                {
                    int lInside = 0;
                    foreach (Transform lPoint in lRoute.transform) if (b.volume.Contains(lPoint.position + Vector3.up)) lInside++;
                    if (lRoute.transform.childCount > 1 && lInside * 2 > lRoute.transform.childCount) { lHasLoop = true; break; }
                }
                if (lHasLoop) continue;
                List<int> lFloors = new List<int>(lPerFloor.Keys);
                lFloors.Sort((u, v) => lPerFloor[v].CompareTo(lPerFloor[u]));
                for (int f = b.spec.MinFloor; f <= b.spec.MaxFloor; f++) if (!lFloors.Contains(f)) lFloors.Add(f);
                PatrolPoint[] lLoop = null;
                foreach (int f in lFloors) { lLoop = PatrolPlanner.InteriorLoop(b, f, 6); if (lLoop != null) break; }
                if (lLoop == null)
                {
                    // One room: pace it, corner to corner, looking round.
                    int lFloor = lFloors.Count > 0 ? lFloors[0] : 0;
                    ArchRoom lBig = null;
                    foreach (ArchRoom lRoom in b.spec.rooms)
                        if (lRoom.floor == lFloor && !lRoom.isVoid && !lRoom.noFloor && (lBig == null || lRoom.rect.width * lRoom.rect.height > lBig.rect.width * lBig.rect.height)) lBig = lRoom;
                    if (lBig == null || lBig.rect.width < 2.6f || lBig.rect.height < 2.6f) { Debug.LogWarning($"[Build] {b.spec.name}: {lPosts} posts but no room to pace for a patrol loop"); continue; }
                    Rect q = new Rect(lBig.rect.xMin + 1.1f, lBig.rect.yMin + 1.1f, lBig.rect.width - 2.2f, lBig.rect.height - 2.2f);
                    lLoop = new[]
                    {
                        AiPlacement.At(b.At(q.xMin, lFloor, q.yMin, 0.05f), PatrolAction.LookAround, 5f), (PatrolPoint)b.At(q.xMax, lFloor, q.yMin, 0.05f),
                        AiPlacement.At(b.At(q.xMax, lFloor, q.yMax, 0.05f), PatrolAction.LookAround, 5f), (PatrolPoint)b.At(q.xMin, lFloor, q.yMax, 0.05f)
                    };
                }
                Beat(b.spec.name + "_Rounds", b.spec.zone, 1, Data.ItemKind.Pistol, 3f, lLoop);
                lAdded++;
            }
            if (lAdded > 0) Debug.Log($"[Build] {K.Map.sceneName}: {lAdded} guarded buildings got an interior patrol loop");
        }

        /// <summary>
        /// Routes laid without errands (the older kit patrols round the walls and the yards) get a street
        /// beat's: a look around at the first stop and every other one, a smoke or a call half-way.
        /// </summary>
        private void Errands()
        {
            int lGiven = 0;
            foreach (PatrolRoute lRoute in Object.FindObjectsByType<PatrolRoute>(FindObjectsSortMode.None))
            {
                if (lRoute.Count < 2 || lRoute.GetComponentInChildren<PatrolStop>() != null) continue;
                int lBreak = lRoute.Count / 2;
                for (int i = 0; i < lRoute.Count; i++)
                {
                    if (i == lBreak && lRoute.Count >= 3) AiPlacement.Stop(lRoute, i, (lGiven & 1) == 0 ? PatrolAction.Smoke : PatrolAction.Phone, 10f);
                    else if (i % 2 == 0) AiPlacement.Stop(lRoute, i, PatrolAction.LookAround, 5f);
                }
                lGiven++;
            }
            if (lGiven > 0) Debug.Log($"[Build] {K.Map.sceneName}: errands given to {lGiven} older patrol routes");
        }

        /// <summary>
        /// Two ways out for every crowd (staff or guests within 15 m of each other, same zone): reachable
        /// NavMesh points 25-55 m away, outdoors, on opposite sides, public ground first - where a
        /// panicked guest runs when trouble starts (<see cref="ExitPoint"/>, after the NavMesh bake).
        /// </summary>
        private void CrowdExits()
        {
            List<NpcSpawnPoint> lCrowd = new List<NpcSpawnPoint>();
            foreach (NpcSpawnPoint lSpawn in Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None)) if (lSpawn.Role == NpcRole.Worker) lCrowd.Add(lSpawn);
            Systems.BuildingVolume[] lVolumes = Object.FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None);
            SecurityZone[] lZones = Object.FindObjectsByType<SecurityZone>(FindObjectsSortMode.None);
            List<ExitPoint> lExits = new List<ExitPoint>(Object.FindObjectsByType<ExitPoint>(FindObjectsSortMode.None));
            UnityEngine.AI.NavMeshPath lPath = new UnityEngine.AI.NavMeshPath();
            bool[] lTaken = new bool[lCrowd.Count];
            int lGroups = 0, lPlaced = 0;
            for (int i = 0; i < lCrowd.Count; i++)
            {
                if (lTaken[i]) continue;
                List<int> lGroup = new List<int> { i };
                lTaken[i] = true;
                for (int g = 0; g < lGroup.Count; g++)
                    for (int j = 0; j < lCrowd.Count; j++)
                        if (!lTaken[j] && lCrowd[j].Zone == lCrowd[i].Zone && Vector3.Distance(lCrowd[j].transform.position, lCrowd[lGroup[g]].transform.position) < 15f) { lTaken[j] = true; lGroup.Add(j); }
                if (lGroup.Count < 3) continue;
                lGroups++;
                int lZone = lCrowd[i].Zone;
                Vector3 lMiddle = Vector3.zero;
                foreach (int k in lGroup) lMiddle += lCrowd[k].transform.position;
                lMiddle /= lGroup.Count;
                // From the member closest to the middle (it stands on the NavMesh).
                Vector3 lFrom = lCrowd[lGroup[0]].transform.position;
                foreach (int k in lGroup) if ((lCrowd[k].transform.position - lMiddle).sqrMagnitude < (lFrom - lMiddle).sqrMagnitude) lFrom = lCrowd[k].transform.position;
                if (!UnityEngine.AI.NavMesh.SamplePosition(lFrom, out UnityEngine.AI.NavMeshHit lStart, 2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                // Exits this crowd already has (its zone's or anyone's) within 60 m count.
                List<Vector3> lHave = new List<Vector3>();
                foreach (ExitPoint lExit in lExits) if ((lExit.Zone < 0 || lExit.Zone == lZone) && Vector3.Distance(lExit.transform.position, lMiddle) < 60f) lHave.Add(lExit.transform.position);
                List<(Vector3 at, Vector3 dir, float score)> lCandidates = new List<(Vector3, Vector3, float)>();
                foreach (float lRadius in new[] { 28f, 40f, 52f })
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * Mathf.PI / 8f + lRadius * 0.01f;
                        Vector3 lDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        // Probe at the land's height there (a crowd upstairs runs down and out).
                        Vector3 lProbe = lMiddle + lDir * lRadius;
                        lProbe.y = Shape.Height(lProbe.x, lProbe.z) + 0.5f;
                        if (!UnityEngine.AI.NavMesh.SamplePosition(lProbe, out UnityEngine.AI.NavMeshHit lHit, 4f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                        bool lIndoors = false;
                        foreach (Systems.BuildingVolume lVolume in lVolumes) if (lVolume.Contains(lHit.position + Vector3.up)) { lIndoors = true; break; }
                        if (lIndoors) continue;
                        if (!UnityEngine.AI.NavMesh.CalculatePath(lStart.position, lHit.position, UnityEngine.AI.NavMesh.AllAreas, lPath) || lPath.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;
                        float lLength = 0f;
                        for (int c = 1; c < lPath.corners.Length; c++) lLength += Vector3.Distance(lPath.corners[c - 1], lPath.corners[c]);
                        Vector3 lFlat = lHit.position - lMiddle;
                        lFlat.y = 0f;
                        if (lLength < 20f || lLength > lFlat.magnitude * 2.8f + 20f) continue;
                        bool lPublic = TierAt(lZones, lHit.position) == SecurityTier.Public;
                        lCandidates.Add((lHit.position, lFlat.normalized, Mathf.Abs(lLength - 40f) + (lPublic ? 0f : 25f)));
                    }
                if (lCandidates.Count == 0) { Debug.LogWarning($"[Build] crowd of {lGroup.Count} at {lMiddle:F0} (zone {lZone}): no way out found"); continue; }
                lCandidates.Sort((u, v) => u.score.CompareTo(v.score));
                List<(Vector3 at, Vector3 dir)> lPicked = new List<(Vector3, Vector3)>();
                foreach (Vector3 lOld in lHave) { Vector3 d = lOld - lMiddle; d.y = 0f; if (d.sqrMagnitude > 1f) lPicked.Add((lOld, d.normalized)); }
                int lWanted = 2;
                foreach ((Vector3 lAt, Vector3 lDir, float lScore) in lCandidates)
                {
                    if (lPicked.Count >= lWanted) break;
                    int lAway = 0;
                    foreach ((Vector3 at, Vector3 dir) lOther in lPicked) if (Vector3.Angle(lOther.dir, lDir) >= 110f) lAway++;
                    bool lClose = lPicked.Exists(p => Vector3.Angle(p.dir, lDir) < 60f);
                    if (lClose) continue;
                    if (lPicked.Count > 0 && lAway == 0) continue;
                    ExitPoint lExit = AiPlacement.Exit(K.Markers, lAt, Quaternion.LookRotation(lDir).eulerAngles.y, lZone);
                    lExit.name = "ExitPoint_Crowd" + lZone + "_" + lPlaced;
                    lExits.Add(lExit);
                    lPicked.Add((lAt, lDir));
                    lPlaced++;
                }
            }
            Debug.Log($"[Build] {K.Map.sceneName}: {lPlaced} crowd exits for {lGroups} crowds");
        }

        private SecurityTier TierAt(SecurityZone[] pZones, Vector3 pPoint)
        {
            bool lInside = false;
            SecurityTier lTier = SecurityTier.Public;
            foreach (SecurityZone lZone in pZones)
            {
                if (!lZone.Contains(pPoint)) continue;
                if (!lInside || lZone.Tier > lTier) lTier = lZone.Tier;
                lInside = true;
            }
            return lInside ? lTier : OutsideTier;
        }

        // ---------------------------------------------------------------- security: turrets, terminals, cables

        /// <summary>World points of a cable run laid in a building: each (x, floor, z, height over that floor), building-local.</summary>
        protected static Vector3[] Route(ArchResult b, params Vector4[] pPoints)
        {
            Vector3[] lWorld = new Vector3[pPoints.Length];
            for (int i = 0; i < pPoints.Length; i++) lWorld[i] = b.At(pPoints[i].x, Mathf.RoundToInt(pPoints[i].y), pPoints[i].z, pPoints[i].w);
            return lWorld;
        }

        /// <summary>Points along the ground (5 cm up) from one point to another, every metre or so: a cable laid on the land.</summary>
        protected Vector3[] GroundRun(Vector3 pFrom, Vector3 pTo, float pStep = 1.2f)
        {
            int lCount = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(pFrom.x, pFrom.z), new Vector2(pTo.x, pTo.z)) / pStep));
            Vector3[] lPoints = new Vector3[lCount + 1];
            for (int i = 0; i <= lCount; i++)
            {
                Vector3 p = Vector3.Lerp(pFrom, pTo, (float)i / lCount);
                lPoints[i] = new Vector3(p.x, K.GroundAt(p.x, p.z) + 0.05f, p.z);
            }
            return lPoints;
        }

        protected static Vector3[] Join(params Vector3[][] pParts)
        {
            List<Vector3> lAll = new List<Vector3>();
            foreach (Vector3[] lPart in pParts)
                foreach (Vector3 p in lPart)
                    if (lAll.Count == 0 || (lAll[lAll.Count - 1] - p).sqrMagnitude > 0.0001f) lAll.Add(p);
            return lAll.ToArray();
        }

        /// <summary>
        /// A turret's hidden control PC on a plain desk (front +z local, against a wall), at building-local
        /// (x, floor, z) and local yaw; logs how far it is from each turret (the design wants 15 to 40 m).
        /// </summary>
        protected Systems.TurretTerminal TerminalDesk(ArchResult b, float pX, int pFloor, float pZ, float pYaw, params Systems.AutoTurret[] pTurrets)
        {
            A.Model("Furn_Desk", b.At(pX, pFloor, pZ, 0f), b.Yaw(pYaw), b.interior);
            A.Model("Furn_OfficeChair", b.At(pX, pFloor, pZ, 0f) + b.Rotation * (Quaternion.Euler(0, pYaw, 0) * new Vector3(0.2f, 0f, 0.95f)), b.Yaw(pYaw + 180f), b.interior, ArchLib.Collide.None);
            Systems.TurretTerminal lTerminal = A.Terminal(b.At(pX, pFloor, pZ, 0.79f), b.Yaw(pYaw), pTurrets);
            foreach (Systems.AutoTurret t in pTurrets)
            {
                float d = Vector3.Distance(t.transform.position, lTerminal.transform.position);
                if (d < 15f || d > 40f) Debug.LogWarning($"[Build] {K.Map.sceneName}: terminal {d:F1} m from its turret at {t.transform.position}");
            }
            return lTerminal;
        }

        /// <summary>
        /// Tops the throwables up to <paramref name="pTarget"/>: bottles, bricks, rocks, wrenches and coins
        /// dropped 2 to 3.5 m beside NPC posts and patrol stops (on a floor or the land, never inside a wall).
        /// </summary>
        private void TopUpThrowables(int pTarget)
        {
            Physics.SyncTransforms();
            int lCount = 0;
            foreach (MatchPoint p in K.Markers.GetComponentsInChildren<MatchPoint>())
                if (p.PointKind == MatchPoint.Kind.GadgetSpawn && (p.Item == Data.ItemKind.Rock || p.Item == Data.ItemKind.Bottle || p.Item == Data.ItemKind.Wrench || p.Item == Data.ItemKind.Brick || p.Item == Data.ItemKind.Coin)) lCount++;
            if (lCount >= pTarget) return;
            List<Vector3> lPosts = new List<Vector3>();
            foreach (NpcSpawnPoint s in K.Markers.GetComponentsInChildren<NpcSpawnPoint>()) lPosts.Add(s.transform.position);
            foreach (PatrolRoute r in K.Markers.GetComponentsInChildren<PatrolRoute>()) foreach (Transform t in r.transform) lPosts.Add(t.position);
            if (lPosts.Count == 0) return;
            System.Random lRandom = new System.Random(K.Map.sceneName.GetHashCode() ^ 0x7b1);
            int lAdded = 0;
            for (int k = 0; k < lPosts.Count * 6 && lCount < pTarget; k++)
            {
                Vector3 lPost = lPosts[(k * 7) % lPosts.Count];
                float a = (float)lRandom.NextDouble() * Mathf.PI * 2f, d = 2f + (float)lRandom.NextDouble() * 1.5f;
                Vector3 p = lPost + new Vector3(Mathf.Cos(a) * d, 1.2f, Mathf.Sin(a) * d);
                if (Physics.Linecast(lPost + Vector3.up * 1.2f, p, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit lHit, 3f, ~0, QueryTriggerInteraction.Ignore) || lHit.normal.y < 0.85f) continue;
                Vector3 lSpot = lHit.point + Vector3.up * 0.05f;
                if (Physics.CheckSphere(lSpot + Vector3.up * 0.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                A.Throwable(lSpot);
                lCount++;
                lAdded++;
            }
            Debug.Log($"[Build] {K.Map.sceneName}: {lAdded} throwables added beside posts ({lCount} in all)");
        }

        /// <summary>The rock texture of the cliff skin (box-projected, so it never stretches down a wall).</summary>
        protected virtual ArchMat CliffMaterial => ArchLib.Surface("cliff_side", 7f);

        /// <summary>
        /// Terrain textures are projected from above, so on a cliff they streak straight down. The
        /// steep cells (over 46°) get a thin rock skin a few centimetres over the land instead: a mesh
        /// following the ground with the land's own normals and UVs projected on the axis each face
        /// looks along (a triplanar-style mapping), in 64 m chunks, no collider (the land collides).
        /// </summary>
        private void CliffSkin()
        {
            Terrain lLand = K.Land;
            if (lLand == null) return;
            ArchMat lRock = CliffMaterial;
            const float STEP = 2f, CHUNK = 64f;
            float lTile = 1f / Mathf.Max(0.05f, lRock.tile);
            Transform lRoot = K.Group("CliffSkin", K.Env);
            Vector3 lOrigin = lLand.transform.position;
            Vector3 Normal(float x, float z) => lLand.terrainData.GetInterpolatedNormal((x - lOrigin.x) / lLand.terrainData.size.x, (z - lOrigin.z) / lLand.terrainData.size.z);
            Vector3 Point(float x, float z) => new Vector3(x, K.GroundAt(x, z), z) + Normal(x, z) * 0.07f;
            int lQuads = 0;
            for (float cx = -HALF; cx < HALF; cx += CHUNK)
                for (float cz = -HALF; cz < HALF; cz += CHUNK)
                {
                    ArchMesh lMesh = new ArchMesh();
                    bool lAny = false;
                    for (float x = cx; x < cx + CHUNK - 0.01f; x += STEP)
                        for (float z = cz; z < cz + CHUNK - 0.01f; z += STEP)
                        {
                            float lSlope = Mathf.Max(K.LandSlope(x + STEP * 0.5f, z + STEP * 0.5f), Mathf.Max(K.LandSlope(x, z), K.LandSlope(x + STEP, z + STEP)));
                            if (lSlope < 46f) continue;
                            if (HoleAt(x + STEP * 0.5f, z + STEP * 0.5f) || HoleAt(x, z) || HoleAt(x + STEP, z + STEP)) continue;
                            if (Built(x + STEP * 0.5f, z + STEP * 0.5f)) continue;
                            Vector3 a = Point(x, z), b = Point(x + STEP, z), c = Point(x + STEP, z + STEP), d = Point(x, z + STEP);
                            Vector3 lFace = Vector3.Cross(d - a, b - a).normalized;
                            Vector3 lAbs = new Vector3(Mathf.Abs(lFace.x), Mathf.Abs(lFace.y), Mathf.Abs(lFace.z));
                            Vector2 U(Vector3 p) => (lAbs.y >= lAbs.x && lAbs.y >= lAbs.z ? new Vector2(p.x, p.z) : lAbs.x >= lAbs.z ? new Vector2(lFace.x > 0 ? -p.z : p.z, p.y) : new Vector2(lFace.z > 0 ? p.x : -p.x, p.y));
                            lMesh.QuadSmooth(lRock, a, b, c, d, Normal(x, z), Normal(x + STEP, z), Normal(x + STEP, z + STEP), Normal(x, z + STEP), U(a), U(b), U(c), U(d), false);
                            lAny = true;
                            lQuads++;
                        }
                    if (lAny) lMesh.Flush(lRoot, K.Map.sceneName + "_CliffSkin_" + (int)cx + "_" + (int)cz, 0, true);
                }
            _ = lTile;
            Debug.Log($"[Build] {K.Map.sceneName}: cliff skin {lQuads} quads");
        }

        /// <summary>
        /// A big boulder half sunk into a cliff face, as a scene object rather than a terrain tree (huge
        /// terrain-tree colliders upset the physics queries of the plan baker), tilted at random.
        /// </summary>
        private void CliffBoulder(string pName, float pX, float pZ, float pScale, System.Random pRandom)
        {
            GameObject lPrefab = VegLibrary.TreePrefab(pName);
            if (lPrefab == null) return;
            if (_cliffRocks == null) _cliffRocks = K.Group("CliffRocks", K.Env);
            GameObject lRock = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(lPrefab, _cliffRocks);
            lRock.transform.SetPositionAndRotation(new Vector3(pX, K.GroundAt(pX, pZ) - pScale * 0.35f, pZ), Quaternion.Euler((float)pRandom.NextDouble() * 30f - 15f, (float)pRandom.NextDouble() * 360f, (float)pRandom.NextDouble() * 30f - 15f));
            lRock.transform.localScale = Vector3.one * pScale;
            foreach (Transform t in lRock.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        private Transform _cliffRocks;

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

        /// <summary>
        /// This map's own copy of each terrain layer (<c>TL_&lt;id&gt;_&lt;scene&gt;</c>, next to the shared one), so
        /// another map's build - which sets the shared layer's tiling - cannot change this map's ground.
        /// </summary>
        private TerrainLayer[] Own(TerrainLayer[] pLayers)
        {
            if (pLayers == null) return null;
            TerrainLayer[] lOwn = new TerrainLayer[pLayers.Length];
            for (int i = 0; i < pLayers.Length; i++)
            {
                TerrainLayer lLayer = pLayers[i];
                string lPath = lLayer != null ? UnityEditor.AssetDatabase.GetAssetPath(lLayer) : null;
                if (string.IsNullOrEmpty(lPath)) { lOwn[i] = lLayer; continue; }
                string lOwnPath = lPath.Replace(".terrainlayer", "_" + K.Map.sceneName + ".terrainlayer");
                TerrainLayer lCopy = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(lOwnPath);
                if (lCopy == null)
                {
                    lCopy = new TerrainLayer();
                    UnityEditor.AssetDatabase.CreateAsset(lCopy, lOwnPath);
                }
                UnityEditor.EditorUtility.CopySerialized(lLayer, lCopy);
                lCopy.name = System.IO.Path.GetFileNameWithoutExtension(lOwnPath);
                UnityEditor.EditorUtility.SetDirty(lCopy);
                lOwn[i] = lCopy;
            }
            return lOwn;
        }

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
