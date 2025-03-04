using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Pitch detector implementation using the Aubio library
    /// </summary>
    public class AubioPitchDetector : PitchDetectorBase
    {
        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr new_aubio_pitch(string method, uint bufferSize, uint hopSize, uint sampleRate);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void del_aubio_pitch(IntPtr pitchDetector);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr new_fvec(uint size);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void del_fvec(IntPtr fvec);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void fvec_set_sample(IntPtr fvec, float value, uint position);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern float fvec_get_sample(IntPtr fvec, uint position);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void aubio_pitch_do(IntPtr pitchDetector, IntPtr input, IntPtr output);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void aubio_pitch_set_silence(IntPtr pitchDetector, float silence);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void aubio_pitch_set_unit(IntPtr pitchDetector, string unit);

        [DllImport("libaubio-5.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern float aubio_pitch_get_confidence(IntPtr pitchDetector);

        private IntPtr pitchDetector;
        private IntPtr inputBuffer;
        private IntPtr outputBuffer;
        private readonly uint bufferSize;
        private readonly uint hopSize;
        private readonly string method;
        private float silenceThreshold = -80.0f;
        private float confidenceThreshold = 0.3f;
        private float lastConfidence;
        private float currentRmsLevel = 0.0f;
        private Queue<float> recentPitches = new Queue<float>();
        private const int MAX_RECENT_PITCHES = 5;

        /// <summary>
        /// Create an Aubio-based pitch detector
        /// </summary>
        public AubioPitchDetector(int sampleRate = 44100, int bufferSize = 8192, string method = "yin", float maxDetectableFrequency = 2000.0f, float basePitch = 130.81f, float volumeThresholdDb = -30.0f)
            : base(sampleRate, maxDetectableFrequency, basePitch, volumeThresholdDb)
        {
            this.bufferSize = (uint)bufferSize;
            this.hopSize = this.bufferSize / 4;
            this.method = method;

            Console.WriteLine($"Creating pitch detector with buffer size: {bufferSize}, max frequency: {maxDetectableFrequency} Hz");
            Initialize();
        }

        /// <summary>
        /// Initialize the Aubio pitch detector
        /// </summary>
        private void Initialize()
        {
            try
            {
                pitchDetector = new_aubio_pitch(method, bufferSize, hopSize, (uint)sampleRate);
                if (pitchDetector == IntPtr.Zero)
                    throw new Exception($"Failed to create Aubio pitch detector with method '{method}'");

                inputBuffer = new_fvec(hopSize);
                if (inputBuffer == IntPtr.Zero)
                    throw new Exception("Failed to create Aubio input buffer");

                outputBuffer = new_fvec(1);
                if (outputBuffer == IntPtr.Zero)
                    throw new Exception("Failed to create Aubio output buffer");

                aubio_pitch_set_silence(pitchDetector, silenceThreshold);
                aubio_pitch_set_unit(pitchDetector, "Hz");

                Console.WriteLine($"Initialized Aubio pitch detector using {method} algorithm");
                Console.WriteLine($"Detection settings: Silence threshold = {silenceThreshold}dB, Confidence threshold = {confidenceThreshold}");
            }
            catch (Exception ex)
            {
                Cleanup();
                throw new Exception($"Error initializing Aubio: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Detect pitch using the Aubio library
        /// </summary>
        protected override void DetectPitch(byte[] buffer, int bytesRecorded, float audioLevelDb)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AubioPitchDetector));

            int sampleCount = Math.Min(bytesRecorded / 2, (int)hopSize);

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                float sampleFloat = sample / 32768.0f;

                float filteredSample = (float)ApplyIIRFilter(sampleFloat);
                fvec_set_sample(inputBuffer, filteredSample, (uint)i);
            }

            aubio_pitch_do(pitchDetector, inputBuffer, outputBuffer);

            float pitch = fvec_get_sample(outputBuffer, 0);
            float confidence = aubio_pitch_get_confidence(pitchDetector);
            lastConfidence = confidence;

            float effectiveConfidenceThreshold = method.ToLower() == "yin" ?
                Math.Min(0.1f, confidenceThreshold) : confidenceThreshold;

            if (confidence > effectiveConfidenceThreshold || method.ToLower() == "schmitt")
            {
                recentPitches.Enqueue(pitch);
                if (recentPitches.Count > MAX_RECENT_PITCHES)
                    recentPitches.Dequeue();

                ProcessPitch(pitch, audioLevelDb);
            }
            else
            {
                isPitchDetected = false;
            }
        }

        /// <summary>
        /// Get the current RMS audio level
        /// </summary>
        public float GetCurrentRmsLevel() => currentRmsLevel;

        /// <summary>
        /// Convert linear amplitude to decibels
        /// </summary>
        public float LinearToDecibels(float linear)
        {
            if (linear < 0.00001f)
                return -100.0f;

            return 20.0f * (float)Math.Log10(linear);
        }

        /// <summary>
        /// Check if the detected pitch is stable
        /// </summary>
        public bool IsPitchStable()
        {
            if (recentPitches.Count < 3)
                return false;

            float stabilityPercent = GetStabilityPercentage();
            return stabilityPercent > 80.0f;
        }

        /// <summary>
        /// Calculate pitch stability as a percentage
        /// </summary>
        public float GetStabilityPercentage()
        {
            if (recentPitches.Count < 3)
                return 0.0f;

            float sum = 0;
            foreach (float pitch in recentPitches)
            {
                sum += pitch;
            }

            float mean = sum / recentPitches.Count;
            float varianceSum = 0;
            foreach (float pitch in recentPitches)
            {
                float diff = pitch - mean;
                varianceSum += diff * diff;
            }

            float variance = varianceSum / recentPitches.Count;
            float stdDev = (float)Math.Sqrt(variance);
            float maxStdDev = mean * 0.1f;
            float stabilityPercentage = 100.0f * (1.0f - Math.Min(1.0f, stdDev / maxStdDev));

            return stabilityPercentage;
        }

        /// <summary>
        /// Get the last confidence value from Aubio
        /// </summary>
        public float GetLastConfidence() => lastConfidence;

        /// <summary>
        /// Set the silence threshold in dB
        /// </summary>
        public void SetSilenceThreshold(float thresholdDB)
        {
            silenceThreshold = thresholdDB;
            if (pitchDetector != IntPtr.Zero)
            {
                aubio_pitch_set_silence(pitchDetector, silenceThreshold);
                Console.WriteLine($"Updated silence threshold to {silenceThreshold}dB");
            }
        }

        /// <summary>
        /// Set the confidence threshold
        /// </summary>
        public void SetConfidenceThreshold(float threshold)
        {
            confidenceThreshold = threshold;
            Console.WriteLine($"Updated confidence threshold to {confidenceThreshold}");
        }

        /// <summary>
        /// Get the number of samples needed for pitch detection
        /// </summary>
        public override int GetNeededSampleCount()
        {
            return (int)bufferSize;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public override void Dispose()
        {
            if (!disposed)
            {
                Cleanup();
                GC.SuppressFinalize(this);
                disposed = true;
            }
        }

        /// <summary>
        /// Clean up native resources
        /// </summary>
        private void Cleanup()
        {
            if (pitchDetector != IntPtr.Zero)
            {
                del_aubio_pitch(pitchDetector);
                pitchDetector = IntPtr.Zero;
            }

            if (inputBuffer != IntPtr.Zero)
            {
                del_fvec(inputBuffer);
                inputBuffer = IntPtr.Zero;
            }

            if (outputBuffer != IntPtr.Zero)
            {
                del_fvec(outputBuffer);
                outputBuffer = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~AubioPitchDetector()
        {
            Dispose();
        }
    }
}