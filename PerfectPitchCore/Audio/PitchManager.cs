using System;
using System.Collections.Generic;
using NAudio.Wave;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Central class that manages audio processing and pitch detection with a simplified plugin system
    /// </summary>
    public class PitchManager : IDisposable
    {
        private WaveInEvent waveIn;
        private IPitchDetector pitchDetector;
        private readonly List<IPitchProcessor> pitchProcessors = new List<IPitchProcessor>();
        private bool disposed = false;
        private float currentAudioLevel = 0;
        private PitchConfig currentConfig;

        /// <summary>
        /// Configuration for the pitch detection system
        /// </summary>
        public class PitchConfig
        {
            public float BasePitch { get; set; } = 130.81f;
            public int DeviceNumber { get; set; } = 0;
            public string Algorithm { get; set; } = "yin";
            public float MaxFrequency { get; set; } = 2000.0f;
            public int MinFrequency { get; set; } = 40;
            public float VolumeThresholdDb { get; set; } = -30.0f;
            public float PitchSensitivity { get; set; } = 0.2f;
            public int SampleRate { get; set; } = 44100;

            // Added stability settings
            public int StabilityHistory { get; set; } = 3;
            public int StabilityThreshold { get; set; } = 2;
        }

        /// <summary>
        /// Constructor with default configuration
        /// </summary>
        public PitchManager() : this(new PitchConfig()) { }

        /// <summary>
        /// Constructor with custom configuration
        /// </summary>
        public PitchManager(PitchConfig config)
        {
            this.currentConfig = config;
            InitializeAudio(config);
        }

        /// <summary>
        /// Initialize audio input and pitch detection
        /// </summary>
        private void InitializeAudio(PitchConfig config)
        {
            // Create pitch detector based on algorithm
            pitchDetector = CreatePitchDetector(config);

            // Configure audio input
            int bufferSize = pitchDetector.GetNeededSampleCount();
            int bufferMilliseconds = (int)((bufferSize * 1000.0) / config.SampleRate);
            Console.WriteLine($"PitchManager: Setting buffer to {bufferMilliseconds} ms for {bufferSize} samples");

            waveIn = new WaveInEvent
            {
                DeviceNumber = config.DeviceNumber,
                WaveFormat = new WaveFormat(config.SampleRate, 1),
                BufferMilliseconds = bufferMilliseconds
            };

            // Set up audio processing
            waveIn.DataAvailable += (sender, e) => ProcessAudioData(e.Buffer, e.BytesRecorded, config);
        }

        /// <summary>
        /// Update pitch detection configuration
        /// </summary>
        public void UpdateConfiguration(PitchConfig newConfig)
        {
            // Store updated configuration
            bool needsRestart = currentConfig.Algorithm != newConfig.Algorithm ||
                               currentConfig.DeviceNumber != newConfig.DeviceNumber ||
                               currentConfig.SampleRate != newConfig.SampleRate;

            // Simple approach - always stop and restart if needed
            bool wasRunning = false;

            if (needsRestart && waveIn != null)
            {
                try
                {
                    waveIn.StopRecording();
                    wasRunning = true;
                }
                catch
                {
                    // If stopping fails, we weren't recording
                    wasRunning = false;
                }
            }

            // Update configuration
            currentConfig = newConfig;

            if (needsRestart)
            {
                // Clean up existing resources
                waveIn?.Dispose();
                pitchDetector?.Dispose();

                // Reinitialize with new configuration
                InitializeAudio(newConfig);

                // Restart if necessary
                if (wasRunning)
                {
                    waveIn.StartRecording();
                }
            }
            else if (!needsRestart && pitchDetector is AubioPitchDetector aubioDetector)
            {
                // Update configurable parameters if using Aubio
                aubioDetector.SetConfidenceThreshold(newConfig.PitchSensitivity);
                // Silence threshold could be adjusted based on volumeThresholdDb if needed
            }

            Console.WriteLine("Configuration updated");
        }

        /// <summary>
        /// Register a pitch processor that will receive pitch events
        /// </summary>
        public void RegisterProcessor(IPitchProcessor processor)
        {
            if (!pitchProcessors.Contains(processor))
            {
                pitchProcessors.Add(processor);
            }
        }

        /// <summary>
        /// Unregister a pitch processor
        /// </summary>
        public void UnregisterProcessor(IPitchProcessor processor)
        {
            pitchProcessors.Remove(processor);
        }

        /// <summary>
        /// Start audio processing
        /// </summary>
        public void Start()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PitchManager));

            waveIn.StartRecording();
            Console.WriteLine("Audio processing started");
        }

        /// <summary>
        /// Stop audio processing
        /// </summary>
        public void Stop()
        {
            if (!disposed && waveIn != null)
                waveIn.StopRecording();
        }

        /// <summary>
        /// Process incoming audio data
        /// </summary>
        private void ProcessAudioData(byte[] buffer, int bytesRecorded, PitchConfig config)
        {
            try
            {
                float audioLevel = CalculateAudioLevel(buffer, bytesRecorded);
                currentAudioLevel = audioLevel;
                float audioLevelDb = ConvertToDecibels(audioLevel);

                // Process audio through pitch detector
                pitchDetector.ProcessAudioData(buffer, bytesRecorded, audioLevelDb);

                // Create pitch data for all processors
                var pitchData = new PitchData
                {
                    Pitch = pitchDetector.GetCurrentPitch(),
                    AudioLevel = audioLevel,
                    AudioLevelDb = audioLevelDb,
                    BasePitch = config.BasePitch,
                    Timestamp = DateTime.Now
                };

                // Calculate jump level using the calculator
                pitchData.JumpLevel = JumpLevelCalculator.CalculateJumpLevel(pitchData.Pitch, config.BasePitch);

                bool hasPitch = pitchDetector.IsPitchDetected() && audioLevelDb > config.VolumeThresholdDb;

                // Process all registered processors
                foreach (var processor in pitchProcessors)
                {
                    try
                    {
                        // Either the processor wants all events OR we have a valid pitch
                        if (processor.ReceiveAllAudioEvents || hasPitch)
                        {
                            processor.ProcessPitch(pitchData);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other processors
                        CoreLogger.Error($"Error processing pitch in {processor.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                CoreLogger.Error($"Error in ProcessAudioData: {ex.Message}");
            }
        }

        /// <summary>
        /// Create the appropriate pitch detector based on the algorithm name
        /// </summary>
        private IPitchDetector CreatePitchDetector(PitchConfig config)
        {
            return PitchDetectorFactory.CreateDetector(
                config.Algorithm,
                config.SampleRate,
                config.BasePitch,
                config.MaxFrequency,
                config.VolumeThresholdDb,
                config.PitchSensitivity);
        }

        /// <summary>
        /// Calculate RMS audio level from raw buffer
        /// </summary>
        private float CalculateAudioLevel(byte[] buffer, int bytesRecorded)
        {
            float sum = 0;
            int sampleCount = bytesRecorded / 2;

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                float normalized = sample / 32768f;
                sum += normalized * normalized;
            }

            return (float)Math.Sqrt(sum / sampleCount);
        }

        /// <summary>
        /// Convert linear audio level to decibels
        /// </summary>
        public float ConvertToDecibels(float linearLevel)
        {
            if (linearLevel < 0.00001f)
                return -100.0f;

            return 20.0f * (float)Math.Log10(linearLevel);
        }

        /// <summary>
        /// Get the current audio level
        /// </summary>
        public float GetCurrentAudioLevel()
        {
            return currentAudioLevel;
        }

        /// <summary>
        /// Get available microphones
        /// </summary>
        public static List<string> GetAvailableMicrophones()
        {
            var deviceList = new List<string>();
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                deviceList.Add($"{i}: {capabilities.ProductName}");
            }
            return deviceList;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                waveIn?.StopRecording();
                waveIn?.Dispose();
                pitchDetector?.Dispose();

                // Dispose any processors that implement IDisposable
                foreach (var processor in pitchProcessors)
                {
                    if (processor is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                pitchProcessors.Clear();
                disposed = true;
            }
        }

        /// <summary>
        /// Get the current configuration
        /// </summary>
        public PitchConfig CurrentConfig => currentConfig;

        // Add these fields to support recalibration
        private PitchConfig originalConfig = null;
        private bool inRecalibrationMode = false;

        /// <summary>
        /// Temporarily switch to a wide-range configuration for recalibration
        /// </summary>
        public void TemporaryRecalibrationConfig(PitchConfig calibrationConfig)
        {
            if (inRecalibrationMode)
                return; // Already in recalibration mode

            // Save original config
            originalConfig = new PitchConfig
            {
                Algorithm = currentConfig.Algorithm,
                BasePitch = currentConfig.BasePitch,
                DeviceNumber = currentConfig.DeviceNumber,
                MaxFrequency = currentConfig.MaxFrequency,
                MinFrequency = currentConfig.MinFrequency,
                PitchSensitivity = currentConfig.PitchSensitivity,
                SampleRate = currentConfig.SampleRate,
                VolumeThresholdDb = currentConfig.VolumeThresholdDb,
                StabilityHistory = currentConfig.StabilityHistory,
                StabilityThreshold = currentConfig.StabilityThreshold
            };

            // Switch to calibration config
            inRecalibrationMode = true;
            UpdateConfiguration(calibrationConfig);

            Console.WriteLine("Switched to temporary wide-range configuration for recalibration");
        }

        /// <summary>
        /// Restore the original configuration after recalibration
        /// </summary>
        public void RestoreConfigAfterRecalibration(float? newBasePitch = null)
        {
            if (!inRecalibrationMode || originalConfig == null)
                return;

            // Create a config with the original settings but potentially new base pitch
            PitchConfig restoredConfig = new PitchConfig
            {
                Algorithm = originalConfig.Algorithm,
                BasePitch = newBasePitch ?? originalConfig.BasePitch,
                DeviceNumber = originalConfig.DeviceNumber,
                MaxFrequency = originalConfig.MaxFrequency,
                MinFrequency = originalConfig.MinFrequency,
                PitchSensitivity = originalConfig.PitchSensitivity,
                SampleRate = originalConfig.SampleRate,
                VolumeThresholdDb = originalConfig.VolumeThresholdDb,
                StabilityHistory = originalConfig.StabilityHistory,
                StabilityThreshold = originalConfig.StabilityThreshold
            };

            // Update the configuration
            inRecalibrationMode = false;
            UpdateConfiguration(restoredConfig);

            // Clear the saved original config
            originalConfig = null;

            Console.WriteLine($"Restored configuration after recalibration" +
                (newBasePitch.HasValue ? $" with new base pitch: {newBasePitch.Value:F1} Hz" : ""));
        }
    }
}