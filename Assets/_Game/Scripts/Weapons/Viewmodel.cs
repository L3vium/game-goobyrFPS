using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The first-person arms and the held item, drawn by the overlay camera.
    /// <para>
    /// Every pose is authored (<see cref="ViewmodelArms"/>, <c>Tools/Blender/fp</c>): for each family of
    /// held item the arms hold it at the hip, aimed down the sights and sprinting, with the hands on
    /// the real grips; the item is parented at identity to the socket bone, so it sits exactly where
    /// it was authored. A generic Animator blends the three poses (direct blend weights from code) and
    /// plays the authored actions on top (jab, cross, takedown, throws; reloads where authored). No IK.
    /// </para>
    /// <para>
    /// Code adds the feel: sway from the mouse and the movement, bob, recoil pivoting about the sight,
    /// the drop of a weapon switch, the procedural reloads; and a last correction slides the rig so the
    /// Sight stays exactly on the screen centre while aiming.
    /// </para>
    /// </summary>
    public class Viewmodel : MonoBehaviour
    {
        public enum MeleeMove { Jab, Cross, Takedown }

        private WeaponHolder _holder;
        private ViewmodelArms _set;
        private Transform _rig;
        private Transform _arms;
        private Animator _animator;
        private Transform _socket;
        private Transform _prop;
        private Transform _model;
        private Transform _sight;
        private Transform _mag;
        private GameObject _propMag;
        private float _magOutAt = -1f, _magGrabAt = -1f, _magInAt = -1f;
        private bool _magDropped;
        private Renderer[] _armRenderers = new Renderer[0];
        private bool _handsShown = true;
        /// <summary>How much faster than authored the punches play, and the takedown.</summary>
        public const float MELEE_SPEED = 2.5f, TAKEDOWN_SPEED = 1.7f;
        /// <summary>How far under its authored pose a held item sits when not aiming: it stays out of the way.</summary>
        private static readonly Vector3 LOWERED = new Vector3(0.012f, -0.04f, -0.015f);
        /// <summary>Where the sight line sits under the view when aiming (the crosshair stays clear above it).</summary>
        private static readonly Vector3 ADS_HOLD = new Vector3(0.05f, -0.075f, 0.01f);

        private int _builtItem = -2;
        private ItemDefinition _item;
        private string _family = "";
        private ViewmodelArms.Family _familyData;

        private Vector3 _sway, _swayVel;
        private Vector3 _kickPos, _kickRot;
        private float _bob, _ads, _sprint;
        private float _switchT = 1f, _reloadT = 1f, _reloadDuration = 1f;
        private string _action = "";
        private int _actionLayer;
        private float _actionEnd, _freezeUntil;
        private float _hiddenUntil = -1f;

        /// <summary>The overlay camera's near plane: the shoulders and upper sleeves behind the eye are cut.</summary>
        public const float NEAR_CLIP = 0.06f;
        /// <summary>Blend times (s): into and out of an action, pose weights.</summary>
        public const float ACTION_IN = 0.05f, ACTION_OUT = 0.14f, ADS_SPEED = 6.5f, SPRINT_SPEED = 5f;
        /// <summary>Tools only (first-person shots): keep the arms drawn while scoped.</summary>
        public static bool ShowScoped;

        public Transform Muzzle { get; private set; }
        public Vector3 MuzzlePosition
        {
            get
            {
                if (Muzzle == null || CameraRig.Instance == null) return transform.position;
                // The viewmodel camera has its own FOV: project the muzzle into the world camera's frustum.
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
            lView._rig = new GameObject("Rig").transform;
            lView._rig.SetParent(lRoot.transform, false);
            lView.BuildArms();
            GameLayers.SetRecursive(lRoot, GameLayers.LOCAL_VIEW);
            return lView;
        }

        private void BuildArms()
        {
            _set = ViewmodelArms.Instance;
            if (_set == null || _set.model == null) { Debug.LogWarning("[Viewmodel] no first-person arms (Goobyr/Build/First Person Arms)"); return; }
            GameObject lArms = Instantiate(_set.model, _rig, false);
            lArms.name = "Arms";
            _arms = lArms.transform;
            _animator = lArms.GetComponent<Animator>();
            if (_animator == null) _animator = lArms.AddComponent<Animator>();
            _animator.runtimeAnimatorController = _set.controller;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.updateMode = AnimatorUpdateMode.Normal;
            _socket = FindDeep(_arms, ViewmodelArms.SOCKET);
            _prop = FindDeep(_arms, ViewmodelArms.PROP);
            _armRenderers = lArms.GetComponentsInChildren<Renderer>();
            foreach (Renderer lRenderer in _armRenderers)
            {
                if (lRenderer is SkinnedMeshRenderer lSkin) { lSkin.updateWhenOffscreen = true; lSkin.skinnedMotionVectors = false; }
            }
            PrepareRenderers(_armRenderers, _shaded);
        }

        /// <summary>
        /// First-person renderers live inside the owner's own (shadow-only) body: they cast no shadow and
        /// take none, or the body's shadow would blacken an arm. Their own instances of the materials so
        /// the rig can dim them when the eye is out of the sun (<see cref="UpdateShade"/>).
        /// </summary>
        private void PrepareRenderers(Renderer[] pRenderers, System.Collections.Generic.List<(Material material, Color color)> pList)
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

        private readonly System.Collections.Generic.List<(Material material, Color color)> _shaded = new System.Collections.Generic.List<(Material, Color)>();
        private readonly System.Collections.Generic.List<(Material material, Color color)> _shadedModel = new System.Collections.Generic.List<(Material, Color)>();
        private float _shade = 1f, _appliedShade = -1f;
        /// <summary>How dark the arms and the held item get when the sun is blocked at the eye.</summary>
        public const float SHADE = 0.8f;

        /// <summary>The arms take no world shadow: they dim themselves when the sun is hidden from the eye.</summary>
        private void UpdateShade(float pDt)
        {
            Light lSun = RenderSettings.sun;
            bool lLit = true;
            if (lSun != null && lSun.isActiveAndEnabled && lSun.type == LightType.Directional)
            {
                Vector3 lEye = transform.position;
                lLit = !Physics.Raycast(lEye, -lSun.transform.forward, 400f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            }
            _shade = Mathf.MoveTowards(_shade, lLit ? 1f : SHADE, pDt * 2.5f);
            if (Mathf.Abs(_shade - _appliedShade) < 0.004f) return;
            _appliedShade = _shade;
            foreach (System.Collections.Generic.List<(Material material, Color color)> lList in new[] { _shaded, _shadedModel })
                foreach ((Material lMaterial, Color c) in lList)
                    if (lMaterial != null) lMaterial.SetColor("_BaseColor", new Color(c.r * _shade, c.g * _shade, c.b * _shade, c.a));
        }

        private static Transform FindDeep(Transform pRoot, string pName)
        {
            if (pRoot.name == pName) return pRoot;
            foreach (Transform lChild in pRoot)
            {
                Transform lFound = FindDeep(lChild, pName);
                if (lFound != null) return lFound;
            }
            return null;
        }

        // ---------------------------------------------------------------- held item

        private void Rebuild(ItemDefinition pItem)
        {
            _builtItem = pItem != null ? pItem.Index : -1;
            _item = pItem;
            if (_model != null) Destroy(_model.gameObject);
            if (_propMag != null) Destroy(_propMag);
            foreach ((Material lMaterial, Color _) in _shadedModel) if (lMaterial != null) Destroy(lMaterial);
            _shadedModel.Clear();
            _model = null;
            _mag = null;
            _propMag = null;
            _magOutAt = _magGrabAt = _magInAt = -1f;
            _sight = null;
            Muzzle = null;
            _hiddenUntil = -1f;
            _releaseAt = -1f;
            StopAction(0.05f);

            _family = ViewmodelArms.FamilyOf(pItem);
            if (_set != null && _set.TryFamily(_family, out _familyData))
            {
                if (_animator != null) _animator.runtimeAnimatorController = _familyData.controller != null ? (RuntimeAnimatorController)_familyData.controller : _set.controller;
            }
            else _familyData = default;

            if (pItem != null && _socket != null)
            {
                GameObject lModel = WeaponModels.Build(pItem, _socket, GameLayers.LOCAL_VIEW);
                _model = lModel.transform;
                HoldAdjust(pItem, _model);
                Muzzle = _model.Find("Muzzle");
                _sight = _model.Find("Sight");
                _mag = _model.Find("Mag");
                // The magazine the support hand carries in a reload: a copy of the gun's, on prop_l.
                if (_mag != null && _prop != null && _familyData.authoredReload)
                {
                    _propMag = Instantiate(_mag.gameObject, _prop, false);
                    _propMag.name = "MagInHand";
                    _propMag.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    _propMag.SetActive(false);
                }
                PrepareRenderers(lModel.GetComponentsInChildren<Renderer>(true), _shadedModel);
            }
        }

        /// <summary>
        /// Authored reload: the gun's magazine leaves at magOut (dropping out of view if no hand holds it),
        /// the one in the support hand shows from magGrab, and it is the gun's again from magIn.
        /// </summary>
        private void UpdateMagazine()
        {
            if (_mag == null || _magInAt < 0f) return;
            float lNow = Time.time;
            bool lOut = lNow >= _magOutAt && lNow < _magInAt;
            bool lInHand = lNow >= _magGrabAt && lNow < _magInAt;
            if (lOut && !_magDropped)
            {
                _magDropped = true;
                // Released with nothing holding it (a pistol): it falls away.
                if (_magGrabAt > _magOutAt + 0.05f) MagazineDrop.Spawn(_mag, transform);
            }
            if (_mag.gameObject.activeSelf == lOut) _mag.gameObject.SetActive(!lOut);
            if (_propMag != null && _propMag.activeSelf != lInHand) _propMag.SetActive(lInHand);
            if (lNow >= _magInAt) { _magOutAt = _magGrabAt = _magInAt = -1f; _magDropped = false; }
        }

        /// <summary>
        /// Small items share a hand pose per family, but not a shape: each one is turned and shifted in
        /// the socket so the hand holds it by the right part (a card by its end, a vial upright, a bottle
        /// by the neck). Guns have their Grip on the socket at identity.
        /// </summary>
        private static void HoldAdjust(ItemDefinition pItem, Transform pModel)
        {
            Vector3 lOffset;
            Vector3 lTurn;
            switch (pItem.kind)
            {
                case ItemKind.Keycard: case ItemKind.StaffPass: lOffset = new Vector3(0f, 0f, 0.03f); lTurn = new Vector3(90f, 0f, 0f); break;
                case ItemKind.Invitation: case ItemKind.CodeNote: lOffset = new Vector3(0f, 0f, 0.05f); lTurn = new Vector3(90f, 0f, 0f); break;
                case ItemKind.Poison: case ItemKind.EmeticPoison: lOffset = new Vector3(0f, 0f, 0.01f); lTurn = new Vector3(90f, 0f, 0f); break;
                case ItemKind.Crowbar: lOffset = new Vector3(0f, 0.12f, 0f); lTurn = new Vector3(-90f, 0f, 0f); break;
                case ItemKind.Bottle: lOffset = Vector3.zero; lTurn = new Vector3(-90f, 0f, 0f); break;
                case ItemKind.Brick: case ItemKind.C4: lOffset = new Vector3(0f, 0f, 0.04f); lTurn = Vector3.zero; break;
                default: return;
            }
            pModel.localRotation = Quaternion.Euler(lTurn) * pModel.localRotation;
            pModel.localPosition = Quaternion.Euler(lTurn) * pModel.localPosition + lOffset;
        }

        // ---------------------------------------------------------------- actions (called by WeaponHolder)

        /// <summary>Recoil: a kick back and up, rotating about the sight (or the hand).</summary>
        public void Kick(float pAmount)
        {
            _kickPos += new Vector3(0f, 0.004f, -0.035f) * pAmount;
            _kickRot += new Vector3(-4.5f, Random.Range(-1f, 1f), Random.Range(-2f, 2f)) * pAmount;
        }

        public void PlaySwitch() => _switchT = 0f;

        /// <summary>
        /// Plays a punch or the takedown and returns how long until its contact (the authored strike
        /// time), when the caller resolves the hit. Bare hands: jab and cross on the whole body; holding
        /// something: a quick left jab while the right hand keeps the item.
        /// </summary>
        public float PlayMelee(MeleeMove pMove)
        {
            string lClip = pMove == MeleeMove.Jab ? ViewmodelArms.S_JAB : pMove == MeleeMove.Cross ? ViewmodelArms.S_CROSS : ViewmodelArms.S_TAKEDOWN;
            float lStrike = _set != null ? _set.Event(lClip, "strike", DefaultStrike(pMove)) : DefaultStrike(pMove);
            if (pMove == MeleeMove.Takedown)
            {
                // Both hands grab: the held item is put away for the takedown and raised again after.
                float lLength = (_set != null ? _set.Length(ViewmodelArms.S_TAKEDOWN, 1.1f) : 1.1f) / TAKEDOWN_SPEED;
                if (_model != null) _hiddenUntil = Time.time + lLength - 0.1f;
                PlayAction(ViewmodelArms.S_TAKEDOWN, ViewmodelArms.LAYER_ACTION, TAKEDOWN_SPEED, lLength);
                return lStrike / TAKEDOWN_SPEED;
            }
            // A blade or a club: the right hand's cross carries it. Another held item: a quick left jab.
            if (_item != null && _item.IsMelee) lClip = ViewmodelArms.S_CROSS;
            bool lLeft = _item != null && !_item.IsMelee;
            float lClipLength = (_set != null ? _set.Length(lLeft ? ViewmodelArms.S_JAB : lClip, 0.5f) : 0.5f) / MELEE_SPEED;
            PlayAction(lLeft ? ViewmodelArms.S_JAB : lClip, lLeft ? ViewmodelArms.LAYER_LEFT : ViewmodelArms.LAYER_ACTION, MELEE_SPEED, lClipLength);
            return lStrike / MELEE_SPEED;
        }

        public static float DefaultStrike(MeleeMove pMove) => pMove == MeleeMove.Takedown ? 0.3f : pMove == MeleeMove.Cross ? 0.18f : 0.16f;

        /// <summary>Predicted hit-stop: the arms freeze for a beat on contact (the game clock never slows).</summary>
        public void HitStop(float pSeconds) => _freezeUntil = Mathf.Max(_freezeUntil, Time.unscaledTime + pSeconds);

        /// <summary>The throw of the held item; returns the time until it leaves the hand.</summary>
        public float PlayThrow()
        {
            string lClip = _family + "_" + ViewmodelArms.S_THROW;
            float lRelease = _set != null ? _set.Event(lClip, "release", 0.27f) : 0.27f;
            float lLength = _familyData.throwLength > 0f ? _familyData.throwLength : 0.66f;
            if (_familyData.throwLength > 0f) PlayAction(ViewmodelArms.S_THROW, ViewmodelArms.LAYER_ACTION, 1f, lLength);
            // The item leaves the hand at the release; the next one (if any) comes up with the switch drop.
            _hiddenUntil = Time.time + lLength;
            _releaseAt = Time.time + lRelease;
            return lRelease;
        }

        private float _releaseAt = -1f;

        public void PlayReload(float pDuration)
        {
            _reloadT = 0f;
            _reloadDuration = Mathf.Max(0.2f, pDuration);
            if (_familyData.authoredReload && _familyData.reloadLength > 0f)
            {
                _reloadT = 1f;
                float lSpeed = _familyData.reloadLength / _reloadDuration;
                PlayAction(ViewmodelArms.S_RELOAD, ViewmodelArms.LAYER_ACTION, lSpeed, _reloadDuration);
                string lClip = _family + "_" + ViewmodelArms.S_RELOAD;
                float lNow = Time.time;
                _magOutAt = lNow + _set.Event(lClip, "magOut", 0.3f) / lSpeed;
                _magGrabAt = lNow + _set.Event(lClip, "magGrab", 0.6f) / lSpeed;
                _magInAt = lNow + _set.Event(lClip, "magIn", 1.0f) / lSpeed;
                _magDropped = false;
            }
        }

        private void PlayAction(string pState, int pLayer, float pSpeed, float pDuration)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;
            if (_action != "" && _actionLayer != pLayer) _animator.CrossFadeInFixedTime(ViewmodelArms.S_EMPTY, ACTION_IN, _actionLayer);
            _animator.SetFloat(ViewmodelArms.P_SPEED, pSpeed);
            _animator.CrossFadeInFixedTime(pState, ACTION_IN, pLayer, 0f);
            _action = pState;
            _actionLayer = pLayer;
            _actionEnd = Time.time + pDuration;
        }

        private void StopAction(float pFade)
        {
            if (_action == "" || _animator == null || !_animator.isActiveAndEnabled) { _action = ""; return; }
            _animator.CrossFadeInFixedTime(ViewmodelArms.S_EMPTY, pFade, _actionLayer);
            _action = "";
        }

        // ---------------------------------------------------------------- frame

        private void Update()
        {
            if (_holder == null) { Destroy(gameObject); return; }
            PlayerAvatar lAvatar = _holder.Avatar;
            ItemDefinition lItem = _holder.HeldItem;
            int lIndex = lItem != null ? lItem.Index : -1;
            if (lIndex != _builtItem) { Rebuild(lItem); _switchT = 0f; }

            bool lAlive = lAvatar.Health.IsAlive && lAvatar.Motor.Mode != MotorMode.Hidden;
            bool lShown = lAlive && (!_holder.Scoped || ShowScoped) && lAvatar.Motor.Mode != MotorMode.Climb && lAvatar.Motor.Mode != MotorMode.LedgeHang && lAvatar.Motor.Mode != MotorMode.Mantle;
            _rig.gameObject.SetActive(lShown);
            if (!lShown || _animator == null) return;

            // Bare hands stay out of sight: only a swing or a raised guard brings them up.
            bool lHands = _item != null || _action != "" || lAvatar.Blocking.Value;
            if (lHands != _handsShown)
            {
                _handsShown = lHands;
                foreach (Renderer lRenderer in _armRenderers) if (lRenderer != null) lRenderer.enabled = lHands;
            }

            float dt = Time.deltaTime;
            bool lAiming = _holder.IsAiming;
            _ads = Mathf.MoveTowards(_ads, lAiming ? 1f : 0f, dt * ADS_SPEED);
            _sprint = Mathf.MoveTowards(_sprint, lAvatar.Motor.Sprinting && !lAiming && _action == "" ? 1f : 0f, dt * SPRINT_SPEED);
            float lAds = Ease(_ads), lSprint = Ease(_sprint);
            _animator.SetFloat(ViewmodelArms.P_ADS, lAds);
            _animator.SetFloat(ViewmodelArms.P_SPRINT, (1f - lAds) * lSprint);
            _animator.SetFloat(ViewmodelArms.P_HIP, (1f - lAds) * (1f - lSprint));

            // Back to the poses once an action is done; hit-stop freezes the arms only.
            if (_action != "" && Time.time >= _actionEnd - ACTION_OUT) StopAction(ACTION_OUT);
            _animator.speed = Time.unscaledTime < _freezeUntil ? 0f : 1f;

            UpdateMagazine();
            UpdateShade(dt);

            // A thrown or put-away item: hidden from its release until the action is over, then raised.
            if (_model != null)
            {
                bool lHide = (_releaseAt > 0f && Time.time >= _releaseAt && Time.time < _hiddenUntil) || (_releaseAt <= 0f && Time.time < _hiddenUntil);
                if (_model.gameObject.activeSelf == lHide) _model.gameObject.SetActive(!lHide);
                if (_hiddenUntil > 0f && Time.time >= _hiddenUntil) { _hiddenUntil = -1f; _releaseAt = -1f; PlaySwitch(); }
            }
        }

        private void LateUpdate()
        {
            if (_holder == null || _arms == null || !_rig.gameObject.activeInHierarchy) return;
            PlayerAvatar lAvatar = _holder.Avatar;
            float dt = Time.deltaTime;

            // No sway: the arms are locked to the camera, they never drag behind a turn.
            _sway = Vector3.zero;
            _kickPos = Vector3.Lerp(_kickPos, Vector3.zero, 1f - Mathf.Exp(-14f * dt));
            _kickRot = Vector3.Lerp(_kickRot, Vector3.zero, 1f - Mathf.Exp(-12f * dt));

            float lAds = Ease(_ads);
            float lSpeed = lAvatar.Motor.Grounded ? lAvatar.Motor.PlanarSpeed : 0f;
            _bob += lSpeed * dt * 1.9f;
            float lBobScale = Mathf.Lerp(1f, 0.12f, lAds) * Mathf.Clamp01(lSpeed / 6f);
            Vector3 lBob = new Vector3(Mathf.Cos(_bob) * 0.009f, Mathf.Abs(Mathf.Sin(_bob)) * 0.011f, 0f) * lBobScale;
            float lFree = 1f - lAds * 0.8f;

            Vector3 lPos = lBob + LOWERED * (1f - lAds);
            Quaternion lRot = Quaternion.Euler(new Vector3(-_sway.y * 180f, _sway.x * 180f, _sway.x * 220f) * lFree);

            // Switch: the new item comes up from below; a procedural reload dips and rolls it.
            _switchT = Mathf.MoveTowards(_switchT, 1f, dt * 3.2f);
            float lDrop = 1f - Ease(_switchT);
            lPos += Vector3.down * lDrop * 0.22f;
            lRot *= Quaternion.Euler(lDrop * 32f, 0f, 0f);
            _reloadT = Mathf.MoveTowards(_reloadT, 1f, dt / _reloadDuration);
            float lReload = Mathf.Sin(_reloadT * Mathf.PI);
            lPos += new Vector3(-0.025f, -0.045f, -0.02f) * lReload;
            lRot *= Quaternion.Euler(lReload * 16f, lReload * -8f, lReload * -26f);
            if (lAvatar.Motor.Sliding) lRot *= Quaternion.Euler(0f, 0f, 8f);

            _rig.localPosition = lPos;
            _rig.localRotation = lRot;

            // Aimed: square the sight line with the view and slide it onto the screen centre (the poses
            // are authored there; this takes out blends, sway and bob).
            if (_sight != null && lAds > 0.001f)
            {
                Vector3 lDir = transform.InverseTransformDirection(_sight.forward);
                Quaternion lSquare = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(lDir, Vector3.forward), lAds);
                Vector3 lSight = transform.InverseTransformPoint(_sight.position);
                _rig.localRotation = lSquare * _rig.localRotation;
                _rig.localPosition = lSight + lSquare * (_rig.localPosition - lSight);
                lSight = transform.InverseTransformPoint(_sight.position);
                _rig.localPosition -= new Vector3(lSight.x, lSight.y, 0f) * lAds;
                // ... then the gun drops below the crosshair: aiming clears the view, it never fills it.
                _rig.localPosition += ADS_HOLD * lAds;
            }

            // Recoil turns the rig about the sight (or the hand): the muzzle flips, the sight stays put.
            Transform lPivotPoint = _sight != null ? _sight : _socket;
            Vector3 lPivot = lPivotPoint != null ? transform.InverseTransformPoint(lPivotPoint.position) : new Vector3(0f, -0.1f, 0.35f);
            Quaternion lKick = Quaternion.Euler(_kickRot);
            _rig.localRotation = lKick * _rig.localRotation;
            _rig.localPosition = lPivot + lKick * (_rig.localPosition - lPivot) + _kickPos;
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }

    /// <summary>A released magazine falling out of the first-person view (a copy, gone in half a second).</summary>
    public class MagazineDrop : MonoBehaviour
    {
        private Vector3 _velocity;
        private Vector3 _spin;
        private float _life = 0.6f;

        public static void Spawn(Transform pMagazine, Transform pViewSpace)
        {
            GameObject lCopy = Instantiate(pMagazine.gameObject, pMagazine.position, pMagazine.rotation, pViewSpace);
            lCopy.name = "DroppedMag";
            MagazineDrop lDrop = lCopy.AddComponent<MagazineDrop>();
            lDrop._velocity = pViewSpace.InverseTransformDirection(Vector3.down) * 0.6f;
            lDrop._spin = new Vector3(Random.Range(-90f, 90f), 0f, Random.Range(-140f, 140f));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 lDown = transform.parent != null ? transform.parent.InverseTransformDirection(Vector3.down) : Vector3.down;
            _velocity += lDown * 9.81f * dt;
            transform.localPosition += _velocity * dt;
            transform.localRotation = Quaternion.Euler(_spin * dt) * transform.localRotation;
            _life -= dt;
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
