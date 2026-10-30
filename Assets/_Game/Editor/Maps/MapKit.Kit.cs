using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Assembly of the Blender modular kit (<c>Art/Resources/Kit/&lt;Style&gt;/&lt;Piece&gt;.fbx</c>):
    /// single pieces, whole buildings (facades of 3 m modules, corners, floor bands, cornices,
    /// balconies, roofs, slabs, stairs, partitions), furnished rooms and the systemic props
    /// (drinks, toilets, chandeliers, breakers, vents, distractions, hiding spots, barrels).
    /// <para>
    /// Kit conventions: pieces face -Z once imported (turned 180° here), building modules are
    /// 3 m wide x 3.4 m tall with their pivot at the bottom centre of the wall line, props pivot
    /// at the bottom centre of their footprint.
    /// </para>
    /// </summary>
    public partial class MapKit
    {
        public const float MODULE = 3f;
        public const float STOREY = 3.4f;
        public enum Collide { None, Mesh, Box }
        public enum Roof { Flat, Gable, Hip }
        public enum Room { None, Lounge, Bar, Office, Bedroom, Kitchen, Bathroom, Storage, Dining, Shop, Utility, Ballroom, Gym, Security }

        private const string KIT = EditorAssets.ROOT + "/Art/Resources/Kit/";
        private static readonly Dictionary<string, Mesh> _kitMeshes = new Dictionary<string, Mesh>();
        private Material _kitGlow;

        public static Mesh KitMesh(string pStyle, string pName)
        {
            string lKey = pStyle + "/" + pName;
            if (_kitMeshes.TryGetValue(lKey, out Mesh lMesh) && lMesh != null) return lMesh;
            GameObject lAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KIT + lKey + ".fbx");
            MeshFilter lFilter = lAsset != null ? lAsset.GetComponentInChildren<MeshFilter>() : null;
            lMesh = lFilter != null ? lFilter.sharedMesh : null;
            _kitMeshes[lKey] = lMesh;
            return lMesh;
        }

        public static bool HasKit(string pStyle, string pName) => KitMesh(pStyle, pName) != null;

        [System.Serializable] private class KitEntry { public string style; public string name; public string kind; public float[] size; public float[] tint; }
        [System.Serializable] private class KitManifest { public KitEntry[] pieces; }
        private static Dictionary<string, Color> _defaultTints;

        /// <summary>The tint the kit suggests for a piece (foliage green, sofa fabric...), white if none.</summary>
        public static Color DefaultTint(string pStyle, string pName)
        {
            if (_defaultTints == null)
            {
                _defaultTints = new Dictionary<string, Color>();
                TextAsset lFile = AssetDatabase.LoadAssetAtPath<TextAsset>(KIT + "manifest.json");
                KitManifest lManifest = lFile != null ? JsonUtility.FromJson<KitManifest>(lFile.text) : null;
                if (lManifest?.pieces != null)
                    foreach (KitEntry lEntry in lManifest.pieces)
                        if (lEntry.tint != null && lEntry.tint.Length >= 3) _defaultTints[lEntry.style + "/" + lEntry.name] = new Color(lEntry.tint[0], lEntry.tint[1], lEntry.tint[2]);
            }
            return _defaultTints.TryGetValue(pStyle + "/" + pName, out Color lTint) ? lTint : Color.white;
        }

        /// <summary>
        /// One kit piece. <paramref name="pYaw"/> is the direction its front faces (degrees, 0 = +Z).
        /// Tint recolours only the tintable surfaces (vertex alpha mask).
        /// </summary>
        public GameObject Piece(string pStyle, string pName, Vector3 pPosition, float pYaw, Color? pTint = null, Transform pParent = null, Collide pCollide = Collide.Mesh, float pScale = 1f, int pLayer = GameLayers.DEFAULT)
        {
            Mesh lMesh = KitMesh(pStyle, pName);
            if (lMesh == null) { Debug.LogWarning($"[Build] missing kit piece {pStyle}/{pName}"); return null; }
            GameObject lObject = new GameObject(pName) { layer = pLayer };
            lObject.transform.SetParent(pParent != null ? pParent : Props, false);
            lObject.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw + 180f, 0));
            lObject.transform.localScale = Vector3.one * pScale;
            lObject.AddComponent<MeshFilter>().sharedMesh = lMesh;
            MeshRenderer lRenderer = lObject.AddComponent<MeshRenderer>();
            lRenderer.sharedMaterial = _toon;
            SetColor(lRenderer, pTint ?? DefaultTint(pStyle, pName));
            if (pCollide == Collide.Mesh) lObject.AddComponent<MeshCollider>().sharedMesh = lMesh;
            else if (pCollide == Collide.Box) { BoxCollider lBox = lObject.AddComponent<BoxCollider>(); lBox.center = lMesh.bounds.center; lBox.size = lMesh.bounds.size; }
            lObject.isStatic = true;

            Mesh lGlow = KitMesh(pStyle, pName + "_Glow");
            if (lGlow != null)
            {
                _kitGlow ??= _glow;
                GameObject lBulb = new GameObject("Glow");
                lBulb.transform.SetParent(lObject.transform, false);
                lBulb.AddComponent<MeshFilter>().sharedMesh = lGlow;
                MeshRenderer lGlowRenderer = lBulb.AddComponent<MeshRenderer>();
                lGlowRenderer.sharedMaterial = _glow;
                lGlowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                SetColor(lGlowRenderer, new Color(2.2f, 1.8f, 1.3f));
                lBulb.AddComponent<PoweredLight>();
            }
            return lObject;
        }

        /// <summary>A prop from the shared kit, registered as cover when it is waist-high or more.</summary>
        public GameObject Prop(string pName, Vector3 pPosition, float pYaw, Color? pTint = null, Collide pCollide = Collide.Box, bool pCover = false, Transform pParent = null)
        {
            GameObject lProp = Piece("Props", pName, pPosition, pYaw, pTint, pParent, pCollide);
            if (lProp != null && pCover)
            {
                Bounds lBounds = lProp.GetComponent<MeshFilter>().sharedMesh.bounds;
                if (lBounds.size.y > 0.7f) _coverSources.Add((pPosition + Vector3.up * lBounds.size.y * 0.5f, lBounds.size, Quaternion.Euler(0, pYaw, 0)));
            }
            return lProp;
        }

        public GameObject NaturePiece(string pName, Vector3 pPosition, float pYaw, float pScale, Color? pTint = null, Collide pCollide = Collide.Box)
        {
            GameObject lPiece = Piece("Nature", pName, pPosition, pYaw, pTint, TreesGroup, Collide.None, pScale);
            if (lPiece == null) return null;
            Bounds lBounds = lPiece.GetComponent<MeshFilter>().sharedMesh.bounds;
            bool lTree = pName.Contains("Pine") || pName.Contains("Broadleaf") || pName.Contains("Palm") || pName.Contains("Birch") || pName.Contains("Cypress") || pName.Contains("Dead");
            if (lTree)
            {
                CapsuleCollider lTrunk = lPiece.AddComponent<CapsuleCollider>();
                lTrunk.radius = 0.35f;
                lTrunk.height = Mathf.Min(6f, lBounds.size.y);
                lTrunk.center = new Vector3(0, lTrunk.height * 0.5f, 0);
            }
            else if (pCollide == Collide.Box)
            {
                BoxCollider lBox = lPiece.AddComponent<BoxCollider>();
                lBox.center = lBounds.center;
                lBox.size = lBounds.size;
                if (lBounds.size.y > 0.8f) _coverSources.Add((pPosition + Vector3.up * lBounds.size.y * pScale * 0.4f, lBounds.size * pScale, Quaternion.Euler(0, pYaw, 0)));
            }
            else if (pCollide == Collide.Mesh) lPiece.AddComponent<MeshCollider>().sharedMesh = lPiece.GetComponent<MeshFilter>().sharedMesh;
            return lPiece;
        }

        // ---------------------------------------------------------------- buildings

        public class KitSpec
        {
            public string name = "Building";
            public string style = "Stucco";
            public Vector3 origin;
            public float yaw;
            /// <summary>Modules along local X (front/back) and Z (sides). 3 m each.</summary>
            public int width = 4, depth = 3;
            public int floors = 2;
            public Color wall = new Color(0.95f, 0.92f, 0.86f);
            public Color roofTint = new Color(0.7f, 0.35f, 0.28f);
            public Color floorColor = new Color(0.62f, 0.5f, 0.4f);
            public Color interiorWall = new Color(0.92f, 0.9f, 0.86f);
            public Roof roof = Roof.Flat;
            /// <summary>Doors on the ground floor: (side, module). Sides: 0 front (-Z), 1 right (+X), 2 back (+Z), 3 left (-X).</summary>
            public List<Vector2Int> doors = new List<Vector2Int>();
            public List<Vector2Int> arches = new List<Vector2Int>();
            public float windowChance = 0.7f;
            public float balconyChance = 0.15f;
            public bool tallWindows;
            public bool brick;
            public bool basement;
            public bool stairs = true;
            public bool roofAccess = true;
            /// <summary>Drainpipe / ivy up one side (side, module), -1 = none.</summary>
            public Vector2Int climb = new Vector2Int(-1, 0);
            /// <summary>Rooms per floor (index = floor, basement first if any). Two rooms per floor when the building is 3+ modules deep.</summary>
            public List<Room[]> rooms = new List<Room[]>();
            public int zone;
            public int seed;
        }

        public class KitBuilding
        {
            public KitSpec spec;
            public Transform root;
            public float W => spec.width * MODULE;
            public float D => spec.depth * MODULE;
            public Vector3 Local(float pX, float pY, float pZ) => root.TransformPoint(new Vector3(pX, pY, pZ));
            public float Yaw => spec.yaw;
            public float FloorY(int pFloor) => root.position.y + pFloor * STOREY;
            public BuildingVolume volume;
        }

        private static Vector3 KitNormal(int pSide) => pSide switch { 0 => Vector3.back, 1 => Vector3.right, 2 => Vector3.forward, _ => Vector3.left };
        private static Vector3 KitTangent(int pSide) => pSide switch { 0 => Vector3.right, 1 => Vector3.forward, 2 => Vector3.left, _ => Vector3.back };
        private static float KitYaw(int pSide) => pSide switch { 0 => 180f, 1 => 90f, 2 => 0f, _ => 270f };

        /// <summary>
        /// A building of kit modules: walls with doors, windows, balconies and arches, corners,
        /// floor bands and a cornice; interior slabs with a stairwell and kit stairs; an optional
        /// basement; a flat roof with parapets (and roof access) or a gable / hip roof; furnished
        /// rooms; a climbable drainpipe; a BuildingVolume for power cuts and gas.
        /// </summary>
        public KitBuilding Building(KitSpec pSpec)
        {
            System.Random lRandom = new System.Random(pSpec.seed != 0 ? pSpec.seed : pSpec.name.GetHashCode());
            Transform lRoot = Group(pSpec.name, Env);
            lRoot.SetPositionAndRotation(pSpec.origin, Quaternion.Euler(0, pSpec.yaw, 0));
            KitBuilding lBuilding = new KitBuilding { spec = pSpec, root = lRoot };
            string S = pSpec.style;
            float W = lBuilding.W, D = lBuilding.D;
            int lFirst = pSpec.basement ? -1 : 0;

            // Facades.
            for (int f = lFirst; f < pSpec.floors; f++)
            {
                float y = f * STOREY;
                for (int s = 0; s < 4; s++)
                {
                    int lCount = s % 2 == 0 ? pSpec.width : pSpec.depth;
                    float lHalfOther = (s % 2 == 0 ? D : W) * 0.5f;
                    for (int i = 0; i < lCount; i++)
                    {
                        float lAlong = -lCount * MODULE * 0.5f + MODULE * 0.5f + i * MODULE;
                        Vector3 lLocal = KitNormal(s) * lHalfOther + KitTangent(s) * lAlong + Vector3.up * y;
                        string lPiece = FacadePiece(pSpec, f, s, i, lRandom);
                        Piece(S, lPiece, lRoot.TransformPoint(lLocal), pSpec.yaw + KitYaw(s), f < 0 ? pSpec.wall * 0.8f : pSpec.wall, lRoot);
                        if (f > 0) Piece(S, "FloorBand", lRoot.TransformPoint(lLocal), pSpec.yaw + KitYaw(s), pSpec.wall, lRoot, Collide.None);
                        if (f == pSpec.floors - 1) Piece(S, pSpec.roof == Roof.Flat ? "Parapet" : "Cornice", lRoot.TransformPoint(lLocal + Vector3.up * STOREY), pSpec.yaw + KitYaw(s), pSpec.wall, lRoot, pSpec.roof == Roof.Flat ? Collide.Mesh : Collide.None);
                    }
                }
                for (int c = 0; c < 4; c++)
                {
                    Vector3 lCorner = new Vector3((c == 0 || c == 3 ? -1 : 1) * W * 0.5f, y, (c < 2 ? -1 : 1) * D * 0.5f);
                    Piece(S, "Corner", lRoot.TransformPoint(lCorner), pSpec.yaw + c * 90f, pSpec.wall, lRoot, Collide.Box);
                }
            }

            // Slabs (with a stairwell near the back-right corner) and stairs.
            Vector2 lWell = new Vector2(W * 0.5f - 1.3f, D * 0.5f - 3.6f);
            for (int f = lFirst; f <= pSpec.floors; f++)
            {
                bool lRoofLevel = f == pSpec.floors;
                bool lCut = pSpec.stairs && f > lFirst && (!lRoofLevel || (pSpec.roofAccess && pSpec.roof == Roof.Flat));
                float y = f * STOREY - 0.12f;
                Color lColor = lRoofLevel ? pSpec.roofTint : pSpec.floorColor;
                if (f == 0 && !pSpec.basement) lCut = false;
                KitSlab(lRoot, W - 0.3f, D - 0.3f, y, lColor, lCut, lWell);
            }
            if (pSpec.stairs)
                for (int f = lFirst; f < pSpec.floors; f++)
                {
                    if (f == pSpec.floors - 1 && !(pSpec.roofAccess && pSpec.roof == Roof.Flat)) break;
                    Piece(S, "Stairs", lRoot.TransformPoint(new Vector3(lWell.x, f * STOREY, lWell.y)), pSpec.yaw + 180f, pSpec.floorColor * 1.1f, lRoot);
                }

            // Roof.
            float lRoofY = pSpec.floors * STOREY;
            if (pSpec.roof == Roof.Gable || pSpec.roof == Roof.Hip)
            {
                GameObject lRoof = Piece(S, pSpec.roof == Roof.Gable ? "RoofGable" : "RoofHip", lRoot.TransformPoint(new Vector3(0, lRoofY, 0)), pSpec.yaw, pSpec.roofTint, lRoot);
                if (lRoof != null) lRoof.transform.localScale = new Vector3(W, Mathf.Min(W, D) * 0.45f, D);
                if (lRandom.NextDouble() < 0.6) Piece(S, "Chimney", lRoot.TransformPoint(new Vector3(-W * 0.25f, lRoofY, D * 0.1f)), pSpec.yaw, pSpec.wall * 0.8f, lRoot, Collide.Box);
            }
            else
            {
                Prop("AirHandler", lRoot.TransformPoint(new Vector3(-W * 0.2f, lRoofY, -D * 0.15f)), pSpec.yaw, Color.white, Collide.Box, true, lRoot);
                if (lRandom.NextDouble() < 0.5) Prop("Crate", lRoot.TransformPoint(new Vector3(W * 0.2f, lRoofY, -D * 0.2f)), pSpec.yaw, null, Collide.Box, true, lRoot);
            }

            // Climbable drainpipe from the ground to the roof.
            if (pSpec.climb.x >= 0)
            {
                int s = pSpec.climb.x;
                int lCount = s % 2 == 0 ? pSpec.width : pSpec.depth;
                float lHalfOther = (s % 2 == 0 ? D : W) * 0.5f;
                float lAlong = -lCount * MODULE * 0.5f + Mathf.Clamp(pSpec.climb.y, 0, lCount - 1) * MODULE;
                for (int f = 0; f < pSpec.floors; f++)
                {
                    Vector3 lLocal = KitNormal(s) * (lHalfOther + 0.25f) + KitTangent(s) * lAlong + Vector3.up * (f * STOREY);
                    Piece(S, "Drainpipe", lRoot.TransformPoint(lLocal), pSpec.yaw + KitYaw(s), null, lRoot, Collide.None, 1f, GameLayers.CLIMBABLE);
                }
                Vector3 lClimbCenter = KitNormal(s) * (lHalfOther + 0.3f) + KitTangent(s) * lAlong + Vector3.up * (pSpec.floors * STOREY * 0.5f);
                Climbable(lRoot, lRoot.TransformPoint(lClimbCenter), new Vector3(0.9f, pSpec.floors * STOREY + 0.6f, 0.25f), lRoot.rotation * Quaternion.LookRotation(-KitNormal(s)), new Color(0, 0, 0, 0), false).GetComponent<MeshRenderer>().enabled = false;
            }

            // Rooms and the building's systems.
            for (int f = lFirst; f < pSpec.floors; f++)
            {
                int lIndex = f - lFirst;
                Room[] lRooms = lIndex < pSpec.rooms.Count ? pSpec.rooms[lIndex] : null;
                if (lRooms == null || lRooms.Length == 0) continue;
                if (lRooms.Length > 1 && pSpec.depth >= 3)
                {
                    PartitionKit(lBuilding, f, new Vector3(-W * 0.5f, 0, 0), new Vector3(W * 0.5f - 3f, 0, 0));
                    Furnish(lBuilding, f, lRooms[0], new Rect(-W * 0.5f + 0.5f, -D * 0.5f + 0.5f, W - 1f, D * 0.5f - 1f), lRandom);
                    Furnish(lBuilding, f, lRooms[1], new Rect(-W * 0.5f + 0.5f, 0.5f, W - 4f, D * 0.5f - 1f), lRandom);
                }
                else Furnish(lBuilding, f, lRooms[0], new Rect(-W * 0.5f + 0.8f, -D * 0.5f + 0.8f, W - 4f, D - 1.6f), lRandom);
                Lamp(lBuilding.Local(-W * 0.2f, f * STOREY + STOREY - 0.45f, 0), new Color(1f, 0.86f, 0.66f), 9f, 1.2f, false);
            }
            lBuilding.volume = BuildingVolume(pSpec.name, lRoot.position, new Vector3(W, (pSpec.floors + 1) * STOREY, D), pSpec.yaw);
            return lBuilding;
        }

        private static string FacadePiece(KitSpec pSpec, int pFloor, int pSide, int pModule, System.Random pRandom)
        {
            Vector2Int lKey = new Vector2Int(pSide, pModule);
            if (pFloor == 0 && pSpec.doors.Contains(lKey)) return pSpec.brick && pSpec.style == "Suburb" ? "WallBrickDoor" : (pModule % 2 == 0 ? "WallDoor" : "WallDoorDouble");
            if (pFloor == 0 && pSpec.arches.Contains(lKey)) return "WallArch";
            if (pFloor < 0) return "Wall";
            double r = pRandom.NextDouble();
            if (pFloor > 0 && r < pSpec.balconyChance) return "WallWindowBalcony";
            if (r < pSpec.balconyChance + pSpec.windowChance)
            {
                if (pSpec.brick && pSpec.style == "Suburb") return "WallBrickWindow";
                return pSpec.tallWindows ? "WallWindowTall" : "WallWindow";
            }
            return pSpec.brick && pSpec.style == "Suburb" ? "WallBrick" : "Wall";
        }

        private void KitSlab(Transform pRoot, float W, float D, float y, Color pColor, bool pWell, Vector2 pWellCenter)
        {
            void Part(float x0, float x1, float z0, float z1)
            {
                if (x1 - x0 < 0.05f || z1 - z0 < 0.05f) return;
                Block(pRoot, "Slab", pRoot.TransformPoint(new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f)), new Vector3(x1 - x0, 0.24f, z1 - z0), pColor, GameLayers.DEFAULT, true, pRoot.rotation);
            }
            if (!pWell) { Part(-W * 0.5f, W * 0.5f, -D * 0.5f, D * 0.5f); return; }
            float hx0 = pWellCenter.x - 0.9f, hx1 = pWellCenter.x + 0.9f, hz0 = pWellCenter.y - 3f, hz1 = pWellCenter.y + 3f;
            Part(-W * 0.5f, hx0, -D * 0.5f, D * 0.5f);
            Part(hx1, W * 0.5f, -D * 0.5f, D * 0.5f);
            Part(hx0, hx1, -D * 0.5f, hz0);
            Part(hx0, hx1, hz1, D * 0.5f);
        }

        /// <summary>An interior wall line of kit walls (one doorway in the middle) on a floor.</summary>
        public void PartitionKit(KitBuilding pBuilding, int pFloor, Vector3 pFromLocal, Vector3 pToLocal)
        {
            Vector3 lDir = pToLocal - pFromLocal;
            int lModules = Mathf.Max(1, Mathf.RoundToInt(lDir.magnitude / MODULE));
            Vector3 lUnit = lDir.normalized;
            float lYaw = pBuilding.Yaw + Quaternion.LookRotation(Vector3.Cross(lUnit, Vector3.up)).eulerAngles.y;
            for (int i = 0; i < lModules; i++)
            {
                Vector3 lLocal = pFromLocal + lUnit * (MODULE * (i + 0.5f)) + Vector3.up * (pFloor * STOREY);
                string lPiece = i == lModules / 2 ? "WallDoor" : "Wall";
                Piece(pBuilding.spec.style, lPiece, pBuilding.root.TransformPoint(lLocal), lYaw, pBuilding.spec.interiorWall, pBuilding.root);
            }
        }

        /// <summary>Dresses a room (rectangle in building-local XZ) with furniture, and its systems.</summary>
        public void Furnish(KitBuilding pBuilding, int pFloor, Room pRoom, Rect pArea, System.Random pRandom)
        {
            float y = pFloor * STOREY;
            Transform R = pBuilding.root;
            float lYaw = pBuilding.Yaw;
            int Z = pBuilding.spec.zone;
            Vector3 P(float u, float v) => R.TransformPoint(new Vector3(Mathf.Lerp(pArea.xMin, pArea.xMax, u), y, Mathf.Lerp(pArea.yMin, pArea.yMax, v)));
            Color lWood = new Color(0.62f, 0.42f, 0.28f);
            switch (pRoom)
            {
                case Room.Lounge:
                    Prop("Rug", P(0.5f, 0.5f), lYaw, null, Collide.None);
                    Prop("Sofa", P(0.5f, 0.2f), lYaw, FX.GameArt.Vary(new Color(0.55f, 0.3f, 0.3f), 0.3f, pRandom), Collide.Box, true);
                    Prop("Armchair", P(0.2f, 0.5f), lYaw + 90, null, Collide.Box, true);
                    Prop("TableRound", P(0.5f, 0.5f), lYaw, lWood, Collide.Box, true);
                    Prop("PlantBig", P(0.95f, 0.05f), lYaw, null);
                    Prop("FloorLamp", P(0.1f, 0.1f), lYaw, null);
                    DistractionKit(P(0.5f, 0.95f), lYaw + 180, "tv");
                    Activities(P(0.5f, 0.35f), lYaw, Z, ActivityType.Idle, ActivityType.Phone);
                    break;
                case Room.Bar:
                    Prop("BarCounter", P(0.5f, 0.75f), lYaw + 180, lWood, Collide.Box, true);
                    Prop("BarShelf", P(0.5f, 0.98f), lYaw + 180, null);
                    for (int i = 0; i < 3; i++) Prop("Stool", P(0.3f + i * 0.2f, 0.6f), lYaw, null);
                    BarTable(P(0.25f, 0.25f), lYaw, Z, 2, true, 0.78f);
                    BarTable(P(0.75f, 0.25f), lYaw, Z, 2, false, 0.78f);
                    DrinksOn(P(0.5f, 0.75f) + Vector3.up * 1.15f, lYaw, Z, 3);
                    DistractionKit(P(0.05f, 0.95f), lYaw, "radio");
                    break;
                case Room.Office:
                    Prop("Desk", P(0.5f, 0.6f), lYaw + 180, lWood, Collide.Box, true);
                    Prop("OfficeChair", P(0.5f, 0.75f), lYaw + 180, null);
                    Prop("Bookshelf", P(0.15f, 0.97f), lYaw + 180, lWood);
                    Prop("Bookshelf", P(0.35f, 0.97f), lYaw + 180, lWood);
                    Prop("Cabinet", P(0.95f, 0.5f), lYaw - 90, null, Collide.Box, true);
                    DistractionKit(P(0.55f, 0.6f) + Vector3.up * 0.8f, lYaw + 180, "phone");
                    Activities(P(0.5f, 0.45f), lYaw, Z, ActivityType.Office, ActivityType.Phone);
                    break;
                case Room.Bedroom:
                    Prop("Rug", P(0.5f, 0.5f), lYaw, null, Collide.None);
                    Prop("Bed", P(0.5f, 0.85f), lYaw + 180, FX.GameArt.Vary(new Color(0.4f, 0.5f, 0.7f), 0.3f, pRandom), Collide.Box, true);
                    HidingKit(P(0.05f, 0.5f), lYaw + 90, "Wardrobe");
                    Prop("FloorLamp", P(0.95f, 0.95f), lYaw, null);
                    break;
                case Room.Kitchen:
                    Prop("Fridge", P(0.05f, 0.95f), lYaw + 180, null, Collide.Box, true);
                    Prop("Stove", P(0.3f, 0.95f), lYaw + 180, null, Collide.Box, true);
                    Prop("Sink", P(0.5f, 0.95f), lYaw + 180, null, Collide.Box);
                    Prop("Table", P(0.5f, 0.4f), lYaw, lWood, Collide.Box, true);
                    for (int i = 0; i < 2; i++) Prop("Chair", P(0.35f + i * 0.3f, 0.25f), lYaw, lWood);
                    Barrel(P(0.95f, 0.1f), false);
                    break;
                case Room.Bathroom:
                    ToiletKit(P(0.2f, 0.9f), lYaw + 180, Z);
                    ToiletKit(P(0.6f, 0.9f), lYaw + 180, Z);
                    Prop("Sink", P(0.95f, 0.5f), lYaw - 90, null);
                    break;
                case Room.Storage:
                    for (int i = 0; i < 4; i++) Crate(P(0.15f + (i % 2) * 0.2f, 0.2f + (i / 2) * 0.5f), 1f);
                    Prop("Pallet", P(0.7f, 0.3f), lYaw, null, Collide.Box);
                    Prop("CrateLarge", P(0.75f, 0.75f), lYaw, null, Collide.Box, true);
                    Barrel(P(0.9f, 0.1f), true);
                    Barrel(P(0.93f, 0.2f), true);
                    break;
                case Room.Dining:
                    Prop("Table", P(0.35f, 0.5f), lYaw, lWood, Collide.Box, true);
                    Prop("Table", P(0.65f, 0.5f), lYaw, lWood, Collide.Box, true);
                    for (int i = 0; i < 4; i++) { Prop("Chair", P(0.25f + i * 0.17f, 0.35f), lYaw, lWood); Prop("Chair", P(0.25f + i * 0.17f, 0.65f), lYaw + 180, lWood); }
                    DrinksOn(P(0.5f, 0.5f) + Vector3.up * 0.8f, lYaw, Z, 3);
                    Activity(ActivityType.Dining, P(0.5f, 0.3f), lYaw, Z);
                    Prop("Fireplace", P(0.5f, 0.98f), lYaw + 180, null);
                    break;
                case Room.Shop:
                    Prop("Cabinet", P(0.3f, 0.7f), lYaw + 180, null, Collide.Box, true);
                    Prop("Bookshelf", P(0.1f, 0.97f), lYaw + 180, null);
                    Prop("Bookshelf", P(0.9f, 0.97f), lYaw + 180, null);
                    Prop("PlantPot", P(0.9f, 0.1f), lYaw, null);
                    Activity(ActivityType.Inspect, P(0.5f, 0.5f), lYaw, Z);
                    break;
                case Room.Utility:
                    BreakerKit(P(0.5f, 0.98f), lYaw + 180, pBuilding);
                    DistractionKit(P(0.15f, 0.5f), lYaw + 90, "generator");
                    Prop("Cabinet", P(0.9f, 0.9f), lYaw + 180, null, Collide.Box, true);
                    break;
                case Room.Ballroom:
                    ChandelierKit(P(0.5f, 0.5f) + Vector3.up * (STOREY - 0.15f), STOREY - 0.4f);
                    Prop("Piano", P(0.9f, 0.85f), lYaw + 200, null, Collide.Box, true);
                    Prop("Rug", P(0.5f, 0.5f), lYaw, null, Collide.None);
                    Activity(ActivityType.Speech, P(0.5f, 0.85f), lYaw + 180, Z, 18f);
                    DrinksOn(P(0.1f, 0.5f) + Vector3.up * 0.8f, lYaw, Z, 2);
                    Prop("TableRound", P(0.1f, 0.5f), lYaw, null, Collide.Box, true);
                    break;
                case Room.Gym:
                    Prop("Rug", P(0.5f, 0.5f), lYaw, new Color(0.3f, 0.3f, 0.35f), Collide.None);
                    Prop("Bench", P(0.3f, 0.5f), lYaw, null, Collide.Box);
                    Activity(ActivityType.Workout, P(0.5f, 0.5f), lYaw, Z);
                    break;
                case Room.Security:
                    Prop("Desk", P(0.5f, 0.8f), lYaw + 180, null, Collide.Box, true);
                    for (int i = 0; i < 3; i++) Prop("TV", P(0.3f + i * 0.2f, 0.98f) + Vector3.up * 1.4f, lYaw + 180, null, Collide.None);
                    Prop("OfficeChair", P(0.5f, 0.65f), lYaw + 180, null);
                    Activity(ActivityType.Lookout, P(0.5f, 0.6f), lYaw + 180, Z);
                    break;
            }
        }

        // ---------------------------------------------------------------- systemic props from the kit

        /// <summary>Poisonable glasses standing on a surface (bar counter, table).</summary>
        public void DrinksOn(Vector3 pSurface, float pYaw, int pZone, int pCount)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            for (int i = 0; i < pCount; i++)
            {
                Vector3 lPos = pSurface + lRot * new Vector3(-0.5f + i * 1f / Mathf.Max(1, pCount - 1), 0, 0);
                GameObject lGlass = new GameObject("Drink") { layer = GameLayers.INTERACTABLE };
                lGlass.transform.SetParent(Props, false);
                lGlass.transform.position = lPos;
                Mesh(lGlass.transform, "Glass", EditorAssets.SaveMesh(FX.LowPolyMesh.Cylinder(8, 1.2f), "Kit_Glass"), lPos + Vector3.up * 0.1f, new Vector3(0.1f, 0.18f, 0.1f), new Color(0.85f, 0.95f, 1f), null, false, _transparent);
                GameObject lLiquid = Mesh(lGlass.transform, "Liquid", EditorAssets.SaveMesh(FX.LowPolyMesh.Cylinder(8), "Kit_Cyl8"), lPos + Vector3.up * 0.06f, new Vector3(0.08f, 0.1f, 0.08f), new Color(0.85f, 0.45f, 0.2f));
                lLiquid.name = "Liquid";
                BoxCollider lBox = lGlass.AddComponent<BoxCollider>();
                lBox.size = new Vector3(0.3f, 0.4f, 0.3f);
                lBox.center = new Vector3(0, 0.15f, 0);
                lBox.isTrigger = true;
                lGlass.AddComponent<Drink>();
            }
            // The drinker stands a metre in front of the surface, on the floor below it.
            Activity(ActivityType.Drink, pSurface + lRot * new Vector3(0, 0, -1f) - Vector3.up * 1f, pYaw, pZone, 14f);
        }

        public void ToiletKit(Vector3 pPosition, float pYaw, int pZone)
        {
            GameObject lToilet = Prop("Toilet", pPosition, pYaw, Color.white, Collide.Box);
            if (lToilet == null) { Toilet(pPosition, pYaw, pZone); return; }
            lToilet.isStatic = false;
            lToilet.AddComponent<Toilet>();
            Activity(ActivityType.Toilet, pPosition + Quaternion.Euler(0, pYaw, 0) * Vector3.forward * 0.7f, pYaw + 180f, pZone, 10f);
        }

        public void ChandelierKit(Vector3 pHook, float pDrop)
        {
            if (!HasKit("Props", "Chandelier")) { Chandelier(pHook, pDrop); return; }
            Transform lRoot = Group("Chandelier", Props);
            lRoot.position = pHook;
            lRoot.gameObject.layer = GameLayers.INTERACTABLE;
            Transform lCrystal = Group("Crystal", lRoot);
            lCrystal.position = pHook;
            GameObject lModel = Piece("Props", "Chandelier", pHook, 0f, null, lCrystal, Collide.None);
            if (lModel != null) lModel.isStatic = false;
            GameObject lChain = Block(lRoot, "ChainHitbox", pHook + Vector3.down * 0.4f, new Vector3(0.25f, 0.8f, 0.25f), Color.white, GameLayers.INTERACTABLE, true);
            lChain.GetComponent<MeshRenderer>().enabled = false;
            lChain.isStatic = false;
            Light lLight = new GameObject("ChandelierLight").AddComponent<Light>();
            lLight.transform.SetParent(lCrystal, false);
            lLight.transform.localPosition = Vector3.down * 1.2f;
            lLight.type = LightType.Point;
            lLight.range = 16f;
            lLight.intensity = 3.5f;
            lLight.color = new Color(1f, 0.85f, 0.6f);
            lRoot.gameObject.AddComponent<Chandelier>().Setup(lCrystal, pDrop);
        }

        public Breaker BreakerKit(Vector3 pPosition, float pYaw, KitBuilding pBuilding)
        {
            BuildingVolume lVolume = pBuilding.volume != null ? pBuilding.volume : BuildingVolume(pBuilding.spec.name, pBuilding.root.position, new Vector3(pBuilding.W, (pBuilding.spec.floors + 1) * STOREY, pBuilding.D), pBuilding.Yaw);
            pBuilding.volume = lVolume;
            GameObject lBox = Prop("BreakerBox", pPosition + Vector3.up * 0.7f, pYaw, null, Collide.Box);
            if (lBox == null) return Breaker(pPosition, pYaw, lVolume);
            lBox.isStatic = false;
            lBox.layer = GameLayers.INTERACTABLE;
            GameObject lLever = Block(lBox.transform, "Lever", pPosition + Vector3.up * 1.4f + Quaternion.Euler(0, pYaw, 0) * new Vector3(0.2f, 0, 0.25f), new Vector3(0.08f, 0.35f, 0.08f), new Color(0.9f, 0.2f, 0.2f), GameLayers.INTERACTABLE, false, Quaternion.Euler(0, pYaw, 0));
            lLever.isStatic = false;
            Breaker lBreaker = lBox.AddComponent<Breaker>();
            lBreaker.Setup(lVolume, lLever.transform);
            return lBreaker;
        }

        /// <summary>The basement air handler that gasses the whole building, with grills on every floor.</summary>
        public void VentKit(KitBuilding pBuilding, Vector3 pPosition, float pYaw)
        {
            List<Transform> lGrills = new List<Transform>();
            for (int f = 0; f < pBuilding.spec.floors; f++)
            {
                Vector3 lGrill = pBuilding.Local(0, f * STOREY + STOREY - 0.2f, -pBuilding.D * 0.2f);
                GameObject lGrillObject = Prop("VentGrill", lGrill, pBuilding.Yaw, null, Collide.None);
                lGrills.Add(lGrillObject != null ? lGrillObject.transform : VentGrill(lGrill));
            }
            BuildingVolume lVolume = pBuilding.volume;
            GameObject lUnit = Prop("AirHandler", pPosition, pYaw, new Color(0.7f, 0.8f, 0.82f), Collide.Box, true);
            if (lUnit == null) { Vent(pPosition, pYaw, lVolume, lGrills.ToArray()); return; }
            lUnit.isStatic = false;
            lUnit.layer = GameLayers.INTERACTABLE;
            lUnit.AddComponent<VentPoison>().Setup(lVolume, lGrills.ToArray());
        }

        public void DistractionKit(Vector3 pPosition, float pYaw, string pKind)
        {
            string lPiece = pKind switch { "radio" => "Radio", "tv" => "TV", "phone" => "Phone", "generator" => "Generator", _ => null };
            GameObject lObject = lPiece != null ? Prop(lPiece, pPosition, pYaw, null, Collide.Box) : null;
            if (lObject == null) { Distraction(pPosition, pYaw, pKind); return; }
            lObject.isStatic = false;
            lObject.layer = GameLayers.INTERACTABLE;
            string lPrompt = pKind switch { "radio" => "TURN THE RADIO ON", "tv" => "TURN THE TV ON", "phone" => "MAKE THE PHONE RING", _ => "START THE GENERATOR" };
            lObject.AddComponent<Distraction>().Setup(lPrompt, pKind, pKind == "generator" ? 28f : 16f);
        }

        /// <summary>A hiding spot from the kit: Wardrobe (closet), Dumpster or Haystack.</summary>
        public void HidingKit(Vector3 pPosition, float pYaw, string pPiece)
        {
            GameObject lObject = Prop(pPiece, pPosition, pYaw, null, Collide.None, true);
            if (lObject == null) { HidingSpot(pPosition, pYaw, pPiece == "Wardrobe" ? "closet" : pPiece == "Haystack" ? "hay" : "dumpster"); return; }
            lObject.isStatic = false;
            lObject.layer = GameLayers.INTERACTABLE;
            Bounds lBounds = lObject.GetComponent<MeshFilter>().sharedMesh.bounds;
            BoxCollider lSolid = lObject.AddComponent<BoxCollider>();
            lSolid.center = lBounds.center;
            lSolid.size = lBounds.size;
            BoxCollider lTrigger = lObject.AddComponent<BoxCollider>();
            lTrigger.center = lBounds.center;
            lTrigger.size = lBounds.size + Vector3.one * 0.6f;
            lTrigger.isTrigger = true;
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Transform lInside = new GameObject("Inside").transform;
            lInside.SetParent(Props, false);
            lInside.position = pPosition + Vector3.up * 0.05f;
            Transform lExit = new GameObject("Exit").transform;
            lExit.SetParent(Props, false);
            lExit.position = pPosition + lRot * new Vector3(0, 0.05f, lBounds.size.z * 0.5f + 0.9f);
            lObject.AddComponent<HidingSpot>().Setup(lInside, lExit, pPiece == "Wardrobe" ? "HIDE IN THE WARDROBE" : pPiece == "Haystack" ? "HIDE IN THE HAY" : "HIDE IN THE DUMPSTER");
            if (pPiece == "Haystack") lObject.AddComponent<Flammable>().Setup(15f, 3f, false, 1.2f);
        }

        /// <summary>Scatter nature pieces in a rectangle, avoiding the given rectangles.</summary>
        public void Scatter(Rect pArea, int pCount, string[] pPieces, float pMinScale, float pMaxScale, IList<Rect> pAvoid, Color? pTint = null, float pMinHeight = -100f)
        {
            int lPlaced = 0;
            for (int i = 0; i < pCount * 4 && lPlaced < pCount; i++)
            {
                float x = Mathf.Lerp(pArea.xMin, pArea.xMax, (float)Rng.NextDouble());
                float z = Mathf.Lerp(pArea.yMin, pArea.yMax, (float)Rng.NextDouble());
                bool lBlocked = false;
                if (pAvoid != null) foreach (Rect lRect in pAvoid) if (lRect.Contains(new Vector2(x, z))) { lBlocked = true; break; }
                if (lBlocked || GroundAt(x, z) < pMinHeight) continue;
                string lPiece = pPieces[Rng.Next(pPieces.Length)];
                Color lTint = FX.GameArt.Vary(pTint ?? DefaultTint("Nature", lPiece), 0.12f, Rng);
                NaturePiece(lPiece, At(x, z), (float)Rng.NextDouble() * 360f, Mathf.Lerp(pMinScale, pMaxScale, (float)Rng.NextDouble()), lTint);
                lPlaced++;
            }
        }
    }
}
