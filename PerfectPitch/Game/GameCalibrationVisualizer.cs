using System;
using System.Collections.Generic;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PerfectPitch.Utils;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Utils;

namespace PerfectPitch.Game
{
    /// <summary>
    /// Game-specific implementation of the calibration visualizer
    /// </summary>
    public class GameCalibrationVisualizer : CalibrationVisualizerBase
    {
        // UI resources
        private static SpriteFont _font;
        private static Texture2D _bgTexture;
        private static Texture2D _gaugeTexture;

        // Styling constants
        private const int OVERLAY_WIDTH = 300;
        private const int OVERLAY_HEIGHT = 200;
        private const int GAUGE_WIDTH = 30;
        private const int GAUGE_HEIGHT = 150;

        /// <summary>
        /// Create a new game visualization overlay
        /// </summary>
        public GameCalibrationVisualizer(CalibrationProcessor calibrator, Harmony harmony)
            : base(calibrator)
        {
            try
            {
                // Patch the game's draw method to show our overlay
                harmony.Patch(
                    typeof(GameLoop).GetMethod(nameof(GameLoop.Draw)),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(GameCalibrationVisualizer), nameof(Draw)))
                );

                // Create UI resources
                InitializeResources();

                Log.Info("Game calibration visualizer initialized");
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing game calibration visualizer", ex);
            }
        }

        /// <summary>
        /// Initialize UI resources
        /// </summary>
        private void InitializeResources()
        {
            try
            {
                // Use font from the game
                _font = Game1.instance.contentManager.font.MenuFont;

                // Create background texture
                _bgTexture = new Texture2D(Game1.instance.GraphicsDevice, 1, 1);
                _bgTexture.SetData(new[] { new Color(0, 0, 0, 200) });

                // Create gauge texture
                _gaugeTexture = new Texture2D(Game1.instance.GraphicsDevice, 1, 1);
                _gaugeTexture.SetData(new[] { Color.White });

                Log.Info("Game calibration resources created");
            }
            catch (Exception ex)
            {
                Log.Error("Error creating game calibration resources", ex);
            }
        }

        /// <summary>
        /// Implementation of the abstract method for UI updates
        /// </summary>
        protected override void OnUpdateUI()
        {
            // No need to do anything here, the Draw method handles rendering
            // when the game calls it
        }

        /// <summary>
        /// Draw the calibration overlay - static method called by Harmony
        /// </summary>
        static void Draw(GameLoop __instance)
        {
            try
            {
                // Get a reference to the current visualizer instance from ModEntry
                var visualizer = ModEntry.GetCalibrationVisualizer();
                if (visualizer == null || !(visualizer is GameCalibrationVisualizer gameVisualizer))
                    return;

                // Check if it should be visible
                if (!gameVisualizer._isVisible)
                    return;

                // Only draw if we're not paused
                if (Traverse.Create(__instance).Field("m_pause_manager").Property("IsPaused").GetValue<bool>())
                    return;

                gameVisualizer.DrawOverlay();
            }
            catch (Exception ex)
            {
                // Silent fail to not crash the game
                Log.Error("Error in game calibration visualizer Draw", ex);
            }
        }

        /// <summary>
        /// Draw the overlay content
        /// </summary>
        private void DrawOverlay()
        {
            try
            {
                // Check if game is in a state where we can draw
                if (Game1.spriteBatch == null || Game1.instance == null)
                    return;

                // Calculate position to center the overlay
                int screenWidth = Game1.instance.GraphicsDevice.Viewport.Width;
                int screenHeight = Game1.instance.GraphicsDevice.Viewport.Height;
                int x = (screenWidth - OVERLAY_WIDTH) / 2;
                int y = (screenHeight - OVERLAY_HEIGHT) / 2;

                // Draw background
                Game1.spriteBatch.Draw(
                    _bgTexture,
                    new Rectangle(x, y, OVERLAY_WIDTH, OVERLAY_HEIGHT),
                    Color.White
                );

                // Draw title
                TextHelper.DrawString(
                    _font,
                    "Voice Calibration",
                    new Vector2(x + 10, y + 10),
                    Color.White,
                    Vector2.Zero,
                    true
                );

                // Draw status
                TextHelper.DrawString(
                    _font,
                    _status,
                    new Vector2(x + 10, y + 40),
                    Color.Yellow,
                    Vector2.Zero,
                    true
                );

                // Draw countdown if active
                if (_countdown > 0)
                {
                    string countdownText = $"Get ready: {_countdown}...";
                    Vector2 textSize = _font.MeasureString(countdownText);
                    TextHelper.DrawString(
                        _font,
                        countdownText,
                        new Vector2(x + (OVERLAY_WIDTH - textSize.X) / 2, y + 70),
                        Color.Red,
                        Vector2.Zero,
                        true
                    );
                }

                // Draw pitch information
                if (_currentPitch > 0)
                {
                    TextHelper.DrawString(
                        _font,
                        $"Current Pitch: {_currentPitch:F1} Hz ({_currentNote})",
                        new Vector2(x + 10, y + 100),
                        Color.White,
                        Vector2.Zero,
                        true
                    );
                }
                else
                {
                    TextHelper.DrawString(
                        _font,
                        "Sing your lowest comfortable note...",
                        new Vector2(x + 10, y + 100),
                        Color.White,
                        Vector2.Zero,
                        true
                    );
                }

                // Draw instructions or error message
                if (_hasError)
                {
                    TextHelper.DrawString(
                        _font,
                        $"Error: {_errorMessage}",
                        new Vector2(x + 10, y + OVERLAY_HEIGHT - 30),
                        Color.Red,
                        Vector2.Zero,
                        true
                    );
                }
                else
                {
                    TextHelper.DrawString(
                        _font,
                        "Hold a steady note until complete",
                        new Vector2(x + 10, y + OVERLAY_HEIGHT - 30),
                        Color.LightGreen,
                        Vector2.Zero,
                        true
                    );
                }

                // Draw pitch gauge
                DrawPitchGauge(x + OVERLAY_WIDTH - GAUGE_WIDTH - 20, y + 40);
            }
            catch (Exception ex)
            {
                Log.Error("Error drawing calibration overlay", ex);
            }
        }

        /// <summary>
        /// Draw the pitch gauge showing current pitch level
        /// </summary>
        private void DrawPitchGauge(int x, int y)
        {
            try
            {
                // Draw gauge background
                Game1.spriteBatch.Draw(
                    _bgTexture,
                    new Rectangle(x - 5, y - 5, GAUGE_WIDTH + 10, GAUGE_HEIGHT + 10),
                    new Color(60, 60, 60, 255)
                );

                // Draw empty gauge
                Game1.spriteBatch.Draw(
                    _gaugeTexture,
                    new Rectangle(x, y, GAUGE_WIDTH, GAUGE_HEIGHT),
                    new Color(30, 30, 30, 255)
                );

                // Draw pitch level if we have a pitch
                if (_currentPitch > 0)
                {
                    // Calculate how many semitones above base C3 (130.81Hz)
                    float targetBasePitch = 130.81f;
                    float semitones = 12 * (float)Math.Log(_currentPitch / targetBasePitch, 2);

                    // Map to a gauge height (0-24 semitones = 0-100% of gauge)
                    float fillPercentage = Math.Min(semitones / 24f, 1.0f);
                    if (fillPercentage < 0) fillPercentage = 0;

                    int fillHeight = (int)(GAUGE_HEIGHT * fillPercentage);

                    // Draw the filled part
                    Game1.spriteBatch.Draw(
                        _gaugeTexture,
                        new Rectangle(x, y + GAUGE_HEIGHT - fillHeight, GAUGE_WIDTH, fillHeight),
                        new Color(0, 180, 255, 255)
                    );

                    // Draw the current note marker
                    string noteLabel = _currentNote;
                    Vector2 noteSize = _font.MeasureString(noteLabel);
                    TextHelper.DrawString(
                        _font,
                        noteLabel,
                        new Vector2(x - noteSize.X - 5, y + GAUGE_HEIGHT - fillHeight - (noteSize.Y / 2)),
                        Color.White,
                        Vector2.Zero,
                        true
                    );
                }

                // Draw some note markers along the gauge (C3, C4, etc.)
                DrawNoteMarker(x, y, "C3", 130.81f);
                DrawNoteMarker(x, y, "G3", 196.00f);
                DrawNoteMarker(x, y, "C4", 261.63f);
                DrawNoteMarker(x, y, "G4", 392.00f);
                DrawNoteMarker(x, y, "C5", 523.25f);
            }
            catch (Exception ex)
            {
                Log.Error("Error drawing pitch gauge", ex);
            }
        }

        /// <summary>
        /// Draw a note marker on the gauge
        /// </summary>
        private void DrawNoteMarker(int gaugeX, int gaugeY, string noteName, float frequency)
        {
            try
            {
                // Calculate how many semitones above base C3 (130.81Hz)
                float targetBasePitch = 130.81f;
                float semitones = 12 * (float)Math.Log(frequency / targetBasePitch, 2);

                // Map to a gauge position (0-24 semitones = 0-100% of gauge)
                float position = Math.Min(semitones / 24f, 1.0f);
                if (position < 0) position = 0;

                int yPos = gaugeY + GAUGE_HEIGHT - (int)(GAUGE_HEIGHT * position);

                // Draw marker line
                Game1.spriteBatch.Draw(
                    _gaugeTexture,
                    new Rectangle(gaugeX - 5, yPos, GAUGE_WIDTH + 10, 1),
                    Color.Gray
                );

                // Draw note name
                Vector2 textSize = _font.MeasureString(noteName);
                TextHelper.DrawString(
                    _font,
                    noteName,
                    new Vector2(gaugeX - textSize.X - 5, yPos - (textSize.Y / 2)),
                    Color.Gray,
                    Vector2.Zero,
                    true
                );
            }
            catch (Exception ex)
            {
                Log.Error("Error drawing note marker", ex);
            }
        }
    }
}