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

        [Header("Atmosphere")]
        public Color hazeColor = new Color(1f, 0.86f, 0.65f);
        public Color cloudShade = new Color(0.58f, 0.64f, 0.74f);
        [Range(0f, 1f)] public float cloudCoverage = 0.48f;
        [Tooltip("Volumetric fog density at its base (0 = none).")]
        public float volumetricDensity = 0.012f;
        public float volumetricHeight = 0f;
        public float volumetricFalloff = 22f;
        public Color volumetricAmbient = new Color(0.45f, 0.55f, 0.65f);
        public float sunScattering = 1.4f;

        [Header("Grading")]
        public float temperature = 6f;
        public float tint = 0f;
        public float saturation = 8f;
        public float contrast = 12f;
        public float postExposure = 0.1f;
        public Color shadowsTint = new Color(0.9f, 0.95f, 1.08f);
        public Color highlightsTint = new Color(1.06f, 1.02f, 0.94f);

        [Header("Plan (baked by the map build)")]
        [Tooltip("The stylized plan per floor: ground first, then a storey up each.")]
        public Texture2D[] planFloors;
        /// <summary>World XZ of the plan's bottom-left corner, and its side in metres.</summary>
        public Vector2 planOrigin;
        public float planSize = 500f;
        public float planStorey = 3.4f;

        [Header("Targets")]
        public List<TargetProfile> targets = new List<TargetProfile>();

        [System.NonSerialized] public int Index = -1;
    }
}
