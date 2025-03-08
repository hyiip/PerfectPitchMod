using System;
using System.Reflection;
using HarmonyLib;
using JumpKing.Controller;
using Microsoft.Xna.Framework.Input;
using PerfectPitch.Utils;

namespace PerfectPitch.Game
{
    /// <summary>
    /// Manager for handling keyboard input through Jump King's controller system
    /// </summary>
    public class InputManager
    {
        private static Keys muteKey = Keys.M;
        private static Keys jumpKey = Keys.J;
        private static Keys debugKey = Keys.K;
        private static bool wasMuteKeyPressed = false;
        private static bool wasJumpKeyPressed = false;
        private static bool wasDebugKeyPressed = false;

        // Events
        public static event Action OnMuteKeyPressed;
        public static event Action OnJumpKeyPressed;
        public static event Action OnDebugKeyPressed;

        /// <summary>
        /// Initialize input detection using Harmony
        /// </summary>
        public static void Initialize(Harmony harmony)
        {
            try
            {
                // Hook the Update method of ControllerManager
                var updateMethod = typeof(ControllerManager).GetMethod("Update");
                var postfix = typeof(InputManager).GetMethod(nameof(UpdatePostfix),
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (updateMethod != null && postfix != null)
                {
                    harmony.Patch(updateMethod, null, new HarmonyMethod(postfix));
                    Log.Info("InputManager initialized - M to mute/unmute voice control, J for test jump, K for debug info");
                }
                else
                {
                    Log.Error("Failed to find Update method or postfix method for patching");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing InputManager", ex);
            }
        }

        /// <summary>
        /// Set the key used for muting voice control
        /// </summary>
        public static void SetMuteKey(Keys key)
        {
            muteKey = key;
            Log.Info($"Mute key set to {key}");
        }

        /// <summary>
        /// Set the key used for test jumps
        /// </summary>
        public static void SetJumpKey(Keys key)
        {
            jumpKey = key;
            Log.Info($"Jump key set to {key}");
        }

        /// <summary>
        /// Set the key used for displaying debug info
        /// </summary>
        public static void SetDebugKey(Keys key)
        {
            debugKey = key;
            Log.Info($"Debug key set to {key}");
        }

        /// <summary>
        /// Postfix for ControllerManager.Update to detect key presses
        /// </summary>
        private static void UpdatePostfix(ControllerManager __instance)
        {
            if (!ModEntry.IsModEnabled()) return;

            try
            {
                // Get current keyboard state
                KeyboardState currentKeyState = Keyboard.GetState();

                // Check for mute key (M)
                bool isMuteKeyDown = currentKeyState.IsKeyDown(muteKey);
                if (isMuteKeyDown && !wasMuteKeyPressed)
                {
                    Log.Info($"Mute key ({muteKey}) pressed");
                    OnMuteKeyPressed?.Invoke();
                }
                wasMuteKeyPressed = isMuteKeyDown;

                // Check for jump key (J)
                bool isJumpKeyDown = currentKeyState.IsKeyDown(jumpKey);
                if (isJumpKeyDown && !wasJumpKeyPressed)
                {
                    Log.Info($"Jump key ({jumpKey}) pressed");
                    OnJumpKeyPressed?.Invoke();
                }
                wasJumpKeyPressed = isJumpKeyDown;

                // Check for debug key (K)
                bool isDebugKeyDown = currentKeyState.IsKeyDown(debugKey);
                if (isDebugKeyDown && !wasDebugKeyPressed)
                {
                    Log.Info($"Debug key ({debugKey}) pressed");
                    OnDebugKeyPressed?.Invoke();
                }
                wasDebugKeyPressed = isDebugKeyDown;
            }
            catch (Exception ex)
            {
                Log.Error("Error in InputManager.UpdatePostfix", ex);
            }
        }
    }
}