using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Writes patrol loops for the map builders (<see cref="AiPlacement.Patrol"/> walks them):
    /// <list type="bullet">
    /// <item><see cref="InteriorLoop"/>: through one floor of a generated building - room to room by
    /// the doors NPCs can pass, an errand fitting each room (look around the office, smoke in the
    /// guard room, chat in the kitchen, stand watch at the vault, try the locked doors), and back;</item>
    /// <item><see cref="TownLoop"/>: through streets and squares - a chat at the first stop, a look
    /// around at every other corner.</item>
    /// </list>
    /// The NavMesh finds the way between waypoints, so a loop only lists where to be.
    /// </summary>
    public static class PatrolPlanner
    {
        /// <summary>
        /// A loop through floor <paramref name="pFloor"/> of a building: from <paramref name="pStartRoom"/>
        /// (else its hall, lobby or corridor, else its largest room) into up to <paramref name="pMaxRooms"/>
        /// rooms joined by doors NPCs can pass, depth first, coming back the way it went. Null when the
        /// floor has fewer than two connected rooms.
        /// </summary>
        public static PatrolPoint[] InteriorLoop(ArchResult pBuilding, int pFloor, int pMaxRooms = 6, string pStartRoom = null)
        {
            ArchSpec lSpec = pBuilding.spec;
            // Rooms sharing a name are one space.
            List<string> lNames = new List<string>();
            Dictionary<string, ArchRoom> lFirst = new Dictionary<string, ArchRoom>();
            foreach (ArchRoom lRoom in lSpec.rooms)
            {
                if (lRoom.floor != pFloor || lRoom.isVoid || lRoom.noFloor || string.IsNullOrEmpty(lRoom.name)) continue;
                if (lFirst.ContainsKey(lRoom.name)) continue;
                lFirst[lRoom.name] = lRoom;
                lNames.Add(lRoom.name);
            }
            if (lNames.Count < 2) return null;

            // Doors between two rooms: an edge NPCs can walk (locked doors are errands, not passages).
            Dictionary<string, List<string>> lLinks = new Dictionary<string, List<string>>();
            List<(ArchDoor door, string room)> lLocked = new List<(ArchDoor, string)>();
            foreach (string lName in lNames) lLinks[lName] = new List<string>();
            foreach (ArchDoor lDoor in lSpec.doors)
            {
                if (lDoor.floor != pFloor || lDoor.sill > 0.3f) continue;
                List<string> lSides = RoomsAt(lSpec, pFloor, lDoor.at);
                bool lPassable = lDoor.kind != DoorKind.Secure || lDoor.npcPass;
                if (!lPassable)
                {
                    foreach (string lSide in lSides) lLocked.Add((lDoor, lSide));
                    continue;
                }
                if (lSides.Count < 2) continue;
                for (int i = 0; i < lSides.Count; i++)
                    for (int j = 0; j < lSides.Count; j++)
                        if (i != j && lLinks.ContainsKey(lSides[i]) && !lLinks[lSides[i]].Contains(lSides[j])) lLinks[lSides[i]].Add(lSides[j]);
            }

            string lStart = pStartRoom != null && lLinks.ContainsKey(pStartRoom) ? pStartRoom : PickStart(lNames, lFirst, lLinks);
            List<PatrolPoint> lPoints = new List<PatrolPoint>();
            HashSet<string> lSeen = new HashSet<string>();
            int lBudget = Mathf.Max(2, pMaxRooms);
            Visit(pBuilding, lStart, lFirst, lLinks, lLocked, lSeen, lPoints, ref lBudget, true);
            if (lSeen.Count < 2) return null;
            return lPoints.ToArray();
        }

        private static void Visit(ArchResult b, string pRoom, Dictionary<string, ArchRoom> pRooms, Dictionary<string, List<string>> pLinks, List<(ArchDoor door, string room)> pLocked,
            HashSet<string> pSeen, List<PatrolPoint> pPoints, ref int pBudget, bool pFirst)
        {
            pSeen.Add(pRoom);
            pBudget--;
            ArchRoom lRoom = pRooms[pRoom];
            Vector3 lCenter = b.At(lRoom.rect.center, lRoom.floor);
            PatrolAction lErrand = ErrandFor(lRoom.type);
            pPoints.Add(lErrand == PatrolAction.Pause && !pFirst ? AiPlacement.Through(lCenter) : AiPlacement.At(lCenter, lErrand == PatrolAction.Pause ? PatrolAction.LookAround : lErrand, SecondsFor(lErrand)));
            // A locked door of this room: walk up to it and try it.
            foreach ((ArchDoor lDoor, string lSide) in pLocked)
            {
                if (lSide != pRoom) continue;
                Vector3 lDoorAt = b.At(lDoor.at, lRoom.floor);
                Vector3 lInside = lDoorAt + (lCenter - lDoorAt).normalized * 1.1f;
                Vector3 lFace = lDoorAt - lInside;
                pPoints.Add(AiPlacement.At(lInside, PatrolAction.CheckDoor, 4f, Quaternion.LookRotation(new Vector3(lFace.x, 0f, lFace.z).normalized).eulerAngles.y));
                break;
            }
            foreach (string lNext in pLinks[pRoom])
            {
                if (pBudget <= 0) break;
                if (pSeen.Contains(lNext)) continue;
                Visit(b, lNext, pRooms, pLinks, pLocked, pSeen, pPoints, ref pBudget, false);
                // Back through this room on the way out (no stop the second time).
                pPoints.Add(AiPlacement.Through(lCenter));
            }
        }

        /// <summary>The rooms (by name) on either side of a door: those whose rectangle, grown a little, holds it.</summary>
        private static List<string> RoomsAt(ArchSpec pSpec, int pFloor, Vector2 pAt)
        {
            List<string> lRooms = new List<string>();
            foreach (ArchRoom lRoom in pSpec.rooms)
            {
                if (lRoom.floor != pFloor || lRoom.isVoid || string.IsNullOrEmpty(lRoom.name) || lRooms.Contains(lRoom.name)) continue;
                Rect lGrown = new Rect(lRoom.rect.xMin - 0.4f, lRoom.rect.yMin - 0.4f, lRoom.rect.width + 0.8f, lRoom.rect.height + 0.8f);
                if (lGrown.Contains(pAt)) lRooms.Add(lRoom.name);
            }
            return lRooms;
        }

        private static string PickStart(List<string> pNames, Dictionary<string, ArchRoom> pRooms, Dictionary<string, List<string>> pLinks)
        {
            string lBest = pNames[0];
            float lBestScore = float.MinValue;
            foreach (string lName in pNames)
            {
                ArchRoom lRoom = pRooms[lName];
                bool lHub = lRoom.type is RoomType.Hall or RoomType.Lobby or RoomType.Corridor or RoomType.Gallery;
                float lScore = pLinks[lName].Count * 10f + (lHub ? 25f : 0f) + lRoom.rect.width * lRoom.rect.height * 0.05f;
                if (lScore > lBestScore) { lBestScore = lScore; lBest = lName; }
            }
            return lBest;
        }

        /// <summary>What a patrol does in a room of this kind.</summary>
        public static PatrolAction ErrandFor(RoomType pType) => pType switch
        {
            RoomType.Kitchen or RoomType.Mess or RoomType.Bar or RoomType.Dining or RoomType.Living or RoomType.Salon => PatrolAction.Chat,
            RoomType.GuardRoom or RoomType.Barracks or RoomType.Garage or RoomType.Workshop or RoomType.Hangar or RoomType.Plant or RoomType.Store or RoomType.Laundry => PatrolAction.Smoke,
            RoomType.Vault or RoomType.Armoury or RoomType.Security => PatrolAction.Watch,
            RoomType.Terrace or RoomType.Loggia or RoomType.Belvedere => PatrolAction.Lean,
            RoomType.Radio or RoomType.Police => PatrolAction.Phone,
            RoomType.Corridor or RoomType.Stair or RoomType.Hall or RoomType.Lobby or RoomType.Gallery => PatrolAction.Pause,
            _ => PatrolAction.LookAround
        };

        private static float SecondsFor(PatrolAction pAction) => pAction switch
        {
            PatrolAction.Smoke => 12f,
            PatrolAction.Chat => 10f,
            PatrolAction.Phone => 9f,
            PatrolAction.Watch => 8f,
            PatrolAction.Lean => 8f,
            _ => 5f
        };

        /// <summary>
        /// A loop through town: the corners in order, a chat at the first (a pair stops and talks), a
        /// look around at every other corner, the rest walked through with the route's pause.
        /// </summary>
        public static PatrolPoint[] TownLoop(params Vector3[] pCorners)
        {
            PatrolPoint[] lPoints = new PatrolPoint[pCorners.Length];
            for (int i = 0; i < pCorners.Length; i++)
                lPoints[i] = i == 0 ? AiPlacement.At(pCorners[i], PatrolAction.Chat, 10f) : i % 2 == 0 ? AiPlacement.At(pCorners[i], PatrolAction.LookAround, 5f) : (PatrolPoint)pCorners[i];
            return lPoints;
        }
    }
}
