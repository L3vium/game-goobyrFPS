using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>How guarded a place is: the social ladder of the map.</summary>
    public enum SecurityTier : byte
    {
        /// <summary>Streets, plazas, the market, the gala floor: you can walk there.</summary>
        Public,
        /// <summary>Staff areas, yards, service corridors: you'll be spotted slower, warned before shot.</summary>
        Restricted,
        /// <summary>Compounds, the VIP floor, the bunker: trespassing, full detection.</summary>
        Hostile
    }

    /// <summary>Who belongs in a zone: carrying the matching pass makes it public for you.</summary>
    /// <remarks>Private = personal rooms (study, suite, vault): no pass ever makes them legal.</remarks>
    public enum AccessLevel : byte { None, Guest, Staff, Security, Private }

    /// <summary>
    /// A box that sets the security tier of everything inside it (the highest tier wins where boxes
    /// overlap; outside every box a map is <see cref="SecurityTier.Hostile"/>). Placed by the map
    /// builders along the design's social-space plan; read by <see cref="DetectionModel.TierFactor"/>.
    /// </summary>
    public class SecurityZone : MonoBehaviour
    {
        [SerializeField] private SecurityTier _tier;
        [SerializeField] private Vector3 _size = new Vector3(20, 10, 20);
        [SerializeField] private AccessLevel _access;

        private static readonly List<SecurityZone> _all = new List<SecurityZone>();
        public static IReadOnlyList<SecurityZone> All => _all;
        /// <summary>Tier of the ground outside every zone (set by the map, hostile by default).</summary>
        public static SecurityTier Outside = SecurityTier.Hostile;

        public SecurityTier Tier => _tier;
        public Vector3 Size => _size;

        public AccessLevel Access => _access;

        public void Setup(SecurityTier pTier, Vector3 pSize, AccessLevel pAccess = AccessLevel.None)
        {
            _tier = pTier;
            _size = pSize;
            _access = pAccess;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public bool Contains(Vector3 pPoint)
        {
            Vector3 lLocal = transform.InverseTransformPoint(pPoint);
            return Mathf.Abs(lLocal.x) <= _size.x * 0.5f && Mathf.Abs(lLocal.z) <= _size.z * 0.5f && lLocal.y >= -2f && lLocal.y <= _size.y;
        }

        /// <summary>The tier at a point: the most guarded zone containing it, else <see cref="Outside"/>.</summary>
        public static SecurityTier At(Vector3 pPoint) => At(pPoint, out _);

        /// <summary>The tier at a point and the access that legitimises it there.</summary>
        public static SecurityTier At(Vector3 pPoint, out AccessLevel pAccess)
        {
            bool lInside = false;
            SecurityTier lTier = SecurityTier.Public;
            pAccess = AccessLevel.None;
            foreach (SecurityZone lZone in _all)
            {
                if (!lZone.Contains(pPoint)) continue;
                if (!lInside || lZone._tier > lTier || (lZone._tier == lTier && lZone._access > pAccess)) { lTier = lZone._tier; pAccess = lZone._access; }
                lInside = true;
            }
            return lInside ? lTier : Outside;
        }

        /// <summary>
        /// The tier that counts for a player: a zone whose access they carry (or better) is public
        /// to them - unless the NPC looking is an enforcer, who sees through passes.
        /// </summary>
        public static SecurityTier ForPlayer(SecurityTier pTier, AccessLevel pRequired, AccessLevel pCarried, bool pEnforcer)
        {
            if (pRequired == AccessLevel.None || pEnforcer || pCarried < pRequired) return pTier;
            return SecurityTier.Public;
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _tier switch { SecurityTier.Public => new Color(0.3f, 0.9f, 0.4f, 0.08f), SecurityTier.Restricted => new Color(1f, 0.8f, 0.2f, 0.08f), _ => new Color(1f, 0.25f, 0.2f, 0.08f) };
            Gizmos.DrawCube(new Vector3(0, _size.y * 0.5f, 0), _size);
        }
    }
}
