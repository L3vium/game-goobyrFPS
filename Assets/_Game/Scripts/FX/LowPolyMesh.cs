using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Flat-shaded low-poly meshes built in code: every triangle has its own vertices, so the
    /// lighting shows each facet - the chunky, soft-toy look of the whole game.
    /// Shared meshes are cached by key; the same generator feeds the editor builders (trees,
    /// rocks saved as assets) and the runtime (characters, debris).
    /// </summary>
    public static class LowPolyMesh
    {
        private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

        public static Mesh Cached(string pKey, System.Func<Mesh> pBuild)
        {
            if (_cache.TryGetValue(pKey, out Mesh lMesh) && lMesh != null) return lMesh;
            lMesh = pBuild();
            lMesh.name = pKey;
            _cache[pKey] = lMesh;
            return lMesh;
        }

        // ---------------------------------------------------------------- primitives

        /// <summary>An icosphere (0-2 subdivisions) with optional noise on its vertices.</summary>
        public static Mesh Icosphere(int pSubdivisions, float pNoise = 0f, int pSeed = 0, Color? pColor = null)
        {
            List<Vector3> lVerts = new List<Vector3>();
            List<int> lTris = new List<int>();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] lBase =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            foreach (Vector3 lV in lBase) lVerts.Add(lV.normalized * 0.5f);
            int[] lFaces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            lTris.AddRange(lFaces);

            for (int s = 0; s < pSubdivisions; s++)
            {
                Dictionary<long, int> lMid = new Dictionary<long, int>();
                List<int> lNext = new List<int>();
                for (int i = 0; i < lTris.Count; i += 3)
                {
                    int a = lTris[i], b = lTris[i + 1], c = lTris[i + 2];
                    int ab = Midpoint(lVerts, lMid, a, b), bc = Midpoint(lVerts, lMid, b, c), ca = Midpoint(lVerts, lMid, c, a);
                    lNext.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                lTris = lNext;
            }

            if (pNoise > 0f)
            {
                System.Random lRandom = new System.Random(pSeed);
                for (int i = 0; i < lVerts.Count; i++)
                    lVerts[i] *= 1f + ((float)lRandom.NextDouble() * 2f - 1f) * pNoise;
            }

            return Flat(lVerts, lTris, pColor ?? Color.white);
        }

        private static int Midpoint(List<Vector3> pVerts, Dictionary<long, int> pCache, int pA, int pB)
        {
            long lKey = pA < pB ? ((long)pA << 32) + pB : ((long)pB << 32) + pA;
            if (pCache.TryGetValue(lKey, out int lIndex)) return lIndex;
            pVerts.Add(((pVerts[pA] + pVerts[pB]) * 0.5f).normalized * 0.5f);
            pCache[lKey] = pVerts.Count - 1;
            return pVerts.Count - 1;
        }

        /// <summary>A prism / cylinder with <paramref name="pSides"/> sides, radius 0.5, height 1, centred.</summary>
        public static Mesh Cylinder(int pSides, float pTopScale = 1f, Color? pColor = null)
        {
            List<Vector3> lVerts = new List<Vector3>();
            List<int> lTris = new List<int>();
            for (int i = 0; i < pSides; i++)
            {
                float a0 = i * Mathf.PI * 2f / pSides, a1 = (i + 1) * Mathf.PI * 2f / pSides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                Vector3 t0 = new Vector3(b0.x * pTopScale, 0.5f, b0.z * pTopScale);
                Vector3 t1 = new Vector3(b1.x * pTopScale, 0.5f, b1.z * pTopScale);
                AddQuad(lVerts, lTris, b0, t0, t1, b1);
                AddTri(lVerts, lTris, new Vector3(0, 0.5f, 0), t1, t0);
                AddTri(lVerts, lTris, new Vector3(0, -0.5f, 0), b0, b1);
            }
            return Build(lVerts, lTris, pColor ?? Color.white);
        }

        /// <summary>A cone (radius 0.5 at the base, height 1, base at y=0).</summary>
        public static Mesh Cone(int pSides, Color? pColor = null)
        {
            List<Vector3> lVerts = new List<Vector3>();
            List<int> lTris = new List<int>();
            for (int i = 0; i < pSides; i++)
            {
                float a0 = i * Mathf.PI * 2f / pSides, a1 = (i + 1) * Mathf.PI * 2f / pSides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                AddTri(lVerts, lTris, b0, new Vector3(0, 1, 0), b1);
                AddTri(lVerts, lTris, Vector3.zero, b0, b1);
            }
            return Build(lVerts, lTris, pColor ?? Color.white);
        }

        /// <summary>A unit box with flat faces (centred).</summary>
        public static Mesh Box(Color? pColor = null)
        {
            List<Vector3> lVerts = new List<Vector3>();
            List<int> lTris = new List<int>();
            Vector3[] c =
            {
                new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, -.5f, -.5f), new Vector3(.5f, .5f, -.5f), new Vector3(-.5f, .5f, -.5f),
                new Vector3(-.5f, -.5f, .5f), new Vector3(.5f, -.5f, .5f), new Vector3(.5f, .5f, .5f), new Vector3(-.5f, .5f, .5f)
            };
            AddQuad(lVerts, lTris, c[0], c[3], c[2], c[1]);
            AddQuad(lVerts, lTris, c[5], c[6], c[7], c[4]);
            AddQuad(lVerts, lTris, c[4], c[7], c[3], c[0]);
            AddQuad(lVerts, lTris, c[1], c[2], c[6], c[5]);
            AddQuad(lVerts, lTris, c[3], c[7], c[6], c[2]);
            AddQuad(lVerts, lTris, c[4], c[0], c[1], c[5]);
            return Build(lVerts, lTris, pColor ?? Color.white);
        }

        /// <summary>A rounded blob: an icosphere stretched into a capsule-ish body.</summary>
        public static Mesh Blob(float pStretch, int pSeed = 0)
        {
            Mesh lMesh = Icosphere(1, 0.04f, pSeed);
            Vector3[] lVerts = lMesh.vertices;
            for (int i = 0; i < lVerts.Length; i++)
            {
                Vector3 v = lVerts[i];
                float lBelly = 1f + 0.12f * Mathf.Clamp01(-v.y * 2f);
                lVerts[i] = new Vector3(v.x * lBelly, v.y * pStretch, v.z * lBelly);
            }
            lMesh.vertices = lVerts;
            lMesh.RecalculateNormals();
            lMesh.RecalculateBounds();
            return lMesh;
        }

        // ---------------------------------------------------------------- helpers

        public static void AddTri(List<Vector3> pVerts, List<int> pTris, Vector3 pA, Vector3 pB, Vector3 pC)
        {
            int lStart = pVerts.Count;
            pVerts.Add(pA);
            pVerts.Add(pB);
            pVerts.Add(pC);
            pTris.Add(lStart);
            pTris.Add(lStart + 1);
            pTris.Add(lStart + 2);
        }

        public static void AddQuad(List<Vector3> pVerts, List<int> pTris, Vector3 pA, Vector3 pB, Vector3 pC, Vector3 pD)
        {
            AddTri(pVerts, pTris, pA, pB, pC);
            AddTri(pVerts, pTris, pA, pC, pD);
        }

        private static Mesh Flat(List<Vector3> pShared, List<int> pTris, Color pColor)
        {
            List<Vector3> lVerts = new List<Vector3>(pTris.Count);
            List<int> lTris = new List<int>(pTris.Count);
            for (int i = 0; i < pTris.Count; i++)
            {
                lVerts.Add(pShared[pTris[i]]);
                lTris.Add(i);
            }
            return Build(lVerts, lTris, pColor);
        }

        public static Mesh Build(List<Vector3> pVerts, List<int> pTris, Color pColor)
        {
            Mesh lMesh = new Mesh();
            if (pVerts.Count > 65000) lMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            lMesh.SetVertices(pVerts);
            lMesh.SetTriangles(pTris, 0);
            Color[] lColors = new Color[pVerts.Count];
            for (int i = 0; i < lColors.Length; i++) lColors[i] = pColor;
            lMesh.colors = lColors;
            lMesh.RecalculateNormals();
            lMesh.RecalculateBounds();
            return lMesh;
        }

        /// <summary>Merges meshes with transforms and vertex colours into one (static props, trees).</summary>
        public static Mesh Combine(IList<(Mesh mesh, Matrix4x4 matrix, Color color)> pParts)
        {
            List<Vector3> lVerts = new List<Vector3>();
            List<Vector3> lNormals = new List<Vector3>();
            List<Color> lColors = new List<Color>();
            List<int> lTris = new List<int>();
            foreach ((Mesh lMesh, Matrix4x4 lMatrix, Color lColor) in pParts)
            {
                int lOffset = lVerts.Count;
                Vector3[] lV = lMesh.vertices;
                Vector3[] lN = lMesh.normals;
                Color[] lC = lMesh.colors;
                for (int i = 0; i < lV.Length; i++)
                {
                    lVerts.Add(lMatrix.MultiplyPoint3x4(lV[i]));
                    lNormals.Add(lMatrix.MultiplyVector(lN[i]).normalized);
                    lColors.Add(lC.Length == lV.Length ? lC[i] * lColor : lColor);
                }
                foreach (int lT in lMesh.triangles) lTris.Add(lT + lOffset);
            }
            Mesh lResult = new Mesh();
            if (lVerts.Count > 65000) lResult.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            lResult.SetVertices(lVerts);
            lResult.SetNormals(lNormals);
            lResult.SetColors(lColors);
            lResult.SetTriangles(lTris, 0);
            lResult.RecalculateBounds();
            return lResult;
        }
    }
}
