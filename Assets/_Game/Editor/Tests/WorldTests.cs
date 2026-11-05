using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.World;
using NUnit.Framework;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>EditMode tests of the world's look: biome presets and grade, the grass meshes, the wind.</summary>
    public class WorldTests
    {
        [Test]
        public void Presets_BiomeIsRecognisedFromItsPresets()
        {
            foreach (TimeOfDay.Biome lBiome in System.Enum.GetValues(typeof(TimeOfDay.Biome)))
            {
                GameObject lObject = new GameObject("TimeOfDayTest");
                try
                {
                    TimeOfDay lTime = lObject.AddComponent<TimeOfDay>();
                    lTime.Setup(null, null, TimeOfDay.PresetsFor(lBiome, 37f));
                    Assert.AreEqual(lBiome, lTime.MapBiome, "maps built through the presets overload must keep their biome");
                }
                finally { Object.DestroyImmediate(lObject); }
            }
        }

        [Test]
        public void Presets_DawnDuskAndNightStayReadable()
        {
            foreach (TimeOfDay.Biome lBiome in System.Enum.GetValues(typeof(TimeOfDay.Biome)))
            {
                TimePreset[] p = TimeOfDay.PresetsFor(lBiome, 0f);
                Assert.AreEqual(4, p.Length, lBiome + ": dawn, noon, dusk, night");
                // The flat look exposes every hour alike (TimeOfDay.Stylise): the night stays readable by its colours.
                Assert.GreaterOrEqual(p[3].exposure, p[1].exposure, lBiome + ": the night is never darker exposed than noon");
                Assert.Less(p[3].saturation, p[1].saturation, lBiome + ": the night is less colourful than noon");
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(p[i].contrast, Is.InRange(-20f, 20f), lBiome + " " + i + ": HDR grade keeps a calm contrast");
                    Assert.That(p[i].saturation, Is.InRange(-30f, 20f), lBiome + " " + i + ": natural saturation");
                    Assert.Greater(p[i].sunIntensity, 0f, lBiome + " " + i + ": a sun or a moon");
                    Assert.Greater(p[i].wind, 0f, lBiome + " " + i + ": some wind");
                }
            }
        }

        [Test]
        public void GrassTuft_FirstBladesSurroundTheTuft()
        {
            Mesh lTuft = GrassMeshes.Tuft();
            try
            {
                System.Collections.Generic.List<Vector4> lBlade = new System.Collections.Generic.List<Vector4>(), lPlace = new System.Collections.Generic.List<Vector4>();
                lTuft.GetUVs(0, lBlade);
                lTuft.GetUVs(1, lPlace);
                // The four lowest-ranked blades (what a thinned tuft keeps) must lie on every side of it.
                bool[] lQuadrant = new bool[4];
                System.Collections.Generic.HashSet<float> lRanks = new System.Collections.Generic.HashSet<float>();
                for (int v = 0; v < lBlade.Count; v++)
                {
                    lRanks.Add(lBlade[v].z);
                    if (lBlade[v].z > 0.4f) continue;
                    float a = Mathf.Atan2(lPlace[v].y, lPlace[v].x);
                    if (new Vector2(lPlace[v].x, lPlace[v].y).magnitude < 0.2f) continue;
                    lQuadrant[Mathf.Clamp(Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 0.5f)), 0, 3)] = true;
                }
                Assert.AreEqual(10, lRanks.Count, "ten blades, ten ranks");
                int lSides = 0;
                foreach (bool b in lQuadrant) if (b) lSides++;
                Assert.GreaterOrEqual(lSides, 3, "a tuft thinned to four blades still covers its ground");
            }
            finally { Object.DestroyImmediate(lTuft); }
        }

        [Test]
        public void Wind_DirectionIsNormalisedAndGustinessClamped()
        {
            Vector2 lDirection = Wind.Direction;
            float lStrength = Wind.Strength, lGust = Wind.Gustiness;
            try
            {
                Wind.Set(new Vector2(3f, 4f), 0.5f, 2f);
                Assert.AreEqual(1f, Wind.Direction.magnitude, 1e-4f);
                Assert.AreEqual(1f, Wind.Gustiness, 1e-6f);
                Wind.Set(Vector2.zero, -1f, 0.5f);
                Assert.AreEqual(1f, Wind.Direction.magnitude, 1e-4f, "a zero direction keeps the last one");
                Assert.AreEqual(0f, Wind.Strength, 1e-6f);
            }
            finally { Wind.Set(lDirection, lStrength, lGust); }
        }
    }
}
