using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Rain around whoever is looking: a particle box of stretched drops that follows the active camera, a
    /// loop of rain on the leaves. Under a roof the drops stop and the sound drops to a murmur.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class RainFall : MonoBehaviour
    {
        private const float RATE = 1500f, HEIGHT = 12f, VOLUME = 0.5f, UNDER_ROOF = 0.3f;

        private ParticleSystem _particles;
        private AudioSource _sound;
        private float _cover;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            ParticleSystemRenderer lRenderer = GetComponent<ParticleSystemRenderer>();
            lRenderer.sharedMaterial = GameArt.ParticleAlpha;
            lRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            lRenderer.velocityScale = 0.024f;
            lRenderer.lengthScale = 1f;
            lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sound = gameObject.AddComponent<AudioSource>();
            _sound.clip = Resources.Load<AudioClip>("Sfx/rain");
            _sound.loop = true;
            _sound.spatialBlend = 0f;
            _sound.volume = 0f;
            if (_sound.clip != null) _sound.Play();
        }

        private void LateUpdate()
        {
            Camera lCamera = Camera.main;
            if (lCamera == null) return;
            transform.position = lCamera.transform.position + Vector3.up * HEIGHT;
            bool lRoof = Physics.Raycast(lCamera.transform.position + Vector3.up * 0.3f, Vector3.up, 30f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            _cover = Mathf.MoveTowards(_cover, lRoof ? 1f : 0f, Time.deltaTime * 1.2f);
            ParticleSystem.EmissionModule lEmission = _particles.emission;
            lEmission.rateOverTime = RATE * (1f - _cover);
            _sound.volume = Mathf.Lerp(VOLUME, VOLUME * UNDER_ROOF, _cover) * Audio.Sfx.Volume;
        }
    }
}
