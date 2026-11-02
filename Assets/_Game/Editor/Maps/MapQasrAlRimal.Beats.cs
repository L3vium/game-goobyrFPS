using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapQasrAlRimal
    {
        /// <summary>
        /// Patrol beats: a loop on every floor of the keep (the cellar ring through the vault and the
        /// armoury, the ground floor round the inner court, the three light-well galleries, the roof), the
        /// three courts, pairs in the souk, round the well plaza and along the bus road, and the grove.
        /// </summary>
        private void Beats()
        {
            const int T = ZONE_TOWN, O = ZONE_GROVE, C = ZONE_CITADEL;
            ArchResult b = _keepR;
            Loop(b, "Cellar", C, 1, ItemKind.Smg, 4f, new Vector3(8.5f, -1, 26f), new Vector3(5f, -1, 17f), new Vector3(5f, -1, 5f), new Vector3(17f, -1, 5f),
                new Vector3(28f, -1, 5f), new Vector3(28f, -1, 17f), new Vector3(28f, -1, 28f), new Vector3(14f, -1, 26.5f));
            Loop(b, "Ground", C, 2, ItemKind.Smg, 4f, new Vector3(17f, 0, 6f), new Vector3(6f, 0, 6f), new Vector3(6f, 0, 20f), new Vector3(6f, 0, 28f),
                new Vector3(14f, 0, 28f), new Vector3(28f, 0, 28f), new Vector3(28f, 0, 17f), new Vector3(17f, 0, 16f));
            for (int f = 1; f <= 3; f++)
                Loop(b, "WellGallery" + f, C, 1, f == 1 ? ItemKind.Pistol : ItemKind.Smg, 4f + f, new Vector3(12.2f, f, 12.2f), new Vector3(17f, f, 12.2f), new Vector3(21.8f, f, 12.2f),
                    new Vector3(21.8f, f, 17f), new Vector3(21.8f, f, 21.8f), new Vector3(17f, f, 21.8f), new Vector3(12.2f, f, 21.8f), new Vector3(12.2f, f, 17f));
            Beat("KeepRoof", C, 1, ItemKind.Smg, 6f, Kp(3f, 4, 3f), Kp(17f, 4, 3f), Kp(31f, 4, 3f), Kp(31f, 4, 15f), Kp(31f, 4, 31f), Kp(17f, 4, 29.5f), Kp(8f, 4, 24f), Kp(9f, 4, 12f));

            Beat("FirstCourt", C, 2, ItemKind.Smg, 4f, G(-60f, 66f), G(-30f, 66f), G(0f, 66f), G(40f, 66f), G(50f, 85f), G(20f, 90f), G(-20f, 90f), G(-60f, 85f));
            Beat("SecondCourt", C, 2, ItemKind.Smg, 4f, G(20f, 100f), G(40f, 100f), G(44f, 112f), G(44f, 140f), G(30f, 160f), G(18f, 150f), G(18f, 125f));
            Beat("ThirdCourt", C, 2, ItemKind.Smg, 4f, G(-60f, 100f), G(-40f, 100f), G(-26f, 110f), G(-26f, 135f), G(-40f, 160f), G(-60f, 160f), G(-55f, 130f));

            Beat("Souk", T, 2, ItemKind.Pistol, 5f, G(2f, -132f), G(2f, -120f), G(2f, -108f), G(20f, -104f), G(40f, -108f), G(40f, -120f), G(40f, -132f), G(20f, -146f));
            Beat("WellPlaza", T, 2, ItemKind.Pistol, 5f, G(4f, -164f), G(20f, -166f), G(36f, -164f), G(36f, -150f), G(36f, -136f), G(20f, -134f), G(4f, -136f), G(4f, -150f));
            Beat("BusRoad", T, 2, ItemKind.Smg, 6f, G(42f, -150f), G(70f, -150f), G(100f, -150f), G(120f, -152f), G(126f, -170f), G(110f, -155f), G(80f, -154f), G(50f, -154f));
            Beat("Grove", O, 2, ItemKind.Smg, 5f, G(-60f, -60f), G(-40f, -70f), G(-20f, -60f), G(-10f, -40f), G(-30f, -30f), G(-55f, -40f));
        }

        /// <summary>Where fleeing civilians leave the map: the bus station, the roads, the dunes, the canyon.</summary>
        private void Exits()
        {
            ExitAt(G(178f, -214f, 0.1f), "Bus station");
            ExitAt(G(-232f, -122f, 0.1f), "Caravan road");
            ExitAt(G(-40f, -222f, 0.1f), "Dune edge");
            ExitAt(G(-170f, -14f, 0.1f), "Grove ford");
            ExitAt(G(192f, 40f, 0.1f), "Canyon trail");
            ExitAt(G(126f, -192f, 0.1f), "Bus");
        }
    }
}
