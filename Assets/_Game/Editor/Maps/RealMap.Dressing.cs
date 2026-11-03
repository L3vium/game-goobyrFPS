using System;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Open-ground dressing shared by the realistic maps: scattering on free flat land, hedged lines, and
    /// the small set pieces that break up empty plazas, lawns, courts and snow aprons (benches, trees in
    /// planters, bandstands, tents, snowbanks, sledges, ski racks, a snowcat, luggage).
    /// </summary>
    public abstract partial class RealMap
    {
        /// <summary>
        /// Places up to <paramref name="pCount"/> things on free flat land in an area: a random spot on the
        /// terrain (not a roof, not a slope) with nothing within <paramref name="pClear"/> m, handed to
        /// <paramref name="pPlace"/> with a yaw. Returns how many were placed.
        /// </summary>
        protected int Scatter(Rect pArea, int pCount, float pClear, int pSeed, Action<Vector3, float> pPlace)
        {
            System.Random r = new System.Random(pSeed);
            Physics.SyncTransforms();
            int n = 0;
            for (int i = 0; i < pCount * 16 && n < pCount; i++)
            {
                float x = Mathf.Lerp(pArea.xMin, pArea.xMax, (float)r.NextDouble()), z = Mathf.Lerp(pArea.yMin, pArea.yMax, (float)r.NextDouble());
                if (!FreeGround(x, z, pClear, out Vector3 g)) continue;
                pPlace(g, (float)r.NextDouble() * 360f);
                Physics.SyncTransforms();
                n++;
            }
            return n;
        }

        /// <summary>Things every <paramref name="pStep"/> m along a line, on free flat land only, facing along it.</summary>
        protected int Line(Vector2 pFrom, Vector2 pTo, float pStep, float pClear, Action<Vector3, float> pPlace)
        {
            Physics.SyncTransforms();
            float lLength = Vector2.Distance(pFrom, pTo), lYaw = Mathf.Atan2(pTo.x - pFrom.x, pTo.y - pFrom.y) * Mathf.Rad2Deg + 90f;
            int n = 0;
            for (float t = 0f; t <= lLength; t += pStep)
            {
                Vector2 p = Vector2.Lerp(pFrom, pTo, lLength > 0f ? t / lLength : 0f);
                if (!FreeGround(p.x, p.y, pClear, out Vector3 g)) continue;
                pPlace(g, lYaw);
                Physics.SyncTransforms();
                n++;
            }
            return n;
        }

        private static bool FreeGround(float x, float z, float pClear, out Vector3 pGround)
        {
            pGround = Vector3.zero;
            if (!Physics.Raycast(new Vector3(x, 400f, z), Vector3.down, out RaycastHit lHit, 800f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (lHit.normal.y < 0.94f || !(lHit.collider is TerrainCollider)) return false;
            if (Physics.CheckBox(lHit.point + Vector3.up * 1.2f, new Vector3(pClear, 1f, pClear), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return false;
            pGround = lHit.point;
            return true;
        }

        protected void Bench(Vector3 g, float pYaw)
        {
            ArchMat lWood = ArchLib.Surface("rough_wood", 1f);
            A.BoxObject("Bench", g, pYaw, K.Props, 0, true,
                (lWood, new Vector3(0, 0.45f, 0), new Vector3(1.7f, 0.07f, 0.45f)),
                (lWood, new Vector3(0, 0.75f, -0.2f), new Vector3(1.7f, 0.4f, 0.05f)),
                (ArchLib.Iron, new Vector3(-0.75f, 0.22f, 0), new Vector3(0.06f, 0.44f, 0.42f)),
                (ArchLib.Iron, new Vector3(0.75f, 0.22f, 0), new Vector3(0.06f, 0.44f, 0.42f)));
        }

        /// <summary>A stone planter with a young tree (or a palm) growing out of it.</summary>
        protected void PlanterTree(Vector3 g, float pYaw, string pTree, float pScale)
        {
            A.BoxObject("TreePlanter", g, pYaw, K.Props, 0, true,
                (ArchLib.Surface("large_sandstone_blocks", 1.5f), new Vector3(0, 0.3f, 0), new Vector3(1.6f, 0.6f, 1.6f)),
                (ArchLib.Surface("forest_ground_04", 1f), new Vector3(0, 0.61f, 0), new Vector3(1.4f, 0.02f, 1.4f)));
            GameObject lPrefab = VegLibrary.TreePrefab(pTree);
            if (lPrefab == null) return;
            GameObject lTree = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(lPrefab, K.Props);
            lTree.transform.SetPositionAndRotation(g + Vector3.up * 0.6f, Quaternion.Euler(0f, pYaw * 1.7f, 0f));
            lTree.transform.localScale = Vector3.one * pScale;
        }

        /// <summary>An open-sided shade on posts (a bandstand, a market tent, a souk awning).</summary>
        protected void Shade(Vector3 g, float pYaw, float pWidth, float pDepth, float pHeight, ArchMat pRoof, ArchMat pPosts) =>
            ArchOutdoor.Shed(A, g, pYaw, pWidth, pDepth, pHeight, pRoof, pPosts, false);

        protected void Snowbank(Vector3 g, float pYaw)
        {
            System.Random r = new System.Random((int)(g.x * 13f + g.z * 7f));
            float l = 2.5f + (float)r.NextDouble() * 3f;
            A.BoxObject("Snowbank", g, pYaw, K.Props, 0, true,
                (ArchLib.Snow, new Vector3(0, 0.3f, 0), new Vector3(l, 0.7f, 1.6f)),
                (ArchLib.Snow, new Vector3(l * 0.15f, 0.65f, 0.1f), new Vector3(l * 0.6f, 0.5f, 1.1f)),
                (ArchLib.Snow, new Vector3(-l * 0.25f, 0.55f, -0.15f), new Vector3(l * 0.4f, 0.45f, 0.9f)));
        }

        protected void Sledge(Vector3 g, float pYaw)
        {
            ArchMat lWood = ArchLib.Surface("old_planks_02", 1f);
            A.BoxObject("Sledge", g, pYaw, K.Props, 0, true,
                (lWood, new Vector3(0, 0.32f, 0), new Vector3(0.45f, 0.05f, 1.1f)),
                (ArchLib.Iron, new Vector3(-0.2f, 0.12f, 0), new Vector3(0.03f, 0.04f, 1.2f)),
                (ArchLib.Iron, new Vector3(0.2f, 0.12f, 0), new Vector3(0.03f, 0.04f, 1.2f)),
                (lWood, new Vector3(-0.2f, 0.22f, 0), new Vector3(0.04f, 0.2f, 0.9f)),
                (lWood, new Vector3(0.2f, 0.22f, 0), new Vector3(0.04f, 0.2f, 0.9f)));
        }

        protected void SkiRack(Vector3 g, float pYaw)
        {
            ArchMat lWood = ArchLib.Surface("dark_wooden_planks", 1f);
            (ArchMat, Vector3, Vector3)[] lParts = new (ArchMat, Vector3, Vector3)[10];
            lParts[0] = (lWood, new Vector3(0, 1.1f, 0), new Vector3(2.6f, 0.08f, 0.12f));
            lParts[1] = (lWood, new Vector3(-1.25f, 0.55f, 0), new Vector3(0.08f, 1.1f, 0.3f));
            lParts[2] = (lWood, new Vector3(1.25f, 0.55f, 0), new Vector3(0.08f, 1.1f, 0.3f));
            Color[] lSki = { new Color(0.1f, 0.2f, 0.6f), new Color(0.85f, 0.85f, 0.8f), new Color(0.15f, 0.15f, 0.15f), new Color(0.7f, 0.5f, 0.1f) };
            for (int k = 0; k < 7; k++)
                lParts[3 + k] = (ArchLib.PlainMat("Ski_" + (k % 4), lSki[k % 4], 0.6f, 0f), new Vector3(-1.05f + k * 0.35f, 0.85f, 0.08f), new Vector3(0.08f, 1.7f, 0.02f));
            A.BoxObject("SkiRack", g, pYaw, K.Props, 0, true, lParts);
        }

        protected void Snowcat(Vector3 g, float pYaw)
        {
            ArchMat lBody = ArchLib.PlainMat("SnowcatBody", new Color(0.62f, 0.12f, 0.08f), 0.45f, 0.2f), lTrack = ArchLib.PlainMat("Track", new Color(0.08f, 0.08f, 0.08f), 0.2f, 0.3f);
            A.BoxObject("Snowcat", g, pYaw, K.Props, 0, true,
                (lTrack, new Vector3(-1.2f, 0.45f, 0), new Vector3(0.8f, 0.9f, 4.4f)),
                (lTrack, new Vector3(1.2f, 0.45f, 0), new Vector3(0.8f, 0.9f, 4.4f)),
                (lBody, new Vector3(0, 1.3f, -0.3f), new Vector3(2.2f, 0.9f, 3.6f)),
                (lBody, new Vector3(0, 2.2f, 0.5f), new Vector3(2f, 1f, 1.8f)),
                (ArchLib.Glass, new Vector3(0, 2.25f, 1.42f), new Vector3(1.8f, 0.7f, 0.04f)),
                (ArchLib.Galvanised, new Vector3(0, 0.8f, 2.6f), new Vector3(3.2f, 1.1f, 0.15f)));
        }

        protected void Luggage(Vector3 g, float pYaw)
        {
            ArchMat lLeather = ArchLib.PlainMat("Leather", new Color(0.35f, 0.2f, 0.12f), 0.35f, 0f), lCase = ArchLib.PlainMat("CaseBlue", new Color(0.15f, 0.2f, 0.32f), 0.4f, 0.1f);
            A.BoxObject("LuggageCart", g, pYaw, K.Props, 0, true,
                (ArchLib.Brass, new Vector3(0, 0.25f, 0), new Vector3(1.2f, 0.05f, 0.6f)),
                (ArchLib.Brass, new Vector3(-0.58f, 0.9f, 0), new Vector3(0.04f, 1.3f, 0.04f)),
                (ArchLib.Brass, new Vector3(0.58f, 0.9f, 0), new Vector3(0.04f, 1.3f, 0.04f)),
                (ArchLib.Brass, new Vector3(0, 1.55f, 0), new Vector3(1.2f, 0.05f, 0.05f)),
                (lLeather, new Vector3(-0.25f, 0.55f, 0), new Vector3(0.55f, 0.55f, 0.3f)),
                (lCase, new Vector3(0.28f, 0.6f, 0), new Vector3(0.45f, 0.65f, 0.28f)),
                (lLeather, new Vector3(0f, 1.0f, 0), new Vector3(0.7f, 0.3f, 0.35f)));
        }
    }
}
