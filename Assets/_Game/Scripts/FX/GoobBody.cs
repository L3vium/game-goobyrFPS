using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// A Goob: the game's original character - a soft pear-shaped body in an outfit, a round head
    /// floating just above it with big googly eyes, mitten hands and chunky shoes. Assembled from
    /// the Blender parts in <c>Resources/Goob</c> (outfit torsos, heads, hair, hats, hands, feet,
    /// accessories) following a <see cref="GoobLook"/>; the eyes stay procedural so they blink.
    /// <see cref="GoobAnimator"/> moves the parts, <see cref="GoobRagdoll"/> makes them floppy.
    /// </summary>
    public class GoobBody : MonoBehaviour
    {
        [SerializeField] private GoobLook _look = GoobLook.Default(new Color(0.9f, 0.35f, 0.35f));
        [SerializeField] private bool _buildOnAwake = true;

        public Transform Root { get; private set; }
        public Transform Torso { get; private set; }
        public Transform Head { get; private set; }
        public Transform HandL { get; private set; }
        public Transform HandR { get; private set; }
        public Transform FootL { get; private set; }
        public Transform FootR { get; private set; }
        public Transform WeaponSocket { get; private set; }
        public Transform HatTransform { get; private set; }
        public Transform[] Pupils { get; private set; }
        public Renderer[] Renderers { get; private set; }
        public Color BodyColor => _look.clothes;
        public Color SkinColor => _look.skin;
        public float Scale => _look.scale;
        public bool Built { get; private set; }
        public GoobLook Look => _look;

        /// <summary>Rest scale of the torso (the animator squashes around it).</summary>
        public Vector3 TorsoRestScale { get; private set; } = Vector3.one;
        /// <summary>Height of the foot pivots at rest (0 for the modelled shoes, sole on the ground).</summary>
        public float FootBaseY { get; private set; }

        public const float HEIGHT = 1.8f;

        private static readonly Dictionary<string, Mesh> _parts = new Dictionary<string, Mesh>();

        private void Awake()
        {
            if (_buildOnAwake) Build();
        }

        /// <summary>Old entry point (players, quick looks): a suit in the given colour and a hat.</summary>
        public void Configure(Color pBody, Color pSkin, HatStyle pHat, Color pHatColor, float pScale)
        {
            GoobLook lLook = GoobLook.Default(pBody);
            lLook.skin = pSkin;
            lLook.hat = pHat;
            lLook.hatColor = pHatColor;
            lLook.scale = pScale;
            lLook.hair = pHat == HatStyle.None ? Hair.Short : Hair.None;
            Configure(lLook);
        }

        public void Configure(GoobLook pLook)
        {
            if (pLook.scale <= 0f) pLook.scale = 1f;
            _look = pLook;
            if (Built) Rebuild();
        }

        public void Rebuild()
        {
            if (Root != null) Destroy(Root.gameObject);
            Built = false;
            Build();
        }

        /// <summary>A Blender part's mesh, or null when the part is missing (procedural fallback).</summary>
        public static Mesh PartMesh(string pName)
        {
            if (_parts.TryGetValue(pName, out Mesh lMesh)) return lMesh;
            GameObject lPrefab = Resources.Load<GameObject>("Goob/" + pName);
            MeshFilter lFilter = lPrefab != null ? lPrefab.GetComponentInChildren<MeshFilter>() : null;
            lMesh = lFilter != null ? lFilter.sharedMesh : null;
            _parts[pName] = lMesh;
            return lMesh;
        }

        /// <summary>A modelled part, turned to face +Z (parts face -Z once imported).</summary>
        private static GameObject Piece(Transform pParent, string pName, Mesh pMesh, Vector3 pLocal, Color pTint)
        {
            GameObject lPart = GameArt.Part(pParent, pName, pMesh, pLocal, Vector3.one, pTint);
            lPart.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            return lPart;
        }

        public void Build()
        {
            if (Built) return;
            Built = true;

            Mesh lSphere = LowPolyMesh.Cached("goob.sphere", () => LowPolyMesh.Icosphere(1));
            Root = new GameObject("GoobRoot").transform;
            Root.SetParent(transform, false);
            Root.localScale = Vector3.one * _look.scale;

            // Torso (the outfit).
            Mesh lTorso = PartMesh("Torso_" + _look.outfit);
            if (lTorso != null)
            {
                Torso = new GameObject("Torso").transform;
                Torso.SetParent(Root, false);
                Torso.localPosition = new Vector3(0, 0.78f, 0);
                Piece(Torso, "Outfit", lTorso, Vector3.zero, _look.clothes);
                TorsoRestScale = Vector3.one;
            }
            else
            {
                Mesh lBlob = LowPolyMesh.Cached("goob.blob", () => LowPolyMesh.Blob(1.25f, 3));
                Torso = GameArt.Part(Root, "Torso", lBlob, new Vector3(0, 0.78f, 0), new Vector3(0.72f, 0.75f, 0.6f), _look.clothes).transform;
                TorsoRestScale = new Vector3(0.72f, 0.75f, 0.6f);
            }

            // Head with procedural eyes.
            Head = new GameObject("Head").transform;
            Head.SetParent(Root, false);
            Head.localPosition = new Vector3(0, 1.42f, 0);
            Mesh lHead = PartMesh("Head_" + (char)('A' + Mathf.Clamp(_look.head, 0, 2)));
            if (lHead != null) Piece(Head, "Skull", lHead, Vector3.zero, _look.skin);
            else GameArt.Part(Head, "Skull", lSphere, Vector3.zero, new Vector3(0.5f, 0.48f, 0.48f), _look.skin);

            Pupils = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float lSide = i == 0 ? -1f : 1f;
                Transform lEye = GameArt.Part(Head, "Eye", lSphere, new Vector3(0.1f * lSide, 0.07f, 0.2f), new Vector3(0.15f, 0.17f, 0.1f), Color.white).transform;
                Pupils[i] = GameArt.Part(lEye, "Pupil", lSphere, new Vector3(0, 0, 0.42f), new Vector3(0.48f, 0.48f, 0.3f), new Color(0.06f, 0.06f, 0.08f)).transform;
            }

            Vector3 lTop = new Vector3(0, 0.24f, 0);
            if (_look.hair != Hair.None && PartMesh("Hair_" + _look.hair) is Mesh lHair) Piece(Head, "Hair", lHair, lTop, _look.hairColor);
            if (_look.facial == Facial.Beard && PartMesh("Beard_Full") is Mesh lBeard) Piece(Head, "Beard", lBeard, Vector3.zero, _look.hairColor);
            if (_look.facial == Facial.Mustache && PartMesh("Mustache") is Mesh lMustache) Piece(Head, "Mustache", lMustache, Vector3.zero, _look.hairColor);
            if (_look.sunglasses && PartMesh("Acc_Sunglasses") is Mesh lGlasses) Piece(Head, "Sunglasses", lGlasses, Vector3.zero, Color.white);
            if (_look.earpiece && PartMesh("Acc_Earpiece") is Mesh lEar) Piece(Head, "Earpiece", lEar, Vector3.zero, Color.white);

            string lHatName = _look.helmet ? "Hat_Helmet" : _look.chefHat ? "Hat_Chef" : _look.headset ? "Hat_Headset" : _look.hat != HatStyle.None ? "Hat_" + _look.hat : null;
            if (lHatName != null && PartMesh(lHatName) is Mesh lHat)
            {
                HatTransform = Piece(Head, "Hat", lHat, lTop, _look.hatColor).transform;
            }

            // Hands and feet.
            Mesh lHandMesh = PartMesh(_look.hands == HandStyle.Glove ? "Hand_Glove" : "Hand_Mitten");
            Mesh lFootMesh = PartMesh("Foot_" + _look.feet);
            Mesh lFallbackHand = LowPolyMesh.Cached("goob.hand", () => LowPolyMesh.Icosphere(0, 0.08f, 7));
            Mesh lFallbackFoot = LowPolyMesh.Cached("goob.foot", () => LowPolyMesh.Icosphere(0, 0.1f, 11));
            HandL = MakeLimb("HandL", lHandMesh, lFallbackHand, new Vector3(-0.42f, 0.8f, 0.05f), Vector3.one * 0.17f, _look.skin);
            HandR = MakeLimb("HandR", lHandMesh, lFallbackHand, new Vector3(0.42f, 0.8f, 0.05f), Vector3.one * 0.17f, _look.skin);
            FootBaseY = lFootMesh != null ? 0f : 0.07f;
            Color lShoe = _look.clothes * 0.35f;
            FootL = MakeLimb("FootL", lFootMesh, lFallbackFoot, new Vector3(-0.16f, FootBaseY, 0.03f), new Vector3(0.19f, 0.13f, 0.27f), lShoe);
            FootR = MakeLimb("FootR", lFootMesh, lFallbackFoot, new Vector3(0.16f, FootBaseY, 0.03f), new Vector3(0.19f, 0.13f, 0.27f), lShoe);

            WeaponSocket = new GameObject("WeaponSocket").transform;
            WeaponSocket.SetParent(HandR, false);
            WeaponSocket.localPosition = new Vector3(0, 0, 0.07f);
            WeaponSocket.localScale = new Vector3(1f / HandR.localScale.x, 1f / HandR.localScale.y, 1f / HandR.localScale.z);

            Renderers = Root.GetComponentsInChildren<Renderer>();
        }

        private Transform MakeLimb(string pName, Mesh pModel, Mesh pFallback, Vector3 pLocal, Vector3 pFallbackScale, Color pTint)
        {
            if (pModel == null) return GameArt.Part(Root, pName, pFallback, pLocal, pFallbackScale, pTint).transform;
            Transform lLimb = new GameObject(pName).transform;
            lLimb.SetParent(Root, false);
            lLimb.localPosition = pLocal;
            Piece(lLimb, "Mesh", pModel, Vector3.zero, pTint);
            return lLimb;
        }

        /// <summary>Owner view: the body only casts its shadow (the camera sits inside the head).</summary>
        public void SetShadowOnly(bool pShadowOnly)
        {
            if (Renderers == null) return;
            foreach (Renderer lRenderer in Renderers)
                if (lRenderer != null) lRenderer.shadowCastingMode = pShadowOnly ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        public void SetVisible(bool pVisible)
        {
            if (Root != null) Root.gameObject.SetActive(pVisible);
        }

        public void SetHeadVisible(bool pVisible)
        {
            if (Head != null) Head.gameObject.SetActive(pVisible);
        }

        public void Flash(float pAmount)
        {
            if (Renderers == null) return;
            foreach (Renderer lRenderer in Renderers) if (lRenderer != null) GameArt.SetFlash(lRenderer, pAmount);
        }
    }
}
