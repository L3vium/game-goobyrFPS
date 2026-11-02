using System;
using System.Collections.Generic;
using System.IO;
using Com.IsartDigital.GoobyrFPS.AI;
using UnityEditor;
using UnityEngine;
using static Com.IsartDigital.GoobyrFPS.EditorTools.SfxSynth;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// ===== AI BARKS (Track D block) =====
    /// <para>
    /// The NPCs' gibberish voices, TABS-flavoured: every line is made of syllables - a consonant
    /// (a breath, a burst, a hiss, a hum) and a vowel sung by a glottal buzz through three formant
    /// filters - with an exaggerated pitch contour ("huh?" rises, "clear" falls, a scream wobbles).
    /// Four voices (<see cref="NpcRules.VOICES"/>: low, mid, high, squeak), two or three takes each,
    /// written at 22 kHz as <c>bark_&lt;voice&gt;_&lt;kind&gt;[_n]</c> (<see cref="Npc"/>.BarkRpc plays them).
    /// </para>
    /// </summary>
    public static partial class SfxBuilder
    {
        private const int BARK_RATE = 22050;
        private static System.Random _bark;

        /// <summary>A vowel's first three formants (Hz) for an average adult voice.</summary>
        private static readonly Dictionary<char, Vector3> FORMANTS = new Dictionary<char, Vector3>
        {
            { 'a', new Vector3(800f, 1200f, 2700f) },
            { 'e', new Vector3(450f, 1850f, 2600f) },
            { 'i', new Vector3(300f, 2250f, 3000f) },
            { 'o', new Vector3(480f, 850f, 2600f) },
            { 'u', new Vector3(330f, 750f, 2400f) },
            { '@', new Vector3(550f, 1450f, 2500f) }
        };

        /// <summary>Each voice: base pitch (Hz) and formant scale (a smaller throat sounds brighter).</summary>
        private static readonly Dictionary<string, Vector2> VOICE = new Dictionary<string, Vector2>
        {
            { "low", new Vector2(92f, 0.9f) },
            { "mid", new Vector2(128f, 1f) },
            { "high", new Vector2(205f, 1.14f) },
            { "squeak", new Vector2(300f, 1.3f) }
        };

        /// <summary>One syllable: onset consonant, vowel (and the vowel it glides to), length, pitch from/to (x the voice's), loudness.</summary>
        private struct Syl
        {
            public char onset, vowel, glide;
            public float seconds, from, to, volume;
            public Syl(char pOnset, char pVowel, float pSeconds, float pFrom, float pTo, float pVolume = 1f, char pGlide = ' ')
            {
                onset = pOnset; vowel = pVowel; glide = pGlide; seconds = pSeconds; from = pFrom; to = pTo; volume = pVolume;
            }
        }

        [MenuItem("Goobyr/Build/Barks")]
        public static void BuildBarks()
        {
            EditorAssets.EnsureFolder(DIR);
            BarkSet();
            AssetDatabase.Refresh();
            Debug.Log("[Build] barks");
        }

        private static void BarkSet()
        {
            _bark = new System.Random(5150);
            foreach (string lVoice in NpcRules.VOICES)
            {
                Vector2 v = VOICE[lVoice];
                for (int take = 0; take < 2; take++)
                {
                    float j = take == 0 ? 1f : 1.06f;
                    WriteBark(lVoice, Barks.Huh, take, Word(v, new Syl('h', '@', 0.32f, 1f * j, 1.55f * j)));
                    WriteBark(lVoice, Barks.Hey, take, Word(v, new Syl('h', 'e', 0.4f, 1.3f * j, 1.05f * j, 1f, 'i')));
                    WriteBark(lVoice, Barks.OverThere, take, Word(v, new Syl(' ', 'o', 0.13f, 1.1f * j, 1.18f * j, 0.8f), new Syl('b', 'a', 0.12f, 1.15f * j, 1.05f * j, 0.8f), new Syl('d', 'e', 0.34f, 1.45f * j, 1.1f * j, 1f, 'i')));
                    WriteBark(lVoice, Barks.Help, take, Word(v, 6f, 0.05f, new Syl('h', 'e', 0.16f, 1.55f * j, 1.7f * j), new Syl('l', 'a', 0.3f, 1.8f * j, 1.45f * j, 1f, 'u')));
                    WriteBark(lVoice, Barks.Scream, take, Scream(v, j));
                    WriteBark(lVoice, Barks.Clear, take, Word(v, new Syl('k', 'i', 0.2f, 1.08f * j, 1f * j, 0.8f, 'e'), new Syl(' ', 'a', 0.24f, 0.95f * j, 0.78f * j, 0.7f)));
                    WriteBark(lVoice, Barks.Radio, take, Radio(v));
                }
                for (int take = 0; take < 3; take++) WriteBark(lVoice, Barks.Chatter, take, Chatter(v));
            }
        }

        // ---------------------------------------------------------------- lines

        private static float[] Word(Vector2 pVoice, params Syl[] pSyllables) => Word(pVoice, 5f, 0.02f, pSyllables);

        /// <summary>Syllables one after the other (a hair of overlap), vibrato at <paramref name="pRate"/> Hz, depth <paramref name="pDepth"/>.</summary>
        private static float[] Word(Vector2 pVoice, float pRate, float pDepth, params Syl[] pSyllables)
        {
            List<float[]> lParts = new List<float[]>();
            float lAt = 0f;
            foreach (Syl s in pSyllables)
            {
                lParts.Add(Delay(Syllable(pVoice, s, pRate, pDepth), lAt));
                lAt += s.seconds + (s.onset != ' ' ? 0.05f : 0f) - 0.015f;
            }
            return Reverb(Mix(lParts.ToArray()), 0.25f, 0.6f, 0.12f, 0.25f);
        }

        /// <summary>A long, wobbling, goofy "aaaah!" that cracks at the end.</summary>
        private static float[] Scream(Vector2 pVoice, float pJitter)
        {
            Syl lAh = new Syl(' ', 'a', 1.05f, 2.7f * pJitter, 2.05f * pJitter, 1f);
            float[] lCry = Syllable(pVoice, lAh, 7.5f, 0.07f);
            // The wobble: the voice trembles in loudness, a breathy rasp under it.
            for (int i = 0; i < lCry.Length; i++)
            {
                float t = (float)i / RATE;
                lCry[i] *= 0.78f + 0.22f * Mathf.Sin(t * 9f * Mathf.PI * 2f);
            }
            float[] lRasp = Env(Bandpass(White(1.05f), 2600f * pVoice.y, 1.2f), 0.04f, 1.5f);
            return Reverb(Drive(Mix(lCry, Gain(lRasp, 0.18f)), 1.4f), 0.35f, 0.5f, 0.2f, 0.4f);
        }

        /// <summary>Quick gibberish through a walkie-talkie: band-limited, static, a click at each end.</summary>
        private static float[] Radio(Vector2 pVoice)
        {
            List<Syl> lWords = new List<Syl>();
            int lCount = 5 + _bark.Next(3);
            for (int i = 0; i < lCount; i++)
            {
                float lPitch = 1f + (float)(_bark.NextDouble() - 0.5) * 0.25f;
                lWords.Add(new Syl(Consonant(), Vowel(), 0.07f + (float)_bark.NextDouble() * 0.05f, lPitch, lPitch * 0.96f, 0.85f));
            }
            float[] lVoice = Word(pVoice, 5f, 0.02f, lWords.ToArray());
            lVoice = Highpass(Lowpass(lVoice, 2800f, 1.1f), 420f, 1.1f);
            float lLength = (float)lVoice.Length / RATE + 0.1f;
            float[] lStatic = Gain(Highpass(White(lLength), 1800f), 0.07f);
            float[] lClickIn = Gain(Env(Bandpass(White(0.03f), 2500f, 2f), 0f, 120f), 0.6f);
            float[] lClickOut = Delay(Gain(Env(Bandpass(White(0.05f), 1800f, 1.5f), 0f, 70f), 0.5f), lLength - 0.05f);
            return Drive(Mix(Delay(lVoice, 0.04f), lStatic, lClickIn, lClickOut), 1.6f);
        }

        /// <summary>Small talk: three to six syllables with a lazy up-and-down melody, softer.</summary>
        private static float[] Chatter(Vector2 pVoice)
        {
            List<Syl> lWords = new List<Syl>();
            int lCount = 3 + _bark.Next(4);
            float lUp = (float)_bark.NextDouble() < 0.5f ? 1f : -1f;
            for (int i = 0; i < lCount; i++)
            {
                float u = (float)i / Mathf.Max(1, lCount - 1);
                float lPitch = 1f + lUp * 0.18f * Mathf.Sin(u * Mathf.PI) + (float)(_bark.NextDouble() - 0.5) * 0.08f;
                lWords.Add(new Syl(Consonant(), Vowel(), 0.09f + (float)_bark.NextDouble() * 0.07f, lPitch, lPitch * (i == lCount - 1 ? 0.88f : 1f), 0.75f));
            }
            return Word(pVoice, 4.5f, 0.015f, lWords.ToArray());
        }

        private static char Consonant() => " bdgmnlwhkt"[_bark.Next(11)];
        private static char Vowel() => "aeiou@"[_bark.Next(6)];

        // ---------------------------------------------------------------- the voice

        /// <summary>
        /// One syllable: its onset consonant, then the vowel - a buzzing glottal source (saw and sine,
        /// jittered, with vibrato) through the vowel's formants, gliding to a second vowel if asked.
        /// </summary>
        private static float[] Syllable(Vector2 pVoice, Syl s, float pRate, float pDepth)
        {
            float lOnset = s.onset == ' ' ? 0f : 0.05f;
            float[] lSource = Glottal(s.seconds, pVoice.x * s.from, pVoice.x * s.to, pRate, pDepth);
            float[] lVowel = Formants(lSource, FORMANTS[s.vowel] * pVoice.y);
            if (s.glide != ' ')
            {
                float[] lGlide = Formants(lSource, FORMANTS[s.glide] * pVoice.y);
                for (int i = 0; i < lVowel.Length; i++)
                {
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, (float)i / lVowel.Length));
                    lVowel[i] = Mathf.Lerp(lVowel[i], lGlide[i], u);
                }
            }
            // Loudness: a quick rise, a held middle, a soft end.
            for (int i = 0; i < lVowel.Length; i++)
            {
                float t = (float)i / RATE;
                lVowel[i] *= Mathf.Min(1f, t / 0.018f) * Mathf.Min(1f, (s.seconds - t) / 0.06f) * s.volume;
            }
            return Mix(Gain(Onset(s.onset, pVoice), s.volume), Delay(lVowel, lOnset));
        }

        /// <summary>The vocal folds' buzz: a saw and its sine, a little jitter, vibrato, a pitch glide.</summary>
        private static float[] Glottal(float pSeconds, float pFrom, float pTo, float pRate, float pDepth)
        {
            float[] lData = new float[Samples(pSeconds)];
            double lPhase = 0;
            float lJitter = 0f;
            for (int i = 0; i < lData.Length; i++)
            {
                float t = (float)i / RATE;
                if (i % 220 == 0) lJitter = (float)(_bark.NextDouble() - 0.5) * 0.02f;
                float f = Mathf.Lerp(pFrom, pTo, Mathf.SmoothStep(0f, 1f, t / pSeconds)) * (1f + pDepth * Mathf.Sin(t * pRate * Mathf.PI * 2f) + lJitter);
                lPhase += f / RATE;
                float lSaw = (float)(lPhase - Math.Floor(lPhase)) * 2f - 1f;
                lData[i] = lSaw * 0.65f + Mathf.Sin((float)(lPhase * Math.PI * 2)) * 0.35f;
            }
            return lData;
        }

        /// <summary>The vowel: the source through three parallel formant band-passes (F1 loud, F3 faint).</summary>
        private static float[] Formants(float[] pSource, Vector3 pFormants)
        {
            float[] f1 = Gain(Bandpass((float[])pSource.Clone(), pFormants.x, 5f), 1f);
            float[] f2 = Gain(Bandpass((float[])pSource.Clone(), pFormants.y, 8f), 0.55f);
            float[] f3 = Gain(Bandpass((float[])pSource.Clone(), pFormants.z, 10f), 0.22f);
            return Mix(f1, f2, f3);
        }

        /// <summary>The consonant before a vowel: a breath (h), a burst (k t d b g), a hum (m n l w).</summary>
        private static float[] Onset(char pOnset, Vector2 pVoice)
        {
            switch (pOnset)
            {
                case 'h': return Gain(Env(Bandpass(White(0.07f), 1600f * pVoice.y, 0.8f), 0.02f, 25f), 0.25f);
                case 'k': return Gain(Env(Bandpass(White(0.03f), 2200f, 1.5f), 0f, 90f), 0.5f);
                case 't': return Gain(Env(Bandpass(White(0.03f), 4200f, 1.5f), 0f, 110f), 0.45f);
                case 'd': return Gain(Env(Bandpass(White(0.025f), 2800f, 1.2f), 0f, 140f), 0.35f);
                case 'b': return Gain(Env(Lowpass(White(0.02f), 900f), 0f, 160f), 0.5f);
                case 'g': return Gain(Env(Bandpass(White(0.025f), 1500f, 1.2f), 0f, 120f), 0.4f);
                case 'm':
                case 'n':
                case 'l':
                case 'w':
                    float[] lHum = Lowpass(Glottal(0.06f, pVoice.x, pVoice.x, 5f, 0.01f), pOnset == 'l' ? 1200f : 450f);
                    return Gain(Env(lHum, 0.01f, 8f), 0.35f);
                default: return new float[1];
            }
        }

        // ---------------------------------------------------------------- output

        /// <summary>Writes a bark take at 22 kHz (low-passed, decimated, normalised).</summary>
        private static void WriteBark(string pVoice, Barks pKind, int pTake, float[] pData)
        {
            string lName = "bark_" + pVoice + "_" + pKind.ToString().ToLowerInvariant() + (pTake == 0 ? "" : "_" + pTake);
            float[] lSmooth = Lowpass(pData, 9000f);
            float[] lHalf = new float[lSmooth.Length / 2];
            for (int i = 0; i < lHalf.Length; i++) lHalf[i] = (lSmooth[i * 2] + lSmooth[i * 2 + 1]) * 0.5f;
            float lPeak = 0.0001f;
            foreach (float lSample in lHalf) lPeak = Mathf.Max(lPeak, Mathf.Abs(lSample));
            float lGain = 0.9f / lPeak;
            int lFade = Mathf.Min(300, lHalf.Length / 4);
            for (int i = 0; i < lFade; i++) lHalf[lHalf.Length - 1 - i] *= (float)i / lFade;

            string lPath = Path.Combine(Application.dataPath, "..", DIR, lName + ".wav");
            using FileStream lStream = new FileStream(lPath, FileMode.Create);
            using BinaryWriter lWriter = new BinaryWriter(lStream);
            int lBytes = lHalf.Length * 2;
            lWriter.Write(new[] { 'R', 'I', 'F', 'F' });
            lWriter.Write(36 + lBytes);
            lWriter.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            lWriter.Write(16);
            lWriter.Write((short)1);
            lWriter.Write((short)1);
            lWriter.Write(BARK_RATE);
            lWriter.Write(BARK_RATE * 2);
            lWriter.Write((short)2);
            lWriter.Write((short)16);
            lWriter.Write(new[] { 'd', 'a', 't', 'a' });
            lWriter.Write(lBytes);
            foreach (float lSample in lHalf) lWriter.Write((short)(Mathf.Clamp(lSample * lGain, -1f, 1f) * 32767f));
        }
    }
}
