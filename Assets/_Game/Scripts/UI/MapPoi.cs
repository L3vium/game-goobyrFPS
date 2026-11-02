using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// A point of interest drawn on the pause map, placed by the map builders: landmarks, the ways
    /// into a building (service door, vent, drainpipe, cave mouth), overlooks. Purely visual.
    /// </summary>
    public class MapPoi : MonoBehaviour
    {
        public enum Kind : byte { Landmark, Entrance, Vent, Climb, Underground, Overlook, Shortcut }

        [SerializeField] private Kind _kind;
        [SerializeField] private string _label;

        private static readonly List<MapPoi> _all = new List<MapPoi>();
        public static IReadOnlyList<MapPoi> All => _all;

        public Kind Type => _kind;
        public string Label => _label;

        public void Setup(Kind pKind, string pLabel)
        {
            _kind = pKind;
            _label = pLabel;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static Color ColorOf(Kind pKind) => pKind switch
        {
            Kind.Landmark => new Color(1f, 1f, 1f, 0.95f),
            Kind.Entrance => new Color(0.55f, 0.85f, 1f),
            Kind.Vent => new Color(0.75f, 0.75f, 0.8f),
            Kind.Climb => new Color(0.6f, 1f, 0.6f),
            Kind.Underground => new Color(0.85f, 0.7f, 0.5f),
            Kind.Overlook => new Color(1f, 0.85f, 0.4f),
            _ => new Color(0.9f, 0.6f, 1f)
        };
    }
}
