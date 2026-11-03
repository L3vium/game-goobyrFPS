using System.Collections.Generic;
using System.IO;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Turns the authored first-person arms (<c>Tools/Blender/fp/fp_build.py</c> exported into
    /// <c>Art/FirstPerson/FP_Arms</c>: FP_Arms.fbx with one take per pose and clip, the baked maps,
    /// fp_poses.json) into what <see cref="Viewmodel"/> plays (<c>Goobyr/Build/First Person Arms</c>):
    /// <list type="bullet">
    /// <item>a generic rig (rooted at the eye), clips named after their takes, compression off so the
    /// sockets land within a millimetre of where they were authored;</item>
    /// <item>the URP Lit material from the baked maps;</item>
    /// <item>the arms controller: a pose layer blending Hip / ADS / Sprint by direct weights, a left-arm
    /// layer (the jab thrown while the right hand holds something), a full-body action layer (jab, cross,
    /// takedown, reload, throw); and one override controller per family of held item;</item>
    /// <item>the <see cref="ViewmodelArms"/> asset in Resources (FirstPerson/ViewmodelArms).</item>
    /// </list>
    /// </summary>
    public static class FpArmsBuilder
    {
        public const string DIR = EditorAssets.ROOT + "/Art/FirstPerson/FP_Arms";
        public const string FBX = DIR + "/FP_Arms.fbx";
        public const string MANIFEST = DIR + "/fp_poses.json";
        public const string OUT = EditorAssets.ROOT + "/Art/Resources/FirstPerson";
        public const string CONTROLLER = OUT + "/FP_Arms.controller";
        public const string LEFT_MASK = OUT + "/M_FP_LeftArm.mask";
        public const string SET = OUT + "/ViewmodelArms.asset";

        /// <summary>The families of held items (fp_build.py FAMILIES and HAND_FAMILIES).</summary>
        public static readonly string[] FAMILIES = { "Fists", "Pistol", "Smg", "Shotgun", "Sniper", "Launcher", "Flamer", "Pinch", "Palm", "Handle" };
        /// <summary>Weapon model of each gun family (the pose test puts it in the socket).</summary>
        public static readonly Dictionary<string, string> WEAPONS = new Dictionary<string, string>
        {
            ["Pistol"] = "W_Pistol", ["Smg"] = "W_Smg", ["Shotgun"] = "W_Shotgun", ["Sniper"] = "W_Sniper",
            ["Launcher"] = "W_RocketLauncher", ["Flamer"] = "W_Flamethrower"
        };
        /// <summary>The static poses (the base controller holds the fists' ones; families override them).</summary>
        private static readonly string[] POSES = { "Hip", "ADS", "Sprint" };

        [MenuItem("Goobyr/Build/First Person Arms")]
        public static void Build()
        {
            if (!File.Exists(FBX)) { Debug.LogError($"[Build] first-person arms: {FBX} is missing (Tools/Blender/fp/fp_build.py)"); return; }
            EditorAssets.EnsureFolder(OUT);
            ImportModel();
            Material();
            PrepareWeapons();
            Dictionary<string, AnimationClip> lClips = Clips();
            AnimatorController lController = Controller(lClips);
            Set(lController, lClips);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Build] first-person arms: {lClips.Count} clips, {FAMILIES.Length} families");
        }

        // ---------------------------------------------------------------- model

        private static string ClipName(string pTake)
        {
            int lBar = pTake.LastIndexOf('|');
            return lBar >= 0 ? pTake.Substring(lBar + 1) : pTake;
        }

        private static bool IsPose(string pName)
        {
            foreach (string lPose in POSES) if (pName.EndsWith("_" + lPose)) return true;
            return false;
        }

        public static void ImportModel()
        {
            ModelImporter lImporter = (ModelImporter)AssetImporter.GetAtPath(FBX);
            lImporter.animationType = ModelImporterAnimationType.Generic;
            lImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            lImporter.optimizeGameObjects = false;
            lImporter.importAnimation = true;
            // Off: the socket must land where it was authored (the pose test checks 1 mm).
            lImporter.animationCompression = ModelImporterAnimationCompression.Off;
            lImporter.resampleCurves = true;
            lImporter.importBlendShapes = false;
            lImporter.importCameras = false;
            lImporter.importLights = false;
            lImporter.importVisibility = false;
            lImporter.isReadable = false;
            lImporter.SaveAndReimport();

            List<ModelImporterClipAnimation> lClips = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation lDefault in lImporter.defaultClipAnimations)
            {
                if (lDefault.takeName.StartsWith("__preview__")) continue;
                lDefault.name = ClipName(lDefault.takeName);
                lDefault.loopTime = IsPose(lDefault.name);
                lDefault.loopPose = false;
                lClips.Add(lDefault);
            }
            lImporter.clipAnimations = lClips.ToArray();
            lImporter.SaveAndReimport();
        }

        /// <summary>URP Lit from the baked maps (FP_Arms_body_*), remapped on the model.</summary>
        public static void Material()
        {
            PbrLibrary.PrepareFolder(DIR);
            // PrepareFolder takes any name containing "_arm" for a linear ARM map, and "FP_Arms_body_diff"
            // contains it: the colour map must stay sRGB or the leather and nylon wash out to white.
            foreach (string lFile in Directory.GetFiles(DIR, "*_diff.*"))
            {
                TextureImporter lColour = AssetImporter.GetAtPath(lFile.Replace('\\', '/')) as TextureImporter;
                if (lColour == null || lColour.sRGBTexture) continue;
                lColour.sRGBTexture = true;
                lColour.SaveAndReimport();
            }
            Material lMaterial = PbrLibrary.MakeMaterial(DIR, "FP_Arms_body", $"{DIR}/M_FP_Arms_body.mat", PbrLibrary.Kind.Opaque);
            if (lMaterial == null) { Debug.LogWarning("[Build] first-person arms: no baked maps"); return; }
            // Leather and nylon: a soft sheen, no plastic highlight; sleeves are open shells.
            lMaterial.SetFloat("_Smoothness", 0.55f);
            lMaterial.SetFloat("_Cull", 0f);
            lMaterial.doubleSidedGI = true;
            EditorUtility.SetDirty(lMaterial);
            ModelImporter lImporter = (ModelImporter)AssetImporter.GetAtPath(FBX);
            bool lChanged = false;
            HashSet<string> lSlots = new HashSet<string>();
            foreach (Object lSub in AssetDatabase.LoadAllAssetsAtPath(FBX))
                if (lSub is Renderer lRenderer) foreach (Material lSlot in lRenderer.sharedMaterials) if (lSlot != null) lSlots.Add(lSlot.name);
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> lPair in lImporter.GetExternalObjectMap())
                if (lPair.Key.type == typeof(Material)) lSlots.Add(lPair.Key.name);
            foreach (string lSlot in lSlots)
            {
                AssetImporter.SourceAssetIdentifier lId = new AssetImporter.SourceAssetIdentifier(typeof(Material), lSlot);
                if (lImporter.GetExternalObjectMap().TryGetValue(lId, out Object lOld) && lOld == lMaterial) continue;
                lImporter.AddRemap(lId, lMaterial);
                lChanged = true;
            }
            if (lChanged) lImporter.SaveAndReimport();
        }

        /// <summary>The re-exported weapons (Sight, ForeGrip, MagWell, Mag child): importer settings and packed maps.</summary>
        public static void PrepareWeapons()
        {
            foreach (string lWeapon in new[] { "W_Pistol", "W_SilencedPistol", "W_Smg", "W_Shotgun", "W_Sniper", "W_RocketLauncher", "W_Flamethrower" })
            {
                string lDir = $"{PbrLibrary.ROOT}/Models/{lWeapon}";
                if (AssetDatabase.IsValidFolder(lDir)) PbrLibrary.PrepareFolder(lDir);
            }
        }

        public static Dictionary<string, AnimationClip> Clips()
        {
            Dictionary<string, AnimationClip> lClips = new Dictionary<string, AnimationClip>();
            foreach (Object lSub in AssetDatabase.LoadAllAssetsAtPath(FBX))
                if (lSub is AnimationClip lClip && !lClip.name.StartsWith("__preview__")) lClips[lClip.name] = lClip;
            return lClips;
        }

        // ---------------------------------------------------------------- controller

        private static AnimationClip Slot(AnimatorController pController, string pName)
        {
            // An empty placeholder each family overrides (an empty clip writes nothing).
            AnimationClip lClip = new AnimationClip { name = pName };
            AssetDatabase.AddObjectToAsset(lClip, pController);
            return lClip;
        }

        private static AnimatorState State(AnimatorStateMachine pMachine, string pName, Motion pMotion, Vector3 pPosition, bool pPaced = false)
        {
            AnimatorState lState = pMachine.AddState(pName, pPosition);
            lState.motion = pMotion;
            lState.writeDefaultValues = false;
            if (pPaced)
            {
                lState.speedParameter = ViewmodelArms.P_SPEED;
                lState.speedParameterActive = true;
            }
            return lState;
        }

        private static AnimatorStateMachine Layer(AnimatorController pController, string pName, AvatarMask pMask)
        {
            pController.AddLayer(pName);
            AnimatorControllerLayer[] lLayers = pController.layers;
            AnimatorControllerLayer lLayer = lLayers[lLayers.Length - 1];
            lLayer.defaultWeight = 1f;
            lLayer.avatarMask = pMask;
            lLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            pController.layers = lLayers;
            return lLayer.stateMachine;
        }

        /// <summary>Transform mask of the left arm (clavicle down to the fingertips and the magazine socket).</summary>
        public static AvatarMask LeftArmMask()
        {
            GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
            AvatarMask lMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(LEFT_MASK);
            if (lMask == null)
            {
                lMask = new AvatarMask();
                AssetDatabase.CreateAsset(lMask, LEFT_MASK);
            }
            lMask.transformCount = 0;
            lMask.AddTransformPath(lModel.transform, true);
            for (int i = 0; i < lMask.transformCount; i++)
            {
                string lPath = lMask.GetTransformPath(i);
                lMask.SetTransformActive(i, lPath.Contains("/clavicle_l"));
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) lMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            EditorUtility.SetDirty(lMask);
            return lMask;
        }

        public static AnimatorController Controller(Dictionary<string, AnimationClip> pClips)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(CONTROLLER) != null) AssetDatabase.DeleteAsset(CONTROLLER);
            AnimatorController lController = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER);
            foreach (string lFloat in new[] { ViewmodelArms.P_HIP, ViewmodelArms.P_ADS, ViewmodelArms.P_SPRINT, ViewmodelArms.P_SPEED })
                lController.AddParameter(lFloat, AnimatorControllerParameterType.Float);
            AnimatorControllerParameter[] lParameters = lController.parameters;
            foreach (AnimatorControllerParameter lParameter in lParameters)
                lParameter.defaultFloat = lParameter.name == ViewmodelArms.P_ADS || lParameter.name == ViewmodelArms.P_SPRINT ? 0f : 1f;
            lController.parameters = lParameters;

            // Pose: the three static poses of the held family, by direct weights from the viewmodel.
            AnimatorControllerLayer[] lLayers = lController.layers;
            lLayers[0].name = "Pose";
            lController.layers = lLayers;
            AnimatorStateMachine lPose = lController.layers[0].stateMachine;
            BlendTree lTree = new BlendTree { name = "Poses", blendType = BlendTreeType.Direct, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(lTree, lController);
            string[] lParams = { ViewmodelArms.P_HIP, ViewmodelArms.P_ADS, ViewmodelArms.P_SPRINT };
            for (int i = 0; i < POSES.Length; i++) lTree.AddChild(Get(pClips, "Fists_" + POSES[i]));
            ChildMotion[] lChildren = lTree.children;
            for (int i = 0; i < lChildren.Length; i++) lChildren[i].directBlendParameter = lParams[i];
            lTree.children = lChildren;
            lPose.defaultState = State(lPose, "Poses", lTree, new Vector3(300, 0));

            // Left arm: the jab thrown while the right hand holds the item.
            AnimatorStateMachine lLeft = Layer(lController, "Left", LeftArmMask());
            lLeft.defaultState = State(lLeft, ViewmodelArms.S_EMPTY, null, new Vector3(300, 0));
            State(lLeft, ViewmodelArms.S_JAB, Get(pClips, "Jab"), new Vector3(300, 60));

            // Actions over everything.
            AnimatorStateMachine lAction = Layer(lController, "Action", null);
            lAction.defaultState = State(lAction, ViewmodelArms.S_EMPTY, null, new Vector3(300, 0));
            State(lAction, ViewmodelArms.S_JAB, Get(pClips, "Jab"), new Vector3(0, 60));
            State(lAction, ViewmodelArms.S_CROSS, Get(pClips, "Cross"), new Vector3(300, 60));
            State(lAction, ViewmodelArms.S_TAKEDOWN, Get(pClips, "Takedown"), new Vector3(600, 60));
            State(lAction, ViewmodelArms.S_RELOAD, Slot(lController, "Slot_Reload"), new Vector3(0, 120), true);
            State(lAction, ViewmodelArms.S_THROW, Slot(lController, "Slot_Throw"), new Vector3(300, 120), true);
            EditorUtility.SetDirty(lController);
            return lController;
        }

        private static AnimationClip Get(Dictionary<string, AnimationClip> pClips, string pName)
        {
            if (pClips.TryGetValue(pName, out AnimationClip lClip)) return lClip;
            Debug.LogWarning($"[Build] first-person arms: no clip {pName}");
            return null;
        }

        /// <summary>One override controller per family: its Hip / ADS / Sprint, its reload and throw.</summary>
        private static AnimatorOverrideController Override(AnimatorController pController, Dictionary<string, AnimationClip> pClips, string pFamily, out bool pReload, out float pReloadLength, out float pThrowLength)
        {
            string lPath = $"{OUT}/FP_{pFamily}.overrideController";
            AnimatorOverrideController lOverride = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(lPath);
            if (lOverride == null)
            {
                lOverride = new AnimatorOverrideController(pController);
                AssetDatabase.CreateAsset(lOverride, lPath);
            }
            lOverride.runtimeAnimatorController = pController;
            List<KeyValuePair<AnimationClip, AnimationClip>> lPairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            lOverride.GetOverrides(lPairs);
            pReload = false;
            pReloadLength = 0f;
            pThrowLength = 0f;
            for (int i = 0; i < lPairs.Count; i++)
            {
                AnimationClip lOriginal = lPairs[i].Key;
                AnimationClip lReplacement = null;
                foreach (string lPose in POSES)
                    if (lOriginal.name == "Fists_" + lPose) pClips.TryGetValue($"{pFamily}_{lPose}", out lReplacement);
                if (lOriginal.name == "Slot_Reload" && pClips.TryGetValue($"{pFamily}_Reload", out lReplacement)) { pReload = true; pReloadLength = lReplacement.length; }
                if (lOriginal.name == "Slot_Throw" && pClips.TryGetValue($"{pFamily}_Throw", out lReplacement)) pThrowLength = lReplacement.length;
                lPairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(lOriginal, lReplacement);
            }
            lOverride.ApplyOverrides(lPairs);
            EditorUtility.SetDirty(lOverride);
            return lOverride;
        }

        public static void Set(AnimatorController pController, Dictionary<string, AnimationClip> pClips)
        {
            ViewmodelArms lSet = EditorAssets.LoadOrCreate<ViewmodelArms>(SET);
            lSet.model = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
            lSet.controller = pController;
            lSet.manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(MANIFEST);
            lSet.families.Clear();
            foreach (string lFamily in FAMILIES)
            {
                AnimatorOverrideController lOverride = Override(pController, pClips, lFamily, out bool lReload, out float lReloadLength, out float lThrowLength);
                lSet.families.Add(new ViewmodelArms.Family
                {
                    name = lFamily, controller = lOverride, authoredReload = lReload, reloadLength = lReloadLength, throwLength = lThrowLength
                });
            }
            EditorUtility.SetDirty(lSet);
        }
    }
}
