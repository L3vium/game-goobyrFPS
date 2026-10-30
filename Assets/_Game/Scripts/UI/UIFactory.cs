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
    /// Builds the game's UI in code: chunky rounded panels, bold outlined text, buttons that
    /// squash on hover and click. One look for every screen (title, lobby, HUD, map, end).
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color Ink = new Color(0.12f, 0.12f, 0.18f);
        public static readonly Color Paper = new Color(0.98f, 0.96f, 0.92f);
        public static readonly Color Accent = new Color(1f, 0.42f, 0.45f);
        public static readonly Color Accent2 = new Color(0.36f, 0.72f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.9f, 0.5f);
        public static readonly Color PanelColor = new Color(0.1f, 0.1f, 0.16f, 0.82f);

        private static Sprite _rounded;

        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int SIZE = 64, RADIUS = 18;
                Texture2D lTexture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < SIZE; y++)
                    for (int x = 0; x < SIZE; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(RADIUS - x, x - (SIZE - 1 - RADIUS)));
                        float dy = Mathf.Max(0, Mathf.Max(RADIUS - y, y - (SIZE - 1 - RADIUS)));
                        float a = Mathf.Clamp01(RADIUS - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        lTexture.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                lTexture.Apply();
                _rounded = Sprite.Create(lTexture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(RADIUS, RADIUS, RADIUS, RADIUS));
                return _rounded;
            }
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

        public static RectTransform Stretch(Transform pParent, string pName)
        {
            RectTransform lRect = Rect(pParent, pName, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return lRect;
        }

        public static Image Panel(Transform pParent, string pName, Color pColor, Vector2 pAnchor, Vector2 pPosition, Vector2 pSize)
        {
            RectTransform lRect = Rect(pParent, pName, pAnchor, pAnchor, pAnchor, pPosition, pSize);
            Image lImage = lRect.gameObject.AddComponent<Image>();
            lImage.sprite = Rounded;
            lImage.type = Image.Type.Sliced;
            lImage.color = pColor;
            return lImage;
        }

        public static TextMeshProUGUI Text(Transform pParent, string pName, string pText, float pSize, Color pColor, TextAlignmentOptions pAlign, Vector2 pAnchor, Vector2 pPosition, Vector2 pBox)
        {
            RectTransform lRect = Rect(pParent, pName, pAnchor, pAnchor, pAnchor, pPosition, pBox);
            TextMeshProUGUI lText = lRect.gameObject.AddComponent<TextMeshProUGUI>();
            lText.text = pText;
            lText.fontSize = pSize;
            lText.color = pColor;
            lText.alignment = pAlign;
            lText.fontStyle = FontStyles.Bold;
            lText.raycastTarget = false;
            lText.outlineWidth = 0.18f;
            lText.outlineColor = new Color32(15, 15, 25, 220);
            lText.textWrappingMode = TextWrappingModes.Normal;
            return lText;
        }

        public static Button Button(Transform pParent, string pLabel, Vector2 pAnchor, Vector2 pPosition, Vector2 pSize, Color pColor, Action pOnClick, float pFont = 34f)
        {
            Image lImage = Panel(pParent, "Btn_" + pLabel, pColor, pAnchor, pPosition, pSize);
            Button lButton = lImage.gameObject.AddComponent<Button>();
            ColorBlock lColors = lButton.colors;
            lColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            lColors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            lColors.fadeDuration = 0.05f;
            lButton.colors = lColors;
            TextMeshProUGUI lText = Text(lImage.transform, "Label", pLabel, pFont, Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, pSize);
            lText.textWrappingMode = TextWrappingModes.NoWrap;
            lButton.onClick.AddListener(() =>
            {
                Audio.Sfx.Play2D("click", 0.7f);
                pOnClick?.Invoke();
            });
            lImage.gameObject.AddComponent<ButtonJuice>();
            return lButton;
        }

        public static TMP_InputField Input(Transform pParent, string pPlaceholder, Vector2 pAnchor, Vector2 pPosition, Vector2 pSize)
        {
            Image lBack = Panel(pParent, "Input", new Color(1, 1, 1, 0.92f), pAnchor, pPosition, pSize);
            RectTransform lArea = Stretch(lBack.transform, "TextArea");
            lArea.offsetMin = new Vector2(16, 6);
            lArea.offsetMax = new Vector2(-16, -6);
            lArea.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI lPlaceholder = Text(lArea, "Placeholder", pPlaceholder, 30, new Color(0.4f, 0.4f, 0.45f), TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), Vector2.zero, pSize - new Vector2(32, 12));
            lPlaceholder.outlineWidth = 0f;
            lPlaceholder.fontStyle = FontStyles.Italic;
            TextMeshProUGUI lText = Text(lArea, "Text", "", 30, Ink, TextAlignmentOptions.Left, new Vector2(0.5f, 0.5f), Vector2.zero, pSize - new Vector2(32, 12));
            lText.outlineWidth = 0f;
            TMP_InputField lInput = lBack.gameObject.AddComponent<TMP_InputField>();
            lInput.textViewport = lArea;
            lInput.textComponent = lText;
            lInput.placeholder = lPlaceholder;
            return lInput;
        }

        public static Toggle Toggle(Transform pParent, string pLabel, bool pValue, Vector2 pAnchor, Vector2 pPosition, Action<bool> pOnChange)
        {
            RectTransform lRoot = Rect(pParent, "Toggle_" + pLabel, pAnchor, pAnchor, pAnchor, pPosition, new Vector2(520, 56));
            Image lBox = Panel(lRoot, "Box", Paper, new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(48, 48));
            Image lCheck = Panel(lBox.transform, "Check", Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            Text(lRoot, "Label", pLabel, 30, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(300, 0), new Vector2(440, 56));
            Toggle lToggle = lRoot.gameObject.AddComponent<Toggle>();
            lToggle.targetGraphic = lBox;
            lToggle.graphic = lCheck;
            lToggle.isOn = pValue;
            lToggle.onValueChanged.AddListener(v => { Audio.Sfx.Play2D("click", 0.6f); pOnChange?.Invoke(v); });
            return lToggle;
        }

        public static Slider Slider(Transform pParent, string pLabel, float pMin, float pMax, float pValue, Vector2 pAnchor, Vector2 pPosition, Action<float> pOnChange)
        {
            RectTransform lRoot = Rect(pParent, "Slider_" + pLabel, pAnchor, pAnchor, pAnchor, pPosition, new Vector2(620, 60));
            Text(lRoot, "Label", pLabel, 28, Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(240, 56));
            Image lTrack = Panel(lRoot, "Track", new Color(1, 1, 1, 0.25f), new Vector2(1, 0.5f), new Vector2(-180, 0), new Vector2(340, 16));
            RectTransform lFillArea = Stretch(lTrack.transform, "FillArea");
            Image lFill = Panel(lFillArea, "Fill", Accent2, new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 16));
            lFill.rectTransform.anchorMin = new Vector2(0, 0);
            lFill.rectTransform.anchorMax = new Vector2(0, 1);
            RectTransform lHandleArea = Stretch(lTrack.transform, "HandleArea");
            Image lHandle = Panel(lHandleArea, "Handle", Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
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
    }

    /// <summary>Hover / press squash on buttons.</summary>
    public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private float _target = 1f, _scale = 1f, _velocity;

        public void OnPointerEnter(PointerEventData pData) { _target = 1.06f; Audio.Sfx.Play2D("hover", 0.25f); }
        public void OnPointerExit(PointerEventData pData) => _target = 1f;
        public void OnPointerDown(PointerEventData pData) => _velocity -= 4f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _velocity += ((_target - _scale) * 300f - _velocity * 18f) * dt;
            _scale += _velocity * dt;
            transform.localScale = new Vector3(_scale, 2f - _scale, 1f);
        }
    }
}
