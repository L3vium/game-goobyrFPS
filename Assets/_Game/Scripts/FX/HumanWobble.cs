using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// The goofy wobble of a body: up to six damped angular springs (upper chest, neck, head) laid on
    /// top of the animated pose every frame, after the Animator (the pose is re-read each frame, the
    /// springs never accumulate into it). They lag behind the body's accelerations and take kicks
    /// (hits, landings, punches). Never the hips or the legs (the feet are planted by IK). Cosmetic
    /// only: host gameplay never reads bone positions; skipped while the body is not seen.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HumanWobble : MonoBehaviour
    {
        private const float MAX_ANGLE = 14f;

        private struct Spring
        {
            public Transform bone;
            public Vector3 angle;      // world axis * radians
            public Vector3 speed;
            public float stiffness, damping, gain;
        }

        private Spring[] _springs = new Spring[0];
        private Renderer _probe;
        private Vector3 _lastPosition, _lastVelocity;
        private bool _primed;

        public void Init(Animator pAnimator, Renderer pProbe)
        {
            _probe = pProbe;
            if (pAnimator == null || !pAnimator.isHuman) return;
            System.Collections.Generic.List<Spring> lSprings = new System.Collections.Generic.List<Spring>();
            void Add(HumanBodyBones pBone, float pStiffness, float pDamping, float pGain)
            {
                Transform lBone = pAnimator.GetBoneTransform(pBone);
                if (lBone != null && lSprings.Count < 6) lSprings.Add(new Spring { bone = lBone, stiffness = pStiffness, damping = pDamping, gain = pGain });
            }
            Add(HumanBodyBones.UpperChest, 90f, 9f, 0.010f);
            Add(HumanBodyBones.Neck, 120f, 8f, 0.014f);
            Add(HumanBodyBones.Head, 140f, 7f, 0.020f);
            _springs = lSprings.ToArray();
        }

        /// <summary>A shove from a world direction (scaled by strength, about 1 for a hit).</summary>
        public void Kick(Vector3 pDirection, float pStrength = 1f)
        {
            Vector3 lAxis = Vector3.Cross(Vector3.up, pDirection.normalized);
            for (int i = 0; i < _springs.Length; i++) _springs[i].speed += lAxis * (3.5f + i * 1.5f) * pStrength;
        }

        private void LateUpdate()
        {
            if (_springs.Length == 0 || (_probe != null && !_probe.isVisible)) { _primed = false; return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;
            Vector3 lPosition = transform.position;
            if (!_primed) { _lastPosition = lPosition; _lastVelocity = Vector3.zero; _primed = true; }
            Vector3 lVelocity = (lPosition - _lastPosition) / dt;
            Vector3 lAcceleration = Vector3.ClampMagnitude((lVelocity - _lastVelocity) / dt, 40f);
            _lastPosition = lPosition;
            _lastVelocity = lVelocity;
            // Inertia: the upper body tips against the acceleration.
            Vector3 lDrive = Vector3.Cross(Vector3.up, -lAcceleration);
            float lMax = MAX_ANGLE * Mathf.Deg2Rad;
            for (int i = 0; i < _springs.Length; i++)
            {
                ref Spring s = ref _springs[i];
                if (s.bone == null) continue;
                Vector3 lForce = -s.stiffness * s.angle - s.damping * s.speed + lDrive * s.gain * s.stiffness;
                s.speed += lForce * dt;
                s.angle += s.speed * dt;
                if (s.angle.magnitude > lMax) { s.angle = s.angle.normalized * lMax; s.speed *= 0.5f; }
                float lDegrees = s.angle.magnitude * Mathf.Rad2Deg;
                if (lDegrees > 0.01f) s.bone.rotation = Quaternion.AngleAxis(lDegrees, s.angle.normalized) * s.bone.rotation;
            }
        }
    }
}
