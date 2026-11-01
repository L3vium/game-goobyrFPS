using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// A realistic human on the map: one of the skinned characters of <c>Art/Characters</c> (shared
    /// skeleton, Humanoid avatar, LOD0/LOD1), spawned from the <see cref="ArtRegistry"/> and animated
    /// by <see cref="HumanAnimator"/>. Players wear the agent's suit with a team armband; NPCs get their
    /// role's outfit (<see cref="HumanCast"/>). Holds the weapon in the right hand (socket calibrated
    /// by <see cref="HumanRig"/>), flashes on hits, hides for the owner's own view (shadow only), and
    /// hands its model over to <see cref="HumanRagdoll"/> on death.
    /// </summary>
    public class HumanBody : MonoBehaviour
    {
        [SerializeField] private string _character = HumanCast.AGENT;
        [SerializeField] private bool _buildOnAwake = true;
        [SerializeField] private AnimatorCullingMode _culling = AnimatorCullingMode.CullUpdateTransforms;

        public const float HEIGHT = 1.8f;

        public string Character => _character;
        /// <summary>Players animate even off screen (their shadow, the spectators); NPCs only when seen.</summary>
        public AnimatorCullingMode Culling { get => _culling; set { _culling = value; if (Animator != null) Animator.cullingMode = value; } }
        public Transform Model { get; private set; }
        public Animator Animator { get; private set; }
        public Transform Head { get; private set; }
        public Transform Hips { get; private set; }
        public Transform Chest { get; private set; }
        public Transform HandL { get; private set; }
        public Transform HandR { get; private set; }
        public Transform WeaponSocket { get; private set; }
        public Renderer[] Renderers { get; private set; } = new Renderer[0];
        public bool Built { get; private set; }
        public Color TeamColor { get; private set; }

        private float _scale = 1f;
        private bool _shadowOnly;
        private bool _visible = true;
        private HeldKind _socketKind = (HeldKind)255;
        private static MaterialPropertyBlock _block;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static RuntimeAnimatorController _controller;
        private static Mesh _bandMesh;

        public static RuntimeAnimatorController Controller => _controller != null ? _controller : _controller = Resources.Load<RuntimeAnimatorController>("Characters/Human");

        private void Awake()
        {
            if (_buildOnAwake) Build();
        }

        /// <summary>Which character to wear, the team armband (clear = none) and a small height variation.</summary>
        public void Configure(string pCharacter, Color pTeam, float pScale = 1f)
        {
            if (string.IsNullOrEmpty(pCharacter)) pCharacter = HumanCast.AGENT;
            bool lSame = Built && pCharacter == _character && Mathf.Approximately(pScale, _scale) && pTeam == TeamColor;
            _character = pCharacter;
            _scale = Mathf.Clamp(pScale, 0.9f, 1.1f);
            TeamColor = pTeam;
            if (lSame) return;
            if (Built) Rebuild(); else Build();
        }

        public void Rebuild()
        {
            if (Model != null)
            {
                // Hidden at once: Destroy only happens at the end of the frame (a portrait shot this frame).
                Model.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(Model.gameObject); else DestroyImmediate(Model.gameObject);
            }
            Model = null;
            Built = false;
            Build();
        }

        public void Build()
        {
            if (Built) return;
            GameObject lSource = ArtRegistry.Get(_character) ?? ArtRegistry.Get(HumanCast.AGENT);
            if (lSource == null) return;
            Built = true;

            GameObject lModel = Instantiate(lSource, transform, false);
            lModel.name = "Model";
            Model = lModel.transform;
            Model.localPosition = Vector3.zero;
            Model.localRotation = Quaternion.identity;
            Model.localScale = Vector3.one * _scale;
            Core.GameLayers.SetRecursive(lModel, gameObject.layer);

            Animator = lModel.GetComponent<Animator>();
            if (Animator == null) Animator = lModel.AddComponent<Animator>();
            Animator.runtimeAnimatorController = Controller;
            Animator.applyRootMotion = false;
            Animator.cullingMode = _culling;
            lModel.AddComponent<HumanIK>().Target = GetComponent<HumanAnimator>();

            Head = Bone(HumanBodyBones.Head);
            Hips = Bone(HumanBodyBones.Hips);
            Chest = Bone(HumanBodyBones.UpperChest) ?? Bone(HumanBodyBones.Chest);
            HandL = Bone(HumanBodyBones.LeftHand);
            HandR = Bone(HumanBodyBones.RightHand);

            WeaponSocket = new GameObject("WeaponSocket").transform;
            WeaponSocket.SetParent(HandR != null ? HandR : Model, false);
            _socketKind = (HeldKind)255;
            SetHeld(HeldKind.Pistol);

            List<Renderer> lAll = new List<Renderer>(), lNear = new List<Renderer>(), lFar = new List<Renderer>();
            foreach (SkinnedMeshRenderer lSkin in lModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                lAll.Add(lSkin);
                lSkin.updateWhenOffscreen = false;
                lSkin.skinnedMotionVectors = true;
                (lSkin.name.EndsWith("_LOD1") ? lFar : lNear).Add(lSkin);
            }
            Renderer lBand = TeamColor.a > 0.01f ? Armband(TeamColor) : null;
            if (lBand != null) lNear.Add(lBand);
            if (lFar.Count > 0)
            {
                LODGroup lLods = lModel.GetComponent<LODGroup>();
                if (lLods == null) lLods = lModel.AddComponent<LODGroup>();
                lLods.SetLODs(new[] { new LOD(0.08f, lNear.ToArray()), new LOD(0.004f, lFar.ToArray()) });
                lLods.RecalculateBounds();
            }
            foreach (Renderer lRenderer in lNear) if (!lAll.Contains(lRenderer)) lAll.Add(lRenderer);
            Renderers = lAll.ToArray();

            ApplyShadow();
            Model.gameObject.SetActive(_visible);
            HumanAnimator lDriver = GetComponent<HumanAnimator>();
            if (lDriver != null) lDriver.Bind(Animator);
        }

        private Transform Bone(HumanBodyBones pBone) => Animator != null && Animator.isHuman ? Animator.GetBoneTransform(pBone) : null;

        /// <summary>Where the held item sits: the calibrated grip for guns, between the fingers for small items.</summary>
        public void SetHeld(HeldKind pKind, bool pReset = false)
        {
            if (WeaponSocket == null || (pKind == _socketKind && !pReset)) return;
            _socketKind = pKind;
            HumanRig lRig = HumanRig.Instance;
            if (pKind == HeldKind.Throwable || pKind == HeldKind.Tool)
                WeaponSocket.SetLocalPositionAndRotation(pKind == HeldKind.Tool ? lRig.pistol.handPosition : lRig.pinchPosition, pKind == HeldKind.Tool ? lRig.pistol.handRotation : lRig.pinchRotation);
            else
            {
                HumanRig.Grip lGrip = lRig.For(pKind);
                WeaponSocket.SetLocalPositionAndRotation(lGrip.handPosition, lGrip.handRotation);
            }
        }

        /// <summary>A band in the team's colour around the left upper arm, readable at a distance.</summary>
        private Renderer Armband(Color pColor)
        {
            Transform lArm = Bone(HumanBodyBones.LeftUpperArm), lElbow = Bone(HumanBodyBones.LeftLowerArm);
            if (lArm == null || lElbow == null) return null;
            _bandMesh = BandMesh();
            GameObject lBand = new GameObject("Armband");
            lBand.layer = gameObject.layer;
            lBand.transform.SetParent(lArm, false);
            Vector3 lAlong = lArm.InverseTransformPoint(lElbow.position);
            lBand.transform.localPosition = lAlong * 0.32f;
            lBand.transform.localRotation = Quaternion.FromToRotation(Vector3.up, lAlong.normalized);
            lBand.transform.localScale = new Vector3(0.112f, 0.065f, 0.112f) / Mathf.Max(0.01f, lArm.lossyScale.x);
            lBand.AddComponent<MeshFilter>().sharedMesh = _bandMesh;
            MeshRenderer lRenderer = lBand.AddComponent<MeshRenderer>();
            Material lMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lMaterial.SetColor("_BaseColor", pColor);
            lMaterial.SetFloat("_Smoothness", 0.3f);
            lMaterial.EnableKeyword("_EMISSION");
            lMaterial.SetColor("_EmissionColor", pColor * 0.15f);
            lRenderer.sharedMaterial = lMaterial;
            return lRenderer;
        }

        /// <summary>An open ring (radius 0.5, height 1), smooth.</summary>
        public static Mesh BandMesh()
        {
            if (_bandMesh != null) return _bandMesh;
            const int N = 20;
            Vector3[] lVerts = new Vector3[(N + 1) * 2];
            Vector3[] lNormals = new Vector3[lVerts.Length];
            int[] lTris = new int[N * 6];
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                lVerts[i * 2] = n * 0.5f + Vector3.down * 0.5f;
                lVerts[i * 2 + 1] = n * 0.5f + Vector3.up * 0.5f;
                lNormals[i * 2] = lNormals[i * 2 + 1] = n;
                if (i == N) break;
                int b = i * 2;
                lTris[i * 6] = b; lTris[i * 6 + 1] = b + 1; lTris[i * 6 + 2] = b + 3;
                lTris[i * 6 + 3] = b; lTris[i * 6 + 4] = b + 3; lTris[i * 6 + 5] = b + 2;
            }
            Mesh lMesh = new Mesh { name = "Armband", vertices = lVerts, normals = lNormals, triangles = lTris };
            lMesh.RecalculateBounds();
            _bandMesh = lMesh;
            return lMesh;
        }

        // ---------------------------------------------------------------- visibility

        /// <summary>Owner view: the body only casts its shadow (the camera sits inside the head).</summary>
        public void SetShadowOnly(bool pShadowOnly)
        {
            _shadowOnly = pShadowOnly;
            ApplyShadow();
        }

        private void ApplyShadow()
        {
            foreach (Renderer lRenderer in Renderers)
                if (lRenderer != null) lRenderer.shadowCastingMode = _shadowOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            if (WeaponSocket != null)
                foreach (Renderer lRenderer in WeaponSocket.GetComponentsInChildren<Renderer>(true))
                    if (_shadowOnly) lRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        public void SetVisible(bool pVisible)
        {
            if (_visible == pVisible) return;
            _visible = pVisible;
            if (Model != null) Model.gameObject.SetActive(pVisible);
        }

        /// <summary>The hit flash: a short white glow over every slot (emission, so textures stay).</summary>
        public void Flash(float pAmount)
        {
            _block ??= new MaterialPropertyBlock();
            Color lGlow = Color.white * (Mathf.Clamp01(pAmount) * 0.55f);
            foreach (Renderer lRenderer in Renderers)
            {
                if (lRenderer == null || lRenderer.name == "Armband") continue;
                lRenderer.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, lGlow);
                lRenderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>Hands the model over (to the ragdoll): it leaves this body, which is empty afterwards.</summary>
        public Transform Detach()
        {
            if (Model == null) return null;
            Transform lModel = Model;
            Flash(0f);
            lModel.SetParent(null, true);
            HumanIK lIK = lModel.GetComponent<HumanIK>();
            if (lIK != null) Destroy(lIK);
            Model = null;
            Animator = null;
            Head = Hips = Chest = HandL = HandR = WeaponSocket = null;
            Renderers = new Renderer[0];
            Built = false;
            return lModel;
        }
    }

    /// <summary>Forwards the model Animator's IK pass to the <see cref="HumanAnimator"/> on the body.</summary>
    public class HumanIK : MonoBehaviour
    {
        public HumanAnimator Target { get; set; }
        private void OnAnimatorIK(int pLayer) { if (Target != null) Target.OnIK(pLayer); }
    }
}
