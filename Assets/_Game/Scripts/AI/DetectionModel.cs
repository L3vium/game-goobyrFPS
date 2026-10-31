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

        /// <summary>The gauge after <paramref name="pDt"/> seconds: fills with exposure, drains without.</summary>
        public static float Fill(float pGauge, float pExposure, float pDetectTime, float pDt, bool pCombat)
        {
            if (pExposure <= 0f) return Mathf.Max(pCombat ? COMBAT_FLOOR : 0f, pGauge - DECAY * pDt);
            return Mathf.Min(1f, pGauge + pExposure / Mathf.Max(0.1f, pDetectTime) * pDt * (pCombat ? 3f : 1f));
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
    }
}
