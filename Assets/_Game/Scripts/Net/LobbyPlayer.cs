using System;
using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Match;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    /// <summary>
    /// One connected player, from the lobby to the end screen: NGO's player object. It survives
    /// scene loads (spawned with <c>destroyWithScene: false</c>) and carries what the menus and the
    /// match need - name, seat, chosen team, ready flag, alive flag, score, and whether they play
    /// (an agent) or only watch (a caster, see <c>CasterView</c>).
    /// The avatar walking on the map is a different object (<c>PlayerAvatar</c>), spawned per match.
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        public static readonly List<LobbyPlayer> All = new List<LobbyPlayer>();
        public static LobbyPlayer Local { get; private set; }
        public static event Action OnListChanged;

        public readonly NetworkVariable<FixedString64Bytes> DisplayName = new NetworkVariable<FixedString64Bytes>("Goob", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<ulong> SteamId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<int> ChosenTeam = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> Seat = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Team = new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> Alive = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> NpcKills = new NetworkVariable<int>(0);
        /// <summary>Watches the match from the caster view instead of playing: no avatar, no seat among the agents.</summary>
        public readonly NetworkVariable<bool> Caster = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> TalkingNow = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public string Name => DisplayName.Value.ToString();
        public bool IsAgent => !Caster.Value;

        /// <summary>The players who play, in seat order (casters left out).</summary>
        public static List<LobbyPlayer> Agents()
        {
            List<LobbyPlayer> lAgents = new List<LobbyPlayer>(All.Count);
            foreach (LobbyPlayer lPlayer in All) if (lPlayer.IsAgent) lAgents.Add(lPlayer);
            return lAgents;
        }

        public static int CasterCount()
        {
            int lCount = 0;
            foreach (LobbyPlayer lPlayer in All) if (lPlayer.Caster.Value) lCount++;
            return lCount;
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            All.Sort((pA, pB) => pA.Seat.Value.CompareTo(pB.Seat.Value));
            if (IsOwner)
            {
                Local = this;
                DisplayName.Value = PlayerIdentity.LocalName;
                SteamId.Value = PlayerIdentity.LocalSteamId;
                Caster.Value = PlayerIdentity.JoinAsCaster;
            }

            DisplayName.OnValueChanged += Changed;
            Ready.OnValueChanged += Changed;
            Caster.OnValueChanged += Changed;
            ChosenTeam.OnValueChanged += Changed;
            Team.OnValueChanged += Changed;
            Seat.OnValueChanged += Changed;
            OnListChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            DisplayName.OnValueChanged -= Changed;
            Ready.OnValueChanged -= Changed;
            Caster.OnValueChanged -= Changed;
            ChosenTeam.OnValueChanged -= Changed;
            Team.OnValueChanged -= Changed;
            Seat.OnValueChanged -= Changed;
            OnListChanged?.Invoke();
        }

        private void Changed<T>(T pOld, T pNew)
        {
            All.Sort((pA, pB) => pA.Seat.Value.CompareTo(pB.Seat.Value));
            OnListChanged?.Invoke();
        }

        /// <summary>The player asks to join a team (Teams mode). -1 = automatic.</summary>
        [Rpc(SendTo.Server)]
        public void ChooseTeamRpc(int pTeam)
        {
            ChosenTeam.Value = Mathf.Clamp(pTeam, -1, MatchRulesLogic.MAX_TEAMS - 1);
        }

        public static LobbyPlayer ForClient(ulong pClientId)
        {
            foreach (LobbyPlayer lPlayer in All) if (lPlayer.OwnerClientId == pClientId) return lPlayer;
            return null;
        }

        public static Color TeamColor(int pTeam)
        {
            Color[] lColors =
            {
                new Color(0.93f, 0.33f, 0.36f), new Color(0.33f, 0.55f, 0.95f), new Color(0.98f, 0.78f, 0.25f), new Color(0.42f, 0.82f, 0.45f),
                new Color(0.75f, 0.45f, 0.92f), new Color(0.98f, 0.55f, 0.25f), new Color(0.35f, 0.85f, 0.85f), new Color(0.95f, 0.55f, 0.78f)
            };
            return lColors[Mathf.Abs(pTeam) % lColors.Length];
        }
    }
}
