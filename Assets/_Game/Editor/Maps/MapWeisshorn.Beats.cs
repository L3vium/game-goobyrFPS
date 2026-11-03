using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapWeisshorn
    {
        /// <summary>
        /// Patrol beats: a loop on every floor of the Grand Hotel (underbelly, staff tier, lobby round the
        /// feature stair, the suites' and the VIP floor's atrium rings, the roof walk), the delivery yard,
        /// pairs on the chalet street and the church square (the shore is left to the boat start), the ski hamlet and the
        /// observatory bunker. Each loop passes the fans, PCs, hoses, camera blind spots and gun approaches
        /// of its area, so every device has someone to pull.
        /// </summary>
        private void Beats()
        {
            const int V = ZONE_VILLAGE, S = ZONE_SKI, H = ZONE_HOTEL;
            ArchResult b = _hotelR;
            Loop(b, "Underbelly", H, 1, ItemKind.Smg, 4f, new Vector3(8f, 0, 7f), new Vector3(30f, 0, 7f), new Vector3(50f, 0, 7f), new Vector3(63f, 0, 8f),
                new Vector3(63f, 0, 22f), new Vector3(49f, 0, 22f), new Vector3(35f, 0, 22f), new Vector3(21f, 0, 22f), new Vector3(8f, 0, 22f));
            Loop(b, "StaffTier", H, 1, ItemKind.Smg, 4f, new Vector3(7f, 1, 7f), new Vector3(28f, 1, 7f), new Vector3(49f, 1, 7f), new Vector3(49f, 1, 22f),
                new Vector3(35f, 1, 22f), new Vector3(35f, 1, 10f), new Vector3(20f, 1, 10f));
            Loop(b, "Lobby", H, 2, ItemKind.Pistol, 5f, new Vector3(3f, 2, 3f), new Vector3(20f, 2, 3f), new Vector3(36f, 2, 3f), new Vector3(36f, 2, 7f),
                new Vector3(17f, 2, 8.5f), new Vector3(15f, 2, 12f), new Vector3(15f, 2, 15f), new Vector3(3f, 2, 15f));
            Loop(b, "SuitesRing", H, 1, ItemKind.Pistol, 4f, new Vector3(6f, 3, 18.5f), new Vector3(20f, 3, 18.5f), new Vector3(34f, 3, 18.5f), new Vector3(34f, 3, 13f),
                new Vector3(34f, 3, 8.8f), new Vector3(20f, 3, 8.8f), new Vector3(6f, 3, 8.8f), new Vector3(6f, 3, 13f));
            Loop(b, "VipRing", H, 2, ItemKind.Smg, 4f, new Vector3(6f, 4, 18.5f), new Vector3(20f, 4, 18.5f), new Vector3(34f, 4, 18.5f), new Vector3(34f, 4, 13f),
                new Vector3(34f, 4, 8.8f), new Vector3(20f, 4, 8.8f), new Vector3(6f, 4, 8.8f), new Vector3(6f, 4, 13f));
            Loop(b, "RoofWalk", H, 1, ItemKind.Smg, 6f, new Vector3(21.5f, 5, 2f), new Vector3(30f, 5, 2f), new Vector3(38.5f, 5, 2f), new Vector3(38.5f, 5, 16f),
                new Vector3(38.5f, 5, 28.5f), new Vector3(30f, 5, 28.5f), new Vector3(21.5f, 5, 28.5f), new Vector3(29.5f, 5, 16f));
            Beat("DeliveryYard", H, 2, ItemKind.Smg, 4f, G(-2f, 114f), G(10f, 121f), G(22f, 121.5f), G(34f, 121.5f), G(44f, 121f), G(46f, 115f), G(30f, 114f), G(14f, 114f));

            Beat("ChaletStreet", V, 2, ItemKind.Pistol, 5f, G(-90f, -147f), G(-45f, -147f), G(0f, -146f), G(45f, -147f), G(84f, -147f), G(84f, -153f), G(0f, -154f), G(-90f, -153f));
            Beat("ChurchSquare", V, 2, ItemKind.Pistol, 6f, G(-27f, -145f), G(-27f, -128f), G(-13f, -128f), G(-13f, -145f), G(-2f, -150f), G(-38f, -150f));

            Beat("Hamlet", S, 2, ItemKind.Smg, 5f, G(MIDSTATION + new Vector2(-24f, -2f)), G(MIDSTATION + new Vector2(-12f, -14f)), G(MIDSTATION + new Vector2(6f, -16f)),
                G(GARAGE + new Vector2(-8f, 4f)), G(GARAGE + new Vector2(8f, 10f)), G(MIDSTATION + new Vector2(22f, 4f)), G(MIDSTATION + new Vector2(6f, 10f)), G(MIDSTATION + new Vector2(-14f, 10f)));
            Loop(_observatoryR, "Bunker", S, 1, ItemKind.Smg, 5f, new Vector3(6f, -1, 2f), new Vector3(11f, -1, 2f), new Vector3(12f, -1, 7f), new Vector3(12f, -1, 12.5f),
                new Vector3(6f, -1, 12.5f), new Vector3(5.5f, -1, 9f));
        }

        /// <summary>Where fleeing civilians leave the map: stations, the lake, the trails.</summary>
        private void Exits()
        {
            ExitAt(G(150f, -150f, 0.1f), "Bus stop");
            ExitAt(_valleyR.At(6f, 0, 8f, 0.1f), "Funicular");
            ExitAt(G(108f, -160f, 0.1f), "Snowmobile");
            ExitAt(G(-165f, -150f, 0.1f), "Boathouse");
            ExitAt(G(-60f, 178f, 0.1f), "Glacier trail");
            ExitAt(_cableTopR.At(4f, 0, 3f, 0.1f), "Cable car");
            ExitAt(G(128f, -116f, 0.1f), "Chairlift bottom");
        }
    }
}
