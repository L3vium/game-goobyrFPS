using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// A loop of waypoints a patrol walks, pausing at each. Several NPCs on one route are a group:
    /// the first still patrolling leads, the others fall in behind (host only, rebuilt each match).
    /// </summary>
    public class PatrolRoute : MonoBehaviour
    {
        [SerializeField] private float _pause = 2.5f;
        public float Pause => _pause;

        private readonly List<Npc> _members = new List<Npc>();

        public void Join(Npc pNpc)
        {
            if (!_members.Contains(pNpc)) _members.Add(pNpc);
        }

        /// <summary>The first member still alive and on its round.</summary>
        public Npc Leader
        {
            get
            {
                foreach (Npc lMember in _members)
                    if (lMember != null && lMember.Health.IsAlive && lMember.State.Value == NpcState.Patrol) return lMember;
                return null;
            }
        }

        /// <summary>The place in the column behind the leader (1, 2, 3...).</summary>
        public int SlotOf(Npc pNpc)
        {
            int lSlot = 0;
            foreach (Npc lMember in _members)
            {
                if (lMember == null || !lMember.Health.IsAlive || lMember.State.Value != NpcState.Patrol) continue;
                if (lMember == pNpc) return lSlot;
                lSlot++;
            }
            return lSlot;
        }

        public int Count => transform.childCount;
        public Vector3 Point(int pIndex) => transform.GetChild(((pIndex % Count) + Count) % Count).position;

        public void Setup(float pPause) => _pause = pPause;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            for (int i = 0; i < Count; i++) Gizmos.DrawLine(Point(i) + Vector3.up * 0.2f, Point(i + 1) + Vector3.up * 0.2f);
        }
    }
}
