using System;
using System.IO;
using System.Xml.Serialization;
using PerfectPitchCore.Audio;

namespace PerfectPitchCore.Utils
{
    /// <summary>
    /// Manages saving and loading of configuration
    /// </summary>
    public static class ConfigManager
    {
        // Default config file name
        private const string DefaultConfigFileName = "config.xml";

        // Default directory in AppData
        private static readonly string DefaultAppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PerfectPitch");

        // Custom config path that can be set by the game mod
        private static string customConfigPath = null;

        /// <summary>
        /// Set a custom base path for configuration 
        /// </summary>
        /// <param name="path">The directory path where config should be stored</param>
        public static void SetConfigPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                customConfigPath = null;
                return;
            }

            try
            {
                // Create the directory if it doesn't exist
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                customConfigPath = path;
                Console.WriteLine($"Config path set to: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting config path: {ex.Message}");
                customConfigPath = null;
            }
        }

        /// <summary>
        /// Get the current config path being used
        /// </summary>
        public static string GetCurrentConfigPath()
        {
            return GetConfigPath();
        }

        /// <summary>
        /// Load configuration from the default path
        /// </summary>
        /// <returns>Configuration object or default if file not found</returns>
        public static PitchManager.PitchConfig LoadConfig()
        {
            string configPath = GetConfigPath();
            return LoadConfig(configPath);
        }

        /// <summary>
        /// Load configuration from a specific path
        /// </summary>
        /// <param name="path">Path to the config file</param>
        /// <returns>Configuration object or default if file not found</returns>
        public static PitchManager.PitchConfig LoadConfig(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var serializer = new XmlSerializer(typeof(PitchManager.PitchConfig));
                    using (var reader = new StreamReader(path))
                    {
                        var config = (PitchManager.PitchConfig)serializer.Deserialize(reader);
                        Console.WriteLine($"Loaded configuration from {path}");
                        Console.WriteLine($"Base note: {NoteUtility.GetNoteName(config.BasePitch)} ({config.BasePitch:F2} Hz)");
                        Console.WriteLine($"Stability settings: History={config.StabilityHistory}, Threshold={config.StabilityThreshold}");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
            }

            // Return default config if loading fails
            return CreateDefaultConfig();
        }

        /// <summary>
        /// Save configuration to the default path
        /// </summary>
        /// <param name="config">Configuration to save</param>
        /// <returns>True if saved successfully</returns>
        public static bool SaveConfig(PitchManager.PitchConfig config)
        {
            string configPath = GetConfigPath();
            return SaveConfig(config, configPath);
        }

        /// <summary>
        /// Save configuration to a specific path
        /// </summary>
        /// <param name="config">Configuration to save</param>
        /// <param name="path">Path to save to</param>
        /// <returns>True if saved successfully</returns>
        public static bool SaveConfig(PitchManager.PitchConfig config, string path)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var serializer = new XmlSerializer(typeof(PitchManager.PitchConfig));
                using (var writer = new StreamWriter(path))
                {
                    serializer.Serialize(writer, config);
                }

                Console.WriteLine($"Configuration saved to {path}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create a default configuration with optimal settings
        /// </summary>
        /// <returns>Default configuration</returns>
        public static PitchManager.PitchConfig CreateDefaultConfig()
        {
            return new PitchManager.PitchConfig
            {
                Algorithm = "dywa", // Default to DYWA as requested
                BasePitch = 103.83f, // C3
                DeviceNumber = 0,
                MaxFrequency = 2000.0f,
                MinFrequency = 40,
                VolumeThresholdDb = -30.0f,
                PitchSensitivity = 0.5f,
                SampleRate = 44100,

                // Default stability settings
                StabilityHistory = 3,
                StabilityThreshold = 2
            };
        }

        /// <summary>
        /// Get the appropriate configuration file path
        /// </summary>
        private static string GetConfigPath()
        {
            // Use custom path if set
            if (!string.IsNullOrEmpty(customConfigPath))
            {
                return Path.Combine(customConfigPath, DefaultConfigFileName);
            }

            // Fall back to AppData 
            string appDataPath = Path.Combine(DefaultAppDataPath, DefaultConfigFileName);

            // Ensure directory exists
            string directory = Path.GetDirectoryName(appDataPath);
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating config directory: {ex.Message}");
                }
            }

            return appDataPath;
        }
    }
}