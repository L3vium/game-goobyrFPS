using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapWeisshorn
    {
        /// <summary>A timber deck on posts (the hotel's terraces), with a railing on its open sides.</summary>
        private void Deck(Rect pRect, float pY, bool pRailS, bool pRailE, bool pRailW)
        {
            ArchMesh lMesh = new ArchMesh();
            ArchMat lDeck = ArchLib.Surface("wood_floor_deck", 2f), lPost = ArchLib.Surface("dark_wood", 1.5f);
            lMesh.BoxMinMax(lDeck, new Vector3(pRect.xMin, pY - 0.3f, pRect.yMin), new Vector3(pRect.xMax, pY, pRect.yMax));
            for (float x = pRect.xMin + 0.3f; x < pRect.xMax; x += 4f)
                for (float z = pRect.yMin + 0.3f; z < pRect.yMax; z += 4f)
                {
                    float g = K.GroundAt(x, z);
                    if (pY - 0.3f - g > 0.3f) lMesh.Box(lPost, new Vector3(x, (g + pY) * 0.5f - 0.2f, z), new Vector3(0.3f, pY - g, 0.3f));
                }
            void Rail(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a;
                lMesh.Box(lPost, (a + b) * 0.5f + Vector3.up * 1.0f, new Vector3(0.08f, 0.08f, d.magnitude), Quaternion.LookRotation(d));
                lMesh.Box(ArchLib.Invisible, (a + b) * 0.5f + Vector3.up * 0.5f, new Vector3(0.06f, 1f, d.magnitude), Quaternion.LookRotation(d));
                for (float t = 0f; t <= d.magnitude; t += 1.2f) lMesh.Box(lPost, a + d.normalized * t + Vector3.up * 0.5f, new Vector3(0.07f, 1f, 0.07f), false);
            }
            if (pRailS) Rail(new Vector3(pRect.xMin, pY, pRect.yMin + 0.05f), new Vector3(pRect.xMax, pY, pRect.yMin + 0.05f));
            if (pRailE) Rail(new Vector3(pRect.xMax - 0.05f, pY, pRect.yMin), new Vector3(pRect.xMax - 0.05f, pY, pRect.yMax));
            if (pRailW) Rail(new Vector3(pRect.xMin + 0.05f, pY, pRect.yMin), new Vector3(pRect.xMin + 0.05f, pY, pRect.yMax));
            if (Biome == FX.TimeOfDay.Biome.Snow) lMesh.Quad(ArchLib.Snow, new Vector3(pRect.xMin, pY + 0.03f, pRect.yMin), new Vector3(pRect.xMax, pY + 0.03f, pRect.yMin), new Vector3(pRect.xMax, pY + 0.03f, pRect.yMax), new Vector3(pRect.xMin, pY + 0.03f, pRect.yMax), false);
            GameObject lRoot = new GameObject("Deck");
            lRoot.transform.SetParent(K.Env, false);
            lMesh.Flush(lRoot.transform, "Weisshorn_Deck_" + (int)pRect.xMin + "_" + (int)pY);
        }

        /// <summary>A ride (funicular, chairlift, cable car): towers along a cable, a car riding it both ways.</summary>
        private void Ride(Vector3 pBottom, Vector3 pTop, string pName, bool pDown = true)
        {
            A.Zipline(pBottom, pTop);
            if (pDown) A.Zipline(pTop + (pTop - pBottom).normalized * -2f + Vector3.Cross(Vector3.up, (pTop - pBottom).normalized) * 2.5f, pBottom + Vector3.Cross(Vector3.up, (pTop - pBottom).normalized) * 2.5f);
            ArchMesh lMesh = new ArchMesh();
            int lTowers = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(pBottom, pTop) / 40f));
            for (int i = 1; i < lTowers; i++)
            {
                Vector3 p = Vector3.Lerp(pBottom, pTop, (float)i / lTowers);
                float g = K.GroundAt(p.x, p.z);
                lMesh.Box(ArchLib.Iron, new Vector3(p.x, (g + p.y + 4f) * 0.5f, p.z), new Vector3(0.6f, p.y + 4f - g, 0.6f));
                lMesh.Box(ArchLib.Iron, new Vector3(p.x, p.y + 4f, p.z), new Vector3(5.5f, 0.3f, 0.4f), Quaternion.LookRotation(pTop - pBottom) * Quaternion.Euler(0, 90, 0));
            }
            GameObject lRoot = new GameObject(pName);
            lRoot.transform.SetParent(K.Env, false);
            lMesh.Flush(lRoot.transform, "Weisshorn_" + pName);
            Poi(pBottom + Vector3.up, pName, MapPoi.Kind.Shortcut);
        }

        private void DressVillage()
        {
            foreach ((ArchSpec lSpec, int lZone) in _village)
            {
                ArchResult r = Build(lSpec, lZone);
                if (lSpec == _church) _churchR = r;
                else if (lSpec == _gasthaus) _gasthausR = r;
                else if (lSpec == _valley) _valleyR = r;
                else if (lSpec == _boathouse) _boathouseR = r;
                else if (lSpec == _rental) _rentalR = r;
                else _chalets.Add(r);
            }
            Poi(_churchR.At(7f, 5, 2.5f, 1f), "Church spire", MapPoi.Kind.Overlook);
            // The rink on the street, lamps, the bus stop, the gasthaus fondue and TV.
            ArchMesh lRink = new ArchMesh();
            lRink.BoxMinMax(ArchLib.PlainMat("RinkIce", new Color(0.82f, 0.9f, 0.96f), 0.92f, 0f, 3f), new Vector3(55f, VILLAGE_Y - 0.2f, -184f), new Vector3(85f, VILLAGE_Y + 0.08f, -168f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = i switch { 0 => new Vector3(55, 0, -184), 1 => new Vector3(85, 0, -184), 2 => new Vector3(85, 0, -168), _ => new Vector3(55, 0, -168) };
                Vector3 b = i switch { 0 => new Vector3(85, 0, -184), 1 => new Vector3(85, 0, -168), 2 => new Vector3(55, 0, -168), _ => new Vector3(55, 0, -184) };
                lRink.Box(ArchLib.Surface("dark_wood", 1.2f), (a + b) * 0.5f + Vector3.up * (VILLAGE_Y + 0.5f), new Vector3(0.12f, 1.0f, (b - a).magnitude), Quaternion.LookRotation(b - a));
            }
            GameObject lRinkRoot = new GameObject("Rink");
            lRinkRoot.transform.SetParent(K.Env, false);
            lRink.Flush(lRinkRoot.transform, "Weisshorn_Rink");
            MarkBuilt(Rect.MinMaxRect(54f, -185f, 86f, -167f));
            for (float x = -100f; x < 110f; x += 18f) A.Lamp(G(x, -149.9f + (((int)x / 18) % 2 == 0 ? 3.8f : -3.8f), 0f), 0f, new Color(1f, 0.75f, 0.45f), 15f, 2.2f);
            ArchOutdoor.Shed(A, G(100f, -143f, 0f), 180f, 6f, 2.4f, 2.6f, ArchLib.Surface("grey_roof_tiles", 2f), ArchLib.Iron, true);
            A.Distraction("tv", _gasthausR.InRoom("Stube", 0.9f, 0.9f, 0f), _gasthaus.yaw + 180f);
            A.Model("Furn_DiningTable", _gasthausR.InRoom("Stube", 0.5f, 0.5f, 0f), _gasthaus.yaw, _gasthausR.interior);
            A.Drinks(_gasthausR.InRoom("Stube", 0.5f, 0.5f, 0.78f), _gasthaus.yaw, ZONE_VILLAGE, 3, true, 1.2f);
            A.Distraction("caralarm", G(96f, -156f, 0f), 90f);
            A.Model("Prop_Boat", G(-150f, -170f, -0.2f), 180f);
            A.CrateStack(_boathouseR.At(2f, 0, 5f, 0f), 0f, 3);
            K.Water(new Vector3(-148f, -0.25f, -172f), new Vector2(4f, 4f));
            Poi(new Vector3(-148f, 0f, -172f), "Ice hole", MapPoi.Kind.Landmark);
            // The funicular from the valley station to the hotel's front terrace.
            Ride(_valleyR.At(6f, 0, 11f, 0f), new Vector3(-15f, PLATFORM_Y + 8.05f, 74f), "Funicular");
        }

        private void DressSki()
        {
            _midR = Build(_midStation, ZONE_SKI);
            _liftBottomR = Build(_liftBottom, ZONE_SKI);
            _liftTopR = Build(_liftTop, ZONE_SKI);
            _garageR = Build(_garage, ZONE_SKI);
            _mineHutR = Build(_mineHut, ZONE_SKI);
            Build(_skiSchool, ZONE_SKI);
            Build(_ticketHut, ZONE_SKI);
            _observatoryR = Build(_observatory, ZONE_SKI);
            _antennaR = Build(_antenna, ZONE_SKI);
            _cableTopR = Build(_cableTop, ZONE_SKI);
            // Chairlift: the bullwheel at the bottom (signature), the ride to the ballroom terrace.
            _bullwheel = _liftBottomR.At(5f, 0, -3f, 0f);
            ArchMesh lWheel = new ArchMesh();
            lWheel.Tube(ArchLib.Iron, _bullwheel + Vector3.up * 3.2f, _bullwheel + Vector3.up * 3.6f, 2.4f, 20);
            lWheel.Box(ArchLib.Iron, _bullwheel + Vector3.up * 1.6f, new Vector3(0.6f, 3.2f, 0.6f));
            GameObject lWheelRoot = new GameObject("Bullwheel") { layer = Core.GameLayers.INTERACTABLE };
            lWheelRoot.transform.SetParent(A.Systems, false);
            lWheelRoot.transform.position = _bullwheel;
            foreach (GameObject lPart in lWheel.Flush(lWheelRoot.transform, "Weisshorn_Bullwheel", Core.GameLayers.INTERACTABLE)) { lPart.isStatic = false; lPart.transform.position -= _bullwheel; }
            lWheelRoot.AddComponent<ExplosiveProp>();
            Poi(_bullwheel + Vector3.up * 3f, "Chairlift", MapPoi.Kind.Landmark);
            Ride(_bullwheel + new Vector3(0, 0.05f, 2f), _liftTopR.At(4.5f, 0, -2f, 0f), "Chairlift");
            K.Activity(ActivityType.Inspect, _bullwheel + new Vector3(1.5f, 0.05f, 0f), 270f, ZONE_SKI, 15f);
            // Groomer garage: the tiller (signature), the crowbar, generator, radio.
            _tiller = _garageR.At(8f, 0, 6f, 0f);
            ArchMesh lGroomer = new ArchMesh();
            lGroomer.Box(ArchLib.PlainMat("GroomerRed", new Color(0.7f, 0.1f, 0.08f), 0.5f, 0.2f), _tiller + new Vector3(0, 1.4f, 0), new Vector3(2.8f, 1.8f, 4.4f));
            lGroomer.Box(ArchLib.Iron, _tiller + new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1f, 5f));
            lGroomer.Tube(ArchLib.Galvanised, _tiller + new Vector3(-1.8f, 0.6f, -3.2f), _tiller + new Vector3(1.8f, 0.6f, -3.2f), 0.55f, 14);
            GameObject lGroomerRoot = new GameObject("GroomerTiller") { layer = Core.GameLayers.INTERACTABLE };
            lGroomerRoot.transform.SetParent(A.Systems, false);
            lGroomerRoot.transform.position = _tiller;
            foreach (GameObject lPart in lGroomer.Flush(lGroomerRoot.transform, "Weisshorn_Groomer", Core.GameLayers.INTERACTABLE)) { lPart.isStatic = false; lPart.transform.position -= _tiller; }
            lGroomerRoot.AddComponent<ExplosiveProp>();
            K.Activity(ActivityType.Inspect, _tiller + new Vector3(0, 0.05f, -4.6f), 0f, ZONE_SKI, 15f);
            A.Gadget(ItemKind.Crowbar, _garageR.At(14f, 0, 10f, 0.1f), "Garage crowbar");
            A.Distraction("generator", _garageR.At(1.5f, 0, 10.5f, 0f), _garage.yaw);
            A.Distraction("radio", _garageR.At(14.5f, 0, 2f, 0f), _garage.yaw);
            A.Dumpster(_garageR.At(-3f, 0, 4f, 0f), _garage.yaw + 90f);
            A.Explosive(_garageR.InRoom("GarageMine", 0.2f, 0.8f, 0f), 0f);
            A.CrateStack(_mineHutR.At(4.5f, 0, -2f, 0f), 0f, 3);
            // Ski-jump tower with its inrun, the lookout on top.
            Vector3 lJump = G(SKIJUMP, 0f);
            _skiJumpTop = lJump + Vector3.up * 24f;
            ArchMesh lTower = new ArchMesh();
            ArchMat lConcrete = ArchLib.Surface("concrete_wall_008", 3f);
            lTower.Box(lConcrete, lJump + new Vector3(0, 12f, 0), new Vector3(5f, 24f, 5f));
            lTower.Box(lConcrete, _skiJumpTop + new Vector3(0, 0.15f, 0), new Vector3(7f, 0.3f, 7f));
            for (int i = 0; i < 18; i++)
            {
                float t0 = i / 18f, t1 = (i + 1) / 18f;
                Vector3 a = _skiJumpTop + new Vector3(0, 0, -3f) + new Vector3(0, -t0 * t0 * 20f, -t0 * 55f), b = _skiJumpTop + new Vector3(0, 0, -3f) + new Vector3(0, -t1 * t1 * 20f, -t1 * 55f);
                lTower.Box(ArchLib.Snow, (a + b) * 0.5f, new Vector3(4f, 0.4f, Vector3.Distance(a, b) + 0.1f), Quaternion.LookRotation(b - a));
            }
            for (int k = 0; k < 4; k++) lTower.Box(ArchLib.Iron, _skiJumpTop + Quaternion.Euler(0, k * 90f, 0) * new Vector3(0, 1.05f, 3.4f), new Vector3(7f, 0.08f, 0.08f), Quaternion.Euler(0, k * 90f, 0));
            GameObject lTowerRoot = new GameObject("SkiJump");
            lTowerRoot.transform.SetParent(K.Env, false);
            lTower.Flush(lTowerRoot.transform, "Weisshorn_SkiJump");
            A.Climb(lJump + new Vector3(0, 0, 2.6f), 24.2f, Vector3.forward, true, "Ski-jump ladder");
            ArchMesh lJumpDress = new ArchMesh();
            ArchMat lRed = ArchLib.Tinted("painted_metal_shutter", new Color(0.85f, 0.12f, 0.1f), "hazard", 1f);
            for (int i = 0; i < 18; i += 2)
            {
                float t = i / 18f;
                Vector3 a = _skiJumpTop + new Vector3(0, 0, -3f) + new Vector3(0, -t * t * 20f, -t * 55f);
                foreach (float s in new[] { -2.2f, 2.2f })
                {
                    lJumpDress.Box(lRed, a + new Vector3(s, 0.8f, 0), new Vector3(0.12f, 1.4f, 0.12f), false);
                    lJumpDress.Box(i % 4 == 0 ? lRed : ArchLib.Snow, a + new Vector3(s, 1.3f, -1.5f), new Vector3(0.05f, 0.5f, 3f), false);
                }
            }
            for (int k = 0; k < 4; k++) lJumpDress.Box(lRed, _skiJumpTop + new Vector3(k < 2 ? -3.4f : 3.4f, 3f, k % 2 == 0 ? -3.4f : 3.4f), new Vector3(0.15f, 6f, 0.15f), false);
            GameObject lJumpRoot = new GameObject("SkiJumpDressing");
            lJumpRoot.transform.SetParent(K.Env, false);
            lJumpDress.Flush(lJumpRoot.transform, "Weisshorn_SkiJumpDress");
            for (int k = 0; k < 2; k++) A.Lamp(_skiJumpTop + new Vector3(k == 0 ? -3.4f : 3.4f, 6f, -3.4f), 0f, new Color(0.9f, 0.95f, 1f), 30f, 3f, false);
            Poi(_skiJumpTop + Vector3.up, "Ski-jump", MapPoi.Kind.Overlook);
            MarkBuilt(new Rect(SKIJUMP.x - 6f, SKIJUMP.y - 60f, 12f, 66f));
            // Mid-station cafe: drinks and the lift queue.
            A.Model("Furn_DiningTable", _midR.InRoom("MidStationRoom", 0.5f, 0.5f, 0f), _midStation.yaw, _midR.interior);
            A.Drinks(_midR.InRoom("MidStationRoom", 0.5f, 0.5f, 0.78f), _midStation.yaw, ZONE_SKI, 2, true, 1.2f);
            A.Distraction("radio", _midR.InRoom("MidStationRoom", 0.9f, 0.9f, 0f), _midStation.yaw);
            ArchOutdoor.Fence(A, LIFT_BOTTOM + new Vector2(-6, -10), LIFT_BOTTOM + new Vector2(-6, 6));
            ArchOutdoor.Fence(A, LIFT_BOTTOM + new Vector2(-2, -10), LIFT_BOTTOM + new Vector2(-2, 6));
            // Piste markers and pine-band fences.
            for (int i = 0; i < 10; i++) ArchOutdoor.Fence(A, new Vector2(-10f + i * 4f, -60f + i * 10f), new Vector2(-6f + i * 4f, -56f + i * 10f), 1.2f);
            PisteMarkers();
            // Observatory: the dome, the mast, the generator gas tanks, the cable car to the delivery yard.
            _domeTop = _observatoryR.At(9f, 1, 4.5f, 0f);
            ArchMesh lDome = new ArchMesh();
            lDome.Dome(ArchLib.PlainMat("ObservatoryDome", new Color(0.85f, 0.86f, 0.88f), 0.6f, 0.6f), _domeTop, 6f, 20, 7, Quaternion.identity);
            lDome.Cylinder(ArchLib.Iron, _domeTop, 6.2f, 0.4f, 20, true, false);
            GameObject lDomeRoot = new GameObject("Dome");
            lDomeRoot.transform.SetParent(K.Env, false);
            lDome.Flush(lDomeRoot.transform, "Weisshorn_Dome");
            Poi(_domeTop + Vector3.up * 4f, "Observatory dome", MapPoi.Kind.Landmark);
            ArchOutdoor.Mast(A, G(OBSERVATORY + new Vector2(-24f, 16f), 0f), 22f);
            A.Climb(G(OBSERVATORY + new Vector2(-24f, 15.2f), 0f), 22f, Vector3.back, true, "Mast ladder");
            for (int i = 0; i < 3; i++) A.Explosive(_observatoryR.At(-2f, 0, 10f + i * 0.7f, 0f), 0f, true);
            A.Distraction("generator", _observatoryR.At(-2.5f, 0, 6f, 0f), 90f);
            A.Distraction("radio", _observatoryR.InRoom("Bunker1", 0.2f, 0.8f, 0f), 0f);
            Ride(new Vector3(40f, PLATFORM_Y + 4.05f, 120f), _cableTopR.At(4f, 0, 2f, 0f), "Cable car");
        }

        /// <summary>Orange and blue piste poles every 14 m along both edges of the three runs.</summary>
        private void PisteMarkers()
        {
            ArchMesh lMesh = new ArchMesh();
            ArchMat lOrange = ArchLib.Tinted("painted_metal_shutter", new Color(1f, 0.45f, 0.05f), "piste_o", 1f), lBlue = ArchLib.Tinted("painted_metal_shutter", new Color(0.1f, 0.3f, 0.9f), "piste_b", 1f);
            for (float z = -95f; z < 68f; z += 14f)
                foreach ((float centre, float half, ArchMat mat) in new[] { (20f - z * 0.15f, 20f, lBlue), (-45f + z * 0.1f, 14f, lOrange), (75f, 10f, lBlue) })
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float x = centre + s * half;
                        if (Built(x, z)) continue;
                        float g = K.GroundAt(x, z);
                        lMesh.Cylinder(mat, new Vector3(x, g - 0.3f, z), 0.035f, 2.0f, 6, false);
                        lMesh.Cylinder(ArchLib.Snow, new Vector3(x, g + 1.3f, z), 0.04f, 0.25f, 6, false);
                    }
            GameObject lRoot = new GameObject("PisteMarkers");
            lRoot.transform.SetParent(K.Env, false);
            lMesh.Flush(lRoot.transform, "Weisshorn_PisteMarkers");
        }

        private void DressHotel()
        {
            _hotelR = Build(_hotel, ZONE_HOTEL);
            Poi(Hp(24f, 9, 16f, 2f), "Clock tower", MapPoi.Kind.Landmark);
            Poi(Hp(35f, 2, 15f, 1f), "Grand Hotel Weisshorn", MapPoi.Kind.Landmark);
            // Terraces: ski-room terrace (L1) under the front terrace (L2), the ballroom terrace, stairs between.
            Deck(Rect.MinMaxRect(HOTEL.x, HOTEL.z - 5f, HOTEL.x + 30f, HOTEL.z), PLATFORM_Y + 4f, false, true, true);
            Deck(Rect.MinMaxRect(HOTEL.x, HOTEL.z - 9f, HOTEL.x + 70f, HOTEL.z), PLATFORM_Y + 8f, false, true, true);
            ArchOutdoor.Stairs(A, new Vector3(HOTEL.x + 27f, PLATFORM_Y, HOTEL.z - 14f), new Vector3(HOTEL.x + 27f, PLATFORM_Y + 4f, HOTEL.z - 4.8f), 2f, ArchLib.Surface("stone_wall", 2f), false);
            ArchOutdoor.Stairs(A, new Vector3(HOTEL.x + 60f, PLATFORM_Y, HOTEL.z - 24f), new Vector3(HOTEL.x + 60f, PLATFORM_Y + 8f, HOTEL.z - 9.3f), 2.4f, ArchLib.Surface("stone_wall", 2f));
            Poi(new Vector3(HOTEL.x + 20f, PLATFORM_Y + 9f, HOTEL.z - 5f), "Front terrace", MapPoi.Kind.Entrance);
            // Spa garden wall (G4 mantle), the greenhouse with aconite, the drop ladder from the roof walk.
            ArchOutdoor.Wall(A, new[] { new Vector2(-62f, 80f), new Vector2(-62f, 112f), new Vector2(-36f, 112f) }, false, 2.2f, 0.5f, ArchLib.Surface("stone_wall", 2.5f), ArchLib.Surface("stone_wall", 2.5f));
            ArchOutdoor.Shed(A, G(-52f, 104f, 0f), 0f, 8f, 5f, 2.6f, ArchLib.Glass, ArchLib.Iron);
            A.Gadget(ItemKind.Poison, G(-52f, 104f, 0.3f), "Aconite");
            A.DropLadder(Hp(-0.6f, 5, 4f, 1.2f), new Vector3(HOTEL.x - 0.6f, PLATFORM_Y + 8f, HOTEL.z + 4f), Vector3.left, "Spa-garden drop ladder");
            for (int i = 0; i < 3; i++) A.Lamp(G(-55f + i * 8f, 86f, 0f), 0f, new Color(1f, 0.75f, 0.45f), 12f, 2f);
            // Delivery yard: barrier gate (Padlock), trucks, dumpster, camera C2.
            ArchOutdoor.Truck(A, G(18f, 119f, 0f), 90f, new Color(0.15f, 0.3f, 0.55f), "BeverageTruck");
            ArchOutdoor.Truck(A, G(31f, 118.5f, 0f), 75f, new Color(0.75f, 0.75f, 0.72f), "LaundryVan");
            A.Dumpster(G(8f, 120f, 0f), 180f);
            ArchOutdoor.Bins(A, G(2f, 122.6f, 0f), 180f, 4);
            A.CrateStack(G(-2f, 120f, 0f), 0f, 4);
            A.LaundryCart(G(26f, 114.2f, 0f), 0f);
            A.LaundryCart(G(23.6f, 114.2f, 0f), 0f);
            A.Model("Prop_Pallet", G(12f, 114f, 0f), 10f);
            ArchOutdoor.LockedGate(A, G(48f, 117f, 0f), 0f, 4f, 2.2f, SecureDoor.Lock.Padlock, true);
            ArchOutdoor.GuardBooth(A, G(44.5f, 113.5f, 0f), 90f, true, 3.4f);
            ArchOutdoor.Sign(A, G(46f, 120.6f, 0f), 270f, "Privé - Personnel");
            ArchOutdoor.Sign(A, G(6f, 113f, 0f), 180f, "Nur Personal");
            ArchOutdoor.Fence(A, new Vector2(-7f, 124.5f), new Vector2(44f, 124.5f), 1.6f);
            ArchOutdoor.Fence(A, new Vector2(50f, 124f), new Vector2(50f, 108f), 1.6f);
            Poi(G(48f, 117f, 1f), "Service-road barrier", MapPoi.Kind.Entrance);
            // The funicular top station on the front terrace: a canopy, the frisk booth, a rope queue.
            Vector3 lFunicular = new Vector3(-15f, PLATFORM_Y + 8.05f, 74f);
            ArchOutdoor.Shed(A, lFunicular + new Vector3(0f, -0.05f, 0f), 0f, 6f, 5f, 3.2f, ArchLib.Surface("grey_roof_tiles", 2f), ArchLib.Surface("dark_wood", 1.5f), false);
            ArchOutdoor.GuardBooth(A, lFunicular + new Vector3(5.5f, -0.05f, 4f), 180f, false);
            ArchOutdoor.RopeLine(A, lFunicular + new Vector3(-1.5f, -0.05f, 3f), lFunicular + new Vector3(-1.5f, -0.05f, 7f), lFunicular + new Vector3(1.5f, -0.05f, 7f), lFunicular + new Vector3(1.5f, -0.05f, 3f));
            ArchOutdoor.Sign(A, lFunicular + new Vector3(3f, -0.05f, 7.6f), 0f, "Gäste - Contrôle", true, new Color(0.15f, 0.25f, 0.45f));
            // The terrace: banquet tables, patio heaters, planters along the rail.
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(HOTEL.x + 6f + i * 9.5f, PLATFORM_Y + 8.02f, HOTEL.z - 5f);
                if (Mathf.Abs(p.x - lFunicular.x) < 6f) continue;
                A.RoundTable(p, A.Props, ZONE_HOTEL);
                ArchOutdoor.PatioHeater(A, p + new Vector3(2.4f, 0f, 1.2f));
            }
            for (int i = 0; i < 8; i++) ArchOutdoor.Planter(A, new Vector3(HOTEL.x + 4f + i * 8.5f, PLATFORM_Y + 8.02f, HOTEL.z - 8.4f), 0f);
            A.Camera(Hp(30f, 1, 30.3f, 3.4f), G(20f, 118f, 1f), ZONE_HOTEL);
            A.Climb(new Vector3(-20f, K.GroundAt(-20f, 131f), 131f), PLATFORM_Y + 20f - K.GroundAt(-20f, 131f), Vector3.back, false, "Crevasse ice wall");

            // ---- hotel systems
            A.Camera(Hp(39.4f, 2, 0.6f, 3.6f), Hp(20f, 2, 8f, 1f), ZONE_HOTEL);
            A.Camera(Hp(39.6f, 3, 19.6f, 3.4f), Hp(30f, 3, 18f, 0f), ZONE_HOTEL);
            A.Camera(Hp(39.6f, 4, 9f, 3.4f), Hp(20f, 4, 18f, 0f), ZONE_HOTEL);
            A.Camera(Hp(27.6f, 1, 29.6f, 3.4f), Hp(20f, 1, 20f, 0f), ZONE_HOTEL);
            A.Camera(Hp(69.6f, 4, 29.6f, 2.4f), Hp(55f, 4, 15f, 0f), ZONE_HOTEL);
            A.Recorder(Hp(15f, 1, 29f, 0f), 180f, ZONE_HOTEL);
            List<Transform> lVents = new List<Transform>();
            foreach (VentCover lCover in _hotelR.systems.GetComponentsInChildren<VentCover>()) lVents.Add(lCover.transform);
            A.AirHandler(Hp(20f, 0, 28f, 0f), 180f, _hotelR.volume, lVents.ToArray());
            A.Breaker(Hp(27.6f, 0, 24f, 0f), -90f, _hotelR.volume);
            // The chandelier over the ballroom stage (the shared trick), the band stage, champagne.
            A.Chandelier(Hp(55f, 2, 15f, 7.6f), 6.8f);
            A.Chandelier(Hp(46f, 2, 8f, 7.6f), 6.8f);
            A.Chandelier(Hp(64f, 2, 8f, 7.6f), 6.8f);
            ArchOutdoor.Stage(A, Hp(55f, 2, 26.2f, 0f), 0f, 11f, 5f);
            int lTable = 0;
            for (float x = 44f; x < 68f; x += 4f)
                for (float z = 3f; z < 21f; z += 4f)
                {
                    if (Mathf.Abs(x - 55f) < 5f && z > 9f && z < 19f) continue;
                    Vector3 p = Hp(x, 2, z, 0f);
                    A.RoundTable(p, _hotelR.interior, ZONE_HOTEL);
                    if (lTable++ % 5 == 1) A.Drinks(p + new Vector3(0.25f, 0.79f, 0.3f), 0f, ZONE_HOTEL, 2, true, 1.3f);
                }
            A.Model("Furn_PianoGrand", Hp(45.5f, 2, 25f, 0f), 120f);
            A.Model("Furn_BarCounter", Hp(68.6f, 2, 6f, 0f), -90f, _hotelR.interior);
            // Spa: pool, sauna stove (explosive), hot tub heater, ipecac at the doctor's.
            K.Water(Hp(7f, 2, 21f, -0.3f), new Vector2(8f, 5f));
            A.Explosive(Hp(1.2f, 2, 29f, 0f), 0f, true);
            A.Explosive(Hp(12f, 2, 17f, 0f), 0f, true);
            A.Distraction("radio", Hp(12f, 2, 29f, 0f), 180f);
            A.Gadget(ItemKind.EmeticPoison, Hp(33f, 3, 4f, 0.8f), "Ipecac (doctor's bag)");
            A.Gadget(ItemKind.CodeNote, Hp(3f, 1, 26f, 0.9f), "Butler's code card");
            A.Gadget(ItemKind.Keycard, Hp(20f, 1, 22f, 0.85f), "Operator's spare keycard");
            A.Distraction("phone", Hp(28f, 4, 4f, 0.8f), 0f);
            A.Distraction("tv", Hp(36f, 4, 6f, 0f), 180f);
            A.Distraction("phone", Hp(32f, 2, 4f, 0.8f), 0f);
            A.Explosive(Hp(20f, 2, 1f, 0f), 0f, true);
            A.LaundryCart(Hp(3f, 1, 28f, 0f), 180f);
            A.LaundryCart(Hp(5f, 0, 3f, 0f), 0f);
            A.Model("Prop_Helicopter", Hp(58f, 4, 14f, 0f), 30f);
            A.Explosive(Hp(-2f, 2, 6f, 0f), 0f, true);
        }

        private void Tunnels()
        {
            // Mine gallery: groomer garage cellar -> the hotel's cold store (rusted door, Crowbar).
            Vector3 lMine = _garageR.At(-0.6f, -1, 6f, 0f);
            Vector3 lCold = Hp(35f, 0, 30.6f, 0f);
            Cave("MineGallery", 3.6f, 3.0f, lMine, lMine + new Vector3(-8f, 0, 4f), new Vector3(60f, 18f, 40f), new Vector3(30f, 32f, 90f), new Vector3(20f, 40f, 125f), new Vector3(lCold.x, lCold.y, lCold.z + 6f), lCold);
            A.Explosive(new Vector3(60f, 18f, 40f) + new Vector3(1.5f, 0f, 0f), 0f);
            Poi(new Vector3(60f, 19f, 40f), "Mine gallery", MapPoi.Kind.Underground);
            // Bunker escape tunnel: command level -> under the ski-jump.
            Vector3 lBunker = _observatoryR.At(-0.6f, -2, 11f, 0f);
            Cave("EscapeTunnel", 2.8f, 2.6f, lBunker, lBunker + new Vector3(-10f, 0f, -4f), new Vector3(-40f, 45f, 120f), new Vector3(-80f, 30f, 60f), new Vector3(-92f, K.GroundAt(-92f, 26f) - 0.1f, 26f));
            Poi(G(-92f, 26f, 1f), "Escape tunnel", MapPoi.Kind.Underground);
            // Lake culvert: Gasthaus cellar -> the lake shore.
            Vector3 lCellar = _gasthausR.At(20.6f, -1, 7f, 0f);
            Cave("LakeCulvert", 2.4f, 2.2f, lCellar, lCellar + new Vector3(0, 0, -6f), new Vector3(-80f, -4f, -175f), new Vector3(-112f, -0.3f, -170f));
            Poi(new Vector3(-112f, 0.5f, -170f), "Lake culvert", MapPoi.Kind.Underground);
        }
    }
}
