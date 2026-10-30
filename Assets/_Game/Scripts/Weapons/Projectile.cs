using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Net;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// A rocket: flies straight and fast on the host, explodes on the first thing it touches.
    /// Clients see it through its NetworkTransform with a smoke trail.
    /// </summary>
    public class Projectile : NetworkBehaviour
    {
        private Vector3 _velocity;
        private ulong _attacker;
        private int _team;
        private float _radius, _damage;
        private float _life = 6f;
        private float _trailTimer;

        public static void SpawnRocket(Vector3 pPosition, Vector3 pDirection, ItemDefinition pItem, ulong pAttacker, int pTeam)
        {
            NetworkObject lObject = Instantiate(NetPrefabs.Instance.rocket, pPosition, Quaternion.LookRotation(pDirection));
            Projectile lRocket = lObject.GetComponent<Projectile>();
            lRocket._velocity = pDirection.normalized * Mathf.Max(10f, pItem.projectileSpeed);
            lRocket._attacker = pAttacker;
            lRocket._team = pTeam;
            lRocket._radius = pItem.explosionRadius;
            lRocket._damage = pItem.damage;
            lObject.Spawn(true);
        }

        public override void OnNetworkSpawn()
        {
            Transform lVisual = new GameObject("Visual").transform;
            lVisual.SetParent(transform, false);
            GameArt.Part(lVisual, "Body", LowPolyMesh.Cached("w.cyl8", () => LowPolyMesh.Cylinder(8)), Vector3.zero, new Vector3(0.12f, 0.35f, 0.12f), new Color(0.3f, 0.45f, 0.25f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
            GameArt.Part(lVisual, "Flame", LowPolyMesh.Cached("w.sphere", () => LowPolyMesh.Icosphere(1)), new Vector3(0, 0, -0.4f), Vector3.one * 0.18f, new Color(1f, 0.7f, 0.3f), GameArt.Glow);
            Light lLight = lVisual.gameObject.AddComponent<Light>();
            lLight.color = new Color(1f, 0.6f, 0.3f);
            lLight.range = 6f;
            lLight.intensity = 3f;
            Audio.Sfx.Play("rocket_fly", transform.position, 0.8f, 60f);
        }

        private void Update()
        {
            _trailTimer -= Time.deltaTime;
            if (_trailTimer <= 0f)
            {
                _trailTimer = 0.03f;
                Effects.Dust(transform.position - transform.forward * 0.4f, 0.4f);
            }

            if (!IsServer) return;
            _life -= Time.deltaTime;
            Vector3 lStep = _velocity * Time.deltaTime;
            bool lHitSomething = Physics.SphereCast(transform.position, 0.15f, _velocity.normalized, out RaycastHit lHit, lStep.magnitude, GameLayers.ShotMask, QueryTriggerInteraction.Ignore);
            if (_life <= 0f || lHitSomething)
            {
                Vector3 lAt = lHitSomething ? lHit.point : transform.position;
                Ballistics.ExplodeServer(lAt, _radius, _damage, _attacker, _team);
                NetworkObject.Despawn();
                return;
            }
            transform.position += lStep;
        }
    }
}
