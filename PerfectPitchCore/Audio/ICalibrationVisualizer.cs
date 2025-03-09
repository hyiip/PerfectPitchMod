using System;
using System.Collections.Generic;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Interface for calibration visualizers that can be implemented differently
    /// for console and game environments
    /// </summary>
    public interface ICalibrationVisualizer : IPitchProcessor
    {
        /// <summary>
        /// Show the calibration UI
        /// </summary>
        void Show();

        /// <summary>
        /// Hide the calibration UI
        /// </summary>
        void Hide();

        /// <summary>
        /// Update the calibration status text
        /// </summary>
        void UpdateStatus(string status);

        /// <summary>
        /// Update the countdown value
        /// </summary>
        void UpdateCountdown(int countdown);

        /// <summary>
        /// Display calibration results
        /// </summary>
        void DisplayResults(float basePitch, string noteName);

        /// <summary>
        /// Display an error message
        /// </summary>
        void DisplayError(string errorMessage);

        /// <summary>
        /// Display collected pitch data
        /// </summary>
        void DisplayPitchData(List<float> pitchData);
    }
}