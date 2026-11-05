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
            public float stiffness, damping, gain, max, gait;
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
                if (lBone != null) lSprings.Add(new Spring { bone = lBone, stiffness = pStiffness, damping = pDamping, gain = pGain, max = MAX_ANGLE });
            }
            Add(HumanBodyBones.UpperChest, 90f, 9f, 0.010f);
            Add(HumanBodyBones.Neck, 120f, 8f, 0.014f);
            Add(HumanBodyBones.Head, 110f, 6f, 0.026f);
            // The arms swing a beat behind the body.
            Add(HumanBodyBones.LeftUpperArm, 55f, 5f, 0.03f);
            Add(HumanBodyBones.RightUpperArm, 55f, 5f, 0.03f);
            Add(HumanBodyBones.LeftLowerArm, 70f, 5f, 0.02f);
            Add(HumanBodyBones.RightLowerArm, 70f, 5f, 0.02f);
            // The ears: soft, far out of step, and they flap with every stride.
            foreach (string lEar in new[] { "ear_l", "ear_r" })
            {
                Transform lBone = FindDeep(pAnimator.transform, lEar);
                if (lBone != null) lSprings.Add(new Spring { bone = lBone, stiffness = 34f, damping = 3.2f, gain = 0.09f, max = 42f, gait = lEar == "ear_l" ? 1f : -1f });
            }
            _springs = lSprings.ToArray();
        }

        private static Transform FindDeep(Transform pRoot, string pName)
        {
            if (pRoot.name == pName) return pRoot;
            foreach (Transform lChild in pRoot)
            {
                Transform lFound = FindDeep(lChild, pName);
                if (lFound != null) return lFound;
            }
            return null;
        }

        /// <summary>A shove from a world direction (scaled by strength, about 1 for a hit).</summary>
        public void Kick(Vector3 pDirection, float pStrength = 1f)
        {
            Vector3 lAxis = Vector3.Cross(Vector3.up, pDirection.normalized);
            for (int i = 0; i < _springs.Length; i++) _springs[i].speed += lAxis * (4f + Mathf.Min(i, 6) * 1.6f) * pStrength;
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
            for (int i = 0; i < _springs.Length; i++)
            {
                ref Spring s = ref _springs[i];
                if (s.bone == null) continue;
                Vector3 lForce = -s.stiffness * s.angle - s.damping * s.speed + lDrive * s.gain * s.stiffness;
                // Each stride flaps the ears, up and down about the walking direction.
                if (s.gait != 0f) lForce += transform.forward * (s.gait * Mathf.Sin(Time.time * 11f) * Mathf.Min(lVelocity.magnitude, 8f) * s.stiffness * 0.018f);
                s.speed += lForce * dt;
                s.angle += s.speed * dt;
                float lLimit = s.max * Mathf.Deg2Rad;
                if (s.angle.magnitude > lLimit) { s.angle = s.angle.normalized * lLimit; s.speed *= 0.5f; }
                float lDegrees = s.angle.magnitude * Mathf.Rad2Deg;
                if (lDegrees > 0.01f) s.bone.rotation = Quaternion.AngleAxis(lDegrees, s.angle.normalized) * s.bone.rotation;
            }
        }
    }
}
