using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Weapons;
using TMPro;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// What a player reads above an NPC: a "?" filling with suspicion, a red "!" in combat, a radio
    /// icon with a countdown ring while it calls for reinforcements (shoot it before it ends),
    /// "zZ" when knocked out, a skull crown on the target. Plus the weapon in its hand.
    /// </summary>
    [RequireComponent(typeof(Npc))]
    public class NpcVisuals : MonoBehaviour
    {
        private Npc _npc;
        private TextMeshPro _icon;
        private Transform _iconRoot;
        private GameObject _weaponModel;
        private int _weaponItem = -2;
        private float _pop;
        private float _callProgress;

        private void Awake()
        {
            _npc = GetComponent<Npc>();
            _iconRoot = new GameObject("Icon").transform;
            _iconRoot.SetParent(transform, false);
            _iconRoot.localPosition = new Vector3(0, 2.35f, 0);
            _icon = _iconRoot.gameObject.AddComponent<TextMeshPro>();
            // TextMeshPro swaps the Transform for a RectTransform: take the new one.
            _iconRoot = _icon.transform;
            _icon.alignment = TextAlignmentOptions.Center;
            _icon.fontSize = 6f;
            _icon.fontStyle = FontStyles.Bold;
            _icon.outlineWidth = 0.25f;
            _icon.outlineColor = new Color32(20, 20, 30, 255);
            _icon.rectTransform.sizeDelta = new Vector2(4f, 2f);
            _icon.text = "";
        }

        public void PopAlert() => _pop = 1f;

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
            _icon.text = "";
            if (_weaponModel != null) _weaponModel.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_weaponModel == null && _npc.Weapon != null && _npc.Health.IsAlive) RefreshWeapon();
            if (!_npc.Health.IsAlive)
            {
                _icon.text = _npc.State.Value == NpcState.KnockedOut ? "<color=#9AD7FF>zZ</color>" : "";
                if (_npc.State.Value != NpcState.KnockedOut) { _icon.text = ""; enabled = false; }
                Billboard();
                return;
            }

            float lAware = _npc.Awareness.Value / 20f;
            string lText = "";
            switch (_npc.State.Value)
            {
                case NpcState.Combat:
                case NpcState.Search:
                    lText = _npc.State.Value == NpcState.Combat ? "<color=#FF4A4A>!</color>" : "<color=#FFB13B>?!</color>";
                    break;
                case NpcState.Calling:
                    _callProgress = Mathf.MoveTowards(_callProgress, 1f, Time.deltaTime / Mathf.Max(0.1f, Data.GameDatabase.Rules.callDuration));
                    int lBars = Mathf.CeilToInt(_callProgress * 5f);
                    lText = "<color=#FF4A4A>((o))</color>\n<size=60%><color=#FFE066>" + new string('|', lBars) + "<color=#555>" + new string('|', 5 - lBars) + "</color></color></size>";
                    break;
                case NpcState.Flee:
                    lText = "<color=#FFD24A>!!</color>";
                    break;
                case NpcState.Sick:
                    lText = "<color=#9BE36B>~</color>";
                    break;
                default:
                    if (lAware > 0.05f)
                    {
                        Color lColor = Color.Lerp(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.5f, 0.2f), lAware);
                        lText = "<color=#" + ColorUtility.ToHtmlStringRGB(lColor) + "><alpha=#" + ((int)(Mathf.Lerp(0.4f, 1f, lAware) * 255)).ToString("X2") + ">?</color>";
                    }
                    break;
            }
            if (_npc.State.Value != NpcState.Calling) _callProgress = 0f;
            if (_npc.IsTarget && lText == "") lText = "<size=45%><color=#FF5E7A>TARGET</color></size>";
            _icon.text = lText;

            _pop = Mathf.MoveTowards(_pop, 0f, Time.deltaTime * 3f);
            _iconRoot.localScale = Vector3.one * (1f + Mathf.Sin(_pop * Mathf.PI) * 0.8f + (lAware > 0.05f && lAware < 1f ? lAware * 0.3f : 0f));
            Billboard();
        }

        private void Billboard()
        {
            if (CameraRig.Instance == null) return;
            Transform lCamera = CameraRig.Instance.WorldCamera.transform;
            _iconRoot.rotation = Quaternion.LookRotation(_iconRoot.position - lCamera.position);
            float lDistance = Vector3.Distance(lCamera.position, _iconRoot.position);
            _icon.enabled = lDistance < 60f;
            _iconRoot.localScale *= Mathf.Clamp(lDistance / 12f, 0.6f, 2.5f);
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
