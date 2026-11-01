using System.IO;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>Fills the <see cref="ArtRegistry"/> with every weapon (W_*) and character (Chr_*) model found.</summary>
    public static class ArtRegistryBuilder
    {
        [MenuItem("Goobyr/Build/Art Registry")]
        public static ArtRegistry Build()
        {
            ArtRegistry lRegistry = EditorAssets.LoadOrCreate<ArtRegistry>(EditorAssets.RESOURCES + "/ArtRegistry.asset");
            lRegistry.entries.Clear();
            Add(lRegistry, PbrLibrary.ROOT + "/Models", "W_");
            Add(lRegistry, EditorAssets.ROOT + "/Art/Characters", "Chr_");
            EditorUtility.SetDirty(lRegistry);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] art registry: {lRegistry.entries.Count} models");
            return lRegistry;
        }

        private static void Add(ArtRegistry pRegistry, string pRoot, string pPrefix)
        {
            if (!AssetDatabase.IsValidFolder(pRoot)) return;
            foreach (string lFolder in AssetDatabase.GetSubFolders(pRoot))
            {
                string lName = Path.GetFileName(lFolder);
                if (!lName.StartsWith(pPrefix)) continue;
                GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{lFolder}/{lName}.fbx");
                if (lModel != null) pRegistry.entries.Add(new ArtRegistry.Entry { name = lName, model = lModel });
            }
        }
    }
}
