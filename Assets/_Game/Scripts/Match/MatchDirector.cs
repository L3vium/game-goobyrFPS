using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Match
{
    public enum ProofState : byte
    {
        /// <summary>The target is alive.</summary>
        TargetAlive,
        /// <summary>The proof lies on the ground.</summary>
        Dropped,
        Carried,
        Extracted
    }

    /// <summary>
    /// Runs a match on the host, from the start point to the end screen:
    /// <list type="bullet">
    /// <item>one start point is drawn and every player spawns around it with the starting loadout;</item>
    /// <item>one target is drawn among the map's fifteen, spawned on its routine with its bodyguards;</item>
    /// <item>the NPCs, the weapons (many guns, one rocket launcher, one flamethrower, both hidden) and the gadgets spawn;</item>
    /// <item>the target dies: the proof drops; whoever carries it is revealed to all every few seconds
    /// and must hold an active extraction point for a moment to win - for their team;</item>
    /// <item>players who die are out and spectate. The match ends on extraction, wipe-out or time.</item>
    /// </list>
    /// Clients read the state from the network variables (HUD, map screen, end screen).
    /// </summary>
    public class MatchDirector : NetworkBehaviour
    {
        public static MatchDirector Instance { get; private set; }

        public readonly NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.Loading);
        public readonly NetworkVariable<float> StartTime = new NetworkVariable<float>(0f);
        public readonly NetworkVariable<float> TimeLeft = new NetworkVariable<float>(1200f);
        public readonly NetworkVariable<int> TargetProfileIndex = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<NetworkObjectReference> TargetNpc = new NetworkVariable<NetworkObjectReference>();
        public readonly NetworkVariable<ProofState> Proof = new NetworkVariable<ProofState>(ProofState.TargetAlive);
        public readonly NetworkVariable<ulong> ProofCarrier = new NetworkVariable<ulong>(ulong.MaxValue);
        public readonly NetworkVariable<Vector3> ProofPosition = new NetworkVariable<Vector3>(Vector3.zero);
        public readonly NetworkVariable<int> ActiveExtractions = new NetworkVariable<int>(0);
        public readonly NetworkVariable<MatchOutcome> Outcome = new NetworkVariable<MatchOutcome>(MatchOutcome.None);
        public readonly NetworkVariable<int> WinnerTeam = new NetworkVariable<int>(MatchRulesLogic.NO_TEAM);
        public readonly NetworkVariable<float> ExtractProgress = new NetworkVariable<float>(0f);

        public bool Playing => Phase.Value == MatchPhase.Playing;
        public float Elapsed => IsSpawned && StartTime.Value > 0f ? NetworkManager.ServerTime.TimeAsFloat - StartTime.Value : 0f;
        public MapDefinition Map => LobbyState.Instance != null ? LobbyState.Instance.Map : null;
        public TargetProfile Target => Map != null && TargetProfileIndex.Value >= 0 && TargetProfileIndex.Value < Map.targets.Count ? Map.targets[TargetProfileIndex.Value] : null;

        private ProofPickup _proof;
        private float _revealTimer;
        private float _endTimer;
        private readonly List<ExtractionPoint> _extractions = new List<ExtractionPoint>();
        private static GoobRagdoll _targetRagdoll;
        private string _killMethod = "";

        public override void OnNetworkSpawn()
        {
            Instance = this;
            AlertDirector.Reset();
            SmokeVolume.Clear();
            _extractions.Clear();
            foreach (MatchPoint lPoint in MatchPoint.OfKind(MatchPoint.Kind.Extraction))
            {
                ExtractionPoint lExtraction = lPoint.GetComponent<ExtractionPoint>();
                if (lExtraction != null) _extractions.Add(lExtraction);
            }
            GameInput.GameplayEnabled = true;
            UI.Hud.Ensure();
            UI.PauseMenu.Ensure();
            UI.MapScreen.Ensure();
            if (IsServer) StartMatchServer();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            GoobRagdoll.ClearAll();
            LkpGhost.Clear();
            Spectator.Stop();
        }

        public static void RegisterTargetRagdoll(GoobRagdoll pRagdoll) => _targetRagdoll = pRagdoll;

        // ---------------------------------------------------------------- start

        private void StartMatchServer()
        {
            MapDefinition lMap = Map;
            int lSeed = System.Environment.TickCount;
            System.Random lRandom = new System.Random(lSeed);
            TimeLeft.Value = (LobbyState.Instance != null ? LobbyState.Instance.TimeLimitMinutes.Value : GameDatabase.Rules.timeLimitMinutes) * 60f;
            foreach (LevelZone lZone in LevelZone.All) lZone.ResetSquad();

            // Players around one start point.
            List<MatchPoint> lStarts = MatchPoint.OfKind(MatchPoint.Kind.Start);
            Transform lStart = lStarts.Count > 0 ? lStarts[lRandom.Next(lStarts.Count)].transform : transform;
            int lIndex = 0;
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All)
            {
                float lAngle = lIndex * Mathf.PI * 2f / Mathf.Max(1, LobbyPlayer.All.Count);
                Vector3 lPosition = lStart.position + new Vector3(Mathf.Cos(lAngle), 0, Mathf.Sin(lAngle)) * (LobbyPlayer.All.Count > 1 ? 2.2f : 0f) + Vector3.up * 0.2f;
                NetworkObject lAvatar = Instantiate(NetPrefabs.Instance.playerAvatar, lPosition, lStart.rotation);
                lAvatar.SpawnWithOwnership(lPlayer.OwnerClientId, true);
                lAvatar.GetComponent<PlayerAvatar>().TeleportOwnerRpc(lPosition, lStart.eulerAngles.y);
                lAvatar.GetComponent<Damageable>().OnDeathServer += pInfo => HandlePlayerDeath(lAvatar.GetComponent<PlayerAvatar>(), pInfo);
                lPlayer.Alive.Value = true;
                lPlayer.Kills.Value = 0;
                lPlayer.NpcKills.Value = 0;
                lIndex++;
            }

            // NPCs from the map's spawn points.
            foreach (NpcSpawnPoint lSpawn in FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None))
                SpawnNpc(lSpawn.transform.position, lSpawn.transform.rotation, lSpawn.Role, lSpawn.Zone, GameDatabase.Find(lSpawn.Weapon), lSpawn.Route, null, -1, null);

            // The target and its bodyguards.
            if (lMap != null && lMap.targets.Count > 0)
            {
                List<bool> lEnabled = new List<bool>();
                foreach (TargetProfile lProfile in lMap.targets) lEnabled.Add(lProfile.enabled);
                int lTarget = MatchRulesLogic.DrawTarget(lEnabled, lSeed);
                if (lTarget < 0) lTarget = 0;
                TargetProfileIndex.Value = lTarget;
                TargetProfile lChosen = lMap.targets[lTarget];
                ActivityType lFirst = lChosen.routine.Count > 0 ? lChosen.routine[0] : ActivityType.Idle;
                ActivityNode lNode = ActivityNode.Find(lChosen.zone, lFirst, LevelZone.Get(lChosen.zone)?.SafeRoom ?? Vector3.zero) ?? ActivityNode.Find(lChosen.zone, ActivityType.Idle, Vector3.zero);
                Vector3 lAt = lNode != null ? lNode.transform.position : (LevelZone.Get(lChosen.zone)?.SafeRoom ?? Vector3.zero);
                Npc lTargetNpc = SpawnNpc(lAt, Quaternion.identity, NpcRole.Target, lChosen.zone, GameDatabase.Find(ItemKind.Pistol), null, null, lTarget, lChosen.routine);
                TargetNpc.Value = lTargetNpc.NetworkObject;
                for (int i = 0; i < lChosen.bodyguards; i++)
                    SpawnNpc(lAt + new Vector3(i % 2 == 0 ? 1.5f : -1.5f, 0, -1.5f - i * 0.5f), Quaternion.identity, NpcRole.Bodyguard, lChosen.zone, GameDatabase.Find(i % 2 == 0 ? ItemKind.Smg : ItemKind.Shotgun), null, lTargetNpc, -1, null);
            }

            SpawnItems(lRandom);

            // Two (or the rule's number) extractions among the map's.
            List<int> lPool = new List<int>();
            for (int i = 0; i < _extractions.Count; i++) lPool.Add(i);
            int lMask = 0;
            for (int i = 0; i < Mathf.Min(GameDatabase.Rules.activeExtractions, lPool.Count); i++)
            {
                int lPick = lRandom.Next(lPool.Count);
                lMask |= 1 << lPool[lPick];
                lPool.RemoveAt(lPick);
            }
            ActiveExtractions.Value = lMask;

            StartTime.Value = NetworkManager.ServerTime.TimeAsFloat;
            Phase.Value = MatchPhase.Playing;
            if (LobbyState.Instance != null) LobbyState.Instance.Phase.Value = MatchPhase.Playing;
            AnnounceRpc("NEW CONTRACT", Target != null ? Target.displayName.ToUpperInvariant() : "");
        }

        private void SpawnItems(System.Random pRandom)
        {
            foreach (MatchPoint lPoint in MatchPoint.OfKind(MatchPoint.Kind.WeaponSpawn))
                ItemPickup.SpawnServer(GameDatabase.Find(lPoint.Item), lPoint.transform.position + Vector3.up * 0.1f, -1, -1, Vector3.zero);
            foreach (MatchPoint lPoint in MatchPoint.OfKind(MatchPoint.Kind.GadgetSpawn))
                ItemPickup.SpawnServer(GameDatabase.Find(lPoint.Item), lPoint.transform.position + Vector3.up * 0.1f, -1, -1, Vector3.zero);

            // Unique weapons: several hidden candidate spots each, only one is used.
            foreach (ItemKind lKind in new[] { ItemKind.RocketLauncher, ItemKind.Flamethrower })
            {
                List<MatchPoint> lSpots = MatchPoint.OfKind(MatchPoint.Kind.UniqueWeaponSpawn).FindAll(p => p.Item == lKind);
                if (lSpots.Count > 0) ItemPickup.SpawnServer(GameDatabase.Find(lKind), lSpots[pRandom.Next(lSpots.Count)].transform.position + Vector3.up * 0.1f, -1, -1, Vector3.zero);
            }
        }

        public Npc SpawnNpc(Vector3 pPosition, Quaternion pRotation, NpcRole pRole, int pZone, ItemDefinition pWeapon, PatrolRoute pRoute, Npc pPrincipal, int pProfile, List<ActivityType> pRoutine)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(pPosition, out UnityEngine.AI.NavMeshHit lHit, 6f, UnityEngine.AI.NavMesh.AllAreas)) pPosition = lHit.position;
            NetworkObject lObject = Instantiate(NetPrefabs.Instance.npc, pPosition, pRotation);
            Npc lNpc = lObject.GetComponent<Npc>();
            lObject.Spawn(true);
            lNpc.InitServer(pRole, pZone, pWeapon, pRoute, pPrincipal, pProfile, pRoutine);
            return lNpc;
        }

        /// <summary>Host: a zone's radio call went through.</summary>
        public void SpawnReinforcements(LevelZone pZone, Vector3 pPoint, int pCount)
        {
            if (!Playing || pZone == null) return;
            Vector3 lEntry = pZone.ReinforcementEntry != null ? pZone.ReinforcementEntry.position : pZone.transform.position;
            for (int i = 0; i < pCount; i++)
            {
                Npc lNpc = SpawnNpc(lEntry + new Vector3(i * 1.2f, 0, (i % 2) * 1.2f), Quaternion.LookRotation(pPoint - lEntry), NpcRole.Reinforcement, pZone.Index, GameDatabase.Find(i % 3 == 2 ? ItemKind.Shotgun : ItemKind.Smg), null, null, -1, null);
                lNpc.AlertToPoint(pPoint, true);
            }
            if (MapEffects.Instance != null) MapEffects.Instance.SoundRpc("truck", lEntry, 120f);
        }

        public void SpawnVehicleCrew(LevelZone pZone, Vector3 pA, Vector3 pB, PlayerAvatar pSeen)
        {
            if (!Playing) return;
            int lZone = pZone != null ? pZone.Index : 0;
            foreach (Vector3 lAt in new[] { pA, pB })
            {
                Npc lNpc = SpawnNpc(lAt, Quaternion.identity, NpcRole.Reinforcement, lZone, GameDatabase.Find(ItemKind.Smg), null, null, -1, null);
                if (pSeen != null) { pZone?.ReportContact(pSeen, pSeen.transform.position, true); lNpc.AlertToPoint(pSeen.transform.position, true); }
            }
        }

        // ---------------------------------------------------------------- deaths

        public void HandleNpcDeath(Npc pNpc, DamageInfo pInfo)
        {
            LobbyPlayer lKiller = LobbyPlayer.ForClient(pInfo.attacker);
            if (lKiller != null && pInfo.attacker != DamageInfo.NO_PLAYER) lKiller.NpcKills.Value++;
            if (!pNpc.IsTarget) return;

            _killMethod = MethodName(pInfo);
            NetworkObject lProof = Instantiate(NetPrefabs.Instance.proof, pNpc.transform.position + Vector3.up * 0.6f, Quaternion.identity);
            lProof.Spawn(true);
            _proof = lProof.GetComponent<ProofPickup>();
            _proof.Kind.Value = Target != null ? Target.proof : ProofKind.Head;
            _proof.Profile.Value = TargetProfileIndex.Value;
            Proof.Value = ProofState.Dropped;
            ProofPosition.Value = lProof.transform.position;
            string lBy = lKiller != null ? lKiller.Name : "THE WORLD";
            AnnounceRpc("TARGET ELIMINATED", (Target != null ? Target.displayName : "Target").ToUpperInvariant() + " - " + _killMethod + " - BY " + lBy.ToUpperInvariant());
            TargetDownRpc(_proof.Kind.Value == ProofKind.Head);
        }

        private static string MethodName(DamageInfo pInfo) => pInfo.type switch
        {
            DamageType.Bullet => pInfo.headshot ? "HEADSHOT" : "SHOT",
            DamageType.Explosion => "BLOWN UP",
            DamageType.Fire => "BURNED",
            DamageType.Poison => "POISONED",
            DamageType.Gas => "GASSED",
            DamageType.Crush => "CRUSHED",
            DamageType.Melee => pInfo.knockout ? "KNOCKED OUT" : "BEATEN",
            DamageType.Fall => "FELL",
            DamageType.Drown => "DROWNED",
            _ => "ELIMINATED"
        };

        private void HandlePlayerDeath(PlayerAvatar pAvatar, DamageInfo pInfo)
        {
            if (pAvatar == null) return;
            LobbyPlayer lVictim = pAvatar.Player;
            LobbyPlayer lKiller = LobbyPlayer.ForClient(pInfo.attacker);
            if (lVictim != null) lVictim.Alive.Value = false;
            if (lKiller != null && lKiller != lVictim) lKiller.Kills.Value++;

            if (pAvatar.CarriesProof.Value) DropProofServer(pAvatar.transform.position + Vector3.up);
            KillFeedRpc((lKiller != null && lKiller != lVictim ? lKiller.Name : pInfo.attacker == DamageInfo.NO_PLAYER ? "NPC" : "?").ToUpperInvariant(),
                (lVictim != null ? lVictim.Name : "?").ToUpperInvariant(), MethodName(pInfo));

            StartCoroutine(DespawnLater(pAvatar.NetworkObject, 1.5f));
        }

        private System.Collections.IEnumerator DespawnLater(NetworkObject pObject, float pDelay)
        {
            yield return new WaitForSeconds(pDelay);
            if (pObject != null && pObject.IsSpawned) pObject.Despawn(true);
        }

        // ---------------------------------------------------------------- proof

        [Rpc(SendTo.Server)]
        public void TakeProofRpc(RpcParams pParams = default)
        {
            ulong lSender = pParams.Receive.SenderClientId;
            PlayerAvatar lAvatar = PlayerAvatar.ForClient(lSender);
            if (_proof == null || Proof.Value != ProofState.Dropped || lAvatar == null || !lAvatar.Health.IsAlive) return;
            if (Vector3.Distance(lAvatar.transform.position, _proof.transform.position) > 3.5f) return;

            Proof.Value = ProofState.Carried;
            ProofCarrier.Value = lSender;
            _proof.Carrier.Value = lSender;
            lAvatar.CarriesProof.Value = true;
            _revealTimer = 0f;
            AnnounceRpc("PROOF TAKEN", ((lAvatar.Player != null ? lAvatar.Player.Name : "?") + " HAS IT - REACH AN EXTRACTION").ToUpperInvariant());
        }

        private void DropProofServer(Vector3 pAt)
        {
            if (_proof == null) return;
            PlayerAvatar lCarrier = PlayerAvatar.ForClient(ProofCarrier.Value);
            if (lCarrier != null) lCarrier.CarriesProof.Value = false;
            ProofCarrier.Value = ulong.MaxValue;
            _proof.Carrier.Value = ulong.MaxValue;
            if (Physics.Raycast(pAt, Vector3.down, out RaycastHit lGround, 20f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) pAt = lGround.point + Vector3.up * 0.3f;
            _proof.transform.position = pAt;
            ProofPosition.Value = pAt;
            Proof.Value = ProofState.Dropped;
            AnnounceRpc("PROOF DROPPED", "GRAB IT!");
        }

        // ---------------------------------------------------------------- loop

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            if (Phase.Value == MatchPhase.Ended)
            {
                _endTimer -= Time.deltaTime;
                if (_endTimer <= 0f && LobbyState.Instance != null)
                {
                    _endTimer = 999f;
                    LobbyState.Instance.ReturnToLobby();
                }
                return;
            }
            if (Phase.Value != MatchPhase.Playing) return;

            TimeLeft.Value = Mathf.Max(0f, TimeLeft.Value - Time.deltaTime);

            if (Proof.Value == ProofState.Carried && _proof != null)
            {
                PlayerAvatar lCarrier = PlayerAvatar.ForClient(ProofCarrier.Value);
                if (lCarrier == null || !lCarrier.Health.IsAlive) DropProofServer(_proof.transform.position);
                else
                {
                    ProofPosition.Value = lCarrier.transform.position;
                    _revealTimer -= Time.deltaTime;
                    if (_revealTimer <= 0f)
                    {
                        _revealTimer = GameDatabase.Rules.proofRevealInterval;
                        RevealCarrierRpc(lCarrier.transform.position);
                    }
                    UpdateExtraction(lCarrier);
                }
            }
            else if (ExtractProgress.Value > 0f) ExtractProgress.Value = 0f;

            List<RulesPlayer> lPlayers = new List<RulesPlayer>();
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All) lPlayers.Add(new RulesPlayer { clientId = lPlayer.OwnerClientId, team = lPlayer.Team.Value, alive = lPlayer.Alive.Value });
            MatchOutcome lOutcome = MatchRulesLogic.Evaluate(lPlayers, WinnerTeam.Value, TimeLeft.Value);
            if (lOutcome != MatchOutcome.None) EndMatchServer(lOutcome);
        }

        private void UpdateExtraction(PlayerAvatar pCarrier)
        {
            bool lInside = false;
            for (int i = 0; i < _extractions.Count; i++)
                if ((ActiveExtractions.Value & (1 << i)) != 0 && _extractions[i].Contains(pCarrier.transform.position)) lInside = true;

            ExtractProgress.Value = lInside ? ExtractProgress.Value + Time.deltaTime / Mathf.Max(0.1f, GameDatabase.Rules.extractionTime) : Mathf.Max(0f, ExtractProgress.Value - Time.deltaTime);
            if (ExtractProgress.Value >= 1f)
            {
                Proof.Value = ProofState.Extracted;
                WinnerTeam.Value = pCarrier.Team.Value;
            }
        }

        private void EndMatchServer(MatchOutcome pOutcome)
        {
            Outcome.Value = pOutcome;
            Phase.Value = MatchPhase.Ended;
            if (LobbyState.Instance != null) LobbyState.Instance.Phase.Value = MatchPhase.Ended;
            _endTimer = 14f;
            string lWinner = "";
            if (pOutcome == MatchOutcome.Extracted)
            {
                List<string> lNames = new List<string>();
                foreach (LobbyPlayer lPlayer in LobbyPlayer.All) if (lPlayer.Team.Value == WinnerTeam.Value) lNames.Add(lPlayer.Name);
                lWinner = string.Join(", ", lNames);
            }
            EndRpc(pOutcome, WinnerTeam.Value, lWinner.ToUpperInvariant(), _killMethod);
        }

        // ---------------------------------------------------------------- client feedback

        [Rpc(SendTo.Everyone)]
        private void AnnounceRpc(string pTitle, string pSubtitle) => UI.Hud.Announce(pTitle, pSubtitle);

        [Rpc(SendTo.Everyone)]
        private void KillFeedRpc(string pKiller, string pVictim, string pMethod) => UI.Hud.KillFeed(pKiller + "  >  " + pVictim + "  (" + pMethod + ")");

        [Rpc(SendTo.Everyone)]
        private void RevealCarrierRpc(Vector3 pPosition)
        {
            UI.Hud.PingCarrier(pPosition);
            Audio.Sfx.Play2D("ping", 0.5f);
        }

        [Rpc(SendTo.Everyone)]
        private void TargetDownRpc(bool pHead)
        {
            if (pHead && _targetRagdoll != null) _targetRagdoll.HideHead();
            Juice.Flash(new Color(1f, 0.85f, 0.4f), 0.3f);
            Audio.Sfx.Play2D("target_down", 0.9f);
        }

        [Rpc(SendTo.Everyone)]
        private void EndRpc(MatchOutcome pOutcome, int pWinnerTeam, string pWinners, string pMethod)
        {
            bool lWon = pOutcome == MatchOutcome.Extracted && LobbyPlayer.Local != null && LobbyPlayer.Local.Team.Value == pWinnerTeam;
            UI.EndScreen.Show(pOutcome, lWon, pWinners, pMethod);
            if (lWon && CameraRig.Instance != null) Effects.Confetti(CameraRig.Instance.WorldCamera.transform.position + CameraRig.Instance.WorldCamera.transform.forward * 3f);
            GameInput.GameplayEnabled = false;
            GameInput.LockCursor(false);
        }
    }
}
