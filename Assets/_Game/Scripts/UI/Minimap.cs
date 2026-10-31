using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The minimap, bottom left: a small round floor plan of the player's surroundings, rotating
    /// with the view. An orthographic camera looks down from just above the player's head, so
    /// ceilings and upper floors are cut away and the current floor reads as a plan. On it: the
    /// player, teammates, NPCs hunting you, and the extractions once they are revealed.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        private const float RANGE = 32f, SIZE = 230f;

        private Camera _camera;
        private RenderTexture _texture;
        private RectTransform _plan, _icons;
        private readonly List<Image> _dots = new List<Image>();
        private int _used, _frame;
        private CanvasGroup _group;

        public void Build(RectTransform pRoot)
        {
            RectTransform lFrame = UIFactory.Rect(pRoot, "Minimap", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(36, 36), new Vector2(SIZE, SIZE));
            _group = lFrame.gameObject.AddComponent<CanvasGroup>();
            Image lMask = lFrame.gameObject.AddComponent<Image>();
            lMask.sprite = Disc();
            lMask.color = new Color(0.05f, 0.06f, 0.08f, 0.55f);
            lMask.raycastTarget = false;
            lFrame.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _texture = new RenderTexture(256, 256, 16) { name = "Minimap" };
            _plan = UIFactory.Rect(lFrame, "Plan", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SIZE, SIZE));
            RawImage lPlan = _plan.gameObject.AddComponent<RawImage>();
            lPlan.texture = _texture;
            lPlan.color = new Color(1f, 1f, 1f, 0.85f);
            lPlan.raycastTarget = false;
            _icons = UIFactory.Rect(lFrame, "Icons", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SIZE, SIZE));

            // The player, fixed at the centre and pointing up.
            Image lMe = UIFactory.Panel(lFrame, "Me", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 14));
            lMe.sprite = Arrow();

            GameObject lCameraObject = new GameObject("MinimapCamera");
            lCameraObject.transform.SetParent(transform, false);
            _camera = lCameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = RANGE;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 30f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.1f, 0.11f, 0.13f, 1f);
            _camera.cullingMask = 1 << GameLayers.DEFAULT | 1 << GameLayers.WATER | 1 << GameLayers.CLIMBABLE | 1 << GameLayers.INTERACTABLE;
            _camera.targetTexture = _texture;
            _camera.enabled = false;
            UniversalAdditionalCameraData lData = _camera.GetUniversalAdditionalCameraData();
            lData.renderShadows = false;
            lData.renderPostProcessing = false;
        }

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
        }

        private void LateUpdate()
        {
            PlayerAvatar lMe = PlayerAvatar.Local;
            bool lShow = lMe != null && lMe.Health.IsAlive && !MapScreen.IsOpen;
            _group.alpha = Mathf.MoveTowards(_group.alpha, lShow ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            if (!lShow) return;

            // The plan: from 1.9 m above the feet, so the floor above and the ceilings are cut away.
            float lYaw = lMe.transform.eulerAngles.y;
            _camera.transform.SetPositionAndRotation(lMe.transform.position + Vector3.up * 1.9f, Quaternion.Euler(90f, lYaw, 0f));
            if (++_frame % 2 == 0) _camera.Render();

            _used = 0;
            Quaternion lToMap = Quaternion.Euler(0, -lYaw, 0);
            if (LobbyState.Instance == null || LobbyState.Instance.Mode.Value != MatchMode.FreeForAll)
                foreach (PlayerAvatar lOther in PlayerAvatar.All)
                    if (lOther != null && lOther != lMe && lOther.Team.Value == lMe.Team.Value && lOther.Health.IsAlive) Dot(lMe, lToMap, lOther.transform.position, new Color(0.5f, 0.75f, 1f), 7f);
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.Health.IsAlive && (lNpc.IsAlerted || lNpc.Awareness.Value >= 10)) Dot(lMe, lToMap, lNpc.transform.position, lNpc.IsAlerted ? UIFactory.Accent : new Color(1f, 0.7f, 0.2f), 7f);
            MatchDirector lDirector = MatchDirector.Instance;
            if (lDirector != null && lDirector.Proof.Value != ProofState.TargetAlive)
            {
                List<MatchPoint> lExtractions = MatchPoint.OfKind(MatchPoint.Kind.Extraction);
                for (int i = 0; i < lExtractions.Count; i++)
                    if ((lDirector.ActiveExtractions.Value & (1 << i)) != 0) Dot(lMe, lToMap, lExtractions[i].transform.position, UIFactory.Good, 11f, true);
            }
            for (int i = _used; i < _dots.Count; i++) _dots[i].enabled = false;
        }

        /// <summary>A dot at a world position; beyond the range it sticks to the rim (when asked).</summary>
        private void Dot(PlayerAvatar pMe, Quaternion pToMap, Vector3 pWorld, Color pColor, float pSize, bool pClampToRim = false)
        {
            Vector3 lLocal = pToMap * (pWorld - pMe.transform.position);
            Vector2 p = new Vector2(lLocal.x, lLocal.z) / RANGE * (SIZE * 0.5f);
            if (p.magnitude > SIZE * 0.5f - 6f)
            {
                if (!pClampToRim) return;
                p = p.normalized * (SIZE * 0.5f - 6f);
            }
            if (_used >= _dots.Count)
            {
                Image lNew = UIFactory.Panel(_icons, "Dot", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 8f);
                lNew.sprite = Disc();
                _dots.Add(lNew);
            }
            Image lDot = _dots[_used++];
            lDot.enabled = true;
            lDot.color = pColor;
            lDot.rectTransform.sizeDelta = Vector2.one * pSize;
            lDot.rectTransform.anchoredPosition = p;
        }

        private static Sprite _disc, _arrow;

        private static Sprite Disc()
        {
            if (_disc != null) return _disc;
            const int S = 128;
            Texture2D lTexture = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f)) / (S * 0.5f);
                    lTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * S * 0.5f)));
                }
            lTexture.Apply();
            return _disc = Sprite.Create(lTexture, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        private static Sprite Arrow()
        {
            if (_arrow != null) return _arrow;
            const int S = 64;
            Texture2D lTexture = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float lHalf = (1f - (float)y / S) * S * 0.5f;
                    bool lNotch = y < S * 0.25f && Mathf.Abs(x - S * 0.5f) < (S * 0.25f - y);
                    lTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Abs(x - S * 0.5f) < lHalf && !lNotch ? 1f : 0f));
                }
            lTexture.Apply();
            return _arrow = Sprite.Create(lTexture, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
    }
}
