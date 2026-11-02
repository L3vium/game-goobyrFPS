using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// The weapon in an NPC's hand. What a player reads above it (gauge, confrontation, combat,
    /// phone call) is drawn by the HUD in screen space (<see cref="UI.ThreatMarkers"/>).
    /// </summary>
    [RequireComponent(typeof(Npc))]
    public class NpcVisuals : MonoBehaviour
    {
        private Npc _npc;
        private GameObject _weaponModel;
        private int _weaponItem = -2;

        private void Awake() => _npc = GetComponent<Npc>();

        public void RefreshWeapon(bool pForce = false)
        {
            HumanBody lBody = GetComponentInChildren<HumanBody>();
            if (lBody == null || lBody.WeaponSocket == null) return;
            int lItem = _npc.WeaponItem.Value;
            if (lItem == _weaponItem && _weaponModel != null && !pForce) return;
            _weaponItem = lItem;
            if (_weaponModel != null) Destroy(_weaponModel);
            if (_npc.Weapon == null) return;
            lBody.SetHeld(WeaponModels.HeldOf(_npc.Weapon));
            _weaponModel = WeaponModels.Build(_npc.Weapon, lBody.WeaponSocket, GameLayers.NPC);
        }

        public void HideAll()
        {
            if (_weaponModel != null) _weaponModel.SetActive(false);
        }

        private void LateUpdate()
        {
            if ((_weaponModel == null || _weaponModel.transform.parent == null) && _npc.Weapon != null && _npc.Health.IsAlive) RefreshWeapon(true);
            UpdateTorch();
            if (!_npc.Health.IsAlive) enabled = false;
        }

        private const int NIGHT = 3;
        private Light _torch;

        /// <summary>
        /// At night a guard on the move - patrolling, checking something out, searching, fighting -
        /// carries a torch along its aim: the beams sweeping the dark show the player where the hunt is.
        /// </summary>
        private void UpdateTorch()
        {
            NpcState lState = _npc.State.Value;
            bool lOn = _npc.Health.IsAlive && _npc.Weapon != null && !_npc.IsCrowd && !_npc.IsTarget
                       && TimeOfDay.Instance != null && TimeOfDay.Instance.Current == NIGHT
                       && (lState == NpcState.Patrol || lState == NpcState.Investigate || lState == NpcState.Search || lState == NpcState.Engaged || lState == NpcState.Combat);
            if (lOn && _torch == null)
            {
                _torch = new GameObject("Torch").AddComponent<Light>();
                _torch.transform.SetParent(transform, false);
                _torch.type = LightType.Spot;
                _torch.range = 20f;
                _torch.spotAngle = 44f;
                _torch.innerSpotAngle = 18f;
                _torch.intensity = 7f;
                _torch.color = new Color(1f, 0.94f, 0.82f);
                _torch.shadows = LightShadows.None;
            }
            if (_torch == null) return;
            if (_torch.enabled != lOn) _torch.enabled = lOn;
            if (!lOn) return;
            float lScale = transform.localScale.y;
            _torch.transform.position = transform.position + transform.up * (1.35f * lScale) + transform.forward * 0.35f + transform.right * 0.15f;
            _torch.transform.rotation = transform.rotation * Quaternion.Euler(_npc.AimPitch.Value + 8f, 0f, 0f);
        }
    }

    /// <summary>
    /// The hunted player's "shadow": a translucent agent standing where the NPCs believe the player
    /// is. Shown only to that player, only while they are out of sight - move away from it and
    /// the guards will keep shooting at an empty spot.
    /// </summary>
    public class LkpGhost : MonoBehaviour
    {
        private static LkpGhost _instance;
        private Transform _visual;
        private Renderer[] _renderers;
        private Vector3 _target;
        private bool _visible;
        private float _alpha;

        public static void Show(Vector3 pPosition, bool pVisible)
        {
            if (_instance == null) _instance = new GameObject("LkpGhost").AddComponent<LkpGhost>();
            _instance._target = pPosition;
            if (pVisible && !_instance._visible) _instance.transform.position = pPosition;
            _instance._visible = pVisible;
        }

        public static void Clear()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        private void Awake()
        {
            _visual = new GameObject("Silhouette").transform;
            _visual.SetParent(transform, false);
            // The agent's body, standing, in a flat translucent blue.
            GameObject lSource = Data.ArtRegistry.Get(HumanCast.AGENT);
            if (lSource != null)
            {
                GameObject lModel = Instantiate(lSource, _visual, false);
                Animator lAnimator = lModel.GetComponent<Animator>();
                if (lAnimator != null) { lAnimator.runtimeAnimatorController = HumanBody.Controller; lAnimator.applyRootMotion = false; }
            }
            List<Renderer> lRenderers = new List<Renderer>();
            foreach (Renderer lRenderer in _visual.GetComponentsInChildren<Renderer>())
            {
                if (lRenderer.name.EndsWith("_LOD1")) { lRenderer.enabled = false; continue; }
                Material[] lMaterials = new Material[lRenderer.sharedMaterials.Length];
                for (int i = 0; i < lMaterials.Length; i++) lMaterials[i] = GameArt.Transparent;
                lRenderer.sharedMaterials = lMaterials;
                lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lRenderers.Add(lRenderer);
            }
            _renderers = lRenderers.ToArray();
        }

        private void Update()
        {
            _alpha = Mathf.MoveTowards(_alpha, _visible ? 1f : 0f, Time.deltaTime * 3f);
            transform.position = Vector3.Lerp(transform.position, _target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            _visual.gameObject.SetActive(_alpha > 0.01f);
            float lPulse = 0.25f + Mathf.Sin(Time.time * 4f) * 0.08f;
            foreach (Renderer lRenderer in _renderers) GameArt.Paint(lRenderer, new Color(0.55f, 0.6f, 1f, lPulse * _alpha));

        }
    }
}
