using System.Collections.Generic;
using System.IO;
using Com.IsartDigital.GoobyrFPS.Core;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The realistic maps' asset access: PBR surfaces as <see cref="ArchMat"/> (tinted variants,
    /// plain fallbacks when a set is missing), glass and painted metals, and the finished models
    /// (<c>Furn_*</c>, <c>Prop_*</c>, <c>Veg_*</c>, <c>W_*</c> in <c>Art/PBR/Models</c>) placed as prefab
    /// instances with a box collider - or, while a model is not there yet, a textured box of its size
    /// from the props manifest, so a map always builds.
    /// </summary>
    public static class ArchLib
    {
        private const string FOLDER = PbrLibrary.MATERIALS + "/Arch";
        private static readonly Dictionary<string, ArchMat> _mats = new Dictionary<string, ArchMat>();
        private static readonly Dictionary<string, Bounds> _bounds = new Dictionary<string, Bounds>();
        private static Dictionary<string, Vector3> _manifest;

        /// <summary>Forget the cached materials (after a PBR rebuild).</summary>
        public static void Reset()
        {
            _mats.Clear();
            _bounds.Clear();
            _manifest = null;
        }

        // ---------------------------------------------------------------- materials

        /// <summary>A PBR surface set tiled every <paramref name="pTile"/> metres (a plain grey if the set is missing).</summary>
        public static ArchMat Surface(string pId, float pTile)
        {
            string lKey = pId + "@" + pTile.ToString("0.##");
            if (_mats.TryGetValue(lKey, out ArchMat lMat) && lMat.material != null) return lMat;
            Material lMaterial = PbrLibrary.HasSurface(pId) ? PbrLibrary.Surface(pId) : null;
            if (lMaterial == null) lMaterial = Plain("Fallback_" + pId, new Color(0.6f, 0.6f, 0.6f), 0.2f, 0f);
            return _mats[lKey] = new ArchMat(pId, lMaterial, pTile);
        }

        /// <summary>A tinted copy of a surface (painted plaster, blue-green shutters).</summary>
        public static ArchMat Tinted(string pId, Color pTint, string pSuffix, float pTile)
        {
            string lKey = pId + "_" + pSuffix + "@" + pTile.ToString("0.##");
            if (_mats.TryGetValue(lKey, out ArchMat lMat) && lMat.material != null) return lMat;
            Material lMaterial = PbrLibrary.HasSurface(pId) ? PbrLibrary.Tinted(pId, pTint, pSuffix) : null;
            if (lMaterial == null) lMaterial = Plain("Fallback_" + pId + "_" + pSuffix, pTint, 0.2f, 0f);
            return _mats[lKey] = new ArchMat(pId + "_" + pSuffix, lMaterial, pTile);
        }

        /// <summary>A flat URP Lit material (painted iron, rubber, emissive lamp glass), cached as an asset.</summary>
        public static Material Plain(string pName, Color pColor, float pSmoothness, float pMetallic, Color? pEmission = null)
        {
            EditorAssets.EnsureFolder(FOLDER);
            string lPath = FOLDER + "/M_" + pName + ".mat";
            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lPath);
            if (lMaterial == null)
            {
                lMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(lMaterial, lPath);
            }
            lMaterial.SetColor("_BaseColor", pColor);
            lMaterial.SetFloat("_Smoothness", pSmoothness);
            lMaterial.SetFloat("_Metallic", pMetallic);
            if (pEmission.HasValue)
            {
                lMaterial.EnableKeyword("_EMISSION");
                lMaterial.SetColor("_EmissionColor", pEmission.Value);
                lMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            lMaterial.enableInstancing = true;
            EditorUtility.SetDirty(lMaterial);
            return lMaterial;
        }

        public static ArchMat PlainMat(string pName, Color pColor, float pSmoothness, float pMetallic, float pTile = 1f, Color? pEmission = null)
        {
            string lKey = "plain_" + pName;
            if (_mats.TryGetValue(lKey, out ArchMat lMat) && lMat.material != null) return lMat;
            return _mats[lKey] = new ArchMat(pName, Plain(pName, pColor, pSmoothness, pMetallic, pEmission), pTile);
        }

        /// <summary>Window glass: transparent, glossy, slightly tinted.</summary>
        public static ArchMat Glass
        {
            get
            {
                if (_mats.TryGetValue("glass", out ArchMat lMat) && lMat.material != null) return lMat;
                EditorAssets.EnsureFolder(FOLDER);
                string lPath = FOLDER + "/M_WindowGlass.mat";
                Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(lPath);
                if (lMaterial == null)
                {
                    lMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(lMaterial, lPath);
                }
                lMaterial.SetFloat("_Surface", 1f);
                lMaterial.SetFloat("_Blend", 0f);
                lMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lMaterial.SetFloat("_ZWrite", 0f);
                lMaterial.SetFloat("_Cull", 0f);
                lMaterial.SetOverrideTag("RenderType", "Transparent");
                lMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                lMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                lMaterial.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.66f, 0.32f));
                lMaterial.SetFloat("_Smoothness", 0.96f);
                lMaterial.SetFloat("_Metallic", 0.1f);
                EditorUtility.SetDirty(lMaterial);
                return _mats["glass"] = new ArchMat("glass", lMaterial, 1f);
            }
        }

        /// <summary>Collision without a renderer.</summary>
        public static readonly ArchMat Invisible = new ArchMat("collider", null, 1f);

        public static ArchMat Iron => PlainMat("WroughtIron", new Color(0.07f, 0.07f, 0.075f), 0.35f, 0.85f);
        public static ArchMat Brass => PlainMat("Brass", new Color(0.75f, 0.58f, 0.3f), 0.7f, 1f);
        public static ArchMat Galvanised => Surface("metal_plate", 1.5f);
        public static ArchMat LampGlow => PlainMat("LampGlow", new Color(1f, 0.9f, 0.7f), 0.5f, 0f, 1f, new Color(4f, 3.2f, 2.2f));
        public static ArchMat Snow => Surface("snow_02", 3f);

        // ---------------------------------------------------------------- models

        [System.Serializable] private class ManifestEntry { public string name; public float[] size_m; }
        [System.Serializable] private class ManifestList { public ManifestEntry[] a; }

        /// <summary>Size of a model from the props manifest (for fallbacks and spacing before import).</summary>
        public static Vector3 ManifestSize(string pName)
        {
            if (_manifest == null)
            {
                _manifest = new Dictionary<string, Vector3>();
                string lPath = PbrLibrary.ROOT + "/Models/props_manifest.json";
                if (File.Exists(lPath))
                {
                    ManifestList lList = JsonUtility.FromJson<ManifestList>("{\"a\":" + File.ReadAllText(lPath) + "}");
                    if (lList != null && lList.a != null)
                        foreach (ManifestEntry lEntry in lList.a)
                            if (lEntry.size_m != null && lEntry.size_m.Length == 3) _manifest[lEntry.name] = new Vector3(lEntry.size_m[0], lEntry.size_m[1], lEntry.size_m[2]);
                }
            }
            return _manifest.TryGetValue(pName, out Vector3 lSize) ? lSize : Vector3.one;
        }

        public static bool Has(string pName) => PbrLibrary.HasModel(pName);

        /// <summary>Local bounds of a model (all its renderers, LOD0 only), or the manifest size.</summary>
        public static Bounds ModelBounds(string pName)
        {
            if (_bounds.TryGetValue(pName, out Bounds lCached)) return lCached;
            Bounds lBounds = new Bounds(Vector3.up * ManifestSize(pName).y * 0.5f, ManifestSize(pName));
            GameObject lModel = Has(pName) ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PbrLibrary.ROOT}/Models/{pName}/{pName}.fbx") : null;
            if (lModel != null)
            {
                bool lAny = false;
                foreach (MeshFilter lFilter in lModel.GetComponentsInChildren<MeshFilter>())
                {
                    if (lFilter.sharedMesh == null || lFilter.name.Contains("_LOD1") || lFilter.name.Contains("_LOD2")) continue;
                    Matrix4x4 m = lModel.transform.worldToLocalMatrix * lFilter.transform.localToWorldMatrix;
                    Bounds b = lFilter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 p = m.MultiplyPoint3x4(c);
                        if (!lAny) { lBounds = new Bounds(p, Vector3.zero); lAny = true; }
                        else lBounds.Encapsulate(p);
                    }
                }
            }
            return _bounds[pName] = lBounds;
        }

        public enum Collide { None, Box, Mesh }

        /// <summary>
        /// A model placed at a world position and yaw (pivot at its bottom centre, facing +Z), with a
        /// box collider over its bounds. Missing models become a PBR box of the manifest size
        /// (<paramref name="pFallback"/>) or nothing.
        /// </summary>
        public static GameObject Place(string pName, Transform pParent, Vector3 pPosition, float pYaw, Collide pCollide = Collide.Box, ArchMat pFallback = null, float pScale = 1f, int pLayer = GameLayers.DEFAULT)
        {
            GameObject lModel = Has(pName) ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PbrLibrary.ROOT}/Models/{pName}/{pName}.fbx") : null;
            GameObject lObject;
            Bounds lBounds = ModelBounds(pName);
            if (lModel != null)
            {
                lObject = (GameObject)PrefabUtility.InstantiatePrefab(lModel, pParent);
                lObject.name = pName;
            }
            else
            {
                if (pFallback == null) return null;
                lObject = new GameObject(pName + " (box)");
                lObject.transform.SetParent(pParent, false);
                GameObject lBox = new GameObject("Box");
                lBox.transform.SetParent(lObject.transform, false);
                lBox.transform.localPosition = lBounds.center;
                lBox.transform.localScale = lBounds.size;
                lBox.AddComponent<MeshFilter>().sharedMesh = UnitCube;
                lBox.AddComponent<MeshRenderer>().sharedMaterial = pFallback.material;
            }
            lObject.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0f, pYaw, 0f));
            lObject.transform.localScale = Vector3.one * pScale;
            foreach (Transform lChild in lObject.GetComponentsInChildren<Transform>(true))
            {
                lChild.gameObject.layer = pLayer;
                lChild.gameObject.isStatic = true;
            }
            if (pCollide == Collide.Box)
            {
                BoxCollider lCollider = lObject.AddComponent<BoxCollider>();
                lCollider.center = lBounds.center;
                lCollider.size = Vector3.Max(lBounds.size, Vector3.one * 0.05f);
            }
            else if (pCollide == Collide.Mesh)
                foreach (MeshFilter lFilter in lObject.GetComponentsInChildren<MeshFilter>())
                    if (!lFilter.name.Contains("_LOD1") && !lFilter.name.Contains("_LOD2")) lFilter.gameObject.AddComponent<MeshCollider>().sharedMesh = lFilter.sharedMesh;
            return lObject;
        }

        private static Mesh _cube;

        /// <summary>A 1 m cube with box UVs (fallback boxes).</summary>
        public static Mesh UnitCube
        {
            get
            {
                if (_cube != null) return _cube;
                _cube = AssetDatabase.LoadAssetAtPath<Mesh>(EditorAssets.MESHES + "/Arch_UnitCube_WroughtIron_v.asset");
                if (_cube != null) return _cube;
                ArchMesh lMesh = new ArchMesh();
                lMesh.Box(Iron, Vector3.zero, Vector3.one, false);
                GameObject lTempObject = new GameObject("temp");
                lMesh.Flush(lTempObject.transform, "Arch_UnitCube");
                _cube = lTempObject.GetComponentInChildren<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(lTempObject);
                return _cube;
            }
        }
    }
}
