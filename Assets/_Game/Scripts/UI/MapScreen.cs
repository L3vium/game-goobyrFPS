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
    /// zoom on the cursor with the wheel or the triggers, pan with any mouse button or the stick; only
    /// the target, you, your teammates and the extractions on it.</item>
    /// <item><b>Contract</b>: the target's portrait rendered when the page opens, its name, its file
    /// (who, habits, where, protection), the orders and where the contract stands.</item>
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
            UIFactory.Title(lSide, "LegendTitle", "LEGEND", 15, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, -280), new Vector2(320, 22), true, 5f);
            AddLegend(lSide, 0, UIIcons.Chevron, ThreatMarkers.Red, "TARGET");
            AddLegend(lSide, 1, UIIcons.Arrow, UIFactory.Accent, "YOU");
            AddLegend(lSide, 2, UIIcons.Dot, ThreatMarkers.Blue, "TEAMMATES");
            AddLegend(lSide, 3, UIIcons.Ring, UIFactory.Good, "EXTRACTIONS");
        }

        private const int MapPlanBakerFloors = 4;

        /// <summary>A legend line: its icon and name.</summary>
        private void AddLegend(RectTransform pSide, int pIndex, Sprite pSprite, Color pColor, string pLabel)
        {
            float y = -316 - pIndex * 38;
            Image lIcon = UIFactory.Panel(pSide, "LegendIcon", pColor, new Vector2(0, 1), new Vector2(11, y), new Vector2(18, 18));
            lIcon.sprite = pSprite;
            TextMeshProUGUI lText = UIFactory.Title(pSide, "Legend", pLabel, 18, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(36, y), new Vector2(280, 28), true, 3f);
            lText.rectTransform.pivot = new Vector2(0, 0.5f);
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
            // No plan baked for this map: a dark sheet, never a white square.
            bool lReady = MapPlan.Ready;
            _plan.texture = lReady ? MapPlan.Floor(_floor) : null;
            _plan.color = lReady ? Color.white : new Color(0.08f, 0.09f, 0.1f, 0.95f);
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
            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null && lDirector.Proof.Value != ProofState.TargetAlive)
            {
                List<MatchPoint> lExtractions = MatchPoint.OfKind(MatchPoint.Kind.Extraction);
                for (int i = 0; i < lExtractions.Count; i++)
                        if ((lDirector.ActiveExtractions.Value & (1 << i)) != 0) Pin(lExtractions[i].transform.position, UIIcons.Ring, UIFactory.Good, 0f, lExtractions[i].Label.ToUpperInvariant(), 26f, true);
                if (lDirector.Proof.Value == ProofState.Carried && (PlayerAvatar.Local == null || !PlayerAvatar.Local.CarriesProof.Value) && Hud.CarrierPing.HasValue)
                    Pin(Hud.CarrierPing.Value, UIIcons.Diamond, Hud.CARRIER_YELLOW, 0f, "PROOF");
                if (lDirector.Proof.Value == ProofState.Dropped) Pin(lDirector.ProofPosition.Value, UIIcons.Diamond, ThreatMarkers.Amber, 0f, "PROOF", 24f, true);
            }
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.IsTarget && lNpc.Health.IsAlive) Pin(lNpc.transform.position, UIIcons.Chevron, ThreatMarkers.Red, 180f, "TARGET", 30f, true);
            PlayerAvatar lMe = PlayerAvatar.Local;
            foreach (PlayerAvatar lAvatar in PlayerAvatar.All)
            {
                if (lAvatar == null || !lAvatar.Health.IsAlive || lAvatar == lMe) continue;
                if (lMe != null && lAvatar.Team.Value == lMe.Team.Value && LobbyState.Instance != null && LobbyState.Instance.Mode.Value != MatchMode.FreeForAll)
                    Pin(lAvatar.transform.position, UIIcons.Dot, ThreatMarkers.Blue, 0f, lAvatar.DisplayName.ToUpperInvariant(), 20f, true);
            }
            // You, last so it draws on top: a big amber arrow on a dark halo, kept on the edge when out of view.
            if (lMe != null && lMe.Health.IsAlive) Pin(lMe.transform.position, UIIcons.Arrow, UIFactory.Accent, -lMe.LookYaw, "YOU", 40f, true);
            // A pin and its label are siblings: both go, or the name stays behind where it last was.
            for (int i = _used; i < _pool.Count; i++)
            {
                _pool[i].rect.gameObject.SetActive(false);
                _pool[i].label.gameObject.SetActive(false);
            }
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
                if (lOver)
                {
                    float lScroll = lMouse.scroll.ReadValue().y;
                    if (Mathf.Abs(lScroll) > 0.01f) ZoomAt(lPosition, 1f + Mathf.Sign(lScroll) * 0.25f);
                }
                bool lPressed = lMouse.leftButton.wasPressedThisFrame || lMouse.rightButton.wasPressedThisFrame || lMouse.middleButton.wasPressedThisFrame;
                if (lPressed && lOver) _dragging = true;
                if (!lMouse.leftButton.isPressed && !lMouse.rightButton.isPressed && !lMouse.middleButton.isPressed) _dragging = false;
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

        /// <summary>Zooms keeping what is under the cursor under the cursor.</summary>
        private void ZoomAt(Vector2 pScreen, float pFactor)
        {
            float lBefore = _zoom;
            _zoom = Mathf.Clamp(_zoom * pFactor, 1f, 6f);
            if (Mathf.Approximately(lBefore, _zoom)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_frame, pScreen, null, out Vector2 lLocal)) return;
            // From the frame's middle, as a share of its width (the frame's pivot sits on its left edge).
            Vector2 lOffset = new Vector2(lLocal.x - _frame.rect.center.x, lLocal.y - _frame.rect.center.y) / _frame.rect.width;
            _center += lOffset * (1f / lBefore - 1f / _zoom);
        }

        /// <summary>
        /// A pin on the plan at a world point: an icon (optional, rotated) over a dark halo so it reads
        /// on any ground, and a label under it. Clamped pins stay on the frame edge when out of view.
        /// </summary>
        private void Pin(Vector3 pWorld, Sprite pIcon, Color pColor, float pRotation, string pLabel, float pSize = 22f, bool pClamp = false)
        {
            Rect lView = _plan.uvRect;
            Vector2 lUv = MapPlan.ToUv(pWorld);
            Vector2 p = new Vector2((lUv.x - lView.x) / lView.width - 0.5f, (lUv.y - lView.y) / lView.height - 0.5f) * FRAME;
            float lEdge = FRAME * 0.5f - pSize * 0.6f;
            if (Mathf.Abs(p.x) > lEdge || Mathf.Abs(p.y) > lEdge)
            {
                if (!pClamp) return;
                p = new Vector2(Mathf.Clamp(p.x, -lEdge, lEdge), Mathf.Clamp(p.y, -lEdge, lEdge));
            }
            if (_used >= _pool.Count)
            {
                RectTransform lRect = UIFactory.Rect(_icons, "Pin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
                Image lHalo = UIFactory.Stretch(lRect, "Halo").gameObject.AddComponent<Image>();
                lHalo.sprite = UIIcons.Disc;
                lHalo.color = new Color(0.04f, 0.05f, 0.06f, 0.7f);
                lHalo.raycastTarget = false;
                ((RectTransform)lHalo.transform).sizeDelta = new Vector2(10, 10);
                Image lImage = UIFactory.Stretch(lRect, "Icon").gameObject.AddComponent<Image>();
                lImage.raycastTarget = false;
                TextMeshProUGUI lText = UIFactory.Title(_icons, "Label", "", 15, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 22), true, 3f);
                lText.outlineWidth = 0.2f;
                lText.outlineColor = new Color32(12, 13, 15, 255);
                _pool.Add((lRect, lImage, lText));
            }
            (RectTransform rect, Image icon, TextMeshProUGUI label) = _pool[_used++];
            rect.gameObject.SetActive(true);
            rect.anchoredPosition = p;
            rect.sizeDelta = new Vector2(pSize, pSize);
            rect.GetChild(0).gameObject.SetActive(pIcon != null);
            rect.localEulerAngles = new Vector3(0, 0, pRotation);
            icon.enabled = pIcon != null;
            icon.sprite = pIcon;
            icon.color = pColor;
            label.gameObject.SetActive(!string.IsNullOrEmpty(pLabel));
            label.text = pLabel;
            label.color = pIcon == null ? new Color(1, 1, 1, 0.75f) : pColor;
            label.fontSize = pIcon == null ? 20 : 14;
            label.rectTransform.anchoredPosition = p + new Vector2(0, pIcon == null ? 0 : -(pSize * 0.5f + 10f));
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
            _targetLine = UIFactory.Text(pPage, "File", "", 22, UIFactory.Paper, TextAlignmentOptions.TopLeft, L, Vector2.zero, new Vector2(760, 420));
            _targetLine.rectTransform.pivot = L;
            _targetLine.rectTransform.anchoredPosition = new Vector2(520, -130);
            UIFactory.Rule(pPage, L, new Vector2(520, -570), 760, false, new Color(1, 1, 1, 0.12f), 1f);
            _targetProof = UIFactory.Title(pPage, "Proof", "", 20, UIFactory.Paper, TextAlignmentOptions.Left, L, new Vector2(520, -600), new Vector2(760, 28), false, 3f);
            _targetState = UIFactory.Title(pPage, "State", "", 20, UIFactory.Accent, TextAlignmentOptions.Left, L, new Vector2(520, -636), new Vector2(760, 28), true, 3f);
        }

        /// <summary>Everything worth knowing on the target: who it is, where it spends its day, who is with it, and what to do.</summary>
        private static string ContractFile(Data.TargetProfile pTarget, bool pProtect, bool pGuarding)
        {
            const string HEAD = "<font=\"BarlowCondensed-Bold SDF\"><color=#9EA0A6>";
            const string TAIL = "</color></font>\n";
            Data.MapDefinition lMap = LobbyState.Instance != null ? LobbyState.Instance.Map : null;
            string lFile = pTarget.dossier + "\n\n";
            if (lMap != null && pTarget.zone >= 0 && pTarget.zone < lMap.zoneNames.Length) lFile += HEAD + "WHERE" + TAIL + lMap.zoneNames[pTarget.zone] + "\n\n";
            if (pTarget.routine != null && pTarget.routine.Count > 0)
            {
                List<string> lHabits = new List<string>();
                foreach (Data.ActivityType lActivity in pTarget.routine) lHabits.Add(lActivity.ToString().ToLowerInvariant());
                lFile += HEAD + "HABITS" + TAIL + string.Join(", ", lHabits) + "\n\n";
            }
            lFile += HEAD + "PROTECTION" + TAIL + (pTarget.bodyguards > 0 ? pTarget.bodyguards + " bodyguards never far, and every guard on the map." : "Every guard on the map.") + "\n\n";
            string lOrders = pGuarding ? "Keep the target alive. Hunt down every agent who comes for it; the guards are on your side and your radio tells you what they see."
                : pProtect ? "Kill the target: that is all it takes. Another team of agents is guarding it and hears everything the guards see."
                : "Kill the target, take the proof from its body and carry it to an extraction point. Anyone can take it from you.";
            return lFile + HEAD + "ORDERS" + TAIL + lOrders;
        }

        /// <summary>The contract: a portrait of the target rendered now, and its file.</summary>
        private void RefreshContract()
        {
            MatchDirector lDirector = MatchDirector.Instance;
            Data.TargetProfile lTarget = lDirector != null ? lDirector.Target : null;
            _targetName.text = lTarget != null ? lTarget.displayName.ToUpperInvariant() : "";
            bool lProtect = lDirector != null && lDirector.Protect;
            bool lGuarding = lProtect && PlayerAvatar.Local != null && PlayerAvatar.Local.IsProtector;
            _targetLine.text = lTarget != null ? ContractFile(lTarget, lProtect, lGuarding) : "";
            _targetProof.text = lTarget != null && !lProtect ? "PROOF   <color=#FFFFFF>" + lTarget.proof.ToString().ToUpperInvariant() + "</color>" : "";
            _targetState.text = lDirector == null ? "" : lDirector.Proof.Value switch
            {
                ProofState.TargetAlive => "STATUS   ALIVE",
                ProofState.Dropped => "STATUS   DOWN  -  PROOF ON THE GROUND",
                ProofState.Carried => "STATUS   DOWN  -  PROOF CARRIED",
                _ => "STATUS   EXTRACTED"
            };

            // The target's photo, shot in the portrait studio (its real body, studio lights).
            if (lTarget != null) PortraitStudio.Shoot(FX.HumanCast.ForTarget(lTarget), _portraitTexture);
            else
            {
                RenderTexture lActive = RenderTexture.active;
                RenderTexture.active = _portraitTexture;
                GL.Clear(true, true, new Color(0.12f, 0.13f, 0.15f));
                RenderTexture.active = lActive;
            }
        }

        private void OnDestroy()
        {
            if (_portraitTexture != null) _portraitTexture.Release();
        }
    }
}
