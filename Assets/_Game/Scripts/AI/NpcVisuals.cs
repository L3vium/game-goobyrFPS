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

        public void RefreshWeapon()
        {
            GoobBody lBody = GetComponentInChildren<GoobBody>();
            if (lBody == null || lBody.WeaponSocket == null) return;
            int lItem = _npc.WeaponItem.Value;
            if (lItem == _weaponItem && _weaponModel != null) return;
            _weaponItem = lItem;
            if (_weaponModel != null) Destroy(_weaponModel);
            if (_npc.Weapon == null) return;
            _weaponModel = WeaponModels.Build(_npc.Weapon, lBody.WeaponSocket, GameLayers.NPC);
            _weaponModel.transform.localScale = Vector3.one * 1.3f;
        }

        public void HideAll()
        {
            if (_weaponModel != null) _weaponModel.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_weaponModel == null && _npc.Weapon != null && _npc.Health.IsAlive) RefreshWeapon();
            if (!_npc.Health.IsAlive) enabled = false;
        }
    }

    /// <summary>
    /// The hunted player's "shadow": a translucent Goob standing where the NPCs believe the player
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
            Mesh lBlob = LowPolyMesh.Cached("goob.blob", () => LowPolyMesh.Blob(1.25f, 3));
            Mesh lSphere = LowPolyMesh.Cached("goob.sphere", () => LowPolyMesh.Icosphere(1));
            Color lColor = new Color(0.55f, 0.6f, 1f, 0.35f);
            GameArt.Part(_visual, "Body", lBlob, new Vector3(0, 0.78f, 0), new Vector3(0.72f, 0.75f, 0.6f), lColor, GameArt.Transparent);
            GameArt.Part(_visual, "Head", lSphere, new Vector3(0, 1.42f, 0), Vector3.one * 0.5f, lColor, GameArt.Transparent);
            _renderers = _visual.GetComponentsInChildren<Renderer>();
            foreach (Renderer lRenderer in _renderers) lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            _alpha = Mathf.MoveTowards(_alpha, _visible ? 1f : 0f, Time.deltaTime * 3f);
            transform.position = Vector3.Lerp(transform.position, _target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            _visual.gameObject.SetActive(_alpha > 0.01f);
            float lPulse = 0.25f + Mathf.Sin(Time.time * 4f) * 0.08f;
            foreach (Renderer lRenderer in _renderers) GameArt.Paint(lRenderer, new Color(0.55f, 0.6f, 1f, lPulse * _alpha));
            _visual.localScale = new Vector3(1f, 1f + Mathf.Sin(Time.time * 3f) * 0.03f, 1f);
        }
    }
}
