using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// A CCTV camera sweeping a corridor or a gate: a trespasser it watches for a moment (same
    /// tier rules as the guards: passes count, behaving in public is fine) is reported - the two
    /// nearest guards of its zone come to the spot. A breaker cut or the monitoring room's recorder
    /// (<see cref="Systems.CameraRecorder"/>) switches the zone's cameras off. Host-side.
    /// </summary>
    public class SecurityCamera : MonoBehaviour
    {
        [SerializeField] private int _zone;
        [SerializeField] private float _range = 18f;
        [SerializeField] private float _halfAngle = 32f;
        [SerializeField] private float _sweep = 40f;

        private static readonly List<SecurityCamera> _all = new List<SecurityCamera>();
        public static IReadOnlyList<SecurityCamera> All => _all;

        public int Zone => _zone;
        public bool Off { get; set; }

        private Quaternion _rest;
        private float _seen, _cooldown;

        public void Setup(int pZone, float pRange) { _zone = pZone; _range = pRange; }

        private void OnEnable() { _all.Add(this); _rest = transform.localRotation; }
        private void OnDisable() => _all.Remove(this);

        /// <summary>Switch every camera of a zone on or off (recorder, breaker).</summary>
        public static void SetZone(int pZone, bool pOff)
        {
            foreach (SecurityCamera lCamera in _all) if (lCamera._zone == pZone) lCamera.Off = pOff;
        }

        private void Update()
        {
            if (Off) return;
            transform.localRotation = _rest * Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.35f + GetInstanceID()) * _sweep, 0f);
            if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
            _cooldown -= Time.deltaTime;

            PlayerAvatar lCaught = null;
            foreach (PlayerAvatar lPlayer in PlayerAvatar.All)
            {
                if (lPlayer == null || !lPlayer.IsSpawned || !lPlayer.Health.IsAlive || lPlayer.Hidden.Value) continue;
                Vector3 lTo = lPlayer.Chest - transform.position;
                if (lTo.magnitude > _range || Vector3.Angle(transform.forward, lTo) > _halfAngle) continue;
                SecurityTier lTier = SecurityZone.ForPlayer(SecurityZone.At(lPlayer.transform.position, out AccessLevel lNeed), lNeed, lPlayer.Weapons != null ? lPlayer.Weapons.Access : AccessLevel.None, false);
                if (DetectionModel.TierFactor(lTier, lPlayer.ActsSuspicious) <= 0.55f) continue;
                if (Physics.Linecast(transform.position, lPlayer.Chest, Core.GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                lCaught = lPlayer;
                break;
            }
            _seen = lCaught != null ? _seen + Time.deltaTime : Mathf.Max(0f, _seen - Time.deltaTime);
            if (lCaught == null || _seen < 1.2f || _cooldown > 0f) return;
            _cooldown = 12f;
            _seen = 0f;
            LevelZone lZone = LevelZone.Get(_zone);
            if (lZone != null) lZone.ReportThreatPoint(lCaught.transform.position, 2, 70f);
            lCaught.NotifyRpc("SPOTTED ON CAMERA", lZone != null ? lZone.ZoneName : "");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
            Gizmos.DrawRay(transform.position, transform.forward * _range);
        }
    }
}
