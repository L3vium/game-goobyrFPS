using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Data
{
    /// <summary>
    /// The one asset the game loads at start (<c>Resources/GameDatabase</c>): rules, every item and
    /// every map, in a fixed order so their index is the same on every machine.
    /// </summary>
    [CreateAssetMenu(menuName = "Goobyr/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public GameRules rules;
        public List<ItemDefinition> items = new List<ItemDefinition>();
        public List<MapDefinition> maps = new List<MapDefinition>();

        private static GameDatabase _instance;

        public static GameDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameDatabase>("GameDatabase");
                    if (_instance != null) _instance.Index();
                }
                return _instance;
            }
        }

        public static GameRules Rules => Instance.rules;
        public static IReadOnlyList<ItemDefinition> Items => Instance.items;
        public static IReadOnlyList<MapDefinition> Maps => Instance.maps;

        public void Index()
        {
            for (int i = 0; i < items.Count; i++) if (items[i] != null) items[i].Index = i;
            for (int i = 0; i < maps.Count; i++) if (maps[i] != null) maps[i].Index = i;
        }

        public static ItemDefinition Item(int pIndex) => pIndex >= 0 && pIndex < Instance.items.Count ? Instance.items[pIndex] : null;

        public static ItemDefinition Find(ItemKind pKind)
        {
            foreach (ItemDefinition lItem in Instance.items) if (lItem != null && lItem.kind == pKind) return lItem;
            return null;
        }

        public static MapDefinition Map(int pIndex) => pIndex >= 0 && pIndex < Instance.maps.Count ? Instance.maps[pIndex] : null;
    }
}
