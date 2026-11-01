using System.Collections.Generic;
using System.IO;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Bakes the stylized plans the minimap and the map screen show: one flat image per floor
    /// (ground, then a storey up each time), read from the built scene.
    /// <list type="bullet">
    /// <item>Ground floor: open ground dark, tree crowns darker, water blue-grey, paths mid grey,
    /// streets and plazas light grey.</item>
    /// <item>Every floor: walls and obstacles cut at chest height as dark lines, walkable floors of
    /// buildings, decks and roofs in a lighter grey; above the ground, the outdoors fades out.</item>
    /// </list>
    /// </summary>
    public static class MapPlanBaker
    {
        public const int RES = 1024, FLOORS = 4;
        private const float CUT = 1.3f;

        private static readonly Color32 VOID = new Color32(16, 18, 21, 255);
        private static readonly Color32 GROUND = new Color32(38, 42, 46, 255);
        private static readonly Color32 TREE = new Color32(28, 32, 35, 255);
        private static readonly Color32 WATER = new Color32(34, 58, 84, 255);
        private static readonly Color32 PATH = new Color32(78, 82, 86, 255);
        private static readonly Color32 ROAD = new Color32(112, 116, 121, 255);
        private static readonly Color32 FLOOR = new Color32(150, 155, 162, 255);
        private static readonly Color32 WALL = new Color32(232, 234, 238, 255);
        private static readonly Color32 EDGE = new Color32(200, 204, 210, 255);

        public static void Bake(MapDefinition pMap, Terrain pLand, float pStorey)
        {
            if (pMap == null || pLand == null) return;
            float lStart = (float)EditorApplication.timeSinceStartup;
            TerrainData lData = pLand.terrainData;
            Vector3 lOrigin = pLand.transform.position;
            float lSize = lData.size.x, lCell = lSize / RES;
            Physics.SyncTransforms();

            // Ground classes from the painted layers: streets and plazas, then dirt paths.
            float[,,] lAlpha = lData.GetAlphamaps(0, 0, lData.alphamapWidth, lData.alphamapHeight);
            List<int> lRoads = new List<int>(), lPaths = new List<int>();
            for (int l = 0; l < lData.terrainLayers.Length; l++)
            {
                // Layers follow the Ground order (realistic maps use PBR sets under other names).
                string lName = lData.terrainLayers[l] != null ? lData.terrainLayers[l].name.ToLowerInvariant() : "";
                if (l == (int)Ground.Asphalt || l == (int)Ground.Cobble || lName.Contains("asphalt") || lName.Contains("cobble")) lRoads.Add(l);
                else if (l == (int)Ground.Dirt) lPaths.Add(l);
            }

            // Tree crowns.
            bool[] lTrees = new bool[RES * RES];
            foreach (TreeInstance lTree in lData.treeInstances)
            {
                GameObject lPrefab = lData.treePrototypes[lTree.prototypeIndex].prefab;
                string lName = lPrefab != null ? lPrefab.name : "";
                if (lName.StartsWith("Rock") || lName.StartsWith("Grass") || lName.StartsWith("Flower") || lName.Contains("_Rock") || lName.Contains("Grass") || lName.Contains("Log") || lName.Contains("Fern")) continue;
                bool lBush = lName.StartsWith("Bush") || lName.Contains("Bush") || lName.Contains("Banana") || lName.Contains("Shrub");
                float lRadius = (lBush ? 1.1f : lName.Contains("Ceiba") ? 9f : lName.Contains("JungleTree") || lName.Contains("Acacia") ? 4.5f : 2.4f) * lTree.widthScale;
                Disc(lTrees, lTree.position.x * RES, lTree.position.z * RES, lRadius / lCell);
            }

            // Water surfaces (planes without colliders): inside the local bounds of any water mesh.
            List<(Transform t, Bounds b)> lWaters = new List<(Transform, Bounds)>();
            foreach (MeshFilter lFilter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (lFilter.gameObject.layer == GameLayers.WATER && lFilter.sharedMesh != null) lWaters.Add((lFilter.transform, lFilter.sharedMesh.bounds));

            int lMask = GameLayers.WorldMask;
            Color32[][] lFloors = new Color32[FLOORS][];
            for (int f = 0; f < FLOORS; f++) lFloors[f] = new Color32[RES * RES];
            bool[] lBuilt = new bool[RES * RES];
            Collider[] lOverlap = new Collider[8];
            RaycastHit[] lHits = new RaycastHit[8];

            for (int z = 0; z < RES; z++)
            {
                if (z % 64 == 0) EditorUtility.DisplayProgressBar("Map plan", pMap.displayName, (float)z / RES);
                for (int x = 0; x < RES; x++)
                {
                    float lX = lOrigin.x + (x + 0.5f) * lCell, lZ = lOrigin.z + (z + 0.5f) * lCell;
                    float lGround = pLand.SampleHeight(new Vector3(lX, 0, lZ)) + lOrigin.y;
                    int i = z * RES + x;

                    // What stands on the ground here, if anything (a footprint to look at on upper floors).
                    lBuilt[i] = Physics.Raycast(new Vector3(lX, lGround + 80f, lZ), Vector3.down, out RaycastHit lTop, 82f, lMask, QueryTriggerInteraction.Ignore)
                                && !(lTop.collider is TerrainCollider) && lTop.point.y > lGround + 0.4f;

                    Color32 lOutdoor = GroundColor(lAlpha, lData, x, z, lRoads, lPaths, lTrees[i], InWater(lWaters, new Vector3(lX, lGround, lZ)));
                    for (int f = 0; f < FLOORS; f++)
                    {
                        if (f > 0 && !lBuilt[i]) { lFloors[f][i] = Dim(lOutdoor); continue; }
                        float lCut = lGround + f * pStorey + CUT;
                        Vector3 lAt = new Vector3(lX, lCut, lZ);
                        bool lWall = false;
                        int lCount = Physics.OverlapBoxNonAlloc(lAt, new Vector3(lCell * 0.5f, 0.35f, lCell * 0.5f), lOverlap, Quaternion.identity, lMask, QueryTriggerInteraction.Ignore);
                        for (int c = 0; c < lCount; c++) if (!(lOverlap[c] is TerrainCollider)) { lWall = true; break; }
                        if (lWall) { lFloors[f][i] = WALL; continue; }
                        bool lDeck = false;
                        int lHitCount = Physics.RaycastNonAlloc(lAt, Vector3.down, lHits, CUT + 1.2f, lMask, QueryTriggerInteraction.Ignore);
                        for (int h = 0; h < lHitCount; h++) if (!(lHits[h].collider is TerrainCollider) && lHits[h].normal.y > 0.6f) { lDeck = true; break; }
                        lFloors[f][i] = lDeck ? FLOOR : f == 0 ? lOutdoor : Dim(lOutdoor);
                    }
                }
            }
            EditorUtility.ClearProgressBar();

            // A soft dark edge around walkable built floors, so rooms read as shapes.
            for (int f = 0; f < FLOORS; f++) Outline(lFloors[f]);

            string lFolder = EditorAssets.ROOT + "/Art/Generated/Plans";
            EditorAssets.EnsureFolder(lFolder);
            pMap.planFloors = new Texture2D[FLOORS];
            for (int f = 0; f < FLOORS; f++)
            {
                Texture2D lTexture = new Texture2D(RES, RES, TextureFormat.RGBA32, false);
                lTexture.SetPixels32(lFloors[f]);
                lTexture.Apply();
                string lPath = $"{lFolder}/Plan_{pMap.sceneName}_F{f}.png";
                File.WriteAllBytes(lPath, lTexture.EncodeToPNG());
                Object.DestroyImmediate(lTexture);
                AssetDatabase.ImportAsset(lPath, ImportAssetOptions.ForceUpdate);
                TextureImporter lImporter = (TextureImporter)AssetImporter.GetAtPath(lPath);
                lImporter.textureType = TextureImporterType.Default;
                lImporter.sRGBTexture = true;
                lImporter.mipmapEnabled = true;
                lImporter.wrapMode = TextureWrapMode.Clamp;
                lImporter.filterMode = FilterMode.Bilinear;
                lImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                lImporter.SaveAndReimport();
                pMap.planFloors[f] = AssetDatabase.LoadAssetAtPath<Texture2D>(lPath);
            }
            pMap.planOrigin = new Vector2(lOrigin.x, lOrigin.z);
            pMap.planSize = lSize;
            pMap.planStorey = pStorey;
            EditorUtility.SetDirty(pMap);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] {pMap.displayName} plan: {FLOORS} floors in {(float)EditorApplication.timeSinceStartup - lStart:0} s");
        }

        private static Color32 GroundColor(float[,,] pAlpha, TerrainData pData, int pX, int pZ, List<int> pRoads, List<int> pPaths, bool pTree, bool pWater)
        {
            if (pWater) return WATER;
            int lAx = Mathf.Min(pData.alphamapWidth - 1, pX * pData.alphamapWidth / RES), lAz = Mathf.Min(pData.alphamapHeight - 1, pZ * pData.alphamapHeight / RES);
            float lRoad = 0f, lPath = 0f;
            foreach (int l in pRoads) lRoad += pAlpha[lAz, lAx, l];
            foreach (int l in pPaths) lPath += pAlpha[lAz, lAx, l];
            if (lRoad > 0.45f) return ROAD;
            if (pTree) return TREE;
            if (lPath > 0.5f) return PATH;
            return GROUND;
        }

        private static bool InWater(List<(Transform t, Bounds b)> pWaters, Vector3 pGround)
        {
            foreach ((Transform t, Bounds b) in pWaters)
            {
                Vector3 lLocal = t.InverseTransformPoint(pGround);
                Vector3 lSurface = t.TransformPoint(new Vector3(lLocal.x, b.center.y, lLocal.z));
                if (lLocal.x >= b.min.x && lLocal.x <= b.max.x && lLocal.z >= b.min.z && lLocal.z <= b.max.z && lSurface.y > pGround.y + 0.05f) return true;
            }
            return false;
        }

        private static Color32 Dim(Color32 pColor) => new Color32((byte)(pColor.r * 0.5f + VOID.r * 0.5f), (byte)(pColor.g * 0.5f + VOID.g * 0.5f), (byte)(pColor.b * 0.5f + VOID.b * 0.5f), 255);

        private static void Disc(bool[] pMask, float pCx, float pCz, float pRadius)
        {
            int lR = Mathf.CeilToInt(pRadius);
            for (int z = (int)pCz - lR; z <= (int)pCz + lR; z++)
                for (int x = (int)pCx - lR; x <= (int)pCx + lR; x++)
                    if (x >= 0 && z >= 0 && x < RES && z < RES && (x - pCx) * (x - pCx) + (z - pCz) * (z - pCz) <= pRadius * pRadius) pMask[z * RES + x] = true;
        }

        private static void Outline(Color32[] pPixels)
        {
            Color32[] lSource = (Color32[])pPixels.Clone();
            for (int z = 1; z < RES - 1; z++)
                for (int x = 1; x < RES - 1; x++)
                {
                    int i = z * RES + x;
                    if (!Same(lSource[i], FLOOR)) continue;
                    bool lBorder = !Same(lSource[i - 1], FLOOR) && !Same(lSource[i - 1], WALL)
                                   || !Same(lSource[i + 1], FLOOR) && !Same(lSource[i + 1], WALL)
                                   || !Same(lSource[i - RES], FLOOR) && !Same(lSource[i - RES], WALL)
                                   || !Same(lSource[i + RES], FLOOR) && !Same(lSource[i + RES], WALL);
                    if (lBorder) pPixels[i] = EDGE;
                }
        }

        private static bool Same(Color32 pA, Color32 pB) => pA.r == pB.r && pA.g == pB.g && pA.b == pB.b;
    }
}
