using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// Escape in a match: the game keeps running (it is online), the mouse is freed, and the
    /// settings and "leave" are one click away.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private static PauseMenu _instance;
        private GameObject _panel;
        private bool _open;

        public static bool IsOpen => _instance != null && _instance._open;

        public static void Ensure()
        {
            if (_instance == null) _instance = new GameObject("PauseMenu").AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            Canvas lCanvas = UIFactory.Canvas("PauseCanvas", 30);
            lCanvas.transform.SetParent(transform, false);
            _panel = UIFactory.Stretch(lCanvas.transform, "Panel").gameObject;
            _panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.7f);
            Transform p = _panel.transform;
            Vector2 C = new Vector2(0.5f, 0.5f);
            UIFactory.Text(p, "Title", "PAUSED", 80, UIFactory.Accent, TMPro.TextAlignmentOptions.Center, C, new Vector2(0, 330), new Vector2(800, 100));
            UIFactory.Button(p, "RESUME", C, new Vector2(0, 200), new Vector2(420, 80), UIFactory.Accent2, () => Toggle(false));
            SettingsPanel.Build(p, new Vector2(0, -20));
            UIFactory.Button(p, "LEAVE THE MATCH", C, new Vector2(0, -330), new Vector2(420, 80), UIFactory.Accent, () =>
            {
                Toggle(false);
                NetSession.Instance?.Leave();
            });
            _panel.SetActive(false);
        }

        private void Update()
        {
            if (GameInput.PausePressed && !MapScreen.IsOpen) Toggle(!_open);
        }

        private void Toggle(bool pOpen)
        {
            _open = pOpen;
            _panel.SetActive(pOpen);
            GameInput.GameplayEnabled = !pOpen;
            GameInput.LockCursor(!pOpen);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            GameInput.GameplayEnabled = true;
        }
    }

    /// <summary>Sensitivity, FOV, volume, crouch mode, voice mode - shared by the title and pause screens.</summary>
    public static class SettingsPanel
    {
        public static void Build(Transform pParent, Vector2 pCenter)
        {
            Vector2 C = new Vector2(0.5f, 0.5f);
            UIFactory.Slider(pParent, "SENSITIVITY", 0.2f, 3f, GameInput.MouseSensitivity, C, pCenter + new Vector2(0, 100), v => GameInput.MouseSensitivity = v);
            UIFactory.Slider(pParent, "FIELD OF VIEW", 65f, 100f, PlayerPrefs.GetFloat("goobyr.fov", 78f), C, pCenter + new Vector2(0, 35), v =>
            {
                PlayerPrefs.SetFloat("goobyr.fov", v);
                if (CameraRig.Instance != null) CameraRig.Instance.BaseFov = v;
            });
            UIFactory.Slider(pParent, "VOLUME", 0f, 1f, Audio.Sfx.Volume, C, pCenter + new Vector2(0, -30), v => { Audio.Sfx.Volume = v; AudioListener.volume = v; });
            UIFactory.Toggle(pParent, "TOGGLE CROUCH", GameInput.ToggleCrouch, C, pCenter + new Vector2(-60, -100), v => GameInput.ToggleCrouch = v);
            UIFactory.Toggle(pParent, "PUSH TO TALK (B)", Audio.VoiceChat.PushToTalk, C, pCenter + new Vector2(-60, -160), v => Audio.VoiceChat.PushToTalk = v);
        }
    }
}
