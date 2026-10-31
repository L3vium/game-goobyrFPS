using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// The camera of an eliminated player: a short death cam over the body, then the
    /// <see cref="CasterView"/> restricted to the agents they may watch (teammates only in Teams,
    /// anyone in free-for-all and co-op).
    /// </summary>
    public class Spectator : MonoBehaviour
    {
        public static Spectator Instance { get; private set; }
        public static bool Active => Instance != null;

        /// <summary>The short look at your own body before watching someone else.</summary>
        private const float DEATH_CAM = 1.2f;

        private Vector3 _deathPoint;
        private float _deathTimer;
        private float _orbit;

        public static void BeginDeathCam(Vector3 pPoint, Vector3 pImpulse)
        {
            if (Instance == null) Instance = new GameObject("Spectator").AddComponent<Spectator>();
            Instance._deathPoint = pPoint;
            Instance._deathTimer = DEATH_CAM;
            Instance._orbit = Mathf.Atan2(pImpulse.x, pImpulse.z) * Mathf.Rad2Deg + 180f;
        }

        public static void Stop()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
            if (CameraRig.Instance != null) CameraRig.Instance.SetDesaturate(0f);
        }

        private void LateUpdate()
        {
            CameraRig lRig = CameraRig.Instance;
            if (lRig == null) return;
            lRig.SetTargetFov(70f);

            _deathTimer -= Time.deltaTime;
            if (_deathTimer <= 0f)
            {
                CasterView.Begin(true);
                Stop();
                return;
            }
            _orbit += Time.deltaTime * 20f;
            Vector3 lFrom = _deathPoint + Quaternion.Euler(0, _orbit, 0) * new Vector3(0, 2.5f, -4.5f) + Vector3.up * (DEATH_CAM - _deathTimer) * 1.6f;
            lRig.SetPose(lFrom, Quaternion.LookRotation(_deathPoint - lFrom));
        }
    }
}
