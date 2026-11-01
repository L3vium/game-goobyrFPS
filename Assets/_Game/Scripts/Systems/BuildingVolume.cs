using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// The inside of a building (hotel tower, mansion, lodge): a named box the systems act on -
    /// the power cut darkens it, the ventilation poison fills it.
    /// </summary>
    public class BuildingVolume : MonoBehaviour
    {
        [SerializeField] private string _buildingName = "Building";
        [SerializeField] private Vector3 _size = new Vector3(30, 12, 30);

        private static readonly List<BuildingVolume> _all = new List<BuildingVolume>();
        public static IReadOnlyList<BuildingVolume> All => _all;

        public string BuildingName => _buildingName;
        /// <summary>Footprint and height (the volume stands on its transform, 4 m of slack below).</summary>
        public Vector3 Size => _size;
        public bool PowerOn { get; set; } = true;
        public bool Gassed { get; set; }

        public void Setup(string pName, Vector3 pSize)
        {
            _buildingName = pName;
            _size = pSize;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public bool Contains(Vector3 pPoint)
        {
            Vector3 lLocal = transform.InverseTransformPoint(pPoint);
            return Mathf.Abs(lLocal.x) <= _size.x * 0.5f && lLocal.y >= -4f && lLocal.y <= _size.y && Mathf.Abs(lLocal.z) <= _size.z * 0.5f;
        }

        public static BuildingVolume At(Vector3 pPoint)
        {
            foreach (BuildingVolume lBuilding in _all) if (lBuilding.Contains(pPoint)) return lBuilding;
            return null;
        }

        /// <summary>How much an NPC sees at this point: dark buildings cut vision.</summary>
        public static float LightFactor(Vector3 pPoint)
        {
            BuildingVolume lBuilding = At(pPoint);
            return lBuilding != null && !lBuilding.PowerOn ? 0.4f : 1f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.15f);
            Gizmos.DrawCube(new Vector3(0, _size.y * 0.5f - 2f, 0), _size + new Vector3(0, 4f, 0));
        }
    }
}
