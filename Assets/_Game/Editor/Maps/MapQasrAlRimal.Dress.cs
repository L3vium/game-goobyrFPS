using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapQasrAlRimal
    {
        private Vector3 _screwPress, _dish, _lookoutRoof;

        private void DressTown()
        {
            foreach ((ArchSpec lSpec, int lZone) in _town)
            {
                ArchResult r = Build(lSpec, lZone);
                if (lSpec == _teaHouse) _teaR = r;
                else if (lSpec == _mosque) _mosqueR = r;
                else if (lSpec == _serai) _seraiR = r;
                else if (lSpec == _busStation) _busR = r;
                else _houses.Add(r);
            }
            Poi(_mosqueR.At(19f, 6, 3f, 1f), "Minaret", MapPoi.Kind.Landmark);
            Poi(G(PLAZA, 1f), "Well plaza", MapPoi.Kind.Landmark);
            // The well, palms and lamps on the plaza.
            ArchOutdoor.Fountain(A, G(PLAZA, 0f), 2.2f);
            foreach (Vector2 p in new[] { new Vector2(8, -138), new Vector2(32, -138), new Vector2(8, -162), new Vector2(32, -162) })
            {
                K.LandTree("Veg_DatePalm", p.x, p.y, 0.9f);
                A.Lamp(G(p + new Vector2(2, 0), 0f), 0f, new Color(1f, 0.78f, 0.5f), 14f, 2.2f);
            }
            // Covered souk streets: awnings overhead, stalls, the two arches framing the keep.
            Color[] lCloths = { new Color(0.75f, 0.2f, 0.15f), new Color(0.9f, 0.7f, 0.25f), new Color(0.2f, 0.35f, 0.65f), new Color(0.85f, 0.85f, 0.8f) };
            foreach (float x in new[] { 2f, 40f })
            {
                for (float z = -132f; z < -106f; z += 3.2f)
                {
                    ArchMesh lAwning = new ArchMesh();
                    ArchMat lCloth = ArchLib.Tinted("rough_linen", lCloths[(int)(z + x) & 3], "souk" + ((int)(z + x) & 3), 1.5f);
                    float y = TOWN_Y + 4.4f;
                    lAwning.Quad(lCloth, new Vector3(x - 3.2f, y, z), new Vector3(x - 3.2f, y, z + 2.9f), new Vector3(x + 3.2f, y - 0.4f, z + 2.9f), new Vector3(x + 3.2f, y - 0.4f, z), false);
                    lAwning.Quad(lCloth, new Vector3(x + 3.2f, y - 0.4f, z), new Vector3(x + 3.2f, y - 0.4f, z + 2.9f), new Vector3(x - 3.2f, y, z + 2.9f), new Vector3(x - 3.2f, y, z), false);
                    lAwning.Tube(ArchLib.Surface("rough_wood", 1f), new Vector3(x - 3.6f, y, z), new Vector3(x + 3.6f, y - 0.4f, z), 0.05f, 6, false);
                    GameObject lRoot = new GameObject("SoukAwning");
                    lRoot.transform.SetParent(K.Props, false);
                    lAwning.Flush(lRoot.transform, "Qasr_Awning_" + (int)x + "_" + (int)(-z));
                }
                for (int i = 0; i < 3; i++) ArchOutdoor.Stall(A, G(x + (i % 2 == 0 ? -1.6f : 1.6f), -128f + i * 8f, 0f), i % 2 == 0 ? 90f : -90f, lCloths[i]);
                Gateway(G(x, -134.5f, 0f), 0f, 5.6f, 6.4f, x < 10f ? "Crossing of Scales" : "Dyers' Arch");
                Gateway(G(x, -105.5f, 0f), 0f, 5.6f, 6.4f, null);
            }
            // Tea house: the storyteller's brazier (rigged canister), drinks, the back room wardrobe.
            A.Explosive(_teaR.InRoom("TeaRoom", 0.5f, 0.5f, 0f), 0f, true);
            A.Model("Furn_DiningTable", _teaR.InRoom("TeaRoom", 0.3f, 0.6f, 0f), _teaHouse.yaw, _teaR.interior);
            A.Drinks(_teaR.InRoom("TeaRoom", 0.3f, 0.6f, 0.77f), _teaHouse.yaw, ZONE_TOWN, 3, true, 1.2f);
            A.Distraction("radio", _teaR.InRoom("TeaKitchen", 0.5f, 0.9f, 0f), _teaHouse.yaw);
            A.Wardrobe(_teaR.InRoom("TeaBackRoom", 0.9f, 0.8f, 0f), _teaHouse.yaw + 180f, _teaR.interior);
            // Caravanserai: fuel drums, crates, the stable, a phone.
            for (int i = 0; i < 4; i++) A.Explosive(_seraiR.At(9f + i * 0.7f, 0, 22f, 0f), i * 30f);
            A.CrateStack(_seraiR.At(20f, 0, 9f, 0f), 0f, 4);
            A.BigCrate(_seraiR.At(15f, 0, 27.6f, 0f), 180f);
            A.Distraction("phone", _seraiR.At(27.5f, 0, 15f, 0.8f), -90f);
            K.LandTree("Veg_DatePalm", _seraiR.At(15f, 0, 15f).x, _seraiR.At(15f, 0, 15f).z, 0.8f);
            Poi(_seraiR.At(15f, 0, 15f, 1f), "Caravanserai", MapPoi.Kind.Landmark);
            // Bus station: the bus (a pickup convoy here), the shelter, the clerk's car alarm.
            ArchOutdoor.Shed(A, G(122f, -178f, 0f), 0f, 10f, 3f, 2.8f, ArchLib.Surface("corrugated_iron_02", 2.5f), ArchLib.Iron, true);
            A.Distraction("caralarm", G(118f, -186f, 0f), 90f);
            A.Model("Prop_Car_Pickup", G(112f, -184f, 0f), 95f);
            // Roof planks between neighbouring roofs.
            for (int i = 0; i + 1 < _houses.Count; i++)
            {
                ArchSpec a = _houses[i].spec, b = _houses[i + 1].spec;
                if (Mathf.Abs(a.yaw - b.yaw) > 1f) continue;
                Vector2 ca = Center(a), cb = Center(b);
                Vector3 pa = new Vector3(ca.x, a.origin.y + a.MaxFloor * a.storey, ca.y), pb = new Vector3(cb.x, b.origin.y + b.MaxFloor * b.storey, cb.y);
                float d = Vector3.Distance(pa, pb);
                if (d < 9f || d > 13f || i % 2 == 1) continue;
                ArchMesh lPlank = new ArchMesh();
                lPlank.Box(ArchLib.Surface("old_planks_02", 1.5f), (pa + pb) * 0.5f + Vector3.up * 1.05f, new Vector3(0.9f, 0.08f, d), Quaternion.LookRotation(pb - pa));
                GameObject lBridge = new GameObject("RoofPlank");
                lBridge.transform.SetParent(K.Props, false);
                lPlank.Flush(lBridge.transform, "Qasr_RoofPlank_" + i);
            }
            for (int i = 0; i < 4; i++) A.Dumpster(G(-10f + i * 30f, -158.5f, 0f), 0f);
        }

        /// <summary>A pointed stone arch over a street (souk gates, the citadel gate).</summary>
        private void Gateway(Vector3 pGround, float pYaw, float pWidth, float pHeight, string pPoi)
        {
            ArchMat lStone = ArchLib.Tinted("large_sandstone_blocks", new Color(0.95f, 0.86f, 0.72f), "sand_trim", 2f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pGround, Quaternion.Euler(0, pYaw, 0), Vector3.one);
            foreach (float s in new[] { -1f, 1f }) lMesh.Box(lStone, new Vector3(s * (pWidth * 0.5f + 0.5f), pHeight * 0.5f - 0.5f, 0), new Vector3(1f, pHeight + 1f, 1.2f));
            lMesh.Box(lStone, new Vector3(0, pHeight + 0.6f, 0), new Vector3(pWidth + 2f, 1.2f, 1.2f));
            for (float x = -pWidth * 0.5f - 0.6f; x <= pWidth * 0.5f + 0.7f; x += 0.9f) lMesh.Box(lStone, new Vector3(x, pHeight + 1.45f, 0), new Vector3(0.5f, 0.5f, 1.2f));
            GameObject lRoot = new GameObject("Gateway");
            lRoot.transform.SetParent(K.Env, false);
            lMesh.Flush(lRoot.transform, "Qasr_Gateway_" + (int)pGround.x + "_" + (int)pGround.z);
            if (!string.IsNullOrEmpty(pPoi)) Poi(pGround + Vector3.up * 2f, pPoi, MapPoi.Kind.Landmark);
        }

        private void DressGrove()
        {
            _pressR = Build(_press, ZONE_GROVE);
            _kasbahR = Build(_kasbah, ZONE_GROVE);
            _baseR = Build(_baseStation, ZONE_GROVE);
            _bunkerR = Build(_bunker, ZONE_GROVE);
            // The screw press (signature), the tithe scale desk with the ledger (vault code).
            _screwPress = _pressR.At(6f, 0, 7f, 0f);
            ArchMesh lPress = new ArchMesh();
            ArchMat lWood = ArchLib.Surface("dark_wood", 1.5f), lIron = ArchLib.Surface("rusty_metal_02", 1.5f);
            lPress.Box(lWood, _screwPress + new Vector3(0, 0.45f, 0), new Vector3(3.2f, 0.9f, 2.4f));
            foreach (float s in new[] { -1.4f, 1.4f }) lPress.Box(lWood, _screwPress + new Vector3(s, 2.4f, 0), new Vector3(0.4f, 4.8f, 0.4f));
            lPress.Box(lWood, _screwPress + new Vector3(0, 4.9f, 0), new Vector3(3.4f, 0.5f, 0.6f));
            lPress.Tube(lIron, _screwPress + new Vector3(0, 1.4f, 0), _screwPress + new Vector3(0, 4.7f, 0), 0.22f, 12);
            lPress.Box(lWood, _screwPress + new Vector3(0, 1.4f, 0), new Vector3(2.6f, 0.3f, 2f));
            GameObject lPressObject = new GameObject("ScrewPress") { layer = Core.GameLayers.INTERACTABLE };
            lPressObject.transform.SetParent(A.Systems, false);
            lPressObject.transform.position = _screwPress;
            foreach (GameObject lPart in lPress.Flush(lPressObject.transform, "Qasr_ScrewPress", Core.GameLayers.INTERACTABLE)) { lPart.isStatic = false; lPart.transform.position -= _screwPress; }
            A.Explosive(_screwPress + new Vector3(2.4f, 0, -1.6f), 0f, true);
            K.Activity(ActivityType.Inspect, _screwPress + new Vector3(0, 0.05f, -2f), 0f, ZONE_GROVE, 30f);
            A.Model("Furn_Desk", _pressR.InRoom("PressOffice", 0.5f, 0.5f, 0f), _press.yaw, _pressR.interior);
            A.Gadget(ItemKind.CodeNote, _pressR.InRoom("PressOffice", 0.5f, 0.5f, 0.82f), "Tithe ledger (vault code)");
            A.Gadget(ItemKind.Keycard, _pressR.InRoom("PressOffice", 0.2f, 0.2f, 0.05f), "Spare lift keycard");
            A.BigCrate(_pressR.InRoom("PressStore", 0.5f, 0.3f, 0f), 0f);
            A.Distraction("caralarm", G(76f, -80f, 0f), 0f);
            Poi(_screwPress + Vector3.up * 2f, "Screw press", MapPoi.Kind.Landmark);
            // Oleander on the channel banks, colocynth in the wadi.
            A.Gadget(ItemKind.Poison, G(POOL + new Vector2(10, -4), 0.1f), "Oleander");
            A.Gadget(ItemKind.Poison, G(-100f, -62f, 0.1f), "Oleander");
            A.Gadget(ItemKind.EmeticPoison, G(-20f, -10f, 0.1f), "Colocynth");
            // Footbridges over the wadi, the rock arch.
            foreach (float x in new[] { -100f, 140f })
            {
                Vector2 c = new Vector2(x, x < 0 ? -10f : -10f);
                ArchOutdoor.Pier(A, c + new Vector2(0, -14f), c + new Vector2(0, 14f), 2.4f, Mathf.Max(K.GroundAt(c.x, c.y - 14f), K.GroundAt(c.x, c.y + 14f)) + 0.1f);
            }
            ArchMat lRock = ArchLib.Surface("cliff_side", 6f);
            ArchMesh lArch = new ArchMesh();
            Vector3 lArchBase = G(128f, -36f, -1f);
            lArch.Box(lRock, lArchBase + new Vector3(-6f, 5f, 0), new Vector3(4f, 12f, 5f), Quaternion.Euler(0, 0, 4f));
            lArch.Box(lRock, lArchBase + new Vector3(6f, 5f, 0), new Vector3(4.5f, 12f, 5.5f), Quaternion.Euler(0, 10f, -3f));
            lArch.Box(lRock, lArchBase + new Vector3(0, 11.5f, 0), new Vector3(17f, 3.5f, 5f), Quaternion.Euler(0, 0, 2f));
            GameObject lArchRoot = new GameObject("RockArch");
            lArchRoot.transform.SetParent(K.Env, false);
            lArch.Flush(lArchRoot.transform, "Qasr_RockArch");
            Poi(lArchBase + Vector3.up * 2f, "Rock arch", MapPoi.Kind.Landmark);
            // Kasbah ruin: an overlook over the hairpins and the gate.
            Poi(_kasbahR.At(7f, 2, 6f, 1f), "Kasbah roof", MapPoi.Kind.Overlook);
            A.CrateStack(_kasbahR.InRoom("KasbahStore", 0.3f, 0.5f, 0f), 0f, 3);
            // The Needle: two ladders and a ledge to the top.
            Vector3 lFoot = G(NEEDLE.x, NEEDLE.y + 7.5f, 0f);
            A.Climb(lFoot, 30f - lFoot.y, Vector3.forward, true, "Needle ladder");
            ArchMesh lLedge = new ArchMesh();
            lLedge.Box(ArchLib.Surface("old_planks_02", 1.5f), new Vector3(NEEDLE.x, 30f, NEEDLE.y + 7.6f), new Vector3(3f, 0.25f, 1.6f));
            lLedge.Box(ArchLib.Surface("old_planks_02", 1.5f), new Vector3(NEEDLE.x, 60.1f, NEEDLE.y), new Vector3(5f, 0.25f, 5f));
            GameObject lLedgeRoot = new GameObject("NeedleLedges");
            lLedgeRoot.transform.SetParent(K.Env, false);
            lLedge.Flush(lLedgeRoot.transform, "Qasr_NeedleLedges");
            A.Climb(new Vector3(NEEDLE.x, 30.1f, NEEDLE.y + 6.9f), 30.2f, Vector3.forward, true, null);
            Poi(new Vector3(NEEDLE.x, 61f, NEEDLE.y), "The Needle", MapPoi.Kind.Overlook);
            for (int i = 0; i < 3; i++) A.Model("Prop_Sandbags", new Vector3(NEEDLE.x - 1.5f + i * 1.5f, 60.22f, NEEDLE.y - 2f), 0f);
            // Goods lift: the keycard cage at the base, the ride up to the third court.
            A.Zipline(_baseR.At(4f, 0, 8.6f, 0f), new Vector3(-66f, MESA_Y + 0.05f, 108f));
            A.Model("Prop_Crate_Wood", _baseR.At(1.2f, 0, -2.5f, 0f), 0f);
            A.Model("Prop_Crate_Wood", _baseR.At(2.2f, 0, -2.6f, 0f), 15f);
            A.BigCrate(_baseR.At(-2.5f, 0, 2f, 0f), 90f);
            // Radar: the dish on its gantry, motor pool, fuel tanks, berms, containers, the ladder face.
            _dish = G(168f, 150f, 0f);
            ArchOutdoor.WaterTower(A, _dish, 13f, 0f);
            ArchMesh lDishMesh = new ArchMesh();
            lDishMesh.Dome(ArchLib.PlainMat("Dish", new Color(0.9f, 0.9f, 0.88f), 0.5f, 0.2f), _dish + Vector3.up * 19f, 6f, 16, 6, Quaternion.Euler(-60f, 200f, 0f));
            GameObject lDishRoot = new GameObject("RadarDish");
            lDishRoot.transform.SetParent(K.Env, false);
            lDishMesh.Flush(lDishRoot.transform, "Qasr_Dish");
            Poi(_dish + Vector3.up * 15f, "Radar dish", MapPoi.Kind.Landmark);
            ArchMesh lDome = new ArchMesh();
            lDome.Dome(ArchLib.PlainMat("RadomeWhite", new Color(0.95f, 0.95f, 0.93f), 0.35f, 0f), G(132f, 150f, 0f), 7f, 18, 7, Quaternion.identity);
            GameObject lDomeRoot = new GameObject("Radome");
            lDomeRoot.transform.SetParent(K.Env, false);
            lDome.Flush(lDomeRoot.transform, "Qasr_Radome");
            ArchOutdoor.Shed(A, G(122f, 100f, 0f), 90f, 14f, 8f, 3.8f, ArchLib.Surface("corrugated_iron_02", 2.5f), ArchLib.Iron);
            A.Model("Prop_Car_Pickup", G(122f, 98f, 0.12f), 0f);
            A.Distraction("generator", G(118f, 104f, 0.12f), 0f);
            A.Dumpster(G(128f, 92f, 0f), 0f);
            ArchOutdoor.FuelTank(A, G(112f, 116f, 0f), 0f, 6f, 1.2f);
            ArchOutdoor.FuelTank(A, G(112f, 124f, 0f), 0f, 6f, 1.2f);
            for (int i = 0; i < 5; i++) A.Model("Prop_Sandbags", G(140f + i * 1.8f, 86f, 0f), 0f);
            for (int i = 0; i < 3; i++)
                A.BoxObject("Container", G(176f, 100f + i * 7f, 0f), 90f, K.Props, 0, true, (ArchLib.Surface(i == 1 ? "blue_metal_plate" : "green_metal_rust", 2f), new Vector3(0, 1.3f, 0), new Vector3(2.45f, 2.6f, 6f)));
            A.Climb(G(130f, 74f, 0f), LITTLE_Y - K.GroundAt(130f, 74f) + 0.2f, Vector3.back, true, "Ladder face");
            Vector3 lGate = G(150f, 77.5f, 0f);
            ArchOutdoor.LockedGate(A, lGate - new Vector3(1.8f, 0, 0), -90f, 3.6f, 2.4f, SecureDoor.Lock.Keycard, true);
            ArchOutdoor.Fence(A, new Vector2(LITTLE.xMin + 2f, 77.5f), new Vector2(148f, 77.5f));
            ArchOutdoor.Fence(A, new Vector2(152f, 77.5f), new Vector2(LITTLE.xMax - 2f, 77.5f));
            Poi(lGate + Vector3.up, "Radar gate", MapPoi.Kind.Entrance);
            Poi(_bunkerR.Center("BunkerHall", 1f), "Radar bunker", MapPoi.Kind.Landmark);
            // Rope bridge from the citadel's east wall to the Little Mesa.
            Vector3 a = new Vector3(CITADEL.xMax + 1f, MESA_Y + 0.1f, 124f), b = new Vector3(LITTLE.xMin + 2f, LITTLE_Y + 0.1f, 124f);
            ArchMesh lBridge = new ArchMesh();
            int lN = 24;
            for (int i = 0; i < lN; i++)
            {
                float t0 = (float)i / lN, t1 = (float)(i + 1) / lN;
                Vector3 p0 = Vector3.Lerp(a, b, t0) - Vector3.up * Mathf.Sin(t0 * Mathf.PI) * 1.4f, p1 = Vector3.Lerp(a, b, t1) - Vector3.up * Mathf.Sin(t1 * Mathf.PI) * 1.4f;
                lBridge.Box(ArchLib.Surface("old_planks_02", 1.2f), (p0 + p1) * 0.5f, new Vector3(1.6f, 0.1f, Vector3.Distance(p0, p1) + 0.05f), Quaternion.LookRotation(p1 - p0));
                foreach (float s in new[] { -0.85f, 0.85f })
                {
                    lBridge.Tube(ArchLib.Surface("hessian_230", 0.5f), p0 + new Vector3(0, 1f, s), p1 + new Vector3(0, 1f, s), 0.03f, 5, false);
                    lBridge.Box(ArchLib.Invisible, (p0 + p1) * 0.5f + new Vector3(0, 0.55f, s), new Vector3(0.05f, 1f, Vector3.Distance(p0, p1)), Quaternion.LookRotation(p1 - p0));
                }
            }
            GameObject lBridgeRoot = new GameObject("RopeBridge");
            lBridgeRoot.transform.SetParent(K.Env, false);
            lBridge.Flush(lBridgeRoot.transform, "Qasr_RopeBridge");
            Poi((a + b) * 0.5f, "Rope bridge", MapPoi.Kind.Shortcut);
        }

        private void DressCitadel()
        {
            _keepR = Build(_keep, ZONE_CITADEL);
            foreach (ArchSpec lSpec in _bastionSpecs) _bastions.Add(Build(lSpec, ZONE_CITADEL));
            foreach (ArchSpec lSpec in _ranges) Build(lSpec, ZONE_CITADEL);
            Poi(Kp(17f, 4, 17f, 2f), "The keep", MapPoi.Kind.Landmark);
            // Citadel wall: main gate (S), goods-lift gap (W), SE postern (Crowbar), bridge gate (E).
            Vector2[] lWall = { new Vector2(CITADEL.xMin, CITADEL.yMin), new Vector2(CITADEL.xMax, CITADEL.yMin), new Vector2(CITADEL.xMax, CITADEL.yMax), new Vector2(CITADEL.xMin, CITADEL.yMax) };
            List<ArchOutdoor.Gate> lGates = new List<ArchOutdoor.Gate>
            {
                new ArchOutdoor.Gate { segment = 0, at = (-4f - CITADEL.xMin) / CITADEL.width, width = 5f, poi = "Main gate" },
                new ArchOutdoor.Gate { segment = 3, at = (CITADEL.yMax - 108f) / CITADEL.height, width = 3f, ironGate = false, poi = "Goods lift" },
                new ArchOutdoor.Gate { segment = 1, at = (70f - CITADEL.yMin) / CITADEL.height, width = 1.6f, locked = SecureDoor.Lock.Crowbar, ironGate = false, poi = "SE postern" },
                new ArchOutdoor.Gate { segment = 1, at = (124f - CITADEL.yMin) / CITADEL.height, width = 2.4f, poi = "Bridge gate" },
                new ArchOutdoor.Gate { segment = 2, at = (CITADEL.xMax - (-10f)) / CITADEL.width, width = 1.4f, ironGate = false, poi = "Cleft" }
            };
            ArchStyle lFort = ArchStyles.Fortress();
            ArchOutdoor.Wall(A, lWall, true, 4.5f, 1.2f, lFort.facade, lFort.trim, lGates);
            Gateway(G(-4f, CITADEL.yMin, 0f), 0f, 5f, 5.4f, null);
            // Courts: first (S) tables for the petitioners, second (E) drill yard, sheds, scaffold, third (W) kitchen yard.
            for (int i = 0; i < 3; i++) A.Model("Furn_DiningTable", G(-20f + i * 10f, 80f, 0f), 0f);
            for (int i = 0; i < 6; i++) K.LandTree("Veg_DatePalm", -30f + i * 11f, 88f, 0.7f);
            ArchOutdoor.Fountain(A, G(-3f, 80f, 0f), 2.4f);
            A.Model("Prop_Car_Pickup", G(28f, 104f, 0f), 180f);
            A.Model("Prop_Car_Pickup", G(34f, 104f, 0f), 180f);
            ArchOutdoor.Shed(A, G(30f, 136f, 0f), 0f, 12f, 6f, 3.4f, ArchLib.Surface("corrugated_iron_02", 2.5f));
            A.Distraction("generator", G(36f, 134f, 0.12f), 0f);
            A.Dumpster(G(-60f, 125f, 0f), 90f);
            A.CrateStack(G(-60f, 132f, 0f), 0f, 4);
            A.Camera(Kp(-0.3f, 0, 29.5f, 3f), G(-50f, 118f, 1f), ZONE_CITADEL);
            // Scaffolding up the keep's east face to the roof (the wind-tower climb), the crowbar on it.
            Vector3 lScaffold = Kp(34.6f, 0, 26f, 0f);
            ArchMesh lPoles = new ArchMesh();
            for (int f = 0; f < 4; f++)
            {
                lPoles.Box(ArchLib.Surface("old_planks_02", 1.5f), lScaffold + new Vector3(0.9f, f * 3.6f + 3.4f, 0f), new Vector3(1.6f, 0.08f, 6f));
                foreach (float z in new[] { -2.9f, 2.9f }) lPoles.Tube(ArchLib.Galvanised, lScaffold + new Vector3(1.6f, 0f, z), lScaffold + new Vector3(1.6f, 15f, z), 0.05f, 6);
            }
            GameObject lScaffoldRoot = new GameObject("Scaffold");
            lScaffoldRoot.transform.SetParent(K.Env, false);
            lPoles.Flush(lScaffoldRoot.transform, "Qasr_Scaffold");
            A.Climb(lScaffold + new Vector3(0.1f, 0f, -2.2f), 14.6f, Vector3.right, true, "Scaffold climb");
            A.Gadget(ItemKind.Crowbar, lScaffold + new Vector3(0.9f, 3.5f, 1.5f), "Scaffold crowbar");
            // Cliff chimney + scaffold on the west face (W3).
            A.Climb(new Vector3(CITADEL.xMin - 6.4f, K.GroundAt(CITADEL.xMin - 7f, 150f), 150f), MESA_Y - K.GroundAt(CITADEL.xMin - 7f, 150f) + 1.5f, Vector3.left, true, "Cliff chimney");

            // ---- keep systems
            A.Camera(Kp(22.4f, 0, 0.6f, 3.1f), Kp(17f, 0, 6f, 1f), ZONE_CITADEL);
            A.Camera(Kp(22.6f, 1, 22.4f, 3f), Kp(12f, 1, 21f, 1f), ZONE_CITADEL);
            A.Camera(Kp(22.6f, 2, 11.6f, 3f), Kp(12f, 2, 13f, 1f), ZONE_CITADEL);
            A.Camera(Kp(10.6f, 3, 22.4f, 3f), Kp(3f, 3, 12f, 1f), ZONE_CITADEL);
            A.Camera(Kp(22.6f, -1, 33.2f, 3f), Kp(14f, -1, 26f, 0f), ZONE_CITADEL);
            A.Recorder(Kp(32.6f, -1, 5.5f, 0f), -90f, ZONE_CITADEL);
            List<Transform> lVents = new List<Transform>();
            foreach (VentCover lCover in _keepR.systems.GetComponentsInChildren<VentCover>()) lVents.Add(lCover.transform);
            A.AirHandler(Kp(16f, -1, 32f, 0f), 180f, _keepR.volume, lVents.ToArray());
            for (int i = 0; i < 3; i++) A.Model("Prop_ExplosiveBarrel", Kp(20f + i * 0.8f, -1, 32.8f, 0f), 0f);
            A.Breaker(Kp(33.4f, -1, 26f, 0f), -90f, _keepR.volume);
            A.Chandelier(Kp(22f, 1, 5.5f, 3.25f), 3f);
            A.Chandelier(Kp(17f, 3, 5.5f, 3.25f), 3f);
            A.Explosive(Kp(30f, 2, 28f, 0f), 0f, true);
            A.Distraction("radio", Kp(31f, 2, 32.8f, 0f), 180f);
            A.Distraction("phone", Kp(28f, 3, 28f, 0.8f), 0f);
            A.Distraction("phone", Kp(28f, 1, 16f, 0.8f), 0f);
            A.Drinks(Kp(28f, 3, 4f, 0.8f), 0f, ZONE_CITADEL, 2, true, 1.2f);
            A.Model("Furn_DiningTable", Kp(28f, 3, 4f, 0f), 0f);
            A.Gadget(ItemKind.EmeticPoison, Kp(12.5f, 3, 30f, 0.9f), "Colocynth (pantry)");
            A.LaundryCart(Kp(14.5f, 3, 30.5f, 0f), 180f);
            A.LaundryCart(Kp(31f, 0, 9f, 0f), 0f);
            A.Wardrobe(Kp(1f, -1, 6f, 0f), 90f, _keepR.interior);
            A.Model("Prop_Helicopter", Kp(25f, 4, 26f, 0f), 30f);
            K.Water(Kp(28.5f, 3, 4f, -0.25f), new Vector2(7f, 4f));
            // The officers' balcony drop ladder (b9) into the second court.
            A.DropLadder(Kp(35.2f, 2, 5.5f, 0f), Kp(35.2f, 0, 5.5f, 0f), Vector3.right, "Officers' balcony ladder");
            // Wind tower on the roof.
            ArchMesh lTower = new ArchMesh();
            ArchMat lAdobe = ArchLib.Tinted("clay_plaster", OCHRES[0], "adobe_h0", 3.5f);
            Vector3 lTowerBase = Kp(12.5f, 4, 32.5f, 0f);
            foreach (Vector3 o in new[] { new Vector3(-1.2f, 0, -1.2f), new Vector3(1.2f, 0, -1.2f), new Vector3(-1.2f, 0, 1.2f), new Vector3(1.2f, 0, 1.2f) })
                lTower.Box(lAdobe, lTowerBase + o + Vector3.up * 2.5f, new Vector3(0.4f, 5f, 0.4f));
            lTower.Box(lAdobe, lTowerBase + Vector3.up * 5.2f, new Vector3(3f, 0.5f, 3f));
            GameObject lTowerRoot = new GameObject("WindTower");
            lTowerRoot.transform.SetParent(K.Env, false);
            lTower.Flush(lTowerRoot.transform, "Qasr_WindTower");
            // The roof zipline to the kasbah (b8).
            A.Zipline(Kp(2f, 4, 2f, 0f), _kasbahR.At(7f, 2, 6f, 0f));
            _lookoutRoof = Kp(4f, 4, 30f);
        }

        private void Tunnels()
        {
            // Qanat: a dry tunnel from the town and from the dune edge, joining under the grove, then
            // climbing inside the mesa to the well shaft (two ladders) into the cistern landing.
            Vector3 lShaftBottom = Kp(5.5f, -1, 29f, -12f);
            Cave("Qanat_Town", 3.2f, 2.8f, G(88f, -112f, -0.2f), new Vector3(80f, -6f, -100f), new Vector3(40f, -9f, -60f), new Vector3(10f, -11f, -20f), new Vector3(-6f, -6f, 30f), new Vector3(-12f, 10f, 70f), new Vector3(-14f, lShaftBottom.y, 100f), new Vector3(lShaftBottom.x, lShaftBottom.y, lShaftBottom.z - 2f));
            Cave("Qanat_Dunes", 3.2f, 2.8f, G(-70f, -196f, -0.2f), new Vector3(-60f, -7f, -170f), new Vector3(-10f, -9f, -90f), new Vector3(40f, -9f, -60f));
            ArchMesh lShaft = new ArchMesh();
            ArchMat lStone = ArchLib.Surface("large_sandstone_blocks", 2f);
            float lTop = Kp(0f, -1, 0f, 0f).y;
            Vector3 c = new Vector3(lShaftBottom.x, 0f, lShaftBottom.z);
            foreach (Vector3 o in new[] { new Vector3(-1.3f, 0, 0), new Vector3(1.3f, 0, 0), new Vector3(0, 0, -1.3f), new Vector3(0, 0, 1.3f) })
                lShaft.Box(lStone, c + o + Vector3.up * ((lShaftBottom.y + lTop) * 0.5f + 0.6f), new Vector3(o.x != 0 ? 0.4f : 2.6f, lTop - lShaftBottom.y + 1.2f, o.z != 0 ? 0.4f : 2.6f));
            GameObject lShaftRoot = new GameObject("WellShaft");
            lShaftRoot.transform.SetParent(K.Env, false);
            lShaft.Flush(lShaftRoot.transform, "Qasr_WellShaft");
            A.Climb(new Vector3(c.x, lShaftBottom.y, c.z - 0.9f), lTop - lShaftBottom.y + 0.4f, Vector3.forward, true, "Well shaft");
            A.Climb(new Vector3(c.x + 0.9f, lShaftBottom.y, c.z), lTop - lShaftBottom.y + 0.4f, Vector3.left, true, null);
            Poi(G(88f, -112f, 1f), "Qanat mouth", MapPoi.Kind.Underground);
            Poi(G(-70f, -196f, 1f), "Qanat exit", MapPoi.Kind.Underground);
            // The smugglers' cleft: radar bunker cellar -> under the saddle -> north terrace.
            Vector3 lBunkerDoor = _bunkerR.At(0f, -1, 9f, 0f) + _bunkerR.Rotation * new Vector3(-0.6f, 0, 0);
            Cave("Cleft", 3f, 2.8f, lBunkerDoor, lBunkerDoor + new Vector3(-8f, 0, 6f), new Vector3(90f, 28f, 165f), new Vector3(40f, 32f, 168f), new Vector3(-10f, MESA_Y - 0.1f, CITADEL.yMax + 4f));
            Poi(new Vector3(-10f, MESA_Y + 1f, CITADEL.yMax + 4f), "Cleft mouth", MapPoi.Kind.Underground);
        }
    }
}
