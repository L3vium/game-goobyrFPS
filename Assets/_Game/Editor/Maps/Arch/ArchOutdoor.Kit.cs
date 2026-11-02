using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Outdoor set pieces for backstage and event areas: a concert stage on box truss, box trucks,
    /// painted signs, rope queue lines, guard booths with barrier arms, bins, patio heaters, market
    /// goods (crates, sacks, baskets) and cover clusters.
    /// </summary>
    public static partial class ArchOutdoor
    {
        /// <summary>A square box truss (four chords, rungs, zig-zag diagonals) between two points.</summary>
        public static void Truss(ArchMesh pMesh, Vector3 pFrom, Vector3 pTo, float pSize = 0.4f)
        {
            ArchMat lAlu = ArchLib.PlainMat("TrussAlu", new Color(0.78f, 0.79f, 0.8f), 0.6f, 0.8f);
            Vector3 lAxis = pTo - pFrom;
            float lLength = lAxis.magnitude;
            Vector3 lDir = lAxis / lLength;
            Vector3 lSide = Vector3.Cross(lDir, Mathf.Abs(lDir.y) > 0.9f ? Vector3.forward : Vector3.up).normalized, lUp = Vector3.Cross(lSide, lDir).normalized;
            float h = pSize * 0.5f;
            Vector3[] lCorners = { lSide * h + lUp * h, -lSide * h + lUp * h, -lSide * h - lUp * h, lSide * h - lUp * h };
            foreach (Vector3 c in lCorners) pMesh.Tube(lAlu, pFrom + c, pTo + c, 0.025f, 6, false);
            int lBays = Mathf.Max(1, Mathf.RoundToInt(lLength / pSize));
            for (int k = 0; k < lBays; k++)
            {
                Vector3 p0 = pFrom + lDir * (lLength * k / lBays), p1 = pFrom + lDir * (lLength * (k + 1) / lBays);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 c0 = lCorners[i], c1 = lCorners[(i + 1) % 4];
                    pMesh.Tube(lAlu, p0 + c0, p1 + c1, 0.012f, 4, false);
                    pMesh.Tube(lAlu, p0 + c0, p0 + c1, 0.012f, 4, false);
                }
            }
        }

        /// <summary>
        /// A concert stage: a skirted deck on a steel frame, steps, a box-truss roof on four towers, a
        /// pleated velvet backdrop and wings, PA stacks either side, par cans, a mixing desk and coloured
        /// spots that come on at dusk. The audience is on the -z side.
        /// </summary>
        public static void Stage(ArchKit A, Vector3 pGround, float pYaw, float pWidth, float pDepth)
        {
            ArchMat lDeck = ArchLib.Surface("wood_floor_deck", 2f), lSkirt = ArchLib.Tinted("velour_velvet", new Color(0.12f, 0.12f, 0.14f), "skirt", 1f);
            ArchMat lCloth = ArchLib.Tinted("velour_velvet", new Color(0.7f, 0.08f, 0.1f), "stage", 1.5f), lSteel = ArchLib.Surface("metal_plate", 1.5f);
            ArchMat lCabinet = ArchLib.PlainMat("SpeakerCabinet", new Color(0.05f, 0.05f, 0.055f), 0.25f, 0f), lCone = ArchLib.PlainMat("SpeakerGrille", new Color(0.13f, 0.13f, 0.14f), 0.1f, 0.4f);
            ArchMesh lMesh = new ArchMesh();
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            lMesh.Frame = Matrix4x4.TRS(pGround, lRot, Vector3.one);
            float W = pWidth, D = pDepth, H = 1.2f, Ht = 6.4f;
            lMesh.Box(lDeck, new Vector3(0, H - 0.08f, 0), new Vector3(W, 0.16f, D));
            lMesh.Box(lSkirt, new Vector3(0, (H - 0.16f) * 0.5f, -D * 0.5f + 0.03f), new Vector3(W, H - 0.16f, 0.04f));
            foreach (float x in new[] { -W * 0.5f + 0.03f, W * 0.5f - 0.03f }) lMesh.Box(lSkirt, new Vector3(x, (H - 0.16f) * 0.5f, 0), new Vector3(0.04f, H - 0.16f, D));
            for (float x = -W * 0.5f + 0.4f; x < W * 0.5f; x += 2f)
                for (float z = -D * 0.5f + 0.4f; z < D * 0.5f; z += 2f) lMesh.Box(lSteel, new Vector3(x, (H - 0.16f) * 0.5f, z), new Vector3(0.08f, H - 0.16f, 0.08f), false);
            // A pleated backdrop and the two wings.
            int lPleat = 0;
            for (float x = -W * 0.5f + 0.3f; x < W * 0.5f - 0.2f; x += 0.6f, lPleat++)
                lMesh.Box(lCloth, new Vector3(x, H + (Ht - H - 0.4f) * 0.5f, D * 0.5f - 0.25f + (lPleat % 2 == 0 ? 0.07f : -0.07f)), new Vector3(0.64f, Ht - H - 0.4f, 0.05f), false);
            lMesh.Box(ArchLib.Invisible, new Vector3(0, H + 2f, D * 0.5f - 0.25f), new Vector3(W, 4f, 0.1f), true);
            foreach (float s in new[] { -1f, 1f })
                lMesh.Box(lCloth, new Vector3(s * (W * 0.5f - 0.4f), H + (Ht - H - 0.4f) * 0.5f, D * 0.5f - 1.6f), new Vector3(0.05f, Ht - H - 0.4f, 1.8f), false);
            // Truss towers at the corners and the roof frame.
            Vector3[] lFeet = { new Vector3(-W * 0.5f - 0.4f, 0, -D * 0.5f + 0.2f), new Vector3(W * 0.5f + 0.4f, 0, -D * 0.5f + 0.2f), new Vector3(-W * 0.5f - 0.4f, 0, D * 0.5f - 0.2f), new Vector3(W * 0.5f + 0.4f, 0, D * 0.5f - 0.2f) };
            foreach (Vector3 f in lFeet) { Truss(lMesh, f, f + Vector3.up * Ht, 0.4f); lMesh.Box(lSteel, f + Vector3.up * 0.05f, new Vector3(0.8f, 0.1f, 0.8f)); }
            Truss(lMesh, lFeet[0] + Vector3.up * Ht, lFeet[1] + Vector3.up * Ht);
            Truss(lMesh, lFeet[2] + Vector3.up * Ht, lFeet[3] + Vector3.up * Ht);
            Truss(lMesh, lFeet[0] + Vector3.up * Ht, lFeet[2] + Vector3.up * Ht);
            Truss(lMesh, lFeet[1] + Vector3.up * Ht, lFeet[3] + Vector3.up * Ht);
            // PA stacks on the ground either side (two subs, three tops), monitor wedges on the deck.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(s * (W * 0.5f + 1.6f), 0, -D * 0.5f + 0.8f);
                for (int k = 0; k < 2; k++) lMesh.Box(lCabinet, c + new Vector3(0, 0.4f + k * 0.8f, 0), new Vector3(1.2f, 0.78f, 0.9f));
                for (int k = 0; k < 3; k++) lMesh.Box(lCabinet, c + new Vector3(0, 1.85f + k * 0.62f, 0), new Vector3(0.9f, 0.6f, 0.7f));
                for (int k = 0; k < 2; k++) lMesh.Box(lCone, c + new Vector3(0, 0.4f + k * 0.8f, -0.46f), new Vector3(0.9f, 0.6f, 0.02f), false);
                for (int k = 0; k < 3; k++) lMesh.Box(lCone, c + new Vector3(0, 1.85f + k * 0.62f, -0.36f), new Vector3(0.7f, 0.45f, 0.02f), false);
                lMesh.Box(lCabinet, new Vector3(s * W * 0.25f, H + 0.18f, -D * 0.5f + 0.6f), new Vector3(0.7f, 0.36f, 0.5f), Quaternion.Euler(-25f, 0, 0));
            }
            // Par cans on the front truss, a microphone stand, the mixing desk out front.
            for (int k = 0; k < 6; k++)
            {
                Vector3 p = new Vector3(-W * 0.4f + k * W * 0.16f, Ht - 0.45f, -D * 0.5f + 0.2f);
                lMesh.Tube(lCabinet, p, p + new Vector3(0, -0.2f, 0.28f), 0.11f, 10, false);
            }
            lMesh.Box(lSteel, new Vector3(0, H + 0.75f, -D * 0.5f + 1.4f), new Vector3(0.04f, 1.5f, 0.04f), false);
            lMesh.Box(lCabinet, new Vector3(0, 0.55f, -D * 0.5f - 9f), new Vector3(2.4f, 1.1f, 1.2f));
            // Steps up the side.
            for (int i = 0; i < 6; i++) lMesh.Box(lSteel, new Vector3(W * 0.5f + 0.6f, 0.1f + i * 0.2f, -D * 0.5f + 0.6f + i * 0.3f), new Vector3(1.0f, 0.2f + i * 0.4f, 0.32f), false);
            GameObject lRoot = Flush(A, lMesh, "Stage", A.Props);
            GameObject lRamp = new GameObject("StageSteps");
            lRamp.transform.SetParent(lRoot.transform, false);
            Vector3 a = pGround + lRot * new Vector3(W * 0.5f + 0.6f, 0f, -D * 0.5f + 0.3f), b = pGround + lRot * new Vector3(W * 0.5f + 0.6f, H + 0.05f, -D * 0.5f + 2.4f);
            Quaternion lSlope = Quaternion.LookRotation((b - a).normalized);
            lRamp.transform.SetPositionAndRotation((a + b) * 0.5f + lSlope * Vector3.down * 0.1f, lSlope);
            lRamp.AddComponent<BoxCollider>().size = new Vector3(1f, 0.2f, Vector3.Distance(a, b) + 0.3f);
            // Coloured spots on the deck ("Lamp" lights follow the time of day).
            Color[] lGels = { new Color(1f, 0.3f, 0.25f), new Color(0.35f, 0.5f, 1f), new Color(1f, 0.75f, 0.3f), new Color(0.8f, 0.3f, 1f) };
            for (int k = 0; k < 4; k++)
            {
                Light lSpot = new GameObject("Lamp").AddComponent<Light>();
                lSpot.transform.SetParent(A.K.Lights, false);
                lSpot.transform.position = pGround + lRot * new Vector3(-W * 0.35f + k * W * 0.23f, Ht - 0.6f, -D * 0.5f + 0.3f);
                lSpot.transform.rotation = Quaternion.LookRotation(pGround + lRot * new Vector3(0, H, D * 0.15f) - lSpot.transform.position);
                lSpot.type = LightType.Spot;
                lSpot.spotAngle = 55f;
                lSpot.range = 14f;
                lSpot.intensity = 6f;
                lSpot.color = lGels[k];
                lSpot.shadows = LightShadows.None;
            }
        }

        /// <summary>A box truck (cab, cargo box, wheels) facing +z: cover and yard dressing.</summary>
        public static GameObject Truck(ArchKit A, Vector3 pGround, float pYaw, Color pCab, string pName = "Truck")
        {
            ArchMat lCab = ArchLib.Tinted("painted_metal_shutter", pCab, "truck_" + ColorUtility.ToHtmlStringRGB(pCab), 2f);
            ArchMat lBox = ArchLib.Tinted("box_profile_metal_sheet", new Color(0.92f, 0.92f, 0.9f), "truckbox", 2f);
            ArchMat lTyre = ArchLib.PlainMat("Tyre", new Color(0.04f, 0.04f, 0.04f), 0.15f, 0f), lChrome = ArchLib.PlainMat("Chrome", new Color(0.8f, 0.8f, 0.82f), 0.85f, 1f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Box(lCab, new Vector3(0, 1.35f, 2.65f), new Vector3(2.3f, 1.6f, 1.9f));
            lMesh.Box(ArchLib.Glass, new Vector3(0, 1.75f, 3.61f), new Vector3(2.0f, 0.7f, 0.04f), false);
            lMesh.Box(lChrome, new Vector3(0, 0.75f, 3.65f), new Vector3(2.3f, 0.25f, 0.1f), false);
            lMesh.Box(lBox, new Vector3(0, 1.9f, -1.0f), new Vector3(2.45f, 2.6f, 5.2f));
            lMesh.Box(ArchLib.Iron, new Vector3(0, 0.55f, 0.2f), new Vector3(1.4f, 0.3f, 7f), false);
            foreach (float z in new[] { 2.6f, -0.6f, -2.6f })
                foreach (float x in new[] { -1.05f, 1.05f })
                    lMesh.Tube(lTyre, new Vector3(x - 0.15f, 0.48f, z), new Vector3(x + 0.15f, 0.48f, z), 0.48f, 14);
            GameObject lRoot = new GameObject(pName);
            lRoot.transform.SetParent(A.Props, false);
            lRoot.transform.SetPositionAndRotation(pGround, Quaternion.Euler(0, pYaw, 0));
            lMesh.Flush(lRoot.transform, A.K.Map.sceneName + "_Truck_" + (_count++));
            BoxCollider lCollider = lRoot.AddComponent<BoxCollider>();
            lCollider.center = new Vector3(0, 1.6f, 0.3f);
            lCollider.size = new Vector3(2.45f, 3.2f, 7.4f);
            lRoot.isStatic = true;
            A.Cover(lRoot);
            return lRoot;
        }

        /// <summary>A painted sign on a post (or flat on a wall): "Staff only", "Solo personal", "Privé".</summary>
        public static void Sign(ArchKit A, Vector3 pAt, float pYaw, string pText, bool pPost = true, Color? pColor = null)
        {
            Color lBack = pColor ?? new Color(0.75f, 0.1f, 0.08f);
            GameObject lRoot = new GameObject("Sign_" + pText);
            lRoot.transform.SetParent(A.Props, false);
            lRoot.transform.SetPositionAndRotation(pAt, Quaternion.Euler(0, pYaw, 0));
            ArchMesh lMesh = new ArchMesh();
            float y = pPost ? 1.7f : 0f;
            if (pPost) lMesh.Box(ArchLib.Iron, new Vector3(0, 0.85f, 0.03f), new Vector3(0.06f, 1.7f, 0.06f));
            lMesh.Box(ArchLib.Tinted("painted_metal_shutter", lBack, "sign_" + ColorUtility.ToHtmlStringRGB(lBack), 1f), new Vector3(0, y, 0), new Vector3(0.95f, 0.34f, 0.03f), !pPost);
            lMesh.Flush(lRoot.transform, A.K.Map.sceneName + "_Sign_" + (_count++));
            // White lettering on both faces.
            foreach (float lFace in new[] { -1f, 1f })
            {
                GameObject lText = new GameObject("Text");
                lText.transform.SetParent(lRoot.transform, false);
                lText.transform.localPosition = new Vector3(0, y, lFace * 0.02f);
                lText.transform.localRotation = Quaternion.Euler(0, lFace < 0 ? 0f : 180f, 0);
                TextMesh lLetters = lText.AddComponent<TextMesh>();
                lLetters.text = pText.ToUpperInvariant();
                lLetters.anchor = TextAnchor.MiddleCenter;
                lLetters.alignment = TextAlignment.Center;
                lLetters.characterSize = 0.04f;
                lLetters.fontSize = 48;
                lLetters.fontStyle = FontStyle.Bold;
                lLetters.color = Color.white;
                lText.transform.localScale = Vector3.one * Mathf.Min(1f, 12f / Mathf.Max(6, pText.Length));
                if (!pPost) break;
            }
        }

        /// <summary>A queue lane of brass posts and sagging red ropes through points (petitioners, frisk lines).</summary>
        public static void RopeLine(ArchKit A, params Vector3[] pPoints)
        {
            ArchMesh lMesh = new ArchMesh();
            ArchMat lRope = ArchLib.Tinted("velour_velvet", new Color(0.65f, 0.05f, 0.08f), "rope", 0.5f);
            for (int i = 0; i < pPoints.Length; i++)
            {
                lMesh.Cylinder(ArchLib.Brass, pPoints[i], 0.17f, 0.04f, 12, false);
                lMesh.Cylinder(ArchLib.Brass, pPoints[i], 0.03f, 0.95f, 8, true);
                lMesh.Cylinder(ArchLib.Brass, pPoints[i] + Vector3.up * 0.95f, 0.05f, 0.06f, 8, false);
                if (i == 0) continue;
                Vector3 a = pPoints[i - 1] + Vector3.up * 0.88f, b = pPoints[i] + Vector3.up * 0.88f;
                for (int k = 0; k < 6; k++)
                {
                    float t0 = k / 6f, t1 = (k + 1) / 6f;
                    Vector3 p0 = Vector3.Lerp(a, b, t0) - Vector3.up * Mathf.Sin(t0 * Mathf.PI) * 0.12f, p1 = Vector3.Lerp(a, b, t1) - Vector3.up * Mathf.Sin(t1 * Mathf.PI) * 0.12f;
                    lMesh.Tube(lRope, p0, p1, 0.022f, 6, false);
                }
                if (Vector3.Distance(a, b) > 0.1f) lMesh.Box(ArchLib.Invisible, (a + b) * 0.5f - Vector3.up * 0.4f, new Vector3(0.06f, 0.9f, Vector3.Distance(a, b)), Quaternion.LookRotation(b - a), true);
            }
            Flush(A, lMesh, "RopeLine", A.Props);
        }

        /// <summary>A guard booth at a tier boundary: a glazed hut, a lamp, a striped barrier arm. Returns the guard's spot.</summary>
        public static Vector3 GuardBooth(ArchKit A, Vector3 pGround, float pYaw, bool pBarrier = true, float pBarrierLength = 4f)
        {
            ArchMat lWall = ArchLib.Tinted("painted_plaster_wall", new Color(0.93f, 0.92f, 0.88f), "booth", 2f), lRoof = ArchLib.Surface("metal_plate", 1.5f);
            ArchMat lStripe = ArchLib.Tinted("painted_metal_shutter", new Color(0.9f, 0.15f, 0.1f), "hazard", 1f);
            ArchMesh lMesh = new ArchMesh();
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            lMesh.Frame = Matrix4x4.TRS(pGround, lRot, Vector3.one);
            lMesh.Box(lWall, new Vector3(0, 0.5f, 0), new Vector3(2.2f, 1f, 2.2f));
            foreach (Vector3 c in new[] { new Vector3(-1.05f, 0, -1.05f), new Vector3(1.05f, 0, -1.05f), new Vector3(-1.05f, 0, 1.05f), new Vector3(1.05f, 0, 1.05f) })
                lMesh.Box(lWall, c + Vector3.up * 1.6f, new Vector3(0.12f, 1.2f, 0.12f));
            lMesh.Box(ArchLib.Glass, new Vector3(0, 1.6f, -1.08f), new Vector3(2f, 1.2f, 0.04f));
            lMesh.Box(ArchLib.Glass, new Vector3(-1.08f, 1.6f, 0), new Vector3(0.04f, 1.2f, 2f));
            lMesh.Box(ArchLib.Glass, new Vector3(1.08f, 1.6f, 0), new Vector3(0.04f, 1.2f, 2f));
            lMesh.Box(lWall, new Vector3(0, 1.6f, 1.08f), new Vector3(2.2f, 1.2f, 0.06f));
            lMesh.Box(lRoof, new Vector3(0, 2.3f, 0), new Vector3(2.8f, 0.2f, 2.8f));
            lMesh.Box(ArchLib.LampGlow, new Vector3(0, 2.18f, 0), new Vector3(0.4f, 0.04f, 0.4f), false);
            if (pBarrier)
            {
                lMesh.Box(lWall, new Vector3(1.6f, 0.5f, -1.5f), new Vector3(0.4f, 1f, 0.4f));
                lMesh.Box(lStripe, new Vector3(1.6f + pBarrierLength * 0.5f, 1.0f, -1.5f), new Vector3(pBarrierLength, 0.1f, 0.1f), false);
            }
            Flush(A, lMesh, "GuardBooth", A.Props);
            return pGround + lRot * new Vector3(0, 0, -1.7f);
        }

        /// <summary>A row of wheelie bins against a wall (kitchen and delivery yards), along +x.</summary>
        public static void Bins(ArchKit A, Vector3 pGround, float pYaw, int pCount = 3)
        {
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pGround, Quaternion.Euler(0, pYaw, 0), Vector3.one);
            Color[] lColours = { new Color(0.15f, 0.35f, 0.18f), new Color(0.2f, 0.25f, 0.5f), new Color(0.3f, 0.3f, 0.3f) };
            for (int i = 0; i < pCount; i++)
            {
                ArchMat lBin = ArchLib.Tinted("rubber_tiles", lColours[i % 3], "bin" + (i % 3), 1f);
                Vector3 c = new Vector3(i * 0.8f, 0, 0);
                lMesh.Box(lBin, c + new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1f, 0.7f));
                lMesh.Box(lBin, c + new Vector3(0, 1.03f, -0.02f), new Vector3(0.64f, 0.06f, 0.76f), false);
            }
            Flush(A, lMesh, "Bins", A.Props);
        }

        /// <summary>A gas patio heater (a mushroom on a pole), glowing at night.</summary>
        public static void PatioHeater(ArchKit A, Vector3 pGround)
        {
            ArchMesh lMesh = new ArchMesh();
            ArchMat lSteel = ArchLib.PlainMat("Steel", new Color(0.7f, 0.7f, 0.72f), 0.75f, 0.9f);
            lMesh.Cylinder(lSteel, pGround, 0.25f, 0.7f, 12);
            lMesh.Cylinder(lSteel, pGround + Vector3.up * 0.7f, 0.05f, 1.5f, 8, false);
            lMesh.Cylinder(ArchLib.LampGlow, pGround + Vector3.up * 2.0f, 0.1f, 0.25f, 8, false);
            lMesh.Dome(lSteel, pGround + Vector3.up * 2.25f, 0.45f, 14, 3, Quaternion.identity, false);
            Flush(A, lMesh, "PatioHeater", A.Props);
            A.Lamp(pGround + Vector3.up * 2.1f, 0f, new Color(1f, 0.55f, 0.25f), 5f, 1.4f, false);
        }

        /// <summary>Market goods on a counter top (local to a stall): real crates, sacks and woven baskets with produce.</summary>
        private static void Goods(ArchKit A, ArchMesh pMesh, Vector3 pTop, float pYaw, Vector3 pWorld, int pSeed)
        {
            System.Random r = new System.Random(pSeed);
            ArchMat lSack = ArchLib.Surface("hessian_230", 0.6f), lWicker = ArchLib.Surface("rough_wood", 0.4f);
            ArchMat[] lProduce =
            {
                ArchLib.Tinted("brown_mud_leaves_01", new Color(1.2f, 0.55f, 0.2f), "produce_orange", 0.25f),
                ArchLib.Tinted("brown_mud_leaves_01", new Color(1.1f, 0.25f, 0.15f), "produce_red", 0.25f),
                ArchLib.Tinted("brown_mud_leaves_01", new Color(0.5f, 0.85f, 0.3f), "produce_green", 0.25f),
                ArchLib.Tinted("brown_mud_leaves_01", new Color(0.55f, 0.35f, 0.2f), "produce_dates", 0.25f)
            };
            // Two baskets and an open sack on the counter.
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = pTop + new Vector3(-0.8f + i * 0.8f, 0f, 0.05f);
                if (i == 1)
                {
                    pMesh.Cylinder(lSack, c, 0.3f, 0.32f, 12, false);
                    pMesh.Dome(lProduce[3], c + Vector3.up * 0.3f, 0.28f, 10, 2, Quaternion.identity, false);
                    continue;
                }
                pMesh.Cylinder(lWicker, c, 0.28f, 0.16f, 14, false);
                pMesh.Dome(lProduce[r.Next(3)], c + Vector3.up * 0.12f, 0.26f, 10, 2, Quaternion.identity, false);
            }
            // Wooden crates and grain sacks stacked in front of the stall.
            Quaternion lRot = Quaternion.Euler(0, pYaw, 0);
            for (int i = 0; i < 2; i++)
                A.Model("Prop_Crate_Wood", pWorld + lRot * new Vector3(-0.8f + i * 1.6f, 0f, -0.55f), pYaw + (float)r.NextDouble() * 10f, A.Props, ArchLib.Collide.Box, 0.55f);
            for (int i = 0; i < 2; i++)
                pMesh.Box(lSack, pTop + new Vector3(1.6f + i * 0.05f, -0.75f + i * 0.42f, 0.4f), new Vector3(0.55f, 0.42f, 0.38f), Quaternion.Euler(0, 15f * i, 4f), false);
        }

        /// <summary>
        /// A market stall: a plank counter on trestles, a cloth awning on poles, baskets, sacks and
        /// crates of produce, a hanging lantern.
        /// </summary>
        public static void Stall(ArchKit A, Vector3 pGround, float pYaw, Color pAwning)
        {
            ArchMat lWood = ArchLib.Surface("rough_wood", 1.2f), lCloth = ArchLib.Tinted("rough_linen", pAwning, "awning_" + ColorUtility.ToHtmlStringRGB(pAwning), 1f);
            ArchMesh lMesh = new ArchMesh();
            lMesh.Frame = Matrix4x4.TRS(pGround, Quaternion.Euler(0, pYaw, 0), Vector3.one);
            lMesh.Box(lWood, new Vector3(0, 0.45f, 0.4f), new Vector3(2.4f, 0.9f, 0.8f));
            lMesh.Box(lWood, new Vector3(0, 0.92f, 0.45f), new Vector3(2.5f, 0.05f, 0.95f));
            foreach (float x in new[] { -1.2f, 1.2f })
                foreach (float z in new[] { 0f, 1.6f })
                    lMesh.Box(lWood, new Vector3(x, 1.2f, z), new Vector3(0.08f, 2.4f, 0.08f), false);
            lMesh.Quad(lCloth, new Vector3(1.4f, 2.5f, -0.3f), new Vector3(1.4f, 2.35f, 1.8f), new Vector3(-1.4f, 2.35f, 1.8f), new Vector3(-1.4f, 2.5f, -0.3f), false);
            lMesh.Quad(lCloth, new Vector3(-1.4f, 2.5f, -0.3f), new Vector3(-1.4f, 2.35f, 1.8f), new Vector3(1.4f, 2.35f, 1.8f), new Vector3(1.4f, 2.5f, -0.3f), false);
            Goods(A, lMesh, new Vector3(0, 0.945f, 0.45f), pYaw, pGround, (int)(pGround.x * 7f + pGround.z * 13f));
            lMesh.Box(ArchLib.LampGlow, new Vector3(0, 2.15f, 0.6f), new Vector3(0.18f, 0.25f, 0.18f), false);
            Flush(A, lMesh, "Stall", A.Props);
            A.Lamp(pGround + Quaternion.Euler(0, pYaw, 0) * new Vector3(0, 2.1f, 0.6f), 0f, new Color(1f, 0.72f, 0.4f), 6f, 1.3f, false);
        }

        /// <summary>
        /// Breaks up long sightlines across open ground: clusters of crates, sandbags, planters, barrels,
        /// vehicles and hedges on a grid over an area, wherever the ground is free and flat.
        /// </summary>
        public static int CoverClusters(ArchKit A, Rect pArea, float pSpacing, string pKit, int pSeed)
        {
            System.Random r = new System.Random(pSeed);
            Physics.SyncTransforms();
            int lPlaced = 0;
            for (float x = pArea.xMin + pSpacing * 0.5f; x < pArea.xMax; x += pSpacing)
                for (float z = pArea.yMin + pSpacing * 0.5f; z < pArea.yMax; z += pSpacing)
                {
                    Vector2 p = new Vector2(x + ((float)r.NextDouble() - 0.5f) * pSpacing * 0.5f, z + ((float)r.NextDouble() - 0.5f) * pSpacing * 0.5f);
                    if (!Physics.Raycast(new Vector3(p.x, 400f, p.y), Vector3.down, out RaycastHit lHit, 800f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (lHit.normal.y < 0.94f || !(lHit.collider is TerrainCollider)) continue;
                    Vector3 g = lHit.point;
                    if (Physics.CheckBox(g + Vector3.up * 1.3f, new Vector3(2.6f, 1.1f, 2.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    float lYaw = (float)r.NextDouble() * 360f;
                    int lKind = r.Next(4);
                    switch (pKit)
                    {
                        case "military":
                            if (lKind == 0) A.CrateStack(g, lYaw, 4, true);
                            else if (lKind == 1) for (int i = 0; i < 3; i++) A.Model("Prop_Sandbags", g + Quaternion.Euler(0, lYaw, 0) * new Vector3(i * 1.4f - 1.4f, 0, 0), lYaw);
                            else if (lKind == 2) A.Model("Prop_Barrier_Concrete", g, lYaw);
                            else { A.Model("Prop_Crate_Military", g, lYaw); A.Explosive(g + Quaternion.Euler(0, lYaw, 0) * new Vector3(1.4f, 0, 0), lYaw); }
                            break;
                        case "market":
                            if (lKind == 0) A.CrateStack(g, lYaw, 3);
                            else if (lKind == 1) Planter(A, g, lYaw);
                            else if (lKind == 2) A.Sacks(g, lYaw);
                            else A.Model(r.Next(2) == 0 ? "Prop_Car_Sedan" : "Prop_Car_Pickup", g, lYaw);
                            break;
                        default:
                            if (lKind == 0) Planter(A, g, lYaw);
                            else if (lKind == 1) A.CrateStack(g, lYaw, 3);
                            else if (lKind == 2) Hedge(A, g, lYaw);
                            else A.Model(r.Next(2) == 0 ? "Prop_Car_Sedan" : "Prop_Car_Pickup", g, lYaw);
                            break;
                    }
                    lPlaced++;
                }
            return lPlaced;
        }

        /// <summary>A stone planter box with a shrub (waist-high cover).</summary>
        public static void Planter(ArchKit A, Vector3 pGround, float pYaw)
        {
            A.BoxObject("Planter", pGround, pYaw, A.Props, GameLayers.DEFAULT, true,
                (ArchLib.Surface("large_sandstone_blocks", 1.5f), new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.9f, 1f)),
                (ArchLib.Surface("forest_ground_04", 1f), new Vector3(0, 0.91f, 0), new Vector3(2f, 0.02f, 0.8f)));
            ArchLib.Place("Furn_PottedPlant", A.Props, pGround + Vector3.up * 0.9f, pYaw, ArchLib.Collide.None, null, 1.4f);
        }

        /// <summary>A clipped hedge block (1.3 m, cover), leaves over a box.</summary>
        public static void Hedge(ArchKit A, Vector3 pGround, float pYaw)
        {
            A.BoxObject("Hedge", pGround, pYaw, A.Props, GameLayers.DEFAULT, true,
                (ArchLib.Surface("forest_leaves_02", 0.8f), new Vector3(0, 0.65f, 0), new Vector3(3f, 1.3f, 0.9f)));
        }
    }
}
