using System;
using System.Collections.Generic;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Utils;
using PerfectPitchCore.Constants;
using PerfectPitch.Utils;

namespace PerfectPitch.Game
{
    /// <summary>
    /// Controller that processes pitch data and triggers jumps
    /// </summary>
    public class JumpController : IPitchProcessor
    {
        // Event for when a jump is triggered
        public event Action<int> OnJumpTriggered;

        // Jump control variables
        private int lastJumpLevel = -1;
        private DateTime lastJumpTime = DateTime.MinValue;

        // Audio threshold - Use constant as default
        private float minAudioLevelDb = AppConstants.Audio.DEFAULT_VOLUME_THRESHOLD_DB;

        // Stability tracking with Queue for better history tracking
        private Queue<int> recentJumpLevels = new Queue<int>();
        private int stabilityHistory = AppConstants.Stability.DEFAULT_HISTORY;
        private int stabilityThreshold = AppConstants.Stability.DEFAULT_THRESHOLD;

        // Use constant for minimum jump interval
        private const double MIN_JUMP_INTERVAL_MS = AppConstants.Stability.MIN_JUMP_INTERVAL_MS;

        // Predefined stability presets
        public enum StabilityPreset
        {
            Fast = 0,      // (3,2) - Quicker response but might have more false positives
            Normal = 1,    // (5,3) - Balanced approach
            Precise = 2    // (7,4) - More stable detection but might feel less responsive
        }

        /// <summary>
        /// Constructor - initializes with default settings from config
        /// </summary>
        public JumpController()
        {
            // Load settings from config
            LoadFromConfig();
        }

        /// <summary>
        /// Load settings from the configuration file
        /// </summary>
        private void LoadFromConfig()
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                if (config != null)
                {
                    // Set the audio threshold
                    minAudioLevelDb = config.VolumeThresholdDb;

                    // Set the stability settings
                    stabilityHistory = config.StabilityHistory;
                    stabilityThreshold = config.StabilityThreshold;

                    Log.Info($"Loaded settings from config: Threshold={minAudioLevelDb}dB, " +
                        $"Stability=({stabilityHistory},{stabilityThreshold})");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error loading settings from config", ex);
            }
        }

        /// <summary>
        /// Process pitch data from the pitch detector
        /// </summary>
        public void ProcessPitch(PitchData pitchData)
        {
            // Skip if no pitch detected or audio level too low
            if (pitchData.Pitch <= 0 || pitchData.AudioLevelDb < minAudioLevelDb)
            {
                // Clear stability queue when no pitch is detected
                recentJumpLevels.Clear();
                return;
            }

            // Get jump level from pitch data
            int jumpLevel = pitchData.JumpLevel;

            // Log basic pitch information - using minimal logging
            Log.Info($"Detected pitch: {pitchData.Pitch:F1} Hz ({pitchData.NoteName}), Jump Level: {jumpLevel}");

            // Add to history
            recentJumpLevels.Enqueue(jumpLevel);

            // Trim history to maintain size
            while (recentJumpLevels.Count > stabilityHistory)
            {
                recentJumpLevels.Dequeue();
            }

            // Check for stability using configured parameters
            if (recentJumpLevels.Count >= stabilityThreshold && jumpLevel > 0)
            {
                // Debounce check
                DateTime now = DateTime.Now;
                double timeSinceLastJump = (now - lastJumpTime).TotalMilliseconds;

                if (timeSinceLastJump >= MIN_JUMP_INTERVAL_MS)
                {
                    Log.Info($"JUMP TRIGGERED: Level {jumpLevel}/{AppConstants.VoiceJump.MAX_JUMP_LEVEL}");

                    // Make sure the jump level is valid (1-35)
                    jumpLevel = Math.Max(1, Math.Min(jumpLevel, AppConstants.VoiceJump.MAX_JUMP_LEVEL));

                    // Trigger the jump
                    OnJumpTriggered?.Invoke(jumpLevel);

                    // Update last jump state
                    lastJumpLevel = jumpLevel;
                    lastJumpTime = now;
                }
            }
        }

        /// <summary>
        /// Set the audio level threshold in dB
        /// </summary>
        public void SetAudioThreshold(float thresholdDb)
        {
            minAudioLevelDb = thresholdDb;
            Log.Info($"Audio threshold set to {thresholdDb:F1} dB");

            // Update the config
            UpdateConfig();
        }

        /// <summary>
        /// Set the stability preset and save to config
        /// </summary>
        public void SetStabilityPreset(StabilityPreset preset)
        {
            // Use arrays from constants
            int presetIndex = (int)preset;
            if (presetIndex >= 0 && presetIndex < AppConstants.Stability.PRESET_HISTORIES.Length)
            {
                stabilityHistory = AppConstants.Stability.PRESET_HISTORIES[presetIndex];
                stabilityThreshold = AppConstants.Stability.PRESET_THRESHOLDS[presetIndex];

                // Clear the queue to ensure it doesn't exceed the new size
                recentJumpLevels.Clear();

                Log.Info($"Stability preset set to {preset} ({stabilityHistory},{stabilityThreshold})");

                // Update the config
                UpdateConfig();
            }
        }

        /// <summary>
        /// Manually set stability parameters and save to config
        /// </summary>
        public void SetStabilityParameters(int history, int threshold)
        {
            // Validate inputs
            history = Math.Max(2, Math.Min(10, history));
            threshold = Math.Max(1, Math.Min(history, threshold));

            stabilityHistory = history;
            stabilityThreshold = threshold;

            // Clear the queue to ensure it doesn't exceed the new size
            recentJumpLevels.Clear();

            Log.Info($"Stability parameters set to ({stabilityHistory},{stabilityThreshold})");

            // Update the config
            UpdateConfig();
        }

        /// <summary>
        /// Update the configuration file with current settings
        /// </summary>
        private void UpdateConfig()
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                if (config == null)
                {
                    config = ConfigManager.CreateDefaultConfig();
                }

                // Update the config
                config.VolumeThresholdDb = minAudioLevelDb;
                config.StabilityHistory = stabilityHistory;
                config.StabilityThreshold = stabilityThreshold;

                // Save the config
                ConfigManager.SaveConfig(config);
            }
            catch (Exception ex)
            {
                Log.Error("Error updating configuration", ex);
            }
        }
    }
}