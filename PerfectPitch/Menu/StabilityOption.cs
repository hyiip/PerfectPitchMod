using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Game;
using PerfectPitch.Utils;
using PerfectPitchCore.Utils;
using PerfectPitchCore.Constants;
using System;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Menu option for selecting the stability preset for jump detection
    /// </summary>
    public class StabilityOption : IOptions
    {
        // Use constants for preset labels and values
        private static readonly string[] PresetLabels = AppConstants.Stability.PRESET_LABELS;
        private static readonly int[] PresetHistories = AppConstants.Stability.PRESET_HISTORIES;
        private static readonly int[] PresetThresholds = AppConstants.Stability.PRESET_THRESHOLDS;

        private static int selectedPresetIndex = 0; // Default to Fast

        /// <summary>
        /// Create a new stability preset selection option
        /// </summary>
        public StabilityOption()
            : base(AppConstants.Stability.PRESET_LABELS.Length, selectedPresetIndex, EdgeMode.Wrap)
        {
            // Try to determine the current preset from config
            try
            {
                var config = ConfigManager.LoadConfig();
                if (config != null)
                {
                    // Find the matching preset if possible
                    bool presetFound = false;
                    for (int i = 0; i < PresetHistories.Length; i++)
                    {
                        if (config.StabilityHistory == PresetHistories[i] &&
                            config.StabilityThreshold == PresetThresholds[i])
                        {
                            CurrentOption = i;
                            selectedPresetIndex = i;
                            presetFound = true;
                            break;
                        }
                    }

                    if (!presetFound)
                    {
                        // Custom settings detected
                        Log.Info($"Custom stability settings detected: ({config.StabilityHistory},{config.StabilityThreshold})");
                    }
                    else
                    {
                        Log.Info($"Using preset: {PresetLabels[CurrentOption]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error determining preset from config", ex);
            }
        }

        protected override bool CanChange() => true;

        protected override string CurrentOptionName()
        {
            if (CurrentOption < 0 || CurrentOption >= PresetLabels.Length)
                return "Invalid selection";

            return PresetLabels[CurrentOption];
        }

        protected override void OnOptionChange(int option)
        {
            Log.Info($"Changing stability preset to {option}: {PresetLabels[option]}");

            // Store the selected option
            selectedPresetIndex = option;

            // Get the corresponding values
            int history = PresetHistories[option];
            int threshold = PresetThresholds[option];

            // Update the stability settings directly in the jump controller
            ModEntry.SetStabilityParameters(history, threshold);
        }
    }
}