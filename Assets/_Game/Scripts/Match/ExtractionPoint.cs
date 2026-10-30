using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Match
{
    /// <summary>
    /// An extraction (boat, helicopter pad, car, sewer): a circle on the ground with a beam of
    /// light, lit only when the match made it active and the proof is out.
    /// </summary>
    [RequireComponent(typeof(MatchPoint))]
    public class ExtractionPoint : MonoBehaviour
    {
        [SerializeField] private float _radius = 4f;

        private Transform _beam;
        private Renderer _beamRenderer, _ringRenderer;
        private int _index = -1;

        public float Radius => _radius;

        public void Setup(float pRadius) => _radius = pRadius;

        public bool Contains(Vector3 pPoint)
        {
            Vector3 lFlat = pPoint - transform.position;
            return new Vector2(lFlat.x, lFlat.z).magnitude <= _radius && Mathf.Abs(lFlat.y) < 4f;
        }

        public bool Active
        {
            get
            {
                MatchDirector lDirector = MatchDirector.Instance;
                if (lDirector == null || _index < 0) return false;
                return (lDirector.ActiveExtractions.Value & (1 << _index)) != 0;
            }
        }

        private void Start()
        {
            _beam = new GameObject("Beam").transform;
            _beam.SetParent(transform, false);
            GameObject lBeam = GameArt.Part(_beam, "Pillar", LowPolyMesh.Cached("fx.ring", () => LowPolyMesh.Cylinder(24)), new Vector3(0, 25f, 0), new Vector3(_radius * 0.5f, 50f, _radius * 0.5f), new Color(0.4f, 1f, 0.6f, 0.15f), GameArt.Transparent);
            GameObject lRing = GameArt.Part(_beam, "Ring", LowPolyMesh.Cached("fx.ring", () => LowPolyMesh.Cylinder(24)), new Vector3(0, 0.05f, 0), new Vector3(_radius * 2f, 0.04f, _radius * 2f), new Color(0.6f, 2f, 0.9f), GameArt.Glow);
            _beamRenderer = lBeam.GetComponent<Renderer>();
            _ringRenderer = lRing.GetComponent<Renderer>();
            _beamRenderer.shadowCastingMode = _ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _index = MatchPoint.OfKind(MatchPoint.Kind.Extraction).IndexOf(GetComponent<MatchPoint>());
        }

        private void Update()
        {
            MatchDirector lDirector = MatchDirector.Instance;
            bool lShow = Active && lDirector != null && lDirector.Proof.Value != ProofState.TargetAlive;
            _beam.gameObject.SetActive(lShow);
            if (!lShow) return;
            float lPulse = 0.12f + Mathf.Sin(Time.time * 2f) * 0.04f;
            GameArt.Paint(_beamRenderer, new Color(0.4f, 1f, 0.6f, lPulse));
            _beam.localRotation = Quaternion.Euler(0, Time.time * 20f, 0);
        }
    }
}
