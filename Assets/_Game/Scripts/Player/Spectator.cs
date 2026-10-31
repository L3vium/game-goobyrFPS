using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// The camera of an eliminated player: a short death cam over the body, then a third-person
    /// view behind a living player - teammates only, anyone in free-for-all. Jump / click cycles.
    /// </summary>
    public class Spectator : MonoBehaviour
    {
        public static Spectator Instance { get; private set; }
        public static bool Active => Instance != null;

        public PlayerAvatar Watched { get; private set; }

        /// <summary>The short look at your own body before watching someone else.</summary>
        private const float DEATH_CAM = 1.2f;

        private Vector3 _deathPoint;
        private float _deathTimer;
        private float _orbit;
        private int _index;

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

        private List<PlayerAvatar> Candidates()
        {
            List<PlayerAvatar> lList = new List<PlayerAvatar>();
            int lMyTeam = LobbyPlayer.Local != null ? LobbyPlayer.Local.Team.Value : 0;
            MatchMode lMode = LobbyState.Instance != null ? LobbyState.Instance.Mode.Value : MatchMode.FreeForAll;
            foreach (PlayerAvatar lAvatar in PlayerAvatar.All)
                if (lAvatar.Health.IsAlive && !lAvatar.IsOwner && MatchRulesLogic.CanSpectate(lMode, lMyTeam, lAvatar.Team.Value)) lList.Add(lAvatar);
            if (lList.Count == 0)
                foreach (PlayerAvatar lAvatar in PlayerAvatar.All)
                    if (lAvatar.Health.IsAlive && !lAvatar.IsOwner) lList.Add(lAvatar);
            return lList;
        }

        private void LateUpdate()
        {
            CameraRig lRig = CameraRig.Instance;
            if (lRig == null) return;
            lRig.SetTargetFov(70f);

            if (_deathTimer > 0f)
            {
                _deathTimer -= Time.deltaTime;
                _orbit += Time.deltaTime * 20f;
                Vector3 lFrom = _deathPoint + Quaternion.Euler(0, _orbit, 0) * new Vector3(0, 2.5f, -4.5f) + Vector3.up * (DEATH_CAM - _deathTimer) * 1.6f;
                lRig.SetPose(lFrom, Quaternion.LookRotation(_deathPoint - lFrom));
                return;
            }

            lRig.SetDesaturate(0.25f);
            List<PlayerAvatar> lCandidates = Candidates();
            if (lCandidates.Count == 0)
            {
                Watched = null;
                _orbit += Time.deltaTime * 8f;
                Vector3 lFrom = _deathPoint + Quaternion.Euler(0, _orbit, 0) * new Vector3(0, 14f, -18f);
                lRig.SetPose(lFrom, Quaternion.LookRotation(_deathPoint - lFrom));
                return;
            }

            if (GameInput.NextSpectate) _index++;
            _index = (_index % lCandidates.Count + lCandidates.Count) % lCandidates.Count;
            Watched = lCandidates[_index];

            Vector3 lLook = Watched.transform.position + Vector3.up * 1.6f;
            Quaternion lAim = Quaternion.Euler(Mathf.Clamp(Watched.Pitch.Value, -30f, 50f) * 0.5f + 12f, Watched.transform.eulerAngles.y, 0);
            Vector3 lCam = lLook + lAim * new Vector3(0.6f, 0.4f, -3.4f);
            if (Physics.Linecast(lLook, lCam, out RaycastHit lHit, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                lCam = lHit.point + (lLook - lCam).normalized * 0.2f;
            lRig.SetPose(Vector3.Lerp(lRig.WorldCamera.transform.position, lCam, 1f - Mathf.Exp(-12f * Time.deltaTime)), lAim);
        }
    }
}
