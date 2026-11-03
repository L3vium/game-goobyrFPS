using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The play-mode side of <see cref="AiShowcase"/>: hosts a match on the open map, then two shots of
    /// the AI at work, each a strip of game-view frames (what the player sees, HUD markers included)
    /// and third-person renders, plus a log of the NPC states frame by frame:
    /// <list type="number">
    /// <item>a guard at a restricted or hostile post meets the player 9 m away with a gun out;</item>
    /// <item>the player fires in the air 11 m from the biggest crowd: screams, a stampede to the exits.</item>
    /// </list>
    /// </summary>
    public class AiShowcaseRunner
    {
        public string OutputFolder { get; set; }

        private PlayerAvatar _player;
        private readonly StringBuilder _log = new StringBuilder();
        private readonly List<Renderer> _hiddenArms = new List<Renderer>();
        private const string GAME_ROOT = "Assets/_Game/Prefabs/Net/P_GameRoot.prefab";

        public IEnumerator Run()
        {
            Directory.CreateDirectory(OutputFolder);
            yield return StartMatch();
            if (_player != null)
            {
                yield return new WaitForSeconds(3f);
                yield return GuardEngages();
                yield return CrowdFlees();
            }
            File.WriteAllText(Path.Combine(OutputFolder, "showcase.txt"), _log.ToString());
            Debug.Log("[AI] showcase done -> " + OutputFolder);
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return new WaitForSecondsRealtime(0.5f);
            GameInput.LockCursor(false);
            EditorApplication.isPlaying = false;
        }

        private void Log(string pLine)
        {
            _log.AppendLine(pLine);
            Debug.Log("[AI] showcase: " + pLine);
        }

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
            Log($"match on {SceneManager.GetActiveScene().name}: {Npc.All.Count} NPCs");
        }

        // ---------------------------------------------------------------- 1. a guard engages

        private IEnumerator GuardEngages()
        {
            // The guard nearest the crowd: the town is open ground (the jungle posts hide everyone in leaves).
            Vector3 lTown = CrowdCentre(out _);
            List<Npc> lGuards = new List<Npc>();
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.Health.IsAlive && lNpc.Role == NpcRole.Guard && lNpc.State.Value == NpcState.Idle) lGuards.Add(lNpc);
            lGuards.Sort((a, b) => (a.transform.position - lTown).sqrMagnitude.CompareTo((b.transform.position - lTown).sqrMagnitude));
            Npc lGuard = null;
            Vector3 lStand = Vector3.zero, lCamera = Vector3.zero;
            foreach (Npc lNpc in lGuards)
            {
                Vector3 lWish = lNpc.transform.position + lNpc.transform.forward * 9f;
                if (!NavMesh.SamplePosition(lWish, out NavMeshHit lHit, 1.5f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(lHit.position.y - lNpc.transform.position.y) > 1.5f) continue;
                if (!Senses.Clear(lNpc.Eye, lHit.position + Vector3.up * 1.15f) || !OpenAround(lHit.position)) continue;
                // Outdoors only (a dark interior renders black): nothing overhead of the guard or the player.
                if (!Outdoors(lNpc.transform.position) || !Outdoors(lHit.position)) continue;
                if (!ClearView(lNpc, lHit.position, out lCamera)) continue;
                lGuard = lNpc;
                lStand = lHit.position;
                break;
            }
            if (lGuard == null) { Log("no guard post with room in front of it"); yield break; }
            Log($"guard at {lGuard.transform.position:F0} ({SecurityZone.At(lGuard.transform.position)}), player 9 m in front with a gun out");

            Vector3 lTo = lGuard.transform.position - lStand;
            float lYaw = Quaternion.LookRotation(new Vector3(lTo.x, 0f, lTo.z)).eulerAngles.y;
            _player.Motor.Teleport(lStand + Vector3.up * 0.1f, lYaw);
            _player.SetLook(lYaw, 4f);
            _player.Weapons.Selected.Value = 0;
            float lStart = Time.time, lCombatAt = -1f;
            int lFrame = 0;
            while (Time.time - lStart < 12f && (lCombatAt < 0f || Time.time - lCombatAt < 1.6f))
            {
                if (lCombatAt < 0f && lGuard.State.Value == NpcState.Combat) lCombatAt = Time.time;
                Log($"  t={Time.time - lStart:0.0}s guard {lGuard.State.Value} gauge {lGuard.Gauge:0.00}");
                yield return Frame("guard", lFrame++, lCamera, Vector3.Lerp(_player.Eye, lGuard.Eye, 0.6f));
                yield return new WaitForSeconds(0.35f);
            }
            Log(lCombatAt > 0f ? $"guard in combat after {lCombatAt - lStart:0.0} s" : "guard never fought");
            _player.Weapons.Selected.Value = 1;
        }

        // ---------------------------------------------------------------- 2. the crowd flees

        private IEnumerator CrowdFlees()
        {
            Vector3 lCentre = CrowdCentre(out int lBest);
            if (lBest < 3) { Log("no crowd"); yield break; }
            Vector3 lStand = Vector3.zero;
            bool lFound = false;
            for (int k = 0; k < 16 && !lFound; k++)
            {
                Vector3 lWish = lCentre + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * 11f;
                if (!NavMesh.SamplePosition(lWish, out NavMeshHit lHit, 2f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(lHit.position.y - lCentre.y) > 1.5f || !Senses.Clear(lHit.position + Vector3.up * 1.6f, lCentre + Vector3.up * 1.6f) || !OpenAround(lHit.position)) continue;
                lStand = lHit.position;
                lFound = true;
            }
            if (!lFound) { Log("no clear spot near the crowd"); yield break; }
            List<Npc> lCrowd = new List<Npc>();
            foreach (Npc lNpc in Npc.All) if (lNpc != null && lNpc.IsCrowd && lNpc.Health.IsAlive && (lNpc.transform.position - lCentre).sqrMagnitude < 20f * 20f) lCrowd.Add(lNpc);
            Log($"crowd of {lCrowd.Count} at {lCentre:F0}; the player fires in the air 11 m away");

            Vector3 lTo = lCentre - lStand;
            float lYaw = Quaternion.LookRotation(new Vector3(lTo.x, 0f, lTo.z)).eulerAngles.y;
            _player.Motor.Teleport(lStand + Vector3.up * 0.1f, lYaw);
            _player.SetLook(lYaw, -2f);
            // A loud gun for the shot (the starting one is silenced: an 8 m noise).
            _player.Weapons.GiveServer(GameDatabase.Find(ItemKind.Pistol), -1, -1);
            yield return null;
            for (int i = 0; i < WeaponHolder.WEAPON_SLOTS; i++)
                if (_player.Weapons.ItemIn(i) != null && _player.Weapons.ItemIn(i).kind == ItemKind.Pistol) _player.Weapons.Selected.Value = i;
            yield return new WaitForSeconds(0.4f);
            Vector3 lHigh = lStand - lTo.normalized * 5f + Vector3.up * 7f;
            yield return Frame("crowd", 0, lHigh, lCentre);
            _player.SetLook(lYaw, -35f);
            yield return null;
            Fire();
            float lStart = Time.time;
            for (int lFrame = 1; Time.time - lStart < 7f; lFrame++)
            {
                if (lFrame == 3) _player.SetLook(lYaw, 2f);
                int lFleeing = 0, lReporting = 0;
                foreach (Npc lNpc in lCrowd) { if (lNpc.State.Value == NpcState.Flee) lFleeing++; else if (lNpc.State.Value == NpcState.Report) lReporting++; }
                Log($"  t={Time.time - lStart:0.0}s {lFleeing}/{lCrowd.Count} fleeing, {lReporting} running to a guard");
                yield return Frame("crowd", lFrame, lHigh, lCentre);
                yield return new WaitForSeconds(0.4f);
            }
        }

        /// <summary>The player's own shot, as if the button was pressed (local effects and the host's shot).</summary>
        private void Fire()
        {
            MethodInfo lTryFire = typeof(WeaponHolder).GetMethod("TryFire", BindingFlags.Instance | BindingFlags.NonPublic);
            ItemDefinition lGun = _player.Weapons.HeldItem;
            if (lTryFire != null && lGun != null && lGun.IsGun) lTryFire.Invoke(_player.Weapons, new object[] { lGun });
            else NoiseSystem.Emit(_player.Eye, 40f, NoiseKind.Alarming, _player.OwnerClientId);
        }

        /// <summary>The middle of the densest crowd (and how many stand within 10 m of its densest member).</summary>
        private static Vector3 CrowdCentre(out int pCount)
        {
            Vector3 lCentre = Vector3.zero;
            pCount = 0;
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || !lNpc.IsCrowd || !lNpc.Health.IsAlive) continue;
                int lNear = 0;
                Vector3 lSum = Vector3.zero;
                foreach (Npc lOther in Npc.All)
                    if (lOther != null && lOther.IsCrowd && (lOther.transform.position - lNpc.transform.position).sqrMagnitude < 100f) { lNear++; lSum += lOther.transform.position; }
                if (lNear > pCount) { pCount = lNear; lCentre = lSum / lNear; }
            }
            return lCentre;
        }

        /// <summary>A camera spot that sees both the guard and the player (over a shoulder, or from the side).</summary>
        private static bool ClearView(Npc pGuard, Vector3 pStand, out Vector3 pCamera)
        {
            Vector3 lAcross = pGuard.transform.position - pStand;
            lAcross.y = 0f;
            lAcross.Normalize();
            Vector3 lSide = Vector3.Cross(Vector3.up, lAcross);
            Vector3[] lTries = { -lAcross * 3.4f + lSide * 1.6f, -lAcross * 3.4f - lSide * 1.6f, lSide * 6f - lAcross * 1f, -lSide * 6f - lAcross * 1f };
            // Scenery only: the people's own colliders are what the camera looks at.
            int lMask = ~((1 << Core.GameLayers.NPC) | (1 << Core.GameLayers.PLAYER) | (1 << Core.GameLayers.RAGDOLL) | (1 << Core.GameLayers.LOCAL_VIEW));
            foreach (Vector3 lOffset in lTries)
            {
                pCamera = pStand + lOffset + Vector3.up * 2.4f;
                if (Physics.Linecast(pCamera, pGuard.Eye, lMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(pCamera, pStand + Vector3.up * 1.2f, lMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckSphere(pCamera, 0.5f, lMask, QueryTriggerInteraction.Ignore) || !Outdoors(pCamera)) continue;
                return true;
            }
            pCamera = Vector3.zero;
            return false;
        }

        /// <summary>Open sky above (no roof, no floor of a storey above within 12 m).</summary>
        private static bool Outdoors(Vector3 pAt) => !Physics.Raycast(pAt + Vector3.up * 0.5f, Vector3.up, 12f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);

        private static bool OpenAround(Vector3 pAt) => !Physics.CheckCapsule(pAt + Vector3.up * 0.5f, pAt + Vector3.up * 1.7f, 0.35f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);

        /// <summary>One frame: what the player sees (game view, HUD) and a third-person render.</summary>
        private IEnumerator Frame(string pName, int pIndex, Vector3 pFrom, Vector3 pTo)
        {
            yield return new WaitForEndOfFrame();
            Texture2D lView = ScreenCapture.CaptureScreenshotAsTexture();
            if (lView != null)
            {
                File.WriteAllBytes(Path.Combine(OutputFolder, $"{pName}_view_{pIndex:00}.png"), lView.EncodeToPNG());
                Object.Destroy(lView);
            }
            // The third-person render must not draw the player's first-person arms (they float in mid-air).
            int lLocal = 1 << Core.GameLayers.LOCAL_VIEW;
            bool lHidden = false;
            foreach (Renderer lRenderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if ((lLocal & (1 << lRenderer.gameObject.layer)) != 0 && lRenderer.enabled) { lRenderer.enabled = false; _hiddenArms.Add(lRenderer); lHidden = true; }
            MapShots.Shot(Path.Combine(OutputFolder, $"{pName}_tp_{pIndex:00}.png"), pFrom, pTo, 58f, 1280, 720);
            if (lHidden) { foreach (Renderer lRenderer in _hiddenArms) if (lRenderer != null) lRenderer.enabled = true; _hiddenArms.Clear(); }
        }
    }
}
