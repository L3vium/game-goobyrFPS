using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>Something a bullet does more than stop on: chandelier chains, barrels, tanks, glass.</summary>
    public interface IShootable
    {
        /// <summary>Host side.</summary>
        void OnShotServer(DamageInfo pInfo);
    }

    /// <summary>
    /// Shot geometry shared by the owner's prediction and the host's resolution: the same seed
    /// gives the same pellets on both, so tracers match the damage.
    /// </summary>
    public static class Ballistics
    {
        public static List<Vector3> Pellets(ItemDefinition pItem, Vector3 pDirection, int pSeed, bool pAiming)
        {
            List<Vector3> lResult = new List<Vector3>(pItem.pellets);
            System.Random lRandom = new System.Random(pSeed);
            float lSpread = pItem.spread * (pAiming ? pItem.adsSpreadFactor : 1f);
            Quaternion lBase = Quaternion.LookRotation(pDirection);
            for (int i = 0; i < Mathf.Max(1, pItem.pellets); i++)
            {
                float lAngle = (float)lRandom.NextDouble() * Mathf.PI * 2f;
                float lRadius = Mathf.Sqrt((float)lRandom.NextDouble()) * lSpread;
                lResult.Add(lBase * Quaternion.Euler(Mathf.Sin(lAngle) * lRadius, Mathf.Cos(lAngle) * lRadius, 0) * Vector3.forward);
            }
            return lResult;
        }

        /// <summary>
        /// Host: traces one pellet, damages what it hits, returns the end point. Ignores the
        /// shooter's own colliders (<paramref name="pIgnore"/>).
        /// </summary>
        public static Vector3 TraceServer(Vector3 pOrigin, Vector3 pDirection, ItemDefinition pItem, float pDamage, ulong pAttacker, int pTeam, Transform pIgnore)
        {
            RaycastHit[] lHits = Physics.RaycastAll(pOrigin, pDirection, pItem.range, GameLayers.ShotMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(lHits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit lHit in lHits)
            {
                if (pIgnore != null && lHit.collider.transform.IsChildOf(pIgnore)) continue;

                DamageInfo lInfo = new DamageInfo
                {
                    amount = pDamage, type = DamageType.Bullet, point = lHit.point, direction = pDirection,
                    attacker = pAttacker, attackerTeam = pTeam
                };

                IShootable lShootable = lHit.collider.GetComponentInParent<IShootable>();
                lShootable?.OnShotServer(lInfo);

                Damageable lTarget = lHit.collider.GetComponentInParent<Damageable>();
                if (lTarget != null)
                {
                    if (!lTarget.IsAlive) continue;
                    lInfo.headshot = lTarget.IsHeadshot(lHit.point);
                    if (lInfo.headshot) lInfo.amount *= pItem.headMultiplier;
                    lTarget.ApplyDamage(lInfo);
                }
                else NoiseSystem.Emit(lHit.point, 7f, NoiseKind.Suspicious, pAttacker);
                return lHit.point;
            }
            return pOrigin + pDirection * pItem.range;
        }

        /// <summary>
        /// Host: an explosion - damage falling off with distance (behind cover is safer), physics
        /// push, ignition, noise for the whole neighbourhood, and the visuals on every machine.
        /// </summary>
        public static void ExplodeServer(Vector3 pPosition, float pRadius, float pDamage, ulong pAttacker, int pTeam)
        {
            HashSet<Damageable> lDone = new HashSet<Damageable>();
            foreach (Collider lCollider in Physics.OverlapSphere(pPosition, pRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Damageable lTarget = lCollider.GetComponentInParent<Damageable>();
                Vector3 lPoint = lCollider.ClosestPoint(pPosition);
                float lDistance = Vector3.Distance(pPosition, lPoint);
                float lFalloff = 1f - Mathf.Clamp01(lDistance / pRadius);

                if (lTarget != null && lDone.Add(lTarget) && lTarget.IsAlive)
                {
                    Vector3 lCenter = lTarget.transform.position + Vector3.up;
                    bool lCovered = Physics.Linecast(pPosition + Vector3.up * 0.3f, lCenter, GameLayers.SightBlockMask, QueryTriggerInteraction.Ignore);
                    float lAmount = pDamage * lFalloff * (lCovered ? 0.35f : 1f);
                    lTarget.ApplyDamage(new DamageInfo
                    {
                        amount = lAmount, type = DamageType.Explosion, point = lPoint, direction = (lCenter - pPosition).normalized,
                        attacker = pAttacker, attackerTeam = pTeam
                    });
                    Player.PlayerAvatar lAvatar = lTarget.GetComponent<Player.PlayerAvatar>();
                    if (lAvatar != null && lTarget.IsAlive) lAvatar.PushOwnerRpc(((lCenter - pPosition).normalized + Vector3.up * 0.6f) * 9f * lFalloff);
                }

                Rigidbody lBody = lCollider.attachedRigidbody;
                if (lBody != null && !lBody.isKinematic) lBody.AddExplosionForce(900f, pPosition, pRadius * 1.5f, 1f);

                lCollider.GetComponentInParent<IShootable>()?.OnShotServer(new DamageInfo { amount = pDamage * lFalloff, type = DamageType.Explosion, point = lPoint, attacker = pAttacker, attackerTeam = pTeam });
                Flammable lFlammable = lCollider.GetComponentInParent<Flammable>();
                if (lFlammable != null && lFalloff > 0.2f) lFlammable.IgniteServer();
            }
            NoiseSystem.Emit(pPosition, Mathf.Max(80f, pRadius * 12f), NoiseKind.Alarming, pAttacker);
            if (MapEffects.Instance != null) MapEffects.Instance.ExplosionRpc(pPosition, pRadius);
        }
    }
}
