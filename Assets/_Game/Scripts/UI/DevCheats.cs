using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The cheats of the Hitmen Dev build (or of any build whose <see cref="GameRules.cheatsEnabled"/>
    /// is ticked): F1 to F10 on a living agent, the list shown bottom right. What touches the match
    /// goes through the host, which refuses it when cheats are off on its side.
    /// </summary>
    public class DevCheats : MonoBehaviour
    {
        /// <summary>Are cheats on in this build?</summary>
        public static bool Allowed
        {
            get
            {
#if HITMEN_DEV
                return true;
#else
                return GameDatabase.Rules != null && GameDatabase.Rules.cheatsEnabled;
#endif
            }
        }

        private static readonly string[] NAMES =
        {
            "INVINCIBLE", "FLY", "SUPER SPEED", "ANY WEAPON", "INFINITE AMMO", "INVISIBLE TO AI", "SEE ENEMIES", "KILL ENEMIES NEARBY", "GO TO THE TARGET", "FREEZE THE CLOCK"
        };

        private const int WEAPONS = 3, KILL = 7, TARGET = 8;
        private const float KILL_RADIUS = 30f;

        private readonly bool[] _on = new bool[NAMES.Length];
        private TextMeshProUGUI _list;
        private GameObject _menu;

        public void Build(RectTransform pRoot)
        {
            _list = UIFactory.Text(pRoot, "DevCheats", "", 16, UIFactory.Paper, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(-30, 240), new Vector2(360, 280));
            _list.rectTransform.pivot = new Vector2(1, 0);
            _list.font = UIFactory.Heading;
            Refresh();
        }

        private void Update()
        {
            PlayerAvatar lMe = PlayerAvatar.Local;
            bool lUsable = lMe != null && lMe.Health.IsAlive && !CasterView.Active;
            _list.enabled = lUsable;
            if (!lUsable) { CloseMenu(); return; }
            Keyboard lKeys = Keyboard.current;
            if (lKeys == null) return;
            for (int i = 0; i < NAMES.Length; i++)
                if (lKeys[Key.F1 + i].wasPressedThisFrame) Press(i, lMe);
        }

        private void Press(int pCheat, PlayerAvatar pMe)
        {
            if (pCheat == WEAPONS) { ToggleMenu(pMe); return; }
            if (pCheat == KILL) { pMe.DevKillNearRpc(KILL_RADIUS); return; }
            if (pCheat == TARGET) { pMe.DevToTargetRpc(); return; }
            _on[pCheat] = !_on[pCheat];
            bool lOn = _on[pCheat];
            switch (pCheat)
            {
                case 0: pMe.DevSetFlagRpc((int)DevFlag.Invincible, lOn); break;
                case 1: pMe.Motor.DevFly = lOn; break;
                case 2: pMe.Motor.DevSpeed = lOn ? 3f : 1f; break;
                case 4: pMe.DevSetFlagRpc((int)DevFlag.InfiniteAmmo, lOn); break;
                case 5: pMe.DevSetFlagRpc((int)DevFlag.Ghost, lOn); break;
                case 6: InstinctVision.RevealAll = lOn; break;
                case 9: pMe.DevFreezeClockRpc(lOn); break;
            }
            Refresh();
        }

        private void Refresh()
        {
            System.Text.StringBuilder lBuilder = new System.Text.StringBuilder();
            for (int i = 0; i < NAMES.Length; i++)
            {
                string lColor = _on[i] ? ColorUtility.ToHtmlStringRGB(UIFactory.Accent) : "D8D8D8";
                lBuilder.Append("<color=#").Append(lColor).Append(">F").Append(i + 1).Append("  ").Append(NAMES[i]).Append("</color>\n");
            }
            _list.text = lBuilder.ToString();
        }

        // ---------------------------------------------------------------- the weapon list

        private void ToggleMenu(PlayerAvatar pMe)
        {
            if (_menu != null) { CloseMenu(); return; }
            RectTransform lPanel = UIFactory.Panel(_list.canvas.transform, "DevWeapons", new Color(0.05f, 0.055f, 0.065f, 0.97f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 760)).rectTransform;
            _menu = lPanel.gameObject;
            _menu.GetComponent<Image>().raycastTarget = true;
            UIFactory.Title(lPanel, "Title", "ANY WEAPON", 26, UIFactory.Paper, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -32), new Vector2(420, 36), true, 4f);
            RectTransform lList = UIWidgets.ScrollList(lPanel, "Items", new Vector2(0.5f, 1), new Vector2(0, -64), 420f, 680f);
            List<ItemDefinition> lItems = GameDatabase.Instance.items;
            foreach (ItemDefinition lItem in lItems)
            {
                if (lItem == null || lItem.IsKey) continue;
                ItemDefinition lChosen = lItem;
                RectTransform lRow = UIWidgets.Row(lList, lItem.name, 46f, false);
                Button lButton = UIFactory.Button(lRow, lItem.displayName.ToUpperInvariant(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 42), UIFactory.Accent2, () =>
                {
                    pMe.DevGiveRpc(lChosen.Index);
                    CloseMenu();
                }, 20);
            }
            GameInput.GameplayEnabled = false;
            GameInput.LockCursor(false);
        }

        private void CloseMenu()
        {
            if (_menu == null) return;
            Destroy(_menu);
            _menu = null;
            GameInput.GameplayEnabled = !PauseMenu.IsOpen && !EndScreen.IsShown;
            GameInput.LockCursor(!PauseMenu.IsOpen && !CasterView.Active && !EndScreen.IsShown);
        }
    }
}
