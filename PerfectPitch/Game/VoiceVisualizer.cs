using HarmonyLib;
using JumpKing;
using JumpKing.GameManager;
using JumpKing.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using PerfectPitchCore.Audio;
using PerfectPitchCore.Utils;
using PerfectPitchCore.Constants;
using PerfectPitch.Utils;

namespace PerfectPitch.Game
{
    /// <summary>
    /// Visualizes microphone level and detected pitch in-game
    /// </summary>
    public class VoiceVisualizer : IPitchProcessor
    {
        // UI-related fields
        private static Texture2D _gaugeTexture;
        private static Texture2D _bgTexture;
        private static SpriteFont _font;
        private const float TEXT_PADDING = 10f;

        // Visualization data
        private float _currentPitch;
        private float _audioLevel;
        private float _audioLevelDb = -60.0f; // Initialize to reasonable default
        private string _noteName = "---";
        private int _jumpLevel;

        // State
        private bool _isEnabled = true;
        private bool _showGauge = true;
        private bool _dataReceived = false;

        // Debug info
        private DateTime _lastDataTime = DateTime.MinValue;
        private int _updateCount = 0;

        // Force data check trigger
        private static DateTime _startTime = DateTime.Now;
        private static bool _isVisualizerInitialized = false;
        public bool ReceiveAllAudioEvents => true;

        /// <summary>
        /// Constructor - patches the game's draw method to render our visualizer
        /// </summary>
        public VoiceVisualizer(Harmony harmony)
        {
            try
            {
                // Patch the game's draw method to inject our visualization
                harmony.Patch(
                    typeof(GameLoop).GetMethod(nameof(GameLoop.Draw)),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(VoiceVisualizer), nameof(Draw)))
                );

                // Create UI resources
                InitializeResources();

                // Mark the visualizer as initialized
                _isVisualizerInitialized = true;
                _startTime = DateTime.Now;

                Log.Info("Voice visualizer initialized and ready!");
            }
            catch (Exception ex)
            {
                Log.Error("Error initializing voice visualizer", ex);
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

                // Create gauge texture
                _gaugeTexture = new Texture2D(Game1.instance.GraphicsDevice, 4, 100);
                Color[] data = new Color[4 * 100];

                // Fill with gradient from green to red
                for (int i = 0; i < 100; i++)
                {
                    // Create gradient: green at bottom, yellow in middle, red at top
                    Color color;
                    if (i < 50)
                    {
                        // Green to yellow gradient
                        float t = i / 50f;
                        color = new Color(t, 1f, 0f);
                    }
                    else
                    {
                        // Yellow to red gradient
                        float t = (i - 50) / 50f;
                        color = new Color(1f, 1f - t, 0f);
                    }

                    data[i * 4] = color;
                    data[i * 4 + 1] = color;
                    data[i * 4 + 2] = color;
                    data[i * 4 + 3] = color;
                }

                _gaugeTexture.SetData(data);

                // Create background texture
                _bgTexture = new Texture2D(Game1.instance.GraphicsDevice, 1, 1);
                _bgTexture.SetData(new[] { new Color(0, 0, 0, 128) });

                Log.Info("Voice visualizer resources created successfully");
            }
            catch (Exception ex)
            {
                Log.Error("Error creating visualizer resources", ex);
            }
        }

        /// <summary>
        /// Process incoming pitch data
        /// </summary>
        /// <summary>
        /// Process incoming pitch data - override to handle both volume-only and pitch data
        /// </summary>
        public void ProcessPitch(PitchData pitchData)
        {
            try
            {
                // Critical for debugging: increment update counter
                _updateCount++;

                // Log every 10th update at INFO level
                if (_updateCount % 10 == 0)
                {
                    Log.Info($"VoiceVisualizer.ProcessPitch called {_updateCount} times. Last data: {pitchData.AudioLevelDb:F1} dB, Pitch: {pitchData.Pitch:F1} Hz");
                }

                if (!_isEnabled)
                    return;

                // Debug info - track data receipt
                _lastDataTime = DateTime.Now;

                // ALWAYS update audio level data
                _audioLevel = pitchData.AudioLevel;
                _audioLevelDb = pitchData.AudioLevelDb;

                // Only update pitch-related data if pitch is detected
                if (pitchData.Pitch > 0)
                {
                    _currentPitch = pitchData.Pitch;
                    _noteName = pitchData.NoteName;
                    _jumpLevel = pitchData.JumpLevel;
                }

                // CRITICAL: Set _dataReceived to true even for non-pitch data
                // We want to see audio levels even when no pitch is detected
                _dataReceived = true;
            }
            catch (Exception ex)
            {
                Log.Error("Error in VoiceVisualizer.ProcessPitch", ex);
            }
        }

        /// <summary>
        /// Enable or disable the visualizer
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            Log.Info($"Voice visualizer enabled: {enabled}");
        }

        /// <summary>
        /// Show or hide the gauge
        /// </summary>
        public void SetShowGauge(bool showGauge)
        {
            _showGauge = showGauge;
            Log.Info($"Voice gauge display: {showGauge}");
        }

        /// <summary>
        /// Inject some dummy data for visualization testing
        /// </summary>
        private void InjectDummyDataIfNeeded()
        {
            // If we've gone more than 3 seconds without data and we have no updates,
            // inject a dummy audio level just to show we're alive
            if (_updateCount == 0 && (DateTime.Now - _startTime).TotalSeconds > 3)
            {
                if (!_dataReceived)
                {
                    // Create a dummy audio level that mimics silence but allows display
                    _audioLevelDb = -55.0f;
                    _currentPitch = 0;
                    _dataReceived = true;

                    // Log this at ERROR level to make sure it's seen
                    Log.Info("Visualizer: Injected dummy audio level data to show UI is alive");
                }
            }
        }

        /// <summary>
        /// Draw method injected into GameLoop.Draw
        /// </summary>
        static void Draw(GameLoop __instance)
        {
            try
            {
                // Only draw if we're not paused
                if (Traverse.Create(__instance).Field("m_pause_manager").Property("IsPaused").GetValue<bool>())
                    return;

                // Get the instance
                VoiceVisualizer instance = ModEntry.GetVoiceVisualizer();
                if (instance == null || !instance._isEnabled || !_isVisualizerInitialized)
                    return;

                // Inject some dummy data if needed (helps with testing)
                instance.InjectDummyDataIfNeeded();

                // Draw the visualization
                instance.DrawVisualization();
            }
            catch (Exception ex)
            {
                // Silent fail to not crash the game
                Log.Error("Error in visualizer Draw", ex);
            }
        }

        /// <summary>
        /// Draw the visualization with a cleaner UI
        /// </summary>
        private void DrawVisualization()
        {
            try
            {
                // Extra check if game is in a state where we can draw
                if (Game1.spriteBatch == null || Game1.instance == null)
                {
                    return;
                }

                int yPosition = 10; // Starting Y position

                // First, check if voice control is muted and show mute indicator
                if (ModEntry.IsMuted)
                {
                    string muteText = "MUTED (Press M to unmute)";
                    Vector2 muteTextSize = _font.MeasureString(muteText);

                    // Draw background
                    Game1.spriteBatch.Draw(
                        _bgTexture,
                        new Rectangle(10, yPosition, (int)muteTextSize.X + 10, (int)muteTextSize.Y + 6),
                        new Color(150, 0, 0, 200)  // Dark red background
                    );

                    // Draw text
                    TextHelper.DrawString(
                        _font,
                        muteText,
                        new Vector2(15, yPosition + 3),
                        Color.Red,
                        Vector2.Zero,
                        true
                    );

                    // Exit early since we're muted
                    return;
                }

                // Check if we've received any data yet
                if (!_dataReceived)
                {
                    // Only show waiting message on initial display
                    string waitingText = "Waiting for audio...";
                    Vector2 waitingTextSize = _font.MeasureString(waitingText);

                    // Draw background
                    Game1.spriteBatch.Draw(
                        _bgTexture,
                        new Rectangle(10, yPosition, (int)waitingTextSize.X + 10, (int)waitingTextSize.Y + 6),
                        Color.White
                    );

                    // Draw text
                    TextHelper.DrawString(
                        _font,
                        waitingText,
                        new Vector2(15, yPosition + 3),
                        Color.Yellow,
                        Vector2.Zero,
                        true
                    );

                    return;
                }

                // Check if our data is stale (older than 2 seconds)
                bool staleData = (DateTime.Now - _lastDataTime).TotalSeconds > 2;

                // If data is stale, don't show any UI except the gauge if enabled
                if (staleData)
                {
                    // Draw gauge if enabled
                    if (_showGauge && _gaugeTexture != null)
                    {
                        DrawGauge();
                    }

                    // Exit early - no text display for stale data
                    return;
                }

                // ACTIVE DATA DISPLAY:

                // Draw mic input text with simplified display
                string micText = $"Mic: {_audioLevelDb:F1} dB";
                Vector2 micTextSize = _font.MeasureString(micText);

                // Draw background
                Game1.spriteBatch.Draw(
                    _bgTexture,
                    new Rectangle(10, yPosition, (int)micTextSize.X + 10, (int)micTextSize.Y + 6),
                    Color.White
                );

                // Draw text
                TextHelper.DrawString(
                    _font,
                    micText,
                    new Vector2(15, yPosition + 3),
                    GetLevelColor(_audioLevelDb),
                    Vector2.Zero,
                    true
                );

                // Update Y position for next element
                yPosition += (int)micTextSize.Y + 5;

                // Only draw pitch info if we have a valid pitch 
                if (_currentPitch > 0)
                {
                    string pitchText = $"Pitch: {_currentPitch:F1} Hz ({_noteName})";
                    Vector2 pitchTextSize = _font.MeasureString(pitchText);

                    // Draw background
                    Game1.spriteBatch.Draw(
                        _bgTexture,
                        new Rectangle(10, yPosition, (int)pitchTextSize.X + 10, (int)pitchTextSize.Y + 6),
                        Color.White
                    );

                    // Draw text
                    TextHelper.DrawString(
                        _font,
                        pitchText,
                        new Vector2(15, yPosition + 3),
                        Color.White,
                        Vector2.Zero,
                        true
                    );

                    // Update Y position for next element
                    yPosition += (int)pitchTextSize.Y + 5;

                    // Draw jump level
                    string jumpText = $"Jump Level: {_jumpLevel}/{AppConstants.VoiceJump.MAX_JUMP_LEVEL}";
                    Vector2 jumpTextSize = _font.MeasureString(jumpText);

                    // Draw background
                    Game1.spriteBatch.Draw(
                        _bgTexture,
                        new Rectangle(10, yPosition, (int)jumpTextSize.X + 10, (int)jumpTextSize.Y + 6),
                        Color.White
                    );

                    // Draw text
                    TextHelper.DrawString(
                        _font,
                        jumpText,
                        new Vector2(15, yPosition + 3),
                        Color.White,
                        Vector2.Zero,
                        true
                    );
                }
                else
                {
                    // For no pitch, just display a cleaner message
                    string noPitchText = "No pitch detected";
                    Vector2 noPitchTextSize = _font.MeasureString(noPitchText);

                    // Draw background
                    Game1.spriteBatch.Draw(
                        _bgTexture,
                        new Rectangle(10, yPosition, (int)noPitchTextSize.X + 10, (int)noPitchTextSize.Y + 6),
                        Color.White
                    );

                    // Draw text
                    TextHelper.DrawString(
                        _font,
                        noPitchText,
                        new Vector2(15, yPosition + 3),
                        Color.Yellow,
                        Vector2.Zero,
                        true
                    );
                }

                // Draw gauge if enabled
                if (_showGauge && _gaugeTexture != null)
                {
                    DrawGauge();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error in DrawVisualization", ex);
            }
        }

        /// <summary>
        /// Draw the gauge visualization with proper audio conventions
        /// </summary>
        private void DrawGauge()
        {
            try
            {
                // Check if data is stale (older than 2 seconds)
                bool staleData = (DateTime.Now - _lastDataTime).TotalSeconds > 2;

                // Don't show gauge at all if data is stale
                if (staleData)
                    return;

                // Calculate normalized sizes for different screen/window sizes
                int screenHeight = Game1.instance.GraphicsDevice.Viewport.Height;
                int screenWidth = Game1.instance.GraphicsDevice.Viewport.Width;

                // Position the gauge on the right side
                int gaugeWidth = 20;
                int gaugeHeight = 150;
                int gaugeX = screenWidth - gaugeWidth - 10;
                int gaugeY = 10;

                // Draw background
                Game1.spriteBatch.Draw(
                    _bgTexture,
                    new Rectangle(gaugeX - 5, gaugeY - 5, gaugeWidth + 10, gaugeHeight + 10),
                    Color.White
                );

                // Draw gauge background (empty)
                Game1.spriteBatch.Draw(
                    _gaugeTexture,
                    new Rectangle(gaugeX, gaugeY, gaugeWidth, gaugeHeight),
                    new Rectangle(0, 0, 4, 100),
                    new Color(50, 50, 50, 200)
                );

                // Determine if we're showing jump level or audio level
                bool showingJumpLevel = _currentPitch > 0;

                // Calculate fill level
                float fillLevel = 0f;
                if (showingJumpLevel)
                {
                    // Map jump level (0-35) to fill level (0-1)
                    fillLevel = Math.Min(_jumpLevel / (float)AppConstants.VoiceJump.MAX_JUMP_LEVEL, 1f);

                    // For jump level, higher values = higher fill, so no inversion needed
                }
                else
                {
                    // No pitch detected, show audio level
                    // Map audio level (-60dB to 0dB) to fill level (0-1)
                    // For audio level, 0dB = top (full), -60dB = bottom (empty)
                    fillLevel = Math.Min(Math.Max((_audioLevelDb + 60) / 60f, 0f), 1f);
                }

                // Draw filled portion if there's a level
                if (fillLevel > 0)
                {
                    int fillHeight = (int)(gaugeHeight * fillLevel);
                    Game1.spriteBatch.Draw(
                        _gaugeTexture,
                        new Rectangle(gaugeX, gaugeY + gaugeHeight - fillHeight, gaugeWidth, fillHeight),
                        new Rectangle(0, 100 - (int)(100 * fillLevel), 4, (int)(100 * fillLevel)),
                        Color.White
                    );
                }

                // Display different markers based on what we're showing
                if (showingJumpLevel)
                {
                    // Draw jump level marks along the gauge
                    int[] jumpLevels = { 0, 5, 11, 17, 23, 29, 35 };

                    for (int i = 0; i < jumpLevels.Length; i++)
                    {
                        // Calculate position based on jump level (0-35 range)
                        float markerLevel = jumpLevels[i] / (float)AppConstants.VoiceJump.MAX_JUMP_LEVEL;
                        int markerY = gaugeY + gaugeHeight - (int)(gaugeHeight * markerLevel);

                        // Draw marker line
                        Game1.spriteBatch.Draw(
                            _bgTexture,
                            new Rectangle(gaugeX - 5, markerY, gaugeWidth + 10, 1),
                            Color.White
                        );

                        // Draw the label with the actual jump level
                        string label = jumpLevels[i].ToString();
                        Vector2 labelSize = _font.MeasureString(label);

                        TextHelper.DrawString(
                            _font,
                            label,
                            new Vector2(gaugeX - labelSize.X - 8, markerY - labelSize.Y / 2),
                            Color.White,
                            Vector2.Zero,
                            true
                        );
                    }
                }
                else
                {
                    // Draw audio level marks like a mixer (0dB at top, negative values going down)
                    int[] dbLevels = { 0, -10, -20, -30, -40, -50, -60 };

                    for (int i = 0; i < dbLevels.Length; i++)
                    {
                        // Calculate position based on dB level (-60 to 0 range)
                        // Note the inverted calculation so 0 is at the top
                        float markerLevel = (dbLevels[i] + 60) / 60f;
                        int markerY = gaugeY + (int)(gaugeHeight * (1.0f - markerLevel));

                        // Draw marker line
                        Game1.spriteBatch.Draw(
                            _bgTexture,
                            new Rectangle(gaugeX - 5, markerY, gaugeWidth + 10, 1),
                            Color.White
                        );

                        // Draw the label with the dB value (including negative sign for negative values)
                        string label = dbLevels[i].ToString();
                        Vector2 labelSize = _font.MeasureString(label);

                        TextHelper.DrawString(
                            _font,
                            label,
                            new Vector2(gaugeX - labelSize.X - 8, markerY - labelSize.Y / 2),
                            Color.White,
                            Vector2.Zero,
                            true
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error drawing gauge", ex);
            }
        }

        /// <summary>
        /// Get color based on audio level
        /// </summary>
        private Color GetLevelColor(float dbLevel)
        {
            // Map dB level to color with better thresholds for typical mic input
            if (dbLevel < -45)
                return Color.Red; // Very low - red
            else if (dbLevel < -30)
                return Color.Yellow; // Low - yellow
            else
                return Color.LightGreen; // Good - green
        }

        /// <summary>
        /// Reset state (for debugging)
        /// </summary>
        public void ResetState()
        {
            _updateCount = 0;
            _dataReceived = false;
            _lastDataTime = DateTime.MinValue;
            Log.Info("VoiceVisualizer state reset");
        }
        public void StartDisplay()
        {
            // Reset state whenever display is started
            _dataReceived = false;
            _updateCount = 0;
            _lastDataTime = DateTime.MinValue;
            _isEnabled = true;

            Log.Info("Voice visualizer display activated - ready to show data");
        }
    }
}