using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The rooms the sentry guns guard are working rooms, not empty boxes: each gets a workbench against a
    /// wall, two stacks of military crates and a cable tray hung under the ceiling along its longest free
    /// wall. Nothing goes on the gun's footprint or in its line of fire.
    /// </summary>
    public abstract partial class RealMap
    {
        private void DressTurretRooms()
        {
            AutoTurret[] lTurrets = Object.FindObjectsByType<AutoTurret>(FindObjectsSortMode.None);
            HashSet<(ArchResult, string)> lDone = new HashSet<(ArchResult, string)>();
            System.Random r = new System.Random(4242);
            int lBenches = 0, lCrates = 0, lTrays = 0;
            foreach (AutoTurret t in lTurrets)
            {
                if (!RoomOf(t.transform.position, out ArchResult b, out ArchRoom lRoom)) continue;
                // Keep the gun's own spot and its line of fire clear, for every gun in the building.
                foreach (AutoTurret o in lTurrets)
                {
                    if (!RoomOf(o.transform.position, out ArchResult ob, out ArchRoom oRoom) || ob != b) continue;
                    Vector3 l = b.root.InverseTransformPoint(o.transform.position);
                    Vector3 f = b.root.InverseTransformDirection(o.transform.forward);
                    Vector2 f2 = new Vector2(f.x, f.z).normalized;
                    b.used.Add((oRoom.floor, new Rect(l.x - 0.8f, l.z - 0.8f, 1.6f, 1.6f)));
                    for (float d = 0.8f; d < 6f; d += 0.6f)
                        b.used.Add((oRoom.floor, new Rect(l.x + f2.x * d - 0.7f, l.z + f2.y * d - 0.7f, 1.4f, 1.4f)));
                }
                if (!lDone.Add((b, lRoom.name))) continue;
                if (ArchFurnish.WallSpot(b, lRoom, new Vector2(2.3f, 0.9f), false, r, out Vector3 lBench, out float lBenchYaw)
                    || ArchFurnish.FloorSpot(b, lRoom, new Vector2(2.3f, 0.9f), r, out lBench, out lBenchYaw))
                {
                    ArchFurnish.Workbench(A, lBench, lBenchYaw, b.interior);
                    lBenches++;
                }
                for (int k = 0; k < 2; k++)
                    if (ArchFurnish.WallSpot(b, lRoom, new Vector2(1.3f, 1.3f), false, r, out Vector3 lStack, out float lStackYaw)
                        || ArchFurnish.FloorSpot(b, lRoom, new Vector2(1.3f, 1.3f), r, out lStack, out lStackYaw))
                    {
                        A.CrateStack(lStack, lStackYaw, 3 + k, true);
                        lCrates++;
                    }
                if (CableTray(b, lRoom, lTurrets)) lTrays++;
            }
            Debug.Log($"[Build] {K.Map.sceneName}: turret rooms dressed ({lBenches} workbenches, {lCrates} crate stacks, {lTrays} cable trays)");
        }

        /// <summary>The building and enclosed room a world point is in.</summary>
        private bool RoomOf(Vector3 pWorld, out ArchResult pBuilding, out ArchRoom pRoom)
        {
            foreach (ArchResult b in _buildings)
            {
                Vector3 l = b.root.InverseTransformPoint(pWorld);
                int lFloor = Mathf.FloorToInt((l.y + 0.1f) / b.spec.storey);
                foreach (ArchRoom lRoom in b.spec.rooms)
                    if (lRoom.floor == lFloor && !lRoom.isVoid && !lRoom.open && lRoom.rect.Contains(new Vector2(l.x, l.z)))
                    {
                        pBuilding = b;
                        pRoom = lRoom;
                        return true;
                    }
            }
            pBuilding = null;
            pRoom = null;
            return false;
        }

        /// <summary>
        /// A galvanised ladder tray with a bundle of black cables, on drop rods under the ceiling, along the
        /// longest wall that has no ceiling gun near it.
        /// </summary>
        private bool CableTray(ArchResult b, ArchRoom pRoom, AutoTurret[] pTurrets)
        {
            Rect R = pRoom.rect;
            float lCeiling = b.Storey - b.spec.slab, y = lCeiling - 0.32f;
            int[] lOrder = R.width >= R.height ? new[] { 0, 2, 1, 3 } : new[] { 1, 3, 0, 2 };
            foreach (int lSide in lOrder)
            {
                bool lAlongX = lSide % 2 == 0;
                float lLength = (lAlongX ? R.width : R.height) - 0.9f;
                if (lLength < 2.5f) continue;
                Vector2 c = lSide == 0 ? new Vector2(R.center.x, R.yMin + 0.45f) : lSide == 1 ? new Vector2(R.xMax - 0.45f, R.center.y)
                    : lSide == 2 ? new Vector2(R.center.x, R.yMax - 0.45f) : new Vector2(R.xMin + 0.45f, R.center.y);
                bool lClash = false;
                foreach (AutoTurret t in pTurrets)
                {
                    Vector3 l = b.root.InverseTransformPoint(t.transform.position);
                    if (Mathf.Abs(l.y - (pRoom.floor * b.spec.storey + y)) > 1.2f) continue;
                    float lAcross = lAlongX ? Mathf.Abs(l.z - c.y) : Mathf.Abs(l.x - c.x);
                    float lAlong = lAlongX ? Mathf.Abs(l.x - c.x) : Mathf.Abs(l.z - c.y);
                    if (lAcross < 0.9f && lAlong < lLength * 0.5f + 0.5f) { lClash = true; break; }
                }
                if (lClash) continue;
                ArchMat lSteel = ArchLib.Galvanised, lCable = ArchLib.PlainMat("CableBundle", new Color(0.04f, 0.04f, 0.045f), 0.45f, 0f);
                List<(ArchMat, Vector3, Vector3)> lParts = new List<(ArchMat, Vector3, Vector3)>
                {
                    (lSteel, new Vector3(0, 0, -0.15f), new Vector3(lLength, 0.08f, 0.015f)),
                    (lSteel, new Vector3(0, 0, 0.15f), new Vector3(lLength, 0.08f, 0.015f)),
                    (lCable, new Vector3(0, 0.0f, -0.03f), new Vector3(lLength - 0.02f, 0.05f, 0.11f)),
                    (lCable, new Vector3(0, 0.02f, 0.07f), new Vector3(lLength - 0.02f, 0.035f, 0.07f))
                };
                for (float x = -lLength * 0.5f + 0.15f; x < lLength * 0.5f; x += 0.3f)
                    lParts.Add((lSteel, new Vector3(x, -0.035f, 0), new Vector3(0.025f, 0.012f, 0.3f)));
                for (float x = -lLength * 0.5f + 0.4f; x < lLength * 0.5f; x += 1.5f)
                    foreach (float z in new[] { -0.17f, 0.17f })
                        lParts.Add((lSteel, new Vector3(x, 0.16f, z), new Vector3(0.012f, 0.32f + 0.02f, 0.012f)));
                A.BoxObject("CableTray", b.At(c.x, pRoom.floor, c.y, y), b.Yaw(lAlongX ? 0f : 90f), b.interior, 0, false, lParts.ToArray());
                return true;
            }
            return false;
        }
    }
}
