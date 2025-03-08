using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Utils;
using PerfectPitchCore.Constants;
using PerfectPitchCore.Utils;
using System;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Menu option for selecting the audio threshold level
    /// </summary>
    public class ThresholdOption : IOptions
    {
        // Use constants instead of hardcoded values
        private static readonly float[] ThresholdValues = AppConstants.Audio.THRESHOLD_VALUES;
        private static readonly string[] ThresholdLabels = AppConstants.Audio.THRESHOLD_LABELS;

        // Default to sensitive (-40dB)
        private static int selectedThresholdIndex = 1;

        /// <summary>
        /// Create a new threshold selection option
        /// </summary>
        public ThresholdOption()
            : base(AppConstants.Audio.THRESHOLD_VALUES.Length, selectedThresholdIndex, EdgeMode.Wrap)
        {
            // Try to determine the current threshold from config
            try
            {
                var config = ConfigManager.LoadConfig();
                if (config != null)
                {
                    // Find the matching threshold if possible
                    bool thresholdFound = false;
                    for (int i = 0; i < ThresholdValues.Length; i++)
                    {
                        if (Math.Abs(config.VolumeThresholdDb - ThresholdValues[i]) < 0.1f)
                        {
                            CurrentOption = i;
                            selectedThresholdIndex = i;
                            thresholdFound = true;
                            break;
                        }
                    }

                    if (!thresholdFound)
                    {
                        // Custom threshold detected
                        Log.Info($"Custom threshold detected: {config.VolumeThresholdDb}dB");
                    }
                    else
                    {
                        Log.Info($"Using threshold preset: {ThresholdLabels[CurrentOption]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error determining threshold from config", ex);
            }
        }

        protected override bool CanChange() => true;

        protected override string CurrentOptionName()
        {
            if (CurrentOption < 0 || CurrentOption >= ThresholdLabels.Length)
                return "Invalid selection";

            return ThresholdLabels[CurrentOption];
        }

        protected override void OnOptionChange(int option)
        {
            Log.Info($"Changing threshold to {option}: {ThresholdLabels[option]} ({ThresholdValues[option]}dB)");

            // Store the selected option
            selectedThresholdIndex = option;

            // Update the threshold in the mod
            ModEntry.SetAudioThreshold(ThresholdValues[option]);
        }
    }
}