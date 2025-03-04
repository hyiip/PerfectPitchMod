using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PerfectPitchCore.Utils;

namespace PerfectPitchCore.Audio
{
    /// <summary>
    /// Processor for calibrating the user's vocal range with proper reset support
    /// </summary>
    public class CalibrationProcessor : IPitchProcessor, IDisposable
    {
        // Calibration states
        private enum CalibrationState
        {
            NotStarted,
            Preparing,
            Recording,
            Analyzing,
            Completed,
            Failed
        }

        // Calibration events
        public event Action<string> CalibrationStatusChanged;
        public event Action<float> CalibrationCompleted;
        public event Action CalibrationFailed;
        public event Action<float> PitchDetected;
        public event Action<int> CountdownTick;
        public event Action<List<float>> CalibrationDataCollected;
        public event Action RecalibrationStarted;

        // Calibration settings
        private readonly int countdownSeconds = 3;
        private readonly int recordingSeconds = 5;
        private readonly int minSamplesToCalibrate = 10;

        // Human voice frequency ranges for validation (very wide range to allow all possibilities)
        private readonly float minValidPitch = 50.0f;  // Minimum valid pitch (very low, but possible)
        private readonly float maxValidPitch = 2000.0f; // Maximum valid pitch (very high, but possible)
        public static bool VerboseCalibrationLogging { get; set; } = false;

        // State management
        private CalibrationState state = CalibrationState.NotStarted;
        private CancellationTokenSource cancellationTokenSource;
        private List<float> detectedPitches = new List<float>();
        private float calibratedBasePitch = 130.81f; // Default to C3
        private string calibratedNoteName = "C3";
        private DateTime calibrationStartTime;
        private bool disposedValue;
        private int calibrationCount = 0; // Track how many times we've calibrated

        // Reference to the PitchManager for recalibration
        private PitchManager pitchManager;

        /// <summary>
        /// Create a new calibration processor
        /// </summary>
        public CalibrationProcessor()
        {
        }

        /// <summary>
        /// Set the pitch manager for recalibration support
        /// </summary>
        public void SetPitchManager(PitchManager manager)
        {
            this.pitchManager = manager;
        }

        /// <summary>
        /// Start the calibration process
        /// </summary>
        public void StartCalibration()
        {
            // Increment calibration count
            calibrationCount++;

            // Log this is a recalibration
            if (calibrationCount > 1)
            {
                Console.WriteLine($"Starting RECALIBRATION #{calibrationCount}");

                // If we have a pitch manager, create a temporary detector optimized for wide range
                if (pitchManager != null)
                {
                    // Notify listeners that recalibration is beginning
                    RecalibrationStarted?.Invoke();

                    // Create a temporary wide-range configuration for recalibration
                    var wideRangeConfig = new PitchManager.PitchConfig
                    {
                        // Copy existing config
                        Algorithm = pitchManager.CurrentConfig.Algorithm,
                        DeviceNumber = pitchManager.CurrentConfig.DeviceNumber,
                        SampleRate = pitchManager.CurrentConfig.SampleRate,

                        // Override with wide-range settings
                        BasePitch = 65.0f, // Low C2 - ensures we can detect low voices
                        MaxFrequency = 2000.0f, // High enough for any voice
                        VolumeThresholdDb = pitchManager.CurrentConfig.VolumeThresholdDb,
                        PitchSensitivity = 0.15f, // More sensitive for calibration
                        MinFrequency = 30 // Very low minimum to catch deeper voices
                    };

                    // Temporarily update configuration for calibration
                    pitchManager.TemporaryRecalibrationConfig(wideRangeConfig);
                }
            }

            if (state != CalibrationState.NotStarted && state != CalibrationState.Completed && state != CalibrationState.Failed)
                return;

            // Reset state
            state = CalibrationState.Preparing;
            detectedPitches.Clear();

            // Create cancellation token for async operations
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            // Start the calibration workflow
            Task.Run(() => RunCalibrationAsync(cancellationTokenSource.Token));
        }

        /// <summary>
        /// Cancel the calibration process
        /// </summary>
        public void CancelCalibration()
        {
            cancellationTokenSource?.Cancel();
            state = CalibrationState.NotStarted;
            UpdateStatus("Calibration cancelled");

            // If temporary config was in use, restore it
            if (calibrationCount > 1 && pitchManager != null)
            {
                pitchManager.RestoreConfigAfterRecalibration();
            }
        }

        /// <summary>
        /// Process pitch data during calibration
        /// </summary>
        public void ProcessPitch(PitchData pitchData)
        {
            // Only process pitch when actively recording
            if (state != CalibrationState.Recording)
                return;

            if (pitchData.Pitch > 0)
            {
                // Add to detected pitches - don't filter here to ensure we catch the full range
                detectedPitches.Add(pitchData.Pitch);
                PitchDetected?.Invoke(pitchData.Pitch);

                // Log during recalibration to see what's happening
                if (calibrationCount > 1 && detectedPitches.Count % 10 == 0)
                {
                    Console.WriteLine($"Recalibration - detected pitch: {pitchData.Pitch:F1} Hz ({NoteUtility.GetNoteName(pitchData.Pitch)})");
                }
            }
        }

        /// <summary>
        /// Get the calibrated base pitch
        /// </summary>
        public float GetCalibratedBasePitch() => calibratedBasePitch;

        /// <summary>
        /// Get the name of the calibrated note
        /// </summary>
        public string GetCalibratedNoteName() => calibratedNoteName;

        /// <summary>
        /// Run the full calibration workflow
        /// </summary>
        private async Task RunCalibrationAsync(CancellationToken token)
        {
            try
            {
                // Start with countdown
                UpdateStatus("Preparing to detect your lowest comfortable note");
                await Task.Delay(1000, token);

                // Countdown
                for (int i = countdownSeconds; i > 0; i--)
                {
                    if (token.IsCancellationRequested) return;
                    CountdownTick?.Invoke(i);
                    UpdateStatus($"Get ready to sing your lowest comfortable note in {i}...");
                    await Task.Delay(1000, token);
                }

                // Start recording
                state = CalibrationState.Recording;
                calibrationStartTime = DateTime.Now;
                UpdateStatus("Now sing and hold your lowest comfortable note...");

                // Record for specified duration
                int elapsed = 0;
                int totalMs = recordingSeconds * 1000;
                int intervalMs = 100;

                while (elapsed < totalMs)
                {
                    if (token.IsCancellationRequested) return;
                    await Task.Delay(intervalMs, token);
                    elapsed += intervalMs;

                    // Update status with progress
                    float progress = (float)elapsed / totalMs;
                    UpdateStatus($"Singing... {(int)(progress * 100)}%");
                }

                // Analyze results
                state = CalibrationState.Analyzing;
                UpdateStatus("Analyzing your vocal range...");
                await Task.Delay(500, token); // Brief pause for UI feedback

                // Log the raw data distribution (verbose during recalibration)
                if (calibrationCount > 1)
                {
                    Console.WriteLine($"RECALIBRATION #{calibrationCount} - Raw pitch data:");
                    if (detectedPitches.Count > 0)
                    {
                        var pitchesCopy = new List<float>(detectedPitches);
                        pitchesCopy.Sort();

                        Console.WriteLine($"Min: {pitchesCopy.First():F1} Hz, Max: {pitchesCopy.Last():F1} Hz, Count: {pitchesCopy.Count}");
                        Console.WriteLine($"25%: {pitchesCopy[pitchesCopy.Count / 4]:F1} Hz, 50%: {pitchesCopy[pitchesCopy.Count / 2]:F1} Hz");

                        // Log 10 representative samples
                        int step = Math.Max(1, pitchesCopy.Count / 10);
                        for (int i = 0; i < pitchesCopy.Count; i += step)
                        {
                            Console.WriteLine($"Sample {i}: {pitchesCopy[i]:F1} Hz ({NoteUtility.GetNoteName(pitchesCopy[i])})");
                        }
                    }
                }

                // Notify listeners of the raw data for visualization or debugging
                if (CalibrationDataCollected != null)
                {
                    CalibrationDataCollected.Invoke(new List<float>(detectedPitches));
                }

                // Process the collected pitches
                if (detectedPitches.Count >= minSamplesToCalibrate)
                {
                    // Apply robust statistical analysis to determine the base pitch
                    float basePitch = CalculateRobustBasePitch(detectedPitches);

                    if (basePitch > 0)
                    {
                        // Round to the nearest semitone frequency
                        calibratedBasePitch = RoundToNearestSemitone(basePitch);
                        calibratedNoteName = NoteUtility.GetNoteName(calibratedBasePitch);

                        // Complete the calibration
                        state = CalibrationState.Completed;

                        // If this was a recalibration, restore the normal configuration
                        if (calibrationCount > 1 && pitchManager != null)
                        {
                            // Update with the new base pitch
                            pitchManager.RestoreConfigAfterRecalibration(calibratedBasePitch);
                        }

                        UpdateStatus($"Calibration successful! Your base note is {calibratedNoteName} ({calibratedBasePitch:F2} Hz)");
                        CalibrationCompleted?.Invoke(calibratedBasePitch);
                    }
                    else
                    {
                        // No valid base pitch detected
                        state = CalibrationState.Failed;

                        // Restore configuration if needed
                        if (calibrationCount > 1 && pitchManager != null)
                        {
                            pitchManager.RestoreConfigAfterRecalibration();
                        }

                        UpdateStatus("Calibration failed to find a consistent pitch. Please try again.");
                        CalibrationFailed?.Invoke();
                    }
                }
                else
                {
                    // Not enough samples
                    state = CalibrationState.Failed;

                    // Restore configuration if needed
                    if (calibrationCount > 1 && pitchManager != null)
                    {
                        pitchManager.RestoreConfigAfterRecalibration();
                    }

                    UpdateStatus("Calibration failed. Please try again in a louder environment.");
                    CalibrationFailed?.Invoke();
                }
            }
            catch (Exception ex)
            {
                state = CalibrationState.Failed;

                // Restore configuration if needed
                if (calibrationCount > 1 && pitchManager != null)
                {
                    pitchManager.RestoreConfigAfterRecalibration();
                }

                UpdateStatus($"Error during calibration: {ex.Message}");
                CalibrationFailed?.Invoke();
            }
        }

        /// <summary>
        /// Calculate a robust base pitch estimate using advanced statistical methods
        /// </summary>
        private float CalculateRobustBasePitch(List<float> pitches)
        {
            if (pitches.Count < minSamplesToCalibrate)
                return 0;

            // Step 1: Remove extreme outliers before analysis
            var filteredPitches = new List<float>();
            if (pitches.Count > 20)
            {
                // Only filter if we have enough samples
                var sortedPitches = new List<float>(pitches);
                sortedPitches.Sort();

                // Calculate Q1 and Q3 (first and third quartiles)
                int q1Index = sortedPitches.Count / 4;
                int q3Index = (sortedPitches.Count * 3) / 4;
                float q1 = sortedPitches[q1Index];
                float q3 = sortedPitches[q3Index];

                // Calculate IQR (Interquartile Range)
                float iqr = q3 - q1;

                // Filter out values more than 1.5 * IQR from the quartiles
                float lowerBound = q1 - (1.5f * iqr);
                float upperBound = q3 + (1.5f * iqr);

                // Add values within bounds
                foreach (var pitch in pitches)
                {
                    if (pitch >= lowerBound && pitch <= upperBound)
                    {
                        filteredPitches.Add(pitch);
                    }
                }

                Console.WriteLine($"Filtered outliers: {pitches.Count - filteredPitches.Count} removed from {pitches.Count} samples");
            }
            else
            {
                // Not enough samples to reliably filter outliers
                filteredPitches = new List<float>(pitches);
            }

            // Step 2: Find clusters in the data using semitone histogram
            var semitoneHistogram = new Dictionary<int, int>();
            foreach (var pitch in filteredPitches)
            {
                // Convert to semitones relative to A4 (440Hz)
                int semitone = (int)Math.Round(12 * Math.Log(pitch / 440.0, 2));
                if (!semitoneHistogram.ContainsKey(semitone))
                    semitoneHistogram[semitone] = 0;
                semitoneHistogram[semitone]++;
            }

            // Find the most common semitone clusters
            var orderedClusters = semitoneHistogram.OrderByDescending(kv => kv.Value).ToList();

            // Log the identified clusters
            Console.WriteLine($"Pitch clusters ({orderedClusters.Count} found):");
            foreach (var cluster in orderedClusters.Take(Math.Min(5, orderedClusters.Count)))
            {
                float freq = 440.0f * (float)Math.Pow(2, cluster.Key / 12.0);
                Console.WriteLine($"  {NoteUtility.GetNoteName(freq)} ({freq:F1} Hz): {cluster.Value} samples ({(float)cluster.Value / filteredPitches.Count:P1})");
            }

            // Step 3: For recalibration, specifically look for lower pitch clusters
            if (calibrationCount > 1)
            {
                Console.WriteLine("RECALIBRATION - Looking for the LOWEST consistent pitch cluster");

                // Check if we have any substantial clusters (at least 15% of samples)
                var significantClusters = orderedClusters
                    .Where(c => c.Value >= filteredPitches.Count * 0.15)
                    .ToList();

                if (significantClusters.Count > 0)
                {
                    // Sort by pitch (low to high) rather than by count
                    var sortedByPitch = significantClusters
                        .OrderBy(c => c.Key) // Semitones relative to A4, so lower = lower pitch
                        .ToList();

                    // Take the lowest significant cluster
                    var lowestCluster = sortedByPitch.First();
                    float lowestFreq = 440.0f * (float)Math.Pow(2, lowestCluster.Key / 12.0);

                    Console.WriteLine($"Found LOWEST significant cluster: {NoteUtility.GetNoteName(lowestFreq)} ({lowestFreq:F1} Hz) with {lowestCluster.Value} samples");

                    // Find all pitches close to this cluster (within ±1 semitone)
                    var clusteredPitches = filteredPitches.Where(p =>
                        Math.Abs(12 * Math.Log(p / lowestFreq, 2)) <= 1).ToList();

                    // Use the median of this cluster as our base pitch
                    float medianPitch = GetMedian(clusteredPitches);

                    Console.WriteLine($"RECALIBRATION - Selected {medianPitch:F1} Hz ({NoteUtility.GetNoteName(medianPitch)}) as base pitch");
                    return medianPitch;
                }
            }

            // Standard approach for first calibration or fallback
            // Step 4: Check if we have a clear dominant cluster (with at least 30% of samples)
            if (orderedClusters.Count > 0 && orderedClusters[0].Value >= filteredPitches.Count * 0.3)
            {
                int dominantSemitone = orderedClusters[0].Key;
                float centralFrequency = 440.0f * (float)Math.Pow(2, dominantSemitone / 12.0);

                // Find all pitches close to this cluster (within ±1 semitone)
                var clusteredPitches = filteredPitches.Where(p =>
                    Math.Abs(12 * Math.Log(p / centralFrequency, 2)) <= 1).ToList();

                // Use the median of this cluster as our base pitch
                float medianPitch = GetMedian(clusteredPitches);

                Console.WriteLine($"Identified dominant pitch cluster around {medianPitch:F1} Hz ({NoteUtility.GetNoteName(medianPitch)})");
                return medianPitch;
            }
            else
            {
                // No clear dominant cluster - use the 25th percentile approach
                Console.WriteLine("No dominant pitch cluster found - using percentile-based approach");

                // Fall back to using the lowest 25th percentile as the base pitch
                var sortedPitches = new List<float>(filteredPitches);
                sortedPitches.Sort();

                // Get the pitch at the 25th percentile
                int lowIndex = Math.Max(0, sortedPitches.Count / 4);
                float lowPitch = sortedPitches[lowIndex];

                Console.WriteLine($"Using 25th percentile pitch: {lowPitch:F1} Hz ({NoteUtility.GetNoteName(lowPitch)})");
                return lowPitch;
            }
        }

        /// <summary>
        /// Get the median value from a list of floats
        /// </summary>
        private float GetMedian(List<float> values)
        {
            if (values.Count == 0)
                return 0;

            var sorted = new List<float>(values);
            sorted.Sort();

            int mid = sorted.Count / 2;
            if (sorted.Count % 2 == 0)
                return (sorted[mid - 1] + sorted[mid]) / 2;
            else
                return sorted[mid];
        }

        /// <summary>
        /// Round a frequency to the nearest semitone
        /// </summary>
        private float RoundToNearestSemitone(float frequencyHz)
        {
            // A4 is 440Hz (MIDI note 69)
            const float A4 = 440.0f;

            // Calculate how many semitones away from A4
            float semitones = 12 * (float)Math.Log(frequencyHz / A4, 2);

            // Round to nearest semitone
            int roundedSemitones = (int)Math.Round(semitones);

            // Convert back to frequency
            return A4 * (float)Math.Pow(2, roundedSemitones / 12.0);
        }

        /// <summary>
        /// Update the calibration status
        /// </summary>
        private void UpdateStatus(string status)
        {
            CalibrationStatusChanged?.Invoke(status);
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource?.Dispose();
                }
                disposedValue = true;
            }
        }
    }
}