using Com.IsartDigital.GoobyrFPS.Net;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// What an agent wears: the colours of its slots (skin, top, bottom, shoes) and a silly hat made of a
    /// few flat shapes on the head bone. A propeller turns, a spring wobbles.
    /// </summary>
    public static class AgentHats
    {
        public static readonly string[] NAMES = { "No hat", "Propeller cap", "Spring hat", "Top hat", "Straw hat", "Bee veil", "Crown", "Traffic cone", "Horned helmet", "Party hat", "Chef's hat", "Bucket" };
        public static int COUNT => NAMES.Length;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;

        /// <summary>Dresses a built body. The hat is rebuilt; a tie and the like keep the team's colour.</summary>
        public static void Apply(HumanBody pBody, AgentLook pLook)
        {
            if (pBody == null || !pBody.Built) return;
            foreach (Renderer lRenderer in pBody.Renderers)
            {
                if (lRenderer == null) continue;
                Material[] lMaterials = lRenderer.sharedMaterials;
                for (int i = 0; i < lMaterials.Length; i++)
                {
                    if (lMaterials[i] == null) continue;
                    string lName = lMaterials[i].name;
                    if (lName.EndsWith("_Skin")) Tint(lRenderer, i, AgentLook.SkinColor(pLook.skin));
                    else if (lName.EndsWith("_EarIn"))
                    {
                        Color lSkin = AgentLook.SkinColor(pLook.skin);
                        Tint(lRenderer, i, new Color(lSkin.r * 0.86f, lSkin.g * 0.62f, lSkin.b * 0.62f));
                    }
                    else if (lName.EndsWith("_Top")) Tint(lRenderer, i, AgentLook.TopColor(pLook.top));
                    else if (lName.EndsWith("_Bottom")) Tint(lRenderer, i, AgentLook.BottomColor(pLook.bottom));
                    else if (lName.EndsWith("_Shoes")) Tint(lRenderer, i, AgentLook.ShoesColor(pLook.shoes));
                }
            }
            BuildHat(pBody, pLook.hat);
        }

        private static void Tint(Renderer pRenderer, int pIndex, Color pColor)
        {
            _block ??= new MaterialPropertyBlock();
            pRenderer.GetPropertyBlock(_block, pIndex);
            _block.SetColor(BaseColorId, new Color(pColor.r, pColor.g, pColor.b, 1f));
            pRenderer.SetPropertyBlock(_block, pIndex);
        }

        private static void BuildHat(HumanBody pBody, int pHat)
        {
            if (pBody.Head == null) return;
            Transform lOld = pBody.Head.Find("Hat");
            if (lOld != null) { lOld.gameObject.SetActive(false); Object.Destroy(lOld.gameObject); }
            if (pHat <= 0 || pHat >= NAMES.Length) return;

            Transform lHat = new GameObject("Hat").transform;
            lHat.SetParent(pBody.Head, false);
            // Level whatever the bone's axes: a local frame with y up and z forward at the top of the skull.
            lHat.localPosition = pBody.Head.InverseTransformPoint(pBody.Head.position + pBody.transform.up * 0.245f);
            lHat.rotation = pBody.transform.rotation;
            float lScale = 1f / Mathf.Max(0.01f, pBody.Head.lossyScale.y);
            lHat.localScale = Vector3.one * lScale;
            Build(lHat, pHat);
            Core.GameLayers.SetRecursive(lHat.gameObject, pBody.gameObject.layer);
            bool lShadowOnly = pBody.ShadowOnly;
            foreach (Renderer lRenderer in lHat.GetComponentsInChildren<Renderer>())
                lRenderer.shadowCastingMode = lShadowOnly ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static Mesh Tube => LowPolyMesh.Cached("h.tube", () => LowPolyMesh.Cylinder(10));
        private static Mesh Cup => LowPolyMesh.Cached("h.cup", () => LowPolyMesh.Cylinder(10, 0.8f));
        private static Mesh Cone => LowPolyMesh.Cached("h.cone", () => LowPolyMesh.Cone(10));
        private static Mesh Ball => LowPolyMesh.Cached("w.sphere", () => LowPolyMesh.Icosphere(1));
        private static Mesh Box => LowPolyMesh.Cached("w.box", () => LowPolyMesh.Box());

        private static void Part(Transform pHat, Mesh pMesh, Vector3 pAt, Vector3 pSize, Color pColor, Vector3 pTurn = default)
        {
            GameObject lPart = GameArt.Part(pHat, "Piece", pMesh, pAt, pSize, pColor);
            lPart.transform.localRotation = Quaternion.Euler(pTurn);
        }

        private static void Build(Transform p, int pHat)
        {
            switch (pHat)
            {
                case 1: // Propeller cap
                {
                    Color lRed = new Color(0.9f, 0.25f, 0.22f), lBlue = new Color(0.25f, 0.5f, 0.9f), lYellow = new Color(0.98f, 0.82f, 0.25f);
                    Part(p, Ball, new Vector3(0, -0.02f, 0), new Vector3(0.31f, 0.16f, 0.31f), lRed);
                    Part(p, Box, new Vector3(0, -0.04f, 0.17f), new Vector3(0.2f, 0.015f, 0.13f), lBlue);
                    Part(p, Tube, new Vector3(0, 0.08f, 0), new Vector3(0.02f, 0.06f, 0.02f), lYellow);
                    Transform lBlades = new GameObject("Blades").transform;
                    lBlades.SetParent(p, false);
                    lBlades.localPosition = new Vector3(0, 0.11f, 0);
                    Part(lBlades, Box, new Vector3(0.09f, 0, 0), new Vector3(0.18f, 0.008f, 0.05f), lYellow, new Vector3(16f, 0, 0));
                    Part(lBlades, Box, new Vector3(-0.09f, 0, 0), new Vector3(0.18f, 0.008f, 0.05f), lBlue, new Vector3(-16f, 0, 0));
                    lBlades.gameObject.AddComponent<HatSpin>();
                    break;
                }
                case 2: // Spring hat
                {
                    Transform lSpring = new GameObject("Spring").transform;
                    lSpring.SetParent(p, false);
                    for (int i = 0; i < 7; i++) Part(lSpring, Tube, new Vector3(0.012f * ((i & 1) * 2 - 1), 0.015f * i, 0), new Vector3(0.07f, 0.012f, 0.07f), new Color(0.75f, 0.75f, 0.8f));
                    Part(lSpring, Tube, new Vector3(0, 0.16f, 0), new Vector3(0.2f, 0.11f, 0.2f), new Color(0.3f, 0.75f, 0.45f));
                    Part(lSpring, Tube, new Vector3(0, 0.105f, 0), new Vector3(0.3f, 0.015f, 0.3f), new Color(0.3f, 0.75f, 0.45f));
                    lSpring.gameObject.AddComponent<HatSpring>();
                    break;
                }
                case 3: // Top hat
                    Part(p, Tube, new Vector3(0, -0.02f, 0), new Vector3(0.42f, 0.018f, 0.42f), new Color(0.12f, 0.12f, 0.15f));
                    Part(p, Tube, new Vector3(0, 0.13f, 0), new Vector3(0.25f, 0.3f, 0.25f), new Color(0.12f, 0.12f, 0.15f));
                    Part(p, Tube, new Vector3(0, 0.02f, 0), new Vector3(0.258f, 0.05f, 0.258f), new Color(0.85f, 0.25f, 0.25f));
                    break;
                case 4: // Straw hat
                    Part(p, Tube, new Vector3(0, -0.015f, 0), new Vector3(0.62f, 0.016f, 0.62f), new Color(0.93f, 0.8f, 0.48f));
                    Part(p, Cup, new Vector3(0, 0.05f, 0), new Vector3(0.28f, 0.12f, 0.28f), new Color(0.93f, 0.8f, 0.48f));
                    Part(p, Tube, new Vector3(0, 0.0f, 0), new Vector3(0.285f, 0.035f, 0.285f), new Color(0.3f, 0.6f, 0.75f));
                    break;
                case 5: // Bee veil
                    Part(p, Tube, new Vector3(0, -0.01f, 0), new Vector3(0.52f, 0.015f, 0.52f), new Color(0.94f, 0.92f, 0.84f));
                    Part(p, Cup, new Vector3(0, 0.05f, 0), new Vector3(0.27f, 0.11f, 0.27f), new Color(0.94f, 0.92f, 0.84f));
                    Part(p, Ball, new Vector3(0.1f, 0.14f, 0.06f), new Vector3(0.05f, 0.04f, 0.04f), new Color(0.98f, 0.78f, 0.2f));
                    break;
                case 6: // Crown
                    Part(p, Tube, new Vector3(0, 0.0f, 0), new Vector3(0.26f, 0.06f, 0.26f), new Color(0.98f, 0.8f, 0.25f));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Part(p, Cone, new Vector3(Mathf.Cos(a) * 0.115f, 0.03f, Mathf.Sin(a) * 0.115f), new Vector3(0.07f, 0.09f, 0.07f), new Color(0.98f, 0.8f, 0.25f));
                    }
                    break;
                case 7: // Traffic cone
                    Part(p, Box, new Vector3(0, -0.01f, 0), new Vector3(0.3f, 0.02f, 0.3f), new Color(0.96f, 0.45f, 0.14f));
                    Part(p, Cone, new Vector3(0, 0f, 0), new Vector3(0.26f, 0.42f, 0.26f), new Color(0.96f, 0.45f, 0.14f));
                    Part(p, Tube, new Vector3(0, 0.15f, 0), new Vector3(0.155f, 0.05f, 0.155f), new Color(0.95f, 0.95f, 0.92f));
                    break;
                case 8: // Horned helmet
                    Part(p, Ball, new Vector3(0, -0.015f, 0), new Vector3(0.33f, 0.2f, 0.33f), new Color(0.55f, 0.56f, 0.6f));
                    Part(p, Cone, new Vector3(0.15f, 0.0f, 0), new Vector3(0.07f, 0.17f, 0.07f), new Color(0.95f, 0.92f, 0.82f), new Vector3(0, 0, -38f));
                    Part(p, Cone, new Vector3(-0.15f, 0.0f, 0), new Vector3(0.07f, 0.17f, 0.07f), new Color(0.95f, 0.92f, 0.82f), new Vector3(0, 0, 38f));
                    break;
                case 9: // Party hat
                    Part(p, Cone, new Vector3(0.015f, -0.01f, 0), new Vector3(0.2f, 0.32f, 0.2f), new Color(0.95f, 0.4f, 0.7f), new Vector3(0, 0, -10f));
                    Part(p, Ball, new Vector3(-0.04f, 0.31f, 0), Vector3.one * 0.06f, new Color(0.98f, 0.85f, 0.3f));
                    break;
                case 10: // Chef's hat
                    Part(p, Tube, new Vector3(0, 0.03f, 0), new Vector3(0.26f, 0.1f, 0.26f), new Color(0.96f, 0.96f, 0.94f));
                    Part(p, Ball, new Vector3(0, 0.15f, 0), new Vector3(0.36f, 0.2f, 0.36f), new Color(0.96f, 0.96f, 0.94f));
                    break;
                case 11: // Bucket
                    Part(p, Cup, new Vector3(0, 0.02f, 0), new Vector3(0.32f, 0.2f, 0.32f), new Color(0.55f, 0.6f, 0.65f), new Vector3(180f, 0, 0));
                    break;
            }
        }
    }

    /// <summary>A propeller on a hat, faster as its wearer runs.</summary>
    public class HatSpin : MonoBehaviour
    {
        private Vector3 _last;

        private void Update()
        {
            float lSpeed = (transform.position - _last).magnitude / Mathf.Max(0.001f, Time.deltaTime);
            _last = transform.position;
            transform.Rotate(0f, (180f + Mathf.Min(lSpeed, 8f) * 180f) * Time.deltaTime, 0f, Space.Self);
        }
    }

    /// <summary>A hat on a spring: it lags behind the head and bounces back.</summary>
    public class HatSpring : MonoBehaviour
    {
        private Vector3 _last, _offset, _speed;
        private bool _primed;

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 lAt = transform.parent.position;
            if (!_primed) { _last = lAt; _primed = true; }
            _speed -= (lAt - _last) * 45f;
            _last = lAt;
            _speed += (-_offset * 220f - _speed * 7f) * dt;
            _offset = Vector3.ClampMagnitude(_offset + _speed * dt, 0.09f);
            transform.position = lAt + _offset;
            float lStretch = 1f + Mathf.Clamp(_offset.y * 5f, -0.3f, 0.5f);
            transform.localScale = new Vector3(1f, lStretch, 1f);
        }
    }
}
