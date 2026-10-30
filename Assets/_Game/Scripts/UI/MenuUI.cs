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
    /// The title scene's screens, in the game's sober dossier style: the main menu (online through
    /// Steam, friends, join, solo, settings, quit), the join screen, and the lobby - roster on the
    /// left, the contract (map, mode, teams, options) on the right. A cinematic flyover of a map
    /// plays behind, under a dark vignette.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private enum Screen { Main, Join, Lobby, Settings }

        private const string VERSION = "0.2.0";

        private RectTransform _root;
        private GameObject _main, _join, _lobby, _settings;
        private TextMeshProUGUI _status, _lobbyPlayers, _lobbyInfo, _code, _modeLabel, _mapLabel, _teamsLabel, _mapDescription, _playerCount;
        private TMP_InputField _joinCode, _joinIp, _name;
        private Button _startButton, _readyButton;
        private RectTransform _teamButtons, _hostControls;
        private Screen _current;

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

            // Cinematic framing: dark gradient on the left where the text sits, letterbox bars.
            Image lShade = UIFactory.Stretch(_root, "Shade").gameObject.AddComponent<Image>();
            lShade.sprite = GradientSprite();
            lShade.color = new Color(0, 0, 0, 0.85f);
            lShade.raycastTarget = false;
            UIFactory.Panel(_root, "BarTop", Color.black, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(4000, 48));
            UIFactory.Panel(_root, "BarBottom", Color.black, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(4000, 48));

            BuildMain();
            BuildJoin();
            BuildLobby();
            BuildSettings();
            _status = UIFactory.Title(_root, "Status", "", 20, UIFactory.Muted, TextAlignmentOptions.Right, new Vector2(1, 0), new Vector2(-560, 24), new Vector2(1000, 40), false, 3f);
            UIFactory.Title(_root, "Version", "GOOBYR FPS  ·  " + VERSION, 18, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(260, 24), new Vector2(440, 40), false, 4f);

            Show(NetworkManagerListening() ? Screen.Lobby : Screen.Main);
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
            _current = pScreen;
            _main.SetActive(pScreen == Screen.Main);
            _join.SetActive(pScreen == Screen.Join);
            _lobby.SetActive(pScreen == Screen.Lobby);
            _settings.SetActive(pScreen == Screen.Settings);
            GameObject lShown = pScreen switch { Screen.Main => _main, Screen.Join => _join, Screen.Lobby => _lobby, _ => _settings };
            UIFactory.FadeIn(lShown, 0.3f, new Vector2(-24, 0));
        }

        private static Sprite GradientSprite()
        {
            Texture2D lTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++)
            {
                float t = x / 255f;
                lTexture.SetPixel(x, 0, new Color(1, 1, 1, Mathf.Clamp01(1.05f - t * 1.6f)));
            }
            lTexture.Apply();
            return Sprite.Create(lTexture, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A section header: small caps label with a short red rule above it.</summary>
        private static void Header(Transform pParent, string pText, Vector2 pAnchor, Vector2 pPosition, float pWidth = 600f)
        {
            // Left-anchored elements pivot on their left edge, right-anchored ones on their right edge.
            UIFactory.Rule(pParent, pAnchor, pPosition + new Vector2(pAnchor.x > 0.9f ? -pWidth + 40 : 0, 24), 40, false, UIFactory.Accent, 3f);
            UIFactory.Title(pParent, "Header", pText, 22, UIFactory.Muted, TextAlignmentOptions.Left, pAnchor, pPosition, new Vector2(pWidth, 34), false, 6f);
        }

        // ---------------------------------------------------------------- main

        private void BuildMain()
        {
            _main = UIFactory.Stretch(_root, "Main").gameObject;
            Transform p = _main.transform;
            Vector2 L = new Vector2(0, 0.5f);
            UIFactory.Title(p, "Title", "GOOBYR FPS", 128, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(170, 300), new Vector2(1100, 150), true, 10f);
            UIFactory.Rule(p, L, new Vector2(170, 222), 220, false, UIFactory.Accent, 4f);
            UIFactory.Title(p, "Subtitle", "CONTRACTS  ·  UP TO 8 PLAYERS  ·  EVERYONE IS A TARGET", 22, UIFactory.Muted, TextAlignmentOptions.Left, L, new Vector2(170, 190), new Vector2(1100, 36), false, 6f);

            Header(p, "AGENT", L, new Vector2(170, 120), 520);
            _name = UIFactory.Input(p, "YOUR NAME", L, new Vector2(170, 76), new Vector2(520, 52));
            _name.text = PlayerIdentity.LocalName;
            _name.onEndEdit.AddListener(v => PlayerIdentity.LocalName = v);

            Header(p, "PLAY", L, new Vector2(170, 6), 520);
            float y = -40;
            void Item(string pLabel, System.Action pAction) { UIFactory.Button(p, pLabel, L, new Vector2(170, y), new Vector2(520, 56), UIFactory.Accent2, pAction, 32); y -= 58; }
            Item("HOST ONLINE", () => { NetSession.Instance.HostSteam(false); Show(Screen.Lobby); });
            Item("HOST FOR FRIENDS", () => { NetSession.Instance.HostSteam(true); Show(Screen.Lobby); });
            Item("JOIN A CONTRACT", () => Show(Screen.Join));
            Item("SOLO AGAINST THE AI", () => { NetSession.Instance.HostLocal(); Show(Screen.Lobby); });
            Item("OPTIONS", () => Show(Screen.Settings));
            Item("QUIT", Application.Quit);
        }

        private void BuildJoin()
        {
            _join = UIFactory.Stretch(_root, "Join").gameObject;
            Transform p = _join.transform;
            Vector2 L = new Vector2(0, 0.5f);
            UIFactory.Title(p, "Title", "JOIN", 96, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(170, 260), new Vector2(1100, 120), true, 10f);
            Header(p, "STEAM ROOM CODE", L, new Vector2(170, 140), 600);
            _joinCode = UIFactory.Input(p, "ABC-DEF", L, new Vector2(170, 96), new Vector2(400, 52));
            UIFactory.Button(p, "JOIN", L, new Vector2(590, 96), new Vector2(200, 52), UIFactory.Accent, () => { NetSession.Instance.JoinSteamCode(_joinCode.text); Show(Screen.Lobby); }, 28);
            UIFactory.Title(p, "Friends", "OR ACCEPT A STEAM INVITE  (SHIFT + TAB)", 20, UIFactory.Muted, TextAlignmentOptions.Left, L, new Vector2(170, 40), new Vector2(600, 30), false, 4f);
            Header(p, "LAN / DIRECT IP", L, new Vector2(170, -40), 600);
            _joinIp = UIFactory.Input(p, "127.0.0.1", L, new Vector2(170, -84), new Vector2(400, 52));
            UIFactory.Button(p, "CONNECT", L, new Vector2(590, -84), new Vector2(200, 52), UIFactory.Accent, () => { NetSession.Instance.JoinLocal(_joinIp.text); Show(Screen.Lobby); }, 28);
            UIFactory.Button(p, "BACK", L, new Vector2(170, -200), new Vector2(400, 52), UIFactory.Accent2, () => Show(Screen.Main), 28);
        }

        private void BuildSettings()
        {
            _settings = UIFactory.Stretch(_root, "Settings").gameObject;
            Transform p = _settings.transform;
            Vector2 L = new Vector2(0, 0.5f);
            UIFactory.Title(p, "Title", "OPTIONS", 96, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(170, 280), new Vector2(1100, 120), true, 10f);
            SettingsPanel.Build(p, new Vector2(-560, 30));
            UIFactory.Button(p, "BACK", L, new Vector2(170, -260), new Vector2(400, 52), UIFactory.Accent2, () => Show(Screen.Main), 28);
        }

        // ---------------------------------------------------------------- lobby

        private void BuildLobby()
        {
            _lobby = UIFactory.Stretch(_root, "Lobby").gameObject;
            Transform p = _lobby.transform;
            Vector2 L = new Vector2(0, 0.5f), Rt = new Vector2(1, 0.5f);

            UIFactory.Title(p, "Title", "BRIEFING", 96, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(170, 380), new Vector2(1100, 120), true, 10f);
            Header(p, "AGENTS", L, new Vector2(170, 290), 600);
            _code = UIFactory.Title(p, "Code", "", 22, UIFactory.Paper, TextAlignmentOptions.Right, L, new Vector2(170, 290), new Vector2(600, 34), false, 5f);
            _lobbyPlayers = UIFactory.Text(p, "Players", "", 28, UIFactory.Paper, TextAlignmentOptions.TopLeft, L, new Vector2(170, 40), new Vector2(600, 460));
            _lobbyPlayers.lineSpacing = 22f;
            _playerCount = UIFactory.Title(p, "Count", "", 20, UIFactory.Muted, TextAlignmentOptions.Left, L, new Vector2(170, -200), new Vector2(600, 30), false, 4f);
            _teamButtons = UIFactory.Rect(p, "TeamButtons", L, L, L, new Vector2(170, -250), new Vector2(600, 52));
            for (int i = 0; i < MatchRulesLogic.MAX_TEAMS; i++)
            {
                int lTeam = i;
                Button lButton = UIFactory.Button(_teamButtons, "TEAM " + (i + 1), new Vector2(0.5f, 0.5f), new Vector2((i - 1.5f) * 150f, 0), new Vector2(140, 48), UIFactory.Accent2, () => LobbyPlayer.Local?.ChooseTeamRpc(lTeam), 24);
                UIFactory.Rule(lButton.transform, new Vector2(0.5f, 0), new Vector2(0, 2), 140, false, LobbyPlayer.TeamColor(i), 4f);
            }
            UIFactory.Button(p, "LEAVE", L, new Vector2(170, -380), new Vector2(400, 52), UIFactory.Accent2, () => { NetSession.Instance.Leave(); Show(Screen.Main); }, 28);

            // Contract column.
            UIFactory.Panel(p, "ContractBack", new Color(0, 0, 0, 0.55f), Rt, new Vector2(-90, -10), new Vector2(760, 820));
            Header(p, "CONTRACT", Rt, new Vector2(-130, 370), 680);
            _mapLabel = UIFactory.Title(p, "Map", "", 64, UIFactory.Paper, TextAlignmentOptions.Left, Rt, new Vector2(-130, 310), new Vector2(680, 80), true, 6f);
            _mapDescription = UIFactory.Text(p, "MapDesc", "", 24, UIFactory.Muted, TextAlignmentOptions.TopLeft, Rt, new Vector2(-130, 220), new Vector2(680, 100));
            UIFactory.Rule(p, Rt, new Vector2(-130, 160), 680);
            _modeLabel = UIFactory.Title(p, "Mode", "", 34, UIFactory.Paper, TextAlignmentOptions.Left, Rt, new Vector2(-130, 120), new Vector2(680, 50), true, 5f);
            _teamsLabel = UIFactory.Title(p, "Teams", "", 26, UIFactory.Paper, TextAlignmentOptions.Left, Rt, new Vector2(-130, 66), new Vector2(680, 40), false, 4f);
            _lobbyInfo = UIFactory.Title(p, "Info", "", 22, UIFactory.Muted, TextAlignmentOptions.Left, Rt, new Vector2(-130, 20), new Vector2(680, 40), false, 4f);

            _hostControls = UIFactory.Rect(p, "HostControls", Rt, Rt, Rt, new Vector2(-90, 0), new Vector2(760, 820));
            Transform h = _hostControls;
            Vector2 C = new Vector2(0.5f, 0.5f);
            UIFactory.Button(h, "‹", C, new Vector2(220, 310), new Vector2(60, 60), UIFactory.Accent2, () => ChangeMap(-1), 44);
            UIFactory.Button(h, "›", C, new Vector2(300, 310), new Vector2(60, 60), UIFactory.Accent2, () => ChangeMap(1), 44);
            UIFactory.Button(h, "‹", C, new Vector2(220, 120), new Vector2(60, 50), UIFactory.Accent2, () => ChangeMode(-1), 40);
            UIFactory.Button(h, "›", C, new Vector2(300, 120), new Vector2(60, 50), UIFactory.Accent2, () => ChangeMode(1), 40);
            UIFactory.Button(h, "−", C, new Vector2(220, 66), new Vector2(60, 44), UIFactory.Accent2, () => ChangeTeams(-1), 34);
            UIFactory.Button(h, "+", C, new Vector2(300, 66), new Vector2(60, 44), UIFactory.Accent2, () => ChangeTeams(1), 34);
            UIFactory.Toggle(h, "PROXIMITY VOICE CHAT", true, C, new Vector2(-60, -60), v => { if (LobbyState.Instance != null) LobbyState.Instance.ProximityChat.Value = v; });
            UIFactory.Button(h, "SHUFFLE TEAMS", C, new Vector2(-170, -150), new Vector2(330, 52), UIFactory.Accent2, ShuffleTeams, 26);
            UIFactory.Button(h, "INVITE FRIENDS", C, new Vector2(170, -150), new Vector2(330, 52), UIFactory.Accent2, () => SteamLobby.Instance?.Invite(), 26);
            _startButton = UIFactory.Button(h, "START CONTRACT", C, new Vector2(0, -330), new Vector2(680, 76), UIFactory.Accent, () => LobbyState.Instance?.StartMatch(), 36);
            _readyButton = UIFactory.Button(p, "READY", Rt, new Vector2(-130, -330), new Vector2(680, 76), UIFactory.Accent, () => { if (LobbyPlayer.Local != null) LobbyPlayer.Local.Ready.Value = !LobbyPlayer.Local.Ready.Value; }, 36);
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
            if (_current == Screen.Lobby && !NetworkManagerListening() && Time.timeSinceLevelLoad > 1f && (NetSession.Instance == null || NetSession.Instance.Kind == SessionKind.None)) Show(Screen.Main);
            if (_current != Screen.Lobby) return;

            LobbyState lState = LobbyState.Instance;
            bool lHost = lState != null && lState.IsServer;
            _hostControls.gameObject.SetActive(lHost);
            _readyButton.gameObject.SetActive(!lHost);
            if (lState == null) { _lobbyPlayers.text = "<color=#9EA0A6>ESTABLISHING CONNECTION...</color>"; return; }

            if (lHost) lState.ResolveTeams();
            MapDefinition lMap = lState.Map;
            _mapLabel.text = lMap != null ? lMap.displayName.ToUpperInvariant() : "-";
            _mapDescription.text = lMap != null ? lMap.description : "";
            _modeLabel.text = lState.Mode.Value switch
            {
                MatchMode.FreeForAll => "FREE FOR ALL",
                MatchMode.Teams => "TEAMS",
                _ => "CO-OP  ·  ONE TEAM AGAINST THE AI"
            };
            _teamsLabel.text = lState.Mode.Value == MatchMode.Teams ? lState.TeamCount.Value + " TEAMS" : "";
            _teamButtons.gameObject.SetActive(lState.Mode.Value == MatchMode.Teams);
            for (int i = 0; i < _teamButtons.childCount; i++) _teamButtons.GetChild(i).gameObject.SetActive(i < lState.TeamCount.Value);
            _lobbyInfo.text = "PROXIMITY VOICE " + (lState.ProximityChat.Value ? "ON" : "OFF") + "   ·   " + Mathf.RoundToInt(lState.TimeLimitMinutes.Value) + " MINUTES";
            _code.text = SteamLobby.Instance != null && !string.IsNullOrEmpty(SteamLobby.Instance.Code) ? "ROOM  <color=#FFFFFF>" + SteamLobby.Instance.Code + "</color>" : NetSession.Instance != null && NetSession.Instance.Kind == SessionKind.Local ? "LOCAL" : "";

            System.Text.StringBuilder lText = new System.Text.StringBuilder();
            bool lAllReady = true;
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All)
            {
                bool lIsHost = lPlayer.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
                if (!lIsHost && !lPlayer.Ready.Value) lAllReady = false;
                string lTeam = ColorUtility.ToHtmlStringRGB(LobbyPlayer.TeamColor(lPlayer.Team.Value));
                lText.Append("<color=#").Append(lTeam).Append(">|</color>  ").Append(lPlayer.Name.ToUpperInvariant())
                    .Append("<pos=62%><size=70%>")
                    .Append(lState.Mode.Value == MatchMode.Teams ? "TEAM " + (lPlayer.Team.Value + 1) + "   " : "")
                    .Append(lIsHost ? "<color=#DC2229>HOST</color>" : lPlayer.Ready.Value ? "<color=#73D18C>READY</color>" : "<color=#9EA0A6>WAITING</color>")
                    .Append("</size>\n");
            }
            _lobbyPlayers.text = lText.ToString();
            _playerCount.text = LobbyPlayer.All.Count + " / " + GameDatabase.Rules.maxPlayers + " AGENTS";
            _startButton.interactable = lAllReady;
            TextMeshProUGUI lReadyLabel = _readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (LobbyPlayer.Local != null) lReadyLabel.text = LobbyPlayer.Local.Ready.Value ? "STAND DOWN" : "READY";
        }
    }
}
