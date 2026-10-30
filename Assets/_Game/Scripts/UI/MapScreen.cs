using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
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
    /// The map (M): a top-down render of the level taken once when it loads, with the zone names,
    /// you, your teammates, the active extractions, the proof and the carrier's last ping.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        private static MapScreen _instance;
        public static bool IsOpen => _instance != null && _instance._panel.activeSelf;

        private GameObject _panel;
        private RawImage _image;
        private RenderTexture _texture;
        private Bounds _bounds;
        private RectTransform _icons;
        private readonly List<TextMeshProUGUI> _pool = new List<TextMeshProUGUI>();
        private int _used;

        public static void Ensure()
        {
            if (_instance == null) _instance = new GameObject("MapScreen").AddComponent<MapScreen>();
        }

        private void Awake()
        {
            Canvas lCanvas = UIFactory.Canvas("MapCanvas", 25);
            lCanvas.transform.SetParent(transform, false);
            _panel = UIFactory.Stretch(lCanvas.transform, "Panel").gameObject;
            _panel.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            RectTransform lFrame = UIFactory.Rect(_panel.transform, "Frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 980));
            _image = lFrame.gameObject.AddComponent<RawImage>();
            _icons = UIFactory.Stretch(lFrame, "Icons");
            UIFactory.Text(_panel.transform, "Title", LobbyState.Instance != null && LobbyState.Instance.Map != null ? LobbyState.Instance.Map.displayName.ToUpperInvariant() : "MAP", 54, UIFactory.Accent, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(260, -60), new Vector2(480, 70));
            UIFactory.Text(_panel.transform, "Legend", "<color=#FFFFFF>YOU</color>\n<color=#7CF29A>EXTRACTION</color>\n<color=#FFE066>PROOF</color>\n<color=#FFB13B>CARRIER</color>\n<color=#9AD7FF>TEAMMATE</color>", 28, UIFactory.Paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(200, -260), new Vector2(360, 300));
            _panel.SetActive(false);
            Render();
        }

        private void Render()
        {
            _bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool lFirst = true;
            foreach (Renderer lRenderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (lRenderer.gameObject.layer != GameLayers.DEFAULT || lRenderer.bounds.size.magnitude > 2000f) continue;
                if (lFirst) { _bounds = lRenderer.bounds; lFirst = false; }
                else _bounds.Encapsulate(lRenderer.bounds);
            }
            float lSize = Mathf.Max(_bounds.size.x, _bounds.size.z) * 0.5f + 5f;
            _bounds = new Bounds(new Vector3(_bounds.center.x, _bounds.center.y, _bounds.center.z), new Vector3(lSize * 2f, _bounds.size.y, lSize * 2f));

            _texture = new RenderTexture(1024, 1024, 24);
            GameObject lCameraObject = new GameObject("MapCamera");
            Camera lCamera = lCameraObject.AddComponent<Camera>();
            lCamera.orthographic = true;
            lCamera.orthographicSize = lSize;
            lCamera.transform.position = new Vector3(_bounds.center.x, _bounds.max.y + 200f, _bounds.center.z);
            lCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            lCamera.farClipPlane = 600f;
            lCamera.clearFlags = CameraClearFlags.SolidColor;
            lCamera.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            lCamera.cullingMask = 1 << GameLayers.DEFAULT | 1 << GameLayers.WATER | 1 << GameLayers.CLIMBABLE | 1 << GameLayers.INTERACTABLE;
            lCamera.targetTexture = _texture;
            lCamera.Render();
            lCamera.targetTexture = null;
            Destroy(lCameraObject);
            _image.texture = _texture;
        }

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (GameInput.MapPressed && !PauseMenu.IsOpen)
            {
                _panel.SetActive(!_panel.activeSelf);
                GameInput.GameplayEnabled = !_panel.activeSelf;
                Audio.Sfx.Play2D("map", 0.6f);
            }
            if (!_panel.activeSelf) return;

            _used = 0;
            foreach (LevelZone lZone in LevelZone.All) Icon(lZone.transform.position, "<size=80%><color=#FFFFFFAA>" + lZone.ZoneName.ToUpperInvariant() + "</color></size>");

            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null && lDirector.Proof.Value != ProofState.TargetAlive)
            {
                List<MatchPoint> lExtractions = MatchPoint.OfKind(MatchPoint.Kind.Extraction);
                for (int i = 0; i < lExtractions.Count; i++)
                    if ((lDirector.ActiveExtractions.Value & (1 << i)) != 0) Icon(lExtractions[i].transform.position, "<color=#7CF29A>[EX]</color>");
                if (lDirector.Proof.Value == ProofState.Dropped) Icon(lDirector.ProofPosition.Value, "<color=#FFE066>*</color>");
            }
            PlayerAvatar lMe = PlayerAvatar.Local;
            foreach (PlayerAvatar lAvatar in PlayerAvatar.All)
            {
                if (!lAvatar.Health.IsAlive) continue;
                if (lAvatar == lMe) Icon(lAvatar.transform.position, "<color=#FFFFFF>▲</color>", -lAvatar.transform.eulerAngles.y);
                else if (lMe != null && lAvatar.Team.Value == lMe.Team.Value && LobbyState.Instance != null && LobbyState.Instance.Mode.Value != MatchMode.FreeForAll)
                    Icon(lAvatar.transform.position, "<color=#9AD7FF>o</color>");
            }
            for (int i = _used; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        }

        private void Icon(Vector3 pWorld, string pText, float pRotation = 0f)
        {
            if (_used >= _pool.Count) _pool.Add(UIFactory.Text(_icons, "Icon", "", 30, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 60)));
            TextMeshProUGUI lText = _pool[_used++];
            lText.gameObject.SetActive(true);
            lText.text = pText;
            float u = (pWorld.x - _bounds.min.x) / Mathf.Max(1f, _bounds.size.x) - 0.5f;
            float v = (pWorld.z - _bounds.min.z) / Mathf.Max(1f, _bounds.size.z) - 0.5f;
            lText.rectTransform.anchoredPosition = new Vector2(u * 980f, v * 980f);
            lText.rectTransform.localRotation = Quaternion.Euler(0, 0, pRotation);
        }
    }
}
