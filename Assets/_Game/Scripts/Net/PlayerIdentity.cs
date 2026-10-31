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

        public static ulong LocalSteamId => SteamLobby.Instance != null && SteamLobby.Instance.IsReady ? SteamLobby.Instance.SteamId : 0;
    }
}
