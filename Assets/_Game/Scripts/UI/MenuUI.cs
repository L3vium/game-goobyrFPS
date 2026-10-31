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
    /// The title scene's screens, over the key art (<see cref="TitleDiorama"/>). The game is played
    /// between friends: one creates a room and shares its code, the others join with it.
    /// <list type="bullet">
    /// <item><b>Main</b>: a vertical list - create a room, join a room, solo, options, quit - each
    /// with its icon and one line saying what it does; the agent's name bottom right.</item>
    /// <item><b>Join</b>: the room code, big; the direct IP (LAN) folded under it.</item>
    /// <item><b>Briefing</b>: the map card (its plan as the picture), the mode as three explicit cards,
    /// teams and options as rows, the agents in aligned columns, the room code with a copy button,
    /// READY or START.</item>
    /// </list>
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private enum Screen { Main, Join, Lobby, Settings }

        private const string VERSION = "0.4.0";
        private static readonly Vector2 TL = new Vector2(0, 1);

        private RectTransform _root;
        private GameObject _main, _join, _lobby, _settings;
        private TextMeshProUGUI _status, _menuHint, _code, _mapName, _mapDescription, _playerCount, _startHint;
        private TMP_InputField _joinCode, _joinIp, _name;
        private Button _startButton, _readyButton, _copyButton, _firstMain, _shuffle, _invite;
        private Toggle _casterSwitch;
        private RawImage _mapPicture;
        private GameObject _mapArrows, _teamsRow, _teamPicker;
        private System.Action<string> _setTeamCount;
        private readonly List<(Button button, Image back, Image bar, MatchMode mode)> _modeCards = new List<(Button, Image, Image, MatchMode)>();
        private readonly List<(RectTransform row, Image bar, TextMeshProUGUI name, TextMeshProUGUI team, TextMeshProUGUI state)> _agents = new List<(RectTransform, Image, TextMeshProUGUI, TextMeshProUGUI, TextMeshProUGUI)>();
        private Screen _current;

        private void Start()
        {
            GameInput.GameplayEnabled = false;
            GameInput.LockCursor(false);
            Hud.Remove();
            CasterView.Stop();
            Spectator.Stop();
            GoobRagdoll.ClearAll();

            Canvas lCanvas = UIFactory.Canvas("MenuCanvas", 20);
            lCanvas.transform.SetParent(transform, false);
            _root = (RectTransform)lCanvas.transform;

            // Cinematic framing: dark gradient on the left where the text sits.
            Image lShade = UIFactory.Stretch(_root, "Shade").gameObject.AddComponent<Image>();
            lShade.sprite = GradientSprite();
            lShade.color = new Color(0, 0, 0, 0.88f);
            lShade.raycastTarget = false;

            BuildMain();
            BuildJoin();
            BuildLobby();
            BuildSettings();
            _status = UIFactory.Title(_root, "Status", "", 18, UIFactory.Muted, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-110, -40), new Vector2(1000, 30), false, 3f);
            UIFactory.Title(_root, "Version", "v" + VERSION, 13, new Color(1, 1, 1, 0.3f), TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(110, 8), new Vector2(200, 20), false, 3f);

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
            _join.SetActive(pScreen == Screen.Join);
            _lobby.SetActive(pScreen == Screen.Lobby);
            _settings.SetActive(pScreen == Screen.Settings);
            GameObject lShown = pScreen switch { Screen.Main => _main, Screen.Join => _join, Screen.Lobby => _lobby, _ => _settings };
            UIFactory.FadeIn(lShown, 0.3f, new Vector2(-24, 0));
            GameObject lFirst = pScreen == Screen.Main && _firstMain != null ? _firstMain.gameObject : lShown.GetComponentInChildren<Selectable>()?.gameObject;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(lFirst);
        }

        private static Sprite GradientSprite()
        {
            Texture2D lTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 256; x++)
            {
                float t = x / 255f;
                lTexture.SetPixel(x, 0, new Color(1, 1, 1, Mathf.Clamp01(1.05f - t * 1.5f)));
            }
            lTexture.Apply();
            return Sprite.Create(lTexture, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A rect anchored and pivoted on the top-left corner of its parent (screen layout in plain coordinates).</summary>
        private static RectTransform Box(Transform pParent, string pName, float pX, float pY, float pWidth, float pHeight) =>
            UIFactory.Rect(pParent, pName, TL, TL, TL, new Vector2(pX, -pY), new Vector2(pWidth, pHeight));

        private static TextMeshProUGUI Label(Transform pParent, string pText, float pSize, Color pColor, float pX, float pY, float pWidth, float pHeight, bool pBold = false, float pTracking = 3f, TextAlignmentOptions pAlign = TextAlignmentOptions.TopLeft)
        {
            TextMeshProUGUI lText = UIFactory.Title(pParent, "Label", pText, pSize, pColor, pAlign, TL, Vector2.zero, new Vector2(pWidth, pHeight), pBold, pTracking);
            lText.rectTransform.pivot = TL;
            lText.rectTransform.anchoredPosition = new Vector2(pX, -pY);
            return lText;
        }

        /// <summary>A section header: small caps label over a short amber rule.</summary>
        private static void Header(Transform pParent, string pText, float pX, float pY)
        {
            Label(pParent, pText, 15, UIFactory.Muted, pX, pY, 500, 22, true, 5f);
            UIFactory.Rule(pParent, TL, new Vector2(pX, -pY - 28), 28, false, UIFactory.Accent, 2f);
        }

        // ---------------------------------------------------------------- main

        private void BuildMain()
        {
            _main = UIFactory.Stretch(_root, "Main").gameObject;
            Transform p = _main.transform;

            Label(p, "STEALTH CONTRACTS  ·  2 TO 8 FRIENDS", 16, UIFactory.Accent, 110, 120, 900, 24, true, 6f);
            Label(p, "GOOBYR FPS", 104, UIFactory.Paper, 104, 148, 1100, 120, true, 4f);

            // The menu: one entry per thing to do, each saying what it does.
            (string label, string hint, Sprite icon, System.Action action)[] lEntries =
            {
                ("CREATE A ROOM", "Open a room and give its code to your friends.", UIIcons.Create, CreateRoom),
                ("JOIN A ROOM", "Type the code a friend gives you.", UIIcons.Code, () => Show(Screen.Join)),
                ("SOLO", "Play a contract alone against the AI.", UIIcons.Person, () => { NetSession.Instance.HostLocal(); Show(Screen.Lobby); }),
                ("OPTIONS", "Controls, video, audio and voice chat.", UIIcons.Options, () => Show(Screen.Settings)),
                ("QUIT", "Back to the desktop.", UIIcons.Exit, Application.Quit)
            };
            for (int i = 0; i < lEntries.Length; i++)
            {
                (string lLabel, string lHint, Sprite lIcon, System.Action lAction) = lEntries[i];
                Button lButton = MenuEntry(p, lLabel, lIcon, 110, 360 + i * 76, i == 0, lAction);
                lButton.gameObject.AddComponent<HoverHint>().Init(lHint, pText => _menuHint.text = pText);
                if (i == 0) _firstMain = lButton;
            }
            _menuHint = Label(p, "", 20, UIFactory.Muted, 110, 360 + lEntries.Length * 76 + 20, 640, 30, false, 2f);

            // Footer: the controls on the left, the agent's name on the right.
            UIFactory.Rule(p, new Vector2(0.5f, 0), new Vector2(0, 74), 1700, false, new Color(1, 1, 1, 0.12f), 1f);
            UIFactory.Title(p, "Hints", "ENTER / A  SELECT      ESC / B  BACK", 16, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(110, 32), new Vector2(700, 24), false, 3f);
            UIFactory.Title(p, "AgentLabel", "AGENT NAME", 14, UIFactory.Accent, TextAlignmentOptions.Right, new Vector2(1, 0), new Vector2(-110, 84), new Vector2(360, 22), true, 4f);
            _name = UIFactory.Input(p, "YOUR NAME", new Vector2(1, 0), new Vector2(-110, 24), new Vector2(360, 52));
            _name.characterLimit = 24;
            _name.text = PlayerIdentity.LocalName;
            _name.onEndEdit.AddListener(v => PlayerIdentity.LocalName = v);
        }

        /// <summary>A main menu entry: icon, label; the first one is filled amber.</summary>
        private static Button MenuEntry(Transform pParent, string pLabel, Sprite pIcon, float pX, float pY, bool pPrimary, System.Action pAction)
        {
            Button lButton = UIFactory.Button(pParent, pLabel, TL, new Vector2(pX, -pY), new Vector2(460, 64), pPrimary ? UIFactory.Accent : UIFactory.Accent2, pAction, 30);
            TextMeshProUGUI lText = lButton.GetComponentInChildren<TextMeshProUGUI>();
            // Room for the icon (a margin: the hover slide moves the label's position, not its margin).
            lText.alignment = TextAlignmentOptions.Left;
            lText.margin = new Vector4(pPrimary ? 56 : 46, 0, 0, 0);
            Image lIcon = UIFactory.Panel(lButton.transform, "Icon", pPrimary ? UIFactory.Ink : UIFactory.Paper, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(30, 30));
            lIcon.sprite = pIcon;
            lIcon.raycastTarget = false;
            return lButton;
        }

        /// <summary>A room on Steam (public but found only by its code); LAN when Steam is not running.</summary>
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
            Transform p = _join.transform;
            Label(p, "JOIN A ROOM", 88, UIFactory.Paper, 104, 150, 1100, 110, true, 4f);
            Label(p, "Ask the friend who created the room for its six-letter code.", 22, UIFactory.Muted, 110, 270, 900, 30, false, 1f);

            Header(p, "ROOM CODE", 110, 340);
            _joinCode = UIFactory.Input(p, "ABC-DEF", TL, new Vector2(110, -390), new Vector2(420, 72));
            _joinCode.characterLimit = 7;
            _joinCode.onValidateInput = (pText, pIndex, pChar) => char.IsLetter(pChar) || pChar == '-' ? char.ToUpperInvariant(pChar) : '\0';
            _joinCode.textComponent.fontSize = 40;
            _joinCode.textComponent.characterSpacing = 18f;
            _joinCode.onSubmit.AddListener(_ => JoinByCode());
            UIFactory.Button(p, "JOIN", TL, new Vector2(110 + 420 + 20, -390), new Vector2(220, 72), UIFactory.Accent, JoinByCode, 30);
            Label(p, "A STEAM INVITE (SHIFT + TAB) JOINS DIRECTLY.", 16, UIFactory.Muted, 110, 490, 900, 24, false, 3f);

            // Casters watch every agent at once (tournament view) and may join a contract already running.
            RectTransform lCaster = UIWidgets.List(p, "Caster", TL, new Vector2(110 + 330, -540), 660f);
            UIWidgets.Switch(lCaster, "Join as a caster  ·  watch, don't play", PlayerIdentity.JoinAsCaster, v => PlayerIdentity.JoinAsCaster = v);
            Label(p, "UP TO FOUR LIVE FEEDS, FREE CAMERA AND TACTICAL MAP. WORKS DURING A CONTRACT TOO.", 14, UIFactory.Muted, 110, 606, 900, 22, false, 3f);

            Header(p, "DIRECT IP  ·  SAME NETWORK", 110, 670);
            _joinIp = UIFactory.Input(p, "192.168.1.20", TL, new Vector2(110, -720), new Vector2(420, 52));
            UIFactory.Button(p, "CONNECT", TL, new Vector2(110 + 420 + 20, -720), new Vector2(220, 52), UIFactory.Accent2, () => { NetSession.Instance.JoinLocal(_joinIp.text); Show(Screen.Lobby); }, 24);

            UIFactory.Button(p, "BACK", TL, new Vector2(110, -860), new Vector2(300, 56), UIFactory.Accent2, () => Show(Screen.Main), 26);
        }

        private void JoinByCode()
        {
            if (string.IsNullOrWhiteSpace(_joinCode.text)) return;
            NetSession.Instance.JoinSteamCode(_joinCode.text);
            Show(Screen.Lobby);
        }

        private void BuildSettings()
        {
            _settings = UIFactory.Stretch(_root, "Settings").gameObject;
            Transform p = _settings.transform;
            Label(p, "OPTIONS", 88, UIFactory.Paper, 104, 150, 1100, 110, true, 4f);
            SettingsPanel.Build(p, TL, new Vector2(110 + 450, -300), 900f);
            UIFactory.Button(p, "BACK", TL, new Vector2(110, -940), new Vector2(300, 56), UIFactory.Accent2, () => Show(Screen.Main), 26);
        }

        // ---------------------------------------------------------------- briefing

        private void BuildLobby()
        {
            _lobby = UIFactory.Stretch(_root, "Lobby").gameObject;
            Transform p = _lobby.transform;
            Image lBack = UIFactory.Stretch(p, "Back").gameObject.AddComponent<Image>();
            lBack.color = new Color(0.03f, 0.035f, 0.04f, 0.72f);
            lBack.raycastTarget = false;

            Label(p, "BRIEFING", 64, UIFactory.Paper, 106, 60, 600, 80, true, 4f);

            // Room code, top right, with a copy button.
            _code = Label(p, "", 22, UIFactory.Paper, 1690 - 600, 88, 600, 32, true, 6f, TextAlignmentOptions.TopRight);
            _copyButton = UIFactory.Button(p, "COPY", TL, new Vector2(1700, -80), new Vector2(110, 44), UIFactory.Accent2, () =>
            {
                if (SteamLobby.Instance != null) GUIUtility.systemCopyBuffer = SteamLobby.Instance.Code;
                HandleStatus("CODE COPIED");
            }, 20);

            // Column 1: the map.
            Header(p, "MAP", 110, 180);
            RectTransform lCard = Box(p, "MapCard", 110, 230, 620, 640);
            lCard.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
            _mapPicture = Box(lCard, "Picture", 0, 0, 620, 470).gameObject.AddComponent<RawImage>();
            _mapPicture.raycastTarget = false;
            _mapName = Label(lCard, "", 40, UIFactory.Paper, 24, 490, 580, 50, true, 3f);
            _mapDescription = Label(lCard, "", 19, UIFactory.Muted, 24, 546, 572, 84, false, 0.5f);
            _mapDescription.textWrappingMode = TextWrappingModes.Normal;
            _mapArrows = Box(lCard, "Arrows", 0, 0, 620, 470).gameObject;
            ArrowButton(_mapArrows.transform, "‹", 0, () => ChangeMap(-1));
            ArrowButton(_mapArrows.transform, "›", 620 - 64, () => ChangeMap(1));

            // Column 2: mode, teams, options.
            Header(p, "MODE", 790, 180);
            (MatchMode mode, string title, string line)[] lModes =
            {
                (MatchMode.FreeForAll, "FREE-FOR-ALL", "Every agent for themselves  ·  1v1v1v1"),
                (MatchMode.Teams, "TEAMS", "2 to 4 teams  ·  the first team to extract wins"),
                (MatchMode.Coop, "CO-OP", "All agents together against the AI")
            };
            for (int i = 0; i < lModes.Length; i++) ModeCard(p, lModes[i].mode, lModes[i].title, lModes[i].line, 790, 230 + i * 104);

            RectTransform lOptions = UIWidgets.List(p, "Options", TL, new Vector2(790 + 290, -560), 580f);
            _teamsRow = UIWidgets.Row(lOptions, "Teams").gameObject;
            _setTeamCount = StepperInRow((RectTransform)_teamsRow.transform, "Number of teams", d => ChangeTeams(d));
            RectTransform lPicker = UIWidgets.Row(lOptions, "Your team");
            _teamPicker = lPicker.gameObject;
            for (int i = 0; i < MatchRulesLogic.MAX_TEAMS; i++)
            {
                int lTeam = i;
                Button lTeamButton = UIFactory.Button(lPicker, (i + 1).ToString(), new Vector2(1, 0.5f), new Vector2(-22 - (MatchRulesLogic.MAX_TEAMS - 1 - i) * 58, 0), new Vector2(50, 40), UIFactory.Accent2, () => LobbyPlayer.Local?.ChooseTeamRpc(lTeam), 22);
                ((RectTransform)lTeamButton.transform).pivot = new Vector2(1, 0.5f);
                UIFactory.Rule(lTeamButton.transform, new Vector2(0.5f, 0), new Vector2(0, 2), 50, false, LobbyPlayer.TeamColor(i), 4f);
            }
            UIWidgets.Switch(lOptions, "Proximity voice chat", true, v => { if (LobbyState.Instance != null) LobbyState.Instance.ProximityChat.Value = v; });
            _casterSwitch = UIWidgets.Switch(lOptions, "Watch as a caster", PlayerIdentity.JoinAsCaster, v => PlayerIdentity.JoinAsCaster = v);
            RectTransform lShuffleRow = UIWidgets.Row(lOptions, "Shuffle the teams");
            _shuffle = UIFactory.Button(lShuffleRow, "SHUFFLE", new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(160, 40), UIFactory.Accent2, ShuffleTeams, 20);
            RectTransform lInviteRow = UIWidgets.Row(lOptions, "Invite Steam friends");
            _invite = UIFactory.Button(lInviteRow, "INVITE", new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(160, 40), UIFactory.Accent2, () => SteamLobby.Instance?.Invite(), 20);

            // Column 3: the agents and the start.
            Header(p, "AGENTS", 1430, 180);
            _playerCount = Label(p, "", 15, UIFactory.Muted, 1430, 180, 380, 22, true, 4f, TextAlignmentOptions.TopRight);
            for (int i = 0; i < 12; i++)
            {
                RectTransform lRow = Box(p, "Agent" + i, 1430, 230 + i * 50, 380, 44);
                lRow.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.04f);
                Image lBar = UIFactory.Panel(lRow, "Team", Color.white, new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(4, 44));
                TextMeshProUGUI lName = Label(lRow, "", 20, UIFactory.Paper, 18, 10, 200, 26, true, 1f);
                lName.overflowMode = TextOverflowModes.Ellipsis;
                TextMeshProUGUI lTeam = Label(lRow, "", 15, UIFactory.Muted, 220, 13, 70, 22, true, 2f);
                TextMeshProUGUI lState = Label(lRow, "", 15, UIFactory.Muted, 290, 13, 76, 22, true, 2f, TextAlignmentOptions.TopRight);
                _agents.Add((lRow, lBar, lName, lTeam, lState));
            }
            _startHint = Label(p, "", 16, UIFactory.Muted, 1430, 890, 380, 24, false, 2f);
            _startButton = UIFactory.Button(p, "START CONTRACT", TL, new Vector2(1430, -930), new Vector2(380, 72), UIFactory.Accent, () => LobbyState.Instance?.StartMatch(), 30);
            _readyButton = UIFactory.Button(p, "READY", TL, new Vector2(1430, -930), new Vector2(380, 72), UIFactory.Accent, () => { if (LobbyPlayer.Local != null) LobbyPlayer.Local.Ready.Value = !LobbyPlayer.Local.Ready.Value; }, 30);
            UIFactory.Button(p, "LEAVE THE ROOM", TL, new Vector2(110, -938), new Vector2(300, 56), UIFactory.Accent2, () => { NetSession.Instance.Leave(); Show(Screen.Main); }, 24);
        }

        private static void ArrowButton(Transform pParent, string pGlyph, float pX, System.Action pAction)
        {
            Button lButton = UIFactory.Button(pParent, pGlyph, TL, new Vector2(pX, -175), new Vector2(64, 120), UIFactory.Accent2, pAction, 54);
            Image lBack = lButton.GetComponent<Image>();
            lBack.color = new Color(0, 0, 0, 0.35f);
            TextMeshProUGUI lText = lButton.GetComponentInChildren<TextMeshProUGUI>();
            lText.alignment = TextAlignmentOptions.Center;
            lText.rectTransform.anchoredPosition = Vector2.zero;
            lText.rectTransform.sizeDelta = new Vector2(64, 120);
        }

        /// <summary>A stepper (‹ value ›) on the right of an existing row.</summary>
        private static System.Action<string> StepperInRow(RectTransform pRow, string pLabel, System.Action<int> pOnStep)
        {
            TextMeshProUGUI lLabel = pRow.GetComponentInChildren<TextMeshProUGUI>();
            if (lLabel != null) lLabel.text = pLabel.ToUpperInvariant();
            TextMeshProUGUI lValue = UIFactory.Title(pRow, "Value", "", 20, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(60, 30), true, 1f);
            Button lMinus = UIFactory.Button(pRow, "−", new Vector2(1, 0.5f), new Vector2(-138, 0), new Vector2(40, 40), UIFactory.Accent2, () => pOnStep(-1), 28);
            Button lPlus = UIFactory.Button(pRow, "+", new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(40, 40), UIFactory.Accent2, () => pOnStep(1), 28);
            foreach (Button lButton in new[] { lMinus, lPlus })
            {
                TextMeshProUGUI lText = lButton.GetComponentInChildren<TextMeshProUGUI>();
                lText.alignment = TextAlignmentOptions.Center;
                lText.rectTransform.anchoredPosition = Vector2.zero;
                lText.rectTransform.sizeDelta = new Vector2(40, 40);
            }
            return pText => lValue.text = pText;
        }

        /// <summary>A mode card: the name, one line on how it plays; amber edge on the chosen one.</summary>
        private void ModeCard(Transform pParent, MatchMode pMode, string pTitle, string pLine, float pX, float pY)
        {
            Button lButton = UIFactory.Button(pParent, "", TL, new Vector2(pX, -pY), new Vector2(580, 92), UIFactory.Accent2, () => SetMode(pMode), 1);
            // The button's own background is the hover wash; the chosen state has a layer of its own.
            Image lBack = UIFactory.Stretch(lButton.transform, "ChosenBack").gameObject.AddComponent<Image>();
            lBack.raycastTarget = false;
            lBack.transform.SetAsFirstSibling();
            Image lBar = UIFactory.Panel(lButton.transform, "Chosen", UIFactory.Accent, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(6, 92));
            Label(lButton.transform, pTitle, 28, UIFactory.Paper, 28, 14, 520, 34, true, 3f);
            Label(lButton.transform, pLine, 18, UIFactory.Muted, 28, 52, 540, 26, false, 1f);
            _modeCards.Add((lButton, lBack, lBar, pMode));
        }

        private void ChangeMap(int pDelta)
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            int lCount = Mathf.Max(1, GameDatabase.Maps.Count);
            lState.MapIndex.Value = ((lState.MapIndex.Value + pDelta) % lCount + lCount) % lCount;
        }

        private void SetMode(MatchMode pMode)
        {
            LobbyState lState = LobbyState.Instance;
            if (lState == null || !lState.IsServer) return;
            lState.Mode.Value = pMode;
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
            List<LobbyPlayer> lShuffled = LobbyPlayer.Agents();
            List<int> lTeams = MatchRulesLogic.AutoBalance(lShuffled.Count, lState.TeamCount.Value);
            for (int i = 0; i < lShuffled.Count; i++) { int j = Random.Range(i, lShuffled.Count); (lShuffled[i], lShuffled[j]) = (lShuffled[j], lShuffled[i]); }
            for (int i = 0; i < lShuffled.Count; i++) lShuffled[i].ChooseTeamRpc(lTeams[i]);
            lState.Invoke(nameof(LobbyState.ResolveTeams), 0.3f);
        }

        private void Update()
        {
            // Steam comes up after the menu: show its persona until the player types a nickname.
            if (_name != null && !_name.isFocused && !PlayerIdentity.HasCustomName && _name.text != PlayerIdentity.LocalName) _name.SetTextWithoutNotify(PlayerIdentity.LocalName);
            if (_current == Screen.Lobby && !NetworkManagerListening() && Time.timeSinceLevelLoad > 1f && (NetSession.Instance == null || NetSession.Instance.Kind == SessionKind.None)) Show(Screen.Main);
            if (_current != Screen.Lobby) return;

            LobbyState lState = LobbyState.Instance;
            bool lHost = lState != null && lState.IsServer;
            _startButton.gameObject.SetActive(lHost);
            _readyButton.gameObject.SetActive(!lHost && (LobbyPlayer.Local == null || !LobbyPlayer.Local.Caster.Value));
            _mapArrows.SetActive(lHost);
            if (lState == null)
            {
                _startHint.text = "CONNECTING TO THE ROOM...";
                return;
            }

            if (lHost) lState.ResolveTeams();
            MapDefinition lMap = lState.Map;
            _mapName.text = lMap != null ? lMap.displayName.ToUpperInvariant() : "-";
            _mapDescription.text = lMap != null ? lMap.description : "";
            _mapPicture.texture = lMap != null && lMap.planFloors != null && lMap.planFloors.Length > 0 ? lMap.planFloors[0] : null;
            _mapPicture.uvRect = new Rect(0.05f, 0.12f, 0.9f, 0.9f * 470f / 620f);
            _mapPicture.color = _mapPicture.texture != null ? Color.white : new Color(0.1f, 0.1f, 0.12f);

            bool lTeams = lState.Mode.Value == MatchMode.Teams;
            foreach ((Button lButton, Image lBack, Image lBar, MatchMode lMode) in _modeCards)
            {
                bool lChosen = lState.Mode.Value == lMode;
                lBar.enabled = lChosen;
                lBack.color = lChosen ? new Color(1, 1, 1, 0.1f) : new Color(1, 1, 1, 0.025f);
                lButton.interactable = lHost;
            }
            _teamsRow.SetActive(lTeams && lHost);
            _setTeamCount(lState.TeamCount.Value.ToString());
            _teamPicker.SetActive(lTeams);
            for (int i = 0; i < MatchRulesLogic.MAX_TEAMS; i++) _teamPicker.transform.GetChild(_teamPicker.transform.childCount - MatchRulesLogic.MAX_TEAMS + i).gameObject.SetActive(i < lState.TeamCount.Value);
            _shuffle.transform.parent.gameObject.SetActive(lTeams && lHost);
            _invite.transform.parent.gameObject.SetActive(lHost && SteamReady);

            bool lSteamRoom = SteamLobby.Instance != null && !string.IsNullOrEmpty(SteamLobby.Instance.Code);
            _code.text = lSteamRoom ? "ROOM CODE   <color=#F5A12B>" + SteamLobby.Instance.Code + "</color>" : NetSession.Instance != null && NetSession.Instance.Kind == SessionKind.Local ? (LobbyPlayer.All.Count <= 1 ? "SOLO" : "LOCAL NETWORK") : "";
            _copyButton.gameObject.SetActive(lSteamRoom);

            if (LobbyPlayer.Local != null && _casterSwitch.isOn != LobbyPlayer.Local.Caster.Value) _casterSwitch.isOn = LobbyPlayer.Local.Caster.Value;

            // Agents first, then the casters (they watch: no team, no ready check).
            List<LobbyPlayer> lRoster = LobbyPlayer.Agents();
            int lAgentCount = lRoster.Count;
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All) if (lPlayer.Caster.Value) lRoster.Add(lPlayer);
            int lWaiting = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                (RectTransform lRow, Image lBar, TextMeshProUGUI lName, TextMeshProUGUI lTeam, TextMeshProUGUI lStateText) = _agents[i];
                LobbyPlayer lPlayer = i < lRoster.Count ? lRoster[i] : null;
                lRow.gameObject.SetActive(lPlayer != null);
                if (lPlayer == null) continue;
                bool lIsHost = lPlayer.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
                bool lCaster = lPlayer.Caster.Value;
                if (!lIsHost && !lCaster && !lPlayer.Ready.Value) lWaiting++;
                lBar.color = lCaster ? new Color(1, 1, 1, 0.25f) : LobbyPlayer.TeamColor(lPlayer.Team.Value);
                lName.text = lPlayer.Name.ToUpperInvariant() + (lPlayer == LobbyPlayer.Local ? "  <color=#9EA0A6><size=70%>YOU</size></color>" : "");
                lTeam.text = lTeams && !lCaster ? "TEAM " + (lPlayer.Team.Value + 1) : "";
                lStateText.text = lCaster ? (lIsHost ? "HOST · CASTER" : "CASTER") : lIsHost ? "HOST" : lPlayer.Ready.Value ? "READY" : "WAITING";
                lStateText.color = lCaster ? ThreatMarkers.Blue : lIsHost ? UIFactory.Accent : lPlayer.Ready.Value ? UIFactory.Good : UIFactory.Muted;
            }
            int lCasters = lRoster.Count - lAgentCount;
            _playerCount.text = lAgentCount + " / " + GameDatabase.Rules.maxPlayers + (lCasters > 0 ? "  ·  " + lCasters + " CASTER" + (lCasters > 1 ? "S" : "") : "");
            _startButton.interactable = lWaiting == 0 && lAgentCount > 0;
            _startHint.text = lHost
                ? lAgentCount == 0 ? "AT LEAST ONE AGENT MUST PLAY"
                : lWaiting > 0 ? "WAITING FOR " + lWaiting + " AGENT" + (lWaiting > 1 ? "S" : "") + " TO BE READY" : "EVERYONE IS READY"
                : "THE HOST STARTS THE CONTRACT";
            TextMeshProUGUI lReadyLabel = _readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (LobbyPlayer.Local != null) lReadyLabel.text = LobbyPlayer.Local.Ready.Value ? "NOT READY" : "READY";
        }
    }
}
