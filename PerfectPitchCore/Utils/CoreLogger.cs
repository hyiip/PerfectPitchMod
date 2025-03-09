using System;
using System.IO;
using System.Reflection;

namespace PerfectPitchCore.Utils
{
    /// <summary>
    /// Core logging utility for PerfectPitchCore components
    /// </summary>
    public static class CoreLogger
    {
        private static string logFilePath;
        private static bool isInitialized = false;
        private static object logLock = new object(); // For thread safety

        /// <summary>
        /// Initialize the logging system
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Setup log file in same directory as the assembly
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                logFilePath = Path.Combine(assemblyPath, "PerfectPitchCore.log");

                // Clear existing log
                File.WriteAllText(logFilePath, "PerfectPitchCore Log File\n");

                isInitialized = true;

                // Log initialization
                Info("Core logging system initialized");
            }
            catch (Exception ex)
            {
                // Write to console since file logging failed
                Console.WriteLine($"Error initializing core log: {ex.Message}");
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

            // Also log inner exception if present
            if (ex.InnerException != null)
            {
                Write("ERROR", $"Inner exception: {ex.InnerException.Message}");
                Write("ERROR", $"Inner stack trace: {ex.InnerException.StackTrace}");
            }
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

                // Write to file if path is available - use a lock for thread safety
                if (!string.IsNullOrEmpty(logFilePath))
                {
                    lock (logLock)
                    {
                        File.AppendAllText(logFilePath, formattedMessage + "\n");
                    }
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }
}