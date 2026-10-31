using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.FX;
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
    /// The in-match menu (Esc, or M straight to the map). The game keeps running (it is online)
    /// and the mouse is freed. A column of tabs on the left - MAP, CONTRACT, OPTIONS - with RESUME
    /// and LEAVE THE MATCH under them; the chosen tab fills the rest of the screen.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public enum Tab { Map, Contract, Options }

        private static PauseMenu _instance;
        private GameObject _panel;
        private bool _open;
        private Tab _tab = Tab.Map;
        private readonly Dictionary<Tab, GameObject> _pages = new Dictionary<Tab, GameObject>();
        private readonly Dictionary<Tab, Button> _tabs = new Dictionary<Tab, Button>();
        private MapScreen _map;

        public static bool IsOpen => _instance != null && _instance._open;

        public static void Ensure()
        {
            if (_instance == null) _instance = new GameObject("PauseMenu").AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            Canvas lCanvas = UIFactory.Canvas("PauseCanvas", 30);
            lCanvas.transform.SetParent(transform, false);
            _panel = UIFactory.Stretch(lCanvas.transform, "Panel").gameObject;
            _panel.AddComponent<Image>().color = new Color(0.05f, 0.055f, 0.065f, 0.96f);
            Transform p = _panel.transform;
            Vector2 L = new Vector2(0, 1);

            // Left column: where you are, the tabs, then resume / leave.
            string lMapName = LobbyState.Instance != null && LobbyState.Instance.Map != null ? LobbyState.Instance.Map.displayName.ToUpperInvariant() : "";
            UIFactory.Title(p, "Kicker", "CONTRACT IN PROGRESS", 15, UIFactory.Accent, TextAlignmentOptions.Left, L, new Vector2(80, -70), new Vector2(340, 22), true, 5f);
            UIFactory.Title(p, "Map", lMapName, 40, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(80, -110), new Vector2(340, 48), true, 2f);
            AddTab(p, Tab.Map, "MAP", 0);
            AddTab(p, Tab.Contract, "CONTRACT", 1);
            AddTab(p, Tab.Options, "OPTIONS", 2);
            UIFactory.Rule(p, L, new Vector2(80, -424), 340, false, new Color(1, 1, 1, 0.12f), 1f);
            Button lResume = UIFactory.Button(p, "RESUME", L, new Vector2(80, -470), new Vector2(340, 56), UIFactory.Accent2, () => Toggle(false), 26);
            UIFactory.Button(p, "LEAVE THE MATCH", L, new Vector2(80, -530), new Vector2(340, 56), UIFactory.Accent2, () =>
            {
                Toggle(false);
                NetSession.Instance?.Leave();
            }, 26);
            UIFactory.Title(p, "Hints", "ESC  RESUME      M  MAP      WHEEL / TRIGGERS  ZOOM      DRAG / STICK  PAN", 15, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(480, 30), new Vector2(1200, 24), false, 3f);

            // The pages, right of the column.
            RectTransform lContent = UIFactory.Rect(p, "Content", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            lContent.offsetMin = new Vector2(480, 80);
            lContent.offsetMax = new Vector2(-70, -60);

            GameObject lMapPage = UIFactory.Stretch(lContent, "MapPage").gameObject;
            _map = lMapPage.AddComponent<MapScreen>();
            _map.Build((RectTransform)lMapPage.transform, false);
            _pages[Tab.Map] = lMapPage;

            GameObject lContractPage = UIFactory.Stretch(lContent, "ContractPage").gameObject;
            MapScreen lContract = lContractPage.AddComponent<MapScreen>();
            lContract.Build((RectTransform)lContractPage.transform, true);
            _pages[Tab.Contract] = lContractPage;

            GameObject lOptions = UIFactory.Stretch(lContent, "OptionsPage").gameObject;
            SettingsPanel.Build(lOptions.transform, new Vector2(0.5f, 1f), new Vector2(0, 0), 900f);
            _pages[Tab.Options] = lOptions;

            _panel.SetActive(false);
        }

        private void AddTab(Transform pParent, Tab pTab, string pLabel, int pIndex)
        {
            Button lButton = UIFactory.Button(pParent, pLabel, new Vector2(0, 1), new Vector2(80, -200 - pIndex * 64), new Vector2(340, 58), UIFactory.Accent2, () => Show(pTab), 30);
            _tabs[pTab] = lButton;
        }

        private void Show(Tab pTab)
        {
            _tab = pTab;
            foreach (KeyValuePair<Tab, GameObject> lPage in _pages)
            {
                bool lOn = lPage.Key == pTab;
                if (lOn && !lPage.Value.activeSelf) UIFactory.FadeIn(lPage.Value, 0.2f, new Vector2(0, -10));
                lPage.Value.SetActive(lOn);
            }
            foreach (KeyValuePair<Tab, Button> lTab in _tabs)
            {
                TextMeshProUGUI lLabel = lTab.Value.GetComponentInChildren<TextMeshProUGUI>();
                lLabel.fontStyle = lTab.Key == pTab ? FontStyles.Bold : FontStyles.Normal;
                Image lBar = lTab.Value.transform.Find("Bar")?.GetComponent<Image>();
                if (lBar != null) lBar.color = lTab.Key == pTab ? UIFactory.Accent : new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b, 0.4f);
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_tabs[pTab].gameObject);
        }

        private void Update()
        {
            if (GameInput.PausePressed) Toggle(!_open);
            else if (GameInput.MapPressed)
            {
                if (_open && _tab == Tab.Map) Toggle(false);
                else { Toggle(true); Show(Tab.Map); }
            }
        }

        private void Toggle(bool pOpen)
        {
            if (pOpen == _open) return;
            _open = pOpen;
            _panel.SetActive(pOpen);
            GameInput.GameplayEnabled = !pOpen;
            GameInput.LockCursor(!pOpen);
            Audio.Sfx.Play2D("map", 0.5f);
            if (pOpen)
            {
                UIFactory.FadeIn(_panel, 0.18f, Vector2.zero);
                Show(_tab);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            GameInput.GameplayEnabled = true;
        }
    }

    /// <summary>The options list, shared by the title and pause screens: rows, label left, control right.</summary>
    public static class SettingsPanel
    {
        public static RectTransform Build(Transform pParent, Vector2 pAnchor, Vector2 pPosition, float pWidth)
        {
            RectTransform lList = UIWidgets.List(pParent, "Settings", pAnchor, pPosition, pWidth);
            UIWidgets.Section(lList, "CONTROLS");
            UIWidgets.Slider(lList, "Mouse sensitivity", 0.2f, 3f, GameInput.MouseSensitivity, v => v.ToString("0.00"), v => GameInput.MouseSensitivity = v);
            UIWidgets.Section(lList, "VIDEO");
            UIWidgets.Slider(lList, "Field of view", 65f, 100f, PlayerPrefs.GetFloat("goobyr.fov", 78f), v => Mathf.RoundToInt(v) + "°", v =>
            {
                PlayerPrefs.SetFloat("goobyr.fov", v);
                if (CameraRig.Instance != null) CameraRig.Instance.BaseFov = v;
            });
            UIWidgets.Section(lList, "AUDIO");
            UIWidgets.Slider(lList, "Master volume", 0f, 1f, Audio.Sfx.Volume, v => Mathf.RoundToInt(v * 100f) + "%", v => { Audio.Sfx.Volume = v; AudioListener.volume = v; });
            UIWidgets.Section(lList, "VOICE CHAT");
            UIWidgets.Switch(lList, "Push to talk", Audio.VoiceChat.PushToTalk, v => Audio.VoiceChat.PushToTalk = v, "B");
            return lList;
        }
    }
}
