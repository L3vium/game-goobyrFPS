using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.EditorTools;
using Com.IsartDigital.GoobyrFPS.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Tests
{
    /// <summary>
    /// The authored first-person poses survive the trip from Blender: every pose of fp_poses.json is
    /// sampled from the imported FP_Arms clips, and its weapon socket - and, with the family's gun in
    /// the socket at identity (Grip on the socket), its Sight - must sit within a millimetre of where
    /// fp_build.py put it (camera space). Aimed poses also keep the Sight on the camera axis.
    /// </summary>
    public class FpPoseTests
    {
        private const float TOLERANCE = 0.001f;
        private const float ANGLE = 0.2f;

        [Test]
        public void SocketsAndSightsMatchTheManifest()
        {
            TextAsset lJson = AssetDatabase.LoadAssetAtPath<TextAsset>(FpArmsBuilder.MANIFEST);
            if (lJson == null) Assert.Ignore($"{FpArmsBuilder.MANIFEST} is not imported yet (Tools/Blender/fp/fp_build.py, Goobyr/Build/First Person Arms)");
            FpPoseManifest lManifest = FpPoseManifest.Parse(lJson.text);
            Assert.Greater(lManifest.poses.Count, 30, "too few poses in the manifest");
            GameObject lModel = AssetDatabase.LoadAssetAtPath<GameObject>(FpArmsBuilder.FBX);
            Assert.IsNotNull(lModel, $"{FpArmsBuilder.FBX} is missing");
            Dictionary<string, AnimationClip> lClips = FpArmsBuilder.Clips();

            GameObject lArms = Object.Instantiate(lModel);
            List<string> lErrors = new List<string>();
            int lChecked = 0, lSights = 0;
            try
            {
                Transform lSocket = FindDeep(lArms.transform, ViewmodelArms.SOCKET);
                Assert.IsNotNull(lSocket, "no socket bone " + ViewmodelArms.SOCKET);
                Dictionary<string, Transform> lWeapons = new Dictionary<string, Transform>();
                foreach (FpPoseManifest.Pose lPose in lManifest.poses)
                {
                    if (!lClips.TryGetValue(lPose.clip, out AnimationClip lClip)) { lErrors.Add($"{lPose.name}: no clip {lPose.clip}"); continue; }
                    lClip.SampleAnimation(lArms, lPose.time);
                    lChecked++;
                    Vector3 lPos = lArms.transform.InverseTransformPoint(lSocket.position);
                    Quaternion lRot = Quaternion.Inverse(lArms.transform.rotation) * lSocket.rotation;
                    float lDistance = Vector3.Distance(lPos, lPose.socket.position);
                    float lAngle = Quaternion.Angle(lRot, lPose.socket.rotation);
                    if (lDistance > TOLERANCE || lAngle > ANGLE)
                        lErrors.Add($"{lPose.name}: socket off by {lDistance * 1000f:0.00} mm / {lAngle:0.00} deg (at {lPos:F4}, authored {lPose.socket.position:F4})");

                    if (lPose.sight == null) continue;
                    string lFamily = lPose.name.Split('_')[0];
                    if (!FpArmsBuilder.WEAPONS.TryGetValue(lFamily, out string lWeaponName)) continue;
                    if (!lWeapons.TryGetValue(lWeaponName, out Transform lWeapon))
                    {
                        GameObject lSource = AssetDatabase.LoadAssetAtPath<GameObject>($"{PbrLibrary.ROOT}/Models/{lWeaponName}/{lWeaponName}.fbx");
                        if (lSource == null) { lErrors.Add($"{lPose.name}: no model {lWeaponName}"); continue; }
                        lWeapon = Object.Instantiate(lSource, lSocket, false).transform;
                        Transform lGrip = lWeapon.Find("Grip");
                        if (lGrip != null) lWeapon.localPosition = -lGrip.localPosition;
                        lWeapons[lWeaponName] = lWeapon;
                    }
                    foreach (Transform lOther in lWeapons.Values) lOther.gameObject.SetActive(lOther == lWeapon);
                    Transform lSightPoint = lWeapon.Find("Sight");
                    if (lSightPoint == null) { lErrors.Add($"{lWeaponName} has no Sight"); continue; }
                    lSights++;
                    Vector3 lSight = lArms.transform.InverseTransformPoint(lSightPoint.position);
                    float lSightDistance = Vector3.Distance(lSight, lPose.sight.position);
                    if (lSightDistance > TOLERANCE)
                        lErrors.Add($"{lPose.name}: Sight off by {lSightDistance * 1000f:0.00} mm (at {lSight:F4}, authored {lPose.sight.position:F4})");
                    if (lPose.name.EndsWith("_ADS") && new Vector2(lSight.x, lSight.y).magnitude > TOLERANCE)
                        lErrors.Add($"{lPose.name}: aimed Sight {new Vector2(lSight.x, lSight.y) * 1000f} mm off the camera axis");
                }
            }
            finally { Object.DestroyImmediate(lArms); }
            Debug.Log($"[FpPoseTests] {lChecked} poses, {lSights} sights checked");
            Assert.IsEmpty(lErrors, string.Join("\n", lErrors));
        }

        [Test]
        public void EveryFamilyHasItsPoses()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FpArmsBuilder.FBX) == null) Assert.Ignore($"{FpArmsBuilder.FBX} is not imported yet");
            Dictionary<string, AnimationClip> lClips = FpArmsBuilder.Clips();
            List<string> lMissing = new List<string>();
            foreach (string lFamily in FpArmsBuilder.FAMILIES)
                foreach (string lPose in new[] { "Hip", "ADS", "Sprint" })
                    if (!lClips.ContainsKey($"{lFamily}_{lPose}")) lMissing.Add($"{lFamily}_{lPose}");
            foreach (string lClip in new[] { "Jab", "Cross", "Takedown" })
                if (!lClips.ContainsKey(lClip)) lMissing.Add(lClip);
            Assert.IsEmpty(lMissing, "missing clips: " + string.Join(", ", lMissing));
        }

        private static Transform FindDeep(Transform pRoot, string pName)
        {
            if (pRoot.name == pName) return pRoot;
            foreach (Transform lChild in pRoot)
            {
                Transform lFound = FindDeep(lChild, pName);
                if (lFound != null) return lFound;
            }
            return null;
        }
    }
}
