using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Data
{
    /// <summary>
    /// A playable map: its scene, its look, its zones and its fifteen targets.
    /// </summary>
    [CreateAssetMenu(menuName = "Goobyr/Map Definition")]
    public class MapDefinition : ScriptableObject
    {
        public string displayName = "Map";
        public string sceneName = "Map";
        [TextArea] public string description = "";
        public string[] zoneNames = { "Town", "Lair", "Mansion" };

        [Header("Look")]
        public Color skyTop = new Color(0.45f, 0.78f, 0.9f);
        public Color skyHorizon = new Color(0.85f, 0.95f, 0.95f);
        public Color fog = new Color(0.8f, 0.9f, 0.92f);
        public Color sun = new Color(1f, 0.95f, 0.85f);
        public Color ambient = new Color(0.55f, 0.6f, 0.68f);
        public float sunIntensity = 1.3f;
        public Vector3 sunEuler = new Vector3(48f, -35f, 0f);
        public float fogDensity = 0.004f;

        [Header("Targets")]
        public List<TargetProfile> targets = new List<TargetProfile>();

        [System.NonSerialized] public int Index = -1;
    }
}
