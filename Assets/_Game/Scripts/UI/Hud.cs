using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The in-match HUD: crosshair and hitmarkers, health, ammo, the six inventory slots, the
    /// target dossier and contract status, the clock, prompts, banners, toasts, kill feed, damage
    /// direction arcs, world markers (proof, carrier pings, extractions, teammates), the stealth
    /// eye, the extraction bar, the sniper scope, spectating and voice indicators, the scoreboard.
    /// Static entry points let gameplay code report events without holding a reference.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private static Hud _instance;

        private Canvas _canvas;
        private RectTransform _root;
        private TextMeshProUGUI _crosshair, _hitmarker, _health, _ammo, _weaponName, _prompt, _clock, _dossier, _status, _bannerTitle, _bannerSub, _stealth, _spectating, _voice, _scoreboard;
        private Image _healthFill, _flash, _extractFill, _scope;
        private RectTransform _extractBar, _scoreboardPanel, _markers;
        private readonly TextMeshProUGUI[] _slots = new TextMeshProUGUI[WeaponHolder.SLOT_COUNT];
        private readonly Image[] _slotBacks = new Image[WeaponHolder.SLOT_COUNT];
        private readonly List<(TextMeshProUGUI text, float until)> _toasts = new List<(TextMeshProUGUI, float)>();
        private readonly List<(TextMeshProUGUI text, float until)> _feed = new List<(TextMeshProUGUI, float)>();
        private readonly List<(Image arc, Vector3 from, float until)> _damageArcs = new List<(Image, Vector3, float)>();
        private readonly Dictionary<string, TextMeshProUGUI> _worldMarkers = new Dictionary<string, TextMeshProUGUI>();
        private float _hitmarkerTime, _bannerTime, _flashAlpha;
        private Color _flashColor;
        private Vector3 _carrierPing;
        private float _carrierPingTime = -99f;
        private float _spreadKick;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GameObject("Hud").AddComponent<Hud>();
        }

        public static void Remove()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        // ---------------------------------------------------------------- static API

        public static void Hitmarker(bool pHead)
        {
            if (_instance == null) return;
            _instance._hitmarkerTime = 0.25f;
            _instance._hitmarker.color = pHead ? new Color(1f, 0.3f, 0.3f) : Color.white;
            _instance._hitmarker.fontSize = pHead ? 70 : 52;
            Audio.Sfx.Play2D(pHead ? "hit_head" : "hit", 0.5f);
        }

        public static void Toast(string pMessage)
        {
            if (_instance == null) return;
            TextMeshProUGUI lText = UIFactory.Text(_instance._root, "Toast", pMessage, 28, UIFactory.Paper, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(760, 40));
            _instance._toasts.Insert(0, (lText, Time.time + 4f));
        }

        public static void Announce(string pTitle, string pSubtitle)
        {
            if (_instance == null) return;
            _instance._bannerTitle.text = pTitle;
            _instance._bannerSub.text = pSubtitle;
            _instance._bannerTime = 4.5f;
            Audio.Sfx.Play2D("announce", 0.7f);
        }

        public static void KillFeed(string pLine)
        {
            if (_instance == null) return;
            TextMeshProUGUI lText = UIFactory.Text(_instance._root, "Feed", pLine, 24, UIFactory.Paper, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-30, 0), new Vector2(700, 34));
            _instance._feed.Insert(0, (lText, Time.time + 7f));
        }

        public static void ReportDamageFrom(Vector3 pWorldPoint)
        {
            if (_instance == null) return;
            Image lArc = UIFactory.Panel(_instance._root, "DamageArc", new Color(1f, 0.15f, 0.15f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 22));
            _instance._damageArcs.Add((lArc, pWorldPoint, Time.time + 1.4f));
        }

        public static void PingCarrier(Vector3 pPosition)
        {
            if (_instance == null) return;
            _instance._carrierPing = pPosition;
            _instance._carrierPingTime = Time.time;
        }

        // ---------------------------------------------------------------- build

        private void Awake()
        {
            _canvas = UIFactory.Canvas("HudCanvas", 10);
            _canvas.transform.SetParent(transform, false);
            _root = (RectTransform)_canvas.transform;
            Vector2 C = new Vector2(0.5f, 0.5f);

            _markers = UIFactory.Stretch(_root, "Markers");

            _scope = UIFactory.Stretch(_root, "Scope").gameObject.AddComponent<Image>();
            _scope.sprite = ScopeSprite();
            _scope.color = Color.black;
            _scope.preserveAspect = false;
            _scope.raycastTarget = false;

            _crosshair = UIFactory.Text(_root, "Crosshair", "+", 44, Color.white, TextAlignmentOptions.Center, C, Vector2.zero, new Vector2(120, 120));
            _hitmarker = UIFactory.Text(_root, "Hitmarker", "x", 52, Color.white, TextAlignmentOptions.Center, C, new Vector2(0, 2), new Vector2(120, 120));
            _prompt = UIFactory.Text(_root, "Prompt", "", 30, UIFactory.Paper, TextAlignmentOptions.Center, C, new Vector2(0, -90), new Vector2(900, 50));

            // Health, bottom left.
            Image lHealthPanel = UIFactory.Panel(_root, "HealthPanel", UIFactory.PanelColor, new Vector2(0, 0), new Vector2(40, 40), new Vector2(360, 90));
            lHealthPanel.rectTransform.pivot = Vector2.zero;
            Image lHealthTrack = UIFactory.Panel(lHealthPanel.transform, "Track", new Color(1, 1, 1, 0.15f), new Vector2(0, 0.5f), new Vector2(180, -14), new Vector2(320, 22));
            _healthFill = UIFactory.Panel(lHealthTrack.transform, "Fill", UIFactory.Good, new Vector2(0, 0.5f), Vector2.zero, new Vector2(320, 22));
            _healthFill.rectTransform.pivot = new Vector2(0, 0.5f);
            _health = UIFactory.Text(lHealthPanel.transform, "Health", "100", 40, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(110, 18), new Vector2(200, 50));
            _stealth = UIFactory.Text(lHealthPanel.transform, "Stealth", "", 24, new Color(0.6f, 1f, 0.7f), TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-90, 18), new Vector2(200, 40));

            // Ammo, bottom right.
            Image lAmmoPanel = UIFactory.Panel(_root, "AmmoPanel", UIFactory.PanelColor, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(360, 90));
            lAmmoPanel.rectTransform.pivot = new Vector2(1, 0);
            _ammo = UIFactory.Text(lAmmoPanel.transform, "Ammo", "", 40, UIFactory.Paper, TextAlignmentOptions.Right, new Vector2(1, 0.5f), new Vector2(-125, -14), new Vector2(220, 52));
            _weaponName = UIFactory.Text(lAmmoPanel.transform, "Weapon", "", 20, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(-175, -16), new Vector2(320, 26));

            // Inventory slots, bottom centre.
            for (int i = 0; i < WeaponHolder.SLOT_COUNT; i++)
            {
                float x = (i - 2.5f) * 132f;
                _slotBacks[i] = UIFactory.Panel(_root, "Slot" + i, UIFactory.PanelColor, new Vector2(0.5f, 0), new Vector2(x, 70), new Vector2(124, 70));
                _slots[i] = UIFactory.Text(_slotBacks[i].transform, "Label", "", 18, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118, 66));
            }

            // Contract, top left.
            Image lDossierPanel = UIFactory.Panel(_root, "Dossier", UIFactory.PanelColor, new Vector2(0, 1), new Vector2(40, -40), new Vector2(660, 200));
            lDossierPanel.rectTransform.pivot = new Vector2(0, 1);
            _dossier = UIFactory.Text(lDossierPanel.transform, "Text", "", 21, UIFactory.Paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(30, -24), new Vector2(610, 130));
            _status = UIFactory.Text(lDossierPanel.transform, "Status", "", 24, UIFactory.Accent, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(30, 16), new Vector2(610, 34));

            _clock = UIFactory.Text(_root, "Clock", "20:00", 40, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(240, 60));
            _bannerTitle = UIFactory.Text(_root, "BannerTitle", "", 72, UIFactory.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1600, 90));
            _bannerSub = UIFactory.Text(_root, "BannerSub", "", 32, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.72f), new Vector2(0, -70), new Vector2(1600, 50));

            _extractBar = UIFactory.Panel(_root, "ExtractBar", UIFactory.PanelColor, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(420, 26)).rectTransform;
            _extractFill = UIFactory.Panel(_extractBar, "Fill", UIFactory.Good, new Vector2(0, 0.5f), Vector2.zero, new Vector2(420, 26));
            _extractFill.rectTransform.pivot = new Vector2(0, 0.5f);

            _spectating = UIFactory.Text(_root, "Spectating", "", 34, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(1200, 50));
            _voice = UIFactory.Text(_root, "Voice", "", 22, UIFactory.Good, TextAlignmentOptions.Left, new Vector2(0, 0), new Vector2(240, 170), new Vector2(420, 120));

            _scoreboardPanel = UIFactory.Panel(_root, "Scoreboard", UIFactory.PanelColor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 560)).rectTransform;
            _scoreboard = UIFactory.Text(_scoreboardPanel, "Text", "", 28, UIFactory.Paper, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840, 520));

            _flash = UIFactory.Stretch(_root, "Flash").gameObject.AddComponent<Image>();
            _flash.raycastTarget = false;
            _flash.color = new Color(1, 1, 1, 0);

            // Typography: condensed capitals for numbers and titles, the accent as a thin rule.
            foreach (TextMeshProUGUI lText in new[] { _health, _ammo, _clock, _bannerTitle, _bannerSub, _weaponName, _status, _stealth, _spectating, _prompt })
            {
                lText.font = UIFactory.Heading;
                lText.characterSpacing = 4f;
            }
            _bannerTitle.font = UIFactory.HeadingBold;
            _bannerTitle.characterSpacing = 14f;
            _bannerTitle.color = UIFactory.Paper;
            _health.font = _ammo.font = _clock.font = UIFactory.HeadingBold;
            foreach (TextMeshProUGUI lSlot in _slots) { lSlot.font = UIFactory.Heading; lSlot.characterSpacing = 3f; }
            UIFactory.Rule(lDossierPanel.transform, new Vector2(0, 1), Vector2.zero, 200, true, UIFactory.Accent, 4f);
            UIFactory.Rule(_root, new Vector2(0.5f, 0.72f), new Vector2(0, -36), 160, false, UIFactory.Accent, 3f).name = "BannerRule";
            _crosshair.fontSize = 34;

            Juice.OnScreenFlash += HandleFlash;
        }

        private void OnDestroy()
        {
            Juice.OnScreenFlash -= HandleFlash;
            if (_instance == this) _instance = null;
        }

        private void HandleFlash(Color pColor, float pStrength)
        {
            _flashColor = pColor;
            _flashAlpha = Mathf.Max(_flashAlpha, pStrength);
        }

        private static Sprite ScopeSprite()
        {
            const int SIZE = 256;
            Texture2D lTexture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dx = (x - SIZE / 2f) / (SIZE / 2f), dy = (y - SIZE / 2f) / (SIZE / 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - 0.78f) * 25f);
                    bool lLine = (Mathf.Abs(dx) < 0.006f || Mathf.Abs(dy) < 0.006f) && d < 0.78f;
                    lTexture.SetPixel(x, y, new Color(0, 0, 0, lLine ? 0.8f : a));
                }
            lTexture.Apply();
            return Sprite.Create(lTexture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f));
        }

        // ---------------------------------------------------------------- update

        private void Update()
        {
            float dt = Time.deltaTime;
            PlayerAvatar lMe = PlayerAvatar.Local;
            MatchDirector lDirector = MatchDirector.Instance;
            bool lAlive = lMe != null && lMe.Health.IsAlive;

            // Crosshair: spreads with movement and shots, a dot when aiming.
            WeaponHolder lWeapons = lAlive ? lMe.Weapons : null;
            ItemDefinition lHeld = lWeapons != null ? lWeapons.HeldItem : null;
            bool lScoped = lWeapons != null && lWeapons.Scoped;
            _scope.enabled = lScoped;
            _crosshair.enabled = lAlive && !lScoped;
            float lSpread = lMe != null ? Mathf.Clamp01(lMe.Motor.PlanarSpeed / 7f) : 0f;
            _crosshair.text = lWeapons != null && lWeapons.AdsFov > 0f ? "·" : lHeld != null && lHeld.kind == ItemKind.Shotgun ? "( + )" : "+";
            _crosshair.rectTransform.localScale = Vector3.one * (1f + lSpread * 0.6f);
            _hitmarkerTime -= dt;
            _hitmarker.enabled = _hitmarkerTime > 0f;
            _hitmarker.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, _hitmarkerTime) * 2f);

            _prompt.text = lAlive ? PlayerInteractor.CurrentPrompt : "";

            // Health.
            if (lMe != null)
            {
                float lHp = lMe.Health.Health.Value;
                float lRatio = Mathf.Clamp01(lHp / Mathf.Max(1f, lMe.Health.MaxHealth));
                _health.text = Mathf.CeilToInt(lHp).ToString();
                _healthFill.rectTransform.sizeDelta = new Vector2(320 * lRatio, 22);
                _healthFill.color = Color.Lerp(UIFactory.Accent, UIFactory.Paper, Mathf.Clamp01(lRatio * 2f - 0.3f));
                if (lRatio < 0.3f && lAlive && CameraRig.Instance != null) CameraRig.Instance.PulseHurt(0.15f + Mathf.Sin(Time.time * 6f) * 0.05f);
                bool lConcealed = lMe.Hidden.Value || (lMe.Crouched.Value && lMe.InGrass.Value);
                _stealth.text = lMe.Hidden.Value ? "HIDDEN" : lConcealed ? "CONCEALED" : lMe.Crouched.Value ? "CROUCHED" : lMe.Sprinting.Value ? "<color=#FFB13B>LOUD</color>" : "";
            }

            // Ammo and slots.
            if (lWeapons != null)
            {
                InventorySlot lSlot = lWeapons.SlotAt(lWeapons.Selected.Value);
                _weaponName.text = lHeld != null ? lHeld.displayName.ToUpperInvariant() : "FISTS";
                _ammo.text = lHeld == null ? "" : lHeld.UsesAmmo ? (lWeapons.Reloading.Value ? "<size=60%>RELOADING</size>" : lSlot.magazine + "<size=60%> / " + lSlot.reserve + "</size>") : "x" + lSlot.reserve;
                for (int i = 0; i < WeaponHolder.SLOT_COUNT; i++)
                {
                    InventorySlot s = lWeapons.SlotAt(i);
                    ItemDefinition lItem = GameDatabase.Item(s.item);
                    string lName = lItem != null ? lItem.displayName.ToUpperInvariant() : (i < WeaponHolder.WEAPON_SLOTS ? "FISTS" : "-");
                    if (lItem != null && lItem.slot == ItemSlot.Gadget) lName += " x" + s.reserve;
                    _slots[i].text = "<size=70%>" + (i + 1) + "</size>\n" + lName;
                    bool lSelected = lWeapons.Selected.Value == i;
                    _slotBacks[i].color = lSelected ? new Color(1, 1, 1, 0.16f) : UIFactory.PanelColor;
                    _slots[i].color = lSelected ? UIFactory.Paper : UIFactory.Muted;
                }
            }

            // Contract.
            if (lDirector != null)
            {
                TargetProfile lTarget = lDirector.Target;
                MapDefinition lMap = lDirector.Map;
                string lZone = lTarget != null && lMap != null && lTarget.zone < lMap.zoneNames.Length ? lMap.zoneNames[lTarget.zone] : "";
                _dossier.text = lTarget == null ? "" : "<size=120%><color=#FFFFFF>" + lTarget.displayName.ToUpperInvariant() + "</color></size>\n<size=85%>" + lTarget.dossier + "\nSEEN AROUND: " + lZone.ToUpperInvariant() + "  -  PROOF: " + lTarget.proof.ToString().ToUpperInvariant() + "</size>";
                string lCarrier = "";
                if (lDirector.Proof.Value == ProofState.Carried)
                {
                    LobbyPlayer lCarrierPlayer = LobbyPlayer.ForClient(lDirector.ProofCarrier.Value);
                    lCarrier = lCarrierPlayer != null ? lCarrierPlayer.Name.ToUpperInvariant() : "?";
                }
                _status.text = lDirector.Proof.Value switch
                {
                    ProofState.TargetAlive => "TARGET ALIVE",
                    ProofState.Dropped => "<color=#FFE066>PROOF ON THE GROUND</color>",
                    ProofState.Carried => lMe != null && lMe.CarriesProof.Value ? "<color=#7CF29A>YOU HAVE THE PROOF - EXTRACT!</color>" : "<color=#FFB13B>" + lCarrier + " HAS THE PROOF</color>",
                    _ => "EXTRACTED"
                };
                int lSeconds = Mathf.CeilToInt(lDirector.TimeLeft.Value);
                _clock.text = (lSeconds / 60).ToString("00") + ":" + (lSeconds % 60).ToString("00");
                _clock.color = lSeconds < 60 ? UIFactory.Accent : UIFactory.Paper;

                bool lExtracting = lMe != null && lMe.CarriesProof.Value && lDirector.ExtractProgress.Value > 0f;
                _extractBar.gameObject.SetActive(lExtracting);
                if (lExtracting) _extractFill.rectTransform.sizeDelta = new Vector2(420 * Mathf.Clamp01(lDirector.ExtractProgress.Value), 26);
                UpdateWorldMarkers(lDirector, lMe);
            }

            _spectating.text = Spectator.Active && Spectator.Instance.Watched != null ? "SPECTATING " + Spectator.Instance.Watched.DisplayName.ToUpperInvariant() + "   <size=70%>[SPACE] NEXT</size>" : Spectator.Active ? "ELIMINATED" : "";

            // Voice.
            string lTalking = "";
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All) if (lPlayer.TalkingNow.Value) lTalking += "<sprite=0> " + lPlayer.Name + "\n";
            _voice.text = lTalking.Replace("<sprite=0>", ")))");

            // Banner, flash.
            _bannerTime -= dt;
            float lBanner = Mathf.Clamp01(_bannerTime);
            _bannerTitle.alpha = _bannerSub.alpha = lBanner;
            _bannerTitle.characterSpacing = 14f + Mathf.Max(0f, _bannerTime - 3.8f) * 40f;
            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt * 2.5f);
            _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAlpha);

            // Stacks.
            Restack(_toasts, -60f, 44f, new Vector2(1, 0.5f));
            Restack(_feed, -40f, 36f, new Vector2(1, 1));
            UpdateDamageArcs(lMe);

            bool lBoard = GameInput.ScoreboardHeld || (lDirector != null && lDirector.Phase.Value == MatchPhase.Ended);
            _scoreboardPanel.gameObject.SetActive(GameInput.ScoreboardHeld);
            if (lBoard) _scoreboard.text = Scoreboard();
        }

        private void Restack(List<(TextMeshProUGUI text, float until)> pList, float pStart, float pStep, Vector2 pAnchor)
        {
            for (int i = pList.Count - 1; i >= 0; i--)
            {
                if (Time.time > pList[i].until || i > 6)
                {
                    Destroy(pList[i].text.gameObject);
                    pList.RemoveAt(i);
                    continue;
                }
                RectTransform lRect = pList[i].text.rectTransform;
                float lTarget = pAnchor.y > 0.9f ? pStart - i * pStep - 60f : pStart - i * pStep + 200f;
                lRect.anchoredPosition = Vector2.Lerp(lRect.anchoredPosition, new Vector2(-30f, lTarget), 1f - Mathf.Exp(-12f * Time.deltaTime));
                pList[i].text.alpha = Mathf.Clamp01(pList[i].until - Time.time);
            }
        }

        private void UpdateDamageArcs(PlayerAvatar pMe)
        {
            for (int i = _damageArcs.Count - 1; i >= 0; i--)
            {
                (Image lArc, Vector3 lFrom, float lUntil) = _damageArcs[i];
                if (Time.time > lUntil || pMe == null) { Destroy(lArc.gameObject); _damageArcs.RemoveAt(i); continue; }
                Vector3 lLocal = pMe.transform.InverseTransformPoint(lFrom);
                float lAngle = Mathf.Atan2(lLocal.x, lLocal.z);
                lArc.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(lAngle), Mathf.Cos(lAngle)) * 170f;
                lArc.rectTransform.localRotation = Quaternion.Euler(0, 0, -lAngle * Mathf.Rad2Deg);
                lArc.color = new Color(1f, 0.15f, 0.15f, Mathf.Clamp01(lUntil - Time.time));
            }
        }

        private void UpdateWorldMarkers(MatchDirector pDirector, PlayerAvatar pMe)
        {
            HashSet<string> lUsed = new HashSet<string>();
            Camera lCamera = CameraRig.Instance != null ? CameraRig.Instance.WorldCamera : null;
            if (lCamera == null) return;

            if (pDirector.Proof.Value == ProofState.Dropped) Marker(lUsed, lCamera, "proof", pDirector.ProofPosition.Value + Vector3.up, "<color=#FFE066>PROOF</color>");
            if (pDirector.Proof.Value == ProofState.Carried && (pMe == null || !pMe.CarriesProof.Value) && Time.time - _carrierPingTime < 4f)
                Marker(lUsed, lCamera, "carrier", _carrierPing + Vector3.up * 2.2f, "<color=#FFB13B>CARRIER</color>");
            if (pDirector.Proof.Value != ProofState.TargetAlive)
            {
                List<AI.MatchPoint> lExtractions = AI.MatchPoint.OfKind(AI.MatchPoint.Kind.Extraction);
                for (int i = 0; i < lExtractions.Count; i++)
                    if ((pDirector.ActiveExtractions.Value & (1 << i)) != 0)
                        Marker(lUsed, lCamera, "ex" + i, lExtractions[i].transform.position + Vector3.up * 3f, "<color=#7CF29A>EXTRACT</color>\n<size=60%>" + lExtractions[i].Label.ToUpperInvariant() + "</size>");
            }
            if (pMe != null && LobbyState.Instance != null && LobbyState.Instance.Mode.Value != MatchMode.FreeForAll)
                foreach (PlayerAvatar lMate in PlayerAvatar.All)
                    if (lMate != pMe && lMate.Health.IsAlive && lMate.Team.Value == pMe.Team.Value)
                        Marker(lUsed, lCamera, "mate" + lMate.OwnerClientId, lMate.transform.position + Vector3.up * 2.3f, "<color=#" + ColorUtility.ToHtmlStringRGB(LobbyPlayer.TeamColor(lMate.Team.Value)) + ">" + lMate.DisplayName + "</color>");

            List<string> lRemove = new List<string>();
            foreach (KeyValuePair<string, TextMeshProUGUI> lPair in _worldMarkers) if (!lUsed.Contains(lPair.Key)) lRemove.Add(lPair.Key);
            foreach (string lKey in lRemove) { Destroy(_worldMarkers[lKey].gameObject); _worldMarkers.Remove(lKey); }
        }

        private void Marker(HashSet<string> pUsed, Camera pCamera, string pKey, Vector3 pWorld, string pText)
        {
            pUsed.Add(pKey);
            if (!_worldMarkers.TryGetValue(pKey, out TextMeshProUGUI lText))
            {
                lText = UIFactory.Text(_markers, "Marker_" + pKey, pText, 24, Color.white, TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, new Vector2(300, 80));
                _worldMarkers[pKey] = lText;
            }
            Vector3 lScreen = pCamera.WorldToScreenPoint(pWorld);
            bool lBehind = lScreen.z < 0f;
            if (lBehind) lScreen = new Vector3(Screen.width - lScreen.x, 0, 0);
            float lScale = _root.rect.width / Mathf.Max(1f, Screen.width);
            Vector2 lPos = new Vector2(Mathf.Clamp(lScreen.x, 60, Screen.width - 60), Mathf.Clamp(lScreen.y, 60, Screen.height - 60)) * lScale;
            lText.rectTransform.anchoredPosition = lPos;
            float lDistance = Vector3.Distance(pCamera.transform.position, pWorld);
            lText.text = pText + "\n<size=55%>" + Mathf.RoundToInt(lDistance) + "m</size>";
        }

        private static string Scoreboard()
        {
            System.Text.StringBuilder lBuilder = new System.Text.StringBuilder();
            lBuilder.Append("<size=120%>PLAYERS</size>\n\n");
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All)
            {
                string lColor = ColorUtility.ToHtmlStringRGB(LobbyPlayer.TeamColor(lPlayer.Team.Value));
                lBuilder.Append("<color=#").Append(lColor).Append(">").Append(lPlayer.Name).Append("</color>")
                    .Append(lPlayer.Alive.Value ? "" : "  <color=#888>(OUT)</color>")
                    .Append("<pos=60%>").Append(lPlayer.Kills.Value).Append(" PLAYERS").Append("<pos=80%>").Append(lPlayer.NpcKills.Value).Append(" NPC\n");
            }
            return lBuilder.ToString();
        }
    }
}
