using Com.IsartDigital.GoobyrFPS.Audio;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// The owner's movement item, on its own key: a grappling hook that pulls you to what you aim at, a
    /// small jetpack that lifts while the key is held, spring boots that throw you up and forward. Movement
    /// is the owner's: only the noise and the rope seen by others go through the network.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class MovementGear : MonoBehaviour
    {
        public const float GRAPPLE_RANGE = 45f, GRAPPLE_COOLDOWN = 3.5f;
        public const float SPRING_UP = 11.5f, SPRING_FORWARD = 5f, SPRING_COOLDOWN = 3f;
        public const float JET_FUEL = 1.6f, JET_REFILL = 0.3f;

        private PlayerAvatar _avatar;
        private float _ready, _fuel = JET_FUEL, _nextPuff;
        private LineRenderer _rope;

        /// <summary>For the HUD: seconds until the item can be used again, 0 when ready.</summary>
        public float Cooldown => Mathf.Max(0f, _ready - Time.time);
        /// <summary>For the HUD: the jetpack's fuel, 1 = full.</summary>
        public float Fuel => _fuel / JET_FUEL;

        private void Awake() => _avatar = GetComponent<PlayerAvatar>();

        private void Update()
        {
            ItemDefinition lItem = _avatar.Weapons != null ? _avatar.Weapons.ItemIn(WeaponHolder.MOVE_SLOT) : null;
            FpsMotor lMotor = _avatar.Motor;
            bool lJetting = false;
            if (lItem != null && _avatar.Health.IsAlive && lMotor.Mode == MotorMode.Walk)
            {
                switch (lItem.kind)
                {
                    case ItemKind.Grapple:
                        if (GameInput.MoveItemPressed && Time.time >= _ready) FireGrapple(lMotor);
                        break;
                    case ItemKind.SpringBoots:
                        if (GameInput.MoveItemPressed && Time.time >= _ready && lMotor.Grounded)
                        {
                            _ready = Time.time + SPRING_COOLDOWN;
                            lMotor.Launch(transform.forward * SPRING_FORWARD + Vector3.up * SPRING_UP, false);
                            Sfx.Play("jump", transform.position, 1f);
                            Effects.Dust(transform.position, 1f);
                            Juice.Shake(0.1f);
                            _avatar.GearNoiseRpc(transform.position, 6f);
                        }
                        break;
                    case ItemKind.JetPack:
                        if (GameInput.MoveItemHeld && _fuel > 0f)
                        {
                            lJetting = true;
                            _fuel = Mathf.Max(0f, _fuel - Time.deltaTime);
                            lMotor.Thrust(Time.deltaTime);
                            if (Time.time >= _nextPuff)
                            {
                                _nextPuff = Time.time + 0.09f;
                                Effects.Muzzle(transform.position + Vector3.up * 0.6f - transform.forward * 0.35f, Vector3.down, 0.6f);
                                _avatar.GearNoiseRpc(transform.position, 7f);
                            }
                        }
                        break;
                }
            }
            if (!lJetting && lMotor.Grounded) _fuel = Mathf.Min(JET_FUEL, _fuel + JET_FUEL * JET_REFILL * Time.deltaTime);
            UpdateRope(lMotor);
        }

        private void FireGrapple(FpsMotor pMotor)
        {
            Vector3 lFrom = _avatar.AimOrigin, lDir = _avatar.AimDirection;
            if (!Physics.Raycast(lFrom, lDir, out RaycastHit lHit, GRAPPLE_RANGE, GameLayers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                Sfx.Play2D("dryfire", 0.6f);
                return;
            }
            _ready = Time.time + GRAPPLE_COOLDOWN;
            pMotor.StartGrapple(lHit.point);
            Sfx.Play("swing", lFrom, 1f);
            _avatar.GrappleRopeRpc(lHit.point);
            _avatar.GearNoiseRpc(transform.position, 5f);
        }

        private void UpdateRope(FpsMotor pMotor)
        {
            bool lOn = pMotor.Grappling;
            if (lOn && _rope == null) _rope = NewRope(transform);
            if (_rope != null)
            {
                _rope.enabled = lOn;
                if (lOn)
                {
                    _rope.SetPosition(0, _avatar.AimOrigin + _avatar.AimDirection * 0.5f + Vector3.down * 0.25f + transform.right * 0.2f);
                    _rope.SetPosition(1, pMotor.GrapplePoint);
                }
            }
        }

        public static LineRenderer NewRope(Transform pParent)
        {
            LineRenderer lRope = new GameObject("Rope").AddComponent<LineRenderer>();
            lRope.transform.SetParent(pParent, false);
            lRope.useWorldSpace = true;
            lRope.positionCount = 2;
            lRope.startWidth = lRope.endWidth = 0.035f;
            lRope.sharedMaterial = GameArt.Glow;
            lRope.startColor = lRope.endColor = new Color(0.25f, 0.22f, 0.2f);
            lRope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lRope;
        }
    }
}
