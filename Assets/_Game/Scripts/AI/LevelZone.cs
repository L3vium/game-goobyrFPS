using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// One of the three zones of a map (town, lair, mansion) and the squad that guards it.
    /// <para>
    /// The squad is the shared brain of the zone's NPCs that have joined a fight: it keeps the last
    /// known position of each player they have seen, hands out combat roles (suppress, flank, push),
    /// decides who calls for reinforcements once someone actually shoots, and brings the
    /// reinforcements in through the zone's entry. The call alerts only the members within
    /// <see cref="DetectionModel.CALL_RADIUS"/> of the caller; a firefight in the town never pulls
    /// the mansion's guards.
    /// </para>
    /// </summary>
    public class LevelZone : MonoBehaviour
    {
        [SerializeField] private int _index;
        [SerializeField] private string _zoneName = "Zone";
        [SerializeField] private Vector3 _size = new Vector3(120, 60, 120);
        [SerializeField] private Transform _reinforcementEntry;
        [SerializeField] private Color _faction = new Color(0.3f, 0.3f, 0.35f);
        [SerializeField] private HatStyle _factionHat = HatStyle.Cap;
        [SerializeField] private Transform _safeRoom;

        private static readonly List<LevelZone> _all = new List<LevelZone>();
        public static IReadOnlyList<LevelZone> All => _all;

        public int Index => _index;
        public string ZoneName => _zoneName;
        public Color Faction => _faction;
        public HatStyle FactionHat => _factionHat;
        public Vector3 SafeRoom => _safeRoom != null ? _safeRoom.position : transform.position;
        public Transform ReinforcementEntry => _reinforcementEntry;

        // ---------------------------------------------------------------- squad state (host)

        public class Contact
        {
            public PlayerAvatar player;
            public Vector3 lastKnown;
            public float lastSeenTime;
            public bool visibleNow;
            public float lastGhostSend;
        }

        public readonly List<Npc> Members = new List<Npc>();
        private readonly Dictionary<ulong, Contact> _contacts = new Dictionary<ulong, Contact>();
        private int _callsMade;
        private bool _callInProgress;
        private float _visibilityResetTime;

        public bool InCombat { get; private set; }
        public float CombatSince { get; private set; }

        public void Setup(int pIndex, string pName, Vector3 pSize, Transform pEntry, Color pFaction, HatStyle pHat, Transform pSafeRoom)
        {
            _index = pIndex;
            _zoneName = pName;
            _size = pSize;
            _reinforcementEntry = pEntry;
            _faction = pFaction;
            _factionHat = pHat;
            _safeRoom = pSafeRoom;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public bool Contains(Vector3 pPoint)
        {
            Vector3 lLocal = transform.InverseTransformPoint(pPoint);
            return Mathf.Abs(lLocal.x) <= _size.x * 0.5f && Mathf.Abs(lLocal.z) <= _size.z * 0.5f && lLocal.y > -20f && lLocal.y < _size.y;
        }

        public static LevelZone Get(int pIndex)
        {
            foreach (LevelZone lZone in _all) if (lZone._index == pIndex) return lZone;
            return _all.Count > 0 ? _all[0] : null;
        }

        public static LevelZone At(Vector3 pPoint)
        {
            foreach (LevelZone lZone in _all) if (lZone.Contains(pPoint)) return lZone;
            return null;
        }

        public void ResetSquad()
        {
            Members.Clear();
            _contacts.Clear();
            _callsMade = 0;
            _callInProgress = false;
            InCombat = false;
        }

        /// <summary>An NPC sees (or clearly hears) a player: the whole squad learns where.</summary>
        public void ReportContact(PlayerAvatar pPlayer, Vector3 pPosition, bool pSeen)
        {
            if (pPlayer == null) return;
            if (!_contacts.TryGetValue(pPlayer.OwnerClientId, out Contact lContact))
            {
                lContact = new Contact { player = pPlayer };
                _contacts[pPlayer.OwnerClientId] = lContact;
            }
            lContact.lastKnown = pPosition;
            if (pSeen)
            {
                lContact.lastSeenTime = Time.time;
                lContact.visibleNow = true;
            }
            if (!InCombat)
            {
                InCombat = true;
                CombatSince = Time.time;
            }
        }

        /// <summary>A point of interest with no player attached (an alarm): the few nearest members go and search it.</summary>
        public void ReportThreatPoint(Vector3 pPoint, int pCount = 4, float pRadius = 150f)
        {
            int lSent = 0;
            foreach (Npc lMember in NearestMembers(pPoint, pRadius))
            {
                lMember.AlertToPoint(pPoint, true);
                if (++lSent >= pCount) break;
            }
        }

        private readonly List<Npc> _nearest = new List<Npc>();

        /// <summary>Living members within <paramref name="pRadius"/> of a point, nearest first (a shared list: don't keep it).</summary>
        public List<Npc> NearestMembers(Vector3 pPoint, float pRadius)
        {
            _nearest.Clear();
            float lRadiusSq = pRadius * pRadius;
            foreach (Npc lMember in Members)
                if (lMember != null && lMember.Health.IsAlive && (lMember.transform.position - pPoint).sqrMagnitude <= lRadiusSq) _nearest.Add(lMember);
            _nearest.Sort((a, b) => (a.transform.position - pPoint).sqrMagnitude.CompareTo((b.transform.position - pPoint).sqrMagnitude));
            return _nearest;
        }

        private float _alertUntil;

        /// <summary>A body was found or an alarm went off: for a while the zone's NPCs see further.</summary>
        public void RaiseAlertness(float pSeconds) => _alertUntil = Mathf.Max(_alertUntil, Time.time + pSeconds);

        /// <summary>Vision range factor of the zone's NPCs (1, or 1.2 while on edge).</summary>
        public float VisionFactor => Time.time < _alertUntil ? 1.2f : 1f;

        public Contact BestContact(Vector3 pFrom)
        {
            Contact lBest = null;
            float lBestScore = float.MaxValue;
            foreach (Contact lContact in _contacts.Values)
            {
                if (lContact.player == null || !lContact.player.IsSpawned || !lContact.player.Health.IsAlive) continue;
                float lAge = Time.time - lContact.lastSeenTime;
                if (lAge > GameDatabase.Rules.searchDuration * 1.5f) continue;
                float lScore = (lContact.lastKnown - pFrom).magnitude + lAge * 4f;
                if (lScore < lBestScore) { lBestScore = lScore; lBest = lContact; }
            }
            return lBest;
        }

        public Contact ContactFor(PlayerAvatar pPlayer) => pPlayer != null && _contacts.TryGetValue(pPlayer.OwnerClientId, out Contact lContact) ? lContact : null;

        private void Update()
        {
            if (!Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
            Members.RemoveAll(pMember => pMember == null || !pMember.IsSpawned);

            // Visibility is re-asserted by the NPCs every perception tick; clear it a bit after.
            if (Time.time > _visibilityResetTime)
            {
                _visibilityResetTime = Time.time + 0.5f;
                foreach (Contact lContact in _contacts.Values)
                {
                    bool lWasVisible = lContact.visibleNow;
                    lContact.visibleNow = Time.time - lContact.lastSeenTime < 0.45f;
                    if (lContact.player == null || !lContact.player.IsSpawned) continue;

                    // The hunted player sees their "shadow" where the squad believes they are.
                    bool lShowGhost = !lContact.visibleNow && Time.time - lContact.lastSeenTime < GameDatabase.Rules.searchDuration;
                    if (lShowGhost != lWasVisible || Time.time - lContact.lastGhostSend > 1f)
                    {
                        lContact.lastGhostSend = Time.time;
                        lContact.player.ShowGhostRpc(lContact.lastKnown, lShowGhost, Index);
                    }
                }
            }

            if (InCombat)
            {
                bool lAnyRecent = false;
                foreach (Contact lContact in _contacts.Values)
                    if (Time.time - lContact.lastSeenTime < GameDatabase.Rules.searchDuration * 2f) lAnyRecent = true;
                if (!lAnyRecent && Time.time - CombatSince > 10f)
                {
                    InCombat = false;
                    foreach (Contact lContact in _contacts.Values)
                        if (lContact.player != null && lContact.player.IsSpawned) lContact.player.ShowGhostRpc(Vector3.zero, false, Index);
                    _contacts.Clear();
                }
                else if (!_callInProgress && _callsMade < GameDatabase.Rules.maxCallsPerZone && Time.time - CombatSince > 1.5f && AnyFighting()) AssignCaller();
            }
        }

        // ---------------------------------------------------------------- roles

        /// <summary>The role the squad gives this member right now (rotates as members die).</summary>
        public CombatRole RoleOf(Npc pMember)
        {
            int lIndex = 0, lCount = 0;
            foreach (Npc lMember in Members)
            {
                if (lMember == null || !lMember.InCombat) continue;
                if (lMember == pMember) lIndex = lCount;
                lCount++;
            }
            if (pMember.Role == NpcRole.Lookout) return CombatRole.Suppress;
            if (lCount <= 1) return CombatRole.Push;
            switch (lIndex % 3)
            {
                case 0: return CombatRole.Suppress;
                case 1: return CombatRole.Flank;
                default: return lCount > 3 ? CombatRole.Push : CombatRole.Flank;
            }
        }

        /// <summary>Average direction from which the squad already covers this point (flankers go elsewhere).</summary>
        public Vector3 SquadAngleTo(Vector3 pPoint, Npc pExcept)
        {
            Vector3 lSum = Vector3.zero;
            foreach (Npc lMember in Members)
                if (lMember != null && lMember != pExcept && lMember.InCombat) lSum += (lMember.transform.position - pPoint).normalized;
            return lSum.sqrMagnitude > 0.01f ? lSum.normalized : Vector3.zero;
        }

        /// <summary>Someone of the squad is actually fighting (a search alone never calls for help).</summary>
        private bool AnyFighting()
        {
            foreach (Npc lMember in Members)
                if (lMember != null && lMember.Health.IsAlive && lMember.State.Value == NpcState.Combat) return true;
            return false;
        }

        private void AssignCaller()
        {
            Npc lBest = null;
            float lBestDistance = float.MaxValue;
            Contact lThreat = BestContact(transform.position);
            Vector3 lThreatPos = lThreat != null ? lThreat.lastKnown : transform.position;
            foreach (Npc lMember in Members)
            {
                if (lMember == null || !lMember.Health.IsAlive || !lMember.InCombat || lMember.Role == NpcRole.Target) continue;
                // The farthest from the fight is the one with time to radio.
                float lDistance = -Vector3.Distance(lMember.transform.position, lThreatPos);
                if (lDistance < lBestDistance) { lBestDistance = lDistance; lBest = lMember; }
            }
            if (lBest == null) return;
            _callInProgress = true;
            lBest.StartCall();
        }

        /// <summary>The caller finished (true) or was dropped mid-call (false).</summary>
        public void CallFinished(Npc pCaller, bool pCompleted)
        {
            _callInProgress = false;
            if (!pCompleted) return;
            _callsMade++;
            Contact lThreat = BestContact(pCaller.transform.position);
            Vector3 lPoint = lThreat != null ? lThreat.lastKnown : pCaller.transform.position;
            // The call reaches the few nearest mates around the caller, not the whole zone nor the map.
            int lSent = 0;
            foreach (Npc lMember in NearestMembers(pCaller.transform.position, DetectionModel.CALL_RADIUS))
            {
                if (lMember == pCaller || !DetectionModel.HearsCall(lMember.ZoneIndex.Value, Index, Vector3.Distance(lMember.transform.position, pCaller.transform.position))) continue;
                lMember.AlertToPoint(lPoint + Random.insideUnitSphere * 5f, true);
                if (++lSent >= DetectionModel.CALL_RESPONDERS) break;
            }
            RaiseAlertness(120f);
            Match.MatchDirector.Instance?.SpawnReinforcements(this, lPoint, GameDatabase.Rules.reinforcementsPerCall);
        }

        public enum CombatRole { Suppress, Flank, Push }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(_faction.r, _faction.g, _faction.b, 0.12f);
            Gizmos.DrawCube(new Vector3(0, _size.y * 0.5f, 0), _size);
        }
    }

    /// <summary>
    /// Map-wide alarms: the flare launchers / sirens, fired on purpose by a guard who ran to one.
    /// Every zone goes on edge (sharper eyes) and sends its few nearest members to the alarm point;
    /// the rest stay at their posts, so the map never empties toward one spot.
    /// </summary>
    public static class AlertDirector
    {
        public static bool GlobalAlarm { get; private set; }
        public static Vector3 AlarmPoint { get; private set; }

        public static void RaiseGlobal(Vector3 pPoint)
        {
            GlobalAlarm = true;
            AlarmPoint = pPoint;
            foreach (LevelZone lZone in LevelZone.All)
            {
                lZone.RaiseAlertness(180f);
                lZone.ReportThreatPoint(pPoint, 4, 150f);
            }
            // The siren itself: heard far, but only as a sound (the nearest few look, the rest glance).
            NoiseSystem.Emit(pPoint, 120f, NoiseKind.Alarming, ulong.MaxValue, false);
        }

        public static void Reset() => GlobalAlarm = false;
    }
}
