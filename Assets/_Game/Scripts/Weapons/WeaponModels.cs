using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The look of every item, chunky toy-like low-poly. A Blender model in
    /// <c>Resources/Models/W_&lt;Kind&gt;</c> wins when it exists; otherwise the item is assembled
    /// from boxes and prisms here. The same model serves the viewmodel, the third-person hand and
    /// the pickup on the ground. The muzzle is the child named "Muzzle".
    /// </summary>
    public static class WeaponModels
    {
        /// <summary>How a character holds an item: pistol grip, long gun, something small to throw or show.</summary>
        public static HeldKind HeldOf(ItemDefinition pItem)
        {
            if (pItem == null) return HeldKind.None;
            return pItem.kind switch
            {
                ItemKind.Fists => HeldKind.None,
                ItemKind.Pistol or ItemKind.SilencedPistol => HeldKind.Pistol,
                ItemKind.Smg or ItemKind.Shotgun or ItemKind.Sniper or ItemKind.RocketLauncher or ItemKind.Flamethrower => HeldKind.Rifle,
                ItemKind.Crowbar or ItemKind.Wrench => HeldKind.Tool,
                _ => HeldKind.Throwable
            };
        }

        public static GameObject Build(ItemDefinition pItem, Transform pParent, int pLayer)
        {
            GameObject lModel;
            // The realistic PBR model (barrel along +Z, Grip and Muzzle children), as exported.
            GameObject lRealistic = pItem != null ? ArtRegistry.Get("W_" + pItem.kind) : null;
            GameObject lPrefab = lRealistic == null && pItem != null ? Resources.Load<GameObject>("Models/W_" + pItem.kind) : null;
            if (lRealistic != null)
            {
                lModel = Object.Instantiate(lRealistic, pParent, false);
                Transform lGrip = lModel.transform.Find("Grip");
                if (lGrip != null) lModel.transform.localPosition = -lGrip.localPosition;
                if (lModel.transform.Find("Muzzle") == null)
                {
                    Transform lMuzzle = new GameObject("Muzzle").transform;
                    lMuzzle.SetParent(lModel.transform, false);
                    lMuzzle.localPosition = new Vector3(0, 0.05f, 0.5f);
                }
            }
            else if (lPrefab != null)
            {
                lModel = Object.Instantiate(lPrefab, pParent, false);
                // Exported facing -Z: turn around (the muzzle child turns with it).
                lModel.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                // Blender props carry their colours as vertex colours: the toon material does the rest.
                foreach (Renderer lRenderer in lModel.GetComponentsInChildren<Renderer>()) lRenderer.sharedMaterial = GameArt.Toon;
                if (lModel.transform.Find("Muzzle") == null)
                {
                    Transform lMuzzle = new GameObject("Muzzle").transform;
                    lMuzzle.SetParent(lModel.transform, false);
                    lMuzzle.localPosition = new Vector3(0, 0.05f, 0.5f);
                }
            }
            else lModel = Procedural(pItem, pParent);

            foreach (Collider lCollider in lModel.GetComponentsInChildren<Collider>()) Object.Destroy(lCollider);
            GameLayers.SetRecursive(lModel, pLayer);
            foreach (Renderer lRenderer in lModel.GetComponentsInChildren<Renderer>())
                if (pLayer == GameLayers.LOCAL_VIEW) lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lModel;
        }

        private static GameObject Procedural(ItemDefinition pItem, Transform pParent)
        {
            GameObject lRoot = new GameObject(pItem != null ? pItem.displayName : "Fists");
            lRoot.transform.SetParent(pParent, false);
            Transform t = lRoot.transform;
            Mesh lBox = LowPolyMesh.Cached("w.box", () => LowPolyMesh.Box());
            Mesh lCyl = LowPolyMesh.Cached("w.cyl8", () => LowPolyMesh.Cylinder(8));
            Mesh lSphere = LowPolyMesh.Cached("w.sphere", () => LowPolyMesh.Icosphere(1));
            Color lMetal = new Color(0.25f, 0.26f, 0.3f);
            Color lWood = new Color(0.55f, 0.35f, 0.2f);
            Color lAccent = pItem != null ? pItem.color : Color.white;
            float lMuzzleZ = 0.3f;

            switch (pItem != null ? pItem.kind : ItemKind.Fists)
            {
                case ItemKind.Fists:
                    break;
                case ItemKind.Pistol:
                case ItemKind.SilencedPistol:
                    GameArt.Part(t, "Slide", lBox, new Vector3(0, 0.05f, 0.05f), new Vector3(0.06f, 0.07f, 0.26f), lMetal);
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, -0.04f, -0.04f), new Vector3(0.055f, 0.14f, 0.07f), lAccent);
                    lMuzzleZ = 0.2f;
                    if (pItem.kind == ItemKind.SilencedPistol)
                    {
                        GameArt.Part(t, "Silencer", lCyl, new Vector3(0, 0.05f, 0.27f), new Vector3(0.05f, 0.1f, 0.05f), new Color(0.12f, 0.12f, 0.14f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                        lMuzzleZ = 0.38f;
                    }
                    break;
                case ItemKind.Smg:
                    GameArt.Part(t, "Body", lBox, new Vector3(0, 0.04f, 0.08f), new Vector3(0.08f, 0.11f, 0.38f), lMetal);
                    GameArt.Part(t, "Mag", lBox, new Vector3(0, -0.09f, 0.12f), new Vector3(0.05f, 0.18f, 0.06f), lAccent);
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, -0.06f, -0.04f), new Vector3(0.055f, 0.12f, 0.06f), lMetal * 0.7f);
                    GameArt.Part(t, "Barrel", lCyl, new Vector3(0, 0.05f, 0.33f), new Vector3(0.035f, 0.08f, 0.035f), lMetal * 0.6f).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    lMuzzleZ = 0.42f;
                    break;
                case ItemKind.Shotgun:
                    GameArt.Part(t, "Stock", lBox, new Vector3(0, 0.0f, -0.12f), new Vector3(0.07f, 0.1f, 0.26f), lWood);
                    GameArt.Part(t, "Body", lBox, new Vector3(0, 0.03f, 0.12f), new Vector3(0.08f, 0.09f, 0.24f), lMetal);
                    GameArt.Part(t, "Barrel", lCyl, new Vector3(0, 0.05f, 0.42f), new Vector3(0.05f, 0.24f, 0.05f), lMetal * 0.7f).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    GameArt.Part(t, "Pump", lCyl, new Vector3(0, 0.0f, 0.36f), new Vector3(0.06f, 0.1f, 0.06f), lAccent).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    lMuzzleZ = 0.67f;
                    break;
                case ItemKind.Sniper:
                    GameArt.Part(t, "Stock", lBox, new Vector3(0, 0f, -0.16f), new Vector3(0.07f, 0.12f, 0.3f), lAccent);
                    GameArt.Part(t, "Body", lBox, new Vector3(0, 0.03f, 0.14f), new Vector3(0.07f, 0.08f, 0.32f), lMetal);
                    GameArt.Part(t, "Barrel", lCyl, new Vector3(0, 0.04f, 0.55f), new Vector3(0.035f, 0.3f, 0.035f), lMetal * 0.6f).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    GameArt.Part(t, "Scope", lCyl, new Vector3(0, 0.12f, 0.12f), new Vector3(0.06f, 0.14f, 0.06f), new Color(0.1f, 0.1f, 0.12f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    lMuzzleZ = 0.86f;
                    break;
                case ItemKind.RocketLauncher:
                    GameArt.Part(t, "Tube", lCyl, new Vector3(0, 0.08f, 0.1f), new Vector3(0.16f, 0.5f, 0.16f), new Color(0.3f, 0.45f, 0.25f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, -0.06f, 0.0f), new Vector3(0.05f, 0.14f, 0.06f), lMetal);
                    GameArt.Part(t, "Warhead", LowPolyMesh.Cached("w.cone", () => LowPolyMesh.Cone(8)), new Vector3(0, 0.08f, 0.6f), new Vector3(0.13f, 0.15f, 0.13f), new Color(0.9f, 0.3f, 0.2f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    lMuzzleZ = 0.62f;
                    break;
                case ItemKind.Flamethrower:
                    GameArt.Part(t, "Tank", lCyl, new Vector3(0, -0.05f, -0.05f), new Vector3(0.14f, 0.24f, 0.14f), new Color(0.85f, 0.25f, 0.2f));
                    GameArt.Part(t, "Nozzle", lCyl, new Vector3(0, 0.04f, 0.28f), new Vector3(0.05f, 0.32f, 0.05f), lMetal).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    GameArt.Part(t, "Pilot", lSphere, new Vector3(0, 0.02f, 0.45f), Vector3.one * 0.03f, new Color(0.3f, 0.6f, 1f), GameArt.Glow);
                    lMuzzleZ = 0.48f;
                    break;
                case ItemKind.Coin:
                    GameArt.Part(t, "Coin", lCyl, Vector3.zero, new Vector3(0.06f, 0.01f, 0.06f), new Color(1f, 0.8f, 0.25f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    break;
                case ItemKind.Brick:
                    GameArt.Part(t, "Brick", lBox, Vector3.zero, new Vector3(0.1f, 0.06f, 0.2f), new Color(0.75f, 0.35f, 0.25f));
                    break;
                case ItemKind.SmokeGrenade:
                    GameArt.Part(t, "Can", lCyl, Vector3.zero, new Vector3(0.07f, 0.14f, 0.07f), new Color(0.6f, 0.65f, 0.7f));
                    GameArt.Part(t, "Top", lCyl, new Vector3(0, 0.08f, 0), new Vector3(0.04f, 0.03f, 0.04f), lMetal);
                    break;
                case ItemKind.C4:
                    GameArt.Part(t, "Block", lBox, Vector3.zero, new Vector3(0.14f, 0.05f, 0.1f), new Color(0.85f, 0.8f, 0.6f));
                    GameArt.Part(t, "Light", lSphere, new Vector3(0.04f, 0.03f, 0.02f), Vector3.one * 0.02f, Color.red, GameArt.Glow);
                    break;
                case ItemKind.Invitation:
                    GameArt.Part(t, "Card", lBox, Vector3.zero, new Vector3(0.11f, 0.004f, 0.16f), new Color(0.95f, 0.9f, 0.78f));
                    GameArt.Part(t, "Seal", lCyl, new Vector3(0, 0.004f, -0.02f), new Vector3(0.03f, 0.004f, 0.03f), new Color(0.6f, 0.08f, 0.1f));
                    break;
                case ItemKind.StaffPass:
                case ItemKind.Keycard:
                    GameArt.Part(t, "Card", lBox, Vector3.zero, new Vector3(0.055f, 0.003f, 0.086f), pItem.kind == ItemKind.Keycard ? new Color(0.75f, 0.15f, 0.15f) : new Color(0.25f, 0.45f, 0.75f));
                    GameArt.Part(t, "Chip", lBox, new Vector3(0, 0.002f, 0.02f), new Vector3(0.012f, 0.002f, 0.01f), new Color(0.85f, 0.75f, 0.35f));
                    break;
                case ItemKind.CodeNote:
                    GameArt.Part(t, "Note", lBox, Vector3.zero, new Vector3(0.075f, 0.002f, 0.075f), new Color(1f, 0.93f, 0.55f));
                    break;
                case ItemKind.Crowbar:
                    GameArt.Part(t, "Bar", lCyl, new Vector3(0, 0, 0.18f), new Vector3(0.022f, 0.3f, 0.022f), new Color(0.55f, 0.12f, 0.1f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    GameArt.Part(t, "Claw", lBox, new Vector3(0, 0.03f, 0.48f), new Vector3(0.02f, 0.07f, 0.03f), new Color(0.3f, 0.3f, 0.32f));
                    break;
                case ItemKind.Poison:
                case ItemKind.EmeticPoison:
                    GameArt.Part(t, "Vial", lCyl, Vector3.zero, new Vector3(0.04f, 0.1f, 0.04f), lAccent, GameArt.Glow);
                    GameArt.Part(t, "Cork", lCyl, new Vector3(0, 0.06f, 0), new Vector3(0.03f, 0.02f, 0.03f), lWood);
                    break;
            }

            Transform lMuzzleT = new GameObject("Muzzle").transform;
            lMuzzleT.SetParent(t, false);
            lMuzzleT.localPosition = new Vector3(0, 0.05f, lMuzzleZ);
            return lRoot;
        }
    }
}
