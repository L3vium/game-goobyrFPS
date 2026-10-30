using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Match;
using UnityEngine;
using Room = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Room;
using Roof = Com.IsartDigital.GoobyrFPS.EditorTools.MapKit.Roof;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Smugglers' Docks (zone 1): a walled container port on the south-west quay, crossed by the
    /// elevated Gulf Highway. Public: the corniche gate and the souk road. Restricted: the
    /// container lanes, the quay apron under the gantry cranes, the yard under the highway (the
    /// dockers' gym), the canteen yard. VIP: the deal in the Bluewater warehouse, the bridge of the
    /// cargo ship <i>Salt Meridian</i>, the harbour office. Ways in: the guarded gates, the highway
    /// deck (maintenance stair, containers stacked against it), the container stacks and their
    /// ladders, the crane zipline onto the warehouse roof, the gangway and the holds of the ship,
    /// and the storm drain that runs under the quay from the sea (also a tunnel extraction).
    /// </summary>
    public partial class MapGulf
    {
        /// <summary>Highway deck: centre line, top of the deck over the docks.</summary>
        private const float HWY_Z = -262f, HWY_TOP = 11f, HWY_WIDTH = 15f;
        private const float QUAY_Z = -362f;
        private const float SHIP_DECK = 6.5f, SHIP_X0 = -425f, SHIP_X1 = -320f, SHIP_Z = -377f;
        private static readonly Color CRANE = new Color(0.2f, 0.42f, 0.62f);
        private static readonly Color[] BOXES =
        {
            new Color(0.72f, 0.24f, 0.18f), new Color(0.2f, 0.42f, 0.6f), new Color(0.85f, 0.6f, 0.2f), new Color(0.3f, 0.5f, 0.35f),
            new Color(0.82f, 0.82f, 0.8f), new Color(0.55f, 0.3f, 0.45f), new Color(0.35f, 0.38f, 0.42f), new Color(0.15f, 0.55f, 0.6f)
        };

        private void PlanPort()
        {
            _zoneStage.Add(() => Flatten(R(-500, -366, -124, -166), YARD, 26f));
            _tame.Add(R(-500, -366, -120, -160));
            PaintRect(R(-500, -362, -124, -166), CONCRETE);
            PaintRect(R(-440, -360, -280, -336), new Color(0.6f, 0.59f, 0.55f));
            for (int s = 0; s < 2; s++) PaintRect(R(-440, (s == 0 ? -338f : -361f) - 0.3f, -280, (s == 0 ? -338f : -361f) + 0.3f), new Color(0.45f, 0.45f, 0.45f), 0);
            PaintRect(R(-500, HWY_Z - 9f, -124, HWY_Z + 9f), new Color(0.56f, 0.56f, 0.54f));
            // Lane markings between the container blocks (on ground-cell centres: the paint is per 4 m cell).
            foreach (float z in new[] { -234f, -214f, -298f, -318f })
                for (float x = -486f; x < -140f; x += 12f) PaintRect(R(x, z - 0.5f, x + 4f, z + 0.5f), new Color(0.9f, 0.78f, 0.3f), 0);
            foreach (float x in new[] { -446f, -394f, -346f })
                for (float z = -330f; z < -175f; z += 12f) if (z < -273f || z > -252f) PaintRect(R(x - 0.5f, z, x + 0.5f, z + 4f), new Color(0.9f, 0.78f, 0.3f), 0);
            Road(10f, ASPHALT, new Vector2(-128, -322), new Vector2(-128, -120));
            Road(9f, ASPHALT, new Vector2(-317, 70), new Vector2(-317, 20), new Vector2(-302, -60), new Vector2(-300, -166));
            // The storm drain: a hole in the ground over its course, the culvert is built of blocks.
            _carveStage.Add(() => Cells(R(-203.9f, -363.9f, -196.1f, -316.1f), (i, j) => _f[i, j] |= BUILT));

            MapKit.KitSpec lDeal = Lot("Bluewater Warehouse", "Stucco", -268f, -300f, 0, 12, 7, 2, Roof.Flat, new Color(0.72f, 0.76f, 0.78f), new Color(0.55f, 0.56f, 0.56f), 1, YARD, 3f);
            Doors(lDeal, 0, 5, 0, 6, 2, 3, 1, 1, 3, 4);
            lDeal.windowChance = 0.2f;
            lDeal.climb = new Vector2Int(2, 9);
            lDeal.rooms.Add(new[] { Room.Storage, Room.None });
            lDeal.rooms.Add(new[] { Room.Office, Room.Lounge });

            MapKit.KitSpec lStore = Lot("Dock Store", "Stucco", -250f, -200f, 0, 10, 6, 1, Roof.Flat, new Color(0.85f, 0.78f, 0.66f), new Color(0.55f, 0.56f, 0.56f), 1, YARD, 3f);
            Doors(lStore, 0, 4, 0, 5, 2, 2);
            lStore.windowChance = 0.2f;
            Floors(lStore, Room.Storage, Room.Storage);

            MapKit.KitSpec lCanteen = Lot("Dock Canteen", "Stucco", -300f, -195f, 0, 6, 4, 1, Roof.Flat, new Color(0.95f, 0.85f, 0.7f), new Color(0.55f, 0.56f, 0.56f), 1, YARD, 3f);
            Doors(lCanteen, 0, 2, 0, 3, 2, 1);
            lCanteen.climb = new Vector2Int(3, 1);
            Floors(lCanteen, Room.Dining, Room.Kitchen);

            MapKit.KitSpec lOffice = Lot("Harbour Office", "Classic", -170f, -326f, 0, 5, 4, 3, Roof.Flat, new Color(0.95f, 0.93f, 0.88f), new Color(0.55f, 0.56f, 0.56f), 1, YARD, 3f);
            Doors(lOffice, 0, 2, 2, 1);
            lOffice.tallWindows = true;
            lOffice.balconyChance = 0.25f;
            Floors(lOffice, Room.Office, Room.Bathroom, Room.Office, Room.Lounge, Room.Security, Room.Office);

            MapKit.KitSpec lPump = Lot("Pump House", "Stucco", -142f, -186f, 270, 3, 3, 1, Roof.Flat, new Color(0.8f, 0.78f, 0.72f), new Color(0.55f, 0.56f, 0.56f), 1, YARD, 3f);
            Doors(lPump, 0, 1);
            lPump.rooms.Add(new[] { Room.Utility });

            // The ship's superstructure: five decks of cabins, mess, galley and the bridge, facing the bow.
            MapKit.KitSpec lBridge = Register(new MapKit.KitSpec
            {
                name = "Salt Meridian Bridge", style = "Stucco", origin = new Vector3(-414f, SHIP_DECK + 0.05f, SHIP_Z), yaw = 270, width = 5, depth = 4, floors = 4,
                roof = Roof.Flat, wall = new Color(0.96f, 0.96f, 0.94f), roofTint = new Color(0.6f, 0.62f, 0.62f), zone = 1, seed = 3301, navigable = true, windowChance = 0.85f, balconyChance = 0f
            });
            Doors(lBridge, 0, 1, 0, 3);
            lBridge.upperDoors.Add(new Vector3Int(1, 1, 3));
            lBridge.upperDoors.Add(new Vector3Int(3, 2, 3));
            lBridge.floorColor = new Color(0.55f, 0.55f, 0.58f);
            Floors(lBridge, Room.Dining, Room.Kitchen, Room.Bedroom, Room.Bathroom, Room.Office, Room.Bedroom, Room.Security, Room.Office);
        }

        private void Port()
        {
            const int Z = 1;
            PortWalls();
            Containers();
            CargoShip();
            for (int i = 0; i < 3; i++) Gantry(-395f + i * 45f, i);
            DealWarehouse();
            DockYard();
            FuelDepot();
            Culvert();
            HarbourOffice();

            // Lanes, apron and the yard under the highway.
            Walk(Z, ItemKind.Smg, 4f, new Vector2(-486, -233.5f), new Vector2(-354, -233.5f), new Vector2(-345, -212), new Vector2(-486, -212));
            Walk(Z, ItemKind.Shotgun, 4f, new Vector2(-486, -297), new Vector2(-352, -297), new Vector2(-345, -318), new Vector2(-486, -318));
            Walk(Z, ItemKind.Smg, 3f, new Vector2(-435, -346), new Vector2(-282, -346), new Vector2(-282, -340), new Vector2(-435, -340));
            Walk(Z, ItemKind.Pistol, 4f, new Vector2(-480, -263), new Vector2(-232, -263), new Vector2(-232, -261), new Vector2(-480, -261));
            // Lookouts on two vantage stacks (four high, a ladder on their end).
            for (int s = 0; s < 2; s++)
            {
                Vector3 lBase = G(s == 0 ? -447f : -397f, s == 0 ? -244f : -284f);
                for (int k = 0; k < 4; k++) Container(lBase + Vector3.up * (k * 2.61f), 90f, BOXES[(k + s * 3) % BOXES.Length]);
                Ladder(lBase + new Vector3(-3.3f, 0, 0), 4 * 2.61f, 270f);
                K.Npc(NpcRole.Lookout, lBase + Vector3.up * (4 * 2.61f + 0.1f), s == 0 ? 0f : 180f, Z, ItemKind.Sniper);
            }
            K.Weapon(ItemKind.Sniper, G(-447f, -244f, 4 * 2.61f + 0.15f) + new Vector3(2f, 0, 0));
            // Straddle carriers parked in the lanes, one lifting a box.
            for (int i = 0; i < 3; i++) Straddle(new Vector3(-470f + i * 52f, YARD, i % 2 == 0 ? -297f : -233.5f), 90f, i == 1);
            K.Weapon(ItemKind.Smg, G(-452, -226, 0.15f));
            K.Weapon(ItemKind.Shotgun, G(-402, -305, 0.15f));
            K.Gadget(ItemKind.Coin, G(-430, -263, 0.1f));
            K.Gadget(ItemKind.SmokeGrenade, G(-360, -330, 0.1f));

            K.Zone(Z, K.Map.zoneNames[1], new Vector3(-310, 20, -272), new Vector3(390, 110, 250), G(-126, -250, 0.1f), new Color(0.28f, 0.32f, 0.38f), HatStyle.Cap,
                Spec("Harbour Office").origin + new Vector3(-4, 0.1f, 3));
        }

        /// <summary>The port wall: concrete, too tall to mantle, with the east and north gates, a gap under the highway.</summary>
        private void PortWalls()
        {
            const int Z = 1;
            Color lWall = new Color(0.78f, 0.76f, 0.72f);
            void Wall(float x0, float z0, float x1, float z1) => K.Fence(G(x0, z0, -0.3f), G(x1, z1, -0.3f), 3.1f, lWall, true);
            Wall(-490, -168, -306, -168);
            Wall(-294, -168, -136, -168);
            Wall(-134, -170, -134, -246);
            Wall(-134, -276, -134, -295);
            Wall(-134, -305, -134, -358);
            // The gates: barriers, booths, guards; the corniche gate gets the alarm board.
            foreach ((Vector2 lAt, float lYaw) in new[] { (new Vector2(-300, -168), 0f), (new Vector2(-134, -300), 90f) })
            {
                Vector3 g = G(lAt);
                Quaternion r = Quaternion.Euler(0, lYaw, 0);
                P("Barrier", g + r * new Vector3(-2.5f, 0, 0), lYaw + 90f, null, true);
                K.Block(K.Env, "Booth", g + r * new Vector3(5f, 1.3f, -3f), new Vector3(2.4f, 2.6f, 2.4f), new Color(0.9f, 0.9f, 0.88f), GameLayers.DEFAULT, true, r, true);
                K.Block(K.Env, "BoothRoof", g + r * new Vector3(5f, 2.75f, -3f), new Vector3(3f, 0.25f, 3f), CRANE);
                Guard(Z, g + r * new Vector3(2f, 0, -2f), lYaw + 180f, ItemKind.Smg);
                Guard(Z, g + r * new Vector3(-2f, 0, -3f), lYaw + 180f, ItemKind.Shotgun);
                K.Lamp(g + r * new Vector3(5f, 0, 2f), WARM, 14f, 1.6f, true);
            }
            K.Weapon(ItemKind.Pistol, G(-132, -307, 0.9f) + Vector3.right * -5f);
            // Floodlight masts over the yard.
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = G(-470f + i * 64f, i % 2 == 0 ? -233.5f : -297f);
                K.Block(K.Env, "LightMast", p + Vector3.up * 9f, new Vector3(0.5f, 18f, 0.5f), new Color(0.5f, 0.5f, 0.52f));
                K.Block(K.Env, "LightHead", p + Vector3.up * 18.2f, new Vector3(2.4f, 0.6f, 0.6f), new Color(0.3f, 0.3f, 0.32f));
                K.Lamp(p + Vector3.up * 17.6f, new Color(1f, 0.92f, 0.8f), 30f, 1.8f, false);
            }
        }

        /// <summary>Blocks of stacked containers with lanes between them; heights vary, some stacks have ladders.</summary>
        private void Containers()
        {
            System.Random r = new System.Random(911);
            (float x0, float z0, int slots, int rows)[] lBlocks =
            {
                (-490, -250, 6, 4), (-440, -250, 6, 4), (-390, -250, 6, 4), (-490, -228, 6, 4), (-440, -228, 6, 4), (-390, -228, 6, 4),
                (-490, -290, 6, 4), (-440, -290, 6, 4), (-390, -290, 6, 4), (-490, -314, 6, 4), (-440, -314, 6, 4), (-390, -314, 6, 4),
                (-340, -290, 4, 4), (-340, -314, 4, 4), (-232, -314, 6, 4), (-232, -290, 6, 4), (-330, -250, 6, 4),
                (-490, -206, 6, 4), (-440, -206, 6, 4), (-390, -206, 5, 4)
            };
            int lCount = 0;
            foreach ((float x0, float z0, int lSlots, int lRows) in lBlocks)
                for (int i = 0; i < lSlots; i++)
                    for (int row = 0; row < lRows; row++)
                    {
                        // Skip the two vantage stacks built by hand.
                        Vector2 c = new Vector2(x0 + i * 6.6f + 3.1f, z0 + row * 2.75f + 1.27f);
                        if (Vector2.Distance(c, new Vector2(-447, -244)) < 4f || Vector2.Distance(c, new Vector2(-397, -284)) < 4f) continue;
                        double h = r.NextDouble();
                        int lHeight = h < 0.22 ? 0 : h < 0.55 ? 1 : h < 0.82 ? 2 : h < 0.96 ? 3 : 4;
                        Color lTint = BOXES[r.Next(BOXES.Length)];
                        for (int k = 0; k < lHeight; k++)
                        {
                            Container(new Vector3(c.x, YARD + k * 2.61f, c.y), 90f, k == 0 ? lTint : BOXES[r.Next(BOXES.Length)], false, k == 0);
                            lCount++;
                        }
                        // A ladder on the lane end of some outer stacks.
                        if (lHeight >= 2 && (i == 0 || i == lSlots - 1) && r.NextDouble() < 0.35)
                            Ladder(new Vector3(i == 0 ? c.x - 3.3f : c.x + 3.3f, YARD, c.y), lHeight * 2.61f, i == 0 ? 270f : 90f);
                    }
            Debug.Log($"[Build] Map_Gulf docks: {lCount} containers");
        }

        /// <summary>A straddle carrier: four legs on wheels, two beams, the cab on top; it may hold a container between its legs.</summary>
        private void Straddle(Vector3 pAt, float pYaw, bool pLoaded)
        {
            Quaternion r = Quaternion.Euler(0, pYaw, 0);
            Color lBody = new Color(0.9f, 0.72f, 0.2f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 lLeg = pAt + r * new Vector3(i % 2 == 0 ? -2.2f : 2.2f, 0, i < 2 ? -4f : 4f);
                K.Block(K.Env, "StraddleLeg", lLeg + Vector3.up * 5f, new Vector3(0.5f, 10f, 0.6f), lBody, GameLayers.DEFAULT, true, r);
                K.Block(K.Env, "StraddleWheel", lLeg + Vector3.up * 0.6f, new Vector3(0.7f, 1.2f, 1.4f), new Color(0.15f, 0.15f, 0.16f), GameLayers.DEFAULT, false, r);
            }
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "StraddleBeam", pAt + r * new Vector3(s * 2.2f, 10.2f, 0), new Vector3(0.6f, 0.8f, 9f), lBody, GameLayers.DEFAULT, true, r);
            K.Block(K.Env, "StraddleCab", pAt + r * new Vector3(0, 11f, -3.6f), new Vector3(2.4f, 1.6f, 1.8f), new Color(0.92f, 0.92f, 0.9f), GameLayers.DEFAULT, true, r);
            if (pLoaded) Container(pAt + Vector3.up * 3.2f, pYaw + 90f, BOXES[5], false, false);
        }

        /// <summary>The cargo ship: hull, rounded bow, main deck with two open holds, hold stairs, the bridge house, the gangway.</summary>
        private void CargoShip()
        {
            const int Z = 1;
            Color lHullTop = new Color(0.16f, 0.24f, 0.36f), lHullBottom = new Color(0.6f, 0.18f, 0.15f), lDeck = new Color(0.42f, 0.44f, 0.42f);
            float zN = SHIP_Z + 11f, zS = SHIP_Z - 11f, lLen = SHIP_X1 - SHIP_X0, xMid = (SHIP_X0 + SHIP_X1) * 0.5f;
            // Hull sides (red below the waterline), the transom, the bow, the bulwark (open for the gangway).
            for (int s = 0; s < 2; s++)
            {
                float z = s == 0 ? zN - 0.4f : zS + 0.4f;
                K.Block(K.Env, "HullLow", new Vector3(xMid, (SEA_FLOOR + SEA + 0.4f) * 0.5f, z), new Vector3(lLen, SEA + 0.4f - SEA_FLOOR, 0.8f), lHullBottom);
                K.Block(K.Env, "HullHigh", new Vector3(xMid, (SEA + 0.4f + SHIP_DECK) * 0.5f, z), new Vector3(lLen, SHIP_DECK - SEA - 0.4f, 0.8f), lHullTop);
                float zb = s == 0 ? zN - 0.15f : zS + 0.15f;
                if (s == 0)
                {
                    K.Block(K.Env, "Bulwark", new Vector3((SHIP_X0 + -371.6f) * 0.5f, SHIP_DECK + 0.55f, zb), new Vector3(-371.6f - SHIP_X0, 1.1f, 0.3f), lHullTop);
                    K.Block(K.Env, "Bulwark", new Vector3((-368.4f + SHIP_X1) * 0.5f, SHIP_DECK + 0.55f, zb), new Vector3(SHIP_X1 - -368.4f, 1.1f, 0.3f), lHullTop);
                }
                else K.Block(K.Env, "Bulwark", new Vector3(xMid, SHIP_DECK + 0.55f, zb), new Vector3(lLen, 1.1f, 0.3f), lHullTop);
                K.Block(K.Env, "Boot", new Vector3(xMid, SEA + 0.5f, s == 0 ? zN + 0.02f : zS - 0.02f), new Vector3(lLen, 0.25f, 0.05f), new Color(0.9f, 0.9f, 0.88f), GameLayers.DEFAULT, false);
            }
            K.Block(K.Env, "Transom", new Vector3(SHIP_X0 + 0.4f, (SEA_FLOOR + SHIP_DECK + 1.1f) * 0.5f, SHIP_Z), new Vector3(0.8f, SHIP_DECK + 1.1f - SEA_FLOOR, 22f), lHullTop);
            K.Mesh(K.Env, "Bow", CylMesh, new Vector3(SHIP_X1, (SEA_FLOOR + SHIP_DECK + 1.1f) * 0.5f, SHIP_Z), new Vector3(30f, SHIP_DECK + 1.1f - SEA_FLOOR, 22f), lHullTop, null, true).isStatic = true;
            K.Mesh(K.Env, "BowRed", CylMesh, new Vector3(SHIP_X1 + 0.05f, (SEA_FLOOR + SEA + 0.4f) * 0.5f, SHIP_Z), new Vector3(30.1f, SEA + 0.4f - SEA_FLOOR, 22.1f), lHullBottom);
            K.Stairs(K.Env, new Vector3(-340f, SHIP_DECK, SHIP_Z), new Vector3(-335.3f, SHIP_DECK + 1.1f, SHIP_Z), 3f, lDeck);
            K.Block(K.Env, "ShipName", new Vector3(-318f, 5f, zN + 0.1f), new Vector3(9f, 1f, 0.05f), new Color(0.95f, 0.95f, 0.92f), GameLayers.DEFAULT, false);

            // Main deck around the two holds; hold floors, walls and coamings; stairs down into each hold.
            float[] lHoldX = { -398f, -374f, -366f, -342f };
            float hzS = SHIP_Z - 6f, hzN = SHIP_Z + 6f;
            void DeckPart(float x0, float x1, float z0, float z1) => K.Block(K.Env, "ShipDeck", new Vector3((x0 + x1) * 0.5f, SHIP_DECK - 0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), lDeck);
            DeckPart(SHIP_X0 + 0.8f, lHoldX[0], zS + 0.8f, zN - 0.8f);
            DeckPart(lHoldX[1], lHoldX[2], zS + 0.8f, zN - 0.8f);
            DeckPart(lHoldX[3], SHIP_X1, zS + 0.8f, zN - 0.8f);
            for (int h = 0; h < 2; h++)
            {
                float x0 = lHoldX[h * 2], x1 = lHoldX[h * 2 + 1];
                DeckPart(x0, x1, zS + 0.8f, hzS);
                DeckPart(x0, x1, hzN, zN - 0.8f);
                K.Block(K.Env, "HoldFloor", new Vector3((x0 + x1) * 0.5f, -0.9f, SHIP_Z), new Vector3(x1 - x0, 0.8f, 12f), new Color(0.35f, 0.33f, 0.3f));
                for (int s = 0; s < 2; s++)
                {
                    K.Block(K.Env, "HoldWall", new Vector3(s == 0 ? x0 - 0.2f : x1 + 0.2f, (SHIP_DECK - 0.5f) * 0.5f - 0.25f, SHIP_Z), new Vector3(0.4f, SHIP_DECK + 0.5f, 12.4f), new Color(0.5f, 0.48f, 0.44f));
                    K.Block(K.Env, "HoldWall", new Vector3((x0 + x1) * 0.5f, (SHIP_DECK - 0.5f) * 0.5f - 0.25f, s == 0 ? hzS - 0.2f : hzN + 0.2f), new Vector3(x1 - x0, SHIP_DECK + 0.5f, 0.4f), new Color(0.5f, 0.48f, 0.44f));
                    // Coamings: the west one has a gap where the stairs arrive.
                    K.Block(K.Env, "Coaming", new Vector3((x0 + x1) * 0.5f, SHIP_DECK + 0.45f, s == 0 ? hzS - 0.2f : hzN + 0.2f), new Vector3(x1 - x0 + 0.8f, 0.9f, 0.4f), new Color(0.75f, 0.55f, 0.15f));
                }
                K.Block(K.Env, "Coaming", new Vector3(x1 + 0.2f, SHIP_DECK + 0.45f, SHIP_Z), new Vector3(0.4f, 0.9f, 12f), new Color(0.75f, 0.55f, 0.15f));
                K.Block(K.Env, "Coaming", new Vector3(x0 - 0.2f, SHIP_DECK + 0.45f, SHIP_Z + 2.6f), new Vector3(0.4f, 0.9f, 6.8f), new Color(0.75f, 0.55f, 0.15f));
                K.Stairs(K.Env, new Vector3(x0 + 15f, -0.5f, hzS + 1.6f), new Vector3(x0 + 0.2f, SHIP_DECK, hzS + 1.6f), 2.4f, new Color(0.6f, 0.45f, 0.15f));
                // Cargo in the hold: two-high stacks along the far wall, a crate pile.
                for (int k = 0; k < 3; k++)
                {
                    Container(new Vector3(x0 + 4f + k * 7f, -0.5f, hzN - 1.6f), 90f, BOXES[(k + h) % BOXES.Length]);
                    if (k != 1) Container(new Vector3(x0 + 4f + k * 7f, -0.5f + 2.61f, hzN - 1.6f), 90f, BOXES[(k + h + 3) % BOXES.Length]);
                }
                Clutter(new Vector3(x1 - 3f, -0.5f, SHIP_Z), 0, h + 1);
                K.Lamp(new Vector3((x0 + x1) * 0.5f, 4.5f, SHIP_Z), WARM, 14f, 1.2f, false);
            }
            // Deck cargo on the fore deck and the hatch covers stacked by the holds.
            for (int l = 0; l < 3; l++) Container(new Vector3(-403f, SHIP_DECK + (l == 2 ? 2.61f : 0f), SHIP_Z - 5f + (l == 1 ? 2.8f : 0f)), 90f, BOXES[(l + 4) % BOXES.Length]);
            K.Block(K.Env, "HatchCovers", new Vector3(-370f, SHIP_DECK + 0.6f, zS + 2.5f), new Vector3(7f, 1.2f, 2.6f), new Color(0.75f, 0.55f, 0.15f), GameLayers.DEFAULT, true, null, true);

            // The bridge house, its wings, the funnel and the mast.
            MapKit.KitBuilding b = K.Building(Spec("Salt Meridian Bridge"));
            float lTop = RoofY(b.spec);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 lWing = new Vector3(-414f, SHIP_DECK + 3 * MapKit.STOREY + 0.05f, SHIP_Z + s * 9.6f);
                Deck(lWing, new Vector2(4f, 3.6f), 0f, 0.25f, new Color(0.6f, 0.62f, 0.62f), 0f);
                K.Block(K.Env, "WingRail", lWing + new Vector3(0, 0.55f, s * 1.75f), new Vector3(4f, 1.1f, 0.1f), Color.white);
                K.Block(K.Env, "WingRail", lWing + new Vector3(2f, 0.55f, 0), new Vector3(0.1f, 1.1f, 3.6f), Color.white);
                K.Block(K.Env, "WingRail", lWing + new Vector3(-2f, 0.55f, 0), new Vector3(0.1f, 1.1f, 3.6f), Color.white);
            }
            K.Mesh(K.Env, "Funnel", CylMesh, new Vector3(-417f, lTop + 3.5f, SHIP_Z - 2.5f), new Vector3(4.2f, 7f, 5.5f), new Color(0.85f, 0.25f, 0.2f), null, true).isStatic = true;
            K.Mesh(K.Env, "FunnelTop", CylMesh, new Vector3(-417f, lTop + 7.2f, SHIP_Z - 2.5f), new Vector3(4.3f, 0.6f, 5.6f), new Color(0.12f, 0.12f, 0.13f));
            K.Block(K.Env, "Mast", new Vector3(-412f, lTop + 4f, SHIP_Z), new Vector3(0.3f, 8f, 0.3f), new Color(0.9f, 0.9f, 0.9f));
            K.Block(K.Env, "Radar", new Vector3(-412f, lTop + 6.5f, SHIP_Z), new Vector3(0.3f, 0.3f, 3.2f), new Color(0.9f, 0.9f, 0.9f));
            K.Prop("Boat", new Vector3(-410f, SHIP_DECK + MapKit.STOREY + 0.6f, zS - 1.2f), 90f, new Color(1f, 0.5f, 0.15f), MapKit.Collide.Box);
            K.Npc(NpcRole.Lookout, b.Local(2f, 3 * MapKit.STOREY + 0.1f, -3f), 90f, Z, ItemKind.Sniper);
            K.Npc(NpcRole.Guard, b.Local(-3f, 0.1f, -0.6f), 90f, Z, ItemKind.Smg);
            K.Npc(NpcRole.Guard, b.Local(-2f, 2 * MapKit.STOREY + 0.1f, 2.5f), 90f, Z, ItemKind.Pistol);
            K.Weapon(ItemKind.Smg, b.Local(-5f, 2 * MapKit.STOREY + 0.15f, -4f));
            K.Weapon(ItemKind.Shotgun, b.Local(4f, 0.15f, -4.5f));

            // The gangway: a ramp along the quay (between the crane legs), then a short bridge over the hull side onto the deck.
            Color lGang = new Color(0.55f, 0.55f, 0.58f);
            Vector3 lQuay = new Vector3(-386.4f, YARD, QUAY_Z + 0.8f), lGangTop = new Vector3(-370f, SHIP_DECK + 0.12f, QUAY_Z + 0.8f), lShip = new Vector3(-370f, SHIP_DECK + 0.02f, zN - 1.6f);
            K.Ramp(K.Env, lQuay, lGangTop, 2.2f, lGang, 0.25f);
            K.Ramp(K.Env, lGangTop, lShip, 2.4f, lGang, 0.25f);
            for (int s = -1; s <= 1; s += 2)
            {
                K.Block(K.Env, "GangRail", (lQuay + lGangTop) * 0.5f + new Vector3(0, 0.9f, s * 1.15f), new Vector3(0.08f, 0.08f, Vector3.Distance(lQuay, lGangTop)), Color.white, GameLayers.DEFAULT, false, Quaternion.LookRotation(lGangTop - lQuay));
                K.Block(K.Env, "GangRail", (lGangTop + lShip) * 0.5f + new Vector3(s * 1.25f, 0.9f, 0), new Vector3(0.08f, 0.08f, Vector3.Distance(lGangTop, lShip)), Color.white, GameLayers.DEFAULT, false, Quaternion.LookRotation(lShip - lGangTop));
            }
            for (int k = 1; k < 4; k++)
            {
                Vector3 lLeg = Vector3.Lerp(lQuay, lGangTop, k / 4f);
                K.Block(K.Env, "GangLeg", new Vector3(lLeg.x, (YARD + lLeg.y) * 0.5f, lLeg.z), new Vector3(0.25f, lLeg.y - YARD, 0.25f), lGang * 0.8f, GameLayers.DEFAULT, false);
            }
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = new Vector3(SHIP_X0 + 12f + i * 30f, YARD + 0.6f, QUAY_Z + 1f), c = new Vector3(SHIP_X0 + 8f + i * 30f, SHIP_DECK - 0.5f, zN);
                K.Prop("Bollard", new Vector3(a.x, YARD, a.z), 0);
                K.Block(K.Props, "Mooring", (a + c) * 0.5f, new Vector3(0.12f, 0.12f, Vector3.Distance(a, c)), new Color(0.85f, 0.8f, 0.65f), GameLayers.DEFAULT, false, Quaternion.LookRotation(c - a));
            }
            for (int i = 0; i < 4; i++) K.Lamp(new Vector3(-400f + i * 22f, SHIP_DECK + 4f, SHIP_Z + (i % 2 == 0 ? 8f : -8f)), WARM, 16f, 1.3f, false);
            Guard(Z, -386, QUAY_Z + 4f, 0, ItemKind.Smg);
            K.Patrol(Z, ItemKind.Smg, 4f, new Vector3(-405, SHIP_DECK + 0.1f, zN - 2f), new Vector3(-345, SHIP_DECK + 0.1f, zN - 2f), new Vector3(-330, SHIP_DECK + 1.2f, SHIP_Z), new Vector3(-345, SHIP_DECK + 0.1f, zS + 2f), new Vector3(-405, SHIP_DECK + 0.1f, zS + 2f));
            K.Npc(NpcRole.Guard, new Vector3(-386f, -0.4f, SHIP_Z + 1f), 90f, Z, ItemKind.Shotgun);
            // Old Tarek addresses his crew on the fore deck; a smoke by the rail.
            K.Activity(ActivityType.Speech, new Vector3(-338f, SHIP_DECK + 0.1f, SHIP_Z - 6f), 270f, Z, 18f);
            K.Activity(ActivityType.Smoke, new Vector3(-400f, SHIP_DECK + 0.1f, zS + 1.8f), 180f, Z);
            K.Activity(ActivityType.Inspect, new Vector3(-390f, -0.4f, SHIP_Z + 3f), 0f, Z);
            K.Gadget(ItemKind.C4, new Vector3(-372f, -0.35f, SHIP_Z - 4f));
        }

        /// <summary>A ship-to-shore gantry crane straddling the quay, its boom over the ship; a ladder to the walkway.</summary>
        private void Gantry(float pX, int pIndex)
        {
            const int Z = 1;
            const float TOP = 34f;
            float zA = -338f, zB = QUAY_Z + 1.2f;
            for (int i = 0; i < 4; i++)
            {
                float x = pX + (i % 2 == 0 ? -7f : 7f), z = i < 2 ? zA : zB;
                K.Block(K.Env, "CraneLeg", new Vector3(x, YARD + TOP * 0.5f, z), new Vector3(1.2f, TOP, 1.2f), CRANE);
                K.Block(K.Env, "Bogie", new Vector3(x, YARD + 0.6f, z), new Vector3(1.6f, 1.2f, 3.2f), new Color(0.3f, 0.3f, 0.32f));
            }
            for (int s = 0; s < 2; s++)
            {
                float x = pX + (s == 0 ? -7f : 7f);
                K.Block(K.Env, "Sill", new Vector3(x, YARD + 9f, (zA + zB) * 0.5f), new Vector3(0.9f, 1.2f, zA - zB), CRANE);
                K.Block(K.Env, "Portal", new Vector3(pX, YARD + TOP - 4f, s == 0 ? zA : zB), new Vector3(15.2f, 1.5f, 1.5f), CRANE);
                K.Block(K.Env, "Girder", new Vector3(pX + (s == 0 ? -2.2f : 2.2f), YARD + TOP + 1f, -366f), new Vector3(1.2f, 2f, 96f), CRANE);
            }
            // The walkway on top (between the girders), its rails, the machinery house, the trolley and its load.
            float lWalk = YARD + TOP + 2.15f;
            K.Block(K.Env, "CraneWalk", new Vector3(pX, lWalk - 0.15f, -342f), new Vector3(5.6f, 0.3f, 28f), new Color(0.4f, 0.42f, 0.44f));
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "CraneRail", new Vector3(pX + s * 2.75f, lWalk + 0.55f, -342f), new Vector3(0.1f, 1.1f, 28f), new Color(0.9f, 0.75f, 0.2f));
            K.Block(K.Env, "Machinery", new Vector3(pX, lWalk + 2.5f, -322f), new Vector3(7f, 5f, 8f), new Color(0.92f, 0.92f, 0.9f), GameLayers.DEFAULT, true, null, true);
            K.Block(K.Env, "Trolley", new Vector3(pX, YARD + TOP - 0.6f, -384f - pIndex * 4f), new Vector3(5.2f, 1.4f, 4f), new Color(0.3f, 0.3f, 0.32f));
            float lHang = SHIP_DECK + 4.5f + pIndex * 2f;
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "Cable", new Vector3(pX + s * 1.5f, (YARD + TOP - 1.3f + lHang + 2.9f) * 0.5f, -384f - pIndex * 4f), new Vector3(0.06f, YARD + TOP - 1.3f - lHang - 2.9f, 0.06f), new Color(0.15f, 0.15f, 0.15f), GameLayers.DEFAULT, false);
            K.Block(K.Env, "Spreader", new Vector3(pX, lHang + 2.9f, -384f - pIndex * 4f), new Vector3(6.4f, 0.4f, 2.6f), new Color(0.85f, 0.65f, 0.15f));
            Container(new Vector3(pX, lHang, -384f - pIndex * 4f), 90f, BOXES[pIndex * 2 + 1]);
            K.Block(K.Env, "Cab", new Vector3(pX + 2.2f, YARD + TOP - 1.8f, -356f), new Vector3(2.4f, 2.4f, 2.8f), new Color(0.92f, 0.92f, 0.9f));
            Ladder(new Vector3(pX + 7f, YARD, zA + 0.75f), TOP + 2.2f, 0f);
            K.Block(K.Env, "LadderLanding", new Vector3(pX + 4.6f, lWalk - 0.15f, zA + 1f), new Vector3(4.2f, 0.3f, 1.4f), new Color(0.4f, 0.42f, 0.44f));
            K.Lamp(new Vector3(pX, lWalk + 3f, -342f), new Color(1f, 0.3f, 0.25f), 8f, 1.5f, false);
            if (pIndex != 1) K.Npc(NpcRole.Lookout, new Vector3(pX - 1f, lWalk + 0.1f, -350f), 180f, Z, ItemKind.Sniper);
            if (pIndex == 2) K.UniqueWeapon(ItemKind.RocketLauncher, new Vector3(pX + 0.5f, lWalk + 0.15f, -330f), "Gantry crane walkway");
            if (pIndex == 0) K.Weapon(ItemKind.Sniper, new Vector3(pX + 1f, lWalk + 0.15f, -345f));
            // The middle crane ziplines down onto the Bluewater warehouse roof.
            if (pIndex == 1)
            {
                MapKit.KitSpec w = Spec("Bluewater Warehouse");
                K.Zipline(new Vector3(pX + 1.5f, lWalk + 0.05f, -334f), new Vector3(w.origin.x - 8f, RoofY(w) + 0.05f, w.origin.z + 3f));
            }
        }

        /// <summary>The Bluewater warehouse: a deal on the ground floor (two cars nose to nose, the cash, the guns), offices upstairs.</summary>
        private void DealWarehouse()
        {
            const int Z = 1;
            MapKit.KitBuilding w = K.Building(Spec("Bluewater Warehouse"));
            Vector3 L(float x, float z, float y = 0f) => w.Local(x, y, z);
            K.Car(L(-8f, 5f), 90f, new Color(0.12f, 0.12f, 0.14f));
            K.Car(L(4f, 5f), 270f, new Color(0.85f, 0.85f, 0.88f));
            K.Prop("Table", L(-2f, 5f), 0, PLANK, MapKit.Collide.Box, true);
            K.Block(K.Props, "Briefcase", L(-2.4f, 5f, 0.88f), new Vector3(0.55f, 0.14f, 0.4f), new Color(0.2f, 0.15f, 0.12f), GameLayers.DEFAULT, false);
            K.Block(K.Props, "Cash", L(-1.6f, 5f, 0.84f), new Vector3(0.5f, 0.08f, 0.35f), new Color(0.45f, 0.62f, 0.35f), GameLayers.DEFAULT, false);
            for (int i = 0; i < 3; i++)
            {
                K.Crate(L(-1f + i * 1.2f, 8.5f), 1.3f);
                K.Block(K.Props, "Rifles", L(-1f + i * 1.2f, 8.5f, 1.32f), new Vector3(0.9f, 0.08f, 0.5f), new Color(0.2f, 0.2f, 0.22f), GameLayers.DEFAULT, false);
            }
            K.Activity(ActivityType.Inspect, L(-2f, 3.6f, 0.1f), w.Yaw, Z);
            K.Activity(ActivityType.Inspect, L(0.2f, 7.2f, 0.1f), w.Yaw + 180f, Z);
            K.DrinksOn(L(-6f, 8.6f, 1.32f), w.Yaw, Z, 2);
            K.Crate(L(-6f, 8.6f), 1.3f);
            for (int i = 0; i < 4; i++) K.Npc(NpcRole.Guard, L(-10f + i * 6f, i % 2 == 0 ? 2f : 9f, 0.1f), w.Yaw + (i % 2 == 0 ? 0f : 180f), Z, i % 2 == 0 ? ItemKind.Smg : ItemKind.Shotgun);
            K.Npc(NpcRole.Lookout, new Vector3(w.root.position.x + 10f, RoofY(w.spec) + 0.1f, w.root.position.z - 6f), 180f, Z, ItemKind.Sniper);
            K.Weapon(ItemKind.Smg, L(14f, -6f, MapKit.STOREY + 0.15f));
            K.Gadget(ItemKind.Poison, L(-12f, -4f, MapKit.STOREY + 0.95f));
            // Outside: the smokers by the side door, a parked van, crates waiting.
            K.Activity(ActivityType.Smoke, G(w.root.position.x + 20f, w.root.position.z - 6f, 0.1f), 90f, Z);
            K.Activity(ActivityType.Phone, G(w.root.position.x - 20f, w.root.position.z + 2f, 0.1f), 270f, Z);
            K.Car(G(w.root.position.x + 6f, w.root.position.z - 15f), 90f, new Color(0.9f, 0.9f, 0.88f));
            for (int i = 0; i < 3; i++) Clutter(G(w.root.position.x - 14f + i * 5f, w.root.position.z - 14f), 0, i + 2);
        }

        /// <summary>The north yard: the canteen and the matriarch's crew talk, the dock store, the forger's container, the gym under the highway.</summary>
        private void DockYard()
        {
            const int Z = 1;
            MapKit.KitBuilding c = K.Building(Spec("Dock Canteen"));
            K.ToiletKit(c.Local(-7.8f, 0, -4f), 90f, Z);
            Awnings(c, 0, FABRIC[0], 1, 2, 3, 4);
            for (int i = 0; i < 3; i++)
            {
                Vector3 t = G(-306f + i * 6f, -205f);
                K.Prop("Table", t, 0, PLANK, MapKit.Collide.Box, true);
                K.Prop("Bench", t + new Vector3(0, 0, 1.1f), 180, PLANK);
                K.Prop("Bench", t + new Vector3(0, 0, -1.1f), 0, PLANK);
                Shade(t, 0, FABRIC[(i + 2) % FABRIC.Length], false);
            }
            K.DrinksOn(G(-300, -205, 0.78f), 0, Z, 3);
            // The crew talk: a stage of pallets and crates in the yard, the crews' benches.
            Vector3 lStage = G(-300, -214);
            for (int i = 0; i < 4; i++) K.Prop("Pallet", lStage + new Vector3(-1.5f + (i % 2) * 1.25f, i / 2 * 0.15f, (i / 2 == 0 ? -0.5f : 0.5f)), 0);
            K.Block(K.Env, "CrewStage", lStage + Vector3.up * 0.3f, new Vector3(5f, 0.6f, 3f), PLANK * 0.8f);
            K.Activity(ActivityType.Speech, lStage + Vector3.up * 0.65f, 0f, Z, 18f);
            for (int i = 0; i < 4; i++) P("Bench", -306f + i * 4f, -208.5f, 180f);
            Guard(Z, -295, -214, 0, ItemKind.Smg);
            Guard(Z, -305, -190, 0, ItemKind.Pistol);
            K.Weapon(ItemKind.Shotgun, c.Local(6f, 0.15f, 3f));

            MapKit.KitBuilding s = K.Building(Spec("Dock Store"));
            K.Activity(ActivityType.Inspect, s.Local(-6f, 0.1f, -2f), 0, Z);
            K.Gadget(ItemKind.C4, s.Local(8f, 0.15f, 4f));

            // The forger's studio: an open container with a desk, a press and lamps, a portable toilet next to it.
            Vector3 f = G(-338, -196);
            Color lBox = BOXES[7];
            K.Block(K.Env, "StudioFloor", f + Vector3.up * 0.1f, new Vector3(6.2f, 0.2f, 2.5f), lBox * 0.8f);
            K.Block(K.Env, "StudioRoof", f + Vector3.up * 2.55f, new Vector3(6.2f, 0.12f, 2.5f), lBox);
            K.Block(K.Env, "StudioBack", f + new Vector3(0, 1.3f, 1.2f), new Vector3(6.2f, 2.6f, 0.1f), lBox);
            for (int k = -1; k <= 1; k += 2) K.Block(K.Env, "StudioSide", f + new Vector3(k * 3.05f, 1.3f, 0), new Vector3(0.1f, 2.6f, 2.5f), lBox);
            K.Prop("Desk", f + new Vector3(-1.2f, 0.2f, 0.5f), 180, null, MapKit.Collide.Box, true);
            K.Prop("OfficeChair", f + new Vector3(-1.2f, 0.2f, -0.2f), 180, null);
            K.Block(K.Props, "Press", f + new Vector3(1.8f, 0.75f, 0.6f), new Vector3(0.9f, 1.1f, 0.8f), new Color(0.3f, 0.3f, 0.32f));
            K.Lamp(f + new Vector3(0, 2.3f, 0.4f), WARM, 6f, 1.2f, false);
            K.Activity(ActivityType.Office, f + new Vector3(-1.2f, 0.3f, -0.9f), 0, Z);
            K.Activity(ActivityType.Phone, G(-338, -201, 0.1f), 180, Z);
            K.Prop("Table", G(-332, -201), 0, PLANK, MapKit.Collide.Box, true);
            K.DrinksOn(G(-332, -201, 0.78f), 0, Z, 2);
            K.Block(K.Env, "PortaLoo", G(-344, -200, 1.2f), new Vector3(1.3f, 2.4f, 1.3f), new Color(0.2f, 0.5f, 0.35f));
            K.ToiletKit(G(-344, -202.2f), 180, Z);
            Guard(Z, -334, -203, 180, ItemKind.Pistol);

            // The dockers' gym under the highway: a boxing ring, benches, bags hung from the deck.
            Vector3 g = G(-236, HWY_Z);
            K.Block(K.Env, "Ring", g + Vector3.up * 0.4f, new Vector3(6f, 0.8f, 6f), new Color(0.25f, 0.35f, 0.55f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 lPost = g + new Vector3(i % 2 == 0 ? -2.9f : 2.9f, 1.4f, i < 2 ? -2.9f : 2.9f);
                K.Block(K.Env, "RingPost", lPost, new Vector3(0.15f, 1.3f, 0.15f), new Color(0.85f, 0.2f, 0.2f));
            }
            for (int k = 0; k < 2; k++)
                for (int e = 0; e < 4; e++)
                {
                    Quaternion r = Quaternion.Euler(0, e * 90f, 0);
                    K.Block(K.Env, "Rope", g + Vector3.up * (1.3f + k * 0.45f) + r * new Vector3(0, 0, 2.9f), new Vector3(5.8f, 0.04f, 0.04f), Color.white, GameLayers.DEFAULT, false, r);
                }
            for (int i = 0; i < 3; i++)
            {
                Vector3 lBag = g + new Vector3(-12f + i * 3.5f, 0, -3f);
                float lBagTop = lBag.y + 2.25f, lDeckBottom = HWY_TOP - 1.2f;
                K.Block(K.Props, "BagChain", new Vector3(lBag.x, (lBagTop + lDeckBottom) * 0.5f, lBag.z), new Vector3(0.04f, lDeckBottom - lBagTop, 0.04f), new Color(0.2f, 0.2f, 0.2f), GameLayers.DEFAULT, false);
                K.Mesh(K.Props, "PunchBag", CylMesh, lBag + Vector3.up * 1.6f, new Vector3(0.5f, 1.3f, 0.5f), new Color(0.55f, 0.15f, 0.12f), null, true);
            }
            K.Prop("Bench", g + new Vector3(8f, 0, -3f), 0, null);
            K.Prop("Bench", g + new Vector3(8f, 0, 3f), 0, null);
            K.Activity(ActivityType.Workout, g + new Vector3(0, 0.1f, -4.3f), 0, Z);
            K.Activity(ActivityType.Workout, g + new Vector3(-8.5f, 0.1f, -1.5f), 0, Z);
            K.Activity(ActivityType.Drink, g + new Vector3(10f, 0.1f, 0), 270, Z);
            K.Prop("Table", g + new Vector3(10f, 0, 1.2f), 0, null, MapKit.Collide.Box, true);
            K.DrinksOn(g + new Vector3(10f, 0.78f, 1.2f), 0, Z, 2);
            for (int i = 0; i < 3; i++) K.Lamp(g + new Vector3(-10f + i * 10f, HWY_TOP - 1.6f - YARD, 0), WARM, 12f, 1.4f, false);
            Guard(Z, g + new Vector3(5f, 0, 5f), 225, ItemKind.Smg);
        }

        /// <summary>The fuel depot: four tanks with ladders and a catwalk, pipes, the pump house, red barrels by the patrol.</summary>
        private void FuelDepot()
        {
            const int Z = 1;
            Vector2[] lTanks = { new Vector2(-195, -228), new Vector2(-163, -228), new Vector2(-195, -196), new Vector2(-163, -196) };
            const float H = 11f, D = 14f;
            foreach (Vector2 t in lTanks)
            {
                Vector3 b = G(t);
                K.Mesh(K.Env, "Tank", CylMesh, b + Vector3.up * (H * 0.5f), new Vector3(D, H, D), new Color(0.92f, 0.92f, 0.9f), null, true).isStatic = true;
                K.Mesh(K.Env, "TankBand", CylMesh, b + Vector3.up * 3f, new Vector3(D + 0.1f, 0.5f, D + 0.1f), new Color(0.8f, 0.22f, 0.18f));
                K.Mesh(K.Env, "TankRoof", ConeMesh, b + Vector3.up * H, new Vector3(D, 1.4f, D), new Color(0.82f, 0.82f, 0.8f), null, true);
                K.Mesh(K.Env, "Bund", CylMesh, b + Vector3.up * 0.4f, new Vector3(D + 3f, 0.8f, D + 3f), new Color(0.6f, 0.58f, 0.55f), null, true);
            }
            // Ladder up the first tank, catwalks joining the tank roofs (players only).
            Ladder(G(-195, -228 + D * 0.5f + 0.2f, 0.8f), H - 0.5f, 0f, "Classic");
            for (int i = 0; i < 2; i++)
            {
                Vector3 a = G(lTanks[i * 2].x, lTanks[i * 2].y, H + 0.9f), b = G(lTanks[i * 2 + 1].x, lTanks[i * 2 + 1].y, H + 0.9f);
                Deck((a + b) * 0.5f, new Vector2(1.6f, 34f), 90f, 0.2f, new Color(0.45f, 0.45f, 0.48f), 0f);
            }
            Deck(G(-195, -212, H + 0.9f), new Vector2(1.6f, 22f), 0f, 0.2f, new Color(0.45f, 0.45f, 0.48f), 0f);
            for (int i = 0; i < 6; i++) K.Mesh(K.Env, "Pipe", CylMesh, G(-180, -244 + i * 0.9f, 0.6f), new Vector3(0.5f, 60f, 0.5f), new Color(0.75f, 0.7f, 0.2f), Quaternion.Euler(0, 0, 90));
            K.Car(G(-150, -212), 0, new Color(0.85f, 0.2f, 0.18f));
            for (int i = 0; i < 6; i++) K.Barrel(G(-208f + (i % 3) * 0.75f, -212f + (i / 3) * 0.8f), true);
            for (int i = 0; i < 4; i++) K.Prop("GasBottle", G(-146f + i * 0.4f, -230f), 0, null, MapKit.Collide.Box);
            K.Fence(G(-214, -246), G(-214, -174), 2.4f, new Color(0.55f, 0.56f, 0.58f));
            K.Fence(G(-214, -174), G(-150, -174), 2.4f, new Color(0.55f, 0.56f, 0.58f));
            MapKit.KitBuilding p = K.Building(Spec("Pump House"));
            K.UniqueWeapon(ItemKind.Flamethrower, p.Local(-2f, 0.15f, 2f), "Fuel depot pump house");
            Guard(Z, -212, -205, 270, ItemKind.Shotgun);
            Guard(Z, -146, -195, 0, ItemKind.Smg);
            Walk(Z, ItemKind.Smg, 3f, new Vector2(-208, -244), new Vector2(-150, -244), new Vector2(-150, -180), new Vector2(-208, -180));
            K.Weapon(ItemKind.Smg, G(-176, -212, 0.15f));
        }

        /// <summary>The storm drain under the quay: an open pit with stairs, a culvert to the sea, the tunnel extraction at its mouth.</summary>
        private void Culvert()
        {
            const int Z = 1;
            const float FLOOR = -1.3f, X = -200f;
            float zIn = -316f, zPit = -324f, zOut = -364.6f;
            K.Hole(R(-203.9f, -363.9f, -196.1f, -316.1f));
            Color lConcrete = new Color(0.55f, 0.55f, 0.53f);
            K.Block(K.Env, "DrainFloor", new Vector3(X, FLOOR - 0.4f, (zIn + zOut) * 0.5f), new Vector3(8f, 0.8f, zIn - zOut), lConcrete * 0.85f);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "DrainWall", new Vector3(X + s * 3f, (FLOOR + YARD) * 0.5f, (zIn + zOut) * 0.5f), new Vector3(2f, YARD - FLOOR, zIn - zOut), lConcrete);
            K.Block(K.Env, "DrainRoof", new Vector3(X, YARD - 0.2f, (zPit + zOut) * 0.5f), new Vector3(4.2f, 0.4f, zPit - zOut), CONCRETE);
            K.Block(K.Env, "DrainHead", new Vector3(X, (FLOOR + YARD) * 0.5f, zIn - 0.2f), new Vector3(4f, YARD - FLOOR, 0.4f), lConcrete);
            K.Stairs(K.Env, new Vector3(X, FLOOR, zPit + 0.2f), new Vector3(X, YARD, zIn - 0.4f), 3.6f, lConcrete);
            for (int s = -1; s <= 1; s += 2) K.Block(K.Env, "PitRail", new Vector3(X + s * 2.1f, YARD + 0.55f, (zIn + zPit) * 0.5f), new Vector3(0.08f, 1.1f, zIn - zPit), new Color(0.9f, 0.75f, 0.2f));
            K.Block(K.Env, "PitRail", new Vector3(X, YARD + 0.55f, zPit), new Vector3(4.2f, 1.1f, 0.08f), new Color(0.9f, 0.75f, 0.2f));
            for (int i = 0; i < 4; i++) K.Lamp(new Vector3(X, YARD - 0.7f, zPit - 6f - i * 9f), new Color(0.8f, 0.9f, 0.75f), 8f, 1f, false);
            K.Mesh(K.Env, "Grate", CylMesh, new Vector3(X, YARD + 0.01f, -340f), new Vector3(1.2f, 0.02f, 1.2f), new Color(0.25f, 0.25f, 0.27f));
            K.Point(MatchPoint.Kind.Extraction, new Vector3(X, FLOOR + 0.1f, -361.5f), 180f, "Quay storm drain");
            K.Prop("Boat", new Vector3(X, SEA - 0.2f, -369f), 0f, new Color(0.35f, 0.4f, 0.3f), MapKit.Collide.Box);
            K.Npc(NpcRole.Guard, new Vector3(X, FLOOR + 0.1f, -335f), 0f, Z, ItemKind.Shotgun);
            K.Gadget(ItemKind.SmokeGrenade, new Vector3(X + 1.5f, FLOOR + 0.1f, -350f));
        }

        /// <summary>The harbour office by the quay, with the alarm flare; the quay edge blocks.</summary>
        private void HarbourOffice()
        {
            const int Z = 1;
            MapKit.KitBuilding o = K.Building(Spec("Harbour Office"));
            K.Alarm(G(-158, -338), 180);
            K.Npc(NpcRole.Guard, o.Local(-3f, 0.1f, -2f), 180f, Z, ItemKind.Pistol);
            K.Activity(ActivityType.Drink, G(-180, -338, 0.1f), 180, Z);
            K.Prop("Bench", G(-180, -339.5f), 180);
            K.Weapon(ItemKind.Pistol, o.Local(4f, MapKit.STOREY + 0.15f, 3f));
            K.Car(G(-150, -330), 0, new Color(0.25f, 0.3f, 0.45f));
            // Quay edge blocks (they cover the last ground cells), open at the storm drain mouth.
            K.Block(K.Env, "QuayEdge", new Vector3((-500f + -204f) * 0.5f, (SEA_FLOOR + YARD) * 0.5f + 0.01f, -362.1f), new Vector3(296f, YARD - SEA_FLOOR + 0.02f, 5f), CONCRETE * 0.9f);
            K.Block(K.Env, "QuayEdge", new Vector3((-196f + -124f) * 0.5f, (SEA_FLOOR + YARD) * 0.5f + 0.01f, -362.1f), new Vector3(72f, YARD - SEA_FLOOR + 0.02f, 5f), CONCRETE * 0.9f);
            for (float x = -490f; x < -130f; x += 12f) if (Mathf.Abs(x + 200f) > 6f) K.Prop("Bollard", new Vector3(x, YARD + 0.02f, -364.2f), 0);
            for (int i = 0; i < 7; i++) { Vector3 p = new Vector3(-480f + i * 55f, YARD, -352f); K.Lamp(p, WARM, 14f, 1.4f, true); }
        }
    }
}
