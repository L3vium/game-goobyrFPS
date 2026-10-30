using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>A cable from one anchor to a lower one. Grab it with interact, drop with jump/crouch.</summary>
    public class Zipline : Interactable
    {
        [SerializeField] private Transform _start;
        [SerializeField] private Transform _end;
        [SerializeField] private float _speed = 15f;

        public float Speed => _speed;
        public float Length => Vector3.Distance(_start.position, _end.position);
        public Vector3 Direction => (_end.position - _start.position).normalized;

        public void Setup(Transform pStart, Transform pEnd, float pSpeed)
        {
            _start = pStart;
            _end = pEnd;
            _speed = pSpeed;
            _prompt = "RIDE ZIPLINE";
            _range = 3.2f;
        }

        public Vector3 PointAt(float pT)
        {
            Vector3 lPoint = Vector3.Lerp(_start.position, _end.position, Mathf.Clamp01(pT));
            lPoint.y -= Mathf.Sin(Mathf.Clamp01(pT) * Mathf.PI) * Length * 0.03f;
            return lPoint;
        }

        public float ClosestT(Vector3 pPosition)
        {
            Vector3 lLine = _end.position - _start.position;
            return Mathf.Clamp01(Vector3.Dot(pPosition - _start.position, lLine) / lLine.sqrMagnitude);
        }

        public override bool InteractLocal(PlayerAvatar pWho) => pWho.Motor.StartZipline(this);
    }

    /// <summary>
    /// A closet, a dumpster, a haystack, a crate: step in to vanish. NPCs only find you inside if
    /// they saw you get in.
    /// </summary>
    public class HidingSpot : Interactable
    {
        [SerializeField] private Transform _inside;
        [SerializeField] private Transform _exit;

        public Vector3 InsidePosition => _inside != null ? _inside.position : transform.position;
        public Vector3 ExitPosition => _exit != null ? _exit.position : transform.position + transform.forward * 1.2f;

        public void Setup(Transform pInside, Transform pExit, string pPrompt)
        {
            _inside = pInside;
            _exit = pExit;
            _prompt = pPrompt;
        }

        public override bool InteractLocal(PlayerAvatar pWho)
        {
            if (pWho.Motor.Mode == MotorMode.Hidden) { pWho.Motor.ExitHidingSpot(); return true; }
            if (pWho.Motor.EnterHidingSpot(this)) Juice.Squash(transform, 0.2f);
            return true;
        }
    }

    /// <summary>Throws whoever steps on it high and far. Squashes in the process.</summary>
    public class JumpPad : MonoBehaviour
    {
        [SerializeField] private Vector3 _launch = new Vector3(0, 16f, 6f);
        [SerializeField] private Transform _visual;

        public void Setup(Vector3 pLaunchLocal, Transform pVisual)
        {
            _launch = pLaunchLocal;
            _visual = pVisual;
        }

        private void OnTriggerEnter(Collider pOther)
        {
            PlayerAvatar lAvatar = pOther.GetComponentInParent<PlayerAvatar>();
            if (lAvatar == null) return;
            Juice.Squash(_visual != null ? _visual : transform, 0.45f, 0.4f);
            if (!lAvatar.IsOwner) return;
            lAvatar.Motor.Launch(transform.TransformDirection(_launch));
            Juice.FovKick(14f);
            Juice.Shake(0.2f);
            Audio.Sfx.Play("jumppad", transform.position);
        }
    }

    /// <summary>Tall grass: crouched inside it, a player is almost invisible.</summary>
    public class TallGrass : MonoBehaviour
    {
        private void OnTriggerEnter(Collider pOther)
        {
            PlayerAvatar lAvatar = pOther.GetComponentInParent<PlayerAvatar>();
            if (lAvatar != null && lAvatar.IsOwner && pOther == lAvatar.Controller) lAvatar.Motor.EnterGrass();
        }

        private void OnTriggerExit(Collider pOther)
        {
            PlayerAvatar lAvatar = pOther.GetComponentInParent<PlayerAvatar>();
            if (lAvatar != null && lAvatar.IsOwner && pOther == lAvatar.Controller) lAvatar.Motor.ExitGrass();
        }

        /// <summary>Host side: is this point inside grass (NPC perception)?</summary>
        public static bool Covers(Vector3 pPoint)
        {
            Collider[] lHits = Physics.OverlapSphere(pPoint, 0.2f, 1 << GameLayers.GRASS, QueryTriggerInteraction.Collide);
            return lHits.Length > 0;
        }
    }
}
