using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEditor;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The Downtown Strip (zone 0): Main Street (east-west) crossed by Ash Avenue, a ring of Back
    /// Street, Birch Street and Mill Road for the police cruiser, two back alleys with dumpsters and
    /// fire escapes, Depot Street and the railway. Public: the sidewalks, the diner, the mall
    /// atrium, the car lot, the station. Restricted: the back alleys, the police yard, the mall's
    /// rooftop car park, the cinema roof. VIP: the police chief's office, the bank vault, the
    /// projection booth, Darlene's office above the food court. Landmark: the Elmridge water tower
    /// (sniper catwalk, radio mast). Staging: the lunch rush at Pike's Diner, a sale at Malloy
    /// Motors, a briefing at the police station, a showing at the Starlight, the evening meeting
    /// at the hall.
    /// </summary>
    public partial class MapSuburb
    {
        private const float DEPOT_Z = -300f, ALLEY_N = -196f, ALLEY_S = -276f;
        private static readonly Color[] SHOP_WALLS =
        {
            new Color(0.72f, 0.42f, 0.34f), new Color(0.92f, 0.86f, 0.72f), new Color(0.62f, 0.66f, 0.6f), new Color(0.82f, 0.62f, 0.45f),
            new Color(0.95f, 0.93f, 0.88f), new Color(0.6f, 0.5f, 0.55f), new Color(0.78f, 0.74f, 0.58f)
        };
        private static readonly Color FLAT_ROOF = new Color(0.42f, 0.42f, 0.44f);
        private static readonly Color POLICE_BLUE = new Color(0.22f, 0.32f, 0.55f);
        private Material _glassMaterial;
        private readonly List<MapKit.KitSpec> _shops = new List<MapKit.KitSpec>();

        /// <summary>A shop with flats above on Main Street (north or south side): shop and stock room, flats, a fire escape at the back.</summary>
        private MapKit.KitSpec PlanShop(string pName, string pStyle, float pX, int pW, int pD, int pFloors, bool pNorth, bool pBrick, int pTint, Room pShop = Room.Shop)
        {
            float z = pNorth ? MAIN_Z + 14f + pD * 1.5f : MAIN_Z - 14f - pD * 1.5f;
            MapKit.KitSpec s = Lot(pName, pStyle, pX, z, pNorth ? 0f : 180f, pW, pD, pFloors, Roof.Flat, SHOP_WALLS[pTint % SHOP_WALLS.Length], FLAT_ROOF, 0, 0f, 3f);
            s.brick = pBrick;
            s.tallWindows = pStyle != "Timber";
            s.windowChance = 0.8f;
            s.balconyChance = pFloors > 2 ? 0.2f : 0.1f;
            Doors(s, 0, pW / 2, 2, 1);
            s.rooms.Add(new[] { pShop, Room.Storage });
            for (int f = 1; f < pFloors; f++)
            {
                s.rooms.Add(f % 2 == 1 ? new[] { Room.Lounge, Room.Kitchen } : new[] { Room.Bedroom, Room.Bathroom });
                s.upperDoors.Add(new Vector3Int(2, pW - 1, f));
            }
            _shops.Add(s);
            return s;
        }

        private void PlanDowntown()
        {
            _zoneStage.Add(() => Flatten(R(-440, -392, -70, -112), 0f, 24f));
            _zoneStage.Add(() => Flatten(R(-500, -378, 446, -366), 0f, 12f));
            // Streets.
            Road(12f, ASPHALT, new Vector2(-498, MAIN_Z), new Vector2(-300, MAIN_Z), new Vector2(-80, MAIN_Z), new Vector2(170, MAIN_Z), new Vector2(330, MAIN_Z), new Vector2(436, MAIN_Z));
            Road(12f, ASPHALT, new Vector2(ASH_X, -345), new Vector2(ASH_X, MAIN_Z), new Vector2(ASH_X, BACK_Z), new Vector2(ASH_X, MAPLE_Z), new Vector2(ASH_X, WILLOW_Z), new Vector2(ASH_X, OAK_Z));
            Lane(7f, ASPHALT, new Vector2(ASH_X, -345), new Vector2(ASH_X, -400), new Vector2(-160, -440), new Vector2(-60, -496));
            Road(10f, ASPHALT, new Vector2(BIRCH_X, MAIN_Z), new Vector2(BIRCH_X, BACK_Z), new Vector2(BIRCH_X, MAPLE_Z), new Vector2(BIRCH_X, WILLOW_Z), new Vector2(BIRCH_X, OAK_Z));
            Road(9f, ASPHALT, new Vector2(MILL_X, BACK_Z), new Vector2(BIRCH_X, BACK_Z));
            Road(9f, ASPHALT, new Vector2(MILL_X, DEPOT_Z), new Vector2(MILL_X, MAIN_Z), new Vector2(MILL_X, BACK_Z));
            Road(8f, ASPHALT, new Vector2(MILL_X, DEPOT_Z), new Vector2(-95, DEPOT_Z));
            Lane(6f, ASPHALT, new Vector2(-410, ALLEY_N), new Vector2(-262, ALLEY_N));
            Lane(6f, ASPHALT, new Vector2(-410, ALLEY_S), new Vector2(-92, ALLEY_S));
            // Sidewalks, lots, the atrium, the station plaza.
            PaintRect(R(-440, -244, -72, -240), PAVE);
            PaintRect(R(-440, -220, -72, -216), PAVE);
            PaintRect(R(-298, -216, -262, -184), ASPHALT);
            PaintRect(R(-196, -216, -184, -186), PAVE);
            PaintRect(R(-164, -186, -88, -136), ASPHALT);
            PaintRect(R(-156, -216, -88, -168), new Color(0.4f, 0.4f, 0.42f));
            PaintRect(R(-384, -268, -360, -244), ASPHALT);
            PaintRect(R(-306, -276, -242, -244), CONCRETE);
            PaintRect(R(-122, -268, -90, -244), ASPHALT);
            PaintRect(R(-260, -356, -150, -306), PAVE);
            PaintRect(R(-412, -190, -244, -140), ASPHALT);
            PaintRect(R(-500, -380, 440, -364), GRAVEL);
            _tame.Add(R(-445, -395, -70, -110));

            // North side, west of Ash: hardware, bank, pawn shop, barber, books, pharmacy, the diner.
            PlanShop("Kessler Hardware", "Suburb", -397.5f, 5, 4, 3, true, true, 0);
            MapKit.KitSpec lBank = Lot("Elmridge Savings Bank", "Classic", -377f, MAIN_Z + 14f + 7.5f, 0, 6, 5, 2, Roof.Flat, new Color(0.9f, 0.87f, 0.8f), FLAT_ROOF, 0, 0f, 3f);
            Doors(lBank, 0, 2, 0, 3, 2, 1);
            lBank.tallWindows = true;
            lBank.balconyChance = 0f;
            Floors(lBank, Room.Lounge, Room.Security, Room.Office, Room.Office);
            PlanShop("Lucky Penny Pawn", "Suburb", -356f, 4, 4, 3, true, false, 1);
            PlanShop("Harmon Barber", "Suburb", -343f, 4, 4, 2, true, true, 3);
            PlanShop("Rosewood Books", "Timber", -330f, 4, 4, 3, true, false, 4);
            PlanShop("Ellison Pharmacy", "Suburb", -308.5f, 5, 4, 3, true, false, 6);
            MapKit.KitSpec lDiner = Lot("Pike's Diner", "Suburb", -250f, MAIN_Z + 20f, 0, 6, 4, 1, Roof.Flat, new Color(0.95f, 0.93f, 0.88f), new Color(0.75f, 0.2f, 0.2f), 0, 0f, 3f);
            Doors(lDiner, 0, 1, 1, 0, 2, 4);
            lDiner.windowChance = 1f;
            lDiner.tallWindows = true;
            // North side, east of Ash: the mall's two wings around the atrium, the car lot office.
            foreach (float x in new[] { -205f, -175f })
            {
                MapKit.KitSpec w = Lot(x < -190f ? "Ridgeview Mall West" : "Ridgeview Mall East", "Classic", x, -201f, 0, 6, 10, 2, Roof.Flat, new Color(0.88f, 0.84f, 0.78f), FLAT_ROOF, 0, 0f, 3f);
                Doors(w, 0, 2, 0, 3, 2, 2);
                Doors(w, x < -190f ? 1 : 3, 2, x < -190f ? 1 : 3, 5);
                w.upperDoors.Add(new Vector3Int(x < -190f ? 1 : 3, x < -190f ? 5 : 4, 1));
                w.tallWindows = true;
                w.balconyChance = 0f;
                if (x < -190f) Floors(w, Room.Shop, Room.Shop, Room.Office, Room.Dining);
                else Floors(w, Room.Shop, Room.Bar, Room.Lounge, Room.Bathroom);
            }
            MapKit.KitSpec lMotors = Lot("Malloy Motors", "Suburb", -104f, -180f, 0, 4, 3, 1, Roof.Flat, new Color(0.95f, 0.95f, 0.92f), new Color(0.2f, 0.35f, 0.65f), 0, 0f, 3f);
            Doors(lMotors, 0, 1, 2, 2);
            lMotors.windowChance = 1f;
            lMotors.tallWindows = true;
            lMotors.roofAccess = false;
            lMotors.rooms.Add(new[] { Room.Office });

            // South side: police station, laundromat, records, bakery, the gas station shop; the cinema, tavern, post office, gazette, dry goods.
            MapKit.KitSpec lPolice = Lot("Elmridge Police", "Classic", -398f, MAIN_Z - 14f - 9f, 180, 8, 6, 2, Roof.Flat, new Color(0.86f, 0.84f, 0.8f), FLAT_ROOF, 0, 0f, 3f);
            Doors(lPolice, 0, 3, 0, 4, 2, 2, 1, 4);
            lPolice.brick = false;
            lPolice.tallWindows = false;
            lPolice.balconyChance = 0f;
            lPolice.climb = new Vector2Int(3, 1);
            lPolice.rooms.Add(new Room[0]);
            Floors(lPolice, Room.Office, Room.Lounge);
            PlanShop("Suds Laundromat", "Suburb", -350.5f, 5, 4, 2, false, false, 2);
            PlanShop("Dellamy Records", "Suburb", -336f, 4, 4, 3, false, true, 5);
            PlanShop("Ridge Bakery", "Timber", -316f, 4, 4, 2, false, false, 1, Room.Kitchen);
            MapKit.KitSpec lFuel = Lot("Sunset Fuel", "Suburb", -293f, MAIN_Z - 14f - 4.5f - 6f, 180, 4, 3, 1, Roof.Flat, new Color(0.95f, 0.95f, 0.92f), new Color(0.85f, 0.4f, 0.15f), 0, 0f, 3f);
            Doors(lFuel, 0, 1, 2, 2);
            lFuel.windowChance = 1f;
            lFuel.roofAccess = false;
            Floors(lFuel, Room.Shop, Room.None);
            MapKit.KitSpec lCinema = Lot("Starlight Cinema", "Suburb", -204.5f, MAIN_Z - 14f - 12f, 180, 7, 8, 2, Roof.Flat, new Color(0.62f, 0.3f, 0.32f), FLAT_ROOF, 0, 0f, 3f);
            Doors(lCinema, 0, 2, 0, 3, 0, 4, 2, 1, 1, 6);
            lCinema.brick = true;
            lCinema.windowChance = 0.25f;
            lCinema.balconyChance = 0f;
            PlanShop("Main Street Tavern", "Suburb", -182.5f, 5, 4, 3, false, true, 0, Room.Bar);
            MapKit.KitSpec lPost = Lot("Elmridge Post Office", "Classic", -161.5f, MAIN_Z - 14f - 6f, 180, 5, 4, 1, Roof.Flat, new Color(0.88f, 0.86f, 0.82f), FLAT_ROOF, 0, 0f, 3f);
            Doors(lPost, 0, 2, 2, 1);
            lPost.tallWindows = true;
            Floors(lPost, Room.Office, Room.Storage);
            PlanShop("Elmridge Gazette", "Suburb", -147f, 4, 4, 2, false, true, 3, Room.Office);
            PlanShop("Hollis Dry Goods", "Suburb", -131.5f, 5, 4, 3, false, false, 6);

            // Depot Street: the feed store, the fire station; the railway depot; the meeting hall on Back Street.
            MapKit.KitSpec lFeed = Lot("Elmridge Feed & Supply", "Suburb", -300f, DEPOT_Z - 6f - 12f, 180, 8, 4, 1, Roof.Flat, new Color(0.7f, 0.55f, 0.38f), FLAT_ROOF, 0, 0f, 3f);
            Doors(lFeed, 0, 3, 0, 4, 2, 2);
            lFeed.arches.Add(new Vector2Int(0, 1));
            lFeed.windowChance = 0.2f;
            MapKit.KitSpec lFire = Lot("Elmridge Fire Station", "Suburb", -130f, DEPOT_Z - 6f - 13.5f, 180, 6, 5, 2, Roof.Flat, new Color(0.72f, 0.3f, 0.26f), FLAT_ROOF, 0, 0f, 3f);
            lFire.brick = true;
            lFire.arches.Add(new Vector2Int(0, 1));
            lFire.arches.Add(new Vector2Int(0, 2));
            Doors(lFire, 0, 4, 2, 2);
            lFire.rooms.Add(new Room[0]);
            Floors(lFire, Room.Lounge, Room.Kitchen);
            MapKit.KitSpec lDepot = Lot("Elmridge Depot", "Timber", -185f, -350f, 180, 6, 3, 1, Roof.Gable, new Color(0.78f, 0.62f, 0.42f), new Color(0.38f, 0.22f, 0.18f), 0, 0f, 3f);
            Doors(lDepot, 0, 2, 0, 3, 2, 2);
            lDepot.windowChance = 0.9f;
            Floors(lDepot, Room.Lounge, Room.None);
            MapKit.KitSpec lHall = Lot("Elmridge Meeting Hall", "Classic", -150f, -106f, 0, 5, 8, 2, Roof.Gable, new Color(0.95f, 0.94f, 0.9f), new Color(0.3f, 0.32f, 0.36f), 0, 0f, 4f);
            Doors(lHall, 0, 2, 2, 1, 1, 6);
            lHall.tallWindows = true;
            lHall.windowChance = 1f;
            lHall.balconyChance = 0f;
            lHall.stairs = false;
            MapKit.KitSpec lTower = Lot("Meeting Hall Tower", "Classic", -165f, -110f, 0, 3, 3, 4, Roof.Flat, new Color(0.95f, 0.94f, 0.9f), FLAT_ROOF, 0, 0f, 4f);
            Doors(lTower, 0, 1);
            lTower.windowChance = 0.5f;
            lTower.balconyChance = 0f;
            PaintRect(R(-180, -126, -120, -118), PAVE);
        }

        private void Downtown()
        {
            const int Z = 0;
            _glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(EditorAssets.ART_RESOURCES + "/M_ToonTransparent.mat");
            foreach (MapKit.KitSpec s in _shops) Shop(K.Building(s));
            Bank();
            Diner();
            Mall();
            Motors();
            Police();
            Fuel();
            Cinema();
            PostAndDepot();
            Hall();
            WaterTower();
            Railway();
            MainStreet();

            // The downtown ring for the cruiser: Main, Birch, Back, Mill.
            Cruiser(9f, 0f, POLICE_BLUE * 0.6f, new Vector2(MILL_X, MAIN_Z), new Vector2(BIRCH_X, MAIN_Z), new Vector2(BIRCH_X, BACK_Z), new Vector2(MILL_X, BACK_Z));
            Walk(Z, ItemKind.Pistol, 3f, new Vector2(-405, MAIN_Z + 11f), new Vector2(-90, MAIN_Z + 11f), new Vector2(-90, MAIN_Z - 11f), new Vector2(-405, MAIN_Z - 11f));
            Walk(Z, ItemKind.Smg, 4f, new Vector2(-405, ALLEY_N), new Vector2(-265, ALLEY_N), new Vector2(-265, -150), new Vector2(-405, -150));
            Walk(Z, ItemKind.Shotgun, 4f, new Vector2(-405, ALLEY_S), new Vector2(-95, ALLEY_S), new Vector2(-95, DEPOT_Z + 5f), new Vector2(-405, DEPOT_Z + 5f));
            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-250, 10, -235), new Vector3(400, 80, 320), G(-480, MAIN_Z, 0.1f), POLICE_BLUE, HatStyle.Cap, Spec("Elmridge Police").origin + Quaternion.Euler(0, 180, 0) * new Vector3(6f, 0.1f, 6f));
        }

        // ---------------------------------------------------------------- shops

        private void Shop(MapKit.KitBuilding b)
        {
            int lDoor = b.spec.width / 2;
            int lTint = Mathf.Abs(b.spec.name.GetHashCode()) % AWNINGS.Length;
            List<int> lModules = new List<int>();
            for (int m = 0; m < b.spec.width; m++) if (m != lDoor) lModules.Add(m);
            Awnings(b, 0, AWNINGS[lTint], lModules.ToArray());
            // The shop sign over the door, lit.
            K.Block(b.root, "SignBoard", Facade(b, 0, lDoor, 3.05f, 0.25f), new Vector3(2.8f, 0.6f, 0.12f), AWNINGS[(lTint + 2) % AWNINGS.Length] * 0.6f, GameLayers.DEFAULT, false, Quaternion.Euler(0, FacadeYaw(b, 0), 0));
            Neon(b.root, Facade(b, 0, lDoor, 3.05f, 0.33f), new Vector3(2.2f, 0.18f, 0.05f), new Color(2.4f, 1.6f, 0.8f), FacadeYaw(b, 0));
            // The fire escape: a landing at every upper door and a ladder from the alley.
            if (b.spec.floors > 1)
            {
                int m = b.spec.width - 1;
                for (int f = 1; f < b.spec.floors; f++)
                {
                    Vector3 lLanding = Facade(b, 2, m, f * MapKit.STOREY + 0.02f, 0.85f);
                    Deck(lLanding, new Vector2(2.8f, 1.5f), FacadeYaw(b, 2), 0.12f, new Color(0.25f, 0.25f, 0.27f), 0.3f, b.root);
                    K.Block(b.root, "EscapeRail", Facade(b, 2, m, f * MapKit.STOREY + 0.55f, 1.55f) + Quaternion.Euler(0, FacadeYaw(b, 2), 0) * Vector3.right * -0.5f, new Vector3(1.8f, 1f, 0.06f), new Color(0.2f, 0.2f, 0.22f), GameLayers.DEFAULT, true, Quaternion.Euler(0, FacadeYaw(b, 2), 0));
                }
                Vector3 lFoot = Facade(b, 2, m, 0f, 1.75f) + Quaternion.Euler(0, FacadeYaw(b, 2), 0) * Vector3.right * 0.9f;
                Ladder(new Vector3(lFoot.x, b.root.position.y, lFoot.z), (b.spec.floors - 1) * MapKit.STOREY + 1f, FacadeYaw(b, 2));
            }
            // Dumpster and bins at the back door.
            Vector3 lBack = Facade(b, 2, 1, 0f, 1.6f);
            if (Mathf.Abs(b.spec.name.GetHashCode()) % 2 == 0) K.HidingKit(lBack + Quaternion.Euler(0, FacadeYaw(b, 2), 0) * Vector3.right * -2.6f, FacadeYaw(b, 2), "Dumpster");
            else K.Prop("TrashBin", lBack + Quaternion.Euler(0, FacadeYaw(b, 2), 0) * Vector3.right * -1.8f, FacadeYaw(b, 2), null, MapKit.Collide.Box);
            K.Weapon(Mathf.Abs(b.spec.name.GetHashCode()) % 3 == 0 ? ItemKind.Smg : ItemKind.Pistol, b.Local(-b.W * 0.3f, MapKit.STOREY * (b.spec.floors - 1) + 0.15f, b.D * 0.3f));
        }

        private void Bank()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Elmridge Savings Bank"));
            // Columns and a pediment band over the doors, the vault door in the back room.
            for (int i = 0; i < 4; i++) K.Piece("Classic", "Column", Facade(b, 0, 1.5f + i * 1f, 0f, 1.6f), FacadeYaw(b, 0), STONE, b.root, MapKit.Collide.Box);
            K.Block(b.root, "Pediment", Facade(b, 0, 2.5f, 3.6f, 1.6f), new Vector3(11f, 0.5f, 3.4f), STONE, GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
            Neon(b.root, Facade(b, 0, 2.5f, 3.6f, 3.32f), new Vector3(6f, 0.3f, 0.05f), new Color(2f, 1.8f, 1.2f), FacadeYaw(b, 0));
            K.Mesh(b.root, "VaultDoor", CylMesh, b.Local(-3f, 1.3f, 0.25f), new Vector3(2.2f, 0.3f, 2.2f), new Color(0.55f, 0.58f, 0.62f), Quaternion.Euler(90, b.Yaw, 0), true).isStatic = true;
            K.Mesh(b.root, "VaultWheel", CylMesh, b.Local(-3f, 1.3f, 0.05f), new Vector3(0.8f, 0.1f, 0.8f), new Color(0.75f, 0.65f, 0.3f), Quaternion.Euler(90, b.Yaw, 0)).isStatic = true;
            K.Npc(NpcRole.Guard, b.Local(1.5f, 0.1f, -6.5f), b.Yaw + 180f, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, b.Local(-4f, 0.1f, 3f), b.Yaw + 90f, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Shotgun, b.Local(-6f, 0.15f, 5.5f));
        }

        /// <summary>Pike's Diner: the lunch rush at the counter, booths along the windows, the kitchen, the neon on the roof.</summary>
        private void Diner()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Pike's Diner"));
            Color lChrome = new Color(0.78f, 0.8f, 0.82f), lRed = new Color(0.78f, 0.15f, 0.16f);
            for (int i = 0; i < 3; i++) K.Prop("BarCounter", b.Local(-6.5f + i * 3.1f, 0, 0f), b.Yaw + 180f, lChrome, MapKit.Collide.Box, true);
            for (int i = 0; i < 7; i++) K.Prop("Stool", b.Local(-7.5f + i * 1.3f, 0, -1f), b.Yaw, lRed);
            K.Prop("BarShelf", b.Local(-4f, 0, 2f), b.Yaw + 180f, null);
            K.Prop("Fridge", b.Local(-0.5f, 0, 2.2f), b.Yaw + 180f, lChrome, MapKit.Collide.Box);
            K.DrinksOn(b.Local(-3.4f, 1.15f, 0f), b.Yaw, Z, 3);
            // Pies under glass domes on the counter.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = b.Local(-6f + i * 0.9f, 1.18f, 0.1f);
                K.Mesh(b.root, "Pie", CylMesh, p + Vector3.up * 0.04f, new Vector3(0.32f, 0.08f, 0.32f), new Color(0.85f, 0.6f, 0.3f)).isStatic = true;
                K.Mesh(b.root, "PieDome", DomeMesh, p + Vector3.up * 0.08f, new Vector3(0.38f, 0.3f, 0.38f), new Color(0.85f, 0.95f, 1f, 0.35f), null, false, _glassMaterial).isStatic = true;
            }
            // Booths along the front windows.
            for (int i = 0; i < 4; i++)
            {
                float x = -6.5f + i * 4f;
                K.Prop("Table", b.Local(x, 0, -4.35f), b.Yaw, new Color(0.9f, 0.88f, 0.84f), MapKit.Collide.Box, true);
                K.Prop("Sofa", b.Local(x, 0, -5.3f), b.Yaw, lRed, MapKit.Collide.Box);
                K.Prop("Sofa", b.Local(x, 0, -3.35f), b.Yaw + 180f, lRed, MapKit.Collide.Box);
            }
            K.DrinksOn(b.Local(-2.5f, 0.8f, -4.35f), b.Yaw + 180f, Z, 2);
            K.Activity(ActivityType.Dining, b.Local(-0.5f, 0.1f, -2.4f), b.Yaw + 180f, Z);
            K.Activity(ActivityType.Phone, b.Local(4.5f, 0.1f, -2.2f), b.Yaw, Z);
            K.DistractionKit(b.Local(2.2f, 1.15f, 0f), b.Yaw + 180f, "radio");
            // The kitchen behind, the toilet in the corner.
            K.PartitionKit(b, 0, new Vector3(-9, 0, 3), new Vector3(3, 0, 3), 1);
            K.Prop("Stove", b.Local(-7.5f, 0, 5.3f), b.Yaw + 180f, null, MapKit.Collide.Box, true);
            K.Prop("Stove", b.Local(-6.5f, 0, 5.3f), b.Yaw + 180f, null, MapKit.Collide.Box);
            K.Prop("Sink", b.Local(-5f, 0, 5.4f), b.Yaw + 180f, null, MapKit.Collide.Box);
            K.Prop("Fridge", b.Local(-1.5f, 0, 5.3f), b.Yaw + 180f, null, MapKit.Collide.Box, true);
            K.Activity(ActivityType.Inspect, b.Local(-5f, 0.1f, 4.3f), b.Yaw, Z);
            K.Gadget(ItemKind.EmeticPoison, b.Local(-2.5f, 1.0f, 5.4f));
            K.ToiletKit(b.Local(4.5f, 0, 5.5f), b.Yaw + 180f, Z);
            K.Npc(NpcRole.Guard, b.Local(-4.5f, 0.1f, -1.8f), b.Yaw, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, b.Local(1.5f, 0.1f, -2f), b.Yaw + 30f, Z, ItemKind.Pistol);
            K.Lamp(b.Local(-3f, MapKit.STOREY - 0.45f, -2f), WARM, 10f, 1.3f, false);
            // Neon on the roof edge and over the door; the parking lot west with cars and a newspaper box.
            K.Block(b.root, "SignFrame", b.Local(0, MapKit.STOREY + 1.6f, -5.6f), new Vector3(9f, 2f, 0.3f), new Color(0.2f, 0.22f, 0.3f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
            Neon(b.root, b.Local(0, MapKit.STOREY + 1.6f, -5.8f), new Vector3(7.6f, 1f, 0.05f), new Color(2.8f, 0.6f, 0.7f), b.Yaw);
            Neon(b.root, b.Local(0, MapKit.STOREY + 0.65f, -5.8f), new Vector3(8.6f, 0.08f, 0.05f), new Color(0.6f, 2.2f, 2.6f), b.Yaw);
            K.Block(b.root, "Stripe", b.Local(0, 1f, -6.25f), new Vector3(18.2f, 0.25f, 0.1f), lRed, GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
            for (int i = 0; i < 6; i++) K.Car(G(-294 + i * 5.5f, -200), 0, CAR_TINTS[(i * 3) % CAR_TINTS.Length]);
            K.Prop("TrashBin", G(-262, -214), 0, null, MapKit.Collide.Box);
            K.Activity(ActivityType.Smoke, G(-262, -194, 0.1f), 180, Z);
            K.HidingKit(G(-245, -200), 0, "Dumpster");
        }

        /// <summary>The Ridgeview Mall: two wings around a glazed atrium (fountain, stair, bridge), the car ramp and the rooftop car park.</summary>
        private void Mall()
        {
            const int Z = 0;
            MapKit.KitBuilding w = K.Building(Spec("Ridgeview Mall West"));
            MapKit.KitBuilding e = K.Building(Spec("Ridgeview Mall East"));
            float lRoof = RoofY(w.spec);
            Color lFrame = new Color(0.3f, 0.32f, 0.36f), lGlass = new Color(0.7f, 0.86f, 0.95f, 0.35f);
            Transform lAtrium = K.Group("MallAtrium", K.Env);
            // Glass fronts (a door gap in each) and the skylight.
            foreach (float z in new[] { -216f, -186f })
            {
                foreach (float x0 in new[] { -196f, -188f })
                {
                    K.Mesh(lAtrium, "Glass", MapKit.SharedBox, new Vector3(x0 + 2f, lRoof * 0.5f, z), new Vector3(4f, lRoof, 0.08f), lGlass, null, true, _glassMaterial).isStatic = true;
                    for (int k = 0; k <= 1; k++) K.Block(lAtrium, "Mullion", new Vector3(x0 + k * 4f, lRoof * 0.5f, z), new Vector3(0.2f, lRoof, 0.2f), lFrame);
                }
                K.Block(lAtrium, "Transom", new Vector3(-190f, lRoof - 2.4f, z), new Vector3(12f, 0.3f, 0.25f), lFrame);
                K.Mesh(lAtrium, "Glass", MapKit.SharedBox, new Vector3(-190f, lRoof - 1.15f, z), new Vector3(4f, 2.3f, 0.08f), lGlass, null, false, _glassMaterial).isStatic = true;
            }
            K.Mesh(lAtrium, "Skylight", MapKit.SharedBox, new Vector3(-190f, lRoof + 0.9f, -201f), new Vector3(12f, 0.08f, 30f), lGlass, null, true, _glassMaterial).isStatic = true;
            for (int i = 0; i <= 6; i++) K.Block(lAtrium, "SkyBeam", new Vector3(-190f, lRoof + 0.8f, -216f + i * 5f), new Vector3(12.4f, 0.25f, 0.25f), lFrame);
            K.Block(lAtrium, "Fascia", new Vector3(-190f, lRoof + 0.4f, -216.2f), new Vector3(12.4f, 1.2f, 0.4f), new Color(0.85f, 0.8f, 0.72f));
            Neon(lAtrium, new Vector3(-190f, lRoof + 0.45f, -216.45f), new Vector3(9f, 0.6f, 0.05f), new Color(0.6f, 2f, 2.6f));
            // Inside: the stair up to the bridge, the bridge between the wings' upper doors, fountain, planters, benches.
            K.Stairs(lAtrium, new Vector3(-190f, 0.05f, -210f), new Vector3(-190f, MapKit.STOREY + 0.05f, -201.1f), 2.6f, new Color(0.8f, 0.78f, 0.74f));
            Deck(new Vector3(-190f, MapKit.STOREY + 0.05f, -199.5f), new Vector2(12f, 3f), 0f, 0.3f, new Color(0.8f, 0.78f, 0.74f), 0f, lAtrium);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(lAtrium, "BridgeRail", new Vector3(s < 0 ? -193f : -187f, MapKit.STOREY + 0.6f, -201f), new Vector3(5.4f, 1f, 0.08f), lFrame);
                K.Block(lAtrium, "BridgeRail", new Vector3(-190f, MapKit.STOREY + 0.6f, -198f), new Vector3(12f, 1f, 0.08f), lFrame);
                K.Block(lAtrium, "StairRail", new Vector3(-190f + s * 1.35f, MapKit.STOREY * 0.5f + 0.6f, -205.5f), new Vector3(0.08f, 1f, 9.5f), lFrame, GameLayers.DEFAULT, false, Quaternion.Euler(-20.7f, 0, 0));
            }
            P("Fountain", G(-190, -192), 0, null, true);
            for (int i = 0; i < 4; i++)
            {
                P("PlantBig", G(-194.5f, -213f + i * 7f), 0, null);
                P("Bench", G(-185f, -212f + i * 6f), 270);
            }
            K.Activities(G(-192.5f, -190f, 0.1f), 0, Z, ActivityType.Idle, ActivityType.Phone);
            K.Lamp(new Vector3(-190f, lRoof - 0.6f, -206f), WARM, 14f, 1.4f, false);
            K.Lamp(new Vector3(-190f, lRoof - 0.6f, -193f), WARM, 14f, 1.4f, false);
            K.Npc(NpcRole.Guard, G(-187f, -214f, 0f), 180, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, new Vector3(-193f, MapKit.STOREY + 0.15f, -199.5f), 90, Z, ItemKind.Smg);
            // The wings: shop dressing, Darlene's office upstairs (west), guards, weapons.
            Awnings(w, 0, AWNINGS[2], 0, 1, 4, 5);
            Awnings(e, 0, AWNINGS[0], 0, 1, 4, 5);
            K.Npc(NpcRole.Guard, w.Local(-4f, MapKit.STOREY + 0.1f, -2f), 0, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, e.Local(3f, 0.1f, -6f), 180, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, w.Local(-7f, MapKit.STOREY + 0.15f, 12f));
            K.Weapon(ItemKind.Shotgun, e.Local(6f, 0.15f, 12f));
            K.Gadget(ItemKind.Coin, e.Local(-6f, 0.15f, -10f));
            for (int f = 0; f < 2; f++)
            {
                K.Sprinklers(w.Local(0, f * MapKit.STOREY, 0), new Vector3(w.W, MapKit.STOREY, w.D), w.Yaw, MapKit.STOREY - 0.05f);
                K.Sprinklers(e.Local(0, f * MapKit.STOREY, 0), new Vector3(e.W, MapKit.STOREY, e.D), e.Yaw, MapKit.STOREY - 0.05f);
            }

            // The car ramp up the east side to the rooftop car park.
            K.Ramp(K.Env, new Vector3(-161f, 0.02f, -140f), new Vector3(-161f, lRoof + 0.02f, -198f), 6f, CONCRETE, 0.5f).name = "CarRamp";
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "RampWall", new Vector3(-161f + s * 3.15f, lRoof * 0.5f + 0.5f, -169f), new Vector3(0.3f, 1f, 58.4f), CONCRETE * 0.9f, GameLayers.DEFAULT, true, Quaternion.LookRotation(new Vector3(0, lRoof, -58f)));
            for (float z = -150f; z > -195f; z -= 9f) K.Block(K.Env, "RampPier", new Vector3(-161f, (lRoof * (-140f - z) / 58f) * 0.5f, z), new Vector3(1f, lRoof * (-140f - z) / 58f, 1f), CONCRETE * 0.85f);
            Deck(new Vector3(-162.1f, lRoof + 0.02f, -201f), new Vector2(8.2f, 6f), 0f, 0.4f, CONCRETE, 0f);
            OpenParapet(e, new Vector3(-166f, lRoof, -199.5f));
            OpenParapet(e, new Vector3(-166f, lRoof, -202.5f));
            // Rooftop parking: bays, cars, lamps; a guard on the roof.
            foreach (MapKit.KitBuilding b in new[] { w, e })
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = b.Local(-4.5f + (i % 2) * 9f, MapKit.STOREY * 2f, -10f + (i / 2) * 20f);
                    if (i == 1 && b == e) continue;
                    K.Car(p, b.Yaw + (i % 2 == 0 ? 0f : 180f), CAR_TINTS[(i + (b == w ? 0 : 3)) % CAR_TINTS.Length]);
                }
                for (int k = 0; k < 4; k++) K.Block(b.root, "Bay", b.Local(-2.2f, MapKit.STOREY * 2f + 0.01f, -12f + k * 8f), new Vector3(0.12f, 0.02f, 5f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
                K.Prop("StreetLamp", b.Local(0, MapKit.STOREY * 2f, 0), b.Yaw, null, MapKit.Collide.Box);
                K.Lamp(b.Local(0, MapKit.STOREY * 2f + 4.3f, 0), SODIUM, 14f, 1.3f, false);
            }
            K.Distraction(e.Local(4.5f, MapKit.STOREY * 2f + 1.5f, 10f), 0, "caralarm");
            K.Npc(NpcRole.Lookout, w.Local(-7.5f, MapKit.STOREY * 2f + 0.1f, -13.5f), 180, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, w.Local(6f, MapKit.STOREY * 2f + 0.15f, -12.5f));
            // The loading dock behind.
            for (int i = 0; i < 3; i++) Clutter(G(-210f + i * 6f, -182f), 0, i + 1);
            K.Car(G(-178, -176), 90, new Color(0.9f, 0.9f, 0.88f));
            K.HidingKit(G(-196, -181), 0, "Dumpster");
            K.Activity(ActivityType.Smoke, G(-200, -180, 0.1f), 0, Z);
        }

        /// <summary>Malloy Motors: a car lot under bunting, the sales office, Chet's sales pitch.</summary>
        private void Motors()
        {
            const int Z = 0;
            MapKit.KitBuilding o = K.Building(Spec("Malloy Motors"));
            K.ToiletKit(o.Local(4.5f, 0, 3.6f), o.Yaw + 180f, Z);
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = G(-148f + i * 10f, -207f + r * 12f);
                    K.Car(p, r == 0 ? 0f : 180f, CAR_TINTS[(i + r * 5) % CAR_TINTS.Length]);
                    K.Block(K.Props, "PriceTag", p + new Vector3(0, 1.55f, 0), new Vector3(0.7f, 0.4f, 0.04f), new Color(1f, 0.85f, 0.2f), GameLayers.DEFAULT, false);
                }
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = G(-154f + i * 18f, -214f, 5f), c = G(-136f + i * 18f, -214f, 5f);
                if (i < 3) Bunting(a, c, i);
                K.Block(K.Env, "FlagPole", G(-154f + i * 18f, -214f, 2.6f), new Vector3(0.15f, 5.2f, 0.15f), new Color(0.85f, 0.85f, 0.88f));
                Bunting(G(-154f + i * 18f, -214f, 5f), G(-154f + i * 18f, -190f, 4f), i + 3);
            }
            K.Block(K.Env, "PoleSign", G(-156, -214, 4.5f), new Vector3(0.4f, 9f, 0.4f), new Color(0.3f, 0.3f, 0.32f));
            Neon(K.Env, G(-156, -214, 8.4f), new Vector3(4.2f, 1.6f, 0.3f), new Color(0.6f, 1.2f, 2.8f));
            K.Mesh(K.Env, "Balloon", BallMesh, G(-96, -210, 7f), new Vector3(2.6f, 3f, 2.6f), new Color(0.95f, 0.25f, 0.2f)).isStatic = true;
            K.Block(K.Env, "BalloonLine", G(-96, -210, 2.8f), new Vector3(0.03f, 5.6f, 0.03f), new Color(0.3f, 0.3f, 0.3f), GameLayers.DEFAULT, false);
            // The pitch: a coffee table with the salesmen's cups by the office, Chet smokes at the gate.
            P("TableRound", G(-112f, -188f), 0, Color.white, true);
            K.DrinksOn(G(-112f, -188f, 0.78f), 0, Z, 2);
            P("Chair", G(-113f, -189.3f), 0);
            P("Chair", G(-111f, -189.3f), 0);
            K.Activity(ActivityType.Smoke, G(-92f, -212f, 0.1f), 180, Z);
            K.Activity(ActivityType.Phone, G(-128f, -200f, 0.1f), 90, Z);
            K.Npc(NpcRole.Guard, G(-120f, -192f, 0f), 180, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, o.Local(-3f, 0.1f, 0f), 180, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Pistol, o.Local(3f, 0.95f, -0.5f));
        }

        /// <summary>The police station: the front desk and the cells, the armory, the chief's office and the briefing upstairs, the antenna on the roof, the yard.</summary>
        private void Police()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Elmridge Police"));
            float lRoof = MapKit.STOREY * 2f;
            // Ground floor: the front desk (security room) and, behind a partition, the cells and the armory.
            System.Random lRandom = new System.Random(41);
            K.PartitionKit(b, 0, new Vector3(-12, 0, 0), new Vector3(9, 0, 0), 4);
            K.Furnish(b, 0, Room.Security, new Rect(-11.5f, -8.5f, 20f, 8f), lRandom);
            K.Furnish(b, 0, Room.Storage, new Rect(0.5f, 1f, 8.5f, 7.5f), lRandom);
            for (int i = 0; i < 2; i++) K.Lamp(b.Local(-6f + i * 10f, MapKit.STOREY - 0.45f, -2f), WARM, 10f, 1.2f, false);
            for (int c = 0; c < 3; c++)
            {
                float x = -10.5f + c * 3.2f;
                for (int k = 0; k < 7; k++) K.Block(b.root, "Bar", b.Local(x - 1.2f + k * 0.4f, 1.2f, 4.5f), new Vector3(0.06f, 2.4f, 0.06f), new Color(0.3f, 0.3f, 0.32f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
                K.Block(b.root, "CellWall", b.Local(x + 1.6f, 1.2f, 6.75f), new Vector3(0.15f, 2.4f, 4.5f), new Color(0.75f, 0.75f, 0.72f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
                K.Block(b.root, "Bunk", b.Local(x, 0.45f, 8f), new Vector3(2f, 0.15f, 0.9f), new Color(0.4f, 0.45f, 0.5f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
            }
            K.UniqueWeapon(ItemKind.RocketLauncher, b.Local(7.5f, 0.2f, 7.8f), "Police armory");
            K.Weapon(ItemKind.Shotgun, b.Local(5f, 0.15f, 7.8f));
            K.Weapon(ItemKind.Smg, b.Local(3f, 0.15f, 7.8f));
            K.Npc(NpcRole.Guard, b.Local(-3f, 0.1f, -5f), b.Yaw + 180f, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, b.Local(-1f, 0.1f, 2f), b.Yaw + 270f, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, b.Local(4f, 0.1f, 3f), b.Yaw + 90f, Z, ItemKind.Smg);
            // Upstairs: the briefing table, the chief's guard.
            K.Prop("Table", b.Local(2f, MapKit.STOREY, 4f), b.Yaw, new Color(0.45f, 0.3f, 0.22f), MapKit.Collide.Box, true);
            K.Prop("Table", b.Local(3.6f, MapKit.STOREY, 4f), b.Yaw, new Color(0.45f, 0.3f, 0.22f), MapKit.Collide.Box);
            for (int i = 0; i < 4; i++) K.Prop("Chair", b.Local(1.2f + i * 1.1f, MapKit.STOREY, 4.9f), b.Yaw + 180f, null);
            K.DrinksOn(b.Local(2.8f, MapKit.STOREY + 0.8f, 4f), b.Yaw, Z, 2);
            K.Block(b.root, "Whiteboard", b.Local(2.8f, MapKit.STOREY + 1.6f, 8.6f), new Vector3(3f, 1.4f, 0.08f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
            K.Npc(NpcRole.Guard, b.Local(-8f, MapKit.STOREY + 0.1f, 1.5f), b.Yaw, Z, ItemKind.Smg);
            K.Gadget(ItemKind.SmokeGrenade, b.Local(8f, MapKit.STOREY + 0.15f, -6f));
            for (int f = 0; f < 2; f++) K.Sprinklers(b.Local(0, f * MapKit.STOREY, 0), new Vector3(b.W, MapKit.STOREY, b.D), b.Yaw, MapKit.STOREY - 0.05f);
            // Roof: the radio antenna (a lattice mast with a red light) and its dish, a lookout.
            Vector3 lMast = b.Local(-7f, lRoof, 6f);
            for (int k = 0; k < 4; k++) K.Block(b.root, "MastLeg", lMast + new Vector3(k % 2 == 0 ? -0.4f : 0.4f, 7f, k < 2 ? -0.4f : 0.4f), new Vector3(0.12f, 14f, 0.12f), new Color(0.75f, 0.75f, 0.78f));
            for (int k = 1; k < 7; k++) K.Block(b.root, "MastRung", lMast + Vector3.up * (k * 2f), new Vector3(0.9f, 0.08f, 0.9f), new Color(0.75f, 0.75f, 0.78f), GameLayers.DEFAULT, false);
            K.Lamp(lMast + Vector3.up * 14.3f, new Color(1f, 0.15f, 0.1f), 8f, 2f, false);
            K.Mesh(b.root, "Dish", DomeMesh, b.Local(4f, lRoof + 1.6f, 6f), new Vector3(2f, 0.6f, 2f), new Color(0.88f, 0.88f, 0.9f), Quaternion.Euler(-60f, b.Yaw, 0)).isStatic = true;
            K.Block(b.root, "DishPost", b.Local(4f, lRoof + 0.8f, 6f), new Vector3(0.2f, 1.6f, 0.2f), new Color(0.6f, 0.6f, 0.62f));
            K.Npc(NpcRole.Lookout, b.Local(2f, lRoof + 0.1f, -7f), b.Yaw + 180f, Z, ItemKind.Sniper);
            K.Activity(ActivityType.Lookout, b.Local(-2f, lRoof + 0.1f, -7.5f), b.Yaw + 180f, Z);
            K.Block(b.root, "Sign", Facade(b, 0, 3.5f, MapKit.STOREY * 2f + 0.5f, 0.3f), new Vector3(8f, 0.9f, 0.15f), POLICE_BLUE, GameLayers.DEFAULT, false, Quaternion.Euler(0, FacadeYaw(b, 0), 0));
            Neon(b.root, Facade(b, 0, 3.5f, MapKit.STOREY * 2f + 0.5f, 0.4f), new Vector3(6.5f, 0.35f, 0.05f), new Color(2.4f, 2.4f, 2.6f), FacadeYaw(b, 0));
            // The lot: patrol cars, the flagpole, the smokers by the back door; the yard behind.
            for (int i = 0; i < 4; i++) P("PoliceCar", -380f + i * 5.2f, -258f, 0, null, true);
            K.Block(K.Env, "FlagPole", G(-366, -246, 4.5f), new Vector3(0.15f, 9f, 0.15f), Color.white);
            K.Block(K.Env, "Flag", G(-366, -246, 8.2f) + new Vector3(0.9f, 0, 0), new Vector3(1.8f, 1.1f, 0.03f), POLICE_BLUE);
            K.Activity(ActivityType.Smoke, G(-392, -266, 0.1f), 180, Z);
            Guard(Z, -372f, -248f, 0, ItemKind.Shotgun, "Barrier");
        }

        /// <summary>Sunset Fuel: the canopy over the pumps, the shop (flamethrower in its stock room), gas bottles and barrels.</summary>
        private void Fuel()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Sunset Fuel"));
            Vector3 c = G(-264, -256);
            Color lOrange = new Color(0.92f, 0.45f, 0.15f);
            K.Block(K.Env, "Canopy", c + Vector3.up * 5f, new Vector3(20f, 0.6f, 10f), Color.white);
            K.Block(K.Env, "CanopyBand", c + Vector3.up * 5.35f, new Vector3(20.2f, 0.5f, 10.2f), lOrange, GameLayers.DEFAULT, false);
            Neon(K.Env, c + new Vector3(0, 4.62f, 0), new Vector3(18f, 0.06f, 8f), new Color(2.4f, 2.3f, 2.1f));
            for (int i = 0; i < 4; i++) K.Block(K.Env, "Pillar", c + new Vector3(i % 2 == 0 ? -7f : 7f, 2.5f, i < 2 ? -2.5f : 2.5f), new Vector3(0.5f, 5f, 0.5f), new Color(0.9f, 0.9f, 0.9f));
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = c + new Vector3(-6f + i * 6f, 0, 0);
                K.Block(K.Env, "Island", p + Vector3.up * 0.1f, new Vector3(1.2f, 0.2f, 4f), CONCRETE);
                K.Block(K.Env, "Pump", p + new Vector3(0, 0.9f, -0.9f), new Vector3(0.7f, 1.6f, 0.5f), lOrange, GameLayers.DEFAULT, true, null, true);
                K.Block(K.Env, "Pump", p + new Vector3(0, 0.9f, 0.9f), new Vector3(0.7f, 1.6f, 0.5f), lOrange, GameLayers.DEFAULT, true);
            }
            K.Car(c + new Vector3(-3f, 0, -0.5f), 90, CAR_TINTS[6]);
            for (int i = 0; i < 4; i++) GasBottle(G(-304f + i * 0.45f, -253f));
            K.Barrel(G(-305, -256), true);
            K.Block(K.Env, "PricePole", G(-246, -246, 4f), new Vector3(0.4f, 8f, 0.4f), new Color(0.3f, 0.3f, 0.32f));
            Neon(K.Env, G(-246, -246, 7f), new Vector3(2.6f, 2.4f, 0.3f), new Color(2.8f, 1.2f, 0.4f));
            K.UniqueWeapon(ItemKind.Flamethrower, b.Local(-4f, 0.15f, 3f), "Gas station stock room");
            K.Npc(NpcRole.Guard, c + new Vector3(8f, 0.1f, 3f), 180, Z, ItemKind.Pistol);
            K.DistractionKit(c + new Vector3(9f, 0, -3.5f), 180, "radio");
            K.Gadget(ItemKind.Brick, G(-280, -270, 0.1f));
        }

        /// <summary>The Starlight Cinema: lobby with the concession stand, a double-height auditorium, the projection booth over it, marquee and blade sign, a sniper roof.</summary>
        private void Cinema()
        {
            const int Z = 0;
            MapKit.KitBuilding b = K.Building(Spec("Starlight Cinema"));
            // Double height: the auditorium has no first-floor slab except the gallery along the stair column.
            CutSlab(b, 1, Rect.MinMaxRect(-10.6f, -3f, 7.1f, 12.2f));
            float W = b.W;
            K.PartitionKit(b, 0, new Vector3(-10.5f, 0, -3), new Vector3(-1.5f, 0, -3), 1);
            K.PartitionKit(b, 0, new Vector3(-1.5f, 0, -3), new Vector3(7.5f, 0, -3), 1);
            // The booth wall upstairs: windows onto the screen.
            for (int m = 0; m < 6; m++) K.Piece("Suburb", "WallWindow", b.Local(-9f + m * 3f, MapKit.STOREY, -3f), b.Yaw, new Color(0.3f, 0.2f, 0.22f), b.root);
            K.Block(b.root, "Screen", b.Local(-1.7f, 3.6f, 11.65f), new Vector3(14f, 4.6f, 0.15f), new Color(0.2f, 0.18f, 0.2f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
            Neon(b.root, b.Local(-1.7f, 3.6f, 11.55f), new Vector3(13.4f, 4.2f, 0.04f), new Color(1.1f, 1.1f, 1.25f), b.Yaw);
            for (int s = -1; s <= 1; s += 2) K.Block(b.root, "Curtain", b.Local(-1.7f + s * 7.6f, 3.2f, 11.4f), new Vector3(1.2f, 6.2f, 0.3f), new Color(0.55f, 0.08f, 0.1f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
            for (int r = 0; r < 8; r++)
            {
                float z = 7.8f - r * 1.3f, y = r * 0.12f;
                K.Block(b.root, "SeatRow", b.Local(-2.3f, 0.25f + y, z), new Vector3(13f, 0.5f, 0.7f), new Color(0.55f, 0.1f, 0.14f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
                K.Block(b.root, "SeatBack", b.Local(-2.3f, 0.75f + y, z - 0.38f), new Vector3(13f, 1f, 0.12f), new Color(0.45f, 0.08f, 0.12f), GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
            }
            K.Lamp(b.Local(-2f, 6f, 4f), new Color(0.6f, 0.65f, 1f), 14f, 0.9f, false);
            K.Activity(ActivityType.Idle, b.Local(5.2f, 0.1f, 1f), b.Yaw + 180f, Z);
            // Lobby: concession stand, posters, toilets.
            K.Prop("BarCounter", b.Local(-6f, 0, -6f), b.Yaw + 180f, new Color(0.75f, 0.15f, 0.16f), MapKit.Collide.Box, true);
            K.Block(b.root, "Popcorn", b.Local(-6f, 1.6f, -5.6f), new Vector3(0.8f, 0.9f, 0.6f), new Color(1f, 0.9f, 0.6f), GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
            K.DrinksOn(b.Local(-5.5f, 1.15f, -6f), b.Yaw, Z, 3);
            for (int i = 0; i < 4; i++) K.Block(b.root, "Poster", b.Local(-9f + i * 5f, 1.6f, -11.7f), new Vector3(1.2f, 1.8f, 0.05f), AWNINGS[i % AWNINGS.Length], GameLayers.DEFAULT, false, Quaternion.Euler(0, b.Yaw, 0));
            K.ToiletKit(b.Local(-9.5f, 0, -4f), b.Yaw + 90f, Z);
            K.ToiletKit(b.Local(-9.5f, 0, -5.5f), b.Yaw + 90f, Z);
            K.Npc(NpcRole.Guard, b.Local(1f, 0.1f, -8f), b.Yaw + 180f, Z, ItemKind.Pistol);
            // The projection booth: the projector, reels, Mickey's chair.
            K.Block(b.root, "Projector", b.Local(-4f, MapKit.STOREY + 1.1f, -4.2f), new Vector3(0.7f, 0.7f, 1.2f), new Color(0.25f, 0.25f, 0.28f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
            K.Block(b.root, "ProjectorStand", b.Local(-4f, MapKit.STOREY + 0.4f, -4.2f), new Vector3(0.5f, 0.8f, 0.5f), new Color(0.3f, 0.3f, 0.32f), GameLayers.DEFAULT, true, Quaternion.Euler(0, b.Yaw, 0));
            for (int i = 0; i < 2; i++) K.Mesh(b.root, "Reel", CylMesh, b.Local(-4f, MapKit.STOREY + 1.75f, -4.6f + i * 0.8f), new Vector3(0.7f, 0.08f, 0.7f), new Color(0.6f, 0.6f, 0.62f), Quaternion.Euler(0, b.Yaw, 90)).isStatic = true;
            K.Activity(ActivityType.Inspect, b.Local(-4f, MapKit.STOREY + 0.1f, -5.6f), b.Yaw, Z);
            K.Prop("OfficeChair", b.Local(-2f, MapKit.STOREY, -6f), b.Yaw, null);
            for (int i = 0; i < 3; i++) K.Prop("Cabinet", b.Local(-9f + i * 1.3f, MapKit.STOREY, -11f), b.Yaw + 180f, null);
            K.Npc(NpcRole.Guard, b.Local(2f, MapKit.STOREY + 0.1f, -7f), b.Yaw, Z, ItemKind.Smg);
            K.Gadget(ItemKind.Poison, b.Local(-7f, MapKit.STOREY + 0.95f, -11f));
            for (int f = 0; f < 2; f++) K.Sprinklers(b.Local(0, f * MapKit.STOREY, 0), new Vector3(b.W, MapKit.STOREY, b.D), b.Yaw, MapKit.STOREY - 0.05f);
            // Marquee over the doors, the blade sign above the roof, the back ladder, the roof nest.
            Vector3 lMarquee = Facade(b, 0, 3f, 3.9f, 1.6f);
            Quaternion r = Quaternion.Euler(0, FacadeYaw(b, 0), 0);
            K.Block(b.root, "Marquee", lMarquee, new Vector3(13f, 1.5f, 3.2f), new Color(0.2f, 0.16f, 0.2f), GameLayers.DEFAULT, true, r);
            Neon(b.root, lMarquee + r * new Vector3(0, 0, 1.62f), new Vector3(12.2f, 1f, 0.04f), new Color(2.6f, 2.4f, 2f), FacadeYaw(b, 0));
            Neon(b.root, lMarquee + r * new Vector3(0, -0.78f, 0), new Vector3(13.1f, 0.06f, 3.3f), new Color(2.8f, 2f, 0.8f), FacadeYaw(b, 0));
            Neon(b.root, lMarquee + r * new Vector3(0, 0.78f, 0), new Vector3(13.1f, 0.06f, 3.3f), new Color(2.8f, 2f, 0.8f), FacadeYaw(b, 0));
            Vector3 lBlade = Facade(b, 0, 6.2f, 8f, 1.2f);
            K.Block(b.root, "BladeSign", lBlade, new Vector3(0.5f, 8f, 2f), new Color(0.2f, 0.16f, 0.2f), GameLayers.DEFAULT, true, r);
            for (int s = -1; s <= 1; s += 2) Neon(b.root, lBlade + r * new Vector3(s * 0.27f, 0, 0), new Vector3(0.04f, 7.2f, 1.5f), new Color(2.8f, 0.5f, 0.6f), FacadeYaw(b, 0));
            Ladder(Facade(b, 2, 1, 0f, 0.35f), MapKit.STOREY * 2f + 1f, FacadeYaw(b, 2));
            K.Npc(NpcRole.Lookout, b.Local(-6f, MapKit.STOREY * 2f + 0.1f, -9f), b.Yaw + 180f, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, b.Local(-8f, MapKit.STOREY * 2f + 0.15f, -10f));
            K.Activity(ActivityType.Smoke, Facade(b, 2, 3f, 0.1f, 2f), FacadeYaw(b, 2), Z);
        }

        /// <summary>The post office, the feed store, the fire station, the railway depot and its plaza.</summary>
        private void PostAndDepot()
        {
            const int Z = 0;
            K.Building(Spec("Elmridge Post Office"));
            MapKit.KitBuilding f = K.Building(Spec("Elmridge Feed & Supply"));
            for (int i = 0; i < 4; i++) Clutter(f.Local(-9f + i * 5f, 0, 4f), f.Yaw, i);
            K.Prop("Haystack", f.Local(-9f, 0, -7.6f), f.Yaw);
            K.Lamp(f.Local(0, MapKit.STOREY - 0.45f, 0), WARM, 12f, 1.2f, false);
            K.HidingKit(Facade(f, 0, 6.5f, 0f, 2f), FacadeYaw(f, 0), "Haystack");
            K.Car(Facade(f, 0, 1f, 0f, 4f), FacadeYaw(f, 0) + 90f, new Color(0.55f, 0.3f, 0.2f));
            MapKit.KitBuilding s = K.Building(Spec("Elmridge Fire Station"));
            // The fire engine in the bays, hoses, the crew room upstairs.
            Vector3 lTruck = s.Local(-4.5f, 0, -3f);
            Quaternion lRot = Quaternion.Euler(0, s.Yaw, 0);
            K.Block(s.root, "Engine", lTruck + Vector3.up * 1.3f, new Vector3(2.5f, 2f, 7.5f), new Color(0.8f, 0.12f, 0.1f), GameLayers.DEFAULT, true, lRot, true);
            K.Block(s.root, "EngineCab", lTruck + lRot * new Vector3(0, 2.6f, -2.6f), new Vector3(2.4f, 0.9f, 2.2f), new Color(0.8f, 0.12f, 0.1f), GameLayers.DEFAULT, false, lRot);
            K.Block(s.root, "EngineLadder", lTruck + lRot * new Vector3(0, 2.6f, 1f), new Vector3(0.8f, 0.3f, 6f), new Color(0.8f, 0.8f, 0.82f), GameLayers.DEFAULT, false, lRot);
            for (int w = 0; w < 6; w++) K.Mesh(s.root, "Wheel", CylMesh, lTruck + lRot * new Vector3(w % 2 == 0 ? -1.25f : 1.25f, 0.5f, -2.6f + (w / 2) * 2.4f), new Vector3(1f, 0.3f, 1f), new Color(0.12f, 0.12f, 0.13f), lRot * Quaternion.Euler(0, 0, 90)).isStatic = true;
            K.Npc(NpcRole.Guard, s.Local(3f, 0.1f, -4f), s.Yaw + 180f, Z, ItemKind.Smg);
            K.Gadget(ItemKind.SmokeGrenade, s.Local(5f, 0.15f, 4f));
            // The depot: the plaza, the bus stop, the parked cars; platform and train in Railway().
            K.Building(Spec("Elmridge Depot"));
            for (int i = 0; i < 5; i++) K.Car(G(-252f + i * 6f, -318f), 0, CAR_TINTS[(i * 2 + 1) % CAR_TINTS.Length]);
            K.Block(K.Env, "BusShelter", G(-220, -312, 2.4f), new Vector3(5f, 0.2f, 2f), new Color(0.3f, 0.45f, 0.55f));
            for (int i = 0; i < 2; i++) K.Block(K.Env, "ShelterPost", G(-222.4f + i * 4.8f, -311.2f, 1.2f), new Vector3(0.12f, 2.4f, 0.12f), new Color(0.3f, 0.3f, 0.32f));
            P("Bench", -220, -312.6f, 0);
            K.Activity(ActivityType.Phone, G(-217, -314, 0.1f), 0, Z);
        }

        /// <summary>The meeting hall on Back Street: pews, the lectern, the evening meeting (Reverend Doyle's speech); its tower is a lookout.</summary>
        private void Hall()
        {
            const int Z = 0;
            MapKit.KitBuilding h = K.Building(Spec("Elmridge Meeting Hall"));
            // A tall nave: no first-floor slab at all.
            CutSlab(h, 1, Rect.MinMaxRect(-7.7f, -12.2f, 7.7f, 12.2f));
            for (int r = 0; r < 6; r++)
                for (int s = -1; s <= 1; s += 2)
                    K.Prop("Bench", h.Local(s * 3.2f - 1.2f, 0, -8f + r * 2f), h.Yaw, new Color(0.5f, 0.34f, 0.22f), MapKit.Collide.Box);
            K.Block(h.root, "Stage", h.Local(-1.5f, 0.25f, 5f), new Vector3(9f, 0.5f, 3f), new Color(0.5f, 0.34f, 0.22f), GameLayers.DEFAULT, true, Quaternion.Euler(0, h.Yaw, 0));
            K.Prop("Desk", h.Local(-1.5f, 0.5f, 5f), h.Yaw + 180f, new Color(0.45f, 0.3f, 0.2f), MapKit.Collide.Box);
            K.Activity(ActivityType.Speech, h.Local(-1.5f, 0.6f, 5.9f), h.Yaw + 180f, Z, 18f);
            K.Block(h.root, "Banner", h.Local(-1.5f, 3.6f, 11.6f), new Vector3(6f, 3f, 0.05f), new Color(0.2f, 0.32f, 0.5f), GameLayers.DEFAULT, false, Quaternion.Euler(0, h.Yaw, 0));
            K.Prop("Piano", h.Local(-6f, 0, 4.5f), h.Yaw + 160f, null, MapKit.Collide.Box, true);
            K.Prop("Table", h.Local(-6f, 0, -10.5f), h.Yaw, Color.white, MapKit.Collide.Box, true);
            K.DrinksOn(h.Local(-6f, 0.8f, -10.5f), h.Yaw + 180f, Z, 3);
            K.ToiletKit(h.Local(6f, 0, 10.5f), h.Yaw + 180f, Z);
            K.Activity(ActivityType.Phone, h.Local(5.5f, 0.1f, 7f), h.Yaw + 270f, Z);
            K.Npc(NpcRole.Guard, h.Local(4f, 0.1f, -10f), h.Yaw + 180f, Z, ItemKind.Pistol);
            K.Npc(NpcRole.Guard, h.Local(-4f, 0.1f, 8f), h.Yaw + 180f, Z, ItemKind.Smg);
            for (int i = 0; i < 3; i++) K.Lamp(h.Local(-1.5f, 6f, -8f + i * 7f), WARM, 12f, 1.3f, false);
            MapKit.KitBuilding t = K.Building(Spec("Meeting Hall Tower"));
            float lTop = RoofY(t.spec);
            for (int i = 0; i < 4; i++) K.Piece("Classic", "Column", new Vector3(t.root.position.x + (i % 2 == 0 ? -3.6f : 3.6f), lTop, t.root.position.z + (i < 2 ? -3.6f : 3.6f)), 0, null, t.root, MapKit.Collide.Box);
            GameObject lCap = K.Piece("Classic", "RoofHip", new Vector3(t.root.position.x, lTop + MapKit.STOREY, t.root.position.z), 0, new Color(0.3f, 0.32f, 0.36f), t.root, MapKit.Collide.None);
            if (lCap != null) lCap.transform.localScale = new Vector3(9.4f, 4f, 9.4f);
            K.Mesh(t.root, "Bell", DomeMesh, new Vector3(t.root.position.x, lTop + 2.4f, t.root.position.z), new Vector3(1.4f, 1.6f, 1.4f), new Color(0.75f, 0.6f, 0.3f)).isStatic = true;
            K.Npc(NpcRole.Lookout, new Vector3(t.root.position.x + 2f, lTop + 0.1f, t.root.position.z - 2f), 180, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Smg, new Vector3(t.root.position.x - 2.5f, lTop + 0.15f, t.root.position.z + 2.5f));
            for (int i = 0; i < 4; i++) K.Car(G(-176f + i * 5.5f, -122f), 90, CAR_TINTS[(i + 4) % CAR_TINTS.Length]);
        }

        /// <summary>The Elmridge water tower: four legs, the catwalk (sniper nest) under the tank, the radio mast on the roof.</summary>
        private void WaterTower()
        {
            const int Z = 0;
            Vector3 lBase = G(-372, -326);
            const float DECK = 26f;
            Color lSteel = new Color(0.62f, 0.64f, 0.66f), lTank = new Color(0.86f, 0.86f, 0.84f);
            Transform lTower = K.Group("WaterTower", K.Env);
            for (int i = 0; i < 4; i++)
            {
                Vector3 lLeg = lBase + new Vector3(i % 2 == 0 ? -4f : 4f, 0, i < 2 ? -4f : 4f);
                K.Mesh(lTower, "Leg", CylMesh, lLeg + Vector3.up * DECK * 0.5f, new Vector3(0.7f, DECK, 0.7f), lSteel, null, true).isStatic = true;
                K.Block(lTower, "Footing", lLeg + Vector3.up * 0.3f, new Vector3(1.6f, 0.6f, 1.6f), CONCRETE);
            }
            for (int k = 1; k < 4; k++)
                for (int s = 0; s < 4; s++)
                    K.Block(lTower, "Brace", lBase + Vector3.up * (k * 6.5f) + Quaternion.Euler(0, s * 90f, 0) * new Vector3(0, 0, 4f), new Vector3(8.2f, 0.25f, 0.25f), lSteel * 0.9f, GameLayers.DEFAULT, false, Quaternion.Euler(0, s * 90f, 0));
            K.Mesh(lTower, "Catwalk", CylMesh, lBase + Vector3.up * (DECK - 0.15f), new Vector3(14f, 0.3f, 14f), lSteel * 0.8f, null, true).isStatic = true;
            for (int s = 0; s < 8; s++)
            {
                Quaternion r = Quaternion.Euler(0, s * 45f + 22.5f, 0);
                if (s == 4) continue;
                K.Block(lTower, "CatwalkRail", lBase + Vector3.up * (DECK + 0.55f) + r * new Vector3(0, 0, 6.85f), new Vector3(5.6f, 1.1f, 0.08f), lSteel, GameLayers.DEFAULT, true, r);
            }
            K.Mesh(lTower, "Tank", CylMesh, lBase + Vector3.up * (DECK + 4.5f), new Vector3(10f, 9f, 10f), lTank, null, true).isStatic = true;
            K.Mesh(lTower, "TankBand", CylMesh, lBase + Vector3.up * (DECK + 5.2f), new Vector3(10.1f, 1.8f, 10.1f), new Color(0.2f, 0.36f, 0.58f)).isStatic = true;
            K.Mesh(lTower, "TankRoof", ConeMesh, lBase + Vector3.up * (DECK + 10.3f), new Vector3(10.8f, 2.6f, 10.8f), new Color(0.5f, 0.52f, 0.55f)).isStatic = true;
            K.Block(lTower, "Mast", lBase + Vector3.up * (DECK + 16f), new Vector3(0.3f, 10f, 0.3f), lSteel);
            K.Lamp(lBase + Vector3.up * (DECK + 21.2f), new Color(1f, 0.15f, 0.1f), 10f, 2.2f, false);
            for (int i = 0; i < 4; i++) K.Lamp(lBase + Quaternion.Euler(0, i * 90f + 45f, 0) * new Vector3(0, 0, 6.2f) + Vector3.up * (DECK + 0.4f), WARM, 6f, 1f, false);
            Ladder(lBase + new Vector3(0, 0, -4.35f), DECK + 0.8f, 180f);
            K.Npc(NpcRole.Lookout, lBase + new Vector3(4.5f, DECK + 0.1f, -4.5f), 135, Z, ItemKind.Sniper);
            K.Activity(ActivityType.Lookout, lBase + new Vector3(-4.5f, DECK + 0.1f, 4.5f), 315, Z);
            K.UniqueWeapon(ItemKind.RocketLauncher, lBase + new Vector3(-4.6f, DECK + 0.2f, -4.6f), "Water tower catwalk");
            K.Fence(G(-382, -336), G(-362, -336), 2.2f, new Color(0.5f, 0.5f, 0.52f));
            K.Fence(G(-382, -316), G(-362, -316), 2.2f, new Color(0.5f, 0.5f, 0.52f));
        }

        /// <summary>The railway: ballast, sleepers in chunked meshes, rails, the level crossing on Ash Avenue, the platform and the train (extraction), the west tunnel.</summary>
        private void Railway()
        {
            const int Z = 0;
            Color lRail = new Color(0.45f, 0.42f, 0.4f), lSleeper = new Color(0.36f, 0.29f, 0.22f);
            const float X0 = -496f, X1 = 440f;
            for (float x = X0; x < X1; x += 48f)
            {
                List<(Mesh, Matrix4x4, Color)> lParts = new List<(Mesh, Matrix4x4, Color)>();
                for (float s = 0.6f; s < Mathf.Min(48f, X1 - x); s += 1.2f) lParts.Add((LowPolyMesh.Box(), Matrix4x4.TRS(new Vector3(s, 0.08f, 0), Quaternion.identity, new Vector3(0.3f, 0.16f, 2.6f)), lSleeper));
                Mesh lMesh = EditorAssets.SaveMesh(LowPolyMesh.Combine(lParts), "Suburb_Sleepers48");
                GameObject lChunk = K.Mesh(K.Env, "Sleepers", lMesh, new Vector3(x, Surf(x + 24f, RAIL_Z), RAIL_Z), Vector3.one, Color.white);
                lChunk.isStatic = true;
            }
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Rail", new Vector3((X0 + X1) * 0.5f, 0.22f, RAIL_Z + s * 0.72f), new Vector3(X1 - X0, 0.12f, 0.1f), lRail, GameLayers.DEFAULT, false);
            K.Block(K.Env, "Bumper", G(X1 + 0.5f, RAIL_Z, 0.6f), new Vector3(0.6f, 1.2f, 2.6f), new Color(0.85f, 0.2f, 0.15f));
            // The level crossing on Ash Avenue: planks, barriers, signals with lights.
            Deck(G(ASH_X, RAIL_Z, 0.12f), new Vector2(10f, 4f), 90f, 0.2f, new Color(0.4f, 0.36f, 0.3f), 0.8f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = G(ASH_X + s * 6.5f, RAIL_Z + s * 4.5f);
                K.Block(K.Env, "SignalPost", p + Vector3.up * 1.8f, new Vector3(0.2f, 3.6f, 0.2f), Color.white);
                K.Block(K.Env, "Crossbuck", p + Vector3.up * 3.3f, new Vector3(1.5f, 0.25f, 0.05f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(0, 0, 35f));
                K.Block(K.Env, "Crossbuck", p + Vector3.up * 3.3f, new Vector3(1.5f, 0.25f, 0.05f), Color.white, GameLayers.DEFAULT, false, Quaternion.Euler(0, 0, -35f));
                K.Lamp(p + Vector3.up * 2.7f + new Vector3(0.3f, 0, 0), new Color(1f, 0.15f, 0.1f), 5f, 1.2f, false);
                K.Lamp(p + Vector3.up * 2.7f + new Vector3(-0.3f, 0, 0), new Color(1f, 0.15f, 0.1f), 5f, 1.2f, false);
                K.Block(K.Env, "BarrierArm", p + new Vector3(-s * 3f, 1f, 0), new Vector3(6f, 0.12f, 0.12f), new Color(0.95f, 0.95f, 0.95f), GameLayers.DEFAULT, false);
            }
            // The platform and the train standing at it (carriages, locomotive): the extraction.
            Deck(new Vector3(-186f, 0.95f, -363.5f), new Vector2(72f, 12.5f), 0f, 1f, CONCRETE, 0f);
            K.Block(K.Env, "PlatformEdge", new Vector3(-186f, 0.97f, -369.6f), new Vector3(72f, 0.04f, 0.4f), new Color(0.95f, 0.8f, 0.2f), GameLayers.DEFAULT, false);
            K.Stairs(K.Env, new Vector3(-224.5f, 0.02f, -363.5f), new Vector3(-221.9f, 0.95f, -363.5f), 4f, CONCRETE);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(-212f + i * 16f, 0.95f, -362f);
                K.Prop("Bench", p, 180);
                K.Prop("LampPostOrnate", p + new Vector3(3f, 0, 2f), 0);
                K.Lamp(p + new Vector3(3f, 4.1f, 2f), WARM, 12f, 1.3f, false);
            }
            Transform lTrain = K.Group("Train", K.Env);
            Color[] lCars = { new Color(0.2f, 0.35f, 0.3f), new Color(0.65f, 0.62f, 0.55f), new Color(0.65f, 0.62f, 0.55f) };
            for (int c = 0; c < 3; c++)
            {
                float x = -210f + c * 21f;
                K.Block(lTrain, "Carriage", new Vector3(x, 2.4f, RAIL_Z), new Vector3(19.5f, 3.2f, 3f), lCars[c], GameLayers.DEFAULT, true, null, true);
                K.Block(lTrain, "Roof", new Vector3(x, 4.15f, RAIL_Z), new Vector3(19.5f, 0.3f, 2.8f), lCars[c] * 0.7f);
                K.Block(lTrain, "Under", new Vector3(x, 0.6f, RAIL_Z), new Vector3(17f, 0.6f, 2.4f), new Color(0.15f, 0.15f, 0.16f), GameLayers.DEFAULT, false);
                for (int w = 0; w < 6; w++) Neon(lTrain, new Vector3(x - 7.5f + w * 3f, 2.9f, RAIL_Z + 1.52f), new Vector3(1.8f, 0.9f, 0.04f), new Color(1.8f, 1.5f, 1f));
                if (c == 0) K.Block(lTrain, "Cab", new Vector3(x - 9.2f, 2.6f, RAIL_Z), new Vector3(1.2f, 3.6f, 3.1f), new Color(0.85f, 0.65f, 0.15f));
            }
            K.Point(MatchPoint.Kind.Extraction, new Vector3(-189f, 1.05f, -366f), 180, "Elmridge Depot train");
            Guard(Z, -170f, -362f, 180, ItemKind.Smg);
            K.Npc(NpcRole.Guard, new Vector3(-200f, 1.05f, -360f), 0, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Pistol, new Vector3(-160f, 1.1f, -358f));
            // The west tunnel portal into the wooded rise.
            Vector3 lPortal = new Vector3(-490f, 0f, RAIL_Z);
            for (int i = -1; i <= 1; i++) K.Piece("Classic", "WallArch", lPortal + new Vector3(0, 0, i * 3f), 90, STONE * 0.85f, K.Env);
            K.Block(K.Env, "PortalTop", lPortal + new Vector3(-1f, 6f, 0), new Vector3(3f, 5f, 12f), STONE * 0.8f);
            K.Block(K.Env, "TunnelDark", lPortal + new Vector3(-4f, 2f, 0), new Vector3(6f, 4.2f, 9f), new Color(0.05f, 0.05f, 0.06f));
        }

        /// <summary>Main Street dressing: lamps, hydrants, benches, bins, mailboxes, signs, parked cars, power poles; the alleys' dumpsters.</summary>
        private void MainStreet()
        {
            const int Z = 0;
            int k = 0;
            for (float x = -430f; x < -80f; x += 20f, k++)
            {
                foreach (int s in new[] { -1, 1 })
                {
                    float z = MAIN_Z + s * 9.5f;
                    if (Mathf.Abs(x - ASH_X) < 10f) continue;
                    if (k % 2 == 0) { P("StreetLamp", x, z, s > 0 ? 180f : 0f); K.Lamp(G(x, z, 4.3f), SODIUM, 15f, 1.6f, false); }
                    else if (k % 4 == 1) P("Bench", x, z + s * 1f, s > 0 ? 180f : 0f);
                    else P("TrashBin", x, z, 0, null, false, MapKit.Collide.Box);
                    if (k % 5 == 2) P("Hydrant", x + 3f, z, 0, new Color(0.85f, 0.15f, 0.12f));
                    if (k % 6 == 3) P("Mailbox", x + 5f, z, s > 0 ? 180f : 0f, POLICE_BLUE);
                    if (k % 3 != 0 && Mathf.Abs(x + 160f) > 30f) K.Car(G(x + 8f, MAIN_Z + s * 5.2f), s > 0 ? 270f : 90f, CAR_TINTS[(k + (s > 0 ? 2 : 5)) % CAR_TINTS.Length]);
                }
            }
            foreach (Vector2 p in new[] { new Vector2(ASH_X - 8f, MAIN_Z - 8f), new Vector2(ASH_X + 8f, MAIN_Z + 8f), new Vector2(BIRCH_X - 7f, MAIN_Z + 8f), new Vector2(MILL_X + 7f, MAIN_Z - 8f) })
                P("StreetSign", p.x, p.y, 45f);
            // Traffic lights at the Ash crossing.
            for (int s = 0; s < 4; s++)
            {
                Vector3 p = G(ASH_X + (s % 2 == 0 ? -8f : 8f), MAIN_Z + (s < 2 ? -8f : 8f));
                K.Block(K.Env, "SignalPole", p + Vector3.up * 2.6f, new Vector3(0.18f, 5.2f, 0.18f), new Color(0.25f, 0.25f, 0.27f));
                K.Block(K.Env, "SignalHead", p + Vector3.up * 4.6f, new Vector3(0.4f, 1.1f, 0.4f), new Color(0.15f, 0.15f, 0.12f));
                Neon(K.Env, p + Vector3.up * 4.85f + new Vector3(0, 0, 0.21f), new Vector3(0.22f, 0.22f, 0.02f), s % 2 == 0 ? new Color(2.6f, 0.3f, 0.2f) : new Color(0.3f, 2.6f, 0.6f));
            }
            // Power poles along the back alleys and Depot Street.
            for (float x = -400f; x < -100f; x += 40f) { P("PowerPole", x, ALLEY_S + 4f, 90f); if (x + 20f < -262f) P("PowerPole", x + 20f, ALLEY_N - 4f, 90f); }
            // The alleys: dumpsters (hiding), crates, an alley cat's worth of clutter, smokers.
            for (int i = 0; i < 6; i++) Clutter(G(-395f + i * 55f, ALLEY_S + 4.5f), 0, i);
            K.HidingKit(G(-332f, ALLEY_S + 4.6f), 0, "Dumpster");
            K.HidingKit(G(-172f, ALLEY_S - 3f), 180, "Dumpster");
            K.HidingKit(G(-320f, ALLEY_N + 3f), 180, "Dumpster");
            K.HidingKit(G(-365f, ALLEY_N - 2.8f), 0, "Dumpster");
            K.Activity(ActivityType.Smoke, G(-326f, ALLEY_S - 2f, 0.1f), 0, Z);
            K.DistractionKit(G(-360f, ALLEY_S + 2.5f), 0, "generator");
            K.Distraction(G(-150f, MAIN_Z - 5.2f, 0.9f), 0, "caralarm");
            K.Weapon(ItemKind.Shotgun, G(-330f, ALLEY_S + 1f, 0.15f));
            K.Weapon(ItemKind.Smg, G(-300f, ALLEY_N, 0.15f));
            K.Gadget(ItemKind.C4, G(-95f, -263f, 0.15f));
            for (int i = 0; i < 4; i++) K.Car(G(-118f + i * 6f, -258f), 0, CAR_TINTS[(i * 3 + 2) % CAR_TINTS.Length]);
            for (int i = 0; i < 6; i++) K.Car(G(-405f + i * 6f, -160f), 0, CAR_TINTS[(i + 1) % CAR_TINTS.Length]);
        }
    }
}
