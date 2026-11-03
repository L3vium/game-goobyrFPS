using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The play-mode side of <see cref="AiScenarios"/>: an arena (public square to the west, a
    /// restricted yard to the east, hostile ground between and around, a wall, two exits), a host
    /// session, a player avatar owned by the host (moved and armed by the script), and the cases -
    /// each seeded, run on fresh NPCs:
    /// <list type="number">
    /// <item>an armed trespasser in a restricted yard is fought within 3 s;</item>
    /// <item>a searcher who sees a trespasser never downgrades to a calm look (it fights);</item>
    /// <item>a pistol shot behind a wall draws at least two guards;</item>
    /// <item>a witness of a body reaches a guard (who goes to look) within 20 s;</item>
    /// <item>an unarmed, calm player on the public square never raises anyone past suspicious;</item>
    /// <item>civilians flee from gunfire toward the exit away from it.</item>
    /// </list>
    /// </summary>
    public class AiScenarioRunner
    {
        public string ResultsPath { get; set; }

        private class CaseResult
        {
            public string name, unit, notes = "";
            public int seed;
            public bool passed;
            public float value = -1f, threshold;
        }

        private readonly List<CaseResult> _results = new List<CaseResult>();
        private PlayerAvatar _player;
        private LevelZone _zone;
        private Transform _arena;
        private Vector3 _westExit, _eastExit;
        private string _fatal = "";

        private const string GAME_ROOT = "Assets/_Game/Prefabs/Net/P_GameRoot.prefab";

        public IEnumerator Run()
        {
            float lStarted = Time.realtimeSinceStartup;
            BuildArena();
            yield return null;
            yield return StartHost();
            if (string.IsNullOrEmpty(_fatal)) yield return SpawnPlayer();
            if (string.IsNullOrEmpty(_fatal))
            {
                yield return Case("armed trespasser fought within 3 s", 101, "s", 3f, ArmedTrespasser);
                yield return Case("searcher never downgrades", 202, "s", 10f, SearcherNeverDowngrades);
                yield return Case("shot behind a wall draws 2+ responders", 303, "guards", 2f, ShotBehindWall);
                yield return Case("witness reaches a guard within 20 s", 404, "s", 20f, WitnessReports);
                yield return Case("unarmed player in public never past suspicious", 505, "state", 0f, UnarmedInPublic);
                yield return Case("civilians flee toward the exit away from gunfire", 606, "share", 0.6f, CiviliansFlee);
            }
            Write(Time.realtimeSinceStartup - lStarted);
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return new WaitForSecondsRealtime(0.5f);
            GameInput.LockCursor(false);
            EditorApplication.isPlaying = false;
        }

        // ---------------------------------------------------------------- set-up

        private void BuildArena()
        {
            _arena = new GameObject("Arena").transform;
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(220f, 1f, 220f));
            // The wall of case 3 (structural: 3 m tall, 16 m long).
            Box("Wall", new Vector3(30f, 1.5f, -45f), new Vector3(0.3f, 3f, 16f));

            SecurityZone.Outside = SecurityTier.Hostile;
            Zone("Public", new Vector3(-40f, 0f, 0f), SecurityTier.Public);
            Zone("Restricted", new Vector3(40f, 0f, 0f), SecurityTier.Restricted);

            GameObject lLevel = new GameObject("LevelZone");
            lLevel.transform.SetParent(_arena, false);
            Transform lSafe = new GameObject("SafeRoom").transform;
            lSafe.SetParent(lLevel.transform, false);
            lSafe.position = new Vector3(0f, 0f, 80f);
            Transform lEntry = new GameObject("Entry").transform;
            lEntry.SetParent(lLevel.transform, false);
            lEntry.position = new Vector3(0f, 0f, -90f);
            _zone = lLevel.AddComponent<LevelZone>();
            _zone.Setup(0, "Arena", new Vector3(220f, 60f, 220f), lEntry, Color.gray, HatStyle.Cap, lSafe);

            _westExit = new Vector3(-68f, 0f, 0f);
            _eastExit = new Vector3(-12f, 0f, 0f);
            foreach (Vector3 lAt in new[] { _westExit, _eastExit })
            {
                GameObject lExit = new GameObject("Exit");
                lExit.transform.SetParent(_arena, false);
                lExit.transform.position = lAt;
                lExit.AddComponent<ExitPoint>().Setup(-1);
            }

            Physics.SyncTransforms();
            NavMeshSurface lSurface = _arena.gameObject.AddComponent<NavMeshSurface>();
            lSurface.collectObjects = CollectObjects.Children;
            lSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            lSurface.layerMask = 1 << Core.GameLayers.DEFAULT;
            lSurface.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
            lSurface.BuildNavMesh();

            // Light, so screenshots of the arena are readable.
            Light lSun = new GameObject("Sun").AddComponent<Light>();
            lSun.type = LightType.Directional;
            lSun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        }

        private void Box(string pName, Vector3 pCentre, Vector3 pSize)
        {
            GameObject lBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lBox.name = pName;
            lBox.transform.SetParent(_arena, false);
            lBox.transform.position = pCentre;
            lBox.transform.localScale = pSize;
            lBox.layer = Core.GameLayers.DEFAULT;
        }

        private void Zone(string pName, Vector3 pCentre, SecurityTier pTier)
        {
            GameObject lZone = new GameObject("Zone_" + pName);
            lZone.transform.SetParent(_arena, false);
            lZone.transform.position = pCentre;
            lZone.AddComponent<SecurityZone>().Setup(pTier, new Vector3(60f, 10f, 60f));
        }

        private IEnumerator StartHost()
        {
            GameObject lPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GAME_ROOT);
            if (lPrefab == null) { _fatal = "no GameRoot prefab at " + GAME_ROOT; yield break; }
            GameObject lRoot = Object.Instantiate(lPrefab);
            NetworkManager lNet = lRoot.GetComponentInChildren<NetworkManager>();
            // The arena is not a build scene: no scene management for it.
            if (lNet != null) lNet.NetworkConfig.EnableSceneManagement = false;
            yield return null;
            yield return null;
            if (NetSession.Instance == null) { _fatal = "no NetSession"; yield break; }
            NetSession.Instance.HostLocal();
            float lUntil = Time.realtimeSinceStartup + 20f;
            while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !NetworkManager.Singleton.IsListening || LobbyState.Instance == null)
            {
                if (Time.realtimeSinceStartup > lUntil) { _fatal = "the host did not start"; yield break; }
                yield return null;
            }
        }

        private IEnumerator SpawnPlayer()
        {
            NetworkObject lAvatar = Object.Instantiate(NetPrefabs.Instance.playerAvatar, new Vector3(0f, 0.2f, -95f), Quaternion.identity);
            lAvatar.SpawnWithOwnership(NetworkManager.ServerClientId, true);
            _player = lAvatar.GetComponent<PlayerAvatar>();
            float lUntil = Time.realtimeSinceStartup + 10f;
            while (_player.Weapons == null || _player.Weapons.Slots.Count < WeaponHolder.SLOT_COUNT)
            {
                if (Time.realtimeSinceStartup > lUntil) { _fatal = "the player avatar did not come up"; yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
        }

        // ---------------------------------------------------------------- helpers

        private IEnumerator Case(string pName, int pSeed, string pUnit, float pThreshold, System.Func<CaseResult, IEnumerator> pBody)
        {
            CaseResult lResult = new CaseResult { name = pName, seed = pSeed, unit = pUnit, threshold = pThreshold };
            _results.Add(lResult);
            yield return Reset(pSeed);
            yield return pBody(lResult);
            Debug.Log($"[AI] scenario '{pName}': {(lResult.passed ? "PASS" : "FAIL")} ({lResult.value:0.##} {pUnit}) {lResult.notes}");
        }

        /// <summary>Fresh NPCs, a forgetful zone, the player far away, unarmed and unkillable, the seed set.</summary>
        private IEnumerator Reset(int pSeed)
        {
            foreach (Npc lNpc in new List<Npc>(Npc.All))
                if (lNpc != null && lNpc.IsSpawned) lNpc.NetworkObject.Despawn(true);
            foreach (ItemPickup lPickup in Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
                if (lPickup != null && lPickup.IsSpawned) lPickup.NetworkObject.Despawn(true);
            yield return null;
            _zone.ResetSquad();
            Random.InitState(pSeed);
            _player.Health.InitHealth(100000f);
            Place(new Vector3(0f, 0.1f, -95f), 0f, false);
            yield return new WaitForSeconds(0.5f);
        }

        private void Place(Vector3 pAt, float pYaw, bool pArmed)
        {
            _player.Motor.Teleport(pAt + Vector3.up * 0.1f, pYaw);
            _player.SetLook(pYaw, 0f);
            // Slot 0 holds the starting gun; slot 1 is empty: the fists.
            int lSlot = pArmed ? 0 : 1;
            if (_player.Weapons.Selected.Value != lSlot) _player.Weapons.Selected.Value = lSlot;
        }

        private Npc Spawn(NpcRole pRole, Vector3 pAt, float pYaw, ItemKind pWeapon)
        {
            if (NavMesh.SamplePosition(pAt, out NavMeshHit lHit, 4f, NavMesh.AllAreas)) pAt = lHit.position;
            NetworkObject lObject = Object.Instantiate(NetPrefabs.Instance.npc, pAt, Quaternion.Euler(0f, pYaw, 0f));
            lObject.Spawn(true);
            Npc lNpc = lObject.GetComponent<Npc>();
            lNpc.InitServer(pRole, 0, pWeapon == ItemKind.Fists ? null : GameDatabase.Find(pWeapon), null, null, -1, null);
            return lNpc;
        }

        /// <summary>The player's shot as the host hears it (what WeaponHolder.FireRpc emits).</summary>
        private void PlayerShot(ItemKind pGun)
        {
            ItemDefinition lGun = GameDatabase.Find(pGun);
            NoiseSystem.Emit(_player.Eye, lGun != null ? lGun.noiseRadius : 45f, NoiseKind.Alarming, _player.OwnerClientId);
        }

        private static bool Above(NpcState pState, params NpcState[] pStates)
        {
            foreach (NpcState lState in pStates) if (pState == lState) return true;
            return false;
        }

        // ---------------------------------------------------------------- cases

        /// <summary>1. A guard 10 m away in a restricted yard sees a player with a gun out: a fight within 3 s.</summary>
        private IEnumerator ArmedTrespasser(CaseResult r)
        {
            Npc lGuard = Spawn(NpcRole.Guard, new Vector3(50f, 0f, 0f), 270f, ItemKind.Smg);
            yield return new WaitForSeconds(0.5f);
            Place(new Vector3(40f, 0f, 0f), 90f, true);
            float lStart = Time.time;
            while (Time.time - lStart < 8f && lGuard.State.Value != NpcState.Combat) yield return null;
            r.value = lGuard.State.Value == NpcState.Combat ? Time.time - lStart : -1f;
            r.passed = r.value >= 0f && r.value <= r.threshold;
            r.notes = "final " + lGuard.State.Value;
        }

        /// <summary>2. A guard sent to search sees a trespasser: it never cools down into a stare or a calm look - it fights.</summary>
        private IEnumerator SearcherNeverDowngrades(CaseResult r)
        {
            Npc lGuard = Spawn(NpcRole.Guard, new Vector3(56f, 0f, 14f), 0f, ItemKind.Smg);
            yield return new WaitForSeconds(0.5f);
            Place(new Vector3(42f, 0f, 6f), 45f, false);
            lGuard.AlertToPoint(new Vector3(46f, 0f, 9f), true);
            float lStart = Time.time;
            string lPath = lGuard.State.Value.ToString();
            bool lDowngraded = false;
            while (Time.time - lStart < 12f && lGuard.State.Value != NpcState.Combat)
            {
                NpcState lState = lGuard.State.Value;
                if (!lPath.EndsWith(lState.ToString())) lPath += ">" + lState;
                if (Above(lState, NpcState.Idle, NpcState.Patrol, NpcState.Routine, NpcState.Investigate, NpcState.Suspicious)) lDowngraded = true;
                yield return null;
            }
            if (lGuard.State.Value == NpcState.Combat && !lPath.EndsWith("Combat")) lPath += ">Combat";
            r.value = lGuard.State.Value == NpcState.Combat ? Time.time - lStart : -1f;
            r.passed = !lDowngraded && r.value >= 0f && r.value <= r.threshold;
            r.notes = lPath;
        }

        /// <summary>3. A pistol fired 3 m behind a wall: at least two of the four guards on the other side come.</summary>
        private IEnumerator ShotBehindWall(CaseResult r)
        {
            List<Npc> lGuards = new List<Npc>
            {
                Spawn(NpcRole.Guard, new Vector3(37f, 0f, -45f), 90f, ItemKind.Smg),
                Spawn(NpcRole.Guard, new Vector3(39f, 0f, -41f), 90f, ItemKind.Smg),
                Spawn(NpcRole.Guard, new Vector3(41f, 0f, -49f), 90f, ItemKind.Smg),
                Spawn(NpcRole.Guard, new Vector3(35f, 0f, -50f), 90f, ItemKind.Pistol)
            };
            yield return new WaitForSeconds(0.6f);
            Place(new Vector3(27f, 0f, -45f), 270f, true);
            yield return new WaitForSeconds(0.3f);
            int lWalls = Senses.WallsBetween(_player.Eye + Vector3.up * 0.4f, lGuards[0].Eye);
            PlayerShot(ItemKind.Pistol);
            yield return new WaitForSeconds(3f);
            int lCame = 0;
            StringBuilder lStates = new StringBuilder();
            foreach (Npc lGuard in lGuards)
            {
                lStates.Append(lGuard.State.Value).Append(' ');
                if (Above(lGuard.State.Value, NpcState.Search, NpcState.Investigate, NpcState.Combat, NpcState.Engaged)) lCame++;
            }
            r.value = lCame;
            r.passed = lCame >= r.threshold;
            r.notes = "walls between the shot and the nearest guard: " + lWalls + "; " + lStates.ToString().Trim();
        }

        /// <summary>4. A guest sees a body: it runs to the guard 20 m off and tells it; the guard goes to look - within 20 s.</summary>
        private IEnumerator WitnessReports(CaseResult r)
        {
            Npc lWitness = Spawn(NpcRole.Worker, new Vector3(-40f, 0f, 12f), 180f, ItemKind.Fists);
            Npc lVictim = Spawn(NpcRole.Worker, new Vector3(-40f, 0f, 6f), 0f, ItemKind.Fists);
            Npc lGuard = Spawn(NpcRole.Guard, new Vector3(-55f, 0f, 26f), 270f, ItemKind.Smg);
            yield return new WaitForSeconds(0.8f);
            lVictim.Health.Kill(Core.DamageInfo.World(1000f, Core.DamageType.Bullet, lVictim.transform.position + Vector3.up, Vector3.forward));
            float lStart = Time.time;
            bool lReported = false;
            while (Time.time - lStart < 30f)
            {
                if (lWitness.State.Value == NpcState.Report) lReported = true;
                if (Above(lGuard.State.Value, NpcState.Search, NpcState.Investigate, NpcState.Combat)) break;
                yield return null;
            }
            bool lCame = Above(lGuard.State.Value, NpcState.Search, NpcState.Investigate, NpcState.Combat);
            r.value = lCame ? Time.time - lStart : -1f;
            r.passed = lReported && lCame && r.value <= r.threshold;
            r.notes = "witness " + (lReported ? "reported" : "never reported") + ", guard " + lGuard.State.Value;
        }

        /// <summary>5. An unarmed, calm player among guards and guests on the public square: nobody goes past suspicious.</summary>
        private IEnumerator UnarmedInPublic(CaseResult r)
        {
            Vector3 lAt = new Vector3(-40f, 0f, -12f);
            List<Npc> lNpcs = new List<Npc>();
            foreach (Vector3 lOffset in new[] { new Vector3(6f, 0, 0), new Vector3(0, 0, 8f), new Vector3(-8f, 0, -4f) })
                lNpcs.Add(Spawn(NpcRole.Guard, lAt + lOffset, Quaternion.LookRotation(-lOffset).eulerAngles.y, ItemKind.Smg));
            foreach (Vector3 lOffset in new[] { new Vector3(4f, 0, 4f), new Vector3(-4f, 0, 4f), new Vector3(2f, 0, -5f), new Vector3(-3f, 0, -3f) })
                lNpcs.Add(Spawn(NpcRole.Worker, lAt + lOffset, Quaternion.LookRotation(-lOffset).eulerAngles.y, ItemKind.Fists));
            yield return new WaitForSeconds(0.5f);
            Place(lAt, 0f, false);
            float lStart = Time.time;
            NpcState lWorst = NpcState.Idle;
            string lWho = "";
            while (Time.time - lStart < 12f)
            {
                // A slow walk back and forth through them after a while (1.2 m/s).
                if (Time.time - lStart > 5f) _player.Motor.Teleport(lAt + new Vector3(Mathf.PingPong((Time.time - lStart - 5f) * 1.2f, 6f) - 3f, 0.1f, 0f), 90f);
                foreach (Npc lNpc in lNpcs)
                {
                    NpcState lState = lNpc.State.Value;
                    if (Above(lState, NpcState.Engaged, NpcState.Combat, NpcState.Calling, NpcState.Search, NpcState.Flee, NpcState.Report)) { lWorst = lState; lWho = lNpc.Role.ToString(); }
                }
                yield return null;
            }
            r.value = (int)lWorst;
            r.passed = !Above(lWorst, NpcState.Engaged, NpcState.Combat, NpcState.Calling, NpcState.Search, NpcState.Flee, NpcState.Report);
            r.notes = r.passed ? "worst: below engaged" : "worst: " + lWorst + " (" + lWho + ")";
        }

        /// <summary>6. Gunfire 12 m east of a crowd: most of it flees, and toward the west exit (the east one is past the shooter).</summary>
        private IEnumerator CiviliansFlee(CaseResult r)
        {
            Vector3 lCentre = new Vector3(-40f, 0f, 0f);
            List<Npc> lCrowd = new List<Npc>();
            for (int i = 0; i < 8; i++)
            {
                float lAngle = i * Mathf.PI * 2f / 8f;
                Vector3 lAt = lCentre + new Vector3(Mathf.Cos(lAngle), 0f, Mathf.Sin(lAngle)) * 2.5f;
                lCrowd.Add(Spawn(NpcRole.Worker, lAt, Random.Range(0f, 360f), ItemKind.Fists));
            }
            yield return new WaitForSeconds(0.8f);
            Place(lCentre + new Vector3(12f, 0f, 0f), 270f, true);
            List<float> lBefore = new List<float>();
            foreach (Npc lNpc in lCrowd) lBefore.Add(Vector3.Distance(lNpc.transform.position, _westExit));
            yield return new WaitForSeconds(0.2f);
            PlayerShot(ItemKind.Pistol);
            yield return new WaitForSeconds(6f);
            int lFled = 0, lWest = 0;
            for (int i = 0; i < lCrowd.Count; i++)
            {
                if (lCrowd[i].State.Value == NpcState.Flee) lFled++;
                if (lBefore[i] - Vector3.Distance(lCrowd[i].transform.position, _westExit) >= 3f) lWest++;
            }
            r.value = Mathf.Min(lFled, lWest) / (float)lCrowd.Count;
            r.passed = r.value >= r.threshold;
            r.notes = lFled + "/" + lCrowd.Count + " fleeing, " + lWest + "/" + lCrowd.Count + " closer to the west exit";
        }

        // ---------------------------------------------------------------- results

        private void Write(float pSeconds)
        {
            int lPassed = 0;
            foreach (CaseResult lResult in _results) if (lResult.passed) lPassed++;
            bool lAll = string.IsNullOrEmpty(_fatal) && lPassed == _results.Count && _results.Count > 0;
            StringBuilder lJson = new StringBuilder();
            lJson.Append("{\n");
            lJson.Append("  \"run\": \"").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append("\",\n");
            lJson.Append("  \"seconds\": ").Append(pSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(",\n");
            lJson.Append("  \"fatal\": \"").Append(Escape(_fatal)).Append("\",\n");
            lJson.Append("  \"passed\": ").Append(lPassed).Append(",\n");
            lJson.Append("  \"total\": ").Append(_results.Count).Append(",\n");
            lJson.Append("  \"allPassed\": ").Append(lAll ? "true" : "false").Append(",\n");
            lJson.Append("  \"cases\": [\n");
            for (int i = 0; i < _results.Count; i++)
            {
                CaseResult c = _results[i];
                lJson.Append("    { \"name\": \"").Append(Escape(c.name)).Append("\", \"seed\": ").Append(c.seed)
                    .Append(", \"passed\": ").Append(c.passed ? "true" : "false")
                    .Append(", \"value\": ").Append(c.value.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(", \"threshold\": ").Append(c.threshold.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(", \"unit\": \"").Append(c.unit).Append("\", \"notes\": \"").Append(Escape(c.notes)).Append("\" }")
                    .Append(i < _results.Count - 1 ? ",\n" : "\n");
            }
            lJson.Append("  ]\n}\n");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath));
            File.WriteAllText(ResultsPath, lJson.ToString());
            Debug.Log($"[AI] scenarios: {lPassed}/{_results.Count} passed{(string.IsNullOrEmpty(_fatal) ? "" : " - " + _fatal)} -> {ResultsPath}");
        }

        private static string Escape(string pText) => (pText ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
