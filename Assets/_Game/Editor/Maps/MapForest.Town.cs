using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Millbrook (zone 0): a logging town on the west bank. Main Street runs east-west on z = -110
    /// with wooden boardwalks and shop fronts; the square opens on its north side with the timber
    /// festival (stage, stalls, benches, bunting) in front of the town hall and its bell tower.
    /// Ridge Street and Mill Lane run behind, with houses and yards, linked by alleys. The quays
    /// drop to walkable banks under the bridges; the water tower ziplines to the sawmill on the
    /// east bank; the rail depot sits south-west at the mouth of a tunnel.
    /// </summary>
    public partial class MapForest
    {
        private readonly Dictionary<string, MapKit.KitSpec> _specs = new Dictionary<string, MapKit.KitSpec>();

        private const float MAIN_Z = -110f, NORTH_FRONT = -102.5f, SOUTH_FRONT = -117.5f;
        private const float RIDGE_Z = -62f, LANE_Z = -150f;

        private static readonly Color[] TIMBER_TINTS =
        {
            Color.white, new Color(0.92f, 0.82f, 0.7f), new Color(0.82f, 0.72f, 0.62f), new Color(0.75f, 0.85f, 0.82f),
            new Color(0.95f, 0.88f, 0.72f), new Color(0.85f, 0.65f, 0.55f), new Color(0.7f, 0.78f, 0.86f)
        };
        private static readonly Color[] ROOF_TINTS =
        {
            new Color(0.45f, 0.3f, 0.25f), new Color(0.35f, 0.38f, 0.42f), new Color(0.55f, 0.28f, 0.22f), new Color(0.3f, 0.4f, 0.32f), new Color(0.48f, 0.42f, 0.36f)
        };
        private static readonly Color[] AWNING_TINTS =
        {
            new Color(0.75f, 0.2f, 0.18f), new Color(0.2f, 0.45f, 0.35f), new Color(0.25f, 0.35f, 0.6f), new Color(0.85f, 0.6f, 0.2f), new Color(0.5f, 0.25f, 0.4f)
        };

        private readonly List<string> _townLots = new List<string>();

        private MapKit.KitSpec Spec(string pName) => _specs[pName];

        private MapKit.KitSpec Register(MapKit.KitSpec pSpec)
        {
            _specs[pSpec.name] = pSpec;
            return pSpec;
        }

        /// <summary>A Main Street building: north row faces south, south row faces north; door in the middle, back door.</summary>
        private void PlanStreet(string pName, string pStyle, float pX, int pW, int pD, int pFloors, Roof pRoof, int pTint, bool pNorth, params Room[] pRooms)
        {
            float z = pNorth ? NORTH_FRONT + pD * 1.5f : SOUTH_FRONT - pD * 1.5f;
            Color lWall = pStyle == "Timber" ? TIMBER_TINTS[pTint % TIMBER_TINTS.Length] : pStyle == "Suburb" ? new Color(0.95f, 0.92f, 0.86f) : new Color(0.96f, 0.93f, 0.86f);
            MapKit.KitSpec lSpec = Register(Lot(pName, pStyle, pX, z, pNorth ? 0f : 180f, pW, pD, pFloors, pRoof, lWall, ROOF_TINTS[pTint % ROOF_TINTS.Length], 0, 0f, 4f));
            _townLots.Add(pName);
            Doors(lSpec, 0, pW / 2, 2, 1);
            lSpec.brick = pStyle == "Suburb" && pTint % 2 == 0;
            lSpec.tallWindows = pStyle == "Classic";
            lSpec.balconyChance = pFloors > 1 ? 0.2f : 0f;
            if (pRoof == Roof.Flat) lSpec.climb = new Vector2Int(pTint % 2 == 0 ? 1 : 3, 1);
            Floors(lSpec, pRooms);
        }

        private void PlanTown()
        {
            // The town floor, Main Street, the back streets, the alleys, the depot.
            _zoneStage.Add(() => Flatten(R(-430, -250, -96, 30), 0f, 35f));
            _zoneStage.Add(() => Flatten(R(-58, -175, 45, -30), 0f, 30f));
            Road(9f, STREET, new Vector2(-445, MAIN_Z), new Vector2(-96, MAIN_Z), new Vector2(-56, MAIN_Z), new Vector2(40, -112), new Vector2(110, -98), new Vector2(170, -70), new Vector2(230, -45), new Vector2(298, -40), new Vector2(298, -66));
            Road(7f, STREET, new Vector2(-418, RIDGE_Z), new Vector2(-100, RIDGE_Z));
            Road(7f, STREET, new Vector2(-418, LANE_Z), new Vector2(-104, LANE_Z));
            Road(6f, STREET, new Vector2(-340, LANE_Z), new Vector2(-340, RIDGE_Z));
            Road(6f, STREET, new Vector2(-156, LANE_Z), new Vector2(-156, RIDGE_Z));
            Road(6f, STREET, new Vector2(-418, LANE_Z), new Vector2(-418, RIDGE_Z), new Vector2(-412, 0), new Vector2(-330, 90), new Vector2(-262, 150), new Vector2(-205, 196), new Vector2(-250, 255), new Vector2(-318, 300), new Vector2(-345, 330));
            Road(6f, STREET, new Vector2(-325, LANE_Z), new Vector2(-325, -182));
            Road(7f, GRAVEL, new Vector2(-452, -200), new Vector2(-262, -200));
            Trail(new Vector2(-104, -30), new Vector2(-96, -30));
            // Quays: the banks run flat to the town walls, a walk below street level.
            _carveStage.Add(() => Cells(R(-130, -190, -30, -15), (i, j) =>
            {
                float x = Cx(i), z = Cx(j);
                if ((_f[i, j] & (WET | BANK)) != 0) return;
                (float d, int lSeg, float t) = Nearest(RIVER, new Vector2(x, z));
                bool lWest = x < RiverSide(z, 0f).x;
                bool lQuay = lWest ? z > -186f && z < -20f : z > -170f && z < -42f;
                if (!lQuay || d > 15.2f) return;
                _h[i, j] = RIVER_BANK;
                _f[i, j] |= BANK;
            }));
            Trail(new Vector2(-62, -28), new Vector2(-30, -10), new Vector2(18, 10));
            PaintRect(R(-298, -102.5f, -226, -46), COBBLE);
            PaintRect(R(-400, -86, -298, -82), TRAIL, 0);
            PaintRect(R(-226, -86, -112, -82), TRAIL, 0);
            PaintRect(R(-400, -138, -112, -134), TRAIL, 0);
            PaintRect(R(-128, -84, -94, -58), COBBLE);
            PaintRect(R(-375, -212, -262, -188), GRAVEL);
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? NORTH_FRONT - 1.5f : SOUTH_FRONT + 1.5f;
                PaintRect(R(-400, z - 1.5f, -116, z + 1.5f), new Color(0.5f, 0.42f, 0.32f));
            }

            // Main Street, north row (west to east), then the square, then the inn block.
            PlanStreet("Feed and Seed", "Timber", -388, 4, 4, 1, Roof.Gable, 1, true, Room.Storage, Room.None);
            PlanStreet("Millbrook Outfitters", "Timber", -371.5f, 5, 4, 2, Roof.Gable, 4, true, Room.Shop, Room.Storage, Room.Bedroom, Room.None);
            PlanStreet("Hollow Savings Bank", "Classic", -352, 6, 4, 2, Roof.Flat, 0, true, Room.Office, Room.Storage, Room.Office, Room.Lounge);
            PlanStreet("Valley Gazette", "Suburb", -325, 6, 3, 2, Roof.Flat, 2, true, Room.Shop, Room.Office, Room.Office, Room.Bedroom);
            PlanStreet("Doc Hartley", "Timber", -307, 4, 3, 2, Roof.Hip, 6, true, Room.Shop, Room.Bathroom, Room.Bedroom, Room.None);
            PlanStreet("Split Rail Inn", "Timber", -211, 8, 5, 3, Roof.Flat, 5, true, Room.Bar, Room.Dining, Room.Bedroom, Room.Bedroom, Room.Bedroom, Room.Bathroom);
            PlanStreet("Quill Diner", "Suburb", -186, 6, 4, 1, Roof.Flat, 1, true, Room.Dining, Room.Kitchen);
            PlanStreet("Barber and Bath", "Timber", -166.5f, 5, 3, 2, Roof.Gable, 3, true, Room.Shop, Room.Bathroom, Room.Bedroom, Room.None);
            PlanStreet("Millbrook Mercantile", "Timber", -144, 6, 4, 2, Roof.Hip, 2, true, Room.Shop, Room.Storage, Room.Office, Room.Bedroom);
            PlanStreet("Telegraph Office", "Timber", -126, 4, 3, 1, Roof.Gable, 0, true, Room.Office, Room.None);

            // South row.
            PlanStreet("Livery", "Timber", -385, 6, 4, 1, Roof.Gable, 2, false, Room.Storage, Room.None);
            PlanStreet("Boarding House", "Timber", -365.5f, 5, 4, 2, Roof.Hip, 3, false, Room.Lounge, Room.Kitchen, Room.Bedroom, Room.Bedroom);
            PlanStreet("Sheriff", "Timber", -343, 6, 4, 2, Roof.Flat, 6, false, Room.Office, Room.Security, Room.Storage, Room.Bedroom);
            PlanStreet("Gunsmith", "Timber", -325, 4, 3, 1, Roof.Gable, 5, false, Room.Shop, Room.None);
            PlanStreet("Pine Hardware", "Suburb", -307, 6, 4, 2, Roof.Flat, 0, false, Room.Shop, Room.Storage, Room.Storage, Room.Office);
            PlanStreet("Grange Hall", "Timber", -277, 10, 5, 1, Roof.Gable, 4, false, Room.Ballroom, Room.None);
            PlanStreet("Bakery", "Timber", -250, 6, 3, 2, Roof.Hip, 1, false, Room.Shop, Room.Kitchen, Room.Bedroom, Room.None);
            PlanStreet("Apothecary", "Suburb", -230.5f, 5, 3, 2, Roof.Gable, 1, false, Room.Shop, Room.Office, Room.Bedroom, Room.Bathroom);
            PlanStreet("Thistlewood Lumber Co", "Classic", -205, 8, 4, 3, Roof.Flat, 0, false, Room.Lounge, Room.Office, Room.Office, Room.Bathroom, Room.Office, Room.Lounge);
            PlanStreet("Mill Workers Lodging", "Timber", -181, 6, 4, 2, Roof.Gable, 0, false, Room.Lounge, Room.Kitchen, Room.Bedroom, Room.Bedroom);
            PlanStreet("Laundry", "Timber", -165, 4, 3, 1, Roof.Gable, 3, false, Room.Utility, Room.None);
            PlanStreet("Fire Station", "Suburb", -141, 6, 4, 2, Roof.Flat, 2, false, Room.Storage, Room.Gym, Room.Bedroom, Room.Lounge);
            PlanStreet("Smithy", "Timber", -123, 4, 3, 1, Roof.Gable, 2, false, Room.Storage, Room.None);

            // Town hall and its bell tower close the square to the north.
            MapKit.KitSpec lHall = Register(Lot("Town Hall", "Classic", -262, -40, 0, 8, 4, 2, Roof.Flat, new Color(0.96f, 0.94f, 0.88f), ROOF_TINTS[1], 0, 0f, 4f));
            Doors(lHall, 0, 3, 0, 4, 2, 1);
            _townLots.Add("Town Hall");
            _townLots.Add("Bell Tower");
            lHall.tallWindows = true;
            lHall.balconyChance = 0.3f;
            Floors(lHall, Room.Lounge, Room.Office, Room.Office, Room.Dining);
            MapKit.KitSpec lTower = Register(Lot("Bell Tower", "Classic", -243, -41.5f, 0, 2, 3, 6, Roof.Flat, new Color(0.93f, 0.9f, 0.84f), ROOF_TINTS[1], 0, 0f, 4f));
            Doors(lTower, 0, 0);
            lTower.windowChance = 0.5f;

            // Houses on Ridge Street (facing south) and Mill Lane (facing north).
            foreach (HouseDef lHouse in HOUSES)
            {
                float lFront = lHouse.north ? RIDGE_Z + 6f : LANE_Z - 6f;
                float z = lHouse.north ? lFront + lHouse.d * 1.5f : lFront - lHouse.d * 1.5f;
                MapKit.KitSpec lSpec = Register(Lot(lHouse.name, lHouse.style, lHouse.x, z, lHouse.north ? 0f : 180f, lHouse.w, lHouse.d, lHouse.floors, lHouse.roof,
                    lHouse.style == "Suburb" ? new[] { new Color(0.93f, 0.9f, 0.82f), new Color(0.78f, 0.86f, 0.9f), new Color(0.95f, 0.85f, 0.75f) }[lHouse.tint % 3] : TIMBER_TINTS[lHouse.tint % TIMBER_TINTS.Length],
                    ROOF_TINTS[(lHouse.tint + 2) % ROOF_TINTS.Length], 0, 0f, 4f));
                Doors(lSpec, 0, lHouse.w / 2, 2, 0);
                _townLots.Add(lHouse.name);
                lSpec.brick = lHouse.style == "Suburb" && lHouse.tint % 3 == 1;
                lSpec.balconyChance = lHouse.floors > 1 ? 0.25f : 0f;
                if (lHouse.floors > 1) Floors(lSpec, Room.Lounge, Room.Kitchen, Room.Bedroom, Room.Bathroom);
                else Floors(lSpec, Room.Lounge, Room.Bedroom);
            }

            // Workshops and garages on the south side of Ridge Street (backing onto the alley).
            float[] lSheds = { -393, -373, -352, -322, -304, -214, -194, -172, -138, -121 };
            for (int i = 0; i < lSheds.Length; i++)
            {
                bool lTimber = i % 3 != 1;
                MapKit.KitSpec lShedSpec = Register(Lot("Shed " + i, lTimber ? "Timber" : "Suburb", lSheds[i], RIDGE_Z - 11f, 180, 3, 2, 1, i % 2 == 0 ? Roof.Gable : Roof.Hip,
                    lTimber ? TIMBER_TINTS[(i + 2) % TIMBER_TINTS.Length] * 0.92f : new Color(0.88f, 0.86f, 0.8f), ROOF_TINTS[(i + 1) % ROOF_TINTS.Length], 0, 0f, 3f));
                Doors(lShedSpec, 0, 1);
                lShedSpec.windowChance = 0.4f;
                lShedSpec.stairs = false;
                Floors(lShedSpec, i % 2 == 0 ? Room.Storage : Room.Gym, Room.None);
                _townLots.Add(lShedSpec.name);
            }

            // Rail depot.
            MapKit.KitSpec lStation = Register(Lot("Depot Station", "Timber", -350, -186.5f, 0, 8, 3, 1, Roof.Gable, TIMBER_TINTS[5], ROOF_TINTS[2], 0, 0f, 6f));
            Doors(lStation, 0, 2, 0, 5, 2, 3);
            Floors(lStation, Room.Office, Room.Lounge);
            MapKit.KitSpec lShed = Register(Lot("Freight Shed", "Timber", -295, -184, 0, 6, 4, 1, Roof.Gable, TIMBER_TINTS[2], ROOF_TINTS[1], 0, 0f, 6f));
            Doors(lShed, 0, 1, 0, 4, 2, 2);
            Floors(lShed, Room.Storage, Room.None);
            PlanSawmill();
            MapKit.KitSpec lShack = Register(Lot("Moonshine Shack", "Timber", -318, -163.5f, 180, 3, 3, 1, Roof.Gable, TIMBER_TINTS[2] * 0.85f, ROOF_TINTS[4], 0, 0f, 4f));
            Doors(lShack, 0, 1, 2, 1);
        }

        private struct HouseDef
        {
            public string name, style; public float x; public int w, d, floors, tint; public Roof roof; public bool north;
            public HouseDef(string pName, string pStyle, float pX, int pW, int pD, int pFloors, Roof pRoof, int pTint, bool pNorth) { name = pName; style = pStyle; x = pX; w = pW; d = pD; floors = pFloors; roof = pRoof; tint = pTint; north = pNorth; }
        }

        private static readonly HouseDef[] HOUSES =
        {
            new HouseDef("House Ridge 1", "Suburb", -393, 4, 3, 1, Roof.Gable, 0, true),
            new HouseDef("House Ridge 2", "Timber", -374.5f, 5, 4, 2, Roof.Hip, 1, true),
            new HouseDef("House Ridge 3", "Suburb", -355, 4, 3, 2, Roof.Gable, 1, true),
            new HouseDef("House Ridge 4", "Timber", -325, 4, 4, 1, Roof.Gable, 3, true),
            new HouseDef("House Ridge 5", "Suburb", -307.5f, 5, 3, 2, Roof.Hip, 2, true),
            new HouseDef("House Ridge 6", "Timber", -213, 4, 3, 1, Roof.Gable, 4, true),
            new HouseDef("House Ridge 7", "Suburb", -194.5f, 5, 4, 2, Roof.Gable, 0, true),
            new HouseDef("House Ridge 8", "Timber", -176, 4, 3, 2, Roof.Hip, 6, true),
            new HouseDef("House Ridge 9", "Suburb", -140.5f, 5, 3, 2, Roof.Gable, 2, true),
            new HouseDef("House Ridge 10", "Timber", -122, 4, 4, 1, Roof.Gable, 5, true),
            new HouseDef("House Lane 1", "Timber", -392, 4, 3, 1, Roof.Gable, 2, false),
            new HouseDef("House Lane 2", "Suburb", -373.5f, 5, 4, 2, Roof.Hip, 1, false),
            new HouseDef("House Lane 3", "Timber", -355, 4, 3, 2, Roof.Gable, 0, false),
            new HouseDef("House Lane 4", "Suburb", -290, 4, 3, 1, Roof.Gable, 2, false),
            new HouseDef("House Lane 5", "Timber", -271.5f, 5, 4, 2, Roof.Gable, 3, false),
            new HouseDef("House Lane 6", "Suburb", -250, 4, 3, 2, Roof.Hip, 0, false),
            new HouseDef("House Lane 7", "Timber", -205, 4, 3, 1, Roof.Gable, 6, false),
            new HouseDef("House Lane 8", "Suburb", -186.5f, 5, 4, 2, Roof.Gable, 1, false),
            new HouseDef("House Lane 9", "Timber", -140, 4, 3, 2, Roof.Hip, 4, false)
        };

        // ================================================================ build

        private void Town()
        {
            const int Z = 0;
            int lIndex = 0;
            foreach (string lName in _townLots)
            {
                MapKit.KitSpec s = Spec(lName);
                MapKit.KitBuilding b = K.Building(s);
                bool lOnMain = Mathf.Abs(s.origin.z - MAIN_Z) < 20f;
                if (lOnMain && s.style != "Classic") Awnings(b, 0, AWNING_TINTS[lIndex % AWNING_TINTS.Length], AwningModules(s.width));
                if (lOnMain) StreetFront(b, lIndex);
                if (s.name.StartsWith("House")) Yard(b, lIndex);
                if (s.name.StartsWith("Shed"))
                {
                    Clutter(b.Local(4.6f, 0, 1.5f), b.Yaw, lIndex);
                    if (lIndex % 2 == 0) K.Car(G(b.root.position.x - 6.5f, b.root.position.z + 1f), 0, new[] { new Color(0.55f, 0.25f, 0.2f), new Color(0.3f, 0.4f, 0.5f), new Color(0.7f, 0.65f, 0.5f) }[lIndex % 3]);
                    Nat(lIndex % 3 == 0 ? "BirchA" : "BroadleafC", b.root.position.x + 7.5f, b.root.position.z - 1f, Rand(0.55f, 0.75f));
                }
                lIndex++;
            }
            MainStreet();
            Square();
            Inn();
            Sheriff();
            BackStreets();
            Quays();
            WaterTower();
            Depot();

            // Town life: posts, patrols, nodes.
            Guard(Z, -100, -104, 90, ItemKind.Smg);
            Guard(Z, -100, -116, 90, ItemKind.Shotgun);
            Guard(Z, -352, -101, 180, ItemKind.Pistol);
            Guard(Z, -205, -119.5f, 0, ItemKind.Smg);
            K.Npc(NpcRole.Guard, Spec("Thistlewood Lumber Co").origin + new Vector3(2, 2 * MapKit.STOREY + 0.1f, -2), 0, Z, ItemKind.Smg);
            Guard(Z, -186, -100.5f, 180, ItemKind.Pistol);
            Guard(Z, -298, -112, 90, ItemKind.Smg);
            Guard(Z, -156, -80, 180, ItemKind.Shotgun);
            Walk(Z, ItemKind.Smg, 3f, new Vector2(-392, -108), new Vector2(-120, -108), new Vector2(-120, -112.5f), new Vector2(-392, -112.5f));
            Walk(Z, ItemKind.Pistol, 3f, new Vector2(-160, -112.5f), new Vector2(-392, -112.5f), new Vector2(-392, -108), new Vector2(-160, -108));
            Walk(Z, ItemKind.Shotgun, 4f, new Vector2(-405, -82), new Vector2(-305, -82), new Vector2(-340, -62), new Vector2(-405, -62));
            Walk(Z, ItemKind.Smg, 4f, new Vector2(-222, -82), new Vector2(-128, -82), new Vector2(-156, -62), new Vector2(-222, -62));
            Walk(Z, ItemKind.Pistol, 3f, new Vector2(-405, -142), new Vector2(-120, -142), new Vector2(-120, -147), new Vector2(-405, -147));
            Walk(Z, ItemKind.Smg, 5f, new Vector2(-340, -140), new Vector2(-340, -72), new Vector2(-156, -72), new Vector2(-156, -140));

            K.Zone(Z, K.Map.zoneNames[0], new Vector3(-200, 0, -105), new Vector3(520, 80, 340), G(-440, MAIN_Z, 0.1f), new Color(0.55f, 0.45f, 0.35f), HatStyle.Cowboy,
                Spec("Sheriff").origin + Vector3.up * 0.1f + Quaternion.Euler(0, 180, 0) * new Vector3(-3, 0, 3));
        }

        private static int[] AwningModules(int pWidth)
        {
            List<int> l = new List<int>();
            for (int m = 0; m < pWidth; m++) l.Add(m);
            return l.ToArray();
        }

        /// <summary>The boardwalk in front of a Main Street building: deck, hitching rails, lamps, dressing.</summary>
        private void StreetFront(MapKit.KitBuilding b, int pIndex)
        {
            bool lNorth = b.Yaw < 90f;
            float z = lNorth ? NORTH_FRONT - 1.5f : SOUTH_FRONT + 1.5f;
            float x0 = b.root.position.x - b.W * 0.5f - 1.5f, x1 = b.root.position.x + b.W * 0.5f + 1.5f;
            Deck(new Vector3((x0 + x1) * 0.5f, 0.22f, z), new Vector2(x1 - x0, 3f), 0f, 0.24f);
            float lEdge = lNorth ? z - 1.45f : z + 1.45f;
            float lDoor = b.root.position.x;
            // Hitching rails on the street edge, leaving the door open.
            K.Piece("Timber", "Railing", new Vector3(lDoor - 4.5f, 0.22f, lEdge), lNorth ? 180f : 0f, null, K.Env);
            if (pIndex % 2 == 1) K.Piece("Timber", "Railing", new Vector3(lDoor + 4.5f, 0.22f, lEdge), lNorth ? 180f : 0f, null, K.Env);
            float lYaw = lNorth ? 180f : 0f;
            K.Prop("Bench", new Vector3(lDoor + (pIndex % 2 == 0 ? -4.5f : 4.5f), 0.22f, z + (lNorth ? 0.9f : -0.9f)), lYaw, null, MapKit.Collide.Box, false);
            if (pIndex % 3 == 0) Clutter(new Vector3(x1 - 2f, 0.22f, z), lYaw, pIndex);
            if (pIndex % 3 == 1) K.Prop("PlantPot", new Vector3(lDoor - 1.8f, 0.22f, z + (lNorth ? 1f : -1f)), 0, null);
            if (pIndex % 2 == 0)
            {
                K.Prop("LampPostOrnate", new Vector3(x0 + 0.8f, 0.22f, lEdge), 0, null, MapKit.Collide.Box);
                K.Lamp(new Vector3(x0 + 0.8f, 4.1f, lEdge), WARM, 11f, 1.6f, false);
            }
            if (pIndex % 4 == 1) K.Prop("StreetSign", new Vector3(x1 - 0.5f, 0.22f, lEdge), lYaw, null);
        }

        private void MainStreet()
        {
            // Parked cars and trucks along the curbs; a car alarm by the bank.
            float[] lXs = { -380, -362, -331, -296, -248, -216, -190, -170, -148, -132 };
            Color[] lCars = { new Color(0.6f, 0.2f, 0.18f), new Color(0.25f, 0.35f, 0.5f), new Color(0.82f, 0.78f, 0.65f), new Color(0.3f, 0.42f, 0.3f), new Color(0.55f, 0.5f, 0.45f) };
            for (int i = 0; i < lXs.Length; i++)
            {
                bool lNorth = i % 2 == 0;
                K.Car(G(lXs[i], lNorth ? MAIN_Z + 3.2f : MAIN_Z - 3.2f), lNorth ? 270f : 90f, lCars[i % lCars.Length]);
            }
            K.Distraction(G(-362, MAIN_Z - 3.2f, 0.9f), 90, "caralarm");
            K.Distraction(G(-190, MAIN_Z - 3.2f, 0.9f), 90, "caralarm");
            for (float x = -400; x <= -120; x += 40f) { P("PowerPole", x, MAIN_Z - 6.2f, 90); }
            P("Hydrant", -338, NORTH_FRONT - 3.2f, 0);
            P("Hydrant", -229, SOUTH_FRONT + 3.2f, 0);
            P("Mailbox", -128, NORTH_FRONT - 3.3f, 180);
            // The west entrance: a timber arch sign and a sheriff checkpoint.
            for (int s = -1; s <= 1; s += 2) K.Piece("Timber", "Column", G(-420, MAIN_Z + s * 6f), 90, null, K.Env, MapKit.Collide.Box);
            K.Block(K.Env, "WelcomeSign", G(-420, MAIN_Z, 3.6f), new Vector3(0.4f, 1.1f, 12.6f), PLANK * 0.9f);
            K.Block(K.Env, "WelcomeBoard", G(-420.25f, MAIN_Z, 3.6f), new Vector3(0.1f, 0.7f, 8f), new Color(0.88f, 0.82f, 0.62f));
            P("Barrier", -414, MAIN_Z + 2.5f, 90, null, true);
            Guard(0, -412, MAIN_Z - 3f, 270, ItemKind.Shotgun);
            K.Weapon(ItemKind.Pistol, G(-414, MAIN_Z + 4f, 0.1f));
        }

        private void Square()
        {
            const int Z = 0;
            // Fountain, the founder's statue, trees and lamps.
            P("Fountain", -262, -78, 0, null, true);
            P("Statue", -262, -54, 180, null, true);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -294 : -230, z = i < 2 ? -98.5f : -50f;
                Nat("BroadleafC", x, z, 0.9f, -1f, null, MapKit.Collide.Box);
                P("Bench", x + (i % 2 == 0 ? 3f : -3f), z, i % 2 == 0 ? 90f : 270f);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = -294 + (i % 3) * 32f, z = i < 3 ? -96f : -52f;
                P("LampPostOrnate", x, z, 0);
                K.Lamp(G(x, z, 4.1f), WARM, 13f, 1.8f, false);
            }
            // Market stalls on the west side, flower beds.
            for (int i = 0; i < 8; i++)
            {
                float z = -96 + i * 5.5f;
                P("MarketStall", -293, z, 90, AWNING_TINTS[i % AWNING_TINTS.Length], true);
                if (i % 3 == 0) K.Crate(G(-296.5f, z + 1.5f));
            }
            for (int i = 0; i < 5; i++) P("MarketStall", -281 + i * 5.5f, -99, 0, AWNING_TINTS[(i + 2) % AWNING_TINTS.Length], true);
            for (int i = 0; i < 5; i++)
            {
                P("MarketStall", -282, -94 + i * 5.5f, 270, AWNING_TINTS[(i + 4) % AWNING_TINTS.Length], true);
                if (i % 2 == 0) K.Barrel(G(-279.5f, -92 + i * 5.5f), false);
            }
            // The festival pine, food tents, picnic tables, hay bales by the stage, planters on the north edge.
            Nat("PineC", -276, -57, 1.1f, 0f);
            for (int i = 0; i < 10; i++) K.Lamp(G(-276, -57, 1.5f + i * 1.1f) + Quaternion.Euler(0, i * 97f, 0) * Vector3.forward * (3.2f - i * 0.28f), AWNING_TINTS[i % AWNING_TINTS.Length] * 1.6f, 4f, 0.6f, false);
            P("TentMilitary", -248, -54, 180, new Color(0.85f, 0.85f, 0.8f), true, MapKit.Collide.Mesh);
            P("TentMilitary", -236, -52, 180, new Color(0.75f, 0.25f, 0.2f), true, MapKit.Collide.Mesh);
            K.Prop("Table", G(-248, -58.5f), 0, PLANK);
            K.Activity(ActivityType.Dining, G(-248, -60f, 0.1f), 0, Z);
            for (int i = 0; i < 4; i++)
            {
                Vector3 lPicnic = G(-272 + i * 6.5f, -86);
                K.Prop("Table", lPicnic, 0, PLANK, MapKit.Collide.Box, true);
                K.Prop("Bench", lPicnic + new Vector3(0, 0, 1.1f), 180, PLANK);
                K.Prop("Bench", lPicnic + new Vector3(0, 0, -1.1f), 0, PLANK);
            }
            for (int i = 0; i < 3; i++) K.HidingKit(G(-232, -88 + i * 3.4f), 90, "Haystack");
            for (int i = 0; i < 12; i++) Nat(i % 3 == 0 ? "BushB" : "FlowerBed", -294 + i * 6f, -48.5f, 1f, 0f, null, MapKit.Collide.None);
            K.Activity(ActivityType.Inspect, G(-289, -80, 0.1f), 270, Z);
            K.Activity(ActivityType.Garden, G(-262, -64, 0.1f), 180, Z);
            for (int i = 0; i < 4; i++) Nat("FlowerBed", -270 + i * 5.3f, -64, 1f, 0f, null, MapKit.Collide.None);

            // The timber festival stage on the east side, facing the benches; the mayor speaks here.
            Vector3 lStage = G(-237, -72);
            K.Block(K.Env, "Stage", lStage + Vector3.up * 0.6f, new Vector3(7f, 1.2f, 11f), PLANK * 0.85f);
            Deck(lStage + Vector3.up * 1.22f, new Vector2(7f, 11f), 90f, 0.04f);
            K.Stairs(K.Env, lStage + new Vector3(-1f, 0, -8.2f), lStage + new Vector3(-1f, 1.2f, -5.5f), 2.2f, PLANK);
            for (int i = -1; i <= 1; i++) K.Piece("Timber", "Wall", lStage + new Vector3(3.2f, 1.2f, i * 3f), 270f, TIMBER_TINTS[1], K.Env);
            K.Block(K.Env, "Banner", lStage + new Vector3(3f, 4.3f, 0), new Vector3(0.1f, 1.2f, 9f), new Color(0.75f, 0.2f, 0.18f));
            K.Block(K.Env, "BannerText", lStage + new Vector3(2.93f, 4.3f, 0), new Vector3(0.05f, 0.5f, 6.5f), new Color(0.95f, 0.88f, 0.6f));
            K.Activity(ActivityType.Speech, lStage + new Vector3(-0.5f, 1.25f, 0), 270, Z, 18f);
            K.Block(K.Props, "MicStand", lStage + new Vector3(-1.5f, 1.85f, 0), new Vector3(0.06f, 1.3f, 0.06f), new Color(0.2f, 0.2f, 0.22f), Core.GameLayers.DEFAULT, false);
            K.DistractionKit(lStage + new Vector3(1.5f, 1.22f, -4.5f), 270, "radio");
            P("Barrel", lStage + new Vector3(1.8f, 1.22f, 4.4f), 0);
            for (int r = 0; r < 4; r++)
                for (int c = -1; c <= 1; c++)
                    P("Bench", -248 - r * 3.2f, -72 + c * 3.2f, 90);
            // Festival bar and food table.
            P("Table", -255, -92, 0, PLANK, true);
            K.DrinksOn(G(-255, -92, 0.8f), 0, Z, 3);
            P("Table", -270, -92, 0, PLANK, true);
            K.Activity(ActivityType.Dining, G(-270, -93.2f, 0.1f), 0, Z);
            P("UmbrellaTable", -276, -86, 0, AWNING_TINTS[3]);
            P("UmbrellaTable", -248, -86, 0, AWNING_TINTS[1]);
            // Log-sawing contest.
            for (int i = 0; i < 2; i++)
            {
                K.Crate(G(-286, -66 + i * 6f));
                K.Crate(G(-282, -66 + i * 6f));
                Nat("Log", -284, -65.5f + i * 6f, 1f, 0f, null, MapKit.Collide.Box, -1.05f);
            }
            // Bunting across the square.
            for (int i = 0; i < 5; i++) Bunting(G(-294, -96 + i * 11f, 4.8f), G(-230, -92 + i * 10f, 4.8f), i);

            // Security and guests.
            Guard(Z, -236, -60.5f, 270, ItemKind.Smg);
            Guard(Z, -236, -83.5f, 270, ItemKind.Smg);
            Guard(Z, -262, -48.5f, 180, ItemKind.Shotgun);
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(-284, -92), new Vector2(-240, -92), new Vector2(-240, -52), new Vector2(-284, -52));
            K.Weapon(ItemKind.Shotgun, lStage + new Vector3(2.4f, 1.3f, -4.6f));
            K.Gadget(ItemKind.Coin, G(-292, -60, 0.9f));
            K.Gadget(ItemKind.EmeticPoison, G(-268, -91.5f, 0.85f));

            // The bell tower: belfry on the roof, sniper nest, zipline down to the inn roof.
            MapKit.KitSpec t = Spec("Bell Tower");
            float lRoofY = t.origin.y + t.floors * MapKit.STOREY;
            for (int c = 0; c < 4; c++)
                K.Piece("Classic", "Column", new Vector3(t.origin.x + (c % 2 == 0 ? -2.5f : 2.5f), lRoofY, t.origin.z + (c < 2 ? -4f : 4f)), 0, null, K.Env, MapKit.Collide.Box);
            GameObject lCap = K.Piece("Classic", "RoofHip", new Vector3(t.origin.x, lRoofY + MapKit.STOREY, t.origin.z), 0, ROOF_TINTS[1], K.Env, MapKit.Collide.None);
            if (lCap != null) lCap.transform.localScale = new Vector3(6.6f, 3.2f, 9.6f);
            K.Block(K.Env, "BelfryCeiling", new Vector3(t.origin.x, lRoofY + MapKit.STOREY - 0.1f, t.origin.z), new Vector3(6.2f, 0.2f, 9.2f), STONE);
            K.Mesh(K.Env, "Bell", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(12, 0.55f), "Forest_Bell"), new Vector3(t.origin.x, lRoofY + 2.4f, t.origin.z + 1.5f), new Vector3(1.4f, 1.3f, 1.4f), new Color(0.85f, 0.65f, 0.3f));
            K.Npc(NpcRole.Lookout, new Vector3(t.origin.x - 1f, lRoofY + 0.1f, t.origin.z - 2f), 180, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, new Vector3(t.origin.x + 1.2f, lRoofY + 0.15f, t.origin.z - 3f));
            MapKit.KitSpec lInn = Spec("Split Rail Inn");
            K.Zipline(new Vector3(t.origin.x + 1.5f, lRoofY + 0.05f, t.origin.z - 3.2f), new Vector3(lInn.origin.x - 6f, lInn.origin.y + lInn.floors * MapKit.STOREY + 0.05f, lInn.origin.z + 3f));
        }

        /// <summary>A line of little flags hanging between two points.</summary>
        private void Bunting(Vector3 pFrom, Vector3 pTo, int pSeed)
        {
            int lCount = Mathf.RoundToInt(Vector3.Distance(pFrom, pTo) / 1.4f);
            Quaternion lRot = Quaternion.LookRotation(new Vector3(pTo.x - pFrom.x, 0, pTo.z - pFrom.z));
            K.Block(K.Props, "BuntingRope", (pFrom + pTo) * 0.5f + Vector3.up * -0.6f, new Vector3(0.03f, 0.03f, Vector3.Distance(pFrom, pTo)), new Color(0.3f, 0.3f, 0.3f), Core.GameLayers.DEFAULT, false, Quaternion.LookRotation(pTo - pFrom));
            for (int i = 1; i < lCount; i++)
            {
                float t = (float)i / lCount;
                Vector3 p = Vector3.Lerp(pFrom, pTo, t) + Vector3.down * (0.6f + Mathf.Sin(t * Mathf.PI) * 0.0f) + Vector3.down * 0.25f;
                K.Block(K.Props, "Flag", p, new Vector3(0.02f, 0.45f, 0.5f), AWNING_TINTS[(i + pSeed) % AWNING_TINTS.Length], Core.GameLayers.DEFAULT, false, lRot * Quaternion.Euler(0, 90, 0));
            }
        }

        private void Inn()
        {
            const int Z = 0;
            MapKit.KitSpec s = Spec("Split Rail Inn");
            float lRoof = s.origin.y + s.floors * MapKit.STOREY;
            // The rooftop terrace (zipline arrival): parasol, crates, a lookout.
            K.Prop("UmbrellaTable", new Vector3(s.origin.x + 2f, lRoof, s.origin.z - 2f), 0, AWNING_TINTS[0]);
            K.Npc(NpcRole.Lookout, new Vector3(s.origin.x - 4f, lRoof + 0.1f, s.origin.z - 4f), 180, Z, ItemKind.Smg);
            // Upstairs card game, poker room weapon, the smokers' alley behind.
            K.Weapon(ItemKind.Pistol, new Vector3(s.origin.x - 6f, s.origin.y + MapKit.STOREY + 0.9f, s.origin.z + 4f));
            K.Activity(ActivityType.Smoke, G(s.origin.x + 4f, s.origin.z + 10f, 0.1f), 180, Z);
            K.Activity(ActivityType.Phone, G(s.origin.x - 4f, s.origin.z + 10.5f, 0.1f), 180, Z);
            K.HidingKit(G(s.origin.x + 8f, s.origin.z + 10f), 180, "Dumpster");
            Guard(Z, s.origin.x - 1f, s.origin.z + 10f, 0, ItemKind.Pistol);
            K.Gadget(ItemKind.Poison, new Vector3(s.origin.x - 8f, s.origin.y + 1.05f, s.origin.z + 4f));
        }

        private void Sheriff()
        {
            const int Z = 0;
            MapKit.KitSpec s = Spec("Sheriff");
            Quaternion r = Quaternion.Euler(0, s.yaw, 0);
            K.Npc(NpcRole.Guard, s.origin + r * new Vector3(-3, 0.1f, -1), 0, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Lookout, s.origin + Vector3.up * (s.floors * MapKit.STOREY + 0.1f) + r * new Vector3(-4, 0, -3), 180, Z, ItemKind.Sniper);
            K.Activity(ActivityType.Lookout, s.origin + Vector3.up * (s.floors * MapKit.STOREY + 0.1f) + r * new Vector3(2, 0, -4.5f), 180 + s.yaw, Z);
            K.Weapon(ItemKind.Shotgun, s.origin + Vector3.up * (MapKit.STOREY + 1f) + r * new Vector3(-5, 0, -3.5f));
            K.Weapon(ItemKind.Smg, s.origin + Vector3.up * (MapKit.STOREY + 1f) + r * new Vector3(-3, 0, -3.5f));
            K.Gadget(ItemKind.SmokeGrenade, s.origin + Vector3.up * (MapKit.STOREY + 1f) + r * new Vector3(-1, 0, -3.5f));
            K.Car(G(s.origin.x + 12f, MAIN_Z - 3.2f), 90, new Color(0.12f, 0.14f, 0.2f));
            MapKit.KitSpec g = Spec("Gunsmith");
            K.Weapon(ItemKind.Pistol, g.origin + new Vector3(1, 1.0f, 0));
            K.Weapon(ItemKind.Shotgun, g.origin + new Vector3(-1, 1.0f, 1));
            K.Weapon(ItemKind.Smg, Spec("Pine Hardware").origin + new Vector3(2, 1.0f, 2));
            K.Weapon(ItemKind.Shotgun, Spec("Quill Diner").origin + new Vector3(-3, 1.0f, 3));
            K.Weapon(ItemKind.Smg, Spec("Fire Station").origin + new Vector3(3, MapKit.STOREY + 0.9f, -2));
            K.Gadget(ItemKind.Brick, Spec("Smithy").origin + new Vector3(0, 0.2f, 0));
            K.Gadget(ItemKind.C4, Spec("Pine Hardware").origin + new Vector3(-3, MapKit.STOREY + 0.9f, 2));
            K.Weapon(ItemKind.Pistol, Spec("Hollow Savings Bank").origin + new Vector3(3, 1.0f, 3));
            // The diner needs a washroom for its regulars.
            MapKit.KitSpec d = Spec("Quill Diner");
            K.ToiletKit(d.origin + new Vector3(-7.4f, 0, 4.6f), 90, Z);
        }

        /// <summary>Yards behind the rows and in front of the houses: fences, sheds, gardens, cars, dumpsters.</summary>
        private void Yard(MapKit.KitBuilding b, int pIndex)
        {
            bool lNorth = b.Yaw < 90f;
            float zFront = lNorth ? RIDGE_Z + 6f : LANE_Z - 6f;
            float x = b.root.position.x, hw = b.W * 0.5f;
            // Picket fence along the street, 1.4 m in front of the house (gate gap at the door, a driveway on the side).
            float lYardZ = lNorth ? zFront - 1.4f : zFront + 1.4f;
            Row("Timber", "Railing", new Vector3(x - hw - 1.5f, 0, lYardZ), new Vector3(x - 1.5f, 0, lYardZ), 3f, 0f, TIMBER_TINTS[0]);
            Row("Timber", "Railing", new Vector3(x + 1.5f, 0, lYardZ), new Vector3(x + hw, 0, lYardZ), 3f, 0f, TIMBER_TINTS[0]);
            P("Mailbox", x + 2.3f, lYardZ + (lNorth ? -0.5f : 0.5f), lNorth ? 180 : 0);
            Nat(pIndex % 2 == 0 ? "BirchA" : "BroadleafC", x - hw - 3f, zFront + (lNorth ? 4f : -4f), Rand(0.6f, 0.85f));
            Nat("FlowerBed", x - hw + 1.5f, lYardZ + (lNorth ? 0.75f : -0.75f), 1f, 0f, null, MapKit.Collide.None);
            if (pIndex % 3 == 0) K.Car(G(x + hw + 3.2f, zFront + (lNorth ? 3f : -3f)), lNorth ? 0f : 180f, new Color(0.55f, 0.25f, 0.2f));
            // Backyard: woodpile and a garden node.
            float zBack = lNorth ? b.root.position.z + b.D * 0.5f + 3f : b.root.position.z - b.D * 0.5f - 3f;
            for (int i = 0; i < 3; i++) Nat("Log", x - hw + 1.5f, zBack + i * 0.85f * (lNorth ? 1 : -1), 0.7f, 90f, null, MapKit.Collide.Box, 0.1f);
            if (pIndex % 4 == 0) K.Activity(ActivityType.Garden, G(x, zBack + (lNorth ? 1.5f : -1.5f), 0.1f), lNorth ? 0f : 180f, 0);
            if (pIndex % 5 == 2) K.HidingKit(G(x + hw - 1.5f, zBack), lNorth ? 180 : 0, "Wardrobe");
        }

        private void BackStreets()
        {
            const int Z = 0;
            // Service alleys behind the Main Street rows: dumpsters (hiding), crates, barrels, sheds.
            float[] lNorthAlley = { -386, -360, -330, -306, -214, -182, -150, -126 };
            for (int i = 0; i < lNorthAlley.Length; i++)
            {
                Vector3 p = G(lNorthAlley[i], -84);
                if (i % 2 == 0) K.HidingKit(p, 180, "Dumpster");
                else Clutter(p, 0, i);
                if (i % 3 == 0) K.Barrel(G(lNorthAlley[i] + 3f, -85f), true);
            }
            float[] lSouthAlley = { -380, -350, -320, -276, -246, -206, -176, -138 };
            for (int i = 0; i < lSouthAlley.Length; i++)
            {
                Vector3 p = G(lSouthAlley[i], -136);
                if (i % 2 == 1) K.HidingKit(p, 0, "Dumpster");
                else Clutter(p, 180, i + 1);
            }
            // Lamps on the back streets.
            for (float x = -400; x <= -110; x += 36f)
            {
                P("StreetLamp", x, RIDGE_Z - 4.2f, 0);
                K.Lamp(G(x, RIDGE_Z - 3f, 4.3f), WARM, 12f, 1.4f, false);
                P("StreetLamp", x + 18f, LANE_Z + 4.2f, 180);
                K.Lamp(G(x + 18f, LANE_Z + 3f, 4.3f), WARM, 12f, 1.4f, false);
            }
            // The moonshiner's shack: barrels, gas bottles, his still (and a flamethrower hidden here).
            MapKit.KitBuilding lShack = K.Building(Spec("Moonshine Shack"));
            Vector3 c = lShack.root.position;
            K.Barrel(c + new Vector3(-2.5f, 0, -1.5f), false);
            K.Barrel(c + new Vector3(-2.5f, 0, -0.7f), false);
            K.Prop("GasBottle", c + new Vector3(2.8f, 0, 3.5f), 0, null, MapKit.Collide.Box);
            K.Prop("GasBottle", c + new Vector3(3.3f, 0, 3.6f), 0, null, MapKit.Collide.Box);
            K.Barrel(c + new Vector3(3.5f, 0, -3.5f), true);
            K.Mesh(K.Props, "Still", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(10, 0.7f), "Forest_Still"), c + new Vector3(1.2f, 0.6f, 0.8f), new Vector3(1.1f, 1.2f, 1.1f), new Color(0.78f, 0.5f, 0.3f), null, true);
            K.Activity(ActivityType.Inspect, c + new Vector3(0.2f, 0.1f, 0.6f), 90, Z);
            K.Activity(ActivityType.Drink, c + new Vector3(-0.5f, 0.1f, 4.6f), 180, Z);
            K.Activity(ActivityType.Smoke, c + new Vector3(3f, 0.1f, 5.5f), 0, Z);
            K.ToiletKit(c + new Vector3(-4.2f, 0, 6.5f), 0, Z);
            K.UniqueWeapon(ItemKind.Flamethrower, c + new Vector3(-1.2f, 0.15f, -1.6f), "Moonshine shack");
            Guard(Z, c.x + 4f, c.z + 7f, 0, ItemKind.Shotgun);
        }

        /// <summary>The river walls of the town: quay walls with balustrades, stairs down to the banks.</summary>
        private void Quays()
        {
            const int Z = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                float lFrom = side < 0 ? -186f : -170f, lTo = side < 0 ? -20f : -42f;
                for (float z = lFrom; z < lTo; z += 6f)
                {
                    if (Mathf.Abs(z + 3f - MAIN_Z) < 9f || Mathf.Abs(z + 3f + 30f) < 5f) continue;
                    Vector3 a = RiverSide(z, side * 16.2f), b = RiverSide(z + 6f, side * 16.2f);
                    Vector3 lMid = (a + b) * 0.5f;
                    Quaternion lRot = Quaternion.LookRotation(b - a);
                    K.Block(K.Env, "QuayWall", new Vector3(lMid.x, -1.7f, lMid.z), new Vector3(2.4f, 3.55f, Vector3.Distance(a, b) + 0.3f), new Color(0.56f, 0.55f, 0.52f), Core.GameLayers.DEFAULT, true, lRot);
                    bool lStairs = Mathf.Abs(z - (side < 0 ? -152f : -76f)) < 1f || Mathf.Abs(z - (side < 0 ? -64f : -128f)) < 1f;
                    if (lStairs)
                    {
                        Vector3 lIn = Vector3.Cross(Vector3.up, (b - a).normalized) * side;
                        K.Stairs(K.Env, new Vector3(lMid.x, RIVER_BANK, lMid.z) - lIn * 4.5f + (b - a).normalized * 1.5f, new Vector3(lMid.x, 0.05f, lMid.z) - lIn * 1f + (b - a).normalized * 1.5f, 2f, STONE);
                        continue;
                    }
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector3 lAt = lMid + (b - a).normalized * (k * 1.5f);
                        K.Piece("Classic", "Balustrade", new Vector3(lAt.x, 0.05f, lAt.z), lRot.eulerAngles.y + 90f * side, STONE, K.Env);
                    }
                }
            }
            // Main Street bridge (stone) and the north footbridge.
            Span(new Vector3(-94, 0.3f, MAIN_Z), new Vector3(-58, 0.3f, MAIN_Z), 10f, true);
            Span(new Vector3(-100, 0.3f, -30), new Vector3(-62, 0.3f, -30), 3f, false);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Abutment", new Vector3(s < 0 ? -91 : -61, -1.7f, MAIN_Z), new Vector3(6f, 3.6f, 10.4f), STONE * 0.88f);
            // Under the bridge: the banks, a stash, a fisherman's spot.
            K.Crate(new Vector3(-86, RIVER_BANK, MAIN_Z + 2f));
            K.Weapon(ItemKind.Pistol, new Vector3(-86.5f, RIVER_BANK + 0.1f, MAIN_Z - 1f));
            K.Gadget(ItemKind.SmokeGrenade, new Vector3(-66, RIVER_BANK + 0.1f, MAIN_Z + 1f));
            Walk(Z, ItemKind.Smg, 4f, Bank(-176, -1), Bank(-40, -1), Bank(-40, 1), Bank(-150, 1));
            // Logs floating in the river by the sawmill.
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = RiverSide(-96 + i * 4.5f, Rand(-4f, 5f));
                GameObject lLog = K.NaturePiece("Log", new Vector3(p.x, RIVER_WATER - 0.45f, p.z), Rand(60f, 120f), Rand(0.9f, 1.3f), LOG, MapKit.Collide.None);
            }
            // The plaza by the bridge: benches, lamps, a fishing pier view.
            P("Bench", -112, -64, 90);
            P("Bench", -112, -80, 90);
            K.Activity(ActivityType.Smoke, G(-104, -84, 0.1f), 90, Z);
        }

        /// <summary>A point beside the river at a given z (offset across it, + = east).</summary>
        private Vector3 RiverSide(float pZ, float pOffset)
        {
            for (int s = 0; s < RIVER.Length - 1; s++)
            {
                Vector2 a = RIVER[s], b = RIVER[s + 1];
                if ((pZ <= a.y && pZ >= b.y) || (pZ >= a.y && pZ <= b.y))
                {
                    float t = Mathf.InverseLerp(a.y, b.y, pZ);
                    Vector2 p = Vector2.Lerp(a, b, t);
                    Vector2 lDir = (b - a).normalized;
                    Vector2 lEast = new Vector2(-lDir.y, lDir.x);
                    if (lEast.x < 0) lEast = -lEast;
                    p += lEast * pOffset;
                    return new Vector3(p.x, 0, p.y);
                }
            }
            return new Vector3(-80, 0, pZ);
        }

        private Vector2 Bank(float pZ, int pSide)
        {
            Vector3 p = RiverSide(pZ, pSide * 9.5f);
            return new Vector2(p.x, p.z);
        }

        private void WaterTower()
        {
            const int Z = 0;
            Vector3 lBase = G(-112, -72);
            const float DECK = 18f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 lLeg = lBase + new Vector3(i % 2 == 0 ? -3f : 3f, 0, i < 2 ? -3f : 3f);
                for (int k = 0; k < 4; k++)
                {
                    GameObject lLog = K.NaturePiece("Log", lLeg + Vector3.up * (k * 4.1f + 2.05f), 0, 1.05f, LOG * 0.9f, MapKit.Collide.None);
                    if (lLog != null) lLog.transform.rotation = Quaternion.Euler(0, 0, 90);
                }
                K.Block(K.Env, "LegCollider", lLeg + Vector3.up * DECK * 0.5f, new Vector3(0.7f, DECK, 0.7f), LOG).GetComponent<MeshRenderer>().enabled = false;
            }
            for (int k = 1; k < 4; k++)
                for (int s = 0; s < 4; s++)
                    K.Block(K.Env, "Brace", lBase + Vector3.up * (k * 4.5f) + Quaternion.Euler(0, s * 90f, 0) * new Vector3(0, 0, 3f), new Vector3(6.2f, 0.25f, 0.25f), LOG * 0.85f, Core.GameLayers.DEFAULT, false, Quaternion.Euler(0, s * 90f, 0));
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    K.Piece("Timber", "RoofSlab", lBase + new Vector3(x * 3f, DECK - 0.3f, z * 3f), 0, PLANK, K.Env);
            for (int s = 0; s < 4; s++)
                for (int m = -1; m <= 1; m++)
                {
                    if (s == 0 && m == 0) continue;
                    Quaternion r = Quaternion.Euler(0, s * 90f, 0);
                    K.Piece("Timber", "Railing", lBase + Vector3.up * DECK + r * new Vector3(m * 3f, 0, -4.4f), s * 90f + 180f, null, K.Env);
                }
            Mesh lTank = EditorAssets.SaveMesh(LowPolyMesh.Cylinder(16), "Forest_Tank");
            K.Mesh(K.Env, "Tank", lTank, lBase + Vector3.up * (DECK + 3.4f), new Vector3(6.4f, 6.4f, 6.4f), new Color(0.5f, 0.36f, 0.25f), null, true).isStatic = true;
            for (int i = 0; i < 3; i++) K.Mesh(K.Env, "Hoop", lTank, lBase + Vector3.up * (DECK + 1.2f + i * 2.2f), new Vector3(6.55f, 0.18f, 6.55f), new Color(0.25f, 0.25f, 0.27f));
            K.Mesh(K.Env, "TankRoof", EditorAssets.SaveMesh(LowPolyMesh.Cone(16), "Forest_Cone16"), lBase + Vector3.up * (DECK + 6.6f), new Vector3(7.2f, 2.6f, 7.2f), new Color(0.35f, 0.3f, 0.28f));
            K.Block(K.Env, "Sign", lBase + new Vector3(0, DECK + 3.6f, -3.25f), new Vector3(3.6f, 1.2f, 0.1f), new Color(0.92f, 0.88f, 0.75f));
            Ladder(lBase + new Vector3(0, 0, -4.6f), DECK + 0.6f, 180f);
            K.Npc(NpcRole.Lookout, lBase + new Vector3(-2, DECK + 0.1f, 2), 90, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, lBase + new Vector3(2.5f, DECK + 0.15f, -2.5f));
            // Zipline across the river to the sawmill's head rig.
            MapKit.KitSpec lRig = Spec("Sawmill Head Rig");
            K.Zipline(lBase + new Vector3(3.2f, DECK + 0.05f, -1.5f), lRig.origin + Vector3.up * (lRig.floors * MapKit.STOREY + 0.05f) + new Vector3(-2.5f, 0, 1f));
        }

        private void Depot()
        {
            const int Z = 0;
            K.Building(Spec("Depot Station"));
            K.Building(Spec("Freight Shed"));
            // Platform, tracks with sleepers, flatcars of logs.
            Deck(G(-350, -195, 0.9f), new Vector2(30, 4), 0, 0.9f, STONE * 0.95f);
            K.Stairs(K.Env, G(-367.5f, -195), G(-365f, -195, 0.9f), 3f, STONE);
            for (float x = -440; x < -262; x += 1.2f)
                K.Block(K.Env, "Sleeper", G(x, -200, 0.08f), new Vector3(0.35f, 0.16f, 2.6f), new Color(0.38f, 0.3f, 0.24f), Core.GameLayers.DEFAULT, false);
            for (int s = -1; s <= 1; s += 2)
                K.Block(K.Env, "Rail", new Vector3(-351, 0.22f, -200 + s * 0.72f), new Vector3(178, 0.12f, 0.1f), new Color(0.45f, 0.45f, 0.48f), Core.GameLayers.DEFAULT, false);
            K.Block(K.Env, "Bumper", G(-263, -200, 0.6f), new Vector3(0.6f, 1.2f, 2.4f), new Color(0.8f, 0.2f, 0.15f));
            for (int c = 0; c < 3; c++)
            {
                Vector3 lCar = G(-325 + c * 14f, -200, 0.25f);
                K.Block(K.Env, "Flatcar", lCar + Vector3.up * 0.6f, new Vector3(12f, 0.5f, 2.8f), new Color(0.32f, 0.28f, 0.26f), Core.GameLayers.DEFAULT, true, null, true);
                for (int w = 0; w < 4; w++)
                    K.Mesh(K.Env, "Wheel", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(10), "Kit_Cyl10"), lCar + new Vector3(w < 2 ? -4.2f : 4.2f, 0.2f, w % 2 == 0 ? -0.72f : 0.72f), new Vector3(0.8f, 0.12f, 0.8f), new Color(0.2f, 0.2f, 0.22f), Quaternion.Euler(90, 0, 0));
                for (int l = 0; l < 6; l++)
                {
                    GameObject lLog = K.NaturePiece("Log", lCar + new Vector3(-3f + (l / 3) * 6f, 1.25f + (l % 3 == 2 ? 0.7f : 0f), (l % 3 == 2 ? 0 : (l % 3 == 0 ? -0.5f : 0.5f))), 90f, 1.45f, LOG, MapKit.Collide.None);
                }
            }
            K.Car(G(-318, -210), 90, new Color(0.62f, 0.25f, 0.18f));
            K.Car(G(-300, -212), 270, new Color(0.25f, 0.32f, 0.45f));
            for (int i = 0; i < 4; i++) Clutter(G(-288 + i * 4f, -177), 0, i);
            Container(G(-280, -210), 90, new Color(0.65f, 0.3f, 0.2f), true);
            Container(G(-280, -210, 2.6f), 90, new Color(0.3f, 0.45f, 0.5f));
            K.DistractionKit(G(-292, -178), 0, "generator");
            K.Activity(ActivityType.Inspect, G(-305, -193, 0.1f), 180, Z);
            K.Activity(ActivityType.Phone, G(-352, -195, 1f), 180, Z);
            Guard(Z, -340, -195, 180, ItemKind.Smg);
            Guard(Z, -300, -204, 270, ItemKind.Shotgun);
            Walk(Z, ItemKind.Smg, 4f, new Vector2(-400, -206), new Vector2(-270, -206), new Vector2(-270, -192), new Vector2(-330, -180));
            K.Weapon(ItemKind.Smg, G(-296, -186, 0.1f));
            K.Gadget(ItemKind.C4, G(-292, -187, 0.1f));
            K.Point(MatchPoint.Kind.Extraction, G(-312, -212, 0.1f), 90, "Depot logging truck");

            // The rail tunnel into the west ridge.
            Vector3 lPortal = G(-438, -200);
            for (int i = -1; i <= 1; i++) K.Piece("Classic", "WallArch", lPortal + new Vector3(0, 0, i * 3f) + Vector3.up * (i == 0 ? 0 : 0), 90, STONE * 0.85f, K.Env);
            K.Block(K.Env, "TunnelDark", lPortal + new Vector3(-3f, 2f, 0), new Vector3(5f, 4f, 9f), new Color(0.05f, 0.05f, 0.06f));
            for (int i = 0; i < 6; i++) Nat("CliffChunk", -444 + Rand(-2f, 2f), -200 + (i - 2.5f) * 4.5f, Rand(0.9f, 1.4f), -1f, null, MapKit.Collide.Box, 1.5f);
        }

        // ================================================================ sawmill

        private void PlanSawmill()
        {
            MapKit.KitSpec lHall = Register(Lot("Sawmill Hall", "Timber", -15, -95, 0, 8, 6, 2, Roof.Gable, new Color(0.82f, 0.7f, 0.58f), ROOF_TINTS[1], 0, 0f, 6f));
            Doors(lHall, 0, 3, 0, 4, 2, 1, 3, 2);
            lHall.upperDoors.Add(new Vector3Int(2, 4, 1));
            lHall.upperDoors.Add(new Vector3Int(1, 1, 1));
            lHall.windowChance = 0.55f;
            Floors(lHall, Room.Storage, Room.None, Room.Storage, Room.None);
            MapKit.KitSpec lRig = Register(Lot("Sawmill Head Rig", "Timber", 5.5f, -99.5f, 0, 3, 3, 4, Roof.Flat, new Color(0.75f, 0.62f, 0.5f), ROOF_TINTS[1], 0, 0f, 6f));
            Doors(lRig, 0, 1);
            lRig.upperDoors.Add(new Vector3Int(3, 1, 1));
            lRig.climb = new Vector2Int(1, 1);
            Floors(lRig, Room.Utility, Room.None, Room.None, Room.None, Room.Office, Room.None);
            MapKit.KitSpec lOffice = Register(Lot("Sawmill Office", "Timber", -40, -96.5f, 0, 4, 3, 2, Roof.Hip, TIMBER_TINTS[1], ROOF_TINTS[0], 0, 0f, 6f));
            Doors(lOffice, 0, 2, 2, 1);
            Floors(lOffice, Room.Office, Room.Bathroom, Room.Office, Room.Lounge);
            PaintRect(R(-52, -84, 40, -34), new Color(0.56f, 0.47f, 0.36f));
            PaintRect(R(-52, -104, 30, -86), new Color(0.56f, 0.47f, 0.36f), BUILT);
        }

        private void Sawmill()
        {
            const int Z = 0;
            MapKit.KitBuilding lHall = K.Building(Spec("Sawmill Hall"));
            MapKit.KitBuilding lRig = K.Building(Spec("Sawmill Head Rig"));
            MapKit.KitBuilding lOffice = K.Building(Spec("Sawmill Office"));
            Awnings(lOffice, 0, AWNING_TINTS[1], 0, 1, 2, 3);

            // The saw line inside: a big blade, the carriage with a log, sawdust.
            Vector3 h = lHall.root.position;
            K.Mesh(K.Props, "SawBlade", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(20), "Forest_Blade"), h + new Vector3(2, 1.6f, 0), new Vector3(2.6f, 0.08f, 2.6f), new Color(0.75f, 0.77f, 0.8f), Quaternion.Euler(0, 0, 90));
            K.Block(K.Env, "SawCarriage", h + new Vector3(-3, 0.5f, 0), new Vector3(14f, 1f, 1.6f), new Color(0.4f, 0.35f, 0.3f), Core.GameLayers.DEFAULT, true, Quaternion.Euler(0, 90, 0), true);
            K.NaturePiece("Log", h + new Vector3(-3, 1.4f, -2f), 0, 1.6f, LOG, MapKit.Collide.None);
            for (int i = 0; i < 4; i++) K.Mesh(K.Props, "Sawdust", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0.15f, 3), "Forest_Dust"), h + new Vector3(4f + i * 1.3f, 0.1f, 2f - i), new Vector3(1.6f, 0.7f, 1.4f), new Color(0.85f, 0.72f, 0.5f));
            for (int i = 0; i < 4; i++) K.Block(K.Env, "Plank Stack", h + new Vector3(-9, 0.4f + i * 0.2f, -4.5f + i * 0.05f), new Vector3(1.3f, 0.18f, 5f), new Color(0.82f, 0.68f, 0.48f), Core.GameLayers.DEFAULT, true, null, i == 3);
            K.Activity(ActivityType.Inspect, h + new Vector3(0, 0.1f, -3.5f), 90, Z);
            K.DistractionKit(h + new Vector3(9, 0, 6), 180, "generator");
            K.Npc(NpcRole.Guard, h + new Vector3(-6, 0.1f, 3), 180, Z, ItemKind.Shotgun);
            K.Npc(NpcRole.Guard, h + new Vector3(4, MapKit.STOREY + 0.1f, 2), 270, Z, ItemKind.Smg);
            K.Weapon(ItemKind.Smg, h + new Vector3(10, 0.15f, 6));
            K.Gadget(ItemKind.C4, h + new Vector3(-10, MapKit.STOREY + 0.15f, 6));

            // A plank bridge between the hall and the head rig on the first floor.
            Span(new Vector3(-2.8f, MapKit.STOREY + 0.32f, -99.5f + 0f), new Vector3(1.2f, MapKit.STOREY + 0.32f, -99.5f), 2.2f, false, 0.1f);
            Vector3 r = lRig.root.position;
            K.Npc(NpcRole.Lookout, r + new Vector3(-2, lRig.spec.floors * MapKit.STOREY + 0.1f, -2), 200, Z, ItemKind.Sniper);
            K.UniqueWeapon(ItemKind.RocketLauncher, r + new Vector3(-2.5f, 3 * MapKit.STOREY + 0.15f, -2.5f), "Sawmill head rig");
            K.Activity(ActivityType.Lookout, r + new Vector3(1, lRig.spec.floors * MapKit.STOREY + 0.1f, -3.5f), 180, Z);

            // Log yard north of the hall: log decks, the gantry crane, trucks, containers, the conveyor.
            for (int stack = 0; stack < 6; stack++)
            {
                float x = -44 + (stack % 3) * 16f, z = -70 + (stack / 3) * 20f;
                LogDeck(new Vector3(x, 0, z), stack % 2 == 0 ? 0f : 90f, 3 + stack % 2);
            }
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                    K.Piece("Timber", "Scaffold", new Vector3(-4 + s * 11f, k * 4f, -58), 90, null, K.Env);
            K.Block(K.Env, "GantryBeam", new Vector3(-4, 12.3f, -58), new Vector3(23f, 0.6f, 1.2f), new Color(0.75f, 0.6f, 0.2f));
            K.Block(K.Env, "Trolley", new Vector3(-2, 11.6f, -58), new Vector3(1.4f, 0.8f, 1.4f), new Color(0.3f, 0.3f, 0.32f));
            K.Block(K.Env, "Cable", new Vector3(-2, 8.6f, -58), new Vector3(0.05f, 5.2f, 0.05f), new Color(0.2f, 0.2f, 0.2f), Core.GameLayers.DEFAULT, false);
            GameObject lHung = K.NaturePiece("Log", new Vector3(-2, 5.8f, -58), 0, 1.3f, LOG, MapKit.Collide.None);
            Ladder(new Vector3(-15.6f, 0, -58), 12f, 270f);
            K.Car(G(20, -66), 0, new Color(0.62f, 0.25f, 0.18f));
            K.Car(G(26, -48), 180, new Color(0.3f, 0.42f, 0.3f));
            Container(G(32, -76), 0, new Color(0.65f, 0.32f, 0.2f), true);
            Container(G(35, -76), 0, new Color(0.3f, 0.45f, 0.55f));
            Container(G(33.5f, -76, 2.6f), 0, new Color(0.55f, 0.55f, 0.3f));
            // The conveyor ramp from the yard to the first-floor door.
            Vector3 lDoorTop = Facade(lHall, 2, 4, MapKit.STOREY, 0f);
            Vector3 lLanding = lDoorTop + Vector3.forward * 1.6f;
            Deck(lLanding + Vector3.up * 0.0f, new Vector2(4f, 3.2f), 0f, 0.3f);
            K.Ramp(K.Env, new Vector3(lLanding.x, 0, lLanding.z + 13f), lLanding + Vector3.forward * 1.5f, 2.4f, new Color(0.35f, 0.32f, 0.3f), 0.35f);
            for (int i = 0; i < 5; i++) K.NaturePiece("Log", Vector3.Lerp(new Vector3(lLanding.x + 0.9f, 0.6f, lLanding.z + 12f), lLanding + new Vector3(0.9f, 0.6f, 2f), i / 5f), 0, 0.5f, LOG, MapKit.Collide.None);
            // The wigwam burner (sawdust incinerator) and the boiler chimney: the sawmill's silhouette.
            Vector3 lBurner = G(30, -56);
            K.Mesh(K.Env, "WigwamBurner", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(16, 0.55f), "Forest_Burner"), lBurner + Vector3.up * 6f, new Vector3(11f, 12f, 11f), new Color(0.42f, 0.3f, 0.25f), null, true).isStatic = true;
            K.Mesh(K.Env, "BurnerScreen", EditorAssets.SaveMesh(LowPolyMesh.Icosphere(1, 0f, 0), "Forest_Dome"), lBurner + Vector3.up * 12.2f, new Vector3(6.2f, 2.2f, 6.2f), new Color(0.3f, 0.28f, 0.27f));
            K.Lamp(lBurner + new Vector3(0, 1.2f, -5.2f), new Color(1f, 0.5f, 0.2f), 8f, 2f, false);
            K.Ramp(K.Env, G(30, -36), lBurner + new Vector3(0, 9f, 4.5f), 1.6f, new Color(0.35f, 0.32f, 0.3f), 0.3f);
            Vector3 lChimney = lHall.Local(-9f, 0, 6f);
            K.Mesh(K.Env, "Chimney", EditorAssets.SaveMesh(LowPolyMesh.Cylinder(12, 0.8f), "Forest_Chimney"), lChimney + Vector3.up * 9f, new Vector3(1.8f, 18f, 1.8f), new Color(0.55f, 0.32f, 0.26f), null, true).isStatic = true;
            K.Activity(ActivityType.Smoke, G(-10, -80, 0.1f), 0, Z);
            K.Activity(ActivityType.Phone, G(14, -82, 0.1f), 270, Z);
            Guard(Z, -46, -104.5f, 180, ItemKind.Smg);
            Guard(Z, 22, -84, 270, ItemKind.Shotgun);
            Walk(Z, ItemKind.Smg, 4f, new Vector2(-48, -80), new Vector2(38, -80), new Vector2(38, -38), new Vector2(-48, -38));
            Walk(Z, ItemKind.Shotgun, 3f, new Vector2(-52, -114), new Vector2(30, -114), new Vector2(30, -107), new Vector2(-52, -107));
            K.Weapon(ItemKind.Shotgun, G(-30, -64, 0.1f));
            K.Gadget(ItemKind.Brick, G(8, -44, 0.1f));
            for (int i = 0; i < 4; i++) { P("StreetLamp", -50 + i * 26f, -82, 180); K.Lamp(G(-50 + i * 26f, -83, 4.3f), WARM, 13f, 1.5f, false); }
        }

        /// <summary>A kit shipping container lying along <paramref name="pYaw"/> (cover), with a ladder on its end when asked.</summary>
        private void Container(Vector3 pAt, float pYaw, Color pTint, bool pLadder = false)
        {
            K.Prop("ShippingContainer", pAt, pYaw + 90f, pTint, MapKit.Collide.Box, true);
            if (pLadder) Ladder(pAt + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0, -3.25f), 2.7f, pYaw + 180f);
        }

        /// <summary>A deck of stacked logs (a pyramid, chained to stakes), cover and climbable.</summary>
        private void LogDeck(Vector3 pAt, float pYaw, int pRows)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            Vector3 lBase = G(pAt.x, pAt.z);
            for (int row = 0; row < pRows; row++)
                for (int i = 0; i < pRows - row + 2; i++)
                {
                    Vector3 p = lBase + r * new Vector3((i - (pRows - row + 1) * 0.5f) * 0.95f, row * 0.82f, 0);
                    K.NaturePiece("Log", p, pYaw, Rand(1.5f, 1.7f), GameArt.Vary(LOG, 0.08f, K.Rng), MapKit.Collide.None);
                }
            float lW = (pRows + 2) * 0.95f;
            K.Block(K.Env, "LogDeckCollider", lBase + Vector3.up * pRows * 0.41f, new Vector3(lW, pRows * 0.82f, 6.2f), LOG, Core.GameLayers.DEFAULT, true, r, true).GetComponent<MeshRenderer>().enabled = false;
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Stake", lBase + r * new Vector3(s * (lW * 0.5f + 0.15f), 1.2f, 0), new Vector3(0.2f, 2.4f, 0.2f), LOG * 0.8f, Core.GameLayers.DEFAULT, false, r);
        }
    }
}
