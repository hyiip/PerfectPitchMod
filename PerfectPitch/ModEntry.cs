using System;
using System.Reflection;
using EntityComponent;
using HarmonyLib;
using JumpKing.Mods;
using JumpKing.PauseMenu;
using JumpKing.Player;
using PerfectPitch.Game;
using PerfectPitch.Menu;
using PerfectPitch.Utils;
using PerfectPitchCore.Audio;
using System.Collections.Generic;
using PerfectPitchCore.Constants;
using PerfectPitchCore.Utils;

namespace PerfectPitch
{
    [JumpKingMod("hyiip.PerfectPitch")]
    public static class ModEntry
    {
        // Basic mod state
        public static bool IsEnabled { get; private set; } = true;
        public static bool IsMuted { get; private set; } = false;

        // Audio capture and jump state
        private static Harmony harmony;
        private static JumpState jumpStateInstance;
        private static VoiceJumpHandler voiceJumpHandler;
        private static VoiceVisualizer voiceVisualizer;
        private static JumpController jumpController;
        private static List<IPitchProcessor> registeredProcessors = new List<IPitchProcessor>();

        // Config storage
        private static float ConfiguredAudioThreshold = AppConstants.Audio.DEFAULT_VOLUME_THRESHOLD_DB;
        private static int ConfiguredStabilityHistory = AppConstants.Stability.DEFAULT_HISTORY;
        private static int ConfiguredStabilityThreshold = AppConstants.Stability.DEFAULT_THRESHOLD;
        private static int ConfiguredMicrophoneDevice = 0;

        // Reference to active toggle menu items
        private static ToggleModEnabled activeToggle = null;
        private static ToggleMute activeMuteToggle = null;
        private static ToggleVisualizer activeVisualizerToggle = null;
        private static ToggleGauge activeGaugeToggle = null;
        private static readonly object entityLock = new object();
        private static DateTime lastJumpTime = DateTime.MinValue;
        private static readonly TimeSpan JUMP_COOLDOWN = TimeSpan.FromMilliseconds(1000);

        private static bool restartAudioPending = false;
        private static DateTime unmutedTime = DateTime.MinValue;
        private static readonly TimeSpan RESTART_DELAY = TimeSpan.FromMilliseconds(100);


        private static CalibrationProcessor calibrationProcessor;
        private static GameCalibrationVisualizer calibrationVisualizer;

        private static int queuedJumpLevel = -1;
        private static bool jumpQueued = false;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            try
            {
                // Initialize logging
                Log.Initialize();
                Log.Info("PerfectPitch mod initializing...");

                // Load configuration first
                InitializeFromConfig();

                // Set up harmony
                harmony = new Harmony("hyiip.PerfectPitch.Harmony");
                Log.Info("Harmony initialized");

                // Initialize voice visualizer
                voiceVisualizer = new VoiceVisualizer(harmony);
                Log.Info("Voice visualizer initialized");

                // Create jump controller with configured settings
                jumpController = new JumpController();
                jumpController.SetAudioThreshold(ConfiguredAudioThreshold);
                jumpController.SetStabilityParameters(ConfiguredStabilityHistory, ConfiguredStabilityThreshold);
                jumpController.OnJumpTriggered += OnJumpTriggered;
                Log.Info("Jump controller initialized");

                // Clear any existing processor registrations
                registeredProcessors.Clear();

                // Register processors in central registry
                RegisterProcessor(voiceVisualizer);
                RegisterProcessor(jumpController);

                // Create voice jump handler with jump controller
                voiceJumpHandler = new VoiceJumpHandler(jumpController);

                calibrationProcessor = new CalibrationProcessor();
                calibrationVisualizer = new GameCalibrationVisualizer(calibrationProcessor, harmony);

                // CRITICAL CHANGE: Don't actually start the audio capture yet
                // We'll do that in OnLevelStart instead to ensure proper sequencing

                Log.Info("Core initialization complete - audio will start in OnLevelStart");
            }
            catch (Exception ex)
            {
                Log.Error("Error in BeforeLevelLoad", ex);
            }
        }

        [OnLevelStartAttribute]
        public static void OnLevelStart()
        {
            try
            {
                // Find player entity
                PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();
                if (player == null)
                {
                    Log.Error("Player entity not found!");
                    return;
                }

                // Initialize InputManager for key detection
                InputManager.Initialize(harmony);

                // Subscribe to key events - only mute key
                InputManager.OnMuteKeyPressed += OnMuteKeyPressed;

                // Set up jump state hook
                var jumpStateType = typeof(JumpState);
                var originalMethod = jumpStateType.GetMethod("MyRun", BindingFlags.NonPublic | BindingFlags.Instance);
                var prefix = typeof(ModEntry).GetMethod(nameof(JumpStatePrefix),
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (originalMethod != null && prefix != null)
                {
                    harmony.Patch(originalMethod, new HarmonyMethod(prefix));
                    Log.Info("JumpState.MyRun patched successfully");
                }
                else
                {
                    Log.Error("Failed to patch JumpState.MyRun - method or prefix not found");
                }

                // CRITICAL: Only now do we start the audio chain properly
                // Start voice jump handler if enabled
                if (IsEnabled && !IsMuted)
                {
                    StartAudioCapture();
                }

                Log.Info("PerfectPitch mod activated");
                Log.Info($"Press {AppConstants.VoiceJump.MUTE_KEY} to mute/unmute voice control");
            }
            catch (Exception ex)
            {
                Log.Error("Error in OnLevelStart", ex);
            }
        }

        private static void InitializeFromConfig()
        {
            try
            {
                // Initialize the ConfigManager to ensure config file exists and is valid
                ConfigManager.Initialize();

                // Load config
                var config = ConfigManager.LoadConfig();
                if (config == null)
                {
                    config = ConfigManager.CreateDefaultConfig();
                    ConfigManager.SaveConfig(config);
                }

                // Store config values to use when creating components
                ConfiguredAudioThreshold = config.VolumeThresholdDb;
                ConfiguredStabilityHistory = config.StabilityHistory;
                ConfiguredStabilityThreshold = config.StabilityThreshold;
                ConfiguredMicrophoneDevice = config.DeviceNumber;

                Log.Info($"Loaded config: Threshold={ConfiguredAudioThreshold}dB, " +
                         $"Stability=({ConfiguredStabilityHistory},{ConfiguredStabilityThreshold}), " +
                         $"Microphone={ConfiguredMicrophoneDevice}");
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing from config", ex);
                // Set default values if config loading fails
                ConfiguredAudioThreshold = AppConstants.Audio.DEFAULT_VOLUME_THRESHOLD_DB;
                ConfiguredStabilityHistory = AppConstants.Stability.DEFAULT_HISTORY;
                ConfiguredStabilityThreshold = AppConstants.Stability.DEFAULT_THRESHOLD;
                ConfiguredMicrophoneDevice = 0;
            }
        }

        [OnLevelUnloadAttribute]
        public static void OnLevelUnload()
        {
            // Stop audio capture
            StopAudioCapture();

            // Unsubscribe from events
            InputManager.OnMuteKeyPressed -= OnMuteKeyPressed;
        }

        [OnLevelEndAttribute]
        public static void OnLevelEnd()
        {
            // Stop audio capture
            StopAudioCapture();

            // Unsubscribe from events
            InputManager.OnMuteKeyPressed -= OnMuteKeyPressed;
        }

        // Event handlers
        private static void OnMuteKeyPressed()
        {
            // Toggle mute state
            SetMuted(!IsMuted);
        }

        // Handle jump triggered from voice
        private static void OnJumpTriggered(int jumpLevel)
        {
            if (!IsEnabled || IsMuted)
            {
                Log.Info("Voice jump ignored - mod disabled or muted");
                return;
            }

            // Check if we're within the cooldown period
            TimeSpan timeSinceLastJump = DateTime.Now - lastJumpTime;
            if (timeSinceLastJump < JUMP_COOLDOWN)
            {
                Log.Info($"Jump ignored - cooldown active ({JUMP_COOLDOWN.TotalMilliseconds - timeSinceLastJump.TotalMilliseconds:F0}ms remaining)");
                return;
            }

            // Queue the jump for later execution
            queuedJumpLevel = jumpLevel;
            jumpQueued = true;
            Log.Info($"Voice Jump Queued - Level: {jumpLevel}/{AppConstants.VoiceJump.MAX_JUMP_LEVEL}");
        }

        // Public method to enable/disable the mod completely
        public static void SetModEnabled(bool enabled)
        {
            IsEnabled = enabled;
            Log.Info($"PerfectPitch mod {(enabled ? "enabled" : "disabled")}");

            // Update audio capture state
            if (enabled && !IsMuted)
            {
                StartAudioCapture();
            }
            else
            {
                StopAudioCapture();
            }

            // Update any active toggle menu instance
            if (activeToggle != null)
            {
                activeToggle.OverrideToggle(enabled);
            }
        }

        public static void RegisterProcessor(IPitchProcessor processor)
        {
            if (processor == null)
                return;

            // Store the processor in the central registry
            if (!registeredProcessors.Contains(processor))
            {
                registeredProcessors.Add(processor);
                Log.Info($"Added {processor.GetType().Name} to central registry");
            }

            // Also register with active handler if exists
            if (voiceJumpHandler != null)
            {
                voiceJumpHandler.RegisterProcessor(processor);
                Log.Info($"Registered {processor.GetType().Name} with active VoiceJumpHandler");
            }
        }

        // Public method to mute/unmute voice control
        public static void RecreateAudioProcessingChain()
        {
            Log.Info("Recreating audio processing chain...");

            try
            {
                // First, fully stop and dispose any existing handlers
                if (voiceJumpHandler != null)
                {
                    voiceJumpHandler.Stop();
                    voiceJumpHandler.Dispose();
                    voiceJumpHandler = null;
                }

                // Create a fresh voice handler - pass our jump controller
                voiceJumpHandler = new VoiceJumpHandler(jumpController);

                // Register all processors from the central registry
                foreach (var processor in registeredProcessors)
                {
                    voiceJumpHandler.RegisterProcessor(processor);
                    Log.Info($"Registered {processor.GetType().Name} from central registry");
                }

                // Start the ENTIRE processing chain from scratch if not muted
                if (IsEnabled && !IsMuted)
                {
                    voiceJumpHandler.Start();
                    Log.Info("Voice jump handler started after recreation");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error recreating audio chain: {ex.Message}");
            }
        }

        public static void SetMuted(bool muted)
        {
            // If the state isn't changing, do nothing
            if (IsMuted == muted)
                return;

            IsMuted = muted;
            Log.Info($"Voice control {(muted ? "muted" : "unmuted")}");

            // Update audio capture state
            if (IsEnabled)
            {
                if (muted)
                {
                    // Stop audio capture when muted
                    StopAudioCapture();
                }
                else
                {
                    // Completely recreate the audio chain
                    RecreateAudioProcessingChain();
                }
            }

            // Update any active toggle in the menu
            if (activeMuteToggle != null)
            {
                activeMuteToggle.OverrideMute(muted);
            }
        }

        // Public method to check if mod is enabled
        public static bool IsModEnabled()
        {
            return IsEnabled;
        }

        /// <summary>
        /// Get the voice visualizer instance
        /// </summary>
        public static VoiceVisualizer GetVoiceVisualizer()
        {
            return voiceVisualizer;
        }

        // Start voice jump handler if not already running
        private static void StartAudioCapture()
        {
            Log.Info("Starting audio capture");

            if (voiceJumpHandler == null)
            {
                Log.Error("Cannot start audio capture - voice jump handler is null");
                return;
            }

            if (!voiceJumpHandler.IsActive)
            {
                // SEQUENTIAL INITIALIZATION: Start creates the audio chain AND starts everything
                voiceJumpHandler.Start();
                Log.Info("Voice jump handler started");
            }
        }

        // Stop voice jump handler if running
        private static void StopAudioCapture()
        {
            if (voiceJumpHandler != null && voiceJumpHandler.IsActive)
            {
                voiceJumpHandler.Stop();
                Log.Info("Voice jump handler stopped");
            }
        }

        /// <summary>
        /// Set the microphone device to use
        /// </summary>
        public static void SetMicrophoneDevice(int deviceNumber)
        {
            Log.Info($"Setting microphone device to index {deviceNumber}");

            try
            {
                // Restart audio capture with the new device
                StopAudioCapture();

                // Load the current config
                var config = ConfigManager.LoadConfig();
                if (config == null)
                {
                    config = ConfigManager.CreateDefaultConfig();
                }

                // Update the device number
                config.DeviceNumber = deviceNumber;

                // Save the config
                ConfigManager.SaveConfig(config);

                // Recreate the audio processing chain with new settings
                RecreateAudioProcessingChain();

                // Start audio capture if not muted
                if (IsEnabled && !IsMuted)
                {
                    StartAudioCapture();
                }

                Log.Info($"Updated microphone device to {deviceNumber}");
            }
            catch (Exception ex)
            {
                Log.Error("Error setting microphone device", ex);
            }
        }

        /// <summary>
        /// Set the audio threshold
        /// </summary>
        public static void SetAudioThreshold(float thresholdDb)
        {
            Log.Info($"Setting audio threshold to {thresholdDb}dB");

            try
            {
                // Update the jump controller
                if (jumpController != null)
                {
                    jumpController.SetAudioThreshold(thresholdDb);
                    Log.Info($"Updated jump controller audio threshold to {thresholdDb}dB");
                }
                else
                {
                    Log.Error("Jump controller not initialized");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error setting audio threshold", ex);
            }
        }

        /// <summary>
        /// Set the stability preset
        /// </summary>
        public static void SetStabilityParameters(int history, int threshold)
        {
            Log.Info($"Setting stability parameters to ({history},{threshold})");

            try
            {
                // Update the jump controller
                if (jumpController != null)
                {
                    jumpController.SetStabilityParameters(history, threshold);
                    Log.Info($"Updated jump controller stability parameters to ({history},{threshold})");
                }
                else
                {
                    Log.Error("Jump controller not initialized");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error setting stability parameters", ex);
            }
        }

        // Jump state prefix hook
        private static bool JumpStatePrefix(JumpState __instance, ref BehaviorTree.BTresult __result, BehaviorTree.TickData p_data)
        {
            // Store the jump state instance
            jumpStateInstance = __instance;

            // Execute any queued jumps
            if (jumpQueued && queuedJumpLevel > 0)
            {
                try
                {
                    // Check if we can jump
                    if (CanPlayerJump(jumpStateInstance))
                    {
                        Log.Info($"Executing queued jump: level {queuedJumpLevel}");

                        // Execute the appropriate jump
                        ExecuteFixedFrameJump(queuedJumpLevel);

                        // Update last jump time for cooldown
                        lastJumpTime = DateTime.Now;
                    }
                    else
                    {
                        Log.Info("Cannot execute queued jump - player not in valid jump state");
                    }

                    // Reset queue regardless of execution success
                    jumpQueued = false;
                    queuedJumpLevel = -1;
                }
                catch (Exception ex)
                {
                    Log.Error("Error executing queued jump", ex);

                    // Reset queue to prevent errors on next frame
                    jumpQueued = false;
                    queuedJumpLevel = -1;
                }
            }

            // Let original method run
            return true;
        }

        /// <summary>
        /// Check if the player is in a state where jumping is allowed
        /// </summary>
        private static bool CanPlayerJump(JumpState jumpState)
        {
            if (jumpState == null)
                return false;

            try
            {
                // First check: Try to get the body component and see if it's on ground
                var bodyProperty = jumpState.GetType().GetProperty("body");
                if (bodyProperty != null)
                {
                    var body = bodyProperty.GetValue(jumpState);
                    // Check if player is on ground
                    var isOnGroundProperty = body.GetType().GetProperty("IsOnGround");
                    if (isOnGroundProperty != null)
                    {
                        bool isOnGround = (bool)isOnGroundProperty.GetValue(body);
                        if (!isOnGround)
                        {
                            Log.Debug("Player is not on ground");
                            return false;
                        }
                    }
                }

                // Second check: Make sure the player isn't already in the middle of a jump
                var playerProperty = jumpState.GetType().GetProperty("player");
                if (playerProperty != null)
                {
                    var player = playerProperty.GetValue(jumpState);
                    if (player != null)
                    {
                        var stateProperty = player.GetType().GetProperty("State");
                        if (stateProperty != null)
                        {
                            var state = stateProperty.GetValue(player);
                            // We want to make sure the player is not in a jumping state already
                            if (state != null && state.ToString().Contains("Jump"))
                            {
                                Log.Debug("Player is already in a jump state");
                                return false;
                            }
                        }
                    }
                }

                // If all checks pass, allow the jump
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Error checking if player can jump", ex);
                // Default to allowing the jump if we can't determine
                return true;
            }
        }


        /// <summary>
        /// Get the calibration overlay instance
        /// </summary>
        public static GameCalibrationVisualizer GetCalibrationVisualizer()
        {
            return calibrationVisualizer;
        }

        /// <summary>
        /// Execute a fixed frame jump
        /// </summary>
        public static void ExecuteFixedFrameJump(int frameCount)
        {
            // Don't execute if mod is disabled or muted
            if (!IsEnabled || IsMuted)
            {
                Log.Info("Jump ignored - mod disabled or muted");
                return;
            }

            if (jumpStateInstance == null)
            {
                Log.Error("Cannot execute jump - jump state not initialized");
                return;
            }

            try
            {
                // Check if player is on ground and able to jump
                if (!CanPlayerJump(jumpStateInstance))
                {
                    Log.Info("Cannot jump - player is not on ground or not in a valid jump state");
                    return;
                }

                // Wrap the actual jump execution in a lock
                lock (entityLock)
                {
                    // Log what frame count we're using for the jump
                    Log.Info($"Executing voice jump: Frame {frameCount}/35");

                    // Convert frame count to jump intensity (0-35 frames maps to 0-0.6 seconds)
                    float jumpTimeSeconds = (frameCount / 35.0f) * 0.6f;

                    // Get CHARGE_TIME field (should be around 0.6 seconds)
                    float chargeTime = 0.6f;
                    try
                    {
                        var chargeTimeProperty = jumpStateInstance.GetType().GetProperty("CHARGE_TIME");
                        if (chargeTimeProperty != null)
                        {
                            chargeTime = (float)chargeTimeProperty.GetValue(jumpStateInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Could not get CHARGE_TIME, using default 0.6", ex);
                    }

                    // Calculate jump intensity (0-1)
                    float jumpIntensity = jumpTimeSeconds / chargeTime;
                    jumpIntensity = Math.Min(Math.Max(jumpIntensity, 0.1f), 1.0f);

                    Log.Info($"Frame {frameCount} -> Time {jumpTimeSeconds:F3}s -> Intensity {jumpIntensity:F2}");

                    // Execute jump using DoJump method
                    var doJumpMethod = jumpStateInstance.GetType().GetMethod("DoJump",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                    if (doJumpMethod != null)
                    {
                        doJumpMethod.Invoke(jumpStateInstance, new object[] { jumpIntensity });
                        Log.Info($"Jump executed with intensity: {jumpIntensity:F2}");
                    }
                    else
                    {
                        Log.Error("Could not find DoJump method");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error executing jump", ex);
            }
        }

        // Menu registration
        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static ToggleModEnabled AddToggleModOption(object factory, GuiFormat format)
        {
            // Store reference to the created toggle
            activeToggle = new ToggleModEnabled();
            return activeToggle;
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static ToggleMute AddToggleMuteOption(object factory, GuiFormat format)
        {
            // Store reference to the created toggle
            activeMuteToggle = new ToggleMute();
            return activeMuteToggle;
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static ToggleVisualizer AddToggleVisualizerOption(object factory, GuiFormat format)
        {
            // Store reference to the created toggle
            activeVisualizerToggle = new ToggleVisualizer();
            return activeVisualizerToggle;
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static ToggleGauge AddToggleGaugeOption(object factory, GuiFormat format)
        {
            // Store reference to the created toggle
            activeGaugeToggle = new ToggleGauge();
            return activeGaugeToggle;
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static MicrophoneOption AddMicrophoneOption(object factory, GuiFormat format)
        {
            return new MicrophoneOption();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static ThresholdOption AddThresholdOption(object factory, GuiFormat format)
        {
            return new ThresholdOption();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static StabilityOption AddStabilityOption(object factory, GuiFormat format)
        {
            return new StabilityOption();
        }

        public static void StartCalibration(CalibrationProcessor calibrator)
        {
            try
            {
                Log.Info("Starting voice calibration from ModEntry");

                // If audio is running, we need to temporarily pause it
                bool wasRunning = false;
                if (voiceJumpHandler != null && voiceJumpHandler.IsActive)
                {
                    voiceJumpHandler.Stop();
                    wasRunning = true;
                    Log.Info("Temporarily stopped voice handler for calibration");
                }

                // Verify native libraries if verification method is available
                if (voiceJumpHandler != null &&
                    typeof(VoiceJumpHandler).GetMethod("VerifyNativeLibraries") != null &&
                    !voiceJumpHandler.VerifyNativeLibraries())
                {
                    Log.Error("Failed to verify native audio libraries - calibration cannot proceed");
                    return;
                }

                // Set the pitch manager in the calibrator
                var pitchManager = voiceJumpHandler?.GetPitchManager();
                if (pitchManager != null)
                {
                    calibrator.SetPitchManager(pitchManager);
                    Log.Info("Set pitch manager in calibrator");
                }

                // Create the visualizer OUTSIDE any conditional blocks so it's in scope for the event handlers
                // Use our new GameCalibrationVisualizer class instead of the old CalibrationVisualizer
                var tempVisualizer = new GameCalibrationVisualizer(calibrator, harmony);

                // Verify audio devices if verification method is available
                if (calibrator != null &&
                    typeof(CalibrationProcessor).GetMethod("VerifyAudioDevices") != null &&
                    !calibrator.VerifyAudioDevices())
                {
                    Log.Error("No valid microphones detected - calibration cannot proceed");
                    return;
                }

                // Register the calibrator as a processor
                if (voiceJumpHandler != null)
                {
                    voiceJumpHandler.RegisterProcessor(calibrator);
                    voiceJumpHandler.RegisterProcessor(tempVisualizer);
                    Log.Info("Registered calibration processors with voice jump handler");
                }
                else
                {
                    Log.Error("Cannot register calibration processors - voice jump handler is null");
                }

                // Start the audio capture if it's not already running
                if (voiceJumpHandler == null)
                {
                    // Create a new handler just for calibration
                    voiceJumpHandler = new VoiceJumpHandler(jumpController);
                    Log.Info("Created new voice jump handler for calibration");

                    // Register with the new handler too
                    voiceJumpHandler.RegisterProcessor(calibrator);
                    voiceJumpHandler.RegisterProcessor(tempVisualizer);
                }

                // Start the audio capture
                voiceJumpHandler.Start();
                Log.Info("Started audio capture for calibration");

                // Start the calibration process
                calibrator.StartCalibration();
                Log.Info("Calibration process started");

                // Show the visualizer
                tempVisualizer.Show();

                // Add event handler to cleanup when calibration is done
                Action<float> cleanupHandler = null;
                cleanupHandler = basePitch => {
                    // Remove the event handler to avoid memory leaks
                    calibrator.CalibrationCompleted -= cleanupHandler;

                    // Clean up after calibration - use voiceJumpHandler to unregister
                    if (voiceJumpHandler != null)
                    {
                        voiceJumpHandler.UnregisterProcessor(tempVisualizer);
                        Log.Info("Unregistered calibration visualizer");
                    }

                    // Hide the visualizer
                    tempVisualizer.Hide();

                    // Keep the calibrator registered as it might be needed later

                    // Restart normal operation if it was running before
                    if (wasRunning && IsEnabled && !IsMuted)
                    {
                        voiceJumpHandler.Start();
                        Log.Info("Restarted voice handler after calibration");
                    }

                    Log.Info($"Calibration completed with base pitch: {basePitch:F2} Hz");
                };

                calibrator.CalibrationCompleted += cleanupHandler;

                // Also handle failure case
                Action failureHandler = null;
                failureHandler = () => {
                    // Remove the event handler to avoid memory leaks
                    calibrator.CalibrationFailed -= failureHandler;

                    // Clean up after calibration - use voiceJumpHandler to unregister
                    if (voiceJumpHandler != null)
                    {
                        voiceJumpHandler.UnregisterProcessor(tempVisualizer);
                        Log.Info("Unregistered calibration visualizer");
                    }

                    // Hide the visualizer
                    tempVisualizer.Hide();

                    // Restart normal operation if it was running before
                    if (wasRunning && IsEnabled && !IsMuted)
                    {
                        voiceJumpHandler.Start();
                        Log.Info("Restarted voice handler after calibration failure");
                    }

                    Log.Info("Calibration failed, cleaned up resources");
                };

                calibrator.CalibrationFailed += failureHandler;
            }
            catch (Exception ex)
            {
                Log.Error("Error starting calibration from ModEntry", ex);
            }
        }


        // Add this to register the CalibrationOption in the menus
        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static CalibrationOption AddCalibrationOption(object factory, GuiFormat format)
        {
            return new CalibrationOption();
        }
    }
}