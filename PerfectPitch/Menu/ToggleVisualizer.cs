using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Utils;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Toggle menu item for showing/hiding the voice visualizer
    /// </summary>
    public class ToggleVisualizer : ITextToggle
    {
        // Default to enabled
        private static bool _isEnabled = true;

        // Constructor
        public ToggleVisualizer() : base(_isEnabled)
        {
            Log.Info($"Created visualizer toggle with initial state: {(_isEnabled ? "Enabled" : "Disabled")}");
        }

        protected override string GetName() => "Show Voice Info";

        protected override void OnToggle()
        {
            _isEnabled = base.toggle;

            // Update visualizer state
            var visualizer = ModEntry.GetVoiceVisualizer();
            if (visualizer != null)
            {
                visualizer.SetEnabled(_isEnabled);
                Log.Info($"Voice visualizer {(_isEnabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Allow external override of visualizer state
        /// </summary>
        public void OverrideState(bool enabled)
        {
            _isEnabled = enabled;
            base.OverrideToggle(enabled);

            // Update visualizer state
            var visualizer = ModEntry.GetVoiceVisualizer();
            if (visualizer != null)
            {
                visualizer.SetEnabled(enabled);
                Log.Info($"Voice visualizer state updated: {(enabled ? "Enabled" : "Disabled")}");
            }
        }
    }
}