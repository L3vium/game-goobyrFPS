using Com.IsartDigital.GoobyrFPS.Player;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The options' key list: one row per action, click its key and press another. Bindings are the
    /// Input System's own overrides, saved by <see cref="GameInput.SaveBindings"/>.
    /// </summary>
    public static class KeyBindings
    {
        private static readonly (string label, string map, string action, string part)[] PLAYER =
        {
            ("Forward", "Player", "Move", "up"), ("Back", "Player", "Move", "down"), ("Left", "Player", "Move", "left"), ("Right", "Player", "Move", "right"),
            ("Jump", "Player", "Jump", null), ("Run", "Player", "Sprint", null), ("Crouch / slide", "Player", "Crouch", null),
            ("Fire", "Player", "Fire", null), ("Aim", "Player", "Aim", null), ("Reload", "Player", "Reload", null),
            ("Interact", "Player", "Interact", null), ("Takedown", "Player", "Takedown", null), ("Punch", "Player", "Melee", null),
            ("Slot 1", "Player", "Slot1", null), ("Slot 2", "Player", "Slot2", null), ("Slot 3", "Player", "Slot3", null), ("Slot 4", "Player", "Slot4", null),
            ("Instinct", "Player", "Instinct", null), ("Zoom", "Player", "Zoom", null), ("Detonate", "Player", "Detonate", null),
            ("Lean left", "Player", "LeanLeft", null), ("Lean right", "Player", "LeanRight", null),
            ("Map", "Player", "Map", null), ("Scoreboard", "Player", "Scoreboard", null), ("Talk", "Player", "PushToTalk", null)
        };

        private static readonly (string label, string map, string action, string part)[] CASTER =
        {
            ("Rise", "Caster", "Rise", null), ("Descend", "Caster", "Descend", null), ("Fly faster", "Caster", "Fast", null),
            ("Switch view", "Caster", "ModeSwitch", null), ("Multi view", "Caster", "ModeMulti", null), ("Fly view", "Caster", "ModeFly", null),
            ("Next feed", "Caster", "NextFeed", null), ("Through the eyes", "Caster", "Eyes", null), ("Clean screen", "Caster", "Clean", null), ("Reset camera", "Caster", "ResetCamera", null)
        };

        private static InputActionRebindingExtensions.RebindingOperation _operation;
        private static readonly List<System.Action> _refreshers = new List<System.Action>();

        public static void Build(RectTransform pList)
        {
            _refreshers.Clear();
            UIWidgets.Section(pList, "KEYS");
            foreach (var lEntry in PLAYER) Row(pList, lEntry.label, lEntry.map, lEntry.action, lEntry.part);
            UIWidgets.Section(pList, "CASTER KEYS");
            foreach (var lEntry in CASTER) Row(pList, lEntry.label, lEntry.map, lEntry.action, lEntry.part);

            RectTransform lReset = UIWidgets.Row(pList, "Reset every key");
            Button lButton = UIFactory.Button(lReset, "RESET", new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(200, 40), UIFactory.Accent2, () =>
            {
                GameInput.ResetBindings();
                foreach (System.Action lRefresh in _refreshers) lRefresh();
            }, 20);
            ((RectTransform)lButton.transform).pivot = new Vector2(1, 0.5f);
            Centre(lButton, 200f);
        }

        private static void Row(RectTransform pList, string pLabel, string pMap, string pAction, string pPart)
        {
            InputAction lAction = InputSystem.actions.FindAction(pMap + "/" + pAction);
            if (lAction == null) return;
            int lIndex = BindingIndex(lAction, pPart);
            if (lIndex < 0) return;

            RectTransform lRow = UIWidgets.Row(pList, pLabel);
            TextMeshProUGUI lKey = null;
            Button lButton = UIFactory.Button(lRow, "KEY", new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(200, 40), UIFactory.Accent2, () => Listen(lAction, lIndex, lKey), 20);
            ((RectTransform)lButton.transform).pivot = new Vector2(1, 0.5f);
            lButton.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
            lKey = Centre(lButton, 200f);
            void Refresh() => lKey.text = KeyText(lAction, lIndex);
            _refreshers.Add(Refresh);
            Refresh();
        }

        /// <summary>The keyboard or mouse binding of an action (a named part of it for the move keys); an empty one is free to set.</summary>
        private static int BindingIndex(InputAction pAction, string pPart)
        {
            for (int i = 0; i < pAction.bindings.Count; i++)
            {
                InputBinding lBinding = pAction.bindings[i];
                if (lBinding.isComposite) continue;
                if (pPart != null && (!lBinding.isPartOfComposite || !string.Equals(lBinding.name, pPart, System.StringComparison.OrdinalIgnoreCase))) continue;
                if (pPart == null && lBinding.isPartOfComposite) continue;
                string lPath = lBinding.path ?? "";
                if (lPath.Length == 0 || lPath.StartsWith("<Keyboard>") || lPath.StartsWith("<Mouse>")) return i;
            }
            return -1;
        }

        private static string KeyText(InputAction pAction, int pIndex)
        {
            string lText = pAction.GetBindingDisplayString(pIndex);
            return string.IsNullOrEmpty(lText) ? "-" : lText.ToUpperInvariant();
        }

        private static TextMeshProUGUI Centre(Button pButton, float pWidth)
        {
            TextMeshProUGUI lLabel = pButton.GetComponentInChildren<TextMeshProUGUI>();
            lLabel.alignment = TextAlignmentOptions.Center;
            lLabel.rectTransform.sizeDelta = new Vector2(pWidth, 40);
            lLabel.rectTransform.anchoredPosition = Vector2.zero;
            return lLabel;
        }

        private static void Listen(InputAction pAction, int pIndex, TextMeshProUGUI pKey)
        {
            _operation?.Cancel();
            pKey.text = "PRESS A KEY";
            GameInput.Rebinding = true;
            bool lWasOn = pAction.enabled;
            pAction.Disable();
            void Done(InputActionRebindingExtensions.RebindingOperation pOperation)
            {
                pOperation.Dispose();
                if (_operation == pOperation) _operation = null;
                if (lWasOn) pAction.Enable();
                GameInput.Rebinding = false;
                GameInput.SaveBindings();
                pKey.text = KeyText(pAction, pIndex);
            }
            _operation = pAction.PerformInteractiveRebinding(pIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(Done)
                .OnCancel(Done)
                .Start();
        }
    }
}
