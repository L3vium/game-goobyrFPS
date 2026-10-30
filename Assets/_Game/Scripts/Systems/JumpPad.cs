using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
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
}
