using System.IO;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>Small helpers every builder shares: folders, create-or-replace assets.</summary>
    public static class EditorAssets
    {
        public const string ROOT = "Assets/_Game";
        public const string DATA = ROOT + "/Data";
        public const string RESOURCES = ROOT + "/Data/Resources";
        public const string ART_RESOURCES = ROOT + "/Art/Resources/Art";
        public const string MESHES = ROOT + "/Art/Generated/Meshes";
        public const string PREFABS = ROOT + "/Prefabs";
        public const string SCENES = ROOT + "/Scenes";

        public static void EnsureFolder(string pPath)
        {
            if (AssetDatabase.IsValidFolder(pPath)) return;
            string lParent = Path.GetDirectoryName(pPath).Replace('\\', '/');
            EnsureFolder(lParent);
            AssetDatabase.CreateFolder(lParent, Path.GetFileName(pPath));
        }

        /// <summary>Loads the asset at the path, or creates it - keeps the GUID stable across rebuilds.</summary>
        public static T LoadOrCreate<T>(string pPath) where T : ScriptableObject
        {
            T lAsset = AssetDatabase.LoadAssetAtPath<T>(pPath);
            if (lAsset != null) return lAsset;
            EnsureFolder(Path.GetDirectoryName(pPath).Replace('\\', '/'));
            lAsset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(lAsset, pPath);
            return lAsset;
        }

        public static Material Material(string pPath, Shader pShader)
        {
            Material lMaterial = AssetDatabase.LoadAssetAtPath<Material>(pPath);
            if (lMaterial == null)
            {
                EnsureFolder(Path.GetDirectoryName(pPath).Replace('\\', '/'));
                lMaterial = new Material(pShader);
                AssetDatabase.CreateAsset(lMaterial, pPath);
            }
            else lMaterial.shader = pShader;
            return lMaterial;
        }

        /// <summary>Saves (or overwrites) a generated mesh asset.</summary>
        public static Mesh SaveMesh(Mesh pMesh, string pName)
        {
            EnsureFolder(MESHES);
            string lPath = MESHES + "/" + pName + ".asset";
            Mesh lExisting = AssetDatabase.LoadAssetAtPath<Mesh>(lPath);
            if (lExisting != null)
            {
                EditorUtility.CopySerialized(pMesh, lExisting);
                return lExisting;
            }
            pMesh.name = pName;
            AssetDatabase.CreateAsset(pMesh, lPath);
            return pMesh;
        }
    }
}
