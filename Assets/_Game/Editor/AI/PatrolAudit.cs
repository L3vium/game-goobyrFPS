using System.Collections.Generic;
using System.Text;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The patrol check of a built map (edit mode, reads the open scene):
    /// <list type="bullet">
    /// <item>at least <see cref="MIN_MOVING"/> of the guards move (patrols on a route);</item>
    /// <item>every key building - a building holding <see cref="KEY_POSTS"/> or more posts - has an
    /// interior loop (a route with most of its points inside it);</item>
    /// <item>at least one patrol walks the town (a route with most of its points in public places);</item>
    /// <item>crowds have somewhere to run: <see cref="ExitPoint"/>s in every zone that has a crowd;</item>
    /// <item>some waypoints carry errands (look around, chat, smoke...), so patrols are not robots.</item>
    /// </list>
    /// <c>Goobyr/Validate/AI Patrols</c>, or <see cref="Fails"/> from a map validator.
    /// </summary>
    public static class PatrolAudit
    {
        public const float MIN_MOVING = 0.35f;
        public const int KEY_POSTS = 3;

        public struct Report
        {
            public int guards, moving, routes, stops, townRoutes, exits;
            public List<string> keyBuildings, keyWithoutLoop, crowdZonesWithoutExit;
            public float MovingShare => guards > 0 ? (float)moving / guards : 0f;
        }

        [MenuItem("Goobyr/Validate/AI Patrols")]
        public static void Menu() => Debug.Log("[AI] " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ": " + Describe(Audit()));

        public static Report Audit()
        {
            Report lReport = new Report { keyBuildings = new List<string>(), keyWithoutLoop = new List<string>(), crowdZonesWithoutExit = new List<string>() };
            NpcSpawnPoint[] lSpawns = Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None);
            PatrolRoute[] lRoutes = Object.FindObjectsByType<PatrolRoute>(FindObjectsSortMode.None);
            BuildingVolume[] lBuildings = Object.FindObjectsByType<BuildingVolume>(FindObjectsSortMode.None);
            SecurityZone[] lZones = Object.FindObjectsByType<SecurityZone>(FindObjectsSortMode.None);
            ExitPoint[] lExits = Object.FindObjectsByType<ExitPoint>(FindObjectsSortMode.None);
            SecurityOutside lOutside = Object.FindFirstObjectByType<SecurityOutside>();
            lReport.routes = lRoutes.Length;
            lReport.exits = lExits.Length;

            HashSet<int> lCrowdZones = new HashSet<int>();
            foreach (NpcSpawnPoint lSpawn in lSpawns)
            {
                if (lSpawn.Role == NpcRole.Worker) { lCrowdZones.Add(lSpawn.Zone); continue; }
                if (lSpawn.Role == NpcRole.Target) continue;
                lReport.guards++;
                if (lSpawn.Route != null && lSpawn.Route.transform.childCount > 1) lReport.moving++;
            }

            foreach (PatrolRoute lRoute in lRoutes)
            {
                lReport.stops += lRoute.GetComponentsInChildren<PatrolStop>().Length;
                int lPublic = 0;
                foreach (Transform lPoint in lRoute.transform) if (TierAt(lZones, lOutside, lPoint.position) == SecurityTier.Public) lPublic++;
                if (lRoute.transform.childCount > 1 && lPublic * 2 > lRoute.transform.childCount) lReport.townRoutes++;
            }

            foreach (BuildingVolume lBuilding in lBuildings)
            {
                int lPosts = 0;
                foreach (NpcSpawnPoint lSpawn in lSpawns) if (lSpawn.Role != NpcRole.Worker && lBuilding.Contains(lSpawn.transform.position + Vector3.up)) lPosts++;
                if (lPosts < KEY_POSTS) continue;
                lReport.keyBuildings.Add(lBuilding.BuildingName);
                bool lLoop = false;
                foreach (PatrolRoute lRoute in lRoutes)
                {
                    int lInside = 0;
                    foreach (Transform lPoint in lRoute.transform) if (lBuilding.Contains(lPoint.position + Vector3.up)) lInside++;
                    if (lRoute.transform.childCount > 1 && lInside * 2 > lRoute.transform.childCount) { lLoop = true; break; }
                }
                if (!lLoop) lReport.keyWithoutLoop.Add(lBuilding.BuildingName);
            }

            foreach (int lZone in lCrowdZones)
            {
                bool lHas = false;
                foreach (ExitPoint lExit in lExits) if (lExit.Zone == lZone || lExit.Zone < 0) { lHas = true; break; }
                if (!lHas) lReport.crowdZonesWithoutExit.Add("zone " + lZone);
            }
            return lReport;
        }

        /// <summary>What fails the check (empty: the patrols are fine).</summary>
        public static List<string> Fails(Report pReport)
        {
            List<string> lFails = new List<string>();
            if (pReport.MovingShare < MIN_MOVING) lFails.Add($"{pReport.moving}/{pReport.guards} guards move ({pReport.MovingShare:P0} < {MIN_MOVING:P0})");
            if (pReport.keyWithoutLoop.Count > 0) lFails.Add("no interior loop in " + string.Join(", ", pReport.keyWithoutLoop));
            if (pReport.townRoutes < 1) lFails.Add("no patrol walks the town (a route mostly in public places)");
            if (pReport.crowdZonesWithoutExit.Count > 0) lFails.Add("crowds with nowhere to run (no ExitPoint) in " + string.Join(", ", pReport.crowdZonesWithoutExit));
            if (pReport.routes > 0 && pReport.stops == 0) lFails.Add("no patrol errands (PatrolStop) anywhere");
            return lFails;
        }

        public static List<string> Fails() => Fails(Audit());

        public static string Describe(Report pReport)
        {
            StringBuilder lText = new StringBuilder();
            lText.Append($"{pReport.moving}/{pReport.guards} guards move ({pReport.MovingShare:P0}), {pReport.routes} routes ({pReport.townRoutes} in town), {pReport.stops} errands, ");
            lText.Append($"{pReport.keyBuildings.Count - pReport.keyWithoutLoop.Count}/{pReport.keyBuildings.Count} key buildings with an interior loop, {pReport.exits} exits");
            List<string> lFails = Fails(pReport);
            lText.Append(lFails.Count == 0 ? " - OK" : " - FAIL: " + string.Join("; ", lFails));
            return lText.ToString();
        }

        /// <summary>The tier at a point in edit mode (the runtime registry is empty until play).</summary>
        private static SecurityTier TierAt(SecurityZone[] pZones, SecurityOutside pOutside, Vector3 pPoint)
        {
            bool lInside = false;
            SecurityTier lTier = SecurityTier.Public;
            foreach (SecurityZone lZone in pZones)
            {
                if (!lZone.Contains(pPoint)) continue;
                if (!lInside || lZone.Tier > lTier) lTier = lZone.Tier;
                lInside = true;
            }
            if (lInside) return lTier;
            if (pOutside == null) return SecurityTier.Hostile;
            SerializedObject lOutside = new SerializedObject(pOutside);
            SerializedProperty lProperty = lOutside.FindProperty("_tier");
            return lProperty != null ? (SecurityTier)lProperty.enumValueIndex : SecurityTier.Hostile;
        }
    }
}
