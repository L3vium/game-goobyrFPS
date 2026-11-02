using Com.IsartDigital.GoobyrFPS.Core;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Terrain-ready vegetation from the <c>Veg_*</c> models: tree prefabs (the model with its three
    /// LODs, a trunk capsule for trees and palms, a box for rocks and logs, nothing for bushes) and
    /// instanced detail meshes (grass, ferns) baked upright for the terrain's detail layers.
    /// <para>
    /// Every prefab casts sun shadows from its full and middle LODs (bushes, rocks and boulders
    /// included; the impostor card of small plants does not), cross-fades its LODs with a dither,
    /// and its plant materials use <c>Goobyr/Foliage</c> (wind, translucency). Existing prefabs are
    /// tuned in place, so the terrains that use them update without a map rebuild.
    /// </para>
    /// </summary>
    public static class VegLibrary
    {
        private const string FOLDER = EditorAssets.ROOT + "/Art/Generated/Veg";

        /// <summary>Re-tunes every vegetation prefab (shadows, LOD cross-fade, foliage materials).</summary>
        [MenuItem("Goobyr/Build/Vegetation")]
        public static void TuneAll()
        {
            int lCount = 0;
            foreach (string lGuid in AssetDatabase.FindAssets("t:Prefab", new[] { FOLDER }))
            {
                string lPath = AssetDatabase.GUIDToAssetPath(lGuid);
                string lName = System.IO.Path.GetFileNameWithoutExtension(lPath);
                if (!lName.StartsWith("Veg_")) continue;
                Tune(lPath, lName);
                lCount++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] vegetation: {lCount} prefabs tuned");
        }

        private static readonly System.Collections.Generic.HashSet<string> _tuned = new System.Collections.Generic.HashSet<string>();

        /// <summary>Applies the shadow, cross-fade and foliage settings to a saved vegetation prefab (keeps its GUID).</summary>
        private static void Tune(string pPath, string pName)
        {
            _tuned.Add(pPath);
            GameObject lRoot = PrefabUtility.LoadPrefabContents(pPath);
            try
            {
                Configure(lRoot, pName);
                PrefabUtility.SaveAsPrefabAsset(lRoot, pPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(lRoot); }
        }

        /// <summary>Shadows on every full and middle LOD, a dithered cross-fade between LODs, the foliage shader on plants.</summary>
        private static void Configure(GameObject pRoot, string pName)
        {
            LODGroup lGroup = pRoot.GetComponent<LODGroup>();
            Renderer[] lLast = null;
            if (lGroup != null)
            {
                lGroup.fadeMode = LODFadeMode.CrossFade;
                lGroup.animateCrossFading = true;
                LOD[] lLods = lGroup.GetLODs();
                if (lLods.Length > 1) lLast = lLods[lLods.Length - 1].renderers;
            }
            bool lTree = IsTree(pName);
            float lBend = PbrLibrary.PlantBend(ArchLib.ModelBounds(pName).size.y);
            foreach (Renderer lRenderer in pRoot.GetComponentsInChildren<Renderer>())
            {
                bool lCard = lLast != null && System.Array.IndexOf(lLast, lRenderer) >= 0;
                lRenderer.shadowCastingMode = lCard && !lTree ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                lRenderer.receiveShadows = true;
                if (!PbrLibrary.IsPlant(pName)) continue;
                foreach (Material lMaterial in lRenderer.sharedMaterials) PbrLibrary.Foliage(lMaterial, lBend);
            }
        }

        private static float TrunkRadius(string pName)
        {
            if (pName.Contains("Ceiba")) return 1.4f;
            if (pName.Contains("JungleTree")) return 0.55f;
            if (pName.Contains("Palm") || pName.Contains("Fern")) return 0.25f;
            if (pName.Contains("Fir_Large") || pName.Contains("Snowy_Large")) return 0.45f;
            if (pName.Contains("Acacia") || pName.Contains("Dead") || pName.Contains("Birch")) return 0.3f;
            return 0.35f;
        }

        private static bool IsTree(string pName) => pName.Contains("Tree") || pName.Contains("Ceiba") || pName.Contains("Palm") || pName.Contains("Fir") || pName.Contains("Birch") || pName.Contains("Acacia") || pName.Contains("DeadTree");
        private static bool IsSolid(string pName) => pName.Contains("Rock") || pName.Contains("Log");

        /// <summary>The prefab a terrain tree instance uses, built once (null if the model is missing).</summary>
        public static GameObject TreePrefab(string pName)
        {
            string lPath = FOLDER + "/" + pName + ".prefab";
            GameObject lExisting = AssetDatabase.LoadAssetAtPath<GameObject>(lPath);
            if (lExisting != null)
            {
                if (!_tuned.Contains(lPath)) { Tune(lPath, pName); lExisting = AssetDatabase.LoadAssetAtPath<GameObject>(lPath); }
                return lExisting;
            }
            if (!ArchLib.Has(pName)) { Debug.LogWarning($"[Veg] missing {pName}"); return null; }
            EditorAssets.EnsureFolder(FOLDER);
            GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{PbrLibrary.ROOT}/Models/{pName}/{pName}.fbx");
            GameObject lInstance = (GameObject)PrefabUtility.InstantiatePrefab(lModel);
            PrefabUtility.UnpackPrefabInstance(lInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            GameObject lRoot = new GameObject(pName);
            lInstance.transform.SetParent(lRoot.transform, false);
            Bounds lBounds = ArchLib.ModelBounds(pName);
            if (IsTree(pName))
            {
                CapsuleCollider lTrunk = lRoot.AddComponent<CapsuleCollider>();
                lTrunk.radius = TrunkRadius(pName);
                lTrunk.height = Mathf.Min(8f, lBounds.size.y);
                lTrunk.center = new Vector3(0, lTrunk.height * 0.5f, 0);
            }
            else if (IsSolid(pName))
            {
                BoxCollider lBox = lRoot.AddComponent<BoxCollider>();
                lBox.center = lBounds.center;
                lBox.size = lBounds.size * 0.85f;
            }
            // Terrain trees need the LOD group on the prefab root.
            LODGroup lSource = lInstance.GetComponent<LODGroup>();
            if (lSource != null)
            {
                LOD[] lLods = lSource.GetLODs();
                Object.DestroyImmediate(lSource, true);
                bool lSmall = !IsTree(pName);
                float[] lHeights = lSmall ? new[] { 0.12f, 0.04f, 0.012f } : new[] { 0.25f, 0.06f, 0.008f };
                for (int i = 0; i < lLods.Length && i < 3; i++) lLods[i].screenRelativeTransitionHeight = lHeights[i];
                LODGroup lGroup = lRoot.AddComponent<LODGroup>();
                lGroup.SetLODs(lLods);
                lGroup.RecalculateBounds();
            }
            Configure(lRoot, pName);
            foreach (Transform t in lRoot.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.DEFAULT; t.gameObject.isStatic = true; }
            GameObject lPrefab = PrefabUtility.SaveAsPrefabAsset(lRoot, lPath);
            Object.DestroyImmediate(lRoot);
            _tuned.Add(lPath);
            return lPrefab;
        }

        /// <summary>A detail prefab: the LOD0 mesh baked upright (y up), with the model's material.</summary>
        public static GameObject DetailPrefab(string pName)
        {
            string lPath = FOLDER + "/Detail_" + pName + ".prefab";
            GameObject lExisting = AssetDatabase.LoadAssetAtPath<GameObject>(lPath);
            if (lExisting != null) return lExisting;
            if (!ArchLib.Has(pName)) { Debug.LogWarning($"[Veg] missing {pName}"); return null; }
            EditorAssets.EnsureFolder(FOLDER);
            GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{PbrLibrary.ROOT}/Models/{pName}/{pName}.fbx");
            MeshFilter lSource = null;
            foreach (MeshFilter lFilter in lModel.GetComponentsInChildren<MeshFilter>()) if (lFilter.name.EndsWith("_LOD0") || lSource == null) lSource = lFilter;
            if (lSource == null) return null;
            Matrix4x4 m = lModel.transform.worldToLocalMatrix * lSource.transform.localToWorldMatrix;
            Mesh lMesh = Object.Instantiate(lSource.sharedMesh);
            Vector3[] v = lMesh.vertices, n = lMesh.normals;
            for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
            for (int i = 0; i < n.Length; i++) n[i] = m.MultiplyVector(n[i]).normalized;
            lMesh.vertices = v;
            lMesh.normals = n;
            lMesh.RecalculateBounds();
            lMesh.RecalculateTangents();
            string lMeshPath = FOLDER + "/Detail_" + pName + "_mesh.asset";
            AssetDatabase.DeleteAsset(lMeshPath);
            AssetDatabase.CreateAsset(lMesh, lMeshPath);
            GameObject lRoot = new GameObject("Detail_" + pName);
            lRoot.AddComponent<MeshFilter>().sharedMesh = lMesh;
            MeshRenderer lRenderer = lRoot.AddComponent<MeshRenderer>();
            lRenderer.sharedMaterials = lSource.GetComponent<MeshRenderer>().sharedMaterials;
            foreach (Material lMaterial in lRenderer.sharedMaterials) if (lMaterial != null && !lMaterial.enableInstancing) { lMaterial.enableInstancing = true; EditorUtility.SetDirty(lMaterial); }
            GameObject lPrefab = PrefabUtility.SaveAsPrefabAsset(lRoot, lPath);
            Object.DestroyImmediate(lRoot);
            return lPrefab;
        }

        /// <summary>An instanced mesh detail prototype for the terrain.</summary>
        public static DetailPrototype Detail(string pName, float pMinScale, float pMaxScale, Color pHealthy, Color pDry)
        {
            GameObject lPrefab = DetailPrefab(pName);
            if (lPrefab == null) return null;
            return new DetailPrototype
            {
                prototype = lPrefab,
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = pMinScale, maxWidth = pMaxScale, minHeight = pMinScale, maxHeight = pMaxScale,
                healthyColor = pHealthy, dryColor = pDry,
                noiseSpread = 0.4f,
                alignToGround = 0.6f,
                useDensityScaling = true
            };
        }
    }
}
