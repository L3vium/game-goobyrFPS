using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// Reads the current map's baked plans (<see cref="MapDefinition.planFloors"/>): which floor a
    /// world point is on (storeys above the ground under it) and where it falls on the plan (0..1).
    /// </summary>
    public static class MapPlan
    {
        public static MapDefinition Map => Net.LobbyState.Instance != null ? Net.LobbyState.Instance.Map : null;
        public static bool Ready => Map != null && Map.planFloors != null && Map.planFloors.Length > 0 && Map.planFloors[0] != null;
        public static int FloorCount => Ready ? Map.planFloors.Length : 0;

        public static int FloorAt(Vector3 pWorld)
        {
            if (!Ready) return 0;
            Terrain lLand = Terrain.activeTerrain;
            float lGround = lLand != null ? lLand.SampleHeight(pWorld) + lLand.transform.position.y : 0f;
            return Mathf.Clamp(Mathf.FloorToInt((pWorld.y - lGround + 0.6f) / Map.planStorey), 0, Map.planFloors.Length - 1);
        }

        public static Texture2D Floor(int pFloor) => Ready ? Map.planFloors[Mathf.Clamp(pFloor, 0, Map.planFloors.Length - 1)] : null;

        /// <summary>A world point on the plan, 0..1 on both axes (north = +Z = up).</summary>
        public static Vector2 ToUv(Vector3 pWorld)
        {
            if (!Ready) return new Vector2(0.5f, 0.5f);
            return new Vector2((pWorld.x - Map.planOrigin.x) / Map.planSize, (pWorld.z - Map.planOrigin.y) / Map.planSize);
        }

        public static float MetresToUv(float pMetres) => Ready ? pMetres / Map.planSize : 0f;
    }
}
