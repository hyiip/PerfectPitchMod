using System;
using System.Runtime.InteropServices;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Pitch detector implementation using the Dynamic Wavelet Algorithm with improved low-frequency handling
    /// </summary>
    public class DywaPitchDetector : PitchDetectorBase
    {
        [DllImport("dywapitchtrack.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dywapitch_neededsamplecount(int minFreq);

        [DllImport("dywapitchtrack.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void dywapitch_inittracking(ref DywaPitchTracker pitchtracker);

        [DllImport("dywapitchtrack.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern double dywapitch_computepitch(ref DywaPitchTracker pitchtracker, double[] samples, int startsample, int samplecount);

        [StructLayout(LayoutKind.Sequential)]
        private struct DywaPitchTracker
        {
            public double PrevPitch;
            public int PitchConfidence;
        }

        private DywaPitchTracker tracker;
        private readonly int minFreq;
        private readonly int bufferSize;
        private double[] sampleBuffer;
        private int currentBufferIndex = 0;
        private readonly int targetMinFreq;

        /// <summary>
        /// Create a DYWA-based pitch detector optimized for the given frequency range
        /// </summary>
        public DywaPitchDetector(int sampleRate = 44100, int minFreq = 40, float maxFrequency = 2000.0f, float basePitch = 130.81f, float volumeThresholdDb = -30.0f)
            : base(sampleRate, maxFrequency, basePitch, volumeThresholdDb)
        {
            // Store the target minimum frequency
            this.targetMinFreq = minFreq;

            // For DYWA, ensure minFreq is low enough to capture the full vocal range
            // The lower we set this, the larger the buffer will be, allowing detection of lower frequencies
            // 40Hz is around E1, which is below the range of most human voices but gives us headroom
            this.minFreq = Math.Min(minFreq, 40);

            // Calculate the needed buffer size based on the minimum frequency
            bufferSize = dywapitch_neededsamplecount(this.minFreq);
            sampleBuffer = new double[bufferSize];

            Console.WriteLine($"DywaPitchDetector: Buffer size set to {bufferSize} samples for min frequency {this.minFreq} Hz");
            Console.WriteLine($"DywaPitchDetector: This allows detection down to approximately {3 * sampleRate / bufferSize} Hz");

            // Initialize the pitch tracker
            tracker = new DywaPitchTracker();
            dywapitch_inittracking(ref tracker);
            Console.WriteLine("Initialized DywaPitchDetector");
        }

        /// <summary>
        /// Detect pitch using the DYWA algorithm
        /// </summary>
        protected override void DetectPitch(byte[] buffer, int bytesRecorded, float audioLevelDb)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(DywaPitchDetector));

            int sampleCount = bytesRecorded / 2;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                double sampleValue = sample / 32768.0;

                double filteredSample = ApplyIIRFilter(sampleValue);
                sampleBuffer[currentBufferIndex] = filteredSample;
                currentBufferIndex++;

                if (currentBufferIndex >= bufferSize)
                {
                    double pitch = dywapitch_computepitch(ref tracker, sampleBuffer, 0, bufferSize);

                    // Apply validation for the detected pitch
                    if (pitch > 0)
                    {
                        // Check if the pitch is in a reasonable range
                        // Reject if it's below our target minimum frequency and DYWA confidence is low
                        if (pitch < targetMinFreq && tracker.PitchConfidence < 3)
                        {
                            //Console.WriteLine($"Rejected low-confidence pitch: {pitch:F1} Hz (confidence: {tracker.PitchConfidence})");
                            pitch = 0; // Reject this pitch
                        }
                    }

                    ProcessPitch(pitch, audioLevelDb);
                    currentBufferIndex = 0;
                }
            }
        }

        /// <summary>
        /// Get the DYWA confidence level (0-5)
        /// </summary>
        public int GetConfidence()
        {
            return tracker.PitchConfidence;
        }

        /// <summary>
        /// Get the number of samples needed for pitch detection
        /// </summary>
        public override int GetNeededSampleCount()
        {
            return bufferSize;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public override void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
            }
        }
    }
}