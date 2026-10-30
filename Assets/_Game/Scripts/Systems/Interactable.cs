using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// Anything in a map the player can use with the interact key: breakers, chandelier
    /// levers, drinks, vents, distractions, hiding spots, ziplines, alarms.
    /// <para>
    /// Interactables are plain scene objects, not NetworkObjects: every machine loads the same map,
    /// so <see cref="WorldRegistry"/> numbers them in the same order everywhere and the
    /// <c>MapSession</c> carries "use #12" to the host and "#12 is now state 1" back to everyone.
    /// </para>
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] protected string _prompt = "USE";
        [SerializeField] protected float _range = 2.6f;

        public int Id { get; set; } = -1;
        public int State { get; private set; }
        public float Range => _range;

        public virtual string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => _prompt;

        /// <summary>False hides the prompt (already used, wrong item...).</summary>
        public virtual bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => true;

        /// <summary>
        /// Owner side, before anything is sent. Return true when the interaction is purely local
        /// (movement: ziplines, hiding spots) and nothing must go to the host.
        /// </summary>
        public virtual bool InteractLocal(PlayerAvatar pWho) => false;

        /// <summary>Host side: the rules of the interaction. <paramref name="pHeldItem"/> is the held item index.</summary>
        public virtual void InteractServer(PlayerAvatar pWho, int pHeldItem) { }

        /// <summary>Host side, for systems (AI flipping a breaker back, fire, timers).</summary>
        public void SetStateServer(int pState)
        {
            if (MapSession.Instance != null) MapSession.Instance.BroadcastState(Id, pState);
            else ApplyStateInternal(pState);
        }

        /// <summary>Every machine, when the host changes the state.</summary>
        public void ApplyStateInternal(int pState)
        {
            int lOld = State;
            State = pState;
            OnStateChanged(lOld, pState);
        }

        protected virtual void OnStateChanged(int pOld, int pNew) { }

        /// <summary>Host side: a one-shot effect on every machine (a sound, a burst of particles).</summary>
        public void PlayEffectServer(int pEffect)
        {
            if (MapSession.Instance != null) MapSession.Instance.BroadcastEffect(Id, pEffect);
            else OnEffect(pEffect);
        }

        public virtual void OnEffect(int pEffect) { }

        public Vector3 InteractPoint => transform.position;
    }

    /// <summary>
    /// Numbers every interactable of the loaded map the same way on every machine: sorted by
    /// position (maps are generated, so positions are identical), then by name.
    /// </summary>
    public static class WorldRegistry
    {
        private static readonly List<Interactable> _all = new List<Interactable>();

        public static IReadOnlyList<Interactable> All => _all;

        public static void Build()
        {
            _all.Clear();
            _all.AddRange(Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            _all.Sort((pA, pB) =>
            {
                Vector3 a = pA.transform.position, b = pB.transform.position;
                int lCompare = Mathf.RoundToInt(a.x * 100f).CompareTo(Mathf.RoundToInt(b.x * 100f));
                if (lCompare != 0) return lCompare;
                lCompare = Mathf.RoundToInt(a.z * 100f).CompareTo(Mathf.RoundToInt(b.z * 100f));
                if (lCompare != 0) return lCompare;
                lCompare = Mathf.RoundToInt(a.y * 100f).CompareTo(Mathf.RoundToInt(b.y * 100f));
                if (lCompare != 0) return lCompare;
                return string.CompareOrdinal(pA.name, pB.name);
            });
            for (int i = 0; i < _all.Count; i++) _all[i].Id = i;
        }

        public static Interactable Get(int pId) => pId >= 0 && pId < _all.Count ? _all[pId] : null;

        public static T Nearest<T>(Vector3 pPosition, float pMaxDistance, System.Func<T, bool> pFilter = null) where T : Interactable
        {
            T lBest = null;
            float lBestDistance = pMaxDistance * pMaxDistance;
            foreach (Interactable lInteractable in _all)
            {
                if (!(lInteractable is T lTyped)) continue;
                if (pFilter != null && !pFilter(lTyped)) continue;
                float lDistance = (lTyped.transform.position - pPosition).sqrMagnitude;
                if (lDistance < lBestDistance) { lBestDistance = lDistance; lBest = lTyped; }
            }
            return lBest;
        }

        public static void Clear() => _all.Clear();
    }
}
