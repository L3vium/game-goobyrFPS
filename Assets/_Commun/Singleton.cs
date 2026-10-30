using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.Utils
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        private void Awake()
        {
            if (!(Instance == null || Instance.Equals(null)) && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
            OnAwake();
        }

        protected virtual void OnAwake() { }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            Instance = null;
        }
    }
}