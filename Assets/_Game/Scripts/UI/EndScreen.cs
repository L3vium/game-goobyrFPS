using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The end of a match, over the match still running behind it: who won, how the target died, the
    /// scores, and what next - the host takes the room back to the lobby, anyone can leave, or spectate
    /// (the panel steps aside for the caster view, a button brings it back). The pause menu stays on top.
    /// Without a click the host's machine goes back to the lobby after <see cref="MatchDirector.END_SCREEN_FALLBACK"/>.
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        private static EndScreen _instance;
        /// <summary>The end screen is up (the HUD keeps its spectating line out of the way).</summary>
        public static bool IsShown => _instance != null;
        /// <summary>The panel is in the way (not while spectating behind it).</summary>
        public static bool IsBlocking => _instance != null && !_instance._spectating;
        private bool _spectating;
        private Button _spectate, _results;
        private GameObject _panel;
        private CanvasGroup _group;
        private TextMeshProUGUI _title, _body, _footer;
        private Button _lobby, _leave;
        private float _time;

        public static void Show(MatchOutcome pOutcome, bool pWon, string pWinners, string pMethod)
        {
            if (_instance == null) _instance = new GameObject("EndScreen").AddComponent<EndScreen>();
            bool lCaster = LobbyPlayer.Local != null && LobbyPlayer.Local.Caster.Value;
            string lTitle = pOutcome switch
            {
                MatchOutcome.Extracted => pWon || lCaster ? "CONTRACT COMPLETE" : "CONTRACT LOST",
                MatchOutcome.AllEliminated => "EVERYONE IS DOWN",
                MatchOutcome.TargetKilled => lCaster ? "TARGET ELIMINATED" : pWon ? "CONTRACT COMPLETE" : "TARGET LOST",
                MatchOutcome.AttackersDown => lCaster ? "ATTACK REPELLED" : pWon ? "TARGET PROTECTED" : "CONTRACT LOST",
                _ => pWon ? "TARGET PROTECTED" : "TIME IS UP"
            };
            _instance._title.text = lTitle;
            // A caster won nothing and lost nothing: the result reads neutral.
            _instance._title.color = lCaster ? UIFactory.Paper : pWon ? UIFactory.Good : UIFactory.Danger;
            string lBody = pOutcome switch
            {
                MatchOutcome.Extracted => "EXTRACTED BY " + pWinners,
                MatchOutcome.TargetKilled => "THE TARGET IS DEAD",
                MatchOutcome.AttackersDown => "EVERY ATTACKER IS DOWN",
                _ => MatchDirector.Instance != null && MatchDirector.Instance.Protect ? "THE TARGET SURVIVED" : "NOBODY EXTRACTED THE PROOF"
            };
            if (!string.IsNullOrEmpty(pMethod)) lBody += "\nTARGET: " + pMethod;
            lBody += "\n\n";
            foreach (LobbyPlayer lPlayer in LobbyPlayer.Agents())
                lBody += "<color=#" + ColorUtility.ToHtmlStringRGB(LobbyPlayer.TeamColor(lPlayer.Team.Value)) + ">" + lPlayer.Name.ToUpperInvariant() + "</color>   " + lPlayer.Kills.Value + " PLAYERS  -  " + lPlayer.NpcKills.Value + " NPC\n";
            _instance._body.text = lBody;
            _instance._time = 0f;
            _instance._spectating = false;
            _instance._panel.SetActive(true);
            _instance._results.gameObject.SetActive(false);

            bool lHost = LobbyState.Instance != null && LobbyState.Instance.IsServer;
            _instance._lobby.gameObject.SetActive(lHost);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject((lHost ? _instance._lobby : _instance._leave).gameObject);
            Audio.Sfx.Play2D(pWon || lCaster ? "win" : "lose", 1f);
        }

        private void Awake()
        {
            Canvas lCanvas = UIFactory.Canvas("EndCanvas", 40);
            lCanvas.transform.SetParent(transform, false);
            RectTransform lRoot = (RectTransform)lCanvas.transform;
            _group = lCanvas.gameObject.AddComponent<CanvasGroup>();
            // A panel across the middle: the match goes on above and below it.
            RectTransform lPanel = UIFactory.Panel(lRoot, "Panel", UIFactory.PanelColor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1320, 760)).rectTransform;
            lPanel.GetComponent<Image>().raycastTarget = true;
            _panel = lPanel.gameObject;
            _title = UIFactory.Title(lPanel, "Title", "", 84, UIFactory.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(1260, 110), true, 6f);
            _body = UIFactory.Text(lPanel, "Body", "", 26, UIFactory.Paper, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0, -320), new Vector2(1200, 340));

            Vector2 lBottom = new Vector2(0.5f, 0f);
            _lobby = UIFactory.Button(lPanel, "BACK TO THE LOBBY", lBottom, new Vector2(-420, 72), new Vector2(390, 68), UIFactory.Accent, () =>
            {
                if (LobbyState.Instance != null && LobbyState.Instance.IsServer) LobbyState.Instance.ReturnToLobby();
            }, 26);
            _spectate = UIFactory.Button(lPanel, "SPECTATE", lBottom, new Vector2(0, 72), new Vector2(390, 68), UIFactory.Accent2, Spectate, 26);
            _leave = UIFactory.Button(lPanel, "LEAVE THE ROOM", lBottom, new Vector2(420, 72), new Vector2(390, 68), UIFactory.Accent2, () => NetSession.Instance?.Leave(), 26);
            _footer = UIFactory.Text(lPanel, "Footer", "", 18, UIFactory.Muted, TextAlignmentOptions.Center, lBottom, new Vector2(0, 20), new Vector2(1200, 30));
            _results = UIFactory.Button(lRoot, "RESULTS", new Vector2(1f, 1f), new Vector2(-120, -40), new Vector2(200, 52), UIFactory.Accent, () => SetSpectating(false), 22);
        }

        /// <summary>The panel steps aside and the caster view takes over, for anyone still alive too.</summary>
        private void Spectate()
        {
            Player.CasterView.Begin(false);
            SetSpectating(true);
        }

        private void SetSpectating(bool pSpectating)
        {
            _spectating = pSpectating;
            _panel.SetActive(!pSpectating);
            _results.gameObject.SetActive(pSpectating);
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(_time * 2.5f);
            float lLeft = Mathf.Max(0f, MatchDirector.END_SCREEN_FALLBACK - _time);
            bool lHost = LobbyState.Instance != null && LobbyState.Instance.IsServer;
            _footer.text = lHost ? $"THE ROOM GOES BACK TO THE LOBBY IN {lLeft:0} S" : $"WAITING FOR THE HOST  -  LOBBY IN {lLeft:0} S AT THE LATEST";
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
