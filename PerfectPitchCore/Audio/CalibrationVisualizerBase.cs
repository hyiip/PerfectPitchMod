using System;
using System.Collections.Generic;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Base class for calibration visualizers that handles common functionality
    /// </summary>
    public abstract class CalibrationVisualizerBase : ICalibrationVisualizer
    {
        protected readonly CalibrationProcessor _calibrator;
        protected string _status = "Ready";
        protected int _countdown = 0;
        protected bool _isVisible = false;
        protected float _currentPitch = 0f;
        protected string _currentNote = "---";
        protected List<float> _pitchSamples = new List<float>();
        protected float _baseFrequency = 130.81f;  // Default C3
        protected string _baseNoteName = "C3";
        protected bool _hasError = false;
        protected string _errorMessage = "";
        public bool ReceiveAllAudioEvents => true; // Always receive all audio events

        /// <summary>
        /// Constructor - connects to the calibration processor's events
        /// </summary>
        public CalibrationVisualizerBase(CalibrationProcessor calibrator)
        {
            _calibrator = calibrator;

            try
            {
                // Subscribe to calibration events
                _calibrator.CalibrationStatusChanged += OnCalibrationStatusChanged;
                _calibrator.CountdownTick += OnCountdownTick;
                _calibrator.CalibrationCompleted += OnCalibrationCompleted;
                _calibrator.CalibrationFailed += OnCalibrationFailed;
                _calibrator.CalibrationDataCollected += OnCalibrationDataCollected;
                _calibrator.PitchDetected += OnPitchDetected;

                CoreLogger.Info("CalibrationVisualizerBase initialized");
            }
            catch (Exception ex)
            {
                CoreLogger.Error("Error initializing CalibrationVisualizerBase", ex);
            }
        }

        // IPitchProcessor implementation
        public virtual void ProcessPitch(PitchData pitchData)
        {
            try
            {
                _currentPitch = pitchData.Pitch;
                _currentNote = pitchData.NoteName;

                // Only update the UI if we're visible
                if (_isVisible)
                {
                    OnUpdateUI();
                }
            }
            catch (Exception ex)
            {
                CoreLogger.Error("Error processing pitch in visualizer", ex);
            }
        }

        // ICalibrationVisualizer implementation
        public virtual void Show()
        {
            _isVisible = true;
            CoreLogger.Info("Calibration visualizer shown");
        }

        public virtual void Hide()
        {
            _isVisible = false;
            CoreLogger.Info("Calibration visualizer hidden");
        }

        public virtual void UpdateStatus(string status)
        {
            _status = status;
            CoreLogger.Info($"Visualizer status updated: {status}");

            if (_isVisible)
            {
                OnUpdateUI();
            }
        }

        public virtual void UpdateCountdown(int countdown)
        {
            _countdown = countdown;
            CoreLogger.Info($"Visualizer countdown updated: {countdown}");

            if (_isVisible)
            {
                OnUpdateUI();
            }
        }

        public virtual void DisplayResults(float basePitch, string noteName)
        {
            _baseFrequency = basePitch;
            _baseNoteName = noteName;
            _hasError = false;

            CoreLogger.Info($"Visualizer displaying results: {noteName} ({basePitch:F2} Hz)");

            if (_isVisible)
            {
                OnUpdateUI();
            }
        }

        public virtual void DisplayError(string errorMessage)
        {
            _errorMessage = errorMessage;
            _hasError = true;

            CoreLogger.Error($"Visualizer displaying error: {errorMessage}");

            if (_isVisible)
            {
                OnUpdateUI();
            }
        }

        public virtual void DisplayPitchData(List<float> pitchData)
        {
            _pitchSamples = new List<float>(pitchData);

            CoreLogger.Info($"Visualizer displaying pitch data ({pitchData.Count} samples)");

            if (_isVisible)
            {
                OnUpdateUI();
            }
        }

        // Event handlers
        protected virtual void OnCalibrationStatusChanged(string status)
        {
            UpdateStatus(status);
        }

        protected virtual void OnCountdownTick(int countdown)
        {
            UpdateCountdown(countdown);
        }

        protected virtual void OnCalibrationCompleted(float basePitch)
        {
            string noteName = NoteUtility.GetNoteName(basePitch);
            DisplayResults(basePitch, noteName);
        }

        protected virtual void OnCalibrationFailed()
        {
            DisplayError("Calibration failed");
        }

        protected virtual void OnCalibrationDataCollected(List<float> pitchData)
        {
            DisplayPitchData(pitchData);
        }

        protected virtual void OnPitchDetected(float pitch)
        {
            // This is handled by ProcessPitch
        }

        // Abstract method that derived classes must implement
        protected abstract void OnUpdateUI();

        // Clean up when done
        public virtual void Dispose()
        {
            try
            {
                // Unsubscribe from events to prevent memory leaks
                if (_calibrator != null)
                {
                    _calibrator.CalibrationStatusChanged -= OnCalibrationStatusChanged;
                    _calibrator.CountdownTick -= OnCountdownTick;
                    _calibrator.CalibrationCompleted -= OnCalibrationCompleted;
                    _calibrator.CalibrationFailed -= OnCalibrationFailed;
                    _calibrator.CalibrationDataCollected -= OnCalibrationDataCollected;
                    _calibrator.PitchDetected -= OnPitchDetected;
                }

                CoreLogger.Info("CalibrationVisualizerBase disposed");
            }
            catch (Exception ex)
            {
                CoreLogger.Error("Error disposing CalibrationVisualizerBase", ex);
            }
        }
    }
}