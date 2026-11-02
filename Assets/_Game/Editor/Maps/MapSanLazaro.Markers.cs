using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapSanLazaro
    {
        private Vector3 CantinaCellarDoor => _cantinaR.At(16f, -1, 4f, 0f);
        private Vector3 WineCellarDoor => C(0f, -1, 28f, 0f);

        private void Caves()
        {
            Vector3 lCantina = CantinaCellarDoor + new Vector3(0, 0, 0.6f);
            Vector3 lQuarryWest = new Vector3(QUARRY.x - 14.5f, 7f, QUARRY.y);
            Vector3 lQuarryNorth = new Vector3(QUARRY.x, 7f, QUARRY.y + 14.5f);
            Vector3 lQuarryEast = new Vector3(QUARRY.x + 14.5f, 7f, QUARRY.y);
            Vector3 lWine = WineCellarDoor + new Vector3(-0.6f, 0, 0);
            Cave("Branch1_Cantina", 4.2f, 3.2f, lCantina, lCantina + new Vector3(0, -2.6f, 7f), new Vector3(-50f, -10f, -112f), new Vector3(-20f, -14f, -92f), new Vector3(20f, -12f, -82f), new Vector3(60f, -6f, -80f), new Vector3(100f, 3f, -77f), lQuarryWest);
            Cave("Branch1_Casa", 4.2f, 3.2f, lQuarryNorth, new Vector3(125f, 10f, -40f), new Vector3(120f, 16f, 0f), new Vector3(110f, 24f, 40f), new Vector3(95f, 33f, 70f), new Vector3(80f, 40f, 100f), new Vector3(81f, lWine.y, 118f), new Vector3(83f, lWine.y, 124f), lWine);
            Cave("Branch2_Waterfall", 4.6f, 3.4f, lQuarryEast, new Vector3(160f, 4f, -76f), new Vector3(176f, 0f, -75f), new Vector3(184f, -2.6f, -76f));
            float lChapel = K.GroundAt(-156f, 63f), lStrip = K.GroundAt(-160f, 117f);
            Cave("Culvert", 1.6f, 1.3f, new Vector3(-156f, lChapel - 0.2f, 63f), new Vector3(-158f, Mathf.Min(lChapel, lStrip) - 3f, 90f), new Vector3(-160f, lStrip - 0.2f, 117f));
            Poi(lQuarryWest + Vector3.up, "Cave (cantina)", MapPoi.Kind.Underground);
            Poi(lQuarryNorth + Vector3.up, "Cave (casa)", MapPoi.Kind.Underground);
            Poi(lQuarryEast + Vector3.up, "Cave (waterfall)", MapPoi.Kind.Underground);
            Poi(new Vector3(184f, -1.5f, -76f), "Cave dock", MapPoi.Kind.Underground);
            Poi(new Vector3(-156f, lChapel + 1f, 63f), "Culvert", MapPoi.Kind.Underground);
            Poi(new Vector3(-160f, lStrip + 1f, 117f), "Culvert", MapPoi.Kind.Underground);
            // The cave dock: a boat, crates, the smugglers' lamp.
            A.Model("Prop_Boat", new Vector3(194f, WATER_Y - 0.3f, -76f), 180f, null, ArchLib.Collide.Box);
            A.CrateStack(new Vector3(187f, -2.6f, -82f), 30f, 4);
            A.Crate(new Vector3(162f, 3.9f, -75f), 10f);
            A.Lamp(new Vector3(186f, -2.6f, -80f), 0f, new Color(1f, 0.65f, 0.35f), 10f, 1.6f, false, 0f);
        }

        private void Matches()
        {
            Zones();
            NpcsPueblo();
            NpcsCafetal();
            NpcsHacienda();
            Activities();
            Points();
            Chatter();
        }

        private void Zones()
        {
            K.Zone(ZONE_PUEBLO, "Pueblo", G(-10f, -150f), new Vector3(240f, 60f, 110f), G(140f, -118f), new Color(0.75f, 0.25f, 0.2f), HatStyle.Cap, _policeR.Center("Desk"));
            K.Zone(ZONE_CAFETAL, "Cafetal & Airstrip", G(-20f, 40f), new Vector3(400f, 90f, 260f), G(-62f, 142f), new Color(0.35f, 0.45f, 0.25f), HatStyle.Cowboy, _hangarBR.Center("Bunks"));
            K.Zone(ZONE_HACIENDA, "Hacienda La Cumbre", G(110f, 106f), new Vector3(110f, 60f, 90f), G(75f, 66f), new Color(0.9f, 0.88f, 0.82f), HatStyle.Fedora, C(20f, -1, 14f));
            // Outdoor tags (the rooms tag themselves).
            A.Zone(G(-20f, -152f, -2f), new Vector3(220f, 40f, 95f), 0f, SecurityTier.Public, AccessLevel.None, "Pueblo");
            A.Zone(new Vector3(-30f, -8f, -205f), new Vector3(70f, 14f, 24f), 0f, SecurityTier.Public, AccessLevel.None, "Dock");
            A.Zone(G(110f, 82f, -2f), new Vector3(96f, 20f, 26f), 0f, SecurityTier.Restricted, AccessLevel.Guest, "Forecourt");
            A.Zone(G(143f, 83f, -2f), new Vector3(30f, 20f, 26f), 0f, SecurityTier.Restricted, AccessLevel.None, "Orchard");
            A.Zone(G(108f, 135f, -2f), new Vector3(60f, 20f, 14f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "ServiceYard");
            A.Zone(G(145f, 118f, -2f), new Vector3(26f, 20f, 40f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "StableYard");
            A.Zone(G(75f, 130f, -2f), new Vector3(26f, 20f, 24f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "KitchenGarden");
            A.Zone(G(75f, 107f, -2f), new Vector3(26f, 20f, 22f), 0f, SecurityTier.Restricted, AccessLevel.None, "ChapelGarden");
            A.Zone(G(40f, 0f, -2f), new Vector3(230f, 60f, 140f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "Cafetal");
            A.Zone(G(-125f, 145f, -2f), new Vector3(190f, 40f, 70f), 0f, SecurityTier.Hostile, AccessLevel.Security, "Airstrip");
            A.Zone(G(-20f, 113f, -2f), new Vector3(24f, 20f, 18f), 0f, SecurityTier.Restricted, AccessLevel.Security, "RidgeCheckpoint");
            A.Zone(G(_casaR.At(35f, 6, 28f, 0f).x, _casaR.At(35f, 6, 28f, 0f).z, 0f) + Vector3.up * 21f, new Vector3(10f, 4f, 8f), 0f, SecurityTier.Hostile, AccessLevel.Security, "Mirador");
        }

        private void Guard(Vector3 p, float pYaw, int pZone, ItemKind pWeapon = ItemKind.Smg, ItemKind pCarries = ItemKind.Fists, bool pEnforcer = false) => A.Npc(NpcRole.Guard, p, pYaw, pZone, pWeapon, pCarries, pEnforcer);
        private void Civ(Vector3 p, float pYaw, int pZone, ItemKind pCarries = ItemKind.Fists, bool pEnforcer = false) => A.Npc(NpcRole.Worker, p, pYaw, pZone, ItemKind.Fists, pCarries, pEnforcer);

        private void NpcsPueblo()
        {
            const int Z = ZONE_PUEBLO;
            // Fiesta crowd (26) facing the stage.
            K.Crowd(G(-22f, -146f), 3.5f, 13f, 26, Z);
            // Sellers at the stalls, the barkeep, the boatman.
            for (int i = 0; i < 3; i++) Civ(G(4f + i * 16f, i % 2 == 0 ? -133.4f : -142.6f), i % 2 == 0 ? 180f : 0f, Z);
            Civ(new Vector3(-30f, -2.8f, -203f), 270f, Z);
            Civ(_cantinaR.InRoom("Bar", 0.5f, 0.9f), _cantina.yaw + 180f, Z);
            Civ(new Vector3(-16f, -2.8f, -214f), 90f, Z);
            // Priest in the nave, sacristan in the sacristy (wardrobe close by).
            Civ(_churchR.InRoom("Nave", 0.5f, 0.8f), 180f, Z);
            Civ(_churchR.InRoom("Sacristy", 0.5f, 0.5f), 90f, Z);
            A.Wardrobe(_churchR.At(0.6f, 0, 12f, 0f), 90f, _churchR.interior);
            // The police pair at their door.
            Guard(_policeR.At(5f, 0, -1.5f), 180f, Z, ItemKind.Pistol);
            Guard(_policeR.At(7f, 0, -1.5f), 180f, Z, ItemKind.Shotgun);
            // The sponsor's secretary (carries an invitation) smokes in the cantina's back alley.
            Civ(G(-71f, -141f), 0f, Z, ItemKind.Invitation);
            K.Activity(ActivityType.Smoke, G(-71f, -141.5f), 0f, Z);
            // The CodeNote in the accountant's missal, in the church sacristy.
            A.Gadget(ItemKind.CodeNote, _churchR.At(3f, 0, 22f, 0.95f), "Missal (vault code)");
            A.Model("Furn_Desk", _churchR.At(3f, 0, 22f, 0f), 0f, _churchR.interior);
            A.Gadget(ItemKind.EmeticPoison, _cantinaR.At(3f, 0, 7.2f, 1.0f), "Tobacco infusion");
            A.Gadget(ItemKind.StaffPass, _cantinaR.At(2f, 0, 12f, 0.95f), "Waiter's pass");
        }

        private void NpcsCafetal()
        {
            const int Z = ZONE_CAFETAL;
            // Pickers (10) on the terraces, in pairs along the rows.
            for (int i = 0; i < 5; i++)
            {
                float x = -20f + (i * 37f) % 140f;
                Civ(G(x, TERRACES[i].x + 6f), (i * 70f) % 360f, Z);
                Civ(G(x + 2.5f, TERRACES[i].x + 8.5f), (i * 70f + 140f) % 360f, Z);
            }
            // The capataz (enforcer, staff pass) at his hut on terrace 5; a crate 5 m away.
            Vector3 lCapataz = _huts[7].result.At(2.5f, 0, -1.5f);
            A.Npc(NpcRole.Guard, lCapataz, 180f, Z, ItemKind.Pistol, ItemKind.StaffPass, true);
            A.BigCrate(lCapataz + new Vector3(5f, -0.05f, 0f), 0f);
            // Roastmaster and hands (4) in the beneficio.
            Civ(_roaster + new Vector3(0, 0.05f, -2.2f), 0f, Z);
            Civ(_beneficioR.InRoom("CuppingRoom", 0.5f, 0.5f), _beneficio.yaw, Z);
            Civ(_beneficioR.InRoom("SackingLoft", 0.3f, 0.4f), _beneficio.yaw, Z);
            Civ(G(122f, 54f), 90f, Z);
            // Patrol pairs (4) on the terraces.
            A.Patrol(Z, 2, ItemKind.Smg, 4f, G(-30f, -38f), G(110f, -38f), G(110f, -12f), G(-30f, -12f));
            A.Patrol(Z, 2, ItemKind.Smg, 4f, G(-30f, 40f), G(140f, 40f), G(140f, 66f), G(-30f, 66f));
            // The water-tower lookout (sniper), smoking every few minutes below.
            A.Npc(NpcRole.Lookout, _waterTowerTop + new Vector3(0, 0.05f, 1f), 40f, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, _waterTowerTop + new Vector3(1.5f, 0.05f, -1f));

            // Airstrip and ridge (13): sicarios, Captain Duarte, pilots, mechanic, radio operator, checkpoint pair.
            A.Patrol(Z, 3, ItemKind.Smg, 5f, G(-195f, 138f), G(-50f, 138f), G(-50f, 166f), G(-195f, 166f));
            Guard(_hangarAR.At(10f, 0, -1.5f), 0f, Z, ItemKind.Smg);
            Guard(_hangarAR.At(14f, 0, -1.5f), 0f, Z, ItemKind.Shotgun);
            Guard(_hangarBR.At(8f, 0, -1.5f), 0f, Z, ItemKind.Smg);
            A.Npc(NpcRole.Guard, _hangarAR.InRoom("OfficeA", 0.5f, 0.5f), 0f, Z, ItemKind.Pistol, ItemKind.Keycard, true);
            Civ(_hangarBR.InRoom("Bunks", 0.4f, 0.5f), 90f, Z);
            Civ(G(-104f, 160f), 0f, Z);
            Civ(G(-100f, 158f), 200f, Z);
            Civ(_radioR.InRoom("Radio", 0.3f, 0.6f), 180f, Z, ItemKind.Fists);
            Guard(_radioR.At(5.5f, 0, -1.5f), 0f, Z, ItemKind.Smg);
            Guard(G(-22f, 109f), 200f, Z, ItemKind.Smg, ItemKind.Keycard);
            Guard(G(-17f, 116f), 20f, Z, ItemKind.Shotgun);
            // Smugglers (2) at the cave dock.
            Guard(new Vector3(186f, -2.55f, -78f), 90f, Z, ItemKind.Pistol);
            Guard(new Vector3(189f, -2.55f, -81f), 270f, Z, ItemKind.Shotgun);
        }

        private void NpcsHacienda()
        {
            const int Z = ZONE_HACIENDA;
            // The frisk pair at the main gate.
            Guard(G(73.5f, 72f), 180f, Z, ItemKind.Smg);
            Guard(G(76.5f, 72f), 180f, Z, ItemKind.Smg);
            // Grounds patrol pairs (6).
            A.Patrol(Z, 2, ItemKind.Smg, 3f, G(66f, 74f), G(154f, 74f), G(154f, 138f), G(66f, 138f));
            A.Patrol(Z, 2, ItemKind.Smg, 3f, G(130f, 96f), G(154f, 96f), G(154f, 128f), G(130f, 128f));
            A.Patrol(Z, 2, ItemKind.Shotgun, 6f, G(66f, 96f), G(86f, 96f), G(86f, 138f), G(66f, 138f));
            // Interior pairs: study door, cellar corridor, F1 gallery.
            Guard(C(24f, 1, 22.8f), 0f, Z, ItemKind.Smg);
            Guard(C(26.5f, 1, 22.8f), 0f, Z, ItemKind.Smg);
            Guard(C(14f, -1, 21f), 90f, Z, ItemKind.Smg);
            Guard(C(26f, -1, 21f), 270f, Z, ItemKind.Shotgun);
            A.Patrol(Z, 2, ItemKind.Smg, 4f, C(11.2f, 1, 7.2f), C(28.8f, 1, 7.2f), C(28.8f, 1, 22.8f), C(11.2f, 1, 22.8f));
            // Mirador lookout.
            A.Npc(NpcRole.Lookout, C(37f, 6, 30f), 200f, Z, ItemKind.Sniper, ItemKind.Fists, true);
            K.Weapon(ItemKind.Sniper, C(38f, 6, 25f, 0.05f));
            // Bodyguards (4): Ramiro's on the loggia, the buyer's by the salon.
            Guard(C(18f, 0, 2f), 180f, Z, ItemKind.Smg, ItemKind.Fists, true);
            Guard(C(22f, 0, 2f), 180f, Z, ItemKind.Smg, ItemKind.Fists, true);
            Guard(C(12f, 0, 23f), 180f, Z, ItemKind.Pistol, ItemKind.Fists, true);
            Guard(C(18f, 0, 23f), 180f, Z, ItemKind.Pistol, ItemKind.Fists, true);
            // Household (7): mayordomo, cook, maids, gardener, mechanic, operator.
            Civ(C(25f, 0, 26f), 0f, Z, ItemKind.StaffPass, true);
            Civ(C(36f, 0, 28f), 90f, Z, ItemKind.StaffPass);
            Civ(C(34f, -1, 29f), 180f, Z);
            Civ(C(4f, 1, 28.5f), 180f, Z);
            Civ(_shedR.At(2.5f, 0, -1.5f), 180f, Z);
            Civ(_garageR.InRoom("GarageBay", 0.3f, 0.7f), _garage.yaw, Z, ItemKind.StaffPass);
            Civ(G(117f, 134f), 180f, Z, ItemKind.Keycard);
            K.Activity(ActivityType.Smoke, G(117.5f, 133.5f), 180f, Z);
            // Reception (18): guests, waiters with trays, musicians in the patio.
            K.Crowd(C(20f, 0, 3f), 1.5f, 6f, 6, Z);
            K.Crowd(C(20f, 0, 15f), 2.5f, 5.5f, 6, Z);
            Civ(C(14f, 0, 4f), 90f, Z, ItemKind.StaffPass);
            Civ(C(26f, 0, 12f), 270f, Z, ItemKind.StaffPass);
            Civ(C(15f, 0, 27f), 0f, Z, ItemKind.StaffPass);
            for (int i = 0; i < 3; i++) Civ(C(14f + i * 1.2f, 0, 20f), 180f, Z);
        }

        private void Activities()
        {
            // Zone 0, the pueblo: the sponsor's stage, cantina, bell deck, dock; breaks and routines.
            K.Activity(ActivityType.Speech, G(-16f, -146f, 1.1f), -90f, ZONE_PUEBLO, 30f);
            K.Activity(ActivityType.Lookout, _churchR.At(3f, 6, 5.2f), 180f, ZONE_PUEBLO, 20f);
            K.Activity(ActivityType.Idle, new Vector3(-26f, -2.85f, -212f), 180f, ZONE_PUEBLO, 20f);
            K.Activity(ActivityType.Office, _policeR.InRoom("Desk", 0.5f, 0.5f), 180f, ZONE_PUEBLO);
            K.Activity(ActivityType.Phone, _policeR.At(9f, 0, -1.2f), 180f, ZONE_PUEBLO);
            K.Activity(ActivityType.Inspect, G(40f, -138f), 0f, ZONE_PUEBLO);
            K.Activity(ActivityType.Garden, G(-36f, -140f), 0f, ZONE_PUEBLO);
            K.Activity(ActivityType.Dining, G(-49f, -148f), 90f, ZONE_PUEBLO);
            K.Activity(ActivityType.Workout, G(10f, -186f), 0f, ZONE_PUEBLO);
            K.Activity(ActivityType.Smoke, G(-10f, -170f), 0f, ZONE_PUEBLO);
            K.Activity(ActivityType.Drink, _cantinaR.InRoom("Bar", 0.3f, 0.7f), _cantina.yaw, ZONE_PUEBLO);
            // Zone 1: hangar, checkpoint, roaster, ridge, terraces.
            K.Activity(ActivityType.Inspect, _hangarAR.At(12f, 0, 4f), 0f, ZONE_CAFETAL, 25f);
            K.Activity(ActivityType.Phone, G(-17f, 110f), 90f, ZONE_CAFETAL, 15f);
            K.Activity(ActivityType.Office, _beneficioR.InRoom("MillOffice", 0.5f, 0.5f), 0f, ZONE_CAFETAL);
            K.Activity(ActivityType.Office, _radioR.InRoom("Radio", 0.5f, 0.5f), 0f, ZONE_CAFETAL);
            K.Activity(ActivityType.Lookout, G(-40f, 128f), 0f, ZONE_CAFETAL);
            K.Activity(ActivityType.Dining, _hangarBR.InRoom("PilotsMess", 0.5f, 0.5f), 0f, ZONE_CAFETAL);
            K.Activity(ActivityType.Workout, G(-140f, 128f), 0f, ZONE_CAFETAL);
            K.Activity(ActivityType.Speech, G(50f, -1f), 180f, ZONE_CAFETAL);
            K.Activity(ActivityType.Idle, G(30f, 30f), 0f, ZONE_CAFETAL);
            foreach ((ArchSpec lSpec, ArchResult r) in _huts) K.Activity(ActivityType.Smoke, r.At(4.5f, 0, -1.2f), 180f, ZONE_CAFETAL);
            for (int t = 0; t < TERRACES.Length; t++) K.Activity(ActivityType.Garden, G(10f + t * 20f, TERRACES[t].x + 8f), 0f, ZONE_CAFETAL);
            A.Toilet(G(-26f, -46f), 0f, ZONE_CAFETAL);
            A.Toilet(_hangarBR.At(19f, 0, 16.3f, 0f), _hangarB.yaw + 180f, ZONE_CAFETAL);
            // Zone 2: the casa.
            K.Activity(ActivityType.Office, C(25f, 1, 26f), 0f, ZONE_HACIENDA, 25f);
            K.Activity(ActivityType.Office, C(20f, -2, 14f), 0f, ZONE_HACIENDA, 30f);
            K.Activity(ActivityType.Speech, C(20f, 0, 1.5f), 180f, ZONE_HACIENDA, 20f);
            K.Activity(ActivityType.Idle, C(5f, 0, 19f), 0f, ZONE_HACIENDA, 15f);
            K.Activity(ActivityType.Dining, C(25f, 0, 27f), 0f, ZONE_HACIENDA, 20f);
            K.Activity(ActivityType.Drink, C(34f, 0, 27f), 0f, ZONE_HACIENDA, 12f);
            K.Activity(ActivityType.Inspect, C(5f, -1, 28f), 270f, ZONE_HACIENDA, 15f);
            K.Activity(ActivityType.Phone, C(35f, 0, 6f), 90f, ZONE_HACIENDA);
            K.Activity(ActivityType.Garden, G(140f, 80f), 0f, ZONE_HACIENDA);
            K.Activity(ActivityType.Garden, G(76f, 128f), 0f, ZONE_HACIENDA);
            K.Activity(ActivityType.Lookout, C(36f, 6, 26f), 180f, ZONE_HACIENDA);
            K.Activity(ActivityType.Workout, G(145f, 118f), 0f, ZONE_HACIENDA);
            K.Activity(ActivityType.Smoke, G(68f, 80f), 0f, ZONE_HACIENDA);
            K.Activity(ActivityType.Smoke, C(26f, -1, 22f), 0f, ZONE_HACIENDA);
            K.Activity(ActivityType.Idle, C(20f, 1, 3f), 180f, ZONE_HACIENDA);
            A.Toilet(C(33f, -1, 15f, 0f), 180f, ZONE_HACIENDA);
        }

        private void Points()
        {
            // Starts: six edge starts, three inside ones with the pass of their tier.
            K.Point(MatchPoint.Kind.Start, G(-84f, -192f, 0.2f), 60f, "River dock");
            K.Point(MatchPoint.Kind.Start, G(130f, -124f, 0.2f), 250f, "Bus stop");
            K.Point(MatchPoint.Kind.Start, G(-125f, -72f, 0.2f), 60f, "Banana grove");
            K.Point(MatchPoint.Kind.Start, G(-162f, 45f, 0.2f), 30f, "Jungle chapel ruin");
            K.Point(MatchPoint.Kind.Start, G(QUARRY.x + 7f, QUARRY.y - 8f, 0.2f), 300f, "Waterfall cave mouth");
            K.Point(MatchPoint.Kind.Start, G(18f, 168f, 0.2f), 120f, "Ridge pine copse");
            K.Point(MatchPoint.Kind.Start, _huts[6].result.At(2.5f, 0, 2f, 0.2f), 0f, "Picker hut, terrace 4", ItemKind.StaffPass);
            K.Point(MatchPoint.Kind.Start, _cantinaR.InRoom("CantinaKitchen", 0.4f, 0.5f, 0.2f), _cantina.yaw, "Reception waiter", ItemKind.StaffPass);
            K.Point(MatchPoint.Kind.Start, G(-46f, -132f, 0.2f), 90f, "Sponsor's table guest", ItemKind.Invitation);
            // Extractions: boat, plane, car, waterfall.
            K.Point(MatchPoint.Kind.Extraction, new Vector3(-30f, -2.8f, -212f), 0f, "Boat");
            K.Point(MatchPoint.Kind.Extraction, G(-200f, 152f, 0.1f), 90f, "Plane");
            K.Point(MatchPoint.Kind.Extraction, G(140f, -118f, 0.1f), 0f, "Car");
            K.Point(MatchPoint.Kind.Extraction, new Vector3(188f, -2.5f, -74f), 0f, "Waterfall");

            // Weapons in guard rooms, posts and caches.
            ItemKind[] lGuns = { ItemKind.Pistol, ItemKind.Smg, ItemKind.Shotgun, ItemKind.SilencedPistol };
            int g = 0;
            void Gun(Vector3 p) => K.Weapon(lGuns[g++ % lGuns.Length], p);
            Gun(_policeR.InRoom("Armoury", 0.3f, 0.5f, 0.9f)); Gun(_policeR.InRoom("Armoury", 0.7f, 0.5f, 0.9f)); Gun(_policeR.InRoom("Desk", 0.2f, 0.5f, 0.85f));
            Gun(_cantinaR.InRoom("PatronOffice", 0.5f, 0.5f, 0.85f)); Gun(_cantinaR.InRoom("CantinaCellar", 0.8f, 0.8f, 0.1f));
            Gun(_churchR.InRoom("Sacristy", 0.5f, 0.2f, 0.1f));
            foreach (ArchResult h in _houses) if (!h.spec.shut && g < 16) Gun(h.InRoom("Front", 0.2f, 0.8f, 0.1f));
            Gun(_beneficioR.InRoom("MillOffice", 0.5f, 0.5f, 0.85f)); Gun(_beneficioR.InRoom("SackingLoft", 0.8f, 0.5f, 0.1f));
            foreach ((ArchSpec lSpec, ArchResult r) in _huts) Gun(r.At(3.8f, 0, 2.5f, 0.1f));
            Gun(_hangarAR.InRoom("WorkshopA", 0.5f, 0.5f, 0.9f)); Gun(_hangarAR.InRoom("OfficeA", 0.5f, 0.5f, 0.85f)); Gun(_hangarAR.InRoom("CatwalkA", 0.5f, 0.5f, 0.1f));
            Gun(_hangarBR.InRoom("Bunks", 0.5f, 0.5f, 0.1f)); Gun(_hangarBR.InRoom("PilotsMess", 0.5f, 0.5f, 0.85f)); Gun(_radioR.InRoom("Radio", 0.5f, 0.5f, 0.85f));
            Gun(G(-20f, 116f, 0.1f)); Gun(G(-66f, 128f, 0.1f)); Gun(G(QUARRY.x + 2f, QUARRY.y - 2f, 0.1f));
            Gun(C(35f, 1, 28f, 0.1f)); Gun(C(35f, 1, 30f, 0.1f)); Gun(C(20f, -1, 14f, 0.1f)); Gun(C(15f, -1, 12f, 0.1f));
            Gun(C(35f, 2, 28f, 0.1f)); Gun(C(35f, 5, 28f, 0.1f)); Gun(C(35f, 0, 5f, 0.85f)); Gun(_garageR.InRoom("GarageBay", 0.8f, 0.8f, 0.1f));
            Gun(C(5f, 2, 28f, 0.1f)); Gun(C(25f, 2, 28f, 0.1f)); Gun(C(15f, 2, 28f, 0.1f));
            K.Weapon(ItemKind.Sniper, _churchR.At(4.5f, 6, 6f, 0.05f));
            K.Weapon(ItemKind.Sniper, _hangarBR.At(18f, 0, 2f, 0.1f));
            // Unique weapons: three spots each, one drawn per match.
            K.UniqueWeapon(ItemKind.RocketLauncher, _hangarAR.At(2f, 0, 11f, 0.1f), "Hangar A crates");
            K.UniqueWeapon(ItemKind.RocketLauncher, C(36f, -1, 31f, 0.1f), "Laundry");
            K.UniqueWeapon(ItemKind.RocketLauncher, new Vector3(188f, -2.5f, -84f), "Cave dock");
            K.UniqueWeapon(ItemKind.Flamethrower, _beneficioR.At(2f, 0, 13f, 0.1f), "Roaster floor");
            K.UniqueWeapon(ItemKind.Flamethrower, _policeR.InRoom("Cells", 0.5f, 0.5f, 0.1f), "Police cells");
            K.UniqueWeapon(ItemKind.Flamethrower, G(QUARRY.x - 6f, QUARRY.y - 4f, 0.1f), "Quarry");
            // Smoke grenades, C4 and coins around the edges.
            K.Gadget(ItemKind.SmokeGrenade, G(-120f, -64f, 0.1f));
            K.Gadget(ItemKind.SmokeGrenade, G(-150f, 50f, 0.1f));
            K.Gadget(ItemKind.C4, _hangarAR.At(22f, 0, 16f, 0.9f));
            K.Gadget(ItemKind.C4, G(QUARRY.x + 5f, QUARRY.y + 5f, 0.1f));
            // Outdoor throwables along the routes (rocks on the paths, bottles by the stalls).
            Vector2[] lLoose = { new Vector2(15, -100), new Vector2(15, -40), new Vector2(95, -20), new Vector2(40, 30), new Vector2(-30, 60), new Vector2(-30, 100), new Vector2(-60, 120), new Vector2(60, 150), new Vector2(150, 64), new Vector2(-110, -60), new Vector2(-150, 60), new Vector2(-120, 170), new Vector2(-40, -175), new Vector2(60, -150), new Vector2(120, -110), new Vector2(170, -76) };
            foreach (Vector2 p in lLoose) A.Throwable(G(p, 0.1f), ItemKind.Rock);
            for (int i = 0; i < 10; i++) A.Throwable(G(-40f + i * 12f, i % 2 == 0 ? -134.5f : -141.5f, 0.1f), ItemKind.Bottle);
            for (int i = 0; i < 8; i++) A.Throwable(C(4f + i * 4.5f, 0, i % 2 == 0 ? 2f : 30f, 0.1f));
        }

        private void Chatter()
        {
            A.Chatter(C(34f, -1, 28f), "Laundry maids", "The old man keeps the vault code in his missal. The one at the church.", "Sacristy, second shelf. Don't tell Teodoro I said so.");
            A.Chatter(_roaster + new Vector3(0, 0.5f, -2f), "Roastmaster", "That valve sticks again. One more turn and the whole roaster goes up.", "The buyer tastes right at the cooling tray. Keep her away from the burner.");
            A.Chatter(C(20f, -1, 20f), "Corridor guards", "Never touch the CO2 panel while Aranda counts in the vault.", "The purge vents straight into the vault. Nobody gets out in time.");
            A.Chatter(C(35f, 0, 28f), "Cook", "The operator smokes by the dumpster every few minutes. Keycard on his belt.", "Datura grows at the old chapel in the jungle. The maids swear by it.");
            A.Chatter(_hangarBR.InRoom("PilotsMess", 0.5f, 0.5f), "Pilots", "The bowser by the DC-3 leaks like a sieve. One spark and it's over.", "Duarte keeps a keycard for the casa's security room.");
            A.Chatter(C(15f, 0, 2f), "Bodyguards", "Don Ramiro prays alone in the chapel. Candles everywhere, lamp oil by the door.", "The toast is on the loggia. Fireworks crate right behind him.");
            A.Chatter(_churchR.InRoom("Nave", 0.5f, 0.6f), "Priest", "Doña Celestina climbs the bell tower to watch the fiesta. Every year.", "The bell deck is the best seat in town.");
            A.Chatter(_huts[7].result.At(2.5f, 0, -2f), "Capataz", "The coffee chute drops right into the stable yard. No way back up.", "Water tower boys can see the whole south loggia.");
            A.Chatter(_cantinaR.InRoom("Bar", 0.5f, 0.5f), "Barkeep", "The patrón's cellar runs into the old caves. Smugglers use them.", "Celestina drinks here after the stage. Same glass every year.");
        }
    }
}
