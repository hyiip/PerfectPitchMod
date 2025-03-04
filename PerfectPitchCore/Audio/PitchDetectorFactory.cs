using System;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Factory for creating pitch detectors optimized for specific vocal ranges
    /// </summary>
    public static class PitchDetectorFactory
    {
        /// <summary>
        /// Creates a pitch detector optimized for the given vocal range
        /// </summary>
        /// <param name="algorithm">The algorithm to use (e.g., "yin", "yinfft", "dywa")</param>
        /// <param name="sampleRate">The audio sample rate (typically 44100Hz)</param>
        /// <param name="basePitch">The base pitch for calibration</param>
        /// <param name="maxFrequency">The maximum frequency to detect</param>
        /// <param name="volumeThresholdDb">The volume threshold for detection</param>
        /// <param name="pitchSensitivity">Sensitivity/confidence threshold (0-1)</param>
        /// <returns>An IPitchDetector optimized for the specified parameters</returns>
        public static IPitchDetector CreateDetector(
            string algorithm,
            int sampleRate,
            float basePitch,
            float maxFrequency = 2000.0f,
            float volumeThresholdDb = -30.0f,
            float pitchSensitivity = 0.3f)
        {
            // Calculate the minimum frequency based on the base pitch
            // We want to detect at least 2 octaves below the base pitch
            int minFrequency = (int)Math.Max(20, basePitch / 4);

            Console.WriteLine($"Creating pitch detector for vocal range: ");
            Console.WriteLine($"- Base pitch: {basePitch:F1} Hz ({NoteUtility.GetNoteName(basePitch)})");
            Console.WriteLine($"- Detection range: {minFrequency} Hz to {maxFrequency} Hz");

            // Create the appropriate detector based on algorithm
            IPitchDetector detector;

            switch (algorithm.ToLowerInvariant())
            {
                case "dywa":
                    // For DYWA, minimum frequency directly impacts buffer size
                    detector = new DywaPitchDetector(
                        sampleRate,
                        minFrequency,
                        maxFrequency,
                        basePitch,
                        volumeThresholdDb);
                    break;

                case "yin":
                case "yinfft":
                case "mcomb":
                case "fcomb":
                case "schmitt":
                default:
                    // For Aubio algorithms, choose the appropriate buffer size based on low-frequency requirements
                    int bufferSize = 8192; // Default

                    // For lower frequencies, use larger buffer sizes
                    if (minFrequency < 50)
                        bufferSize = 16384;
                    else if (minFrequency < 100)
                        bufferSize = 8192;
                    else
                        bufferSize = 4096;

                    detector = new AubioPitchDetector(
                        sampleRate,
                        bufferSize,
                        algorithm,
                        maxFrequency,
                        basePitch,
                        volumeThresholdDb);

                    // Set confidence threshold for Aubio detectors
                    if (detector is AubioPitchDetector aubioDetector)
                    {
                        aubioDetector.SetConfidenceThreshold(pitchSensitivity);
                    }
                    break;
            }

            return detector;
        }
    }
}