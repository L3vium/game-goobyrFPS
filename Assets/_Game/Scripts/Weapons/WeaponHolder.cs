using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Audio;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Systems;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// A player's inventory and everything they do with it: shoot, reload, punch, throw, plant
    /// and blow C4, burn with the flamethrower, pick up and drop.
    /// <para>
    /// Bare hands (key 1, always there), three weapon slots (keys 2-4), a movement item and a distraction,
    /// each on its own key. Keys and tools sit in hidden slots after them: they open things, they are never held.
    /// The host owns the slot list and the ammo; the owner predicts the shot locally (muzzle,
    /// tracer, recoil, sound) and sends origin + direction + spread seed, the host re-traces the
    /// same pellets, deals the damage and replays the shot on the other machines.
    /// Damage is high both ways - a room can be cleared, and a room can clear you.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class WeaponHolder : NetworkBehaviour
    {
        public const int SLOT_COUNT = 5;
        public const int KEY_SLOTS = 3;
        public const int WEAPON_SLOTS = 3;
        public const int MOVE_SLOT = 3, DISTRACTION_SLOT = 4;
        /// <summary>The value of <see cref="Selected"/> with nothing in hand.</summary>
        public const int FISTS = -1;

        public readonly NetworkList<InventorySlot> Slots = new NetworkList<InventorySlot>();
        public readonly NetworkVariable<int> Selected = new NetworkVariable<int>(FISTS, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Flaming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Reloading = new NetworkVariable<bool>(false);

        public PlayerAvatar Avatar { get; private set; }
        public Viewmodel View { get; private set; }

        public ItemDefinition HeldItem => ItemIn(Selected.Value);
        public bool HoldsWeapon => HeldItem != null && HeldItem.slot == ItemSlot.Weapon;
        public float AdsFov => _aiming && HeldItem != null && HeldItem.IsGun ? HeldItem.adsFov : 0f;
        /// <summary>Owner: the held weapon is raised (guns, the launcher, the flamethrower).</summary>
        public bool IsAiming => _aiming;
        public float LookSensitivityFactor => _aiming && HeldItem != null ? Mathf.Clamp(HeldItem.adsFov / 78f, 0.25f, 1f) : 1f;
        public bool Scoped => _aiming && HeldItem != null && HeldItem.kind == ItemKind.Sniper;

        private float _nextFire;
        private float _nextMelee;
        private float _nextMeleeServer;
        private float _nextScroll;
        // Owner: a punch or takedown in flight - resolved at the clip's contact time, not on the click.
        private bool _strikePending;
        private float _strikeAt;
        private Viewmodel.MeleeMove _strikeMove;
        private bool _nextCross;
        private bool _predictedHit;
        // Owner: a throw in flight - the item leaves at the clip's release time.
        private int _throwSlot = -1;
        private float _throwAt;

        /// <summary>Melee reach: a sphere this wide swept this far from the eye (owner prediction and host).</summary>
        public const float MELEE_RADIUS = 0.35f, MELEE_RANGE = 1.7f;
        /// <summary>Owner cooldown between swings; the host refuses swings closer than its own minimum.</summary>
        public const float MELEE_COOLDOWN = 0.18f, TAKEDOWN_COOLDOWN = 0.6f, MELEE_MIN_INTERVAL = 0.12f;
        /// <summary>A bare punch's damage; a blade or a club deals its item's.</summary>
        public const float FIST_DAMAGE = 34f;
        /// <summary>Predicted contact: how long the arms freeze, and the camera's jolt.</summary>
        public const float HIT_STOP = 0.07f;
        /// <summary>Tools only (first-person shots): aim as if the button were held.</summary>
        public static bool ForceAim;
        private ThrowArc _arc;
        private bool _aiming;
        private GameObject _thirdPersonModel;
        private int _thirdPersonItem = -2;
        private GameObject _flameFx;
        private float _flameTick;
        private readonly List<NetworkObject> _charges = new List<NetworkObject>();
        private System.Random _spreadRandom = new System.Random();

        private void Awake()
        {
            Avatar = GetComponent<PlayerAvatar>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                for (int i = 0; i < SLOT_COUNT + KEY_SLOTS; i++) Slots.Add(InventorySlot.Empty);
                GiveLoadoutServer();
            }
            if (IsOwner)
            {
                View = Viewmodel.Create(this);
                Selected.Value = FISTS;
            }
            Avatar.Health.OnDeathServer += HandleDeathServer;
            Selected.OnValueChanged += (_, __) => RefreshThirdPerson();
        }

        /// <summary>Host: what the agent chose in its loadout (a starting gun is lightly loaded: one magazine in it, one spare).</summary>
        private void GiveLoadoutServer()
        {
            Net.LobbyPlayer lPlayer = Avatar.Player;
            if (lPlayer == null) return;
            Net.AgentLook lLook = lPlayer.Look.Value;
            foreach (short lIndex in new[] { lLook.weapon, lLook.movement, lLook.distraction })
            {
                ItemDefinition lItem = GameDatabase.Item(lIndex);
                if (lItem != null && lItem.inLoadout) GiveServer(lItem, -1, lItem.UsesAmmo ? lItem.magazine : -1);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (View != null) Destroy(View.gameObject);
            if (_arc != null) Destroy(_arc.gameObject);
            if (_flameFx != null) Destroy(_flameFx);
        }

        /// <summary>Is an item of this kind anywhere in the inventory?</summary>
        public bool Carries(ItemKind pKind)
        {
            if (Slots == null) return false;
            for (int i = 0; i < Slots.Count; i++)
            {
                ItemDefinition lItem = GameDatabase.Item(Slots[i].item);
                if (lItem != null && lItem.kind == pKind) return true;
            }
            return false;
        }

        /// <summary>The best pass carried: keycard over staff pass over invitation.</summary>
        public AI.AccessLevel Access => Carries(ItemKind.Keycard) ? AI.AccessLevel.Security : Carries(ItemKind.StaffPass) ? AI.AccessLevel.Staff : Carries(ItemKind.Invitation) ? AI.AccessLevel.Guest : AI.AccessLevel.None;

        public InventorySlot SlotAt(int pSlot) => Slots != null && pSlot >= 0 && pSlot < Slots.Count ? Slots[pSlot] : InventorySlot.Empty;
        public ItemDefinition ItemIn(int pSlot) => GameDatabase.Item(SlotAt(pSlot).item);

        // ---------------------------------------------------------------- owner input

        private void Update()
        {
            if (!IsSpawned) return;
            UpdateThirdPersonFlame();
            if (!IsOwner || !Avatar.Health.IsAlive) { if (_arc != null) _arc.Hide(); return; }
            if (Avatar.Motor.Mode == MotorMode.Hidden || Avatar.Motor.Mode == MotorMode.Zipline) { _aiming = false; if (_arc != null) _arc.Hide(); return; }

            if (_strikePending && Time.time >= _strikeAt) Strike();
            if (_throwSlot >= 0 && Time.time >= _throwAt) Release();

            // Key 1 is bare hands, keys 2 to 4 the weapons.
            int lSlot = GameInput.SlotPressed;
            if (lSlot == 0) Select(FISTS);
            else if (lSlot > 0) Select(lSlot - 1);
            float lScroll = GameInput.Scroll;
            if (Mathf.Abs(lScroll) > 0.1f && Time.time >= _nextScroll)
            {
                _nextScroll = Time.time + 0.12f;
                Cycle(lScroll < 0 ? 1 : -1);
            }

            // Left click fires, from the hip or (right click held) aimed.
            ItemDefinition lHeld = HeldItem;
            bool lShooter = lHeld != null && (lHeld.IsGun || lHeld.IsLauncher || lHeld.kind == ItemKind.Flamethrower);
            _aiming = (GameInput.Aim || ForceAim) && lShooter;
            // Firing from the hip breaks a sprint just like aiming does.
            Avatar.Motor.Aiming = _aiming || (lShooter && GameInput.Fire);
            bool lBlocking = GameInput.BlockHeld && !_aiming;
            if (Avatar.Blocking.Value != lBlocking) Avatar.Blocking.Value = lBlocking;
            if (Avatar.Aiming.Value != _aiming) Avatar.Aiming.Value = _aiming;

            TakedownReady = PredictTakedown();
            if (GameInput.TakedownPressed && TakedownReady) TryMelee(true);
            if (GameInput.Reload && lHeld != null && lHeld.UsesAmmo) ReloadRpc(Selected.Value);
            if (GameInput.Melee) TryMelee();
            if (GameInput.Throw)
            {
                // The distraction's key: thrown at once; what can't be thrown (a poison) comes to hand.
                ItemDefinition lDistraction = ItemIn(DISTRACTION_SLOT);
                if (lDistraction != null && lDistraction.IsThrowable) ThrowGadget(DISTRACTION_SLOT);
                else if (lDistraction != null) Select(DISTRACTION_SLOT);
            }
            if (GameInput.Detonate) DetonateRpc();

            bool lFlaming = false;
            bool lArc = lHeld != null && lHeld.IsThrowable && GameInput.Aim;
            if (lArc)
            {
                if (_arc == null) _arc = ThrowArc.Create();
                Vector3 lDir = Avatar.AimDirection;
                _arc.Show(Avatar.AimOrigin + lDir * 0.5f, lDir.normalized * lHeld.throwForce + Vector3.up * 2.5f);
            }
            else if (_arc != null) _arc.Hide();

            // Left click attacks, whatever is held.
            if (lHeld != null && lHeld.slot == ItemSlot.Gadget)
            {
                // Gadgets: the right button shows the arc, letting it go throws; left click still punches.
                if (GameInput.AimReleased) ThrowGadget(Selected.Value);
                if (GameInput.FirePressed) TryMelee();
            }
            else if (lHeld == null || lHeld.IsMelee)
            {
                if (GameInput.FirePressed) TryMelee();
            }
            else if (lHeld.kind == ItemKind.Flamethrower)
            {
                lFlaming = GameInput.Fire && SlotAt(Selected.Value).magazine > 0 && !Reloading.Value;
            }
            else if (lHeld.automatic ? GameInput.Fire : GameInput.FirePressed) TryFire(lHeld);

            if (Flaming.Value != lFlaming) Flaming.Value = lFlaming;
        }

        /// <summary>
        /// The mouse wheel: bare hands, then the weapons, wrapping round.
        /// </summary>
        private void Cycle(int pStep)
        {
            const int STOPS = WEAPON_SLOTS + 1;
            int lFrom = Selected.Value >= WEAPON_SLOTS ? 0 : Selected.Value + 1;
            for (int n = 1; n <= STOPS; n++)
            {
                int lStop = ((lFrom + pStep * n) % STOPS + STOPS) % STOPS;
                if (lStop == 0 || !SlotAt(lStop - 1).IsEmpty) { Select(lStop - 1); return; }
            }
        }

        public void Select(int pSlot)
        {
            if (pSlot < FISTS || pSlot >= SLOT_COUNT || pSlot == Selected.Value) return;
            if (pSlot != FISTS && SlotAt(pSlot).IsEmpty) return;
            Selected.Value = pSlot;
            View?.PlaySwitch();
            Sfx.Play2D("switch", 0.5f);
        }

        private void TryFire(ItemDefinition pItem)
        {
            if (Time.time < _nextFire || Reloading.Value) return;
            InventorySlot lSlot = SlotAt(Selected.Value);
            if (lSlot.magazine <= 0)
            {
                _nextFire = Time.time + 0.25f;
                Sfx.Play2D("dryfire", 0.6f);
                if (lSlot.reserve > 0) ReloadRpc(Selected.Value);
                return;
            }
            _nextFire = Time.time + 1f / Mathf.Max(0.1f, pItem.fireRate);

            Vector3 lOrigin = Avatar.AimOrigin;
            Vector3 lDirection = Avatar.AimDirection;
            int lSeed = _spreadRandom.Next();
            Vector3 lMuzzle = View != null ? View.MuzzlePosition : lOrigin;

            if (pItem.IsLauncher)
            {
                Effects.Muzzle(lMuzzle, lDirection, 2f);
                Juice.Shake(0.4f);
            }
            else
            {
                // Local prediction of the pellets: tracers and impacts right now.
                List<Vector3> lPellets = Ballistics.Pellets(pItem, lDirection, lSeed, _aiming);
                foreach (Vector3 lDir in lPellets)
                {
                    Vector3 lEnd = lOrigin + lDir * pItem.range;
                    if (Ballistics.Trace(lOrigin, lDir, pItem.range, transform, out RaycastHit lHit))
                    {
                        lEnd = lHit.point;
                        Damageable lTarget = lHit.collider.GetComponentInParent<Damageable>();
                        if (lTarget != null && lTarget.IsAlive)
                        {
                            bool lHead = lTarget.IsHeadshot(lHit.point);
                            UI.Hud.Hitmarker(lHead);
                            Effects.Fluff(lHit.point, -lDir, Effects.Blood, lHead);
                        }
                        else Effects.Impact(lHit.point, lHit.normal, new Color(0.75f, 0.7f, 0.6f));
                    }
                    Effects.Tracer(lMuzzle, lEnd, new Color(1f, 0.85f, 0.5f, 0.9f));
                }
                Effects.Muzzle(lMuzzle, lDirection, pItem.kind is ItemKind.Shotgun or ItemKind.SawedOff ? 1.6f : pItem.kind is ItemKind.SilencedPistol or ItemKind.Crossbow ? 0.5f : 1f);
                Juice.Shake(pItem.shake);
            }

            View?.Kick(pItem.recoil);
            Avatar.SetLook(Avatar.LookYaw + Random.Range(-0.25f, 0.25f) * pItem.recoil, Avatar.LookPitch - pItem.recoil * (_aiming ? 0.45f : 0.8f));
            Juice.FovKick(pItem.recoil * 0.6f);
            Sfx.Play(SoundFor(pItem), lMuzzle, 1f, pItem.noiseRadius * 2f);
            FireRpc(Selected.Value, lOrigin, lDirection, lSeed, _aiming);
        }

        public static string SoundFor(ItemDefinition pItem) => pItem.kind switch
        {
            ItemKind.SilencedPistol => "shot_silenced",
            ItemKind.Pistol or ItemKind.Revolver => "shot_pistol",
            ItemKind.Smg or ItemKind.AssaultRifle => "shot_smg",
            ItemKind.Shotgun or ItemKind.SawedOff => "shot_shotgun",
            ItemKind.Sniper => "shot_sniper",
            ItemKind.Crossbow => "shot_silenced",
            ItemKind.RocketLauncher or ItemKind.GrenadeLauncher => "rocket_launch",
            _ => "shot_pistol"
        };

        /// <summary>
        /// Left click with nothing raised (or the melee key): the punch plays at once - jab and cross
        /// alternating bare-handed, a left jab holding something - or the takedown key behind an unaware
        /// NPC; the hit is resolved at the clip's contact time (<see cref="Strike"/>).
        /// </summary>
        private void TryMelee(bool pTakedown = false)
        {
            if (Time.time < _nextMelee || _strikePending || _throwSlot >= 0) return;
            Viewmodel.MeleeMove lMove;
            if (pTakedown) lMove = Viewmodel.MeleeMove.Takedown;
            else if (HeldItem == null) { lMove = _nextCross ? Viewmodel.MeleeMove.Cross : Viewmodel.MeleeMove.Jab; _nextCross = !_nextCross; }
            else lMove = Viewmodel.MeleeMove.Jab;
            float lStrike = View != null ? View.PlayMelee(lMove) : Viewmodel.DefaultStrike(lMove);
            _nextMelee = Time.time + (lMove == Viewmodel.MeleeMove.Takedown ? TAKEDOWN_COOLDOWN : MELEE_COOLDOWN);
            _strikePending = true;
            _strikeAt = Time.time + lStrike;
            _strikeMove = lMove;
            Sfx.Play("swing", transform.position, 0.6f);
            SwingRpc();
        }

        /// <summary>Owner: an NPC's unaware back is in reach, the takedown key would kill it.</summary>
        public bool TakedownReady { get; private set; }

        /// <summary>The same test the host makes: an NPC that hasn't noticed us, seen from behind, in reach.</summary>
        private bool PredictTakedown()
        {
            if (Avatar.IsProtector) return false;
            if (!Physics.SphereCast(Avatar.AimOrigin, MELEE_RADIUS, Avatar.AimDirection, out RaycastHit lHit, MELEE_RANGE, GameLayers.CharacterMask, QueryTriggerInteraction.Ignore)) return false;
            Npc lNpc = lHit.collider.GetComponentInParent<Npc>();
            if (lNpc == null || !lNpc.Health.IsAlive) return false;
            float lFromBack = Vector3.Angle(lNpc.transform.forward, Vector3.ProjectOnPlane(Avatar.AimDirection, Vector3.up));
            return DetectionModel.CanTakedown(lFromBack, lNpc.Gauge, lNpc.IsAlerted);
        }

        /// <summary>
        /// Contact: the owner predicts the hit (arms freeze for a beat, the camera jolts, the victim
        /// squashes, the punch is heard) and sends the swing to the host, which judges it.
        /// </summary>
        private void Strike()
        {
            _strikePending = false;
            Vector3 lOrigin = Avatar.AimOrigin, lDirection = Avatar.AimDirection;
            _predictedHit = false;
            if (Physics.SphereCast(lOrigin, MELEE_RADIUS, lDirection, out RaycastHit lHit, MELEE_RANGE, GameLayers.CharacterMask, QueryTriggerInteraction.Ignore))
            {
                Damageable lTarget = lHit.collider.GetComponentInParent<Damageable>();
                if (lTarget != null && lTarget != Avatar.Health && lTarget.IsAlive)
                {
                    _predictedHit = true;
                    bool lTakedown = _strikeMove == Viewmodel.MeleeMove.Takedown;
                    View?.HitStop(lTakedown ? HIT_STOP * 1.6f : HIT_STOP);
                    CameraRig.Instance?.Kick(lTakedown ? new Vector3(1.5f, 0f, 0f) : new Vector3(-1.6f, _strikeMove == Viewmodel.MeleeMove.Jab ? 1.1f : -1.4f, _strikeMove == Viewmodel.MeleeMove.Jab ? -1.2f : 1.5f));
                    if (!lTakedown)
                    {
                        Juice.Shake(0.08f);
                        SquashBody(lTarget.transform);
                        Sfx.Play("punch", lHit.point, 1f, 20f);
                        Effects.Fluff(lHit.point, -lDirection, Color.white, false);
                        UI.Hud.Hitmarker(false);
                    }
                }
            }
            MeleeRpc(lOrigin, lDirection, _strikeMove == Viewmodel.MeleeMove.Takedown);
        }

        private void ThrowGadget(int pSlot)
        {
            ItemDefinition lItem = ItemIn(pSlot);
            if (lItem == null || lItem.slot != ItemSlot.Gadget || !lItem.IsThrowable || _throwSlot >= 0 || _strikePending) return;
            Sfx.Play("throw", transform.position, 0.6f);
            ThrowAnimRpc();
            if (pSlot != Selected.Value)
            {
                // Quick throw of a gadget not in hand (throw key): it goes at once.
                View?.PlayThrow();
                ThrowRpc(pSlot, Avatar.AimOrigin + Avatar.AimDirection * 0.5f, Avatar.AimDirection);
                return;
            }
            // The held item leaves the hand at the clip's release, aimed where the player looks then.
            float lRelease = View != null ? View.PlayThrow() : 0.2f;
            _throwSlot = pSlot;
            _throwAt = Time.time + lRelease;
        }

        private void Release()
        {
            int lSlot = _throwSlot;
            _throwSlot = -1;
            ThrowRpc(lSlot, Avatar.AimOrigin + Avatar.AimDirection * 0.5f, Avatar.AimDirection);
        }

        // ---------------------------------------------------------------- host

        [Rpc(SendTo.Server)]
        private void FireRpc(int pSlot, Vector3 pOrigin, Vector3 pDirection, int pSeed, bool pAiming)
        {
            if (!Avatar.Health.IsAlive) return;
            InventorySlot lSlot = SlotAt(pSlot);
            ItemDefinition lItem = GameDatabase.Item(lSlot.item);
            if (lItem == null || lSlot.magazine <= 0 || Reloading.Value) return;
            if (Vector3.Distance(pOrigin, transform.position) > 4f) pOrigin = Avatar.Eye;

            if (!Avatar.DevInfiniteAmmo) lSlot.magazine--;
            Slots[pSlot] = lSlot;

            NoiseSystem.Emit(pOrigin, lItem.noiseRadius, lItem.kind is ItemKind.SilencedPistol or ItemKind.Crossbow ? NoiseKind.Suspicious : NoiseKind.Alarming, OwnerClientId);

            if (lItem.IsLauncher)
            {
                Projectile.SpawnRocket(pOrigin + pDirection.normalized * 1.2f, pDirection, lItem, OwnerClientId, Avatar.Team.Value);
                RemoteShotRpc(lSlot.item, pOrigin, new Vector3[0]);
                return;
            }

            List<Vector3> lPellets = Ballistics.Pellets(lItem, pDirection.normalized, pSeed, pAiming);
            Vector3[] lEnds = new Vector3[lPellets.Count];
            for (int i = 0; i < lPellets.Count; i++)
                lEnds[i] = Ballistics.TraceServer(pOrigin, lPellets[i], lItem, lItem.damage * GameDatabase.Rules.playerDamageMultiplier / lItem.pellets * (lItem.pellets > 1 ? 1.6f : 1f), OwnerClientId, Avatar.Team.Value, transform);
            RemoteShotRpc(lSlot.item, pOrigin, lEnds);
        }

        [Rpc(SendTo.NotOwner)]
        private void RemoteShotRpc(int pItem, Vector3 pOrigin, Vector3[] pEnds)
        {
            ItemDefinition lItem = GameDatabase.Item(pItem);
            if (lItem == null) return;
            Vector3 lMuzzle = _thirdPersonModel != null ? (_thirdPersonModel.transform.Find("Muzzle")?.position ?? pOrigin) : pOrigin;
            Avatar.Animator?.Fire();
            Effects.Muzzle(lMuzzle, Avatar.AimDirection, lItem.kind == ItemKind.SilencedPistol ? 0.5f : 1f);
            foreach (Vector3 lEnd in pEnds)
            {
                Effects.Tracer(lMuzzle, lEnd, new Color(1f, 0.85f, 0.5f, 0.9f));
                Effects.Impact(lEnd, (pOrigin - lEnd).normalized, new Color(0.75f, 0.7f, 0.6f));
            }
            Sfx.Play(SoundFor(lItem), lMuzzle, 1f, lItem.noiseRadius * 2f);
        }

        [Rpc(SendTo.Server)]
        private void ReloadRpc(int pSlot)
        {
            InventorySlot lSlot = SlotAt(pSlot);
            ItemDefinition lItem = GameDatabase.Item(lSlot.item);
            if (lItem == null || !lItem.UsesAmmo || Reloading.Value || lSlot.reserve <= 0 || lSlot.magazine >= lItem.magazine) return;
            StartCoroutine(ReloadRoutine(pSlot, lItem));
            ReloadFeedbackRpc(lItem.reloadTime);
        }

        private System.Collections.IEnumerator ReloadRoutine(int pSlot, ItemDefinition pItem)
        {
            Reloading.Value = true;
            yield return new WaitForSeconds(pItem.reloadTime);
            Reloading.Value = false;
            InventorySlot lSlot = SlotAt(pSlot);
            if (lSlot.item != pItem.Index) yield break;
            int lNeeded = pItem.magazine - lSlot.magazine;
            int lTaken = Mathf.Min(lNeeded, lSlot.reserve);
            lSlot.magazine += lTaken;
            lSlot.reserve -= lTaken;
            Slots[pSlot] = lSlot;
        }

        [Rpc(SendTo.Everyone)]
        private void ReloadFeedbackRpc(float pTime)
        {
            if (IsOwner) View?.PlayReload(pTime);
            else Avatar.Animator?.Reload(pTime);
            Sfx.Play("reload", transform.position, 0.6f, 15f);
        }

        /// <summary>
        /// Host: a swing at its contact time. Like <see cref="FireRpc"/>, the host checks the rate (no
        /// swing closer than <see cref="MELEE_MIN_INTERVAL"/> to the last) and the origin (a reported
        /// eye more than 4 m from the body is replaced by the real eye); reach is the host's own.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void MeleeRpc(Vector3 pOrigin, Vector3 pDirection, bool pTakedown)
        {
            if (!Avatar.Health.IsAlive || Time.time < _nextMeleeServer) return;
            _nextMeleeServer = Time.time + MELEE_MIN_INTERVAL;
            if (Vector3.Distance(pOrigin, transform.position) > 4f) pOrigin = Avatar.Eye;
            if (pDirection.sqrMagnitude < 0.0001f) return;
            pDirection.Normalize();
            if (!Physics.SphereCast(pOrigin, MELEE_RADIUS, pDirection, out RaycastHit lHit, MELEE_RANGE, GameLayers.CharacterMask, QueryTriggerInteraction.Ignore)) return;
            Damageable lTarget = lHit.collider.GetComponentInParent<Damageable>();
            if (lTarget == null || lTarget == Avatar.Health) return;

            // The takedown key, from behind, on an NPC that hasn't noticed you: dead at once and silent.
            // Otherwise a solid punch (and a scuffle the guards close by hear).
            Npc lNpc = lTarget.GetComponent<Npc>();
            Vector3 lFlat = Vector3.ProjectOnPlane(pDirection, Vector3.up);
            float lFromBack = Vector3.Angle(lTarget.transform.forward, lFlat);
            bool lTakedown = pTakedown && lNpc != null && !Avatar.IsProtector && DetectionModel.CanTakedown(lFromBack, lNpc.Gauge, lNpc.IsAlerted);
            if (lTakedown)
            {
                lNpc.Takedown(pDirection, OwnerClientId, Avatar.Team.Value);
                TakedownFeedbackRpc(lHit.point, pDirection);
                return;
            }
            lTarget.ApplyDamage(new DamageInfo
            {
                amount = HeldItem != null && HeldItem.IsMelee ? HeldItem.damage : FIST_DAMAGE, type = DamageType.Melee, point = lHit.point, direction = pDirection,
                attacker = OwnerClientId, attackerTeam = Avatar.Team.Value
            });
            NoiseSystem.Emit(lHit.point, 6f, NoiseKind.Suspicious, OwnerClientId);
            NetworkObject lVictim = lTarget.GetComponent<NetworkObject>();
            PunchFeedbackRpc(lHit.point, pDirection, lVictim != null ? new NetworkObjectReference(lVictim) : default);
        }

        /// <summary>The punched body squashes (Feel) - its visual body, never the root that carries the collider.</summary>
        private static void SquashBody(Transform pRoot)
        {
            HumanBody lBody = pRoot.GetComponentInChildren<HumanBody>();
            Juice.Squash(lBody != null ? lBody.transform : pRoot, 0.35f, 0.3f);
        }

        /// <summary>
        /// Sent by the owner when the swing starts: everyone else sees the agent's third-person swing (a
        /// punch or the start of a takedown) in step with the first-person clip, not at its contact.
        /// </summary>
        [Rpc(SendTo.NotOwner)]
        private void SwingRpc() => Avatar.Animator?.Punch();

        [Rpc(SendTo.NotOwner)]
        private void ThrowAnimRpc() => Avatar.Animator?.Throw();

        [Rpc(SendTo.Everyone)]
        private void TakedownFeedbackRpc(Vector3 pPoint, Vector3 pDirection)
        {
            Sfx.Play("takedown", pPoint, 0.8f, 8f);
            if (IsOwner) Juice.Takedown();
        }

        /// <summary>
        /// Everyone: a punch landed. The owner already felt it (predicted at contact) unless the
        /// prediction missed; the others get the puff, the sound and the victim's squash.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void PunchFeedbackRpc(Vector3 pPoint, Vector3 pDirection, NetworkObjectReference pVictim)
        {
            if (IsOwner && _predictedHit) return;
            Effects.Fluff(pPoint, -pDirection, Color.white, false);
            Sfx.Play("punch", pPoint, 1f, 20f);
            if (pVictim.TryGet(out NetworkObject lVictim)) SquashBody(lVictim.transform);
            if (IsOwner) { Juice.Shake(0.15f); UI.Hud.Hitmarker(false); }
        }

        [Rpc(SendTo.Server)]
        private void ThrowRpc(int pSlot, Vector3 pOrigin, Vector3 pDirection)
        {
            if (!Avatar.Health.IsAlive) return;
            InventorySlot lSlot = SlotAt(pSlot);
            ItemDefinition lItem = GameDatabase.Item(lSlot.item);
            if (lItem == null || !lItem.IsThrowable || lSlot.reserve <= 0) return;
            if (Vector3.Distance(pOrigin, transform.position) > 4f) pOrigin = Avatar.Eye;

            NetworkObject lThrown = ThrownItem.Spawn(lItem, pOrigin, pDirection, OwnerClientId, Avatar.Team.Value);
            if (lItem.kind == ItemKind.C4 && lThrown != null) _charges.Add(lThrown);
            ConsumeServer(pSlot);
        }

        [Rpc(SendTo.Server)]
        private void DetonateRpc()
        {
            foreach (NetworkObject lCharge in _charges)
                if (lCharge != null && lCharge.IsSpawned) lCharge.GetComponent<ThrownItem>().DetonateServer();
            _charges.Clear();
        }

        /// <summary>Host: one use of the item in a slot (throwables, poison).</summary>
        public void ConsumeServer(int pSlot)
        {
            if (Avatar.DevInfiniteAmmo) return;
            InventorySlot lSlot = SlotAt(pSlot);
            lSlot.reserve--;
            Slots[pSlot] = lSlot.reserve <= 0 ? InventorySlot.Empty : lSlot;
        }

        /// <summary>Host: consumes one item of this index wherever it sits. True if found.</summary>
        public bool ConsumeItemServer(int pItemIndex)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].item == pItemIndex) { ConsumeServer(i); return true; }
            return false;
        }

        /// <summary>
        /// Host: adds an item. Weapons take a free weapon slot or replace the held one (which drops);
        /// gadgets stack. -1 ammo means "the item's default".
        /// </summary>
        public bool GiveServer(ItemDefinition pItem, int pMagazine, int pReserve)
        {
            if (pItem == null) return false;
            int lMag = pMagazine < 0 ? pItem.magazine : pMagazine;
            int lReserve = pReserve < 0 ? (pItem.slot == ItemSlot.Gadget ? pItem.stack : pItem.reserveAmmo) : pReserve;

            if (pItem.IsKey)
            {
                for (int i = SLOT_COUNT; i < Slots.Count; i++)
                    if (Slots[i].item == pItem.Index) return true;
                for (int i = SLOT_COUNT; i < Slots.Count; i++)
                    if (Slots[i].IsEmpty) { Slots[i] = new InventorySlot { item = pItem.Index, magazine = 0, reserve = 1 }; return true; }
                return false;
            }

            if (pItem.slot == ItemSlot.Movement)
            {
                if (Slots[MOVE_SLOT].item == pItem.Index) return false;
                DropSlotServer(MOVE_SLOT);
                Slots[MOVE_SLOT] = new InventorySlot { item = pItem.Index, magazine = 0, reserve = 1 };
                return true;
            }

            if (pItem.slot == ItemSlot.Gadget)
            {
                if (Slots[DISTRACTION_SLOT].item == pItem.Index) { InventorySlot s = Slots[DISTRACTION_SLOT]; s.reserve += lReserve; Slots[DISTRACTION_SLOT] = s; return true; }
                DropSlotServer(DISTRACTION_SLOT);
                Slots[DISTRACTION_SLOT] = new InventorySlot { item = pItem.Index, magazine = 0, reserve = lReserve };
                return true;
            }

            for (int i = 0; i < WEAPON_SLOTS; i++)
                if (Slots[i].item == pItem.Index) { InventorySlot s = Slots[i]; s.reserve += lMag + lReserve; Slots[i] = s; return true; }
            for (int i = 0; i < WEAPON_SLOTS; i++)
                if (Slots[i].IsEmpty) { Slots[i] = new InventorySlot { item = pItem.Index, magazine = lMag, reserve = lReserve }; return true; }

            int lReplace = Selected.Value >= 0 && Selected.Value < WEAPON_SLOTS ? Selected.Value : 0;
            DropSlotServer(lReplace);
            Slots[lReplace] = new InventorySlot { item = pItem.Index, magazine = lMag, reserve = lReserve };
            return true;
        }

        private void DropSlotServer(int pSlot)
        {
            InventorySlot lSlot = SlotAt(pSlot);
            if (lSlot.IsEmpty) return;
            ItemPickup.SpawnServer(GameDatabase.Item(lSlot.item), transform.position + Vector3.up * 1.2f + transform.forward * 0.6f, lSlot.magazine, lSlot.reserve, transform.forward * 3f + Vector3.up * 2f);
            Slots[pSlot] = InventorySlot.Empty;
        }

        [Rpc(SendTo.Server)]
        public void PickupRpc(NetworkObjectReference pPickup)
        {
            if (!pPickup.TryGet(out NetworkObject lObject) || !Avatar.Health.IsAlive) return;
            ItemPickup lPickup = lObject.GetComponent<ItemPickup>();
            if (lPickup == null || Vector3.Distance(lPickup.transform.position, transform.position) > 4f) return;
            if (GiveServer(lPickup.Item, lPickup.Magazine.Value, lPickup.Reserve.Value))
            {
                PickupFeedbackRpc(lPickup.ItemIndex.Value);
                lObject.Despawn();
            }
        }

        [Rpc(SendTo.Owner)]
        private void PickupFeedbackRpc(int pItem)
        {
            Sfx.Play2D("pickup", 0.8f);
            ItemDefinition lItem = GameDatabase.Item(pItem);
            for (int i = 0; i < WEAPON_SLOTS; i++)
                if (SlotAt(i).item == pItem && lItem != null && lItem.slot == ItemSlot.Weapon) { Select(i); break; }
        }

        private void HandleDeathServer(DamageInfo pInfo)
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                InventorySlot lSlot = SlotAt(i);
                if (lSlot.IsEmpty) continue;
                Vector3 lFling = Random.insideUnitSphere * 2f + Vector3.up * 3f;
                ItemPickup.SpawnServer(GameDatabase.Item(lSlot.item), transform.position + Vector3.up * 1.2f, lSlot.magazine, lSlot.reserve, lFling);
                Slots[i] = InventorySlot.Empty;
            }
        }

        // ---------------------------------------------------------------- flamethrower (host ticks, everyone sees)

        private void UpdateThirdPersonFlame()
        {
            bool lOn = Flaming.Value && Avatar.Health.IsAlive;
            if (lOn && _flameFx == null)
            {
                Transform lAnchor = IsOwner && View != null ? View.Muzzle : (_thirdPersonModel != null ? _thirdPersonModel.transform.Find("Muzzle") : transform);
                _flameFx = Flamethrower.CreateJet(lAnchor != null ? lAnchor : transform);
                Sfx.Play("flame_start", transform.position, 0.8f);
            }
            else if (!lOn && _flameFx != null)
            {
                Destroy(_flameFx);
                _flameFx = null;
            }

            if (!IsServer || !lOn) return;
            _flameTick -= Time.deltaTime;
            if (_flameTick > 0f) return;
            _flameTick = 0.12f;
            InventorySlot lSlot = SlotAt(Selected.Value);
            ItemDefinition lItem = GameDatabase.Item(lSlot.item);
            if (lItem == null || lItem.kind != ItemKind.Flamethrower || lSlot.magazine <= 0) return;
            if (!Avatar.DevInfiniteAmmo) lSlot.magazine--;
            Slots[Selected.Value] = lSlot;
            Flamethrower.TickServer(Avatar.Eye, Avatar.AimDirection, lItem, OwnerClientId, Avatar.Team.Value, Avatar.Health);
        }

        /// <summary>The held item in the third-person body's right hand (the owner only sees its shadow).</summary>
        public void RefreshThirdPerson(bool pForce = false)
        {
            ItemDefinition lItem = HeldItem;
            int lIndex = lItem != null ? lItem.Index : -1;
            if (lIndex == _thirdPersonItem && !pForce && _thirdPersonModel != null) return;
            _thirdPersonItem = lIndex;
            if (_thirdPersonModel != null) Destroy(_thirdPersonModel);
            _thirdPersonModel = null;
            if (lItem == null || Avatar.Body == null || Avatar.Body.WeaponSocket == null) return;
            Avatar.Body.SetHeld(WeaponModels.HeldOf(lItem));
            _thirdPersonModel = WeaponModels.Build(lItem, Avatar.Body.WeaponSocket, GameLayers.PLAYER);
            if (IsOwner) foreach (Renderer lRenderer in _thirdPersonModel.GetComponentsInChildren<Renderer>()) lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        private void LateUpdate()
        {
            if (_thirdPersonItem == -2 || (_thirdPersonModel == null && HeldItem != null && Avatar.Body != null && Avatar.Body.Built)) RefreshThirdPerson();
        }
    }
}
