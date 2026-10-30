using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// The network side of a loaded map: carries interactions to the host and state changes and
    /// one-shot effects back to everyone (see <see cref="Interactable"/>). Spawned by the host when
    /// the map scene is loaded everywhere; the <c>MatchDirector</c> rides on the same object.
    /// </summary>
    public class MapSession : NetworkBehaviour
    {
        public static MapSession Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            WorldRegistry.Build();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            WorldRegistry.Clear();
        }

        /// <summary>Host side: validates and runs an interaction asked by a player.</summary>
        public void HandleInteract(PlayerAvatar pWho, int pId, int pHeldItem)
        {
            if (!IsServer) return;
            Interactable lInteractable = WorldRegistry.Get(pId);
            if (lInteractable == null || pWho == null || !pWho.Health.IsAlive) return;
            if (Vector3.Distance(pWho.transform.position + Vector3.up, lInteractable.InteractPoint) > lInteractable.Range + 2.5f) return;
            if (!lInteractable.CanInteract(pWho, Data.GameDatabase.Item(pHeldItem))) return;
            lInteractable.InteractServer(pWho, pHeldItem);
        }

        public void BroadcastState(int pId, int pState)
        {
            if (!IsServer) return;
            StateRpc(pId, pState);
        }

        public void BroadcastEffect(int pId, int pEffect)
        {
            if (!IsServer) return;
            EffectRpc(pId, pEffect);
        }

        [Rpc(SendTo.Everyone)]
        private void StateRpc(int pId, int pState) => WorldRegistry.Get(pId)?.ApplyStateInternal(pState);

        [Rpc(SendTo.Everyone)]
        private void EffectRpc(int pId, int pEffect) => WorldRegistry.Get(pId)?.OnEffect(pEffect);
    }
}
