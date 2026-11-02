using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapWeisshorn
    {
        private void Guard(Vector3 p, float pYaw, int pZone, ItemKind pWeapon = ItemKind.Smg, ItemKind pCarries = ItemKind.Fists, bool pEnforcer = false) => A.Npc(NpcRole.Guard, p, pYaw, pZone, pWeapon, pCarries, pEnforcer);
        private void Civ(Vector3 p, float pYaw, int pZone, ItemKind pCarries = ItemKind.Fists, bool pEnforcer = false) => A.Npc(NpcRole.Worker, p, pYaw, pZone, ItemKind.Fists, pCarries, pEnforcer);

        private void Matches()
        {
            K.Zone(ZONE_VILLAGE, "Sankt Anna", G(0f, -155f), new Vector3(260f, 60f, 110f), G(105f, -150f), new Color(0.55f, 0.15f, 0.15f), HatStyle.Beanie, _gasthausR.Center("Landlord"));
            K.Zone(ZONE_SKI, "Ski Area & Observatory", G(20f, 40f), new Vector3(420f, 140f, 300f), G(150f, -60f), new Color(0.2f, 0.3f, 0.5f), HatStyle.Beanie, _observatoryR.Center("Bunker1"));
            K.Zone(ZONE_HOTEL, "Grand Hotel Weisshorn", G(0f, 98f), new Vector3(130f, 70f, 70f), new Vector3(48f, PLATFORM_Y + 4f, 117f), new Color(0.1f, 0.1f, 0.12f), HatStyle.Fedora, Hp(20f, 1, 22f));
            A.Zone(G(0f, -155f, -2f), new Vector3(260f, 30f, 100f), 0f, SecurityTier.Public, AccessLevel.None, "Village");
            A.Zone(G(20f, -20f, -10f), new Vector3(300f, 80f, 190f), 0f, SecurityTier.Public, AccessLevel.None, "Pistes");
            A.Zone(new Vector3(0f, PLATFORM_Y + 7f, 77f), new Vector3(80f, 10f, 12f), 0f, SecurityTier.Restricted, AccessLevel.Guest, "FrontTerrace");
            A.Zone(new Vector3(-20f, PLATFORM_Y + 3f, 79f), new Vector3(34f, 4f, 8f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "SkiRoomTerrace");
            A.Zone(new Vector3(0f, PLATFORM_Y - 1f, 70f), new Vector3(80f, 5f, 24f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "LowerTerrace");
            A.Zone(new Vector3(21f, PLATFORM_Y + 3f, 118f), new Vector3(60f, 12f, 16f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "DeliveryYard");
            A.Zone(new Vector3(-49f, PLATFORM_Y + 7f, 96f), new Vector3(28f, 12f, 34f), 0f, SecurityTier.Restricted, AccessLevel.None, "SpaGarden");
            A.Zone(G(OBSERVATORY, -2f), new Vector3(80f, 40f, 60f), 0f, SecurityTier.Hostile, AccessLevel.Security, "Observatory");
            A.Zone(G(LIFT_BOTTOM, -2f), new Vector3(20f, 12f, 24f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "LiftStation");
            A.Zone(G(GARAGE, -2f), new Vector3(30f, 14f, 26f), 0f, SecurityTier.Restricted, AccessLevel.Staff, "GarageYard");

            // ---- village (27)
            const int V = ZONE_VILLAGE;
            K.Crowd(G(-40f, -150f), 2f, 14f, 8, V);
            K.Crowd(G(40f, -150f), 2f, 14f, 6, V);
            K.Crowd(G(70f, -176f), 3f, 9f, 8, V);
            Guard(G(-2f, -147f), 180f, V, ItemKind.Pistol);
            Guard(G(2f, -147f), 180f, V, ItemKind.Pistol);
            Civ(_gasthausR.InRoom("Stube", 0.8f, 0.2f), _gasthaus.yaw, V);
            Civ(_gasthausR.InRoom("Stube", 0.3f, 0.4f), _gasthaus.yaw, V);
            Civ(_gasthausR.InRoom("Stube", 0.5f, 0.8f), _gasthaus.yaw + 180f, V, ItemKind.Invitation);
            Civ(_valleyR.Center("Waiting"), 0f, V, ItemKind.Invitation);
            A.BigCrate(_valleyR.At(2f, 0, 8f, 0f), 0f);

            // ---- ski area and observatory (17)
            const int S = ZONE_SKI;
            Civ(_bullwheel + new Vector3(2f, 0.05f, 1f), 270f, S); Civ(_liftBottomR.Center("LiftBottomRoom"), 0f, S); Civ(_liftTopR.At(4.5f, 0, -1.5f), 180f, S);
            Civ(_midR.InRoom("MidStationRoom", 0.3f, 0.5f), 0f, S, ItemKind.StaffPass); Civ(_midR.InRoom("MidStationRoom", 0.7f, 0.5f), 180f, S, ItemKind.StaffPass);
            A.Patrol(S, 2, ItemKind.Smg, 6f, G(-20f, -60f), G(40f, -20f), G(20f, 40f), G(-40f, 20f));
            Civ(_tiller + new Vector3(2f, 0.05f, -1f), 0f, S);
            Civ(_garageR.At(12f, 0, 9f), 270f, S, ItemKind.StaffPass);
            A.Npc(NpcRole.Lookout, _skiJumpTop + new Vector3(1.5f, 0.35f, 1.5f), 0f, S, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, _skiJumpTop + new Vector3(-1.5f, 0.35f, 1.5f));
            A.Patrol(S, 2, ItemKind.Smg, 4f, G(OBSERVATORY + new Vector2(-28, -20)), G(OBSERVATORY + new Vector2(28, -20)), G(OBSERVATORY + new Vector2(28, 18)), G(OBSERVATORY + new Vector2(-28, 18)));
            Guard(_observatoryR.At(8f, 0, -1.5f), 180f, S); Guard(_observatoryR.At(10f, 0, -1.5f), 180f, S, ItemKind.Shotgun);
            Civ(_observatoryR.InRoom("ObsLab", 0.5f, 0.5f), 0f, S); Civ(_observatoryR.InRoom("Bunker1", 0.5f, 0.5f), 0f, S);
            Civ(_cableTopR.Center("CableTopRoom"), 0f, S, ItemKind.Keycard);

            // ---- hotel security (20), gala (39), staff (13)
            const int H = ZONE_HOTEL;
            Guard(new Vector3(-17f, PLATFORM_Y + 8.05f, 75f), 180f, H); Guard(new Vector3(-13f, PLATFORM_Y + 8.05f, 75f), 180f, H);
            A.Patrol(H, 2, ItemKind.Smg, 4f, new Vector3(-30f, PLATFORM_Y + 8.05f, 75.5f), new Vector3(30f, PLATFORM_Y + 8.05f, 75.5f), new Vector3(30f, PLATFORM_Y + 8.05f, 80f), new Vector3(-30f, PLATFORM_Y + 8.05f, 80f));
            A.Patrol(H, 2, ItemKind.Smg, 4f, G(-40f, 66f), G(40f, 66f), G(40f, 75f), G(-40f, 75f));
            Guard(G(10f, 116f), 0f, H); Guard(G(14f, 116f), 0f, H, ItemKind.Shotgun);
            Guard(Hp(16f, 3, 4f), 90f, H); Guard(Hp(16f, 3, 6.5f), 90f, H, ItemKind.Shotgun);
            Guard(Hp(10f, 4, 18.5f), 90f, H); Guard(Hp(30f, 4, 18.5f), 270f, H);
            Guard(Hp(41.5f, 3, 12f), 90f, H); Guard(Hp(68.5f, 3, 12f), 270f, H);
            Civ(Hp(20f, 1, 20f), 0f, H, ItemKind.Keycard);
            A.Npc(NpcRole.Lookout, Hp(24f, 9, 16f), 180f, H, ItemKind.Sniper, ItemKind.Fists, true);
            K.Weapon(ItemKind.Sniper, Hp(22f, 9, 14f, 0.1f));
            for (int i = 0; i < 4; i++) Guard(Hp(50f + i * 4f, 2, 22f), 180f, H, ItemKind.Pistol, ItemKind.Fists, true);
            K.Crowd(Hp(55f, 2, 12f), 2f, 9f, 20, H);
            K.Crowd(Hp(20f, 2, 22f), 1.5f, 4f, 6, H);
            for (int i = 0; i < 6; i++) Civ(Hp(44f + i * 4.5f, 2, i % 2 == 0 ? 4f : 18f), 0f, H, ItemKind.StaffPass);
            for (int i = 0; i < 4; i++) Civ(Hp(51f + i * 2.5f, 2, 27.5f, 1.1f), 180f, H);
            for (int i = 0; i < 3; i++) Civ(Hp(4f + i * 2.5f, 3, 25f), 180f, H, ItemKind.Invitation);
            Civ(Hp(20f, 2, 6f), 0f, H, ItemKind.StaffPass, true);
            Civ(Hp(35f, 1, 26f), 0f, H, ItemKind.StaffPass); Civ(Hp(31f, 1, 20f), 90f, H); Civ(Hp(38f, 1, 20f), 270f, H);
            Civ(Hp(20f, 0, 22f), 0f, H, ItemKind.StaffPass); Civ(Hp(8f, 0, 6f), 0f, H);
            Civ(Hp(31f, 3, 26f), 0f, H); Civ(Hp(6f, 1, 26f), 90f, H, ItemKind.CodeNote);
            Civ(Hp(4f, 4, 4f), 0f, H, ItemKind.StaffPass); Civ(Hp(6f, 3, 4f), 0f, H); Civ(Hp(33f, 3, 4f), 0f, H);
            Civ(Hp(9f, 2, 20f), 0f, H); Civ(Hp(33f, 2, 22f), 0f, H);
            K.Activity(ActivityType.Smoke, Hp(4f, 1, 22f), 0f, H);

            Activities();
            Points();
            Chatter();
        }

        private void Activities()
        {
            const int V = ZONE_VILLAGE, S = ZONE_SKI, H = ZONE_HOTEL;
            K.Activity(ActivityType.Dining, _gasthausR.InRoom("Stube", 0.3f, 0.6f), 0f, V, 20f);
            K.Activity(ActivityType.Phone, _gasthausR.InRoom("Landlord", 0.5f, 0.5f), 0f, V);
            K.Activity(ActivityType.Office, _gasthausR.InRoom("Landlord", 0.3f, 0.3f), 0f, V);
            K.Activity(ActivityType.Idle, G(70f, -170f), 0f, V);
            K.Activity(ActivityType.Speech, G(-20f, -128f), 180f, V);
            K.Activity(ActivityType.Inspect, _valleyR.Center("Waiting"), 0f, V);
            K.Activity(ActivityType.Lookout, _churchR.At(7f, 5, 2.5f), 180f, V);
            K.Activity(ActivityType.Smoke, G(-60f, -160f), 0f, V);
            K.Activity(ActivityType.Garden, G(-100f, -140f), 0f, V);
            K.Activity(ActivityType.Workout, G(80f, -176f), 0f, V);
            A.Toilet(_gasthausR.InRoom("GastKitchen", 0.9f, 0.2f, 0f), _gasthaus.yaw, V);
            K.Activity(ActivityType.Inspect, _observatoryR.InRoom("Bunker1", 0.6f, 0.6f), 0f, S, 25f);
            K.Activity(ActivityType.Office, _observatoryR.InRoom("Command", 0.5f, 0.5f), 0f, S, 25f);
            K.Activity(ActivityType.Office, _observatoryR.InRoom("ObsLab", 0.5f, 0.5f), 0f, S);
            K.Activity(ActivityType.Phone, _cableTopR.At(4f, 0, -1.5f), 180f, S);
            K.Activity(ActivityType.Smoke, G(OBSERVATORY + new Vector2(-10, -18)), 0f, S);
            K.Activity(ActivityType.Lookout, _domeTop + Vector3.up * 0.1f + new Vector3(0, 0, -6.5f), 180f, S);
            K.Activity(ActivityType.Drink, _midR.InRoom("MidStationRoom", 0.5f, 0.3f), 0f, S);
            K.Activity(ActivityType.Dining, _observatoryR.InRoom("ObsBunks", 0.5f, 0.5f), 0f, S);
            K.Activity(ActivityType.Workout, G(MIDSTATION + new Vector2(14, 0)), 0f, S);
            K.Activity(ActivityType.Garden, G(-30f, 20f), 0f, S);
            K.Activity(ActivityType.Speech, G(MIDSTATION + new Vector2(0, -12)), 0f, S);
            K.Activity(ActivityType.Idle, G(LIFT_BOTTOM + new Vector2(-4, -6)), 0f, S);
            A.Toilet(_midR.InRoom("MidStationRoom", 0.9f, 0.9f, 0f), _midStation.yaw + 180f, S);
            K.Activity(ActivityType.Office, Hp(28f, 4, 4f), 0f, H, 25f);
            K.Activity(ActivityType.Office, Hp(20f, 1, 24f), 0f, H, 25f);
            K.Activity(ActivityType.Idle, Hp(36f, 4, 4f), 0f, H, 20f);
            K.Activity(ActivityType.Speech, Hp(55f, 2, 25f, 1.1f), 180f, H, 20f);
            K.Activity(ActivityType.Workout, Hp(3f, 2, 28f), 0f, H, 20f);
            K.Activity(ActivityType.Inspect, Hp(20f, 4, 18.5f), 0f, H, 15f);
            K.Activity(ActivityType.Drink, Hp(19f, 2, 26f), 0f, H);
            K.Activity(ActivityType.Garden, G(-50f, 96f), 0f, H);
            K.Activity(ActivityType.Dining, Hp(50f, 2, 8f), 0f, H);
            K.Activity(ActivityType.Phone, Hp(30f, 2, 4f), 0f, H);
            K.Activity(ActivityType.Smoke, new Vector3(25f, PLATFORM_Y + 8.05f, 76f), 0f, H);
            K.Activity(ActivityType.Lookout, Hp(24f, 9, 18f), 0f, H);
            A.Toilet(Hp(2f, 2, 17f, 0f), 0f, H);
        }

        private void Points()
        {
            K.Point(MatchPoint.Kind.Start, G(150f, -150f, 0.2f), 270f, "Bus stop");
            K.Point(MatchPoint.Kind.Start, G(-165f, -150f, 0.2f), 120f, "Boathouse");
            K.Point(MatchPoint.Kind.Start, G(-110f, -215f, 0.2f), 30f, "Lake shore");
            K.Point(MatchPoint.Kind.Start, G(128f, -116f, 0.2f), 300f, "Chairlift bottom");
            K.Point(MatchPoint.Kind.Start, G(-120f, 60f, 0.2f), 60f, "Pine copse");
            K.Point(MatchPoint.Kind.Start, G(-60f, 178f, 0.2f), 120f, "Glacier trail");
            K.Point(MatchPoint.Kind.Start, _gasthausR.InRoom("Stube", 0.2f, 0.2f, 0.2f), _gasthaus.yaw, "Gala guest at the Gasthaus", ItemKind.Invitation);
            K.Point(MatchPoint.Kind.Start, _midR.At(7f, 0, -2f, 0.2f), _midStation.yaw, "Ski instructor", ItemKind.StaffPass);
            K.Point(MatchPoint.Kind.Start, G(30f, 120f, 0.2f), 180f, "Waiter, delivery yard", ItemKind.StaffPass);
            K.Point(MatchPoint.Kind.Extraction, Hp(62f, 4, 20f, 0.1f), 0f, "Helicopter");
            K.Point(MatchPoint.Kind.Extraction, _cableTopR.At(4f, 0, 3f, 0.1f), 0f, "Cable car");
            K.Point(MatchPoint.Kind.Extraction, G(108f, -160f, 0.1f), 0f, "Snowmobile");
            K.Point(MatchPoint.Kind.Extraction, _valleyR.At(6f, 0, 8f, 0.1f), 0f, "Funicular");

            ItemKind[] lGuns = { ItemKind.Pistol, ItemKind.Smg, ItemKind.Shotgun, ItemKind.SilencedPistol };
            int g = 0;
            void Gun(Vector3 p) => K.Weapon(lGuns[g++ % lGuns.Length], p);
            foreach (ArchResult c in _chalets) if (!c.spec.shut && g < 8) Gun(c.InRoom("Front", 0.2f, 0.8f, 0.1f));
            Gun(_gasthausR.InRoom("GastCellar", 0.5f, 0.5f, 0.1f)); Gun(_gasthausR.InRoom("Landlord", 0.5f, 0.5f, 0.85f)); Gun(_boathouseR.Center("Boats", 0.1f));
            Gun(_garageR.At(2f, 0, 2f, 0.1f)); Gun(_garageR.InRoom("GarageMine", 0.5f, 0.5f, 0.1f)); Gun(_mineHutR.Center("MineHutRoom", 0.1f)); Gun(_liftTopR.Center("LiftTopRoom", 0.1f));
            Gun(_observatoryR.InRoom("Bunker1", 0.3f, 0.3f, 0.1f)); Gun(_observatoryR.InRoom("Command", 0.8f, 0.8f, 0.1f)); Gun(_observatoryR.InRoom("ObsBunks", 0.5f, 0.5f, 0.1f)); Gun(_antennaR.Center("AntennaRoom", 0.1f));
            Gun(Hp(20f, 1, 26f, 0.1f)); Gun(Hp(7f, 1, 7f, 0.1f)); Gun(Hp(6f, 4, 26f, 0.1f)); Gun(Hp(20f, 4, 26f, 0.1f)); Gun(Hp(30f, 0, 20f, 0.1f));
            Gun(Hp(7f, 5, 20f, 0.1f)); Gun(Hp(24f, 5, 16f, 0.1f)); Gun(Hp(5f, 4, 4f, 0.1f)); Gun(Hp(60f, 1, 15f, 0.1f)); Gun(Hp(62f, 0, 20f, 0.1f));
            Gun(G(20f, 120f, 0.2f)); Gun(G(-50f, 100f, 0.1f));
            K.Weapon(ItemKind.Sniper, _churchR.At(7f, 5, 2.5f, 0.1f));
            K.UniqueWeapon(ItemKind.RocketLauncher, _observatoryR.InRoom("Command", 0.2f, 0.2f, 0.1f), "Bunker command");
            K.UniqueWeapon(ItemKind.RocketLauncher, Hp(65f, 0, 25f, 0.1f), "Snowcat bay");
            K.UniqueWeapon(ItemKind.RocketLauncher, _boathouseR.At(8f, 0, 5f, 0.1f), "Boathouse");
            K.UniqueWeapon(ItemKind.Flamethrower, _garageR.At(14f, 0, 6f, 0.1f), "Groomer garage");
            K.UniqueWeapon(ItemKind.Flamethrower, Hp(21f, 0, 20f, 0.1f), "Boiler room");
            K.UniqueWeapon(ItemKind.Flamethrower, _mineHutR.Center("MineHutRoom", 0.1f), "Mine hut");
            K.Gadget(ItemKind.SmokeGrenade, G(-100f, 30f, 0.1f));
            K.Gadget(ItemKind.C4, new Vector3(61.5f, 18.1f, 40f));
            Vector2[] lLoose = { new Vector2(-60, -100), new Vector2(0, -80), new Vector2(50, -60), new Vector2(-30, -30), new Vector2(30, 0), new Vector2(-50, 40), new Vector2(80, 30), new Vector2(110, 0), new Vector2(-120, -100), new Vector2(-80, 100), new Vector2(0, 140), new Vector2(60, 150), new Vector2(-30, 160), new Vector2(150, -100), new Vector2(-140, -180), new Vector2(0, -190) };
            foreach (Vector2 p in lLoose) A.Throwable(G(p, 0.1f), ItemKind.Rock);
            for (int i = 0; i < 8; i++) A.Throwable(G(-90f + i * 25f, i % 2 == 0 ? -146f : -154f, 0.1f), ItemKind.Bottle);
            for (int i = 0; i < 6; i++) A.Throwable(Hp(44f + i * 4.5f, 2, 1.5f, 0.1f), ItemKind.Bottle);
        }

        private void Chatter()
        {
            A.Chatter(Hp(4f, 1, 26f), "Maids", "The butler keeps the presidential code on a card in his waistcoat.", "He takes his break in the linen room. Alone, every time.");
            A.Chatter(Hp(5f, 2, 22f), "Spa attendant", "Herr Aschauer likes the sauna at ninety. The dial sticks above that.", "Lock the door from outside and nobody hears a thing.");
            A.Chatter(_bullwheel + new Vector3(3f, 0, 0), "Lift operators", "The banker always boards right by the bullwheel. Drives me mad.", "Open the guard and that wheel would take an arm.");
            A.Chatter(_tiller + new Vector3(3f, 0, -3f), "Mechanic", "Brandt inspects the tiller himself every round. Stands right in front of it.", "The old blasting crate is still down the mine gallery.");
            A.Chatter(Hp(50f, 2, 12f), "Waiters", "They say the chandelier rigging over the stage is held by one cable.", "Aschauer gives his speech right under it.");
            A.Chatter(Hp(35f, 1, 24f), "Kitchen hands", "Aconite in the spa greenhouse. The gardener wears gloves for it.", "The dumbwaiter goes from the wine cellar up behind the bar.");
            A.Chatter(_midR.InRoom("MidStationRoom", 0.5f, 0.5f), "Instructors", "There's a crowbar in the groomer garage, by the workbench.", "The mine gallery comes out under the hotel's cold store.");
        }
    }
}
