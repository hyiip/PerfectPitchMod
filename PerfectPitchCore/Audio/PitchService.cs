using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.InteropServices;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Service that handles audio capture and pitch detection through NAudio
    /// </summary>
    public class PitchService : IPitchService
    {
        // P/Invoke for native DLL handling
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private PitchManager pitchManager;
        private Thread audioThread;
        private bool isRunning = false;
        private bool disposed = false;
        private PitchManager.PitchConfig currentConfig;
        private readonly object lockObject = new object();
        private readonly List<IPitchProcessor> pendingProcessors = new List<IPitchProcessor>();

        /// <summary>
        /// Create a new pitch service
        /// </summary>
        public PitchService()
        {
            // Load native libraries immediately before any other initialization
            LoadNativeLibraries();
        }

        /// <summary>
        /// Load native DLLs required for pitch detection
        /// </summary>
        private void LoadNativeLibraries()
        {
            try
            {
                string assemblyPath = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string libPath = System.IO.Path.Combine(assemblyPath, "lib");

                // Log paths for debugging
                Console.WriteLine($"Assembly path: {assemblyPath}");
                Console.WriteLine($"Library path: {libPath}");

                // Check if lib directory exists
                if (!System.IO.Directory.Exists(libPath))
                {
                    Console.WriteLine("Creating lib directory");
                    System.IO.Directory.CreateDirectory(libPath);
                }

                // Set the DLL directory to include lib folder
                bool success = SetDllDirectory(libPath);
                Console.WriteLine($"Setting DLL directory to {libPath}: {(success ? "Success" : "Failed")}");

                // Check for DLLs in both locations
                string dywaDllInLib = System.IO.Path.Combine(libPath, "dywapitchtrack.dll");
                string aubioDllInLib = System.IO.Path.Combine(libPath, "libaubio-5.dll");
                string dywaDllInRoot = System.IO.Path.Combine(assemblyPath, "dywapitchtrack.dll");
                string aubioDllInRoot = System.IO.Path.Combine(assemblyPath, "libaubio-5.dll");

                // If DLLs exist in root but not in lib, copy them
                if (System.IO.File.Exists(dywaDllInRoot) && !System.IO.File.Exists(dywaDllInLib))
                {
                    Console.WriteLine("Moving dywapitchtrack.dll from root to lib directory");
                    System.IO.File.Copy(dywaDllInRoot, dywaDllInLib);
                }

                if (System.IO.File.Exists(aubioDllInRoot) && !System.IO.File.Exists(aubioDllInLib))
                {
                    Console.WriteLine("Moving libaubio-5.dll from root to lib directory");
                    System.IO.File.Copy(aubioDllInRoot, aubioDllInLib);
                }

                // Explicitly try to load the DLLs
                if (System.IO.File.Exists(dywaDllInLib))
                {
                    IntPtr dywaDllHandle = LoadLibrary(dywaDllInLib);
                    if (dywaDllHandle != IntPtr.Zero)
                        Console.WriteLine("Successfully loaded dywapitchtrack.dll");
                    else
                        Console.WriteLine($"Failed to load dywapitchtrack.dll, error code: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Console.WriteLine("dywapitchtrack.dll not found in lib directory");
                }

                if (System.IO.File.Exists(aubioDllInLib))
                {
                    IntPtr aubioDllHandle = LoadLibrary(aubioDllInLib);
                    if (aubioDllHandle != IntPtr.Zero)
                        Console.WriteLine("Successfully loaded libaubio-5.dll");
                    else
                        Console.WriteLine($"Failed to load libaubio-5.dll, error code: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Console.WriteLine("libaubio-5.dll not found in lib directory");
                }

                // List all files in the lib directory
                if (System.IO.Directory.Exists(libPath))
                {
                    string[] files = System.IO.Directory.GetFiles(libPath);
                    Console.WriteLine($"Files in lib directory ({files.Length}):");
                    foreach (string file in files)
                    {
                        Console.WriteLine($"  - {System.IO.Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading native libraries: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Configure the pitch service
        /// </summary>
        public void Configure(PitchManager.PitchConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            currentConfig = config;

            // If already running, reconfigure the pitch manager
            if (pitchManager != null && isRunning)
            {
                pitchManager.UpdateConfiguration(config);
                Console.WriteLine($"PitchService: Reconfigured with {config.Algorithm} algorithm, base pitch {config.BasePitch:F2} Hz");
            }
        }

        /// <summary>
        /// Start the pitch detection service
        /// </summary>
        public void Start()
        {
            lock (lockObject)
            {
                if (isRunning)
                    return;

                try
                {
                    // Make sure we have a configuration
                    if (currentConfig == null)
                    {
                        currentConfig = new PitchManager.PitchConfig();
                        Console.WriteLine("PitchService: Using default configuration");
                    }

                    // Start audio processing in a separate thread
                    audioThread = new Thread(RunAudioCapture)
                    {
                        IsBackground = true,
                        Name = "PitchServiceThread"
                    };

                    isRunning = true;
                    audioThread.Start();

                    Console.WriteLine("PitchService: Started");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PitchService: Error starting - {ex.Message}");
                    isRunning = false;
                    throw;
                }
            }
        }

        /// <summary>
        /// Stop the pitch detection service
        /// </summary>
        public void Stop()
        {
            lock (lockObject)
            {
                isRunning = false;

                if (audioThread != null && audioThread.IsAlive)
                {
                    // Wait for thread to exit gracefully
                    audioThread.Join(500);
                }

                if (pitchManager != null)
                {
                    pitchManager.Stop();
                }

                Console.WriteLine("PitchService: Stopped");
            }
        }

        /// <summary>
        /// Register a processor to receive pitch data
        /// </summary>
        /// 

        public void RegisterProcessor(IPitchProcessor processor)
        {
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));

            if (pitchManager != null)
            {
                pitchManager.RegisterProcessor(processor);
                Console.WriteLine($"PitchService: Registered processor {processor.GetType().Name}");
            }
            else
            {
                // Store for later registration when pitchManager is created
                pendingProcessors.Add(processor);
                Console.WriteLine($"PitchService: Queued processor {processor.GetType().Name} for registration");
            }
        }

        /// <summary>
        /// Unregister a processor
        /// </summary>
        public void UnregisterProcessor(IPitchProcessor processor)
        {
            if (processor == null)
                throw new ArgumentNullException(nameof(processor));

            if (pitchManager != null)
            {
                pitchManager.UnregisterProcessor(processor);
                Console.WriteLine($"PitchService: Unregistered processor {processor.GetType().Name}");
            }
        }

        /// <summary>
        /// Get available microphones in the system
        /// </summary>
        public List<string> GetAvailableMicrophones()
        {
            // Forward to PitchManager which handles the NAudio dependency
            return PitchManager.GetAvailableMicrophones();
        }

        /// <summary>
        /// Get the current audio level (0-1)
        /// </summary>
        public float GetCurrentAudioLevel()
        {
            return pitchManager?.GetCurrentAudioLevel() ?? 0f;
        }

        /// <summary>
        /// Get the current audio level in decibels
        /// </summary>
        public float GetCurrentAudioLevelDb()
        {
            if (pitchManager == null)
                return -100f;

            float level = pitchManager.GetCurrentAudioLevel();
            return pitchManager.ConvertToDecibels(level);
        }

        /// <summary>
        /// Enable or disable verbose logging
        /// </summary>
        public void SetVerboseLogging(bool verbose)
        {
            // Set static verbose flags in various components
            PitchDetectorBase.VerboseDebugLogging = verbose;
            JumpLevelCalculator.EnableDetailedLogging = verbose;
            CalibrationProcessor.VerboseCalibrationLogging = verbose;

            Console.WriteLine($"PitchService: Verbose logging {(verbose ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Main audio capture thread function
        /// </summary>
        private void RunAudioCapture()
        {
            try
            {
                Console.WriteLine("PitchService: Audio thread running");
                Console.WriteLine($"PitchService: Using {currentConfig.Algorithm} algorithm");
                Console.WriteLine($"PitchService: Base pitch: {currentConfig.BasePitch:F2} Hz");

                // Create pitch manager with config
                pitchManager = new PitchManager(currentConfig);

                // Register any pending processors
                foreach (var processor in pendingProcessors)
                {
                    pitchManager.RegisterProcessor(processor);
                    Console.WriteLine($"PitchService: Registered queued processor {processor.GetType().Name}");
                }
                pendingProcessors.Clear();

                // Start pitch detection
                pitchManager.Start();

                // Keep thread alive until stopped
                while (isRunning)
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PitchService: Error in audio thread - {ex.Message}");
            }
            finally
            {
                if (pitchManager != null)
                {
                    pitchManager.Stop();
                    pitchManager.Dispose();
                    pitchManager = null;
                }

                Console.WriteLine("PitchService: Audio thread exited");
            }
        }

        /// <summary>
        /// Get the underlying pitch manager for calibration and advanced operations
        /// </summary>
        /// <returns>The current pitch manager or null if not available</returns>
        public PitchManager GetPitchManager()
        {
            return pitchManager;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                Stop();

                if (pitchManager != null)
                {
                    pitchManager.Dispose();
                    pitchManager = null;
                }

                disposed = true;
                Console.WriteLine("PitchService: Disposed");
            }
        }
    }
}