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
                Map("Map_Forest", "Pinecrest Hollow", "Map_Forest",
                    "A logging town on a cold river, a fortified commune hidden in the pines with a military outpost next door, and a lakeside timber lodge.",
                    new[] { "Millbrook Town", "The Grove & Outpost", "Lakeside Lodge" },
                    new Color(0.45f, 0.72f, 0.82f), new Color(0.92f, 0.86f, 0.74f), new Color(0.78f, 0.8f, 0.72f), new Color(1f, 0.9f, 0.75f), new Color(0.5f, 0.55f, 0.55f), 1.25f, new Vector3(38f, -40f, 0), 0.0028f, pResetTuning, ForestTargets()),
                Map("Map_Gulf", "Mirage Bay", "Map_Gulf",
                    "A sun-bleached Gulf city: a dense souk, smugglers working the docks under the highway, and the Zenith, a luxury hotel tower with a helipad.",
                    new[] { "Old Souk", "Smugglers' Docks", "The Zenith Tower" },
                    new Color(0.35f, 0.65f, 0.95f), new Color(1f, 0.88f, 0.7f), new Color(0.98f, 0.85f, 0.7f), new Color(1f, 0.93f, 0.8f), new Color(0.6f, 0.55f, 0.5f), 1.5f, new Vector3(55f, 30f, 0), 0.0018f, pResetTuning, GulfTargets()),
                Map("Map_Suburb", "Elmridge", "Map_Suburb",
                    "A tidy American suburb: the downtown strip, a biker hideout in the old junkyard and bowling alley, and the colonial mansion on the hill.",
                    new[] { "Downtown Strip", "Junkyard Bowl", "Hilltop Mansion" },
                    new Color(0.55f, 0.75f, 0.95f), new Color(1f, 0.8f, 0.7f), new Color(0.95f, 0.82f, 0.75f), new Color(1f, 0.85f, 0.7f), new Color(0.55f, 0.5f, 0.6f), 1.35f, new Vector3(30f, 120f, 0), 0.0022f, pResetTuning, SuburbTargets())
            };

            // Ambiance per map: a misty pine morning, a hazy golden Gulf afternoon, a pink suburban dusk.
            Atmosphere(lMaps[0], new Color(0.3f, 0.52f, 0.78f), new Color(0.78f, 0.84f, 0.86f), new Color(1f, 0.82f, 0.6f), new Color(1f, 0.9f, 0.76f), 1.6f, new Vector3(22f, -40f, 0), 0.55f, 0.0065f, -1f, 14f, new Color(0.22f, 0.28f, 0.34f), 0.9f, 2f, 14f);
            Atmosphere(lMaps[1], new Color(0.22f, 0.48f, 0.86f), new Color(0.96f, 0.86f, 0.7f), new Color(1f, 0.75f, 0.45f), new Color(1f, 0.88f, 0.7f), 1.9f, new Vector3(28f, 60f, 0), 0.32f, 0.0035f, 0f, 40f, new Color(0.36f, 0.32f, 0.27f), 0.7f, 14f, 12f);
            Atmosphere(lMaps[2], new Color(0.34f, 0.42f, 0.72f), new Color(1f, 0.74f, 0.62f), new Color(1f, 0.6f, 0.45f), new Color(1f, 0.78f, 0.62f), 1.5f, new Vector3(14f, 120f, 0), 0.5f, 0.0045f, 0f, 20f, new Color(0.3f, 0.25f, 0.3f), 1.0f, 18f, 16f);

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

        private static List<TargetProfile> ForestTargets() => new List<TargetProfile>
        {
            T("Barnaby Thistlewood", "Sawmill owner. Cuts corners and people.", 0, "#C8553D", HatStyle.Cap, 1.15f, 2, ProofKind.Dossier, ActivityType.Inspect, ActivityType.Drink, ActivityType.Phone, ActivityType.Toilet, ActivityType.Smoke),
            T("Marta Quill", "Runs the diner and the town's secrets.", 0, "#E2A03F", HatStyle.Beanie, 1.05f, 1, ProofKind.Ledger, ActivityType.Dining, ActivityType.Drink, ActivityType.Phone, ActivityType.Toilet),
            T("Sheriff Dunmore", "A badge for sale to the highest bidder.", 0, "#5B7DB1", HatStyle.Cowboy, 1.2f, 2, ProofKind.Badge, ActivityType.Lookout, ActivityType.Office, ActivityType.Drink, ActivityType.Smoke),
            T("Otis Fernhallow", "Moonshiner. Never leaves his still for long.", 0, "#7C9A3C", HatStyle.Fedora, 1.1f, 2, ProofKind.Dossier, ActivityType.Inspect, ActivityType.Drink, ActivityType.Smoke, ActivityType.Toilet),
            T("Penny Lark", "The mayor's fixer. Always on the phone.", 0, "#B65FCF", HatStyle.Beret, 1.0f, 1, ProofKind.Keycard, ActivityType.Phone, ActivityType.Office, ActivityType.Drink, ActivityType.Garden),
            T("Elder Wren", "Leader of the Grove. Preaches by the fire.", 1, "#F2E3BC", HatStyle.Turban, 1.2f, 3, ProofKind.Dossier, ActivityType.Speech, ActivityType.Garden, ActivityType.Drink, ActivityType.Toilet),
            T("Colonel Ashgrave", "Commands the outpost. Trusts nobody.", 1, "#556B2F", HatStyle.Beret, 1.25f, 3, ProofKind.Keycard, ActivityType.Inspect, ActivityType.Lookout, ActivityType.Office, ActivityType.Workout),
            T("Sister Moss", "Brews the Grove's 'tea'. Taste-tests it herself.", 1, "#8FBF7F", HatStyle.Beanie, 1.0f, 2, ProofKind.Ring, ActivityType.Garden, ActivityType.Drink, ActivityType.Dining, ActivityType.Toilet),
            T("Quartermaster Bell", "Sells the outpost's guns out the back door.", 1, "#A0522D", HatStyle.Cap, 1.15f, 2, ProofKind.Ledger, ActivityType.Inspect, ActivityType.Smoke, ActivityType.Phone, ActivityType.Drink),
            T("Brother Tamsin", "The Grove's recruiter. Smiles too much.", 1, "#D98E73", HatStyle.None, 1.05f, 2, ProofKind.Dossier, ActivityType.Speech, ActivityType.Workout, ActivityType.Drink, ActivityType.Toilet),
            T("Lady Corvina Vale", "Owns the lodge, the lake and the valley.", 2, "#2F4858", HatStyle.TopHat, 1.1f, 3, ProofKind.Ring, ActivityType.Dining, ActivityType.Drink, ActivityType.Garden, ActivityType.Toilet, ActivityType.Speech),
            T("Rupert Vale", "Heir. Gambles the family fortune at the bar.", 2, "#33658A", HatStyle.Fedora, 1.15f, 2, ProofKind.Dossier, ActivityType.Drink, ActivityType.Phone, ActivityType.Toilet, ActivityType.Smoke),
            T("Doctor Ilse Brandt", "Lodge physician. Prescribes silence.", 2, "#E0E0E0", HatStyle.None, 1.0f, 2, ProofKind.Briefcase, ActivityType.Office, ActivityType.Drink, ActivityType.Dining, ActivityType.Toilet),
            T("Huntmaster Gale", "Leads the lodge's hunts. Prey varies.", 2, "#8B5A2B", HatStyle.Cowboy, 1.25f, 2, ProofKind.Dossier, ActivityType.Lookout, ActivityType.Smoke, ActivityType.Drink, ActivityType.Inspect),
            T("The Accountant", "Nobody knows his name. Everybody knows his ledger.", 2, "#4A4A4A", HatStyle.Fedora, 1.05f, 3, ProofKind.Ledger, ActivityType.Office, ActivityType.Phone, ActivityType.Drink, ActivityType.Toilet)
        };

        private static List<TargetProfile> GulfTargets() => new List<TargetProfile>
        {
            T("Faisal Amberline", "Spice merchant. The spice is not the product.", 0, "#E07A5F", HatStyle.Turban, 1.15f, 2, ProofKind.Ledger, ActivityType.Inspect, ActivityType.Drink, ActivityType.Phone, ActivityType.Toilet),
            T("Noor Halcyon", "Gold dealer. Weighs everything twice.", 0, "#F2CC8F", HatStyle.None, 1.05f, 2, ProofKind.Ring, ActivityType.Office, ActivityType.Drink, ActivityType.Dining, ActivityType.Phone),
            T("Karim 'Kite' Saleh", "Rooftop courier. Hard to catch, easy to spot.", 0, "#81B29A", HatStyle.Cap, 1.0f, 1, ProofKind.Dossier, ActivityType.Lookout, ActivityType.Smoke, ActivityType.Drink, ActivityType.Toilet),
            T("Madame Ysolde", "Carpet seller and information broker.", 0, "#9A348E", HatStyle.Beret, 1.05f, 2, ProofKind.Keycard, ActivityType.Speech, ActivityType.Dining, ActivityType.Drink, ActivityType.Phone),
            T("Inspector Rahal", "Customs inspector with very full pockets.", 0, "#3D5A80", HatStyle.Cap, 1.2f, 2, ProofKind.Badge, ActivityType.Inspect, ActivityType.Office, ActivityType.Smoke, ActivityType.Drink),
            T("Captain Brine", "Runs the dock crews and the night boats.", 1, "#264653", HatStyle.Cap, 1.25f, 3, ProofKind.Dossier, ActivityType.Inspect, ActivityType.Lookout, ActivityType.Drink, ActivityType.Smoke),
            T("Ziva Rook", "Forger. Her studio hides in a container.", 1, "#E76F51", HatStyle.Beanie, 1.0f, 2, ProofKind.Briefcase, ActivityType.Office, ActivityType.Drink, ActivityType.Phone, ActivityType.Toilet),
            T("Old Man Tarek", "Owns the cargo ship. And the harbour master.", 1, "#B08968", HatStyle.Turban, 1.1f, 3, ProofKind.Ledger, ActivityType.Dining, ActivityType.Drink, ActivityType.Speech, ActivityType.Toilet),
            T("Dex Marlowe", "Arms broker. Tests every crate personally.", 1, "#6D6875", HatStyle.Fedora, 1.15f, 2, ProofKind.Dossier, ActivityType.Inspect, ActivityType.Workout, ActivityType.Smoke, ActivityType.Drink),
            T("The Twins' Mother", "Matriarch of the docks. Feared and fed.", 1, "#FFB4A2", HatStyle.None, 1.2f, 3, ProofKind.Ring, ActivityType.Dining, ActivityType.Speech, ActivityType.Drink, ActivityType.Toilet),
            T("Sultana Varela", "Hotel owner. The penthouse is her throne.", 2, "#D4A373", HatStyle.Crown, 1.1f, 3, ProofKind.Ring, ActivityType.Speech, ActivityType.Drink, ActivityType.Dining, ActivityType.Toilet, ActivityType.Garden),
            T("Hugo Spade", "Crypto prince. Lives at the pool bar.", 2, "#00B4D8", HatStyle.Cap, 1.05f, 2, ProofKind.Keycard, ActivityType.Drink, ActivityType.Workout, ActivityType.Phone, ActivityType.Toilet),
            T("General Okafor", "Retired, rich, and planning a coup from suite 40.", 2, "#2A9D8F", HatStyle.Beret, 1.25f, 3, ProofKind.Dossier, ActivityType.Office, ActivityType.Phone, ActivityType.Drink, ActivityType.Smoke),
            T("Celeste Moreau", "Star architect of the Zenith. Knows every vent.", 2, "#F4A261", HatStyle.Beret, 1.0f, 2, ProofKind.Briefcase, ActivityType.Inspect, ActivityType.Office, ActivityType.Drink, ActivityType.Toilet),
            T("Mr. Velvet", "The hotel's head of security. Smiles at cameras.", 2, "#5E548E", HatStyle.Fedora, 1.2f, 2, ProofKind.Badge, ActivityType.Lookout, ActivityType.Inspect, ActivityType.Drink, ActivityType.Smoke)
        };

        private static List<TargetProfile> SuburbTargets() => new List<TargetProfile>
        {
            T("Chet Malloy", "Car dealer. Every sale is a scam.", 0, "#E63946", HatStyle.Cowboy, 1.15f, 2, ProofKind.Keycard, ActivityType.Phone, ActivityType.Drink, ActivityType.Smoke, ActivityType.Toilet),
            T("Darlene Pike", "Owns the diner, the mall and the councilman.", 0, "#F4A6A6", HatStyle.None, 1.1f, 2, ProofKind.Ring, ActivityType.Dining, ActivityType.Drink, ActivityType.Office, ActivityType.Phone),
            T("Chief Halvorsen", "Police chief with a second payroll.", 0, "#1D3557", HatStyle.Cap, 1.2f, 3, ProofKind.Badge, ActivityType.Office, ActivityType.Lookout, ActivityType.Drink, ActivityType.Smoke),
            T("Mickey Glass", "Projectionist. Films more than movies.", 0, "#A8DADC", HatStyle.Beanie, 1.0f, 1, ProofKind.Briefcase, ActivityType.Inspect, ActivityType.Smoke, ActivityType.Drink, ActivityType.Toilet),
            T("Reverend Doyle", "Weekend sermons, weeknight deals.", 0, "#6C584C", HatStyle.TopHat, 1.1f, 2, ProofKind.Dossier, ActivityType.Speech, ActivityType.Phone, ActivityType.Drink, ActivityType.Toilet),
            T("Big Ronnie", "Bikers' president. The bowling alley is his court.", 1, "#3A3A3A", HatStyle.Beanie, 1.3f, 3, ProofKind.Dossier, ActivityType.Workout, ActivityType.Drink, ActivityType.Smoke, ActivityType.Toilet),
            T("Jolene 'Wrench' Hart", "Chop-shop genius. Sleeps under cars.", 1, "#E9C46A", HatStyle.Cap, 1.05f, 2, ProofKind.Keycard, ActivityType.Inspect, ActivityType.Drink, ActivityType.Smoke, ActivityType.Phone),
            T("Slick Tobias", "Fences everything the bikers steal.", 1, "#8E7DBE", HatStyle.Fedora, 1.0f, 2, ProofKind.Ledger, ActivityType.Office, ActivityType.Phone, ActivityType.Drink, ActivityType.Toilet),
            T("Granny Voss", "Bakes pies. The pies are not the business.", 1, "#F1FAEE", HatStyle.Beanie, 0.95f, 2, ProofKind.Ring, ActivityType.Dining, ActivityType.Garden, ActivityType.Drink, ActivityType.Toilet),
            T("Butch Calloway", "Enforcer. Practises his swing at the lanes.", 1, "#BC4749", HatStyle.Cap, 1.3f, 2, ProofKind.Dossier, ActivityType.Workout, ActivityType.Lookout, ActivityType.Drink, ActivityType.Smoke),
            T("Senator Whitlock", "Hosts the garden party. Sells the town.", 2, "#F8F9FA", HatStyle.TopHat, 1.15f, 3, ProofKind.Ledger, ActivityType.Speech, ActivityType.Garden, ActivityType.Drink, ActivityType.Toilet, ActivityType.Dining),
            T("Vivian Whitlock", "The senator's wife. The real senator.", 2, "#9D4EDD", HatStyle.Beret, 1.05f, 2, ProofKind.Ring, ActivityType.Garden, ActivityType.Drink, ActivityType.Phone, ActivityType.Toilet),
            T("Preston Whitlock III", "Golden boy. Throws pool parties nightly.", 2, "#FFD166", HatStyle.Cap, 1.1f, 2, ProofKind.Dossier, ActivityType.Drink, ActivityType.Workout, ActivityType.Smoke, ActivityType.Toilet),
            T("Agnes the Butler", "Has served three families. Buried two.", 2, "#495057", HatStyle.None, 1.0f, 1, ProofKind.Keycard, ActivityType.Inspect, ActivityType.Dining, ActivityType.Drink, ActivityType.Toilet),
            T("Dr. Oswin Hale", "Family lawyer. Keeps the will in his case.", 2, "#2B2D42", HatStyle.Fedora, 1.05f, 2, ProofKind.Briefcase, ActivityType.Office, ActivityType.Phone, ActivityType.Drink, ActivityType.Garden)
        };
    }
}
