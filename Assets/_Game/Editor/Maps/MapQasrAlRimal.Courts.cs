using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapQasrAlRimal
    {
        /// <summary>
        /// The citadel courts stop being empty sand: canvas shades and tents, stalls, palms in planters
        /// and crate cover in the three courts; palms in irregular clusters; scrub on the open desert; the
        /// date press staged as the opportunity it is (lamp, warning sign, sacks of dates, the brazier).
        /// </summary>
        private void Courts()
        {
            ArchMat lCanvas = ArchLib.Tinted("rough_linen", new Color(0.92f, 0.85f, 0.7f), "tent_canvas", 1f), lStriped = ArchLib.Tinted("rough_linen", new Color(0.65f, 0.25f, 0.18f), "tent_red", 1f);
            ArchMat lPoles = ArchLib.Surface("rough_wood", 1.5f);
            Color[] lAwnings = { new Color(0.7f, 0.3f, 0.15f), new Color(0.2f, 0.35f, 0.55f), new Color(0.85f, 0.7f, 0.35f) };
            int k = 0;
            Rect[] lCourts = { Rect.MinMaxRect(-60f, 62f, 50f, 92f), Rect.MinMaxRect(16f, 98f, 46f, 162f), Rect.MinMaxRect(-62f, 98f, -24f, 162f) };
            for (int i = 0; i < lCourts.Length; i++)
            {
                Scatter(lCourts[i], 2, 3.2f, 801 + i, (g, y) => Shade(g, Mathf.Round(y / 90f) * 90f, 5f, 4f, 2.8f, (k++ & 1) == 0 ? lCanvas : lStriped, lPoles));
                Scatter(lCourts[i], 2, 1.8f, 811 + i, (g, y) => ArchOutdoor.Stall(A, g, Mathf.Round(y / 90f) * 90f, lAwnings[k++ % 3]));
                Scatter(lCourts[i], 3, 1.2f, 821 + i, (g, y) => PlanterTree(g, y, "Veg_DatePalm", 0.45f));
                ArchOutdoor.CoverClusters(A, lCourts[i], 11f, "default", 831 + i);
            }
            // Palms in clusters of two to four, never in rows.
            System.Random r = new System.Random(841);
            foreach (Vector2 c in new[] { new Vector2(-58f, 88f), new Vector2(-38f, 86f), new Vector2(24f, 88f), new Vector2(44f, 90f), new Vector2(12f, -150f), new Vector2(30f, -142f) })
            {
                int n = 2 + r.Next(3);
                for (int j = 0; j < n; j++)
                {
                    float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 1.2f + (float)r.NextDouble() * 3f;
                    float x = c.x + Mathf.Cos(a) * d, z = c.y + Mathf.Sin(a) * d;
                    if (!Built(x, z)) K.LandTree("Veg_DatePalm", x, z, 0.6f + (float)r.NextDouble() * 0.45f);
                }
            }
            // Scrub on the open desert.
            for (int j = 0; j < 900; j++)
            {
                float x = (float)r.NextDouble() * 460f - 230f, z = (float)r.NextDouble() * 460f - 230f;
                if (Built(x, z) || K.LandSlope(x, z) > 22f || K.GroundAt(x, z) < WaterLevel + 0.5f) continue;
                K.LandTree(r.NextDouble() < 0.5 ? "Veg_DesertShrub_A" : "Veg_DesertShrub_B", x, z, 0.6f + (float)r.NextDouble() * 0.7f);
            }
            // The date press, staged: a lamp over it, the warning, sacks of dates, a brazier for the night shift.
            Light lLamp = new GameObject("PressLamp").AddComponent<Light>();
            lLamp.transform.SetParent(K.Lights, false);
            lLamp.transform.position = _screwPress + new Vector3(0f, 5.6f, 0f);
            lLamp.type = LightType.Point; lLamp.range = 9f; lLamp.intensity = 2.2f; lLamp.color = new Color(1f, 0.75f, 0.45f); lLamp.shadows = LightShadows.Soft;
            ArchOutdoor.Sign(A, _screwPress + new Vector3(-2.2f, 0f, -2.2f), 180f, "Danger - Presse", true, new Color(0.85f, 0.55f, 0.05f));
            ArchMat lSack = ArchLib.Tinted("rough_linen", new Color(0.6f, 0.45f, 0.3f), "sack", 1f), lDates = ArchLib.PlainMat("Dates", new Color(0.35f, 0.12f, 0.06f), 0.5f, 0f);
            for (int j = 0; j < 4; j++)
                A.BoxObject("DateSacks", _screwPress + new Vector3(2.4f + (j % 2) * 0.7f, 0f, 0.6f + (j / 2) * 0.8f), j * 23f, K.Props, 0, true,
                    (lSack, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.45f)),
                    (lDates, new Vector3(0, 0.62f, 0), new Vector3(0.45f, 0.05f, 0.3f)));
            A.BoxObject("Brazier", _screwPress + new Vector3(-2.6f, 0f, 1.2f), 0f, K.Props, 0, true,
                (ArchLib.Iron, new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.7f, 0.6f)),
                (ArchLib.LampGlow, new Vector3(0, 0.72f, 0), new Vector3(0.45f, 0.06f, 0.45f)));
        }
    }
}
