using System;
using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    public enum MotorMode
    {
        Walk,
        Climb,
        Mantle,
        Zipline,
        Hidden,
        Locked
    }

    /// <summary>
    /// The owner's first-person movement on a <see cref="CharacterController"/>: walk, sprint,
    /// crouch (smooth height), jump with coyote time and buffer, a quick lateral slide (Q/E) to hop
    /// from cover to cover, a sprint-crouch slide, mantling over ledges, climbing <c>Climbable</c>
    /// walls, riding ziplines, jump pads and hiding spots.
    /// Only enabled on the owning machine; the result reaches everyone through the NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FpsMotor : MonoBehaviour
    {
        [Header("Speeds")]
        [SerializeField] private float _walkSpeed = 4.4f;
        [SerializeField] private float _sprintSpeed = 7.2f;
        [SerializeField] private float _crouchSpeed = 2.3f;
        [SerializeField] private float _acceleration = 55f;
        [SerializeField] private float _airAcceleration = 14f;
        [SerializeField] private float _jumpHeight = 1.15f;
        [SerializeField] private float _gravity = -24f;

        [Header("Heights")]
        [SerializeField] private float _standHeight = 1.8f;
        [SerializeField] private float _crouchHeight = 1.05f;
        [SerializeField] private float _eyeOffset = 0.16f;

        [Header("Slides")]
        [SerializeField] private float _sideSlideSpeed = 11f;
        [SerializeField] private float _sideSlideTime = 0.28f;
        [SerializeField] private float _slideCooldown = 0.55f;
        [SerializeField] private float _crouchSlideBoost = 9.5f;

        [Header("Traversal")]
        [SerializeField] private float _climbSpeed = 3.2f;
        [SerializeField] private float _mantleMaxHeight = 2.4f;
        [SerializeField] private float _mantleTime = 0.32f;

        public MotorMode Mode { get; private set; } = MotorMode.Walk;
        public bool Grounded { get; private set; }
        public bool Crouched { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Sliding => _slideTimer > 0f || _crouchSliding;
        public float SlideSide { get; private set; }
        public Vector3 Velocity => _velocity;
        public float PlanarSpeed => new Vector3(_velocity.x, 0, _velocity.z).magnitude;
        public float EyeHeight => _controller.height - _eyeOffset;
        public bool InGrass => _grassCount > 0;
        public bool Aiming { get; set; }
        /// <summary>Multiplies the speed (carrying a heavy weapon, being sick).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Raised on landing with the fall speed.</summary>
        public event Action<float> OnLanded;
        public event Action OnJumped;
        public event Action<float> OnSlide;
        /// <summary>Raised on landing with the fall damage to apply (0 = none).</summary>
        public event Action<float> OnFallDamage;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _coyote, _jumpBuffer;
        private float _slideTimer, _slideCooldownTimer;
        private Vector3 _slideDirection;
        private bool _crouchSliding;
        private bool _wantCrouch;
        private float _airStartY;
        private int _grassCount;
        private Vector3 _mantleFrom, _mantleTo;
        private float _mantleT;
        private Systems.Zipline _zipline;
        private float _ziplineT;
        private Systems.HidingSpot _hidingSpot;
        private Vector3 _climbNormal;

        public Transform Yaw => transform;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            _velocity = Vector3.zero;
            Mode = MotorMode.Walk;
        }

        public void Teleport(Vector3 pPosition, float pYaw)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            _controller.enabled = true;
            _velocity = Vector3.zero;
            Mode = MotorMode.Walk;
        }

        /// <summary>Jump pads, explosions.</summary>
        public void Launch(Vector3 pVelocity, bool pReplace = true)
        {
            if (Mode == MotorMode.Hidden || Mode == MotorMode.Locked) return;
            if (Mode != MotorMode.Walk) Mode = MotorMode.Walk;
            _velocity = pReplace ? pVelocity : _velocity + pVelocity;
            Grounded = false;
            _coyote = 0f;
            _airStartY = transform.position.y + 50f;
        }

        public void EnterGrass() => _grassCount++;
        public void ExitGrass() => _grassCount = Mathf.Max(0, _grassCount - 1);

        public void Lock(bool pLocked)
        {
            if (pLocked) { Mode = MotorMode.Locked; _velocity = Vector3.zero; }
            else if (Mode == MotorMode.Locked) Mode = MotorMode.Walk;
        }

        // ---------------------------------------------------------------- traversal entry points

        public bool StartZipline(Systems.Zipline pZipline)
        {
            if (Mode != MotorMode.Walk && Mode != MotorMode.Climb) return false;
            _zipline = pZipline;
            _ziplineT = pZipline.ClosestT(transform.position);
            Mode = MotorMode.Zipline;
            return true;
        }

        public bool EnterHidingSpot(Systems.HidingSpot pSpot)
        {
            if (Mode != MotorMode.Walk) return false;
            _hidingSpot = pSpot;
            Mode = MotorMode.Hidden;
            _velocity = Vector3.zero;
            _controller.enabled = false;
            transform.position = pSpot.InsidePosition;
            _controller.enabled = true;
            return true;
        }

        public void ExitHidingSpot()
        {
            if (Mode != MotorMode.Hidden || _hidingSpot == null) return;
            _controller.enabled = false;
            transform.position = _hidingSpot.ExitPosition;
            _controller.enabled = true;
            _hidingSpot = null;
            Mode = MotorMode.Walk;
        }

        public Systems.HidingSpot HidingSpot => Mode == MotorMode.Hidden ? _hidingSpot : null;

        // ---------------------------------------------------------------- update

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector2 lInput = GameInput.Move;
            switch (Mode)
            {
                case MotorMode.Walk: UpdateWalk(dt, lInput); break;
                case MotorMode.Climb: UpdateClimb(dt, lInput); break;
                case MotorMode.Mantle: UpdateMantle(dt); break;
                case MotorMode.Zipline: UpdateZipline(dt); break;
                case MotorMode.Hidden:
                    if (GameInput.Interact || GameInput.JumpPressed) ExitHidingSpot();
                    break;
            }
            UpdateHeight(dt);
        }

        private void UpdateWalk(float dt, Vector2 pInput)
        {
            bool lWasGrounded = Grounded;
            Grounded = _controller.isGrounded;
            if (Grounded)
            {
                _coyote = 0.12f;
                if (!lWasGrounded)
                {
                    float lFall = -_velocity.y;
                    float lDrop = _airStartY - transform.position.y;
                    OnLanded?.Invoke(lFall);
                    if (lDrop > 7f) OnFallDamage?.Invoke((lDrop - 7f) * 14f);
                }
                _airStartY = transform.position.y;
            }
            else
            {
                _coyote -= dt;
                _airStartY = Mathf.Max(_airStartY, transform.position.y);
            }

            // Crouch: toggle or hold.
            if (GameInput.ToggleCrouch) { if (GameInput.CrouchPressed) _wantCrouch = !_wantCrouch; }
            else _wantCrouch = GameInput.CrouchHeld;

            // Sprinting stands a toggled crouch back up.
            if (GameInput.Sprint && _wantCrouch && !_crouchSliding && pInput.y > 0.3f && GameInput.ToggleCrouch && !GameInput.CrouchPressed && CanStand()) _wantCrouch = false;
            Sprinting = GameInput.Sprint && pInput.y > 0.3f && !Aiming && !_wantCrouch && Grounded;

            // Sprint + crouch press = forward slide.
            if (GameInput.CrouchPressed && PlanarSpeed > _walkSpeed + 0.5f && Grounded && _slideCooldownTimer <= 0f)
            {
                _crouchSliding = true;
                _wantCrouch = true;
                _slideDirection = new Vector3(_velocity.x, 0, _velocity.z).normalized;
                _velocity += _slideDirection * (_crouchSlideBoost - PlanarSpeed * 0.5f);
                _slideCooldownTimer = _slideCooldown;
                OnSlide?.Invoke(0f);
            }

            // Lateral slide (Q/E): a quick low dash sideways, cover to cover.
            _slideCooldownTimer -= dt;
            if (_slideCooldownTimer <= 0f && Grounded && (GameInput.SlideLeft || GameInput.SlideRight))
            {
                SlideSide = GameInput.SlideLeft ? -1f : 1f;
                _slideDirection = transform.right * SlideSide;
                _slideTimer = _sideSlideTime;
                _slideCooldownTimer = _slideCooldown;
                OnSlide?.Invoke(SlideSide);
            }

            Crouched = _wantCrouch || _slideTimer > 0f || !CanStand();

            float lSpeed = (Crouched ? _crouchSpeed : Sprinting ? _sprintSpeed : _walkSpeed) * SpeedMultiplier;
            if (Aiming) lSpeed *= 0.65f;
            Vector3 lWish = (transform.forward * pInput.y + transform.right * pInput.x) * lSpeed;
            Vector3 lPlanar = new Vector3(_velocity.x, 0, _velocity.z);

            if (_slideTimer > 0f)
            {
                _slideTimer -= dt;
                lPlanar = _slideDirection * _sideSlideSpeed * Mathf.Lerp(0.4f, 1f, _slideTimer / _sideSlideTime);
            }
            else if (_crouchSliding)
            {
                lPlanar = Vector3.MoveTowards(lPlanar, Vector3.zero, 7f * dt);
                if (lPlanar.magnitude < _crouchSpeed + 0.3f || !Grounded) _crouchSliding = false;
            }
            else
            {
                float lAccel = Grounded ? _acceleration : _airAcceleration;
                lPlanar = Vector3.MoveTowards(lPlanar, lWish, lAccel * dt);
            }

            // Jump, mantle.
            if (GameInput.JumpPressed) _jumpBuffer = 0.15f;
            else _jumpBuffer -= dt;

            if (_jumpBuffer > 0f)
            {
                if (TryMantle()) { _jumpBuffer = 0f; return; }
                if (_coyote > 0f && !Crouched)
                {
                    _velocity.y = Mathf.Sqrt(-2f * _gravity * _jumpHeight);
                    _coyote = 0f;
                    _jumpBuffer = 0f;
                    Grounded = false;
                    OnJumped?.Invoke();
                }
                else if (_wantCrouch && _coyote > 0f) { _wantCrouch = false; _jumpBuffer = 0f; }
            }

            if (Grounded && _velocity.y < 0f) _velocity.y = -3f;
            else _velocity.y += _gravity * dt;

            _velocity.x = lPlanar.x;
            _velocity.z = lPlanar.z;
            _controller.Move(_velocity * dt);

            // Climbable surface ahead and pushing forward: grab it.
            if (pInput.y > 0.5f && TryGrabClimbable()) Mode = MotorMode.Climb;
        }

        private bool CanStand()
        {
            if (_controller.height >= _standHeight - 0.01f) return true;
            float lRadius = _controller.radius * 0.95f;
            Vector3 lBottom = transform.position + Vector3.up * (lRadius + 0.05f);
            return !Physics.SphereCast(lBottom, lRadius, Vector3.up, out _, _standHeight - lRadius * 2f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
        }

        private void UpdateHeight(float dt)
        {
            float lTarget = Crouched || Mode == MotorMode.Hidden ? _crouchHeight : _standHeight;
            float lHeight = Mathf.MoveTowards(_controller.height, lTarget, dt * 6f);
            _controller.height = lHeight;
            _controller.center = new Vector3(0, lHeight * 0.5f, 0);
        }

        private bool TryMantle()
        {
            Vector3 lOrigin = transform.position + Vector3.up * 0.9f;
            if (!Physics.Raycast(lOrigin, transform.forward, out RaycastHit lWall, 0.9f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Dot(lWall.normal, Vector3.up) > 0.5f) return false;

            Vector3 lTop = lWall.point - lWall.normal * 0.45f + Vector3.up * (_mantleMaxHeight + 0.2f);
            if (!Physics.Raycast(lTop, Vector3.down, out RaycastHit lLedge, _mantleMaxHeight + 0.2f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            float lRise = lLedge.point.y - transform.position.y;
            if (lRise < 0.5f || lRise > _mantleMaxHeight || Vector3.Dot(lLedge.normal, Vector3.up) < 0.7f) return false;
            if (Physics.CheckCapsule(lLedge.point + Vector3.up * (_controller.radius + 0.1f), lLedge.point + Vector3.up * (_crouchHeight - _controller.radius), _controller.radius * 0.9f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) return false;

            _mantleFrom = transform.position;
            _mantleTo = lLedge.point + Vector3.up * 0.05f;
            _mantleT = 0f;
            Mode = MotorMode.Mantle;
            _velocity = Vector3.zero;
            OnJumped?.Invoke();
            return true;
        }

        private void UpdateMantle(float dt)
        {
            _mantleT += dt / _mantleTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_mantleT));
            Vector3 lUp = Vector3.Lerp(_mantleFrom, new Vector3(_mantleFrom.x, _mantleTo.y, _mantleFrom.z), Mathf.Clamp01(t * 1.6f));
            Vector3 lPos = Vector3.Lerp(lUp, _mantleTo, Mathf.Clamp01(t * 1.6f - 0.6f));
            _controller.enabled = false;
            transform.position = lPos;
            _controller.enabled = true;
            Crouched = true;
            if (_mantleT >= 1f)
            {
                Mode = MotorMode.Walk;
                _airStartY = transform.position.y;
                OnLanded?.Invoke(2f);
            }
        }

        private bool TryGrabClimbable()
        {
            Vector3 lOrigin = transform.position + Vector3.up * 1f;
            if (!Physics.Raycast(lOrigin, transform.forward, out RaycastHit lHit, 0.75f, 1 << GameLayers.CLIMBABLE, QueryTriggerInteraction.Ignore)) return false;
            _climbNormal = lHit.normal;
            _velocity = Vector3.zero;
            return true;
        }

        private void UpdateClimb(float dt, Vector2 pInput)
        {
            Grounded = false;
            Crouched = false;
            Vector3 lOrigin = transform.position + Vector3.up * 1f;
            bool lWall = Physics.Raycast(lOrigin, -_climbNormal, out RaycastHit lHit, 1f, 1 << GameLayers.CLIMBABLE, QueryTriggerInteraction.Ignore);
            if (lWall) _climbNormal = lHit.normal;

            if (GameInput.JumpPressed)
            {
                Mode = MotorMode.Walk;
                _velocity = _climbNormal * 4.5f + Vector3.up * 4f;
                OnJumped?.Invoke();
                return;
            }

            if (!lWall)
            {
                // Out of wall at chest height: top out over the ledge.
                Mode = MotorMode.Walk;
                if (!TryMantle()) _velocity = Vector3.up * 3f - _climbNormal * 2f;
                return;
            }

            Vector3 lRight = Vector3.Cross(Vector3.up, _climbNormal).normalized;
            Vector3 lMove = Vector3.up * pInput.y * _climbSpeed - lRight * pInput.x * _climbSpeed * 0.7f - _climbNormal * 0.5f;
            _controller.Move(lMove * dt);
            if (pInput.y < -0.5f && _controller.isGrounded) Mode = MotorMode.Walk;
            _airStartY = transform.position.y;
        }

        private void UpdateZipline(float dt)
        {
            if (_zipline == null) { Mode = MotorMode.Walk; return; }
            _ziplineT += dt * _zipline.Speed / Mathf.Max(1f, _zipline.Length);
            Vector3 lPos = _zipline.PointAt(_ziplineT) - Vector3.up * (_controller.height + 0.1f);
            _controller.enabled = false;
            transform.position = lPos;
            _controller.enabled = true;
            _airStartY = transform.position.y;
            if (_ziplineT >= 1f || GameInput.JumpPressed || GameInput.CrouchPressed)
            {
                _velocity = _zipline.Direction * _zipline.Speed * 0.6f;
                _zipline = null;
                Mode = MotorMode.Walk;
            }
        }
    }
}
