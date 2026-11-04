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
    /// The whole front of the game in two screens. <b>Title</b>: PLAY, and small icons for the options
    /// and quit. <b>The room</b>: PLAY opens it at once, and everything happens there - the map on the
    /// left, the agents on the right in team boxes (a + adds a box, agents are dragged between them,
    /// a box can protect the target), a role and an icon on each agent's own line, invites under the
    /// agents. There is no mode to choose: the boxes are the mode (see <see cref="LobbyState.ResolveTeams"/>).
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private enum Screen { Main, Lobby, Settings }

        private const string VERSION = "0.8.1";
        private static readonly Vector2 TL = new Vector2(0, 1);
        private static readonly Color Navy = new Color(0.1f, 0.11f, 0.17f, 1f);
        private static readonly Color Slate = new Color(1f, 1f, 1f, 0.05f);

        private const float BOX_W = 362f, BOX_GAP = 16f, BOX_TOP = 112f, BOX_BOTTOM = 820f, HEADER_H = 40f, ROW_H = 42f, DROP_H = 44f;
        private const int MAX_ROWS = 12;

        private class TeamBox
        {
            public RectTransform rect;
            public Image bar, drop;
            public TextMeshProUGUI title;
            public Button protect, remove;
            public Image protectIcon;
            public TextMeshProUGUI protectLabel;
        }

        private class AgentRow
        {
            public RectTransform rect;
            public Image back, icon;
            public Button iconButton, role;
            public TextMeshProUGUI name, state, roleLabel;
            public LobbyPlayer player;
            public AgentRowDrag drag;
        }

        private RectTransform _root;
        private GameObject _main, _lobby, _settings, _iconPicker;
        private TextMeshProUGUI _status, _mapName, _mapDescription, _playerCount, _startHint, _code, _viewLabel;
        private TMP_InputField _name, _joinField;
        private Button _startButton, _readyButton, _copyButton, _invite, _shuffle, _addBox, _left, _right, _viewSwap;
        private Toggle _proximity;
        private RawImage _mapPicture, _mapInset;
        private GameObject _insetFrame;
        private bool _planBig;
        private Screen _current;
        private RectTransform _casterBox;
        private readonly List<TeamBox> _boxes = new List<TeamBox>();
        private readonly List<AgentRow> _rows = new List<AgentRow>();
        private RectTransform _ghost;
        private TextMeshProUGUI _ghostName;
        private float _joiningUntil;

        private void Start()
        {
            GameInput.GameplayEnabled = false;
            GameInput.LockCursor(false);
            Hud.Remove();
            CasterView.Stop();
            Spectator.Stop();
            HumanRagdoll.ClearAll();

            // A camera only so the screen has a known clear colour: the backdrop is a still image.
            Camera lCamera = new GameObject("MenuCamera").AddComponent<Camera>();
            lCamera.transform.SetParent(transform, false);
            lCamera.clearFlags = CameraClearFlags.SolidColor;
            lCamera.backgroundColor = Navy;
            lCamera.cullingMask = 0;

            Canvas lCanvas = UIFactory.Canvas("MenuCanvas", 20);
            lCanvas.transform.SetParent(transform, false);
            _root = (RectTransform)lCanvas.transform;

            RawImage lBackdrop = UIFactory.Stretch(_root, "Backdrop").gameObject.AddComponent<RawImage>();
            Texture2D lArt = Resources.Load<Texture2D>("Title/TitleArt");
            lBackdrop.texture = lArt;
            lBackdrop.color = lArt != null ? Color.white : Navy;
            lBackdrop.raycastTarget = false;

            BuildMain();
            BuildLobby();
            BuildSettings();
            BuildIconPicker();
            _status = UIFactory.Title(_root, "Status", "", 16, UIFactory.Muted, TextAlignmentOptions.Right, new Vector2(1, 0), new Vector2(-70, 14), new Vector2(900, 24), false, 3f);
            UIFactory.Title(_root, "Version", "v" + VERSION, 13, new Color(1, 1, 1, 0.3f), TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(70, 8), new Vector2(200, 20), false, 3f);

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
        private static bool SteamReady => SteamLobby.Instance != null && SteamLobby.Instance.IsReady;

        private void Show(Screen pScreen)
        {
            _current = pScreen;
            _main.SetActive(pScreen == Screen.Main);
            _lobby.SetActive(pScreen == Screen.Lobby);
            _settings.SetActive(pScreen == Screen.Settings);
            GameObject lShown = pScreen switch { Screen.Main => _main, Screen.Lobby => _lobby, _ => _settings };
            UIFactory.FadeIn(lShown, 0.25f, Vector2.zero);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
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

        private static Button SmallButton(Transform pParent, string pLabel, float pX, float pY, float pWidth, float pHeight, System.Action pAction, float pFont = 18f, bool pPrimary = false)
        {
            Button lButton = UIFactory.Button(pParent, pLabel, TL, new Vector2(pX, -pY), new Vector2(pWidth, pHeight), pPrimary ? UIFactory.Accent : UIFactory.Accent2, pAction, pFont);
            ((RectTransform)lButton.transform).pivot = TL;
            ((RectTransform)lButton.transform).anchoredPosition = new Vector2(pX, -pY);
            lButton.GetComponent<Image>().color = pPrimary ? UIFactory.Accent : Slate;
            TextMeshProUGUI lText = lButton.GetComponentInChildren<TextMeshProUGUI>();
            lText.alignment = TextAlignmentOptions.Center;
            lText.rectTransform.anchoredPosition = Vector2.zero;
            lText.rectTransform.sizeDelta = new Vector2(pWidth, pHeight);
            return lButton;
        }

        private static Button IconButton(Transform pParent, Sprite pIcon, float pX, float pY, float pSize, System.Action pAction)
        {
            Button lButton = SmallButton(pParent, "", pX, pY, pSize, pSize, pAction);
            Image lIcon = UIFactory.Panel(lButton.transform, "Icon", UIFactory.Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(pSize * 0.55f, pSize * 0.55f));
            lIcon.sprite = pIcon;
            return lButton;
        }

        // ---------------------------------------------------------------- title

        private void BuildMain()
        {
            _main = UIFactory.Stretch(_root, "Main").gameObject;
            Transform p = _main.transform;
            Label(p, "GOOBYR", 170, UIFactory.Paper, 104, 250, 1100, 190, true, 3f);
            Button lPlay = SmallButton(p, "PLAY", 110, 470, 440, 104, Play, 54, true);
            lPlay.GetComponentInChildren<TextMeshProUGUI>().characterSpacing = 3f;
            IconButton(p, UIIcons.Options, 110, 960, 60, () => Show(Screen.Settings));
            IconButton(p, UIIcons.Exit, 182, 960, 60, Application.Quit);
        }

        /// <summary>PLAY: a room right away, on Steam when it runs (invites work at once), else on the local network.</summary>
        private void Play()
        {
            if (SteamReady) NetSession.Instance.HostSteam(false);
            else NetSession.Instance.HostLocal();
            Show(Screen.Lobby);
        }

        private void BuildSettings()
        {
            _settings = UIFactory.Stretch(_root, "Settings").gameObject;
            Transform p = _settings.transform;
            _settings.AddComponent<Image>().color = new Color(Navy.r, Navy.g, Navy.b, 0.96f);
            Label(p, "OPTIONS", 72, UIFactory.Paper, 104, 90, 1100, 90, true, 8f);
            SettingsPanel.Build(p, TL, new Vector2(110 + 450, -230), 900f);
            SmallButton(p, "BACK", 110, 960, 240, 56, () => Show(Screen.Main), 24);
        }

        // ---------------------------------------------------------------- the room

        private void BuildLobby()
        {
            _lobby = UIFactory.Stretch(_root, "Lobby").gameObject;
            Transform p = _lobby.transform;
            _lobby.AddComponent<Image>().color = new Color(Navy.r, Navy.g, Navy.b, 0.97f);

            // Left: the map, as a picture with its plan beside it (a click swaps the two).
            RectTransform lFrame = Box(p, "MapFrame", 70, 60, 1000, 563);
            lFrame.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.055f, 0.09f, 1f);
            _mapPicture = UIFactory.Stretch(lFrame, "Picture").gameObject.AddComponent<RawImage>();
            _mapPicture.raycastTarget = false;
            _insetFrame = Box(lFrame, "Inset", 1000 - 266, 563 - 266, 256, 256).gameObject;
            _insetFrame.AddComponent<Image>().color = new Color(0.05f, 0.055f, 0.09f, 0.92f);
            _mapInset = Box(_insetFrame.transform, "Plan", 4, 4, 248, 248).gameObject.AddComponent<RawImage>();
            _mapInset.raycastTarget = false;
            _viewSwap = _insetFrame.AddComponent<Button>();
            _viewSwap.transition = Selectable.Transition.None;
            _viewSwap.onClick.AddListener(() => _planBig = !_planBig);
            _viewLabel = Label(lFrame, "", 13, UIFactory.Muted, 14, 563 - 26, 400, 20, true, 3f);
            _left = SmallButton(lFrame, "‹", 0, 563 / 2 - 60, 56, 120, () => ChangeMap(-1), 48);
            _right = SmallButton(lFrame, "›", 1000 - 56, 563 / 2 - 60, 56, 120, () => ChangeMap(1), 48);

            _mapName = Label(p, "", 44, UIFactory.Paper, 70, 640, 1000, 54, true, 5f);
            _mapDescription = UIFactory.Text(p, "Description", "", 21, UIFactory.Muted, TextAlignmentOptions.TopLeft, TL, Vector2.zero, new Vector2(1000, 84));
            _mapDescription.rectTransform.pivot = TL;
            _mapDescription.rectTransform.anchoredPosition = new Vector2(70, -696);

            // Left, under the map: who you are, joining someone else's room, the one global option.
            Label(p, "AGENT NAME", 14, UIFactory.Muted, 70, 800, 360, 20, true, 4f);
            _name = UIFactory.Input(p, "YOUR NAME", TL, Vector2.zero, new Vector2(340, 52));
            ((RectTransform)_name.transform).pivot = TL;
            ((RectTransform)_name.transform).anchoredPosition = new Vector2(70, -826);
            _name.characterLimit = 24;
            _name.text = PlayerIdentity.LocalName;
            _name.onEndEdit.AddListener(v => PlayerIdentity.LocalName = v);

            Label(p, "JOIN A ROOM  ·  CODE OR IP", 14, UIFactory.Muted, 440, 800, 460, 20, true, 4f);
            _joinField = UIFactory.Input(p, "ABC-DEF", TL, Vector2.zero, new Vector2(320, 52));
            ((RectTransform)_joinField.transform).pivot = TL;
            ((RectTransform)_joinField.transform).anchoredPosition = new Vector2(440, -826);
            _joinField.characterLimit = 40;
            _joinField.onValidateInput = (pText, pIndex, pChar) => char.IsLetterOrDigit(pChar) || pChar == '-' || pChar == '.' ? char.ToUpperInvariant(pChar) : '\0';
            _joinField.onSubmit.AddListener(_ => JoinOther());
            SmallButton(p, "JOIN", 770, 826, 120, 52, JoinOther, 22);

            RectTransform lOptions = UIWidgets.List(p, "Options", TL, new Vector2(70 + 300, -890), 600f);
            _proximity = UIWidgets.Switch(lOptions, "Proximity voice chat", true, v =>
            {
                LobbyState lState = LobbyState.Instance;
                if (lState != null && lState.IsServer) lState.ProximityChat.Value = v;
            });

            SmallButton(p, "LEAVE", 70, 980, 200, 56, () => { NetSession.Instance.Leave(); Show(Screen.Main); }, 22);
            IconButton(p, UIIcons.Options, 282, 980, 56, () => Show(Screen.Settings));

            // Right: the agents, in their boxes.
            Label(p, "AGENTS", 30, UIFactory.Paper, 1110, 60, 400, 38, true, 6f);
            _playerCount = Label(p, "", 16, UIFactory.Muted, 1110, 70, 740, 24, true, 4f, TextAlignmentOptions.TopRight);
            for (int i = 0; i < MatchRulesLogic.MAX_TEAMS; i++) _boxes.Add(BuildBox(p, i));
            _casterBox = Box(p, "Casters", 1110, BOX_TOP, BOX_W, HEADER_H);
            _casterBox.gameObject.AddComponent<Image>().color = Slate;
            Label(_casterBox, "CASTERS", 18, UIFactory.Muted, 14, 10, 300, 24, true, 4f);
            _addBox = SmallButton(p, "+  NEW TEAM", 1110, BOX_TOP, BOX_W, HEADER_H, () => LobbyState.Instance?.AddBoxRpc(), 20);
            for (int i = 0; i < MAX_ROWS; i++) _rows.Add(BuildRow(p));

            // Under the agents: inviting and shuffling belong with them.
            _invite = SmallButton(p, "INVITE FRIENDS", 1110, 836, 200, 48, () => SteamLobby.Instance?.Invite(), 18);
            _shuffle = SmallButton(p, "SHUFFLE", 1320, 836, 130, 48, () => LobbyState.Instance?.ShuffleRpc(), 18);
            _code = Label(p, "", 18, UIFactory.Paper, 1460, 850, 300, 26, true, 4f, TextAlignmentOptions.TopRight);
            _copyButton = SmallButton(p, "COPY", 1770, 836, 80, 48, () =>
            {
                if (SteamLobby.Instance != null) GUIUtility.systemCopyBuffer = SteamLobby.Instance.Code;
                HandleStatus("CODE COPIED");
            }, 16);

            _startHint = Label(p, "", 16, UIFactory.Muted, 1110, 900, 740, 24, false, 2f, TextAlignmentOptions.TopRight);
            _startButton = SmallButton(p, "START CONTRACT", 1110, 930, 740, 106, () => LobbyState.Instance?.StartMatch(), 40, true);
            _readyButton = SmallButton(p, "READY", 1110, 930, 740, 106, () => { if (LobbyPlayer.Local != null) LobbyPlayer.Local.Ready.Value = !LobbyPlayer.Local.Ready.Value; }, 40, true);

            // What follows the pointer while an agent is dragged.
            _ghost = UIFactory.Panel(p, "Ghost", new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BOX_W, ROW_H)).rectTransform;
            _ghostName = UIFactory.Title(_ghost, "Name", "", 20, UIFactory.Ink, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BOX_W, ROW_H), true, 2f);
            _ghost.gameObject.SetActive(false);
        }

        private TeamBox BuildBox(Transform pParent, int pIndex)
        {
            TeamBox lBox = new TeamBox();
            lBox.rect = Box(pParent, "Team" + pIndex, 1110, BOX_TOP, BOX_W, HEADER_H);
            lBox.rect.gameObject.AddComponent<Image>().color = Slate;
            lBox.bar = UIFactory.Panel(lBox.rect, "Bar", LobbyPlayer.TeamColor(pIndex), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(BOX_W, 4));
            lBox.title = Label(lBox.rect, "TEAM " + (pIndex + 1), 18, UIFactory.Paper, 14, 12, 90, 24, true, 3f);

            int lIndex = pIndex;
            lBox.protect = SmallButton(lBox.rect, "", 100, 6, 198, 30, () => LobbyState.Instance?.ToggleProtectRpc(lIndex), 14);
            lBox.protectIcon = UIFactory.Panel(lBox.protect.transform, "Icon", UIFactory.Muted, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(16, 16));
            lBox.protectIcon.sprite = UIIcons.Agent(UIIcons.SHIELD);
            lBox.protectLabel = Label(lBox.protect.transform, "PROTECT THE TARGET", 13, UIFactory.Muted, 30, 8, 170, 18, true, 1.5f);
            lBox.remove = SmallButton(lBox.rect, "×", BOX_W - 38, 6, 30, 30, () => LobbyState.Instance?.RemoveBoxRpc(lIndex), 22);
            lBox.drop = Box(lBox.rect, "Drop", 8, HEADER_H, BOX_W - 16, DROP_H - 8).gameObject.AddComponent<Image>();
            lBox.drop.color = new Color(1, 1, 1, 0.025f);
            lBox.drop.raycastTarget = false;
            Label(lBox.drop.transform, "DROP AN AGENT HERE", 13, new Color(1, 1, 1, 0.3f), 0, 10, BOX_W - 16, 20, false, 3f, TextAlignmentOptions.Top);
            return lBox;
        }

        private AgentRow BuildRow(Transform pParent)
        {
            AgentRow lRow = new AgentRow();
            lRow.rect = Box(pParent, "Agent", 0, 0, BOX_W - 8, ROW_H - 4);
            lRow.back = lRow.rect.gameObject.AddComponent<Image>();
            lRow.back.color = new Color(1, 1, 1, 0.045f);
            lRow.drag = lRow.rect.gameObject.AddComponent<AgentRowDrag>();
            lRow.drag.Init(this, lRow.rect);

            lRow.iconButton = SmallButton(lRow.rect, "", 3, 3, 32, 32, () => OpenIconPicker(lRow), 10);
            lRow.icon = UIFactory.Panel(lRow.iconButton.transform, "Icon", UIFactory.Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));

            lRow.name = Label(lRow.rect, "", 19, UIFactory.Paper, 44, 8, 150, 24, true, 1f);
            lRow.name.overflowMode = TextOverflowModes.Ellipsis;
            lRow.state = Label(lRow.rect, "", 13, UIFactory.Muted, 190, 11, 76, 20, true, 1.5f, TextAlignmentOptions.TopRight);
            lRow.role = SmallButton(lRow.rect, "", BOX_W - 8 - 80, 4, 76, 30, ToggleCaster, 13);
            lRow.roleLabel = lRow.role.GetComponentInChildren<TextMeshProUGUI>();
            lRow.rect.gameObject.SetActive(false);
            return lRow;
        }

        private static void ToggleCaster()
        {
            if (LobbyPlayer.Local == null) return;
            PlayerIdentity.JoinAsCaster = !LobbyPlayer.Local.Caster.Value;
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
                IconButton(lPanel, UIIcons.Agent(i), 24 + i % 4 * 82, 60 + i / 4 * 86, 78, () =>
                {
                    PlayerIdentity.LocalIcon = lIcon;
                    _iconPicker.SetActive(false);
                });
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
        }

        // ---------------------------------------------------------------- actions

        private void ChangeMap(int pDelta)
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            lState.MapIndex.Value = Mathf.Clamp(lState.MapIndex.Value + pDelta, 0, Mathf.Max(0, GameDatabase.Maps.Count - 1));
            _planBig = false;
        }

        /// <summary>Someone else's room: this one closes first, then we dial theirs (a code on Steam, an IP on the network).</summary>
        private void JoinOther()
        {
            string lWhere = _joinField.text.Trim();
            if (lWhere.Length == 0) return;
            _joiningUntil = Time.unscaledTime + 12f;
            NetSession.Instance.Leave();
            StartCoroutine(JoinAfterLeaving(lWhere));
        }

        private System.Collections.IEnumerator JoinAfterLeaving(string pWhere)
        {
            while (NetSession.Instance.Network.ShutdownInProgress || NetSession.Instance.Network.IsListening) yield return null;
            if (pWhere.Contains(".")) NetSession.Instance.JoinLocal(pWhere);
            else NetSession.Instance.JoinSteamCode(pWhere);
        }

        // ---------------------------------------------------------------- refresh

        private void Update()
        {
            if (_name != null && !_name.isFocused && !PlayerIdentity.HasCustomName && _name.text != PlayerIdentity.LocalName) _name.SetTextWithoutNotify(PlayerIdentity.LocalName);
            bool lGone = !NetworkManagerListening() && Time.timeSinceLevelLoad > 1f && (NetSession.Instance == null || NetSession.Instance.Kind == SessionKind.None);
            if (_current == Screen.Lobby && lGone && Time.unscaledTime > _joiningUntil) Show(Screen.Main);
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
                _casterBox.gameObject.SetActive(false);
                _addBox.gameObject.SetActive(false);
                return;
            }

            if (lHost) lState.ResolveTeams();
            RefreshMap(lState, lHost);
            int lAgents = RefreshAgents(lState, lHost, out int lWaiting, out bool lAttackers);

            _proximity.SetIsOnWithoutNotify(lState.ProximityChat.Value);
            _proximity.interactable = lHost;
            _invite.gameObject.SetActive(lHost && SteamReady);
            _shuffle.gameObject.SetActive(lHost && lState.BoxCount.Value > 1);
            bool lSteamRoom = SteamLobby.Instance != null && !string.IsNullOrEmpty(SteamLobby.Instance.Code);
            _code.text = lSteamRoom ? "ROOM <color=#" + ColorUtility.ToHtmlStringRGB(UIFactory.Accent) + ">" + SteamLobby.Instance.Code + "</color>" : "";
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
            bool lPlanBig = lShot == null || _planBig;
            _insetFrame.SetActive(lShot != null && lPlan != null);
            _mapInset.texture = _planBig ? lShot : lPlan;
            _mapInset.uvRect = new Rect(0, 0, 1, 1);
            // The plan is square: it fits the frame's height, whole; the screenshot fills the frame.
            RectTransform lPicture = _mapPicture.rectTransform;
            _mapPicture.texture = lPlanBig ? lPlan : lShot;
            _mapPicture.color = _mapPicture.texture != null ? Color.white : new Color(0, 0, 0, 0);
            lPicture.anchorMin = lPlanBig ? new Vector2(0.5f, 0.5f) : Vector2.zero;
            lPicture.anchorMax = lPlanBig ? new Vector2(0.5f, 0.5f) : Vector2.one;
            lPicture.sizeDelta = lPlanBig ? new Vector2(563, 563) : Vector2.zero;
            lPicture.anchoredPosition = Vector2.zero;
            _viewLabel.text = lShot != null ? (_planBig ? "CLICK THE PICTURE: VIEW" : "CLICK THE PLAN: FULL MAP") : "";
        }

        /// <summary>Lays out the boxes and fills them: two columns, each box as tall as its agents, the next box in the shorter column.</summary>
        private int RefreshAgents(LobbyState pState, bool pHost, out int pWaiting, out bool pAttackers)
        {
            pWaiting = 0;
            pAttackers = false;
            int lBoxes = pState.BoxCount.Value;
            List<LobbyPlayer> lAgents = LobbyPlayer.Agents();
            float[] lColumn = { BOX_TOP, BOX_TOP };
            int lRowIndex = 0;

            for (int i = 0; i < _boxes.Count; i++)
            {
                TeamBox lBox = _boxes[i];
                bool lOn = i < lBoxes;
                lBox.rect.gameObject.SetActive(lOn);
                if (!lOn) continue;

                int lCount = 0;
                foreach (LobbyPlayer lAgent in lAgents) if (lAgent.Team.Value == i) lCount++;
                float lHeight = HEADER_H + (lCount > 0 ? lCount * ROW_H + 4f : DROP_H);
                int lCol = lColumn[1] < lColumn[0] ? 1 : 0;
                float lX = 1110 + lCol * (BOX_W + BOX_GAP), lY = lColumn[lCol];
                lBox.rect.anchoredPosition = new Vector2(lX, -lY);
                lBox.rect.sizeDelta = new Vector2(BOX_W, lHeight);
                lColumn[lCol] += lHeight + 12f;
                lBox.drop.gameObject.SetActive(lCount == 0);
                lBox.remove.gameObject.SetActive(pHost && lCount == 0 && lBoxes > 1);

                bool lProtects = pState.IsProtector(i);
                if (!lProtects && lCount > 0) pAttackers = true;
                lBox.protect.gameObject.SetActive(pHost || lProtects);
                lBox.protect.interactable = pHost;
                lBox.protectIcon.color = lProtects ? UIFactory.Accent : UIFactory.Muted;
                lBox.protectLabel.color = lProtects ? UIFactory.Accent : UIFactory.Muted;

                float lRowY = HEADER_H;
                foreach (LobbyPlayer lAgent in lAgents)
                {
                    if (lAgent.Team.Value != i || lRowIndex >= _rows.Count) continue;
                    if (FillRow(_rows[lRowIndex++], lAgent, lX + 4, lY + lRowY, pHost)) pWaiting++;
                    lRowY += ROW_H;
                }
            }

            // The + comes after the last box, in the shorter column.
            bool lCanAdd = pHost && lBoxes < MatchRulesLogic.MAX_TEAMS;
            _addBox.gameObject.SetActive(lCanAdd);
            if (lCanAdd)
            {
                int lCol = lColumn[1] < lColumn[0] ? 1 : 0;
                ((RectTransform)_addBox.transform).anchoredPosition = new Vector2(1110 + lCol * (BOX_W + BOX_GAP), -lColumn[lCol]);
                lColumn[lCol] += HEADER_H + 12f;
            }

            // Casters watch: no team, a box of their own in the shorter column.
            int lCasters = LobbyPlayer.CasterCount();
            _casterBox.gameObject.SetActive(lCasters > 0);
            if (lCasters > 0)
            {
                int lCol = lColumn[1] < lColumn[0] ? 1 : 0;
                float lX = 1110 + lCol * (BOX_W + BOX_GAP), lY = lColumn[lCol];
                _casterBox.anchoredPosition = new Vector2(lX, -lY);
                _casterBox.sizeDelta = new Vector2(BOX_W, HEADER_H + lCasters * ROW_H + 4f);
                float lRowY = HEADER_H;
                foreach (LobbyPlayer lPlayer in LobbyPlayer.All)
                {
                    if (!lPlayer.Caster.Value || lRowIndex >= _rows.Count) continue;
                    FillRow(_rows[lRowIndex++], lPlayer, lX + 4, lY + lRowY, pHost);
                    lRowY += ROW_H;
                }
            }

            for (int i = lRowIndex; i < _rows.Count; i++) { _rows[i].player = null; _rows[i].rect.gameObject.SetActive(false); }
            return lAgents.Count;
        }

        /// <summary>One agent's line. True when the host is still waiting for this agent to be ready.</summary>
        private static bool FillRow(AgentRow pRow, LobbyPlayer pPlayer, float pX, float pY, bool pHost)
        {
            pRow.player = pPlayer;
            pRow.rect.gameObject.SetActive(true);
            pRow.rect.anchoredPosition = new Vector2(pX, -pY);
            bool lMe = pPlayer == LobbyPlayer.Local;
            bool lIsHost = pPlayer.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
            bool lCaster = pPlayer.Caster.Value;
            pRow.icon.sprite = UIIcons.Agent(pPlayer.Icon.Value);
            pRow.iconButton.interactable = lMe;
            pRow.name.text = pPlayer.Name.ToUpperInvariant();
            pRow.back.color = lMe ? new Color(1, 1, 1, 0.09f) : new Color(1, 1, 1, 0.045f);
            bool lWaiting = !lIsHost && !lCaster && !pPlayer.Ready.Value;
            pRow.state.text = lIsHost ? "HOST" : lCaster ? "" : pPlayer.Ready.Value ? "READY" : "WAITING";
            pRow.state.color = lIsHost ? UIFactory.Accent : pPlayer.Ready.Value ? UIFactory.Good : UIFactory.Muted;
            // Your role is on your own line; others' lines only say what they are when they cast.
            pRow.role.gameObject.SetActive(lMe || lCaster);
            pRow.role.interactable = lMe;
            pRow.roleLabel.text = lCaster ? "CASTER" : "AGENT";
            return lWaiting;
        }
    }

    /// <summary>Drags an agent's line to another team box (<see cref="MenuUI.CanDrag"/> says who may).</summary>
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
