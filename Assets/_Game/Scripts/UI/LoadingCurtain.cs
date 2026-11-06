using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.World;
using TMPro;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// A plain screen between the room and the map. It goes up as soon as a contract starts and comes
    /// down once the map has done its heavy work (loading, reflection captures, first use of its
    /// shaders), so the player never sees those hitches, only a game that is ready.
    /// </summary>
    public class LoadingCurtain : MonoBehaviour
    {
        /// <summary>Never longer than this, whatever goes wrong.</summary>
        private const float TIMEOUT = 30f;
        private const float MIN_TIME = 0.4f, FADE = 0.35f;

        private static LoadingCurtain _instance;
        private CanvasGroup _group;
        private TextMeshProUGUI _map;
        private float _shownAt;
        private bool _leaving;

        public static void Show(string pMap)
        {
            if (_instance == null)
            {
                _instance = new GameObject("LoadingCurtain").AddComponent<LoadingCurtain>();
                DontDestroyOnLoad(_instance.gameObject);
            }
            _instance.Open(pMap);
        }

        private void Awake()
        {
            Canvas lCanvas = UIFactory.Canvas("CurtainCanvas", 200);
            lCanvas.transform.SetParent(transform, false);
            RectTransform lRoot = UIFactory.Stretch(lCanvas.transform, "Curtain");
            lRoot.gameObject.AddComponent<UnityEngine.UI.Image>().color = UIFactory.Ink;
            _group = lRoot.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = true;
            _map = UIFactory.Title(lRoot, "Map", "", 64, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1400, 90), true, 8f);
            UIFactory.Title(lRoot, "Line", "LOADING THE CONTRACT", 18, UIFactory.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(800, 30), false, 6f);
        }

        private void Open(string pMap)
        {
            _map.text = pMap.ToUpperInvariant();
            _shownAt = Time.unscaledTime;
            _leaving = false;
            _group.alpha = 1f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            float lElapsed = Time.unscaledTime - _shownAt;
            if (!_leaving && lElapsed > MIN_TIME && (Ready || lElapsed > TIMEOUT)) _leaving = true;
            if (!_leaving) return;
            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FADE);
            if (_group.alpha <= 0f) gameObject.SetActive(false);
        }

        private static bool Ready
        {
            get
            {
                MatchDirector lDirector = MatchDirector.Instance;
                if (lDirector == null || !lDirector.IsSpawned || lDirector.Phase.Value == MatchPhase.Loading) return false;
                return RoomProbes.Instance == null || RoomProbes.Instance.Captured;
            }
        }
    }
}
