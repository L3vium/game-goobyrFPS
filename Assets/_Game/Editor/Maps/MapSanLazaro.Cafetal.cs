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
        private ArchSpec _beneficio, _hangarA, _hangarB, _radio, _booth;
        private ArchResult _beneficioR, _hangarAR, _hangarBR, _radioR, _boothR;
        private readonly List<(ArchSpec spec, ArchResult result)> _huts = new List<(ArchSpec, ArchResult)>();
        private Vector3 _waterTowerTop, _roaster;
        /// <summary>Hut positions: terrace index, x.</summary>
        private static readonly Vector2Int[] HUTS = { new Vector2Int(1, 8), new Vector2Int(1, 70), new Vector2Int(2, -22), new Vector2Int(2, 104), new Vector2Int(3, -12), new Vector2Int(3, 60), new Vector2Int(3, 128), new Vector2Int(4, -32), new Vector2Int(4, 92) };
        private static readonly float[] STAIR_X = { 15f, 95f };

        private float TerraceY(int i) => TERRACES[i].z;

        private void PlanCafetal()
        {
            // Mule track up from the market, the quarry ramp.
            Road(3.2f, Ground.Dirt, 20f, new Vector2(15, -128), new Vector2(17, -96), new Vector2(15, -60));
            Road(4f, Ground.Dirt, 20f, new Vector2(112, -48), new Vector2(124, -60), new Vector2(128, -70));
            // The beneficio on terrace 5, its drying patio.
            ArchStyle lMill = ArchStyles.Colonial(new Color(0.95f, 0.9f, 0.82f), "mill", new Color(0.5f, 0.18f, 0.12f));
            lMill.roof = ArchLib.Surface("corrugated_iron_02", 2.5f);
            _beneficio = Facing("Beneficio", new Vector2(152f, 47f), TerraceY(4), 2, 22f, 15f, lMill, ZONE_CAFETAL);
            ArchSpec b = _beneficio;
            b.Tall("RoasterFloor", 0, 2, 0, 0, 14, 15, RoomType.Workshop, SecurityTier.Restricted, AccessLevel.Staff);
            b.Room("CuppingRoom", 0, 14, 0, 22, 7, RoomType.Office, SecurityTier.Restricted, AccessLevel.Staff);
            b.Room("SackStore", 0, 14, 7, 22, 15, RoomType.Store, SecurityTier.Restricted, AccessLevel.Staff).noFurniture = true;
            b.Room("MillOffice", 1, 14, 0, 22, 7, RoomType.Office, SecurityTier.Restricted, AccessLevel.Staff);
            b.Room("MillLanding", 1, 14, 7, 22, 15, RoomType.Corridor, SecurityTier.Restricted, AccessLevel.Staff);
            b.Room("SackingLoft", 2, 0, 0, 22, 15, RoomType.Attic, SecurityTier.Restricted, AccessLevel.Staff);
            b.Door(0, 7, 0, DoorKind.Double, 3.0f).poi = "Beneficio";
            b.Door(0, 18, 0, DoorKind.Door, 1.2f);
            b.Door(0, 14, 4); b.Door(0, 18, 7); b.Door(0, 14, 11);
            b.Door(0, 22, 11, DoorKind.Double, 2.4f).poi = "Loading ramp";
            b.Door(0, 4, 15, DoorKind.Door, 1.2f);
            b.Door(1, 18, 7);
            b.Stair(0, 14.6f, 8.4f, 21.4f, 13.4f, 3, true, true);
            b.Stair(1, 14.6f, 8.4f, 21.4f, 13.4f, 3, true, true);
            b.Window(2, 11f, 15f, WindowKind.Open, 1.4f).poi = "Coffee chute";
            b.Roof(2, 0, 0, 22, 15, RoofKind.Gable, true, 22f);
            b.cornices.Add(1);
            b.drains.Add(new ArchDrain { at = new Vector2(0, 2f), topFloor = 2, poi = "Beneficio drainpipe" });
            Lot(b, 3f, Ground.Cobble);
            Plaza(Rect.MinMaxRect(112f, 47f, 138f, 63f), Ground.Cobble);

            // Picker huts, the capataz's hut on terrace 5.
            foreach (Vector2Int h in HUTS)
            {
                float z = TERRACES[h.x].x + 3.5f;
                ArchSpec lHut = Facing("PickerHut_T" + (h.x + 1) + "_" + h.y, new Vector2(h.y, z), TerraceY(h.x), 2, 5f, 4f, ArchStyles.Timber(), ZONE_CAFETAL);
                lHut.Room("Hut", 0, 0, 0, 5, 4, RoomType.Bedroom, SecurityTier.Restricted, AccessLevel.Staff);
                lHut.Door(0, 2.5f, 0, DoorKind.Doorway, 1.1f);
                lHut.Roof(0, -0.01f, -0.01f, 5.01f, 4.01f, RoofKind.Shed, true, 14f).lowSide = 0;
                lHut.plinth = 0.6f;
                lHut.furnish = false;
                Lot(lHut, 1.5f, Ground.Dirt, 2f);
                _huts.Add((lHut, null));
            }

            // The airstrip: hangars, radio shack, the ridge checkpoint booth.
            ArchStyle lTin = ArchStyles.Industrial("concrete_wall_008", "corrugated_iron_02");
            _hangarA = Facing("HangarA", new Vector2(-162f, 140f), AIRSTRIP_Y, 0, 24f, 19f, lTin, ZONE_CAFETAL);
            ArchSpec ha = _hangarA;
            ha.Tall("HangarFloorA", 0, 2, 0, 0, 24, 13, RoomType.Hangar, SecurityTier.Hostile, AccessLevel.Security);
            ha.Room("WorkshopA", 0, 0, 13, 12, 19, RoomType.Workshop, SecurityTier.Hostile, AccessLevel.Security);
            ha.Room("OfficeA", 0, 12, 13, 24, 19, RoomType.Office, SecurityTier.Hostile, AccessLevel.Security);
            ha.Room("CatwalkA", 1, 0, 13, 24, 19, RoomType.Store, SecurityTier.Hostile, AccessLevel.Security);
            ha.Door(0, 12, 0, DoorKind.Doorway, 9f).height = 5.6f;
            ha.doors[ha.doors.Count - 1].poi = "Hangar A";
            ha.Door(0, 6, 13); ha.Door(0, 18, 13); ha.Door(0, 24, 16, DoorKind.Door, 1.2f);
            ha.Door(1, 12, 13, DoorKind.Doorway, 3f);
            ha.Roof(1, 0, 0, 24, 19, RoofKind.Gable, false, 16f);
            Lot(ha, 4f, Ground.Dirt);
            _hangarB = Facing("HangarB", new Vector2(-122f, 140f), AIRSTRIP_Y, 0, 20f, 17f, lTin, ZONE_CAFETAL);
            ArchSpec hb = _hangarB;
            hb.Tall("HangarFloorB", 0, 2, 0, 0, 20, 12, RoomType.Hangar, SecurityTier.Hostile, AccessLevel.Security);
            hb.Room("Bunks", 0, 0, 12, 10, 17, RoomType.Barracks, SecurityTier.Hostile, AccessLevel.Security);
            hb.Room("PilotsMess", 0, 10, 12, 20, 17, RoomType.Mess, SecurityTier.Hostile, AccessLevel.Security);
            hb.Door(0, 10, 0, DoorKind.Doorway, 8f).height = 5.4f;
            hb.Door(0, 5, 12); hb.Door(0, 15, 12); hb.Door(0, 10, 14.5f);
            hb.Secure(0, 20, 6, SecureDoor.Lock.Padlock, false, "Hangar B side door");
            hb.Roof(1, 0, 0, 20, 17, RoofKind.Gable, false, 16f);
            hb.drains.Add(new ArchDrain { at = new Vector2(0, 3f), topFloor = 1, poi = "Hangar B drainpipe" });
            Lot(hb, 4f, Ground.Dirt);
            _radio = Facing("RadioShack", new Vector2(-84f, 136f), AIRSTRIP_Y, 0, 7f, 6f, ArchStyles.Timber(), ZONE_CAFETAL);
            _radio.Room("Radio", 0, 0, 0, 7, 6, RoomType.Radio, SecurityTier.Hostile, AccessLevel.Security);
            _radio.Door(0, 3.5f, 0, DoorKind.Door, 1.1f);
            _radio.Window(0, 7f, 3f, WindowKind.Open).poi = "Radio shack window";
            _radio.Roof(0, -0.01f, -0.01f, 7.01f, 6.01f, RoofKind.Shed, true, 12f);
            Lot(_radio, 2f, Ground.Dirt);
            _booth = Facing("CheckpointBooth", new Vector2(-14f, 110f), Shape.Height(-14f, 112f), 3, 4f, 3.5f, ArchStyles.Industrial(), ZONE_CAFETAL);
            _booth.Room("Booth", 0, 0, 0, 4, 3.5f, RoomType.Security, SecurityTier.Restricted, AccessLevel.Security);
            _booth.Door(0, 2f, 0, DoorKind.Doorway, 1f);
            _booth.furnish = false;
            Lot(_booth, 1f, Ground.Asphalt, 2f);
        }

        private void DressCafetal()
        {
            _beneficioR = Build(_beneficio, ZONE_CAFETAL);
            for (int i = 0; i < _huts.Count; i++) _huts[i] = (_huts[i].spec, Build(_huts[i].spec, ZONE_CAFETAL));
            foreach ((ArchSpec lSpec, ArchResult r) in _huts)
            {
                A.Model("Furn_Bed", r.At(1.2f, 0, 2.8f, 0f), 90f, r.interior);
                A.Model("Prop_Crate_Wood", r.At(4f, 0, 3.2f, 0f), 0f, r.interior);
                A.Throwable(r.At(3.5f, 0, 1.5f));
            }
            // The roaster (the signature machine: a rigged gas valve), cooling tray, cupping cups.
            _roaster = _beneficioR.At(6f, 0, 8f, 0f);
            ArchMesh lMachine = new ArchMesh();
            lMachine.Tube(ArchLib.Surface("rusty_metal_02", 1.5f), _roaster + new Vector3(-1.6f, 1.6f, 0), _roaster + new Vector3(1.6f, 1.6f, 0), 1.0f, 18);
            lMachine.Box(ArchLib.Iron, _roaster + new Vector3(0, 0.3f, 0), new Vector3(3.6f, 0.6f, 1.6f));
            lMachine.Cylinder(ArchLib.Galvanised, _roaster + new Vector3(0, 0f, 2.4f), 1.2f, 0.7f, 18);
            lMachine.Tube(ArchLib.Iron, _roaster + new Vector3(1.2f, 2.4f, 0), _roaster + new Vector3(1.2f, 6.8f, 0), 0.25f, 10);
            // Hopper, burner glow, gauges, the gas line to the bottles, the stirring arm over the cooling tray.
            lMachine.Box(ArchLib.Surface("rusty_metal_02", 1.5f), _roaster + new Vector3(-0.6f, 3.0f, 0), new Vector3(1.0f, 0.8f, 1.0f));
            lMachine.Box(ArchLib.LampGlow, _roaster + new Vector3(0, 0.62f, -0.81f), new Vector3(2.2f, 0.18f, 0.02f), false);
            lMachine.Cylinder(ArchLib.Brass, _roaster + new Vector3(1.75f, 1.6f, -0.5f), 0.12f, 0.06f, 10, false);
            lMachine.Tube(ArchLib.Brass, _roaster + new Vector3(-1.8f, 0.4f, -0.4f), _roaster + new Vector3(-2.6f, 0.4f, -1.1f), 0.03f, 6, false);
            lMachine.Tube(ArchLib.Iron, _roaster + new Vector3(0, 0.75f, 2.4f), _roaster + new Vector3(0, 1.1f, 2.4f), 0.05f, 6, false);
            lMachine.Box(ArchLib.Iron, _roaster + new Vector3(0, 1.1f, 2.4f), new Vector3(2.0f, 0.06f, 0.08f), false);
            GameObject lRoaster = new GameObject("GasRoaster") { layer = Core.GameLayers.INTERACTABLE };
            lRoaster.transform.SetParent(A.Systems, false);
            lRoaster.transform.position = _roaster + Vector3.up;
            foreach (GameObject lPart in lMachine.Flush(lRoaster.transform, "SanLazaro_Roaster", Core.GameLayers.INTERACTABLE)) { lPart.isStatic = false; lPart.transform.position -= lRoaster.transform.position; }
            lRoaster.AddComponent<ExplosiveProp>();
            Light lBurner = new GameObject("RoasterBurner").AddComponent<Light>();
            lBurner.transform.SetParent(lRoaster.transform, false);
            lBurner.transform.position = _roaster + new Vector3(0, 0.6f, -1.2f);
            lBurner.type = LightType.Point;
            lBurner.range = 6f;
            lBurner.intensity = 2f;
            lBurner.color = new Color(1f, 0.45f, 0.15f);
            lBurner.shadows = LightShadows.None;
            ArchOutdoor.Sign(A, _roaster + new Vector3(-2.6f, 0f, -2.4f), 180f, "Gas - Peligro", true, new Color(0.85f, 0.6f, 0.05f));
            for (int i = 0; i < 3; i++) A.Sacks(_roaster + new Vector3(3.2f, 0f, -1.5f + i * 1.6f), 90f + i * 20f);
            A.Explosive(_roaster + new Vector3(-2.6f, 0, -1.4f), 0f, true);
            A.Explosive(_roaster + new Vector3(-2.6f, 0, -0.8f), 0f, true);
            K.Activity(ActivityType.Drink, _roaster + new Vector3(0, 0.05f, 3.8f), 180f, ZONE_CAFETAL, 20f);
            A.Drinks(_beneficioR.At(18f, 0, 3.5f, 0.8f), _beneficio.yaw, ZONE_CAFETAL, 3, false);
            A.Model("Furn_DiningTable", _beneficioR.At(18f, 0, 3.5f, 0f), _beneficio.yaw);
            A.Gadget(ItemKind.Poison, _beneficioR.At(20.5f, 0, 1.2f, 0.05f), "Copper fungicide");
            for (int i = 0; i < 6; i++) A.Sacks(_beneficioR.At(1.5f + i * 3.2f, 2, 12.5f, 0f), _beneficio.yaw + i * 13f);
            A.Distraction("radio", _beneficioR.At(13f, 0, 1f, 0f), _beneficio.yaw);
            // Drying patio: raked coffee on the concrete.
            for (int i = 0; i < 4; i++)
            {
                ArchMesh lBeans = new ArchMesh();
                Vector3 c = G(116f + i * 6f, 55f, 0f);
                lBeans.Box(ArchLib.PlainMat("CoffeeBeans", new Color(0.33f, 0.2f, 0.12f), 0.25f, 0f), c + Vector3.up * 0.04f, new Vector3(5f, 0.06f, 12f), false);
                GameObject lBed = new GameObject("DryingBed");
                lBed.transform.SetParent(K.Props, false);
                lBeans.Flush(lBed.transform, "SanLazaro_Beans_" + i);
            }
            // The coffee chute: from the loft's north window down into the stable yard.
            A.Zipline(_beneficioR.At(11f, 2, 15.6f, 0.05f), new Vector3(146f, PAD_Y + 0.05f, 108f));

            // Coffee rows, shade trees, sheds, stairs between the terraces.
            for (int i = 0; i < TERRACES.Length; i++)
            {
                float y = TerraceY(i);
                for (float z = TERRACES[i].x + 5f; z < TERRACES[i].y - 1f; z += 2.6f)
                    for (float x = TERRACE_X0 + 2f; x < TERRACE_X1[i] - 2f; x += 2.2f)
                    {
                        if (!IsClear(x, z) || Mathf.Abs(x - STAIR_X[0]) < 3f || Mathf.Abs(x - STAIR_X[1]) < 3f) continue;
                        if (Mathf.Abs(x - 20f) < 6f && i == 4) continue;
                        K.LandTree("Veg_JungleBush_A", x + Random01(x, z) * 0.4f, z, 1.15f + Random01(z, x) * 0.35f);
                    }
                for (float x = TERRACE_X0 + 8f; x < TERRACE_X1[i] - 6f; x += 19f)
                    if (IsClear(x, TERRACES[i].y - 3f)) K.LandTree("Veg_JungleTree_B", x, TERRACES[i].y - 3f, 0.8f);
                // A dry-stone wall along the terrace's downhill edge, open at the stairs.
                float lEdge = TERRACES[i].x + 0.4f;
                ArchOutdoor.Retaining(A, new Vector2(TERRACE_X0, lEdge), new Vector2(STAIR_X[0] - 1.6f, lEdge), y + 0.9f, 0.6f);
                ArchOutdoor.Retaining(A, new Vector2(STAIR_X[0] + 1.6f, lEdge), new Vector2(STAIR_X[1] - 1.6f, lEdge), y + 0.9f, 0.6f);
                ArchOutdoor.Retaining(A, new Vector2(STAIR_X[1] + 1.6f, lEdge), new Vector2(TERRACE_X1[i], lEdge), y + 0.9f, 0.6f);
                if (i + 1 < TERRACES.Length)
                    foreach (float sx in STAIR_X)
                        ArchOutdoor.Stairs(A, new Vector3(sx, y, TERRACES[i].y - 0.5f), new Vector3(sx, TerraceY(i + 1), TERRACES[i + 1].x + 1.5f), 2.2f);
            }
            ArchOutdoor.Stairs(A, new Vector3(STAIR_X[1], PUEBLO_Y + 3f, -78f), new Vector3(STAIR_X[1], TerraceY(0), TERRACES[0].x + 1.5f), 2.2f);
            ArchOutdoor.Shed(A, G(50f, 2f, 0f), 0f, 12f, 8f, 3.2f, ArchLib.Surface("corrugated_iron_02", 2.5f));
            ArchOutdoor.Shed(A, G(92f, 28f, 0f), 0f, 12f, 8f, 3.2f, ArchLib.Surface("corrugated_iron_02", 2.5f));
            ArchOutdoor.Shed(A, G(-20f, -50f, 0f), 0f, 10f, 6f, 3f, ArchLib.Surface("corrugated_iron_02", 2.5f));
            for (int i = 0; i < 3; i++) A.Sacks(G(46f + i * 3f, 3.5f, 0.12f), i * 30f);
            A.CrateStack(G(95f, 26f, 0.12f), 0f, 4);
            // The water tower on terrace 5, looking at the casa's south loggia.
            _waterTowerTop = ArchOutdoor.WaterTower(A, G(20f, 56f, 0f), 11f, 0f);
            Poi(_waterTowerTop, "Water tower", MapPoi.Kind.Overlook);

            // The quarry: rock piles, crates, the crowbar.
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Vector3 p = G(QUARRY.x + Mathf.Cos(a) * 11f, QUARRY.y + Mathf.Sin(a) * 11f, 0f);
                A.Model(i % 2 == 0 ? "Veg_Rock_B" : "Veg_Rock_A", p - Vector3.up * 0.4f, i * 37f, null, ArchLib.Collide.Box, 2.5f + (i % 3));
            }
            A.CrateStack(G(QUARRY.x - 4f, QUARRY.y + 3f, 0f), 20f, 3);
            A.BigCrate(G(QUARRY.x + 3f, QUARRY.y - 6f, 0f), 80f);
            A.Gadget(ItemKind.Crowbar, G(QUARRY.x - 2f, QUARRY.y + 1f, 0.1f), "Quarry crowbar");
            A.Distraction("generator", G(QUARRY.x + 6f, QUARRY.y + 2f, 0f), 30f);
            Poi(G(QUARRY.x, QUARRY.y, 1f), "Quarry", MapPoi.Kind.Landmark);

            // The jungle chapel ruin: broken stone walls, an altar, datura.
            ArchMat lRuin = ArchLib.Surface("castle_brick_07", 2f);
            ArchMesh lWalls = new ArchMesh();
            Vector3 lBase = G(CHAPEL_RUIN, 0f);
            float[] lHeights = { 3.2f, 1.4f, 2.6f, 0.8f, 2.9f, 1.8f };
            for (int i = 0; i < 6; i++)
            {
                bool lSide = i < 3;
                Vector3 c = lBase + (lSide ? new Vector3(-4f, 0, -6f + i * 5f) : new Vector3(4f, 0, -6f + (i - 3) * 5f));
                lWalls.Box(lRuin, c + Vector3.up * (lHeights[i] * 0.5f - 0.4f), new Vector3(0.8f, lHeights[i] + 0.8f, 4.2f));
            }
            lWalls.Box(lRuin, lBase + new Vector3(0, 1.6f, 8f), new Vector3(8.8f, 4f, 0.8f));
            lWalls.Box(ArchLib.Surface("stone_wall", 2f), lBase + new Vector3(0, 0.5f, 6.5f), new Vector3(2f, 1f, 1f));
            GameObject lRuins = new GameObject("ChapelRuin");
            lRuins.transform.SetParent(K.Env, false);
            lWalls.Flush(lRuins.transform, "SanLazaro_ChapelRuin");
            A.Gadget(ItemKind.Poison, lBase + new Vector3(0, 1.05f, 6.5f), "Datura");
            Poi(lBase + Vector3.up, "Jungle chapel", MapPoi.Kind.Landmark);
        }

        private static float Random01(float a, float b) => Mathf.Repeat(Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.547f, 1f);

        private void DressAirstrip()
        {
            _hangarAR = Build(_hangarA, ZONE_CAFETAL);
            _hangarBR = Build(_hangarB, ZONE_CAFETAL);
            _radioR = Build(_radio, ZONE_CAFETAL);
            _boothR = Build(_booth, ZONE_CAFETAL);
            // Hangar A: a plane under repair, the catwalk ladder.
            A.Model("Prop_Helicopter", _hangarAR.At(12f, 0, 6.5f, 0f), _hangarA.yaw + 10f, _hangarAR.interior);
            A.CrateStack(_hangarAR.At(3f, 0, 4f, 0f), _hangarA.yaw, 6, true);
            A.Distraction("generator", _hangarAR.At(21f, 0, 3f, 0f), _hangarA.yaw);
            A.Climb(_hangarAR.At(23.2f, 0, 12.2f, 0f), 3.6f, _hangarAR.Rotation * Vector3.back, true, "Hangar A catwalk ladder");
            A.Model("Prop_Car_Pickup", _hangarBR.At(14f, 0, 6f, 0f), _hangarB.yaw + 80f, _hangarBR.interior);
            A.CrateStack(_hangarBR.At(3f, 0, 3f, 0f), _hangarB.yaw, 4, true);
            A.Wardrobe(_hangarBR.At(9f, 0, 16.4f, 0f), _hangarB.yaw + 180f, _hangarBR.interior);
            ArchOutdoor.Mast(A, G(-76f, 142f, 0f), 26f);
            Poi(G(-76f, 142f, 2f), "Radio mast", MapPoi.Kind.Landmark);
            A.Distraction("radio", _radioR.At(2f, 0, 4.8f, 0.8f), _radio.yaw);
            A.Model("Furn_Desk", _radioR.At(3.5f, 0, 5f, 0f), _radio.yaw + 180f, _radioR.interior);
            // The DC-3 wreck, the fuel bowser beside it, the depot.
            ArchOutdoor.Dc3(A, G(-96f, 170f, -0.4f), 115f);
            Poi(G(-96f, 170f, 2f), "DC-3 wreck", MapPoi.Kind.Landmark);
            ArchOutdoor.FuelTank(A, G(-104f, 162.5f, 0f), 90f, 4.5f, 0.9f);
            A.Model("Prop_Car_Pickup", G(-104f, 165.5f, 0f), 90f);
            ArchOutdoor.FuelTank(A, G(-64f, 126f, 0f), 0f, 7f, 1.3f);
            ArchOutdoor.FuelTank(A, G(-58f, 126f, 0f), 0f, 7f, 1.3f);
            for (int i = 0; i < 6; i++) A.Explosive(G(-70f + (i % 3) * 0.7f, 120f + (i / 3) * 0.7f, 0f), i * 40f);
            ArchOutdoor.Fence(A, new Vector2(-75f, 117f), new Vector2(-50f, 117f));
            // Runway edge: drums, a ditch of elephant grass, landing lights.
            for (float x = -200f; x < -45f; x += 15f)
            {
                A.Lamp(G(x, 140.5f, 0f), 0f, new Color(1f, 0.85f, 0.4f), 6f, 1.2f, false);
                A.Lamp(G(x, 163.5f, 0f), 0f, new Color(1f, 0.85f, 0.4f), 6f, 1.2f, false);
            }
            A.Model("Prop_Helicopter", G(-150f, 152f, 0f), 80f);
            Poi(G(-120f, 152f, 1f), "Airstrip", MapPoi.Kind.Landmark);
            // Ridge checkpoint: barrier, sandbags, a jeep.
            Vector3 lCheck = G(-20f, 113f, 0f);
            for (int i = 0; i < 3; i++) A.Model("Prop_Sandbags", lCheck + new Vector3(-4f + i * 1.8f, 0f, -3f), 0f);
            A.Model("Prop_Barrier_Concrete", lCheck + new Vector3(2f, 0f, 2f), 0f);
            A.Distraction("caralarm", lCheck + new Vector3(5f, 0f, -1f), 30f);
            Poi(lCheck + Vector3.up, "Ridge checkpoint", MapPoi.Kind.Entrance);
        }
    }
}
