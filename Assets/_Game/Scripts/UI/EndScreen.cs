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
    /// The end of a match: who extracted (or nobody), how the target died, the scores, and buttons to
    /// go on at once - the host takes the room back to the lobby, a guest can leave the room. Without a
    /// click the host's machine goes back to the lobby after <see cref="MatchDirector.END_SCREEN_FALLBACK"/>.
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        private static EndScreen _instance;
        /// <summary>The end screen is up (the HUD keeps its spectating line out of the way).</summary>
        public static bool IsShown => _instance != null;
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
                _ => "TIME IS UP"
            };
            _instance._title.text = lTitle;
            // A caster won nothing and lost nothing: the result reads neutral.
            _instance._title.color = lCaster ? UIFactory.Paper : pWon ? UIFactory.Good : UIFactory.Danger;
            string lBody = pOutcome == MatchOutcome.Extracted ? "EXTRACTED BY " + pWinners : "NOBODY EXTRACTED THE PROOF";
            if (!string.IsNullOrEmpty(pMethod)) lBody += "\nTARGET: " + pMethod;
            lBody += "\n\n";
            foreach (LobbyPlayer lPlayer in LobbyPlayer.Agents())
                lBody += "<color=#" + ColorUtility.ToHtmlStringRGB(LobbyPlayer.TeamColor(lPlayer.Team.Value)) + ">" + lPlayer.Name.ToUpperInvariant() + "</color>   " + lPlayer.Kills.Value + " PLAYERS  -  " + lPlayer.NpcKills.Value + " NPC\n";
            _instance._body.text = lBody;
            _instance._time = 0f;

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
            UIFactory.Stretch(lRoot, "Dim").gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.04f, 0.78f);
            // Everything hangs from the top centre (UIFactory pivots on the anchor).
            _title = UIFactory.Title(lRoot, "Title", "", 96, UIFactory.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(1700, 120), true, 4f);
            _body = UIFactory.Text(lRoot, "Body", "", 28, UIFactory.Paper, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0, -320), new Vector2(1400, 440));

            Vector2 lBottom = new Vector2(0.5f, 0.2f);
            _lobby = UIFactory.Button(lRoot, "BACK TO THE LOBBY", lBottom, new Vector2(-230, 0), new Vector2(420, 72), UIFactory.Accent, () =>
            {
                if (LobbyState.Instance != null && LobbyState.Instance.IsServer) LobbyState.Instance.ReturnToLobby();
            }, 28);
            _leave = UIFactory.Button(lRoot, "LEAVE THE ROOM", lBottom, new Vector2(230, 0), new Vector2(420, 72), UIFactory.Accent2, () => NetSession.Instance?.Leave(), 28);
            _footer = UIFactory.Text(lRoot, "Footer", "", 20, UIFactory.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(1200, 40));
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
