using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// What lives behind the title: an agent breathing at the edge of a jungle clearing, trees on hills
    /// up to the sky, the camera drifting. The builder hands over the plants and the ground's material
    /// (<c>Goobyr/Build/Title Scene</c>), so they ship with the scene; the menus sit on the left.
    /// </summary>
    public class TitleDiorama : MonoBehaviour
    {
        [SerializeField] private GameObject[] _trees;
        [SerializeField] private GameObject[] _bushes;
        [SerializeField] private GameObject[] _rocks;
        [SerializeField] private Material _ground;

        private const float SIZE = 180f, CELL = 3f;
        private const float WOBBLE_SPEED = 0.2f;

        private Camera _camera;
        private readonly Vector3 _eye = new Vector3(-0.6f, 1.5f, -3.9f);
        private readonly Vector3 _look = new Vector3(-0.15f, 1.5f, 0f);

        public void Setup(GameObject[] pTrees, GameObject[] pBushes, GameObject[] pRocks, Material pGround)
        {
            _trees = pTrees;
            _bushes = pBushes;
            _rocks = pRocks;
            _ground = pGround;
        }

        private void Start()
        {
            _camera = new GameObject("TitleCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.fieldOfView = 34f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 600f;
            _camera.tag = "MainCamera";
            UniversalAdditionalCameraData lData = _camera.GetUniversalAdditionalCameraData();
            lData.renderPostProcessing = true;
            lData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            lData.antialiasingQuality = AntialiasingQuality.High;
            if (FindFirstObjectByType<AudioListener>() == null) _camera.gameObject.AddComponent<AudioListener>();

            BuildGround();
            Scatter();
            BuildAgent();
        }

        /// <summary>The clearing, level around the agent, the hills rising behind it.</summary>
        private static float Height(float pX, float pZ)
        {
            float lFar = Mathf.Clamp01((pZ - 6f) / 90f);
            float lHills = (Mathf.PerlinNoise(pX * 0.018f + 3f, pZ * 0.018f + 7f) * 26f + Mathf.PerlinNoise(pX * 0.06f, pZ * 0.06f) * 3f) * lFar;
            float lSides = Mathf.Clamp01((Mathf.Abs(pX) - 14f) / 40f) * 8f;
            return lHills + lSides * Mathf.Clamp01(pZ / 10f);
        }

        private void BuildGround()
        {
            int lCount = Mathf.RoundToInt(SIZE / CELL) + 1;
            Vector3[] lVertices = new Vector3[lCount * lCount];
            int[] lTriangles = new int[(lCount - 1) * (lCount - 1) * 6];
            for (int z = 0; z < lCount; z++)
                for (int x = 0; x < lCount; x++)
                {
                    float px = -SIZE * 0.5f + x * CELL, pz = -20f + z * CELL;
                    lVertices[z * lCount + x] = new Vector3(px, Height(px, pz), pz);
                }
            int t = 0;
            for (int z = 0; z < lCount - 1; z++)
                for (int x = 0; x < lCount - 1; x++)
                {
                    int a = z * lCount + x, b = a + 1, c = a + lCount, d = c + 1;
                    lTriangles[t++] = a; lTriangles[t++] = c; lTriangles[t++] = b;
                    lTriangles[t++] = b; lTriangles[t++] = c; lTriangles[t++] = d;
                }
            Mesh lMesh = new Mesh { name = "TitleGround", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = lVertices, triangles = lTriangles };
            lMesh.RecalculateNormals();
            lMesh.RecalculateBounds();
            GameObject lGround = new GameObject("Ground");
            lGround.transform.SetParent(transform, false);
            lGround.AddComponent<MeshFilter>().sharedMesh = lMesh;
            MeshRenderer lRenderer = lGround.AddComponent<MeshRenderer>();
            lRenderer.sharedMaterial = _ground;
        }

        private void Scatter()
        {
            System.Random lRandom = new System.Random(11);
            for (int i = 0; i < 170; i++)
            {
                float x = Mathf.Lerp(-85f, 85f, (float)lRandom.NextDouble()), z = Mathf.Lerp(7f, 150f, (float)lRandom.NextDouble());
                if (Mathf.Abs(x) < 3f + z * 0.08f) continue;
                Plant(_trees, x, z, Mathf.Lerp(0.9f, 1.4f, (float)lRandom.NextDouble()), lRandom);
            }
            for (int i = 0; i < 70; i++)
            {
                float x = Mathf.Lerp(-30f, 30f, (float)lRandom.NextDouble()), z = Mathf.Lerp(0.5f, 26f, (float)lRandom.NextDouble());
                if (Mathf.Abs(x - 1.1f) < 2.2f && z < 5f) continue;
                Plant(_bushes, x, z, Mathf.Lerp(0.8f, 1.5f, (float)lRandom.NextDouble()), lRandom);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = Mathf.Lerp(-22f, 22f, (float)lRandom.NextDouble()), z = Mathf.Lerp(3f, 30f, (float)lRandom.NextDouble());
                if (Mathf.Abs(x - 1.1f) < 3f && z < 6f) continue;
                Plant(_rocks, x, z, Mathf.Lerp(0.8f, 2.2f, (float)lRandom.NextDouble()), lRandom);
            }
        }

        private void Plant(GameObject[] pSet, float pX, float pZ, float pScale, System.Random pRandom)
        {
            if (pSet == null || pSet.Length == 0) return;
            GameObject lPrefab = pSet[pRandom.Next(pSet.Length)];
            if (lPrefab == null) return;
            GameObject lPlant = Instantiate(lPrefab, transform);
            lPlant.transform.localPosition = new Vector3(pX, Height(pX, pZ) - 0.05f, pZ);
            lPlant.transform.localRotation = Quaternion.Euler(0f, (float)pRandom.NextDouble() * 360f, 0f);
            lPlant.transform.localScale = Vector3.one * pScale;
            foreach (Collider lCollider in lPlant.GetComponentsInChildren<Collider>()) lCollider.enabled = false;
        }

        /// <summary>The agent, right of the frame: dark suit, a silenced pistol held low, watching the jungle.</summary>
        private void BuildAgent()
        {
            GameObject lAgent = new GameObject("Agent");
            lAgent.transform.SetParent(transform, false);
            lAgent.transform.localPosition = new Vector3(1.05f, 0f, 0.4f);
            lAgent.transform.localRotation = Quaternion.Euler(0f, 205f, 0f);
            HumanBody lBody = lAgent.AddComponent<HumanBody>();
            lBody.Culling = AnimatorCullingMode.AlwaysAnimate;
            lBody.Configure(HumanCast.AGENT, UIFactory.Accent);
            HumanAnimator lAnimator = lAgent.AddComponent<HumanAnimator>();
            lAnimator.Velocity = Vector3.zero;
            lAnimator.Held = HeldKind.Pistol;
            lAnimator.Pose = BodyPose.ArmedIdle;
            lAnimator.AimPitch = -3f;
            lBody.SetHeld(HeldKind.Pistol);
            ItemDefinition lPistol = GameDatabase.Instance != null ? GameDatabase.Find(ItemKind.SilencedPistol) : null;
            if (lPistol != null && lBody.WeaponSocket != null) WeaponModels.Build(lPistol, lBody.WeaponSocket, 0);
        }

        private void Update()
        {
            // A slow breath of the camera.
            float t = Time.unscaledTime * WOBBLE_SPEED;
            Vector3 lDrift = new Vector3(Mathf.Sin(t * 1.05f) * 0.12f, Mathf.Sin(t * 1.65f) * 0.05f, Mathf.Sin(t * 0.85f) * 0.1f);
            _camera.transform.SetPositionAndRotation(_eye + lDrift, Quaternion.LookRotation(_look - (_eye + lDrift)));
        }
    }
}
