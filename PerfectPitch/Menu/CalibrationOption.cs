using System;
using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Utils;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Utils;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Menu option for voice calibration
    /// </summary>
    public class CalibrationOption : ITextToggle
    {
        private CalibrationProcessor calibrator;
        private bool isCalibrating = false;
        private string calibrationStatus = "Ready";
        private float currentBasePitch;
        private string currentNoteName;

        public CalibrationOption() : base(false)
        {
            // Get current base pitch from config
            try
            {
                var config = ConfigManager.LoadConfig();
                if (config != null)
                {
                    currentBasePitch = config.BasePitch;
                    currentNoteName = NoteUtility.GetNoteName(currentBasePitch);
                    Log.Info($"Calibration option loaded with base pitch: {currentNoteName} ({currentBasePitch:F2} Hz)");
                }
                else
                {
                    // Default to C3 if no config
                    currentBasePitch = 130.81f;
                    currentNoteName = "C3";
                    Log.Info("Calibration option loaded with default base pitch: C3 (130.81 Hz)");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error loading base pitch for calibration menu", ex);
                currentBasePitch = 130.81f;
                currentNoteName = "C3";
            }
        }

        protected override string GetName()
        {
            if (isCalibrating)
            {
                return $"Calibrating... {calibrationStatus}";
            }

            return $"Calibrate Voice [{currentNoteName}]";
        }

        protected override void OnToggle()
        {
            // Since ITextToggle requires a toggle state, we'll use it as a trigger
            // and then reset back to false
            Log.Info("Calibration toggle activated");

            if (base.toggle)
            {
                // Start calibration when toggled on
                StartCalibration();

                // Reset toggle state after a short delay
                System.Threading.Tasks.Task.Run(async () => {
                    await System.Threading.Tasks.Task.Delay(2000);
                    OverrideToggle(false);
                });
            }
        }

        private void StartCalibration()
        {
            try
            {
                // Check if mod is enabled
                if (!ModEntry.IsEnabled)
                {
                    Log.Info("Cannot calibrate - mod is disabled");
                    return;
                }

                // Check if already calibrating
                if (isCalibrating)
                {
                    Log.Info("Calibration already in progress");
                    return;
                }

                isCalibrating = true;
                calibrationStatus = "Starting...";

                // Create a new calibrator or get the existing one
                if (calibrator == null)
                {
                    calibrator = new CalibrationProcessor();

                    // Subscribe to calibration events
                    calibrator.CalibrationStatusChanged += OnCalibrationStatusChanged;
                    calibrator.CalibrationCompleted += OnCalibrationCompleted;
                    calibrator.CalibrationFailed += OnCalibrationFailed;

                    Log.Info("Created new calibrator for menu option");
                }

                // Tell the mod to start calibration
                ModEntry.StartCalibration(calibrator);

                Log.Info("Calibration started from menu");
            }
            catch (Exception ex)
            {
                Log.Error("Error starting calibration", ex);
                isCalibrating = false;
                calibrationStatus = "Error";
            }
        }

        private void OnCalibrationStatusChanged(string status)
        {
            calibrationStatus = status;
            Log.Info($"Calibration status: {status}");
        }

        private void OnCalibrationCompleted(float basePitch)
        {
            currentBasePitch = basePitch;
            currentNoteName = NoteUtility.GetNoteName(basePitch);

            Log.Info($"Calibration completed: {currentNoteName} ({currentBasePitch:F2} Hz)");

            isCalibrating = false;
            calibrationStatus = "Completed";
        }

        private void OnCalibrationFailed()
        {
            Log.Info("Calibration failed");
            isCalibrating = false;
            calibrationStatus = "Failed";
        }
    }
}