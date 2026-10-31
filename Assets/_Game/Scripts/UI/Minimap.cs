using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
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
    /// The minimap, bottom left: the map's baked stylized plan (<see cref="MapPlan"/>), north up and
    /// still, showing the floor the player stands on; the player's arrow turns with the view. On it:
    /// the target in red (always, pinned to the rim when far), teammates, NPCs noticing or hunting
    /// you, the extractions once revealed. A ring around it takes the threat colour (amber when
    /// suspected or searched for, red in combat, blue when hidden), with N marked on top.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        private const float RANGE = 45f, SIZE = 230f;

        private RawImage _plan;
        private RectTransform _icons, _me;
        private Image _ring;
        private TextMeshProUGUI _floor;
        private readonly List<Image> _dots = new List<Image>();
        private int _used;
        private CanvasGroup _group;

        public void Build(RectTransform pRoot)
        {
            RectTransform lFrame = UIFactory.Rect(pRoot, "Minimap", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(36, 36), new Vector2(SIZE, SIZE));
            _group = lFrame.gameObject.AddComponent<CanvasGroup>();

            RectTransform lDisc = UIFactory.Rect(lFrame, "Disc", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image lMask = lDisc.gameObject.AddComponent<Image>();
            lMask.sprite = UIIcons.Disc;
            lMask.color = new Color(0.09f, 0.1f, 0.11f, 0.9f);
            lMask.raycastTarget = false;
            lDisc.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _plan = UIFactory.Rect(lDisc, "Plan", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            _plan.raycastTarget = false;
            _icons = UIFactory.Rect(lDisc, "Icons", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SIZE, SIZE));

            // The thin ring sprite's circle sits at 94 % of its half-size: scale it so it rides the rim.
            _ring = UIFactory.Rect(lFrame, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * ((SIZE + 6f) / 0.94f)).gameObject.AddComponent<Image>();
            _ring.sprite = UIIcons.ThinRing;
            _ring.raycastTarget = false;

            TextMeshProUGUI lNorth = UIFactory.Title(lFrame, "North", "N", 18, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, 2), new Vector2(30, 24), true, 0f);
            lNorth.outlineWidth = 0.2f;
            lNorth.outlineColor = new Color32(10, 10, 12, 255);
            _floor = UIFactory.Title(lFrame, "Floor", "", 14, UIFactory.Muted, TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(-8, 4), new Vector2(80, 20), true, 2f);

            _me = UIFactory.Panel(lDisc, "Me", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22)).rectTransform;
            _me.GetComponent<Image>().sprite = UIIcons.Arrow;
        }

        private void LateUpdate()
        {
            PlayerAvatar lMe = PlayerAvatar.Local;
            bool lShow = lMe != null && lMe.Health.IsAlive && !MapScreen.IsOpen && MapPlan.Ready;
            _group.alpha = Mathf.MoveTowards(_group.alpha, lShow ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            if (!lShow) return;

            // The plan around the player, north up: only the window moves.
            int lFloor = MapPlan.FloorAt(lMe.transform.position);
            _plan.texture = MapPlan.Floor(lFloor);
            Vector2 lCenter = MapPlan.ToUv(lMe.transform.position);
            float lSpan = MapPlan.MetresToUv(RANGE * 2f);
            _plan.uvRect = new Rect(lCenter.x - lSpan * 0.5f, lCenter.y - lSpan * 0.5f, lSpan, lSpan);
            _floor.text = lFloor == 0 ? "" : "FLOOR " + lFloor;
            // The arrow points where the player looks (UI angles turn counter-clockwise).
            _me.localEulerAngles = new Vector3(0, 0, -lMe.LookYaw);

            ThreatStatus lStatus = ThreatMarkers.Status;
            Color lRing = lStatus switch
            {
                ThreatStatus.Combat => ThreatMarkers.Red,
                ThreatStatus.Spotted or ThreatStatus.Searching or ThreatStatus.Suspicious => ThreatMarkers.Amber,
                ThreatStatus.Hidden or ThreatStatus.Concealed or ThreatStatus.Underwater => ThreatMarkers.Blue,
                _ => new Color(1f, 1f, 1f, 0.18f)
            };
            if (lStatus == ThreatStatus.Combat || lStatus == ThreatStatus.Spotted) lRing.a = 0.7f + Mathf.Sin(Time.time * 8f) * 0.3f;
            _ring.color = lRing;

            _used = 0;
            Vector3 lFrom = lMe.transform.position;
            if (LobbyState.Instance == null || LobbyState.Instance.Mode.Value != MatchMode.FreeForAll)
                foreach (PlayerAvatar lOther in PlayerAvatar.All)
                    if (lOther != null && lOther != lMe && lOther.Team.Value == lMe.Team.Value && lOther.Health.IsAlive) Dot(lFrom, lOther.transform.position, ThreatMarkers.Blue, 8f, true);
            Npc lTarget = null;
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || !lNpc.Health.IsAlive) continue;
                if (lNpc.IsTarget) { lTarget = lNpc; continue; }
                if (lNpc.IsAlerted || lNpc.Awareness.Value >= 6) Dot(lFrom, lNpc.transform.position, lNpc.IsAlerted ? ThreatMarkers.Red : ThreatMarkers.Amber, 7f);
            }
            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null && lDirector.Proof.Value != ProofState.TargetAlive)
            {
                List<MatchPoint> lExtractions = MatchPoint.OfKind(MatchPoint.Kind.Extraction);
                for (int i = 0; i < lExtractions.Count; i++)
                    if ((lDirector.ActiveExtractions.Value & (1 << i)) != 0) Dot(lFrom, lExtractions[i].transform.position, UIFactory.Good, 11f, true);
            }
            if (lTarget != null) Dot(lFrom, lTarget.transform.position, ThreatMarkers.Red, 12f, true);
            for (int i = _used; i < _dots.Count; i++) _dots[i].enabled = false;
        }

        /// <summary>A dot at a world position, north up; beyond the range it sticks to the rim (when asked).</summary>
        private void Dot(Vector3 pFrom, Vector3 pWorld, Color pColor, float pSize, bool pClampToRim = false)
        {
            Vector3 lOffset = pWorld - pFrom;
            Vector2 p = new Vector2(lOffset.x, lOffset.z) / RANGE * (SIZE * 0.5f);
            if (p.magnitude > SIZE * 0.5f - 7f)
            {
                if (!pClampToRim) return;
                p = p.normalized * (SIZE * 0.5f - 7f);
            }
            if (_used >= _dots.Count)
            {
                Image lNew = UIFactory.Panel(_icons, "Dot", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 8f);
                lNew.sprite = UIIcons.Dot;
                lNew.raycastTarget = false;
                _dots.Add(lNew);
            }
            Image lDot = _dots[_used++];
            lDot.enabled = true;
            lDot.color = pColor;
            lDot.rectTransform.sizeDelta = Vector2.one * pSize;
            lDot.rectTransform.anchoredPosition = p;
        }
    }
}
