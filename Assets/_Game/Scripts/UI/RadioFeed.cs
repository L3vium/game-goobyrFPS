using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The talkie-walkie of a team protecting the target: each alert is a line on the right of the HUD
    /// and, for a while, a mark on the map.
    /// </summary>
    public static class RadioFeed
    {
        /// <summary>How long an alert stays marked on the map (seconds).</summary>
        public const float MAP_TIME = 30f;

        public struct Alert
        {
            public string line;
            public Vector3 where;
            public float at;
        }

        public static readonly List<Alert> Recent = new List<Alert>();

        public static void Push(string pLine, Vector3 pWhere)
        {
            Recent.RemoveAll(pAlert => Time.time - pAlert.at > MAP_TIME);
            Recent.Add(new Alert { line = pLine, where = pWhere, at = Time.time });
            Hud.Notify("RADIO", pLine, UIFactory.Accent);
            Audio.Sfx.Play2D("radio", 0.6f);
        }
    }
}
