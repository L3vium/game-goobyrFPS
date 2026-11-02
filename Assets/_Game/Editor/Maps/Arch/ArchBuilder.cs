using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>A generated building: where things are, for the map builder to dress it.</summary>
    public class ArchResult
    {
        public ArchSpec spec;
        public Transform root, shell, interior, systems;
        public BuildingVolume volume;
        /// <summary>Openings per floor (local xz rectangles 1 m deep into the rooms): kept clear by furniture.</summary>
        public readonly List<(int floor, Rect rect, bool door)> keepClear = new List<(int, Rect, bool)>();
        public readonly List<(int floor, Rect rect)> used = new List<(int, Rect)>();

        public float Storey => spec.storey;
        public Vector3 World(float pX, float pY, float pZ) => root.TransformPoint(new Vector3(pX, pY, pZ));
        /// <summary>A point on a floor (local x, z), <paramref name="pUp"/> above it.</summary>
        public Vector3 At(float pX, int pFloor, float pZ, float pUp = 0.05f) => World(pX, pFloor * spec.storey + pUp, pZ);
        public Vector3 At(Vector2 p, int pFloor, float pUp = 0.05f) => At(p.x, pFloor, p.y, pUp);
        public float Yaw(float pLocal) => spec.yaw + pLocal;
        public Quaternion Rotation => root.rotation;

        public ArchRoom Room(string pName)
        {
            foreach (ArchRoom lRoom in spec.rooms) if (lRoom.name == pName) return lRoom;
            Debug.LogWarning($"[Arch] {spec.name}: no room {pName}");
            return null;
        }

        public ArchRoom RoomAt(int pFloor, Vector2 p)
        {
            foreach (ArchRoom lRoom in spec.rooms) if (lRoom.floor == pFloor && lRoom.rect.Contains(p)) return lRoom;
            return null;
        }

        /// <summary>The centre of a room on its floor.</summary>
        public Vector3 Center(string pRoom, float pUp = 0.05f)
        {
            ArchRoom r = Room(pRoom);
            return r == null ? root.position : At(r.rect.center, r.floor, pUp);
        }

        /// <summary>A point inside a room: (0,0) one corner, (1,1) the opposite one, kept 0.8 m from the walls.</summary>
        public Vector3 InRoom(string pRoom, float pU, float pV, float pUp = 0.05f)
        {
            ArchRoom r = Room(pRoom);
            if (r == null) return root.position;
            float x = Mathf.Lerp(r.rect.xMin + 0.8f, r.rect.xMax - 0.8f, pU), z = Mathf.Lerp(r.rect.yMin + 0.8f, r.rect.yMax - 0.8f, pV);
            return At(x, r.floor, z, pUp);
        }
    }

    /// <summary>
    /// Generates realistic buildings from an <see cref="ArchSpec"/>: rooms on a 0.5 m grid per floor,
    /// the walls found on the boundaries between them (two-leaf facades with stucco outside and
    /// plaster inside, thin partitions, arcades with columns and arches, terrace parapets), door and
    /// window openings with frames, deep sills, glass, shutters and casings, slabs with stair wells,
    /// straight and dogleg stairs (visible steps, smooth walkable ramps), pitched and flat roofs with
    /// eaves, cornice ledges, balconies, drainpipes, crawlable vent ducts with grilles, secure doors,
    /// lights, security zones and the building volume. One combined mesh per material.
    /// </summary>
    public static class ArchBuilder
    {
        private const float CELL = 0.5f;

        /// <summary>Prefix of the saved meshes (the map's scene name), so maps never share a building's assets.</summary>
        public static string MeshPrefix = "Arch";
        private const float PARTITION = 0.16f;

        private class Seg
        {
            public bool vertical;
            public float line, a, b;
            public int neg, pos;
            public int floor;
        }

        private struct Opening
        {
            public float s0, s1, y0, y1;
        }

        private class Ctx
        {
            public ArchSpec spec;
            public ArchResult result;
            public ArchMesh mesh;
            public int nx, nz, minF, maxF;
            public Dictionary<int, int[,]> owner = new Dictionary<int, int[,]>();
            public int[] group;
            public List<Bounds> cutters = new List<Bounds>();
            public List<Bounds> shafts = new List<Bounds>();
            public List<Seg> segs = new List<Seg>();
        }

        public static ArchResult Build(ArchSpec pSpec, Transform pParent)
        {
            Ctx c = new Ctx { spec = pSpec, mesh = new ArchMesh() };
            Transform lRoot = new GameObject(pSpec.name).transform;
            lRoot.SetParent(pParent, false);
            lRoot.SetPositionAndRotation(pSpec.origin, Quaternion.Euler(0, pSpec.yaw, 0));
            ArchResult r = new ArchResult { spec = pSpec, root = lRoot };
            r.shell = Child(lRoot, "Shell");
            r.interior = Child(lRoot, "Interior");
            r.systems = Child(lRoot, "Systems");
            c.result = r;

            c.nx = Mathf.CeilToInt(pSpec.size.x / CELL - 0.01f);
            c.nz = Mathf.CeilToInt(pSpec.size.y / CELL - 0.01f);
            c.minF = pSpec.MinFloor;
            c.maxF = pSpec.MaxFloor;
            Dictionary<string, int> lGroups = new Dictionary<string, int>();
            c.group = new int[pSpec.rooms.Count];
            for (int i = 0; i < pSpec.rooms.Count; i++)
            {
                pSpec.rooms[i].index = i;
                string lKey = pSpec.rooms[i].floor + "/" + pSpec.rooms[i].name;
                if (!lGroups.TryGetValue(lKey, out int g)) lGroups[lKey] = g = lGroups.Count;
                c.group[i] = g;
            }
            for (int f = c.minF; f <= c.maxF; f++)
            {
                int[,] o = new int[c.nx, c.nz];
                for (int i = 0; i < c.nx; i++) for (int j = 0; j < c.nz; j++) o[i, j] = -1;
                foreach (ArchRoom lRoom in pSpec.rooms)
                {
                    if (lRoom.floor != f) continue;
                    for (int i = 0; i < c.nx; i++)
                        for (int j = 0; j < c.nz; j++)
                            if (lRoom.rect.Contains(new Vector2((i + 0.5f) * CELL, (j + 0.5f) * CELL))) o[i, j] = lRoom.index;
                }
                c.owner[f] = o;
            }

            // Balconies imply French windows.
            foreach (ArchBalcony lBalcony in pSpec.balconies)
                if (!pSpec.windows.Exists(w => w.floor == lBalcony.floor && Vector2.Distance(w.at, lBalcony.at) < 0.6f))
                    pSpec.windows.Add(new ArchWindow { floor = lBalcony.floor, at = lBalcony.at, kind = WindowKind.French, width = Mathf.Min(1.4f, lBalcony.width - 0.6f), balcony = true });

            Ducts(c);
            for (int f = c.minF; f <= c.maxF; f++) Segments(c, f);
            foreach (Seg s in c.segs) Wall(c, s);
            for (int f = c.minF; f <= c.maxF; f++) Slabs(c, f);
            foreach (ArchStair s in pSpec.stairs) Stair(c, s);
            foreach (ArchRoof lRoof in pSpec.roofs) Roof(c, lRoof);
            foreach (int f in pSpec.cornices) Cornice(c, f);
            if (pSpec.stringCourses)
                for (int f = 1; f <= c.maxF; f++) if (!pSpec.cornices.Contains(f)) StringCourse(c, f);
            foreach (ArchBalcony b in pSpec.balconies) Balcony(c, b);
            foreach (ArchDrain d in pSpec.drains) Drain(c, d);
            foreach (ArchHatch h in pSpec.hatches) Hatch(c, h);
            c.mesh.Flush(r.shell, MeshPrefix + "_" + pSpec.name);

            if (!pSpec.shut)
            {
                if (pSpec.lights) Lights(c);
                if (pSpec.zones) Zones(c);
            }
            float lHeight = (c.maxF + 1 - c.minF) * pSpec.storey + 2f;
            r.volume = BuildingVolumeAt(lRoot, pSpec.name, r.World(pSpec.size.x * 0.5f, c.minF * pSpec.storey, pSpec.size.y * 0.5f), new Vector3(pSpec.size.x, lHeight, pSpec.size.y), pSpec.yaw);
            return r;
        }

        private static Transform Child(Transform pParent, string pName)
        {
            Transform t = new GameObject(pName).transform;
            t.SetParent(pParent, false);
            return t;
        }

        private static BuildingVolume BuildingVolumeAt(Transform pParent, string pName, Vector3 pCenter, Vector3 pSize, float pYaw)
        {
            GameObject lVolume = new GameObject("BuildingVolume_" + pName);
            lVolume.transform.SetParent(pParent, true);
            lVolume.transform.SetPositionAndRotation(pCenter, Quaternion.Euler(0, pYaw, 0));
            BuildingVolume lBuilding = lVolume.AddComponent<BuildingVolume>();
            lBuilding.Setup(pName, pSize);
            return lBuilding;
        }

        // ---------------------------------------------------------------- grid helpers

        private static ArchRoom RoomOf(Ctx c, int pIndex) => pIndex >= 0 ? c.spec.rooms[pIndex] : null;

        private static int Owner(Ctx c, int f, int i, int j)
        {
            if (!c.owner.TryGetValue(f, out int[,] o) || i < 0 || j < 0 || i >= c.nx || j >= c.nz) return -1;
            return o[i, j];
        }

        private static float FloorY(Ctx c, int f) => f * c.spec.storey;

        /// <summary>Greedy rectangles covering the true cells of a grid.</summary>
        private static List<Rect> Rects(bool[,] pCells, int nx, int nz)
        {
            bool[,] lUsed = new bool[nx, nz];
            List<Rect> lRects = new List<Rect>();
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (!pCells[i, j] || lUsed[i, j]) continue;
                    int w = 1;
                    while (i + w < nx && pCells[i + w, j] && !lUsed[i + w, j]) w++;
                    int h = 1;
                    bool lOk = true;
                    while (lOk && j + h < nz)
                    {
                        for (int k = 0; k < w; k++) if (!pCells[i + k, j + h] || lUsed[i + k, j + h]) { lOk = false; break; }
                        if (lOk) h++;
                    }
                    for (int a = 0; a < w; a++) for (int b = 0; b < h; b++) lUsed[i + a, j + b] = true;
                    lRects.Add(Rect.MinMaxRect(i * CELL, j * CELL, (i + w) * CELL, (j + h) * CELL));
                }
            return lRects;
        }

        // ---------------------------------------------------------------- segments and walls

        private static void Segments(Ctx c, int f)
        {
            // Vertical boundaries: x = i * CELL.
            for (int i = 0; i <= c.nx; i++)
            {
                Seg lRun = null;
                for (int j = 0; j <= c.nz; j++)
                {
                    int lNeg = j < c.nz ? Owner(c, f, i - 1, j) : -2, lPos = j < c.nz ? Owner(c, f, i, j) : -2;
                    bool lWall = j < c.nz && NeedsWall(c, lNeg, lPos);
                    if (lRun != null && (!lWall || lRun.neg != lNeg || lRun.pos != lPos)) { lRun.b = j * CELL; c.segs.Add(lRun); lRun = null; }
                    if (lWall && lRun == null) lRun = new Seg { vertical = true, line = i * CELL, a = j * CELL, neg = lNeg, pos = lPos, floor = f };
                }
            }
            // Horizontal boundaries: z = j * CELL.
            for (int j = 0; j <= c.nz; j++)
            {
                Seg lRun = null;
                for (int i = 0; i <= c.nx; i++)
                {
                    int lNeg = i < c.nx ? Owner(c, f, i, j - 1) : -2, lPos = i < c.nx ? Owner(c, f, i, j) : -2;
                    bool lWall = i < c.nx && NeedsWall(c, lNeg, lPos);
                    if (lRun != null && (!lWall || lRun.neg != lNeg || lRun.pos != lPos)) { lRun.b = i * CELL; c.segs.Add(lRun); lRun = null; }
                    if (lWall && lRun == null) lRun = new Seg { vertical = false, line = j * CELL, a = i * CELL, neg = lNeg, pos = lPos, floor = f };
                }
            }
        }

        private static bool NeedsWall(Ctx c, int pNeg, int pPos)
        {
            if (pNeg == pPos) return false;
            if (pNeg >= 0 && pPos >= 0 && c.group[pNeg] == c.group[pPos]) return false;
            ArchRoom a = RoomOf(c, pNeg), b = RoomOf(c, pPos);
            if (a == null && b == null) return false;
            // An interior void (a double-height hall's upper part) is a facade to the world, open air to its galleries.
            if (a != null && a.isVoid) { if (b == null) return true; a = null; }
            if (b != null && b.isVoid) { if (a == null) return true; b = null; }
            if (a == null && b == null) return false;
            bool aE = a != null && a.Enclosed, bE = b != null && b.Enclosed;
            if (aE || bE) return true;
            // Arcade or terrace against the outside: arcade, else parapet; an arcade on an open court: arcade.
            if (a != null && b != null) return (a.arcade && b.open) || (b.arcade && a.open);
            return true;
        }

        private enum WallKind { Partition, Facade, Arcade, Parapet, Balustrade }

        /// <summary>Kind of a boundary, and which side is "inside" (+1: the room on the negative side owns it).</summary>
        private static WallKind Classify(Ctx c, Seg s, out int pOwnerSide, out ArchRoom pOwner, out ArchRoom pOther)
        {
            ArchRoom a = RoomOf(c, s.neg), b = RoomOf(c, s.pos);
            if (a != null && a.isVoid && b != null && b.overlook) { pOwnerSide = 1; pOwner = b; pOther = null; return WallKind.Balustrade; }
            if (b != null && b.isVoid && a != null && a.overlook) { pOwnerSide = -1; pOwner = a; pOther = null; return WallKind.Balustrade; }
            if (a != null && a.isVoid && b == null) { pOwnerSide = -1; pOwner = a; pOther = null; return WallKind.Facade; }
            if (b != null && b.isVoid && a == null) { pOwnerSide = 1; pOwner = b; pOther = null; return WallKind.Facade; }
            if (a != null && a.isVoid) a = null;
            if (b != null && b.isVoid) b = null;
            bool aE = a != null && a.Enclosed, bE = b != null && b.Enclosed;
            if (aE && bE) { pOwnerSide = 0; pOwner = a; pOther = b; return WallKind.Partition; }
            if (aE) { pOwnerSide = -1; pOwner = a; pOther = b; return WallKind.Facade; }
            if (bE) { pOwnerSide = 1; pOwner = b; pOther = a; return WallKind.Facade; }
            if (a != null && b != null)
            {
                // An arcade facing an open court (a patio, a yard).
                bool lA = a.arcade;
                pOwnerSide = lA ? -1 : 1;
                pOwner = lA ? a : b;
                pOther = lA ? b : a;
                return WallKind.Arcade;
            }
            ArchRoom lRoom = a ?? b;
            pOwnerSide = a != null ? -1 : 1;
            pOwner = lRoom;
            pOther = null;
            return lRoom.arcade ? WallKind.Arcade : WallKind.Parapet;
        }

        /// <summary>
        /// A local point from segment coordinates: <paramref name="s"/> along the line, height
        /// <paramref name="y"/>, offset <paramref name="d"/> from the line toward the outside (n).
        /// </summary>
        private static Vector3 P(Seg pSeg, float n, float s, float y, float d) => pSeg.vertical ? new Vector3(pSeg.line + d * n, y, s) : new Vector3(s, y, pSeg.line + d * n);

        /// <summary>A box in segment coordinates.</summary>
        private static void SegBox(Ctx c, Seg pSeg, float n, float s0, float s1, float y0, float y1, float d0, float d1, ArchMat pMat, bool pSolid = true, bool pSkipStart = false, bool pSkipEnd = false)
        {
            if (s1 - s0 < 0.01f || y1 - y0 < 0.01f || Mathf.Abs(d1 - d0) < 0.005f) return;
            Vector3 p0 = P(pSeg, n, s0, y0, d0), p1 = P(pSeg, n, s1, y1, d1);
            // The end faces where a wall runs on into the next one are hidden inside it (and would
            // z-fight with its outer face: the white seams at corners and partitions).
            ArchMesh.Faces lSkip = ArchMesh.Faces.None;
            if (pSkipStart) lSkip |= pSeg.vertical ? ArchMesh.Faces.Back : ArchMesh.Faces.Left;
            if (pSkipEnd) lSkip |= pSeg.vertical ? ArchMesh.Faces.Front : ArchMesh.Faces.Right;
            c.mesh.BoxMinMax(pMat, Vector3.Min(p0, p1), Vector3.Max(p0, p1), pSolid, lSkip);
        }

        /// <summary>The wall finish a room shows: wallpaper for guests, concrete back of house, plaster otherwise.</summary>
        private static ArchMat InnerFor(Ctx c, ArchRoom pRoom)
        {
            ArchStyle st = c.spec.style;
            if (pRoom == null) return st.interior;
            if (IsCellar(pRoom)) return st.cellarWall ?? st.underbelly ?? st.interior;
            if (st.underbelly != null && ArchSpec.IsUnderbelly(pRoom)) return st.underbelly;
            if (st.wallpaper != null && ArchSpec.IsGuest(pRoom.type)) return st.wallpaper;
            return st.interior;
        }

        /// <summary>
        /// One room-facing leaf of a wall between <paramref name="pBack"/> and the face
        /// <paramref name="pFace"/> (segment depths): guest rooms get a panelled dado, a dado rail and a
        /// crown moulding under the ceiling.
        /// </summary>
        private static void Leaf(Ctx c, Seg s, float n, float p, float q, float ya, float yb, float pBack, float pFace, ArchRoom pRoom, bool pSkipStart, bool pSkipEnd)
        {
            ArchStyle st = c.spec.style;
            ArchMat lMat = InnerFor(c, pRoom);
            float y0 = FloorY(c, s.floor);
            bool lGuest = st.wallpaper != null && st.wainscot != null && pRoom != null && pRoom.floor >= 0 && ArchSpec.IsGuest(pRoom.type);
            float lOut = pFace > pBack ? 1f : -1f;
            if (!lGuest) { SegBox(c, s, n, p, q, ya, yb, Mathf.Min(pBack, pFace), Mathf.Max(pBack, pFace), lMat, true, pSkipStart, pSkipEnd); return; }
            float lDado = y0 + 0.95f;
            if (ya < lDado)
            {
                float lTop = Mathf.Min(yb, lDado);
                SegBox(c, s, n, p, q, ya, lTop, Mathf.Min(pBack, pFace + lOut * 0.015f), Mathf.Max(pBack, pFace + lOut * 0.015f), st.wainscot, true, pSkipStart, pSkipEnd);
                if (lTop >= lDado - 0.01f) SegBox(c, s, n, p, q, lDado - 0.03f, lDado + 0.03f, Mathf.Min(pFace, pFace + lOut * 0.035f), Mathf.Max(pFace, pFace + lOut * 0.035f), st.trim, false, pSkipStart, pSkipEnd);
            }
            if (yb > lDado) SegBox(c, s, n, p, q, Mathf.Max(ya, lDado), yb, Mathf.Min(pBack, pFace), Mathf.Max(pBack, pFace), lMat, true, pSkipStart, pSkipEnd);
            float lCeiling = y0 + c.spec.storey - c.spec.slab;
            bool lOpenAbove = c.spec.rooms.Exists(o => o.floor == pRoom.floor + 1 && (o.noFloor || o.isVoid) && o.rect.Overlaps(pRoom.rect));
            if (yb >= lCeiling - 0.01f && !pRoom.isVoid && !lOpenAbove)
                SegBox(c, s, n, p, q, lCeiling - 0.14f, lCeiling, Mathf.Min(pFace, pFace + lOut * 0.07f), Mathf.Max(pFace, pFace + lOut * 0.07f), st.trim, false, pSkipStart, pSkipEnd);
        }

        private static bool DoorOn(Seg s, int pFloor, Vector2 pAt, out float pAlong)
        {
            float lPerp = s.vertical ? pAt.x : pAt.y;
            pAlong = s.vertical ? pAt.y : pAt.x;
            return s.floor == pFloor && Mathf.Abs(lPerp - s.line) < 0.35f && pAlong > s.a + 0.2f && pAlong < s.b - 0.2f;
        }

        private static bool IsCellar(ArchRoom pRoom) => pRoom != null && pRoom.floor < 0;

        private static void Wall(Ctx c, Seg s)
        {
            ArchSpec lSpec = c.spec;
            ArchStyle st = lSpec.style;
            WallKind lKind = Classify(c, s, out int lSide, out ArchRoom lOwner, out ArchRoom lOther);
            // n points away from the owner (toward the outside) for facades; +axis for partitions.
            float n = lSide == 0 ? 1f : (lSide < 0 ? 1f : -1f);
            float y0 = FloorY(c, s.floor), H = lSpec.storey;
            if (lKind == WallKind.Arcade) { Arcade(c, s, n, lOwner); return; }
            if (lKind == WallKind.Balustrade) { Balustrade(c, s, n, s.a, s.b, y0, -0.2f, st.column ?? st.trim); return; }
            if (lKind == WallKind.Parapet) { Parapet(c, s, n, y0, lOwner.wallMat ?? st.facade); return; }

            // Openings: doors, windows, ducts.
            List<Opening> lOpenings = new List<Opening>();
            List<(ArchDoor door, float s)> lDoors = new List<(ArchDoor, float)>();
            List<(ArchWindow win, float s)> lWindows = new List<(ArchWindow, float)>();
            foreach (ArchDoor lDoor in lSpec.doors)
                if (DoorOn(s, lDoor.floor, lDoor.at, out float lAlong))
                {
                    lDoors.Add((lDoor, lAlong));
                    lOpenings.Add(new Opening { s0 = lAlong - lDoor.width * 0.5f, s1 = lAlong + lDoor.width * 0.5f, y0 = lDoor.sill, y1 = lDoor.sill + lDoor.height });
                }
            if (lKind == WallKind.Facade)
            {
                foreach (ArchWindow lWin in lSpec.windows)
                    if (DoorOn(s, lWin.floor, lWin.at, out float lAlong)) lWindows.Add((lWin, lAlong));
                if (lSpec.autoWindows && !lOwner.noWindows) AutoWindows(c, s, lOwner, lDoors, lWindows);
                foreach ((ArchWindow w, float sa) in lWindows)
                {
                    WindowGeometry(w, lSpec.storey, out float lBottom, out float lTop);
                    lOpenings.Add(new Opening { s0 = sa - w.width * 0.5f, s1 = sa + w.width * 0.5f, y0 = lBottom, y1 = lTop });
                }
            }
            foreach (Bounds lCut in c.cutters)
            {
                float lPerpMin = s.vertical ? lCut.min.x : lCut.min.z, lPerpMax = s.vertical ? lCut.max.x : lCut.max.z;
                float lAlongMin = s.vertical ? lCut.min.z : lCut.min.x, lAlongMax = s.vertical ? lCut.max.z : lCut.max.x;
                if (s.line < lPerpMin - 0.3f || s.line > lPerpMax + 0.3f || lAlongMax < s.a || lAlongMin > s.b) continue;
                if (lCut.max.y < y0 || lCut.min.y > y0 + H) continue;
                lOpenings.Add(new Opening { s0 = lAlongMin, s1 = lAlongMax, y0 = lCut.min.y - y0, y1 = lCut.max.y - y0 });
            }

            float lBase = 0f;
            if (lKind == WallKind.Facade && s.floor == c.minF) lBase = -c.spec.plinth;
            ArchMat lOuter = IsCellar(lOwner) ? (st.cellarWall ?? st.facade) : (lOwner.wallMat ?? st.facade);
            // Top floor: walls rise a little into the roof (no gap under the eaves).
            float lTopExtra = 0f;

            // Spans between opening boundaries; in each, the wall is the complement of the openings.
            List<float> lCuts = new List<float> { s.a, s.b };
            foreach (Opening o in lOpenings) { lCuts.Add(Mathf.Clamp(o.s0, s.a, s.b)); lCuts.Add(Mathf.Clamp(o.s1, s.a, s.b)); }
            lCuts.Sort();
            float lT = lKind == WallKind.Facade ? st.outerThickness : PARTITION;
            for (int k = 0; k < lCuts.Count - 1; k++)
            {
                float p = lCuts[k], q = lCuts[k + 1];
                if (q - p < 0.01f) continue;
                float lMid = (p + q) * 0.5f;
                List<Vector2> lBlocked = new List<Vector2>();
                foreach (Opening o in lOpenings) if (lMid > o.s0 && lMid < o.s1) lBlocked.Add(new Vector2(Mathf.Max(0f, o.y0), Mathf.Min(H, o.y1)));
                lBlocked.Sort((u, v) => u.x.CompareTo(v.x));
                float lCursor = lBase;
                List<Vector2> lSolid = new List<Vector2>();
                foreach (Vector2 b in lBlocked)
                {
                    if (b.x > lCursor) lSolid.Add(new Vector2(lCursor, b.x));
                    lCursor = Mathf.Max(lCursor, b.y);
                }
                if (H + lTopExtra > lCursor) lSolid.Add(new Vector2(lCursor, H + lTopExtra));
                bool lSkipA = p <= s.a + 0.001f, lSkipB = q >= s.b - 0.001f;
                foreach (Vector2 v in lSolid)
                {
                    if (lKind == WallKind.Partition)
                    {
                        // Two leaves, each finished for the room it faces.
                        Leaf(c, s, n, p, q, y0 + v.x, y0 + v.y, 0f, -PARTITION * 0.5f, RoomOf(c, s.neg), lSkipA, lSkipB);
                        Leaf(c, s, n, p, q, y0 + v.x, y0 + v.y, 0f, PARTITION * 0.5f, RoomOf(c, s.pos), lSkipA, lSkipB);
                    }
                    else
                    {
                        SegBox(c, s, n, p, q, y0 + v.x, y0 + v.y, -lT + 0.15f, 0f, lOuter, true, lSkipA, lSkipB);
                        if (lOwner.isVoid || lOwner.open) SegBox(c, s, n, p, q, y0 + Mathf.Max(0f, v.x), y0 + v.y, -lT, -lT + 0.15f, InnerFor(c, lOwner), true, lSkipA, lSkipB);
                        else Leaf(c, s, n, p, q, y0 + Mathf.Max(0f, v.x), y0 + v.y, -lT + 0.15f, -lT, lOwner, lSkipA, lSkipB);
                    }
                }
            }

            // Plinth course along the foot of the facades.
            if (lKind == WallKind.Facade && s.floor == Mathf.Max(0, c.minF) && st.plinth != null && s.floor == 0)
            {
                float lPlinthTop = 0.55f;
                List<Opening> lDoorOpenings = lOpenings.FindAll(o => o.y0 < 0.1f);
                float lCursorS = s.a - 0.06f;
                lDoorOpenings.Sort((u, v) => u.s0.CompareTo(v.s0));
                foreach (Opening o in lDoorOpenings)
                {
                    SegBox(c, s, n, lCursorS, o.s0, y0 - c.spec.plinth, y0 + lPlinthTop, -0.05f, 0.05f, st.plinth);
                    lCursorS = o.s1;
                }
                SegBox(c, s, n, lCursorS, s.b + 0.06f, y0 - c.spec.plinth, y0 + lPlinthTop, -0.05f, 0.05f, st.plinth);
            }

            foreach ((ArchDoor d, float sa) in lDoors) DoorGeometry(c, s, n, lKind, d, sa, y0, lOwner, lOther);
            foreach ((ArchWindow w, float sa) in lWindows) WindowFrame(c, s, n, w, sa, y0, lOwner);
        }

        private static void WindowGeometry(ArchWindow w, float pStorey, out float pBottom, out float pTop)
        {
            switch (w.kind)
            {
                case WindowKind.French: pBottom = 0f; pTop = Mathf.Min(pStorey - 0.75f, 2.7f); break;
                case WindowKind.Small: pBottom = 1.5f; pTop = 2.4f; break;
                case WindowKind.Slit: pBottom = 1.2f; pTop = 2.6f; break;
                default: pBottom = 0.9f; pTop = Mathf.Min(pStorey - 0.75f, 2.65f); break;
            }
        }

        private static void AutoWindows(Ctx c, Seg s, ArchRoom pRoom, List<(ArchDoor, float)> pDoors, List<(ArchWindow, float)> pWindows)
        {
            if (pRoom.floor < 0) return;
            RoomType t = pRoom.type;
            if (t == RoomType.Vault || t == RoomType.Store || t == RoomType.Security || t == RoomType.ColdRoom || t == RoomType.CoalStore || t == RoomType.Crypt || t == RoomType.Plant || t == RoomType.Armoury) return;
            float lSpacing = pRoom.windowWidth > 0f ? pRoom.windowWidth + 1.2f : c.spec.style.windowSpacing;
            float lLength = s.b - s.a;
            int lCount = Mathf.FloorToInt((lLength - 0.8f) / lSpacing);
            if (lCount < 1)
            {
                if (lLength < 2.2f) return;
                lCount = 1;
            }
            WindowKind lKind = t == RoomType.Bath ? WindowKind.Small : t == RoomType.Hangar || t == RoomType.Garage || t == RoomType.Workshop ? WindowKind.Small : WindowKind.Glazed;
            if (t == RoomType.Church || t == RoomType.Chapel) lKind = WindowKind.Slit;
            if (c.spec.shut) lKind = WindowKind.Shuttered;
            for (int k = 0; k < lCount; k++)
            {
                float sa = s.a + lLength * (k + 0.5f) / lCount;
                bool lClash = false;
                foreach ((ArchDoor d, float ds) in pDoors) if (Mathf.Abs(ds - sa) < d.width * 0.5f + 0.9f) lClash = true;
                foreach ((ArchWindow w, float ws) in pWindows) if (Mathf.Abs(ws - sa) < w.width * 0.5f + 0.9f) lClash = true;
                if (sa - s.a < 0.9f || s.b - sa < 0.9f) lClash = true;
                if (lClash) continue;
                float lWidth = pRoom.windowWidth > 0f ? pRoom.windowWidth : lKind == WindowKind.Slit ? 0.7f : lKind == WindowKind.Small ? 0.9f : 1.2f;
                pWindows.Add((new ArchWindow { floor = s.floor, at = Vector2.zero, kind = lKind, width = lWidth }, sa));
            }
        }

        private static void WindowFrame(Ctx c, Seg s, float n, ArchWindow w, float sa, float y0, ArchRoom pRoom)
        {
            ArchStyle st = c.spec.style;
            float lT = st.outerThickness;
            WindowGeometry(w, c.spec.storey, out float lBottom, out float lTop);
            float s0 = sa - w.width * 0.5f, s1 = sa + w.width * 0.5f, b = y0 + lBottom, t = y0 + lTop;
            float fd0 = -lT * 0.55f - 0.05f, fd1 = -lT * 0.55f + 0.05f;
            ArchMat lFrame = st.frame ?? st.trim;
            // Frame.
            SegBox(c, s, n, s0, s0 + 0.07f, b, t, fd0, fd1, lFrame, false);
            SegBox(c, s, n, s1 - 0.07f, s1, b, t, fd0, fd1, lFrame, false);
            SegBox(c, s, n, s0, s1, t - 0.07f, t, fd0, fd1, lFrame, false);
            if (lBottom > 0.05f) SegBox(c, s, n, s0, s1, b, b + 0.06f, fd0, fd1, lFrame, false);
            bool lGlass = w.kind == WindowKind.Glazed || w.kind == WindowKind.French || w.kind == WindowKind.Small || w.kind == WindowKind.Slit;
            if (w.kind != WindowKind.Boarded && w.kind != WindowKind.Shuttered)
            {
                SegBox(c, s, n, sa - 0.03f, sa + 0.03f, b, t, fd0 + 0.01f, fd1 - 0.01f, lFrame, false);
                SegBox(c, s, n, s0, s1, b + (t - b) * 0.62f, b + (t - b) * 0.62f + 0.05f, fd0 + 0.01f, fd1 - 0.01f, lFrame, false);
            }
            if (lGlass) SegBox(c, s, n, s0 + 0.05f, s1 - 0.05f, b + 0.03f, t - 0.05f, -lT * 0.55f - 0.012f, -lT * 0.55f + 0.012f, ArchLib.Glass, true);
            // A deep stone sill (a hangable ledge) and the surround.
            if (lBottom > 0.05f)
                SegBox(c, s, n, s0 - 0.1f, s1 + 0.1f, b - 0.07f, b, -lT - 0.02f, 0.14f, st.trim);
            SegBox(c, s, n, s0 - 0.16f, s1 + 0.16f, t, t + 0.2f, -0.01f, 0.05f, st.trim);
            SegBox(c, s, n, s0 - 0.14f, s0, b, t, -0.01f, 0.035f, st.trim);
            SegBox(c, s, n, s1, s1 + 0.14f, b, t, -0.01f, 0.035f, st.trim);
            // Shutters: open flat on the facade, or shut in the opening.
            if (st.shutters && st.shutter != null && w.kind != WindowKind.Slit && w.kind != WindowKind.Small)
            {
                float lLeaf = w.width * 0.5f;
                if (w.kind == WindowKind.Shuttered)
                {
                    SegBox(c, s, n, s0, s1, b, t, -0.09f, -0.04f, st.shutter, true);
                    for (float y = b + 0.15f; y < t - 0.1f; y += 0.16f) SegBox(c, s, n, s0, s1, y, y + 0.05f, -0.04f, -0.02f, st.shutter, false);
                }
                else
                {
                    SegBox(c, s, n, s0 - 0.16f - lLeaf, s0 - 0.16f, b, t, 0.035f, 0.075f, st.shutter, false);
                    SegBox(c, s, n, s1 + 0.16f, s1 + 0.16f + lLeaf, b, t, 0.035f, 0.075f, st.shutter, false);
                }
            }
            if (w.kind == WindowKind.Boarded)
                for (int k = 0; k < 3; k++) SegBox(c, s, n, s0 - 0.08f, s1 + 0.08f, b + 0.2f + k * 0.5f, b + 0.4f + k * 0.5f, 0.0f, 0.04f, st.door ?? st.trim, true);
            if (w.balcony == false && w.kind == WindowKind.French) RailBetween(c, s, n, s0, s1, b, 0.02f);
            if (w.kind == WindowKind.Open || w.kind == WindowKind.Glazed || w.kind == WindowKind.French || w.kind == WindowKind.Boarded) c.result.keepClear.Add((s.floor, ClearRect(s, n, s0, s1, lT, 0.6f), false));
            if (!string.IsNullOrEmpty(w.poi)) Poi(c, P(s, n, sa, b + 0.5f, 0.3f), w.poi, MapPoi.Kind.Entrance);
        }

        /// <summary>The floor area in front of an opening, inside the room (local xz).</summary>
        private static Rect ClearRect(Seg s, float n, float s0, float s1, float pThickness, float pDepth)
        {
            Vector3 a = P(s, n, s0, 0, -pThickness), b = P(s, n, s1, 0, -pThickness - pDepth);
            Vector3 a2 = P(s, n, s0, 0, pThickness), b2 = P(s, n, s1, 0, pThickness + pDepth);
            float x0 = Mathf.Min(a.x, b.x, a2.x, b2.x), x1 = Mathf.Max(a.x, b.x, a2.x, b2.x), z0 = Mathf.Min(a.z, b.z, a2.z, b2.z), z1 = Mathf.Max(a.z, b.z, a2.z, b2.z);
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        /// <summary>A low iron rail across an opening (a French window without a balcony).</summary>
        private static void RailBetween(Ctx c, Seg s, float n, float s0, float s1, float y, float d)
        {
            SegBox(c, s, n, s0, s1, y + 0.95f, y + 1.0f, d, d + 0.04f, ArchLib.Iron);
            for (float k = s0 + 0.06f; k < s1; k += 0.13f) SegBox(c, s, n, k, k + 0.02f, y, y + 0.95f, d + 0.01f, d + 0.03f, ArchLib.Iron, false);
        }

        private static void DoorGeometry(Ctx c, Seg s, float n, WallKind pKind, ArchDoor d, float sa, float y0, ArchRoom pOwner, ArchRoom pOther)
        {
            ArchStyle st = c.spec.style;
            float lT = pKind == WallKind.Facade ? st.outerThickness : PARTITION;
            float dIn = pKind == WallKind.Facade ? -lT : -PARTITION * 0.5f, dOut = pKind == WallKind.Facade ? 0f : PARTITION * 0.5f;
            float s0 = sa - d.width * 0.5f, s1 = sa + d.width * 0.5f, b = y0 + d.sill, t = y0 + d.sill + d.height;
            ArchMat lCasing = st.frame ?? st.trim;
            // Casings on both faces.
            foreach (float lFace in new[] { dIn - 0.025f, dOut + 0.025f })
            {
                float f0 = Mathf.Min(lFace, lFace + (lFace < 0 ? 0.025f : -0.025f)), f1 = Mathf.Max(lFace, lFace + (lFace < 0 ? 0.025f : -0.025f));
                SegBox(c, s, n, s0 - 0.1f, s0, b, t + 0.1f, f0, f1, lCasing, false);
                SegBox(c, s, n, s1, s1 + 0.1f, b, t + 0.1f, f0, f1, lCasing, false);
                SegBox(c, s, n, s0 - 0.1f, s1 + 0.1f, t, t + 0.1f, f0, f1, lCasing, false);
            }
            if (pKind == WallKind.Facade && d.sill < 0.05f)
            {
                // Threshold step and a stone surround outside.
                SegBox(c, s, n, s0 - 0.05f, s1 + 0.05f, y0 - 0.15f, y0 + 0.02f, -lT, 0.25f, st.trim);
                SegBox(c, s, n, s0 - 0.22f, s1 + 0.22f, t, t + 0.28f, -0.01f, 0.06f, st.trim);
            }
            c.result.keepClear.Add((s.floor, ClearRect(s, n, s0 - 0.3f, s1 + 0.3f, lT, 1.2f), true));
            ArchMat lLeafMat = st.door ?? st.trim;
            if (c.spec.shut && d.kind != DoorKind.Secure)
            {
                // A shut house: the door is closed.
                float lMid = pKind == WallKind.Facade ? -lT * 0.5f : 0f;
                SegBox(c, s, n, s0 + 0.02f, s1 - 0.02f, b, t - 0.02f, lMid - 0.04f, lMid + 0.04f, lLeafMat, true);
                return;
            }
            if (d.kind == DoorKind.Secure) { SecureLeaf(c, s, n, d, s0, s1, b, t, -lT * 0.5f + (pKind == WallKind.Partition ? lT * 0.5f : 0f), lLeafMat, lT); }
            else if (d.kind == DoorKind.Door || d.kind == DoorKind.Double)
            {
                // Leaves swung open into the owner room (perpendicular to the wall, against the jamb).
                float lInside = pKind == WallKind.Facade ? -lT : -PARTITION * 0.5f;
                float lDir = -1f;
                if (d.kind == DoorKind.Door)
                {
                    float w = d.width - 0.06f;
                    SegBox(c, s, n, s0 + 0.02f, s0 + 0.07f, b + 0.01f, t - 0.02f, lInside + lDir * w, lInside, lLeafMat, false);
                }
                else
                {
                    float w = d.width * 0.5f - 0.04f;
                    SegBox(c, s, n, s0 + 0.02f, s0 + 0.07f, b + 0.01f, t - 0.02f, lInside + lDir * w, lInside, lLeafMat, false);
                    SegBox(c, s, n, s1 - 0.07f, s1 - 0.02f, b + 0.01f, t - 0.02f, lInside + lDir * w, lInside, lLeafMat, false);
                }
            }
            if (!string.IsNullOrEmpty(d.poi)) Poi(c, P(s, n, sa, b + 1f, 0.6f), d.poi, MapPoi.Kind.Entrance);
        }

        /// <summary>A shut door that needs a tool: hinge, leaf (interactable), keypad, NavMesh obstacle.</summary>
        private static void SecureLeaf(Ctx c, Seg s, float n, ArchDoor d, float s0, float s1, float b, float t, float dMid, ArchMat pMat, float pThickness)
        {
            Transform lRoot = c.result.root;
            Vector3 lNormal = (s.vertical ? Vector3.right : Vector3.forward) * n;
            float w = s1 - s0 - 0.06f, h = t - b - 0.03f;
            // Door frame: +z the outward normal, +x along the opening.
            GameObject lDoorObject = new GameObject("SecureDoor_" + d.lockKind) { layer = GameLayers.INTERACTABLE };
            lDoorObject.transform.SetParent(c.result.systems, false);
            lDoorObject.transform.localPosition = P(s, n, (s0 + s1) * 0.5f, b, dMid);
            lDoorObject.transform.localRotation = Quaternion.LookRotation(lNormal, Vector3.up);
            Transform lHinge = new GameObject("Hinge").transform;
            lHinge.SetParent(lDoorObject.transform, false);
            lHinge.localPosition = new Vector3(-w * 0.5f, 0f, 0f);
            lHinge.gameObject.layer = GameLayers.INTERACTABLE;
            lHinge.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            GameObject lLeaf = new GameObject("Leaf") { layer = GameLayers.INTERACTABLE };
            lLeaf.transform.SetParent(lHinge, false);
            lLeaf.transform.localPosition = new Vector3(w * 0.5f, h * 0.5f, 0f);
            lLeaf.transform.localScale = new Vector3(w, h, 0.06f);
            lLeaf.AddComponent<MeshFilter>().sharedMesh = ArchLib.UnitCube;
            bool lMetal = d.lockKind == SecureDoor.Lock.Keycard || d.lockKind == SecureDoor.Lock.Keypad;
            lLeaf.AddComponent<MeshRenderer>().sharedMaterial = (lMetal ? ArchLib.Surface("painted_metal_shutter", 1.5f) : pMat).material;
            lLeaf.AddComponent<BoxCollider>();
            if (d.lockKind == SecureDoor.Lock.Crowbar)
                for (int k = 0; k < 3; k++)
                {
                    GameObject lPlank = new GameObject("Plank") { layer = GameLayers.INTERACTABLE };
                    lPlank.transform.SetParent(lHinge, false);
                    lPlank.transform.localPosition = new Vector3(w * 0.5f, h * (0.25f + k * 0.25f), 0.06f);
                    lPlank.transform.localRotation = Quaternion.Euler(0, 0, k == 1 ? 9f : -8f);
                    lPlank.transform.localScale = new Vector3(w + 0.25f, 0.16f, 0.04f);
                    lPlank.AddComponent<MeshFilter>().sharedMesh = ArchLib.UnitCube;
                    lPlank.AddComponent<MeshRenderer>().sharedMaterial = ArchLib.Surface("old_planks_02", 1.5f).material;
                }
            if (d.lockKind == SecureDoor.Lock.Padlock)
            {
                GameObject lLock = new GameObject("Padlock") { layer = GameLayers.INTERACTABLE };
                lLock.transform.SetParent(lHinge, false);
                lLock.transform.localPosition = new Vector3(w - 0.12f, 1.05f, 0.06f);
                lLock.transform.localScale = new Vector3(0.07f, 0.09f, 0.03f);
                lLock.AddComponent<MeshFilter>().sharedMesh = ArchLib.UnitCube;
                lLock.AddComponent<MeshRenderer>().sharedMaterial = ArchLib.Brass.material;
            }
            SecureDoor lSecure = lDoorObject.AddComponent<SecureDoor>();
            lSecure.Setup(d.lockKind, lHinge);
            if (!d.npcPass)
            {
                NavMeshObstacle lObstacle = lDoorObject.AddComponent<NavMeshObstacle>();
                lObstacle.carving = true;
                lObstacle.shape = NavMeshObstacleShape.Box;
                lObstacle.center = new Vector3(0, h * 0.5f, 0);
                lObstacle.size = new Vector3(w + 0.2f, h, 0.6f);
            }
            if (lMetal)
                foreach (float lFace in new[] { 1f, -1f })
                {
                    Vector3 lPanel = lDoorObject.transform.TransformPoint(new Vector3(w * 0.5f + 0.32f, 1.1f, lFace * (pThickness * 0.5f + 0.005f)));
                    float lYaw = Quaternion.LookRotation(lDoorObject.transform.forward * lFace).eulerAngles.y;
                    ArchLib.Place("Prop_AlarmPanel", c.result.systems, lPanel, lYaw, ArchLib.Collide.None, ArchLib.Iron);
                }
            if (!string.IsNullOrEmpty(d.poi)) Poi(c, P(s, n, (s0 + s1) * 0.5f, b + 1f, 0.6f), d.poi, MapPoi.Kind.Entrance);
        }

        // ---------------------------------------------------------------- arcades and parapets

        private static void Arcade(Ctx c, Seg s, float n, ArchRoom pRoom)
        {
            ArchStyle st = c.spec.style;
            ArchMat lColumn = st.column ?? st.trim, lWall = pRoom.wallMat ?? st.facade;
            float y0 = FloorY(c, s.floor), H = c.spec.storey;
            float lDepth = 0.45f;
            float lLength = s.b - s.a;
            int lBays = Mathf.Max(1, Mathf.RoundToInt(lLength / 3.3f));
            float lBay = lLength / lBays;
            float lSpring = y0 + Mathf.Min(2.75f, H - 0.75f);
            float lTop = y0 + H;
            // Piers at both ends, columns between.
            for (int k = 0; k <= lBays; k++)
            {
                float sc = s.a + k * lBay;
                bool lEnd = k == 0 || k == lBays;
                if (lEnd)
                {
                    float p0 = k == 0 ? s.a : s.b - 0.45f, p1 = k == 0 ? s.a + 0.45f : s.b;
                    SegBox(c, s, n, p0, p1, y0, lTop, -lDepth, 0f, lWall);
                    continue;
                }
                Vector3 lBase = P(s, n, sc, y0, -lDepth * 0.5f);
                c.mesh.Box(lColumn, lBase + Vector3.up * 0.12f, new Vector3(0.56f, 0.24f, 0.56f));
                c.mesh.Cylinder(lColumn, lBase + Vector3.up * 0.24f, 0.19f, lSpring - y0 - 0.5f, 14);
                c.mesh.Box(lColumn, lBase + Vector3.up * (lSpring - y0 - 0.16f), new Vector3(0.6f, 0.26f, 0.6f));
            }
            // Arches between them, the wall above up to the floor.
            for (int k = 0; k < lBays; k++)
            {
                float sl = s.a + k * lBay + (k == 0 ? 0.45f : 0.3f), sr = s.a + (k + 1) * lBay - (k == lBays - 1 ? 0.45f : 0.3f);
                float lRise = Mathf.Min((sr - sl) * 0.5f, lTop - lSpring - 0.3f);
                Spandrel(c, s, n, sl, sr, lSpring, lRise, lTop, -lDepth, 0f, lWall);
            }
            // Pier strip over the columns (between arches).
            for (int k = 1; k < lBays; k++)
            {
                float sc = s.a + k * lBay;
                SegBox(c, s, n, sc - 0.3f, sc + 0.3f, lSpring, lTop, -lDepth, 0f, lWall);
            }
            // Balustrade upstairs.
            if (s.floor > 0)
                for (int k = 0; k < lBays; k++)
                {
                    float sl = s.a + k * lBay + (k == 0 ? 0.45f : 0.3f), sr = s.a + (k + 1) * lBay - (k == lBays - 1 ? 0.45f : 0.3f);
                    Balustrade(c, s, n, sl, sr, y0, -lDepth * 0.5f, lColumn);
                }
        }

        /// <summary>The wall above an arch: a rectangle minus a (segmental) arch, front, back and intrados.</summary>
        private static void Spandrel(Ctx c, Seg s, float n, float sl, float sr, float ySpring, float pRise, float yTop, float d0, float d1, ArchMat pMat)
        {
            const int K = 10;
            float lHalf = (sr - sl) * 0.5f, lMid = (sl + sr) * 0.5f;
            // Circle through the springers with the given rise.
            float lRadius = (lHalf * lHalf + pRise * pRise) / (2f * pRise);
            float lCy = ySpring + pRise - lRadius;
            float lA0 = Mathf.Atan2(ySpring - lCy, -lHalf), lA1 = Mathf.Atan2(ySpring - lCy, lHalf);
            for (int k = 0; k < K; k++)
            {
                float a0 = Mathf.Lerp(lA0, lA1, (float)k / K), a1 = Mathf.Lerp(lA0, lA1, (float)(k + 1) / K);
                float s0 = lMid + Mathf.Cos(a0) * lRadius, y0 = lCy + Mathf.Sin(a0) * lRadius;
                float s1 = lMid + Mathf.Cos(a1) * lRadius, y1 = lCy + Mathf.Sin(a1) * lRadius;
                foreach (float d in new[] { d0, d1 })
                {
                    Vector3 p0 = P(s, n, s0, y0, d), p1 = P(s, n, s1, y1, d), q0 = P(s, n, s0, yTop, d), q1 = P(s, n, s1, yTop, d);
                    c.mesh.DoubleQuad(pMat, p0, q0, q1, p1);
                }
                Vector3 i0 = P(s, n, s0, y0, d0), i1 = P(s, n, s1, y1, d0), j0 = P(s, n, s0, y0, d1), j1 = P(s, n, s1, y1, d1);
                c.mesh.Quad(pMat, i0, i1, j1, j0);
                c.mesh.Quad(pMat, j0, j1, i1, i0);
            }
            // Springer blocks filling the corners below the arch's start.
            SegBox(c, s, n, sl - 0.01f, sl + 0.02f, ySpring - 0.02f, yTop, d0, d1, pMat);
            SegBox(c, s, n, sr - 0.02f, sr + 0.01f, ySpring - 0.02f, yTop, d0, d1, pMat);
        }

        private static void Balustrade(Ctx c, Seg s, float n, float s0, float s1, float y0, float d, ArchMat pMat)
        {
            SegBox(c, s, n, s0, s1, y0, y0 + 0.14f, d - 0.17f, d + 0.17f, pMat);
            SegBox(c, s, n, s0, s1, y0 + 0.86f, y0 + 1.0f, d - 0.18f, d + 0.18f, pMat);
            for (float k = s0 + 0.15f; k < s1 - 0.1f; k += 0.28f)
            {
                Vector3 lBase = P(s, n, k, y0 + 0.14f, d);
                c.mesh.Cylinder(pMat, lBase, 0.055f, 0.72f, 8, false, false);
            }
            // An invisible collider so the gaps never let anyone through.
            SegBox(c, s, n, s0, s1, y0 + 0.14f, y0 + 0.86f, d - 0.03f, d + 0.03f, ArchLib.Invisible, true);
        }

        private static void Parapet(Ctx c, Seg s, float n, float y0, ArchMat pMat)
        {
            SegBox(c, s, n, s.a, s.b, y0, y0 + 1.0f, -0.3f, 0f, pMat);
            SegBox(c, s, n, s.a - 0.03f, s.b + 0.03f, y0 + 1.0f, y0 + 1.08f, -0.36f, 0.05f, c.spec.style.trim);
            if (c.spec.style.crenels)
                for (float k = s.a + 0.25f; k < s.b - 0.4f; k += 1.0f) SegBox(c, s, n, k, k + 0.5f, y0 + 1.08f, y0 + 1.75f, -0.3f, 0f, pMat);
        }

        // ---------------------------------------------------------------- slabs, ceilings, flat roofs

        private static void Slabs(Ctx c, int f)
        {
            ArchSpec lSpec = c.spec;
            ArchStyle st = lSpec.style;
            float y = FloorY(c, f);
            bool[,] lHole = new bool[c.nx, c.nz];
            void MarkHole(Rect pRect)
            {
                for (int i = 0; i < c.nx; i++)
                    for (int j = 0; j < c.nz; j++)
                        if (pRect.Contains(new Vector2((i + 0.5f) * CELL, (j + 0.5f) * CELL))) lHole[i, j] = true;
            }
            foreach (ArchStair s in lSpec.stairs) if (s.fromFloor + 1 == f) MarkHole(s.rect);
            foreach ((int hf, Rect hr) in lSpec.holes) if (hf == f) MarkHole(hr);
            foreach (Bounds b in c.shafts) if (b.min.y < y - 0.05f && b.max.y > y - lSpec.slab + 0.05f) MarkHole(Rect.MinMaxRect(b.min.x + 0.06f, b.min.z + 0.06f, b.max.x - 0.06f, b.max.z - 0.06f));

            foreach (ArchRoom lRoom in lSpec.rooms)
            {
                if (lRoom.floor != f || lRoom.noFloor) continue;
                bool[,] lCells = new bool[c.nx, c.nz];
                bool lAny = false;
                for (int i = 0; i < c.nx; i++)
                    for (int j = 0; j < c.nz; j++)
                        if (c.owner[f][i, j] == lRoom.index && !lHole[i, j]) { lCells[i, j] = true; lAny = true; }
                if (!lAny) continue;
                ArchMat lFloor = lRoom.floorMat ?? FloorFor(st, lRoom);
                foreach (Rect r in Rects(lCells, c.nx, c.nz))
                {
                    c.mesh.BoxMinMax(f == c.minF ? (st.cellarWall ?? st.ceiling) : st.ceiling, new Vector3(r.xMin, y - lSpec.slab, r.yMin), new Vector3(r.xMax, y - 0.02f, r.yMax), true, ArchMesh.Faces.Top | OutsideSides(c, f, r));
                    c.mesh.Quad(lFloor, new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMax, y, r.yMin), new Vector3(r.xMax, y, r.yMax), new Vector3(r.xMin, y, r.yMax));
                }
            }

            // Ceilings and flat roofs where nothing stands above.
            bool[,] lCeiling = new bool[c.nx, c.nz], lFlat = new bool[c.nx, c.nz], lGuestCeiling = new bool[c.nx, c.nz];
            bool lAnyFlat = false;
            for (int i = 0; i < c.nx; i++)
                for (int j = 0; j < c.nz; j++)
                {
                    int o = c.owner[f][i, j];
                    if (o < 0) continue;
                    ArchRoom lRoom = lSpec.rooms[o];
                    if (lRoom.open) continue;
                    int lAbove = Owner(c, f + 1, i, j);
                    bool lGuestRoom = st.guestCeiling != null && f >= 0 && ArchSpec.IsGuest(lRoom.type);
                    Vector2 lCell = new Vector2((i + 0.5f) * CELL, (j + 0.5f) * CELL);
                    bool lHoleAbove = lSpec.stairs.Exists(st2 => st2.fromFloor == f && st2.rect.Contains(lCell)) || lSpec.holes.Exists(h => h.floor == f + 1 && h.rect.Contains(lCell))
                        || c.shafts.Exists(b => b.min.y < FloorY(c, f + 1) - 0.05f && b.max.y > FloorY(c, f + 1) - lSpec.slab + 0.05f && b.min.x - 0.1f < lCell.x && b.max.x + 0.1f > lCell.x && b.min.z - 0.1f < lCell.y && b.max.z + 0.1f > lCell.y);
                    if (lGuestRoom && !lHoleAbove && (lAbove < 0 || (!lSpec.rooms[lAbove].noFloor && !lSpec.rooms[lAbove].isVoid))) lGuestCeiling[i, j] = true;
                    if (lAbove >= 0) continue;
                    if (f < 0) { lFlat[i, j] = true; lAnyFlat = true; continue; }
                    Vector2 p = new Vector2((i + 0.5f) * CELL, (j + 0.5f) * CELL);
                    if (!lGuestRoom) lCeiling[i, j] = true;
                    bool lRoofed = lSpec.roofs.Exists(r => r.onFloor == f && r.kind != RoofKind.Flat && r.rect.Contains(p));
                    if (!lRoofed) { lFlat[i, j] = true; lAnyFlat = true; }
                }
            float yc = FloorY(c, f + 1) - lSpec.slab;
            foreach (Rect r in Rects(lCeiling, c.nx, c.nz))
                c.mesh.Quad(st.ceiling, new Vector3(r.xMin, yc, r.yMin), new Vector3(r.xMin, yc, r.yMax), new Vector3(r.xMax, yc, r.yMax), new Vector3(r.xMax, yc, r.yMin));
            float yg = yc - 0.006f;
            foreach (Rect r in Rects(lGuestCeiling, c.nx, c.nz))
                c.mesh.Quad(st.guestCeiling, new Vector3(r.xMin, yg, r.yMin), new Vector3(r.xMin, yg, r.yMax), new Vector3(r.xMax, yg, r.yMax), new Vector3(r.xMax, yg, r.yMin));
            if (!lAnyFlat) return;
            float yr = FloorY(c, f + 1);
            if (f < 0)
            {
                // A cellar reaching out under a yard: the yard's paving over it, no parapet.
                foreach (Rect r in Rects(lFlat, c.nx, c.nz))
                {
                    c.mesh.BoxMinMax(st.ceiling, new Vector3(r.xMin, yc, r.yMin), new Vector3(r.xMax, yr - 0.02f, r.yMax), true, ArchMesh.Faces.Top | ArchMesh.Faces.Bottom);
                    c.mesh.Quad(ArchLib.Surface("patterned_cobblestone", 2.5f), new Vector3(r.xMin, yr, r.yMin), new Vector3(r.xMax, yr, r.yMin), new Vector3(r.xMax, yr, r.yMax), new Vector3(r.xMin, yr, r.yMax));
                }
                return;
            }
            foreach (Rect r in Rects(lFlat, c.nx, c.nz))
            {
                c.mesh.BoxMinMax(st.ceiling, new Vector3(r.xMin, yc, r.yMin), new Vector3(r.xMax, yr - 0.02f, r.yMax), true, ArchMesh.Faces.Top | ArchMesh.Faces.Bottom | OutsideSides(c, f, r));
                c.mesh.Quad(st.flatRoof ?? st.roof, new Vector3(r.xMin, yr, r.yMin), new Vector3(r.xMax, yr, r.yMin), new Vector3(r.xMax, yr, r.yMax), new Vector3(r.xMin, yr, r.yMax));
                if (st.snow) c.mesh.Quad(ArchLib.Snow, new Vector3(r.xMin, yr + 0.06f, r.yMin), new Vector3(r.xMax, yr + 0.06f, r.yMin), new Vector3(r.xMax, yr + 0.06f, r.yMax), new Vector3(r.xMin, yr + 0.06f, r.yMax), false);
            }
            // Parapets on the edges of the flat roofs that drop away.
            for (int i = 0; i < c.nx; i++)
                for (int j = 0; j < c.nz; j++)
                {
                    if (!lFlat[i, j]) continue;
                    for (int dir = 0; dir < 4; dir++)
                    {
                        int ni = i + (dir == 1 ? 1 : dir == 3 ? -1 : 0), nj = j + (dir == 2 ? 1 : dir == 0 ? -1 : 0);
                        bool lInside = ni >= 0 && nj >= 0 && ni < c.nx && nj < c.nz;
                        if (lInside && (lFlat[ni, nj] || Owner(c, f + 1, ni, nj) >= 0)) continue;
                        if (lInside && Owner(c, f, ni, nj) >= 0 && lSpec.rooms[Owner(c, f, ni, nj)].open) continue;
                        float x0 = i * CELL, z0 = j * CELL;
                        Vector3 a, b;
                        switch (dir)
                        {
                            case 0: a = new Vector3(x0, yr, z0); b = new Vector3(x0 + CELL, yr + 1f, z0 + 0.28f); break;
                            case 1: a = new Vector3(x0 + CELL - 0.28f, yr, z0); b = new Vector3(x0 + CELL, yr + 1f, z0 + CELL); break;
                            case 2: a = new Vector3(x0, yr, z0 + CELL - 0.28f); b = new Vector3(x0 + CELL, yr + 1f, z0 + CELL); break;
                            default: a = new Vector3(x0, yr, z0); b = new Vector3(x0 + 0.28f, yr + 1f, z0 + CELL); break;
                        }
                        c.mesh.BoxMinMax(st.facade, a, b);
                        c.mesh.BoxMinMax(st.trim, new Vector3(a.x - 0.03f, b.y, a.z - 0.03f), new Vector3(b.x + 0.03f, b.y + 0.07f, b.z + 0.03f), false);
                        if (st.crenels && ((i + j) & 1) == 0) c.mesh.BoxMinMax(st.facade, new Vector3(a.x, b.y + 0.07f, a.z), new Vector3(b.x, b.y + 0.75f, b.z));
                    }
                }
        }

        /// <summary>The sides of a slab rectangle that lie on the outer wall line (no room beyond on that floor): hidden in the facade.</summary>
        private static ArchMesh.Faces OutsideSides(Ctx c, int f, Rect r)
        {
            int i0 = Mathf.RoundToInt(r.xMin / CELL), i1 = Mathf.RoundToInt(r.xMax / CELL), j0 = Mathf.RoundToInt(r.yMin / CELL), j1 = Mathf.RoundToInt(r.yMax / CELL);
            bool Out(int i, int j) => Owner(c, f, i, j) < 0;
            bool lLeft = true, lRight = true, lBack = true, lFront = true;
            for (int j = j0; j < j1; j++) { if (!Out(i0 - 1, j)) lLeft = false; if (!Out(i1, j)) lRight = false; }
            for (int i = i0; i < i1; i++) { if (!Out(i, j0 - 1)) lBack = false; if (!Out(i, j1)) lFront = false; }
            ArchMesh.Faces lSkip = ArchMesh.Faces.None;
            if (lLeft) lSkip |= ArchMesh.Faces.Left;
            if (lRight) lSkip |= ArchMesh.Faces.Right;
            if (lBack) lSkip |= ArchMesh.Faces.Back;
            if (lFront) lSkip |= ArchMesh.Faces.Front;
            return lSkip;
        }

        public static ArchMat FloorFor(ArchStyle st, ArchRoom pRoom)
        {
            switch (pRoom.type)
            {
                case RoomType.Kitchen: case RoomType.Pantry: case RoomType.Bath: case RoomType.Laundry: case RoomType.Spa: case RoomType.Clinic:
                    return ArchLib.Surface("large_floor_tiles_02", 2f);
                case RoomType.Hall: case RoomType.Chapel: case RoomType.Church: case RoomType.Lobby: case RoomType.Ballroom:
                    return ArchLib.Surface("marble_01", 2.5f);
                case RoomType.Salon: case RoomType.Dining: case RoomType.Study: case RoomType.Suite: case RoomType.Bedroom: case RoomType.Library: case RoomType.Office: case RoomType.Living:
                    return ArchLib.Surface("herringbone_parquet", 2f);
                case RoomType.Cellar: case RoomType.WineCellar: case RoomType.ColdRoom: case RoomType.CoalStore: case RoomType.Crypt: case RoomType.Vault: case RoomType.Plant:
                    return ArchLib.Surface("cobblestone_floor_01", 2.5f);
                case RoomType.Garage: case RoomType.Hangar: case RoomType.Workshop: case RoomType.Security: case RoomType.Armoury: case RoomType.Radio:
                    return ArchLib.Surface("concrete_floor_02", 3f);
                case RoomType.Terrace: case RoomType.Loggia: case RoomType.Gallery: case RoomType.Belvedere:
                    return ArchLib.Surface("patterned_cobblestone", 2.5f);
                case RoomType.Attic: case RoomType.Dormitory: case RoomType.Barracks: case RoomType.Archive: case RoomType.Linen:
                    return ArchLib.Surface("old_planks_02", 2f);
                default:
                    return st.floorDefault ?? ArchLib.Surface("wooden_planks", 2f);
            }
        }

        // ---------------------------------------------------------------- stairs

        private static void Stair(Ctx c, ArchStair s)
        {
            ArchSpec lSpec = c.spec;
            ArchMat lMat = s.mat ?? lSpec.style.stair ?? lSpec.style.trim;
            float y0 = FloorY(c, s.fromFloor), lRise = lSpec.storey;
            Rect r = s.rect;
            // Canonical frame: z (u) climbs away from the start edge, x (w) across, origin at a start corner.
            float lYaw; Vector3 lOrigin; float L, W;
            switch (s.startSide)
            {
                case 0: lYaw = 0f; lOrigin = new Vector3(r.xMin, y0, r.yMin); L = r.height; W = r.width; break;
                case 1: lYaw = -90f; lOrigin = new Vector3(r.xMax, y0, r.yMin); L = r.width; W = r.height; break;
                case 2: lYaw = 180f; lOrigin = new Vector3(r.xMax, y0, r.yMax); L = r.height; W = r.width; break;
                default: lYaw = 90f; lOrigin = new Vector3(r.xMin, y0, r.yMax); L = r.width; W = r.height; break;
            }
            Matrix4x4 lFrame = Matrix4x4.TRS(lOrigin, Quaternion.Euler(0, lYaw, 0), Vector3.one);
            Matrix4x4 lOld = c.mesh.Frame;
            c.mesh.Frame = lFrame;
            Transform lRamps = new GameObject("StairRamps").transform;
            lRamps.SetParent(c.result.shell, false);

            // One flight from (u0, h0) to (u1, h1) across w0..w1: visible steps on a solid mass, a
            // smooth ramp collider through the nosings, step-shaped colliders under it (no walking
            // into the mass from below).
            bool lStackedBelow = lSpec.stairs.Exists(o => o != s && o.fromFloor == s.fromFloor - 1 && o.rect.Overlaps(s.rect));
            bool lStackedAbove = lSpec.stairs.Exists(o => o != s && o.fromFloor == s.fromFloor + 1 && o.rect.Overlaps(s.rect));
            ArchMat lSoffit = lSpec.style.ceiling ?? lMat, lRunner = s.grand ? ArchLib.Surface("velour_velvet", 1f) : null;
            ArchMat lBaluster = s.grand ? (lSpec.style.column ?? lSpec.style.trim) : ArchLib.Iron;
            void Flight(float w0, float w1, float u0, float u1, float h0, float h1, bool pSolid)
            {
                int lCount = Mathf.Max(2, Mathf.CeilToInt((h1 - h0) / 0.18f - 0.01f));
                float lRun = (u1 - u0) / lCount, lUp = (h1 - h0) / lCount, lSign = Mathf.Sign(u1 - u0);
                // Grand flights always read as an open stair: steps on a sloping soffit between stringers.
                bool lOpen = !pSolid || s.grand;
                for (int k = 0; k < lCount; k++)
                {
                    float ua = u0 + k * lRun, ub = ua + lRun, lTop = h0 + (k + 1) * lUp;
                    float lFoot = pSolid && !s.grand ? 0f : Mathf.Max(0f, lTop - lUp - 0.12f);
                    // The tread with a nosing over the riser.
                    c.mesh.BoxMinMax(lMat, new Vector3(w0, lFoot, Mathf.Min(ua - lSign * 0.03f, ub)), new Vector3(w1, lTop, Mathf.Max(ua - lSign * 0.03f, ub)), false);
                    if (lRunner != null)
                    {
                        float lInset = (w1 - w0) * 0.18f;
                        c.mesh.BoxMinMax(lRunner, new Vector3(w0 + lInset, lTop, Mathf.Min(ua, ub)), new Vector3(w1 - lInset, lTop + 0.008f, Mathf.Max(ua, ub)), false);
                        c.mesh.BoxMinMax(lRunner, new Vector3(w0 + lInset, lTop - lUp, Mathf.Min(ua - lSign * 0.035f, ua - lSign * 0.045f)), new Vector3(w1 - lInset, lTop, Mathf.Max(ua - lSign * 0.035f, ua - lSign * 0.045f)), false);
                    }
                    float lUnder = pSolid ? 0f : Mathf.Max(0f, lTop - lUp - 0.3f);
                    if (lTop - lUp - 0.05f > lUnder + 0.05f)
                        c.mesh.BoxMinMax(ArchLib.Invisible, new Vector3(w0 + 0.02f, lUnder, Mathf.Min(ua, ub) + 0.02f), new Vector3(w1 - 0.02f, lTop - lUp - 0.05f, Mathf.Max(ua, ub) - 0.02f), true);
                }
                // Nosing line N(u): through the top of every riser.
                float N(float u) => h0 + lUp + (u - u0) / (u1 - u0) * (h1 - h0 - lUp);
                if (lOpen)
                {
                    float Bt(float u) => Mathf.Max(h0 - 0.28f, N(u) - 0.48f);
                    // Plastered soffit and two stringers (no stepped blocks underneath).
                    c.mesh.DoubleQuad(lSoffit, new Vector3(w0, Bt(u0), u0), new Vector3(w1, Bt(u0), u0), new Vector3(w1, Bt(u1), u1), new Vector3(w0, Bt(u1), u1), true);
                    foreach (float w in new[] { w0 - 0.04f, w1 + 0.04f })
                    {
                        c.mesh.DoubleQuad(lMat, new Vector3(w, Bt(u0), u0), new Vector3(w, N(u0) + 0.06f, u0), new Vector3(w, Mathf.Min(N(u1) + 0.06f, h1 + 0.06f), u1), new Vector3(w, Bt(u1), u1), false);
                    }
                }
                if (s.grand)
                {
                    // A turned balustrade along both sides: handrail, a baluster per tread, newel posts.
                    foreach (float w in new[] { w0 + 0.06f, w1 - 0.06f })
                    {
                        Vector3 lRailA = new Vector3(w, N(u0) + 0.92f, u0), lRailB = new Vector3(w, N(u1) + 0.92f, u1);
                        c.mesh.Tube(ArchLib.Surface("walnut_veneer", 0.6f), lRailA, lRailB, 0.04f, 8, false);
                        for (int k = 0; k < lCount; k++)
                        {
                            float um = u0 + (k + 0.5f) * lRun, lTop = h0 + (k + 1) * lUp;
                            c.mesh.Cylinder(lBaluster, new Vector3(w, lTop, um), 0.03f, N(um) + 0.9f - lTop, 8, false, false);
                        }
                        c.mesh.BoxMinMax(lBaluster, new Vector3(w - 0.11f, h0, u0 - lSign * 0.11f - 0.11f), new Vector3(w + 0.11f, h0 + 1.15f, u0 - lSign * 0.11f + 0.11f), true);
                        c.mesh.BoxMinMax(lBaluster, new Vector3(w - 0.14f, h0 + 1.15f, u0 - lSign * 0.11f - 0.14f), new Vector3(w + 0.14f, h0 + 1.22f, u0 - lSign * 0.11f + 0.14f), false);
                        // Nothing falls through the balusters.
                        for (int k = 0; k < lCount; k++)
                        {
                            float ua = u0 + k * lRun, ub = ua + lRun, lTop = h0 + (k + 1) * lUp;
                            c.mesh.BoxMinMax(ArchLib.Invisible, new Vector3(w - 0.03f, lTop, Mathf.Min(ua, ub)), new Vector3(w + 0.03f, lTop + 0.95f, Mathf.Max(ua, ub)), true);
                        }
                    }
                }
                float lShift = lRun * 0.5f;
                Vector3 a = new Vector3((w0 + w1) * 0.5f, h0, u0 - lShift), b = new Vector3((w0 + w1) * 0.5f, h1, u1 - lShift);
                Vector3 lDir = b - a;
                Quaternion lRot = Quaternion.LookRotation(lDir.normalized, Vector3.up);
                GameObject lRamp = new GameObject("Ramp");
                lRamp.transform.SetParent(lRamps, false);
                lRamp.transform.localPosition = lFrame.MultiplyPoint3x4((a + b) * 0.5f + lRot * Vector3.down * 0.1f);
                lRamp.transform.localRotation = Quaternion.Euler(0, lYaw, 0) * lRot;
                BoxCollider lBox = lRamp.AddComponent<BoxCollider>();
                lBox.size = new Vector3(w1 - w0, 0.2f, lDir.magnitude + 0.4f);
                lRamp.isStatic = true;
            }
            void Rail(Vector3 pFrom, Vector3 pTo)
            {
                Vector3 lMin = Vector3.Min(pFrom, pTo), lMax = Vector3.Max(pFrom, pTo);
                c.mesh.BoxMinMax(ArchLib.Iron, lMin + new Vector3(-0.025f, 0.95f, -0.025f), lMax + new Vector3(0.025f, 1.0f, 0.025f));
                Vector3 lDir = pTo - pFrom;
                int lBars = Mathf.Max(1, Mathf.RoundToInt(lDir.magnitude / 0.14f));
                for (int k = 0; k <= lBars; k++)
                {
                    Vector3 p = Vector3.Lerp(pFrom, pTo, (float)k / lBars);
                    c.mesh.BoxMinMax(ArchLib.Iron, p + new Vector3(-0.011f, 0f, -0.011f), p + new Vector3(0.011f, 0.95f, 0.011f), true);
                }
            }
            if (!s.dogleg)
            {
                Flight(0f, W, 0f, L, 0f, lRise, !lStackedBelow);
                if (!lStackedAbove) Rail(new Vector3(0.03f, lRise, 0.03f), new Vector3(W - 0.03f, lRise, 0.03f));
                Rail(new Vector3(0.03f, lRise, 0.03f), new Vector3(0.03f, lRise, L));
                Rail(new Vector3(W - 0.03f, lRise, 0.03f), new Vector3(W - 0.03f, lRise, L));
            }
            else
            {
                float lLanding = Mathf.Clamp(W * 0.5f + 0.1f, 1.1f, 1.5f);
                float lRun = L - lLanding, lHalfW = W * 0.5f, lMidY = lRise * 0.5f;
                float a0 = s.leftFirst ? 0f : lHalfW, a1 = s.leftFirst ? lHalfW : W;
                float b0 = s.leftFirst ? lHalfW : 0f, b1 = s.leftFirst ? W : lHalfW;
                Flight(a0, a1, 0f, lRun, 0f, lMidY, !lStackedBelow);
                c.mesh.BoxMinMax(lMat, new Vector3(0f, lMidY - 0.35f, lRun), new Vector3(W, lMidY, L));
                Flight(b0, b1, lRun, 0f, lMidY, lRise, false);
                // A low wall between the flights, rising a metre over the upper floor as a parapet.
                c.mesh.BoxMinMax(lSpec.style.trim, new Vector3(lHalfW - 0.07f, 0.2f, 0f), new Vector3(lHalfW + 0.07f, lRise + 1.0f, lRun));
                if (!lStackedAbove) Rail(new Vector3(a0 + 0.03f, lRise, 0.03f), new Vector3(a1 - 0.07f, lRise, 0.03f));
                Rail(new Vector3(0.03f, lRise, 0.03f), new Vector3(0.03f, lRise, L - 0.03f));
                Rail(new Vector3(W - 0.03f, lRise, 0.03f), new Vector3(W - 0.03f, lRise, L - 0.03f));
                Rail(new Vector3(0.03f, lRise, L - 0.03f), new Vector3(W - 0.03f, lRise, L - 0.03f));
            }
            c.mesh.Frame = lOld;
        }

        // ---------------------------------------------------------------- roofs

        private static void Roof(Ctx c, ArchRoof pRoof)
        {
            ArchStyle st = c.spec.style;
            ArchMat lTile = pRoof.mat ?? st.roof, lSoffit = st.soffit ?? ArchLib.Surface("wooden_planks", 2f), lGable = st.facade;
            float yBase = FloorY(c, pRoof.onFloor + 1);
            Rect r = pRoof.rect;
            float o = pRoof.overhang, tan = Mathf.Tan(pRoof.pitch * Mathf.Deg2Rad);
            if (pRoof.kind == RoofKind.Flat)
            {
                c.mesh.BoxMinMax(st.flatRoof ?? lTile, new Vector3(r.xMin, yBase - 0.3f, r.yMin), new Vector3(r.xMax, yBase, r.yMax));
                return;
            }
            // Work in a frame where the ridge runs along x.
            bool lAlongX = pRoof.ridgeAlongX;
            float lLen = lAlongX ? r.width : r.height, lDepth = lAlongX ? r.height : r.width;
            Matrix4x4 lFrame = lAlongX ? Matrix4x4.Translate(new Vector3(r.xMin, 0, r.yMin)) : Matrix4x4.TRS(new Vector3(r.xMin, 0, r.yMax), Quaternion.Euler(0, 90, 0), Vector3.one);
            Matrix4x4 lOld = c.mesh.Frame;
            c.mesh.Frame = lFrame;
            float ye = yBase - o * tan;
            float x0 = -o, x1 = lLen + o, z0 = -o, z1 = lDepth + o;
            float lHalf = (z1 - z0) * 0.5f, lRidgeY = ye + lHalf * tan, zm = (z0 + z1) * 0.5f;
            float lSlope = lHalf / Mathf.Cos(pRoof.pitch * Mathf.Deg2Rad);
            void Plane(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                // Given clockwise seen from above: the tiles face up, the soffit down, snow over the tiles.
                c.mesh.QuadUv(lTile, d, cc, b, a, ud, uc, ub, ua);
                c.mesh.Quad(lSoffit, a - Vector3.up * 0.12f, b - Vector3.up * 0.12f, cc - Vector3.up * 0.12f, d - Vector3.up * 0.12f);
                if (st.snow) c.mesh.Quad(ArchLib.Snow, d + Vector3.up * 0.1f, cc + Vector3.up * 0.1f, b + Vector3.up * 0.1f, a + Vector3.up * 0.1f, false);
            }
            if (pRoof.kind == RoofKind.Gable)
            {
                Plane(new Vector3(x0, ye, z0), new Vector3(x0, lRidgeY, zm), new Vector3(x1, lRidgeY, zm), new Vector3(x1, ye, z0), new Vector2(x0, 0), new Vector2(x0, lSlope), new Vector2(x1, lSlope), new Vector2(x1, 0));
                Plane(new Vector3(x1, ye, z1), new Vector3(x1, lRidgeY, zm), new Vector3(x0, lRidgeY, zm), new Vector3(x0, ye, z1), new Vector2(-x1, 0), new Vector2(-x1, lSlope), new Vector2(-x0, lSlope), new Vector2(-x0, 0));
                // Gable ends (walls up to the ridge).
                float lWallRidge = yBase + lDepth * 0.5f * tan;
                foreach (float gx in new[] { 0.02f, lLen - 0.02f })
                {
                    c.mesh.Tri(lGable, new Vector3(gx, yBase, 0), new Vector3(gx, lWallRidge, lDepth * 0.5f), new Vector3(gx, yBase, lDepth));
                    c.mesh.Tri(lGable, new Vector3(gx, yBase, lDepth), new Vector3(gx, lWallRidge, lDepth * 0.5f), new Vector3(gx, yBase, 0));
                }
            }
            else if (pRoof.kind == RoofKind.Hip)
            {
                float lInset = Mathf.Min(lHalf, (x1 - x0) * 0.5f);
                float rx0 = x0 + lInset, rx1 = x1 - lInset;
                Plane(new Vector3(x0, ye, z0), new Vector3(rx0, lRidgeY, zm), new Vector3(rx1, lRidgeY, zm), new Vector3(x1, ye, z0), new Vector2(x0, 0), new Vector2(rx0, lSlope), new Vector2(rx1, lSlope), new Vector2(x1, 0));
                Plane(new Vector3(x1, ye, z1), new Vector3(rx1, lRidgeY, zm), new Vector3(rx0, lRidgeY, zm), new Vector3(x0, ye, z1), new Vector2(-x1, 0), new Vector2(-rx1, lSlope), new Vector2(-rx0, lSlope), new Vector2(-x0, 0));
                // Hip ends as quads with a collapsed edge.
                Plane(new Vector3(x0, ye, z1), new Vector3(rx0, lRidgeY, zm), new Vector3(rx0, lRidgeY, zm), new Vector3(x0, ye, z0), new Vector2(z1, 0), new Vector2(zm, lSlope), new Vector2(zm, lSlope), new Vector2(z0, 0));
                Plane(new Vector3(x1, ye, z0), new Vector3(rx1, lRidgeY, zm), new Vector3(rx1, lRidgeY, zm), new Vector3(x1, ye, z1), new Vector2(-z0, 0), new Vector2(-zm, lSlope), new Vector2(-zm, lSlope), new Vector2(-z1, 0));
            }
            else
            {
                // Shed: falls toward the low side (in the ridge frame, low = z0 unless lowSide says the far one).
                bool lLowFar = lAlongX ? pRoof.lowSide == 2 : pRoof.lowSide == 1;
                float lHigh = ye + (z1 - z0) * tan * 0.5f;
                float yz0 = lLowFar ? lHigh : ye, yz1 = lLowFar ? ye : lHigh;
                float lFull = (z1 - z0) / Mathf.Cos(pRoof.pitch * 0.5f * Mathf.Deg2Rad);
                if (!lLowFar) Plane(new Vector3(x0, yz0, z0), new Vector3(x0, yz1, z1), new Vector3(x1, yz1, z1), new Vector3(x1, yz0, z0), new Vector2(x0, 0), new Vector2(x0, lFull), new Vector2(x1, lFull), new Vector2(x1, 0));
                else Plane(new Vector3(x1, yz1, z1), new Vector3(x1, yz0, z0), new Vector3(x0, yz0, z0), new Vector3(x0, yz1, z1), new Vector2(-x1, 0), new Vector2(-x1, lFull), new Vector2(-x0, lFull), new Vector2(-x0, 0));
                // Side walls under the slope.
                float lWallHigh = yBase + lDepth * tan * 0.5f;
                float w0 = lLowFar ? lWallHigh : yBase, w1 = lLowFar ? yBase : lWallHigh;
                c.mesh.DoubleQuad(lGable, new Vector3(0.02f, yBase, 0), new Vector3(0.02f, w0, 0), new Vector3(0.02f, w1, lDepth), new Vector3(0.02f, yBase, lDepth));
                c.mesh.DoubleQuad(lGable, new Vector3(lLen - 0.02f, yBase, lDepth), new Vector3(lLen - 0.02f, w1, lDepth), new Vector3(lLen - 0.02f, w0, 0), new Vector3(lLen - 0.02f, yBase, 0));
                float lBack = lLowFar ? 0f : lDepth;
                c.mesh.DoubleQuad(lGable, new Vector3(0, yBase, lBack), new Vector3(lLen, yBase, lBack), new Vector3(lLen, Mathf.Max(w0, w1), lBack), new Vector3(0, Mathf.Max(w0, w1), lBack));
            }
            // Fascia and gutter along the eaves.
            c.mesh.BoxMinMax(st.trim, new Vector3(x0, ye - 0.22f, z0 - 0.04f), new Vector3(x1, ye + 0.02f, z0 + 0.02f), false);
            if (pRoof.kind != RoofKind.Shed) c.mesh.BoxMinMax(st.trim, new Vector3(x0, ye - 0.22f, z1 - 0.02f), new Vector3(x1, ye + 0.02f, z1 + 0.04f), false);
            c.mesh.Frame = lOld;
        }

        // ---------------------------------------------------------------- ledges, balconies, drainpipes

        private static void Cornice(Ctx c, int f)
        {
            ArchStyle st = c.spec.style;
            float y = FloorY(c, f);
            foreach (Seg s in c.segs)
            {
                if (s.floor != f) continue;
                WallKind lKind = Classify(c, s, out int lSide, out ArchRoom lOwner, out ArchRoom lOther);
                if (lKind != WallKind.Facade || lOther != null) continue;
                float n = lSide < 0 ? 1f : -1f;
                SegBox(c, s, n, s.a - 0.4f, s.b + 0.4f, y - 0.1f, y + 0.16f, -0.02f, 0.4f, st.trim);
                SegBox(c, s, n, s.a - 0.3f, s.b + 0.3f, y - 0.22f, y - 0.1f, -0.02f, 0.25f, st.trim);
            }
        }

        /// <summary>A plain stone band at a floor line, 0.3 m deep: a ledge to hang from and shimmy along.</summary>
        private static void StringCourse(Ctx c, int f)
        {
            ArchStyle st = c.spec.style;
            float y = FloorY(c, f);
            foreach (Seg s in c.segs)
            {
                if (s.floor != f) continue;
                WallKind lKind = Classify(c, s, out int lSide, out ArchRoom lOwner, out ArchRoom lOther);
                if (lKind != WallKind.Facade || lOther != null) continue;
                float n = lSide < 0 ? 1f : -1f;
                SegBox(c, s, n, s.a - 0.3f, s.b + 0.3f, y - 0.06f, y + 0.12f, -0.02f, 0.3f, st.trim);
                SegBox(c, s, n, s.a - 0.22f, s.b + 0.22f, y - 0.16f, y - 0.06f, -0.02f, 0.18f, st.trim);
            }
        }

        private static bool FacadeAt(Ctx c, int pFloor, Vector2 pAt, out Seg pSeg, out float pN, out float pAlong)
        {
            foreach (Seg s in c.segs)
            {
                if (!DoorOn(s, pFloor, pAt, out pAlong)) continue;
                WallKind lKind = Classify(c, s, out int lSide, out ArchRoom lOwner, out ArchRoom lOther);
                if (lKind != WallKind.Facade) continue;
                pSeg = s;
                pN = lSide < 0 ? 1f : -1f;
                return true;
            }
            pSeg = null; pN = 1f; pAlong = 0f;
            return false;
        }

        private static void Balcony(Ctx c, ArchBalcony b)
        {
            if (!FacadeAt(c, b.floor, b.at, out Seg s, out float n, out float sa)) { Debug.LogWarning($"[Arch] {c.spec.name}: balcony at {b.at} is not on a facade"); return; }
            ArchStyle st = c.spec.style;
            float y = FloorY(c, b.floor);
            float s0 = sa - b.width * 0.5f, s1 = sa + b.width * 0.5f, d = b.depth;
            // A moulded stone slab on two carved consoles, paved on top.
            SegBox(c, s, n, s0, s1, y - 0.22f, y + 0.04f, -0.05f, d, st.trim);
            SegBox(c, s, n, s0 - 0.04f, s1 + 0.04f, y - 0.1f, y - 0.02f, d - 0.02f, d + 0.04f, st.trim, false);
            SegBox(c, s, n, s0 + 0.03f, s1 - 0.03f, y + 0.04f, y + 0.05f, 0.02f, d - 0.03f, ArchLib.Surface("large_floor_tiles_02", 1.2f), false);
            foreach (float k in new[] { s0 + 0.25f, s1 - 0.5f })
            {
                SegBox(c, s, n, k, k + 0.25f, y - 0.55f, y - 0.22f, 0f, d * 0.45f, st.trim);
                SegBox(c, s, n, k, k + 0.25f, y - 0.8f, y - 0.55f, 0f, d * 0.2f, st.trim);
            }
            // Wrought-iron railing: top rail, bottom rail, bars, posts; collision by invisible panels.
            SegBox(c, s, n, s0, s1, y + 0.98f, y + 1.05f, d - 0.07f, d, ArchLib.Iron, false);
            SegBox(c, s, n, s0, s0 + 0.06f, y + 0.98f, y + 1.05f, 0f, d, ArchLib.Iron, false);
            SegBox(c, s, n, s1 - 0.06f, s1, y + 0.98f, y + 1.05f, 0f, d, ArchLib.Iron, false);
            SegBox(c, s, n, s0, s1, y + 0.12f, y + 0.15f, d - 0.06f, d - 0.03f, ArchLib.Iron, false);
            foreach (float k in new[] { s0, s1 - 0.05f, (s0 + s1) * 0.5f - 0.025f })
                SegBox(c, s, n, k, k + 0.05f, y + 0.04f, y + 1.0f, d - 0.07f, d - 0.02f, ArchLib.Iron, false);
            for (float k = s0 + 0.12f; k < s1 - 0.08f; k += 0.12f) SegBox(c, s, n, k, k + 0.02f, y + 0.04f, y + 0.98f, d - 0.055f, d - 0.035f, ArchLib.Iron, false);
            for (float k = 0.12f; k < d - 0.06f; k += 0.12f)
            {
                SegBox(c, s, n, s0 + 0.015f, s0 + 0.035f, y + 0.04f, y + 0.98f, k, k + 0.02f, ArchLib.Iron, false);
                SegBox(c, s, n, s1 - 0.035f, s1 - 0.015f, y + 0.04f, y + 0.98f, k, k + 0.02f, ArchLib.Iron, false);
            }
            SegBox(c, s, n, s0, s1, y + 0.04f, y + 1.0f, d - 0.06f, d - 0.03f, ArchLib.Invisible, true);
            SegBox(c, s, n, s0, s0 + 0.04f, y + 0.04f, y + 1.0f, 0f, d, ArchLib.Invisible, true);
            SegBox(c, s, n, s1 - 0.04f, s1, y + 0.04f, y + 1.0f, 0f, d, ArchLib.Invisible, true);
            Poi(c, P(s, n, sa, y + 1f, d + 0.3f), "Balcony", MapPoi.Kind.Entrance);
        }

        private static ArchMat _ivy;

        /// <summary>Leaves for ivy sheets: the jungle bush foliage (alpha-cut), else a plain green.</summary>
        private static ArchMat Ivy
        {
            get
            {
                if (_ivy != null && _ivy.material != null) return _ivy;
                Material lLeaf = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/PBR/Models/Veg_JungleBush_A/M_Veg_JungleBush_A_leaf.mat");
                _ivy = lLeaf != null ? new ArchMat("IvyLeaf", lLeaf, 0.9f) : ArchLib.PlainMat("Ivy", new Color(0.15f, 0.3f, 0.1f), 0.2f, 0f);
                return _ivy;
            }
        }

        private static void Drain(Ctx c, ArchDrain d)
        {
            if (!FacadeAt(c, 0, d.at, out Seg s, out float n, out float sa) && !FacadeAt(c, Mathf.Max(0, c.minF), d.at, out s, out n, out sa))
            {
                Debug.LogWarning($"[Arch] {c.spec.name}: drainpipe at {d.at} is not on a ground facade");
                return;
            }
            int lTop = d.topFloor >= 0 ? d.topFloor : c.maxF;
            // The highest floor with a room at that point.
            for (int f = lTop; f >= 0; f--)
            {
                Vector2 lInside = d.at - (s.vertical ? new Vector2(n, 0) : new Vector2(0, n)) * 0.6f;
                int i = Mathf.FloorToInt(lInside.x / CELL), j = Mathf.FloorToInt(lInside.y / CELL);
                if (Owner(c, f, i, j) >= 0) { lTop = f; break; }
            }
            float lHeight = FloorY(c, lTop + 1) - 0.1f;
            Vector3 lNormal = (s.vertical ? Vector3.right : Vector3.forward) * n;
            string lLabel = string.IsNullOrEmpty(d.poi) ? (d.kind == ClimbKind.Drainpipe ? "Drainpipe" : d.kind == ClimbKind.Ivy ? "Ivy" : "Trellis") : d.poi;
            float lWidth;
            if (d.kind == ClimbKind.Drainpipe)
            {
                // A cast-iron downpipe on wall brackets: shoe at the foot, hopper head under the gutter.
                ArchMat lCast = ArchLib.PlainMat("CastIronPipe", new Color(0.16f, 0.17f, 0.18f), 0.45f, 0.55f);
                Vector3 lFoot = P(s, n, sa, 0f, 0.13f);
                c.mesh.Cylinder(lCast, lFoot + Vector3.up * 0.25f, 0.06f, lHeight - 0.6f, 10, false);
                c.mesh.Tube(lCast, lFoot + Vector3.up * 0.27f, lFoot + Vector3.up * 0.05f + lNormal * 0.18f, 0.06f, 10, false);
                for (float y = 0.9f; y < lHeight - 0.5f; y += 1.6f)
                {
                    c.mesh.Box(lCast, P(s, n, sa, y, 0.065f), s.vertical ? new Vector3(0.13f, 0.06f, 0.05f) : new Vector3(0.05f, 0.06f, 0.13f), false);
                    c.mesh.Box(lCast, P(s, n, sa, y, 0.13f), new Vector3(0.15f, 0.07f, 0.15f), false);
                }
                c.mesh.Box(lCast, P(s, n, sa, lHeight - 0.2f, 0.16f), new Vector3(0.28f, 0.3f, 0.28f), false);
                lWidth = 0.45f;
            }
            else if (d.kind == ClimbKind.Ivy)
            {
                // Three staggered sheets of leaves over the wall, densest at the foot.
                lWidth = 1.6f;
                for (int k = 0; k < 3; k++)
                {
                    float w = lWidth * (1f - k * 0.18f), h = lHeight * (1f - k * 0.12f), dd = 0.04f + k * 0.07f, off = (k - 1) * 0.15f;
                    c.mesh.DoubleQuad(Ivy, P(s, n, sa + off - w * 0.5f, 0f, dd), P(s, n, sa + off + w * 0.5f, 0f, dd), P(s, n, sa + off + w * 0.5f, h, dd), P(s, n, sa + off - w * 0.5f, h, dd), false);
                }
                ArchMat lStem = ArchLib.Surface("rough_wood", 0.6f);
                for (float k = -0.5f; k <= 0.51f; k += 0.5f) c.mesh.Tube(lStem, P(s, n, sa + k, 0f, 0.05f), P(s, n, sa + k * 0.4f, lHeight - 0.3f, 0.05f), 0.035f, 6, false);
            }
            else
            {
                // A timber espalier lattice on battens, with vines through it.
                lWidth = 1.4f;
                ArchMat lWood = ArchLib.Surface("rough_wood", 1f);
                for (float k = -0.6f; k <= 0.61f; k += 0.3f) SegBox(c, s, n, sa + k - 0.02f, sa + k + 0.02f, 0.05f, lHeight, 0.04f, 0.08f, lWood, false);
                for (float y = 0.3f; y < lHeight; y += 0.3f) SegBox(c, s, n, sa - 0.68f, sa + 0.68f, y, y + 0.035f, 0.08f, 0.115f, lWood, false);
                c.mesh.DoubleQuad(Ivy, P(s, n, sa - 0.55f, 0f, 0.13f), P(s, n, sa + 0.55f, 0f, 0.13f), P(s, n, sa + 0.4f, lHeight * 0.85f, 0.13f), P(s, n, sa - 0.4f, lHeight * 0.7f, 0.13f), false);
            }
            GameObject lClimb = new GameObject(d.kind + "_Climb") { layer = GameLayers.CLIMBABLE };
            lClimb.transform.SetParent(c.result.shell, false);
            lClimb.transform.localPosition = P(s, n, sa, lHeight * 0.5f, 0.16f);
            lClimb.transform.localRotation = Quaternion.LookRotation(lNormal);
            BoxCollider lBox = lClimb.AddComponent<BoxCollider>();
            lBox.center = new Vector3(0, 0.5f, 0);
            lBox.size = new Vector3(lWidth, lHeight + 1f, 0.28f);
            lClimb.isStatic = true;
            Poi(c, P(s, n, sa, 1f, 0.5f), lLabel, MapPoi.Kind.Climb);
        }

        /// <summary>A coal-chute hatch: a sloping steel double lid in a concrete curb at the foot of the wall, padlocked.</summary>
        private static void Hatch(Ctx c, ArchHatch h)
        {
            if (!FacadeAt(c, 0, h.at, out Seg s, out float n, out float sa) && !FacadeAt(c, Mathf.Max(0, c.minF), h.at, out s, out n, out sa)) return;
            ArchMat lCurb = ArchLib.Surface("concrete_wall_008", 1.5f), lLid = ArchLib.Surface("painted_metal_shutter", 1f);
            SegBox(c, s, n, sa - 0.75f, sa + 0.75f, -0.1f, 0.25f, 0f, 1.1f, lCurb);
            // Two lids leaning from the wall (0.6 m up) to the curb.
            for (int k = 0; k < 2; k++)
            {
                float a0 = sa - 0.68f + k * 0.69f, a1 = a0 + 0.67f;
                c.mesh.Quad(lLid, P(s, n, a0, 0.3f, 1.02f), P(s, n, a1, 0.3f, 1.02f), P(s, n, a1, 0.62f, 0.05f), P(s, n, a0, 0.62f, 0.05f));
                c.mesh.Quad(lLid, P(s, n, a0, 0.62f, 0.05f), P(s, n, a1, 0.62f, 0.05f), P(s, n, a1, 0.3f, 1.02f), P(s, n, a0, 0.3f, 1.02f));
            }
            c.mesh.Box(ArchLib.Brass, P(s, n, sa, 0.42f, 0.62f), new Vector3(0.08f, 0.1f, 0.08f), false);
            Poi(c, P(s, n, sa, 0.6f, 1.4f), h.poi, MapPoi.Kind.Entrance);
        }

        // ---------------------------------------------------------------- vents

        private static void Ducts(Ctx c)
        {
            foreach (ArchDuct lDuct in c.spec.ducts)
            {
                if (lDuct.nodes.Count < 1) continue;
                Vector3 lOrigin = lDuct.nodes[0];
                HashSet<Vector3Int> lCells = new HashSet<Vector3Int>();
                Vector3Int Key(Vector3 p) => Vector3Int.RoundToInt(p - lOrigin);
                for (int k = 0; k < lDuct.nodes.Count; k++)
                {
                    Vector3Int a = Key(lDuct.nodes[k]);
                    lCells.Add(a);
                    if (k == 0) continue;
                    Vector3Int b = Key(lDuct.nodes[k - 1]);
                    Vector3Int lStep = new Vector3Int(System.Math.Sign(a.x - b.x), System.Math.Sign(a.y - b.y), System.Math.Sign(a.z - b.z));
                    for (Vector3Int p = b; p != a; p += lStep) lCells.Add(p);
                }
                Dictionary<(Vector3Int, Vector3Int), (int node, string poi)> lOpen = new Dictionary<(Vector3Int, Vector3Int), (int, string)>();
                foreach ((int lNode, Vector3Int lDir, string lPoi) in lDuct.grilles) lOpen[(Key(lDuct.nodes[lNode]), lDir)] = (lNode, lPoi);
                Vector3Int[] lDirs = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
                ArchMat lMetal = ArchLib.PlainMat("DuctSteel", new Color(0.6f, 0.61f, 0.62f), 0.5f, 0.3f, 1f);
                foreach (Vector3Int lCell in lCells)
                {
                    // Cell centre: x/z centred on the node, y = floor of the duct + 0.5.
                    Vector3 lCenter = lOrigin + (Vector3)lCell + Vector3.up * 0.5f;
                    c.cutters.Add(new Bounds(lCenter, Vector3.one * 1.12f));
                    if (lCells.Contains(lCell + Vector3Int.up) || lCells.Contains(lCell + Vector3Int.down)) c.shafts.Add(new Bounds(lCenter, Vector3.one * 1.12f));
                    foreach (Vector3Int lDir in lDirs)
                    {
                        if (lCells.Contains(lCell + lDir)) continue;
                        Vector3 lFace = lCenter + (Vector3)lDir * 0.53f;
                        Vector3 lSize = new Vector3(lDir.x != 0 ? 0.06f : 1.12f, lDir.y != 0 ? 0.06f : 1.12f, lDir.z != 0 ? 0.06f : 1.12f);
                        if (lOpen.TryGetValue((lCell, lDir), out (int node, string poi) lGrille)) { Grille(c, lFace, lDir, lGrille.poi); continue; }
                        c.mesh.Box(lMetal, lFace, lSize);
                    }
                    // Horizontal runs hang from the slab above on threaded rods (no floating boxes).
                    bool lVertical = lCells.Contains(lCell + Vector3Int.up) || lCells.Contains(lCell + Vector3Int.down);
                    if (!lVertical)
                    {
                        int lFloor = Mathf.FloorToInt((lCenter.y - 0.5f + 0.05f) / c.spec.storey);
                        float lCeil = (lFloor + 1) * c.spec.storey - c.spec.slab, lTopY = lCenter.y + 0.56f;
                        if (lCeil - lTopY > 0.04f && lCeil - lTopY < 3f)
                            foreach (float k in new[] { -0.45f, 0.45f })
                            {
                                Vector3 lRod = lCenter + (lCells.Contains(lCell + Vector3Int.right) || lCells.Contains(lCell + Vector3Int.left) ? new Vector3(0, 0, k) : new Vector3(k, 0, 0));
                                c.mesh.Box(ArchLib.Iron, new Vector3(lRod.x, (lTopY + lCeil) * 0.5f, lRod.z), new Vector3(0.025f, lCeil - lTopY, 0.025f), false);
                            }
                        // A strap round the duct.
                        Vector3 lAxis = lCells.Contains(lCell + Vector3Int.right) || lCells.Contains(lCell + Vector3Int.left) ? Vector3.right : Vector3.forward;
                        Vector3 lStrap = lAxis == Vector3.right ? new Vector3(0.05f, 1.15f, 1.15f) : new Vector3(1.15f, 1.15f, 0.05f);
                        c.mesh.Box(lMetal, lCenter + Vector3.up * 0.575f, new Vector3(lStrap.x, 0.02f, lStrap.z), false);
                        c.mesh.Box(lMetal, lCenter - Vector3.up * 0.575f, new Vector3(lStrap.x, 0.02f, lStrap.z), false);
                    }
                    // Vertical cells: a climbable inner side.
                    if (lCells.Contains(lCell + Vector3Int.up) || lCells.Contains(lCell + Vector3Int.down))
                    {
                        Vector3Int lSide = Vector3Int.right;
                        foreach (Vector3Int d in new[] { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) })
                            if (!lCells.Contains(lCell + d)) { lSide = d; break; }
                        GameObject lClimb = new GameObject("Duct_Climb") { layer = GameLayers.CLIMBABLE };
                        lClimb.transform.SetParent(c.result.shell, false);
                        lClimb.transform.localPosition = lCenter + (Vector3)lSide * 0.46f;
                        BoxCollider lBox = lClimb.AddComponent<BoxCollider>();
                        lBox.size = new Vector3(lSide.x != 0 ? 0.04f : 0.9f, 1.0f, lSide.z != 0 ? 0.04f : 0.9f);
                    }
                }
            }
        }

        private static void Grille(Ctx c, Vector3 pFaceLocal, Vector3Int pDir, string pPoi)
        {
            Transform lRoot = c.result.root;
            GameObject lCover = new GameObject("VentCover") { layer = GameLayers.INTERACTABLE };
            lCover.transform.SetParent(c.result.systems, false);
            lCover.transform.localPosition = pFaceLocal;
            Vector3 lOut = pDir;
            lCover.transform.localRotation = pDir.y != 0 ? Quaternion.LookRotation(lOut, Vector3.forward) : Quaternion.LookRotation(lOut);
            GameObject lGrille = ArchLib.Place("Prop_VentGrille", lCover.transform, lCover.transform.position, 0f, ArchLib.Collide.None, ArchLib.Galvanised, 1f, GameLayers.INTERACTABLE);
            if (lGrille == null) lGrille = new GameObject("Grille");
            lGrille.transform.SetParent(lCover.transform, false);
            lGrille.transform.localPosition = Vector3.zero;
            lGrille.transform.localRotation = Quaternion.identity;
            lGrille.transform.localScale = Vector3.one * 1.18f;
            foreach (Transform t in lGrille.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            BoxCollider lBox = lGrille.AddComponent<BoxCollider>();
            lBox.center = new Vector3(0, 0, -0.02f);
            lBox.size = new Vector3(0.9f, 0.9f, 0.08f);
            lGrille.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            lCover.AddComponent<VentCover>().Setup(lGrille);
            Poi(c, pFaceLocal, string.IsNullOrEmpty(pPoi) ? "Vent" : pPoi, MapPoi.Kind.Vent);
        }

        // ---------------------------------------------------------------- lights, zones, markers

        private static void Lights(Ctx c)
        {
            Transform lLights = Child(c.result.root, "RoomLights");
            foreach (ArchRoom lRoom in c.spec.rooms)
            {
                if (lRoom.open || lRoom.noLight) continue;
                float y = FloorY(c, lRoom.floor) + c.spec.storey - c.spec.slab - 0.35f;
                float lSize = Mathf.Max(lRoom.rect.width, lRoom.rect.height);
                int lCount = lSize > 13f ? 2 : 1;
                for (int k = 0; k < lCount; k++)
                {
                    Vector2 p = lCount == 1 ? lRoom.rect.center : Vector2.Lerp(lRoom.rect.min, lRoom.rect.max, k == 0 ? 0.3f : 0.7f);
                    GameObject lObject = new GameObject("RoomLight_" + lRoom.name);
                    lObject.transform.SetParent(lLights, false);
                    lObject.transform.localPosition = new Vector3(p.x, y, p.y);
                    Light lLight = lObject.AddComponent<Light>();
                    lLight.type = LightType.Point;
                    lLight.range = Mathf.Clamp(lSize * (lCount == 1 ? 0.95f : 0.6f), 5f, 13f);
                    bool lCold = lRoom.floor < 0 || lRoom.type == RoomType.Security || lRoom.type == RoomType.Hangar || lRoom.type == RoomType.Garage;
                    lLight.color = lCold ? new Color(0.85f, 0.9f, 1f) : c.spec.style.lightColor;
                    lLight.intensity = lRoom.type == RoomType.Study ? 1.8f : lRoom.floor < 0 ? 1.8f : 2.6f;
                    if (lRoom.type == RoomType.Study) lLight.color = new Color(1f, 0.45f, 0.3f);
                    lLight.shadows = LightShadows.None;
                    lLight.renderMode = LightRenderMode.Auto;
                    // The fixture.
                    c.mesh.Box(ArchLib.LampGlow, new Vector3(p.x, FloorY(c, lRoom.floor) + c.spec.storey - c.spec.slab - 0.05f, p.y), new Vector3(0.45f, 0.06f, 0.45f), false);
                    c.mesh.Box(ArchLib.Brass, new Vector3(p.x, FloorY(c, lRoom.floor) + c.spec.storey - c.spec.slab - 0.01f, p.y), new Vector3(0.55f, 0.03f, 0.55f), false);
                }
            }
            c.mesh.Flush(c.result.shell, MeshPrefix + "_" + c.spec.name + "_fixtures", 0, false);
        }

        private static void Zones(Ctx c)
        {
            Transform lZones = Child(c.result.root, "SecurityZones");
            foreach (ArchRoom lRoom in c.spec.rooms)
            {
                if (lRoom.noZone) continue;
                GameObject lZone = new GameObject("Zone_" + lRoom.name + "_" + lRoom.tier + "_" + lRoom.access);
                lZone.transform.SetParent(lZones, false);
                lZone.transform.localPosition = new Vector3(lRoom.rect.center.x, FloorY(c, lRoom.floor), lRoom.rect.center.y);
                lZone.AddComponent<SecurityZone>().Setup(lRoom.tier, new Vector3(lRoom.rect.width, c.spec.storey, lRoom.rect.height), lRoom.access);
            }
        }

        private static void Poi(Ctx c, Vector3 pLocal, string pLabel, MapPoi.Kind pKind)
        {
            GameObject lPoi = new GameObject("Poi_" + pLabel);
            lPoi.transform.SetParent(c.result.systems, false);
            lPoi.transform.localPosition = pLocal;
            lPoi.AddComponent<MapPoi>().Setup(pKind, pLabel);
        }

        private static void Poi(ArchResult r, Vector3 pWorld, string pLabel, MapPoi.Kind pKind)
        {
            GameObject lPoi = new GameObject("Poi_" + pLabel);
            lPoi.transform.SetParent(r.systems, false);
            lPoi.transform.position = pWorld;
            lPoi.AddComponent<MapPoi>().Setup(pKind, pLabel);
        }
    }
}
