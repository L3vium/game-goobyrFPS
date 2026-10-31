using System;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Match;
using Netcode.Transports.Facepunch;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    public enum SessionKind
    {
        None,
        /// <summary>Direct IP / LAN / solo, through Unity Transport.</summary>
        Local,
        /// <summary>Steam lobby + Facepunch transport (Steam relay).</summary>
        Steam
    }

    /// <summary>
    /// Starts and stops a session, whichever transport it rides on, and runs the host side of the
    /// room: approval (8 agents in the lobby, plus casters who may also join a running match), seats,
    /// and the <see cref="LobbyState"/> object.
    /// Lives on the persistent GameRoot prefab next to the NetworkManager.
    /// <para>
    /// The Steam transport sits on its own child, switched off until a Steam option is picked:
    /// its Awake starts Steam, so a LAN game never touches Steam.
    /// </para>
    /// <para>Command line for tests: <c>-host</c>, <c>-join &lt;ip&gt;</c>, <c>-solo &lt;mapIndex&gt; &lt;mode&gt;</c>.</para>
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public const ushort PORT = 7787;
        /// <summary>Casters a room takes on top of its agents.</summary>
        public const int MAX_CASTERS = 4;
        private const byte PAYLOAD_CASTER = 1;

        public static NetSession Instance { get; private set; }
        public static event Action<string> OnStatus;

        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private UnityTransport _unityTransport;
        [SerializeField] private GameObject _steamTransportObject;
        [SerializeField] private NetworkObject _lobbyStatePrefab;

        public SessionKind Kind { get; private set; }
        public string LastStatus { get; private set; } = "";
        public NetworkManager Network => _networkManager;

        private FacepunchTransport _steamTransport;
        private int _soloMap = -1;
        private MatchMode _soloMode = MatchMode.Coop;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameDatabase.Instance.Index();
        }

        private void Start()
        {
            _networkManager.ConnectionApprovalCallback = Approve;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _networkManager.OnServerStarted += HandleServerStarted;
            ReadCommandLine();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ReadCommandLine()
        {
            string[] lArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < lArgs.Length; i++)
            {
                if (lArgs[i] == "-host") HostLocal();
                else if (lArgs[i] == "-join" && i + 1 < lArgs.Length) JoinLocal(lArgs[i + 1]);
                else if (lArgs[i] == "-solo")
                {
                    int lMap = i + 1 < lArgs.Length && int.TryParse(lArgs[i + 1], out int lParsed) ? lParsed : 0;
                    MatchMode lMode = i + 2 < lArgs.Length && Enum.TryParse(lArgs[i + 2], true, out MatchMode lParsedMode) ? lParsedMode : MatchMode.Coop;
                    StartSolo(lMap, lMode);
                }
            }
        }

        private void Status(string pStatus)
        {
            LastStatus = pStatus;
            OnStatus?.Invoke(pStatus);
            Debug.Log("[Net] " + pStatus);
        }

        // ---------------------------------------------------------------- start / stop

        public void HostLocal()
        {
            PlayerIdentity.JoinAsCaster = false;
            UseUnityTransport();
            _unityTransport.SetConnectionData("0.0.0.0", PORT, "0.0.0.0");
            Kind = SessionKind.Local;
            if (_networkManager.StartHost()) Status("HOSTING ON PORT " + PORT);
            else Status("COULD NOT HOST");
        }

        public void JoinLocal(string pAddress)
        {
            UseUnityTransport();
            _unityTransport.SetConnectionData(string.IsNullOrWhiteSpace(pAddress) ? "127.0.0.1" : pAddress.Trim(), PORT);
            Kind = SessionKind.Local;
            SetJoinPayload();
            if (_networkManager.StartClient()) Status("CONNECTING TO " + pAddress + "...");
            else Status("COULD NOT CONNECT");
        }

        /// <summary>A private local host that loads a map right away (solo against the AI, and tests).</summary>
        public void StartSolo(int pMapIndex, MatchMode pMode)
        {
            _soloMap = pMapIndex;
            _soloMode = pMode;
            PlayerIdentity.JoinAsCaster = false;
            UseUnityTransport();
            _unityTransport.SetConnectionData("127.0.0.1", PORT, "127.0.0.1");
            Kind = SessionKind.Local;
            if (!_networkManager.StartHost()) Status("COULD NOT START SOLO");
        }

        public void HostSteam(bool pFriendsOnly)
        {
            PlayerIdentity.JoinAsCaster = false;
            UseSteamTransport();
            Kind = SessionKind.Steam;
            if (_networkManager.StartHost())
            {
                SteamLobby.Instance.Host(pFriendsOnly, GameDatabase.Rules.maxPlayers);
                Status(pFriendsOnly ? "FRIENDS ROOM" : "OPENING STEAM ROOM...");
            }
            else Status("COULD NOT HOST ON STEAM");
        }

        public void JoinSteamCode(string pCode)
        {
            UseSteamTransport();
            SteamLobby.Instance.OnJoinLobby -= JoinSteamOwner;
            SteamLobby.Instance.OnJoinLobby += JoinSteamOwner;
            SteamLobby.Instance.JoinByCode(pCode);
        }

        private void JoinSteamOwner(ulong pOwner)
        {
            if (_networkManager.IsListening) return;
            UseSteamTransport();
            _steamTransport.targetSteamId = pOwner;
            Kind = SessionKind.Steam;
            SetJoinPayload();
            if (_networkManager.StartClient()) Status("CONNECTING THROUGH STEAM...");
            else Status("COULD NOT CONNECT THROUGH STEAM");
        }

        /// <summary>Wakes Steam up (so friends' invites work) without starting a session.</summary>
        public void EnsureSteam()
        {
            if (!_steamTransportObject.activeSelf) _steamTransportObject.SetActive(true);
            _steamTransport = _steamTransportObject.GetComponent<FacepunchTransport>();
            if (SteamLobby.Instance != null)
            {
                SteamLobby.Instance.OnJoinLobby -= JoinSteamOwner;
                SteamLobby.Instance.OnJoinLobby += JoinSteamOwner;
            }
        }

        public void Leave()
        {
            if (SteamLobby.Instance != null) SteamLobby.Instance.Leave();
            if (_networkManager.IsListening) _networkManager.Shutdown();
            Kind = SessionKind.None;
            _soloMap = -1;
            Status("");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameScenes.TITLE)
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.TITLE);
        }

        /// <summary>The connection request says whether we come to play or to cast.</summary>
        private void SetJoinPayload()
        {
            _networkManager.NetworkConfig.ConnectionData = new[] { PlayerIdentity.JoinAsCaster ? PAYLOAD_CASTER : (byte)0 };
        }

        private void UseUnityTransport()
        {
            _networkManager.NetworkConfig.NetworkTransport = _unityTransport;
        }

        private void UseSteamTransport()
        {
            EnsureSteam();
            _networkManager.NetworkConfig.NetworkTransport = _steamTransport;
        }

        // ---------------------------------------------------------------- host side

        private void Approve(NetworkManager.ConnectionApprovalRequest pRequest, NetworkManager.ConnectionApprovalResponse pResponse)
        {
            bool lHostItself = pRequest.ClientNetworkId == NetworkManager.ServerClientId;
            bool lCaster = pRequest.Payload != null && pRequest.Payload.Length > 0 && pRequest.Payload[0] == PAYLOAD_CASTER;
            int lCasters = LobbyPlayer.CasterCount();
            bool lFull = lCaster ? lCasters >= MAX_CASTERS : _networkManager.ConnectedClientsIds.Count - lCasters >= GameDatabase.Rules.maxPlayers;
            // Agents join in the lobby only; casters may come in at any time to watch.
            bool lInMatch = !lCaster && LobbyState.Instance != null && LobbyState.Instance.Phase.Value != MatchPhase.Lobby;

            pResponse.Approved = lHostItself || (!lFull && !lInMatch);
            pResponse.CreatePlayerObject = pResponse.Approved;
            pResponse.Reason = lFull ? (lCaster ? "NO CASTER SEAT LEFT" : "ROOM FULL") : lInMatch ? "MATCH IN PROGRESS - JOIN AS A CASTER TO WATCH" : "";
        }

        private void HandleServerStarted()
        {
            NetworkObject lState = Instantiate(_lobbyStatePrefab);
            lState.Spawn(false);
            _networkManager.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
            _networkManager.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
        }

        /// <summary>Host: a map is loaded on every machine - the match session comes to life.</summary>
        private void HandleSceneLoaded(string pScene, UnityEngine.SceneManagement.LoadSceneMode pMode, System.Collections.Generic.List<ulong> pCompleted, System.Collections.Generic.List<ulong> pTimedOut)
        {
            if (pScene == GameScenes.TITLE) return;
            NetworkObject lSession = Instantiate(NetPrefabs.Instance.mapSession);
            lSession.Spawn(true);
        }

        private void HandleClientConnected(ulong pClientId)
        {
            if (_networkManager.IsServer)
            {
                LobbyPlayer lPlayer = LobbyPlayer.ForClient(pClientId);
                if (lPlayer != null) lPlayer.Seat.Value = FreeSeat(lPlayer);

                if (_soloMap >= 0 && pClientId == NetworkManager.ServerClientId && LobbyState.Instance != null)
                {
                    LobbyState.Instance.MapIndex.Value = _soloMap;
                    LobbyState.Instance.Mode.Value = _soloMode;
                    LobbyState.Instance.StartMatch();
                }
            }

            if (pClientId == _networkManager.LocalClientId) Status(_networkManager.IsHost ? LastStatus : "CONNECTED");
        }

        private void HandleClientDisconnected(ulong pClientId)
        {
            if (!_networkManager.IsServer && pClientId == _networkManager.LocalClientId)
            {
                string lReason = _networkManager.DisconnectReason;
                Leave();
                Status(string.IsNullOrEmpty(lReason) ? "DISCONNECTED" : lReason);
            }
        }

        private static int FreeSeat(LobbyPlayer pFor)
        {
            for (int lSeat = 0; lSeat < 16; lSeat++)
            {
                bool lTaken = false;
                foreach (LobbyPlayer lOther in LobbyPlayer.All)
                    if (lOther != pFor && lOther.Seat.Value == lSeat) { lTaken = true; break; }
                if (!lTaken) return lSeat;
            }
            return 0;
        }
    }
}
