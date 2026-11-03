using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// Recent incidents on the host (pure, so it can be tested): a firefight, a body found, a gun
    /// pulled on the plaza. Shots, screams and sightings close together in space and time are one
    /// incident, and panic spreads through a crowd once per incident - a ripple, not an echo.
    /// </summary>
    public sealed class IncidentLog
    {
        /// <summary>Two alarms closer than this (metres) and <see cref="WINDOW"/> seconds are the same incident.</summary>
        public const float RADIUS = 20f, WINDOW = 20f;

        private readonly List<(int id, Vector3 at, float time)> _recent = new List<(int, Vector3, float)>();
        private int _next;

        /// <summary>The incident this alarm belongs to: a recent one close by (which it keeps alive), else a new one.</summary>
        public int At(Vector3 pPoint, float pNow)
        {
            _recent.RemoveAll(pIncident => pNow - pIncident.time > WINDOW || pIncident.time > pNow);
            for (int i = 0; i < _recent.Count; i++)
            {
                if ((_recent[i].at - pPoint).sqrMagnitude > RADIUS * RADIUS) continue;
                _recent[i] = (_recent[i].id, _recent[i].at, pNow);
                return _recent[i].id;
            }
            _recent.Add((++_next, pPoint, pNow));
            return _next;
        }

        public void Clear() => _recent.Clear();
    }
}
