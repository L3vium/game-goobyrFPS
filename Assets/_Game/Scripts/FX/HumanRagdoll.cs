using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// The death of a <see cref="HumanBody"/>: the animated model leaves the character and its
    /// humanoid bones become a ragdoll built on the spot from the pose it died in - pelvis, chest,
    /// head, upper and lower arms and legs, capsules and boxes sized from the bones, realistic mass
    /// split (75 kg), character joints whose hinge limits start from the current bend so elbows and
    /// knees only fold the right way. It keeps the speed it had and takes the blow at the hit point.
    /// Local to each machine (the impulse comes through the death RPC), capped so long matches keep
    /// their frame rate.
    /// </summary>
    public class HumanRagdoll : MonoBehaviour
    {
        private const int MAX_RAGDOLLS = 45;
        private const float MASS = 75f;
        private static readonly Queue<HumanRagdoll> _alive = new Queue<HumanRagdoll>();

        public Transform Head { get; private set; }
        public Rigidbody Pelvis { get; private set; }

        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private float _settleCheck;
        private float _startY;

        /// <summary>Turns the body into a ragdoll; returns null when there is no model.</summary>
        public static HumanRagdoll Spawn(HumanBody pBody, Vector3 pImpulse, Vector3 pPoint, Vector3 pVelocity = default)
        {
            if (pBody == null || !pBody.Built || pBody.Animator == null || !pBody.Animator.isHuman) return null;
            while (_alive.Count >= MAX_RAGDOLLS)
            {
                HumanRagdoll lOld = _alive.Dequeue();
                if (lOld != null) Destroy(lOld.gameObject);
            }

            Animator lAnimator = pBody.Animator;
            Transform lCharacter = pBody.transform;
            Transform lModel = pBody.Detach();
            lModel.name = "Ragdoll_" + lCharacter.root.name;
            lAnimator.enabled = false;
            HumanRagdoll lRagdoll = lModel.gameObject.AddComponent<HumanRagdoll>();
            _alive.Enqueue(lRagdoll);
            GameLayers.SetRecursive(lModel.gameObject, GameLayers.RAGDOLL);
            foreach (SkinnedMeshRenderer lSkin in lModel.GetComponentsInChildren<SkinnedMeshRenderer>()) lSkin.updateWhenOffscreen = true;
            lRagdoll.Build(lAnimator, lCharacter.right, lCharacter.forward);
            lRagdoll._startY = lModel.position.y;

            foreach (Rigidbody lRigidbody in lRagdoll._bodies) lRigidbody.linearVelocity = pVelocity;
            if (pImpulse.sqrMagnitude > 0.0001f)
            {
                // Most of the blow on the part that was hit, the rest spread so the whole body reacts.
                // (a bullet's 9 N.s turns into a stagger of the whole body and a shove of the part hit).
                Rigidbody lHit = lRagdoll.Closest(pPoint);
                foreach (Rigidbody lRigidbody in lRagdoll._bodies)
                    lRigidbody.AddForce(pImpulse * 5f * (lRigidbody.mass / MASS), ForceMode.Impulse);
                if (lHit != null) lHit.AddForceAtPosition(pImpulse * 3f, pPoint, ForceMode.Impulse);
            }
            return lRagdoll;
        }

        public static void ClearAll()
        {
            while (_alive.Count > 0)
            {
                HumanRagdoll lOld = _alive.Dequeue();
                if (lOld != null) Destroy(lOld.gameObject);
            }
        }

        private Rigidbody Closest(Vector3 pPoint)
        {
            Rigidbody lBest = null;
            float lBestDistance = float.MaxValue;
            foreach (Rigidbody lRigidbody in _bodies)
            {
                float lDistance = (lRigidbody.worldCenterOfMass - pPoint).sqrMagnitude;
                if (lDistance < lBestDistance) { lBestDistance = lDistance; lBest = lRigidbody; }
            }
            return lBest;
        }

        // ---------------------------------------------------------------- build

        private void Build(Animator pAnimator, Vector3 pRight, Vector3 pForward)
        {
            Transform B(HumanBodyBones pBone) => pAnimator.GetBoneTransform(pBone);
            Transform lPelvis = B(HumanBodyBones.Hips), lSpine = B(HumanBodyBones.Spine), lChest = B(HumanBodyBones.UpperChest) ?? B(HumanBodyBones.Chest);
            Transform lNeck = B(HumanBodyBones.Neck), lHead = B(HumanBodyBones.Head);
            Transform lArmL = B(HumanBodyBones.LeftUpperArm), lForeL = B(HumanBodyBones.LeftLowerArm), lHandL = B(HumanBodyBones.LeftHand);
            Transform lArmR = B(HumanBodyBones.RightUpperArm), lForeR = B(HumanBodyBones.RightLowerArm), lHandR = B(HumanBodyBones.RightHand);
            Transform lThighL = B(HumanBodyBones.LeftUpperLeg), lCalfL = B(HumanBodyBones.LeftLowerLeg), lFootL = B(HumanBodyBones.LeftFoot);
            Transform lThighR = B(HumanBodyBones.RightUpperLeg), lCalfR = B(HumanBodyBones.RightLowerLeg), lFootR = B(HumanBodyBones.RightFoot);
            Transform lToesL = B(HumanBodyBones.LeftToes), lToesR = B(HumanBodyBones.RightToes);
            Vector3 lUp = Vector3.Cross(pForward, pRight);
            float lHip = Vector3.Distance(lThighL.position, lThighR.position);

            // Pelvis and chest: boxes across the body.
            Pelvis = Body(lPelvis, 0.15f);
            BoxCollider lPelvisBox = lPelvis.gameObject.AddComponent<BoxCollider>();
            Vector3 lPelvisTop = lSpine.position, lPelvisBottom = (lThighL.position + lThighR.position) * 0.5f - lUp * 0.05f;
            FitBox(lPelvisBox, lPelvis, (lPelvisTop + lPelvisBottom) * 0.5f, pRight * (lHip + 0.16f), lUp * Vector3.Distance(lPelvisTop, lPelvisBottom), pForward * 0.2f);

            Rigidbody lChestBody = Body(lSpine, 0.28f);
            BoxCollider lChestBox = lSpine.gameObject.AddComponent<BoxCollider>();
            Vector3 lChestTop = lNeck.position, lChestBottom = lSpine.position;
            FitBox(lChestBox, lSpine, (lChestTop + lChestBottom) * 0.5f, pRight * Vector3.Distance(lArmL.position, lArmR.position) * 0.85f, lUp * Vector3.Distance(lChestTop, lChestBottom), pForward * 0.22f);
            Ball(lChestBody, Pelvis, lSpine, (lChest.position - lSpine.position).normalized, pRight, 22f, 18f, 12f);

            Rigidbody lHeadBody = Body(lHead, 0.075f);
            SphereCollider lSkull = lHead.gameObject.AddComponent<SphereCollider>();
            lSkull.radius = 0.11f / Mathf.Max(0.01f, lHead.lossyScale.x);
            lSkull.center = lHead.InverseTransformPoint(lHead.position + lUp * 0.08f + pForward * 0.02f);
            Ball(lHeadBody, lChestBody, lHead, (lHead.position - lNeck.position).normalized, pRight, 40f, 35f, 30f);
            Head = lHead;

            Limb(lArmL, lForeL, lChestBody, 0.027f, 0.055f, pRight, 70f, 80f, 60f, out Rigidbody lUpperL);
            Hinge(lForeL, lHandL, lUpperL, lArmL, 0.022f, 0.045f, -pRight, 145f);
            Limb(lArmR, lForeR, lChestBody, 0.027f, 0.055f, pRight, 70f, 80f, 60f, out Rigidbody lUpperR);
            Hinge(lForeR, lHandR, lUpperR, lArmR, 0.022f, 0.045f, -pRight, 145f);
            Limb(lThighL, lCalfL, Pelvis, 0.1f, 0.085f, pRight, 35f, 75f, 40f, out Rigidbody lLegL);
            Hinge(lCalfL, lToesL != null ? lToesL : lFootL, lLegL, lThighL, 0.06f, 0.06f, pRight, 140f);
            Limb(lThighR, lCalfR, Pelvis, 0.1f, 0.085f, pRight, 35f, 75f, 40f, out Rigidbody lLegR);
            Hinge(lCalfR, lToesR != null ? lToesR : lFootR, lLegR, lThighR, 0.06f, 0.06f, pRight, 140f);

            // No self-collision: the parts start overlapping at the joints and would push each other apart.
            Collider[] lColliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < lColliders.Length; i++)
                for (int j = i + 1; j < lColliders.Length; j++) Physics.IgnoreCollision(lColliders[i], lColliders[j]);
        }

        private Rigidbody Body(Transform pBone, float pShare)
        {
            Rigidbody lBody = pBone.gameObject.AddComponent<Rigidbody>();
            lBody.mass = MASS * pShare;
            lBody.linearDamping = 0.05f;
            lBody.angularDamping = 0.6f;
            lBody.interpolation = RigidbodyInterpolation.Interpolate;
            lBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            lBody.solverIterations = 12;
            lBody.solverVelocityIterations = 4;
            lBody.maxDepenetrationVelocity = 3f;
            _bodies.Add(lBody);
            return lBody;
        }

        /// <summary>A box in the bone's space covering a world-space centre and three world extents.</summary>
        private static void FitBox(BoxCollider pBox, Transform pBone, Vector3 pCentre, Vector3 pX, Vector3 pY, Vector3 pZ)
        {
            pBox.center = pBone.InverseTransformPoint(pCentre);
            Vector3 lX = pBone.InverseTransformVector(pX), lY = pBone.InverseTransformVector(pY), lZ = pBone.InverseTransformVector(pZ);
            pBox.size = new Vector3(
                Mathf.Abs(lX.x) + Mathf.Abs(lY.x) + Mathf.Abs(lZ.x),
                Mathf.Abs(lX.y) + Mathf.Abs(lY.y) + Mathf.Abs(lZ.y),
                Mathf.Abs(lX.z) + Mathf.Abs(lY.z) + Mathf.Abs(lZ.z));
        }

        /// <summary>A capsule from a bone to its child.</summary>
        private static void Capsule(Transform pBone, Transform pEnd, float pRadius, float pExtend = 0f)
        {
            CapsuleCollider lCapsule = pBone.gameObject.AddComponent<CapsuleCollider>();
            Vector3 lEnd = pBone.InverseTransformPoint(pEnd.position);
            float lScale = Mathf.Max(0.01f, pBone.lossyScale.x);
            lCapsule.direction = Mathf.Abs(lEnd.x) > Mathf.Abs(lEnd.y) ? (Mathf.Abs(lEnd.x) > Mathf.Abs(lEnd.z) ? 0 : 2) : (Mathf.Abs(lEnd.y) > Mathf.Abs(lEnd.z) ? 1 : 2);
            float lLength = lEnd.magnitude * (1f + pExtend);
            lCapsule.center = lEnd * (0.5f + pExtend * 0.5f);
            lCapsule.radius = pRadius / lScale;
            lCapsule.height = lLength + pRadius * 2f / lScale * 0.5f;
        }

        private void Limb(Transform pBone, Transform pEnd, Rigidbody pParent, float pShare, float pRadius, Vector3 pRight, float pTwist, float pSwing1, float pSwing2, out Rigidbody pBody)
        {
            pBody = Body(pBone, pShare);
            Capsule(pBone, pEnd, pRadius);
            Ball(pBody, pParent, pBone, (pEnd.position - pBone.position).normalized, pRight, pTwist, pSwing1, pSwing2);
        }

        /// <summary>
        /// Elbow or knee: a hinge whose axis is the current bend (or <paramref name="pFallback"/> when
        /// straight), limited from straight to fully folded - never backwards.
        /// </summary>
        private void Hinge(Transform pBone, Transform pEnd, Rigidbody pParent, Transform pParentBone, float pShare, float pRadius, Vector3 pFallback, float pFold)
        {
            Rigidbody lBody = Body(pBone, pShare);
            Capsule(pBone, pEnd, pRadius, 0.15f);
            Vector3 lUpper = (pBone.position - pParentBone.position).normalized, lLower = (pEnd.position - pBone.position).normalized;
            Vector3 lAxis = Vector3.Cross(lUpper, lLower);
            float lBend = Vector3.Angle(lUpper, lLower);
            if (lAxis.sqrMagnitude < 0.01f || lBend < 8f) { lAxis = pFallback; lBend = 0f; }
            lAxis.Normalize();
            CharacterJoint lJoint = Joint(lBody, pParent, pBone, lAxis, Vector3.Cross(lAxis, lLower));
            // Twist about the bend axis: from straightened (minus the current bend) to fully folded.
            lJoint.lowTwistLimit = new SoftJointLimit { limit = -Mathf.Min(lBend, 170f) };
            lJoint.highTwistLimit = new SoftJointLimit { limit = Mathf.Clamp(pFold - lBend, 5f, 170f) };
            lJoint.swing1Limit = new SoftJointLimit { limit = 4f };
            lJoint.swing2Limit = new SoftJointLimit { limit = 4f };
        }

        private static void Ball(Rigidbody pBody, Rigidbody pParent, Transform pBone, Vector3 pTwistAxis, Vector3 pSwingAxis, float pTwist, float pSwing1, float pSwing2)
        {
            CharacterJoint lJoint = Joint(pBody, pParent, pBone, pTwistAxis, pSwingAxis);
            lJoint.lowTwistLimit = new SoftJointLimit { limit = -pTwist };
            lJoint.highTwistLimit = new SoftJointLimit { limit = pTwist };
            lJoint.swing1Limit = new SoftJointLimit { limit = pSwing1 };
            lJoint.swing2Limit = new SoftJointLimit { limit = pSwing2 };
        }

        private static CharacterJoint Joint(Rigidbody pBody, Rigidbody pParent, Transform pBone, Vector3 pWorldAxis, Vector3 pWorldSwing)
        {
            CharacterJoint lJoint = pBody.gameObject.AddComponent<CharacterJoint>();
            lJoint.connectedBody = pParent;
            lJoint.axis = pBone.InverseTransformDirection(pWorldAxis).normalized;
            Vector3 lSwing = Vector3.ProjectOnPlane(pWorldSwing, pWorldAxis);
            if (lSwing.sqrMagnitude < 0.0001f) lSwing = Vector3.Cross(pWorldAxis, Vector3.up);
            lJoint.swingAxis = pBone.InverseTransformDirection(lSwing).normalized;
            lJoint.enableProjection = true;
            lJoint.projectionDistance = 0.04f;
            lJoint.projectionAngle = 20f;
            lJoint.enablePreprocessing = false;
            lJoint.enableCollision = false;
            return lJoint;
        }

        // ---------------------------------------------------------------- settle

        private void Update()
        {
            // A body that fell through the world (water without a floor, a hole) is not kept.
            if (Pelvis != null && Pelvis.position.y < _startY - 40f) { Destroy(gameObject); return; }
            // Once everything sleeps for a while, freeze the corpse: no more solver cost.
            _settleCheck += Time.deltaTime;
            if (_settleCheck < 6f) return;
            _settleCheck = 4f;
            foreach (Rigidbody lBody in _bodies) if (lBody != null && !lBody.isKinematic && !lBody.IsSleeping()) return;
            foreach (Rigidbody lBody in _bodies) if (lBody != null) lBody.isKinematic = true;
            foreach (SkinnedMeshRenderer lSkin in GetComponentsInChildren<SkinnedMeshRenderer>()) lSkin.updateWhenOffscreen = false;
            enabled = false;
        }
    }
}
