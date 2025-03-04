namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Interface for pitch processors that can be plugged into the PitchManager
    /// </summary>
    public interface IPitchProcessor
    {
        void ProcessPitch(PitchData pitchData);
    }
}