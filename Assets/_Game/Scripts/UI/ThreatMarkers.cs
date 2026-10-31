using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>What the local player stands as, from calm to combat (HUD chip, minimap ring).</summary>
    public enum ThreatStatus { None, Hidden, Concealed, Underwater, Suspicious, Searching, Spotted, Combat }

    /// <summary>
    /// The stealth read-out on the HUD, in screen space:
    /// <list type="bullet">
    /// <item>above each NPC, a diamond: an outline filling white → amber with its gauge, amber "!"
    /// when it confronts, red "!" in combat, "?" while searching, a phone with a countdown ring while
    /// it calls (kill it first); the alerted ones stay pinned to the screen edge when off-screen;</item>
    /// <item>around the crosshair, one arc per NPC noticing you, pointing at it, growing and warming
    /// with its gauge;</item>
    /// <item>the status chip above the minimap (hidden, concealed, suspicious, spotted, searching,
    /// combat) and a red chevron over the target, always.</item>
    /// </list>
    /// </summary>
    public class ThreatMarkers : MonoBehaviour
    {
        public static ThreatStatus Status { get; private set; }

        public static readonly Color White = new Color(0.96f, 0.95f, 0.92f);
        public static readonly Color Amber = new Color(1f, 0.69f, 0.18f);
        public static readonly Color Red = new Color(0.93f, 0.2f, 0.18f);
        public static readonly Color Blue = new Color(0.45f, 0.72f, 1f);

        private const float SHOW_RANGE = 70f, EDGE = 70f, ARC_RADIUS = 120f;
        private const int MAX_ARCS = 8;

        private class Marker
        {
            public RectTransform root;
            public Image back, outline, fill, ring, phone;
            public TextMeshProUGUI glyph;
            public NpcState lastState;
            public float stateSince;
        }

        private RectTransform _root, _arcs, _chip;
        private Image _chipBar;
        private TextMeshProUGUI _chipText, _targetLabel;
        private RectTransform _target;
        private readonly Dictionary<Npc, Marker> _markers = new Dictionary<Npc, Marker>();
        private readonly List<Image> _arcPool = new List<Image>();
        private readonly List<Npc> _gone = new List<Npc>();
        private AudioSource _tension;
        private ThreatStatus _heard;

        public void Build(RectTransform pRoot)
        {
            _root = UIFactory.Stretch(pRoot, "Threats");
            _arcs = UIFactory.Stretch(_root, "DetectionArcs");

            _target = UIFactory.Rect(_root, "TargetMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(30, 30));
            Image lChevron = _target.gameObject.AddComponent<Image>();
            lChevron.sprite = UIIcons.Chevron;
            lChevron.color = Red;
            lChevron.raycastTarget = false;
            _targetLabel = UIFactory.Text(_target, "Distance", "", 16, Red, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, 12), new Vector2(120, 22));
            _target.gameObject.SetActive(false);

            // The status chip sits right above the minimap disc (bottom left, 230 px).
            _chip = UIFactory.Rect(_root, "StatusChip", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(36, 290), new Vector2(230, 30));
            Image lBack = _chip.gameObject.AddComponent<Image>();
            lBack.color = new Color(0.03f, 0.03f, 0.04f, 0.62f);
            lBack.raycastTarget = false;
            _chipBar = UIFactory.Panel(_chip, "Bar", White, new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(4, 30));
            _chipText = UIFactory.Title(_chip, "Label", "", 17, White, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(210, 30), true, 4f);
            ((RectTransform)_chipText.transform).pivot = new Vector2(0, 0.5f);
            _chip.gameObject.SetActive(false);
        }

        private Marker Create(Npc pNpc)
        {
            Marker lMarker = new Marker { root = UIFactory.Rect(_root, "Npc", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34)) };
            lMarker.back = Icon(lMarker.root, "Back", UIIcons.Diamond, 34f);
            lMarker.fill = Icon(lMarker.root, "Fill", UIIcons.Diamond, 30f);
            lMarker.fill.type = Image.Type.Filled;
            lMarker.fill.fillMethod = Image.FillMethod.Vertical;
            lMarker.fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            lMarker.outline = Icon(lMarker.root, "Outline", UIIcons.DiamondOutline, 34f);
            lMarker.ring = Icon(lMarker.root, "Ring", UIIcons.Ring, 46f);
            lMarker.ring.type = Image.Type.Filled;
            lMarker.ring.fillMethod = Image.FillMethod.Radial360;
            lMarker.ring.fillOrigin = (int)Image.Origin360.Top;
            lMarker.ring.fillClockwise = false;
            lMarker.phone = Icon(lMarker.root, "Phone", UIIcons.Phone, 26f);
            lMarker.glyph = UIFactory.Text(lMarker.root, "Glyph", "", 22, UIFactory.Ink, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(34, 34));
            lMarker.glyph.font = UIFactory.HeadingBold;
            lMarker.lastState = pNpc.State.Value;
            lMarker.stateSince = Time.time;
            return lMarker;
        }

        private static Image Icon(RectTransform pParent, string pName, Sprite pSprite, float pSize)
        {
            RectTransform lRect = UIFactory.Rect(pParent, pName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(pSize, pSize));
            Image lImage = lRect.gameObject.AddComponent<Image>();
            lImage.sprite = pSprite;
            lImage.raycastTarget = false;
            return lImage;
        }

        private void LateUpdate()
        {
            Camera lCamera = CameraRig.Instance != null ? CameraRig.Instance.WorldCamera : null;
            PlayerAvatar lMe = PlayerAvatar.Local;
            ulong lMyId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            if (lCamera == null) { _root.gameObject.SetActive(false); return; }
            _root.gameObject.SetActive(true);

            ThreatStatus lStatus = ThreatStatus.None;
            int lArc = 0;
            Vector3 lCamPos = lCamera.transform.position;
            Npc lTarget = null;

            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null) continue;
                if (lNpc.IsTarget && lNpc.Health.IsAlive) lTarget = lNpc;
                bool lMine = lNpc.Suspect.Value == lMyId;
                NpcState lState = lNpc.State.Value;
                float lGauge = lNpc.Awareness.Value / 20f;

                if (lMine && lNpc.Health.IsAlive)
                {
                    ThreatStatus lFrom = lState == NpcState.Combat || lState == NpcState.Calling ? ThreatStatus.Combat
                        : lState == NpcState.Engaged ? ThreatStatus.Spotted
                        : lGauge > 0.02f ? ThreatStatus.Suspicious : ThreatStatus.None;
                    if (lFrom > lStatus) lStatus = lFrom;
                    if (lArc < MAX_ARCS && lFrom >= ThreatStatus.Suspicious) DrawArc(lArc++, lCamera, lNpc.transform.position, lFrom, lGauge);
                }
                if (lState == NpcState.Search && lMe != null && lStatus < ThreatStatus.Searching && (lNpc.transform.position - lMe.transform.position).sqrMagnitude < 45f * 45f) lStatus = ThreatStatus.Searching;

                UpdateMarker(lNpc, lCamera, lCamPos, lMine, lState, lGauge);
            }
            for (int i = lArc; i < _arcPool.Count; i++) _arcPool[i].enabled = false;

            // Markers of despawned NPCs.
            _gone.Clear();
            foreach (KeyValuePair<Npc, Marker> lPair in _markers) if (lPair.Key == null) { Destroy(lPair.Value.root.gameObject); _gone.Add(lPair.Key); }
            foreach (Npc lNpc in _gone) _markers.Remove(lNpc);

            if (lStatus == ThreatStatus.None && lMe != null)
            {
                if (lMe.Hidden.Value) lStatus = ThreatStatus.Hidden;
                else if (lMe.Submerged.Value) lStatus = ThreatStatus.Underwater;
                else if (lMe.InGrass.Value && lMe.Crouched.Value) lStatus = ThreatStatus.Concealed;
            }
            Status = lMe != null && lMe.Health.IsAlive ? lStatus : ThreatStatus.None;
            UpdateChip();
            UpdateTension();
            UpdateTarget(lCamera, lTarget, lMe);
        }

        private void UpdateMarker(Npc pNpc, Camera pCamera, Vector3 pCamPos, bool pMine, NpcState pState, float pGauge)
        {
            if (!_markers.TryGetValue(pNpc, out Marker lMarker)) _markers[pNpc] = lMarker = Create(pNpc);
            if (lMarker.lastState != pState) { lMarker.lastState = pState; lMarker.stateSince = Time.time; }

            bool lAlive = pNpc.Health.IsAlive;
            bool lAlert = lAlive && (pState == NpcState.Engaged || pState == NpcState.Combat || pState == NpcState.Calling);
            bool lShow = lAlive ? lAlert || pState == NpcState.Search || pGauge > 0.02f || pState == NpcState.Flee || pState == NpcState.Sick : pState == NpcState.KnockedOut;
            Vector3 lHead = pNpc.transform.position + Vector3.up * (2.25f * pNpc.transform.localScale.y);
            float lDistance = Vector3.Distance(pCamPos, lHead);
            if (!lShow || lDistance > (pState == NpcState.KnockedOut ? 20f : SHOW_RANGE)) { lMarker.root.gameObject.SetActive(false); return; }

            // Alerted NPCs that concern you stay pinned to the screen edge.
            bool lPin = lAlert && (pMine || pState == NpcState.Calling);
            if (!Place(lMarker.root, pCamera, lHead, lPin)) { lMarker.root.gameObject.SetActive(false); return; }
            lMarker.root.gameObject.SetActive(true);
            float lScale = Mathf.Lerp(1.1f, 0.65f, Mathf.InverseLerp(4f, SHOW_RANGE, lDistance));

            lMarker.ring.enabled = lMarker.phone.enabled = false;
            lMarker.fill.fillAmount = 1f;
            switch (pState)
            {
                case NpcState.Calling:
                    float lProgress = Mathf.Clamp01((Time.time - lMarker.stateSince) / Mathf.Max(0.1f, GameDatabase.Rules.callDuration));
                    Paint(lMarker, new Color(0.03f, 0.03f, 0.04f, 0.85f), Red, null, "", Red);
                    lMarker.phone.enabled = lMarker.ring.enabled = true;
                    lMarker.phone.color = Red;
                    lMarker.ring.color = Red;
                    lMarker.ring.fillAmount = 1f - lProgress;
                    lScale *= 1.2f + Mathf.Sin(Time.time * 10f) * 0.05f;
                    break;
                case NpcState.Combat:
                    Paint(lMarker, Red, Red, Red, "!", UIFactory.Ink);
                    break;
                case NpcState.Engaged:
                    Paint(lMarker, Amber, Amber, Amber, "!", UIFactory.Ink);
                    lScale *= 1f + Mathf.Sin(Time.time * 12f) * 0.06f;
                    break;
                case NpcState.Search:
                    Paint(lMarker, new Color(0.03f, 0.03f, 0.04f, 0.7f), Amber, null, "?", Amber);
                    break;
                case NpcState.Flee:
                    Paint(lMarker, new Color(0.03f, 0.03f, 0.04f, 0.6f), White, null, "!!", White);
                    lScale *= 0.75f;
                    break;
                case NpcState.Sick:
                    Paint(lMarker, new Color(0.03f, 0.03f, 0.04f, 0.6f), UIFactory.Good, null, "~", UIFactory.Good);
                    lScale *= 0.75f;
                    break;
                case NpcState.KnockedOut:
                    Paint(lMarker, new Color(0.03f, 0.03f, 0.04f, 0.6f), Blue, null, "z", Blue);
                    lScale *= 0.7f;
                    break;
                default:
                    // The gauge: an outline that fills from the bottom, white warming to amber.
                    Color lWarm = Color.Lerp(White, Amber, pGauge);
                    Paint(lMarker, new Color(0.03f, 0.03f, 0.04f, 0.45f), lWarm, lWarm, "", UIFactory.Ink);
                    lMarker.fill.fillAmount = Mathf.Max(0.08f, pGauge);
                    lMarker.outline.color = new Color(lWarm.r, lWarm.g, lWarm.b, Mathf.Lerp(0.55f, 1f, pGauge));
                    lScale *= Mathf.Lerp(0.8f, 1.05f, pGauge);
                    break;
            }
            lMarker.root.localScale = Vector3.one * lScale;
        }

        /// <summary>Colours a marker: dark or coloured back, outline, an optional solid fill, the glyph.</summary>
        private static void Paint(Marker pMarker, Color pBack, Color pOutline, Color? pFill, string pGlyph, Color pGlyphColor)
        {
            pMarker.back.color = pBack;
            pMarker.outline.color = pOutline;
            pMarker.fill.enabled = pFill.HasValue;
            if (pFill.HasValue) pMarker.fill.color = pFill.Value;
            pMarker.glyph.text = pGlyph;
            pMarker.glyph.color = pGlyphColor;
        }

        /// <summary>Screen position of a world point on this canvas; pinned to the edge (or hidden) when off-screen.</summary>
        private bool Place(RectTransform pMarker, Camera pCamera, Vector3 pWorld, bool pPin)
        {
            Vector3 lScreen = pCamera.WorldToScreenPoint(pWorld);
            bool lBehind = lScreen.z < 0f;
            bool lOnScreen = !lBehind && lScreen.x > 0f && lScreen.x < Screen.width && lScreen.y > 0f && lScreen.y < Screen.height;
            if (!lOnScreen && !pPin) return false;
            Vector2 lHalf = _root.rect.size * 0.5f;
            Vector2 lLocal = new Vector2(lScreen.x / Screen.width * _root.rect.width - lHalf.x, lScreen.y / Screen.height * _root.rect.height - lHalf.y);
            if (lBehind) lLocal = -lLocal;
            if (!lOnScreen || lBehind)
            {
                Vector2 lBox = lHalf - Vector2.one * EDGE;
                float lScale = Mathf.Min(lBox.x / Mathf.Max(0.01f, Mathf.Abs(lLocal.x)), lBox.y / Mathf.Max(0.01f, Mathf.Abs(lLocal.y)));
                if (lScale < 1f || lBehind) lLocal *= Mathf.Min(1f, lScale);
                if (lBehind && lLocal.sqrMagnitude < 1f) lLocal = new Vector2(0, -lBox.y);
            }
            pMarker.anchoredPosition = lLocal;
            return true;
        }

        private void DrawArc(int pIndex, Camera pCamera, Vector3 pFrom, ThreatStatus pLevel, float pGauge)
        {
            while (_arcPool.Count <= pIndex)
            {
                RectTransform lRect = UIFactory.Rect(_arcs, "Arc", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ARC_RADIUS * 2f, ARC_RADIUS * 2f));
                Image lImage = lRect.gameObject.AddComponent<Image>();
                lImage.sprite = UIIcons.Arc;
                lImage.raycastTarget = false;
                _arcPool.Add(lImage);
            }
            Image lArc = _arcPool[pIndex];
            lArc.enabled = true;
            Vector3 lTo = pFrom - pCamera.transform.position;
            float lBearing = Vector3.SignedAngle(Vector3.ProjectOnPlane(pCamera.transform.forward, Vector3.up), Vector3.ProjectOnPlane(lTo, Vector3.up), Vector3.up);
            lArc.rectTransform.localEulerAngles = new Vector3(0, 0, -lBearing);
            Color lColor = pLevel == ThreatStatus.Combat ? Red : pLevel == ThreatStatus.Spotted ? Amber : Color.Lerp(White, Amber, pGauge);
            lColor.a = pLevel == ThreatStatus.Suspicious ? Mathf.Lerp(0.3f, 0.95f, pGauge) : 0.95f;
            lArc.color = lColor;
            float lSize = pLevel == ThreatStatus.Suspicious ? Mathf.Lerp(0.85f, 1.05f, pGauge) : 1.08f;
            lArc.rectTransform.localScale = Vector3.one * lSize;
        }

        /// <summary>A low drone that swells with suspicion, a stinger when spotted, a hit when it turns to combat.</summary>
        private void UpdateTension()
        {
            if (_tension == null)
            {
                AudioClip lClip = Resources.Load<AudioClip>("Sfx/tension_loop");
                if (lClip == null) return;
                _tension = gameObject.AddComponent<AudioSource>();
                _tension.clip = lClip;
                _tension.loop = true;
                _tension.spatialBlend = 0f;
                _tension.volume = 0f;
                _tension.Play();
            }
            if (Status != _heard)
            {
                if (Status == ThreatStatus.Spotted && _heard < ThreatStatus.Spotted) Audio.Sfx.Play2D("spotted", 0.7f, 0f);
                if (Status == ThreatStatus.Combat && _heard < ThreatStatus.Combat) Audio.Sfx.Play2D("combat_hit", 0.8f, 0f);
                _heard = Status;
            }
            float lTarget = Status switch
            {
                ThreatStatus.Combat => 0.7f,
                ThreatStatus.Spotted => 0.6f,
                ThreatStatus.Searching => 0.35f,
                ThreatStatus.Suspicious => 0.4f,
                _ => 0f
            };
            _tension.volume = Mathf.MoveTowards(_tension.volume, lTarget * Audio.Sfx.Volume, Time.unscaledDeltaTime * (lTarget > _tension.volume ? 0.8f : 0.25f));
        }

        private void UpdateChip()
        {
            string lText;
            Color lColor;
            switch (Status)
            {
                case ThreatStatus.Hidden: lText = "HIDDEN"; lColor = Blue; break;
                case ThreatStatus.Concealed: lText = "CONCEALED"; lColor = Blue; break;
                case ThreatStatus.Underwater:
                    float lBreath = PlayerAvatar.Local != null ? PlayerAvatar.Local.Motor.Breath : 1f;
                    lText = lBreath < 0.6f ? "UNDER WATER   BREATH " + Mathf.CeilToInt(lBreath * 100f) + "%" : "UNDER WATER";
                    lColor = lBreath < 0.25f ? Red : Blue;
                    break;
                case ThreatStatus.Suspicious: lText = "SUSPICIOUS"; lColor = White; break;
                case ThreatStatus.Searching: lText = "SEARCHING"; lColor = Amber; break;
                case ThreatStatus.Spotted: lText = "SPOTTED"; lColor = Amber; break;
                case ThreatStatus.Combat: lText = "COMBAT"; lColor = Red; break;
                default: _chip.gameObject.SetActive(false); return;
            }
            _chip.gameObject.SetActive(true);
            _chipText.text = lText;
            _chipText.color = lColor;
            _chipBar.color = lColor;
        }

        /// <summary>The target's chevron, at any range: over its head on screen, pinned to the edge off it.</summary>
        private void UpdateTarget(Camera pCamera, Npc pTarget, PlayerAvatar pMe)
        {
            if (pTarget == null || pMe == null || !pMe.Health.IsAlive) { _target.gameObject.SetActive(false); return; }
            Vector3 lHead = pTarget.transform.position + Vector3.up * (2.5f * pTarget.transform.localScale.y);
            _target.gameObject.SetActive(Place(_target, pCamera, lHead, true));
            _targetLabel.text = Mathf.RoundToInt(Vector3.Distance(pMe.transform.position, pTarget.transform.position)) + " M";
        }
    }
}
