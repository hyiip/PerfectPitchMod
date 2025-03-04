using System;
using PerfectPitchCore.Audio;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Debug visualizer for pitch detection data that displays in the console
    /// </summary>
    public class DebugVisualizer : IPitchProcessor
    {
        public void ProcessPitch(PitchData pitchData)
        {
            // Format a nice display string
            float normalizedDb = Math.Min(1.0f, Math.Max(0.0f, (pitchData.AudioLevelDb + 60) / 60));
            string levelBar = GetLevelBar(normalizedDb, 20);
            string jumpBar = GetLevelBar(pitchData.JumpLevel / 35.0f, 35);

            string display = $"Audio: {pitchData.AudioLevelDb:F1} dB {levelBar} | " +
                             $"Pitch: {pitchData.Pitch:F1} Hz | " +
                             $"Note: {pitchData.NoteName} | " +
                             $"Jump: {pitchData.JumpLevel}/35 {jumpBar}";

            ClearAndWriteLine(display);
        }

        private string GetLevelBar(float level, int maxLength)
        {
            int barLength = (int)(level * maxLength);
            barLength = Math.Min(barLength, maxLength);
            return new string('█', barLength);
        }

        private void ClearAndWriteLine(string text)
        {
            int width = Console.WindowWidth;
            if (width <= 0)
                width = 120;

            Console.Write("\r" + new string(' ', width - 1));
            Console.Write("\r" + text);
        }
    }
}