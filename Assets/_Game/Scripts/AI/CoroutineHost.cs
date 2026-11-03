using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// A bare component to run coroutines on: the editor's AI test tools (AI Scenarios, the San
    /// Lázaro showcase) drive play mode through it, since a script from an Editor folder can't be a
    /// component.
    /// </summary>
    public class CoroutineHost : MonoBehaviour
    {
        public static CoroutineHost Create(string pName) => new GameObject(pName).AddComponent<CoroutineHost>();
    }
}
