using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// The inside of a building (hotel tower, mansion, lodge): a named box the systems act on -
    /// the power cut darkens it, the ventilation poison fills it.
    /// </summary>
    public class BuildingVolume : MonoBehaviour
    {
        [SerializeField] private string _buildingName = "Building";
        [SerializeField] private Vector3 _size = new Vector3(30, 12, 30);

        private static readonly List<BuildingVolume> _all = new List<BuildingVolume>();
        public static IReadOnlyList<BuildingVolume> All => _all;

        public string BuildingName => _buildingName;
        public bool PowerOn { get; set; } = true;
        public bool Gassed { get; set; }

        public void Setup(string pName, Vector3 pSize)
        {
            _buildingName = pName;
            _size = pSize;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public bool Contains(Vector3 pPoint)
        {
            Vector3 lLocal = transform.InverseTransformPoint(pPoint);
            return Mathf.Abs(lLocal.x) <= _size.x * 0.5f && lLocal.y >= -4f && lLocal.y <= _size.y && Mathf.Abs(lLocal.z) <= _size.z * 0.5f;
        }

        public static BuildingVolume At(Vector3 pPoint)
        {
            foreach (BuildingVolume lBuilding in _all) if (lBuilding.Contains(pPoint)) return lBuilding;
            return null;
        }

        /// <summary>How much an NPC sees at this point: dark buildings cut vision.</summary>
        public static float LightFactor(Vector3 pPoint)
        {
            BuildingVolume lBuilding = At(pPoint);
            return lBuilding != null && !lBuilding.PowerOn ? 0.4f : 1f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.15f);
            Gizmos.DrawCube(new Vector3(0, _size.y * 0.5f - 2f, 0), _size + new Vector3(0, 4f, 0));
        }
    }

    /// <summary>
    /// The breaker in a building's utility room. Off: every light of the building dies, NPCs see
    /// far less inside, and the nearest one walks over to switch it back on (a distraction with a
    /// known destination).
    /// </summary>
    public class Breaker : Interactable
    {
        [SerializeField] private BuildingVolume _building;
        [SerializeField] private Transform _lever;

        private readonly List<Light> _lights = new List<Light>();
        private readonly List<Renderer> _bulbs = new List<Renderer>();

        public BuildingVolume Building => _building;
        public bool PowerOn => State == 0;

        public void Setup(BuildingVolume pBuilding, Transform pLever)
        {
            _building = pBuilding;
            _lever = pLever;
        }

        private void Start()
        {
            if (_building == null) return;
            foreach (Light lLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (lLight.type != LightType.Directional && _building.Contains(lLight.transform.position)) _lights.Add(lLight);
            foreach (PoweredLight lBulb in FindObjectsByType<PoweredLight>(FindObjectsSortMode.None))
                if (_building.Contains(lBulb.transform.position)) _bulbs.AddRange(lBulb.GetComponentsInChildren<Renderer>());
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0 ? "CUT THE POWER" : "RESTORE POWER";

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => ToggleServer(pWho != null ? pWho.OwnerClientId : ulong.MaxValue);

        public void ToggleServer(ulong pBy)
        {
            int lNext = State == 0 ? 1 : 0;
            SetStateServer(lNext);
            if (lNext == 1) NoiseSystem.Emit(transform.position, 0f, NoiseKind.Distraction, pBy);
            BreakerAlert.Raise(this);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            bool lOn = pNew == 0;
            if (_building != null) _building.PowerOn = lOn;
            foreach (Light lLight in _lights) if (lLight != null) lLight.enabled = lOn;
            foreach (Renderer lBulb in _bulbs) if (lBulb != null) GameArt.SetEmission(lBulb, lOn ? new Color(1.5f, 1.3f, 0.9f) : Color.black);
            if (_lever != null) _lever.localRotation = Quaternion.Euler(lOn ? -35f : 35f, 0, 0);
            Effects.Sparks(transform.position + transform.forward * 0.2f, lOn ? 4 : 18);
            Audio.Sfx.Play(lOn ? "power_on" : "power_off", transform.position, 1f, 40f);
        }
    }

    /// <summary>Host: tells the AI a breaker flipped (the nearest NPC of the building goes to it).</summary>
    public static class BreakerAlert
    {
        public static event System.Action<Breaker> OnBreaker;
        public static void Raise(Breaker pBreaker) => OnBreaker?.Invoke(pBreaker);
    }

    /// <summary>A lamp bulb that goes dark with its building's power.</summary>
    public class PoweredLight : MonoBehaviour { }

    /// <summary>
    /// A chandelier hanging by a chain. Shoot the chain (or pull the release lever) and it falls:
    /// whoever stands under it is crushed.
    /// </summary>
    public class Chandelier : Interactable, IShootable
    {
        [SerializeField] private Transform _crystal;
        [SerializeField] private float _dropHeight = 6f;
        [SerializeField] private float _killRadius = 2.2f;

        private float _fall;
        private Vector3 _restLocal;
        private bool _falling;

        public void Setup(Transform pCrystal, float pDropHeight)
        {
            _crystal = pCrystal;
            _dropHeight = pDropHeight;
            _prompt = "RELEASE THE CHANDELIER";
            _range = 2.5f;
        }

        private void Awake()
        {
            if (_crystal != null) _restLocal = _crystal.localPosition;
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => DropServer(pWho != null ? pWho.OwnerClientId : DamageInfo.NO_PLAYER, pWho != null ? pWho.Team.Value : Match.MatchRulesLogic.NO_TEAM);

        public void OnShotServer(DamageInfo pInfo) => DropServer(pInfo.attacker, pInfo.attackerTeam);

        private void DropServer(ulong pBy, int pTeam)
        {
            if (State != 0) return;
            SetStateServer(1);
            Vector3 lFloor = _crystal.position + Vector3.down * _dropHeight;
            foreach (Collider lCollider in Physics.OverlapSphere(lFloor + Vector3.up, _killRadius, GameLayers.CharacterMask, QueryTriggerInteraction.Ignore))
            {
                Damageable lVictim = lCollider.GetComponentInParent<Damageable>();
                if (lVictim != null) lVictim.Kill(new DamageInfo { type = DamageType.Crush, point = lFloor, direction = Vector3.down, attacker = pBy, attackerTeam = pTeam });
            }
            NoiseSystem.Emit(lFloor, 40f, NoiseKind.Alarming, pBy);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew == 1) { _falling = true; _fall = 0f; Audio.Sfx.Play("chain_snap", _crystal.position, 1f, 40f); }
            else if (_crystal != null) _crystal.localPosition = _restLocal;
        }

        private void Update()
        {
            if (!_falling || _crystal == null) return;
            _fall += Time.deltaTime;
            float lDrop = Mathf.Min(_dropHeight, 0.5f * 22f * _fall * _fall);
            _crystal.localPosition = _restLocal + Vector3.down * lDrop / Mathf.Max(0.01f, transform.lossyScale.y);
            if (lDrop >= _dropHeight)
            {
                _falling = false;
                Effects.Sparks(_crystal.position, 30);
                Effects.Dust(_crystal.position, 2f);
                Juice.ShakeAt(_crystal.position, 0.6f, 25f);
                Juice.Squash(_crystal, 0.4f);
                Audio.Sfx.Play("crash", _crystal.position, 1f, 60f);
            }
        }
    }

    /// <summary>
    /// The air-handling unit in a building's basement. Pour poison in, and after a delay green gas
    /// pours from every vent of the building: everyone inside - guards, target, players - dies.
    /// Once per match. Loud, final, and visible from outside.
    /// </summary>
    public class VentPoison : Interactable
    {
        [SerializeField] private BuildingVolume _building;
        [SerializeField] private Transform[] _vents;

        private float _timer;
        private ulong _by;
        private int _team;
        private readonly List<GameObject> _gas = new List<GameObject>();

        public void Setup(BuildingVolume pBuilding, Transform[] pVents)
        {
            _building = pBuilding;
            _vents = pVents;
        }

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => pHeld != null && pHeld.kind == ItemKind.Poison ? "POISON THE VENTILATION" : "VENTILATION (NEEDS LETHAL POISON)";

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            ItemDefinition lHeld = GameDatabase.Item(pHeldItem);
            if (lHeld == null || lHeld.kind != ItemKind.Poison || !pWho.Weapons.ConsumeItemServer(pHeldItem)) return;
            _by = pWho.OwnerClientId;
            _team = pWho.Team.Value;
            _timer = GameDatabase.Rules.ventGasDelay;
            SetStateServer(1);
            if (MapEffects.Instance != null) MapEffects.Instance.ToastRpc("SOMETHING IS WRONG WITH THE AIR IN " + _building.BuildingName.ToUpperInvariant());
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew == 1) Audio.Sfx.Play("vent_hiss", transform.position, 1f, 30f);
            if (pNew == 2 && _vents != null)
            {
                foreach (Transform lVent in _vents) if (lVent != null) _gas.Add(Effects.Gas(lVent, Vector3.zero));
                if (_building != null) _building.Gassed = true;
                Audio.Sfx.Play("gas_alarm", transform.position + Vector3.up * 6f, 1f, 120f);
            }
            if (pNew == 3)
            {
                foreach (GameObject lGas in _gas) if (lGas != null) Destroy(lGas, 4f);
                _gas.Clear();
                if (_building != null) _building.Gassed = false;
            }
        }

        private void Update()
        {
            if (State == 0 || State == 3 || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            _timer -= Time.deltaTime;
            if (State == 1 && _timer <= 0f)
            {
                SetStateServer(2);
                _timer = GameDatabase.Rules.ventGasDuration;
                NoiseSystem.Emit(_building.transform.position, 60f, NoiseKind.Alarming, _by);
            }
            else if (State == 2)
            {
                // Gas kills over the duration: everyone inside, a few each tick.
                foreach (Damageable lVictim in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                {
                    if (!lVictim.IsAlive || _building == null || !_building.Contains(lVictim.transform.position + Vector3.up)) continue;
                    lVictim.ApplyDamage(new DamageInfo { amount = 30f * Time.deltaTime, type = DamageType.Gas, point = lVictim.transform.position, direction = Vector3.up, attacker = _by, attackerTeam = Match.MatchRulesLogic.NO_TEAM });
                }
                if (_timer <= 0f) SetStateServer(3);
            }
        }
    }

    /// <summary>
    /// A glass on a table along the target's routine. Poison it: lethal poison kills whoever drinks,
    /// emetic poison sends them running to the nearest toilet - alone.
    /// </summary>
    public class Drink : Interactable
    {
        public const int CLEAN = 0, LETHAL = 1, EMETIC = 2, EMPTY = 3;

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld)
        {
            if (pHeld != null && pHeld.kind == ItemKind.Poison) return "POISON THE DRINK (LETHAL)";
            if (pHeld != null && pHeld.kind == ItemKind.EmeticPoison) return "POISON THE DRINK (EMETIC)";
            return "A DRINK (HOLD A POISON)";
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == CLEAN;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            ItemDefinition lHeld = GameDatabase.Item(pHeldItem);
            if (lHeld == null || !lHeld.IsPoison || !pWho.Weapons.ConsumeItemServer(pHeldItem)) return;
            Poisoner = pWho.OwnerClientId;
            PoisonerTeam = pWho.Team.Value;
            SetStateServer(lHeld.kind == ItemKind.Poison ? LETHAL : EMETIC);
        }

        public ulong Poisoner { get; private set; } = DamageInfo.NO_PLAYER;
        public int PoisonerTeam { get; private set; } = Match.MatchRulesLogic.NO_TEAM;

        /// <summary>Host: an NPC drinks it. Returns the poison it held.</summary>
        public int DrinkServer()
        {
            int lWas = State;
            SetStateServer(EMPTY);
            return lWas;
        }

        /// <summary>Host: a fresh glass after a while.</summary>
        public void RefillServer() => SetStateServer(CLEAN);

        protected override void OnStateChanged(int pOld, int pNew)
        {
            Renderer lLiquid = transform.Find("Liquid")?.GetComponent<Renderer>();
            if (lLiquid != null)
            {
                lLiquid.enabled = pNew != EMPTY;
                // Poison is only visible up close: a faint green fizz.
                GameArt.SetEmission(lLiquid, pNew == LETHAL || pNew == EMETIC ? new Color(0.05f, 0.25f, 0.02f) : Color.black);
            }
            if (pNew == LETHAL || pNew == EMETIC) Audio.Sfx.Play("pour", transform.position, 0.6f, 6f);
        }
    }

    /// <summary>A toilet: where sick NPCs go, and where they are alone and easy to finish.</summary>
    public class Toilet : MonoBehaviour
    {
        private static readonly List<Toilet> _all = new List<Toilet>();
        public Vector3 StandPoint => transform.position + transform.forward * 0.6f;

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public static Toilet Nearest(Vector3 pPoint)
        {
            Toilet lBest = null;
            float lBestDistance = float.MaxValue;
            foreach (Toilet lToilet in _all)
            {
                float lDistance = (lToilet.transform.position - pPoint).sqrMagnitude;
                if (lDistance < lBestDistance) { lBestDistance = lDistance; lBest = lToilet; }
            }
            return lBest;
        }
    }

    /// <summary>
    /// A radio, a TV, a car alarm, a phone, a generator: switch it on and it makes noise until
    /// someone comes to switch it off - that someone is the nearest NPC.
    /// </summary>
    public class Distraction : Interactable
    {
        [SerializeField] private string _sound = "radio";
        [SerializeField] private float _radius = 18f;
        [SerializeField] private float _duration = 25f;

        private float _timer;
        private float _pulse;

        public void Setup(string pPrompt, string pSound, float pRadius)
        {
            _prompt = pPrompt;
            _sound = pSound;
            _radius = pRadius;
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => ActivateServer(pWho != null ? pWho.OwnerClientId : ulong.MaxValue);

        public void ActivateServer(ulong pBy)
        {
            if (State != 0) return;
            _timer = _duration;
            _pulse = 0f;
            SetStateServer(1);
            NoiseSystem.Emit(transform.position, _radius, NoiseKind.Distraction, pBy);
        }

        /// <summary>Host: an NPC reached it and turned it off.</summary>
        public void TurnOffServer()
        {
            if (State == 1) SetStateServer(0);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew == 1) Juice.Squash(transform, 0.25f);
        }

        private void Update()
        {
            if (State != 1) return;
            _pulse -= Time.deltaTime;
            if (_pulse <= 0f)
            {
                _pulse = 1.2f;
                Audio.Sfx.Play(_sound, transform.position, 0.8f, _radius * 1.5f);
                Juice.Squash(transform, 0.08f, 0.2f);
            }
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) SetStateServer(0);
        }
    }

    /// <summary>A red barrel, a gas bottle: shoot it and it goes up.</summary>
    public class ExplosiveProp : Interactable, IShootable
    {
        [SerializeField] private float _radius = 6f;
        [SerializeField] private float _damage = 150f;
        private float _health = 30f;

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => false;

        public void OnShotServer(DamageInfo pInfo)
        {
            if (State != 0) return;
            _health -= pInfo.type == DamageType.Fire ? 12f : pInfo.amount;
            if (_health > 0f && pInfo.type != DamageType.Explosion) return;
            SetStateServer(1);
            Ballistics.ExplodeServer(transform.position + Vector3.up * 0.5f, _radius, _damage, pInfo.attacker, pInfo.attackerTeam);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            foreach (Renderer lRenderer in GetComponentsInChildren<Renderer>()) lRenderer.enabled = pNew == 0;
            foreach (Collider lCollider in GetComponentsInChildren<Collider>()) lCollider.enabled = pNew == 0;
        }
    }

    /// <summary>
    /// The flare launcher / siren of a lair: an NPC (or a player) fires it and the whole map is
    /// alerted toward this position - reinforcements of every zone converge.
    /// </summary>
    public class AlarmTrigger : Interactable
    {
        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld) => "FIRE THE ALARM FLARE";
        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0;

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem) => FireServer(transform.position);

        public void FireServer(Vector3 pAlertPoint)
        {
            if (State != 0) return;
            SetStateServer(1);
            AlertDirector.RaiseGlobal(pAlertPoint);
            Invoke(nameof(Rearm), 60f);
        }

        private void Rearm() => SetStateServer(0);

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (pNew != 1) return;
            Effects.Muzzle(transform.position + Vector3.up, Vector3.up, 3f);
            FlareVisual.Launch(transform.position + Vector3.up);
            Audio.Sfx.Play("siren", transform.position, 1f, 400f, 0f);
            Juice.Flash(new Color(1f, 0.3f, 0.2f), 0.15f);
            UI.Hud.Toast("ALARM! THE WHOLE AREA IS ON ALERT");
        }
    }

    /// <summary>A red flare climbing into the sky, visible map-wide.</summary>
    public class FlareVisual : MonoBehaviour
    {
        private float _t;
        public static void Launch(Vector3 pFrom)
        {
            GameObject lFlare = GameArt.Part(null, "Flare", LowPolyMesh.Cached("w.sphere", () => LowPolyMesh.Icosphere(1)), pFrom, Vector3.one * 0.6f, new Color(3f, 0.4f, 0.3f), GameArt.Glow);
            Light lLight = lFlare.AddComponent<Light>();
            lLight.color = new Color(1f, 0.3f, 0.2f);
            lLight.range = 120f;
            lLight.intensity = 8f;
            lFlare.AddComponent<FlareVisual>();
            Object.Destroy(lFlare, 14f);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            transform.position += Vector3.up * Mathf.Max(0f, 30f - _t * 6f) * Time.deltaTime + new Vector3(Mathf.Sin(_t * 2f), 0, 0) * Time.deltaTime;
            if (Random.value < 0.4f) Effects.Dust(transform.position, 0.3f);
        }
    }
}
