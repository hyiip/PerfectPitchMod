namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Interface for pitch processors that can be plugged into the PitchManager
    /// </summary>
    public interface IPitchProcessor
    {
        /// <summary>
        /// Process the pitch data
        /// </summary>
        void ProcessPitch(PitchData pitchData);

        /// <summary>
        /// Gets whether this processor should receive all audio events,
        /// even when no pitch is detected or audio is below threshold
        /// </summary>
        bool ReceiveAllAudioEvents { get; }
    }
}