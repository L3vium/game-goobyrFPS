using System;
using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Data
{
    public enum ActivityType
    {
        Idle,
        Drink,
        Toilet,
        Office,
        Garden,
        Dining,
        Phone,
        Smoke,
        Inspect,
        Lookout,
        Workout,
        Speech
    }

    public enum ProofKind
    {
        Head,
        Ring,
        Briefcase,
        Keycard,
        Badge,
        Ledger
    }

    public enum HatStyle
    {
        None,
        Fedora,
        Cap,
        Beret,
        Crown,
        Cowboy,
        Turban,
        Beanie,
        TopHat
    }

    /// <summary>
    /// One possible target. Every map keeps fifteen; the host draws one at the start of a match.
    /// The routine lists the activity types the target cycles through - the map builder places
    /// <c>ActivityNode</c>s of those types in the target's zone.
    /// </summary>
    [Serializable]
    public class TargetProfile
    {
        public string displayName = "Target";
        [TextArea] public string dossier = "";
        [Tooltip("Index of the map zone the target lives in (0 town, 1 lair, 2 mansion).")]
        public int zone;
        public Color bodyColor = new Color(0.85f, 0.3f, 0.3f);
        public HatStyle hat = HatStyle.Fedora;
        public float scale = 1.1f;
        public int bodyguards = 2;
        public ProofKind proof = ProofKind.Head;
        public List<ActivityType> routine = new List<ActivityType>();
        public bool enabled = true;
    }
}
