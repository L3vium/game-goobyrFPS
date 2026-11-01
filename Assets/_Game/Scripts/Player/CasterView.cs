using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// The tournament view: up to four live feeds on screen at once, each its own camera.
    /// <list type="bullet">
    /// <item>a feed follows an agent (behind the shoulder, or through their eyes), flies freely, or shows the tactical map;</item>
    /// <item>layouts: one feed, two side by side, four in a grid, or a main feed with three small ones;</item>
    /// <item>focus brings the selected feed (or it and the main one) full screen and back;</item>
    /// <item>the auto-director keeps the agent worth watching (proof, fights, close to the target) on the main feed.</item>
    /// </list>
    /// Casters (who joined to watch) get it all; an eliminated agent gets it restricted to whom they
    /// may watch (<see cref="MatchRulesLogic.CanSpectate"/>), without the free camera and map in Teams.
    /// The overlays are drawn by <see cref="CasterHud"/>.
    /// </summary>
    public class CasterView : MonoBehaviour
    {
        public const ulong NOBODY = ulong.MaxValue;

        /// <summary>One feed: what it shows and its camera.</summary>
        public class Feed
        {
            public FeedKind kind = FeedKind.Chase;
            public ulong agent = NOBODY;
            public Camera camera;
            public float orbitYaw, orbitPitch, distance = DEFAULT_DISTANCE;
            public float freeYaw, freePitch, freeSpeed = FREE_SPEED;
            public float lastYaw;
            public float deadFor;
            public Vector3 lastSeen;
            public bool hasPose;
        }

        public static CasterView Instance { get; private set; }
        public static bool Active => Instance != null;

        /// <summary>An eliminated agent watching (the rules limit whom), not a caster.</summary>
        public bool Restricted { get; private set; }
        public CasterLayout Layout { get; private set; } = CasterLayout.MainPlus3;
        public int Selected { get; private set; }
        public bool AutoDirector { get; private set; }
        /// <summary>Overlays hidden: a clean feed for streaming.</summary>
        public bool Clean { get; private set; }
        public bool Focused => _focus.Count > 0;
        /// <summary>The agents in seat order: their numbers on the cards and the 1-8 keys.</summary>
        public List<LobbyPlayer> Agents { get; } = new List<LobbyPlayer>();
        public Feed[] Feeds => _feeds;

        /// <summary>The layout on screen: the chosen one, or the focus (one or two feeds).</summary>
        public CasterLayout ShownLayout => _focus.Count == 1 ? CasterLayout.Single : _focus.Count == 2 ? CasterLayout.Duo : Layout;
        public int ShownCount => CasterLayoutLogic.FeedCount(ShownLayout);
        /// <summary>The feed drawn at an on-screen position.</summary>
        public int ShownFeed(int pPosition) => _focus.Count > 0 ? _focus[pPosition] : pPosition;

        /// <summary>The free camera and the map would let an eliminated agent scout for their team.</summary>
        public bool CanFly => !Restricted || LobbyState.Instance == null || LobbyState.Instance.Mode.Value != MatchMode.Teams;

        private const float DEFAULT_DISTANCE = 3.6f, FREE_SPEED = 9f, DEAD_HOLD = 3f, DOUBLE_CLICK = 0.3f, HURT_MEMORY = 4f, AUTO_PAUSE = 15f;

        private readonly Feed[] _feeds = new Feed[CasterLayoutLogic.MAX_FEEDS];
        private readonly List<int> _focus = new List<int>();
        private readonly HashSet<ulong> _combat = new HashSet<ulong>(), _engaged = new HashSet<ulong>();
        private readonly Dictionary<ulong, float> _lastHealth = new Dictionary<ulong, float>(), _hurtAt = new Dictionary<ulong, float>();
        private readonly List<Renderer> _hiddenHead = new List<Renderer>();
        private CameraRig _rig;
        private CasterHud _hud;
        private float _mainHeld, _autoTimer, _autoPausedUntil, _lastClick = -1f;
        private int _lastClickFeed = -1;
        private bool _dragging;

        // ---------------------------------------------------------------- life

        public static void Begin(bool pRestricted)
        {
            if (Instance != null) return;
            CameraRig lRig = CameraRig.Ensure();
            CasterView lView = new GameObject("CasterView").AddComponent<CasterView>();
            lView.Restricted = pRestricted;
            lView._rig = lRig;
            lView.Init();
        }

        public static void Stop()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Awake() => Instance = this;

        private void Init()
        {
            // Clears the bands around the feeds (and under the tactical map), which no feed draws.
            Camera lBackdrop = new GameObject("Backdrop").AddComponent<Camera>();
            lBackdrop.transform.SetParent(transform, false);
            lBackdrop.cullingMask = 0;
            lBackdrop.clearFlags = CameraClearFlags.SolidColor;
            lBackdrop.backgroundColor = new Color(0.035f, 0.04f, 0.045f);
            lBackdrop.depth = 9;
            lBackdrop.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            for (int i = 0; i < _feeds.Length; i++)
            {
                GameObject lObject = new GameObject("Feed" + i);
                lObject.transform.SetParent(transform, false);
                Camera lCamera = lObject.AddComponent<Camera>();
                lCamera.nearClipPlane = 0.05f;
                lCamera.farClipPlane = 900f;
                lCamera.fieldOfView = 70f;
                lCamera.depth = 10 + i;
                lCamera.cullingMask = ~(1 << GameLayers.LOCAL_VIEW);
                UniversalAdditionalCameraData lData = lCamera.GetUniversalAdditionalCameraData();
                lData.renderPostProcessing = true;
                lData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                _feeds[i] = new Feed { camera = lCamera };
                // Every feed starts where the eye was (the death cam, or the rig's spot for a caster).
                if (_rig != null)
                {
                    Transform lEye = _rig.WorldCamera.transform;
                    lObject.transform.SetPositionAndRotation(lEye.position, lEye.rotation);
                    _feeds[i].lastSeen = lEye.position + Vector3.ProjectOnPlane(lEye.forward, Vector3.up).normalized * 4.5f;
                    _feeds[i].lastYaw = lEye.eulerAngles.y;
                }
            }
            // The rig stops drawing; its camera stays as the ear (the audio listener follows the selected feed).
            if (_rig != null)
            {
                _rig.WorldCamera.enabled = false;
                _rig.SetDesaturate(0f);
            }

            RefreshAgents();
            List<LobbyPlayer> lAlive = Agents.FindAll(pAgent => CanWatch(pAgent) && IsAlive(pAgent.OwnerClientId));
            for (int i = 0; i < _feeds.Length; i++)
            {
                if (i < lAlive.Count) _feeds[i].agent = lAlive[i].OwnerClientId;
                else if (CanFly) _feeds[i].kind = i == _feeds.Length - 1 ? FeedKind.Tactical : FeedKind.Free;
            }
            Layout = Restricted || lAlive.Count < 2 ? CasterLayout.Single : CasterLayout.MainPlus3;
            AutoDirector = !Restricted;
            Selected = 0;
            // Free cameras start high over the agents, each from another side.
            Vector3 lCentre = Vector3.zero;
            foreach (LobbyPlayer lAgent in lAlive) lCentre += PlayerAvatar.ForClient(lAgent.OwnerClientId).transform.position / lAlive.Count;
            for (int i = 0; i < _feeds.Length; i++)
            {
                if (_feeds[i].kind != FeedKind.Free) continue;
                Vector3 lFrom = lCentre + Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0f, 38f, -48f);
                _feeds[i].camera.transform.SetPositionAndRotation(lFrom, Quaternion.LookRotation(lCentre - lFrom));
                StartFree(_feeds[i]);
            }

            GameInput.LockCursor(false);
            RenderPipelineManager.beginCameraRendering += BeforeFeed;
            RenderPipelineManager.endCameraRendering += AfterFeed;
            _hud = gameObject.AddComponent<CasterHud>();
            _hud.Build(this);
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeFeed;
            RenderPipelineManager.endCameraRendering -= AfterFeed;
            RestoreHead();
            if (Instance == this) Instance = null;
            if (_rig != null) _rig.WorldCamera.enabled = true;
            if (_dragging) GameInput.LockCursor(false);
        }

        // ---------------------------------------------------------------- queries

        public static bool IsAlive(ulong pClient)
        {
            PlayerAvatar lAvatar = PlayerAvatar.ForClient(pClient);
            return lAvatar != null && lAvatar.Health.IsAlive;
        }

        public bool InCombat(ulong pClient) => _combat.Contains(pClient);
        public bool IsEngaged(ulong pClient) => _engaged.Contains(pClient);
        public bool HurtRecently(ulong pClient) => _hurtAt.TryGetValue(pClient, out float lAt) && Time.time - lAt < HURT_MEMORY;

        /// <summary>The agent's number on the cards (1-based), 0 when not an agent.</summary>
        public int NumberOf(ulong pClient)
        {
            for (int i = 0; i < Agents.Count; i++) if (Agents[i].OwnerClientId == pClient) return i + 1;
            return 0;
        }

        public bool CanWatch(LobbyPlayer pAgent)
        {
            if (pAgent == null || !pAgent.IsAgent) return false;
            if (!Restricted) return true;
            int lMyTeam = LobbyPlayer.Local != null ? LobbyPlayer.Local.Team.Value : MatchRulesLogic.NO_TEAM;
            MatchMode lMode = LobbyState.Instance != null ? LobbyState.Instance.Mode.Value : MatchMode.FreeForAll;
            return MatchRulesLogic.CanSpectate(lMode, lMyTeam, pAgent.Team.Value);
        }

        /// <summary>The on-screen position showing a feed, -1 when it is off screen.</summary>
        public int PositionOf(int pFeed)
        {
            for (int i = 0; i < ShownCount; i++) if (ShownFeed(i) == pFeed) return i;
            return -1;
        }

        private void RefreshAgents()
        {
            Agents.Clear();
            foreach (LobbyPlayer lPlayer in LobbyPlayer.All) if (lPlayer.IsAgent) Agents.Add(lPlayer);
        }

        // ---------------------------------------------------------------- commands

        public void SetLayout(CasterLayout pLayout)
        {
            Layout = pLayout;
            _focus.Clear();
            if (PositionOf(Selected) < 0) Selected = 0;
            Audio.Sfx.Play2D("click", 0.35f);
        }

        /// <summary>The selected feed full screen; with <paramref name="pWithMain"/>, it and the main feed side by side. Again: back.</summary>
        public void ToggleFocus(bool pWithMain)
        {
            if (Focused) _focus.Clear();
            else if (pWithMain && CasterLayoutLogic.FeedCount(Layout) > 1)
            {
                _focus.Add(0);
                _focus.Add(Selected != 0 ? Selected : 1);
            }
            else _focus.Add(Selected);
            if (PositionOf(Selected) < 0) Selected = ShownFeed(0);
            Audio.Sfx.Play2D("click", 0.35f);
        }

        public void SelectFeed(int pFeed)
        {
            if (pFeed < 0 || pFeed >= _feeds.Length || PositionOf(pFeed) < 0) return;
            if (pFeed != Selected) Audio.Sfx.Play2D("hover", 0.25f);
            Selected = pFeed;
        }

        private void StepSelection(int pStep)
        {
            int lPosition = PositionOf(Selected);
            int lCount = ShownCount;
            SelectFeed(ShownFeed(((lPosition + pStep) % lCount + lCount) % lCount));
        }

        /// <summary>Puts the agent numbered <paramref name="pIndex"/> (0-based) on the selected feed.</summary>
        public void AssignIndex(int pIndex)
        {
            if (pIndex >= 0 && pIndex < Agents.Count) Assign(Selected, Agents[pIndex]);
        }

        public void Assign(int pFeed, LobbyPlayer pAgent)
        {
            if (!CanWatch(pAgent)) return;
            Feed lFeed = _feeds[pFeed];
            if (lFeed.agent != pAgent.OwnerClientId || lFeed.kind == FeedKind.Free || lFeed.kind == FeedKind.Tactical)
            {
                lFeed.agent = pAgent.OwnerClientId;
                if (lFeed.kind == FeedKind.Free || lFeed.kind == FeedKind.Tactical) lFeed.kind = FeedKind.Chase;
                lFeed.orbitYaw = lFeed.orbitPitch = 0f;
                lFeed.deadFor = 0f;
                lFeed.hasPose = false;
            }
            if (pFeed == 0) ManualMain();
            Audio.Sfx.Play2D("click", 0.35f);
        }

        private void Cycle(int pStep)
        {
            Feed lFeed = _feeds[Selected];
            List<LobbyPlayer> lPool = Agents.FindAll(pAgent => CanWatch(pAgent) && IsAlive(pAgent.OwnerClientId));
            if (lPool.Count == 0) return;
            int lCurrent = lPool.FindIndex(pAgent => pAgent.OwnerClientId == lFeed.agent);
            int lNext = lCurrent < 0 ? 0 : ((lCurrent + pStep) % lPool.Count + lPool.Count) % lPool.Count;
            Assign(Selected, lPool[lNext]);
        }

        private void ToggleEyes()
        {
            Feed lFeed = _feeds[Selected];
            if (lFeed.kind == FeedKind.Chase) lFeed.kind = FeedKind.Eyes;
            else if (lFeed.kind == FeedKind.Eyes) lFeed.kind = FeedKind.Chase;
            else if (lFeed.agent != NOBODY) lFeed.kind = FeedKind.Chase;
            lFeed.hasPose = false;
            Audio.Sfx.Play2D("click", 0.35f);
        }

        private void ToggleFree()
        {
            if (!CanFly) return;
            Feed lFeed = _feeds[Selected];
            if (lFeed.kind == FeedKind.Free) { if (lFeed.agent != NOBODY) { lFeed.kind = FeedKind.Chase; lFeed.hasPose = false; } }
            else StartFree(lFeed);
            if (Selected == 0) ManualMain();
            Audio.Sfx.Play2D("click", 0.35f);
        }

        private void StartFree(Feed pFeed)
        {
            Transform lFrom = pFeed.camera.transform;
            pFeed.kind = FeedKind.Free;
            Vector3 lAngles = lFrom.eulerAngles;
            pFeed.freeYaw = lAngles.y;
            pFeed.freePitch = Mathf.DeltaAngle(0f, lAngles.x);
            pFeed.hasPose = true;
        }

        private void ToggleTactical()
        {
            if (!CanFly) return;
            Feed lFeed = _feeds[Selected];
            lFeed.kind = lFeed.kind == FeedKind.Tactical ? (lFeed.agent != NOBODY ? FeedKind.Chase : FeedKind.Free) : FeedKind.Tactical;
            lFeed.hasPose = lFeed.kind == FeedKind.Free;
            if (Selected == 0) ManualMain();
            Audio.Sfx.Play2D("click", 0.35f);
        }

        /// <summary>The selected feed goes on the main screen (the main one takes its place); a focus keeps showing it.</summary>
        private void Promote()
        {
            if (Selected == 0) return;
            (_feeds[0], _feeds[Selected]) = (_feeds[Selected], _feeds[0]);
            for (int i = 0; i < _focus.Count; i++) _focus[i] = _focus[i] == 0 ? Selected : _focus[i] == Selected ? 0 : _focus[i];
            Selected = 0;
            ManualMain();
            Audio.Sfx.Play2D("click", 0.35f);
        }

        public void ToggleAutoDirector()
        {
            AutoDirector = !AutoDirector;
            _autoPausedUntil = 0f;
            Audio.Sfx.Play2D("click", 0.35f);
        }

        /// <summary>The caster took the main feed in hand: the auto-director waits a while.</summary>
        private void ManualMain()
        {
            _autoPausedUntil = Time.time + AUTO_PAUSE;
            _mainHeld = 0f;
        }

        // ---------------------------------------------------------------- loop

        private void Update()
        {
            RefreshAgents();
            ReadThreats();
            if (!PauseMenu.IsOpen && !EndScreen.IsShown) ReadInput();
            else if (_dragging) EndDrag();
            UpdateAutoDirector();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _feeds.Length; i++)
            {
                Feed lFeed = _feeds[i];
                int lPosition = PositionOf(i);
                bool lOn = lPosition >= 0 && lFeed.kind != FeedKind.Tactical;
                lFeed.camera.enabled = lOn;
                if (lPosition >= 0) lFeed.camera.rect = CasterLayoutLogic.Viewport(ShownLayout, lPosition);
                UpdateFeed(lFeed, i == Selected, dt);
            }

            // The ear follows the selected feed (the main one while the selected shows the map).
            Feed lEar = _feeds[Selected].kind != FeedKind.Tactical ? _feeds[Selected] : _feeds[0];
            if (_rig != null) _rig.WorldCamera.transform.SetPositionAndRotation(lEar.camera.transform.position, lEar.camera.transform.rotation);
            _hud.Refresh();
        }

        private void ReadThreats()
        {
            _combat.Clear();
            _engaged.Clear();
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || !lNpc.Health.IsAlive || lNpc.Suspect.Value == Npc.NO_SUSPECT) continue;
                if (lNpc.State.Value == NpcState.Combat) _combat.Add(lNpc.Suspect.Value);
                else if (lNpc.State.Value == NpcState.Engaged || lNpc.State.Value == NpcState.Suspicious) _engaged.Add(lNpc.Suspect.Value);
            }
            foreach (PlayerAvatar lAvatar in PlayerAvatar.All)
            {
                if (lAvatar == null) continue;
                float lHealth = lAvatar.Health.Health.Value;
                if (_lastHealth.TryGetValue(lAvatar.OwnerClientId, out float lWas) && lHealth < lWas - 0.5f) _hurtAt[lAvatar.OwnerClientId] = Time.time;
                _lastHealth[lAvatar.OwnerClientId] = lHealth;
            }
        }

        private void ReadInput()
        {
            Keyboard lKeys = Keyboard.current;
            Mouse lMouse = Mouse.current;
            Gamepad lPad = Gamepad.current;
            bool lShift = lKeys != null && lKeys.shiftKey.isPressed;

            if (lKeys != null)
            {
                if (lKeys.f1Key.wasPressedThisFrame) SetLayout(CasterLayout.Single);
                if (lKeys.f2Key.wasPressedThisFrame) SetLayout(CasterLayout.Duo);
                if (lKeys.f3Key.wasPressedThisFrame) SetLayout(CasterLayout.Quad);
                if (lKeys.f4Key.wasPressedThisFrame) SetLayout(CasterLayout.MainPlus3);
                if (lKeys.fKey.wasPressedThisFrame) ToggleFocus(lShift);
                if (lKeys.tabKey.wasPressedThisFrame) StepSelection(lShift ? -1 : 1);
                for (int i = 0; i < 8; i++)
                    if (lKeys[Key.Digit1 + i].wasPressedThisFrame || lKeys[Key.Numpad1 + i].wasPressedThisFrame) AssignIndex(i);
                if (lKeys.leftArrowKey.wasPressedThisFrame) Cycle(-1);
                if (lKeys.rightArrowKey.wasPressedThisFrame) Cycle(1);
                if (lKeys.upArrowKey.wasPressedThisFrame) StepSelection(-1);
                if (lKeys.downArrowKey.wasPressedThisFrame) StepSelection(1);
                if (lKeys.vKey.wasPressedThisFrame) ToggleEyes();
                if (lKeys.cKey.wasPressedThisFrame) ToggleFree();
                if (lKeys.tKey.wasPressedThisFrame) ToggleTactical();
                if (lKeys.enterKey.wasPressedThisFrame || lKeys.numpadEnterKey.wasPressedThisFrame) Promote();
                if (lKeys.pKey.wasPressedThisFrame) ToggleAutoDirector();
                if (lKeys.hKey.wasPressedThisFrame) Clean = !Clean;
                if (lKeys.rKey.wasPressedThisFrame) ResetCamera(_feeds[Selected]);
            }

            if (lPad != null)
            {
                if (lPad.dpad.up.wasPressedThisFrame) SetLayout((CasterLayout)(((int)Layout + 1) % 4));
                if (lPad.dpad.down.wasPressedThisFrame) ToggleFocus(false);
                if (lPad.dpad.left.wasPressedThisFrame) StepSelection(-1);
                if (lPad.dpad.right.wasPressedThisFrame) StepSelection(1);
                if (lPad.leftShoulder.wasPressedThisFrame) Cycle(-1);
                if (lPad.rightShoulder.wasPressedThisFrame) Cycle(1);
                if (lPad.buttonSouth.wasPressedThisFrame) Promote();
                if (lPad.buttonWest.wasPressedThisFrame) ToggleEyes();
                if (lPad.buttonNorth.wasPressedThisFrame) ToggleFree();
                if (lPad.buttonEast.wasPressedThisFrame) ToggleTactical();
                if (lPad.rightStickButton.wasPressedThisFrame) ToggleAutoDirector();
            }

            if (lMouse == null) return;
            bool lOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 lPoint = lMouse.position.ReadValue();
            Vector2 lNormal = new Vector2(lPoint.x / Mathf.Max(1, Screen.width), lPoint.y / Mathf.Max(1, Screen.height));
            int lUnder = CasterLayoutLogic.FeedAt(ShownLayout, lNormal);
            int lFeedUnder = lUnder >= 0 ? ShownFeed(lUnder) : -1;

            if (lMouse.leftButton.wasPressedThisFrame && !lOverUi && lFeedUnder >= 0)
            {
                bool lDouble = lFeedUnder == _lastClickFeed && Time.unscaledTime - _lastClick < DOUBLE_CLICK;
                SelectFeed(lFeedUnder);
                if (lDouble) ToggleFocus(false);
                _lastClick = lDouble ? -1f : Time.unscaledTime;
                _lastClickFeed = lFeedUnder;
            }
            if (lMouse.rightButton.wasPressedThisFrame && !lOverUi && lFeedUnder >= 0)
            {
                SelectFeed(lFeedUnder);
                _dragging = true;
                GameInput.LockCursor(true);
            }
            if (_dragging && !lMouse.rightButton.isPressed) EndDrag();

            float lScroll = lMouse.scroll.ReadValue().y;
            if (Mathf.Abs(lScroll) > 0.01f && !lOverUi)
            {
                Feed lFeed = _feeds[Selected];
                float lStep = Mathf.Sign(lScroll);
                if (lFeed.kind == FeedKind.Free) lFeed.freeSpeed = Mathf.Clamp(lFeed.freeSpeed * (lStep > 0 ? 1.25f : 0.8f), 2f, 60f);
                else lFeed.distance = Mathf.Clamp(lFeed.distance - lStep * 0.6f, 1.6f, 14f);
            }
        }

        private void EndDrag()
        {
            _dragging = false;
            GameInput.LockCursor(false);
        }

        private static void ResetCamera(Feed pFeed)
        {
            pFeed.orbitYaw = pFeed.orbitPitch = 0f;
            pFeed.distance = DEFAULT_DISTANCE;
            pFeed.freeSpeed = FREE_SPEED;
        }

        /// <summary>Mouse (right button held) or right stick: orbit the agent, or look around with the free camera.</summary>
        private Vector2 LookInput()
        {
            Vector2 lLook = Vector2.zero;
            if (_dragging && Mouse.current != null) lLook += Mouse.current.delta.ReadValue() * 0.12f * GameInput.MouseSensitivity;
            if (Gamepad.current != null) lLook += Gamepad.current.rightStick.ReadValue() * 140f * Time.unscaledDeltaTime;
            return lLook;
        }

        private void UpdateAutoDirector()
        {
            _mainHeld += Time.deltaTime;
            _autoTimer -= Time.deltaTime;
            if (!AutoDirector || Time.time < _autoPausedUntil || _autoTimer > 0f) return;
            _autoTimer = 0.5f;
            Feed lMain = _feeds[0];
            if (lMain.kind != FeedKind.Chase && lMain.kind != FeedKind.Eyes) return;

            List<LobbyPlayer> lPool = Agents.FindAll(CanWatch);
            float[] lScores = new float[lPool.Count];
            int lCurrent = -1;
            for (int i = 0; i < lPool.Count; i++)
            {
                lScores[i] = CasterLayoutLogic.Interest(InterestOf(lPool[i].OwnerClientId));
                if (lPool[i].OwnerClientId == lMain.agent) lCurrent = i;
            }
            int lPick = CasterLayoutLogic.PickMain(lScores, lCurrent, _mainHeld);
            if (lPick < 0 || lPick == lCurrent) return;

            ulong lAgent = lPool[lPick].OwnerClientId;
            // Already on a small feed: the two feeds swap places, cameras and all (no cut).
            for (int i = 1; i < _feeds.Length; i++)
            {
                if (PositionOf(i) < 0 || _feeds[i].agent != lAgent || (_feeds[i].kind != FeedKind.Chase && _feeds[i].kind != FeedKind.Eyes)) continue;
                (_feeds[0], _feeds[i]) = (_feeds[i], _feeds[0]);
                _mainHeld = 0f;
                return;
            }
            lMain.agent = lAgent;
            lMain.orbitYaw = lMain.orbitPitch = 0f;
            lMain.deadFor = 0f;
            lMain.hasPose = false;
            _mainHeld = 0f;
        }

        private FeedInterest InterestOf(ulong pClient)
        {
            PlayerAvatar lAvatar = PlayerAvatar.ForClient(pClient);
            FeedInterest lInterest = new FeedInterest
            {
                alive = lAvatar != null && lAvatar.Health.IsAlive,
                combat = InCombat(pClient),
                engaged = IsEngaged(pClient),
                hurtRecently = HurtRecently(pClient),
                carriesProof = lAvatar != null && lAvatar.CarriesProof.Value,
                objectiveDistance = 9999f
            };
            if (lAvatar == null) return lInterest;
            MatchDirector lDirector = MatchDirector.Instance;
            Vector3 lObjective = Vector3.zero;
            bool lHas = false;
            if (lDirector != null && lDirector.Proof.Value != ProofState.TargetAlive) { lObjective = lDirector.ProofPosition.Value; lHas = true; }
            else
                foreach (Npc lNpc in Npc.All)
                    if (lNpc != null && lNpc.IsTarget && lNpc.Health.IsAlive) { lObjective = lNpc.transform.position; lHas = true; break; }
            if (lHas) lInterest.objectiveDistance = Vector3.Distance(lAvatar.transform.position, lObjective);
            return lInterest;
        }

        // ---------------------------------------------------------------- cameras

        private void UpdateFeed(Feed pFeed, bool pControlled, float dt)
        {
            if (pFeed.kind == FeedKind.Tactical) return;
            Transform lCamera = pFeed.camera.transform;
            if (pFeed.kind == FeedKind.Free)
            {
                UpdateFree(pFeed, pControlled, dt);
                return;
            }

            PlayerAvatar lAvatar = PlayerAvatar.ForClient(pFeed.agent);
            bool lAlive = lAvatar != null && lAvatar.Health.IsAlive;
            if (lAvatar != null)
            {
                pFeed.lastSeen = lAvatar.transform.position;
                pFeed.lastYaw = lAvatar.transform.eulerAngles.y;
            }
            pFeed.deadFor = lAlive ? 0f : pFeed.deadFor + dt;
            // A watched agent who fell: a few seconds on the scene, then the next one nobody watches.
            if (!lAlive && (pFeed.deadFor > DEAD_HOLD || pFeed.agent == NOBODY) && PickReplacement(pFeed)) return;

            if (pControlled)
            {
                Vector2 lLook = LookInput();
                pFeed.orbitYaw += lLook.x;
                pFeed.orbitPitch = Mathf.Clamp(pFeed.orbitPitch - lLook.y, -40f, 60f);
            }

            if (pFeed.kind == FeedKind.Eyes && lAlive)
            {
                pFeed.camera.fieldOfView = Mathf.Lerp(pFeed.camera.fieldOfView, 80f, 1f - Mathf.Exp(-8f * dt));
                Vector3 lEye = lAvatar.Eye;
                Quaternion lAim = Quaternion.Euler(Mathf.Clamp(lAvatar.Pitch.Value, -85f, 85f), lAvatar.transform.eulerAngles.y, 0f);
                if (!pFeed.hasPose) lCamera.SetPositionAndRotation(lEye, lAim);
                else lCamera.SetPositionAndRotation(Vector3.Lerp(lCamera.position, lEye, 1f - Mathf.Exp(-30f * dt)), Quaternion.Slerp(lCamera.rotation, lAim, 1f - Mathf.Exp(-20f * dt)));
                pFeed.hasPose = true;
                return;
            }

            if (!lAlive) pFeed.orbitYaw += dt * 12f;
            pFeed.camera.fieldOfView = Mathf.Lerp(pFeed.camera.fieldOfView, 70f, 1f - Mathf.Exp(-8f * dt));
            float lDistance = lAlive ? pFeed.distance : Mathf.Max(pFeed.distance, 6f);
            Vector3 lLookAt = pFeed.lastSeen + Vector3.up * (lAlive ? 1.6f : 0.8f);
            float lPitch = Mathf.Clamp((lAlive ? lAvatar.Pitch.Value * 0.5f : 0f) + 12f + pFeed.orbitPitch, -30f, 80f);
            Quaternion lOrbit = Quaternion.Euler(lPitch, pFeed.lastYaw + pFeed.orbitYaw, 0f);
            Vector3 lFrom = lLookAt + lOrbit * new Vector3(0.6f * Mathf.Min(1f, DEFAULT_DISTANCE / lDistance), 0.4f, -lDistance);
            if (Physics.Linecast(lLookAt, lFrom, out RaycastHit lHit, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                lFrom = lHit.point + (lLookAt - lFrom).normalized * 0.2f;
            if (!pFeed.hasPose) lCamera.SetPositionAndRotation(lFrom, lOrbit);
            else lCamera.SetPositionAndRotation(Vector3.Lerp(lCamera.position, lFrom, 1f - Mathf.Exp(-12f * dt)), Quaternion.Slerp(lCamera.rotation, lOrbit, 1f - Mathf.Exp(-14f * dt)));
            pFeed.hasPose = true;
        }

        /// <summary>The next living agent no feed on screen shows (or any living one). False when nobody is left.</summary>
        private bool PickReplacement(Feed pFeed)
        {
            LobbyPlayer lFallback = null;
            foreach (LobbyPlayer lAgent in Agents)
            {
                if (!CanWatch(lAgent) || !IsAlive(lAgent.OwnerClientId)) continue;
                bool lShown = false;
                for (int i = 0; i < ShownCount; i++)
                {
                    Feed lOther = _feeds[ShownFeed(i)];
                    if (lOther != pFeed && lOther.agent == lAgent.OwnerClientId && lOther.kind != FeedKind.Free && lOther.kind != FeedKind.Tactical) lShown = true;
                }
                if (!lShown)
                {
                    lFallback = lAgent;
                    break;
                }
                if (lFallback == null) lFallback = lAgent;
            }
            if (lFallback == null) return false;
            pFeed.agent = lFallback.OwnerClientId;
            pFeed.orbitYaw = pFeed.orbitPitch = 0f;
            pFeed.deadFor = 0f;
            pFeed.hasPose = false;
            return true;
        }

        private void UpdateFree(Feed pFeed, bool pControlled, float dt)
        {
            Transform lCamera = pFeed.camera.transform;
            pFeed.camera.fieldOfView = Mathf.Lerp(pFeed.camera.fieldOfView, 70f, 1f - Mathf.Exp(-8f * dt));
            if (pControlled)
            {
                Vector2 lLook = LookInput();
                pFeed.freeYaw += lLook.x;
                pFeed.freePitch = Mathf.Clamp(pFeed.freePitch - lLook.y, -89f, 89f);

                Keyboard lKeys = Keyboard.current;
                Gamepad lPad = Gamepad.current;
                Vector2 lMove = GameInput.Move;
                float lRise = 0f;
                if (lKeys != null) lRise += (lKeys.spaceKey.isPressed || lKeys.eKey.isPressed ? 1f : 0f) - (lKeys.leftCtrlKey.isPressed || lKeys.qKey.isPressed ? 1f : 0f);
                if (lPad != null) lRise += lPad.rightTrigger.ReadValue() - lPad.leftTrigger.ReadValue();
                float lSpeed = pFeed.freeSpeed * (GameInput.Sprint ? 3f : 1f);
                Quaternion lYaw = Quaternion.Euler(0f, pFeed.freeYaw, 0f);
                Quaternion lView = Quaternion.Euler(pFeed.freePitch, pFeed.freeYaw, 0f);
                Vector3 lVelocity = (lView * Vector3.forward * lMove.y + lYaw * Vector3.right * lMove.x + Vector3.up * Mathf.Clamp(lRise, -1f, 1f)) * lSpeed;
                lCamera.position += lVelocity * dt;
            }
            lCamera.rotation = Quaternion.Slerp(lCamera.rotation, Quaternion.Euler(pFeed.freePitch, pFeed.freeYaw, 0f), 1f - Mathf.Exp(-20f * dt));
        }

        // ---------------------------------------------------------------- through the eyes

        /// <summary>A feed through an agent's eyes does not draw that agent's head.</summary>
        private void BeforeFeed(ScriptableRenderContext pContext, Camera pCamera)
        {
            RestoreHead();
            foreach (Feed lFeed in _feeds)
            {
                if (lFeed.camera != pCamera || lFeed.kind != FeedKind.Eyes) continue;
                PlayerAvatar lAvatar = PlayerAvatar.ForClient(lFeed.agent);
                if (lAvatar == null || lAvatar.Body == null || lAvatar.Body.Head == null) return;
                foreach (Renderer lRenderer in lAvatar.Body.Head.GetComponentsInChildren<Renderer>())
                    if (!lRenderer.forceRenderingOff)
                    {
                        lRenderer.forceRenderingOff = true;
                        _hiddenHead.Add(lRenderer);
                    }
                return;
            }
        }

        private void AfterFeed(ScriptableRenderContext pContext, Camera pCamera) => RestoreHead();

        private void RestoreHead()
        {
            foreach (Renderer lRenderer in _hiddenHead) if (lRenderer != null) lRenderer.forceRenderingOff = false;
            _hiddenHead.Clear();
        }
    }
}
