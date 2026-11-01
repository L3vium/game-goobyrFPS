using System;
using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Data
{
    /// <summary>
    /// The realistic models the game spawns at runtime (weapons, throwables, characters), by name.
    /// Built by <c>Goobyr/Build/Art Registry</c> from <c>Art/PBR/Models</c> and <c>Art/Characters</c>;
    /// living in Resources, it also pulls those models into the player build.
    /// </summary>
    [CreateAssetMenu(menuName = "Goobyr/Art Registry")]
    public class ArtRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public GameObject model;
        }

        public List<Entry> entries = new List<Entry>();

        private static ArtRegistry _instance;
        private Dictionary<string, GameObject> _lookup;

        public static ArtRegistry Instance => _instance != null ? _instance : _instance = Resources.Load<ArtRegistry>("ArtRegistry");

        /// <summary>The model registered under a name, or null.</summary>
        public static GameObject Get(string pName)
        {
            ArtRegistry lRegistry = Instance;
            if (lRegistry == null) return null;
            if (lRegistry._lookup == null)
            {
                lRegistry._lookup = new Dictionary<string, GameObject>();
                foreach (Entry lEntry in lRegistry.entries) if (lEntry.model != null) lRegistry._lookup[lEntry.name] = lEntry.model;
            }
            return lRegistry._lookup.TryGetValue(pName, out GameObject lModel) ? lModel : null;
        }
    }
}
