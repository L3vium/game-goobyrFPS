using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Falling snow around whoever is looking: a small particle box (built by the map builder) that
    /// follows the active camera, so the whole map seems to snow for the cost of a few hundred flakes.
    /// Indoors (a roof over the camera) the emission fades out.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class SnowFall : MonoBehaviour
    {
        [SerializeField] private float _rate = 900f;
        private ParticleSystem _particles;
        private float _cover;

        private void Awake() => _particles = GetComponent<ParticleSystem>();

        private void LateUpdate()
        {
            Camera lCamera = Camera.main;
            if (lCamera == null) return;
            transform.position = lCamera.transform.position + Vector3.up * 6f;
            bool lRoof = Physics.Raycast(lCamera.transform.position + Vector3.up * 0.3f, Vector3.up, 30f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            _cover = Mathf.MoveTowards(_cover, lRoof ? 1f : 0f, Time.deltaTime * 0.8f);
            ParticleSystem.EmissionModule lEmission = _particles.emission;
            lEmission.rateOverTime = _rate * (1f - _cover);
        }
    }
}
