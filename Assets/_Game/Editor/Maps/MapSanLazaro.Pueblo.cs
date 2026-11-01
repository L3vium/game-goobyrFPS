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
        private static readonly Color[] FACADES =
        {
            new Color(0.98f, 0.97f, 0.93f), new Color(0.99f, 0.92f, 0.78f), new Color(0.96f, 0.8f, 0.52f), new Color(0.74f, 0.87f, 0.96f),
            new Color(0.98f, 0.8f, 0.76f), new Color(0.78f, 0.93f, 0.83f), new Color(0.99f, 0.9f, 0.58f), new Color(0.98f, 0.97f, 0.93f)
        };
        private static readonly Color[] SHUTTERS = { new Color(0.22f, 0.5f, 0.5f), new Color(0.14f, 0.28f, 0.55f), new Color(0.5f, 0.18f, 0.12f), new Color(0.2f, 0.45f, 0.25f) };

        private readonly List<(ArchSpec spec, int zone, string role)> _pueblo = new List<(ArchSpec, int, string)>();
        private ArchSpec _church, _cantina, _police;
        private ArchResult _churchR, _cantinaR, _policeR;
        private readonly List<ArchResult> _houses = new List<ArchResult>();
        private int _houseCount;

        /// <summary>A pueblo house: shut (barred, nobody in) or enterable (shop or home, kitchen, upstairs room, roof).</summary>
        private ArchSpec House(Vector2 pFront, int pFacing, float pWidth, float pDepth, int pFloors, bool pEnterable, string pRole, bool pTerrace)
        {
            int k = _houseCount++;
            ArchStyle lStyle = ArchStyles.Colonial(FACADES[k % FACADES.Length], "p" + (k % FACADES.Length), SHUTTERS[(k / 2) % SHUTTERS.Length]);
            lStyle.outerThickness = 0.4f;
            ArchSpec h = Facing("House" + k + (pEnterable ? "_" + pRole : ""), pFront, PUEBLO_Y, pFacing, pWidth, pDepth, lStyle, ZONE_PUEBLO);
            h.shut = !pEnterable;
            h.plinth = 1.4f;
            RoomType lFront = pRole switch { "shop" => RoomType.Shop, "bar" => RoomType.Bar, "office" => RoomType.Office, "clinic" => RoomType.Clinic, _ => RoomType.Living };
            if (!pEnterable)
            {
                for (int f = 0; f < pFloors; f++) h.Room("Shut" + f, f, 0, 0, pWidth, pDepth, RoomType.Bedroom, SecurityTier.Restricted).noFurniture = true;
                h.Door(0, pWidth * 0.5f, 0, DoorKind.Door, 1.2f);
            }
            else
            {
                float lSplit = Mathf.Round(pDepth * 0.55f * 2f) / 2f;
                h.Room("Front", 0, 0, 0, pWidth, lSplit, lFront, SecurityTier.Public);
                h.Room("Kitchen", 0, 0, lSplit, pWidth, pDepth, RoomType.Kitchen, SecurityTier.Restricted);
                h.Door(0, pWidth * 0.5f, 0, pRole == "shop" || pRole == "bar" ? DoorKind.Double : DoorKind.Door, pRole == "shop" || pRole == "bar" ? 2.0f : 1.2f);
                h.Door(0, pWidth * 0.3f, lSplit);
                h.Door(0, pWidth * 0.75f, pDepth, DoorKind.Door, 1.1f);
                if (pFloors > 1)
                {
                    h.Stair(0, 0.9f, pDepth - 1.5f, pWidth - 1.1f, pDepth - 0.45f, 3, false);
                    h.Room("Bedroom", 1, 0, 0, pWidth, lSplit, RoomType.Bedroom, SecurityTier.Restricted);
                    h.Room("Landing", 1, 0, lSplit, pWidth, pDepth, RoomType.Corridor, SecurityTier.Restricted);
                    h.Door(1, pWidth * 0.75f, lSplit);
                    h.balconies.Add(new ArchBalcony { floor = 1, at = new Vector2(pWidth * 0.5f, 0), width = Mathf.Min(3f, pWidth - 2f), depth = 0.9f });
                    if (pTerrace)
                    {
                        ArchRoom lRoof = h.Room("RoofTerrace", pFloors, 0, 0, pWidth, pDepth, RoomType.Terrace, SecurityTier.Public);
                        lRoof.open = true;
                        h.holes.Add((pFloors, new Rect(0.6f, lSplit + 0.6f, 1f, 1f)));
                    }
                }
            }
            if (!pTerrace || !pEnterable) h.Roof(pFloors - 1, -0.01f, -0.01f, pWidth + 0.01f, pDepth + 0.01f, RoofKind.Gable, true, 24f);
            if (pFloors > 1) h.cornices.Add(1);
            if (k % 3 == 0) h.drains.Add(new ArchDrain { at = new Vector2(0.35f, 0), topFloor = pFloors - 1 });
            _pueblo.Add((h, ZONE_PUEBLO, pRole));
            return h;
        }

        private void PlanPueblo()
        {
            // The church on the plaza's north side: nave, sacristy, bell tower with its deck.
            ArchStyle lChurchStyle = ArchStyles.Colonial(new Color(0.99f, 0.98f, 0.95f), "church");
            _church = Facing("Iglesia", new Vector2(-30f, -124f), PUEBLO_Y, 2, 20f, 26f, lChurchStyle, ZONE_PUEBLO);
            ArchSpec c = _church;
            c.Tall("Nave", 0, 3, 6, 0, 20, 26, RoomType.Church, SecurityTier.Public);
            c.Room("Sacristy", 0, 0, 7, 6, 26, RoomType.Sacristy, SecurityTier.Restricted);
            for (int f = 0; f <= 5; f++) c.Room("BellTower" + f, f, 0, 0, 6, 7, RoomType.Stair, SecurityTier.Restricted).noFurniture = true;
            ArchRoom lDeck = c.Room("BellDeck", 6, 0, 0, 6, 7, RoomType.Belvedere, SecurityTier.Restricted); lDeck.arcade = true;
            for (int f = 0; f <= 5; f++) c.Stair(f, 0.6f, 1.8f, 5.4f, 6.4f, 0, true, true);
            c.Door(0, 13, 0, DoorKind.Double, 2.6f).poi = "Church";
            c.Door(0, 6, 1.2f);
            c.Door(0, 6, 16);
            c.Door(0, 0, 20, DoorKind.Door, 1.1f);
            c.Roof(2, 6, 0, 20, 26, RoofKind.Gable, false, 30f);
            c.Roof(6, 0, 0, 6, 7, RoofKind.Hip, true, 32f).overhang = 0.5f;
            c.Roof(0, 0, 7, 6, 26, RoofKind.Shed, false, 18f).lowSide = 3;
            c.cornices.Add(3);
            c.drains.Add(new ArchDrain { at = new Vector2(20f, 12f), topFloor = 2, poi = "Church drainpipe" });

            // The cantina on the plaza's west side, its cellar and the patrón's floor.
            ArchStyle lCantinaStyle = ArchStyles.Colonial(new Color(0.97f, 0.82f, 0.55f), "cantina", new Color(0.5f, 0.18f, 0.12f));
            _cantina = Facing("Cantina", new Vector2(-56f, -144f), PUEBLO_Y, 1, 16f, 13f, lCantinaStyle, ZONE_PUEBLO);
            ArchSpec t = _cantina;
            t.Room("CantinaCellar", -1, 0, 0, 16, 13, RoomType.WineCellar, SecurityTier.Restricted, AccessLevel.Staff);
            t.Room("Bar", 0, 0, 0, 16, 8, RoomType.Bar, SecurityTier.Public);
            t.Room("CantinaKitchen", 0, 0, 8, 7, 13, RoomType.Kitchen, SecurityTier.Restricted, AccessLevel.Staff);
            t.Room("CantinaWC", 0, 7, 8, 10, 13, RoomType.Bath, SecurityTier.Public);
            t.Room("CantinaStore", 0, 10, 8, 16, 13, RoomType.Store, SecurityTier.Restricted, AccessLevel.Staff).noFurniture = true;
            t.Room("PatronOffice", 1, 0, 0, 8, 13, RoomType.Office, SecurityTier.Restricted, AccessLevel.Staff);
            t.Room("PatronRoom", 1, 8, 0, 16, 8, RoomType.Bedroom, SecurityTier.Restricted, AccessLevel.Staff);
            t.Room("CantinaLanding", 1, 8, 8, 16, 13, RoomType.Corridor, SecurityTier.Restricted, AccessLevel.Staff);
            t.Door(0, 6, 0, DoorKind.Double, 2.2f).poi = "Cantina";
            t.Door(0, 11, 0, DoorKind.Double, 1.8f);
            t.Door(0, 3.5f, 8); t.Door(0, 8.5f, 8); t.Door(0, 13, 8);
            t.Door(0, 3.5f, 13, DoorKind.Door, 1.2f).poi = "Cantina back door";
            t.Door(-1, 16, 4, DoorKind.Door, 1.4f).poi = "Cave entrance";
            t.Stair(-1, 11.6f, 8.4f, 15.6f, 12.6f, 3, true, true);
            t.Stair(0, 11.6f, 8.4f, 15.6f, 12.6f, 3, true, true);
            t.Door(1, 8, 4); t.Door(1, 4, 13 - 0.01f); t.Door(1, 8, 10.5f);
            t.balconies.Add(new ArchBalcony { floor = 1, at = new Vector2(11.5f, 0), width = 5f, depth = 1.2f });
            t.Roof(1, 0, 0, 16, 13, RoofKind.Hip, true, 24f);
            t.cornices.Add(1);
            t.drains.Add(new ArchDrain { at = new Vector2(16f, 2f), topFloor = 1 });
            _pueblo.Add((t, ZONE_PUEBLO, "cantina"));

            // The police post on the market street.
            _police = Facing("PolicePost", new Vector2(46f, -132.8f), PUEBLO_Y, 2, 12f, 10f, ArchStyles.Colonial(new Color(0.8f, 0.88f, 0.95f), "police", new Color(0.14f, 0.28f, 0.55f)), ZONE_PUEBLO);
            _police.Room("Desk", 0, 0, 0, 12, 5, RoomType.Police, SecurityTier.Hostile, AccessLevel.Security);
            _police.Room("Cells", 0, 0, 5, 6, 10, RoomType.Store, SecurityTier.Hostile, AccessLevel.Security);
            _police.Room("Armoury", 0, 6, 5, 12, 10, RoomType.Armoury, SecurityTier.Hostile, AccessLevel.Security);
            _police.Door(0, 6, 0, DoorKind.Door, 1.4f).poi = "Police post";
            _police.Door(0, 3, 5); _police.Door(0, 9, 5);
            _police.Door(0, 3, 10, DoorKind.Door, 1.2f);
            _police.drains.Add(new ArchDrain { at = new Vector2(12f, 8f), topFloor = 0 });
            _pueblo.Add((_police, ZONE_PUEBLO, "police"));

            // Rows of houses: 12 enterable, the rest shut and barred (fiesta day, everyone is out).
            string[] lRoles = { "shop", "home", "bar", "home", "shop", "clinic", "home", "office", "shop", "home", "home", "shop" };
            int lOpen = 0;
            bool Open(int pIndex) => pIndex % 3 != 1 && lOpen < 12;
            // Market street, north side (facing south), then south side.
            float x = -4f;
            for (int i = 0; x < 82f; i++)
            {
                float w = 8f + (i % 3) * 1f;
                if (x + w > 39f && x < 53f) { x = 53f; continue; }
                bool lEnter = Open(i);
                House(new Vector2(x + w * 0.5f, -132.8f), 2, w, 9f, 2, lEnter, lEnter ? lRoles[lOpen++ % lRoles.Length] : "", i % 2 == 0);
                x += w + (i % 3 == 2 ? 2.5f : 0.2f);
            }
            x = -2f;
            for (int i = 0; x < 82f; i++)
            {
                float w = 8f + ((i + 1) % 3) * 1f;
                bool lEnter = Open(i + 1);
                House(new Vector2(x + w * 0.5f, -143.2f), 0, w, 9f, 2, lEnter, lEnter ? lRoles[lOpen++ % lRoles.Length] : "", i % 2 == 1);
                x += w + (i % 3 == 1 ? 2.5f : 0.2f);
            }
            // Plaza south side (facing north) and the riverside row behind it (facing the river).
            x = -52f;
            for (int i = 0; x < -10f; i++)
            {
                float w = 9f + (i % 2);
                House(new Vector2(x + w * 0.5f, -165f), 0, w, 9f, 2, Open(i + 2), "home", i % 2 == 0);
                x += w + 0.2f;
            }
            x = -100f;
            for (int i = 0; x < -60f; i++)
            {
                float w = 9f;
                House(new Vector2(x + w * 0.5f, -178f), 2, w, 8f, 1, false, "", false);
                x += w + 3f;
            }
            // West of the plaza around the cantina, and the row behind the church.
            House(new Vector2(-56f, -130f), 1, 9f, 10f, 2, false, "", true);
            House(new Vector2(-56f, -158f), 1, 8f, 10f, 2, false, "", false);
            for (int i = 0; i < 3; i++) House(new Vector2(-104f + i * 12f, -118f), 2, 10f, 9f, 2, i == 1 && lOpen < 12, "home", i != 1);
            for (int i = 0; i < 3; i++) House(new Vector2(-6f + i * 11f, -112f), 2, 10f, 9f, 1, false, "", false);
            // East of the plaza.
            House(new Vector2(-6f, -158.5f), 3, 9f, 9f, 2, lOpen < 12, "bar", false);
            if (lOpen < 12) Debug.Log($"[SanLazaro] {lOpen} enterable houses");
        }

        private void DressPueblo()
        {
            _churchR = Build(_church, ZONE_PUEBLO);
            foreach ((ArchSpec lSpec, int lZone, string lRole) in _pueblo)
            {
                ArchResult r = Build(lSpec, lZone);
                if (lSpec == _cantina) _cantinaR = r;
                else if (lSpec == _police) _policeR = r;
                else _houses.Add(r);
            }
            Vector3 lTower = _churchR.At(3f, 6, 3.5f);
            Poi(lTower, "Bell tower", MapPoi.Kind.Landmark);
            Poi(_churchR.At(3f, 6, 3.5f, 1f), "Bell deck", MapPoi.Kind.Overlook);
            // The bell.
            ArchMesh lBell = new ArchMesh();
            lBell.Cylinder(ArchLib.Brass, _churchR.At(3f, 6, 3.5f, 1.4f), 0.55f, 0.9f, 14);
            lBell.Box(ArchLib.Iron, _churchR.At(3f, 6, 3.5f, 2.8f), new Vector3(0.08f, 1.1f, 0.08f));
            lBell.Box(ArchLib.Iron, _churchR.At(3f, 6, 3.5f, 3.3f), new Vector3(5f, 0.15f, 0.15f));
            GameObject lBellObject = new GameObject("Bell");
            lBellObject.transform.SetParent(K.Props, false);
            lBell.Flush(lBellObject.transform, "SanLazaro_Bell");

            // Plaza: the fiesta stage, the fountain, benches, lamps, bunting.
            Vector3 lStage = G(-16f, -146f, 0f);
            ArchOutdoor.Stage(A, lStage, -90f, 9f, 6f);
            // The fireworks: mortar racks on crates behind the stage, the "pólvora" sign.
            ArchMesh lFireworks = new ArchMesh();
            ArchMat lTube = ArchLib.Tinted("painted_metal_shutter", new Color(0.2f, 0.25f, 0.6f), "mortar", 1f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = lStage + new Vector3(-6.5f, 0f, -3f + k * 3f);
                lFireworks.Box(ArchLib.Surface("wooden_planks", 1f), c + Vector3.up * 0.35f, new Vector3(1.2f, 0.7f, 1.2f));
                for (int t = 0; t < 9; t++) lFireworks.Cylinder(lTube, c + new Vector3(-0.4f + (t % 3) * 0.4f, 0.7f, -0.4f + (t / 3) * 0.4f), 0.08f, 0.55f, 10);
            }
            GameObject lFireworksRoot = new GameObject("Fireworks");
            lFireworksRoot.transform.SetParent(K.Props, false);
            lFireworks.Flush(lFireworksRoot.transform, "SanLazaro_Fireworks");
            ArchOutdoor.Sign(A, lStage + new Vector3(-5f, 0f, 2.5f), -90f, "Pólvora - Peligro");
            ArchOutdoor.Sign(A, lStage + new Vector3(4.2f, 0f, 5f), 90f, "Solo personal");
            A.Distraction("radio", lStage + new Vector3(-1f, 1.1f, 2.5f), 90f);
            ArchOutdoor.Fountain(A, G(-32f, -146f, 0f), 2.6f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Vector3 p = G(-32f + Mathf.Cos(a) * 9f, -146f + Mathf.Sin(a) * 9f, 0f);
                A.Model("Furn_PottedPlant", p, i * 60f);
            }
            foreach (Vector2 p in new[] { new Vector2(-50, -126), new Vector2(-10, -126), new Vector2(-50, -160), new Vector2(-10, -160), new Vector2(-30, -126), new Vector2(-30, -160) })
                A.Lamp(G(p, 0f), 0f, new Color(1f, 0.78f, 0.5f), 16f, 2.4f);
            // Royal palms on the plaza's corners, papel picado strung across it.
            foreach (Vector2 p in new[] { new Vector2(-48, -128), new Vector2(-12, -128), new Vector2(-48, -158), new Vector2(-12, -158) }) K.LandTree("Veg_RoyalPalm", p.x, p.y, 0.75f);
            Color[] lFlags = { new Color(0.95f, 0.8f, 0.1f), new Color(0.1f, 0.25f, 0.7f), new Color(0.8f, 0.1f, 0.12f), new Color(0.2f, 0.6f, 0.3f), new Color(0.95f, 0.45f, 0.7f) };
            ArchMesh lBunting = new ArchMesh();
            for (int line = 0; line < 6; line++)
            {
                Vector3 a = G(-50f, -128f - line * 6f, 6.5f), b = G(-10f, -132f - line * 5f, 6f);
                lBunting.Tube(ArchLib.Iron, a, b, 0.01f, 4, false);
                for (int k = 1; k < 26; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / 26f) - Vector3.up * Mathf.Sin(k / 26f * Mathf.PI) * 0.8f;
                    Vector3 lDir = (b - a).normalized, lSide = Vector3.Cross(lDir, Vector3.up).normalized * 0.01f;
                    ArchMat lFlag = ArchLib.PlainMat("Flag" + (k % lFlags.Length), lFlags[k % lFlags.Length], 0.2f, 0f);
                    lBunting.Tri(lFlag, p - lDir * 0.22f, p + lDir * 0.22f, p - Vector3.up * 0.38f, false);
                    lBunting.Tri(lFlag, p + lDir * 0.22f, p - lDir * 0.22f, p - Vector3.up * 0.38f, false);
                }
            }
            GameObject lBuntingRoot = new GameObject("Bunting");
            lBuntingRoot.transform.SetParent(K.Env, false);
            lBunting.Flush(lBuntingRoot.transform, "SanLazaro_Bunting");
            // Fireworks mortars under the stage (explosive), the empanada cart's gas tank on the cantina terrace.
            A.Explosive(lStage + new Vector3(0.5f, 0f, 1.5f), 0f, true);
            A.Explosive(lStage + new Vector3(-0.8f, 0f, -1.2f), 0f, true);
            A.Explosive(G(-52.5f, -140f, 0f), 0f, true);
            ArchOutdoor.Stall(A, G(-53.5f, -137f, 0f), 90f, new Color(0.9f, 0.75f, 0.2f));
            // Cantina terrace tables with drinks (the sponsor's glass).
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = G(-50.5f, -152f + i * 4f, 0f);
                A.Model("Furn_DiningTable", p, 90f);
                A.Model("Furn_DiningChair", p + new Vector3(-1f, 0, 0), 90f);
                A.Model("Furn_DiningChair", p + new Vector3(1f, 0, 0), -90f);
                if (i == 1) A.Drinks(p + Vector3.up * 0.77f, 90f, ZONE_PUEBLO, 2, true, 1.2f);
            }
            // Market stalls along the street.
            Color[] lAwnings = { new Color(0.85f, 0.2f, 0.15f), new Color(0.15f, 0.45f, 0.75f), new Color(0.95f, 0.75f, 0.15f), new Color(0.2f, 0.6f, 0.3f), new Color(0.9f, 0.45f, 0.65f) };
            for (int i = 0; i < 10; i++)
            {
                float sx = 2f + i * 8f;
                if (sx > 38f && sx < 54f) continue;
                bool lNorth = i % 2 == 0;
                ArchOutdoor.Stall(A, G(sx, lNorth ? -134.0f : -142.0f, 0f), lNorth ? 180f : 0f, lAwnings[i % lAwnings.Length]);
            }
            for (float sx = 0f; sx < 84f; sx += 16f) A.Lamp(G(sx, -133.4f, 0f), 180f, new Color(1f, 0.8f, 0.55f), 14f, 2f);

            // The dock: a quay wall, stairs down to the pier, boats.
            ArchOutdoor.Retaining(A, new Vector2(-62f, -195.5f), new Vector2(8f, -196.5f), PUEBLO_Y + 0.4f, 1.0f);
            ArchOutdoor.Stairs(A, new Vector3(-22f, -2.9f, -201f), new Vector3(-22f, PUEBLO_Y, -193.5f), 2.2f);
            ArchOutdoor.Pier(A, new Vector2(-30f, -197f), new Vector2(-30f, -214f), 3.2f, -2.9f);
            ArchOutdoor.Pier(A, new Vector2(-30f, -214f), new Vector2(-14f, -214f), 2.6f, -2.9f);
            A.Model("Prop_Boat", new Vector3(-34.5f, WATER_Y - 0.3f, -208f), 0f, null, ArchLib.Collide.Box);
            A.Model("Prop_Boat", new Vector3(-20f, WATER_Y - 0.3f, -218.5f), 90f, null, ArchLib.Collide.Box);
            Poi(new Vector3(-30f, -2f, -205f), "Dock", MapPoi.Kind.Landmark);
            A.Zipline(_churchR.At(1.5f, 6, 1.0f, 0.05f), new Vector3(-29.5f, -2.85f, -211f));

            // The bus stop at the street's east end.
            ArchOutdoor.Shed(A, G(80f, -128.5f, 0f), 180f, 5f, 2.4f, 2.6f, ArchLib.Surface("corrugated_iron_02", 2f), ArchLib.Iron, true);
            A.Model("Furn_Sofa", G(80f, -128f, 0.12f), 180f);
            A.Model("Prop_Car_Pickup", G(72f, -125f, 0f), 75f);
            Poi(G(80f, -130f, 1f), "Bus stop", MapPoi.Kind.Landmark);
            // Back alleys: dumpsters and crates (the secretary's smoke behind the cantina).
            A.Dumpster(G(-71f, -138f, 0f), 90f);
            A.Dumpster(G(-60f, -168f, 0f), 0f);
            A.Dumpster(G(30f, -148.5f, 0f), 0f);
            A.CrateStack(G(-66f, -134f, 0f), 15f, 3);
            A.CrateStack(G(60f, -147.5f, 0f), -10f, 4);
            // Plank bridges between some roof terraces (the roofs are a second street).
            for (int i = 0; i + 1 < _houses.Count; i++)
            {
                ArchSpec a = _houses[i].spec, b = _houses[i + 1].spec;
                if (a.rooms.Exists(r => r.open) && b.rooms.Exists(r => r.open) && Vector3.Distance(a.origin, b.origin) < 16f && Mathf.Abs(a.yaw - b.yaw) < 1f)
                {
                    Vector3 pa = _houses[i].At(a.size.x - 0.4f, 2, a.size.y * 0.5f, 0f), pb = _houses[i + 1].At(0.4f, 2, b.size.y * 0.5f, 0f);
                    ArchMesh lPlank = new ArchMesh();
                    Vector3 lDir = pb - pa;
                    lPlank.Box(ArchLib.Surface("old_planks_02", 1.5f), (pa + pb) * 0.5f + Vector3.up * 1.05f, new Vector3(0.9f, 0.08f, lDir.magnitude + 0.6f), Quaternion.LookRotation(lDir));
                    GameObject lBridge = new GameObject("RoofPlank");
                    lBridge.transform.SetParent(K.Props, false);
                    lPlank.Flush(lBridge.transform, "SanLazaro_RoofPlank_" + i);
                }
            }
        }
    }
}
