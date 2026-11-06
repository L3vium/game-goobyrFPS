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
                ItemKind.Pistol or ItemKind.SilencedPistol or ItemKind.Revolver => HeldKind.Pistol,
                ItemKind.Smg or ItemKind.Shotgun or ItemKind.Sniper or ItemKind.RocketLauncher or ItemKind.Flamethrower or ItemKind.AssaultRifle or ItemKind.SawedOff or ItemKind.Crossbow or ItemKind.GrenadeLauncher => HeldKind.Rifle,
                ItemKind.Crowbar or ItemKind.Wrench or ItemKind.Katana or ItemKind.FryingPan => HeldKind.Tool,
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
                case ItemKind.Revolver:
                    GameArt.Part(t, "Frame", lBox, new Vector3(0, 0.05f, 0.03f), new Vector3(0.05f, 0.075f, 0.14f), new Color(0.72f, 0.72f, 0.74f));
                    GameArt.Part(t, "Drum", lCyl, new Vector3(0, 0.05f, 0.05f), new Vector3(0.075f, 0.075f, 0.075f), new Color(0.5f, 0.5f, 0.54f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameArt.Part(t, "Barrel", lCyl, new Vector3(0, 0.065f, 0.17f), new Vector3(0.032f, 0.14f, 0.032f), new Color(0.72f, 0.72f, 0.74f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, -0.03f, -0.035f), new Vector3(0.05f, 0.12f, 0.06f), lWood).transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
                    lMuzzleZ = 0.25f;
                    break;
                case ItemKind.AssaultRifle:
                    GameArt.Part(t, "Stock", lBox, new Vector3(0, 0.01f, -0.17f), new Vector3(0.055f, 0.1f, 0.22f), lAccent);
                    GameArt.Part(t, "Body", lBox, new Vector3(0, 0.04f, 0.08f), new Vector3(0.07f, 0.1f, 0.34f), lMetal);
                    GameArt.Part(t, "Guard", lBox, new Vector3(0, 0.035f, 0.31f), new Vector3(0.062f, 0.075f, 0.18f), lAccent);
                    GameArt.Part(t, "Barrel", lCyl, new Vector3(0, 0.05f, 0.47f), new Vector3(0.025f, 0.2f, 0.025f), lMetal * 0.6f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameArt.Part(t, "Mag", lBox, new Vector3(0, -0.075f, 0.12f), new Vector3(0.045f, 0.16f, 0.07f), lMetal * 0.7f).transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, -0.05f, -0.03f), new Vector3(0.045f, 0.11f, 0.055f), lMetal * 0.7f).transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
                    GameArt.Part(t, "Sight", lBox, new Vector3(0, 0.105f, 0.12f), new Vector3(0.03f, 0.03f, 0.07f), lMetal * 0.5f);
                    lMuzzleZ = 0.58f;
                    break;
                case ItemKind.SawedOff:
                    GameArt.Part(t, "Stock", lBox, new Vector3(0, -0.02f, -0.06f), new Vector3(0.06f, 0.12f, 0.14f), lWood).transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                    GameArt.Part(t, "Body", lBox, new Vector3(0, 0.03f, 0.06f), new Vector3(0.08f, 0.07f, 0.14f), lMetal);
                    GameArt.Part(t, "BarrelL", lCyl, new Vector3(-0.022f, 0.045f, 0.2f), new Vector3(0.04f, 0.2f, 0.04f), lMetal * 0.7f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameArt.Part(t, "BarrelR", lCyl, new Vector3(0.022f, 0.045f, 0.2f), new Vector3(0.04f, 0.2f, 0.04f), lMetal * 0.7f).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    lMuzzleZ = 0.31f;
                    break;
                case ItemKind.Crossbow:
                    GameArt.Part(t, "Tiller", lBox, new Vector3(0, 0.02f, 0.08f), new Vector3(0.05f, 0.05f, 0.46f), lWood);
                    GameArt.Part(t, "ArmL", lBox, new Vector3(-0.15f, 0.025f, 0.28f), new Vector3(0.3f, 0.025f, 0.035f), lMetal).transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
                    GameArt.Part(t, "ArmR", lBox, new Vector3(0.15f, 0.025f, 0.28f), new Vector3(0.3f, 0.025f, 0.035f), lMetal).transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
                    GameArt.Part(t, "Bolt", lCyl, new Vector3(0, 0.055f, 0.2f), new Vector3(0.012f, 0.34f, 0.012f), new Color(0.85f, 0.25f, 0.2f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    lMuzzleZ = 0.38f;
                    break;
                case ItemKind.GrenadeLauncher:
                    GameArt.Part(t, "Stock", lBox, new Vector3(0, 0.0f, -0.14f), new Vector3(0.06f, 0.1f, 0.2f), lMetal * 0.7f);
                    GameArt.Part(t, "Drum", lCyl, new Vector3(0, 0.05f, 0.08f), new Vector3(0.17f, 0.15f, 0.17f), lAccent).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameArt.Part(t, "Barrel", lCyl, new Vector3(0, 0.06f, 0.27f), new Vector3(0.09f, 0.28f, 0.09f), lMetal).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, -0.06f, -0.02f), new Vector3(0.05f, 0.12f, 0.06f), lMetal * 0.7f);
                    lMuzzleZ = 0.42f;
                    break;
                case ItemKind.Katana:
                    GameArt.Part(t, "Handle", lBox, new Vector3(0, 0.08f, 0), new Vector3(0.03f, 0.2f, 0.035f), new Color(0.15f, 0.15f, 0.2f));
                    GameArt.Part(t, "Guard", lCyl, new Vector3(0, 0.19f, 0), new Vector3(0.08f, 0.012f, 0.08f), new Color(0.8f, 0.68f, 0.3f));
                    GameArt.Part(t, "Blade", lBox, new Vector3(0, 0.56f, 0), new Vector3(0.012f, 0.72f, 0.04f), new Color(0.88f, 0.9f, 0.94f));
                    break;
                case ItemKind.FryingPan:
                    GameArt.Part(t, "Handle", lBox, new Vector3(0, 0.1f, 0), new Vector3(0.03f, 0.28f, 0.02f), new Color(0.2f, 0.17f, 0.15f));
                    GameArt.Part(t, "Pan", lCyl, new Vector3(0, 0.36f, 0), new Vector3(0.28f, 0.04f, 0.28f), new Color(0.22f, 0.22f, 0.25f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    GameArt.Part(t, "Egg", lSphere, new Vector3(0, 0.36f, 0.025f), new Vector3(0.12f, 0.1f, 0.02f), new Color(0.98f, 0.96f, 0.9f));
                    break;
                case ItemKind.RadioDecoy:
                    GameArt.Part(t, "Box", lBox, Vector3.zero, new Vector3(0.12f, 0.1f, 0.06f), lAccent);
                    GameArt.Part(t, "Aerial", lCyl, new Vector3(0.045f, 0.12f, 0), new Vector3(0.008f, 0.16f, 0.008f), lMetal);
                    break;
                case ItemKind.Firecrackers:
                    for (int i = 0; i < 4; i++) GameArt.Part(t, "Banger", lCyl, new Vector3(-0.04f + i * 0.027f, 0, 0), new Vector3(0.024f, 0.11f, 0.024f), lAccent);
                    break;
                case ItemKind.RubberDuck:
                    GameArt.Part(t, "Body", lSphere, Vector3.zero, new Vector3(0.1f, 0.08f, 0.13f), lAccent);
                    GameArt.Part(t, "Head", lSphere, new Vector3(0, 0.065f, 0.04f), Vector3.one * 0.07f, lAccent);
                    GameArt.Part(t, "Beak", lBox, new Vector3(0, 0.06f, 0.085f), new Vector3(0.04f, 0.015f, 0.03f), new Color(0.95f, 0.5f, 0.15f));
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
                case ItemKind.Knife:
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, 0, 0.02f), new Vector3(0.028f, 0.03f, 0.11f), new Color(0.16f, 0.14f, 0.13f));
                    GameArt.Part(t, "Blade", lBox, new Vector3(0, 0.004f, 0.17f), new Vector3(0.008f, 0.032f, 0.2f), new Color(0.78f, 0.8f, 0.82f));
                    break;
                case ItemKind.Machete:
                    GameArt.Part(t, "Grip", lBox, new Vector3(0, 0, 0.03f), new Vector3(0.03f, 0.035f, 0.13f), new Color(0.32f, 0.2f, 0.12f));
                    GameArt.Part(t, "Blade", lBox, new Vector3(0, 0.012f, 0.32f), new Vector3(0.008f, 0.07f, 0.45f), new Color(0.68f, 0.7f, 0.72f));
                    break;
                case ItemKind.Bat:
                    GameArt.Part(t, "Handle", lCyl, new Vector3(0, 0, 0.12f), new Vector3(0.03f, 0.15f, 0.03f), new Color(0.62f, 0.42f, 0.24f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    GameArt.Part(t, "Barrel", lCyl, new Vector3(0, 0, 0.5f), new Vector3(0.06f, 0.26f, 0.06f), new Color(0.72f, 0.5f, 0.3f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
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
