using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    public enum NpcState : byte
    {
        Idle,
        Patrol,
        Routine,
        Escort,
        Investigate,
        Combat,
        Search,
        Calling,
        Flee,
        Sick,
        KnockedOut,
        Dead
    }

    /// <summary>
    /// Every NPC of the game - guard, patrol, lookout, bodyguard, target - and every one of them is
    /// hostile to every player. The brain runs on the host only; clients get the state, the
    /// awareness and the shots through network variables and RPCs.
    /// <para>
    /// <b>Senses.</b> Sight is a cone weighed by distance, light (power cuts), stance, grass and
    /// smoke; exposure fills an awareness gauge ("?" then "!") instead of detecting instantly.
    /// Hearing comes from the <see cref="NoiseSystem"/> and from players running close by.
    /// </para>
    /// <para>
    /// <b>Combat.</b> An NPC never knows where a player is unless it sees them: the squad
    /// (<see cref="LevelZone"/>) shares the last known position, NPCs shoot at it, take cover from
    /// it, flank it and push it - so a player who moves unseen from cover to cover can come round
    /// behind them. Lost contact turns into a search around that position, then back to duty.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public class Npc : NetworkBehaviour
    {
        public static readonly List<Npc> All = new List<Npc>();

        public readonly NetworkVariable<NpcState> State = new NetworkVariable<NpcState>(NpcState.Idle);
        public readonly NetworkVariable<byte> Awareness = new NetworkVariable<byte>(0);
        public readonly NetworkVariable<NpcRole> RoleVar = new NetworkVariable<NpcRole>(NpcRole.Guard);
        public readonly NetworkVariable<int> WeaponItem = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> ZoneIndex = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> TargetProfile = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<float> AimPitch = new NetworkVariable<float>(0f);

        public Damageable Health { get; private set; }
        public NpcRole Role => RoleVar.Value;
        public bool InCombat => State.Value == NpcState.Combat || State.Value == NpcState.Calling || State.Value == NpcState.Search;
        public bool IsTarget => Role == NpcRole.Target;
        public LevelZone Zone { get; private set; }
        public ItemDefinition Weapon => GameDatabase.Item(WeaponItem.Value);
        public Vector3 Eye => transform.position + Vector3.up * 1.55f * transform.localScale.y;

        private NavMeshAgent _agent;
        private GoobBody _body;
        private GoobAnimator _animator;
        private NpcVisuals _visuals;

        // ---------------------------------------------------------------- host-side brain state

        private Vector3 _home;
        private Quaternion _homeRotation;
        private PatrolRoute _route;
        private int _routeIndex;
        private float _waitUntil;
        private float _awareness;
        private float _perceptionTimer;
        private PlayerAvatar _suspect;
        private Vector3 _investigatePoint;
        private bool _investigateAlarmed;
        private Distraction _distraction;
        private Breaker _breaker;
        private float _stateTime;
        private PlayerAvatar _combatTarget;
        private bool _targetVisible;
        private float _nextShot;
        private int _burstLeft;
        private float _repositionTime;
        private CoverPoint _cover;
        private float _reactionUntil;
        private float _callTimer;
        private float _searchUntil;
        private Vector3 _searchCenter;
        private Npc _principal;
        private List<ActivityType> _routine;
        private int _routineIndex;
        private ActivityNode _activityNode;
        private float _sickUntil;
        private ulong _lastAttacker = DamageInfo.NO_PLAYER;
        private readonly HashSet<ulong> _seenBodies = new HashSet<ulong>();
        private bool _wantsAlarm;

        private const float WALK = 1.9f, RUN = 4.9f;

        private void Awake()
        {
            Health = GetComponent<Damageable>();
            _agent = GetComponent<NavMeshAgent>();
            _body = GetComponentInChildren<GoobBody>();
            _animator = GetComponentInChildren<GoobAnimator>();
            _visuals = GetComponent<NpcVisuals>();
        }

        // ---------------------------------------------------------------- spawn

        /// <summary>Host: set up a freshly spawned NPC.</summary>
        public void InitServer(NpcRole pRole, int pZone, ItemDefinition pWeapon, PatrolRoute pRoute, Npc pPrincipal, int pTargetProfile, List<ActivityType> pRoutine)
        {
            RoleVar.Value = pRole;
            ZoneIndex.Value = pZone;
            WeaponItem.Value = pWeapon != null ? pWeapon.Index : -1;
            TargetProfile.Value = pTargetProfile;
            _route = pRoute;
            _principal = pPrincipal;
            _routine = pRoutine;
            _home = transform.position;
            _homeRotation = transform.rotation;
            Zone = LevelZone.Get(pZone);
            Zone?.Members.Add(this);
            State.Value = pRole switch
            {
                NpcRole.Patrol => NpcState.Patrol,
                NpcRole.Bodyguard => NpcState.Escort,
                NpcRole.Target => NpcState.Routine,
                _ => NpcState.Idle
            };
            ApplyLook();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            Health.Team = Damageable.NPC_TEAM;
            Health.HeadHeight = 1.3f;
            Health.InitHealth(GameDatabase.Rules.npcHealth * (Role == NpcRole.Target ? 1.2f : 1f));
            Health.OnDeathClient += HandleDeathClient;
            Health.OnHitClient += HandleHitClient;
            if (IsServer)
            {
                Health.OnDamagedServer += HandleDamagedServer;
                Health.OnDeathServer += HandleDeathServer;
                NoiseSystem.OnNoise += HandleNoise;
                BreakerAlert.OnBreaker += HandleBreaker;
                _agent.enabled = true;
                _agent.speed = WALK;
                _agent.angularSpeed = 540f;
                _agent.acceleration = 20f;
            }
            else _agent.enabled = false;

            Zone = LevelZone.Get(ZoneIndex.Value);
            RoleVar.OnValueChanged += (_, __) => ApplyLook();
            TargetProfile.OnValueChanged += (_, __) => ApplyLook();
            WeaponItem.OnValueChanged += (_, __) => _visuals?.RefreshWeapon();
            ApplyLook();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (IsServer)
            {
                NoiseSystem.OnNoise -= HandleNoise;
                BreakerAlert.OnBreaker -= HandleBreaker;
                if (_cover != null && _cover.Occupant == this) _cover.Occupant = null;
            }
        }

        private void ApplyLook()
        {
            if (_body == null) return;
            Zone = LevelZone.Get(ZoneIndex.Value);
            System.Random lRandom = new System.Random((int)NetworkObjectId * 7919 + 13);
            Color lSkin = GameArt.Vary(new Color(1f, 0.8f, 0.66f), 0.25f, lRandom);
            if (Role == NpcRole.Target)
            {
                MapDefinition lMap = Net.LobbyState.Instance != null ? Net.LobbyState.Instance.Map : null;
                TargetProfile lProfile = lMap != null && TargetProfile.Value >= 0 && TargetProfile.Value < lMap.targets.Count ? lMap.targets[TargetProfile.Value] : null;
                if (lProfile != null) _body.Configure(lProfile.bodyColor, lSkin, lProfile.hat, lProfile.bodyColor * 0.4f, lProfile.scale);
                else _body.Configure(new Color(0.9f, 0.8f, 0.2f), lSkin, HatStyle.Crown, Color.yellow, 1.15f);
            }
            else
            {
                Color lFaction = Zone != null ? Zone.Faction : new Color(0.3f, 0.3f, 0.35f);
                Color lBody = GameArt.Vary(lFaction, 0.15f, lRandom);
                HatStyle lHat = Role == NpcRole.Bodyguard ? HatStyle.None : Role == NpcRole.Lookout ? HatStyle.Beret : Zone != null ? Zone.FactionHat : HatStyle.Cap;
                _body.Configure(lBody, lSkin, lHat, lFaction * 0.5f, Role == NpcRole.Bodyguard ? 1.12f : 0.95f + (float)lRandom.NextDouble() * 0.12f);
            }
            _visuals?.RefreshWeapon();
        }

        // ---------------------------------------------------------------- host loop

        private void Update()
        {
            if (_animator != null)
            {
                _animator.AimPitch = AimPitch.Value;
                _animator.HoldingWeapon = Weapon != null && (InCombat || State.Value == NpcState.Investigate || Role == NpcRole.Lookout);
                _animator.Calling = State.Value == NpcState.Calling;
                _animator.Sick = State.Value == NpcState.Sick;
                _animator.Panic = State.Value == NpcState.Flee ? 1f : 0f;
                _animator.Crouched = InCombat && _cover != null && _cover.Low && !_targetVisible;
            }

            if (!IsServer || !IsSpawned || !Health.IsAlive) return;
            float dt = Time.deltaTime;
            _stateTime += dt;

            _perceptionTimer -= dt;
            if (_perceptionTimer <= 0f)
            {
                _perceptionTimer = 0.15f + Random.value * 0.05f;
                Perceive(0.18f);
            }

            switch (State.Value)
            {
                case NpcState.Idle: UpdateIdle(); break;
                case NpcState.Patrol: UpdatePatrol(); break;
                case NpcState.Routine: UpdateRoutine(); break;
                case NpcState.Escort: UpdateEscort(); break;
                case NpcState.Investigate: UpdateInvestigate(); break;
                case NpcState.Combat: UpdateCombat(dt); break;
                case NpcState.Search: UpdateSearch(); break;
                case NpcState.Calling: UpdateCalling(dt); break;
                case NpcState.Flee: UpdateFlee(); break;
                case NpcState.Sick: UpdateSick(); break;
            }

            byte lAware = (byte)Mathf.RoundToInt(Mathf.Clamp01(_awareness) * 20f);
            if (Awareness.Value != lAware) Awareness.Value = lAware;
        }

        private void SetState(NpcState pState)
        {
            if (State.Value == pState) return;
            if (State.Value == NpcState.Calling && pState != NpcState.Calling) Zone?.CallFinished(this, false);
            State.Value = pState;
            _stateTime = 0f;
            if (pState != NpcState.Combat && _cover != null) { if (_cover.Occupant == this) _cover.Occupant = null; _cover = null; }
        }

        private bool MoveTo(Vector3 pPoint, float pSpeed)
        {
            if (!_agent.isOnNavMesh) return false;
            _agent.speed = pSpeed;
            if (NavMesh.SamplePosition(pPoint, out NavMeshHit lHit, 4f, NavMesh.AllAreas)) pPoint = lHit.position;
            if ((_agent.destination - pPoint).sqrMagnitude > 0.25f) _agent.SetDestination(pPoint);
            _agent.isStopped = false;
            return true;
        }

        private bool Arrived(float pTolerance = 0.6f) => _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance <= Mathf.Max(pTolerance, _agent.stoppingDistance);

        private void Stop()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
        }

        private void FaceTowards(Vector3 pPoint, float pSpeed = 360f)
        {
            Vector3 lFlat = pPoint - transform.position;
            lFlat.y = 0f;
            if (lFlat.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(lFlat), pSpeed * Time.deltaTime);
        }

        // ---------------------------------------------------------------- perception

        private void Perceive(float dt)
        {
            GameRules lRules = GameDatabase.Rules;
            PlayerAvatar lBest = null;
            float lBestExposure = 0f;
            bool lCombat = InCombat;

            foreach (PlayerAvatar lPlayer in PlayerAvatar.All)
            {
                if (lPlayer == null || !lPlayer.IsSpawned || !lPlayer.Health.IsAlive) continue;
                float lExposure = Exposure(lPlayer, lRules, lCombat);
                if (lExposure > lBestExposure) { lBestExposure = lExposure; lBest = lPlayer; }

                // Hearing a player run (or walk) close by.
                if (lExposure <= 0f && !lPlayer.Hidden.Value)
                {
                    float lDistance = Vector3.Distance(lPlayer.transform.position, transform.position);
                    float lHear = lPlayer.Sprinting.Value ? 10f : lPlayer.Crouched.Value ? 0f : 3.5f;
                    if (lDistance < lHear)
                    {
                        if (lCombat) Zone?.ReportContact(lPlayer, lPlayer.transform.position, false);
                        else if (State.Value != NpcState.Investigate) Investigate(lPlayer.transform.position, false);
                    }
                }
            }

            if (lBest != null)
            {
                float lRate = lBestExposure / Mathf.Max(0.1f, lRules.npcDetectTime);
                _awareness += lRate * dt * (lCombat ? 3f : 1f);
                _suspect = lBest;
                if (_awareness >= 1f)
                {
                    _awareness = 1f;
                    Spot(lBest);
                }
                else if (_awareness > 0.35f && !lCombat && State.Value != NpcState.Investigate)
                    Investigate(lBest.transform.position, false);
            }
            else
            {
                _awareness = Mathf.Max(lCombat ? 0.6f : 0f, _awareness - dt * 0.25f);
                if (_combatTarget != null) _targetVisible = false;
            }

            if (!lCombat) LookForBodies();
        }

        /// <summary>0 = unseen, 1 = fully exposed. Distance, cone, light, stance, grass, smoke, walls.</summary>
        private float Exposure(PlayerAvatar pPlayer, GameRules pRules, bool pCombat)
        {
            if (pPlayer.Hidden.Value) return 0f;
            Vector3 lEye = Eye;
            Vector3 lTarget = pPlayer.Chest;
            Vector3 lTo = lTarget - lEye;
            float lDistance = lTo.magnitude;

            float lRange = pRules.npcVisionRange * (Role == NpcRole.Lookout ? 1.8f : 1f) * BuildingVolume.LightFactor(lTarget) * (pCombat ? 1.5f : 1f);
            if (pPlayer.CarriesProof.Value) lRange *= 1.2f;
            if (lDistance > lRange) return 0f;

            float lAngle = Vector3.Angle(transform.forward, lTo);
            float lHalfCone = pRules.npcVisionAngle * 0.5f * (pCombat ? 1.4f : 1f);
            bool lPeripheral = lDistance < 2.2f;
            if (lAngle > lHalfCone && !lPeripheral) return 0f;

            float lStance = 1f;
            if (pPlayer.Crouched.Value) lStance *= 0.55f;
            if (pPlayer.InGrass.Value) lStance *= pPlayer.Crouched.Value ? 0.08f : 0.6f;
            if (pPlayer.Sprinting.Value) lStance *= 1.3f;
            if (lStance < 0.1f && lDistance > 3f) return 0f;

            if (SmokeVolume.Blocks(lEye, lTarget)) return 0f;
            bool lChest = !Physics.Linecast(lEye, lTarget, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
            bool lHead = !Physics.Linecast(lEye, pPlayer.Eye, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
            if (!lChest && !lHead) return 0f;

            float lCloseness = Mathf.Lerp(2.5f, 0.35f, lDistance / lRange);
            float lCentered = Mathf.Lerp(1f, 0.45f, lAngle / Mathf.Max(1f, lHalfCone));
            return lCloseness * lCentered * lStance * (lChest ? 1f : 0.6f);
        }

        private void Spot(PlayerAvatar pPlayer)
        {
            bool lFirst = !InCombat;
            _combatTarget = pPlayer;
            _targetVisible = true;
            Zone?.ReportContact(pPlayer, pPlayer.transform.position, true);
            if (lFirst)
            {
                _reactionUntil = Time.time + GameDatabase.Rules.npcReactionTime + Random.value * 0.25f;
                SpottedRpc();
                if (IsTarget) { SetState(NpcState.Flee); return; }
                EnterCombat();
                // A spotter in a zone with a flare launcher may go fire it.
                _wantsAlarm = Random.value < 0.3f && WorldRegistry.Nearest<AlarmTrigger>(transform.position, 45f, a => a.State == 0) != null;
            }
        }

        private void LookForBodies()
        {
            foreach (Npc lOther in All)
            {
                if (lOther == this || lOther.Health.IsAlive || _seenBodies.Contains(lOther.NetworkObjectId)) continue;
                Vector3 lBody = lOther.transform.position + Vector3.up * 0.3f;
                float lDistance = Vector3.Distance(Eye, lBody);
                if (lDistance > 16f * BuildingVolume.LightFactor(lBody)) continue;
                if (Vector3.Angle(transform.forward, lBody - Eye) > GameDatabase.Rules.npcVisionAngle * 0.5f) continue;
                if (Physics.Linecast(Eye, lBody, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore)) continue;
                _seenBodies.Add(lOther.NetworkObjectId);
                Investigate(lOther.transform.position, true);
                if (Zone != null) foreach (Npc lMate in Zone.Members) if (lMate != null) lMate._seenBodies.Add(lOther.NetworkObjectId);
                return;
            }
        }

        private void HandleNoise(NoiseEvent pNoise)
        {
            if (!Health.IsAlive || State.Value == NpcState.KnockedOut) return;
            float lDistance = Vector3.Distance(pNoise.position, transform.position);
            if (lDistance > pNoise.radius) return;

            switch (pNoise.kind)
            {
                case NoiseKind.Alarming:
                    if (IsTarget) { SetState(NpcState.Flee); return; }
                    if (State.Value == NpcState.Sick) return;
                    AlertToPoint(pNoise.position, true);
                    PlayerAvatar lShooter = PlayerAvatar.ForClient(pNoise.source);
                    if (lShooter != null && lDistance < pNoise.radius * 0.6f) Zone?.ReportContact(lShooter, lShooter.transform.position, false);
                    break;
                case NoiseKind.Suspicious:
                case NoiseKind.Distraction:
                    if (InCombat || State.Value == NpcState.Flee || State.Value == NpcState.Sick) return;
                    // Only the nearest few NPCs walk to a distraction; the rest glance.
                    if (pNoise.kind == NoiseKind.Distraction && !IsNearestTo(pNoise.position, 2)) { FaceTowards(pNoise.position, 9999f); return; }
                    if (IsTarget && pNoise.kind == NoiseKind.Distraction && !IsNearestTo(pNoise.position, 1)) return;
                    Investigate(pNoise.position, pNoise.kind == NoiseKind.Suspicious);
                    _distraction = WorldRegistry.Nearest<Distraction>(pNoise.position, 2.5f, d => d.State == 1);
                    break;
            }
        }

        private bool IsNearestTo(Vector3 pPoint, int pRank)
        {
            float lMine = Vector3.Distance(pPoint, transform.position);
            int lCloser = 0;
            foreach (Npc lOther in All)
            {
                if (lOther == this || !lOther.Health.IsAlive || lOther.InCombat || lOther.IsTarget) continue;
                if (Vector3.Distance(pPoint, lOther.transform.position) < lMine) lCloser++;
                if (lCloser >= pRank) return false;
            }
            return true;
        }

        private void HandleBreaker(Breaker pBreaker)
        {
            if (!Health.IsAlive || InCombat || IsTarget || pBreaker.PowerOn) return;
            if (pBreaker.Building == null || !pBreaker.Building.Contains(transform.position + Vector3.up)) return;
            if (!IsNearestTo(pBreaker.transform.position, 1)) return;
            _breaker = pBreaker;
            Investigate(pBreaker.transform.position + pBreaker.transform.forward, false);
        }

        /// <summary>Host: an explosion, gunfire, a global alarm - go there ready to fight.</summary>
        public void AlertToPoint(Vector3 pPoint, bool pAlarmed)
        {
            if (!Health.IsAlive || State.Value == NpcState.KnockedOut || State.Value == NpcState.Sick) return;
            if (IsTarget) { SetState(NpcState.Flee); return; }
            if (InCombat) { if (_combatTarget == null) _searchCenter = pPoint; return; }
            _awareness = Mathf.Max(_awareness, 0.7f);
            _searchCenter = pPoint;
            _searchUntil = Time.time + GameDatabase.Rules.searchDuration;
            SetState(NpcState.Search);
            MoveTo(pPoint + Random.insideUnitSphere * 4f, RUN);
        }

        private void Investigate(Vector3 pPoint, bool pAlarmed)
        {
            if (IsTarget && pAlarmed) { SetState(NpcState.Flee); return; }
            _investigatePoint = pPoint;
            _investigateAlarmed = pAlarmed;
            _awareness = Mathf.Max(_awareness, 0.3f);
            SetState(NpcState.Investigate);
            MoveTo(pPoint, pAlarmed ? RUN * 0.75f : WALK * 1.15f);
        }

        // ---------------------------------------------------------------- non-combat states

        private void UpdateIdle()
        {
            if (Vector3.Distance(transform.position, _home) > 1.2f)
            {
                MoveTo(_home, WALK);
                return;
            }
            Stop();
            float lScan = Role == NpcRole.Lookout ? Mathf.Sin(Time.time * 0.25f + NetworkObjectId) * 70f : Mathf.Sin(Time.time * 0.15f + NetworkObjectId) * 30f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _homeRotation * Quaternion.Euler(0, lScan, 0), 40f * Time.deltaTime);
        }

        private void UpdatePatrol()
        {
            if (_route == null || _route.Count == 0) { SetState(NpcState.Idle); return; }
            if (Time.time < _waitUntil) { Stop(); return; }
            Vector3 lPoint = _route.Point(_routeIndex);
            MoveTo(lPoint, WALK);
            if (Arrived(0.8f))
            {
                _routeIndex++;
                _waitUntil = Time.time + _route.Pause * Random.Range(0.6f, 1.4f);
            }
        }

        private void UpdateRoutine()
        {
            if (_routine == null || _routine.Count == 0) { UpdateIdle(); return; }
            if (_activityNode == null)
            {
                ActivityType lType = _routine[_routineIndex % _routine.Count];
                _activityNode = ActivityNode.Find(ZoneIndex.Value, lType, transform.position) ?? ActivityNode.Find(ZoneIndex.Value, ActivityType.Idle, transform.position);
                _waitUntil = 0f;
                if (_activityNode == null) { _routineIndex++; UpdateIdle(); return; }
            }

            if (_waitUntil <= 0f)
            {
                MoveTo(_activityNode.transform.position, WALK);
                if (!Arrived(0.9f)) return;
                _waitUntil = Time.time + _activityNode.Duration * Random.Range(0.8f, 1.3f);
                transform.rotation = _activityNode.transform.rotation;
                if (_activityNode.Activity == ActivityType.Drink) DrinkNearby();
            }
            else
            {
                Stop();
                FaceTowards(_activityNode.transform.position + _activityNode.transform.forward, 120f);
                if (Time.time > _waitUntil)
                {
                    _routineIndex++;
                    _activityNode = null;
                }
            }
        }

        private void DrinkNearby()
        {
            Drink lDrink = WorldRegistry.Nearest<Drink>(transform.position, 3.5f, d => d.State != Drink.EMPTY);
            if (lDrink == null) return;
            int lPoison = lDrink.DrinkServer();
            ulong lPoisoner = lDrink.Poisoner;
            int lTeam = lDrink.PoisonerTeam;
            lDrink.Invoke(nameof(Drink.RefillServer), 40f);
            if (lPoison == Drink.LETHAL)
                Health.Kill(new DamageInfo { type = DamageType.Poison, point = transform.position, direction = transform.forward, attacker = lPoisoner, attackerTeam = lTeam });
            else if (lPoison == Drink.EMETIC) GetSick();
        }

        /// <summary>Host: emetic poison - run to the nearest toilet and stay there, alone.</summary>
        public void GetSick()
        {
            _sickUntil = Time.time + 22f;
            SetState(NpcState.Sick);
            Toilet lToilet = Toilet.Nearest(transform.position);
            if (lToilet != null) MoveTo(lToilet.StandPoint, RUN * 0.8f);
        }

        private void UpdateSick()
        {
            Toilet lToilet = Toilet.Nearest(transform.position);
            if (lToilet != null && Vector3.Distance(transform.position, lToilet.StandPoint) > 1f) MoveTo(lToilet.StandPoint, RUN * 0.8f);
            else
            {
                Stop();
                if (lToilet != null) FaceTowards(lToilet.transform.position);
                if (Random.value < 0.02f) PukeRpc();
            }
            if (Time.time > _sickUntil) SetState(IsTarget ? NpcState.Routine : NpcState.Idle);
        }

        private void UpdateEscort()
        {
            if (_principal == null || !_principal.Health.IsAlive)
            {
                // The principal is gone: the bodyguard turns into a furious guard.
                RoleVar.Value = NpcRole.Guard;
                _home = transform.position;
                SetState(NpcState.Search);
                _searchCenter = transform.position;
                _searchUntil = Time.time + GameDatabase.Rules.searchDuration;
                return;
            }
            // Wait outside when the principal goes to the toilet.
            float lKeep = _principal.State.Value == NpcState.Sick ? 6f : 2.6f;
            Vector3 lSlot = _principal.transform.position + _principal.transform.rotation * new Vector3((NetworkObjectId % 2 == 0 ? 1f : -1f) * 1.6f, 0, -1.4f);
            if (Vector3.Distance(transform.position, _principal.transform.position) > lKeep) MoveTo(lSlot, _principal.State.Value == NpcState.Flee ? RUN : WALK * 1.3f);
            else { Stop(); FaceTowards(transform.position + _principal.transform.forward); }
        }

        private void UpdateInvestigate()
        {
            if (_breaker != null && Arrived(1.2f))
            {
                if (!_breaker.PowerOn) _breaker.ToggleServer(ulong.MaxValue);
                _breaker = null;
                _waitUntil = Time.time + 2f;
            }
            if (_distraction != null && Vector3.Distance(transform.position, _distraction.transform.position) < 2f)
            {
                _distraction.TurnOffServer();
                _distraction = null;
                _waitUntil = Time.time + 2f;
            }

            MoveTo(_investigatePoint, _investigateAlarmed ? RUN * 0.75f : WALK * 1.15f);
            if (Arrived(1.5f))
            {
                Stop();
                transform.Rotate(0, Mathf.Sin(_stateTime * 1.4f) * 90f * Time.deltaTime, 0);
                if (_waitUntil < Time.time - 30f) _waitUntil = Time.time + (_investigateAlarmed ? 7f : 4f);
                if (Time.time > _waitUntil) ReturnToDuty();
            }
            else if (_stateTime > 25f) ReturnToDuty();
        }

        private void ReturnToDuty()
        {
            _waitUntil = 0f;
            _awareness = Mathf.Min(_awareness, 0.2f);
            SetState(Role switch
            {
                NpcRole.Patrol => NpcState.Patrol,
                NpcRole.Bodyguard => NpcState.Escort,
                NpcRole.Target => NpcState.Routine,
                _ => NpcState.Idle
            });
        }

        private void UpdateFlee()
        {
            // The target bolts to its zone's safe room and hides there; it fights only if cornered.
            Vector3 lSafe = Zone != null ? Zone.SafeRoom : _home;
            MoveTo(lSafe, RUN * 0.9f);
            if (Arrived(1.5f))
            {
                Stop();
                if (_combatTarget != null && _targetVisible && Weapon != null) TryShoot(Time.deltaTime);
            }
            if (_stateTime > 60f && (Zone == null || !Zone.InCombat)) ReturnToDuty();
        }

        // ---------------------------------------------------------------- combat

        private void EnterCombat()
        {
            SetState(NpcState.Combat);
            _repositionTime = 0f;
            _burstLeft = 0;
        }

        private void UpdateCombat(float dt)
        {
            LevelZone.Contact lContact = _combatTarget != null ? Zone?.ContactFor(_combatTarget) : Zone?.BestContact(transform.position);
            if (lContact == null || lContact.player == null || !lContact.player.Health.IsAlive)
            {
                lContact = Zone?.BestContact(transform.position);
                _combatTarget = lContact?.player;
                if (lContact == null)
                {
                    _searchCenter = _combatTarget != null ? _combatTarget.transform.position : transform.position;
                    _searchUntil = Time.time + GameDatabase.Rules.searchDuration;
                    SetState(NpcState.Search);
                    return;
                }
            }

            Vector3 lKnown = lContact.lastKnown;
            _targetVisible = lContact.visibleNow && _combatTarget != null && Exposure(_combatTarget, GameDatabase.Rules, true) > 0f;
            if (_targetVisible) Zone?.ReportContact(_combatTarget, _combatTarget.transform.position, true);
            float lSinceSeen = Time.time - lContact.lastSeenTime;

            if (_wantsAlarm)
            {
                AlarmTrigger lAlarm = WorldRegistry.Nearest<AlarmTrigger>(transform.position, 45f, a => a.State == 0);
                if (lAlarm == null) _wantsAlarm = false;
                else
                {
                    MoveTo(lAlarm.transform.position, RUN);
                    if (Vector3.Distance(transform.position, lAlarm.transform.position) < 2f) { lAlarm.FireServer(lKnown); _wantsAlarm = false; }
                    return;
                }
            }

            // Lost them for a while: the squad sweeps around the last known position.
            if (lSinceSeen > 7f)
            {
                _searchCenter = lKnown;
                _searchUntil = Time.time + GameDatabase.Rules.searchDuration;
                SetState(NpcState.Search);
                return;
            }

            LevelZone.CombatRole lRole = Zone != null ? Zone.RoleOf(this) : LevelZone.CombatRole.Push;
            _repositionTime -= dt;
            bool lNeedsMove = _repositionTime <= 0f || _cover == null || !_cover.CoversFrom(lKnown);

            if (lNeedsMove)
            {
                _repositionTime = Random.Range(3.5f, 7f);
                CoverPoint lNext = PickCover(lKnown, lRole);
                if (_cover != null && _cover.Occupant == this) _cover.Occupant = null;
                _cover = lNext;
                if (_cover != null) _cover.Occupant = this;
            }

            if (_cover != null)
            {
                MoveTo(_cover.transform.position, RUN);
            }
            else if (lRole == LevelZone.CombatRole.Push || lSinceSeen > 2.5f)
            {
                // No cover: close in on the known position, but keep a distance.
                Vector3 lApproach = lKnown + (transform.position - lKnown).normalized * PreferredRange() * 0.6f;
                MoveTo(lApproach, RUN);
            }
            else Stop();

            Vector3 lAimPoint = _targetVisible ? _combatTarget.Chest : lKnown + Vector3.up * 1.1f;
            FaceTowards(lAimPoint, 540f);
            Vector3 lAimDir = lAimPoint - Eye;
            AimPitch.Value = -Mathf.Asin(Mathf.Clamp(lAimDir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

            // Visible: shoot. Not visible: suppressors keep firing at the last known position.
            if (_targetVisible || (lRole == LevelZone.CombatRole.Suppress && lSinceSeen < 4f)) TryShoot(dt);
        }

        private float PreferredRange()
        {
            ItemDefinition lWeapon = Weapon;
            if (lWeapon == null) return 8f;
            return lWeapon.kind switch
            {
                ItemKind.Shotgun => 7f,
                ItemKind.Sniper => 40f,
                ItemKind.Pistol => 12f,
                _ => 16f
            };
        }

        private CoverPoint PickCover(Vector3 pThreat, LevelZone.CombatRole pRole)
        {
            CoverPoint lBest = null;
            float lBestScore = float.MinValue;
            float lRange = PreferredRange();
            Vector3 lSquadDir = Zone != null ? Zone.SquadAngleTo(pThreat, this) : Vector3.zero;

            foreach (CoverPoint lCover in CoverPoint.All)
            {
                if (lCover.Occupant != null && lCover.Occupant != this && lCover.Occupant.Health.IsAlive) continue;
                Vector3 lPos = lCover.transform.position;
                float lFromMe = Vector3.Distance(lPos, transform.position);
                if (lFromMe > 28f) continue;
                float lToThreat = Vector3.Distance(lPos, pThreat);
                if (lToThreat < 4f) continue;

                float lScore = 0f;
                lScore -= Mathf.Abs(lToThreat - (pRole == LevelZone.CombatRole.Push ? lRange * 0.5f : lRange)) * 0.8f;
                lScore -= lFromMe * 0.35f;
                if (lCover.CoversFrom(pThreat)) lScore += 12f; else lScore -= 20f;
                if (lCover.CanPeek(pThreat)) lScore += 8f;
                if (pRole == LevelZone.CombatRole.Flank && lSquadDir != Vector3.zero)
                {
                    float lAngle = Vector3.Angle(lSquadDir, (lPos - pThreat).normalized);
                    lScore += Mathf.Clamp(lAngle - 40f, -20f, 60f) * 0.3f;
                }
                // Don't run across the threat's line of fire.
                if (Vector3.Dot((lPos - transform.position).normalized, (pThreat - transform.position).normalized) > 0.85f && lFromMe > 6f) lScore -= 10f;
                if (lScore > lBestScore) { lBestScore = lScore; lBest = lCover; }
            }
            return lBestScore > -15f ? lBest : null;
        }

        private void TryShoot(float dt)
        {
            ItemDefinition lWeapon = Weapon;
            if (lWeapon == null || Time.time < _reactionUntil || Time.time < _nextShot) return;
            if (_agent.velocity.magnitude > 3.5f && lWeapon.kind == ItemKind.Sniper) return;

            if (_burstLeft <= 0)
            {
                _burstLeft = lWeapon.automatic ? Random.Range(3, 7) : Random.Range(1, 3);
                _nextShot = Time.time + Random.Range(0.5f, 1.1f) * (lWeapon.kind == ItemKind.Sniper ? 2.5f : 1f);
                return;
            }
            _burstLeft--;
            _nextShot = Time.time + 1f / Mathf.Max(0.5f, lWeapon.fireRate * 0.8f);

            Vector3 lMuzzle = Eye + transform.forward * 0.5f - Vector3.up * 0.25f;
            Vector3 lAim = _targetVisible && _combatTarget != null ? _combatTarget.Chest : (Zone?.ContactFor(_combatTarget)?.lastKnown ?? transform.position + transform.forward * 10f) + Vector3.up * 1.1f;
            float lDistance = Vector3.Distance(lMuzzle, lAim);

            // Hit chance: accuracy falls with range, target crouched or moving; suppression rarely hits.
            float lChance = GameDatabase.Rules.npcAccuracy * Mathf.Clamp(10f / Mathf.Max(3f, lDistance), 0.15f, 1.6f);
            if (_combatTarget != null)
            {
                if (_combatTarget.Crouched.Value) lChance *= 0.75f;
                if (_combatTarget.Motor != null && _combatTarget.Sprinting.Value) lChance *= 0.6f;
            }
            if (!_targetVisible) lChance *= 0.25f;
            if (lWeapon.kind == ItemKind.Sniper) lChance *= 1.8f;

            Vector3 lDir = (lAim - lMuzzle).normalized;
            bool lHitIntended = Random.value < lChance;
            if (!lHitIntended) lDir = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-5f, 5f), 0) * lDir;

            int lPellets = Mathf.Max(1, lWeapon.pellets);
            Vector3 lEnd = lMuzzle + lDir * lWeapon.range;
            for (int i = 0; i < lPellets; i++)
            {
                Vector3 lPelletDir = lPellets > 1 ? Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0) * lDir : lDir;
                lEnd = Ballistics.TraceServer(lMuzzle, lPelletDir, lWeapon, lWeapon.damage * GameDatabase.Rules.npcDamageMultiplier / lPellets * (lPellets > 1 ? 1.4f : 1f) * 0.75f, DamageInfo.NO_PLAYER, Match.MatchRulesLogic.NO_TEAM, transform);
            }
            ShotRpc(lMuzzle, lEnd, WeaponItem.Value);
        }

        private void UpdateSearch()
        {
            if (Time.time > _searchUntil)
            {
                _combatTarget = null;
                ReturnToDuty();
                return;
            }
            if (Arrived(1.5f) || _stateTime < 0.1f)
            {
                float lRadius = Mathf.Lerp(3f, 14f, 1f - (_searchUntil - Time.time) / GameDatabase.Rules.searchDuration);
                Vector2 lRand = Random.insideUnitCircle * lRadius;
                MoveTo(_searchCenter + new Vector3(lRand.x, 0, lRand.y), RUN * 0.6f);
            }
        }

        public void StartCall()
        {
            if (!Health.IsAlive) { Zone?.CallFinished(this, false); return; }
            _callTimer = GameDatabase.Rules.callDuration;
            SetState(NpcState.Calling);
        }

        private void UpdateCalling(float dt)
        {
            Stop();
            _callTimer -= dt;
            if (_combatTarget != null) FaceTowards(_combatTarget.transform.position);
            if (_callTimer > 0f) return;
            State.Value = NpcState.Combat;
            _stateTime = 0f;
            Zone?.CallFinished(this, true);
            CallDoneRpc();
        }

        // ---------------------------------------------------------------- damage

        private void HandleDamagedServer(DamageInfo pInfo)
        {
            _lastAttacker = pInfo.attacker;
            PlayerAvatar lAttacker = PlayerAvatar.ForClient(pInfo.attacker);
            if (IsTarget) { SetState(NpcState.Flee); return; }
            if (lAttacker != null)
            {
                _awareness = 1f;
                Zone?.ReportContact(lAttacker, lAttacker.transform.position, Vector3.Distance(lAttacker.transform.position, transform.position) < 25f);
                if (!InCombat) { _combatTarget = lAttacker; _reactionUntil = Time.time + 0.4f; EnterCombat(); }
            }
            else if (!InCombat) AlertToPoint(pInfo.point - pInfo.direction * 6f, true);
            _repositionTime = Mathf.Min(_repositionTime, 0.4f);
        }

        private void HandleDeathServer(DamageInfo pInfo)
        {
            if (State.Value == NpcState.Calling) Zone?.CallFinished(this, false);
            State.Value = pInfo.knockout ? NpcState.KnockedOut : NpcState.Dead;
            if (_cover != null && _cover.Occupant == this) _cover.Occupant = null;
            _agent.enabled = false;
            NoiseSystem.Emit(transform.position, pInfo.type == DamageType.Bullet && pInfo.headshot ? 5f : 9f, NoiseKind.Suspicious, pInfo.attacker);

            // Guards drop their gun: clearing a room arms you.
            ItemDefinition lWeapon = Weapon;
            if (lWeapon != null && !IsTarget) ItemPickup.SpawnServer(lWeapon, transform.position + Vector3.up * 1.1f, Random.Range(lWeapon.magazine / 3, lWeapon.magazine + 1), lWeapon.reserveAmmo / 3, Random.insideUnitSphere + Vector3.up * 2f);
            Match.MatchDirector.Instance?.HandleNpcDeath(this, pInfo);
        }

        /// <summary>Host: a takedown or a brick to the head - out cold, for good.</summary>
        public void KnockOut(Vector3 pDirection, ulong pBy)
        {
            if (!Health.IsAlive) return;
            Health.Kill(new DamageInfo { type = DamageType.Melee, point = transform.position + Vector3.up * 1.5f, direction = pDirection, attacker = pBy, attackerTeam = Match.MatchRulesLogic.NO_TEAM, knockout = true });
        }

        // ---------------------------------------------------------------- client feedback

        private void HandleHitClient(float pAmount, Vector3 pPoint, Vector3 pDirection, bool pHeadshot)
        {
            HitFlash.Play(_body);
            _animator?.Shove(pDirection * 0.6f);
            Effects.Fluff(pPoint, -pDirection, _body != null ? _body.BodyColor : Color.white, pHeadshot);
        }

        private void HandleDeathClient(Vector3 pImpulse, Vector3 pPoint, DamageType pType)
        {
            GoobRagdoll lRagdoll = GoobRagdoll.Spawn(_body, pImpulse, pPoint);
            if (lRagdoll != null && IsTarget) Match.MatchDirector.RegisterTargetRagdoll(lRagdoll);
            _visuals?.HideAll();
            foreach (Collider lCollider in GetComponentsInChildren<Collider>()) lCollider.enabled = false;
            Audio.Sfx.Play(pType == DamageType.Melee ? "thud" : "death", transform.position, 0.9f, 25f);
        }

        [Rpc(SendTo.Everyone)]
        private void ShotRpc(Vector3 pFrom, Vector3 pTo, int pItem)
        {
            ItemDefinition lItem = GameDatabase.Item(pItem);
            Effects.Muzzle(pFrom, (pTo - pFrom).normalized, lItem != null && lItem.kind == ItemKind.Shotgun ? 1.5f : 1f);
            Effects.Tracer(pFrom, pTo, new Color(1f, 0.55f, 0.4f, 0.9f));
            Effects.Impact(pTo, (pFrom - pTo).normalized, new Color(0.75f, 0.7f, 0.6f));
            if (lItem != null) Audio.Sfx.Play(WeaponHolder.SoundFor(lItem), pFrom, 0.9f, lItem.noiseRadius * 2f);
            // Bullets whizzing past the local player.
            if (PlayerAvatar.Local != null)
            {
                Vector3 lMe = PlayerAvatar.Local.Eye;
                Vector3 lClosest = pFrom + Vector3.Project(lMe - pFrom, pTo - pFrom);
                if (Vector3.Distance(lClosest, lMe) < 2f) { Audio.Sfx.Play2D("whizz", 0.5f); Juice.Aberration(0.25f); Juice.Shake(0.08f); }
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SpottedRpc()
        {
            _visuals?.PopAlert();
            Audio.Sfx.Play("spotted", transform.position + Vector3.up * 2f, 1f, 30f, 0f);
        }

        [Rpc(SendTo.Everyone)]
        private void CallDoneRpc()
        {
            Audio.Sfx.Play("radio", transform.position, 1f, 25f);
            if (Zone != null) UI.Hud.Toast("REINFORCEMENTS CALLED IN " + Zone.ZoneName.ToUpperInvariant());
        }

        [Rpc(SendTo.Everyone)]
        private void PukeRpc()
        {
            if (_body != null && _body.Head != null) Effects.Puke(_body.Head.position + transform.forward * 0.2f, transform.forward + Vector3.down);
            Audio.Sfx.Play("puke", transform.position, 0.8f, 15f);
        }
    }
}
