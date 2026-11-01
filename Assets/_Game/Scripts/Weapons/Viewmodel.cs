using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The first-person arms (Chr_Arms: suit sleeves and hands) and the held item, drawn by the
    /// overlay camera.
    /// <para>
    /// A gun is posed procedurally in front of the camera - hip or aimed down the sights (its sight
    /// line on the screen centre), sway from the mouse and the movement, bob, recoil, lowered while
    /// sprinting, dropped while switching - and the hands are put on it by IK with the grips the
    /// character builder measured (<see cref="HumanRig"/>), so they always hold it right. The arms'
    /// Animator (<c>Characters/Arms</c>, arms-only mask: the shoulders never move) plays the aim
    /// clips for the fingers and elbows, and the reload, punch and throw clips, during which the
    /// clip leads the hands. Small items ride in the right hand.
    /// </para>
    /// </summary>
    public class Viewmodel : MonoBehaviour
    {
        private WeaponHolder _holder;
        private Transform _pivot;
        private Transform _model;
        private Transform _arms;
        private Animator _animator;
        private Transform _handSocket;
        private int _builtItem = -2;
        private HeldKind _held;
        private ItemDefinition _item;
        private bool _placed;
        private float _sightHeight;
        private Renderer[] _armRenderers = new Renderer[0];

        private Vector3 _sway, _swayVel;
        private Vector3 _kickPos, _kickRot;
        private float _switchT = 1f, _reloadT = 1f, _reloadDuration = 1f, _punchT = 1f, _throwT = 1f;
        private float _bob;
        private float _ads;
        private float _ikRight = 1f, _ikLeft = 1f;
        private int _armsState;
        private Quaternion _rightRotation = Quaternion.identity, _leftRotation = Quaternion.identity;
        private Vector3 _leftTarget;

        private static RuntimeAnimatorController _controller;

        // Poses of the held item in camera space (metres, degrees).
        public static Vector3 PISTOL_HIP = new Vector3(0.12f, -0.17f, 0.44f), PISTOL_TURN = new Vector3(0f, -2f, 0f);
        public static Vector3 RIFLE_HIP = new Vector3(0.13f, -0.17f, 0.34f), RIFLE_TURN = new Vector3(0f, -1.5f, 0f);
        public static Vector3 ITEM_HIP = new Vector3(0.14f, -0.13f, 0.34f), ITEM_TURN = new Vector3(-10f, -12f, 8f);
        public static Vector3 FISTS = new Vector3(0.15f, -0.17f, 0.37f), FIST_LEFT = new Vector3(0f, 0.015f, 0.05f), FIST_TURN = new Vector3(-12f, -8f, -65f);
        public static float PISTOL_ADS_Z = 0.36f, RIFLE_ADS_Z = 0.3f;

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
            GameObject lRoot = new GameObject("Viewmodel");
            lRoot.transform.SetParent(lRig.ViewmodelRoot, false);
            Viewmodel lView = lRoot.AddComponent<Viewmodel>();
            lView._holder = pHolder;
            lView._pivot = new GameObject("Pivot").transform;
            lView._pivot.SetParent(lRoot.transform, false);
            lView.BuildArms();
            GameLayers.SetRecursive(lRoot, GameLayers.LOCAL_VIEW);
            foreach (Renderer lRenderer in lRoot.GetComponentsInChildren<Renderer>()) lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lView;
        }

        private void BuildArms()
        {
            GameObject lSource = ArtRegistry.Get(HumanCast.ARMS);
            if (lSource == null) return;
            GameObject lArms = Instantiate(lSource, transform, false);
            lArms.name = "Arms";
            _arms = lArms.transform;
            _animator = lArms.GetComponent<Animator>();
            if (_controller == null) _controller = Resources.Load<RuntimeAnimatorController>("Characters/Arms");
            if (_animator != null)
            {
                _animator.runtimeAnimatorController = _controller;
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                lArms.AddComponent<ViewmodelIK>().View = this;
                Transform lHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (lHand != null)
                {
                    _handSocket = new GameObject("HandSocket").transform;
                    _handSocket.SetParent(lHand, false);
                    _handSocket.SetLocalPositionAndRotation(HumanRig.Instance.pinchPosition, HumanRig.Instance.pistol.handRotation);
                }
            }
            Cuff(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            Cuff(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            _armRenderers = lArms.GetComponentsInChildren<Renderer>();
            // Arms half a metre from the eye catch the full sun the world around them is shaded from:
            // their own instances, skin a little darker and less glossy, so they read as skin, not white.
            foreach (Renderer lRenderer in _armRenderers)
            {
                Material[] lMaterials = lRenderer.materials;
                foreach (Material lMaterial in lMaterials)
                {
                    bool lSkin = lMaterial.name.Contains("Body");
                    lMaterial.SetColor("_BaseColor", lSkin ? new Color(0.68f, 0.62f, 0.6f) : new Color(0.8f, 0.8f, 0.82f));
                    lMaterial.SetFloat("_Smoothness", lSkin ? 0.35f : 0.3f);
                }
                lRenderer.materials = lMaterials;
            }
            foreach (Renderer lRenderer in _armRenderers)
            {
                if (lRenderer is SkinnedMeshRenderer lSkin) { lSkin.updateWhenOffscreen = true; lSkin.skinnedMotionVectors = false; }
            }
        }

        /// <summary>A white shirt cuff showing past the suit sleeve at the wrist.</summary>
        private void Cuff(HumanBodyBones pForearm, HumanBodyBones pHand)
        {
            Transform lArm = _animator != null ? _animator.GetBoneTransform(pForearm) : null, lHand = _animator != null ? _animator.GetBoneTransform(pHand) : null;
            if (lArm == null || lHand == null) return;
            Vector3 lAlong = lArm.InverseTransformPoint(lHand.position);
            GameObject lCuff = new GameObject("Cuff");
            lCuff.transform.SetParent(lArm, false);
            lCuff.transform.localPosition = lAlong * 0.86f;
            lCuff.transform.localRotation = Quaternion.FromToRotation(Vector3.up, lAlong.normalized);
            lCuff.transform.localScale = new Vector3(0.068f, 0.035f, 0.058f) / Mathf.Max(0.01f, lArm.lossyScale.x);
            lCuff.AddComponent<MeshFilter>().sharedMesh = HumanBody.BandMesh();
            MeshRenderer lRenderer = lCuff.AddComponent<MeshRenderer>();
            Material lMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lMaterial.SetColor("_BaseColor", new Color(0.78f, 0.79f, 0.8f));
            lMaterial.SetFloat("_Smoothness", 0.25f);
            lMaterial.SetFloat("_Cull", 0f);
            lRenderer.sharedMaterial = lMaterial;
        }

        public void Kick(float pAmount)
        {
            _kickPos += new Vector3(0, 0.008f, -0.045f) * pAmount;
            _kickRot += new Vector3(-5f, Random.Range(-1.2f, 1.2f), Random.Range(-2.5f, 2.5f)) * pAmount;
            Play(_held == HeldKind.Rifle ? HumanAnimator.S_FIRE_RIFLE : HumanAnimator.S_FIRE_PISTOL, 0.02f);
        }

        public void PlaySwitch() => _switchT = 0f;
        public void PlayReload(float pDuration)
        {
            _reloadT = 0f;
            _reloadDuration = pDuration;
            if (_animator != null) _animator.SetFloat(HumanAnimator.PACE, 2f / Mathf.Max(0.5f, pDuration));
            Play(HumanAnimator.S_RELOAD, 0.1f);
        }

        public void PlayPunch()
        {
            _punchT = 0f;
            if (_animator != null) _animator.SetFloat(HumanAnimator.PACE, 1.7f);
            Play(HumanAnimator.S_PUNCH, 0.05f);
        }

        public void PlayThrow()
        {
            _throwT = 0f;
            if (_animator != null) _animator.SetFloat(HumanAnimator.PACE, 1.6f);
            Play(HumanAnimator.S_THROW, 0.05f, 0.35f);
        }

        private void Play(string pState, float pFade, float pOffset = 0f)
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;
            _armsState = Animator.StringToHash(pState);
            _animator.CrossFadeInFixedTime(_armsState, pFade, 0, pOffset);
        }

        /// <summary>The idle hold of the current item.</summary>
        private string HoldState => _held == HeldKind.Rifle ? HumanAnimator.S_AIM_RIFLE : _held == HeldKind.Pistol ? HumanAnimator.S_AIM_PISTOL : _held == HeldKind.None ? HumanAnimator.S_PUNCH : HumanAnimator.S_PISTOL_READY;

        /// <summary>Back to the idle hold: fists hold the punch clip's opening guard (its fingers make the fists).</summary>
        private void PlayHold(float pFade)
        {
            if (_animator != null) _animator.SetFloat(HumanAnimator.PACE, _held == HeldKind.None ? 0f : 1f);
            Play(HoldState, pFade, _held == HeldKind.None ? 0.04f : 0f);
        }

        private void Rebuild(ItemDefinition pItem)
        {
            _builtItem = pItem != null ? pItem.Index : -1;
            _item = pItem;
            _held = WeaponModels.HeldOf(pItem);
            if (_model != null) Destroy(_model.gameObject);
            _model = null;
            Muzzle = null;
            _sightHeight = 0.1f;
            if (pItem != null)
            {
                // Guns sit on the pivot (the hands follow them); small items ride in the hand.
                bool lGun = _held == HeldKind.Pistol || _held == HeldKind.Rifle;
                Transform lParent = lGun || _handSocket == null ? _pivot : _handSocket;
                GameObject lModel = WeaponModels.Build(pItem, lParent, GameLayers.LOCAL_VIEW);
                _model = lModel.transform;
                Muzzle = _model.Find("Muzzle");
                _sightHeight = SightHeight(pItem, Muzzle);
                foreach (Renderer lRenderer in lModel.GetComponentsInChildren<Renderer>()) lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            PlayHold(0.15f);
        }

        /// <summary>Height of the sight line above the grip: the top of the slide for pistols, the rail for long guns.</summary>
        private static float SightHeight(ItemDefinition pItem, Transform pMuzzle)
        {
            float lBore = pMuzzle != null ? pMuzzle.localPosition.y : 0.08f;
            return pItem.kind switch
            {
                ItemKind.Pistol or ItemKind.SilencedPistol => lBore,
                ItemKind.Smg => lBore + 0.05f,
                ItemKind.Shotgun => lBore + 0.03f,
                _ => lBore + 0.04f
            };
        }

        private void Update()
        {
            if (_holder == null) { Destroy(gameObject); return; }
            PlayerAvatar lAvatar = _holder.Avatar;
            ItemDefinition lItem = _holder.HeldItem;
            int lIndex = lItem != null ? lItem.Index : -1;
            if (lIndex != _builtItem) { Rebuild(lItem); _switchT = 0f; }
            PlaceArms();

            bool lAlive = lAvatar.Health.IsAlive && lAvatar.Motor.Mode != MotorMode.Hidden;
            bool lShown = lAlive && !_holder.Scoped && lAvatar.Motor.Mode != MotorMode.Climb && lAvatar.Motor.Mode != MotorMode.LedgeHang;
            _pivot.gameObject.SetActive(lShown);
            if (_arms != null) _arms.gameObject.SetActive(lShown);
            if (!lShown) return;

            float dt = Time.deltaTime;
            Vector2 lLook = GameInput.Look;
            Vector3 lTargetSway = new Vector3(-lLook.x * 0.0008f, -lLook.y * 0.0008f, 0);
            Vector3 lLocalVel = lAvatar.transform.InverseTransformDirection(lAvatar.Motor.Velocity);
            lTargetSway += new Vector3(-lLocalVel.x * 0.0035f, -lLocalVel.y * 0.0025f, -lLocalVel.z * 0.0015f);
            _swayVel += ((lTargetSway - _sway) * 160f - _swayVel * 16f) * dt;
            _sway += _swayVel * dt;
            _sway = Vector3.ClampMagnitude(_sway, 0.05f);

            _kickPos = Vector3.Lerp(_kickPos, Vector3.zero, 1f - Mathf.Exp(-14f * dt));
            _kickRot = Vector3.Lerp(_kickRot, Vector3.zero, 1f - Mathf.Exp(-12f * dt));

            float lSpeed = lAvatar.Motor.Grounded ? lAvatar.Motor.PlanarSpeed : 0f;
            _bob += lSpeed * dt * 1.9f;
            bool lAiming = _holder.AdsFov > 0f;
            _ads = Mathf.MoveTowards(_ads, lAiming ? 1f : 0f, dt * 6f);
            float lAds = Ease(_ads);
            float lBobScale = Mathf.Lerp(1f, 0.15f, lAds) * Mathf.Clamp01(lSpeed / 6f);
            Vector3 lBob = new Vector3(Mathf.Cos(_bob) * 0.01f, Mathf.Abs(Mathf.Sin(_bob)) * 0.012f, 0) * lBobScale;

            // Hip and sights: the sight line on the screen centre when aiming.
            Vector3 lHip, lSight;
            Quaternion lHipTurn;
            switch (_held)
            {
                case HeldKind.Pistol:
                    lHip = PISTOL_HIP; lHipTurn = Quaternion.Euler(PISTOL_TURN);
                    lSight = new Vector3(0f, -_sightHeight, PISTOL_ADS_Z);
                    break;
                case HeldKind.Rifle:
                    lHip = RIFLE_HIP; lHipTurn = Quaternion.Euler(RIFLE_TURN);
                    lSight = new Vector3(0f, -_sightHeight, RIFLE_ADS_Z);
                    break;
                case HeldKind.None:
                    lHip = FISTS; lHipTurn = Quaternion.Euler(FIST_TURN);
                    lSight = lHip;
                    break;
                default:
                    lHip = ITEM_HIP; lHipTurn = Quaternion.Euler(ITEM_TURN);
                    lSight = lHip;
                    break;
            }
            Vector3 lPos = Vector3.Lerp(lHip, lSight, lAds) + _sway * (1f - lAds * 0.75f) + lBob + _kickPos;
            Quaternion lRot = Quaternion.Slerp(lHipTurn, Quaternion.identity, lAds) * Quaternion.Euler(_kickRot + new Vector3(-_sway.y * 200f, _sway.x * 200f, _sway.x * 250f) * (1f - lAds * 0.75f));

            // Sprint lowers and turns the gun in, slides cant it.
            float lSprint = lAvatar.Motor.Sprinting && !lAiming ? 1f : 0f;
            lPos += new Vector3(-0.03f, -0.05f, -0.03f) * lSprint;
            lRot *= Quaternion.Euler(new Vector3(14f, -22f, 10f) * lSprint);
            if (lAvatar.Motor.Sliding) lRot *= Quaternion.Euler(0, 0, 10f);

            _switchT = Mathf.MoveTowards(_switchT, 1f, dt * 3.5f);
            lPos += Vector3.down * (1f - Ease(_switchT)) * 0.25f;
            lRot *= Quaternion.Euler((1f - Ease(_switchT)) * 35f, 0, 0);

            _reloadT = Mathf.MoveTowards(_reloadT, 1f, dt / Mathf.Max(0.1f, _reloadDuration));
            float lReload = Mathf.Sin(_reloadT * Mathf.PI);
            lPos += new Vector3(-0.03f, -0.05f, -0.02f) * lReload;
            lRot *= Quaternion.Euler(lReload * 18f, lReload * -10f, lReload * -28f);

            _punchT = Mathf.MoveTowards(_punchT, 1f, dt * 1.5f);
            _throwT = Mathf.MoveTowards(_throwT, 1f, dt * 1.4f);

            _pivot.localPosition = lPos;
            _pivot.localRotation = lRot;

            // Back to the hold once an action clip is done.
            if (_punchT >= 1f && _throwT >= 1f && _reloadT >= 1f && _animator != null && (_armsState != Animator.StringToHash(HoldState) || (_held == HeldKind.None && _animator.GetFloat(HumanAnimator.PACE) > 0f))
                && (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9f || _armsState != Animator.StringToHash(HumanAnimator.S_PUNCH)) && !_animator.IsInTransition(0))
            {
                PlayHold(0.2f);
            }

            // How much the IK leads the hands: the clip takes over for the actions.
            bool lGun = _held == HeldKind.Pistol || _held == HeldKind.Rifle;
            float lRightTarget = _punchT < 1f || _throwT < 1f ? 0f : 1f;
            float lLeftTarget = (lGun || _held == HeldKind.None) && _reloadT >= 1f && _punchT >= 1f ? 1f : 0f;
            _ikRight = Mathf.MoveTowards(_ikRight, lRightTarget, dt * 8f);
            _ikLeft = Mathf.MoveTowards(_ikLeft, lLeftTarget, dt * 6f);
        }

        /// <summary>
        /// Keeps the arms' eyes on the camera: the arms-only mask leaves the body in the avatar's
        /// default pose, so the offset is measured on the evaluated pose (it settles after a frame and
        /// stays, the shoulders never move).
        /// </summary>
        private void PlaceArms()
        {
            if (_arms == null || _animator == null) return;
            Transform lHead = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (lHead == null) return;
            Vector3 lEye = transform.InverseTransformPoint(lHead.TransformPoint(HumanRig.Instance.eyeInHead));
            Vector3 lError = lEye - SHOULDER_DROP;
            if (lError.sqrMagnitude > 0.000001f) _arms.localPosition -= lError;
            _placed = true;
        }

        /// <summary>Shoulders lower and further back than real: the forearms rise into the view from the bottom corners instead of hiding the hands.</summary>
        public static Vector3 SHOULDER_DROP = new Vector3(0f, 0.2f, 0.24f);
        /// <summary>Elbows down and in: the forearms rise from the bottom of the screen, not across it.</summary>
        public static Vector3 RIGHT_ELBOW = new Vector3(0.3f, -0.62f, 0.1f), LEFT_ELBOW = new Vector3(-0.18f, -0.65f, 0.18f);

        /// <summary>The hands on the item (called from the arms' IK pass).</summary>
        public void OnIK()
        {
            if (_animator == null) return;
            HumanRig lRig = HumanRig.Instance;
            bool lGun = _held == HeldKind.Pistol || _held == HeldKind.Rifle;
            HumanRig.Grip lGrip = lRig.For(_held);
            Quaternion lHandRotation;
            Vector3 lHandPosition;
            if (lGun)
            {
                lHandRotation = _pivot.rotation * Quaternion.Inverse(lGrip.handRotation);
                lHandPosition = _pivot.position - lHandRotation * lGrip.handPosition;
            }
            else if (_held == HeldKind.None)
            {
                // Fists up: the right fist at the pivot, the left one mirrored and a little forward.
                lHandRotation = _pivot.rotation * Quaternion.Inverse(lRig.pistol.handRotation);
                lHandPosition = _pivot.position - lHandRotation * lRig.pistol.handPosition;
                Vector3 lLeftPivot = transform.TransformPoint(Vector3.Scale(_pivot.localPosition, new Vector3(-1f, 1f, 1f)) + FIST_LEFT);
                Quaternion lLeftFist = transform.rotation * Quaternion.Euler(Vector3.Scale(_pivot.localEulerAngles, new Vector3(1f, -1f, -1f))) * lRig.leftFistRotation;
                _leftRotation = Quaternion.Inverse(transform.rotation) * lLeftFist;
                _leftTarget = lLeftPivot - lLeftFist * lRig.leftPalm;
            }
            else
            {
                // A small item (or the empty hand): the palm at the pivot, item between the fingers.
                lHandRotation = _pivot.rotation * Quaternion.Inverse(lRig.pinchRotation);
                lHandPosition = _pivot.position - lHandRotation * lRig.pinchPosition;
            }
            // Position by IK (the wrist), rotation written on the bone after the pass (LateUpdate):
            // the grips are measured on the raw bones, not in the humanoid goal space.
            _rightRotation = Quaternion.Inverse(transform.rotation) * lHandRotation;
            if (_held != HeldKind.None)
            {
                _leftRotation = Quaternion.Inverse(transform.rotation) * (_pivot.rotation * lGrip.supportRotation);
                _leftTarget = _pivot.position + _pivot.rotation * lRig.SupportFor(_held, Muzzle);
            }
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _ikRight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, lHandPosition);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _ikRight * 0.9f);
            _animator.SetIKHintPosition(AvatarIKHint.RightElbow, transform.TransformPoint(RIGHT_ELBOW));

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _ikLeft);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftTarget);
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _ikLeft * 0.9f);
            _animator.SetIKHintPosition(AvatarIKHint.LeftElbow, transform.TransformPoint(LEFT_ELBOW));
        }

        private void LateUpdate()
        {
            if (_animator == null || _arms == null || !_arms.gameObject.activeInHierarchy) return;
            Turn(HumanBodyBones.RightHand, _rightRotation, _ikRight);
            Turn(HumanBodyBones.LeftHand, _leftRotation, _ikLeft);
        }

        private void Turn(HumanBodyBones pBone, Quaternion pRotation, float pWeight)
        {
            Transform lBone = _animator.GetBoneTransform(pBone);
            if (lBone != null && pWeight > 0.001f) lBone.rotation = Quaternion.Slerp(lBone.rotation, transform.rotation * pRotation, pWeight);
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }

    /// <summary>Forwards the first-person arms' IK pass to the <see cref="Viewmodel"/>.</summary>
    public class ViewmodelIK : MonoBehaviour
    {
        public Viewmodel View { get; set; }
        private void OnAnimatorIK(int pLayer) { if (View != null) View.OnIK(); }
    }
}
