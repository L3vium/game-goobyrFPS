using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>What a standing NPC is busy with (sent by the host as one byte).</summary>
    public enum BodyPose : byte { None, Phone, Smoke, Sit, Lean, LookAround, Cower, ArmedIdle }

    /// <summary>
    /// Drives a <see cref="HumanBody"/>'s Animator (<c>Characters/Human</c>, built by
    /// <c>Goobyr/Build/Characters</c>) from what the character is doing.
    /// <para>
    /// <b>Velocity</b> comes from the owner (motor or NavMeshAgent) when there is one, otherwise from
    /// the transform over a short window (the interpolated NetworkTransform), smoothed - never from a
    /// raw frame delta. <b>Gait</b>: the speed picks a point in the locomotion tree (walk ring 1, run 2,
    /// sprint 3, capped by intent: a player only shows the sprint clip while sprinting) and the pace
    /// parameter plays it at the speed that keeps the feet planted. <b>Air</b> is debounced and checked
    /// against the ground under the feet, so stairs and slopes never flash a fall.
    /// </para>
    /// <para>
    /// States are chosen here and reached by cross-fades: base layer (locomotion, crouch, crawl, slide,
    /// jump/fall/land, vault, mantle, ladder, hang, swim, NPC poses), upper-body layer (weapon hold,
    /// aim, fire, reload, punch, throw, phone), additive hit flinches. IK plants the feet on slopes and
    /// stairs, turns the head and spine to the aim pitch and puts the left hand on long guns.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(HumanBody))]
    public class HumanAnimator : MonoBehaviour
    {
        // ---------------------------------------------------------------- controller contract
        public const string MOVE_X = "MoveX", MOVE_Z = "MoveZ", PACE = "Pace", HANG_X = "HangX", SWIM = "Swim";
        public const string S_EMPTY = "Empty", S_LOCOMOTION = "Locomotion", S_CROUCH = "Crouch", S_CRAWL = "Crawl", S_SLIDE = "Slide";
        public const string S_JUMP = "Jump", S_FALL = "Fall", S_LAND = "Land", S_VAULT = "Vault", S_MANTLE = "Mantle", S_CLIMB_UP = "ClimbUp";
        public const string S_LADDER = "Ladder", S_HANG = "Hang", S_SWIM = "Swim";
        public const string S_PHONE = "Phone", S_SMOKE = "Smoke", S_SIT = "Sit", S_LEAN = "Lean", S_LOOK_AROUND = "LookAround", S_COWER = "Cower";
        public const string S_PANIC = "Panic", S_PATROL = "Patrol", S_ARMED_IDLE = "ArmedIdle";
        public const string S_TAKEDOWN_ATTACKER = "TakedownAttacker", S_TAKEDOWN_VICTIM = "TakedownVictim", S_DEATH_F = "DeathF", S_DEATH_B = "DeathB";
        public const string S_PISTOL_READY = "PistolReady", S_RIFLE_READY = "RifleReady", S_AIM_PISTOL = "AimPistol", S_AIM_RIFLE = "AimRifle";
        public const string S_FIRE_PISTOL = "FirePistol", S_FIRE_RIFLE = "FireRifle", S_RELOAD = "Reload", S_PUNCH = "Punch", S_THROW = "Throw";
        public const string S_HIT_F = "HitF", S_HIT_B = "HitB";

        private const int BODY = 0, UPPER = 1, HIT = 2;

        private static readonly int MoveXId = Animator.StringToHash(MOVE_X), MoveZId = Animator.StringToHash(MOVE_Z), PaceId = Animator.StringToHash(PACE);
        private static readonly int HangXId = Animator.StringToHash(HANG_X), SwimId = Animator.StringToHash(SWIM);

        // Ground speed of each clip (m/s), measured on the stance foot of the reference body (Chr_Agent,
        // human scale REF_SCALE); a body's stride scales with its own human scale. The pace parameter
        // plays a clip at speed / its ground speed so the planted foot does not slide.
        private static readonly Vector4 WALK = new Vector4(1.29f, 1.61f, 1.70f, 1.70f);   // forward, back, left, right
        // The backward run ring is the walk-back clip played faster (the backpedal clip is slower than the walk).
        private static readonly Vector4 RUN = new Vector4(3.05f, 1.61f, 1.90f, 1.90f);
        private const float SPRINT = 3.79f;
        private static readonly Vector4 CROUCH = new Vector4(1.12f, 1.12f, 0.9f, 0.9f);
        private const float CRAWL = 0.58f, PANIC = 3.07f, SWIM_SPEED = 1.1f, LADDER = 0.36f, SHIMMY = 0.22f;
        private const float REF_SCALE = 1.074f;
        /// <summary>Above this a non-sprinting character shows the run ring.</summary>
        private const float RUN_FROM = 2.7f;

        // ---------------------------------------------------------------- inputs (set every frame by the owner)

        public MotionState Motion { get; set; }
        public bool Crouched { get; set; }
        public bool Sprinting { get; set; }
        public bool Aiming { get; set; }
        /// <summary>Degrees, positive looks down (camera convention).</summary>
        public float AimPitch { get; set; }
        public HeldKind Held { get; set; }
        public BodyPose Pose { get; set; }
        /// <summary>Running in panic (arms over the head).</summary>
        public bool Panic { get; set; }
        /// <summary>On the phone while walking (upper body only).</summary>
        public bool Calling { get; set; }
        /// <summary>Velocity given by the owner (motor, agent); null to measure it from the transform.</summary>
        public Vector3? Velocity { get; set; }
        /// <summary>A point the head turns to (an NPC staring at a suspect); null to look along the aim.</summary>
        public Vector3? LookAt { get; set; }
        /// <summary>Foot IK and look IK (off for the owner's own shadow-only body).</summary>
        public bool UseIK { get; set; } = true;

        public Vector3 MeasuredVelocity => _velocity;
        public bool Airborne => _airborne;

        private HumanBody _body;
        private Animator _animator;
        private int _baseState, _upperState;
        private float _upperWeight;
        private float _actionUntil;
        private int _actionState;
        private float _lockUntil;

        // Velocity window.
        private readonly Vector3[] _positions = new Vector3[12];
        private readonly float[] _times = new float[12];
        private int _head;
        private Vector3 _velocity;

        // Air.
        private float _airTime, _groundTime;
        private bool _airborne;
        private float _fallSpeed;
        private float _landUntil;
        private bool _jumpRequested;

        // Gait.
        private float _gait;
        private Vector2 _move;
        private MotionState _lastMotion;

        // IK.
        private float _lookWeight, _footWeight, _supportWeight, _alignWeight;
        private float _pelvisOffset;
        private static readonly RaycastHit[] _hits = new RaycastHit[4];

        private void Awake()
        {
            _body = GetComponent<HumanBody>();
            ResetWindow();
            // The body may have built itself before this component was added.
            if (_body.Built && _body.Model != null)
            {
                HumanIK lIK = _body.Model.GetComponent<HumanIK>();
                if (lIK != null) lIK.Target = this;
                Bind(_body.Animator);
            }
        }

        private void ResetWindow()
        {
            for (int i = 0; i < _positions.Length; i++) { _positions[i] = transform.position; _times[i] = Time.time - (_positions.Length - i) * 0.02f; }
        }

        /// <summary>Called by the body when its model (and Animator) changes.</summary>
        public void Bind(Animator pAnimator)
        {
            _animator = pAnimator;
            _baseState = _upperState = 0;
            _upperWeight = 0f;
            if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
            {
                Play(BODY, S_LOCOMOTION, 0f);
                _animator.SetLayerWeight(UPPER, 0f);
                _animator.Update(0f);
            }
        }

        // ---------------------------------------------------------------- actions

        public void Fire()
        {
            if (Held != HeldKind.Pistol && Held != HeldKind.Rifle) return;
            Action(Held == HeldKind.Rifle ? S_FIRE_RIFLE : S_FIRE_PISTOL, Held == HeldKind.Rifle ? 0.2f : 0.3f, 0.03f);
        }

        public void Reload(float pDuration) => Action(S_RELOAD, Mathf.Max(0.6f, pDuration), 0.12f);
        public void Punch() => Action(S_PUNCH, 0.75f, 0.06f);
        public void Throw() => Action(S_THROW, 1.15f, 0.08f);

        /// <summary>A hit from <paramref name="pWorldDirection"/> (the bullet's travel): flinch forward or back.</summary>
        public void Hit(Vector3 pWorldDirection)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;
            bool lFromBack = Vector3.Dot(pWorldDirection, transform.forward) > 0f;
            _animator.CrossFadeInFixedTime(lFromBack ? S_HIT_B : S_HIT_F, 0.04f, HIT, 0f);
        }

        /// <summary>The owner jumped (remote players infer it from the vertical speed).</summary>
        public void Jumped() => _jumpRequested = true;

        /// <summary>The paired takedown: the attacker's choke, or the victim's struggle (both 3 s clips).</summary>
        public void Takedown(bool pVictim)
        {
            if (_animator == null) return;
            Play(BODY, pVictim ? S_TAKEDOWN_VICTIM : S_TAKEDOWN_ATTACKER, 0.1f);
            _lockUntil = Time.time + (pVictim ? 1.6f : 1.2f);
            _animator.SetLayerWeight(UPPER, 0f);
            _upperWeight = 0f;
        }

        private void Action(string pState, float pDuration, float pFade)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;
            _actionState = Animator.StringToHash(pState);
            _actionUntil = Time.time + pDuration;
            _animator.CrossFadeInFixedTime(_actionState, pFade, UPPER, 0f);
            _upperState = _actionState;
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            MeasureVelocity(dt);
            Vector3 lLocal = transform.InverseTransformDirection(new Vector3(_velocity.x, 0f, _velocity.z));
            float lSpeed = new Vector2(lLocal.x, lLocal.z).magnitude;
            UpdateAir(dt);

            if (Time.time < _lockUntil) { _lastMotion = Motion; return; }

            float lPace = 1f;
            string lState = ChooseBase(lLocal, lSpeed, dt, ref lPace);
            _animator.SetFloat(PaceId, lPace, 0.1f, dt);
            int lHash = Animator.StringToHash(lState);
            if (lHash != _baseState) Play(BODY, lState, FadeFor(lState));
            _lastMotion = Motion;

            UpdateUpper(lState, dt);
        }

        private void MeasureVelocity(float dt)
        {
            _head = (_head + 1) % _positions.Length;
            _positions[_head] = transform.position;
            _times[_head] = Time.time;
            Vector3 lTarget;
            if (Velocity.HasValue) lTarget = Velocity.Value;
            else
            {
                // Over ~0.15 s: the interpolated NetworkTransform moves in small uneven steps.
                int lOld = _head;
                for (int i = 1; i < _positions.Length; i++)
                {
                    int lIndex = (_head - i + _positions.Length) % _positions.Length;
                    lOld = lIndex;
                    if (Time.time - _times[lIndex] >= 0.15f) break;
                }
                float lSpan = Time.time - _times[lOld];
                lTarget = lSpan > 0.0001f ? (transform.position - _positions[lOld]) / lSpan : Vector3.zero;
                if (lTarget.sqrMagnitude > 400f) lTarget = Vector3.zero; // a teleport
            }
            _velocity = Vector3.Lerp(_velocity, lTarget, 1f - Mathf.Exp(-14f * dt));
        }

        /// <summary>
        /// Airborne only after a moment in the air with no ground right under the feet (a step down a
        /// stair or a slope keeps the walk), or at once on a jump. Landing needs the ground back.
        /// </summary>
        private void UpdateAir(float dt)
        {
            bool lAirMotion = Motion == MotionState.Air;
            bool lGroundBelow = Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, 0.75f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            if (lAirMotion && (!lGroundBelow || _velocity.y > 1.5f)) { _airTime += dt; _groundTime = 0f; }
            else { _groundTime += dt; if (_groundTime > 0.06f || !lAirMotion) _airTime = 0f; }

            bool lJump = _jumpRequested || (lAirMotion && _velocity.y > 2.2f && _airTime > 0.02f);
            if (!_airborne && lAirMotion && (lJump || _airTime > 0.18f))
            {
                _airborne = true;
                _fallSpeed = 0f;
                if (lJump) Play(BODY, S_JUMP, 0.08f);
            }
            _jumpRequested = false;
            if (_airborne)
            {
                _fallSpeed = Mathf.Max(_fallSpeed, -_velocity.y);
                if (!lAirMotion || (Motion == MotionState.Air && lGroundBelow && _velocity.y < 0.5f && _groundTime > 0.05f))
                {
                    _airborne = false;
                    // A real drop gets the landing absorb, unless already running on.
                    if (_fallSpeed > 5.5f && new Vector2(_velocity.x, _velocity.z).magnitude < 2.5f) _landUntil = Time.time + 0.45f;
                }
            }
        }

        private string ChooseBase(Vector3 pLocal, float pSpeed, float dt, ref float pPace)
        {
            switch (Motion)
            {
                case MotionState.Climb:
                    pPace = Mathf.Clamp(Mathf.Abs(_velocity.y) / LADDER, 0f, 3.2f);
                    return S_LADDER;
                case MotionState.Hang:
                case MotionState.Zipline:
                {
                    float lSide = Motion == MotionState.Zipline ? 0f : Mathf.Clamp(pLocal.x / 0.6f, -1f, 1f);
                    _animator.SetFloat(HangXId, lSide, 0.12f, dt);
                    pPace = Mathf.Abs(lSide) > 0.2f ? Mathf.Clamp(Mathf.Abs(pLocal.x) / SHIMMY, 1f, 3.2f) : 1f;
                    return S_HANG;
                }
                case MotionState.Mantle:
                    pPace = 2.6f;
                    return _lastMotion == MotionState.Hang || _baseState == Animator.StringToHash(S_CLIMB_UP) ? S_CLIMB_UP : S_MANTLE;
                case MotionState.Vault:
                    pPace = 2.8f;
                    return S_VAULT;
                case MotionState.Swim:
                    _animator.SetFloat(SwimId, Mathf.Clamp01(pSpeed / SWIM_SPEED), 0.15f, dt);
                    pPace = pSpeed > SWIM_SPEED ? Mathf.Clamp(pSpeed / SWIM_SPEED, 1f, 1.8f) : 1f;
                    return S_SWIM;
                case MotionState.Slide:
                    return S_SLIDE;
                case MotionState.Crawl:
                    pPace = Mathf.Clamp(pSpeed / (CRAWL * _animator.humanScale / REF_SCALE), 0f, 2.6f);
                    return S_CRAWL;
                case MotionState.Hidden:
                    return S_CROUCH;
            }

            if (_airborne) return _baseState == Animator.StringToHash(S_JUMP) ? S_JUMP : S_FALL;
            if (Time.time < _landUntil && pSpeed < 1.5f) return S_LAND;

            if (Crouched)
            {
                SetGait(pLocal, pSpeed, 1, CROUCH, CROUCH, 0f, dt, out pPace, 2.2f);
                return S_CROUCH;
            }
            if (Panic && pSpeed > 1f)
            {
                pPace = Mathf.Clamp(pSpeed / (PANIC * _animator.humanScale / REF_SCALE), 0.7f, 1.8f);
                return S_PANIC;
            }
            if (pSpeed < 0.25f && Pose != BodyPose.None)
            {
                switch (Pose)
                {
                    case BodyPose.Phone: return S_PHONE;
                    case BodyPose.Smoke: return S_SMOKE;
                    case BodyPose.Sit: return S_SIT;
                    case BodyPose.Lean: return S_LEAN;
                    case BodyPose.LookAround: return S_LOOK_AROUND;
                    case BodyPose.Cower: return S_COWER;
                    case BodyPose.ArmedIdle: return S_ARMED_IDLE;
                }
            }
            int lTier = Sprinting && pLocal.z > 0.5f * pSpeed ? 3 : pSpeed > RUN_FROM ? 2 : 1;
            SetGait(pLocal, pSpeed, lTier, WALK, RUN, SPRINT, dt, out pPace, 2f);
            return S_LOCOMOTION;
        }

        /// <summary>
        /// The point in gait space (direction × ring) for this speed, and the pace that matches the
        /// blended clips' own speed to it.
        /// </summary>
        private void SetGait(Vector3 pLocal, float pSpeed, int pMaxTier, Vector4 pRing1, Vector4 pRing2, float pSprint, float dt, out float pPace, float pMaxPace)
        {
            Vector2 lDir = pSpeed > 0.05f ? new Vector2(pLocal.x, pLocal.z) / pSpeed : Vector2.up;
            float lStride = _animator.humanScale / REF_SCALE;
            float s1 = Directional(pRing1, lDir) * lStride, s2 = Mathf.Max(Directional(pRing2, lDir) * lStride, s1 * 1.05f);
            float s3 = pSprint > 0f ? Mathf.Max(pSprint * lStride, s2 * 1.05f) : s2;
            // A short walker hurrying (an NPC's brisk walk) breaks into the run ring rather than scurrying.
            if (pMaxTier == 1 && pRing2.x > pRing1.x && pSpeed > s1 * 1.45f) pMaxTier = 2;
            float lGait = pSpeed <= s1 ? pSpeed / s1 : pSpeed <= s2 ? 1f + (pSpeed - s1) / (s2 - s1) : 2f + Mathf.Clamp01((pSpeed - s2) / (s3 - s2));
            lGait = Mathf.Min(lGait, pMaxTier);
            if (pSpeed < 0.12f) lGait = 0f;
            _gait = Mathf.MoveTowards(_gait, lGait, dt * 6f);
            float lNominal = _gait <= 1f ? _gait * s1 : _gait <= 2f ? Mathf.Lerp(s1, s2, _gait - 1f) : Mathf.Lerp(s2, s3, _gait - 2f);
            pPace = lNominal > 0.2f ? Mathf.Clamp(pSpeed / lNominal, 0.6f, pMaxPace) : 1f;
            Vector2 lTarget = lDir * _gait;
            _move = Vector2.Lerp(_move, lTarget, 1f - Mathf.Exp(-12f * dt));
            _animator.SetFloat(MoveXId, _move.x);
            _animator.SetFloat(MoveZId, _move.y);
        }

        /// <summary>A clip-ring speed for a direction, weighting the four cardinal clips like the blend tree.</summary>
        private static float Directional(Vector4 pRing, Vector2 pDir)
        {
            float lX = pDir.x * pDir.x, lZ = pDir.y * pDir.y;
            return lX * (pDir.x < 0f ? pRing.z : pRing.w) + lZ * (pDir.y >= 0f ? pRing.x : pRing.y);
        }

        private float FadeFor(string pState)
        {
            if (pState == S_JUMP || pState == S_VAULT || pState == S_MANTLE || pState == S_CLIMB_UP) return 0.08f;
            if (pState == S_FALL) return 0.2f;
            if (pState == S_SLIDE || pState == S_LAND) return 0.1f;
            if (_baseState == Animator.StringToHash(S_FALL) || _baseState == Animator.StringToHash(S_LAND)) return 0.15f;
            return 0.22f;
        }

        private void Play(int pLayer, string pState, float pFade)
        {
            int lHash = Animator.StringToHash(pState);
            if (pLayer == BODY) _baseState = lHash; else if (pLayer == UPPER) _upperState = lHash;
            if (_animator == null || !_animator.HasState(pLayer, lHash)) return;
            if (pFade <= 0f) _animator.Play(lHash, pLayer, 0f);
            else _animator.CrossFadeInFixedTime(lHash, pFade, pLayer);
        }

        /// <summary>The weapon hold / aim / action over the legs; off while climbing, hanging, swimming or vaulting.</summary>
        private void UpdateUpper(string pBase, float dt)
        {
            bool lFree = pBase == S_LOCOMOTION || pBase == S_CROUCH || pBase == S_LAND || pBase == S_FALL || pBase == S_JUMP || pBase == S_SLIDE
                         || pBase == S_ARMED_IDLE || pBase == S_LOOK_AROUND;
            bool lAction = Time.time < _actionUntil;
            string lHold = null;
            if (Calling) lHold = S_PHONE;
            else if (Held == HeldKind.Pistol) lHold = Aiming ? S_AIM_PISTOL : S_PISTOL_READY;
            else if (Held == HeldKind.Rifle) lHold = Aiming ? S_AIM_RIFLE : S_RIFLE_READY;
            if (pBase == S_ARMED_IDLE && !Aiming) lHold = null;

            if (!lAction)
            {
                int lWanted = lHold != null ? Animator.StringToHash(lHold) : 0;
                if (lWanted != 0 && lWanted != _upperState) Play(UPPER, lHold, _upperState == _actionState ? 0.18f : 0.25f);
                if (lWanted == 0) _upperState = 0;
            }
            float lTarget = lFree && (lAction || lHold != null) ? 1f : 0f;
            if (Sprinting && !lAction && !Aiming) lTarget *= 0.75f;
            _upperWeight = Mathf.MoveTowards(_upperWeight, lTarget, dt * (lTarget > _upperWeight ? 8f : 4f));
            _animator.SetLayerWeight(UPPER, _upperWeight);
        }

        // ---------------------------------------------------------------- IK (forwarded by HumanBody's model)

        public void OnIK(int pLayer)
        {
            if (pLayer != BODY || _animator == null) return;
            if (_body == null) _body = GetComponent<HumanBody>();
            float dt = Time.deltaTime;
            bool lNear = UseIK && IsNearCamera(35f);

            // Look: head (and spine when aiming) towards the aim or the stared-at point.
            Transform lHead = _body.Head;
            float lLookTarget = lNear && lHead != null && Motion == MotionState.Ground && !_airborne ? 1f : 0f;
            _lookWeight = Mathf.MoveTowards(_lookWeight, lLookTarget, dt * 3f);
            if (_lookWeight > 0.01f && lHead != null)
            {
                Vector3 lFrom = lHead.position;
                Vector3 lPoint = LookAt ?? lFrom + Quaternion.Euler(Mathf.Clamp(AimPitch, -70f, 70f), transform.eulerAngles.y, 0f) * Vector3.forward * 10f;
                bool lArmed = Aiming && (Held == HeldKind.Pistol || Held == HeldKind.Rifle);
                _animator.SetLookAtWeight(_lookWeight, lArmed ? 0.55f : 0.15f, lArmed ? 0.6f : 0.85f, 0f, lArmed ? 0.35f : 0.55f);
                _animator.SetLookAtPosition(lPoint);
            }

            // Feet on the ground: keep each foot's animated lift above the ground under it.
            bool lPlanted = lNear && !_airborne && (Motion == MotionState.Ground || Motion == MotionState.Slide) && _baseState != Animator.StringToHash(S_SIT) && _baseState != Animator.StringToHash(S_LEAN);
            _footWeight = Mathf.MoveTowards(_footWeight, lPlanted ? 1f : 0f, dt * 5f);
            float lLowest = 0f;
            if (_footWeight > 0.01f)
            {
                float lLeft = Foot(AvatarIKGoal.LeftFoot), lRight = Foot(AvatarIKGoal.RightFoot);
                lLowest = Mathf.Min(lLeft, lRight, 0f);
            }
            _pelvisOffset = Mathf.Lerp(_pelvisOffset, lLowest * _footWeight, 1f - Mathf.Exp(-12f * dt));
            if (Mathf.Abs(_pelvisOffset) > 0.001f) _animator.bodyPosition += Vector3.up * _pelvisOffset;

            // Long guns: the left hand on the fore-end.
            Transform lSocket = _body.WeaponSocket;
            bool lTwoHanded = Held == HeldKind.Rifle || (Held == HeldKind.Pistol && Aiming);
            bool lSupport = lNear && lTwoHanded && _upperWeight > 0.5f && Time.time >= _actionUntil && lSocket != null && lSocket.childCount > 0;
            _supportWeight = Mathf.MoveTowards(_supportWeight, lSupport ? 1f : 0f, dt * 6f);
            if (_supportWeight > 0.01f && lSocket != null)
            {
                Transform lMuzzle = lSocket.childCount > 0 ? lSocket.GetChild(0).Find("Muzzle") : null;
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _supportWeight * _upperWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, lSocket.TransformPoint(HumanRig.Instance.SupportFor(Held, lMuzzle)));
            }
        }

        /// <summary>The support hand's rotation on the bone itself (the grips are measured on raw bones, not in the IK goal space).</summary>
        private void LateUpdate()
        {
            AlignWeapon();
            if (_supportWeight <= 0.01f || _body == null || _body.HandL == null || _body.WeaponSocket == null) return;
            Quaternion lTarget = _body.WeaponSocket.rotation * HumanRig.Instance.For(Held).supportRotation;
            _body.HandL.rotation = Quaternion.Slerp(_body.HandL.rotation, lTarget, _supportWeight * _upperWeight * 0.85f);
        }

        /// <summary>
        /// Aiming, the gun turns about the grip onto the aim line (the clips' hands are keyed for the
        /// pose, not for each gun): what a remote player sees pointed at them is really pointed at them.
        /// </summary>
        private void AlignWeapon()
        {
            if (_body == null || _body.WeaponSocket == null || _animator == null) return;
            bool lGun = Held == HeldKind.Pistol || Held == HeldKind.Rifle;
            _alignWeight = Mathf.MoveTowards(_alignWeight, lGun && Aiming && _upperWeight > 0.5f && Time.time >= _actionUntil ? 1f : 0f, Time.deltaTime * 5f);
            _body.SetHeld(Held, true);
            if (_alignWeight <= 0.001f) return;
            Quaternion lAim = Quaternion.Euler(Mathf.Clamp(AimPitch, -70f, 70f), transform.eulerAngles.y, 0f);
            Transform lSocket = _body.WeaponSocket;
            lSocket.rotation = Quaternion.Slerp(lSocket.rotation, lAim, _alignWeight);
        }

        /// <summary>Sets one foot's goal over the ground below it; returns how far the ground sits below the animated foot (negative = lower).</summary>
        private float Foot(AvatarIKGoal pGoal)
        {
            Vector3 lFoot = _animator.GetIKPosition(pGoal);
            Quaternion lRotation = _animator.GetIKRotation(pGoal);
            float lLift = lFoot.y - transform.position.y;
            Vector3 lFrom = new Vector3(lFoot.x, transform.position.y + 0.55f, lFoot.z);
            int lCount = Physics.RaycastNonAlloc(lFrom, Vector3.down, _hits, 1.2f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            if (lCount == 0) { _animator.SetIKPositionWeight(pGoal, 0f); _animator.SetIKRotationWeight(pGoal, 0f); return 0f; }
            RaycastHit lHit = _hits[0];
            for (int i = 1; i < lCount; i++) if (_hits[i].distance < lHit.distance) lHit = _hits[i];
            float lGround = lHit.point.y - transform.position.y;
            if (lGround < -0.5f || lGround > 0.5f) { _animator.SetIKPositionWeight(pGoal, 0f); _animator.SetIKRotationWeight(pGoal, 0f); return 0f; }
            Vector3 lTarget = new Vector3(lFoot.x, lHit.point.y + Mathf.Max(lLift, 0.06f), lFoot.z);
            Quaternion lSlope = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, lHit.normal, 0.8f));
            _animator.SetIKPositionWeight(pGoal, _footWeight);
            _animator.SetIKRotationWeight(pGoal, _footWeight * Mathf.Clamp01(1f - lLift * 6f));
            _animator.SetIKPosition(pGoal, lTarget + Vector3.up * -_pelvisOffset * 0f);
            _animator.SetIKRotation(pGoal, lSlope * lRotation);
            return lGround;
        }

        private bool IsNearCamera(float pRange)
        {
            Camera lCamera = Camera.main;
            if (lCamera == null) return true;
            return (lCamera.transform.position - transform.position).sqrMagnitude < pRange * pRange;
        }
    }
}
