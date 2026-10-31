using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Audio
{
    /// <summary>
    /// Footsteps for anything that walks (players and NPCs): a step every stride, its sound picked
    /// from the surface underfoot - the terrain's painted layer (grass, dirt, stone, sand) or the
    /// piece stepped on (wood, metal) - and water when wading or swimming. Louder running, quiet
    /// crouched; only played near the listener.
    /// </summary>
    public class Footsteps : MonoBehaviour
    {
        private const float HEAR = 32f;

        private Vector3 _last;
        private float _distance;
        private System.Func<float> _loudness;
        private System.Func<bool> _swimming;

        /// <summary><paramref name="pLoudness"/>: 0 (silent) .. 1 (running); <paramref name="pSwimming"/>: in deep water.</summary>
        public void Init(System.Func<float> pLoudness, System.Func<bool> pSwimming = null)
        {
            _loudness = pLoudness;
            _swimming = pSwimming;
            _last = transform.position;
        }

        private void Update()
        {
            Vector3 lPosition = transform.position;
            Vector3 lStep = lPosition - _last;
            _last = lPosition;
            Camera lCamera = CameraRig.Instance != null ? CameraRig.Instance.WorldCamera : null;
            if (lCamera == null || (lCamera.transform.position - lPosition).sqrMagnitude > HEAR * HEAR) return;
            float lLoud = _loudness != null ? _loudness() : 0.5f;
            if (lLoud <= 0f) { _distance = 0f; return; }

            bool lSwim = _swimming != null && _swimming();
            lStep.y = 0f;
            float lMoved = lStep.magnitude;
            if (lMoved > 1.5f) return; // a teleport
            _distance += lMoved;
            float lStride = lSwim ? 1.6f : Mathf.Lerp(0.65f, 1.05f, lLoud);
            if (_distance < lStride) return;
            _distance = 0f;
            string lSound = lSwim ? "swim" : "step_" + SurfaceAt(lPosition);
            Sfx.Play(lSound, lPosition + Vector3.up * 0.05f, Mathf.Lerp(0.25f, 0.9f, lLoud), Mathf.Lerp(10f, 30f, lLoud), 0.1f);
        }

        /// <summary>The surface name under a point: water, grass, dirt, stone, sand, wood or metal.</summary>
        public static string SurfaceAt(Vector3 pPoint)
        {
            if (WaterVolume.At(pPoint + Vector3.up * 0.15f, out _) != null) return "water";
            if (!Physics.Raycast(pPoint + Vector3.up * 0.5f, Vector3.down, out RaycastHit lHit, 1.6f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) return "dirt";
            if (lHit.collider is TerrainCollider lTerrainCollider) return TerrainSurface(lTerrainCollider.GetComponent<Terrain>(), lHit.point);
            string lName = lHit.collider.name;
            if (lName.Contains("Pier") || lName.Contains("Plank") || lName.Contains("Deck") || lName.Contains("Wood") || lName.Contains("Stair") || lName.Contains("Barn") || lName.Contains("Boat")) return "wood";
            if (lName.Contains("Container") || lName.Contains("Metal") || lName.Contains("Crane") || lName.Contains("Catwalk") || lName.Contains("Scaffold") || lName.Contains("Car") || lName.Contains("Truck")) return "metal";
            return "stone";
        }

        private static string TerrainSurface(Terrain pTerrain, Vector3 pPoint)
        {
            if (pTerrain == null) return "dirt";
            TerrainData lData = pTerrain.terrainData;
            Vector3 lLocal = pPoint - pTerrain.transform.position;
            int x = Mathf.Clamp((int)(lLocal.x / lData.size.x * lData.alphamapWidth), 0, lData.alphamapWidth - 1);
            int z = Mathf.Clamp((int)(lLocal.z / lData.size.z * lData.alphamapHeight), 0, lData.alphamapHeight - 1);
            float[,,] lWeights = lData.GetAlphamaps(x, z, 1, 1);
            int lBest = 0;
            for (int l = 1; l < lWeights.GetLength(2); l++) if (lWeights[0, 0, l] > lWeights[0, 0, lBest]) lBest = l;
            string lLayer = lBest < lData.terrainLayers.Length && lData.terrainLayers[lBest] != null ? lData.terrainLayers[lBest].name : "";
            if (lLayer.Contains("Sand")) return "sand";
            if (lLayer.Contains("Asphalt") || lLayer.Contains("Cobble") || lLayer.Contains("Rock")) return "stone";
            if (lLayer.Contains("Dirt") || lLayer.Contains("Forest")) return "dirt";
            return "grass";
        }
    }
}
