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
    /// The map (M): the contract on the left (the target's portrait, name, one line of dossier,
    /// the proof wanted - nothing more, the rest is found in the level) and a top-down render of
    /// the level taken once when it loads, with the zone names, you, your teammates, the active
    /// extractions and the proof.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        private static MapScreen _instance;
        public static bool IsOpen => _instance != null && _instance._panel.activeSelf;

        private GameObject _panel;
        private RawImage _portrait;
        private RenderTexture _portraitTexture;
        private TextMeshProUGUI _targetName, _targetLine, _targetProof, _targetState;
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
            _panel.AddComponent<Image>().color = new Color(0.07f, 0.075f, 0.09f, 0.97f);
            RectTransform lFrame = UIFactory.Rect(_panel.transform, "Frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220, 0), new Vector2(900, 900));
            _image = lFrame.gameObject.AddComponent<RawImage>();
            _icons = UIFactory.Stretch(lFrame, "Icons");
            // Tab bar: CONTRACT and MAP side by side, the map's name above.
            RectTransform lBar = UIFactory.Rect(_panel.transform, "Bar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(0, 56));
            lBar.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.05f);
            Image lTab = UIFactory.Panel(lBar, "Tab", UIFactory.Accent, new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(300, 56));
            lTab.rectTransform.pivot = new Vector2(0, 0.5f);
            UIFactory.Title(lTab.transform, "Label", "CONTRACT  /  MAP", 22, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(280, 40), true, 3f);
            UIFactory.Title(_panel.transform, "Title", LobbyState.Instance != null && LobbyState.Instance.Map != null ? LobbyState.Instance.Map.displayName : "MAP", 18, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(90, -30), new Vector2(600, 30), false, 4f);

            // The contract column.
            RectTransform lContract = UIFactory.Rect(_panel.transform, "Contract", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, -20), new Vector2(420, 760));
            _portraitTexture = new RenderTexture(420, 520, 24) { name = "TargetPortrait" };
            _portrait = UIFactory.Rect(lContract, "Portrait", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(420, 520)).gameObject.AddComponent<RawImage>();
            _portrait.texture = _portraitTexture;
            _portrait.raycastTarget = false;
            UIFactory.Title(lContract, "Kicker", "TARGET", 16, UIFactory.Accent, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, -540), new Vector2(420, 24), true, 4f);
            _targetName = UIFactory.Title(lContract, "Name", "", 38, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, -566), new Vector2(420, 48), true, 1f);
            _targetLine = UIFactory.Text(lContract, "Line", "", 19, UIFactory.Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, -620), new Vector2(420, 60));
            _targetProof = UIFactory.Title(lContract, "Proof", "", 18, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, -690), new Vector2(420, 28), false, 3f);
            _targetState = UIFactory.Title(lContract, "State", "", 18, UIFactory.Accent, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, -722), new Vector2(420, 28), false, 3f);
            UIFactory.Text(_panel.transform, "Legend", "<color=#FFFFFF>YOU</color>   <color=#7CF29A>EXTRACTION</color>   <color=#FFE066>PROOF</color>   <color=#9AD7FF>TEAMMATE</color>", 18, UIFactory.Muted, TextAlignmentOptions.Right, new Vector2(1, 0), new Vector2(-90, 50), new Vector2(900, 30));
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

        /// <summary>The contract column: a portrait of the target rendered now, and its file.</summary>
        private void RefreshContract()
        {
            MatchDirector lDirector = MatchDirector.Instance;
            Data.TargetProfile lTarget = lDirector != null ? lDirector.Target : null;
            _targetName.text = lTarget != null ? lTarget.displayName.ToUpperInvariant() : "";
            _targetLine.text = lTarget != null ? lTarget.dossier : "";
            _targetProof.text = lTarget != null ? "PROOF  <color=#FFFFFF>" + lTarget.proof.ToString().ToUpperInvariant() + "</color>" : "";
            _targetState.text = lDirector == null ? "" : lDirector.Proof.Value switch
            {
                ProofState.TargetAlive => "ALIVE",
                ProofState.Dropped => "DOWN  -  PROOF ON THE GROUND",
                ProofState.Carried => "DOWN  -  PROOF CARRIED",
                _ => "EXTRACTED"
            };

            Npc lNpc = null;
            foreach (Npc lCandidate in Npc.All) if (lCandidate != null && lCandidate.IsTarget) lNpc = lCandidate;
            GameObject lCameraObject = new GameObject("PortraitCamera");
            Camera lCamera = lCameraObject.AddComponent<Camera>();
            lCamera.clearFlags = CameraClearFlags.SolidColor;
            lCamera.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            lCamera.fieldOfView = 22f;
            lCamera.nearClipPlane = 0.3f;
            lCamera.farClipPlane = 6f;
            lCamera.targetTexture = _portraitTexture;
            if (lNpc != null)
            {
                Vector3 lHead = lNpc.transform.position + Vector3.up * 1.6f;
                lCamera.transform.position = lHead + lNpc.transform.forward * 3.3f + lNpc.transform.right * 0.6f + Vector3.up * 0.1f;
                lCamera.transform.LookAt(lHead + Vector3.down * 0.15f);
                lCamera.Render();
            }
            else
            {
                RenderTexture lActive = RenderTexture.active;
                RenderTexture.active = _portraitTexture;
                GL.Clear(true, true, new Color(0.16f, 0.17f, 0.2f));
                RenderTexture.active = lActive;
            }
            lCamera.targetTexture = null;
            Destroy(lCameraObject);
        }

        private void OnDestroy()
        {
            if (_portraitTexture != null) _portraitTexture.Release();
            if (_texture != null) _texture.Release();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (GameInput.MapPressed && !PauseMenu.IsOpen)
            {
                _panel.SetActive(!_panel.activeSelf);
                GameInput.GameplayEnabled = !_panel.activeSelf;
                if (_panel.activeSelf) RefreshContract();
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
            lText.rectTransform.anchoredPosition = new Vector2(u * 900f, v * 900f);
            lText.rectTransform.localRotation = Quaternion.Euler(0, 0, pRotation);
        }
    }
}
