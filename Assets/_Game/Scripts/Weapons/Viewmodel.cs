using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The first-person hands and held item, drawn by the overlay camera: sway from the mouse and
    /// the movement, bob, recoil kick, aim-down-sights pose, lowering while sprinting, and little
    /// animations for switching, reloading, punching and throwing.
    /// </summary>
    public class Viewmodel : MonoBehaviour
    {
        private WeaponHolder _holder;
        private Transform _pivot;
        private Transform _model;
        private Transform _handL, _handR;
        private int _builtItem = -2;

        private Vector3 _sway, _swayVel;
        private Vector3 _kickPos, _kickRot;
        private float _switchT = 1f, _reloadT = 1f, _reloadDuration = 1f, _punchT = 1f, _throwT = 1f;
        private float _bob;
        private float _ads;

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
            GameObject lRoot = new GameObject("Viewmodel");
            lRoot.transform.SetParent(lRig.ViewmodelRoot, false);
            Viewmodel lView = lRoot.AddComponent<Viewmodel>();
            lView._holder = pHolder;
            lView._pivot = new GameObject("Pivot").transform;
            lView._pivot.SetParent(lRoot.transform, false);

            Mesh lMitten = LowPolyMesh.Cached("goob.hand", () => LowPolyMesh.Icosphere(0, 0.08f, 7));
            Color lSkin = new Color(1f, 0.83f, 0.7f);
            lView._handR = GameArt.Part(lView._pivot, "HandR", lMitten, new Vector3(0.02f, -0.07f, -0.06f), Vector3.one * 0.09f, lSkin).transform;
            lView._handL = GameArt.Part(lView._pivot, "HandL", lMitten, new Vector3(-0.06f, -0.05f, 0.12f), Vector3.one * 0.085f, lSkin).transform;
            GameLayers.SetRecursive(lRoot, GameLayers.LOCAL_VIEW);
            foreach (Renderer lRenderer in lRoot.GetComponentsInChildren<Renderer>()) lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lView;
        }

        public void Kick(float pAmount)
        {
            _kickPos += new Vector3(0, 0.01f, -0.05f) * pAmount;
            _kickRot += new Vector3(-6f, Random.Range(-1.5f, 1.5f), Random.Range(-3f, 3f)) * pAmount;
        }

        public void PlaySwitch() => _switchT = 0f;
        public void PlayReload(float pDuration) { _reloadT = 0f; _reloadDuration = pDuration; }
        public void PlayPunch() => _punchT = 0f;
        public void PlayThrow() => _throwT = 0f;

        private void Rebuild(ItemDefinition pItem)
        {
            _builtItem = pItem != null ? pItem.Index : -1;
            if (_model != null) Destroy(_model.gameObject);
            _model = null;
            Muzzle = null;
            if (pItem == null) return;
            GameObject lModel = WeaponModels.Build(pItem, _pivot, GameLayers.LOCAL_VIEW);
            _model = lModel.transform;
            _model.localPosition = Vector3.zero;
            Muzzle = _model.Find("Muzzle");
        }

        private void LateUpdate()
        {
            if (_holder == null) { Destroy(gameObject); return; }
            PlayerAvatar lAvatar = _holder.Avatar;
            ItemDefinition lItem = _holder.HeldItem;
            int lIndex = lItem != null ? lItem.Index : -1;
            if (lIndex != _builtItem) { Rebuild(lItem); _switchT = 0f; }

            bool lAlive = lAvatar.Health.IsAlive && lAvatar.Motor.Mode != MotorMode.Hidden;
            _pivot.gameObject.SetActive(lAlive && !_holder.Scoped);
            if (!lAlive) return;

            float dt = Time.deltaTime;
            Vector2 lLook = GameInput.Look;
            Vector3 lTargetSway = new Vector3(-lLook.x * 0.0009f, -lLook.y * 0.0009f, 0);
            Vector3 lLocalVel = lAvatar.transform.InverseTransformDirection(lAvatar.Motor.Velocity);
            lTargetSway += new Vector3(-lLocalVel.x * 0.004f, -lLocalVel.y * 0.003f, -lLocalVel.z * 0.002f);
            _swayVel += ((lTargetSway - _sway) * 160f - _swayVel * 16f) * dt;
            _sway += _swayVel * dt;
            _sway = Vector3.ClampMagnitude(_sway, 0.08f);

            _kickPos = Vector3.Lerp(_kickPos, Vector3.zero, 1f - Mathf.Exp(-14f * dt));
            _kickRot = Vector3.Lerp(_kickRot, Vector3.zero, 1f - Mathf.Exp(-12f * dt));

            float lSpeed = lAvatar.Motor.Grounded ? lAvatar.Motor.PlanarSpeed : 0f;
            _bob += lSpeed * dt * 1.9f;
            bool lAiming = _holder.AdsFov > 0f;
            _ads = Mathf.MoveTowards(_ads, lAiming ? 1f : 0f, dt * 7f);
            float lBobScale = Mathf.Lerp(1f, 0.2f, _ads) * Mathf.Clamp01(lSpeed / 6f);
            Vector3 lBob = new Vector3(Mathf.Cos(_bob) * 0.012f, Mathf.Abs(Mathf.Sin(_bob)) * 0.012f, 0) * lBobScale;

            Vector3 lHip = new Vector3(0.17f, -0.17f, 0.36f);
            Vector3 lAds = new Vector3(0f, -0.105f, 0.3f);
            Vector3 lPos = Vector3.Lerp(lHip, lAds, _ads) + _sway * (1f - _ads * 0.7f) + lBob + _kickPos;
            Quaternion lRot = Quaternion.Euler(_kickRot + new Vector3(-_sway.y * 200f, _sway.x * 200f, _sway.x * 300f) * (1f - _ads * 0.7f));

            // Sprint lowers the weapon, slides cant it.
            if (lAvatar.Motor.Sprinting) { lPos += new Vector3(-0.04f, -0.05f, -0.04f); lRot *= Quaternion.Euler(10f, -25f, 8f); }
            if (lAvatar.Motor.Sliding) lRot *= Quaternion.Euler(0, 0, 12f);

            _switchT = Mathf.MoveTowards(_switchT, 1f, dt * 3.5f);
            lPos += Vector3.down * (1f - Ease(_switchT)) * 0.3f;
            lRot *= Quaternion.Euler((1f - Ease(_switchT)) * 40f, 0, 0);

            _reloadT = Mathf.MoveTowards(_reloadT, 1f, dt / Mathf.Max(0.1f, _reloadDuration));
            float lReload = Mathf.Sin(_reloadT * Mathf.PI);
            lPos += new Vector3(0, -0.07f, 0) * lReload;
            lRot *= Quaternion.Euler(lReload * 25f, 0, lReload * -35f);

            _punchT = Mathf.MoveTowards(_punchT, 1f, dt * 5f);
            float lPunch = Mathf.Sin(_punchT * Mathf.PI);
            _throwT = Mathf.MoveTowards(_throwT, 1f, dt * 4f);
            float lThrow = Mathf.Sin(_throwT * Mathf.PI);

            _pivot.localPosition = lPos;
            _pivot.localRotation = lRot;
            _handL.localPosition = lItem != null && lItem.slot == ItemSlot.Weapon ? new Vector3(-0.04f, -0.05f, 0.16f) : new Vector3(-0.2f, -0.06f, 0.05f) + new Vector3(0.12f, 0.06f, 0.25f) * lPunch;
            _handR.localPosition = new Vector3(0.02f, -0.07f, -0.06f) + (lItem == null ? new Vector3(-0.05f, 0.05f, 0.3f) * lPunch : Vector3.zero) + new Vector3(0, 0.15f, 0.2f) * lThrow;
            if (_model != null) _model.localPosition = lItem != null && lItem.slot == ItemSlot.Gadget ? new Vector3(0.02f, -0.04f + lThrow * 0.15f, 0.02f + lThrow * 0.2f) : Vector3.zero;
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
