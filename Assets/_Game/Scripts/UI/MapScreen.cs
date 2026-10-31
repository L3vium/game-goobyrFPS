using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// A page of the in-match menu (<see cref="PauseMenu"/>):
    /// <list type="bullet">
    /// <item><b>Map</b>: the whole stylized plan of the level, floor by floor (it opens on yours),
    /// zoom with the wheel or the triggers, pan by dragging or with the stick; the zones' names, the
    /// target in red (always), you, your teammates, the proof and the revealed extractions.</item>
    /// <item><b>Contract</b>: the target's portrait rendered when the page opens, its name, one line
    /// of dossier, the proof wanted and where the contract stands - nothing more.</item>
    /// </list>
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        /// <summary>The in-match menu is open (the HUD's map pieces hide).</summary>
        public static bool IsOpen => PauseMenu.IsOpen;

        public static void Ensure() => PauseMenu.Ensure();

        private const float FRAME = 900f;
        private bool _contract;
        private RectTransform _frame, _icons;
        private RawImage _plan;
        private int _floor;
        private float _zoom = 1f;
        private Vector2 _center = new Vector2(0.5f, 0.5f);
        private readonly List<Button> _floorButtons = new List<Button>();
        private readonly List<(RectTransform rect, Image icon, TextMeshProUGUI label)> _pool = new List<(RectTransform, Image, TextMeshProUGUI)>();
        private int _used;
        private Vector2 _lastMouse;
        private bool _dragging;

        private RawImage _portrait;
        private RenderTexture _portraitTexture;
        private TextMeshProUGUI _targetName, _targetLine, _targetProof, _targetState;

        public void Build(RectTransform pPage, bool pContract)
        {
            _contract = pContract;
            if (pContract) BuildContract(pPage);
            else BuildMap(pPage);
        }

        // ---------------------------------------------------------------- map page

        private void BuildMap(RectTransform pPage)
        {
            _frame = UIFactory.Rect(pPage, "Frame", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(FRAME, FRAME));
            Image lBack = _frame.gameObject.AddComponent<Image>();
            lBack.color = new Color(0.09f, 0.1f, 0.11f, 1f);
            _frame.gameObject.AddComponent<RectMask2D>();
            _plan = UIFactory.Stretch(_frame, "Plan").gameObject.AddComponent<RawImage>();
            _plan.raycastTarget = false;
            _icons = UIFactory.Stretch(_frame, "Icons");
            UIFactory.Title(_frame, "North", "N", 22, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(40, 30), true, 0f);

            // Right column: floors, then the legend.
            RectTransform lSide = UIFactory.Rect(pPage, "Side", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(FRAME + 40, 0), new Vector2(320, FRAME));
            UIFactory.Title(lSide, "FloorsTitle", "FLOOR", 15, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, -14), new Vector2(320, 22), true, 5f);
            string[] lNames = { "GROUND", "FIRST", "SECOND", "THIRD" };
            for (int f = 0; f < MapPlanBakerFloors; f++)
            {
                int lFloor = f;
                Button lButton = UIFactory.Button(lSide, lNames[f], new Vector2(0, 1), new Vector2(0, -50 - f * 52), new Vector2(320, 48), UIFactory.Accent2, () => _floor = lFloor, 22);
                _floorButtons.Add(lButton);
            }
            UIFactory.Title(lSide, "LegendTitle", "LEGEND", 15, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, -290), new Vector2(320, 22), true, 5f);
            Legend(lSide, 0, UIIcons.Chevron, ThreatMarkers.Red, "TARGET");
            Legend(lSide, 1, UIIcons.Chevron, UIFactory.Paper, "YOU");
            Legend(lSide, 2, UIIcons.Dot, ThreatMarkers.Blue, "TEAMMATE");
            Legend(lSide, 3, UIIcons.Diamond, ThreatMarkers.Amber, "PROOF");
            Legend(lSide, 4, UIIcons.Ring, UIFactory.Good, "EXTRACTION");
        }

        private const int MapPlanBakerFloors = 4;

        private static void Legend(RectTransform pSide, int pIndex, Sprite pSprite, Color pColor, string pLabel)
        {
            float y = -330 - pIndex * 38;
            Image lIcon = UIFactory.Panel(pSide, "Icon", pColor, new Vector2(0, 1), new Vector2(14, y), new Vector2(18, 18));
            lIcon.sprite = pSprite;
            UIFactory.Title(pSide, "Label", pLabel, 18, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(44, y + 4), new Vector2(276, 26), false, 3f);
        }

        private void OnEnable()
        {
            if (_contract) { RefreshContract(); return; }
            if (PlayerAvatar.Local != null)
            {
                _floor = MapPlan.FloorAt(PlayerAvatar.Local.transform.position);
                if (_zoom > 1.01f) _center = MapPlan.ToUv(PlayerAvatar.Local.transform.position);
            }
        }

        private void Update()
        {
            if (_contract || _frame == null) return;
            ReadNavigation();
            _plan.texture = MapPlan.Floor(_floor);
            float lSpan = 1f / _zoom;
            _center.x = Mathf.Clamp(_center.x, lSpan * 0.5f, 1f - lSpan * 0.5f);
            _center.y = Mathf.Clamp(_center.y, lSpan * 0.5f, 1f - lSpan * 0.5f);
            _plan.uvRect = new Rect(_center.x - lSpan * 0.5f, _center.y - lSpan * 0.5f, lSpan, lSpan);
            for (int f = 0; f < _floorButtons.Count; f++)
            {
                _floorButtons[f].gameObject.SetActive(f < MapPlan.FloorCount);
                _floorButtons[f].GetComponentInChildren<TextMeshProUGUI>().color = f == _floor ? UIFactory.Accent : UIFactory.Paper;
            }

            _used = 0;
            foreach (LevelZone lZone in LevelZone.All) Pin(lZone.transform.position, null, Color.white, 0f, lZone.ZoneName.ToUpperInvariant());
            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null && lDirector.Proof.Value != ProofState.TargetAlive)
            {
                List<MatchPoint> lExtractions = MatchPoint.OfKind(MatchPoint.Kind.Extraction);
                for (int i = 0; i < lExtractions.Count; i++)
                    if ((lDirector.ActiveExtractions.Value & (1 << i)) != 0) Pin(lExtractions[i].transform.position, UIIcons.Ring, UIFactory.Good, 0f, lExtractions[i].Label.ToUpperInvariant());
                if (lDirector.Proof.Value == ProofState.Dropped) Pin(lDirector.ProofPosition.Value, UIIcons.Diamond, ThreatMarkers.Amber, 0f, null);
            }
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.IsTarget && lNpc.Health.IsAlive) Pin(lNpc.transform.position, UIIcons.Chevron, ThreatMarkers.Red, 0f, "TARGET");
            PlayerAvatar lMe = PlayerAvatar.Local;
            foreach (PlayerAvatar lAvatar in PlayerAvatar.All)
            {
                if (lAvatar == null || !lAvatar.Health.IsAlive) continue;
                if (lAvatar == lMe) Pin(lAvatar.transform.position, UIIcons.Chevron, UIFactory.Paper, 180f - lAvatar.LookYaw, null);
                else if (lMe != null && lAvatar.Team.Value == lMe.Team.Value && LobbyState.Instance != null && LobbyState.Instance.Mode.Value != MatchMode.FreeForAll)
                    Pin(lAvatar.transform.position, UIIcons.Dot, ThreatMarkers.Blue, 0f, lAvatar.DisplayName.ToUpperInvariant());
            }
            for (int i = _used; i < _pool.Count; i++) _pool[i].rect.gameObject.SetActive(false);
        }

        /// <summary>Wheel / triggers zoom, drag / left stick pan.</summary>
        private void ReadNavigation()
        {
            float lZoomInput = 0f;
            Vector2 lPan = Vector2.zero;
            Mouse lMouse = Mouse.current;
            if (lMouse != null)
            {
                Vector2 lPosition = lMouse.position.ReadValue();
                bool lOver = RectTransformUtility.RectangleContainsScreenPoint(_frame, lPosition, null);
                if (lOver) lZoomInput += Mathf.Sign(lMouse.scroll.ReadValue().y) * (Mathf.Abs(lMouse.scroll.ReadValue().y) > 0.01f ? 0.25f : 0f);
                if (lMouse.leftButton.wasPressedThisFrame && lOver) _dragging = true;
                if (!lMouse.leftButton.isPressed) _dragging = false;
                if (_dragging)
                {
                    Vector2 lDelta = lPosition - _lastMouse;
                    float lScale = _frame.rect.width * _frame.lossyScale.x;
                    lPan -= lDelta / Mathf.Max(1f, lScale) / _zoom;
                }
                _lastMouse = lPosition;
            }
            Gamepad lPad = Gamepad.current;
            if (lPad != null)
            {
                lPan += lPad.leftStick.ReadValue() * Time.unscaledDeltaTime * 0.45f / _zoom;
                lZoomInput += (lPad.rightTrigger.ReadValue() - lPad.leftTrigger.ReadValue()) * Time.unscaledDeltaTime * 2.5f;
            }
            if (Mathf.Abs(lZoomInput) > 0f) _zoom = Mathf.Clamp(_zoom * (1f + lZoomInput), 1f, 5f);
            _center += lPan;
        }

        /// <summary>A pin on the plan at a world point: an icon (optional, rotated) and a label under it.</summary>
        private void Pin(Vector3 pWorld, Sprite pIcon, Color pColor, float pRotation, string pLabel)
        {
            Rect lView = _plan.uvRect;
            Vector2 lUv = MapPlan.ToUv(pWorld);
            Vector2 p = new Vector2((lUv.x - lView.x) / lView.width - 0.5f, (lUv.y - lView.y) / lView.height - 0.5f) * FRAME;
            if (Mathf.Abs(p.x) > FRAME * 0.5f || Mathf.Abs(p.y) > FRAME * 0.5f) return;
            if (_used >= _pool.Count)
            {
                RectTransform lRect = UIFactory.Rect(_icons, "Pin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
                Image lImage = lRect.gameObject.AddComponent<Image>();
                lImage.raycastTarget = false;
                TextMeshProUGUI lText = UIFactory.Title(_icons, "Label", "", 15, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 22), true, 3f);
                lText.outlineWidth = 0.2f;
                lText.outlineColor = new Color32(12, 13, 15, 255);
                _pool.Add((lRect, lImage, lText));
            }
            (RectTransform rect, Image icon, TextMeshProUGUI label) = _pool[_used++];
            rect.gameObject.SetActive(true);
            rect.anchoredPosition = p;
            rect.localEulerAngles = new Vector3(0, 0, pRotation);
            icon.enabled = pIcon != null;
            icon.sprite = pIcon;
            icon.color = pColor;
            label.gameObject.SetActive(!string.IsNullOrEmpty(pLabel));
            label.text = pLabel;
            label.color = pIcon == null ? new Color(1, 1, 1, 0.75f) : pColor;
            label.fontSize = pIcon == null ? 20 : 14;
            label.rectTransform.anchoredPosition = p + new Vector2(0, pIcon == null ? 0 : -20);
        }

        // ---------------------------------------------------------------- contract page

        private void BuildContract(RectTransform pPage)
        {
            Vector2 L = new Vector2(0, 1);
            _portraitTexture = new RenderTexture(460, 580, 24) { name = "TargetPortrait" };
            _portrait = UIFactory.Rect(pPage, "Portrait", L, L, L, Vector2.zero, new Vector2(460, 580)).gameObject.AddComponent<RawImage>();
            _portrait.texture = _portraitTexture;
            _portrait.raycastTarget = false;
            UIFactory.Title(pPage, "Kicker", "TARGET", 16, ThreatMarkers.Red, TextAlignmentOptions.Left, L, new Vector2(520, -10), new Vector2(600, 24), true, 5f);
            _targetName = UIFactory.Title(pPage, "Name", "", 56, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(520, -60), new Vector2(600, 70), true, 1f);
            _targetLine = UIFactory.Text(pPage, "Line", "", 22, UIFactory.Muted, TextAlignmentOptions.TopLeft, L, new Vector2(520, -150), new Vector2(600, 90));
            UIFactory.Rule(pPage, L, new Vector2(520, -240), 600, false, new Color(1, 1, 1, 0.12f), 1f);
            _targetProof = UIFactory.Title(pPage, "Proof", "", 20, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(520, -270), new Vector2(600, 28), false, 3f);
            _targetState = UIFactory.Title(pPage, "State", "", 20, UIFactory.Accent, TextAlignmentOptions.Left, L, new Vector2(520, -306), new Vector2(600, 28), true, 3f);
        }

        /// <summary>The contract: a portrait of the target rendered now, and its file.</summary>
        private void RefreshContract()
        {
            MatchDirector lDirector = MatchDirector.Instance;
            Data.TargetProfile lTarget = lDirector != null ? lDirector.Target : null;
            _targetName.text = lTarget != null ? lTarget.displayName.ToUpperInvariant() : "";
            _targetLine.text = lTarget != null ? lTarget.dossier : "";
            _targetProof.text = lTarget != null ? "PROOF   <color=#FFFFFF>" + lTarget.proof.ToString().ToUpperInvariant() + "</color>" : "";
            _targetState.text = lDirector == null ? "" : lDirector.Proof.Value switch
            {
                ProofState.TargetAlive => "STATUS   ALIVE",
                ProofState.Dropped => "STATUS   DOWN  -  PROOF ON THE GROUND",
                ProofState.Carried => "STATUS   DOWN  -  PROOF CARRIED",
                _ => "STATUS   EXTRACTED"
            };

            Npc lNpc = null;
            foreach (Npc lCandidate in Npc.All) if (lCandidate != null && lCandidate.IsTarget) lNpc = lCandidate;
            GameObject lCameraObject = new GameObject("PortraitCamera");
            Camera lCamera = lCameraObject.AddComponent<Camera>();
            lCamera.clearFlags = CameraClearFlags.SolidColor;
            lCamera.backgroundColor = new Color(0.12f, 0.13f, 0.15f);
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
                GL.Clear(true, true, new Color(0.12f, 0.13f, 0.15f));
                RenderTexture.active = lActive;
            }
            lCamera.targetTexture = null;
            Destroy(lCameraObject);
        }

        private void OnDestroy()
        {
            if (_portraitTexture != null) _portraitTexture.Release();
        }
    }
}
