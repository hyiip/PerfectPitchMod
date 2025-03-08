using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Utils;
using PerfectPitchCore.Constants;
using PerfectPitch.Utils;

namespace PerfectPitch.Game
{
    /// <summary>
    /// Handles voice pitch detection for the Jump King mod (decoupled from audio capture)
    /// </summary>
    public class VoiceJumpHandler : IDisposable
    {
        // P/Invoke for native DLL handling
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        // Core components from PerfectPitchCore
        private IPitchService _pitchService;
        private JumpController _jumpController;
        private bool _isRunning = false;
        private bool _disposed = false;

        // Queue of processors waiting to be registered
        private List<IPitchProcessor> _pendingProcessors = new List<IPitchProcessor>();

        // Event for when a jump should be executed
        public event Action<int> OnJumpTriggered;

        /// <summary>
        /// Initialize voice jump handler (but don't start audio capture yet)
        /// </summary>
        public VoiceJumpHandler(JumpController jumpController = null)
        {
            Log.Info("Initializing voice jump handler");

            try
            {
                // 1. Load native DLLs first
                LoadNativeLibraries();

                // 2. Store the jump controller reference
                if (jumpController != null)
                {
                    _jumpController = jumpController;
                    Log.Info("Using provided jump controller");
                }
                else
                {
                    // Create a new one if needed (fallback)
                    _jumpController = new JumpController();
                    Log.Info("Created new jump controller (not from registry)");
                }

                // 3. Connect to jump events
                _jumpController.OnJumpTriggered += JumpTriggered;

                // 4. Initialize the configuration system
                string assemblyPath = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                ConfigManager.SetConfigPath(assemblyPath);

                // 5. DON'T create the pitch service or register processors yet
                // That will happen in Start() to ensure proper sequencing

                Log.Info("Voice jump handler initialized (audio chain will be created when started)");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize voice jump handler", ex);
            }
        }

        /// <summary>
        /// Start voice pitch detection and actually create the audio chain
        /// </summary>
        /// <summary>
        /// Start voice pitch detection and actually create the audio chain
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;

            try
            {
                Log.Info("Starting voice pitch detection and creating audio chain");

                // 1. Load or create config
                var config = ConfigManager.LoadConfig();
                if (config == null)
                {
                    config = ConfigManager.CreateDefaultConfig();
                    ConfigManager.SaveConfig(config);
                }

                Log.Info($"Using configuration with base pitch: {NoteUtility.GetNoteName(config.BasePitch)} ({config.BasePitch:F2} Hz)");
                Log.Info($"Audio threshold: {config.VolumeThresholdDb} dB");

                // 2. Create pitch service (with clean state)
                _pitchService = new PitchService();

                // 3. Configure the service
                _pitchService.Configure(config);

                // 4. FIRST, register the JumpController (most important)
                _pitchService.RegisterProcessor(_jumpController);
                Log.Info("Jump controller registered with pitch service");

                // CRITICAL FIX: Get visualizer from ModEntry and register it explicitly
                var visualizer = ModEntry.GetVoiceVisualizer();
                if (visualizer != null)
                {
                    _pitchService.RegisterProcessor(visualizer);
                    Log.Info("Voice visualizer explicitly registered with pitch service");
                }

                // 5. Register all pending processors that were queued
                if (_pendingProcessors.Count > 0)
                {
                    Log.Info($"Registering {_pendingProcessors.Count} pending processors:");
                    foreach (var processor in _pendingProcessors)
                    {
                        string processorName = processor.GetType().Name;
                        _pitchService.RegisterProcessor(processor);
                        Log.Info($"- Registered pending processor: {processorName}");
                    }
                    _pendingProcessors.Clear();
                }

                // 6. NOW actually start the pitch service
                _pitchService.Start();
                _isRunning = true;

                // 7. Log available microphones
                var mics = _pitchService.GetAvailableMicrophones();
                Log.Info($"Available microphones ({mics.Count}):");
                for (int i = 0; i < mics.Count; i++)
                {
                    Log.Info($"- {i}: {mics[i]}");
                }

                Log.Info("Voice pitch detection audio chain created and started successfully");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start voice pitch detection", ex);
                _isRunning = false;
            }
        }

        /// <summary>
        /// Stop voice pitch detection
        /// </summary>
        public void Stop()
        {
            if (!_isRunning || _pitchService == null)
                return;

            try
            {
                _pitchService.Stop();
                _isRunning = false;
                Log.Info("Voice pitch detection stopped");
            }
            catch (Exception ex)
            {
                Log.Error("Error stopping voice pitch detection", ex);
            }
        }

        /// <summary>
        /// Called when the jump controller triggers a jump
        /// </summary>
        private void JumpTriggered(int jumpLevel)
        {
            // Log the jump
            Log.Info($"Jump triggered from controller: Level {jumpLevel}");

            // Forward the event
            OnJumpTriggered?.Invoke(jumpLevel);
        }

        /// <summary>
        /// Register a processor with the pitch service
        /// </summary>
        public void RegisterProcessor(IPitchProcessor processor)
        {
            if (processor == null)
            {
                Log.Error("Cannot register null processor");
                return;
            }

            // Get processor name for better logging
            string processorName = processor.GetType().Name;
            Log.Info($"VoiceJumpHandler.RegisterProcessor called with: {processorName}");

            // Store in pending queue if we're not running yet
            if (_pitchService == null || !_isRunning)
            {
                if (!_pendingProcessors.Contains(processor))
                {
                    _pendingProcessors.Add(processor);
                    Log.Info($"Added {processorName} to pending processors queue (will register when started)");
                }
                return;
            }

            // Otherwise register immediately with the running service
            _pitchService.RegisterProcessor(processor);
            Log.Info($"Directly registered processor: {processorName} with active service");
        }

        /// <summary>
        /// Unregister a processor from the pitch service
        /// </summary>
        public void UnregisterProcessor(IPitchProcessor processor)
        {
            if (processor == null)
                return;

            string processorName = processor.GetType().Name;

            // Remove from pending queue if we're not running
            if (_pitchService == null || !_isRunning)
            {
                if (_pendingProcessors.Contains(processor))
                {
                    _pendingProcessors.Remove(processor);
                    Log.Info($"Removed {processorName} from pending processors queue");
                }
                return;
            }

            // Otherwise unregister from the running service
            _pitchService.UnregisterProcessor(processor);
            Log.Info($"Unregistered processor: {processorName} from active service");
        }

        /// <summary>
        /// Gets if the voice pitch detection is active
        /// </summary>
        public bool IsActive => _isRunning;

        /// <summary>
        /// Gets the current audio level (0-1)
        /// </summary>
        public float GetCurrentAudioLevel()
        {
            return _pitchService?.GetCurrentAudioLevel() ?? 0f;
        }

        /// <summary>
        /// Gets the current audio level in decibels
        /// </summary>
        public float GetCurrentAudioLevelDb()
        {
            if (_pitchService == null)
                return -100f;

            float level = _pitchService.GetCurrentAudioLevel();
            return _pitchService.GetCurrentAudioLevelDb();
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
                Log.Info($"Assembly path: {assemblyPath}");
                Log.Info($"Library path: {libPath}");

                // Check if lib directory exists
                if (!System.IO.Directory.Exists(libPath))
                {
                    Log.Info("Creating lib directory");
                    System.IO.Directory.CreateDirectory(libPath);
                }

                // Set the DLL directory to include lib folder
                bool success = SetDllDirectory(libPath);
                Log.Info($"Setting DLL directory to {libPath}: {(success ? "Success" : "Failed")}");

                // Check for DLLs in both locations
                string dywaDllInLib = System.IO.Path.Combine(libPath, "dywapitchtrack.dll");
                string aubioDllInLib = System.IO.Path.Combine(libPath, "libaubio-5.dll");
                string dywaDllInRoot = System.IO.Path.Combine(assemblyPath, "dywapitchtrack.dll");
                string aubioDllInRoot = System.IO.Path.Combine(assemblyPath, "libaubio-5.dll");

                // If DLLs exist in root but not in lib, copy them
                if (System.IO.File.Exists(dywaDllInRoot) && !System.IO.File.Exists(dywaDllInLib))
                {
                    Log.Info("Moving dywapitchtrack.dll from root to lib directory");
                    System.IO.File.Copy(dywaDllInRoot, dywaDllInLib);
                }

                if (System.IO.File.Exists(aubioDllInRoot) && !System.IO.File.Exists(aubioDllInLib))
                {
                    Log.Info("Moving libaubio-5.dll from root to lib directory");
                    System.IO.File.Copy(aubioDllInRoot, aubioDllInLib);
                }

                // Explicitly try to load the DLLs
                if (System.IO.File.Exists(dywaDllInLib))
                {
                    IntPtr dywaDllHandle = LoadLibrary(dywaDllInLib);
                    if (dywaDllHandle != IntPtr.Zero)
                        Log.Info("Successfully loaded dywapitchtrack.dll");
                    else
                        Log.Error($"Failed to load dywapitchtrack.dll, error code: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Log.Error("dywapitchtrack.dll not found in lib directory");
                }

                if (System.IO.File.Exists(aubioDllInLib))
                {
                    IntPtr aubioDllHandle = LoadLibrary(aubioDllInLib);
                    if (aubioDllHandle != IntPtr.Zero)
                        Log.Info("Successfully loaded libaubio-5.dll");
                    else
                        Log.Error($"Failed to load libaubio-5.dll, error code: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Log.Error("libaubio-5.dll not found in lib directory");
                }

                // List all files in the lib directory
                if (System.IO.Directory.Exists(libPath))
                {
                    string[] files = System.IO.Directory.GetFiles(libPath);
                    Log.Info($"Files in lib directory ({files.Length}):");
                    foreach (string file in files)
                    {
                        Log.Info($"  - {System.IO.Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading native libraries: {ex.Message}");
                Log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();

                if (_jumpController != null)
                {
                    _jumpController.OnJumpTriggered -= JumpTriggered;
                    _jumpController = null;
                }

                if (_pitchService != null)
                {
                    _pitchService.Dispose();
                    _pitchService = null;
                }

                _pendingProcessors.Clear();

                _disposed = true;
                Log.Info("Voice jump handler disposed");
            }
        }
    }
}