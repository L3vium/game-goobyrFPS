using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The arc a throwable will follow, drawn for the owner while they hold aim with a rock, a
    /// bottle, a coin... in hand: a thin dotted curve to where it lands and a ring on the spot, the
    /// same ballistics as <see cref="ThrownItem.Spawn"/>.
    /// </summary>
    public class ThrowArc : MonoBehaviour
    {
        private const int STEPS = 48;
        private const float STEP_TIME = 0.04f;

        private LineRenderer _line;
        private Transform _ring;
        private readonly Vector3[] _points = new Vector3[STEPS];

        public static ThrowArc Create()
        {
            GameObject lObject = new GameObject("ThrowArc");
            return lObject.AddComponent<ThrowArc>();
        }

        private void Awake()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.widthMultiplier = 0.035f;
            _line.numCapVertices = 2;
            _line.textureMode = LineTextureMode.Tile;
            _line.startColor = new Color(1f, 1f, 1f, 0.75f);
            _line.endColor = new Color(1f, 1f, 1f, 0.25f);
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            GameObject lRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(lRing.GetComponent<Collider>());
            lRing.transform.SetParent(transform, false);
            lRing.transform.localScale = new Vector3(0.45f, 0.005f, 0.45f);
            Renderer lRenderer = lRing.GetComponent<Renderer>();
            lRenderer.sharedMaterial = _line.material;
            lRenderer.material.color = new Color(1f, 0.85f, 0.4f, 0.55f);
            lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring = lRing.transform;
            Hide();
        }

        public void Hide()
        {
            _line.enabled = false;
            _ring.gameObject.SetActive(false);
        }

        /// <summary>Draw the arc from <paramref name="pOrigin"/> with the throw's starting velocity.</summary>
        public void Show(Vector3 pOrigin, Vector3 pVelocity)
        {
            Vector3 lPosition = pOrigin;
            Vector3 lVelocity = pVelocity;
            int lCount = 0;
            bool lHit = false;
            RaycastHit lHitInfo = default;
            _points[lCount++] = lPosition;
            while (lCount < STEPS)
            {
                Vector3 lNext = lPosition + lVelocity * STEP_TIME + 0.5f * Physics.gravity * STEP_TIME * STEP_TIME;
                lVelocity += Physics.gravity * STEP_TIME;
                Vector3 lDelta = lNext - lPosition;
                if (Physics.Raycast(lPosition, lDelta.normalized, out lHitInfo, lDelta.magnitude, GameLayers.WorldMask | GameLayers.CharacterMask, QueryTriggerInteraction.Ignore))
                {
                    _points[lCount++] = lHitInfo.point;
                    lHit = true;
                    break;
                }
                lPosition = lNext;
                _points[lCount++] = lPosition;
            }
            _line.enabled = true;
            _line.positionCount = lCount;
            _line.SetPositions(_points);
            _ring.gameObject.SetActive(lHit);
            if (lHit)
            {
                _ring.position = lHitInfo.point + lHitInfo.normal * 0.02f;
                _ring.up = lHitInfo.normal;
            }
        }
    }
}
