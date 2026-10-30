using TMPro;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// TextMeshPro font assets from the Barlow family (SIL Open Font License, in
    /// <c>Art/Fonts/Barlow</c>) into <c>Art/Resources/Fonts</c>, where the UI loads them by name.
    /// Dynamic atlases: any glyph the game prints is added on the fly.
    /// </summary>
    public static class FontBuilder
    {
        private const string SOURCE = EditorAssets.ROOT + "/Art/Fonts/Barlow/";
        private const string OUTPUT = EditorAssets.ROOT + "/Art/Resources/Fonts/";

        [MenuItem("Goobyr/Build/Fonts")]
        public static void Build()
        {
            EditorAssets.EnsureFolder(OUTPUT.TrimEnd('/'));
            foreach (string lName in new[] { "Barlow-Regular", "Barlow-Medium", "Barlow-SemiBold", "Barlow-Bold", "BarlowCondensed-Medium", "BarlowCondensed-SemiBold", "BarlowCondensed-Bold" })
                Make(lName);
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] fonts");
        }

        private static void Make(string pName)
        {
            Font lFont = AssetDatabase.LoadAssetAtPath<Font>(SOURCE + pName + ".ttf");
            if (lFont == null) { Debug.LogError("[Build] missing font " + pName); return; }
            string lPath = OUTPUT + pName + " SDF.asset";
            AssetDatabase.DeleteAsset(lPath);
            TMP_FontAsset lAsset = TMP_FontAsset.CreateFontAsset(lFont, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            lAsset.name = pName + " SDF";
            AssetDatabase.CreateAsset(lAsset, lPath);
            // The atlas texture and material live inside the font asset.
            lAsset.atlasTexture.name = pName + " Atlas";
            AssetDatabase.AddObjectToAsset(lAsset.atlasTexture, lAsset);
            lAsset.material.name = pName + " Material";
            AssetDatabase.AddObjectToAsset(lAsset.material, lAsset);
            lAsset.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;!?'\"-_+=/\\()[]<>%#&*@°·•—–|←→↑↓▲▼");
            EditorUtility.SetDirty(lAsset);
        }
    }
}
