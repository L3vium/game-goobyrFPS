using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// A patrol car looping on a route. Its position is a pure function of the match clock while it
    /// drives, so every machine moves it identically without sending a transform. Its headlights
    /// are a vision cone: spot a player and it brakes, its two guards jump out and fight, and the
    /// stop point is broadcast in the state (decimetres along the loop). Shoot it enough: it burns,
    /// then explodes. Shooting its windscreen sets its alarm off (a distraction).
    /// </summary>
    public class VehiclePatrol : Interactable, IShootable
    {
        [SerializeField] private Transform _route;
        [SerializeField] private float _speed = 7f;
        [SerializeField] private Transform _body;
        [SerializeField] private float _offset;

        private float _length;
        private float _health = 160f;
        private float _scanTimer;
        private bool _dismounted;
        private GameObject _fire;

        public const int DESTROYED = -1;

        public void Setup(Transform pRoute, float pSpeed, Transform pBody, float pOffset)
        {
            _route = pRoute;
            _speed = pSpeed;
            _body = pBody;
            _offset = pOffset;
            _prompt = "";
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => false;

        private void Start() => _length = Length();

        private float Length()
        {
            if (_route == null || _route.childCount < 2) return 1f;
            float lLength = 0f;
            for (int i = 0; i < _route.childCount; i++) lLength += Vector3.Distance(P(i), P(i + 1));
            return lLength;
        }

        private Vector3 P(int pIndex) => _route.GetChild(((pIndex % _route.childCount) + _route.childCount) % _route.childCount).position;

        private void Sample(float pDistance, out Vector3 pPosition, out Vector3 pForward)
        {
            float d = Mathf.Repeat(pDistance, _length);
            for (int i = 0; i < _route.childCount; i++)
            {
                Vector3 a = P(i), b = P(i + 1);
                float lSegment = Vector3.Distance(a, b);
                if (d <= lSegment)
                {
                    pPosition = Vector3.Lerp(a, b, d / Mathf.Max(0.01f, lSegment));
                    pForward = (b - a).normalized;
                    return;
                }
                d -= lSegment;
            }
            pPosition = P(0);
            pForward = Vector3.forward;
        }

        private float MatchClock => MatchDirector.Instance != null ? MatchDirector.Instance.Elapsed : 0f;

        private float DistanceNow => State == 0 ? MatchClock * _speed + _offset : (State - 1) / 10f;

        private void Update()
        {
            if (_route == null || _body == null || _length <= 1f || State == DESTROYED) return;
            Sample(DistanceNow, out Vector3 lPos, out Vector3 lFwd);
            Sample(DistanceNow + 2.5f, out Vector3 lAhead, out _);
            Vector3 lDir = (lAhead - lPos).sqrMagnitude > 0.01f ? (lAhead - lPos).normalized : lFwd;
            _body.position = Vector3.Lerp(_body.position, lPos, State == 0 ? 1f : 1f - Mathf.Exp(-4f * Time.deltaTime));
            _body.rotation = Quaternion.Slerp(_body.rotation, Quaternion.LookRotation(lDir), 1f - Mathf.Exp(-6f * Time.deltaTime));

            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer || State != 0 || MatchDirector.Instance == null || !MatchDirector.Instance.Playing) return;
            _scanTimer -= Time.deltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = 0.3f;
            foreach (PlayerAvatar lPlayer in PlayerAvatar.All)
            {
                if (!lPlayer.Health.IsAlive || lPlayer.Hidden.Value || (lPlayer.Crouched.Value && lPlayer.InGrass.Value)) continue;
                Vector3 lTo = lPlayer.Chest - (_body.position + Vector3.up * 1.4f);
                if (lTo.magnitude > 22f || Vector3.Angle(_body.forward, lTo) > 50f) continue;
                if (Physics.Linecast(_body.position + Vector3.up * 1.4f, lPlayer.Chest, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                StopAndDismount(lPlayer);
                return;
            }
        }

        private void StopAndDismount(PlayerAvatar pSeen)
        {
            float lStop = DistanceNow + 4f;
            SetStateServer(1 + Mathf.RoundToInt(Mathf.Repeat(lStop, _length) * 10f));
            if (_dismounted) return;
            _dismounted = true;
            LevelZone lZone = LevelZone.At(_body.position) ?? LevelZone.Get(0);
            MatchDirector.Instance?.SpawnVehicleCrew(lZone, _body.position + _body.right * 2f, _body.position - _body.right * 2f, pSeen);
            PlayEffectServer(1);
        }

        public void OnShotServer(DamageInfo pInfo)
        {
            if (State == DESTROYED) return;
            _health -= pInfo.amount;
            if (State == 0)
            {
                PlayerAvatar lShooter = PlayerAvatar.ForClient(pInfo.attacker);
                StopAndDismount(lShooter);
            }
            if (_health < 60f && _fire == null) PlayEffectServer(2);
            if (_health > 0f) return;
            int lLast = State;
            SetStateServer(DESTROYED);
            Ballistics.ExplodeServer(_body.position + Vector3.up, 7f, 180f, pInfo.attacker, pInfo.attackerTeam);
            _ = lLast;
        }

        public override void OnEffect(int pEffect)
        {
            if (pEffect == 1) Audio.Sfx.Play("brakes", _body.position, 1f, 60f);
            if (pEffect == 2 && _fire == null) _fire = Effects.Fire(_body, new Vector3(0, 1.2f, 1.5f), 1.2f);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew != DESTROYED) return;
            foreach (Renderer lRenderer in _body.GetComponentsInChildren<Renderer>()) GameArt.Paint(lRenderer, new Color(0.12f, 0.11f, 0.1f));
            if (_fire == null) _fire = Effects.Fire(_body, new Vector3(0, 1.2f, 0), 1.6f);
        }
    }
}
