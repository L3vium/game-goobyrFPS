using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// A player's Goob on the map, spawned by the match for each living player.
    /// <para>
    /// The owner moves it (<see cref="FpsMotor"/>, owner-authoritative NetworkTransform) and looks
    /// through its eyes; everyone else sees a wobbly third-person Goob in the team's colour. What the
    /// NPCs need to judge visibility - crouched, concealed, hidden, sprinting - is written by the
    /// owner into network variables; health, team and the proof stay with the host.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Damageable), typeof(FpsMotor))]
    public class PlayerAvatar : NetworkBehaviour
    {
        public static readonly List<PlayerAvatar> All = new List<PlayerAvatar>();
        public static PlayerAvatar Local { get; private set; }

        public readonly NetworkVariable<bool> Crouched = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Sprinting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> InGrass = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Hidden = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Aiming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<float> Pitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<int> Team = new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> CarriesProof = new NetworkVariable<bool>(false);

        public FpsMotor Motor { get; private set; }
        public CharacterController Controller { get; private set; }
        public Damageable Health { get; private set; }
        public WeaponHolder Weapons { get; private set; }
        public GoobBody Body { get; private set; }
        public GoobAnimator Animator { get; private set; }
        public LobbyPlayer Player => LobbyPlayer.ForClient(OwnerClientId);
        public string DisplayName => Player != null ? Player.Name : "Goob";

        /// <summary>Eye position as the NPCs and the camera see it.</summary>
        public Vector3 Eye => transform.position + Vector3.up * (Crouched.Value || Hidden.Value ? 0.98f : 1.74f);
        public Vector3 Chest => transform.position + Vector3.up * (Crouched.Value ? 0.6f : 1.1f);

        private float _yaw, _pitch;
        private float _bobPhase, _bobAmount;
        private float _landDip, _landDipVel;
        private float _tilt;
        private float _slideTilt;

        private void Awake()
        {
            Motor = GetComponent<FpsMotor>();
            Controller = GetComponent<CharacterController>();
            Health = GetComponent<Damageable>();
            Weapons = GetComponent<WeaponHolder>();
            Body = GetComponentInChildren<GoobBody>();
            Animator = GetComponentInChildren<GoobAnimator>();
            Motor.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Health.HeadHeight = 1.3f;
            Health.InitHealth(GameDatabase.Rules.playerHealth);
            Health.OnHitClient += HandleHit;
            Health.OnDeathClient += HandleDeath;
            Team.OnValueChanged += (_, pNew) => ApplyTeam(pNew);

            if (IsServer)
            {
                LobbyPlayer lPlayer = Player;
                Team.Value = lPlayer != null ? lPlayer.Team.Value : 0;
            }
            ApplyTeam(Team.Value);

            NetworkTransform lTransform = GetComponent<NetworkTransform>();
            if (lTransform != null) lTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

            if (IsOwner)
            {
                Local = this;
                Motor.enabled = true;
                _yaw = transform.eulerAngles.y;
                CameraRig.Ensure();
                Body.SetShadowOnly(true);
                GameInput.LockCursor(true);
                Motor.OnLanded += HandleLanded;
                Motor.OnJumped += () => { Animator.Squash(-0.15f); Audio.Sfx.Play("jump", transform.position, 0.6f); };
                Motor.OnSlide += HandleSlide;
                Motor.OnFallDamage += pDamage => FallDamageRpc(pDamage);
            }
            else Controller.enabled = true;
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }

        private void ApplyTeam(int pTeam)
        {
            Health.Team = pTeam;
            if (Body == null) return;
            // Agents wear a suit in their team's colour; the face and hair vary with the seat.
            int lSeat = Player != null ? Player.Seat.Value : (int)OwnerClientId;
            GoobLook lLook = GoobLook.Random(new System.Random(lSeat * 977 + 11), Outfit.Suit, LobbyPlayer.TeamColor(pTeam) * 0.85f);
            lLook.facial = Facial.None;
            lLook.earpiece = true;
            lLook.scale = 1f;
            Body.Configure(lLook);
            if (IsOwner) Body.SetShadowOnly(true);
        }

        // ---------------------------------------------------------------- owner loop

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner && Health.IsAlive)
            {
                Vector2 lLook = GameInput.Look;
                float lZoom = Weapons != null ? Weapons.LookSensitivityFactor : 1f;
                _yaw += lLook.x * 0.09f * lZoom;
                _pitch = Mathf.Clamp(_pitch - lLook.y * 0.09f * lZoom, -88f, 88f);
                if (Motor.Mode != MotorMode.Hidden) transform.rotation = Quaternion.Euler(0, _yaw, 0);

                Crouched.Value = Motor.Crouched;
                Sprinting.Value = Motor.Sprinting;
                InGrass.Value = Motor.InGrass;
                Hidden.Value = Motor.Mode == MotorMode.Hidden;
                if (Mathf.Abs(Pitch.Value - _pitch) > 1f) Pitch.Value = _pitch;
                Motor.SpeedMultiplier = CarriesProof.Value ? 0.92f : 1f;
            }

            if (Animator != null)
            {
                Animator.AimPitch = IsOwner ? _pitch : Pitch.Value;
                Animator.Crouched = Crouched.Value;
                Animator.Aiming = Aiming.Value;
                Animator.Talking = Player != null && Player.TalkingNow.Value;
                Animator.HoldingWeapon = Weapons != null && Weapons.HoldsWeapon;
            }
            if (Body != null && !IsOwner) Body.SetVisible(!Hidden.Value && Health.IsAlive);
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || !Health.IsAlive || CameraRig.Instance == null) return;
            float dt = Time.deltaTime;

            // Head bob from planar speed, landing dip spring, strafe tilt, slide tilt.
            float lSpeed = Motor.Grounded ? Motor.PlanarSpeed : 0f;
            _bobAmount = Mathf.Lerp(_bobAmount, Mathf.Clamp01(lSpeed / 7f), 1f - Mathf.Exp(-10f * dt));
            _bobPhase += lSpeed * dt * 1.9f;
            Vector3 lBob = new Vector3(Mathf.Cos(_bobPhase) * 0.04f, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.06f, 0) * _bobAmount;
            if (Motor.Aiming) lBob *= 0.25f;

            _landDipVel += (-_landDip * 140f - _landDipVel * 14f) * dt;
            _landDip += _landDipVel * dt;

            float lStrafe = Vector3.Dot(Motor.Velocity, transform.right);
            _tilt = Mathf.Lerp(_tilt, -lStrafe * 0.45f, 1f - Mathf.Exp(-8f * dt));
            _slideTilt = Mathf.Lerp(_slideTilt, Motor.Sliding ? -4f : 0f, 1f - Mathf.Exp(-12f * dt));

            Vector3 lEye;
            Quaternion lRotation;
            HidingSpot lSpot = Motor.HidingSpot;
            if (lSpot != null)
            {
                lEye = lSpot.InsidePosition + Vector3.up * 0.9f;
                lRotation = Quaternion.Euler(_pitch * 0.4f, lSpot.transform.eulerAngles.y + Mathf.DeltaAngle(lSpot.transform.eulerAngles.y, _yaw) * 0.25f, 0);
            }
            else
            {
                lEye = transform.position + Vector3.up * (Motor.EyeHeight + _landDip) + transform.rotation * lBob;
                lRotation = Quaternion.Euler(_pitch, _yaw, _tilt + _slideTilt);
            }
            CameraRig.Instance.SetPose(lEye, lRotation);

            float lFov = CameraRig.Instance.BaseFov + (Motor.Sprinting ? 7f : 0f) + (Motor.Sliding ? 10f : 0f);
            if (Weapons != null && Weapons.AdsFov > 0f) lFov = Weapons.AdsFov;
            CameraRig.Instance.SetTargetFov(lFov);

            // Concealment reads on the HUD status, never as a screen vignette.
            CameraRig.Instance.SetVignette(0.18f, Color.black);
        }

        private void HandleLanded(float pFallSpeed)
        {
            float lImpact = Mathf.Clamp01((pFallSpeed - 2f) / 14f);
            _landDipVel -= lImpact * 3.5f;
            Animator.Squash(lImpact * 0.6f);
            if (lImpact > 0.2f)
            {
                Juice.Shake(lImpact * 0.35f);
                Audio.Sfx.Play("land", transform.position, 0.4f + lImpact);
            }
        }

        private void HandleSlide(float pSide)
        {
            Juice.FovKick(6f);
            Juice.Shake(0.08f);
            Audio.Sfx.Play("slide", transform.position, 0.7f);
        }

        public float LookYaw => _yaw;
        public float LookPitch => _pitch;

        public Vector3 AimOrigin => CameraRig.Instance != null && IsOwner ? CameraRig.Instance.WorldCamera.transform.position : Eye;
        public Vector3 AimDirection => IsOwner && CameraRig.Instance != null ? CameraRig.Instance.WorldCamera.transform.forward : Quaternion.Euler(Pitch.Value, transform.eulerAngles.y, 0) * Vector3.forward;

        public void SetLook(float pYaw, float pPitch)
        {
            _yaw = pYaw;
            _pitch = pPitch;
        }

        // ---------------------------------------------------------------- hits and death

        private void HandleHit(float pAmount, Vector3 pPoint, Vector3 pDirection, bool pHeadshot)
        {
            Animator?.Shove(pDirection * Mathf.Clamp01(pAmount / 40f));
            HitFlash.Play(Body);
            if (IsOwner)
            {
                Juice.Hurt(Mathf.Clamp01(pAmount / 60f) + 0.25f);
                UI.Hud.ReportDamageFrom(pPoint - pDirection * 5f);
            }
        }

        private void HandleDeath(Vector3 pImpulse, Vector3 pPoint, DamageType pType)
        {
            GoobRagdoll.Spawn(Body, pImpulse, pPoint);
            Controller.enabled = false;
            if (IsOwner)
            {
                Motor.enabled = false;
                Juice.Flash(new Color(0.8f, 0.1f, 0.1f), 0.6f);
                Juice.Shake(0.6f);
                if (CameraRig.Instance != null) CameraRig.Instance.SetDesaturate(0.8f);
                Spectator.BeginDeathCam(transform.position + Vector3.up * 1.5f, pImpulse);
            }
        }

        [Rpc(SendTo.Server)]
        private void FallDamageRpc(float pDamage)
        {
            Health.ApplyDamage(DamageInfo.World(Mathf.Min(pDamage, 300f), DamageType.Fall, transform.position, Vector3.down));
        }

        /// <summary>Owner asks the host to use an interactable while holding <paramref name="pHeldItem"/>.</summary>
        [Rpc(SendTo.Server)]
        public void InteractRpc(int pInteractableId, int pHeldItem)
        {
            if (MapSession.Instance != null) MapSession.Instance.HandleInteract(this, pInteractableId, pHeldItem);
        }

        /// <summary>Host: pushes the owner (explosions).</summary>
        [Rpc(SendTo.Owner)]
        public void PushOwnerRpc(Vector3 pVelocity) => Motor.Launch(pVelocity, false);

        /// <summary>Host: puts the owner at a start point.</summary>
        [Rpc(SendTo.Owner)]
        public void TeleportOwnerRpc(Vector3 pPosition, float pYaw)
        {
            Motor.Teleport(pPosition, pYaw);
            _yaw = pYaw;
            _pitch = 0f;
        }

        /// <summary>Host: where a zone's squad believes this player is (the "shadow" only they see).</summary>
        [Rpc(SendTo.Owner)]
        public void ShowGhostRpc(Vector3 pPosition, bool pVisible, int pZone)
        {
            if (Health.IsAlive) AI.LkpGhost.Show(pPosition, pVisible);
        }

        public static PlayerAvatar ForClient(ulong pClientId)
        {
            foreach (PlayerAvatar lAvatar in All) if (lAvatar.OwnerClientId == pClientId) return lAvatar;
            return null;
        }
    }
}
