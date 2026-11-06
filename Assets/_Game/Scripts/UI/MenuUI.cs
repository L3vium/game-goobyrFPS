using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The front of the game over a living scene (<see cref="TitleDiorama"/>). <b>Title</b>: create a room,
    /// join one, options, quit, the agent's name. <b>Join</b>: friends and their rooms, or a code.
    /// <b>The briefing</b>: the map on the left; nobody has a team to begin with, every agent for
    /// themselves, until teams are made in the middle column and agents dragged into them (each team has
    /// its game mode); on the right the agents without a team, the name and the start. Esc opens the
    /// options over any of it.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private enum Screen { Main, Join, Lobby, Settings }

        private static readonly Vector2 TL = new Vector2(0, 1);
        private static readonly Color Navy = new Color(0.1f, 0.11f, 0.17f, 1f);
        private static readonly Color Dim = new Color(0.05f, 0.055f, 0.09f, 0.86f);
        private static readonly Color Slate = new Color(1f, 1f, 1f, 0.05f);

        // The three columns of the briefing.
        private const float MAP_X = 110f, MAP_W = 620f, TEAM_X = 790f, TEAM_W = 580f, AGENT_X = 1430f, AGENT_W = 380f;
        private const float TOP = 214f, HEADER_H = 48f, ROW_H = 42f, DROP_H = 42f, BOX_GAP = 12f;
        private const float MAP_H = 349f, PLAN = 180f;
        private const int MAX_ROWS = 12;
        private const float JOIN_TIME = 15f;

        private class TeamBox
        {
            public RectTransform rect;
            public Image bar, drop;
            public TextMeshProUGUI title, modeLabel;
            public Button mode, remove;
        }

        private class AgentRow
        {
            public RectTransform rect;
            public Image back, icon;
            public Button iconButton;
            public TextMeshProUGUI name, state;
            public LobbyPlayer player;
            public AgentRowDrag drag;
        }

        private class FriendRow
        {
            public RectTransform rect;
            public TextMeshProUGUI name, state;
            public Button join;
            public ulong lobby;
        }

        private RectTransform _root;
        private GameObject _main, _join, _lobby, _settings, _iconPicker;
        private TextMeshProUGUI _status, _mapName, _mapDescription, _playerCount, _startHint, _code, _joinStatus, _noFriends, _voidHint;
        private TMP_InputField _titleName, _name, _joinField;
        private Button _startButton, _readyButton, _copyButton, _invite, _shuffle, _addBox, _left, _right, _roleAgent, _roleCaster;
        private RawImage _mapPicture, _mapPlan;
        private GameObject _planFrame;
        private Screen _current, _beforeSettings;
        private RectTransform _voidArea, _friendList;
        private readonly List<TeamBox> _boxes = new List<TeamBox>();
        private readonly List<AgentRow> _rows = new List<AgentRow>();
        private readonly List<FriendRow> _friends = new List<FriendRow>();
        private RectTransform _ghost;
        private TextMeshProUGUI _ghostName;
        private float _joiningUntil, _friendsAt;
        private bool _joining;

        private void Start()
        {
            GameInput.GameplayEnabled = false;
            GameInput.LockCursor(false);
            Hud.Remove();
            CasterView.Stop();
            Spectator.Stop();
            HumanRagdoll.ClearAll();

            // Behind the menus, the scene the title builder set up; a plain colour without it.
            if (FindFirstObjectByType<TitleDiorama>() == null)
            {
                Camera lCamera = new GameObject("MenuCamera").AddComponent<Camera>();
                lCamera.transform.SetParent(transform, false);
                lCamera.clearFlags = CameraClearFlags.SolidColor;
                lCamera.backgroundColor = Navy;
                lCamera.cullingMask = 0;
            }

            Canvas lCanvas = UIFactory.Canvas("MenuCanvas", 20);
            lCanvas.transform.SetParent(transform, false);
            _root = (RectTransform)lCanvas.transform;

            BuildMain();
            BuildJoin();
            BuildLobby();
            BuildSettings();
            BuildIconPicker();
            _status = UIFactory.Title(_root, "Status", "", 16, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(1, 1), new Vector2(-500, -150), new Vector2(620, 24), false, 3f);

            Show(NetworkManagerListening() ? Screen.Lobby : Screen.Main);
            NetSession.OnStatus += HandleStatus;
            if (NetSession.Instance != null) NetSession.Instance.EnsureSteam();
            if (SteamLobby.Instance != null) SteamLobby.Instance.OnJoinFailed += HandleJoinFailed;
        }

        private void OnDestroy()
        {
            NetSession.OnStatus -= HandleStatus;
            if (SteamLobby.Instance != null) SteamLobby.Instance.OnJoinFailed -= HandleJoinFailed;
        }

        private void HandleStatus(string pStatus)
        {
            if (_joinStatus != null && _current == Screen.Join) _joinStatus.text = pStatus;
        }

        private static bool NetworkManagerListening() => NetSession.Instance != null && NetSession.Instance.Network.IsListening;
        private static bool SteamReady => SteamLobby.Instance != null && SteamLobby.Instance.IsReady;

        private void Show(Screen pScreen)
        {
            _current = pScreen;
            _main.SetActive(pScreen == Screen.Main);
            _join.SetActive(pScreen == Screen.Join);
            _lobby.SetActive(pScreen == Screen.Lobby || (pScreen == Screen.Settings && _beforeSettings == Screen.Lobby));
            _settings.SetActive(pScreen == Screen.Settings);
            GameObject lShown = pScreen switch { Screen.Main => _main, Screen.Join => _join, Screen.Lobby => _lobby, _ => _settings };
            UIFactory.FadeIn(lShown, 0.25f, Vector2.zero);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (pScreen == Screen.Join) { _friendsAt = 0f; _joinStatus.text = ""; }
        }

        private void OpenSettings()
        {
            _beforeSettings = _current;
            Show(Screen.Settings);
        }

        private static RectTransform Box(Transform pParent, string pName, float pX, float pY, float pWidth, float pHeight) =>
            UIFactory.Rect(pParent, pName, TL, TL, TL, new Vector2(pX, -pY), new Vector2(pWidth, pHeight));

        private static TextMeshProUGUI Label(Transform pParent, string pText, float pSize, Color pColor, float pX, float pY, float pWidth, float pHeight, bool pBold = false, float pTracking = 3f, TextAlignmentOptions pAlign = TextAlignmentOptions.TopLeft)
        {
            TextMeshProUGUI lText = UIFactory.Title(pParent, "Label", pText, pSize, pColor, pAlign, TL, Vector2.zero, new Vector2(pWidth, pHeight), pBold, pTracking);
            lText.rectTransform.pivot = TL;
            lText.rectTransform.anchoredPosition = new Vector2(pX, -pY);
            return lText;
        }

        /// <summary>A section header: small caps over a short accent rule.</summary>
        private static void Header(Transform pParent, string pText, float pX, float pY)
        {
            Label(pParent, pText, 16, UIFactory.Muted, pX, pY, 500, 22, true, 5f);
            UIFactory.Rule(pParent, TL, new Vector2(pX, -pY - 28), 28, false, UIFactory.Accent, 2f);
        }

        private static Button MenuButton(Transform pParent, string pLabel, float pX, float pY, float pWidth, float pHeight, System.Action pAction, float pFont = 20f, bool pPrimary = false)
        {
            Button lButton = UIFactory.Button(pParent, pLabel, TL, new Vector2(pX, -pY), new Vector2(pWidth, pHeight), pPrimary ? UIFactory.Accent : UIFactory.Accent2, pAction, pFont);
            ((RectTransform)lButton.transform).pivot = TL;
            ((RectTransform)lButton.transform).anchoredPosition = new Vector2(pX, -pY);
            return lButton;
        }

        private static TMP_InputField NameField(Transform pParent, Vector2 pAnchor, Vector2 pPosition, Vector2 pSize)
        {
            TMP_InputField lField = UIFactory.Input(pParent, "YOUR NAME", pAnchor, pPosition, pSize);
            lField.characterLimit = 24;
            lField.text = PlayerIdentity.LocalName;
            lField.onEndEdit.AddListener(v => PlayerIdentity.LocalName = v);
            return lField;
        }

        // ---------------------------------------------------------------- title

        private void BuildMain()
        {
            _main = UIFactory.Stretch(_root, "Main").gameObject;
            Transform p = _main.transform;
            Label(p, "HITMEN", 230, UIFactory.Paper, 96, 70, 1200, 250, true, 6f);

            (string label, System.Action action)[] lEntries =
            {
                ("CREATE A ROOM", CreateRoom),
                ("JOIN A ROOM", () => Show(Screen.Join)),
                ("OPTIONS", OpenSettings),
                ("QUIT", Application.Quit)
            };
            for (int i = 0; i < lEntries.Length; i++) MenuButton(p, lEntries[i].label, 110, 370 + i * 104, 540, 88, lEntries[i].action, 40, true);

            // The agent's name, bottom right.
            UIFactory.Title(p, "AgentLabel", "AGENT NAME", 16, UIFactory.Paper, TextAlignmentOptions.Right, new Vector2(1, 0), new Vector2(-110, 96), new Vector2(380, 24), true, 4f);
            _titleName = NameField(p, new Vector2(1, 0), new Vector2(-110, 40), new Vector2(380, 56));
            ((RectTransform)_titleName.transform).pivot = new Vector2(1, 0.5f);
            UIFactory.Rule(p, new Vector2(1, 0), new Vector2(-110, 114), 380, false, new Color(1, 1, 1, 0.2f), 1f);
        }

        /// <summary>A room right away, on Steam when it runs (invites work at once), else on the local network.</summary>
        private void CreateRoom()
        {
            if (SteamReady) NetSession.Instance.HostSteam(false);
            else NetSession.Instance.HostLocal();
            Show(Screen.Lobby);
        }

        // ---------------------------------------------------------------- join

        private void BuildJoin()
        {
            _join = UIFactory.Stretch(_root, "Join").gameObject;
            _join.AddComponent<Image>().color = Dim;
            Transform p = _join.transform;
            Label(p, "JOIN A ROOM", 88, UIFactory.Paper, 104, 70, 1200, 110, true, 4f);

            Header(p, "FRIENDS", 110, 214);
            _friendList = UIWidgets.ScrollList(p, "Friends", TL, new Vector2(110 + 400, -258), 800f, 640f);
            _noFriends = Label(p, "", 22, UIFactory.Muted, 110, 262, 800, 34, false, 2f);

            Header(p, "ROOM CODE", 1010, 214);
            _joinField = UIFactory.Input(p, "ABC-DEF", TL, Vector2.zero, new Vector2(800, 104));
            ((RectTransform)_joinField.transform).pivot = TL;
            ((RectTransform)_joinField.transform).anchoredPosition = new Vector2(1010, -258);
            _joinField.characterLimit = 40;
            _joinField.textComponent.fontSize = 52;
            _joinField.textComponent.characterSpacing = 14f;
            _joinField.onValidateInput = (pText, pIndex, pChar) => char.IsLetterOrDigit(pChar) || pChar == '-' || pChar == '.' ? char.ToUpperInvariant(pChar) : '\0';
            _joinField.onSubmit.AddListener(_ => JoinByField());
            Label(p, "ASK THE HOST FOR THE ROOM'S CODE. ON THE SAME NETWORK, AN IP WORKS TOO.", 16, UIFactory.Muted, 1010, 376, 800, 24, false, 2f);
            MenuButton(p, "JOIN", 1010, 420, 800, 96, JoinByField, 40, true);
            _joinStatus = Label(p, "", 20, UIFactory.Accent, 1010, 534, 800, 30, true, 2f);

            MenuButton(p, "BACK", 110, 960, 300, 60, () => Show(Screen.Main), 26);
        }

        private void JoinByField()
        {
            string lWhere = _joinField.text.Trim();
            if (lWhere.Length == 0) return;
            StartJoining();
            if (lWhere.Contains(".")) NetSession.Instance.JoinLocal(lWhere);
            else NetSession.Instance.JoinSteamCode(lWhere);
        }

        private void JoinFriend(ulong pLobby)
        {
            StartJoining();
            NetSession.Instance.JoinSteamLobby(pLobby);
        }

        private void StartJoining()
        {
            _joining = true;
            _joiningUntil = Time.unscaledTime + JOIN_TIME;
            _joinStatus.text = "LOOKING FOR THE ROOM...";
        }

        private void HandleJoinFailed(string pWhy) => StopJoining(pWhy);

        /// <summary>The attempt is over: the screen stays, it only says why.</summary>
        private void StopJoining(string pWhy)
        {
            if (!_joining) return;
            _joining = false;
            if (NetworkManagerListening() && NetSession.Instance != null) NetSession.Instance.Leave();
            if (_joinStatus != null) _joinStatus.text = pWhy;
        }

        private void RefreshFriends()
        {
            if (Time.unscaledTime < _friendsAt) return;
            _friendsAt = Time.unscaledTime + 3f;
            List<SteamLobby.FriendRoom> lFriends = SteamLobby.Instance != null ? SteamLobby.Instance.Friends() : new List<SteamLobby.FriendRoom>();
            _noFriends.text = !SteamReady ? "STEAM IS NOT RUNNING: USE A CODE OR AN IP." : lFriends.Count == 0 ? "NONE OF YOUR FRIENDS IS ONLINE." : "";
            while (_friends.Count < lFriends.Count) _friends.Add(BuildFriendRow());
            for (int i = 0; i < _friends.Count; i++)
            {
                FriendRow lRow = _friends[i];
                bool lOn = i < lFriends.Count;
                lRow.rect.gameObject.SetActive(lOn);
                if (!lOn) continue;
                SteamLobby.FriendRoom lFriend = lFriends[i];
                lRow.name.text = lFriend.name.ToUpperInvariant();
                lRow.lobby = lFriend.lobby;
                lRow.state.text = lFriend.lobby != 0 ? "IN A ROOM" : lFriend.inGame ? "PLAYING" : "ONLINE";
                lRow.join.gameObject.SetActive(lFriend.lobby != 0);
            }
        }

        private FriendRow BuildFriendRow()
        {
            FriendRow lRow = new FriendRow();
            lRow.rect = UIWidgets.Row(_friendList, "Friend", 60f, true);
            lRow.name = lRow.rect.Find("Label").GetComponent<TextMeshProUGUI>();
            lRow.name.rectTransform.sizeDelta = new Vector2(420, 60);
            lRow.state = UIFactory.Title(lRow.rect, "State", "", 15, UIFactory.Muted, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-180, 0), new Vector2(160, 30), true, 2f);
            lRow.state.rectTransform.pivot = new Vector2(1, 0.5f);
            lRow.join = UIFactory.Button(lRow.rect, "JOIN", new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(150, 44), UIFactory.Accent, () => JoinFriend(lRow.lobby), 22);
            ((RectTransform)lRow.join.transform).pivot = new Vector2(1, 0.5f);
            return lRow;
        }

        private void BuildSettings()
        {
            _settings = UIFactory.Stretch(_root, "Settings").gameObject;
            Transform p = _settings.transform;
            _settings.AddComponent<Image>().color = new Color(Navy.r, Navy.g, Navy.b, 0.96f);
            Label(p, "OPTIONS", 72, UIFactory.Paper, 104, 70, 1100, 90, true, 8f);
            SettingsPanel.Build(p, TL, new Vector2(110 + 450, -190), 900f, 760f);
            MenuButton(p, "BACK", 110, 960, 300, 60, CloseSettings, 26);
        }

        private void CloseSettings() => Show(_beforeSettings);

        // ---------------------------------------------------------------- the briefing

        private void BuildLobby()
        {
            _lobby = UIFactory.Stretch(_root, "Lobby").gameObject;
            Transform p = _lobby.transform;
            _lobby.AddComponent<Image>().color = Dim;

            Label(p, "BRIEFING", 72, UIFactory.Paper, 106, 56, 800, 90, true, 5f);

            // The room's code, top right.
            _code = Label(p, "", 26, UIFactory.Paper, 1080, 92, 600, 36, true, 6f, TextAlignmentOptions.TopRight);
            _copyButton = MenuButton(p, "COPY", 1700, 80, 110, 52, () =>
            {
                if (SteamLobby.Instance != null) GUIUtility.systemCopyBuffer = SteamLobby.Instance.Code;
                _status.text = "CODE COPIED";
            }, 20);

            // The map: its picture, the plan in the top left corner of it, its name and its story.
            Header(p, "MAP", MAP_X, 166);
            RectTransform lFrame = Box(p, "MapFrame", MAP_X, TOP, MAP_W, MAP_H);
            lFrame.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.055f, 0.09f, 1f);
            _mapPicture = UIFactory.Stretch(lFrame, "Picture").gameObject.AddComponent<RawImage>();
            _mapPicture.raycastTarget = false;
            _planFrame = Box(lFrame, "Plan", 10, 10, PLAN, PLAN).gameObject;
            Image lPlanBack = _planFrame.AddComponent<Image>();
            lPlanBack.color = new Color(0.05f, 0.055f, 0.09f, 0.92f);
            lPlanBack.raycastTarget = false;
            _mapPlan = Box(_planFrame.transform, "Sheet", 4, 4, PLAN - 8, PLAN - 8).gameObject.AddComponent<RawImage>();
            _mapPlan.raycastTarget = false;
            _left = MenuButton(lFrame, "‹", 0, MAP_H / 2 - 56, 56, 112, () => ChangeMap(-1), 48);
            _right = MenuButton(lFrame, "›", MAP_W - 56, MAP_H / 2 - 56, 56, 112, () => ChangeMap(1), 48);
            _mapName = Label(p, "", 40, UIFactory.Paper, MAP_X, TOP + MAP_H + 16, MAP_W, 50, true, 3f);
            _mapDescription = UIFactory.Text(p, "Description", "", 20, UIFactory.Muted, TextAlignmentOptions.TopLeft, TL, Vector2.zero, new Vector2(MAP_W, 180));
            _mapDescription.rectTransform.pivot = TL;
            _mapDescription.rectTransform.anchoredPosition = new Vector2(MAP_X, -(TOP + MAP_H + 70));
            _mapDescription.textWrappingMode = TextWrappingModes.Normal;

            MenuButton(p, "LEAVE", MAP_X, 960, 300, 60, () => { NetSession.Instance.Leave(); Show(Screen.Main); }, 26);

            // The teams, one under the other.
            Header(p, "TEAMS", TEAM_X, 166);
            for (int i = 0; i < MatchRulesLogic.MAX_BOXES; i++) _boxes.Add(BuildBox(p, i));
            _addBox = MenuButton(p, "+  NEW TEAM", TEAM_X, TOP, TEAM_W, 64, () => LobbyState.Instance?.AddBoxRpc(), 26);
            _shuffle = MenuButton(p, "SHUFFLE THE TEAMS", TEAM_X, 930, TEAM_W, 90, () => LobbyState.Instance?.ShuffleRpc(), 34);

            // The agents still on their own, you with them or not; the name and the start below.
            Header(p, "AGENTS", AGENT_X, 166);
            _playerCount = Label(p, "", 16, UIFactory.Muted, AGENT_X, 166, AGENT_W, 24, true, 4f, TextAlignmentOptions.TopRight);
            _roleAgent = MenuButton(p, "AGENT", AGENT_X, TOP, AGENT_W / 2 - 3, 50, () => SetCaster(false), 22);
            _roleCaster = MenuButton(p, "CASTER", AGENT_X + AGENT_W / 2 + 3, TOP, AGENT_W / 2 - 3, 50, () => SetCaster(true), 22);
            _voidArea = Box(p, "NoTeam", AGENT_X, TOP + 60, AGENT_W, 390);
            Image lVoid = _voidArea.gameObject.AddComponent<Image>();
            lVoid.color = new Color(1, 1, 1, 0.025f);
            lVoid.raycastTarget = false;
            _voidHint = Label(_voidArea, "EVERY AGENT FOR THEMSELVES", 14, new Color(1, 1, 1, 0.35f), 0, 362, AGENT_W, 20, false, 3f, TextAlignmentOptions.Top);
            for (int i = 0; i < MAX_ROWS; i++) _rows.Add(BuildRow(p));

            _invite = MenuButton(p, "INVITE FRIENDS", AGENT_X, 670, AGENT_W, 50, () => SteamLobby.Instance?.Invite(), 22);
            Label(p, "AGENT NAME", 15, UIFactory.Paper, AGENT_X, 738, AGENT_W, 22, true, 4f);
            _name = NameField(p, TL, Vector2.zero, new Vector2(AGENT_W, 56));
            ((RectTransform)_name.transform).pivot = TL;
            ((RectTransform)_name.transform).anchoredPosition = new Vector2(AGENT_X, -764);
            _startHint = Label(p, "", 16, UIFactory.Muted, AGENT_X, 832, AGENT_W, 60, false, 2f);
            _startHint.textWrappingMode = TextWrappingModes.Normal;
            _startButton = MenuButton(p, "START CONTRACT", AGENT_X, 900, AGENT_W, 120, () => LobbyState.Instance?.StartMatch(), 42, true);
            _readyButton = MenuButton(p, "READY", AGENT_X, 900, AGENT_W, 120, () => { if (LobbyPlayer.Local != null) LobbyPlayer.Local.Ready.Value = !LobbyPlayer.Local.Ready.Value; }, 42, true);

            // What follows the pointer while an agent is dragged.
            _ghost = UIFactory.Panel(p, "Ghost", new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(AGENT_W, ROW_H)).rectTransform;
            _ghostName = UIFactory.Title(_ghost, "Name", "", 20, UIFactory.Ink, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(AGENT_W, ROW_H), true, 2f);
            _ghost.gameObject.SetActive(false);
        }

        private static void SetCaster(bool pCaster)
        {
            if (LobbyPlayer.Local != null) PlayerIdentity.JoinAsCaster = pCaster;
        }

        private TeamBox BuildBox(Transform pParent, int pIndex)
        {
            TeamBox lBox = new TeamBox();
            lBox.rect = Box(pParent, "Team" + pIndex, TEAM_X, TOP, TEAM_W, HEADER_H);
            lBox.rect.gameObject.AddComponent<Image>().color = Slate;
            lBox.bar = UIFactory.Panel(lBox.rect, "Bar", LobbyPlayer.TeamColor(pIndex), new Vector2(0, 1), new Vector2(3, -HEADER_H / 2), new Vector2(6, HEADER_H));
            lBox.title = Label(lBox.rect, "TEAM " + (pIndex + 1), 20, UIFactory.Paper, 18, 13, 110, 26, true, 3f);

            int lIndex = pIndex;
            lBox.mode = MenuButton(lBox.rect, "CONTRACT", 130, 7, 390, 34, () => LobbyState.Instance?.ToggleProtectRpc(lIndex), 16);
            lBox.modeLabel = lBox.mode.GetComponentInChildren<TextMeshProUGUI>();
            lBox.remove = MenuButton(lBox.rect, "×", TEAM_W - 50, 7, 42, 34, () => LobbyState.Instance?.RemoveBoxRpc(lIndex), 22);
            lBox.drop = Box(lBox.rect, "Drop", 0, HEADER_H, TEAM_W, DROP_H).gameObject.AddComponent<Image>();
            lBox.drop.color = new Color(1, 1, 1, 0.02f);
            lBox.drop.raycastTarget = false;
            Label(lBox.drop.transform, "DROP AN AGENT HERE", 14, new Color(1, 1, 1, 0.35f), 0, 12, TEAM_W, 20, false, 3f, TextAlignmentOptions.Top);
            return lBox;
        }

        private AgentRow BuildRow(Transform pParent)
        {
            AgentRow lRow = new AgentRow();
            lRow.rect = Box(pParent, "Agent", 0, 0, AGENT_W, ROW_H - 4);
            lRow.back = lRow.rect.gameObject.AddComponent<Image>();
            lRow.back.color = new Color(1, 1, 1, 0.045f);
            lRow.drag = lRow.rect.gameObject.AddComponent<AgentRowDrag>();
            lRow.drag.Init(this, lRow.rect);

            lRow.iconButton = MenuButton(lRow.rect, "", 3, 3, 32, 32, () => OpenIconPicker(lRow), 10);
            lRow.icon = UIFactory.Panel(lRow.iconButton.transform, "Icon", UIFactory.Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));

            lRow.name = Label(lRow.rect, "", 19, UIFactory.Paper, 44, 8, 230, 24, true, 1f);
            lRow.name.overflowMode = TextOverflowModes.Ellipsis;
            lRow.state = Label(lRow.rect, "", 13, UIFactory.Muted, 0, 11, 84, 20, true, 1.5f, TextAlignmentOptions.TopRight);
            lRow.rect.gameObject.SetActive(false);
            return lRow;
        }

        // ---------------------------------------------------------------- icons

        private void BuildIconPicker()
        {
            _iconPicker = UIFactory.Stretch(_root, "IconPicker").gameObject;
            Image lDim = _iconPicker.AddComponent<Image>();
            lDim.color = new Color(0, 0, 0, 0.45f);
            Button lClose = _iconPicker.AddComponent<Button>();
            lClose.transition = Selectable.Transition.None;
            lClose.onClick.AddListener(() => _iconPicker.SetActive(false));
            RectTransform lPanel = UIFactory.Panel(_iconPicker.transform, "Panel", Navy, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(372, 420)).rectTransform;
            lPanel.GetComponent<Image>().raycastTarget = true;
            Label(lPanel, "YOUR ICON", 20, UIFactory.Paper, 24, 18, 300, 26, true, 5f);
            for (int i = 0; i < UIIcons.AGENT_COUNT; i++)
            {
                int lIcon = i;
                Button lButton = MenuButton(lPanel, "", 24 + i % 4 * 82, 60 + i / 4 * 86, 78, 78, () =>
                {
                    PlayerIdentity.LocalIcon = lIcon;
                    _iconPicker.SetActive(false);
                });
                Image lImage = UIFactory.Panel(lButton.transform, "Icon", UIFactory.Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
                lImage.sprite = UIIcons.Agent(i);
            }
            _iconPicker.SetActive(false);
        }

        private void OpenIconPicker(AgentRow pRow)
        {
            if (pRow.player == LobbyPlayer.Local) _iconPicker.SetActive(true);
        }

        // ---------------------------------------------------------------- drag and drop

        /// <summary>A row may be dragged by its own player, or by the host.</summary>
        public bool CanDrag(RectTransform pRow)
        {
            AgentRow lRow = _rows.Find(r => r.rect == pRow);
            if (lRow == null || lRow.player == null || lRow.player.Caster.Value) return false;
            return lRow.player == LobbyPlayer.Local || (LobbyState.Instance != null && LobbyState.Instance.IsServer);
        }

        public void DragStart(RectTransform pRow)
        {
            AgentRow lRow = _rows.Find(r => r.rect == pRow);
            _ghostName.text = lRow != null && lRow.player != null ? lRow.player.Name.ToUpperInvariant() : "";
            _ghost.gameObject.SetActive(true);
            _ghost.SetAsLastSibling();
        }

        public void DragMove(Vector2 pScreen)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, pScreen, null, out Vector2 lLocal)) _ghost.anchoredPosition = lLocal;
        }

        public void DragEnd(RectTransform pRow, Vector2 pScreen)
        {
            _ghost.gameObject.SetActive(false);
            AgentRow lRow = _rows.Find(r => r.rect == pRow);
            LobbyState lState = LobbyState.Instance;
            if (lRow == null || lRow.player == null || lState == null) return;
            for (int i = 0; i < lState.BoxCount.Value; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_boxes[i].rect, pScreen, null))
                {
                    lState.MovePlayerRpc(lRow.player.OwnerClientId, i);
                    return;
                }
            if (RectTransformUtility.RectangleContainsScreenPoint(_voidArea, pScreen, null)) lState.MovePlayerRpc(lRow.player.OwnerClientId, MatchRulesLogic.NO_TEAM);
        }

        // ---------------------------------------------------------------- actions

        private void ChangeMap(int pDelta)
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            lState.MapIndex.Value = Mathf.Clamp(lState.MapIndex.Value + pDelta, 0, Mathf.Max(0, GameDatabase.Maps.Count - 1));
        }

        // ---------------------------------------------------------------- refresh

        private void Update()
        {
            KeepName(_titleName);
            KeepName(_name);

            if (GameInput.PausePressed)
            {
                if (_current == Screen.Settings) CloseSettings();
                else if (_current == Screen.Join) Show(Screen.Main);
                else if (_current == Screen.Lobby) OpenSettings();
            }

            if (_joining)
            {
                if (LobbyState.Instance != null) { _joining = false; Show(Screen.Lobby); }
                else if (Time.unscaledTime > _joiningUntil) StopJoining("NO ANSWER FROM THAT ROOM");
            }
            if (_current == Screen.Join)
            {
                RefreshFriends();
                return;
            }

            bool lGone = !NetworkManagerListening() && Time.timeSinceLevelLoad > 1f && (NetSession.Instance == null || NetSession.Instance.Kind == SessionKind.None);
            if (_current == Screen.Lobby && lGone) Show(Screen.Main);
            if (_current != Screen.Lobby) return;

            LobbyState lState = LobbyState.Instance;
            bool lHost = lState != null && lState.IsServer;
            _startButton.gameObject.SetActive(lHost);
            _readyButton.gameObject.SetActive(!lHost && (LobbyPlayer.Local == null || !LobbyPlayer.Local.Caster.Value));
            if (lState == null)
            {
                _startHint.text = "CONNECTING TO THE ROOM...";
                foreach (TeamBox lBox in _boxes) lBox.rect.gameObject.SetActive(false);
                foreach (AgentRow lRow in _rows) lRow.rect.gameObject.SetActive(false);
                _addBox.gameObject.SetActive(false);
                return;
            }

            if (lHost) lState.ResolveTeams();
            RefreshMap(lState, lHost);
            int lAgents = RefreshAgents(lState, lHost, out int lWaiting, out bool lAttackers);

            bool lCaster = LobbyPlayer.Local != null && LobbyPlayer.Local.Caster.Value;
            Paint(_roleAgent, !lCaster);
            Paint(_roleCaster, lCaster);
            _invite.gameObject.SetActive(lHost && SteamReady);
            _shuffle.gameObject.SetActive(lHost && lAgents > 1);
            bool lSteamRoom = SteamLobby.Instance != null && !string.IsNullOrEmpty(SteamLobby.Instance.Code);
            _code.text = lSteamRoom ? "ROOM CODE  <color=#" + ColorUtility.ToHtmlStringRGB(UIFactory.Accent) + ">" + SteamLobby.Instance.Code + "</color>" : "";
            _copyButton.gameObject.SetActive(lSteamRoom);

            int lCasters = LobbyPlayer.CasterCount();
            _playerCount.text = lAgents + " / " + GameDatabase.Rules.maxPlayers + (lCasters > 0 ? "  ·  " + lCasters + " CASTER" + (lCasters > 1 ? "S" : "") : "");
            bool lNeedAttackers = lState.DefenderMask.Value != 0 && !lAttackers;
            _startButton.interactable = lWaiting == 0 && !lNeedAttackers && (lAgents > 0 || lCasters > 0);
            _startHint.text = !lHost ? "THE HOST STARTS THE CONTRACT"
                : lNeedAttackers ? "SOMEONE HAS TO COME FOR THE TARGET"
                : lWaiting > 0 ? "WAITING FOR " + lWaiting + " AGENT" + (lWaiting > 1 ? "S" : "") + " TO BE READY" : "";
            if (LobbyPlayer.Local != null) _readyButton.GetComponentInChildren<TextMeshProUGUI>().text = LobbyPlayer.Local.Ready.Value ? "NOT READY" : "READY";
        }

        private static void KeepName(TMP_InputField pField)
        {
            if (pField != null && !pField.isFocused && pField.text != PlayerIdentity.LocalName) pField.SetTextWithoutNotify(PlayerIdentity.LocalName);
        }

        private static void Paint(Button pButton, bool pOn) => pButton.GetComponent<ButtonFx>().Active = pOn;

        private void RefreshMap(LobbyState pState, bool pHost)
        {
            MapDefinition lMap = pState.Map;
            _mapName.text = lMap != null ? lMap.displayName.ToUpperInvariant() : "-";
            _mapDescription.text = lMap != null ? lMap.description : "";
            int lLast = Mathf.Max(0, GameDatabase.Maps.Count - 1);
            _left.gameObject.SetActive(pHost && pState.MapIndex.Value > 0);
            _right.gameObject.SetActive(pHost && pState.MapIndex.Value < lLast);

            Texture lPlan = lMap != null && lMap.planFloors != null && lMap.planFloors.Length > 0 ? lMap.planFloors[0] : null;
            Texture lShot = lMap != null ? lMap.preview : null;
            _mapPicture.texture = lShot;
            _mapPicture.color = lShot != null ? Color.white : new Color(0, 0, 0, 0);
            _planFrame.SetActive(lPlan != null);
            _mapPlan.texture = lPlan;
        }

        /// <summary>Stacks the teams, with the new-team button after the last; the rest of the agents and the casters go on the right.</summary>
        private int RefreshAgents(LobbyState pState, bool pHost, out int pWaiting, out bool pAttackers)
        {
            pWaiting = 0;
            pAttackers = false;
            int lBoxes = pState.BoxCount.Value;
            List<LobbyPlayer> lAgents = LobbyPlayer.Agents();
            float lY = TOP;
            int lRowIndex = 0;

            for (int i = 0; i < _boxes.Count; i++)
            {
                TeamBox lBox = _boxes[i];
                bool lOn = i < lBoxes;
                lBox.rect.gameObject.SetActive(lOn);
                if (!lOn) continue;

                int lCount = 0;
                foreach (LobbyPlayer lAgent in lAgents) if (lAgent.ChosenTeam.Value == i) lCount++;
                float lHeight = HEADER_H + (lCount > 0 ? lCount * ROW_H + 4f : DROP_H);
                lBox.rect.anchoredPosition = new Vector2(TEAM_X, -lY);
                lBox.rect.sizeDelta = new Vector2(TEAM_W, lHeight);
                lBox.drop.gameObject.SetActive(lCount == 0);
                lBox.remove.gameObject.SetActive(pHost);

                bool lProtects = pState.IsProtectingBox(i);
                if (!lProtects && lCount > 0) pAttackers = true;
                lBox.mode.interactable = pHost;
                lBox.modeLabel.text = lProtects ? "MODE:  PROTECT THE TARGET" : "MODE:  KILL THE TARGET";
                lBox.mode.GetComponent<ButtonFx>().Active = lProtects;

                float lRowY = HEADER_H;
                foreach (LobbyPlayer lAgent in lAgents)
                {
                    if (lAgent.ChosenTeam.Value != i || lRowIndex >= _rows.Count) continue;
                    if (FillRow(_rows[lRowIndex++], lAgent, TEAM_X + 4, lY + lRowY, TEAM_W - 8)) pWaiting++;
                    lRowY += ROW_H;
                }
                lY += lHeight + BOX_GAP;
            }

            bool lCanAdd = pHost && lBoxes < MatchRulesLogic.MAX_BOXES;
            _addBox.gameObject.SetActive(lCanAdd);
            if (lCanAdd) ((RectTransform)_addBox.transform).anchoredPosition = new Vector2(TEAM_X, -lY);

            // On the right: the agents with no team, then the casters.
            float lVoidY = TOP + 64f;
            int lSolo = 0;
            foreach (LobbyPlayer lAgent in lAgents)
            {
                if (lAgent.ChosenTeam.Value >= 0 || lRowIndex >= _rows.Count) continue;
                lSolo++;
                pAttackers = true;
                if (FillRow(_rows[lRowIndex++], lAgent, AGENT_X + 4, lVoidY, AGENT_W - 8)) pWaiting++;
                lVoidY += ROW_H;
            }
            _voidHint.gameObject.SetActive(lSolo > 0 && lBoxes == 0);
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All)
            {
                if (!lPlayer.Caster.Value || lRowIndex >= _rows.Count) continue;
                FillRow(_rows[lRowIndex++], lPlayer, AGENT_X + 4, lVoidY, AGENT_W - 8);
                lVoidY += ROW_H;
            }

            for (int i = lRowIndex; i < _rows.Count; i++) { _rows[i].player = null; _rows[i].rect.gameObject.SetActive(false); }
            return lAgents.Count;
        }

        /// <summary>One agent's line. True when the host is still waiting for this agent to be ready.</summary>
        private static bool FillRow(AgentRow pRow, LobbyPlayer pPlayer, float pX, float pY, float pWidth)
        {
            pRow.player = pPlayer;
            pRow.rect.gameObject.SetActive(true);
            pRow.rect.anchoredPosition = new Vector2(pX, -pY);
            pRow.rect.sizeDelta = new Vector2(pWidth, ROW_H - 4);
            pRow.state.rectTransform.anchoredPosition = new Vector2(pWidth - 92, -11);
            bool lMe = pPlayer == LobbyPlayer.Local;
            bool lIsHost = pPlayer.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
            bool lCaster = pPlayer.Caster.Value;
            pRow.icon.sprite = UIIcons.Agent(pPlayer.Icon.Value);
            pRow.iconButton.interactable = lMe;
            pRow.name.text = pPlayer.Name.ToUpperInvariant();
            pRow.back.color = lMe ? new Color(1, 1, 1, 0.09f) : new Color(1, 1, 1, 0.045f);
            bool lWaiting = !lIsHost && !lCaster && !pPlayer.Ready.Value;
            pRow.state.text = lCaster ? "CASTER" : lIsHost ? "HOST" : pPlayer.Ready.Value ? "READY" : "WAITING";
            pRow.state.color = lCaster ? UIFactory.Muted : lIsHost ? UIFactory.Accent : pPlayer.Ready.Value ? UIFactory.Good : UIFactory.Muted;
            return lWaiting;
        }
    }

    /// <summary>Drags an agent's line to a team, or out of one (<see cref="MenuUI.CanDrag"/> says who may).</summary>
    public class AgentRowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private MenuUI _menu;
        private RectTransform _row;
        private bool _dragging;

        public void Init(MenuUI pMenu, RectTransform pRow)
        {
            _menu = pMenu;
            _row = pRow;
        }

        public void OnBeginDrag(PointerEventData pData)
        {
            _dragging = _menu.CanDrag(_row);
            if (!_dragging) return;
            _menu.DragStart(_row);
            _menu.DragMove(pData.position);
        }

        public void OnDrag(PointerEventData pData)
        {
            if (_dragging) _menu.DragMove(pData.position);
        }

        public void OnEndDrag(PointerEventData pData)
        {
            if (_dragging) _menu.DragEnd(_row, pData.position);
            _dragging = false;
        }
    }
}
