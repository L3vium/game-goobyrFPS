using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.UI
{
    /// <summary>
    /// The title screen's key art: one agent, close, posed with a silenced pistol against a stone
    /// facade at golden hour - a rim of warm light, the background soft in the depth of field, the
    /// camera breathing. The menus sit over the left of the frame.
    /// </summary>
    public class TitleDiorama : MonoBehaviour
    {
        private Camera _camera;
        private Vector3 _eye = new Vector3(-0.6f, 1.5f, -4.4f);
        private Vector3 _look = new Vector3(-1.15f, 1.38f, 0f);

        private void Start()
        {
            _camera = new GameObject("TitleCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.1f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.62f, 0.5f, 0.42f);
            UniversalAdditionalCameraData lData = _camera.GetUniversalAdditionalCameraData();
            lData.renderPostProcessing = true;
            lData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            _camera.gameObject.AddComponent<AudioListener>();

            BuildSet();
            BuildAgent();
            BuildLight();
            BuildGrade();
        }

        /// <summary>A two-storey stone facade behind, a pavement, a lamp: the kit, out of focus.</summary>
        private void BuildSet()
        {
            string[] lRow = { "Column", "WallWindowTall", "WallWindowTall", "Column", "WallDoorDouble", "Column", "WallWindowTall", "Column" };
            Color lStone = new Color(0.86f, 0.8f, 0.7f);
            for (int f = 0; f < 2; f++)
                for (int i = 0; i < lRow.Length; i++)
                {
                    string lPiece = f == 1 && lRow[i] == "WallDoorDouble" ? "WallWindowBalcony" : lRow[i];
                    Piece("Classic", lPiece, new Vector3(-6f + i * 3f, f * 3.4f, 4.2f), lStone);
                }
            for (int i = 0; i < lRow.Length; i++) Piece("Classic", "Cornice", new Vector3(-6f + i * 3f, 6.8f, 4.2f), lStone * 0.95f);
            GameArt.Part(transform, "Pavement", LowPolyMesh.Cached("w.box", () => LowPolyMesh.Box()), new Vector3(0, -0.05f, 2f), new Vector3(30f, 0.1f, 12f), new Color(0.5f, 0.48f, 0.46f));
            GameArt.Part(transform, "Kerb", LowPolyMesh.Cached("w.box", () => LowPolyMesh.Box()), new Vector3(0, 0.05f, -1.4f), new Vector3(30f, 0.15f, 0.3f), new Color(0.66f, 0.64f, 0.6f));
            GameObject lLamp = Piece("Props", "LampPostOrnate", new Vector3(-2.6f, 0, 2.2f), Color.white);
            if (lLamp == null) Piece("Props", "StreetLamp", new Vector3(-2.6f, 0, 2.2f), Color.white);
            Light lBulb = new GameObject("LampLight").AddComponent<Light>();
            lBulb.transform.SetParent(transform, false);
            lBulb.transform.localPosition = new Vector3(-2.6f, 3.9f, 2.2f);
            lBulb.type = LightType.Point;
            lBulb.range = 6f;
            lBulb.intensity = 2.5f;
            lBulb.color = new Color(1f, 0.72f, 0.45f);
        }

        private GameObject Piece(string pStyle, string pName, Vector3 pPosition, Color pTint)
        {
            GameObject lPrefab = Resources.Load<GameObject>("Kit/" + pStyle + "/" + pName);
            MeshFilter lFilter = lPrefab != null ? lPrefab.GetComponentInChildren<MeshFilter>() : null;
            if (lFilter == null) return null;
            GameObject lPart = GameArt.Part(transform, pName, lFilter.sharedMesh, pPosition, Vector3.one, pTint);
            lPart.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            return lPart;
        }

        /// <summary>The agent: black suit, red tie, the silenced pistol held low in both hands, breathing.</summary>
        private void BuildAgent()
        {
            GameObject lAgent = new GameObject("Agent");
            lAgent.transform.SetParent(transform, false);
            lAgent.transform.localPosition = new Vector3(0.55f, 0, 0);
            lAgent.transform.localRotation = Quaternion.Euler(0, 205f, 0);
            HumanBody lBody = lAgent.AddComponent<HumanBody>();
            lBody.Culling = AnimatorCullingMode.AlwaysAnimate;
            lBody.Configure(HumanCast.AGENT, Color.clear);
            HumanAnimator lAnimator = lAgent.AddComponent<HumanAnimator>();
            lAnimator.Velocity = Vector3.zero;
            lAnimator.Held = HeldKind.Pistol;
            lAnimator.Pose = BodyPose.ArmedIdle;
            lAnimator.AimPitch = 12f;
            lBody.SetHeld(HeldKind.Pistol);
            ItemDefinition lPistol = GameDatabase.Instance != null ? GameDatabase.Find(ItemKind.SilencedPistol) : null;
            if (lPistol != null && lBody.WeaponSocket != null) WeaponModels.Build(lPistol, lBody.WeaponSocket, 0);
        }

        /// <summary>Low golden sun from behind (the rim), a cool fill from the front.</summary>
        private void BuildLight()
        {
            Light lSun = new GameObject("Sun").AddComponent<Light>();
            lSun.transform.SetParent(transform, false);
            lSun.type = LightType.Directional;
            lSun.transform.rotation = Quaternion.Euler(18f, 160f, 0);
            lSun.intensity = 2.2f;
            lSun.shadows = LightShadows.Soft;
            lSun.color = new Color(1f, 0.78f, 0.52f);
            Light lFill = new GameObject("Fill").AddComponent<Light>();
            lFill.transform.SetParent(transform, false);
            lFill.type = LightType.Directional;
            lFill.transform.rotation = Quaternion.Euler(10f, -20f, 0);
            lFill.intensity = 0.45f;
            lFill.shadows = LightShadows.None;
            lFill.color = new Color(0.6f, 0.7f, 0.95f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.48f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);
        }

        /// <summary>Depth of field on the agent, warm grade, bloom on the rim, a vignette.</summary>
        private void BuildGrade()
        {
            Volume lVolume = new GameObject("KeyArtVolume").AddComponent<Volume>();
            lVolume.transform.SetParent(transform, false);
            lVolume.isGlobal = true;
            lVolume.priority = 20;
            VolumeProfile lProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            lVolume.sharedProfile = lProfile;
            DepthOfField lDof = lProfile.Add<DepthOfField>(true);
            lDof.mode.Override(DepthOfFieldMode.Bokeh);
            lDof.focusDistance.Override(4.5f);
            lDof.focalLength.Override(85f);
            lDof.aperture.Override(2.2f);
            Bloom lBloom = lProfile.Add<Bloom>(true);
            lBloom.intensity.Override(0.9f);
            lBloom.threshold.Override(0.95f);
            ColorAdjustments lColor = lProfile.Add<ColorAdjustments>(true);
            lColor.contrast.Override(18f);
            lColor.saturation.Override(-8f);
            lColor.postExposure.Override(0.1f);
            Vignette lVignette = lProfile.Add<Vignette>(true);
            lVignette.intensity.Override(0.38f);
            lVignette.smoothness.Override(0.5f);
            Tonemapping lTone = lProfile.Add<Tonemapping>(true);
            lTone.mode.Override(TonemappingMode.ACES);
        }

        private void Update()
        {
            // A slow breath of the camera.
            float t = Time.time;
            Vector3 lDrift = new Vector3(Mathf.Sin(t * 0.21f) * 0.06f, Mathf.Sin(t * 0.33f) * 0.03f, Mathf.Sin(t * 0.17f) * 0.05f);
            _camera.transform.SetPositionAndRotation(_eye + lDrift, Quaternion.LookRotation(_look - (_eye + lDrift)));
        }
    }
}
