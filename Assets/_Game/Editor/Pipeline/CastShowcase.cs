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

        private static void HandlePlayMode(PlayModeStateChange pChange)
        {
            if (pChange == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PENDING, false))
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
            if (_player != null)
            {
                yield return new WaitForSeconds(3f);
                foreach (int lTime in new[] { 1, 2 })
                {
                    LobbyState.Instance.TimeOfDay.Value = lTime;
                    yield return new WaitForSeconds(1.5f);
                    yield return Faces(lTime == 1 ? "noon" : "dusk");
                }
                LobbyState.Instance.TimeOfDay.Value = 1;
                yield return new WaitForSeconds(1f);
                yield return Spotted();
                yield return Flee();
                yield return Death();
            }
            File.WriteAllText(Path.Combine(OutputFolder, "showcase.txt"), _log.ToString());
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
            Vector3 lCam = lCentre + (lHit.position - lCentre).normalized * 9f + Vector3.up * 4f;
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.6f);
                yield return Shot($"flee_{i:00}", lCam, lCentre + Vector3.up, 55f, 1600, 900);
            }
            Log("flee: shot fired near " + lCentre.ToString("F0"));
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
