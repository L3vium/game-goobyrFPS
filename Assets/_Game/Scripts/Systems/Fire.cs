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
    /// Something that burns: crates, curtains, hay, fuel cans, wooden walls. The rule is short so
    /// players can plan with it: a burning thing burns for a while, hurts what stands in it, and
    /// lights its flammable neighbours within <see cref="_spreadRadius"/> - unless it is wet.
    /// Fuel explodes when it catches.
    /// </summary>
    public class Flammable : Interactable, IShootable
    {
        [SerializeField] private float _burnTime = 14f;
        [SerializeField] private float _spreadRadius = 3.2f;
        [SerializeField] private bool _explodes;
        [SerializeField] private float _fireSize = 1f;

        public bool Burning => State == 1;
        public bool Burnt => State == 2;

        private float _timer;
        private float _spreadTimer;
        private GameObject _fireFx;

        public void Setup(float pBurnTime, float pSpreadRadius, bool pExplodes, float pFireSize)
        {
            _burnTime = pBurnTime;
            _spreadRadius = pSpreadRadius;
            _explodes = pExplodes;
            _fireSize = pFireSize;
            _prompt = "";
        }

        public override bool CanInteract(Player.PlayerAvatar pWho, ItemDefinition pHeld) => false;

        public void IgniteServer()
        {
            if (State != 0 || Sprinkler.IsWet(transform.position)) return;
            _timer = _burnTime;
            _spreadTimer = GameDatabase.Rules.fireSpreadInterval;
            SetStateServer(1);
            NoiseSystem.Emit(transform.position, 22f, NoiseKind.Alarming, ulong.MaxValue, false);
            FireAlarm.Report(transform.position);
            if (_explodes)
            {
                Ballistics.ExplodeServer(transform.position + Vector3.up * 0.5f, 6f, 140f, DamageInfo.NO_PLAYER, Match.MatchRulesLogic.NO_TEAM);
                _timer = 6f;
            }
        }

        public void ExtinguishServer()
        {
            if (State == 1) SetStateServer(0);
        }

        public void OnShotServer(DamageInfo pInfo)
        {
            if (_explodes && pInfo.type != DamageType.Fire) IgniteServer();
            else if (pInfo.type == DamageType.Explosion || pInfo.type == DamageType.Fire) IgniteServer();
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew == 1 && _fireFx == null)
            {
                Bounds lBounds = GetBounds();
                _fireFx = Effects.Fire(transform, transform.InverseTransformPoint(new Vector3(lBounds.center.x, lBounds.min.y + 0.2f, lBounds.center.z)), _fireSize);
                Audio.Sfx.Play("fire_start", transform.position, 0.8f, 40f);
            }
            else if (pNew != 1 && _fireFx != null)
            {
                Destroy(_fireFx);
                _fireFx = null;
                Effects.Dust(transform.position + Vector3.up, 1.5f);
            }
            if (pNew == 2)
                foreach (Renderer lRenderer in GetComponentsInChildren<Renderer>()) GameArt.Paint(lRenderer, new Color(0.15f, 0.13f, 0.12f));
        }

        private Bounds GetBounds()
        {
            Renderer lRenderer = GetComponentInChildren<Renderer>();
            return lRenderer != null ? lRenderer.bounds : new Bounds(transform.position, Vector3.one);
        }

        private void Update()
        {
            if (!Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer || State != 1) return;
            _timer -= Time.deltaTime;
            _spreadTimer -= Time.deltaTime;

            if (Sprinkler.IsWet(transform.position)) { ExtinguishServer(); return; }
            if (_timer <= 0f) { SetStateServer(2); return; }
            if (_spreadTimer > 0f) return;
            _spreadTimer = GameDatabase.Rules.fireSpreadInterval;

            Bounds lBounds = GetBounds();
            foreach (Collider lCollider in Physics.OverlapSphere(lBounds.center, _spreadRadius + lBounds.extents.magnitude * 0.5f, ~0, QueryTriggerInteraction.Collide))
            {
                Flammable lOther = lCollider.GetComponentInParent<Flammable>();
                if (lOther != null && lOther != this && Random.value < 0.55f) lOther.IgniteServer();

                Damageable lVictim = lCollider.GetComponentInParent<Damageable>();
                if (lVictim != null && lVictim.IsAlive && lBounds.SqrDistance(lVictim.transform.position + Vector3.up * 0.5f) < 1.5f)
                    Weapons.Burning.Apply(lVictim, DamageInfo.NO_PLAYER, Match.MatchRulesLogic.NO_TEAM);
            }
            SmokeVolume.AddServer(lBounds.center + Vector3.up * 2f, 2.5f, GameDatabase.Rules.fireSpreadInterval + 0.5f);
        }
    }

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

    /// <summary>Host: where fires get reported (sprinklers, AI).</summary>
    public static class FireAlarm
    {
        public static void Report(Vector3 pPoint) => Sprinkler.TriggerAt(pPoint);
    }

    /// <summary>Fire drawn on burning characters, on every machine.</summary>
    public static class BurnVisuals
    {
        private static readonly Dictionary<ulong, GameObject> _fires = new Dictionary<ulong, GameObject>();

        public static void Broadcast(Damageable pTarget, bool pOn)
        {
            if (MapEffects.Instance != null) MapEffects.Instance.BurnRpc(pTarget.NetworkObject, pOn);
        }

        public static void Apply(Unity.Netcode.NetworkObject pTarget, bool pOn)
        {
            if (pTarget == null) return;
            ulong lId = pTarget.NetworkObjectId;
            if (_fires.TryGetValue(lId, out GameObject lFire) && lFire != null) Destroy(lFire);
            _fires.Remove(lId);
            if (pOn) _fires[lId] = Effects.Fire(pTarget.transform, new Vector3(0, 0.9f, 0), 0.9f);
        }

        private static void Destroy(Object pObject) => Object.Destroy(pObject);
    }
}
