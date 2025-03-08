using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Utils;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Toggle menu item for showing/hiding the voice visualizer gauge
    /// </summary>
    public class ToggleGauge : ITextToggle
    {
        // Default to enabled
        private static bool _showGauge = true;

        // Constructor
        public ToggleGauge() : base(_showGauge)
        {
        }

        protected override string GetName() => "Show Jump Gauge";

        protected override void OnToggle()
        {
            _showGauge = base.toggle;

            // Update visualizer gauge state
            var visualizer = ModEntry.GetVoiceVisualizer();
            if (visualizer != null)
            {
                visualizer.SetShowGauge(_showGauge);
                Log.Info($"Voice gauge {(_showGauge ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Allow external override of gauge state
        /// </summary>
        public void OverrideState(bool showGauge)
        {
            _showGauge = showGauge;
            base.OverrideToggle(showGauge);

            // Update visualizer gauge state
            var visualizer = ModEntry.GetVoiceVisualizer();
            if (visualizer != null)
            {
                visualizer.SetShowGauge(showGauge);
                Log.Info($"Voice gauge state updated: {(showGauge ? "Enabled" : "Disabled")}");
            }
        }
    }
}