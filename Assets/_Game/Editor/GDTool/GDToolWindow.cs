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
            Fields(lRules, "playerHealth", "playerDamageMultiplier", "maxPlayers", "timeLimitMinutes", "proximityChatByDefault", "proximityChatRange", "proofRevealInterval", "extractionTime", "activeExtractions");
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
            if (GUILayout.Button("Pinecrest Hollow (forest)")) new MapForest().Build();
            if (GUILayout.Button("Mirage Bay (gulf)")) new MapGulf().Build();
            if (GUILayout.Button("Elmridge (suburb)")) new MapSuburb().Build();
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
            int Count(MatchPoint.Kind pKind) => System.Linq.Enumerable.Count(lPoints, p => p.PointKind == pKind);
            int lStarts = Count(MatchPoint.Kind.Start), lExtractions = Count(MatchPoint.Kind.Extraction), lWeapons = Count(MatchPoint.Kind.WeaponSpawn);
            int lRockets = System.Linq.Enumerable.Count(lPoints, p => p.PointKind == MatchPoint.Kind.UniqueWeaponSpawn && p.Item == ItemKind.RocketLauncher);
            int lFlames = System.Linq.Enumerable.Count(lPoints, p => p.PointKind == MatchPoint.Kind.UniqueWeaponSpawn && p.Item == ItemKind.Flamethrower);
            int lNpcs = Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None).Length;
            int lZones = Object.FindObjectsByType<LevelZone>(FindObjectsSortMode.None).Length;
            int lCovers = Object.FindObjectsByType<CoverPoint>(FindObjectsSortMode.None).Length;
            if (lStarts < 3) lFails.Add($"{lStarts} start points (< 3)");
            if (lExtractions < 3) lFails.Add($"{lExtractions} extractions (< 3)");
            if (lWeapons < 40) lFails.Add($"{lWeapons} weapon spawns (< 40)");
            if (lRockets < 3) lFails.Add($"{lRockets} rocket launcher spots (< 3)");
            if (lFlames < 3) lFails.Add($"{lFlames} flamethrower spots (< 3)");
            if (lZones != 3) lFails.Add($"{lZones} zones (!= 3)");
            if (lNpcs < 150) lFails.Add($"{lNpcs} NPCs (< 150)");
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
                if (lStart.PointKind != MatchPoint.Kind.Start) continue;
                float lClearance = MatchRulesLogic.StartClearance(lStart.transform.position, lThreats);
                if (lClearance < MatchRulesLogic.SAFE_START_DISTANCE) lFails.Add($"start {lStart.Label} {lClearance:0} m from an NPC (< {MatchRulesLogic.SAFE_START_DISTANCE:0})");
            }
            int lOffMesh = 0;
            foreach (NpcSpawnPoint lSpawn in Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None))
                if (!UnityEngine.AI.NavMesh.SamplePosition(lSpawn.transform.position, out _, 1f, UnityEngine.AI.NavMesh.AllAreas)) lOffMesh++;
            if (lOffMesh > 0) lFails.Add($"{lOffMesh} NPC posts off the NavMesh");
            int lFloating = 0;
            string lFloatingExample = "";
            Terrain lLand = Terrain.activeTerrain;
            System.Collections.Generic.List<Bounds> lWaters = new System.Collections.Generic.List<Bounds>();
            foreach (MeshRenderer lWater in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (lWater.gameObject.layer == GameLayers.WATER) lWaters.Add(lWater.bounds);
            // Piers and boats stand over water, not over its bed.
            bool OverWater(Vector3 pFoot) => System.Linq.Enumerable.Any(lWaters, w => pFoot.x >= w.min.x && pFoot.x <= w.max.x && pFoot.z >= w.min.z && pFoot.z <= w.max.z && pFoot.y > w.max.y - 0.6f && pFoot.y - w.max.y < 2.5f);
            Physics.SyncTransforms();
            foreach (MeshRenderer lRenderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (lLand == null || !lRenderer.gameObject.isStatic || lRenderer.transform.parent == null) continue;
                // Pieces under a parent other than the props/env groups belong to buildings (floors, roofs).
                string lGroup = lRenderer.transform.parent.name;
                if (lGroup != "Props" && lGroup != "Env" && lGroup != "TreesAll") continue;
                Bounds lBounds = lRenderer.bounds;
                Vector3 lFoot = new Vector3(lBounds.center.x, lBounds.min.y, lBounds.center.z);
                float lGround = lLand.SampleHeight(lFoot) + lLand.transform.position.y;
                if (lFoot.y - lGround < 0.3f || OverWater(lFoot)) continue;
                // Held by anything it touches: a floor under it, a table, or a wall it hangs on (TVs, grilles).
                bool lSupported = false;
                foreach (Collider lTouch in Physics.OverlapBox(lBounds.center, lBounds.extents + Vector3.one * 0.12f, Quaternion.identity, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
                    if (lTouch.gameObject != lRenderer.gameObject) { lSupported = true; break; }
                if (lSupported) continue;
                lFloating++;
                if (lFloatingExample.Length == 0) lFloatingExample = $" e.g. {lRenderer.name} at {lFoot:F0} +{lFoot.y - lGround:0.0} m";
            }
            if (lFloating > 0) lFails.Add($"{lFloating} floating props{lFloatingExample}");
            string lCounts = $"{lStarts} starts, {lExtractions} extractions, {lWeapons} weapons, {lNpcs} NPCs, {lCovers} covers";
            return lFails.Count == 0 ? $"OK   {pMap.displayName}: {lCounts}" : $"FAIL {pMap.displayName}: {string.Join("; ", lFails)}  ({lCounts})";
        }
    }
}
