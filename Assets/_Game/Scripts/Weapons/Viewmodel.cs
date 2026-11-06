using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The first-person arms and the held item, drawn by the overlay camera, and all of it code: two fat
    /// sleeves ending in mittens, in the agent's own colours. Each hand follows a pose in camera space
    /// (hip, aimed, running) plus what it is doing: punches, swings and throws are short curves, recoil
    /// and everything else springs. Nothing is rigged or authored, and little of the screen is covered:
    /// bare hands stay out of sight until they swing, and a pistol is held in one hand.
    /// </summary>
    public class Viewmodel : MonoBehaviour
    {
        public enum MeleeMove { Jab, Cross, Takedown }

        private enum ActionKind { None, Jab, Cross, Takedown, Swing, Throw }

        // Poses in camera space: x right, y up, z forward, in metres in front of the overlay camera.
        private static readonly Vector3 HIP = new Vector3(0.17f, -0.17f, 0.46f);
        private static readonly Vector3 HIP_TURN = new Vector3(1.5f, -3f, 0f);
        private static readonly Vector3 SPRINT = new Vector3(0.2f, -0.27f, 0.38f);
        private static readonly Vector3 SPRINT_TURN = new Vector3(24f, -30f, 12f);
        private static readonly Vector3 MELEE = new Vector3(0.2f, -0.17f, 0.42f);
        private static readonly Vector3 MELEE_TURN = new Vector3(-28f, -8f, -8f);
        /// <summary>Where the sight line sits when aiming: low and to the right, the crosshair stays clear above it.</summary>
        private static readonly Vector3 AIM_SIGHT = new Vector3(0.055f, -0.082f, 0.36f);
        /// <summary>The same for what has no sight: the hand itself.</summary>
        private static readonly Vector3 AIM_HAND = new Vector3(0.07f, -0.15f, 0.4f);
        private static readonly Vector3 REST_R = new Vector3(0.27f, -0.42f, 0.26f), REST_L = new Vector3(-0.27f, -0.42f, 0.26f);
        private static readonly Vector3 SHOULDER_R = new Vector3(0.34f, -0.5f, -0.2f), SHOULDER_L = new Vector3(-0.34f, -0.5f, -0.2f);
        /// <summary>Everything in the hand is drawn a little small: the view belongs to the world.</summary>
        private const float ITEM_SCALE = 0.78f;
        private const float SLEEVE_WIDTH = 0.085f;
        private const float KICK_STIFFNESS = 210f, KICK_DAMPING = 17f;
        private const float ADS_SPEED = 7f, SPRINT_SPEED = 5f;
        private const float NEAR_CLIP = 0.06f;
        /// <summary>How dark the arms and the held item get when the sun is blocked at the eye.</summary>
        public const float SHADE = 0.8f;
        /// <summary>Tools only: keep the arms drawn while scoped.</summary>
        public static bool ShowScoped;

        private WeaponHolder _holder;
        private Transform _handR, _handL, _sleeveR, _sleeveL, _socket;
        private readonly List<Renderer> _armRenderers = new List<Renderer>();
        private Transform _model, _sight, _foreGrip, _mag;
        private Renderer[] _modelRenderers = new Renderer[0];
        private ItemDefinition _item;
        private int _builtItem = -2;
        private bool _twoHands;
        private GameObject _flash;
        private float _flashUntil;

        private float _ads, _sprint, _bob, _switchT = 1f, _reloadT = 1f, _reloadLength = 1f;
        private Vector3 _kickPos, _kickPosSpeed, _kickRot, _kickRotSpeed;
        private float _pulse;
        private ActionKind _action;
        private float _actionT, _actionLength, _freezeUntil;
        private float _hiddenFrom = -1f, _hiddenUntil = -1f;
        private bool _magOut;

        private readonly List<(Material material, Color color)> _shaded = new List<(Material, Color)>();
        private readonly List<(Material material, Color color)> _shadedModel = new List<(Material, Color)>();
        private float _shade = 1f, _appliedShade = -1f;

        public Transform Muzzle { get; private set; }

        /// <summary>The muzzle as the world camera would see it there: tracers start from it.</summary>
        public Vector3 MuzzlePosition
        {
            get
            {
                if (Muzzle == null || CameraRig.Instance == null) return transform.position;
                Camera lView = CameraRig.Instance.ViewCamera, lWorld = CameraRig.Instance.WorldCamera;
                Vector3 lViewport = lView.WorldToViewportPoint(Muzzle.position);
                return lWorld.ViewportToWorldPoint(new Vector3(lViewport.x, lViewport.y, Mathf.Max(0.4f, lViewport.z)));
            }
        }

        public static Viewmodel Create(WeaponHolder pHolder)
        {
            CameraRig lRig = CameraRig.Ensure();
            // The arms must go through the same exposure and tonemapping as the world, or they burn white.
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(lRig.ViewCamera).renderPostProcessing = true;
            lRig.ViewCamera.nearClipPlane = NEAR_CLIP;
            lRig.ViewCamera.fieldOfView = CameraRig.VIEW_FOV;
            GameObject lRoot = new GameObject("Viewmodel");
            lRoot.transform.SetParent(lRig.ViewmodelRoot, false);
            Viewmodel lView = lRoot.AddComponent<Viewmodel>();
            lView._holder = pHolder;
            lView.BuildArms();
            GameLayers.SetRecursive(lRoot, GameLayers.LOCAL_VIEW);
            return lView;
        }

        private void BuildArms()
        {
            LobbyPlayer lPlayer = _holder.Avatar.Player;
            AgentLook lLook = lPlayer != null ? lPlayer.Look.Value : AgentLook.Default;
            Color lSleeve = AgentLook.TopColor(lLook.top), lSkin = AgentLook.SkinColor(lLook.skin);
            _handR = BuildHand("HandR", 1f, lSkin);
            _handL = BuildHand("HandL", -1f, lSkin);
            _sleeveR = BuildSleeve("SleeveR", lSleeve);
            _sleeveL = BuildSleeve("SleeveL", lSleeve);
            _socket = new GameObject("Socket").transform;
            _socket.SetParent(_handR, false);
            _socket.localPosition = new Vector3(0f, 0f, 0.005f);
            _socket.localScale = Vector3.one * ITEM_SCALE;
            PrepareRenderers(_armRenderers.ToArray(), _shaded);
        }

        private Transform BuildHand(string pName, float pSide, Color pSkin)
        {
            Transform lHand = new GameObject(pName).transform;
            lHand.SetParent(transform, false);
            Mesh lBall = LowPolyMesh.Cached("w.sphere", () => LowPolyMesh.Icosphere(1));
            GameObject lPalm = GameArt.Part(lHand, "Mitten", lBall, new Vector3(0f, -0.005f, -0.01f), new Vector3(0.075f, 0.082f, 0.1f), Color.white, Tinted(pSkin));
            GameObject lThumb = GameArt.Part(lHand, "Thumb", lBall, new Vector3(-pSide * 0.038f, 0.022f, 0.012f), new Vector3(0.032f, 0.03f, 0.06f), Color.white, Tinted(pSkin));
            lPalm.GetComponent<Renderer>().SetPropertyBlock(null);
            lThumb.GetComponent<Renderer>().SetPropertyBlock(null);
            _armRenderers.Add(lPalm.GetComponent<Renderer>());
            _armRenderers.Add(lThumb.GetComponent<Renderer>());
            return lHand;
        }

        private Transform BuildSleeve(string pName, Color pColor)
        {
            Mesh lTube = LowPolyMesh.Cached("w.sleeve", () => LowPolyMesh.Cylinder(7, 1.25f));
            GameObject lSleeve = GameArt.Part(transform, pName, lTube, Vector3.zero, Vector3.one, Color.white, Tinted(pColor));
            lSleeve.GetComponent<Renderer>().SetPropertyBlock(null);
            _armRenderers.Add(lSleeve.GetComponent<Renderer>());
            return lSleeve.transform;
        }

        private static Material Tinted(Color pColor)
        {
            Material lMaterial = new Material(GameArt.Toon);
            if (lMaterial.HasProperty(GameArt.BaseColor)) lMaterial.SetColor(GameArt.BaseColor, pColor);
            return lMaterial;
        }

        /// <summary>
        /// First-person renderers live inside the owner's own (shadow-only) body: they cast no shadow and
        /// take none. Their own instances of the materials so the rig can dim them out of the sun.
        /// </summary>
        private void PrepareRenderers(Renderer[] pRenderers, List<(Material material, Color color)> pList)
        {
            foreach (Renderer lRenderer in pRenderers)
            {
                lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lRenderer.receiveShadows = false;
                Material[] lMaterials = lRenderer.materials;
                foreach (Material lMaterial in lMaterials)
                {
                    if (lMaterial.HasProperty("_ReceiveShadows")) lMaterial.SetFloat("_ReceiveShadows", 0f);
                    lMaterial.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                    if (lMaterial.HasProperty("_BaseColor")) pList.Add((lMaterial, lMaterial.GetColor("_BaseColor")));
                }
                lRenderer.materials = lMaterials;
            }
            _appliedShade = -1f;
        }

        /// <summary>The arms take no world shadow: they dim themselves when the sun is hidden from the eye.</summary>
        private void UpdateShade(float pDt)
        {
            Light lSun = RenderSettings.sun;
            bool lLit = true;
            if (lSun != null && lSun.isActiveAndEnabled && lSun.type == LightType.Directional)
                lLit = !Physics.Raycast(transform.position, -lSun.transform.forward, 400f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            _shade = Mathf.MoveTowards(_shade, lLit ? 1f : SHADE, pDt * 2.5f);
            if (Mathf.Abs(_shade - _appliedShade) < 0.004f) return;
            _appliedShade = _shade;
            foreach (List<(Material material, Color color)> lList in new[] { _shaded, _shadedModel })
                foreach ((Material lMaterial, Color c) in lList)
                    if (lMaterial != null) lMaterial.SetColor("_BaseColor", new Color(c.r * _shade, c.g * _shade, c.b * _shade, c.a));
        }

        // ---------------------------------------------------------------- held item

        private void Rebuild(ItemDefinition pItem)
        {
            _builtItem = pItem != null ? pItem.Index : -1;
            _item = pItem;
            if (_model != null) Destroy(_model.gameObject);
            foreach ((Material lMaterial, Color _) in _shadedModel) if (lMaterial != null) Destroy(lMaterial);
            _shadedModel.Clear();
            _model = _sight = _foreGrip = _mag = null;
            _modelRenderers = new Renderer[0];
            Muzzle = null;
            _hiddenFrom = _hiddenUntil = -1f;
            _action = ActionKind.None;
            _reloadT = 1f;
            _twoHands = false;
            if (_flash != null) { Destroy(_flash); _flash = null; }
            if (pItem == null) return;

            GameObject lModel = WeaponModels.Build(pItem, _socket, GameLayers.LOCAL_VIEW);
            _model = lModel.transform;
            Muzzle = _model.Find("Muzzle");
            _sight = _model.Find("Sight");
            _foreGrip = _model.Find("ForeGrip");
            _mag = _model.Find("Mag");
            _twoHands = pItem.kind is not (ItemKind.Pistol or ItemKind.SilencedPistol or ItemKind.Revolver) && (pItem.IsGun || pItem.IsLauncher || pItem.kind == ItemKind.Flamethrower);
            _modelRenderers = lModel.GetComponentsInChildren<Renderer>(true);
            PrepareRenderers(_modelRenderers, _shadedModel);
            if (Muzzle != null) BuildFlash();
        }

        /// <summary>The muzzle's own flash in the overlay: a flat star, on for a blink.</summary>
        private void BuildFlash()
        {
            _flash = new GameObject("Flash");
            _flash.transform.SetParent(Muzzle, false);
            Mesh lCone = LowPolyMesh.Cached("w.flashcone", () => LowPolyMesh.Cone(5));
            for (int i = 0; i < 4; i++)
            {
                GameObject lRay = GameArt.Part(_flash.transform, "Ray", lCone, Vector3.zero, new Vector3(0.05f, 0.14f, 0.012f), new Color(1f, 0.86f, 0.42f), GameArt.Glow);
                lRay.transform.localRotation = Quaternion.Euler(i == 0 ? 90f : 0f, 0f, i * 90f);
                lRay.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            GameLayers.SetRecursive(_flash, GameLayers.LOCAL_VIEW);
            _flash.SetActive(false);
        }

        // ---------------------------------------------------------------- actions (called by WeaponHolder)

        /// <summary>A shot: the gun jumps back and up on a spring, swells for a beat, flashes and throws a case.</summary>
        public void Kick(float pAmount)
        {
            _kickPosSpeed += new Vector3(Random.Range(-0.05f, 0.05f), 0.28f, -1.5f) * pAmount;
            _kickRotSpeed += new Vector3(-90f, Random.Range(-16f, 16f), Random.Range(-28f, 28f)) * pAmount;
            _pulse = 1f;
            if (_flash != null)
            {
                _flash.SetActive(true);
                _flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
                _flash.transform.localScale = Vector3.one * Random.Range(0.8f, 1.3f) * Mathf.Clamp(pAmount, 0.8f, 2f);
                _flashUntil = Time.time + 0.045f;
            }
            if (_item != null && _item.IsGun && _handR != null) ViewDebris.Shell(_handR, _item.kind == ItemKind.Shotgun || _item.kind == ItemKind.SawedOff);
        }

        public void PlaySwitch() => _switchT = 0f;

        public static float DefaultStrike(MeleeMove pMove) => pMove == MeleeMove.Takedown ? 0.24f : pMove == MeleeMove.Cross ? 0.11f : 0.09f;

        /// <summary>
        /// A punch, a swing of the held blade or club, or the takedown; returns the time to its contact,
        /// when the caller resolves the hit.
        /// </summary>
        public float PlayMelee(MeleeMove pMove)
        {
            if (pMove == MeleeMove.Takedown)
            {
                if (_model != null) { _hiddenFrom = Time.time; _hiddenUntil = Time.time + 0.5f; }
                Play(ActionKind.Takedown, 0.52f);
            }
            else if (_item != null && _item.IsMelee) Play(ActionKind.Swing, 0.3f);
            else Play(pMove == MeleeMove.Jab ? ActionKind.Jab : ActionKind.Cross, pMove == MeleeMove.Jab ? 0.24f : 0.27f);
            return DefaultStrike(pMove);
        }

        /// <summary>Predicted hit-stop: the arms freeze for a beat on contact (the game clock never slows).</summary>
        public void HitStop(float pSeconds) => _freezeUntil = Mathf.Max(_freezeUntil, Time.unscaledTime + pSeconds);

        /// <summary>The throw of the held item; returns the time until it leaves the hand.</summary>
        public float PlayThrow()
        {
            const float RELEASE = 0.17f, LENGTH = 0.42f;
            Play(ActionKind.Throw, LENGTH);
            _hiddenFrom = Time.time + RELEASE;
            _hiddenUntil = Time.time + LENGTH;
            return RELEASE;
        }

        public void PlayReload(float pDuration)
        {
            _reloadT = 0f;
            _reloadLength = Mathf.Max(0.2f, pDuration);
            _magOut = false;
        }

        private void Play(ActionKind pKind, float pLength)
        {
            _action = pKind;
            _actionT = 0f;
            _actionLength = pLength;
        }

        // ---------------------------------------------------------------- frame

        private void Update()
        {
            if (_holder == null) { Destroy(gameObject); return; }
            ItemDefinition lItem = _holder.HeldItem;
            int lIndex = lItem != null ? lItem.Index : -1;
            if (lIndex != _builtItem) { Rebuild(lItem); _switchT = 0f; }
            if (_flash != null && _flash.activeSelf && Time.time >= _flashUntil) _flash.SetActive(false);
            UpdateShade(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (_holder == null || _handR == null) return;
            PlayerAvatar lAvatar = _holder.Avatar;
            float dt = Time.deltaTime;
            bool lShown = lAvatar.Health.IsAlive && lAvatar.Motor.Mode != MotorMode.Hidden && (!_holder.Scoped || ShowScoped)
                          && lAvatar.Motor.Mode != MotorMode.Climb && lAvatar.Motor.Mode != MotorMode.LedgeHang && lAvatar.Motor.Mode != MotorMode.Mantle;
            SetVisible(lShown);
            if (!lShown) return;

            if (Time.unscaledTime >= _freezeUntil && _action != ActionKind.None)
            {
                _actionT += dt / Mathf.Max(0.01f, _actionLength);
                if (_actionT >= 1f) _action = ActionKind.None;
            }

            bool lAiming = _holder.IsAiming;
            _ads = Mathf.MoveTowards(_ads, lAiming ? 1f : 0f, dt * ADS_SPEED);
            _sprint = Mathf.MoveTowards(_sprint, lAvatar.Motor.Sprinting && !lAiming && _action == ActionKind.None ? 1f : 0f, dt * SPRINT_SPEED);
            float lAds = Ease(_ads), lSprint = Ease(_sprint);
            _switchT = Mathf.MoveTowards(_switchT, 1f, dt * 4f);
            _reloadT = Mathf.MoveTowards(_reloadT, 1f, dt / _reloadLength);
            Spring(ref _kickPos, ref _kickPosSpeed, dt);
            Spring(ref _kickRot, ref _kickRotSpeed, dt);
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 9f);

            float lSpeed = lAvatar.Motor.Grounded ? lAvatar.Motor.PlanarSpeed : 0f;
            _bob += lSpeed * dt * 1.9f;
            float lBobScale = Mathf.Lerp(1f, 0.1f, lAds) * Mathf.Clamp01(lSpeed / 6f);
            Vector3 lBob = new Vector3(Mathf.Cos(_bob) * 0.01f, Mathf.Abs(Mathf.Sin(_bob)) * 0.013f, 0f) * lBobScale;

            bool lHoldsItem = _item != null;
            Vector3 lHip = _item != null && _item.IsMelee ? MELEE : HIP;
            Vector3 lHipTurn = _item != null && _item.IsMelee ? MELEE_TURN : HIP_TURN;
            Vector3 lPosR = Vector3.Lerp(lHip, SPRINT, lSprint);
            Quaternion lRotR = Quaternion.Slerp(Quaternion.Euler(lHipTurn), Quaternion.Euler(SPRINT_TURN), lSprint);
            if (lAds > 0.001f)
            {
                Vector3 lAim = AIM_HAND;
                if (_sight != null) lAim = AIM_SIGHT - _handR.InverseTransformPoint(_sight.position) * _handR.localScale.x;
                lPosR = Vector3.Lerp(lPosR, lAim, lAds);
                lRotR = Quaternion.Slerp(lRotR, Quaternion.identity, lAds);
            }
            if (!lHoldsItem) { lPosR = REST_R; lRotR = Quaternion.identity; }
            Vector3 lPosL = REST_L;
            Quaternion lRotL = Quaternion.identity;

            // Switching: up from below. Reloading: down, a roll, the magazine out and in.
            float lDrop = 1f - Ease(_switchT);
            lPosR += Vector3.down * lDrop * 0.2f;
            lRotR *= Quaternion.Euler(lDrop * 28f, 0f, 0f);
            float lReload = _reloadT < 1f ? Mathf.Sin(_reloadT * Mathf.PI) : 0f;
            lPosR += new Vector3(-0.03f, -0.055f, -0.03f) * lReload;
            lRotR *= Quaternion.Euler(lReload * 22f, lReload * -10f, lReload * -32f);
            UpdateMagazine();

            // The support hand: on the fore-end of a long gun, under it during a reload.
            bool lLeftOn = false;
            if (_twoHands)
            {
                lLeftOn = true;
                Vector3 lGrip = _foreGrip != null ? _handR.InverseTransformPoint(_foreGrip.position) * 1f : new Vector3(-0.012f, -0.03f, 0.22f);
                lPosL = lPosR + lRotR * lGrip + Vector3.down * 0.012f;
                lRotL = lRotR * Quaternion.Euler(-10f, 8f, 0f);
                if (lReload > 0f) lPosL += new Vector3(0.02f, -0.09f, -0.05f) * lReload;
            }

            ApplyAction(ref lPosR, ref lRotR, ref lPosL, ref lRotL, ref lLeftOn);

            lPosR += lBob + _kickPos;
            lRotR = Quaternion.Euler(_kickRot) * lRotR;
            if (_twoHands) { lPosL += lBob + _kickPos; lRotL = Quaternion.Euler(_kickRot) * lRotL; }
            else lPosL += lBob;
            if (lAvatar.Motor.Sliding) lRotR *= Quaternion.Euler(0f, 0f, 8f);

            _handR.SetLocalPositionAndRotation(lPosR, lRotR);
            _handL.SetLocalPositionAndRotation(lPosL, lRotL);
            float lSwell = 1f + _pulse * _pulse * 0.1f;
            _socket.localScale = Vector3.one * ITEM_SCALE * lSwell;

            // Bare hands and an unused left hand stay out of sight.
            bool lRightOn = lHoldsItem || _action != ActionKind.None;
            SetHand(_handR, _sleeveR, lRightOn);
            SetHand(_handL, _sleeveL, lLeftOn);
            Stretch(_sleeveR, lPosR, SHOULDER_R);
            Stretch(_sleeveL, lPosL, SHOULDER_L);

            if (_model != null)
            {
                bool lHide = _hiddenFrom >= 0f && Time.time >= _hiddenFrom && Time.time < _hiddenUntil;
                if (_model.gameObject.activeSelf == lHide) _model.gameObject.SetActive(!lHide);
                if (_hiddenUntil > 0f && Time.time >= _hiddenUntil) { _hiddenFrom = _hiddenUntil = -1f; _switchT = 0f; }
            }
        }

        private void ApplyAction(ref Vector3 pPosR, ref Quaternion pRotR, ref Vector3 pPosL, ref Quaternion pRotL, ref bool pLeftOn)
        {
            if (_action == ActionKind.None) return;
            float t = Mathf.Clamp01(_actionT);
            // Out fast, back slower, with a little overshoot at the far end.
            float lOut = t < 0.38f ? Ease(t / 0.38f) * 1.08f : Mathf.Lerp(1.08f, 0f, Ease((t - 0.38f) / 0.62f));
            switch (_action)
            {
                case ActionKind.Jab:
                    // Holding something, the left hand punches past it; bare-handed the same.
                    pLeftOn = true;
                    pPosL = Vector3.LerpUnclamped(REST_L, new Vector3(-0.03f, -0.07f, 0.62f), lOut);
                    pRotL = Quaternion.Euler(lOut * -6f, lOut * 6f, lOut * -18f);
                    break;
                case ActionKind.Cross:
                    if (_item != null) goto case ActionKind.Jab;
                    pPosR = Vector3.LerpUnclamped(REST_R, new Vector3(0.02f, -0.06f, 0.64f), lOut);
                    pRotR = Quaternion.Euler(lOut * -6f, lOut * -6f, lOut * 18f);
                    break;
                case ActionKind.Swing:
                {
                    float lArc = t < 0.42f ? Ease(t / 0.42f) : 1f;
                    float lBack = t < 0.42f ? 0f : Ease((t - 0.42f) / 0.58f);
                    Vector3 lEnd = new Vector3(-0.08f, -0.14f, 0.58f);
                    Vector3 lWind = pPosR;
                    pPosR = Vector3.Lerp(Vector3.Lerp(lWind + new Vector3(0.07f, 0.1f, -0.06f), lEnd, lArc), lWind, lBack);
                    pRotR = Quaternion.Slerp(Quaternion.Euler(-55f + lArc * 125f, 40f - lArc * 80f, 0f), pRotR, lBack);
                    break;
                }
                case ActionKind.Takedown:
                    pLeftOn = true;
                    pPosR = Vector3.LerpUnclamped(new Vector3(0.3f, 0.02f, 0.3f), new Vector3(0.05f, -0.08f, 0.56f), lOut);
                    pPosL = Vector3.LerpUnclamped(new Vector3(-0.3f, -0.12f, 0.3f), new Vector3(-0.08f, -0.12f, 0.56f), lOut);
                    pRotR = Quaternion.Euler(lOut * 25f, 0f, lOut * 20f);
                    pRotL = Quaternion.Euler(lOut * 15f, 0f, lOut * -20f);
                    break;
                case ActionKind.Throw:
                {
                    Vector3 lBackHand = new Vector3(0.27f, 0.02f, 0.2f), lForward = new Vector3(0.04f, -0.02f, 0.62f);
                    float lWind = Ease(Mathf.Clamp01(t / 0.25f)), lRelease = Ease(Mathf.Clamp01((t - 0.25f) / 0.2f)), lEnd = Ease(Mathf.Clamp01((t - 0.55f) / 0.45f));
                    Vector3 lPath = Vector3.Lerp(Vector3.Lerp(pPosR, lBackHand, lWind), lForward, lRelease);
                    pPosR = Vector3.Lerp(lPath, pPosR, lEnd);
                    pRotR = Quaternion.Euler(lRelease * 50f - lWind * 40f, 0f, 0f);
                    break;
                }
            }
        }

        private void UpdateMagazine()
        {
            if (_mag == null) return;
            bool lOut = _reloadT > 0.22f && _reloadT < 0.72f;
            if (_mag.gameObject.activeSelf == lOut) _mag.gameObject.SetActive(!lOut);
            if (lOut && !_magOut)
            {
                _magOut = true;
                ViewDebris.Magazine(_mag, transform);
            }
        }

        private void SetVisible(bool pOn)
        {
            if (_handR.gameObject.activeSelf == pOn) return;
            _handR.gameObject.SetActive(pOn);
            _handL.gameObject.SetActive(pOn);
            _sleeveR.gameObject.SetActive(pOn);
            _sleeveL.gameObject.SetActive(pOn);
        }

        private void SetHand(Transform pHand, Transform pSleeve, bool pOn)
        {
            foreach (Renderer lRenderer in pHand.GetComponentsInChildren<Renderer>(true))
            {
                if (_modelRenderers.Length > 0 && System.Array.IndexOf(_modelRenderers, lRenderer) >= 0) continue;
                if (_flash != null && lRenderer.transform.IsChildOf(_flash.transform)) continue;
                lRenderer.enabled = pOn;
            }
            pSleeve.GetComponent<Renderer>().enabled = pOn;
        }

        /// <summary>A sleeve from the wrist back to an unseen shoulder.</summary>
        private static void Stretch(Transform pSleeve, Vector3 pHand, Vector3 pShoulder)
        {
            Vector3 lAlong = pShoulder - pHand;
            float lLength = lAlong.magnitude;
            pSleeve.localPosition = pHand + lAlong * 0.5f;
            pSleeve.localRotation = Quaternion.FromToRotation(Vector3.up, lAlong / Mathf.Max(0.001f, lLength));
            pSleeve.localScale = new Vector3(SLEEVE_WIDTH, lLength, SLEEVE_WIDTH);
        }

        private static void Spring(ref Vector3 pValue, ref Vector3 pSpeed, float dt)
        {
            float lStep = Mathf.Min(dt, 0.033f);
            pSpeed += (-pValue * KICK_STIFFNESS - pSpeed * KICK_DAMPING) * lStep;
            pValue += pSpeed * lStep;
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }

    /// <summary>What flies out of a gun in the first person: a case, a magazine. A small fall in the overlay.</summary>
    public class ViewDebris : MonoBehaviour
    {
        private Vector3 _velocity, _spin;
        private float _life;

        public static void Shell(Transform pHand, bool pBig)
        {
            Mesh lTube = LowPolyMesh.Cached("w.cyl8", () => LowPolyMesh.Cylinder(8));
            GameObject lCase = GameArt.Part(pHand.parent, "Case", lTube, pHand.localPosition + pHand.localRotation * new Vector3(0.025f, 0.05f, 0.08f), pBig ? new Vector3(0.02f, 0.05f, 0.02f) : new Vector3(0.012f, 0.028f, 0.012f), pBig ? new Color(0.8f, 0.2f, 0.16f) : new Color(0.95f, 0.72f, 0.28f));
            lCase.layer = GameLayers.LOCAL_VIEW;
            lCase.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ViewDebris lFall = lCase.AddComponent<ViewDebris>();
            lFall._velocity = new Vector3(Random.Range(0.7f, 1.1f), Random.Range(0.9f, 1.3f), Random.Range(-0.2f, 0.15f));
            lFall._spin = Random.insideUnitSphere * 900f;
            lFall._life = 0.5f;
        }

        public static void Magazine(Transform pMag, Transform pParent)
        {
            GameObject lCopy = Instantiate(pMag.gameObject, pParent, true);
            lCopy.SetActive(true);
            ViewDebris lFall = lCopy.AddComponent<ViewDebris>();
            lFall._velocity = new Vector3(0.1f, -0.6f, 0f);
            lFall._spin = new Vector3(Random.Range(-180f, 180f), 0f, Random.Range(-180f, 180f));
            lFall._life = 0.5f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _velocity += Vector3.down * 7f * dt;
            transform.localPosition += _velocity * dt;
            transform.Rotate(_spin * dt, Space.Self);
            _life -= dt;
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
