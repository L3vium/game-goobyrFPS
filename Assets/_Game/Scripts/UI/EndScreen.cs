using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using TMPro;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>The end of a match: who extracted (or nobody), how the target died, the scores.</summary>
    public class EndScreen : MonoBehaviour
    {
        private static EndScreen _instance;
        private TextMeshProUGUI _title, _body;
        private float _time;

        public static void Show(MatchOutcome pOutcome, bool pWon, string pWinners, string pMethod)
        {
            if (_instance == null) _instance = new GameObject("EndScreen").AddComponent<EndScreen>();
            string lTitle = pOutcome switch
            {
                MatchOutcome.Extracted => pWon ? "CONTRACT COMPLETE" : "CONTRACT LOST",
                MatchOutcome.AllEliminated => "EVERYONE IS DOWN",
                _ => "TIME IS UP"
            };
            _instance._title.text = lTitle;
            _instance._title.color = pWon ? UIFactory.Good : UIFactory.Accent;
            string lBody = pOutcome == MatchOutcome.Extracted ? "EXTRACTED BY " + pWinners : "NOBODY EXTRACTED THE PROOF";
            if (!string.IsNullOrEmpty(pMethod)) lBody += "\nTARGET: " + pMethod;
            lBody += "\n\n";
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All)
                lBody += "<color=#" + ColorUtility.ToHtmlStringRGB(LobbyPlayer.TeamColor(lPlayer.Team.Value)) + ">" + lPlayer.Name.ToUpperInvariant() + "</color>   " + lPlayer.Kills.Value + " PLAYERS  -  " + lPlayer.NpcKills.Value + " NPC\n";
            lBody += "\n<size=70%>BACK TO THE LOBBY IN A FEW SECONDS</size>";
            _instance._body.text = lBody;
            _instance._time = 0f;
            Audio.Sfx.Play2D(pWon ? "win" : "lose", 1f);
        }

        private void Awake()
        {
            Canvas lCanvas = UIFactory.Canvas("EndCanvas", 40);
            lCanvas.transform.SetParent(transform, false);
            RectTransform lRoot = (RectTransform)lCanvas.transform;
            UIFactory.Stretch(lRoot, "Dim").gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.55f);
            _title = UIFactory.Text(lRoot, "Title", "", 110, UIFactory.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(1700, 140));
            _body = UIFactory.Text(lRoot, "Body", "", 34, UIFactory.Paper, TextAlignmentOptions.Top, new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(1400, 600));
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float lPop = Mathf.Clamp01(_time * 3f);
            _title.rectTransform.localScale = Vector3.one * (1f + (1f - lPop) * 0.6f + Mathf.Sin(_time * 3f) * 0.02f);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
