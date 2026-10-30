using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    /// <summary>The local player's name: the Steam persona when Steam runs, a saved nickname otherwise.</summary>
    public static class PlayerIdentity
    {
        private const string PREF_NAME = "goobyr.name";

        public static string LocalName
        {
            get
            {
                if (SteamLobby.Instance != null && SteamLobby.Instance.IsReady) return SteamLobby.Instance.PersonaName;
                string lName = PlayerPrefs.GetString(PREF_NAME, "");
                if (string.IsNullOrEmpty(lName)) lName = "Goob" + Random.Range(100, 999);
                return lName;
            }
            set => PlayerPrefs.SetString(PREF_NAME, value);
        }

        public static ulong LocalSteamId => SteamLobby.Instance != null && SteamLobby.Instance.IsReady ? SteamLobby.Instance.SteamId : 0;
    }
}
