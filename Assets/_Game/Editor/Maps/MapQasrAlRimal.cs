using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <b>Qasr al-Rimal</b> - a desert oasis town on audience day, its palm grove and dry wadi, the
    /// citadel on the Great Mesa and the radar on the Little Mesa (docs/maps/QasrAlRimal.md).
    /// <list type="bullet">
    /// <item><b>Ain Zarqa</b> (zone 0, 0 m): well plaza, two covered souk streets framing the keep,
    /// tea house, mosque court and minaret, caravanserai, bus station, flat roofs and planks.</item>
    /// <item><b>Grove and wadi</b> (zone 1, -4 m): date palms, irrigation channels from the spring
    /// pool, the date press, reed beds, the rock arch, the abandoned kasbah on the mesa's face, the
    /// Needle; and the radar on the Little Mesa (+38).</item>
    /// <item><b>The citadel</b> (zone 2, +42): hairpins, three courts, bastions, the keep.</item>
    /// </list>
    /// </summary>
    public partial class MapQasrAlRimal : RealMap
    {
        public const int ZONE_TOWN = 0, ZONE_GROVE = 1, ZONE_CITADEL = 2;
        private const float TOWN_Y = 0.6f, WADI_Y = -4f, MESA_Y = 42f, LITTLE_Y = 38f;
        private static readonly Rect MESA = Rect.MinMaxRect(-78f, 56f, 62f, 178f), LITTLE = Rect.MinMaxRect(96f, 78f, 192f, 176f);
        private static readonly Rect CITADEL = Rect.MinMaxRect(-72f, 60f, 58f, 172f);
        private static readonly Vector3 KEEP = new Vector3(-20f, MESA_Y, 96f);
        private static readonly Vector2 PLAZA = new Vector2(20f, -150f), POOL = new Vector2(-40f, -58f), NEEDLE = new Vector2(122f, 28f), KASBAH = new Vector2(72f, 26f);
        private static readonly Vector2[] WADI = { new Vector2(-260, -20), new Vector2(-150, -8), new Vector2(-40, -12), new Vector2(60, -4), new Vector2(160, -14), new Vector2(260, -6) };

        protected override string MapFile => "Map_QasrAlRimal";
        protected override TimeOfDay.Biome Biome => TimeOfDay.Biome.Desert;
        protected override float SunHeading => -20f;
        protected override float MinY => -20f;
        protected override float MaxY => 120f;
        protected override float WaterLevel => WADI_Y - 3f;

        [MenuItem("Goobyr/Build/Map Qasr al-Rimal")]
        public static void BuildMenu() => new MapQasrAlRimal().Build();

        // ================================================================ land

        private static float Mesa(Rect pRect, float pX, float pZ, float pEdge)
        {
            float d = LandShape.RectDistance(pRect, pX, pZ);
            return 1f - Mathf.SmoothStep(0f, 1f, d / pEdge);
        }

        protected override float Relief(float x, float z)
        {
            float h = TOWN_Y + LandShape.Fbm(x, z, 70f, 3, 4.2f) * 1.5f;
            // Dune sea to the south, rising in long crests.
            float lDunes = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-195f, -235f, z));
            h += lDunes * (6f + LandShape.Ridged(x * 0.6f, z * 1.4f, 45f, 3, 2.2f) * 16f);
            // The grove lies a little lower toward the wadi.
            h -= Mathf.Clamp01((z + 110f) / 60f) * Mathf.Clamp01((40f - z) / 40f) * 2.5f;
            // Great Mesa: cliffs all round, its south face a long slope for the hairpins.
            float lMesa = Mesa(MESA, x, z, 7f);
            float lSouth = Mathf.Clamp01((z - 4f) / (MESA.yMin - 4f)) * Mathf.Clamp01(1f - LandShape.RectDistance(Rect.MinMaxRect(MESA.xMin + 10f, 0f, MESA.xMax - 10f, MESA.yMin), x, z) / 12f);
            float lTop = Mathf.Max(lMesa, lSouth * lSouth * 0.98f);
            h = Mathf.Lerp(h, MESA_Y + LandShape.Fbm(x, z, 25f, 2, 9f) * 0.6f, lTop);
            // Little Mesa (radar), and the rock saddle joining them in the north.
            float lLittle = Mesa(LITTLE, x, z, 6f);
            h = Mathf.Lerp(h, LITTLE_Y, lLittle);
            float lSaddle = Mesa(Rect.MinMaxRect(55f, 150f, 105f, 180f), x, z, 6f);
            h = Mathf.Max(h, Mathf.Lerp(h, 34f, lSaddle));
            // Canyon walls north and east, the salt flat west.
            h += Mathf.Max(0f, z - 185f) * 3f + Mathf.Max(0f, x - 205f) * 3f;
            h += (Mathf.Clamp01((z - 185f) / 10f) + Mathf.Clamp01((x - 205f) / 10f)) * LandShape.Ridged(x, z, 30f, 3, 6f) * 14f;
            // The Needle: a rock spire.
            float lSpire = Vector2.Distance(new Vector2(x, z), NEEDLE);
            if (lSpire < 14f) h = Mathf.Max(h, Mathf.Lerp(60f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 14f, lSpire))));
            // The dry wadi.
            float d = LandShape.PathDistance(WADI, new Vector2(x, z), out _, out _);
            if (d < 22f && h < 20f) h = Mathf.Lerp(WADI_Y, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 22f, d)));
            return h;
        }

        protected override float Woods(float x, float z, float h)
        {
            bool lGrove = z > -100f && z < -24f && x > -160f && x < 150f;
            if (lGrove) return Mathf.Clamp01(0.45f + LandShape.Fbm(x, z, 30f, 2, 1.1f) * 0.4f);
            if (h > 30f) return 0.06f;
            return 0.08f;
        }

        protected override string TreeAt(float x, float z, float h, float pRoll, System.Random r, out float pScale)
        {
            pScale = 0.85f + (float)r.NextDouble() * 0.35f;
            bool lGrove = z > -100f && z < -24f && x > -160f && x < 150f;
            if (lGrove) return pRoll < 0.7f ? "Veg_DatePalm" : pRoll < 0.85f ? "Veg_DesertShrub_A" : "Veg_Banana";
            if (h > 30f) return pRoll < 0.5f ? "Veg_DesertShrub_B" : null;
            if (pRoll < 0.35f) return "Veg_DesertShrub_A";
            if (pRoll < 0.6f) return "Veg_DesertShrub_B";
            if (pRoll < 0.7f) return "Veg_Acacia";
            if (pRoll < 0.8f) return "Veg_DeadTree";
            pScale = 2f + (float)r.NextDouble() * 3f;
            return r.NextDouble() < 0.5 ? "Veg_Rock_B" : "Veg_Rock_C";
        }

        protected override TerrainLayer[] Layers() => new[]
        {
            PbrLibrary.Layer("dry_ground_01", 5f), PbrLibrary.Layer("gravelly_sand", 5f), PbrLibrary.Layer("cracked_red_ground", 5f),
            PbrLibrary.Layer("dirt", 4f), PbrLibrary.Layer("cliff_side", 10f), PbrLibrary.Layer("coast_sand_01", 6f),
            PbrLibrary.Layer("asphalt_02", 5f), PbrLibrary.Layer("patterned_cobblestone", 3f)
        };

        protected override void NaturalGround(float x, float z, float h, float pSlope, float[] w)
        {
            float n = LandShape.Fbm(x, z, 25f, 3, 1.7f);
            bool lGrove = z > -100f && z < -24f && x > -160f && x < 150f;
            w[(int)Ground.Sand] = 1f + Mathf.Clamp01((-180f - z) / 20f) * 6f + Mathf.Clamp01((-150f - x) / 20f) * 2f;
            w[(int)Ground.Grass] = lGrove ? 0.4f : Mathf.Clamp01(0.5f + n);
            w[(int)Ground.DryGrass] = Mathf.Clamp01(0.4f - n) * 2f;
            w[(int)Ground.ForestFloor] = lGrove ? 2f + n : (h > 25f ? 1.5f : 0f);
            w[(int)Ground.Dirt] = h < WADI_Y + 2f ? 4f : 0f;
        }

        protected override void Details()
        {
            DetailPrototype lGrass = VegLibrary.Detail("Veg_Grass_Dry", 0.8f, 1.3f, Color.white, new Color(0.95f, 0.85f, 0.65f));
            if (lGrass != null) K.Details.Add((lGrass, (x, z) =>
            {
                bool lGrove = z > -100f && z < -24f && x > -160f && x < 150f;
                if (Built(x, z) || K.LandSlope(x, z) > 30f) return 0f;
                return lGrove ? Mathf.Clamp01(0.55f + LandShape.Fbm(x, z, 15f, 2, 3f)) : Mathf.Clamp01(LandShape.Fbm(x, z, 18f, 2, 7f) - 0.15f) * 0.6f;
            }, 5));
            K.TallGrassDetail = VegLibrary.Detail("Veg_TallGrass_Dry", 0.9f, 1.25f, Color.white, new Color(0.95f, 0.88f, 0.7f));
        }

        // ================================================================ plan

        private readonly List<(ArchSpec spec, int zone)> _town = new List<(ArchSpec, int)>();
        private ArchSpec _keep, _teaHouse, _mosque, _serai, _press, _kasbah, _bunker, _baseStation, _busStation;
        private ArchResult _keepR, _teaR, _mosqueR, _seraiR, _pressR, _kasbahR, _bunkerR, _baseR, _busR;
        private readonly List<ArchResult> _houses = new List<ArchResult>();
        private readonly List<ArchResult> _bastions = new List<ArchResult>();
        private readonly List<ArchSpec> _bastionSpecs = new List<ArchSpec>();
        private int _houseCount;

        protected override void Plan()
        {
            Plaza(new Rect(PLAZA.x - 18f, PLAZA.y - 16f, 36f, 32f), Ground.Cobble, TOWN_Y, 6f);
            Shape.Flat(Rect.MinMaxRect(-140f, -200f, 150f, -104f), TOWN_Y, 10f);
            Clear.Add(Rect.MinMaxRect(-145f, -205f, 155f, -100f));
            Clear.Add(Rect.MinMaxRect(CITADEL.xMin - 2f, CITADEL.yMin - 2f, CITADEL.xMax + 2f, CITADEL.yMax + 2f));
            Shape.Flat(CITADEL, MESA_Y, 3f);
            Shape.Flat(Rect.MinMaxRect(LITTLE.xMin + 3f, LITTLE.yMin + 3f, LITTLE.xMax - 3f, LITTLE.yMax - 3f), LITTLE_Y, 3f);
            Shape.Disc(POOL, 9f, -2.2f, 4f);
            Shape.Flat(new Rect(KASBAH.x - 10f, KASBAH.y - 9f, 20f, 18f), 18f, 4f);
            Shape.Flat(new Rect(NEEDLE.x - 3f, NEEDLE.y - 3f, 6f, 6f), 60f, 1f);
            // Souk streets (covered later), the main street, the caravan road, the bus station road.
            Road(6f, Ground.Cobble, new Vector2(2, -134), new Vector2(2, -106));
            Road(6f, Ground.Cobble, new Vector2(40, -134), new Vector2(40, -106));
            Road(7f, Ground.Cobble, new Vector2(-60, -150), new Vector2(2, -150));
            Road(7f, Ground.Cobble, new Vector2(38, -150), new Vector2(120, -150), new Vector2(130, -182));
            Road(6f, Ground.Dirt, new Vector2(-250, -125), new Vector2(-140, -140), new Vector2(-95, -145));
            // Grove paths, the ford over the wadi, the hairpins up the mesa's south face.
            Road(3f, Ground.Dirt, 15f, new Vector2(20, -102), new Vector2(18, -60), new Vector2(30, -26), new Vector2(40, -6));
            Road(3f, Ground.Dirt, 15f, new Vector2(-40, -48), new Vector2(-90, -40), new Vector2(-130, -60));
            Road(3f, Ground.Dirt, 15f, new Vector2(-40, -48), new Vector2(60, -58), new Vector2(110, -40));
            Road(6f, Ground.Asphalt, 34f, new Vector2(48, -2), new Vector2(46, 9), new Vector2(-46, 15), new Vector2(-48, 26), new Vector2(42, 32), new Vector2(44, 42), new Vector2(-24, 47), new Vector2(-6, 54), new Vector2(-4, 59));
            Road(4f, Ground.Dirt, 20f, new Vector2(46, 9), new Vector2(KASBAH.x - 9f, KASBAH.y - 4f));
            Road(4f, Ground.Dirt, 20f, new Vector2(160, -14), new Vector2(170, 40), new Vector2(150, 74));
            PlanTown();
            PlanGrove();
            PlanCitadel();
        }

        // ================================================================ dress

        protected override void Dress()
        {
            ArchOutdoor.ResetCount();
            Water();
            DressTown();
            DressGrove();
            DressCitadel();
            Tunnels();
            Matches();
        }

        private void Water()
        {
            K.Water(new Vector3(POOL.x, -1.6f, POOL.y), new Vector2(16f, 16f));
            // Irrigation channels from the spring pool across the grove (shallow, a step wide).
            River(-1.7f, 1.4f, Vector3.zero, POOL + new Vector2(8, 0), new Vector2(40, -64), new Vector2(110, -56));
            River(-1.7f, 1.4f, Vector3.zero, POOL + new Vector2(-8, 0), new Vector2(-90, -66), new Vector2(-140, -58));
            River(-1.7f, 1.4f, Vector3.zero, POOL + new Vector2(0, -8), new Vector2(-30, -92));
            Poi(G(POOL, 1f), "Spring pool", MapPoi.Kind.Landmark);
        }
    }
}
