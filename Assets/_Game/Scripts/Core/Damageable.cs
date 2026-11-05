using System;
using Com.IsartDigital.GoobyrFPS.Match;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Core
{
    public enum DamageType : byte
    {
        Bullet,
        Explosion,
        Fire,
        Melee,
        Poison,
        Gas,
        Crush,
        Fall,
        Drown,
        Electric
    }

    /// <summary>One hit, as the host resolves it.</summary>
    public struct DamageInfo
    {
        public float amount;
        public DamageType type;
        public Vector3 point;
        public Vector3 direction;
        /// <summary>Client id of the attacking player, or <see cref="NO_PLAYER"/> for NPCs and the world.</summary>
        public ulong attacker;
        public int attackerTeam;
        public bool headshot;
        /// <summary>Knock out instead of hurting (takedowns, thrown bricks).</summary>
        public bool knockout;

        public const ulong NO_PLAYER = ulong.MaxValue;

        public static DamageInfo World(float pAmount, DamageType pType, Vector3 pPoint, Vector3 pDirection) => new DamageInfo
        {
            amount = pAmount, type = pType, point = pPoint, direction = pDirection, attacker = NO_PLAYER, attackerTeam = MatchRulesLogic.NO_TEAM
        };
    }

    /// <summary>
    /// Health of anything alive - players and NPCs. Only the host changes it; clients watch
    /// <see cref="Health"/> and play their hit feedback from <see cref="OnHitClient"/>.
    /// </summary>
    public class Damageable : NetworkBehaviour
    {
        public readonly NetworkVariable<float> Health = new NetworkVariable<float>(100f);
        public readonly NetworkVariable<bool> Dead = new NetworkVariable<bool>(false);

        public float MaxHealth { get; private set; } = 100f;
        /// <summary>Team used for friendly fire. NPCs are on their own team (-2).</summary>
        public int Team { get; set; } = NPC_TEAM;
        /// <summary>Where the head is now (headshots): the body's head bone when it has one, else the standing contract height.</summary>
        public Func<Vector3> HeadCentre { get; set; }

        public const int NPC_TEAM = -2;

        /// <summary>Host side, after the hit is applied.</summary>
        public event Action<DamageInfo> OnDamagedServer;
        /// <summary>Host side, the hit that killed.</summary>
        public event Action<DamageInfo> OnDeathServer;
        /// <summary>Every machine: amount, world point, direction, headshot.</summary>
        public event Action<float, Vector3, Vector3, bool> OnHitClient;
        /// <summary>Every machine: impulse and point of the killing blow.</summary>
        public event Action<Vector3, Vector3, DamageType> OnDeathClient;

        public bool IsAlive => !Dead.Value;

        public void InitHealth(float pMax)
        {
            MaxHealth = pMax;
            if (IsServer)
            {
                Health.Value = pMax;
                Dead.Value = false;
            }
        }

        /// <summary>A hit on the head: from the shoulders up, forgiving on purpose (a hat or hair counts).</summary>
        public bool IsHeadshot(Vector3 pPoint)
        {
            Vector3 lHead = HeadCentre != null ? HeadCentre() : transform.position + Vector3.up * BodyMetrics.HEAD_CENTRE * transform.lossyScale.y;
            float lRadius = BodyMetrics.HEAD_HIT_RADIUS * transform.lossyScale.y;
            Vector3 lOffset = pPoint - lHead;
            return lOffset.y > -lRadius && lOffset.x * lOffset.x + lOffset.z * lOffset.z < lRadius * lRadius * 2.5f;
        }

        /// <summary>Host only (dev cheat): nothing hurts.</summary>
        public bool Invincible { get; set; }

        /// <summary>Host only: what the victim's stance makes of a hit (a raised guard against a punch).</summary>
        public System.Func<DamageInfo, float> DamageScale;

        /// <summary>Host only. Returns true if the hit killed.</summary>
        public bool ApplyDamage(DamageInfo pInfo)
        {
            if (!IsServer || Dead.Value || Invincible) return false;
            if (pInfo.attackerTeam != MatchRulesLogic.NO_TEAM && Team != NPC_TEAM && pInfo.attacker != DamageInfo.NO_PLAYER
                && !MatchRulesLogic.CanDamage(pInfo.attackerTeam, Team)) return false;

            if (DamageScale != null) pInfo.amount *= DamageScale(pInfo);
            if (pInfo.amount <= 0f) return false;
            Health.Value = Mathf.Max(0f, Health.Value - pInfo.amount);
            OnDamagedServer?.Invoke(pInfo);
            HitClientRpc(pInfo.amount, pInfo.point, pInfo.direction, pInfo.headshot);
            bool lByPlayer = pInfo.attacker != DamageInfo.NO_PLAYER;

            if (Health.Value > 0f)
            {
                // Bullets and punches are already felt on the shooter's machine; blasts and flames are not.
                if (lByPlayer && pInfo.type != DamageType.Bullet && pInfo.type != DamageType.Melee) ConfirmRpc(false, false, RpcTarget.Single(pInfo.attacker, RpcTargetUse.Temp));
                return false;
            }

            if (lByPlayer) ConfirmRpc(pInfo.headshot, true, RpcTarget.Single(pInfo.attacker, RpcTargetUse.Temp));
            Dead.Value = true;
            OnDeathServer?.Invoke(pInfo);
            float lForce = pInfo.type == DamageType.Explosion ? 30f : pInfo.type == DamageType.Bullet ? 9f : pInfo.type == DamageType.Melee ? 6f : 3f;
            DeathClientRpc(pInfo.direction.normalized * lForce + Vector3.up * lForce * 0.3f, pInfo.point, pInfo.type);
            return true;
        }

        /// <summary>Host only: straight to zero (vent gas, drowning).</summary>
        public void Kill(DamageInfo pInfo)
        {
            pInfo.amount = Health.Value + 1f;
            ApplyDamage(pInfo);
        }

        public void Heal(float pAmount)
        {
            if (IsServer && !Dead.Value) Health.Value = Mathf.Min(MaxHealth, Health.Value + pAmount);
        }

        /// <summary>To the shooter only: the host's word on a hit it could not predict, and on every kill.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void ConfirmRpc(bool pHead, bool pKill, RpcParams pParams = default) => OnConfirmed?.Invoke(pHead, pKill);

        /// <summary>The shooter's machine: head, kill.</summary>
        public static event Action<bool, bool> OnConfirmed;

        [Rpc(SendTo.Everyone)]
        private void HitClientRpc(float pAmount, Vector3 pPoint, Vector3 pDirection, bool pHeadshot) => OnHitClient?.Invoke(pAmount, pPoint, pDirection, pHeadshot);

        [Rpc(SendTo.Everyone)]
        private void DeathClientRpc(Vector3 pImpulse, Vector3 pPoint, DamageType pType) => OnDeathClient?.Invoke(pImpulse, pPoint, pType);
    }
}
