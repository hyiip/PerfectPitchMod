using PerfectPitchCore.Audio;
using PerfectPitch.Utils;

namespace PerfectPitch.Game
{
    /// <summary>
    /// Simple processor to test if pitch data is being received
    /// </summary>
    public class TestPitchProcessor : IPitchProcessor
    {
        private int _counter = 0;

        public void ProcessPitch(PitchData pitchData)
        {
            _counter++;

            if (_counter % 10 == 1) // Log every 10th sample to avoid spam
            {
                Log.Info($"TestProcessor: Received {_counter} samples. Current: {pitchData.AudioLevelDb:F1} dB, Pitch: {pitchData.Pitch:F1} Hz");
            }
        }
    }
}