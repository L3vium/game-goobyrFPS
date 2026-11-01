using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// The rules of being noticed, kept pure so they can be tested.
    /// <para>
    /// <b>1. Suspicious.</b> While a player is in view an NPC's gauge fills (fast up close, slowly far
    /// away or at the edge of its eye); it stops and stares, and past half it walks over to look.
    /// Out of view the gauge drains.
    /// </para>
    /// <para>
    /// <b>2. Engaged.</b> A full gauge: the NPC confronts the player - weapon up, walking over,
    /// shouting - for <see cref="CONFRONT_TIME"/>. Breaking line of sight for
    /// <see cref="CONFRONT_LOST"/> turns it into a search; holding still in its sight, aiming at it or
    /// shooting turns it into combat.
    /// </para>
    /// <para>
    /// <b>3. Combat.</b> It shoots. The fight stays contained: another NPC joins only if it sees the
    /// player, sees the confrontation (<see cref="Witnesses"/>), hears the shots, or gets the phone
    /// call (<see cref="HearsCall"/>).
    /// </para>
    /// </summary>
    public static class DetectionModel
    {
        /// <summary>Gauge level past which a suspicious NPC walks over to look.</summary>
        public const float INVESTIGATE = 0.5f;
        /// <summary>Seconds of confrontation before an engaged NPC opens fire.</summary>
        public const float CONFRONT_TIME = 2f;
        /// <summary>Out of an engaged NPC's sight this long: it searches instead of shooting.</summary>
        public const float CONFRONT_LOST = 1.2f;
        /// <summary>Half-angle of the edge of vision: seen there, the gauge fills at <see cref="PERIPHERAL_FACTOR"/>.</summary>
        public const float PERIPHERAL_ANGLE = 75f;
        public const float PERIPHERAL_FACTOR = 0.3f;
        /// <summary>Closer than this, an NPC feels a player whatever it faces.</summary>
        public const float CLOSE_RANGE = 2.2f;
        /// <summary>Gauge drain per second out of sight.</summary>
        public const float DECAY = 0.22f;
        /// <summary>Gauge floor while fighting (a lost player is not forgotten).</summary>
        public const float COMBAT_FLOOR = 0.6f;
        /// <summary>An NPC that sees a confrontation or a fight this close joins it.</summary>
        public const float WITNESS_RANGE = 18f;
        /// <summary>A completed phone call alerts the caller's zone mates this close to it.</summary>
        public const float CALL_RADIUS = 60f;

        /// <summary>What an NPC's eye gets of a player this perception tick.</summary>
        public struct Sight
        {
            public float distance, range, angle, halfCone;
            public bool crouched, inGrass, sprinting, chestVisible, headVisible;
        }

        /// <summary>0 = unseen; higher fills the gauge faster (about 2.5 at arm's length, 0.3 at the edge of range).</summary>
        public static float Exposure(in Sight pSight)
        {
            if (pSight.distance > pSight.range) return 0f;
            float lCone;
            if (pSight.angle <= pSight.halfCone) lCone = Mathf.Lerp(1f, 0.55f, pSight.angle / Mathf.Max(1f, pSight.halfCone));
            else if (pSight.angle <= PERIPHERAL_ANGLE) lCone = PERIPHERAL_FACTOR;
            else if (pSight.distance < CLOSE_RANGE) lCone = 0.6f;
            else return 0f;

            float lStance = 1f;
            if (pSight.crouched) lStance *= 0.55f;
            if (pSight.inGrass) lStance *= pSight.crouched ? 0.08f : 0.6f;
            if (pSight.sprinting) lStance *= 1.3f;
            if (lStance < 0.1f && pSight.distance > 3f) return 0f;
            if (!pSight.chestVisible && !pSight.headVisible) return 0f;

            float lCloseness = Mathf.Lerp(2.5f, 0.3f, pSight.distance / Mathf.Max(0.1f, pSight.range));
            return lCloseness * lCone * lStance * (pSight.chestVisible ? 1f : 0.6f);
        }

        /// <summary>
        /// The gauge after <paramref name="pDt"/> seconds: fills with exposure, drains without. A
        /// fighter fills three times faster and never forgets below <see cref="COMBAT_FLOOR"/>; an
        /// alerted NPC (searching, investigating a shot) fills twice as fast.
        /// </summary>
        public static float Fill(float pGauge, float pExposure, float pDetectTime, float pDt, bool pCombat, bool pAlert = false)
        {
            if (pExposure <= 0f) return Mathf.Max(pCombat ? COMBAT_FLOOR : 0f, pGauge - DECAY * pDt);
            return Mathf.Min(1f, pGauge + pExposure / Mathf.Max(0.1f, pDetectTime) * pDt * (pCombat ? 3f : pAlert ? 2f : 1f));
        }

        public enum Confrontation { Hold, Combat, LostSight }

        /// <summary>How a confrontation ends: a hostile act or a full window in sight is combat, a broken line of sight a search.</summary>
        public static Confrontation ResolveConfront(float pEngagedFor, float pUnseenFor, bool pHostileAct)
        {
            if (pHostileAct) return Confrontation.Combat;
            if (pUnseenFor >= CONFRONT_LOST) return Confrontation.LostSight;
            return pEngagedFor >= CONFRONT_TIME ? Confrontation.Combat : Confrontation.Hold;
        }

        /// <summary>Does an NPC notice a confrontation or a fight (close, roughly in front of it, nothing in between)?</summary>
        public static bool Witnesses(float pDistance, float pAngle, bool pLineOfSight) => pLineOfSight && pDistance <= WITNESS_RANGE && pAngle <= PERIPHERAL_ANGLE;

        /// <summary>Does a completed call alert this NPC? Only the caller's zone, only around the caller.</summary>
        public static bool HearsCall(int pZone, int pCallerZone, float pDistance) => pZone == pCallerZone && pDistance <= CALL_RADIUS;

        /// <summary>A completed call sends at most this many of the caller's mates (the nearest).</summary>
        public const int CALL_RESPONDERS = 6;

        // ---------------------------------------------------------------- hearing

        /// <summary>Each wall or floor between a sound and an ear leaves it this share of its reach.</summary>
        public const float WALL_DAMPING = 0.4f;
        public const int MAX_WALLS = 3;

        /// <summary>What an NPC does with a sound it heard.</summary>
        public enum Heard : byte
        {
            None,
            /// <summary>Turns to look for a moment, a little more wary.</summary>
            Glance,
            /// <summary>Walks over to look around the guessed spot.</summary>
            Investigate,
            /// <summary>Goes over weapon up and searches the area.</summary>
            Alarmed
        }

        /// <summary>
        /// How loud a sound is at an ear: 1 at the source, 0 at the edge of its reach.
        /// <paramref name="pRadius"/> is the reach in the open; every wall or floor in between cuts it
        /// to <see cref="WALL_DAMPING"/>, so a pistol heard 30 m away outside carries 12 m through one
        /// wall and under 5 m through two.
        /// </summary>
        public static float Loudness(float pDistance, float pRadius, int pWalls)
        {
            float lReach = pRadius * Mathf.Pow(WALL_DAMPING, Mathf.Clamp(pWalls, 0, MAX_WALLS));
            if (lReach <= 0.01f || pDistance >= lReach) return 0f;
            return 1f - pDistance / lReach;
        }

        /// <summary>How many of the NPCs who hear a sound go and look: the rest only glance.</summary>
        public static int Responders(NoiseKind pKind, float pLoudest) => pKind switch
        {
            NoiseKind.Distraction => pLoudest > 0.7f ? 2 : 1,
            NoiseKind.Footstep => 1,
            NoiseKind.Suspicious => 2,
            _ => 3
        };

        /// <summary>The reaction to a sound, given how loud it is here and whether this NPC is one of the responders.</summary>
        public static Heard React(NoiseKind pKind, float pLoudness, bool pResponder)
        {
            if (pLoudness <= 0f) return Heard.None;
            if (!pResponder) return Heard.Glance;
            return pKind switch
            {
                NoiseKind.Distraction => Heard.Investigate,
                NoiseKind.Footstep => pLoudness > 0.5f ? Heard.Investigate : Heard.Glance,
                NoiseKind.Suspicious => pLoudness > 0.6f ? Heard.Alarmed : Heard.Investigate,
                _ => pLoudness > 0.35f ? Heard.Alarmed : Heard.Investigate
            };
        }

        /// <summary>A sound gives a direction, not a position: the guess is off by this much (a few metres close by, ten far away).</summary>
        public static float GuessError(float pDistance) => Mathf.Clamp(pDistance * 0.25f, 1.5f, 10f);

        /// <summary>Reach of a player's footsteps in the open (metres).</summary>
        public const float STEP_CROUCH = 1.5f, STEP_WALK = 5f, STEP_SPRINT = 14f, STEP_SLIDE = 8f;

        /// <summary>Footsteps: silent standing still, a whisper crouched, a few metres walking, far sprinting or sliding.</summary>
        public static float FootstepRadius(float pSpeed, bool pCrouched, bool pSprinting)
        {
            if (pSpeed < 0.6f) return 0f;
            if (pCrouched) return pSpeed > 4f ? STEP_SLIDE : Mathf.Lerp(0.5f, STEP_CROUCH, Mathf.InverseLerp(0.6f, 2.5f, pSpeed));
            if (pSprinting) return STEP_SPRINT;
            return Mathf.Lerp(2f, STEP_WALK, Mathf.InverseLerp(0.6f, 4.4f, pSpeed));
        }

        /// <summary>A landing: a hop is free, a real drop is heard about 3 m per metre fallen (half crouched).</summary>
        public static float LandingRadius(float pFall, bool pCrouched)
        {
            if (pFall < 1.2f) return 0f;
            return Mathf.Min(20f, (pFall - 0.6f) * 3f) * (pCrouched ? 0.5f : 1f);
        }

        /// <summary>Within this angle of an NPC's back, a punch from someone it has not noticed is a silent takedown.</summary>
        public const float TAKEDOWN_ANGLE = 70f;

        /// <summary>Silent takedown: from behind, the NPC neither confronting nor fighting, its gauge under half.</summary>
        public static bool CanTakedown(float pAngleFromBack, float pAwareness, bool pAlerted) => !pAlerted && pAwareness < INVESTIGATE && pAngleFromBack <= TAKEDOWN_ANGLE;
    }
}
