using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Play-mode check of the new cast on Map_SanLazaro (<c>Goobyr/Cast/Showcase (San Lazaro)</c>): hosts
    /// a match, then at noon and at dusk renders faces of the crowd and the guards, a guard spotting the
    /// player (surprise, then anger), the crowd fleeing a shot, and a body killed on the spot (ragdoll, X
    /// eyes). Shots and a log go to Logs/cast_showcase; the scene that was open comes back afterwards.
    /// </summary>
    [InitializeOnLoad]
    public static class CastShowcase
    {
        private const string PENDING = "Goobyr.CastShowcase.Pending", RESTORE = "Goobyr.CastShowcase.Restore";
        private const string MAP = "Assets/_Game/Scenes/Map_SanLazaro.unity";
        public static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "cast_showcase"));

        static CastShowcase() { EditorApplication.playModeStateChanged += HandlePlayMode; }

        [MenuItem("Goobyr/Cast/Showcase (San Lazaro)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Cast] showcase: already in play mode"); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Cast] showcase: unsaved scene changes - save or revert first"); return; }
            SessionState.SetString(RESTORE, SceneManager.GetActiveScene().path);
            SessionState.SetBool(PENDING, true);
            EditorSceneManager.OpenScene(MAP, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        public const string DRILL_ONLY = "Goobyr.CastShowcase.DrillOnly";

        /// <summary>Only the gait drill (quicker loop while tuning the feet).</summary>
        [MenuItem("Goobyr/Cast/Gait Drill (San Lazaro)")]
        public static void DrillOnly()
        {
            SessionState.SetBool(DRILL_ONLY, true);
            Run();
        }

        private const string TITLE_PENDING = "Goobyr.CastShowcase.Title", TITLE = "Assets/_Game/Scenes/Title.unity";

        /// <summary>The title screen's key art (TitleDiorama, the hero) rendered through its own camera to Logs/cast_showcase/title.png.</summary>
        [MenuItem("Goobyr/Cast/Title Shot")]
        public static void TitleShot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Cast] title shot: already in play mode"); return; }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[Cast] title shot: unsaved scene changes - save or revert first"); return; }
            SessionState.SetString(RESTORE, SceneManager.GetActiveScene().path);
            SessionState.SetBool(TITLE_PENDING, true);
            EditorSceneManager.OpenScene(TITLE, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static IEnumerator RunTitle()
        {
            yield return new WaitForSecondsRealtime(4f);
            Camera lCamera = Camera.main;
            if (lCamera == null) foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.enabled) { lCamera = c; break; }
            if (lCamera != null)
            {
                Directory.CreateDirectory(OutputFolder);
                RenderTexture lRt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                RenderTexture lOld = lCamera.targetTexture;
                lCamera.targetTexture = lRt;
                lCamera.Render();
                lCamera.targetTexture = lOld;
                RenderTexture lPrev = RenderTexture.active;
                RenderTexture.active = lRt;
                Texture2D lTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                lTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                lTex.Apply();
                RenderTexture.active = lPrev;
                File.WriteAllBytes(Path.Combine(OutputFolder, "title.png"), lTex.EncodeToPNG());
                Object.Destroy(lTex);
                lRt.Release();
                Debug.Log("[Cast] title shot -> " + Path.Combine(OutputFolder, "title.png"));
            }
            else Debug.LogError("[Cast] title shot: no camera");
            EditorApplication.isPlaying = false;
        }

        private static void HandlePlayMode(PlayModeStateChange pChange)
        {
            if (pChange == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(TITLE_PENDING, false))
            {
                SessionState.SetBool(TITLE_PENDING, false);
                CoroutineHost.Create("CastTitleShot").StartCoroutine(RunTitle());
            }
            else if (pChange == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PENDING, false))
            {
                SessionState.SetBool(PENDING, false);
                CastShowcaseRunner lRunner = new CastShowcaseRunner { OutputFolder = OutputFolder };
                CoroutineHost.Create("CastShowcaseRunner").StartCoroutine(lRunner.Run());
            }
            else if (pChange == PlayModeStateChange.EnteredEditMode)
            {
                string lScene = SessionState.GetString(RESTORE, "");
                if (string.IsNullOrEmpty(lScene)) return;
                SessionState.EraseString(RESTORE);
                if (File.Exists(Path.Combine(Application.dataPath, "..", lScene))) EditorSceneManager.OpenScene(lScene, OpenSceneMode.Single);
            }
        }
    }

    public class CastShowcaseRunner
    {
        public string OutputFolder { get; set; }
        private PlayerAvatar _player;
        private readonly StringBuilder _log = new StringBuilder();
        private const string GAME_ROOT = "Assets/_Game/Prefabs/Net/P_GameRoot.prefab";
        private static readonly int SCENERY = ~((1 << GameLayers.NPC) | (1 << GameLayers.PLAYER) | (1 << GameLayers.RAGDOLL) | (1 << GameLayers.LOCAL_VIEW));

        public IEnumerator Run()
        {
            Directory.CreateDirectory(OutputFolder);
            yield return StartMatch();
            bool lDrillOnly = SessionState.GetBool(CastShowcase.DRILL_ONLY, false);
            SessionState.SetBool(CastShowcase.DRILL_ONLY, false);
            if (_player != null && lDrillOnly)
            {
                yield return new WaitForSeconds(2f);
                yield return Drill();
            }
            else if (_player != null)
            {
                yield return new WaitForSeconds(3f);
                Portraits();
                foreach (int lTime in new[] { 1, 2 })
                {
                    LobbyState.Instance.TimeOfDay.Value = lTime;
                    yield return new WaitForSeconds(1.5f);
                    yield return Faces(lTime == 1 ? "noon" : "dusk");
                }
                LobbyState.Instance.TimeOfDay.Value = 1;
                yield return new WaitForSeconds(1f);
                yield return Measure("calm", 4f);
                yield return Drill();
                yield return Spotted();
                yield return Measure("combat", 3f);
                yield return Flee();
                yield return Death();
            }
            File.WriteAllText(Path.Combine(OutputFolder, lDrillOnly ? "drill.txt" : "showcase.txt"), _log.ToString());
            Debug.Log("[Cast] showcase done -> " + OutputFolder);
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return new WaitForSecondsRealtime(0.5f);
            GameInput.LockCursor(false);
            EditorApplication.isPlaying = false;
        }

        private void Log(string pLine) { _log.AppendLine(pLine); Debug.Log("[Cast] showcase: " + pLine); }

        private IEnumerator StartMatch()
        {
            GameObject lRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GAME_ROOT));
            NetworkManager lNet = lRoot.GetComponentInChildren<NetworkManager>();
            if (lNet != null) lNet.NetworkConfig.EnableSceneManagement = false;
            yield return null;
            yield return null;
            NetSession.Instance.HostLocal();
            float lUntil = Time.realtimeSinceStartup + 20f;
            while (LobbyState.Instance == null || LobbyPlayer.Agents().Count == 0)
            {
                if (Time.realtimeSinceStartup > lUntil) { Log("the host did not start"); yield break; }
                yield return null;
            }
            List<MapDefinition> lMaps = GameDatabase.Instance.maps;
            for (int i = 0; i < lMaps.Count; i++) if (lMaps[i] != null && lMaps[i].sceneName == SceneManager.GetActiveScene().name) LobbyState.Instance.MapIndex.Value = i;
            NetworkObject lSession = Object.Instantiate(NetPrefabs.Instance.mapSession);
            lSession.Spawn(true);
            lUntil = Time.realtimeSinceStartup + 30f;
            while (PlayerAvatar.Local == null || Npc.All.Count < 10)
            {
                if (Time.realtimeSinceStartup > lUntil) { Log("the match did not start"); yield break; }
                yield return null;
            }
            _player = PlayerAvatar.Local;
            _player.Health.InitHealth(100000f);
            Dictionary<string, int> lCast = new Dictionary<string, int>();
            foreach (Npc lNpc in Npc.All)
            {
                HumanBody lBody = lNpc != null ? lNpc.GetComponentInChildren<HumanBody>() : null;
                string lName = lBody != null ? lBody.Character : "none";
                lCast[lName] = lCast.TryGetValue(lName, out int n) ? n + 1 : 1;
            }
            StringBuilder lLine = new StringBuilder($"match on {SceneManager.GetActiveScene().name}: {Npc.All.Count} NPCs;");
            foreach (KeyValuePair<string, int> lPair in lCast) lLine.Append($" {lPair.Key} x{lPair.Value}");
            Log(lLine.ToString());
        }

        private static bool Outdoors(Vector3 pAt) => !Physics.Raycast(pAt + Vector3.up * 0.5f, Vector3.up, 12f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);

        /// <summary>A portrait camera spot in front of a face with nothing in between; false when boxed in.</summary>
        private static bool FaceSpot(Npc pNpc, float pDistance, out Vector3 pFrom, out Vector3 pTo)
        {
            pTo = pNpc.Eye + Vector3.down * 0.06f;
            Vector3 lForward = pNpc.transform.forward;
            foreach (float lYaw in new[] { 15f, -20f, 40f, -45f })
            {
                pFrom = pTo + Quaternion.Euler(0f, lYaw, 0f) * lForward * pDistance + Vector3.up * 0.05f;
                if (!Physics.Linecast(pFrom, pTo, SCENERY, QueryTriggerInteraction.Ignore) && !Physics.CheckSphere(pFrom, 0.2f, SCENERY, QueryTriggerInteraction.Ignore)) return true;
            }
            pFrom = Vector3.zero;
            return false;
        }

        private IEnumerator Shot(string pName, Vector3 pFrom, Vector3 pTo, float pFov = 30f, int pW = 1000, int pH = 1000)
        {
            yield return new WaitForEndOfFrame();
            MapShots.Shot(Path.Combine(OutputFolder, pName + ".png"), pFrom, pTo, pFov, pW, pH);
        }

        /// <summary>Close-ups of different characters of the crowd and the guards, and one wide group shot.</summary>
        private IEnumerator Faces(string pTag)
        {
            HashSet<string> lDone = new HashSet<string>();
            int lCount = 0;
            Npc lWide = null;
            foreach (Npc lNpc in Npc.All)
            {
                if (lCount >= 7) break;
                if (lNpc == null || !lNpc.Health.IsAlive || !Outdoors(lNpc.transform.position)) continue;
                HumanBody lBody = lNpc.GetComponentInChildren<HumanBody>();
                if (lBody == null || lDone.Contains(lBody.Character)) continue;
                if (!FaceSpot(lNpc, 1.1f, out Vector3 lFrom, out Vector3 lTo)) continue;
                lDone.Add(lBody.Character);
                if (lWide == null && lNpc.IsCrowd) lWide = lNpc;
                yield return Shot($"{pTag}_face_{lBody.Character}", lFrom, lTo, 32f);
                lCount++;
            }
            if (lWide != null && FaceSpot(lWide, 6f, out Vector3 lWFrom, out Vector3 lWTo))
                yield return Shot($"{pTag}_group", lWFrom + Vector3.up * 0.6f, lWTo + Vector3.down * 0.6f, 50f, 1600, 900);
            Log($"{pTag}: {lCount} faces ({string.Join(", ", lDone)})");
            yield return Heroes(pTag);
        }

        /// <summary>
        /// Rook M and F stood in the sun near the crowd, turned three-quarters to it, under the map's own
        /// light (the player's own body is shadow-only in first person).
        /// </summary>
        private IEnumerator Heroes(string pTag)
        {
            Light lSun = RenderSettings.sun;
            if (lSun == null) foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && (lSun == null || l.intensity > lSun.intensity)) lSun = l;
            Vector3 lToSun = lSun != null ? -lSun.transform.forward : Vector3.up;
            Vector3 lBase = Vector3.zero;
            bool lFound = false;
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || !lNpc.IsCrowd || !Outdoors(lNpc.transform.position)) continue;
                foreach (Vector3 lSide in new[] { lNpc.transform.right, -lNpc.transform.right, lNpc.transform.forward })
                {
                    if (!NavMesh.SamplePosition(lNpc.transform.position + lSide * 1.5f, out NavMeshHit lHit, 1f, NavMesh.AllAreas)) continue;
                    if (Physics.Raycast(lHit.position + Vector3.up * 1.6f, lToSun, 60f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    lBase = lHit.position;
                    // on the ground itself (the nav mesh floats a little above it)
                    if (Physics.Raycast(lBase + Vector3.up * 0.5f, Vector3.down, out RaycastHit lGround, 1.5f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) lBase = lGround.point;
                    lFound = true;
                    break;
                }
                if (lFound) break;
            }
            if (!lFound) { Log($"{pTag}: no sunlit spot for the heroes"); yield break; }
            Vector3 lFlat = new Vector3(lToSun.x, 0f, lToSun.z);
            Quaternion lFacing = Quaternion.LookRotation(lFlat.sqrMagnitude > 1e-4f ? lFlat.normalized : Vector3.forward) * Quaternion.Euler(0f, 35f, 0f);
            // the player stands just behind them (unseen, shadow-only body): near the game camera, so the look IK runs as in play
            if (NavMesh.SamplePosition(lBase - lFacing * Vector3.forward * 4f, out NavMeshHit lBehind, 3f, NavMesh.AllAreas)) _player.Motor.Teleport(lBehind.position + Vector3.up * 0.1f, lFacing.eulerAngles.y);
            foreach (string lCharacter in new[] { HumanCast.AGENT, HumanCast.AGENT_F })
            {
                GameObject lGo = new GameObject("ShowcaseHero");
                lGo.transform.SetPositionAndRotation(lBase, lFacing);
                HumanBody lBody = lGo.AddComponent<HumanBody>();
                lBody.Culling = AnimatorCullingMode.AlwaysAnimate;
                HumanAnimator lAnim = lGo.AddComponent<HumanAnimator>();
                lAnim.Velocity = Vector3.zero;
                lBody.Configure(lCharacter, Color.clear);
                yield return new WaitForSeconds(0.8f);
                Vector3 lFace = lBody.Head != null ? lBody.Head.position + Vector3.up * 0.03f : lBase + Vector3.up * 1.6f;
                foreach (float lYaw in new[] { 25f, -30f })
                {
                    Vector3 lFrom = lFace + Quaternion.Euler(0f, lYaw, 0f) * lGo.transform.forward * 1.15f + Vector3.up * 0.04f;
                    yield return Shot($"{pTag}_hero_{lCharacter}_{(lYaw > 0 ? "l" : "r")}", lFrom, lFace + Vector3.down * 0.05f, 30f);
                }
                yield return Shot($"{pTag}_hero_{lCharacter}_full", lFace + Quaternion.Euler(0f, 15f, 0f) * lGo.transform.forward * 3.4f + Vector3.down * 0.5f, lFace + Vector3.down * 0.78f, 40f, 900, 1200);
                Object.Destroy(lGo);
                yield return null;
            }
        }

        /// <summary>
        /// Rook driven along a straight line at the player's speeds (run forward / left / right, sprint,
        /// crouch forward / left, an NPC walk) next to the player camera, foot IK and push-out on: the
        /// stance foot's world speed and the hands' distance to the torso axis per gait, and a frame each.
        /// </summary>
        private IEnumerator Drill()
        {
            Vector3 lSpot = _player.transform.position + _player.transform.forward * 6f;
            Vector3 lFwd = Vector3.ProjectOnPlane(_player.transform.right, Vector3.up).normalized;
            bool lClear = false;
            for (int a = 0; a < 16 && !lClear; a++)
            {
                Vector3 lDir = Quaternion.Euler(0f, a * 22.5f, 0f) * lFwd;
                Vector3 lCentre = _player.transform.position + lDir * 6f;
                lClear = true;
                for (int k = -6; k <= 6 && lClear; k++)
                    foreach (Vector3 lAxis in new[] { lDir, Vector3.Cross(Vector3.up, lDir) })
                        if (!NavMesh.SamplePosition(lCentre + lAxis * k, out NavMeshHit lHit, 0.3f, NavMesh.AllAreas) || Mathf.Abs(lHit.position.y - lCentre.y) > 0.3f) { lClear = false; break; }
                if (lClear) { lSpot = lCentre; lFwd = lDir; }
            }
            if (Physics.Raycast(lSpot + Vector3.up, Vector3.down, out RaycastHit lGround, 3f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) lSpot = lGround.point;
            Log($"drill at {lSpot:F0} ({(lClear ? "open ground" : "no open 12 m cross found, nearest spot")})");
            Vector3 lRight = Vector3.Cross(Vector3.up, lFwd);
            (string, Vector3, float, bool)[] lRuns =
            {
                ("walk_f", lFwd, 1.4f, false), ("run_f", lFwd, 4.4f, false), ("run_l", -lRight, 4.4f, false), ("run_r", lRight, 4.4f, false),
                ("sprint_f", lFwd, 7.2f, false), ("crouch_f", lFwd, 2.3f, true), ("crouch_l", -lRight, 2.3f, true), ("crouch_r", lRight, 2.3f, true),
            };
            GameObject lGo = new GameObject("ShowcaseDrill");
            lGo.transform.SetPositionAndRotation(lSpot, Quaternion.LookRotation(lFwd));
            HumanBody lBody = lGo.AddComponent<HumanBody>();
            lBody.Culling = AnimatorCullingMode.AlwaysAnimate;
            HumanAnimator lAnim = lGo.AddComponent<HumanAnimator>();
            lBody.Configure(HumanCast.AGENT, Color.clear);
            lAnim.Velocity = Vector3.zero;
            yield return new WaitForSeconds(0.5f);
            foreach ((string lName, Vector3 lDir, float lSpeed, bool lCrouch) in lRuns)
            {
                float lSeconds = Mathf.Clamp(10f / lSpeed, 1.6f, 3f);
                Vector3 lStart = lSpot - lDir * lSpeed * lSeconds * 0.5f;
                lGo.transform.position = lStart;
                lAnim.Crouched = lCrouch;
                lAnim.Sprinting = lName.StartsWith("sprint");
                lAnim.Velocity = lDir * lSpeed;
                List<Vector3>[] lFeet = { new List<Vector3>(), new List<Vector3>() };
                List<Vector2>[] lAlong = { new List<Vector2>(), new List<Vector2>() };
                List<float> lHands = new List<float>();
                Vector3[] lPrev = new Vector3[2];
                float lT0 = Time.time, lPaceSum = 0f, lSpeedSum = 0f;
                Vector2 lMoveSum = Vector2.zero;
                int lFrames = 0;
                bool lShot = false;
                while (Time.time - lT0 < lSeconds)
                {
                    yield return new WaitForEndOfFrame();
                    float dt = Mathf.Max(Time.deltaTime, 0.0001f), t = Time.time - lT0;
                    lGo.transform.position = lStart + lDir * lSpeed * t;
                    if (t < 0.6f) { for (int f = 0; f < 2; f++) lPrev[f] = Foot(lBody, f); continue; }   // settle into the gait
                    float lScale = lBody.Model != null ? lBody.Model.lossyScale.y : 1f;
                    lPaceSum += lBody.Animator.GetFloat("Pace");
                    lMoveSum += new Vector2(lBody.Animator.GetFloat("MoveX"), lBody.Animator.GetFloat("MoveZ"));
                    lSpeedSum += lBody.Animator.GetCurrentAnimatorStateInfo(0).speed * lBody.Animator.GetCurrentAnimatorStateInfo(0).speedMultiplier * lBody.Animator.speed;
                    lFrames++;
                    for (int f = 0; f < 2; f++)
                    {
                        Vector3 lFoot = Foot(lBody, f), lD = lFoot - lPrev[f];
                        lD.y = 0f;
                        lFeet[f].Add(new Vector3((lFoot.y - lGo.transform.position.y) / lScale, lD.magnitude / dt, lAnim.FootLocked(f) ? 1f : 0f));
                        lAlong[f].Add(new Vector2((lFoot.y - lGo.transform.position.y) / lScale, Vector3.Dot(lD, lDir) / dt));
                        lPrev[f] = lFoot;
                    }
                    if (lBody.Hips != null && lBody.Chest != null)
                        foreach (Transform lHand in new[] { lBody.HandL, lBody.HandR })
                        {
                            Vector3 lAxis = lBody.Chest.position - lBody.Hips.position;
                            float u = Mathf.Clamp01(Vector3.Dot(lHand.position - lBody.Hips.position, lAxis) / Mathf.Max(lAxis.sqrMagnitude, 1e-4f));
                            Vector3 lAway = lHand.position - (lBody.Hips.position + lAxis * u);
                            lAway.y = 0f;
                            lHands.Add(lAway.magnitude / lScale);
                        }
                    if (!lShot && t > lSeconds * 0.55f)
                    {
                        lShot = true;
                        Vector3 lAt = lGo.transform.position + Vector3.up * (lCrouch ? 0.7f : 1.0f);
                        Vector3 lFrom = lAt + Quaternion.Euler(0f, 25f, 0f) * lGo.transform.forward * 3.2f + Vector3.up * 0.3f;
                        MapShots.Shot(Path.Combine(OutputFolder, $"drill_{lName}.png"), lFrom, lAt, 42f, 900, 900);
                    }
                }
                List<float> lStance = new List<float>();
                int lLocked = 0;
                foreach (List<Vector3> lTrack in lFeet)
                {
                    float lLow = float.MaxValue;
                    foreach (Vector3 v in lTrack) lLow = Mathf.Min(lLow, v.x);
                    foreach (Vector3 v in lTrack) if (v.x <= lLow + 0.03f) { lStance.Add(v.y); if (v.z > 0.5f) lLocked++; }
                }
                lHands.Sort();
                int lInside = 0;
                foreach (float h in lHands) if (h < 0.13f) lInside++;
                // the clip's own ground speed implied by the stance foot (world speed along the run = body speed - pace x ring x stride)
                List<float> lSigned = new List<float>();
                foreach (List<Vector2> lTrack in lAlong)
                {
                    float lLow = float.MaxValue;
                    foreach (Vector2 v in lTrack) lLow = Mathf.Min(lLow, v.x);
                    foreach (Vector2 v in lTrack) if (v.x <= lLow + 0.03f) lSigned.Add(v.y);
                }
                float lStride = lBody.Animator.humanScale / HumanAnimator.REF_SCALE * (lBody.Model != null ? lBody.Model.lossyScale.y : 1f);
                float lPaceMean = lPaceSum / Mathf.Max(1, lFrames);
                float lRing = (lSpeed - Median(lSigned)) / Mathf.Max(0.01f, lPaceMean * lStride);
                Log($"drill {lName} implied clip ground speed at REF_SCALE {lRing:0.000} m/s (signed stance median {Median(lSigned):0.000})");
                Log($"drill {lName} at {lSpeed:0.0} m/s: stance-foot world speed median {Median(lStance):0.000} m/s ({lStance.Count} samples, locked {(lStance.Count > 0 ? 100f * lLocked / lStance.Count : 0f):0}%); hands to torso axis min {(lHands.Count > 0 ? lHands[0] : -1f):0.000} m, under 0.13 m {(lHands.Count > 0 ? 100f * lInside / lHands.Count : 0f):0.0}%; pace {lPaceSum / Mathf.Max(1, lFrames):0.00}, move {lMoveSum / Mathf.Max(1, lFrames):F2}, state speed {lSpeedSum / Mathf.Max(1, lFrames):0.00}, humanScale {lBody.Animator.humanScale:0.000}, model scale {(lBody.Model != null ? lBody.Model.lossyScale.y : 1f):0.000}");
            }
            Object.Destroy(lGo);
        }

        private static Vector3 Foot(HumanBody pBody, int pIndex)
        {
            Transform lFoot = pBody.Animator != null ? pBody.Animator.GetBoneTransform(pIndex == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot) : null;
            return lFoot != null ? lFoot.position : Vector3.zero;
        }

        /// <summary>The contract portraits (PortraitStudio) of the players and the targets, as saved PNGs.</summary>
        private void Portraits()
        {
            RenderTexture lRt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            Texture2D lTex = new Texture2D(512, 512, TextureFormat.RGB24, false);
            foreach (string lCharacter in new[] { HumanCast.AGENT, HumanCast.AGENT_F, HumanCast.VIP, HumanCast.VIP_F })
            {
                Com.IsartDigital.GoobyrFPS.UI.PortraitStudio.Shoot(lCharacter, lRt);
                RenderTexture lPrev = RenderTexture.active;
                RenderTexture.active = lRt;
                lTex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                lTex.Apply();
                RenderTexture.active = lPrev;
                File.WriteAllBytes(Path.Combine(OutputFolder, $"portrait_{lCharacter}.png"), lTex.EncodeToPNG());
            }
            Object.Destroy(lTex);
            lRt.Release();
            Log("portraits: Rook, Rook F, VIP M, VIP F");
        }

        /// <summary>The player walks into a guard's view with a gun out: the guard's face, frame by frame.</summary>
        private IEnumerator Spotted()
        {
            Npc lGuard = null;
            Vector3 lStand = Vector3.zero;
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || !lNpc.Health.IsAlive || lNpc.Role != NpcRole.Guard || lNpc.State.Value != NpcState.Idle || !Outdoors(lNpc.transform.position)) continue;
                Vector3 lWish = lNpc.transform.position + lNpc.transform.forward * 8f;
                if (!NavMesh.SamplePosition(lWish, out NavMeshHit lHit, 1.5f, NavMesh.AllAreas) || Mathf.Abs(lHit.position.y - lNpc.transform.position.y) > 1.5f) continue;
                if (!Senses.Clear(lNpc.Eye, lHit.position + Vector3.up * 1.15f) || !FaceSpot(lNpc, 1.4f, out _, out _)) continue;
                lGuard = lNpc;
                lStand = lHit.position;
                break;
            }
            if (lGuard == null) { Log("no guard to surprise"); yield break; }
            Vector3 lTo = lGuard.transform.position - lStand;
            float lYaw = Quaternion.LookRotation(new Vector3(lTo.x, 0f, lTo.z)).eulerAngles.y;
            _player.Motor.Teleport(lStand + Vector3.up * 0.1f, lYaw);
            _player.SetLook(lYaw, 4f);
            _player.Weapons.Selected.Value = 0;
            float lStart = Time.time;
            for (int i = 0; i < 10; i++)
            {
                FaceSpot(lGuard, 1.4f, out Vector3 lFrom, out Vector3 lAt);
                Log($"  spotted t={Time.time - lStart:0.0}s state {lGuard.State.Value}");
                yield return Shot($"spotted_{i:00}_{lGuard.State.Value}", lFrom, lAt, 30f, 800, 800);
                yield return new WaitForSeconds(0.3f);
            }
            _player.Weapons.Selected.Value = 1;
        }

        /// <summary>A shot in the air near the biggest crowd: people run (wide shots).</summary>
        private IEnumerator Flee()
        {
            Npc lAny = null;
            foreach (Npc lNpc in Npc.All) if (lNpc != null && lNpc.IsCrowd && lNpc.Health.IsAlive && Outdoors(lNpc.transform.position)) { lAny = lNpc; break; }
            if (lAny == null) { Log("no crowd"); yield break; }
            Vector3 lCentre = lAny.transform.position;
            if (!NavMesh.SamplePosition(lCentre + lAny.transform.forward * 10f, out NavMeshHit lHit, 3f, NavMesh.AllAreas)) { Log("no spot near the crowd"); yield break; }
            _player.Motor.Teleport(lHit.position + Vector3.up * 0.1f, 0f);
            yield return new WaitForSeconds(0.3f);
            NoiseSystem.Emit(_player.Eye, 40f, NoiseKind.Alarming, _player.OwnerClientId);
            yield return Measure("panic", 4f);
            // a camera spot with a clear view of the crowd (first free direction round it)
            Vector3 lAway = (lHit.position - lCentre).normalized, lCam = lCentre + lAway * 9f + Vector3.up * 4f;
            for (int a = 0; a < 12; a++)
            {
                Vector3 lTry = lCentre + Quaternion.Euler(0f, a * 30f, 0f) * lAway * 9f + Vector3.up * 4f;
                if (Physics.Linecast(lTry, lCentre + Vector3.up, SCENERY, QueryTriggerInteraction.Ignore) || Physics.CheckSphere(lTry, 0.4f, SCENERY, QueryTriggerInteraction.Ignore)) continue;
                lCam = lTry;
                break;
            }
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.6f);
                yield return Shot($"flee_{i:00}", lCam, lCentre + Vector3.up, 55f, 1600, 900);
            }
            Log("flee: shot fired near " + lCentre.ToString("F0"));
        }

        /// <summary>
        /// In-game check of the moving bodies near the player (foot IK, foot lock and hand push-out
        /// active). Foot slide: the horizontal world speed of a foot while it is down (within 3 cm of
        /// its lowest point over the window), by gait; hands: how close the hand bones come to the
        /// torso axis.
        /// </summary>
        private IEnumerator Measure(string pTag, float pSeconds)
        {
            List<HumanBody> lBodies = new List<HumanBody>();
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || !lNpc.Health.IsAlive || (lNpc.transform.position - _player.transform.position).sqrMagnitude > 30f * 30f) continue;
                HumanBody lBody = lNpc.GetComponentInChildren<HumanBody>();
                if (lBody != null && lBody.Animator != null && lBody.Animator.isHuman) lBodies.Add(lBody);
            }
            // off-screen bodies keep their last pose (CullUpdateTransforms): animate them all while measuring
            foreach (HumanBody lBody in lBodies) lBody.Culling = AnimatorCullingMode.AlwaysAnimate;
            Dictionary<Transform, Vector3> lLast = new Dictionary<Transform, Vector3>();
            Dictionary<Transform, List<Vector4>> lTracks = new Dictionary<Transform, List<Vector4>>();
            List<float> lHands = new List<float>();
            int lPanic = Animator.StringToHash(HumanAnimator.S_PANIC), lCrouch = Animator.StringToHash(HumanAnimator.S_CROUCH), lLoco = Animator.StringToHash(HumanAnimator.S_LOCOMOTION), lPatrol = Animator.StringToHash(HumanAnimator.S_PATROL);
            float lEnd = Time.time + pSeconds;
            while (Time.time < lEnd)
            {
                yield return new WaitForEndOfFrame();
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                foreach (HumanBody lBody in lBodies)
                {
                    if (lBody == null || lBody.Animator == null) continue;
                    HumanAnimator lAnim = lBody.GetComponent<HumanAnimator>();
                    float lScale = lBody.Model != null ? lBody.Model.lossyScale.y : 1f;
                    Vector3 lMove = lBody.transform.position - (lLast.TryGetValue(lBody.transform, out Vector3 lPrevBody) ? lPrevBody : lBody.transform.position);
                    float lBodySpeed = new Vector2(lMove.x, lMove.z).magnitude / dt;
                    lLast[lBody.transform] = lBody.transform.position;
                    // gait: 0 walk, 1 run, 2 panic, 3 crouch (moving only)
                    int lState = lAnim != null ? lAnim.BaseState : 0;
                    float lGait = lState == lPanic ? 2f : lState == lCrouch ? 3f : lState == lLoco ? (lBodySpeed > 2.2f ? 1f : 0f) : lState == lPatrol ? 0f : -1f;
                    for (int f = 0; f < 2; f++)
                    {
                        Transform lFoot = lBody.Animator.GetBoneTransform(f == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                        if (lFoot == null) continue;
                        bool lHad = lLast.TryGetValue(lFoot, out Vector3 lPrev);
                        lLast[lFoot] = lFoot.position;
                        if (!lHad || lGait < 0f || lBodySpeed < 0.4f) continue;
                        Vector3 lD = lFoot.position - lPrev;
                        lD.y = 0f;
                        if (!lTracks.TryGetValue(lFoot, out List<Vector4> lTrack)) lTracks[lFoot] = lTrack = new List<Vector4>();
                        bool lLocked = lAnim != null && lAnim.FootLocked(f);
                        lTrack.Add(new Vector4((lFoot.position.y - lBody.transform.position.y) / lScale, lD.magnitude / dt, lGait, lLocked ? 1f : 0f));
                    }
                    // hands: bare-handed bodies on the move (a gun's support hand sits at the belly by design)
                    if (lBody.Hips == null || lBody.Chest == null || lGait < 0f || lBodySpeed < 0.4f || (lAnim != null && (lAnim.Held == HeldKind.Pistol || lAnim.Held == HeldKind.Rifle))) continue;
                    foreach (Transform lHand in new[] { lBody.HandL, lBody.HandR })
                    {
                        if (lHand == null) continue;
                        Vector3 lAxis = lBody.Chest.position - lBody.Hips.position;
                        float t = Mathf.Clamp01(Vector3.Dot(lHand.position - lBody.Hips.position, lAxis) / Mathf.Max(lAxis.sqrMagnitude, 1e-4f));
                        Vector3 lAway = lHand.position - (lBody.Hips.position + lAxis * t);
                        lAway.y = 0f;
                        lHands.Add(lAway.magnitude / lScale);
                    }
                }
            }
            foreach (HumanBody lBody in lBodies) if (lBody != null) lBody.Culling = AnimatorCullingMode.CullUpdateTransforms;
            string[] lNames = { "walk", "run", "panic", "crouch" };
            List<float>[] lByGait = { new List<float>(), new List<float>(), new List<float>(), new List<float>() };
            List<float> lAll = new List<float>();
            int lStance = 0, lLockedCount = 0;
            foreach (List<Vector4> lTrack in lTracks.Values)
            {
                if (lTrack.Count < 20) continue;
                float lLow = float.MaxValue;
                foreach (Vector4 v in lTrack) lLow = Mathf.Min(lLow, v.x);
                foreach (Vector4 v in lTrack)
                {
                    if (v.x > lLow + 0.03f) continue;
                    lByGait[(int)v.z].Add(v.y);
                    lAll.Add(v.y);
                    lStance++;
                    if (v.w > 0.5f) lLockedCount++;
                }
            }
            StringBuilder lLine = new StringBuilder($"{pTag} measure over {pSeconds:0}s, {lBodies.Count} bodies near the player: stance-foot world speed median {Median(lAll):0.000} m/s ({lAll.Count} samples, locked {(lStance > 0 ? 100f * lLockedCount / lStance : 0f):0}%)");
            for (int g = 0; g < 4; g++) if (lByGait[g].Count > 0) lLine.Append($"; {lNames[g]} {Median(lByGait[g]):0.000} ({lByGait[g].Count})");
            lHands.Sort();
            float lP10 = lHands.Count > 0 ? lHands[lHands.Count / 10] : -1f, lMin = lHands.Count > 0 ? lHands[0] : -1f;
            int lInside = 0;
            foreach (float h in lHands) if (h < 0.13f) lInside++;
            lLine.Append($"; moving bare hands: hand bone to torso axis min {lMin:0.000} m, 10th percentile {lP10:0.000} m, under 0.13 m {(lHands.Count > 0 ? 100f * lInside / lHands.Count : 0f):0.0}% of {lHands.Count} (push-out radius 0.15 m)");
            Log(lLine.ToString());
        }

        private static float Median(List<float> pValues)
        {
            if (pValues.Count == 0) return -1f;
            pValues.Sort();
            return pValues[pValues.Count / 2];
        }

        /// <summary>A crowd member dies on the spot: the ragdoll falls and the face shows X eyes.</summary>
        private IEnumerator Death()
        {
            Npc lVictim = null;
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.Health.IsAlive && lNpc.Role != NpcRole.Target && Outdoors(lNpc.transform.position) && FaceSpot(lNpc, 3f, out _, out _)) { lVictim = lNpc; break; }
            if (lVictim == null) { Log("no victim"); yield break; }
            Vector3 lPos = lVictim.transform.position, lAt = lVictim.Eye;
            FaceSpot(lVictim, 3f, out Vector3 lFrom, out _);
            lVictim.Health.Kill(DamageInfo.World(9999f, DamageType.Bullet, lAt, (lAt - lFrom).normalized));
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(i == 0 ? 0.4f : 1.5f);
                HumanRagdoll lDoll = null;
                foreach (HumanRagdoll lRagdoll in Object.FindObjectsByType<HumanRagdoll>(FindObjectsSortMode.None))
                    if (lRagdoll.Head != null && (lRagdoll.Head.position - lPos).sqrMagnitude < 9f) lDoll = lRagdoll;
                Vector3 lHead = lDoll != null ? lDoll.Head.position : lAt;
                yield return Shot($"death_{i:00}_body", lHead + (lFrom - lAt).normalized * 2.4f + Vector3.up * 1.6f, lHead, 45f, 1200, 900);
                yield return Shot($"death_{i:00}_face", lHead + Vector3.up * 0.75f + (lFrom - lAt).normalized * 0.25f, lHead, 32f, 800, 800);
            }
            Log("death: " + lVictim.name);
        }
    }
}
