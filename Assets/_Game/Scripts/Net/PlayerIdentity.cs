using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    /// <summary>
    /// The local player's agent name: the nickname typed in the menu when there is one, else the Steam
    /// persona, else a generated name kept for the session (Steam may come up a moment later).
    /// </summary>
    public static class PlayerIdentity
    {
        private const string PREF_NAME = "goobyr.name";
        private static string _fallback;

        /// <summary>True once the player typed a nickname (it then wins over the Steam persona).</summary>
        public static bool HasCustomName => !string.IsNullOrEmpty(PlayerPrefs.GetString(PREF_NAME, ""));

        public static string LocalName
        {
            get
            {
                string lName = PlayerPrefs.GetString(PREF_NAME, "");
                if (!string.IsNullOrEmpty(lName)) return lName;
                if (SteamLobby.Instance != null && SteamLobby.Instance.IsReady) return SteamLobby.Instance.PersonaName;
                if (_fallback == null) _fallback = "Agent " + Random.Range(10, 99);
                return _fallback;
            }
            set
            {
                string lName = string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
                if (lName.Length > 24) lName = lName.Substring(0, 24);
                PlayerPrefs.SetString(PREF_NAME, lName);
                PlayerPrefs.Save();
                // Already in a room: the new name shows for everyone at once.
                if (LobbyPlayer.Local != null && LobbyPlayer.Local.IsSpawned) LobbyPlayer.Local.DisplayName.Value = LocalName;
            }
        }

        /// <summary>The agent's clothes and loadout, remembered between sessions; a pistol, boots and coins the first time.</summary>
        public static AgentLook LocalLook
        {
            get
            {
                if (_look.HasValue) return _look.Value;
                AgentLook lLook = AgentLook.Default;
                string lSaved = PlayerPrefs.GetString("hitmen.look", "");
                string[] lParts = lSaved.Split(',');
                if (lParts.Length == 8)
                {
                    byte.TryParse(lParts[0], out lLook.hat);
                    byte.TryParse(lParts[1], out lLook.skin);
                    byte.TryParse(lParts[2], out lLook.top);
                    byte.TryParse(lParts[3], out lLook.bottom);
                    byte.TryParse(lParts[4], out lLook.shoes);
                    short.TryParse(lParts[5], out lLook.weapon);
                    short.TryParse(lParts[6], out lLook.movement);
                    short.TryParse(lParts[7], out lLook.distraction);
                }
                else
                {
                    lLook.weapon = (short)IndexOf(Data.ItemKind.Pistol);
                    lLook.movement = (short)IndexOf(Data.ItemKind.SpringBoots);
                    lLook.distraction = (short)IndexOf(Data.ItemKind.Coin);
                }
                _look = lLook;
                return lLook;
            }
            set
            {
                _look = value;
                PlayerPrefs.SetString("hitmen.look", string.Join(",", value.hat, value.skin, value.top, value.bottom, value.shoes, value.weapon, value.movement, value.distraction));
                PlayerPrefs.Save();
                if (LobbyPlayer.Local != null && LobbyPlayer.Local.IsSpawned) LobbyPlayer.Local.Look.Value = value;
            }
        }

        private static AgentLook? _look;

        private static int IndexOf(Data.ItemKind pKind)
        {
            Data.ItemDefinition lItem = Data.GameDatabase.Find(pKind);
            return lItem != null ? lItem.Index : -1;
        }

        /// <summary>The agent's chosen icon (see <see cref="UI.UIIcons.Agent"/>), remembered between sessions.</summary>
        public static int LocalIcon
        {
            get => PlayerPrefs.GetInt("goobyr.icon", 0);
            set
            {
                PlayerPrefs.SetInt("goobyr.icon", value);
                PlayerPrefs.Save();
                if (LobbyPlayer.Local != null && LobbyPlayer.Local.IsSpawned) LobbyPlayer.Local.Icon.Value = value;
            }
        }

        /// <summary>
        /// Join (or stay in) rooms as a caster: watch every agent from the multi-feed view, never play.
        /// Sent with the connection request, so a caster may also join a match already running.
        /// </summary>
        public static bool JoinAsCaster
        {
            get => _joinAsCaster;
            set
            {
                _joinAsCaster = value;
                if (LobbyPlayer.Local != null && LobbyPlayer.Local.IsSpawned) LobbyPlayer.Local.Caster.Value = value;
            }
        }
        private static bool _joinAsCaster;

        public static ulong LocalSteamId => SteamLobby.Instance != null && SteamLobby.Instance.IsReady ? SteamLobby.Instance.SteamId : 0;
    }
}
