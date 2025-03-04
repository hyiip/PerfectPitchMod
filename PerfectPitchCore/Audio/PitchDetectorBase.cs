using System;
using System.Collections.Generic;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    public abstract class PitchDetectorBase : IPitchDetector
    {
        protected readonly int sampleRate;
        protected readonly float maxFrequency;
        protected readonly float basePitch;
        protected readonly float volumeThresholdDb;
        protected float currentPitch;
        protected bool isPitchDetected;
        private double prevSample = 0.0; // For IIR low-pass filter
        private readonly Queue<float> pitchHistory = new Queue<float>(); // For smoothing
        private const int PITCH_HISTORY_SIZE = 5; // Smooth over last 5 detections

        /// <summary>
        /// Controls whether verbose debug logging is enabled
        /// </summary>
        public static bool VerboseDebugLogging { get; set; } = false;

        protected PitchDetectorBase(int sampleRate, float maxFrequency, float basePitch, float volumeThresholdDb)
        {
            this.sampleRate = sampleRate;
            this.maxFrequency = maxFrequency;
            this.basePitch = basePitch;
            this.volumeThresholdDb = volumeThresholdDb;
        }

        protected abstract void DetectPitch(byte[] buffer, int bytesRecorded, float audioLevelDb);

        public void ProcessAudioData(byte[] buffer, int bytesRecorded, float audioLevelDb = -100.0f)
        {
            DetectPitch(buffer, bytesRecorded, audioLevelDb);
        }

        // Then modify the ProcessPitch method:
        protected void ProcessPitch(double pitch, float audioLevelDb)
        {
            if (audioLevelDb <= volumeThresholdDb)
            {
                isPitchDetected = false;
                return;
            }

            if (sampleRate != 44100)
                pitch *= (double)sampleRate / 44100.0;

            bool frequencyInRange = pitch > 0 && pitch <= maxFrequency;

            if (pitch > 0 && frequencyInRange)
            {
                // Add pitch to history for smoothing
                pitchHistory.Enqueue((float)pitch);
                if (pitchHistory.Count > PITCH_HISTORY_SIZE)
                    pitchHistory.Dequeue();

                // Calculate smoothed pitch
                float smoothedPitch = 0;
                foreach (float p in pitchHistory)
                    smoothedPitch += p;
                smoothedPitch /= pitchHistory.Count;

                currentPitch = smoothedPitch;
                isPitchDetected = true;

                // Only log if verbose logging is enabled
                if (VerboseDebugLogging)
                {
                    float semitonesAboveBase = 12 * (float)Math.Log(currentPitch / basePitch, 2);
                    int jumpLevel = (int)Math.Round(semitonesAboveBase);
                    jumpLevel = Math.Min(Math.Max(jumpLevel, 0), 35);
                    Console.WriteLine($"DEBUG: Pitch Detected - Smoothed Pitch: {currentPitch:F1} Hz, Jump Level: {jumpLevel}");
                }
            }
            else
            {
                isPitchDetected = false;
            }
        }

        protected double ApplyIIRFilter(double sample, float alpha = 0.2f)
        {
            double filteredSample = (alpha * sample) + ((1 - alpha) * prevSample);
            prevSample = filteredSample;
            return filteredSample;
        }

        public float GetCurrentPitch() => currentPitch;

        public bool IsPitchDetected() => isPitchDetected;

        public int GetJumpLevel(float basePitch)
        {
            if (!isPitchDetected || currentPitch <= 0)
                return 0;

            // Use the JumpLevelCalculator for consistent jump level calculation
            return JumpLevelCalculator.CalculateJumpLevel(currentPitch, basePitch);
        }

        public abstract int GetNeededSampleCount();

        public abstract void Dispose();

        protected bool disposed = false;
    }
}