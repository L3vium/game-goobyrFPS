using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// What the three maps share on top of <see cref="MapKit"/>: furnished small buildings with a
    /// guard, scattering vegetation while keeping roads and plazas clear, guard posts, weapon
    /// caches, and the minimum activity set every zone needs for its targets.
    /// </summary>
    public abstract class MapBuilderBase
    {
        protected MapKit K;
        protected readonly List<Rect> Clear = new List<Rect>();

        protected abstract string MapFile { get; }
        protected abstract void Compose();

        public void Build()
        {
            GameDatabase lDatabase = GameDatabase.Instance;
            MapDefinition lMap = lDatabase.maps.Find(m => m.sceneName == MapFile);
            K = new MapKit(lMap, MapFile.GetHashCode());
            Compose();
            K.Finish();
        }

        protected static Rect R(float pX0, float pZ0, float pX1, float pZ1) => Rect.MinMaxRect(Mathf.Min(pX0, pX1), Mathf.Min(pZ0, pZ1), Mathf.Max(pX0, pX1), Mathf.Max(pZ0, pZ1));

        protected bool IsClear(float pX, float pZ)
        {
            foreach (Rect lRect in Clear) if (lRect.Contains(new Vector2(pX, pZ))) return false;
            return true;
        }

        /// <summary>Trees in a rectangle, avoiding the clear areas (roads, plazas, buildings).</summary>
        protected void Forest(Rect pArea, int pCount, MapKit.TreeKind pKind, float pMinScale, float pMaxScale, Color? pTint = null)
        {
            for (int i = 0; i < pCount * 3 && pCount > 0; i++)
            {
                float x = Mathf.Lerp(pArea.xMin, pArea.xMax, (float)K.Rng.NextDouble());
                float z = Mathf.Lerp(pArea.yMin, pArea.yMax, (float)K.Rng.NextDouble());
                if (!IsClear(x, z)) continue;
                K.Tree(K.At(x, z), pKind, Mathf.Lerp(pMinScale, pMaxScale, (float)K.Rng.NextDouble()), pTint);
                pCount--;
            }
        }

        protected void Rocks(Rect pArea, int pCount, Color pColor, float pMin = 0.8f, float pMax = 2.2f)
        {
            for (int i = 0; i < pCount * 3 && pCount > 0; i++)
            {
                float x = Mathf.Lerp(pArea.xMin, pArea.xMax, (float)K.Rng.NextDouble());
                float z = Mathf.Lerp(pArea.yMin, pArea.yMax, (float)K.Rng.NextDouble());
                if (!IsClear(x, z)) continue;
                K.Rock(K.At(x, z), Mathf.Lerp(pMin, pMax, (float)K.Rng.NextDouble()), pColor);
                pCount--;
            }
        }

        /// <summary>A small building with a door, furniture, a guard (optional) and a weapon inside.</summary>
        protected MapKit.BuildingResult House(string pName, float pX, float pZ, Vector2 pSize, float pYaw, int pFloors, Color pWall, Color pRoof, int pZone, int pDoorSide = 0, bool pGuard = true, ItemKind pLoot = ItemKind.Pistol, bool pPitched = false, int pClimbSide = -1)
        {
            MapKit.BuildingSpec lSpec = new MapKit.BuildingSpec
            {
                name = pName, origin = K.At(pX, pZ), size = pSize, yaw = pYaw, floors = pFloors, wall = pWall, roof = pRoof,
                trim = pWall * 0.75f, floor = new Color(0.68f, 0.56f, 0.45f), pitchedRoof = pPitched, roofAccess = !pPitched, climbSide = pClimbSide,
                stairs = pFloors > 1 || !pPitched
            };
            lSpec.openings.Add(new MapKit.Opening(pDoorSide, 0.35f));
            lSpec.openings.Add(new MapKit.Opening((pDoorSide + 2) % 4, 0.65f));
            MapKit.BuildingResult lBuilding = K.Building(lSpec);
            Clear.Add(R(pX - pSize.x * 0.6f - 1f, pZ - pSize.y * 0.6f - 1f, pX + pSize.x * 0.6f + 1f, pZ + pSize.y * 0.6f + 1f));

            // Furniture and cover inside.
            K.Block(lBuilding.interior, "Counter", lBuilding.Local(-pSize.x * 0.25f, 0.5f, 0), new Vector3(2.2f, 1f, 0.8f), new Color(0.55f, 0.38f, 0.25f), Core.GameLayers.DEFAULT, true, lBuilding.Rotation, true);
            if (K.Rng.NextDouble() < 0.5) K.HidingSpot(lBuilding.Local(-pSize.x * 0.5f + 1f, 0, -pSize.y * 0.5f + 0.8f), pYaw, "closet");
            if (pGuard) K.Npc(NpcRole.Guard, lBuilding.Local(pSize.x * 0.1f, 0.1f, -pSize.y * 0.2f), pYaw + 180f, pZone, K.Rng.NextDouble() < 0.5 ? ItemKind.Smg : ItemKind.Pistol);
            K.Weapon(pLoot, lBuilding.Local(-pSize.x * 0.25f, 1.05f, 0));
            K.Lamp(lBuilding.Local(0, 3f, 0), new Color(1f, 0.85f, 0.6f), 9f, 1.4f, false);
            return lBuilding;
        }

        /// <summary>Every activity type a target can need, at least once in the zone.</summary>
        protected void BaseActivities(int pZone, Vector3 pCenter, float pRadius)
        {
            ActivityType[] lAll = { ActivityType.Idle, ActivityType.Phone, ActivityType.Smoke, ActivityType.Inspect, ActivityType.Office, ActivityType.Garden, ActivityType.Dining, ActivityType.Speech, ActivityType.Lookout, ActivityType.Workout };
            for (int i = 0; i < lAll.Length; i++)
            {
                float lAngle = i * Mathf.PI * 2f / lAll.Length;
                Vector3 lPos = pCenter + new Vector3(Mathf.Cos(lAngle), 0, Mathf.Sin(lAngle)) * pRadius;
                K.Activity(lAll[i], K.At(lPos.x, lPos.z, 0.1f), lAngle * Mathf.Rad2Deg, pZone);
            }
        }

        /// <summary>A guard post: a guard, sandbag cover in front, sometimes a crate of grenades.</summary>
        protected void Post(int pZone, float pX, float pZ, float pYaw, ItemKind pWeapon)
        {
            Vector3 lPos = K.At(pX, pZ, 0.1f);
            K.Npc(NpcRole.Guard, lPos, pYaw, pZone, pWeapon);
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            K.Block(K.Props, "Sandbags", lPos + lRot * new Vector3(0, 0.45f, 1.4f), new Vector3(2.4f, 0.9f, 0.7f), new Color(0.75f, 0.68f, 0.5f), Core.GameLayers.DEFAULT, true, lRot, true);
        }

        /// <summary>A watchtower with a ladder and a lookout with a sniper on top.</summary>
        protected void Watchtower(int pZone, float pX, float pZ, float pHeight, float pYaw, Color pColor)
        {
            Vector3 lBase = K.At(pX, pZ);
            for (int i = 0; i < 4; i++)
            {
                Vector3 lLeg = new Vector3(i % 2 == 0 ? -1.4f : 1.4f, pHeight * 0.5f, i < 2 ? -1.4f : 1.4f);
                K.Block(K.Env, "Leg", lBase + lLeg, new Vector3(0.3f, pHeight, 0.3f), pColor * 0.8f);
            }
            K.Block(K.Env, "Platform", lBase + Vector3.up * pHeight, new Vector3(3.6f, 0.3f, 3.6f), pColor);
            for (int s = 0; s < 4; s++)
            {
                Quaternion lRot = Quaternion.Euler(0, s * 90f, 0);
                K.Block(K.Env, "Rail", lBase + Vector3.up * (pHeight + 0.6f) + lRot * new Vector3(0, 0, 1.75f), new Vector3(3.6f, 0.9f, 0.12f), pColor, Core.GameLayers.DEFAULT, true, lRot);
            }
            K.Block(K.Env, "Roof", lBase + Vector3.up * (pHeight + 2.6f), new Vector3(4f, 0.25f, 4f), pColor * 0.7f);
            Quaternion lLadderRot = Quaternion.Euler(0, pYaw + 180f, 0);
            K.Climbable(K.Env, lBase + lLadderRot * new Vector3(0, 0, -1.9f) + Vector3.up * (pHeight * 0.5f + 0.5f), new Vector3(0.8f, pHeight + 1f, 0.15f), lLadderRot, new Color(0.5f, 0.4f, 0.3f), true);
            K.Npc(NpcRole.Lookout, lBase + Vector3.up * (pHeight + 0.2f), pYaw, pZone, ItemKind.Sniper);
            K.Weapon(ItemKind.Sniper, lBase + Vector3.up * (pHeight + 0.25f) + new Vector3(1f, 0, 1f));
        }

        protected void ShippingContainer(Vector3 pPosition, float pYaw, Color pColor, bool pLadder = false)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            K.Block(K.Env, "Container", pPosition + Vector3.up * 1.3f, new Vector3(2.5f, 2.6f, 6.1f), pColor, Core.GameLayers.DEFAULT, true, lRot, true);
            for (int i = -2; i <= 2; i++)
                K.Block(K.Env, "Rib", pPosition + Vector3.up * 1.3f + lRot * new Vector3(1.27f, 0, i * 1.2f), new Vector3(0.06f, 2.5f, 0.15f), pColor * 0.8f, Core.GameLayers.DEFAULT, false, lRot);
            if (pLadder) K.Climbable(K.Env, pPosition + lRot * new Vector3(0, 1.4f, -3.12f), new Vector3(0.7f, 2.8f, 0.1f), lRot, new Color(0.4f, 0.4f, 0.45f), true);
        }
    }
}
