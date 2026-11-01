using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>A PBR surface and how many metres one repeat of its texture covers.</summary>
    public class ArchMat
    {
        public readonly string id;
        public readonly Material material;
        public readonly float tile;

        public ArchMat(string pId, Material pMaterial, float pTile)
        {
            id = pId;
            material = pMaterial;
            tile = pTile;
        }
    }

    /// <summary>
    /// Accumulates the geometry of one building part (walls, floors, trim, roofs) in the local frame
    /// of its root, one batch per material (and per "solid or visual only"), with world-scale box UVs
    /// (each face projected on the plane its normal faces, in metres / tile) and flat normals.
    /// <see cref="Flush"/> writes one mesh per batch, saved as an asset, with a mesh collider on the
    /// solid ones: a whole house is a handful of renderers, not thousands of blocks.
    /// </summary>
    public class ArchMesh
    {
        private class Batch
        {
            public ArchMat mat;
            public bool solid;
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        private readonly Dictionary<string, Batch> _batches = new Dictionary<string, Batch>();

        /// <summary>Applied to every point added (the frame of a stair flight, a rotated wing...).</summary>
        public Matrix4x4 Frame = Matrix4x4.identity;

        public int Triangles
        {
            get
            {
                int lCount = 0;
                foreach (Batch lBatch in _batches.Values) lCount += lBatch.t.Count / 3;
                return lCount;
            }
        }

        private Batch Get(ArchMat pMat, bool pSolid)
        {
            string lKey = pMat.id + (pSolid ? "#s" : "#v");
            if (!_batches.TryGetValue(lKey, out Batch lBatch)) _batches[lKey] = lBatch = new Batch { mat = pMat, solid = pSolid };
            return lBatch;
        }

        private static Vector2 BoxUv(Vector3 p, Vector3 pNormal, float pTile)
        {
            Vector3 a = new Vector3(Mathf.Abs(pNormal.x), Mathf.Abs(pNormal.y), Mathf.Abs(pNormal.z));
            Vector2 lUv;
            if (a.y >= a.x && a.y >= a.z) lUv = new Vector2(p.x, p.z);
            else if (a.x >= a.z) lUv = new Vector2(pNormal.x > 0 ? -p.z : p.z, p.y);
            else lUv = new Vector2(pNormal.z > 0 ? p.x : -p.x, p.y);
            return lUv / Mathf.Max(0.05f, pTile);
        }

        /// <summary>A flat quad a-b-c-d (counter-clockwise seen from the front), box-mapped.</summary>
        public void Quad(ArchMat pMat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool pSolid = true)
        {
            if (pMat == null) return;
            a = Frame.MultiplyPoint3x4(a); b = Frame.MultiplyPoint3x4(b); c = Frame.MultiplyPoint3x4(c); d = Frame.MultiplyPoint3x4(d);
            Vector3 lNormal = Vector3.Cross(c - a, b - a);
            if (lNormal.sqrMagnitude < 1e-10f) lNormal = Vector3.Cross(d - a, c - a);
            lNormal.Normalize();
            Batch lBatch = Get(pMat, pSolid);
            int s = lBatch.v.Count;
            lBatch.v.Add(a); lBatch.v.Add(b); lBatch.v.Add(c); lBatch.v.Add(d);
            for (int i = 0; i < 4; i++) lBatch.n.Add(lNormal);
            lBatch.uv.Add(BoxUv(a, lNormal, pMat.tile)); lBatch.uv.Add(BoxUv(b, lNormal, pMat.tile));
            lBatch.uv.Add(BoxUv(c, lNormal, pMat.tile)); lBatch.uv.Add(BoxUv(d, lNormal, pMat.tile));
            lBatch.t.Add(s); lBatch.t.Add(s + 2); lBatch.t.Add(s + 1);
            lBatch.t.Add(s); lBatch.t.Add(s + 3); lBatch.t.Add(s + 2);
        }

        /// <summary>A quad with explicit UVs (roof slopes: u along the eave, v up the slope).</summary>
        public void QuadUv(ArchMat pMat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, bool pSolid = true)
        {
            if (pMat == null) return;
            a = Frame.MultiplyPoint3x4(a); b = Frame.MultiplyPoint3x4(b); c = Frame.MultiplyPoint3x4(c); d = Frame.MultiplyPoint3x4(d);
            Vector3 lNormal = Vector3.Cross(c - a, b - a);
            if (lNormal.sqrMagnitude < 1e-10f) lNormal = Vector3.Cross(d - a, c - a);
            lNormal.Normalize();
            Batch lBatch = Get(pMat, pSolid);
            int s = lBatch.v.Count;
            lBatch.v.Add(a); lBatch.v.Add(b); lBatch.v.Add(c); lBatch.v.Add(d);
            for (int i = 0; i < 4; i++) lBatch.n.Add(lNormal);
            float k = 1f / Mathf.Max(0.05f, pMat.tile);
            lBatch.uv.Add(ua * k); lBatch.uv.Add(ub * k); lBatch.uv.Add(uc * k); lBatch.uv.Add(ud * k);
            lBatch.t.Add(s); lBatch.t.Add(s + 2); lBatch.t.Add(s + 1);
            lBatch.t.Add(s); lBatch.t.Add(s + 3); lBatch.t.Add(s + 2);
        }

        /// <summary>The same quad seen from both sides.</summary>
        public void DoubleQuad(ArchMat pMat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool pSolid = true)
        {
            Quad(pMat, a, b, c, d, pSolid);
            Quad(pMat, d, c, b, a, pSolid);
        }

        public void Tri(ArchMat pMat, Vector3 a, Vector3 b, Vector3 c, bool pSolid = true)
        {
            if (pMat == null) return;
            a = Frame.MultiplyPoint3x4(a); b = Frame.MultiplyPoint3x4(b); c = Frame.MultiplyPoint3x4(c);
            Vector3 lNormal = Vector3.Cross(c - a, b - a).normalized;
            Batch lBatch = Get(pMat, pSolid);
            int s = lBatch.v.Count;
            lBatch.v.Add(a); lBatch.v.Add(b); lBatch.v.Add(c);
            for (int i = 0; i < 3; i++) lBatch.n.Add(lNormal);
            lBatch.uv.Add(BoxUv(a, lNormal, pMat.tile)); lBatch.uv.Add(BoxUv(b, lNormal, pMat.tile)); lBatch.uv.Add(BoxUv(c, lNormal, pMat.tile));
            lBatch.t.Add(s); lBatch.t.Add(s + 2); lBatch.t.Add(s + 1);
        }

        /// <summary>Faces of a box to leave out (hidden against a wall or a floor).</summary>
        [System.Flags] public enum Faces { None = 0, Bottom = 1, Top = 2, Left = 4, Right = 8, Back = 16, Front = 32 }

        /// <summary>An oriented box (centre, size, rotation in the local frame).</summary>
        public void Box(ArchMat pMat, Vector3 pCenter, Vector3 pSize, Quaternion pRotation, bool pSolid = true, Faces pSkip = Faces.None)
        {
            if (pMat == null || pSize.x <= 0.001f || pSize.y <= 0.001f || pSize.z <= 0.001f) return;
            Vector3 h = pSize * 0.5f;
            Vector3 P(float x, float y, float z) => pCenter + pRotation * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 p000 = P(-1, -1, -1), p100 = P(1, -1, -1), p010 = P(-1, 1, -1), p110 = P(1, 1, -1);
            Vector3 p001 = P(-1, -1, 1), p101 = P(1, -1, 1), p011 = P(-1, 1, 1), p111 = P(1, 1, 1);
            if ((pSkip & Faces.Front) == 0) Quad(pMat, p001, p011, p111, p101, pSolid);    // +z
            if ((pSkip & Faces.Back) == 0) Quad(pMat, p100, p110, p010, p000, pSolid);     // -z
            if ((pSkip & Faces.Right) == 0) Quad(pMat, p101, p111, p110, p100, pSolid);    // +x
            if ((pSkip & Faces.Left) == 0) Quad(pMat, p000, p010, p011, p001, pSolid);     // -x
            if ((pSkip & Faces.Top) == 0) Quad(pMat, p010, p110, p111, p011, pSolid);      // +y
            if ((pSkip & Faces.Bottom) == 0) Quad(pMat, p000, p001, p101, p100, pSolid);   // -y
        }

        public void Box(ArchMat pMat, Vector3 pCenter, Vector3 pSize, bool pSolid = true, Faces pSkip = Faces.None) => Box(pMat, pCenter, pSize, Quaternion.identity, pSolid, pSkip);

        /// <summary>An axis-aligned box from its min and max corners.</summary>
        public void BoxMinMax(ArchMat pMat, Vector3 pMin, Vector3 pMax, bool pSolid = true, Faces pSkip = Faces.None) => Box(pMat, (pMin + pMax) * 0.5f, pMax - pMin, Quaternion.identity, pSolid, pSkip);

        /// <summary>A quad with its own vertex normals and UVs (smooth curved surfaces).</summary>
        public void QuadSmooth(ArchMat pMat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, bool pSolid = true)
        {
            if (pMat == null) return;
            a = Frame.MultiplyPoint3x4(a); b = Frame.MultiplyPoint3x4(b); c = Frame.MultiplyPoint3x4(c); d = Frame.MultiplyPoint3x4(d);
            na = Frame.MultiplyVector(na).normalized; nb = Frame.MultiplyVector(nb).normalized; nc = Frame.MultiplyVector(nc).normalized; nd = Frame.MultiplyVector(nd).normalized;
            Batch lBatch = Get(pMat, pSolid);
            int s = lBatch.v.Count;
            lBatch.v.Add(a); lBatch.v.Add(b); lBatch.v.Add(c); lBatch.v.Add(d);
            lBatch.n.Add(na); lBatch.n.Add(nb); lBatch.n.Add(nc); lBatch.n.Add(nd);
            float k = 1f / Mathf.Max(0.05f, pMat.tile);
            lBatch.uv.Add(ua * k); lBatch.uv.Add(ub * k); lBatch.uv.Add(uc * k); lBatch.uv.Add(ud * k);
            lBatch.t.Add(s); lBatch.t.Add(s + 2); lBatch.t.Add(s + 1);
            lBatch.t.Add(s); lBatch.t.Add(s + 3); lBatch.t.Add(s + 2);
        }

        /// <summary>A vertical cylinder (column, pipe, post) from its base centre: smooth sides, flat caps.</summary>
        public void Cylinder(ArchMat pMat, Vector3 pBase, float pRadius, float pHeight, int pSides = 12, bool pSolid = true, bool pCaps = true)
        {
            for (int i = 0; i < pSides; i++)
            {
                float a0 = i * Mathf.PI * 2f / pSides, a1 = (i + 1) * Mathf.PI * 2f / pSides;
                Vector3 n0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), n1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 d0 = n0 * pRadius, d1 = n1 * pRadius, h = Vector3.up * pHeight;
                float u0 = a0 * pRadius, u1 = a1 * pRadius;
                QuadSmooth(pMat, pBase + d1, pBase + d1 + h, pBase + d0 + h, pBase + d0, n1, n1, n0, n0,
                    new Vector2(-u1, 0), new Vector2(-u1, pHeight), new Vector2(-u0, pHeight), new Vector2(-u0, 0), pSolid);
                if (pCaps)
                {
                    Tri(pMat, pBase + h, pBase + d0 + h, pBase + d1 + h, pSolid);
                    Tri(pMat, pBase, pBase + d1, pBase + d0, pSolid);
                }
            }
        }

        /// <summary>A hemisphere (a radome, a dish when rotated), open at its base: smooth outside, a plain inside.</summary>
        public void Dome(ArchMat pMat, Vector3 pCenter, float pRadius, int pSegments, int pRings, Quaternion pRotation, bool pSolid = true, ArchMat pInside = null)
        {
            for (int r = 0; r < pRings; r++)
            {
                float a0 = r * Mathf.PI * 0.5f / pRings, a1 = (r + 1) * Mathf.PI * 0.5f / pRings;
                for (int s = 0; s < pSegments; s++)
                {
                    float b0 = s * Mathf.PI * 2f / pSegments, b1 = (s + 1) * Mathf.PI * 2f / pSegments;
                    Vector3 N(float a, float b) => pRotation * new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(b));
                    Vector3 P(float a, float b) => pCenter + N(a, b) * pRadius;
                    Vector2 U(float a, float b) => new Vector2(b * pRadius, a * pRadius);
                    QuadSmooth(pMat, P(a0, b1), P(a1, b1), P(a1, b0), P(a0, b0), N(a0, b1), N(a1, b1), N(a1, b0), N(a0, b0), U(a0, b1), U(a1, b1), U(a1, b0), U(a0, b0), pSolid);
                    QuadSmooth(pInside ?? pMat, P(a0, b0), P(a1, b0), P(a1, b1), P(a0, b1), -N(a0, b0), -N(a1, b0), -N(a1, b1), -N(a0, b1), U(a0, b0), U(a1, b0), U(a1, b1), U(a0, b1), false);
                }
            }
        }

        /// <summary>A cylinder between two points (beams, pipes, rails, fuselages).</summary>
        public void Tube(ArchMat pMat, Vector3 pFrom, Vector3 pTo, float pRadius, int pSides = 10, bool pSolid = true)
        {
            Vector3 lAxis = pTo - pFrom;
            if (lAxis.sqrMagnitude < 1e-6f) return;
            Quaternion lRot = Quaternion.FromToRotation(Vector3.up, lAxis.normalized);
            Matrix4x4 lOld = Frame;
            Frame = lOld * Matrix4x4.TRS(pFrom, lRot, Vector3.one);
            Cylinder(pMat, Vector3.zero, pRadius, lAxis.magnitude, pSides, pSolid);
            Frame = lOld;
        }

        /// <summary>
        /// The batches as child objects of <paramref name="pParent"/> (meshes saved as assets named
        /// <paramref name="pName"/>_material), solid ones with mesh colliders.
        /// </summary>
        public List<GameObject> Flush(Transform pParent, string pName, int pLayer = 0, bool pShadows = true)
        {
            List<GameObject> lObjects = new List<GameObject>();
            foreach (Batch lBatch in _batches.Values)
            {
                if (lBatch.t.Count == 0) continue;
                Mesh lMesh = new Mesh { indexFormat = lBatch.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                lMesh.SetVertices(lBatch.v);
                lMesh.SetNormals(lBatch.n);
                lMesh.SetUVs(0, lBatch.uv);
                lMesh.SetTriangles(lBatch.t, 0);
                lMesh.RecalculateBounds();
                lMesh.RecalculateTangents();
                string lName = Sanitize(pName + "_" + lBatch.mat.id + (lBatch.solid ? "" : "_v"));
                lMesh = EditorAssets.SaveMesh(lMesh, lName);
                GameObject lObject = new GameObject(lBatch.mat.id + (lBatch.solid ? "" : " (visual)")) { layer = pLayer };
                lObject.transform.SetParent(pParent, false);
                lObject.isStatic = true;
                lObjects.Add(lObject);
                if (lBatch.mat.material == null)
                {
                    // Collision only (balustrade gaps, invisible rails).
                    lObject.AddComponent<MeshCollider>().sharedMesh = lMesh;
                    continue;
                }
                lObject.AddComponent<MeshFilter>().sharedMesh = lMesh;
                MeshRenderer lRenderer = lObject.AddComponent<MeshRenderer>();
                lRenderer.sharedMaterial = lBatch.mat.material;
                lRenderer.shadowCastingMode = pShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                if (lBatch.solid) lObject.AddComponent<MeshCollider>().sharedMesh = lMesh;
            }
            _batches.Clear();
            return lObjects;
        }

        private static string Sanitize(string pName)
        {
            char[] lChars = pName.ToCharArray();
            for (int i = 0; i < lChars.Length; i++) if (!char.IsLetterOrDigit(lChars[i]) && lChars[i] != '_' && lChars[i] != '-') lChars[i] = '_';
            return new string(lChars);
        }
    }
}
