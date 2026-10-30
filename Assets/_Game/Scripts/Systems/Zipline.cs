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
}
