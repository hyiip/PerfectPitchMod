using System;
using System.Collections.Generic;
using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Utils;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Constants;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Menu option for selecting the microphone input device
    /// </summary>
    public class MicrophoneOption : IOptions
    {
        private List<string> microphoneOptions;
        private static int selectedMicrophoneIndex = 0;

        /// <summary>
        /// Create a new microphone selection option
        /// </summary>
        public MicrophoneOption()
            : base(1, 0, EdgeMode.Wrap) // Start with just one option
        {
            try
            {
                // Get available microphones
                microphoneOptions = PitchManager.GetAvailableMicrophones();

                // Update the options count
                OptionCount = microphoneOptions.Count;

                // Update the current option
                CurrentOption = Math.Min(selectedMicrophoneIndex, microphoneOptions.Count - 1);
                selectedMicrophoneIndex = CurrentOption;

                Log.Info($"Created microphone option with {microphoneOptions.Count} devices. Selected: {CurrentOption}");
            }
            catch (Exception ex)
            {
                Log.Error("Error getting microphones", ex);

                // Create a fallback option
                microphoneOptions = new List<string> { "Default Microphone" };

                // Update the current option
                CurrentOption = 0;
                selectedMicrophoneIndex = 0;

                Log.Info("Created microphone option with fallback device");
            }
        }

        protected override bool CanChange() => true;

        protected override string CurrentOptionName()
        {
            if (microphoneOptions == null || microphoneOptions.Count == 0)
                return "No microphones found";

            if (CurrentOption < 0 || CurrentOption >= microphoneOptions.Count)
                return "Invalid selection";

            return microphoneOptions[CurrentOption];
        }

        protected override void OnOptionChange(int option)
        {
            Log.Info($"Changing microphone to {option}: {CurrentOptionName()}");

            // Store the selected option
            selectedMicrophoneIndex = option;

            // Update the microphone in the mod
            ModEntry.SetMicrophoneDevice(option);
        }
    }
}