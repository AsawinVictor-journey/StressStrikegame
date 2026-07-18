using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class YogaTracker : MonoBehaviour
{
    // ESP32Glove is an InputDevice, not a UnityEngine.Object, so it can't be
    // wired up in the Inspector — it has to be fetched from the Input System.
    ESP32Glove glove;

    [Header("Current Target")]
    public Vector3 targetRotation;

    [Header("Score")]
    public float accuracy;
    public TMP_Text accuracyText;

    public float smoothSpeed = 5f;
    public bool tracking;

    [Header("Calibration")]
    [Tooltip("Arm angle (degrees) away from the target at which accuracy reaches 0. " +
             "Smaller = stricter pose matching. Within this cone you earn partial " +
             "credit; dead-on the target = 100%.")]
    public float maxAngle = 60f;

    // Neutral reference captured by Recenter(). All accuracy is measured RELATIVE
    // to this, so pose targets stay valid even though the BNO055's absolute heading
    // shifts between sessions. Runtime-only — not authored in the Inspector.
    Quaternion zeroOffset = Quaternion.identity;

    [Header("Session Result")]
    [Tooltip("How often (seconds) accuracy is sampled for the end-of-session result.")]
    public float sampleInterval = 0.2f;

    [Tooltip("Average accuracy across the whole hold, computed when tracking stops.")]
    public float alignment;

    [Tooltip("How little accuracy wobbled during the hold (100 = rock steady).")]
    public float steadiness;

    readonly List<float> accuracySamples = new List<float>();
    float sampleTimer;

    void Update()
    {
        if (!tracking)
            return;

        // `!glove.added` guards a stale reference after the glove disconnects —
        // reading a removed device throws "before 'ESP32Glove' has been added".
        if (glove == null || !glove.added)
        {
            glove = InputSystem.GetDevice<ESP32Glove>();
            if (glove == null) return;
        }


        Quaternion current = GetGloveRotation();

        Quaternion target = Quaternion.Euler(targetRotation);

        float newAccuracy = CalculateAccuracy(
            current,
            target
        );


        accuracy = Mathf.Lerp(
            accuracy,
            newAccuracy,
            Time.deltaTime * smoothSpeed
        );


        if(accuracyText != null)
        {
            accuracyText.text =
                "Accuracy: " +
                Mathf.RoundToInt(accuracy) +
                "%";
        }

        sampleTimer += Time.deltaTime;
        if (sampleTimer >= sampleInterval)
        {
            sampleTimer = 0f;
            accuracySamples.Add(accuracy);
        }
    }


    public void SetTargetPose(Vector3 rotation)
    {
        targetRotation = rotation;
    }


    Quaternion GetRawGloveRotation()
    {
        // Same BNO055 -> Unity axis remap that VRGloveProcessor uses.
        float qX = glove.x.ReadValue();
        float qY = glove.y.ReadValue();
        float qZ = glove.z.ReadValue();
        float qW = glove.w.ReadValue();

        return new Quaternion(-qY, -qZ, qX, qW).normalized;
    }

    // Orientation relative to the recentered neutral — this is what pose targets
    // are compared against.
    Quaternion GetGloveRotation()
    {
        return Quaternion.Inverse(zeroOffset) * GetRawGloveRotation();
    }

    /// <summary>Current glove orientation relative to the neutral reference.
    /// Used by YogaPoseCalibrator to capture a real target for a pose.</summary>
    public Quaternion CurrentRecentered
    {
        get
        {
            if (glove == null || !glove.added) glove = InputSystem.GetDevice<ESP32Glove>();
            return glove == null ? Quaternion.identity : GetGloveRotation();
        }
    }

    /// <summary>Capture the glove's current orientation as the neutral reference.
    /// Call while the player holds a relaxed/neutral pose, so every pose is scored
    /// relative to it. Returns false if no glove is connected.</summary>
    public bool Recenter()
    {
        if (glove == null || !glove.added) glove = InputSystem.GetDevice<ESP32Glove>();
        if (glove == null) return false;
        zeroOffset = GetRawGloveRotation();
        return true;
    }


    float CalculateAccuracy(
        Quaternion current,
        Quaternion target)
    {
        float difference = Quaternion.Angle(current, target);

        // Linear falloff: 0 deg off = 100%, maxAngle deg off (or more) = 0%.
        float span = Mathf.Max(1f, maxAngle);
        return Mathf.Clamp(100f * (1f - difference / span), 0f, 100f);
    }

    public void StartTracking()
    {
        tracking = true;
        accuracySamples.Clear();
        sampleTimer = 0f;
    }


    public void StopTracking()
    {
        tracking = false;
        CalculateSessionResult();
    }

    void CalculateSessionResult()
    {
        if (accuracySamples.Count == 0)
        {
            alignment = accuracy;
            steadiness = 100f;
            return;
        }

        float sum = 0f;
        foreach (float sample in accuracySamples)
            sum += sample;
        float mean = sum / accuracySamples.Count;

        float variance = 0f;
        foreach (float sample in accuracySamples)
            variance += (sample - mean) * (sample - mean);
        variance /= accuracySamples.Count;
        float stdDev = Mathf.Sqrt(variance);

        alignment = mean;

        // A stdDev of ~30 or more reads as very shaky; 0 is rock steady.
        steadiness = Mathf.Clamp(100f - (stdDev / 30f) * 100f, 0f, 100f);
    }
}