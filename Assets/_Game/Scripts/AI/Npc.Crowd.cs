using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;
using UnityEngine.AI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// The crowd's side of the brain (guests, staff, spectators - unarmed <see cref="NpcRole.Worker"/>s):
    /// the witness loop of Hitman.
    /// <list type="bullet">
    /// <item><b>Panic.</b> Startled (a scream, a frozen stare at the threat), then a run for the
    /// nearest exit that does not lead toward the threat (<see cref="ExitPoint"/>,
    /// <see cref="NpcRules.ExitScore"/>); cornered, or with the threat right on it, it cowers.</item>
    /// <item><b>Spread.</b> Its panic reaches everyone within <see cref="NpcRules.PANIC_RADIUS"/> who sees
    /// it, once per incident (<see cref="IncidentLog"/>), each a beat later than the last: a stampede.</item>
    /// <item><b>Witnesses.</b> Someone who saw a crime, a body or a gun out runs to the nearest guard
    /// (<see cref="NpcState.Report"/>, a running "!" over its head), tells it, and the guard goes to
    /// look weapon up - and knows who did it. Stop the witness before it gets there.</item>
    /// <item><b>Fights</b> close by make it run; confrontations make it stare.</item>
    /// </list>
    /// </summary>
    public partial class Npc
    {
        private static readonly IncidentLog _incidents = new IncidentLog();

        /// <summary>The incident it panics about (its panic spreads once per incident); 0: none yet.</summary>
        private int _incident;
        /// <summary>Frozen, staring at the threat, until then (the startle before the run).</summary>
        private float _startledUntil;
        /// <summary>When its panic reaches the people around it (-1: already spread).</summary>
        private float _spreadAt = -1f;
        private ExitPoint _exit;
        /// <summary>The player it runs from, when it knows (a gun out, a shooter): its distance decides cowering.</summary>
        private PlayerAvatar _threatPlayer;
        private Npc _reportTo;
        private Vector3 _reportPoint;
        private PlayerAvatar _reportCulprit;
        private HostileAct _reportAct;
        private float _reportTalkUntil, _nextGuardPick;

        private const float STARTLE = 0.6f;

        /// <summary>Host: a new match (statics outlive a match on the host).</summary>
        private static void ResetCrowd() => _incidents.Clear();

        /// <summary>
        /// Host: run from a threat at <paramref name="pFrom"/>. A crowd member startles first (frozen,
        /// staring, screaming) and passes its panic on once per incident; the target runs to its safe room.
        /// A new alarm while fleeing re-plans the escape.
        /// </summary>
        private void FleeFrom(Vector3 pFrom) => FleeFrom(pFrom, _incidents.At(pFrom, Time.time), STARTLE, true);

        private void FleeFrom(Vector3 pFrom, int pIncident, float pStartle, bool pScream)
        {
            bool lNewIncident = _incident != pIncident;
            _incident = pIncident;
            _threatPoint = pFrom;
            _fleePoint = null;
            _exit = null;
            if (State.Value != NpcState.Flee)
            {
                _startledUntil = IsCrowd ? Time.time + pStartle : 0f;
                SetState(NpcState.Flee);
                if (IsCrowd) Bark(pScream ? Barks.Scream : Barks.Help);
                // A scream: the guards close by come and look.
                if (IsCrowd && pScream) NoiseSystem.Emit(transform.position, 14f, NoiseKind.Suspicious, ulong.MaxValue, false);
            }
            if (IsCrowd && lNewIncident) _spreadAt = Mathf.Max(Time.time, _startledUntil) + Random.Range(0.1f, 0.4f);
        }

        /// <summary>The crowd runs: startle, spread, exit, cower; calm again a while after the fighting stops.</summary>
        private void UpdateCrowdFlee()
        {
            if (_threatPlayer != null && (!_threatPlayer.IsSpawned || !_threatPlayer.Health.IsAlive)) _threatPlayer = null;
            if (_threatPlayer != null && Sees(_threatPlayer)) _threatPoint = _threatPlayer.transform.position;
            if (Time.time < _startledUntil)
            {
                Stop();
                FaceTowards(_threatPoint, 400f);
                return;
            }
            if (_spreadAt >= 0f && Time.time >= _spreadAt)
            {
                _spreadAt = -1f;
                SpreadPanic();
            }

            if (_fleePoint == null)
            {
                _exit = PickExit(_threatPoint);
                if (_exit != null) _fleePoint = _exit.transform.position;
                else
                {
                    Vector3 lWish = NpcRules.FleeWish(transform.position, _threatPoint, transform.forward, 22f);
                    _fleePoint = NavMesh.SamplePosition(lWish, out NavMeshHit lHit, 8f, NavMesh.AllAreas) ? lHit.position : _home;
                }
            }
            Vector3 lToThreat = _threatPoint - transform.position;
            lToThreat.y = 0f;
            if (NpcRules.ShouldCower(lToThreat.magnitude, _exit != null || (_fleePoint.Value - transform.position).sqrMagnitude > 25f))
            {
                // Hands over the head where it stands (the pose comes from being stopped while fleeing).
                Stop();
                FaceTowards(_threatPoint, 200f);
            }
            else
            {
                MoveTo(_fleePoint.Value, RUN);
                if (Arrived(1.5f)) Stop();
            }
            if (_stateTime > 45f && (Zone == null || !Zone.InCombat))
            {
                _fleePoint = null;
                _threatPlayer = null;
                ReturnToDuty();
            }
        }

        /// <summary>The best exit away from the threat: straight-line scores first, then a real path for the two best.</summary>
        private ExitPoint PickExit(Vector3 pThreat)
        {
            ExitPoint lFirst = null, lSecond = null;
            float lFirstScore = float.MaxValue, lSecondScore = float.MaxValue;
            foreach (ExitPoint lExit in ExitPoint.All)
            {
                if (lExit == null || (lExit.Zone >= 0 && lExit.Zone != ZoneIndex.Value)) continue;
                float lScore = NpcRules.ExitScore(transform.position, pThreat, lExit.transform.position);
                if (lScore >= float.MaxValue) continue;
                if (lScore < lFirstScore) { lSecond = lFirst; lSecondScore = lFirstScore; lFirst = lExit; lFirstScore = lScore; }
                else if (lScore < lSecondScore) { lSecond = lExit; lSecondScore = lScore; }
            }
            if (lFirst != null && Reachable(lFirst.transform.position)) return lFirst;
            if (lSecond != null && Reachable(lSecond.transform.position)) return lSecond;
            return null;
        }

        private static readonly NavMeshPath _path = new NavMeshPath();

        private bool Reachable(Vector3 pPoint)
        {
            if (!_agent.isOnNavMesh) return false;
            if (!NavMesh.SamplePosition(pPoint, out NavMeshHit lHit, 4f, NavMesh.AllAreas)) return false;
            return NavMesh.CalculatePath(transform.position, lHit.position, NavMesh.AllAreas, _path) && _path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>Its panic reaches every civilian close by who sees it, a beat later each - once per incident.</summary>
        private void SpreadPanic()
        {
            foreach (Npc lOther in All)
            {
                if (lOther == this || lOther == null || !lOther.IsCrowd || !lOther.Health.IsAlive || lOther._incident == _incident) continue;
                if (lOther.State.Value == NpcState.KnockedOut || lOther.State.Value == NpcState.Report) continue;
                Vector3 lTo = lOther.transform.position - transform.position;
                if (lTo.sqrMagnitude > NpcRules.PANIC_RADIUS * NpcRules.PANIC_RADIUS) continue;
                if (!Senses.Clear(Eye, lOther.Eye)) continue;
                lOther._threatPlayer = _threatPlayer;
                lOther.FleeFrom(_threatPoint, _incident, Random.Range(0.25f, 0.7f), false);
            }
        }

        /// <summary>
        /// Host: a civilian saw something it should tell (a crime by <paramref name="pCulprit"/>, a body, a gun
        /// out): it runs to the nearest guard to report - or, with no guard within reach, it runs away.
        /// </summary>
        private void Witness(Vector3 pWhere, PlayerAvatar pCulprit, HostileAct pAct, ulong pBody = NO_BODY)
        {
            if (!IsCrowd || !Health.IsAlive || State.Value == NpcState.Report || State.Value == NpcState.KnockedOut) return;
            _threatPlayer = pCulprit;
            Npc lGuard = NearestGuard();
            if (!NpcRules.Reports(true, lGuard != null ? Vector3.Distance(lGuard.transform.position, transform.position) : float.MaxValue))
            {
                FleeFrom(pWhere);
                return;
            }
            _incident = _incidents.At(pWhere, Time.time);
            _reportBody = pBody;
            _reportTo = lGuard;
            _reportPoint = pWhere;
            _reportCulprit = pCulprit;
            _reportAct = pAct;
            _reportTalkUntil = 0f;
            _nextGuardPick = Time.time + 1.5f;
            _threatPoint = pWhere;
            _startledUntil = Time.time + STARTLE;
            SetState(NpcState.Report);
            Bark(Barks.Scream);
            WitnessRunsRpc();
        }

        /// <summary>The nearest armed guard free to listen (alive, conscious, not fighting), within report range.</summary>
        private Npc NearestGuard()
        {
            Npc lBest = null;
            float lBest2 = NpcRules.REPORT_RANGE * NpcRules.REPORT_RANGE;
            foreach (Npc lOther in All)
            {
                if (lOther == null || !lOther.IsArmedGuard || !lOther.Health.IsAlive || lOther.InCombat || lOther.State.Value == NpcState.KnockedOut) continue;
                float lDistance2 = (lOther.transform.position - transform.position).sqrMagnitude;
                if (lDistance2 < lBest2) { lBest2 = lDistance2; lBest = lOther; }
            }
            return lBest;
        }

        /// <summary>The witness runs to its guard, tells it (a moment face to face), then runs for an exit.</summary>
        private void UpdateReport()
        {
            if (Time.time < _startledUntil)
            {
                Stop();
                FaceTowards(_threatPoint, 400f);
                return;
            }
            bool lLost = _reportTo == null || !_reportTo.Health.IsAlive || _reportTo.State.Value == NpcState.KnockedOut || _reportTo.InCombat;
            if (_reportTalkUntil <= 0f && (lLost || Time.time >= _nextGuardPick))
            {
                _nextGuardPick = Time.time + 1.5f;
                Npc lGuard = NearestGuard();
                if (lGuard != null) _reportTo = lGuard;
                else if (lLost) { FleeFrom(_reportPoint); return; }
            }
            if (_reportTo == null) { FleeFrom(_reportPoint); return; }
            if (_reportTalkUntil <= 0f)
            {
                MoveTo(_reportTo.transform.position, RUN);
                if (Vector3.Distance(transform.position, _reportTo.transform.position) <= 2.4f)
                {
                    _reportTalkUntil = Time.time + 1.6f;
                    _reportTo.HearWitness(this);
                    Bark(Barks.OverThere);
                }
                else if (_stateTime > 40f) FleeFrom(_reportPoint);
                return;
            }
            Stop();
            FaceTowards(_reportTo.transform.position, 300f);
            if (Time.time < _reportTalkUntil) return;
            _reportTo.ReceiveReport(_reportPoint, _reportCulprit, _reportAct, _reportBody);
            _reportTo = null;
            _reportBody = NO_BODY;
            FleeFrom(_reportPoint, _incident, 0f, false);
        }

        /// <summary>Host: a witness runs up to this guard: it turns to listen.</summary>
        private void HearWitness(Npc pWitness)
        {
            if (!OnDuty) return;
            _glancePoint = pWitness.transform.position;
            _glanceUntil = Time.time + 2f;
        }

        /// <summary>
        /// Host: a witness told this guard what it saw at <paramref name="pWhere"/>: it goes to look,
        /// weapon up, and the zone stays on edge; if the witness saw who did it, the zone knows them.
        /// </summary>
        public void ReceiveReport(Vector3 pWhere, PlayerAvatar pCulprit, HostileAct pAct, ulong pBody = NO_BODY)
        {
            if (!Health.IsAlive) return;
            if (pCulprit != null) Zone?.MarkHostile(pCulprit, pAct);
            // A body: it radios it in like one it found itself.
            if (pBody != NO_BODY) RadioBodyIn(pBody, pWhere);
            Zone?.RaiseAlertness(60f);
            Bark(Barks.Radio);
            AlertToPoint(pWhere, true);
        }

        /// <summary>A crowd member's gauge filled on a player: a gun out (or a known hostile) is reported; anything else only earns a stare.</summary>
        private void CrowdSpotted(PlayerAvatar pPlayer)
        {
            if (WeaponDrawn(pPlayer) || IsHostile(pPlayer))
            {
                Witness(pPlayer.transform.position, pPlayer, HostileAct.DrawnGun);
                return;
            }
            Bark(Barks.Huh);
            _awareness = DetectionModel.INVESTIGATE - 0.05f;
            Glance(pPlayer.transform.position, _awareness);
        }

        /// <summary>A fight close by and in view: the crowd runs from it; a confrontation only makes it stare.</summary>
        private void CrowdWatchFights()
        {
            if (State.Value == NpcState.Flee || State.Value == NpcState.Report || State.Value == NpcState.Sick || !IsCrowd) return;
            foreach (Npc lOther in _hot)
            {
                if (lOther == this || lOther == null || !lOther.Health.IsAlive) continue;
                Vector3 lTo = lOther.Eye - Eye;
                float lDistance = lTo.magnitude;
                if (lDistance > DetectionModel.WITNESS_RANGE * 1.4f) continue;
                if (!DetectionModel.Witnesses(lDistance * 0.7f, Vector3.Angle(transform.forward, lTo), Senses.Clear(Eye, lOther.Eye))) continue;
                if (lOther.State.Value == NpcState.Engaged) Glance(lOther.transform.position, 0.3f);
                else
                {
                    _threatPlayer = lOther._combatTarget;
                    FleeFrom(lOther._combatTarget != null ? lOther._combatTarget.transform.position : lOther.transform.position);
                }
                return;
            }
        }

        /// <summary>Everyone: a witness runs for a guard - the players close by are told (stop them).</summary>
        [Unity.Netcode.Rpc(Unity.Netcode.SendTo.Everyone)]
        private void WitnessRunsRpc()
        {
            PlayerAvatar lMe = PlayerAvatar.Local;
            if (lMe == null || (lMe.transform.position - transform.position).sqrMagnitude > 60f * 60f) return;
            UI.Hud.Notify("WITNESS", "RUNNING TO A GUARD", UI.ThreatMarkers.Amber);
        }
    }
}
