using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Utils;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Toggle for enabling/disabling the mod
    /// </summary>
    public class ToggleModEnabled : ITextToggle
    {
        // Constructor - initialize with current state of the mod
        public ToggleModEnabled() : base(ModEntry.IsEnabled)
        {
            Log.Info($"Created mod toggle with initial state: {(ModEntry.IsEnabled ? "Enabled" : "Disabled")}");
        }

        protected override string GetName() => "Voice Jump Mod";

        protected override void OnToggle()
        {
            // Toggle mod state
            ModEntry.SetModEnabled(!ModEntry.IsEnabled);
            Log.Info($"Mod toggled to: {(ModEntry.IsEnabled ? "Enabled" : "Disabled")}");
        }
    }
}