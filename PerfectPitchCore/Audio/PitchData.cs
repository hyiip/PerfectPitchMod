using System;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Data structure containing pitch information
    /// </summary>
    public class PitchData
    {
        public float Pitch { get; set; }

        // Keep this as a settable property for compatibility with existing code
        public int JumpLevel { get; set; }

        public float AudioLevel { get; set; }
        public float AudioLevelDb { get; set; }
        public float BasePitch { get; set; }
        public DateTime Timestamp { get; set; }

        // Add any additional pitch-related data you might need
        public string NoteName => NoteUtility.GetNoteName(Pitch, BasePitch);
        public float SemitonesFromBase => NoteUtility.GetSemitones(Pitch, BasePitch);

        // Calculate the jump level using the calculator
        public int CalculateJumpLevel()
        {
            return JumpLevelCalculator.CalculateJumpLevel(Pitch, BasePitch);
        }
    }
}