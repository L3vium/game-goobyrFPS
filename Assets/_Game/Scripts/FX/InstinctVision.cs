using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Match;
using Com.IsartDigital.GoobyrFPS.Net;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Instinct: hold the instinct key and the world dims to a cold grey while what matters glows
    /// through the walls - the target and the proof in red at any distance, teammates in their
    /// colour, NPCs nearby in white (amber when they hunt you), and everything you can use or pick
    /// up around you in white. Purely local: it changes what this player sees, never the game.
    /// <para>
    /// This collects what to draw; <see cref="Rendering.InstinctFeature"/> grades the frame and
    /// draws the silhouettes.
    /// </para>
    /// </summary>
    public class InstinctVision : MonoBehaviour
    {
        public enum Slot { Target, Ally, Alert, Npc, Use }

        public struct Draw
        {
            public Renderer renderer;
            public Slot slot;
            public int submeshes;
        }

        /// <summary>HDR colours per slot, so the bloom catches them.</summary>
        public static readonly Color[] Palette =
        {
            new Color(4.2f, 0.28f, 0.3f, 1f),
            new Color(0.5f, 1.4f, 3.2f, 1f),
            new Color(3.4f, 1.5f, 0.25f, 1f),
            new Color(1.25f, 1.3f, 1.4f, 0.6f),
            new Color(2.4f, 2.4f, 2.4f, 1f),
        };

        /// <summary>Instinct: the target wherever it is, people within 55 m, objects close by (25 m).</summary>
        public const float TARGET_RANGE = 10000f;
        public const float NPC_RANGE = 55f;
        public const float USE_RANGE = 25f;
        private const float REFRESH = 0.12f;
        private const float WAVE_SPEED = 85f;
        private const float WAVE_REACH = 140f;

        /// <summary>0 off, 1 fully on (eased).</summary>
        public static float Amount { get; private set; }
        /// <summary>Distance of the scan wave from the eye, and its strength.</summary>
        public static float Wave { get; private set; }
        public static float WaveFade { get; private set; }
        public static IReadOnlyList<Draw> Draws => _draws;
        public static bool Active => Amount > 0.001f;

        private static readonly List<Draw> _draws = new List<Draw>();
        private static readonly List<Renderer> _buffer = new List<Renderer>();
        private static readonly Dictionary<Interactable, Renderer[]> _interactableRenderers = new Dictionary<Interactable, Renderer[]>();

        private bool _on;
        private float _nextRefresh;

        private void OnDestroy()
        {
            Amount = 0f;
            _draws.Clear();
            _interactableRenderers.Clear();
        }

        private void Update()
        {
            PlayerAvatar lLocal = PlayerAvatar.Local;
            bool lWant = GameInput.InstinctHeld && lLocal != null && lLocal.Health != null && lLocal.Health.IsAlive;
            if (lWant && !_on)
            {
                Wave = 0f;
                WaveFade = 1f;
                _nextRefresh = 0f;
                Audio.Sfx.Play2D("map", 0.35f);
                Juice.Aberration(0.12f);
            }
            _on = lWant;

            float dt = Time.unscaledDeltaTime;
            Amount = Mathf.MoveTowards(Amount, _on ? 1f : 0f, dt * (_on ? 5f : 7f));
            Wave += dt * WAVE_SPEED;
            WaveFade = _on ? Mathf.Clamp01(1f - Wave / WAVE_REACH) : Mathf.MoveTowards(WaveFade, 0f, dt * 4f);

            if (!Active) { _draws.Clear(); return; }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + REFRESH;
            Collect(lLocal);
        }

        /// <summary>Builds the draw list, most important first: a pixel goes to the first one drawn.</summary>
        private static void Collect(PlayerAvatar pLocal)
        {
            _draws.Clear();
            if (pLocal == null) return;
            Vector3 lEye = pLocal.Eye;

            // The target and the proof, when close enough to sense.
            float lTargetRange = TARGET_RANGE * TARGET_RANGE;
            foreach (Npc lNpc in Npc.All)
                if (lNpc != null && lNpc.IsTarget && lNpc.Health.IsAlive && (lNpc.transform.position - lEye).sqrMagnitude < lTargetRange) AddTree(lNpc.transform, Slot.Target);
            ProofPickup lProof = ProofPickup.Current;
            if (lProof != null && (lProof.transform.position - lEye).sqrMagnitude < lTargetRange)
            {
                PlayerAvatar lCarrier = lProof.Carried ? PlayerAvatar.ForClient(lProof.Carrier.Value) : null;
                if (lCarrier == null) AddTree(lProof.transform, Slot.Target);
                else if (lCarrier != pLocal) AddBody(lCarrier.Body, Slot.Target);
            }

            // Teammates (nobody is a teammate in free for all).
            bool lTeams = LobbyState.Instance == null || LobbyState.Instance.Mode.Value != MatchMode.FreeForAll;
            if (lTeams)
                foreach (PlayerAvatar lOther in PlayerAvatar.All)
                    if (lOther != null && lOther != pLocal && lOther.Team.Value == pLocal.Team.Value && lOther.Health.IsAlive) AddBody(lOther.Body, Slot.Ally);

            // NPCs around: the ones hunting you first.
            float lNpcRange = NPC_RANGE * NPC_RANGE;
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || lNpc.IsTarget || !lNpc.Health.IsAlive) continue;
                if ((lNpc.transform.position - lEye).sqrMagnitude > lNpcRange) continue;
                if (lNpc.IsAlerted || lNpc.Awareness.Value >= 10) AddTree(lNpc.transform, Slot.Alert);
            }
            foreach (Npc lNpc in Npc.All)
            {
                if (lNpc == null || lNpc.IsTarget || !lNpc.Health.IsAlive || lNpc.IsAlerted || lNpc.Awareness.Value >= 10) continue;
                if ((lNpc.transform.position - lEye).sqrMagnitude > lNpcRange) continue;
                AddTree(lNpc.transform, Slot.Npc);
            }

            // What you can use or take.
            float lUseRange = USE_RANGE * USE_RANGE;
            foreach (Interactable lInteractable in WorldRegistry.All)
            {
                if (lInteractable == null || !lInteractable.isActiveAndEnabled || !Usable(lInteractable)) continue;
                if ((lInteractable.transform.position - lEye).sqrMagnitude > lUseRange) continue;
                foreach (Renderer lRenderer in RenderersOf(lInteractable)) Add(lRenderer, Slot.Use);
            }
            foreach (ItemPickup lPickup in ItemPickup.All)
            {
                if (lPickup == null || (lPickup.transform.position - lEye).sqrMagnitude > lUseRange) continue;
                AddTree(lPickup.transform, Slot.Use);
            }
        }

        private static void Add(Renderer pRenderer, Slot pSlot)
        {
            if (pRenderer == null || !pRenderer.enabled || !pRenderer.gameObject.activeInHierarchy) return;
            if (!(pRenderer is MeshRenderer) && !(pRenderer is SkinnedMeshRenderer)) return;
            _draws.Add(new Draw { renderer = pRenderer, slot = pSlot, submeshes = Mathf.Max(1, pRenderer.sharedMaterials.Length) });
        }

        private static void AddTree(Transform pRoot, Slot pSlot)
        {
            pRoot.GetComponentsInChildren(false, _buffer);
            foreach (Renderer lRenderer in _buffer) if (!IsGlow(lRenderer)) Add(lRenderer, pSlot);
        }

        private static void AddBody(GoobBody pBody, Slot pSlot)
        {
            if (pBody == null || pBody.Renderers == null) return;
            foreach (Renderer lRenderer in pBody.Renderers) Add(lRenderer, pSlot);
        }

        /// <summary>Glow rings and light cards stay out: the silhouette is the object itself.</summary>
        private static bool IsGlow(Renderer pRenderer) => pRenderer.name == "Ring" || pRenderer.name == "Glow" || pRenderer.name.EndsWith("_Glow");

        /// <summary>
        /// What the player can actually use: sprinkler heads, burning crates and patrol cars are
        /// interactables for the systems, not for the player.
        /// </summary>
        private static bool Usable(Interactable pInteractable) => !(pInteractable is Sprinkler) && !(pInteractable is Flammable) && !(pInteractable is VehiclePatrol);

        /// <summary>
        /// The meshes of an interactable: its own, or - for a trigger put next to a kit piece (a breaker
        /// box, a toilet, a vent) - the small meshes of its parent close to it.
        /// </summary>
        private static Renderer[] RenderersOf(Interactable pInteractable)
        {
            if (_interactableRenderers.TryGetValue(pInteractable, out Renderer[] lCached)) return lCached;
            List<Renderer> lFound = new List<Renderer>();
            foreach (Renderer lRenderer in pInteractable.GetComponentsInChildren<Renderer>(true))
                if ((lRenderer is MeshRenderer || lRenderer is SkinnedMeshRenderer) && !IsGlow(lRenderer)) lFound.Add(lRenderer);
            if (lFound.Count == 0 && pInteractable.transform.parent != null)
            {
                Vector3 lPoint = pInteractable.transform.position;
                foreach (Renderer lRenderer in pInteractable.transform.parent.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(lRenderer is MeshRenderer) || IsGlow(lRenderer)) continue;
                    Bounds lBounds = lRenderer.bounds;
                    if (lBounds.size.magnitude < 4.5f && lBounds.SqrDistance(lPoint) < 1.2f * 1.2f) lFound.Add(lRenderer);
                }
            }
            lCached = lFound.ToArray();
            _interactableRenderers[pInteractable] = lCached;
            return lCached;
        }
    }
}
