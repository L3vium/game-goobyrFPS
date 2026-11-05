using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
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
    /// The in-match HUD: crosshair and hit marks, health, ammo, the inventory slots, the contract
    /// status, the clock, prompts, kill feed, damage direction arcs, world markers (proof, carrier
    /// pings, extractions, teammates), the stealth
    /// eye, the extraction bar, the sniper scope, spectating and voice indicators, the scoreboard.
    /// Static entry points let gameplay code report events without holding a reference.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        private static Hud _instance;

        /// <summary>The mark of the proof's carrier, on every map and compass.</summary>
        public static readonly Color CARRIER_YELLOW = new Color(1f, 0.85f, 0.23f);

        private Canvas _canvas;
        private RectTransform _root;
        private TextMeshProUGUI _health, _ammo, _weaponName, _prompt, _clock, _dossier, _status, _stealth, _spectating, _voice, _scoreboard;
        private readonly Image[] _crossTicks = new Image[4];
        private readonly Image[] _hitBars = new Image[4];
        private Image _crossDot, _skull;
        /// <summary>Where you stand socially: nothing in public, TRESPASSING (amber), HOSTILE AREA (red).</summary>
        private TextMeshProUGUI _zone;
        private Image _healthFill, _flash, _extractFill, _scope;
        private RectTransform _extractBar, _scoreboardPanel, _markers;
        private CanvasGroup _healthGroup, _ammoGroup, _stripGroup;
        private readonly TextMeshProUGUI[] _stripNames = new TextMeshProUGUI[WeaponHolder.SLOT_COUNT];
        private readonly TextMeshProUGUI[] _stripCounts = new TextMeshProUGUI[WeaponHolder.SLOT_COUNT];
        private readonly Image[] _stripBars = new Image[WeaponHolder.SLOT_COUNT];
        private readonly Image[] _stripBacks = new Image[WeaponHolder.SLOT_COUNT];
        private float _healthShowUntil, _ammoShowUntil, _lastHp = -1f;
        private string _lastAmmo = "";
        private readonly List<(TextMeshProUGUI text, float until)> _feed = new List<(TextMeshProUGUI, float)>();
        private readonly List<(Image arc, Vector3 from, float until)> _damageArcs = new List<(Image, Vector3, float)>();
        private readonly Dictionary<string, TextMeshProUGUI> _worldMarkers = new Dictionary<string, TextMeshProUGUI>();
        private float _hitTime, _hitLength = 0.2f, _skullTime, _flashAlpha;
        private bool _hitHead;
        /// <summary>How long the skull of a headshot, and of a kill, stays over the crosshair.</summary>
        private const float SKULL_HEAD = 0.45f, SKULL_KILL = 0.8f;
        private Color _flashColor;
        private Vector3? _carrierPing;

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

        /// <summary>A hit: white marks around the crosshair; on the head they are red, with the skull.</summary>
        public static void Hitmarker(bool pHead)
        {
            if (_instance == null) return;
            _instance._hitTime = _instance._hitLength = pHead ? 0.3f : 0.2f;
            _instance._hitHead = pHead;
            if (pHead) _instance._skullTime = Mathf.Max(_instance._skullTime, SKULL_HEAD);
            Audio.Sfx.Play2D(pHead ? "hit_head" : "hit", 0.5f);
        }

        /// <summary>The host's word: a hit it alone could see, or a kill (the marks go red, the skull stays longer).</summary>
        private void HandleConfirmed(bool pHead, bool pKill)
        {
            if (pKill)
            {
                _hitTime = _hitLength = 0.4f;
                _hitHead = true;
                _skullTime = SKULL_KILL;
                Juice.Kill(pHead);
                Audio.Sfx.Play2D("hit_head", 0.7f);
            }
            else Hitmarker(pHead);
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
            if (_instance != null) _instance._carrierPing = pPosition;
        }

        /// <summary>Where the proof's carrier was last revealed, if anywhere yet.</summary>
        public static Vector3? CarrierPing => _instance != null ? _instance._carrierPing : null;

        // ---------------------------------------------------------------- build

        private void Awake()
        {
            _canvas = UIFactory.Canvas("HudCanvas", 10);
            _canvas.transform.SetParent(transform, false);
            _root = (RectTransform)_canvas.transform;
            Vector2 C = new Vector2(0.5f, 0.5f);

            _markers = UIFactory.Stretch(_root, "Markers");
            gameObject.AddComponent<ThreatMarkers>().Build(_root);

            _scope = UIFactory.Stretch(_root, "Scope").gameObject.AddComponent<Image>();
            _scope.sprite = ScopeSprite();
            _scope.color = Color.black;
            _scope.preserveAspect = false;
            _scope.raycastTarget = false;

            BuildCrosshair(C);
            _prompt = UIFactory.Text(_root, "Prompt", "", 30, UIFactory.Paper, TextAlignmentOptions.Center, C, new Vector2(0, -90), new Vector2(900, 50));

            // Health, bottom left.
            Image lHealthPanel = UIFactory.Panel(_root, "HealthPanel", new Color(0, 0, 0, 0), new Vector2(0.5f, 0), new Vector2(0, 38), new Vector2(360, 60));
            _healthGroup = lHealthPanel.gameObject.AddComponent<CanvasGroup>();
            lHealthPanel.rectTransform.pivot = new Vector2(0.5f, 0);
            Image lHealthTrack = UIFactory.Panel(lHealthPanel.transform, "Track", new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(320, 4));
            _healthFill = UIFactory.Panel(lHealthTrack.transform, "Fill", UIFactory.Good, new Vector2(0, 0.5f), Vector2.zero, new Vector2(320, 4));
            _healthFill.rectTransform.pivot = new Vector2(0, 0.5f);
            _health = UIFactory.Text(lHealthPanel.transform, "Health", "100", 22, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(200, 30));
            _stealth = UIFactory.Text(lHealthPanel.transform, "Stealth", "", 18, new Color(0.6f, 1f, 0.7f), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 36), new Vector2(200, 26));
            _zone = UIFactory.Text(_root, "Zone", "", 17, UIFactory.Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 112), new Vector2(500, 26));

            // Ammo, bottom right.
            Image lAmmoPanel = UIFactory.Panel(_root, "AmmoPanel", new Color(0, 0, 0, 0), new Vector2(1, 0), new Vector2(-40, 36), new Vector2(320, 70));
            lAmmoPanel.rectTransform.pivot = new Vector2(1, 0);
            _ammoGroup = lAmmoPanel.gameObject.AddComponent<CanvasGroup>();
            _ammo = UIFactory.Text(lAmmoPanel.transform, "Ammo", "", 30, UIFactory.Paper, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(-160, 18), new Vector2(320, 40));
            _weaponName = UIFactory.Text(lAmmoPanel.transform, "Weapon", "", 15, new Color(1, 1, 1, 0.7f), TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(-160, 54), new Vector2(320, 22));

            // The inventory: a horizontal bar, bottom centre, always there.
            const float SLOT_W = 158f, SLOT_GAP = 6f;
            float lBarWidth = WeaponHolder.SLOT_COUNT * SLOT_W + (WeaponHolder.SLOT_COUNT - 1) * SLOT_GAP;
            RectTransform lStrip = UIFactory.Rect(_root, "Inventory", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(lBarWidth, 44));
            _stripGroup = lStrip.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < WeaponHolder.SLOT_COUNT; i++)
            {
                RectTransform lCell = UIFactory.Rect(lStrip, "Slot" + i, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(i * (SLOT_W + SLOT_GAP), 0), new Vector2(SLOT_W, 44));
                _stripBacks[i] = lCell.gameObject.AddComponent<Image>();
                _stripBacks[i].raycastTarget = false;
                _stripBars[i] = UIFactory.Panel(lCell, "Bar", UIFactory.Accent, new Vector2(0.5f, 0), new Vector2(0, 1.5f), new Vector2(SLOT_W, 3));
                TextMeshProUGUI lKey = UIFactory.Text(lCell, "Key", (i + 1).ToString(), 13, UIFactory.Muted, TextAlignmentOptions.Center, new Vector2(0, 0.5f), new Vector2(11, 0), new Vector2(18, 40));
                lKey.font = UIFactory.Heading;
                _stripNames[i] = UIFactory.Text(lCell, "Name", "", 15, UIFactory.Paper, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(82, 7), new Vector2(120, 22));
                _stripNames[i].overflowMode = TextOverflowModes.Ellipsis;
                _stripNames[i].font = UIFactory.Heading;
                _stripCounts[i] = UIFactory.Text(lCell, "Count", "", 13, UIFactory.Muted, TextAlignmentOptions.Left, new Vector2(0, 0.5f), new Vector2(82, -10), new Vector2(120, 18));
                _stripCounts[i].font = UIFactory.Heading;
            }

            // No contract panel: the contract is on the map screen. Only the proof, once in play, gets a quiet line.
            _dossier = UIFactory.Text(_root, "Dossier", "", 1, UIFactory.Paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(-4000, 0), new Vector2(10, 10));
            _status = UIFactory.Text(_root, "Status", "", 20, UIFactory.Paper, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(40, -36), new Vector2(700, 30));

            _clock = UIFactory.Text(_root, "Clock", "", 26, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(240, 40));
            gameObject.AddComponent<Minimap>().Build(_root);
            gameObject.AddComponent<Compass>().Build(_root);

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
            foreach (TextMeshProUGUI lText in new[] { _health, _ammo, _clock, _weaponName, _status, _stealth, _zone, _spectating, _prompt })
            {
                lText.font = UIFactory.Heading;
                lText.characterSpacing = 4f;
            }
            _health.font = _ammo.font = _clock.font = UIFactory.HeadingBold;
            _health.font = _ammo.font = UIFactory.Heading;

            if (DevCheats.Allowed) gameObject.AddComponent<DevCheats>().Build(_root);

            Juice.OnScreenFlash += HandleFlash;
            Core.Damageable.OnConfirmed += HandleConfirmed;
        }

        /// <summary>One crosshair whatever is held: a dot and four ticks, the hit marks on its diagonals, the skull over it.</summary>
        private void BuildCrosshair(Vector2 pCentre)
        {
            RectTransform lCross = UIFactory.Rect(_root, "Crosshair", pCentre, pCentre, pCentre, Vector2.zero, Vector2.zero);
            _crossDot = UIFactory.Panel(lCross, "Dot", Color.white, pCentre, Vector2.zero, new Vector2(3, 3));
            for (int i = 0; i < 4; i++)
                _crossTicks[i] = UIFactory.Panel(lCross, "Tick" + i, Color.white, pCentre, Vector2.zero, i % 2 == 0 ? new Vector2(2, 8) : new Vector2(8, 2));
            foreach (Image lPart in lCross.GetComponentsInChildren<Image>()) Outlined(lPart);
            for (int i = 0; i < 4; i++)
            {
                _hitBars[i] = UIFactory.Panel(lCross, "Hit" + i, Color.white, pCentre, Vector2.zero, new Vector2(16, 3));
                _hitBars[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
                Outlined(_hitBars[i]);
            }
            _skull = UIFactory.Panel(lCross, "Skull", new Color(1f, 0.16f, 0.14f), pCentre, new Vector2(0, 58), new Vector2(46, 46));
            _skull.sprite = SkullSprite();
            _skull.preserveAspect = true;
        }

        private static void Outlined(Image pImage)
        {
            pImage.raycastTarget = false;
            Shadow lShadow = pImage.gameObject.AddComponent<Shadow>();
            lShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            lShadow.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>The kill mark, drawn here: a round skull, its jaw and teeth, two eyes and a nose cut out.</summary>
        private static Sprite SkullSprite()
        {
            const int SIZE = 96;
            Texture2D lTexture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float Disc(Vector2 pPoint, Vector2 pCentre, float pRadius) => Mathf.Clamp01(pRadius - Vector2.Distance(pPoint, pCentre) + 0.5f);
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float lBody = Disc(p, new Vector2(48, 56), 36f);
                    bool lJaw = p.x > 28f && p.x < 68f && p.y > 12f && p.y < 40f;
                    if (lJaw) lBody = 1f;
                    // Teeth: gaps in the lower jaw.
                    if (p.y < 24f && p.x > 28f && p.x < 68f && Mathf.Repeat(p.x - 28f, 10f) < 2.5f) lBody = 0f;
                    float lHoles = Mathf.Max(Disc(p, new Vector2(34, 54), 10f), Disc(p, new Vector2(62, 54), 10f), Disc(p, new Vector2(48, 36), 4f));
                    lTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(lBody - lHoles)));
                }
            lTexture.Apply();
            return Sprite.Create(lTexture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            Juice.OnScreenFlash -= HandleFlash;
            Core.Damageable.OnConfirmed -= HandleConfirmed;
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

            // The crosshair: always the same, it only opens a little with speed.
            WeaponHolder lWeapons = lAlive ? lMe.Weapons : null;
            ItemDefinition lHeld = lWeapons != null ? lWeapons.HeldItem : null;
            bool lScoped = lWeapons != null && lWeapons.Scoped;
            _scope.enabled = lScoped;
            float lSpread = lMe != null ? Mathf.Clamp01(lMe.Motor.PlanarSpeed / 7f) : 0f;
            UpdateCrosshair(dt, lAlive && !lScoped, lSpread);

            _prompt.text = !lAlive ? "" : lWeapons != null && lWeapons.TakedownReady ? "[" + GameInput.KeyName("Takedown") + "] TAKEDOWN" : PlayerInteractor.CurrentPrompt;

            // Health.
            if (lMe != null)
            {
                float lHp = lMe.Health.Health.Value;
                float lRatio = Mathf.Clamp01(lHp / Mathf.Max(1f, lMe.Health.MaxHealth));
                _health.text = Mathf.CeilToInt(lHp).ToString();
                _healthFill.rectTransform.sizeDelta = new Vector2(320 * lRatio, 4);
                // Health shows only when it changes, and stays while low.
                if (_lastHp >= 0f && Mathf.Abs(lHp - _lastHp) > 0.5f) _healthShowUntil = Time.time + 4f;
                _lastHp = lHp;
                bool lShowHealth = lAlive && (Time.time < _healthShowUntil || lRatio < 0.35f || lMe.Hidden.Value || (lMe.Crouched.Value && lMe.InGrass.Value));
                _healthGroup.alpha = Mathf.MoveTowards(_healthGroup.alpha, lShowHealth ? 1f : 0f, dt * (lShowHealth ? 6f : 1.5f));
                _healthFill.color = Color.Lerp(UIFactory.Danger, UIFactory.Paper, Mathf.Clamp01(lRatio * 2f - 0.3f));
                if (lRatio < 0.3f && lAlive && CameraRig.Instance != null) CameraRig.Instance.PulseHurt(0.15f + Mathf.Sin(Time.time * 6f) * 0.05f);
                bool lConcealed = lMe.Hidden.Value || (lMe.Crouched.Value && lMe.InGrass.Value);
                _stealth.text = "";

                _zone.text = "";
            }

            // Ammo and slots.
            if (lWeapons != null)
            {
                InventorySlot lSlot = lWeapons.SlotAt(lWeapons.Selected.Value);
                _weaponName.text = lHeld != null ? lHeld.displayName.ToUpperInvariant() : "FISTS";
                _ammo.text = lHeld == null ? "" : lHeld.UsesAmmo ? (lWeapons.Reloading.Value ? "<size=60%>RELOADING</size>" : lSlot.magazine + "<size=60%> / " + lSlot.reserve + "</size>") : "x" + lSlot.reserve;
                // Weapon and ammo show when they change (a shot, a reload, a switch) or while aiming, then fade.
                string lAmmoKey = _weaponName.text + _ammo.text;
                if (lAmmoKey != _lastAmmo || GameInput.Aim) _ammoShowUntil = Time.time + 3f;
                _lastAmmo = lAmmoKey;
                _ammoGroup.alpha = Mathf.MoveTowards(_ammoGroup.alpha, Time.time < _ammoShowUntil ? 1f : 0f, dt * (Time.time < _ammoShowUntil ? 6f : 1.2f));

                for (int i = 0; i < WeaponHolder.SLOT_COUNT; i++)
                {
                    InventorySlot s = lWeapons.SlotAt(i);
                    ItemDefinition lItem = GameDatabase.Item(s.item);
                    bool lCurrent = lWeapons.Selected.Value == i;
                    _stripNames[i].text = lItem != null ? lItem.displayName.ToUpperInvariant() : (i < WeaponHolder.WEAPON_SLOTS ? "FISTS" : "");
                    _stripNames[i].color = lCurrent ? UIFactory.Paper : lItem != null ? new Color(0.85f, 0.85f, 0.85f) : new Color(1, 1, 1, 0.3f);
                    _stripCounts[i].text = lItem == null ? "" : lItem.UsesAmmo ? s.magazine + " / " + s.reserve : lItem.slot == ItemSlot.Gadget ? "x" + s.reserve : "";
                    _stripBacks[i].color = lCurrent ? new Color(0.03f, 0.03f, 0.04f, 0.78f) : new Color(0.03f, 0.03f, 0.04f, 0.42f);
                    _stripBars[i].enabled = lCurrent;
                }
            }

            // Contract.
            if (lDirector != null)
            {
                TargetProfile lTarget = lDirector.Target;
                MapDefinition lMap = lDirector.Map;
                string lZone = lTarget != null && lMap != null && lTarget.zone < lMap.zoneNames.Length ? lMap.zoneNames[lTarget.zone] : "";
                _dossier.text = "";
                string lCarrier = "";
                if (lDirector.Proof.Value == ProofState.Carried)
                {
                    LobbyPlayer lCarrierPlayer = LobbyPlayer.ForClient(lDirector.ProofCarrier.Value);
                    lCarrier = lCarrierPlayer != null ? lCarrierPlayer.Name.ToUpperInvariant() : "?";
                }
                _status.text = lDirector.Proof.Value switch
                {
                    ProofState.TargetAlive => "",
                    ProofState.Dropped => "<color=#FFE066>PROOF ON THE GROUND</color>",
                    ProofState.Carried => lMe != null && lMe.CarriesProof.Value ? "<color=#7CF29A>YOU HAVE THE PROOF - EXTRACT!</color>" : "<color=#FFB13B>" + lCarrier + " HAS THE PROOF</color>",
                    _ => "EXTRACTED"
                };
                int lSeconds = Mathf.CeilToInt(lDirector.TimeLeft.Value);
                _clock.text = lSeconds <= 180 ? (lSeconds / 60).ToString("00") + ":" + (lSeconds % 60).ToString("00") : "";
                _clock.color = lSeconds < 60 ? UIFactory.Danger : UIFactory.Paper;

                bool lExtracting = lMe != null && lMe.CarriesProof.Value && lDirector.ExtractProgress.Value > 0f;
                _extractBar.gameObject.SetActive(lExtracting);
                if (lExtracting) _extractFill.rectTransform.sizeDelta = new Vector2(420 * Mathf.Clamp01(lDirector.ExtractProgress.Value), 26);
                if (!CasterView.Active) UpdateWorldMarkers(lDirector, lMe);
                else ClearWorldMarkers();
            }

            // The caster view (casters, and agents once eliminated) draws its own read-out.
            _spectating.text = EndScreen.IsShown || CasterView.Active ? "" : Spectator.Active ? "ELIMINATED" : "";

            // Voice.
            string lTalking = "";
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All) if (lPlayer.TalkingNow.Value) lTalking += "<sprite=0> " + lPlayer.Name + "\n";
            _voice.text = lTalking.Replace("<sprite=0>", ")))");

            // Flash.
            _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt * 2.5f);
            _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashAlpha);

            // Stacks.
            Restack(_feed, -40f, 36f, new Vector2(1, 1));
            UpdateDamageArcs(lMe);

            bool lBoard = GameInput.ScoreboardHeld || (lDirector != null && lDirector.Phase.Value == MatchPhase.Ended);
            _scoreboardPanel.gameObject.SetActive(GameInput.ScoreboardHeld && !CasterView.Active);
            if (lBoard) _scoreboard.text = Scoreboard();
        }

        private void UpdateCrosshair(float dt, bool pShown, float pSpread)
        {
            float lGap = 7f + pSpread * 5f;
            _crossDot.enabled = pShown;
            for (int i = 0; i < 4; i++)
            {
                float lAngle = i * Mathf.PI * 0.5f;
                _crossTicks[i].enabled = pShown;
                _crossTicks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(lAngle), Mathf.Cos(lAngle)) * (lGap + 4f);
            }

            // The marks burst out and fade; the skull pops, then fades.
            _hitTime = Mathf.Max(0f, _hitTime - dt);
            float lHit = _hitTime / Mathf.Max(0.01f, _hitLength);
            Color lColor = _hitHead ? new Color(1f, 0.16f, 0.14f) : Color.white;
            lColor.a = lHit;
            for (int i = 0; i < 4; i++)
            {
                float lAngle = (45f + 90f * i) * Mathf.Deg2Rad;
                _hitBars[i].enabled = _hitTime > 0f;
                _hitBars[i].color = lColor;
                _hitBars[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(lAngle), Mathf.Sin(lAngle)) * (18f + (1f - lHit) * 12f);
                _hitBars[i].rectTransform.sizeDelta = new Vector2(_hitHead ? 20f : 14f, _hitHead ? 4f : 3f);
            }
            _skullTime = Mathf.Max(0f, _skullTime - dt);
            _skull.enabled = _skullTime > 0f;
            if (_skullTime > 0f)
            {
                _skull.color = new Color(1f, 0.16f, 0.14f, Mathf.Clamp01(_skullTime * 4f));
                _skull.rectTransform.localScale = Vector3.one * (1f + Mathf.Clamp01(_skullTime - SKULL_HEAD + 0.15f) * 2.5f);
            }
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
            // The carrier is never tracked live: a yellow mark stays where they were last seen, until the next reveal.
            if (pDirector.Proof.Value == ProofState.Carried && (pMe == null || !pMe.CarriesProof.Value) && CarrierPing.HasValue)
                Marker(lUsed, lCamera, "carrier", CarrierPing.Value + Vector3.up * 2.2f, "<color=#FFD83B>PROOF</color>");
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

        private void ClearWorldMarkers()
        {
            foreach (TextMeshProUGUI lText in _worldMarkers.Values) Destroy(lText.gameObject);
            _worldMarkers.Clear();
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
            foreach (LobbyPlayer lPlayer in LobbyPlayer.Agents())
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
