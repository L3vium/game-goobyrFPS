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

        public MapDefinition Map => GameDatabase.Map(MapIndex.Value);

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
        }

        private void Changed<T>(T pOld, T pNew) => OnChanged?.Invoke();

        /// <summary>Host side: resolves every player's team from the mode and their choice.</summary>
        public void ResolveTeams()
        {
            if (!IsServer) return;
            // A copy: each team change re-sorts LobbyPlayer.All.
            foreach (LobbyPlayer lPlayer in new System.Collections.Generic.List<LobbyPlayer>(LobbyPlayer.All))
                lPlayer.Team.Value = lPlayer.Caster.Value ? MatchRulesLogic.NO_TEAM : MatchRulesLogic.ResolveTeam(Mode.Value, lPlayer.Seat.Value, lPlayer.ChosenTeam.Value, TeamCount.Value);
        }

        /// <summary>Host side: starts the match on the chosen map.</summary>
        public void StartMatch()
        {
            if (!IsServer || Phase.Value != MatchPhase.Lobby) return;
            MapDefinition lMap = Map;
            if (lMap == null || LobbyPlayer.Agents().Count == 0) return;

            ResolveTeams();
            Phase.Value = MatchPhase.Loading;
            NetworkManager.SceneManager.LoadScene(lMap.sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

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
