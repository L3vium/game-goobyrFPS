using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Procedural, wobbly animation of a <see cref="GoobBody"/>: feet stepping in arcs, a springy
    /// torso that leans into its speed and squashes on landings, a head that lags behind, arms that
    /// swing or hold the weapon toward the aim pitch. No clips - everything reads the velocity,
    /// so it looks the same on every machine whatever the network sends.
    /// </summary>
    [RequireComponent(typeof(GoobBody))]
    public class GoobAnimator : MonoBehaviour
    {
        [SerializeField] private float _stepLength = 0.55f;
        [SerializeField] private float _stepHeight = 0.16f;

        public float AimPitch { get; set; }
        public bool Crouched { get; set; }
        public bool HoldingWeapon { get; set; } = true;
        public bool Aiming { get; set; }
        public bool Talking { get; set; }
        public bool Calling { get; set; }
        public bool Sick { get; set; }
        public float Panic { get; set; }

        private GoobBody _body;
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private float _phase;
        private Vector3 _torsoSpring, _torsoSpringVel;
        private float _squash = 1f, _squashVel;
        private float _crouchBlend;
        private float _headYawLag;
        private float _lastYaw;
        private bool _wasGrounded = true;
        private float _blinkTimer;

        private void Awake()
        {
            _body = GetComponent<GoobBody>();
            _lastPosition = transform.position;
            _lastYaw = transform.eulerAngles.y;
            _phase = Random.value * 10f;
            _blinkTimer = Random.Range(1f, 4f);
        }

        /// <summary>A landing (or a hit): the body squashes, then springs back.</summary>
        public void Squash(float pAmount) => _squashVel -= pAmount * 6f;

        /// <summary>A shove from a hit, in world space.</summary>
        public void Shove(Vector3 pWorldDirection) => _torsoSpringVel += transform.InverseTransformDirection(pWorldDirection) * 4f;

        private void LateUpdate()
        {
            if (!_body.Built || _body.Root == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            Vector3 lVelocity = (transform.position - _lastPosition) / dt;
            _lastPosition = transform.position;
            _velocity = Vector3.Lerp(_velocity, lVelocity, 1f - Mathf.Exp(-12f * dt));
            Vector3 lPlanar = new Vector3(_velocity.x, 0, _velocity.z);
            float lSpeed = Mathf.Min(lPlanar.magnitude, 10f);
            Vector3 lLocalVel = transform.InverseTransformDirection(lPlanar);

            bool lGrounded = Mathf.Abs(_velocity.y) < 2.5f;
            if (lGrounded && !_wasGrounded) Squash(0.25f);
            _wasGrounded = lGrounded;

            _crouchBlend = Mathf.MoveTowards(_crouchBlend, Crouched ? 1f : 0f, dt * 5f);
            _phase += lSpeed / _stepLength * dt * Mathf.PI * 0.5f;

            float lMoving = Mathf.Clamp01(lSpeed / 2f);
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);

            // Feet.
            float lStride = Mathf.Clamp(lSpeed * 0.07f, 0f, 0.35f);
            Vector3 lFwd = lSpeed > 0.1f ? lLocalVel.normalized : Vector3.forward;
            _body.FootL.localPosition = new Vector3(-0.16f, 0.07f + Mathf.Max(0, s) * _stepHeight * lMoving, 0.03f) + lFwd * (s * lStride);
            _body.FootR.localPosition = new Vector3(0.16f, 0.07f + Mathf.Max(0, -s) * _stepHeight * lMoving, 0.03f) - lFwd * (s * lStride);
            if (!lGrounded)
            {
                _body.FootL.localPosition += new Vector3(0, 0.15f, 0.1f);
                _body.FootR.localPosition += new Vector3(0, 0.1f, -0.1f);
            }

            // Torso spring: leans into speed, wobbles back.
            Vector3 lTarget = new Vector3(-lLocalVel.z * 2.2f - _crouchBlend * 18f, 0, lLocalVel.x * 2.2f);
            Vector3 lForce = (lTarget - _torsoSpring) * 90f - _torsoSpringVel * 9f;
            _torsoSpringVel += lForce * dt;
            _torsoSpring += _torsoSpringVel * dt;

            _squashVel += ((1f - _squash) * 160f - _squashVel * 10f) * dt;
            _squash += _squashVel * dt;
            float lBob = Mathf.Abs(c) * 0.05f * lMoving;
            float lCrouchDrop = _crouchBlend * 0.42f;

            _body.Torso.localPosition = new Vector3(0, 0.78f + lBob - lCrouchDrop, 0);
            _body.Torso.localRotation = Quaternion.Euler(-_torsoSpring.x, s * 6f * lMoving, -_torsoSpring.z + Panic * Mathf.Sin(Time.time * 30f) * 4f);
            _body.Torso.localScale = new Vector3(0.72f / Mathf.Sqrt(_squash), 0.75f * _squash, 0.6f / Mathf.Sqrt(_squash));

            // Head lags the body's turn and looks along the aim pitch.
            float lYaw = transform.eulerAngles.y;
            float lYawDelta = Mathf.DeltaAngle(_lastYaw, lYaw);
            _lastYaw = lYaw;
            _headYawLag = Mathf.Lerp(_headYawLag - lYawDelta * 0.6f, 0f, 1f - Mathf.Exp(-8f * dt));
            _body.Head.localPosition = _body.Torso.localPosition + _body.Torso.localRotation * new Vector3(0, 0.62f, 0) + new Vector3(0, lBob * 0.6f, 0);
            float lNod = Talking ? Mathf.Sin(Time.time * 14f) * 6f : 0f;
            float lSickTilt = Sick ? Mathf.Sin(Time.time * 3f) * 15f : 0f;
            _body.Head.localRotation = Quaternion.Euler(Mathf.Clamp(AimPitch, -60f, 60f) * 0.6f + lNod + lSickTilt, _headYawLag, -_torsoSpring.z * 0.5f);

            // Arms.
            Vector3 lShoulderL = _body.Torso.localPosition + new Vector3(-0.38f, 0.08f, 0);
            Vector3 lShoulderR = _body.Torso.localPosition + new Vector3(0.38f, 0.08f, 0);
            if (Calling)
            {
                _body.HandR.localPosition = _body.Head.localPosition + new Vector3(0.22f, -0.02f, 0.05f);
                _body.HandL.localPosition = lShoulderL + new Vector3(0, -0.05f, 0.05f) + Vector3.forward * (s * 0.1f);
            }
            else if (HoldingWeapon)
            {
                Quaternion lAim = Quaternion.Euler(AimPitch, 0, 0);
                float lReach = Aiming ? 0.55f : 0.42f;
                _body.HandR.localPosition = new Vector3(0.18f, _body.Torso.localPosition.y + 0.18f, 0) + lAim * new Vector3(0, 0, lReach);
                _body.HandL.localPosition = new Vector3(-0.05f, _body.Torso.localPosition.y + 0.16f, 0) + lAim * new Vector3(0, -0.04f, lReach + 0.18f);
                _body.HandR.localRotation = lAim;
            }
            else
            {
                float lSwing = s * 0.22f * lMoving;
                _body.HandL.localPosition = lShoulderL + new Vector3(-0.05f, -0.12f, lSwing);
                _body.HandR.localPosition = lShoulderR + new Vector3(0.05f, -0.12f, -lSwing);
                if (Panic > 0f)
                {
                    _body.HandL.localPosition += new Vector3(0, 0.5f + Mathf.Sin(Time.time * 20f) * 0.1f, 0);
                    _body.HandR.localPosition += new Vector3(0, 0.5f + Mathf.Cos(Time.time * 20f) * 0.1f, 0);
                }
                _body.HandR.localRotation = Quaternion.identity;
            }

            // Blinks.
            _blinkTimer -= dt;
            float lEye = _blinkTimer < 0.12f ? 0.15f : 1f;
            if (_blinkTimer < 0f) _blinkTimer = Random.Range(2f, 5f);
            foreach (Transform lPupil in _body.Pupils) lPupil.parent.localScale = new Vector3(0.15f, 0.17f * lEye, 0.1f);
        }
    }
}
