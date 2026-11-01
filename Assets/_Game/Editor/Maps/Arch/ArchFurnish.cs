using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Furnishes the rooms of a generated building from the <c>Furn_*</c> models by room type: wall
    /// pieces backed against the walls (never in front of a door, tall ones never in front of a
    /// window, nothing on a stair), a centre group (a sofa set on a rug, a dining table and chairs, a
    /// desk), the systemic props a room implies (drinks, a toilet, a wardrobe to hide in, a TV or a
    /// phone to distract) and a throwable lying somewhere. Every room gets a purpose.
    /// </summary>
    public static class ArchFurnish
    {
        private class Item
        {
            public string model;
            public bool tall;
            public System.Action<ArchKit, Vector3, float> system;
            public Item(string pModel, bool pTall = false, System.Action<ArchKit, Vector3, float> pSystem = null) { model = pModel; tall = pTall; system = pSystem; }
        }

        private static Item I(string pModel, bool pTall = false) => new Item(pModel, pTall);

        private static Item[] WallItems(RoomType t)
        {
            switch (t)
            {
                case RoomType.Salon: case RoomType.Living: return new[] { I("Furn_Bookshelf", true), I("Furn_Cabinet", true), I("Furn_PottedPlant"), I("Furn_FloorLamp"), I("Furn_Sofa"), I("Furn_PianoGrand"), I("Furn_PottedPlant"), I("Furn_Armchair") };
                case RoomType.Dining: return new[] { I("Furn_Cabinet", true), I("Furn_PottedPlant"), I("Furn_Cabinet", true), I("Furn_PottedPlant") };
                case RoomType.Kitchen: return new[] { I("Furn_KitchenCounter"), I("Furn_Stove"), I("Furn_KitchenCounter"), I("Furn_Fridge", true), I("Furn_Sink"), I("Furn_Shelves_Metal", true) };
                case RoomType.Pantry: case RoomType.Store: return new[] { I("Furn_Shelves_Metal", true), I("Furn_Shelves_Metal", true), I("Prop_Crate_Wood"), I("Furn_Shelves_Metal", true), I("Prop_Pallet") };
                case RoomType.Office: return new[] { I("Furn_Bookshelf", true), I("Furn_Cabinet", true), I("Furn_Shelves_Metal", true), I("Furn_PottedPlant"), I("Furn_Cabinet", true), I("Furn_Armchair") };
                case RoomType.Study: case RoomType.Library: return new[] { I("Furn_Bookshelf", true), I("Furn_Bookshelf", true), I("Furn_Armchair"), I("Furn_FloorLamp"), I("Furn_Cabinet", true) };
                case RoomType.Bedroom: case RoomType.Suite: return new[] { I("Furn_Bed"), I("Furn_Cabinet", true), I("Furn_FloorLamp"), I("Furn_Armchair"), I("Furn_Desk"), I("Furn_PottedPlant"), I("Furn_Sofa") };
                case RoomType.Bath: return new[] { I("Furn_Sink"), I("Furn_Bathtub") };
                case RoomType.Corridor: case RoomType.Gallery: case RoomType.Loggia: return new[] { I("Furn_PottedPlant"), I("Furn_Armchair"), I("Furn_Cabinet", true), I("Furn_PottedPlant"), I("Furn_Sofa") };
                case RoomType.Hall: case RoomType.Lobby: return new[] { I("Furn_PottedPlant"), I("Furn_Cabinet", true), I("Furn_Armchair"), I("Furn_Sofa"), I("Furn_PottedPlant"), I("Furn_FloorLamp") };
                case RoomType.Cellar: case RoomType.ColdRoom: case RoomType.CoalStore: return new[] { I("Furn_Shelves_Metal", true), I("Prop_Crate_Wood"), I("Prop_Pallet"), I("Prop_Crate_Wood") };
                case RoomType.WineCellar: return new[] { I("Furn_Shelves_Metal", true), I("Furn_Shelves_Metal", true), I("Furn_Shelves_Metal", true), I("Prop_Crate_Wood") };
                case RoomType.Laundry: return new[] { I("Furn_Shelves_Metal", true), I("Furn_Sink"), I("Furn_Shelves_Metal", true) };
                case RoomType.Security: return new[] { I("Furn_Locker", true), I("Furn_Locker", true), I("Furn_Cabinet", true) };
                case RoomType.Vault: case RoomType.Armoury: return new[] { I("Furn_Shelves_Metal", true), I("Prop_Crate_Military"), I("Furn_Shelves_Metal", true), I("Prop_Crate_Military") };
                case RoomType.GuardRoom: case RoomType.Barracks: case RoomType.Dormitory: return new[] { I("Furn_Bed"), I("Furn_Locker", true), I("Furn_Bed"), I("Furn_Locker", true), I("Furn_Bed") };
                case RoomType.Linen: return new[] { I("Furn_Shelves_Metal", true), I("Furn_Shelves_Metal", true), I("Furn_Cabinet", true) };
                case RoomType.Archive: return new[] { I("Furn_Bookshelf", true), I("Furn_Bookshelf", true), I("Furn_Shelves_Metal", true), I("Furn_Bookshelf", true) };
                case RoomType.Garage: case RoomType.Workshop: case RoomType.Hangar: return new[] { I("Furn_Shelves_Metal", true), I("Prop_Crate_Military"), I("Prop_Generator"), I("Prop_Jerrycan"), I("Furn_Shelves_Metal", true), I("Prop_Pallet") };
                case RoomType.Bar: return new[] { I("Furn_BarCounter"), I("Furn_Shelves_Metal", true), I("Furn_PottedPlant") };
                case RoomType.Shop: return new[] { I("Furn_Shelves_Metal", true), I("Furn_KitchenCounter"), I("Furn_Shelves_Metal", true), I("Prop_Crate_Wood") };
                case RoomType.Police: return new[] { I("Furn_Cabinet", true), I("Furn_Locker", true), I("Furn_Bookshelf", true) };
                case RoomType.Radio: return new[] { I("Furn_ServerRack", true), I("Furn_Cabinet", true) };
                case RoomType.Mess: return new[] { I("Furn_KitchenCounter"), I("Furn_Fridge", true), I("Furn_Stove") };
                case RoomType.Attic: return new[] { I("Prop_Crate_Wood"), I("Furn_Cabinet", true), I("Prop_Pallet"), I("Prop_Crate_Wood") };
                case RoomType.Sacristy: return new[] { I("Furn_Cabinet", true), I("Furn_Bookshelf", true) };
                case RoomType.Spa: case RoomType.Gym: return new[] { I("Furn_PottedPlant"), I("Furn_Locker", true), I("Furn_Sofa") };
                case RoomType.Clinic: return new[] { I("Furn_Bed"), I("Furn_Cabinet", true), I("Furn_Sink") };
                case RoomType.Plant: return new[] { I("Furn_Shelves_Metal", true), I("Prop_Generator") };
                case RoomType.Classroom: return new[] { I("Furn_Bookshelf", true), I("Furn_Desk") };
                case RoomType.Ballroom: return new[] { I("Furn_PianoGrand"), I("Furn_PottedPlant"), I("Furn_BarCounter"), I("Furn_PottedPlant"), I("Furn_Sofa"), I("Furn_FloorLamp") };
                default: return new Item[0];
            }
        }

        /// <summary>Furnishes every room of a building (with the systemic extras), zone for the activities.</summary>
        public static void Furnish(ArchKit pKit, ArchResult pBuilding, int pZone)
        {
            if (!pBuilding.spec.furnish || pBuilding.spec.shut) return;
            System.Random lRandom = new System.Random(pBuilding.spec.name.GetHashCode());
            foreach (ArchRoom lRoom in pBuilding.spec.rooms)
                if (!lRoom.noFurniture && !lRoom.open) Room(pKit, pBuilding, lRoom, pZone, lRandom);
        }

        private static bool Free(ArchResult b, ArchRoom pRoom, Rect pFoot, bool pTall)
        {
            Rect lInner = new Rect(pRoom.rect.xMin + 0.15f, pRoom.rect.yMin + 0.15f, pRoom.rect.width - 0.3f, pRoom.rect.height - 0.3f);
            if (pFoot.xMin < lInner.xMin || pFoot.yMin < lInner.yMin || pFoot.xMax > lInner.xMax || pFoot.yMax > lInner.yMax) return false;
            foreach ((int f, Rect r, bool lDoor) in b.keepClear) if (f == pRoom.floor && (lDoor || pTall) && r.Overlaps(pFoot)) return false;
            foreach ((int f, Rect r) in b.used) if (f == pRoom.floor && r.Overlaps(pFoot)) return false;
            foreach (ArchStair s in b.spec.stairs)
                if ((s.fromFloor == pRoom.floor || s.fromFloor + 1 == pRoom.floor) && Grow(s.rect, 0.9f).Overlaps(pFoot)) return false;
            foreach ((int f, Rect r) in b.spec.holes) if (f == pRoom.floor && Grow(r, 0.4f).Overlaps(pFoot)) return false;
            foreach (ArchDuct d in b.spec.ducts)
                foreach (Vector3 n in d.nodes)
                    if (Mathf.Abs(n.y - pRoom.floor * b.spec.storey) < 2.6f && Grow(new Rect(n.x - 0.6f, n.z - 0.6f, 1.2f, 1.2f), 0.3f).Overlaps(pFoot)) return false;
            return true;
        }

        private static Rect Grow(Rect r, float d) => new Rect(r.xMin - d, r.yMin - d, r.width + d * 2f, r.height + d * 2f);

        /// <summary>Local footprint of a model placed with its back against a wall side (0 S, 1 E, 2 N, 3 W) at a position along it.</summary>
        private static Rect Foot(Rect pRoom, int pSide, float pAlong, Vector2 pSize, out Vector2 pCenter, out float pYaw)
        {
            float w = pSize.x, d = pSize.y, inset = 0.12f;
            switch (pSide)
            {
                case 0: pCenter = new Vector2(pAlong, pRoom.yMin + inset + d * 0.5f); pYaw = 0f; return new Rect(pAlong - w * 0.5f, pRoom.yMin + inset, w, d);
                case 1: pCenter = new Vector2(pRoom.xMax - inset - d * 0.5f, pAlong); pYaw = -90f; return new Rect(pRoom.xMax - inset - d, pAlong - w * 0.5f, d, w);
                case 2: pCenter = new Vector2(pAlong, pRoom.yMax - inset - d * 0.5f); pYaw = 180f; return new Rect(pAlong - w * 0.5f, pRoom.yMax - inset - d, w, d);
                default: pCenter = new Vector2(pRoom.xMin + inset + d * 0.5f, pAlong); pYaw = 90f; return new Rect(pRoom.xMin + inset, pAlong - w * 0.5f, d, w);
            }
        }

        /// <summary>Places a model against the first free wall spot (sides tried in a shuffled order). Returns the local centre or null.</summary>
        public static Vector2? AgainstWall(ArchKit pKit, ArchResult b, ArchRoom pRoom, string pModel, bool pTall, System.Random pRandom, System.Action<ArchKit, Vector3, float> pSystem = null)
        {
            Bounds lBounds = ArchLib.ModelBounds(pModel);
            Vector2 lSize = new Vector2(lBounds.size.x + 0.1f, lBounds.size.z + 0.05f);
            int lStart = pRandom.Next(4);
            Rect lRoomRect = new Rect(pRoom.rect.xMin + 0.25f, pRoom.rect.yMin + 0.25f, pRoom.rect.width - 0.5f, pRoom.rect.height - 0.5f);
            for (int k = 0; k < 4; k++)
            {
                int lSide = (lStart + k) % 4;
                float lFrom = lSide % 2 == 0 ? lRoomRect.xMin : lRoomRect.yMin, lTo = lSide % 2 == 0 ? lRoomRect.xMax : lRoomRect.yMax;
                float lOffset = (float)pRandom.NextDouble() * 0.6f;
                for (float a = lFrom + lSize.x * 0.5f + 0.1f + lOffset; a < lTo - lSize.x * 0.5f; a += 0.35f)
                {
                    Rect lFoot = Foot(lRoomRect, lSide, a, lSize, out Vector2 lCenter, out float lYaw);
                    if (!Free(b, pRoom, lFoot, pTall)) continue;
                    b.used.Add((pRoom.floor, lFoot));
                    Vector3 lWorld = b.At(lCenter.x, pRoom.floor, lCenter.y, 0f) - b.Rotation * (Quaternion.Euler(0, lYaw, 0) * new Vector3(lBounds.center.x, 0, lBounds.center.z));
                    if (pSystem != null) pSystem(pKit, lWorld, b.Yaw(lYaw));
                    else pKit.Model(pModel, lWorld, b.Yaw(lYaw), b.interior);
                    return lCenter;
                }
            }
            return null;
        }

        /// <summary>Places a model free-standing at a local point (yaw local) if the spot is free.</summary>
        public static bool At(ArchKit pKit, ArchResult b, ArchRoom pRoom, string pModel, Vector2 pLocal, float pYaw, bool pReserve = true)
        {
            Bounds lBounds = ArchLib.ModelBounds(pModel);
            bool lQuarter = Mathf.Abs(Mathf.DeltaAngle(pYaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(pYaw, -90f)) < 1f;
            Vector2 lSize = lQuarter ? new Vector2(lBounds.size.z, lBounds.size.x) : new Vector2(lBounds.size.x, lBounds.size.z);
            Rect lFoot = new Rect(pLocal.x - lSize.x * 0.5f, pLocal.y - lSize.y * 0.5f, lSize.x, lSize.y);
            if (pReserve && !Free(b, pRoom, lFoot, false)) return false;
            if (pReserve) b.used.Add((pRoom.floor, lFoot));
            pKit.Model(pModel, b.At(pLocal.x, pRoom.floor, pLocal.y, 0f), b.Yaw(pYaw), b.interior, pModel == "Furn_Rug" ? ArchLib.Collide.None : ArchLib.Collide.Box);
            return true;
        }

        private static void Room(ArchKit pKit, ArchResult b, ArchRoom pRoom, int pZone, System.Random r)
        {
            Vector2 c = pRoom.rect.center;
            float W = pRoom.rect.width, D = pRoom.rect.height;
            bool lWide = W >= D;
            switch (pRoom.type)
            {
                case RoomType.Salon: case RoomType.Living:
                    if (W > 4.5f && D > 4.5f)
                    {
                        At(pKit, b, pRoom, "Furn_Rug", c, lWide ? 0f : 90f, false);
                        At(pKit, b, pRoom, "Furn_CoffeeTable", c, lWide ? 0f : 90f);
                        At(pKit, b, pRoom, "Furn_Sofa", c + (lWide ? new Vector2(0, -1.3f) : new Vector2(-1.3f, 0)), lWide ? 0f : 90f);
                        At(pKit, b, pRoom, "Furn_Armchair", c + (lWide ? new Vector2(-1.6f, 0.6f) : new Vector2(0.6f, -1.6f)), lWide ? 120f : 210f);
                        At(pKit, b, pRoom, "Furn_Armchair", c + (lWide ? new Vector2(1.6f, 0.6f) : new Vector2(0.6f, 1.6f)), lWide ? 240f : -30f);
                        pKit.Drinks(b.At(c.x, pRoom.floor, c.y, 0.4f), b.Yaw(0f), pZone, 2, true, 1.2f);
                    }
                    break;
                case RoomType.Dining: case RoomType.Mess:
                {
                    int lTables = pRoom.type == RoomType.Mess && Mathf.Max(W, D) > 9f ? 2 : 1;
                    for (int t = 0; t < lTables; t++)
                    {
                        Vector2 p = lTables == 1 ? c : c + (lWide ? new Vector2((t - 0.5f) * W * 0.45f, 0) : new Vector2(0, (t - 0.5f) * D * 0.45f));
                        if (!At(pKit, b, pRoom, "Furn_DiningTable", p, lWide ? 0f : 90f)) continue;
                        for (int k = 0; k < 6; k++)
                        {
                            float lAlong = (k % 3 - 1) * 0.7f, lSide = k < 3 ? -0.85f : 0.85f;
                            Vector2 lChair = p + (lWide ? new Vector2(lAlong, lSide) : new Vector2(lSide, lAlong));
                            float lYaw = lWide ? (k < 3 ? 0f : 180f) : (k < 3 ? 90f : -90f);
                            At(pKit, b, pRoom, "Furn_DiningChair", lChair, lYaw, false);
                        }
                        pKit.Drinks(b.At(p.x, pRoom.floor, p.y, 0.77f), b.Yaw(lWide ? 0f : 90f), pZone, 3, true, 1.3f);
                    }
                    break;
                }
                case RoomType.Kitchen:
                    // A central island: two counters back to back, the prep table beyond.
                    if (W > 5f && D > 5f)
                    {
                        Vector2 lOff = lWide ? new Vector2(0, 0.32f) : new Vector2(0.32f, 0);
                        At(pKit, b, pRoom, "Furn_KitchenCounter", c - lOff, lWide ? 180f : -90f);
                        At(pKit, b, pRoom, "Furn_KitchenCounter", c + lOff, lWide ? 0f : 90f);
                        At(pKit, b, pRoom, "Furn_KitchenCounter", c - lOff + (lWide ? new Vector2(1.3f, 0) : new Vector2(0, 1.3f)), lWide ? 180f : -90f);
                        At(pKit, b, pRoom, "Furn_KitchenCounter", c + lOff + (lWide ? new Vector2(1.3f, 0) : new Vector2(0, 1.3f)), lWide ? 0f : 90f);
                        pKit.Model("Furn_Stove", b.At(c.x - (lWide ? 1.4f : 0f), pRoom.floor, c.y - (lWide ? 0f : 1.4f), 0f), b.Yaw(lWide ? 0f : 90f), b.interior);
                    }
                    else if (W > 4f && D > 4f) At(pKit, b, pRoom, "Furn_DiningTable", c, lWide ? 0f : 90f);
                    break;
                case RoomType.Hall: case RoomType.Lobby:
                    // A fireplace on a wall, a sofa group on a rug before it, a reception desk in a lobby.
                    Vector2? lFire = AgainstWall(pKit, b, pRoom, "Furn_Cabinet", true, r, (k, p, y) => k.Fireplace(p, y, b.interior));
                    if (W > 6f && D > 6f)
                    {
                        Vector2 g = c + (lWide ? new Vector2(W * 0.18f, 0) : new Vector2(0, D * 0.18f));
                        At(pKit, b, pRoom, "Furn_Rug", g, lWide ? 0f : 90f, false);
                        At(pKit, b, pRoom, "Furn_CoffeeTable", g, lWide ? 0f : 90f);
                        At(pKit, b, pRoom, "Furn_Sofa", g + (lWide ? new Vector2(0, -1.4f) : new Vector2(-1.4f, 0)), lWide ? 0f : 90f);
                        At(pKit, b, pRoom, "Furn_Sofa", g + (lWide ? new Vector2(0, 1.4f) : new Vector2(1.4f, 0)), lWide ? 180f : -90f);
                        At(pKit, b, pRoom, "Furn_Armchair", g + (lWide ? new Vector2(1.7f, 0) : new Vector2(0, 1.7f)), lWide ? -90f : 180f);
                        pKit.Drinks(b.At(g.x, pRoom.floor, g.y, 0.42f), b.Yaw(0f), pZone, 2, true, 1.3f);
                    }
                    if (pRoom.type == RoomType.Lobby || W * D > 120f)
                    {
                        Vector2 lDesk = c - (lWide ? new Vector2(W * 0.25f, 0) : new Vector2(0, D * 0.25f));
                        if (At(pKit, b, pRoom, "Furn_BarCounter", lDesk, lWide ? 90f : 0f))
                        {
                            pKit.Distraction("phone", b.At(lDesk.x, pRoom.floor, lDesk.y, 1.08f), b.Yaw(0f));
                            pKit.Model("Furn_FloorLamp", b.At(lDesk.x + (lWide ? 0f : 1.4f), pRoom.floor, lDesk.y + (lWide ? 1.4f : 0f), 0f), b.Yaw(0f), b.interior);
                        }
                    }
                    break;
                case RoomType.Archive: case RoomType.Library:
                    // Aisles of shelving across the room.
                    for (float u = (lWide ? pRoom.rect.xMin : pRoom.rect.yMin) + 2.2f; u < (lWide ? pRoom.rect.xMax : pRoom.rect.yMax) - 1.6f; u += 1.9f)
                        for (float v = (lWide ? pRoom.rect.yMin : pRoom.rect.xMin) + 1.9f; v < (lWide ? pRoom.rect.yMax : pRoom.rect.xMax) - 1.5f; v += 1.3f)
                            At(pKit, b, pRoom, pRoom.type == RoomType.Library ? "Furn_Bookshelf" : "Furn_Shelves_Metal", lWide ? new Vector2(u, v) : new Vector2(v, u), lWide ? 90f : 0f);
                    break;
                case RoomType.Armoury: case RoomType.Vault:
                    AgainstWall(pKit, b, pRoom, "Furn_Bookshelf", true, r, (k, p, y) => k.GunRack(p, y, b.interior));
                    AgainstWall(pKit, b, pRoom, "Furn_Bookshelf", true, r, (k, p, y) => k.GunRack(p, y, b.interior));
                    pKit.CrateStack(b.At(c.x, pRoom.floor, c.y, 0f), b.Yaw(0f), 4, true);
                    break;
                case RoomType.WineCellar:
                    for (int k = 0; k < 4; k++) AgainstWall(pKit, b, pRoom, "Furn_Bookshelf", true, r, (kk, p, y) => kk.WineRack(p, y, b.interior));
                    if (W > 4.5f && D > 4.5f) pKit.WineRack(b.At(c.x, pRoom.floor, c.y, 0f), b.Yaw(lWide ? 0f : 90f), b.interior, true);
                    break;
                case RoomType.CoalStore:
                    pKit.CoalHeap(b.At(pRoom.rect.xMin + 1.6f, pRoom.floor, pRoom.rect.yMax - 1.6f, 0f), b.interior);
                    break;
                case RoomType.Store: case RoomType.Pantry: case RoomType.ColdRoom:
                    if (W > 4f && D > 4f) pKit.CrateStack(b.At(c.x, pRoom.floor, c.y, 0f), b.Yaw(0f), 4);
                    break;

                case RoomType.Office: case RoomType.Study: case RoomType.Police: case RoomType.Radio: case RoomType.Classroom:
                    if (At(pKit, b, pRoom, "Furn_Desk", c, lWide ? 180f : 90f))
                    {
                        At(pKit, b, pRoom, "Furn_OfficeChair", c + (lWide ? new Vector2(0, 0.85f) : new Vector2(0.85f, 0)), lWide ? 180f : -90f, false);
                        pKit.Distraction("phone", b.At(c.x + 0.6f, pRoom.floor, c.y, 0.79f), b.Yaw(0f));
                        if (pRoom.type == RoomType.Study) At(pKit, b, pRoom, "Furn_Rug", c, lWide ? 0f : 90f, false);
                    }
                    break;
                case RoomType.Bedroom: case RoomType.Suite:
                    AgainstWall(pKit, b, pRoom, "Furn_Wardrobe", true, r, (k, p, y) => k.Wardrobe(p, y, b.interior));
                    if (W > 3.5f && D > 3.5f) At(pKit, b, pRoom, "Furn_Rug", c, lWide ? 0f : 90f, false);
                    break;
                case RoomType.Bath:
                    AgainstWall(pKit, b, pRoom, "Furn_Toilet", false, r, (k, p, y) => k.Toilet(p, y, pZone));
                    break;
                case RoomType.Security:
                    if (At(pKit, b, pRoom, "Furn_Desk", c, 0f))
                    {
                        At(pKit, b, pRoom, "Furn_OfficeChair", c + new Vector2(0, -0.85f), 0f, false);
                        pKit.Model("Furn_TV", b.At(c.x - 0.5f, pRoom.floor, c.y + 0.15f, 0.79f), b.Yaw(180f), b.interior, ArchLib.Collide.None, 0.6f);
                        pKit.Model("Furn_TV", b.At(c.x + 0.5f, pRoom.floor, c.y + 0.15f, 0.79f), b.Yaw(180f), b.interior, ArchLib.Collide.None, 0.6f);
                    }
                    break;
                case RoomType.GuardRoom: case RoomType.Barracks: case RoomType.Dormitory:
                    if (W > 4f && D > 4f && At(pKit, b, pRoom, "Furn_DiningTable", c, lWide ? 0f : 90f))
                    {
                        At(pKit, b, pRoom, "Furn_DiningChair", c + (lWide ? new Vector2(0, -0.85f) : new Vector2(-0.85f, 0)), lWide ? 0f : 90f, false);
                        At(pKit, b, pRoom, "Furn_DiningChair", c + (lWide ? new Vector2(0, 0.85f) : new Vector2(0.85f, 0)), lWide ? 180f : -90f, false);
                        pKit.Distraction("radio", b.At(c.x + 0.5f, pRoom.floor, c.y, 0.77f), b.Yaw(0f));
                    }
                    break;
                case RoomType.Bar:
                    for (int t = 0; t < 3; t++)
                    {
                        Vector2 p = c + (lWide ? new Vector2((t - 1) * W * 0.28f, -D * 0.12f) : new Vector2(-W * 0.12f, (t - 1) * D * 0.28f));
                        if (!At(pKit, b, pRoom, "Furn_DiningTable", p, lWide ? 0f : 90f)) continue;
                        At(pKit, b, pRoom, "Furn_DiningChair", p + (lWide ? new Vector2(0, -0.85f) : new Vector2(-0.85f, 0)), lWide ? 0f : 90f, false);
                        At(pKit, b, pRoom, "Furn_DiningChair", p + (lWide ? new Vector2(0, 0.85f) : new Vector2(0.85f, 0)), lWide ? 180f : -90f, false);
                        if (t == 1) pKit.Drinks(b.At(p.x, pRoom.floor, p.y, 0.77f), b.Yaw(lWide ? 0f : 90f), pZone, 2, true, 1.2f);
                    }
                    break;
                case RoomType.Chapel: case RoomType.Church:
                    Pews(pKit, b, pRoom);
                    break;
                case RoomType.Crypt:
                    for (int k = 0; k < 3; k++)
                    {
                        Vector2 p = new Vector2(pRoom.rect.xMin + 1.4f, pRoom.rect.yMin + 1.6f + k * 2.4f);
                        if (p.y > pRoom.rect.yMax - 1.2f) break;
                        Rect lFoot = new Rect(p.x - 0.5f, p.y - 1.1f, 1f, 2.2f);
                        if (!Free(b, pRoom, lFoot, false)) continue;
                        b.used.Add((pRoom.floor, lFoot));
                        pKit.BoxObject("Tomb", b.At(p.x, pRoom.floor, p.y, 0f), b.Yaw(0f), b.interior, 0, true,
                            (ArchLib.Surface("stone_wall", 2f), new Vector3(0, 0.45f, 0), new Vector3(1f, 0.9f, 2.2f)),
                            (ArchLib.Surface("marble_01", 2f), new Vector3(0, 0.95f, 0), new Vector3(1.1f, 0.1f, 2.3f)));
                    }
                    break;
                case RoomType.Laundry: case RoomType.Linen:
                    AgainstWall(pKit, b, pRoom, "Furn_Shelves_Metal", false, r, (k, p, y) => k.LaundryCart(p, y));
                    break;
                case RoomType.Attic:
                    AgainstWall(pKit, b, pRoom, "Furn_Wardrobe", true, r, (k, p, y) => k.Wardrobe(p, y, b.interior));
                    break;
                case RoomType.Garage: case RoomType.Hangar: case RoomType.Workshop:
                    AgainstWall(pKit, b, pRoom, "Furn_Shelves_Metal", false, r, (k, p, y) => k.BigCrate(p, y));
                    break;
                case RoomType.Ballroom:
                {
                    bool lTall = b.spec.rooms.Exists(o => o.floor == pRoom.floor + 1 && (o.noFloor || o.isVoid) && o.rect.Overlaps(pRoom.rect));
                    float lHook = (lTall ? 2f : 1f) * b.Storey - b.spec.slab - 0.05f;
                    pKit.Chandelier(b.At(c.x, pRoom.floor, c.y, lHook), lHook - 0.4f);
                    // Dressed round tables in rows round a dance floor in the middle.
                    for (float u = pRoom.rect.xMin + 2.4f; u < pRoom.rect.xMax - 2f; u += 3.4f)
                        for (float v = pRoom.rect.yMin + 2.4f; v < pRoom.rect.yMax - 2f; v += 3.4f)
                        {
                            Vector2 p = new Vector2(u, v);
                            if (Mathf.Abs(p.x - c.x) < W * 0.2f && Mathf.Abs(p.y - c.y) < D * 0.2f) continue;
                            Rect lFoot = new Rect(p.x - 1.3f, p.y - 1.3f, 2.6f, 2.6f);
                            if (!Free(b, pRoom, lFoot, false)) continue;
                            b.used.Add((pRoom.floor, lFoot));
                            pKit.RoundTable(b.At(p.x, pRoom.floor, p.y, 0f), b.interior, pZone);
                        }
                    break;
                }
            }
            // Line the walls: the room's pieces, again and again until the perimeter is dressed.
            Item[] lItems = WallItems(pRoom.type);
            int lWanted = Mathf.Min(16, Mathf.RoundToInt((W + D) * 2f / 2.6f)), lMisses = 0, lPlaced = 0;
            for (int k = 0; lItems.Length > 0 && lPlaced < lWanted && lMisses < lItems.Length + 2; k++)
            {
                Item lItem = lItems[k % lItems.Length];
                if (AgainstWall(pKit, b, pRoom, lItem.model, lItem.tall, r) != null) { lPlaced++; lMisses = 0; }
                else lMisses++;
            }
            // A throwable in every room with a purpose.
            if (pRoom.type != RoomType.Stair && pRoom.type != RoomType.Corridor)
            {
                Vector2 p = new Vector2(Mathf.Lerp(pRoom.rect.xMin + 0.9f, pRoom.rect.xMax - 0.9f, (float)r.NextDouble()), Mathf.Lerp(pRoom.rect.yMin + 0.9f, pRoom.rect.yMax - 0.9f, (float)r.NextDouble()));
                if (Free(b, pRoom, new Rect(p.x - 0.2f, p.y - 0.2f, 0.4f, 0.4f), false)) pKit.Throwable(b.At(p.x, pRoom.floor, p.y, 0.05f));
                else pKit.Throwable(b.At(c.x, pRoom.floor, c.y, 0.05f));
            }
        }

        /// <summary>Rows of pews facing the altar on the far (north/east) wall.</summary>
        private static void Pews(ArchKit pKit, ArchResult b, ArchRoom pRoom)
        {
            bool lAlongZ = pRoom.rect.height >= pRoom.rect.width;
            ArchMat lWood = ArchLib.Surface("dark_wood", 1.2f);
            float lLength = lAlongZ ? pRoom.rect.width : pRoom.rect.height;
            float lPew = Mathf.Min(3.2f, lLength * 0.5f - 1.1f);
            if (lPew < 1f) return;
            // Altar.
            Vector2 lAltar = lAlongZ ? new Vector2(pRoom.rect.center.x, pRoom.rect.yMax - 1.2f) : new Vector2(pRoom.rect.xMax - 1.2f, pRoom.rect.center.y);
            float lYaw = lAlongZ ? 0f : 90f;
            Rect lAltarFoot = new Rect(lAltar.x - 1.1f, lAltar.y - 1.1f, 2.2f, 2.2f);
            if (Free(b, pRoom, lAltarFoot, false))
            {
                b.used.Add((pRoom.floor, lAltarFoot));
                pKit.BoxObject("Altar", b.At(lAltar.x, pRoom.floor, lAltar.y, 0f), b.Yaw(lYaw), b.interior, 0, true,
                    (ArchLib.Surface("marble_01", 1.5f), new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 0.9f)),
                    (ArchLib.Surface("velour_velvet", 1f), new Vector3(0, 1.01f, 0), new Vector3(2.1f, 0.02f, 1f)));
            }
            float lStart = lAlongZ ? pRoom.rect.yMin + 1.5f : pRoom.rect.xMin + 1.5f, lEnd = (lAlongZ ? lAltar.y : lAltar.x) - 2.2f;
            for (float u = lStart; u < lEnd; u += 1.25f)
                foreach (float lSide in new[] { -1f, 1f })
                {
                    float lAcross = (lAlongZ ? pRoom.rect.center.x : pRoom.rect.center.y) + lSide * (lPew * 0.5f + 0.6f);
                    Vector2 p = lAlongZ ? new Vector2(lAcross, u) : new Vector2(u, lAcross);
                    Rect lFoot = lAlongZ ? new Rect(p.x - lPew * 0.5f, p.y - 0.3f, lPew, 0.6f) : new Rect(p.x - 0.3f, p.y - lPew * 0.5f, 0.6f, lPew);
                    if (!Free(b, pRoom, lFoot, false)) continue;
                    b.used.Add((pRoom.floor, lFoot));
                    pKit.BoxObject("Pew", b.At(p.x, pRoom.floor, p.y, 0f), b.Yaw(lYaw), b.interior, 0, true,
                        (lWood, new Vector3(0, 0.44f, 0), new Vector3(lPew, 0.06f, 0.42f)),
                        (lWood, new Vector3(0, 0.75f, -0.2f), new Vector3(lPew, 0.6f, 0.05f)),
                        (lWood, new Vector3(-lPew * 0.5f + 0.03f, 0.4f, 0), new Vector3(0.06f, 0.8f, 0.45f)),
                        (lWood, new Vector3(lPew * 0.5f - 0.03f, 0.4f, 0), new Vector3(0.06f, 0.8f, 0.45f)));
                }
        }
    }
}
