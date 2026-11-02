using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapSanLazaro
    {
        /// <summary>
        /// Patrol beats: a loop on every floor of Casa La Cumbre (the cellar ring through the wine, cold and
        /// coal stores, the portico, the first-floor and upper galleries round the patio), pairs round the
        /// plaza, along the market street and down to the dock, and round the beneficio's drying patio.
        /// </summary>
        private void Beats()
        {
            const int P = ZONE_PUEBLO, F = ZONE_CAFETAL, Z = ZONE_HACIENDA;
            ArchResult b = _casaR;
            Loop(b, "Cellar", Z, 1, ItemKind.Smg, 4f, new Vector3(11f, -1, 21f), new Vector3(29f, -1, 21f), new Vector3(35f, -1, 21f), new Vector3(35f, -1, 28f),
                new Vector3(25f, -1, 28f), new Vector3(15f, -1, 28f), new Vector3(5f, -1, 28f), new Vector3(5f, -1, 20f));
            Loop(b, "Portico", Z, 2, ItemKind.Smg, 4f, new Vector3(11.2f, 0, 7.2f), new Vector3(20f, 0, 7.2f), new Vector3(28.8f, 0, 7.2f), new Vector3(28.8f, 0, 15f),
                new Vector3(28.8f, 0, 22.8f), new Vector3(20f, 0, 22.8f), new Vector3(11.2f, 0, 22.8f), new Vector3(11.2f, 0, 15f));
            Loop(b, "UpperGallery", Z, 1, ItemKind.Smg, 5f, new Vector3(11.2f, 2, 7.2f), new Vector3(20f, 2, 7.2f), new Vector3(28.8f, 2, 7.2f), new Vector3(28.8f, 2, 15f),
                new Vector3(28.8f, 2, 22.8f), new Vector3(20f, 2, 22.8f), new Vector3(11.2f, 2, 22.8f), new Vector3(11.2f, 2, 15f));

            Beat("Plaza", P, 2, ItemKind.Pistol, 5f, G(-44f, -156f), G(-30f, -162f), G(-10f, -158f), G(-8f, -140f), G(-10f, -126f), G(-30f, -124f), G(-50f, -126f), G(-52f, -145f));
            Beat("MarketStreet", P, 2, ItemKind.Pistol, 5f, G(-6f, -140f), G(20f, -140f), G(45f, -140f), G(76f, -140f), G(76f, -136f), G(45f, -136f), G(20f, -136f), G(-6f, -136f));
            Beat("RiverRoad", P, 2, ItemKind.Smg, 6f, G(-30f, -165f), G(-30f, -178f), G(-30f, -190f), G(-15f, -192f), G(0f, -190f), G(-5f, -178f), G(-15f, -168f));
            Beat("Beneficio", F, 2, ItemKind.Smg, 5f, G(112f, 48f), G(125f, 47f), G(138f, 49f), G(140f, 58f), G(138f, 64f), G(125f, 64f), G(112f, 62f), G(110f, 55f));
        }

        /// <summary>Where fleeing civilians leave the map: the dock, the roads, the ridge, the airstrip.</summary>
        private void Exits()
        {
            ExitAt(G(-30f, -200f, 0.1f), "Dock");
            ExitAt(G(150f, -116f, 0.1f), "Bus road");
            ExitAt(G(-96f, -118f, 0.1f), "Drive");
            ExitAt(G(10f, -190f, 0.1f), "South lane");
            ExitAt(G(-62f, 142f, 0.1f), "Ridge road");
            ExitAt(G(-190f, 150f, 0.1f), "Airstrip");
        }
    }
}
