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
    /// <b>Weisshorn</b> - an alpine gala night in the snow (docs/maps/Weisshorn.md).
    /// <list type="bullet">
    /// <item><b>Sankt Anna</b> (zone 0, 0 m): chalet street, rink, church and spire, Gasthaus and its
    /// cellar, ski rental, the funicular's valley station, the frozen lake and the boathouse.</item>
    /// <item><b>Ski area</b> (zone 1, +5 to +40): pistes between pine bands, the chairlift, the
    /// mid-station cafe, the groomer garage and the mine, the ski-jump; the observatory and its bunker
    /// on the summit (+70).</item>
    /// <item><b>Grand Hotel Weisshorn</b> (zone 2, +45): terraces, delivery yard, spa garden, the hotel.</item>
    /// </list>
    /// </summary>
    public partial class MapWeisshorn : RealMap
    {
        public const int ZONE_VILLAGE = 0, ZONE_SKI = 1, ZONE_HOTEL = 2;
        private const float VILLAGE_Y = 0.5f, PLATFORM_Y = 45f, SUMMIT_Y = 70f;
        private static readonly Vector3 HOTEL = new Vector3(-35f, PLATFORM_Y, 82f);
        private static readonly Rect LAKE = Rect.MinMaxRect(-240f, -245f, -120f, -165f);
        private static readonly Vector2[] TOWER_VIEW = { new Vector2(-20f, -118f), new Vector2(-11f, 98f) };
        private static readonly Vector2 SKIJUMP = new Vector2(-95f, 10f), GARAGE = new Vector2(36f, -26f), MIDSTATION = new Vector2(10f, -10f), OBSERVATORY = new Vector2(45f, 172f), LIFT_BOTTOM = new Vector2(70f, -100f), LIFT_TOP = new Vector2(62f, 70f);

        protected override string MapFile => "Map_Weisshorn";
        protected override TimeOfDay.Biome Biome => TimeOfDay.Biome.Snow;
        protected override float SunHeading => 40f;
        protected override float MinY => -10f;
        protected override float MaxY => 140f;
        protected override float WaterLevel => -0.2f;

        [MenuItem("Goobyr/Build/Map Weisshorn")]
        public static void BuildMenu() => new MapWeisshorn().Build();

        protected override float Relief(float x, float z)
        {
            float h;
            if (z < -110f) h = VILLAGE_Y;
            else if (z < 72f) h = Mathf.Lerp(VILLAGE_Y, 42f, Mathf.SmoothStep(0f, 1f, (z + 110f) / 182f));
            else if (z < 140f) h = Mathf.Lerp(46f, 62f, (z - 72f) / 68f);
            else h = Mathf.Lerp(62f, 74f, Mathf.Clamp01((z - 140f) / 40f));
            h += LandShape.Fbm(x, z, 70f, 4, 5.5f) * 3f;
            // Glacier and summit north, avalanche cliffs east, the pine wall west.
            h += Mathf.Max(0f, z - 190f) * 2.2f + LandShape.Ridged(x, z, 45f, 3, 2.5f) * Mathf.Clamp01((z - 190f) / 20f) * 20f;
            h += Mathf.Max(0f, x - 200f) * 3f + LandShape.Ridged(x, z, 30f, 3, 8f) * Mathf.Clamp01((x - 200f) / 12f) * 18f;
            h += Mathf.Max(0f, -205f - x) * 1.2f;
            // The crevasse behind the hotel: a sharp drop then the ice wall.
            float lCrevasse = Mathf.Abs(z - 128f);
            if (lCrevasse < 6f && x > -60f && x < 30f) h -= (6f - lCrevasse) * 1.4f;
            // The frozen lake basin.
            float lLake = LandShape.RectDistance(LAKE, x, z);
            if (lLake < 18f) h = Mathf.Lerp(-2.5f, h, Mathf.SmoothStep(0f, 1f, lLake / 18f));
            return h;
        }

        protected override float Woods(float x, float z, float h)
        {
            if (h < 0.5f) return 0f;
            // Pine bands across the slope, open pistes between them.
            bool lPiste = z > -100f && z < 70f && (Mathf.Abs(x - 20f + z * 0.15f) < 22f || Mathf.Abs(x + 45f - z * 0.1f) < 16f || Mathf.Abs(x - 75f) < 12f);
            if (lPiste) return 0.02f;
            // The view up the church street to the clock tower stays open.
            if (LandShape.PathDistance(TOWER_VIEW, new Vector2(x, z), out _, out _) < 10f) return 0f;
            if (z > 185f) return 0.05f;
            float n = LandShape.Fbm(x, z, 40f, 3, 2.2f);
            return Mathf.Clamp01(0.75f + n * 0.5f + Mathf.Clamp01((Mathf.Abs(x) - 140f) / 40f));
        }

        protected override string TreeAt(float x, float z, float h, float pRoll, System.Random r, out float pScale)
        {
            pScale = 0.8f + (float)r.NextDouble() * 0.4f;
            if (h > 85f) return pRoll < 0.6f ? (r.NextDouble() < 0.5 ? "Veg_Rock_A" : "Veg_Rock_C") : null;
            if (pRoll < 0.3f) return "Veg_Fir_Snowy_Large";
            if (pRoll < 0.55f) return "Veg_Fir_Snowy_Medium";
            if (pRoll < 0.7f) return "Veg_Fir_Medium";
            if (pRoll < 0.78f) return "Veg_Fir_Small";
            if (pRoll < 0.84f) return "Veg_Birch";
            if (pRoll < 0.93f) return "Veg_SnowBush";
            pScale = 2f + (float)r.NextDouble() * 3f;
            return r.NextDouble() < 0.5 ? "Veg_Rock_B" : "Veg_Rock_A";
        }

        protected override TerrainLayer[] Layers() => new[]
        {
            PbrLibrary.Layer("snow_02", 4f), PbrLibrary.Layer("snow_field_aerial", 8f), PbrLibrary.Layer("forest_ground_04", 4f),
            PbrLibrary.Layer("gravel_road", 4f), PbrLibrary.Layer("rock_face", 8f), PbrLibrary.Layer("rocky_terrain_02", 6f),
            PbrLibrary.Layer("asphalt_02", 5f), PbrLibrary.Layer("cobblestone_floor_01", 3f)
        };

        protected override float RockSlope => 47f;

        protected override void NaturalGround(float x, float z, float h, float pSlope, float[] w)
        {
            float n = LandShape.Fbm(x, z, 25f, 3, 1.7f);
            w[(int)Ground.Grass] = 2f + n;
            w[(int)Ground.DryGrass] = Mathf.Clamp01(0.3f - n) * 2f + Mathf.Clamp01((h - 70f) / 10f) * 3f;
            w[(int)Ground.ForestFloor] = Mathf.Clamp01(Woods(x, z, h) - 0.7f) * 1.5f;
            w[(int)Ground.Sand] = Mathf.Clamp01((pSlope - 40f) / 10f) * 2f;
        }

        protected override void Details()
        {
            DetailPrototype lGrass = VegLibrary.Detail("Veg_Grass_Alpine", 0.7f, 1.1f, new Color(0.95f, 0.97f, 1f), new Color(0.9f, 0.9f, 0.85f));
            if (lGrass != null) K.Details.Add((lGrass, (x, z) => Built(x, z) || K.LandSlope(x, z) > 30f || K.GroundAt(x, z) < 0.3f ? 0f : Mathf.Clamp01(LandShape.Fbm(x, z, 14f, 2, 6f) - 0.1f) * 0.6f, 4));
            K.TallGrassDetail = VegLibrary.Detail("Veg_TallGrass_Alpine", 0.9f, 1.25f, Color.white, new Color(0.9f, 0.88f, 0.8f));
        }

        // ================================================================ plan

        private ArchSpec _hotel, _church, _gasthaus, _valley, _rental, _boathouse, _midStation, _liftBottom, _liftTop, _garage, _mineHut, _observatory, _antenna, _cableTop, _skiSchool, _ticketHut;
        private ArchResult _hotelR, _churchR, _gasthausR, _valleyR, _rentalR, _boathouseR, _midR, _liftBottomR, _liftTopR, _garageR, _mineHutR, _observatoryR, _antennaR, _cableTopR;
        private readonly List<(ArchSpec spec, int zone)> _village = new List<(ArchSpec, int)>();
        private readonly List<ArchResult> _chalets = new List<ArchResult>();
        private int _chaletCount;

        protected override void Plan()
        {
            Shape.Flat(Rect.MinMaxRect(-120f, -200f, 110f, -112f), VILLAGE_Y, 10f);
            // Hotel platform: lower terrace (L0), the hotel pad, the delivery yard (L1), spa garden and front terrace (L2).
            Shape.Flat(Rect.MinMaxRect(-45f, 60f, 45f, 112f), PLATFORM_Y, 6f);
            Shape.Flat(Rect.MinMaxRect(-7f, 112f, 50f, 124f), PLATFORM_Y + 4f, 3f);
            Shape.Flat(Rect.MinMaxRect(-62f, 80f, -36f, 112f), PLATFORM_Y + 8f, 4f);
            Shape.Flat(new Rect(OBSERVATORY.x - 30f, OBSERVATORY.y - 22f, 60f, 44f), SUMMIT_Y, 6f);
            Shape.Flat(new Rect(GARAGE.x - 12f, GARAGE.y - 10f, 24f, 20f), Shape.Height(GARAGE.x, GARAGE.y), 5f);
            Shape.Flat(new Rect(MIDSTATION.x - 10f, MIDSTATION.y - 8f, 20f, 16f), Shape.Height(MIDSTATION.x, MIDSTATION.y), 5f);
            Shape.Flat(new Rect(SKIJUMP.x - 8f, SKIJUMP.y - 8f, 16f, 16f), Shape.Height(SKIJUMP.x, SKIJUMP.y), 4f);
            Shape.Flat(new Rect(LIFT_BOTTOM.x - 8f, LIFT_BOTTOM.y - 8f, 16f, 16f), Shape.Height(LIFT_BOTTOM.x, LIFT_BOTTOM.y), 4f);
            Shape.Flat(new Rect(LIFT_TOP.x - 8f, LIFT_TOP.y - 8f, 16f, 16f), PLATFORM_Y + 8f, 4f);
            // Chalet street, the service road switchbacks to the delivery yard, footpaths, the ridge path.
            Road(9f, Ground.Cobble, new Vector2(-110, -150), new Vector2(110, -150));
            Road(6f, Ground.Cobble, new Vector2(-20, -150), new Vector2(-20, -118));
            Road(6f, Ground.Asphalt, 40f, new Vector2(110, -150), new Vector2(140, -120), new Vector2(150, -60), new Vector2(125, -20), new Vector2(150, 20), new Vector2(120, 60), new Vector2(80, 100), new Vector2(50, 118));
            Road(3f, Ground.Dirt, 20f, new Vector2(-30, -112), new Vector2(-50, -60), new Vector2(-60, -10), new Vector2(-70, 40), new Vector2(-62, 80));
            Road(3f, Ground.Dirt, 20f, new Vector2(-30, 120), new Vector2(-20, 150), new Vector2(10, 168), new Vector2(OBSERVATORY.x - 20f, OBSERVATORY.y));
            Road(3f, Ground.Dirt, 20f, new Vector2(MIDSTATION.x, MIDSTATION.y), new Vector2(GARAGE.x, GARAGE.y), new Vector2(80, -30), new Vector2(125, -20));
            Clear.Add(Rect.MinMaxRect(-120f, -200f, 110f, -112f));
            Clear.Add(Rect.MinMaxRect(-62f, 60f, 50f, 124f));
            PlanVillage();
            PlanSki();
            _hotel = HotelSpec(HOTEL);
        }

        protected override void Dress()
        {
            ArchOutdoor.ResetCount();
            Lake();
            DressVillage();
            DressSki();
            DressHotel();
            Tunnels();
            Snow();
            Matches();
        }

        private void Lake()
        {
            // Frozen lake: walkable ice, an ice hole (disposal) by the boathouse.
            ArchMesh lIce = new ArchMesh();
            ArchMat lIceMat = ArchLib.PlainMat("LakeIce", new Color(0.75f, 0.86f, 0.92f), 0.88f, 0f, 4f);
            lIce.BoxMinMax(lIceMat, new Vector3(LAKE.xMin - 6f, -0.6f, LAKE.yMin - 6f), new Vector3(LAKE.xMax + 6f, -0.2f, LAKE.yMax + 6f));
            GameObject lIceRoot = new GameObject("FrozenLake");
            lIceRoot.transform.SetParent(K.Env, false);
            lIce.Flush(lIceRoot.transform, "Weisshorn_Ice");
            Poi(new Vector3(LAKE.center.x, 0f, LAKE.center.y), "Frozen lake", MapPoi.Kind.Landmark);
        }

        /// <summary>The snow falling round the camera (a small particle box that follows it).</summary>
        private void Snow()
        {
            GameObject lSnow = new GameObject("SnowFall");
            lSnow.transform.SetParent(K.Lights, false);
            ParticleSystem lParticles = lSnow.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule lMain = lParticles.main;
            lMain.startLifetime = 7f;
            lMain.startSpeed = 0f;
            lMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            lMain.maxParticles = 4000;
            lMain.simulationSpace = ParticleSystemSimulationSpace.World;
            lMain.gravityModifier = 0.05f;
            lMain.startColor = new Color(1f, 1f, 1f, 0.9f);
            ParticleSystem.EmissionModule lEmission = lParticles.emission;
            lEmission.rateOverTime = 600f;
            ParticleSystem.ShapeModule lShape = lParticles.shape;
            lShape.shapeType = ParticleSystemShapeType.Box;
            lShape.scale = new Vector3(40f, 1f, 40f);
            ParticleSystem.VelocityOverLifetimeModule lVelocity = lParticles.velocityOverLifetime;
            lVelocity.enabled = true;
            lVelocity.space = ParticleSystemSimulationSpace.World;
            lVelocity.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.6f);
            lVelocity.y = new ParticleSystem.MinMaxCurve(-1.4f, -0.9f);
            lVelocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            ParticleSystem.NoiseModule lNoise = lParticles.noise;
            lNoise.enabled = true;
            lNoise.strength = 0.4f;
            lNoise.frequency = 0.3f;
            ParticleSystemRenderer lRenderer = lSnow.GetComponent<ParticleSystemRenderer>();
            Material lFlake = ArchLib.Plain("SnowFlake", Color.white, 0f, 0f);
            lFlake.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? lFlake.shader;
            lFlake.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f));
            lRenderer.sharedMaterial = lFlake;
            lRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            lSnow.AddComponent<SnowFall>();
        }
    }
}
