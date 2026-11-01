using System.Collections.Generic;
using System.IO;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Turns the realistic humans (<c>Art/Characters/Chr_*</c>, one shared 53-bone skeleton in an
    /// A-pose rest) and the mocap clips (<c>Art/Characters/Animations</c>, <c>clips.json</c>) into
    /// what the game plays (<c>Goobyr/Build/Characters</c>):
    /// <list type="bullet">
    /// <item>URP Lit materials for every slot (skin, eyes glossy, hair/brows/lashes alpha-clipped), remapped on the models;</item>
    /// <item>a Humanoid avatar per character whose reference pose is a real T-pose (the A-pose arms
    /// are raised to the horizontal before the avatar is built), and one for the clips built on the
    /// reference skeleton (Chr_Agent), so retargeting is consistent on every body;</item>
    /// <item>clip settings from <c>clips.json</c> (loop, root motion baked or extracted);</item>
    /// <item>the upper-body and arms avatar masks, the third-person controller (<c>Characters/Human</c>)
    /// and the first-person arms controller (<c>Characters/Arms</c>), and the calibrated grips
    /// (<see cref="HumanRig"/>).</item>
    /// </list>
    /// </summary>
    public static class CharacterBuilder
    {
        public const string DIR = EditorAssets.ROOT + "/Art/Characters";
        public const string ANIM_DIR = DIR + "/Animations";
        public const string OUT = EditorAssets.ROOT + "/Art/Resources/Characters";
        public const string REFERENCE = "Chr_Agent";
        /// <summary>The clip whose avatar every other clip copies (all clips share its hierarchy).</summary>
        public const string REFERENCE_CLIP = "Idle";

        [System.Serializable]
        private class ClipInfo
        {
            public string name;
            public string file;
            public bool loop;
            public bool inPlace;
            public float durationSec;
            public bool upperBodyOnly;
        }

        [System.Serializable]
        private class ClipList { public ClipInfo[] clips; }

        /// <summary>Clips whose travel the game moves itself (the motor, the ragdoll): extracted from the body, not baked.</summary>
        private static readonly HashSet<string> TRAVEL = new HashSet<string> { "Vault", "Mantle", "Hang_ClimbUp", "Death_F", "Death_B", "Takedown_Attacker", "Takedown_Victim" };

        private static readonly (HumanBodyBones bone, string name)[] BONES =
        {
            (HumanBodyBones.Hips, "pelvis"), (HumanBodyBones.Spine, "spine_01"), (HumanBodyBones.Chest, "spine_02"), (HumanBodyBones.UpperChest, "spine_03"),
            (HumanBodyBones.Neck, "neck_01"), (HumanBodyBones.Head, "head"),
            (HumanBodyBones.LeftShoulder, "clavicle_l"), (HumanBodyBones.LeftUpperArm, "upperarm_l"), (HumanBodyBones.LeftLowerArm, "lowerarm_l"), (HumanBodyBones.LeftHand, "hand_l"),
            (HumanBodyBones.RightShoulder, "clavicle_r"), (HumanBodyBones.RightUpperArm, "upperarm_r"), (HumanBodyBones.RightLowerArm, "lowerarm_r"), (HumanBodyBones.RightHand, "hand_r"),
            (HumanBodyBones.LeftUpperLeg, "thigh_l"), (HumanBodyBones.LeftLowerLeg, "calf_l"), (HumanBodyBones.LeftFoot, "foot_l"), (HumanBodyBones.LeftToes, "ball_l"),
            (HumanBodyBones.RightUpperLeg, "thigh_r"), (HumanBodyBones.RightLowerLeg, "calf_r"), (HumanBodyBones.RightFoot, "foot_r"), (HumanBodyBones.RightToes, "ball_r"),
            (HumanBodyBones.LeftThumbProximal, "thumb_01_l"), (HumanBodyBones.LeftThumbIntermediate, "thumb_02_l"), (HumanBodyBones.LeftThumbDistal, "thumb_03_l"),
            (HumanBodyBones.LeftIndexProximal, "index_01_l"), (HumanBodyBones.LeftIndexIntermediate, "index_02_l"), (HumanBodyBones.LeftIndexDistal, "index_03_l"),
            (HumanBodyBones.LeftMiddleProximal, "middle_01_l"), (HumanBodyBones.LeftMiddleIntermediate, "middle_02_l"), (HumanBodyBones.LeftMiddleDistal, "middle_03_l"),
            (HumanBodyBones.LeftRingProximal, "ring_01_l"), (HumanBodyBones.LeftRingIntermediate, "ring_02_l"), (HumanBodyBones.LeftRingDistal, "ring_03_l"),
            (HumanBodyBones.LeftLittleProximal, "pinky_01_l"), (HumanBodyBones.LeftLittleIntermediate, "pinky_02_l"), (HumanBodyBones.LeftLittleDistal, "pinky_03_l"),
            (HumanBodyBones.RightThumbProximal, "thumb_01_r"), (HumanBodyBones.RightThumbIntermediate, "thumb_02_r"), (HumanBodyBones.RightThumbDistal, "thumb_03_r"),
            (HumanBodyBones.RightIndexProximal, "index_01_r"), (HumanBodyBones.RightIndexIntermediate, "index_02_r"), (HumanBodyBones.RightIndexDistal, "index_03_r"),
            (HumanBodyBones.RightMiddleProximal, "middle_01_r"), (HumanBodyBones.RightMiddleIntermediate, "middle_02_r"), (HumanBodyBones.RightMiddleDistal, "middle_03_r"),
            (HumanBodyBones.RightRingProximal, "ring_01_r"), (HumanBodyBones.RightRingIntermediate, "ring_02_r"), (HumanBodyBones.RightRingDistal, "ring_03_r"),
            (HumanBodyBones.RightLittleProximal, "pinky_01_r"), (HumanBodyBones.RightLittleIntermediate, "pinky_02_r"), (HumanBodyBones.RightLittleDistal, "pinky_03_r"),
        };

        [MenuItem("Goobyr/Build/Characters")]
        public static void Build()
        {
            EditorAssets.EnsureFolder(OUT);
            Materials();
            Avatars();
            Clips();
            Masks();
            HumanController();
            ArmsController();
            Calibrate();
            ArtRegistryBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] characters");
        }

        private static IEnumerable<string> CharacterFolders()
        {
            foreach (string lFolder in AssetDatabase.GetSubFolders(DIR))
                if (Path.GetFileName(lFolder).StartsWith("Chr_")) yield return lFolder;
        }

        private static string Fbx(string pFolder) => $"{pFolder}/{Path.GetFileName(pFolder)}.fbx";

        // ---------------------------------------------------------------- materials

        /// <summary>One URP Lit material per slot (<c>&lt;Name&gt;_&lt;Slot&gt;_diff/_nor_gl/_arm</c>), remapped on the model.</summary>
        public static void Materials()
        {
            foreach (string lFolder in CharacterFolders())
            {
                string lName = Path.GetFileName(lFolder);
                PbrLibrary.PrepareFolder(lFolder);
                // Hair, brows and lashes carry their coverage in the PNG's alpha.
                foreach (string lFile in Directory.GetFiles(lFolder, "*_diff.png"))
                {
                    TextureImporter lPng = AssetImporter.GetAtPath(lFile.Replace('\\', '/')) as TextureImporter;
                    if (lPng == null || lPng.alphaIsTransparency) continue;
                    lPng.alphaIsTransparency = true;
                    lPng.mipMapsPreserveCoverage = true;
                    lPng.alphaTestReferenceValue = 0.4f;
                    lPng.SaveAndReimport();
                }

                ModelImporter lImporter = AssetImporter.GetAtPath(Fbx(lFolder)) as ModelImporter;
                if (lImporter == null) continue;
                HashSet<string> lSlots = new HashSet<string>();
                foreach (Object lSub in AssetDatabase.LoadAllAssetsAtPath(Fbx(lFolder)))
                    if (lSub is Renderer lRenderer) foreach (Material lMat in lRenderer.sharedMaterials) if (lMat != null) lSlots.Add(lMat.name);
                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> lPair in lImporter.GetExternalObjectMap())
                    if (lPair.Key.type == typeof(Material)) lSlots.Add(lPair.Key.name);

                bool lChanged = false;
                foreach (string lSlot in lSlots)
                {
                    string lPart = lSlot.StartsWith(lName + "_") ? lSlot.Substring(lName.Length + 1) : lSlot;
                    bool lCard = lPart == "Hair" || lPart == "Brows" || lPart == "Lashes";
                    Material lMaterial = PbrLibrary.MakeMaterial(lFolder, lSlot, $"{lFolder}/M_{lSlot}.mat", lCard ? PbrLibrary.Kind.Cutout : PbrLibrary.Kind.Opaque);
                    if (lMaterial == null) continue;
                    Tune(lMaterial, lPart);
                    AssetImporter.SourceAssetIdentifier lId = new AssetImporter.SourceAssetIdentifier(typeof(Material), lSlot);
                    if (lImporter.GetExternalObjectMap().TryGetValue(lId, out Object lOld) && lOld == lMaterial) continue;
                    lImporter.AddRemap(lId, lMaterial);
                    lChanged = true;
                }
                if (lChanged) lImporter.SaveAndReimport();
            }
        }

        /// <summary>Per-slot look: skin a little soft, eyes wet, cards thin and two-sided; emission on (black) for the hit flash.</summary>
        private static void Tune(Material pMaterial, string pPart)
        {
            bool lHasMap = pMaterial.GetTexture("_MetallicGlossMap") != null;
            float lSmoothness = pPart switch
            {
                "Body" => 0.62f,
                "Eyes" => 1f,
                "Hair" => 0.55f,
                "Brows" or "Lashes" => 0.3f,
                "Shoes" or "Boots" => 0.85f,
                "Earpiece" => 0.9f,
                // Wool suits and cotton: a soft sheen, no plastic highlight.
                _ => 0.4f
            };
            // With a metallic map, _Smoothness scales the map's smoothness; without, it is the value.
            if (pPart == "Eyes" && !lHasMap) lSmoothness = 0.9f;
            if (!lHasMap && pPart == "Body") lSmoothness = 0.35f;
            pMaterial.SetFloat("_Smoothness", lSmoothness);
            if (pPart == "Eyes")
            {
                pMaterial.DisableKeyword("_METALLICSPECGLOSSMAP");
                pMaterial.SetTexture("_MetallicGlossMap", null);
                pMaterial.SetFloat("_Metallic", 0f);
                pMaterial.SetFloat("_Smoothness", 0.9f);
            }
            if (pPart == "Hair" || pPart == "Brows" || pPart == "Lashes")
            {
                pMaterial.SetFloat("_Cutoff", pPart == "Hair" ? 0.4f : 0.3f);
                pMaterial.SetFloat("_ReceiveShadows", 1f);
            }
            // Garments are open shells (collars, cuffs, hems): seen from inside they must not vanish.
            bool lCloth = pPart is "Suit" or "Shirt" or "Top" or "Jacket" or "Parka" or "Vest" or "Pants";
            if (lCloth) { pMaterial.SetFloat("_Cull", 0f); pMaterial.doubleSidedGI = true; }
            pMaterial.SetColor("_EmissionColor", Color.black);
            pMaterial.EnableKeyword("_EMISSION");
            pMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(pMaterial);
        }

        // ---------------------------------------------------------------- avatars

        /// <summary>Humanoid avatars: every character from its own T-posed skeleton, the clips from the reference skeleton.</summary>
        public static void Avatars()
        {
            foreach (string lFolder in CharacterFolders())
            {
                string lPath = Fbx(lFolder);
                GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>(lPath);
                ModelImporter lImporter = AssetImporter.GetAtPath(lPath) as ModelImporter;
                if (lModel == null || lImporter == null) continue;
                List<SkeletonBone> lSkeleton = TPoseSkeleton(lModel);
                lImporter.animationType = ModelImporterAnimationType.Human;
                lImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                lImporter.humanDescription = Description(lSkeleton);
                lImporter.importAnimation = false;
                lImporter.optimizeGameObjects = false;
                lImporter.importBlendShapes = false;
                lImporter.importCameras = false;
                lImporter.importLights = false;
                lImporter.SaveAndReimport();
                Avatar lAvatar = AvatarAt(lPath);
                if (lAvatar == null || !lAvatar.isValid || !lAvatar.isHuman) Debug.LogError($"[Build] characters: bad avatar on {lPath}");
            }
        }

        public static Avatar AvatarAt(string pPath)
        {
            foreach (Object lSub in AssetDatabase.LoadAllAssetsAtPath(pPath)) if (lSub is Avatar lAvatar) return lAvatar;
            return null;
        }

        private static HumanDescription Description(List<SkeletonBone> pSkeleton)
        {
            List<HumanBone> lHuman = new List<HumanBone>();
            foreach ((HumanBodyBones lBone, string lName) in BONES)
                lHuman.Add(new HumanBone { humanName = HumanTrait.BoneName[(int)lBone], boneName = lName, limit = new HumanLimit { useDefaultValues = true } });
            return new HumanDescription
            {
                human = lHuman.ToArray(),
                skeleton = pSkeleton.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
            };
        }

        /// <summary>
        /// The model's skeleton with the arms raised from the A-pose to a T-pose (upper arm, forearm
        /// and hand straight along the shoulder line, palms down) and the legs straight: the reference
        /// pose a Humanoid avatar must be built on, or every arm pose comes out 45 degrees off.
        /// </summary>
        private static List<SkeletonBone> TPoseSkeleton(GameObject pModel)
        {
            GameObject lInstance = Object.Instantiate(pModel);
            lInstance.name = pModel.name;
            try
            {
                Transform t(string n) => FindDeep(lInstance.transform, n);
                foreach (string s in new[] { "l", "r" })
                {
                    Vector3 lOut = s == "l" ? Vector3.left : Vector3.right;
                    Straighten(t("upperarm_" + s), t("lowerarm_" + s), lOut);
                    Straighten(t("lowerarm_" + s), t("hand_" + s), lOut);
                    Straighten(t("hand_" + s), t("middle_01_" + s), lOut);
                    Straighten(t("thigh_" + s), t("calf_" + s), Vector3.down);
                    Straighten(t("calf_" + s), t("foot_" + s), Vector3.down);
                }
                List<SkeletonBone> lBones = new List<SkeletonBone>();
                foreach (Transform lBone in lInstance.GetComponentsInChildren<Transform>(true))
                    lBones.Add(new SkeletonBone { name = lBone.name, position = lBone.localPosition, rotation = lBone.localRotation, scale = lBone.localScale });
                return lBones;
            }
            finally { Object.DestroyImmediate(lInstance); }
        }

        private static void Straighten(Transform pBone, Transform pChild, Vector3 pDirection)
        {
            if (pBone == null || pChild == null) return;
            Vector3 lCurrent = (pChild.position - pBone.position).normalized;
            pBone.rotation = Quaternion.FromToRotation(lCurrent, pDirection) * pBone.rotation;
        }

        public static Transform FindDeep(Transform pRoot, string pName)
        {
            if (pRoot.name == pName) return pRoot;
            foreach (Transform lChild in pRoot)
            {
                Transform lFound = FindDeep(lChild, pName);
                if (lFound != null) return lFound;
            }
            return null;
        }

        // ---------------------------------------------------------------- clips

        private static ClipInfo[] ReadClips()
        {
            string lJson = File.ReadAllText(ANIM_DIR + "/clips.json");
            return JsonUtility.FromJson<ClipList>("{\"clips\":" + lJson + "}").clips;
        }

        /// <summary>
        /// The reference clip gets an avatar built from its own hierarchy (root = the armature) with the
        /// reference character's T-posed bones; every other clip copies it. Then loop and root motion:
        /// in-place clips bake everything into the pose, travelling ones let the body's travel go
        /// (the motor moves the character), rotation is always baked so a clip never turns the body.
        /// </summary>
        public static void Clips()
        {
            ClipInfo[] lClips = ReadClips();
            string lReferencePath = $"{ANIM_DIR}/{REFERENCE_CLIP}.fbx";
            GameObject lReferenceModel = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx($"{DIR}/{REFERENCE}"));
            GameObject lClipModel = AssetDatabase.LoadAssetAtPath<GameObject>(lReferencePath);
            List<SkeletonBone> lCharacter = TPoseSkeleton(lReferenceModel);

            // The clip files have no "Armature" node: their root is the armature (same transform).
            List<SkeletonBone> lSkeleton = new List<SkeletonBone>();
            SkeletonBone lArmature = lCharacter.Find(b => b.name == "Armature");
            lSkeleton.Add(new SkeletonBone { name = lClipModel.name, position = lClipModel.transform.localPosition, rotation = lClipModel.transform.localRotation, scale = Vector3.one });
            HashSet<string> lClipBones = new HashSet<string>();
            foreach (Transform lBone in lClipModel.GetComponentsInChildren<Transform>(true)) lClipBones.Add(lBone.name);
            foreach (SkeletonBone lBone in lCharacter)
                if (lBone.name != lReferenceModel.name && lBone.name != "Armature" && lClipBones.Contains(lBone.name)) lSkeleton.Add(lBone);
            if (Quaternion.Angle(lArmature.rotation, lClipModel.transform.localRotation) > 1f)
                Debug.LogWarning($"[Build] characters: clip root {lClipModel.transform.localEulerAngles} differs from the armature {lArmature.rotation.eulerAngles}");

            ModelImporter lReference = (ModelImporter)AssetImporter.GetAtPath(lReferencePath);
            lReference.animationType = ModelImporterAnimationType.Human;
            lReference.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            lReference.humanDescription = Description(lSkeleton);
            lReference.SaveAndReimport();
            Avatar lClipAvatar = AvatarAt(lReferencePath);
            if (lClipAvatar == null || !lClipAvatar.isHuman) { Debug.LogError("[Build] characters: the reference clip has no human avatar"); return; }

            foreach (ClipInfo lInfo in lClips)
            {
                string lPath = $"{ANIM_DIR}/{lInfo.file}";
                ModelImporter lImporter = AssetImporter.GetAtPath(lPath) as ModelImporter;
                if (lImporter == null) { Debug.LogWarning($"[Build] characters: missing clip {lPath}"); continue; }
                if (lInfo.name != REFERENCE_CLIP)
                {
                    lImporter.animationType = ModelImporterAnimationType.Human;
                    lImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    lImporter.sourceAvatar = lClipAvatar;
                }
                lImporter.importAnimation = true;
                lImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                lImporter.resampleCurves = true;
                lImporter.animationCompression = ModelImporterAnimationCompression.Optimal;

                ModelImporterClipAnimation[] lDefaults = lImporter.defaultClipAnimations;
                ModelImporterClipAnimation lClip = null;
                foreach (ModelImporterClipAnimation lDefault in lDefaults) if (!lDefault.takeName.StartsWith("__preview__")) lClip = lDefault;
                if (lClip == null && lDefaults.Length > 0) lClip = lDefaults[0];
                if (lClip == null) continue;
                bool lTravel = TRAVEL.Contains(lInfo.name);
                lClip.name = lInfo.name;
                lClip.loopTime = lInfo.loop;
                lClip.loopPose = false;
                lClip.lockRootRotation = true;
                lClip.keepOriginalOrientation = true;
                lClip.lockRootHeightY = !lTravel;
                lClip.keepOriginalPositionY = !lTravel;
                lClip.heightFromFeet = false;
                lClip.lockRootPositionXZ = !lTravel;
                lClip.keepOriginalPositionXZ = !lTravel;
                lClip.mirror = false;
                lImporter.clipAnimations = new[] { lClip };
                lImporter.SaveAndReimport();
            }
        }

        public static AnimationClip Clip(string pName)
        {
            foreach (Object lSub in AssetDatabase.LoadAllAssetsAtPath($"{ANIM_DIR}/{pName}.fbx"))
                if (lSub is AnimationClip lClip && !lClip.name.StartsWith("__preview__")) return lClip;
            Debug.LogWarning($"[Build] characters: no clip {pName}");
            return null;
        }

        // ---------------------------------------------------------------- masks

        public const string UPPER_MASK = OUT + "/M_UpperBody.mask";
        public const string ARMS_MASK = OUT + "/M_Arms.mask";

        public static void Masks()
        {
            Mask(UPPER_MASK, AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK);
            Mask(ARMS_MASK, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
                AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK);
        }

        private static void Mask(string pPath, params AvatarMaskBodyPart[] pParts)
        {
            AvatarMask lMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(pPath);
            if (lMask == null)
            {
                lMask = new AvatarMask();
                AssetDatabase.CreateAsset(lMask, pPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                lMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, System.Array.IndexOf(pParts, (AvatarMaskBodyPart)i) >= 0);
            lMask.transformCount = 0;
            EditorUtility.SetDirty(lMask);
        }

        // ---------------------------------------------------------------- controllers

        private static AnimatorController NewController(string pPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(pPath) != null) AssetDatabase.DeleteAsset(pPath);
            return AnimatorController.CreateAnimatorControllerAtPath(pPath);
        }

        private static AnimatorState State(AnimatorStateMachine pMachine, string pName, Motion pMotion, Vector3 pPosition, bool pPaced = false)
        {
            AnimatorState lState = pMachine.AddState(pName, pPosition);
            lState.motion = pMotion;
            lState.writeDefaultValues = false;
            if (pPaced)
            {
                lState.speedParameter = HumanAnimator.PACE;
                lState.speedParameterActive = true;
            }
            return lState;
        }

        private static AnimatorState Clip(AnimatorStateMachine pMachine, string pState, string pClip, Vector3 pPosition, bool pPaced = false)
            => State(pMachine, pState, Clip(pClip), pPosition, pPaced);

        private static BlendTree Tree(AnimatorController pController, string pName, BlendTreeType pType, string pX, string pY)
        {
            BlendTree lTree = new BlendTree { name = pName, blendType = pType, blendParameter = pX, blendParameterY = pY ?? pX, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(lTree, pController);
            return lTree;
        }

        /// <summary>A new layer (its state machine saved in the controller), set up and returned.</summary>
        private static AnimatorStateMachine Layer(AnimatorController pController, string pName, float pWeight, AvatarMask pMask, AnimatorLayerBlendingMode pMode)
        {
            pController.AddLayer(pName);
            AnimatorControllerLayer[] lLayers = pController.layers;
            AnimatorControllerLayer lLayer = lLayers[lLayers.Length - 1];
            lLayer.defaultWeight = pWeight;
            lLayer.avatarMask = pMask;
            lLayer.blendingMode = pMode;
            pController.layers = lLayers;
            return lLayer.stateMachine;
        }

        private static void Transition(AnimatorState pFrom, AnimatorState pTo, float pExitTime, float pDuration)
        {
            AnimatorStateTransition lTransition = pFrom.AddTransition(pTo);
            lTransition.hasExitTime = true;
            lTransition.exitTime = pExitTime;
            lTransition.duration = pDuration;
            lTransition.hasFixedDuration = true;
        }

        /// <summary>
        /// Third-person controller. The states are driven by <see cref="HumanAnimator"/> through
        /// cross-fades (no parameter-driven transition graph to get stuck in): base layer for the
        /// body, an upper-body override layer for weapons and actions, an additive layer for hits.
        /// Locomotion trees are laid out in gait space (walk ring 1, run ring 2, sprint 3) and the
        /// pace parameter matches each clip's playback to the real ground speed.
        /// </summary>
        public static void HumanController()
        {
            AnimatorController lController = NewController(OUT + "/Human.controller");
            foreach (string lFloat in new[] { HumanAnimator.MOVE_X, HumanAnimator.MOVE_Z, HumanAnimator.PACE, HumanAnimator.HANG_X, HumanAnimator.SWIM })
                lController.AddParameter(lFloat, AnimatorControllerParameterType.Float);
            AnimatorControllerParameter[] lParameters = lController.parameters;
            foreach (AnimatorControllerParameter lParameter in lParameters) if (lParameter.name == HumanAnimator.PACE) lParameter.defaultFloat = 1f;
            lController.parameters = lParameters;

            AnimatorControllerLayer[] lLayers = lController.layers;
            lLayers[0].name = "Body";
            lLayers[0].iKPass = true;
            lController.layers = lLayers;
            AnimatorStateMachine lBody = lController.layers[0].stateMachine;

            BlendTree lLoco = Tree(lController, "Locomotion", BlendTreeType.FreeformDirectional2D, HumanAnimator.MOVE_X, HumanAnimator.MOVE_Z);
            lLoco.AddChild(Clip("Idle"), Vector2.zero);
            lLoco.AddChild(Clip("Walk_F"), new Vector2(0, 1));
            lLoco.AddChild(Clip("Walk_B"), new Vector2(0, -1));
            lLoco.AddChild(Clip("Walk_L"), new Vector2(-1, 0));
            lLoco.AddChild(Clip("Walk_R"), new Vector2(1, 0));
            lLoco.AddChild(Clip("Run_F"), new Vector2(0, 2));
            // The backpedal clip is slower than the walk back: the walk back, played faster, is the back run.
            lLoco.AddChild(Clip("Walk_B"), new Vector2(0, -2));
            lLoco.AddChild(Clip("Run_L"), new Vector2(-2, 0));
            lLoco.AddChild(Clip("Run_R"), new Vector2(2, 0));
            lLoco.AddChild(Clip("Sprint_F"), new Vector2(0, 3));
            AnimatorState lLocomotion = State(lBody, HumanAnimator.S_LOCOMOTION, lLoco, new Vector3(300, 0), true);
            lBody.defaultState = lLocomotion;

            BlendTree lCrouch = Tree(lController, "Crouch", BlendTreeType.FreeformDirectional2D, HumanAnimator.MOVE_X, HumanAnimator.MOVE_Z);
            lCrouch.AddChild(Clip("CrouchIdle"), Vector2.zero);
            lCrouch.AddChild(Clip("CrouchWalk_F"), new Vector2(0, 1));
            lCrouch.AddChild(Clip("CrouchWalk_B"), new Vector2(0, -1));
            lCrouch.AddChild(Clip("CrouchWalk_L"), new Vector2(-1, 0));
            lCrouch.AddChild(Clip("CrouchWalk_R"), new Vector2(1, 0));
            State(lBody, HumanAnimator.S_CROUCH, lCrouch, new Vector3(300, 60), true);

            BlendTree lHang = Tree(lController, "Hang", BlendTreeType.Simple1D, HumanAnimator.HANG_X, null);
            lHang.AddChild(Clip("Shimmy_L"), -1f);
            lHang.AddChild(Clip("Hang_Idle"), 0f);
            lHang.AddChild(Clip("Shimmy_R"), 1f);
            State(lBody, HumanAnimator.S_HANG, lHang, new Vector3(600, 0), true);

            BlendTree lSwim = Tree(lController, "Swim", BlendTreeType.Simple1D, HumanAnimator.SWIM, null);
            lSwim.AddChild(Clip("Swim_Idle"), 0f);
            lSwim.AddChild(Clip("Swim_F"), 1f);
            State(lBody, HumanAnimator.S_SWIM, lSwim, new Vector3(600, 60), true);

            Clip(lBody, HumanAnimator.S_CRAWL, "Crawl_F", new Vector3(300, 120), true);
            Clip(lBody, HumanAnimator.S_SLIDE, "Slide", new Vector3(300, 180));
            AnimatorState lJump = Clip(lBody, HumanAnimator.S_JUMP, "Jump_Start", new Vector3(0, 240));
            AnimatorState lFall = Clip(lBody, HumanAnimator.S_FALL, "Fall_Loop", new Vector3(300, 240));
            Clip(lBody, HumanAnimator.S_LAND, "Land", new Vector3(600, 240));
            Transition(lJump, lFall, 0.8f, 0.2f);
            Clip(lBody, HumanAnimator.S_VAULT, "Vault", new Vector3(0, 300));
            Clip(lBody, HumanAnimator.S_MANTLE, "Mantle", new Vector3(300, 300));
            Clip(lBody, HumanAnimator.S_CLIMB_UP, "Hang_ClimbUp", new Vector3(600, 300));
            Clip(lBody, HumanAnimator.S_LADDER, "Climb_Ladder", new Vector3(0, 360), true);
            Clip(lBody, HumanAnimator.S_PHONE, "Talk_Phone", new Vector3(0, 420));
            Clip(lBody, HumanAnimator.S_SMOKE, "Smoke_Idle", new Vector3(300, 420));
            Clip(lBody, HumanAnimator.S_SIT, "Sit_Chair", new Vector3(600, 420));
            Clip(lBody, HumanAnimator.S_LEAN, "Lean_Wall", new Vector3(0, 480));
            Clip(lBody, HumanAnimator.S_LOOK_AROUND, "Look_Around", new Vector3(300, 480));
            Clip(lBody, HumanAnimator.S_COWER, "Cower", new Vector3(600, 480));
            Clip(lBody, HumanAnimator.S_PANIC, "Run_Panic", new Vector3(0, 540), true);
            Clip(lBody, HumanAnimator.S_PATROL, "Patrol_Walk", new Vector3(300, 540), true);
            Clip(lBody, HumanAnimator.S_ARMED_IDLE, "Idle_Armed", new Vector3(600, 540));
            Clip(lBody, HumanAnimator.S_TAKEDOWN_ATTACKER, "Takedown_Attacker", new Vector3(0, 600));
            Clip(lBody, HumanAnimator.S_TAKEDOWN_VICTIM, "Takedown_Victim", new Vector3(300, 600));
            Clip(lBody, HumanAnimator.S_DEATH_F, "Death_F", new Vector3(0, 660));
            Clip(lBody, HumanAnimator.S_DEATH_B, "Death_B", new Vector3(300, 660));

            // Upper body: weapon holds, aims and actions over whatever the legs do.
            AnimatorStateMachine lUpper = Layer(lController, "Upper", 0f, AssetDatabase.LoadAssetAtPath<AvatarMask>(UPPER_MASK), AnimatorLayerBlendingMode.Override);
            lUpper.defaultState = State(lUpper, HumanAnimator.S_EMPTY, null, new Vector3(300, 0));
            Clip(lUpper, HumanAnimator.S_PISTOL_READY, "Idle_Armed", new Vector3(0, 60));
            Clip(lUpper, HumanAnimator.S_RIFLE_READY, "Patrol_Walk", new Vector3(300, 60));
            Clip(lUpper, HumanAnimator.S_AIM_PISTOL, "Aim_Pistol", new Vector3(0, 120));
            Clip(lUpper, HumanAnimator.S_AIM_RIFLE, "Aim_Rifle", new Vector3(300, 120));
            Clip(lUpper, HumanAnimator.S_FIRE_PISTOL, "Fire_Pistol", new Vector3(0, 180));
            Clip(lUpper, HumanAnimator.S_FIRE_RIFLE, "Fire_Rifle", new Vector3(300, 180));
            Clip(lUpper, HumanAnimator.S_RELOAD, "Reload", new Vector3(600, 180));
            Clip(lUpper, HumanAnimator.S_PUNCH, "Punch", new Vector3(0, 240));
            Clip(lUpper, HumanAnimator.S_THROW, "Throw", new Vector3(300, 240));
            Clip(lUpper, HumanAnimator.S_PHONE, "Talk_Phone", new Vector3(600, 240));
            Clip(lUpper, HumanAnimator.S_PANIC, "Run_Panic", new Vector3(0, 300), true);

            // Hits: additive flinches over everything.
            AnimatorStateMachine lHit = Layer(lController, "Hit", 1f, null, AnimatorLayerBlendingMode.Additive);
            AnimatorState lNone = State(lHit, HumanAnimator.S_EMPTY, null, new Vector3(300, 0));
            lHit.defaultState = lNone;
            Transition(Clip(lHit, HumanAnimator.S_HIT_F, "HitReact_F", new Vector3(0, 60)), lNone, 0.95f, 0.05f);
            Transition(Clip(lHit, HumanAnimator.S_HIT_B, "HitReact_B", new Vector3(300, 60)), lNone, 0.95f, 0.05f);
            EditorUtility.SetDirty(lController);
        }

        /// <summary>
        /// First-person arms: the arms-only mask, so the shoulders never move; the clip gives the
        /// fingers and the elbows, the viewmodel's IK puts the hands on the item.
        /// </summary>
        public static void ArmsController()
        {
            AnimatorController lController = NewController(OUT + "/Arms.controller");
            lController.AddParameter(HumanAnimator.PACE, AnimatorControllerParameterType.Float);
            AnimatorControllerLayer[] lLayers = lController.layers;
            lLayers[0].name = "Arms";
            lLayers[0].iKPass = true;
            lLayers[0].avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ARMS_MASK);
            lController.layers = lLayers;
            AnimatorStateMachine lArms = lController.layers[0].stateMachine;
            lArms.defaultState = Clip(lArms, HumanAnimator.S_AIM_PISTOL, "Aim_Pistol", new Vector3(300, 0));
            Clip(lArms, HumanAnimator.S_AIM_RIFLE, "Aim_Rifle", new Vector3(300, 60));
            Clip(lArms, HumanAnimator.S_PISTOL_READY, "Idle_Armed", new Vector3(0, 60));
            Clip(lArms, HumanAnimator.S_FIRE_PISTOL, "Fire_Pistol", new Vector3(0, 120));
            Clip(lArms, HumanAnimator.S_FIRE_RIFLE, "Fire_Rifle", new Vector3(300, 120));
            Clip(lArms, HumanAnimator.S_RELOAD, "Reload", new Vector3(600, 120), true);
            Clip(lArms, HumanAnimator.S_PUNCH, "Punch", new Vector3(0, 180), true);
            Clip(lArms, HumanAnimator.S_THROW, "Throw", new Vector3(300, 180), true);
            Clip(lArms, HumanAnimator.S_LOCOMOTION, "Idle", new Vector3(600, 0));
            EditorUtility.SetDirty(lController);
        }

        // ---------------------------------------------------------------- grips

        /// <summary>Rake of a pistol grip: the handle leans back this much from vertical.</summary>
        private const float GRIP_RAKE = 18f;

        /// <summary>
        /// The grips, from the hand's own geometry (the skeleton is shared, so the numbers hold for
        /// every body): the knuckle line lies along the handle (pinky low, index high, raked back),
        /// the wrist-to-knuckles axis along the barrel, the palm against the handle - so the clip's
        /// curled fingers wrap round it. Long guns: the same firing hand; the support hand under the
        /// fore-end, palm up, knuckle line along the barrel. Pistols: the support hand cups the firing
        /// hand from the left. Small items sit between the thumb and the first fingers.
        /// </summary>
        public static void Calibrate()
        {
            HumanRig lRig = EditorAssets.LoadOrCreate<HumanRig>(OUT + "/HumanRig.asset");
            GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx($"{DIR}/{REFERENCE}"));
            GameObject lInstance = Object.Instantiate(lModel);
            try
            {
                Animator lAnimator = lInstance.GetComponent<Animator>();
                HandFrame lRight = Frame(lAnimator, true), lLeft = Frame(lAnimator, false);

                // Weapon frame: +Z barrel, +Y up. The handle axis leans back by the rake.
                Vector3 lHandleUp = Quaternion.AngleAxis(GRIP_RAKE, Vector3.right) * Vector3.up;
                Quaternion lFiring = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, lHandleUp), lHandleUp);
                // Weapon in the right hand's space: hand basis maps onto the firing basis.
                Quaternion lWeaponInHand = lRight.basis * Quaternion.Inverse(lFiring);
                Vector3 lGripInHand = lRight.palm + lRight.palmNormal * 0.016f;

                // Long guns and pistols alike: the support palm up, knuckle line along the barrel, fingers
                // wrapping up the right side (a pistol: under and around the firing hand's fingers).
                Quaternion lUnder = Quaternion.LookRotation(Quaternion.AngleAxis(35f, Vector3.forward) * Vector3.right, Vector3.forward);
                Quaternion lUnderHand = lUnder * Quaternion.Inverse(lLeft.basis);
                Vector3 lCupPalm = new Vector3(-0.002f, -0.05f, 0.015f);
                lRig.pistol = new HumanRig.Grip
                {
                    handPosition = lGripInHand,
                    handRotation = lWeaponInHand,
                    supportRotation = lUnderHand,
                    supportPosition = lCupPalm - lUnderHand * (lLeft.palm + lLeft.palmNormal * 0.02f)
                };

                Vector3 lForePalm = new Vector3(0f, -0.04f, lRig.foreEnd);
                lRig.rifle = new HumanRig.Grip
                {
                    handPosition = lGripInHand,
                    handRotation = lWeaponInHand,
                    supportRotation = lUnderHand,
                    supportPosition = lForePalm - lUnderHand * (lLeft.palm + lLeft.palmNormal * 0.02f)
                };

                // The left fist of the first-person guard: the firing frame, mirrored hand.
                lRig.leftFistRotation = lFiring * Quaternion.Inverse(lLeft.basis);
                lRig.leftPalm = lLeft.palm;

                // A small item between the thumb and the index, lying along the fingers.
                Transform lHand = lAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform lIndex = lAnimator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                Transform lThumb = lAnimator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate);
                lRig.pinchPosition = lHand.InverseTransformPoint((lIndex.position + lThumb.position) * 0.5f) + lRight.palmNormal * 0.01f;
                lRig.pinchRotation = lWeaponInHand;

                Transform lHead = lAnimator.GetBoneTransform(HumanBodyBones.Head);
                lRig.eyeInHead = lHead.InverseTransformPoint(lHead.position + Vector3.up * 0.085f + Vector3.forward * 0.1f);
            }
            finally { Object.DestroyImmediate(lInstance); }
            EditorUtility.SetDirty(lRig);
        }

        private struct HandFrame
        {
            /// <summary>In the hand bone's space: up = pinky to index knuckle, forward = wrist to knuckles.</summary>
            public Quaternion basis;
            public Vector3 palm, palmNormal;
        }

        private static HandFrame Frame(Animator pAnimator, bool pRight)
        {
            Transform lHand = pAnimator.GetBoneTransform(pRight ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Vector3 lIndex = lHand.InverseTransformPoint(pAnimator.GetBoneTransform(pRight ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal).position);
            Vector3 lMiddle = lHand.InverseTransformPoint(pAnimator.GetBoneTransform(pRight ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal).position);
            Vector3 lLittle = lHand.InverseTransformPoint(pAnimator.GetBoneTransform(pRight ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal).position);
            Vector3 lUp = (lIndex - lLittle).normalized;
            Vector3 lForward = Vector3.ProjectOnPlane(lMiddle, lUp).normalized;
            Vector3 lRightAxis = Vector3.Cross(lUp, lForward);
            // A right hand's palm is on its left (-right axis) in this basis; a left hand's on its right.
            Vector3 lPalmNormal = pRight ? -lRightAxis : lRightAxis;
            return new HandFrame
            {
                basis = Quaternion.LookRotation(lForward, lUp),
                palm = Vector3.Lerp(Vector3.zero, lMiddle, 0.5f) + (lLittle - lIndex) * 0.15f,
                palmNormal = lPalmNormal
            };
        }
    }
}
