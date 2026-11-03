using System.Collections.Generic;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>What makes a zone treat a player as hostile.</summary>
    public enum HostileAct : byte
    {
        /// <summary>Fired a gun (or set off an explosion) in front of someone.</summary>
        Shot,
        /// <summary>Killed someone in front of a witness.</summary>
        Killed,
        /// <summary>Punched someone (the victim remembers, so do the witnesses).</summary>
        Punched,
        /// <summary>Took someone down or knocked them out in front of a witness.</summary>
        Takedown,
        /// <summary>Carried a body in front of a witness.</summary>
        CarriedBody,
        /// <summary>Broke line of sight while being confronted somewhere they had no business being.</summary>
        FledConfrontation,
        /// <summary>A gun drawn where it is a crime (outside public places).</summary>
        DrawnGun,
        /// <summary>Already fighting the zone's NPCs.</summary>
        Fought
    }

    /// <summary>
    /// A zone's memory of hostile players (host side, pure so it can be tested): who did something
    /// unforgivable in front of its NPCs, and until when it remembers. While it remembers, a
    /// sighting skips the social ladder - no tier, no confrontation: seen means a fight.
    /// </summary>
    public sealed class HostileMemory
    {
        /// <summary>How long a zone remembers a hostile player after their last act (seconds).</summary>
        public const float DURATION = 120f;
        public const ulong NO_PLAYER = ulong.MaxValue;

        private readonly Dictionary<ulong, float> _until = new Dictionary<ulong, float>();

        /// <summary>The player did it again (or for the first time): remembered for <paramref name="pDuration"/> from now.</summary>
        public void Mark(ulong pPlayer, float pNow, float pDuration = DURATION)
        {
            if (pPlayer == NO_PLAYER) return;
            float lUntil = pNow + pDuration;
            if (!_until.TryGetValue(pPlayer, out float lOld) || lOld < lUntil) _until[pPlayer] = lUntil;
        }

        public bool IsHostile(ulong pPlayer, float pNow) => _until.TryGetValue(pPlayer, out float lUntil) && pNow < lUntil;

        public void Clear() => _until.Clear();
    }
}
