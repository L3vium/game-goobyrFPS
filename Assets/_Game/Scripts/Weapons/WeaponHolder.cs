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
    /// Six slots: two weapons (1-2) and four gadgets (3-6); an empty weapon slot is the fists.
    /// The host owns the slot list and the ammo; the owner predicts the shot locally (muzzle,
    /// tracer, recoil, sound) and sends origin + direction + spread seed, the host re-traces the
    /// same pellets, deals the damage and replays the shot on the other machines.
    /// Damage is high both ways - a room can be cleared, and a room can clear you.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class WeaponHolder : NetworkBehaviour
    {
        public const int SLOT_COUNT = 6;
        public const int WEAPON_SLOTS = 2;

        public readonly NetworkList<InventorySlot> Slots = new NetworkList<InventorySlot>();
        public readonly NetworkVariable<int> Selected = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Flaming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<bool> Reloading = new NetworkVariable<bool>(false);

        public PlayerAvatar Avatar { get; private set; }
        public Viewmodel View { get; private set; }

        public ItemDefinition HeldItem => ItemIn(Selected.Value);
        public bool HoldsWeapon => HeldItem != null && HeldItem.slot == ItemSlot.Weapon;
        public float AdsFov => _aiming && HeldItem != null && HeldItem.IsGun ? HeldItem.adsFov : 0f;
        public float LookSensitivityFactor => _aiming && HeldItem != null ? Mathf.Clamp(HeldItem.adsFov / 78f, 0.25f, 1f) : 1f;
        public bool Scoped => _aiming && HeldItem != null && HeldItem.kind == ItemKind.Sniper;

        private float _nextFire;
        private float _nextMelee;
        private float _nextScroll;
        private ThrowArc _arc;
        private bool _aiming;
        private int _lastGadget = 2;
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
                for (int i = 0; i < SLOT_COUNT; i++) Slots.Add(InventorySlot.Empty);
                foreach (ItemDefinition lItem in GameDatabase.Rules.startingLoadout) if (lItem != null) GiveServer(lItem, -1, -1);
            }
            if (IsOwner)
            {
                View = Viewmodel.Create(this);
                Selected.Value = 0;
            }
            Avatar.Health.OnDeathServer += HandleDeathServer;
            Selected.OnValueChanged += (_, __) => RefreshThirdPerson();
        }

        public override void OnNetworkDespawn()
        {
            if (View != null) Destroy(View.gameObject);
            if (_arc != null) Destroy(_arc.gameObject);
            if (_flameFx != null) Destroy(_flameFx);
        }

        public InventorySlot SlotAt(int pSlot) => Slots != null && pSlot >= 0 && pSlot < Slots.Count ? Slots[pSlot] : InventorySlot.Empty;
        public ItemDefinition ItemIn(int pSlot) => GameDatabase.Item(SlotAt(pSlot).item);

        // ---------------------------------------------------------------- owner input

        private void Update()
        {
            if (!IsSpawned) return;
            UpdateThirdPersonFlame();
            if (!IsOwner || !Avatar.Health.IsAlive) { if (_arc != null) _arc.Hide(); return; }
            if (Avatar.Motor.Mode == MotorMode.Hidden || Avatar.Motor.Mode == MotorMode.Zipline) { _aiming = false; if (_arc != null) _arc.Hide(); return; }

            int lSlot = GameInput.SlotPressed;
            if (lSlot >= 0) Select(lSlot);
            float lScroll = GameInput.Scroll;
            if (Mathf.Abs(lScroll) > 0.1f && Time.time >= _nextScroll)
            {
                _nextScroll = Time.time + 0.12f;
                Cycle(lScroll < 0 ? 1 : -1);
            }

            // Right click raises the weapon (aims); only then does left click fire. Lowered, left click punches.
            ItemDefinition lHeld = HeldItem;
            _aiming = GameInput.Aim && lHeld != null && (lHeld.IsGun || lHeld.kind == ItemKind.RocketLauncher || lHeld.kind == ItemKind.Flamethrower);
            Avatar.Motor.Aiming = _aiming;
            if (Avatar.Aiming.Value != _aiming) Avatar.Aiming.Value = _aiming;

            if (GameInput.Reload && lHeld != null && lHeld.UsesAmmo) ReloadRpc(Selected.Value);
            if (GameInput.Melee) TryMelee();
            if (GameInput.Throw) ThrowGadget(_lastGadget);
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

            if (lHeld != null && lHeld.slot == ItemSlot.Gadget)
            {
                // Throwables: right click shows the arc, left click throws.
                if (GameInput.FirePressed) ThrowGadget(Selected.Value);
            }
            else if (lHeld == null || !_aiming)
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
        /// The mouse wheel: next / previous slot that holds something, plus the fists (an empty
        /// weapon slot). Empty gadget slots are skipped, and it wraps round.
        /// </summary>
        private void Cycle(int pStep)
        {
            int lFists = -1;
            for (int i = 0; i < WEAPON_SLOTS; i++) if (SlotAt(i).IsEmpty) { lFists = i; break; }
            for (int n = 1; n <= SLOT_COUNT; n++)
            {
                int lSlot = ((Selected.Value + pStep * n) % SLOT_COUNT + SLOT_COUNT) % SLOT_COUNT;
                if (!SlotAt(lSlot).IsEmpty || lSlot == lFists) { Select(lSlot); return; }
            }
        }

        public void Select(int pSlot)
        {
            if (pSlot < 0 || pSlot >= SLOT_COUNT || pSlot == Selected.Value) return;
            if (pSlot >= WEAPON_SLOTS && SlotAt(pSlot).IsEmpty) return;
            Selected.Value = pSlot;
            if (pSlot >= WEAPON_SLOTS) _lastGadget = pSlot;
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

            if (pItem.kind == ItemKind.RocketLauncher)
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
                    if (Physics.Raycast(lOrigin, lDir, out RaycastHit lHit, pItem.range, GameLayers.ShotMask, QueryTriggerInteraction.Ignore) && !lHit.collider.transform.IsChildOf(transform))
                    {
                        lEnd = lHit.point;
                        Damageable lTarget = lHit.collider.GetComponentInParent<Damageable>();
                        if (lTarget != null && lTarget.IsAlive)
                        {
                            UI.Hud.Hitmarker(lTarget.IsHeadshot(lHit.point));
                            Effects.Fluff(lHit.point, -lDir, new Color(1f, 0.9f, 0.8f), false);
                        }
                        else Effects.Impact(lHit.point, lHit.normal, new Color(0.75f, 0.7f, 0.6f));
                    }
                    Effects.Tracer(lMuzzle, lEnd, new Color(1f, 0.85f, 0.5f, 0.9f));
                }
                Effects.Muzzle(lMuzzle, lDirection, pItem.kind == ItemKind.Shotgun ? 1.6f : pItem.kind == ItemKind.SilencedPistol ? 0.5f : 1f);
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
            ItemKind.Pistol => "shot_pistol",
            ItemKind.Smg => "shot_smg",
            ItemKind.Shotgun => "shot_shotgun",
            ItemKind.Sniper => "shot_sniper",
            ItemKind.RocketLauncher => "rocket_launch",
            _ => "shot_pistol"
        };

        private void TryMelee()
        {
            if (Time.time < _nextMelee) return;
            _nextMelee = Time.time + 0.55f;
            View?.PlayPunch();
            Sfx.Play("swing", transform.position, 0.6f);
            MeleeRpc(Avatar.AimOrigin, Avatar.AimDirection);
        }

        private void ThrowGadget(int pSlot)
        {
            ItemDefinition lItem = ItemIn(pSlot);
            if (lItem == null || lItem.slot != ItemSlot.Gadget || !lItem.IsThrowable) return;
            View?.PlayThrow();
            Sfx.Play("throw", transform.position, 0.6f);
            ThrowRpc(pSlot, Avatar.AimOrigin + Avatar.AimDirection * 0.5f, Avatar.AimDirection);
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

            lSlot.magazine--;
            Slots[pSlot] = lSlot;

            NoiseSystem.Emit(pOrigin, lItem.noiseRadius, lItem.kind == ItemKind.SilencedPistol ? NoiseKind.Suspicious : NoiseKind.Alarming, OwnerClientId);

            if (lItem.kind == ItemKind.RocketLauncher)
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
            Sfx.Play("reload", transform.position, 0.6f, 15f);
        }

        [Rpc(SendTo.Server)]
        private void MeleeRpc(Vector3 pOrigin, Vector3 pDirection)
        {
            if (!Avatar.Health.IsAlive) return;
            if (!Physics.SphereCast(pOrigin, 0.35f, pDirection, out RaycastHit lHit, 1.7f, GameLayers.CharacterMask, QueryTriggerInteraction.Ignore)) return;
            Damageable lTarget = lHit.collider.GetComponentInParent<Damageable>();
            if (lTarget == null || lTarget == Avatar.Health) return;

            // From behind, on an NPC that hasn't noticed you: a silent takedown, dead at once.
            // Otherwise a solid punch (and a scuffle the guards close by hear).
            Npc lNpc = lTarget.GetComponent<Npc>();
            Vector3 lFlat = Vector3.ProjectOnPlane(pDirection, Vector3.up);
            float lFromBack = Vector3.Angle(lTarget.transform.forward, lFlat);
            bool lTakedown = lNpc != null && DetectionModel.CanTakedown(lFromBack, lNpc.Gauge, lNpc.IsAlerted);
            if (lTakedown)
            {
                lNpc.Takedown(pDirection, OwnerClientId, Avatar.Team.Value);
                TakedownFeedbackRpc(lHit.point, pDirection);
                return;
            }
            lTarget.ApplyDamage(new DamageInfo
            {
                amount = 34f, type = DamageType.Melee, point = lHit.point, direction = pDirection,
                attacker = OwnerClientId, attackerTeam = Avatar.Team.Value
            });
            NoiseSystem.Emit(lHit.point, 6f, NoiseKind.Suspicious, OwnerClientId);
            PunchFeedbackRpc(lHit.point, pDirection);
        }

        [Rpc(SendTo.Everyone)]
        private void TakedownFeedbackRpc(Vector3 pPoint, Vector3 pDirection)
        {
            Sfx.Play("takedown", pPoint, 0.8f, 8f);
            if (IsOwner) { Juice.Takedown(); UI.Hud.Hitmarker(true); }
        }

        [Rpc(SendTo.Everyone)]
        private void PunchFeedbackRpc(Vector3 pPoint, Vector3 pDirection)
        {
            Effects.Fluff(pPoint, -pDirection, Color.white, false);
            Sfx.Play("punch", pPoint, 1f, 20f);
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

            if (pItem.slot == ItemSlot.Gadget)
            {
                for (int i = WEAPON_SLOTS; i < SLOT_COUNT; i++)
                    if (Slots[i].item == pItem.Index) { InventorySlot s = Slots[i]; s.reserve += lReserve; Slots[i] = s; return true; }
                for (int i = WEAPON_SLOTS; i < SLOT_COUNT; i++)
                    if (Slots[i].IsEmpty) { Slots[i] = new InventorySlot { item = pItem.Index, magazine = 0, reserve = lReserve }; return true; }
                return false;
            }

            for (int i = 0; i < WEAPON_SLOTS; i++)
                if (Slots[i].item == pItem.Index) { InventorySlot s = Slots[i]; s.reserve += lMag + lReserve; Slots[i] = s; return true; }
            for (int i = 0; i < WEAPON_SLOTS; i++)
                if (Slots[i].IsEmpty) { Slots[i] = new InventorySlot { item = pItem.Index, magazine = lMag, reserve = lReserve }; return true; }

            int lReplace = Selected.Value < WEAPON_SLOTS ? Selected.Value : 0;
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
            if (lItem != null) UI.Hud.Toast("+ " + lItem.displayName.ToUpperInvariant());
            for (int i = 0; i < SLOT_COUNT; i++)
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
            lSlot.magazine--;
            Slots[Selected.Value] = lSlot;
            Flamethrower.TickServer(Avatar.Eye, Avatar.AimDirection, lItem, OwnerClientId, Avatar.Team.Value, Avatar.Health);
        }

        private void RefreshThirdPerson()
        {
            ItemDefinition lItem = HeldItem;
            int lIndex = lItem != null ? lItem.Index : -1;
            if (lIndex == _thirdPersonItem) return;
            _thirdPersonItem = lIndex;
            if (_thirdPersonModel != null) Destroy(_thirdPersonModel);
            _thirdPersonModel = null;
            if (lItem == null || Avatar.Body == null || Avatar.Body.WeaponSocket == null) return;
            _thirdPersonModel = WeaponModels.Build(lItem, Avatar.Body.WeaponSocket, GameLayers.PLAYER);
            _thirdPersonModel.transform.localPosition = Vector3.zero;
            _thirdPersonModel.transform.localScale = Vector3.one * 1.3f;
            if (IsOwner) foreach (Renderer lRenderer in _thirdPersonModel.GetComponentsInChildren<Renderer>()) lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        private void LateUpdate()
        {
            if (_thirdPersonItem == -2 || (_thirdPersonModel == null && HeldItem != null)) RefreshThirdPerson();
        }
    }
}
