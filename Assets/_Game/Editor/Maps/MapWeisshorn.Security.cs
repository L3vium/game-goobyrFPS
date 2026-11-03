using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapWeisshorn
    {
        /// <summary>
        /// The sentry guns of the hotel's VIP floor and the observatory bunker, their hidden terminals and
        /// cables, the extra cameras and the hoses (offices get their PCs and kitchens, messes and guard rooms
        /// their fans from the furnisher).
        /// <list type="bullet">
        /// <item>VIP floor (L4): one gun in the presidential suite facing its keypad door (the balcony stays
        /// behind it), one in the banker's suite facing its corridor door. Both cables climb to the roof walk
        /// and run along the parapets into the attic, to the terminal by its north wall, reached through the
        /// crevasse window, the spa-garden drop ladder or the roof door. Hacked, the presidential gun covers
        /// the desk where the Office routine stops: an accident.</item>
        /// <item>Observatory: one gun in the command level, one in the bunker above it. The cable climbs the
        /// building's east wall, leaves by the back and runs along the ground to the terminal in the antenna
        /// hut (its door or the mast ladder).</item>
        /// </list>
        /// </summary>
        private void Security()
        {
            ArchResult b = _hotelR;
            float H = b.Storey - b.spec.slab - 0.06f;

            // ---- hotel VIP floor
            AutoTurret lPresidential = A.Turret(Hp(25f, 4, 1.2f, 0f), Hp(25f, 4, 7.5f, 1f));
            AutoTurret lBanker = A.Turret(Hp(39f, 4, 29f, 0f), Hp(37f, 4, 21f, 1f));
            A.Cable(lPresidential, Route(b,
                new Vector4(25f, 4, 1.2f, 0.06f), new Vector4(25f, 4, 0.51f, 0.06f), new Vector4(25f, 4, 0.51f, H),
                new Vector4(25f, 5, 0.36f, 0.06f), new Vector4(20.06f, 5, 0.36f, 0.06f), new Vector4(19.49f, 5, 0.51f, 0.06f),
                new Vector4(19.49f, 5, 0.51f, H), new Vector4(19.49f, 5, 29.49f, H), new Vector4(3.35f, 5, 29.49f, H),
                new Vector4(3.35f, 5, 29.49f, 0.95f), new Vector4(3.2f, 5, 29.25f, 0.85f)));
            A.Cable(lBanker, Route(b,
                new Vector4(39f, 4, 29f, 0.06f), new Vector4(39f, 4, 29.49f, 0.06f), new Vector4(39f, 4, 29.49f, H),
                new Vector4(39f, 5, 29.64f, 0.06f), new Vector4(20.06f, 5, 29.64f, 0.06f), new Vector4(19.49f, 5, 29.42f, 0.06f),
                new Vector4(3.65f, 5, 29.42f, 0.06f), new Vector4(3.65f, 5, 29.42f, 0.95f), new Vector4(3.5f, 5, 29.2f, 0.85f)));
            TerminalDesk(b, 3.0f, 5, 29.0f, 180f, lPresidential, lBanker);

            // ---- observatory: the command level (-2) and the bunker (-1)
            ArchResult o = _observatoryR;
            float Ho = o.Storey - o.spec.slab - 0.06f;
            AutoTurret lCommand = A.Turret(o.At(16.9f, -2, 13.0f, 0f), o.At(6f, -2, 4f, 1f));
            AutoTurret lBunker = A.Turret(o.At(1.0f, -1, 13.0f, 0f), o.At(10f, -1, 4f, 1f));
            A.Cable(lBunker, Route(o,
                new Vector4(1.0f, -1, 13.0f, 0.06f), new Vector4(0.36f, -1, 13.0f, 0.06f), new Vector4(0.36f, -1, 13.64f, 0.06f),
                new Vector4(0.36f, -2, 13.64f, Ho), new Vector4(16.5f, -2, 13.64f, Ho), new Vector4(16.5f, -2, 13.64f, 0.06f),
                new Vector4(16.7f, -2, 13.3f, 0.06f)));
            Vector3[] lInside = Route(o,
                new Vector4(16.9f, -2, 13.0f, 0.06f), new Vector4(17.64f, -2, 13.0f, 0.06f), new Vector4(17.64f, -2, 13.0f, Ho),
                new Vector4(17.64f, -1, 13.0f, 0.06f), new Vector4(17.64f, -1, 13.0f, Ho), new Vector4(17.64f, 0, 13.0f, 0.06f),
                new Vector4(17.64f, 0, 13.64f, 0.06f), new Vector4(17.64f, 0, 13.64f, 0.6f), new Vector4(17.64f, 0, 14.06f, 0.6f),
                new Vector4(17.64f, 0, 14.06f, 0.06f));
            Vector3 lHutDoor = _antennaR.At(1.0f, 0, -0.06f, 0.06f);
            Vector3 lFarCorner = o.At(-0.06f, 0, 14.06f, 0.06f), lNearCorner = lInside[lInside.Length - 1];
            Vector3[] lOutside = Vector3.Distance(lFarCorner, lHutDoor) < Vector3.Distance(lNearCorner, lHutDoor)
                ? Join(GroundRun(lNearCorner, lFarCorner, 1.5f), GroundRun(lFarCorner, lHutDoor))
                : GroundRun(lNearCorner, lHutDoor);
            Vector3[] lHut = Route(_antennaR,
                new Vector4(1.0f, 0, -0.06f, 0.06f), new Vector4(1.0f, 0, 0.31f, 0.06f), new Vector4(0.31f, 0, 0.31f, 0.06f),
                new Vector4(0.31f, 0, 4.69f, 0.06f), new Vector4(1.6f, 0, 4.69f, 0.06f), new Vector4(1.6f, 0, 4.69f, 0.95f),
                new Vector4(2.0f, 0, 4.45f, 0.85f));
            A.Cable(lCommand, Join(lInside, lOutside, lHut));
            TerminalDesk(_antennaR, 3.0f, 0, 4.2f, 180f, lCommand, lBunker);

            // ---- cameras: the VIP corridor, the observatory hall and bunker, the service-road barrier
            A.Camera(Hp(0.6f, 4, 19.6f, 3.4f), Hp(20f, 4, 18.5f, 0f), ZONE_HOTEL);
            A.Camera(o.At(17.4f, 0, 8.6f, 3.1f), o.At(9f, 0, 1f, 1f), ZONE_SKI);
            A.Camera(o.At(17.4f, -1, 13.4f, 3.1f), o.At(9f, -1, 4f, 0f), ZONE_SKI);
            A.Camera(G(44.5f, 113.5f, 2.6f), G(48f, 118f, 1f), ZONE_HOTEL);

            // ---- hoses: the delivery yard, the spa garden, behind the Gasthaus, the groomer garage
            A.Distraction("hose", Hp(12f, 1, 30.02f, 0f), b.Yaw(0f));
            A.Distraction("hose", Hp(-0.02f, 2, 24f, 0f), b.Yaw(-90f));
            A.Distraction("hose", _gasthausR.At(4f, 0, 14.02f, 0f), _gasthausR.Yaw(0f));
            A.Distraction("hose", _garageR.At(16.02f, 0, 3f, 0f), _garageR.Yaw(90f));
        }
    }
}
