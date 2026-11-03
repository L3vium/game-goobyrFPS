using System.Collections.Generic;
using System.IO;
using System.Text;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// G2 check of the new cast (<c>Goobyr/Cast/G2 Gallery</c>), all in a preview scene (no open scene
    /// touched): every clip on the hero and the three bases as frame strips (Logs/cast_g2/strips),
    /// close-ups of the gloved hand holding a pistol and a long gun, and an automated report
    /// (Logs/cast_g2/report.json): hand-into-body penetration per clip and body (raw clip and with the
    /// runtime push-out), stance-foot slide of the locomotion clips at their nominal speed, and ten
    /// dropped ragdolls asleep within 6 s.
    /// </summary>
    public static class CastGallery
    {
        /// <summary>The hero, the three bases, and the clips' reference body as a control.</summary>
        public static readonly string[] BODIES = { "Chr_Rook", "Chr_Base_Slim", "Chr_Base_Average", "Chr_Base_Heavy", CharacterBuilder.REFERENCE };
        private const string OUT_DIR = "Logs/cast_g2";
        private const int FRAMES = 6, THUMB_W = 150, THUMB_H = 200;

        [System.Serializable] private class ClipInfo { public string name; public string file; public bool loop; public bool inPlace; public float durationSec; public bool upperBodyOnly; }
        [System.Serializable] private class ClipList { public ClipInfo[] clips; }

        [MenuItem("Goobyr/Cast/G2 Gallery")]
        public static void Run()
        {
            Directory.CreateDirectory(OUT_DIR + "/strips");
            StringBuilder lReport = new StringBuilder("{\n");
            ClipInfo[] lClips = JsonUtility.FromJson<ClipList>("{\"clips\":" + File.ReadAllText(CharacterBuilder.ANIM_DIR + "/clips.json") + "}").clips;
            Scene lScene = EditorSceneManager.NewPreviewScene();
            // Shaders compile synchronously, or the shots show the cyan async-compilation placeholder.
            bool lAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                Light(lScene);
                Camera lCamera = NewCamera(lScene);
                lReport.Append("\"bodies\": [\n");
                bool lFirst = true;
                foreach (string lName in BODIES)
                {
                    GameObject lSource = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterBuilder.DIR}/{lName}/{lName}.fbx");
                    if (lSource == null) { Debug.LogWarning($"[G2] no {lName}"); continue; }
                    if (!lFirst) lReport.Append(",\n");
                    lFirst = false;
                    lReport.Append(Body(lScene, lCamera, lSource, lClips));
                }
                lReport.Append("\n],\n");
                lReport.Append("\"grips\": ").Append(Grips(lScene, lCamera)).Append(",\n");
                lReport.Append("\"ragdolls\": ").Append(Ragdolls(lScene)).Append("\n}\n");
            }
            finally { EditorSceneManager.ClosePreviewScene(lScene); ShaderUtil.allowAsyncCompilation = lAsync; }
            File.WriteAllText(OUT_DIR + "/report.json", lReport.ToString());
            Debug.Log($"[G2] gallery and report written to {Path.GetFullPath(OUT_DIR)}");
        }

        // ---------------------------------------------------------------- scene helpers

        private static void Light(Scene pScene)
        {
            GameObject lSun = new GameObject("Sun");
            SceneManager.MoveGameObjectToScene(lSun, pScene);
            Light lLight = lSun.AddComponent<Light>();
            lLight.type = LightType.Directional;
            lLight.intensity = 1.3f;
            lLight.shadows = LightShadows.Soft;
            lSun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            GameObject lFill = new GameObject("Fill");
            SceneManager.MoveGameObjectToScene(lFill, pScene);
            Light lF = lFill.AddComponent<Light>();
            lF.type = LightType.Directional;
            lF.intensity = 0.45f;
            lFill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
        }

        private static Camera NewCamera(Scene pScene)
        {
            GameObject lGo = new GameObject("G2Camera");
            SceneManager.MoveGameObjectToScene(lGo, pScene);
            Camera lCamera = lGo.AddComponent<Camera>();
            lCamera.scene = pScene;
            lCamera.cameraType = CameraType.Preview;
            lCamera.clearFlags = CameraClearFlags.SolidColor;
            lCamera.backgroundColor = new Color(0.86f, 0.85f, 0.83f);
            lCamera.nearClipPlane = 0.05f;
            lCamera.farClipPlane = 30f;
            lCamera.enabled = false;
            return lCamera;
        }

        private static Texture2D Shoot(Camera pCamera, int pWidth, int pHeight)
        {
            RenderTexture lTarget = RenderTexture.GetTemporary(pWidth, pHeight, 24, RenderTextureFormat.ARGB32);
            lTarget.antiAliasing = 1;
            pCamera.targetTexture = lTarget;
            pCamera.Render();
            RenderTexture lOld = RenderTexture.active;
            RenderTexture.active = lTarget;
            Texture2D lShot = new Texture2D(pWidth, pHeight, TextureFormat.RGB24, false);
            lShot.ReadPixels(new Rect(0, 0, pWidth, pHeight), 0, 0);
            lShot.Apply();
            RenderTexture.active = lOld;
            pCamera.targetTexture = null;
            RenderTexture.ReleaseTemporary(lTarget);
            return lShot;
        }

        private static GameObject Spawn(Scene pScene, GameObject pSource, Vector3 pPosition)
        {
            GameObject lGo = Object.Instantiate(pSource, pPosition, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(lGo, pScene);
            Animator lAnimator = lGo.GetComponent<Animator>() ?? lGo.AddComponent<Animator>();
            lAnimator.runtimeAnimatorController = null;
            lAnimator.applyRootMotion = false;
            foreach (SkinnedMeshRenderer lSkin in lGo.GetComponentsInChildren<SkinnedMeshRenderer>())
                lSkin.enabled = !lSkin.name.EndsWith("_LOD1") && !lSkin.name.EndsWith("_LOD2");
            return lGo;
        }

        /// <summary>
        /// Static copies of a model's skinned meshes, re-baked on demand: a synchronous Camera.Render in
        /// the editor would otherwise draw the skins in their last player-loop pose (the bind pose).
        /// </summary>
        private sealed class Proxies : System.IDisposable
        {
            private readonly List<(SkinnedMeshRenderer skin, GameObject go, Mesh mesh)> _items = new List<(SkinnedMeshRenderer, GameObject, Mesh)>();
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public Proxies(GameObject pRoot, Scene pScene)
            {
                foreach (SkinnedMeshRenderer lSkin in pRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!lSkin.enabled) continue;
                    GameObject lGo = new GameObject("Proxy_" + lSkin.name);
                    SceneManager.MoveGameObjectToScene(lGo, pScene);
                    Mesh lMesh = new Mesh();
                    lGo.AddComponent<MeshFilter>().sharedMesh = lMesh;
                    MeshRenderer lRenderer = lGo.AddComponent<MeshRenderer>();
                    lRenderer.sharedMaterials = lSkin.sharedMaterials;
                    lSkin.forceRenderingOff = true;
                    _items.Add((lSkin, lGo, lMesh));
                    Renderers.Add(lRenderer);
                }
            }
            public void Update()
            {
                foreach ((SkinnedMeshRenderer lSkin, GameObject lGo, Mesh lMesh) in _items)
                {
                    lSkin.BakeMesh(lMesh, true);
                    lMesh.RecalculateBounds();
                    lGo.transform.SetPositionAndRotation(lSkin.transform.position, lSkin.transform.rotation);
                }
            }
            public void Dispose()
            {
                foreach ((SkinnedMeshRenderer lSkin, GameObject lGo, Mesh lMesh) in _items)
                {
                    if (lSkin != null) lSkin.forceRenderingOff = false;
                    Object.DestroyImmediate(lGo);
                    Object.DestroyImmediate(lMesh);
                }
            }
        }

        /// <summary>
        /// A clip sampled on a model through the editor's AnimationMode (humanoid retargeting included;
        /// works in preview scenes, unlike a manually evaluated PlayableGraph). Disposing restores the pose.
        /// </summary>
        private sealed class Pose : System.IDisposable
        {
            private readonly GameObject _go;
            private readonly AnimationClip _clip;
            private readonly bool _started;
            public Pose(Animator pAnimator, AnimationClip pClip)
            {
                _go = pAnimator.gameObject;
                _clip = pClip;
                _started = !AnimationMode.InAnimationMode();
                if (_started) AnimationMode.StartAnimationMode();
            }
            public void At(float pTime)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(_go, _clip, pTime);
                AnimationMode.EndSampling();
            }
            public void Dispose() { if (_started && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode(); }
        }

        // ---------------------------------------------------------------- clips: strips, penetration, foot slide

        private static readonly Dictionary<string, (float speed, Vector3 dir)> LOCOMOTION = new Dictionary<string, (float, Vector3)>
        {
            { "Walk_F", (1.29f, Vector3.forward) }, { "Walk_B", (1.61f, Vector3.back) }, { "Walk_L", (1.70f, Vector3.left) }, { "Walk_R", (1.70f, Vector3.right) },
            { "Run_F", (3.05f, Vector3.forward) }, { "Run_L", (1.90f, Vector3.left) }, { "Run_R", (1.90f, Vector3.right) }, { "Sprint_F", (3.79f, Vector3.forward) },
            { "CrouchWalk_F", (1.12f, Vector3.forward) }, { "CrouchWalk_B", (1.12f, Vector3.back) }, { "CrouchWalk_L", (0.9f, Vector3.left) }, { "CrouchWalk_R", (0.9f, Vector3.right) },
            { "Run_Panic", (3.07f, Vector3.forward) },
        };
        private const float REF_SCALE = 1.074f;

        private static string Body(Scene pScene, Camera pCamera, GameObject pSource, ClipInfo[] pClips)
        {
            GameObject lGo = Spawn(pScene, pSource, Vector3.zero);
            Animator lAnimator = lGo.GetComponent<Animator>();
            SkinnedMeshRenderer lMain = null;
            foreach (SkinnedMeshRenderer lSkin in lGo.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (lSkin.name == pSource.name + "_LOD0") lMain = lSkin;
            Transform lHandL = lAnimator.GetBoneTransform(HumanBodyBones.LeftHand), lHandR = lAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform lHips = lAnimator.GetBoneTransform(HumanBodyBones.Hips), lChest = lAnimator.GetBoneTransform(HumanBodyBones.UpperChest) ?? lAnimator.GetBoneTransform(HumanBodyBones.Chest);
            Transform lFootL = lAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), lFootR = lAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            Classify(lMain, out int[] lHandVerts, out int[] lTorsoVerts, out int[] lHandSide);
            Proxies lProxies = new Proxies(lGo, pScene);
            Mesh lBaked = new Mesh();
            StringBuilder lJson = new StringBuilder();
            lJson.Append($"{{\"name\":\"{pSource.name}\",\"humanScale\":{F(lAnimator.humanScale)},\"clips\":[");
            bool lFirst = true;
            float lWorstRaw = 0f, lWorstIK = 0f, lWorstSlide = 0f;
            foreach (ClipInfo lInfo in pClips)
            {
                AnimationClip lClip = CharacterBuilder.Clip(lInfo.name);
                if (lClip == null) continue;
                Texture2D lStrip = new Texture2D(THUMB_W * FRAMES, THUMB_H, TextureFormat.RGB24, false);
                float lRaw = 0f, lIK = 0f;
                using (Pose lPose = new Pose(lAnimator, lClip))
                {
                    for (int f = 0; f < FRAMES; f++)
                    {
                        float t = lClip.length * (f + 0.5f) / FRAMES;
                        lPose.At(t);
                        lProxies.Update();
                        Frame(pCamera, lProxies.Renderers);
                        Texture2D lShot = Shoot(pCamera, THUMB_W, THUMB_H);
                        lStrip.SetPixels(f * THUMB_W, 0, THUMB_W, THUMB_H, lShot.GetPixels());
                        Object.DestroyImmediate(lShot);
                    }
                    // penetration over 24 samples
                    for (int s = 0; s < 12 && lMain != null; s++)
                    {
                        lPose.At(lClip.length * (s + 0.5f) / 12f);
                        lMain.BakeMesh(lBaked, true);
                        Penetration(lBaked, lMain.transform, lHandVerts, lHandSide, lTorsoVerts, lHips, lChest, lHandL, lHandR, lGo.transform, out float lDepthRaw, out float lDepthIK);
                        lRaw = Mathf.Max(lRaw, lDepthRaw);
                        lIK = Mathf.Max(lIK, lDepthIK);
                    }
                }
                lStrip.Apply();
                File.WriteAllBytes($"{OUT_DIR}/strips/{pSource.name}__{lInfo.name}.png", lStrip.EncodeToPNG());
                Object.DestroyImmediate(lStrip);
                string lSlide = "null";
                if (LOCOMOTION.TryGetValue(lInfo.name, out (float speed, Vector3 dir) lLoco))
                {
                    float lValue = FootSlide(lAnimator, lClip, lFootL, lFootR, lLoco.speed * lAnimator.humanScale / REF_SCALE, lLoco.dir);
                    lSlide = lValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    lWorstSlide = Mathf.Max(lWorstSlide, lValue);
                }
                lWorstRaw = Mathf.Max(lWorstRaw, lRaw);
                lWorstIK = Mathf.Max(lWorstIK, lIK);
                if (!lFirst) lJson.Append(',');
                lFirst = false;
                lJson.Append($"\n  {{\"clip\":\"{lInfo.name}\",\"handPenetrationRaw\":{F(lRaw)},\"handPenetrationIK\":{F(lIK)},\"footSlide\":{lSlide}}}");
            }
            lJson.Append($"],\"worstHandPenetrationRaw\":{F(lWorstRaw)},\"worstHandPenetrationIK\":{F(lWorstIK)},\"worstFootSlide\":{F(lWorstSlide)}}}");
            Object.DestroyImmediate(lBaked);
            lProxies.Dispose();
            Object.DestroyImmediate(lGo);
            return lJson.ToString();
        }

        private static string F(float pValue) => pValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        private static void Frame(Camera pCamera, List<Renderer> pRenderers)
        {
            Bounds lBounds = new Bounds(Vector3.up * 0.9f, Vector3.one * 0.5f);
            foreach (Renderer lRenderer in pRenderers) lBounds.Encapsulate(lRenderer.bounds);
            pCamera.orthographic = true;
            pCamera.orthographicSize = Mathf.Max(lBounds.extents.y, lBounds.extents.x * THUMB_H / THUMB_W) * 1.08f;
            Vector3 lView = Quaternion.Euler(8f, 215f, 0f) * Vector3.forward;
            pCamera.transform.position = lBounds.center - lView * 8f;
            pCamera.transform.rotation = Quaternion.LookRotation(lView);
        }

        /// <summary>Vertices of the hands (mostly hand / finger bones) and of the torso (pelvis, spine).</summary>
        private static void Classify(SkinnedMeshRenderer pSkin, out int[] pHands, out int[] pTorso, out int[] pSide)
        {
            List<int> lHands = new List<int>(), lTorso = new List<int>(), lSide = new List<int>();
            if (pSkin == null) { pHands = pTorso = pSide = new int[0]; return; }
            Mesh lMesh = pSkin.sharedMesh;
            Transform[] lBones = pSkin.bones;
            BoneWeight[] lWeights = lMesh.boneWeights;
            for (int i = 0; i < lWeights.Length; i++)
            {
                string lBone = lBones[lWeights[i].boneIndex0].name;
                bool lHand = lBone.StartsWith("hand_") || lBone.StartsWith("thumb_") || lBone.StartsWith("index_") || lBone.StartsWith("middle_") || lBone.StartsWith("ring_") || lBone.StartsWith("pinky_");
                if (lHand && lWeights[i].weight0 > 0.5f) { lHands.Add(i); lSide.Add(lBone.EndsWith("_l") ? 0 : 1); }
                else if ((lBone == "pelvis" || lBone.StartsWith("spine_")) && lWeights[i].weight0 > 0.5f && i % 3 == 0) lTorso.Add(i);
            }
            pHands = lHands.ToArray();
            pTorso = lTorso.ToArray();
            pSide = lSide.ToArray();
        }

        /// <summary>
        /// Deepest hand vertex behind the torso surface (nearest torso vertex and its normal), raw and
        /// after the runtime push-out (the wrist moved out of the torso capsule like HumanAnimator does).
        /// </summary>
        private static void Penetration(Mesh pBaked, Transform pSkin, int[] pHands, int[] pSide, int[] pTorso, Transform pHips, Transform pChest, Transform pHandL, Transform pHandR, Transform pRoot, out float pRaw, out float pIK)
        {
            pRaw = pIK = 0f;
            if (pHands.Length == 0 || pTorso.Length == 0) return;
            Vector3[] lV = pBaked.vertices, lN = pBaked.normals;
            Matrix4x4 lToWorld = Matrix4x4.TRS(pSkin.position, pSkin.rotation, Vector3.one);
            float lScale = pRoot.lossyScale.y;
            Vector3 lBottom = pHips.position + Vector3.up * 0.02f * lScale, lTop = pChest != null ? pChest.position : lBottom + Vector3.up * 0.35f;
            Vector3[] lShift = { PushOut(pHandL.position, lBottom, lTop, lScale) - pHandL.position, PushOut(pHandR.position, lBottom, lTop, lScale) - pHandR.position };
            for (int h = 0; h < pHands.Length; h += 3)
            {
                Vector3 lP = lToWorld.MultiplyPoint3x4(lV[pHands[h]]);
                float lBest = float.MaxValue;
                int lNear = -1;
                for (int t = 0; t < pTorso.Length; t++)
                {
                    float d = (lToWorld.MultiplyPoint3x4(lV[pTorso[t]]) - lP).sqrMagnitude;
                    if (d < lBest) { lBest = d; lNear = t; }
                }
                if (lNear < 0 || lBest > 0.04f) continue;
                Vector3 lT = lToWorld.MultiplyPoint3x4(lV[pTorso[lNear]]), lTn = lToWorld.MultiplyVector(lN[pTorso[lNear]]);
                pRaw = Mathf.Max(pRaw, -Vector3.Dot(lP - lT, lTn));
                Vector3 lQ = lP + lShift[pSide[h]];
                pIK = Mathf.Max(pIK, -Vector3.Dot(lQ - lT, lTn));
            }
        }

        private static Vector3 PushOut(Vector3 pHand, Vector3 pBottom, Vector3 pTop, float pScale)
        {
            Vector3 lAxis = pTop - pBottom;
            float t = Mathf.Clamp01(Vector3.Dot(pHand - pBottom, lAxis) / Mathf.Max(lAxis.sqrMagnitude, 1e-4f));
            Vector3 lOnAxis = pBottom + lAxis * t, lAway = pHand - lOnAxis;
            lAway.y = 0f;
            float lRadius = 0.15f * pScale;
            if (lAway.sqrMagnitude >= lRadius * lRadius || lAway.sqrMagnitude < 1e-6f) return pHand;
            return new Vector3(lOnAxis.x, pHand.y, lOnAxis.z) + lAway.normalized * lRadius;
        }

        /// <summary>
        /// Median horizontal speed of a planted foot (within 2 cm of its lowest point in the clip) when the
        /// clip plays at its nominal ground speed: the body moves at that speed, the planted foot should not.
        /// </summary>
        private static float FootSlide(Animator pAnimator, AnimationClip pClip, Transform pFootL, Transform pFootR, float pSpeed, Vector3 pDir)
        {
            const int STEPS = 120;
            Vector3[][] lFeet = { new Vector3[STEPS + 1], new Vector3[STEPS + 1] };
            using (Pose lPose = new Pose(pAnimator, pClip))
            {
                for (int i = 0; i <= STEPS; i++)
                {
                    lPose.At(pClip.length * i / STEPS);
                    lFeet[0][i] = pAnimator.transform.InverseTransformPoint(pFootL.position);
                    lFeet[1][i] = pAnimator.transform.InverseTransformPoint(pFootR.position);
                }
            }
            float lDt = pClip.length / STEPS;
            List<float> lSpeeds = new List<float>();
            foreach (Vector3[] lFoot in lFeet)
            {
                float lLow = float.MaxValue;
                foreach (Vector3 p in lFoot) lLow = Mathf.Min(lLow, p.y);
                for (int i = 0; i < STEPS; i++)
                {
                    if (lFoot[i].y > lLow + 0.02f || lFoot[i + 1].y > lLow + 0.02f) continue;
                    Vector3 lWorld = (lFoot[i + 1] - lFoot[i]) / lDt + pDir * pSpeed;
                    lWorld.y = 0f;
                    lSpeeds.Add(lWorld.magnitude);
                }
            }
            if (lSpeeds.Count == 0) return 0f;
            lSpeeds.Sort();
            return lSpeeds[lSpeeds.Count / 2];
        }

        // ---------------------------------------------------------------- grips

        private static string Grips(Scene pScene, Camera pCamera)
        {
            GameObject lSource = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterBuilder.DIR}/Chr_Rook/Chr_Rook.fbx");
            if (lSource == null) return "null";
            StringBuilder lJson = new StringBuilder("[");
            string[] lCases = { "Aim_Pistol|W_Pistol", "Aim_Rifle|W_Smg" };
            for (int c = 0; c < lCases.Length; c++)
            {
                string[] lParts = lCases[c].Split('|');
                GameObject lGo = Spawn(pScene, lSource, Vector3.zero);
                Animator lAnimator = lGo.GetComponent<Animator>();
                AnimationClip lClip = CharacterBuilder.Clip(lParts[0]);
                Transform lHand = lAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                GameObject lWeaponSource = Data.ArtRegistry.Get(lParts[1]);
                GameObject lWeapon = null;
                if (lClip != null && lHand != null)
                {
                    Pose lPose = new Pose(lAnimator, lClip);
                    lPose.At(lClip.length * 0.5f);
                    Proxies lProxies = new Proxies(lGo, pScene);
                    lProxies.Update();
                    if (lWeaponSource != null)
                    {
                        HumanRig.Grip lGrip = HumanRig.Instance.For(c == 0 ? HeldKind.Pistol : HeldKind.Rifle);
                        GameObject lSocket = new GameObject("Socket");
                        lSocket.transform.SetParent(lHand, false);
                        lSocket.transform.SetLocalPositionAndRotation(lGrip.handPosition, lGrip.handRotation);
                        lWeapon = Object.Instantiate(lWeaponSource, lSocket.transform, false);
                    }
                    pCamera.orthographic = false;
                    pCamera.fieldOfView = 30f;
                    Vector3 lFocus = lHand.position;
                    foreach (Vector3 lFrom in new[] { new Vector3(0.55f, 0.15f, 0.45f), new Vector3(-0.35f, 0.25f, 0.6f) })
                    {
                        pCamera.transform.position = lFocus + lFrom;
                        pCamera.transform.rotation = Quaternion.LookRotation(lFocus - pCamera.transform.position);
                        Texture2D lShot = Shoot(pCamera, 900, 700);
                        File.WriteAllBytes($"{OUT_DIR}/grip_{lParts[1]}_{(lFrom.x > 0 ? "right" : "left")}.png", lShot.EncodeToPNG());
                        Object.DestroyImmediate(lShot);
                    }
                    // the whole body in the aim pose, three-quarter view
                    Frame(pCamera, lProxies.Renderers);
                    pCamera.orthographic = true;
                    Texture2D lFull = Shoot(pCamera, 600, 800);
                    File.WriteAllBytes($"{OUT_DIR}/grip_{lParts[1]}_body.png", lFull.EncodeToPNG());
                    Object.DestroyImmediate(lFull);
                    lProxies.Dispose();
                    lPose.Dispose();
                }
                lJson.Append(c > 0 ? "," : "").Append($"{{\"clip\":\"{lParts[0]}\",\"weapon\":\"{lParts[1]}\",\"found\":{(lWeaponSource != null ? "true" : "false")}}}");
                Object.DestroyImmediate(lGo);
            }
            return lJson.Append("]").ToString();
        }

        // ---------------------------------------------------------------- ragdolls

        /// <summary>Ten bodies dropped with a shove onto a floor; how many sleep within 6 s.</summary>
        private static string Ragdolls(Scene pScene)
        {
            PhysicsScene lPhysics = pScene.GetPhysicsScene();
            GameObject lFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(lFloor, pScene);
            lFloor.transform.localScale = new Vector3(60f, 1f, 60f);
            lFloor.transform.position = Vector3.down * 0.5f;
            List<HumanRagdoll> lDolls = new List<HumanRagdoll>();
            List<GameObject> lTrash = new List<GameObject> { lFloor };
            Random.InitState(7);
            string[] lNames = { "Chr_Rook", "Chr_Base_Slim", "Chr_Base_Average", "Chr_Base_Heavy" };
            for (int i = 0; i < 10; i++)
            {
                GameObject lHolder = new GameObject("Doll" + i);
                SceneManager.MoveGameObjectToScene(lHolder, pScene);
                lHolder.transform.position = new Vector3((i % 5) * 2.5f, 0.4f + (i / 5) * 0.6f, (i / 5) * 3f);
                lHolder.transform.rotation = Quaternion.Euler(0f, i * 37f, 0f);
                HumanBody lBody = lHolder.AddComponent<HumanBody>();
                lBody.Configure(lNames[i % lNames.Length], Color.clear);
                lTrash.Add(lHolder);
                if (!lBody.Built) continue;
                lBody.Animator.Update(0f);
                Vector3 lImpulse = (lHolder.transform.forward + Random.insideUnitSphere * 0.5f) * Random.Range(8f, 25f);
                HumanRagdoll lDoll = HumanRagdoll.Spawn(lBody, lImpulse, lHolder.transform.position + Vector3.up * 1.3f);
                if (lDoll == null) continue;
                SceneManager.MoveGameObjectToScene(lDoll.gameObject, pScene);
                lDolls.Add(lDoll);
                lTrash.Add(lDoll.gameObject);
            }
            int lAsleep = 0;
            float lLast = 0f, lMoved = 0f;
            List<Vector3> lStart = new List<Vector3>();
            foreach (HumanRagdoll lDoll in lDolls) lStart.Add(lDoll.Pelvis != null ? lDoll.Pelvis.position : Vector3.zero);
            bool lDefault = lPhysics == Physics.defaultPhysicsScene;
            SimulationMode lMode = Physics.simulationMode;
            if (lDefault) Physics.simulationMode = SimulationMode.Script;
            try { for (int s = 0; s < 300; s++) lPhysics.Simulate(0.02f); }
            finally { if (lDefault) Physics.simulationMode = lMode; }
            for (int i = 0; i < lDolls.Count; i++) if (lDolls[i].Pelvis != null) lMoved += (lDolls[i].Pelvis.position - lStart[i]).magnitude / lDolls.Count;
            foreach (HumanRagdoll lDoll in lDolls)
            {
                bool lSleeping = true;
                foreach (Rigidbody lRb in lDoll.GetComponentsInChildren<Rigidbody>())
                {
                    lSleeping &= lRb.IsSleeping() || lRb.linearVelocity.magnitude < 0.05f;
                    lLast = Mathf.Max(lLast, lRb.linearVelocity.magnitude);
                }
                if (lSleeping) lAsleep++;
            }
            foreach (GameObject lGo in lTrash) if (lGo != null) Object.DestroyImmediate(lGo);
            return $"{{\"dropped\":{lDolls.Count},\"asleepWithin6s\":{lAsleep},\"fastestBodyAfter6s\":{F(lLast)},\"meanPelvisTravel\":{F(lMoved)},\"localPhysics\":{(lDefault ? "false" : "true")}}}";
        }
    }
}
