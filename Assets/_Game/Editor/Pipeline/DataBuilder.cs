using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Builds the data assets: every item with its tuning, the rules, the three maps with their
    /// fifteen original targets each, and the database in Resources. Existing assets are updated in
    /// place (GUIDs kept), but a value a designer changed in the GD Tool is only overwritten when
    /// <c>pResetTuning</c> is true.
    /// </summary>
    public static class DataBuilder
    {
        [MenuItem("Goobyr/Build/Data")]
        public static void BuildMenu() => Build(false);

        [MenuItem("Goobyr/Build/Data (reset tuning)")]
        public static void BuildReset() => Build(true);

        public static GameDatabase Build(bool pResetTuning)
        {
            EditorAssets.EnsureFolder(EditorAssets.DATA + "/Items");
            EditorAssets.EnsureFolder(EditorAssets.DATA + "/Maps");
            EditorAssets.EnsureFolder(EditorAssets.RESOURCES);

            List<ItemDefinition> lItems = new List<ItemDefinition>
            {
                Item("SilencedPistol", "Silenced Pistol", ItemKind.SilencedPistol, ItemSlot.Weapon, new Color(0.35f, 0.75f, 0.95f), pResetTuning, i =>
                { i.damage = 55; i.headMultiplier = 3; i.fireRate = 3.2f; i.magazine = 10; i.reserveAmmo = 30; i.spread = 1.2f; i.adsFov = 62; i.recoil = 1.4f; i.shake = 0.08f; i.noiseRadius = 9; i.range = 90; i.reloadTime = 1.3f; }),
                Item("Pistol", "Pistol", ItemKind.Pistol, ItemSlot.Weapon, new Color(0.9f, 0.6f, 0.3f), pResetTuning, i =>
                { i.damage = 62; i.headMultiplier = 3; i.fireRate = 4f; i.magazine = 12; i.reserveAmmo = 36; i.spread = 1.4f; i.adsFov = 62; i.recoil = 1.8f; i.shake = 0.12f; i.noiseRadius = 45; i.range = 100; i.reloadTime = 1.2f; }),
                Item("Smg", "SMG", ItemKind.Smg, ItemSlot.Weapon, new Color(0.95f, 0.85f, 0.3f), pResetTuning, i =>
                { i.damage = 27; i.headMultiplier = 2.2f; i.fireRate = 11; i.automatic = true; i.magazine = 30; i.reserveAmmo = 90; i.spread = 2.6f; i.adsSpreadFactor = 0.45f; i.adsFov = 64; i.recoil = 0.9f; i.shake = 0.1f; i.noiseRadius = 55; i.range = 80; i.reloadTime = 1.8f; }),
                Item("Shotgun", "Shotgun", ItemKind.Shotgun, ItemSlot.Weapon, new Color(0.85f, 0.35f, 0.3f), pResetTuning, i =>
                { i.damage = 150; i.headMultiplier = 1.5f; i.pellets = 8; i.fireRate = 1.2f; i.magazine = 6; i.reserveAmmo = 18; i.spread = 6.5f; i.adsSpreadFactor = 0.7f; i.adsFov = 68; i.recoil = 4.5f; i.shake = 0.45f; i.noiseRadius = 60; i.range = 32; i.reloadTime = 2.4f; }),
                Item("Sniper", "Sniper Rifle", ItemKind.Sniper, ItemSlot.Weapon, new Color(0.45f, 0.8f, 0.45f), pResetTuning, i =>
                { i.damage = 150; i.headMultiplier = 3; i.fireRate = 0.8f; i.magazine = 5; i.reserveAmmo = 15; i.spread = 4f; i.adsSpreadFactor = 0.02f; i.adsFov = 18; i.recoil = 6f; i.shake = 0.35f; i.noiseRadius = 90; i.range = 450; i.reloadTime = 2.6f; }),
                Item("RocketLauncher", "Rocket Launcher", ItemKind.RocketLauncher, ItemSlot.Weapon, new Color(1f, 0.75f, 0.2f), pResetTuning, i =>
                { i.unique = true; i.damage = 260; i.explosionRadius = 7.5f; i.projectileSpeed = 42; i.fireRate = 0.7f; i.magazine = 1; i.reserveAmmo = 4; i.spread = 0.5f; i.adsFov = 60; i.recoil = 8f; i.shake = 0.6f; i.noiseRadius = 80; i.range = 200; i.reloadTime = 2.8f; }),
                Item("Flamethrower", "Flamethrower", ItemKind.Flamethrower, ItemSlot.Weapon, new Color(1f, 0.45f, 0.15f), pResetTuning, i =>
                { i.unique = true; i.damage = 7; i.fireRate = 8; i.automatic = true; i.magazine = 150; i.reserveAmmo = 150; i.spread = 0; i.recoil = 0.1f; i.shake = 0.05f; i.noiseRadius = 25; i.range = 9; i.reloadTime = 2.5f; }),
                Item("Coin", "Coin", ItemKind.Coin, ItemSlot.Gadget, new Color(1f, 0.85f, 0.3f), pResetTuning, i => { i.stack = 3; i.throwForce = 16; }),
                Item("Brick", "Brick", ItemKind.Brick, ItemSlot.Gadget, new Color(0.8f, 0.4f, 0.3f), pResetTuning, i => { i.stack = 1; i.throwForce = 20; }),
                Item("SmokeGrenade", "Smoke Grenade", ItemKind.SmokeGrenade, ItemSlot.Gadget, new Color(0.7f, 0.75f, 0.8f), pResetTuning, i => { i.stack = 2; i.throwForce = 17; }),
                Item("C4", "C4 Charge", ItemKind.C4, ItemSlot.Gadget, new Color(0.9f, 0.85f, 0.6f), pResetTuning, i => { i.stack = 2; i.damage = 280; i.explosionRadius = 7.5f; i.throwForce = 10; }),
                Item("Poison", "Lethal Poison", ItemKind.Poison, ItemSlot.Gadget, new Color(0.45f, 1f, 0.35f), pResetTuning, i => { i.stack = 1; }),
                Item("EmeticPoison", "Emetic Poison", ItemKind.EmeticPoison, ItemSlot.Gadget, new Color(1f, 0.85f, 0.3f), pResetTuning, i => { i.stack = 1; }),
                Item("Rock", "Rock", ItemKind.Rock, ItemSlot.Gadget, new Color(0.55f, 0.53f, 0.5f), pResetTuning, i => { i.stack = 2; i.throwForce = 19; }),
                Item("Bottle", "Bottle", ItemKind.Bottle, ItemSlot.Gadget, new Color(0.35f, 0.55f, 0.35f), pResetTuning, i => { i.stack = 1; i.throwForce = 18; }),
                Item("Wrench", "Wrench", ItemKind.Wrench, ItemSlot.Gadget, new Color(0.6f, 0.6f, 0.62f), pResetTuning, i => { i.stack = 1; i.throwForce = 19; }),
                Item("Invitation", "Invitation", ItemKind.Invitation, ItemSlot.Gadget, new Color(0.95f, 0.88f, 0.7f), pResetTuning, i => { i.stack = 1; }),
                Item("StaffPass", "Staff Pass", ItemKind.StaffPass, ItemSlot.Gadget, new Color(0.5f, 0.75f, 0.95f), pResetTuning, i => { i.stack = 1; }),
                Item("Keycard", "Keycard", ItemKind.Keycard, ItemSlot.Gadget, new Color(0.9f, 0.3f, 0.3f), pResetTuning, i => { i.stack = 1; }),
                Item("Crowbar", "Crowbar", ItemKind.Crowbar, ItemSlot.Gadget, new Color(0.7f, 0.2f, 0.15f), pResetTuning, i => { i.stack = 1; }),
                Item("CodeNote", "Code Note", ItemKind.CodeNote, ItemSlot.Gadget, new Color(0.95f, 0.95f, 0.85f), pResetTuning, i => { i.stack = 1; })
            };

            // How far each item is heard in the open (walls muffle it, DetectionModel.Loudness): kept in
            // step with the 20 m sight range so a shot pulls the guards of one area, never the map.
            Dictionary<ItemKind, float> lNoise = new Dictionary<ItemKind, float>
            {
                { ItemKind.SilencedPistol, 8f }, { ItemKind.Pistol, 40f }, { ItemKind.Smg, 45f }, { ItemKind.Shotgun, 50f },
                { ItemKind.Sniper, 70f }, { ItemKind.RocketLauncher, 70f }, { ItemKind.Flamethrower, 20f },
                { ItemKind.Coin, 12f }, { ItemKind.Brick, 11f }, { ItemKind.Rock, 10f }, { ItemKind.Bottle, 15f }, { ItemKind.Wrench, 12f }
            };
            foreach (ItemDefinition lItem in lItems)
                if (lNoise.TryGetValue(lItem.kind, out float lRadius)) { lItem.noiseRadius = lRadius; EditorUtility.SetDirty(lItem); }

            GameRules lRules = EditorAssets.LoadOrCreate<GameRules>(EditorAssets.DATA + "/GameRules.asset");
            if (lRules.startingLoadout.Count == 0 || pResetTuning)
            {
                lRules.startingLoadout = new List<ItemDefinition> { lItems[0], lItems[7] };
            }
            EditorUtility.SetDirty(lRules);

            List<MapDefinition> lMaps = new List<MapDefinition>
            {
                Map("Map_SanLazaro", "San Lázaro", "Map_SanLazaro",
                    "A Colombian river town on fiesta day: the plaza and the church, five coffee terraces climbing to an airstrip on the ridge, and the white Hacienda La Cumbre with its mirador over the gorge.",
                    new[] { "Pueblo", "Cafetal & Airstrip", "Hacienda La Cumbre" },
                    new Color(0.25f, 0.5f, 0.85f), new Color(0.85f, 0.9f, 0.92f), new Color(0.7f, 0.78f, 0.76f), new Color(1f, 0.95f, 0.85f), new Color(0.45f, 0.5f, 0.48f), 2.2f, new Vector3(55f, 30f, 0), 0.0025f, pResetTuning, SanLazaroTargets()),
                Map("Map_QasrAlRimal", "Qasr al-Rimal", "Map_QasrAlRimal",
                    "An oasis town on audience day: the souk under the Great Mesa, a palm grove and its dry wadi, the governor's citadel on the rock and a radar on the Little Mesa.",
                    new[] { "Ain Zarqa", "Grove, Wadi & Radar", "The Citadel" },
                    new Color(0.2f, 0.45f, 0.85f), new Color(0.95f, 0.88f, 0.75f), new Color(0.95f, 0.85f, 0.7f), new Color(1f, 0.95f, 0.85f), new Color(0.6f, 0.55f, 0.5f), 2.6f, new Vector3(62f, -20f, 0), 0.0015f, pResetTuning, QasrTargets()),
                Map("Map_Weisshorn", "Weisshorn", "Map_Weisshorn",
                    "An alpine gala night: the chalet village by the frozen lake, the pistes and the chairlift, the Grand Hotel on its platform and the cold-war observatory on the summit.",
                    new[] { "Sankt Anna", "Ski Area & Observatory", "Grand Hotel Weisshorn" },
                    new Color(0.3f, 0.45f, 0.75f), new Color(0.86f, 0.9f, 0.95f), new Color(0.8f, 0.84f, 0.9f), new Color(1f, 0.97f, 0.94f), new Color(0.55f, 0.6f, 0.7f), 2.0f, new Vector3(32f, 40f, 0), 0.003f, pResetTuning, WeisshornTargets())
            };

            // Ambiance per map: a misty pine morning, a hazy golden Gulf afternoon, a pink suburban dusk.
            Atmosphere(lMaps[0], new Color(0.24f, 0.48f, 0.84f), new Color(0.84f, 0.89f, 0.9f), new Color(1f, 0.88f, 0.72f), new Color(1f, 0.95f, 0.86f), 2.2f, new Vector3(55f, 30f, 0), 0.5f, 0.006f, 0f, 22f, new Color(0.3f, 0.36f, 0.34f), 0.9f, 4f, 12f);
            Atmosphere(lMaps[1], new Color(0.2f, 0.45f, 0.86f), new Color(0.96f, 0.88f, 0.74f), new Color(1f, 0.82f, 0.55f), new Color(1f, 0.93f, 0.8f), 2.8f, new Vector3(62f, -20f, 0), 0.12f, 0.003f, 0f, 40f, new Color(0.4f, 0.36f, 0.3f), 0.7f, 10f, 12f);
            Atmosphere(lMaps[2], new Color(0.3f, 0.46f, 0.76f), new Color(0.86f, 0.9f, 0.95f), new Color(1f, 0.92f, 0.85f), new Color(1f, 0.97f, 0.94f), 2.0f, new Vector3(32f, 40f, 0), 0.45f, 0.006f, 0f, 24f, new Color(0.38f, 0.42f, 0.5f), 0.8f, -6f, 12f);

            GameDatabase lDatabase = EditorAssets.LoadOrCreate<GameDatabase>(EditorAssets.RESOURCES + "/GameDatabase.asset");
            lDatabase.rules = lRules;
            lDatabase.items = lItems;
            lDatabase.maps = lMaps;
            EditorUtility.SetDirty(lDatabase);
            AssetDatabase.SaveAssets();
            lDatabase.Index();
            Debug.Log($"[Build] data: {lItems.Count} items, {lMaps.Count} maps");
            return lDatabase;
        }

        private static ItemDefinition Item(string pFile, string pName, ItemKind pKind, ItemSlot pSlot, Color pColor, bool pReset, System.Action<ItemDefinition> pTune)
        {
            string lPath = EditorAssets.DATA + "/Items/" + pFile + ".asset";
            bool lNew = AssetDatabase.LoadAssetAtPath<ItemDefinition>(lPath) == null;
            ItemDefinition lItem = EditorAssets.LoadOrCreate<ItemDefinition>(lPath);
            lItem.displayName = pName;
            lItem.kind = pKind;
            lItem.slot = pSlot;
            lItem.color = pColor;
            if (lNew || pReset) pTune(lItem);
            EditorUtility.SetDirty(lItem);
            return lItem;
        }

        private static MapDefinition Map(string pFile, string pName, string pScene, string pDescription, string[] pZones, Color pTop, Color pHorizon, Color pFog, Color pSun, Color pAmbient, float pSunIntensity, Vector3 pSunEuler, float pFogDensity, bool pReset, List<TargetProfile> pTargets)
        {
            string lPath = EditorAssets.DATA + "/Maps/" + pFile + ".asset";
            bool lNew = AssetDatabase.LoadAssetAtPath<MapDefinition>(lPath) == null;
            MapDefinition lMap = EditorAssets.LoadOrCreate<MapDefinition>(lPath);
            lMap.displayName = pName;
            lMap.sceneName = pScene;
            lMap.description = pDescription;
            lMap.zoneNames = pZones;
            lMap.skyTop = pTop;
            lMap.skyHorizon = pHorizon;
            lMap.fog = pFog;
            lMap.sun = pSun;
            lMap.ambient = pAmbient;
            lMap.sunIntensity = pSunIntensity;
            lMap.sunEuler = pSunEuler;
            lMap.fogDensity = pFogDensity;
            if (lNew || pReset || lMap.targets.Count != 15) lMap.targets = pTargets;
            EditorUtility.SetDirty(lMap);
            return lMap;
        }

        private static void Atmosphere(MapDefinition pMap, Color pZenith, Color pHorizon, Color pHaze, Color pSun, float pSunIntensity, Vector3 pSunEuler, float pClouds, float pVolumetric, float pFogHeight, float pFalloff, Color pFogAmbient, float pScatter, float pTemperature, float pContrast)
        {
            pMap.skyTop = pZenith;
            pMap.skyHorizon = pHorizon;
            pMap.hazeColor = pHaze;
            pMap.sun = pSun;
            pMap.sunIntensity = pSunIntensity;
            pMap.sunEuler = pSunEuler;
            pMap.cloudCoverage = pClouds;
            pMap.volumetricDensity = pVolumetric;
            pMap.volumetricHeight = pFogHeight;
            pMap.volumetricFalloff = pFalloff;
            pMap.volumetricAmbient = pFogAmbient;
            pMap.sunScattering = pScatter;
            pMap.temperature = pTemperature;
            pMap.contrast = pContrast;
            pMap.fog = Color.Lerp(pHorizon, pFogAmbient, 0.4f);
            pMap.fogDensity = 0.0012f;
            pMap.postExposure = -0.15f;
            pMap.saturation = 14f;
            pMap.ambient = Color.Lerp(pZenith, pHorizon, 0.5f) * 0.75f;
            EditorUtility.SetDirty(pMap);
        }

        // ---------------------------------------------------------------- targets (all original)

        private static TargetProfile T(string pName, string pDossier, int pZone, string pColor, HatStyle pHat, float pScale, int pGuards, ProofKind pProof, params ActivityType[] pRoutine)
        {
            ColorUtility.TryParseHtmlString(pColor, out Color lColor);
            return new TargetProfile { displayName = pName, dossier = pDossier, zone = pZone, bodyColor = lColor, hat = pHat, scale = pScale, bodyguards = pGuards, proof = pProof, routine = new List<ActivityType>(pRoutine) };
        }

        private static List<TargetProfile> SanLazaroTargets() => new List<TargetProfile>
        {
            T("Doña Celestina Ruiz", "Sponsors the fiesta, launders the cartel's coffee money. Stage, cantina, bell deck, dock.", 0, "#C0392B", HatStyle.None, 1.05f, 2, ProofKind.Ring, ActivityType.Speech, ActivityType.Drink, ActivityType.Lookout, ActivityType.Idle),
            T("Fabio 'El Mono' Rengifo", "Owns the cantina and the smugglers' cave under it.", 0, "#E67E22", HatStyle.Cap, 1.15f, 2, ProofKind.Ledger, ActivityType.Drink, ActivityType.Dining, ActivityType.Office, ActivityType.Toilet),
            T("Inspector Gilma Pardo", "The pueblo's police chief. Paid by everyone.", 0, "#2E4053", HatStyle.Cap, 1.1f, 2, ProofKind.Badge, ActivityType.Office, ActivityType.Phone, ActivityType.Inspect, ActivityType.Smoke),
            T("Wilber Castaño", "Boatman. Runs the river for whoever pays.", 0, "#1F618D", HatStyle.Cowboy, 1.1f, 1, ProofKind.Dossier, ActivityType.Idle, ActivityType.Workout, ActivityType.Smoke, ActivityType.Drink),
            T("Mayor Hernando Lozada", "Opens the fiesta, signs whatever the hacienda sends down.", 0, "#F4D03F", HatStyle.Fedora, 1.15f, 2, ProofKind.Keycard, ActivityType.Speech, ActivityType.Dining, ActivityType.Phone, ActivityType.Garden),
            T("Ilse Varga", "The buyer. Flies in, tastes the coffee, buys the rest.", 1, "#5B2C6F", HatStyle.Beret, 1.05f, 2, ProofKind.Briefcase, ActivityType.Inspect, ActivityType.Phone, ActivityType.Drink, ActivityType.Dining),
            T("Captain Rodrigo Duarte", "Commands the airstrip's sicarios. Sees through every pass.", 1, "#4D5656", HatStyle.Beret, 1.25f, 3, ProofKind.Keycard, ActivityType.Lookout, ActivityType.Inspect, ActivityType.Smoke, ActivityType.Workout),
            T("Nelson Arbeláez", "The capataz. Every picker owes him a week.", 1, "#7E5109", HatStyle.Cowboy, 1.15f, 2, ProofKind.Dossier, ActivityType.Garden, ActivityType.Speech, ActivityType.Smoke, ActivityType.Toilet),
            T("Lucho Guerrero", "Pilot. Never sober on the ground.", 1, "#117864", HatStyle.Cap, 1.05f, 1, ProofKind.Dossier, ActivityType.Dining, ActivityType.Inspect, ActivityType.Drink, ActivityType.Workout),
            T("Dra. Amparo Rincón", "The chemist behind the 'fungicide'.", 1, "#D5DBDB", HatStyle.None, 1.0f, 2, ProofKind.Ledger, ActivityType.Office, ActivityType.Garden, ActivityType.Drink, ActivityType.Toilet),
            T("Don Ramiro Elizalde", "The patrón. Study, toast on the loggia, chapel, dinner.", 2, "#F8F9F9", HatStyle.Fedora, 1.15f, 2, ProofKind.Ring, ActivityType.Office, ActivityType.Speech, ActivityType.Idle, ActivityType.Dining),
            T("Teodoro Aranda", "The accountant. Counts the vault, drinks his coffee, checks the wine.", 2, "#566573", HatStyle.None, 1.0f, 1, ProofKind.Ledger, ActivityType.Office, ActivityType.Drink, ActivityType.Inspect, ActivityType.Toilet),
            T("Valentina Elizalde", "The heir. Runs the reception like a campaign.", 2, "#A93226", HatStyle.Beret, 1.0f, 2, ProofKind.Ring, ActivityType.Garden, ActivityType.Drink, ActivityType.Dining, ActivityType.Phone),
            T("Celso Ibarra", "The mayordomo. Knows every door and who opens it.", 2, "#283747", HatStyle.None, 1.05f, 1, ProofKind.Keycard, ActivityType.Inspect, ActivityType.Phone, ActivityType.Dining, ActivityType.Smoke),
            T("Senator Octavio Peñalosa", "Guest of honour. Sells the valley's roads.", 2, "#1A5276", HatStyle.TopHat, 1.15f, 3, ProofKind.Briefcase, ActivityType.Speech, ActivityType.Drink, ActivityType.Garden, ActivityType.Toilet)
        };

        private static List<TargetProfile> QasrTargets() => new List<TargetProfile>
        {
            T("Selin Arda", "Arms buyer. Bus station, tea house, caravanserai, never sits far from a brazier.", 0, "#7D3C98", HatStyle.Beret, 1.05f, 2, ProofKind.Briefcase, ActivityType.Inspect, ActivityType.Speech, ActivityType.Phone, ActivityType.Drink),
            T("Hajj Mansour", "Gold dealer of the covered souk. Weighs everything twice.", 0, "#D4AC0D", HatStyle.Turban, 1.1f, 2, ProofKind.Ring, ActivityType.Office, ActivityType.Drink, ActivityType.Dining, ActivityType.Toilet),
            T("Sheikh Badr al-Fahim", "Caravan master. Every truck in the oasis pays him.", 0, "#A04000", HatStyle.Turban, 1.2f, 2, ProofKind.Ledger, ActivityType.Workout, ActivityType.Dining, ActivityType.Phone, ActivityType.Smoke),
            T("Captain Farid Lahlou", "Town police. Sells the citadel the names of petitioners.", 0, "#1B4F72", HatStyle.Cap, 1.15f, 2, ProofKind.Badge, ActivityType.Office, ActivityType.Smoke, ActivityType.Inspect, ActivityType.Garden),
            T("Mona Haddad", "Runs the souk's smugglers from the minaret's shadow.", 0, "#922B21", HatStyle.None, 1.0f, 1, ProofKind.Dossier, ActivityType.Drink, ActivityType.Idle, ActivityType.Lookout, ActivityType.Dining),
            T("Colonel Rashed", "Commands the radar. Walks the rope bridge to the keep every hour.", 1, "#4D5656", HatStyle.Beret, 1.25f, 3, ProofKind.Keycard, ActivityType.Office, ActivityType.Lookout, ActivityType.Idle, ActivityType.Workout),
            T("Adjutant Karim", "The colonel's shadow. Sees through every pass.", 1, "#566573", HatStyle.Cap, 1.15f, 2, ProofKind.Badge, ActivityType.Inspect, ActivityType.Phone, ActivityType.Smoke, ActivityType.Dining),
            T("Dr. Nabil Sayegh", "Radar engineer. The dish is his and nobody else's.", 1, "#5DADE2", HatStyle.None, 1.0f, 1, ProofKind.Briefcase, ActivityType.Office, ActivityType.Workout, ActivityType.Drink, ActivityType.Toilet),
            T("Foreman Youssef", "Runs the date press and the tithe scale.", 1, "#6E2C00", HatStyle.Turban, 1.15f, 1, ProofKind.Ledger, ActivityType.Inspect, ActivityType.Garden, ActivityType.Speech, ActivityType.Smoke),
            T("Layla Bennani", "Buys the oasis's water rights, one well at a time.", 1, "#117A65", HatStyle.Beret, 1.0f, 2, ProofKind.Dossier, ActivityType.Drink, ActivityType.Garden, ActivityType.Phone, ActivityType.Dining),
            T("Governor Nasser al-Hadd", "Study, war room, the throne, tea on the pool terrace.", 2, "#F8F9F9", HatStyle.Crown, 1.15f, 3, ProofKind.Ring, ActivityType.Office, ActivityType.Speech, ActivityType.Drink, ActivityType.Dining),
            T("Treasurer Idris", "Counts the vault, the archive and the tithe.", 2, "#7B7D7D", HatStyle.Fedora, 1.0f, 1, ProofKind.Ledger, ActivityType.Office, ActivityType.Inspect, ActivityType.Phone, ActivityType.Toilet),
            T("Chamberlain Aziz", "Keeps the doors of the keep and the secrets behind them.", 2, "#283747", HatStyle.Turban, 1.05f, 1, ProofKind.Keycard, ActivityType.Inspect, ActivityType.Dining, ActivityType.Phone, ActivityType.Smoke),
            T("Samira al-Hadd", "The governor's daughter. Holds court on the roof.", 2, "#AF7AC5", HatStyle.None, 1.0f, 2, ProofKind.Ring, ActivityType.Drink, ActivityType.Garden, ActivityType.Dining, ActivityType.Toilet),
            T("General Halim Darwish", "Visiting with an army for sale.", 2, "#1D8348", HatStyle.Beret, 1.25f, 3, ProofKind.Dossier, ActivityType.Speech, ActivityType.Workout, ActivityType.Lookout, ActivityType.Drink)
        };

        private static List<TargetProfile> WeisshornTargets() => new List<TargetProfile>
        {
            T("Mara Lindqvist", "The banker. Fondue at the Gasthaus before the gala, always the same table.", 0, "#1F618D", HatStyle.Beret, 1.0f, 2, ProofKind.Briefcase, ActivityType.Dining, ActivityType.Drink, ActivityType.Phone, ActivityType.Idle),
            T("Mayor Gerhard Moll", "Sold the mountain to the hotel, one permit at a time.", 0, "#7B241C", HatStyle.Fedora, 1.15f, 2, ProofKind.Ledger, ActivityType.Speech, ActivityType.Dining, ActivityType.Office, ActivityType.Toilet),
            T("Lotte Brenner", "The landlady. Hears everything the skiers say.", 0, "#B7950B", HatStyle.None, 1.05f, 1, ProofKind.Dossier, ActivityType.Office, ActivityType.Drink, ActivityType.Smoke, ActivityType.Garden),
            T("Inspector Rainer Huber", "The gendarme who never sees the snowcats at night.", 0, "#2E4053", HatStyle.Cap, 1.15f, 2, ProofKind.Badge, ActivityType.Inspect, ActivityType.Smoke, ActivityType.Lookout, ActivityType.Phone),
            T("Pascal Imboden", "Rents skis. Rents other things to the hotel's guests.", 0, "#117864", HatStyle.Beanie, 1.05f, 1, ProofKind.Dossier, ActivityType.Workout, ActivityType.Idle, ActivityType.Drink, ActivityType.Dining),
            T("Viktor Sorel", "Arms dealer. Cable car, bunker, the bar, a cigar on the terrace.", 1, "#4A235A", HatStyle.Fedora, 1.15f, 3, ProofKind.Briefcase, ActivityType.Inspect, ActivityType.Phone, ActivityType.Smoke, ActivityType.Drink),
            T("Dr. Elias Frey", "Runs the observatory. The telescope looks at the hotel.", 1, "#D0D3D4", HatStyle.None, 1.0f, 1, ProofKind.Dossier, ActivityType.Office, ActivityType.Lookout, ActivityType.Dining, ActivityType.Toilet),
            T("Captain Lars Wenger", "Commands the bunker for whoever pays this winter.", 1, "#424949", HatStyle.Beret, 1.25f, 3, ProofKind.Keycard, ActivityType.Inspect, ActivityType.Office, ActivityType.Workout, ActivityType.Smoke),
            T("Annika Roth", "Ski instructor and courier. The pistes are her office.", 1, "#C0392B", HatStyle.Beanie, 1.0f, 1, ProofKind.Dossier, ActivityType.Workout, ActivityType.Drink, ActivityType.Speech, ActivityType.Idle),
            T("Bruno Gasser", "Drives the groomer. Moves crates nobody declares.", 1, "#A04000", HatStyle.Cap, 1.2f, 1, ProofKind.Ledger, ActivityType.Inspect, ActivityType.Garden, ActivityType.Smoke, ActivityType.Toilet),
            T("Konrad Aschauer", "The host. Suite, cinema, the speech under the chandelier, the sauna.", 2, "#17202A", HatStyle.TopHat, 1.15f, 3, ProofKind.Ring, ActivityType.Office, ActivityType.Idle, ActivityType.Speech, ActivityType.Workout),
            T("Chief Brandt", "Hotel security. Inspects everything, trusts nothing.", 2, "#283747", HatStyle.Cap, 1.2f, 2, ProofKind.Keycard, ActivityType.Office, ActivityType.Inspect, ActivityType.Smoke, ActivityType.Phone),
            T("Sabine Aschauer", "The host's wife. The real owner of the hotel.", 2, "#922B21", HatStyle.None, 1.0f, 2, ProofKind.Ring, ActivityType.Drink, ActivityType.Garden, ActivityType.Dining, ActivityType.Toilet),
            T("Dmitri Volkov", "VIP guest. Buys the observatory's data.", 2, "#1C2833", HatStyle.Fedora, 1.2f, 3, ProofKind.Briefcase, ActivityType.Drink, ActivityType.Phone, ActivityType.Lookout, ActivityType.Dining),
            T("Celeste Morel", "The gala singer. Carries messages in her songs.", 2, "#F5B7B1", HatStyle.None, 1.0f, 1, ProofKind.Dossier, ActivityType.Speech, ActivityType.Drink, ActivityType.Toilet, ActivityType.Idle)
        };
    }
}
