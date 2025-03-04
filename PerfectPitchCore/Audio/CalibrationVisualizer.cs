using System;
using System.Collections.Generic;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Console-based UI for calibration process
    /// </summary>
    public class CalibrationVisualizer : IPitchProcessor
    {
        private readonly CalibrationProcessor calibrator;
        private readonly Queue<float> recentPitches = new Queue<float>();
        private const int MAX_RECENT_PITCHES = 20;
        private const int DISPLAY_WIDTH = 60;
        private string currentStatus = "Ready to calibrate";
        private int countdownValue = 0;
        private bool isCalibrating = false;

        public CalibrationVisualizer(CalibrationProcessor calibrator)
        {
            this.calibrator = calibrator;

            // Subscribe to calibration events
            calibrator.CalibrationStatusChanged += status =>
            {
                currentStatus = status;
                UpdateDisplay();
            };

            calibrator.PitchDetected += pitch =>
            {
                AddPitch(pitch);
                UpdateDisplay();
            };

            calibrator.CountdownTick += countdown =>
            {
                countdownValue = countdown;
                UpdateDisplay();
            };

            calibrator.CalibrationCompleted += _ =>
            {
                isCalibrating = false;
                UpdateDisplay();
            };

            calibrator.CalibrationFailed += () =>
            {
                isCalibrating = false;
                UpdateDisplay();
            };
        }

        /// <summary>
        /// Start the calibration process
        /// </summary>
        public void StartCalibration()
        {
            isCalibrating = true;
            recentPitches.Clear();
            calibrator.StartCalibration();
            UpdateDisplay();
        }

        /// <summary>
        /// Process incoming pitch data
        /// </summary>
        public void ProcessPitch(PitchData pitchData)
        {
            // We're only visualizing calibration, not processing regular pitch data
            if (!isCalibrating) return;

            // Add the visualizer's own view of the pitch
            AddPitch(pitchData.Pitch);
        }

        /// <summary>
        /// Add a pitch to the visualization queue
        /// </summary>
        private void AddPitch(float pitch)
        {
            if (pitch > 0)
            {
                recentPitches.Enqueue(pitch);
                while (recentPitches.Count > MAX_RECENT_PITCHES)
                    recentPitches.Dequeue();
            }
        }

        /// <summary>
        /// Update the console display
        /// </summary>
        private void UpdateDisplay()
        {
            Console.Clear();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                   PERFECT PITCH CALIBRATION                      ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");

            // Status display
            Console.WriteLine($"║ Status: {PadOrTruncate(currentStatus, DISPLAY_WIDTH - 10)}║");

            // Countdown display if active
            if (countdownValue > 0)
            {
                string countdown = $"GET READY: {countdownValue}";
                Console.WriteLine($"║ {CenterText(countdown, DISPLAY_WIDTH - 4)}║");
            }

            Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");

            // Pitch visualization
            Console.WriteLine("║ Current Pitch:                                                  ║");

            if (recentPitches.Count > 0)
            {
                // Calculate frequency range for visualization
                float minFreq = 60.0f;
                float maxFreq = 500.0f;

                // Create histogram-like visualization
                string pitchBar = GetPitchVisualization(minFreq, maxFreq);
                Console.WriteLine($"║ {pitchBar}║");

                // Create note visualization
                string noteBar = GetNoteVisualization(minFreq, maxFreq);
                Console.WriteLine($"║ {noteBar}║");
            }
            else
            {
                Console.WriteLine("║ No pitch detected yet...                                       ║");
                Console.WriteLine("║                                                                ║");
            }

            Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");

            // Calibration results or instructions
            if (isCalibrating)
            {
                Console.WriteLine("║ Instructions:                                                  ║");
                Console.WriteLine("║ * Sing your lowest comfortable note and hold it steady         ║");
                Console.WriteLine("║ * Try to maintain the same pitch throughout the recording      ║");
                Console.WriteLine("║ * Avoid humming - use a vowel sound like 'Ahh'                 ║");
            }
            else
            {
                string noteName = calibrator.GetCalibratedNoteName();
                float pitch = calibrator.GetCalibratedBasePitch();

                if (noteName != "C3" || Math.Abs(pitch - 130.81f) > 0.1f)
                {
                    Console.WriteLine($"║ Calibrated Base Note: {noteName} ({pitch:F2} Hz)                    ║");
                    Console.WriteLine("║                                                                ║");
                    Console.WriteLine("║ Press 'R' to recalibrate or any other key to continue         ║");
                }
                else
                {
                    Console.WriteLine("║ Press 'C' to start calibration                                ║");
                    Console.WriteLine("║ Press any other key to continue with default (C3)             ║");
                    Console.WriteLine("║                                                                ║");
                }
            }

            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
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
            float lastPitch = 0;
            foreach (float pitch in recentPitches)
            {
                if (pitch > 0)
                {
                    // Calculate position in the bar
                    float normalizedPos = (float)Math.Log(pitch / minFreq, 2) /
                                          (float)Math.Log(maxFreq / minFreq, 2);
                    int pos = (int)(normalizedPos * barWidth);

                    // Clamp to valid range
                    pos = Math.Max(0, Math.Min(pos, barWidth - 1));

                    // Mark position
                    bar[pos] = '█';
                    lastPitch = pitch;
                }
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
            float lastPitch = 0;
            foreach (float pitch in recentPitches)
            {
                if (pitch > minFreq && pitch < maxFreq)
                    lastPitch = pitch;
            }

            string noteText = lastPitch > 0 ? NoteUtility.GetNoteName(lastPitch) : "---";
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
            if (text.Length > length)
                return text.Substring(0, length);
            return text.PadRight(length);
        }

        /// <summary>
        /// Center a text within the specified width
        /// </summary>
        private string CenterText(string text, int width)
        {
            int padding = (width - text.Length) / 2;
            return text.PadLeft(padding + text.Length).PadRight(width);
        }
    }
}