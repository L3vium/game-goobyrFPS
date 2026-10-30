using System;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    public enum NoiseKind : byte
    {
        /// <summary>Something odd: a coin, a radio, a phone. NPCs go and look.</summary>
        Distraction,
        Footstep,
        /// <summary>A silenced shot or a body falling: suspicious.</summary>
        Suspicious,
        /// <summary>Gunfire, explosions, screams: NPCs go into combat toward the source.</summary>
        Alarming
    }

    public struct NoiseEvent
    {
        public Vector3 position;
        public float radius;
        public NoiseKind kind;
        /// <summary>The player responsible (for "who shot"), or ulong.MaxValue.</summary>
        public ulong source;
        public bool fromPlayer;
    }

    /// <summary>
    /// Host-side bus of sounds the NPCs can hear. Weapons, distractions, explosions and systems
    /// emit; every NPC within the radius receives it through <see cref="OnNoise"/>.
    /// </summary>
    public static class NoiseSystem
    {
        public static event Action<NoiseEvent> OnNoise;

        public static void Emit(Vector3 pPosition, float pRadius, NoiseKind pKind, ulong pSource = ulong.MaxValue, bool pFromPlayer = true)
        {
            OnNoise?.Invoke(new NoiseEvent { position = pPosition, radius = pRadius, kind = pKind, source = pSource, fromPlayer = pFromPlayer });
        }
    }
}
