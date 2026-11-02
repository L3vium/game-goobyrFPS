using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// The physics side of the NPCs' senses (host side): lines of sight that see through window
    /// glass, and the walls counted between a sound and an ear - only structural ones (building
    /// shells, floors, doors, terrain, big rocks), never a crate, a fence, a tree or a pane of glass
    /// (<see cref="NpcRules.IsStructural"/>). Each collider's verdict is cached.
    /// </summary>
    public static class Senses
    {
        private static readonly Dictionary<int, bool> _seeThrough = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> _structural = new Dictionary<int, bool>();
        private static int _glassLayer = -2;
        private const int MAX_STEPS = 10;
        private const float STEP = 0.02f;

        private static int GlassLayer => _glassLayer != -2 ? _glassLayer : _glassLayer = LayerMask.NameToLayer("Glass");

        /// <summary>Window glass: seen through.</summary>
        public static bool SeeThrough(Collider pCollider)
        {
            int lId = pCollider.GetInstanceID();
            if (_seeThrough.TryGetValue(lId, out bool lGlass)) return lGlass;
            Renderer lRenderer = pCollider.GetComponent<Renderer>();
            string lMaterial = lRenderer != null && lRenderer.sharedMaterial != null ? lRenderer.sharedMaterial.name : null;
            return _seeThrough[lId] = NpcRules.IsSeeThrough(pCollider.gameObject.name, lMaterial, pCollider.gameObject.layer, GlassLayer);
        }

        /// <summary>Does this collider muffle a sound (a wall, a floor, the ground)?</summary>
        public static bool Structural(Collider pCollider)
        {
            int lId = pCollider.GetInstanceID();
            if (_structural.TryGetValue(lId, out bool lWall)) return lWall;
            return _structural[lId] = NpcRules.IsStructural(pCollider is TerrainCollider, pCollider.bounds.size, SeeThrough(pCollider));
        }

        /// <summary>Nothing opaque between the two points (window glass is seen through).</summary>
        public static bool Clear(Vector3 pFrom, Vector3 pTo)
        {
            Vector3 lDir = pTo - pFrom;
            float lLength = lDir.magnitude;
            if (lLength < 0.01f) return true;
            lDir /= lLength;
            float lTravelled = 0f;
            for (int i = 0; i < MAX_STEPS; i++)
            {
                if (!Physics.Raycast(pFrom + lDir * lTravelled, lDir, out RaycastHit lHit, lLength - lTravelled, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore)) return true;
                if (!SeeThrough(lHit.collider)) return false;
                lTravelled += lHit.distance + STEP;
                if (lTravelled >= lLength) return true;
            }
            return false;
        }

        /// <summary>
        /// Walls and floors between two points (each one muffles a sound), up to
        /// <see cref="DetectionModel.MAX_WALLS"/>. Steps through every surface the ray enters, so two
        /// walls of the same building count twice; the leaves of one partition count once.
        /// </summary>
        public static int WallsBetween(Vector3 pFrom, Vector3 pTo)
        {
            Vector3 lDir = pTo - pFrom;
            float lLength = lDir.magnitude;
            if (lLength < 0.05f) return 0;
            lDir /= lLength;
            int lWalls = 0;
            float lTravelled = 0f, lLastWall = -10f;
            for (int i = 0; i < MAX_STEPS && lWalls < DetectionModel.MAX_WALLS; i++)
            {
                if (!Physics.Raycast(pFrom + lDir * lTravelled, lDir, out RaycastHit lHit, lLength - lTravelled, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore)) break;
                float lAt = lTravelled + lHit.distance;
                if (Structural(lHit.collider))
                {
                    if (lAt - lLastWall > NpcRules.WALL_MERGE) lWalls++;
                    lLastWall = lAt;
                }
                lTravelled = lAt + STEP;
                if (lTravelled >= lLength) break;
            }
            return lWalls;
        }
    }
}
