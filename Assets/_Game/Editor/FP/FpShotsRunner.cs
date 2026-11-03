using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
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
    /// The play-mode side of <see cref="FpShots"/>: hosts a match on the open map, stands the player in
    /// the plaza (NPCs nearby paused so nobody walks into the frame or opens fire), and renders the camera
    /// stack (world + first-person arms, post-processing, no HUD) into 1920x1080 and 2520x1080 PNGs for
    /// each pose and action. The view pitch is reset before every shot.
    /// <para>
    /// An editor script cannot be a component in play mode, so this is a plain routine stepped from
    /// <c>EditorApplication.update</c> (<see cref="Begin"/>): yield a float to wait that many seconds,
    /// null for the next tick, another IEnumerator to run it first.
    /// </para>
    /// </summary>
    public class FpShotsRunner
    {
        public string OutputFolder { get; set; }

        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private float _resumeAt;

        private bool _resume;

        /// <param name="pResume">After a domain reload in play mode: the match is already running.</param>
        public static void Begin(string pFolder, bool pResume = false)
        {
            FpShotsRunner lRunner = new FpShotsRunner { OutputFolder = pFolder, _resume = pResume };
            lRunner._stack.Push(lRunner.Start());
            SessionState.SetBool(FpShots.RUNNING, true);
            EditorApplication.update += lRunner.Tick;
        }

        private void Tick()
        {
            if (!EditorApplication.isPlaying) { Debug.Log("[FP] shots: play mode ended"); EditorApplication.update -= Tick; return; }
            if (Time.realtimeSinceStartup < _resumeAt) return;
            while (_stack.Count > 0)
            {
                IEnumerator lTop = _stack.Peek();
                bool lMore;
                try { lMore = lTop.MoveNext(); }
                catch (System.Exception e) { Debug.LogError("[FP] shots stopped: " + e); _stack.Clear(); lMore = false; }
                if (!lMore) { if (_stack.Count > 0) _stack.Pop(); continue; }
                object lYield = lTop.Current;
                if (lYield is IEnumerator lNested) { _stack.Push(lNested); continue; }
                _resumeAt = lYield is float lSeconds ? Time.realtimeSinceStartup + lSeconds : 0f;
                return;
            }
            EditorApplication.update -= Tick;
        }

        private PlayerAvatar _player;
        private float _yaw;
        private readonly StringBuilder _log = new StringBuilder();
        private const string GAME_ROOT = "Assets/_Game/Prefabs/Net/P_GameRoot.prefab";
        /// <summary>Where to stand (map XZ) and which way to look: the plaza, the fountain and the stage ahead.</summary>
        public static Vector2 SPOT = new Vector2(-42f, -146f);
        public static float YAW = 90f, PITCH = 2f;
        public static readonly Vector2Int[] SIZES = { new Vector2Int(1920, 1080), new Vector2Int(2520, 1080) };

        private IEnumerator Start()
        {
            Directory.CreateDirectory(OutputFolder);
            if (_resume)
            {
                _player = PlayerAvatar.Local;
                if (_player != null) _player.Health.InitHealth(100000f);
                Log("resumed after a script reload");
            }
            else yield return StartMatch();
            if (_player != null)
            {
                yield return Stand();
                yield return Guns();
                yield return Fists();
                yield return Reloads();
                yield return Items();
            }
            WeaponHolder.ForceAim = false;
            Viewmodel.ShowScoped = false;
            SessionState.EraseBool(FpShots.RUNNING);
            File.AppendAllText(Path.Combine(OutputFolder, "fp_shots.txt"), _log.ToString());
            Debug.Log("[FP] shots done -> " + OutputFolder);
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return 0.5f;
            GameInput.LockCursor(false);
            EditorApplication.isPlaying = false;
        }

        private void Log(string pLine)
        {
            _log.AppendLine(pLine);
            Debug.Log("[FP] shots: " + pLine);
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
            while (PlayerAvatar.Local == null)
            {
                if (Time.realtimeSinceStartup > lUntil) { Log("the match did not start"); yield break; }
                yield return null;
            }
            _player = PlayerAvatar.Local;
            _player.Health.InitHealth(100000f);
            Log($"match on {SceneManager.GetActiveScene().name}");
        }

        /// <summary>To the plaza; the NPCs around are paused (their AI and their walking).</summary>
        private IEnumerator Stand()
        {
            Log("to the plaza");
            Vector3 lSpot = new Vector3(SPOT.x, 300f, SPOT.y);
            if (Physics.Raycast(lSpot, Vector3.down, out RaycastHit lGround, 600f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) lSpot = lGround.point;
            if (NavMesh.SamplePosition(lSpot, out NavMeshHit lNav, 4f, NavMesh.AllAreas)) lSpot = lNav.position;
            _yaw = YAW;
            _player.Motor.Teleport(lSpot + Vector3.up * 0.1f, _yaw);
            int lPaused = 0;
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || (lNpc.transform.position - lSpot).sqrMagnitude > 70f * 70f) continue;
                lNpc.enabled = false;
                NavMeshAgent lAgent = lNpc.GetComponent<NavMeshAgent>();
                if (lAgent != null && lAgent.enabled) lAgent.isStopped = true;
                lPaused++;
            }
            Log($"standing at {lSpot:F1} looking {_yaw} deg; {lPaused} NPCs paused");
            yield return 2.5f;
            _player.Motor.Teleport(lSpot + Vector3.up * 0.1f, _yaw);
            yield return 0.5f;
        }

        private ItemDefinition Find(ItemKind pKind)
        {
            foreach (ItemDefinition lItem in GameDatabase.Items) if (lItem != null && lItem.kind == pKind) return lItem;
            return null;
        }

        /// <summary>Puts an item in hand (or empties the hands for the fists) and waits for the raise.</summary>
        private IEnumerator Hold(ItemKind pKind)
        {
            WeaponHolder lWeapons = _player.Weapons;
            for (int i = 0; i < WeaponHolder.SLOT_COUNT; i++) lWeapons.Slots[i] = InventorySlot.Empty;
            if (pKind != ItemKind.Fists)
            {
                ItemDefinition lItem = Find(pKind);
                if (lItem == null) { Log("no item " + pKind); yield break; }
                lWeapons.GiveServer(lItem, -1, -1);
                for (int i = 0; i < WeaponHolder.SLOT_COUNT; i++)
                    if (lWeapons.SlotAt(i).item == lItem.Index) { lWeapons.Selected.Value = i; break; }
            }
            else lWeapons.Selected.Value = 0;
            ResetLook();
            yield return 0.9f;
        }

        private void ResetLook() => _player.SetLook(_yaw, PITCH);

        private IEnumerator Shot(string pName)
        {
            ResetLook();
            yield return null;
            foreach (Vector2Int lSize in SIZES)
            {
                string lPath = Path.Combine(OutputFolder, $"{pName}_{(lSize.x * 9 == lSize.y * 16 ? "16x9" : "21x9")}.png");
                Render(lPath, lSize.x, lSize.y);
            }
            // The game view too for a few shots (the real frame, to compare with the stack renders).
            if (pName == "pistol_hip" || pName == "fists_idle" || pName == "pistol_ads")
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, pName + "_game.png"));
                yield return null;
                yield return null;
            }
            Log("shot " + pName);
        }

        /// <summary>The camera stack (world camera and the arms' overlay camera) into a PNG.</summary>
        private static void Render(string pPath, int pWidth, int pHeight)
        {
            Camera lCamera = CameraRig.Instance.WorldCamera;
            RenderTexture lTarget = new RenderTexture(pWidth, pHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture lOld = lCamera.targetTexture;
            lCamera.targetTexture = lTarget;
            for (int i = 0; i < 4; i++) lCamera.Render();
            RenderTexture.active = lTarget;
            Texture2D lImage = new Texture2D(pWidth, pHeight, TextureFormat.RGB24, false);
            lImage.ReadPixels(new Rect(0, 0, pWidth, pHeight), 0, 0);
            lImage.Apply();
            RenderTexture.active = null;
            lCamera.targetTexture = lOld;
            File.WriteAllBytes(pPath, lImage.EncodeToPNG());
            Object.DestroyImmediate(lImage);
            lTarget.Release();
            Object.DestroyImmediate(lTarget);
        }

        // ---------------------------------------------------------------- sets

        private IEnumerator Guns()
        {
            foreach ((ItemKind lKind, string lName) in new[] { (ItemKind.Pistol, "pistol"), (ItemKind.Smg, "smg"), (ItemKind.Shotgun, "shotgun"), (ItemKind.Sniper, "sniper"), (ItemKind.RocketLauncher, "launcher"), (ItemKind.Flamethrower, "flamethrower") })
            {
                yield return Hold(lKind);
                yield return Shot(lName + "_hip");
                WeaponHolder.ForceAim = true;
                Viewmodel.ShowScoped = true;
                yield return 0.7f;
                yield return Shot(lName + "_ads");
                WeaponHolder.ForceAim = false;
                Viewmodel.ShowScoped = false;
                yield return 0.5f;
            }
        }

        private IEnumerator Fists()
        {
            yield return Hold(ItemKind.Fists);
            yield return Shot("fists_idle");
            Viewmodel lView = _player.Weapons.View;
            if (lView == null) yield break;
            yield return Action(lView.PlayMelee(Viewmodel.MeleeMove.Jab), "fists_jab");
            yield return 0.6f;
            yield return Action(lView.PlayMelee(Viewmodel.MeleeMove.Cross), "fists_cross");
            yield return 0.6f;
            lView.PlayMelee(Viewmodel.MeleeMove.Takedown);
            yield return Action(0.52f, "fists_takedown");
            yield return 1f;
        }

        /// <summary>Waits until a moment of the action (its contact, its release) and shoots it.</summary>
        private IEnumerator Action(float pAt, string pName)
        {
            float lStart = Time.time;
            while (Time.time - lStart < pAt - 0.02f) yield return null;
            yield return Shot(pName);
        }

        private IEnumerator Reloads()
        {
            foreach ((ItemKind lKind, string lName, float lAt) in new[] { (ItemKind.Pistol, "pistol_reload", 0.92f), (ItemKind.Smg, "smg_reload", 0.55f) })
            {
                yield return Hold(lKind);
                Viewmodel lView = _player.Weapons.View;
                ViewmodelArms lArms = ViewmodelArms.Instance;
                string lClip = (lKind == ItemKind.Pistol ? "Pistol" : "Smg") + "_Reload";
                float lLength = lArms != null ? lArms.Length(lClip, 1.5f) : 1.5f;
                lView?.PlayReload(lLength);
                yield return Action(lAt, lName);
                yield return lLength;
            }
        }

        private IEnumerator Items()
        {
            yield return Hold(ItemKind.Brick);
            yield return Shot("throwable_held");
            Viewmodel lView = _player.Weapons.View;
            if (lView != null) yield return Action(lView.PlayThrow() - 0.03f, "throwable_throw");
            yield return 1f;
            foreach ((ItemKind lKind, string lName) in new[] { (ItemKind.Keycard, "keycard_held"), (ItemKind.Wrench, "wrench_held"), (ItemKind.Coin, "coin_held") })
            {
                yield return Hold(lKind);
                yield return Shot(lName);
            }
        }
    }
}
