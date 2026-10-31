using UnityEngine;
using static Com.IsartDigital.GoobyrFPS.EditorTools.SfxSynth;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The realistic sound set, layered like recorded foley:
    /// <list type="bullet">
    /// <item>Guns: a crack transient, a noise body, a low thump, the action cycling, and an outdoor
    /// tail (slap echo and a long rumble). Suppressed: a "pfft" and the slide clack.</item>
    /// <item>Footsteps per surface (grass, dirt, stone, wood, metal, water, sand), four takes each.</item>
    /// <item>Water: splashes, swim strokes, the muffled underwater bed.</item>
    /// <item>Ambience beds per map (forest morning, desert wind and surf, suburban night), seamless loops.</item>
    /// <item>Stealth: a low tension drone that follows the detection gauge, the "spotted" stinger, the combat hit.</item>
    /// </list>
    /// </summary>
    public static partial class SfxBuilder
    {
        private static void RealisticSet()
        {
            SfxSynth.Random = new System.Random(4321);

            // --------------------------------------------------------- guns
            Firearm("shot_pistol", 0.32f, 1800f, 85f, 0.7f, 0.9f);
            Firearm("shot_smg", 0.22f, 2200f, 95f, 0.55f, 0.7f);
            Firearm("shot_shotgun", 0.55f, 1100f, 60f, 1f, 1.3f);
            Firearm("shot_sniper", 0.7f, 1400f, 55f, 1f, 1.8f);
            for (int v = 0; v < 3; v++)
            {
                float[] lPfft = Env(Bandpass(White(0.12f), Range(900f, 1300f), 0.9f), 0.002f, 38f);
                float[] lClack = Delay(Env(Bandpass(White(0.04f), Range(2800f, 3600f), 3f), 0f, 120f), 0.035f);
                float[] lThump = Gain(Sine(0.09f, 140f, 70f, 40f), 0.5f);
                Write(Variant("shot_silenced", v), Reverb(Mix(Gain(lPfft, 1.4f), Gain(lClack, 0.9f), lThump), 0.2f, 0.5f, 0.15f, 0.25f));
            }
            Write("explosion", Reverb(Drive(Mix(
                Env(Lowpass(Brown(2.4f), 380f), 0.004f, 1.6f),
                Gain(Env(Lowpass(White(1.2f), 2400f), 0.001f, 5f), 0.8f),
                Gain(Sine(1.2f, 70f, 28f, 2.4f), 1.2f),
                Delay(Gain(Env(Grains(1.5f, 60, 1500f, 6000f, 90f), 0f, 2f), 0.25f), 0.12f)), 1.8f), 0.85f, 0.4f, 0.45f, 1.6f));
            Write("whizz", Mix(Env(Highpass(White(0.12f), 2500f), 0.003f, 30f), Gain(Env(Bandpass(White(0.25f), 1800f, 2f), 0.05f, 12f), 0.5f)));

            // --------------------------------------------------------- body and moves
            Write("punch", Mix(Gain(Sine(0.16f, 120f, 55f, 22f), 1.2f), Env(Lowpass(White(0.08f), 1800f), 0f, 45f), Gain(Env(Bandpass(White(0.05f), 600f, 2f), 0f, 70f), 0.6f)));
            Write("thud", Drive(Mix(Gain(Sine(0.3f, 90f, 45f, 14f), 1.3f), Env(Lowpass(Brown(0.3f), 500f), 0.002f, 12f)), 1.5f));
            Write("death", Drive(Mix(Gain(Sine(0.35f, 80f, 40f, 10f), 1.2f), Env(Lowpass(Brown(0.4f), 400f), 0.003f, 9f), Delay(Env(Lowpass(White(0.2f), 1500f), 0f, 20f), 0.08f)), 1.4f));
            Write("land", Mix(Gain(Sine(0.14f, 95f, 50f, 25f), 0.9f), Env(Lowpass(White(0.12f), 1400f), 0f, 30f), Delay(Gain(Env(Bandpass(White(0.15f), 3000f, 1.5f), 0.01f, 25f), 0.25f), 0.02f)));
            Write("jump", Mix(Gain(Swell(Bandpass(White(0.22f), 2400f, 1f)), 0.35f), Gain(Swell(Lowpass(White(0.25f), 700f)), 0.3f)));
            Write("slide", Mix(Gain(Swell(Bandpass(White(0.7f), 1400f, 0.8f), 0.6f), 0.7f), Gain(Swell(Grains(0.7f, 90, 2000f, 7000f, 300f), 0.5f), 0.25f)));
            Write("hit", Env(Bandpass(White(0.05f), 4200f, 4f), 0f, 70f));
            Write("hit_head", Mix(Env(Bandpass(White(0.06f), 5200f, 5f), 0f, 55f), Gain(Delay(Sine(0.12f, 3200f, 3200f, 30f), 0.015f), 0.25f)));
            Write("click", Gain(Env(Bandpass(White(0.03f), 3500f, 3f), 0f, 120f), 0.6f));
            Write("hover", Gain(Env(Bandpass(White(0.02f), 5000f, 4f), 0f, 160f), 0.25f));

            // --------------------------------------------------------- footsteps
            for (int v = 0; v < 4; v++)
            {
                float lPitch = Range(0.9f, 1.1f);
                float[] lHeel = Gain(Sine(0.07f, 110f * lPitch, 60f, 50f), 0.45f);
                Write(Variant("step_grass", v), Mix(lHeel, Gain(Env(Bandpass(White(0.16f), 3200f * lPitch, 0.7f), 0.01f, 22f), 0.55f), Gain(Grains(0.14f, 14, 2500f, 6000f, 400f), 0.12f)));
                Write(Variant("step_dirt", v), Mix(lHeel, Gain(Env(Lowpass(White(0.12f), 2200f * lPitch), 0.004f, 30f), 0.5f), Gain(Env(Grains(0.16f, 26, 1800f, 6500f, 350f), 0f, 14f), 0.3f)));
                Write(Variant("step_stone", v), Reverb(Mix(Gain(lHeel, 0.7f), Gain(Env(Highpass(White(0.05f), 1400f * lPitch), 0f, 80f), 0.7f), Delay(Gain(Env(Highpass(White(0.04f), 1800f), 0f, 90f), 0.35f), 0.07f)), 0.25f, 0.4f, 0.18f, 0.2f));
                Write(Variant("step_wood", v), Mix(Gain(Sine(0.14f, 140f * lPitch, 90f, 22f), 0.8f), Gain(Env(Bandpass(White(0.1f), 520f * lPitch, 3f), 0f, 35f), 0.9f), Gain(Delay(Env(Bandpass(White(0.05f), 800f, 4f), 0f, 60f), 0.06f), 0.3f)));
                float[] lRing = Mix(Sine(0.3f, 620f * lPitch, 615f, 14f), Gain(Sine(0.3f, 1470f * lPitch, 1460f, 18f), 0.6f), Gain(Sine(0.3f, 2380f * lPitch, 2370f, 22f), 0.4f));
                Write(Variant("step_metal", v), Mix(Gain(lHeel, 0.6f), Gain(lRing, 0.18f), Gain(Env(Highpass(White(0.04f), 2000f), 0f, 90f), 0.4f)));
                Write(Variant("step_water", v), Mix(Gain(Swell(Lowpass(White(0.25f), 1600f * lPitch)), 0.55f), Gain(Env(Grains(0.25f, 18, 500f, 1600f, 60f), 0.02f, 8f), 0.35f)));
                Write(Variant("step_sand", v), Mix(Gain(lHeel, 0.5f), Gain(Swell(Bandpass(White(0.18f), 2600f * lPitch, 0.6f), 0.7f), 0.4f)));
            }

            // --------------------------------------------------------- water
            Write("splash", Mix(Drive(Env(Lowpass(White(0.9f), 2600f), 0.005f, 4.5f), 1.3f), Gain(Env(Grains(1f, 70, 400f, 1800f, 50f), 0.05f, 3f), 0.5f), Gain(Sine(0.25f, 90f, 50f, 12f), 0.6f)));
            Write("splash_small", Mix(Env(Bandpass(White(0.35f), 1500f, 0.7f), 0.003f, 12f), Gain(Env(Grains(0.35f, 16, 600f, 2200f, 70f), 0.02f, 8f), 0.4f)));
            for (int v = 0; v < 3; v++)
                Write(Variant("swim", v), Mix(Gain(Swell(Lowpass(White(0.6f), Range(900f, 1300f)), 1.5f), 0.6f), Gain(Swell(Grains(0.6f, 24, 400f, 1400f, 70f)), 0.35f)));
            Write("underwater", Seamless(Mix(Gain(Lfo(Lowpass(Brown(8f), 260f), 0.5f, 0.5f), 0.8f), Gain(Grains(8f, 40, 300f, 900f, 40f), 0.2f)), 1f));

            // --------------------------------------------------------- ambience beds (seamless)
            float[] lForestWind = Gain(Lfo(Lowpass(Brown(32f), 700f), 0.15f, 0.7f), 0.5f);
            float[] lLeaves = Gain(Lfo(Bandpass(White(32f), 4500f, 0.5f), 0.3f, 0.9f), 0.08f);
            Write("amb_forest", Seamless(Mix(lForestWind, lLeaves, Birds(32f, 22), Gain(Insects(32f, 6800f), 0.05f)), 2f));
            float[] lGusts = Gain(Lfo(Bandpass(Brown(32f), 520f, 0.4f), 0.12f, 0.85f), 0.7f);
            float[] lSurf = Gain(Lfo(Lowpass(White(32f), 900f), 0.09f, 0.9f), 0.35f);
            Write("amb_desert", Seamless(Mix(lGusts, lSurf, Gain(Lfo(Highpass(White(32f), 3500f), 0.2f, 0.9f), 0.04f)), 2f));
            Write("amb_night", Seamless(Mix(Gain(Lfo(Lowpass(Brown(32f), 350f), 0.1f, 0.6f), 0.35f), Gain(Crickets(32f), 0.22f), Gain(Lfo(Lowpass(Brown(32f), 120f), 0.05f, 0.5f), 0.25f)), 2f));

            // --------------------------------------------------------- stealth music cues
            float[] lDrone = Mix(Gain(Sine(12f, 55f, 55f, 0f), 0.5f), Gain(Sine(12f, 55.6f, 55.6f, 0f), 0.4f), Gain(Lowpass(Saw(12f, 82.4f, 82.4f), 400f), 0.22f), Gain(Lfo(Bandpass(White(12f), 220f, 2f), 0.4f, 0.8f), 0.25f));
            Write("tension_loop", Seamless(lDrone, 1.5f));
            Write("spotted", Reverb(Mix(Gain(Env(Lowpass(Saw(0.9f, 220f, 440f), 2200f), 0.25f, 4f), 0.45f), Gain(Env(Lowpass(Saw(0.9f, 330f, 660f), 2600f), 0.25f, 4f), 0.3f), Delay(Gain(Sine(0.6f, 70f, 40f, 6f), 0.9f), 0.32f)), 0.6f, 0.4f, 0.35f, 1f));
            Write("combat_hit", Reverb(Drive(Mix(Gain(Sine(1f, 60f, 32f, 4f), 1.2f), Env(Lowpass(Brown(1f), 500f), 0.002f, 5f), Gain(Env(Highpass(White(0.6f), 3000f), 0.12f, 5f), 0.2f)), 1.6f), 0.7f, 0.5f, 0.4f, 1.2f));
        }

        private static string Variant(string pName, int pIndex) => pIndex == 0 ? pName : pName + "_" + pIndex;

        /// <summary>
        /// A gunshot in layers. <paramref name="pLength"/> the body, <paramref name="pBody"/> its
        /// low-pass, <paramref name="pThump"/> the low punch, <paramref name="pMechanism"/> how loud the
        /// action is, <paramref name="pTail"/> the outdoor tail length. Three takes.
        /// </summary>
        private static void Firearm(string pName, float pLength, float pBody, float pThump, float pMechanism, float pTail)
        {
            for (int v = 0; v < 3; v++)
            {
                float[] lCrack = Gain(Env(Highpass(White(0.012f), 1800f), 0f, 300f), 1.6f);
                float[] lBody = Drive(Gain(Env(Lowpass(White(pLength), pBody * Range(0.9f, 1.1f)), 0.0015f, 9f / pLength), 1.3f), 2.2f);
                float[] lPunch = Gain(Sine(pLength * 0.7f, pThump * 2.2f, pThump, 12f / pLength), 1.1f);
                float[] lAction = Delay(Gain(Env(Bandpass(White(0.05f), Range(2600f, 3400f), 2.5f), 0f, 90f), 0.35f * pMechanism), Range(0.05f, 0.08f));
                float[] lDry = Mix(lCrack, lBody, lPunch, lAction);
                // Outdoors: a slap off the nearest walls, then the report rolling away.
                float[] lSlap = Delay(Gain(Lowpass(lDry, 2200f), 0.28f), Range(0.11f, 0.18f));
                float[] lRumble = Gain(Env(Lowpass(Brown(pTail), 320f), 0.03f, 3.2f / pTail), 0.45f);
                Write(Variant(pName, v), Reverb(Mix(lDry, lSlap, lRumble), 0.75f, 0.55f, 0.25f, pTail * 0.6f));
            }
        }

        /// <summary>Birdsong: random chirps (quick pitch sweeps and trills) across the bed, near and far.</summary>
        private static float[] Birds(float pSeconds, int pCalls)
        {
            float[] lBed = new float[Samples(pSeconds)];
            for (int c = 0; c < pCalls; c++)
            {
                float lStart = Range(0f, pSeconds - 2f), lFar = Range(0.15f, 0.5f);
                int lNotes = SfxSynth.Random.Next(2, 7);
                float lBase = Range(2200f, 4200f);
                for (int n = 0; n < lNotes; n++)
                {
                    float[] lNote = Env(Sine(0.12f, lBase * Range(0.9f, 1.3f), lBase * Range(0.7f, 1.5f), 0f), 0.01f, 30f);
                    int lAt = Samples(lStart + n * Range(0.09f, 0.16f));
                    for (int i = 0; i < lNote.Length && lAt + i < lBed.Length; i++) lBed[lAt + i] += lNote[i] * lFar * 0.5f;
                }
            }
            return lBed;
        }

        /// <summary>Cicada-like insects: a high band of noise pulsing in slow waves.</summary>
        private static float[] Insects(float pSeconds, float pPitch)
        {
            float[] lBuzz = Bandpass(White(pSeconds), pPitch, 6f);
            for (int i = 0; i < lBuzz.Length; i++)
            {
                float t = (float)i / RATE;
                lBuzz[i] *= 0.5f + 0.5f * Mathf.Sin(t * 70f * Mathf.PI * 2f) * (0.5f + 0.5f * Mathf.Sin(t * 0.21f * Mathf.PI * 2f));
            }
            return lBuzz;
        }

        /// <summary>Crickets: chirp trains (pulses of a 4.6 kHz tone) from a few insects at different rates.</summary>
        private static float[] Crickets(float pSeconds)
        {
            float[] lBed = new float[Samples(pSeconds)];
            for (int k = 0; k < 5; k++)
            {
                float lFreq = Range(4200f, 5200f), lRate = Range(1.2f, 2.4f), lPhase = Range(0f, 1f), lLevel = Range(0.3f, 1f);
                for (int i = 0; i < lBed.Length; i++)
                {
                    float t = (float)i / RATE;
                    float lCycle = Mathf.Repeat(t * lRate + lPhase, 1f);
                    float lGate = lCycle < 0.25f ? Mathf.Max(0f, Mathf.Sin(lCycle / 0.25f * Mathf.PI * 3f)) : 0f;
                    lBed[i] += Mathf.Sin(t * lFreq * Mathf.PI * 2f) * lGate * lLevel * 0.3f;
                }
            }
            return lBed;
        }
    }
}
