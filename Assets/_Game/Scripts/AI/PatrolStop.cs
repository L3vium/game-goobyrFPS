using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>What a patrol does when it reaches a waypoint.</summary>
    public enum PatrolAction : byte
    {
        /// <summary>Stands a moment (the route's pause), then walks on.</summary>
        Pause,
        /// <summary>Scans the area the waypoint faces.</summary>
        LookAround,
        /// <summary>Chats with whoever walks with it (or stands there): they face each other and talk.</summary>
        Chat,
        Smoke,
        Phone,
        /// <summary>Tries the door (or the gate, the hatch) the waypoint faces, then looks around.</summary>
        CheckDoor,
        /// <summary>Leans on the wall or the railing behind it.</summary>
        Lean,
        /// <summary>Stands watch, weapon ready, facing the waypoint's forward.</summary>
        Watch
    }

    /// <summary>
    /// A waypoint's errand: placed on a child of a <see cref="PatrolRoute"/> by the map builders
    /// (<c>AiPlacement.At</c> / <c>PatrolPlanner</c>). The patrol faces the waypoint's forward and does
    /// its <see cref="Action"/> for about <see cref="Duration"/> seconds before walking on; waypoints
    /// without one are a plain pause.
    /// </summary>
    public class PatrolStop : MonoBehaviour
    {
        [SerializeField] private PatrolAction _action = PatrolAction.LookAround;
        [SerializeField] private float _duration = 6f;

        public PatrolAction Action => _action;
        public float Duration => _duration;

        public void Setup(PatrolAction pAction, float pDuration)
        {
            _action = pAction;
            _duration = pDuration;
        }

        /// <summary>The standing pose that goes with an errand (for the animation).</summary>
        public static BodyPose PoseFor(PatrolAction pAction) => pAction switch
        {
            PatrolAction.LookAround or PatrolAction.CheckDoor => BodyPose.LookAround,
            PatrolAction.Smoke => BodyPose.Smoke,
            PatrolAction.Phone => BodyPose.Phone,
            PatrolAction.Lean => BodyPose.Lean,
            PatrolAction.Watch => BodyPose.ArmedIdle,
            _ => BodyPose.None
        };

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, transform.position + Vector3.up * 0.2f + transform.forward);
        }
    }
}
