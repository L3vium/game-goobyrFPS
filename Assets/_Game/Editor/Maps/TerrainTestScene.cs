using Com.IsartDigital.GoobyrFPS.Data;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// A 500 m test valley for the land tools: ridged hills, a terraced slope, a river, a road
    /// that climbs, a plateau with a few houses, dense forest and grass. Saved as
    /// <c>Scenes/TerrainTest.unity</c> (not in the build).
    /// </summary>
    public static class TerrainTestScene
    {
        [MenuItem("Goobyr/Build/Terrain Test")]
        public static void Build()
        {
            MapDefinition lMap = ScriptableObject.CreateInstance<MapDefinition>();
            EditorUtility.CopySerialized(GameDatabase.Instance.maps[0], lMap);
            lMap.sceneName = "TerrainTest";
            MapKit K = new MapKit(lMap, 3);

            Vector2[] lRiver = { new Vector2(-260, -120), new Vector2(-120, -60), new Vector2(0, -90), new Vector2(120, -40), new Vector2(260, -70) };
            Vector2[] lRoad = { new Vector2(-200, -200), new Vector2(-150, -110), new Vector2(-60, -30), new Vector2(20, 40), new Vector2(60, 110), new Vector2(40, 170) };
            LandShape lShape = new LandShape((x, z) =>
                LandShape.Fbm(x, z, 180f, 5, 3.1f) * 14f + LandShape.Ridged(x, z, 140f, 4, 8.2f) * 34f * Mathf.Clamp01((z + 40f) / 160f) + 6f);
            lShape.Carve(lRiver, 10f, 5f, 14f)
                .Terraces(Rect.MinMaxRect(-240, 60, -80, 220), 4f, 0.25f, 20f)
                .Flat(Rect.MinMaxRect(20, 130, 90, 190), 32f, 18f)
                .Road(lRoad, 8f, 6f, 40f);
            K.BuildLand(500f, -20f, 80f, lShape.Height);

            K.PaintLand((x, z, h, s) =>
            {
                float[] w = new float[8];
                float n = LandShape.Fbm(x, z, 30f, 3, 1.7f);
                w[(int)Ground.Grass] = 1f + n;
                w[(int)Ground.DryGrass] = Mathf.Clamp01(h / 40f) * (0.6f - n);
                w[(int)Ground.ForestFloor] = Mathf.Clamp01(LandShape.Fbm(x, z, 60f, 2, 4.4f) * 2f);
                w[(int)Ground.Rock] = Mathf.Clamp01((s - 30f) / 10f) * 6f;
                w[(int)Ground.Dirt] = Mathf.Clamp01((s - 18f) / 12f) * 1.5f;
                float lRiverDistance = LandShape.PathDistance(lRiver, new Vector2(x, z), out _, out _);
                w[(int)Ground.Sand] = Mathf.Clamp01(1f - (lRiverDistance - 6f) / 6f) * 6f;
                float lRoadDistance = LandShape.PathDistance(lRoad, new Vector2(x, z), out _, out _);
                w[(int)Ground.Asphalt] = lRoadDistance < 4f ? 20f : 0f;
                w[(int)Ground.Dirt] += lRoadDistance < 6f && lRoadDistance >= 4f ? 4f : 0f;
                return w;
            });

            // The plateau: a few houses around a square.
            for (int i = 0; i < 4; i++)
            {
                MapKit.KitSpec lSpec = new MapKit.KitSpec
                {
                    name = "House" + i, style = i % 2 == 0 ? "Timber" : "Suburb", origin = new Vector3(30 + (i % 2) * 34, 32.05f, 140 + (i / 2) * 30), width = 4, depth = 3, floors = 2,
                    roof = MapKit.Roof.Gable, wall = new[] { new Color(0.95f, 0.9f, 0.8f), new Color(0.7f, 0.8f, 0.9f) }[i % 2], seed = 20 + i, navigable = true
                };
                lSpec.doors.Add(new Vector2Int(0, 1));
                K.Building(lSpec);
            }

            // Forest: dense where the noise says woods, never on the road, river, plateau or cliffs.
            System.Random lRandom = new System.Random(5);
            string[] lConifers = { "PineA", "PineB", "PineC" }, lBroad = { "BroadleafA", "BroadleafC", "BirchA" };
            for (float x = -246f; x < 246f; x += 4.2f)
                for (float z = -246f; z < 246f; z += 4.2f)
                {
                    float px = x + (float)lRandom.NextDouble() * 3f, pz = z + (float)lRandom.NextDouble() * 3f;
                    if (LandShape.PathDistance(lRoad, new Vector2(px, pz), out _, out _) < 9f || LandShape.PathDistance(lRiver, new Vector2(px, pz), out _, out _) < 11f) continue;
                    if (px > 10f && px < 100f && pz > 120f && pz < 200f) continue;
                    if (K.LandSlope(px, pz) > 38f) continue;
                    float lWoods = LandShape.Fbm(px, pz, 70f, 3, 2.2f);
                    if (lWoods < -0.05f) continue;
                    string lPiece = K.GroundAt(px, pz) > 25f ? lConifers[lRandom.Next(3)] : (lRandom.NextDouble() < 0.6 ? lConifers[lRandom.Next(3)] : lBroad[lRandom.Next(3)]);
                    K.LandTree(lPiece, px, pz, 0.8f + (float)lRandom.NextDouble() * 0.5f);
                    if (lRandom.NextDouble() < 0.3) K.LandTree(lRandom.NextDouble() < 0.5 ? "BushA" : "BushB", px + 1.6f, pz - 1.2f, 0.8f + (float)lRandom.NextDouble() * 0.6f);
                }
            K.LandGrass((x, z) => LandShape.PathDistance(lRoad, new Vector2(x, z), out _, out _) < 6f || K.LandSlope(x, z) > 30f ? 0f : Mathf.Clamp01(0.4f + LandShape.Fbm(x, z, 25f, 2, 9.1f)));

            K.Point(AI.MatchPoint.Kind.Start, K.At(-200, -200, 0.2f), 0, "Test");
            K.Finish();
            Debug.Log("[Build] terrain test scene");
        }
    }
}
