using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>Host: tells the AI a breaker flipped (the nearest NPC of the building goes to it).</summary>
    public static class BreakerAlert
    {
        public static event System.Action<Breaker> OnBreaker;
        public static void Raise(Breaker pBreaker) => OnBreaker?.Invoke(pBreaker);
    }
}
