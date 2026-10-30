using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Player
{
    /// <summary>
    /// The owner's "what am I looking at": picks the best interactable, pickup or proof in front of
    /// the camera, shows its prompt, and sends the interaction on the interact key.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerInteractor : MonoBehaviour
    {
        private PlayerAvatar _avatar;

        public static string CurrentPrompt { get; private set; } = "";

        private void Awake() => _avatar = GetComponent<PlayerAvatar>();

        private void Update()
        {
            if (!_avatar.IsSpawned || !_avatar.IsOwner) return;
            CurrentPrompt = "";
            if (!_avatar.Health.IsAlive) return;

            // Inside a hiding spot: the only thing to do is to get out.
            if (_avatar.Motor.Mode == MotorMode.Hidden)
            {
                CurrentPrompt = "[F] LEAVE";
                return;
            }

            Vector3 lOrigin = _avatar.AimOrigin;
            Vector3 lDirection = _avatar.AimDirection;
            ItemDefinition lHeld = _avatar.Weapons != null ? _avatar.Weapons.HeldItem : null;

            // The proof first: it is what everybody wants.
            MatchDirector lDirector = MatchDirector.Instance;
            ProofPickup lProof = lDirector != null && lDirector.Proof.Value == ProofState.Dropped ? FindFirstObjectByType<ProofPickup>() : null;
            if (lProof != null && Vector3.Distance(lProof.transform.position, transform.position + Vector3.up) < 2.6f)
            {
                CurrentPrompt = "[F] TAKE THE PROOF";
                if (GameInput.Interact) lDirector.TakeProofRpc();
                return;
            }

            ItemPickup lPickup = null;
            Interactable lInteractable = null;
            float lBest = float.MaxValue;

            foreach (Collider lCollider in Physics.OverlapSphere(lOrigin + lDirection * 1.4f, 1.8f, ~0, QueryTriggerInteraction.Collide))
            {
                Vector3 lPoint = lCollider.bounds.center;
                Vector3 lTo = lPoint - lOrigin;
                float lAngle = Vector3.Angle(lDirection, lTo);
                if (lAngle > 35f) continue;
                float lScore = lAngle + lTo.magnitude * 6f;

                ItemPickup lP = lCollider.GetComponentInParent<ItemPickup>();
                if (lP != null && lTo.magnitude < 3f && lScore < lBest) { lBest = lScore; lPickup = lP; lInteractable = null; continue; }

                Interactable lI = lCollider.GetComponentInParent<Interactable>();
                if (lI != null && lI.Id >= 0 && lTo.magnitude < lI.Range && lI.CanInteract(_avatar, lHeld) && lScore < lBest)
                {
                    if (Physics.Linecast(lOrigin, lPoint, GameLayers.WorldMask & ~(1 << GameLayers.INTERACTABLE), QueryTriggerInteraction.Ignore)) continue;
                    lBest = lScore;
                    lInteractable = lI;
                    lPickup = null;
                }
            }

            if (lPickup != null && lPickup.Item != null)
            {
                CurrentPrompt = "[F] TAKE " + lPickup.Item.displayName.ToUpperInvariant();
                if (GameInput.Interact) _avatar.Weapons.PickupRpc(lPickup.NetworkObject);
                return;
            }

            if (lInteractable != null)
            {
                string lPrompt = lInteractable.Prompt(_avatar, lHeld);
                if (string.IsNullOrEmpty(lPrompt)) return;
                CurrentPrompt = "[F] " + lPrompt;
                if (!GameInput.Interact) return;
                if (lInteractable.InteractLocal(_avatar)) return;
                _avatar.InteractRpc(lInteractable.Id, lHeld != null ? lHeld.Index : -1);
                FX.Juice.Squash(lInteractable.transform, 0.15f);
            }
        }
    }
}
