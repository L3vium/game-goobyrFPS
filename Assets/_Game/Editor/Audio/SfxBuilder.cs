using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// Synthesises every sound effect of the game (no audio was delivered): noise bursts and
    /// pitched blips shaped by envelopes and filters, written as WAV files in
    /// <c>Audio/Resources/Sfx</c>. The gameplay sounds are layered realistically
    /// (<c>SfxBuilder.Realistic</c>); the systemic props keep these simpler recipes.
    /// </summary>
    public static partial class SfxBuilder
    {
        private const int RATE = 44100;
        private const string DIR = "Assets/_Game/Audio/Resources/Sfx";
        private static System.Random _random;

        [MenuItem("Goobyr/Build/Sound Effects")]
        public static void Build()
        {
            EditorAssets.EnsureFolder(DIR);
            _random = new System.Random(1234);

            Gun("shot_silenced", 0.18f, 0.25f, 900f, 0.35f, 2500f);
            Gun("shot_pistol", 0.35f, 0.9f, 220f, 0.6f, 6000f);
            Gun("shot_smg", 0.22f, 0.7f, 260f, 0.5f, 7000f);
            Gun("shot_shotgun", 0.6f, 1f, 120f, 0.9f, 4000f);
            Gun("shot_sniper", 0.9f, 1f, 90f, 1f, 5000f);
            Write("rocket_launch", Mix(Noise(0.7f, 0.9f, 1500f, 4f), Tone(0.6f, 140f, 60f, 0.4f, 3f)));
            Write("rocket_fly", Noise(1.2f, 0.4f, 1200f, 0.8f));
            Write("explosion", Mix(Noise(1.8f, 1f, 900f, 2.2f), Tone(1.4f, 70f, 28f, 0.9f, 2.5f), Noise(0.15f, 0.8f, 9000f, 20f)));
            Write("dryfire", Tone(0.06f, 1800f, 1600f, 0.4f, 40f));
            Write("reload", Mix(Delay(Tone(0.05f, 1200f, 900f, 0.5f, 50f), 0f), Delay(Tone(0.06f, 800f, 700f, 0.5f, 40f), 0.35f), Delay(Tone(0.05f, 1400f, 1300f, 0.6f, 50f), 0.7f)));
            Write("switch", Mix(Tone(0.05f, 900f, 700f, 0.4f, 40f), Delay(Tone(0.05f, 1100f, 900f, 0.4f, 40f), 0.08f)));
            Write("swing", Noise(0.22f, 0.5f, 2500f, 8f, true));
            Write("punch", Mix(Tone(0.18f, 160f, 60f, 1f, 14f), Noise(0.1f, 0.6f, 1500f, 25f)));
            Write("throw", Noise(0.25f, 0.4f, 1800f, 6f, true));
            Write("coin", Mix(Tone(0.5f, 2600f, 2600f, 0.5f, 6f), Tone(0.5f, 3900f, 3900f, 0.3f, 7f), Delay(Tone(0.3f, 2700f, 2700f, 0.3f, 9f), 0.12f)));
            Write("brick", Mix(Tone(0.25f, 120f, 70f, 0.9f, 12f), Noise(0.2f, 0.7f, 1200f, 14f)));
            // Throwables, takedowns, parkour, doors (0.6): glass shatter, metal clang, a muffled neck snap...
            Write("glass", Mix(Noise(0.5f, 0.8f, 7000f, 7f), Tone(0.4f, 3100f, 2900f, 0.35f, 9f), Delay(Tone(0.3f, 4400f, 4200f, 0.25f, 12f), 0.05f), Delay(Noise(0.3f, 0.4f, 6000f, 10f), 0.09f)));
            Write("metal_clang", Mix(Tone(0.7f, 620f, 600f, 0.7f, 5f, 0.02f), Tone(0.6f, 1470f, 1450f, 0.4f, 6f), Noise(0.06f, 0.6f, 3000f, 40f)));
            Write("takedown", Mix(Noise(0.25f, 0.6f, 900f, 12f, true), Delay(Tone(0.12f, 140f, 60f, 0.8f, 20f), 0.12f), Delay(Noise(0.05f, 0.5f, 2500f, 50f), 0.2f)));
            Write("vault", Mix(Noise(0.3f, 0.45f, 1400f, 8f, true), Delay(Tone(0.12f, 110f, 70f, 0.6f, 18f), 0.28f)));
            Write("mantle", Mix(Noise(0.4f, 0.4f, 1100f, 6f, true), Tone(0.1f, 160f, 90f, 0.4f, 20f), Delay(Tone(0.12f, 120f, 70f, 0.6f, 18f), 0.32f)));
            Write("crowbar", Mix(Tone(0.5f, 900f, 700f, 0.5f, 6f, 0.05f), Noise(0.45f, 0.7f, 2200f, 6f), Delay(Mix(Tone(0.3f, 180f, 90f, 0.9f, 10f), Noise(0.3f, 0.8f, 1800f, 10f)), 0.4f)));
            Write("door_unlock", Mix(Tone(0.06f, 1800f, 1800f, 0.5f, 40f), Delay(Tone(0.08f, 900f, 900f, 0.6f, 30f), 0.12f), Delay(Noise(0.2f, 0.3f, 1500f, 12f), 0.2f)));
            Write("vent_open", Mix(Noise(0.5f, 0.35f, 3000f, 5f), Delay(Tone(0.3f, 480f, 440f, 0.4f, 8f), 0.35f)));
            Write("ladder_drop", Mix(Noise(0.4f, 0.5f, 1500f, 6f, true), Delay(Tone(0.5f, 300f, 280f, 0.8f, 7f, 0.03f), 0.38f), Delay(Tone(0.4f, 760f, 740f, 0.4f, 8f), 0.4f)));
            Write("c4_stick", Mix(Noise(0.08f, 0.5f, 800f, 30f), Delay(Tone(0.06f, 2200f, 2200f, 0.4f, 30f), 0.1f), Delay(Tone(0.06f, 2200f, 2200f, 0.4f, 30f), 0.25f)));
            Write("pickup", Mix(Tone(0.12f, 700f, 1000f, 0.6f, 10f), Delay(Tone(0.15f, 1000f, 1500f, 0.6f, 10f), 0.07f)));
            Write("flame_start", Noise(0.6f, 0.6f, 900f, 2f));
            Write("fire_start", Noise(0.8f, 0.5f, 700f, 2f));
            Write("smoke_pop", Mix(Tone(0.12f, 300f, 120f, 0.8f, 18f), Noise(1.2f, 0.5f, 3000f, 1.4f)));
            Write("sprinkler", Noise(1.4f, 0.4f, 6000f, 0.6f));
            Write("power_off", Mix(Tone(0.6f, 220f, 40f, 0.7f, 3f), Noise(0.1f, 0.6f, 5000f, 25f)));
            Write("power_on", Mix(Tone(0.5f, 60f, 240f, 0.6f, 3f), Noise(0.08f, 0.5f, 5000f, 25f)));
            Write("chain_snap", Mix(Tone(0.08f, 3000f, 2500f, 0.6f, 30f), Noise(0.12f, 0.7f, 6000f, 20f)));
            Write("crash", Mix(Noise(1.3f, 1f, 4000f, 2.5f), Tone(0.6f, 90f, 40f, 0.8f, 5f), Bells(1.2f, 0.4f)));
            Write("vent_hiss", Noise(1.6f, 0.4f, 7000f, 0.5f));
            Write("gas_alarm", Siren(2.5f, 600f, 900f, 2.5f, 0.6f));
            Write("pour", Noise(0.6f, 0.3f, 1500f, 2f, true));
            Write("radio", Mix(Noise(0.8f, 0.25f, 3000f, 0.5f), Tone(0.8f, 440f, 440f, 0.15f, 0.5f, 7f)));
            Write("tv", Mix(Noise(0.8f, 0.2f, 5000f, 0.5f), Tone(0.8f, 330f, 330f, 0.15f, 0.5f, 5f)));
            Write("phone", Mix(Tone(0.4f, 1300f, 1300f, 0.4f, 1f, 22f), Delay(Tone(0.4f, 1300f, 1300f, 0.4f, 1f, 22f), 0.5f)));
            Write("caralarm", Siren(1.2f, 700f, 1100f, 6f, 0.5f));
            Write("generator", Tone(1.2f, 55f, 55f, 0.5f, 0.3f, 18f));
            Write("siren", Siren(5f, 500f, 1000f, 0.5f, 0.7f));
            Write("brakes", Mix(Tone(0.8f, 2400f, 1800f, 0.25f, 2f), Noise(0.8f, 0.4f, 3000f, 2f)));
            Write("truck", Mix(Tone(2.5f, 60f, 75f, 0.5f, 0.6f, 12f), Noise(2.5f, 0.3f, 600f, 0.6f)));
            Write("death", Mix(Tone(0.4f, 400f, 120f, 0.5f, 5f, 12f)));
            Write("thud", Tone(0.25f, 110f, 50f, 0.8f, 12f));
            Write("whizz", Mix(Tone(0.18f, 3000f, 1200f, 0.4f, 12f), Noise(0.18f, 0.3f, 6000f, 12f)));
            Write("spotted", Mix(Tone(0.12f, 900f, 1400f, 0.6f, 10f), Delay(Tone(0.18f, 1400f, 1400f, 0.6f, 8f), 0.1f)));
            Write("puke", Mix(Noise(0.6f, 0.6f, 900f, 3f, true), Tone(0.6f, 180f, 120f, 0.4f, 3f, 9f)));
            Write("hit", Tone(0.07f, 1800f, 1500f, 0.5f, 40f));
            Write("hit_head", Mix(Tone(0.1f, 2600f, 2200f, 0.6f, 25f), Delay(Tone(0.12f, 3400f, 3400f, 0.4f, 20f), 0.04f)));
            Write("ping", Mix(Tone(0.4f, 1600f, 1600f, 0.4f, 6f), Delay(Tone(0.4f, 2400f, 2400f, 0.3f, 6f), 0.12f)));
            Write("target_down", Mix(Tone(0.25f, 500f, 500f, 0.5f, 5f), Delay(Tone(0.25f, 750f, 750f, 0.5f, 5f), 0.15f), Delay(Tone(0.5f, 1000f, 1000f, 0.5f, 3f), 0.3f)));
            Write("announce", Mix(Tone(0.3f, 660f, 660f, 0.4f, 5f), Delay(Tone(0.4f, 880f, 880f, 0.4f, 4f), 0.12f)));
            Write("win", Mix(Tone(0.25f, 523f, 523f, 0.4f, 4f), Delay(Tone(0.25f, 659f, 659f, 0.4f, 4f), 0.18f), Delay(Tone(0.25f, 784f, 784f, 0.4f, 4f), 0.36f), Delay(Tone(0.8f, 1046f, 1046f, 0.5f, 2f), 0.54f)));
            Write("lose", Mix(Tone(0.35f, 392f, 392f, 0.4f, 3f), Delay(Tone(0.35f, 330f, 330f, 0.4f, 3f), 0.3f), Delay(Tone(0.9f, 262f, 250f, 0.5f, 2f), 0.6f)));
            Write("click", Tone(0.05f, 1300f, 1100f, 0.4f, 40f));
            Write("hover", Tone(0.04f, 2000f, 2100f, 0.15f, 50f));
            Write("map", Noise(0.25f, 0.4f, 3500f, 8f, true));
            Write("jump", Tone(0.18f, 300f, 600f, 0.3f, 10f));
            Write("land", Mix(Tone(0.15f, 140f, 70f, 0.6f, 15f), Noise(0.12f, 0.4f, 900f, 20f)));
            Write("slide", Noise(0.45f, 0.4f, 1600f, 3f));
            Write("jumppad", Mix(Tone(0.5f, 150f, 900f, 0.7f, 4f, 30f), Noise(0.2f, 0.3f, 2500f, 10f)));
            for (int i = 1; i <= 3; i++) Write("footstep_" + i, Mix(Tone(0.08f, 130f + i * 20f, 80f, 0.4f, 30f), Noise(0.07f, 0.3f, 1200f + i * 200f, 30f)));
            // The realistic set overwrites the gameplay sounds above (guns, body, steps) and adds the rest.
            RealisticSet();

            AssetDatabase.Refresh();
            Debug.Log("[Build] sound effects");
        }

        // ---------------------------------------------------------------- synthesis

        private static void Gun(string pName, float pLength, float pBody, float pThump, float pCrack, float pCutoff)
        {
            Write(pName, Mix(Noise(pLength, pCrack, pCutoff, 10f / pLength), Tone(pLength * 0.8f, pThump, pThump * 0.4f, pBody, 9f / pLength)));
            Write(pName + "_1", Mix(Noise(pLength * 1.05f, pCrack, pCutoff * 0.9f, 10f / pLength), Tone(pLength * 0.8f, pThump * 1.08f, pThump * 0.42f, pBody, 9f / pLength)));
        }

        private static float[] Noise(float pSeconds, float pVolume, float pCutoff, float pDecay, bool pSwell = false)
        {
            float[] lData = new float[(int)(pSeconds * RATE)];
            float lLow = 0f;
            float lAlpha = Mathf.Clamp01(2f * Mathf.PI * pCutoff / RATE);
            for (int i = 0; i < lData.Length; i++)
            {
                float t = (float)i / RATE;
                float lWhite = (float)_random.NextDouble() * 2f - 1f;
                lLow += (lWhite - lLow) * lAlpha;
                float lEnv = pSwell ? Mathf.Sin(Mathf.Clamp01(t / pSeconds) * Mathf.PI) : Mathf.Exp(-t * pDecay);
                lData[i] = lLow * lEnv * pVolume * 1.8f;
            }
            return Fade(lData);
        }

        private static float[] Tone(float pSeconds, float pFrom, float pTo, float pVolume, float pDecay, float pWobble = 0f)
        {
            float[] lData = new float[(int)(pSeconds * RATE)];
            double lPhase = 0;
            for (int i = 0; i < lData.Length; i++)
            {
                float t = (float)i / RATE;
                float lFreq = Mathf.Lerp(pFrom, pTo, t / pSeconds) * (1f + (pWobble > 0 ? Mathf.Sin(t * pWobble * Mathf.PI * 2f) * 0.08f : 0f));
                lPhase += lFreq / RATE;
                float lWave = Mathf.Sin((float)(lPhase * Math.PI * 2)) * 0.8f + Mathf.Sin((float)(lPhase * Math.PI * 4)) * 0.2f;
                float lEnv = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * pDecay);
                lData[i] = lWave * lEnv * pVolume;
            }
            return Fade(lData);
        }

        private static float[] Siren(float pSeconds, float pLow, float pHigh, float pRate, float pVolume)
        {
            float[] lData = new float[(int)(pSeconds * RATE)];
            double lPhase = 0;
            for (int i = 0; i < lData.Length; i++)
            {
                float t = (float)i / RATE;
                float lFreq = Mathf.Lerp(pLow, pHigh, 0.5f + 0.5f * Mathf.Sin(t * pRate * Mathf.PI * 2f));
                lPhase += lFreq / RATE;
                float lWave = Mathf.Sign(Mathf.Sin((float)(lPhase * Math.PI * 2))) * 0.3f + Mathf.Sin((float)(lPhase * Math.PI * 2)) * 0.5f;
                lData[i] = lWave * pVolume * Mathf.Min(1f, t * 20f) * Mathf.Min(1f, (pSeconds - t) * 5f);
            }
            return lData;
        }

        private static float[] Bells(float pSeconds, float pVolume)
        {
            float[] lA = Tone(pSeconds, 2200f, 2150f, pVolume, 3f);
            float[] lB = Delay(Tone(pSeconds * 0.8f, 3300f, 3250f, pVolume * 0.7f, 4f), 0.05f);
            float[] lC = Delay(Tone(pSeconds * 0.7f, 2750f, 2700f, pVolume * 0.6f, 5f), 0.11f);
            return Mix(lA, lB, lC);
        }

        private static float[] Delay(float[] pData, float pSeconds)
        {
            int lOffset = (int)(pSeconds * RATE);
            float[] lResult = new float[pData.Length + lOffset];
            Array.Copy(pData, 0, lResult, lOffset, pData.Length);
            return lResult;
        }

        private static float[] Mix(params float[][] pLayers)
        {
            int lLength = 0;
            foreach (float[] lLayer in pLayers) lLength = Mathf.Max(lLength, lLayer.Length);
            float[] lResult = new float[lLength];
            foreach (float[] lLayer in pLayers) for (int i = 0; i < lLayer.Length; i++) lResult[i] += lLayer[i];
            return lResult;
        }

        private static float[] Fade(float[] pData)
        {
            int lFade = Mathf.Min(400, pData.Length / 4);
            for (int i = 0; i < lFade; i++) pData[pData.Length - 1 - i] *= (float)i / lFade;
            return pData;
        }

        private static void Write(string pName, float[] pData)
        {
            float lPeak = 0.0001f;
            foreach (float lSample in pData) lPeak = Mathf.Max(lPeak, Mathf.Abs(lSample));
            float lGain = lPeak > 0.95f ? 0.95f / lPeak : 1f;

            string lPath = Path.Combine(Application.dataPath, "..", DIR, pName + ".wav");
            using FileStream lStream = new FileStream(lPath, FileMode.Create);
            using BinaryWriter lWriter = new BinaryWriter(lStream);
            int lBytes = pData.Length * 2;
            lWriter.Write(new[] { 'R', 'I', 'F', 'F' });
            lWriter.Write(36 + lBytes);
            lWriter.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            lWriter.Write(16);
            lWriter.Write((short)1);
            lWriter.Write((short)1);
            lWriter.Write(RATE);
            lWriter.Write(RATE * 2);
            lWriter.Write((short)2);
            lWriter.Write((short)16);
            lWriter.Write(new[] { 'd', 'a', 't', 'a' });
            lWriter.Write(lBytes);
            foreach (float lSample in pData) lWriter.Write((short)(Mathf.Clamp(lSample * lGain, -1f, 1f) * 32767f));
        }
    }
}
