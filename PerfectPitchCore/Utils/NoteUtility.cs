using System;

namespace PerfectPitchCore.Utils
{
    public static class NoteUtility
    {
        private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private const float REFERENCE_PITCH = 440.0f; // A4
        private const int REFERENCE_OCTAVE = 4;
        private const int REFERENCE_NOTE_INDEX = 9; // A in NoteNames

        /// <summary>
        /// Get the note name for a given frequency
        /// </summary>
        public static string GetNoteName(float pitch)
        {
            if (pitch <= 0) return "---";

            // Calculate semitones from A4 (440 Hz)
            float semitones = 12 * (float)Math.Log(pitch / REFERENCE_PITCH, 2);
            int roundedSemitones = (int)Math.Round(semitones);

            // Calculate octave and note index
            int octave = REFERENCE_OCTAVE + (roundedSemitones / 12);
            int noteIndex = REFERENCE_NOTE_INDEX + (roundedSemitones % 12);

            // Adjust for wrapping around the octave
            while (noteIndex >= 12)
            {
                noteIndex -= 12;
                octave++;
            }

            while (noteIndex < 0)
            {
                noteIndex += 12;
                octave--;
            }

            return NoteNames[noteIndex] + octave;
        }

        /// <summary>
        /// Legacy method signature for backward compatibility. Now calls the corrected method.
        /// </summary>
        public static string GetNoteName(float pitch, float basePitch)
        {
            // Ignore basePitch, use the fixed reference method
            return GetNoteName(pitch);
        }

        /// <summary>
        /// Calculate semitones above a base pitch
        /// </summary>
        public static float GetSemitones(float pitch, float basePitch)
        {
            if (pitch <= 0) return 0;
            return 12 * (float)Math.Log(pitch / basePitch, 2);
        }

        /// <summary>
        /// Get the frequency of a note from its name
        /// </summary>
        public static float GetFrequencyFromNoteName(string noteName)
        {
            if (string.IsNullOrWhiteSpace(noteName))
                return -1;

            string note = "";
            string octaveStr = "";
            bool parsingNote = true;

            for (int i = 0; i < noteName.Length; i++)
            {
                char c = char.ToUpper(noteName[i]);
                if (parsingNote && (c == '#' || (c >= 'A' && c <= 'G')))
                {
                    note += c;
                }
                else if (c >= '0' && c <= '9')
                {
                    parsingNote = false;
                    octaveStr += c;
                }
                else
                {
                    return -1;
                }
            }

            if (string.IsNullOrEmpty(note) || string.IsNullOrEmpty(octaveStr))
                return -1;

            if (!int.TryParse(octaveStr, out int octave))
                return -1;

            int noteIndex = -1;
            for (int i = 0; i < NoteNames.Length; i++)
            {
                if (NoteNames[i].Equals(note, StringComparison.OrdinalIgnoreCase))
                {
                    noteIndex = i;
                    break;
                }
            }

            if (noteIndex == -1)
                return -1;

            int semitonesFromA4 = (octave - REFERENCE_OCTAVE) * 12 + (noteIndex - REFERENCE_NOTE_INDEX);
            float frequency = REFERENCE_PITCH * (float)Math.Pow(2, semitonesFromA4 / 12.0);
            return frequency;
        }

        /// <summary>
        /// Print a table of standard note frequencies for debugging
        /// </summary>
        public static void PrintNoteFrequencies()
        {
            Console.WriteLine("Standard Note Frequencies:");
            Console.WriteLine("---------------------------");

            for (int octave = 2; octave <= 5; octave++)
            {
                for (int noteIdx = 0; noteIdx < NoteNames.Length; noteIdx++)
                {
                    string noteName = NoteNames[noteIdx] + octave;
                    float freq = GetFrequencyFromNoteName(noteName);
                    Console.WriteLine($"{noteName}: {freq:F2} Hz");
                }
            }
        }
    }
}