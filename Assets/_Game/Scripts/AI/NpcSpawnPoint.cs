using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>Where the host spawns an NPC at the start of a match, and how it behaves.</summary>
    public class NpcSpawnPoint : MonoBehaviour
    {
        [SerializeField] private NpcRole _role = NpcRole.Guard;
        [SerializeField] private int _zone;
        [SerializeField] private PatrolRoute _route;
        [SerializeField] private ItemKind _weapon = ItemKind.Smg;
        /// <summary>A pass, key or tool carried and dropped when taken down (Fists = nothing).</summary>
        [SerializeField] private ItemKind _carries = ItemKind.Fists;
        public ItemKind Carries { get => _carries; set => _carries = value; }
        /// <summary>Sees through passes (a security chief, a captain): like bodyguards and lookouts.</summary>
        [SerializeField] private bool _enforcer;
        public bool Enforcer { get => _enforcer; set => _enforcer = value; }

        public NpcRole Role => _role;
        public int Zone => _zone;
        public PatrolRoute Route => _route;
        public ItemKind Weapon => _weapon;

        public void Setup(NpcRole pRole, int pZone, PatrolRoute pRoute, ItemKind pWeapon)
        {
            _role = pRole;
            _zone = pZone;
            _route = pRoute;
            _weapon = pWeapon;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _role == NpcRole.Lookout ? Color.magenta : _role == NpcRole.Patrol ? Color.yellow : Color.red;
            Gizmos.DrawSphere(transform.position + Vector3.up, 0.3f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward);
        }
    }
}
