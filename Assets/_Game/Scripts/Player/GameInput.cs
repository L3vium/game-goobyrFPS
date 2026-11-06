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
        private static InputAction _move, _look, _fire, _aim, _reload, _interact, _crouch, _jump, _sprint, _melee, _throw, _scroll, _map_, _ptt, _pause, _detonate, _score, _nextSpectate, _instinct, _leanLeft, _leanRight, _block, _zoom, _takedown, _moveItem;
        private static InputActionMap _caster;
        private static InputAction _casterRise, _casterDescend, _casterFast, _casterSwitch, _casterMulti, _casterFly, _casterNext, _casterEyes, _casterClean, _casterReset;
        private static readonly InputAction[] _slots = new InputAction[Weapons.WeaponHolder.WEAPON_SLOTS + 1];

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
            LoadBindings();
            _map = InputSystem.actions.FindActionMap("Player", true);
            _move = _map.FindAction("Move"); _look = _map.FindAction("Look"); _fire = _map.FindAction("Fire"); _aim = _map.FindAction("Aim");
            _reload = _map.FindAction("Reload"); _interact = _map.FindAction("Interact"); _crouch = _map.FindAction("Crouch"); _jump = _map.FindAction("Jump");
            _sprint = _map.FindAction("Sprint"); _melee = _map.FindAction("Melee");
            _throw = _map.FindAction("Throw"); _scroll = _map.FindAction("Scroll"); _map_ = _map.FindAction("Map"); _ptt = _map.FindAction("PushToTalk");
            _pause = _map.FindAction("Pause"); _detonate = _map.FindAction("Detonate"); _score = _map.FindAction("Scoreboard"); _nextSpectate = _map.FindAction("NextSpectate");
            _instinct = _map.FindAction("Instinct");
            _leanLeft = _map.FindAction("LeanLeft"); _leanRight = _map.FindAction("LeanRight");
            _block = _map.FindAction("Block"); _zoom = _map.FindAction("Zoom"); _takedown = _map.FindAction("Takedown"); _moveItem = _map.FindAction("MoveItem");
            for (int i = 0; i < _slots.Length; i++) _slots[i] = _map.FindAction("Slot" + (i + 1));
            _map.Enable();
            _caster = InputSystem.actions.FindActionMap("Caster", true);
            _casterRise = _caster.FindAction("Rise"); _casterDescend = _caster.FindAction("Descend"); _casterFast = _caster.FindAction("Fast");
            _casterSwitch = _caster.FindAction("ModeSwitch"); _casterMulti = _caster.FindAction("ModeMulti"); _casterFly = _caster.FindAction("ModeFly");
            _casterNext = _caster.FindAction("NextFeed"); _casterEyes = _caster.FindAction("Eyes"); _casterClean = _caster.FindAction("Clean"); _casterReset = _caster.FindAction("ResetCamera");
            _caster.Enable();
        }

        // ---------------------------------------------------------------- key bindings

        private const string BINDINGS_KEY = "hitmen.bindings";

        private static void LoadBindings()
        {
            string lJson = PlayerPrefs.GetString(BINDINGS_KEY, "");
            if (!string.IsNullOrEmpty(lJson)) InputSystem.actions.LoadBindingOverridesFromJson(lJson);
        }

        /// <summary>Keeps the player's keys for the next run.</summary>
        public static void SaveBindings()
        {
            PlayerPrefs.SetString(BINDINGS_KEY, InputSystem.actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void ResetBindings()
        {
            InputSystem.actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BINDINGS_KEY);
            PlayerPrefs.Save();
        }

        /// <summary>What to press for an action of the Player map, as a prompt shows it: "E", "F"...</summary>
        public static string KeyName(string pAction)
        {
            Ensure();
            InputAction lAction = _map.FindAction(pAction);
            if (lAction == null) return "?";
            for (int i = 0; i < lAction.bindings.Count; i++)
            {
                InputBinding lBinding = lAction.bindings[i];
                if (lBinding.isComposite || lBinding.isPartOfComposite) continue;
                string lPath = lBinding.effectivePath ?? "";
                if (!lPath.StartsWith("<Keyboard>") && !lPath.StartsWith("<Mouse>")) continue;
                // The number row prints its symbol on some layouts: the digit reads better.
                char lLast = lPath[lPath.Length - 1];
                if (lPath.Length == "<Keyboard>/1".Length && char.IsDigit(lLast)) return lLast.ToString();
                return lAction.GetBindingDisplayString(i).ToUpperInvariant();
            }
            return "?";
        }

        private static bool On => GameplayEnabled;

        public static Vector2 Move { get { Ensure(); return On ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero; } }
        /// <summary>The move keys whatever the gameplay lock (the caster's flight).</summary>
        public static Vector2 MoveAlways { get { Ensure(); return Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f); } }
        public static Vector2 Look { get { Ensure(); return On ? _look.ReadValue<Vector2>() * MouseSensitivity : Vector2.zero; } }
        public static bool Fire { get { Ensure(); return On && _fire.IsPressed(); } }
        public static bool FirePressed { get { Ensure(); return On && _fire.WasPressedThisFrame(); } }
        public static bool Aim { get { Ensure(); return On && _aim.IsPressed(); } }
        /// <summary>The right button let go: a gadget held with its arc showing is thrown.</summary>
        public static bool AimReleased { get { Ensure(); return On && _aim.WasReleasedThisFrame(); } }
        /// <summary>Lean: -1 left (Q, A on AZERTY), +1 right (E), 0 both or neither.</summary>
        public static float Lean { get { Ensure(); return !On ? 0f : (_leanRight.IsPressed() ? 1f : 0f) - (_leanLeft.IsPressed() ? 1f : 0f); } }
        public static bool BlockHeld { get { Ensure(); return On && _block.IsPressed(); } }
        public static bool ZoomHeld { get { Ensure(); return On && _zoom.IsPressed(); } }
        public static bool Reload { get { Ensure(); return On && _reload.WasPressedThisFrame(); } }
        public static bool Interact { get { Ensure(); return On && _interact.WasPressedThisFrame(); } }
        public static bool InteractHeld { get { Ensure(); return On && _interact.IsPressed(); } }
        public static bool CrouchPressed { get { Ensure(); return On && _crouch.WasPressedThisFrame(); } }
        public static bool CrouchHeld { get { Ensure(); return On && _crouch.IsPressed(); } }
        public static bool JumpPressed { get { Ensure(); return On && _jump.WasPressedThisFrame(); } }
        public static bool JumpHeld { get { Ensure(); return On && _jump.IsPressed(); } }
        public static bool Sprint { get { Ensure(); return On && _sprint.IsPressed(); } }
        public static bool SprintPressed { get { Ensure(); return On && _sprint.WasPressedThisFrame(); } }
        public static bool Melee { get { Ensure(); return On && _melee.WasPressedThisFrame(); } }
        public static bool Throw { get { Ensure(); return On && _throw.WasPressedThisFrame(); } }
        public static float Scroll { get { Ensure(); return On ? _scroll.ReadValue<Vector2>().y : 0f; } }
        public static bool Detonate { get { Ensure(); return On && _detonate.WasPressedThisFrame(); } }
        public static bool InstinctHeld { get { Ensure(); return On && _instinct.IsPressed(); } }
        /// <summary>The silent kill, from behind an NPC that has not seen you.</summary>
        public static bool TakedownPressed { get { Ensure(); return On && _takedown.WasPressedThisFrame(); } }
        /// <summary>The movement item (grapple, boots) is used, or held (jetpack).</summary>
        public static bool MoveItemPressed { get { Ensure(); return On && _moveItem.WasPressedThisFrame(); } }
        public static bool MoveItemHeld { get { Ensure(); return On && _moveItem.IsPressed(); } }
        public static bool NextSpectate { get { Ensure(); return On && _nextSpectate.WasPressedThisFrame(); } }

        // Menu keys work whatever the gameplay lock.
        public static bool MapPressed { get { Ensure(); return _map_.WasPressedThisFrame(); } }
        public static bool PausePressed { get { Ensure(); return !Rebinding && Time.frameCount > _rebindEnded + 1 && _pause.WasPressedThisFrame(); } }

        private static bool _rebinding;
        private static int _rebindEnded = -10;

        /// <summary>A key is being set in the options: Esc cancels that, it does not close the menu.</summary>
        public static bool Rebinding
        {
            get => _rebinding;
            set
            {
                if (_rebinding && !value) _rebindEnded = Time.frameCount;
                _rebinding = value;
            }
        }
        public static bool ScoreboardHeld { get { Ensure(); return _score.IsPressed(); } }
        public static bool PushToTalk { get { Ensure(); return _ptt.IsPressed(); } }

        // The caster's keys: past the gameplay lock (the end of a match stops the avatars, not the flight).
        public static float CasterRise { get { Ensure(); return (_casterRise.IsPressed() ? 1f : 0f) - (_casterDescend.IsPressed() ? 1f : 0f); } }
        public static bool CasterFast { get { Ensure(); return _casterFast.IsPressed(); } }
        public static bool CasterModeSwitch { get { Ensure(); return _casterSwitch.WasPressedThisFrame(); } }
        public static bool CasterModeMulti { get { Ensure(); return _casterMulti.WasPressedThisFrame(); } }
        public static bool CasterModeFly { get { Ensure(); return _casterFly.WasPressedThisFrame(); } }
        public static bool CasterNext { get { Ensure(); return _casterNext.WasPressedThisFrame(); } }
        public static bool CasterEyes { get { Ensure(); return _casterEyes.WasPressedThisFrame(); } }
        public static bool CasterClean { get { Ensure(); return _casterClean.WasPressedThisFrame(); } }
        public static bool CasterReset { get { Ensure(); return _casterReset.WasPressedThisFrame(); } }

        public static int SlotPressed
        {
            get
            {
                Ensure();
                if (!On) return -1;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i].WasPressedThisFrame()) return i;
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
