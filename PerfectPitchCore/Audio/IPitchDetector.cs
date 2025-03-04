using System;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Interface for pitch detector implementations
    /// </summary>
    public interface IPitchDetector : IDisposable
    {
        /// <summary>
        /// Process a buffer of audio data to detect pitch
        /// </summary>
        void ProcessAudioData(byte[] buffer, int bytesRecorded, float audioLevelDb = -100.0f);

        /// <summary>
        /// Get the current detected pitch in Hz
        /// </summary>
        float GetCurrentPitch();

        /// <summary>
        /// Check if a pitch is currently detected
        /// </summary>
        bool IsPitchDetected();

        /// <summary>
        /// Calculate jump level based on the current pitch relative to a base pitch
        /// </summary>
        int GetJumpLevel(float basePitch);

        /// <summary>
        /// Get the number of samples needed for effective pitch detection
        /// </summary>
        int GetNeededSampleCount();
    }
}