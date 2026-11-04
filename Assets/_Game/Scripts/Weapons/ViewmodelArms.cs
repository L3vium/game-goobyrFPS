using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Weapons
{
    /// <summary>
    /// The authored first-person arms (<c>Tools/Blender/fp</c>, built by <c>Goobyr/Build/First Person Arms</c>):
    /// the arms model (generic rig rooted at the eye, weapon socket <see cref="SOCKET"/> under the right
    /// hand), the arms controller and one override controller per family of held item, and the pose
    /// manifest (clip events: punch contact, throw release, magazine out / in). Lives in Resources
    /// (<c>FirstPerson/ViewmodelArms</c>).
    /// </summary>
    public class ViewmodelArms : ScriptableObject
    {
        /// <summary>The bone the held item is parented to at identity (its frame is the item's Grip frame).</summary>
        public const string SOCKET = "weapon_r";
        /// <summary>The bone a magazine rides on in the support hand during a reload.</summary>
        public const string PROP = "prop_l";

        public const string P_HIP = "Hip", P_ADS = "Ads", P_SPRINT = "Sprint", P_SPEED = "ActionSpeed";
        public const string S_EMPTY = "Empty", S_JAB = "Jab", S_CROSS = "Cross", S_TAKEDOWN = "Takedown", S_RELOAD = "Reload", S_THROW = "Throw";
        public const int LAYER_POSE = 0, LAYER_LEFT = 1, LAYER_ACTION = 2;

        [System.Serializable]
        public struct Family
        {
            public string name;
            public AnimatorOverrideController controller;
            [Tooltip("Has an authored reload clip (else the reload stays procedural).")]
            public bool authoredReload;
            public float reloadLength;
            public float throwLength;
        }

        public GameObject model;
        public RuntimeAnimatorController controller;
        public List<Family> families = new List<Family>();
        public TextAsset manifest;

        private static ViewmodelArms _instance;
        public static ViewmodelArms Instance => _instance != null ? _instance : _instance = Resources.Load<ViewmodelArms>("FirstPerson/ViewmodelArms");

        public bool TryFamily(string pName, out Family pFamily)
        {
            foreach (Family lFamily in families)
                if (lFamily.name == pName) { pFamily = lFamily; return true; }
            pFamily = default;
            return false;
        }

        /// <summary>Which authored family holds an item (null: the fists).</summary>
        public static string FamilyOf(ItemDefinition pItem)
        {
            if (pItem == null) return "Fists";
            return pItem.kind switch
            {
                ItemKind.Fists => "Fists",
                ItemKind.Pistol or ItemKind.SilencedPistol => "Pistol",
                ItemKind.Smg => "Smg",
                ItemKind.Shotgun => "Shotgun",
                ItemKind.Sniper => "Sniper",
                ItemKind.RocketLauncher => "Launcher",
                ItemKind.Flamethrower => "Flamer",
                ItemKind.Wrench or ItemKind.Crowbar or ItemKind.Bottle or ItemKind.Knife or ItemKind.Machete or ItemKind.Bat => "Handle",
                ItemKind.Rock or ItemKind.Brick or ItemKind.C4 or ItemKind.SmokeGrenade => "Palm",
                _ => "Pinch"
            };
        }

        // ---------------------------------------------------------------- manifest

        private Dictionary<string, Dictionary<string, float>> _events;

        /// <summary>Time (s, at normal speed) of an event of a clip from the manifest (strike, release, magOut...), or the fallback.</summary>
        public float Event(string pClip, string pEvent, float pFallback)
        {
            if (_events == null) ParseEvents();
            return _events.TryGetValue(pClip, out Dictionary<string, float> lClip) && lClip.TryGetValue(pEvent, out float lTime) ? lTime : pFallback;
        }

        /// <summary>Length (s) of an authored clip from the manifest, or the fallback.</summary>
        public float Length(string pClip, float pFallback) => Event(pClip, "__length", pFallback);

        private void ParseEvents()
        {
            _events = new Dictionary<string, Dictionary<string, float>>();
            if (manifest == null) return;
            FpPoseManifest lData = FpPoseManifest.Parse(manifest.text);
            foreach (FpPoseManifest.Clip lClip in lData.clips)
            {
                Dictionary<string, float> lMap = new Dictionary<string, float> { ["__length"] = lClip.length };
                foreach (FpPoseManifest.Event lEvent in lClip.events) lMap[lEvent.name] = lEvent.time;
                _events[lClip.name] = lMap;
            }
        }
    }

    /// <summary>
    /// The pose manifest written by <c>Tools/Blender/fp/fp_build.py</c> (<c>fp_poses.json</c>): for every
    /// authored pose the socket and the Sight in camera space (Unity axes, metres), for every clip its
    /// length and events. The JSON uses objects keyed by name; <see cref="Parse"/> turns them into lists.
    /// </summary>
    public class FpPoseManifest
    {
        public class Frame
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        public class Pose
        {
            public string name, clip;
            public float time;
            public Frame socket, sight;
        }

        public class Event
        {
            public string name;
            public float time;
        }

        public class Clip
        {
            public string name, family;
            public float length;
            public List<Event> events = new List<Event>();
        }

        public readonly List<Pose> poses = new List<Pose>();
        public readonly List<Clip> clips = new List<Clip>();

        /// <summary>Reads the manifest (a small hand-rolled reader: JsonUtility has no dictionaries).</summary>
        public static FpPoseManifest Parse(string pJson)
        {
            FpPoseManifest lOut = new FpPoseManifest();
            object lRoot = MiniJson.Parse(pJson);
            if (!(lRoot is Dictionary<string, object> lTop)) return lOut;
            if (lTop.TryGetValue("poses", out object lPoses) && lPoses is Dictionary<string, object> lPoseMap)
                foreach (KeyValuePair<string, object> lPair in lPoseMap)
                {
                    if (!(lPair.Value is Dictionary<string, object> p)) continue;
                    lOut.poses.Add(new Pose
                    {
                        name = lPair.Key,
                        clip = p.TryGetValue("clip", out object c) ? c as string : lPair.Key,
                        time = p.TryGetValue("time", out object t) ? System.Convert.ToSingle(t) : 0f,
                        socket = ReadFrame(p, "socket"),
                        sight = ReadFrame(p, "sight")
                    });
                }
            if (lTop.TryGetValue("clips", out object lClips) && lClips is Dictionary<string, object> lClipMap)
                foreach (KeyValuePair<string, object> lPair in lClipMap)
                {
                    if (!(lPair.Value is Dictionary<string, object> c)) continue;
                    Clip lClip = new Clip
                    {
                        name = lPair.Key,
                        family = c.TryGetValue("family", out object f) ? f as string : "",
                        length = c.TryGetValue("length", out object l) ? System.Convert.ToSingle(l) : 0f
                    };
                    if (c.TryGetValue("events", out object e) && e is Dictionary<string, object> lEvents)
                        foreach (KeyValuePair<string, object> lEvent in lEvents)
                            lClip.events.Add(new Event { name = lEvent.Key, time = System.Convert.ToSingle(lEvent.Value) });
                    lOut.clips.Add(lClip);
                }
            return lOut;
        }

        private static Frame ReadFrame(Dictionary<string, object> pPose, string pKey)
        {
            if (!pPose.TryGetValue(pKey, out object lValue) || !(lValue is Dictionary<string, object> lFrame)) return null;
            List<object> lPos = lFrame.TryGetValue("pos", out object p) ? p as List<object> : null;
            List<object> lRot = lFrame.TryGetValue("rot", out object r) ? r as List<object> : null;
            if (lPos == null || lRot == null || lPos.Count < 3 || lRot.Count < 4) return null;
            return new Frame
            {
                position = new Vector3(F(lPos[0]), F(lPos[1]), F(lPos[2])),
                rotation = new Quaternion(F(lRot[0]), F(lRot[1]), F(lRot[2]), F(lRot[3]))
            };
        }

        private static float F(object pValue) => System.Convert.ToSingle(pValue, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A minimal JSON reader (objects, arrays, numbers, strings, booleans, null).</summary>
    public static class MiniJson
    {
        public static object Parse(string pJson)
        {
            int i = 0;
            return Value(pJson, ref i);
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{')
            {
                Dictionary<string, object> lObject = new Dictionary<string, object>();
                i++;
                Skip(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return lObject; }
                while (i < s.Length)
                {
                    Skip(s, ref i);
                    string lKey = Text(s, ref i);
                    Skip(s, ref i);
                    i++; // ':'
                    lObject[lKey] = Value(s, ref i);
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    i++; // '}'
                    break;
                }
                return lObject;
            }
            if (c == '[')
            {
                List<object> lList = new List<object>();
                i++;
                Skip(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return lList; }
                while (i < s.Length)
                {
                    lList.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    i++; // ']'
                    break;
                }
                return lList;
            }
            if (c == '"') return Text(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int lStart = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(lStart, i - lStart), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Text(string s, ref int i)
        {
            System.Text.StringBuilder lText = new System.Text.StringBuilder();
            i++; // opening quote
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char e = s[i];
                    if (e == 'u' && i + 4 < s.Length)
                    {
                        lText.Append((char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16));
                        i += 4;
                    }
                    else lText.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e);
                }
                else lText.Append(s[i]);
                i++;
            }
            i++; // closing quote
            return lText.ToString();
        }
    }
}
