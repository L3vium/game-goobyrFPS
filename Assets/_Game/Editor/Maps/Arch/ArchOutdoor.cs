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
    /// <summary>
    /// Outdoor structures of the realistic maps, each one combined mesh: compound walls that follow
    /// the land (with gates), stone stairs, retaining walls, open sheds, market stalls, piers, a water
    /// tower, a radio mast, fuel tanks, a crashed DC-3, a stage, a fountain, garden walls, fences.
    /// </summary>
    public static class ArchOutdoor
    {
        private static int _count;

        private static GameObject Flush(ArchKit A, ArchMesh pMesh, string pName, Transform pParent = null)
        {
            GameObject lRoot = new GameObject(pName);
            lRoot.transform.SetParent(pParent ?? A.K.Env, false);
            lRoot.isStatic = true;
            pMesh.Flush(lRoot.transform, A.K.Map.sceneName + "_" + pName + "_" + (_count++));
            return lRoot;
        }

        public static void ResetCount() => _count = 0;

        /// <summary>A gap in a wall: segment index, position along it (0..1), width, and what fills it.</summary>
        public class Gate
        {
            public int segment;
            public float at;
            public float width = 3.5f;
            /// <summary>Null: an open gateway; otherwise a locked gate.</summary>
            public SecureDoor.Lock? locked;
            public string poi;
            public bool ironGate = true;
        }

        /// <summary>
        /// A masonry wall along a polyline that follows the ground (pieces every ~3 m, buried 0.6 m),
        /// capped, with gates (open or locked).
        /// </summary>
        public static void Wall(ArchKit A, Vector2[] pPath, bool pClosed, float pHeight, float pThickness, ArchMat pBody, ArchMat pCap, List<Gate> pGates = null)
        {
            ArchMesh lMesh = new ArchMesh();
            int lSegments = pClosed ? pPath.Length : pPath.Length - 1;
            for (int s = 0; s < lSegments; s++)
            {
                Vector2 a = pPath[s], b = pPath[(s + 1) % pPath.Length];
                float lLength = Vector2.Distance(a, b);
                Vector2 lDir = (b - a) / lLength;
                List<Vector2> lCuts = new List<Vector2>();
                if (pGates != null)
                    foreach (Gate g in pGates)
                        if (g.segment == s) lCuts.Add(new Vector2(g.at * lLength - g.width * 0.5f, g.at * lLength + g.width * 0.5f));
                lCuts.Sort((u, v) => u.x.CompareTo(v.x));
                List<Vector2> lSolid = new List<Vector2>();
                float lCursor = 0f;
                foreach (Vector2 c in lCuts) { if (c.x > lCursor) lSolid.Add(new Vector2(lCursor, c.x)); lCursor = c.y; }
                if (lCursor < lLength) lSolid.Add(new Vector2(lCursor, lLength));
                float lYaw = Mathf.Atan2(lDir.x, lDir.y) * Mathf.Rad2Deg;
                Quaternion lRot = Quaternion.Euler(0, lYaw, 0);
                foreach (Vector2 lRun in lSolid)
                {
                    int lPieces = Mathf.Max(1, Mathf.CeilToInt((lRun.y - lRun.x) / 3f));
                    float lPiece = (lRun.y - lRun.x) / lPieces;
                    for (int k = 0; k < lPieces; k++)
                    {
                        float t0 = lRun.x + k * lPiece, t1 = t0 + lPiece;
                        Vector2 c = a + lDir * ((t0 + t1) * 0.5f);
                        float lGround = Mathf.Min(A.K.GroundAt(a.x + lDir.x * t0, a.y + lDir.y * t0), A.K.GroundAt(a.x + lDir.x * t1, a.y + lDir.y * t1), A.K.GroundAt(c.x, c.y));
                        float lTop = Mathf.Max(A.K.GroundAt(a.x + lDir.x * t0, a.y + lDir.y * t0), A.K.GroundAt(a.x + lDir.x * t1, a.y + lDir.y * t1)) + pHeight;
                        float lBottom = lGround - 0.8f;
                        lMesh.Box(pBody, new Vector3(c.x, (lBottom + lTop) * 0.5f, c.y), new Vector3(pThickness, lTop - lBottom, lPiece + 0.02f), lRot);
                        lMesh.Box(pCap, new Vector3(c.x, lTop + 0.06f, c.y), new Vector3(pThickness + 0.12f, 0.12f, lPiece + 0.04f), lRot);
                    }
                }
                if (pGates != null)
                    foreach (Gate g in pGates)
                    {
                        if (g.segment != s) continue;
                        Vector2 c = a + lDir * (g.at * lLength);
                        float lGround = A.K.GroundAt(c.x, c.y);
                        // Gate piers.
                        foreach (float lSide in new[] { -1f, 1f })
                        {
                            Vector2 p = c + lDir * lSide * (g.width * 0.5f + 0.3f);
                            lMesh.Box(pBody, new Vector3(p.x, lGround + (pHeight + 0.6f) * 0.5f - 0.4f, p.y), new Vector3(pThickness + 0.25f, pHeight + 1.4f, 0.6f), lRot);
                            lMesh.Box(pCap, new Vector3(p.x, lGround + pHeight + 0.4f, p.y), new Vector3(pThickness + 0.4f, 0.16f, 0.75f), lRot);
                        }
                        Vector3 lCenter = new Vector3(c.x, lGround, c.y);
                        if (g.locked.HasValue) LockedGate(A, lCenter, lYaw, g.width, Mathf.Min(pHeight, 2.6f), g.locked.Value, g.ironGate);
                        else if (g.ironGate) OpenGate(lMesh, lCenter, lYaw, g.width, Mathf.Min(pHeight, 2.6f));
                        if (!string.IsNullOrEmpty(g.poi)) A.Poi(lCenter + Vector3.up, g.poi, MapPoi.Kind.Entrance);
                    }
            }
            Flush(A, lMesh, "Wall");
        }

        /// <summary>Wrought-iron gate leaves swung open against the wall.</summary>
        private static void OpenGate(ArchMesh pMesh, Vector3 pCenter, float pYaw, float pWidth, float pHeight)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Vector3 lAlong = lRot * Vector3.forward, lAcross = lRot * Vector3.right;
            foreach (float lSide in new[] { -1f, 1f })
            {
                Vector3 lHinge = pCenter + lAlong * lSide * pWidth * 0.5f + lAcross * 0.3f;
                float lLeaf = pWidth * 0.5f;
                for (float k = 0.05f; k < lLeaf; k += 0.14f)
                    pMesh.Box(ArchLib.Iron, lHinge + lAcross * k + Vector3.up * pHeight * 0.5f, new Vector3(0.025f, pHeight, 0.025f), lRot, false);
                pMesh.Box(ArchLib.Iron, lHinge + lAcross * lLeaf * 0.5f + Vector3.up * (pHeight - 0.05f), new Vector3(lLeaf, 0.05f, 0.05f), Quaternion.Euler(0, pYaw + 90f, 0), false);
                pMesh.Box(ArchLib.Iron, lHinge + lAcross * lLeaf * 0.5f + Vector3.up * 0.15f, new Vector3(lLeaf, 0.05f, 0.05f), Quaternion.Euler(0, pYaw + 90f, 0), false);
            }
        }

        /// <summary>A shut gate opened with a tool (a padlock, a crowbar for a rusted one).</summary>
        public static void LockedGate(ArchKit A, Vector3 pCenter, float pYaw, float pWidth, float pHeight, SecureDoor.Lock pLock, bool pIron)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            GameObject lDoor = new GameObject("SecureGate_" + pLock) { layer = GameLayers.INTERACTABLE };
            lDoor.transform.SetParent(A.Systems, false);
            lDoor.transform.SetPositionAndRotation(pCenter, lRot * Quaternion.Euler(0, 90f, 0));
            Transform lHinge = new GameObject("Hinge").transform;
            lHinge.SetParent(lDoor.transform, false);
            lHinge.localPosition = new Vector3(-pWidth * 0.5f, 0, 0);
            lHinge.gameObject.layer = GameLayers.INTERACTABLE;
            lHinge.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            ArchMesh lMesh = new ArchMesh();
            ArchMat lMat = pIron ? ArchLib.Iron : ArchLib.Surface("old_planks_02", 1.5f);
            if (pIron)
            {
                for (float k = 0.05f; k < pWidth; k += 0.14f) lMesh.Box(lMat, new Vector3(k, pHeight * 0.5f, 0), new Vector3(0.03f, pHeight, 0.03f), false);
                lMesh.Box(lMat, new Vector3(pWidth * 0.5f, pHeight - 0.05f, 0), new Vector3(pWidth, 0.06f, 0.05f), false);
                lMesh.Box(lMat, new Vector3(pWidth * 0.5f, 0.15f, 0), new Vector3(pWidth, 0.06f, 0.05f), false);
                lMesh.Box(lMat, new Vector3(pWidth * 0.5f, 1.1f, 0), new Vector3(pWidth, 0.06f, 0.05f), false);
                if (pLock == SecureDoor.Lock.Crowbar) lMesh.Box(ArchLib.Surface("rust_coarse_01", 1f), new Vector3(pWidth - 0.15f, 1.1f, 0.05f), new Vector3(0.3f, 0.3f, 0.06f), false);
            }
            else lMesh.Box(lMat, new Vector3(pWidth * 0.5f, pHeight * 0.5f, 0), new Vector3(pWidth, pHeight, 0.08f), false);
            if (pLock == SecureDoor.Lock.Padlock) lMesh.Box(ArchLib.Brass, new Vector3(pWidth - 0.1f, 1.1f, 0.06f), new Vector3(0.08f, 0.1f, 0.04f), false);
            GameObject lLeaf = new GameObject("Leaf") { layer = GameLayers.INTERACTABLE };
            lLeaf.transform.SetParent(lHinge, false);
            foreach (GameObject lPart in lMesh.Flush(lLeaf.transform, A.K.Map.sceneName + "_Gate_" + (_count++), GameLayers.INTERACTABLE)) lPart.isStatic = false;
            BoxCollider lBox = lLeaf.AddComponent<BoxCollider>();
            lBox.center = new Vector3(pWidth * 0.5f, pHeight * 0.5f, 0);
            lBox.size = new Vector3(pWidth, pHeight, 0.12f);
            lDoor.AddComponent<SecureDoor>().Setup(pLock, lHinge);
            NavMeshObstacle lObstacle = lDoor.AddComponent<NavMeshObstacle>();
            lObstacle.carving = true;
            lObstacle.shape = NavMeshObstacleShape.Box;
            lObstacle.center = new Vector3(0, pHeight * 0.5f, 0);
            lObstacle.size = new Vector3(pWidth + 0.2f, pHeight, 0.6f);
        }

        /// <summary>A straight run of stone steps (visible) with a smooth ramp collider, and side walls.</summary>
        public static void Stairs(ArchKit A, Vector3 pBottom, Vector3 pTop, float pWidth, ArchMat pMat = null, bool pSideWalls = true)
        {
            ArchMat lMat = pMat ?? ArchLib.Surface("stone_wall", 2f);
            Vector3 lFlat = new Vector3(pTop.x - pBottom.x, 0, pTop.z - pBottom.z);
            float lRise = pTop.y - pBottom.y, lRun = lFlat.magnitude;
            int lSteps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(lRise) / 0.17f));
            Quaternion lRot = Quaternion.LookRotation(lFlat.normalized);
            ArchMesh lMesh = new ArchMesh();
            for (int i = 0; i < lSteps; i++)
            {
                float lTop = pBottom.y + lRise * (i + 1) / lSteps;
                Vector3 lCenterFlat = pBottom + lFlat * ((i + 0.5f) / lSteps);
                float lLow = Mathf.Min(lTop - 0.8f, A.K.GroundAt(lCenterFlat.x, lCenterFlat.z) - 0.4f);
                lMesh.Box(lMat, new Vector3(lCenterFlat.x, (lLow + lTop) * 0.5f, lCenterFlat.z), new Vector3(pWidth, lTop - lLow, lRun / lSteps + 0.02f), lRot, false);
            }
            if (pSideWalls)
                foreach (float lSide in new[] { -1f, 1f })
                    for (int i = 0; i < lSteps; i += 2)
                    {
                        float lTop = pBottom.y + lRise * Mathf.Min(lSteps, i + 2) / lSteps + 0.6f;
                        Vector3 c = pBottom + lFlat * ((i + 1f) / lSteps) + lRot * Vector3.right * lSide * (pWidth * 0.5f + 0.2f);
                        float lLow = Mathf.Min(lTop - 1.4f, A.K.GroundAt(c.x, c.z) - 0.4f);
                        lMesh.Box(lMat, new Vector3(c.x, (lLow + lTop) * 0.5f, c.z), new Vector3(0.4f, lTop - lLow, lRun / lSteps * 2f + 0.02f), lRot);
                    }
            GameObject lRoot = Flush(A, lMesh, "Stairs");
            Vector3 lDir = pTop - pBottom;
            GameObject lRamp = new GameObject("Ramp");
            lRamp.transform.SetParent(lRoot.transform, false);
            Quaternion lSlope = Quaternion.LookRotation(lDir.normalized, Vector3.up);
            lRamp.transform.SetPositionAndRotation((pBottom + pTop) * 0.5f + lSlope * Vector3.down * 0.15f, lSlope);
            BoxCollider lBox = lRamp.AddComponent<BoxCollider>();
            lBox.size = new Vector3(pWidth, 0.3f, lDir.magnitude + 0.6f);
            lRamp.isStatic = true;
        }

        /// <summary>A stone retaining wall along a line, from below the lower ground to <paramref name="pTop"/>.</summary>
        public static void Retaining(ArchKit A, Vector2 pFrom, Vector2 pTo, float pTop, float pThickness = 0.8f, ArchMat pMat = null)
        {
            ArchMat lMat = pMat ?? ArchLib.Surface("stone_wall", 2.5f);
            ArchMesh lMesh = new ArchMesh();
            float lLength = Vector2.Distance(pFrom, pTo);
            Vector2 lDir = (pTo - pFrom) / lLength;
            Quaternion lRot = Quaternion.Euler(0, Mathf.Atan2(lDir.x, lDir.y) * Mathf.Rad2Deg, 0);
            int lPieces = Mathf.Max(1, Mathf.CeilToInt(lLength / 4f));
            for (int k = 0; k < lPieces; k++)
            {
                Vector2 c = pFrom + lDir * (lLength * (k + 0.5f) / lPieces);
                float lLow = A.K.GroundAt(c.x, c.y) - 6f;
                lMesh.Box(lMat, new Vector3(c.x, (lLow + pTop) * 0.5f, c.y), new Vector3(pThickness, pTop - lLow, lLength / lPieces + 0.02f), lRot);
            }
            Flush(A, lMesh, "Retaining");
        }

        /// <summary>An open shed (posts, a sloping roof) over a concrete slab: drying sheds, carports, markets.</summary>
        public static GameObject Shed(ArchKit A, Vector3 pCenter, float pYaw, float pWidth, float pDepth, float pHeight, ArchMat pRoof, ArchMat pPost = null, bool pSlab = true)
        {
            ArchMat lPost = pPost ?? ArchLib.Surface("rough_wood", 1.5f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pCenter, Quaternion.Euler(0, pYaw, 0), Vector3.one);
            if (pSlab) lMesh.BoxMinMax(ArchLib.Surface("concrete_floor_02", 3f), new Vector3(-pWidth * 0.5f, -0.6f, -pDepth * 0.5f), new Vector3(pWidth * 0.5f, 0.12f, pDepth * 0.5f));
            int lBays = Mathf.Max(1, Mathf.RoundToInt(pWidth / 4f));
            for (int i = 0; i <= lBays; i++)
                foreach (float z in new[] { -pDepth * 0.5f + 0.2f, pDepth * 0.5f - 0.2f })
                {
                    float x = -pWidth * 0.5f + 0.2f + (pWidth - 0.4f) * i / lBays;
                    float h = z > 0 ? pHeight + 0.6f : pHeight;
                    lMesh.Box(lPost, new Vector3(x, h * 0.5f, z), new Vector3(0.2f, h, 0.2f));
                }
            Vector3 a = new Vector3(-pWidth * 0.5f - 0.4f, pHeight, -pDepth * 0.5f - 0.5f), b = new Vector3(-pWidth * 0.5f - 0.4f, pHeight + 0.6f, pDepth * 0.5f + 0.5f);
            Vector3 c = new Vector3(pWidth * 0.5f + 0.4f, pHeight + 0.6f, pDepth * 0.5f + 0.5f), d = new Vector3(pWidth * 0.5f + 0.4f, pHeight, -pDepth * 0.5f - 0.5f);
            lMesh.QuadUv(pRoof, d, c, b, a, new Vector2(0, d.x), new Vector2(pDepth + 1, d.x), new Vector2(pDepth + 1, a.x), new Vector2(0, a.x));
            lMesh.Quad(pRoof, a - Vector3.up * 0.05f, b - Vector3.up * 0.05f, c - Vector3.up * 0.05f, d - Vector3.up * 0.05f);
            lMesh.Box(lPost, new Vector3(0, pHeight - 0.1f, -pDepth * 0.5f + 0.2f), new Vector3(pWidth, 0.2f, 0.2f));
            lMesh.Box(lPost, new Vector3(0, pHeight + 0.5f, pDepth * 0.5f - 0.2f), new Vector3(pWidth, 0.2f, 0.2f));
            return Flush(A, lMesh, "Shed");
        }

        /// <summary>A market stall: a counter, four posts, a striped fabric awning.</summary>
        public static void Stall(ArchKit A, Vector3 pGround, float pYaw, Color pAwning)
        {
            ArchMat lWood = ArchLib.Surface("rough_wood", 1.2f), lCloth = ArchLib.Tinted("rough_linen", pAwning, "awning_" + ColorUtility.ToHtmlStringRGB(pAwning), 1f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pGround, Quaternion.Euler(0, pYaw, 0), Vector3.one);
            lMesh.Box(lWood, new Vector3(0, 0.45f, 0.4f), new Vector3(2.4f, 0.9f, 0.8f));
            lMesh.Box(lWood, new Vector3(0, 0.92f, 0.45f), new Vector3(2.5f, 0.05f, 0.95f));
            foreach (float x in new[] { -1.2f, 1.2f })
                foreach (float z in new[] { 0f, 1.6f })
                    lMesh.Box(lWood, new Vector3(x, 1.2f, z), new Vector3(0.08f, 2.4f, 0.08f), false);
            lMesh.Quad(lCloth, new Vector3(1.4f, 2.5f, -0.3f), new Vector3(1.4f, 2.35f, 1.8f), new Vector3(-1.4f, 2.35f, 1.8f), new Vector3(-1.4f, 2.5f, -0.3f), false);
            lMesh.Quad(lCloth, new Vector3(-1.4f, 2.5f, -0.3f), new Vector3(-1.4f, 2.35f, 1.8f), new Vector3(1.4f, 2.35f, 1.8f), new Vector3(1.4f, 2.5f, -0.3f), false);
            // Produce: crates of fruit as coloured boxes.
            Color[] lFruit = { new Color(0.85f, 0.45f, 0.1f), new Color(0.75f, 0.15f, 0.1f), new Color(0.35f, 0.55f, 0.15f), new Color(0.9f, 0.8f, 0.2f) };
            for (int i = 0; i < 4; i++)
                lMesh.Box(ArchLib.PlainMat("Fruit" + i, lFruit[i], 0.4f, 0f), new Vector3(-0.9f + i * 0.6f, 1.02f, 0.45f), new Vector3(0.5f, 0.14f, 0.4f), false);
            Flush(A, lMesh, "Stall", A.Props);
        }

        /// <summary>A timber pier on posts from <paramref name="pFrom"/> to <paramref name="pTo"/> at a deck height.</summary>
        public static void Pier(ArchKit A, Vector2 pFrom, Vector2 pTo, float pWidth, float pDeck)
        {
            ArchMat lDeck = ArchLib.Surface("wood_floor_deck", 2f), lPost = ArchLib.Surface("rough_wood", 1.5f);
            ArchMesh lMesh = new ArchMesh();
            Vector2 lDir = (pTo - pFrom).normalized;
            float lLength = Vector2.Distance(pFrom, pTo);
            Quaternion lRot = Quaternion.Euler(0, Mathf.Atan2(lDir.x, lDir.y) * Mathf.Rad2Deg, 0);
            Vector2 c = (pFrom + pTo) * 0.5f;
            lMesh.Box(lDeck, new Vector3(c.x, pDeck - 0.1f, c.y), new Vector3(pWidth, 0.2f, lLength), lRot);
            for (float t = 0f; t <= lLength; t += 3f)
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 p = pFrom + lDir * t + new Vector2(lDir.y, -lDir.x) * s * (pWidth * 0.5f - 0.15f);
                    lMesh.Cylinder(lPost, new Vector3(p.x, pDeck - 6f, p.y), 0.14f, 6.8f, 8);
                }
            Flush(A, lMesh, "Pier");
        }

        /// <summary>A water tower on four legs with a tank, a gallery and a ladder (climbable).</summary>
        public static Vector3 WaterTower(ArchKit A, Vector3 pBase, float pHeight, float pYaw)
        {
            ArchMat lWood = ArchLib.Surface("rough_wood", 1.5f), lTank = ArchLib.Surface("rusty_metal_02", 2f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pBase, Quaternion.Euler(0, pYaw, 0), Vector3.one);
            float s = 2.2f;
            foreach (float x in new[] { -s, s })
                foreach (float z in new[] { -s, s })
                    lMesh.Box(lWood, new Vector3(x, pHeight * 0.5f - 0.5f, z), new Vector3(0.3f, pHeight + 1f, 0.3f));
            for (float y = 2.5f; y < pHeight; y += 3f)
                for (int k = 0; k < 4; k++)
                {
                    Quaternion r = Quaternion.Euler(0, k * 90f, 0);
                    lMesh.Box(lWood, r * new Vector3(0, y, s), new Vector3(s * 2f, 0.16f, 0.16f), r, false);
                }
            lMesh.Box(lWood, new Vector3(0, pHeight + 0.1f, 0), new Vector3(s * 2f + 1.6f, 0.2f, s * 2f + 1.6f));
            for (int k = 0; k < 4; k++)
            {
                Quaternion r = Quaternion.Euler(0, k * 90f, 0);
                lMesh.Box(ArchLib.Iron, r * new Vector3(0, pHeight + 1.15f, s + 0.75f), new Vector3(s * 2f + 1.5f, 0.06f, 0.06f), r);
                lMesh.Box(ArchLib.Invisible, r * new Vector3(0, pHeight + 0.65f, s + 0.75f), new Vector3(s * 2f + 1.5f, 1.0f, 0.06f), r);
                for (float x = -s - 0.7f; x < s + 0.8f; x += 0.5f) lMesh.Box(ArchLib.Iron, r * new Vector3(x, pHeight + 0.7f, s + 0.75f), new Vector3(0.04f, 0.9f, 0.04f), r, false);
            }
            lMesh.Cylinder(lTank, new Vector3(0, pHeight + 2.3f, 0), 1.5f, 3.6f, 18);
            foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -0.9f, 0.9f }) lMesh.Box(lWood, new Vector3(x, pHeight + 1.25f, z), new Vector3(0.14f, 2.1f, 0.14f));
            Flush(A, lMesh, "WaterTower");
            Vector3 lOut = Quaternion.Euler(0, pYaw, 0) * Vector3.back;
            A.Climb(pBase + lOut * (s + 0.25f), pHeight + 0.2f, lOut, true, "Water tower ladder");
            return pBase + Vector3.up * (pHeight + 0.25f);
        }

        /// <summary>A lattice radio mast with guy wires (visual) and a lamp on top.</summary>
        public static void Mast(ArchKit A, Vector3 pBase, float pHeight)
        {
            ArchMesh lMesh = new ArchMesh();
            ArchMat lSteel = ArchLib.PlainMat("MastRed", new Color(0.65f, 0.15f, 0.1f), 0.4f, 0.6f);
            float r = 0.6f;
            Vector3[] lLegs = { new Vector3(r, 0, 0), new Vector3(-r * 0.5f, 0, r * 0.87f), new Vector3(-r * 0.5f, 0, -r * 0.87f) };
            foreach (Vector3 l in lLegs) lMesh.Tube(lSteel, pBase + l, pBase + l * 0.4f + Vector3.up * pHeight, 0.05f, 6);
            for (float y = 0f; y < pHeight; y += 1.6f)
                for (int k = 0; k < 3; k++)
                {
                    float t0 = y / pHeight, t1 = Mathf.Min(1f, (y + 1.6f) / pHeight);
                    Vector3 a = pBase + lLegs[k] * Mathf.Lerp(1f, 0.4f, t0) + Vector3.up * y, b = pBase + lLegs[(k + 1) % 3] * Mathf.Lerp(1f, 0.4f, t1) + Vector3.up * (y + 1.6f);
                    lMesh.Tube(lSteel, a, b, 0.02f, 4, false);
                }
            lMesh.Box(ArchLib.Invisible, pBase + Vector3.up * pHeight * 0.5f, new Vector3(r * 1.4f, pHeight, r * 1.4f));
            Flush(A, lMesh, "Mast");
            A.Lamp(pBase + Vector3.up * (pHeight + 0.3f), 0f, new Color(1f, 0.15f, 0.1f), 6f, 2f, false);
        }

        /// <summary>A fuel tank (horizontal cylinder on saddles), shootable.</summary>
        public static void FuelTank(ArchKit A, Vector3 pGround, float pYaw, float pLength = 6f, float pRadius = 1.2f)
        {
            ArchMesh lMesh = new ArchMesh();
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Vector3 lAxis = lRot * Vector3.forward * (pLength * 0.5f);
            Vector3 lWorld = pGround;
            pGround = Vector3.zero;
            Vector3 c = pGround + Vector3.up * (pRadius + 0.4f);
            lMesh.Tube(ArchLib.PlainMat("TankWhite", new Color(0.85f, 0.84f, 0.8f), 0.45f, 0.3f), c - lAxis, c + lAxis, pRadius, 16);
            foreach (float t in new[] { -0.35f, 0.35f }) lMesh.Box(ArchLib.Surface("concrete_floor_02", 2f), pGround + lAxis * t * 2f + Vector3.up * 0.3f, new Vector3(pRadius * 2f, 0.6f, 0.5f), lRot);
            GameObject lTank = Flush(A, lMesh, "FuelTank", A.Systems);
            foreach (Transform t in lTank.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            lTank.transform.position = lWorld;
            lTank.AddComponent<ExplosiveProp>();
        }

        /// <summary>The crashed DC-3: a fuselage tube, wings, a tail, engines, broken off, rusted.</summary>
        public static void Dc3(ArchKit A, Vector3 pGround, float pYaw)
        {
            ArchMat lSkin = ArchLib.Surface("rusty_metal_02", 2f), lAlu = ArchLib.Surface("metal_plate_02", 2f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pGround, Quaternion.Euler(-3f, pYaw, 4f), Vector3.one);
            lMesh.Tube(lAlu, new Vector3(0, 1.6f, -8f), new Vector3(0, 1.8f, 7f), 1.25f, 16);
            lMesh.Tube(lSkin, new Vector3(0, 1.8f, 7f), new Vector3(0, 1.9f, 8.6f), 0.9f, 14);
            lMesh.Tube(lAlu, new Vector3(0, 1.6f, -8f), new Vector3(0, 2.2f, -12f), 0.7f, 12);
            lMesh.Box(lAlu, new Vector3(-6.5f, 1.0f, 1.5f), new Vector3(10f, 0.25f, 3.2f), Quaternion.Euler(0, 0, -6f));
            lMesh.Box(lSkin, new Vector3(4.5f, 0.6f, 1.2f), new Vector3(6f, 0.25f, 3f), Quaternion.Euler(0, 12f, 9f));
            lMesh.Tube(lSkin, new Vector3(-3.4f, 1.2f, 3.6f), new Vector3(-3.4f, 1.3f, 0.2f), 0.7f, 12);
            lMesh.Tube(lSkin, new Vector3(3.6f, 0.7f, 3.2f), new Vector3(3.6f, 0.8f, 0.5f), 0.7f, 12);
            lMesh.Box(lAlu, new Vector3(0, 3.6f, -11.5f), new Vector3(0.2f, 2.8f, 2.2f));
            lMesh.Box(lAlu, new Vector3(0, 2.4f, -11.6f), new Vector3(5.5f, 0.15f, 1.6f));
            Flush(A, lMesh, "DC3");
        }

        /// <summary>The fiesta stage: a plank platform on trestles, steps, a backdrop, lights.</summary>
        public static void Stage(ArchKit A, Vector3 pGround, float pYaw, float pWidth, float pDepth)
        {
            ArchMat lDeck = ArchLib.Surface("wood_floor_deck", 2f), lFrame = ArchLib.Surface("rough_wood", 1.2f), lCloth = ArchLib.Tinted("velour_velvet", new Color(0.7f, 0.08f, 0.1f), "stage", 1.5f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pGround, Quaternion.Euler(0, pYaw, 0), Vector3.one);
            lMesh.Box(lDeck, new Vector3(0, 1.0f, 0), new Vector3(pWidth, 0.15f, pDepth));
            lMesh.Box(lCloth, new Vector3(0, 0.47f, -pDepth * 0.5f + 0.02f), new Vector3(pWidth, 0.9f, 0.03f));
            for (float x = -pWidth * 0.5f + 0.3f; x <= pWidth * 0.5f; x += 2f)
                foreach (float z in new[] { -pDepth * 0.5f + 0.3f, pDepth * 0.5f - 0.3f })
                    lMesh.Box(lFrame, new Vector3(x, 0.47f, z), new Vector3(0.12f, 0.95f, 0.12f), false);
            lMesh.Box(lCloth, new Vector3(0, 3.0f, pDepth * 0.5f - 0.1f), new Vector3(pWidth, 4f, 0.06f));
            foreach (float x in new[] { -pWidth * 0.5f, pWidth * 0.5f }) lMesh.Box(lFrame, new Vector3(x, 2.6f, pDepth * 0.5f - 0.1f), new Vector3(0.2f, 5.2f, 0.2f));
            lMesh.Box(lFrame, new Vector3(0, 5.1f, pDepth * 0.5f - 0.1f), new Vector3(pWidth + 0.2f, 0.2f, 0.2f));
            // Steps up the side.
            for (int i = 0; i < 5; i++) lMesh.Box(lFrame, new Vector3(pWidth * 0.5f + 0.5f, 0.1f + i * 0.18f, -pDepth * 0.5f + 0.6f + i * 0.28f), new Vector3(1.0f, 0.2f + i * 0.36f, 0.3f), false);
            GameObject lRoot = Flush(A, lMesh, "Stage", A.Props);
            GameObject lRamp = new GameObject("StageSteps");
            lRamp.transform.SetParent(lRoot.transform, false);
            Vector3 a = pGround + Quaternion.Euler(0, pYaw, 0) * new Vector3(pWidth * 0.5f + 0.5f, 0f, -pDepth * 0.5f + 0.3f), b = pGround + Quaternion.Euler(0, pYaw, 0) * new Vector3(pWidth * 0.5f + 0.5f, 1.08f, -pDepth * 0.5f + 2.2f);
            Quaternion lSlope = Quaternion.LookRotation((b - a).normalized);
            lRamp.transform.SetPositionAndRotation((a + b) * 0.5f + lSlope * Vector3.down * 0.1f, lSlope);
            lRamp.AddComponent<BoxCollider>().size = new Vector3(1f, 0.2f, Vector3.Distance(a, b) + 0.3f);
            for (int k = 0; k < 4; k++)
                A.Lamp(pGround + Quaternion.Euler(0, pYaw, 0) * new Vector3(-pWidth * 0.4f + k * pWidth * 0.27f, 5f, pDepth * 0.5f - 0.4f), 0f, new Color(1f, 0.6f, 0.35f), 12f, 2.5f, false);
        }

        /// <summary>A stone fountain: a round basin with water, a column.</summary>
        public static void Fountain(ArchKit A, Vector3 pGround, float pRadius)
        {
            ArchMat lStone = ArchLib.Surface("large_sandstone_blocks", 2f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Cylinder(lStone, pGround - Vector3.up * 0.3f, pRadius, 0.9f, 20);
            lMesh.Cylinder(lStone, pGround, 0.35f, 1.8f, 12);
            lMesh.Cylinder(lStone, pGround + Vector3.up * 1.8f, 0.9f, 0.18f, 14);
            lMesh.Cylinder(ArchLib.PlainMat("FountainWater", new Color(0.25f, 0.4f, 0.42f), 0.95f, 0f), pGround + Vector3.up * 0.58f, pRadius - 0.25f, 0.02f, 20, false);
            Flush(A, lMesh, "Fountain", A.Props);
        }

        /// <summary>A plain low fence of posts and rails along a line.</summary>
        public static void Fence(ArchKit A, Vector2 pFrom, Vector2 pTo, float pHeight = 1.1f)
        {
            ArchMat lWood = ArchLib.Surface("rough_wood", 1.2f);
            ArchMesh lMesh = new ArchMesh();
            float lLength = Vector2.Distance(pFrom, pTo);
            Vector2 lDir = (pTo - pFrom) / lLength;
            Quaternion lRot = Quaternion.Euler(0, Mathf.Atan2(lDir.x, lDir.y) * Mathf.Rad2Deg, 0);
            for (float t = 0f; t <= lLength; t += 2.4f)
            {
                Vector2 p = pFrom + lDir * t;
                float g = A.K.GroundAt(p.x, p.y);
                lMesh.Box(lWood, new Vector3(p.x, g + pHeight * 0.5f - 0.2f, p.y), new Vector3(0.12f, pHeight + 0.4f, 0.12f), lRot);
            }
            for (float t = 0f; t < lLength; t += 2.4f)
            {
                float t1 = Mathf.Min(lLength, t + 2.4f);
                Vector2 a = pFrom + lDir * t, b = pFrom + lDir * t1, c = (a + b) * 0.5f;
                float g = (A.K.GroundAt(a.x, a.y) + A.K.GroundAt(b.x, b.y)) * 0.5f;
                foreach (float h in new[] { 0.45f, 0.9f }) lMesh.Box(lWood, new Vector3(c.x, g + h * pHeight / 1.1f, c.y), new Vector3(0.05f, 0.1f, t1 - t), lRot);
            }
            Flush(A, lMesh, "Fence");
        }
    }
}
