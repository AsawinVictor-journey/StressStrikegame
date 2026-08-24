using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of truth for heart-rate data coming off the ESP32 glove's
/// pulse sensor (ESP32Glove.heartRate). Survives scene loads so a calibration
/// done in the menu is still valid inside Rage Room / Boxing / Meditate.
/// </summary>
public class BiometricEngine : MonoBehaviour
{
    public static BiometricEngine Instance { get; private set; }

    [Header("Biometric Status")]
    public float currentBPM = 0f;
    public float restingBPM = 0f;

    [Tooltip("True while the glove is present and reporting a plausible pulse.")]
    public bool isConnected = false;

    [Header("Game Mechanics")]
    [Tooltip("How much higher the current BPM is compared to resting. Use this to trigger Rage mode!")]
    public float stressDelta = 0f;
    public float caloriesBurned = 0f;

    [Tooltip("SDNN in ms over the current sample window. Higher = calmer / more recovered.")]
    public float currentHRV = 0f;

    [Header("Calibration Settings")]
    [Tooltip("Seconds required at the start of the game to find resting heart rate.")]
    public float calibrationTime = 10f;

    [Tooltip("Seconds between BPM samples used for averaging and HRV.")]
    public float sampleInterval = 1f;

    [Tooltip("Seconds without a valid pulse before we consider the sensor disconnected.")]
    public float signalTimeout = 3f;

    // Any reading at or below this is the sensor idling, not a real pulse.
    const float MinValidBPM = 20f;

    private ESP32Glove gloveDevice;
    private bool isCalibrating = true;
    private float calibrationTimer = 0f;
    private float accumulatedCalibrationBPM = 0f;
    private int calibrationTicks = 0;

    private readonly List<float> windowSamples = new List<float>();
    private float sampleTimer = 0f;
    private float lastValidSignalTime = -999f;

    /// <summary>
    /// Spawns the engine on its own persistent GameObject before the first scene
    /// loads, so every mode (Menu, Rage Room, Boxing, Meditate) can rely on
    /// BiometricEngine.Instance without anyone having to place it in a scene.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        var host = new GameObject(nameof(BiometricEngine));
        host.AddComponent<BiometricEngine>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Only drop the duplicate component — the host GameObject may carry
            // unrelated components (e.g. the Yoga managers) that must survive.
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // DISABLED: Using keyboard controller instead
        return;

        // 1. Find the connected Glove. Re-check `added` too, not just null: when
        // the glove disconnects mid-session the Input System removes the device,
        // but our cached reference stays non-null. Reading a removed device throws
        // "before 'ESP32Glove' has been added to system", so refresh in that case.
        if (gloveDevice == null || !gloveDevice.added)
        {
            gloveDevice = InputSystem.GetDevice<ESP32Glove>();
            if (gloveDevice == null || !gloveDevice.added)
            {
                gloveDevice = null;
                isConnected = false;
                return;
            }
        }

        // 2. Read the raw BPM
        // Unity crushes SHRT to -1 to 1. We multiply by 32767 to restore your true BPM number.
        currentBPM = gloveDevice.heartRate.ReadValue() * 32767f;

        // Skip calculations if the sensor isn't reading a pulse yet (returns 0)
        if (currentBPM <= MinValidBPM)
        {
            if (Time.time - lastValidSignalTime > signalTimeout) isConnected = false;
            return;
        }

        lastValidSignalTime = Time.time;
        isConnected = true;

        SampleForWindow();

        // 3. Calibration Phase (Finding the Baseline)
        if (isCalibrating)
        {
            calibrationTimer += Time.deltaTime;
            accumulatedCalibrationBPM += currentBPM;
            calibrationTicks++;

            if (calibrationTimer >= calibrationTime)
            {
                // Lock in the resting heart rate
                restingBPM = accumulatedCalibrationBPM / calibrationTicks;
                isCalibrating = false;
                Debug.Log($"Biometric Calibration Complete! Resting BPM: {Mathf.Round(restingBPM)}");
            }
            return; // Don't calculate game mechanics while calibrating
        }

        // 4. Active Gameplay Phase (Stress & Calories)
        CalculateStress();
        CalculateCalories();
    }

    private void CalculateStress()
    {
        // Stress Delta is how much faster the heart is beating compared to the baseline
        stressDelta = currentBPM - restingBPM;

        // Prevent negative stress if they relax below their initial baseline
        if (stressDelta < 0f) stressDelta = 0f;
    }

    private void CalculateCalories()
    {
        // Calorie burn approximation per second based on heart rate zones
        float caloriesPerMinute;

        if (currentBPM >= 120f)
        {
            caloriesPerMinute = 10f; // Heavy Smashing Zone
        }
        else if (currentBPM >= 90f)
        {
            caloriesPerMinute = 5f;  // Light Work Zone
        }
        else
        {
            caloriesPerMinute = 1.5f; // Resting / Minimal Movement
        }

        // Add the fraction of calories burned during this specific frame
        caloriesBurned += (caloriesPerMinute / 60f) * Time.deltaTime;
    }

    // --- Sample window (used by the Yoga / Meditate pre- and post-game measurements) ---

    private void SampleForWindow()
    {
        sampleTimer += Time.deltaTime;
        if (sampleTimer < sampleInterval) return;

        sampleTimer = 0f;
        windowSamples.Add(currentBPM);
        currentHRV = ComputeSDNN(windowSamples);
    }

    /// <summary>Clears the sample window. Call this when a measurement period starts.</summary>
    public void BeginSampleWindow()
    {
        windowSamples.Clear();
        sampleTimer = 0f;
        currentHRV = 0f;
    }

    /// <summary>Mean BPM across the current sample window.</summary>
    public float GetAverageBPM()
    {
        if (windowSamples.Count == 0) return currentBPM;

        float sum = 0f;
        foreach (float bpm in windowSamples) sum += bpm;
        return sum / windowSamples.Count;
    }

    /// <summary>SDNN across the current sample window, in milliseconds.</summary>
    public float CalculateFinalHRV()
    {
        currentHRV = ComputeSDNN(windowSamples);
        return currentHRV;
    }

    /// <summary>
    /// The glove reports BPM, not beat-to-beat timing, so this is an SDNN
    /// approximation: each BPM sample is converted to the RR interval it implies
    /// (60000 / BPM ms) and we take the standard deviation of those intervals.
    /// It trends the same way real SDNN does (calmer = higher) but is not
    /// clinically comparable to a chest-strap reading.
    /// </summary>
    private static float ComputeSDNN(List<float> bpmSamples)
    {
        if (bpmSamples.Count < 2) return 0f;

        float sum = 0f;
        foreach (float bpm in bpmSamples) sum += 60000f / bpm;
        float mean = sum / bpmSamples.Count;

        float variance = 0f;
        foreach (float bpm in bpmSamples)
        {
            float rr = 60000f / bpm;
            variance += (rr - mean) * (rr - mean);
        }
        variance /= bpmSamples.Count;

        return Mathf.Sqrt(variance);
    }

    /// <summary>Throws away the current baseline and re-runs the calibration phase.</summary>
    public void RestartCalibration()
    {
        isCalibrating = true;
        calibrationTimer = 0f;
        accumulatedCalibrationBPM = 0f;
        calibrationTicks = 0;
        restingBPM = 0f;
        stressDelta = 0f;
    }
}
