using System;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Match;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    /// <summary>
    /// The room's settings, owned by the host and read by everyone: mode, map, team count,
    /// proximity chat, time limit, and the phase the room is in. Spawned by the host once it starts
    /// and kept across scene loads.
    /// </summary>
    public class LobbyState : NetworkBehaviour
    {
        public static LobbyState Instance { get; private set; }
        public static event Action OnChanged;

        public readonly NetworkVariable<MatchMode> Mode = new NetworkVariable<MatchMode>(MatchMode.FreeForAll);
        public readonly NetworkVariable<int> MapIndex = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> TeamCount = new NetworkVariable<int>(2);
        public readonly NetworkVariable<bool> ProximityChat = new NetworkVariable<bool>(true);
        public readonly NetworkVariable<float> TimeLimitMinutes = new NetworkVariable<float>(20f);
        public readonly NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.Lobby);
        public readonly NetworkVariable<int> AiDifficulty = new NetworkVariable<int>(1);
        /// <summary>How many teams the room has made: none to begin with, every agent for themselves.</summary>
        public readonly NetworkVariable<int> BoxCount = new NetworkVariable<int>(0);
        /// <summary>Which boxes protect the target, one bit per box.</summary>
        public readonly NetworkVariable<int> DefenderMask = new NetworkVariable<int>(0);
        /// <summary>Time of day of the contract, set from the map when it starts (<see cref="FX.TimeOfDay"/>): 0 dawn, 1 noon, 2 dusk, 3 night.</summary>
        public readonly NetworkVariable<int> TimeOfDay = new NetworkVariable<int>(1);

        public MapDefinition Map => GameDatabase.Map(MapIndex.Value);

        public bool IsProtector(int pTeam) => MatchRulesLogic.IsProtector(DefenderMask.Value, pTeam);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                ProximityChat.Value = GameDatabase.Rules.proximityChatByDefault;
                TimeLimitMinutes.Value = GameDatabase.Rules.timeLimitMinutes;
            }
            Mode.OnValueChanged += Changed;
            MapIndex.OnValueChanged += Changed;
            TeamCount.OnValueChanged += Changed;
            ProximityChat.OnValueChanged += Changed;
            TimeLimitMinutes.OnValueChanged += Changed;
            Phase.OnValueChanged += Changed;
            AiDifficulty.OnValueChanged += Changed;
            BoxCount.OnValueChanged += Changed;
            DefenderMask.OnValueChanged += Changed;
            OnChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            Mode.OnValueChanged -= Changed;
            MapIndex.OnValueChanged -= Changed;
            TeamCount.OnValueChanged -= Changed;
            ProximityChat.OnValueChanged -= Changed;
            TimeLimitMinutes.OnValueChanged -= Changed;
            Phase.OnValueChanged -= Changed;
            AiDifficulty.OnValueChanged -= Changed;
            BoxCount.OnValueChanged -= Changed;
            DefenderMask.OnValueChanged -= Changed;
        }

        private void Changed<T>(T pOld, T pNew) => OnChanged?.Invoke();

        /// <summary>Does this team of the room protect the target?</summary>
        public bool IsProtectingBox(int pBox) => MatchRulesLogic.IsProtector(DefenderMask.Value, pBox);

        /// <summary>Host side: every agent plays for the team they are in or alone, and that makes the mode.</summary>
        public void ResolveTeams()
        {
            if (!IsServer) return;
            System.Collections.Generic.List<int> lCounts = new System.Collections.Generic.List<int>();
            for (int i = 0; i < BoxCount.Value; i++) lCounts.Add(0);
            int lNextSolo = MatchRulesLogic.MAX_BOXES;
            // A copy: each team change re-sorts LobbyPlayer.All.
            foreach (LobbyPlayer lPlayer in new System.Collections.Generic.List<LobbyPlayer>(LobbyPlayer.All))
            {
                if (lPlayer.Caster.Value) { lPlayer.Team.Value = MatchRulesLogic.NO_TEAM; continue; }
                int lBox = lPlayer.ChosenTeam.Value;
                if (lBox >= 0 && lBox < BoxCount.Value)
                {
                    lPlayer.Team.Value = lBox;
                    lCounts[lBox]++;
                }
                else
                {
                    if (lBox >= 0) lPlayer.ChosenTeam.Value = MatchRulesLogic.NO_TEAM;
                    lPlayer.Team.Value = lNextSolo++;
                    lCounts.Add(1);
                }
            }
            Mode.Value = MatchRulesLogic.ModeFromBoxes(lCounts);
            int lUsed = 0;
            foreach (int lCount in lCounts) if (lCount > 0) lUsed++;
            TeamCount.Value = lUsed;
        }

        // ---------------------------------------------------------------- the team boxes

        /// <summary>Puts a player in a team, or on their own (-1): the host moves anyone, a player only themselves.</summary>
        [Rpc(SendTo.Server)]
        public void MovePlayerRpc(ulong pClient, int pBox, RpcParams pParams = default)
        {
            ulong lSender = pParams.Receive.SenderClientId;
            if (lSender != NetworkManager.ServerClientId && lSender != pClient) return;
            LobbyPlayer lPlayer = LobbyPlayer.ForClient(pClient);
            if (lPlayer == null) return;
            lPlayer.ChosenTeam.Value = pBox < 0 ? MatchRulesLogic.NO_TEAM : Mathf.Min(pBox, BoxCount.Value - 1);
            ResolveTeams();
        }

        [Rpc(SendTo.Server)]
        public void AddBoxRpc(RpcParams pParams = default)
        {
            if (pParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            BoxCount.Value = Mathf.Min(BoxCount.Value + 1, MatchRulesLogic.MAX_BOXES);
        }

        /// <summary>Removes a team: its agents go back on their own, the teams after it close up with their agents and their modes.</summary>
        [Rpc(SendTo.Server)]
        public void RemoveBoxRpc(int pBox, RpcParams pParams = default)
        {
            if (pParams.Receive.SenderClientId != NetworkManager.ServerClientId || pBox < 0 || pBox >= BoxCount.Value) return;
            foreach (LobbyPlayer lPlayer in LobbyPlayer.Agents())
            {
                if (lPlayer.ChosenTeam.Value == pBox) lPlayer.ChosenTeam.Value = MatchRulesLogic.NO_TEAM;
                else if (lPlayer.ChosenTeam.Value > pBox) lPlayer.ChosenTeam.Value--;
            }
            int lBelow = DefenderMask.Value & ((1 << pBox) - 1);
            int lAbove = (DefenderMask.Value >> (pBox + 1)) << pBox;
            DefenderMask.Value = lBelow | lAbove;
            BoxCount.Value--;
            ResolveTeams();
        }

        [Rpc(SendTo.Server)]
        public void ToggleProtectRpc(int pBox, RpcParams pParams = default)
        {
            if (pParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            DefenderMask.Value ^= 1 << pBox;
        }

        /// <summary>Deals the agents out at random over the teams there are (two are made if there are none).</summary>
        [Rpc(SendTo.Server)]
        public void ShuffleRpc(RpcParams pParams = default)
        {
            if (pParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            if (BoxCount.Value == 0) BoxCount.Value = 2;
            System.Collections.Generic.List<LobbyPlayer> lAgents = LobbyPlayer.Agents();
            System.Collections.Generic.List<int> lBoxes = new System.Collections.Generic.List<int>();
            for (int i = 0; i < lAgents.Count; i++) lBoxes.Add(i % BoxCount.Value);
            for (int i = 0; i < lAgents.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, lAgents.Count);
                (lBoxes[i], lBoxes[j]) = (lBoxes[j], lBoxes[i]);
                lAgents[i].ChosenTeam.Value = lBoxes[i];
            }
            ResolveTeams();
        }

        /// <summary>Host side: starts the match on the chosen map.</summary>
        public void StartMatch()
        {
            if (!IsServer || Phase.Value != MatchPhase.Lobby) return;
            MapDefinition lMap = Map;
            // A caster alone may start one, to fly around the map.
            if (lMap == null || LobbyPlayer.All.Count == 0) return;

            ResolveTeams();
            TimeOfDay.Value = lMap.timeOfDay;
            ShowCurtainRpc(lMap.displayName);
            Phase.Value = MatchPhase.Loading;
            NetworkManager.SceneManager.LoadScene(lMap.sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        /// <summary>Everyone: the loading screen, up before the heavy work starts.</summary>
        [Rpc(SendTo.Everyone)]
        private void ShowCurtainRpc(string pMap) => UI.LoadingCurtain.Show(pMap);

        /// <summary>Host side: back to the lobby (title scene) after the end screen.</summary>
        public void ReturnToLobby()
        {
            if (!IsServer) return;
            Phase.Value = MatchPhase.Lobby;
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All) lPlayer.Alive.Value = false;
            NetworkManager.SceneManager.LoadScene(GameScenes.TITLE, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
