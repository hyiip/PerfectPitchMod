using System;
using System.Threading;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Utils;

namespace PerfectPitchTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("PerfectPitch Test Program");
            Console.WriteLine("=======================");

            // Set logging verbosity - disable all verbose logging
            PitchDetectorBase.VerboseDebugLogging = false;
            JumpLevelCalculator.EnableDetailedLogging = false;
            CalibrationProcessor.VerboseCalibrationLogging = false;

            try
            {
                // Load configuration or create default
                Console.WriteLine("Loading configuration...");
                var config = ConfigManager.LoadConfig();

                // Override with command line parameters if provided
                Console.WriteLine("\nCurrent configuration:");
                Console.WriteLine($"Microphone: Device #{config.DeviceNumber}");
                Console.WriteLine($"Algorithm: {config.Algorithm}");
                Console.WriteLine($"Pitch sensitivity: {config.PitchSensitivity}");
                Console.WriteLine($"Volume threshold: {config.VolumeThresholdDb} dB");
                Console.WriteLine($"Base note: {NoteUtility.GetNoteName(config.BasePitch)} ({config.BasePitch:F2} Hz)");

                Console.WriteLine("\nWould you like to change these settings? (Y/N, default N):");
                string input = Console.ReadLine();
                if (!string.IsNullOrEmpty(input) && input.ToUpper().StartsWith("Y"))
                {
                    // Display available microphones
                    Console.WriteLine("\nAvailable microphones:");
                    var microphones = PitchManager.GetAvailableMicrophones();
                    for (int i = 0; i < microphones.Count; i++)
                    {
                        Console.WriteLine(microphones[i]);
                    }

                    // Get user input for microphone
                    Console.Write($"\nSelect microphone (number, default {config.DeviceNumber}): ");
                    input = Console.ReadLine();
                    if (!string.IsNullOrEmpty(input) && int.TryParse(input, out int deviceNumber))
                    {
                        config.DeviceNumber = deviceNumber;
                    }

                    // Get user input for algorithm
                    string algorithm = GetAlgorithmFromUser(config.Algorithm);
                    config.Algorithm = algorithm;

                    // Get other configuration values
                    config.PitchSensitivity = GetFloatFromUser($"Select pitch detection sensitivity (0.0-1.0)", config.PitchSensitivity);
                    config.VolumeThresholdDb = GetFloatFromUser($"Select volume threshold in dB (-60 to -20)", config.VolumeThresholdDb);
                    config.MaxFrequency = GetFloatFromUser($"Enter maximum frequency to detect in Hz (80-2000)", config.MaxFrequency);
                    config.MaxFrequency = Math.Min(Math.Max(config.MaxFrequency, 80.0f), 2000.0f);

                    if (config.Algorithm.ToLower() == "dywa")
                    {
                        config.MinFrequency = (int)GetFloatFromUser($"Enter minimum frequency to detect in Hz (30-200)", config.MinFrequency);
                        config.MinFrequency = Math.Min(Math.Max(config.MinFrequency, 30), 200);
                    }
                }

                // Create and start pitch manager
                using (var pitchManager = new PitchManager(config))
                {
                    // Set up calibration processor and visualizer
                    var calibrator = new CalibrationProcessor();
                    var calibrationUI = new CalibrationVisualizer(calibrator);

                    // Connect the calibrator to the pitch manager for recalibration support
                    calibrator.SetPitchManager(pitchManager);

                    // Register calibration components
                    pitchManager.RegisterProcessor(calibrator);
                    pitchManager.RegisterProcessor(calibrationUI);

                    // Start audio processing
                    pitchManager.Start();

                    // Check if we're receiving audio after a few seconds
                    Thread.Sleep(2000);

                    if (pitchManager.GetCurrentAudioLevel() < 0.001f)
                    {
                        Console.WriteLine("\n\nWARNING: No audio input detected! Please check your microphone.");
                        Console.WriteLine("1. Validate the selected microphone is the one you're using");
                        Console.WriteLine("2. Check if your microphone is muted in Windows settings");
                        Console.WriteLine("3. Try a different microphone");

                        Console.WriteLine("\nPress any key to continue anyway...");
                        Console.ReadKey(true);
                    }

                    // Run calibration or use default
                    bool runCalibration = true;
                    while (runCalibration)
                    {
                        Console.WriteLine("\nDo you want to calibrate your voice? (Y/N, default Y):");
                        input = Console.ReadLine();

                        if (string.IsNullOrEmpty(input) || input.ToUpper().StartsWith("Y"))
                        {
                            // Start calibration
                            calibrationUI.StartCalibration();

                            // Wait for calibration to complete
                            bool waitingForCalibration = true;
                            while (waitingForCalibration)
                            {
                                if (Console.KeyAvailable)
                                {
                                    var key = Console.ReadKey(true);
                                    if (key.Key == ConsoleKey.R)
                                    {
                                        // Restart calibration
                                        calibrationUI.StartCalibration();
                                    }
                                    else
                                    {
                                        waitingForCalibration = false;
                                    }
                                }
                                Thread.Sleep(100);
                            }

                            // Update the base pitch with calibrated value
                            float basePitch = calibrator.GetCalibratedBasePitch();
                            string baseNoteName = calibrator.GetCalibratedNoteName();

                            // IMPORTANT: Update the configuration with the new base pitch
                            config.BasePitch = basePitch;
                            pitchManager.UpdateConfiguration(config);

                            // Save the updated configuration
                            ConfigManager.SaveConfig(config);

                            Console.WriteLine($"\nCalibration complete! Base pitch set to {baseNoteName} ({basePitch:F2} Hz)");
                            Console.WriteLine($"Configuration updated and saved.");

                            Console.WriteLine("Do you want to calibrate again? (Y/N, default N):");
                            input = Console.ReadLine();

                            if (string.IsNullOrEmpty(input) || !input.ToUpper().StartsWith("Y"))
                            {
                                runCalibration = false;
                            }
                            // If Y, we'll loop back and calibrate again
                        }
                        else
                        {
                            runCalibration = false;
                        }
                    }

                    // Remove calibration processors
                    pitchManager.UnregisterProcessor(calibrator);
                    pitchManager.UnregisterProcessor(calibrationUI);

                    // Add standard processors for testing
                    pitchManager.RegisterProcessor(new DebugVisualizer());
                    var jumpController = new JumpControllerSimulator();
                    pitchManager.RegisterProcessor(jumpController);

                    // Print setup info
                    Console.Clear();
                    Console.WriteLine("\nStarting pitch detection. Sing or hum into your microphone.");
                    Console.WriteLine($"Algorithm: {config.Algorithm}");
                    Console.WriteLine($"Pitch sensitivity: {config.PitchSensitivity}");
                    Console.WriteLine($"Volume threshold: {config.VolumeThresholdDb} dB");
                    Console.WriteLine($"Maximum frequency: {config.MaxFrequency} Hz");
                    if (config.Algorithm.ToLower() == "dywa")
                    {
                        Console.WriteLine($"Minimum frequency: {config.MinFrequency} Hz");
                    }
                    Console.WriteLine($"Base note: {NoteUtility.GetNoteName(config.BasePitch)} ({config.BasePitch:F2} Hz)");
                    Console.WriteLine("Press Q to quit, C to recalibrate\n");

                    // Wait for user to quit
                    bool running = true;
                    while (running)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Q)
                            {
                                running = false;
                            }
                            else if (key.Key == ConsoleKey.C)
                            {
                                // Re-register calibration components
                                pitchManager.UnregisterProcessor(jumpController);
                                pitchManager.RegisterProcessor(calibrator);
                                pitchManager.RegisterProcessor(calibrationUI);

                                Console.Clear();
                                Console.WriteLine("RECALIBRATION MODE - Sing your lowest comfortable note");

                                // Start calibration
                                calibrationUI.StartCalibration();

                                // Wait for calibration to complete
                                bool waitingForCalibration = true;
                                while (waitingForCalibration)
                                {
                                    if (Console.KeyAvailable)
                                    {
                                        var calKey = Console.ReadKey(true);
                                        if (calKey.Key == ConsoleKey.R)
                                        {
                                            // Restart calibration
                                            calibrationUI.StartCalibration();
                                        }
                                        else
                                        {
                                            waitingForCalibration = false;
                                        }
                                    }
                                    Thread.Sleep(100);
                                }

                                // Update the base pitch with calibrated value
                                float basePitch = calibrator.GetCalibratedBasePitch();
                                string baseNoteName = calibrator.GetCalibratedNoteName();

                                // IMPORTANT: Update the configuration with the new base pitch
                                config.BasePitch = basePitch;
                                pitchManager.UpdateConfiguration(config);

                                // Save the updated configuration
                                ConfigManager.SaveConfig(config);

                                // Clear UI
                                Console.Clear();
                                Console.WriteLine($"\nRecalibration complete! Base pitch set to {baseNoteName} ({basePitch:F2} Hz)");
                                Console.WriteLine($"Configuration updated and saved.");

                                // Remove calibration processors
                                pitchManager.UnregisterProcessor(calibrator);
                                pitchManager.UnregisterProcessor(calibrationUI);

                                // Restore jump controller
                                pitchManager.RegisterProcessor(jumpController);

                                // Print setup info
                                Console.WriteLine("\nResuming pitch detection. Sing or hum into your microphone.");
                                Console.WriteLine($"Algorithm: {config.Algorithm}");
                                Console.WriteLine($"Pitch sensitivity: {config.PitchSensitivity}");
                                Console.WriteLine($"Volume threshold: {config.VolumeThresholdDb} dB");
                                Console.WriteLine($"Maximum frequency: {config.MaxFrequency} Hz");
                                if (config.Algorithm.ToLower() == "dywa")
                                {
                                    Console.WriteLine($"Minimum frequency: {config.MinFrequency} Hz");
                                }
                                Console.WriteLine($"Base note: {baseNoteName} ({basePitch:F2} Hz)");
                                Console.WriteLine("Press Q to quit, C to recalibrate\n");
                            }
                        }
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }

        private static string GetAlgorithmFromUser(string defaultAlgorithm = "dywa")
        {
            Console.Write("\nSelect pitch detection algorithm:\n");
            Console.WriteLine("1. YIN (better for voice, more CPU intensive)");
            Console.WriteLine("2. YINFFT (fast Fourier transform version of YIN)");
            Console.WriteLine("3. MCOMB (multi-comb filter)");
            Console.WriteLine("4. FCOMB (fast comb filter)");
            Console.WriteLine("5. SCHMITT (simple threshold method)");
            Console.WriteLine("6. DYWA (dynamic wavelet algorithm)");

            // Show the current default
            string defaultChoice = "6"; // DYWA
            switch (defaultAlgorithm.ToLower())
            {
                case "yin": defaultChoice = "1"; break;
                case "yinfft": defaultChoice = "2"; break;
                case "mcomb": defaultChoice = "3"; break;
                case "fcomb": defaultChoice = "4"; break;
                case "schmitt": defaultChoice = "5"; break;
                case "dywa": defaultChoice = "6"; break;
            }

            Console.Write($"Enter choice (default {defaultChoice}): ");

            string input = Console.ReadLine();
            string algorithm = defaultAlgorithm;

            if (!string.IsNullOrEmpty(input))
            {
                switch (input.Trim())
                {
                    case "1": algorithm = "yin"; break;
                    case "2": algorithm = "yinfft"; break;
                    case "3": algorithm = "mcomb"; break;
                    case "4": algorithm = "fcomb"; break;
                    case "5": algorithm = "schmitt"; break;
                    case "6": algorithm = "dywa"; break;
                }
            }

            return algorithm;
        }

        private static float GetFloatFromUser(string prompt, float defaultValue)
        {
            Console.Write($"{prompt} (default {defaultValue}): ");
            string input = Console.ReadLine();
            return string.IsNullOrEmpty(input) ? defaultValue : float.Parse(input);
        }
    }

    /// <summary>
    /// Simple simulator for the jump controller that just logs jump events
    /// </summary>
    class JumpControllerSimulator : IPitchProcessor
    {
        private int lastJumpLevel = -1;
        private DateTime lastJumpTime = DateTime.MinValue;
        private const double MIN_JUMP_INTERVAL_MS = 200; // Debounce interval

        public void ProcessPitch(PitchData pitchData)
        {
            // Apply debouncing logic
            DateTime now = DateTime.Now;
            double timeSinceLastJump = (now - lastJumpTime).TotalMilliseconds;

            if (pitchData.JumpLevel != lastJumpLevel && timeSinceLastJump >= MIN_JUMP_INTERVAL_MS)
            {
                Console.WriteLine($"\nJUMP! Level: {pitchData.JumpLevel}, Note: {pitchData.NoteName}, Pitch: {pitchData.Pitch:F1} Hz");

                // Update state
                lastJumpLevel = pitchData.JumpLevel;
                lastJumpTime = now;
            }
        }
    }
}