using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// Keeps the cursor locked in the game window while <see cref="GameInput.CursorWanted"/>: the OS
    /// releases it on alt-tab, on a click outside or when focus comes back, so the lock is re-applied
    /// every frame the window has focus and the moment it gets it back.
    /// </summary>
    public class CursorKeeper : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject lObject = new GameObject("CursorKeeper");
            DontDestroyOnLoad(lObject);
            lObject.AddComponent<CursorKeeper>();
        }

        private void Update()
        {
            if (Application.isFocused) GameInput.ApplyCursor();
        }

        private void OnApplicationFocus(bool pFocused)
        {
            if (pFocused) GameInput.ApplyCursor();
        }
    }
}
