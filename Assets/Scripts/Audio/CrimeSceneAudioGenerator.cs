using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace CrimeSceneVR.Audio
{
    public static class CrimeSceneAudioGenerator
    {
        private const int SAMPLE_RATE = 44100;

        public static void GenerateAudioAssets()
        {
            string dir = Path.Combine(Application.dataPath, "Audio");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 1. Generate Ambient Room Tone (3.0s loopable subtle low-pass atmospheric presence)
            float[] roomTone = GenerateRoomTone(3.0f);
            SaveWav(Path.Combine(dir, "Ambience_RoomTone.wav"), roomTone, SAMPLE_RATE);

            // 2. Generate Clock Tick (1.0s loop with subtle tick at t=0)
            float[] clockTick = GenerateClockTick(1.0f);
            SaveWav(Path.Combine(dir, "SFX_ClockTick.wav"), clockTick, SAMPLE_RATE);

            // 3. Generate Footstep 1 (0.2s soft wooden floor tap)
            float[] step1 = GenerateFootstep(0.2f, 180f, 0.035f);
            SaveWav(Path.Combine(dir, "SFX_Footstep1.wav"), step1, SAMPLE_RATE);

            // 4. Generate Footstep 2 (0.2s slight variation)
            float[] step2 = GenerateFootstep(0.2f, 210f, 0.030f);
            SaveWav(Path.Combine(dir, "SFX_Footstep2.wav"), step2, SAMPLE_RATE);

            AssetDatabase.Refresh();
        }

        private static float[] GenerateRoomTone(float duration)
        {
            int numSamples = (int)(SAMPLE_RATE * duration);
            float[] samples = new float[numSamples];
            System.Random rand = new System.Random(42);

            // Gentle filtered noise + subtle 60Hz/120Hz warmth
            float lastSample = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                // Brown/pink noise filter
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                lastSample = (lastSample + (0.02f * white)) / 1.02f;

                // Subtle warm harmonics (air tone)
                float hum = 0.015f * Mathf.Sin(2f * Mathf.PI * 55f * t)
                          + 0.010f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                          + 0.005f * Mathf.Sin(2f * Mathf.PI * 220f * t);

                float combined = (lastSample * 0.15f + hum) * 0.4f;

                // Crossfade ends for seamless loop
                float fadeLen = 0.1f * SAMPLE_RATE;
                if (i < fadeLen) combined *= (i / fadeLen);
                else if (i > numSamples - fadeLen) combined *= ((numSamples - i) / fadeLen);

                samples[i] = Mathf.Clamp(combined, -1f, 1f);
            }
            return samples;
        }

        private static float[] GenerateClockTick(float duration)
        {
            int numSamples = (int)(SAMPLE_RATE * duration);
            float[] samples = new float[numSamples];

            // A crisp mechanical tick at the start, then quiet
            int tickLen = (int)(0.03f * SAMPLE_RATE); // 30ms
            for (int i = 0; i < numSamples; i++)
            {
                if (i < tickLen)
                {
                    float t = (float)i / SAMPLE_RATE;
                    float decay = Mathf.Exp(-t * 120f);
                    float osc = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.6f
                              + Mathf.Sin(2f * Mathf.PI * 2800f * t) * 0.3f
                              + Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.4f;
                    samples[i] = osc * decay * 0.45f;
                }
                else
                {
                    samples[i] = 0f;
                }
            }
            return samples;
        }

        private static float[] GenerateFootstep(float duration, float baseFreq, float decayRate)
        {
            int numSamples = (int)(SAMPLE_RATE * duration);
            float[] samples = new float[numSamples];
            System.Random rand = new System.Random(101);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float decay = Mathf.Exp(-t / decayRate);
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.3f;
                float thud = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                samples[i] = (thud + noise) * decay * 0.35f;
            }
            return samples;
        }

        private static void SaveWav(string filePath, float[] samples, int sampleRate)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            using (var writer = new BinaryWriter(fileStream))
            {
                int byteRate = sampleRate * 1 * 2; // 1 channel, 16-bit
                writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + samples.Length * 2);
                writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                writer.Write(16); // subchunk1 size
                writer.Write((short)1); // PCM
                writer.Write((short)1); // mono
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)2); // block align
                writer.Write((short)16); // bits per sample
                writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                writer.Write(samples.Length * 2);

                for (int i = 0; i < samples.Length; i++)
                {
                    short val = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                    writer.Write(val);
                }
            }
        }
    }
}
