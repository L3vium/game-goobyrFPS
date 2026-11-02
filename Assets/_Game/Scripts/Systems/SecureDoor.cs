using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Player;
using UnityEngine;
using UnityEngine.AI;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>
    /// A way in that needs a tool: a boarded or rusted door (crowbar, loud), a keycard door, a
    /// padlocked hatch (a staff pass carries the key). Opened once, it stays open for everyone - a
    /// shortcut. The leaf swings on its hinge child; while shut a NavMesh obstacle keeps NPCs out.
    /// </summary>
    public class SecureDoor : Interactable
    {
        /// <summary>Crowbar (loud), keycard, padlock (a staff pass carries the key), keypad (a code note found or carried somewhere).</summary>
        public enum Lock : byte { Crowbar, Keycard, Padlock, Keypad }

        [SerializeField] private Lock _lock;
        [SerializeField] private Transform _leaf;

        public Lock LockKind => _lock;

        public void Setup(Lock pLock, Transform pLeaf)
        {
            _lock = pLock;
            _leaf = pLeaf;
            _prompt = pLock switch { Lock.Crowbar => "PRY OPEN", Lock.Keycard => "SWIPE KEYCARD", Lock.Keypad => "ENTER CODE", _ => "UNLOCK" };
        }

        private ItemKind Needed => _lock switch { Lock.Crowbar => ItemKind.Crowbar, Lock.Keycard => ItemKind.Keycard, Lock.Keypad => ItemKind.CodeNote, _ => ItemKind.StaffPass };

        public override string Prompt(PlayerAvatar pWho, ItemDefinition pHeld)
        {
            if (State != 0) return "";
            return Has(pWho) ? _prompt : _lock switch { Lock.Crowbar => "NEEDS A CROWBAR", Lock.Keycard => "NEEDS A KEYCARD", Lock.Keypad => "NEEDS THE CODE", _ => "NEEDS A STAFF KEY" };
        }

        public override bool CanInteract(PlayerAvatar pWho, ItemDefinition pHeld) => State == 0 && Has(pWho);

        private bool Has(PlayerAvatar pWho) => pWho != null && pWho.Weapons != null && pWho.Weapons.Carries(Needed);

        public override void InteractServer(PlayerAvatar pWho, int pHeldItem)
        {
            if (State != 0 || !Has(pWho)) return;
            // Prying is loud; a key or a card is not.
            if (_lock == Lock.Crowbar) NoiseSystem.Emit(transform.position, 10f, NoiseKind.Suspicious, pWho.OwnerClientId);
            SetStateServer(1);
            PlayEffectServer(_lock == Lock.Crowbar ? 1 : 0);
        }

        protected override void OnStateChanged(int pOld, int pNew)
        {
            if (_leaf != null)
            {
                _leaf.localRotation = Quaternion.Euler(0f, pNew == 1 ? 100f : 0f, 0f);
                foreach (Collider lCollider in _leaf.GetComponentsInChildren<Collider>()) lCollider.enabled = pNew == 0;
            }
            NavMeshObstacle lObstacle = GetComponent<NavMeshObstacle>();
            if (lObstacle != null) lObstacle.enabled = pNew == 0;
        }

        public override void OnEffect(int pEffect) => Audio.Sfx.Play(pEffect == 1 ? "crowbar" : "door_unlock", transform.position, 0.9f, pEffect == 1 ? 25f : 10f);
    }
}
