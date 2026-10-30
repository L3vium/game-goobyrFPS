using Com.IsartDigital.GoobyrFPS.Audio;
using Com.IsartDigital.GoobyrFPS.FX;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// World-space effects the host triggers for everyone: explosions, smoke clouds, global alarm
    /// sirens, gas. Rides on the MapSession object.
    /// </summary>
    public class MapEffects : NetworkBehaviour
    {
        public static MapEffects Instance { get; private set; }

        public override void OnNetworkSpawn() => Instance = this;

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        [Rpc(SendTo.Everyone)]
        public void ExplosionRpc(Vector3 pPosition, float pRadius)
        {
            Effects.Explosion(pPosition, pRadius);
            Juice.ShakeAt(pPosition, 0.9f, pRadius * 7f);
            Sfx.Play("explosion", pPosition, 1f, 220f, 0.15f);
        }

        [Rpc(SendTo.Everyone)]
        public void SmokeRpc(Vector3 pPosition, float pRadius, float pDuration)
        {
            Effects.SmokeCloud(pPosition, pRadius, pDuration);
            Sfx.Play("smoke_pop", pPosition, 1f, 40f);
        }

        [Rpc(SendTo.Everyone)]
        public void SoundRpc(string pName, Vector3 pPosition, float pRange)
        {
            Sfx.Play(pName, pPosition, 1f, pRange);
        }

        [Rpc(SendTo.Everyone)]
        public void BurnRpc(NetworkObjectReference pTarget, bool pOn)
        {
            if (pTarget.TryGet(out NetworkObject lObject)) BurnVisuals.Apply(lObject, pOn);
        }

        [Rpc(SendTo.Everyone)]
        public void ToastRpc(string pMessage)
        {
            UI.Hud.Toast(pMessage);
        }
    }
}
