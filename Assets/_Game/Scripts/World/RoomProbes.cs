using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.World
{
    /// <summary>
    /// Local reflections, without a bake. When a map loads, every building floor gets a box-projected
    /// reflection probe (the box covers the floor's rooms, the capture sits at eye height in its largest
    /// room), and the streets near buildings get a coarse set of outdoor probes; all are realtime
    /// probes rendered once, two per frame, and again when the time of day changes. Interiors then
    /// reflect their own walls instead of the blue sky, and metal, glass and water outside reflect the
    /// buildings round them. Rooms come from the generated buildings' <see cref="AI.SecurityZone"/>
    /// boxes. Added by <see cref="FX.TimeOfDay"/>; Forward+ blends and box-projects the probes.
    /// </summary>
    public class RoomProbes : MonoBehaviour
    {
        public const int ROOM_RESOLUTION = 64, OUTDOOR_RESOLUTION = 64, MAX_ROOMS = 160, MAX_OUTDOOR = 48;
        private const float OUTDOOR_SPACING = 48f;

        public static RoomProbes Instance { get; private set; }
        public int Count => _probes.Count;
        /// <summary>The first capture of the whole map is over (the loading screen waits for it).</summary>
        public bool Captured { get; private set; }

        private readonly List<ReflectionProbe> _probes = new List<ReflectionProbe>();
        private Coroutine _rendering;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            // Let the map settle (time of day applied, everything spawned) before the first capture.
            yield return null;
            yield return null;
            Build();
            Refresh();
        }

        /// <summary>Re-captures every probe (the time of day changed), two per frame.</summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            if (_rendering != null) StopCoroutine(_rendering);
            _rendering = StartCoroutine(RenderAll());
        }

        private IEnumerator RenderAll()
        {
            for (int i = 0; i < _probes.Count; i++)
            {
                if (_probes[i] != null) _probes[i].RenderProbe();
                if (i % 2 == 1) yield return null;
            }
            _rendering = null;
            Captured = true;
        }

        /// <summary>Creates the probes (once per map; again only if called again).</summary>
        public void Build()
        {
            foreach (ReflectionProbe p in _probes) if (p != null) Destroy(p.gameObject);
            _probes.Clear();
            Transform lRoot = new GameObject("RoomProbes").transform;
            lRoot.SetParent(transform, false);

            // Interiors: per building floor, the box over its rooms, captured from the largest one.
            Dictionary<(Transform building, int floor), List<AI.SecurityZone>> lFloors = new Dictionary<(Transform, int), List<AI.SecurityZone>>();
            foreach (AI.SecurityZone z in FindObjectsByType<AI.SecurityZone>(FindObjectsSortMode.None))
            {
                if (z == null || z.transform.parent == null || z.transform.parent.name != "SecurityZones") continue;
                Transform lBuilding = z.transform.parent.parent;
                if (lBuilding == null) continue;
                float lStorey = Mathf.Max(2.5f, z.Size.y);
                int lFloor = Mathf.RoundToInt((z.transform.position.y - lBuilding.position.y) / lStorey);
                if (!lFloors.TryGetValue((lBuilding, lFloor), out List<AI.SecurityZone> lList)) lFloors[(lBuilding, lFloor)] = lList = new List<AI.SecurityZone>();
                lList.Add(z);
            }
            List<(float area, Vector3 eye, Bounds box)> lRooms = new List<(float, Vector3, Bounds)>();
            foreach (List<AI.SecurityZone> lZones in lFloors.Values)
            {
                Bounds lBox = default;
                bool lAny = false;
                AI.SecurityZone lLargest = null;
                float lFloorArea = 0f;
                foreach (AI.SecurityZone z in lZones)
                {
                    if (!Covered(z)) continue;
                    Bounds b = WorldBox(z);
                    if (!lAny) { lBox = b; lAny = true; } else lBox.Encapsulate(b);
                    float lArea = z.Size.x * z.Size.z;
                    lFloorArea += lArea;
                    if (lLargest == null || lArea > lLargest.Size.x * lLargest.Size.z) lLargest = z;
                }
                if (!lAny || lFloorArea < 12f) continue;
                Vector3 lEye = lLargest.transform.position + Vector3.up * Mathf.Min(1.6f, lLargest.Size.y * 0.5f);
                lRooms.Add((lFloorArea, lEye, lBox));
            }
            lRooms.Sort((a, b) => b.area.CompareTo(a.area));
            for (int i = 0; i < lRooms.Count && i < MAX_ROOMS; i++) Add(lRoot, "Room", lRooms[i].eye, lRooms[i].box, ROOM_RESOLUTION, 1, 0.6f);

            // Outdoors: a coarse grid round the buildings (the open land keeps the sky's reflection).
            List<Vector3> lBuildings = new List<Vector3>();
            foreach (Systems.BuildingVolume b in FindObjectsByType<Systems.BuildingVolume>(FindObjectsSortMode.None)) lBuildings.Add(b.transform.position);
            Terrain lLand = Terrain.activeTerrain;
            if (lLand != null && lBuildings.Count > 0)
            {
                Vector3 o = lLand.transform.position, s = lLand.terrainData.size;
                int lOutdoor = 0;
                for (float x = o.x + OUTDOOR_SPACING * 0.5f; x < o.x + s.x && lOutdoor < MAX_OUTDOOR; x += OUTDOOR_SPACING)
                    for (float z = o.z + OUTDOOR_SPACING * 0.5f; z < o.z + s.z && lOutdoor < MAX_OUTDOOR; z += OUTDOOR_SPACING)
                    {
                        bool lNear = false;
                        foreach (Vector3 b in lBuildings)
                            if ((new Vector2(b.x - x, b.z - z)).sqrMagnitude < 34f * 34f) { lNear = true; break; }
                        if (!lNear) continue;
                        float y = lLand.SampleHeight(new Vector3(x, 0f, z)) + o.y;
                        Add(lRoot, "Street", new Vector3(x, y + 3f, z), new Bounds(new Vector3(x, y + 10f, z), new Vector3(OUTDOOR_SPACING, 26f, OUTDOOR_SPACING)), OUTDOOR_RESOLUTION, 0, 8f);
                        lOutdoor++;
                    }
            }
            Debug.Log($"[World] {_probes.Count} reflection probes ({Mathf.Min(lRooms.Count, MAX_ROOMS)} building floors)");
        }

        /// <summary>A room with a ceiling over it (not a terrace, a courtyard or a roof).</summary>
        private static bool Covered(AI.SecurityZone pZone)
        {
            Vector3 p = pZone.transform.position + Vector3.up * 1.2f;
            return Physics.Raycast(p, Vector3.up, pZone.Size.y + 1.5f, Core.GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>The world-aligned box round a zone (probes are axis-aligned; a turned building gets its enclosing box).</summary>
        private static Bounds WorldBox(AI.SecurityZone pZone)
        {
            Vector3 lHalf = pZone.Size * 0.5f;
            Vector3 lCenter = pZone.transform.position + Vector3.up * lHalf.y;
            Bounds b = new Bounds(lCenter, Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3((i & 1) == 0 ? -lHalf.x : lHalf.x, (i & 2) == 0 ? -lHalf.y : lHalf.y, (i & 4) == 0 ? -lHalf.z : lHalf.z);
                b.Encapsulate(lCenter + pZone.transform.rotation * c);
            }
            return b;
        }

        private void Add(Transform pRoot, string pName, Vector3 pEye, Bounds pBox, int pResolution, int pImportance, float pBlend)
        {
            GameObject lObject = new GameObject(pName + "Probe");
            lObject.transform.SetParent(pRoot, false);
            lObject.transform.position = pEye;
            ReflectionProbe p = lObject.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            p.resolution = pResolution;
            p.hdr = true;
            p.boxProjection = true;
            p.center = pBox.center - pEye;
            p.size = pBox.size;
            p.blendDistance = pBlend;
            p.importance = pImportance;
            p.nearClipPlane = 0.1f;
            p.farClipPlane = Mathf.Max(30f, pBox.size.magnitude);
            p.shadowDistance = 30f;
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            p.cullingMask = Core.GameLayers.WorldMask;
            _probes.Add(p);
        }
    }
}
