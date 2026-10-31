using UnityEngine;
using UnityEngine.InputSystem;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// The Player action map of the project-wide actions asset (new Input System), with the
    /// cursor and menu locks. Gameplay reads it only while <see cref="GameplayEnabled"/>.
    /// </summary>
    public static class GameInput
    {
        private static InputActionMap _map;
        private static InputAction _move, _look, _fire, _aim, _reload, _interact, _crouch, _jump, _sprint, _melee, _throw, _scroll, _map_, _ptt, _pause, _detonate, _score, _nextSpectate, _instinct, _wheel;
        private static readonly InputAction[] _slots = new InputAction[6];

        /// <summary>Menus (pause, map screen, chat) switch gameplay input off.</summary>
        public static bool GameplayEnabled { get; set; } = true;

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("goobyr.sens", 1f);
            set => PlayerPrefs.SetFloat("goobyr.sens", value);
        }

        private static void Ensure()
        {
            if (_map != null) return;
            _map = InputSystem.actions.FindActionMap("Player", true);
            _move = _map.FindAction("Move"); _look = _map.FindAction("Look"); _fire = _map.FindAction("Fire"); _aim = _map.FindAction("Aim");
            _reload = _map.FindAction("Reload"); _interact = _map.FindAction("Interact"); _crouch = _map.FindAction("Crouch"); _jump = _map.FindAction("Jump");
            _sprint = _map.FindAction("Sprint"); _melee = _map.FindAction("Melee");
            _throw = _map.FindAction("Throw"); _scroll = _map.FindAction("Scroll"); _map_ = _map.FindAction("Map"); _ptt = _map.FindAction("PushToTalk");
            _pause = _map.FindAction("Pause"); _detonate = _map.FindAction("Detonate"); _score = _map.FindAction("Scoreboard"); _nextSpectate = _map.FindAction("NextSpectate");
            _instinct = _map.FindAction("Instinct");
            _wheel = _map.FindAction("Wheel");
            for (int i = 0; i < 6; i++) _slots[i] = _map.FindAction("Slot" + (i + 1));
            _map.Enable();
        }

        private static bool On => GameplayEnabled;

        public static Vector2 Move { get { Ensure(); return On ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero; } }
        /// <summary>The camera's look input (zero while the weapon wheel takes the look to pick).</summary>
        public static Vector2 Look { get { Ensure(); return On && !WheelOpen ? _look.ReadValue<Vector2>() * MouseSensitivity : Vector2.zero; } }
        /// <summary>The look input whatever the wheel (the wheel reads it to pick a slot).</summary>
        public static Vector2 RawLook { get { Ensure(); return On ? _look.ReadValue<Vector2>() : Vector2.zero; } }
        public static bool WheelHeld { get { Ensure(); return On && _wheel.IsPressed(); } }
        /// <summary>Set by the HUD while the weapon wheel is shown.</summary>
        public static bool WheelOpen { get; set; }
        public static bool Fire { get { Ensure(); return On && _fire.IsPressed(); } }
        public static bool FirePressed { get { Ensure(); return On && _fire.WasPressedThisFrame(); } }
        public static bool Aim { get { Ensure(); return On && _aim.IsPressed(); } }
        public static bool Reload { get { Ensure(); return On && _reload.WasPressedThisFrame(); } }
        public static bool Interact { get { Ensure(); return On && _interact.WasPressedThisFrame(); } }
        public static bool InteractHeld { get { Ensure(); return On && _interact.IsPressed(); } }
        public static bool CrouchPressed { get { Ensure(); return On && _crouch.WasPressedThisFrame(); } }
        public static bool CrouchHeld { get { Ensure(); return On && _crouch.IsPressed(); } }
        public static bool JumpPressed { get { Ensure(); return On && _jump.WasPressedThisFrame(); } }
        public static bool JumpHeld { get { Ensure(); return On && _jump.IsPressed(); } }
        public static bool Sprint { get { Ensure(); return On && _sprint.IsPressed(); } }
        public static bool Melee { get { Ensure(); return On && _melee.WasPressedThisFrame(); } }
        public static bool Throw { get { Ensure(); return On && _throw.WasPressedThisFrame(); } }
        public static float Scroll { get { Ensure(); return On ? _scroll.ReadValue<Vector2>().y : 0f; } }
        public static bool Detonate { get { Ensure(); return On && _detonate.WasPressedThisFrame(); } }
        public static bool InstinctHeld { get { Ensure(); return On && _instinct.IsPressed(); } }
        public static bool NextSpectate { get { Ensure(); return On && _nextSpectate.WasPressedThisFrame(); } }

        // Menu keys work whatever the gameplay lock.
        public static bool MapPressed { get { Ensure(); return _map_.WasPressedThisFrame(); } }
        public static bool PausePressed { get { Ensure(); return _pause.WasPressedThisFrame(); } }
        public static bool ScoreboardHeld { get { Ensure(); return _score.IsPressed(); } }
        public static bool PushToTalk { get { Ensure(); return _ptt.IsPressed(); } }

        public static int SlotPressed
        {
            get
            {
                Ensure();
                if (!On) return -1;
                for (int i = 0; i < 6; i++) if (_slots[i].WasPressedThisFrame()) return i;
                return -1;
            }
        }

        /// <summary>What the game wants; <see cref="CursorKeeper"/> re-applies it every frame and on focus.</summary>
        public static bool CursorWanted { get; private set; }

        public static void LockCursor(bool pLocked)
        {
            CursorWanted = pLocked;
            ApplyCursor();
        }

        /// <summary>Applies the wanted lock (the OS drops it on alt-tab or a click outside the window).</summary>
        public static void ApplyCursor()
        {
            CursorLockMode lMode = CursorWanted ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != lMode) Cursor.lockState = lMode;
            if (Cursor.visible == CursorWanted) Cursor.visible = !CursorWanted;
        }
    }
}
