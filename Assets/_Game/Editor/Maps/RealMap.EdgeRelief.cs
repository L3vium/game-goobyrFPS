using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The rock walls that close the box are not one even rampart: along each walled side their height
    /// over the land inside swings by about a third, saddles notch them down so the hills and peaks
    /// beyond show through, and they climb in two or three benches with a talus apron at the foot.
    /// Applied to the map's <see cref="Relief"/> on the sides a map names in <see cref="RockEdges"/>.
    /// </summary>
    public abstract partial class RealMap
    {
        /// <summary>The sides closed by rock walls (north = +z, east = +x, south = -z, west = -x).</summary>
        protected virtual (bool north, bool east, bool south, bool west) RockEdges => (false, false, false, false);

        /// <summary>Height of one bench of an edge wall (m).</summary>
        protected virtual float EdgeBench => 14f;

        /// <summary>How far in from the box edge the wall's foot is measured (m).</summary>
        private const float EDGE_REF = HALF - 80f;

        private float EdgeRelief(float x, float z)
        {
            float h = Relief(x, z);
            (bool lNorth, bool lEast, bool lSouth, bool lWest) = RockEdges;
            float lBest = 0f, lBase = 0f, lAlong = 0f;
            int lSide = -1;
            void Try(bool pOn, int pSide, float pDepth, float pRefX, float pRefZ, float pAlongCoord)
            {
                if (!pOn || pDepth < EDGE_REF) return;
                float lRef = Relief(pRefX, pRefZ), e = h - lRef;
                if (e <= lBest) return;
                lBest = e; lBase = lRef; lAlong = pAlongCoord; lSide = pSide;
            }
            Try(lNorth, 0, z, x, EDGE_REF, x);
            Try(lEast, 1, x, EDGE_REF, z, z);
            Try(lSouth, 2, -z, x, -EDGE_REF, x);
            Try(lWest, 3, -x, -EDGE_REF, z, z);
            if (lSide < 0) return h;
            return lBase + EdgeShape(lBest, lAlong, lSide);
        }

        /// <summary>Reshapes a wall's height over its foot: swing along the side, saddles, benches and talus.</summary>
        private float EdgeShape(float e, float pAlong, int pSide)
        {
            float lSwing = 1f + 0.35f * Mathf.Clamp(LandShape.Fbm(pAlong, pSide * 137f, 70f, 2, 5.1f + pSide) * 2.2f, -1f, 1f);
            float lWave = Mathf.Sin(pAlong * (2f * Mathf.PI / (135f + pSide * 12f)) + pSide * 1.9f);
            float lSaddle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.97f, lWave));
            float s = e * lSwing * (1f - 0.55f * lSaddle);
            // Benches: a flat tread for most of each step, then a steep riser; the lowest tread leans out as talus.
            float lStep = EdgeBench, u = s / lStep, k = Mathf.Floor(u), lRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, u - k));
            float lBenched = Mathf.Max((k + lRise) * lStep, s * 0.45f);
            return Mathf.Lerp(s, lBenched, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 12f, s)));
        }
    }
}
