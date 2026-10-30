using System;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX.Rendering
{
    /// <summary>
    /// Per-map atmosphere, set in each map's volume profile: height fog with drifting noise,
    /// lit by the sun through its shadow map (light shafts between trees and buildings), plus
    /// an ambient tint. Read by <see cref="VolumetricFogFeature"/>.
    /// </summary>
    [Serializable, VolumeComponentMenu("Goobyr/Atmosphere")]
    public class GoobyrAtmosphere : VolumeComponent
    {
        [Tooltip("Fog density at the fog base height (per metre).")]
        public ClampedFloatParameter density = new ClampedFloatParameter(0f, 0f, 0.08f);
        [Tooltip("World height where the fog is densest.")]
        public FloatParameter baseHeight = new FloatParameter(0f);
        [Tooltip("Metres over which the density halves going up.")]
        public ClampedFloatParameter falloff = new ClampedFloatParameter(18f, 1f, 200f);
        [Tooltip("Colour of the light the fog scatters from the sky (ambient).")]
        public ColorParameter ambientColor = new ColorParameter(new Color(0.55f, 0.65f, 0.75f), false, false, true);
        [Tooltip("How strongly the sun lights the fog (shafts).")]
        public ClampedFloatParameter sunScattering = new ClampedFloatParameter(1.2f, 0f, 8f);
        [Tooltip("Forward scattering: 0 = even, 0.8 = strong glow toward the sun.")]
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.6f, 0f, 0.95f);
        [Tooltip("Strength of the drifting noise in the fog.")]
        public ClampedFloatParameter noiseStrength = new ClampedFloatParameter(0.6f, 0f, 1f);
        public ClampedFloatParameter noiseScale = new ClampedFloatParameter(0.03f, 0.001f, 0.3f);
        public Vector3Parameter wind = new Vector3Parameter(new Vector3(1.2f, 0.1f, 0.6f));
        [Tooltip("Ray length in metres (beyond, the scene's own fog takes over).")]
        public ClampedFloatParameter maxDistance = new ClampedFloatParameter(220f, 20f, 1000f);

        public bool IsActive() => density.value > 0f;
    }
}
