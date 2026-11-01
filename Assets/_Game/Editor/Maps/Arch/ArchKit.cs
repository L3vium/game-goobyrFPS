using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.Systems;
using Com.IsartDigital.GoobyrFPS.UI;
using Unity.AI.Navigation;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The systemic props of the realistic maps, built from the finished models (or PBR boxes): drinks,
    /// toilets, wardrobes, dumpsters, crates and laundry carts to hide in, TVs, radios, phones and
    /// generators that distract, explosive barrels and gas tanks, CCTV cameras and the recorder,
    /// breakers, the air handler, drop ladders, chandeliers, ziplines, chatter spots and map POIs.
    /// Markers go through <see cref="MapKit"/>.
    /// </summary>
    public class ArchKit
    {
        public readonly MapKit K;
        public readonly Transform Props, Systems;
        private readonly System.Random _rng;
        private readonly List<Vector3> _hides = new List<Vector3>();
        private int _unique;

        /// <summary>Every hiding container placed so far (for the "container within 15 m" rule).</summary>
        public IReadOnlyList<Vector3> Hides => _hides;

        public ArchKit(MapKit pKit)
        {
            K = pKit;
            Props = K.Props;
            Systems = K.Group("ArchSystems", K.Props);
            _rng = new System.Random(K.Map.sceneName.GetHashCode() ^ 0x5a17);
        }

        public float R => (float)_rng.NextDouble();
        public T Pick<T>(params T[] pItems) => pItems[_rng.Next(pItems.Length)];

        private static ArchMat Wood => ArchLib.Surface("old_planks_02", 1.5f);
        private static ArchMat DarkWood => ArchLib.Surface("dark_wooden_planks", 1.5f);

        /// <summary>A model with a box collider (or a PBR box of its manifest size).</summary>
        public GameObject Model(string pName, Vector3 pPosition, float pYaw, Transform pParent = null, ArchLib.Collide pCollide = ArchLib.Collide.Box, float pScale = 1f)
        {
            GameObject lObject = ArchLib.Place(pName, pParent ?? Props, pPosition, pYaw, pCollide, Wood, pScale);
            if (lObject != null && pCollide == ArchLib.Collide.Box) Cover(lObject);
            return lObject;
        }

        /// <summary>Registers an object waist-high or taller (and not huge) as cover for the AI.</summary>
        public void Cover(GameObject pObject)
        {
            BoxCollider lBox = pObject.GetComponent<BoxCollider>();
            if (lBox == null || lBox.isTrigger) return;
            Vector3 lSize = Vector3.Scale(lBox.size, pObject.transform.lossyScale);
            if (lSize.y < 0.8f || lSize.x * lSize.z > 30f || lSize.y > 4f) return;
            K.AddCover(pObject.transform.TransformPoint(lBox.center), lSize, pObject.transform.rotation);
        }

        /// <summary>A box mesh object (no model exists for it): a dumpster, a cart, a pew.</summary>
        public GameObject BoxObject(string pName, Vector3 pPosition, float pYaw, Transform pParent, int pLayer, bool pCollider, params (ArchMat mat, Vector3 center, Vector3 size)[] pParts)
        {
            GameObject lObject = new GameObject(pName) { layer = pLayer };
            lObject.transform.SetParent(pParent ?? Props, false);
            lObject.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            ArchMesh lMesh = new ArchMesh();
            Bounds lBounds = new Bounds(pParts[0].center, pParts[0].size);
            foreach ((ArchMat lMat, Vector3 lCenter, Vector3 lSize) in pParts)
            {
                lMesh.Box(lMat, lCenter, lSize, false);
                lBounds.Encapsulate(new Bounds(lCenter, lSize));
            }
            // Same parts, same mesh: shared between every copy of a shape.
            int lHash = 17;
            foreach ((ArchMat lMat, Vector3 lCenter, Vector3 lSize) in pParts) lHash = lHash * 31 + (lMat.id + lCenter.ToString("F2") + lSize.ToString("F2")).GetHashCode();
            foreach (GameObject lPart in lMesh.Flush(lObject.transform, "Shape_" + pName + "_" + ((uint)lHash).ToString("X8"), pLayer))
                lPart.isStatic = pLayer == GameLayers.DEFAULT;
            if (pCollider)
            {
                BoxCollider lBox = lObject.AddComponent<BoxCollider>();
                lBox.center = lBounds.center;
                lBox.size = lBounds.size;
                Cover(lObject);
            }
            lObject.isStatic = pLayer == GameLayers.DEFAULT;
            return lObject;
        }

        // ---------------------------------------------------------------- drinks, toilets

        /// <summary>Poisonable glasses (and a bottle) on a surface, plus the Drink activity in front.</summary>
        public void Drinks(Vector3 pSurface, float pYaw, int pZone, int pCount, bool pActivity = true, float pFront = 1.0f)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            for (int i = 0; i < pCount; i++)
            {
                Vector3 lPos = pSurface + lRot * new Vector3((i - (pCount - 1) * 0.5f) * 0.35f, 0f, 0f);
                GameObject lGlass = new GameObject("Drink") { layer = GameLayers.INTERACTABLE };
                lGlass.transform.SetParent(Systems, false);
                lGlass.transform.position = lPos;
                ArchMesh lMesh = new ArchMesh();
                lMesh.Cylinder(ArchLib.Glass, Vector3.zero, 0.04f, 0.13f, 10, false, false);
                lMesh.Flush(lGlass.transform, "Arch_DrinkGlass");
                GameObject lLiquid = new GameObject("Liquid");
                lLiquid.transform.SetParent(lGlass.transform, false);
                ArchMesh lLiquidMesh = new ArchMesh();
                lLiquidMesh.Cylinder(ArchLib.PlainMat("Aguardiente", new Color(0.75f, 0.5f, 0.2f, 1f), 0.9f, 0f), Vector3.up * 0.005f, 0.035f, 0.08f, 10, false);
                List<GameObject> lParts = lLiquidMesh.Flush(lLiquid.transform, "Arch_DrinkLiquid");
                // Drink looks for a renderer on "Liquid": move it up one level.
                if (lParts.Count > 0)
                {
                    lLiquid.AddComponent<MeshFilter>().sharedMesh = lParts[0].GetComponent<MeshFilter>().sharedMesh;
                    lLiquid.AddComponent<MeshRenderer>().sharedMaterial = lParts[0].GetComponent<MeshRenderer>().sharedMaterial;
                    Object.DestroyImmediate(lParts[0]);
                }
                foreach (Transform t in lGlass.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
                BoxCollider lBox = lGlass.AddComponent<BoxCollider>();
                lBox.size = new Vector3(0.3f, 0.35f, 0.3f);
                lBox.center = new Vector3(0, 0.15f, 0);
                lBox.isTrigger = true;
                lGlass.AddComponent<Drink>();
            }
            Model("W_Bottle", pSurface + lRot * new Vector3((pCount * 0.5f + 0.2f) * 0.35f, 0f, 0.05f), pYaw, null, ArchLib.Collide.None);
            if (pActivity) K.Activity(ActivityType.Drink, pSurface + lRot * new Vector3(0, 0, -pFront), pYaw, pZone, 14f);
        }

        public void Toilet(Vector3 pPosition, float pYaw, int pZone)
        {
            GameObject lToilet = Model("Furn_Toilet", pPosition, pYaw, Systems);
            if (lToilet == null) return;
            lToilet.AddComponent<Toilet>();
            K.Activity(ActivityType.Toilet, pPosition + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0, 0.75f), pYaw + 180f, pZone, 10f);
        }

        // ---------------------------------------------------------------- hiding spots

        private void Hiding(GameObject pObject, Vector3 pInside, Vector3 pExit, string pPrompt)
        {
            foreach (Transform t in pObject.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            Transform lInside = new GameObject("Inside").transform;
            lInside.SetParent(pObject.transform, false);
            lInside.position = pInside;
            Transform lExit = new GameObject("Exit").transform;
            lExit.SetParent(pObject.transform, false);
            lExit.position = pExit;
            pObject.AddComponent<HidingSpot>().Setup(lInside, lExit, pPrompt);
            _hides.Add(pObject.transform.position);
        }

        public void Wardrobe(Vector3 pPosition, float pYaw, Transform pParent = null)
        {
            GameObject lWardrobe = Model("Furn_Wardrobe", pPosition, pYaw, pParent ?? Systems);
            if (lWardrobe == null) return;
            Hiding(lWardrobe, pPosition + Vector3.up * 0.05f, pPosition + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0.05f, 1.0f), "HIDE IN THE WARDROBE");
        }

        public void Dumpster(Vector3 pPosition, float pYaw)
        {
            ArchMat lMetal = ArchLib.Surface("green_metal_rust", 1.5f), lDark = ArchLib.Iron;
            GameObject lBin = BoxObject("Dumpster", pPosition, pYaw, Systems, GameLayers.INTERACTABLE, true,
                (lMetal, new Vector3(0, 0.65f, 0), new Vector3(1.9f, 1.1f, 1.15f)),
                (lMetal, new Vector3(0, 1.24f, -0.1f), new Vector3(1.95f, 0.06f, 1.3f)),
                (lDark, new Vector3(-0.75f, 0.06f, -0.4f), new Vector3(0.12f, 0.12f, 0.12f)),
                (lDark, new Vector3(0.75f, 0.06f, -0.4f), new Vector3(0.12f, 0.12f, 0.12f)),
                (lDark, new Vector3(-0.75f, 0.06f, 0.4f), new Vector3(0.12f, 0.12f, 0.12f)),
                (lDark, new Vector3(0.75f, 0.06f, 0.4f), new Vector3(0.12f, 0.12f, 0.12f)));
            Hiding(lBin, pPosition + Vector3.up * 0.1f, pPosition + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0.05f, 1.3f), "HIDE IN THE DUMPSTER");
        }

        public void BigCrate(Vector3 pPosition, float pYaw)
        {
            ArchMat lWood = ArchLib.Surface("wooden_planks", 1.2f);
            GameObject lCrate = BoxObject("HideCrate", pPosition, pYaw, Systems, GameLayers.INTERACTABLE, true,
                (lWood, new Vector3(0, 0.65f, 0), new Vector3(1.6f, 1.3f, 1.2f)),
                (DarkWood, new Vector3(0, 0.65f, -0.61f), new Vector3(1.64f, 0.12f, 0.03f)),
                (DarkWood, new Vector3(0, 0.65f, 0.61f), new Vector3(1.64f, 0.12f, 0.03f)));
            Hiding(lCrate, pPosition + Vector3.up * 0.1f, pPosition + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0.05f, 1.3f), "HIDE IN THE CRATE");
            lCrate.AddComponent<Flammable>().Setup(14f, 2.6f, false, 1.2f);
        }

        public void LaundryCart(Vector3 pPosition, float pYaw)
        {
            ArchMat lCloth = ArchLib.Surface("hessian_230", 1f), lMetal = ArchLib.Galvanised;
            GameObject lCart = BoxObject("LaundryCart", pPosition, pYaw, Systems, GameLayers.INTERACTABLE, true,
                (lCloth, new Vector3(0, 0.6f, 0), new Vector3(1.1f, 0.8f, 0.75f)),
                (lMetal, new Vector3(0, 0.18f, 0), new Vector3(1.15f, 0.06f, 0.8f)),
                (lMetal, new Vector3(-0.5f, 0.07f, -0.32f), new Vector3(0.1f, 0.14f, 0.1f)),
                (lMetal, new Vector3(0.5f, 0.07f, 0.32f), new Vector3(0.1f, 0.14f, 0.1f)));
            Hiding(lCart, pPosition + Vector3.up * 0.1f, pPosition + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0.05f, 1.0f), "HIDE IN THE LAUNDRY CART");
        }

        /// <summary>A banana-leaf pile, a haystack of coffee sacks: soft hiding outdoors.</summary>
        public void Sacks(Vector3 pPosition, float pYaw)
        {
            ArchMat lCloth = ArchLib.Surface("hessian_230", 0.8f);
            GameObject lPile = BoxObject("SackPile", pPosition, pYaw, Systems, GameLayers.INTERACTABLE, true,
                (lCloth, new Vector3(0, 0.35f, 0), new Vector3(1.8f, 0.7f, 1.4f)),
                (lCloth, new Vector3(0.1f, 0.9f, 0.05f), new Vector3(1.4f, 0.5f, 1.1f)),
                (lCloth, new Vector3(-0.2f, 1.25f, 0), new Vector3(0.9f, 0.3f, 0.7f)));
            Hiding(lPile, pPosition + Vector3.up * 0.1f, pPosition + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 0.05f, 1.3f), "HIDE IN THE SACKS");
            lPile.AddComponent<Flammable>().Setup(15f, 3f, false, 1.2f);
        }

        // ---------------------------------------------------------------- distractions, hazards

        /// <summary>tv, radio, phone, generator, caralarm (with the matching model).</summary>
        public void Distraction(string pKind, Vector3 pPosition, float pYaw)
        {
            GameObject lObject;
            switch (pKind)
            {
                case "tv": lObject = Model("Furn_TV", pPosition, pYaw, Systems); break;
                case "generator": lObject = Model("Prop_Generator", pPosition, pYaw, Systems); break;
                case "caralarm": lObject = Model(R < 0.5f ? "Prop_Car_Sedan" : "Prop_Car_Pickup", pPosition, pYaw, Systems); break;
                case "phone":
                    lObject = BoxObject("Phone", pPosition, pYaw, Systems, GameLayers.INTERACTABLE, true,
                        (ArchLib.PlainMat("Bakelite", new Color(0.05f, 0.05f, 0.05f), 0.8f, 0f), new Vector3(0, 0.05f, 0), new Vector3(0.22f, 0.1f, 0.2f)),
                        (ArchLib.PlainMat("Bakelite", new Color(0.05f, 0.05f, 0.05f), 0.8f, 0f), new Vector3(0, 0.13f, 0), new Vector3(0.24f, 0.05f, 0.06f)));
                    break;
                default:
                    lObject = BoxObject("Radio", pPosition, pYaw, Systems, GameLayers.INTERACTABLE, true,
                        (ArchLib.Surface("walnut_veneer", 0.5f), new Vector3(0, 0.15f, 0), new Vector3(0.45f, 0.3f, 0.2f)),
                        (ArchLib.Surface("rough_linen", 0.3f), new Vector3(0.08f, 0.15f, 0.101f), new Vector3(0.22f, 0.2f, 0.01f)));
                    pKind = "radio";
                    break;
            }
            if (lObject == null) return;
            foreach (Transform t in lObject.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            string lPrompt = pKind switch { "radio" => "TURN THE RADIO ON", "tv" => "TURN THE TV ON", "phone" => "MAKE THE PHONE RING", "generator" => "START THE GENERATOR", "caralarm" => "SET OFF THE CAR ALARM", _ => "MAKE SOME NOISE" };
            lObject.AddComponent<Systems.Distraction>().Setup(lPrompt, pKind, pKind == "generator" || pKind == "caralarm" ? 28f : 16f);
        }

        /// <summary>A red barrel or a gas tank that explodes when shot or set on fire.</summary>
        public GameObject Explosive(Vector3 pPosition, float pYaw, bool pTank = false)
        {
            GameObject lObject = Model(pTank ? "Prop_PropaneTank" : "Prop_ExplosiveBarrel", pPosition, pYaw, Systems);
            if (lObject == null) return null;
            foreach (Transform t in lObject.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            lObject.AddComponent<ExplosiveProp>();
            return lObject;
        }

        public GameObject Crate(Vector3 pPosition, float pYaw, bool pMilitary = false)
        {
            GameObject lObject = Model(pMilitary ? "Prop_Crate_Military" : "Prop_Crate_Wood", pPosition, pYaw);
            if (lObject != null) { lObject.isStatic = false; lObject.AddComponent<Flammable>().Setup(12f, 2.4f, false, 0.8f); foreach (Transform t in lObject.GetComponentsInChildren<Transform>()) t.gameObject.layer = GameLayers.INTERACTABLE; }
            return lObject;
        }

        /// <summary>A stack of crates on a pallet (cover).</summary>
        public void CrateStack(Vector3 pPosition, float pYaw, int pCount, bool pMilitary = false)
        {
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            Model("Prop_Pallet", pPosition, pYaw);
            Vector3 lSize = ArchLib.ManifestSize(pMilitary ? "Prop_Crate_Military" : "Prop_Crate_Wood");
            for (int i = 0; i < pCount; i++)
            {
                Vector3 lOffset = new Vector3((i % 2 - 0.5f) * (lSize.x + 0.02f), 0.145f + (i / 4) * lSize.y, ((i / 2) % 2 - 0.5f) * Mathf.Min(0.4f, lSize.z * 0.35f));
                Crate(pPosition + lRot * lOffset, pYaw + (R - 0.5f) * 6f, pMilitary);
            }
        }

        // ---------------------------------------------------------------- security

        /// <summary>A CCTV camera on a wall, looking at a point; reports to the zone's guards.</summary>
        public SecurityCamera Camera(Vector3 pMount, Vector3 pLookAt, int pZone, float pRange = 18f)
        {
            GameObject lRoot = new GameObject("SecurityCamera");
            lRoot.transform.SetParent(Systems, false);
            lRoot.transform.position = pMount;
            Vector3 lDir = (pLookAt - pMount).normalized;
            lRoot.transform.rotation = Quaternion.LookRotation(lDir);
            GameObject lModel = ArchLib.Place("Prop_SecurityCamera", lRoot.transform, pMount - Vector3.up * 0.15f, lRoot.transform.eulerAngles.y, ArchLib.Collide.None, ArchLib.Iron);
            if (lModel != null) { lModel.transform.SetParent(lRoot.transform, true); foreach (Transform t in lModel.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false; }
            SecurityCamera lCamera = lRoot.AddComponent<SecurityCamera>();
            lCamera.Setup(pZone, pRange);
            GameObject lPoi = new GameObject("Poi_Camera");
            lPoi.transform.SetParent(lRoot.transform, false);
            return lCamera;
        }

        public void Recorder(Vector3 pPosition, float pYaw, int pZone)
        {
            GameObject lRack = Model("Furn_ServerRack", pPosition, pYaw, Systems);
            if (lRack == null) return;
            foreach (Transform t in lRack.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            lRack.AddComponent<CameraRecorder>().Setup(pZone);
        }

        public Breaker Breaker(Vector3 pWallPoint, float pYaw, BuildingVolume pBuilding)
        {
            GameObject lBox = Model("Prop_BreakerBox", pWallPoint + Vector3.up * 1.2f, pYaw, Systems, ArchLib.Collide.Box);
            if (lBox == null) return null;
            foreach (Transform t in lBox.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            GameObject lLever = BoxObject("Lever", pWallPoint + Vector3.up * 1.45f + Quaternion.Euler(0, pYaw, 0) * new Vector3(0.2f, 0, 0.08f), pYaw, lBox.transform, GameLayers.INTERACTABLE, false,
                (ArchLib.PlainMat("LeverRed", new Color(0.7f, 0.08f, 0.06f), 0.5f, 0f), new Vector3(0, 0, 0), new Vector3(0.05f, 0.2f, 0.05f)));
            Breaker lBreaker = lBox.AddComponent<Breaker>();
            lBreaker.Setup(pBuilding, lLever.transform);
            return lBreaker;
        }

        /// <summary>The building's air handler: poison it and the gas comes out of every listed vent.</summary>
        public void AirHandler(Vector3 pPosition, float pYaw, BuildingVolume pBuilding, Transform[] pVents)
        {
            GameObject lUnit = Model("Prop_AirHandler", pPosition, pYaw, Systems);
            if (lUnit == null) return;
            foreach (Transform t in lUnit.GetComponentsInChildren<Transform>()) { t.gameObject.layer = GameLayers.INTERACTABLE; t.gameObject.isStatic = false; }
            lUnit.AddComponent<VentPoison>().Setup(pBuilding, pVents);
        }

        /// <summary>
        /// A ladder folded on a roof or a tower, lowered from the top (stays down): the lever at the
        /// top, the ladder hanging from <paramref name="pTop"/> to <paramref name="pBottom"/>
        /// against a wall whose outward normal is <paramref name="pOut"/>.
        /// </summary>
        public void DropLadder(Vector3 pTop, Vector3 pBottom, Vector3 pOut, string pPoi)
        {
            float lYaw = Quaternion.LookRotation(pOut).eulerAngles.y;
            GameObject lRoot = new GameObject("DropLadder") { layer = GameLayers.INTERACTABLE };
            lRoot.transform.SetParent(Systems, false);
            lRoot.transform.position = pTop + Vector3.up * 0.6f - pOut * 0.4f;
            GameObject lHook = BoxObject("Hook", lRoot.transform.position, lYaw, lRoot.transform, GameLayers.INTERACTABLE, true,
                (ArchLib.Iron, new Vector3(0, 0, 0), new Vector3(0.6f, 0.9f, 0.12f)),
                (ArchLib.PlainMat("LeverRed", new Color(0.7f, 0.08f, 0.06f), 0.5f, 0f), new Vector3(0, 0.3f, 0.1f), new Vector3(0.08f, 0.3f, 0.08f)));
            GameObject lLadder = new GameObject("Ladder");
            lLadder.transform.SetParent(lRoot.transform, false);
            float lHeight = pTop.y - pBottom.y;
            Vector3 lFoot = new Vector3(pTop.x, pBottom.y, pTop.z) + pOut * 0.25f;
            for (float y = 0f; y < lHeight - 0.2f; y += 3f) ArchLib.Place("Prop_Ladder", lLadder.transform, lFoot + Vector3.up * y, lYaw, ArchLib.Collide.None, ArchLib.Iron);
            GameObject lClimb = new GameObject("Climbable") { layer = GameLayers.CLIMBABLE };
            lClimb.transform.SetParent(lLadder.transform, false);
            lClimb.transform.position = lFoot + Vector3.up * (lHeight * 0.5f + 0.5f) + pOut * 0.05f;
            lClimb.transform.rotation = Quaternion.LookRotation(pOut);
            BoxCollider lBox = lClimb.AddComponent<BoxCollider>();
            lBox.size = new Vector3(0.6f, lHeight + 1f, 0.2f);
            lLadder.SetActive(false);
            lRoot.AddComponent<DropLadder>().Setup(lLadder);
            Poi(pBottom + pOut * 0.6f, pPoi, MapPoi.Kind.Shortcut);
        }

        /// <summary>A fixed climb: a ladder or a vine trellis against a wall (layer Climbable).</summary>
        public void Climb(Vector3 pBottom, float pHeight, Vector3 pOut, bool pLadder, string pPoi)
        {
            float lYaw = Quaternion.LookRotation(pOut).eulerAngles.y;
            Transform lRoot = new GameObject(pLadder ? "Ladder" : "Trellis").transform;
            lRoot.SetParent(Props, false);
            lRoot.position = pBottom;
            if (pLadder)
                for (float y = 0f; y < pHeight - 0.2f; y += 3f) ArchLib.Place("Prop_Ladder", lRoot, pBottom + Vector3.up * y, lYaw, ArchLib.Collide.None, ArchLib.Iron);
            else
            {
                ArchMesh lMesh = new ArchMesh();
                ArchMat lWood = ArchLib.Surface("rough_wood", 1f);
                Quaternion lRot = Quaternion.Euler(0, lYaw, 0);
                for (float x = -0.6f; x <= 0.61f; x += 0.3f) lMesh.Box(lWood, lRot * new Vector3(x, pHeight * 0.5f, 0.06f), new Vector3(0.04f, pHeight, 0.04f), lRot, false);
                for (float y = 0.3f; y < pHeight; y += 0.3f) lMesh.Box(lWood, new Vector3(0, y, 0) + lRot * new Vector3(0, 0, 0.1f), new Vector3(1.3f, 0.035f, 0.035f), lRot, false);
                lMesh.Flush(lRoot, K.Map.sceneName + "_Trellis_" + (_unique++));
                // Vines on it.
                for (float y = 0.2f; y < pHeight - 0.5f; y += 1.1f)
                    ArchLib.Place(R < 0.5f ? "Veg_JungleBush_A" : "Veg_Fern", lRoot, pBottom + Vector3.up * y + pOut * 0.15f + Quaternion.Euler(0, lYaw, 0) * new Vector3((R - 0.5f) * 0.8f, 0, 0), lYaw + R * 360f, ArchLib.Collide.None, null, 0.7f);
            }
            GameObject lClimb = new GameObject("Climbable") { layer = GameLayers.CLIMBABLE };
            lClimb.transform.SetParent(lRoot, false);
            lClimb.transform.position = pBottom + Vector3.up * (pHeight * 0.5f + 0.5f) + pOut * 0.12f;
            lClimb.transform.rotation = Quaternion.LookRotation(pOut);
            BoxCollider lBox = lClimb.AddComponent<BoxCollider>();
            lBox.size = new Vector3(pLadder ? 0.6f : 1.3f, pHeight + 1f, 0.22f);
            lClimb.isStatic = true;
            if (!string.IsNullOrEmpty(pPoi)) Poi(pBottom + pOut * 0.6f + Vector3.up, pPoi, MapPoi.Kind.Climb);
        }

        public void Chandelier(Vector3 pHook, float pDrop)
        {
            Transform lRoot = new GameObject("Chandelier") { layer = GameLayers.INTERACTABLE }.transform;
            lRoot.SetParent(Systems, false);
            lRoot.position = pHook;
            Transform lCrystal = new GameObject("Crystal").transform;
            lCrystal.SetParent(lRoot, false);
            lCrystal.position = pHook + Vector3.down * 0.75f;
            GameObject lModel = ArchLib.Place("Prop_Chandelier", lCrystal, lCrystal.position - Vector3.up * 0.4f, 0f, ArchLib.Collide.None, ArchLib.Brass, 1.6f);
            if (lModel != null) foreach (Transform t in lModel.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            GameObject lChain = BoxObject("Chain", pHook + Vector3.down * 0.25f, 0f, lRoot, GameLayers.INTERACTABLE, true, (ArchLib.Brass, Vector3.zero, new Vector3(0.05f, 0.5f, 0.05f)));
            lChain.isStatic = false;
            Light lLight = new GameObject("ChandelierLight").AddComponent<Light>();
            lLight.transform.SetParent(lCrystal, false);
            lLight.type = LightType.Point;
            lLight.range = 12f;
            lLight.intensity = 2.2f;
            lLight.color = new Color(1f, 0.85f, 0.62f);
            lLight.shadows = LightShadows.None;
            lRoot.gameObject.AddComponent<Systems.Chandelier>().Setup(lCrystal, pDrop);
        }

        public void Zipline(Vector3 pStart, Vector3 pEnd)
        {
            Transform lRoot = new GameObject("Zipline").transform;
            lRoot.SetParent(Systems, false);
            lRoot.position = pStart;
            Transform lA = new GameObject("Start").transform;
            lA.SetParent(lRoot, false);
            lA.position = pStart + Vector3.up * 2.6f;
            Transform lB = new GameObject("End").transform;
            lB.SetParent(lRoot, false);
            lB.position = pEnd + Vector3.up * 2.6f;
            ArchMesh lMesh = new ArchMesh();
            lMesh.Cylinder(ArchLib.Iron, pStart - lRoot.position, 0.08f, 2.9f, 8);
            lMesh.Cylinder(ArchLib.Iron, pEnd - lRoot.position, 0.08f, 2.9f, 8);
            lMesh.Tube(ArchLib.Iron, lA.localPosition, lB.localPosition, 0.015f, 6, false);
            lMesh.Flush(lRoot, K.Map.sceneName + "_Zipline_" + (_unique++));
            GameObject lGrab = new GameObject("Grab") { layer = GameLayers.INTERACTABLE };
            lGrab.transform.SetParent(lRoot, false);
            lGrab.transform.position = pStart + Vector3.up * 1.6f;
            BoxCollider lBox = lGrab.AddComponent<BoxCollider>();
            lBox.isTrigger = true;
            lBox.size = new Vector3(1.6f, 2f, 1.6f);
            lGrab.AddComponent<Zipline>().Setup(lA, lB, 16f);
            Poi(pStart, "Zipline", MapPoi.Kind.Shortcut);
        }

        public void Chatter(Vector3 pPosition, string pSpeaker, params string[] pLines)
        {
            GameObject lSpot = new GameObject("Chatter_" + pSpeaker);
            lSpot.transform.SetParent(Systems, false);
            lSpot.transform.position = pPosition;
            lSpot.AddComponent<ChatterSpot>().Setup(pSpeaker, 7f, pLines);
        }

        public void Poi(Vector3 pPosition, string pLabel, MapPoi.Kind pKind)
        {
            GameObject lPoi = new GameObject("Poi_" + pLabel);
            lPoi.transform.SetParent(Systems, false);
            lPoi.transform.position = pPosition;
            lPoi.AddComponent<MapPoi>().Setup(pKind, pLabel);
        }

        /// <summary>A security zone box (outdoor areas): centre on the ground, size, yaw.</summary>
        public void Zone(Vector3 pCenter, Vector3 pSize, float pYaw, SecurityTier pTier, AccessLevel pAccess = AccessLevel.None, string pName = "Area")
        {
            GameObject lZone = new GameObject("Zone_" + pName + "_" + pTier + "_" + pAccess);
            lZone.transform.SetParent(Systems, false);
            lZone.transform.SetPositionAndRotation(pCenter, Quaternion.Euler(0, pYaw, 0));
            lZone.AddComponent<SecurityZone>().Setup(pTier, pSize, pAccess);
        }

        /// <summary>A street or garden lamp lit at night (lights named "Lamp" follow the time of day).</summary>
        public void Lamp(Vector3 pGround, float pYaw, Color pColor, float pRange = 14f, float pIntensity = 2.2f, bool pPost = true, float pHeight = 3.8f)
        {
            if (pPost) Model("Prop_StreetLamp", pGround, pYaw, null, ArchLib.Collide.Box);
            Light lLight = new GameObject("Lamp").AddComponent<Light>();
            lLight.transform.SetParent(K.Lights, false);
            lLight.transform.position = pGround + Vector3.up * (pPost ? pHeight : 0f);
            lLight.type = LightType.Point;
            lLight.range = pRange;
            lLight.intensity = pIntensity;
            lLight.color = pColor;
            lLight.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- markers

        /// <summary>An NPC post with what it carries and whether it sees through passes.</summary>
        public NpcSpawnPoint Npc(NpcRole pRole, Vector3 pPosition, float pYaw, int pZone, ItemKind pWeapon, ItemKind pCarries = ItemKind.Fists, bool pEnforcer = false, PatrolRoute pRoute = null)
        {
            GameObject lMarker = new GameObject("Npc_" + pRole);
            lMarker.transform.SetParent(K.Markers, false);
            lMarker.transform.SetPositionAndRotation(pPosition, Quaternion.Euler(0, pYaw, 0));
            NpcSpawnPoint lSpawn = lMarker.AddComponent<NpcSpawnPoint>();
            lSpawn.Setup(pRole, pZone, pRoute, pWeapon);
            lSpawn.Carries = pCarries;
            lSpawn.Enforcer = pEnforcer;
            return lSpawn;
        }

        /// <summary>A patrol pair (or group) on a loop of world points.</summary>
        public void Patrol(int pZone, int pSize, ItemKind pWeapon, float pPause, params Vector3[] pPoints)
        {
            PatrolRoute lRoute = K.Route(pPause, pPoints);
            Vector3 lBack = pPoints.Length > 1 ? (pPoints[0] - pPoints[1]).normalized : Vector3.back;
            lBack.y = 0f;
            Vector3 lSide = Vector3.Cross(Vector3.up, lBack).normalized;
            float lYaw = Quaternion.LookRotation(lBack.sqrMagnitude > 0.001f ? -lBack : Vector3.forward).eulerAngles.y;
            for (int i = 0; i < Mathf.Clamp(pSize, 1, 5); i++)
            {
                Vector3 p = pPoints[0] + lBack * (1.4f * ((i + 1) / 2)) + lSide * (i == 0 ? 0f : i % 2 == 1 ? -0.8f : 0.8f);
                Npc(NpcRole.Patrol, p, lYaw, pZone, i == 0 ? pWeapon : ItemKind.Smg, ItemKind.Fists, false, lRoute);
            }
        }

        /// <summary>A gadget spawn (a pass, a key, a tool, a poison) with a label for the designers.</summary>
        public void Gadget(ItemKind pKind, Vector3 pPosition, string pLabel) => K.Point(MatchPoint.Kind.GadgetSpawn, pPosition, 0f, pLabel, pKind);

        /// <summary>A throwable lying on a surface (rock, bottle, wrench, brick, coin).</summary>
        public void Throwable(Vector3 pPosition, ItemKind pKind = ItemKind.Fists)
        {
            if (pKind == ItemKind.Fists) pKind = Pick(ItemKind.Bottle, ItemKind.Wrench, ItemKind.Rock, ItemKind.Brick, ItemKind.Coin, ItemKind.Bottle);
            K.Gadget(pKind, pPosition);
        }
    }
}
