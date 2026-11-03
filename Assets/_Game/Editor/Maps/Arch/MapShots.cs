using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Review tools for the generated maps: renders a camera to a PNG (any point, any time of day),
    /// and an architecture test scene (a building on a lawn under a sun) for quick iterations.
    /// </summary>
    public static class MapShots
    {
        /// <summary>Renders the open scene from a point toward another into a PNG.</summary>
        public static string Shot(string pPath, Vector3 pFrom, Vector3 pTo, float pFov = 60f, int pWidth = 1600, int pHeight = 900, float pFar = 1500f)
        {
            // Every framing is checked before it is saved; a failing one is re-rolled round its target, or dropped.
            if (!Frames(pFrom, pTo, out string lWhy))
            {
                if (Reroll(pFrom, pTo, out Vector3 lNew))
                {
                    _rerolls.Append($"{Path.GetFileName(pPath)}: {lWhy}; re-rolled {pFrom:F1} -> {lNew:F1}\n");
                    pFrom = lNew;
                }
                else
                {
                    _rerolls.Append($"{Path.GetFileName(pPath)}: {lWhy}; no valid framing, dropped\n");
                    return pPath + " (dropped)";
                }
            }
            GameObject lObject = new GameObject("ShotCamera");
            Camera lCamera = lObject.AddComponent<Camera>();
            lObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            lCamera.transform.position = pFrom;
            Vector3 lLook = (pTo - pFrom).normalized;
            if (Mathf.Abs(lLook.y) > 0.97f) lLook = (lLook + Vector3.forward * 0.3f).normalized;
            lCamera.transform.rotation = Quaternion.LookRotation(lLook, Vector3.up);
            lCamera.fieldOfView = pFov;
            lCamera.nearClipPlane = 0.05f;
            lCamera.farClipPlane = pFar;
            RenderTexture lTarget = new RenderTexture(pWidth, pHeight, 24, RenderTextureFormat.ARGB32);
            lTarget.antiAliasing = 4;
            lCamera.targetTexture = lTarget;
            lCamera.Render();
            lCamera.Render();
            RenderTexture.active = lTarget;
            Texture2D lImage = new Texture2D(pWidth, pHeight, TextureFormat.RGB24, false);
            lImage.ReadPixels(new Rect(0, 0, pWidth, pHeight), 0, 0);
            lImage.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(pPath));
            File.WriteAllBytes(pPath, lImage.EncodeToPNG());
            lCamera.targetTexture = null;
            Object.DestroyImmediate(lTarget);
            Object.DestroyImmediate(lImage);
            Object.DestroyImmediate(lObject);
            return pPath;
        }

        private static bool Clear(Vector3 p) => !Physics.CheckSphere(p, 0.35f, ~0, QueryTriggerInteraction.Ignore);

        private static readonly System.Text.StringBuilder _rerolls = new System.Text.StringBuilder();

        /// <summary>The framings re-rolled or dropped since the last call, one per line (and clears the log).</summary>
        public static string Rerolls()
        {
            string s = _rerolls.ToString();
            _rerolls.Clear();
            return s;
        }

        /// <summary>True when a point is inside one of the map's buildings.</summary>
        private static bool Indoors(Vector3 p)
        {
            foreach (Systems.BuildingVolume v in Object.FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None)) if (v.Contains(p)) return true;
            return false;
        }

        /// <summary>
        /// The framing rules: the camera is not inside a collider (0.3 m sphere), and the centre ray meets
        /// nothing closer than 3 m (2 m indoors), or than the subject itself when it is nearer.
        /// </summary>
        public static bool Frames(Vector3 pFrom, Vector3 pTo, out string pWhy)
        {
            if (Physics.CheckSphere(pFrom, 0.3f, ~0, QueryTriggerInteraction.Ignore)) { pWhy = "camera inside a collider"; return false; }
            float lNeed = Mathf.Min(Indoors(pFrom) ? 2f : 3f, Vector3.Distance(pFrom, pTo) - 0.5f);
            if (lNeed > 0f && Physics.Raycast(pFrom, (pTo - pFrom).normalized, out RaycastHit lHit, lNeed, ~0, QueryTriggerInteraction.Ignore))
            {
                pWhy = $"centre ray hits {lHit.collider.name} at {lHit.distance:F1} m";
                return false;
            }
            pWhy = null;
            return true;
        }

        /// <summary>A new camera point round the same target: turned 15-90 degrees either way, nearer or further, a little up or down.</summary>
        private static bool Reroll(Vector3 pFrom, Vector3 pTo, out Vector3 pNew)
        {
            Vector3 v = pFrom - pTo;
            float[] lScales = { 1f, 0.8f, 1.2f, 0.65f };
            float[] lUps = { 0f, 0.4f, -0.3f };
            for (int k = 0; k < 12; k++)
            {
                float lAngle = (k % 2 == 0 ? 1 : -1) * (k / 2 + 1) * 15f;
                foreach (float lScale in lScales)
                    foreach (float lUp in lUps)
                    {
                        Vector3 p = pTo + Quaternion.Euler(0, lAngle, 0) * v * lScale + Vector3.up * lUp;
                        if (!Frames(p, pTo, out _)) continue;
                        if (Physics.Linecast(p, pTo, out RaycastHit lHit, ~0, QueryTriggerInteraction.Ignore) && lHit.distance < Vector3.Distance(p, pTo) - 1.2f) continue;
                        pNew = p;
                        return true;
                    }
            }
            pNew = pFrom;
            return false;
        }

        /// <summary>How far the centre ray runs free from a camera toward a target (up to 40 m).</summary>
        private static float Depth(Vector3 pFrom, Vector3 pTo) => Physics.Raycast(pFrom, (pTo - pFrom).normalized, out RaycastHit lHit, 40f, ~0, QueryTriggerInteraction.Ignore) ? lHit.distance : 40f;

        /// <summary>
        /// A room from a corner: each corner at eye height is tried, aiming across the room's middle at
        /// the far corner; the corner that frames and sees furthest wins.
        /// </summary>
        private static string RoomShot(string pPath, Transform pZone, Vector3 pSize)
        {
            Vector3 c = pZone.position;
            Vector3 lHalf = new Vector3(pSize.x * 0.5f - 0.55f, 0f, pSize.z * 0.5f - 0.55f);
            Vector3 lBestFrom = Vector3.zero, lBestTo = Vector3.zero;
            float lBest = -1f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    foreach (float lUp in new[] { 1.75f, 1.5f })
                    {
                        Vector3 lFrom = c + pZone.rotation * new Vector3(sx * lHalf.x, lUp, sz * lHalf.z);
                        Vector3 lTo = c + pZone.rotation * new Vector3(-sx * lHalf.x * 0.7f, 1.0f, -sz * lHalf.z * 0.7f);
                        if (!Frames(lFrom, lTo, out _)) continue;
                        float d = Depth(lFrom, lTo);
                        if (d > lBest) { lBest = d; lBestFrom = lFrom; lBestTo = lTo; }
                    }
            if (lBest < 0f)
            {
                _rerolls.Append($"{Path.GetFileName(pPath)}: no corner frames the room, dropped\n");
                return pPath + " (dropped)";
            }
            return Shot(pPath, lBestFrom, lBestTo, 74f);
        }

        /// <summary>True when the land at a point is painted mostly with a grass layer.</summary>
        private static bool OnGrass(Vector3 p)
        {
            Terrain lLand = Terrain.activeTerrain;
            if (lLand == null) return false;
            TerrainData td = lLand.terrainData;
            Vector3 l = p - lLand.transform.position;
            int x = Mathf.Clamp(Mathf.RoundToInt(l.x / td.size.x * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(l.z / td.size.z * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);
            float[,,] a = td.GetAlphamaps(x, z, 1, 1);
            float lGrass = 0f;
            for (int i = 0; i < td.terrainLayers.Length && i < a.GetLength(2); i++)
            {
                TerrainLayer lLayer = td.terrainLayers[i];
                string lName = ((lLayer != null ? lLayer.name : "") + " " + (lLayer != null && lLayer.diffuseTexture != null ? lLayer.diffuseTexture.name : "")).ToLowerInvariant();
                if (lName.Contains("grass") || lName.Contains("turf") || lName.Contains("meadow") || lName.Contains("lawn")) lGrass += a[0, 0, i];
            }
            return lGrass > 0.5f;
        }

        /// <summary>
        /// A landmark framed from the ground: the first candidate point (x, z at eye height) with a clear
        /// line to it; every candidate passed over is logged as a re-roll.
        /// </summary>
        public static string FrameFrom(string pPath, Vector3 pTarget, float pFov, params Vector2[] pCandidates)
        {
            Physics.SyncTransforms();
            foreach (Vector2 c in pCandidates)
            {
                Vector3 lFrom = Eye(c.x, c.y, 1.7f);
                bool lBlocked = Physics.Linecast(lFrom, pTarget, out RaycastHit lHit, ~0, QueryTriggerInteraction.Ignore) && lHit.distance < Vector3.Distance(lFrom, pTarget) - 4f;
                if (lBlocked || !Frames(lFrom, pTarget, out _) || Indoors(lFrom))
                {
                    _rerolls.Append($"{Path.GetFileName(pPath)}: ({c.x:F0}, {c.y:F0}) {(lBlocked ? "blocked by " + lHit.collider.name : "no clear framing")}, next\n");
                    continue;
                }
                return Shot(pPath, lFrom, pTarget, pFov);
            }
            _rerolls.Append($"{Path.GetFileName(pPath)}: no candidate sees the target, dropped\n");
            return pPath + " (dropped)";
        }

        /// <summary>The San Lazaro mirador tower from the plaza or the hacienda grounds.</summary>
        public static string MiradorShot(string pPath)
        {
            Vector3 lTop = new Vector3(121f, 69f, 124f);
            foreach (UI.MapPoi lPoi in Object.FindObjectsByType<UI.MapPoi>(FindObjectsSortMode.None))
                if (lPoi.Label == "Mirador") { lTop = lPoi.transform.position; break; }
            Vector3 lTarget = lTop + Vector3.down * 7f;
            return FrameFrom(pPath, lTarget, 38f,
                new Vector2(110f, 52f), new Vector2(135f, 55f), new Vector2(150f, 62f), new Vector2(92f, 60f), new Vector2(70f, 72f),
                new Vector2(165f, 95f), new Vector2(60f, 105f), new Vector2(-20f, -140f), new Vector2(-30f, -150f), new Vector2(-12f, -128f));
        }

        /// <summary>The first viewpoint round a target (distance, height) with a clear line of sight to it.</summary>
        private static bool View(Vector3 pTarget, Vector3 pPreferred, float pDistance, float pUp, out Vector3 pFrom, bool pOutdoors = false)
        {
            Systems.BuildingVolume[] lVolumes = pOutdoors ? Object.FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None) : new Systems.BuildingVolume[0];
            Vector3 lFlat = pPreferred;
            lFlat.y = 0f;
            if (lFlat.sqrMagnitude < 0.01f) lFlat = Vector3.back;
            lFlat.Normalize();
            for (int k = 0; k < 16; k++)
            {
                float lAngle = (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 22.5f;
                for (float lScale = 1f; lScale > 0.35f; lScale -= 0.2f)
                {
                    Vector3 p = pTarget + Quaternion.Euler(0, lAngle, 0) * lFlat * pDistance * lScale + Vector3.up * pUp;
                    if (!Clear(p)) continue;
                    bool lIndoors = false;
                    foreach (Systems.BuildingVolume v in lVolumes) if (v.Contains(p)) { lIndoors = true; break; }
                    if (lIndoors) continue;
                    if (Physics.Linecast(p, pTarget, out RaycastHit lHit, ~0, QueryTriggerInteraction.Ignore) && lHit.distance < Vector3.Distance(p, pTarget) - 1.2f) continue;
                    pFrom = p;
                    return true;
                }
            }
            pFrom = pTarget + lFlat * pDistance + Vector3.up * pUp;
            return false;
        }

        private static Transform Find(string pName)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == pName && t.Find("Shell") != null) return t;
            return null;
        }

        /// <summary>
        /// Shots of a generated building for review: every exterior climb (drainpipe, ivy, trellis) with
        /// its wall, every balcony, the grand stair, and the largest furnished rooms of each floor.
        /// </summary>
        public static string AutoShots(string pDir, string pPrefix, string pBuilding, int pRoomsPerFloor = 2, float pStorey = 3.6f)
        {
            Physics.SyncTransforms();
            Transform lRoot = Find(pBuilding);
            if (lRoot == null) return "no building " + pBuilding;
            System.Text.StringBuilder lLog = new System.Text.StringBuilder();
            int n = 0;
            foreach (Transform t in lRoot.GetComponentsInChildren<Transform>())
            {
                if (!t.name.EndsWith("_Climb") || t.name.StartsWith("Duct")) continue;
                BoxCollider lBox = t.GetComponent<BoxCollider>();
                float lHeight = lBox != null ? lBox.size.y : 6f;
                Vector3 lFoot = t.position - Vector3.up * (lHeight * 0.5f - 0.5f);
                Vector3 lTarget = lFoot + Vector3.up * Mathf.Min(4f, lHeight * 0.45f);
                View(lTarget, t.forward + t.right * 0.5f, 9f, 1.2f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_climb{n++}_{t.name.Replace("_Climb", "").ToLowerInvariant()}.png"), lFrom, lTarget, 62f)).Append('\n');
            }
            n = 0;
            foreach (Transform t in lRoot.GetComponentsInChildren<Transform>())
            {
                if (t.name != "Poi_Balcony" || n >= 4) continue;
                Systems.BuildingVolume lVolume = lRoot.GetComponentInChildren<Systems.BuildingVolume>();
                Vector3 lCentre = lVolume != null ? lVolume.transform.position : lRoot.position;
                Vector3 lTarget = t.position + Vector3.down * 0.5f;
                View(lTarget, t.position - lCentre, 10f, -1.5f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_balcony{n++}.png"), lFrom, lTarget, 60f)).Append('\n');
            }
            // Rooms: the largest named spaces of every floor (no corridors, stairs, galleries, terraces).
            Transform lZones = lRoot.Find("SecurityZones");
            // Stairs: the first two stair halls, from a top corner looking down the flight.
            if (lZones != null)
            {
                int lStairs = 0;
                foreach (Transform z in lZones)
                {
                    AI.SecurityZone lZone = z.GetComponent<AI.SecurityZone>();
                    if (lStairs >= 2 || lZone == null || !z.name.Contains("Stair") || lZone.Size.x < 2.5f || lZone.Size.z < 2.5f) continue;
                    Vector3 c = z.position;
                    Vector3 lHalf = new Vector3(lZone.Size.x * 0.5f - 0.5f, 0f, lZone.Size.z * 0.5f - 0.5f);
                    Vector3 lFrom = c + z.rotation * new Vector3(-lHalf.x, 2.3f, -lHalf.z);
                    lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_stair{lStairs++}_{z.name.Replace("Zone_", "").Split('_')[0].ToLowerInvariant()}.png"), lFrom, c + z.rotation * new Vector3(lHalf.x * 0.3f, 0.4f, lHalf.z * 0.3f), 74f)).Append('\n');
                }
            }
            if (lZones != null)
            {
                System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<AI.SecurityZone>> lFloors = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<AI.SecurityZone>>();
                string[] lSkip = { "Corridor", "Stair", "Chute", "Gallery", "Portico", "Patio", "Terrace", "Roof", "Atrium", "Landing", "Walk", "Court", "Loggia", "Helipad", "Niche", "WC", "Belfry", "Belvedere", "Mezzanine", "Pool", "Clock", "Tower", "Sauna" };
                foreach (Transform z in lZones)
                {
                    AI.SecurityZone lZone = z.GetComponent<AI.SecurityZone>();
                    if (lZone == null) continue;
                    bool lBad = false;
                    foreach (string k in lSkip) if (z.name.Contains(k)) lBad = true;
                    if (lBad || lZone.Size.x < 3.5f || lZone.Size.z < 3.5f) continue;
                    int f = Mathf.RoundToInt((z.position.y - lRoot.position.y) / pStorey);
                    if (!lFloors.TryGetValue(f, out var lList)) lFloors[f] = lList = new System.Collections.Generic.List<AI.SecurityZone>();
                    lList.Add(lZone);
                }
                foreach (var lFloor in lFloors)
                {
                    lFloor.Value.Sort((a, b) => (b.Size.x * b.Size.z).CompareTo(a.Size.x * a.Size.z));
                    for (int k = 0; k < Mathf.Min(pRoomsPerFloor, lFloor.Value.Count); k++)
                    {
                        AI.SecurityZone z = lFloor.Value[k];
                        string lName = z.name.Replace("Zone_", "").Split('_')[0];
                        lLog.Append(RoomShot(Path.Combine(pDir, $"{pPrefix}_f{lFloor.Key}_{lName.ToLowerInvariant()}.png"), z.transform, z.Size)).Append('\n');
                    }
                }
            }
            return lLog.ToString();
        }

        /// <summary>
        /// Shots of the security set: every sentry gun from in front (its laser toward the camera),
        /// every terminal from where a player would stand, and each cable from beside its middle.
        /// </summary>
        public static string SecurityShots(string pDir, string pPrefix)
        {
            Physics.SyncTransforms();
            System.Text.StringBuilder lLog = new System.Text.StringBuilder();
            int n = 0;
            foreach (Systems.AutoTurret t in Object.FindObjectsByType<Systems.AutoTurret>(FindObjectsSortMode.None))
            {
                Vector3 lTarget = t.transform.position + Vector3.up * 0.9f;
                View(lTarget, t.transform.forward, 3.6f, 0.5f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_turret{n++}.png"), lFrom, lTarget, 64f)).Append('\n');
            }
            n = 0;
            foreach (Systems.TurretTerminal t in Object.FindObjectsByType<Systems.TurretTerminal>(FindObjectsSortMode.None))
            {
                Vector3 lTarget = t.transform.position + Vector3.up * 0.2f;
                View(lTarget, t.transform.forward, 2.2f, 0.9f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_terminal{n++}.png"), lFrom, lTarget, 68f)).Append('\n');
            }
            n = 0;
            foreach (Systems.TurretCable c in Object.FindObjectsByType<Systems.TurretCable>(FindObjectsSortMode.None))
            {
                // Aim at a point a third of the way along the cable (its mesh is laid out in path order).
                MeshFilter lFilter = null;
                foreach (MeshFilter f in c.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null && (lFilter == null || f.sharedMesh.vertexCount > lFilter.sharedMesh.vertexCount)) lFilter = f;
                if (lFilter == null) continue;
                Vector3[] lVertices = lFilter.sharedMesh.vertices;
                Vector3 lTarget = lFilter.transform.TransformPoint(lVertices[lVertices.Length / 3]);
                View(lTarget, OpenSide(lTarget), 3.2f, 0.3f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_cable{n++}.png"), lFrom, lTarget, 72f)).Append('\n');
            }
            return lLog.ToString();
        }

        /// <summary>The horizontal direction with the most free space from a point (the room side of a wall).</summary>
        private static Vector3 OpenSide(Vector3 pPoint)
        {
            Vector3 lBest = Vector3.back;
            float lFree = -1f;
            for (int k = 0; k < 12; k++)
            {
                Vector3 d = Quaternion.Euler(0, k * 30f, 0) * Vector3.forward;
                float lDistance = Physics.Raycast(pPoint + d * 0.12f, d, out RaycastHit lHit, 6f, ~0, QueryTriggerInteraction.Ignore) ? lHit.distance : 6f;
                if (lDistance > lFree) { lFree = lDistance; lBest = d; }
            }
            return lBest;
        }

        private static Vector3 _panic;

        /// <summary>The stealth entries (vents, chutes, wells, tunnel mouths, drop ladders) from their approach, at eye height.</summary>
        public static string EntryShots(string pDir, string pPrefix, int pMax = 10)
        {
            Physics.SyncTransforms();
            System.Text.StringBuilder lLog = new System.Text.StringBuilder();
            Systems.BuildingVolume[] lVolumes = Object.FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None);
            int n = 0;
            foreach (UI.MapPoi lPoi in Object.FindObjectsByType<UI.MapPoi>(FindObjectsSortMode.None))
            {
                string lLabel = lPoi.Label ?? "";
                bool lEntry = lPoi.Type == UI.MapPoi.Kind.Vent || lPoi.Type == UI.MapPoi.Kind.Underground || lPoi.Type == UI.MapPoi.Kind.Shortcut
                    || System.Text.RegularExpressions.Regex.IsMatch(lLabel, "chute|well|qanat|cave|crevasse|bay|boarded|hatch|ladder|duct", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!lEntry || n >= pMax) continue;
                Vector3 p = lPoi.transform.position;
                // Approach from outside: away from the nearest building's middle.
                Vector3 lAway = Vector3.back;
                float lBest = float.MaxValue;
                foreach (Systems.BuildingVolume v in lVolumes)
                {
                    float d = Vector3.Distance(v.transform.position, p);
                    if (d < lBest) { lBest = d; lAway = p - v.transform.position; }
                }
                lAway.y = 0f;
                if (!View(p + Vector3.up * 0.6f, lAway, 6f, 1.0f, out Vector3 lFrom, true)) View(p + Vector3.up * 0.6f, lAway, 4f, 1.0f, out lFrom);
                string lName = System.Text.RegularExpressions.Regex.Replace(lLabel.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_entry{n++}_{lName}.png"), lFrom, p + Vector3.up * 0.6f, 64f)).Append('\n');
            }
            return lLog.ToString();
        }

        /// <summary>Play mode: patrols caught walking (moving ones, 25 m apart), each from ahead and to one side.</summary>
        public static string PatrolShots(string pDir, string pPrefix, int pCount)
        {
            Physics.SyncTransforms();
            System.Text.StringBuilder lLog = new System.Text.StringBuilder();
            System.Collections.Generic.List<Vector3> lDone = new System.Collections.Generic.List<Vector3>();
            int n = 0;
            foreach (AI.Npc lNpc in Object.FindObjectsByType<AI.Npc>(FindObjectsSortMode.None))
            {
                if (n >= pCount || lNpc.Role != AI.NpcRole.Patrol) continue;
                UnityEngine.AI.NavMeshAgent lAgent = lNpc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (lAgent == null || lAgent.velocity.magnitude < 0.4f) continue;
                Vector3 p = lNpc.transform.position;
                if (lDone.Exists(d => Vector3.Distance(d, p) < 25f)) continue;
                lDone.Add(p);
                Vector3 lTarget = p + Vector3.up * 1.15f;
                View(lTarget, lNpc.transform.forward + lNpc.transform.right * 0.7f, 6.5f, 0.45f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_patrol{n++}.png"), lFrom, lTarget, 58f)).Append('\n');
            }
            return lLog.ToString();
        }

        /// <summary>
        /// Play mode: a gunshot-loud alarm (blamed on nobody) in the middle of the biggest crowd - the
        /// crowd startles, screams and runs for its exits. <see cref="FleeShots"/> a few seconds later.
        /// </summary>
        public static string Panic()
        {
            AI.Npc[] lNpcs = Object.FindObjectsByType<AI.Npc>(FindObjectsSortMode.None);
            int lBest = -1, lBestCount = 0;
            for (int i = 0; i < lNpcs.Length; i++)
            {
                if (lNpcs[i].Role != AI.NpcRole.Worker) continue;
                int lCount = 0;
                foreach (AI.Npc o in lNpcs) if (o.Role == AI.NpcRole.Worker && Vector3.Distance(o.transform.position, lNpcs[i].transform.position) < 12f) lCount++;
                if (lCount > lBestCount) { lBestCount = lCount; lBest = i; }
            }
            if (lBest < 0) return "no crowd";
            _panic = lNpcs[lBest].transform.position;
            AI.NoiseSystem.Emit(_panic + Vector3.up, 45f, AI.NoiseKind.Alarming, ulong.MaxValue, false);
            return $"panic at {_panic:F0} among {lBestCount}";
        }

        /// <summary>Play mode, after <see cref="Panic"/>: the scene wide from above, then the runners close up.</summary>
        public static string FleeShots(string pDir, string pPrefix)
        {
            Physics.SyncTransforms();
            System.Text.StringBuilder lLog = new System.Text.StringBuilder();
            View(_panic + Vector3.up * 1.2f, Vector3.back, 16f, 7f, out Vector3 lHigh);
            lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_flee_wide.png"), lHigh, _panic + Vector3.up * 0.8f, 66f)).Append('\n');
            int n = 0;
            foreach (AI.Npc lNpc in Object.FindObjectsByType<AI.Npc>(FindObjectsSortMode.None))
            {
                if (n >= 3 || lNpc.Role != AI.NpcRole.Worker || Vector3.Distance(lNpc.transform.position, _panic) > 40f) continue;
                UnityEngine.AI.NavMeshAgent lAgent = lNpc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (lAgent == null || lAgent.velocity.magnitude < 1.5f) continue;
                Vector3 lTarget = lNpc.transform.position + Vector3.up * 1.1f;
                View(lTarget, lNpc.transform.forward, 5f, 0.3f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_flee{n++}.png"), lFrom, lTarget, 60f)).Append('\n');
            }
            return lLog.ToString();
        }

        /// <summary>
        /// Edit mode review set of a map: the whole map from a corner high up, the key building from the
        /// air, the key building at dawn, noon, dusk and night, street fronts with posters and weathering,
        /// a dressed corner (quoins or chamfer), and the grass at eye level.
        /// </summary>
        public static string Review(string pDir, string pPrefix)
        {
            Physics.SyncTransforms();
            System.Text.StringBuilder lLog = new System.Text.StringBuilder();
            Systems.BuildingVolume lKey = null;
            foreach (Systems.BuildingVolume v in Object.FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None))
                if (lKey == null || v.Size.x * v.Size.z > lKey.Size.x * lKey.Size.z) lKey = v;
            Time(1);
            lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_aerial_map.png"), new Vector3(-170f, 190f, -230f), new Vector3(10f, 20f, 20f), 60f)).Append('\n');
            if (lKey != null)
            {
                Vector3 c = lKey.transform.position;
                float r = Mathf.Max(lKey.Size.x, lKey.Size.z);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_aerial_key.png"), c + new Vector3(-r * 0.9f, r * 0.85f, -r * 1.1f), c, 55f)).Append('\n');
                // Four times of day on the key building, from a clear point on its approach.
                // A clear view of the building from its front, a little above the ground.
                Vector3 lTo = c + Vector3.up * 2f;
                View(lTo, -lKey.transform.forward + lKey.transform.right * 0.6f, r * 0.9f, 6f, out Vector3 lFrom, true);
                string[] lNames = { "dawn", "noon", "dusk", "night" };
                for (int t = 0; t < 4; t++)
                {
                    Time(t);
                    lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_time_{lNames[t]}.png"), lFrom, lTo, 62f)).Append('\n');
                }
                Time(1);
            }
            // Street fronts: posters, weathering and door dressing, at eye level down the street's axis.
            int n = 0;
            System.Collections.Generic.List<Vector3> lSeen = new System.Collections.Generic.List<Vector3>();
            System.Collections.Generic.List<(Vector3 at, Vector3 outward)> lFronts = new System.Collections.Generic.List<(Vector3, Vector3)>();
            foreach (UnityEngine.Rendering.Universal.DecalProjector d in Object.FindObjectsByType<UnityEngine.Rendering.Universal.DecalProjector>(FindObjectsSortMode.None))
                if (d.name.StartsWith("Decal_Poster")) lFronts.Add((d.transform.position, -d.transform.forward));
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "StreetProps")
                    foreach (Transform p in t) lFronts.Add((p.position + Vector3.up * 1.6f, OpenSide(p.position + Vector3.up * 0.6f)));
            foreach ((Vector3 p, Vector3 lOutward) in lFronts)
            {
                if (n >= 3) break;
                if (lSeen.Exists(o => Vector3.Distance(o, p) < 40f)) continue;
                Vector3 lOut = lOutward;
                lOut.y = 0f;
                if (lOut.sqrMagnitude < 0.01f) continue;
                lOut.Normalize();
                Vector3 lAxis = Vector3.Cross(Vector3.up, lOut);
                foreach (float lSign in new[] { 1f, -1f })
                {
                    Vector3 lFrom = p + lOut * 2.6f - lAxis * lSign * 9f;
                    lFrom = Eye(lFrom.x, lFrom.z, 1.6f);
                    Vector3 lTo = p + lOut * 1.0f + lAxis * lSign * 8f;
                    lTo.y = lFrom.y + 0.4f;
                    if (Indoors(lFrom) || !Frames(lFrom, lTo, out _)) continue;
                    lSeen.Add(p);
                    lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_street{n++}.png"), lFrom, lTo, 64f)).Append('\n');
                    break;
                }
            }
            // A dressed corner of the key building (quoins or a chamfer), from the ground.
            if (lKey != null)
            {
                Vector3 lCorner = lKey.transform.position + lKey.transform.rotation * new Vector3(-lKey.Size.x * 0.5f, 0f, -lKey.Size.z * 0.5f);
                // Down the street along the facade: from beyond the corner, the corner near in the frame.
                Vector3 lAlong = lKey.transform.rotation * Vector3.right, lFront = lKey.transform.rotation * Vector3.back;
                Vector3 lFrom = lCorner - lAlong * 9f + lFront * 3.2f;
                lFrom = MapShots.Eye(lFrom.x, lFrom.z, 1.6f);
                Vector3 lTo = lCorner + lAlong * 9f + lFront * 0.8f;
                lTo.y = lFrom.y + 2.0f;
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_corner.png"), lFrom, lTo, 64f)).Append('\n');
            }
            // Grass at eye level: standing on painted grass, looking over a tall grass field with 15 m clear ahead.
            bool lGrassShot = false;
            int lTried = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (lGrassShot || t.name != "TallGrass") continue;
                for (int i = 0; i < t.childCount && !lGrassShot && lTried < 400; i += Mathf.Max(1, t.childCount / 24))
                {
                    Vector3 g = t.GetChild(i).position;
                    for (int k = 0; k < 8 && !lGrassShot; k++, lTried++)
                    {
                        Vector3 d = Quaternion.Euler(0, k * 45f, 0) * Vector3.forward;
                        Vector3 lFrom = Eye(g.x - d.x * 9f, g.z - d.z * 9f, 1.6f), lTo = Eye(g.x, g.z, 0.9f);
                        if (!OnGrass(lFrom) || !OnGrass(new Vector3(g.x, 0f, g.z)) || Indoors(lFrom)) continue;
                        Vector3 lFlat = lTo - lFrom;
                        lFlat.y = 0f;
                        if (Physics.Raycast(lFrom, lFlat.normalized, 15f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (!Frames(lFrom, lTo, out _)) continue;
                        lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_grass_eye.png"), lFrom, lTo, 64f)).Append('\n');
                        lGrassShot = true;
                    }
                }
            }
            if (!lGrassShot) _rerolls.Append($"{pPrefix}_grass_eye.png: no grass point with 15 m clear, dropped\n");
            return lLog.ToString();
        }

        /// <summary>Play mode: shots of the busiest groups of NPCs, each from a clear viewpoint.</summary>
        public static string NpcShots(string pDir, string pPrefix, int pCount)
        {
            Physics.SyncTransforms();
            AI.Npc[] lNpcs = Object.FindObjectsByType<AI.Npc>(FindObjectsSortMode.None);
            System.Collections.Generic.List<Vector3> lDone = new System.Collections.Generic.List<Vector3>();
            System.Text.StringBuilder lLog = new System.Text.StringBuilder();
            for (int shot = 0; shot < pCount; shot++)
            {
                int lBest = -1, lBestCount = 0;
                for (int i = 0; i < lNpcs.Length; i++)
                {
                    Vector3 p = lNpcs[i].transform.position;
                    bool lNear = false;
                    foreach (Vector3 d in lDone) if (Vector3.Distance(d, p) < 30f) lNear = true;
                    if (lNear) continue;
                    int lCount = 0;
                    foreach (AI.Npc o in lNpcs) if (Vector3.Distance(o.transform.position, p) < 9f) lCount++;
                    if (lCount > lBestCount) { lBestCount = lCount; lBest = i; }
                }
                if (lBest < 0) break;
                Vector3 lCentre = Vector3.zero;
                int lIn = 0;
                foreach (AI.Npc o in lNpcs) if (Vector3.Distance(o.transform.position, lNpcs[lBest].transform.position) < 9f) { lCentre += o.transform.position; lIn++; }
                lCentre = lCentre / lIn + Vector3.up * 1.1f;
                lDone.Add(lCentre);
                View(lCentre, lNpcs[lBest].transform.forward, 8f, 1.3f, out Vector3 lFrom);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_npc{shot}_{lIn}.png"), lFrom, lCentre, 60f)).Append('\n');
            }
            return lLog.ToString();
        }

        /// <summary>
        /// Into the sun over open ground: from eye height at (x, z), facing the sun's bearing and looking a
        /// little down, so the sun sits just above the frame (snow glints, back-lit grass).
        /// </summary>
        public static string SunShot(string pPath, float pX, float pZ, float pPitch = 9f)
        {
            Light lSun = RenderSettings.sun;
            if (lSun == null) foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { lSun = l; break; }
            Vector3 lToSun = lSun != null ? -lSun.transform.forward : Vector3.forward;
            lToSun.y = 0f;
            if (lToSun.sqrMagnitude < 0.001f) lToSun = Vector3.forward;
            lToSun.Normalize();
            Vector3 lFrom = Eye(pX, pZ, 1.7f);
            Vector3 lLook = Quaternion.AngleAxis(pPitch, Vector3.Cross(Vector3.up, lToSun)) * lToSun;
            return Shot(pPath, lFrom, lFrom + lLook * 20f, 66f);
        }

        /// <summary>A point <paramref name="pUp"/> metres over the land (terrain only, not roofs).</summary>
        public static Vector3 Eye(float pX, float pZ, float pUp = 1.7f)
        {
            Terrain lLand = Terrain.activeTerrain;
            float y = lLand != null ? lLand.SampleHeight(new Vector3(pX, 0, pZ)) + lLand.transform.position.y : 0f;
            return new Vector3(pX, y + pUp, pZ);
        }

        /// <summary>Applies one of the map's time-of-day presets in edit mode (0 dawn, 1 noon, 2 dusk, 3 night).</summary>
        public static void Time(int pPreset)
        {
            FX.TimeOfDay lTime = Object.FindFirstObjectByType<FX.TimeOfDay>();
            if (lTime == null) return;
            System.Reflection.MethodInfo lApply = typeof(FX.TimeOfDay).GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (lApply != null) lApply.Invoke(lTime, new object[] { pPreset });
        }

        [MenuItem("Goobyr/Debug/Arch Test Scene")]
        public static void ArchTest()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ArchLib.Reset();
            ArchBuilder.MeshPrefix = "ArchTest";
            GameObject lGround = new GameObject("Ground");
            ArchMesh lMesh = new ArchMesh();
            lMesh.BoxMinMax(ArchLib.Surface("leafy_grass", 4f), new Vector3(-60, -6, -60), new Vector3(100, 0, 100));
            lMesh.Flush(lGround.transform, "ArchTest_Ground");
            GameObject lSun = new GameObject("Sun");
            Light lLight = lSun.AddComponent<Light>();
            lLight.type = LightType.Directional;
            lLight.intensity = 2.2f;
            lLight.shadows = LightShadows.Soft;
            lLight.color = new Color(1f, 0.95f, 0.88f);
            lSun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = lLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.25f);
            Transform lRoot = new GameObject("ArchTest").transform;
            ArchResult r = ArchBuilder.Build(MapSanLazaro.CasaSpec(Vector3.zero, 0f), lRoot);
            MapKit lFake = null;
            _ = lFake;
            _ = r;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), EditorAssets.SCENES + "/ArchTest.unity");
        }
    }
}
