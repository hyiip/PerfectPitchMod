using System;
using System.Collections.Generic;
using System.Linq;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Utils;

namespace PerfectPitchTest
{
    /// <summary>
    /// Console-specific implementation of the calibration visualizer
    /// </summary>
    public class ConsoleCalibrationVisualizer : CalibrationVisualizerBase
    {
        private const int DISPLAY_WIDTH = 60;
        private bool hasConsoleWindow;
        
        public ConsoleCalibrationVisualizer(CalibrationProcessor calibrator) : base(calibrator)
        {
            // Check if we have a valid console window
            try
            {
                // These operations will throw an exception if no console is attached
                var windowWidth = Console.WindowWidth;
                var windowHeight = Console.WindowHeight;
                hasConsoleWindow = true;
                
                CoreLogger.Info("ConsoleCalibrationVisualizer initialized with console window");
            }
            catch
            {
                hasConsoleWindow = false;
                CoreLogger.Warning("ConsoleCalibrationVisualizer initialized without console window");
            }
        }
        
        protected override void OnUpdateUI()
        {
            if (!hasConsoleWindow)
            {
                // Just log the status without trying to manipulate the console
                CoreLogger.Info($"Calibration Status: {_status}, Countdown: {_countdown}");
                if (_currentPitch > 0)
                {
                    CoreLogger.Info($"Current Pitch: {_currentPitch:F1} Hz ({_currentNote})");
                }
                return;
            }
            
            try
            {
                // Safe clear - doesn't use Console.Clear which can cause issues
                SafeClearConsole();
                
                Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                   PERFECT PITCH CALIBRATION                      ║");
                Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");

                // Status display
                Console.WriteLine($"║ Status: {PadOrTruncate(_status, DISPLAY_WIDTH - 10)}║");

                // Countdown display if active
                if (_countdown > 0)
                {
                    string countdown = $"GET READY: {_countdown}";
                    Console.WriteLine($"║ {CenterText(countdown, DISPLAY_WIDTH - 4)}║");
                }

                Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");

                // Pitch visualization
                Console.WriteLine("║ Current Pitch:                                                  ║");

                if (_currentPitch > 0)
                {
                    // Create histogram-like visualization
                    string pitchBar = GetPitchVisualization(60.0f, 500.0f);
                    Console.WriteLine($"║ {pitchBar}║");

                    // Create note visualization
                    string noteBar = GetNoteVisualization(60.0f, 500.0f);
                    Console.WriteLine($"║ {noteBar}║");
                }
                else
                {
                    Console.WriteLine("║ No pitch detected yet...                                       ║");
                    Console.WriteLine("║                                                                ║");
                }

                Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");

                // Instructions or results
                if (_hasError)
                {
                    Console.WriteLine($"║ Error: {PadOrTruncate(_errorMessage, DISPLAY_WIDTH - 10)}║");
                    Console.WriteLine("║                                                                ║");
                }
                else if (_calibrator.GetCalibratedBasePitch() > 0)
                {
                    Console.WriteLine($"║ Calibrated Base Note: {_baseNoteName} ({_baseFrequency:F2} Hz)                    ║");
                    Console.WriteLine("║                                                                ║");
                    Console.WriteLine("║ Press 'R' to recalibrate or any other key to continue         ║");
                }
                else
                {
                    Console.WriteLine("║ Instructions:                                                  ║");
                    Console.WriteLine("║ * Sing your lowest comfortable note and hold it steady         ║");
                    Console.WriteLine("║ * Try to maintain the same pitch throughout the recording      ║");
                }

                Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                // If console operations fail, fall back to simple logging
                CoreLogger.Error("Error updating console UI", ex);
                hasConsoleWindow = false;
            }
        }
        
        /// <summary>
        /// Clear the console without using Console.Clear() which can cause issues
        /// </summary>
        private void SafeClearConsole()
        {
            try
            {
                // Try to determine if we have a valid console window
                int width = Console.WindowWidth;
                int height = Console.WindowHeight;
                
                // Move cursor to top-left corner
                Console.SetCursorPosition(0, 0);
                
                // Write blank lines to "clear" the console
                string blank = new string(' ', width);
                for (int i = 0; i < height; i++)
                {
                    Console.WriteLine(blank);
                }
                
                // Reset cursor position
                Console.SetCursorPosition(0, 0);
            }
            catch
            {
                // If this fails, we don't have a proper console window
                hasConsoleWindow = false;
            }
        }
        
        /// <summary>
        /// Generate a visualization of the current pitch
        /// </summary>
        private string GetPitchVisualization(float minFreq, float maxFreq)
        {
            int barWidth = DISPLAY_WIDTH - 4;
            char[] bar = new char[barWidth];

            // Initialize empty bar
            for (int i = 0; i < barWidth; i++)
                bar[i] = ' ';

            // Get the last pitch
            float lastPitch = _currentPitch;
            
            if (lastPitch > 0)
            {
                // Calculate position in the bar
                float normalizedPos = (float)Math.Log(lastPitch / minFreq, 2) /
                                      (float)Math.Log(maxFreq / minFreq, 2);
                int pos = (int)(normalizedPos * barWidth);

                // Clamp to valid range
                pos = Math.Max(0, Math.Min(pos, barWidth - 1));

                // Mark position
                bar[pos] = '█';
            }

            // Add pitch value to the end
            string freqText = lastPitch > 0 ? $"{lastPitch:F1} Hz" : "---";
            int textPos = Math.Max(0, barWidth - freqText.Length);

            // Combine everything
            string barString = new string(bar);
            return barString.Substring(0, textPos) + freqText;
        }

        /// <summary>
        /// Generate a visualization of the note names
        /// </summary>
        private string GetNoteVisualization(float minFreq, float maxFreq)
        {
            int barWidth = DISPLAY_WIDTH - 4;
            char[] bar = new char[barWidth];

            // Initialize empty bar
            for (int i = 0; i < barWidth; i++)
                bar[i] = ' ';

            // Add note markers
            string[] noteNames = { "C", "D", "E", "F", "G", "A", "B" };

            for (int octave = 2; octave <= 5; octave++)
            {
                foreach (string note in noteNames)
                {
                    string noteName = $"{note}{octave}";
                    float noteFreq = NoteUtility.GetFrequencyFromNoteName(noteName);

                    if (noteFreq > minFreq && noteFreq < maxFreq)
                    {
                        float normalizedPos = (float)Math.Log(noteFreq / minFreq, 2) /
                                             (float)Math.Log(maxFreq / minFreq, 2);
                        int pos = (int)(normalizedPos * barWidth);

                        // Clamp to valid range
                        pos = Math.Max(0, Math.Min(pos, barWidth - 1));

                        // Add note marker if there's space
                        if (pos < barWidth - 2)
                        {
                            bar[pos] = noteName[0];
                            if (noteName.Length > 1 && pos + 1 < barWidth)
                                bar[pos + 1] = noteName[1];
                        }
                    }
                }
            }

            // Add current note
            string noteText = !string.IsNullOrEmpty(_currentNote) ? _currentNote : "---";
            int textPos = Math.Max(0, barWidth - noteText.Length);

            // Combine everything
            string barString = new string(bar);
            return barString.Substring(0, textPos) + noteText;
        }
        
        /// <summary>
        /// Pad or truncate a string to the specified length
        /// </summary>
        private string PadOrTruncate(string text, int length)
        {
            if (string.IsNullOrEmpty(text))
                return new string(' ', length);
                
            if (text.Length > length)
                return text.Substring(0, length);
                
            return text.PadRight(length);
        }

        /// <summary>
        /// Center a text within the specified width
        /// </summary>
        private string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text))
                return new string(' ', width);
                
            int padding = (width - text.Length) / 2;
            return text.PadLeft(padding + text.Length).PadRight(width);
        }
    }
}