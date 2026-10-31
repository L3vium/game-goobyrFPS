using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Audio
{
    /// <summary>
    /// The bed of sound under a map: the forest morning (wind in the leaves, birds, insects), the
    /// desert (gusts, distant surf), the suburban night (crickets, far traffic). Under water it
    /// fades to the muffled underwater bed. Created whenever a map scene loads.
    /// </summary>
    public class Ambience : MonoBehaviour
    {
        private AudioSource _bed, _under;
        private float _level = 0.45f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (pScene, _) =>
            {
                string lBed = pScene.name switch
                {
                    "Map_Forest" => "amb_forest",
                    "Map_Gulf" => "amb_desert",
                    "Map_Suburb" => "amb_night",
                    _ => null
                };
                if (lBed == null) return;
                new GameObject("Ambience").AddComponent<Ambience>().Play(lBed);
            };
        }

        private void Play(string pBed)
        {
            _bed = Source(pBed);
            _under = Source("underwater");
            if (_under != null) _under.volume = 0f;
        }

        private AudioSource Source(string pClip)
        {
            AudioClip lClip = Resources.Load<AudioClip>("Sfx/" + pClip);
            if (lClip == null) return null;
            AudioSource lSource = gameObject.AddComponent<AudioSource>();
            lSource.clip = lClip;
            lSource.loop = true;
            lSource.spatialBlend = 0f;
            lSource.volume = 0f;
            lSource.time = Random.Range(0f, lClip.length);
            lSource.Play();
            return lSource;
        }

        private void Update()
        {
            bool lUnder = PlayerAvatar.Local != null && PlayerAvatar.Local.Motor != null && PlayerAvatar.Local.Motor.Underwater;
            float dt = Time.unscaledDeltaTime;
            if (_bed != null) _bed.volume = Mathf.MoveTowards(_bed.volume, (lUnder ? 0.05f : _level) * Sfx.Volume, dt * 0.6f);
            if (_under != null) _under.volume = Mathf.MoveTowards(_under.volume, (lUnder ? 0.6f : 0f) * Sfx.Volume, dt * 2f);
        }
    }
}
