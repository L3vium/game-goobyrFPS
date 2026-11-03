using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapSanLazaro
    {
        /// <summary>
        /// Casa La Cumbre's sentry guns, their hidden terminals and cables, the extra cameras and the
        /// garden hoses (offices get their PCs and kitchens and guard rooms their fans from the furnisher).
        /// <list type="bullet">
        /// <item>Vault (-2): two guns cover the vault stair's foot and the vault desk. The cable climbs the
        /// vault's east wall, crosses the security room and the cellar corridor and ends at the terminal
        /// in the coal store, reached by the coal chute without passing the guns.</item>
        /// <item>F1 private wing: one gun in the study, one in the master suite, both facing the doors
        /// (the balcony and the study window stay behind them). The cable runs up through the archive,
        /// the mirador base, the stair hall and the steward's office to the terminal in the sewing room
        /// (padlocked door, or the south balcony). Hacked, the study gun covers the study desk where
        /// the Don's Office routine stops: an accident.</item>
        /// </list>
        /// </summary>
        private void Security()
        {
            ArchResult b = _casaR;
            float H = b.Storey - b.spec.slab - 0.06f;
            // ---- vault (-2)
            AutoTurret lVaultA = A.Turret(C(25f, -2, 11.2f, 0f), C(15.2f, -2, 13f, 1f));
            AutoTurret lVaultB = A.Turret(C(13.9f, -2, 11.0f, b.Storey - b.spec.slab), C(22f, -2, 14f, 1f), true);
            A.Cable(lVaultB, Route(b,
                new Vector4(13.9f, -2, 11.0f, H), new Vector4(13.9f, -2, 10.56f, H), new Vector4(25.0f, -2, 10.56f, H),
                new Vector4(25.0f, -2, 10.56f, 0.06f), new Vector4(25.0f, -2, 10.9f, 0.06f)));
            A.Cable(lVaultA, Route(b,
                new Vector4(25.0f, -2, 11.2f, 0.06f), new Vector4(26.44f, -2, 11.2f, 0.06f), new Vector4(26.44f, -2, 11.2f, H),
                new Vector4(26.44f, -2, 16.44f, H), new Vector4(26.44f, -1, 16.44f, 0.06f), new Vector4(27.44f, -1, 16.44f, 0.06f),
                new Vector4(27.44f, -1, 17.86f, 0.06f), new Vector4(27.44f, -1, 17.86f, H), new Vector4(27.44f, -1, 18.14f, H),
                new Vector4(27.44f, -1, 23.86f, H), new Vector4(27.44f, -1, 24.14f, H), new Vector4(27.44f, -1, 31.44f, H),
                new Vector4(27.44f, -1, 31.44f, 0.95f), new Vector4(28.0f, -1, 31.2f, 0.85f)));
            TerminalDesk(b, 28.6f, -1, 30.95f, 180f, lVaultA, lVaultB);

            // ---- F1 private wing: the study and the master suite
            AutoTurret lStudy = A.Turret(C(23.75f, 1, 31.0f, 0f), C(23.75f, 1, 24.5f, 1f));
            AutoTurret lSuite = A.Turret(C(11.0f, 1, 31.0f, 0f), C(11.0f, 1, 24.5f, 1f));
            A.Cable(lSuite, Route(b,
                new Vector4(11.0f, 1, 31.0f, 0.06f), new Vector4(11.0f, 1, 31.44f, 0.06f), new Vector4(11.0f, 1, 31.44f, H),
                new Vector4(23.5f, 1, 31.44f, H), new Vector4(23.5f, 1, 31.44f, 0.06f), new Vector4(23.6f, 1, 31.2f, 0.06f)));
            A.Cable(lStudy, Route(b,
                new Vector4(23.9f, 1, 31.2f, 0.06f), new Vector4(24.0f, 1, 31.44f, 0.06f), new Vector4(24.0f, 1, 31.44f, H - 0.09f),
                new Vector4(29.84f, 1, 31.44f, H - 0.09f), new Vector4(29.84f, 2, 31.44f, 0.06f), new Vector4(29.84f, 2, 31.44f, H),
                new Vector4(30.16f, 2, 31.44f, H), new Vector4(30.16f, 2, 24.16f, H), new Vector4(30.16f, 2, 23.84f, H),
                new Vector4(30.16f, 2, 14.16f, H), new Vector4(30.16f, 2, 13.84f, H), new Vector4(30.16f, 2, 3.0f, H),
                new Vector4(29.84f, 2, 3.0f, H), new Vector4(29.84f, 2, 0.56f, H), new Vector4(20.4f, 2, 0.56f, H),
                new Vector4(20.4f, 2, 0.56f, 0.95f), new Vector4(20.7f, 2, 0.8f, 0.85f)));
            TerminalDesk(b, 21.1f, 2, 1.0f, 0f, lStudy, lSuite);

            // ---- cameras: the gates, the vault approach, the private wing's gallery
            A.Camera(G(72.4f, 70.4f, 3.4f), G(75f, 79f, 0.5f), ZONE_HACIENDA);
            A.Camera(G(105.4f, 141.4f, 3.2f), G(108f, 133f, 1f), ZONE_HACIENDA);
            A.Camera(C(27.4f, -1, 10.6f, 3.1f), C(16f, -1, 15f, 1f), ZONE_HACIENDA);
            A.Camera(C(10.4f, 1, 23.6f, 3.0f), C(25f, 1, 23f, 1f), ZONE_HACIENDA);

            // ---- garden hoses at the foot of the walls (kitchen yard, orchard, sacristy garden)
            A.Distraction("hose", C(33f, 0, 32.02f, 0f), b.Yaw(0f));
            A.Distraction("hose", C(40.02f, 0, 10f, 0f), b.Yaw(90f));
            A.Distraction("hose", C(-0.02f, 0, 26f, 0f), b.Yaw(-90f));
        }
    }
}
