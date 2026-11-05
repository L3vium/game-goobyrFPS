using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// An automatic sentry gun guarding a restricted core. It sweeps its arc with a red laser; a
    /// trespasser it sees (anyone without the area's pass, calm or not - never in a public area)
    /// gets a beep, a 0.6 s lock, then bursts. A cable runs from it to a <see cref="TurretTerminal"/> hidden somewhere else:
    /// hacking that turns the laser green and the gun on the guards and the target (staff and
    /// guests are spared), the hacker credited with the kills.
    /// <para>
    /// Every machine aims it with the same rules (positions are synced); only the host fires and
    /// deals damage, each burst replayed everywhere as an effect. State 0 hostile, 1 turned.
    /// </para>
    /// </summary>
    public class AutoTurret : Interactable
    {
        public const int HOSTILE = 0;
        public const int TURNED = 1;

        [SerializeField] private Transform _yaw;
        [SerializeField] private Transform _pitch;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private LineRenderer _laser;
        [SerializeField] private Renderer _lamp;
        [SerializeField] private float _reach = 24f;
        [SerializeField] private float _arc = 75f;
        [SerializeField] private float _damage = 9f;

        private const float TURN_SPEED = 140f;
        private const float LOCK_TIME = 0.6f;
        private const float SHOT_INTERVAL = 0.11f;
        private const float RETARGET = 0.2f;
        private static readonly Color Hostile = new Color(4f, 0.15f, 0.1f);
        private static readonly Color Friendly = new Color(0.2f, 3.5f, 0.6f);

        private static readonly List<AutoTurret> _all = new List<AutoTurret>();
        public static IReadOnlyList<AutoTurret> All => _all;

        public bool Turned => State == TURNED;

        private Quaternion _restYaw;
        private Damageable _target;
        private Vector3 _aimPoint;
        private float _retarget, _lock, _shot, _noise;
        private ulong _hacker = DamageInfo.NO_PLAYER;
        private MaterialPropertyBlock _block;

        public void Setup(Transform pYaw, Transform pPitch, Transform pMuzzle, LineRenderer pLaser, Renderer pLamp, float pRange)
        {
            _yaw = pYaw;
            _pitch = pPitch;
            _muzzle = pMuzzle;
            _laser = pLaser;
            _lamp = pLamp;
            _reach = pRange;
            _prompt = "";
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => "";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => false;

        private void OnEnable()
        {
            _all.Add(this);
            if (_yaw != null) _restYaw = _yaw.localRotation;
            Tint();
        }

        private void OnDisable() => _all.Remove(this);

        /// <summary>Host: the terminal was hacked by <paramref name="pBy"/>.</summary>
        public void TurnServer(ulong pBy)
        {
            _hacker = pBy;
            SetStateServer(TURNED);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            _target = null;
            _lock = 0f;
            Tint();
            if (pNew == TURNED) { Audio.Sfx.Play("power_on", transform.position, 0.8f, 20f); Juice.Squash(_yaw != null ? _yaw : transform, 0.2f); }
        }

        private void Update()
        {
            if (_yaw == null || _pitch == null || _muzzle == null) return;
            float dt = Time.deltaTime;

            _retarget -= dt;
            if (_retarget <= 0f)
            {
                _retarget = RETARGET;
                Damageable lBefore = _target;
                _target = FindTarget(out _aimPoint);
                if (_target != null && _target != lBefore && !Turned) Audio.Sfx.Play("turret_warn", _muzzle.position, 0.9f, 30f);
            }
            else if (_target != null) _aimPoint = AimPoint(_target);

            Aim(dt);
            UpdateLaser();

            bool lLocked = _target != null && Vector3.Angle(_muzzle.forward, _aimPoint - _muzzle.position) < 5f;
            _lock = lLocked ? _lock + dt : 0f;
            if (!lLocked || _lock < LOCK_TIME || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            _shot -= dt;
            if (_shot > 0f) return;
            _shot = SHOT_INTERVAL;
            FireServer();
        }

        /// <summary>Turns toward the target, or sweeps the arc slowly around the rest heading.</summary>
        private void Aim(float pDt)
        {
            Vector3 lLocalDir;
            if (_target != null) lLocalDir = _yaw.parent.InverseTransformDirection(_aimPoint - _pitch.position);
            else lLocalDir = _restYaw * Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.4f + GetHashCode()) * _arc * 0.8f, 0f) * Vector3.forward;

            float lYaw = Mathf.Atan2(lLocalDir.x, lLocalDir.z) * Mathf.Rad2Deg;
            float lRest = _restYaw.eulerAngles.y;
            lYaw = lRest + Mathf.Clamp(Mathf.DeltaAngle(lRest, lYaw), -_arc, _arc);
            float lPitch = _target != null ? -Mathf.Atan2(lLocalDir.y, new Vector2(lLocalDir.x, lLocalDir.z).magnitude) * Mathf.Rad2Deg : 4f;
            lPitch = Mathf.Clamp(lPitch, -35f, 50f);

            float lStep = TURN_SPEED * pDt;
            _yaw.localRotation = Quaternion.RotateTowards(_yaw.localRotation, Quaternion.Euler(0f, lYaw, 0f), lStep);
            _pitch.localRotation = Quaternion.RotateTowards(_pitch.localRotation, Quaternion.Euler(lPitch, 0f, 0f), lStep);
        }

        private void UpdateLaser()
        {
            if (_laser == null) return;
            Vector3 lFrom = _muzzle.position;
            Vector3 lTo = lFrom + _muzzle.forward * _reach;
            if (Physics.Raycast(lFrom, _muzzle.forward, out RaycastHit lHit, _reach, ~0, QueryTriggerInteraction.Ignore)) lTo = lHit.point;
            _laser.SetPosition(0, lFrom);
            _laser.SetPosition(1, lTo);
        }

        /// <summary>Hostile: the nearest trespassing player in view (outside public areas). Turned: the nearest guard or target in view.</summary>
        private Damageable FindTarget(out Vector3 pAim)
        {
            pAim = Vector3.zero;
            Damageable lBest = null;
            float lBestDistance = _reach;
            if (!Turned)
            {
                foreach (PlayerAvatar lPlayer in PlayerAvatar.All)
                {
                    if (lPlayer != null && lPlayer.DevGhost) continue;
                    if (lPlayer == null || !lPlayer.IsSpawned || lPlayer.Health == null || !lPlayer.Health.IsAlive || lPlayer.Hidden.Value || lPlayer.IsProtector) continue;
                    SecurityTier lTier = SecurityZone.ForPlayer(SecurityZone.At(lPlayer.transform.position, out AccessLevel lNeed), lNeed, lPlayer.Weapons != null ? lPlayer.Weapons.Access : AccessLevel.None, false);
                    if (lTier == SecurityTier.Public) continue;
                    Consider(lPlayer.Health, lPlayer.Chest, ref lBest, ref lBestDistance, ref pAim);
                }
                return lBest;
            }
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || lNpc.Health == null || !lNpc.Health.IsAlive || lNpc.IsCrowd) continue;
                Consider(lNpc.Health, NpcChest(lNpc), ref lBest, ref lBestDistance, ref pAim);
            }
            return lBest;
        }

        private void Consider(Damageable pWho, Vector3 pPoint, ref Damageable pBest, ref float pBestDistance, ref Vector3 pAim)
        {
            Vector3 lTo = pPoint - _pitch.position;
            float lDistance = lTo.magnitude;
            if (lDistance > pBestDistance) return;
            Vector3 lFlat = _yaw.parent.InverseTransformDirection(lTo);
            float lYaw = Mathf.Atan2(lFlat.x, lFlat.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(_restYaw.eulerAngles.y, lYaw)) > _arc + 10f) return;
            if (Physics.Linecast(_muzzle.position + _muzzle.forward * 0.3f, pPoint, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore)) return;
            pBest = pWho;
            pBestDistance = lDistance;
            pAim = pPoint;
        }

        private static Vector3 NpcChest(Npc pNpc) => pNpc.transform.position + Vector3.up * 1.15f * pNpc.transform.localScale.y;

        private static Vector3 AimPoint(Damageable pTarget)
        {
            PlayerAvatar lPlayer = pTarget.GetComponent<PlayerAvatar>();
            if (lPlayer != null) return lPlayer.Chest;
            Npc lNpc = pTarget.GetComponent<Npc>();
            return lNpc != null ? NpcChest(lNpc) : pTarget.transform.position + Vector3.up;
        }

        /// <summary>Host: one round. A moving target is harder to hit; a hit is a bullet from the turret (or the hacker).</summary>
        private void FireServer()
        {
            if (_target == null || !_target.IsAlive) return;
            float lChance = 0.75f;
            PlayerAvatar lPlayer = _target.GetComponent<PlayerAvatar>();
            if (lPlayer != null && lPlayer.HostSpeed > 4f) lChance = 0.45f;
            if (Random.value < lChance)
            {
                _target.ApplyDamage(new DamageInfo
                {
                    amount = _damage,
                    type = DamageType.Bullet,
                    point = _aimPoint,
                    direction = (_aimPoint - _muzzle.position).normalized,
                    attacker = Turned ? _hacker : DamageInfo.NO_PLAYER,
                    attackerTeam = Match.MatchRulesLogic.NO_TEAM
                });
            }
            PlayEffectServer(0);
            _noise -= SHOT_INTERVAL;
            if (_noise > 0f) return;
            _noise = 1f;
            NoiseSystem.Emit(_muzzle.position, 35f, NoiseKind.Alarming, ulong.MaxValue, false);
        }

        public override void OnEffect(int pEffect)
        {
            if (_muzzle == null) return;
            Vector3 lFrom = _muzzle.position;
            Vector3 lTo = (_target != null ? _aimPoint : lFrom + _muzzle.forward * _reach) + Random.insideUnitSphere * 0.25f;
            Effects.Muzzle(lFrom, _muzzle.forward, 0.8f);
            Effects.Tracer(lFrom, lTo, Turned ? new Color(0.5f, 1f, 0.6f, 0.9f) : new Color(1f, 0.55f, 0.4f, 0.9f));
            Effects.Impact(lTo, (lFrom - lTo).normalized, new Color(0.75f, 0.7f, 0.6f));
            Audio.Sfx.Play("shot_smg", lFrom, 0.75f, 70f, 0.15f);
        }

        /// <summary>Laser and status lamp: red while it guards, green once turned.</summary>
        private void Tint()
        {
            _block ??= new MaterialPropertyBlock();
            Color lColor = Turned ? Friendly : Hostile;
            foreach (Renderer lRenderer in new Renderer[] { _laser, _lamp })
            {
                if (lRenderer == null) continue;
                lRenderer.GetPropertyBlock(_block);
                _block.SetColor("_BaseColor", lColor / 4f);
                _block.SetColor("_EmissionColor", lColor);
                lRenderer.SetPropertyBlock(_block);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _reach);
        }
    }
}
