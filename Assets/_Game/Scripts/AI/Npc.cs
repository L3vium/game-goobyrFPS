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
        /// <summary>Gauge filling: stops and stares at what it half-saw.</summary>
        Suspicious,
        /// <summary>Gauge full: confronts the player (weapon up, walking over) before shooting.</summary>
        Engaged,
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
    /// smoke; exposure fills an awareness gauge instead of detecting instantly, then the NPC
    /// confronts the player before it shoots (<see cref="DetectionModel"/>). Hearing comes from the
    /// <see cref="NoiseSystem"/> and from players running close by. A fight stays local: others
    /// join only by seeing it, hearing it or getting the phone call.
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
        /// <summary>The client filling this NPC's gauge or being confronted by it (HUD arcs and status); <see cref="NO_SUSPECT"/> otherwise.</summary>
        public readonly NetworkVariable<ulong> Suspect = new NetworkVariable<ulong>(NO_SUSPECT);
        public const ulong NO_SUSPECT = ulong.MaxValue;

        /// <summary>NPCs confronting or fighting someone: the ones others can witness.</summary>
        private static readonly List<Npc> _hot = new List<Npc>();

        public Damageable Health { get; private set; }
        public NpcRole Role => RoleVar.Value;
        /// <summary>Fighting (shooting, or phoning for help mid-fight). A search is not a fight: a searcher who sees you or is hit opens fire.</summary>
        public bool InCombat => State.Value == NpcState.Combat || State.Value == NpcState.Calling;
        /// <summary>Confronting, fighting or searching: what the minimap, instinct and HUD show as a threat.</summary>
        public bool IsAlerted => InCombat || State.Value == NpcState.Engaged || State.Value == NpcState.Search;
        /// <summary>Weapon up and expecting trouble (searching, or checking out a shot): sees faster and fires on sight.</summary>
        private bool OnEdge => State.Value == NpcState.Search || (State.Value == NpcState.Investigate && _investigateAlarmed);
        /// <summary>Going about its business: the states a sound or a glimpse can interrupt.</summary>
        private bool OnDuty => State.Value == NpcState.Idle || State.Value == NpcState.Patrol || State.Value == NpcState.Routine || State.Value == NpcState.Escort;
        public bool IsTarget => Role == NpcRole.Target;
        /// <summary>Unarmed crowd members (guests, staff, spectators): they run away instead of fighting.</summary>
        public bool IsCrowd => Role == NpcRole.Worker;
        private bool Flees => IsTarget || IsCrowd;
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
        private Vector3? _fleePoint;
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
        private float _combatStart;
        private float _engagedSince, _lastSawTarget;
        private bool _combatTargetExposed;
        private Vector3 _glancePoint;
        private float _glanceUntil;
        private float _nextBreakAt;
        private ActivityNode _break;
        private float _breakUntil;
        private float _nextStepHeard;

        /// <summary>Bodies some NPC already reported: the next to stumble on them is wary, not a new alert.</summary>
        private static readonly HashSet<ulong> _reportedBodies = new HashSet<ulong>();
        private static bool _hearing;
        private static readonly RaycastHit[] _wallHits = new RaycastHit[8];
        private static readonly List<(Npc npc, float loudness, float distance)> _hearers = new List<(Npc, float, float)>();

        private const float WALK = 1.9f, RUN = 4.9f;
        /// <summary>Unhurried paces: a patrol on its round, a target going about its routine.</summary>
        private const float PATROL = 1.15f, STROLL = 1.3f;

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
            _route?.Join(this);
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
            gameObject.AddComponent<Audio.Footsteps>().Init(() => !Health.IsAlive ? 0f : State.Value == NpcState.Combat || State.Value == NpcState.Flee || State.Value == NpcState.Search ? 0.9f : 0.45f);
            if (IsServer)
            {
                Health.OnDamagedServer += HandleDamagedServer;
                Health.OnDeathServer += HandleDeathServer;
                if (!_hearing) { NoiseSystem.OnNoise += HearAll; _hearing = true; _reportedBodies.Clear(); }
                BreakerAlert.OnBreaker += HandleBreaker;
                _nextBreakAt = Time.time + Random.Range(20f, 90f);
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
            _hot.Remove(this);
            if (IsServer)
            {
                if (All.Count == 0 && _hearing) { NoiseSystem.OnNoise -= HearAll; _hearing = false; }
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
                Color lColor = lProfile != null ? lProfile.bodyColor : new Color(0.9f, 0.8f, 0.2f);
                Outfit lOutfit = lProfile == null ? Outfit.Suit : lProfile.zone == 2 ? (TargetProfile.Value % 2 == 0 ? Outfit.Fancy : Outfit.Suit) : lProfile.zone == 1 ? (TargetProfile.Value % 2 == 0 ? Outfit.Biker : Outfit.Military) : (TargetProfile.Value % 3 == 0 ? Outfit.Worker : Outfit.Suit);
                GoobLook lLook = GoobLook.Random(lRandom, lOutfit, lColor);
                lLook.skin = lSkin;
                lLook.hat = lProfile != null ? lProfile.hat : HatStyle.Crown;
                lLook.hatColor = lColor * 0.45f;
                lLook.hair = lLook.hat == HatStyle.None ? lLook.hair : Hair.None;
                lLook.scale = lProfile != null ? lProfile.scale : 1.15f;
                _body.Configure(lLook);
            }
            else
            {
                Color lFaction = Zone != null ? Zone.Faction : new Color(0.3f, 0.3f, 0.35f);
                Outfit lOutfit = Role == NpcRole.Bodyguard ? Outfit.Suit : Role == NpcRole.Lookout ? Outfit.Military : Zone != null ? Zone.FactionOutfit : Outfit.Guard;
                GoobLook lLook = GoobLook.Random(lRandom, lOutfit, GameArt.Vary(Role == NpcRole.Bodyguard ? new Color(0.16f, 0.16f, 0.2f) : lFaction, 0.12f, lRandom));
                lLook.skin = lSkin;
                lLook.hat = Role == NpcRole.Bodyguard ? HatStyle.None : Zone != null ? Zone.FactionHat : HatStyle.Cap;
                lLook.hatColor = lFaction * 0.55f;
                lLook.helmet = Role == NpcRole.Lookout;
                lLook.sunglasses = Role == NpcRole.Bodyguard;
                lLook.earpiece = Role == NpcRole.Bodyguard || Role == NpcRole.Reinforcement;
                lLook.hands = Role == NpcRole.Bodyguard || lOutfit == Outfit.Military ? HandStyle.Glove : HandStyle.Mitten;
                if (lLook.hat != HatStyle.None || lLook.helmet) lLook.hair = Hair.None;
                lLook.scale = Role == NpcRole.Bodyguard ? 1.12f : lLook.scale;
                _body.Configure(lLook);
            }
            _visuals?.RefreshWeapon();
        }

        // ---------------------------------------------------------------- host loop

        private void Update()
        {
            if (_animator != null)
            {
                _animator.AimPitch = AimPitch.Value;
                _animator.HoldingWeapon = Weapon != null && (IsAlerted || State.Value == NpcState.Investigate || Role == NpcRole.Lookout);
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

            // A sound made it turn and look: it stands there a moment before carrying on.
            if (Time.time < _glanceUntil && OnDuty)
            {
                Stop();
                FaceTowards(_glancePoint, 240f);
            }
            else switch (State.Value)
            {
                case NpcState.Idle: UpdateIdle(); break;
                case NpcState.Patrol: UpdatePatrol(); break;
                case NpcState.Routine: UpdateRoutine(); break;
                case NpcState.Escort: UpdateEscort(); break;
                case NpcState.Investigate: UpdateInvestigate(); break;
                case NpcState.Suspicious: UpdateSuspicious(); break;
                case NpcState.Engaged: UpdateEngaged(); break;
                case NpcState.Combat: UpdateCombat(dt); break;
                case NpcState.Search: UpdateSearch(); break;
                case NpcState.Calling: UpdateCalling(dt); break;
                case NpcState.Flee: UpdateFlee(); break;
                case NpcState.Sick: UpdateSick(); break;
            }

            byte lAware = (byte)Mathf.RoundToInt(Mathf.Clamp01(_awareness) * 20f);
            if (Awareness.Value != lAware) Awareness.Value = lAware;
            bool lConfronting = State.Value == NpcState.Engaged || State.Value == NpcState.Combat || State.Value == NpcState.Calling;
            PlayerAvatar lWho = lConfronting ? _combatTarget : _awareness > 0.02f ? _suspect : null;
            ulong lSuspect = lWho != null ? lWho.OwnerClientId : NO_SUSPECT;
            if (Suspect.Value != lSuspect) Suspect.Value = lSuspect;
        }

        private void SetState(NpcState pState)
        {
            if (State.Value == pState) return;
            if (State.Value == NpcState.Calling && pState != NpcState.Calling) Zone?.CallFinished(this, false);
            State.Value = pState;
            _stateTime = 0f;
            bool lHot = pState == NpcState.Engaged || pState == NpcState.Combat || pState == NpcState.Calling;
            if (lHot && !_hot.Contains(this)) _hot.Add(this);
            else if (!lHot) _hot.Remove(this);
            if (pState != NpcState.Combat && _cover != null) { if (_cover.Occupant == this) _cover.Occupant = null; _cover = null; }
            if (pState != NpcState.Idle) _break = null;
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
            _combatTargetExposed = false;

            foreach (PlayerAvatar lPlayer in PlayerAvatar.All)
            {
                if (lPlayer == null || !lPlayer.IsSpawned || !lPlayer.Health.IsAlive) continue;
                float lExposure = Exposure(lPlayer, lRules, lCombat || OnEdge);
                if (lExposure > lBestExposure) { lBestExposure = lExposure; lBest = lPlayer; }
                if (lPlayer == _combatTarget && lExposure > 0f) _combatTargetExposed = true;
                if (lExposure <= 0f && !lPlayer.Hidden.Value) ListenToSteps(lPlayer);
            }

            _awareness = DetectionModel.Fill(_awareness, lBestExposure, lRules.npcDetectTime, dt, lCombat, OnEdge);
            if (lBest != null)
            {
                _suspect = lBest;
                if (lBest == _combatTarget) _lastSawTarget = Time.time;
                if (State.Value == NpcState.Engaged) { }
                else if (_awareness >= 1f) Spot(lBest);
                else if (CanBecomeSuspicious)
                {
                    if (_awareness > DetectionModel.INVESTIGATE && State.Value != NpcState.Investigate) Investigate(lBest.transform.position, false);
                    else if (State.Value != NpcState.Investigate && State.Value != NpcState.Suspicious) SetState(NpcState.Suspicious);
                }
            }
            else if (_combatTarget != null) _targetVisible = false;

            if (!lCombat && State.Value != NpcState.Engaged)
            {
                LookForBodies();
                WatchForFights();
            }
        }

        /// <summary>
        /// Footsteps out of sight: their reach follows the player's real speed and stance (silent
        /// standing still, a whisper crouched) and walls muffle them. Close and clear, the NPC comes to
        /// look around where it thinks they were; faint, it only turns its head.
        /// </summary>
        private void ListenToSteps(PlayerAvatar pPlayer)
        {
            float lRadius = DetectionModel.FootstepRadius(pPlayer.HostSpeed, pPlayer.Crouched.Value, pPlayer.Sprinting.Value);
            if (lRadius <= 0f) return;
            Vector3 lFeet = pPlayer.transform.position + Vector3.up * 0.3f;
            float lDistance = Vector3.Distance(lFeet, Eye);
            if (lDistance >= lRadius || Time.time < _nextStepHeard) return;
            float lLoudness = DetectionModel.Loudness(lDistance, lRadius, WallsBetween(lFeet, Eye));
            if (lLoudness <= 0f) return;
            _nextStepHeard = Time.time + 0.8f;
            Vector3 lGuess = Guess(pPlayer.transform.position, lDistance);

            if (InCombat) { if (lLoudness > 0.5f) Zone?.ReportContact(pPlayer, lGuess, false); return; }
            if (State.Value == NpcState.Search) { if (lLoudness > 0.4f) { _searchCenter = lGuess; MoveTo(lGuess, RUN * 0.6f); } return; }
            if (!CanBecomeSuspicious) return;
            _suspect = pPlayer;
            if (DetectionModel.React(NoiseKind.Footstep, lLoudness, true) == DetectionModel.Heard.Investigate && State.Value != NpcState.Investigate) Investigate(lGuess, false);
            else Glance(pPlayer.transform.position, _awareness + 0.12f);
        }

        /// <summary>Turn and look toward a sound for a moment, a little more wary (never past the point of walking over).</summary>
        private void Glance(Vector3 pPoint, float pWary)
        {
            _awareness = Mathf.Max(_awareness, Mathf.Min(pWary, DetectionModel.INVESTIGATE - 0.05f));
            if (!OnDuty) return;
            _glancePoint = pPoint;
            _glanceUntil = Time.time + Random.Range(1.8f, 3f);
        }

        /// <summary>Where a sound seems to come from: a direction, not a position.</summary>
        private static Vector3 Guess(Vector3 pSource, float pDistance)
        {
            Vector2 lOff = Random.insideUnitCircle * DetectionModel.GuessError(pDistance);
            return pSource + new Vector3(lOff.x, 0f, lOff.y);
        }

        /// <summary>Walls and floors between two points (each one muffles a sound), up to <see cref="DetectionModel.MAX_WALLS"/>.</summary>
        private static int WallsBetween(Vector3 pFrom, Vector3 pTo)
        {
            Vector3 lDir = pTo - pFrom;
            float lLength = lDir.magnitude;
            if (lLength < 0.05f) return 0;
            int lHits = Physics.RaycastNonAlloc(pFrom, lDir / lLength, _wallHits, lLength, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
            return Mathf.Min(lHits, DetectionModel.MAX_WALLS);
        }

        /// <summary>States from which a glimpse of a player makes the NPC stop and stare.</summary>
        private bool CanBecomeSuspicious => !InCombat && State.Value != NpcState.Engaged && State.Value != NpcState.Flee && State.Value != NpcState.Sick;

        /// <summary>See <see cref="DetectionModel.Exposure"/>: distance, cone and edge of vision, light, stance, grass, smoke, walls.</summary>
        private float Exposure(PlayerAvatar pPlayer, GameRules pRules, bool pCombat)
        {
            if (pPlayer.Hidden.Value) return 0f;
            Vector3 lEye = Eye;
            Vector3 lTarget = pPlayer.Chest;
            Vector3 lTo = lTarget - lEye;

            DetectionModel.Sight lSight = new DetectionModel.Sight
            {
                distance = lTo.magnitude,
                range = pRules.npcVisionRange * (Role == NpcRole.Lookout ? 1.5f : 1f) * BuildingVolume.LightFactor(lTarget) * (pCombat ? 1.5f : 1f) * (pPlayer.CarriesProof.Value ? 1.2f : 1f) * (Zone != null ? Zone.VisionFactor : 1f),
                angle = Vector3.Angle(transform.forward, lTo),
                halfCone = pRules.npcVisionAngle * 0.5f * (pCombat ? 1.4f : 1f),
                crouched = pPlayer.Crouched.Value,
                inGrass = pPlayer.InGrass.Value,
                sprinting = pPlayer.Sprinting.Value
            };
            // Cheap rejections before the line casts. Under water, only at arm's length.
            if (lSight.distance > lSight.range) return 0f;
            if (pPlayer.Submerged.Value && lSight.distance > 3f) return 0f;
            if (lSight.angle > Mathf.Max(lSight.halfCone, DetectionModel.PERIPHERAL_ANGLE) && lSight.distance >= DetectionModel.CLOSE_RANGE) return 0f;
            if (SmokeVolume.Blocks(lEye, lTarget)) return 0f;
            // The social ladder: a behaving player on the plaza is just another passer-by.
            SecurityTier lZoneTier = SecurityZone.At(pPlayer.transform.position, out AccessLevel lRequired);
            SecurityTier lFor = SecurityZone.ForPlayer(lZoneTier, lRequired, pPlayer.Weapons != null ? pPlayer.Weapons.Access : AccessLevel.None, IsEnforcer);
            float lTier = pCombat ? 1f : DetectionModel.TierFactor(lFor, pPlayer.ActsSuspicious);
            if (lTier <= 0f) return 0f;
            lSight.chestVisible = !Physics.Linecast(lEye, lTarget, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
            lSight.headVisible = !Physics.Linecast(lEye, pPlayer.Eye, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
            return DetectionModel.Exposure(lSight) * lTier;
        }

        /// <summary>
        /// A confrontation or a fight close by and in view pulls this NPC in: it walks over to a
        /// confrontation ready to spot, and joins a fight outright.
        /// </summary>
        private void WatchForFights()
        {
            if (Flees || Weapon == null || State.Value == NpcState.Flee || State.Value == NpcState.Sick) return;
            foreach (Npc lOther in _hot)
            {
                if (lOther == this || lOther == null || !lOther.Health.IsAlive || lOther._combatTarget == null) continue;
                Vector3 lTo = lOther.Eye - Eye;
                float lDistance = lTo.magnitude;
                if (lDistance > DetectionModel.WITNESS_RANGE) continue;
                bool lSight = !Physics.Linecast(Eye, lOther.Eye, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
                if (!DetectionModel.Witnesses(lDistance, Vector3.Angle(transform.forward, lTo), lSight)) continue;

                PlayerAvatar lPlayer = lOther._combatTarget;
                if (lOther.State.Value == NpcState.Engaged)
                {
                    if (State.Value == NpcState.Investigate && _investigateAlarmed) return;
                    _awareness = Mathf.Max(_awareness, 0.75f);
                    _suspect = lPlayer;
                    Investigate(lPlayer.transform.position, true);
                }
                else
                {
                    _combatTarget = lPlayer;
                    _awareness = 1f;
                    Zone?.ReportContact(lPlayer, lOther.Zone?.ContactFor(lPlayer)?.lastKnown ?? lPlayer.transform.position, false);
                    _reactionUntil = Time.time + GameDatabase.Rules.npcReactionTime;
                    EnterCombat();
                }
                return;
            }
        }

        /// <summary>The gauge is full: confront the player (or run, for the unarmed). Already fighting: keep the squad informed.</summary>
        private void Spot(PlayerAvatar pPlayer)
        {
            _combatTarget = pPlayer;
            _targetVisible = true;
            _lastSawTarget = Time.time;
            if (InCombat)
            {
                Zone?.ReportContact(pPlayer, pPlayer.transform.position, true);
                return;
            }
            SpottedRpc();
            if (!Flees && OnEdge)
            {
                // Already expecting trouble: no confrontation, it opens fire.
                Zone?.ReportContact(pPlayer, pPlayer.transform.position, true);
                _reactionUntil = Time.time + 0.35f;
                EnterCombat();
                return;
            }
            if (Flees)
            {
                // A scream: the guards close by come and look.
                NoiseSystem.Emit(transform.position, 14f, NoiseKind.Suspicious, pPlayer.OwnerClientId);
                SetState(NpcState.Flee);
                return;
            }
            _engagedSince = Time.time;
            SetState(NpcState.Engaged);
        }

        /// <summary>The confrontation: weapon up, walk over, shout - then a fight, or a search if the player slipped away.</summary>
        private void UpdateEngaged()
        {
            if (_combatTarget == null || !_combatTarget.IsSpawned || !_combatTarget.Health.IsAlive) { ReturnToDuty(); return; }
            Vector3 lPlayer = _combatTarget.transform.position;
            Vector3 lAimPoint = _combatTarget.Chest;
            FaceTowards(lAimPoint, 300f);
            Vector3 lAimDir = lAimPoint - Eye;
            AimPitch.Value = -Mathf.Asin(Mathf.Clamp(lAimDir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float lUnseen = Time.time - _lastSawTarget;
            if (Vector3.Distance(transform.position, lPlayer) > 6f && lUnseen < 0.5f) MoveTo(lPlayer, WALK);
            else Stop();

            // Aiming a gun at it from close enough is a hostile act.
            bool lHostile = _combatTarget.Aiming.Value && lUnseen < 0.4f && Vector3.Distance(transform.position, lPlayer) < 25f;
            // In a restricted area it is an escort: "get out" - leaving ends it, staying 4 s turns it into a fight.
            SecurityTier lWhere = SecurityZone.ForPlayer(SecurityZone.At(lPlayer, out AccessLevel lNeed), lNeed, _combatTarget.Weapons != null ? _combatTarget.Weapons.Access : AccessLevel.None, IsEnforcer);
            if (lWhere == SecurityTier.Public && !lHostile && !_combatTarget.ActsSuspicious)
            {
                _awareness = 0.4f;
                ReturnToDuty();
                return;
            }
            float lWindow = lWhere == SecurityTier.Restricted ? DetectionModel.ESCORT_TIME : DetectionModel.CONFRONT_TIME;
            switch (DetectionModel.ResolveConfront(Time.time - _engagedSince, lUnseen, lHostile, lWindow))
            {
                case DetectionModel.Confrontation.Combat:
                    Zone?.ReportContact(_combatTarget, lPlayer, lUnseen < 0.4f);
                    _reactionUntil = Time.time + 0.3f;
                    EnterCombat();
                    // A rare few go for the zone's flare launcher (a map-wide alarm).
                    _wantsAlarm = Random.value < 0.1f && WorldRegistry.Nearest<AlarmTrigger>(transform.position, 45f, a => a.State == 0) != null;
                    break;
                case DetectionModel.Confrontation.LostSight:
                    _searchCenter = lPlayer;
                    _searchUntil = Time.time + GameDatabase.Rules.searchDuration * 0.6f;
                    SetState(NpcState.Search);
                    MoveTo(lPlayer, RUN * 0.6f);
                    break;
            }
        }

        /// <summary>Stops and stares at what it half-saw while the gauge fills; back to duty once it drains.</summary>
        private void UpdateSuspicious()
        {
            Stop();
            if (_suspect != null) FaceTowards(_suspect.transform.position, 150f);
            if (_awareness <= 0.01f || _stateTime > 10f) ReturnToDuty();
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
                if (!_reportedBodies.Add(lOther.NetworkObjectId))
                {
                    // Someone already radioed this one in: wary, not a new alert.
                    Glance(lOther.transform.position, 0.4f);
                    return;
                }
                // First to find it: runs over and radios it in. The two nearest mates come to secure
                // the area and the zone stays on edge for a while (sharper eyes).
                Investigate(lOther.transform.position, true);
                BodyFoundRpc();
                if (Zone == null) return;
                Zone.RaiseAlertness(90f);
                int lSent = 0;
                foreach (Npc lMate in Zone.NearestMembers(lOther.transform.position, 30f))
                {
                    if (lMate == this || !lMate.OnDuty || lMate.Flees) continue;
                    lMate._seenBodies.Add(lOther.NetworkObjectId);
                    lMate.AlertToPoint(lOther.transform.position, true);
                    if (++lSent >= 2) break;
                }
                return;
            }
        }

        /// <summary>
        /// Host: hand a sound out to the NPCs who can hear it. Loudness falls with distance and with
        /// every wall or floor in between (<see cref="DetectionModel.Loudness"/>); only the few who hear
        /// it best go and look (<see cref="DetectionModel.Responders"/>), the others turn their heads.
        /// Nobody learns a position from a sound: they head for a guess around it.
        /// </summary>
        private static void HearAll(NoiseEvent pNoise)
        {
            _hearers.Clear();
            Vector3 lFrom = pNoise.position + Vector3.up * 0.4f;
            foreach (Npc lNpc in All)
            {
                if (lNpc == null || !lNpc.IsServer || !lNpc.Health.IsAlive || lNpc.State.Value == NpcState.KnockedOut) continue;
                float lDistance = Vector3.Distance(lFrom, lNpc.Eye);
                if (lDistance >= pNoise.radius) continue;
                float lLoudness = DetectionModel.Loudness(lDistance, pNoise.radius, WallsBetween(lFrom, lNpc.Eye));
                if (lLoudness > 0f) _hearers.Add((lNpc, lLoudness, lDistance));
            }
            if (_hearers.Count == 0) return;
            _hearers.Sort((a, b) => b.loudness.CompareTo(a.loudness));

            int lResponders = DetectionModel.Responders(pNoise.kind, _hearers[0].loudness);
            int lGiven = 0;
            foreach ((Npc lNpc, float lLoudness, float lDistance) in _hearers)
            {
                bool lResponder = lGiven < lResponders && lNpc.CanRespond;
                if (lResponder) lGiven++;
                lNpc.Hear(pNoise, DetectionModel.React(pNoise.kind, lLoudness, lResponder), lDistance);
            }
        }

        /// <summary>Free to go and check a sound: not fighting, fleeing, sick or already on its way to one.</summary>
        private bool CanRespond => !InCombat && State.Value != NpcState.Engaged && State.Value != NpcState.Flee && State.Value != NpcState.Sick && State.Value != NpcState.Investigate;

        private void Hear(NoiseEvent pNoise, DetectionModel.Heard pHeard, float pDistance)
        {
            if (pHeard == DetectionModel.Heard.None) return;
            if (Flees)
            {
                if (pNoise.kind == NoiseKind.Alarming && pHeard == DetectionModel.Heard.Alarmed) SetState(NpcState.Flee);
                else Glance(pNoise.position, 0.3f);
                return;
            }
            if (InCombat || State.Value == NpcState.Engaged || State.Value == NpcState.Sick) return;

            Vector3 lGuess = Guess(pNoise.position, pDistance);
            switch (pHeard)
            {
                case DetectionModel.Heard.Glance:
                    Glance(pNoise.position, pNoise.kind == NoiseKind.Alarming ? 0.4f : 0.2f);
                    break;
                case DetectionModel.Heard.Investigate:
                    Investigate(lGuess, false);
                    if (pNoise.kind == NoiseKind.Distraction) _distraction = WorldRegistry.Nearest<Distraction>(pNoise.position, 2.5f, d => d.State == 1);
                    break;
                case DetectionModel.Heard.Alarmed:
                    if (pNoise.kind == NoiseKind.Alarming) AlertToPoint(lGuess, true);
                    else Investigate(lGuess, true);
                    break;
            }
        }

        private void HandleBreaker(Breaker pBreaker)
        {
            if (!Health.IsAlive || InCombat || IsTarget || pBreaker.PowerOn) return;
            if (pBreaker.Building == null || !pBreaker.Building.Contains(transform.position + Vector3.up)) return;
            // The nearest free NPC inside that building goes and flips it back.
            float lMine = Vector3.Distance(pBreaker.transform.position, transform.position);
            foreach (Npc lOther in All)
            {
                if (lOther == this || !lOther.Health.IsAlive || lOther.InCombat || lOther.IsTarget) continue;
                if (!pBreaker.Building.Contains(lOther.transform.position + Vector3.up)) continue;
                if (Vector3.Distance(pBreaker.transform.position, lOther.transform.position) < lMine) return;
            }
            _breaker = pBreaker;
            Investigate(pBreaker.transform.position + pBreaker.transform.forward, false);
        }

        /// <summary>Host: gunfire, an explosion, a radio call - go and search that area ready to fight.</summary>
        public void AlertToPoint(Vector3 pPoint, bool pAlarmed)
        {
            if (!Health.IsAlive || State.Value == NpcState.KnockedOut || State.Value == NpcState.Sick) return;
            if (Flees) { SetState(NpcState.Flee); return; }
            if (InCombat || State.Value == NpcState.Engaged) { if (_combatTarget == null) _searchCenter = pPoint; return; }
            if (State.Value == NpcState.Search)
            {
                // Already searching: a new sound well away from the search moves it (a burst of shots doesn't).
                if ((pPoint - _searchCenter).sqrMagnitude > 36f)
                {
                    _searchCenter = pPoint;
                    _searchUntil = Mathf.Max(_searchUntil, Time.time + GameDatabase.Rules.searchDuration * 0.6f);
                    MoveTo(pPoint, RUN * 0.8f);
                }
                return;
            }
            _awareness = Mathf.Max(_awareness, 0.7f);
            _searchCenter = pPoint;
            _searchUntil = Time.time + GameDatabase.Rules.searchDuration;
            SetState(NpcState.Search);
            MoveTo(pPoint + Random.insideUnitSphere * 4f, RUN);
        }

        private void Investigate(Vector3 pPoint, bool pAlarmed)
        {
            if (Flees && pAlarmed) { SetState(NpcState.Flee); return; }
            _investigatePoint = pPoint;
            _investigateAlarmed = pAlarmed;
            _awareness = Mathf.Max(_awareness, 0.3f);
            SetState(NpcState.Investigate);
            MoveTo(pPoint, pAlarmed ? RUN * 0.75f : WALK * 1.15f);
        }

        // ---------------------------------------------------------------- non-combat states

        private void UpdateIdle()
        {
            if (TakeBreak()) return;
            if (Vector3.Distance(transform.position, _home) > 1.2f)
            {
                MoveTo(_home, PATROL);
                return;
            }
            Stop();
            float lScan = Role == NpcRole.Lookout ? Mathf.Sin(Time.time * 0.25f + NetworkObjectId) * 70f : Mathf.Sin(Time.time * 0.15f + NetworkObjectId) * 30f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _homeRotation * Quaternion.Euler(0, lScan, 0), 40f * Time.deltaTime);
        }

        /// <summary>
        /// Guards and staff on a post don't stand still all match: now and then one strolls to an
        /// activity close to its post (a smoke, a phone call, a look around), stays a while and comes
        /// back - the windows a player waits for.
        /// </summary>
        private bool TakeBreak()
        {
            if (Role != NpcRole.Guard && !IsCrowd) return false;
            if (_break == null)
            {
                if (Time.time < _nextBreakAt) return false;
                _nextBreakAt = Time.time + Random.Range(45f, 120f);
                _break = ActivityNode.Near(ZoneIndex.Value, _home, 22f, BreakActivity);
                _breakUntil = 0f;
                if (_break == null) return false;
            }
            if (_breakUntil <= 0f)
            {
                MoveTo(_break.transform.position, STROLL);
                if (!Arrived(0.9f)) return true;
                _breakUntil = Time.time + _break.Duration * Random.Range(0.8f, 1.4f);
            }
            Stop();
            FaceTowards(_break.transform.position + _break.transform.forward, 120f);
            if (Time.time > _breakUntil) _break = null;
            return true;
        }

        private static bool BreakActivity(ActivityType pType) => pType == ActivityType.Smoke || pType == ActivityType.Phone || pType == ActivityType.Inspect || pType == ActivityType.Idle || pType == ActivityType.Drink;

        private void UpdatePatrol()
        {
            if (_route == null || _route.Count == 0) { SetState(NpcState.Idle); return; }
            if (Time.time < _waitUntil) { Stop(); return; }
            // Groups walk in a column of twos behind whoever leads (the first one still on the round).
            Npc lLeader = _route.Leader;
            if (lLeader != null && lLeader != this)
            {
                int lSlot = _route.SlotOf(this);
                Vector3 lOffset = new Vector3(lSlot % 2 == 1 ? -0.9f : 0.9f, 0f, -1.5f * ((lSlot + 1) / 2));
                Vector3 lSpot = lLeader.transform.position + lLeader.transform.rotation * lOffset;
                if (Vector3.Distance(transform.position, lSpot) > 0.6f) MoveTo(lSpot, PATROL * 1.25f);
                else { Stop(); transform.rotation = Quaternion.RotateTowards(transform.rotation, lLeader.transform.rotation, 120f * Time.deltaTime); }
                _routeIndex = lLeader._routeIndex;
                return;
            }
            Vector3 lPoint = _route.Point(_routeIndex);
            MoveTo(lPoint, PATROL);
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
                MoveTo(_activityNode.transform.position, STROLL);
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
            // A crowd member just runs away from the trouble.
            if (IsCrowd)
            {
                if (_fleePoint == null)
                {
                    Vector3 lFrom = _combatTarget != null ? _combatTarget.transform.position : _investigatePoint;
                    Vector3 lAway = transform.position - lFrom;
                    lAway.y = 0f;
                    Vector3 lWish = transform.position + (lAway.sqrMagnitude > 0.01f ? lAway.normalized : -transform.forward) * 22f;
                    _fleePoint = UnityEngine.AI.NavMesh.SamplePosition(lWish, out UnityEngine.AI.NavMeshHit lHit, 8f, UnityEngine.AI.NavMesh.AllAreas) ? lHit.position : _home;
                }
                MoveTo(_fleePoint.Value, RUN);
                if (Arrived(1.5f)) Stop();
                if (_stateTime > 45f && (Zone == null || !Zone.InCombat)) { _fleePoint = null; ReturnToDuty(); }
                return;
            }
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
            _combatStart = Time.time;
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
            // Visibility comes from the last perception tick, not raycasts every frame.
            _targetVisible = lContact.visibleNow && _combatTarget != null && _combatTargetExposed;
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
            if (!HasFireToken()) return;
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
            // Warm-up: the first seconds of a fight are wild - time to dive into cover.
            lChance *= Mathf.Lerp(0.15f, 1f, Mathf.Clamp01((Time.time - _combatStart) / 4f));
            if (lWeapon.kind == ItemKind.Sniper) lChance *= 1.8f;

            Vector3 lDir = (lAim - lMuzzle).normalized;
            bool lHitIntended = Random.value < lChance;
            if (!lHitIntended) lDir = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(-5f, 5f), 0) * lDir;

            int lPellets = Mathf.Max(1, lWeapon.pellets);
            Vector3 lEnd = lMuzzle + lDir * lWeapon.range;
            for (int i = 0; i < lPellets; i++)
            {
                // Each pellet rolls its own spread: deadly up close, a scatter at range.
                Vector3 lPelletDir = lPellets > 1 ? Quaternion.Euler(Random.Range(-6.5f, 6.5f), Random.Range(-6.5f, 6.5f), 0) * lDir : lDir;
                lEnd = Ballistics.TraceServer(lMuzzle, lPelletDir, lWeapon, lWeapon.damage * GameDatabase.Rules.npcDamageMultiplier / lPellets * (lPellets > 1 ? 1.4f : 1f) * 0.75f, DamageInfo.NO_PLAYER, Match.MatchRulesLogic.NO_TEAM, transform);
            }
            ShotRpc(lMuzzle, lEnd, WeaponItem.Value);
        }

        /// <summary>
        /// Fire discipline: only the three closest NPCs that see a player shoot at them; the others
        /// keep moving (flank, push) - a firefight stays readable and survivable for the few seconds
        /// a player needs to dive into cover.
        /// </summary>
        private bool HasFireToken()
        {
            if (_combatTarget == null || !_targetVisible) return true;
            float lMine = (transform.position - _combatTarget.transform.position).sqrMagnitude;
            int lCloser = 0;
            foreach (Npc lOther in All)
            {
                if (lOther == this || !lOther.Health.IsAlive || lOther._combatTarget != _combatTarget || !lOther._targetVisible) continue;
                if ((lOther.transform.position - _combatTarget.transform.position).sqrMagnitude < lMine && ++lCloser >= MAX_SHOOTERS)
                {
                    _repositionTime = Mathf.Min(_repositionTime, 0.5f);
                    return false;
                }
            }
            return true;
        }

        private const int MAX_SHOOTERS = 3;

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
            if (Flees) { SetState(NpcState.Flee); return; }
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
            if (State.Value == NpcState.Calling) { Zone?.CallFinished(this, false); CallCutRpc(); }
            State.Value = pInfo.knockout ? NpcState.KnockedOut : NpcState.Dead;
            if (_cover != null && _cover.Occupant == this) _cover.Occupant = null;
            _agent.enabled = false;
            NoiseSystem.Emit(transform.position, _silentDeath ? 2.5f : pInfo.type == DamageType.Bullet && pInfo.headshot ? 5f : 9f, NoiseKind.Suspicious, pInfo.attacker);

            // Guards drop their gun: clearing a room arms you.
            if (_carried != null) ItemPickup.SpawnServer(_carried, transform.position + Vector3.up * 0.6f, -1, -1, Random.insideUnitSphere + Vector3.up);
            ItemDefinition lWeapon = Weapon;
            if (lWeapon != null && lWeapon.IsGun && !IsTarget) ItemPickup.SpawnServer(lWeapon, transform.position + Vector3.up * 1.1f, Random.Range(lWeapon.magazine / 3, lWeapon.magazine + 1), lWeapon.reserveAmmo / 3, Random.insideUnitSphere + Vector3.up * 2f);
            Match.MatchDirector.Instance?.HandleNpcDeath(this, pInfo);
        }

        /// <summary>Bodyguards and lookouts see through passes.</summary>
        public bool IsEnforcer => _enforcer || Role == NpcRole.Bodyguard || Role == NpcRole.Lookout;
        private bool _enforcer;

        /// <summary>Host: this NPC sees through passes (a security chief, a captain).</summary>
        public void SetEnforcer(bool pEnforcer) => _enforcer = pEnforcer;

        private ItemDefinition _carried;

        /// <summary>Host: a pass, a key or a tool this NPC carries and drops when taken down.</summary>
        public void SetCarried(ItemDefinition pItem) => _carried = pItem;

        /// <summary>The awareness gauge as everyone sees it (0-1).</summary>
        public float Gauge => Awareness.Value / 20f;

        private bool _silentDeath;

        /// <summary>Host: a takedown from behind - dead at once, with barely a sound.</summary>
        public void Takedown(Vector3 pDirection, ulong pBy, int pTeam)
        {
            if (!Health.IsAlive) return;
            _silentDeath = true;
            Health.Kill(new DamageInfo { type = DamageType.Melee, point = transform.position + Vector3.up * 1.5f, direction = pDirection, attacker = pBy, attackerTeam = pTeam });
        }

        /// <summary>Host: a brick to the head - out cold, for good.</summary>
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
            Effects.Fluff(pPoint, -pDirection, Effects.Blood, pHeadshot);
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
            // A short alarmed intake of breath where it stands; the player's own stinger plays from the HUD.
            Audio.Sfx.Play("whizz", transform.position + Vector3.up * 1.6f, 0.25f, 15f, 0.2f);
        }

        [Rpc(SendTo.Everyone)]
        private void CallDoneRpc()
        {
            Audio.Sfx.Play("radio", transform.position, 1f, 25f);
            UI.Hud.Notify("REINFORCEMENTS CALLED", Zone != null ? Zone.ZoneName : "", UI.ThreatMarkers.Red);
        }

        [Rpc(SendTo.Everyone)]
        private void CallCutRpc() => UI.Hud.Notify("CALL INTERRUPTED", Zone != null ? Zone.ZoneName : "", UI.ThreatMarkers.White);

        [Rpc(SendTo.Everyone)]
        private void BodyFoundRpc() => UI.Hud.Notify("BODY FOUND", Zone != null ? Zone.ZoneName : "", UI.ThreatMarkers.Amber);

        [Rpc(SendTo.Everyone)]
        private void PukeRpc()
        {
            if (_body != null && _body.Head != null) Effects.Puke(_body.Head.position + transform.forward * 0.2f, transform.forward + Vector3.down);
            Audio.Sfx.Play("puke", transform.position, 0.8f, 15f);
        }
    }
}
