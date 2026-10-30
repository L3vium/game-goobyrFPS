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
    /// <summary>
    /// A sprinkler zone in a "sophisticated" building (hotel, mansion): any fire reported inside
    /// switches the sprinklers on for a while - water everywhere, fires out, and NPCs nearby
    /// come and look.
    /// </summary>
    public class Sprinkler : Interactable
    {
        [SerializeField] private Vector3 _size = new Vector3(20, 6, 20);
        [SerializeField] private float _duration = 20f;
        [SerializeField] private Transform[] _heads;

        private static readonly List<Sprinkler> _all = new List<Sprinkler>();
        private float _timer;
        private readonly List<GameObject> _sprays = new List<GameObject>();

        public void Setup(Vector3 pSize, Transform[] pHeads)
        {
            _size = pSize;
            _heads = pHeads;
            _prompt = "";
        }

        public override bool CanInteract(Player.PlayerAvatar pWho, ItemDefinition pHeld) => false;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public bool Contains(Vector3 pPoint)
        {
            Vector3 lLocal = transform.InverseTransformPoint(pPoint);
            return Mathf.Abs(lLocal.x) <= _size.x * 0.5f && lLocal.y >= -0.5f && lLocal.y <= _size.y && Mathf.Abs(lLocal.z) <= _size.z * 0.5f;
        }

        public static bool IsWet(Vector3 pPoint)
        {
            foreach (Sprinkler lSprinkler in _all) if (lSprinkler.State == 1 && lSprinkler.Contains(pPoint)) return true;
            return false;
        }

        /// <summary>Host: a fire somewhere - the zones that contain it switch on.</summary>
        public static void TriggerAt(Vector3 pPoint)
        {
            foreach (Sprinkler lSprinkler in _all)
                if (lSprinkler.Contains(pPoint) && lSprinkler.State == 0)
                {
                    lSprinkler._timer = lSprinkler._duration;
                    lSprinkler.SetStateServer(1);
                    NoiseSystem.Emit(lSprinkler.transform.position, 25f, NoiseKind.Distraction, ulong.MaxValue, false);
                }
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            foreach (GameObject lSpray in _sprays) if (lSpray != null) Destroy(lSpray);
            _sprays.Clear();
            if (pNew != 1 || _heads == null) return;
            foreach (Transform lHead in _heads) if (lHead != null) _sprays.Add(Effects.Spray(lHead, Vector3.zero));
            Audio.Sfx.Play("sprinkler", transform.position + Vector3.up * 3f, 1f, 40f);
        }

        private void Update()
        {
            if (State != 1 || !Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) SetStateServer(0);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.3f);
            Gizmos.DrawCube(new Vector3(0, _size.y * 0.5f, 0), _size);
        }
    }
}
