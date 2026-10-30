using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The title screen's backdrop: a little floating island with pines and a few Goobs in their
    /// contract outfits wobbling about, under a slowly orbiting camera.
    /// </summary>
    public class TitleDiorama : MonoBehaviour
    {
        private Camera _camera;
        private Transform[] _goobs;
        private Vector3[] _targets;
        private float _orbit;

        private void Start()
        {
            _camera = new GameObject("TitleCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.fieldOfView = 45f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            UniversalAdditionalCameraData lData = _camera.GetUniversalAdditionalCameraData();
            lData.renderPostProcessing = true;
            _camera.gameObject.AddComponent<AudioListener>();

            Mesh lIsland = LowPolyMesh.Cached("title.island", () => LowPolyMesh.Cylinder(12, 1.15f));
            GameArt.Part(transform, "Island", lIsland, new Vector3(0, -1.5f, 0), new Vector3(22, 3, 22), new Color(0.55f, 0.75f, 0.42f));
            GameArt.Part(transform, "Rock", LowPolyMesh.Cached("title.cone", () => LowPolyMesh.Cone(10)), new Vector3(0, -3f, 0), new Vector3(20, -9, 20), new Color(0.6f, 0.55f, 0.5f));
            System.Random lRandom = new System.Random(7);
            Mesh lPine = LowPolyMesh.Cached("title.pine", () => LowPolyMesh.Cone(7));
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f * Mathf.Deg2Rad + (float)lRandom.NextDouble();
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (8f + (float)lRandom.NextDouble() * 2.5f);
                for (int t = 0; t < 3; t++) GameArt.Part(transform, "Pine", lPine, p + Vector3.up * (0.6f + t * 1.1f), new Vector3(2.6f - t * 0.6f, 2f, 2.6f - t * 0.6f), GameArt.Vary(new Color(0.25f, 0.5f, 0.38f), 0.1f, lRandom));
            }
            GameArt.Part(transform, "Crate", LowPolyMesh.Cached("w.box", () => LowPolyMesh.Box()), new Vector3(2, 0.5f, 1), Vector3.one, new Color(0.72f, 0.52f, 0.32f));
            GameArt.Part(transform, "Barrel", LowPolyMesh.Cached("w.cyl8", () => LowPolyMesh.Cylinder(8)), new Vector3(3.2f, 0.55f, 0.4f), new Vector3(0.7f, 1.1f, 0.7f), new Color(0.9f, 0.25f, 0.2f));

            HatStyle[] lHats = { HatStyle.Fedora, HatStyle.Beanie, HatStyle.Cap, HatStyle.TopHat, HatStyle.Cowboy, HatStyle.Beret };
            _goobs = new Transform[6];
            _targets = new Vector3[6];
            for (int i = 0; i < _goobs.Length; i++)
            {
                GameObject lGoob = new GameObject("Goob" + i);
                lGoob.transform.SetParent(transform, false);
                lGoob.transform.position = new Vector3(Mathf.Cos(i) * 4f, 0, Mathf.Sin(i) * 4f);
                GoobBody lBody = lGoob.AddComponent<GoobBody>();
                lBody.Configure(Net.LobbyPlayer.TeamColor(i), new Color(1f, 0.83f, 0.7f), lHats[i], Net.LobbyPlayer.TeamColor(i) * 0.5f, 1f);
                lGoob.AddComponent<GoobAnimator>().HoldingWeapon = false;
                _goobs[i] = lGoob.transform;
                _targets[i] = lGoob.transform.position;
            }

            Light lSun = new GameObject("Sun").AddComponent<Light>();
            lSun.transform.SetParent(transform, false);
            lSun.type = LightType.Directional;
            lSun.transform.rotation = Quaternion.Euler(40, -30, 0);
            lSun.intensity = 1.3f;
            lSun.shadows = LightShadows.Soft;
            lSun.color = new Color(1f, 0.93f, 0.82f);
        }

        private void Update()
        {
            _orbit += Time.deltaTime * 6f;
            Vector3 lFrom = Quaternion.Euler(0, _orbit, 0) * new Vector3(0, 9, -22);
            _camera.transform.SetPositionAndRotation(lFrom, Quaternion.LookRotation(new Vector3(0, 1, 0) - lFrom));

            for (int i = 0; i < _goobs.Length; i++)
            {
                Transform lGoob = _goobs[i];
                Vector3 lTo = _targets[i] - lGoob.position;
                lTo.y = 0;
                if (lTo.magnitude < 0.3f) _targets[i] = new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
                else
                {
                    lGoob.position += lTo.normalized * Time.deltaTime * 1.6f;
                    lGoob.rotation = Quaternion.RotateTowards(lGoob.rotation, Quaternion.LookRotation(lTo), 200f * Time.deltaTime);
                }
            }
        }
    }
}
