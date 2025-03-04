using System;

namespace PerfectPitchCore.Utils
{
    public static class JumpLevelCalculator
    {
        // Fixed reference pitch - A2 (110 Hz)
        private const float REFERENCE_PITCH = 110.0f;

        // Maximum jump level
        private const int MAX_JUMP_LEVEL = 35;

        /// <summary>
        /// Controls whether detailed logging is enabled
        /// </summary>
        public static bool EnableDetailedLogging { get; set; } = false;

        public static int CalculateJumpLevel(float currentPitch, float calibratedBasePitch)
        {
            // Handle invalid input
            if (currentPitch <= 0)
                return 0;

            // Only log if detailed logging is enabled
            if (EnableDetailedLogging)
            {
                Console.WriteLine($"JumpLevel: {currentPitch:F1} Hz with base {calibratedBasePitch:F1} Hz");
            }

            // Calculate semitones above the fixed reference pitch (A2)
            float semitonesAboveReference = 12 * (float)Math.Log(currentPitch / REFERENCE_PITCH, 2);

            // Calculate the calibration offset (how many semitones is the user's base note from A2)
            float calibrationOffset = 12 * (float)Math.Log(calibratedBasePitch / REFERENCE_PITCH, 2);

            // Apply offset and round to nearest integer
            float adjustedSemitones = semitonesAboveReference - calibrationOffset;
            int jumpLevel = (int)Math.Round(adjustedSemitones);

            // Only log calculation details if enabled
            if (EnableDetailedLogging)
            {
                Console.WriteLine($"  Semitones from A2: {semitonesAboveReference:F2}, Offset: {calibrationOffset:F2}, Result: {jumpLevel}");
            }

            // Ensure non-negative and cap at maximum
            if (jumpLevel <= 0)
                return 0;
            else
                return Math.Min(jumpLevel, MAX_JUMP_LEVEL);
        }

        public static float GetPitchForJumpLevel(int jumpLevel, float calibratedBasePitch)
        {
            // Handle invalid input
            if (jumpLevel <= 0)
                return calibratedBasePitch;

            // Calculate calibration offset
            float calibrationOffset = 12 * (float)Math.Log(calibratedBasePitch / REFERENCE_PITCH, 2);

            // Calculate semitones above reference for this jump level
            float semitonesAboveReference = jumpLevel + calibrationOffset;

            // Convert back to frequency
            return REFERENCE_PITCH * (float)Math.Pow(2, semitonesAboveReference / 12.0);
        }

        public static void DebugPrintJumpLevels(float calibratedBasePitch)
        {
            string baseNoteName = NoteUtility.GetNoteName(calibratedBasePitch);
            Console.WriteLine($"Jump levels with base note {baseNoteName} ({calibratedBasePitch:F2} Hz):");

            // Calculate semitone jump levels
            for (int i = 0; i <= 12; i++)
            {
                float pitchForSemitone = calibratedBasePitch * (float)Math.Pow(2, i / 12.0);
                int jumpLevel = CalculateJumpLevel(pitchForSemitone, calibratedBasePitch);
                string noteName = NoteUtility.GetNoteName(pitchForSemitone);

                Console.WriteLine($"  {noteName} ({pitchForSemitone:F2} Hz): Jump Level {jumpLevel}");
            }
        }
    }
}