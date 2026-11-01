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
        /// <summary>Scripted move over an obstacle: a mantle onto it, a vault over or through it, a climb up from a hang.</summary>
        Mantle,
        Zipline,
        Hidden,
        Locked,
        Swim,
        /// <summary>Hanging from a ledge by the hands: shimmy, climb up, drop, leap to the next ledge.</summary>
        LedgeHang
    }

    /// <summary>What the body is doing, for the third-person animation of remote players (sent as one byte).</summary>
    public enum MotionState : byte
    {
        Ground,
        Air,
        Slide,
        Crawl,
        Climb,
        Mantle,
        Vault,
        Hang,
        Swim,
        Zipline,
        Hidden
    }

    /// <summary>
    /// The owner's first-person movement on a <see cref="CharacterController"/>: walk, sprint,
    /// crouch (smooth height), crawl where a crouch doesn't fit (vents), jump with coyote time and
    /// buffer, a sprint-crouch slide, and the traversal of the sandbox:
    /// <list type="bullet">
    /// <item><b>Mantle</b> onto anything from 0.45 m to 2.4 m (crates, walls, roofs) - the ledge is
    /// probed at several heights and several depths past the face, so 30 cm walls and window sills
    /// count.</item>
    /// <item><b>Vault</b> over or through thin obstacles (fences, low walls, open windows) when there is
    /// floor on the other side.</item>
    /// <item><b>Ledge grab</b> in the air (automatic when the hands reach a ledge), then hang: shimmy
    /// sideways, climb up, drop, or leap to the next ledge with jump and a direction.</item>
    /// <item><b>Climb</b> drainpipes, ladders and ivy (<c>Climbable</c>), topping out onto the roof.</item>
    /// </list>
    /// plus ziplines, jump pads, hiding spots, swimming and diving
    /// (<see cref="Systems.WaterVolume"/>). Only enabled on the owning machine; the result reaches
    /// everyone through the NetworkTransform and <see cref="State"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FpsMotor : MonoBehaviour
    {
        [Header("Speeds")]
        [SerializeField] private float _walkSpeed = 4.4f;
        [SerializeField] private float _sprintSpeed = 7.2f;
        [SerializeField] private float _crouchSpeed = 2.3f;
        [SerializeField] private float _crawlSpeed = 1.6f;
        [SerializeField] private float _acceleration = 55f;
        [SerializeField] private float _airAcceleration = 14f;
        [SerializeField] private float _jumpHeight = 1.15f;
        [SerializeField] private float _gravity = -24f;

        [Header("Heights")]
        [SerializeField] private float _standHeight = 1.86f;
        [SerializeField] private float _crouchHeight = 1.1f;
        [SerializeField] private float _crawlHeight = 0.74f;
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
        [SerializeField] private float _mantleMinHeight = 0.45f;
        [SerializeField] private float _mantleMaxHeight = 2.4f;
        /// <summary>Thin obstacles up to this height are vaulted over rather than climbed onto.</summary>
        [SerializeField] private float _vaultMaxHeight = 1.3f;
        /// <summary>Feet-to-ledge height of a hang (the hands on the ledge, the body against the wall).</summary>
        [SerializeField] private float _hangDepth = 2.05f;
        [SerializeField] private float _shimmySpeed = 1.3f;

        public MotorMode Mode { get; private set; } = MotorMode.Walk;
        public bool Grounded { get; private set; }
        public bool Crouched { get; private set; }
        /// <summary>Crouched too low for a crouch: on all fours in a vent or under a low ceiling.</summary>
        public bool Crawling { get; private set; }
        public bool Sprinting { get; private set; }
        public bool Sliding => _crouchSliding;
        public Vector3 Velocity => _velocity;
        public float PlanarSpeed => new Vector3(_velocity.x, 0, _velocity.z).magnitude;
        public float EyeHeight => _controller.height - _eyeOffset;
        /// <summary>Camera offset that eases step-ups (stairs, kerbs) instead of popping the view.</summary>
        public float StepSmoothing => _stepOffset;
        public bool InGrass => _grassCount > 0;
        public bool Swimming => Mode == MotorMode.Swim;
        /// <summary>The eyes are below the water surface (hidden from sight, muffled, breath running out).</summary>
        public bool Underwater { get; private set; }
        /// <summary>Breath left, 1 = full.</summary>
        public float Breath { get; private set; } = 1f;
        public bool Aiming { get; set; }
        /// <summary>Multiplies the speed (carrying a heavy weapon, being sick).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>What the body is doing, for the third-person animation.</summary>
        public MotionState State => Mode switch
        {
            MotorMode.Climb => MotionState.Climb,
            MotorMode.Mantle => _vaulting ? MotionState.Vault : MotionState.Mantle,
            MotorMode.Zipline => MotionState.Zipline,
            MotorMode.Hidden => MotionState.Hidden,
            MotorMode.Swim => MotionState.Swim,
            MotorMode.LedgeHang => MotionState.Hang,
            _ => !Grounded ? MotionState.Air : _crouchSliding ? MotionState.Slide : Crawling ? MotionState.Crawl : MotionState.Ground
        };

        /// <summary>Raised on landing with the fall speed.</summary>
        public event Action<float> OnLanded;
        /// <summary>Raised on landing with the height dropped (for the noise it makes).</summary>
        public event Action<float> OnDrop;
        public event Action OnJumped;
        public event Action<float> OnSlide;
        /// <summary>A mantle, vault or ledge grab started (for the sound and the view).</summary>
        public event Action<MotionState> OnTraversal;
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
        private Systems.Zipline _zipline;
        private float _ziplineT;
        private Systems.HidingSpot _hidingSpot;
        private Vector3 _climbNormal;
        private float _drownTimer;
        private float _stepOffset;
        private float _regrabLock;

        // Scripted moves: a quadratic curve from -> control -> to over a duration.
        private Vector3 _moveFrom, _moveControl, _moveTo;
        private float _moveT, _moveTime;
        private bool _vaulting;
        private MotorMode _afterMove = MotorMode.Walk;

        private Ledge _hang;

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
            // A teleport is not a fall: measure drops from here.
            _airStartY = pPosition.y;
            _stepOffset = 0f;
            Mode = MotorMode.Walk;
        }

        /// <summary>Jump pads, explosions.</summary>
        public void Launch(Vector3 pVelocity, bool pReplace = true)
        {
            if (Mode == MotorMode.Hidden || Mode == MotorMode.Locked) return;
            if (Mode != MotorMode.Walk) SetMode(MotorMode.Walk);
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

        private void SetMode(MotorMode pMode)
        {
            if (Mode == pMode) return;
            if (!_controller.enabled) _controller.enabled = true;
            Mode = pMode;
        }

        // ---------------------------------------------------------------- traversal entry points

        public bool StartZipline(Systems.Zipline pZipline)
        {
            if (Mode != MotorMode.Walk && Mode != MotorMode.Climb && Mode != MotorMode.LedgeHang) return false;
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
            _regrabLock -= dt;
            switch (Mode)
            {
                case MotorMode.Walk: UpdateWalk(dt, lInput); break;
                case MotorMode.Climb: UpdateClimb(dt, lInput); break;
                case MotorMode.Mantle: UpdateMove(dt); break;
                case MotorMode.LedgeHang: UpdateHang(dt, lInput); break;
                case MotorMode.Zipline: UpdateZipline(dt); break;
                case MotorMode.Swim: UpdateSwim(dt, lInput); break;
                case MotorMode.Hidden:
                    if (GameInput.Interact || GameInput.JumpPressed) ExitHidingSpot();
                    break;
            }
            UpdateHeight(dt);
            _stepOffset = Mathf.Lerp(_stepOffset, 0f, 1f - Mathf.Exp(-14f * dt));
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
                    OnDrop?.Invoke(lDrop);
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
            // Coyote time, not the raw grounded flag: going down stairs never flickers the sprint.
            Sprinting = GameInput.Sprint && pInput.y > 0.3f && !Aiming && !_wantCrouch && _coyote > 0f && !Crawling;

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

            Crouched = _wantCrouch || _crouchSliding || !CanFit(_standHeight);
            // A crouch that doesn't fit ahead (a vent, a crawlspace) goes down on all fours; it stays so until a crouch fits again.
            Crawling = Crouched && !_crouchSliding && ((pInput.y > 0.2f && LowCeilingAhead()) || (Crawling && !CanFit(_crouchHeight)));

            float lSpeed = (Crawling ? _crawlSpeed : Crouched ? _crouchSpeed : Sprinting ? _sprintSpeed : _walkSpeed) * SpeedMultiplier;
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

            // Jump: mantle or vault what's ahead first, else jump.
            if (GameInput.JumpPressed) _jumpBuffer = 0.15f;
            else _jumpBuffer -= dt;

            bool lJumped = false;
            if (_jumpBuffer > 0f && !Crawling)
            {
                if (TryTraverse(MoveDirection(pInput), true)) { _jumpBuffer = 0f; return; }
                if (_coyote > 0f && !Crouched)
                {
                    _velocity.y = Mathf.Sqrt(-2f * _gravity * _jumpHeight);
                    _coyote = 0f;
                    _jumpBuffer = 0f;
                    Grounded = false;
                    lJumped = true;
                    OnJumped?.Invoke();
                }
            }

            // In the air, pushing toward a ledge: the hands grab it (or a low one is climbed straight onto).
            if (!Grounded && !lJumped && pInput.y > 0.3f && _velocity.y < 3.5f && _regrabLock <= 0f && TryAirGrab(MoveDirection(pInput))) return;

            if (Grounded && _velocity.y < 0f) _velocity.y = -3f;
            else _velocity.y += _gravity * dt;

            _velocity.x = lPlanar.x;
            _velocity.z = lPlanar.z;
            float lBeforeY = transform.position.y;
            _controller.Move(_velocity * dt);

            // Down stairs and slopes: stay glued to the ground instead of hopping off every step.
            if (lWasGrounded && !lJumped && !_controller.isGrounded && _velocity.y <= 0f && !_crouchSliding) SnapToGround();
            // Up a step: the body pops up, the eyes follow smoothly.
            float lRise = transform.position.y - lBeforeY;
            if (lWasGrounded && lRise > 0.04f && lRise < 0.6f) _stepOffset = Mathf.Max(-0.6f, _stepOffset - lRise);

            // Climbable surface ahead and pushing forward: grab it.
            if (pInput.y > 0.5f && TryGrabClimbable()) SetMode(MotorMode.Climb);

            // Deep enough water: swim.
            if (Systems.WaterVolume.At(transform.position + Vector3.up * 0.1f, out float lSurface) != null && lSurface - transform.position.y > SWIM_DEPTH)
            {
                OnSplash?.Invoke(Mathf.Abs(_velocity.y) + PlanarSpeed * 0.3f);
                _crouchSliding = false;
                Crawling = false;
                SetMode(MotorMode.Swim);
            }
        }

        /// <summary>The flat direction the player pushes toward (their facing when only pressing forward or nothing).</summary>
        private Vector3 MoveDirection(Vector2 pInput)
        {
            Vector3 lDir = transform.forward * Mathf.Max(0.35f, pInput.y) + transform.right * pInput.x * 0.5f;
            lDir.y = 0f;
            return lDir.sqrMagnitude > 0.01f ? lDir.normalized : transform.forward;
        }

        private void SnapToGround()
        {
            float lRadius = _controller.radius * 0.9f;
            Vector3 lOrigin = transform.position + Vector3.up * (lRadius + 0.05f);
            if (!Physics.SphereCast(lOrigin, lRadius, Vector3.down, out RaycastHit lHit, 0.6f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) return;
            if (lHit.normal.y < 0.6f) return;
            _controller.Move(Vector3.down * (lHit.distance + 0.02f));
            Grounded = _controller.isGrounded;
        }

        /// <summary>Water deeper than this at the feet makes the player swim.</summary>
        private const float SWIM_DEPTH = 1.25f;

        private void UpdateSwim(float dt, Vector2 pInput)
        {
            Systems.WaterVolume lWater = Systems.WaterVolume.At(transform.position + Vector3.up * 0.5f, out float lSurface);
            Grounded = false;
            Crouched = false;
            Crawling = false;
            Sprinting = false;
            _crouchSliding = false;
            if (lWater == null || (lSurface - transform.position.y < SWIM_DEPTH - 0.2f && _controller.isGrounded))
            {
                SetMode(MotorMode.Walk);
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
            if (GameInput.JumpPressed && !Underwater && TryTraverse(transform.forward, false)) { Underwater = false; return; }

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

        // ---------------------------------------------------------------- heights

        /// <summary>Is there room for a body this tall where the player stands?</summary>
        private bool CanFit(float pHeight)
        {
            if (_controller.height >= pHeight - 0.01f) return true;
            float lRadius = _controller.radius * 0.95f;
            Vector3 lBottom = transform.position + Vector3.up * (lRadius + 0.05f);
            return !Physics.SphereCast(lBottom, lRadius, Vector3.up, out _, pHeight - lRadius * 2f, LedgeMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>A crouch doesn't pass just ahead but a crawl does (a vent mouth, a crawlspace).</summary>
        private bool LowCeilingAhead()
        {
            float lRadius = _controller.radius * 0.9f;
            Vector3 lFeet = transform.position + Vector3.up * (lRadius + 0.06f);
            bool lCrouchBlocked = Physics.CapsuleCast(lFeet, transform.position + Vector3.up * (_crouchHeight - lRadius), lRadius, transform.forward, 0.6f, LedgeMask, QueryTriggerInteraction.Ignore);
            if (!lCrouchBlocked) return false;
            return !Physics.CapsuleCast(lFeet, transform.position + Vector3.up * (_crawlHeight - lRadius), lRadius, transform.forward, 0.6f, LedgeMask, QueryTriggerInteraction.Ignore);
        }

        private void UpdateHeight(float dt)
        {
            float lTarget = Mode == MotorMode.Hidden ? _crouchHeight
                : Crawling ? _crawlHeight
                : Crouched || Mode == MotorMode.Mantle ? _crouchHeight
                : _standHeight;
            float lHeight = Mathf.MoveTowards(_controller.height, lTarget, dt * 6f);
            _controller.height = lHeight;
            _controller.center = new Vector3(0, lHeight * 0.5f, 0);
        }

        // ---------------------------------------------------------------- ledges

        /// <summary>A ledge found ahead: the wall face, its outward normal, the top surface.</summary>
        private struct Ledge
        {
            public Vector3 wall, normal, top;
            public float rise;
            /// <summary>Under ~0.55 m deep: a wall top, a sill, a fence - something to go over.</summary>
            public bool thin;
        }

        /// <summary>Everything solid except the invisible climb volumes (a drainpipe's box is not a ledge).</summary>
        private static int LedgeMask => GameLayers.WorldMask & ~(1 << GameLayers.CLIMBABLE);

        private static readonly float[] FACE_HEIGHTS = { 0.3f, 0.7f, 1.1f, 1.55f, 2.0f, 2.4f };
        private static readonly float[] TOP_DEPTHS = { 0.08f, 0.16f, 0.28f, 0.42f };

        /// <summary>
        /// Looks for a ledge ahead whose top is between <paramref name="pMinRise"/> and
        /// <paramref name="pMaxRise"/> above <paramref name="pFeet"/>: sweeps for a wall at several
        /// heights, then probes down for its top at several depths past the face (so a 30 cm wall or a
        /// window sill is found, not the floor behind it).
        /// </summary>
        private bool FindLedge(Vector3 pFeet, Vector3 pDir, float pMinRise, float pMaxRise, out Ledge pLedge)
        {
            pLedge = default;
            foreach (float lHeight in FACE_HEIGHTS)
            {
                if (lHeight > pMaxRise + 0.05f) break;
                if (lHeight < pMinRise - 0.6f) continue;
                Vector3 lOrigin = pFeet + Vector3.up * lHeight - pDir * 0.1f;
                if (!Physics.SphereCast(lOrigin, 0.16f, pDir, out RaycastHit lWall, _controller.radius + 0.7f, LedgeMask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 lNormal = new Vector3(lWall.normal.x, 0f, lWall.normal.z);
                if (lNormal.sqrMagnitude < 0.3f) continue;
                lNormal.Normalize();

                foreach (float lDepth in TOP_DEPTHS)
                {
                    Vector3 lAbove = lWall.point - lNormal * lDepth;
                    lAbove.y = pFeet.y + pMaxRise + 0.3f;
                    float lReach = pMaxRise + 0.3f - pMinRise + 0.1f;
                    if (!Physics.SphereCast(lAbove, 0.05f, Vector3.down, out RaycastHit lTop, lReach, LedgeMask, QueryTriggerInteraction.Ignore)) continue;
                    if (lTop.normal.y < 0.7f) continue;
                    float lRise = lTop.point.y - pFeet.y;
                    if (lRise < pMinRise || lRise > pMaxRise) continue;
                    // Nothing as high a bit further in: a thin top.
                    Vector3 lBeyond = lTop.point - lNormal * 0.55f + Vector3.up * 0.25f;
                    bool lThin = !Physics.Raycast(lBeyond, Vector3.down, 0.5f, LedgeMask, QueryTriggerInteraction.Ignore);
                    pLedge = new Ledge { wall = lWall.point, normal = lNormal, top = lTop.point, rise = lRise, thin = lThin };
                    return true;
                }
                // The first wall met has no reachable top: don't look for ledges behind it.
                return false;
            }
            return false;
        }

        /// <summary>Room for a crouched body standing at <paramref name="pFeet"/>.</summary>
        private bool FitsCrouched(Vector3 pFeet, float pRadiusFactor = 0.9f)
        {
            float lRadius = _controller.radius * pRadiusFactor;
            return !Physics.CheckCapsule(pFeet + Vector3.up * (lRadius + 0.06f), pFeet + Vector3.up * (_crouchHeight - lRadius), lRadius, LedgeMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Where a body stands on a ledge: just inside its edge, or centred on a thin top.</summary>
        private Vector3 StandPoint(in Ledge pLedge) => pLedge.top - pLedge.normal * (pLedge.thin ? 0.15f : _controller.radius + 0.08f) + Vector3.up * 0.03f;

        /// <summary>Floor on the far side of a thin obstacle (a window, a wall, a fence): where a vault lands.</summary>
        private bool FindLanding(in Ledge pLedge, out Vector3 pLanding)
        {
            pLanding = default;
            Vector3 lOver = pLedge.top - pLedge.normal * (0.55f + _controller.radius);
            Vector3 lStart = new Vector3(lOver.x, pLedge.top.y + 0.3f, lOver.z);
            if (!Physics.Raycast(lStart, Vector3.down, out RaycastHit lFloor, 4f, LedgeMask, QueryTriggerInteraction.Ignore)) return false;
            if (lFloor.normal.y < 0.7f) return false;
            pLanding = lFloor.point + Vector3.up * 0.03f;
            // Room to pass over the top crouched (an open window), and to land.
            Vector3 lCrossing = pLedge.top - pLedge.normal * 0.2f + Vector3.up * 0.05f;
            return FitsCrouched(lCrossing, 0.7f) && FitsCrouched(pLanding);
        }

        /// <summary>
        /// Jump pressed (or climbing out of water / off a climb): vault a thin obstacle, else mantle
        /// onto what's ahead. True if a move started.
        /// </summary>
        private bool TryTraverse(Vector3 pDir, bool pAllowVault)
        {
            if (!FindLedge(transform.position, pDir, _mantleMinHeight, _mantleMaxHeight, out Ledge lLedge)) return false;

            if (pAllowVault && lLedge.thin && FindLanding(lLedge, out Vector3 lLanding))
            {
                // Low: a quick vault. High (a garden wall): over the top and down, slower.
                float lTime = lLedge.rise <= _vaultMaxHeight ? 0.42f + lLedge.rise * 0.08f : 0.7f + (lLedge.rise - _vaultMaxHeight) * 0.25f;
                Vector3 lControl = lLedge.top - lLedge.normal * 0.2f + Vector3.up * 0.55f;
                StartMove(lLanding, lControl, lTime, true, MotorMode.Walk);
                return true;
            }

            Vector3 lStand = StandPoint(lLedge);
            if (!FitsCrouched(lStand)) return false;
            Vector3 lUp = new Vector3(transform.position.x, lStand.y + 0.25f, transform.position.z);
            StartMove(lStand, Vector3.Lerp(lUp, lStand, 0.25f), 0.28f + lLedge.rise * 0.12f, false, MotorMode.Walk);
            return true;
        }

        /// <summary>
        /// Airborne and pushing toward a wall: a low ledge (under the chest) is climbed onto at once;
        /// a higher one within reach of the hands is grabbed and hung from.
        /// </summary>
        private bool TryAirGrab(Vector3 pDir)
        {
            if (FindLedge(transform.position, pDir, 0.2f, 1.35f, out Ledge lLow))
            {
                Vector3 lStand = StandPoint(lLow);
                if (FitsCrouched(lStand))
                {
                    StartMove(lStand, Vector3.Lerp(transform.position, lStand, 0.5f) + Vector3.up * 0.3f, 0.3f + lLow.rise * 0.1f, false, MotorMode.Walk);
                    return true;
                }
            }
            if (FindLedge(transform.position, pDir, 1.35f, _hangDepth + 0.35f, out Ledge lHigh)) return StartHang(lHigh);
            return false;
        }

        private bool StartHang(in Ledge pLedge)
        {
            Vector3 lFeet = HangPosition(pLedge);
            // The body hangs against the wall: there must be room for it there.
            float lRadius = _controller.radius * 0.85f;
            if (Physics.CheckCapsule(lFeet + Vector3.up * (lRadius + 0.05f), lFeet + Vector3.up * (_standHeight - lRadius - 0.1f), lRadius, LedgeMask, QueryTriggerInteraction.Ignore)) return false;
            _hang = pLedge;
            _velocity = Vector3.zero;
            _crouchSliding = false;
            StartMove(lFeet, Vector3.Lerp(transform.position, lFeet, 0.5f), 0.18f, false, MotorMode.LedgeHang);
            OnTraversal?.Invoke(MotionState.Hang);
            return true;
        }

        /// <summary>Where the feet are when hanging from a ledge: the hands on its edge, the chest against the wall.</summary>
        private Vector3 HangPosition(in Ledge pLedge)
        {
            Vector3 lWall = new Vector3(pLedge.wall.x, pLedge.top.y - _hangDepth, pLedge.wall.z);
            return lWall + pLedge.normal * (_controller.radius + 0.06f);
        }

        private void StartMove(Vector3 pTo, Vector3 pControl, float pTime, bool pVault, MotorMode pAfter)
        {
            _moveFrom = transform.position;
            _moveControl = pControl;
            _moveTo = pTo;
            _moveT = 0f;
            _moveTime = Mathf.Max(0.05f, pTime);
            _vaulting = pVault;
            _afterMove = pAfter;
            _velocity = Vector3.zero;
            Mode = MotorMode.Mantle;
            if (pAfter != MotorMode.LedgeHang)
            {
                OnJumped?.Invoke();
                OnTraversal?.Invoke(pVault ? MotionState.Vault : MotionState.Mantle);
            }
        }

        private void UpdateMove(float dt)
        {
            _moveT += dt / _moveTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_moveT));
            float u = 1f - t;
            Vector3 lPos = u * u * _moveFrom + 2f * u * t * _moveControl + t * t * _moveTo;
            _controller.enabled = false;
            transform.position = lPos;
            _controller.enabled = true;
            Crouched = _afterMove != MotorMode.LedgeHang;
            Crawling = false;
            Grounded = false;
            if (_moveT < 1f) return;

            Mode = _afterMove;
            _vaulting = false;
            _airStartY = transform.position.y;
            if (_afterMove == MotorMode.Walk)
            {
                Grounded = true;
                _coyote = 0.12f;
                OnLanded?.Invoke(2f);
            }
        }

        private void UpdateHang(float dt, Vector2 pInput)
        {
            Grounded = false;
            Crouched = false;
            Crawling = false;
            Sprinting = false;
            _velocity = Vector3.zero;
            _airStartY = transform.position.y;
            Vector3 lToWall = -_hang.normal;
            Vector3 lRight = Vector3.Cross(Vector3.up, lToWall).normalized;

            // Drop: crouch or pull back.
            if (GameInput.CrouchPressed || (pInput.y < -0.5f && !GameInput.JumpPressed))
            {
                ReleaseHang(_hang.normal * 1.2f);
                return;
            }

            if (GameInput.JumpPressed)
            {
                if (Mathf.Abs(pInput.x) > 0.4f)
                {
                    // Leap sideways to the next ledge within two metres.
                    for (float lDistance = 0.8f; lDistance <= 2.2f; lDistance += 0.35f)
                    {
                        Vector3 lFeet = transform.position + lRight * Mathf.Sign(pInput.x) * lDistance;
                        if (FindLedge(lFeet, lToWall, _hangDepth - 0.7f, _hangDepth + 0.7f, out Ledge lNext) && StartHang(lNext)) return;
                    }
                }
                else if (pInput.y < -0.3f)
                {
                    // Kick off the wall.
                    ReleaseHang(_hang.normal * 4.5f + Vector3.up * 4.5f);
                    OnJumped?.Invoke();
                    return;
                }
                else if (ClimbUp()) return;
            }
            if (pInput.y > 0.5f && ClimbUp()) return;

            // Shimmy along the ledge while it goes on.
            if (Mathf.Abs(pInput.x) > 0.2f)
            {
                Vector3 lStep = lRight * pInput.x * _shimmySpeed * dt;
                Vector3 lProbe = transform.position + lStep + lRight * Mathf.Sign(pInput.x) * 0.3f;
                if (FindLedge(lProbe, lToWall, _hangDepth - 0.35f, _hangDepth + 0.35f, out Ledge lAlong) && Vector3.Angle(lAlong.normal, _hang.normal) < 50f)
                {
                    _hang = lAlong;
                    Vector3 lTarget = HangPosition(lAlong);
                    Vector3 lPos = transform.position + lStep;
                    lPos.y = Mathf.MoveTowards(lPos.y, lTarget.y, dt * 2f);
                    // Keep the chest against the wall as it turns or steps.
                    Vector3 lFlat = lTarget - lPos;
                    lFlat -= lRight * Vector3.Dot(lFlat, lRight);
                    lPos += Vector3.ClampMagnitude(lFlat, dt * 2f);
                    _controller.enabled = false;
                    transform.position = lPos;
                    _controller.enabled = true;
                }
            }
        }

        private bool ClimbUp()
        {
            Vector3 lStand = StandPoint(_hang);
            if (!FitsCrouched(lStand)) return false;
            Vector3 lControl = new Vector3(transform.position.x, _hang.top.y + 0.35f, transform.position.z);
            StartMove(lStand, lControl, 0.55f, false, MotorMode.Walk);
            return true;
        }

        private void ReleaseHang(Vector3 pVelocity)
        {
            SetMode(MotorMode.Walk);
            _velocity = pVelocity;
            _regrabLock = 0.45f;
        }

        // ---------------------------------------------------------------- climbables

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
            Crawling = false;
            Vector3 lChest = transform.position + Vector3.up * 1f;
            bool lWall = Physics.Raycast(lChest, -_climbNormal, out RaycastHit lHit, 1f, 1 << GameLayers.CLIMBABLE, QueryTriggerInteraction.Ignore);
            if (lWall) _climbNormal = lHit.normal;

            if (GameInput.JumpPressed)
            {
                // At the top: up onto the roof. Elsewhere: kick off.
                if (TryTraverse(-_climbNormal, false)) return;
                SetMode(MotorMode.Walk);
                _velocity = _climbNormal * 4.5f + Vector3.up * 4f;
                _regrabLock = 0.4f;
                OnJumped?.Invoke();
                return;
            }

            if (!lWall)
            {
                SetMode(MotorMode.Walk);
                // Off the top: climb onto the ledge (the parapet, the roof). Off a side: let go.
                if (pInput.y > 0.2f && (TryTraverse(-_climbNormal, false) || TryAirGrab(-_climbNormal))) return;
                _velocity = pInput.y > 0.2f ? Vector3.up * 2.5f - _climbNormal * 1.5f : _climbNormal * 0.8f;
                _regrabLock = 0.3f;
                return;
            }

            // Near the top, pushing up: the roof edge is in reach of the hands.
            if (pInput.y > 0.5f && !Physics.Raycast(transform.position + Vector3.up * 1.9f, -_climbNormal, 1f, 1 << GameLayers.CLIMBABLE, QueryTriggerInteraction.Ignore)
                && TryTraverse(-_climbNormal, false)) return;

            Vector3 lRight = Vector3.Cross(Vector3.up, _climbNormal).normalized;
            Vector3 lMove = Vector3.up * pInput.y * _climbSpeed - lRight * pInput.x * _climbSpeed * 0.7f - _climbNormal * 0.5f;
            _controller.Move(lMove * dt);
            if (pInput.y < -0.5f && _controller.isGrounded) SetMode(MotorMode.Walk);
            _airStartY = transform.position.y;
        }

        private void UpdateZipline(float dt)
        {
            if (_zipline == null) { SetMode(MotorMode.Walk); return; }
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
                SetMode(MotorMode.Walk);
            }
        }
    }
}
