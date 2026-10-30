using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>A point where the match can start (players spawn around it) or end (extraction).</summary>
    public class MatchPoint : MonoBehaviour
    {
        public enum Kind { Start, Extraction, WeaponSpawn, UniqueWeaponSpawn, GadgetSpawn }

        [SerializeField] private Kind _kind;
        [SerializeField] private string _label = "";
        [SerializeField] private ItemKind _item = ItemKind.Smg;

        private static readonly List<MatchPoint> _all = new List<MatchPoint>();
        public static IReadOnlyList<MatchPoint> All => _all;

        public Kind PointKind => _kind;
        public string Label => _label;
        public ItemKind Item => _item;

        public void Setup(Kind pKind, string pLabel, ItemKind pItem = ItemKind.Smg)
        {
            _kind = pKind;
            _label = pLabel;
            _item = pItem;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static List<MatchPoint> OfKind(Kind pKind)
        {
            List<MatchPoint> lList = new List<MatchPoint>();
            foreach (MatchPoint lPoint in _all) if (lPoint._kind == pKind) lList.Add(lPoint);
            lList.Sort((a, b) => string.CompareOrdinal(a.name + a.transform.position, b.name + b.transform.position));
            return lList;
        }
    }
}
