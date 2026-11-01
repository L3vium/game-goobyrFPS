using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// <b>San Lázaro</b> - a Colombian river town on fiesta day, its coffee terraces, the airstrip on
    /// the ridge and the white hacienda on the hill (docs/maps/SanLazaro.md). 500 m of land, play
    /// ~360 m, bordered by the Río Claro gorge (south, west), basalt cliffs (north) and the canopy and
    /// waterfall (east).
    /// <list type="bullet">
    /// <item><b>Pueblo</b> (zone 0, 0 m): plaza and stage, church and bell tower, market street,
    /// cantina and its cellar, police post, dock, houses, the bus stop.</item>
    /// <item><b>Cafetal and airstrip</b> (zone 1, +13 to +41, airstrip +30): five coffee terraces with
    /// shade trees, picker huts and drying sheds, the beneficio, the water tower, the quarry, the
    /// banana grove, the jungle chapel ruin; the ridge track, the checkpoint, hangars, radio shack,
    /// the DC-3 wreck and the fuel depot.</item>
    /// <item><b>Hacienda La Cumbre</b> (zone 2, +46): the walled grounds and Casa La Cumbre.</item>
    /// <item><b>Cueva del Diablo</b>: cantina cellar - quarry - casa wine cellar, and quarry - waterfall dock.</item>
    /// </list>
    /// </summary>
    public partial class MapSanLazaro : RealMap
    {
        public const int ZONE_PUEBLO = 0, ZONE_CAFETAL = 1, ZONE_HACIENDA = 2;
        private const float WATER_Y = -4f, PUEBLO_Y = 1.2f, PAD_Y = 46f, AIRSTRIP_Y = 30f;
        /// <summary>Terraces: z from, z to, height.</summary>
        private static readonly Vector3[] TERRACES = { new Vector3(-58, -42, 13), new Vector3(-32, -16, 20), new Vector3(-6, 10, 27), new Vector3(20, 36, 34), new Vector3(46, 64, 41) };
        private static readonly float[] TERRACE_X1 = { 120, 128, 138, 150, 176 };
        private const float TERRACE_X0 = -45f;
        private static readonly Rect COMPOUND = Rect.MinMaxRect(62, 70, 158, 142);
        private static readonly Vector3 CASA = new Vector3(88, PAD_Y, 96);
        private static readonly Vector2 QUARRY = new Vector2(128, -76), CHAPEL_RUIN = new Vector2(-158, 52), WATERFALL_POOL = new Vector2(198, -66);

        private static readonly Vector2[] RIVER =
        {
            new Vector2(210, -58), new Vector2(200, -110), new Vector2(165, -168), new Vector2(95, -206), new Vector2(0, -214),
            new Vector2(-95, -211), new Vector2(-165, -190), new Vector2(-208, -140), new Vector2(-216, -60), new Vector2(-212, 20),
            new Vector2(-232, 80), new Vector2(-262, 110)
        };

        protected override string MapFile => "Map_SanLazaro";
        protected override TimeOfDay.Biome Biome => TimeOfDay.Biome.Jungle;
        protected override float SunHeading => 30f;
        protected override float WaterLevel => WATER_Y;
        protected override float MinY => -20f;
        protected override float MaxY => 120f;

        [MenuItem("Goobyr/Build/Map San Lazaro")]
        public static void BuildMenu() => new MapSanLazaro().Build();

        // ================================================================ land

        protected override float Relief(float x, float z)
        {
            // Base: the valley climbs north from the river town to the hacienda hill.
            float h;
            if (z < -112f) h = PUEBLO_Y;
            else if (z < 70f) h = Mathf.Lerp(PUEBLO_Y, 44f, Mathf.SmoothStep(0f, 1f, (z + 112f) / 182f));
            else h = 46f + (z - 70f) * 0.06f;
            // The banana grove and the chapel ruin lie in a lower fold to the west.
            float lWest = Mathf.Clamp01((-60f - x) / 50f) * Mathf.Clamp01((z + 120f) / 40f) * Mathf.Clamp01((110f - z) / 50f);
            h -= lWest * 8f;
            // The ridge between airstrip and hacienda.
            h += Mathf.Clamp01(1f - Mathf.Abs(x + 10f) / 45f) * Mathf.Clamp01((z - 80f) / 30f) * 6f;
            h += LandShape.Fbm(x, z, 60f, 4, 3.1f) * 3f;
            // North basalt cliffs.
            h += Mathf.Max(0f, z - 182f) * 2.6f + LandShape.Ridged(x, z, 40f, 3, 1.7f) * Mathf.Clamp01((z - 182f) / 20f) * 18f;
            // East: the plateau under the canopy, the waterfall's lip.
            float lEast = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(196f, 214f, x)) * Mathf.Clamp01((z + 150f) / 30f);
            h = Mathf.Lerp(h, 32f + LandShape.Fbm(x, z, 30f, 3, 7.7f) * 4f, lEast);
            // The gorge's far walls (south and west of the river).
            float lFar = Mathf.Max(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-222f, -240f, z)), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-226f, -244f, x)));
            h = Mathf.Lerp(h, 26f + LandShape.Ridged(x, z, 35f, 3, 4.4f) * 12f, lFar);
            // Río Claro: a gorge with a flat bed.
            float d = LandShape.PathDistance(RIVER, new Vector2(x, z), out _, out _);
            if (d < 26f) h = Mathf.Lerp(-7.5f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 26f, d)));
            // The waterfall pool.
            float lPool = Vector2.Distance(new Vector2(x, z), WATERFALL_POOL);
            if (lPool < 22f) h = Mathf.Lerp(-7.5f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 22f, lPool)));
            return h;
        }

        /// <summary>Sightlines kept open through the canopy: plaza and dock to the mirador.</summary>
        private static readonly Vector2[] VIEW_PLAZA = { new Vector2(-5f, -150f), new Vector2(123f, 124f) }, VIEW_DOCK = { new Vector2(-30f, -200f), new Vector2(123f, 124f) };

        protected override float Woods(float x, float z, float h)
        {
            if (h < WATER_Y + 1f) return 0f;
            Vector2 p = new Vector2(x, z);
            if (z > -105f && z < 100f && (LandShape.PathDistance(VIEW_PLAZA, p, out _, out _) < 11f || LandShape.PathDistance(VIEW_DOCK, p, out _, out _) < 11f)) return 0f;
            float n = LandShape.Fbm(x, z, 50f, 3, 2.2f);
            // Dense jungle on the edges and the slopes, open terraces and pueblo.
            float lEdge = Mathf.Clamp01((Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - 150f) / 50f);
            return Mathf.Clamp01(0.8f + n * 0.4f + lEdge * 0.4f);
        }

        protected override string TreeAt(float x, float z, float h, float pRoll, System.Random r, out float pScale)
        {
            pScale = 0.85f + (float)r.NextDouble() * 0.35f;
            bool lBanana = x > -150f && x < -75f && z > -105f && z < -30f;
            if (lBanana) return pRoll < 0.75f ? "Veg_Banana" : pRoll < 0.9f ? "Veg_JungleBush_B" : "Veg_TreeFern";
            bool lRidge = z > 85f && x > -45f && x < 60f && z < 175f;
            if (lRidge) return pRoll < 0.35f ? "Veg_Fir_Medium" : pRoll < 0.55f ? "Veg_JungleTree_B" : pRoll < 0.8f ? "Veg_JungleBush_A" : "Veg_Rock_B";
            bool lCafetal = z > -62f && z < 70f && x > TERRACE_X0 - 5f && x < 180f;
            if (lCafetal) return pRoll < 0.015f ? "Veg_JungleTree_B" : pRoll < 0.03f ? "Veg_Banana" : null;
            if (h < 4f && LandShape.PathDistance(RIVER, new Vector2(x, z), out _, out _) < 34f) return pRoll < 0.3f ? "Veg_CoconutPalm" : pRoll < 0.55f ? "Veg_JungleBush_B" : pRoll < 0.7f ? "Veg_TreeFern" : null;
            if (pRoll < 0.06f) { pScale = 0.8f + (float)r.NextDouble() * 0.3f; return "Veg_Ceiba"; }
            if (pRoll < 0.3f) return "Veg_JungleTree_A";
            if (pRoll < 0.45f) return "Veg_JungleTree_B";
            if (pRoll < 0.52f) return "Veg_RoyalPalm";
            if (pRoll < 0.62f) return "Veg_TreeFern";
            if (pRoll < 0.78f) return "Veg_JungleBush_B";
            if (pRoll < 0.9f) return "Veg_JungleBush_A";
            if (pRoll < 0.95f) { pScale = 2f + (float)r.NextDouble() * 3f; return (r.NextDouble() < 0.5 ? "Veg_Rock_A" : "Veg_Rock_B"); }
            return "Veg_Log";
        }

        protected override TerrainLayer[] Layers() => new[]
        {
            Tint(PbrLibrary.Layer("leafy_grass", 4f), new Color(0.72f, 0.95f, 0.62f)), PbrLibrary.Layer("grass_path_2", 5f), PbrLibrary.Layer("forest_leaves_02", 4f),
            PbrLibrary.Layer("brown_mud_leaves_01", 4f), PbrLibrary.Layer("rock_face", 8f), PbrLibrary.Layer("coast_sand_01", 5f),
            Tint(PbrLibrary.Layer("dirt", 5f), new Color(0.95f, 0.85f, 0.75f)), PbrLibrary.Layer("cobblestone_floor_01", 3f)
        };

        protected override void NaturalGround(float x, float z, float h, float pSlope, float[] w)
        {
            float n = LandShape.Fbm(x, z, 25f, 3, 1.7f);
            float lWoods = Woods(x, z, h);
            bool lCafetal = z > -62f && z < 70f && x > TERRACE_X0 && x < 180f;
            w[(int)Ground.Grass] = 1f + n;
            w[(int)Ground.DryGrass] = Mathf.Clamp01(0.3f - n) * 1.5f;
            w[(int)Ground.ForestFloor] = lCafetal ? 0.4f : Mathf.Clamp01(lWoods - 0.55f) * 4f;
            w[(int)Ground.Dirt] = lCafetal ? 1.2f + n : 0f;
            // Terrace banks and wild slopes stay green (grass and ferns), rock only where it is sheer.
            w[(int)Ground.Grass] += Mathf.Clamp01((pSlope - 15f) / 15f) * (lCafetal ? 8f : 3f);
            if (h < 0f) w[(int)Ground.Sand] += Mathf.Clamp01(-h) * 6f;
            if (h < -1.5f) w[(int)Ground.Rock] += 4f;
        }

        protected override void Details()
        {
            DetailPrototype lGrass = VegLibrary.Detail("Veg_Grass_Jungle", 0.8f, 1.3f, new Color(0.85f, 0.95f, 0.8f), new Color(0.95f, 0.9f, 0.7f));
            DetailPrototype lFern = VegLibrary.Detail("Veg_Fern", 0.8f, 1.4f, Color.white, new Color(0.9f, 0.95f, 0.85f));
            if (lGrass != null) K.Details.Add((lGrass, (x, z) => Built(x, z) || K.GroundAt(x, z) < WATER_Y + 0.4f || K.LandSlope(x, z) > 34f ? 0f : Mathf.Clamp01(0.5f + LandShape.Fbm(x, z, 20f, 2, 9.1f)), 6));
            if (lFern != null) K.Details.Add((lFern, (x, z) => Built(x, z) || K.GroundAt(x, z) < WATER_Y + 0.4f || K.LandSlope(x, z) > 38f ? 0f : Mathf.Clamp01(Woods(x, z, K.GroundAt(x, z)) - 0.6f + LandShape.Fbm(x, z, 14f, 2, 4.4f) * 0.5f) * 1.2f, 3));
            K.TallGrassDetail = VegLibrary.Detail("Veg_TallGrass_Jungle", 0.9f, 1.25f, Color.white, new Color(0.95f, 0.92f, 0.75f));
        }

        // ================================================================ plan

        private Street _drive, _ridge, _market, _busRoad;

        protected override void Plan()
        {
            // The pueblo's ground and plaza.
            Shape.Flat(Rect.MinMaxRect(-132f, -197f, 92f, -110f), PUEBLO_Y, 12f);
            Plaza(Rect.MinMaxRect(-52f, -162f, -8f, -124f), Ground.Cobble, PUEBLO_Y, 4f);
            // Coffee terraces.
            for (int i = 0; i < TERRACES.Length; i++) Shape.Flat(Rect.MinMaxRect(TERRACE_X0, TERRACES[i].x, TERRACE_X1[i], TERRACES[i].y), TERRACES[i].z, 4.5f);
            // The hill and the airstrip.
            Shape.Flat(Rect.MinMaxRect(COMPOUND.xMin - 4f, COMPOUND.yMin - 2f, COMPOUND.xMax + 4f, COMPOUND.yMax + 4f), PAD_Y, 7f);
            Shape.Flat(Rect.MinMaxRect(-212f, 116f, -34f, 182f), AIRSTRIP_Y, 14f);
            Shape.Disc(QUARRY, 15f, 7f, 7f);
            Shape.Flat(new Rect(CHAPEL_RUIN.x - 13f, CHAPEL_RUIN.y - 11f, 26f, 22f), Shape.Height(CHAPEL_RUIN.x, CHAPEL_RUIN.y), 6f);
            Shape.Flat(Rect.MinMaxRect(183f, -86f, 199f, -70f), -2.6f, 4f);
            Shape.Flat(Rect.MinMaxRect(-34f, 104f, -6f, 122f), Shape.Height(-20f, 113f), 6f);

            _market = Road(9f, Ground.Cobble, 30f, new Vector2(-8, -138), new Vector2(84, -138));
            Road(6f, Ground.Cobble, new Vector2(-30, -124), new Vector2(-32, -110));
            Road(5f, Ground.Cobble, new Vector2(-52, -150), new Vector2(-100, -152), new Vector2(-118, -150));
            Road(5f, Ground.Cobble, new Vector2(-30, -162), new Vector2(-30, -192));
            Road(4f, Ground.Cobble, new Vector2(20, -140), new Vector2(22, -170), new Vector2(10, -190));
            _drive = Road(6f, Ground.Asphalt, 40f, new Vector2(-96, -118), new Vector2(-78, -84), new Vector2(-66, -50), new Vector2(-62, -15), new Vector2(-60, 18), new Vector2(-56, 44), new Vector2(-40, 67), new Vector2(0, 68), new Vector2(40, 67.5f), new Vector2(75, 66.5f));
            _ridge = Road(4f, Ground.Asphalt, 30f, new Vector2(-48, 62), new Vector2(-36, 90), new Vector2(-20, 113), new Vector2(-40, 132), new Vector2(-62, 142));
            _busRoad = Road(7f, Ground.Asphalt, new Vector2(84, -136), new Vector2(118, -128), new Vector2(150, -116));
            // Runway and apron.
            Plaza(Rect.MinMaxRect(-205f, 141f, -45f, 163f), Ground.Asphalt, AIRSTRIP_Y, 6f);
            Plaza(Rect.MinMaxRect(-185f, 117f, -60f, 140f), Ground.Dirt, AIRSTRIP_Y, 6f);
            Plaza(new Rect(QUARRY.x - 12f, QUARRY.y - 12f, 24f, 24f), Ground.Rock);
            Clear.Add(Rect.MinMaxRect(COMPOUND.xMin - 3f, COMPOUND.yMin - 3f, COMPOUND.xMax + 3f, COMPOUND.yMax + 3f));
            Clear.Add(Rect.MinMaxRect(-135f, -198f, 92f, -108f));
            Plaza(Rect.MinMaxRect(-120f, -182f, 88f, -112f), Ground.Cobble);
            PlanPueblo();
            PlanCafetal();
            PlanHacienda();
        }

        // ================================================================ dress

        protected override void Dress()
        {
            ArchOutdoor.ResetCount();
            Water();
            DressPueblo();
            DressCafetal();
            DressAirstrip();
            DressHacienda();
            Caves();
            Matches();
        }

        /// <summary>A cluster of basalt columns (hexagonal prisms, 0.5 to 0.9 m) rising from the ground round a point.</summary>
        private void Basalt(Vector2 pCenter, float pRadius, int pCount, int pSeed)
        {
            System.Random r = new System.Random(pSeed);
            ArchMesh lMesh = new ArchMesh();
            ArchMat lRock = ArchLib.Tinted("rock_face", new Color(0.42f, 0.42f, 0.46f), "basalt", 2.5f);
            for (int i = 0; i < pCount; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)r.NextDouble()) * pRadius;
                Vector2 p = pCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (Built(p.x, p.y)) continue;
                float g = K.GroundAt(p.x, p.y);
                if (g < WATER_Y - 1f) continue;
                float lSlope = K.LandSlope(p.x, p.y);
                float lHeight = 2f + (float)r.NextDouble() * (lSlope > 40f ? 9f : 4f);
                lMesh.Cylinder(lRock, new Vector3(p.x, g - 2.5f, p.y), 0.5f + (float)r.NextDouble() * 0.4f, lHeight + 2.5f, 6);
            }
            GameObject lRoot = new GameObject("Basalt");
            lRoot.transform.SetParent(K.Env, false);
            lMesh.Flush(lRoot.transform, "SanLazaro_Basalt_" + pSeed);
        }

        private void Water()
        {
            River(WATER_Y, 22f, new Vector3(-0.6f, 0, 0), RIVER);
            K.Water(new Vector3(WATERFALL_POOL.x, WATER_Y, WATERFALL_POOL.y), new Vector2(34f, 34f));
            // The fall: a tilted sheet from the plateau's lip, the stream above it.
            // The fall: three stepped sheets of fast water over basalt ledges.
            Material lRiver = RiverMaterial();
            foreach ((Vector3 c, float w, float tilt) in new[] { (new Vector3(214f, 13f, -60f), 10f, -74f), (new Vector3(212.6f, 6f, -57.5f), 6f, -66f), (new Vector3(213.2f, 9f, -63f), 5f, -80f) })
            {
                GameObject lFall = K.Water(c, new Vector2(w, 36f));
                lFall.transform.rotation = Quaternion.Euler(tilt, -90f, 0f);
                if (lRiver != null) lFall.GetComponent<MeshRenderer>().sharedMaterial = lRiver;
                Object.DestroyImmediate(lFall.GetComponent<Systems.WaterVolume>());
                for (int i = lFall.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(lFall.transform.GetChild(i).gameObject);
            }
            // Basalt: hexagonal columns flank the fall and crown the north cliffs.
            Basalt(new Vector2(214f, -60f), 22f, 70, 1337);
            for (float x = -170f; x < 190f; x += 36f) Basalt(new Vector2(x, 192f), 14f, 22, (int)x);
            K.Water(new Vector3(232f, 30.4f, -54f), new Vector2(40f, 9f), new Vector3(-1.5f, 0, 0));
            Light lMist = new GameObject("WaterfallGlow").AddComponent<Light>();
            lMist.transform.SetParent(K.Lights, false);
            lMist.transform.position = new Vector3(205f, 2f, -62f);
            lMist.type = LightType.Point;
            lMist.range = 25f;
            lMist.intensity = 0.6f;
            lMist.color = new Color(0.7f, 0.9f, 1f);
            Poi(new Vector3(212f, 10f, -60f), "Waterfall", UI.MapPoi.Kind.Landmark);
        }
    }
}
