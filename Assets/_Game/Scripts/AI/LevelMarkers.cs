using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    public enum NpcRole : byte
    {
        Guard,
        Patrol,
        Lookout,
        Bodyguard,
        Target,
        Worker,
        Reinforcement
    }

    /// <summary>
    /// A spot an NPC can fight from: low (crouch behind) or high (stand behind, peek around).
    /// Placed by the map builders along walls, crates, cars, counters.
    /// </summary>
    public class CoverPoint : MonoBehaviour
    {
        [SerializeField] private bool _low = true;

        private static readonly List<CoverPoint> _all = new List<CoverPoint>();
        public static IReadOnlyList<CoverPoint> All => _all;

        public bool Low => _low;
        /// <summary>Host side: the NPC holding it.</summary>
        public Npc Occupant { get; set; }

        public void Setup(bool pLow) => _low = pLow;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        /// <summary>Is a threat at <paramref name="pThreat"/> blocked from someone crouched/standing here?</summary>
        public bool CoversFrom(Vector3 pThreat)
        {
            Vector3 lEye = transform.position + Vector3.up * (_low ? 0.9f : 1.5f);
            return Physics.Linecast(lEye, pThreat + Vector3.up * 1.2f, Core.GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Can someone here see the threat by standing up or leaning out?</summary>
        public bool CanPeek(Vector3 pThreat)
        {
            Vector3 lUp = transform.position + Vector3.up * 1.65f;
            Vector3 lSide = transform.position + Vector3.up * 1.4f + transform.right * 0.7f;
            Vector3 lOther = transform.position + Vector3.up * 1.4f - transform.right * 0.7f;
            Vector3 lTo = pThreat + Vector3.up * 1.2f;
            int lMask = Core.GameLayers.SightBlockMask;
            return !Physics.Linecast(lUp, lTo, lMask, QueryTriggerInteraction.Ignore)
                || !Physics.Linecast(lSide, lTo, lMask, QueryTriggerInteraction.Ignore)
                || !Physics.Linecast(lOther, lTo, lMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = _low ? new Color(0.2f, 0.8f, 1f) : new Color(0.2f, 0.4f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * (_low ? 0.45f : 0.9f), new Vector3(0.5f, _low ? 0.9f : 1.8f, 0.5f));
        }
    }

    /// <summary>A loop of waypoints a patrol walks, pausing at each.</summary>
    public class PatrolRoute : MonoBehaviour
    {
        [SerializeField] private float _pause = 2.5f;
        public float Pause => _pause;

        public int Count => transform.childCount;
        public Vector3 Point(int pIndex) => transform.GetChild(((pIndex % Count) + Count) % Count).position;

        public void Setup(float pPause) => _pause = pPause;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            for (int i = 0; i < Count; i++) Gizmos.DrawLine(Point(i) + Vector3.up * 0.2f, Point(i + 1) + Vector3.up * 0.2f);
        }
    }

    /// <summary>A place where a target spends time: a bar, an office desk, a garden bench, a toilet.</summary>
    public class ActivityNode : MonoBehaviour
    {
        [SerializeField] private ActivityType _activity;
        [SerializeField] private int _zone;
        [SerializeField] private float _duration = 12f;

        private static readonly List<ActivityNode> _all = new List<ActivityNode>();
        public static IReadOnlyList<ActivityNode> All => _all;

        public ActivityType Activity => _activity;
        public int Zone => _zone;
        public float Duration => _duration;

        public void Setup(ActivityType pActivity, int pZone, float pDuration)
        {
            _activity = pActivity;
            _zone = pZone;
            _duration = pDuration;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static ActivityNode Find(int pZone, ActivityType pType, Vector3 pNear)
        {
            ActivityNode lBest = null;
            float lBestDistance = float.MaxValue;
            foreach (ActivityNode lNode in _all)
            {
                if (lNode._zone != pZone || lNode._activity != pType) continue;
                float lDistance = (lNode.transform.position - pNear).sqrMagnitude + Random.Range(0f, 400f);
                if (lDistance < lBestDistance) { lBestDistance = lDistance; lBest = lNode; }
            }
            return lBest;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        }
    }

    /// <summary>Where the host spawns an NPC at the start of a match, and how it behaves.</summary>
    public class NpcSpawnPoint : MonoBehaviour
    {
        [SerializeField] private NpcRole _role = NpcRole.Guard;
        [SerializeField] private int _zone;
        [SerializeField] private PatrolRoute _route;
        [SerializeField] private ItemKind _weapon = ItemKind.Smg;

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

    /// <summary>A point where the match can start (players spawn around it) or end (extraction).</summary>
    public class MatchPoint : MonoBehaviour
    {
        public enum Kind { Start, Extraction, WeaponSpawn, UniqueWeaponSpawn, GadgetSpawn }

        [SerializeField] private Kind _kind;
        [SerializeField] private string _label = "";
        [SerializeField] private ItemKind _item = ItemKind.Smg;

        private static readonly List<MatchPoint> _all = new List<MatchPoint>();
        public static IReadOnlyList<MatchPoint> All => _all;

        public Kind PointKind => _kind;
        public string Label => _label;
        public ItemKind Item => _item;

        public void Setup(Kind pKind, string pLabel, ItemKind pItem = ItemKind.Smg)
        {
            _kind = pKind;
            _label = pLabel;
            _item = pItem;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static List<MatchPoint> OfKind(Kind pKind)
        {
            List<MatchPoint> lList = new List<MatchPoint>();
            foreach (MatchPoint lPoint in _all) if (lPoint._kind == pKind) lList.Add(lPoint);
            lList.Sort((a, b) => string.CompareOrdinal(a.name + a.transform.position, b.name + b.transform.position));
            return lList;
        }
    }
}
