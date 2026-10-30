using System;
using UnityEngine;
using Steamworks;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    /// <summary>
    /// Steam lobbies on top of the Facepunch transport (same design as Hangtime's): the lobby is
    /// only the rendezvous, it says who hosts. A guest joins the lobby, then dials the owner's Steam
    /// id through Steam's relay network - nobody forwards a port.
    /// <para>
    /// HOST opens a public lobby carrying a six-letter code, JOIN finds it with a lobby search.
    /// FRIENDS opens a friends-only lobby and Steam's invite overlay. App ID 480 is shared by every
    /// Steamworks test, so every lobby carries "game=goobyrfps" and searches ask for it.
    /// </para>
    /// <para>
    /// Steam itself is started by the transport's Awake when the Steam child is switched on, so
    /// nothing here throws when Steam is not running: entry points check <see cref="IsReady"/>.
    /// </para>
    /// </summary>
    public class SteamLobby : MonoBehaviour
    {
        private const string LAUNCH_ARG = "+connect_lobby";
        private const string KEY_GAME = "game";
        private const string GAME = "goobyrfps";
        private const string KEY_CODE = "code";
        private const string KEY_NAME = "name";

        public static SteamLobby Instance { get; private set; }

        /// <summary>Raised with the host's Steam id once this machine joined a lobby and should dial it.</summary>
        public event Action<ulong> OnJoinLobby;
        public event Action<string> OnJoinFailed;

        public string Status { get; private set; } = "";
        public string Code { get; private set; } = "";
        public ulong LaunchLobby { get; private set; }

        public bool IsReady => SteamClient.IsValid;
        public string PersonaName => IsReady ? SteamClient.Name : "";
        public ulong SteamId => IsReady ? SteamClient.SteamId.Value : 0;

        private Steamworks.Data.Lobby? _lobby;
        private bool _subscribed;
        private bool _hostWhenReady;
        private bool _hostFriendsOnly;
        private int _maxMembers = 8;
        private string _joinCodeWhenReady;

        private void Awake()
        {
            Instance = this;
            LaunchLobby = ReadLaunchLobby();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_subscribed && IsReady) SteamFriends.OnGameLobbyJoinRequested -= HandleJoinRequested;
        }

        private void Update()
        {
            if (!IsReady) return;

            if (!_subscribed)
            {
                _subscribed = true;
                SteamFriends.OnGameLobbyJoinRequested += HandleJoinRequested;
            }

            if (_hostWhenReady) { _hostWhenReady = false; Host(_hostFriendsOnly, _maxMembers); }
            if (_joinCodeWhenReady != null) { string lCode = _joinCodeWhenReady; _joinCodeWhenReady = null; JoinByCode(lCode); }

            if (LaunchLobby != 0)
            {
                ulong lLobby = LaunchLobby;
                LaunchLobby = 0;
                Join(lLobby);
            }
        }

        /// <summary>Opens the lobby with this machine as owner. Waits for Steam if it is still starting.</summary>
        public async void Host(bool pFriendsOnly, int pMaxMembers)
        {
            _hostFriendsOnly = pFriendsOnly;
            _maxMembers = pMaxMembers;

            if (!IsReady)
            {
                _hostWhenReady = true;
                Status = "STARTING STEAM...";
                return;
            }

            Leave(false);
            Code = pFriendsOnly ? "" : RoomCode.New();
            Status = "OPENING THE ROOM...";

            Steamworks.Data.Lobby? lLobby = await SteamMatchmaking.CreateLobbyAsync(pMaxMembers);
            if (!lLobby.HasValue)
            {
                Status = "COULD NOT OPEN A STEAM ROOM";
                return;
            }

            Steamworks.Data.Lobby lOpened = lLobby.Value;
            if (pFriendsOnly) lOpened.SetFriendsOnly();
            else lOpened.SetPublic();
            lOpened.SetJoinable(true);
            lOpened.SetData(KEY_GAME, GAME);
            lOpened.SetData(KEY_NAME, SteamClient.Name);
            if (!pFriendsOnly) lOpened.SetData(KEY_CODE, RoomCode.Clean(Code));
            _lobby = lOpened;
            Status = pFriendsOnly ? "FRIENDS ROOM OPEN - INVITE WITH SHIFT+TAB" : "ROOM CODE " + Code;
            Debug.Log($"[Net] steam room open ({(pFriendsOnly ? "friends" : Code)}), lobby {lOpened.Id}");
        }

        /// <summary>Finds a public lobby by its code (worldwide) and joins it.</summary>
        public async void JoinByCode(string pCode)
        {
            string lCode = RoomCode.Clean(pCode);

            if (!IsReady)
            {
                _joinCodeWhenReady = lCode;
                Status = "STARTING STEAM...";
                return;
            }

            Leave(false);
            Status = "LOOKING FOR ROOM " + RoomCode.Format(lCode) + "...";

            Steamworks.Data.Lobby[] lFound = await SteamMatchmaking.LobbyList
                .WithKeyValue(KEY_GAME, GAME)
                .WithKeyValue(KEY_CODE, lCode)
                .FilterDistanceWorldwide()
                .WithMaxResults(4)
                .RequestAsync();

            if (lFound == null || lFound.Length == 0)
            {
                Status = "NO ROOM " + RoomCode.Format(lCode);
                OnJoinFailed?.Invoke(Status);
                return;
            }

            Join(lFound[0].Id);
        }

        /// <summary>Steam's invite overlay for the open lobby.</summary>
        public void Invite()
        {
            if (!IsReady || !_lobby.HasValue) return;
            SteamFriends.OpenGameInviteOverlay(_lobby.Value.Id);
        }

        public void Leave() => Leave(true);

        private void Leave(bool pForget)
        {
            _hostWhenReady = false;
            _joinCodeWhenReady = null;
            if (pForget) { Code = ""; Status = ""; }
            if (!_lobby.HasValue) return;
            _lobby.Value.Leave();
            _lobby = null;
        }

        private void HandleJoinRequested(Steamworks.Data.Lobby pLobby, SteamId pFriend) => Join(pLobby.Id);

        private async void Join(ulong pLobbyId)
        {
            Leave(false);
            Status = "JOINING THE ROOM...";

            Steamworks.Data.Lobby? lLobby = await SteamMatchmaking.JoinLobbyAsync(pLobbyId);
            if (!lLobby.HasValue)
            {
                Status = "COULD NOT JOIN THAT ROOM";
                OnJoinFailed?.Invoke(Status);
                return;
            }

            _lobby = lLobby.Value;
            ulong lOwner = lLobby.Value.Owner.Id;
            if (lOwner == SteamClient.SteamId) { Status = ""; return; }

            Status = "CONNECTING TO THE HOST...";
            OnJoinLobby?.Invoke(lOwner);
        }

        private static ulong ReadLaunchLobby()
        {
            string[] lArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < lArgs.Length - 1; i++)
                if (lArgs[i] == LAUNCH_ARG && ulong.TryParse(lArgs[i + 1], out ulong lId)) return lId;
            return 0;
        }
    }
}
