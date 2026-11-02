using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// A waypoint of a patrol as a map builder writes it: a position, and optionally an errand there
    /// (<see cref="PatrolAction"/>) for some seconds facing a world yaw (NaN: the way it walked in).
    /// A plain <see cref="Vector3"/> converts to one (the route's pause, no errand).
    /// </summary>
    public struct PatrolPoint
    {
        public Vector3 position;
        public bool errand;
        public PatrolAction action;
        public float seconds;
        public float yaw;

        public static implicit operator PatrolPoint(Vector3 pPosition) => new PatrolPoint { position = pPosition, yaw = float.NaN };
    }

    /// <summary>
    /// How the map builders place what the AI needs (Track D API, see docs/changes/0.7.0-ai.md):
    /// <list type="bullet">
    /// <item><see cref="Exit"/>: where the crowd runs when trouble starts;</item>
    /// <item><see cref="Patrol"/> / <see cref="Route"/>: a loop of <see cref="PatrolPoint"/>s with errands
    /// (<see cref="At"/>: look around, chat, smoke, phone, check a door, lean, stand watch), walked by a
    /// patrol of one to five (two walk side by side and chat);</item>
    /// <item><see cref="PatrolPlanner"/> writes loops for you (inside a building, through town);
    /// <see cref="PatrolAudit"/> checks a map's patrols.</item>
    /// </list>
    /// Everything goes under the builder's markers group, so the end-of-build NavMesh snap moves the
    /// points onto the floor like every other NPC point.
    /// </summary>
    public static class AiPlacement
    {
        private static int _routes;

        /// <summary>A waypoint with an errand: do <paramref name="pAction"/> there for about <paramref name="pSeconds"/>, facing <paramref name="pYaw"/> (world degrees; NaN: the way it walked in).</summary>
        public static PatrolPoint At(Vector3 pPosition, PatrolAction pAction, float pSeconds = 6f, float pYaw = float.NaN)
            => new PatrolPoint { position = pPosition, errand = true, action = pAction, seconds = pSeconds, yaw = pYaw };

        /// <summary>A waypoint walked through without stopping (a doorway, a corner of a corridor).</summary>
        public static PatrolPoint Through(Vector3 pPosition) => At(pPosition, PatrolAction.Pause, 0f);

        /// <summary>
        /// An exit for the crowd: a gate, the road out, a door to the street, a jetty - wherever
        /// a panicked guest would run to get away. <paramref name="pYaw"/> faces out (gizmo only).
        /// Put two or more around every crowd, on opposite sides; <paramref name="pZone"/> -1 serves every zone.
        /// </summary>
        public static ExitPoint Exit(Transform pMarkers, Vector3 pPosition, float pYaw, int pZone)
        {
            GameObject lExit = new GameObject("Exit_" + pZone);
            lExit.transform.SetParent(pMarkers, false);
            lExit.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0f, pYaw, 0f));
            ExitPoint lPoint = lExit.AddComponent<ExitPoint>();
            lPoint.Setup(pZone);
            return lPoint;
        }

        /// <summary>A loop of waypoints (no NPC on it yet): plain points pause <paramref name="pPause"/> s, points with an errand do it.</summary>
        public static PatrolRoute Route(Transform pMarkers, float pPause, params PatrolPoint[] pPoints)
        {
            Transform lRoute = new GameObject("Route_AI_" + _routes++).transform;
            lRoute.SetParent(pMarkers, false);
            lRoute.position = pPoints.Length > 0 ? pPoints[0].position : pMarkers.position;
            for (int i = 0; i < pPoints.Length; i++)
            {
                Transform lWaypoint = new GameObject("WP").transform;
                lWaypoint.SetParent(lRoute, false);
                lWaypoint.position = pPoints[i].position;
                // Facing: the yaw asked for, else the way the patrol walks in.
                Vector3 lIn = pPoints[i].position - pPoints[(i - 1 + pPoints.Length) % pPoints.Length].position;
                lIn.y = 0f;
                float lYaw = !float.IsNaN(pPoints[i].yaw) ? pPoints[i].yaw : lIn.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lIn).eulerAngles.y : 0f;
                lWaypoint.rotation = Quaternion.Euler(0f, lYaw, 0f);
                if (pPoints[i].errand) lWaypoint.gameObject.AddComponent<PatrolStop>().Setup(pPoints[i].action, pPoints[i].seconds);
            }
            PatrolRoute lPatrol = lRoute.gameObject.AddComponent<PatrolRoute>();
            lPatrol.Setup(pPause);
            return lPatrol;
        }

        /// <summary>
        /// A patrol of <paramref name="pSize"/> (1-5) on a loop: the first leads, a second walks beside
        /// it (a pair chats on the way and at its stops), more fall in behind. Spawned at the first point.
        /// </summary>
        public static PatrolRoute Patrol(Transform pMarkers, int pZone, int pSize, ItemKind pWeapon, float pPause, params PatrolPoint[] pPoints)
        {
            PatrolRoute lRoute = Route(pMarkers, pPause, pPoints);
            Vector3 lStart = pPoints[0].position;
            Vector3 lAhead = pPoints.Length > 1 ? pPoints[1].position - lStart : Vector3.forward;
            lAhead.y = 0f;
            if (lAhead.sqrMagnitude < 0.01f) lAhead = Vector3.forward;
            lAhead.Normalize();
            Vector3 lSide = Vector3.Cross(Vector3.up, lAhead);
            float lYaw = Quaternion.LookRotation(lAhead).eulerAngles.y;
            for (int i = 0; i < Mathf.Clamp(pSize, 1, 5); i++)
            {
                Vector3 lOffset = i == 0 ? Vector3.zero : i == 1 ? lSide * 0.9f : -lAhead * (1.5f * ((i + 1) / 2)) + lSide * (i % 2 == 1 ? 0.9f : -0.9f);
                Npc(pMarkers, NpcRole.Patrol, lStart + lOffset, lYaw, pZone, i == 0 ? pWeapon : ItemKind.Smg, lRoute);
            }
            return lRoute;
        }

        /// <summary>Gives the waypoint <paramref name="pIndex"/> of an existing route an errand (routes made by other kits).</summary>
        public static PatrolStop Stop(PatrolRoute pRoute, int pIndex, PatrolAction pAction, float pSeconds = 6f, float pYaw = float.NaN)
        {
            Transform lWaypoint = pRoute.Waypoint(pIndex);
            if (!float.IsNaN(pYaw)) lWaypoint.rotation = Quaternion.Euler(0f, pYaw, 0f);
            PatrolStop lStop = lWaypoint.GetComponent<PatrolStop>();
            if (lStop == null) lStop = lWaypoint.gameObject.AddComponent<PatrolStop>();
            lStop.Setup(pAction, pSeconds);
            return lStop;
        }

        private static void Npc(Transform pMarkers, NpcRole pRole, Vector3 pPosition, float pYaw, int pZone, ItemKind pWeapon, PatrolRoute pRoute)
        {
            GameObject lMarker = new GameObject("Npc_" + pRole);
            lMarker.transform.SetParent(pMarkers, false);
            lMarker.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0f, pYaw, 0f));
            lMarker.AddComponent<NpcSpawnPoint>().Setup(pRole, pZone, pRoute, pWeapon);
        }
    }
}
