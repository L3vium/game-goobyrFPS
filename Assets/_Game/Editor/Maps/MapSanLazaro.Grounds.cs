using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapSanLazaro
    {
        /// <summary>
        /// The casa's grounds and the plaza stop being empty flats: a hedged gravel drive from the frisk
        /// gate to the portico, a fountain on the west lawn, flower beds and garden cover by the walls;
        /// on the plaza, market stalls, benches, trees in planters and a bandstand round the fiesta.
        /// </summary>
        private void Grounds()
        {
            // Casa: hedges along the drive (gravel painted in Plan), the fountain, beds and garden cover.
            Line(new Vector2(80f, 79f), new Vector2(104f, 92f), 3.4f, 1.0f, (g, y) => ArchOutdoor.Hedge(A, g + Quaternion.Euler(0, y, 0) * new Vector3(0, 0, 2.6f), y));
            Line(new Vector2(80f, 79f), new Vector2(104f, 92f), 3.4f, 1.0f, (g, y) => ArchOutdoor.Hedge(A, g + Quaternion.Euler(0, y, 0) * new Vector3(0, 0, -2.6f), y));
            Scatter(Rect.MinMaxRect(66f, 98f, 84f, 126f), 1, 3.2f, 501, (g, y) => ArchOutdoor.Fountain(A, g, 2.2f));
            Scatter(Rect.MinMaxRect(64f, 96f, 86f, 140f), 6, 1.4f, 502, (g, y) => ArchOutdoor.Planter(A, g, Mathf.Round(y / 90f) * 90f));
            Scatter(Rect.MinMaxRect(64f, 96f, 86f, 140f), 4, 1.2f, 503, (g, y) => PlanterTree(g, y, "Veg_RoyalPalm", 0.45f));
            Scatter(Rect.MinMaxRect(130f, 72f, 156f, 116f), 5, 1.4f, 504, (g, y) => ArchOutdoor.Hedge(A, g, Mathf.Round(y / 90f) * 90f));
            Scatter(Rect.MinMaxRect(64f, 96f, 156f, 140f), 6, 1.0f, 505, (g, y) => Bench(g, y));
            ArchOutdoor.CoverClusters(A, Rect.MinMaxRect(64f, 98f, 86f, 140f), 10f, "default", 506);
            ArchOutdoor.CoverClusters(A, Rect.MinMaxRect(130f, 72f, 156f, 116f), 10f, "default", 507);

            // Plaza: stalls round the edges, benches, trees in planters, a bandstand.
            Rect lPlaza = Rect.MinMaxRect(-52f, -162f, -8f, -124f);
            Color[] lAwnings = { new Color(0.85f, 0.2f, 0.15f), new Color(0.95f, 0.75f, 0.15f), new Color(0.15f, 0.45f, 0.7f), new Color(0.2f, 0.55f, 0.3f) };
            int k = 0;
            Scatter(Rect.MinMaxRect(lPlaza.xMin + 1f, lPlaza.yMin + 1f, lPlaza.xMax - 1f, lPlaza.yMin + 6f), 3, 1.8f, 511, (g, y) => ArchOutdoor.Stall(A, g, 0f, lAwnings[k++ % 4]));
            Scatter(Rect.MinMaxRect(lPlaza.xMin + 1f, lPlaza.yMax - 6f, lPlaza.xMax - 1f, lPlaza.yMax - 1f), 3, 1.8f, 512, (g, y) => ArchOutdoor.Stall(A, g, 180f, lAwnings[k++ % 4]));
            Scatter(lPlaza, 1, 3.6f, 513, (g, y) => Shade(g, 0f, 6f, 6f, 3.2f, ArchLib.Surface("clay_roof_tiles_02", 2f), ArchLib.Surface("painted_plaster_wall", 2f)));
            Scatter(lPlaza, 8, 1.0f, 514, (g, y) => Bench(g, Mathf.Round(y / 90f) * 90f));
            Scatter(lPlaza, 6, 1.2f, 515, (g, y) => PlanterTree(g, y, "Veg_Ceiba", 0.3f));
        }
    }
}
