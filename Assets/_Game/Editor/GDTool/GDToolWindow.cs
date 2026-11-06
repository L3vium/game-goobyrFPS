using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The game designer's window (<c>Goobyr → GD Tool</c>): every tuning knob of the game without
    /// opening code - the starting loadout (the hush pistol by default, or anything else), match
    /// rules, health and damage, every weapon, the AI's senses and radio, each map's fifteen targets,
    /// and the map tools (rebuild, validate).
    /// </summary>
    public class GDToolWindow : EditorWindow
    {
        private static readonly string[] TABS = { "Rules", "Weapons", "AI", "Targets", "Maps" };
        private int _tab;
        private int _item;
        private int _map;
        private Vector2 _scroll;
        private string _report = "";

        [MenuItem("Goobyr/GD Tool %#g")]
        public static void Open() => GetWindow<GDToolWindow>("Goobyr GD Tool").minSize = new Vector2(520, 560);

        private GameDatabase Db => GameDatabase.Instance;

        private void OnGUI()
        {
            if (Db == null || Db.rules == null)
            {
                EditorGUILayout.HelpBox("No GameDatabase yet. Build the data first.", MessageType.Warning);
                if (GUILayout.Button("Build data")) DataBuilder.Build(false);
                return;
            }

            _tab = GUILayout.Toolbar(_tab, TABS, GUILayout.Height(28));
            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: Rules(); break;
                case 1: Weapons(); break;
                case 2: Ai(); break;
                case 3: Targets(); break;
                case 4: Maps(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private static void Fields(SerializedObject pObject, params string[] pNames)
        {
            pObject.Update();
            foreach (string lName in pNames)
            {
                SerializedProperty lProperty = pObject.FindProperty(lName);
                if (lProperty != null) EditorGUILayout.PropertyField(lProperty, true);
            }
            pObject.ApplyModifiedProperties();
        }

        private void Rules()
        {
            SerializedObject lRules = new SerializedObject(Db.rules);
            EditorGUILayout.LabelField("Starting loadout", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("What every player spawns with. Drag items from Data/Items, or use the quick buttons.", MessageType.None);
            Fields(lRules, "startingLoadout");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Hush pistol + coins")) SetLoadout(ItemKind.SilencedPistol, ItemKind.Coin);
            if (GUILayout.Button("Fists only")) SetLoadout();
            if (GUILayout.Button("Pistol + smoke")) SetLoadout(ItemKind.Pistol, ItemKind.SmokeGrenade);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Players & match", EditorStyles.boldLabel);
            Fields(lRules, "cheatsEnabled", "playerHealth", "playerDamageMultiplier", "maxPlayers", "timeLimitMinutes", "proximityChatByDefault", "proximityChatRange", "proofRevealInterval", "extractionTime", "activeExtractions");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Systems", EditorStyles.boldLabel);
            Fields(lRules, "ventGasDelay", "ventGasDuration", "fireSpreadInterval");
        }

        private void SetLoadout(params ItemKind[] pKinds)
        {
            Undo.RecordObject(Db.rules, "Loadout");
            Db.rules.startingLoadout.Clear();
            foreach (ItemKind lKind in pKinds)
            {
                ItemDefinition lItem = Db.items.Find(i => i != null && i.kind == lKind);
                if (lItem != null) Db.rules.startingLoadout.Add(lItem);
            }
            EditorUtility.SetDirty(Db.rules);
        }

        private void Weapons()
        {
            string[] lNames = Db.items.ConvertAll(i => i != null ? i.displayName + "  (" + i.kind + ")" : "-").ToArray();
            _item = EditorGUILayout.Popup("Item", Mathf.Clamp(_item, 0, lNames.Length - 1), lNames);
            ItemDefinition lItem = Db.items[_item];
            if (lItem == null) return;
            SerializedObject lObject = new SerializedObject(lItem);
            Fields(lObject, "displayName", "color", "unique", "damage", "headMultiplier", "fireRate", "automatic", "pellets", "spread", "adsSpreadFactor", "range", "magazine", "reserveAmmo", "reloadTime", "recoil", "adsFov", "shake", "noiseRadius", "projectileSpeed", "explosionRadius", "throwForce", "stack");
            EditorGUILayout.HelpBox($"Body shots to drop a {Db.rules.npcHealth:0} HP guard: {Mathf.CeilToInt(Db.rules.npcHealth / Mathf.Max(1f, lItem.damage * Db.rules.playerDamageMultiplier))}   -   headshots: {Mathf.CeilToInt(Db.rules.npcHealth / Mathf.Max(1f, lItem.damage * lItem.headMultiplier * Db.rules.playerDamageMultiplier))}", MessageType.Info);
        }

        private void Ai()
        {
            SerializedObject lRules = new SerializedObject(Db.rules);
            EditorGUILayout.LabelField("Senses", EditorStyles.boldLabel);
            Fields(lRules, "npcVisionRange", "npcVisionAngle", "npcDetectTime", "npcReactionTime");
            EditorGUILayout.LabelField("Combat", EditorStyles.boldLabel);
            Fields(lRules, "npcHealth", "npcDamageMultiplier", "npcAccuracy", "searchDuration");
            EditorGUILayout.LabelField("Radio & reinforcements (per zone)", EditorStyles.boldLabel);
            Fields(lRules, "callDuration", "reinforcementsPerCall", "maxCallsPerZone");
        }

        private void Targets()
        {
            string[] lNames = Db.maps.ConvertAll(m => m != null ? m.displayName : "-").ToArray();
            _map = EditorGUILayout.Popup("Map", Mathf.Clamp(_map, 0, lNames.Length - 1), lNames);
            MapDefinition lMap = Db.maps[_map];
            if (lMap == null) return;
            int lEnabled = lMap.targets.FindAll(t => t.enabled).Count;
            EditorGUILayout.HelpBox($"{lEnabled} / {lMap.targets.Count} targets in the draw. Untick one to take it out of the pool.", MessageType.None);
            SerializedObject lObject = new SerializedObject(lMap);
            Fields(lObject, "targets");
        }

        private void Maps()
        {
            EditorGUILayout.LabelField("Rebuild", EditorStyles.boldLabel);
            if (GUILayout.Button("San Lázaro (jungle)")) new MapSanLazaro().Build();
            if (GUILayout.Button("Qasr al-Rimal (desert)")) new MapQasrAlRimal().Build();
            if (GUILayout.Button("Weisshorn (snow)")) new MapWeisshorn().Build();
            if (GUILayout.Button("Everything (data, art, prefabs, sounds, maps, title)")) BuildPipelineMenu.All();
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate all maps", GUILayout.Height(30))) _report = MapValidator.ValidateAll();
            if (!string.IsNullOrEmpty(_report)) EditorGUILayout.HelpBox(_report, _report.Contains("FAIL") ? MessageType.Error : MessageType.Info);
        }
    }

    /// <summary>Structural checks of a built map: every element a match needs is there.</summary>
    public static class MapValidator
    {
        [MenuItem("Goobyr/Validate Maps")]
        public static void Menu() => Debug.Log(ValidateAll());

        public static string ValidateAll()
        {
            System.Text.StringBuilder lReport = new System.Text.StringBuilder();
            string lCurrent = EditorSceneManager.GetActiveScene().path;
            foreach (MapDefinition lMap in GameDatabase.Instance.maps)
            {
                EditorSceneManager.OpenScene(EditorAssets.SCENES + "/" + lMap.sceneName + ".unity", OpenSceneMode.Single);
                lReport.AppendLine(Validate(lMap));
            }
            if (!string.IsNullOrEmpty(lCurrent)) EditorSceneManager.OpenScene(lCurrent, OpenSceneMode.Single);
            return lReport.ToString().TrimEnd();
        }

        public static string Validate(MapDefinition pMap)
        {
            System.Collections.Generic.List<string> lFails = new System.Collections.Generic.List<string>();
            // Edit mode: the static registries fill in OnEnable at play time, so look in the scene.
            MatchPoint[] lPoints = Object.FindObjectsByType<MatchPoint>(FindObjectsSortMode.None);
            ActivityNode[] lNodes = Object.FindObjectsByType<ActivityNode>(FindObjectsSortMode.None);
            NpcSpawnPoint[] lSpawns = Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None);
            int Count(MatchPoint.Kind pKind) => System.Linq.Enumerable.Count(lPoints, p => p.PointKind == pKind);
            bool Kit(ItemKind k) => k == ItemKind.Invitation || k == ItemKind.StaffPass || k == ItemKind.Keycard;
            bool Throwable(ItemKind k) => k == ItemKind.Rock || k == ItemKind.Bottle || k == ItemKind.Wrench || k == ItemKind.Brick || k == ItemKind.Coin;
            int lStarts = Count(MatchPoint.Kind.Start), lExtractions = Count(MatchPoint.Kind.Extraction), lWeapons = Count(MatchPoint.Kind.WeaponSpawn);
            int lInside = System.Linq.Enumerable.Count(lPoints, p => p.PointKind == MatchPoint.Kind.Start && Kit(p.Item));
            int lThrowables = System.Linq.Enumerable.Count(lPoints, p => p.PointKind == MatchPoint.Kind.GadgetSpawn && Throwable(p.Item));
            int lRockets = System.Linq.Enumerable.Count(lPoints, p => p.PointKind == MatchPoint.Kind.UniqueWeaponSpawn && p.Item == ItemKind.RocketLauncher);
            int lFlames = System.Linq.Enumerable.Count(lPoints, p => p.PointKind == MatchPoint.Kind.UniqueWeaponSpawn && p.Item == ItemKind.Flamethrower);
            int lNpcs = lSpawns.Length;
            int lZones = Object.FindObjectsByType<LevelZone>(FindObjectsSortMode.None).Length;
            int lCovers = Object.FindObjectsByType<CoverPoint>(FindObjectsSortMode.None).Length;
            int lVents = Object.FindObjectsByType<Systems.VentCover>(FindObjectsSortMode.None).Length;
            int lSecure = Object.FindObjectsByType<Systems.SecureDoor>(FindObjectsSortMode.None).Length;
            int lCameras = Object.FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None).Length;
            int lRecorders = Object.FindObjectsByType<Systems.CameraRecorder>(FindObjectsSortMode.None).Length;
            int lClimbs = 0;
            foreach (Collider lCollider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (lCollider.gameObject.layer == GameLayers.CLIMBABLE) lClimbs++;
            UI.MapPoi[] lPois = Object.FindObjectsByType<UI.MapPoi>(FindObjectsSortMode.None);
            if (lStarts - lInside < 6) lFails.Add($"{lStarts - lInside} edge starts (< 6)");
            if (lInside < 3) lFails.Add($"{lInside} inside starts with a pass (< 3)");
            if (lExtractions < 3) lFails.Add($"{lExtractions} extractions (< 3)");
            if (lWeapons < 30) lFails.Add($"{lWeapons} weapon spawns (< 30)");
            if (lThrowables < 150) lFails.Add($"{lThrowables} throwables (< 150)");
            if (lRockets < 3) lFails.Add($"{lRockets} rocket launcher spots (< 3)");
            if (lFlames < 3) lFails.Add($"{lFlames} flamethrower spots (< 3)");
            if (lZones != 3) lFails.Add($"{lZones} zones (!= 3)");
            if (lNpcs < 100) lFails.Add($"{lNpcs} NPCs (< 100)");
            if (lVents < 2) lFails.Add($"{lVents} vent grilles (< 2)");
            if (lClimbs < 6) lFails.Add($"{lClimbs} climbable drainpipes/ladders/vines (< 6)");
            if (lCameras < 8 || lRecorders < 1) lFails.Add($"{lCameras} cameras, {lRecorders} recorders (need 8+ and 1)");
            // Sentry guns: 3+, each driven by a terminal and reached by its cable; none covering a start.
            Systems.AutoTurret[] lTurrets = Object.FindObjectsByType<Systems.AutoTurret>(FindObjectsSortMode.None);
            Systems.TurretTerminal[] lTerminals = Object.FindObjectsByType<Systems.TurretTerminal>(FindObjectsSortMode.None);
            Systems.TurretCable[] lCables = Object.FindObjectsByType<Systems.TurretCable>(FindObjectsSortMode.None);
            if (lTurrets.Length < 3) lFails.Add($"{lTurrets.Length} sentry guns (< 3)");
            foreach (Systems.AutoTurret lTurret in lTurrets)
            {
                bool lTerminal = System.Linq.Enumerable.Any(lTerminals, t => t.Turrets != null && System.Array.IndexOf(t.Turrets, lTurret) >= 0);
                bool lCable = System.Linq.Enumerable.Any(lCables, c => c.Turret == lTurret);
                if (!lTerminal || !lCable) lFails.Add($"sentry gun at {lTurret.transform.position:F0} has {(lTerminal ? "" : "no terminal ")}{(lCable ? "" : "no cable")}");
                foreach (MatchPoint lPoint in lPoints)
                {
                    if (lPoint.PointKind != MatchPoint.Kind.Start && lPoint.PointKind != MatchPoint.Kind.Extraction) continue;
                    Vector3 lTo = lPoint.transform.position + Vector3.up - (lTurret.transform.position + Vector3.up);
                    if (lTo.magnitude > 24f || Vector3.Angle(new Vector3(lTo.x, 0f, lTo.z), lTurret.transform.forward) > 80f) continue;
                    if (!Physics.Linecast(lTurret.transform.position + Vector3.up * 1.3f + lTurret.transform.forward * 0.6f, lPoint.transform.position + Vector3.up, ~0, QueryTriggerInteraction.Ignore))
                        lFails.Add($"sentry gun at {lTurret.transform.position:F0} covers {lPoint.PointKind} {lPoint.name}");
                }
            }
            // The new distractions: pedestal fans, garden hoses, office PCs.
            int lFans = 0, lHoses = 0, lPcs = 0;
            foreach (Systems.Distraction lDistraction in Object.FindObjectsByType<Systems.Distraction>(FindObjectsSortMode.None))
            {
                if (lDistraction.name == "Fan") lFans++;
                else if (lDistraction.name == "HoseReel") lHoses++;
                else if (lDistraction.name == "Computer") lPcs++;
            }
            if (lFans < 4 || lHoses < 3 || lPcs < 4) lFails.Add($"{lFans} fans, {lHoses} hoses, {lPcs} computers (need 4, 3, 4)");
            // Patrols: at least 35 % of the guards walk a route; the key building has a loop on every floor
            // with rooms; every new distraction device is within earshot of a post or a patrol stop.
            int lArmed = 0, lMoving = 0;
            System.Collections.Generic.List<Vector3> lStops = new System.Collections.Generic.List<Vector3>();
            foreach (NpcSpawnPoint lSpawn in lSpawns)
            {
                if (lSpawn.Role == NpcRole.Guard || lSpawn.Role == NpcRole.Lookout) lArmed++;
                if (lSpawn.Role == NpcRole.Patrol) { lArmed++; lMoving++; }
                if (lSpawn.Role != NpcRole.Target) lStops.Add(lSpawn.transform.position);
            }
            PatrolRoute[] lRoutes = Object.FindObjectsByType<PatrolRoute>(FindObjectsSortMode.None);
            foreach (PatrolRoute lRoute in lRoutes) for (int i = 0; i < lRoute.Count; i++) lStops.Add(lRoute.Point(i));
            // The AI track's patrol audit: 35 % moving, a loop in every guarded building, a town patrol,
            // an exit for every crowd, errands.
            PatrolAudit.Report lAudit = PatrolAudit.Audit();
            lFails.AddRange(PatrolAudit.Fails(lAudit));
            lMoving = lAudit.moving;
            lArmed = lAudit.guards;
            // Staff and guests have somewhere to go between stints: 2+ break spots within 30 m in their zone.
            int lCrowdCount = 0, lCrowdServed = 0;
            foreach (NpcSpawnPoint lSpawn in lSpawns)
            {
                if (lSpawn.Role != NpcRole.Worker) continue;
                lCrowdCount++;
                int lNear = 0;
                foreach (ActivityNode lNode in lNodes)
                {
                    bool lCrowdSpot = lNode.Activity is ActivityType.Drink or ActivityType.Dining or ActivityType.Garden or ActivityType.Smoke or ActivityType.Phone or ActivityType.Idle or ActivityType.Inspect;
                    if (lCrowdSpot && lNode.Zone == lSpawn.Zone && Vector3.Distance(lNode.transform.position, lSpawn.transform.position) < 30f) lNear++;
                }
                if (lNear >= 2) lCrowdServed++;
            }
            if (lCrowdCount > 0 && lCrowdServed < lCrowdCount * 0.8f) lFails.Add($"only {lCrowdServed}/{lCrowdCount} staff and guests have 2+ break spots within 30 m (< 80 %)");
            Systems.BuildingVolume lKey = null;
            foreach (Systems.BuildingVolume lVolume in Object.FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None))
                if (lKey == null || lVolume.Size.x * lVolume.Size.z > lKey.Size.x * lKey.Size.z) lKey = lVolume;
            string lLoops = "";
            if (lKey != null)
            {
                Transform lBuilding = lKey.transform.parent;
                System.Collections.Generic.Dictionary<int, int> lRoomsPerFloor = new System.Collections.Generic.Dictionary<int, int>();
                float lStorey = 3.6f;
                foreach (SecurityZone lZone in lBuilding.GetComponentsInChildren<SecurityZone>()) { lStorey = lZone.Size.y; break; }
                foreach (SecurityZone lZone in lBuilding.GetComponentsInChildren<SecurityZone>())
                {
                    int f = Mathf.RoundToInt((lZone.transform.position.y - lBuilding.position.y) / lStorey);
                    lRoomsPerFloor[f] = lRoomsPerFloor.TryGetValue(f, out int n) ? n + 1 : 1;
                }
                System.Collections.Generic.List<int> lMissing = new System.Collections.Generic.List<int>();
                foreach (var lFloor in lRoomsPerFloor)
                {
                    if (lFloor.Value < 4) continue;
                    float y = lBuilding.position.y + lFloor.Key * lStorey;
                    bool lHas = System.Linq.Enumerable.Any(lRoutes, r => r.Count >= 4 && System.Linq.Enumerable.All(System.Linq.Enumerable.Range(0, r.Count), i => Mathf.Abs(r.Point(i).y - y) < 1.9f && lKey.Contains(r.Point(i) + Vector3.up)));
                    if (!lHas) lMissing.Add(lFloor.Key);
                }
                lLoops = $"{lRoomsPerFloor.Count - lMissing.Count}/{lRoomsPerFloor.Count} floors looped in {lBuilding.name}";
                if (lMissing.Count > 0) lFails.Add($"no patrol loop on floor(s) {string.Join(",", lMissing)} of {lBuilding.name}");
            }
            int lHeard = 0, lDevices = 0;
            foreach (Systems.Distraction lDistraction in Object.FindObjectsByType<Systems.Distraction>(FindObjectsSortMode.None))
            {
                float lReach = lDistraction.name == "Fan" ? 9f : lDistraction.name == "HoseReel" ? 11f : lDistraction.name == "Computer" ? 14f : 0f;
                if (lReach <= 0f) continue;
                lDevices++;
                if (System.Linq.Enumerable.Any(lStops, p => Vector3.Distance(p, lDistraction.transform.position) < lReach)) lHeard++;
            }
            if (lDevices > 0 && lHeard < lDevices * 0.6f) lFails.Add($"only {lHeard}/{lDevices} fans, hoses and PCs are within earshot of a post or a patrol stop (< 60 %)");
            // Vent exits and sentry guns are watched: an armed post or a patrol stop within 20 m of each.
            System.Collections.Generic.List<Vector3> lWatch = new System.Collections.Generic.List<Vector3>();
            foreach (NpcSpawnPoint lSpawn in lSpawns) if (lSpawn.Role != NpcRole.Worker && lSpawn.Role != NpcRole.Target) lWatch.Add(lSpawn.transform.position);
            foreach (PatrolRoute lRoute in lRoutes) for (int i = 0; i < lRoute.Count; i++) lWatch.Add(lRoute.Point(i));
            int lVentsWatched = 0, lVentCount = 0, lGunsWatched = 0;
            foreach (Systems.VentCover lVent in Object.FindObjectsByType<Systems.VentCover>(FindObjectsSortMode.None))
            {
                lVentCount++;
                if (System.Linq.Enumerable.Any(lWatch, p => Vector3.Distance(p, lVent.transform.position) < 20f)) lVentsWatched++;
            }
            foreach (Systems.AutoTurret lGun in lTurrets) if (System.Linq.Enumerable.Any(lWatch, p => Vector3.Distance(p, lGun.transform.position) < 20f)) lGunsWatched++;
            if (lVentCount > 0 && lVentsWatched < lVentCount * 0.6f) lFails.Add($"only {lVentsWatched}/{lVentCount} vent exits have a post or a patrol within 20 m (< 60 %)");
            if (lGunsWatched < lTurrets.Length) lFails.Add($"{lTurrets.Length - lGunsWatched} sentry guns with no post or patrol within 20 m");
            int lExits = Object.FindObjectsByType<ExitPoint>(FindObjectsSortMode.None).Length;
            if (lExits < 4) lFails.Add($"{lExits} exits for fleeing civilians (< 4)");
            if (System.Linq.Enumerable.Count(lPois, p => p.Type == UI.MapPoi.Kind.Landmark) < 3) lFails.Add("fewer than 3 landmark POIs");
            if (pMap.targets.Count != 15) lFails.Add($"{pMap.targets.Count} targets (!= 15)");
            foreach (TargetProfile lTarget in pMap.targets)
                foreach (ActivityType lActivity in lTarget.routine)
                    if (!System.Linq.Enumerable.Any(lNodes, n => n.Zone == lTarget.zone && n.Activity == lActivity))
                        lFails.Add($"{lTarget.displayName}: no {lActivity} node in zone {lTarget.zone}");
            foreach (MatchPoint lStart in lPoints)
                if (lStart.PointKind == MatchPoint.Kind.Start && !UnityEngine.AI.NavMesh.SamplePosition(lStart.transform.position, out _, 3f, UnityEngine.AI.NavMesh.AllAreas)) lFails.Add($"start {lStart.Label} off the NavMesh");
            System.Collections.Generic.List<Vector3> lThreats = MatchDirector.ThreatPositions();
            foreach (MatchPoint lStart in lPoints)
            {
                // Inside starts (with a pass) are among the NPCs on purpose.
                if (lStart.PointKind != MatchPoint.Kind.Start || Kit(lStart.Item)) continue;
                float lClearance = MatchRulesLogic.StartClearance(lStart.transform.position, lThreats);
                if (lClearance < MatchRulesLogic.SAFE_START_DISTANCE) lFails.Add($"start {lStart.Label} {lClearance:0} m from an NPC (< {MatchRulesLogic.SAFE_START_DISTANCE:0})");
            }
            int lFields = 0;
            foreach (Transform lGroup in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (lGroup.name == "TallGrass" && lGroup.GetComponentInChildren<Systems.TallGrass>() != null) lFields++;
            if (lFields < 6) lFails.Add($"{lFields} tall grass fields (< 6)");
            int lOffMesh = 0;
            string lOffExample = "";
            foreach (NpcSpawnPoint lSpawn in lSpawns)
                if (!UnityEngine.AI.NavMesh.SamplePosition(lSpawn.transform.position, out _, 1f, UnityEngine.AI.NavMesh.AllAreas)) { lOffMesh++; if (lOffExample.Length == 0) lOffExample = $" e.g. {lSpawn.name} at {lSpawn.transform.position:F0}"; }
            if (lOffMesh > 0) lFails.Add($"{lOffMesh} NPC posts off the NavMesh{lOffExample}");
            // No isolated NPCs: every post has another NPC or a patrol point within 25 m (lookouts excepted).
            int lAlone = 0;
            string lAloneExample = "";
            foreach (NpcSpawnPoint lSpawn in lSpawns)
            {
                if (lSpawn.Role == NpcRole.Lookout || lSpawn.Route != null) continue;
                bool lNear = false;
                foreach (Vector3 lOther in lThreats)
                {
                    Vector3 d = lOther - lSpawn.transform.position;
                    if (d.sqrMagnitude > 0.25f && d.sqrMagnitude < 25f * 25f) { lNear = true; break; }
                }
                if (!lNear) { lAlone++; if (lAloneExample.Length == 0) lAloneExample = $" e.g. {lSpawn.name} at {lSpawn.transform.position:F0}"; }
            }
            if (lAlone > 0) lFails.Add($"{lAlone} NPCs more than 25 m from any other{lAloneExample}");
            // Entries: the key building (largest volume) has 5+ ways in, the next two 3+.
            Systems.BuildingVolume[] lBuildings = Object.FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None);
            System.Array.Sort(lBuildings, (a, b) => (b.Size.x * b.Size.z).CompareTo(a.Size.x * a.Size.z));
            for (int i = 0; i < Mathf.Min(3, lBuildings.Length); i++)
            {
                Bounds lArea = new Bounds(lBuildings[i].transform.position + Vector3.up * lBuildings[i].Size.y * 0.5f, lBuildings[i].Size + new Vector3(16f, 30f, 16f));
                int lEntries = System.Linq.Enumerable.Count(lPois, p => p.Type != UI.MapPoi.Kind.Landmark && p.Type != UI.MapPoi.Kind.Overlook && lArea.Contains(p.transform.position));
                int lNeed = i == 0 ? 5 : 3;
                if (lEntries < lNeed) lFails.Add($"{lBuildings[i].name}: {lEntries} entries (< {lNeed})");
            }
            // Floating props: anything placed in the props groups must stand on something.
            int lFloating = 0;
            string lFloatingExample = "";
            Terrain lLand = Terrain.activeTerrain;
            System.Collections.Generic.List<Bounds> lWaters = new System.Collections.Generic.List<Bounds>();
            foreach (MeshRenderer lWater in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (lWater.gameObject.layer == GameLayers.WATER) lWaters.Add(lWater.bounds);
            bool OverWater(Vector3 pFoot) => System.Linq.Enumerable.Any(lWaters, w => pFoot.x >= w.min.x && pFoot.x <= w.max.x && pFoot.z >= w.min.z && pFoot.z <= w.max.z && pFoot.y > w.max.y - 0.8f && pFoot.y - w.max.y < 2.5f);
            Physics.SyncTransforms();
            foreach (Transform lGroup in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (lLand == null || (lGroup.name != "Props" && lGroup.name != "ArchProps")) continue;
                foreach (Transform lProp in lGroup)
                {
                    Renderer[] lRenderers = lProp.GetComponentsInChildren<Renderer>();
                    if (lRenderers.Length == 0) continue;
                    Bounds lBounds = lRenderers[0].bounds;
                    foreach (Renderer r in lRenderers) lBounds.Encapsulate(r.bounds);
                    if (lBounds.size.x > 30f || lBounds.size.z > 30f) continue;
                    Vector3 lFoot = new Vector3(lBounds.center.x, lBounds.min.y, lBounds.center.z);
                    float lGround = lLand.SampleHeight(lFoot) + lLand.transform.position.y;
                    if (lFoot.y - lGround < 0.3f || OverWater(lFoot)) continue;
                    bool lSupported = false;
                    foreach (Collider lTouch in Physics.OverlapBox(lBounds.center, lBounds.extents + Vector3.one * 0.12f, Quaternion.identity, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                        if (!lTouch.transform.IsChildOf(lProp)) { lSupported = true; break; }
                    if (lSupported) continue;
                    lFloating++;
                    if (lFloatingExample.Length == 0) lFloatingExample = $" e.g. {lProp.name} at {lFoot:F0} +{lFoot.y - lGround:0.0} m";
                }
            }
            if (lFloating > 0) lFails.Add($"{lFloating} floating props{lFloatingExample}");
            string lScene = EditorAssets.SCENES + "/" + pMap.sceneName + ".unity";
            float lMegabytes = System.IO.File.Exists(lScene) ? new System.IO.FileInfo(lScene).Length / (1024f * 1024f) : 0f;
            if (lMegabytes > 90f) lFails.Add($"scene {lMegabytes:0} MB (> 90)");
            string lCounts = $"{lStarts} starts ({lInside} inside), {lExtractions} extractions, {lWeapons} weapons, {lThrowables} throwables, {lNpcs} NPCs, {lVents} vents, {lClimbs} climbs, {lSecure} secure doors, {lCameras} cameras, {lTurrets.Length} sentry guns, {lTerminals.Length} terminals, {lFans} fans, {lHoses} hoses, {lPcs} PCs, {lMoving}/{lArmed} guards moving, {lRoutes.Length} routes ({lAudit.townRoutes} in town, {lAudit.stops} errands), {lLoops}, {lExits} exits, {lCrowdServed}/{lCrowdCount} with break spots, {lVentsWatched}/{lVentCount} vents and {lGunsWatched}/{lTurrets.Length} guns watched, {lCovers} covers, {lMegabytes:0} MB";
            return lFails.Count == 0 ? $"OK   {pMap.displayName}: {lCounts}" : $"FAIL {pMap.displayName}: {string.Join("; ", lFails)}  ({lCounts})";
        }
    }
}
