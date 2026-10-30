using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The title scene's screens: main menu (play online through Steam, LAN, solo, settings,
    /// quit), join, and the lobby where the host picks the map, the mode, the team count and the
    /// options, players pick their team and get ready. A slow camera flyover over a Goob diorama
    /// plays behind everything.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private enum Screen { Main, Join, Lobby, Settings }

        private RectTransform _root;
        private GameObject _main, _join, _lobby, _settings;
        private TextMeshProUGUI _status, _lobbyPlayers, _lobbyInfo, _code, _modeLabel, _mapLabel, _teamsLabel, _mapDescription;
        private TMP_InputField _joinCode, _joinIp, _name;
        private Button _startButton, _readyButton;
        private RectTransform _teamButtons, _hostControls;
        private Transform _title;

        private void Start()
        {
            GameInput.GameplayEnabled = false;
            GameInput.LockCursor(false);
            Hud.Remove();
            Spectator.Stop();
            GoobRagdoll.ClearAll();

            Canvas lCanvas = UIFactory.Canvas("MenuCanvas", 20);
            lCanvas.transform.SetParent(transform, false);
            _root = (RectTransform)lCanvas.transform;
            BuildMain();
            BuildJoin();
            BuildLobby();
            BuildSettings();
            _status = UIFactory.Text(_root, "Status", "", 28, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1600, 44));

            bool lInRoom = NetworkManagerListening();
            Show(lInRoom ? Screen.Lobby : Screen.Main);
            NetSession.OnStatus += HandleStatus;
            if (NetSession.Instance != null) NetSession.Instance.EnsureSteam();
        }

        private void OnDestroy() => NetSession.OnStatus -= HandleStatus;

        private void HandleStatus(string pStatus)
        {
            if (_status != null) _status.text = pStatus;
        }

        private static bool NetworkManagerListening() => NetSession.Instance != null && NetSession.Instance.Network.IsListening;

        private void Show(Screen pScreen)
        {
            _main.SetActive(pScreen == Screen.Main);
            _join.SetActive(pScreen == Screen.Join);
            _lobby.SetActive(pScreen == Screen.Lobby);
            _settings.SetActive(pScreen == Screen.Settings);
        }

        // ---------------------------------------------------------------- main

        private void BuildMain()
        {
            _main = UIFactory.Stretch(_root, "Main").gameObject;
            Transform p = _main.transform;
            Vector2 L = new Vector2(0, 0.5f);
            _title = UIFactory.Text(p, "Title", "GOOBYR", 170, UIFactory.Accent, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(560, 330), new Vector2(1000, 200)).transform;
            UIFactory.Text(p, "Subtitle", "CONTRACTS FOR UP TO 8 GOOBS", 34, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(560, 210), new Vector2(1000, 50));
            _name = UIFactory.Input(p, "YOUR NAME", L, new Vector2(330, 120), new Vector2(520, 64));
            _name.text = PlayerIdentity.LocalName;
            _name.onEndEdit.AddListener(v => PlayerIdentity.LocalName = v);

            UIFactory.Button(p, "HOST ON STEAM", L, new Vector2(330, 30), new Vector2(520, 78), UIFactory.Accent, () => { NetSession.Instance.HostSteam(false); Show(Screen.Lobby); });
            UIFactory.Button(p, "HOST FOR FRIENDS", L, new Vector2(330, -60), new Vector2(520, 78), UIFactory.Accent, () => { NetSession.Instance.HostSteam(true); Show(Screen.Lobby); });
            UIFactory.Button(p, "JOIN", L, new Vector2(330, -150), new Vector2(520, 78), UIFactory.Accent2, () => Show(Screen.Join));
            UIFactory.Button(p, "SOLO VS AI", L, new Vector2(330, -240), new Vector2(520, 78), UIFactory.Accent2, () => { NetSession.Instance.HostLocal(); Show(Screen.Lobby); });
            UIFactory.Button(p, "SETTINGS", L, new Vector2(195, -330), new Vector2(250, 70), new Color(0.4f, 0.4f, 0.5f), () => Show(Screen.Settings), 28);
            UIFactory.Button(p, "QUIT", L, new Vector2(465, -330), new Vector2(250, 70), new Color(0.4f, 0.4f, 0.5f), Application.Quit, 28);
        }

        private void BuildJoin()
        {
            _join = UIFactory.Stretch(_root, "Join").gameObject;
            Transform p = _join.transform;
            Vector2 C = new Vector2(0.5f, 0.5f);
            UIFactory.Panel(p, "Back", UIFactory.PanelColor, C, Vector2.zero, new Vector2(900, 620));
            UIFactory.Text(p, "Title", "JOIN A ROOM", 64, UIFactory.Accent, TextAlignmentOptions.Center, C, new Vector2(0, 230), new Vector2(800, 80));
            _joinCode = UIFactory.Input(p, "STEAM ROOM CODE (ABC-DEF)", C, new Vector2(-110, 110), new Vector2(520, 70));
            UIFactory.Button(p, "JOIN CODE", C, new Vector2(290, 110), new Vector2(240, 70), UIFactory.Accent, () => { NetSession.Instance.JoinSteamCode(_joinCode.text); Show(Screen.Lobby); }, 28);
            UIFactory.Text(p, "Friends", "OR ACCEPT A STEAM INVITE (SHIFT+TAB)", 26, UIFactory.Paper, TextAlignmentOptions.Center, C, new Vector2(0, 30), new Vector2(800, 40));
            _joinIp = UIFactory.Input(p, "LAN / IP ADDRESS (127.0.0.1)", C, new Vector2(-110, -60), new Vector2(520, 70));
            UIFactory.Button(p, "JOIN IP", C, new Vector2(290, -60), new Vector2(240, 70), UIFactory.Accent2, () => { NetSession.Instance.JoinLocal(_joinIp.text); Show(Screen.Lobby); }, 28);
            UIFactory.Button(p, "BACK", C, new Vector2(0, -220), new Vector2(300, 70), new Color(0.4f, 0.4f, 0.5f), () => Show(Screen.Main), 28);
        }

        private void BuildSettings()
        {
            _settings = UIFactory.Stretch(_root, "Settings").gameObject;
            Transform p = _settings.transform;
            Vector2 C = new Vector2(0.5f, 0.5f);
            UIFactory.Panel(p, "Back", UIFactory.PanelColor, C, Vector2.zero, new Vector2(900, 620));
            UIFactory.Text(p, "Title", "SETTINGS", 64, UIFactory.Accent, TextAlignmentOptions.Center, C, new Vector2(0, 240), new Vector2(800, 80));
            SettingsPanel.Build(p, new Vector2(0, 30));
            UIFactory.Button(p, "BACK", C, new Vector2(0, -240), new Vector2(300, 70), new Color(0.4f, 0.4f, 0.5f), () => Show(Screen.Main), 28);
        }

        // ---------------------------------------------------------------- lobby

        private void BuildLobby()
        {
            _lobby = UIFactory.Stretch(_root, "Lobby").gameObject;
            Transform p = _lobby.transform;
            Vector2 C = new Vector2(0.5f, 0.5f);
            UIFactory.Panel(p, "PlayersBack", UIFactory.PanelColor, C, new Vector2(-470, 20), new Vector2(760, 820));
            UIFactory.Text(p, "PlayersTitle", "PLAYERS", 48, UIFactory.Accent, TextAlignmentOptions.Left, C, new Vector2(-470, 370), new Vector2(680, 60));
            _code = UIFactory.Text(p, "Code", "", 30, UIFactory.Good, TextAlignmentOptions.Right, C, new Vector2(-470, 370), new Vector2(680, 60));
            _lobbyPlayers = UIFactory.Text(p, "Players", "", 32, UIFactory.Paper, TextAlignmentOptions.TopLeft, C, new Vector2(-470, 40), new Vector2(680, 580));
            _teamButtons = UIFactory.Rect(p, "TeamButtons", C, C, C, new Vector2(-470, -290), new Vector2(700, 80));
            for (int i = 0; i < MatchRulesLogic.MAX_TEAMS; i++)
            {
                int lTeam = i;
                UIFactory.Button(_teamButtons, "TEAM " + (i + 1), C, new Vector2((i - 1.5f) * 170f, 0), new Vector2(160, 66), LobbyPlayer.TeamColor(i), () => LobbyPlayer.Local?.ChooseTeamRpc(lTeam), 26);
            }

            UIFactory.Panel(p, "SetupBack", UIFactory.PanelColor, C, new Vector2(420, 20), new Vector2(900, 820));
            _mapLabel = UIFactory.Text(p, "Map", "", 50, UIFactory.Accent2, TextAlignmentOptions.Center, C, new Vector2(420, 350), new Vector2(560, 70));
            _mapDescription = UIFactory.Text(p, "MapDesc", "", 24, UIFactory.Paper, TextAlignmentOptions.Top, C, new Vector2(420, 250), new Vector2(820, 120));
            _modeLabel = UIFactory.Text(p, "Mode", "", 40, UIFactory.Paper, TextAlignmentOptions.Center, C, new Vector2(420, 130), new Vector2(560, 60));
            _teamsLabel = UIFactory.Text(p, "Teams", "", 34, UIFactory.Paper, TextAlignmentOptions.Center, C, new Vector2(420, 40), new Vector2(560, 60));
            _lobbyInfo = UIFactory.Text(p, "Info", "", 26, UIFactory.Paper, TextAlignmentOptions.Center, C, new Vector2(420, -60), new Vector2(820, 80));

            _hostControls = UIFactory.Rect(p, "HostControls", C, C, C, new Vector2(420, 0), new Vector2(900, 800));
            Transform h = _hostControls;
            UIFactory.Button(h, "<", C, new Vector2(-340, 350), new Vector2(80, 70), UIFactory.Accent2, () => ChangeMap(-1));
            UIFactory.Button(h, ">", C, new Vector2(340, 350), new Vector2(80, 70), UIFactory.Accent2, () => ChangeMap(1));
            UIFactory.Button(h, "<", C, new Vector2(-340, 130), new Vector2(80, 64), UIFactory.Accent2, () => ChangeMode(-1));
            UIFactory.Button(h, ">", C, new Vector2(340, 130), new Vector2(80, 64), UIFactory.Accent2, () => ChangeMode(1));
            UIFactory.Button(h, "-", C, new Vector2(-340, 40), new Vector2(80, 60), UIFactory.Accent2, () => ChangeTeams(-1));
            UIFactory.Button(h, "+", C, new Vector2(340, 40), new Vector2(80, 60), UIFactory.Accent2, () => ChangeTeams(1));
            UIFactory.Toggle(h, "PROXIMITY VOICE CHAT", true, C, new Vector2(-40, -140), v => { if (LobbyState.Instance != null) LobbyState.Instance.ProximityChat.Value = v; });
            UIFactory.Button(h, "SHUFFLE TEAMS", C, new Vector2(-200, -220), new Vector2(330, 66), new Color(0.45f, 0.45f, 0.55f), ShuffleTeams, 26);
            UIFactory.Button(h, "INVITE FRIENDS", C, new Vector2(200, -220), new Vector2(330, 66), new Color(0.45f, 0.45f, 0.55f), () => SteamLobby.Instance?.Invite(), 26);
            _startButton = UIFactory.Button(h, "START CONTRACT", C, new Vector2(0, -320), new Vector2(560, 90), UIFactory.Good, () => LobbyState.Instance?.StartMatch(), 40);

            _readyButton = UIFactory.Button(p, "READY", C, new Vector2(420, -320), new Vector2(560, 90), UIFactory.Good, () => { if (LobbyPlayer.Local != null) LobbyPlayer.Local.Ready.Value = !LobbyPlayer.Local.Ready.Value; }, 40);
            UIFactory.Button(p, "LEAVE", C, new Vector2(-470, -390), new Vector2(300, 64), UIFactory.Accent, () => { NetSession.Instance.Leave(); Show(Screen.Main); }, 28);
        }

        private void ChangeMap(int pDelta)
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            int lCount = Mathf.Max(1, GameDatabase.Maps.Count);
            lState.MapIndex.Value = ((lState.MapIndex.Value + pDelta) % lCount + lCount) % lCount;
        }

        private void ChangeMode(int pDelta)
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            lState.Mode.Value = (MatchMode)((((int)lState.Mode.Value + pDelta) % 3 + 3) % 3);
            lState.ResolveTeams();
        }

        private void ChangeTeams(int pDelta)
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            lState.TeamCount.Value = Mathf.Clamp(lState.TeamCount.Value + pDelta, 2, MatchRulesLogic.MAX_TEAMS);
            lState.ResolveTeams();
        }

        private void ShuffleTeams()
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            System.Collections.Generic.List<int> lTeams = MatchRulesLogic.AutoBalance(LobbyPlayer.All.Count, lState.TeamCount.Value);
            System.Collections.Generic.List<LobbyPlayer> lShuffled = new System.Collections.Generic.List<LobbyPlayer>(LobbyPlayer.All);
            for (int i = 0; i < lShuffled.Count; i++) { int j = Random.Range(i, lShuffled.Count); (lShuffled[i], lShuffled[j]) = (lShuffled[j], lShuffled[i]); }
            for (int i = 0; i < lShuffled.Count; i++) lShuffled[i].ChooseTeamRpc(lTeams[i]);
            lState.Invoke(nameof(LobbyState.ResolveTeams), 0.3f);
        }

        private void Update()
        {
            if (_title != null) _title.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.3f) * 2.5f);

            if (_lobby.activeSelf && !NetworkManagerListening() && Time.timeSinceLevelLoad > 1f && (NetSession.Instance == null || NetSession.Instance.Kind == SessionKind.None)) Show(Screen.Main);
            if (!_lobby.activeSelf) return;

            LobbyState lState = LobbyState.Instance;
            bool lHost = lState != null && lState.IsServer;
            _hostControls.gameObject.SetActive(lHost);
            _readyButton.gameObject.SetActive(!lHost);
            if (lState == null) { _lobbyPlayers.text = "CONNECTING..."; return; }

            if (lHost) lState.ResolveTeams();
            MapDefinition lMap = lState.Map;
            _mapLabel.text = lMap != null ? lMap.displayName.ToUpperInvariant() : "-";
            _mapDescription.text = lMap != null ? lMap.description : "";
            _modeLabel.text = lState.Mode.Value switch
            {
                MatchMode.FreeForAll => "EVERY GOOB FOR THEMSELF",
                MatchMode.Teams => "TEAMS",
                _ => "CO-OP: ONE TEAM VS THE AI"
            };
            _teamsLabel.text = lState.Mode.Value == MatchMode.Teams ? lState.TeamCount.Value + " TEAMS" : "";
            _teamButtons.gameObject.SetActive(lState.Mode.Value == MatchMode.Teams);
            for (int i = 0; i < _teamButtons.childCount; i++) _teamButtons.GetChild(i).gameObject.SetActive(i < lState.TeamCount.Value);
            _lobbyInfo.text = "PROXIMITY CHAT " + (lState.ProximityChat.Value ? "ON" : "OFF") + "   -   " + Mathf.RoundToInt(lState.TimeLimitMinutes.Value) + " MIN";
            _code.text = SteamLobby.Instance != null && !string.IsNullOrEmpty(SteamLobby.Instance.Code) ? "CODE " + SteamLobby.Instance.Code : NetSession.Instance != null && NetSession.Instance.Kind == SessionKind.Local ? "LAN" : "";

            System.Text.StringBuilder lText = new System.Text.StringBuilder();
            bool lAllReady = true;
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All)
            {
                bool lIsHost = lPlayer.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
                if (!lIsHost && !lPlayer.Ready.Value) lAllReady = false;
                lText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(LobbyPlayer.TeamColor(lPlayer.Team.Value))).Append(">●</color> ")
                    .Append(lPlayer.Name.ToUpperInvariant())
                    .Append(lIsHost ? "  <size=70%><color=#FFE066>HOST</color></size>" : lPlayer.Ready.Value ? "  <size=70%><color=#7CF29A>READY</color></size>" : "  <size=70%><color=#999>NOT READY</color></size>")
                    .Append(lState.Mode.Value == MatchMode.Teams ? "  <size=70%>TEAM " + (lPlayer.Team.Value + 1) + "</size>" : "")
                    .Append('\n');
            }
            lText.Append("\n<size=70%>").Append(LobbyPlayer.All.Count).Append(" / ").Append(GameDatabase.Rules.maxPlayers).Append("</size>");
            _lobbyPlayers.text = lText.ToString();
            _startButton.interactable = lAllReady;
            TextMeshProUGUI lReadyLabel = _readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (LobbyPlayer.Local != null) lReadyLabel.text = LobbyPlayer.Local.Ready.Value ? "NOT READY" : "READY";
        }
    }
}
