// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Core
{
    /// <summary>
    /// The one body size every system agrees on: the player's camera and capsule, NPC eyes, hit
    /// boxes, markers, cover heights - and the character models, which are scaled so their eyes
    /// sit at <see cref="EYE"/> (<c>FX/HumanBody</c>). A person is 1.80 m to the top of the head.
    /// </summary>
    public static class BodyMetrics
    {
        /// <summary>Top of the head, standing.</summary>
        public const float HEIGHT = 1.80f;
        public const float EYE = 1.60f;
        public const float CROUCH_EYE = 0.90f;
        public const float CHEST = 1.15f;
        public const float CROUCH_CHEST = 0.60f;

        /// <summary>Head sphere (headshots): centre above the feet and radius, standing and crouched.</summary>
        public const float HEAD_CENTRE = 1.64f;
        public const float CROUCH_HEAD_CENTRE = 0.94f;
        public const float HEAD_RADIUS = 0.16f;
        /// <summary>The head as bullets see it, much larger than the skull: from the shoulders up is a headshot.</summary>
        public const float HEAD_HIT_RADIUS = 0.24f;

        /// <summary>Player capsule heights; the camera sits <see cref="CAPSULE"/> - <see cref="EYE"/> under the top.</summary>
        public const float CAPSULE = 1.80f;
        public const float CROUCH_CAPSULE = 1.10f;
        public const float CRAWL_CAPSULE = 0.74f;

        /// <summary>Head bone to eyes, on the character models (the eyes sit a little above the skull's pivot).</summary>
        public const float HEAD_BONE_TO_EYE = 0.04f;
        /// <summary>Where world markers float above a standing person.</summary>
        public const float MARKER = HEIGHT + 0.25f;
    }
}
