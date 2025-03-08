using System;
using System.Collections.Generic;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Service interface for pitch detection that hides implementation details from clients
    /// </summary>
    public interface IPitchService : IDisposable
    {
        /// <summary>
        /// Configure the pitch service
        /// </summary>
        void Configure(PitchManager.PitchConfig config);

        /// <summary>
        /// Start the pitch detection service
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the pitch detection service
        /// </summary>
        void Stop();

        /// <summary>
        /// Register a processor to receive pitch data
        /// </summary>
        void RegisterProcessor(IPitchProcessor processor);

        /// <summary>
        /// Unregister a processor
        /// </summary>
        void UnregisterProcessor(IPitchProcessor processor);

        /// <summary>
        /// Get available microphones in the system
        /// </summary>
        List<string> GetAvailableMicrophones();

        /// <summary>
        /// Get the current audio level (0-1)
        /// </summary>
        float GetCurrentAudioLevel();

        /// <summary>
        /// Get the current audio level in decibels
        /// </summary>
        float GetCurrentAudioLevelDb();

        /// <summary>
        /// Enable or disable verbose logging
        /// </summary>
        void SetVerboseLogging(bool verbose);

        /// <summary>
        /// Enable or disable the debug visualizer
        /// </summary>
        void EnableDebugVisualizer(bool enable);
    }
}