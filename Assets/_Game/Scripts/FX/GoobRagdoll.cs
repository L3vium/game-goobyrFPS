using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Turns a dead Goob into a floppy physics body: the torso, head, hands and feet become
    /// rigidbodies joined by springy joints, thrown by the killing blow. Local to each machine
    /// (the impulse comes through the death RPC), capped so long matches keep their frame rate.
    /// </summary>
    public class GoobRagdoll : MonoBehaviour
    {
        private const int MAX_RAGDOLLS = 45;
        private static readonly Queue<GoobRagdoll> _alive = new Queue<GoobRagdoll>();

        public Transform Head { get; private set; }
        public Rigidbody Pelvis { get; private set; }

        public static GoobRagdoll Spawn(GoobBody pSource, Vector3 pImpulse, Vector3 pPoint, bool pWithHead = true)
        {
            if (pSource == null || !pSource.Built) return null;

            while (_alive.Count >= MAX_RAGDOLLS)
            {
                GoobRagdoll lOld = _alive.Dequeue();
                if (lOld != null) Destroy(lOld.gameObject);
            }

            GameObject lRoot = new GameObject("Ragdoll_" + pSource.name);
            GoobRagdoll lRagdoll = lRoot.AddComponent<GoobRagdoll>();
            _alive.Enqueue(lRagdoll);

            Rigidbody lTorso = Piece(lRoot.transform, pSource.Torso, 30f, true);
            lRagdoll.Pelvis = lTorso;
            Rigidbody lHead = pWithHead ? Piece(lRoot.transform, pSource.Head, 6f, false) : null;
            Rigidbody lHandL = Piece(lRoot.transform, pSource.HandL, 2f, false);
            Rigidbody lHandR = Piece(lRoot.transform, pSource.HandR, 2f, false);
            Rigidbody lFootL = Piece(lRoot.transform, pSource.FootL, 3f, false);
            Rigidbody lFootR = Piece(lRoot.transform, pSource.FootR, 3f, false);

            if (lHead != null) { Join(lHead, lTorso, 0.4f); lRagdoll.Head = lHead.transform; }
            Join(lHandL, lTorso, 0.25f);
            Join(lHandR, lTorso, 0.25f);
            Join(lFootL, lTorso, 0.35f);
            Join(lFootR, lTorso, 0.35f);

            foreach (Rigidbody lBody in lRoot.GetComponentsInChildren<Rigidbody>())
            {
                float lNear = 1f / (1f + Vector3.Distance(lBody.position, pPoint));
                lBody.AddForce(pImpulse * (0.5f + lNear), ForceMode.Impulse);
                lBody.AddTorque(Random.insideUnitSphere * pImpulse.magnitude * 0.3f, ForceMode.Impulse);
            }

            pSource.SetVisible(false);
            return lRagdoll;
        }

        private static Rigidbody Piece(Transform pParent, Transform pSource, float pMass, bool pBox)
        {
            GameObject lCopy = Instantiate(pSource.gameObject, pSource.position, pSource.rotation, pParent);
            lCopy.transform.localScale = pSource.lossyScale;
            GameLayers.SetRecursive(lCopy, GameLayers.RAGDOLL);
            Rigidbody lBody = lCopy.AddComponent<Rigidbody>();
            lBody.mass = pMass;
            lBody.linearDamping = 0.3f;
            lBody.angularDamping = 1.5f;
            lBody.interpolation = RigidbodyInterpolation.Interpolate;
            lBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (pBox)
            {
                CapsuleCollider lCapsule = lCopy.AddComponent<CapsuleCollider>();
                lCapsule.radius = 0.5f;
                lCapsule.height = 1.6f;
            }
            else lCopy.AddComponent<SphereCollider>().radius = 0.5f;
            return lBody;
        }

        private static void Join(Rigidbody pChild, Rigidbody pParent, float pSlack)
        {
            ConfigurableJoint lJoint = pChild.gameObject.AddComponent<ConfigurableJoint>();
            lJoint.connectedBody = pParent;
            lJoint.xMotion = lJoint.yMotion = lJoint.zMotion = ConfigurableJointMotion.Limited;
            lJoint.linearLimit = new SoftJointLimit { limit = pSlack };
            lJoint.linearLimitSpring = new SoftJointLimitSpring { spring = 800f, damper = 30f };
            lJoint.angularXMotion = lJoint.angularYMotion = lJoint.angularZMotion = ConfigurableJointMotion.Free;
            lJoint.enableCollision = false;
        }

        public void HideHead()
        {
            if (Head != null) Head.gameObject.SetActive(false);
        }

        public static void ClearAll()
        {
            while (_alive.Count > 0)
            {
                GoobRagdoll lOld = _alive.Dequeue();
                if (lOld != null) Destroy(lOld.gameObject);
            }
        }
    }
}
