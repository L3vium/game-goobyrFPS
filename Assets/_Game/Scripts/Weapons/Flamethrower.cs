using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The one flamethrower of each map: a cone of fire that burns whoever is in it and sets every
    /// <see cref="Flammable"/> it licks alight. The host ticks the damage; every machine draws the jet.
    /// </summary>
    public static class Flamethrower
    {
        private const float RANGE = 9f;
        private const float ANGLE = 22f;

        public static void TickServer(Vector3 pOrigin, Vector3 pDirection, ItemDefinition pItem, ulong pAttacker, int pTeam, Damageable pSelf)
        {
            HashSet<Object> lDone = new HashSet<Object>();
            foreach (Collider lCollider in Physics.OverlapSphere(pOrigin + pDirection * RANGE * 0.5f, RANGE * 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 lTo = lCollider.ClosestPoint(pOrigin) - pOrigin;
                if (lTo.magnitude > RANGE || Vector3.Angle(pDirection, lTo) > ANGLE + 10f) continue;
                if (Physics.Linecast(pOrigin, pOrigin + lTo * 0.95f, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore)) continue;

                Damageable lTarget = lCollider.GetComponentInParent<Damageable>();
                if (lTarget != null && lTarget != pSelf && lDone.Add(lTarget))
                {
                    lTarget.ApplyDamage(new DamageInfo { amount = pItem.damage, type = DamageType.Fire, point = lCollider.bounds.center, direction = pDirection, attacker = pAttacker, attackerTeam = pTeam });
                    Burning.Apply(lTarget, pAttacker, pTeam);
                }

                Flammable lFlammable = lCollider.GetComponentInParent<Flammable>();
                if (lFlammable != null && lDone.Add(lFlammable)) lFlammable.IgniteServer();
            }
            NoiseSystem.Emit(pOrigin, pItem.noiseRadius, NoiseKind.Alarming, pAttacker);
        }

        /// <summary>A looping jet of fire on the nozzle.</summary>
        public static GameObject CreateJet(Transform pNozzle)
        {
            GameObject lJet = new GameObject("FlameJet");
            lJet.transform.SetParent(pNozzle, false);
            ParticleSystem lSystem = lJet.AddComponent<ParticleSystem>();
            lSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule lMain = lSystem.main;
            lMain.loop = true;
            lMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            lMain.startSpeed = new ParticleSystem.MinMaxCurve(15f, 20f);
            lMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            lMain.startColor = new Color(1f, 0.65f, 0.2f);
            lMain.gravityModifier = -0.3f;
            lMain.simulationSpace = ParticleSystemSimulationSpace.World;
            lMain.scalingMode = ParticleSystemScalingMode.Shape;
            ParticleSystem.EmissionModule lEmission = lSystem.emission;
            lEmission.rateOverTime = 120f;
            ParticleSystem.ShapeModule lShape = lSystem.shape;
            lShape.shapeType = ParticleSystemShapeType.Cone;
            lShape.angle = 9f;
            lShape.radius = 0.03f;
            ParticleSystem.SizeOverLifetimeModule lSize = lSystem.sizeOverLifetime;
            lSize.enabled = true;
            lSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.4f, 1, 4f));
            ParticleSystem.ColorOverLifetimeModule lColor = lSystem.colorOverLifetime;
            lColor.enabled = true;
            Gradient lGradient = new Gradient();
            lGradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.8f, 1f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0.15f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 0.6f), new GradientColorKey(new Color(0.2f, 0.1f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            lColor.color = lGradient;
            ParticleSystemRenderer lRenderer = lJet.GetComponent<ParticleSystemRenderer>();
            lRenderer.sharedMaterial = GameArt.ParticleAdd;
            lRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            lRenderer.mesh = LowPolyMesh.Cached("fx.blob", () => LowPolyMesh.Icosphere(0));
            lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lSystem.Play();

            Light lLight = new GameObject("JetLight").AddComponent<Light>();
            lLight.transform.SetParent(lJet.transform, false);
            lLight.transform.localPosition = new Vector3(0, 0, 3f);
            lLight.color = new Color(1f, 0.55f, 0.2f);
            lLight.range = 10f;
            lLight.intensity = 6f;
            lLight.gameObject.AddComponent<Flicker>();
            return lJet;
        }
    }

    /// <summary>A character on fire: damage over time, a fire on its body, panic (NPCs run around).</summary>
    public class Burning : MonoBehaviour
    {
        private Damageable _target;
        private ulong _attacker;
        private int _team;
        private float _time;
        private float _tick;

        public static bool IsBurning(Component pTarget) => pTarget != null && pTarget.GetComponent<Burning>() != null;

        /// <summary>Host side.</summary>
        public static void Apply(Damageable pTarget, ulong pAttacker, int pTeam)
        {
            Burning lBurning = pTarget.GetComponent<Burning>();
            if (lBurning == null)
            {
                lBurning = pTarget.gameObject.AddComponent<Burning>();
                if (MapEffects.Instance != null) BurnVisuals.Broadcast(pTarget, true);
            }
            lBurning._target = pTarget;
            lBurning._attacker = pAttacker;
            lBurning._team = pTeam;
            lBurning._time = 4f;
        }

        private void Update()
        {
            _time -= Time.deltaTime;
            _tick -= Time.deltaTime;
            if (_target == null || !_target.IsAlive || _time <= 0f || Sprinkler.IsWet(transform.position))
            {
                if (_target != null) BurnVisuals.Broadcast(_target, false);
                Destroy(this);
                return;
            }
            if (_tick > 0f) return;
            _tick = 0.5f;
            _target.ApplyDamage(new DamageInfo { amount = 9f, type = DamageType.Fire, point = transform.position + Vector3.up, direction = Vector3.up, attacker = _attacker, attackerTeam = _team });
        }
    }
}
