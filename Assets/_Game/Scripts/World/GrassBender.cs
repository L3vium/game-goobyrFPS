using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.World
{
    /// <summary>Something that presses the grass where it stands (a walker, a dropped item, a crate).</summary>
    [ExecuteAlways]
    public class GrassBender : MonoBehaviour
    {
        [Tooltip("Radius of the pressed patch (m).")] public float Radius = 0.6f;
        [Range(0f, 1f)] public float Strength = 1f;

        private void OnEnable() => GrassTrample.Add(this);
        private void OnDisable() => GrassTrample.Remove(this);
    }
}
