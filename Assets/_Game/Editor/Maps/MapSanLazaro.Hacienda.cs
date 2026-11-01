using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapSanLazaro
    {
        private ArchSpec _casa, _garage, _shed;
        private ArchResult _casaR, _garageR, _shedR;

        private void PlanHacienda()
        {
            _casa = CasaSpec(CASA, 0f);
            Lot(_casa, 2f, Ground.Cobble);
            _garage = Facing("Garage", new Vector2(140f, 124f), PAD_Y, 3, 12f, 11f, ArchStyles.Colonial(new Color(0.96f, 0.95f, 0.91f), "casa"), ZONE_HACIENDA);
            _garage.Room("GarageBay", 0, 0, 0, 12, 11, RoomType.Garage, SecurityTier.Restricted, AccessLevel.Staff);
            _garage.Door(0, 6f, 0, DoorKind.Doorway, 4.5f).height = 3.2f;
            _garage.Door(0, 12f, 8f, DoorKind.Door, 1.1f);
            _garage.Roof(0, -0.01f, -0.01f, 12.01f, 11.01f, RoofKind.Hip, true, 24f);
            _shed = Facing("ToolShed", new Vector2(150f, 84f), PAD_Y, 3, 5f, 4f, ArchStyles.Timber("clay_roof_tiles_02"), ZONE_HACIENDA);
            _shed.Room("Shed", 0, 0, 0, 5, 4, RoomType.Store, SecurityTier.Restricted);
            _shed.Door(0, 2.5f, 0, DoorKind.Doorway, 1.1f);
            _shed.Roof(0, -0.01f, -0.01f, 5.01f, 4.01f, RoofKind.Shed, true, 14f);
            _shed.furnish = false;
            // The north footpath from the ridge (the W5 scramble), the produce truck's lane.
            Road(2.5f, Ground.Dirt, 20f, new Vector2(-20, 113), new Vector2(20, 136), new Vector2(60, 150), new Vector2(110, 152));
        }

        private Vector3 C(float x, int f, float z, float up = 0.05f) => _casaR.At(x, f, z, up);

        private void DressHacienda()
        {
            _casaR = Build(_casa, ZONE_HACIENDA);
            _garageR = Build(_garage, ZONE_HACIENDA);
            _shedR = Build(_shed, ZONE_HACIENDA);
            Poi(C(35f, 6, 28f, 2f), "Mirador", MapPoi.Kind.Landmark);
            Poi(C(20f, 0, 15f, 1f), "Casa La Cumbre", MapPoi.Kind.Landmark);

            // The compound wall and its gates (W1 frisk gate, W2 service gate, W6 cemetery gate).
            Vector2[] lWall = { new Vector2(COMPOUND.xMin, COMPOUND.yMin), new Vector2(COMPOUND.xMax, COMPOUND.yMin), new Vector2(COMPOUND.xMax, COMPOUND.yMax), new Vector2(COMPOUND.xMin, COMPOUND.yMax) };
            List<ArchOutdoor.Gate> lGates = new List<ArchOutdoor.Gate>
            {
                new ArchOutdoor.Gate { segment = 0, at = (75f - COMPOUND.xMin) / COMPOUND.width, width = 4.2f, poi = "Main gate" },
                new ArchOutdoor.Gate { segment = 2, at = (COMPOUND.xMax - 108f) / COMPOUND.width, width = 3.6f, locked = SecureDoor.Lock.Padlock, poi = "Service gate" },
                new ArchOutdoor.Gate { segment = 3, at = (COMPOUND.yMax - 105f) / COMPOUND.height, width = 1.6f, locked = SecureDoor.Lock.Crowbar, poi = "Cemetery gate" }
            };
            ArchStyle lCasa = _casa.style;
            ArchOutdoor.Wall(A, lWall, true, 3.2f, 0.5f, lCasa.facade, lCasa.trim, lGates);
            ArchOutdoor.Retaining(A, new Vector2(80f, 68.6f), new Vector2(COMPOUND.xMax, 68.6f), PAD_Y + 0.3f, 1.2f);
            // Garden walls round the forecourt and the patio's planters.
            ArchOutdoor.Fountain(A, C(20f, 0, 15f, 0f), 2.2f);
            for (int i = 0; i < 4; i++) A.Model("Veg_JungleBush_B", C(i < 2 ? 15f : 25f, 0, i % 2 == 0 ? 11f : 19f, 0f), i * 40f, null, ArchLib.Collide.None, 1.1f);
            for (int i = 0; i < 6; i++) A.Model("Furn_PottedPlant", C(13.5f + i * 2.6f, 0, 2.5f, 0f), 0f);

            // Forecourt (SW): parked cars, lamps, the frisk table.
            A.Model("Prop_Car_Sedan", G(70f, 86f, 0f), 10f);
            A.Model("Prop_Car_Sedan", G(76f, 88f, 0f), 15f);
            A.Distraction("caralarm", G(82f, 84f, 0f), 200f);
            foreach (Vector2 p in new[] { new Vector2(70, 75), new Vector2(84, 75), new Vector2(70, 93), new Vector2(100, 92), new Vector2(120, 92), new Vector2(140, 92) })
                A.Lamp(G(p, 0f), 0f, new Color(1f, 0.82f, 0.6f), 14f, 2.2f);
            A.Model("Furn_DiningTable", G(75f, 76.5f, 0f), 0f);

            // Orchard (SE): orange trees, the gardener's shed, the espalier under the buyer's balcony.
            for (float x = 132f; x < 156f; x += 5f)
                for (float z = 74f; z < 94f; z += 5f)
                    if (!(x > 144f && z < 88f)) K.LandTree("Veg_JungleBush_B", x + Random01(x, z), z + Random01(z, x), 1.3f);
            A.BigCrate(_shedR.At(1.2f, 0, 2.6f, 0f), _shed.yaw);
            A.Gadget(ItemKind.Wrench, _shedR.At(3.5f, 0, 2f, 0.05f), "Shed wrench");
            A.Climb(new Vector3(145f, 41f, 67.6f), 8.4f, Vector3.back, false, "Vine trellis");

            // Chapel garden (W): the family cemetery.
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = G(68f + (i % 4) * 4f, 100f + (i / 4) * 6f, 0f);
                ArchMat lMarble = ArchLib.Surface("marble_01", 1.2f);
                A.BoxObject("Tomb", p, 0f, K.Props, 0, true,
                    (ArchLib.Surface("stone_wall", 1.5f), new Vector3(0, 0.12f, 0), new Vector3(1.1f, 0.24f, 2.2f)),
                    (lMarble, new Vector3(0, 0.38f, 0.1f), new Vector3(0.85f, 0.28f, 1.8f)),
                    (lMarble, new Vector3(0, 0.95f, -0.92f), new Vector3(0.75f, 1.2f, 0.14f)),
                    (lMarble, new Vector3(0, 1.75f, -0.92f), new Vector3(0.1f, 0.5f, 0.1f)),
                    (lMarble, new Vector3(0, 1.82f, -0.92f), new Vector3(0.36f, 0.09f, 0.1f)));
            }
            // Kitchen garden (NW): beds and the ivy on the north wall (W5).
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = G(68f + i * 5f, 132f, 0f);
                A.BoxObject("Bed", p, 0f, K.Props, 0, true, (ArchLib.Surface("brown_mud_leaves_01", 2f), new Vector3(0, 0.15f, 0), new Vector3(3.6f, 0.3f, 7f)), (ArchLib.Surface("rough_wood", 1f), new Vector3(0, 0.2f, -3.55f), new Vector3(3.8f, 0.4f, 0.1f)));
                for (int k = 0; k < 4; k++) A.Model("Veg_Fern", p + new Vector3(0f, 0.3f, -2.6f + k * 1.7f), k * 50f, null, ArchLib.Collide.None, 1.3f);
            }
            A.Climb(new Vector3(74f, PAD_Y + 0.6f, COMPOUND.yMax + 0.3f), 3f, Vector3.forward, false, "North wall ivy");
            A.Climb(new Vector3(74f, PAD_Y, COMPOUND.yMax - 0.3f), 3.2f, Vector3.back, false, null);
            // Service yard (N): the produce truck at the service gate, bins, the laundry lines, the dumpster
            // (the operator's smoke spot), the staff signs.
            ArchOutdoor.Truck(A, G(100f, 148f, 0f), 90f, new Color(0.15f, 0.45f, 0.25f), "ProduceTruck");
            A.Dumpster(G(117f, 136f, 0f), 180f);
            ArchOutdoor.Bins(A, G(119.5f, 139.6f, 0f), 0f, 3);
            A.CrateStack(G(98f, 135f, 0f), 0f, 4);
            A.LaundryCart(G(132f, 134f, 0f), 0f);
            ArchMesh lLines = new ArchMesh();
            ArchMat lLinen = ArchLib.PlainMat("Linen", new Color(0.93f, 0.92f, 0.88f), 0.2f, 0f);
            for (int k = 0; k < 2; k++)
            {
                Vector3 a = G(136f, 132f + k * 3f, 0f), b = G(152f, 132f + k * 3f, 0f);
                lLines.Cylinder(ArchLib.Surface("rough_wood", 1f), a, 0.06f, 2.3f, 6);
                lLines.Cylinder(ArchLib.Surface("rough_wood", 1f), b, 0.06f, 2.3f, 6);
                lLines.Tube(ArchLib.Iron, a + Vector3.up * 2.2f, b + Vector3.up * 2.2f, 0.01f, 4, false);
                for (float t = 0.08f; t < 0.92f; t += 0.14f)
                {
                    Vector3 c = Vector3.Lerp(a, b, t) + Vector3.up * 1.55f;
                    lLines.DoubleQuad(lLinen, c + new Vector3(-0.45f, -0.6f, 0), c + new Vector3(0.45f, -0.6f, 0), c + new Vector3(0.45f, 0.62f, 0), c + new Vector3(-0.45f, 0.62f, 0), false);
                }
            }
            GameObject lLinesRoot = new GameObject("LaundryLines");
            lLinesRoot.transform.SetParent(K.Props, false);
            lLines.Flush(lLinesRoot.transform, "SanLazaro_LaundryLines");
            ArchOutdoor.Sign(A, G(108f, 139f, 0f), 180f, "Solo personal");
            ArchOutdoor.Sign(A, C(37f, 0, 32.6f, 1.6f), 180f, "Cocina - Solo personal", false);
            // The frisk lane at the main gate: a guard booth, rope lines, the sign.
            ArchOutdoor.GuardBooth(A, G(80.5f, 72.5f, 0f), 180f, false);
            ArchOutdoor.RopeLine(A, G(73f, 71f, 0f), G(73f, 78f, 0f));
            ArchOutdoor.RopeLine(A, G(77f, 71f, 0f), G(77f, 78f, 0f));
            ArchOutdoor.Sign(A, G(78.5f, 79f, 0f), 180f, "Control de invitados", true, new Color(0.15f, 0.3f, 0.2f));
            ArchOutdoor.CoverClusters(A, Rect.MinMaxRect(96f, 72f, 128f, 92f), 11f, "default", 91);
            A.Camera(C(33f, 0, 32.3f, 3.2f), G(108f, 140f, 1f), ZONE_HACIENDA);
            // Stable yard (E): garage, a jeep, the stables' feed sacks.
            A.Model("Prop_Car_Pickup", _garageR.At(6f, 0, 6f, 0f), _garage.yaw);
            A.BigCrate(_garageR.At(10.5f, 0, 9.5f, 0f), _garage.yaw);
            A.Distraction("generator", _garageR.At(1.5f, 0, 9.6f, 0f), _garage.yaw);
            ArchOutdoor.Shed(A, G(150f, 106f, 0f), 90f, 12f, 6f, 3.2f, ArchLib.Surface("clay_roof_tiles_02", 2.4f));
            for (int i = 0; i < 3; i++) A.Sacks(G(147f + i * 2.4f, 102f, 0.12f), i * 30f);

            // ---- the casa's systems
            // Cameras C1-C5 and the recorder in the security room, the CO2 purge panel (air handler on the vault).
            A.Camera(C(9.4f, 0, 0.6f, 3.1f), C(5f, 0, 7f, 1f), ZONE_HACIENDA);
            A.Camera(C(12.7f, 1, 8.7f, 3.1f), C(20f, 1, 4f, 1f), ZONE_HACIENDA);
            A.Camera(C(29.4f, -1, 23.4f, 3.0f), C(15f, -1, 21f, 1f), ZONE_HACIENDA);
            A.Camera(C(29.6f, 2, 5.6f, 1.6f), C(15f, 2, 3f, 0f), ZONE_HACIENDA);
            A.Recorder(C(26.8f, -1, 11f, 0f), -90f, ZONE_HACIENDA);
            BuildingVolume lVault = ArchVolume("Vault", C(20f, -2, 13.5f, 0f), new Vector3(14f, 3.4f, 7f));
            Transform[] lVents = { _casaR.systems.Find("VentCover") };
            A.AirHandler(C(22f, -1, 16.6f, 0f), 180f, lVault, lVents);
            A.Breaker(C(39.5f, -1, 30f, 0f), -90f, _casaR.volume);
            // Chandeliers in the salon and the dining room.
            A.Chandelier(C(15f, 0, 28f, 3.25f), 3f);
            A.Chandelier(C(25f, 0, 28f, 3.25f), 3f);
            // Ramiro's aguardiente in the study, the chapel candles and lamp oil, the fireworks on the loggia.
            A.Drinks(C(23f, 1, 30.8f, 0.8f), 180f, ZONE_HACIENDA, 1, true, 1f);
            A.Model("Furn_Cabinet", C(23f, 1, 31f, 0f), 180f, _casaR.interior);
            for (int i = 0; i < 4; i++)
            {
                GameObject lCandle = A.BoxObject("Candles", C(3.5f + i * 1.2f, 0, 22.6f, 1.0f), 0f, A.Systems, Core.GameLayers.INTERACTABLE, true, (ArchLib.PlainMat("Wax", new Color(0.95f, 0.92f, 0.8f), 0.3f, 0f), new Vector3(0, 0.15f, 0), new Vector3(0.5f, 0.3f, 0.2f)));
                lCandle.AddComponent<Flammable>().Setup(20f, 3f, false, 1.4f);
            }
            A.Model("Prop_Jerrycan", C(8.6f, 0, 22.9f, 0f), 0f);
            A.Explosive(C(14f, 0, 1.2f, 0f), 0f);
            A.Explosive(C(14.7f, 0, 1.2f, 0f), 0f);
            A.Distraction("radio", C(12.8f, 0, 9f, 0f), 90f);
            A.Distraction("phone", C(24.5f, 1, 27.6f, 0.8f), 0f);
            A.Distraction("phone", C(35f, 0, 7f, 0.8f), 0f);
            A.Distraction("radio", C(4f, -1, 30.6f, 0f), 180f);
            A.Explosive(C(38.6f, -1, 30.6f, 0f), 0f, true);
            A.LaundryCart(C(33f, -1, 30.5f, 0f), 180f);
            A.LaundryCart(C(2f, 1, 30.5f, 0f), 180f);
            A.Gadget(ItemKind.EmeticPoison, C(36f, 0, 30.6f, 0.95f), "Emetic (kitchen)");
            A.Gadget(ItemKind.Keycard, C(37f, 0, 2f, 0.85f), "Accountant's spare keycard");
            // The mirador drop ladder (b7): from the belvedere down to the stair-hall roof.
            A.DropLadder(C(33f, 6, 23.7f, 0f), C(33f, 3, 23.7f, 0f), Vector3.back, "Mirador drop ladder");
            // Lamps on the facades, the hall's chandelier lights.
            foreach (Vector2 p in new[] { new Vector2(3, -0.6f), new Vector2(36, 32.6f), new Vector2(20, -0.6f) })
                A.Lamp(C(p.x, 0, p.y, 2.8f), 0f, new Color(1f, 0.8f, 0.55f), 10f, 1.8f, false, 0f);
        }

        private BuildingVolume ArchVolume(string pName, Vector3 pCenter, Vector3 pSize)
        {
            GameObject lVolume = new GameObject("BuildingVolume_" + pName);
            lVolume.transform.SetParent(K.Markers, false);
            lVolume.transform.position = pCenter;
            BuildingVolume lBuilding = lVolume.AddComponent<BuildingVolume>();
            lBuilding.Setup(pName, pSize);
            return lBuilding;
        }
    }
}
