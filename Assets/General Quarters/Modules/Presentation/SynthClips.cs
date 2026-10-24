using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Presentation
{
    /// <summary>
    /// Stand-in sounds synthesised in code (G32), for every slot that has no recording: filtered noise for reports, booms
    /// and splashes, a pulse train for the CIWS, a struck two-partial tone for the general-quarters alarm. Each is built
    /// once from a fixed seed, so it is the same every run. They are honest stand-ins, not recordings; drop a recording
    /// into the slot and it wins.
    /// </summary>
    public static class SynthClips
    {
        public const int SampleRate = 44100;
        static readonly Dictionary<SynthSound, AudioClip> s_Cache = new Dictionary<SynthSound, AudioClip>();

        /// <summary>The clip for <paramref name="sound"/>, built on first use. Null for None.</summary>
        public static AudioClip Get(SynthSound sound)
        {
            if (sound == SynthSound.None) return null;
            if (s_Cache.TryGetValue(sound, out AudioClip clip) && clip != null) return clip;
            float[] data = Samples(sound);
            clip = AudioClip.Create($"GQ synth {sound}", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            s_Cache[sound] = clip;
            return clip;
        }

        /// <summary>The samples for <paramref name="sound"/> (mono, <see cref="SampleRate"/> Hz, peak ≤ 1). Pure and seeded.</summary>
        public static float[] Samples(SynthSound sound)
        {
            var rng = new System.Random(1000 + (int)sound);
            switch (sound)
            {
                case SynthSound.Mk45Report:
                    return Normalise(Mix(Crack(rng, 1.6f, 0.012f, 0.9f), Boom(rng, 1.6f, 0.35f, 260f, 1f), Boom(rng, 1.6f, 0.9f, 90f, 0.5f)));
                case SynthSound.CiwsBurst:
                    return Normalise(Burst(rng, 1.4f, 75f, 0.004f, 1.33f));
                case SynthSound.Mk38Burst:
                {
                    float[] o = new float[Len(3.6f)];
                    for (int k = 0; k < 10; k++)
                        AddAt(o, Mix(Crack(rng, 0.5f, 0.006f, 0.8f), Boom(rng, 0.5f, 0.12f, 400f, 0.7f)), Len(k * 0.333f));
                    return Normalise(o);
                }
                case SynthSound.Airburst:
                    return Normalise(Mix(Boom(rng, 1.4f, 0.25f, 300f, 1f), Crack(rng, 1.4f, 0.008f, 0.3f)));
                case SynthSound.Splash:
                    return Normalise(Hiss(rng, 1.6f, 0.03f, 0.55f, 0.35f));
                case SynthSound.ShipHit:
                    return Normalise(Mix(Crack(rng, 3.2f, 0.02f, 0.8f), Boom(rng, 3.2f, 0.8f, 160f, 1f), Hiss(rng, 3.2f, 0.05f, 1.2f, 0.25f)));
                case SynthSound.Alarm:
                {
                    // General quarters: a struck alarm, twice a second.
                    float[] o = new float[Len(6f)];
                    for (int k = 0; k < 11; k++) AddAt(o, Strike(0.45f, 440f, 880f, 0.22f), Len(k * 0.5f));
                    return Normalise(o);
                }
                case SynthSound.MissileLaunch:
                    return Normalise(Mix(Boom(rng, 3f, 0.6f, 220f, 0.8f), Hiss(rng, 3f, 0.05f, 1.8f, 1f)));
                case SynthSound.Detonation:
                    return Normalise(Mix(Crack(rng, 2.5f, 0.015f, 1f), Boom(rng, 2.5f, 0.6f, 200f, 1f)));
                default:
                    return new float[1];
            }
        }

        static int Len(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));

        /// <summary>A sharp onset: white noise over <paramref name="decayS"/>.</summary>
        static float[] Crack(System.Random rng, float lengthS, float decayS, float gain)
        {
            float[] o = new float[Len(lengthS)];
            for (int i = 0; i < o.Length; i++) o[i] = gain * Noise(rng) * Mathf.Exp(-i / (decayS * SampleRate));
            return o;
        }

        /// <summary>A low boom: noise low-passed at <paramref name="cutoffHz"/>, decaying over <paramref name="decayS"/>.</summary>
        static float[] Boom(System.Random rng, float lengthS, float decayS, float cutoffHz, float gain)
        {
            float[] o = new float[Len(lengthS)];
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / SampleRate), y = 0f, y2 = 0f;
            for (int i = 0; i < o.Length; i++)
            {
                y += a * (Noise(rng) - y);
                y2 += a * (y - y2);   // two poles, so the boom has no hiss
                float attack = Mathf.Clamp01(i / (0.004f * SampleRate));
                o[i] = gain * 6f * y2 * attack * Mathf.Exp(-i / (decayS * SampleRate));
            }
            return o;
        }

        /// <summary>Broadband hiss with an attack and a decay (spray, rocket roar).</summary>
        static float[] Hiss(System.Random rng, float lengthS, float attackS, float decayS, float gain)
        {
            float[] o = new float[Len(lengthS)];
            float y = 0f, a = 1f - Mathf.Exp(-2f * Mathf.PI * 3000f / SampleRate);
            for (int i = 0; i < o.Length; i++)
            {
                y += a * (Noise(rng) - y);
                float t = (float)i / SampleRate;
                o[i] = gain * y * Mathf.Clamp01(t / attackS) * Mathf.Exp(-Mathf.Max(0f, t - attackS) / decayS);
            }
            return o;
        }

        /// <summary>A burst of rounds at <paramref name="rateHz"/>: short filtered clicks, with a ramp in and out.</summary>
        static float[] Burst(System.Random rng, float lengthS, float rateHz, float clickS, float burstS)
        {
            float[] o = new float[Len(lengthS)];
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * 900f / SampleRate), y = 0f;
            int period = Mathf.RoundToInt(SampleRate / rateHz), click = Len(clickS), burst = Len(burstS);
            for (int i = 0; i < o.Length; i++)
            {
                int phase = i % period;
                float x = i < burst && phase < click ? Noise(rng) * (1f - (float)phase / click) : 0f;
                y += a * (x - y);
                float env = Mathf.Clamp01(i / (0.02f * SampleRate)) * (i < burst ? 1f : Mathf.Exp(-(i - burst) / (0.08f * SampleRate)));
                o[i] = 3f * y * env;
            }
            return o;
        }

        /// <summary>A struck tone: two partials, fast attack, exponential ring.</summary>
        static float[] Strike(float lengthS, float f1, float f2, float decayS)
        {
            float[] o = new float[Len(lengthS)];
            for (int i = 0; i < o.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t / decayS);
                o[i] = env * (0.7f * Mathf.Sin(2f * Mathf.PI * f1 * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f2 * t));
            }
            return o;
        }

        static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        static float[] Mix(params float[][] parts)
        {
            int n = 0;
            foreach (float[] p in parts) n = Math.Max(n, p.Length);
            float[] o = new float[n];
            foreach (float[] p in parts) for (int i = 0; i < p.Length; i++) o[i] += p[i];
            return o;
        }

        static void AddAt(float[] into, float[] part, int offset)
        {
            for (int i = 0; i < part.Length && offset + i < into.Length; i++) into[offset + i] += part[i];
        }

        static float[] Normalise(float[] o)
        {
            float peak = 1e-6f;
            foreach (float s in o) peak = Mathf.Max(peak, Mathf.Abs(s));
            float k = 0.95f / peak;
            for (int i = 0; i < o.Length; i++) o[i] *= k;
            return o;
        }
    }
}
