using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// Builds the game's UI in code, in one restrained look: flat near-black panels, white
    /// typography in Barlow (condensed caps for headings, letter-spaced), thin rules, a single red
    /// accent. Motion is quiet - fades, short slides, an accent bar on the hovered item - never
    /// squash and stretch (that belongs to the game world, not the menus).
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(0.04f, 0.04f, 0.05f);
        public static readonly Color Paper = new Color(0.95f, 0.95f, 0.94f);
        public static readonly Color Muted = new Color(0.62f, 0.63f, 0.65f);
        /// <summary>Signal amber: the one accent of the interface (red is kept for the target and danger).</summary>
        public static readonly Color Accent = new Color(0.96f, 0.63f, 0.17f);
        public static readonly Color Danger = new Color(0.9f, 0.2f, 0.18f);
        public static readonly Color Accent2 = new Color(0.95f, 0.95f, 0.94f);
        public static readonly Color Good = new Color(0.45f, 0.82f, 0.55f);
        public static readonly Color PanelColor = new Color(0.03f, 0.03f, 0.04f, 0.78f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.14f);

        private static TMP_FontAsset _body, _bodyBold, _heading, _headingBold;

        public static TMP_FontAsset Body => _body != null ? _body : _body = LoadFont("Barlow-Medium SDF");
        public static TMP_FontAsset BodyBold => _bodyBold != null ? _bodyBold : _bodyBold = LoadFont("Barlow-SemiBold SDF");
        public static TMP_FontAsset Heading => _heading != null ? _heading : _heading = LoadFont("BarlowCondensed-SemiBold SDF");
        public static TMP_FontAsset HeadingBold => _headingBold != null ? _headingBold : _headingBold = LoadFont("BarlowCondensed-Bold SDF");

        private static TMP_FontAsset LoadFont(string pName)
        {
            TMP_FontAsset lFont = Resources.Load<TMP_FontAsset>("Fonts/" + pName);
            return lFont != null ? lFont : TMP_Settings.defaultFontAsset;
        }

        public static Canvas Canvas(string pName, int pOrder)
        {
            GameObject lObject = new GameObject(pName);
            Canvas lCanvas = lObject.AddComponent<Canvas>();
            lCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            lCanvas.sortingOrder = pOrder;
            CanvasScaler lScaler = lObject.AddComponent<CanvasScaler>();
            lScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            lScaler.referenceResolution = new Vector2(1920, 1080);
            lScaler.matchWidthOrHeight = 0.5f;
            lObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return lCanvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject lSystem = new GameObject("EventSystem");
            lSystem.AddComponent<EventSystem>();
            lSystem.AddComponent<InputSystemUIInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(lSystem);
        }

        public static RectTransform Rect(Transform pParent, string pName, Vector2 pAnchorMin, Vector2 pAnchorMax, Vector2 pPivot, Vector2 pPosition, Vector2 pSize)
        {
            GameObject lObject = new GameObject(pName, typeof(RectTransform));
            RectTransform lRect = lObject.GetComponent<RectTransform>();
            lRect.SetParent(pParent, false);
            lRect.anchorMin = pAnchorMin;
            lRect.anchorMax = pAnchorMax;
            lRect.pivot = pPivot;
            lRect.anchoredPosition = pPosition;
            lRect.sizeDelta = pSize;
            return lRect;
        }

        public static RectTransform Stretch(Transform pParent, string pName) => Rect(pParent, pName, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        /// <summary>A flat panel (no rounding: hard edges read as "professional").</summary>
        public static Image Panel(Transform pParent, string pName, Color pColor, Vector2 pAnchor, Vector2 pPosition, Vector2 pSize)
        {
            RectTransform lRect = Rect(pParent, pName, pAnchor, pAnchor, pAnchor, pPosition, pSize);
            Image lImage = lRect.gameObject.AddComponent<Image>();
            lImage.color = pColor;
            lImage.raycastTarget = false;
            return lImage;
        }

        /// <summary>A hairline (1-2 px) rule.</summary>
        public static Image Rule(Transform pParent, Vector2 pAnchor, Vector2 pPosition, float pLength, bool pVertical = false, Color? pColor = null, float pThickness = 2f)
        {
            return Panel(pParent, "Rule", pColor ?? Line, pAnchor, pPosition, pVertical ? new Vector2(pThickness, pLength) : new Vector2(pLength, pThickness));
        }

        public static TextMeshProUGUI Text(Transform pParent, string pName, string pText, float pSize, Color pColor, TextAlignmentOptions pAlign, Vector2 pAnchor, Vector2 pPosition, Vector2 pBox)
        {
            RectTransform lRect = Rect(pParent, pName, pAnchor, pAnchor, pAnchor, pPosition, pBox);
            TextMeshProUGUI lText = lRect.gameObject.AddComponent<TextMeshProUGUI>();
            lText.font = Body;
            lText.text = pText;
            lText.fontSize = pSize * 0.92f;
            lText.color = pColor;
            lText.alignment = pAlign;
            lText.raycastTarget = false;
            lText.textWrappingMode = TextWrappingModes.Normal;
            return lText;
        }

        /// <summary>Condensed capitals with tracking: titles, section names, big numbers.</summary>
        public static TextMeshProUGUI Title(Transform pParent, string pName, string pText, float pSize, Color pColor, TextAlignmentOptions pAlign, Vector2 pAnchor, Vector2 pPosition, Vector2 pBox, bool pBold = false, float pTracking = 6f)
        {
            TextMeshProUGUI lText = Text(pParent, pName, pText.ToUpperInvariant(), pSize, pColor, pAlign, pAnchor, pPosition, pBox);
            lText.font = pBold ? HeadingBold : Heading;
            lText.fontSize = pSize;
            lText.characterSpacing = pTracking;
            lText.textWrappingMode = TextWrappingModes.NoWrap;
            return lText;
        }

        /// <summary>
        /// A menu entry: text on a transparent row that lights up on hover (soft white wash, red
        /// bar on the left, text slides 8 px). Pass <see cref="Accent"/> for a primary filled button.
        /// </summary>
        public static Button Button(Transform pParent, string pLabel, Vector2 pAnchor, Vector2 pPosition, Vector2 pSize, Color pColor, Action pOnClick, float pFont = 34f)
        {
            bool lPrimary = pColor == Accent || pColor == Good;
            RectTransform lRoot = Rect(pParent, "Btn_" + pLabel, pAnchor, pAnchor, pAnchor, pPosition, pSize);
            Image lBack = lRoot.gameObject.AddComponent<Image>();
            lBack.color = lPrimary ? pColor : new Color(1, 1, 1, 0f);
            Button lButton = lRoot.gameObject.AddComponent<Button>();
            lButton.transition = Selectable.Transition.None;
            lButton.targetGraphic = lBack;

            Image lBar = Panel(lRoot, "Bar", Accent, new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(4, pSize.y));
            lBar.rectTransform.localScale = new Vector3(1, 0, 1);
            TextMeshProUGUI lText = Title(lRoot, "Label", pLabel, Mathf.Min(pFont, pSize.y * 0.6f), Paper, lPrimary ? TextAlignmentOptions.Center : TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), new Vector2(lPrimary ? 0 : 14, 0), pSize - new Vector2(40, 0), true, 4f);

            ButtonFx lFx = lRoot.gameObject.AddComponent<ButtonFx>();
            lFx.Init(lBack, lBar, lText, lPrimary, pColor);
            lButton.onClick.AddListener(() =>
            {
                Audio.Sfx.Play2D("click", 0.5f);
                pOnClick?.Invoke();
            });
            return lButton;
        }

        public static TMP_InputField Input(Transform pParent, string pPlaceholder, Vector2 pAnchor, Vector2 pPosition, Vector2 pSize)
        {
            Image lBack = Panel(pParent, "Input", new Color(1, 1, 1, 0.06f), pAnchor, pPosition, pSize);
            lBack.raycastTarget = true;
            Rule(lBack.transform, new Vector2(0.5f, 0), new Vector2(0, 1), pSize.x, false, new Color(1, 1, 1, 0.5f));
            RectTransform lArea = Stretch(lBack.transform, "TextArea");
            lArea.offsetMin = new Vector2(16, 4);
            lArea.offsetMax = new Vector2(-16, -4);
            lArea.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI lPlaceholder = Text(lArea, "Placeholder", pPlaceholder, 26, Muted, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), Vector2.zero, pSize - new Vector2(32, 8));
            TextMeshProUGUI lText = Text(lArea, "Text", "", 28, Paper, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), Vector2.zero, pSize - new Vector2(32, 8));
            TMP_InputField lInput = lBack.gameObject.AddComponent<TMP_InputField>();
            lInput.textViewport = lArea;
            lInput.textComponent = lText;
            lInput.placeholder = lPlaceholder;
            lInput.caretColor = Accent;
            lInput.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.4f);
            return lInput;
        }

        public static Toggle Toggle(Transform pParent, string pLabel, bool pValue, Vector2 pAnchor, Vector2 pPosition, Action<bool> pOnChange)
        {
            RectTransform lRoot = Rect(pParent, "Toggle_" + pLabel, pAnchor, pAnchor, pAnchor, pPosition, new Vector2(560, 48));
            Image lBox = Panel(lRoot, "Box", new Color(1, 1, 1, 0.12f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(26, 26));
            lBox.raycastTarget = true;
            Image lCheck = Panel(lBox.transform, "Check", Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            Title(lRoot, "Label", pLabel, 26, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(300, 0), new Vector2(500, 48), false, 3f);
            Toggle lToggle = lRoot.gameObject.AddComponent<Toggle>();
            lToggle.targetGraphic = lBox;
            lToggle.graphic = lCheck;
            lToggle.isOn = pValue;
            lToggle.onValueChanged.AddListener(v => { Audio.Sfx.Play2D("click", 0.4f); pOnChange?.Invoke(v); });
            return lToggle;
        }

        public static Slider Slider(Transform pParent, string pLabel, float pMin, float pMax, float pValue, Vector2 pAnchor, Vector2 pPosition, Action<float> pOnChange)
        {
            RectTransform lRoot = Rect(pParent, "Slider_" + pLabel, pAnchor, pAnchor, pAnchor, pPosition, new Vector2(640, 52));
            Title(lRoot, "Label", pLabel, 26, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(130, 0), new Vector2(260, 52), false, 3f);
            Image lTrack = Panel(lRoot, "Track", new Color(1, 1, 1, 0.18f), new Vector2(1, 0.5f), new Vector2(-190, 0), new Vector2(360, 3));
            RectTransform lFillArea = Stretch(lTrack.transform, "FillArea");
            Image lFill = Panel(lFillArea, "Fill", Accent, new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 3));
            lFill.rectTransform.anchorMin = new Vector2(0, 0);
            lFill.rectTransform.anchorMax = new Vector2(0, 1);
            RectTransform lHandleArea = Stretch(lTrack.transform, "HandleArea");
            Image lHandle = Panel(lHandleArea, "Handle", Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 24));
            lHandle.raycastTarget = true;
            Slider lSlider = lRoot.gameObject.AddComponent<Slider>();
            lSlider.fillRect = lFill.rectTransform;
            lSlider.handleRect = lHandle.rectTransform;
            lSlider.targetGraphic = lHandle;
            lSlider.minValue = pMin;
            lSlider.maxValue = pMax;
            lSlider.value = pValue;
            lSlider.onValueChanged.AddListener(v => pOnChange?.Invoke(v));
            return lSlider;
        }

        /// <summary>Fades a whole group in (screen transitions).</summary>
        public static CanvasGroup FadeIn(GameObject pTarget, float pDuration = 0.25f, Vector2? pSlide = null)
        {
            // Unity's "fake null" defeats ??: test explicitly.
            CanvasGroup lGroup = pTarget.GetComponent<CanvasGroup>();
            if (lGroup == null) lGroup = pTarget.AddComponent<CanvasGroup>();
            UIFade lFade = pTarget.GetComponent<UIFade>();
            if (lFade == null) lFade = pTarget.AddComponent<UIFade>();
            lFade.Play(lGroup, pDuration, pSlide ?? new Vector2(0, -12));
            return lGroup;
        }
    }

    /// <summary>Quiet hover feedback: background wash, accent bar, 8 px slide of the label.</summary>
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private Image _back, _bar;
        private TextMeshProUGUI _label;
        private bool _primary, _hover, _down;
        private Color _base;
        private float _t;
        private Vector2 _labelRest;

        public void Init(Image pBack, Image pBar, TextMeshProUGUI pLabel, bool pPrimary, Color pBase)
        {
            _back = pBack;
            _bar = pBar;
            _label = pLabel;
            _primary = pPrimary;
            _base = pBase;
            _labelRest = pLabel.rectTransform.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData pData) { _hover = true; Audio.Sfx.Play2D("hover", 0.15f); }
        public void OnPointerExit(PointerEventData pData) { _hover = false; _down = false; }
        public void OnSelect(BaseEventData pData) { _hover = true; Audio.Sfx.Play2D("hover", 0.15f); }
        public void OnDeselect(BaseEventData pData) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData pData) => _down = true;
        public void OnPointerUp(PointerEventData pData) => _down = false;

        private void Update()
        {
            Selectable lSelectable = GetComponent<Selectable>();
            bool lEnabled = lSelectable == null || lSelectable.interactable;
            _t = Mathf.MoveTowards(_t, _hover && lEnabled ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float e = 1f - (1f - _t) * (1f - _t);
            if (_primary)
            {
                Color lColor = Color.Lerp(_base, Color.Lerp(_base, Color.white, 0.18f), e);
                if (_down) lColor *= 0.85f;
                _back.color = lEnabled ? lColor : new Color(0.3f, 0.3f, 0.32f, 0.8f);
                _bar.rectTransform.localScale = new Vector3(1, 0, 1);
            }
            else
            {
                _back.color = new Color(1, 1, 1, (_down ? 0.16f : 0.08f) * e);
                _bar.rectTransform.localScale = new Vector3(1, e, 1);
                _label.rectTransform.anchoredPosition = _labelRest + new Vector2(8f * e, 0);
            }
            // Dark ink on the amber fill, light text on the transparent rows.
            _label.color = !lEnabled ? UIFactory.Muted : _primary ? UIFactory.Ink : Color.Lerp(new Color(0.86f, 0.86f, 0.86f), Color.white, e);
        }
    }

    /// <summary>A short fade + slide in, on unscaled time.</summary>
    public class UIFade : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _rect;
        private Vector2 _rest, _from;
        private float _t = 1f, _duration = 0.25f;

        public void Play(CanvasGroup pGroup, float pDuration, Vector2 pSlide)
        {
            _group = pGroup;
            _rect = (RectTransform)transform;
            if (_t >= 1f) _rest = _rect.anchoredPosition;
            _from = _rest + pSlide;
            _duration = pDuration;
            _t = 0f;
            enabled = true;
        }

        private void Update()
        {
            _t = Mathf.MoveTowards(_t, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, _duration));
            float e = 1f - Mathf.Pow(1f - _t, 3f);
            _group.alpha = e;
            _rect.anchoredPosition = Vector2.Lerp(_from, _rest, e);
            if (_t >= 1f) enabled = false;
        }
    }
}
