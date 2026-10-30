using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Systems;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The level-design kit the three map builders are written with: a height grid for the ground
    /// (plateaus, trenches, rivers), blocks and ramps, hollow multi-floor buildings with doors,
    /// windows, stairs, basements and roof access, vegetation, water, every systemic prop, and the
    /// AI / match markers. <see cref="Finish"/> generates cover points, lights the scene, bakes
    /// the NavMesh and saves.
    /// </summary>
    public class MapKit
    {
        public readonly MapDefinition Map;
        public readonly Transform Root, Env, Props, Markers, Lights;
        public readonly System.Random Rng;
        private readonly Material _toon, _glow, _water, _transparent;
        private readonly List<(Vector3 center, Vector3 size, Quaternion rotation)> _coverSources = new List<(Vector3, Vector3, Quaternion)>();
        private readonly string _scenePath;
        private int _routeCount;

        // ---------------------------------------------------------------- ground grid
        public float Cell { get; private set; } = 5f;
        private int _w, _h;
        private Vector2 _origin;
        private float[,] _height;
        private Color[,] _color;
        private bool[,] _hole;

        public MapKit(MapDefinition pMap, int pSeed)
        {
            Map = pMap;
            Rng = new System.Random(pSeed);
            _scenePath = EditorAssets.SCENES + "/" + pMap.sceneName + ".unity";
            EditorAssets.EnsureFolder(EditorAssets.SCENES);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            _toon = AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ART_RESOURCES + "/M_Toon.mat");
            _glow = AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ART_RESOURCES + "/M_Glow.mat");
            _water = AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ART_RESOURCES + "/M_Water.mat");
            _transparent = AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ART_RESOURCES + "/M_ToonTransparent.mat");

            Root = new GameObject("--- MAP " + pMap.displayName.ToUpperInvariant() + " ---").transform;
            Env = Group("Environment");
            Props = Group("Props");
            Markers = Group("Markers");
            Lights = Group("Lights");
        }

        public Transform Group(string pName, Transform pParent = null)
        {
            Transform lGroup = new GameObject(pName).transform;
            lGroup.SetParent(pParent != null ? pParent : Root, false);
            return lGroup;
        }

        public void Grid(int pWidth, int pHeight, float pCell, Vector2 pOrigin, Color pBase)
        {
            _w = pWidth;
            _h = pHeight;
            Cell = pCell;
            _origin = pOrigin;
            _height = new float[_w, _h];
            _color = new Color[_w, _h];
            _hole = new bool[_w, _h];
            for (int x = 0; x < _w; x++) for (int z = 0; z < _h; z++) _color[x, z] = GameArt.Vary(pBase, 0.06f, Rng);
        }

        private Vector2Int CellOf(float pX, float pZ) => new Vector2Int(Mathf.FloorToInt((pX - _origin.x) / Cell), Mathf.FloorToInt((pZ - _origin.y) / Cell));

        /// <summary>Raises or lowers the ground in a world rectangle (xMin, zMin, xMax, zMax).</summary>
        public void Height(Rect pRect, float pHeight, Color? pColor = null)
        {
            ForCells(pRect, (x, z) => { _height[x, z] = pHeight; if (pColor.HasValue) _color[x, z] = GameArt.Vary(pColor.Value, 0.05f, Rng); });
        }

        public void Paint(Rect pRect, Color pColor) => ForCells(pRect, (x, z) => _color[x, z] = GameArt.Vary(pColor, 0.05f, Rng));

        /// <summary>No ground at all (a basement pit, a stairwell to a tunnel).</summary>
        public void Hole(Rect pRect) => ForCells(pRect, (x, z) => _hole[x, z] = true);

        public float GroundAt(float pX, float pZ)
        {
            Vector2Int c = CellOf(pX, pZ);
            if (c.x < 0 || c.y < 0 || c.x >= _w || c.y >= _h) return 0f;
            return _height[c.x, c.y];
        }

        public Vector3 At(float pX, float pZ, float pUp = 0f) => new Vector3(pX, GroundAt(pX, pZ) + pUp, pZ);

        private void ForCells(Rect pRect, System.Action<int, int> pAction)
        {
            Vector2Int a = CellOf(pRect.xMin + 0.01f, pRect.yMin + 0.01f);
            Vector2Int b = CellOf(pRect.xMax - 0.01f, pRect.yMax - 0.01f);
            for (int x = Mathf.Max(0, a.x); x <= Mathf.Min(_w - 1, b.x); x++)
                for (int z = Mathf.Max(0, a.y); z <= Mathf.Min(_h - 1, b.y); z++) pAction(x, z);
        }

        /// <summary>Builds the ground: one column per cell down to -6 m, merged per row of equal cells; vertical cliffs between levels.</summary>
        private void BuildGround()
        {
            Transform lGround = Group("Ground", Env);
            List<(Mesh, Matrix4x4, Color)> lParts = new List<(Mesh, Matrix4x4, Color)>();
            Mesh lBox = LowPolyMesh.Box();
            const float BOTTOM = -8f;
            for (int z = 0; z < _h; z++)
            {
                int x = 0;
                while (x < _w)
                {
                    if (_hole[x, z]) { x++; continue; }
                    int lStart = x;
                    float lHeight = _height[x, z];
                    while (x + 1 < _w && !_hole[x + 1, z] && Mathf.Approximately(_height[x + 1, z], lHeight)) x++;
                    float lMinX = _origin.x + lStart * Cell, lMaxX = _origin.x + (x + 1) * Cell;
                    Vector3 lCenter = new Vector3((lMinX + lMaxX) * 0.5f, (lHeight + BOTTOM) * 0.5f, _origin.y + (z + 0.5f) * Cell);
                    Vector3 lSize = new Vector3(lMaxX - lMinX, lHeight - BOTTOM, Cell);
                    GameObject lCollider = new GameObject("G");
                    lCollider.transform.SetParent(lGround, false);
                    lCollider.transform.position = lCenter;
                    lCollider.AddComponent<BoxCollider>().size = lSize;
                    lCollider.isStatic = true;
                    for (int c = lStart; c <= x; c++)
                    {
                        Vector3 lCellCenter = new Vector3(_origin.x + (c + 0.5f) * Cell, (lHeight + BOTTOM) * 0.5f, lCenter.z);
                        lParts.Add((lBox, Matrix4x4.TRS(lCellCenter, Quaternion.identity, new Vector3(Cell, lHeight - BOTTOM, Cell)), _color[c, z]));
                    }
                    x++;
                }
            }
            for (int i = 0; i < lParts.Count; i += 2500)
            {
                Mesh lMesh = LowPolyMesh.Combine(lParts.GetRange(i, Mathf.Min(2500, lParts.Count - i)));
                lMesh = EditorAssets.SaveMesh(lMesh, Map.sceneName + "_Ground" + i / 2500);
                GameObject lVisual = new GameObject("GroundMesh" + i / 2500);
                lVisual.transform.SetParent(lGround, false);
                lVisual.AddComponent<MeshFilter>().sharedMesh = lMesh;
                lVisual.AddComponent<MeshRenderer>().sharedMaterial = _toon;
                lVisual.isStatic = true;
            }
        }

        // ---------------------------------------------------------------- primitives

        public GameObject Block(Transform pParent, string pName, Vector3 pCenter, Vector3 pSize, Color pColor, int pLayer = GameLayers.DEFAULT, bool pCollider = true, Quaternion? pRotation = null, bool pCover = false)
        {
            GameObject lBlock = new GameObject(pName) { layer = pLayer };
            lBlock.transform.SetParent(pParent != null ? pParent : Env, false);
            lBlock.transform.SetPositionAndRotation(pCenter, pRotation ?? Quaternion.identity);
            lBlock.transform.localScale = pSize;
            lBlock.AddComponent<MeshFilter>().sharedMesh = SharedBox;
            MeshRenderer lRenderer = lBlock.AddComponent<MeshRenderer>();
            lRenderer.sharedMaterial = _toon;
            SetColor(lRenderer, pColor);
            if (pCollider) lBlock.AddComponent<BoxCollider>();
            lBlock.isStatic = true;
            if (pCover) _coverSources.Add((pCenter, pSize, pRotation ?? Quaternion.identity));
            return lBlock;
        }

        private static Mesh _sharedBox;
        public static Mesh SharedBox => _sharedBox != null ? _sharedBox : _sharedBox = EditorAssets.SaveMesh(LowPolyMesh.Box(), "Kit_Box");

        /// <summary>A mesh object with the toon material (no collider unless asked).</summary>
        public GameObject Mesh(Transform pParent, string pName, Mesh pMesh, Vector3 pPosition, Vector3 pScale, Color pColor, Quaternion? pRotation = null, bool pCollider = false, Material pMaterial = null)
        {
            GameObject lObject = new GameObject(pName);
            lObject.transform.SetParent(pParent != null ? pParent : Props, false);
            lObject.transform.SetPositionAndRotation(pPosition, pRotation ?? Quaternion.identity);
            lObject.transform.localScale = pScale;
            lObject.AddComponent<MeshFilter>().sharedMesh = pMesh;
            MeshRenderer lRenderer = lObject.AddComponent<MeshRenderer>();
            lRenderer.sharedMaterial = pMaterial != null ? pMaterial : _toon;
            SetColor(lRenderer, pColor);
            if (pCollider) lObject.AddComponent<MeshCollider>().sharedMesh = pMesh;
            return lObject;
        }

        /// <summary>
        /// One material per (base, quantised colour), saved as assets: scene renderers keep their
        /// colour without property blocks, and the SRP batcher batches them all (same shader).
        /// </summary>
        private static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

        private void SetColor(MeshRenderer pRenderer, Color pColor)
        {
            Material lBase = pRenderer.sharedMaterial != null ? pRenderer.sharedMaterial : _toon;
            Color lQuantised = new Color(Mathf.Round(pColor.r * 24f) / 24f, Mathf.Round(pColor.g * 24f) / 24f, Mathf.Round(pColor.b * 24f) / 24f, Mathf.Round(pColor.a * 8f) / 8f);
            string lKey = lBase.name + "_" + ((int)(lQuantised.r * 24)).ToString("X2") + ((int)(lQuantised.g * 24)).ToString("X2") + ((int)(lQuantised.b * 24)).ToString("X2") + ((int)(lQuantised.a * 8)).ToString("X1");
            if (!_materials.TryGetValue(lKey, out Material lMaterial) || lMaterial == null)
            {
                string lPath = EditorAssets.ROOT + "/Art/Generated/Materials/" + lKey + ".mat";
                lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lPath);
                if (lMaterial == null)
                {
                    EditorAssets.EnsureFolder(EditorAssets.ROOT + "/Art/Generated/Materials");
                    lMaterial = new Material(lBase);
                    AssetDatabase.CreateAsset(lMaterial, lPath);
                }
                lMaterial.SetColor("_BaseColor", lQuantised);
                _materials[lKey] = lMaterial;
            }
            pRenderer.sharedMaterial = lMaterial;
        }

        /// <summary>A walkable slope from one point to another (bottom centre to top centre).</summary>
        public GameObject Ramp(Transform pParent, Vector3 pFrom, Vector3 pTo, float pWidth, Color pColor, float pThickness = 0.4f)
        {
            Vector3 lDir = pTo - pFrom;
            Quaternion lRot = Quaternion.LookRotation(lDir.normalized, Vector3.up);
            Vector3 lCenter = (pFrom + pTo) * 0.5f + lRot * Vector3.down * (pThickness * 0.5f);
            return Block(pParent, "Ramp", lCenter, new Vector3(pWidth, pThickness, lDir.magnitude + 0.2f), pColor, GameLayers.DEFAULT, true, lRot);
        }

        /// <summary>Stairs: a ramp collider (smooth for the controller and the NavMesh) and visual steps.</summary>
        public void Stairs(Transform pParent, Vector3 pBottom, Vector3 pTop, float pWidth, Color pColor)
        {
            GameObject lRamp = Ramp(pParent, pBottom, pTop, pWidth, pColor, 0.2f);
            lRamp.GetComponent<MeshRenderer>().enabled = false;
            Vector3 lFlat = new Vector3(pTop.x - pBottom.x, 0, pTop.z - pBottom.z);
            float lRise = pTop.y - pBottom.y;
            int lSteps = Mathf.Max(2, Mathf.RoundToInt(lRise / 0.3f));
            Quaternion lRot = Quaternion.LookRotation(lFlat.normalized);
            for (int i = 0; i < lSteps; i++)
            {
                float t = (i + 0.5f) / lSteps;
                Vector3 lPos = pBottom + lFlat * t + Vector3.up * (lRise * (i + 1) / lSteps * 0.5f);
                Block(pParent, "Step", lPos, new Vector3(pWidth, lRise * (i + 1) / lSteps, lFlat.magnitude / lSteps), pColor * (i % 2 == 0 ? 1f : 0.94f), GameLayers.DEFAULT, false, lRot);
            }
        }

        /// <summary>A climbable surface: ivy, a ladder, scaffolding, a drainpipe (layer Climbable).</summary>
        public GameObject Climbable(Transform pParent, Vector3 pCenter, Vector3 pSize, Quaternion pRotation, Color pColor, bool pLadder)
        {
            GameObject lClimb = Block(pParent, pLadder ? "Ladder" : "Ivy", pCenter, pSize, pColor, GameLayers.CLIMBABLE, true, pRotation);
            if (pLadder)
            {
                int lRungs = Mathf.RoundToInt(pSize.y / 0.4f);
                for (int i = 0; i < lRungs; i++)
                    Block(lClimb.transform.parent, "Rung", pCenter + pRotation * new Vector3(0, -pSize.y * 0.5f + (i + 0.5f) * pSize.y / lRungs, -pSize.z * 0.5f - 0.05f), new Vector3(pSize.x, 0.06f, 0.08f), pColor * 0.7f, GameLayers.DEFAULT, false, pRotation);
            }
            return lClimb;
        }

        // ---------------------------------------------------------------- buildings

        public class Opening
        {
            /// <summary>0 = south (-z), 1 = east (+x), 2 = north (+z), 3 = west (-x).</summary>
            public int side;
            /// <summary>Position along the wall, 0..1.</summary>
            public float at;
            public float width = 1.6f;
            public float bottom;
            public float top = 2.4f;
            public int floor;
            public Opening(int pSide, float pAt, int pFloor = 0, float pWidth = 1.6f, float pBottom = 0f, float pTop = 2.4f) { side = pSide; at = pAt; floor = pFloor; width = pWidth; bottom = pBottom; top = pTop; }
        }

        public class BuildingSpec
        {
            public string name = "Building";
            public Vector3 origin;
            public Vector2 size = new Vector2(12, 10);
            public float yaw;
            public int floors = 1;
            public float floorHeight = 3.4f;
            public Color wall = new Color(0.9f, 0.85f, 0.75f);
            public Color trim = new Color(0.6f, 0.5f, 0.45f);
            public Color floor = new Color(0.7f, 0.6f, 0.5f);
            public Color roof = new Color(0.75f, 0.35f, 0.3f);
            public List<Opening> openings = new List<Opening>();
            /// <summary>Windows on every floor, every n metres (0 = none).</summary>
            public float windowSpacing = 4f;
            public bool stairs = true;
            public bool roofAccess = true;
            public bool pitchedRoof;
            public bool basement;
            public bool parapet = true;
            /// <summary>Climbable drainpipe / ivy on this side up to the roof (-1 = none).</summary>
            public int climbSide = -1;
            public float climbAt = 0.15f;
        }

        public class BuildingResult
        {
            public Transform root;
            public Transform interior;
            public Vector3 Local(float pX, float pY, float pZ) => root.TransformPoint(new Vector3(pX, pY, pZ));
            public Quaternion Rotation => root.rotation;
            public float floorHeight;
            public Vector2 size;
            public float FloorY(int pFloor) => root.position.y + pFloor * floorHeight;
        }

        /// <summary>
        /// A hollow building: walls per floor with doors and windows, floor slabs with a stairwell,
        /// a staircase per floor, a flat roof (parapet, hatch) or a pitched one, an optional
        /// basement under the ground floor, an optional climbable side.
        /// </summary>
        public BuildingResult Building(BuildingSpec pSpec)
        {
            Transform lRoot = Group(pSpec.name, Env);
            lRoot.position = pSpec.origin;
            lRoot.rotation = Quaternion.Euler(0, pSpec.yaw, 0);
            Transform lInterior = Group("Interior", lRoot);
            BuildingResult lResult = new BuildingResult { root = lRoot, interior = lInterior, floorHeight = pSpec.floorHeight, size = pSpec.size };
            float W = pSpec.size.x, D = pSpec.size.y, H = pSpec.floorHeight;
            const float T = 0.3f;

            // Automatic windows: openings every windowSpacing on each side and floor, unless a door is there.
            List<Opening> lOpenings = new List<Opening>(pSpec.openings);
            if (pSpec.windowSpacing > 0f)
                for (int f = 0; f < pSpec.floors; f++)
                    for (int s = 0; s < 4; s++)
                    {
                        float lLength = s % 2 == 0 ? W : D;
                        int lCount = Mathf.FloorToInt(lLength / pSpec.windowSpacing);
                        for (int i = 0; i < lCount; i++)
                        {
                            float lAt = (i + 0.5f) / lCount;
                            bool lClash = lOpenings.Exists(o => o.side == s && o.floor == f && Mathf.Abs(o.at - lAt) * lLength < 1.6f);
                            if (!lClash) lOpenings.Add(new Opening(s, lAt, f, 1.1f, 1.0f, 2.1f));
                        }
                    }

            int lStartFloor = pSpec.basement ? -1 : 0;
            for (int f = lStartFloor; f < pSpec.floors; f++)
            {
                float y = f * H;
                for (int s = 0; s < 4; s++)
                {
                    List<Opening> lWallOpenings = lOpenings.FindAll(o => o.side == s && o.floor == f);
                    Wall(lRoot, s, W, D, y, H, T, lWallOpenings, f < 0 ? pSpec.trim : pSpec.wall, pSpec.trim);
                }
            }

            // Slabs with a stairwell in the north-east corner.
            Vector2 lWell = new Vector2(W * 0.5f - 2.2f, D * 0.5f - 3.2f);
            for (int f = lStartFloor; f <= pSpec.floors; f++)
            {
                float y = f * H;
                bool lTop = f == pSpec.floors;
                bool lCutWell = pSpec.stairs && (f > lStartFloor) && (!lTop || pSpec.roofAccess);
                Color lColor = lTop ? pSpec.roof : pSpec.floor;
                if (f == 0 && !pSpec.basement) { Slab(lRoot, W, D, y - 0.15f, 0.3f, lColor, false, lWell); continue; }
                Slab(lRoot, W, D, y - 0.15f, 0.3f, lColor, lCutWell, lWell);
            }

            // Stairs along the east wall, going north, one flight per floor.
            if (pSpec.stairs)
                for (int f = lStartFloor; f < pSpec.floors; f++)
                {
                    if (f == pSpec.floors - 1 && !pSpec.roofAccess) break;
                    float y = f * H;
                    Vector3 lBottom = new Vector3(lWell.x, y, lWell.y - 3.6f);
                    Vector3 lTopPoint = new Vector3(lWell.x, y + H, lWell.y + 1.4f);
                    Stairs(lInterior, lRoot.TransformPoint(lBottom), lRoot.TransformPoint(lTopPoint), 1.6f, pSpec.trim * 1.1f);
                }

            float lRoofY = pSpec.floors * H;
            if (pSpec.parapet && !pSpec.pitchedRoof)
                for (int s = 0; s < 4; s++) Wall(lRoot, s, W, D, lRoofY, 1.0f, 0.25f, new List<Opening>(), pSpec.trim, pSpec.trim);
            if (pSpec.pitchedRoof)
            {
                Mesh lPrism = LowPolyMesh.Cached("kit.prism", () => Prism());
                Mesh(lRoot, "Roof", EditorAssets.SaveMesh(Object.Instantiate(lPrism), "Kit_Prism"), lRoot.TransformPoint(new Vector3(0, lRoofY, 0)), new Vector3(W + 0.8f, Mathf.Min(W, D) * 0.4f, D + 0.8f), pSpec.roof, lRoot.rotation, true);
            }

            if (pSpec.climbSide >= 0)
            {
                Vector3 lOut = SideNormal(pSpec.climbSide);
                float lLength = pSpec.climbSide % 2 == 0 ? W : D;
                Vector3 lAlong = SideTangent(pSpec.climbSide) * ((pSpec.climbAt - 0.5f) * lLength);
                Vector3 lPos = lOut * ((pSpec.climbSide % 2 == 0 ? D : W) * 0.5f + 0.2f) + lAlong + Vector3.up * (lRoofY * 0.5f + 0.4f);
                Climbable(lRoot, lRoot.TransformPoint(lPos), new Vector3(1.2f, lRoofY + 0.8f, 0.25f), lRoot.rotation * Quaternion.LookRotation(-lOut), new Color(0.35f, 0.6f, 0.3f), false);
            }
            return lResult;
        }

        private static Vector3 SideNormal(int pSide) => pSide switch { 0 => Vector3.back, 1 => Vector3.right, 2 => Vector3.forward, _ => Vector3.left };
        private static Vector3 SideTangent(int pSide) => pSide switch { 0 => Vector3.right, 1 => Vector3.forward, 2 => Vector3.left, _ => Vector3.back };

        private void Wall(Transform pRoot, int pSide, float W, float D, float y, float H, float T, List<Opening> pOpenings, Color pColor, Color pTrim)
        {
            float lLength = pSide % 2 == 0 ? W : D;
            float lOffset = (pSide % 2 == 0 ? D : W) * 0.5f - T * 0.5f;
            Vector3 lNormal = SideNormal(pSide), lTangent = SideTangent(pSide);
            pOpenings.Sort((a, b) => a.at.CompareTo(b.at));

            float lCursor = -lLength * 0.5f;
            void Piece(float pFrom, float pTo, float pBottom, float pTop)
            {
                if (pTo - pFrom < 0.05f || pTop - pBottom < 0.05f) return;
                Vector3 lCenter = lNormal * lOffset + lTangent * ((pFrom + pTo) * 0.5f) + Vector3.up * (y + (pBottom + pTop) * 0.5f);
                Vector3 lSize = pSide % 2 == 0 ? new Vector3(pTo - pFrom, pTop - pBottom, T) : new Vector3(T, pTop - pBottom, pTo - pFrom);
                Block(pRoot, "Wall", pRoot.TransformPoint(lCenter), lSize, pColor, GameLayers.DEFAULT, true, pRoot.rotation);
            }

            foreach (Opening lOpening in pOpenings)
            {
                float lMid = (lOpening.at - 0.5f) * lLength;
                float lA = Mathf.Max(-lLength * 0.5f, lMid - lOpening.width * 0.5f);
                float lB = Mathf.Min(lLength * 0.5f, lMid + lOpening.width * 0.5f);
                Piece(lCursor, lA, 0f, H);
                Piece(lA, lB, 0f, lOpening.bottom);
                Piece(lA, lB, Mathf.Min(H, lOpening.top), H);
                // A sill / frame accent on windows.
                if (lOpening.bottom > 0.1f)
                    Block(pRoot, "Sill", pRoot.TransformPoint(lNormal * (lOffset + 0.1f) + lTangent * lMid + Vector3.up * (y + lOpening.bottom)), pSide % 2 == 0 ? new Vector3(lB - lA + 0.2f, 0.12f, T + 0.2f) : new Vector3(T + 0.2f, 0.12f, lB - lA + 0.2f), pTrim, GameLayers.DEFAULT, true, pRoot.rotation);
                lCursor = lB;
            }
            Piece(lCursor, lLength * 0.5f, 0f, H);
        }

        private void Slab(Transform pRoot, float W, float D, float y, float pThickness, Color pColor, bool pWell, Vector2 pWell2)
        {
            if (!pWell)
            {
                Block(pRoot, "Slab", pRoot.TransformPoint(new Vector3(0, y, 0)), new Vector3(W, pThickness, D), pColor, GameLayers.DEFAULT, true, pRoot.rotation);
                return;
            }
            // Hole of 2 x 5 m around the stairwell (x: wellX-1..wellX+1, z: wellZ-3.6..wellZ+1.4).
            float hx0 = pWell2.x - 1f, hx1 = pWell2.x + 1.2f, hz0 = pWell2.y - 3.4f, hz1 = pWell2.y + 1.4f;
            void Part(float x0, float x1, float z0, float z1)
            {
                if (x1 - x0 < 0.05f || z1 - z0 < 0.05f) return;
                Block(pRoot, "Slab", pRoot.TransformPoint(new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f)), new Vector3(x1 - x0, pThickness, z1 - z0), pColor, GameLayers.DEFAULT, true, pRoot.rotation);
            }
            Part(-W * 0.5f, hx0, -D * 0.5f, D * 0.5f);
            Part(hx1, W * 0.5f, -D * 0.5f, D * 0.5f);
            Part(hx0, hx1, -D * 0.5f, hz0);
            Part(hx0, hx1, hz1, D * 0.5f);
        }

        private static Mesh Prism()
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0, 1, -0.5f);
            Vector3 d = new Vector3(-0.5f, 0, 0.5f), e = new Vector3(0.5f, 0, 0.5f), f = new Vector3(0, 1, 0.5f);
            LowPolyMesh.AddTri(v, t, a, c, b);
            LowPolyMesh.AddTri(v, t, d, e, f);
            LowPolyMesh.AddQuad(v, t, a, d, f, c);
            LowPolyMesh.AddQuad(v, t, b, c, f, e);
            LowPolyMesh.AddQuad(v, t, a, b, e, d);
            return LowPolyMesh.Build(v, t, Color.white);
        }

        /// <summary>Interior partition with one doorway, in building-local coordinates.</summary>
        public void Partition(BuildingResult pBuilding, int pFloor, Vector3 pFromLocal, Vector3 pToLocal, float pDoorAt, Color pColor)
        {
            float y = pFloor * pBuilding.floorHeight;
            Vector3 lDir = pToLocal - pFromLocal;
            float lLength = lDir.magnitude;
            Vector3 lUnit = lDir / lLength;
            float lDoorMid = pDoorAt * lLength;
            void Piece(float a, float b, float bottom, float top)
            {
                if (b - a < 0.05f) return;
                Vector3 lCenter = pFromLocal + lUnit * ((a + b) * 0.5f) + Vector3.up * (y + (bottom + top) * 0.5f);
                Block(pBuilding.root, "Partition", pBuilding.root.TransformPoint(lCenter), new Vector3(0.2f, top - bottom, b - a), pColor, GameLayers.DEFAULT, true, pBuilding.root.rotation * Quaternion.LookRotation(lUnit));
            }
            if (pDoorAt < 0f) { Piece(0, lLength, 0, pBuilding.floorHeight); return; }
            Piece(0, lDoorMid - 0.8f, 0, pBuilding.floorHeight);
            Piece(lDoorMid - 0.8f, lDoorMid + 0.8f, 2.4f, pBuilding.floorHeight);
            Piece(lDoorMid + 0.8f, lLength, 0, pBuilding.floorHeight);
        }

        // ---------------------------------------------------------------- nature

        public enum TreeKind { Round, Pine, Palm, Blossom, Dead }

        private readonly Dictionary<string, Mesh> _treeMeshes = new Dictionary<string, Mesh>();

        public void Tree(Vector3 pPosition, TreeKind pKind, float pScale, Color? pTint = null)
        {
            int lVariant = Rng.Next(3);
            string lKey = pKind + "_" + lVariant + "_" + (pTint.HasValue ? ColorUtility.ToHtmlStringRGB(pTint.Value) : "d");
            if (!_treeMeshes.TryGetValue(lKey, out Mesh lMesh))
            {
                lMesh = EditorAssets.SaveMesh(TreeMesh(pKind, lVariant, pTint), "Tree_" + lKey);
                _treeMeshes[lKey] = lMesh;
            }
            GameObject lTree = new GameObject("Tree");
            lTree.transform.SetParent(TreesGroup, false);
            lTree.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, (float)Rng.NextDouble() * 360f, 0));
            lTree.transform.localScale = Vector3.one * pScale;
            lTree.AddComponent<MeshFilter>().sharedMesh = lMesh;
            lTree.AddComponent<MeshRenderer>().sharedMaterial = _toon;
            CapsuleCollider lTrunk = lTree.AddComponent<CapsuleCollider>();
            lTrunk.radius = pKind == TreeKind.Palm ? 0.25f : 0.35f;
            lTrunk.height = 4f;
            lTrunk.center = new Vector3(0, 2f, 0);
            lTree.isStatic = true;
        }

        private Transform _trees;
        private Transform TreesGroup => _trees != null ? _trees : _trees = Group("TreesAll", Env);

        private Mesh TreeMesh(TreeKind pKind, int pVariant, Color? pTint)
        {
            List<(Mesh, Matrix4x4, Color)> lParts = new List<(Mesh, Matrix4x4, Color)>();
            Color lTrunk = new Color(0.45f, 0.3f, 0.2f);
            System.Random r = new System.Random(pVariant * 31 + (int)pKind * 7);
            switch (pKind)
            {
                case TreeKind.Round:
                case TreeKind.Blossom:
                {
                    Color lLeaf = pTint ?? (pKind == TreeKind.Blossom ? new Color(0.95f, 0.65f, 0.85f) : new Color(0.45f, 0.72f, 0.35f));
                    lParts.Add((LowPolyMesh.Cylinder(6, 0.7f), Matrix4x4.TRS(new Vector3(0, 1.5f, 0), Quaternion.identity, new Vector3(0.45f, 3f, 0.45f)), lTrunk));
                    int lBlobs = 2 + pVariant;
                    for (int i = 0; i < lBlobs; i++)
                    {
                        Vector3 lOff = new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() * 0.8f, (float)r.NextDouble() - 0.5f) * 1.6f;
                        float s = 2.4f + (float)r.NextDouble() * 1.2f;
                        lParts.Add((LowPolyMesh.Icosphere(1, 0.12f, i + pVariant * 5), Matrix4x4.TRS(new Vector3(0, 3.6f, 0) + lOff, Quaternion.identity, Vector3.one * s), GameArt.Vary(lLeaf, 0.12f, r)));
                    }
                    break;
                }
                case TreeKind.Pine:
                {
                    Color lLeaf = pTint ?? new Color(0.25f, 0.5f, 0.38f);
                    lParts.Add((LowPolyMesh.Cylinder(6, 0.7f), Matrix4x4.TRS(new Vector3(0, 1f, 0), Quaternion.identity, new Vector3(0.4f, 2f, 0.4f)), lTrunk));
                    int lTiers = 3 + pVariant;
                    for (int i = 0; i < lTiers; i++)
                    {
                        float s = 3.4f - i * 0.6f;
                        lParts.Add((LowPolyMesh.Cone(7), Matrix4x4.TRS(new Vector3(0, 1.4f + i * 1.25f, 0), Quaternion.Euler(0, i * 25f, 0), new Vector3(s, 2.2f, s)), GameArt.Vary(lLeaf, 0.1f, r)));
                    }
                    break;
                }
                case TreeKind.Palm:
                {
                    Color lLeaf = pTint ?? new Color(0.35f, 0.7f, 0.35f);
                    float lBend = 0.15f + pVariant * 0.1f;
                    for (int i = 0; i < 6; i++)
                        lParts.Add((LowPolyMesh.Cylinder(6, 0.85f), Matrix4x4.TRS(new Vector3(i * i * lBend * 0.08f, 0.6f + i * 1.1f, 0), Quaternion.Euler(0, 0, -i * lBend * 10f), new Vector3(0.36f - i * 0.02f, 1.2f, 0.36f - i * 0.02f)), new Color(0.6f, 0.45f, 0.3f)));
                    Vector3 lTop = new Vector3(25f * lBend * 0.08f, 6.8f, 0);
                    for (int i = 0; i < 7; i++)
                        lParts.Add((LowPolyMesh.Box(), Matrix4x4.TRS(lTop + Quaternion.Euler(0, i * 51f, 0) * new Vector3(0, 0, 1.4f), Quaternion.Euler(25f, i * 51f, 0), new Vector3(0.7f, 0.06f, 3f)), GameArt.Vary(lLeaf, 0.1f, r)));
                    break;
                }
                case TreeKind.Dead:
                {
                    lParts.Add((LowPolyMesh.Cylinder(5, 0.5f), Matrix4x4.TRS(new Vector3(0, 2f, 0), Quaternion.identity, new Vector3(0.4f, 4f, 0.4f)), new Color(0.35f, 0.3f, 0.28f)));
                    for (int i = 0; i < 3; i++)
                        lParts.Add((LowPolyMesh.Cylinder(5, 0.4f), Matrix4x4.TRS(new Vector3(0, 2.5f + i * 0.6f, 0), Quaternion.Euler(50f, i * 120f, 0), new Vector3(0.15f, 1.6f, 0.15f)), new Color(0.35f, 0.3f, 0.28f)));
                    break;
                }
            }
            return LowPolyMesh.Combine(lParts);
        }

        public void Rock(Vector3 pPosition, float pScale, Color pColor, bool pCover = true)
        {
            int lVariant = Rng.Next(4);
            Mesh lMesh = LowPolyMesh.Cached("rock" + lVariant, () => LowPolyMesh.Icosphere(1, 0.22f, 100 + lVariant));
            lMesh = EditorAssets.SaveMesh(Object.Instantiate(lMesh), "Rock" + lVariant);
            Vector3 lScale = new Vector3(pScale * (0.8f + (float)Rng.NextDouble() * 0.6f), pScale * (0.5f + (float)Rng.NextDouble() * 0.4f), pScale * (0.8f + (float)Rng.NextDouble() * 0.6f));
            GameObject lRock = Mesh(Env, "Rock", lMesh, pPosition, lScale, GameArt.Vary(pColor, 0.08f, Rng), Quaternion.Euler(0, (float)Rng.NextDouble() * 360f, 0), true);
            lRock.isStatic = true;
            if (pCover && pScale > 1f) _coverSources.Add((pPosition + Vector3.up * lScale.y * 0.3f, lScale * 0.8f, Quaternion.identity));
        }

        /// <summary>Tall grass: crouch inside it and the AI barely sees you.</summary>
        public void Grass(Vector3 pCenter, Vector2 pSize, Color pColor)
        {
            Transform lPatch = Group("TallGrass", Env);
            lPatch.position = pCenter;
            int lCount = Mathf.RoundToInt(pSize.x * pSize.y * 1.4f);
            List<(Mesh, Matrix4x4, Color)> lParts = new List<(Mesh, Matrix4x4, Color)>();
            Mesh lBlade = LowPolyMesh.Cone(3);
            for (int i = 0; i < lCount; i++)
            {
                Vector3 lPos = new Vector3(((float)Rng.NextDouble() - 0.5f) * pSize.x, 0, ((float)Rng.NextDouble() - 0.5f) * pSize.y);
                float lHeight = 1.1f + (float)Rng.NextDouble() * 0.5f;
                lParts.Add((lBlade, Matrix4x4.TRS(lPos, Quaternion.Euler(((float)Rng.NextDouble() - 0.5f) * 20f, (float)Rng.NextDouble() * 360f, ((float)Rng.NextDouble() - 0.5f) * 20f), new Vector3(0.35f, lHeight, 0.35f)), GameArt.Vary(pColor, 0.15f, Rng)));
            }
            Mesh lMesh = EditorAssets.SaveMesh(LowPolyMesh.Combine(lParts), Map.sceneName + "_Grass_" + Mathf.RoundToInt(pCenter.x) + "_" + Mathf.RoundToInt(pCenter.z));
            GameObject lVisual = Mesh(lPatch, "Blades", lMesh, pCenter, Vector3.one, Color.white);
            lVisual.isStatic = true;
            GameObject lTrigger = new GameObject("GrassVolume") { layer = GameLayers.GRASS };
            lTrigger.transform.SetParent(lPatch, false);
            lTrigger.transform.position = pCenter + Vector3.up * 0.7f;
            BoxCollider lBox = lTrigger.AddComponent<BoxCollider>();
            lBox.isTrigger = true;
            lBox.size = new Vector3(pSize.x, 1.4f, pSize.y);
            lTrigger.AddComponent<TallGrass>();
        }

        public void Water(Vector3 pCenter, Vector2 pSize)
        {
            GameObject lWater = new GameObject("Water") { layer = GameLayers.WATER };
            lWater.transform.SetParent(Env, false);
            lWater.transform.position = pCenter;
            lWater.transform.localScale = new Vector3(pSize.x, 1f, pSize.y);
            lWater.AddComponent<MeshFilter>().sharedMesh = EditorAssets.SaveMesh(WaterMesh(), "Kit_WaterPlane");
            lWater.AddComponent<MeshRenderer>().sharedMaterial = _water;
        }

        private static Mesh WaterMesh()
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            const int N = 24;
            for (int x = 0; x < N; x++)
                for (int z = 0; z < N; z++)
                {
                    float x0 = (float)x / N - 0.5f, x1 = (float)(x + 1) / N - 0.5f, z0 = (float)z / N - 0.5f, z1 = (float)(z + 1) / N - 0.5f;
                    LowPolyMesh.AddQuad(v, t, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0));
                }
            return LowPolyMesh.Build(v, t, Color.white);
        }

        // ---------------------------------------------------------------- props

        public GameObject Crate(Vector3 pPosition, float pSize = 1.1f, bool pFlammable = true, Transform pParent = null)
        {
            GameObject lCrate = Block(pParent ?? Props, "Crate", pPosition + Vector3.up * pSize * 0.5f, Vector3.one * pSize, new Color(0.72f, 0.52f, 0.32f), GameLayers.DEFAULT, true, Quaternion.Euler(0, (float)Rng.NextDouble() * 20f - 10f, 0), true);
            lCrate.isStatic = false;
            if (pFlammable) lCrate.AddComponent<Flammable>().Setup(12f, 2.6f, false, pSize);
            return lCrate;
        }

        public void CrateStack(Vector3 pPosition, int pCount)
        {
            for (int i = 0; i < pCount; i++)
                Crate(pPosition + new Vector3((i % 2) * 1.15f, (i / 2) * 1.1f, ((i / 4) % 2) * 1.15f), 1.1f);
        }

        public GameObject Barrel(Vector3 pPosition, bool pExplosive)
        {
            GameObject lBarrel = Mesh(Props, pExplosive ? "RedBarrel" : "Barrel", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(8), "Kit_Cyl8"), pPosition + Vector3.up * 0.55f, new Vector3(0.7f, 1.1f, 0.7f), pExplosive ? new Color(0.9f, 0.25f, 0.2f) : new Color(0.35f, 0.5f, 0.6f));
            lBarrel.AddComponent<CapsuleCollider>().height = 1f;
            if (pExplosive) lBarrel.AddComponent<ExplosiveProp>();
            else lBarrel.AddComponent<Flammable>().Setup(10f, 2.5f, true, 0.8f);
            _coverSources.Add((pPosition + Vector3.up * 0.55f, new Vector3(0.7f, 1.1f, 0.7f), Quaternion.identity));
            return lBarrel;
        }

        public GameObject Car(Vector3 pPosition, float pYaw, Color pColor, Transform pParent = null)
        {
            Transform lCar = Group("Car", pParent ?? Props);
            lCar.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            GameObject lPrefab = Resources.Load<GameObject>("Models/Car");
            if (lPrefab != null)
            {
                GameObject lModel = (GameObject)PrefabUtility.InstantiatePrefab(lPrefab, lCar);
                lModel.transform.localPosition = Vector3.zero;
                lModel.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                foreach (MeshRenderer lRenderer in lModel.GetComponentsInChildren<MeshRenderer>()) lRenderer.sharedMaterial = _toon;
            }
            else
            {
                Block(lCar, "Body", lCar.TransformPoint(new Vector3(0, 0.75f, 0)), new Vector3(1.9f, 0.9f, 4.3f), pColor, GameLayers.DEFAULT, false, lCar.rotation);
                Block(lCar, "Cabin", lCar.TransformPoint(new Vector3(0, 1.5f, -0.2f)), new Vector3(1.7f, 0.7f, 2.2f), new Color(0.7f, 0.85f, 0.95f), GameLayers.DEFAULT, false, lCar.rotation);
                for (int i = 0; i < 4; i++)
                    Mesh(lCar, "Wheel", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(10), "Kit_Cyl10"), lCar.TransformPoint(new Vector3(i % 2 == 0 ? -0.95f : 0.95f, 0.35f, i < 2 ? 1.4f : -1.4f)), new Vector3(0.7f, 0.25f, 0.7f), new Color(0.15f, 0.15f, 0.17f), lCar.rotation * Quaternion.Euler(0, 0, 90));
            }
            BoxCollider lBox = lCar.gameObject.AddComponent<BoxCollider>();
            lBox.center = new Vector3(0, 0.9f, 0);
            lBox.size = new Vector3(1.9f, 1.8f, 4.3f);
            _coverSources.Add((lCar.TransformPoint(new Vector3(0, 0.6f, 0)), new Vector3(1.9f, 1.2f, 4.3f), lCar.rotation));
            return lCar.gameObject;
        }

        /// <summary>A patrol car looping on a route (positions follow the match clock on every machine).</summary>
        public void PatrolCar(float pSpeed, float pOffset, Color pColor, params Vector3[] pPoints)
        {
            Transform lRoot = Group("PatrolCar", Props);
            lRoot.gameObject.layer = GameLayers.INTERACTABLE;
            Transform lRoute = Group("Route", lRoot);
            foreach (Vector3 lPoint in pPoints)
            {
                Transform lWaypoint = new GameObject("WP").transform;
                lWaypoint.SetParent(lRoute, false);
                lWaypoint.position = lPoint;
            }
            Vector3 lDir = (pPoints[1] - pPoints[0]).normalized;
            GameObject lCar = Car(pPoints[0], Quaternion.LookRotation(lDir).eulerAngles.y, pColor, lRoot);
            _coverSources.RemoveAt(_coverSources.Count - 1);
            foreach (Transform lChild in lCar.GetComponentsInChildren<Transform>()) lChild.gameObject.isStatic = false;
            Light lHead = new GameObject("Headlight").AddComponent<Light>();
            lHead.transform.SetParent(lCar.transform, false);
            lHead.transform.localPosition = new Vector3(0, 0.9f, 2.3f);
            lHead.type = LightType.Spot;
            lHead.spotAngle = 70f;
            lHead.range = 22f;
            lHead.intensity = 3f;
            lHead.color = new Color(1f, 0.95f, 0.8f);
            lRoot.gameObject.AddComponent<VehiclePatrol>().Setup(lRoute, pSpeed, lCar.transform, pOffset);
        }

        /// <summary>A table with poisonable glasses (and an activity node for the target).</summary>
        public void BarTable(Vector3 pPosition, float pYaw, int pZone, int pGlasses = 2, bool pActivity = true, float pTableHeight = 1.0f)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Block(Props, "Table", pPosition + Vector3.up * pTableHeight * 0.5f, new Vector3(1.4f, pTableHeight, 0.8f), new Color(0.55f, 0.35f, 0.25f), GameLayers.DEFAULT, true, lRot, true);
            for (int i = 0; i < pGlasses; i++)
            {
                Vector3 lPos = pPosition + lRot * new Vector3(-0.3f + i * 0.6f / Mathf.Max(1, pGlasses - 1), pTableHeight, 0);
                GameObject lGlass = new GameObject("Drink") { layer = GameLayers.INTERACTABLE };
                lGlass.transform.SetParent(Props, false);
                lGlass.transform.position = lPos;
                Mesh(lGlass.transform, "Glass", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(8, 1.2f), "Kit_Glass"), lPos + Vector3.up * 0.1f, new Vector3(0.12f, 0.2f, 0.12f), new Color(0.85f, 0.95f, 1f), null, false, _transparent);
                GameObject lLiquid = Mesh(lGlass.transform, "Liquid", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(8), "Kit_Cyl8"), lPos + Vector3.up * 0.07f, new Vector3(0.1f, 0.12f, 0.1f), new Color(0.85f, 0.45f, 0.2f));
                lLiquid.name = "Liquid";
                BoxCollider lBox = lGlass.AddComponent<BoxCollider>();
                lBox.size = new Vector3(0.3f, 0.4f, 0.3f);
                lBox.center = new Vector3(0, 0.15f, 0);
                lBox.isTrigger = true;
                lGlass.AddComponent<Drink>();
            }
            if (pActivity) Activity(ActivityType.Drink, pPosition + lRot * new Vector3(0, 0, -1.1f), pYaw, pZone, 14f);
        }

        public void Toilet(Vector3 pPosition, float pYaw, int pZone)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Transform lRoot = Group("Toilet", Props);
            lRoot.SetPositionAndRotation(pPosition, lRot);
            Block(lRoot, "Bowl", pPosition + lRot * new Vector3(0, 0.25f, 0.1f), new Vector3(0.5f, 0.5f, 0.6f), Color.white, GameLayers.DEFAULT, true, lRot);
            Block(lRoot, "Tank", pPosition + lRot * new Vector3(0, 0.7f, -0.25f), new Vector3(0.55f, 0.6f, 0.2f), Color.white, GameLayers.DEFAULT, true, lRot);
            lRoot.gameObject.AddComponent<Toilet>();
            Activity(ActivityType.Toilet, pPosition + lRot * new Vector3(0, 0, 0.7f), pYaw + 180f, pZone, 10f);
        }

        public void Chandelier(Vector3 pHangPoint, float pDrop)
        {
            Transform lRoot = Group("Chandelier", Props);
            lRoot.position = pHangPoint;
            lRoot.gameObject.layer = GameLayers.INTERACTABLE;
            Transform lCrystal = Group("Crystal", lRoot);
            lCrystal.position = pHangPoint + Vector3.down * 1.2f;
            Mesh lRing = EditorAssets.SaveMesh(LowPolyMesh.Cylinder(12, 1.3f), "Kit_ChandRing");
            Mesh(lCrystal, "Ring", lRing, lCrystal.position, new Vector3(2.2f, 0.25f, 2.2f), new Color(1f, 0.8f, 0.35f));
            for (int i = 0; i < 8; i++)
            {
                Vector3 lPos = lCrystal.position + Quaternion.Euler(0, i * 45f, 0) * new Vector3(1.05f, 0.2f, 0);
                GameObject lBulb = Mesh(lCrystal, "Bulb", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(0), "Kit_Ico0"), lPos, Vector3.one * 0.22f, new Color(2.2f, 1.8f, 1.2f), null, false, _glow);
                lBulb.AddComponent<PoweredLight>();
            }
            GameObject lChain = Block(lRoot, "Chain", pHangPoint + Vector3.down * 0.55f, new Vector3(0.12f, 1.1f, 0.12f), new Color(0.6f, 0.55f, 0.4f), GameLayers.INTERACTABLE, true);
            lChain.isStatic = false;
            Light lLight = new GameObject("ChandelierLight").AddComponent<Light>();
            lLight.transform.SetParent(lCrystal, false);
            lLight.type = LightType.Point;
            lLight.range = 14f;
            lLight.intensity = 3f;
            lLight.color = new Color(1f, 0.85f, 0.6f);
            Chandelier lChandelier = lRoot.gameObject.AddComponent<Chandelier>();
            lChandelier.Setup(lCrystal, pDrop);
        }

        public Breaker Breaker(Vector3 pPosition, float pYaw, BuildingVolume pBuilding)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            GameObject lBox = Block(Props, "Breaker", pPosition + Vector3.up * 1.4f, new Vector3(0.8f, 1f, 0.25f), new Color(0.5f, 0.55f, 0.6f), GameLayers.INTERACTABLE, true, lRot);
            lBox.isStatic = false;
            GameObject lLever = Block(lBox.transform, "Lever", pPosition + Vector3.up * 1.4f + lRot * new Vector3(0, 0, 0.2f), new Vector3(0.12f, 0.4f, 0.12f), new Color(0.9f, 0.2f, 0.2f), GameLayers.INTERACTABLE, false, lRot);
            lLever.isStatic = false;
            Block(Props, "Sign", pPosition + Vector3.up * 2.1f, new Vector3(0.6f, 0.25f, 0.05f), new Color(1f, 0.85f, 0.2f), GameLayers.DEFAULT, false, lRot);
            Breaker lBreaker = lBox.AddComponent<Breaker>();
            lBreaker.Setup(pBuilding, lLever.transform);
            return lBreaker;
        }

        public void Vent(Vector3 pPosition, float pYaw, BuildingVolume pBuilding, Transform[] pVents)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            GameObject lUnit = Block(Props, "AirHandler", pPosition + Vector3.up * 1.1f, new Vector3(2.6f, 2.2f, 1.6f), new Color(0.6f, 0.7f, 0.72f), GameLayers.INTERACTABLE, true, lRot, true);
            lUnit.isStatic = false;
            Block(lUnit.transform, "Pipe", pPosition + Vector3.up * 2.8f, new Vector3(0.8f, 1.6f, 0.8f), new Color(0.5f, 0.55f, 0.6f), GameLayers.DEFAULT, false, lRot);
            Block(Props, "HazardSign", pPosition + Vector3.up * 1.4f + lRot * new Vector3(0, 0, -0.85f), new Vector3(0.7f, 0.5f, 0.05f), new Color(0.55f, 0.95f, 0.35f), GameLayers.DEFAULT, false, lRot);
            VentPoison lVent = lUnit.AddComponent<VentPoison>();
            lVent.Setup(pBuilding, pVents);
        }

        public Transform VentGrill(Vector3 pPosition)
        {
            GameObject lGrill = Block(Props, "VentGrill", pPosition, new Vector3(0.8f, 0.1f, 0.8f), new Color(0.75f, 0.78f, 0.8f), GameLayers.DEFAULT, false);
            return lGrill.transform;
        }

        public void Sprinklers(Vector3 pCenter, Vector3 pSize, float pYaw, float pCeiling)
        {
            Transform lRoot = Group("Sprinklers", Props);
            lRoot.SetPositionAndRotation(pCenter, Quaternion.Euler(0, pYaw, 0));
            List<Transform> lHeads = new List<Transform>();
            for (float x = -pSize.x * 0.5f + 3f; x < pSize.x * 0.5f; x += 6f)
                for (float z = -pSize.z * 0.5f + 3f; z < pSize.z * 0.5f; z += 6f)
                {
                    GameObject lHead = Block(lRoot, "Head", lRoot.TransformPoint(new Vector3(x, pCeiling - 0.1f, z)), new Vector3(0.15f, 0.12f, 0.15f), new Color(0.8f, 0.2f, 0.2f), GameLayers.DEFAULT, false);
                    lHeads.Add(lHead.transform);
                }
            Sprinkler lSprinkler = lRoot.gameObject.AddComponent<Sprinkler>();
            lSprinkler.Setup(pSize, lHeads.ToArray());
        }

        public void Distraction(Vector3 pPosition, float pYaw, string pKind)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Color lColor = pKind switch { "radio" => new Color(0.85f, 0.4f, 0.3f), "tv" => new Color(0.2f, 0.2f, 0.25f), "phone" => new Color(0.9f, 0.8f, 0.3f), "generator" => new Color(0.9f, 0.6f, 0.15f), _ => new Color(0.6f, 0.6f, 0.65f) };
            Vector3 lSize = pKind switch { "tv" => new Vector3(1f, 0.7f, 0.3f), "generator" => new Vector3(1.2f, 0.9f, 0.8f), "phone" => new Vector3(0.3f, 0.2f, 0.3f), _ => new Vector3(0.5f, 0.35f, 0.25f) };
            GameObject lObject = Block(Props, "Distraction_" + pKind, pPosition + Vector3.up * lSize.y * 0.5f, lSize, lColor, GameLayers.INTERACTABLE, true, lRot);
            lObject.isStatic = false;
            string lPrompt = pKind switch { "radio" => "TURN THE RADIO ON", "tv" => "TURN THE TV ON", "phone" => "MAKE THE PHONE RING", "generator" => "START THE GENERATOR", "caralarm" => "SET OFF THE CAR ALARM", _ => "MAKE SOME NOISE" };
            lObject.AddComponent<Distraction>().Setup(lPrompt, pKind, pKind == "generator" || pKind == "caralarm" ? 28f : 16f);
        }

        public void HidingSpot(Vector3 pPosition, float pYaw, string pKind)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Vector3 lSize = pKind == "closet" ? new Vector3(1.2f, 2.2f, 0.8f) : pKind == "hay" ? new Vector3(1.8f, 1.3f, 1.8f) : new Vector3(2f, 1.3f, 1.2f);
            Color lColor = pKind == "closet" ? new Color(0.55f, 0.38f, 0.25f) : pKind == "hay" ? new Color(0.95f, 0.82f, 0.45f) : new Color(0.3f, 0.5f, 0.35f);
            GameObject lObject = Block(Props, "Hide_" + pKind, pPosition + Vector3.up * lSize.y * 0.5f, lSize, lColor, GameLayers.INTERACTABLE, false, lRot, true);
            lObject.isStatic = false;
            BoxCollider lBox = lObject.AddComponent<BoxCollider>();
            lBox.isTrigger = true;
            Transform lInside = new GameObject("Inside").transform;
            lInside.SetParent(lObject.transform.parent, false);
            lInside.position = pPosition + Vector3.up * 0.05f;
            Transform lExit = new GameObject("Exit").transform;
            lExit.SetParent(lObject.transform.parent, false);
            lExit.position = pPosition + lRot * new Vector3(0, 0.05f, -lSize.z * 0.5f - 0.9f);
            HidingSpot lSpot = lObject.AddComponent<HidingSpot>();
            lSpot.Setup(lInside, lExit, pKind == "closet" ? "HIDE IN THE CLOSET" : pKind == "hay" ? "HIDE IN THE HAY" : "HIDE IN THE DUMPSTER");
            if (pKind == "hay") lObject.AddComponent<Flammable>().Setup(15f, 3f, false, 1.2f);
        }

        public void JumpPad(Vector3 pPosition, float pYaw, Vector3 pLaunchLocal)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Transform lRoot = Group("JumpPad", Props);
            lRoot.SetPositionAndRotation(pPosition, lRot);
            Block(lRoot, "Base", pPosition + Vector3.up * 0.1f, new Vector3(2f, 0.2f, 2f), new Color(0.3f, 0.3f, 0.35f), GameLayers.DEFAULT, true, lRot);
            GameObject lPad = Mesh(lRoot, "Pad", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(12), "Kit_Cyl12"), pPosition + Vector3.up * 0.25f, new Vector3(1.6f, 0.15f, 1.6f), new Color(0.4f, 2f, 1.2f), lRot, false, _glow);
            GameObject lTrigger = new GameObject("Trigger") { layer = GameLayers.TRIGGER };
            lTrigger.transform.SetParent(lRoot, false);
            lTrigger.transform.position = pPosition + Vector3.up * 0.6f;
            BoxCollider lBox = lTrigger.AddComponent<BoxCollider>();
            lBox.isTrigger = true;
            lBox.size = new Vector3(1.6f, 1f, 1.6f);
            lTrigger.AddComponent<JumpPad>().Setup(pLaunchLocal, lPad.transform);
            lTrigger.transform.rotation = lRot;
        }

        public void Zipline(Vector3 pStart, Vector3 pEnd)
        {
            Transform lRoot = Group("Zipline", Props);
            lRoot.position = pStart;
            Transform lA = new GameObject("Start").transform;
            lA.SetParent(lRoot, false);
            lA.position = pStart + Vector3.up * 2.6f;
            Transform lB = new GameObject("End").transform;
            lB.SetParent(lRoot, false);
            lB.position = pEnd + Vector3.up * 2.6f;
            Block(lRoot, "PoleA", pStart + Vector3.up * 1.4f, new Vector3(0.2f, 2.8f, 0.2f), new Color(0.4f, 0.4f, 0.45f), GameLayers.DEFAULT, true);
            Block(lRoot, "PoleB", pEnd + Vector3.up * 1.4f, new Vector3(0.2f, 2.8f, 0.2f), new Color(0.4f, 0.4f, 0.45f), GameLayers.DEFAULT, true);
            Vector3 lDir = lB.position - lA.position;
            Block(lRoot, "Cable", (lA.position + lB.position) * 0.5f, new Vector3(0.05f, 0.05f, lDir.magnitude), new Color(0.2f, 0.2f, 0.22f), GameLayers.DEFAULT, false, Quaternion.LookRotation(lDir));
            GameObject lGrab = new GameObject("Grab") { layer = GameLayers.INTERACTABLE };
            lGrab.transform.SetParent(lRoot, false);
            lGrab.transform.position = pStart + Vector3.up * 1.6f;
            BoxCollider lBox = lGrab.AddComponent<BoxCollider>();
            lBox.isTrigger = true;
            lBox.size = new Vector3(1.6f, 2f, 1.6f);
            Mesh(lRoot, "Handle", EditorAssets.SaveMesh(LowPolyMesh.Box(), "Kit_Box"), pStart + Vector3.up * 2.2f, new Vector3(0.5f, 0.12f, 0.12f), new Color(1f, 0.85f, 0.2f));
            lGrab.AddComponent<Zipline>().Setup(lA, lB, 16f);
        }

        public void Alarm(Vector3 pPosition, float pYaw)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            GameObject lPost = Block(Props, "AlarmFlare", pPosition + Vector3.up * 0.9f, new Vector3(0.7f, 1.8f, 0.7f), new Color(0.85f, 0.2f, 0.2f), GameLayers.INTERACTABLE, true, lRot);
            lPost.isStatic = false;
            Mesh(Props, "Lamp", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(0), "Kit_Ico0"), pPosition + Vector3.up * 2f, Vector3.one * 0.4f, new Color(3f, 0.5f, 0.4f), null, false, _glow);
            lPost.AddComponent<AlarmTrigger>();
        }

        public void Lamp(Vector3 pPosition, Color pColor, float pRange = 12f, float pIntensity = 2.2f, bool pPost = true)
        {
            if (pPost) Block(Props, "LampPost", pPosition + Vector3.up * 2f, new Vector3(0.15f, 4f, 0.15f), new Color(0.25f, 0.25f, 0.3f), GameLayers.DEFAULT, true);
            GameObject lBulb = Mesh(Props, "Bulb", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(0), "Kit_Ico0"), pPosition + Vector3.up * (pPost ? 4.1f : 0f), Vector3.one * 0.35f, pColor * 2.2f, null, false, _glow);
            lBulb.AddComponent<PoweredLight>();
            Light lLight = new GameObject("Light").AddComponent<Light>();
            lLight.transform.SetParent(Lights, false);
            lLight.transform.position = lBulb.transform.position - Vector3.up * 0.3f;
            lLight.type = LightType.Point;
            lLight.range = pRange;
            lLight.intensity = pIntensity;
            lLight.color = pColor;
            lLight.shadows = LightShadows.None;
        }

        public void Fence(Vector3 pFrom, Vector3 pTo, float pHeight, Color pColor, bool pSolid = false)
        {
            Vector3 lDir = pTo - pFrom;
            if (pSolid) { Block(Env, "Wall", (pFrom + pTo) * 0.5f + Vector3.up * pHeight * 0.5f, new Vector3(0.3f, pHeight, lDir.magnitude), pColor, GameLayers.DEFAULT, true, Quaternion.LookRotation(lDir), pHeight < 1.6f); return; }
            int lPosts = Mathf.Max(2, Mathf.RoundToInt(lDir.magnitude / 2.5f) + 1);
            for (int i = 0; i < lPosts; i++) Block(Env, "Post", pFrom + lDir * ((float)i / (lPosts - 1)) + Vector3.up * pHeight * 0.5f, new Vector3(0.18f, pHeight, 0.18f), pColor * 0.8f, GameLayers.DEFAULT, true);
            Block(Env, "Rail", (pFrom + pTo) * 0.5f + Vector3.up * pHeight * 0.8f, new Vector3(0.08f, 0.12f, lDir.magnitude), pColor, GameLayers.DEFAULT, true, Quaternion.LookRotation(lDir));
            Block(Env, "Rail", (pFrom + pTo) * 0.5f + Vector3.up * pHeight * 0.4f, new Vector3(0.08f, 0.12f, lDir.magnitude), pColor, GameLayers.DEFAULT, true, Quaternion.LookRotation(lDir));
        }

        public void Bridge(Vector3 pFrom, Vector3 pTo, float pWidth, Color pColor, float pRail = 1f)
        {
            Vector3 lDir = pTo - pFrom;
            Quaternion lRot = Quaternion.LookRotation(new Vector3(lDir.x, 0, lDir.z));
            Block(Env, "Deck", (pFrom + pTo) * 0.5f, new Vector3(pWidth, 0.4f, lDir.magnitude), pColor, GameLayers.DEFAULT, true, Quaternion.LookRotation(lDir));
            for (int s = -1; s <= 1; s += 2)
                Block(Env, "Rail", (pFrom + pTo) * 0.5f + lRot * new Vector3(s * pWidth * 0.5f, pRail * 0.5f + 0.2f, 0), new Vector3(0.2f, pRail, lDir.magnitude), pColor * 0.8f, GameLayers.DEFAULT, true, Quaternion.LookRotation(lDir));
        }

        // ---------------------------------------------------------------- markers

        public LevelZone Zone(int pIndex, string pName, Vector3 pCenter, Vector3 pSize, Vector3 pEntry, Color pFaction, HatStyle pHat, Vector3 pSafeRoom)
        {
            Transform lZone = Group("Zone_" + pIndex + "_" + pName, Markers);
            lZone.position = pCenter;
            Transform lEntry = new GameObject("ReinforcementEntry").transform;
            lEntry.SetParent(lZone, false);
            lEntry.position = pEntry;
            Transform lSafe = new GameObject("SafeRoom").transform;
            lSafe.SetParent(lZone, false);
            lSafe.position = pSafeRoom;
            LevelZone lLevelZone = lZone.gameObject.AddComponent<LevelZone>();
            lLevelZone.Setup(pIndex, pName, pSize, lEntry, pFaction, pHat, lSafe);
            return lLevelZone;
        }

        public BuildingVolume BuildingVolume(string pName, Vector3 pCenter, Vector3 pSize, float pYaw)
        {
            GameObject lVolume = new GameObject("BuildingVolume_" + pName);
            lVolume.transform.SetParent(Markers, false);
            lVolume.transform.SetPositionAndRotation(pCenter, Quaternion.Euler(0, pYaw, 0));
            BuildingVolume lBuilding = lVolume.AddComponent<BuildingVolume>();
            lBuilding.Setup(pName, pSize);
            return lBuilding;
        }

        public void Npc(NpcRole pRole, Vector3 pPosition, float pYaw, int pZone, ItemKind pWeapon, PatrolRoute pRoute = null)
        {
            GameObject lMarker = new GameObject("Npc_" + pRole);
            lMarker.transform.SetParent(Markers, false);
            lMarker.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            lMarker.AddComponent<NpcSpawnPoint>().Setup(pRole, pZone, pRoute, pWeapon);
        }

        public PatrolRoute Route(float pPause, params Vector3[] pPoints)
        {
            Transform lRoute = Group("Route_" + _routeCount++, Markers);
            lRoute.position = pPoints[0];
            foreach (Vector3 lPoint in pPoints)
            {
                Transform lWaypoint = new GameObject("WP").transform;
                lWaypoint.SetParent(lRoute, false);
                lWaypoint.position = lPoint;
            }
            PatrolRoute lPatrol = lRoute.gameObject.AddComponent<PatrolRoute>();
            lPatrol.Setup(pPause);
            return lPatrol;
        }

        /// <summary>A patrol walking a loop, spawned at the first waypoint.</summary>
        public void Patrol(int pZone, ItemKind pWeapon, float pPause, params Vector3[] pPoints)
        {
            PatrolRoute lRoute = Route(pPause, pPoints);
            Npc(NpcRole.Patrol, pPoints[0], 0f, pZone, pWeapon, lRoute);
            ExplosiveTrap(pPoints);
        }

        /// <summary>
        /// Red explosive barrels a couple of metres off a patrol route, halfway along a leg the
        /// patrol walks: shoot them as it passes. Two side by side so one blast sets off the other.
        /// </summary>
        public void ExplosiveTrap(Vector3[] pRoute)
        {
            if (pRoute.Length < 2) return;
            int lLeg = Rng.Next(pRoute.Length - 1);
            Vector3 a = pRoute[lLeg], b = pRoute[lLeg + 1];
            Vector3 lDir = (b - a).normalized;
            Vector3 lSide = Vector3.Cross(Vector3.up, lDir) * (Rng.NextDouble() < 0.5 ? -2.4f : 2.4f);
            Vector3 lSpot = Vector3.Lerp(a, b, 0.35f + (float)Rng.NextDouble() * 0.3f) + lSide;
            Physics.SyncTransforms();
            if (Physics.Raycast(lSpot + Vector3.up * 2f, Vector3.down, out RaycastHit lFloor, 6f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) lSpot = lFloor.point;
            else lSpot.y = Mathf.Max(lSpot.y, GroundAt(lSpot.x, lSpot.z));
            lSpot.y -= 0.1f;
            Barrel(lSpot, true);
            Barrel(lSpot + lDir * 0.9f + lSide.normalized * 0.3f, true);
        }

        public void Activity(ActivityType pType, Vector3 pPosition, float pYaw, int pZone, float pDuration = 12f)
        {
            GameObject lNode = new GameObject("Activity_" + pType);
            lNode.transform.SetParent(Markers, false);
            lNode.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            lNode.AddComponent<ActivityNode>().Setup(pType, pZone, pDuration);
        }

        /// <summary>Several activity types at the same spot (a lounge works for Idle, Phone and Smoke).</summary>
        public void Activities(Vector3 pPosition, float pYaw, int pZone, params ActivityType[] pTypes)
        {
            for (int i = 0; i < pTypes.Length; i++) Activity(pTypes[i], pPosition + Quaternion.Euler(0, pYaw, 0) * new Vector3((i - pTypes.Length * 0.5f) * 0.8f, 0, 0), pYaw, pZone);
        }

        public void Cover(Vector3 pPosition, float pYaw, bool pLow)
        {
            GameObject lCover = new GameObject("Cover");
            lCover.transform.SetParent(Markers, false);
            lCover.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            lCover.AddComponent<CoverPoint>().Setup(pLow);
        }

        public void Point(MatchPoint.Kind pKind, Vector3 pPosition, float pYaw, string pLabel, ItemKind pItem = ItemKind.Smg)
        {
            GameObject lPoint = new GameObject(pKind + "_" + pLabel);
            lPoint.transform.SetParent(Markers, false);
            lPoint.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            lPoint.AddComponent<MatchPoint>().Setup(pKind, pLabel, pItem);
            if (pKind == MatchPoint.Kind.Extraction) lPoint.AddComponent<ExtractionPoint>().Setup(4.5f);
        }

        public void Weapon(ItemKind pKind, Vector3 pPosition) => Point(MatchPoint.Kind.WeaponSpawn, pPosition, 0f, pKind.ToString(), pKind);
        public void Gadget(ItemKind pKind, Vector3 pPosition) => Point(MatchPoint.Kind.GadgetSpawn, pPosition, 0f, pKind.ToString(), pKind);
        public void UniqueWeapon(ItemKind pKind, Vector3 pPosition, string pHint) => Point(MatchPoint.Kind.UniqueWeaponSpawn, pPosition, 0f, pHint, pKind);

        // ---------------------------------------------------------------- finish

        /// <summary>Cover points around every registered cover source, where there is floor and room.</summary>
        private void GenerateCover()
        {
            int lCount = 0;
            foreach ((Vector3 lCenter, Vector3 lSize, Quaternion lRot) in _coverSources)
            {
                bool lLow = lSize.y < 1.6f;
                for (int s = 0; s < 4; s++)
                {
                    Vector3 lNormal = lRot * SideNormal(s);
                    float lHalf = s % 2 == 0 ? lSize.z * 0.5f : lSize.x * 0.5f;
                    Vector3 lPos = new Vector3(lCenter.x, lCenter.y, lCenter.z) + lNormal * (lHalf + 0.6f);
                    if (!Physics.Raycast(lPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit lFloor, 4f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    lPos = lFloor.point;
                    if (Physics.CheckCapsule(lPos + Vector3.up * 0.4f, lPos + Vector3.up * 1.6f, 0.3f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    Cover(lPos, Quaternion.LookRotation(-lNormal).eulerAngles.y, lLow);
                    lCount++;
                }
            }
            Debug.Log($"[Build] {Map.sceneName}: {lCount} cover points");
        }

        private void Light()
        {
            Material lSky = ArtBuilder.Sky(Map);
            RenderSettings.skybox = lSky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(Map.skyTop, Map.ambient, 0.4f);
            RenderSettings.ambientEquatorColor = Color.Lerp(Map.skyHorizon, Map.ambient, 0.5f);
            RenderSettings.ambientGroundColor = Map.ambient * 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Map.fog;
            RenderSettings.fogDensity = Map.fogDensity;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            GameObject lSun = new GameObject("Sun");
            lSun.transform.SetParent(Lights, false);
            lSun.transform.rotation = Quaternion.Euler(Map.sunEuler);
            Light lLight = lSun.AddComponent<Light>();
            lLight.type = LightType.Directional;
            lLight.color = Map.sun;
            lLight.intensity = Map.sunIntensity;
            lLight.shadows = LightShadows.Soft;
            lLight.shadowStrength = 0.75f;
            RenderSettings.sun = lLight;

            GameObject lVolume = new GameObject("MapVolume");
            lVolume.transform.SetParent(Lights, false);
            Volume lV = lVolume.AddComponent<Volume>();
            lV.isGlobal = true;
            lV.priority = 1;
        }

        private void BakeNavMesh()
        {
            NavMeshSurface lSurface = Root.gameObject.AddComponent<NavMeshSurface>();
            lSurface.collectObjects = CollectObjects.All;
            lSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            lSurface.layerMask = (1 << GameLayers.DEFAULT) | (1 << GameLayers.INTERACTABLE) | (1 << GameLayers.CLIMBABLE);
            NavMeshBuildSettings lSettings = NavMesh.GetSettingsByIndex(0);
            lSettings.agentRadius = 0.35f;
            lSettings.agentHeight = 1.8f;
            lSettings.agentSlope = 42f;
            lSettings.agentClimb = 0.45f;
            lSurface.agentTypeID = lSettings.agentTypeID;
            lSurface.overrideVoxelSize = true;
            lSurface.voxelSize = 0.15f;
            lSurface.BuildNavMesh();
            string lPath = EditorAssets.SCENES + "/" + Map.sceneName + "_NavMesh.asset";
            AssetDatabase.DeleteAsset(lPath);
            AssetDatabase.CreateAsset(lSurface.navMeshData, lPath);
        }

        public void Finish()
        {
            BuildGround();
            Physics.SyncTransforms();
            GenerateCover();
            Light();
            BakeNavMesh();
            Scene lScene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(lScene, _scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] map {Map.displayName} saved to {_scenePath}");
        }
    }
}
