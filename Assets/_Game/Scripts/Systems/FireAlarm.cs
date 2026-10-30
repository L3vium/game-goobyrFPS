using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>Host: where fires get reported (sprinklers, AI).</summary>
    public static class FireAlarm
    {
        public static void Report(Vector3 pPoint) => Sprinkler.TriggerAt(pPoint);
    }
}
