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

        /// <summary>The first viewpoint round a target (distance, height) with a clear line of sight to it.</summary>
        private static bool View(Vector3 pTarget, Vector3 pPreferred, float pDistance, float pUp, out Vector3 pFrom)
        {
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
                        Vector3 c = z.transform.position;
                        Vector3 lHalf = new Vector3(z.Size.x * 0.5f - 0.6f, 0f, z.Size.z * 0.5f - 0.6f);
                        Vector3 lFrom = c + z.transform.rotation * new Vector3(-lHalf.x, 1.75f, -lHalf.z);
                        for (int tries = 0; tries < 4 && !Clear(lFrom); tries++) lFrom = c + z.transform.rotation * Quaternion.Euler(0, 90f * (tries + 1), 0) * new Vector3(-lHalf.x, 1.75f, -lHalf.z);
                        Vector3 lTo = c + z.transform.rotation * new Vector3(lHalf.x * 0.4f, 1.0f, lHalf.z * 0.4f);
                        string lName = z.name.Replace("Zone_", "").Split('_')[0];
                        lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_f{lFloor.Key}_{lName.ToLowerInvariant()}.png"), lFrom, lTo, 72f)).Append('\n');
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
                View(lTo, -lKey.transform.forward + lKey.transform.right * 0.6f, r * 0.9f, 6f, out Vector3 lFrom);
                string[] lNames = { "dawn", "noon", "dusk", "night" };
                for (int t = 0; t < 4; t++)
                {
                    Time(t);
                    lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_time_{lNames[t]}.png"), lFrom, lTo, 62f)).Append('\n');
                }
                Time(1);
            }
            // Street fronts: posters and weathering on the town houses, at eye level from across the street.
            int n = 0;
            System.Collections.Generic.List<Vector3> lSeen = new System.Collections.Generic.List<Vector3>();
            foreach (UnityEngine.Rendering.Universal.DecalProjector d in Object.FindObjectsByType<UnityEngine.Rendering.Universal.DecalProjector>(FindObjectsSortMode.None))
            {
                if (n >= 3 || !d.name.StartsWith("Decal_Poster")) continue;
                Vector3 p = d.transform.position;
                if (lSeen.Exists(o => Vector3.Distance(o, p) < 40f)) continue;
                lSeen.Add(p);
                Vector3 lOut = -d.transform.forward;
                // From across the street, with a clear line to the poster (else the next poster).
                if (!View(p, lOut + Vector3.Cross(Vector3.up, lOut) * 0.35f, 6.5f, 0f, out Vector3 lFrom)) continue;
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_street{n++}.png"), lFrom, p + Vector3.down * 0.5f, 62f)).Append('\n');
            }
            // A dressed corner of the key building (quoins or a chamfer), from the ground.
            if (lKey != null)
            {
                Vector3 lCorner = lKey.transform.position + lKey.transform.rotation * new Vector3(-lKey.Size.x * 0.5f, 0f, -lKey.Size.z * 0.5f);
                Vector3 lOut = lKey.transform.rotation * new Vector3(-1f, 0f, -1f).normalized;
                Vector3 lBase = MapShots.Eye(lCorner.x + lOut.x * 7f, lCorner.z + lOut.z * 7f, 1.6f);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_corner.png"), lBase, new Vector3(lCorner.x, lBase.y + 2.5f, lCorner.z), 64f)).Append('\n');
            }
            // Grass at eye level: toward the first tall grass field, from 9 m away.
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name != "TallGrass" || t.childCount == 0) continue;
                Vector3 g = t.GetChild(0).position;
                Vector3 lFrom = MapShots.Eye(g.x - 9f, g.z - 6f, 1.6f);
                lLog.Append(Shot(Path.Combine(pDir, $"{pPrefix}_grass_eye.png"), lFrom, MapShots.Eye(g.x, g.z, 0.9f), 64f)).Append('\n');
                break;
            }
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
