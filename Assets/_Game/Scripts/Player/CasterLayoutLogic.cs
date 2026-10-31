using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>How the caster's screen is split between feeds.</summary>
    public enum CasterLayout : byte
    {
        /// <summary>One feed, full screen.</summary>
        Single,
        /// <summary>Two 16:9 feeds side by side, bands above and below for the overlays.</summary>
        Duo,
        /// <summary>Four feeds, 2 x 2.</summary>
        Quad,
        /// <summary>A big main feed and three small ones on the right, the agent cards below.</summary>
        MainPlus3
    }

    /// <summary>What a feed shows.</summary>
    public enum FeedKind : byte
    {
        /// <summary>Behind an agent's shoulder (the caster can orbit and zoom).</summary>
        Chase,
        /// <summary>Through an agent's eyes.</summary>
        Eyes,
        /// <summary>A camera flown freely.</summary>
        Free,
        /// <summary>The map plan with every agent, NPC and objective.</summary>
        Tactical
    }

    /// <summary>What the auto-director weighs to choose who goes on the main feed.</summary>
    public struct FeedInterest
    {
        public bool alive;
        public bool combat;
        public bool engaged;
        public bool carriesProof;
        public bool hurtRecently;
        /// <summary>Metres to the target (or to the proof once it dropped).</summary>
        public float objectiveDistance;
    }

    /// <summary>
    /// The pure part of the caster view: where each feed sits on screen for a layout, which feed is
    /// under a point, and the auto-director's choice of the agent worth watching. Tested in EditMode.
    /// </summary>
    public static class CasterLayoutLogic
    {
        public const int MAX_FEEDS = 4;
        /// <summary>The auto-director keeps a pick at least this long.</summary>
        public const float MIN_HOLD = 5f;
        /// <summary>...and changes only for an agent this much more interesting.</summary>
        public const float SWITCH_MARGIN = 1.5f;

        public static int FeedCount(CasterLayout pLayout) => pLayout switch
        {
            CasterLayout.Single => 1,
            CasterLayout.Duo => 2,
            _ => MAX_FEEDS
        };

        /// <summary>The feed's camera viewport, normalised with the origin bottom-left (<see cref="Camera.rect"/>).</summary>
        public static Rect Viewport(CasterLayout pLayout, int pFeed)
        {
            switch (pLayout)
            {
                case CasterLayout.Single: return new Rect(0f, 0f, 1f, 1f);
                case CasterLayout.Duo: return new Rect(pFeed * 0.5f, 0.25f, 0.5f, 0.5f);
                case CasterLayout.Quad: return new Rect(pFeed % 2 * 0.5f, pFeed < 2 ? 0.5f : 0f, 0.5f, 0.5f);
                default: return pFeed == 0 ? new Rect(0f, 0.25f, 0.75f, 0.75f) : new Rect(0.75f, 1f - pFeed * 0.25f, 0.25f, 0.25f);
            }
        }

        /// <summary>The feed under a normalised screen point, -1 for none (the bands around the feeds).</summary>
        public static int FeedAt(CasterLayout pLayout, Vector2 pPoint)
        {
            for (int i = 0; i < FeedCount(pLayout); i++)
                if (Viewport(pLayout, i).Contains(pPoint)) return i;
            return -1;
        }

        public static float Interest(FeedInterest pAgent)
        {
            if (!pAgent.alive) return -1f;
            float lScore = 0f;
            if (pAgent.carriesProof) lScore += 6f;
            if (pAgent.combat) lScore += 5f;
            else if (pAgent.engaged) lScore += 3f;
            if (pAgent.hurtRecently) lScore += 2f;
            lScore += Mathf.Clamp01(1f - pAgent.objectiveDistance / 150f) * 2f;
            return lScore;
        }

        /// <summary>
        /// The agent the main feed should show: the current one while it was shown less than
        /// <see cref="MIN_HOLD"/> seconds or nobody beats it by <see cref="SWITCH_MARGIN"/>; the best one otherwise.
        /// A current pick that died is replaced at once. -1 when nobody is alive.
        /// </summary>
        public static int PickMain(IReadOnlyList<float> pScores, int pCurrent, float pHeld)
        {
            int lBest = -1;
            for (int i = 0; i < pScores.Count; i++)
                if (pScores[i] >= 0f && (lBest < 0 || pScores[i] > pScores[lBest])) lBest = i;
            if (lBest < 0) return -1;
            bool lCurrentAlive = pCurrent >= 0 && pCurrent < pScores.Count && pScores[pCurrent] >= 0f;
            if (!lCurrentAlive) return lBest;
            if (pHeld < MIN_HOLD) return pCurrent;
            return pScores[lBest] >= pScores[pCurrent] + SWITCH_MARGIN ? lBest : pCurrent;
        }
    }
}
