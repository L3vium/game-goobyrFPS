using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>What a hand holds, as far as the animation cares.</summary>
    public enum HeldKind : byte { None, Pistol, Rifle, Throwable, Tool }

    /// <summary>
    /// The calibrated grips of the shared human skeleton, measured by <c>Goobyr/Build/Characters</c>
    /// on the aim clips: where a weapon's Grip sits in the right hand (palm, barrel along the aim),
    /// and where the left hand holds it, in the weapon's frame. The third-person socket, the
    /// left-hand IK and the first-person arms all read the same numbers, so a gun sits in the hand
    /// the same way everywhere. Lives in Resources (<c>Characters/HumanRig</c>).
    /// </summary>
    public class HumanRig : ScriptableObject
    {
        [System.Serializable]
        public struct Grip
        {
            [Tooltip("The weapon's Grip in the right hand's space.")]
            public Vector3 handPosition;
            public Quaternion handRotation;
            [Tooltip("The left hand in the weapon's space.")]
            public Vector3 supportPosition;
            public Quaternion supportRotation;
        }

        public Grip pistol = new Grip { handRotation = Quaternion.identity, supportRotation = Quaternion.identity };
        public Grip rifle = new Grip { handRotation = Quaternion.identity, supportRotation = Quaternion.identity };
        [Tooltip("Distance of the long-gun support hand down the barrel the grips were built for; each gun shifts it to its own fore-end.")]
        public float foreEnd = 0.3f;
        [Tooltip("A small item between the fingers (coin, card, vial), in the right hand's space.")]
        public Vector3 pinchPosition;
        public Quaternion pinchRotation = Quaternion.identity;
        [Tooltip("The left hand bone in a forward-facing fist frame (first-person guard), and its palm point.")]
        public Quaternion leftFistRotation = Quaternion.identity;
        public Vector3 leftPalm;
        [Tooltip("Eye point in the head bone's space (camera of the first-person arms, look-at origin).")]
        public Vector3 eyeInHead = new Vector3(0f, 0.09f, 0.1f);

        private static HumanRig _instance;
        public static HumanRig Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<HumanRig>("Characters/HumanRig");
                if (_instance == null) _instance = CreateInstance<HumanRig>();
                return _instance;
            }
        }

        public Grip For(HeldKind pKind) => pKind == HeldKind.Rifle ? rifle : pistol;

        /// <summary>The left hand's position in a weapon's space, moved to that gun's fore-end (long guns).</summary>
        public Vector3 SupportFor(HeldKind pKind, Transform pMuzzle)
        {
            if (pKind != HeldKind.Rifle) return pistol.supportPosition;
            float lFore = pMuzzle != null ? Mathf.Clamp(pMuzzle.localPosition.z * 0.5f, 0.17f, 0.42f) : foreEnd;
            return rifle.supportPosition + Vector3.forward * (lFore - foreEnd);
        }
    }
}
