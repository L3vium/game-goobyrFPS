using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Match
{
    /// <summary>
    /// The proof of the kill: the target's head (a plush cartoon head, the target's hat still on)
    /// or the item it always carries. It lies where the target fell, glows, and is carried on the
    /// back of whoever took it - and drops when that player dies.
    /// </summary>
    public class ProofPickup : NetworkBehaviour
    {
        public readonly NetworkVariable<ProofKind> Kind = new NetworkVariable<ProofKind>(ProofKind.Head);
        public readonly NetworkVariable<int> Profile = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<ulong> Carrier = new NetworkVariable<ulong>(ulong.MaxValue);

        public bool Carried => Carrier.Value != ulong.MaxValue;

        private Transform _visual;
        private Light _light;
        private float _phase;

        public override void OnNetworkSpawn()
        {
            Kind.OnValueChanged += (_, __) => Build();
            Profile.OnValueChanged += (_, __) => Build();
            Build();
        }

        private void Build()
        {
            if (_visual != null) Destroy(_visual.gameObject);
            _visual = new GameObject("ProofVisual").transform;
            _visual.SetParent(transform, false);

            MapDefinition lMap = Net.LobbyState.Instance != null ? Net.LobbyState.Instance.Map : null;
            TargetProfile lProfile = lMap != null && Profile.Value >= 0 && Profile.Value < lMap.targets.Count ? lMap.targets[Profile.Value] : null;
            Mesh lSphere = LowPolyMesh.Cached("goob.sphere", () => LowPolyMesh.Icosphere(1));
            Mesh lBox = LowPolyMesh.Cached("w.box", () => LowPolyMesh.Box());
            Mesh lCyl = LowPolyMesh.Cached("w.cyl8", () => LowPolyMesh.Cylinder(8));

            switch (Kind.Value)
            {
                case ProofKind.Head:
                    GoobBody lHeadBody = new GameObject("Head").AddComponent<GoobBody>();
                    lHeadBody.transform.SetParent(_visual, false);
                    lHeadBody.Configure(Color.white, new Color(1f, 0.82f, 0.7f), lProfile != null ? lProfile.hat : HatStyle.Fedora, lProfile != null ? lProfile.bodyColor * 0.4f : Color.gray, 1f);
                    lHeadBody.Build();
                    // Keep only the head: a plush head with crossed-out eyes.
                    lHeadBody.Torso.gameObject.SetActive(false);
                    lHeadBody.HandL.gameObject.SetActive(false);
                    lHeadBody.HandR.gameObject.SetActive(false);
                    lHeadBody.FootL.gameObject.SetActive(false);
                    lHeadBody.FootR.gameObject.SetActive(false);
                    lHeadBody.Head.localPosition = Vector3.zero;
                    foreach (Transform lPupil in lHeadBody.Pupils) lPupil.localScale = new Vector3(0.9f, 0.15f, 0.3f);
                    break;
                case ProofKind.Ring:
                    GameArt.Part(_visual, "Ring", lCyl, Vector3.zero, new Vector3(0.22f, 0.04f, 0.22f), new Color(1f, 0.8f, 0.2f));
                    GameArt.Part(_visual, "Gem", lSphere, new Vector3(0, 0.05f, 0.1f), Vector3.one * 0.08f, new Color(0.3f, 0.9f, 1f), GameArt.Glow);
                    break;
                case ProofKind.Briefcase:
                    GameArt.Part(_visual, "Case", lBox, Vector3.zero, new Vector3(0.45f, 0.32f, 0.12f), new Color(0.35f, 0.22f, 0.12f));
                    GameArt.Part(_visual, "Handle", lBox, new Vector3(0, 0.2f, 0), new Vector3(0.14f, 0.05f, 0.04f), Color.black);
                    break;
                case ProofKind.Keycard:
                    GameArt.Part(_visual, "Card", lBox, Vector3.zero, new Vector3(0.22f, 0.14f, 0.02f), new Color(0.2f, 0.6f, 1f));
                    break;
                case ProofKind.Badge:
                    GameArt.Part(_visual, "Badge", LowPolyMesh.Cached("goob.crown", () => LowPolyMesh.Cylinder(6, 1.2f)), Vector3.zero, new Vector3(0.22f, 0.04f, 0.22f), new Color(1f, 0.75f, 0.2f));
                    break;
                case ProofKind.Ledger:
                    GameArt.Part(_visual, "Book", lBox, Vector3.zero, new Vector3(0.3f, 0.06f, 0.22f), new Color(0.6f, 0.15f, 0.15f));
                    break;
            }
            foreach (Collider lCollider in _visual.GetComponentsInChildren<Collider>()) Destroy(lCollider);
            GameLayers.SetRecursive(_visual.gameObject, GameLayers.PICKUP);

            _light = _visual.gameObject.AddComponent<Light>();
            _light.color = new Color(1f, 0.85f, 0.3f);
            _light.range = 5f;
            _light.intensity = 3f;
        }

        private void Update()
        {
            _phase += Time.deltaTime;
            PlayerAvatar lCarrier = Carried ? PlayerAvatar.ForClient(Carrier.Value) : null;

            if (IsServer && lCarrier != null)
                transform.position = lCarrier.transform.position + Vector3.up * 1.2f - lCarrier.transform.forward * 0.45f;

            if (_visual == null) return;
            bool lMine = lCarrier != null && lCarrier.IsOwner;
            _visual.gameObject.SetActive(!lMine);
            _visual.localPosition = Carried ? Vector3.zero : new Vector3(0, 0.5f + Mathf.Sin(_phase * 2.5f) * 0.1f, 0);
            _visual.localRotation = Quaternion.Euler(0, _phase * 70f, Carried ? 0f : Mathf.Sin(_phase) * 10f);
            if (_light != null) _light.intensity = Carried ? 1.5f : 3f + Mathf.Sin(_phase * 5f);
        }
    }
}
