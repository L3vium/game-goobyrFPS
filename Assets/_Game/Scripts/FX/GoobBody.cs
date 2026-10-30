using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// A Goob: the game's original character - a soft pear-shaped body, a round head floating just
    /// above it with big googly eyes, mitten hands and stubby feet, all procedural low-poly.
    /// Players, guards and targets are all Goobs; colour, size and hat tell them apart.
    /// <see cref="GoobAnimator"/> moves the parts, <see cref="GoobRagdoll"/> turns them into
    /// physics when the Goob dies.
    /// </summary>
    public class GoobBody : MonoBehaviour
    {
        [SerializeField] private Color _bodyColor = new Color(0.9f, 0.35f, 0.35f);
        [SerializeField] private Color _skinColor = new Color(1f, 0.82f, 0.7f);
        [SerializeField] private HatStyle _hat = HatStyle.None;
        [SerializeField] private Color _hatColor = new Color(0.2f, 0.2f, 0.25f);
        [SerializeField] private float _scale = 1f;
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
        public Color BodyColor => _bodyColor;
        public Color SkinColor => _skinColor;
        public float Scale => _scale;
        public bool Built { get; private set; }

        public const float HEIGHT = 1.8f;

        private void Awake()
        {
            if (_buildOnAwake) Build();
        }

        public void Configure(Color pBody, Color pSkin, HatStyle pHat, Color pHatColor, float pScale)
        {
            _bodyColor = pBody;
            _skinColor = pSkin;
            _hat = pHat;
            _hatColor = pHatColor;
            _scale = pScale;
            if (Built) Rebuild();
        }

        public void Rebuild()
        {
            if (Root != null) Destroy(Root.gameObject);
            Built = false;
            Build();
        }

        public void Build()
        {
            if (Built) return;
            Built = true;

            Mesh lSphere = LowPolyMesh.Cached("goob.sphere", () => LowPolyMesh.Icosphere(1));
            Mesh lBlob = LowPolyMesh.Cached("goob.blob", () => LowPolyMesh.Blob(1.25f, 3));
            Mesh lHand = LowPolyMesh.Cached("goob.hand", () => LowPolyMesh.Icosphere(0, 0.08f, 7));
            Mesh lFoot = LowPolyMesh.Cached("goob.foot", () => LowPolyMesh.Icosphere(0, 0.1f, 11));

            Root = new GameObject("GoobRoot").transform;
            Root.SetParent(transform, false);
            Root.localScale = Vector3.one * _scale;

            Torso = GameArt.Part(Root, "Torso", lBlob, new Vector3(0, 0.78f, 0), new Vector3(0.72f, 0.75f, 0.6f), _bodyColor).transform;
            // A belt line breaks the blob into "shirt" and "trousers".
            GameArt.Part(Torso, "Belt", LowPolyMesh.Cached("goob.belt", () => LowPolyMesh.Cylinder(10)), new Vector3(0, -0.12f, 0), new Vector3(1.02f, 0.08f, 1.02f), _bodyColor * 0.55f);

            Head = new GameObject("Head").transform;
            Head.SetParent(Root, false);
            Head.localPosition = new Vector3(0, 1.42f, 0);
            GameArt.Part(Head, "Skull", lSphere, Vector3.zero, new Vector3(0.5f, 0.48f, 0.48f), _skinColor);
            GameArt.Part(Head, "Nose", lHand, new Vector3(0, -0.02f, 0.25f), new Vector3(0.09f, 0.08f, 0.09f), _skinColor * 0.92f);

            Pupils = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float lSide = i == 0 ? -1f : 1f;
                Transform lEye = GameArt.Part(Head, "Eye", lSphere, new Vector3(0.1f * lSide, 0.07f, 0.2f), new Vector3(0.15f, 0.17f, 0.1f), Color.white).transform;
                Pupils[i] = GameArt.Part(lEye, "Pupil", lSphere, new Vector3(0, 0, 0.42f), new Vector3(0.48f, 0.48f, 0.3f), new Color(0.08f, 0.08f, 0.1f)).transform;
            }

            HandL = GameArt.Part(Root, "HandL", lHand, new Vector3(-0.42f, 0.8f, 0.05f), Vector3.one * 0.17f, _skinColor).transform;
            HandR = GameArt.Part(Root, "HandR", lHand, new Vector3(0.42f, 0.8f, 0.05f), Vector3.one * 0.17f, _skinColor).transform;
            FootL = GameArt.Part(Root, "FootL", lFoot, new Vector3(-0.16f, 0.07f, 0.03f), new Vector3(0.19f, 0.13f, 0.27f), _bodyColor * 0.4f).transform;
            FootR = GameArt.Part(Root, "FootR", lFoot, new Vector3(0.16f, 0.07f, 0.03f), new Vector3(0.19f, 0.13f, 0.27f), _bodyColor * 0.4f).transform;

            WeaponSocket = new GameObject("WeaponSocket").transform;
            WeaponSocket.SetParent(HandR, false);
            WeaponSocket.localPosition = new Vector3(0, 0, 0.4f);
            WeaponSocket.localScale = Vector3.one / 0.17f;

            BuildHat();
            Renderers = Root.GetComponentsInChildren<Renderer>();
        }

        private void BuildHat()
        {
            if (_hat == HatStyle.None) return;
            Mesh lCyl = LowPolyMesh.Cached("goob.cyl8", () => LowPolyMesh.Cylinder(8));
            Mesh lCone = LowPolyMesh.Cached("goob.cone8", () => LowPolyMesh.Cone(8));
            Mesh lSphere = LowPolyMesh.Cached("goob.sphere", () => LowPolyMesh.Icosphere(1));
            HatTransform = new GameObject("Hat").transform;
            HatTransform.SetParent(Head, false);
            HatTransform.localPosition = new Vector3(0, 0.19f, 0);

            switch (_hat)
            {
                case HatStyle.Fedora:
                    GameArt.Part(HatTransform, "Brim", lCyl, Vector3.zero, new Vector3(0.62f, 0.03f, 0.62f), _hatColor);
                    GameArt.Part(HatTransform, "Crown", lCyl, new Vector3(0, 0.08f, 0), new Vector3(0.36f, 0.16f, 0.36f), _hatColor);
                    GameArt.Part(HatTransform, "Band", lCyl, new Vector3(0, 0.03f, 0), new Vector3(0.37f, 0.04f, 0.37f), Color.black);
                    break;
                case HatStyle.Cap:
                    GameArt.Part(HatTransform, "Dome", lSphere, new Vector3(0, -0.02f, 0), new Vector3(0.5f, 0.26f, 0.5f), _hatColor);
                    GameArt.Part(HatTransform, "Visor", lCyl, new Vector3(0, -0.03f, 0.22f), new Vector3(0.3f, 0.02f, 0.25f), _hatColor * 0.8f);
                    break;
                case HatStyle.Beret:
                    GameArt.Part(HatTransform, "Beret", lSphere, new Vector3(0.04f, 0, 0), new Vector3(0.5f, 0.14f, 0.48f), _hatColor);
                    break;
                case HatStyle.Crown:
                    GameArt.Part(HatTransform, "Crown", LowPolyMesh.Cached("goob.crown", () => LowPolyMesh.Cylinder(6, 1.2f)), new Vector3(0, 0.05f, 0), new Vector3(0.32f, 0.14f, 0.32f), new Color(1f, 0.8f, 0.2f));
                    break;
                case HatStyle.Cowboy:
                    GameArt.Part(HatTransform, "Brim", lCyl, Vector3.zero, new Vector3(0.85f, 0.03f, 0.7f), _hatColor);
                    GameArt.Part(HatTransform, "Crown", lCyl, new Vector3(0, 0.1f, 0), new Vector3(0.36f, 0.2f, 0.36f), _hatColor);
                    break;
                case HatStyle.Turban:
                    GameArt.Part(HatTransform, "Turban", lSphere, new Vector3(0, 0.02f, 0), new Vector3(0.52f, 0.3f, 0.52f), _hatColor);
                    break;
                case HatStyle.Beanie:
                    GameArt.Part(HatTransform, "Beanie", lSphere, new Vector3(0, 0, 0), new Vector3(0.5f, 0.3f, 0.5f), _hatColor);
                    GameArt.Part(HatTransform, "Pom", lSphere, new Vector3(0, 0.15f, 0), Vector3.one * 0.1f, Color.white);
                    break;
                case HatStyle.TopHat:
                    GameArt.Part(HatTransform, "Brim", lCyl, Vector3.zero, new Vector3(0.6f, 0.03f, 0.6f), _hatColor);
                    GameArt.Part(HatTransform, "Crown", lCyl, new Vector3(0, 0.17f, 0), new Vector3(0.34f, 0.34f, 0.34f), _hatColor);
                    break;
            }
            _ = lCone;
        }

        /// <summary>Owner view: the body only casts its shadow (the camera sits inside the head).</summary>
        public void SetShadowOnly(bool pShadowOnly)
        {
            if (Renderers == null) return;
            foreach (Renderer lRenderer in Renderers)
                lRenderer.shadowCastingMode = pShadowOnly ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
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
