using System;
using System.IO;
using System.Reflection;

namespace PerfectPitch.Utils
{
    /// <summary>
    /// Utility class for logging messages to file and console
    /// </summary>
    public static class Log
    {
        private static string logFilePath;
        private static bool isInitialized = false;

        /// <summary>
        /// Initialize the logging system
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Setup log file in same directory as the assembly
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                logFilePath = Path.Combine(assemblyPath, "PerfectPitchLog.txt");

                // Clear existing log
                File.WriteAllText(logFilePath, "PerfectPitch Log File\n");

                isInitialized = true;

                // Log initialization
                Info("Logging system initialized");
            }
            catch (Exception ex)
            {
                // Write to console since file logging failed
                Console.WriteLine($"Error initializing log: {ex.Message}");
            }
        }

        /// <summary>
        /// Log informational message
        /// </summary>
        public static void Info(string message)
        {
            Write("INFO", message);
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public static void Warning(string message)
        {
            Write("WARN", message);
        }

        /// <summary>
        /// Log error message
        /// </summary>
        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        /// <summary>
        /// Log error message with exception details
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            Write("ERROR", $"{message}: {ex.Message}");
            Write("ERROR", $"Stack trace: {ex.StackTrace}");
        }

        /// <summary>
        /// Log debug message (only in debug builds)
        /// </summary>
        public static void Debug(string message)
        {
#if DEBUG
            Write("DEBUG", message);
#endif
        }

        /// <summary>
        /// Write message to log file and console
        /// </summary>
        private static void Write(string level, string message)
        {
            try
            {
                // Initialize if not already
                if (!isInitialized)
                {
                    Initialize();
                }

                // Format log message
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string formattedMessage = $"[{timestamp}] [{level}] {message}";

                // Write to console
                Console.WriteLine(formattedMessage);

                // Write to file if path is available
                if (!string.IsNullOrEmpty(logFilePath))
                {
                    File.AppendAllText(logFilePath, formattedMessage + "\n");
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }
}