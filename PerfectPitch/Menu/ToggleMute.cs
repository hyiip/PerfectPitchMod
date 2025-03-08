using JumpKing.PauseMenu.BT.Actions;
using PerfectPitch.Utils;
using PerfectPitchCore.Constants;

namespace PerfectPitch.Menu
{
    /// <summary>
    /// Toggle menu item for muting/unmuting the voice control
    /// </summary>
    public class ToggleMute : ITextToggle
    {
        // Constructor - initialize with current mute state
        // Note: ITextToggle expects true for "on", so we invert IsMuted
        public ToggleMute() : base(!ModEntry.IsMuted)
        {
            Log.Info($"Created mute toggle with initial state: {(ModEntry.IsMuted ? "Muted" : "Active")}");
        }

        protected override string GetName() => "Voice Control Active";

        protected override void OnToggle()
        {
            // Toggle mute state - since base.toggle has already been toggled in IToggle,
            // we can use it directly to determine the new mute state
            bool muted = !base.toggle;
            ModEntry.SetMuted(muted);
            Log.Info($"Voice control {(muted ? "muted" : "active")}");
        }

        /// <summary>
        /// Allow external override of the mute state
        /// </summary>
        public void OverrideMute(bool muted)
        {
            // Use the OverrideToggle method that's available in IToggle
            // Remember that toggle is inverted - true means NOT muted
            base.OverrideToggle(!muted);
            Log.Info($"Voice control toggle updated: {(muted ? "Muted" : "Active")}");
        }
    }
}