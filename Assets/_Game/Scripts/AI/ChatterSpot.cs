using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.AI
{
    /// <summary>
    /// The guidance layer: NPCs chatting at a spot (two maids, the pilots, waiters) reveal an
    /// opportunity when the local player lingers close by - a subtitle, never a marker. Purely local;
    /// each line plays once, then again after a while.
    /// </summary>
    public class ChatterSpot : MonoBehaviour
    {
        [SerializeField] private string _speaker = "";
        [SerializeField, TextArea] private string[] _lines = new string[0];
        [SerializeField] private float _radius = 7f;

        private int _next;
        private float _cooldown;

        public void Setup(string pSpeaker, float pRadius, params string[] pLines)
        {
            _speaker = pSpeaker;
            _radius = pRadius;
            _lines = pLines;
        }

        private void Update()
        {
            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f || _lines == null || _lines.Length == 0) return;
            PlayerAvatar lMe = PlayerAvatar.Local;
            if (lMe == null || !lMe.Health.IsAlive) return;
            if ((lMe.transform.position - transform.position).sqrMagnitude > _radius * _radius) return;
            // Only while someone is actually standing here to say it.
            bool lSpeaker = false;
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.Health.IsAlive && !lNpc.IsAlerted && (lNpc.transform.position - transform.position).sqrMagnitude < 25f) { lSpeaker = true; break; }
            if (!lSpeaker) return;
            UI.Hud.Notify("OVERHEARD · " + _speaker.ToUpperInvariant(), _lines[_next], UI.ThreatMarkers.White);
            _next = (_next + 1) % _lines.Length;
            _cooldown = _next == 0 ? 90f : 7f;
        }
    }
}
