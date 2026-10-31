using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
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
    /// The overlays of the <see cref="CasterView"/>, broadcast style:
    /// <list type="bullet">
    /// <item>per feed: a frame (amber when selected), what it shows, the agent's plate (team, health,
    /// weapon, stealth state), names over the other agents and the target, or the tactical map;</item>
    /// <item>a bar on top: map, mode, clock, contract, auto-director;</item>
    /// <item>the agent cards at the bottom (a click puts the agent on the selected feed) and the key hints.</item>
    /// </list>
    /// H hides everything for a clean feed.
    /// </summary>
    public class CasterHud : MonoBehaviour
    {
        private const float CARD_W = 222f, CARD_H = 64f, CARD_GAP = 8f, TAG_RANGE = 180f, PLATE_W = 380f, PLATE_H = 58f, TOP_W = 900f;
        private static readonly Color Back = new Color(0.03f, 0.03f, 0.04f, 0.8f);
        private static readonly Color Studio = new Color(0.045f, 0.05f, 0.058f, 1f);

        private class Frame
        {
            public RectTransform rect;
            public readonly Image[] edges = new Image[4];
            public TextMeshProUGUI tag, centre;
            public RectTransform plate;
            public Image plateBar, healthFill;
            public TextMeshProUGUI plateName, plateLine;
            public RawImage plan;
            public Image planBack;
            public RectTransform tags;
            public readonly List<TextMeshProUGUI> tagPool = new List<TextMeshProUGUI>();
            public readonly List<(Image icon, TextMeshProUGUI label)> markPool = new List<(Image, TextMeshProUGUI)>();
            public int usedTags, usedMarks;
        }

        private class Card
        {
            public RectTransform rect;
            public Image back, edge, bar, health;
            public TextMeshProUGUI number, name, line, badge;
        }

        private CasterView _view;
        private RectTransform _root, _overlay, _cardsRow;
        private Image _bandTop, _bandBottom;
        private CanvasGroup _group;
        private readonly Frame[] _frames = new Frame[CasterLayoutLogic.MAX_FEEDS];
        private readonly List<Card> _cards = new List<Card>();
        private TextMeshProUGUI _kicker, _info, _auto, _help;

        // ---------------------------------------------------------------- build

        public void Build(CasterView pView)
        {
            _view = pView;
            Canvas lCanvas = UIFactory.Canvas("CasterCanvas", 12);
            lCanvas.transform.SetParent(transform, false);
            _root = (RectTransform)lCanvas.transform;
            // The bands no feed covers (duo, main + 3), kept even on a clean feed.
            _bandTop = Band("BandTop", new Vector2(0f, 0.75f), Vector2.one);
            _bandBottom = Band("BandBottom", Vector2.zero, new Vector2(1f, 0.25f));
            _overlay = UIFactory.Stretch(_root, "Overlay");
            _group = _overlay.gameObject.AddComponent<CanvasGroup>();

            for (int i = 0; i < _frames.Length; i++) _frames[i] = BuildFrame(i);

            RectTransform lTop = UIFactory.Panel(_overlay, "Top", Back, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(TOP_W, 46)).rectTransform;
            _kicker = UIFactory.Title(lTop, "Kicker", "", 14, UIFactory.Accent, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(170, 20), true, 4f);
            _info = UIFactory.Title(lTop, "Info", "", 18, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(620, 26), true, 2f);
            _info.overflowMode = TextOverflowModes.Ellipsis;
            _auto = UIFactory.Title(lTop, "Auto", "", 14, UIFactory.Good, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(170, 20), true, 3f);

            _cardsRow = UIFactory.Rect(_overlay, "Cards", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(8 * (CARD_W + CARD_GAP), CARD_H));
            for (int i = 0; i < 8; i++) _cards.Add(BuildCard(i));
            _help = UIFactory.Title(_overlay, "Help", "", 13, UIFactory.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(1880, 18), false, 2f);
        }

        private Image Band(string pName, Vector2 pMin, Vector2 pMax)
        {
            Image lBand = UIFactory.Rect(_root, pName, pMin, pMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            lBand.color = Studio;
            lBand.raycastTarget = false;
            return lBand;
        }

        private Frame BuildFrame(int pIndex)
        {
            Frame lFrame = new Frame();
            lFrame.rect = UIFactory.Rect(_overlay, "Frame" + pIndex, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            lFrame.edges[0] = Edge(lFrame.rect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f));
            lFrame.edges[1] = Edge(lFrame.rect, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f));
            lFrame.edges[2] = Edge(lFrame.rect, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f));
            lFrame.edges[3] = Edge(lFrame.rect, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f));

            lFrame.planBack = UIFactory.Stretch(lFrame.rect, "PlanBack").gameObject.AddComponent<Image>();
            lFrame.planBack.color = Studio;
            lFrame.planBack.raycastTarget = false;
            lFrame.planBack.transform.SetAsFirstSibling();
            RawImage lPlan = UIFactory.Rect(lFrame.rect, "Plan", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 400)).gameObject.AddComponent<RawImage>();
            lPlan.raycastTarget = false;
            lFrame.plan = lPlan;
            lFrame.tags = UIFactory.Stretch(lFrame.rect, "Tags");

            lFrame.tag = UIFactory.Title(lFrame.rect, "Tag", "", 15, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(16, -12), new Vector2(700, 20), true, 3f);
            lFrame.centre = UIFactory.Title(lFrame.rect, "Centre", "", 30, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 40), true, 6f);

            Image lPlate = UIFactory.Panel(lFrame.rect, "Plate", Back, new Vector2(0, 0), new Vector2(14, 14), new Vector2(PLATE_W, PLATE_H));
            lFrame.plate = lPlate.rectTransform;
            lFrame.plateBar = UIFactory.Panel(lFrame.plate, "Team", Color.white, new Vector2(0, 0.5f), Vector2.zero, new Vector2(5, PLATE_H));
            lFrame.plateName = UIFactory.Title(lFrame.plate, "Name", "", 22, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(18, -2), new Vector2(PLATE_W - 30, 30), true, 1.5f);
            lFrame.plateName.overflowMode = TextOverflowModes.Ellipsis;
            lFrame.plateLine = UIFactory.Text(lFrame.plate, "Line", "", 15, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(18, 8), new Vector2(PLATE_W - 30, 20));
            lFrame.plateLine.textWrappingMode = TextWrappingModes.NoWrap;
            UIFactory.Panel(lFrame.plate, "HealthBack", new Color(1, 1, 1, 0.12f), new Vector2(0, 0), new Vector2(5, 0), new Vector2(PLATE_W - 5, 3));
            lFrame.healthFill = UIFactory.Panel(lFrame.plate, "Health", UIFactory.Good, new Vector2(0, 0), new Vector2(5, 0), new Vector2(PLATE_W - 5, 3));
            return lFrame;
        }

        private static Image Edge(RectTransform pFrame, Vector2 pMin, Vector2 pMax, Vector2 pPivot)
        {
            RectTransform lRect = UIFactory.Rect(pFrame, "Edge", pMin, pMax, pPivot, Vector2.zero, new Vector2(2, 2));
            Image lImage = lRect.gameObject.AddComponent<Image>();
            lImage.raycastTarget = false;
            return lImage;
        }

        private Card BuildCard(int pIndex)
        {
            Card lCard = new Card();
            Image lBack = UIFactory.Panel(_cardsRow, "Card" + pIndex, Back, new Vector2(0, 0.5f), new Vector2(pIndex * (CARD_W + CARD_GAP) + CARD_GAP * 0.5f, 0), new Vector2(CARD_W, CARD_H));
            lBack.raycastTarget = true;
            lCard.back = lBack;
            lCard.rect = lBack.rectTransform;
            Button lButton = lBack.gameObject.AddComponent<Button>();
            lButton.transition = Selectable.Transition.None;
            Navigation lNavigation = lButton.navigation;
            lNavigation.mode = Navigation.Mode.None;
            lButton.navigation = lNavigation;
            int lIndex = pIndex;
            lButton.onClick.AddListener(() => _view.AssignIndex(lIndex));

            lCard.edge = UIFactory.Panel(lCard.rect, "OnAir", UIFactory.Accent, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CARD_W, 3));
            lCard.bar = UIFactory.Panel(lCard.rect, "Team", Color.white, new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, CARD_H));
            lCard.number = UIFactory.Title(lCard.rect, "Number", (pIndex + 1).ToString(), 30, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(30, 40), true, 0f);
            lCard.name = UIFactory.Title(lCard.rect, "Name", "", 18, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(44, -8), new Vector2(CARD_W - 52, 22), true, 1f);
            lCard.name.overflowMode = TextOverflowModes.Ellipsis;
            lCard.line = UIFactory.Text(lCard.rect, "Line", "", 14, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(44, 9), new Vector2(CARD_W - 52, 18));
            lCard.line.textWrappingMode = TextWrappingModes.NoWrap;
            lCard.badge = UIFactory.Title(lCard.rect, "Badge", "", 11, UIFactory.Accent, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-8, -8), new Vector2(90, 14), true, 2f);
            UIFactory.Panel(lCard.rect, "HealthBack", new Color(1, 1, 1, 0.1f), new Vector2(0, 0), new Vector2(4, 0), new Vector2(CARD_W - 4, 3));
            lCard.health = UIFactory.Panel(lCard.rect, "Health", UIFactory.Good, new Vector2(0, 0), new Vector2(4, 0), new Vector2(CARD_W - 4, 3));
            return lCard;
        }

        // ---------------------------------------------------------------- read-outs

        /// <summary>An agent's state in one word and its colour (empty when nothing stands out).</summary>
        public static (string text, Color color) StatusOf(CasterView pView, PlayerAvatar pAvatar)
        {
            if (pAvatar == null || !pAvatar.Health.IsAlive) return ("OUT", UIFactory.Muted);
            ulong lId = pAvatar.OwnerClientId;
            if (pAvatar.CarriesProof.Value) return ("PROOF", UIFactory.Good);
            if (pView.InCombat(lId)) return ("COMBAT", ThreatMarkers.Red);
            if (pView.IsEngaged(lId)) return ("SPOTTED", ThreatMarkers.Amber);
            if (pAvatar.Submerged.Value) return ("UNDERWATER", ThreatMarkers.Blue);
            if (pAvatar.Hidden.Value) return ("HIDDEN", ThreatMarkers.Blue);
            if (pAvatar.InGrass.Value && pAvatar.Crouched.Value) return ("IN THE GRASS", ThreatMarkers.Blue);
            if (pView.HurtRecently(lId)) return ("HIT", UIFactory.Danger);
            return ("", UIFactory.Muted);
        }

        private static string Hex(Color pColor) => "#" + ColorUtility.ToHtmlStringRGB(pColor);

        private static string Weapon(PlayerAvatar pAvatar)
        {
            ItemDefinition lHeld = pAvatar != null && pAvatar.Weapons != null ? pAvatar.Weapons.HeldItem : null;
            return lHeld != null ? lHeld.displayName.ToUpperInvariant() : "FISTS";
        }

        // ---------------------------------------------------------------- refresh

        /// <summary>Called by the view after it placed its cameras this frame.</summary>
        public void Refresh()
        {
            CasterLayout lShown = _view.ShownLayout;
            _bandTop.enabled = lShown == CasterLayout.Duo;
            _bandBottom.enabled = lShown == CasterLayout.Duo || lShown == CasterLayout.MainPlus3;
            bool lShow = !_view.Clean && !EndScreen.IsShown;
            _group.alpha = lShow ? 1f : 0f;
            _group.blocksRaycasts = lShow;
            if (!lShow) return;

            CasterLayout lLayout = _view.ShownLayout;
            bool lBand = lLayout == CasterLayout.MainPlus3 || lLayout == CasterLayout.Duo;
            float lBandHeight = _root.rect.height * 0.25f;
            float lCardsY = lBand ? Mathf.Max(34f, (lBandHeight - CARD_H) * 0.5f + 8f) : 34f;
            _cardsRow.anchoredPosition = new Vector2(0, lCardsY);
            _help.rectTransform.anchoredPosition = new Vector2(0, 10);
            float lCovered = lBand ? 0f : lCardsY + CARD_H + 8f;

            for (int p = 0; p < _frames.Length; p++)
            {
                Frame lFrame = _frames[p];
                bool lOn = p < _view.ShownCount;
                lFrame.rect.gameObject.SetActive(lOn);
                if (lOn) RefreshFrame(lFrame, p, lLayout, lCovered);
            }
            RefreshTop();
            RefreshCards();
            _help.text = Help();
        }

        private void RefreshFrame(Frame pFrame, int pPosition, CasterLayout pLayout, float pCovered)
        {
            Rect lViewport = CasterLayoutLogic.Viewport(pLayout, pPosition);
            pFrame.rect.anchorMin = lViewport.min;
            pFrame.rect.anchorMax = lViewport.max;
            pFrame.rect.sizeDelta = Vector2.zero;
            pFrame.rect.anchoredPosition = Vector2.zero;
            Vector2 lSize = pFrame.rect.rect.size;
            int lIndex = _view.ShownFeed(pPosition);
            CasterView.Feed lFeed = _view.Feeds[lIndex];
            bool lSelected = lIndex == _view.Selected;
            bool lSmall = lViewport.width < 0.3f;

            bool lMulti = _view.ShownCount > 1;
            foreach (Image lEdge in pFrame.edges)
            {
                lEdge.enabled = lMulti;
                lEdge.color = lSelected ? UIFactory.Accent : new Color(0, 0, 0, 0.9f);
                lEdge.rectTransform.sizeDelta = new Vector2(lSelected ? 3 : 2, lSelected ? 3 : 2);
            }

            string lKind = lFeed.kind switch
            {
                FeedKind.Chase => "CHASE",
                FeedKind.Eyes => "EYES",
                FeedKind.Free => "FREE CAMERA",
                _ => "TACTICAL MAP"
            };
            pFrame.tag.text = (lIndex == 0 ? "<color=#E8483A>LIVE</color>   " : "") + (lIndex == 0 ? "MAIN" : "FEED " + (lIndex + 1)) + "   <color=#9EA0A6>" + lKind + "</color>" + (lSelected && lMulti ? "   <color=#F5A12B>SELECTED</color>" : "");
            pFrame.tag.fontSize = lSmall ? 12 : 15;
            // A feed's tag drops under the top bar when the bar would cover it.
            float lWidth = _root.rect.width;
            float lTagLeft = lViewport.xMin * lWidth + 16f;
            bool lUnderBar = lViewport.yMax > 0.99f && lTagLeft < (lWidth + TOP_W) * 0.5f && lTagLeft + 320f > (lWidth - TOP_W) * 0.5f;
            pFrame.tag.rectTransform.anchoredPosition = new Vector2(16, lUnderBar ? -66 : -12);

            bool lAgentFeed = lFeed.kind == FeedKind.Chase || lFeed.kind == FeedKind.Eyes;
            PlayerAvatar lAvatar = lAgentFeed ? PlayerAvatar.ForClient(lFeed.agent) : null;
            LobbyPlayer lPlayer = lAgentFeed ? LobbyPlayer.ForClient(lFeed.agent) : null;
            bool lAlive = lAvatar != null && lAvatar.Health.IsAlive;

            // The agent's plate, bottom left (lifted above the cards when they overlap the feed).
            pFrame.plate.gameObject.SetActive(lAgentFeed && lPlayer != null);
            if (lAgentFeed && lPlayer != null)
            {
                float lScale = lSmall ? 0.78f : 1f;
                pFrame.plate.localScale = Vector3.one * lScale;
                pFrame.plate.anchoredPosition = new Vector2(14, (lViewport.yMin < 0.01f ? pCovered : 0f) + 14f);
                pFrame.plateBar.color = LobbyPlayer.TeamColor(lPlayer.Team.Value);
                pFrame.plateName.text = _view.NumberOf(lFeed.agent) + "   " + lPlayer.Name.ToUpperInvariant();
                (string lStatus, Color lColor) = StatusOf(_view, lAvatar);
                string lKills = lPlayer.Kills.Value + " K  ·  " + lPlayer.NpcKills.Value + " NPC";
                pFrame.plateLine.text = (lAlive ? Weapon(lAvatar) : "") + (string.IsNullOrEmpty(lStatus) ? "" : "   <font=\"BarlowCondensed-Bold SDF\"><color=" + Hex(lColor) + ">" + lStatus + "</color></font>") + "   <color=#6F7177>" + lKills + "</color>";
                float lRatio = lAvatar != null ? Mathf.Clamp01(lAvatar.Health.Health.Value / Mathf.Max(1f, lAvatar.Health.MaxHealth)) : 0f;
                pFrame.healthFill.rectTransform.sizeDelta = new Vector2((PLATE_W - 5) * lRatio, 3);
                pFrame.healthFill.color = Color.Lerp(UIFactory.Danger, UIFactory.Good, Mathf.Clamp01(lRatio * 1.6f - 0.3f));
            }

            pFrame.centre.text = !lAgentFeed ? "" : lFeed.agent == CasterView.NOBODY ? "WAITING FOR AN AGENT" : !lAlive && lFeed.deadFor > 0.2f ? "<color=#E8483A>ELIMINATED</color>" : "";
            pFrame.centre.fontSize = lSmall ? 20 : 30;

            // Tactical map, or names over the agents in the picture.
            pFrame.usedTags = 0;
            pFrame.usedMarks = 0;
            bool lTactical = lFeed.kind == FeedKind.Tactical;
            pFrame.plan.gameObject.SetActive(lTactical);
            pFrame.planBack.enabled = lTactical;
            if (lTactical) RefreshPlan(pFrame, lSize, lSmall);
            else RefreshTags(pFrame, lFeed, lSize, lSmall);
            for (int i = pFrame.usedTags; i < pFrame.tagPool.Count; i++) pFrame.tagPool[i].gameObject.SetActive(false);
            for (int i = pFrame.usedMarks; i < pFrame.markPool.Count; i++) pFrame.markPool[i].icon.gameObject.SetActive(false);
        }

        private void RefreshTags(Frame pFrame, CasterView.Feed pFeed, Vector2 pSize, bool pSmall)
        {
            Camera lCamera = pFeed.camera;
            foreach (PlayerAvatar lOther in PlayerAvatar.All)
            {
                if (lOther == null || !lOther.Health.IsAlive) continue;
                if (lOther.OwnerClientId == pFeed.agent && pFeed.kind != FeedKind.Free) continue;
                LobbyPlayer lPlayer = lOther.Player;
                string lName = lPlayer != null ? lPlayer.Name.ToUpperInvariant() : "?";
                Tag(pFrame, lCamera, lOther.transform.position + Vector3.up * 2.15f, pSize, "<color=" + Hex(LobbyPlayer.TeamColor(lOther.Team.Value)) + ">" + _view.NumberOf(lOther.OwnerClientId) + "  " + lName + "</color>", pSmall);
            }
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.IsTarget && lNpc.Health.IsAlive)
                    Tag(pFrame, lCamera, lNpc.transform.position + Vector3.up * 2.2f, pSize, "<color=#E8483A>TARGET</color>", pSmall);
            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null && lDirector.Proof.Value == ProofState.Dropped)
                Tag(pFrame, lCamera, lDirector.ProofPosition.Value + Vector3.up * 0.8f, pSize, "<color=#F5A12B>PROOF</color>", pSmall);
        }

        private void Tag(Frame pFrame, Camera pCamera, Vector3 pWorld, Vector2 pSize, string pText, bool pSmall)
        {
            Vector3 lPoint = pCamera.WorldToViewportPoint(pWorld);
            if (lPoint.z <= 0.3f || lPoint.z > TAG_RANGE || lPoint.x < 0.02f || lPoint.x > 0.98f || lPoint.y < 0.04f || lPoint.y > 0.96f) return;
            if (pFrame.usedTags >= pFrame.tagPool.Count)
            {
                TextMeshProUGUI lNew = UIFactory.Title(pFrame.tags, "Tag", "", 15, UIFactory.Paper, TextAlignmentOptions.Bottom, Vector2.zero, Vector2.zero, new Vector2(260, 40), true, 1f);
                lNew.rectTransform.pivot = new Vector2(0.5f, 0f);
                lNew.textWrappingMode = TextWrappingModes.NoWrap;
                pFrame.tagPool.Add(lNew);
            }
            TextMeshProUGUI lText = pFrame.tagPool[pFrame.usedTags++];
            lText.gameObject.SetActive(true);
            lText.text = pText + "\n<size=70%><color=#B8BAC0>" + Mathf.RoundToInt(lPoint.z) + " M</color></size>";
            lText.fontSize = pSmall ? 11 : 15;
            lText.alpha = Mathf.Clamp01((TAG_RANGE - lPoint.z) / 40f);
            lText.rectTransform.anchoredPosition = new Vector2(lPoint.x * pSize.x, lPoint.y * pSize.y);
        }

        private void RefreshPlan(Frame pFrame, Vector2 pSize, bool pSmall)
        {
            float lSide = Mathf.Min(pSize.x, pSize.y) - (pSmall ? 36f : 70f);
            RectTransform lRect = pFrame.plan.rectTransform;
            lRect.sizeDelta = new Vector2(lSide, lSide);
            pFrame.plan.texture = MapPlan.Floor(0);
            pFrame.plan.color = MapPlan.Ready ? Color.white : new Color(0.1f, 0.1f, 0.12f);
            if (!MapPlan.Ready) return;
            float lScale = pSmall ? 0.6f : 1f;

            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || !lNpc.Health.IsAlive) continue;
                if (lNpc.IsTarget)
                {
                    Mark(pFrame, lSide, lNpc.transform.position, UIIcons.Ring, ThreatMarkers.Red, 26f * lScale, "");
                    continue;
                }
                Color lColor = lNpc.State.Value switch
                {
                    NpcState.Combat or NpcState.Calling => ThreatMarkers.Red,
                    NpcState.Engaged or NpcState.Suspicious or NpcState.Search or NpcState.Investigate => ThreatMarkers.Amber,
                    NpcState.KnockedOut => new Color(1, 1, 1, 0.25f),
                    _ => new Color(1, 1, 1, 0.55f)
                };
                Mark(pFrame, lSide, lNpc.transform.position, UIIcons.Dot, lColor, 9f * lScale, "");
            }

            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null)
            {
                if (lDirector.Proof.Value == ProofState.Dropped) Mark(pFrame, lSide, lDirector.ProofPosition.Value, UIIcons.Diamond, UIFactory.Accent, 18f * lScale, "");
                if (lDirector.Proof.Value != ProofState.TargetAlive)
                {
                    List<MatchPoint> lExtractions = MatchPoint.OfKind(MatchPoint.Kind.Extraction);
                    for (int i = 0; i < lExtractions.Count; i++)
                        if ((lDirector.ActiveExtractions.Value & (1 << i)) != 0) Mark(pFrame, lSide, lExtractions[i].transform.position, UIIcons.ThinRing, UIFactory.Good, 30f * lScale, "");
                }
            }

            foreach (PlayerAvatar lAgent in PlayerAvatar.All)
                if (lAgent != null && lAgent.Health.IsAlive)
                    Mark(pFrame, lSide, lAgent.transform.position, UIIcons.Disc, LobbyPlayer.TeamColor(lAgent.Team.Value), 24f * lScale, _view.NumberOf(lAgent.OwnerClientId).ToString());
        }

        private void Mark(Frame pFrame, float pSide, Vector3 pWorld, Sprite pSprite, Color pColor, float pSize, string pLabel)
        {
            if (pFrame.usedMarks >= pFrame.markPool.Count)
            {
                Image lIcon = UIFactory.Panel(pFrame.plan.rectTransform, "Mark", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
                TextMeshProUGUI lLabel = UIFactory.Title(lIcon.rectTransform, "Label", "", 14, UIFactory.Ink, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 20), true, 0f);
                pFrame.markPool.Add((lIcon, lLabel));
            }
            (Image icon, TextMeshProUGUI label) = pFrame.markPool[pFrame.usedMarks++];
            icon.gameObject.SetActive(true);
            icon.sprite = pSprite;
            icon.color = pColor;
            icon.rectTransform.sizeDelta = new Vector2(pSize, pSize);
            Vector2 lUv = MapPlan.ToUv(pWorld);
            icon.rectTransform.anchoredPosition = new Vector2((Mathf.Clamp01(lUv.x) - 0.5f) * pSide, (Mathf.Clamp01(lUv.y) - 0.5f) * pSide);
            label.text = pLabel;
            label.fontSize = pSize * 0.62f;
            // Later marks draw on top: agents last.
            icon.rectTransform.SetAsLastSibling();
        }

        private void RefreshTop()
        {
            LobbyState lState = LobbyState.Instance;
            MatchDirector lDirector = MatchDirector.Instance;
            string lMap = lState != null && lState.Map != null ? lState.Map.displayName.ToUpperInvariant() : "";
            string lMode = lState == null ? "" : lState.Mode.Value switch
            {
                MatchMode.FreeForAll => "FREE-FOR-ALL",
                MatchMode.Teams => lState.TeamCount.Value + " TEAMS",
                _ => "CO-OP"
            };
            string lClock = "", lContract = "";
            if (lDirector != null)
            {
                int lSeconds = Mathf.CeilToInt(lDirector.TimeLeft.Value);
                lClock = (lSeconds / 60).ToString("00") + ":" + (lSeconds % 60).ToString("00");
                if (lSeconds < 60) lClock = "<color=#E8483A>" + lClock + "</color>";
                TargetProfile lTarget = lDirector.Target;
                LobbyPlayer lCarrier = LobbyPlayer.ForClient(lDirector.ProofCarrier.Value);
                lContract = lDirector.Proof.Value switch
                {
                    ProofState.TargetAlive => "<color=#E8483A>TARGET</color>  " + (lTarget != null ? lTarget.displayName.ToUpperInvariant() : "?"),
                    ProofState.Dropped => "<color=#F5A12B>PROOF ON THE GROUND</color>",
                    ProofState.Carried => "<color=#7CF29A>PROOF</color>  " + (lCarrier != null ? lCarrier.Name.ToUpperInvariant() : "?"),
                    _ => "<color=#7CF29A>EXTRACTED</color>"
                };
            }
            const string DOT = "   <color=#6F7177>·</color>   ";
            _info.text = lMap + DOT + lMode + DOT + lClock + DOT + lContract;
            _kicker.text = _view.Restricted ? "ELIMINATED" : "CASTER";
            _kicker.color = _view.Restricted ? UIFactory.Danger : UIFactory.Accent;
            _auto.text = _view.AutoDirector ? "AUTO DIRECTOR" : "MANUAL";
            _auto.color = _view.AutoDirector ? UIFactory.Good : UIFactory.Muted;
        }

        private void RefreshCards()
        {
            List<LobbyPlayer> lAgents = _view.Agents;
            int lCount = Mathf.Min(lAgents.Count, _cards.Count);
            _cardsRow.sizeDelta = new Vector2(lCount * (CARD_W + CARD_GAP), CARD_H);
            Vector2 lMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1, -1);
            CasterView.Feed lSelectedFeed = _view.Feeds[_view.Selected];
            for (int i = 0; i < _cards.Count; i++)
            {
                Card lCard = _cards[i];
                bool lOn = i < lCount;
                lCard.rect.gameObject.SetActive(lOn);
                if (!lOn) continue;

                LobbyPlayer lPlayer = lAgents[i];
                ulong lId = lPlayer.OwnerClientId;
                PlayerAvatar lAvatar = PlayerAvatar.ForClient(lId);
                bool lAlive = lAvatar != null && lAvatar.Health.IsAlive;
                bool lWatchable = _view.CanWatch(lPlayer);

                // Which feeds on screen show this agent.
                string lBadge = "";
                for (int p = 0; p < _view.ShownCount; p++)
                {
                    int lFeedIndex = _view.ShownFeed(p);
                    CasterView.Feed lFeed = _view.Feeds[lFeedIndex];
                    if (lFeed.agent != lId || lFeed.kind == FeedKind.Free || lFeed.kind == FeedKind.Tactical) continue;
                    lBadge += (lBadge.Length > 0 ? " " : "") + (lFeedIndex == 0 ? "MAIN" : "F" + (lFeedIndex + 1));
                }
                bool lOnSelected = lSelectedFeed.agent == lId && (lSelectedFeed.kind == FeedKind.Chase || lSelectedFeed.kind == FeedKind.Eyes);
                bool lHover = RectTransformUtility.RectangleContainsScreenPoint(lCard.rect, lMouse, null);

                lCard.back.color = new Color(Back.r, Back.g, Back.b, lHover && lWatchable ? 0.95f : Back.a);
                lCard.edge.enabled = lBadge.Length > 0;
                lCard.edge.color = lOnSelected ? UIFactory.Accent : new Color(1, 1, 1, 0.5f);
                lCard.badge.text = lBadge;
                lCard.badge.color = lOnSelected ? UIFactory.Accent : UIFactory.Muted;
                lCard.bar.color = LobbyPlayer.TeamColor(lPlayer.Team.Value);
                lCard.name.text = lPlayer.Name.ToUpperInvariant();
                float lDim = lAlive && lWatchable ? 1f : 0.4f;
                lCard.name.alpha = lCard.number.alpha = lDim;
                (string lStatus, Color lColor) = StatusOf(_view, lAvatar);
                lCard.line.text = (string.IsNullOrEmpty(lStatus) ? "" : "<font=\"BarlowCondensed-Bold SDF\"><color=" + Hex(lColor) + ">" + lStatus + "</color></font>   ")
                    + "<color=#9EA0A6>" + lPlayer.Kills.Value + " K  ·  " + lPlayer.NpcKills.Value + " NPC</color>";
                float lRatio = lAvatar != null ? Mathf.Clamp01(lAvatar.Health.Health.Value / Mathf.Max(1f, lAvatar.Health.MaxHealth)) : 0f;
                lCard.health.rectTransform.sizeDelta = new Vector2((CARD_W - 4) * lRatio, 3);
                lCard.health.color = Color.Lerp(UIFactory.Danger, UIFactory.Good, Mathf.Clamp01(lRatio * 1.6f - 0.3f));
            }
        }

        // ---------------------------------------------------------------- hints

        private static string K(Key pKey) => Keyboard.current != null ? Keyboard.current[pKey].displayName.ToUpperInvariant() : pKey.ToString().ToUpperInvariant();

        private string Help()
        {
            bool lPad = Gamepad.current != null && (Keyboard.current == null || Gamepad.current.lastUpdateTime > Keyboard.current.lastUpdateTime) && (Mouse.current == null || Gamepad.current.lastUpdateTime > Mouse.current.lastUpdateTime);
            CasterView.Feed lFeed = _view.Feeds[_view.Selected];
            const string GAP = "      ";
            string W(string pKey, string pWhat) => "<color=#F2F2F0>" + pKey + "</color> " + pWhat + GAP;

            if (lPad)
                return W("LB RB", "AGENT") + W("D-PAD LEFT RIGHT", "FEED") + W("D-PAD UP", "LAYOUT") + W("D-PAD DOWN", "FOCUS") + W("A", "TO MAIN") + W("X", "EYES / CHASE")
                    + (_view.CanFly ? W("Y", "FREE CAMERA") + W("B", "MAP") : "") + W("R3", "AUTO DIRECTOR") + W("R STICK", lFeed.kind == FeedKind.Free ? "LOOK" : "ORBIT")
                    + (lFeed.kind == FeedKind.Free ? W("L STICK  LT RT", "FLY") : "");

            string lHelp = W("1-" + Mathf.Clamp(_view.Agents.Count, 1, 8), "AGENT") + W("ARROWS", "NEXT AGENT") + W("TAB", "FEED") + W(K(Key.Enter), "TO MAIN")
                + W(K(Key.F), "FOCUS") + W("SHIFT+" + K(Key.F), "FOCUS 2") + W("F1-F4", "LAYOUT") + W(K(Key.V), "EYES / CHASE");
            if (_view.CanFly) lHelp += W(K(Key.C), "FREE CAMERA") + W(K(Key.T), "MAP");
            lHelp += W(K(Key.P), "AUTO") + W(K(Key.H), "HIDE") + W("RIGHT CLICK", lFeed.kind == FeedKind.Free ? "LOOK" : "ORBIT") + W("WHEEL", lFeed.kind == FeedKind.Free ? "SPEED" : "ZOOM");
            if (lFeed.kind == FeedKind.Free) lHelp += W(K(Key.W) + K(Key.A) + K(Key.S) + K(Key.D) + "  " + K(Key.E) + K(Key.Q), "FLY");
            return lHelp;
        }
    }
}
