using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// Layout-driven settings widgets, standard PC/console conventions: a list is a vertical layout
    /// (nothing can overlap), each row has its label on the left and its control right-aligned -
    /// a switch, a slider with its value, a stepper (‹ value ›) or a button.
    /// </summary>
    public static class UIWidgets
    {
        public const float ROW_HEIGHT = 56f;
        private static readonly Color ROW_BACK = new Color(1f, 1f, 1f, 0.035f);
        private static readonly Color TRACK = new Color(1f, 1f, 1f, 0.16f);

        /// <summary>A vertical list filling its parent's width, rows stacked from the top.</summary>
        public static RectTransform List(Transform pParent, string pName, Vector2 pAnchor, Vector2 pPosition, float pWidth, float pSpacing = 6f)
        {
            RectTransform lList = UIFactory.Rect(pParent, pName, pAnchor, pAnchor, new Vector2(0.5f, 1f), pPosition, new Vector2(pWidth, 0));
            VerticalLayoutGroup lLayout = lList.gameObject.AddComponent<VerticalLayoutGroup>();
            lLayout.spacing = pSpacing;
            lLayout.childControlWidth = true;
            lLayout.childControlHeight = true;
            lLayout.childForceExpandWidth = true;
            lLayout.childForceExpandHeight = false;
            lList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return lList;
        }

        /// <summary>A small-caps section title with a short accent rule, inside a list.</summary>
        public static void Section(RectTransform pList, string pTitle)
        {
            RectTransform lRow = Row(pList, "Section_" + pTitle, 44f, false);
            UIFactory.Rule(lRow, new Vector2(0, 0), new Vector2(0, 6), 28, false, UIFactory.Accent, 2f);
            TextMeshProUGUI lText = UIFactory.Title(lRow, "Title", pTitle, 16, UIFactory.Muted, TextAlignmentOptions.BottomLeft, new Vector2(0, 0), new Vector2(0, 10), new Vector2(600, 26), true, 5f);
            lText.rectTransform.pivot = new Vector2(0, 0);
        }

        /// <summary>A row of the list: a faint band with the label on the left; controls go on its right.</summary>
        public static RectTransform Row(RectTransform pList, string pLabel, float pHeight = ROW_HEIGHT, bool pBand = true)
        {
            RectTransform lRow = UIFactory.Rect(pList, "Row_" + pLabel, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, pHeight));
            LayoutElement lElement = lRow.gameObject.AddComponent<LayoutElement>();
            lElement.preferredHeight = pHeight;
            lElement.minHeight = pHeight;
            if (!pBand) return lRow;
            Image lBack = lRow.gameObject.AddComponent<Image>();
            lBack.color = ROW_BACK;
            lBack.raycastTarget = false;
            TextMeshProUGUI lText = UIFactory.Title(lRow, "Label", pLabel, 22, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(420, pHeight), false, 2f);
            lText.rectTransform.pivot = new Vector2(0, 0.5f);
            return lRow;
        }

        /// <summary>An on/off switch on the right of a row: a pill track, the knob slides right and lights up when on.</summary>
        public static Toggle Switch(RectTransform pList, string pLabel, bool pValue, Action<bool> pOnChange, string pHint = null)
        {
            RectTransform lRow = Row(pList, pLabel);
            if (!string.IsNullOrEmpty(pHint)) Hint(lRow, pHint, 110f);
            RectTransform lTrack = UIFactory.Rect(lRow, "Switch", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(60, 30));
            Image lTrackImage = lTrack.gameObject.AddComponent<Image>();
            lTrackImage.sprite = Pill;
            lTrackImage.type = Image.Type.Sliced;
            Image lKnob = UIFactory.Panel(lTrack, "Knob", UIFactory.Paper, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(22, 22));
            lKnob.sprite = UIIcons.Disc;
            Toggle lToggle = lRow.gameObject.AddComponent<Toggle>();
            lToggle.transition = Selectable.Transition.None;
            lToggle.targetGraphic = lTrackImage;
            void Show(bool pOn)
            {
                lTrackImage.color = pOn ? UIFactory.Accent : TRACK;
                lKnob.rectTransform.anchoredPosition = new Vector2(pOn ? 34 : 4, 0);
                lKnob.color = pOn ? UIFactory.Ink : UIFactory.Paper;
            }
            lToggle.SetIsOnWithoutNotify(pValue);
            Show(pValue);
            lToggle.onValueChanged.AddListener(v => { Show(v); Audio.Sfx.Play2D("click", 0.4f); pOnChange?.Invoke(v); });
            lRow.gameObject.AddComponent<RowFocus>().Init(lRow.GetComponent<Image>());
            return lToggle;
        }

        /// <summary>A slider on the right of a row, its value written right of the track.</summary>
        public static Slider Slider(RectTransform pList, string pLabel, float pMin, float pMax, float pValue, Func<float, string> pFormat, Action<float> pOnChange)
        {
            RectTransform lRow = Row(pList, pLabel);
            TextMeshProUGUI lValue = UIFactory.Title(lRow, "Value", pFormat(pValue), 20, UIFactory.Paper, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(70, 30), true, 1f);
            lValue.rectTransform.pivot = new Vector2(1, 0.5f);
            Image lTrack = UIFactory.Panel(lRow, "Track", TRACK, new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(300, 4));
            lTrack.rectTransform.pivot = new Vector2(1, 0.5f);
            RectTransform lFillArea = UIFactory.Stretch(lTrack.transform, "FillArea");
            Image lFill = UIFactory.Panel(lFillArea, "Fill", UIFactory.Accent, new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 4));
            lFill.rectTransform.anchorMin = new Vector2(0, 0);
            lFill.rectTransform.anchorMax = new Vector2(0, 1);
            RectTransform lHandleArea = UIFactory.Stretch(lTrack.transform, "HandleArea");
            Image lHandle = UIFactory.Panel(lHandleArea, "Handle", UIFactory.Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            lHandle.sprite = UIIcons.Disc;
            lHandle.raycastTarget = true;
            Slider lSlider = lRow.gameObject.AddComponent<Slider>();
            lSlider.transition = Selectable.Transition.None;
            lSlider.fillRect = lFill.rectTransform;
            lSlider.handleRect = lHandle.rectTransform;
            lSlider.targetGraphic = lHandle;
            lSlider.minValue = pMin;
            lSlider.maxValue = pMax;
            lSlider.SetValueWithoutNotify(pValue);
            lSlider.onValueChanged.AddListener(v => { lValue.text = pFormat(v); pOnChange?.Invoke(v); });
            lRow.gameObject.AddComponent<RowFocus>().Init(lRow.GetComponent<Image>());
            return lSlider;
        }

        /// <summary>‹ value ›: two arrow buttons around the current choice, on the right of a row. Returns the setter of the shown text.</summary>
        public static Action<string> Stepper(RectTransform pList, string pLabel, string pValue, Action<int> pOnStep)
        {
            RectTransform lRow = Row(pList, pLabel);
            TextMeshProUGUI lValue = UIFactory.Title(lRow, "Value", pValue, 20, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(1, 0.5f), new Vector2(-142, 0), new Vector2(220, 30), true, 2f);
            Arrow(lRow, "‹", new Vector2(-272, 0), () => pOnStep(-1));
            Arrow(lRow, "›", new Vector2(-12, 0), () => pOnStep(1));
            return pText => lValue.text = pText;
        }

        private static void Arrow(RectTransform pRow, string pGlyph, Vector2 pPosition, Action pOnClick)
        {
            Button lButton = UIFactory.Button(pRow, pGlyph, new Vector2(1, 0.5f), pPosition, new Vector2(40, 40), UIFactory.Accent2, pOnClick, 30);
            ((RectTransform)lButton.transform).pivot = new Vector2(1, 0.5f);
            TextMeshProUGUI lLabel = lButton.GetComponentInChildren<TextMeshProUGUI>();
            lLabel.alignment = TextAlignmentOptions.Center;
            lLabel.rectTransform.sizeDelta = new Vector2(40, 40);
            lLabel.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>A key or button glyph in a keycap, right-aligned at <paramref name="pRight"/> from the row's end.</summary>
        public static void Hint(RectTransform pRow, string pKey, float pRight)
        {
            Image lCap = UIFactory.Panel(pRow, "Key", new Color(1, 1, 1, 0.1f), new Vector2(1, 0.5f), new Vector2(-pRight, 0), new Vector2(34, 28));
            lCap.rectTransform.pivot = new Vector2(1, 0.5f);
            UIFactory.Title(lCap.transform, "Glyph", pKey, 16, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 28), true, 0f);
        }

        private static Sprite _pill;

        /// <summary>A white rounded pill, 9-sliced (switch tracks).</summary>
        private static Sprite Pill
        {
            get
            {
                if (_pill != null) return _pill;
                const int W = 64, H = 32;
                Texture2D lTexture = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float lCx = Mathf.Clamp(x + 0.5f, H * 0.5f, W - H * 0.5f);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(lCx, H * 0.5f)) - H * 0.5f + 0.5f;
                        lTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(0.5f - d)));
                    }
                lTexture.Apply();
                return _pill = Sprite.Create(lTexture, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16, 0, 16, 0));
            }
        }
    }
}
