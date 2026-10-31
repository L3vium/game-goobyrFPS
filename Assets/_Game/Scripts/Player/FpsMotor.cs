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
        Locked,
        Swim
    }

    /// <summary>
    /// The owner's first-person movement on a <see cref="CharacterController"/>: walk, sprint,
    /// crouch (smooth height), jump with coyote time and buffer, a quick lateral slide (Q/E) to hop
    /// from cover to cover, a sprint-crouch slide, mantling over ledges, climbing <c>Climbable</c>
    /// walls, riding ziplines, jump pads and hiding spots, swimming and diving in water
    /// (<see cref="Systems.WaterVolume"/>: crouch dives, jump surfaces, breath runs out under water).
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
        [SerializeField] private float _standHeight = 1.86f;
        [SerializeField] private float _crouchHeight = 1.1f;
        [SerializeField] private float _eyeOffset = 0.12f;

        [Header("Slides")]
        [SerializeField] private float _slideCooldown = 0.55f;
        /// <summary>Slide speed out of a sprint, and out of a walk.</summary>
        [SerializeField] private float _crouchSlideBoost = 12f;
        [SerializeField] private float _walkSlideBoost = 7f;
        /// <summary>How fast a slide bleeds speed out of a sprint, and out of a walk (m/s²).</summary>
        [SerializeField] private float _sprintSlideFriction = 2.8f;
        [SerializeField] private float _walkSlideFriction = 6f;

        [Header("Water")]
        [SerializeField] private float _swimSpeed = 3f;
        [SerializeField] private float _swimSprintSpeed = 4.5f;
        [SerializeField] private float _diveSpeed = 2.4f;
        /// <summary>Seconds of breath under water before drowning starts.</summary>
        [SerializeField] private float _breathTime = 20f;

        [Header("Traversal")]
        [SerializeField] private float _climbSpeed = 3.2f;
        [SerializeField] private float _mantleMaxHeight = 2.4f;
        [SerializeField] private float _mantleTime = 0.32f;

        public MotorMode Mode { get; private set; } = MotorMode.Walk;
        public bool Grounded { get; private set; }
        public bool Crouched { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Sliding => _crouchSliding;
        public Vector3 Velocity => _velocity;
        public float PlanarSpeed => new Vector3(_velocity.x, 0, _velocity.z).magnitude;
        public float EyeHeight => _controller.height - _eyeOffset;
        public bool InGrass => _grassCount > 0;
        public bool Swimming => Mode == MotorMode.Swim;
        /// <summary>The eyes are below the water surface (hidden from sight, muffled, breath running out).</summary>
        public bool Underwater { get; private set; }
        /// <summary>Breath left, 1 = full.</summary>
        public float Breath { get; private set; } = 1f;
        public bool Aiming { get; set; }
        /// <summary>Multiplies the speed (carrying a heavy weapon, being sick).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Raised on landing with the fall speed.</summary>
        public event Action<float> OnLanded;
        public event Action OnJumped;
        public event Action<float> OnSlide;
        /// <summary>Raised on landing with the fall damage to apply (0 = none), and each second of drowning.</summary>
        public event Action<float> OnFallDamage;
        /// <summary>Raised when hitting the water, with the entry speed.</summary>
        public event Action<float> OnSplash;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _coyote, _jumpBuffer;
        private float _slideCooldownTimer, _slideFriction = 7f;
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
        private float _drownTimer;

        public Transform Yaw => transform;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            _velocity = Vector3.zero;
            Mode = MotorMode.Walk;
            Underwater = false;
            Breath = 1f;
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
                case MotorMode.Swim: UpdateSwim(dt, lInput); break;
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

            // Crouch and sprint are held. Crouching while moving slides; from a sprint, further.
            bool lHeld = GameInput.CrouchHeld;
            bool lCrouchStarted = lHeld && !_wantCrouch;
            _wantCrouch = lHeld;
            Sprinting = GameInput.Sprint && pInput.y > 0.3f && !Aiming && !_wantCrouch && Grounded;

            _slideCooldownTimer -= dt;
            if (lCrouchStarted && Grounded && PlanarSpeed > _crouchSpeed + 0.6f && _slideCooldownTimer <= 0f)
            {
                bool lFromSprint = PlanarSpeed > _walkSpeed + 0.5f;
                _crouchSliding = true;
                _slideDirection = new Vector3(_velocity.x, 0, _velocity.z).normalized;
                float lTarget = lFromSprint ? _crouchSlideBoost : _walkSlideBoost;
                _velocity += _slideDirection * Mathf.Max(0f, lTarget - PlanarSpeed);
                _slideFriction = lFromSprint ? _sprintSlideFriction : _walkSlideFriction;
                _slideCooldownTimer = _slideCooldown;
                OnSlide?.Invoke(lFromSprint ? 1f : 0.5f);
            }

            Crouched = _wantCrouch || _crouchSliding || !CanStand();

            float lSpeed = (Crouched ? _crouchSpeed : Sprinting ? _sprintSpeed : _walkSpeed) * SpeedMultiplier;
            if (Aiming) lSpeed *= 0.65f;
            Vector3 lWish = (transform.forward * pInput.y + transform.right * pInput.x) * lSpeed;
            Vector3 lPlanar = new Vector3(_velocity.x, 0, _velocity.z);

            if (_crouchSliding)
            {
                // A slide keeps its direction and bleeds speed (less downhill, more uphill); releasing crouch ends it.
                float lSlope = Grounded && Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit lGround, 0.8f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)
                    ? Vector3.Dot(new Vector3(lGround.normal.x, 0f, lGround.normal.z), lPlanar.normalized) : 0f;
                lPlanar = Vector3.MoveTowards(lPlanar, Vector3.zero, Mathf.Max(0.5f, _slideFriction - lSlope * 14f) * dt);
                if (lPlanar.magnitude < _crouchSpeed + 0.3f || !Grounded || !_wantCrouch) _crouchSliding = false;
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
            }

            if (Grounded && _velocity.y < 0f) _velocity.y = -3f;
            else _velocity.y += _gravity * dt;

            _velocity.x = lPlanar.x;
            _velocity.z = lPlanar.z;
            _controller.Move(_velocity * dt);

            // Climbable surface ahead and pushing forward: grab it.
            if (pInput.y > 0.5f && TryGrabClimbable()) Mode = MotorMode.Climb;

            // Deep enough water: swim.
            if (Systems.WaterVolume.At(transform.position + Vector3.up * 0.1f, out float lSurface) != null && lSurface - transform.position.y > SWIM_DEPTH)
            {
                OnSplash?.Invoke(Mathf.Abs(_velocity.y) + PlanarSpeed * 0.3f);
                _crouchSliding = false;
                Mode = MotorMode.Swim;
            }
        }

        /// <summary>Water deeper than this at the feet makes the player swim.</summary>
        private const float SWIM_DEPTH = 1.25f;

        private void UpdateSwim(float dt, Vector2 pInput)
        {
            Systems.WaterVolume lWater = Systems.WaterVolume.At(transform.position + Vector3.up * 0.5f, out float lSurface);
            Grounded = false;
            Crouched = false;
            Sprinting = false;
            _crouchSliding = false;
            if (lWater == null || (lSurface - transform.position.y < SWIM_DEPTH - 0.2f && _controller.isGrounded))
            {
                Mode = MotorMode.Walk;
                Underwater = false;
                _airStartY = transform.position.y;
                return;
            }

            Underwater = transform.position.y + EyeHeight < lSurface - 0.05f;
            // Floating keeps the eyes just above the surface; crouch dives, jump rises.
            float lFloat = lSurface - EyeHeight + 0.15f;
            float lTargetY = GameInput.CrouchHeld ? -_diveSpeed
                : GameInput.JumpHeld && Underwater ? _diveSpeed * 1.1f
                : Mathf.Clamp((lFloat - transform.position.y) * 2.5f, -1.5f, 2.2f);
            _velocity.y = Mathf.MoveTowards(_velocity.y, lTargetY, 8f * dt);

            float lSpeed = (GameInput.Sprint ? _swimSprintSpeed : _swimSpeed) * (Underwater ? 0.85f : 1f) * SpeedMultiplier;
            Vector3 lWish = (transform.forward * pInput.y + transform.right * pInput.x) * lSpeed;
            Vector3 lPlanar = Vector3.MoveTowards(new Vector3(_velocity.x, 0, _velocity.z), lWish, 8f * dt);
            _velocity.x = lPlanar.x;
            _velocity.z = lPlanar.z;

            // At the surface, jump climbs out onto a ledge.
            if (GameInput.JumpPressed && !Underwater && TryMantle()) { Underwater = false; return; }

            if (Underwater)
            {
                Breath = Mathf.Max(0f, Breath - dt / _breathTime);
                if (Breath <= 0f && (_drownTimer -= dt) <= 0f) { _drownTimer = 1f; OnFallDamage?.Invoke(12f); }
            }
            else
            {
                Breath = Mathf.Min(1f, Breath + dt / 3f);
                _drownTimer = 0f;
            }
            _controller.Move((_velocity + lWater.Current) * dt);
            _airStartY = transform.position.y;
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
