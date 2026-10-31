using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.FX;
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
    /// The heading strip across the top: cardinal letters and ticks every 15° sliding with the view,
    /// the heading in degrees under the centre, and markers - the target in red with its distance
    /// (always), the extractions once revealed, teammates, the proof.
    /// </summary>
    public class Compass : MonoBehaviour
    {
        private const float WIDTH = 620f, SPAN = 100f;
        private static readonly string[] LETTERS = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private RectTransform _strip;
        private CanvasGroup _group;
        private TextMeshProUGUI _heading;
        private readonly List<(RectTransform rect, Graphic graphic, float angle)> _marks = new List<(RectTransform, Graphic, float)>();
        private readonly List<(RectTransform rect, Image icon, TextMeshProUGUI label)> _pins = new List<(RectTransform, Image, TextMeshProUGUI)>();
        private int _usedPins;

        public void Build(RectTransform pRoot)
        {
            _strip = UIFactory.Rect(pRoot, "Compass", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(WIDTH, 44));
            _group = _strip.gameObject.AddComponent<CanvasGroup>();
            Image lLine = UIFactory.Panel(_strip, "Line", new Color(1, 1, 1, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(WIDTH, 1));
            lLine.raycastTarget = false;
            UIFactory.Panel(_strip, "Centre", UIFactory.Paper, new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(2, 14));
            _heading = UIFactory.Title(_strip, "Heading", "", 13, UIFactory.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, -12), new Vector2(80, 18), true, 2f);

            for (int a = 0; a < 360; a += 15)
            {
                if (a % 45 == 0)
                {
                    bool lCardinal = a % 90 == 0;
                    TextMeshProUGUI lLetter = UIFactory.Title(_strip, "L" + a, LETTERS[a / 45], lCardinal ? 20 : 14, lCardinal ? UIFactory.Paper : UIFactory.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(40, 24), lCardinal, 1f);
                    _marks.Add(((RectTransform)lLetter.transform, lLetter, a));
                }
                else
                {
                    Image lTick = UIFactory.Panel(_strip, "T" + a, new Color(1, 1, 1, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(1, 8));
                    lTick.raycastTarget = false;
                    _marks.Add((lTick.rectTransform, lTick, a));
                }
            }
        }

        private void LateUpdate()
        {
            PlayerAvatar lMe = PlayerAvatar.Local;
            Camera lCamera = CameraRig.Instance != null ? CameraRig.Instance.WorldCamera : null;
            bool lShow = lMe != null && lMe.Health.IsAlive && lCamera != null && !MapScreen.IsOpen;
            _group.alpha = Mathf.MoveTowards(_group.alpha, lShow ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            if (!lShow) return;

            float lYaw = lCamera.transform.eulerAngles.y;
            _heading.text = Mathf.RoundToInt(Mathf.Repeat(lYaw, 360f)).ToString("000");
            foreach ((RectTransform lRect, Graphic lGraphic, float lAngle) in _marks)
            {
                float lDelta = Mathf.DeltaAngle(lYaw, lAngle);
                bool lIn = Mathf.Abs(lDelta) < SPAN * 0.5f;
                lGraphic.enabled = lIn;
                if (!lIn) continue;
                lRect.anchoredPosition = new Vector2(lDelta / SPAN * WIDTH, lRect.anchoredPosition.y);
                Color lColor = lGraphic.color;
                lColor.a = Mathf.Clamp01((SPAN * 0.5f - Mathf.Abs(lDelta)) / 12f) * (lGraphic is Image ? 0.45f : 1f);
                lGraphic.color = lColor;
            }

            _usedPins = 0;
            Vector3 lFrom = lMe.transform.position;
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.IsTarget && lNpc.Health.IsAlive) Pin(lYaw, lFrom, lNpc.transform.position, ThreatMarkers.Red, true);
            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null)
            {
                if (lDirector.Proof.Value == ProofState.Dropped) Pin(lYaw, lFrom, lDirector.ProofPosition.Value, ThreatMarkers.Amber, true);
                if (lDirector.Proof.Value != ProofState.TargetAlive)
                {
                    List<MatchPoint> lExtractions = MatchPoint.OfKind(MatchPoint.Kind.Extraction);
                    for (int i = 0; i < lExtractions.Count; i++)
                        if ((lDirector.ActiveExtractions.Value & (1 << i)) != 0) Pin(lYaw, lFrom, lExtractions[i].transform.position, UIFactory.Good, true);
                }
            }
            if (LobbyState.Instance == null || LobbyState.Instance.Mode.Value != MatchMode.FreeForAll)
                foreach (PlayerAvatar lOther in PlayerAvatar.All)
                    if (lOther != null && lOther != lMe && lOther.Team.Value == lMe.Team.Value && lOther.Health.IsAlive) Pin(lYaw, lFrom, lOther.transform.position, ThreatMarkers.Blue, false);
            for (int i = _usedPins; i < _pins.Count; i++) _pins[i].rect.gameObject.SetActive(false);
        }

        /// <summary>A marker on the strip toward a world point; off the strip it waits at the nearest end.</summary>
        private void Pin(float pYaw, Vector3 pFrom, Vector3 pTo, Color pColor, bool pDistance)
        {
            Vector3 lTo = pTo - pFrom;
            float lDelta = Mathf.DeltaAngle(pYaw, Mathf.Atan2(lTo.x, lTo.z) * Mathf.Rad2Deg);
            if (_usedPins >= _pins.Count)
            {
                RectTransform lRect = UIFactory.Rect(_strip, "Pin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
                Image lIcon = lRect.gameObject.AddComponent<Image>();
                lIcon.sprite = UIIcons.Chevron;
                lIcon.raycastTarget = false;
                TextMeshProUGUI lLabel = UIFactory.Title(lRect, "Distance", "", 12, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, -12), new Vector2(70, 16), true, 1f);
                _pins.Add((lRect, lIcon, lLabel));
            }
            (RectTransform rect, Image icon, TextMeshProUGUI label) = _pins[_usedPins++];
            rect.gameObject.SetActive(true);
            float lClamped = Mathf.Clamp(lDelta, -SPAN * 0.5f, SPAN * 0.5f);
            rect.anchoredPosition = new Vector2(lClamped / SPAN * WIDTH, -16);
            icon.color = Mathf.Abs(lDelta) > SPAN * 0.5f ? new Color(pColor.r, pColor.g, pColor.b, 0.55f) : pColor;
            label.text = pDistance ? Mathf.RoundToInt(lTo.magnitude) + " M" : "";
            label.color = pColor;
        }
    }
}
