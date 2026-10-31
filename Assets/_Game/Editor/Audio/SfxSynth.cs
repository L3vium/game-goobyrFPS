using System;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// The building blocks of the synthesised sound: coloured noises, biquad filters (RBJ),
    /// envelopes, a small Schroeder reverb for tails and rooms, soft clipping, and seamless loops.
    /// Mono buffers of floats at <see cref="RATE"/>.
    /// </summary>
    public static class SfxSynth
    {
        public const int RATE = 44100;
        public static System.Random Random = new System.Random(4321);

        public static float Rand() => (float)Random.NextDouble() * 2f - 1f;
        public static float Range(float pMin, float pMax) => pMin + (float)Random.NextDouble() * (pMax - pMin);
        public static int Samples(float pSeconds) => Mathf.Max(1, (int)(pSeconds * RATE));

        public static float[] White(float pSeconds)
        {
            float[] lData = new float[Samples(pSeconds)];
            for (int i = 0; i < lData.Length; i++) lData[i] = Rand();
            return lData;
        }

        /// <summary>Brown noise (rumbles, wind, water bodies).</summary>
        public static float[] Brown(float pSeconds)
        {
            float[] lData = new float[Samples(pSeconds)];
            float lLast = 0f;
            for (int i = 0; i < lData.Length; i++)
            {
                lLast = Mathf.Clamp(lLast + Rand() * 0.04f, -1f, 1f) * 0.998f;
                lData[i] = lLast * 3f;
            }
            return lData;
        }

        public static float[] Sine(float pSeconds, float pFrom, float pTo, float pDecay)
        {
            float[] lData = new float[Samples(pSeconds)];
            double lPhase = 0;
            for (int i = 0; i < lData.Length; i++)
            {
                float t = (float)i / RATE;
                lPhase += Mathf.Lerp(pFrom, pTo, Mathf.Clamp01(t / pSeconds)) / RATE;
                lData[i] = Mathf.Sin((float)(lPhase * Math.PI * 2)) * Mathf.Exp(-t * pDecay) * Mathf.Min(1f, t * 2000f);
            }
            return lData;
        }

        /// <summary>Bright saw for synth stingers (band-limited enough once low-passed).</summary>
        public static float[] Saw(float pSeconds, float pFrom, float pTo)
        {
            float[] lData = new float[Samples(pSeconds)];
            double lPhase = 0;
            for (int i = 0; i < lData.Length; i++)
            {
                lPhase += Mathf.Lerp(pFrom, pTo, (float)i / lData.Length) / RATE;
                lData[i] = (float)(lPhase - Math.Floor(lPhase)) * 2f - 1f;
            }
            return lData;
        }

        // ---------------------------------------------------------------- filters (RBJ biquads)

        private static float[] Biquad(float[] pData, double b0, double b1, double b2, double a0, double a1, double a2)
        {
            float[] lOut = new float[pData.Length];
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < pData.Length; i++)
            {
                double x = pData[i];
                double y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                lOut[i] = (float)y;
            }
            return lOut;
        }

        public static float[] Lowpass(float[] pData, float pCutoff, float pQ = 0.707f)
        {
            double w = 2 * Math.PI * Mathf.Clamp(pCutoff, 20f, RATE * 0.45f) / RATE, c = Math.Cos(w), a = Math.Sin(w) / (2 * pQ);
            return Biquad(pData, (1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
        }

        public static float[] Highpass(float[] pData, float pCutoff, float pQ = 0.707f)
        {
            double w = 2 * Math.PI * Mathf.Clamp(pCutoff, 20f, RATE * 0.45f) / RATE, c = Math.Cos(w), a = Math.Sin(w) / (2 * pQ);
            return Biquad(pData, (1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
        }

        public static float[] Bandpass(float[] pData, float pCenter, float pQ)
        {
            double w = 2 * Math.PI * Mathf.Clamp(pCenter, 20f, RATE * 0.45f) / RATE, c = Math.Cos(w), a = Math.Sin(w) / (2 * pQ);
            return Biquad(pData, a, 0, -a, 1 + a, -2 * c, 1 - a);
        }

        // ---------------------------------------------------------------- shaping

        /// <summary>Linear attack then exponential decay.</summary>
        public static float[] Env(float[] pData, float pAttack, float pDecay)
        {
            for (int i = 0; i < pData.Length; i++)
            {
                float t = (float)i / RATE;
                pData[i] *= (pAttack > 0f ? Mathf.Min(1f, t / pAttack) : 1f) * Mathf.Exp(-Mathf.Max(0f, t - pAttack) * pDecay);
            }
            return pData;
        }

        /// <summary>Rise and fall over the whole buffer (swells, gusts).</summary>
        public static float[] Swell(float[] pData, float pPower = 1f)
        {
            for (int i = 0; i < pData.Length; i++) pData[i] *= Mathf.Pow(Mathf.Sin((float)i / pData.Length * Mathf.PI), pPower);
            return pData;
        }

        public static float[] Gain(float[] pData, float pGain)
        {
            for (int i = 0; i < pData.Length; i++) pData[i] *= pGain;
            return pData;
        }

        /// <summary>Soft clipping: weight and grit on bodies and blasts.</summary>
        public static float[] Drive(float[] pData, float pDrive)
        {
            float lNorm = 1f / (float)Math.Tanh(pDrive);
            for (int i = 0; i < pData.Length; i++) pData[i] = (float)Math.Tanh(pData[i] * pDrive) * lNorm;
            return pData;
        }

        public static float[] Delay(float[] pData, float pSeconds)
        {
            int lOffset = Samples(pSeconds);
            float[] lResult = new float[pData.Length + lOffset];
            Array.Copy(pData, 0, lResult, lOffset, pData.Length);
            return lResult;
        }

        public static float[] Mix(params float[][] pLayers)
        {
            int lLength = 0;
            foreach (float[] lLayer in pLayers) lLength = Mathf.Max(lLength, lLayer.Length);
            float[] lResult = new float[lLength];
            foreach (float[] lLayer in pLayers) for (int i = 0; i < lLayer.Length; i++) lResult[i] += lLayer[i];
            return lResult;
        }

        public static float[] Pad(float[] pData, float pSeconds)
        {
            float[] lResult = new float[pData.Length + Samples(pSeconds)];
            Array.Copy(pData, lResult, pData.Length);
            return lResult;
        }

        /// <summary>
        /// A small Schroeder reverb (four damped combs into two all-passes). <paramref name="pSize"/>
        /// 0..1 from a booth to a valley, <paramref name="pWet"/> how much tail is added.
        /// </summary>
        public static float[] Reverb(float[] pData, float pSize, float pDamp, float pWet, float pTail)
        {
            float[] lIn = Pad(pData, pTail);
            float[] lOut = new float[lIn.Length];
            int[] lCombs = { 1116, 1188, 1277, 1356 };
            float lFeedback = Mathf.Lerp(0.7f, 0.92f, pSize);
            foreach (int lBase in lCombs)
            {
                int lLength = (int)(lBase * Mathf.Lerp(0.6f, 2.2f, pSize));
                float[] lBuffer = new float[lLength];
                float lStore = 0f;
                for (int i = 0, p = 0; i < lIn.Length; i++, p = (p + 1) % lLength)
                {
                    float lDelayed = lBuffer[p];
                    lStore = lDelayed * (1f - pDamp) + lStore * pDamp;
                    lBuffer[p] = lIn[i] + lStore * lFeedback;
                    lOut[i] += lDelayed * 0.25f;
                }
            }
            foreach (int lLength in new[] { 556, 441 })
            {
                float[] lBuffer = new float[lLength];
                for (int i = 0, p = 0; i < lOut.Length; i++, p = (p + 1) % lLength)
                {
                    float lDelayed = lBuffer[p];
                    float lInput = lOut[i];
                    lBuffer[p] = lInput + lDelayed * 0.5f;
                    lOut[i] = lDelayed - lInput * 0.5f;
                }
            }
            for (int i = 0; i < lIn.Length; i++) lOut[i] = lIn[i] + lOut[i] * pWet;
            return lOut;
        }

        /// <summary>Random clicks (gravel, crackles, bubbles): <paramref name="pCount"/> impulses over the buffer, each ringing at its own pitch.</summary>
        public static float[] Grains(float pSeconds, int pCount, float pLow, float pHigh, float pGrainDecay)
        {
            float[] lData = new float[Samples(pSeconds)];
            for (int g = 0; g < pCount; g++)
            {
                int lStart = (int)(Random.NextDouble() * lData.Length * 0.9f);
                float lFreq = Range(pLow, pHigh), lAmp = Range(0.3f, 1f);
                for (int i = 0; i < Samples(0.03f) && lStart + i < lData.Length; i++)
                {
                    float t = (float)i / RATE;
                    lData[lStart + i] += Mathf.Sin(t * lFreq * Mathf.PI * 2f) * Mathf.Exp(-t * pGrainDecay) * lAmp;
                }
            }
            return lData;
        }

        /// <summary>Makes a buffer loop seamlessly by crossfading its end into its start.</summary>
        public static float[] Seamless(float[] pData, float pFade)
        {
            int lFade = Mathf.Min(Samples(pFade), pData.Length / 3);
            float[] lOut = new float[pData.Length - lFade];
            Array.Copy(pData, lOut, lOut.Length);
            for (int i = 0; i < lFade; i++)
            {
                float t = (float)i / lFade;
                lOut[i] = lOut[i] * t + pData[pData.Length - lFade + i] * (1f - t);
            }
            return lOut;
        }

        /// <summary>Slow random amplitude movement (wind, crowds), values 0..1, smoothed.</summary>
        public static float[] Lfo(float[] pData, float pRate, float pDepth)
        {
            float lValue = 0.5f, lTarget = 0.5f;
            int lStep = Mathf.Max(1, (int)(RATE / Mathf.Max(0.01f, pRate)));
            for (int i = 0; i < pData.Length; i++)
            {
                if (i % lStep == 0) lTarget = (float)Random.NextDouble();
                lValue += (lTarget - lValue) / lStep * 3f;
                pData[i] *= 1f - pDepth + pDepth * lValue;
            }
            return pData;
        }
    }
}
