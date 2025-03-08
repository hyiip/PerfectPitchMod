using System;

namespace PerfectPitchCore.Constants
{
    /// <summary>
    /// Constants for the Perfect Pitch mod
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// Audio processing and pitch detection constants
        /// </summary>
        public static class Audio
        {
            // Default audio settings
            public const float DEFAULT_BASE_PITCH = 103.83f; // C3
            public const float DEFAULT_VOLUME_THRESHOLD_DB = -30.0f;
            public const float DEFAULT_MAX_FREQUENCY = 2000.0f;
            public const int DEFAULT_MIN_FREQUENCY = 40;
            public const float DEFAULT_PITCH_SENSITIVITY = 0.5f;
            public const int DEFAULT_SAMPLE_RATE = 44100;

            // Microphone thresholds
            public static readonly float[] THRESHOLD_VALUES = {
                -50.0f,  // Very sensitive
                -40.0f,  // Sensitive (default)
                -30.0f,  // Moderate
                -20.0f   // Less sensitive
            };

            public static readonly string[] THRESHOLD_LABELS = {
                "Very Sensitive (-50dB)",
                "Sensitive (-40dB)",
                "Moderate (-30dB)",
                "Less Sensitive (-20dB)"
            };

            // Default algorithm
            public const string DEFAULT_ALGORITHM = "dywa";
        }

        /// <summary>
        /// Stability settings constants
        /// </summary>
        public static class Stability
        {
            // Default stability settings
            public const int DEFAULT_HISTORY = 3;
            public const int DEFAULT_THRESHOLD = 2;

            // Preset values
            public static readonly int[] PRESET_HISTORIES = { 3, 5, 7 };
            public static readonly int[] PRESET_THRESHOLDS = { 2, 3, 4 };
            public static readonly string[] PRESET_LABELS = {
                "Fast (3,2)",
                "Normal (5,3)",
                "Precise (7,4)"
            };

            // Minimum time between jumps (ms)
            public const double MIN_JUMP_INTERVAL_MS = 300;
        }

        /// <summary>
        /// Configuration file constants
        /// </summary>
        public static class Config
        {
            public const string DEFAULT_CONFIG_FILENAME = "PerfectPitchConfig.xml";
            public const string APP_FOLDER_NAME = "PerfectPitch";
        }

        /// <summary>
        /// Voice jump mod constants
        /// </summary>
        public static class VoiceJump
        {
            // Maximum jump level in Jump King
            public const int MAX_JUMP_LEVEL = 35;

            // Default keyboard shortcuts - only keeping the mute key
            public const string MUTE_KEY = "M";
        }
    }
}