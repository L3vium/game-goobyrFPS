using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapQasrAlRimal
    {
        private void Guard(Vector3 p, float pYaw, int pZone, ItemKind pWeapon = ItemKind.Smg, ItemKind pCarries = ItemKind.Fists, bool pEnforcer = false) => A.Npc(NpcRole.Guard, p, pYaw, pZone, pWeapon, pCarries, pEnforcer);
        private void Civ(Vector3 p, float pYaw, int pZone, ItemKind pCarries = ItemKind.Fists, bool pEnforcer = false) => A.Npc(NpcRole.Worker, p, pYaw, pZone, ItemKind.Fists, pCarries, pEnforcer);

        private void Matches()
        {
            // Zones.
            K.Zone(ZONE_TOWN, "Ain Zarqa", G(0f, -150f), new Vector3(280f, 60f, 110f), G(-230f, -125f), new Color(0.2f, 0.35f, 0.7f), HatStyle.Turban, _busR.Center("Ticket"));
            K.Zone(ZONE_GROVE, "Grove, Wadi & Radar", G(40f, 0f), new Vector3(420f, 100f, 260f), G(150f, 74f), new Color(0.6f, 0.5f, 0.3f), HatStyle.Cap, _bunkerR.Center("BunkerBunks"));
            K.Zone(ZONE_CITADEL, "The Citadel", G(-7f, 116f), new Vector3(140f, 60f, 120f), G(-4f, 56f), new Color(0.9f, 0.85f, 0.75f), HatStyle.Beret, Kp(17f, -1, 5f));
            A.Zone(G(0f, -150f, -2f), new Vector3(280f, 40f, 100f), 0f, SecurityTier.Public, AccessLevel.None, "Town");
            A.Zone(G(0f, -62f, -4f), new Vector3(320f, 40f, 80f), 0f, SecurityTier.Public, AccessLevel.None, "Grove");
            A.Zone(G(0f, -8f, -8f), new Vector3(500f, 40f, 32f), 0f, SecurityTier.Public, AccessLevel.None, "Wadi");
            A.Zone(G(0f, 30f, -6f), new Vector3(120f, 60f, 56f), 0f, SecurityTier.Public, AccessLevel.None, "Hairpins");
            A.Zone(G(_press.origin.x + 9f, _press.origin.z + 6f, -2f), new Vector3(36f, 20f, 30f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "PressYard");
            A.Zone(G(KASBAH, -2f), new Vector3(26f, 20f, 24f), 0f, SecurityTier.Restricted, AccessLevel.None, "Kasbah");
            A.Zone(G(-7f, 77f, -2f), new Vector3(120f, 20f, 34f), 0f, SecurityTier.Restricted, AccessLevel.Guest, "FirstCourt");
            A.Zone(G(36f, 118f, -2f), new Vector3(42f, 20f, 46f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "SecondCourt");
            A.Zone(G(-48f, 118f, -2f), new Vector3(46f, 20f, 46f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "ThirdCourt");
            A.Zone(G(-7f, 155f, -2f), new Vector3(120f, 20f, 34f), 0f, SecurityTier.Restricted, AccessLevel.None, "NorthTerrace");
            A.Zone(G(144f, 127f, -2f), new Vector3(96f, 30f, 98f), 0f, SecurityTier.Hostile, AccessLevel.Security, "Radar");
            A.Zone(G(-96f, 104f, -2f), new Vector3(22f, 20f, 20f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "BaseStation");

            // ---- town (50)
            const int T = ZONE_TOWN;
            K.Crowd(G(PLAZA, 0.05f), 4f, 15f, 16, T);
            K.Crowd(G(2f, -120f), 1f, 2.2f, 6, T);
            K.Crowd(G(40f, -120f), 1f, 2.2f, 6, T);
            for (int i = 0; i < 3; i++) { Civ(G(0.4f, -128f + i * 8f), 90f, T); Civ(G(41.6f, -124f + i * 8f), -90f, T); }
            for (int i = 0; i < 8; i++) Civ(_teaR.InRoom("TeaRoom", 0.2f + (i % 4) * 0.2f, i < 4 ? 0.3f : 0.7f), _teaHouse.yaw + 180f, T, i % 2 == 0 ? ItemKind.Invitation : ItemKind.Fists);
            for (int i = 0; i < 5; i++) Civ(_seraiR.At(10f + i * 2.5f, 0, 12f + (i % 2) * 4f), i * 70f, T);
            Guard(G(-26f, -148f), 90f, T, ItemKind.Pistol);
            Guard(G(-26f, -152f), 90f, T, ItemKind.Shotgun);
            Civ(_busR.Center("Ticket"), _busStation.yaw, T, ItemKind.Invitation);
            Civ(G(120f, -180f), 90f, T);
            A.Toilet(_teaR.InRoom("TeaBackRoom", 0.2f, 0.8f, 0f), _teaHouse.yaw, T);

            // ---- grove, wadi, radar
            const int G1 = ZONE_GROVE;
            for (int i = 0; i < 2; i++) { Civ(G(-80f + i * 100f, -66f), i * 90f, G1); Civ(G(-77f + i * 100f, -63f), i * 90f + 160f, G1); }
            Civ(_pressR.InRoom("PressOffice", 0.5f, 0.8f), _press.yaw, G1, ItemKind.StaffPass);
            for (int i = 0; i < 3; i++) Civ(_screwPress + new Vector3(-2f + i * 2f, 0.05f, 2.4f), 180f, G1);
            Civ(_baseR.At(6f, 0, -1.5f), 180f, G1, ItemKind.Keycard);
            Civ(_baseR.At(-3f, 0, 4f), 90f, G1);
            K.Activity(ActivityType.Smoke, _baseR.At(6.5f, 0, -2f), 180f, G1);
            A.Patrol(G1, 2, ItemKind.Smg, 4f, G(110f, 92f), G(186f, 92f), G(186f, 168f), G(110f, 168f));
            Guard(_bunkerR.At(8f, 0, -1.5f), 180f, G1, ItemKind.Smg);
            Guard(_bunkerR.At(10f, 0, -1.5f), 180f, G1, ItemKind.Shotgun);
            Civ(_bunkerR.InRoom("Consoles", 0.3f, 0.5f), 0f, G1);
            Civ(G(120f, 102f), 0f, G1);
            A.Npc(NpcRole.Guard, _bunkerR.InRoom("BunkerHall", 0.8f, 0.5f), 0f, G1, ItemKind.Pistol, ItemKind.Keycard, true);
            A.Npc(NpcRole.Lookout, _dish + Vector3.up * 13.3f, 200f, G1, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, _dish + new Vector3(1f, 13.3f, -1f));

            // ---- citadel garrison and civilians
            const int C = ZONE_CITADEL;
            Guard(G(-6f, CITADEL.yMin + 2f), 180f, C);
            Guard(G(-2f, CITADEL.yMin + 2f), 180f, C);
            A.Patrol(C, 2, ItemKind.Smg, 4f, G(CITADEL.xMin + 3f, CITADEL.yMin + 4f), G(CITADEL.xMin + 3f, CITADEL.yMax - 4f), G(-30f, CITADEL.yMax - 4f));
            A.Patrol(C, 2, ItemKind.Smg, 4f, G(CITADEL.xMax - 3f, CITADEL.yMin + 4f), G(CITADEL.xMax - 3f, CITADEL.yMax - 4f), G(20f, CITADEL.yMax - 4f));
            A.Patrol(C, 2, ItemKind.Shotgun, 6f, G(-40f, 64f), G(40f, 64f), G(40f, 90f), G(-40f, 90f));
            A.Patrol(C, 4, ItemKind.Smg, 8f, G(22f, 110f), G(50f, 110f), G(50f, 134f), G(22f, 134f));
            Guard(Kp(13f, 1, 9.8f), 0f, C); Guard(Kp(13f, 2, 9.8f), 0f, C, ItemKind.Shotgun);
            Guard(Kp(21.5f, 2, 26f), 90f, C); Guard(Kp(21.5f, 2, 29f), 90f, C);
            Guard(Kp(5f, 3, 18f), 0f, C); Guard(Kp(7f, 3, 18f), 0f, C, ItemKind.Shotgun);
            Guard(Kp(22f, -1, 26f), 90f, C); Guard(Kp(22f, -1, 30f), 90f, C, ItemKind.Shotgun);
            Guard(Kp(28f, 0, 16f), 0f, C); Guard(Kp(30f, 0, 19f), 270f, C);
            A.Npc(NpcRole.Lookout, _lookoutRoof, 180f, C, ItemKind.Sniper, ItemKind.Fists, true);
            K.Weapon(ItemKind.Sniper, _lookoutRoof + new Vector3(1.5f, 0f, 0f));
            for (int i = 0; i < 4; i++) Guard(Kp(14f + i * 2f, 1, 7f), 180f, C, ItemKind.Smg, ItemKind.Fists, true);
            K.Crowd(G(-7f, 80f, 0.05f), 2f, 8f, 10, C);
            Civ(G(40f, 104f), 0f, C); Civ(G(46f, 108f), 0f, C);
            Civ(Kp(5f, 0, 28f), 90f, C, ItemKind.StaffPass); Civ(Kp(4f, 0, 31f), 90f, C); Civ(Kp(8f, 0, 31f), 270f, C);
            Civ(Kp(28f, 0, 5f), 0f, C, ItemKind.StaffPass); Civ(Kp(17f, 3, 29f), 0f, C, ItemKind.StaffPass);
            Civ(Kp(28f, 1, 29f), 0f, C);
            Civ(Kp(10f, -1, 5f), 180f, C, ItemKind.Keycard);
            Civ(Kp(17f, 1, 5f), 0f, C, ItemKind.StaffPass, true);
            Civ(Kp(5f, 1, 26f), 90f, C); Civ(Kp(6f, 1, 30f), 270f, C);
            K.Activity(ActivityType.Smoke, G(-62f, 122f), 0f, C);

            Activities();
            Points();
            Chatter();
        }

        private void Activities()
        {
            const int T = ZONE_TOWN, G1 = ZONE_GROVE, C = ZONE_CITADEL;
            K.Activity(ActivityType.Speech, _teaR.InRoom("TeaRoom", 0.5f, 0.6f), _teaHouse.yaw, T, 25f);
            K.Activity(ActivityType.Inspect, _busR.At(5f, 0, -2f), _busStation.yaw, T, 20f);
            K.Activity(ActivityType.Phone, _seraiR.At(26f, 0, 15f), -90f, T);
            K.Activity(ActivityType.Office, _busR.Center("Ticket"), 0f, T);
            K.Activity(ActivityType.Dining, _seraiR.At(28f, 0, 10f), 0f, T);
            K.Activity(ActivityType.Workout, _seraiR.At(15f, 0, 10f), 0f, T);
            K.Activity(ActivityType.Garden, G(PLAZA + new Vector2(10, 10)), 0f, T);
            K.Activity(ActivityType.Lookout, _mosqueR.At(19f, 6, 3f), 180f, T);
            K.Activity(ActivityType.Idle, G(PLAZA + new Vector2(-6, 4)), 0f, T);
            K.Activity(ActivityType.Smoke, G(-8f, -160f), 0f, T);
            K.Activity(ActivityType.Drink, _seraiR.At(20f, 0, 20f), 0f, T);
            K.Activity(ActivityType.Office, _pressR.InRoom("PressOffice", 0.5f, 0.4f), 0f, G1);
            K.Activity(ActivityType.Office, _bunkerR.InRoom("Consoles", 0.5f, 0.5f), 0f, G1);
            K.Activity(ActivityType.Lookout, _dish + Vector3.up * 13.3f + new Vector3(1.5f, 0, 1.5f), 0f, G1);
            K.Activity(ActivityType.Phone, _bunkerR.At(4f, 0, -1.2f), 180f, G1);
            K.Activity(ActivityType.Garden, G(-60f, -70f), 0f, G1);
            K.Activity(ActivityType.Garden, G(80f, -50f), 0f, G1);
            K.Activity(ActivityType.Drink, G(POOL + new Vector2(0, 10)), 0f, G1);
            K.Activity(ActivityType.Dining, _bunkerR.InRoom("BunkerBunks", 0.5f, 0.5f), 0f, G1);
            K.Activity(ActivityType.Workout, G(124f, 108f), 0f, G1);
            K.Activity(ActivityType.Idle, G(LITTLE.xMin + 6f, 124f), 270f, G1);
            K.Activity(ActivityType.Speech, G(10f, -62f), 0f, G1);
            K.Activity(ActivityType.Smoke, G(60f, -80f), 0f, G1);
            A.Toilet(_pressR.InRoom("PressStore", 0.9f, 0.9f, 0f), 180f, G1);
            A.Toilet(_bunkerR.InRoom("BunkerBunks", 0.9f, 0.9f, 0f), 180f, G1);
            K.Activity(ActivityType.Office, Kp(28f, 3, 28f), 0f, C, 25f);
            K.Activity(ActivityType.Office, Kp(28f, 2, 20f), 0f, C, 20f);
            K.Activity(ActivityType.Office, Kp(28f, 1, 17f), 0f, C, 20f);
            K.Activity(ActivityType.Office, Kp(28f, -1, 17f), 0f, C, 25f);
            K.Activity(ActivityType.Speech, Kp(22f, 1, 9f), 180f, C, 20f);
            K.Activity(ActivityType.Drink, Kp(28f, 3, 2.8f), 0f, C, 20f);
            K.Activity(ActivityType.Inspect, Kp(28f, 1, 30f), 0f, C, 15f);
            K.Activity(ActivityType.Dining, Kp(5f, 3, 28f), 0f, C);
            K.Activity(ActivityType.Phone, Kp(4f, 2, 26f), 0f, C);
            K.Activity(ActivityType.Lookout, Kp(30f, 4, 30f), 0f, C);
            K.Activity(ActivityType.Workout, G(36f, 122f), 0f, C);
            K.Activity(ActivityType.Garden, G(-7f, 160f), 0f, C);
            K.Activity(ActivityType.Idle, G(-20f, 72f), 0f, C);
            K.Activity(ActivityType.Smoke, Kp(36f, 0, 20f), 0f, C);
        }

        private void Points()
        {
            K.Point(MatchPoint.Kind.Start, G(178f, -214f, 0.2f), 300f, "Bus station");
            K.Point(MatchPoint.Kind.Start, G(-232f, -122f, 0.2f), 80f, "Caravan road");
            K.Point(MatchPoint.Kind.Start, G(-40f, -222f, 0.2f), 0f, "Dune edge");
            K.Point(MatchPoint.Kind.Start, G(-170f, -14f, 0.2f), 60f, "Grove ford");
            K.Point(MatchPoint.Kind.Start, G(128f, -44f, 0.2f), 0f, "Wadi arch");
            K.Point(MatchPoint.Kind.Start, G(192f, 40f, 0.2f), 270f, "Canyon trail");
            K.Point(MatchPoint.Kind.Start, _teaR.InRoom("TeaRoom", 0.8f, 0.4f, 0.2f), _teaHouse.yaw, "Tea-house petitioner", ItemKind.Invitation);
            K.Point(MatchPoint.Kind.Start, _pressR.At(6f, 0, -2f, 0.2f), _press.yaw, "Press hand", ItemKind.StaffPass);
            K.Point(MatchPoint.Kind.Start, _baseR.At(4f, 0, -3f, 0.2f), 0f, "Water carrier", ItemKind.StaffPass);
            K.Point(MatchPoint.Kind.Extraction, Kp(25f, 4, 20f, 0.1f), 0f, "Helicopter");
            K.Point(MatchPoint.Kind.Extraction, G(-244f, -124f, 0.1f), 0f, "Car");
            K.Point(MatchPoint.Kind.Extraction, G(126f, -192f, 0.1f), 0f, "Bus");
            K.Point(MatchPoint.Kind.Extraction, G(-70f, -199f, 0.1f), 0f, "Qanat");

            ItemKind[] lGuns = { ItemKind.Pistol, ItemKind.Smg, ItemKind.Shotgun, ItemKind.SilencedPistol };
            int g = 0;
            void Gun(Vector3 p) => K.Weapon(lGuns[g++ % lGuns.Length], p);
            foreach (ArchResult h in _houses) if (!h.spec.shut && g < 10) Gun(h.InRoom("Front", 0.2f, 0.8f, 0.1f));
            Gun(_teaR.InRoom("TeaKitchen", 0.5f, 0.5f, 0.1f)); Gun(_seraiR.At(2f, 0, 2f, 0.1f)); Gun(_busR.Center("Ticket", 0.85f));
            Gun(_pressR.InRoom("PressLoft", 0.5f, 0.5f, 0.1f)); Gun(_kasbahR.InRoom("KasbahUpper", 0.5f, 0.5f, 0.1f)); Gun(_baseR.At(2f, 0, 5f, 0.1f));
            Gun(_bunkerR.InRoom("BunkerHall", 0.2f, 0.5f, 0.1f)); Gun(_bunkerR.InRoom("BunkerCellar", 0.5f, 0.5f, 0.1f)); Gun(_bunkerR.InRoom("BunkerBunks", 0.5f, 0.5f, 0.1f));
            Gun(G(122f, 104f, 0.2f)); Gun(G(170f, 110f, 0.1f));
            foreach (ArchResult b in _bastions) { Gun(b.Center("BastionGuard", 0.1f)); Gun(b.Center("BastionTop", 0.1f)); }
            Gun(Kp(28f, -1, 28f, 0.1f)); Gun(Kp(30f, -1, 31f, 0.1f)); Gun(Kp(30f, 0, 20f, 0.1f)); Gun(Kp(5f, 2, 26f, 0.1f));
            Gun(Kp(28f, 2, 30f, 0.1f)); Gun(Kp(5f, 3, 5f, 0.1f)); Gun(Kp(17f, 2, 5f, 0.1f)); Gun(G(-50f, 128f, 0.1f));
            K.Weapon(ItemKind.Sniper, new Vector3(NEEDLE.x + 1f, 60.25f, NEEDLE.y + 1f));
            K.Weapon(ItemKind.Sniper, _kasbahR.At(7f, 2, 6f, 0.1f));
            K.UniqueWeapon(ItemKind.RocketLauncher, Kp(32f, -1, 32f, 0.1f), "Armoury");
            K.UniqueWeapon(ItemKind.RocketLauncher, _bunkerR.InRoom("BunkerCellar", 0.9f, 0.2f, 0.1f), "Bunker cellar");
            K.UniqueWeapon(ItemKind.RocketLauncher, _seraiR.At(28f, 1, 28f, 0.1f), "Caravanserai loft");
            K.UniqueWeapon(ItemKind.Flamethrower, _pressR.At(2f, 0, 10f, 0.1f), "Press floor");
            K.UniqueWeapon(ItemKind.Flamethrower, G(122f, 96f, 0.2f), "Motor pool");
            K.UniqueWeapon(ItemKind.Flamethrower, _kasbahR.InRoom("KasbahStore", 0.8f, 0.8f, 0.1f), "Kasbah store");
            K.Gadget(ItemKind.SmokeGrenade, G(-150f, -20f, 0.1f));
            K.Gadget(ItemKind.C4, G(122f, 98f, 0.3f));
            K.Gadget(ItemKind.C4, Kp(31f, -1, 29f, 0.1f));
            Vector2[] lLoose = { new Vector2(-100, -40), new Vector2(-40, -80), new Vector2(40, -40), new Vector2(100, -70), new Vector2(0, -10), new Vector2(-150, -10), new Vector2(140, -10), new Vector2(-10, 30), new Vector2(30, 40), new Vector2(KASBAH.x, KASBAH.y + 8f), new Vector2(-30, 70), new Vector2(30, 70), new Vector2(-50, 150), new Vector2(40, 160), new Vector2(150, 140), new Vector2(-200, -120) };
            foreach (Vector2 p in lLoose) A.Throwable(G(p, 0.1f), ItemKind.Rock);
            for (int i = 0; i < 8; i++) A.Throwable(G(-50f + i * 14f, i % 2 == 0 ? -147f : -153f, 0.1f), ItemKind.Bottle);
            for (int i = 0; i < 6; i++) A.Throwable(G(-40f + i * 14f, 120f + (i % 3) * 14f, 0.1f));
        }

        private void Chatter()
        {
            A.Chatter(_screwPress + new Vector3(0, 0, 2.5f), "Press hands", "The treasurer pencils the vault code in the tithe ledger. It's on the scale desk.", "He weighs the tithe right on the press bed. Every afternoon.");
            A.Chatter(Kp(5f, 0, 30f), "Cooks", "The lift operator smokes down at the base station. Keycard and all.", "Chlorine drums next to the cooler. One spill and the whole keep breathes it.");
            A.Chatter(_pressR.InRoom("PressOffice", 0.5f, 0.5f), "Foreman", "Idris weighs the tithe himself. Stands right on the bed while we turn the screw.", "Spare lift card in my drawer. Don't lose it again.");
            A.Chatter(Kp(28f, 1, 29f), "Scribes", "That archive shelf is going to come down on someone.", "The niche in the waiting gallery opens onto a stair. Nobody uses it.");
            A.Chatter(_teaR.InRoom("TeaRoom", 0.5f, 0.5f), "Tea-house owner", "The buyer sits by the brazier for the story. That canister leaks.", "Oleander grows on the channel banks. Never put it in the tea.");
            A.Chatter(_bunkerR.InRoom("Consoles", 0.5f, 0.5f), "Technicians", "Release the brake and the dish sweeps the gantry clean.", "The colonel walks the bridge to the keep every hour.");
            A.Chatter(Kp(28f, 3, 30f), "Servants", "The governor takes his tea on the pool terrace. Always the same glass.", "Colocynth in the pantry. The governor runs for his toilet after one cup.");
            A.Chatter(G(-60f, -70f), "Farmers", "Oleander on the banks. The goats won't touch it.", "The qanat runs dry all the way up into the keep's cistern.");
        }
    }
}
